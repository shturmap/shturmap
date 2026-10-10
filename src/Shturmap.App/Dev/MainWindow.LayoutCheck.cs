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

    // The names that stay as they are in every language: the loaded game data's, and the releases' names.
    private async Task<IReadOnlySet<string>> LayoutNamesAsync()
    {
        var data = _snapshot?.Data;
        if (_layoutNames is { } cached && ReferenceEquals(cached.Data, data))
            return cached.Names;
        var names = data is null ? new HashSet<string>(StringComparer.Ordinal) : await Task.Run(() => LayoutWords.NamesIn(data));
        foreach (var section in WhatsNewSections)
        {
            if (section.Name is { } name)
                names.Add(LayoutWords.Key(name));
        }
        if (data is not null)
            _layoutNames = (data, names);
        return names;
    }
}
#endif
