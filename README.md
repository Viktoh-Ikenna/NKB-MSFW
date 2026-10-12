# Nailangs Keyboard — Windows

The Windows port of the Nailangs keyboard (macOS version: `../NKB-NEW-MAC`).
Same layers, special letters and nine dead keys; Alt Gr = **right Alt**.

Ships two ways:
1. **The layout** `layout/Nailangs.klc`, built into an installer with Microsoft
   Keyboard Layout Creator (MSKLC), and
2. **A companion app** (`Nailangs/`, WPF / .NET 8) with the same three tabs as
   the Mac app:
   - **Install** — runs the bundled MSKLC `setup.exe` (elevated), shows install
     status and next steps, links to Language Settings and the layout folder.
   - **Keyboard** — all four layers (Base / Shift / Caps / Alt Gr) and a
     **Pop Out** button for an always-on-top keyboard that types into the app
     you're using without taking focus.
   - **Reference** — the dead keys, special letters, test string and notes.

## Tones in the pop-out keyboard

As on the Mac: **type the letter first, then the tone.** Type `ẹ`, then Alt Gr +
O (acute) → the letter is taken back and re-typed as `ẹ́`. A second tone replaces
the first. Tones only go on the letters the NKB "Signs & Marks" table allows
(e.g. bar only on i/u). Clicking elsewhere, Space, Backspace, Enter or arrows
clear the letter waiting for a tone.

## Building the layout installer

`layout/Nailangs.klc` is generated from the Mac layout. Don't edit it by hand:

```sh
cd layout
python3 make_klc.py ../Nailangs/Resources/Nailangs.keylayout Nailangs.klc
```

Then on Windows: open `Nailangs.klc` in **MSKLC 1.4** → *Project → Build DLL and
Setup Package*, and copy the output (`setup.exe` + the MSIs) into `layout/`.
The app's build copies `layout/` next to `Nailangs.exe`, where Install looks for
`setup.exe`.

### Windows layout limits

- A Windows dead key can only produce **one** character. 68 Mac combinations
  are letter + combining mark (e.g. ẹ́, ọ̀, ɔ̃, a̧), so the installed Windows
  layout types the bare mark followed by the letter for those. `make_klc.py`
  prints the full list. **The pop-out keyboard does produce all of them.**
- To type those with the installed layout, use the Windows-only **combining
  tones**: type the letter, then **Alt Gr + Shift + the tone key** (I ` · O ´ ·
  P ~ · J ^ · K ¨ · L ¸ · U ¯). Example: `ẹ` then Alt Gr+Shift+O → `ẹ́`.
  Stroke (Y) and bar (H) have no combining form.
- Caps Lock can't produce a ligature, so Caps + `` ` `` gives `gb`, not `Gb`.

## Building the app

```sh
cd Nailangs
dotnet build -c Release      # needs the .NET 8 SDK; builds on Windows
```

`Nailangs/Resources/Nailangs.keylayout` must match the Mac copy. The pop-out
keyboard reads its dead-key table from it at runtime.
