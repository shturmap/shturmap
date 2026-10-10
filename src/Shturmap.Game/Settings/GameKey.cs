namespace Shturmap.Game.Settings;

/// <summary>
/// A key or a combination of keys the game binds, kept as the Unity key codes its Control.ini writes, joined by "+"
/// ("LeftControl+Print"). It is named when shown (<see cref="Name"/>), in the language in use, so a switch of language
/// names it anew: until 2026-10-10 the names were made once, when the settings were read (docs/DESIGN.md §8, "Texts").
/// The app log and the CLI write the codes (<see cref="ToString"/>).
/// </summary>
public sealed record GameKey(string Codes)
{
    /// <summary>The Print Screen key: the game's screenshot key where its settings name none.</summary>
    public static GameKey PrintScreen { get; } = new("Print");

    /// <summary>As the keyboards of the language in use label it: "Ctrl+PrtSc", German "Strg+Druck".</summary>
    public string Name => string.Join("+", Codes.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(Named));

    /// <summary>The codes as the game writes them, for the log: "LeftControl+Print".</summary>
    public override string ToString() => Codes;

    // A Unity key code → what is printed on the key. A word is a text (GameTexts), so each language names the key as its
    // keyboards do (German: Druck, Pos1, Entf); a sign is the sign; a letter, a digit and F1 are themselves.
    private static string Named(string code) => code switch
    {
        "SysReq" or "Print" => GameTexts.ScreenshotKeyPrintScreen,
        "LeftControl" => GameTexts.ScreenshotKeyCtrl,
        "RightControl" => GameTexts.ScreenshotKeyRightCtrl,
        "LeftShift" => GameTexts.ScreenshotKeyShift,
        "RightShift" => GameTexts.ScreenshotKeyRightShift,
        "LeftAlt" => GameTexts.ScreenshotKeyAlt,
        "RightAlt" or "AltGr" => GameTexts.ScreenshotKeyAltGr,
        "Home" => GameTexts.ScreenshotKeyHome,
        "End" => GameTexts.ScreenshotKeyEnd,
        "Insert" => GameTexts.ScreenshotKeyInsert,
        "Delete" => GameTexts.ScreenshotKeyDelete,
        "PageUp" => GameTexts.ScreenshotKeyPageUp,
        "PageDown" => GameTexts.ScreenshotKeyPageDown,
        "Backspace" => GameTexts.ScreenshotKeyBackspace,
        "Return" => GameTexts.ScreenshotKeyEnter,
        "KeypadEnter" => GameTexts.ScreenshotKeyNumpadEnter,
        "Escape" => GameTexts.ScreenshotKeyEscape,
        "Tab" => GameTexts.ScreenshotKeyTab,
        "Space" => GameTexts.ScreenshotKeySpace,
        "Pause" => GameTexts.ScreenshotKeyPause,
        "CapsLock" => GameTexts.ScreenshotKeyCapsLock,
        "Numlock" => GameTexts.ScreenshotKeyNumLock,
        "ScrollLock" => GameTexts.ScreenshotKeyScrollLock,
        "UpArrow" => GameTexts.ScreenshotKeyUp,
        "DownArrow" => GameTexts.ScreenshotKeyDown,
        "LeftArrow" => GameTexts.ScreenshotKeyLeft,
        "RightArrow" => GameTexts.ScreenshotKeyRight,
        _ when code.StartsWith("Mouse", StringComparison.Ordinal) && code.Length > "Mouse".Length && char.IsDigit(code["Mouse".Length]) =>
            GameTexts.ScreenshotKeyMouse(button: code["Mouse".Length..]),
        _ when code.StartsWith("Alpha", StringComparison.Ordinal) => code["Alpha".Length..],
        _ when code.StartsWith("Keypad", StringComparison.Ordinal) => GameTexts.ScreenshotKeyNumpad(key: Sign(code["Keypad".Length..]) ?? code["Keypad".Length..]),
        _ => Sign(code) ?? code,
    };

    // The keys Unity names after the sign on them ("Minus"), and the number pad's ("KeypadPlus" is "Plus" here).
    private static string? Sign(string code) => code switch
    {
        "BackQuote" => "`",
        "Exclaim" => "!",
        "DoubleQuote" => "\"",
        "Hash" => "#",
        "Dollar" => "$",
        "Percent" => "%",
        "Ampersand" => "&",
        "Quote" => "'",
        "LeftParen" => "(",
        "RightParen" => ")",
        "Asterisk" or "Multiply" => "*",
        "Plus" => "+",
        "Comma" => ",",
        "Minus" => "-",
        "Period" => ".",
        "Slash" or "Divide" => "/",
        "Colon" => ":",
        "Semicolon" => ";",
        "Less" => "<",
        "Equals" => "=",
        "Greater" => ">",
        "Question" => "?",
        "At" => "@",
        "LeftBracket" => "[",
        "Backslash" => "\\",
        "RightBracket" => "]",
        "Caret" => "^",
        "Underscore" => "_",
        "LeftCurlyBracket" => "{",
        "Pipe" => "|",
        "RightCurlyBracket" => "}",
        "Tilde" => "~",
        _ => null,
    };
}
