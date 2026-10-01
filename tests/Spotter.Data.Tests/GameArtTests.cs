using System.Net;
using SkiaSharp;
using Spotter.Data.Images;

namespace Spotter.Data.Tests;

public class GameArtTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("spotter-art-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    private sealed class FakeServer(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request.RequestUri!));
        }
    }

    private static byte[] Webp()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Orange);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Webp, 90).ToArray();
    }

    [Fact]
    public async Task Downloads_once_and_stores_a_png()
    {
        var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Webp()) });
        var art = new GameArt(new HttpClient(server), _folder);

        var path = await art.GetAsync(ArtKind.Item, "5780cf7f2459777de4559322");
        var again = await art.GetAsync(ArtKind.Item, "5780cf7f2459777de4559322");

        Assert.NotNull(path);
        Assert.Equal(path, again);
        Assert.Equal(path, art.Cached(ArtKind.Item, "5780cf7f2459777de4559322"));
        Assert.Equal("https://assets.tarkov.dev/5780cf7f2459777de4559322-icon.webp", Assert.Single(server.Requests).ToString());
        using var decoded = SKBitmap.Decode(path);
        Assert.Equal(8, decoded.Width);
    }

    [Fact]
    public async Task Missing_pictures_and_odd_ids_give_null()
    {
        var server = new FakeServer(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var art = new GameArt(new HttpClient(server), _folder);

        Assert.Null(await art.GetAsync(ArtKind.Trader, "54cb50c76803fa8b248b4571"));
        Assert.Null(await art.GetAsync(ArtKind.Trader, "../../evil"));
        Assert.Single(server.Requests);
    }
}
