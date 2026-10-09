namespace Shturmap.Core.Tests;

// The game's language codes in the common ones: one table for tarkov.dev's files and Windows' text recognition
// (review of 2026-10-09: each had a copy).
public class GameLanguageTests
{
    [Theory]
    [InlineData("ge", "de")]
    [InlineData("cz", "cs")]
    [InlineData("jp", "ja")]
    [InlineData("kr", "ko")]
    [InlineData("po", "pt")]
    [InlineData("tu", "tr")]
    [InlineData("ch", "zh")]
    [InlineData("es-mx", "es")]
    [InlineData("ES-MX", "es")]
    [InlineData(" ge ", "de")]
    [InlineData("EN", "en")]
    [InlineData("ru", "ru")]
    [InlineData("fr", "fr")]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void The_games_codes_become_the_common_ones(string? game, string? common) =>
        Assert.Equal(common, GameLanguage.Common(game));
}
