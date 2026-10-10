using Shturmap.Core;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session;

/// <summary>
/// What an extract or transit needs, in a few words: "Pay 5,000 ₽", "Red Rebel ice pick and paracord, no armored
/// rig", "ZB-013 Power Switch first". From tarkov.dev's fields (transfer items, switches, transit conditions) and,
/// where no field says it, from the extract's internal name (the climbing exits, flare exits) and its English name
/// (flare and co-op exits). Never from the name in the game's language: German has "Mira-Allee" for "Mira Ave
/// (Flare)", so the rule was lost there (review of 2026-10-04).
/// </summary>
public static class ExtractRules
{
    private const string Roubles = "5449016a4bdc2d6f028b456f";
    private const string Dollars = "5696686a4bdc2da3298b456a";
    private const string Euros = "569668774bdc2da2298b4568";

    // The climbing exits, by the game's internal name ("Alpinist", "Alpinist_light", "RedRebel_alp").
    private static bool Climb(string key) =>
        key.Contains("Alpinist", StringComparison.OrdinalIgnoreCase) || key.Contains("RedRebel", StringComparison.OrdinalIgnoreCase);

    // A flare exit: "(Flare)" in its English name, or "sniper" as a word of its own in the internal name
    // ("E9_sniper", "customs_sniper_exit"). Not "sniper" anywhere in it: Customs' "Sniper Roadblock" is an ordinary
    // exit, and was told to fire a flare.
    private static bool Flare(string key, string english) =>
        english.Contains("(Flare)", StringComparison.OrdinalIgnoreCase)
        || key.Split('_').Contains("sniper", StringComparer.OrdinalIgnoreCase);

    private static bool CoOp(string english) => english.Contains("(Co-op)", StringComparison.OrdinalIgnoreCase);

    /// <returns>The requirement text (empty if none) and an item to picture, if one has to be handed over.</returns>
    public static (string Text, string? ItemId) Needs(GameData data, ApiMap map, ApiExtract extract)
    {
        var parts = new List<string>();
        string? item = null;
        var key = data.ExtractKeys.GetValueOrDefault(extract.Id) ?? "";
        var english = data.EnglishName(extract.Id, extract.Name);

        if (Climb(key))
            parts.Add(SessionTexts.ExtractClimb);
        if (Flare(key, english))
            parts.Add(SessionTexts.ExtractFlare);
        if (CoOp(english))
            parts.Add(SessionTexts.ExtractCoOp);

        if (extract.TransferItem is { Count: > 0 } transfer)
        {
            var amount = transfer.Count.ToString("N0", UiLanguage.Culture);
            switch (transfer.Item)
            {
                case Roubles:
                    parts.Add(SessionTexts.ExtractPayRoubles(amount: amount));
                    break;
                case Dollars:
                    parts.Add(SessionTexts.ExtractPayDollars(amount: amount));
                    break;
                case Euros:
                    parts.Add(SessionTexts.ExtractPayEuros(amount: amount));
                    break;
                default:
                    var name = data.ItemName(transfer.Item);
                    parts.Add(SessionTexts.ExtractHandOver(item: transfer.Count > 1 ? SessionTexts.ItemTimes(count: amount, item: name) : name));
                    item = transfer.Item;
                    break;
            }
        }

        // tarkov.dev lists some switches on every extract of a map (all of Customs points at the ZB-013 power
        // switch). A switch listed by most extracts is only shown where its name says it belongs: by the English
        // names of both, so that a translation that words them apart doesn't lose it.
        var extracts = map.Extracts ?? [];
        foreach (var id in extract.Switches ?? [])
        {
            if (map.Switches?.FirstOrDefault(s => s.Id == id)?.Name is not { Length: > 0 } switchName)
                continue;
            var common = extracts.Count(e => e.Switches?.Contains(id) == true) * 2 > extracts.Count;
            if (!common || (english.Length > 0 && data.EnglishName(id, switchName).Contains(english, StringComparison.OrdinalIgnoreCase)))
                parts.Add(SessionTexts.ExtractSwitchFirst(switchName: switchName));
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
        if (Climb(key))
        {
            items.Add((IcePick, 1));
            items.Add((Paracord, 1));
        }
        if (Flare(key, data.EnglishName(extract.Id, extract.Name)))
            items.Add((RedFlare, 1));
        if (extract.TransferItem is { Count: > 0 } transfer)
            items.Add((transfer.Item, (int)Math.Ceiling(transfer.Count)));
        return items;
    }

    /// <summary>What a transit needs ("TerraGroup Labs access keycard required (1)"), or empty.</summary>
    public static string Needs(ApiTransit transit) => transit.Conditions?.Trim() ?? "";
}
