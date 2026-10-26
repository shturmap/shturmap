# QA replay: the newest What's New card, line by line

The release UI QA pass of 0.7.0 (2026-10-26) checked each line of 0.7.0's What's New card with a script of its own; this
is that script, made to take the newest card, whatever the build's version. What's New is a card at the top of Plan's
rail after an update (docs/DESIGN.md §4, "Screen anatomy", *What's New*; its lines are docs/whats-new.md's newest
section, `docs/whats-new.de.md`'s in German). A line is `- <preview> · <name> · <what it is>`: pointing at a line with a
preview (`replay`, `extracts`, `stash`, …) stages its example on the map under a PREVIEW label; a `tour:N` line previews
nothing, and a tap on it opens the tour at chapter N (*The tour*, "Keeping it current"). A tap on any other line does
nothing. The rules are held by `WhatsNewTests` and `TourTests`; the checks below are for the stills.

`whatsnew-lines.devscript` shows the card (`whatsnew`, the newest version's, as help's link shows it), then for each of
its lines points at it (`whatsnew <n>`), taps it (`whatsnew click <n>`) and ends the tour if one opened (`tour end`).

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

- The stills go to `%TEMP%\shturmap-qa\whatsnew-lines\card`, `line-1`, `line-1-tap` … `line-5`, `line-5-tap` and
  `end`; then `--culture de-DE` and `--window 900x560` for the other runs (docs/QA.md, "Replay scripts").
- `--dev-view`: a fake game of its own; the app exits at the script's `exit`. `--verbose`: the app log
  (`$qa\data\logs`) has every step, each still's moment, and for each tap on a `tour:N` line `Tour opened (whatsnew)
  at chapter N`.
- A card has up to five lines. On one with fewer, the steps past its last line say `What's New <version> has <n> lines`
  in the log and change nothing: their stills are the one before them again.
- Windows' animation effects on: with them off a preview and a chapter show their end at once. Keep the mouse off
  Shturmap's window.

Each folder holds `window.png` (the window; its map's panel empty), `map.png` (the map alone), `tour.png` while the tour
is up (its layer alone, with its transparency), `cue.png` while a cue is up (a chapter's staged cue), `diagnostics.txt`
and the developer view's `devview-build.png` and `devview-play.png` (taken after the step, while the script goes on).
Put `map.png` into the map's panel of `window.png`, and `tour.png` over both, to see what a player saw.

## When each still is taken

A still is taken 2 s after the pointer went onto its line (a preview has staged its example by then) and 3 s after a
tap (the chapter has opened and begun to stage). A `snapshot` step takes about half a second here, the map standing
still (2026-10-26, Debug build, the default window 2560×1392); the log gives each still's moment.

## What each still must show

Read the card's lines from docs/whats-new.md's newest section first (in German from `docs/whats-new.de.md`): line n's
preview, name and words.

- **card**: the card at the top of Plan's rail, under MAP: NEW IN <version> · <NAME> (NEU IN …), its lines in the
  section's order, each with its name and words, wrapped and never cut, the note under them saying what its lines do
  (DESIGN.md: "Point at one: the map shows it." where each previews, "Click one: the tour shows it." where each is a
  `tour:N` line, "Point or click: the map or the tour shows it." where there are both; in German as `ViewTexts.de.resx`
  has them), in one line. The map is the player's own (Customs), with no PREVIEW label.
- **line-n** (the pointer on line n): for a line with a preview, the map shows its example under the label PREVIEW ·
  NEW IN <version> · <name> (and its hint where it has one); the line tinted as the one pointed at. For a `tour:N`
  line, nothing staged: no label, the map as in **card**, and the line without the linked tint.
- **line-n-tap**: for a `tour:N` line, the tour up at chapter N: `tour.png`'s band says TOUR · N OF 7 (TOUR · N VON 7)
  with that chapter's title as docs/tour.md (`docs/tour.de.md`) has it, its lines, ← BACK, NEXT → and ESC ENDS THE TOUR;
  the dim with the chapter's cut-outs around the parts it frames; the log's `Tour opened (whatsnew) at chapter N`. A
  chapter that stages on the map (5, 6) shows its label PREVIEW · THE TOUR · <map> with AN EXAMPLE, NOT YOUR RAID. For
  any other line, the same as **line-n**: a tap does nothing there.
- **end**: the tour ended (`tour end`), the card as in **card**, the map the player's own, no label; the notice "The
  tour stays in help …" at the map's top.

In every still: the card's words whole (the layout file `window.layout.json` with no problem), in German its German
lines; nothing of an earlier line's preview or chapter left on the map once the pointer has moved on.

## What would be a fault

- A `tour:N` line that previews something, a tap on it that opens another chapter or none, or a chapter's band with
  another number.
- A preview line that shows nothing, another line's example, or its example on the wrong map; a preview still up after
  the pointer moved to the next line.
- The tour's chapter over the player's map with the chapter's own map gone (the fault of `whatsnew-tour-line`,
  2026-10-15), or the tour still up in the next line's still.
- A line's words cut, a name broken inside a word, or an English line in the German run (or a German one in English).
