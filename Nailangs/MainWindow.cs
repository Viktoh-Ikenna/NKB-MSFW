using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nailangs;

/// Green header + Install / Keyboard / Reference tabs — mirrors the Mac MainView.
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Nailangs";
        Width = 1000;
        Height = 720;
        MinWidth = 900;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = SystemColors.WindowBrush;
        Icon = KeyboardControl.BrandImage("nailangs-mark.png");

        var tabs = new TabControl { Margin = new Thickness(12, 6, 12, 12) };
        // The Store build can't run the elevated layout installer, so it has no
        // Install tab and opens on the keyboard instead.
        if (!AppInfo.IsPackaged)
            tabs.Items.Add(Tab("Install", Icons.Download, new InstallControl()));
        tabs.Items.Add(Tab("Keyboard", Icons.Keyboard, new KeyboardControl(interactive: false, showsPopOutButton: true)));
        tabs.Items.Add(Tab("Reference", Icons.Book, new ReferenceControl()));

        var root = new DockPanel();
        var header = new GreenHeaderBar("Nailangs", "Windows keyboard layout");
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(tabs);
        Content = root;

        Closed += (_, _) => FloatingKeyboard.Close();
    }

    private static TabItem Tab(string title, string icon, UIElement content)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock
        {
            Text = icon, FontFamily = Icons.Font, Foreground = Brand.Primary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
        });
        header.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
        return new TabItem { Header = header, Content = content, Padding = new Thickness(12, 4, 12, 4) };
    }
}
