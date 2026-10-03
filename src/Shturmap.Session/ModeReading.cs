using System.Globalization;
using Shturmap.Core.Logs;

namespace Shturmap.Session;

/// <summary>
/// What the game's log said about the mode, for the status bar's label (owner, 2026-10-03: the mode comes from the
/// log alone, no chooser; docs/DESIGN.md, "Screen anatomy"). <see cref="LoggedAt"/> is the time of the last line that
/// named a known mode; <see cref="Unknown"/> the raw value of a later line Shturmap can't read. The mode itself stays
/// the last known one either way (<see cref="GameSession"/> ignores an unknown mode).
/// </summary>
public sealed record ModeReading(DateTime? LoggedAt = null, string? Unknown = null)
{
    public ModeReading Read(SessionModeEvent e) => e.Mode == GameMode.Unknown ? this with { Unknown = e.Raw } : new(e.At, null);

    /// <summary>The mode to show after the log names one: a mode Shturmap doesn't know keeps the current one.</summary>
    public static GameMode Follow(GameMode current, GameMode logged) => logged == GameMode.Unknown ? current : logged;

    public static string Text(GameMode mode) => mode switch
    {
        GameMode.Pvp => "PvP",
        GameMode.Seasonal => "Seasonal",
        _ => "PvE",
    };

    /// <summary>Where the shown mode comes from, saying only what was read.</summary>
    public string Tooltip(bool gameFound, DateTime now)
    {
        if (Unknown is { } raw)
            return $"The game says '{raw}', which Shturmap doesn't know yet; showing the mode you last played";
        if (LoggedAt is { } at)
            return "From the game's log, " + at.ToString(at.Date == now.Date ? "HH:mm" : "d MMM HH:mm", CultureInfo.CurrentCulture);
        return gameFound ? "The mode you last played; follows the game once it starts" : "No game on this PC: the mode you last played";
    }
}
