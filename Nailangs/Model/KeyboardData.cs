namespace Nailangs;

public enum Layer { Base, Shift, Caps, AltGr }

public sealed class DeadKey
{
    public string Name { get; init; } = "";        // human label shown in the UI, e.g. "circumflex"
    public string Mark { get; init; } = "";        // the diacritic terminator glyph, e.g. "`"
    public string HostLabel { get; init; } = "";   // the host key label, e.g. "U"
    public string Sample { get; init; } = "";      // a sample result, e.g. "à ← a"

    /// The matching `state=` name in Nailangs.keylayout. Usually the display name,
    /// but three of them are abbreviated in the layout ("circum", "diaer", "cedil")
    /// — composition looks marks up by THIS, so it must not drift from the file.
    public string State { get => _state ?? Name; init => _state = value; }
    private readonly string? _state;
}

public static class DeadKeys
{
    public static readonly DeadKey Grave  = new() { Name = "grave",                        Mark = "`", HostLabel = "I", Sample = "à ← a" };
    public static readonly DeadKey Acute  = new() { Name = "acute",                        Mark = "´", HostLabel = "O", Sample = "é ← e" };
    public static readonly DeadKey Tilde  = new() { Name = "tilde",                        Mark = "~", HostLabel = "P", Sample = "ñ ← n" };
    public static readonly DeadKey Circum = new() { Name = "circumflex", State = "circum", Mark = "^", HostLabel = "J", Sample = "ô ← o" };
    public static readonly DeadKey Diaer  = new() { Name = "diaeresis",  State = "diaer",  Mark = "¨", HostLabel = "K", Sample = "ü ← u" };
    public static readonly DeadKey Cedil  = new() { Name = "cedilla",    State = "cedil",  Mark = "¸", HostLabel = "L", Sample = "ç ← c" };
    public static readonly DeadKey Macron = new() { Name = "macron",                       Mark = "¯", HostLabel = "U", Sample = "ā ← a" };
    public static readonly DeadKey Stroke = new() { Name = "stroke",                       Mark = "/", HostLabel = "Y", Sample = "ø ← o" };
    public static readonly DeadKey Bar    = new() { Name = "bar",                          Mark = "|", HostLabel = "H", Sample = "ɨ ← i" };

    public static readonly DeadKey[] All = { Grave, Acute, Tilde, Circum, Diaer, Cedil, Macron, Stroke, Bar };
}

/// One visible key cap. Glyphs mirror Nailangs.keylayout exactly.
public sealed class KeyCap
{
    public string Base = "", Shift = "", Caps = "", AltGr = "";
    public DeadKey? Dead;
    public double Width = 1;
    public int? Vk;          // Windows virtual-key code (highlight / typing)
    public string? Label;    // set => modifier/function key (drawn muted, no legends)
    public bool Accent;      // brand-green key (the logo key)

    public bool IsModifier => Label != null;

    public static KeyCap Key(string b, string s, string c, string a = "",
                             DeadKey? dead = null, double w = 1, int? vk = null)
        => new() { Base = b, Shift = s, Caps = c, AltGr = a, Dead = dead, Width = w, Vk = vk };

    public static KeyCap Mod(string label, double w = 1, int? vk = null, bool accent = false)
        => new() { Label = label, Width = w, Vk = vk, Accent = accent };
}

public sealed class SpecialLetter
{
    public string Lower = "", Upper = "", Key = "", Codepoint = "";
}

public static class NailangsData
{
    // Windows virtual-key codes used below
    private const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_CAPITAL = 0x14,
        VK_SPACE = 0x20, VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2,
        VK_LMENU = 0xA4, VK_RMENU = 0xA5, VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
        VK_OEM_3 = 0xC0, VK_OEM_MINUS = 0xBD, VK_OEM_PLUS = 0xBB, VK_OEM_4 = 0xDB, VK_OEM_6 = 0xDD,
        VK_OEM_1 = 0xBA, VK_OEM_7 = 0xDE, VK_OEM_5 = 0xDC, VK_OEM_COMMA = 0xBC, VK_OEM_PERIOD = 0xBE,
        VK_OEM_2 = 0xBF, VK_OEM_102 = 0xE2;

