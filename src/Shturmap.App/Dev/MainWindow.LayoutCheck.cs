#if DEVTOOLS
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using Shturmap.Session;

namespace Shturmap.App;

// The layout check (docs/LANGUAGES.md, "Layout check"; Dev/LayoutCheck.cs): each picture a snapshot saves of the window
// or one of its popups gets its texts and its problems in this language beside it. tools\layout-check.ps1 runs it for
// every state, language and window size.
public sealed partial class MainWindow
{
    private (GameData Data, HashSet<string> Names)? _layoutNames;

    // Called once a picture is saved (RenderToPngAsync), with what was rendered.
    private async Task CheckLayoutAsync(UIElement element, string path, RenderTargetBitmap rendered)
    {
        // The main window's own pictures only: the developer view is a developer's tool and isn't translated.
        if (element.XamlRoot is null || element.XamlRoot != Content.XamlRoot)
            return;
        try
        {
            var pixels = (await rendered.GetPixelsAsync()).ToArray();
            // "--culture qps-ploc" is the pseudo-language once it is the language in use; until then its culture says so.
            var pseudo = UiLanguage.Code == UiLanguage.Pseudo
                         || string.Equals(CultureInfo.CurrentUICulture.Name, UiLanguage.Pseudo, StringComparison.OrdinalIgnoreCase);
            var context = new Dev.LayoutCheck.Context(pseudo, UiLanguage.Code, CultureInfo.CurrentUICulture.Name, await LayoutNamesAsync(),
                (AppWindow.Size.Width, AppWindow.Size.Height), LayoutExpected(Path.GetFileNameWithoutExtension(path)));
            await Dev.LayoutCheck.RunAsync(element, path, pixels, rendered.PixelWidth, rendered.PixelHeight, context);
        }
        catch (Exception e)
        {
            AppLog.Warn("Layout check of " + Path.GetFileName(path) + " failed", e);
        }
        if (SnapshotMode && !_layoutScreens && ReferenceEquals(element, Content) && Path.GetFileName(path) == "window.png" && _deskChosen)
            await DeskScreensAsync(Path.GetDirectoryName(path) ?? ".");
    }

    // Taking the desk's page in screens: the screens' own pictures aren't taken in screens again.
    private bool _layoutScreens;

