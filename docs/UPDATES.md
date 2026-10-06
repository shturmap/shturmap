# Updates and checks

What to verify when the game is patched or tarkov.dev's data changes (new quests, items, maps, an event, a wipe), so
that Shturmap's rules still hold (owner, 2026-10-05: "document things we need to verify when new quests or items are
added where we have to check if our current logic holds and does still apply"). Everything here runs on this PC.
What it prints stays here: no quest texts, logs, screenshots or dumps go into the repository (CLAUDE.md).

## When

- **tarkov.dev's data changed**: new quests or items, an event, a wipe. At the latest before each release.
- **The game was patched**: the log folders' names carry the build (`log_2026.01.01_…_1.1.5.1.47510`). The dev build
  says it once: "Game build … is new: time for the checks after a patch".
- **A player reports** something missing or wrong on a map or a card.

## 1. Refresh the data (2 min)

```
shturmap-cli data pve
shturmap-cli data regular
shturmap-cli data pve de
```

The commands refresh the cache (`%LOCALAPPDATA%\Shturmap\cache\tarkov-dev`) that the audits and tests read; the
German texts are for `ExtractRulesTests`. Compare the counts (tasks, maps, traders, definitions) with the last run
below. Every map should say `svg` or `tiles`: one with `NONE` has no definition in maps.json and drops out of Plan.

## 2. The audits, both modes (5 min)

Run each with `pve` and with `regular`.

| command | look at | if it shows up |
| --- | --- | --- |
| `synopses` | FALLBACK, LONG, BREAK rows | extend the tables in `QuestSynopsis` (DESIGN.md §5, "Quest synopsis") |
| `effort` | UNKNOWN TARGET; "Unknown objective types: none"; the group totals | name the target in `QuestEffort`; a new objective type is classed as Trader and leaves the map and Plan, so add it to `QuestTaxonomy.Classify`. A jump in "Go there" can mean `ExpBonusSurvived` was renamed (DESIGN.md §7, "Plan order") |
| `bring` | UNKNOWN ITEM, LIST, NO EXIT | a name the translations lack; a weapon class split up; an exit no map has (`ExtractRules`) |
| `handovers` | NEAR; new names under "Quest items handed over with no pickup" | the pairing rule in `Handovers` (DESIGN.md, "Quest cards", *Hand-overs*) |
| `spawns` | markers far from any spawn point; boss names | the spawn grouping in `MapContent`; a new AI PMC id would draw as a boss |

Last good totals, to compare against: `effort pve` 2026-10-03: 614 rows, Go there 214, Find or survive 134, Fight
266. `handovers` 2026-10-05: PvE 217 fold, 174 own lines, 0 NEAR; PvP 217, 186, 0.

## 3. The tests against the fresh cache (1 min)

`.\eng\dotnet.ps1 test --solution Shturmap.slnx`, and check that the summary's `skipped` is 0 here: these read the
local cache and skip without it.

- `SynopsisDataTests`: every quest on every map keeps the synopsis rules (fails on BREAK only).
- `PlanOrderTests`: over 100 Plan rows and objective facts read.
- `ExtractRulesTests`: every exit takes the same in German as in English, with at least 5 flare and 5 co-op exits.
- `PicksTests`: picks on the real data.

## 4. After a game patch (one played raid)

