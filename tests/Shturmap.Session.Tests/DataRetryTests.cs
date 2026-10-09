using System.Net;
using System.Text.Json;
using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// What the session does when tarkov.dev doesn't answer (review of 2026-10-04): the game data, the game language's
// texts (A46) and the item sources (H7) are each asked for again by themselves, after waits that grow (owner, the same
// day: no unnecessary load on tarkov.dev; it was every 2 minutes for as long as it failed), and what the player is
// told is true: what failed, what is shown instead, and when it is tried again.
public class DataRetryTests
{
    private static HttpRequestException Busy() => new("tarkov.dev answered 503", null, HttpStatusCode.ServiceUnavailable);

    // What the loader throws when a sign-in page or a filter's page came in place of the data.
    private static NotDataException Page() => new(["regular_tasks.json"], new JsonException("'<' is an invalid start of a value."));

    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(30);

    // The waits a session asked for, by how many tries in a row had failed.
    private sealed class Waits
    {
        private readonly List<int> _asked = [];

        public TimeSpan Next(int failures)
        {
            lock (_asked)
                _asked.Add(failures);
            return Short;
        }

        public int[] Asked
        {
            get
            {
                lock (_asked)
                    return [.. _asked];
            }
        }
    }

    // ---- the notices ----

    [Fact]
    public void The_data_notice_names_the_wait_that_follows_it()
    {
        var busy = LoadProblem.Explain(Busy());
        Assert.Equal(
            "No game data. tarkov.dev answered 503. It is busy or down for a moment; try again in a few minutes. Shturmap tries again in 8 minutes; if it keeps failing, please report it.",
            GameSession.DataNotice(busy, TimeSpan.FromMinutes(8)));
        // Where the text stays up while the tries go on (the DATA chip), it names no single wait: they grow.
        Assert.EndsWith("Shturmap tries again by itself, at first after 2 minutes, then less often; if it keeps failing, please report it.", GameSession.DataNotice(busy));
        // A failure that won't pass by itself is not tried again, and the notice doesn't say it is.
        var unreadable = LoadProblem.Explain(new JsonException("changed"));
        Assert.DoesNotContain("tries again", GameSession.DataNotice(unreadable, TimeSpan.FromMinutes(2)));
        Assert.DoesNotContain("tries again", GameSession.DataNotice(unreadable));
        Assert.True(GameSession.DataNoticeOffersReport(busy));
        Assert.True(GameSession.DataNoticeOffersReport(unreadable));
    }

