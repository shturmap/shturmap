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

    // A translation that fails for a reason that may pass doesn't cost the whole load any more (review of 2026-10-04,
    // A46: no data at all for as long as one translation file kept failing): the data comes in English and says which
    // language didn't load and why, so the session can say it truthfully and ask again.
    [Fact]
    public async Task A_busy_answer_to_a_translation_is_not_a_missing_language()
    {
        var data = await Loader(_ => Status(HttpStatusCode.ServiceUnavailable)).LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal(("en", null), (data.Language, data.MissingLanguage));
        var failure = Assert.IsType<LanguageFailure>(data.LanguageFailure);
        // Said as what it is, and tried again by itself.
        Assert.Equal(("de", LoadFailure.ServerBusy, 503, true), (failure.Language, failure.Why.Kind, failure.Why.Status, failure.Why.Transient));
    }

    [Fact]
    public async Task A_translation_that_doesnt_answer_in_time_is_a_timeout()
    {
        var timeout = Task.FromException<HttpResponseMessage>(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        var data = await Loader(name => name.StartsWith("tasks", StringComparison.Ordinal) ? timeout : Status(HttpStatusCode.NotFound))
            .LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal(LoadFailure.TimedOut, data.LanguageFailure?.Why.Kind);
        Assert.Null(data.MissingLanguage);
    }

    [Fact]
    public async Task Beside_a_missing_file_the_failure_that_may_pass_is_the_one_told()
    {
        // "maps_de" comes first and is not found; "tasks_de" is busy: the language counts as not loaded (and is asked
        // for again), not as one tarkov.dev lacks.
        var data = await Loader(name => Status(name.StartsWith("maps", StringComparison.Ordinal) ? HttpStatusCode.NotFound : HttpStatusCode.BadGateway))
            .LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal((LoadFailure.ServerBusy, 502), (data.LanguageFailure?.Why.Kind, data.LanguageFailure?.Why.Status));
        Assert.Null(data.MissingLanguage);
    }

    [Fact]
    public async Task The_language_comes_with_a_later_load()
    {
        var busy = true;
        var loader = Loader(_ => busy ? Status(HttpStatusCode.ServiceUnavailable) : null);
        Assert.NotNull((await loader.LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken)).LanguageFailure);
        busy = false;
        var data = await loader.LoadAsync(GameMode.Pve, "ge", TestContext.Current.CancellationToken);
        Assert.Equal(("de", null, null), (data.Language, data.LanguageFailure, data.MissingLanguage));
    }

    [Fact]
    public async Task The_caller_stopping_is_no_language_failure()
    {
        // The translations don't answer until the caller gives up.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var unanswered = new TaskCompletionSource<HttpResponseMessage>();
        using var given = stop.Token.Register(() => unanswered.TrySetCanceled(stop.Token));
        var load = Loader(_ => unanswered.Task).LoadAsync(GameMode.Pve, "ge", stop.Token);
        stop.CancelAfter(100);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => load);
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
