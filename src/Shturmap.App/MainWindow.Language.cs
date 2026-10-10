using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Shturmap.Core;
using Shturmap.Session;

namespace Shturmap.App;

/// <summary>One choice in settings' "Language" row: Automatic, or a language in its own name.</summary>
/// <param name="Setting">What choosing it saves: <see cref="GameSession.AutomaticLanguage"/> or the language's code.</param>
/// <param name="Tip">Its tooltip, or null for none.</param>
public sealed record LanguageOption(string Setting, string Label, string? Tip, bool Chosen)
{
    public Visibility ChosenVisibility => Chosen ? Visibility.Visible : Visibility.Collapsed;

    public Brush LabelBrush => (Brush)Application.Current.Resources[Chosen ? "AmberBrush" : "MutedBrush"];
}

// The language Shturmap shows (owner, 2026-10-10; docs/DESIGN.md §8, "The app's own language"): settings' "Language"
// row, and the switch while Shturmap runs. A choice applies at once: the session chooses again and, when the game data's
// language changes, loads the data in it; Shturmap's own texts take the new language here, on the window's thread
// (UiLanguage.Set); the window says everything it shows again (OnUiLanguageChanged); then the session composes its
// snapshot again, so what the session words is in the new language too. What is on screen is then what a fresh start
// in that language shows (the layout check compares the two: docs/LANGUAGES.md). A notice already shown stays as said.
public sealed partial class MainWindow
{
    private void WatchLanguage()
    {
        UiLanguage.Changed += OnUiLanguageChanged;
        Closed += (_, _) => UiLanguage.Changed -= OnUiLanguageChanged;
    }

    // Settings' row from the snapshot: Automatic, then every language offered in its own name, and under it what
    // Automatic comes to and why.
    private void ShowLanguage(SessionSnapshot s)
    {
        var language = s.Language;
        var options = new List<LanguageOption>
        {
            new(GameSession.AutomaticLanguage, AppTexts.SettingsLanguageAutomatic, AppTexts.SettingsLanguageAutomaticTip,
                language.Setting == GameSession.AutomaticLanguage),
        };
        foreach (var code in UiLanguage.Offered)
        {
            var name = UiLanguage.NativeName(code).ToUpper(CultureInfo.GetCultureInfo(code));
            // A language in translation shows only in a developer build (UiLanguage.Offered), and says so.
            options.Add(UiLanguage.IsSupported(code)
                ? new(code, name, null, language.Setting == code)
                : new(code, AppTexts.SettingsLanguageInTranslation(language: name), AppTexts.SettingsLanguageInTranslationTip, language.Setting == code));
        }
        if (!Rules.RowLists.Same(ViewModel.LanguageOptions, options))
            ViewModel.LanguageOptions = options;
        var automatic = UiLanguage.NativeName(language.Automatic.Ui);
        ViewModel.LanguageNote = language.Automatic.Source switch
        {
            LanguageSource.Game => AppTexts.SettingsLanguageFromGame(language: automatic),
            LanguageSource.Windows => AppTexts.SettingsLanguageFromWindows(language: automatic),
            _ => AppTexts.SettingsLanguageNeither(language: automatic),
        };
    }

    private async void OnLanguageClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string setting } || setting == Volatile.Read(ref _snapshot)?.Language.Setting)
            return;
        Study.Ui("settings.language", ("setting", setting));
        await SwitchLanguageAsync(setting, save: true);
    }

    /// <summary>A choice of language, as settings make it: <paramref name="save"/> false keeps it for this run only.</summary>
    private async Task SwitchLanguageAsync(string setting, bool save)
    {
        var choice = await _session.SetLanguageAsync(setting, save);
        // Shturmap's own texts, on this thread: the window says what it shows again (OnUiLanguageChanged)...
        UiLanguage.Set(choice.Ui);
        // ...and the session what it composes, in a snapshot made again.
        await _session.RepublishAsync();
    }

    // Shturmap's texts changed language: everything the window shows is said again. Bindings.Update reads every x:Bind
    // again, the texts in XAML first; what code sets over them is set again from its state; the lists whose rows are
    // drawn from templates, and the open cards, are made anew; the snapshot is applied again. The session's own words
    // follow with its next snapshot (RepublishAsync).
    private void OnUiLanguageChanged()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(OnUiLanguageChanged);
            return;
        }
        AppLog.Info($"Shturmap's texts: {UiLanguage.Code}");
        Bindings.Update();
        // Help: the legend's rows, drawn in code, and its heading; the rest of the legend's state as the player left it.
        _legendRows = LegendRows();
        if (HelpFlyout.IsOpen)
        {
            var all = _legendAll;
            ShowLegend(opened: true);
            _legendAll = all;
            ShowLegendRest();
        }
