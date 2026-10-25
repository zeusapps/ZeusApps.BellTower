using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using BellTower.Cli;
using BellTower.Distribution;

namespace BellTower.Updates;

public sealed class ReleaseClient(HttpClient client)
{
    public static HttpClient CreateClient() => new() { Timeout = TimeSpan.FromMinutes(3) };
    public async Task<ReleaseCheck> CheckAsync(CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            using var request = Request("https://api.github.com/repos/" + ReleaseOffer.Repository + "/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            { return new(); }
            if (!response.IsSuccessStatusCode)
            {
                var retry = response.Headers.RetryAfter?.Date;
                if (response.Headers.RetryAfter?.Delta is { } delta)
                { retry = DateTimeOffset.UtcNow + delta; }
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var seconds))
                { retry = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                return new(Error: "GitHub check failed (HTTP " + (int)response.StatusCode + ").", RetryAfter: retry);
            }
            using var stream = new MemoryStream();
            await CopyAsync(response.Content, stream, 262144, timeout.Token).ConfigureAwait(false);
            using var document = JsonDocument.Parse(stream.ToArray());
            var root = document.RootElement;
            if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            { return new(); }
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!tag.StartsWith('v'))
            { return new(); }
            var version = tag[1..];
            _ = ReleaseContract.Parse(version);
            var package = new ReleaseContract(version).PackageName;
            var url = ReleaseOffer.Releases + "/download/" + tag + "/";
            var required = new[] { package, package + ".sha256", package[..^4] + ".release.json" };
            var assets = root.GetProperty("assets").EnumerateArray().ToArray();
            if (root.GetProperty("html_url").GetString() != ReleaseOffer.Releases + "/tag/" + tag || required.Any(name =>
                assets.Count(a => a.GetProperty("name").GetString() == name && a.GetProperty("browser_download_url").GetString() == url + name) != 1))
            { return new(); }
            using var metadata = new MemoryStream();
            using var metadataRequest = Request(url + required[2]);
            using var metadataResponse = await client.SendAsync(metadataRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            metadataResponse.EnsureSuccessStatusCode();
            await CopyAsync(metadataResponse.Content, metadata, 262144, timeout.Token).ConfigureAwait(false);
            var offer = JsonSerializer.Deserialize<ReleaseOffer>(metadata.ToArray(), DistributionJson.Options) ?? throw new InvalidDataException("Missing release metadata.");
            offer.EnsureStable();
            if (offer.Version != version)
            { throw new InvalidDataException("Release metadata version mismatch."); }
            return new(offer);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or JsonException or OperationCanceledException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return new(Error: "Release check unavailable: " + error.Message); }
    }
    public async Task DownloadAsync(string url, string destination, long limit, CancellationToken token)
    {
        using var request = Request(url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await CopyAsync(response.Content, output, limit, token).ConfigureAwait(false);
    }
    private static HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("BellTower/" + ApplicationInfo.Version);
        return request;
    }
    private static async Task CopyAsync(HttpContent content, Stream output, long maximum, CancellationToken token)
    {
        if (content.Headers.ContentLength > maximum)
        { throw new InvalidDataException("Release content exceeds size limit."); }
        await using var input = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        var buffer = new byte[65536];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) != 0)
        {
            total += count;
            if (total > maximum)
            { throw new InvalidDataException("Release content exceeds size limit."); }
            await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
        }
    }
}
