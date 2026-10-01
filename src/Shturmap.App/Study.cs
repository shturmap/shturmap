using Shturmap.Session;

namespace Shturmap.App;

/// <summary>
/// What the player does in Shturmap, for the study log (docs/DESIGN.md §8). Events say what happened, not every
/// pointer move: a pan is one event when the drag ends, a zoom one event when the wheel stops.
/// </summary>
internal static class Study
{
    public static StudyLog? Log { get; set; }

    public static void Ui(string name, params (string Key, object? Value)[] data) => Log?.Ui(name, data);
}
