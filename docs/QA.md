# UI QA

How a change to what the player sees is checked before it counts as done (owner, 2026-10-15: "reviewing passes where
an agent acts as a expert UI QA entity that tests the User interface and the flow"). The 0.5.0 What's New card's tour
line moved its frames oddly and lit the wrong parts, and no one had looked at it. A pass uses the app's own headless
checks only (DESIGN.md §8, "Rules": verify UI with snapshots, never by capturing the screen) and judges what they show
against `docs/DESIGN.md`.

A pass is made by someone who didn't make the change: the **UI QA tester**, an agent in the role of an expert UI QA
tester of a second-monitor game companion, adversarial, patient and exact, who tests what the player sees and does, not
the code, and judges against DESIGN.md, never against taste. Locally it is `.claude/agents/ui-qa.md` (the `.claude`
folder isn't in the repository); its rules are these, so any reviewer applies the same. It never edits product files:
it returns findings, the change's author fixes them, and it checks again.

## When a QA pass runs

- After any change to what the user sees (the window, a card, a cue, a dialog, the map's drawing, a text, a flow),
  before the change is reported done.
- Before every release (docs/UPDATES.md, step 6).
- After a layout change, in any language.

Its findings are fixed, or reported to the owner as open. After the fixes, a new pass from fresh builds, until it says
PASS.

## Builds and folders

- **The folder build** (Release, what players run, no developer tools): `.\eng\publish.ps1` →
  `artifacts\Shturmap\Shturmap.exe`.
- **Developer builds** (the layout files beside each picture, `--whats-new`, `--dev-view --dev-script`,
  `--switch-language`, `qps-ploc`): for the layout check and `--whats-new`, `.\eng\dotnet.ps1 publish
  src\Shturmap.App\Shturmap.App.csproj -c Release -o artifacts\layout-check\app '-p:ShturmapDev=true'`, what
  `tools\layout-check.ps1` builds when given no `-Exe`; for the replay scripts, the Debug build of `.\eng\dotnet.ps1
  build Shturmap.slnx`, as their `.md` says. Not `eng\dev.ps1`: it also packs into the dev feed, and the owner's
  installed dev app takes that build at its next start.
- All from the commit under review, again after each fix; the app log's first line names the build and commit.
- The CLI: `.\eng\dotnet.ps1 run --project tools\Shturmap.Cli -- <command>` (`shturmap-cli`).
- `$pass`: one folder per pass, outside the repository (an agent's session scratchpad); each run gets its own folder in it.
  Paths absolute and without spaces (`fake-raid.ps1` passes `-Out` and `-MoreArgs` to the app as they are). The replay
  scripts' stills go where the script names them, `%TEMP%\shturmap-qa\<flow>\`, and are copied into `$pass` after each run.
- Every app run gets `--data <a folder of its own>`: its app log lands beside its pictures, and nothing goes into
  `%LOCALAPPDATA%\Shturmap-dev`. `tools\layout-check.ps1` takes it through `-MoreArgs`, as `fake-raid.ps1` does.
- Sizes are DIP: 900x560 the smallest window, 1600x900 the large one. The layout check converts them to pixels at the
  scale of the monitor the app would open on (its `summary.md` names it); `fake-raid.ps1 -Window` (`--window`) takes
  pixels: the DIP size times that scale.

## What it runs

Every pass runs the replay scripts in `tools\qa\` (below), and the checks of each row the change reaches:

| change | checks |
| --- | --- |
| the rail, a card, a cue, a dialog, the status bar, help, settings, a text | views plan, raid, quest (help and settings in each) in en-US and de-DE at 900x560 and 1600x900, folder build; the layout check for those views in en-US, de-DE and qps-ploc at both sizes |
| map drawing | `shturmap-cli render` of the maps it reaches; `shturmap-cli frames`, both scenes, with and without `--glide`; the raid view at both sizes |
| an animation or a flow | the replay scripts; one drafted in the pass folder for a flow without one; `frames` for the map's animations |
| the layout, in any language | the layout check, every view, en-US, de-DE and qps-ploc, both sizes |
| a release | all of the above for every view; the tour's seven chapters in both languages; What's New and each line's preview; the layout check in full with `-SwitchCheck` |

```powershell
# Views (folder build): plan, raid (a PMC raid on Streets, a list read, a walk), quest (Dandies' card and its item's)
.\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out $out -PlanOnly -Culture de-DE -Window <px> -MoreArgs '--data', "$out\data"
.\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out $out -Culture de-DE -Window <px> -MoreArgs '--data', "$out\data"
.\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out $out -PlanOnly -ShowQuest Dandies -Culture de-DE -Window <px> -MoreArgs '--data', "$out\data"
# The layout check (developer build); -States: plan, raid, report, crash, whatsnew, quest, tour1 … tour7
.\tools\layout-check.ps1 -Out "$pass\layout" -Exe artifacts\layout-check\app\Shturmap.exe -Languages en-US,de-DE,qps-ploc -Sizes 900x560,1600x900 -States plan,raid,quest -MoreArgs '--data', "$pass\layout\data"
# Map drawing; each frames run again with --glide into a folder of its own
.\eng\dotnet.ps1 run --project tools\Shturmap.Cli -- render streets-of-tarkov "$pass\render\streets.png" --data "$pass\render\data"
.\eng\dotnet.ps1 run --project tools\Shturmap.Cli -- frames customs "$pass\frames\list-read" --data "$pass\frames\data"
.\eng\dotnet.ps1 run --project tools\Shturmap.Cli -- frames customs "$pass\frames\whatsnew" --scene whatsnew-extracts --data "$pass\frames\data"
# A release: the tour's chapters (also with -Culture de-DE), What's New (developer build), the full layout check
foreach ($n in 1..7) { $out = "$pass\tour\$n"; .\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out $out -PlanOnly -Tour $n -MoreArgs '--data', "$out\data" }
$out = "$pass\whatsnew"; .\tools\fake-raid.ps1 -Exe artifacts\layout-check\app\Shturmap.exe -Out $out -PlanOnly -MoreArgs '--whats-new', '--data', "$out\data"
.\tools\layout-check.ps1 -Out "$pass\layout" -Exe artifacts\layout-check\app\Shturmap.exe -SwitchCheck -MoreArgs '--data', "$pass\layout\data"
```

Other states where a change reaches them: `-Scav`, `-LocalRaid`, `-HoldLoading`, `-GroupPick`, `-Map lab` (a sheet),
`-MoreArgs '--show-report'` or `'--show-crash'`. `render` also takes made-up screenshot names (dated 2026-01-01) for
positions, `--focus-item <item>` and `--pick <quest>`; `frames` 36 frames 100 ms apart (`--every`, `--count`, `--fix`),
`summary.txt` first. What each tool writes: DESIGN.md §8, "Developer aids", and docs/LANGUAGES.md, "Layout check".

## How it looks

- **Every picture** a run wrote, opened: `window.png`, `map.png`, `help.png`, `settings.png`, `card*.png`, `tour.png`,
  `devview-*.png`, every `*.problems.png`, and of `frames` the first, each frame something rings or moves in, each it
  flags and the last. A picture not opened is a check not run; one empty, cut short or missing is a finding.
- **The popups not empty**: every `settings.png` and `help.png` of a quiet run must show its panel. They are drawn on
  transparency, so an empty one looks like a blank page: count the pixels that aren't transparent
  (`[Drawing.Bitmap]::FromFile(…).GetPixel(x, y).A`); none is a finding.
- **Known open**, re-checked first in every pass: on 2026-10-15, at f028dff, a quiet run's `settings.png` came out
  empty (480×655, every pixel transparent) while `window.png`, `map.png` and `help.png` rendered:
  `.\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out $out -PlanOnly -MoreArgs '--data', "$out\data"` →
  `settings.png`. A finding until it renders.
- **Against the words**: DESIGN.md's for that view (what shows, in which order, colour, type role, size; what is lit,
  framed, pulsing, stepped back), `docs/tour.md` and `docs/whats-new.md` (and their German files), a replay's `.md`.
- **Against the last pass**, where one is kept: what changed that the change didn't mean to.
- **The states DESIGN.md names**, where the change reaches them: animation effects off (a `--snapshot` run draws the
  tour and the raid replay without motion; elsewhere it is Windows' setting, which only the owner changes; a replay
  that needs it on reads it first, `SystemParametersInfo` with SPI_GETCLIENTAREAANIMATION, and isn't run when off), both
  languages, the pseudo-language for layout, the smallest window, PMC and Scav, Plan and Raid, a sheet map, no game.
- **The app log** of each run (`<out>\data\logs\shturmap-*.log`): any ERROR or WARN the run didn't provoke on purpose;
  the window line, which must say "Window off every monitor, …" (and a script's run "Developer view off every monitor,
  beside the window: …"). A run that opened on a monitor stops the pass: it may have taken the focus from a game.
- **The texts**: a word on screen not in a `<Name>Texts.resx` (without brackets in the pseudo-language), English in the
  German run, a placeholder shown raw.

## What counts as a finding

Anything the player would see that DESIGN.md doesn't say: overlap; clipping, a trim or a word broken where the design
doesn't allow it; a wrong state (the wrong map or view, a card or preview left behind); a symbol lit, framed, tinted or
pulsing that shouldn't be, or not where it should; motion where *Motion* names none or none where it names some, a frame
that jumps or goes into nothing; a colour off the token table; a text not in the texts files; a flow that ends in
another state than its `.md` says; an ERROR or WARN; a picture empty or missing; a planned check not run. Where
DESIGN.md says nothing of what a picture shows, it is a finding for the owner to decide.

- **BLOCKS**: the player reads something wrong or loses the way (a wrong map, state, extract, distance or time; the
  glance unreadable at 900x560; a UX principle broken; a flow ending in the wrong state; a window on a monitor; an ERROR).
- **SHOULD**: a visible defect the design contradicts, the right answer still readable (an overflow, trim or overlap in
  an offered language, motion off its timing, a symbol off the visual language, a text not in the texts files, a WARN,
  a check not run).
- **NIT**: a few pixels off the spacing or alignment, nothing misread; a pseudo-language overflow.

## How it reports

One line per finding, numbered, then the runs and a verdict:

```
FINDING <n> · BLOCKS|SHOULD|NIT · <view or flow> · <what the picture shows> · expected: <DESIGN.md §…, "<its words>"> · repro: <command> → <file>
PASS FOLDER: <the pass folder, the user folder written %USERPROFILE%>
CHECKED:
- <tool, view or flow, language, size, build and commit> → <its folder>
PASS | FAIL (<n> blocks, <n> should, <n> nits)
```

A check that didn't run is a finding: `· not run: <why> ·`. `PASS` only when every planned check ran and found nothing.

## Replay scripts

`tools\qa\` holds the replays of flows, one `<flow>.devscript` and one `<flow>.md` of expectations per flow; the QA pass
runs every script there, on the Debug build, as its `.md` says, then again in de-DE and at the smallest window. The
first is `whatsnew-tour-line` (the 0.5.0 card's tour line).

- **`<flow>.devscript`**: a developer script (DESIGN.md §8, "Developer aids", `--dev-script`; `DevScript.Verbs`), for
  instance `tour end` (a `--dev-view` session starts as a first start, and the tour opens by itself), `quest start <id
  or name>`, `pick`, `show`, `hover`, `point`, `whatsnew [<n>]`, `whatsnew click <n>` (taps line n, as a click does),
  `whatsnew leave` (the pointer leaves the line), `note <quest id or name> <text>` (a quest note through the session),
  `cards`, `wait <s>`, `snapshot <folder>`, `exit`. Each `snapshot` names `%TEMP%\shturmap-qa\<flow>\<moment>`, a
  folder per moment; the app expands `%TEMP%`, so a script in the repository names no user's folder.
- **`<flow>.md`**: the flow and the fault it guards against, "Run it" (its command line, with its own flags), when each
  still is taken, and per still what it must and must not show, with the DESIGN.md words it holds to.
- `DevToolsTests` read every script here: a `.md` beside it, every step one the app knows, every still under
  `%TEMP%\shturmap-qa\<flow>\`, `exit` last.
- **Running one**, as `whatsnew-tour-line.md` says, from the repository's root (quiet: the app ends with the script):

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\whatsnew-tour-line'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\whatsnew-tour-line.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --follow --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

Then `--culture de-DE` and `--window <px>` added to the arguments for the other runs, and after each run
`Copy-Item -Recurse $qa "$pass\flows\<flow>\<culture>\<size>"`: the next run clears `$qa`. Each moment holds the
window's pictures with their layout files and the developer view's tabs (`devview-play.png` shows the script's last ten
lines, newest first); with `--verbose` the app log (`$qa\data\logs`) has every step, what it said and when each still
was taken. A step that found no quest or failed leaves its moments unchecked. A snapshot takes a second or two, so a
moment inside a motion shows where the motion was by then; the map's own motion frame by frame is `shturmap-cli frames`.
A flow a change reaches without a script gets one, added here with the change.

## Rules

- No praise and no "looks fine" without the pictures opened; findings are not softened.
- The reviewer never edits product files; it writes only into its pass folder. The change's author fixes.
- "Not run" is a finding, never a pass.
- No `--visible`, `tools\record-window` or `fake-raid.ps1 -Demo` unless the owner has said they aren't playing; then
  `tools\record-window` records Shturmap's own window only (DESIGN.md §8). Never the owner's monitors.
- `--data` on every run that would otherwise use `%LOCALAPPDATA%\Shturmap-dev` (the layout check's through `-MoreArgs`);
  never the release's data folder; no change to Windows' settings.
- Nothing private in a report: `%USERPROFILE%` for the user folder, no real screenshot names, log lines or player data.
  The pictures stay on the PC and go into no commit.
