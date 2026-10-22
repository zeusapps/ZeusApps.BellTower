using System.Net;
using System.Text.Json;
using BellTower.Distribution;
using BellTower.IO;
using BellTower.Tests.Testing;
using BellTower.Updates;
using Xunit;

namespace BellTower.Tests.Updates;

public sealed class DiscoveryTests
{
    [Fact]
    public async Task ConcurrentAutomaticChecksShareAttemptAndDisabledChecksRemainOffline()
    {
        using var workspace = new TestWorkspace();
        var handler = new Handler();
        using var http = new HttpClient(handler);
        var discovery = new UpdateDiscovery(workspace.Paths, new ReleaseClient(http));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => discovery.CheckAsync(false)));
        Assert.Equal(2, handler.Requests);
        Assert.NotNull(discovery.Hint());
        new ConfigStore(workspace.Paths).Update(c => c.Updates.Enabled = false);
        Assert.Null(discovery.Hint());
        await discovery.CheckAsync(false);
        Assert.Equal(2, handler.Requests);
        await discovery.CheckAsync(true);
        Assert.Equal(4, handler.Requests);
        Assert.DoesNotContain(handler.Urls, url => url.EndsWith(".zip"));
    }
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task UnsuitableReleaseIsNotOffered(bool draft, bool prerelease, bool missing)
    {
        var handler = new Handler { Draft = draft, Prerelease = prerelease, Missing = missing };
        using var http = new HttpClient(handler);
        var result = await new ReleaseClient(http).CheckAsync();
        Assert.Null(result.Offer);
    }
    [Fact]
    public async Task RateLimitBackoffPreventsEvenExplicitRetry()
    {
        using var workspace = new TestWorkspace();
        var handler = new Handler { Limited = true };
        using var http = new HttpClient(handler);
        var discovery = new UpdateDiscovery(workspace.Paths, new ReleaseClient(http));
        Assert.NotNull((await discovery.CheckAsync(true))!.Error);
        await discovery.CheckAsync(true);
        Assert.Equal(1, handler.Requests);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Requests;
        public bool Draft;
        public bool Prerelease;
        public bool Missing;
        public bool Limited;
        public List<string> Urls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            lock (Urls)
            { Urls.Add(request.RequestUri!.ToString()); }
            if (Limited)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromHours(1));
                return Task.FromResult(response);
            }
            var offer = new ReleaseOffer(new ReleaseContract("9.0.0"), new string('a', 64));
            var names = new[] { offer.Contract.PackageName, offer.Contract.PackageName + ".sha256", offer.Contract.PackageName[..^4] + ".release.json" };
            var data = request.RequestUri!.AbsolutePath.EndsWith("/latest")
                ? JsonSerializer.Serialize(new
                {
                    draft = Draft,
                    prerelease = Prerelease,
                    tag_name = "v9.0.0",
                    html_url = offer.Page,
                    assets = names.Take(Missing ? 1 : 3).Select(name => new { name, browser_download_url = offer.Url[..^(offer.Contract.PackageName.Length)] + name })
                })
                : JsonSerializer.Serialize(offer, DistributionJson.Options);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(data) });
        }
    }
}
