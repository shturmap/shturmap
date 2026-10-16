# QA replay: the extract list's roll call

The owner's ask of 2026-10-15: "When the extraction list is read, I want a nice fancy animation in line with the others
where a list of the extracts appears in the middle of the screen along with their icons. Then, the icons move where
extracts are (also outside of the current view pane) so it contextualizes them and the player knows where they are."
From a panel of four the owner chose A, "Roll call, then fly-out" (docs/DESIGN.md §4, principle 11, YOUR EXTRACTS THIS
RAID; "Map drawing", *A list just read is called out*; *Motion*). A list read from a screenshot taken in the last 30 s
opens the cue across the map's middle; from 0.5 s a row per listed extract rises in, nearest the position first (its
triangle, its name, "???" for a "??:??:??" one); from 1.2 s the view zooms out over 1.2 s to take in the listed extracts
and the position, where they aren't all in view already (where they are, it doesn't move); from 1.6 s each row's
triangle lifts off, 0.4 s after the one before, and flies 1.5 s to its place (owner, 2026-10-16: "too fast"; until then
0.16 s apart and 0.9 s), its row fading as it leaves and the band's dark going with the rows; it lands as its ring
starts; the title stays on its halo until 1.5 s after the last landing, then fades.

`extract-roll-call.devscript` replays it without a screenshot: the script's `exits` step hands the session a reading of
the game's list as a screenshot taken now would (`GameSession.DevReadExitsAsync`, from where text recognition hands its
reading over). The checks below are for its stills; the map's own motion frame by frame is `shturmap-cli frames customs
<folder> --scene list-callout` (docs/QA.md), and the rules are held by `ExitCallOutFramesTests` and `DevToolsTests`.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\extract-roll-call'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\extract-roll-call.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\extract-roll-call\t0` … `t4`; then `--culture de-DE` and `--window 900x560` for
  the other runs (docs/QA.md, "Replay scripts").
- `--dev-view`: a fake game of its own; the app exits at the script's `exit`. Follow my position stays off (a new
  session's setting), so nothing but the roll call moves the view. `--verbose`: the app log has every step, each still's
  moment, and a `Roll call:` line with each row's hand-off point (the centre of its triangle in the map panel's pixels)
  and its lift-off.
- Windows' animation effects on: with them off the band stands still for 5 s with all its rows, the view takes the
  extracts in at once and they are simply lit; nothing here would show. Keep the mouse off Shturmap's window.

