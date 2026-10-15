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

A snapshot takes about a second, and holds the window's thread while it draws the map, so neither the stills nor the
chapter keep to the script's times: each still's moment is in the app log. The click is the line `Developer view:
script: whatsnew click 4`; a still is taken between its `script: snapshot …\tN` line and its `Snapshot saved to
…\tN` line (`window.png` first, then `tour.png`, `map.png` last).

The chapter, in seconds after the click, each later step pushed back by the snapshots taken before it:

| From | Chapter 5 shows |
| --- | --- |
| 0 | The window dims, no cut-out. The band at the map's foot: TOUR · 5 OF 7, YOUR SCREENSHOT KEY decoding, its two lines. Customs framed on the example position, above the band. The key plate in the middle: the screenshot key drawn large, SCREENSHOT, POSITION FROM THE FILE NAME. |
| about 1.0 | The key sinks and comes back up; from about 1.7 the file name decodes under it, `…_x, y, z_…`. |
| about 3.2 | The plate goes. The example position appears and pings; one square cut-out, centred on it. |
| about 5.6 | The example extract list at the map's top right: IN THE GAME: O TWICE, THEN (the key), the names coming in one after another, one of them `· ???`; a second cut-out around the list. At about 7.8 its frame flashes amber and YOUR LIST THIS RAID: n OF m EXTRACTS shows in green. |
| about 9.3 | The list fades. The view moves for 1.2 s to take in the listed extracts and the position, above the band; then one cut-out glides around them. |
| about 11.2 | The listed extracts light up one after another, nearest the position first, their triangles ringing out three times; then the `??:??:??` one's "?" pops in. The other PMC extracts are struck out (a faint outline with a stroke). The map is a PMC raid's: no Scav extract is drawn (teal triangles), so no lit or struck-out extract has an unlit twin beside it on a leader. |
| about 14.5 on | The chapter's end, held until NEXT, Esc or the exit. |

## What each still must show

In every still, t0 to t6:

- `tour.png`: the tour's layer up, the band saying TOUR · 5 OF 7 and YOUR SCREENSHOT KEY.
- `map.png`: Customs. Never Woods, the map this replay's "player" has on screen.
- `window.png`: the label over the map reads PREVIEW · THE TOUR · CUSTOMS, with AN EXAMPLE, NOT YOUR RAID: the
  tour's own label. No What's New label (PREVIEW · NEW IN 0.5.0 · …): a tour line previews nothing. Line 4 of the card
  in the rail without the linked tint.
- Every cut-out on its part: the square centred on the example position's marker, the list's cut-out around the list,
  the last one around the lit extracts and the position.

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
