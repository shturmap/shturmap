# Spotter design

This document is binding for anyone changing Spotter, people and agents alike. Read it before you change
behaviour or UI, follow it, and update it in the same change when a decision changes.

## 1. What Spotter is

Spotter answers two questions for an Escape from Tarkov player, on a second monitor:

1. **Before a raid: where should I go, and what do I need to bring?**
2. **During a raid: where am I, and where is my next objective?**

That is the whole product. A feature belongs in Spotter only if it helps one of these two questions and works
without the player having to click during a raid. When in doubt, leave it out.

**Not goals** (declined on purpose; don't add them): item prices or a flea-market view, hideout tracking, loot
or container maps beyond what quests need, item scanning by hotkey, ammo charts, squad sharing, achievements,
overlays on the game window, statistics dashboards, a settings maze.

## 2. Ground rules (game terms of service)

Spotter reads only files the game writes for the player, plus public community data:

- screenshot **file names** in `Documents\Escape from Tarkov\Screenshots`; pixels only of menu screenshots, to read
  the Tasks screen;
- `application_*.log` and `push-notifications_*.log` in the game's `Logs` folder, with shared read access;
- `Control.ini` and `Game.ini`, read-only.

It never opens a handle to `EscapeFromTarkov.exe`, reads or writes its memory, injects, hooks, sends input,
registers global hotkeys, captures the screen, draws over the game, edits game files (including
`Logging.config`), or reads `backend_000.log` or launcher credentials (only `gamesRootDir` is read from the
launcher settings). `SafetyTests` fails the test run if forbidden APIs appear in `src/`. Keyboard shortcuts work
only while Spotter's own window has focus.

BSG's licence agreement (4.3.4, 2018 text), read literally, covers all companion tools; the owner accepted that
residual risk. Do not widen the boundary.

## 3. Copyright and data use

- **Game data** (quests, maps, items, extracts, bosses) comes from tarkov.dev's public JSON service
  (`json.tarkov.dev`). It is downloaded at runtime into the user's cache for personal use and shown with
  "data tarkov.dev". It is never bundled in the repository or a build.
- **Map geometry** comes from tarkov.dev's `maps.json` (MIT; a snapshot is used as a test fixture with attribution).
- **Map artwork** is the SVG maps by Shebuka and contributors (CC BY-NC-SA 4.0, licence void for cheats/radars).
  Downloaded at runtime, displayed unmodified except for showing one floor at a time, credited on the map, never
  redistributed. Non-commercial only.
- **Trader portraits and item icons** are Battlestate's art. They are never in the repository, a build or the
  test fixtures. Spotter fetches each one from tarkov.dev's image service (`assets.tarkov.dev`) the first time it
  is shown, keeps it in the user's cache (`cache\game-art`), and shows a glyph when it can't be had. This is
  display for personal use, the way every Tarkov tool and tarkov.dev itself show them.
- **Not used**: other Battlestate artwork (quest images, game UI art, map art), text from the EFT wiki or guide
  sites (the quest card links to the wiki page instead), and code from GPL or unlicensed projects (TarkovMonitor,
  TarkovTracker, MAYAK, Tarkov Pilot, RatScanner). Facts learned from them (log formats, file paths) are
  reimplemented.
- **Icons** are the Segoe Fluent Icons font that ships with Windows, used in place, never copied into the repo;
  the only custom glyph is the crosshair (drawn from a path in `Spotter.Map.Glyphs`).
- **Test fixtures** contain only scrubbed logs and the owner's own Tasks screenshots (game UI, for OCR tests in a
  private repository). Remove `tests/fixtures/ocr` before ever publishing the repository.

## 4. UX principles

1. **No clicks in a raid.** Spotter follows the game: the logs decide the view, the map, the mode and the side;
   screenshots move the player. Nothing in a raid requires input. Defaults must be right without configuration.
2. **Glanceable from a second monitor.** Primary text ≥ 14 px, numbers in a monospaced face, high contrast on a
   dark ground, the important line first. No animation beyond what helps the eye follow a change.
3. **One window, two states.** *Plan* while in the menus, *Raid* while loading or in a raid. The switch is
   automatic. No tabs, no modes to pick, no modal dialogs. The only other windows are quest cards the player
   pinned; they are owned by the main window and never topmost, so they can't cover the game.
4. **Minimal surface.** Every control earns its place. Prefer an automatic behaviour over a button, a sensible
   default over a setting. Messages are one line and dismiss themselves.
