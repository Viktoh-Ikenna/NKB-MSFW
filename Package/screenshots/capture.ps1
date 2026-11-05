# Takes the Microsoft Store screenshots: drives the INSTALLED Store build of
# Nailangs on a Windows desktop and captures the screen. Run by
# .github/workflows/screenshots.yml. Windows PowerShell 5.1 (needs UI Automation).
#
#   capture.ps1 -IdentityName <Package/Identity/Name> -Out <folder>

param(
    [Parameter(Mandatory)] [string] $IdentityName,
    [string] $Out = 'screenshots'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;

public static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettings(ref DEVMODE dm, int flags);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hgt, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);

    public static string Modes()
    {
        var seen = new System.Collections.Generic.SortedSet<string>();
        var dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++) seen.Add(dm.dmPelsWidth + "x" + dm.dmPelsHeight);
        return string.Join(", ", seen);
    }

    /// 0 on success (DISP_CHANGE_SUCCESSFUL).
    public static int SetResolution(int w, int h)
    {
        var dm = new DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
        if (!EnumDisplaySettings(null, -1, ref dm)) return -100;
        dm.dmPelsWidth = w; dm.dmPelsHeight = h;
        dm.dmFields = 0x80000 | 0x100000;           // DM_PELSWIDTH | DM_PELSHEIGHT
        return ChangeDisplaySettings(ref dm, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public int type; public InputUnion u; }

    /// A "physical" key event: Nailangs' keyboard hook only skips the input it
    /// injects itself, so it remaps these like real key presses.
    public static void Key(int vk, bool down)
    {
        var ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = (ushort)MapVirtualKey((uint)vk, 0) };
        if (!down) ki.dwFlags |= 2;                                     // KEYEVENTF_KEYUP
        if (vk == 0xA5 || (vk >= 0x21 && vk <= 0x28)) ki.dwFlags |= 1;  // KEYEVENTF_EXTENDEDKEY
        var input = new INPUT { type = 1 };
        input.u.ki = ki;
        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }
}
"@

[Native]::SetProcessDPIAware() | Out-Null
New-Item -ItemType Directory -Force $Out | Out-Null
$UIA = [System.Windows.Automation.AutomationElement]
$Cond = [System.Windows.Automation.PropertyCondition]
$Scope = [System.Windows.Automation.TreeScope]

function Shot([string] $name) {
    Start-Sleep -Milliseconds 800
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $Out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "::notice::saved $name.png ($($b.Width)x$($b.Height))"
}

# Annotations (::error::/::notice::) show on the run page without opening logs.
trap {
    Write-Host "::error::$($_.Exception.Message) (line $($_.InvocationInfo.ScriptLineNumber))"
    Shot 'zz-error'
    break
}

function WaitWindow([string] $title, [int] $seconds = 30) {
    for ($i = 0; $i -lt $seconds * 2; $i++) {
        $w = $UIA::RootElement.FindFirst($Scope::Children,
            (New-Object $Cond $UIA::NameProperty, $title))
        if ($w) { return $w }
        Start-Sleep -Milliseconds 500
    }
    throw "Window '$title' did not appear"
}

# The control whose subtree has a text element named $text (WPF buttons and tabs
# here have a StackPanel header, so the name sits on a child TextBlock).
function FindByText($root, [System.Windows.Automation.ControlType] $type, [string] $text) {
    $all = $root.FindAll($Scope::Descendants, (New-Object $Cond $UIA::ControlTypeProperty, $type))
    foreach ($e in $all) {
        if ($e.Current.Name -eq $text) { return $e }
        $hit = $e.FindFirst($Scope::Descendants, (New-Object $Cond $UIA::NameProperty, $text))
        if ($hit) { return $e }
    }
    throw "No $($type.ProgrammaticName) with text '$text'"
}

function Tap([int] $vk) { [Native]::Key($vk, $true); [Native]::Key($vk, $false); Start-Sleep -Milliseconds 120 }
function Tone([int] $vk) { [Native]::Key(0xA5, $true); Tap $vk; [Native]::Key(0xA5, $false); Start-Sleep -Milliseconds 120 }
function Shifted([int] $vk) { [Native]::Key(0xA0, $true); Tap $vk; [Native]::Key(0xA0, $false); Start-Sleep -Milliseconds 120 }
function Letters([string] $s) { foreach ($c in $s.ToUpper().ToCharArray()) { Tap ([int] $c) } }

