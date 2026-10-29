namespace BellTower.Tests.Tray;

internal static class ReminderFixtureDriver
{
    public static async Task<T> RunAsync<T>(Func<T> work, Action release, TimeSpan timeout)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStarted = 0;
        Exception? releaseError = null;

        void ReleaseHeldWork()
        {
            if (Interlocked.Exchange(ref releaseStarted, 1) != 0)
            {
                return;
            }

            try
            {
                release();
            }
            catch (Exception exception)
            {
                releaseError = exception;
            }
        }

        var driver = new Thread(() =>
        {
            T? result = default;
            Exception? workError = null;
            try
            {
                result = work();
            }
            catch (Exception exception)
            {
                workError = exception;
            }
            finally
            {
                ReleaseHeldWork();
            }

            var errors = new[] { workError, releaseError }.Where(exception => exception is not null).ToArray();
            if (errors.Length == 1)
            {
                completion.TrySetException(errors[0]!);
            }
            else if (errors.Length > 1)
            {
                completion.TrySetException(new AggregateException(errors!));
            }
            else
            {
                completion.TrySetResult(result!);
            }
        })
        { IsBackground = true, Name = "Reminder test fixture driver" };
        try
        {
            driver.Start();
        }
        catch
        {
            ReleaseHeldWork();
            throw;
        }

        T? value = default;
        Exception? operationError = null;
        try
        {
            value = await completion.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            operationError = exception;
        }
        finally
        {
            ReleaseHeldWork();
            if (!driver.Join(timeout))
            {
                var joinError = new TimeoutException("Reminder fixture driver did not stop after its release signal.");
                operationError = operationError is null ? joinError : new AggregateException(operationError, joinError);
            }
        }

        if (releaseError is not null && operationError is not null && !ReferenceEquals(operationError, releaseError) &&
            (operationError is not AggregateException aggregate || !aggregate.InnerExceptions.Contains(releaseError)))
        {
            operationError = new AggregateException(operationError, releaseError);
        }
        else if (releaseError is not null && operationError is null)
        {
            operationError = releaseError;
        }

        if (operationError is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(operationError).Throw();
        }

        return value!;
    }

    public static T[] RunDedicatedConsumers<T>(int count, Func<T> work, TimeSpan timeout)
    {
        var results = new T[count];
        var errors = new Exception?[count];
        var complete = new CountdownEvent(count);
        var threads = Enumerable.Range(0, count).Select(index => new Thread(() =>
        {
            try
            {
                results[index] = work();
            }
            catch (Exception exception)
            {
                errors[index] = exception;
            }
            finally
            {
                complete.Signal();
            }
        })
        { IsBackground = true, Name = "Reminder test consumer" }).ToArray();
        var started = 0;
        try
        {
            foreach (var thread in threads)
            {
                thread.Start();
                started++;
            }

            if (!complete.Wait(timeout))
            {
                throw new TimeoutException("Dedicated reminder fixture consumers did not finish within the test budget.");
            }
        }
        finally
        {
            var unjoined = new List<int>();
            for (var index = 0; index < started; index++)
            {
                if (!threads[index].Join(timeout))
                {
                    unjoined.Add(index);
                }
            }
            complete.Dispose();
            if (unjoined.Count > 0)
            {
                throw new TimeoutException($"Dedicated reminder fixture consumers did not stop: {string.Join(", ", unjoined)}.");
            }
        }

        if (errors.Any(exception => exception is not null))
        {
            throw new AggregateException(errors.Where(exception => exception is not null)!);
        }

        return results;
    }
}
