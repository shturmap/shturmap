using Microsoft.UI.Xaml;
using Shturmap.Core;

namespace Shturmap.App.Controls;

/// <summary>
/// What a control says from its code (a tooltip, a screen reader's name), said again in the language in use whenever
/// it comes into the window and when the language changes while it is there: neither x:Bind nor the window's
/// Bindings.Update reaches it (docs/DESIGN.md §8, "The app's own language"; review of 2026-10-10: after a switch, a
/// quest card's pen still said "Picked for the coming raid").
/// </summary>
internal static class SaidAgain
{
    public static void OnLanguage(FrameworkElement element, Action say)
    {
        var listening = false;
        void Changed()
        {
            if (element.DispatcherQueue.HasThreadAccess)
                say();
            else
                element.DispatcherQueue.TryEnqueue(() => say());
        }
        element.Loaded += (_, _) =>
        {
            if (!listening)
            {
                UiLanguage.Changed += Changed;
                listening = true;
            }
            say();
        };
        element.Unloaded += (_, _) =>
        {
            if (listening)
            {
                UiLanguage.Changed -= Changed;
                listening = false;
            }
        };
    }
}
