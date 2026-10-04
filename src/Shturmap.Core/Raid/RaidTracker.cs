using Shturmap.Core.Logs;

namespace Shturmap.Core.Raid;

public enum RaidPhase
{
    Menu,
    Loading,
    InRaid,
}

public enum RaidSide
{
    Unknown,
    Pmc,
    Scav,
}

/// <summary>Where the game is, as far as the logs can tell.</summary>
public sealed record RaidState
{
    public RaidPhase Phase { get; init; } = RaidPhase.Menu;

    public GameMode Mode { get; init; } = GameMode.Unknown;

    /// <summary>"maps/city_preset.bundle" from the scene line; the most reliable map identity.</summary>
    public string? ScenePath { get; init; }

    /// <summary>Location id from match setup or transit lines, e.g. "TarkovStreets" or "bigmap".</summary>
    public string? LocationId { get; init; }

    public string? RaidId { get; init; }

    public DateTime? LoadingSince { get; init; }

    public DateTime? RaidStartedAt { get; init; }

    public RaidSide Side { get; init; } = RaidSide.Unknown;

    /// <summary>The last loading step the log reported while loading; null before the first.</summary>
    public LoadingStep? LoadingStep { get; init; }

    /// <summary>Every loading step the log reported for this load, one bit per <see cref="Logs.LoadingStep"/>.</summary>
    public int LoadingStepsSeen { get; init; }
}

public abstract record RaidTransition(RaidState State);

public sealed record RaidLoading(RaidState State) : RaidTransition(State);

public sealed record RaidStarted(RaidState State) : RaidTransition(State);

/// <summary>The raid ended (or the load was abandoned) and the game is back in the menus.</summary>
/// <param name="At">When the log said so. For an end the log never told, the last the log said of the raid: not its end.</param>
/// <param name="EndInLog">False when no line of the log ended it (the game was closed or crashed in the raid, see
/// <see cref="UnfinishedRaid"/>): the raid is over, but nobody may take its length from <paramref name="At"/>.</param>
public sealed record RaidEnded(RaidState Previous, RaidState State, DateTime At, bool EndInLog = true) : RaidTransition(State)
{
    /// <summary>How long the raid ran, when the log has both its start and its end; null for a load, and for an end
    /// that isn't in the log.</summary>
    public TimeSpan? Length => LengthIn(null);

    /// <summary><see cref="Length"/> by a given time zone's clock changes (the PC's own when null): the log's times
    /// are wall-clock times, and a raid across a clock change ran an hour longer or shorter than they are apart
    /// (<see cref="WallClock"/>).</summary>
    public TimeSpan? LengthIn(TimeZoneInfo? zone) =>
        EndInLog && Previous.RaidStartedAt is { } started ? WallClock.Elapsed(started, At, zone) : null;
}

public sealed record ModeChanged(RaidState State) : RaidTransition(State);

/// <summary>
/// Turns log events into raid state. Deterministic and side-effect free, so a whole log session can be
/// replayed through it in tests.
/// </summary>
public sealed class RaidTracker
{
    // Side detection, checked against real logs:
    // - The menu always loads the main (PMC) profile. When a server-hosted raid is set up, its match-setup line
    //   names the joining profile: the same id means a PMC raid, a different id means the Scav profile.
    // - Scav raids join in progress and log GameStarted without a GameStarting before it.
    // - Locally hosted raids (no match-setup line, GameStarting and GameStarted together) stay Unknown.
    private string? _menuProfileId;
    private string? _setupProfileId;
    private bool _sawStarting;

    public RaidState State { get; private set; } = new();

    public RaidTransition? Apply(GameEvent e)
    {
        switch (e)
        {
            case SessionModeEvent m when m.Mode != State.Mode:
                State = State with { Mode = m.Mode };
                return new ModeChanged(State);

            case MapLoadingEvent load:
                // A new map loading while in a raid is a transit to another location.
                _setupProfileId = null;
                _sawStarting = false;
                State = State with
                {
                    Phase = RaidPhase.Loading,
                    ScenePath = load.ScenePath,
                    LocationId = null,
                    RaidId = State.Phase == RaidPhase.InRaid ? State.RaidId : null,
                    LoadingSince = e.At,
                    RaidStartedAt = null,
                    Side = RaidSide.Unknown,
                    LoadingStep = null,
                    LoadingStepsSeen = 0,
                };
                _steps.Clear();
                return new RaidLoading(State);

            case LoadingStepEvent step when State.Phase == RaidPhase.Loading:
                // Matching can finish before the scene line (then it belongs to no loading) or after it.
                if (_steps.All(s => s.Step != step.Step))
                    _steps.Add((step.Step, WallClock.Elapsed(State.LoadingSince!.Value, e.At).TotalSeconds));
                if (step.Step != Logs.LoadingStep.MatchingCompleted)
                    State = State with { LoadingStep = step.Step, LoadingStepsSeen = State.LoadingStepsSeen | (1 << (int)step.Step) };
                return null;

            case MatchSetupEvent setup when State.Phase == RaidPhase.Loading:
                _setupProfileId = setup.ProfileId ?? _setupProfileId;
                State = State with
                {
                    LocationId = setup.LocationId ?? State.LocationId,
                    RaidId = setup.ShortId ?? State.RaidId,
                    // A server-hosted raid's setup names the joining profile while it still loads: the side is known
                    // then already (the kit reminder leaves a Scav's loading alone). The raid start decides it again.
                    Side = _setupProfileId is not null && _menuProfileId is not null
                        ? _setupProfileId == _menuProfileId ? RaidSide.Pmc : RaidSide.Scav
                        : State.Side,
                };
                return null;

            case TransitInfoEvent transit when State.Phase != RaidPhase.Menu:
                State = State with
                {
                    LocationId = State.LocationId ?? transit.LocationId,
                    RaidId = State.RaidId ?? transit.RaidId,
                };
                return null;

            case GameStartingEvent when State.Phase == RaidPhase.Loading:
                _sawStarting = true;
                return null;

            case GameStartedEvent when State.Phase == RaidPhase.Loading:
                _steps.Add((null, WallClock.Elapsed(State.LoadingSince!.Value, e.At).TotalSeconds));
                LoadingSteps = _steps.ToList();
                State = State with { Phase = RaidPhase.InRaid, RaidStartedAt = e.At, Side = DetectSide(), LoadingStep = null, LoadingStepsSeen = 0 };
                return new RaidStarted(State);

            case ProfileLoadedEvent loaded:
                _menuProfileId = loaded.ProfileId ?? _menuProfileId;
                return State.Phase == RaidPhase.Menu ? null : EndRaid(e.At);

            case MatchingCancelledEvent when State.Phase != RaidPhase.Menu:
                return EndRaid(e.At);

            default:
                return null;
        }
    }

