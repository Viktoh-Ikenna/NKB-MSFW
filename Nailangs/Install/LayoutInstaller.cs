using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Nailangs;

/// On Windows a keyboard layout is a registered DLL, not a copied file. We ship
/// the MSKLC-built installer (setup.exe) and launch it elevated; the app itself
/// just orchestrates and links to the language settings.
public static class LayoutInstaller
{
    public const string LayoutText = "Nailangs";

    /// The folder next to the app that holds the layout source and MSKLC output.
    public static string LayoutFolder => Path.Combine(AppContext.BaseDirectory, "layout");

    /// Where the installer is expected when none is bundled yet (shown in the UI).
    public static string ExpectedInstaller => Path.Combine(LayoutFolder, "setup.exe");

    /// The MSKLC output, looked for next to the app (layout\setup.exe).
    public static string? BundledInstaller
    {
        get
        {
            string baseDir = AppContext.BaseDirectory;
            foreach (var rel in new[] { @"layout\setup.exe", @"layout\Setup.exe", @"layout\Nailangs\setup.exe", "setup.exe" })
            {
                var p = Path.Combine(baseDir, rel);
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }

    /// True if a keyboard layout named "Nailangs" is registered.
    public static bool IsInstalled
    {
        get
        {
            try
            {
                using var root = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts");
                if (root == null) return false;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var sub = root.OpenSubKey(name);
                    var text = sub?.GetValue("Layout Text") as string;
                    if (string.Equals(text, LayoutText, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { /* registry not readable */ }
            return false;
        }
    }

    public abstract record Result
    {
        /// The installer is running; await it to know when to re-check.
        public sealed record Launched(Process Process) : Result;
        /// The user declined the UAC prompt.
        public sealed record Cancelled : Result;
        /// No installer bundled, or it could not start.
        public sealed record Failed(string Message) : Result;
    }

    /// Run the bundled installer (elevated).
    public static Result Install()
    {
        var installer = BundledInstaller;
        if (installer == null)
            return new Result.Failed(
                "No bundled installer found. Build the layout first " +
                "(layout\\Nailangs.klc → MSKLC → setup.exe) and place its output in the 'layout' folder.");
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = installer,
                UseShellExecute = true,
                Verb = "runas"      // request UAC elevation
            });
            return p == null ? new Result.Failed("The installer did not start.") : new Result.Launched(p);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)   // ERROR_CANCELLED
        {
            return new Result.Cancelled();
        }
        catch (Exception ex)
        {
            return new Result.Failed("Could not start the installer: " + ex.Message);
        }
    }

    public static void OpenLanguageSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:regionlanguage",
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }

    public static void OpenLayoutFolder()
    {
        var dir = Directory.Exists(LayoutFolder) ? LayoutFolder : AppContext.BaseDirectory;
        try { Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch { /* ignore */ }
    }
}
