# QA replay: the update band and its cue, UPDATE READY

The owner's ask of 2026-10-15: "A Beta-tester did not realize there is an update, which shows me that it is currently
too subtle. I, however, do not want to have a blocking modal like a pop-up." From a panel of four the owner chose B, "A
cue, then the band" (docs/DESIGN.md §8, "Updates"; §4, principle 11, UPDATE READY; "Screen anatomy", *Rail*, *The
update band*). Until then a new version was one 11 px amber line at the top of what the rail scrolls. Now a band is
pinned under MAP between raids (a plate with an amber hairline: the update glyph, UPDATE READY, "0.6.1 · OKAMI", one
note line and RESTART NOW as a button), and the cue across the map's middle says it as the download completes, then once
more at the first menu after each raid while the update waits. The band glows in when it shows, at each return to the
menus, and as the cue fades.

`update-ready.devscript` replays it without an installed build: its `trigger update` takes the next patch of the running
version (0.6.0 gives 0.6.1, which keeps its line's name, OKAMI) for downloaded, as a download that just completed leaves
it (`Updater.DevReady`), so the band and the cue read the stage they read for a real one. RESTART NOW finds no download
there and says so ("its download is gone"): don't click it in this run. The rule of when the cue is due is held by
`UpdateTests` (`UpdateCue`); the checks below are for the stills.

## Run it

On a developer build (Debug, or one published with `ShturmapDev=true`) of the commit under test, from the repository's
root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\update-ready'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\update-ready.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\update-ready\t0` … `t4`; then `--culture de-DE` and `--window 900x560` (in the
  monitor's pixels) for the other runs (docs/QA.md, "Replay scripts").
- `--dev-view`: a fake game of its own, so nothing of the player's is read or changed, and no update is asked for or
  downloaded; the app exits at the script's `exit`. `--verbose`: the app log (`$qa\data\logs`) has every step, each
  still's moment, and the lines `Updates: developer aid, 0.6.1 taken for downloaded` and `Updates: the cue for 0.6.1
  (Ready)`, once for each cue.
- Windows' animation effects on: with them off the cue stands still for 5 s and the band never glows; t0's glow and
  t1's middle of the cue can't show. Keep the mouse off Shturmap's window.

Each folder holds `window.png` (the window, the cue in it while it is up; its map's panel empty), `map.png` (the map
alone), `cue.png` while the cue is up (the band alone, with its transparency; the log line `Snapshot: cue.png at 0,y DIP
in the map's panel` says where), `diagnostics.txt` and the developer view's `devview-build.png` and `devview-play.png`.

## When each still is taken

A `snapshot` step takes about 1.6 s (`window.png` first, about 0.3 s after its step) and the step after it follows 0.3 s
later. The cue comes once the map's middle has been free 0.5 s (checked every 0.25 s): about 0.5 s after `trigger
update`, and after the raid 6.9 to 7.3 s after `end` (RAID OVER is read about half a second after the step and stands
5 s; this raid has no positions, so no replay). In runs on the developer build (2026-10-15, both languages, both
sizes) `window.png` was taken, in seconds after the cue's log line: t0 about 0.6, t1 2.5 to 2.8, t2 6.8 to 7.2; t3
about 3.6 after `start`; t4 2.4 to 2.8 after the second cue's line.

| From the cue's start | A still taken then shows |
| --- | --- |
| before 0 | The band under MAP, the title's gold copy easing to ink (1.2 s) and the plate's faint flash (0.9 s) from `trigger update` on; no cue yet. |
| 0 | The cue springing open across the map's middle with its gold flash, UPDATE READY (UPDATE BEREIT), the title "0.6.1 · OKAMI" sliding up and decoding, its undecoded letters flickering in gold until about 1.6; the amber line RESTART NOW · TOP LEFT · NEVER IN A RAID (JETZT NEU STARTEN · OBEN LINKS · NIE IM RAID) under it, the gold rules shooting out. The floor picker stepped aside. |
| 1.6 | The cue whole and still: dark band, gold rules, the title decoded in ink. |
| 4.4 | The cue fading, gone at 5.0; from 4.6 the band glows again (gold title, faint flash), until 5.8. |
| 5.8 on | The band standing in ink, the floor picker back at the map's right. |

## What each still must show

In every still between raids, the band under MAP, above NEXT RAID and what scrolls: its amber hairline frame on the
panel's fill, the update glyph (two turning arrows) at its left, UPDATE READY in amber eyebrow type, "0.6.1 · OKAMI" in
the rail's title type, the note "Restarts Shturmap in a few seconds. Never in a raid." („Startet Shturmap in wenigen
Sekunden neu. Nie im Raid.“), wrapped and never cut, and RESTART NOW (JETZT NEU STARTEN) as a button with an amber
hairline and amber words. No × on it.

- **t0** (about 0.6 into the cue): the cue's entrance: the band's dark with its gold flash, the short gold rules
  shooting out, UPDATE READY, the title still decoding (gold flickering letters), the amber line under it. The band in
  the rail at the end of its own glow-in (it showed 0.5 s before the cue): its title ink or faintly warm. The notice
  "The tour stays in help …" at the map's top, from `tour end`.
- **t1** (2.5 to 2.8): the cue whole and still: UPDATE READY over "0.6.1 · OKAMI" decoded in ink, the amber line, the
  gold rules full width; the band in ink.
- **t2** (6.8 to 7.2): no cue; the band standing in ink (its second glow, from 4.6 to 5.8 as the cue faded, is over);
  the floor picker back at the map's right.
- **t3** (in the raid, about 3.6 after `start`): no band. The row under MAP holds the raid card's head: THIS RAID,
  CUSTOMS with PMC, 35 MIN LEFT; the status bar IN RAID · CUSTOMS · PMC. The RAID LOADING cue still up, with the first
  raid's line (a `--dev-view` session is a first start). No UPDATE READY cue: never in a raid or while one loads.
- **t4** (back in the menus, 2.4 to 2.8 into the second cue): the band back under MAP, in ink (its glow at the return
  to the menus was under RAID OVER); the UPDATE READY cue up again, after RAID OVER, its title decoded. The rail's
  last-raid line under the band: LAST RAID · CUSTOMS · 0 MIN · PMC.

The band's glow as the cue fades (4.6 s) falls between t1 and t2, where a snapshot's 1.6 s leaves no still; the app
log's `Updates: the cue for …` line and the code (`MainWindow.Updates`, `UpdateGlowAt`) time it.

At 900×560 the band's note takes two lines, as at 1600×900 (the rail is 384 px wide at every size), and the rail under
it is shorter. The cue's title fits the map's column; its amber line keeps clear of the map's buttons (it wraps short
of them where it must): in German "JETZT NEU STARTEN · OBEN LINKS · NIE IM RAID" stands on one line that ends some 20 px
left of the buttons' column.

## What would be a fault

- The band scrolling with the rail, or cut at either side; a note cut instead of wrapped; the button's words cut.
- The band in a raid or while one loads (t3), or the raid card's head missing from its row.
- The cue in a raid (t3), a second cue for the same raid, or none at t4.
- The cue at t4 while RAID OVER is still up (it waits for it), or RAID OVER cut short by it.
- A notice in place of the cue, or the old line ("UPDATE 0.6.1 READY: APPLIES AT NEXT START") anywhere.
