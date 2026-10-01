using Shturmap.Data.Http;

namespace Shturmap.Data.Maps;

/// <summary>
/// Downloads map artwork (tarkov.dev's SVG maps by Shebuka and contributors, CC BY-NC-SA 4.0) on first use and
/// keeps it for personal use. Nothing is bundled with the app.
/// </summary>
public sealed class ArtworkCache(CachedHttp http)
{
    public async Task<string> GetSvgAsync(string url, CancellationToken ct = default)
    {
        var name = "svg_" + Path.GetFileName(new Uri(url).AbsolutePath);
        var response = await http.GetAsync(new Uri(url), name, TimeSpan.FromDays(7), ct);
        return response.FilePath;
    }
}
