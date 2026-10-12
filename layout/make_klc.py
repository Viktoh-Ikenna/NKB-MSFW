#!/usr/bin/env python3
"""Generate Nailangs.klc (Microsoft Keyboard Layout Creator source) from the
macOS Nailangs.keylayout, so the Windows layout types exactly what the Mac does.

    python3 make_klc.py ../Nailangs/Resources/Nailangs.keylayout Nailangs.klc

Then open Nailangs.klc in MSKLC 1.4 on Windows → Project → Build DLL and Setup
Package, and copy the output (setup.exe + MSIs) into this folder.

Known Windows limits (reported when the script runs):
  * A dead key can only produce ONE UTF-16 character, so compositions the Mac
    builds as letter + combining mark (e.g. a + U+0327) are left out — the bare
    mark is typed first, then the letter, as with any undefined combination.
    Alt Gr + Shift + the tone key types the mark as a combining character
    instead, AFTER the letter (ẹ then Alt Gr+Shift+O → ẹ́).
  * Caps Lock cannot produce a ligature, so Caps + ` gives "gb" (not "Gb").
"""
import sys
import xml.etree.ElementTree as ET

# Mac virtual key code -> (Windows scancode, VK name). Same physical keys as
# Nailangs/Model/KeyboardData.cs uses for highlighting and remapping.
KEYS = {
    50: ("29", "OEM_3"),
    18: ("02", "1"), 19: ("03", "2"), 20: ("04", "3"), 21: ("05", "4"), 23: ("06", "5"),
    22: ("07", "6"), 26: ("08", "7"), 28: ("09", "8"), 25: ("0a", "9"), 29: ("0b", "0"),
    27: ("0c", "OEM_MINUS"), 24: ("0d", "OEM_PLUS"),
    12: ("10", "Q"), 13: ("11", "W"), 14: ("12", "E"), 15: ("13", "R"), 17: ("14", "T"),
    16: ("15", "Y"), 32: ("16", "U"), 34: ("17", "I"), 31: ("18", "O"), 35: ("19", "P"),
    33: ("1a", "OEM_4"), 30: ("1b", "OEM_6"),
    0: ("1e", "A"), 1: ("1f", "S"), 2: ("20", "D"), 3: ("21", "F"), 5: ("22", "G"),
    4: ("23", "H"), 38: ("24", "J"), 40: ("25", "K"), 37: ("26", "L"),
    41: ("27", "OEM_1"), 39: ("28", "OEM_7"), 42: ("2b", "OEM_5"),
    6: ("2c", "Z"), 7: ("2d", "X"), 8: ("2e", "C"), 9: ("2f", "V"), 11: ("30", "B"),
    45: ("31", "N"), 46: ("32", "M"), 43: ("33", "OEM_COMMA"), 47: ("34", "OEM_PERIOD"),
    44: ("35", "OEM_2"),
    49: ("39", "SPACE"),
    10: ("56", "OEM_102"),
}

# Alt Gr + Shift on a tone key types the tone as a COMBINING mark, to put after
# the letter. Covers what a Windows dead key can't (ẹ́, ọ̀, ɔ̃, …). Windows-only:
# the Mac layout's dead keys can already produce those. Stroke and bar are letter
# forms with no combining equivalent, so they're absent — same as the app's
# DeadKeyComposer.
COMBINING = {
    34: "\u0300",   # I  grave       (low tone)
    31: "\u0301",   # O  acute       (high tone)
    35: "\u0303",   # P  tilde
    38: "\u0302",   # J  circumflex
    40: "\u0308",   # K  diaeresis
    37: "\u0327",   # L  cedilla
    32: "\u0304",   # U  macron      (mid / down-step)
}

# keylayout map index -> meaning (see <modifierMap> in the keylayout)
BASE, SHIFT, CAPS, ALTGR = 0, 1, 2, 3


