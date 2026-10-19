# QA replay: the stash desk's view

The owner's ask of 2026-10-19: a view of the player's own stash and gear from a screenshot, each item with what it is
worth and what it is kept for, "and it should also be fun to use". From a panel the owner chose A1 · B1 · C1 · D2, and
from its addendum A(2) · B1 · C1a · Da (docs/DESIGN.md §4, principle 3, "Screen anatomy", *The stash desk*; "Visual
language", *The stash desk's marks*; "Design system", *Motion*). Step B builds the view: PLAN | DESK at the rail's head;
the desk in the map's place out of a raid, opening by itself with a reading in the menus; the head line; the regions
stacked in reading order with the loadout strip, folded by the reading; each cell's marks; a container's window
stemmed to its cell; the page coming in cell by cell and the totals counting up once. Step C (2026-10-19) builds the
desk's own card in the rail (decision first, the plate with the rule a click copies by, KEEP, the 30 days' band, the
day's bar, the traders), the click that copies a list price (its flash, decode and notice), pinning by a right-click or
the card's pin, the copy's guards, a "?" cell's pick, and the cells scaling with the desk's width (40 to 80 DIP). After
the owner's first test (2026-10-19) the cells fill the desk (40 to 128 DIP, the page flowed into columns), the pointed
card stands first in the rail with the pins as one line each under it, the cell shows the flea's listing price, and the
card's plate says what a click copies in short lines. Since the UI QA pass of that round (F1) those lines stand at the
rail's foot, always in sight, three at most, and the pointed card scrolls in the room above them.

`desk.devscript` replays it without a screenshot: its `desk <n>` hands the window the made-up desk's reading n
(`Rules.DeskExample`, built through the model as a reading's page is: real items of the catalogue, so their art and
cards are there, with made-up prices, counts, conditions, needs and price histories, and a cell in each of the copy's
states). Reading 1 is the mode's first; reading 2 changed the backpack (a LEDX more); reading 3 changed nothing. The
rules are held by `DeskSwitchTests`, `DeskFoldTests`, `DeskCellsTests`, `DeskNumbersTests`, `DeskPageRulesTests`,
`DeskExampleTests`, `DeskCardTests`, `DeskCopyTests`, `DeskPinsTests`, `DeskHistoryTests` and `DeskStemTests`; the
checks below are for the stills. The script's clicks (`desk copy`) leave the clipboard alone; the app log says what they
would have copied. `desk show <n>` clicks the n-th pinned line in the rail's foot, from the top, as the pointer does
(`desk show` alone is DESK at the rail's head).

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

- The stills go to `%TEMP%\shturmap-qa\desk\entry`, `first`, `window`, `card`, `copy`, `rule`, `pinned`, `pinshown`,
  `pinmore`, `trader`, `noflea`, `pick`, `picked`, `fold`, `unfold`, `same`, `plan`, `show`, `raid`, `after` and
  `kept`; then `--culture de-DE`, `--window 900x560` (40 DIP cells, one column, scrolling) and `--window 1600x900` (40
  DIP, three columns: the gear's two and the stash's, unscrolled) for the other runs, and `--window 2560x1440` (80 DIP,
  three columns, as large as the work area allows) (docs/QA.md, "Replay scripts").
- `--verbose`: the app log (`$qa\data\logs`) has every step and the lines `Example desk: reading 1 of 3`, `Stash desk
  up (reading)`, `Stash desk left (switch)` or `(dev)`, `Stash desk left (raid)`.
- Windows' animation effects on: with them off the page stands at once and `entry` shows it whole. Keep the mouse off
  Shturmap's window: the pointer on a cell puts its card in the rail.

## What each still must show

- **entry** (about 0.3 s after `desk 1`): the desk in the map's place, the map's buttons and credit gone; at the rail's
  head, above MAP, PLAN | DESK across the rail's width in one frame, 36 DIP tall, the words in the title's size and
  centred in their halves, DESK's half on the raised ground with an amber bar at its left and DESK in amber, PLAN
  muted (owner, 2026-10-19: the switch a primary control); the rail under the MAP list and the trader strip
  says "Point at an item for its card." and nothing of Plan's (no NEXT RAID, no ANY MAP). The head line in muted
  capitals counting up ("ON YOU … ₽ · STASH … ₽ · KEEP n · n · n"); the first cells of LOADOUT in, the rest still
  coming, 50 ms apart.
- **first**: every region open, in reading order under its own head: LOADOUT, TACTICAL RIG, POCKETS beside SPECIAL
  SLOTS, BACKPACK, ZHUK · IN THE BACKPACK, POUCH, STASH, then WZ with the muted "Not attached: WZ ×2 on the page, on
  you and in the stash; counted in neither". The head line ends "ON YOU 5.56 M+ ₽ · STASH 7.46 M+ ₽ · KEEP 2 · 7 · 3"
  (what the sales bring, not the cells' listing prices).
  LOADOUT: slot cells 80×40 (a weapon 164×40) in the game's order, no slot names: the helmet and the armband with the
  "?" plate, their art at half strength and a range ("~34–52k", "0.3–294k"); the body armour "~67k" over "98/147"; the
  dog tag's figure after the person glyph; the AK and the Glock "~…k+" (mods unseen), the AK's "30/30". In the grids:
  a dark band across each item's top with its figure at the right after the price tag or the person; "~" before an
  estimate, "+" after the wallet's and the item case's figure; the IFAK's "166/300" without an arrow, the Analgin's
  "2/4" with "↓" (at 2560×1440, 80 DIP, the change as a figure: "−12%" beside "2/4", "↓ −10%" on a wider cell); the
  keycard and the stash's LEDX with a filled amber notch, the graphics cards an open one, the
  moonshine a corner bracket, the bitcoins a filled notch in ink; the roubles, the Gamma and the streamer case with no
  figure; the cell nothing was said of only its frame; the rig's and the Zhuk window's compartments a few DIP apart, as
  the game sets them (the Zhuk's ten pockets of one by two, a graphics card turned in one); a stem from the Zhuk's cell,
  out of its left edge's middle, to its window's head, along the backpack's margin (at 1600×900, where the Zhuk window
  heads the gear's second column, out of the cell's lower right corner and down the gap between the columns into the
  head, along the Grizzly's bottom edge); never across an item's picture (UI QA re-check, 2026-10-19, F2). No grid
  under the LOADOUT strip; at 1600×900 and 2560×1440 the page unscrolled, the stash's ten columns whole
  beside the gear, POCKETS and SPECIAL SLOTS side by side in the gear's column. Keys and meds at a full print ("4/4",
  "100/100") without "~". No red or green anywhere on the page.
- **window**: the Zhuk's cell gold-tinted with an amber frame, its window's head on gold, the stem in amber; the rail
  says "One of 2 items".
- **card**: the LEDX's desk card first in the rail, under the MAP list and the trader strip, 351 DIP: its icon, name,
  the full dot with "60 OFFERS · FLEA FROM LEVEL 15" and the pin (a cell's outline with a muted tab) at the right; the
  figure in gold, "1,000,000 ₽" (the flea's listing price), with its 7-day change at the right ("↑ +…% · 7 D" or "↓
  −…"); right under it the plate on the raised ground: "A CLICK COPIES" with − "3% UNDER" + (the rule in amber),
  "Copies 970,000 ₽" semibold, "Nets 873k ₽ after the fee" muted, "Last low 1,000,000 ₽ · Therapist pays 650,000 ₽"
  muted (in German the trader on a line of its own, "· Therapist zahlt 650.000 ₽", never a line ending in "·"); "Flea
  net 901,728 ₽ · fee 98,272 ₽" with "↓ −10.4% · 48 H"; KEEP: Private Clinic ×1 · now · FIR (a filled amber notch), Medstation 3 ×1 · a
  hideout module (filled, ink), Crisis ×2 · later · FIR (a bracket); 30 DAYS: the amber band with its average's line, a
  dashed hairline "7 D", "HIGH … · LOW …, n DAYS AGO"; 24 H: "LAST LOW 1,000,000 · AVG 980,000" over the track, the
  ticks, "820,000" and "1,100,000 ›" under it, the outlier's line (9,999,999); TRADERS Therapist then Fence; CRAFTS
  AND BARTERS where the data has them; "Prices just in · tarkov.dev". At 900×560 the figure, the plate, the flea's net
  and KEEP's first lines show without scrolling, in English and German. The − and +'s tips: "Lower: 4% under the last
  low", "Higher: 2% under the last low". No red or green.
- **copy** (right after the click): the LEDX's cell framed in gold, its figure decoding (gold glyphs settling to ink);
  the notice "970,000 ₽ copied · 3% under the last low".
- **rule**: the card's plate says "2% OVER", "Copies 1,020,000 ₽" and "Nets 921k ₽ after the fee"; settings' row
  "Flea price to copy" (open it by hand to check) the same; the −'s tip "Lower: 1% over the last low".
- **pinned**: the pointed key's card first (UI QA pass of step C, F2), its plate with the 6 px gold square: "Copies
  1,832,221 ₽", muted "Over the best net: at 1.75 M ₽ it nets 708k ₽, at this 707k ₽"; the two pinned cards as one line
  each, framed in amber, at the rail's foot under the rail's hairline, the Grizzly's on top ("Grizzly medical kit
  ~33,461 ₽") over the LEDX's ("LEDX Skin Transilluminator 1,000,000 ₽"), both whole and in sight at every size, in
  English and German (UI QA pass of the second round, F1: at 1600×900 the LEDX's line stood cut at the window's bottom,
  in German below it); the key's card runs on to the hairline and scrolls in the room above it (at 900×560 its figure
  and the plate's lines in view, in English and German). On the page the LEDX's and the Grizzly's cells carry a short
  amber bar on their top edge.
- **pinshown** (`desk show 2`, the foot's second line clicked): the LEDX's card whole first in the rail, framed in
  amber as pinned, its head at the top of the card's room (a click scrolls the room back to its top); the foot holds
  the Grizzly's line alone; the key's card gone. At 900×560 KEEP's eyebrow under the
  flea's net line, its first need at the hairline.
- **pinmore** (four pinned, the key pointed at again): the key's card first; at the foot three lines, "5.45x39mm BS gs
  27,000 ₽" on top, then "TerraGroup Labs keycard (Blue) 459,000 ₽" and the Grizzly's; the LEDX's, the fourth, not in
  sight (the strip's own scroll reaches it; the strip no taller than three lines). In German the keycard's line, the
  second, is trimmed with an ellipsis at its end ("Blaue TerraGroup Labs Schlüsselk…"), its figure whole: the rule
  (DESIGN.md §4, *The rail*; UI QA re-check, 2026-10-19, F1).
- **trader**: the blue keycard's card: the plate's line muted, "Therapist pays more · 459,000 ₽. A click copies
  nothing.", only the last low under it; the notice "Nothing copied: Therapist pays more, 459,000 ₽"; no flash.
- **noflea**: the 5.45 ammunition's card: "NOT ON THE FLEA" under its name, the plate "Can't be listed on the flea. A
  click copies nothing.", no flea net line, Prapor and Fence under TRADERS.
- **pick**: the keycards' "?" card: "One of 3 items", the note, three rows (icon, name, figure in gold: 459k, 330k,
  160k, the listing prices); the notice "One of 3 items: pick it on the card first".
- **picked**: the violet keycard's card in the rail; on the page the cell's "?" plate gone, its art at full strength, its
  figure the violet's.
- **fold**: "UNCHANGED · LOADOUT ▸ · TACTICAL RIG ▸ · POCKETS ▸ · SPECIAL SLOTS ▸" over BACKPACK, then "UNCHANGED ·
  ZHUK ▸ · POUCH ▸ · STASH ▸ · WZ ▸" (at 1600×900 the stash's column has its own line, "UNCHANGED · STASH ▸ · WZ ▸");
  where a fold line wraps, the next line starts with "·", never a line ending in one;
  the backpack has the LEDX at 3,4 with its notch; the head line's totals the new ones. The backpack's cells as large as
  the desk's height lets them be (104 DIP at 1600×900, its grid filling the height, nothing scrolled).
- **unfold**: the stash open under its head, its column's fold line gone or WZ alone in it.
- **same**: as unfold: a reading that changed nothing changed nothing.
- **plan**: the map and Plan's rail back, PLAN's half raised with the amber bar and PLAN in amber, DESK muted; the
  map's buttons and credit back.
- **show**: the desk as in same, the page and its fold kept.
- **raid**: the raid view (RAID LOADING), the switch gone from the rail's head, MAP at its top as before, no desk.
- **after** (7 s after `end`, the RAID OVER cue gone): Plan, the switch back above MAP with PLAN's half raised and in
  amber; no desk.
- **kept**: the desk with the page and fold of same.
