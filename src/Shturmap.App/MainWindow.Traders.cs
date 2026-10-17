using Microsoft.UI.Xaml;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Session;

namespace Shturmap.App;

// The trader strip under the MAP list (owner, 2026-10-17: "some players want to level a specific trader or want to
// work towards specific unlocks of these traders ... you can only select by map and not by trader. I at the same time
// want to keep the UI footprint lean"; from a panel of three, "B"; docs/DESIGN.md §4, "Screen anatomy", *The trader
// strip*). A portrait per trader with active quests and their count; pointing at one lights that trader's quests, a
// click holds the focus. The focus is this run's: never saved, kept until let go (Rules.TraderFocus).
public sealed partial class MainWindow
{
    // The trader whose focus is held, or null.
    private string? _heldTrader;

    /// <summary>"--trader-focus &lt;name&gt;" (snapshots): holds that trader's focus once the data is there, as a click
    /// on the portrait does (by the trader's name in the data's language or tarkov.dev's normalized name, "ragman").</summary>
    public string? TraderFocusOnStart { get; set; }

    // Each active quest and the trader who gave it, as the data names them.
    private static IEnumerable<(string QuestId, string? TraderId)> ActiveByTrader(SessionSnapshot s) =>
        s.Data is not { } data ? []
            : s.Quests.Values.Where(q => q.State == QuestState.Active).Select(q => (q.QuestId, data.Tasks.GetValueOrDefault(q.QuestId)?.Trader));

    // The trader who gave a quest, or null.
    private static string? TraderOf(SessionSnapshot s, string questId) => s.Data?.Tasks.GetValueOrDefault(questId)?.Trader;

    // The held trader's name, for the maps' counts ("Ragman 2"); null without a focus.
    private string? HeldTraderName(SessionSnapshot s) =>
        _heldTrader is not { } trader ? null : s.Data?.TraderName(trader) is { Length: > 0 } name ? name : trader;

    // How many of the held trader's quests a map's row holds, to complete or to progress.
    private int HeldCountOn(MapPlanView plan) =>
        _heldTrader is { } trader ? PlanList.ByTrader(plan.Finish.Concat(plan.Progress).Select(q => q.TraderId)).GetValueOrDefault(trader) : 0;

    // What the map draws as the held focus: the trader's active quests (in a raid too).
    private IReadOnlySet<string> HeldOnMap(SessionSnapshot s) => TraderFocus.OnMap(_heldTrader, ActiveByTrader(s));

    // The strip: every trader with active quests between raids; in a raid (or while one loads) only the held one.
    private void UpdateTraders(SessionSnapshot s)
    {
        if (s.Data is not { } data)
        {
            ViewModel.Traders = [];
            return;
        }
        var strip = TraderFocus.Strip(ActiveByTrader(s), data.Traders.Keys.ToList(), _heldTrader);
        var chips = TraderFocus.Shown(strip, _heldTrader, s.Raid.Phase != RaidPhase.Menu)
            .Select(t => new TraderChip(t.TraderId, t.Count.ToString(UiLanguage.Culture), t.Quests,
                TraderFocus.Tip(data.TraderName(t.TraderId) is { Length: > 0 } name ? name : t.TraderId, t.Count, t.TraderId == _heldTrader),
                t.TraderId == _heldTrader))
            .ToList();
        ViewModel.Traders = RowLists.Keep(ViewModel.Traders, chips, TraderChip.Same);
    }

    // A click on a portrait holds its trader's focus, or lets go of it on the held one; one trader at a time.
    private void OnTraderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string trader })
            HoldTrader(TraderFocus.Click(_heldTrader, trader), "click");
    }

    // Esc: the cards first, as before; with none open, the trader's held focus.
    private void OnEscape()
    {
        var (held, closeCards) = TraderFocus.Escape(_heldTrader, _cards.Cards.Count > 0);
        if (closeCards)
            _cards.CloseAll();
        else if (held != _heldTrader)
            HoldTrader(held, "esc");
    }

    /// <summary>Holds a trader's focus (null lets go): the strip, the maps' counts, the rows and the map follow at once.</summary>
    private void HoldTrader(string? trader, string how)
    {
        if (trader == _heldTrader)
            return;
        var before = _heldTrader;
        _heldTrader = trader;
        Study.Ui(trader is null ? "trader.release" : "trader.hold", ("trader", trader ?? before), ("how", how));
        if (Volatile.Read(ref _snapshot) is not { } s)
            return;
        UpdateTraders(s);
        UpdatePicker(s);
        UpdatePlan(s);
        // The map on screen, or a map row's preview, takes the focus as it is now; What's New's and the tour's examples
        // aren't the player's and keep none.
        if (Map.Scene is { } scene && (_previewing is null ? _scenes.Holds(s.Definition?.Key) : !_previewing.StartsWith(WhatsNewPreviewPrefix, StringComparison.Ordinal)))
        {
            scene.Held = HeldOnMap(s);
            Map.Redraw();
        }
    }

    // "--trader-focus <name>": the focus held once the data names the trader.
    private void HoldTraderOnStart(SessionSnapshot s)
    {
        if (TraderFocusOnStart is not { } text || s.Data is not { } data)
            return;
        TraderFocusOnStart = null;
        if (TraderNamed(data, text) is not { } trader)
        {
            AppLog.Warn($"--trader-focus {text}: no trader by that name");
            return;
        }
        _heldTrader = trader.Id;
        AppLog.Info($"Trader focus held: {trader.Name}");
    }

    // A trader by their name in the data's language, tarkov.dev's normalized name ("ragman") or their id.
    private static ApiTrader? TraderNamed(GameData data, string name)
    {
        var text = name.Trim();
        var normalized = text.ToLowerInvariant().Replace(' ', '-');
        return data.Traders.Values.FirstOrDefault(t => t.Name.Equals(text, StringComparison.OrdinalIgnoreCase) || t.NormalizedName == normalized || t.Id == text);
    }
}
