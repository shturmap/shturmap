using System.Globalization;
using System.Resources;
using Shturmap.Core.Text;

namespace Shturmap.Core;

/// <summary>
/// The language of Shturmap's own texts, and with it the way its numbers and dates are written and its lists sorted.
/// They go together: "Pay 5,000 ₽" and "25 Sep" in English texts, "5.000 ₽" and "25. Sept." in German ones, whatever
/// Windows' own formats are (review of 2026-10-04: a German Windows showed "5.000 ₽" and "3 Okt" inside English
/// sentences). One language for everything (owner, 2026-10-10): the player's choice in settings, else the game's
/// language, else Windows' display language, else English (<see cref="Choose"/>). It can change while Shturmap runs.
/// Every text is looked up in <see cref="Culture"/>, never in a thread's culture: a task started before a switch
/// keeps the culture it started with, and would write the old language.
/// </summary>
public static class UiLanguage
{
    public const string English = "en";
    public const string German = "de";

    /// <summary>
    /// A made-up language for developer checks (docs/LANGUAGES.md): every English text accented, longer and in
    /// brackets, "[Ŝéţţîñĝš ···]", with English formats. A text that shows without brackets wasn't moved into the
    /// texts; one that is cut off or overflows is a layout that a longer language will break. Never offered in settings.
    /// </summary>
    public const string Pseudo = "qps-ploc";

    /// <summary>
    /// The languages Shturmap's texts are complete in, in the order settings list them: only these are offered and
    /// chosen by themselves. A language joins when its last text is translated and reviewed (docs/LANGUAGES.md); the
    /// translation tests then hold every text of it.
    /// </summary>
    public static IReadOnlyList<string> Supported { get; } = [English];

    /// <summary>
    /// Languages being translated: not offered, but a developer run can show them ("--culture de-DE") and the
    /// translation tests check what is written of them. A text not written yet shows in English.
    /// </summary>
    public static IReadOnlyList<string> InTranslation { get; } = [German];

    private static volatile CultureInfo _culture = CultureFor(English);
    private static volatile bool _pseudo;

    /// <summary>The culture of the language in use: its number and date formats, and its sorting.</summary>
    public static CultureInfo Culture => _culture;

    /// <summary>The language in use: "en", "de", or <see cref="Pseudo"/>.</summary>
    public static string Code => _pseudo ? Pseudo : _culture.TwoLetterISOLanguageName;

    /// <summary>Raised after the language changed, on the thread that changed it.</summary>
    public static event Action? Changed;

    /// <summary>Whether Shturmap's texts are complete in this language (an ISO 639-1 code, "de").</summary>
    public static bool IsSupported(string? code) => code is not null && Supported.Contains(code);

    /// <summary>Whether this language has texts of Shturmap's, complete or not (<see cref="InTranslation"/>).</summary>
    public static bool IsWritten(string? code) => IsSupported(code) || (code is not null && InTranslation.Contains(code));

    /// <summary>The culture a language's texts are written with: German as in Germany, English as in the US.</summary>
    public static CultureInfo CultureFor(string code) => CultureInfo.GetCultureInfo(code switch
    {
        German => "de-DE",
        _ => "en-US",
    });

    /// <summary>
    /// Makes <paramref name="code"/> the language in use and the current culture of every thread started from now on
    /// (<see cref="Apply"/>), then raises <see cref="Changed"/> if it changed. A language without texts is English.
    /// </summary>
    public static void Set(string code)
    {
        var pseudo = code == Pseudo;
        var culture = CultureFor(pseudo || !IsWritten(code) ? English : code);
        var changed = pseudo != _pseudo || !culture.Equals(_culture);
        (_culture, _pseudo) = (culture, pseudo);
        Apply();
        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Makes <see cref="Culture"/> the culture of every thread, so "current culture" means the app's language.</summary>
    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = _culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = _culture;
    }

    /// <summary>
    /// A text of Shturmap's own in the language in use, as the generated texts classes ask for it (docs/DESIGN.md §8,
    /// "Texts"). A text the language lacks is the English one; a key no file has comes back as itself, so it shows
    /// instead of failing (the translation tests catch both).
    /// </summary>
    public static string Text(ResourceManager texts, string key)
    {
        if (_pseudo)
            return PseudoText.Of(texts.GetString(key, CultureInfo.InvariantCulture) ?? key);
        return texts.GetString(key, _culture) ?? key;
    }

    /// <summary>
    /// A text in capitals, as headings and labels in the game's style show it, by the rules of the language in use. ß
    /// becomes SS in every language: .NET keeps it ("STRAßE"), the font has no capital ẞ, and German writes SS in
    /// capitals; a German name can stand in another language's text. Every upper-casing on screen goes through here
    /// (Caps.Of in the app), so the rule is in one place (docs/LANGUAGES.md, "Review").
    /// </summary>
    public static string Upper(string? text)
    {
        var upper = (text ?? "").ToUpper(_culture);
        return upper.Contains('ß') ? upper.Replace("ß", "SS", StringComparison.Ordinal) : upper;
    }

