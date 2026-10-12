using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Nailangs;

/// On-screen keyboard with multi-legend keycaps (Shift symbol top-left, letter in
/// the centre, Alt Gr / dead-key mark bottom-right) — mirrors the macOS preview.
/// When Interactive, clicking a key types into the focused app, physical keys
/// light up and are remapped, and Shift / Caps Lock / Alt Gr switch the layer.
///
/// Tones are applied AFTER the letter, as on the Mac: type `a`, then Alt Gr + the
/// acute key → the `a` is taken back and re-typed as `á`.
public sealed class KeyboardControl : UserControl
{
    public bool Interactive { get; }
    private readonly bool _showsPopOutButton;

    private Layer _layer = Layer.Base;
    private readonly KeyMonitor _monitor = new();
    private readonly List<KeyVisual> _visuals = new();
    private readonly List<(Layer layer, ToggleButton btn)> _layerButtons = new();
    private TextBlock _hint = null!;
    private Border _toneBanner = null!;
    private TextBlock _toneBannerText = null!;
    private Button? _popOutButton;

    /// The letter a tone would land on: the last glyph this keyboard typed into the
    /// focused app. Marks are applied AFTER the letter (type `a`, then Alt Gr+acute
    /// → á), so this is the composition target. null whenever we can no longer be
    /// sure what sits before the caret — the caret moved, or something else typed.
    private string? _lastGlyph;

    // Physical modifier state; the sticky flags come from clicking the on-screen
    // modifiers / the layer picker.
    private bool _shiftHeld, _altGrHeld, _capsHardware;
    private bool _stickyShift, _stickyAltGr, _stickyCaps;
    /// Set when a tone has been taken from this Alt Gr press: the layer drops back
    /// immediately instead of staying lit until the key is released.
    private bool _altGrConsumed;

    // Physical virtual-key -> the cap it types. Character keys only; modifier /
    // function keys (Backspace, Enter, arrows, …) are deliberately absent so they
    // pass straight through to the focused app.
    private readonly Dictionary<int, KeyCap> _vkMap = new();

    private const double Unit = 44, Gap = 5, Radius = 9;
    private const double MaxUnit = 80;              // same ceiling as the Mac preview

    // Windows virtual-key codes we react to directly.
    private const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10,
        VK_CONTROL = 0x11, VK_MENU = 0x12,
        VK_CAPITAL = 0x14, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
        VK_LWIN = 0x5B, VK_RWIN = 0x5C,
        VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3,
        VK_LMENU = 0xA4, VK_RMENU = 0xA5;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private sealed class KeyVisual
    {
        public KeyCap Key = null!;
        public Border Border = null!;
        public ScaleTransform Scale = null!;
        public TextBlock Center = null!;
        public TextBlock? TopLeft;
        public bool TopLeftIsAlt;
        public TextBlock? BottomRight;
    }

    public KeyboardControl(bool interactive, bool showsPopOutButton = false)
    {
        Interactive = interactive;
        _showsPopOutButton = showsPopOutButton;
        Content = Build();
        UpdateAll();
        if (interactive)
        {
            BuildVkMap();
            _monitor.Changed += UpdateAll;
            _monitor.Intercept = OnPhysicalKey;
            _monitor.MouseDown += OnMouseDown;
            Loaded += (_, _) =>
            {
                _capsHardware = (GetKeyState(VK_CAPITAL) & 1) != 0;
                RecomputeLayer();
                _monitor.Start();
            };
            Unloaded += (_, _) => _monitor.Stop();
        }
        if (showsPopOutButton)
        {
            FloatingKeyboard.Changed += UpdatePopOutButton;
            Unloaded += (_, _) => FloatingKeyboard.Changed -= UpdatePopOutButton;
        }
    }

    private void BuildVkMap()
    {
        foreach (var row in NailangsData.Rows)
            foreach (var key in row)
                if (!key.IsModifier && key.Vk is { } vk)
                    _vkMap[vk] = key;
    }

    // MARK: - layout

