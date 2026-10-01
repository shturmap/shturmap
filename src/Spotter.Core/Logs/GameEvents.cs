namespace Spotter.Core.Logs;

public enum GameMode
{
    Unknown,
    Pvp,
    Pve,
    Seasonal,
}

public enum QuestStatus
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
public sealed record QuestEvent(DateTime At, string QuestId, QuestStatus Status, string EventId, string? TraderId) : GameEvent(At);
