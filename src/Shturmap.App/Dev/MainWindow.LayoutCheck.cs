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
                (AppWindow.Size.Width, AppWindow.Size.Height));
            await Dev.LayoutCheck.RunAsync(element, path, pixels, rendered.PixelWidth, rendered.PixelHeight, context);
        }
        catch (Exception e)
        {
            AppLog.Warn("Layout check of " + Path.GetFileName(path) + " failed", e);
        }
    }

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
        return phrases;
    }
}
#endif
