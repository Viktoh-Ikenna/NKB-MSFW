using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Nailangs;

/// Brand colours, shared with the macOS/iOS apps.
public static class Brand
{
    public static readonly Color PrimaryGreen = Color.FromRgb(56, 130, 59);   // #38823B
    public static readonly Color AccentGreen  = Color.FromRgb(124, 185, 64);  // #7CB940

    public static readonly SolidColorBrush Primary = new(PrimaryGreen);
    public static readonly SolidColorBrush Accent  = new(AccentGreen);
    public static readonly SolidColorBrush AccentSoft = new(Color.FromArgb(40, 124, 185, 64));     // ~16%
    public static readonly SolidColorBrush AccentTint = new(Color.FromArgb(46, 124, 185, 64));     // ~18%
    public static readonly SolidColorBrush AccentWash = new(Color.FromArgb(31, 124, 185, 64));     // ~12%

    /// Vertical accent → primary gradient (buttons, the logo key).
    public static LinearGradientBrush GreenGradient() => new()
    {
        StartPoint = new(0, 0),
        EndPoint = new(0, 1),
        GradientStops =
        {
            new GradientStop(AccentGreen, 0),
            new GradientStop(PrimaryGreen, 1),
        }
    };

    /// Horizontal primary → accent gradient (the header bar).
    public static LinearGradientBrush HeaderGradient() => new()
    {
        StartPoint = new(0, 0),
        EndPoint = new(1, 0),
        GradientStops =
        {
            new GradientStop(PrimaryGreen, 0),
            new GradientStop(AccentGreen, 1),
        }
    };

    static Brand()
    {
        Primary.Freeze(); Accent.Freeze(); AccentSoft.Freeze(); AccentTint.Freeze(); AccentWash.Freeze();
    }
}

/// Curved green header bar, matching the macOS / iOS GreenHeaderBar.
public sealed class GreenHeaderBar : Grid
{
    private readonly Path _curve = new() { Fill = Brand.HeaderGradient() };

    public GreenHeaderBar(string title, string? subtitle = null)
    {
        Height = 96;
        Children.Add(_curve);
        SizeChanged += (_, e) => _curve.Data = CurveGeometry(e.NewSize.Width, e.NewSize.Height);

        var mark = new Border
        {
            Width = 60, Height = 60,
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(14),
            Background = Brushes.White,
            Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.18 },
            Child = new Image { Source = KeyboardControl.BrandImage("nailangs-mark.png"), Stretch = Stretch.Uniform }
        };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.White });
        if (subtitle != null)
            text.Children.Add(new TextBlock
            {
                Text = subtitle, FontSize = 13, FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255))
            });

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 14)
        };
        row.Children.Add(mark);
        row.Children.Add(text);
        Children.Add(row);
    }

    /// Flat top, sides down to 72% of the height, then a quad curve dipping to the
    /// bottom in the middle — the same shape as HeaderCurveShape on the Mac.
    private static Geometry CurveGeometry(double w, double h)
    {
        var fig = new PathFigure { StartPoint = new Point(0, 0), IsClosed = true };
        fig.Segments.Add(new LineSegment(new Point(w, 0), true));
        fig.Segments.Add(new LineSegment(new Point(w, h * 0.72), true));
        fig.Segments.Add(new QuadraticBezierSegment(new Point(w / 2, h), new Point(0, h * 0.72), true));
        return new PathGeometry(new[] { fig });
    }
}

/// Rounded gradient button matching the macOS / iOS GradientButton.
public sealed class GradientButton : Button
{
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    public string Title { get => _title.Text; set => _title.Text = value; }

    public GradientButton(string title, string? icon = null)
    {
        Title = title;
        Foreground = Brushes.White;
        Cursor = System.Windows.Input.Cursors.Hand;

        var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon != null)
            content.Children.Add(new TextBlock
            {
                Text = icon, FontFamily = Icons.Font, FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0)
            });
        content.Children.Add(_title);
        Content = content;

        // Replace the stock chrome with a flat rounded gradient.
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, Brand.GreenGradient());
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
        border.SetValue(Border.PaddingProperty, new Thickness(16, 12, 16, 12));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.AppendChild(presenter);
        Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}

/// Segoe MDL2 glyphs standing in for the SF Symbols the Mac app uses.
public static class Icons
{
    public static readonly FontFamily Font = new("Segoe MDL2 Assets");
    public const string Download = "";   // square.and.arrow.down
    public const string Keyboard = "";   // keyboard
    public const string Book     = "";   // character.book.closed
    public const string Settings = "";   // gearshape
    public const string Folder   = "";   // folder
    public const string Check    = "";   // checkmark.seal.fill
    public const string Pending  = "";   // keyboard.badge.ellipsis
    public const string PopOut   = "";   // macwindow.badge.plus
    public const string Window   = "";   // macwindow
    public const string Cursor   = "";   // text.cursor
}
