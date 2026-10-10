using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// One language for everything (owner, 2026-10-10; docs/DESIGN.md §8, "The app's own language" and "Language"): the
// session chooses it at start from the setting, the game's language and Windows' display language, loads the game
// data in it, keeps the setting, and loads the data again when a choice while it runs changes the data's language.
// The game's language is the rig's Game.ini; the data is made by hand per language (GameSession.GivenDataIn).
public class LanguageSessionTests
{
    private static readonly Dictionary<string, (string Customs, string Woods, string Crossroads)> Names = new()
    {
        ["en"] = ("Customs", "Woods", "Crossroads"),
        ["ru"] = ("Таможня", "Лес", "Перекрёсток"),
        ["de"] = ("Zoll", "Wald", "Kreuzung"),
    };

    // Customs and Woods named in the language asked, and on Customs two exits for a PMC.
    private static GameData Data(GameMode mode, string language)
    {
        var data = SessionRig.Data(mode);
        var (customs, woods, crossroads) = Names[language];
        ApiExtract Exit(string id, string name, double x) => new(id, name, "pmc", new ApiPosition(x, 0, 0), null, null, null);
        return new GameData
        {
            Mode = mode,
            Language = language,
            Maps = new[]
            {
                new ApiMap("map-customs", customs, "customs", "bigmap", "maps/customs_preset.bundle", null, SessionRig.CustomsMinutes,
                    [Exit("near", crossroads, 20), Exit("far", "ZB-1011", 300)], [], [], [], []),
                new ApiMap("map-woods", woods, "woods", "Woods", "maps/woods_preset.bundle", null, 40, [], [], [], [], []),
            }.ToDictionary(m => m.Id),
            Tasks = data.Tasks,
            Traders = data.Traders,
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    // A session whose game is in gameLanguage (its Game.ini), on a Windows in English; the data languages it asks for
    // go into asked.
    private static SessionRig Rig(string? gameLanguage, List<string> asked, Action<GameSession>? configure = null,
        Func<GameMode, string, GameData>? data = null, Func<string, ExitListReading?>? shows = null) =>
        new(configure: (paths, locations) =>
        {
            if (gameLanguage is not null)
            {
                Directory.CreateDirectory(locations.SettingsFolder);
                File.WriteAllText(Path.Combine(locations.SettingsFolder, "Game.ini"), $$"""{ "Language": "{{gameLanguage}}" }""");
            }
            var session = new GameSession(paths, locations)
            {
                GivenDataIn = (mode, language) =>
                {
                    lock (asked)
                        asked.Add(language);
                    return (data ?? Data)(mode, language);
                },
                ExitReader = shows is null ? null : (path, _) => Task.FromResult(shows(path)),
                WindowsLanguage = "en-US",
            };
            configure?.Invoke(session);
            return session;
        });

    [Theory]
    [InlineData(null, GameSession.AutomaticLanguage)]
    [InlineData("", GameSession.AutomaticLanguage)]
    [InlineData("auto", GameSession.AutomaticLanguage)]
    [InlineData("xx", GameSession.AutomaticLanguage)]
    [InlineData("qps-ploc", GameSession.AutomaticLanguage)]
    [InlineData("en", "en")]
    // German is offered in every build since 2026-10-10; until then a release read a saved "de" as automatic.
    [InlineData("de", "de")]
    public void A_saved_value_is_a_language_offered_or_automatic(string? saved, string setting) =>
        Assert.Equal(setting, GameSession.LanguageSettingOf(saved));

    [Fact]
    public async Task At_start_a_game_language_without_texts_keeps_its_game_names_and_windows_decides_the_texts()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked);
        Assert.Equal(new LanguageChoice("en", "ru", LanguageSource.Windows), rig.Session.Open());
        await rig.StartAsync();
        var s = await rig.Until(s => s.Data is not null, "the data");
        Assert.Equal("ru", s.Data!.Language);
        Assert.Equal(["ru"], asked);
        Assert.Equal(GameSession.AutomaticLanguage, s.Language.Setting);
        Assert.False(s.Language.ForRun);
        Assert.Equal(LanguageSource.Windows, s.Language.Automatic.Source);
        Assert.Equal("en-US", s.Language.Windows);
    }

