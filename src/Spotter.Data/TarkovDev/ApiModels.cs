using System.Text.Json.Serialization;
using Spotter.Core;

namespace Spotter.Data.TarkovDev;

// Shapes of json.tarkov.dev's maps, tasks and traders payloads (after translation). Only the fields Spotter uses
// are modelled; everything else is ignored. References between records are ids.

public sealed record ApiPosition(double X, double Y, double Z)
{
    public WorldPoint ToWorld() => new(X, Y, Z);
}

public sealed record ApiExtract(
    string Id,
    string? Name,
    string? Faction,
    ApiPosition? Position,
    List<ApiPosition>? Outline,
    double? Top,
    double? Bottom);

public sealed record ApiTransit(string Id, string? Description, string? Map, ApiPosition? Position, List<ApiPosition>? Outline);

public sealed record ApiLock(string? Id, string? LockType, string? Key, bool NeedsPower, ApiPosition? Position);

public sealed record ApiHazard(string? HazardType, string? Name, ApiPosition? Position, List<ApiPosition>? Outline, double? Top, double? Bottom);

/// <param name="Mob">Id into the payload's mobs, e.g. "bossBoar"; AI PMCs appear as "pmcBEAR"/"pmcUSEC".</param>
public sealed record ApiBoss(string Mob, double SpawnChance);

public sealed record ApiMob(string Id, string Name);

/// <summary>A place where loose loot can spawn, and which items can spawn there.</summary>
public sealed record ApiLootSpawn(ApiPosition? Position, List<string>? Items);

public sealed record ApiMap(
    string Id,
    string Name,
    string NormalizedName,
    string NameId,
    string? ScenePath,
    string? Wiki,
    int? RaidDuration,
    List<ApiExtract>? Extracts,
    List<ApiTransit>? Transits,
    List<ApiLock>? Locks,
    List<ApiHazard>? Hazards,
    List<ApiBoss>? Bosses,
    List<ApiLootSpawn>? LootLoose = null);

// Where items come from (json.tarkov.dev items, barters, crafts, hideout). Prices are only used to say which trader
// offer is the cheap one; Spotter has no price views (docs/DESIGN.md §1).

public sealed record ApiTraderOffer(string Trader, double Price, string? Currency, double? PriceRUB, int MinTraderLevel, string? TaskUnlock);

/// <param name="Types">Includes "noFlea" for items that can't be sold on the flea market.</param>
public sealed record ApiItem(string Id, List<string>? Types, int? MinLevelForFlea, double? LastLowPrice, List<ApiTraderOffer>? BuyFromTrader);

public sealed record ApiCount(string Item, double Count);

public sealed record ApiBarter(string Trader, int MinTraderLevel, string? TaskUnlock, List<ApiCount>? RequiredItems, ApiCount? OfferedItem);

public sealed record ApiCraft(string Station, int Level, List<ApiCount>? RequiredItems, ApiCount? ProductItem);

public sealed record ApiStation(string Id, string Name);

internal sealed record ApiItemsData(Dictionary<string, ApiItem>? Items);

internal sealed record ApiItemsEnvelope(ApiItemsData? Data);

internal sealed record ApiBartersEnvelope(List<ApiBarter>? Data);

internal sealed record ApiCraftsEnvelope(List<ApiCraft>? Data);

public sealed record ApiZone(string? Id, string? Name, string? Map, ApiPosition? Position, List<ApiPosition>? Outline, double? Top, double? Bottom);

public sealed record ApiPossibleLocation(string? Map, List<ApiPosition>? Positions);

/// <param name="Items">Items the objective takes or wants (plant, hand in, find); any one of them will do.</param>
/// <param name="MarkerItem">The marker a "mark" objective uses, e.g. the MS2000.</param>
/// <param name="UseAny">Items a "useItem" objective accepts, e.g. signal flares.</param>
/// <param name="RequiredKeys">Ways in: each inner list is a set of alternative keys.</param>
public sealed record ApiObjective(
    string Id,
    string? Type,
    string? Description,
    bool Optional,
    List<string>? Maps,
    List<ApiZone>? Zones,
    List<ApiPossibleLocation>? PossibleLocations,
    int? Count,
    string? QuestItem,
    List<string>? Items,
    string? MarkerItem,
    List<string>? UseAny,
    List<List<string>>? RequiredKeys,
    bool FoundInRaid);

public sealed record ApiTaskRequirement(string Task, List<string>? Status);

public sealed record ApiNeededKeys(string? Map, List<string>? Keys);

public sealed record ApiTask(
    string Id,
    string Name,
    string? NormalizedName,
    string? Trader,
    string? Map,
    int? MinPlayerLevel,
    bool KappaRequired,
    bool LightkeeperRequired,
    string? WikiLink,
    string? TaskImageLink,
    string? FactionName,
    bool Restartable,
    List<ApiTaskRequirement>? TaskRequirements,
    List<ApiObjective>? Objectives,
    List<ApiNeededKeys>? NeededKeys);

public sealed record ApiTrader(string Id, string Name, string? NormalizedName, string? ImageLink);

public sealed record ApiQuestItem(string Id, string Name, string? ShortName);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Dictionary<string, ApiMap>))]
[JsonSerializable(typeof(Dictionary<string, ApiTask>))]
[JsonSerializable(typeof(Dictionary<string, ApiTrader>))]
[JsonSerializable(typeof(Dictionary<string, ApiQuestItem>))]
[JsonSerializable(typeof(Dictionary<string, ApiMob>))]
[JsonSerializable(typeof(Dictionary<string, ApiStation>))]
[JsonSerializable(typeof(ApiItemsEnvelope))]
[JsonSerializable(typeof(ApiBartersEnvelope))]
[JsonSerializable(typeof(ApiCraftsEnvelope))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
