using System.Text.Json.Serialization;
using Shturmap.Core;

namespace Shturmap.Data.TarkovDev;

// Shapes of json.tarkov.dev's maps, tasks and traders payloads (after translation). Only the fields Shturmap uses
// are modelled; everything else is ignored. References between records are ids.

public sealed record ApiPosition(double X, double Y, double Z)
{
    public WorldPoint ToWorld() => new(X, Y, Z);
}

/// <param name="Switches">Ids of switches (in the map's Switches) the extract depends on.</param>
/// <param name="TransferItem">What has to be handed over to leave: roubles for a car, a special item for a secret exit.</param>
public sealed record ApiExtract(
    string Id,
    string? Name,
    string? Faction,
    ApiPosition? Position,
    List<ApiPosition>? Outline,
    double? Top,
    double? Bottom,
    List<string>? Switches = null,
    ApiCount? TransferItem = null);

/// <param name="Conditions">What the transit needs, in words ("TerraGroup Labs access keycard required (1)"), if anything.</param>
public sealed record ApiTransit(string Id, string? Description, string? Map, ApiPosition? Position, List<ApiPosition>? Outline,
    string? Conditions = null);

/// <param name="Name">What the game calls it, translated ("Med Elevator Power Button", "Alarm Switch").</param>
/// <param name="Position">Where it is on the map; null when tarkov.dev places it nowhere.</param>
/// <param name="Activates">What flipping it does to other switches ("Unlock": a power switch frees a lever).</param>
public sealed record ApiSwitch(string Id, string? Name, ApiPosition? Position = null, List<ApiSwitchActivation>? Activates = null);

public sealed record ApiSwitchActivation(string? Operation, string? Switch);

/// <summary>A spawn point: who can spawn there ("scav", "pmc", "all") and as what ("bot", "player", "boss", "all").</summary>
public sealed record ApiSpawn(ApiPosition? Position, List<string>? Sides, List<string>? Categories, string? ZoneName);

public sealed record ApiBossLocation(string? Name, double Chance, List<ApiPosition>? Positions);

/// <param name="LockType">"door", "trunk" (a car's) or "container".</param>
/// <param name="Key">The item id of the key that opens it.</param>
public sealed record ApiLock(string? Id, string? LockType, string? Key, bool NeedsPower, ApiPosition? Position);

/// <summary>Where a loot container stands (a safe, a PC, a crate…); <paramref name="LootContainer"/> is its type's id.</summary>
public sealed record ApiContainerSpot(string? LootContainer, ApiPosition? Position);

public sealed record ApiHazard(string? HazardType, string? Name, ApiPosition? Position, List<ApiPosition>? Outline, double? Top, double? Bottom);

/// <param name="Mob">Id into the payload's mobs, e.g. "bossBoar"; AI PMCs appear as "pmcBEAR"/"pmcUSEC".</param>
/// <param name="SpawnLocations">Where it can spawn, each with its share of the spawns and its points.</param>
public sealed record ApiBoss(string Mob, double SpawnChance, List<ApiBossLocation>? SpawnLocations = null);

public sealed record ApiMob(string Id, string Name);

/// <summary>A place where loose loot can spawn, and which items can spawn there.</summary>
public sealed record ApiLootSpawn(ApiPosition? Position, List<string>? Items);

/// <param name="AccessKeys">What it takes to enter the map at all (The Lab's TerraGroup Labs access keycard), as item ids.</param>
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
    List<ApiLootSpawn>? LootLoose = null,
    List<ApiSwitch>? Switches = null,
    List<ApiSpawn>? Spawns = null,
    List<ApiContainerSpot>? LootContainers = null,
    List<string>? AccessKeys = null);

// Where items come from (json.tarkov.dev items, barters, crafts, hideout). Outside the stash desk prices are only used to
// say which trader offer is the cheap one; the desk (docs/DESIGN.md §1, the third answer) reads the item file's price
// fields, its footprints and the flea market's rules (Shturmap.Core.Desk).

public sealed record ApiTraderOffer(string Trader, double Price, string? Currency, double? PriceRUB, int MinTraderLevel, string? TaskUnlock);

/// <summary>What a trader pays for an item (the item file's <c>sellToTrader</c>), in its currency and in roubles.</summary>
public sealed record ApiTraderPrice(string Trader, double Price, string? Currency, double? PriceRUB);

/// <param name="Types">Includes "noFlea" for items that can't be sold on the flea market.</param>
/// <param name="Categories">The game's item categories, most specific first ("Sniper rifle", then "Weapon", then
/// "Item"), as ids; their names are item-name entries (<see cref="GameData.ItemName"/>).</param>
/// <param name="Properties">Of a gun, its default preset (<see cref="ApiItemProperties"/>); null for most items.</param>
/// <param name="Avg24hPrice">The flea's average over the last 24 hours (the stash desk's card; null without offers).</param>
/// <param name="Low24hPrice">The day's lowest offer.</param>
/// <param name="High24hPrice">The day's highest offer: one outlier offer can set it (9,999,999 seen), never a ceiling.</param>
/// <param name="ChangeLast48hPercent">The last low's change over 48 hours, in percent (−10.33).</param>
/// <param name="LastOfferCount">How many offers the last scan saw: the desk's liquidity.</param>
/// <param name="SellToTrader">What each trader pays for it, Fence among them.</param>
/// <param name="BasePrice">The game's base price, the flea fee's other input.</param>
/// <param name="Updated">When tarkov.dev's prices for it were last scanned (ISO 8601, UTC).</param>
/// <param name="Width">Its footprint in cells, as it lies unrotated: the desk's tie-break between items of one short name.</param>
/// <param name="HasGrid">It holds a grid of its own (a backpack, a rig, a case, a wallet): on the desk its contents are
/// unseen until its window is read.</param>
/// <param name="StackMaxSize">The most of it one cell stacks (1 for an item that doesn't stack): on the desk a count read
/// past it is a misread (a drill read "×310" on 2026-10-19).</param>
public sealed record ApiItem(string Id, List<string>? Types, int? MinLevelForFlea, double? LastLowPrice, List<ApiTraderOffer>? BuyFromTrader,
    List<string>? Categories = null, ApiItemProperties? Properties = null,
    double? Avg24hPrice = null, double? Low24hPrice = null, double? High24hPrice = null, double? ChangeLast48hPercent = null,
    int? LastOfferCount = null, List<ApiTraderPrice>? SellToTrader = null, double? BasePrice = null, string? Updated = null,
    int? Width = null, int? Height = null, bool? HasGrid = null, int? StackMaxSize = null);

