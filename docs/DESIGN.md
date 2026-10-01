# Spotter design

This file is the working summary of the plan and the decisions taken on 2026-10-01. It stays next to the
code. Update it when behaviour changes.

## Ground rules

Spotter reads only files the game writes for the player, plus public community data:

- screenshot **file names** in `Documents\Escape from Tarkov\Screenshots` (pixels only for menu screenshots, to
  read the Tasks screen);
- `application_*.log` and `push-notifications_*.log` in the game's `Logs` folder, with shared read access;
- `Control.ini` and `Game.ini` (screenshot key, language), read-only;
- json.tarkov.dev, tarkov.dev's `maps.json`, and the SVG map artwork on assets.tarkov.dev.

It never opens a handle to `EscapeFromTarkov.exe`, reads or writes its memory, injects, hooks, sends input,
captures the screen, draws over the game, edits game files (including `Logging.config`), or reads
`backend_000.log` or the launcher's credentials (only `gamesRootDir` is read from the launcher settings).
`SafetyTests` fails the build if forbidden APIs appear in `src`.

BSG's licence agreement (4.3.4, 2018 text) read literally covers all companion tools; the user accepted that
residual risk (decision D1).

## Projects

| project | role |
| --- | --- |
| `Spotter.Core` | no Windows dependencies: screenshot names, map projection, floors, bearings, log parsing, raid tracker, quest progress, quest-name matching |
| `Spotter.Game` | install discovery (BSG launcher and Steam), log tailer, screenshot watcher, game settings |
| `Spotter.Data` | json.tarkov.dev loader with ETag cache and translations, SQLite progress store, TarkovEyes import |
| `Spotter.Ocr` | Tasks-screen reader on Windows.Media.Ocr |
| `Spotter.Map` | SkiaSharp map drawing: SVG artwork per floor, camera, renderer, map content |
| `Spotter.Session` | the coordinator: inputs in, one `SessionSnapshot` out |
| `Spotter.App` | WinUI 3 window: status bar, objective rail, GPU map (`SKSwapChainPanel`) |
| `tools/Spotter.Cli` | headless runner for every piece (see README) |

## How things work

**Finding the game.** BSG launcher and Steam are equal. Candidates come from the `EscapeFromTarkov` uninstall key
(HKLM 32/64, HKCU), the launcher's `gamesRootDir` (and older `gameRootDir`) with its subfolders, the default
`C:\Battlestate Games`, and Steam's `libraryfolders.vdf` + `appmanifest_3932890.acf` (Steam leaves
`InstallLocation` empty). A candidate needs `EscapeFromTarkov.exe` or a `Logs\log_*` session, in the folder or its
`build` subfolder. The newest log session wins; all valid installs feed the quest backfill.

**Screenshot names.** `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`. The trailing number is the in-raid
time of day in hours when a position is present; menu screenshots may carry a number of unknown meaning. Facing:
`yaw = atan2(2(qx·qz + qw·qy), 1 − 2(qx² + qy²))`, 0 = +Z, 90 = +X.

**Projection.** Same math as tarkov.dev: rotate (x, z) by `coordinateRotation`, then `X = a·rx + b`,
`Y = −c·ry + d` from the map's `transform`; the SVG is fitted into the projected `svgBounds ?? bounds` with
xMidYMid meet. Golden test: a Streets screenshot lands at (232.2, 749.0) in
`StreetsOfTarkov.svg`. Screen heading is computed by projecting a step along the facing (works with Icebreaker's
unequal scale).

**Floors.** A layer applies when the height is inside one of its extents and the position inside that extent's
boxes, if any; the narrowest band wins. The SVG base picture omits the layer groups; each layer is its own picture.

**Logs and raids.** Records are a header line plus an optional JSON block; the reader keeps partial lines and
blocks across reads, the tailer keeps a stateful UTF-8 decoder per file and polls (500 ms active, 2 s idle).
Raid states: Menu → Loading (`scene preset path:`) → InRaid (`GameStarted`) → Menu (`PrepareSelectedProfileLocally`
or matching cancelled). The map comes from the scene path matched to tarkov.dev's `scenePath`, else the
`Location:` id matched to `nameId`, else a small alias table. Side: the match-setup `Profileid` equals the menu
profile for PMC raids and differs for Scav raids; Scav raids also lack `GameStarting`; locally hosted PvE raids
have neither signal and stay Unknown. Quest notifications: `ChatMessageReceived` type 10 started, 11 failed,
12 completed; the quest id is the first word of `templateId`. Repeatable tasks use ids no catalog has; they are
stored but not shown.

**Quest progress.** Observations (log, Tasks scan, manual, import) are stored and never rewritten; the newest
observation per quest wins. Prerequisites of an active or completed quest that strictly require "complete" are
shown as implied complete, never stored.

**Tasks OCR.** A screenshot without a position is read whole once; it is the Tasks screen if four headers
(Trader, Type, Class, Task, Location, Status, Progress) line up. Columns come from header positions; the Task to
Status columns are cropped, upscaled 2× and read again; rows anchor on the Status cells. Names are matched by edit
distance with the exact-number rule, a Location cross-check, and folding of known OCR confusions in the game font
("IJ" for "U"). Accepted at ≥ 0.92 with a clear margin, confirmation at ≥ 0.75. Measured on two 1440p
screenshots: 16 of 16 rows correct, about 200 ms per screenshot. The selected tab is the light one among the
STORY/SIDE/OPERATIONAL slots.

## Status (2026-10-01)

- M0 done: toolchain (per-user .NET 10 SDK, Git), WinUI 3 builds from the CLI, GPU map via SkiaSharp, OCR and data
  spikes turned into real code.
- M1 mostly done: discovery, both watchers, raid tracking, map with floors, player marker, facing, trail,
  extracts and transits, quest markers, objective list by distance and direction, second-monitor window.
- Parts of M2 and M3 are in: log backfill, live quest events, TarkovEyes import, inference, automatic Tasks scans
  with confirmation.

Known gaps: The Lab, Labyrinth and Icebreaker are tile-only maps and are not drawn yet; no quest journal or manual
quest editing yet; no floor picker; the published folder is 237 MB (budget 80–120 MB, needs trimming).
