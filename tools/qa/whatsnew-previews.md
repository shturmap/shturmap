# QA replay: older What's New cards' previews, one of each kind

The UI QA re-check of the squad grid's fixes (2026-10-26) pointed at the older cards' preview lines with a script of its
own, to see their framings inside the grid's strips; this is that script. Pointing at a What's New line with a preview
stages its example under a PREVIEW label (docs/DESIGN.md §4, "Screen anatomy", *What's New*): four stage a map of their
own (`replay`, `extracts`, `joined`, `leaders`), the others lay only a plate or a cue over the player's map (`clock`,
`stash`, `stash-card`, `stash-given`, `notes`; `Rules.WhatsNew.StagesMap`). Moving from one line to the next switches
the preview at once, and each starts from the player's map put back ("leaving puts the map back as it was"). The rules
are held by `WhatsNewTests`, `ReplayTests` and `GridTests`; the checks below are for the stills.

`whatsnew-previews.devscript` sets the player's map to Customs (`map customs`), shows 0.4.0's card (`whatsnew 0.4.0`),
then points at `replay`, `extracts`, `clock` and `joined` there, 0.5.0's `stash`, 0.6.0's `notes` and 0.6.2's first
`leaders` line, one after the other as a pointer moving down the cards (`whatsnew <version> <n>`), and lets go (`whatsnew
leave`).

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\whatsnew-previews'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\whatsnew-previews.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --window 900x560 --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\whatsnew-previews\replay`, `extracts`, `clock`, `joined`, `stash`, `notes`,
  `leaders` and `end`; then without `--window` (the default window) and with `--culture de-DE` for the other runs
  (docs/QA.md, "Replay scripts"). The smallest window is where the controls and the PREVIEW label come closest.
- `--dev-view`: a fake game of its own, no position (out of a raid there is no "you"); the app exits at the script's
  `exit`. `--verbose`: the app log (`$qa\data\logs`) has every step and each still's moment.
- Windows' animation effects on: with them off a preview shows its end at once. Keep the mouse off Shturmap's window.

Each folder holds `window.png` (the window; its map's panel empty), `map.png` (the map alone), `cue.png` while a cue is
up, `diagnostics.txt` and the developer view's `devview-build.png` and `devview-play.png`. Put `map.png` into the map's
panel of `window.png` to see what a player saw.

## What each still must show

Each still 2.5 s after the pointer went onto its line: the preview has staged its example, and a replay has played to
its end (in snapshots it shows its end at once).

- **replay**: Customs receded under the made-up raid's pen, seven positions from Old Gas Station toward Dorms V-Ex, each
  dot with its "N MIN IN" tag where one fits, the last one's "LAST SEEN · 4 MIN BEFORE THE END" framed in sand; under
  PREVIEW · NEW IN 0.4.0 · RAID REPLAY with AN EXAMPLE, NOT YOUR RAID. No tag under the + and − buttons, the floor list's
  place, WIKI MAP or the credit, nor under the PREVIEW label or over the grid's strips: a tag with no free place is left
  out (UI QA re-check of the grid's fixes, R2 and R7).
- **extracts**: Customs with the example list called out: YOUR EXTRACTS THIS RAID with its rows, its extracts lit, one
  marked "?", the rest struck out, no Scav extracts; the label for YOUR EXTRACTS THIS RAID.
- **clock**: the raid card's clock with example times on a plate over the dimmed map, its caption under it; under the
  plate the player's own Customs as it was before the card was pointed at: no lit or struck-out extracts, no roll call
  (R3: the extract list's example stood under it).
- **joined**: Woods, Gratitude's objectives on one spot as one marker, framed inside the strips.
- **stash**: the SEEN IN YOUR STASH cue with example items, over the player's own Customs: not Woods (R3).
- **notes**: the example quest card with two made-up notes on the plate, over the player's own Customs.
- **leaders**: Streets' Scav Checkpoint, its extract and transit set apart on leaders, framed inside the strips.
- **end**: the pointer gone: no label, no plate, no cue; the player's own Customs at the view it had.

In every still: every label, map name and tag inside the grid's strips and off the window's controls; the PREVIEW label's
words whole, the hint beside it or under it; in German the card's lines and the label in German.

## What would be a fault

- A preview's scene left under the next one's plate or cue: the extract list's lit extracts under the clock, Woods under
  the stash cue or the notes card (R3, 2026-10-26).
- A replay tag under a control or under the PREVIEW label, cut, or over the strips (R2 and R7, 2026-10-26).
- A preview still up in `end`, or the map in `end` another than the player's Customs.
- A plate cut, its caption missing, or a label's words cut.
