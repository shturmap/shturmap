namespace Shturmap.Data.TarkovDev;

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

    private ILookup<string, string>? _members;

    /// <summary>
    /// Item ids by category id; an item is listed under every category in its chain (<see cref="ApiItem.Categories"/>).
    /// Presets (a weapon in a ready build, tarkov.dev type "preset") are left out: they repeat the weapon they build.
    /// </summary>
    public ILookup<string, string> Members =>
        _members ??= Items.Values.Where(i => i.Types?.Contains("preset") != true)
            .SelectMany(i => (i.Categories ?? []).Select(c => (Category: c, i.Id))).ToLookup(x => x.Category, x => x.Id);
}
