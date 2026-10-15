# QA replay: What's New's tour line

The owner's report of 2026-10-15, on 0.5.0's What's New line "Your extracts, clearer" (`tour:5`): "the highlight does
weird movements and highlights the wrong things sometimes". A click on the line opens the tour at chapter 5, YOUR
SCREENSHOT KEY, which stages an example on Customs. The tour's layer then covered the line, the pointer's next move
left it, and its leaving asked the preview machinery for the end of a preview: 0.3 s later the chapter's map was taken
down and the player's own map put back under the tour, while the chapter went on staging on the map that had gone.
With Follow my position on, a second fault: the example position glided the view off the chapter's framing for 2.4 s
under a cut-out placed before the glide. docs/NEXT.md, "2026-10-15", has the causes and the fix.

`whatsnew-tour-line.devscript` replays it without a mouse. The checks below are for its stills; nothing here is
checked by a test (the tests cover the rules and the script's steps, not the window).

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\whatsnew-tour-line'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\whatsnew-tour-line.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --follow --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The script's path is given whole: the app's working folder is `%USERPROFILE%`, not the repository. The stills go to
  `%TEMP%\shturmap-qa\whatsnew-tour-line\t0` … `t6` (the `snapshot` step expands `%TEMP%`); a snapshot writes over
  what its folder had, so each moment has its own.
- `--dev-view`: a fake game of its own, so nothing of the player's is read or changed; the developer view opens beside
  the window, and the app exits by itself at the script's `exit`. `--follow`: Follow my position on, for the second
  fault. `--verbose`: the app log has every step and every snapshot to the millisecond. `--data`: that log in the QA
  folder, `$qa\data\logs` (the session's settings are the fake game's own); the download cache stays the shared one.
- Windows' animation effects on (Settings, Accessibility, Visual effects): with them off each chapter shows its end at
  once, and nothing here can show. Keep the mouse off Shturmap's window: a real pointer over it raises real events.
- A `--dev-view` session starts on settings of its own, as at a first start, so the tour opens by itself at chapter 1;
  `tour end` ends it first, and its notice ("The tour stays in help …") is gone by the click.

Each folder holds `window.png` (the window without the tour; its map's panel is empty), `map.png` (the map alone),
`tour.png` (the tour's layer alone, with its transparency: the dim, the cut-outs and their amber corner marks, the
plate, the band), `diagnostics.txt`, and the developer view's `devview-build.png` and `devview-play.png`. Put
`map.png` into the map's panel of `window.png` and `tour.png` over both to see what a player saw.

## When each still is taken

A `snapshot` step takes about 1.7 s: the window's pictures about 0.7 s, holding the window's thread while it draws the
map, then the developer view's two. A step of the chapter that falls due while the thread is held comes when it is
free, so neither the stills nor the chapter keep to the script's times: each still's moment is in the app log. The
click is the line `Developer view: script: whatsnew click 4`; a still is taken between its `script: snapshot …\tN` line
and its `Snapshot saved to …\tN` line (`window.png` first, `tour.png` about 0.2 s later, `map.png` last). In runs on a
Debug build the stills were taken about 0.4, 3.4, 7.3, 9.5, 13.2, 18 and 22.6 s after the click.

A still shows one moment: what the chapter does over time shows in it as where it was by then. In seconds after the
click, give or take the snapshots before it:

| From | A still taken then shows |
| --- | --- |
| 0 | The window dimmed, no cut-out. The band at the map's foot: TOUR · 5 OF 7, the title YOUR SCREENSHOT KEY (some of its letters still scrambled before about 0.7), its two lines, ← BACK, NEXT → and ESC ENDS THE TOUR. Customs framed on the example position, above the band; no position drawn. The key plate in the middle, faint before about 0.3: the screenshot key drawn large, SCREENSHOT, POSITION FROM THE FILE NAME. |
| about 1.0 | The key down or on its way back up, its frame amber, until about 1.6; from then the file name under it, `…_x, y, z_…`, some of its characters still scrambled before about 2.2. |
| about 3.2 | No plate. The example position's marker with a ping ring around it (at whatever size the ping has reached) and one square cut-out centred on it, faint before about 3.6. |
| about 5.6 | Also the example extract list at the map's top right, in a second cut-out: IN THE GAME: O TWICE, THEN (the key), and the names in by then, the last one perhaps still scrambled; one of them ends `· ???`. From about 7.8 YOUR LIST THIS RAID: n OF m EXTRACTS in green under them; a still between about 7.8 and 8.2 has the list's frame amber, a later one grey again. |
| about 9.3 | The list faint, or gone after about 9.6; the view on its way to the listed extracts and the position, above the band, or there after about 10.8; the cut-outs where they were until then, after that one cut-out on its way to them or around them. |
| about 11.2 | The listed extracts lit, nearest the position first, a later one perhaps not yet; a ring around the triangles of those ringing out (at whatever size it has reached); the `??:??:??` one's "?" once all are lit. The other PMC extracts struck out (a faint outline with a stroke). The map is a PMC raid's: no Scav extract is drawn (teal triangles), so no lit or struck-out extract has an unlit twin beside it on a leader. |
| about 14.5 on | The chapter's end, the same in every still until NEXT, Esc or the exit. |

## What each still must show

In every still, t0 to t6:

- `tour.png`: the tour's layer up, the band saying TOUR · 5 OF 7 and YOUR SCREENSHOT KEY, with ← BACK, NEXT → and
  ESC ENDS THE TOUR (`tour.texts.txt` lists them; the layout check reports one missing).
- `map.png`: Customs. Never Woods, the map this replay's "player" has on screen.
- `window.png`: the label over the map reads PREVIEW · THE TOUR · CUSTOMS, with AN EXAMPLE, NOT YOUR RAID: the
  tour's own label. No What's New label (PREVIEW · NEW IN 0.5.0 · …): a tour line previews nothing. Line 4 of the card
  in the rail without the linked tint. The rail and the status bar with their words (`window.texts.txt` has some sixty
  lines; a window of empty frames is the fault of the QA pass of 2026-10-15, below).
- Every cut-out on its part, or on its way from one to the next (they glide): the square centred on the example
  position's marker, the list's cut-out around the list, the last one around the lit extracts and the position.

Each still, against the table above:

- **t0**: the first moments: the dim without cut-outs, the key plate (fading in or in), Customs framed on the
  position, with no position drawn yet.
- **t1**: the key pressed or its file name decoding or decoded; with a slow snapshot, the ping and its square cut-out.
- **t2**: the ping and the square cut-out with the marker in its middle, or the extract list coming in.
- **t3** (after `whatsnew leave`): the same moment as t2 or a later one, and nothing else changed: no map but
  Customs, the label still there, the cut-outs still on their parts. What differs is only the chapter's own motion (more
  names on the list, the read line, the list fading, the view moving).
- **t4**: the list read or fading, the view moving or moved, or the extracts ringing out, as far as a still shows it.
- **t5** and **t6**: the chapter's end: the view on the listed extracts and the position, above the band; the
  listed ones lit, one with a "?", the other PMC extracts struck out, no Scav extract anywhere on the map; one cut-out
  around them. t6 the same as t5: nothing moves at the end.

While the view moves (t4, if it catches the move), a symbol set apart on a leader can jump from one frame to the next:
that is how the map places crowded symbols at each zoom, not this fault, and it is left as it is.

## What the fault looked like

Before the fix, in this replay:

- **From 0.3 s after `whatsnew leave`** (in a player's session, after the pointer's first move once the tour was up):
  `map.png` shows Woods, the player's own map, at the view it had before the click; the label in `window.png` is gone;
  `tour.png` still shows chapter 5, its band, plate and cut-outs, now over whatever Woods has there.
- **At the view's move**: the view goes where the framing worked out on Customs puts it, applied to Woods: some other
  part of Woods, or past its edge ("weird movements"). The last cut-out frames Woods' ground, not extracts ("highlights
  the wrong things"), and nothing rings out: the rings went to Customs' scene, no longer drawn.
- **With Follow my position on**, from the ping on, a fault of its own: the view glided for 2.4 s from the chapter's
  framing to the position (a sixth of the view ahead of it, the way the example faces), while the square cut-out stayed
  where the position had been: the marker slid out of the middle of its square.
- A tour ended at that chapter left the player's map where the chapter's move had taken it.
- Even without the fault, Customs' Scav extracts were on the chapter's map: five of them, under 5 m from a PMC extract
  of the same name (Crossroads, RUAF Roadblock, Trailer Park, Old Gas Station, ZB-1011), stood solid teal and unlit
  beside the lit or struck-out one, set apart on a leader.

A fault of the quiet runs, found by the QA pass of 2026-10-15: the script's window, shown without activation, never
had its texts bound in XAML set, since the tour that opens by itself at its start took help's place and nothing
activated the window (DESIGN.md §8, "Developer aids", *Quiet runs*). Every `window.png` was frames without a word
(`window.texts.txt`: SHTURMAP and "?"), the What's New card had no lines, so `whatsnew click 4` said "line 4 of What's
New 0.5.0 isn't in the window", and all seven stills showed Woods without the tour.
