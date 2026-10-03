#if DEVTOOLS
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core.Logs;
using Shturmap.Session;
using Shturmap.Session.Dev;

namespace Shturmap.App.Dev;

/// <summary>
/// The developer view (owner, 2026-10-03; docs/DESIGN.md §8, "Developer aids"): plays a fake raid against the app,
/// with positions on demand, and says what this build holds. Developer builds only: a release never has it. Plain on
/// purpose; it doesn't follow the product's visual rules.
/// </summary>
internal sealed partial class DevView : Window
{
    private readonly DevController _dev;
    private readonly StackPanel _play = new() { Spacing = 10, Padding = new Thickness(14) };
    private readonly StackPanel _build = new() { Spacing = 8, Padding = new Thickness(14) };
    private readonly ScrollViewer _playScroll = new();
    private readonly ScrollViewer _buildScroll = new();
    private readonly ToggleButton _playTab = new() { Content = "PLAY", IsChecked = true };
    private readonly ToggleButton _buildTab = new() { Content = "WHAT'S IN THIS BUILD" };
    private readonly ComboBox _maps = new() { MinWidth = 220, PlaceholderText = "map" };
    private readonly ToggleSwitch _side = new() { OnContent = "Scav", OffContent = "PMC", Header = "Side" };
    private readonly ToggleSwitch _hosting = new() { OnContent = "Local", OffContent = "Server", Header = "Hosted" };
    private readonly ToggleSwitch _pick = new() { OnContent = "Click on the map takes a screenshot there (drag: facing)", OffContent = "Map clicks as usual", Header = "Pick on the map" };
    private readonly ToggleSwitch _record = new() { OnContent = "Clicks add path points", OffContent = "Off", Header = "Record a path" };
    private bool _syncing;
    private readonly ComboBox _transitTo = new() { MinWidth = 160, PlaceholderText = "transit to" };
    private readonly TextBox _questFilter = new() { PlaceholderText = "find a quest (name or id)", MinWidth = 300 };
    private readonly ListView _quests = new() { Height = 220, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _position = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _height = new();
    private readonly TextBlock _path = new();
    private readonly TextBlock _log = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11 };
    private readonly TextBox _changeFilter = new() { PlaceholderText = "filter: is a change in this build?" };
    private readonly StackPanel _changes = new() { Spacing = 2 };
    private IReadOnlyList<DevMap> _mapList = [];
    private IReadOnlyList<(string Id, string Name, string Trader)> _questList = [];
    private readonly DevChangelog? _changelog = DevChangelog.Read(AppContext.BaseDirectory);

