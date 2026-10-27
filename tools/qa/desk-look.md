# QA replay: the stash desk's newest look in front

The owner's decisions of 2026-10-27, from the decision page on the desk (docs/DESIGN.md §4, *The stash desk*, *The newest
look in front* and *The head line*; docs/NEXT.md, "2026-10-27: the newest look in front, the prices' age, the file when
due"). D1 B: "I also think right now that the manual clean up might not be a good idea, maybe we should do in general a
better job just showing the items the player is currently interested in." The regions of the newest look (the page's
newest screenshot and any taken within 2 minutes of it) stand full; a region of an earlier look steps back where it
stands, its grid, slot boxes and slot names faint (25 %), its cells' figures, counts and changes not shown, its head whole
and dated; an earlier look's container window folds to its head line in a hairline frame, "▸" after it; pointing at
either shows it whole (a folded window unfolds in place, "▾", the windows under it moving down), at once. Nothing else
moves: the three columns, the cell size and every head stand where they stood. CLEAR and its UNDO went. D2 C: at the head
line's right, "PRICES 3 MIN OLD" (the newest scan in the item file in hand; amber past 20 minutes or offline), and a copy's
notice ends with the copied item's own age, "· price 4 min old" (amber past 30 minutes).

`desk-look.devscript` hands the window the made-up desk (`Rules.DeskExample`): reading 1, then reading 2 seven minutes
later (the backpack alone, so everything else steps back and the Zhuk's and the wallets' windows fold), points at the rig
and at the Zhuk's window, copies the backpack's Grizzly and the stash's LEDX (the made-up LEDX was scanned 52 minutes
ago), and plays reading 3 (the same screenshot again two minutes on). The made-up desk's item file was scanned 3 minutes
ago, its items 4 minutes. The rules are held by `DeskPageRulesTests` (the look, the fold, the head line's age, windows
out of the fit) and `DeskCardTests` (the copy's age); the checks below are for the stills.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\desk-look'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\desk-look.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
Get-ChildItem "$qa\data\logs" | Select-String -Pattern 'INFO Developer view: desk'
```

Then `--culture de-DE` and `--window 900x560` and `--window 1600x900` added to the arguments for the other runs, each
copied away before the next (docs/QA.md, "Replay scripts").

## What the stills must show

- `first`: the made-up desk as `desk.md`'s `first` has it, every region full and undated, no frame round the windows. At
  the head line's right, at the desk's right edge 18 DIP in, "PRICES 3 MIN OLD" (German „PREISE 3 MIN ALT“) in the head
  line's muted capitals on its row, its top level with the head line's, never over it and never wrapping; the head line
  wraps before it where the desk is narrow (900×560), never runs under it.
- `stepped-back`: the backpack full, its cells come in; LOADOUT, the rig, the pockets, the special slots and the stash in
  their places, their heads whole and dated ("TACTICAL RIG · 14:02", the day's time; another day's date at the next
  start), their grids, boxes and slot names faint, no figure, count or change on their cells; the POUCH still the dashed
  frame of a region never read (never faint: it was never read). The Zhuk's and the wallets' windows one line each, each
  in a hairline frame as wide as the windows' column, "ZHUK · IN THE BACKPACK · 14:02 ▸"; the Zhuk's stem still runs to
  its head. The page's shape, the cell size and every head's place as in `first` (compare the two at the same size: only
  the windows' tail is shorter). The head line's totals as in `first` plus the backpack's new LEDX: the stepped-back
  regions still count.
- `rig-pointed`: the rig whole, its figures and counts back, its card in the rail; every other stepped-back region as in
  `stepped-back`.
- `window-pointed`: the Zhuk's window unfolded in its frame, "▾" after its head, its cells and figures whole; the
  wallets' window moved down under it, still folded; the stem to the Zhuk's head, and the Zhuk's cell in the backpack
  lit; the rig stepped back again.
- `copied`: the Grizzly's frame flashed, the notice at the top: "… ₽ copied · 3% under the last low, scaled to 1469/1800
  · price 4 min old", the age muted; at 900×560 in German it may wrap to two lines, never cut.
- `copied-old`: the stash shown whole while its LEDX is clicked, the notice "970,000 ₽ copied · 3% under the last low ·
  price 52 min old", the age in amber.
- `again`: as `stepped-back` (the backpack read anew, nothing comes in, the same regions stepped back).
- Nowhere: CLEAR or UNDO in the desk's head, a region that moves or resizes as it steps back or is pointed at, a figure
  on a stepped-back cell that isn't pointed at, the age over the head line or cut.
