using System.Reflection;
using System.Xml;

namespace Nailangs;

/// Dead-key composition, derived from the embedded Nailangs.keylayout so the
/// pop-out keyboard composes exactly what the installed layout would. The keylayout models dead keys as
/// a state machine: a dead key sets a state ("grave", …) and the next letter's
/// action emits the combined glyph. We turn its <actions>/<terminators> into a
/// [state][producedGlyph] → output lookup.
public sealed class DeadKeyComposer
{
    public static readonly DeadKeyComposer Shared = new();

    /// _compose["grave"]["a"] == "à"
    private readonly Dictionary<string, Dictionary<string, string>> _compose = new();
    /// The inverse: _base["à"] == "a". Lets a second tone REPLACE the first
    /// instead of stacking on it, now that marks are applied after the letter.
    private readonly Dictionary<string, string> _base = new();
    /// The mark emitted when a dead key is followed by something it can't combine
    /// with (e.g. _terminators["grave"] == "`").
    private readonly Dictionary<string, string> _terminators = new();

    private DeadKeyComposer() => Load();

    // MARK: - Design: which letters each mark may sit on

    /// Base letters as they appear on the NKB layout, both cases. Escapes are used
    /// for the non-ASCII ones so the sets are unambiguous.
    private static class Letters
    {
        public const string A = "aA", E = "eE", I = "iI", O = "oO", U = "uU";
        public const string EDot  = "\u1EB9\u1EB8";   // ẹ Ẹ
        public const string ODot  = "\u1ECD\u1ECC";   // ọ Ọ
        public const string IDot  = "\u1ECB\u1ECA";   // ị Ị
        public const string UDot  = "\u1EE5\u1EE4";   // ụ Ụ
        public const string Schwa = "\u01DD\u018E";   // ǝ Ǝ
        public const string OpenO = "\u0254\u0186";   // ɔ Ɔ
        public const string OpenE = "\u025B\u0190";   // ɛ Ɛ
        public const string N = "nN", M = "mM", R = "rR", C = "cC";
        public const string S = "sS", T = "tT", G = "gG", K = "kK", L = "lL", D = "dD";
        public const string Y = "yY", W = "wW";
    }

    private static HashSet<string> LetterSet(params string[] groups)
        => string.Concat(groups).Select(ch => ch.ToString()).ToHashSet();

    /// From the NKB "Signs & Marks" design table — the EXACT letters each mark is
    /// allowed on. A mark is NOT valid on every letter (e.g. bar only on i and u),
    /// so anything outside its set does not compose.
    private static readonly Dictionary<string, HashSet<string>> AllowedLetters = new()
    {
        ["grave"]  = LetterSet(Letters.A, Letters.E, Letters.EDot, Letters.I, Letters.O, Letters.ODot, Letters.U, Letters.UDot, Letters.IDot, Letters.Schwa, Letters.OpenO, Letters.N, Letters.M),
        ["acute"]  = LetterSet(Letters.A, Letters.E, Letters.EDot, Letters.I, Letters.O, Letters.ODot, Letters.U, Letters.UDot, Letters.IDot, Letters.Schwa, Letters.OpenO, Letters.N, Letters.M),
        ["tilde"]  = LetterSet(Letters.A, Letters.E, Letters.EDot, Letters.Schwa, Letters.I, Letters.O, Letters.ODot, Letters.U, Letters.IDot, Letters.OpenO, Letters.N, Letters.R, Letters.C),
        ["circum"] = LetterSet(Letters.A, Letters.E, Letters.EDot, Letters.I, Letters.O, Letters.ODot, Letters.U, Letters.OpenO),
        ["diaer"]  = LetterSet(Letters.A, Letters.E, Letters.I, Letters.O, Letters.U, Letters.OpenO, Letters.Y, Letters.W),
        ["cedil"]  = LetterSet(Letters.A, Letters.E, Letters.OpenE, Letters.Schwa, Letters.I, Letters.O, Letters.OpenO, Letters.U, Letters.S, Letters.T, Letters.C, Letters.M, Letters.G, Letters.K, Letters.L, Letters.N, Letters.R),
        ["macron"] = LetterSet(Letters.A, Letters.E, Letters.EDot, Letters.Schwa, Letters.I, Letters.O, Letters.U, Letters.IDot, Letters.OpenO),
        ["stroke"] = LetterSet(Letters.I, Letters.O, Letters.U, Letters.D, Letters.L, Letters.T),
        ["bar"]    = LetterSet(Letters.I, Letters.U),
    };