    private UIElement Build()
    {
        var root = new DockPanel { Margin = new Thickness(24), LastChildFill = true };

        // layer selector (+ pop-out button on the right)
        var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        var sel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (l, label) in new[] { (Layer.Base, "Base"), (Layer.Shift, "⇧ Shift"), (Layer.Caps, "⇪ Caps"), (Layer.AltGr, "⌥ Alt Gr") })
        {
            var tb = new ToggleButton { Content = label, Padding = new Thickness(14, 4, 14, 4), Margin = new Thickness(1, 0, 1, 0) };
            tb.Click += (_, _) => SelectLayer(l);
            _layerButtons.Add((l, tb));
            sel.Children.Add(tb);
        }
        top.Children.Add(sel);
        if (_showsPopOutButton)
        {
            _popOutButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(10, 4, 10, 4),
                ToolTip = "Show the keyboard in a floating window that stays on top while you type in other apps."
            };
            _popOutButton.Click += (_, _) => FloatingKeyboard.Toggle();
            top.Children.Add(_popOutButton);
            UpdatePopOutButton();
        }
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        _hint = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            Foreground = SystemColors.GrayTextBrush,
            FontSize = 13,
            MaxWidth = 640,
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(_hint, Dock.Top);
        root.Children.Add(_hint);

        // "“a” typed — tap a tone…" capsule, shown while a tone target exists.
        _toneBannerText = new TextBlock { FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Brand.Primary, VerticalAlignment = VerticalAlignment.Center };
        var bannerRow = new StackPanel { Orientation = Orientation.Horizontal };
        bannerRow.Children.Add(new TextBlock
        {
            Text = Icons.Cursor, FontFamily = Icons.Font, Foreground = Brand.Primary,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
        });
        bannerRow.Children.Add(_toneBannerText);
        _toneBanner = new Border
        {
            Background = Brand.AccentTint,
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12),
            Visibility = Visibility.Collapsed,
            Child = bannerRow
        };
        DockPanel.SetDock(_toneBanner, Dock.Top);
        root.Children.Add(_toneBanner);

        var legend = BuildLegend();
        DockPanel.SetDock(legend, Dock.Bottom);
        root.Children.Add(legend);

        // The keyboard fits itself to the space it is actually given — both width
        // and height — so the pop-out and in-app preview resize freely.
        var grid = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var row in NailangsData.Rows)
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var key in row) rowPanel.Children.Add(BuildKey(key));
            grid.Children.Add(rowPanel);
        }
        var card = new Border
        {
            Background = SystemColors.WindowBrush,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Margin = new Thickness(8),
            Child = grid,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.12 }
        };
        var fit = new Viewbox { Stretch = Stretch.Uniform, Child = card, Margin = new Thickness(0, 0, 0, 16) };
        // Cap the scale-up so keys never grow past the Mac's 80pt maximum.
        card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        fit.MaxWidth = card.DesiredSize.Width * MaxUnit / Unit;
        fit.MaxHeight = card.DesiredSize.Height * MaxUnit / Unit;
        root.Children.Add(fit);

        return root;
    }

    private static UIElement BuildLegend()
    {
        var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        legend.Children.Add(new Border
        {
            Width = 18, Height = 18,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromArgb(56, 124, 185, 64)),
            BorderBrush = Brand.Primary,
            BorderThickness = new Thickness(1.5),
            Margin = new Thickness(0, 0, 6, 0)
        });
        legend.Children.Add(new TextBlock { Text = "Dead key (Alt Gr layer)", FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        legend.Children.Add(new TextBlock
        {
            Text = "9 dead keys • 4 layers • right Alt = Alt Gr",
            FontSize = 11, Foreground = SystemColors.GrayTextBrush,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0)
        });
        return legend;
    }

    private UIElement BuildKey(KeyCap key)
    {
        double w = Unit * key.Width + Gap * (key.Width - 1);
        var scale = new ScaleTransform(1, 1);
        var border = new Border
        {
            Width = w,
            Height = Unit,
            Margin = new Thickness(Gap / 2),
            CornerRadius = new CornerRadius(Radius),
            BorderThickness = new Thickness(1),
            RenderTransform = scale,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var g = new Grid();
        border.Child = g;

        var v = new KeyVisual { Key = key, Border = border, Scale = scale };

        if (key.IsModifier)
        {
            var t = new TextBlock
            {
                Text = key.Accent ? "" : key.Label,   // the logo key shows the mark, not text
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = (key.Label?.Length ?? 0) > 2 ? 11 : 15,
                FontWeight = FontWeights.Medium,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(4, 0, 4, 0)
            };
            v.Center = t;
            g.Children.Add(t);

            if (key.Accent)
            {
                // The logo key: the Nailangs bird mark fills the cap.
                g.Children.Add(new Image
                {
                    Source = BrandImage("nailangs-mark.png"),
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(3)
                });
            }

            var mapped = LayerForModifier(key.Label ?? "");
            var label = (key.Label ?? "").Trim();
            if (mapped != null) border.ToolTip = $"Show the {label} layer";
            else if (Interactive && key.Vk is { } fvk && IsFunctionKey(fvk)) border.ToolTip = $"Send {label}";
        }
        else
        {
            // Multi-legend keycap (like the PDF): the Shift symbol and the Alt Gr /
            // dead-key mark sit in the corners; the letter sits in the centre.
            bool isSpace = key.Base == "space";
            string shiftSym = key.Shift != key.Caps ? key.Shift : "";           // symbol, not a plain uppercase
            string altLegend = key.Dead != null ? key.Dead.Mark : key.AltGr;
            bool altTopLeft = shiftSym.Length == 0 && altLegend.Length > 0;   // no shift symbol → mark goes top-left
            string topLeft = shiftSym.Length == 0 ? altLegend : shiftSym;
            string bottomRight = shiftSym.Length == 0 ? "" : altLegend;

            var center = new TextBlock
            {
                Text = isSpace ? "space" : key.Caps,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = isSpace ? 15 : (key.Caps.Length > 2 ? 15 : 22),
                FontWeight = FontWeights.Medium
            };
            v.Center = center;
            g.Children.Add(center);

            if (!isSpace && topLeft.Length > 0)
            {
                v.TopLeft = MakeCorner(topLeft, HorizontalAlignment.Left, VerticalAlignment.Top);
                v.TopLeftIsAlt = altTopLeft;
                g.Children.Add(v.TopLeft);
            }
            if (!isSpace && bottomRight.Length > 0)
            {
                v.BottomRight = MakeCorner(bottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom);
                g.Children.Add(v.BottomRight);
            }
        }

        border.Background = Brushes.Transparent;   // hit-testable across the whole cap
        border.MouseLeftButtonUp += (_, _) => OnKeyClicked(key);
        if (Interactive) border.Cursor = Cursors.Hand;
        _visuals.Add(v);
        return border;
    }

    private static TextBlock MakeCorner(string text, HorizontalAlignment h, VerticalAlignment vert) => new()
    {
        Text = text,
        FontSize = 11,
        HorizontalAlignment = h,
        VerticalAlignment = vert,
        Margin = new Thickness(5)
    };

    /// A brand PNG bundled as a WPF Resource (see Nailangs.csproj).
    internal static BitmapImage BrandImage(string file) =>
        new(new Uri($"pack://application:,,,/Resources/{file}"));

    private void UpdatePopOutButton()
    {
        if (_popOutButton == null) return;
        bool open = FloatingKeyboard.IsOpen;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = open ? Icons.Window : Icons.PopOut, FontFamily = Icons.Font,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
        });
        row.Children.Add(new TextBlock { Text = open ? "Close Pop-out" : "Pop Out", VerticalAlignment = VerticalAlignment.Center });
        _popOutButton.Content = row;
    }

    // MARK: - clicks

    /// Maps the on-screen modifier keys that drive the layer view.
    private static Layer? LayerForModifier(string label)
    {
        if (label.Contains("Shift")) return Layer.Shift;
        if (label.Contains("Caps")) return Layer.Caps;
        if (label == "Alt Gr") return Layer.AltGr;
        return null;
    }

    private void OnKeyClicked(KeyCap key)
    {
        if (key.IsModifier)
        {
            var l = LayerForModifier(key.Label ?? "");
            if (l is { } layer) ToggleModifier(layer);
            // Backspace / Enter / Tab / arrows: fire the real key into the focused app.
            else if (Interactive && key.Vk is { } vk && IsFunctionKey(vk))
            {
                KeyInput.SendKey((ushort)vk, IsExtended(vk));
                // It deletes or moves past the letter we remember, so nothing is
                // left for a tone to attach to.
                _lastGlyph = null;
                UpdateAll();
            }
            return;
        }
        if (!Interactive) return;
        HandleTap(key);
    }

    /// Modifier / function keys whose on-screen tap posts a real key press.
    private static bool IsFunctionKey(int vk) =>
        vk is VK_BACK or VK_TAB or VK_RETURN or VK_LEFT or VK_UP or VK_RIGHT or VK_DOWN;

    private static bool IsExtended(int vk) =>
        vk is VK_LEFT or VK_UP or VK_RIGHT or VK_DOWN;

    // MARK: - layer state

    /// The layer picker selects one sticky layer outright.
    private void SelectLayer(Layer l)
    {
        _stickyShift = l == Layer.Shift;
        _stickyAltGr = l == Layer.AltGr;
        _stickyCaps = l == Layer.Caps;
        RecomputeLayer();
    }

    /// Clicking an on-screen modifier cap toggles just that modifier.
    private void ToggleModifier(Layer layer)
    {
        switch (layer)
        {
            case Layer.Shift: _stickyShift = !_stickyShift; break;
            case Layer.AltGr: _stickyAltGr = !_stickyAltGr; break;
            case Layer.Caps: _stickyCaps = !_stickyCaps; break;
        }
        RecomputeLayer();
    }

    /// Collapse the modifier state into the single visible layer. Alt Gr beats
    /// Shift beats Caps, matching how a real keyboard resolves overlaps.
    private void RecomputeLayer()
    {
        _layer = ((_altGrHeld && !_altGrConsumed) || _stickyAltGr) ? Layer.AltGr
               : (_shiftHeld || _stickyShift) ? Layer.Shift
               : (_capsHardware || _stickyCaps) ? Layer.Caps
               : Layer.Base;
        UpdateAll();
    }

    // MARK: - physical keyboard

    /// Invoked synchronously by the hook for every real key. Returns true to
    /// swallow the physical key so we can substitute the Nailangs glyph.
    private bool OnPhysicalKey(int vk, bool down, bool repeat)
    {
        switch (vk)
        {
            case VK_LSHIFT: case VK_RSHIFT: case VK_SHIFT:
                _shiftHeld = _monitor.IsPressed(VK_LSHIFT) || _monitor.IsPressed(VK_RSHIFT) || _monitor.IsPressed(VK_SHIFT);
                RecomputeLayer();
                return false;                               // never swallow modifiers
            case VK_RMENU:                                  // Alt Gr (right Alt)
                _altGrHeld = down;
                if (!down) _altGrConsumed = false;          // released: the next press counts again
                RecomputeLayer();
                return false;
            case VK_CAPITAL:
                if (down && !repeat) { _capsHardware = !_capsHardware; RecomputeLayer(); }
                return false;
            case VK_LCONTROL: case VK_RCONTROL: case VK_CONTROL:
            case VK_LMENU: case VK_MENU: case VK_LWIN: case VK_RWIN:
                return false;                               // modifiers alone don't move the caret
        }

        // Leave real shortcuts (Ctrl / left Alt) to the system; the RIGHT Alt is
        // our Alt Gr and must still remap.
        if (ShortcutActive)
        {
            if (down) ForgetLastGlyph();                    // shortcut may edit the text
            return false;
        }

        if (!_vkMap.TryGetValue(vk, out var key))
        {
            // Backspace/Enter/arrows/… delete or move past the letter we remember,
            // so nothing is left for a tone to attach to.
            if (down) ForgetLastGlyph();
            return false;
        }
        if (down) HandleTap(key);
        return true;   // swallow both down and up of the remapped key
    }

    /// Ctrl or left-Alt held for a genuine shortcut (Ctrl+C, Alt+Tab, …). Alt Gr
    /// also raises a phantom left-Ctrl, so it must not count as a shortcut.
    private bool ShortcutActive =>
        !_altGrHeld && (_monitor.IsPressed(VK_LCONTROL)
            || _monitor.IsPressed(VK_RCONTROL) || _monitor.IsPressed(VK_LMENU));

    /// A click can drop the caret anywhere, so the letter we remember is no longer
    /// the one in front of it. Clicking our OWN windows is the exception — the
    /// pop-out is non-activating and types nothing by itself.
    private void OnMouseDown(bool ownWindow)
    {
        if (!ownWindow) ForgetLastGlyph();
    }

    private void ForgetLastGlyph()
    {
        if (_lastGlyph == null) return;
        _lastGlyph = null;
        UpdateAll();
    }

    // MARK: - typing (shared by mouse and hardware)

    /// Insert the character this key produces on the current layer into the
    /// focused app. Tone keys instead mark the letter already typed.
    private void HandleTap(KeyCap key)
    {
        // A tone/dead key (Alt Gr layer) marks what came BEFORE it: the letter is
        // chosen first, the tone second.
        if (_layer == Layer.AltGr && key.Dead is { } dk)
        {
            ApplyTone(dk);
            return;
        }

        string glyph = Display(key);
        string input = glyph == "space" ? " " : glyph;
        if (input.Length == 0) return;

        KeyInput.Type(input);
        // Space ends the run — a tone can't reach back past it. Everything else
        // becomes the next tone target (Compose() rejects marks that don't belong).
        _lastGlyph = input == " " ? null : input;
        UpdateAll();
    }

    /// Put `dead`'s mark on the letter that was just typed, by taking that letter
    /// back and re-typing it marked. When there is nothing to mark — or the mark
    /// isn't allowed on that letter — emit the bare mark instead, exactly as the
    /// installed layout does for an unmatched dead key.
    private void ApplyTone(DeadKey dead)
    {
        var composer = DeadKeyComposer.Shared;

        if (_lastGlyph is not { } previous
            || composer.Compose(dead.State, composer.BaseGlyph(previous)) is not { } composed)
        {
            KeyInput.Type(composer.Terminator(dead.State));
            return;
        }

        KeyInput.SendKey(VK_BACK);    // ⌫ — take back the unmarked letter
        KeyInput.Type(composed);
        _lastGlyph = composed;        // a second tone replaces this mark

        // Alt Gr was only needed to reach the tone: drop the layer the moment the
        // mark lands, whether it was clicked (sticky) or is still physically held.
        _stickyAltGr = false;
        _altGrConsumed = true;
        RecomputeLayer();
    }

    /// Would tapping this tone key actually mark the current letter? Drives the
    /// on-screen hint, so the applicable tones are visible before committing.
    private bool CanApplyTone(KeyCap key)
    {
        if (key.Dead is not { } dead || _lastGlyph is not { } previous) return false;
        var composer = DeadKeyComposer.Shared;
        return composer.Compose(dead.State, composer.BaseGlyph(previous)) != null;
    }

    private string Display(KeyCap key) => _layer switch
    {
        Layer.Base => key.Base,
        Layer.Shift => key.Shift,
        Layer.Caps => key.Caps,
        Layer.AltGr => key.Dead?.Mark ?? key.AltGr,
        _ => key.Base
    };

    // MARK: - appearance

    private string LayerHint => _layer switch
    {
        Layer.Base => "No modifier — the everyday Nailangs letters.",
        Layer.Shift => "Hold Shift — standard symbols and uppercase letters.",
        Layer.Caps => "Caps Lock on — uppercase Nailangs letters.",
        Layer.AltGr => "Right Alt (Alt Gr) — the green keys are tones. Type the letter first, then tap a tone to mark it: " +
                       "high ´, low `, mid/down-step ¯ (Igbo, Hausa, Yorùbá). Left Alt stays normal.",
        _ => ""
    };

    private void UpdateAll()
    {
        _hint.Text = LayerHint;

        if (Interactive && _lastGlyph is { } target)
        {
            _toneBannerText.Text = $"“{target}” typed — tap a tone on the Alt Gr layer to mark it";
            _toneBanner.Visibility = Visibility.Visible;
        }
        else _toneBanner.Visibility = Visibility.Collapsed;

        foreach (var (l, btn) in _layerButtons) btn.IsChecked = l == _layer;
        foreach (var v in _visuals) UpdateKey(v);
    }

    private static readonly SolidColorBrush Hairline = new(Color.FromArgb(90, 128, 128, 128));
    private static readonly SolidColorBrush DeadBorder = new(Color.FromArgb(178, 56, 130, 59));
    private static readonly SolidColorBrush LitFill = new(Color.FromArgb(217, 56, 130, 59));
    private static readonly SolidColorBrush WhiteSoft = new(Color.FromArgb(230, 255, 255, 255));
    private static readonly SolidColorBrush Muted = SystemColors.GrayTextBrush;

    private void UpdateKey(KeyVisual v)
    {
        var key = v.Key;
        bool pressed = Interactive && key.Vk is { } vk && _monitor.IsPressed(vk);

        if (key.IsModifier)
        {
            var mapped = LayerForModifier(key.Label ?? "");
            bool active = mapped is { } ml && _layer == ml;
            bool lit = active || pressed;
            if (key.Accent)
            {
                v.Border.Background = Brand.GreenGradient();
                v.Border.BorderBrush = Brand.Primary;
            }
            else
            {
                v.Border.Background = lit ? LitFill : SystemColors.WindowBrush;
                v.Border.BorderBrush = lit ? Brand.Primary : Hairline;
                v.Center.Foreground = lit ? Brushes.White : Muted;
            }
            v.Border.BorderThickness = new Thickness(lit ? 2 : 1);
            return;
        }

        bool isDead = key.Dead != null;
        // A tone key only advertises itself (green) while the Alt Gr layer is up.
        bool showDead = isDead && _layer == Layer.AltGr;
        // On the Alt Gr layer, light up the tones that the letter just typed can
        // actually take — the mark lands on it the moment the key is tapped.
        bool ready = Interactive && showDead && CanApplyTone(key);
        bool highlight = pressed || ready;

        v.Border.Background = highlight ? LitFill
            : (showDead ? Brand.AccentSoft : SystemColors.ControlBrush);
        v.Border.BorderBrush = highlight ? Brand.Primary : (showDead ? DeadBorder : Hairline);
        v.Border.BorderThickness = new Thickness(highlight || showDead ? 2 : 1);
        v.Scale.ScaleX = v.Scale.ScaleY = pressed ? 0.94 : 1;

        bool isSpace = key.Base == "space";
        v.Center.Foreground = highlight ? Brushes.White : (isSpace ? Muted : SystemColors.ControlTextBrush);
        if (!isSpace) v.Center.FontWeight = _layer == Layer.Caps ? FontWeights.SemiBold : FontWeights.Medium;

        if (v.TopLeft != null)
        {
            bool emph = (_layer == Layer.Shift && !v.TopLeftIsAlt) || (_layer == Layer.AltGr && v.TopLeftIsAlt);
            bool isMark = v.TopLeftIsAlt && showDead;
            v.TopLeft.Foreground = highlight ? WhiteSoft : (isMark || emph ? Brand.Primary : Muted);
            v.TopLeft.FontWeight = emph ? FontWeights.Bold : FontWeights.Normal;
        }
        if (v.BottomRight != null)
        {
            bool emph = _layer == Layer.AltGr;
            v.BottomRight.Foreground = highlight ? WhiteSoft : (showDead || emph ? Brand.Primary : Muted);
            v.BottomRight.FontWeight = emph ? FontWeights.Bold : FontWeights.Normal;
        }

        if (Interactive) v.Border.ToolTip = $"Click to type {Display(key)}";
    }
}