5. **Clear requirements.** What a raid needs (keys, items to bring) is shown before the raid, unprompted, and next
   to the objective that needs it during the raid.
6. **Says why.** Every quest state can say where it came from (log, Tasks scan, import, manual, implied).
7. **Only ask when unsure.** The only confirmations in the app are uncertain Tasks-scan reads, one click each.
8. **Position is occasional.** Players press the screenshot key now and then, not continuously. Everything except
   the "you are here" parts works without a fix. A fix shows its age and the marker fades. No "you may be
   anywhere in here" ring: it was tried and is visual noise. Directions relative to your facing
   ("ahead-left") are shown only for 45 s after a fix; after that they become map directions ("NE", map-up is
   north), which stay true while you move. Distances say how old they are. A new fix frames you and your nearest
   objective together.
9. **Say it before it matters.** When a raid starts loading, a one-line notice repeats what to bring for that
   map, while there is still time to back out of matching.
10. **Point, don't navigate.** Pointing at a quest, an item or an extract anywhere lights up every other place it
    appears (rail rows, cards, map markers) and, for a quest, shows its card. Details come to the pointer; there
    are no detail pages to open. Finished quests appear nowhere.

### Visual language

| meaning | colour |
| --- | --- |
| quests and objectives (one accent) | amber `#E49A3C` |
| extracts, success, healthy inputs | green `#6CC38E` |
| the player, the trail | sand `#F2D79C` / teal `#74B8B1` |
| transits | violet `#B598E8` |
| bosses, danger | red `#EC7D69` |
| secondary text | grey `#8E9A93` on ground `#0E1413` |

Quest **types are shown by glyph, never by colour**; colour stays free for state (open, done, selected).
The type glyph is always the first thing on a quest's row and the only thing inside its map marker. **Trader
portraits** are secondary: small and round, at the right end of Plan rows, before the quest line of Raid rows and
in the card header; never in place of the glyph, never on the map. **Item icons** sit in a dark inventory cell
next to what to bring. **Linked highlight**: amber at 20 % behind rows; map markers not in focus step back to
28 % opacity; markers on another floor than the one shown are drawn at half strength.
Typography: the system UI font for text, Cascadia Mono / Consolas for numbers (distances, times, chances).

### Screen anatomy

- **Status bar** (top): mode (PvE/PvP/Seasonal), raid state, last fix; on the right the inputs (logs,
  screenshots, data) as chips that turn amber when something needs attention, and the help button.
- **Rail** (left, 380 px), content by state:
  - *Plan*: last raid in one line; **Next raid**: up to four maps ranked by what can be done there, each with one
    line in words ("Complete 7 quests · progress 2 more"), the best one expanded with COMPLETE, PROGRESS and
    BRING (keys, items to bring). Clicking another map expands it and shows it on the map; that click is optional.
  - *Raid*: one raid line (time left, bosses with spawn chance, time of day); **objectives here**, nearest first,
    with type glyph, distance, direction and floor hint, and an inline key/item requirement where needed; then
    objectives with no fixed place; then extracts and transits for your side.
- **Map** (rest): artwork, quest markers with type glyphs, extracts, transits, player, trail, guide line to the
  selected objective. Map controls bottom-right, with the floor picker above them on maps with floors; one-line
  notices top-centre; attribution bottom-left.
- **Help** (F1 or `?`): one panel with how it works, the shortcuts and the glyph legend. Opens once by itself on
  first run.

### Quest cards

One card per quest, the same everywhere: trader portrait, type glyph and name; trader, level and Kappa /
Lightkeeper; the state and where it came from ("Active · from the game log, 25 Sep"); every objective with its
glyph, where it is and the item it is about; BRING (keys and items, with icons and maps); what it unlocks; a link
to the wiki page. Nothing else: no rewards, no guides.

It has three states, like the pinnable tooltips in Crusader Kings III:

1. **Hover**: rest the pointer on a quest (rail row or map marker) for 0.4 s and the card appears beside it.
   Moving down a list swaps cards after 0.12 s.
2. **Held**: keep resting while a thin amber bar fills (0.9 s), or move into the card, and it stays; the border
   turns amber. Leaving it closes it after 0.5 s. Esc closes it.
3. **Pinned**: the pin turns it into a small window of its own, placed where the card was. Pinned cards come back
   after a restart and close by themselves when their quest is completed.