    /// Full ISO keyboard incl. the surrounding modifier / function keys.
    public static readonly KeyCap[][] Rows =
    {
        // number row
        new[]
        {
            KeyCap.Key("gb", "₦", "Gb", "€", vk: VK_OEM_3),
            KeyCap.Key("ɓ", "!", "Ɓ", "1", vk: '1'),
            KeyCap.Key("ɗ", "\"", "Ɗ", "2", vk: '2'),
            KeyCap.Key("ǝ", "£", "Ǝ", "3", vk: '3'),
            KeyCap.Key("ƙ", "$", "Ƙ", "4", vk: '4'),
            KeyCap.Key("ƴ", "%", "Ƴ", "5", vk: '5'),
            KeyCap.Key("ị", "^", "Ị", "6", vk: '6'),
            KeyCap.Key("ṅ", "&", "Ṅ", "7", vk: '7'),
            KeyCap.Key("ọ", "*", "Ọ", "8", vk: '8'),
            KeyCap.Key("ụ", "(", "Ụ", "9", vk: '9'),
            KeyCap.Key("ẹ", ")", "Ẹ", "0", vk: '0'),
            KeyCap.Key("ṣ", "_", "Ṣ", "-", vk: VK_OEM_MINUS),
            KeyCap.Key("ɍ", "=", "Ɍ", "+", vk: VK_OEM_PLUS),
            KeyCap.Mod("⌫ Backspace", 2.0, VK_BACK),
        },
        // top row
        new[]
        {
            KeyCap.Mod("⇥ Tab", 1.6, VK_TAB),
            KeyCap.Key("q", "Q", "Q", "¬", vk: 'Q'),
            KeyCap.Key("w", "W", "W", "¦", vk: 'W'),
            KeyCap.Key("e", "E", "E", vk: 'E'),
            KeyCap.Key("r", "R", "R", vk: 'R'),
            KeyCap.Key("t", "T", "T", vk: 'T'),
            KeyCap.Key("y", "Y", "Y", "/", DeadKeys.Stroke, vk: 'Y'),
            KeyCap.Key("u", "U", "U", "¯", DeadKeys.Macron, vk: 'U'),
            KeyCap.Key("i", "I", "I", "`", DeadKeys.Grave, vk: 'I'),
            KeyCap.Key("o", "O", "O", "´", DeadKeys.Acute, vk: 'O'),
            KeyCap.Key("p", "P", "P", "~", DeadKeys.Tilde, vk: 'P'),
            KeyCap.Key("ŋ", "{", "Ŋ", "[", vk: VK_OEM_4),
            KeyCap.Key("ɔ", "}", "Ɔ", "]", vk: VK_OEM_6),
        },
        // home row
        new[]
        {
            KeyCap.Mod("⇪ Caps Lock", 1.9, VK_CAPITAL),
            KeyCap.Key("a", "A", "A", vk: 'A'),
            KeyCap.Key("s", "S", "S", "ẞ", vk: 'S'),
            KeyCap.Key("d", "D", "D", vk: 'D'),
            KeyCap.Key("f", "F", "F", vk: 'F'),
            KeyCap.Key("g", "G", "G", vk: 'G'),
            KeyCap.Key("h", "H", "H", "|", DeadKeys.Bar, vk: 'H'),
            KeyCap.Key("j", "J", "J", "^", DeadKeys.Circum, vk: 'J'),
            KeyCap.Key("k", "K", "K", "¨", DeadKeys.Diaer, vk: 'K'),
            KeyCap.Key("l", "L", "L", "¸", DeadKeys.Cedil, vk: 'L'),
            KeyCap.Key("æ", ":", "Æ", vk: VK_OEM_1),
            KeyCap.Key("œ", "@", "Œ", vk: VK_OEM_7),
            KeyCap.Key("ɲ", "#", "Ɲ", "~", vk: VK_OEM_5),
            KeyCap.Mod("⏎ Enter", 1.5, VK_RETURN),
        },
        // bottom row
        new[]
        {
            KeyCap.Mod("⇧ Shift", 2.5, VK_LSHIFT),
            KeyCap.Key("z", "Z", "Z", vk: 'Z'),
            KeyCap.Key("x", "X", "X", vk: 'X'),
            KeyCap.Key("c", "C", "C", vk: 'C'),
            KeyCap.Key("v", "V", "V", vk: 'V'),
            KeyCap.Key("b", "/", "B", vk: 'B'),
            KeyCap.Key("n", ";", "N", vk: 'N'),
            KeyCap.Key("m", "'", "M", vk: 'M'),
            KeyCap.Key(",", "<", ",", vk: VK_OEM_COMMA),
            KeyCap.Key(".", ">", ".", vk: VK_OEM_PERIOD),
            KeyCap.Key("ɛ", "?", "Ɛ", "ß", vk: VK_OEM_2),
            KeyCap.Mod("⇧ Shift", 2.5, VK_RSHIFT),
        },
        // space row
        new[]
        {
            KeyCap.Mod("Ctrl", 1.3, VK_LCONTROL),
            KeyCap.Mod("fn", 1.0),
            KeyCap.Mod("Nailangs", 1.1, accent: true),
            KeyCap.Mod("⌥ Alt", 1.2, VK_LMENU),
            KeyCap.Key("space", "space", "space", " ", w: 5.4, vk: VK_SPACE),
            KeyCap.Mod("Alt Gr", 1.4, VK_RMENU),
            KeyCap.Key("\\", "|", "\\", w: 1.0, vk: VK_OEM_102),
            KeyCap.Mod("◀", 0.95, VK_LEFT),
            KeyCap.Mod("▲", 0.95, VK_UP),
            KeyCap.Mod("▼", 0.95, VK_DOWN),
            KeyCap.Mod("▶", 0.95, VK_RIGHT),
        },
    };

