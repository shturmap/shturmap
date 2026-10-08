using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shturmap.App.Rules;

namespace Shturmap.App.Controls;

/// <summary>
/// The MAP list's items (Rules.MapList): a map's item stretches, so its count stands at the right; the OTHER MAPS
/// heading's item can't be chosen, by pointer or keys.
/// </summary>
public sealed partial class MapChoiceStyles : StyleSelector
{
    public Style? Map { get; set; }

    public Style? Header { get; set; }

    protected override Style? SelectStyleCore(object item, DependencyObject container) =>
        item is MapChoice { IsHeader: true } ? Header : Map;
}
