namespace Spotter.Data.TarkovDev;

/// <summary>Where items can be had: trader offers, barters, hideout crafts, the flea market (json.tarkov.dev).</summary>
public sealed class ItemSources
{
    public required IReadOnlyDictionary<string, ApiItem> Items { get; init; }

    /// <summary>Barters by the item they give.</summary>
    public required ILookup<string, ApiBarter> Barters { get; init; }

    /// <summary>Crafts by the item they make.</summary>
    public required ILookup<string, ApiCraft> Crafts { get; init; }

    /// <summary>Hideout station names by id, in the game's language.</summary>
    public required IReadOnlyDictionary<string, string> Stations { get; init; }
}
