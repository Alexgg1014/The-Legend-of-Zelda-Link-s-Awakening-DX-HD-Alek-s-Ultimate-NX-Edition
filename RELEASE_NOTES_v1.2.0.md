# v1.2.0 — upstream v2.0.9 and the port's UI in six languages

Built against **bighead's ladxhd_updated v2.0.9** (released 9 October 2026). If you are on
v1.1.0, just replace the NRO: the first start notices the assets are from v2.0.8, re-patches
your `original/` copy on the console (about a minute) and carries on. Your saves are untouched.

## What's new

### Up to date with upstream v2.0.9

Everything bighead fixed in v2.0.9 comes with it, including:

- The boomerang is redirected when you smack it with the sword, following the direction Link faces.
- Picking up the Angler Fish heart container works in any state, not only while swimming.
- The enemy Urchin no longer crashes the game when burned.
- High definition texture replacement (for LAHDMods that ship one; it changes nothing otherwise).
- Improved Italian and German translations, and Chinese achievements.

### The port's own interface in six languages

- The **Switch rows in the game's own Video menu** (Switch resolution, FPS limit, Flip Grip,
  hide HUD, large UI) were English or Spanish only. They now follow the game language across
  **English, Spanish, Portuguese, French, German and Italian**, tooltips included — the same
  six the companion panel already spoke.
- The **story guide** (the "next step" line on the QUEST tab, and the trading-sequence hint)
  was English and Spanish. It is now in all six as well: 27 main-quest steps and the 14 trades
  per language.

  The place, dungeon, key and character names are **not** invented or machine-translated: they
  are read out of the game's own language files, so French really says *Plage Coco*, *Cave
  Flagello* and *Toutou*, and German says *Torongastrand*, *Wurmpalast* and *Struppi* — the
  names those players actually see on their signposts.

Any other game language still falls back to English, as before.

## Installing

Same as v1.1.0 — see the [README](README.md). Short version: put your own v1.0.0 `Content` and
`Data` in `sdmc:/switch/zelda-ladxhd/original/`, drop the NRO next to them, launch it from
hbmenu via title override.

**If you are updating:** replace `ProjectZ.Switch.nro` and leave `original/` where it is. Do not
leave the old NRO in the folder — hbmenu lists every `.nro` it finds, and launching the old one
is the most common "it crashed after updating" report.

## Credits

The HD remake and all the asset patches are [bighead's](https://gitlab.com/bighead.0/ladxhd_updated);
the vcdiffs are embedded here with his explicit permission. The original game is Nintendo's.
This project only adds the Switch layer and ships no game assets.