Each folder holds `window.png` (the window; its map's panel empty), `map.png` (the map alone), `cue.png` while the cue is
up (the band alone, with its transparency: the log line `Snapshot: cue.png at 0,y DIP in the map's panel` says where it
stands), `diagnostics.txt` and the developer view's `devview-build.png` and `devview-play.png`. Put `map.png` into the
map's panel of `window.png` and `cue.png` over it at that place to see what a player saw.

## When each still is taken

A `snapshot` step takes 1 to 1.9 s: it saves `window.png` first and `map.png` after it (the log's `Snapshot saved`
line) in about 0.3 s at 900×560 and in about 1.0 s at the default window, then `cue.png` and the developer view's
pictures; the step after it follows 0.3 s later. So one roll call can't give a still at each of its moments, and a
still after a snapshot comes later at the default window than at the smallest. The script reads the same list in two
raids on Customs: the first for the roll call's start and the band gone (t0, t1), the second for its icons in the air,
its landings and its end (t2, t3, t4); the second raid also shows that a new raid calls its list out afresh. In the QA
pass's runs on the developer build (2026-10-16, both languages, the slower fly-out) the snapshot step began
(`window.png` its first picture), in seconds after its raid's `exits` line in the log: at the default window t0 0.65,
t2 2.52, t3 4.79, t4 8.13; at 900×560 t0 0.65, t2 2.53, t3 4.31, t4 7.08. t1 is timed for the small window, where t0
saves fastest: its wait of 2.2 s puts it at about 4.3 there and about 5.0 at the default window (the pass measured it
after a wait of 1.5 s, at 4.31 at the default window but at 3.60 at 900×560, 3.66 in German, when Railroad Passage was
still unlit and ZB-1013 in flight). The log gives each still's moment.

| From | A still taken then shows |
| --- | --- |
| 0 | The band across the map's middle: its dark, the gold rules, FROM YOUR SCREENSHOT over YOUR EXTRACTS THIS RAID · 4 OF 11 (DEINE EXTRACTS IM RAID · 4 VON 11), some of its letters still scrambled in gold before about 1.6. On the map the four listed extracts solid and unlit, the other PMC extracts struck out, the position's marker. |
| 0.5 | The rows rising in, 0.1 s apart, nearest the position first: Crossroads, Trailer Park, Railroad Passage (Flare), ZB-1013 ??? (Kreuzung, Anhängerstellplatz, …), each with its green triangle in a dark collar; all four by about 1.1. |
| 1.2 | On Customs, fitted whole, the listed extracts and the position are all in view already: the view doesn't move (coordinator, 2026-10-15: until then it panned to take them into its middle, which cut off the map's west and left empty map at the top). |
| 1.6 | Each row's triangle gone from its row as its icon leaves, a row 0.4 s after the one before (at 1.6, 2.0, 2.4 and 2.8), the row's name fading over 0.3 s; the band's dark and its rules thinning with them, gone by about 3.1; on the map the icons in flight from the rows to their places, 1.5 s each. |
| 3.1 | Icons landing, 0.4 s apart: each lights up and rings out (its triangle's outline leaving it), ZB-1013's "?" popping in; the last lands at 4.3. The title and FROM YOUR SCREENSHOT stand on their halo, the dark around their letters, over the map. |
| 5.8 | The title fading, gone by about 6.4; the rings over by about 6.0. |
| 6.4 on | The end, the same in every still: no cue, the floor picker back at the map's right; the view as the roll call left it. |

## What each still must show

- **t0** (first raid, about 0.65): the band up and its dark over the map's middle; the title decoding; the first row
  rising (`cue.png`, a moment later, has more of them). The rows' triangles are where the log's `Roll call:` line hands
  them over: the map panel's top left is `window.png`'s (384, 40) at 1600×900. No notice "Your extract list is read":
  the cue says it (2026-10-15).
- **t1** (first raid, about 4.3 at 900×560, about 5.0 at the default window): no band, no rules, no rows; the title
  and eyebrow on their halo (it stays to 5.8). On the map (put together as above) the view as it was, Customs whole,
  the listed extracts in it; all four landed (ZB-1013, the last, at 4.3, with its "?"), Crossroads, Trailer Park and
  Railroad Passage lit, the rings going or, in a `map.png` taken later at the default window, some of them over.
  Nothing lit that hasn't landed, no icon in flight.
- **t2** (second raid, about 2.5): the hand-off: Crossroads' and Trailer Park's rows gone, Railroad Passage's fading
  without its triangle, ZB-1013 ??? with its triangle still in its row (it lifts off at 2.8); the band's dark and rules
  more than half gone. `map.png` (later) has three icons in flight, the first close to its place, and nothing lit
  yet. In the second raid the roll call runs as in the first: a new raid on the same map calls its list out
  afresh.
- **t3** (second raid, about 4.8; 4.3 at 900×560): all four landed (the last at 4.3); the lit ones' outlines leaving
  them, ZB-1013 with its "?"; no band; the title on its halo (it fades from 5.8).
- **t4** (second raid, about 8.1; 7.1 at 900×560): the end: no cue; the three listed extracts glowing in green, ZB-1013
  solid with its "?", the other PMC extracts struck out (a faint outline with a stroke), no Scav extract on the map;
  nothing ringing; the view where the roll call left it. Follow my position is off, so nothing moves it back (principle 8).

At 900×560 the title takes two lines, drawn a little smaller to the map's column (owner, confirmed 2026-10-15; the app
log's `Roll call's cue:` line gives the sizes), and the band most of the column; the four rows stand in one column,
clear of the map's buttons, "???" after ZB-1013's name. Once the band's dark has gone, the title on its halo lies over the middle of the map, where some
icons land, until it fades: the design keeps it 1.5 s past the last landing.

## What would be a fault

- An icon that doesn't leave from its row's triangle (a jump between the row and the start of its flight), a row whose
  triangle stays after its icon has left, or one that fades before.
- The band's dark still standing over the middle while icons land there, or the title without its halo over the map.
- An extract lit before its icon lands, a struck-out one flying, the view moving back after the roll call, or a second
  roll call for the same list.
- The rows cut, a name broken inside a word, the band taller than the map's column (docs/DESIGN.md: twelve rows, the most
  a side has, are drawn smaller at the smallest window rather than cut).
- The title in three lines, or sticking out of its slot in a still taken while it decodes (`CueTitle` in
  `window.layout.json`; QA pass 3, 2026-10-15: its gold glyphs were wider than its letters, and at 1600×900 "7 OF" ended
  it); "???" inside a name broken over two lines rather than after its last line.
- A notice "Your extract list is read: …" beside the cue (it went with the cue, 2026-10-15).
