# QA replay: a Tasks screenshot's operational tasks, read

The owner's ask of 2026-10-17: "Add dailies and weeklies via screenshot." From a panel of four the owner chose B,
"OPERATIONAL rows where the map's work is" (docs/DESIGN.md §2, "Menu screenshots"; §4, principle 11, no cue; "Screen
anatomy", *Operational tasks*). A screenshot of the game's Tasks screen, OPERATIONAL tab, is read in the menus; its
tasks show as framed glyphs on NEXT RAID's rows, as an OPERATIONAL section in the open card and the raid card, and as a
Scav's hand-over items under FIND IN RAID. The rows a reading brought glow in (the synopsis in gold easing to ink in
1.2 s, the row's faint flash in 0.9 s), and a notice says how many were read: no cue.

`operational-feedback.devscript` replays it without a picture: the script's `tasks 5 ended` hands the session the table
a screenshot taken now would show, from where the reading hands its rows over on (`GameSession.ReadExampleTasksAsync`):
a survival on Streets of Tarkov ending in 25 minutes (4 of 7), a kill with its condition on Customs, a weekly's two
hand-overs on any map ("SSD drive", "CPU fan"), a kill on Shoreline whose countdown went unread (1 of 5), a line of no
known kind on Woods ("Mark the made-up spots", 0 of 2), and one more on Woods that runs out 3 s later. The operational
tasks' file isn't written that run. The rules are held by `OperationalRailTests`, `OperationalTasksTests` and
`OperationalSessionTests`; the checks below are for the stills. Drafted by the UI QA pass of 2026-10-19.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\operational-feedback'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\operational-feedback.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\operational-feedback\t0`, `t1`, `shoreline`, `woods` and `raid-start`; then
  `--culture de-DE` and `--window 900x560` for the other runs (docs/QA.md, "Replay scripts").
- `--dev-view`: a fake game of its own; the app exits at the script's `exit`. `--verbose`: the app log
  (`$qa\data\logs`) has every step, each still's moment, and the lines `Example operational tasks: a table of 6 rows`,
  `Operational tasks read from a menu screenshot: 6 rows` and, 3 s later, `Operational tasks: 1 ran out`.
- Windows' animation effects on: with them off nothing glows, and t0's gold can't show. Keep the mouse off Shturmap's
  window.

## When each still is taken

A `snapshot` step saves `window.png` about 0.3 s after its step, and the step after it follows about 1 s later. In the
QA pass's runs (2026-10-19, en-US and de-DE, 900×1500) `window.png` was taken, in seconds: t0 0.6 after `tasks` (the
synopses' gold half eased to ink, the flash nearly gone); t1 5.6 to 5.7 after `tasks` (2.6 after the sixth task ran
out); shoreline and woods 2.4 after their `map … look`; raid-start 3.4 after `start`.

## What each still must show

- **t0**: the notice "Operational tasks read: 6." („Dailies und Weeklies gelesen: 6.“) at the map's top; no cue, no view
  moved. NEXT RAID lists the maps with operational work and none of your quests at its end, by name, without counts:
  CUSTOMS and SHORELINE each with a framed kill glyph, STREETS OF TARKOV with a framed survival glyph, WOODS with the
  framed stopwatch and a survival glyph (the task about to run out). The open Streets card says, muted, that none of
  your quests is on it; its OPERATIONAL eyebrow with "AS OF hh:mm" („STAND hh:mm“) at its right; the rows "Survive and
  extract" with "4 OF 7 · 24 MIN LEFT" („4 VON 7 · NOCH 24 MIN“), and after a hairline "Find in raid: SSD drive, CPU
  fan" („FiR finden: SSD drive, CPU fan“, the items' names the game's) with its two item cells and "ANY MAP · HAND
  OVER AFTER · 2 D LEFT" („JEDE MAP · DANACH ABGEBEN · NOCH 2 TAGE“). Both rows glowing: gold words easing to ink
  over the row's faint gold.
- **t1**: no glow; under the eyebrow, muted, "1 ended at hh:mm. Screenshot your tasks for the new one." („1 endete um
  hh:mm. Mach einen Screenshot deiner Dailies und Weeklies für die neue.“); WOODS's row with the stopwatch alone.
- **shoreline**: Shoreline's card (looked at, Streets still chosen): the ended line, the kill "Kill 5 PMC operatives"
  („5 PMC operatives töten“: the target is the game's words as read) with "1 OF 5 · READ JUST NOW" („1 VON 5 · GERADE
  GELESEN“: its countdown wasn't taken, so the reading's age), then the any-map hand-over after a hairline.
- **woods**: Woods' card: the ended line, "Mark the made-up spots" as read with the generic operational glyph (the
  stopwatch; pointing at it says what it is) and "0 OF 2 · 8 H LEFT" („0 VON 2 · NOCH 8 H“), then the hand-over.
- **raid-start** (in the raid, about 3.4 after `start`): THIS RAID's card for Streets of Tarkov, 40 MIN LEFT; its
  OPERATIONAL after the bosses' line, without "As of": the ended line; "Survive and extract" with "4 OF 7 · ENDS IN 24
  MIN" („4 VON 7 · ENDET IN 24 MIN“), the time in the clock's red (it ends before the raid could; rounded down, as
  Plan's); after a hairline the hand-over with its two item cells, ANYWHERE („ÜBERALL“) where a quest row has its
  distance, and the muted line "HAND OVER AFTER THE RAID" („NACH DEM RAID ABGEBEN“) on one line, without "ANY MAP".
  The RAID LOADING cue still up, with the first raid's line (a `--dev-view` session is a first start).

At 900×560 the rail scrolls: the OPERATIONAL section is further down, and the rows and lines are as at 900×1500 (the
rail is 384 px wide at every size).

## What would be a fault

- A cue for the reading, or no notice; a view that moved.
- A row that doesn't glow at t0, or glows again at t1.
- A row's muted line broken inside a part ("HAND OVER AFTER THE / RAID"), or a two-word item broken over two lines
  ("CPU / fan").
- "ANY MAP" beside ANYWHERE in the raid card, or a red time that says more than Plan's at the same moment.
- The hand-over's item cells missing in either language (QA, 2026-10-19: a German app's catalogue matched none).
- A date in the eyebrow in English's form in German ("18 Okt." for „18. Okt.“), when the reading was on an earlier day.
- Woods' ended task still on its row at t1, or the ended line missing from a card's section.
