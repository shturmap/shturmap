using Shturmap.Core;
using Shturmap.Core.Text;
using Shturmap.Game.Settings;

namespace Shturmap.Game.Tests;

/// <summary>Tests that change the language in use (a single value for the whole app) run alone, after the others.</summary>
[CollectionDefinition("UiLanguage", DisableParallelization = true)]
public sealed class UiLanguageCollection;

// The game's screenshot keys are kept as the codes its Control.ini writes and named when shown, each word a text, so a
// language names them as its keyboards do (German: Druck, Pos1, Entf) and a switch of language names them anew. Until
// 2026-10-10 eight were texts, made once when the settings were read; the rest showed Unity's English names.
[Collection("UiLanguage")]
public class GameKeyTests
{
    // Control.ini's binding of the screenshot key, one variant per argument: "\"LeftControl\", \"Print\"".
    private static IReadOnlyList<GameKey> Keys(params string[] variants) => GameSettingsReader.ScreenshotKeys(
        """{ "keyBindings": [ { "keyName": "MakeScreenshot", "variants": [ """ +
        string.Join(", ", variants.Select(v => "{ \"keyCode\": [" + v + "] }")) + " ] } ] }");

    [Fact]
    public void Keys_are_named_as_keyboards_label_them_and_logged_as_the_game_writes_them()
    {
        var keys = Keys("\"LeftControl\", \"Print\"", "\"Home\"", "\"KeypadPlus\"", "\"Minus\"", "\"Alpha5\"", "\"F12\"", "\"Mouse3\"", "\"PageUp\"");
        Assert.Equal(["Ctrl+PrtSc", "Home", "Num +", "-", "5", "F12", "Mouse3", "PageUp"], keys.Select(k => k.Name));
        Assert.Equal("LeftControl+Print or Home", string.Join(" or ", keys.Take(2)));
        Assert.Equal("PrtSc", GameKey.PrintScreen.Name);
    }

    [Fact]
    public void Every_word_on_a_key_is_a_text_named_in_the_language_in_use_when_shown()
    {
        string[] words = ["Print", "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt", "Home", "End", "Insert",
            "Delete", "PageUp", "PageDown", "Backspace", "Return", "KeypadEnter", "Escape", "Tab", "Space", "Pause", "CapsLock", "Numlock",
            "ScrollLock", "UpArrow", "DownArrow", "LeftArrow", "RightArrow", "Mouse4", "Keypad7"];
        var keys = words.Select(w => new GameKey(w)).ToList();
        try
        {
            UiLanguage.Set(UiLanguage.Pseudo);
            Assert.All(keys, k => Assert.True(PseudoText.IsPseudo(k.Name), $"{k}: {k.Name}"));
        }
        finally
        {
            UiLanguage.Set(UiLanguage.English);
        }
        Assert.All(keys, k => Assert.False(PseudoText.IsPseudo(k.Name), k.Name));
    }
}