    public DevView(DevController dev)
    {
        _dev = dev;
        Title = "Shturmap · developer view";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(520, 980));
        AppWindow.SetIcon(App.IconPath);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Padding = new Thickness(14, 10, 14, 0) };
        tabs.Children.Add(_playTab);
        tabs.Children.Add(_buildTab);
        _playTab.Click += (_, _) => ShowTab(play: true);
        _buildTab.Click += (_, _) => ShowTab(play: false);
        _playScroll.Content = _play;
        _buildScroll.Content = _build;
        _buildScroll.Visibility = Visibility.Collapsed;

        var root = new Grid { Background = (Brush)Application.Current.Resources["RailBrush"] };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(tabs);
        Grid.SetRow(_playScroll, 1);
        Grid.SetRow(_buildScroll, 1);
        root.Children.Add(_playScroll);
        root.Children.Add(_buildScroll);
        Content = root;

        BuildPlay();
        BuildChangelog();
        dev.Done += _ => DispatcherQueue.TryEnqueue(Refresh);
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += (_, _) => Refresh();
        timer.Start();
        Closed += (_, _) => timer.Stop();
        Refresh();
    }

    private void ShowTab(bool play)
    {
        _playTab.IsChecked = play;
        _buildTab.IsChecked = !play;
        _playScroll.Visibility = play ? Visibility.Visible : Visibility.Collapsed;
        _buildScroll.Visibility = play ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- PLAY ----

    private void BuildPlay()
    {
        if (_dev.Game is null)
        {
            _play.Children.Add(Heading("NOT A DEVELOPER SESSION"));
            _play.Children.Add(Note("This session reads the real game. The developer view plays a fake game of its own: restart in it."));
            _play.Children.Add(ButtonFor("Restart in the developer view", RestartInDevView));
            return;
        }
        _play.Children.Add(Heading("FAKE GAME"));
        var folder = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        folder.Children.Add(Note(_dev.Game.Root));
        folder.Children.Add(ButtonFor("Open", () => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_dev.Game.Root}\"") { UseShellExecute = true })));
        _play.Children.Add(folder);

        _play.Children.Add(Heading("MODE"));
        _play.Children.Add(Row(ButtonFor("PvE", () => _dev.SetMode(GameMode.Pve)), ButtonFor("PvP", () => _dev.SetMode(GameMode.Pvp)),
            ButtonFor("Seasonal", () => _dev.SetMode(GameMode.Seasonal))));

        _play.Children.Add(Heading("RAID"));
        _maps.SelectionChanged += async (_, _) =>
        {
            if (!_syncing && _maps.SelectedIndex >= 0 && _maps.SelectedIndex < _mapList.Count)
                await _dev.SelectMapAsync(_mapList[_maps.SelectedIndex]);
        };
        _side.Toggled += (_, _) => { if (!_syncing) _dev.Scav = _side.IsOn; };
        _hosting.Toggled += (_, _) => { if (!_syncing) _dev.Local = _hosting.IsOn; };
        _play.Children.Add(Row(_maps));
        _play.Children.Add(Row(_side, _hosting));
        _play.Children.Add(Row(ButtonFor("Group pick", _dev.GroupPick), ButtonFor("Load", _dev.Load), ButtonFor("Steps", _dev.Steps),
            ButtonFor("Start", _dev.Start)));
        _play.Children.Add(Row(ButtonFor("Load + steps + start", () => _ = _dev.RaidAsync()), ButtonFor("End", _dev.End), ButtonFor("Cancel", _dev.Cancel)));
        _play.Children.Add(Row(_transitTo, ButtonFor("Transit", () =>
        {
            if (_transitTo.SelectedIndex >= 0 && _transitTo.SelectedIndex < _mapList.Count)
                _ = _dev.TransitAsync(_mapList[_transitTo.SelectedIndex]);
        })));

        _play.Children.Add(Heading("POSITION"));
        _pick.Toggled += (_, _) => { if (!_syncing) _dev.Picking = _pick.IsOn; };
        _record.Toggled += (_, _) => { if (!_syncing) _dev.RecordingPath = _record.IsOn; };
        _play.Children.Add(_pick);
        _play.Children.Add(_position);
        _play.Children.Add(Row(Note("Height:"), _height, ButtonFor("−1 m", () => Nudge(-1)), ButtonFor("+1 m", () => Nudge(1)), ButtonFor("+3 m", () => Nudge(3))));
        _play.Children.Add(Row(ButtonFor("Repeat (F9)", _dev.Repeat), ButtonFor("Age +1 min", () => _ = _dev.AgeAsync(1)),
            ButtonFor("Age +5 min", () => _ = _dev.AgeAsync(5))));
        _play.Children.Add(_record);
        _play.Children.Add(Row(_path, ButtonFor("Walk (3 s apart)", () => _ = _dev.WalkAsync(3)), ButtonFor("Clear path", () =>
        {
            _dev.Path.Clear();
            Refresh();
        })));

        _play.Children.Add(Heading("QUESTS"));
        _questFilter.TextChanged += (_, _) => FillQuests();
        _play.Children.Add(_questFilter);
        _play.Children.Add(_quests);
        _play.Children.Add(Row(ButtonFor("Start", () => QuestAction(QuestLogStatus.Started)), ButtonFor("Complete", () => QuestAction(QuestLogStatus.Completed)),
            ButtonFor("Fail", () => QuestAction(QuestLogStatus.Failed)), ButtonFor("Start 8 on this map", () => _dev.StartSomeHere())));
        _play.Children.Add(Note("The game's logs say only when a quest starts, fails or completes, not single objectives, so neither can this."));

        _play.Children.Add(Heading("TRIGGERS"));
        _play.Children.Add(Row(Trigger("Report dialog", "report"), Trigger("Crash question", "crash"), Trigger("Update ready", "update")));
        _play.Children.Add(Row(Trigger("Data: offline", "offline"), Trigger("Data: 404", "404"), Trigger("Data: 503", "503"), Trigger("Reload data", "reload")));
        _play.Children.Add(Row(Trigger("No game", "nogame")));

        _play.Children.Add(Heading("DONE"));
        _play.Children.Add(_log);
    }

    private void Nudge(double metres)
    {
        _dev.HeightOffset += metres;
        Refresh();
    }

    private Button Trigger(string label, string what) => ButtonFor(label, () => _ = _dev.TriggerAsync(what));

    private void QuestAction(QuestLogStatus status)
    {
        if (_quests.SelectedIndex >= 0 && _quests.SelectedIndex < _questList.Count)
            _dev.Quest(_questList[_quests.SelectedIndex].Id, status);
    }

    private void FillQuests()
    {
        _questList = _dev.QuestsMatching(_questFilter.Text);
        _quests.ItemsSource = _questList.Select(q => q.Trader.Length > 0 ? $"{q.Name}  ·  {q.Trader}" : q.Name).ToList();
    }

    private void Refresh()
    {
        if (_dev.Game is null)
            return;
        if (_mapList.Count == 0 && _dev.Maps() is { Count: > 0 } maps)
        {
            _mapList = maps;
            var names = maps.Select(m => m.Name).ToList();
            _maps.ItemsSource = names;
            _transitTo.ItemsSource = names.ToList();
            FillQuests();
        }
        // The controls follow what a script (or F9) did.
        _syncing = true;
        var shown = _dev.Map is { } map ? _mapList.ToList().FindIndex(m => m.NormalizedName == map.NormalizedName) : -1;
        if (_maps.SelectedIndex != shown)
            _maps.SelectedIndex = shown;
        _side.IsOn = _dev.Scav;
        _hosting.IsOn = _dev.Local;
        _pick.IsOn = _dev.Picking;
        _record.IsOn = _dev.RecordingPath;
        _syncing = false;
        _position.Text = "Last position: " + _dev.LastPositionText();
        _height.Text = _dev.HeightOffset == 0 ? "from the shown floor" : string.Format(CultureInfo.InvariantCulture, "floor {0:+0;-0} m", _dev.HeightOffset);
        _path.Text = _dev.Path.Count == 0 ? "No path" : $"Path: {_dev.Path.Count} places";
        _log.Text = string.Join(Environment.NewLine, _dev.History.TakeLast(10).Reverse());
    }

    private void RestartInDevView()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--dev-view") { UseShellExecute = false });
        Application.Current.Exit();
    }

    // ---- WHAT'S IN THIS BUILD ----

    private void BuildChangelog()
    {
        _build.Children.Add(Heading("WHAT'S IN THIS BUILD"));
        var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (_changelog is null)
        {
            _build.Children.Add(Note("No changelog in this build (eng\\dev.ps1 writes one)."));
            _build.Children.Add(Note($"Shturmap {version ?? GameSession.Version} · commit {DevChangelog.CommitOf(version) ?? "unknown"}"));
            return;
        }
        var built = _changelog.Built is { } at ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "unknown time";
        var hash = _changelog.Build is { Length: >= 7 } b ? b[..7] : _changelog.Build ?? DevChangelog.CommitOf(version) ?? "unknown";
        _build.Children.Add(new TextBlock { Text = $"This build: {hash} · built {built}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _build.Children.Add(Note($"{_changelog.Commits.Count} commits, newest first"));
        _changeFilter.TextChanged += (_, _) => FillChanges();
        _build.Children.Add(_changeFilter);
        _build.Children.Add(_changes);
        FillChanges();
    }

    private void FillChanges()
    {
        if (_changelog is null)
            return;
        _changes.Children.Clear();
        var shown = _changelog.Matching(_changeFilter.Text);
        if (shown.Count == 0)
            _changes.Children.Add(Note("Not in this build."));
        foreach (var c in shown)
        {
            var line = new Grid { ColumnSpacing = 8 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var date = new TextBlock { Text = c.Date?.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "", Opacity = 0.7, FontSize = 12 };
            var hash = new TextBlock { Text = c.Hash, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12, Opacity = 0.8 };
            var subject = new TextBlock { Text = c.Subject, TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };
            Grid.SetColumn(hash, 1);
            Grid.SetColumn(subject, 2);
            line.Children.Add(date);
            line.Children.Add(hash);
            line.Children.Add(subject);
            _changes.Children.Add(line);
        }
    }

    /// <summary>Both tabs as PNGs beside the window's (a script's "snapshot"): devview-play.png, devview-build.png.</summary>
    public async Task SaveSnapshotsAsync(string folder)
    {
        var mainWindow = _dev.Window;
        var playShown = _playScroll.Visibility == Visibility.Visible;
        ShowTab(play: false);
        await Task.Delay(300);
        await mainWindow.DevRenderAsync((UIElement)Content, Path.Combine(folder, "devview-build.png"));
        ShowTab(play: true);
        await Task.Delay(300);
        await mainWindow.DevRenderAsync((UIElement)Content, Path.Combine(folder, "devview-play.png"));
        ShowTab(playShown);
    }

    // ---- plain building blocks ----

    private static TextBlock Heading(string text) => new()
    {
        Text = text, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 12, CharacterSpacing = 120, Margin = new Thickness(0, 8, 0, 0),
        Foreground = (Brush)Application.Current.Resources["AmberBrush"],
    };

    private static TextBlock Note(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center };

    private static Button ButtonFor(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 4, 10, 5) };
        button.Click += (_, _) => action();
        return button;
    }

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var child in children)
            row.Children.Add(child);
        return row;
    }
}
#endif
