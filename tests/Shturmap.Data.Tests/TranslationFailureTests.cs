using System.Net;
using Shturmap.Core.Logs;
using Shturmap.Data.Http;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// Only "not found" means tarkov.dev has no texts in a language. A translation that fails for a moment (5xx, a timeout,
// no connection) must not read as a missing language, and a load that fails must leave no download unwatched (the
// review of 2026-10-04: a 503 put "No German texts on tarkov.dev" on the screen and left the session in English, and
// the translations of a failed first load became crash records).
public class TranslationFailureTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-translation-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    // tarkov.dev in miniature: every payload empty; the test decides what a translation ("…_de") gets.
    private sealed class FakeTarkovDev(Func<string, Task<HttpResponseMessage>?> translation, Exception? everythingElseFails = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var name = path[(path.LastIndexOf('/') + 1)..];
            if (name.EndsWith("_de", StringComparison.Ordinal) && translation(name) is { } answer)
                return answer;
            if (everythingElseFails is not null)
                return Task.FromException<HttpResponseMessage>(everythingElseFails);
            var body = name switch
            {
                "maps.json" => "[]",
                "maps" => """{ "data": { "maps": {}, "mobs": {} } }""",
                "tasks" => """{ "data": { "tasks": {}, "questItems": {} } }""",
                "items" or "barters" or "crafts" => """{ "data": null }""",
                _ => """{ "data": {} }""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    private static Task<HttpResponseMessage> Status(HttpStatusCode status) => Task.FromResult(new HttpResponseMessage(status));

    private GameDataLoader Loader(Func<string, Task<HttpResponseMessage>?> translation, Exception? everythingElseFails = null) =>
        new(new CachedHttp(new HttpClient(new FakeTarkovDev(translation, everythingElseFails)), _folder));

    [Fact]
    public async Task A_language_tarkov_dev_doesnt_have_is_english_and_says_so()
    {
        var data = await Loader(_ => Status(HttpStatusCode.NotFound)).LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal(("en", "de"), (data.Language, data.MissingLanguage));
    }

    [Fact]
    public async Task A_busy_answer_to_a_translation_is_not_a_missing_language()
    {
        var e = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Loader(_ => Status(HttpStatusCode.ServiceUnavailable)).LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
        var problem = LoadProblem.Explain(e);
        // Said as what it is, and tried again by itself.
        Assert.Equal((LoadFailure.ServerBusy, 503, true), (problem.Kind, problem.Status, problem.Transient));
    }

    [Fact]
    public async Task A_translation_that_doesnt_answer_in_time_is_a_timeout()
    {
        var timeout = Task.FromException<HttpResponseMessage>(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        var e = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Loader(name => name.StartsWith("tasks", StringComparison.Ordinal) ? timeout : Status(HttpStatusCode.NotFound))
                .LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
        Assert.Equal(LoadFailure.TimedOut, LoadProblem.Explain(e).Kind);
    }

    [Fact]
    public async Task Beside_a_missing_file_the_failure_that_may_pass_is_the_one_told()
    {
        // "maps_de" comes first and is not found; "tasks_de" is busy: the load fails as busy (and is tried again), not as
        // "404, please report it".
        var e = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Loader(name => Status(name.StartsWith("maps", StringComparison.Ordinal) ? HttpStatusCode.NotFound : HttpStatusCode.BadGateway))
                .LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.BadGateway, e.StatusCode);
    }

    [Fact]
    public async Task The_item_sources_follow_the_same_rule()
    {
        var english = await Loader(_ => Status(HttpStatusCode.NotFound)).LoadSourcesAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Empty(english.Stations);
        Directory.Delete(_folder, true);
        Directory.CreateDirectory(_folder);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Loader(_ => Status(HttpStatusCode.ServiceUnavailable)).LoadSourcesAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
    }

    // ---- nothing left unwatched ----

    private const string Marker = "translation still on its way when the load failed";

    // In a method of its own, so nothing of the load is still referenced when the test collects garbage.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private async Task LoadOfflineAsync(Func<GameDataLoader, Task> load)
    {
        // Everything fails at once; the translations fail a moment later, after the load has given up.
        async Task<HttpResponseMessage> Later()
        {
            await Task.Delay(50);
            throw new HttpRequestException(Marker);
        }
        await Assert.ThrowsAsync<HttpRequestException>(() => load(Loader(_ => Later(), new HttpRequestException("offline"))));
    }

    [Fact]
    public async Task A_failed_load_leaves_no_download_unwatched()
    {
        var unobserved = new List<Exception>();
        void Note(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // Other tests run beside this one: only this test's own downloads count.
            if (e.Exception.InnerExceptions.Any(x => x.Message == Marker))
                lock (unobserved)
                    unobserved.Add(e.Exception);
        }
        TaskScheduler.UnobservedTaskException += Note;
        try
        {
            await LoadOfflineAsync(l => l.LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
            await LoadOfflineAsync(l => l.LoadSourcesAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken));
            await Task.Delay(300, TestContext.Current.CancellationToken); // the translations have failed by now
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.Empty(unobserved);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Note;
        }
    }
}
