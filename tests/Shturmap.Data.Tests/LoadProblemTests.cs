using System.Net;
using System.Text.Json.Nodes;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// What the player reads when tarkov.dev's data doesn't load: what failed, with the status, and what to do; never the
// exception's own text (owner, 2026-10-03).
public class LoadProblemTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-problem-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    // tarkov.dev in miniature: an answer per path, or an exception for every request.
    private sealed class FakeTarkovDev(Func<string, HttpResponseMessage>? answer = null, Exception? fail = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            fail is not null ? Task.FromException<HttpResponseMessage>(fail) : Task.FromResult(answer!(request.RequestUri!.AbsolutePath));
    }

    private static HttpResponseMessage Ok(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        var body = name switch
        {
            "maps.json" => "[]",
            "maps" => """{ "data": { "maps": {}, "mobs": {} } }""",
            "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
            _ => """{ "data": {} }""",
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    }

    private async Task<LoadProblem> Fails(HttpMessageHandler server)
    {
        var e = await Assert.ThrowsAnyAsync<Exception>(() => new GameDataLoader(new CachedHttp(new HttpClient(server), _folder)).LoadAsync(GameMode.Pve, "en"));
        return LoadProblem.Explain(e);
    }

    [Fact]
    public async Task A_missing_file_asks_for_a_report()
    {
        var problem = await Fails(new FakeTarkovDev(p => p.EndsWith("/tasks", StringComparison.Ordinal) ? new(HttpStatusCode.NotFound) : Ok(p)));
        Assert.Equal((LoadFailure.Refused, 404, false), (problem.Kind, problem.Status, problem.Transient));
        Assert.Equal("tarkov.dev answered 404. Please report it.", problem.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    public async Task A_busy_server_is_tried_again(HttpStatusCode status, int code)
    {
        var problem = await Fails(new FakeTarkovDev(p => p.EndsWith("/maps", StringComparison.Ordinal) ? new(status) : Ok(p)));
        Assert.Equal((LoadFailure.ServerBusy, code, true), (problem.Kind, problem.Status, problem.Transient));
        Assert.Equal($"tarkov.dev answered {code}. It is busy or down for a moment; try again in a few minutes.", problem.Text);
    }

    [Fact]
    public async Task No_connection_and_no_saved_copy_says_check_the_connection()
    {
        var problem = await Fails(new FakeTarkovDev(fail: new HttpRequestException("No such host is known. (json.tarkov.dev:443)")));
        Assert.Equal(LoadFailure.Unreachable, problem.Kind);
        Assert.Equal("Couldn't reach tarkov.dev, and there's no saved copy yet. Check the internet connection.", problem.Text);
        Assert.DoesNotContain("host", problem.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_connection_with_a_saved_copy_loads_the_copy()
    {
        await new GameDataLoader(new CachedHttp(new HttpClient(new FakeTarkovDev(Ok)), _folder)).LoadAsync(GameMode.Pve, "en");
        // A day later, offline.
        foreach (var meta in Directory.GetFiles(_folder, "*.meta.json"))
        {
            var node = JsonNode.Parse(File.ReadAllText(meta))!;
            node["FetchedAt"] = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
            File.WriteAllText(meta, node.ToJsonString());
        }
        var data = await new GameDataLoader(new CachedHttp(new HttpClient(new FakeTarkovDev(fail: new HttpRequestException("offline"))), _folder))
            .LoadAsync(GameMode.Pve, "en");
        Assert.True(data.Offline);
    }

    [Fact]
    public async Task A_timeout_says_so()
    {
        var problem = await Fails(new FakeTarkovDev(fail: new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout")));
        Assert.Equal((LoadFailure.TimedOut, true), (problem.Kind, problem.Transient));
        Assert.StartsWith("tarkov.dev didn't answer in time", problem.Text);
    }

    [Fact]
    public async Task Data_in_a_new_shape_asks_for_a_report()
    {
        // JSON, but a task that isn't one. (Until 2026-10-09 this body lacked its last brace: JSON cut off, which is a
        // page's kind now, below.)
        var problem = await Fails(new FakeTarkovDev(p => p.EndsWith("/tasks", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "data": { "tasks": { "t1": 5 }, "questItems": {} } }""") }
            : Ok(p)));
        Assert.Equal((LoadFailure.Unreadable, false), (problem.Kind, problem.Transient));
        Assert.Equal("tarkov.dev's data has changed in a way Shturmap can't read. Please report it.", problem.Text);
    }

    // Something between the PC and tarkov.dev answered in its place with 200 (a sign-in page, a network filter's or a
    // CDN's page), or the body came empty or cut off: that says nothing of tarkov.dev's format, so it is tried again
    // by itself, like a network problem, and asks for no report, which couldn't change it (owner, 2026-10-09).
    [Theory]
    [InlineData("<!DOCTYPE html><html><head><title>Wi-Fi</title></head><body>Sign in to the network</body></html>")]
    [InlineData("")]
    [InlineData("""{ "data": { "tasks": {""")]
    public async Task A_page_in_place_of_the_data_is_tried_again_and_asks_for_no_report(string body)
    {
        var problem = await Fails(new FakeTarkovDev(p => p.EndsWith("/tasks", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }
            : Ok(p)));
        Assert.Equal((LoadFailure.NotData, null, true), (problem.Kind, problem.Status, problem.Transient));
        Assert.Equal("tarkov.dev's answer wasn't its data: a sign-in page or a filter in between? Shturmap tries again in a few minutes.", problem.Text);
        Assert.DoesNotContain("report", problem.Text, StringComparison.OrdinalIgnoreCase);
    }
}