Rows inside a card take part in linked highlighting, so pointing at a key in a card lights up that key in BRING.

### Keyboard (window focused only)

| key | action |
| --- | --- |
| F | follow my position |
| + / − | zoom in / out |
| 0 | show the whole map |
| PgUp / PgDn | show the floor above / below |
| Esc | close the card, else clear the selection |
| F1 or ? | help |

Keyboard accelerators sit on the window root with their placement hidden; WinUI would otherwise show the first
one's key as a tooltip over the whole window.

Mouse: drag to pan, wheel to zoom at the cursor, double-click to zoom in, click an objective (in the list or on the
map) to draw a line to it, point at anything to see what belongs to it.

## 5. Quest taxonomy

Every objective gets one type, from tarkov.dev's objective `type`. The game gives each quest one hand-assigned
type on its Tasks screen (Elimination, Pickup, Exploration, Discovery, Completion, …); that type is not in any
public data and can't be derived reliably (a check against the wiki matched 62.5 %), so Spotter types objectives
instead and borrows the game's names and look where one fits, so the labels feel familiar:

| type | glyph | objective types | in raid? |
| --- | --- | --- | --- |
| **Elimination** | crosshair (custom; the game uses a skull) | `shoot` | yes |
| **Exploration** | magnifier `E721` (as in the game) | `visit` | yes, at a place |
| **Pickup** | pointing hand `E7C9` (as in the game) | `findQuestItem` | yes, at a place |
| **Place** | pin `E840` | `plantItem`, `plantQuestItem`, `mark`, `useItem` | yes, at a place; needs an item |
| **Find in raid** | bag `E719` | `findItem` | yes, anywhere (FIR) |
| **Survive** | runner `E726` | `extract`, `experience` (an in-raid health condition, not XP) | yes |
| **Trader** | people `E716` | `giveItem`, `giveQuestItem`, `sellItem`, `buildWeapon`, `traderLevel`, `traderStanding`, `skill`, `taskStatus`, `dialogue`, `globalVariable`, anything new | no |

A quest's type is that of its most common in-raid objective type (ties: Elimination, Pickup, Place, Exploration,
Survive, Find in raid); a quest with no in-raid objectives is a Trader quest. This is derived, not the game's own
label. Requirements use a key glyph `E8D7` for keys and a briefcase `E821` for items to bring. Never use the
game's icon artwork; the glyphs only echo it.

## 6. Raid requirements

- **Keys**: the quest's `neededKeys` for that map, and each objective's `requiredKeys` (a list of alternatives:
  shown as "A or B").
- **Bring**: items a Place objective consumes (`items` of `plantItem`, `markerItem` of `mark`, `useAny` of
  `useItem`, the quest item of `plantQuestItem`), with counts summed per item.
- Shown aggregated per map in Plan, and inline on the objective in Raid ("needs Dorm room 114 key").
- Spotter cannot see the stash; it lists what is needed, not what is missing.

## 7. Raid planner

For each map (variants sharing artwork, like Ground Zero 21+, count as one), using active quests only
(objective progress is unknown to Spotter):

- An objective is **doable** on a map if it names that map or has a place there, or names no map and is Kill,
  Collect or Survive. It is **tied** to the map if it names the map or has a place there.
- A quest counts for a map only if at least one of its doable objectives is tied to it; work that fits any map
  doesn't argue for one map over another.
- A quest can be **finished** on a map if all its in-raid objectives are doable there, none is Collect (found-in-
  raid items depend on luck) and no kill count is above 3; otherwise it is **progressed**.
- Score = 3 per finishable quest + 1 per tied objective with a place + 0.6 per tied objective without one + 0.1 per
  untied doable objective. Maps are ranked by score; the top four are shown, the best (or the one on screen)
  expanded.
- Quests whose in-raid work fits any map are listed once under **Any map**.
- Walking estimate: a nearest-neighbour route through one place per located objective, at 3 m/s, shown against
  the raid length ("~7 min walking · 35 min raid").
- Quest-level keys are shown on the objectives that have a place on that map, not on hand-ins or extracts.

## 8. Engineering

