# QA replay: the stash desk's view

The owner's ask of 2026-10-19: a view of the player's own stash and gear from a screenshot, each item with what it is
worth and what it is kept for, "and it should also be fun to use". From a panel the owner chose A1 · B1 · C1 · D2, and
from its addendum A(2) · B1 · C1a · Da (docs/DESIGN.md §4, principle 3, "Screen anatomy", *The stash desk*; "Visual
language", *The stash desk's marks*; "Design system", *Motion*). Step B builds the view: PLAN | DESK at the rail's head;
the desk in the map's place out of a raid, opening by itself with a reading in the menus; the head line; the regions
stacked in reading order with the loadout strip, folded by the reading (until 2026-10-25, below); each cell's marks; a container's window
stemmed to its cell; the page coming in cell by cell and the totals counting up once. Step C (2026-10-19) builds the
desk's own card in the rail (decision first, the plate with the rule a click copies by, KEEP, the 30 days' band, the
day's bar, the traders), the click that copies a list price (its flash, decode and notice), pinning by a right-click or
the card's pin, the copy's guards, a "?" cell's pick, and the cells scaling with the desk's width (40 to 80 DIP). After
the owner's first test (2026-10-19) the cells fill the desk (40 to 128 DIP, the page flowed into columns), the pointed
card stands first in the rail with the pins as one line each under it, the cell shows the flea's listing price, and the
card's plate says what a click copies in short lines. Since the UI QA pass of that round (F1) those lines stand at the
rail's foot, always in sight, three at most, and the pointed card scrolls in the room above them. The layout round
(owner, 2026-10-25, decision 1: A, the GEAR tab mirrored and fixed) gives the page the game's GEAR tab's shape at one
cell size for the desk: LOADOUT as the game's slot grid, each box under its slot's name; TACTICAL RIG, POCKETS beside
SPECIAL SLOTS, BACKPACK and POUCH, a container's slot box beside its grid; STASH; the windows right of the stash or under
it; a region never read a dashed frame ("NOT IN A SCREENSHOT YET"), one of an earlier look stepped back (since
2026-10-27: faint, without figures, its head dated; an earlier look's window folded to a line); the fold gone. Its UI QA pass (2026-10-25) added the scroll mark, "MORE BELOW ▾" at the desk's
foot while the page runs past it, the heads' room for their dates from the first reading, and the slot names of a row
on one baseline.

`desk.devscript` replays it without a screenshot: its `desk <n>` hands the window the made-up desk's reading n
(`Rules.DeskExample`, built through the model as a reading's page is: real items of the catalogue, so their art and
cards are there, with made-up prices, counts, conditions, needs and price histories, and a cell in each of the copy's
states; no reading shows its POUCH, so its place is the dashed frame). Reading 1 is the mode's first; reading 2, seven
minutes later, showed and changed the backpack alone (a LEDX more), so the rest is kept from reading 1, stepped back and
dated; reading 3 is the same screenshot again and changed nothing; reading 4 is another first, whose POCKETS, SPECIAL
SLOTS and STASH came from their headings alone, each the dashed frame of a region never read. The made-up readings are dated 2026-01-01, so the
dated heads show the day and month before the time, 14:02 UTC in the PC's own time zone ("· 1 JAN 14:02" in UTC). The
rules are held by `DeskSwitchTests`, `DeskCellsTests`, `DeskNumbersTests`, `DeskPageRulesTests`,
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

- The stills go to `%TEMP%\shturmap-qa\desk\entry`, `first`, `end`, `window`, `card`, `copy`, `rule`, `pinned`,
  `pinshown`, `pinmore`, `trader`, `noflea`, `pick`, `picked`, `kept`, `same`, `plan`, `show`, `raid`, `after` and `back` (until
  2026-10-25 `fold` and `unfold` stood where `kept` does, and `kept` was today's `back`); then `--culture de-DE`,
  `--window 900x560` (40 DIP cells, one column in the same order, LOADOUT first, scrolling) and `--window 1600x900` (40
  DIP, three columns: LOADOUT, the middle column and the stash with the two windows under it, whose column runs past
  the desk, so the page scrolls; LOADOUT and the middle column whole) for the other runs, and `--window 2560x1440` (as
  large as the work area allows: a desk of 2176 × 1330 DIP takes 64 DIP, four columns, the windows right of the stash,
  unscrolled) (docs/QA.md, "Replay scripts").
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
  coming, 50 ms apart. The cells' coming in can't be seen in a still: a still draws a storyboard's opacity whole (the
  compositor runs it, the still's render doesn't look there; measured 2026-10-25, UI QA pass of step 2, F2), so every
  cell stands whole here while the totals count up; the owner's eye on the dev build checks the entry.
- **first**: the game's GEAR tab's shape, every region under its own head, none dated or dimmed: left LOADOUT; in the
  middle TACTICAL RIG, POCKETS beside SPECIAL SLOTS (their grids level), BACKPACK, POUCH; right STASH, ten cells wide;
  the windows ZHUK · IN THE BACKPACK and WZ, with the muted "Not attached: WZ ×2 on the page, on you and in the stash;
  counted in neither", right of the stash where the desk is wide enough (2560×1440), else under it. POUCH's place: an
  empty slot box and beside it a dashed frame five cells by two, "NOT IN A SCREENSHOT YET" muted inside it. The head
  line ends "ON YOU 3.70 M+ ₽ · STASH 7.46 M+ ₽ · KEEP 2 · 6 · 3" (what the sales bring, not the cells' listing
  prices; until 2026-10-25, with the made-up POUCH, 5.56 M+ and KEEP 2 · 7 · 3).
  LOADOUT: the game's slot grid, eight cells wide, three columns of boxes two cells square a cell apart, each box under
  its slot's name in small muted capitals: EARPIECE · HEADWEAR · FACE COVER; ARMBAND over DOGTAG (two short boxes) ·
  BODY ARMOR · EYEWEAR; ON SLING (five cells wide) · HOLSTER; ON BACK (five wide) · SHEATH. The earpiece, face cover,
  eyewear, on-back and sheath boxes empty (the cell's ground in its frame); the helmet and the armband with the "?"
  plate, their art at half strength and a range ("~40–59k", "0.3–350k"); the body armour "~74k" over "98/147"; the
  dog tag's "900" after the person glyph; the AK in its wide box and the Glock "~…k+" (mods unseen), each gun's picture
  at the box's top so the band covers the name it prints, the AK's "30/30". No name cut or over its neighbour's; in
  German "GESICHTSBEDECKUNG" over FACE COVER set smaller to fit its box. In the grids:
  a dark band across each item's top with its figure at the right after the price tag or the person; "~" before an
  estimate, "+" after the wallet's and the item case's figure; the IFAK's "166/300" without an arrow, the Analgin's
  "2/4" with "↓" (at 2560×1440, 80 DIP, the change as a figure: "−12%" beside "2/4", "↓ −10%" on a wider cell); the
  keycard and the stash's LEDX with a filled amber notch, the graphics cards an open one, the
  moonshine a corner bracket, the bitcoins a filled notch in ink; the roubles and the streamer case with no figure;
  the cell nothing was said of only its frame; the rig's and the Zhuk window's compartments a few DIP apart, as the
  game sets them (the Zhuk's ten pockets of one by two, a graphics card turned in one); the rig's and the backpack's
  own containers in their slot boxes left of their grids. A stem from the Zhuk's cell to its window's head, never
  across an item's picture (UI QA re-check, 2026-10-19, F2) nor a slot box: at 1600×900, the window under the stash,
  out of the cell's lower right corner along the Grizzly's bottom edge to the backpack's right edge, and down the gap
  between the middle column and the stash into the head; at 2560×1440, the window right of the stash, up that gap to
  the lane over the columns, across over STASH's head and down the gap left of the windows into the head; at 900×560,
  one column, out of the cell's left edge's middle, down between the backpack's slot box and its grid to under the box,
  and down the page's left margin past POUCH and STASH (`window` shows it). No grid under LOADOUT's boxes; at 1600×900
  LOADOUT and the middle column whole and the stash's ten columns whole beside them, the page scrolling for the windows
  under the stash; at 2560×1440 unscrolled. Keys and meds at a full print ("4/4", "100/100") without "~". No red or
  green anywhere on the page. POCKETS' and SPECIAL SLOTS' heads right over their grids, as near as every other head
  stands to its own, under a line left empty for the date a later reading gives them (UI QA pass of step 2, F1; the UI
  QA re-check of 2026-10-25: until then the heads stood at the top of that room, 23 px over their grids), their grids
  level. The names over LOADOUT's boxes of a row on one baseline; in German "GESICHTSBEDECKUNG" at its row's size
  without the spacing, within its box (F5). At 900×560 and 1600×900 the scroll mark at the desk's foot, over the
  page: "MORE BELOW ▾" muted in the eyebrows' small spaced capitals at the page's left margin (German
  „MEHR DARUNTER ▾“), on the desk's ground with a short fade of the page above it (at 900×560 over ON BACK's and
  SHEATH's boxes, at 1600×900 over the WZ window); none at 2560×1440, where the page fits (F3; DESIGN.md §4, *Its
  place*).
- **end** (the WZ window's cell, the page's last, pointed at): the page scrolled to its end, the WZ window's grid whole
  at the foot and the scroll mark gone; the rail shows the roubles' card ("NOT ON THE FLEA"). At 2560×1440 the page
  doesn't scroll and nothing moves.
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
- **kept** (a reading that showed and changed the backpack alone): the page in the shape and at the cell size of
  `first`, nothing moved; the backpack has the LEDX at 3,4 with its notch, its head undated and at full strength; every
  other region read (LOADOUT, TACTICAL RIG, POCKETS, SPECIAL SLOTS, STASH) stepped back where it stands (the newest look in
  front, 2026-10-27; desk-look.md): its grid, boxes and slot names faint (25 %), no figure, count or change on its
  cells, the ZHUK and WZ windows each folded to its head in a hairline frame with "▸", its head ending in its date and time, "· 1 JAN 14:02" in UTC (in German "· 1. JAN., 14:02"); SPECIAL SLOTS' head
  wrapped before its "·", its name gone up into the line left empty in `first` and the date whole on the second line,
  where the name stood; POCKETS' head on that line, its grid level with SPECIAL SLOTS'. Nothing under a head moved
  from `first`: every grid, box and head at the same y but SPECIAL SLOTS' name, a line up, scrolled alike (UI QA pass
  of step 2, F1: at 121e0f2 everything under POCKETS and SPECIAL SLOTS stood 13 px lower than in `first`; compare the
  rows where the brightness steps, which the stepping back leaves in place, not the pixels; only the windows' tail is
  shorter, folded). POUCH's frame as in `first`, not faint (never read). The head line's totals the new ones ("ON YOU 4.60 M+ ₽ · … · KEEP 3 · 6 · 3"). The selected
  violet keycard's pick kept.
- **same**: as kept: a reading that changed nothing changed nothing, and stepped nothing back anew; the backpack full.
- **plan**: the map and Plan's rail back, PLAN's half raised with the amber bar and PLAN in amber, DESK muted; the
  map's buttons and credit back.
- **show**: the desk as in same, the page kept.
- **raid**: the raid view (RAID LOADING), the switch gone from the rail's head, MAP at its top as before, no desk.
- **after** (7 s after `end`, the RAID OVER cue gone): Plan, the switch back above MAP with PLAN's half raised and in
  amber; no desk.
- **back**: the desk with the page of same.