    [Fact]
    public async Task The_setting_decides_everything_and_is_kept_for_the_next_start()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked);
        await rig.StartAsync();
        await rig.Until(s => s.Data?.Language == "ru", "the game's language");
        Assert.Equal(new LanguageChoice("en", "en", LanguageSource.Setting), await rig.Session.SetLanguageAsync("en"));
        Assert.Equal("en", rig.Session.GetSetting(GameSession.LanguageSetting));
        await rig.Until(s => s.Data?.Language == "en", "the data in English");

        await rig.RestartAsync();
        Assert.Equal(new LanguageChoice("en", "en", LanguageSource.Setting), rig.Session.Open());

        // Automatic again: saved as such, and the game's language decides the data once more.
        await rig.StartAsync();
        await rig.Until(s => s.Data?.Language == "en", "the data in English at the next start");
        Assert.Equal(new LanguageChoice("en", "ru", LanguageSource.Windows), await rig.Session.SetLanguageAsync(GameSession.AutomaticLanguage));
        Assert.Equal(GameSession.AutomaticLanguage, rig.Session.GetSetting(GameSession.LanguageSetting));
        await rig.Until(s => s.Data?.Language == "ru", "the data in the game's language again");
    }

    [Fact]
    public async Task A_switch_while_running_loads_the_data_again_and_names_the_maps_anew()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        var before = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.Name == "Таможня", "the raid on Таможня");
        Assert.Equal("Таможня", before.Map?.Name);
        Assert.Contains("Перекрёсток", before.Extracts.Select(e => e.Name));

        await rig.Session.SetLanguageAsync("en");
        var after = await rig.Until(s => s.Data?.Language == "en", "the data in English");
        // The raid goes on, on the same map, in the new language's names.
        Assert.Equal(RaidPhase.InRaid, after.Raid.Phase);
        Assert.Equal("Customs", after.RaidMap?.Name);
        Assert.Equal("Customs", after.Map?.Name);
        Assert.Contains("Crossroads", after.Extracts.Select(e => e.Name));
        Assert.Equal(["ru", "en"], asked);

        // Composed again on the window's word, after Shturmap's texts switched: the same data, a new snapshot.
        await rig.Session.RepublishAsync();
        Assert.NotSame(after, rig.Snapshot);
        Assert.Same(after.Data, rig.Snapshot.Data);
        Assert.Equal("en", rig.Snapshot.Language.Setting);
    }

    [Fact]
    public async Task A_choice_that_keeps_the_datas_language_loads_nothing_again()
    {
        var asked = new List<string>();
        await using var rig = Rig("en", asked);
        await rig.StartAsync();
        await rig.Until(s => s.Data?.Language == "en", "the data");
        // Automatic was English already: the same data, and the setting kept.
        await rig.Session.SetLanguageAsync("en");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(["en"], asked);
        Assert.Equal("en", rig.Session.GetSetting(GameSession.LanguageSetting));
    }

    [Fact]
    public async Task A_load_in_the_old_language_that_ends_after_a_switch_is_dropped()
    {
        var asked = new List<string>();
        using var release = new ManualResetEventSlim();
        await using var rig = Rig("ru", asked, data: (mode, language) =>
        {
            // The game's language takes long: it is still on its way when the player switches.
            if (language == "ru")
                release.Wait(TimeSpan.FromSeconds(20));
            return Data(mode, language);
        });
        try
        {
            await rig.StartAsync();
            await rig.Until(() =>
            {
                lock (asked)
                    return asked.Contains("ru");
            }, "the load in Russian under way");
            await rig.Session.SetLanguageAsync("en");
            await rig.Until(s => s.Data?.Language == "en", "the data in English");
        }
        finally
        {
            release.Set();
        }
        await Task.Delay(400, TestContext.Current.CancellationToken);
        Assert.Equal("en", rig.Snapshot.Data?.Language);
        Assert.Equal("Customs", rig.Snapshot.Map?.Name);
    }

    [Fact]
    public async Task This_runs_language_decides_everything_and_is_never_saved()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked, session => session.LanguageForRun = "en");
        Assert.Equal(new LanguageChoice("en", "en", LanguageSource.Setting), rig.Session.Open());
        await rig.StartAsync();
        var s = await rig.Until(s => s.Data is not null, "the data");
        Assert.Equal(["en"], asked);
        Assert.True(s.Language.ForRun);
        Assert.Equal("en", s.Language.Setting);
        Assert.Null(rig.Session.GetSetting(GameSession.LanguageSetting));

        // "--switch-language": as a choice in settings, for this run only.
        Assert.Equal(new LanguageChoice("de", "de", LanguageSource.Setting), await rig.Session.SetLanguageAsync("de", save: false));
        await rig.Until(s => s.Data?.Language == "de", "the data in German");
        Assert.Null(rig.Session.GetSetting(GameSession.LanguageSetting));
    }

    [Fact]
    public async Task The_pseudo_language_shows_english_game_names()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked, session => session.LanguageForRun = UiLanguage.Pseudo);
        Assert.Equal(new LanguageChoice(UiLanguage.Pseudo, "en", LanguageSource.Setting), rig.Session.Open());
        await rig.StartAsync();
        await rig.Until(s => s.Data?.Language == "en", "the data in English");
    }

    [Fact]
    public async Task A_runs_language_without_texts_leaves_it_to_the_setting()
    {
        var asked = new List<string>();
        await using var rig = Rig("ru", asked, session => session.LanguageForRun = "ja");
        Assert.Equal(new LanguageChoice("en", "ru", LanguageSource.Windows), rig.Session.Open());
        Assert.Null(rig.Session.LanguageForRun);
    }

    [Fact]
    public void Diagnostics_say_the_language_where_it_came_from_and_the_datas()
    {
        var snapshot = new SessionSnapshot
        {
            GameLanguage = "ru",
            Language = new LanguageView(new LanguageChoice("en", "ru", LanguageSource.Windows), GameSession.AutomaticLanguage, false,
                new LanguageChoice("en", "ru", LanguageSource.Windows), "en-GB"),
        };
        var text = Diagnostics.Build(snapshot, "0.4.0+d0e71e1", "Windows 11 (10.0.26200)", "folder build", [], new DateTime(2026, 1, 1, 12, 0, 0), null);
        Assert.Contains("Game language: ru", text);
        Assert.Contains("Windows language: en-GB", text);
        Assert.Contains("Language: en, from Windows; game data in ru", text);

        var forRun = snapshot with { Language = snapshot.Language with { Choice = new LanguageChoice("de", "de", LanguageSource.Setting), Setting = "de", ForRun = true } };
        Assert.Contains("Language: de, chosen in settings; game data in de (this run only)",
            Diagnostics.Build(forRun, "0.4.0+d0e71e1", "Windows 11 (10.0.26200)", "folder build", [], new DateTime(2026, 1, 1, 12, 0, 0), null));
    }

    [Fact]
    public void An_exit_is_named_in_the_datas_language_the_games_and_english()
    {
        var data = Data(GameMode.Pve, "en");
        data = new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = data.Maps,
            Tasks = data.Tasks,
            Traders = data.Traders,
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
            GameNames = new Dictionary<string, string> { ["near"] = "Kreuzung" },
            GameNamesLanguage = "de",
        };
        var near = GameSession.ExitNames(data, "map-customs", RaidSide.Pmc).Single(e => e.Id == "extract:near");
        Assert.Equal(["Crossroads", "Kreuzung"], near.Names);
    }

    [Fact]
    public async Task The_list_in_a_german_game_is_read_while_the_data_is_english()
    {
        // The player chose English for Shturmap; the game is German, and so is its extract list in a screenshot.
        GameData English(GameMode mode, string language)
        {
            var data = Data(mode, language);
            return new GameData
            {
                Mode = data.Mode,
                Language = data.Language,
                Maps = data.Maps,
                Tasks = data.Tasks,
                Traders = data.Traders,
                MapDefinitions = data.MapDefinitions,
                CheckedAt = data.CheckedAt,
                GameNames = language == "en" ? new Dictionary<string, string> { ["near"] = Names["de"].Crossroads } : new Dictionary<string, string>(),
                GameNamesLanguage = language == "en" ? "de" : null,
            };
        }
        var asked = new List<string>();
        await using var rig = Rig("ge", asked, session => session.LanguageForRun = "en", English,
            _ => new ExitListReading("Finde einen Ausgang", [new ExitListRow("EXFILØ1 Kreuzung", false), new ExitListRow("EXFILØ2 ZB-1011", false)]));
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Extracts.Count > 0, "the raid on Customs");
        rig.Screenshot(0, 0, 0);
        var s = await rig.Until(s => s.ExitsReadAt is not null, "the list read");
        Assert.Equal(["en"], asked);
        Assert.Equal(ExitState.Listed, s.Extracts.Single(e => e.Name == "Crossroads").State);
        Assert.Equal(ExitState.Listed, s.Extracts.Single(e => e.Name == "ZB-1011").State);
    }
}