1. `shturmap-cli locate`: the install, the Logs folder, the Screenshots folder, the screenshot key (not "NOT
   BOUND"), the game's language.
2. Play one raid: a screenshot at its start while the extract list shows, one later on, then extract.
3. `shturmap-cli replay`: the mode line, `loading <scene>`, `raid started on <map> as <side>`, `back in menu after
   mm:ss`. Anything missing is a pattern in `GameLogParser` (or a scene path in `data`).
4. `shturmap-cli quests`: a quest started or completed in that session shows, with its observation.
5. `shturmap-cli render <map> out.png "<the screenshot's file name>"`: the marker where you stood, facing the right
   way.
6. The extract list: the app log says "Extract list read …: N exits" (or why it wasn't taken); compare with what the
   game showed. Copy the screenshot into `tests\fixtures\ocr` (git-ignored) and run `ExitListReaderTests`.
7. The raid card's MIN LEFT against the game's own timer on a screenshot (within a minute).
8. `tools\make-log-fixtures.ps1` makes local fixtures of the new session; run the tests.

When a check finds drift: fix the rule, add a test with made-up ids (never a real log line or quest text), update
DESIGN.md in the same change, and add a line to the log at the end.

## What the rules assume

⚠ marks a failure that says nothing: something just goes missing or reads wrong.

### tarkov.dev data

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| Payloads at `json.tarkov.dev/<mode>/…` with `data.maps`, `data.tasks`, `data.mobs`, `data.questItems`; traders as `data` itself | `GameDataLoader`, `GameData.Slug` | ⚠ a section with a new shape loads as empty: maps or quests vanish | `data` counts; `PlanOrderTests` |
| Translations: keys plus a `translations` path list, item names under `<id> Name` / `ShortName`, transit conditions translated by hand | `JsonTranslator`, `GameDataLoader` | raw keys or "Unknown item" on screen | `bring` UNKNOWN ITEM; `data pve de` |
| The game's language codes map to tarkov.dev's (`ge→de`, `cz→cs`, `jp→ja`, …), in two copies | `GameDataLoader.ApiLanguage`, `ExitListReader.WindowsLanguage` | a 404, then English everywhere | `LoaderLanguageTests` pins the table only |
| Objective types are the known ones | `QuestTaxonomy.Classify`, `QuestEffort` | ⚠ a new type is classed as Trader: off the map and out of Plan | `effort` "Unknown objective types" |
| Kill targets: `Savage`, `Marksman`, `assaultGroup`, `Any` are Scavs; prefixes `boss`, `follower`, `sectant`, `infected` | `QuestEffort` | an unknown target counts as a fight (the safe side) | `effort` UNKNOWN TARGET |
| The exit status `ExpBonusSurvived` means "survive" | `QuestEffort` | ⚠ every extract objective falls to "Go there" | `effort` group totals against the last run |
| Raw objective fields: `targetNames`, `exitStatus`, `exitName`, `usingWeapon`, `usingWeaponMods`, `bodyParts`, `distance`, `wearing`, `notWearing`, time and health conditions, `zones` | `GameDataLoader` (ObjectiveFacts) | ⚠ a renamed field drops a condition: conditional kills move groups | `PlanOrderTests` catches a total loss only |
| What a quest takes: `plantItem.items`, `mark.markerItem`, `useItem.useAny` | `Planning`, `ItemCards` | ⚠ BRING rows go missing | `bring` by eye |
| A `plantQuestItem`'s quest item is got by a `findQuestItem` (of its quest, or one before) and goes into the raid from the quest items by itself, so it is never brought | `Planning.QuestItemFrom`, `ItemCards` | a "With:" line without where it comes from; a quest item handed by a trader would need saying so | `QuestItemTests`; 15 such objectives on 2026-10-06, each got in a quest |
| Hand-overs pair with their pickup or find by quest item, or by item set, count and found-in-raid | `Handovers` | a hand-over line comes back, or a mark goes missing | `handovers` NEAR |
| `possibleLocations` with one position is where the thing is; several are each "maybe here" | `MapContentBuilder.PlacesItCanBe`, `QuestCards` | a "?" where the place is certain, or none where it isn't | `PossiblePlaceTests`; the card by eye |
| Synopsis tables (English): verbs, terms, conditions, place patterns, map names | `QuestSynopsis` | FALLBACK and LONG rows; BREAK | `synopses`; `SynopsisDataTests` |
| Exit rules: `Alpinist*`/`RedRebel*` climb, `sniper` or "(Flare)" flare, "(Co-op)" co-op; item ids of roubles, dollars, euros, the red flare, ice pick and paracord | `ExtractRules` | wrong or missing "to leave through" items | `ExtractRulesTests`; `bring` NO EXIT |
| Bosses are mobs whose id starts `boss`; every mob but `pmcUSEC`/`pmcBEAR` is drawn | `GameData.BossMobsOn`, `MapContent` | a boss missing from Plan; ⚠ new AI PMCs drawn as bosses | `spawns` |
| Spawn sides and categories: `scav`; `bot`, `all`, `sniper` | `MapContent` | ⚠ spawn rings go missing | `spawns` |
| Lock types `door`/`trunk`; switches named `switch_…` are untranslated; activation `Unlock`; hazards `hazard` (up to 50 m²), `minefield`, `sniper`; extract factions `pmc`/`scav`, the rest shared | `MapContent`, `GameSession.Exits` | ⚠ new values are left out | none: `render` by eye |
| Item sources: `preset` left out; `noFlea`, `minLevelForFlea`, `lastLowPrice`; USD and EUR, the rest shown as ₽; categories most specific first | `ItemSources`, `ItemCards`, `Planning` | wrong source lines, wrong currency | `bring` LIST rows |
| A quest requirement's status `complete`; the log's `templateId` is tarkov.dev's task id; trader order as given | `QuestProgress`, `Planning` | quests not tracked or implied | `quests` |
| `raidDuration` is the raid's length | `Planning`, `Rules/RaidTime`, `RaidTracker` | ⚠ a wrong MIN LEFT; an open raid closed too early or late | none: step 4.7 |
| Wiki links on `escapefromtarkov.fandom.com`; map link is the page plus `_Interactive_Map` | `OutsideLink` | ⚠ the links disappear | none |
| Art at `assets.tarkov.dev/<id>.webp`; downloads only from `json.tarkov.dev`, `assets.tarkov.dev`, `raw.githubusercontent.com` | `GameArt`, `CachedHttp.Hosts` | pictures or tiles from a moved host are refused | `AllowedHostsTests` pins the list only |

### Game logs

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| Line header `yyyy-MM-dd HH:mm:ss.fff[ ±hh:mm]\|ver\|lvl\|channel\|msg`; a JSON body ends with a lone `}` | `LogRecordReader` | nothing is read | `replay` |
| Files `application_N.log`, `push-notifications_N.log` (or `notifications_N.log`) in `log_yyyy.MM.dd_H-mm-ss…` folders | `LogTailer`, `InstallLocator` | no logs found | `locate` |
| `Session mode: Pve\|Regular\|PvpSeason` | `GameLogParser` | an unknown mode keeps the last one, with a WARN in the app log | the app log |
| `scene preset path:maps/*.bundle`, `TRACE-NetworkGameCreate profileStatus` (Location, shortId, Profileid), `[Transit]`, `GameStarting:`/`GameStarted:`, the loading steps, `PrepareSelectedProfileLocally`, "matching cancelled" | `GameLogParser` | no raid, no map ("MAP NOT KNOWN"), no side, no end | `replay`; the local log fixtures |
| Notifications: `ChatMessageReceived` type 10/11/12 is quest started/failed/completed; `GroupMatchRaid*`; `UserConfirmed`; `templateId` starts with the quest id | `GameLogParser` | ⚠ quests stop following the game | `quests` |
| The side: the setup's profile is the menu's for a PMC; no `GameStarting` is a Scav joining; a locally hosted PvE raid can't tell (no local Scav raid seen yet) | `RaidTracker` | the wrong card (PMC or Scav) | `replay` |
| Maps by scene path, then `nameId`, then aliases (`bigmap`, `city`, `shopping_mall`, `factory_day`, …); Ground Zero 21+ by a later location line | `MapIdentity`, `GameSession` | "MAP NOT KNOWN" | `replay` with `data`'s scene paths |

### Screenshots and the extract list

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| File name `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`, `.` as the decimal point; facing from the quaternion | `ScreenshotName` | no position, or a wrong facing | `ScreenshotNameTests`; `render` with a new name |
| The folder `Documents\Escape from Tarkov\Screenshots` | `InstallLocator` | no screenshots seen | `locate` |
| The list in the top right 0.60 × 0.62 of the picture's height, under a green bar, the "??:??:??" column right of it, header "Find an extraction point" | `ExitListReader`, `ExitList` | nothing read (says "not checked": safe); a list of one exit refused | the app log; `ExitListReaderTests` with new screenshots |
| The list's names are tarkov.dev's extract names, in the game's language or English | `ExitList.Match`, `GameSession.Exits` | ⚠ an extract the game renamed reads "not on your list" and is drawn hollow though it is open | compare the app log's count with the game's list (step 4.6) |

### Game settings and install

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| Control.ini: `keyBindings[keyName=MakeScreenshot]`, Unity key names; Game.ini: `Language`; in `%APPDATA%\Battlestate Games\Escape from Tarkov\Settings` | `GameSettingsReader` | help names no key; the data in the wrong language | `locate` |
| Install found by the uninstall key `EscapeFromTarkov`, the launcher's `gamesRootDir`, `C:\Battlestate Games`, Steam app 3932890 | `InstallLocator` | "No game found" | `locate` |

### Maps and artwork (maps.json)

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| maps.json from the-hideout/tarkov-dev on GitHub; entries with `projection: "interactive"`, a key, bounds and a 4-number transform | `GameDataLoader`, `MapDefinitionReader` | a map drops out of Plan and can't be drawn | `data` (`NONE`) |
| tarkov.dev's transform; Icebreaker's unequal scale kept | `MapProjection` | positions off on the map | `render` with a known screenshot; `MapProjectionTests` read the committed snapshot of 2026-10-01 only |
| A floor's `svgLayer` is a top-level `<g id>`; minefields are ids starting `mine`, sniper zones `sniper`/`danger` | `MapArtwork` | ⚠ a floor or a hazard goes missing | `render` by eye |
| Tiles at `{z}/{x}/{y}`; The Lab's tile size 175; floors need extents | `TileGrid`, `FloorResolver` | tiles in the wrong place | `render the-lab` |

## No check yet

Raid lengths against the game; the exit status key; the lock, switch and hazard vocabularies; the wiki host; the
extract list's names against renamed extracts; live maps.json against the committed snapshot; the log format after a
patch (only by hand, step 4). These are the first candidates for an automated check.

DESIGN.md quotes counts that age with the data (Customs' 27 extracts on one lever, 107 border-sniper zones,
Labyrinth's 18 traps, 60 of 1,418 objectives optional): they say what was true on their date.

## Log

| date | what | result |
| --- | --- | --- |
| 2026-10-03 | `effort pve` | 614 rows: Go there 214, Find or survive 134, Fight 266 |
| 2026-10-03 | `bring` pve and regular | no unknown items, no unmatched exits |
| 2026-10-05 | `handovers` pve and regular | 217 fold in each; 0 NEAR |
