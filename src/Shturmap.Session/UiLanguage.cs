using System.Globalization;

namespace Shturmap.Session;

/// <summary>
/// The language Shturmap's own texts are written in, and with it the way its numbers and dates are written. The two
/// go together: "Pay 5,000 ₽" and "25 Sep" in English texts, whatever Windows' own language is (review of
/// 2026-10-04: a German Windows showed "5.000 ₽" and "3 Okt" inside English sentences). English is the only language
/// today. When Shturmap's texts are translated (owner, 2026-10-04: other languages are likely to come), the formats
/// follow the language chosen, from this one place; no code asks Windows for its formats.
/// </summary>
public static class UiLanguage
{
    /// <summary>The culture of the language in use: its number and date formats, and its sorting.</summary>
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Makes <see cref="Culture"/> the culture of every thread, so "current culture" means the app's language.</summary>
    public static void Apply()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Culture;
    }
}
