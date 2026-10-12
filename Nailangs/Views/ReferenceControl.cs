using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nailangs;

/// Reference tab: the nine dead keys, the special base letters, the test string
/// and notes — mirrors the Mac ReferenceView.
public sealed class ReferenceControl : UserControl
{
    private const double CardWidth = 180;

    public ReferenceControl()
    {
        var root = new StackPanel { Margin = new Thickness(28), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Center };

        root.Children.Add(Section("Dead keys",
            "Type the letter first, then hold right Alt (Alt Gr) and tap the host key to mark it.",
            Cards(DeadKeys.All.Select(DeadCard))));

        root.Children.Add(Section("Special base letters",
            "Typed directly (no modifier). Caps Lock gives the uppercase form.",
            Cards(NailangsData.Specials.Select(SpecialCard))));

        root.Children.Add(Section("Test string",
            "If you can type this line, every base special and all nine dead keys work.",
            new Border
            {
                Background = SystemColors.ControlBrush,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                // Read-only TextBox so the line can be selected and copied.
                Child = new TextBox
                {
                    Text = "ɓɗǝẹɔŋɛ  àéñôüçāøɨ",
                    FontSize = 26,
                    FontWeight = FontWeights.Medium,
                    IsReadOnly = true,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent
                }
            }));

        var notes = new StackPanel();
        notes.Children.Add(Bullet("Right Alt (Alt Gr) only is the Alt Gr layer; left Alt stays standard."));
        notes.Children.Add(Bullet("ß is on Alt Gr + /, and ẞ on Alt Gr + S."));
        notes.Children.Add(Bullet("` types “gb”, Shift+` types ₦ (Naira)."));
        notes.Children.Add(Bullet("Some accented results are a letter + combining mark where no single character exists."));
        if (!AppInfo.IsPackaged)   // the Store build ships no installed layout
            notes.Children.Add(Bullet("Installed layout: a Windows dead key can't make letter + combining mark (ẹ́, ọ̀, ɔ̃ …). " +
                                      "Type the letter, then Alt Gr + Shift + the tone key to add the mark. The pop-out keyboard does this for you."));
        root.Children.Add(Section("Notes", null, notes));

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private static UIElement Section(string title, string? subtitle, UIElement content)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
        sp.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Brand.Primary });
        if (subtitle != null)
            sp.Children.Add(new TextBlock
            {
                Text = subtitle, FontSize = 13, Foreground = SystemColors.GrayTextBrush,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            });
        if (content is FrameworkElement fe) fe.Margin = new Thickness(0, 10, 0, 0);
        sp.Children.Add(content);
        return sp;
    }

    private static UIElement Cards(IEnumerable<UIElement> cards)
    {
        var wrap = new WrapPanel();
        foreach (var c in cards) wrap.Children.Add(c);
        return wrap;
    }

    private static Border Card(UIElement child) => new()
    {
        Width = CardWidth,
        Margin = new Thickness(0, 0, 10, 10),
        Padding = new Thickness(10),
        CornerRadius = new CornerRadius(10),
        Background = SystemColors.ControlBrush,
        Child = child
    };

    private static UIElement DeadCard(DeadKey d)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Border
        {
            Width = 40, Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = Brand.AccentTint,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = d.Mark, FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brand.Primary,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        });
        var text = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(d.Name), FontSize = 14, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock { Text = $"Alt Gr + {d.HostLabel}", FontSize = 11, Foreground = SystemColors.GrayTextBrush });
        text.Children.Add(new TextBlock { Text = d.Sample, FontSize = 13 });
        row.Children.Add(text);
        return Card(row);
    }

    private static UIElement SpecialCard(SpecialLetter s)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = $"{s.Lower} {s.Upper}", FontSize = 22, FontWeight = FontWeights.Medium,
            MinWidth = 52, VerticalAlignment = VerticalAlignment.Center
        });
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = $"key “{s.Key}”", FontSize = 11 });
        text.Children.Add(new TextBlock
        {
            Text = s.Codepoint, FontSize = 10, Foreground = SystemColors.GrayTextBrush,
            FontFamily = new FontFamily("Consolas, Courier New")
        });
        row.Children.Add(text);
        return Card(row);
    }

    private static UIElement Bullet(string text)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var dot = new TextBlock { Text = "•", Foreground = Brand.Primary, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(dot, Dock.Left);
        row.Children.Add(dot);
        row.Children.Add(new TextBlock { Text = text, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        return row;
    }
}
