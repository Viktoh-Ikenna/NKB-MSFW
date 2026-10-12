using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Nailangs;

/// Always-on-top floating keyboard. WS_EX_NOACTIVATE keeps typing focus in the
/// app you're using, so clicking keys inserts characters there (not here).
public sealed class PopoutWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    public PopoutWindow()
    {
        Title = "Nailangs Keyboard";
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.ToolWindow;
        ResizeMode = ResizeMode.CanResize;
        Width = 960;
        Height = 520;
        MinWidth = 520;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = SystemColors.WindowBrush;
        Content = new KeyboardControl(interactive: true, showsPopOutButton: false);
        SourceInitialized += (_, _) => ApplyNoActivate();
    }

    private void ApplyNoActivate()
    {
        var h = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(h, GWL_EXSTYLE);
        SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }
}

/// Manages the single pop-out keyboard window, so the user can keep the Nailangs
/// layout visible while typing in another app.
public static class FloatingKeyboard
{
    private static PopoutWindow? _window;

    /// Drives button labels in the UI; stays in sync as the window opens/closes.
    public static bool IsOpen => _window != null;
    public static event Action? Changed;

    public static void Toggle()
    {
        if (IsOpen) Close(); else Show();
    }

    public static void Show()
    {
        if (_window != null) { _window.Show(); return; }
        _window = new PopoutWindow();
        _window.Closed += (_, _) => { _window = null; Changed?.Invoke(); };
        _window.Show();
        Changed?.Invoke();
    }

    public static void Close() => _window?.Close();
}
