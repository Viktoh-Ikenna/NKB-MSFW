using System.Runtime.InteropServices;
using System.Windows;

namespace Nailangs;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application
        {
            // The pop-out has no taskbar button, so it must not keep the app alive
            // once the main window is closed.
            ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        app.Run(new MainWindow());
    }
}

/// Facts about how this copy of the app was installed.
public static class AppInfo
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// True when running from an MSIX package (the Microsoft Store build). A Store
    /// app may not run the elevated layout installer, so the Install tab is hidden.
    public static readonly bool IsPackaged = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            int len = 0;
            return GetCurrentPackageFullName(ref len, null) != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch { return false; }   // API missing (very old Windows): not packaged
    }
}
