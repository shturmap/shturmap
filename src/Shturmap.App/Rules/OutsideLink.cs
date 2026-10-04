namespace Shturmap.App.Rules;

/// <summary>
/// The links that leave Shturmap (WIKI PAGE ↗ on a quest's card, WIKI MAP ↗ on the map) come out of tarkov.dev's data.
/// A link is offered only when it is an https address on the Escape from Tarkov wiki; anything else in that field is
/// no link at all, since a click would hand it to whatever program Windows has registered for its scheme (review of
/// 2026-10-04: the links were taken as given). If the wiki ever moves, the links go until the host here follows.
/// </summary>
public static class OutsideLink
{
    public const string WikiHost = "escapefromtarkov.fandom.com";

    /// <summary>The wiki address in <paramref name="link"/>, or null when it isn't one.</summary>
    public static Uri? Wiki(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.IdnHost, WikiHost, StringComparison.OrdinalIgnoreCase)
            ? uri
            : null;

    /// <summary>A map's interactive map on the wiki: its page plus "_Interactive_Map"; null when the page is no wiki address.</summary>
    public static Uri? WikiMap(string? page) =>
        string.IsNullOrEmpty(page) ? null : Wiki(page.TrimEnd('/') + "_Interactive_Map");
}