    /// <summary>
    /// A day and month as the language in use writes it inside a sentence: "4 Oct" in English, "4. Okt." in German (the
    /// month as .NET abbreviates it in that language: "25. Sept."). A pattern of one language ("d MMM") read in another
    /// writes it wrong: German "4 Okt.".
    /// </summary>
    public static string DayMonth(DateTime day)
    {
        var culture = _culture;
        return day.ToString(DatePatterns(culture).DayMonth, culture);
    }

    /// <inheritdoc cref="DayMonth(DateTime)"/>
    public static string DayMonth(DateOnly day)
    {
        var culture = _culture;
        return day.ToString(DatePatterns(culture).DayMonth, culture);
    }

    /// <summary>A day and month with the time, as the language in use writes it: "4 Oct 14:30", German "4. Okt., 14:30".</summary>
    public static string DayMonthTime(DateTime at)
    {
        var culture = _culture;
        return at.ToString(DatePatterns(culture).DayMonthTime, culture);
    }

    // How each language writes a date inside its sentences: one row per language, a language without one writes
    // English's until it has its own. German's are CLDR's ("d. MMM", and a comma before the time).
    private static (string DayMonth, string DayMonthTime) DatePatterns(CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        German => ("d. MMM", "d. MMM, HH:mm"),
        _ => ("d MMM", "d MMM HH:mm"),
    };

    /// <summary>
    /// A text of Shturmap's own in English whatever the language in use, its placeholders filled in English formats: for
    /// the app log, diagnostics and reports, which stay English for whoever fixes Shturmap (docs/DESIGN.md §8, "The
    /// app's own language"). The text is named with nameof, so a misspelt name is a build error:
    /// <c>UiLanguage.InEnglish(DataTexts.Resources, nameof(DataTexts.LoadFailedStatus), ("status", 503))</c>.
    /// </summary>
    public static string InEnglish(ResourceManager texts, string key, params ReadOnlySpan<(string Name, object? Value)> args)
    {
        var pattern = texts.GetString(key, CultureInfo.InvariantCulture) ?? key;
        return args.IsEmpty ? pattern : TextFormat.Format(CultureFor(English), pattern, args);
    }

    /// <summary>
    /// The language Shturmap's texts are shown in and the language the game data is asked for in. The player's
    /// choice in settings (<paramref name="setting"/>, a language code; anything else means automatic) decides both.
    /// Otherwise the game's language decides both when Shturmap has its texts; Windows' display language, then English,
    /// decides the texts when it doesn't, and the game data keeps the game's language, so names stay as the game shows
    /// them until that language's texts are written (owner, 2026-10-10).
    /// </summary>
    /// <param name="setting">The "Language" setting: a code from <see cref="Supported"/>, or automatic.</param>
    /// <param name="gameLanguage">The game's language as its settings name it ("ge"), or null when unknown.</param>
    /// <param name="windowsLanguage">Windows' display language at start ("de-DE"), or null.</param>
    /// <param name="supported">The languages to choose from; <see cref="Supported"/> unless a test says otherwise.</param>
    public static LanguageChoice Choose(string? setting, string? gameLanguage, string? windowsLanguage, IReadOnlyList<string>? supported = null)
    {
        supported ??= Supported;
        if (setting == Pseudo)
            return new(Pseudo, English, LanguageSource.Setting);
        if (setting is not null && supported.Contains(setting))
            return new(setting, setting, LanguageSource.Setting);
        var game = GameLanguage.Common(gameLanguage);
        if (game is not null && supported.Contains(game))
            return new(game, game, LanguageSource.Game);
        var windows = WindowsCode(windowsLanguage);
        if (windows is not null && supported.Contains(windows))
            return new(windows, game ?? windows, LanguageSource.Windows);
        return new(English, game ?? English, LanguageSource.Default);
    }

    // "de-DE" → "de"; null for no name or one .NET doesn't know.
    private static string? WindowsCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        try
        {
            return CultureInfo.GetCultureInfo(name.Trim()).TwoLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}

/// <summary>Where the language in use came from, for settings ("Automatic: Deutsch, from the game") and diagnostics.</summary>
public enum LanguageSource
{
    /// <summary>The player chose it in settings.</summary>
    Setting,
    /// <summary>The game's language, which Shturmap has texts for.</summary>
    Game,
    /// <summary>Windows' display language: the game's is unknown or has no texts yet.</summary>
    Windows,
    /// <summary>English: neither the game's nor Windows' language has texts yet.</summary>
    Default,
}

/// <param name="Ui">The language of Shturmap's own texts ("de").</param>
/// <param name="Data">tarkov.dev's code for the language of the game data ("de"); the game's when its texts are missing.</param>
/// <param name="Source">Where <paramref name="Ui"/> came from.</param>
public sealed record LanguageChoice(string Ui, string Data, LanguageSource Source);
