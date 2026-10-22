using BellTower.Distribution;
using BellTower.IO;

namespace BellTower.Updates;

public sealed class UpdateDiscovery(DataPaths paths, ReleaseClient client, TimeProvider? time = null)
{
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private string CachePath => Path.Combine(paths.Root, "updates", "release-cache.json");
    public UpdateCache Read()
    {
        try
        { return File.Exists(CachePath) ? DistributionJson.Read<UpdateCache>(CachePath) : new(); }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { return new(); }
    }
    public ReleaseOffer? Hint()
    {
        if (!new ConfigStore(paths).Read().Updates.Enabled)
        { return null; }
        var cache = Read();
        try
        {
            cache.Offer?.Validate();
            var selected = Installation.ForPayload(Environment.ProcessPath!)?.Selection().Version ?? Cli.ApplicationInfo.Version;
            return cache.Validated is { } validated && validated <= clock.GetUtcNow() && clock.GetUtcNow() - validated < TimeSpan.FromDays(7)
                && cache.Offer is { } offer && ReleaseContract.Parse(offer.Version) > ReleaseContract.Parse(selected) ? offer : null;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException) { return null; }
    }
    public async Task<ReleaseCheck?> CheckAsync(bool force, CancellationToken token = default)
    {
        var now = clock.GetUtcNow();
        try
        {
            using var gate = new DataLock(paths, "updates-cache");
            var cache = Read();
            if (cache.RetryAfter > now)
            { return new(Error: "GitHub requested a later retry.", RetryAfter: cache.RetryAfter); }
            if (!force && (!new ConfigStore(paths).Read().Updates.Enabled || cache.Attempted is { } last && now - last < TimeSpan.FromDays(1)))
            { return null; }
            DistributionJson.Write(CachePath, cache with { Attempted = now });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { if (!force) { return null; } }
        var result = await client.CheckAsync(token).ConfigureAwait(false);
        try
        {
            using var gate = new DataLock(paths, "updates-cache");
            var cache = Read();
            if (cache.Attempted == now)
            { DistributionJson.Write(CachePath, result.Error is null ? cache with { Validated = clock.GetUtcNow(), Offer = result.Offer, RetryAfter = null } : cache with { RetryAfter = result.RetryAfter }); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return result;
    }
    public async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await CheckAsync(false, token).ConfigureAwait(false);
                try
                {
                    var installation = Installation.ForPayload(Environment.ProcessPath!);
                    if (installation is not null)
                    { UpdateTransaction.Cleanup(installation); }
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
                { new LocalLog(paths).Write("update-cleanup-deferred"); }
                await Task.Delay(TimeSpan.FromMinutes(15), token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { new LocalLog(paths).Write("update-discovery-unavailable"); }
    }
}
