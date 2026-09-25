using System.Diagnostics;
using System.Net;
using System.Text.Json;

sealed class WallhavenApi : IDisposable
{
    const string SearchUrl = "https://wallhaven.cc/api/v1/search";
    const string SettingsUrl = "https://wallhaven.cc/api/v1/settings";
    const string UserAgent = "Wallshall/1.0";
    const int ParallelDownloads = 6;

    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public WallhavenApi() => http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

    public async Task<List<string>> GetPageAsync(AppSettings settings, int page)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{SearchUrl}?{settings.BuildQuery()}&page={page}");
        var key = settings.ApiKey;
        if (key != "") req.Headers.Add("X-API-Key", key);

        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("path").GetString()!)
            .ToList();
    }

    public async Task DownloadAsync(IReadOnlyList<string> urls, string dir)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads };
        await Parallel.ForEachAsync(urls, options, async (url, ct) =>
        {
            var target = Path.Combine(dir, FileNameOf(url));
            if (File.Exists(target)) return;

            var tmp = target + ".part";
            try
            {
                await using (var src = await http.GetStreamAsync(url, ct))
                await using (var dst = File.Create(tmp))
                    await src.CopyToAsync(dst, ct);
                File.Move(tmp, target, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Cache.Delete(tmp);
            }
        });
    }

    public static async Task<HttpStatusCode?> CheckKeyAsync(string key)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var req = new HttpRequestMessage(HttpMethod.Get, SettingsUrl);
            req.Headers.Add("X-API-Key", key);
            req.Headers.UserAgent.ParseAdd(UserAgent);
            using var resp = await http.SendAsync(req);
            return resp.StatusCode;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return null;
        }
    }

    public static string FileNameOf(string url) => Path.GetFileName(new Uri(url).LocalPath);

    public void Dispose() => http.Dispose();
}