#if DEVTOOLS
        // The study log's switch, built in code: built again (the old one's handler stays behind, on nothing shown).
        StudySwitch.Children.Clear();
        AddStudySwitch();
#endif
        // The tour and What's New read their words again; an open tour shows its chapter again.
        _tourChapters = null;
        if (TourOpen)
            ShowChapter(_tourAt);
        _whatsNewSections = null;
        if (_whatsNewChecked)
            SayWhatsNewHelp();
        if (ViewModel.WhatsNewShown)
        {
            var labels = _whatsNewShown.Select(w => w.Label).ToHashSet(StringComparer.Ordinal);
            ShowWhatsNew(WhatsNewSections.Where(w => labels.Contains(w.Label)).ToList(), "language");
        }
        // The report dialog's words that follow its state.
        SayShowSent();
        SayCancel();
        if (ReportOverlay.Visibility == Visibility.Visible)
            SetReportKind(_reportKind);
        // Settings: updates' line and note.
        if (_updater is { } updater)
        {
            ViewModel.UpdatesNote = Distribution.NoUpdatesText(App.BuildKind == "installed", updater.StartProblem is not null);
            RefreshUpdates();
        }
        // The question after a crash, while it waits for an answer (what it would send stays as it is).
        if (ViewModel.CrashAsking && _crashes.Count > 0)
        {
            var details = ViewModel.CrashDetails;
            AskAboutCrashes(_crashes);
            ViewModel.CrashDetails = details;
        }
        // Rows made from templates, whose words aren't in the rows' data: a list that says the same is kept on screen
        // (RowLists), so these start over and the snapshot fills them again.
        ForgetRows();
        if (Volatile.Read(ref _snapshot) is { } s)
            Apply(s);
        Map.Redraw();
    }

    private void ForgetRows()
    {
        var vm = ViewModel;
        vm.Plans = [];
        vm.AnyMap = [];
        vm.RaidPicks = [];
        vm.RaidComplete = [];
        vm.RaidProgress = [];
        vm.RaidKit = [];
        vm.RaidKitMore = [];
        vm.RaidBring = [];
        vm.Extracts = [];
        vm.RaidLineParts = [];
        vm.LanguageOptions = [];
        // Not a choice of map: the list starts over and the snapshot fills it again.
        _updatingPicker = true;
        try
        {
            vm.MapChoices = [];
        }
        finally
        {
            _updatingPicker = false;
        }
    }

#if DEVTOOLS
    /// <summary>
    /// Developer aid "--switch-language &lt;culture&gt;": once the window has the game data, the language switches as a
    /// choice in settings does (for this run only), and this returns once the data in the new language is on screen,
    /// before any snapshot is taken. The layout check compares such a run with a fresh one in that language
    /// (docs/LANGUAGES.md, "Layout check"): every text must be the same.
    /// </summary>
    public async Task SwitchLanguageWhenLoadedAsync(string culture)
    {
        if (UiLanguage.CodeOf(culture) is not { } code)
        {
            AppLog.Warn($"--switch-language {culture}: not a culture this Windows knows");
            return;
        }
        var until = DateTime.UtcNow.AddMinutes(1);
        while (Volatile.Read(ref _snapshot)?.Data is null && DateTime.UtcNow < until)
            await Task.Delay(100);
        // And shown: the snapshot with the data applied, and what it opens by itself (help in a snapshot run) open.
        await Task.Delay(1000);
        var before = Volatile.Read(ref _snapshot)?.Data;
        var dataLanguage = _session.Language.Data;
        AppLog.Info($"--switch-language {culture}: switching");
        await SwitchLanguageAsync(code, save: false);
        // The game data again in the new language, when that changed it (a load from the saved copy takes a moment).
        if (_session.Language.Data != dataLanguage)
        {
            while (ReferenceEquals(Volatile.Read(ref _snapshot)?.Data, before) && DateTime.UtcNow < until)
                await Task.Delay(100);
        }
        // The last snapshot applied.
        await Task.Delay(300);
        AppLog.Info($"--switch-language {culture}: done, {GameSession.Describe(_session.Language)}");
    }
#endif
}
