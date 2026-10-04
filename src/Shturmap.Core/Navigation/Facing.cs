namespace Shturmap.Core.Navigation;

/// <summary>The way the player faced when a screenshot was taken.</summary>
public static class Facing
{
    /// <summary>
    /// How long the facing a screenshot recorded is taken for the way the player still looks. For this long the map
    /// draws the facing cone and the cards say directions relative to it ("ahead-left"); past it the cone goes and
    /// the directions become map directions ("NE"), which stay true while the player turns. One duration for both
    /// (the review of 2026-10-04: the cone showed for 60 s, the directions for 45 s). 45 s, the shorter: the facing is
    /// only true for a moment, and a cone still pointing "ahead" after the cards stopped saying so would mislead.
    /// </summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromSeconds(45);
}