| project | role |
| --- | --- |
| `Spotter.Core` | no Windows dependencies: screenshots, projection, floors, bearings, logs, raid tracker, quest progress, taxonomy, planner, name matching |
| `Spotter.Game` | install discovery (BSG launcher and Steam are equal), log tailer, screenshot watcher, game settings |
| `Spotter.Data` | json.tarkov.dev loader (ETag cache, translations), SQLite progress store, TarkovEyes import, game art cache (portraits, icons) |
| `Spotter.Ocr` | Tasks-screen reader on Windows.Media.Ocr |
| `Spotter.Map` | SkiaSharp drawing: artwork per floor, camera, renderer, map content, glyphs |
| `Spotter.Session` | the coordinator: inputs in, one immutable `SessionSnapshot` out |
| `Spotter.App` | WinUI 3 window; reads snapshots, never game files |
| `tools/Spotter.Cli` | headless runner: locate, replay, data, ocr, render, watch, simulate |

Rules:

- Core logic is pure and unit-tested; fixtures come from real logs (scrubbed with `tools/make-log-fixtures.ps1`).
- Every change keeps `.\eng\dotnet.ps1 test --solution Spotter.slnx` green, including `SafetyTests`.
- Verify UI with `Spotter.exe --snapshot <folder>` (renders the window and the map to PNGs) or
  `spotter-cli render`; never capture the user's screens.
- Comments explain why, not what. Match the surrounding style.
- Write user-facing text plainly: short sentences, units on numbers, no jargon.

### How the parts work

**Finding the game.** Candidates come from the `EscapeFromTarkov` uninstall key (HKLM 32/64, HKCU), the
launcher's `gamesRootDir` (older `gameRootDir`) and its subfolders, `C:\Battlestate Games`, and Steam's
`libraryfolders.vdf` + `appmanifest_3932890.acf`. A candidate needs `EscapeFromTarkov.exe` or a `Logs\log_*`
session (also under `build`). The newest log session wins; all valid installs feed the quest backfill.

**Screenshot names.** `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`; the trailing number is the in-raid
time of day when a position is present. Facing: `yaw = atan2(2(qx·qz + qw·qy), 1 − 2(qx² + qy²))`.

**Projection.** tarkov.dev's math: rotate (x, z) by `coordinateRotation`, `X = a·rx + b`, `Y = −c·ry + d`, SVG
fitted into the projected `svgBounds ?? bounds`. Golden test: a Streets screenshot lands at
(232.2, 749.0) in `StreetsOfTarkov.svg`.

**Logs and raids.** Menu → Loading (`scene preset path:`) → InRaid (`GameStarted`) → Menu
(`PrepareSelectedProfileLocally` or matching cancelled). Map: scene path ↔ `scenePath`, then `Location:` ↔
`nameId`, then an alias table. Side: the match-setup `Profileid` equals the menu profile for PMC raids and differs
for Scav raids; locally hosted PvE raids stay Unknown. Quests: `ChatMessageReceived` type 10/11/12.

**Quest progress.** Stored observations (log, Tasks scan, manual, import); newest wins, except that an import or
a scan never reopens a quest the log saw completed or failed (TarkovEyes' save is dated by its file, which can be
after the hand-in); only a later log event can. Prerequisites of active or completed quests that strictly require
"complete" are shown as implied, never stored. `spotter-cli quests` lists active quests with every observation.

**Floors.** The floor shown is the player's, from the height of the last fix. The picker lists floors that have
artwork of their own, top first, with a dot on the player's; a pick (click or PgUp/PgDn) holds until the next
screenshot. Floors without their own artwork (Customs' 4th, Reserve's upper floors) are drawn in the base layer.

**Tasks OCR.** Header row found in a whole-frame pass, Task→Status columns cropped and read at 2×, rows anchored
on Status; names matched by edit distance with the exact-number rule, a Location check and font-confusion
folding. Accept ≥ 0.92 with margin; confirm ≥ 0.75.

## 9. Status

- Done: discovery, watchers, raid tracking, map with floors, player, facing, trail, extracts, transits, quest
  markers, objectives by distance, log backfill, live quest events, TarkovEyes import, Tasks scans, safety test,
  self-contained publish, taxonomy, raid planner, requirements, raid line, help panel, keyboard shortcuts,
  occasional-position UX (fix age, fading marker, compass directions), bring-list notice on raid load, linked
  highlighting, quest cards (hover, held, pinned), trader portraits and item icons, floor picker.
- On hold (owner, 2026-10-01): product name (proposed: Shturman). Don't rename or raise it.
- Open: tile-only maps (The Lab, Labyrinth, Icebreaker); manual quest editing; objective progress;
  published size 237 MB (budget 80–120 MB, needs trimming).
