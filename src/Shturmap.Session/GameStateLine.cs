using Shturmap.Game.Install;

namespace Shturmap.Session;

/// <summary>
/// The rail's line where the Plan card would be, while the game or its logs aren't found (owner, 2026-10-03: the
/// no-game fallback; docs/DESIGN.md, "No game on this PC"). It stays as long as that lasts, not as a notice that goes:
/// without the game's logs there are no quests to plan, so the line says why and what can be done instead.
/// </summary>
/// <param name="Title">The line itself, shown in capitals.</param>
/// <param name="Note">What follows from it, and what still works.</param>
/// <param name="OffersChoice">Whether "Choose game folder…" belongs under it.</param>
public sealed record GameStateLine(string Title, string Note, bool OffersChoice)
{
    /// <summary>The line for these game folders, or null when the game and its logs are found (or still looked for).</summary>
    /// <param name="canChoose">"Choose game folder…" can work (not with a fake game's folders).</param>
    public static GameStateLine? For(GameLocations? locations, bool canChoose)
    {
        if (locations is null)
            return null;
        if (locations.Install is null)
            return new("No game found on this PC",
                "Or browse the maps: pick one above, everything on it shows. Your quests and raids follow the game once " +
                "Shturmap finds it; it keeps looking while it runs.",
                canChoose);
        if (locations.LogsFolder is null)
            return new("The game hasn't run on this PC yet",
                $"Found Escape from Tarkov in {locations.Install.Root}. Your quests and raids follow it once it has run " +
                "here; until then, browse the maps above.",
                canChoose);
        return null;
    }
}