    public static readonly SpecialLetter[] Specials =
    {
        new() { Lower = "ɓ", Upper = "Ɓ", Key = "1", Codepoint = "U+0253" },
        new() { Lower = "ɗ", Upper = "Ɗ", Key = "2", Codepoint = "U+0257" },
        new() { Lower = "ǝ", Upper = "Ǝ", Key = "3", Codepoint = "U+01DD" },
        new() { Lower = "ƙ", Upper = "Ƙ", Key = "4", Codepoint = "U+0199" },
        new() { Lower = "ƴ", Upper = "Ƴ", Key = "5", Codepoint = "U+01B4" },
        new() { Lower = "ị", Upper = "Ị", Key = "6", Codepoint = "U+1ECB" },
        new() { Lower = "ṅ", Upper = "Ṅ", Key = "7", Codepoint = "U+1E45" },
        new() { Lower = "ọ", Upper = "Ọ", Key = "8", Codepoint = "U+1ECD" },
        new() { Lower = "ụ", Upper = "Ụ", Key = "9", Codepoint = "U+1EE5" },
        new() { Lower = "ẹ", Upper = "Ẹ", Key = "0", Codepoint = "U+1EB9" },
        new() { Lower = "ṣ", Upper = "Ṣ", Key = "-", Codepoint = "U+1E63" },
        new() { Lower = "ɍ", Upper = "Ɍ", Key = "=", Codepoint = "U+024D" },
        new() { Lower = "ŋ", Upper = "Ŋ", Key = "[", Codepoint = "U+014B" },
        new() { Lower = "ɔ", Upper = "Ɔ", Key = "]", Codepoint = "U+0254" },
        new() { Lower = "æ", Upper = "Æ", Key = ";", Codepoint = "U+00E6" },
        new() { Lower = "œ", Upper = "Œ", Key = "'", Codepoint = "U+0153" },
        new() { Lower = "ɲ", Upper = "Ɲ", Key = "#", Codepoint = "U+0272" },
        new() { Lower = "ɛ", Upper = "Ɛ", Key = "/", Codepoint = "U+025B" },
    };
}