/// <summary>The flea market's rules in the item file (<c>data.fleaMarket</c>): the fee's two rates, the level it opens
/// at, and whether an item must be found in raid to be listed (the game's globals; false in PvE on 2026-10-19).</summary>
public sealed record ApiFleaMarket(int? MinPlayerLevel = null, bool? Enabled = null, double? SellOfferFeeRate = null,
    double? SellRequirementFeeRate = null, bool? FoundInRaidRequired = null);

/// <summary>An item's properties, of which only a gun's default preset is read.</summary>
/// <param name="DefaultPreset">A gun's default preset: an item of its own (type "preset"), the gun assembled as the
/// traders sell it, whose icon pictures the gun (<see cref="ItemSources.PictureId"/>; owner, 2026-10-19). Null for
/// every other item, and for a weapon without one (a knife, a grenade, a launcher).</param>
public sealed record ApiItemProperties(string? DefaultPreset = null);

public sealed record ApiCount(string Item, double Count);

public sealed record ApiBarter(string Trader, int MinTraderLevel, string? TaskUnlock, List<ApiCount>? RequiredItems, ApiCount? OfferedItem);

public sealed record ApiCraft(string Station, int Level, List<ApiCount>? RequiredItems, ApiCount? ProductItem);

/// <param name="Levels">Its levels with what each takes to build (the stash desk's hideout need); null when not read.</param>
public sealed record ApiStation(string Id, string Name, List<ApiStationLevel>? Levels = null);

public sealed record ApiStationLevel(int Level, List<ApiItemRequirement>? ItemRequirements = null);

/// <summary>An item a hideout level takes, with whether it must be found in raid (<c>attributes.foundInRaid</c>).</summary>
public sealed record ApiItemRequirement(string Item, double Count, ApiRequirementAttributes? Attributes = null);

public sealed record ApiRequirementAttributes(bool? FoundInRaid = null);

internal sealed record ApiItemsData(Dictionary<string, ApiItem>? Items, ApiFleaMarket? FleaMarket = null);

internal sealed record ApiItemsEnvelope(ApiItemsData? Data);

internal sealed record ApiBartersEnvelope(List<ApiBarter>? Data);

internal sealed record ApiCraftsEnvelope(List<ApiCraft>? Data);

public sealed record ApiZone(string? Id, string? Name, string? Map, ApiPosition? Position, List<ApiPosition>? Outline, double? Top, double? Bottom);

public sealed record ApiPossibleLocation(string? Map, List<ApiPosition>? Positions);

/// <param name="Items">Items the objective takes or wants (plant, hand in, find); any one of them will do.</param>
/// <param name="MarkerItem">The marker a "mark" objective uses, e.g. the MS2000.</param>
/// <param name="UseAny">Items a "useItem" objective accepts, e.g. signal flares.</param>
/// <param name="RequiredKeys">Ways in: each inner list is a set of alternative keys.</param>
/// <param name="Wearing">Gear to wear for a kill objective: each inner list is a set worn together; any set will do.</param>
/// <param name="UsingWeapon">Weapons a kill objective accepts: any one of them will do (a class, such as every
/// bolt-action rifle, arrives as all its members).</param>
/// <param name="UsingWeaponMods">Mods a kill objective's weapon must carry: each inner list is a set fitted together;
/// any set will do.</param>
/// <param name="NotWearing">Gear a kill objective forbids (body armor, helmets): none of it may be worn.</param>
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
    bool FoundInRaid,
    List<List<ApiItemRef>>? Wearing = null,
    List<string>? UsingWeapon = null,
    List<List<string>>? UsingWeaponMods = null,
    List<ApiItemRef>? NotWearing = null);

/// <summary>
/// What an objective asks, in the game's own keys rather than words: tarkov.dev translates these fields, so the
/// values below come from before translation and are the same in every language.
/// </summary>
/// <param name="Targets">A kill objective's targets: "Savage", "AnyPmc", "bossKnight", …</param>
/// <param name="ExitStatus">An extract objective's accepted exit statuses: "ExpBonusSurvived", "marathon Name", …</param>
/// <param name="Conditions">A kill objective's conditions that are set, by name: "weapon", "distance", "zone", …
/// (see <see cref="GameDataLoader"/>).</param>
/// <param name="Exit">An extract objective's exit as the game's internal name ("E9_sniper" for Klimov Street (Flare)),
/// the same name the map's extracts carry (<see cref="GameData.ExtractKeys"/>); null when it names none.</param>
public sealed record ObjectiveFacts(IReadOnlyList<string> Targets, IReadOnlyList<string> ExitStatus, IReadOnlyList<string> Conditions,
    string? Exit = null);

/// <summary>An item named in an objective's conditions.</summary>
public sealed record ApiItemRef(string Id, string? Name);

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
