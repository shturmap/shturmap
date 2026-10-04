using Shturmap.App.Rules;

namespace Shturmap.Core.Tests;

/// <summary>
/// The wiki links on the quest card and the map come from tarkov.dev's data: only an https address on the Escape from
/// Tarkov wiki becomes a link (the review of 2026-10-04: any scheme was accepted, and a click starts whatever
/// program is registered for it).
/// </summary>
public class OutsideLinkTests
{
    [Theory]
    [InlineData("https://escapefromtarkov.fandom.com/wiki/Debut")]
    [InlineData("https://EscapeFromTarkov.fandom.com/wiki/Customs")]
    public void A_wiki_address_is_a_link(string link) => Assert.Equal(new Uri(link), OutsideLink.Wiki(link));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wiki/Debut")] // not absolute
    [InlineData("http://escapefromtarkov.fandom.com/wiki/Debut")] // not https
    [InlineData("https://example.com/wiki/Debut")] // another host
    [InlineData("https://escapefromtarkov.fandom.com.example.com/wiki/Debut")] // a look-alike
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("ms-settings:privacy")]
    [InlineData("javascript:alert(1)")]
    public void Anything_else_is_no_link(string? link) => Assert.Null(OutsideLink.Wiki(link));

    [Fact]
    public void A_maps_interactive_map_is_its_wiki_page_with_the_suffix()
    {
        Assert.Equal(new Uri("https://escapefromtarkov.fandom.com/wiki/Customs_Interactive_Map"),
            OutsideLink.WikiMap("https://escapefromtarkov.fandom.com/wiki/Customs/"));
        Assert.Null(OutsideLink.WikiMap("https://example.com/wiki/Customs"));
        Assert.Null(OutsideLink.WikiMap(null));
    }
}
