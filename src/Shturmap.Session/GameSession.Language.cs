using Shturmap.Core;
using Shturmap.Game.Settings;

namespace Shturmap.Session;

/// <summary>The language as settings and diagnostics show it (docs/DESIGN.md §8, "The app's own language").</summary>
/// <param name="Choice">The language of Shturmap's texts, the game data's, and where it came from.</param>
/// <param name="Setting">The "Language" setting as it counts: <see cref="GameSession.AutomaticLanguage"/> or a language's
/// code; this run's when <paramref name="ForRun"/>.</param>
/// <param name="ForRun">This run's language ("--culture", "--switch-language"), not the saved setting.</param>
/// <param name="Automatic">What Automatic comes to, whatever is chosen: the game's language, Windows', or English.</param>
/// <param name="Windows">Windows' display language at start ("de-DE"), or null.</param>
public sealed record LanguageView(LanguageChoice Choice, string Setting, bool ForRun, LanguageChoice Automatic, string? Windows)
{
    /// <summary>English, automatically, before the session has chosen.</summary>
    public static LanguageView Default { get; } = new(English, GameSession.AutomaticLanguage, false, English, null);

    private static LanguageChoice English => new(UiLanguage.English, UiLanguage.English, LanguageSource.Default);
}

// One language for everything (owner, 2026-10-10; docs/DESIGN.md §8, "The app's own language" and "Language"):
// Shturmap's own texts, its number and date formats and the game data's names. The player's choice in settings, else
// the game's language when Shturmap's texts are complete in it, else Windows' display language, else English
// (UiLanguage.Choose); a game language without texts keeps the game data in it. The session keeps the choice and loads
// the game data in its language. The window applies the texts' language (UiLanguage.Set, on its own thread) before it
// builds them, and when the choice changes while Shturmap runs, then asks for the snapshot again (RepublishAsync), so
// every text the session composes is in the new language.
public sealed partial class GameSession
{
    /// <summary>The key of "Language" in the app's settings: <see cref="AutomaticLanguage"/> (or absent) or a language's
    /// code from <see cref="UiLanguage.Offered"/> ("en", "de").</summary>
    public const string LanguageSetting = "language";

    /// <summary>The setting's value for Automatic.</summary>
    public const string AutomaticLanguage = "auto";

    /// <summary>Windows' display language at start ("de-DE"), read before anything set the app's culture; null when
    /// unknown (tests, the CLI). Set before <see cref="Open"/>.</summary>
    public string? WindowsLanguage { get; set; }

    /// <summary>
    /// This run's language for everything, as if chosen in settings, never saved: "--culture" ("en", "de", or
    /// <see cref="UiLanguage.Pseudo"/>), a language being translated too. Null follows the setting. Set before
    /// <see cref="Open"/>.
    /// </summary>
    public string? LanguageForRun { get; set; }

    /// <summary>The language chosen: Shturmap's texts', the game data's, and where it came from. English until
    /// <see cref="Open"/>.</summary>
    public LanguageChoice Language { get; private set; } = LanguageView.Default.Choice;

    /// <summary>What a saved "Language" means: a language settings offer, else Automatic (no value, "auto", a language
    /// no longer offered, or one only a developer build offers, read by another build).</summary>
    public static string LanguageSettingOf(string? saved) =>
        saved is not null && UiLanguage.Offered.Contains(saved) ? saved : AutomaticLanguage;

    // The languages a run's own language may be: every one with texts, finished or not (UiLanguage.IsWritten).
    private static readonly IReadOnlyList<string> Written = [.. UiLanguage.Supported, .. UiLanguage.InTranslation];

    // Under _gate. The setting as it counts now: this run's, else the saved one.
    private string LanguageSettingNow => LanguageForRun ?? LanguageSettingOf(_store?.GetSetting(LanguageSetting));

    // Under _gate.
    private LanguageChoice ChooseLanguage()
    {
        var setting = LanguageSettingNow;
        return UiLanguage.Choose(setting == AutomaticLanguage ? null : setting, _settings.Language, WindowsLanguage,
            offered: LanguageForRun is null ? null : Written);
    }

    // Under _gate, once the game's settings are read at start.
    private void ChooseLanguageAtStart()
    {
        if (LanguageForRun is { } run && run != AutomaticLanguage && run != UiLanguage.Pseudo && !UiLanguage.IsWritten(run))
        {
            AppLog.Warn($"Language for this run: Shturmap has no '{run}' texts; the setting decides");
            LanguageForRun = null;
        }
        Language = ChooseLanguage();
        AppLog.Info("Language: " + Describe(Language) + (LanguageForRun is null ? "" : " (this run only)"));
    }

    // "de, from the game; game data in de", for the app log and diagnostics (English: they are read by whoever fixes it).
    public static string Describe(LanguageChoice choice) =>
        $"{choice.Ui}, " + choice.Source switch
        {
            LanguageSource.Setting => "chosen in settings",
            LanguageSource.Game => "from the game",
            LanguageSource.Windows => "from Windows",
            _ => "by default",
        } + $"; game data in {choice.Data}";

    // Under _gate: the language as the snapshot shows it.
    private LanguageView ShownLanguage() => new(Language, LanguageSettingNow, LanguageForRun is not null,
        UiLanguage.Choose(null, _settings.Language, WindowsLanguage), WindowsLanguage);

    /// <summary>
    /// The "Language" setting: <see cref="AutomaticLanguage"/> or a code from <see cref="UiLanguage.Offered"/>; saved, or
    /// with <paramref name="save"/> false for this run only ("--switch-language"). The language is chosen again; when the
    /// game data's changes, the data and the item sources load again in it, as after a change of mode, but the data on
    /// screen stays until the new is here. The caller then applies the new choice to Shturmap's texts (UiLanguage.Set, on
    /// the window's thread) and asks for the snapshot again (<see cref="RepublishAsync"/>).
    /// </summary>
    /// <returns>The language chosen now.</returns>
    public async Task<LanguageChoice> SetLanguageAsync(string setting, bool save = true)
    {
        await _gate.WaitAsync();
        try
        {
            if (save)
            {
                _store?.SetSetting(LanguageSetting, LanguageSettingOf(setting));
                LanguageForRun = null;
            }
            else
            {
                LanguageForRun = setting;
            }
            var before = Language;
            Language = ChooseLanguage();
            AppLog.Info($"Language set to {setting}{(save ? "" : " for this run")}: {Describe(Language)}");
            if (Language.Data != before.Data)
                LoadDataInLanguage();
            return Language;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under _gate. The game data in the language chosen now: a new round, as a change of mode starts one, so what the old
    // language still waited for (a retry of its texts, its item sources) is dropped and the waits start over; the data
    // on screen stays until the new data is here (LoadDataAsync names the maps anew), and the item sources follow it.
    private void LoadDataInLanguage()
    {
        AppLog.Info($"Language: loading the game data in {Language.Data}");
        _loadRound++;
        _sourcesRound++;
        _dataFailures = _languageFailures = _sourcesFailures = 0;
        var mode = _mode;
        _ = Task.Run(() => LoadDataAsync(mode));
    }

    /// <summary>
    /// Composes the snapshot again, every text in it in the language in use now: the window calls it once it switched
    /// Shturmap's texts to a new language (UiLanguage.Set). The plans are made again too, and the screenshot keys' names
    /// read from the game's settings: they carry texts.
    /// </summary>
    public async Task RepublishAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_env is not null && _locations is not null)
                _settings = _settings with { ScreenshotKeys = new GameSettingsReader(_env).Read(_locations.SettingsFolder).ScreenshotKeys };
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }
}