    /// <summary>
    /// Closes a raid or a load whose end the log never told (<see cref="UnfinishedRaid"/>): back to the menus, with the
    /// end marked as not in the log. Null when no raid is open.
    /// </summary>
    /// <param name="lastSaid">The last the log said of the raid; kept as the transition's time, never as the raid's end.</param>
    public RaidEnded? CloseUnfinished(DateTime lastSaid) => State.Phase == RaidPhase.Menu ? null : EndRaid(lastSaid, endInLog: false);

    /// <summary>What the side of the last raid start was decided from, for the study log: "setup:same,starting:yes".</summary>
    public string SideEvidence { get; private set; } = "";

    /// <summary>
    /// The last raid's loading, for the study log: each step with its seconds since the scene line, in the order
    /// they came; the raid start is the entry without a step.
    /// </summary>
    public IReadOnlyList<(LoadingStep? Step, double Seconds)> LoadingSteps { get; private set; } = [];

    private readonly List<(LoadingStep? Step, double Seconds)> _steps = [];

    private RaidSide DetectSide()
    {
        var setup = _setupProfileId is null ? "none" : _menuProfileId is null ? "no-menu" : _setupProfileId == _menuProfileId ? "same" : "other";
        SideEvidence = $"setup:{setup},starting:{(_sawStarting ? "yes" : "no")}";
        if (_setupProfileId is not null && _menuProfileId is not null)
            return _setupProfileId == _menuProfileId ? RaidSide.Pmc : RaidSide.Scav;
        return _sawStarting ? RaidSide.Unknown : RaidSide.Scav;
    }

    private RaidEnded EndRaid(DateTime at, bool endInLog = true)
    {
        var previous = State;
        _setupProfileId = null;
        _sawStarting = false;
        State = new RaidState { Mode = State.Mode };
        return new RaidEnded(previous, State, at, endInLog);
    }
}

/// <summary>
/// A raid or a load the log left open. The application log has no line for the game quitting, so a raid the game was
/// closed or crashed in never gets its end line: read back the next day it would still be "in raid", 1,310 minutes
/// long, and the next game start's login line would then end it as a raid of 22 hours (review of 2026-10-04). Two
/// things the log does show say that such a raid can't still be running, and nothing else may: a newer log session
/// (the game has started again), and the time since it began.
/// </summary>
public static class UnfinishedRaid
{
    /// <summary>
    /// How long past its map's raid length an open raid still counts as running. Generous on purpose, because dropping
    /// a live raid would be the worse mistake: tarkov.dev's length may be behind a patch (raid timers have moved by 5
    /// to 15 minutes), a Scav's or a transit's timer isn't the map's, and the end line only comes after the screens
    /// that follow a raid. A raid left open the evening before is hours past it.
    /// </summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(30);

    /// <summary>The bound for a map whose raid length isn't known (no data yet, or a map the data doesn't have): well
    /// past the longest raid length in the data, with the same margin.</summary>
    public static readonly TimeSpan WithoutLength = TimeSpan.FromHours(2);

    /// <summary>How long after it began an open raid or load can still be running.</summary>
    /// <param name="raidMinutes">The map's raid length in the data, in minutes; null or 0 when it isn't known.</param>
    public static TimeSpan Bound(int? raidMinutes) => raidMinutes is > 0 ? TimeSpan.FromMinutes(raidMinutes.Value) + Margin : WithoutLength;

    /// <summary>
    /// Whether an open raid or load can't still be running: the game has started again since (a newer log session than
    /// the raid's), or it began longer ago than <see cref="Bound"/>. A load counts from its scene line (matching and
    /// loading take minutes, never that long). The time since it began is the time that passed, not the two clock
    /// times apart: on the night the clocks go forward those are an hour more, which closed a raid still running, and
    /// a raid that began in the hour the autumn change repeats counts from its later reading (<see cref="WallClock"/>).
    /// </summary>
    /// <param name="zone">The time zone of the clock times; the PC's own unless a test gives another.</param>
    public static bool CannotStillRun(RaidState state, DateTime now, int? raidMinutes, bool newerSession, TimeZoneInfo? zone = null)
    {
        if (state.Phase == RaidPhase.Menu)
            return false;
        if (newerSession)
            return true;
        return (state.RaidStartedAt ?? state.LoadingSince) is { } began && WallClock.Elapsed(began, now, zone) > Bound(raidMinutes);
    }
}
