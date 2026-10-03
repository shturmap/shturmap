using System.Globalization;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>
/// What an extract or transit needs, in a few words: "Pay 5,000 ₽", "Red Rebel ice pick and paracord, no armored
/// rig", "ZB-013 Power Switch first". From tarkov.dev's fields (transfer items, switches, transit conditions) and the
/// extract's internal name where no field says it (the climbing exits, flare and co-op exits).
/// </summary>
public static class ExtractRules
{
    private const string Roubles = "5449016a4bdc2d6f028b456f";
    private const string Dollars = "5696686a4bdc2da3298b456a";
    private const string Euros = "569668774bdc2da2298b4568";

    /// <returns>The requirement text (empty if none) and an item to picture, if one has to be handed over.</returns>
    public static (string Text, string? ItemId) Needs(GameData data, ApiMap map, ApiExtract extract)
    {
        var parts = new List<string>();
        string? item = null;
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id) ?? "";
        var name = extract.Name ?? "";

        if (key.Contains("Alpinist", StringComparison.OrdinalIgnoreCase) || key.Contains("RedRebel", StringComparison.OrdinalIgnoreCase))
            parts.Add("Red Rebel ice pick and paracord, no armored rig");
        if (name.Contains("(Flare)", StringComparison.OrdinalIgnoreCase) || key.Contains("sniper", StringComparison.OrdinalIgnoreCase))
            parts.Add("Fire a red signal flare there");
        if (name.Contains("(Co-op)", StringComparison.OrdinalIgnoreCase))
            parts.Add("Co-op: a PMC and a player Scav leave together");

        if (extract.TransferItem is { Count: > 0 } transfer)
        {
            var amount = transfer.Count.ToString("N0", CultureInfo.CurrentCulture);
            switch (transfer.Item)
            {
                case Roubles:
                    parts.Add($"Pay {amount} ₽");
                    break;
                case Dollars:
                    parts.Add($"Pay ${amount}");
                    break;
                case Euros:
                    parts.Add($"Pay €{amount}");
                    break;
                default:
                    parts.Add("Hand over " + data.ItemName(transfer.Item) + (transfer.Count > 1 ? $" ×{amount}" : ""));
                    item = transfer.Item;
                    break;
            }
        }

        // tarkov.dev lists some switches on every extract of a map (all of Customs points at the ZB-013 power
        // switch). A switch listed by most extracts is only shown where its name says it belongs.
        var extracts = map.Extracts ?? [];
        foreach (var id in extract.Switches ?? [])
        {
            if (map.Switches?.FirstOrDefault(s => s.Id == id)?.Name is not { Length: > 0 } switchName)
                continue;
            var common = extracts.Count(e => e.Switches?.Contains(id) == true) * 2 > extracts.Count;
            if (!common || (name.Length > 0 && switchName.Contains(name, StringComparison.OrdinalIgnoreCase)))
                parts.Add(switchName + " first");
        }
        return (string.Join(" · ", parts.Distinct()), item);
    }

    // Items the rules above name in words: the red signal flare (RSP-30 red cartridge) fired at a flare exit, and the
    // climbing exits' ice pick and paracord. Game item ids, which don't change.
    public const string RedFlare = "62178c4d4ecf221597654e3d";
    public const string IcePick = "5c0126f40db834002a125382";
    public const string Paracord = "5c12688486f77426843c7d32";

    /// <summary>
    /// The items leaving through an extract takes, by the same rules as <see cref="Needs(GameData, ApiMap, ApiExtract)"/>:
    /// a red flare at a flare exit, ice pick and paracord at a climbing exit, the money or item a paid exit asks for.
    /// Empty when it takes none (switches and co-op exits take no item).
    /// </summary>
    public static IReadOnlyList<(string ItemId, int Count)> Items(GameData data, ApiExtract extract)
    {
        var items = new List<(string, int)>();
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id) ?? "";
        var name = extract.Name ?? "";
        if (key.Contains("Alpinist", StringComparison.OrdinalIgnoreCase) || key.Contains("RedRebel", StringComparison.OrdinalIgnoreCase))
        {
            items.Add((IcePick, 1));
            items.Add((Paracord, 1));
        }
        if (name.Contains("(Flare)", StringComparison.OrdinalIgnoreCase) || key.Contains("sniper", StringComparison.OrdinalIgnoreCase))
            items.Add((RedFlare, 1));
        if (extract.TransferItem is { Count: > 0 } transfer)
            items.Add((transfer.Item, (int)Math.Ceiling(transfer.Count)));
        return items;
    }

    /// <summary>What a transit needs ("TerraGroup Labs access keycard required (1)"), or empty.</summary>
    public static string Needs(ApiTransit transit) => transit.Conditions?.Trim() ?? "";
}