    [Fact]
    public void A_page_in_place_of_the_data_is_said_with_the_next_try_and_no_report()
    {
        // A report can't change what sends the page (owner, 2026-10-09): the notice says the next try, and only that.
        var page = LoadProblem.Explain(Page());
        Assert.Equal(
            "No game data. tarkov.dev's answer wasn't its data: a sign-in page or a filter in between? Shturmap tries again in 2 minutes.",
            GameSession.DataNotice(page, TimeSpan.FromMinutes(2)));
        Assert.Equal(
            "No game data. tarkov.dev's answer wasn't its data: a sign-in page or a filter in between? Shturmap tries again by itself, at first after 2 minutes, then less often.",
            GameSession.DataNotice(page));
        Assert.False(GameSession.DataNoticeOffersReport(page));
        Assert.Equal(
            "Showing English: the German texts couldn't be loaded. tarkov.dev's answer wasn't its data: a sign-in page or a filter in between? Shturmap tries again in 2 minutes.",
            GameSession.LanguageNotice("German", page, TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void The_language_notice_says_what_failed_what_is_shown_and_when_it_is_tried_again()
    {
        Assert.Equal(
            "Showing English: the German texts couldn't be loaded. tarkov.dev answered 503. Shturmap tries again in 2 minutes.",
            GameSession.LanguageNotice("German", LoadProblem.Explain(Busy()), TimeSpan.FromMinutes(2)));
        // One that won't pass by itself: what to do instead.
        var refused = LoadProblem.Explain(new HttpRequestException("403", null, HttpStatusCode.Forbidden));
        Assert.Equal("Showing English: the German texts couldn't be loaded. tarkov.dev answered 403. Please report it.",
            GameSession.LanguageNotice("German", refused, TimeSpan.FromMinutes(2)));
    }

    // ---- the game data ----

    [Fact]
    public async Task A_failed_load_is_tried_again_after_growing_waits_and_a_change_of_mode_starts_over()
    {
        var failing = 3;
        var waits = new Waits();
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = mode => Interlocked.Decrement(ref failing) >= 0 ? throw Busy() : SessionRig.Data(mode),
            RetryWait = waits.Next,
        });
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data after three failed tries");
        // The first failure waits the first wait, the second the second, …; and it is said once, not three times.
        Assert.Equal([1, 2, 3], waits.Asked);
        Assert.Single(rig.Notices, n => n.StartsWith("No game data.", StringComparison.Ordinal));
        Assert.Null(rig.Snapshot.DataProblem);

        // Another mode's data fails once: the count starts over.
        Volatile.Write(ref failing, 1);
        rig.Log("Session mode: Regular");
        await rig.Until(s => s.Mode == GameMode.Pvp && s.Data is not null, "the other mode's data");
        Assert.Equal([1, 2, 3, 1], waits.Asked);
    }

    [Fact]
    public async Task A_page_in_place_of_the_data_is_tried_again_and_offers_no_report()
    {
        var failing = 2;
        var waits = new Waits();
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = mode => Interlocked.Decrement(ref failing) >= 0 ? throw Page() : SessionRig.Data(mode),
            RetryWait = waits.Next,
        });
        var said = new List<SessionNotice>();
        rig.Session.Notice += notice =>
        {
            lock (said)
                said.Add(notice);
        };
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data after two pages in its place");
        Assert.Equal([1, 2], waits.Asked);
        lock (said)
        {
            var notice = Assert.Single(said, n => n.Text.StartsWith("No game data.", StringComparison.Ordinal));
            Assert.Contains("a sign-in page or a filter in between?", notice.Text, StringComparison.Ordinal);
            Assert.False(notice.OffersReport);
        }
    }

    // ---- the game language's texts ----

    // The rig's data in another language, by hand: Customs under another name.
    private static GameData InLanguage(GameMode mode, string language, string customs, LanguageFailure? failure = null)
    {
        var data = SessionRig.Data(mode);
        return new GameData
        {
            Mode = data.Mode,
            Language = language,
            LanguageFailure = failure,
            Maps = data.Maps.ToDictionary(m => m.Key, m => m.Value.NormalizedName == "customs" ? m.Value with { Name = customs } : m.Value),
            Tasks = data.Tasks,
            Traders = data.Traders,
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    [Fact]
    public async Task Texts_that_did_not_load_are_said_as_that_and_asked_for_again_until_they_come()
    {
        var loads = 0;
        var waits = new Waits();
        var busy = new LanguageFailure("de", LoadProblem.Explain(Busy()));
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            // The first load and the first try after it get no German texts; the second try does.
            GivenData = mode => Interlocked.Increment(ref loads) <= 2 ? InLanguage(mode, "en", "Customs", busy) : InLanguage(mode, "de", "Zoll"),
            RetryWait = waits.Next,
        });
        await rig.StartAsync();
        // English at once, not "no data": everything works while the texts are asked for again.
        var english = await rig.Until(s => s.Data is not null, "the data in English");
        Assert.Equal("en", english.Data!.Language);
        await rig.Until(() => rig.Notices.Any(n => n.StartsWith("Showing English: the German texts couldn't be loaded. tarkov.dev answered 503.", StringComparison.Ordinal)),
            "the notice that says what happened");

        var german = await rig.Until(s => s.Data?.Language == "de", "the German texts");
        // The map on screen is named anew.
        Assert.Equal("Zoll", german.Map?.Name);
        await rig.Until(() => rig.Notices.Contains("German texts loaded."), "the notice that they came");
        Assert.Equal(3, Volatile.Read(ref loads));
        Assert.Equal([1, 2], waits.Asked);
        // Said once, though it failed twice; and never as a language tarkov.dev lacks.
        Assert.Single(rig.Notices, n => n.StartsWith("Showing English", StringComparison.Ordinal));
        Assert.DoesNotContain(rig.Notices, n => n.Contains("texts on tarkov.dev", StringComparison.Ordinal));
    }

    // ---- the item sources ----

    private static ItemSources NoSources() => new()
    {
        Items = new Dictionary<string, ApiItem>(),
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Item_sources_that_did_not_load_are_asked_for_again_without_a_restart(bool page)
    {
        var failing = 2;
        var waits = new Waits();
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            GivenSources = _ => Interlocked.Decrement(ref failing) >= 0 ? throw (page ? Page() : (Exception)Busy()) : NoSources(),
            RetryWait = waits.Next,
        });
        await rig.StartAsync();
        await rig.Until(s => s.Sources is not null, "the item sources after two failed tries");
        Assert.Equal([1, 2], waits.Asked);
    }

    [Fact]
    public async Task Item_sources_that_cannot_be_read_are_not_asked_for_over_and_over()
    {
        var asked = 0;
        var waits = new Waits();
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = SessionRig.Data,
            GivenSources = _ =>
            {
                Interlocked.Increment(ref asked);
                throw new JsonException("tarkov.dev changed its format");
            },
            RetryWait = waits.Next,
        });
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        await rig.Until(() => Volatile.Read(ref asked) == 1, "the one try");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, Volatile.Read(ref asked));
        Assert.Null(rig.Snapshot.Sources);
    }

    // ---- closing ----

    // Closing runs on more than one path (the window closing, an uninstall): every call waits for the one close, and
    // none runs it twice (review of 2026-10-04, A36).
    [Fact]
    public async Task Closing_twice_is_one_close()
    {
        await using var rig = new SessionRig();
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        await Task.WhenAll(rig.Session.DisposeAsync().AsTask(), rig.Session.DisposeAsync().AsTask());
        await rig.Session.DisposeAsync();
        // What the window still asks afterwards is dropped, not a crash.
        rig.Session.SetSetting("help.seen", "1");
        Assert.Null(rig.Session.GetSetting("help.seen"));
    }
}
