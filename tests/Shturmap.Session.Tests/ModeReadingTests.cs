using Shturmap.Core.Logs;

namespace Shturmap.Session.Tests;

// The status bar shows the mode the game's log says, with no chooser; its tooltip says where it comes from
// (owner, 2026-10-03).
public class ModeReadingTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 22, 0, 0);

    private static SessionModeEvent Said(string raw, DateTime at) => new(at, GameLogParser.ModeFrom(raw), raw);

    [Theory]
    [InlineData(GameMode.Pve, "PvE")]
    [InlineData(GameMode.Pvp, "PvP")]
    [InlineData(GameMode.Seasonal, "Seasonal")]
    public void The_label_names_the_mode(GameMode mode, string text) => Assert.Equal(text, ModeReading.Text(mode));

    [Fact]
    public void A_mode_from_todays_log_says_when()
    {
        var reading = new ModeReading().Read(Said("Pve", new DateTime(2026, 10, 3, 21, 52, 7)));
        Assert.Equal("From the game's log, 21:52", reading.Tooltip(gameFound: true, Now));
    }

    [Fact]
    public void A_mode_from_an_earlier_day_says_the_day()
    {
        var reading = new ModeReading().Read(Said("Regular", new DateTime(2026, 1, 1, 14, 0, 30)));
        Assert.Matches(@"^From the game's log, 1 \S+ 14:00$", reading.Tooltip(gameFound: true, Now));
    }

    [Fact]
    public void Before_the_log_says_anything_it_is_the_mode_last_played()
    {
        Assert.Equal("The mode you last played; follows the game once it starts", new ModeReading().Tooltip(gameFound: true, Now));
        Assert.Equal("No game on this PC: the mode you last played", new ModeReading().Tooltip(gameFound: false, Now));
    }

    [Fact]
    public void A_mode_Shturmap_doesnt_know_keeps_the_last_known_one_and_says_what_the_game_said()
    {
        var known = new ModeReading().Read(Said("Pve", new DateTime(2026, 10, 3, 21, 52, 0)));
        var unknown = known.Read(Said("Arena", new DateTime(2026, 10, 3, 21, 55, 0)));
        Assert.Equal(GameMode.Unknown, GameLogParser.ModeFrom("Arena"));
        Assert.Equal(known.LoggedAt, unknown.LoggedAt);
        Assert.Equal("The game says 'Arena', which Shturmap doesn't know yet; showing the mode you last played", unknown.Tooltip(true, Now));
        // A known mode later in the log clears it.
        Assert.Equal("From the game's log, 21:58", unknown.Read(Said("Pve", new DateTime(2026, 10, 3, 21, 58, 0))).Tooltip(true, Now));
    }

    [Theory]
    [InlineData(GameMode.Pve, GameMode.Unknown, GameMode.Pve)]
    [InlineData(GameMode.Seasonal, GameMode.Unknown, GameMode.Seasonal)]
    [InlineData(GameMode.Pve, GameMode.Pvp, GameMode.Pvp)]
    [InlineData(GameMode.Pvp, GameMode.Seasonal, GameMode.Seasonal)]
    public void The_session_follows_the_log_but_keeps_the_last_known_mode(GameMode current, GameMode logged, GameMode shown) =>
        Assert.Equal(shown, ModeReading.Follow(current, logged));
}