    /// <summary>
    /// The desk's page in screens (UI QA pass of step 2, F4: at 900×560 the desk's check passed with 95 texts below the
    /// first screen, the middle column, STASH and the windows, none of them in a picture): in a snapshot run (--snapshot)
    /// with the desk up, where its page runs past the desk's foot, the page is scrolled down screen by screen, each by the
    /// desk's room less the scroll mark's band (DeskPageRules.ScreenOffsets), and the window pictured and checked at each,
    /// "screen-2\window.png" and on beside window.png, until the page's end shows; then back to its top. tools\layout-check.ps1
    /// takes each as a view of its own ("desk-2", "desk-3" …), so every text of the page is in view in some picture.
    /// </summary>
    private async Task DeskScreensAsync(string folder)
    {
        var offsets = StashDesk.Screens();
        if (offsets.Count < 2)
            return;
        var was = StashDesk.ScrolledTo;
        _layoutScreens = true;
        AppLog.Info(FormattableString.Invariant($"Layout check: the desk's page in {offsets.Count} screens, window.png the first"));
        try
        {
            for (var n = 2; n <= offsets.Count; n++)
            {
                await StashDesk.ScrollToAsync(offsets[n - 1]);
                await Task.Delay(150);
                var screen = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"screen-{n}"));
                Directory.CreateDirectory(screen);
                await RenderToPngAsync((UIElement)Content, Path.Combine(screen, "window.png"));
                AppLog.Info(FormattableString.Invariant(
                    $"Layout check: the desk's page, screen {n} of {offsets.Count}, scrolled {StashDesk.ScrolledTo:0} DIP down, the scroll mark {(StashDesk.MoreBelowShown ? "shown" : "gone")}"));
            }
        }
        catch (Exception e)
        {
            AppLog.Warn("Layout check of the desk's screens failed", e);
        }
        finally
        {
            await StashDesk.ScrollToAsync(was);
            _layoutScreens = false;
        }
    }

    // What each picture always shows, as its texts file writes it, in the language in use (LayoutWords.Missing): texts
    // its XAML binds, which went missing all together when the window's bindings were never set (QA pass of 2026-10-15),
    // in every state the picture is taken in. The window: the rail's MAP and the status bar's help and settings buttons
    // (their words, LOGS and the rest, change with the state and give way in a narrow window); out of a raid PLAN | DESK
    // at the rail's head. The tour: its band's
    // ← BACK (from the second chapter), NEXT → (DONE at the last) and ESC ENDS THE TOUR. Help and settings: their titles.
    private IReadOnlyList<string> LayoutExpected(string picture) => picture switch
    {
        "window" =>
        [
            ViewTexts.RailMapHeading, "name: " + ViewTexts.HelpButtonName, "name: " + ViewTexts.SettingsButtonName,
            .. ViewModel.InRaid ? Array.Empty<string>() : [ViewTexts.DeskSwitchPlan, ViewTexts.DeskSwitchDesk],
        ],
        "tour" when TourOpen =>
        [
            .. _tourAt > 0 ? [ViewTexts.TourBack] : Array.Empty<string>(),
            _tourAt + 1 < TourChapters.Count ? ViewTexts.TourNext : AppTexts.TourDone,
            ViewTexts.TourEnd,
        ],
        "help" => [ViewTexts.HelpTitle],
        "settings" => [ViewTexts.SettingsTitle],
        _ => [],
    };

    // The synopsis phrases the tour's example raid card shows (MainWindow.Tour, ExampleRaidCard): not in the snapshot.
    private readonly HashSet<string> _layoutTourPhrases = new(StringComparer.Ordinal);

    // The names that stay as they are in every language: the loaded game data's, the releases' names and the languages'
    // own names (settings lists each in its own: ENGLISH, DEUTSCH); and what the rules that read English data made of
    // it (quest synopses: Plan's lines, the raid card's objectives, the tour's example), which the pseudo-language,
    // whose data is English, shows in English and every other language replaces with tarkov.dev's sentence
    // (docs/LANGUAGES.md, "Layout check").
    private async Task<IReadOnlySet<string>> LayoutNamesAsync()
    {
        var data = _snapshot?.Data;
        HashSet<string> names;
        if (_layoutNames is { } cached && ReferenceEquals(cached.Data, data))
            names = cached.Names;
        else
        {
            names = data is null ? new HashSet<string>(StringComparer.Ordinal) : await Task.Run(() => LayoutWords.NamesIn(data));
            foreach (var section in WhatsNewSections)
            {
                if (section.Name is { } name)
                    names.Add(LayoutWords.Key(name));
            }
            foreach (var code in UiLanguage.Offered.Append("en"))
                names.Add(LayoutWords.Key(UiLanguage.NativeName(code)));
            if (data is not null)
                _layoutNames = (data, names);
        }
        var phrases = new HashSet<string>(names, StringComparer.Ordinal);
        if (_snapshot is { } s)
        {
            var rows = s.Plan.Append(s.MapPlan).OfType<Shturmap.Session.MapPlanView>().SelectMany(p => p.Finish.Concat(p.Progress)).Concat(s.AnyMap);
            foreach (var phrase in rows.SelectMany(r => r.Synopsis.Split(" · ")).Concat(s.Objectives.Select(o => o.Short)).Concat(_layoutTourPhrases))
            {
                if (!string.IsNullOrWhiteSpace(phrase))
                    phrases.Add(LayoutWords.Key(phrase));
            }
        }
        // The stash desk's window titles, a container's short name as the game prints it ("WZ"): the game's word, as a
        // name is (release QA pass of 2026-10-19, F6: a window not attached stands under its title alone).
        foreach (var region in StashDesk.View?.Sheet.Regions ?? [])
        {
            if (region.Region.Title is { Length: > 0 } title)
                phrases.Add(LayoutWords.Key(title));
        }
        return phrases;
    }
}
#endif