    /// Combining diacritic for the accent / tone marks, used to fill any gap the
    /// layout table left INSIDE the allowed set (stroke & bar are letter forms the
    /// table handles directly, so they're intentionally absent here).
    private static readonly Dictionary<string, char> CombiningMark = new()
    {
        ["grave"]  = '\u0300',   // à  low tone
        ["acute"]  = '\u0301',   // á  high tone
        ["tilde"]  = '\u0303',   // ã
        ["circum"] = '\u0302',   // â
        ["diaer"]  = '\u0308',   // ä
        ["cedil"]  = '\u0327',   // ç
        ["macron"] = '\u0304',   // ā  (Igbo mid / down-step)
    };

    /// The composed glyph for `glyph` while `state` is armed, or null when the mark
    /// is not allowed on that letter (caller then emits the standalone mark).
    public string? Compose(string state, string glyph)
    {
        // Enforce the design first: the mark must be permitted on this letter.
        if (!AllowedLetters.TryGetValue(state, out var allowed) || !allowed.Contains(glyph)) return null;
        if (_compose.TryGetValue(state, out var m) && m.TryGetValue(glyph, out var o)) return o; // layout's precomposed form
        if (!CombiningMark.TryGetValue(state, out var mark)) return null;                       // stroke/bar: table only
        var output = (glyph + mark).Normalize(System.Text.NormalizationForm.FormC);
        _base[output] = glyph;   // gap-filler: record the inverse so re-toning works
        return output;
    }

    /// The plain letter `glyph` was marked from, so pressing a second tone replaces
    /// the mark rather than refusing (á + grave → à). Anything we did not compose
    /// ourselves is already a base letter — returned unchanged.
    ///
    /// Deliberately does NOT fall back to Unicode canonical decomposition: that
    /// would strip the dot off base letters like ẹ / ọ (they decompose to e / o
    /// plus a combining dot), silently dropping part of the letter.
    public string BaseGlyph(string glyph)
        => _base.TryGetValue(glyph, out var b) ? b : glyph;

    public string Terminator(string state)
        => _terminators.TryGetValue(state, out var o) ? o : "";

    private void Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Nailangs.keylayout");
        if (stream == null) return;

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,      // don't fetch the external DTD
            XmlResolver = null
        };
        using var reader = XmlReader.Create(stream, settings);

        // actionId -> (state -> output)
        var actions = new Dictionary<string, Dictionary<string, string>>();
        bool inActions = false, inTerminators = false;
        string? currentAction = null;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                switch (reader.Name)
                {
                    case "actions": inActions = true; break;
                    case "terminators": inTerminators = true; break;
                    case "action":
                        currentAction = reader.GetAttribute("id");
                        if (currentAction != null) actions[currentAction] = new();
                        break;
                    case "when":
                        var state = reader.GetAttribute("state");
                        var output = reader.GetAttribute("output") ?? "";
                        if (state == null) break;
                        if (inTerminators) _terminators[state] = output;
                        else if (inActions && currentAction != null) actions[currentAction][state] = output;
                        break;
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                switch (reader.Name)
                {
                    case "actions": inActions = false; break;
                    case "terminators": inTerminators = false; break;
                    case "action": currentAction = null; break;
                }
            }
        }

        // An action's "none" output is the plain character; every other state is a
        // composition keyed by that plain character.
        foreach (var states in actions.Values)
        {
            if (!states.TryGetValue("none", out var baseGlyph) || baseGlyph.Length == 0) continue;
            foreach (var (state, outp) in states)
            {
                if (state == "none") continue;
                if (!_compose.TryGetValue(state, out var map)) _compose[state] = map = new();
                map[baseGlyph] = outp;
                _base[outp] = baseGlyph;
            }
        }
    }
}