def load(path):
    root = ET.parse(path).getroot()
    actions, dead_next = {}, {}
    for a in root.iter("action"):
        whens = list(a.iter("when"))
        actions[a.get("id")] = {w.get("state"): w.get("output") for w in whens if w.get("output") is not None}
        for w in whens:
            if w.get("state") == "none" and w.get("next"):
                dead_next[a.get("id")] = w.get("next")   # dead key: <when state="none" next="grave"/>
    terminators = {w.get("state"): w.get("output") for w in root.find("terminators").iter("when")}

    layers = {}   # (index, code) -> ("out", text) | ("dead", state)
    for km in root.find("keyMapSet").iter("keyMap"):
        idx = int(km.get("index"))
        for k in km.iter("key"):
            code = int(k.get("code"))
            if k.get("output") is not None:
                layers[(idx, code)] = ("out", k.get("output"))
            elif k.get("action") in dead_next:
                layers[(idx, code)] = ("dead", dead_next[k.get("action")])
            else:
                layers[(idx, code)] = ("out", actions[k.get("action")]["none"])
    return actions, terminators, layers


def hx(ch):
    return f"{ord(ch):04x}"


def main(src, dst):
    actions, terminators, layers = load(src)
    ligatures, skipped, warnings = [], [], []

    def cell(idx, code, vk, col):
        v = layers.get((idx, code))
        if v is None:
            return "-1"
        kind, val = v
        if kind == "dead":
            return hx(terminators[val]) + "@"
        if len(val) == 1:
            return hx(val)
        ligatures.append((vk, col, val))
        return "%%"

    rows = []
    for code, (sc, vk) in sorted(KEYS.items(), key=lambda kv: int(kv[1][0], 16)):
        base = layers.get((BASE, code), ("out", ""))[1]
        shift = layers.get((SHIFT, code), ("out", ""))[1]
        caps = layers.get((CAPS, code), ("out", ""))[1]
        c0, c1 = cell(BASE, code, vk, 0), cell(SHIFT, code, vk, 1)
        c6 = cell(ALTGR, code, vk, 3)
        ctrl = "0020" if vk == "SPACE" else "-1"
        c7 = hx(COMBINING[code]) if code in COMBINING else "-1"
        if caps == base:
            rows.append(f"{sc}\t{vk}\t\t0\t{c0}\t{c1}\t{ctrl}\t{c6}\t{c7}\t// {base} {shift}")
        elif caps == shift:
            rows.append(f"{sc}\t{vk}\t\t1\t{c0}\t{c1}\t{ctrl}\t{c6}\t{c7}\t// {base} {shift}")
        elif len(caps) == 1:
            rows.append(f"{sc}\t{vk}\t\tSGCap\t{c0}\t{c1}\t{ctrl}\t{c6}\t{c7}\t// {base} {shift}")
            rows.append(f"-1\t-1\t\t0\t{hx(caps)}\t{c1}\t\t\t\t// caps: {caps}")
        else:
            warnings.append(f"Caps + {vk}: '{caps}' is a ligature; Caps Lock gives '{base}' instead")
            rows.append(f"{sc}\t{vk}\t\t0\t{c0}\t{c1}\t{ctrl}\t{c6}\t{c7}\t// {base} {shift}")
    rows.append("53\tDECIMAL\t\t0\t002e\t002e\t-1\t-1\t-1")

    # Dead keys: state -> {plain glyph: composed glyph}
    dead_sections = []
    for state, mark in terminators.items():
        entries = []
        for act in actions.values():
            plain, out = act.get("none"), act.get(state)
            if not plain or out is None or len(plain) != 1:
                continue
            if len(out) != 1:
                skipped.append(f"{state}: {plain} -> {out!r} (U+{' U+'.join(hx(c).upper() for c in out)})")
                continue
            entries.append(f"{hx(plain)}\t{hx(out)}\t// {plain} -> {out}")
        entries.append(f"0020\t{hx(mark)}\t// space -> {mark}")
        dead_sections.append(f"DEADKEY\t{hx(mark)}\r\n\r\n" + "\r\n".join(sorted(set(entries))) + "\r\n")

    names = {"grave": "Grave", "acute": "Acute", "tilde": "Tilde", "circum": "Circumflex",
             "diaer": "Diaeresis", "cedil": "Cedilla", "macron": "Macron", "stroke": "Stroke", "bar": "Bar"}

    out = []
    out.append('KBD\tNailangs\t"Nailangs"\r\n')
    out.append('COPYRIGHT\t"(c) 2026 Nailangs"\r\n')
    out.append('COMPANY\t"Nailangs"\r\n')
    out.append('LOCALENAME\t"en-US"\r\n')
    out.append('LOCALEID\t"00000409"\r\n')
    out.append('VERSION\t1.0\r\n')
    out.append('SHIFTSTATE\r\n\r\n0\t//Column 4\r\n1\t//Column 5 : Shft\r\n2\t//Column 6 :       Ctrl\r\n'
               '6\t//Column 7 :       Ctrl Alt\r\n7\t//Column 8 : Shft  Ctrl Alt\r\n')
    out.append("LAYOUT\t\t;an extra '@' at the end is a dead key\r\n\r\n"
               "//SC\tVK_\t\tCap\t0\t1\t2\t6\t7\r\n//--\t----\t\t----\t----\t----\t----\t----\t----\r\n\r\n"
               + "\r\n".join(rows) + "\r\n")
    if ligatures:
        lig = ["LIGATURE\r\n", "//VK_\tMod#\tChar0\tChar1\tChar2\tChar3", "//----\t\t----\t----\t----\t----\t----\r\n"]
        for vk, col, val in ligatures:
            lig.append(f"{vk}\t{col}\t" + "\t".join(hx(c) for c in val) + f"\t// {val}")
        out.append("\r\n".join(lig) + "\r\n")
    out.extend(dead_sections)
    out.append(KEYNAMES)
    out.append("KEYNAME_DEAD\r\n\r\n" + "\r\n".join(
        f'{hx(m)}\t"{names.get(s, s.title())}"' for s, m in terminators.items()) + "\r\n")
    out.append("DESCRIPTIONS\r\n\r\n0409\tNailangs\r\n")
    out.append("LANGUAGENAMES\r\n\r\n0409\tEnglish (United States)\r\n")
    out.append("ENDKBD\r\n")

    with open(dst, "w", encoding="utf-16", newline="") as f:   # MSKLC reads UTF-16 LE + BOM
        f.write("\r\n".join(out))

    print(f"wrote {dst}: {len(rows)} key rows, {len(ligatures)} ligatures, {len(dead_sections)} dead keys")
    for w in warnings:
        print("  note:", w)
    if skipped:
        print(f"  {len(skipped)} dead-key results need a combining mark (not possible in a Windows dead key):")
        for s in skipped:
            print("   ", s)