# --- screen size: the Store recommends 1366x768 or larger --------------------
Write-Host "::notice::Available modes: $([Native]::Modes())"
foreach ($r in @(@(1366, 768), @(1920, 1080), @(1600, 900), @(1280, 1024))) {
    $rc = [Native]::SetResolution($r[0], $r[1])
    Write-Host "::notice::SetResolution $($r[0])x$($r[1]) -> $rc"
    if ($rc -eq 0) { break }
}
Start-Sleep -Seconds 2

# Big, readable text in Notepad for the typing shot.
$np = 'HKCU:\Software\Microsoft\Notepad'
New-Item $np -Force | Out-Null
Set-ItemProperty $np lfFaceName 'Segoe UI'
Set-ItemProperty $np iPointSize 280 -Type DWord
Set-ItemProperty $np fWrap 1 -Type DWord

# --- launch the installed Store build ----------------------------------------
$pkg = Get-AppxPackage -Name $IdentityName
if (-not $pkg) { throw "Package $IdentityName is not installed" }
Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!Nailangs"
$main = WaitWindow 'Nailangs'
$mainHwnd = [IntPtr] $main.Current.NativeWindowHandle
$main.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).SetWindowVisualState('Maximized')
[Native]::SetForegroundWindow($mainHwnd) | Out-Null
Start-Sleep -Seconds 2

# 1. Keyboard tab (the Store build opens on it)
Shot '01-keyboard'

# 2. Alt Gr layer: the tone keys
# "⌥ Alt Gr", spelled with [char] since Windows PowerShell reads this file as ANSI.
$altGr = $main.FindFirst($Scope::Descendants, (New-Object $Cond $UIA::NameProperty, "$([char]0x2325) Alt Gr"))
if ($altGr) {
    $altGr.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Shot '02-keyboard-altgr'
    $base = $main.FindFirst($Scope::Descendants, (New-Object $Cond $UIA::NameProperty, 'Base'))
    if ($base) { $base.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle() }
} else { Write-Host "::warning::Alt Gr toggle not found; skipping that shot" }

# 3. Reference tab
$ref = FindByText $main ([System.Windows.Automation.ControlType]::TabItem) 'Reference'
$ref.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Shot '03-reference'

# 4. Pop-out keyboard typing into Notepad
$kbd = FindByText $main ([System.Windows.Automation.ControlType]::TabItem) 'Keyboard'
$kbd.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
Start-Sleep -Seconds 1
$pop = FindByText $main ([System.Windows.Automation.ControlType]::Button) 'Pop Out'
$pop.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
$popout = WaitWindow 'Nailangs Keyboard'
[Native]::ShowWindow($mainHwnd, 6) | Out-Null          # SW_MINIMIZE

$notepad = Start-Process notepad -PassThru
$npWin = WaitWindow 'Untitled - Notepad'
$npHwnd = [IntPtr] $npWin.Current.NativeWindowHandle
[Native]::ShowWindow($npHwnd, 3) | Out-Null            # SW_MAXIMIZE
[Native]::SetForegroundWindow($npHwnd) | Out-Null

# Pop-out along the bottom of the screen, leaving the typed text visible.
$scr = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$pw = [Math]::Min(960, $scr.Width - 40); $ph = [Math]::Min(480, [int]($scr.Height * 0.62))
[Native]::SetWindowPos([IntPtr] $popout.Current.NativeWindowHandle, [IntPtr]::Zero,
    [int](($scr.Width - $pw) / 2), $scr.Bottom - $ph - 10, $pw, $ph, 0x0010) | Out-Null   # SWP_NOACTIVATE
Start-Sleep -Seconds 1

# Typed through Nailangs: number-row keys are the special letters
# (6 ị, 8 ọ, 9 ụ, 0 ẹ, 4 ƙ); Alt Gr+O / Alt Gr+I add an acute / grave tone.
Tap 0x30; Tap 0x20                                     # ẹ
Letters 'ka'; Tone 0x4F                                # ká
Letters 'a'; Tone 0x49                                 # à
Letters 'b'; Tap 0x38; Tone 0x49                       # bọ̀
Tap 0x0D
Tap 0x39; Letters 'b'; Tap 0x38; Letters 'ch'; Tap 0x36; Tap 0x20   # ụbọchị
Tap 0x38; Letters 'ma'                                 # ọma
Tap 0x0D
Tap 0x34; Letters 'asar'; Tap 0x20; Shifted 0x4E; Letters 'ajeriya' # ƙasar Najeriya
Shot '04-popout-typing'

Get-Process Nailangs -ErrorAction SilentlyContinue | Stop-Process -Force
$notepad | Stop-Process -Force -ErrorAction SilentlyContinue
