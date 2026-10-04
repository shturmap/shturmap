namespace Shturmap.App.Rules;

/// <summary>
/// The keys that zoom the map, as Windows virtual-key codes with whether Shift is held. "+" is a key of its own on
/// some keyboards (German: the key right of Ü) and Shift with the "=" key on others (US); both are the same
/// virtual key, 0xBB, and a shortcut without Shift never saw the US one (review of 2026-10-04, A42: "+" didn't zoom
/// on a US keyboard). The number pad's keys need no Shift anywhere.
/// </summary>
public static class ZoomKeys
{
    public const int NumPadPlus = 0x6B;
    public const int NumPadMinus = 0x6D;

    /// <summary>The "+" key, or the "=" key that gives "+" with Shift (VK_OEM_PLUS).</summary>
    public const int Plus = 0xBB;

    /// <summary>The "-" key (VK_OEM_MINUS).</summary>
    public const int Minus = 0xBD;

    /// <summary>Zoom in: the number pad's "+", the "+" key, "=" (the same key without Shift) and Shift with it.</summary>
    public static IReadOnlyList<(int Key, bool Shift)> In { get; } = [(NumPadPlus, false), (Plus, false), (Plus, true)];

    /// <summary>Zoom out: the number pad's "−" and the "-" key.</summary>
    public static IReadOnlyList<(int Key, bool Shift)> Out { get; } = [(NumPadMinus, false), (Minus, false)];
}
