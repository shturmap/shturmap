using Shturmap.Core;
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
        GameMode.Pvp => SessionTexts.ModePvp,
        GameMode.Seasonal => SessionTexts.ModeSeasonal,
        _ => SessionTexts.ModePve,
    };

    /// <summary>Where the shown mode comes from, saying only what was read.</summary>
    public string Tooltip(bool gameFound, DateTime now)
    {
        if (Unknown is { } raw)
            return SessionTexts.ModeTipUnknown(mode: raw);
        if (LoggedAt is { } at)
            return SessionTexts.ModeTipFromLog(time: at.Date == now.Date ? at.ToString("HH:mm", UiLanguage.Culture) : UiLanguage.DayMonthTime(at));
        return gameFound ? SessionTexts.ModeTipLastPlayed : SessionTexts.ModeTipNoGame;
    }
}
