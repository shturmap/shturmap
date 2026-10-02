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
public sealed record RaidEnded(RaidState Previous, RaidState State, DateTime At) : RaidTransition(State);

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
                    _steps.Add((step.Step, (e.At - State.LoadingSince!.Value).TotalSeconds));
                if (step.Step != Logs.LoadingStep.MatchingCompleted)
                    State = State with { LoadingStep = step.Step, LoadingStepsSeen = State.LoadingStepsSeen | (1 << (int)step.Step) };
                return null;

            case MatchSetupEvent setup when State.Phase == RaidPhase.Loading:
                _setupProfileId = setup.ProfileId ?? _setupProfileId;
                State = State with
                {
                    LocationId = setup.LocationId ?? State.LocationId,
                    RaidId = setup.ShortId ?? State.RaidId,
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
                _steps.Add((null, (e.At - State.LoadingSince!.Value).TotalSeconds));
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

    private RaidEnded EndRaid(DateTime at)
    {
        var previous = State;
        _setupProfileId = null;
        _sawStarting = false;
        State = new RaidState { Mode = State.Mode };
        return new RaidEnded(previous, State, at);
    }
}
