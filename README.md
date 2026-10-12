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

## Microsoft Store build

`.github/workflows/store.yml` builds the Store package on a GitHub Windows
runner. No Windows PC is needed:

1. In Partner Center, reserve the app name, then copy **Product identity**
   into the GitHub repo's Actions variables: `STORE_IDENTITY_NAME`,
   `STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME` (and `STORE_DISPLAY_NAME`
   if the reserved name isn't "Nailangs").
2. Actions → **Microsoft Store package** → Run workflow, entering a version
   higher than the last submission (or push a tag like `v1.0.1`).
3. Download `Nailangs_<version>.msixbundle` from the run's Artifacts and
   upload it in Partner Center → Packages. The Store signs it.

The Store build has no Install tab: a Store app may not run the elevated
layout installer. Users get the pop-out keyboard, which remaps the physical
keys while it's open. `Package/` holds the manifest template and tile images.
