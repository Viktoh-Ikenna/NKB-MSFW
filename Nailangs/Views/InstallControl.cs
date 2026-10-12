using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nailangs;

/// Install tab: registers the Nailangs layout (via the bundled MSKLC installer)
/// and links to Windows language settings — mirrors the Mac InstallView.
public sealed class InstallControl : UserControl
{
    private readonly TextBlock _statusIcon = new() { FontFamily = Icons.Font, FontSize = 34, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { FontWeight = FontWeights.SemiBold, FontSize = 15 };
    private readonly TextBox _path = new()
    {
        IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0),
        FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 11, Foreground = SystemColors.GrayTextBrush
    };
    private readonly GradientButton _install = new("Install Keyboard", Icons.Download);
    private readonly UIElement _nextSteps;
    private bool _didSucceed;

    public InstallControl()
    {
        var root = new StackPanel { Margin = new Thickness(28), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Center };

        // status card
        var statusText = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        statusText.Children.Add(_status);
        statusText.Children.Add(_path);
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
        statusRow.Children.Add(_statusIcon);
        statusRow.Children.Add(statusText);
        root.Children.Add(new Border
        {
            Background = SystemColors.ControlBrush,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 20),
            Child = statusRow
        });

        _install.HorizontalAlignment = HorizontalAlignment.Left;
        _install.Width = 360;
        _install.Margin = new Thickness(0, 0, 0, 20);
        _install.Click += async (_, _) => await RunInstall();
        root.Children.Add(_install);

        _nextSteps = NextSteps();
        _nextSteps.Visibility = Visibility.Collapsed;
        root.Children.Add(_nextSteps);

        root.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 20) });

        // helper buttons
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        buttons.Children.Add(HelperButton("Open Language Settings", Icons.Settings, LayoutInstaller.OpenLanguageSettings));
        buttons.Children.Add(HelperButton("Open Layout Folder", Icons.Folder, LayoutInstaller.OpenLayoutFolder));
        root.Children.Add(buttons);

        root.Children.Add(ManualFallback());

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Loaded += (_, _) => Refresh();
    }

    // MARK: - pieces

    private static UIElement NextSteps()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Almost there — two more steps", FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 10) });
        sp.Children.Add(StepRow(1, "Sign out and back in (or restart) if Nailangs doesn't show up yet — Windows loads new layouts at sign-in."));
        sp.Children.Add(StepRow(2, "Open Language Settings → your language → Language options → Add a keyboard, then pick “Nailangs”. Switch with Win+Space."));
        return new Border
        {
            Background = Brand.AccentWash,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 20),
            Child = sp
        };
    }

    private static UIElement StepRow(int n, string text)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var badge = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = Brand.Primary,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = n.ToString(), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        DockPanel.SetDock(badge, Dock.Left);
        row.Children.Add(badge);
        row.Children.Add(new TextBlock { Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static Button HelperButton(string title, string icon, Action action)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock { Text = icon, FontFamily = Icons.Font, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        content.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = content, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 12, 0) };
        b.Click += (_, _) => action();
        return b;
    }

    private static UIElement ManualFallback()
    {
        var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        sp.Children.Add(new TextBlock
        {
            Text = "Open layout\\Nailangs.klc in Microsoft Keyboard Layout Creator (MSKLC 1.4), choose " +
                   "Project → Build DLL and Setup Package, then run the setup.exe it produces as administrator:",
            FontSize = 13, TextWrapping = TextWrapping.Wrap
        });
        sp.Children.Add(new Border
        {
            Background = SystemColors.WindowBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 8, 0, 0),
            Child = new TextBox
            {
                Text = LayoutInstaller.LayoutFolder,
                IsReadOnly = true, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                FontFamily = new FontFamily("Consolas, Courier New"), FontSize = 12
            }
        });
        return new Expander { Header = "Prefer to install manually?", Content = sp, Margin = new Thickness(0, 4, 0, 0) };
    }

    // MARK: - action

    private async Task RunInstall()
    {
        switch (LayoutInstaller.Install())
        {
            case LayoutInstaller.Result.Launched launched:
                _install.IsEnabled = false;
                _install.Title = "Installing…";
                try { await launched.Process.WaitForExitAsync(); }
                catch { /* elevated process: exit may not be observable */ }
                _install.IsEnabled = true;
                _didSucceed = LayoutInstaller.IsInstalled;
                if (!_didSucceed)
                    Alert("Not installed yet",
                          "The installer closed but the Nailangs layout isn't registered. Run it again and follow its prompts.");
                Refresh();
                break;
            case LayoutInstaller.Result.Cancelled:
                break;
            case LayoutInstaller.Result.Failed failed:
                _didSucceed = false;
                Alert("Install failed", failed.Message);
                Refresh();
                break;
        }
    }

    private void Refresh()
    {
        bool installed = LayoutInstaller.IsInstalled;
        _statusIcon.Text = installed ? Icons.Check : Icons.Pending;
        _statusIcon.Foreground = installed ? Brand.Primary : SystemColors.GrayTextBrush;
        _status.Text = installed ? "Layout is installed" : "Not installed yet";
        _path.Text = LayoutInstaller.BundledInstaller ?? LayoutInstaller.ExpectedInstaller;
        if (_install.IsEnabled) _install.Title = installed ? "Re-install Keyboard" : "Install Keyboard";
        _nextSteps.Visibility = _didSucceed ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void Alert(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
