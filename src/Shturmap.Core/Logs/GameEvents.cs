namespace Shturmap.Core.Logs;

public enum GameMode
{
    Unknown,
    Pvp,
    Pve,
    Seasonal,
}

public enum QuestLogStatus
{
    Started,
    Failed,
    Completed,
}

/// <summary>Something the game's logs tell us.</summary>
public abstract record GameEvent(DateTime At);

/// <summary>"Session mode: Pve" — which profile the game session plays.</summary>
public sealed record SessionModeEvent(DateTime At, GameMode Mode, string Raw) : GameEvent(At);

/// <summary>"scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset" — a map starts loading.</summary>
public sealed record MapLoadingEvent(DateTime At, string ScenePath, string? Rcid) : GameEvent(At);

/// <summary>
/// Match set up on the server: the location's nameId (e.g. "TarkovStreets"), the raid's short id, and the
/// profile that joins it. That profile differs from the menu profile when the raid is a Scav raid.
/// </summary>
public sealed record MatchSetupEvent(DateTime At, string? LocationId, string? ShortId, string? ProfileId) : GameEvent(At);

/// <summary>"[Transit] … RaidId:…, Locations:bigmap -> " — the raid id and its location.</summary>
public sealed record TransitInfoEvent(DateTime At, string? RaidId, string? LocationId) : GameEvent(At);

public sealed record GameStartingEvent(DateTime At) : GameEvent(At);

public sealed record GameStartedEvent(DateTime At) : GameEvent(At);

/// <summary>
/// "PrepareSelectedProfileLocally ProfileId:…" — the main (PMC) profile is (re)loaded: after login and after
/// every raid.
/// </summary>
public sealed record ProfileLoadedEvent(DateTime At, string? ProfileId) : GameEvent(At);

public sealed record MatchingCancelledEvent(DateTime At) : GameEvent(At);

/// <summary>A quest started, failed or was completed (push-notifications ChatMessageReceived, types 10–12).</summary>
public sealed record QuestEvent(DateTime At, string QuestId, QuestLogStatus Status, string EventId, string? TraderId) : GameEvent(At);

/// <summary>
/// "GroupMatchRaidSettings": the group's leader picked a raid. Location is the map's nameId ("Sandbox_high",
/// "bigmap"); TimeVariant is "CURR" or "PAST", one of the two raid times 12 hours apart.
/// </summary>
public sealed record GroupRaidSettingsEvent(DateTime At, string LocationId, string? TimeVariant) : GameEvent(At);

public enum GroupStatus
{
    Ready,
    NotReady,
    Start,
}

/// <summary>
/// "GroupMatchRaidReady", "GroupMatchRaidNotReady", "GroupMatchStartGame". Only the kind is read: their bodies hold
/// other players' profiles, which Shturmap never looks at.
/// </summary>
public sealed record GroupStatusEvent(DateTime At, GroupStatus Status) : GameEvent(At);

/// <summary>The steps a raid's loading logs between the scene line and "GameStarted", in the order they come.</summary>
public enum LoadingStep
{
    MatchingCompleted,
    LocationLoaded,
    GamePrepared,
    GameCreated,
    PlayerSpawned,
    GamePooled,
    GameRunning,
}

/// <summary>"LocationLoaded:9.61 real:13.28 …", "PlayerSpawnEvent:…" — a loading step done.</summary>
public sealed record LoadingStepEvent(DateTime At, LoadingStep Step) : GameEvent(At);

public enum InsuranceNotice
{
    /// <summary>A trader message (type 2) naming a raid's location: the insurer's note that insured gear was lost.</summary>
    Lost,

    /// <summary>The insurance return (type 8): the gear that came back, hours later.</summary>
    Returned,
}

/// <summary>An insurer's message about a raid (ChatMessageReceived types 2 and 8 with systemData.location).</summary>
public sealed record InsuranceNoticeEvent(DateTime At, InsuranceNotice Kind, string LocationId, int ItemCount) : GameEvent(At);
