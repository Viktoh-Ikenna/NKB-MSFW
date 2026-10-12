using System.Runtime.InteropServices;

namespace Nailangs;

/// Watches physical key presses system-wide (low-level keyboard hook) so the
/// on-screen keyboard can light up the matching cap and, via Intercept, remap or
/// swallow physical keys to mirror the virtual keyboard. Mouse-downs are watched
/// too (never swallowed) so the caller can tell when the caret may have moved.
/// Only runs while started.
public sealed class KeyMonitor : IDisposable
{
    public event Action? Changed;

    /// Synchronous decision hook, invoked for every real (non-injected) key event
    /// as (vk, isDown, isRepeat). Return true to swallow the physical key so it
    /// never reaches the focused app — the caller injects a replacement itself.
    /// Runs on the hook's thread (the UI thread, since that is where the hook is
    /// installed).
    public Func<int, bool, bool, bool>? Intercept;

    /// A real mouse button went down. The argument is true when the click landed
    /// on one of this app's own windows (e.g. the pop-out keyboard).
    public event Action<bool>? MouseDown;

    private readonly HashSet<int> _pressed = new();
    public bool IsPressed(int vk) => _pressed.Contains(vk);

    private const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204;
    private const uint GA_ROOT = 2;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private HookProc? _keyProc, _mouseProc;       // keep alive
    private IntPtr _keyHook = IntPtr.Zero, _mouseHook = IntPtr.Zero;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    public void Start()
    {
        if (_keyHook != IntPtr.Zero) return;
        var module = GetModuleHandle(null);
        _keyProc = KeyCallback;
        _mouseProc = MouseCallback;
        _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, module, 0);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, module, 0);
    }

    public void Stop()
    {
        if (_keyHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        _keyProc = null;
        _mouseProc = null;
        _pressed.Clear();
        Changed?.Invoke();
    }

    private IntPtr KeyCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Skip the input we synthesize ourselves — otherwise the glyphs we
            // inject would be re-read as fresh presses and loop forever.
            if (data.dwExtraInfo != KeyInput.Signature)
            {
                int msg = wParam.ToInt32();
                int vk = (int)data.vkCode;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = msg is WM_KEYUP or WM_SYSKEYUP;
                if (down || up)
                {
                    // Low-level hooks fire on the installing (UI) thread, so this
                    // runs synchronously — required, because the return value below
                    // decides whether the key is swallowed.
                    bool repeat = down && _pressed.Contains(vk);
                    if (down) _pressed.Add(vk); else _pressed.Remove(vk);
                    bool suppress = Intercept?.Invoke(vk, down, repeat) ?? false;
                    Changed?.Invoke();
                    if (suppress) return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                MouseDown?.Invoke(IsOwnWindow(data.pt));
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    /// Did this click land on one of our own windows (main window or pop-out)?
    private static bool IsOwnWindow(POINT pt)
    {
        var hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return false;
        GetWindowThreadProcessId(GetAncestor(hwnd, GA_ROOT), out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    public void Dispose() => Stop();
}