KEYNAMES = "\r\n".join([
    "KEYNAME", "",
    "01\tEsc", "0e\tBackspace", "0f\tTab", "1c\tEnter", "1d\tCtrl", "2a\tShift",
    '36\t"Right Shift"', '37\t"Num *"', "38\tAlt", "39\tSpace", '3a\t"Caps Lock"',
    "3b\tF1", "3c\tF2", "3d\tF3", "3e\tF4", "3f\tF5", "40\tF6", "41\tF7", "42\tF8", "43\tF9", "44\tF10",
    "45\tPause", '46\t"Scroll Lock"', '47\t"Num 7"', '48\t"Num 8"', '49\t"Num 9"', '4a\t"Num -"',
    '4b\t"Num 4"', '4c\t"Num 5"', '4d\t"Num 6"', '4e\t"Num +"', '4f\t"Num 1"', '50\t"Num 2"',
    '51\t"Num 3"', '52\t"Num 0"', '53\t"Num Del"', '54\t"Sys Req"', "57\tF11", "58\tF12",
    "7c\tF13", "7d\tF14", "7e\tF15", "7f\tF16", "80\tF17", "81\tF18", "82\tF19", "83\tF20",
    "84\tF21", "85\tF22", "86\tF23", "87\tF24", "",
    "KEYNAME_EXT", "",
    '1c\t"Num Enter"', '1d\t"Right Ctrl"', '35\t"Num /"', '37\t"Prnt Scrn"', '38\t"Right Alt"',
    '45\t"Num Lock"', "46\tBreak", "47\tHome", "48\tUp", '49\t"Page Up"', "4b\tLeft", "4d\tRight",
    "4f\tEnd", "50\tDown", '51\t"Page Down"', "52\tInsert", "53\tDelete", "54\t<00>", "56\tHelp",
    '5b\t"Left Windows"', '5c\t"Right Windows"', "5d\tApplication", "",
]) + "\r\n"


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../Nailangs/Resources/Nailangs.keylayout",
         sys.argv[2] if len(sys.argv) > 2 else "Nailangs.klc")
