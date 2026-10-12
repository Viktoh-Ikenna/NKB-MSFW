using System.Runtime.InteropServices;

namespace Nailangs;

/// Types arbitrary Unicode into the focused app via SendInput. Unlike macOS,
/// Windows needs no special "Accessibility" permission for this (but you cannot
/// inject into a higher-integrity / elevated window unless this app is elevated).
public static class KeyInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    /// Stamped onto every event we synthesize so the global hook can recognise —
    /// and ignore — its own echo instead of re-processing it into a loop.
    public static readonly IntPtr Signature = (IntPtr)0x4E4C4E47; // "NLNG"

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// Type a string into whatever window currently has focus.
    public static void Type(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char ch in text)        // each UTF-16 code unit (handles surrogates)
        {
            inputs.Add(MakeChar(ch, keyUp: false));
            inputs.Add(MakeChar(ch, keyUp: true));
        }
        var arr = inputs.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// Modifiers lifted around SendKey, in release order (Alt before Ctrl, so a
    /// lone Alt release can't pop the focused app's menu bar). (vk, extended)
    private static readonly (ushort vk, bool ext)[] Modifiers =
    {
        (0xA5, true), (0xA4, false),            // RMENU (Alt Gr), LMENU
        (0xA0, false), (0xA1, false),           // LSHIFT, RSHIFT
        (0xA2, false), (0xA3, true),            // LCONTROL, RCONTROL
        (0x5B, true), (0x5C, true),             // LWIN, RWIN
    };

    /// Press and release a real virtual key (Backspace, Enter, Tab, arrows, …) in
    /// the focused app. Unicode injection cannot produce these — they need a VK.
    /// Extended keys (the arrow cluster) must carry KEYEVENTF_EXTENDEDKEY.
    ///
    /// Held modifiers are released for the key and restored afterwards — the
    /// equivalent of the Mac posting with cleared flags. Without it a physically
    /// held Alt Gr (= Ctrl+Alt) turns the tone's Backspace into Ctrl+Backspace,
    /// which deletes a whole word.
    public static void SendKey(ushort vk, bool extended = false)
    {
        var held = Modifiers.Where(m => (GetAsyncKeyState(m.vk) & 0x8000) != 0).ToList();

        var inputs = new List<INPUT>();
        foreach (var (mvk, ext) in held) inputs.Add(MakeVk(mvk, ext, keyUp: true));
        inputs.Add(MakeVk(vk, extended, keyUp: false));
        inputs.Add(MakeVk(vk, extended, keyUp: true));
        for (int i = held.Count - 1; i >= 0; i--) inputs.Add(MakeVk(held[i].vk, held[i].ext, keyUp: false));

        var arr = inputs.ToArray();
        SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeChar(char ch, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = ch,
                dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = Signature
            }
        }
    };

    private static INPUT MakeVk(ushort vk, bool extended, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = (extended ? KEYEVENTF_EXTENDEDKEY : 0) | (keyUp ? KEYEVENTF_KEYUP : 0),
                time = 0,
                dwExtraInfo = Signature
            }
        }
    };
}
