# QA replay: the newest What's New card, line by line

The release UI QA pass of 0.7.0 (2026-10-26) checked each line of 0.7.0's What's New card with a script of its own; this
is that script, made to take the newest card, whatever the build's version. What's New is a card at the top of Plan's
rail after an update (docs/DESIGN.md §4, "Screen anatomy", *What's New*; its lines are docs/whats-new.md's newest
section, `docs/whats-new.de.md`'s in German). A line is `- <preview> · <name> · <what it is>`: pointing at a line stages
its example on the map under a PREVIEW label, and from 0.7.0 on each line has a preview of its own, its picture drawn
beside it (owner, 2026-10-27, D4 B: `desk`, `tasks`, `textsize`, `glance`, `grid`). A click on a line holds its preview
when the pointer leaves the card; a second click lets it go. No line opens the tour (an older version's `tour:N` line
previews nothing and does nothing on a click). The rules are held by `WhatsNewTests` and `FeatureGlyphsTests`; the
checks below are for the stills.

`whatsnew-lines.devscript` shows the card (`whatsnew`, the newest version's, as help's link shows it), then for each of
its lines points at it (`whatsnew <n>`), takes a still early in its preview and one at its end, clicks it (`whatsnew
click <n>`), takes the pointer off the card (`whatsnew leave`) and takes a still of the preview held, then clicks it
again and leaves, which lets it go.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\whatsnew-lines'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\whatsnew-lines.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\whatsnew-lines\card`, `line-1-early`, `line-1`, `line-1-held` … `line-5-held`
  and `end`; then `--culture de-DE` and `--window 900x560` for the other runs (docs/QA.md, "Replay scripts").
- `--dev-view`: a fake game of its own; the app exits at the script's `exit`. `--verbose`: the app log
  (`$qa\data\logs`) has every step, each still's moment, and for each click `What's New: <version>:<i> held` or `… let
  go`.
- A card has up to five lines. On one with fewer, the steps past its last line say `What's New <version> has <n> lines`
  in the log and change nothing: their stills are the one before them again.
- Windows' animation effects on: with them off a moving preview shows its end at once, and `line-n-early` is
  `line-n` again. Keep the mouse off Shturmap's window.

Each folder holds `window.png` (the window; its map's panel empty), `map.png` (the map alone), `cue.png` while a cue is
up (the `tasks` preview's cue), `diagnostics.txt` and the developer view's `devview-build.png` and `devview-play.png`
(taken after the step, while the script goes on). Put `map.png` into the map's panel of `window.png`, and `cue.png`
over both, to see what a player saw.

## When each still is taken

`line-n-early` 1.5 s after the pointer went onto line n (a moving preview is under way), `line-n` 6 s after (a moving
preview has played its 4.5 s and holds its end), `line-n-held` 1.5 s after the click and the pointer's leaving. A
`snapshot` step takes about half a second here, the map standing still (2026-10-26, Debug build, the default window
2560×1392); the log gives each still's moment. A still is taken with RenderTargetBitmap, which draws a storyboard's
opacity at its end: cells coming in and a plate rising in look whole in `line-n-early`; the count-up, the decode and the
position's walk show where they are.

## What each still must show

Read the card's lines from docs/whats-new.md's newest section first (in German from `docs/whats-new.de.md`): line n's
preview, name and words.

- **card**: the card at the top of Plan's rail, under MAP: NEW IN <version> · <NAME> (NEU IN …), its lines in the
  section's order, each with its picture in the 56×34 place beside it, its name and words, wrapped and never cut; under
  them, in one line, "Point at one: the map shows it. Click: it stays." ("Zeig auf eine Zeile: Die Map zeigt sie. Klick:
  Sie bleibt."). The map is the player's own (Customs), with no PREVIEW label.
- **line-n** (the pointer on line n, its preview at its end): the label PREVIEW · NEW IN <version> · <NAME> with its
  hint, AN EXAMPLE, NOT YOUR … (EIN BEISPIEL, NICHT …), at the map's top, and by the line's preview:
  - `desk`: a desk page on a plate over the dimmed player's map, the made-up page laid out at the map's size in the
    desk's columns and drawn smaller (its head line with ON YOU and the stash's totals counted up, LOADOUT, TACTICAL RIG,
    POCKETS, BACKPACK with the Zhuk's window, the STASH with its figures; MORE BELOW where the page is longer than the
    map, as on the desk), never cut; under it THE STASH DESK, WITH AN EXAMPLE PAGE (DER DESK,
    MIT EINER BEISPIELSEITE); hint AN EXAMPLE, NOT YOUR STASH. In `line-n-early` the totals still counting.
  - `tasks`: the cue of the tasks read over the player's map: FROM YOUR SCREENSHOT, 3 DAILIES AND WEEKLIES, "An example,
    not your dailies." under it, three rows with their framed glyphs, each with its map and time left in muted capitals
    (STREETS OF TARKOV · 25 MIN LEFT first, CUSTOMS, ANY MAP); hint AN EXAMPLE, NOT YOUR DAILIES. Still: the cue stays.
  - `textsize`: a plate with two panels side by side, NORMAL and LARGEST under them (AM GRÖSSTEN), each with the same
    three lines (a quest's name, its trader, an extract's name in green), the LARGEST one's clearly larger; under it
    SETTINGS' TEXT SIZE, WITH EXAMPLE LINES; hint AN EXAMPLE, NOT YOUR SETTING. The window's own text, the card's
    included, unchanged.
  - `glance`: the in-raid glance's plate over the dimmed player's map, a band between two amber hairlines: FROM YOUR
    SCREENSHOT, LOOT with its total in amber, ON YOU under it, a drawer's items in the desk's cells with their figures;
    under it THE IN-RAID GLANCE, WITH EXAMPLE LOOT; hint AN EXAMPLE, NOT YOUR RAID. In `line-n-early` the plate risen,
    figures decoding.
  - `grid`: Customs with the squad grid (also where settings have it off), framed on C5 and D5, the player's marker in
    D5's top middle, the tag beside the marker and the readout at the map's top left YOU D5-8 (DU D5-8); hint AN
    EXAMPLE, NOT YOUR RAID. In `line-n-early` the marker still in C5 or crossing, the readout YOU C5-9 or D5-7. The map
    lights no square: the reference says where YOU is.
  - `desk-look` (0.7.1's line 1, "Your newest screenshot in front"): the made-up desk on the plate as for `desk`, the
    page scrolled to the rig; the BACKPACK full, its LEDX at 3,4, its head undated; TACTICAL RIG shown whole, its
    figures back, the drawn pointer (white arrow on a dark rim) on its first cell; every other region read stepped back:
    its head whole and dated with the time 7 minutes before the still ("POCKETS · 22:25"), its grid, boxes and slot
    names faint, no figure, count or change on its cells; the ZHUK and WZ windows (below, MORE BELOW at 900×560) folded
    to a framed line with "▸". The head line's totals reading 2's (ON YOU 4.60 M+ ₽ … KEEP 3 · 6 · 3), "PRICES 3 MIN
    OLD" at its right. In `line-n-early` reading 1: every region full, undated, no pointer.
  - `prices` (line 2, "Prices with their age"): the made-up desk (reading 1, every region full) scrolled to the stash's
    top, "PRICES 3 MIN OLD" (PREISE 3 MIN ALT) at the head line's right; the stash's LEDX with the pointer on it, its
    figure whole; at the page's foot the copy notice in the window's notice look, the amber square, "970,000 ₽ copied ·
    3% under the last low · price 52 min old" ("970.000 ₽ kopiert · … · Preis 52 min alt"), the age in amber, whole
    (wrapped, never cut). In `line-n-early` no notice yet, the pointer on its way or not yet shown.
  - `grid-pointer` (line 3, "The grid under the pointer"): Customs with the grid as for `grid`, the player's marker in
    D5's top middle, the drawn pointer just above it in D4's bottom middle; in `map.png` the square D4 lit faintly and
    its bottom middle ninth a little more, the strips' D (top and bottom) and 4 (left and right) bold in full ink on a
    lighter band; at the map's top left the readout's two lines, YOU D5-8 and under it POINTER D4-2 (DU D5-8, MAUS
    D4-2), whole, at 900×560 too; the preview label right of the readout there (centred where the map is wide), never
    over it. In `line-n-early` the pointer at E4's top right or crossing, the readout's line POINTER E4-9 or on its way.
  - an older line (`replay`, `extracts`, `stash`, …): its example as before; a `tour:N` line: nothing staged, no label.
  The line tinted as the one pointed at.
- **line-n-held** (clicked, the pointer off the card): the same as **line-n**, the preview held at its end (not played
  again), the line still tinted; the log's `What's New: <version>:<n−1> held`.
- **end** (line 5 clicked again, the pointer off the card): the card as in **card**, no line tinted, the map the
  player's own, no label; the log's `… let go`.

In every still: the card's words whole (the layout file `window.layout.json` with no problem), in German its German
lines; nothing of an earlier line's preview left on the map once the next line is pointed at; no tour.

## What would be a fault

- A line that opens the tour, or a `tour:N` chapter's band in any still.
- A preview line that shows nothing, another line's example, or its example on the wrong map; a preview still up after
  the next line was pointed at, or after a click let it go and the pointer left.
- A held preview gone when the pointer left the card, or played again from its start.
- A plate cut at its top or its foot, under WIKI MAP and the credit, or its caption not drawn.
- The grid's preview leaving the grid on the player's map where settings have it off.
