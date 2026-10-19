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
German texts are for `ExtractRulesTests` and `ItemCardTests`. Compare the counts (tasks, maps, traders, definitions)
with the last run below. Every map should say `svg` or `tiles`: one with `NONE` has no definition in maps.json and
drops out of Plan.

**Story chapters** (Tour, Falling Skies, Batya, …; owner, 2026-10-08: wait for tarkov.dev). Shturmap has none of
them: tarkov.dev builds them into the tasks file as `data.story` but deletes that part in production ("not ready
for prime time", `tarkov-data-manager`, `jobs/update-quests.mjs`), and the game's logs don't say which chapter or
section is active. Check whether the part has arrived and whether its objectives have places yet:

```powershell
foreach ($mode in 'pve', 'regular') {
  $d = (Get-Content "$env:LOCALAPPDATA\Shturmap\cache\tarkov-dev\${mode}_tasks.json" -Raw -Encoding UTF8 | ConvertFrom-Json).data
  $main = @($d.story.tasks.objectives | Where-Object { $_ -and -not $_.optional })
  "{0}: {1} chapters, {2} main objectives, {3} on a map, {4} with a place" -f $mode, @($d.story | Where-Object { $_ }).Count,
    $main.Count, @($main | Where-Object { $_.maps }).Count, @($main | Where-Object { $_.zones -or $_.possibleLocations }).Count
}
```

Today it says 0 chapters. When chapters show up, Shturmap still ignores them (`GameDataLoader` reads `data.tasks`
only), so nothing breaks; tell the owner, with the counts. Support is worth designing once main objectives have
places: what is active must then come from the player (DESIGN.md §4, principle 7), as no log line says it. Players
are told the chapters aren't shown (owner, 2026-10-09): the tour's last chapter (`docs/tour.md`), a note in help
(`MainWindow.xaml`), the README's and the website's FAQ, and the release notes. Once Shturmap reads them, those lines go.

## 2. The audits, both modes (5 min)

Run each with `pve` and with `regular`. They stay English, with no language to choose (owner, 2026-10-11): they check
rules that read English data. The German data is covered by `shturmap-cli data pve de` (step 1) and the tests that
read its cache, `ExtractRulesTests` and `ItemCardTests` (step 3).

| command | look at | if it shows up |
| --- | --- | --- |
| `synopses` | FALLBACK, LONG, BREAK rows | extend the tables in `QuestSynopsis` (DESIGN.md §5, "Quest synopsis") |
| `effort` | UNKNOWN TARGET; "Unknown objective types: none"; the group totals | name the target in `QuestEffort`; a new objective type is classed as Trader and leaves the map and Plan, so add it to `QuestTaxonomy.Classify`. A jump in "Go there" can mean `ExpBonusSurvived` was renamed (DESIGN.md §7, "Plan order") |
| `bring` | DESK (the stash desk's values over the item file; also `desk --audit`, from the cache): NO PRICE, NO FOOTPRINT and ONE OF N against the last run, the fee's rates, the found-in-raid rule and the level (a jump in NO PRICE or NO FOOTPRINT is a renamed field; NO FLEA RULES, the file lost its `fleaMarket`; new rates or the rule turned on move every flea figure: `DeskData`, `DeskValues`, DESIGN.md §4, *The stash desk*); HISTORY, the price histories the desk's cards fetched into the cache, read as the card reads them (UNREAD a file whose points' shape changed; FEW one without a day's points in its last 30 days; step C, 2026-10-20); UNKNOWN ITEM, LIST, NO EXIT; GUN WITHOUT PRESET, PRESET NOT AN ITEM; SWITCH EXITS, FLARE EXITS and EXIT FACTS "as the data has them"; the data rules' lines against the last run below | a name the translations lack; a weapon class split up; an exit no map has (`ExtractRules`). GUN WITHOUT PRESET, PRESET NOT AN ITEM: a gun BRING pictures by its bare receiver, or by a preset the items lack (`ItemSources.PictureId`; PRESETS counts them, 31 of 34 by a preset in PvE on 2026-10-19, the bare three a bayonet and two under-barrel launchers). SWITCH EXITS STALE: a row of `SwitchExits` whose extract or step the data no longer has, or whose id changed: put the row right (its extract shows no badge, line or link until then); DATA: the data now names switches of its own for a table extract: compare them with the row, which may then go. FLARE EXITS STALE: the same for `ExtractRules.FlaresByHand`. EXIT FACTS STALE: a row of `ExitFacts` whose extract or door lock is gone, or whose id or key changed: put it right (its words go unsaid until then); DATA: the data gives a row's faction itself, or its extracts carry a field they didn't on 2026-10-17: compare with the table, whose row may then go. OUTSIDE dropped, FACTION FROM BASE, EXTRACTS FROM BASE, FACTION BY HAND: what the rules for the data's defects did (`ExtractRepairs`); "none", or fewer than on 2026-10-17, is a fix upstream (strike it from the report to tarkov.dev, docs/NEXT.md); a new one is a new defect. OUTSIDE kept: extracts past their map's edge with no namesake elsewhere, kept: one far out is a copy the rule can't tell |
| `handovers` | NEAR; new names under "Quest items handed over with no pickup" | the pairing rule in `Handovers` (DESIGN.md, "Quest cards", *Hand-overs*) |
| `spawns` | markers far from any spawn point; boss names | the spawn grouping in `MapContent`; a new AI PMC id would draw as a boss |

Last good totals, to compare against: `effort pve` 2026-10-19: 602 rows, Go there 209, Find or survive 124, Fight
269 (regular 618: 207, 136, 275; one Go there fewer in each than on 2026-10-14, with the quest tarkov.dev's data no
longer has: 499 tasks in PvE, 503 in PvP). `handovers` 2026-10-19: PvE 220 fold, 155 own lines, 0 NEAR; PvP 220, 171, 0.

## 3. The tests against the fresh cache (1 min)

`.\eng\dotnet.ps1 test --solution Shturmap.slnx`, and check that the summary's `skipped` is 0 here: these read the
local cache and skip without it.

- `SynopsisDataTests`: every quest on every map keeps the synopsis rules (fails on BREAK only).
- `PlanOrderTests`: over 100 Plan rows and objective facts read.
- `ExtractRulesTests`: every exit takes the same in German as in English, with at least 7 flare and 5 co-op exits,
  the table's ten gated extracts, the seven doors with a key and at least 15 exits with a muted line; the hand-kept
  switch, flare and fact exits match the data in both modes (it fails on a STALE row and warns on DATA, as `bring`
  says them); the data rules leave Ground Zero no copy outside it, the variants' extracts a side, The Lab (Dark)
  extracts and the Customs rail a PMC's, and it warns where a rule did less than on 2026-10-17.
- `ItemCardTests`: German quest cards leave out the line of map names under at least half as many objectives as the
  English ones (2026-10-11: 363 of 936, English 577).
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
9. The stash reading: in the menus, screenshot the key tool and one stash page with the game's key; the app log says
   "Stash read from a menu screenshot: N grids, M cells with items the quests need", and the rows of what the
   pictures showed say "Seen in your stash". `shturmap-cli stash "<the screenshot>"` prints what it read cell by
   cell (a changed cell size, label font or line breaks it silently). Copy both into `tests\fixtures\ocr\stash`
   (git-ignored) and run `StashReaderTests`.

When a check finds drift: fix the rule, add a test with made-up ids (never a real log line or quest text), update
DESIGN.md in the same change, and add a line to the log at the end.

## 5. Before a release: the layout check (25 min)

New quests, items and maps bring longer names, in German most. With the fresh cache of step 1,
`.\tools\layout-check.ps1 -Out <folder>` plays every view in English, German and the pseudo-language at the smallest
window and at 1600x900 (docs/LANGUAGES.md, "Layout check"). It must say PASSED for every language but the
pseudo-language; read the pseudo-language's warnings in `summary.md` for what a longer language will break next. Its
pictures stay on the PC.

## 6. Before a release: the QA pass (about an hour)

The UI QA pass for a release (docs/QA.md, "What it runs"), by someone who didn't make the release's changes: the views
in English and German at both sizes, the tour's seven chapters, What's New and each line's preview, every replay script
in `tools\qa\`, `shturmap-cli frames` in both scenes, and the layout check of step 5 with `-SwitchCheck`, every picture
opened. It must say PASS; a finding left open goes to the owner before the Release workflow runs.

## What the rules assume

⚠ marks a failure that says nothing: something just goes missing or reads wrong.

### tarkov.dev data

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| Payloads at `json.tarkov.dev/<mode>/…` with `data.maps`, `data.tasks`, `data.mobs`, `data.questItems`; traders as `data` itself | `GameDataLoader`, `GameData.Slug` | ⚠ a section with a new shape loads as empty: maps or quests vanish | `data` counts; `PlanOrderTests` |
| Translations: keys plus a `translations` path list, item names under `<id> Name` / `ShortName`, transit conditions translated by hand | `JsonTranslator`, `GameDataLoader` | raw keys or "Unknown item" on screen | `bring` UNKNOWN ITEM; `data pve de` |
| The game's language codes map to the common ones (`ge→de`, `cz→cs`, `jp→ja`, …), one table for tarkov.dev's files, the extract list's text recognition and Automatic's choice of the app's language | `GameLanguage.Common`; its callers `GameDataLoader.ApiLanguage`, `ExitListReader.WindowsLanguage`, `UiLanguage.Choose` | a 404, then English data; the extract list read in English; Automatic takes Windows' language instead of the game's | `GameLanguageTests` pins the table only; `LoaderLanguageTests`: tarkov.dev's code (English for none), and against a fake tarkov.dev that a German game asks for `_de` and a language it lacks loads in English |
| Objective types are the known ones | `QuestTaxonomy.Classify`, `QuestEffort` | ⚠ a new type is classed as Trader: off the map and out of Plan | `effort` "Unknown objective types" |
| Kill targets: `Savage`, `Marksman`, `assaultGroup`, `Any` are Scavs; prefixes `boss`, `follower`, `sectant`, `infected` | `QuestEffort` | an unknown target counts as a fight (the safe side) | `effort` UNKNOWN TARGET |
| The exit status `ExpBonusSurvived` means "survive" | `QuestEffort` | ⚠ every extract objective falls to "Go there" | `effort` group totals against the last run |
| Raw objective fields: `targetNames`, `exitStatus`, `exitName`, `usingWeapon`, `usingWeaponMods`, `bodyParts`, `distance`, `wearing`, `notWearing`, time and health conditions, `zones` | `GameDataLoader` (ObjectiveFacts) | ⚠ a renamed field drops a condition: conditional kills move groups | `PlanOrderTests` catches a total loss only |
| What a quest takes: `plantItem.items`, `mark.markerItem`, `useItem.useAny` | `Planning`, `ItemCards` | ⚠ BRING rows go missing | `bring` by eye |
| A `plantQuestItem`'s quest item is got by a `findQuestItem` (of its quest, or one before) and goes into the raid from the quest items by itself, so it is never brought; the raid card and the quest card say it on the stashing objective | `Planning.QuestItemFrom`, `QuestCards`, `ItemCards` | a "With:" line without where it comes from; a quest item handed by a trader would need saying so | `QuestItemTests`; 15 such objectives on 2026-10-06, each got in a quest |
| Hand-overs pair with their pickup or find by quest item, or by item set, count and found-in-raid | `Handovers` | a hand-over line comes back, or a mark goes missing | `handovers` NEAR |
| `possibleLocations` with one position is where the thing is; several are each "maybe here"; a spot listed again for a map variant (or the same map) within half a metre is the same spot | `MapContentBuilder.PlacesItCanBe`, `QuestCards` | a "?" where the place is certain, or none where it isn't; "One of 10 places" for 5 | `PossiblePlaceTests`; the card by eye |
| Synopsis tables (English): verbs, terms, conditions, place patterns, map names | `QuestSynopsis` | FALLBACK and LONG rows; BREAK | `synopses`; `SynopsisDataTests` |
| An objective's sentence says where with "on", "in", "from", "at" (English) or "auf", "in" (German) before the map's name as the data writes it, alone or in a list | `QuestSynopsis.MapList`, `QuestCards.SaysWhere` | a quest card says the map twice (longer, never wrong) | `ItemCardTests` against the German cache, counts only |
| Exit rules: `Alpinist*`/`RedRebel*` climb, `sniper` or "(Flare)" flare, "(Co-op)" co-op, and Icebreaker's `Heli` a flare exit by internal name and id (`FlaresByHand`); item ids of roubles, dollars, euros, the green flare (red until 2026-10-17: the wiki says green for every flare exit), ice pick and paracord | `ExtractRules` | wrong or missing "to leave through" items; the helicopter's flare line goes | `ExtractRulesTests`; `bring` NO EXIT, FLARE EXITS |
| The extracts a switch gates are kept by hand (2026-10-17: tarkov.dev's `switches` tell none apart; Customs lists one lever on all 28 extracts, The Lab the Med Elevator's three buttons on all 7, Interchange and Reserve none): ten rows by the map's `normalizedName`, the extract's internal name and id, each step a switch's or lock's id in the order of use, each with a position; the data's `switches` count only for an extract the table lacks, where they are on half or fewer of the map's extracts | `Data.TarkovDev.SwitchExits`; `ExtractRules.Needs`, `MapContentBuilder.ExtractSwitchLinks` and `SwitchSteps` | ⚠ a changed id or a step gone drops that extract's badge, its "… first" line and its link; a fix upstream would go unnoticed | `bring` SWITCH EXITS; `ExtractRulesTests` against the cache (fails on STALE, warns on DATA); `SwitchExitTests` and `ExtractRulesTests` on the table's ids |
| The facts tarkov.dev's extracts don't carry are kept by hand (2026-10-17, from the wiki's "Extractions" of each map): 30 rows by the map's `normalizedName`, the extract's internal name and id: seven keys at exit doors, each with its door's lock id; two no-backpack exits; nine single use, the cars with how many leave together; 13 "not always available"; five times (Reserve's and Lighthouse's trains from 16 to 12 and 20 to 15 min left, staying 7; The Way Up from 15 min in; the bunker's door 4 min after its lever; the conduit ~80 s after its pump); one faction. Extracts carried only `id`, `name`, `faction`, `switch`, `switches`, `position`, `outline`, `top`, `bottom`, `transferItem`, `size` | `Data.TarkovDev.ExitFacts`; `ExtractRules.Needs`, `Items`, `Notes` | ⚠ a changed id or a lock gone drops that exit's words and the key from BRING; a fact the data starts carrying would go unnoticed; a train's times changed in a patch read wrong | `bring` EXIT FACTS; `ExtractRulesTests` against the cache (fails on STALE, warns on DATA: a new extract field, a faction the data gives itself); `ExtractRulesTests` on the table's ids. The times: none, step 4 by eye |
| An extract outside its map's bounds (`svgBounds`, else `bounds`) whose internal name is an extract's of a map not drawn with the same artwork is a copy left behind (Ground Zero's UN Roadblock, Scav Bunker, Pinewood Basement on 2026-10-17); a real one past the edge has no namesake elsewhere (six, 1.5 to 16 m out) | `Data.TarkovDev.ExtractRepairs` (in `GameDataLoader`) | ⚠ a real extract that shares a name with another map's and stands past its edge would be dropped; a copy with a new name would be drawn off the map | `bring` OUTSIDE dropped and kept; `ExtractRepairsTests` on made-up maps |
| A map variant's base is the map whose maps.json entry lists it among its `altMaps` (Night Factory under Factory, Ground Zero 21+ under Ground Zero, The Lab (Dark) under The Lab); a map drawn with another's artwork only by its name's start (Ground Zero Tutorial) has none and borrows nothing. A variant's extract with no `faction` has its base's side by internal name (the nearest of two), and a variant with no extracts has its base's, with its switches where it has none (Night Factory's nine and Ground Zero 21+'s six without a side, The Lab (Dark) without extracts or switches, on 2026-10-17) | `ExtractRepairs.BaseOf`, `MapDefinition.AltKeys` | ⚠ a variant whose exits differ from its base's shows the base's sides or exits until the data gives its own (the list read from a screenshot strikes out the wrong ones); a variant tarkov.dev stops listing under `altMaps` loses its borrowed sides and extracts | `bring` FACTION FROM BASE, EXTRACTS FROM BASE; `ExtractRulesTests` against the cache (warns where a rule did less; the tutorial borrows nothing); `ExtractRepairsTests` |
| Borrowed extracts keep their base map's ids, so the hand-kept tables find them under the base map's `normalizedName`: The Lab (Dark)'s elevators, gates and conduit take The Lab's switch chains, badges and facts (the same layout with the lights off; if its power differs in the game, a row can be dropped for it) | `GameData.ExtractsFrom`, `ExtractMapName`; `SwitchExits.RowOf`, `ExitFacts.RowOf` | ⚠ a switch line or fact on The Lab (Dark) that the game doesn't have there | `ExtractRulesTests` (made-up maps, and the cache: the Lab (Dark) elevators say their chains); in the game by eye |
| Customs' `ex_customs_pmc_rail_milbase` (Railroad to Reserve) is a PMC's, which the data tags `scav` | `ExitFacts` (`Faction`), `ExtractRepairs` | PMCs get no row for it again if its id changes | `bring` FACTION BY HAND and EXIT FACTS (DATA once the data says pmc itself) |
| Bosses are mobs whose id starts `boss`; every mob but `pmcUSEC`/`pmcBEAR` is drawn | `GameData.BossMobsOn`, `MapContent` | a boss missing from Plan; ⚠ new AI PMCs drawn as bosses | `spawns` |
| A kill objective's `targetNames` name a boss by the same id the map's bosses list carries (`bossKilla`); the likeliest zone is the largest `spawnLocations` chance | `MapContentBuilder.QuestBosses`, `SpawnZones` | ⚠ a picked or pointed-at quest no longer lights its boss's zones; another zone carries the boss's name at rest | `BossRestTests` on made-up ids only; `render <map> --pick "<quest>"` by eye |
| Spawn sides and categories: `scav`; `bot`, `all`, `sniper` | `MapContent` | ⚠ spawn rings go missing | `spawns` |
| Lock types `door`/`trunk`; switches named `switch_…` are untranslated; activation `Unlock`; hazards `hazard` (up to 50 m²), `minefield`, `sniper`; extract factions `pmc`/`scav`, the rest shared (a variant's extract without one takes its base map's first, `ExtractRepairs`) | `MapContent`, `GameSession.Exits` | ⚠ new values are left out | none: `render` by eye |
| Item sources: `preset` left out; `noFlea`, `minLevelForFlea`, `lastLowPrice`; USD and EUR, the rest shown as ₽; categories most specific first | `ItemSources`, `ItemCards`, `Planning` | wrong source lines, wrong currency | `bring` LIST rows |
| The stash desk's item facts: `lastLowPrice`, `avg24hPrice`, `low24hPrice`, `high24hPrice`, `changeLast48hPercent`, `lastOfferCount`, `basePrice`, `updated` (when tarkov.dev scanned the item's prices: the card's "Prices n min old", 2026-10-19), `stackMaxSize` (a count read past it is a misread, 2026-10-19), `sellToTrader` (`priceRUB`; Fence among the traders), `width` and `height` (unrotated), `hasGrid`; the types `noFlea`, `gun`, `meds`, `injectors`, `provisions`, `armor`, `helmet`, `armorPlate`, `backpack`, `container`, `preset`, and the magazine category `5448bc234bdc2d3c308b4569`; `data.fleaMarket` with `sellOfferFeeRate`, `sellRequirementFeeRate`, `foundInRaidRequired`, `minPlayerLevel`, `enabled` | `ApiItem`, `ApiFleaMarket`, `DeskData`, `DeskValues` | ⚠ a renamed price field leaves cells without a figure; a renamed footprint leaves cells unread (the tie-break fails); a magazine's rounds taken for a condition; no `fleaMarket`: the rates of 2026-10-19; no `updated`: the card's age from the file's fetch; no `stackMaxSize`: a misread count stands | `bring` DESK; `DeskValuesTests`, `DeskSheetTests`, `DeskCardTests` on made-up items |
| tarkov.dev scans the flea's prices every 5 minutes and json.tarkov.dev's item file is cached 5 (owner, 2026-10-19: "Maybe we need to fetch the prices more often then?"): a desk reading asks for it again with its ETag once the saved copy is over ten minutes old, never on a timer (an hour until then) | `GameDataLoader.DeskPricesAge`, `GameSession.RefreshDeskPrices` | slower scans: the requests buy nothing (an unchanged file costs a few hundred bytes); a file no longer cached: a 1.4 MB answer per reading ten minutes apart | the item file's `.meta.json` (`LastModified`, `FetchedAt`) against the items' `updated` after a desk session; `PriceHistoryTests` holds the ten minutes |
| The flea's fee is tarkov-api's `fleaMarketFee` (read 2026-10-19: V0·Ti·4^P0·Q + VR·Tr·4^PR·Q, the larger side's exponent to the power 1.08, Intelligence Center 3 a 30 % cut and Hideout Management 1 % of it a level) | `DeskValues.Fee`, `DeskCopies` (the copy's net and the price that nets the most, about 51.5 times the base price at 0.05/0.05) | ⚠ every flea net off by the change, and the copy's guards with it | `DeskValuesTests`, `DeskCopyTests` pin the formula only; against the game's own fee by eye when it changes |
| The hideout file's `levels[].itemRequirements` with `item`, `count`, `attributes.foundInRaid`; a quest's item needs from `giveItem`, `findItem` (once with its hand-over), `plantItem`, `mark`, `useItem`, `requiredKeys`, `wearing`, `usingWeaponMods`, `neededKeys`, the tree from `taskRequirements` | `GameDataLoader.HideoutNeeds`, `DeskData.Keep`, `DeskKeep` | ⚠ keep notches go missing | `bring` DESK hideout needs; `DeskKeepTests` on made-up quests; the notches by eye (`desk`) |
| The stash desk card's price history (step C, 2026-10-20): json.tarkov.dev's `/{mode}/prices/{id}` (`pve`, `regular`), one file per item, `{"data":[{"priceMin", "price", "offerCount" (recent points only), "timestamp" (ms)}], "translations":[]}`, a point a day for the item's whole history and every 2 h over the last 30 days (read 2026-10-19 on the LEDX: 952 points in PvE, 1,811 in PvP; 2026-10-20: 85 KB, a weak ETag) | `GameDataLoader.LoadPriceHistoryAsync`, `DeskHistory.Parse` | ⚠ a renamed field or another wrapping: the card says "No price history at hand" and shows no 7-day change; points a day apart: the band coarse | `bring` / `desk --audit` DESK HISTORY (UNREAD, FEW) over the cached files; `DeskHistoryTests`, `PriceHistoryTests` on made-up points |
| The day's range on the card: `low24hPrice`, `high24hPrice`, `avg24hPrice` beside `lastLowPrice`; a high over twice the average and the last low taken for one outlier offer (the LEDX's 9,999,999 on 2026-10-19) | `DeskDayRange` | a bar drawn to a spike, or a real high said as an outlier | `DeskHistoryTests` |
| The card's CRAFTS AND BARTERS are the item card's (barters by the item they give, crafts by the item they make, the stations' names) and its KEEP lines name the quests (`tasks`' `name`) and the hideout's stations (`ItemSources.Stations`) | `ItemCards`, `Rules.DeskCards`, `MainWindow.Desk` | the lists empty, or an id where a name was | by eye on a card (`desk point stash 8 0` in the made-up desk) |
| A gun's `properties.defaultPreset` names an item of the `items` payload, of type `preset`, whose icon (`assets.tarkov.dev/<id>-icon.webp`, as every item's) is the gun assembled; weapons that aren't guns (knives, grenades, launchers) have none (164 of 172 guns had one on 2026-10-19; owner, 2026-10-19, DESIGN.md §3, "Trader portraits and item icons") | `ItemSources.PictureId`; `Picture.PictureId` | a gun pictured by its bare receiver again: no preset named, or the preset's icon gone (the cell falls back to the gun's own icon, then the glyph) | `bring` PRESETS, GUN WITHOUT PRESET, PRESET NOT AN ITEM; `WeaponPictureTests` (made-up ids); that the icon is there and is the whole gun: none, `--dev-script` with `show <quest>` by eye |
| A quest requirement's status `complete`; the log's `templateId` is tarkov.dev's task id; trader order as given | `QuestProgress`, `Planning` | quests not tracked or implied | `quests` |
| `raidDuration` is the raid's length | `Planning`, `Rules/RaidTime`, `RaidTracker`; the trains' words step back by it (`ExtractRules.NoteLine`) | ⚠ a wrong MIN LEFT; an open raid closed too early or late; a train's window passed too early or late | none: step 4.7 |
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
| The items a message hands over (type 10 what the quest gives at its start, 12 its rewards, 8 the insurance return) are `items.data[]`, top-level where `parentId` equals `items.stash`; of each only `_tpl` (the item's id) and `upd.StackObjectsCount` (absent for one) are read | `GameLogParser.ItemsIn` | the given-item marks go missing or name a weapon's parts | `GrantedItemsTests`; `LogReplayTests` against the local log fixtures |
| The side: the setup's profile is the menu's for a PMC; no `GameStarting` is a Scav joining; a locally hosted PvE raid can't tell (no local Scav raid seen yet) | `RaidTracker` | the wrong card (PMC or Scav) | `replay` |
| Maps by scene path, then `nameId`, then aliases (`bigmap`, `city`, `shopping_mall`, `factory_day`, …); Ground Zero 21+ by a later location line | `MapIdentity`, `GameSession` | "MAP NOT KNOWN" | `replay` with `data`'s scene paths |

### Screenshots and the extract list

| assumption | where | if it stops holding | check |
| --- | --- | --- | --- |
| File name `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`, `.` as the decimal point; facing from the quaternion | `ScreenshotName` | no position, or a wrong facing | `ScreenshotNameTests`; `render` with a new name |
| The folder `Documents\Escape from Tarkov\Screenshots` | `InstallLocator` | no screenshots seen | `locate` |
| The list in the top right 0.60 × 0.62 of the picture's height, under a green bar, the "??:??:??" column right of it, header "Find an extraction point"; in full light its lightest pixels at 200 of 255 or more, darker only while the raid fades in (then brightened, `ExitListReader.Brighten`) | `ExitListReader`, `ExitList` | nothing read (says "not checked": safe, but silent); a list of one exit refused | the app log; the study log's `exits.none` on a screenshot at a raid's start is a list missed; `ExitListReaderTests` with new screenshots (`Every_real_list_is_read`) |
| The list's names are tarkov.dev's extract names, in the game's language or English | `ExitList.Match`, `GameSession.Exits` | ⚠ an extract the game renamed reads "not on your list" and is struck out though it is open | compare the app log's count with the game's list (step 4.6) |
| A menu screenshot's item grids: cells of 63 px at 1080p scaled by min(width/1920, height/1080), a one-pixel line in the stash and a two-pixel one in a window, the game's cyan outline on marked cells, the short name at the top right, the stack count and the found-in-raid mark at the bottom right, the stash ten wide at the right, the trader's grid on the trading screen hanging on the left edge | `StashReader` | nothing read (no marks: safe), or a wrong count | `shturmap-cli stash <png>` on one stash screenshot and one of the key tool after a patch; `StashReaderTests` with the local screenshots |
| The items' short names on the cells are tarkov.dev's `shortName` in the game's language | `GameSession.StashCandidates`, `StashReader` | an item the game renamed gets no mark | the app log's "cells with items the quests need" against the picture |
| The Tasks screen's OPERATIONAL table (the English game): its header words (Trader, Type, Class, Task, Location, Status, Progress) on one line, the columns where they stand at 16:9 within the picture's left 0.665, a row's status above its middle and the orange countdown hh:mm:ss under it, the percentage over the Progress cell, an expanded row's "Objective(s)" and "Rewards" labels; a trader's tab by TRADING with SERVICES or REPLACE with TASKS | `TasksReader`, `TaskTable` | no table read (the tasks stay as they were), a countdown not taken (the rail says the reading's age), or a trader's tab taken for the stash | `shturmap-cli tasks <png or folder>` on screenshots of the table after a patch; `TaskTableTests` with the local screenshots |
| A task's location is a map's name as the English game writes it, or "Any location" | `GameSession.Operational` (OperationalMap) | ⚠ a task on a renamed or new map is left out (the app log counts it "on no known map") | the app log's "Operational tasks read" line against the picture |
| The GEAR tab for the stash desk (the English game at 16:9; measured at 1440p): the loadout's headings EARPIECE … SHEATH, each slot's box 5 px left of its word and 4 px under it, two cells square (the armband and the dog tag a strip of 0.76, the sling and the back 4.95 wide); the middle column's headings with each part's compartments framed below (a rig's pockets a few pixels apart), a container's own slot first; QUICK USE under the column; the stash pane's SEARCH and SORT TABLE over the stash; a window's bar (magnifier, title 15 px, sort, close) a cell or less over its compartments, its grid's lines two pixels wide; a quick slot's number over a label's start; toasts with the screenshot's path at the lower right | `DeskLayout`, `DeskFrames`, `DeskMatch` | regions not found (the page keeps what it had), a window missed (its cells unread), a slot's label unread; never a wrong name by itself (the label still has to fit at 0.9) | `shturmap-cli desk <folder>` on GEAR screenshots after a patch, with a window open; `DeskRealPictureTests` with the local screenshots |
| The short names on the cells, the loadout's slots and a window's title are tarkov.dev's `shortName` (a title may be the `name`) | `DeskMatch`, `DeskData.Catalogue` | ⚠ a renamed item reads "one of N" or unread; a renamed container's window attaches to none | `desk` on a known page |

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
| 2026-10-08 | story chapters | none in json.tarkov.dev (pve, regular). Its dev host, json-dev.tarkov.dev (6 Oct), has 10 chapters, 322 main objectives: 45 on a map, none with a place. No chapter or section id in the application or notification logs |
| 2026-10-09 | `data` pve, regular, pve de | 500 tasks (PvE), 504 (PvP), 17 maps, 16 traders, 13 definitions |
| 2026-10-09 | `effort` pve and regular | 604 rows (PvE: Go there 211, Find or survive 124, Fight 269); regular 620. No unknown objective types. UNKNOWN TARGET `exUsecFree` (tarkov.dev: "Rogue"): named a fight in `QuestEffort` |
| 2026-10-09 | `bring` pve and regular | no unknown items. NO EXIT: Information Source (" V-Ex_light", with a leading space) and, PvP only, Belka and Strelka (`customs_sniper_exit`): no extract in tarkov.dev's maps has either name, so there is nothing to match; tarkov.dev's data, not a rule |
| 2026-10-09 | `synopses`, `handovers`, `spawns` | no FALLBACK or BREAK (153 LONG PvE, 156 PvP); hand-overs 221 fold, 0 NEAR; spawns at most 25 m from a spawn point |
| 2026-10-09 | story chapters; tests | still none in json.tarkov.dev; 1321 tests passed against the fresh cache, 0 skipped |
| 2026-10-09 | before 0.4.0: `data`, the five audits in both modes, story chapters, tests | the same counts as the morning's runs, the same NO EXIT rows, no chapters; 1407 tests passed against the fresh cache, 0 skipped |
| 2026-10-11 | German quest cards' line of map names, over `pve de` (`ItemCardTests`) | 363 of 936 lines left out (English 577), each under a sentence that says where; none taken wrongly. Kept: sentences that name none or not all of the line's maps, 17 extracts "aus …", 17 transits "von … nach …", 151 sentences still English in the German data |
| 2026-10-14 | before 0.5.0: `data`, the five audits in both modes, story chapters, tests | 500 tasks (PvE), 504 (PvP), 17 maps, 16 traders, 13 definitions. `effort` 603 rows (PvE: Go there 210, Find or survive 124, Fight 269); regular 619 (208, 136, 275): Oil Run's mark is on Customs only now, no longer on Reserve too (tarkov.dev's data; v0.4.0's code gives the same). Otherwise as on 2026-10-09: no FALLBACK or BREAK (153 LONG PvE, 156 PvP), no unknown items or types, the same NO EXIT rows, hand-overs 221 fold, 0 NEAR, spawns at most 25 m off; no chapters. 1623 tests passed against the fresh cache, 0 skipped; `ItemCardTests` 363 of 936, English 578 |
| 2026-10-15 | before 0.6.0: `data`, the five audits in both modes, story chapters, tests, the tour's chapters, the layout check with -SwitchCheck | No count moved since 2026-10-14: 500 tasks (PvE), 504 (PvP), 17 maps, 16 traders, 13 definitions, none without artwork. `effort` 603 rows (PvE: Go there 210, Find or survive 124, Fight 269); regular 619 (208, 136, 275); no unknown targets or objective types. No FALLBACK or BREAK (153 LONG PvE, 156 PvP), no unknown items, the same NO EXIT rows, hand-overs 221 fold (155 own lines PvE, 171 PvP), 0 NEAR, spawns at most 25 m off; no chapters. Tests against the fresh cache, with the local log and screenshot fixtures: Debug 1805 of 1806 passed, Release 1725 of 1726, none failed; the one skipped in each is the website's palette test, as a worktree has no `shturmap.github.io` beside it; `ItemCardTests` 363 of 936, English 578. The tour's seven chapters in English and German (folder build): the words hold; chapter 2 shows no update band, chapter 5 ends on the roll call's lit extracts. Layout check: PASSED, 0 missing, overflowing, trimmed or overlapping in every language and size (and 0 untranslated in the pseudo-language), 52 views the same after a switch as at a fresh start; 130 runs in 44 minutes |
| 2026-10-16 | before 0.6.0: the re-check of the third QA pass's fixes (targeted, on c6618a2; the owner's call) | Every fix holds at 900×560 and 1600×900 in English and German: F1 to F6, F11, E3, E4, E7, nothing else changed. `frames customs` at a 500×481 viewport: list-callout's largest jump 6.0 px (was 143.8), 22 leaders and none twice (was 41, 8 twice); +/− steps and a notch never zoom a frame by a tenth. Layout check without -SwitchCheck over plan, raid, quest, tour5 and whatsnew in three languages at two sizes: PASSED, every count 0. The three replay scripts as their .md say and at 900×560. The empty settings.png of 2026-10-15 no longer reproduces (59 panels counted, none empty). Open, none in a player's path (docs/NEXT.md, E8): `--snapshot` failed twice while three snapshot apps rendered at once; `layout-check.ps1` says PASSED for a view with pictures missing; about 95 of 2,122 pictures opened. Tests on main: 1832 passed, 0 failed, 0 skipped |
| 2026-10-17 | before 0.6.1: `data`, the five audits in both modes, tests against the fresh cache, the What's New card | No count moved since 2026-10-15: 500 tasks (PvE), 504 (PvP), 17 maps, 16 traders, 13 definitions. `effort` 603 rows (PvE: Go there 210, Find or survive 124, Fight 269); regular 619 (208, 136, 275); no unknown targets or objective types. No FALLBACK or BREAK (153 LONG PvE, 156 PvP), no unknown items, the same NO EXIT rows, hand-overs 221 fold (155 own lines PvE, 171 PvP), 0 NEAR, spawns at most 21 m off. Tests against the fresh cache, with the local fixtures: 1839 passed, 0 failed, 0 skipped. The What's New card at 900×560: both 0.6.1 lines whole with their pictures. Not run, on the owner's word ("land this", after the UI QA passes were cut back the same evening): the tour's chapters, the layout check and the release QA pass; the changes since 0.6.0 had their own targeted pass on 2026-10-16 (docs/NEXT.md). The story chapters weren't checked |
| 2026-10-17 | the exits verification: the wiki's "Extractions" of 13 maps against the release cache, both modes | Two rules fixed. The switch rule had never fired: every `switches` entry (Customs' 28, The Lab's 7) fell under the "most extracts" rule, and its name check never matched ("ZB-013" against "ZB-1013", "Med Elevator" against "Medical Block Elevator"); the ten gated extracts are kept by hand now (`SwitchExits`), `bring` SWITCH EXITS "as the data has them" in both modes. The flare exits take a green flare, not red (all six on the wiki, and the RSP-30 green's "Usage"), and Icebreaker's helicopter is one (`FlaresByHand`), `bring` FLARE EXITS as the data has it; `bring` Exit rows now say the RSP-30 green (PvE 10 Exit rows, PvP 9). The verification's other findings (exits not always open, timed windows, keys at exit doors, no-backpack exits, wrong factions in the data) are not built |
| 2026-10-17 | the exit facts and the data rules: `bring` pve and regular, tests against the cache | The rest of the verification built: `ExitFacts` (30 rows: keys at seven doors, two no-backpack exits, nine single use, 13 "not always available", five times, the Customs rail's side), `bring` EXIT FACTS "as the data has them" in both modes; Exit rows as before (PvE 10, PvP 9: no quest leaves through a door with a key). The rules for the data's defects, the same in both modes: OUTSIDE dropped 3 (Ground Zero's Pinewood Basement 14.7 m out, UN Roadblock 428.9 m, Scav Bunker 581.8 m) and kept 6 (Factory's and Night Factory's Courtyard Gate 1.5 m, Customs' Railroad Passage 15.8 m, Woods' RUAF Gate 5.6 m, Interchange's Path to River 15.9 m, Icebreaker's helicopter 1.5 m); FACTION FROM BASE 15 (Night Factory 9, Ground Zero 21+ 6); EXTRACTS FROM BASE 2 (The Lab (Dark) 7, Ground Zero Tutorial 6); FACTION BY HAND 1. Tests: 1960, 1924 passed, 36 skipped (a worktree's: no log or screenshot fixtures, no Release build, no website beside it), 0 failed; the cache-backed ones all ran |
| 2026-10-17 | the data rules, follow-up (coordinator): a variant's base by maps.json's `altMaps`, borrowed extracts found under the base's rows; `bring` pve and regular | EXTRACTS FROM BASE 1 in both modes: The Lab (Dark) takes The Lab's 7 extracts and 15 switches, and its elevators, gates and conduit say The Lab's chains and facts; Ground Zero Tutorial (drawn with Ground Zero's artwork by its name only) takes nothing. The other lines as above. Tests: 1962, 1926 passed, 36 skipped, 0 failed |
| 2026-10-17 | before 0.6.2 | On the owner's word ("You do not need to verify it") no check was re-run: the data and the audits are the morning's (no count moved since 2026-10-15), 1996 tests passed, 0 failed, 0 skipped on 63ac845 with the local fixtures, CI green; the tour, the layout check and the QA pass not run |
| 2026-10-19 | the stash desk's step B part 1: `desk --audit` pve and pvp (cache only); `desk` over the GEAR and stash truths | PvE: 5,006 items with a short name, NO PRICE 608, NO FOOTPRINT 24, ONE OF N 759 of 3,344 flea-priced (not guns); PvP 607, 24, 690 of 3,159; both: fee 0.05/0.05, found in raid not required, from level 15, hideout needs 317 (119 items). The GEAR truth 29 of 37 right, the stash truths 129 of 148, none wrong (docs/NEXT.md) |
| 2026-10-19 | before 0.7.0: `data`, the five audits in both modes, story chapters, tests against the fresh cache, the tour's chapters, the layout check (plan, raid, whatsnew) | One quest fewer in each mode, tarkov.dev's data: 499 tasks (PvE), 503 (PvP); 17 maps, 16 traders, 13 definitions, none without artwork. The quest that went had one Go there row and one hand-over that folded: `effort` 602 rows (PvE: Go there 209, Find or survive 124, Fight 269), regular 618 (207, 136, 275); hand-overs 220 fold (155 own lines PvE, 171 PvP), 0 NEAR; which quest it was isn't known (no copy of the earlier file is kept, and no other quest names it). Otherwise as on 2026-10-17: no unknown targets or objective types; no FALLBACK or BREAK (153 LONG PvE, 156 PvP); no unknown items, the same NO EXIT rows; SWITCH EXITS 10, FLARE EXITS 1 and EXIT FACTS 30 "as the data has them"; OUTSIDE dropped 3 and kept 6, FACTION FROM BASE 15, EXTRACTS FROM BASE 1, FACTION BY HAND 1; Exit rows PvE 10, PvP 9. Today's rows: PRESETS 34 weapons pictured in BRING, 31 by their default preset, in both modes (bare: a bayonet and two under-barrel launchers), GUN WITHOUT PRESET and PRESET NOT AN ITEM none; the Tasks screen's table, `TaskTableTests` on the nine local screenshots, passed; the stash desk's prices come with the desk. Spawns at most 25 m off (Shoreline); among the bosses Scourge (`bossScourgeEventLab`, The Lab, an event's), drawn as a boss as the rule has it. No story chapters. Tests against the fresh cache with the local fixtures: Debug 2147 of 2148 passed, Release 2065 of 2066, 0 failed; the one skipped in each is the website's palette test (a worktree). The tour's seven chapters in English and German (folder build, 230c18a): the words hold; chapter 1 names the tasks ("Your stash and tasks in a menu screenshot", „Stash, Dailies und Weeklies …“); no chapter shows a card's OPERATIONAL section (the fake game has no tasks). Layout check of plan, raid and whatsnew in English, German and the pseudo-language at 900×560 and 1600×900: PASSED, every count 0, every run's four pictures there; the What's New card's four 0.7.0 lines whole at 900×560 in both languages. Not run: the other views, -SwitchCheck and the release QA pass (the UI QA role's) |
