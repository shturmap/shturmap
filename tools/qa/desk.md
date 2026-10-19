# QA replay: the stash desk's view

The owner's ask of 2026-10-19: a view of the player's own stash and gear from a screenshot, each item with what it is
worth and what it is kept for, "and it should also be fun to use". From a panel the owner chose A1 · B1 · C1 · D2, and
from its addendum A(2) · B1 · C1a · Da (docs/DESIGN.md §4, principle 3, "Screen anatomy", *The stash desk*; "Visual
language", *The stash desk's marks*; "Design system", *Motion*). Step B builds the view: PLAN | DESK at the rail's head;
the desk in the map's place out of a raid, opening by itself with a reading in the menus; the head line; the regions
stacked in reading order with the loadout strip, folded by the reading; each cell's marks; a container's window
stemmed to its cell; the page coming in cell by cell and the totals counting up once. The desk's own card and the
click are step C: for step B the rail shows the item card the pointer opens anywhere.

`desk.devscript` replays it without a screenshot: its `desk <n>` hands the window the made-up desk's reading n
(`Rules.DeskExample`, built through the model as a reading's page is: real items of the catalogue, so their art and
cards are there, with made-up prices, counts, conditions and needs). Reading 1 is the mode's first; reading 2 changed
the backpack (a LEDX more); reading 3 changed nothing. The rules are held by `DeskSwitchTests`, `DeskFoldTests`,
`DeskCellsTests`, `DeskNumbersTests`, `DeskPageRulesTests` and `DeskExampleTests`; the checks below are for the stills.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\desk'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\desk.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\desk\entry`, `first`, `window`, `card`, `fold`, `unfold`, `same`, `plan`,
  `show`, `raid`, `after` and `kept`; then `--culture de-DE`, `--window 900x560` (40 DIP cells, one column) and
  `--window 1600x900` (56 DIP cells, the stash in a column of its own from a desk 980 DIP across) for the other runs
  (docs/QA.md, "Replay scripts").
- `--verbose`: the app log (`$qa\data\logs`) has every step and the lines `Example desk: reading 1 of 3`, `Stash desk
  up (reading)`, `Stash desk left (switch)` or `(dev)`, `Stash desk left (raid)`.
- Windows' animation effects on: with them off the page stands at once and `entry` shows it whole. Keep the mouse off
  Shturmap's window: the pointer on a cell puts its card in the rail.

## What each still must show

- **entry** (about 0.3 s after `desk 1`): the desk in the map's place, the map's buttons and credit gone; at the rail's
  head PLAN | DESK in one frame, DESK in amber on the raised ground; the rail under the MAP list and the trader strip
  says "Point at an item for its card." and nothing of Plan's (no NEXT RAID, no ANY MAP). The head line in muted
  capitals counting up ("ON YOU … ₽ · STASH … ₽ · KEEP n · n · n"); the first cells of LOADOUT in, the rest still
  coming, 50 ms apart.
- **first**: every region open, in reading order under its own head: LOADOUT, TACTICAL RIG, POCKETS beside SPECIAL
  SLOTS, BACKPACK, ZHUK · IN THE BACKPACK, POUCH, STASH, then WZ with the muted "Not attached: WZ ×2 on the page, on
  you and in the stash; counted in neither". The head line ends "ON YOU 5.4x M+ ₽ · STASH 6.7x M+ ₽ · KEEP 2 · 7 · 3".
  LOADOUT: slot cells 80×40 (a weapon 164×40) in the game's order, no slot names: the helmet and the armband with the
  "?" plate, their art at half strength and a range ("~34–52k", "0.3–294k"); the body armour "~67k" over "98/147"; the
  dog tag's figure after the person glyph; the AK and the Glock "~…k+" (mods unseen), the AK's "30/30". In the grids:
  a dark band across each item's top with its figure at the right after the price tag or the person; "~" before an
  estimate, "+" after the wallet's and the item case's figure; the IFAK's "166/300" without an arrow, the Analgin's
  "2/4" with "↓" (at 1600×900, 56 DIP, the change as a figure: "−12%" beside "2/4", "↓ −10%" on a wider cell); the
  keycard and the stash's LEDX with a filled amber notch, the graphics cards an open one, the
  moonshine a corner bracket, the bitcoins a filled notch in ink; the roubles, the Gamma and the streamer case with no
  figure; the cell nothing was said of only its frame; the rig's and the Zhuk window's compartments a few DIP apart, as
  the game sets them (the Zhuk's ten pockets of one by two, a graphics card turned in one); a stem from the Zhuk's cell,
  out of its left edge's middle, to its window's head, along the backpack's margin. No grid under the LOADOUT strip; at
  1600×900 the stash's ten columns whole beside the gear, POCKETS and SPECIAL SLOTS side by side in the gear's column.
  No red or green anywhere on the page.
- **window**: the Zhuk's cell gold-tinted with an amber frame, its window's head on gold, the stem in amber; the rail
  says "One of 2 items".
- **card**: the LEDX's item card in the rail, whole (not see-through), under the MAP list and the trader strip.
- **fold**: "UNCHANGED · LOADOUT ▸ · TACTICAL RIG ▸ · POCKETS ▸ · SPECIAL SLOTS ▸" over BACKPACK, then "UNCHANGED ·
  ZHUK ▸ · POUCH ▸ · STASH ▸ · WZ ▸" (at 1600×900 the stash's column has its own line, "UNCHANGED · STASH ▸ · WZ ▸");
  where a fold line wraps, the next line starts with "·", never a line ending in one;
  the backpack has the LEDX at 3,4 with its notch; the head line's totals the new ones.
- **unfold**: the stash open under its head, its column's fold line gone or WZ alone in it.
- **same**: as unfold: a reading that changed nothing changed nothing.
- **plan**: the map and Plan's rail back, PLAN in amber; the map's buttons and credit back.
- **show**: the desk as in same, the page and its fold kept.
- **raid**: the raid view (RAID LOADING), the switch gone from the rail's head, no desk.
- **after** (7 s after `end`, the RAID OVER cue gone): Plan, the switch back with PLAN in amber; no desk.
- **kept**: the desk with the page and fold of same.
