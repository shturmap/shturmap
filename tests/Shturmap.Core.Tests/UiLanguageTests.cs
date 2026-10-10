using System.Globalization;

namespace Shturmap.Core.Tests;

// Numbers and dates are written in the language of Shturmap's own texts, not in Windows' (review of 2026-10-04: a
// German Windows showed "5.000 ₽" and "3 Okt" inside English sentences). English today; other languages later, from
// this one place (owner, 2026-10-04).
public class UiLanguageTests
{
    [Fact]
    public void Numbers_and_dates_read_as_the_english_texts_around_them()
    {
        Assert.Equal("5,000", 5000.ToString("N0", UiLanguage.Culture));
        Assert.Equal("25 Sep", new DateTime(2026, 9, 25).ToString("d MMM", UiLanguage.Culture));
        Assert.Equal("4 Oct", new DateTime(2026, 10, 4).ToString("d MMM", UiLanguage.Culture));
    }

    [Fact]
    public void Applying_it_makes_it_the_current_culture_whatever_windows_says()
    {
        var before = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture, CultureInfo.DefaultThreadCurrentCulture, CultureInfo.DefaultThreadCurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("5.000", 5000.ToString("N0", CultureInfo.CurrentCulture));
            UiLanguage.Apply();
            Assert.Equal("5,000", 5000.ToString("N0", CultureInfo.CurrentCulture));
            Assert.Equal(UiLanguage.Culture, CultureInfo.CurrentUICulture);
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (before.Item1, before.Item2);
            CultureInfo.DefaultThreadCurrentCulture = before.Item3;
            CultureInfo.DefaultThreadCurrentUICulture = before.Item4;
        }
    }
}
