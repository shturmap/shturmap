# Shturmap design

This document is binding for anyone changing Shturmap, people and agents alike. Read it before you change
behaviour or UI, follow it, and update it in the same change when a decision changes.

## 1. What Shturmap is

Shturmap answers two questions for an Escape from Tarkov player, on a second monitor:

1. **Before a raid: where should I go, and what do I need to bring?**
2. **During a raid: where am I, and where is my next objective?**

Between raids it gives a third answer, in a view of its own (owner, 2026-10-19; designed; its reading and data built the
same day, step B part 1, its view the same day, part 2, the card and the click to come with step C; docs/NEXT.md):
**what is this worth, and what should I ask for it?**, for the items on a screenshot the player took of their own
stash. That view is the stash desk; the raid planner stays the other view. The recommendation was a separate app; the
owner kept it in one: "It makes sense to keep it in one app since shturmap idea is to be mostly as hands off as
possible", "The map could still be visible, the app just helps with additional information", "one raid planning view
and one stash desk view", "Separate apps would mean less screenspace if you have them open at the same time and one
feedback we already got was that readability might be an issue. I still feel we can leverage a lot with the
tooltip-modes we already have implemented" (the cards: hover, held, nested; §4, "Quest cards"), and "In the end, it's
a app made for gamers, so it should also be fun to use." The desk alone reads a stash's item grids against the whole
catalogue (§2, "For the stash desk"). An answer from a loot screenshot in a raid, beside the map, which the owner
described too, is not part of the desk (the coordinator's scoping, 2026-10-19) but a step of its own, the in-raid
glance: its when, what and what is kept decided by the owner the same day, its look chosen from its own panel the same
day (Variant 1, with the looted container's or body's layout mirrored), its reading and its plate built the same day
(§2, "For the stash desk"; §4, "Screen anatomy", *The in-raid glance*).

That is the whole product. A feature belongs in Shturmap only if it helps one of these three questions and works
without the player having to click during a raid. When in doubt, leave it out. One exception, the owner's, for
eye candy at a raid's end: the raid replay (owner, 2026-10-07: "more on the eye-candy side of things like the loading
animations"; "Map drawing", *The raid replay*). And one for learning the app: the tour at a first start (owner,
2026-10-09: "a proper first-time-opening tour of the app ... Here we can be a bit more flashy with nice animations";
§4, "Screen anatomy", *The tour*). And one for a quest handed in: the QUEST COMPLETE cue (owner, 2026-10-07: "For
completed quests, also make a nice animation"; §4, principle 11). And one in a raid, for the extract list just read:
its roll call (owner, 2026-10-15: "When the extraction list is read, I want a nice fancy animation in line with the
others where a list of the extracts appears in the middle of the screen along with their icons. Then, the icons move
where extracts are (also outside of the current view pane) so it contextualizes them and the player knows where they
are"; from a panel of four, 2026-10-15: A; §4, principle 11, and "Map drawing", *The player's extracts this raid*).
And one for the daily and weekly tasks a Tasks screenshot brought: their cue, one for all of them (owner, 2026-10-19,
after trying the dev build: "they should get the same animation as the other quests. Be mindful of multiple missions
started at the same time, we might want to have them in one animation included"; §4, principle 11).

**Not goals** (declined on purpose; don't add them): hideout tracking, loot or container maps beyond what quests
need, item scanning by hotkey, ammo charts, squad sharing, achievements, overlays on the game window, statistics
dashboards, a settings maze. Of the market, the stash desk's one view and nothing more: no register of the stash over
time (the desk mirrors the screenshot in hand; outside it, what Shturmap learned of having an item shows only on the
rows that need it, §4, "What you have"), no watchlists or alerts, no stash value over time, no buying or selling
automation, no flip or arbitrage finders; outside the desk a price appears only as part of "where to get" an item a
quest needs. Struck on 2026-10-19 for the desk (owner, above): "item prices or a flea-market view" and "a stash
register or inventory browser". For the desk as for everything else: when in doubt, leave it out.

## 2. Ground rules (game terms of service)

Shturmap reads only files the game writes for the player, plus public community data:

- screenshot **file names** in `Documents\Escape from Tarkov\Screenshots`, for positions; of a screenshot taken in
  a raid, **the top right corner of its picture**, for the game's extract list; and of a screenshot taken in the
  menus, **the item grids its picture shows**, for the items the active quests need (both below; the player can turn
  each off, and no other part of any picture is looked at);
- `application_*.log` and `push-notifications_*.log` in the game's `Logs` folder, with shared read access;
- `Control.ini` and `Game.ini`, read-only.

Its own code never opens a handle to `EscapeFromTarkov.exe`, reads or writes its memory, injects, hooks, sends input,
registers global hotkeys, captures the screen, draws over the game, edits game files (including
`Logging.config`), or reads `backend_000.log` or launcher credentials (only `gamesRootDir` is read from the
launcher settings). `SafetyTests` fails the test run if forbidden APIs appear in `src/`. Keyboard shortcuts work
only while Shturmap's own window has focus.

**The installer is the one exception, and the texts say so** (review of 2026-10-04; owner the same day: say it
precisely rather than change how updates work, for now). Velopack's `Update.exe`, which installs, updates and
removes Shturmap (§8, "Distribution"), closes running copies of Shturmap first. For that it lists every running
process and opens each one with `PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE`, reads the path of its
executable, and ends only those under Shturmap's install folder (its `get_processes_running_in_directory`; checked
in the installed 1.2.161 binary's imports and in Velopack's source). So while it runs (the Setup, an update being
applied at Shturmap's start or on RESTART NOW, an uninstall) a running game is asked for such a handle like every
other process; nothing is read from it or written to it. `SafetyTests` reads `src/` only and can't see this. The
README says it under "The installer and running programs", with the advice to install and update while the game is
closed; help says only what holds for Shturmap's own code. Still open: applying updates in a way that looks at no
other process (an apply step of our own, or applying only on the player's word).

**One corner of a screenshot's picture is read, for the extract list** (owner, 2026-10-05: "The currently open exits
are displayed on the upper right corner of a screenshot ... It would be great if we could check a screenshot if it's
taken if it contains this information ... read it and update the map accordingly"). Until then no picture was ever
opened: positions come from the names, and the Tasks-screen reading of September was removed on 2026-10-01. The game
gives each raid its own extracts, by where the player starts, and neither its logs nor tarkov.dev's data say which;
it shows them in a list at the top right, when a raid starts and when the player asks for it (O twice by default).
That list was the one thing read from a picture until the menu screenshots below (2026-10-11); each reading is kept
this narrow (`ExitListReader`, `Shturmap.Core.Screenshots.ExitList`):

- **Which pictures:** only a screenshot the watcher reported as new, named with a position, while the log shows a raid
  running on a map the data knows. Never a menu screenshot, never one already in the folder, never while a raid loads.
- **How much of it:** the top right corner (0.60 by 0.62 of the picture's height). A corner whose lightest pixels
  stand below 170 of 255 is first brightened until they are white again, by up to 8 times (`ExitListReader.Brighten`):
  the game fades a raid in from black, and its first seconds are when the list shows by itself, so a screenshot taken
  then is dark all over, the list with it (2026-10-15: of the owner's ten latest screenshots that showed the list, five
  peaked at 69 to 121 of 255, the bar's green at a third of its own, and none of those five was read, without a trace;
  in full light the list's lightest pixels stand at 200 or more). If no green bar stands near its top
  (the list's header), nothing is read and the picture is let go. With the bar, the words in the corner are read with
  the text recognition built into Windows (`Windows.Media.Ocr`): on the PC, with no download, no model of Shturmap's
  own and nothing sent. On a full-size screenshot a look takes 110–180 ms, most of it decoding the PNG.
- **What is kept:** which of the raid map's extracts the rows name, and whether the game marked a row "??:??:??".
  No pixel and no other word is kept, logged or sent; the study log notes counts only (`exits.read`, with how much
  the corner was brightened; `exits.none` for a raid screenshot that showed no list, so a list that went unread shows).
  It is forgotten when the raid ends.
- **How:** the file is opened for reading, shared with the game that may still be writing it, and only once it is
  written to its end (a PNG's IEND chunk; half a list read as the whole would call the missing rows' exits closed);
  it is tried for about three seconds, then left. Nothing is written to the screenshots folder. This is a file the
  game saved for the player, like the logs: no screen is captured (`SafetyTests` still forbids that), nothing is asked
  of the game, and a list is read only from a screenshot the player took with their own key press (the rule below,
  "never take a screenshot for the player", stands).
- **The player's say:** "Read the extract list from screenshots" in settings, on unless unticked (a setting that had to
  be found first would leave EXIT guessing for most players; principle 1). Unticked, no picture is opened. Its note,
  help, the README, PRIVACY.md and the website say what is looked at; the diagnostics carry the setting's state and
  the recognition's language.

This widened what Shturmap takes from the game's files by one thing, on the owner's word; BSG's 4.3.4 (below) speaks of
"information reproduced ... by the Game" either way, and the texts that named the risk still do.

**The item grids in a menu screenshot are read, for what the quests need** (owner, 2026-10-11, after a design panel
and a spike on their own screenshots: "all as recommended", then "start phase 1. Continue with phase 2 as well";
docs/NEXT.md, "2026-10-11: what you have, the spike"). The pain point: "I don't know which items I have in my
inventory that are required for quests, for example keys". Neither the logs nor the data say what is in the stash;
the game draws it as grids of cells, each with the item's short name on it, in the menus. So a screenshot the player
takes there with the game's own key is read for those grids, and what it showed of the items the active quests need
becomes a mark on their rows (§4, "What you have"). The reading is kept as narrow as the list's (`StashReader`,
`GameSession.Stash`):

- **Which pictures:** only a screenshot the watcher reported as new, named without a position (the game names a
  menu screenshot so), while the log shows the menus (not a raid loading or running). Never one already in the
  folder, never a raid screenshot (those keep their one corner). The trading screen (a grid of cells hanging on the
  screen's left edge, the trader's; the stash's cells show prices there, not names) is recognised and nothing is read
  from it. The messages screen (owner, 2026-10-11: a message's attachments, a trader's delivery or a reward, "will be
  on the left hand side of the screen", in a grid of the stash's width set in from the edge by most of a cell where
  the trader's hangs on it) is told from it: the attachments aren't the player's until taken and are never read; the
  stash beside them is read as on any other screen (`StashGridPlace.Message`, a drawn test). On the owner's two
  screenshots of it the attachments' faint lines give no grid at all, so they read as a stash page either way.
- **How much of it:** the item grids the picture shows (the stash, a container's window): their cells' labels, read
  with the text recognition built into Windows (`Windows.Media.Ocr`, on the PC, nothing sent), their stack counts
  (a count of one digit reads as one: "at least" holds) and the found-in-raid mark (a drawn shape of Shturmap's own
  is correlated at the mark's place, never the game's icon); against the short names of the items BRING lists for any map (keys, items to bring, gear, mods; not
  weapons), never against the whole catalogue (for these marks; the stash desk's reading, below, is for its own
  view). A cell whose label fits none of them says nothing; a name is never taken from the picture alone
  (tarkov.dev's grid image of a needed item, when the cache has it, can only veto a
  label whose art plainly disagrees). The spike of 2026-10-11 measured this on the owner's screenshots: 46 of 48
  keys, 43 of 43 medical items and 51 of 55 items of a stash page right, none wrong.
- **What is kept:** per item id, the count seen (at least), how many of those carried the found-in-raid mark, when,
  and from which source, in `stash.json` in the data folder (§8, "The stash file"). No pixel, no path, no word that
  didn't match; the study log counts cells, never names. The file never goes into a report.
- **How:** as the list's: the file opened read-only, shared, whole; nothing written to the screenshots folder; no
  screen captured; a screenshot the player took with their own key press, never one taken for them. With "Delete
  screenshots after reading" ticked it goes once read, as a raid screenshot does (below).
- **The player's say:** "Read your stash, gear and tasks from menu screenshots" in settings ("Read your stash from menu
  screenshots" until 2026-10-19, when the tasks below joined it, then "Read your stash and tasks …" until the desk's
  words the same day, below), on unless unticked, beside the list's tick and with a note that says what is looked at
  and what is kept. Unticked, no menu screenshot is opened; the marks learned stay until the player retracts them. Its
  note, help, the README, PRIVACY.md, the tour's first chapter and the website say it; the diagnostics carry the
  setting's state and the recognition's language.

**The OPERATIONAL table of a menu screenshot is read, for the daily and weekly tasks** (owner, 2026-10-17: "Add
dailies and weeklies via screenshot"; from a panel of four, 2026-10-19: B, "Use B, the recommended one"; the spike of
the same day, docs/NEXT.md). The game's operational tasks (dailies and weeklies) are in neither its logs nor
tarkov.dev's data; the game lists them on Character › TASKS › OPERATIONAL. So a screenshot the player takes of that
table is read as the stash's is, in the same path, and its tasks show where the map's work is (§4, "Screen anatomy",
*Operational tasks*; `TasksReader`, `TaskTable`, `GameSession.Operational`):

- **Which pictures:** the stash's (above): a screenshot the watcher reported as new, named without a position, while
  the log shows the menus. Its picture is opened once for both readings (`MenuScreenshotReader`): the tasks reader
  looks first, and a picture with the table's header (Trader, Type, Class, Task, Location, Status, Progress) goes to
  it alone; the stash reader never sees it, and sees every other picture as before. A trader's TASKS tab (TRADING with
  SERVICES, or REPLACE with TASKS) goes to neither: its grid is the task's rewards. A Tasks table without any countdown
  (STORY or SIDE, presumably) is told and not read.
- **How much of it:** the picture's left two thirds (0.665 of its width: the right third carries the player's name
  and money on a trader's tab and the game's toasts with the screenshot's path), read with Windows' text recognition
  in English (the table's words are the English game's; the German game's aren't known): per row the task's name,
  location, status and time left; an expanded row's objective lines with their counts n/m. **Rewards are never read
  or kept**: the expanded row's rewards make no row. The digits (the countdown, the percentage) are what Windows'
  recognition drops, as the raid timer's (below): each row's own bands are read 14 ways and the value most of them
  read is taken (the spike: the countdowns 12 of 12, the percentages 7 of 12, none wrong). The countdowns must then
  pass checks (`OperationalChecks`): within 0 and 7 days; rows that share a countdown (within a minute of each other)
  agree to 2 s, else the one more of the ways agreed on keeps its own and on a tie neither does; the same task's expiry
  from two screenshots agrees within 5 s, else it has none (which is wrong can't be told). A countdown that fails
  leaves the task the expiry an earlier screenshot gave it, else none: then it is kept until a newer screenshot
  replaces the set, and the rail says the reading's age instead of its time left. The expiry is the screenshot's time
  (the watcher's, the file's creation, to the second; never the minute in its name) plus the time left. Progress: the
  percentage read when it passes (with one counted objective on the same screenshot, at most a point off its count),
  else from the objectives' counts, else none.
- **What is kept:** per game mode, per task its name, its map (a tarkov.dev map id, from the data's map names as the
  English game writes them; a variant's base map; none for "Any location"; a task whose location names no map of the
  data is left out), its objectives' words with their counts, its kind, its progress, its expiry or none, and when it
  and its objectives were read, in `operational.json` in the data folder (§8, "The operational tasks file"). A newer
  table screenshot replaces the mode's set (a task gone from it was handed in, replaced or ran out); a row expanded on
  it brings that task's objectives, a folded row keeps the ones read before. A task goes at its expiry. No pixel, no
  other word, nothing of the picture's right third; the study log counts rows, never names; the file never goes into a
  report.
- **How:** as the stash's: the file opened read-only, shared, whole; nothing written to the screenshots folder; no
  screen captured; a screenshot the player took with their own key press, never one taken for them; deleted after
  reading when "Delete screenshots after reading" is ticked.
- **The player's say:** the stash's one tick, its words widened to cover the Tasks screen (owner, 2026-10-19, B: one
  tick, no new setting): "Read your stash and tasks from menu screenshots" (widened again for the desk, below); its
  note says the OPERATIONAL tab's daily and weekly tasks are read, never their rewards. Unticked, no menu screenshot is
  opened; the tasks kept stay until they run out. Its note, help, the README, PRIVACY.md and the tour's first chapter
  say it.

**For the stash desk, the item grids are read against the whole catalogue** (owner, 2026-10-19, keeping the price view
in Shturmap as a view of its own, §1; designed the same day, §4, "Screen anatomy", *The stash desk*; its reading and
page built the same day, step B part 1: `DeskReader`, `DeskLedger`, `GameSession.Desk`; the view, part 2; docs/NEXT.md).
The desk mirrors a screenshot's
grids with each item's prices, so it has to name every cell, not only the ones BRING lists. Its keep mark has three
levels from the quest dependency tree, NOW (an active quest's BRING needs the item), NEXT (a quest the active ones
unlock needs it) and LATER (further down the tree), plus a hideout module's need, regardless of the hideout's progress
(the logs don't say its level, and hideout tracking stays a not-goal, §1), each with its found-in-raid flag (owner,
2026-10-19: the keep mark's levels, not a planner screen of their own; docs/NEXT.md). The page in hand is kept in
`desk.json` and priced from tarkov.dev's item file, asked for again at most every ten minutes while the desk is used
(owner, 2026-10-19: "Accept, once an hour"; then, after trying the desk the same day: "Maybe we need to fetch the prices
more often then?"; §3). The boundary widens for the desk alone, and no further (and, decided the same
day, for the in-raid glance: "Which pictures", below):

- **Which pictures:** the stash's (above): a menu screenshot of the player's own stash or inventory, new, named
  without a position, while the log shows the menus. The inventory is the GEAR tab (Character › GEAR), the player's
  loadout beside the stash, so a screenshot taken right after a raid shows what came back (owner, 2026-10-19: "the
  stash also has the player loadout in view and when a raid ends the player might want just take a screenshot of the
  items they have and get an overview of what to do with that"). Never a raid screenshot for the desk, the menus'
  view. **In a raid, for the in-raid glance alone** (owner, 2026-10-19, three choices; its reading built the same day,
  the reader round, and its plate; until then a raid screenshot kept its one corner; the coordinator's scoping of
  the same day had left the glance a later step with a boundary for the owner to decide): every in-raid screenshot that
  shows an inventory screen (the player's own loadout, or a looted container or body) is read as it arrives, as the
  extract list is and after its corner, for its grids, as the desk reads them (below; the nested windows open there too
  when looting, owner, 2026-10-19: "This will also happen in-game btw"); nothing of it is kept, and the menu
  screenshots' tick, its words widened by "and in a raid" (with the plate, 2026-10-19: "Read your stash, gear and tasks
  from menu screenshots, and loot in a raid"; its note, help, the README and PRIVACY.md say it), says yes or no (§4,
  "Screen anatomy", *The in-raid glance*). Never a trader's grid, a message's attachments or another player's
  inventory in a group screen. **Built** (the reader round, 2026-10-19; `DeskReader.ReadRaidAsync`, `DeskLoot`,
  `GameSession.Glance`): the raid's inventory screen is told by the character screen's tabs at its top (OVERALL, GEAR,
  HEALTH, SKILLS, read first in a strip of the picture, about 30 ms: a picture of the world goes no further), the
  loadout's headings, and neither the stash pane nor the menus' bar at its foot (BUILDS, HANDBOOK, MESSENGER); QUICK
  USE stays. Looting, the LOOT pane fills the right third: "LOOT" at its head, the container's kind in the game's words
  at the screen's right edge ("BODY", "SAFE", "DRAWER", "WEAPON BOX"). A container's pane is one grid (its frames on
  one lattice one grid, starting under the head). A body's is its slots under the loadout's headings (a weapon's
  "?/30" says nothing) and its TACTICAL RIG and BACKPACK (each a slot and a grid beside it) and POCKETS, each with the
  game's search state: "UNSEARCHED" where its grid stands, "SEARCHING ✕ 0:00" at its heading and the items found so
  far; contents not shown are unseen ("+"), never zero, and so is what the pane's scrolling cut at its foot. A part
  whose heading scrolled away above is not read; a slot with nothing in it (the rig taken) has no part. The game's
  experience toasts ("Searching experience", "Looting experience") are left out with what they cover, as the path's
  are. The page holds the player's regions (ON YOU) and the pane's (LOOT, titled "LOOT · BODY"); it is published for
  the glance (`SessionSnapshot.Glance`) and goes with the raid: no file, never the desk's page in hand. Measured on the
  22 screenshots of one raid (`shturmap-cli desk --raid`): the 9 that show an inventory told, the 12 of the world and
  the menus' one not; the LOOT panes' cells 16 named, 12 "one of N", 11 unread (no truth written; the four panes looked
  at read as they show); about 1 s an inventory picture, 30 ms one of the world.
- **How much of it:** the item grids' cells, their labels, counts and found-in-raid mark as above, and their positions
  and footprints, for the mirror; matched against every item of tarkov.dev's catalogue, for the desk alone (What You
  Have's marks still come from the items BRING lists only). On the GEAR tab that is three regions (five screenshots of
  2026-10-19 (GEAR tab), 2560×1440, PvE), all read, for the desk alone: the loadout's slots at the left (earpiece,
  headwear, face cover, armband, body armour, eyewear, dogtag, on sling, holster, on back, sheath), each printing its
  item's short name and, where it has one, its condition ("46/50"); a middle column, POCKETS, SPECIAL SLOTS and the
  equipped BACKPACK, TACTICAL RIG and POUCH, each a slot's item with its grid inline; and the stash's grid at the right.
  The middle column and the stash scroll each on its own, so a screenshot holds one page of each. In a grid a condition
  ("147/147", "1469/1800") stands where a stack's count does, and is read as the count is. **Nested containers** (owner,
  2026-10-19: "keep in mind of stackable things, like a bag stashed in a bag that contains additional items"): a
  container in a grid (a Zhuk rig in the backpack) hides its contents, which the game shows only in a floating window
  titled with its name (with search, sort and close buttons) that the player places anywhere, over what lies under it.
  Such a window is read as that container's grid (the stash reader already tells a container's window apart); the cells
  it covers aren't read from that picture; contents no window showed are never guessed. Skipped, as the operational
  table's right third is: the stash pane's head (it prints the player's money) and the game's toasts at the lower right
  (they carry the screenshot's path). A cell that fits more than one item is said honestly, never guessed: "one of N",
  with the group's price range and a "?", and its hover card lists the candidates for the player to pick with one click
  (of the 3,159 flea-priced items that aren't guns or presets, 672 share their short name and footprint with another; a
  silent guess prices the wrong item). A name is never taken from the picture alone. The stash spike measured the false
  names against the whole catalogue (2026-10-19, docs/NEXT.md): the label alone is safe at a fit of 0.9, not at the
  quest path's 0.8, so the desk names a cell at 0.9, the item file's footprint (either way round) the tie-break; a slot
  has no footprint. **Built** (step B part 1, 2026-10-19; `DeskReader`, `DeskLayout`, `DeskFrames`, `DeskMatch`): the
  screen told by its headings, read once over the whole picture (the loadout's EARPIECE … SHEATH, the middle column's
  TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK, POUCH, the stash pane's SEARCH and SORT TABLE; the English game's
  words); a slot's box under its heading; the middle column's compartments as rectangles of whole cells framed on all
  four sides (a rig's pockets stand a few pixels apart, no lattice), each under the lowest heading above it, a
  container's own slot the first under BACKPACK, TACTICAL RIG and POUCH; a compartment the column's scrolling cuts is
  left out (the page keeps what it had; read as far as it shows since 2026-10-25, *The layout round* below); a window by
  its title bar (the magnifier before the title, sort and close after it, or the title's size: 15 px at 1440p, a label
  11 to 14; narrowed on 2026-10-25, below) over compartments of its own, or by its grid's two-pixel lines when no title
  is read (its search field open: "ITEM SEARCH"); a window's lattice moved onto the lines
  between its cells, within a sixth of a cell, and the frame found a few pixels off it taken as the same compartment,
  never a second (the owner's test of 2026-10-19: the Keys window's lattice stood on the window's outline 6 px left of
  its cells; after the game's SORT its 71 keys read as one odd shape, and before it the frame beside the lattice read
  the keys once and a "?" across the other; two compartments of a region never overlap, so one lying over another is
  read once); a shape whose label fits nothing (a hover highlight or light art hid the lines between its items) is
  read cell by cell, an item of one cell where its label fits one. What a window or a toast covers is left
  unread, and a cell running under it with it. Where the grid's lines split an item, its label at the top right claims
  the cells its footprint takes from there when no other label fits in them; where a label fits items of two
  footprints and both fit there, the cell is "one of N" over both. A short label (three letters or fewer) is read once
  more. The loadout is a strip of slots, no grid. A slot's label is matched against the items its slot takes, by the
  game's categories (headphones, headwear, face cover, armband, armour, eyewear, the dog tags' "Other", weapons, knives,
  a rig, a backpack, a secure container; the special slots their own items), and a special slot holds its item in one
  cell whatever its footprint (UI QA pass of 2026-10-19, F4: the AK-12 on the sling was "one of 5" with its magazine,
  stock, grip and muzzle device; "Surv12" read "SurvL", a lubricant). Where a pocket's or a rig's cell shows a quick
  slot's number (its light plate, measured), the label is a name's end even where the recognition left the number out
  ("splint", the Splint or the Alu splint; "ALOK-B", the CALOK-B). A print the band reads nothing of is read again by its
  brightest channel (the game's red "1/3" of a used-up med) and, on a cell wider than one, in a wider band (the armour
  slot's "147/147"). Measured on the owner's screenshots (`shturmap-cli desk`): the GEAR tab's stash, 37 hand-labelled
  items, 29 right, 2 "one of N", 6 unread, 0 wrong; the three stash truths of 2026-10-11 (148 items: the key container, the
  medical case, a stash page) 129 right, 16 "one of N", 3 unread, 0 wrong; regions on the five GEAR pictures: the
  loadout and the stash on each, the middle column's parts in view (4 to 6), the Zhuk window on the two that show it,
  attached to its cell in the backpack across the readings; about 2 s a picture. **The reader round** (2026-10-19;
  `StashGrids`, docs/NEXT.md): an item's footprint is its frame's, not the lines that show. The grid's own line shows
  through a wide item's background, faintly (18 grey levels at most on the owner's pictures, the items' frames 25 to
  130), so a line between two cells that stands out by less than 0.45 of the grid's frames (their median, per grid) is
  no frame, and a cell read as empty beside such a line is the item's dark art (a gun's receiver); the cells joined so
  are one item where they make a whole rectangle, and stay as the lines that show give them where they make none (a
  frame beside bright art stands out as faintly: a junk box's top under a case). Where such a rectangle's label names
  no item of its size, its pieces are read instead (two items of one width, one over the other: a keys case over a
  vest). A gun's footprint in the item file is its bare receiver's (the M4A1 one by one), so a gun takes a larger cell
  where no item fits it exactly. The windows' lattices are moved onto their cells' lines in the grid finding itself, so
  the quest marks' reader (`StashReader`) reads the one lattice the desk does, and reads an odd shape cell by cell as the
  desk does. Measured: the 20:38 page's 72 items, hand-labelled with their footprints, read whole 61 → 72 (right 51 →
  61, one of N 10 → 11, 0 wrong; its 34 "?" pieces of 11 rifles, silencers and scopes gone); the GEAR truth 30 right,
  2 one of N, 2 unread, 0 wrong (34 items: three bottles whose labels lie above the visible grid left it); the stash
  truths unchanged; the quest marks' reader finds the Keys window's 72 keys on both pictures (23 and 28 rectangles of
  the window before). **The layout round** (owner, 2026-10-25, decision 3 of the session's decision page; docs/NEXT.md;
  `DeskLayout`, `DeskFrames`): the 13 menu screenshots of the test of 2026-10-19 made another set of regions each, with
  windows made of misread headings and of a region's grid, and the page never dropped them (the owner's page kept 13
  regions, 4 of them such; §4, *What it mirrors*). Fixed, each on those pictures:
  - **A heading one letter off still counts**: a word in capitals, the heading's word of four letters or more with one
    letter dropped, added or changed (the first one too; accents aside), is that word ("*ACE COVER" is FACE COVER;
    "ÄRMBANI", ARMBAND half under a window's edge, is ARMBAND). A two- or three-letter word must read as it is: ON BACK
    needs its "ON", and the screen's own BACK button stays no BACKPACK.
  - **No window from a misread heading**: a word level with the loadout's headings, left of 0.4 of the width, is a
    window's title only after its bar's magnifier, whatever it reads (the Keys window dragged over the loadout has its bar
    level with ARMBAND, magnifier and all). Left of the stash pane, a title in capitals needs its bar's glyphs read (the
    magnifier, or sort and close), or a two-pixel lattice under it that is no region's grid. A title in small letters still
    counts by its size there, as any title does right of the pane: the decision asked for the glyphs everywhere left of
    the pane, but the Zhuk window over the middle column on the GEAR pictures of 2026-10-19, its glyphs unread, would
    have been lost, and the game's headings are capitals where its containers' names mostly aren't (step 1's
    narrowing; the owner's to overrule).
  - **A lattice under a middle heading is that region's grid**, never a window of no name (the POUCH's grid, its lines
    two pixels wide as a window's, read as one on three of the pictures): the frames across it (one across the POUCH's
    slot and two columns of its grid) are dropped. A container's slot no frame shows is boxed from its heading's place
    (5 px left of the word, 6 under it at 1440p), as the loadout's are; a part that is that box is no grid.
  - **A grid the column's foot cuts is read as far as it shows**: a compartment framed on its top and both sides down to
    the foot (the column's view ends a quarter of a cell above the QUICK USE bar's top; its word folds to "0u1ck" and was
    looked for as "qu1ck" until now, so the foot was always 88.5 % of the height, 1274 px at 1440p, the bar's word at
    1254) is its whole rows in view, and so is one ending within a quarter of a cell of the foot; a frame whose lines run
    on past its corners is left unread. Either way the region is read in part, and the page keeps the rest (§4, *What it
    mirrors*). Until then such a region was left out, and its slot alone, read on its own, took the page's grid away (the
    backpack ran empty past the foot, the lines between its empty cells too faint to frame anything).
  - **A frame whose heading a window may hide is placed under none**: one with a window over the rows between it and the
    heading above, from the heading's place to the frame, unless a compartment stands right above it (a cell of the
    POUCH's grid below the Keys window, the POUCH's heading under it, was the BACKPACK's grid of one cell).
  - Compartments whose tops (or lefts) stand within a quarter of a cell are on one row of their region (until now within
    4 px: a pocket's frame found 5 px above the others' made the pockets three rows high).
  - In a raid the glance reads as before (a region the foot cuts left out, a lattice under a heading no region's grid):
    the 22 raid screenshots of 2026-10-19 read the same regions and cells.
  Measured with `shturmap-cli desk` on the 13 screenshots and one more of the same evening (ARMBAND half under the Keys
  window), renamed in their order without their time into the git-ignored `tests\fixtures\ocr\desk\session` with the
  regions each should make (`regions-<picture>.txt`) and, for three, every item whose label shows (`truth-<picture>.txt`,
  by the page's regions): regions of their size 50 → 64 of 66, missing 7 → 0, invented 5 → 0 (a window of no name three
  times, "*ACE COVER", "ÄRMBANI"), of another size 9 → 2 (the stash's rows under a page of large boxes, a pocket's frame
  unfound: open); screens all told right; the three pictures' 158 items 123 → 127 named right, 17 → 18 "one of N", 8 → 10
  unread, no cell read 10 → 3 (the backpack's slot and two Junk boxes, their corners under the Keys window), 0 wrong; the
  GEAR truth of 2026-10-19 and the Keys page's unchanged (30 and 61 right, 0 wrong). Over the last six pictures of the
  evening the page in hand ends as the GEAR tab has it: LOADOUT, TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK five by
  six (its grid emptied after the raid), POUCH three by three, STASH and the Keys window; until then a BACKPACK of no
  cell, no POUCH and two invented windows. About 1.3 s a picture, as before.
- **What is kept:** no pixel. The mirror is drawn from catalogue art (tarkov.dev's grid images, fetched at runtime as
  the item icons are, §3) at the positions read, never from the screenshot. A reading keeps the page in hand (each
  cell's item or candidates, position, footprint, count, condition and found-in-raid mark, and a "?" cell's pick) until
  a newer reading changes it (§4, "Screen anatomy", *The stash desk*); never a register of the stash over time (§1).
  Per game mode, in `desk.json` in the data folder (§8, "The desk file").
- **How:** as the stash's: the file opened read-only, shared, whole; nothing written to the screenshots folder; no
  screen captured; a screenshot the player took with their own key press, never one taken for them.
- **The player's say:** the menu screenshots' one tick, its words widened for the desk (2026-10-19, after step B part
  1; the desk's panel had left them out): "Read your stash, gear and tasks from menu screenshots" (the GEAR tab is the
  screen the desk adds; its prices aren't read from the picture, so the words don't name them). Its note adds that the
  desk shows what each item is worth from tarkov.dev's prices, fetched again at most every ten minutes when a screenshot
  is read (§3; once an hour until 2026-10-19), and that item ids, counts and places are kept, nothing of the picture. Unticked, no menu screenshot is
  opened, for the desk either. Its note, the README and PRIVACY.md say what the desk reads, keeps and fetches; help with
  the desk's view, the website when its media are next made.

Nothing else of a picture may be read without the owner deciding it anew: not the raid timer beside the list
(Windows' recognition doesn't read the game's digits, and a reader of Shturmap's own was not built), not the Tasks
screen's other tabs, a trader's tasks or a task's rewards, not the hideout, not another player's inventory in a group
screen.

**It changes nothing outside its own folders, with one exception the player turns on** (owner, 2026-10-04: "a mode
that auto-deletes screenshots after a couple of seconds grace period"). "Delete screenshots after reading" in
settings, off unless ticked: a new screenshot, from a raid or the menus, is deleted once Shturmap is done with it (its
name's position, and the extract list, the stash or the tasks its picture shows) and at least 5 seconds after it was seen
(`ScreenshotCleaner`), for good, not to the Recycle Bin. The 5 seconds leave the game time to finish writing the
file and any other tool the player runs time to read its name; the wait for the reading (at most 2 minutes) keeps a
slow one, such as a stash reading that first fetches the needed items' pictures, from losing its file. Until
2026-10-14 it was "Delete position screenshots": only a screenshot with a position went, 5 seconds after it was
seen whatever its picture's reading was doing, and menu screenshots stayed as "taken for their picture". Since the
stash is read from them they are taken for Shturmap too, and the owner asked for them to go ("otherwise the folder is
littered with screenshots from the menu"). Every new one the game names goes, whether "Read your stash, gear and tasks
from menu screenshots" is ticked or not: the tick says delete screenshots, and one rule is easier to trust than two.
What may go is narrow, and checked when the file is seen and again before it goes (`ScreenshotCleaner.MayDelete`):
only a file the watcher reported as new in this run, directly in the screenshots folder, named as the game names a
screenshot (with a position or without). What was in the folder before Shturmap looked and every other file stay.
Nor is a screenshot that comes back new: a file last written before Shturmap started watching, or before a screenshot of
its name was deleted, is old (`ScreenshotWatcher`; review of 2026-10-09: a Screenshots folder a sync tool or the
Recycle Bin restored had each file it brought back reported as new and, with the setting on, deleted 5 s later). So
restored or copied-in screenshots are neither placed on the map nor deleted. The
cleaner never opens the image: the file is removed by name. One that is still open elsewhere (the game writing it)
can't be removed and is tried again, six times 5 seconds apart, then left with a line in the app log; a file marked
read-only is left. Unticking within the 5 seconds keeps what still waits, and so does closing Shturmap. The folder
is the player's own Documents folder, not the game's install: the game is not touched, asked or told anything, and
"never edits game files" above stands. The setting's note, the README, PRIVACY.md and help say it; the diagnostics
carry the setting's state, for "where did my screenshots go?".

Over the network it downloads public data and art (tarkov.dev, the map artwork), and nothing about the player
leaves the PC unless the player sends a report or allows crash reports (§8, "Reports"). `SafetyTests` fails if the
code names any other address, or if anything but the reporting code uses Sentry. Some addresses come out of the
data instead (maps.json gives each map's artwork and tile paths), so the app's download client itself asks only
`json.tarkov.dev`, `assets.tarkov.dev` and `raw.githubusercontent.com`, over https, and refuses anything else before
a request is sent (`CachedHttp.Hosts`; review of 2026-10-04: a changed maps.json could have pointed every Shturmap at
another host). The links that leave Shturmap come out of the data as well (a quest's wiki page, a map's wiki page):
one is offered only when it is an https address on the Escape from Tarkov wiki (`OutsideLink`); anything else in
that field is no link, since a click would start whatever program Windows has registered for it.

BSG's licence agreement, read literally, covers all companion tools: 4.3.4 (text of 2026-06-15) forbids "outside
software that captures, collects, counts or otherwise 'retrieves' information reproduced or stored by the Game
Launcher Application or the Game", unless BSG permits it "at its own exclusive and absolute discretion"; no tool has
been permitted. The owner accepted that residual risk. Do not widen the boundary: only the owner does, as with the
extract list's corner of a screenshot (above, 2026-10-05), the item grids of a menu screenshot (2026-10-11), the
operational tasks' table (2026-10-19), those grids against the whole catalogue for the stash desk (2026-10-19; its
reading and view built, step B) and the item grids of an in-raid screenshot that shows an inventory screen, for the
in-raid glance (2026-10-19; its reading and its plate built the same day).

What players accept and what they don't (web research of 2026-10-03: a dozen screenshot-position tools, BSG's
pages and licence, about a dozen Reddit threads). BSG documents the coordinates in screenshot names itself (support
article 437, on bug reports) and has never removed them; no BSG statement for or against such tools and no verified
ban were found. Players tolerate a map of their own position, above all in PvE; sharing positions with a squad is
called a "radar", and pressing the screenshot key automatically a "bot". Hence two rules that stay (owner,
2026-10-03):

- **Never share a position.** Shturmap shows the player's position to that player only: no squad map, no syncing
  between players, no live sending of positions anywhere. The app log records no positions (only demo runs do, at
  DEBUG), so a report doesn't carry them either.
  One thing comes close, and PRIVACY.md says it: The Lab, Labyrinth and Icebreaker are drawn from tile renders
  (§3), fetched for the part of the map on screen, and in a raid that part is usually where the player is. So
  tarkov.dev's image service can tell roughly where on such a map the player looks. It gets no position, no
  account and no raid, and a tile stays in the cache for a month, so a part seen once isn't asked for again. The
  ways around it were measured and not taken (owner, 2026-10-04): a whole map at every zoom level is about 290 MB
  a layer for The Lab, and a whole map up to zoom 4 only would turn the picture soft when zooming in ("I would
  like to avoid that").
- **Never take a screenshot for the player.** A position comes only from a screenshot the player took with their
  own key press; no timer, no synthesised key, no "auto" mode (on top of "never sends input" above).

And one for every text about Shturmap (README, website, release notes): say what it reads and what it never does;
never use the vocabulary cheat sellers use for their tools ("radar", "minimap", "GPS", "tracker", "undetected",
"safe", "allowed", "ban-proof"); never suggest BSG approves of it; name the risk plainly (4.3.4, no guarantee
against sanctions); and say that some players see a position map as an unfair advantage in PvP.
Where the risk is named, a text may add that 4.3.4, read literally, speaks against companion tools players have
used for years as well (owner, 2026-10-04: "also state that this would also speak against other popular companion
apps that people use for years"; on the website's "Is this allowed? Can I get banned?"). It names tools with what
each reads, from their own pages: TarkovMonitor (The Hideout, the makers of tarkov.dev; on GitHub since July 2022;
it watches the game's log files and reads positions from screenshots) and RatScanner (since March 2020; it takes a
screenshot to identify an item). Neither claims Battlestate's permission. The sentence is context and never the
last word: the risk line follows it, for them and for Shturmap alike. Check the two tools' pages again before
quoting it anywhere new.

## 3. Copyright and data use

- **Shturmap's own code** is MIT-licensed (`LICENSE`; owner, 2026-10-02, to publish it on GitHub). Everything a
  build redistributes is listed with its licence in `THIRD-PARTY-NOTICES.md`; `eng\notices.ps1`, run by the app
  project before every publish, gathers it, `LICENSE` and every package's own licence files into the build's
  `licenses` folder, which is installed with the app (§8, "Distribution"). The help
  panel's LICENCES link opens it. Update it in the same change as any package. Everything Shturmap uses but doesn't ship (data, artwork, ideas) is credited in the README.
- **Game data** (quests, maps, items, extracts, bosses) comes from tarkov.dev's public JSON service
  (`json.tarkov.dev`), run by The Hideout. Its API page says it is free with no rate limit; there are no written
  terms. Requests say who they are (User-Agent `Shturmap/<version>` with the project's address, the build's own
  version without its commit; `CachedHttp.UserAgent`) and are cached with ETags (1 h, 24 h, 7 days by kind).
  Data is downloaded at runtime into the user's cache for personal use and shown with "data tarkov.dev". It is
  never bundled in the repository or a build.
- **No unnecessary load on tarkov.dev** (owner, 2026-10-04: "Check if we properly cache the maps so we don't put
  unneccessary load on tarkov.dev"; checked the same day against the servers). What Shturmap asks, and when:
  - Only at a start and when the mode changes (the stash desk's prices aside, below), never on a timer while it runs. A
    saved copy younger than its age is used without a request: quests, maps and traders 1 hour; item names, item sources
    and `maps.json` 1 day; a map's SVG 7 days; a map tile 30 days. Portraits and icons are downloaded once and kept.
  - An older copy is asked for with its ETag. All three hosts (`json.tarkov.dev`, `assets.tarkov.dev`,
    `raw.githubusercontent.com`) answer "not modified" to that, which costs a few hundred bytes. A file that did
    change comes compressed: the maps file about 0.8 MB, the quests 0.4 MB, the item sources 1.9 MB (they change
    every few minutes with the flea market's prices, hence once a day). A start a day after the last one is 13
    requests (18 in another game language) and about 3 MB if every file changed; a second start within the hour
    asks nothing.
  - **The stash desk's prices come per reading** (owner, 2026-10-19, from a panel: A1 · B1 · C1 · D2; §4,
    "Screen anatomy", *The stash desk*): a list price to copy needs prices minutes old, and the item sources' may be a
    day old. So, for the desk alone, the prices of the items a screenshot showed were to be asked for when it is read
    (those items, never the catalogue; the exception below took its place), and an item's price history
    (json.tarkov.dev's `/{mode}/prices/{id}`) when its card first shows it, each with its ETag, so an unchanged answer
    costs a few hundred bytes. Still never on a timer: a page left open isn't refreshed, and the card says its prices'
    age ("Prices 4 min old · tarkov.dev"), counted from tarkov.dev's scan of the item, the item file's `updated` (owner,
    2026-10-19: the Dorm 104 key scanned 17:38, the file had at 18:14, the card said "8 min old" at 18:22; until then
    from when the file was had). Everything else keeps the ages above. Its requests and bytes per player are
    counted and said to the owner before it is built (the rule at the end of this list). **The exception: the item
    file, at most every ten minutes while the desk is used** (owner, 2026-10-19: "Accept, once an hour", on the count
    below; built in step B part 1, `GameSession.RefreshDeskPrices`; then, after trying the desk the same day: "Maybe we
    need to fetch the prices more often then?": tarkov.dev scans every 5 minutes and caches 5, so ten minutes). json.tarkov.dev has no price file per item (the GraphQL API that
    answers per item was down all of 2026-10-19), so a desk reading asks for the whole item file again with its ETag
    when the saved copy is over ten minutes old (`GameDataLoader.DeskPricesAge`; an hour until 2026-10-19): at most six
    requests an hour of desk use, a few hundred bytes when unchanged, about 1.4 MB compressed when it changed (it changes
    every few minutes, so in practice about 1.4 MB a reading ten minutes after the last, some 8 MB an hour of steady
    use). Nothing is asked per item for a reading's prices, and no picture but the page's
    cells' grid images, fetched and kept as BRING's are today (*Grid images*, below). **The history is built** (step C,
    2026-10-19; `GameDataLoader.LoadPriceHistoryAsync`, `GameSession.DeskHistoryAsync`): asked for when a card first
    shows the item, one file per item from json.tarkov.dev (`{"data":[{priceMin, price, offerCount, timestamp}, …]}`, a
    point a day, every 2 h over the last 30 days; about 20 to 130 KB, the LEDX's PvE file 85 KB), kept in the download
    cache with its ETag and given again without a request within the hour (`GameDataLoader.DeskHistoryAge`), then asked
    for with its ETag when a card shows the item again; a failure is asked again at a card's showing two minutes later.
    Never on a timer and never in bulk: a page of a hundred items fetches only the cards' that are shown; the made-up
    desk's histories are made up, no request.
  - What the server doesn't have is remembered for a week, the time its own Cache-Control gives its answers
    (`CachedHttp.MissingAge`): a tile or a picture that answered "not found" was asked for again in every session
    before. A refusal (403) is not remembered: it can be a block that passes.
  - While a download fails, each try waits longer than the one before: 2, 4, 8, 16 minutes, then every 30
    (`RetrySchedule`; §8, "Asking again"). With tarkov.dev down, every running Shturmap asked again every 2
    minutes before, 720 times a day; now about 50.
  - The hour for the data stays: it only matters at a start, an unchanged file costs one small answer, and a
    longer age would keep a corrected quest away for longer after a patch. Tiles are fetched by view (§2, "Never
    share a position"); a whole map would be hundreds of megabytes.
  Before anything is added that downloads more or more often (prefetching, a refresh timer, shorter ages, quicker
  retries), count its requests and bytes per player and say them to the owner.
- **Map geometry** comes from tarkov.dev's `maps.json` (MIT, Copyright (c) 2019 Oskar Risberg; the test fixture
  snapshot carries that notice).
- **Map artwork** is the SVG maps by Shebuka and contributors (CC BY-NC-SA 4.0). Their README revokes the licence
  for software "designed to facilitate cheating or gaining an unfair advantage" (radars and ESP overlays, maps
  modified for cheat clients, automation scripts, pixel-bots). Shturmap is none of these: positions come only from
  screenshots the player takes, the game is never touched and nothing is automated (§2). The artwork is downloaded
  at runtime, displayed unmodified except for showing one floor at a time (their README invites that), credited on
  the map with its licence, and never redistributed. Non-commercial only. A published screenshot that shows a map
  shares the artwork: credit it the same way and keep it non-commercial.
- **Maps without SVG artwork** (The Lab, Labyrinth, Icebreaker). tarkov.dev publishes them only as image tiles
  (assets.tarkov.dev; maps.json credits "Tarkov.dev", and TarkovBOT.eu for Icebreaker): top-down renders of the
  game's own level geometry and textures, i.e. Battlestate's art. **They are shown** (owner, 2026-10-03: "Do B and A",
  extending to these renders the terms accepted for trader portraits and item icons below; until then they weren't
  used and the sheet below stood in): fetched from tarkov.dev's image service as a view needs them (the zoom level
  that matches the screen, the base layer and the shown floor's layer, at most four at a time; while one loads the
  nearest coarser tile stands in, stretched), kept in the user's cache (`cache\map-tiles\<map>\<layer>\<z>\<x>_<y>.png`,
  checked again after a month) and decoded in memory (the 192 most recently used, and beyond that
  every tile the view on screen needs: a 4K window takes more than 192, and a tile thrown out and asked for again with
  each frame never finished loading; the review of 2026-10-04), never in the repository, a build
  or the test fixtures, credited on the map ("Map: Tarkov.dev · data tarkov.dev"). Screenshots of Shturmap may show
  them, as with icons. They are placed with tarkov.dev's own transform, through its Leaflet CRS: a map unit is a pixel
  at zoom 0, so the tile (z, x, y) covers [x·T, (x+1)·T] ÷ 2^z map units each way, T being maps.json's tileSize (175
  for The Lab, 256 elsewhere) whatever the image's own size (`TileGrid`; worked out from Leaflet's documented maths,
  no code taken). Checked by eye on 2026-10-03: The Lab's extracts land in their rooms on its levels (the medical
  block's elevator room on Technical, the hangar's floor), its room names on their rooms on both floors, Labyrinth's
  and Icebreaker's loot spots on their decks and rooms. Like SVG artwork the renders recede, the colour treatment
  baked into each tile when it is decoded, so a frame only copies pixels (on the CPU, 16–45 ms a frame for The Lab,
  3–11 ms for Icebreaker and Labyrinth); they are clipped to maps.json's bounds (Labyrinth's render runs on with a
  border line and an opaque background). The Icebreaker render is stretched 1.75× along one axis, so these maps keep
  tarkov.dev's transform (`MapProjection.For`). The svg-maps repository's `Labs.svg` (Shebuka, CC BY-NC-SA 4.0, last
  changed 2023) stays unused: a schematic that isn't to scale, its rooms 13–22 m off when fitted to the level.
- **The sheet** stands in when no tile can be had (offline without saved tiles, or none published), drawn from data
  only: maps.json's bounds (the extent tarkov.dev gives the map, not a traced outline) as a panel with a metric grid
  on whole metres (10 m, every fifth line stronger), tarkov.dev's labels for the shown floor, and everything else as
  on any map (player, trail, objectives, extracts, transits, spawns, floors). No walls or layouts are drawn: none
  exist in the data (owner, 2026-10-02: Shturmap shows only what it has a precise readout for). The sheet says
  "NO ARTWORK FOR THIS MAP · GRID 10 M" in its corner (plus "NO FLOOR DATA" where maps.json has no floors, as for
  Labyrinth: its markers are then all on one plane), the credit line says so, and a notice says it once per map ("No
  map render for The Lab: couldn't download it…"). With tarkov.dev's transform kept, Icebreaker's 10 m squares are
  oblong on its sheet (the lines are still 10 m apart); a map with neither SVG nor tiles would get one scale for both
  axes, so its squares stay square. The credit line names an artist only where their SVG is drawn.
  A sheet that stands in for a render that couldn't be had gives way by itself (the review of 2026-10-04: The Lab
  opened offline stayed a sheet until the app was started again): the tiles the view needs are asked for again
  every 30 s, each tile once a round, whether or not the map is touched meanwhile, and the sheet stays until the
  first tile arrives (`MapTiles`: `Ask`, `RetryAfter`). A tile tarkov.dev doesn't have (404) is not asked for again; one whose
  file isn't an image (a download cut short) counts as one that couldn't be had, and is.
- **Trader portraits and item icons** are Battlestate's art. They are never in the repository, a build or the
  test fixtures. Shturmap fetches each one from tarkov.dev's image service (`assets.tarkov.dev`) the first time it
  is shown, keeps it in the user's cache (`cache\game-art`), and shows a glyph when it can't be had. This is
  display for personal use, the way every Tarkov tool and tarkov.dev itself show them. Screenshots of Shturmap
  on the website and in the README may show them, as the wiki and other community sites do (owner, 2026-10-02,
  accepting that BSG's licence 4.2.2, read literally, doesn't allow it); they are never published on their own.
  **A gun is pictured by its default preset** (owner, 2026-10-19, from a panel of five, 2026-10-19: A): the gun's own
  item is its receiver alone, without stock, grip, magazine or sight, and its icon was barely a weapon ("When a weapon
  is required for a quest … barely recognisable as a weapon using that icon"). Every cell that pictures a gun (BRING's
  rows and cells, the quest card's objective and BRING cells, the item card, the kit cue, a hand-over's cell) shows the
  icon of the gun's default preset instead (tarkov.dev's `properties.defaultPreset`: an item of its own, the gun
  assembled as the traders sell it, its icon at the same `assets.tarkov.dev/<id>-icon.webp`, 64×64, about 1.7 KB: an
  item icon like any other, fetched and kept under the terms above), in the same square cell. Links, the item card
  and every word stay the gun's; only the picture changes (`ItemSources.PictureId`, `Picture.PictureId`). A weapon
  without a preset (knives, grenades, under-barrel launchers: 37 of the 198 weapons the quests' kills named on
  2026-10-19, their icons already whole) keeps its own; a preset's icon that can't be had falls back to the gun's own,
  then the glyph. The presets come with the item sources, so until
  they load (a second or two after the rest, from the cache) a gun shows its own icon. A wide gun letterboxes: a 5×2
  gun is about 31×12 DIP in a 40 DIP cell, 14×6 in an 18 DIP one, a 6×1 rifle a thin line; the panel's E (a wider
  cell with the 512 px picture) is the step if that bothers.
  **Grid images** (tarkov.dev's drawing of an item's inventory cell, `assets.tarkov.dev/<id>-grid-image.webp`, a few
  kilobytes each) are fetched the same way, but only for the items BRING lists when a menu screenshot is read (§2),
  as the reading's check; never the whole catalogue (the spike of 2026-10-11 fetched 3,905 of them once, 42 MB as
  PNG, which is what this rule avoids per player). Kept in `cache\game-art` as `grid-<id>.png`. The stash desk
  (§1, §2; decided 2026-10-19, not built) draws its mirror from them too, for the items a screenshot showed.
- **Not used**: other Battlestate artwork (quest images, game UI art, and map art except tarkov.dev's renders of the
  three maps without SVG artwork, above), text from the EFT wiki or guide
  sites (the quest card links to the wiki page instead), and code from other community tools: TarkovMonitor,
  TarkovTracker and MAYAK (GPL-3.0), Tarkov Pilot (no licence), RatScanner (source-available, based on the Elastic
  License). Facts learned from them (log formats, file paths) are reimplemented. The idea comes from TarkovEyes
  (MelGP, MIT); Shturmap is a from-scratch rebuild and takes no code from it.
- **Name and branding**: no "Tarkov" in the app's or the repository's name, no Battlestate logos, typefaces or UI
  art. Logo lettering is drawn from scratch (Windows fonts may be used on screen, not converted to outlines). The
  README says Shturmap is unofficial and that game content belongs to Battlestate Games.
- **Icons** are the Segoe Fluent Icons font that ships with Windows, used in place, never copied into the repo;
  the only custom glyph is the crosshair (drawn from a path in `Shturmap.Map.Glyphs`).
- **Test fixtures** in the repository are the maps.json snapshot only. **Game logs are not in the repository**, not
  even scrubbed (owner, 2026-10-04: "I don't think logs should be in the repo at all if not needed"; until then
  four scrubbed sessions were committed, and the review of that day found ids the scrubber had missed: profile ids
  were masked only after "profileid", so ids on other lines stayed. They were taken out of the whole history the
  same day). The sessions that a few tests replay stay on the PC that made them, in `tests/fixtures/logs`, which git
  ignores; without them those tests are skipped, and the same logic is tested on log lines written by hand, with
  made-up ids. `tools/make-log-fixtures.ps1` makes the local copies under a made-up name, every time in them moved
  to it (2026-01-01, the day all examples use; owner, 2026-10-09), and still scrubs them: an id stays only where it
  is public game data (a quest or trader id in a message's `templateId`, an item template, a trader as the sender);
  every other id is a placeholder (profiles, messages, events, item instances, the stash, raids, the push channel),
  and tokens, session ids, account ids and addresses are masked. `FixtureScrubTests` check the local copies. Quest
  texts are not committed beyond a sample: tests hold up to about seventy one-line objective texts from tarkov.dev,
  the sample the synopsis rules are built against, never a dump and never a quest's full description (owner,
  2026-10-09). The check over all quests reads the user's local tarkov.dev cache.
  `tests/fixtures/ocr` is ignored by git: it holds the owner's own screenshots (Tasks screens from the removed OCR
  feature, and since 2026-10-05 raid screenshots with and without the extract list, which `ExitListReaderTests` read
  where a PC has them and skip elsewhere; the same reader is tested everywhere on a list the tests draw themselves).
  Never commit or publish it; it was removed from the whole history on 2026-10-02, before going public.

## 4. UX principles

1. **No clicks in a raid.** Shturmap follows the game: the logs decide the view, the map, the mode and the side;
   screenshots move the player. Nothing in a raid requires input. Defaults must be right without configuration.
2. **Glanceable from a second monitor.** High contrast on a dark ground, the important line first and largest (the
   glance's distances), sizes by the type roles ("Design system"). No animation beyond what helps the eye follow a
   change, the raid replay at a raid's end (§1's exception), and the tour (§1's second exception, owner, 2026-10-09:
   outside raids only, and still in the cues' own motion), the QUEST COMPLETE cue (§1's third), the extract list's
   roll call (§1's fourth), the cue of the daily and weekly tasks read (§1's fifth), and the stash desk's three moves
   (owner, 2026-10-19: "it should also be fun to use"; from a panel: A1 · B1 · C1 · D2; "Screen anatomy", *The stash
   desk*). (Until 2026-10-04 this also said "primary text
   ≥ 14 px, numbers in a monospaced face"; the app never kept to either, and the owner dropped the rule: "the 14px rule
   is not necessary".)
3. **One window, three states** (owner, 2026-10-19: "one raid planning view and one stash desk view"; from a panel:
   A1; until then "One window, two states", *Plan* and *Raid*). *Plan* or *Desk* while in the menus, *Raid* while
   loading or in a raid. The switch to *Raid* and back to *Plan* is automatic, from the desk too: a raid loading always
   takes the window, with no click. Out of a raid *Desk* opens by itself when a stash or inventory screenshot is read,
   and PLAN | DESK at the rail's head is the one choice of view, two words, not a tab row ("Screen anatomy", *The stash
   desk*): a primary control across the rail's width, above the MAP list (owner, 2026-10-19: "The Plan/Desk switch is
   way to small, needs much bigger."; until then two 11 px words at the right of MAP). No tabs, no other modes to pick,
   no modal dialogs. Shturmap has one window, never topmost, so it can't cover
   the game (popped-out quest cards, the only other windows, were removed on 2026-10-09). One thing does
   cover the window, and only because the player opened it: the Report dialog (review of 2026-10-04: the
   principle didn't name it). And the tour, which dims the window around the parts it is about (owner, 2026-10-09,
   the panel's T3: "I want to put an exception to the design for the tour"): at a first start and when the player
   opens it from help or What's New, never in a raid, and Esc ends it from its first frame (*The tour*).
4. **Minimal surface.** Every control earns its place. Prefer an automatic behaviour over a button, a sensible
   default over a setting. Messages are one line and dismiss themselves.
5. **Clear requirements.** What a raid needs (keys, items to bring) is shown before the raid, unprompted, and next
   to the objective that needs it during the raid.
6. **Says why.** Every quest state can say where it came from (the game log and when, or implied by a later
   quest). So can an objective shown as done: "Done · ticked by you, 4 Oct" (below, and "Quest cards", *Ticks*).
7. **Never ask.** No confirmations, no prompts. Quest states come from the game's logs alone (owner, 2026-10-01:
   players don't screenshot their Tasks screen, so Tasks-screen reading and the TarkovEyes import were removed).
   The daily and weekly operational tasks, which neither the logs nor the data carry, are read from a screenshot of
   the Tasks screen's OPERATIONAL tab when the player takes one (owner, 2026-10-19; §2): never asked for, and every
   quest's state still comes from the logs alone.
   The exceptions, each named where it is described (review of 2026-10-04: the principle didn't name them): the
   one question after a crash (send a report?), the question before an uninstall the player started, and the
   PMC ⇄ SCAV tag of a raid whose side the logs can't tell, which is a switch the player may use, never a prompt.
   Nothing else asks, and nothing asks during a raid's play.
   **One thing the player may tell Shturmap about progress, and is never asked for: that an objective is done**
   (owner, 2026-10-04: "Tick an objective done by hand makes sense"). The logs say when a quest starts, completes or
   fails, never when one of its objectives is done, so a quest that takes several raids kept leading to places
   already dealt with. A tick on the quest's card takes that objective out ("Quest cards", *Ticks*). The quest's own
   state still comes from the logs alone: a quest with every objective ticked is active until the log says
   otherwise. Editing a quest's state by hand stays declined (owner, 2026-10-04); the code for it is gone, and rows a
   database may still hold from it are ignored like the Tasks-scan and import rows (§8, "Quest progress").
   **And one thing about what they have, never asked for either: NOT ANY MORE on an item's card** (owner, 2026-10-11),
   which retracts whatever Shturmap learned of having the item ("What you have"). Nothing asks whether they have
   something.
8. **Position is occasional.** Players press the screenshot key now and then, not continuously. Everything except
   the "you are here" parts works without a fix. A fix shows its age; the marker stays at full strength and says
   it (see "Map drawing"). No "you may be
   anywhere in here" ring: it was tried and is visual noise. Directions relative to your facing
   ("ahead-left") are shown only for 45 s after a fix; after that they become map directions ("NE", map-up is
   north), which stay true while you move. The map's facing cone shows for the same 45 s, from the same constant
   (`Facing.Fresh`; the review of 2026-10-04: the cone stayed for 60 s, so for a quarter of a minute it still pointed
   "ahead" when the cards no longer said so. The shorter of the two, since the facing is only true for a moment).
   Distances say how old they are. **A new fix moves the view only while
   Follow my position is on** (owner, 2026-10-03: "It should be a toggle in the map view and the camera should
   smooth scroll to the updated player position"). It replaces the rule of 2026-10-01, "a new fix never moves the
   view", made when the study log showed the player zooming back out within seconds of every automatic framing; so
   following is the player's choice, off by default, and it keeps their zoom (see "Map", "Follow my position").
   Every new position pings, three bold sand rings leaving the marker over 2.6 s (two still rings with animation
   effects off). Following, the view glides to it. Not following, the map stays where the player put it; out of
   view, a badge at the edge
   of the map points to it with an arrow and pings, says "YOUR NEW POSITION · PRESS F", and a one-line notice says
   so; the badge stays (quietly) while the position is out of view, and clicking it, F, or the map button shows
   the position at the current zoom. An old screenshot found at start doesn't ping. When the raid ends, the
   player marker and trail go: out of a raid there is no "you". The raid's positions, with their minutes, stay in
   memory for its replay until the next raid loads ("Map drawing", *The raid replay*). So while the game's logs are followed, a position
   the log shows no raid for isn't plotted, and a notice says so ("Got a position, but the game's log shows no
   raid, so it isn't shown."; nothing is said for a screenshot taken just before the raid's end line reached the
   log). Only with no game logs at all, when nothing can say where the game is, does the shown map take a position
   that lies on it (review of 2026-10-04: with the logs followed, a position in the menus used to be plotted on the
   shown map whenever it fell inside its bounds). A position's age is said beside the marker: from
   a minute, the ring turns dashed and a tag gives the age ("1 MIN OLD"; with "OLD" from the first minute since
   2026-10-04, bare minutes on a map read as a time to get somewhere); from 2 minutes the tag ("7 MIN
   OLD") is larger and framed in sand (the study log: about one position per 8 raid minutes, often several minutes
   old when the app was looked at). Until 2026-10-03 the 2-minute case was also said over the map in big gold type
   ("POSITION 7 MIN OLD · PRESS PRTSC OR HOME FOR A NEW ONE", and "NO POSITION YET" a minute into a raid without
   one); the owner had it removed: "Put it next to the marker". No position yet is said by the status bar and the
   raid card.
9. **Say it before it matters.** When a raid starts loading, Shturmap shows what to bring for that map, while there
   is still time to back out of matching. **The kit reminder** (owner, 2026-10-03: the one-line "Loading … · bring:
   …" notice came too late to read and as a list of words; "it could still be a good reminder what to bring … with
   icon previews"; "If the player sees they forgot something they can still cancel loading into the map"):
   - **When:** the scene line names the map 1–2 s after matching starts, before matching completes (2–25 s) and
     before a server raid's match setup (15–71 s); in the study logs (a few dozen loads over seven weeks)
     matching was cancelled 9–27 s after it started, so it is the earliest line naming the map and still in time.
     A group's pick comes earlier still (below). A cancelled load goes back to the menus, which ends it.
   - **What** (`Planning.Kit`): every active quest's BRING row for the map; what it takes to get in or out first
     (the entry item, the exits quests name: a flare, climbing gear, money), then what the picks need, then, with
     picks on this map, the rest under ALSO USEFUL. Without picks there it is all one list. It is a list to check,
     never a claim that something is missing: the marks ("What you have") don't change it.
   - **The cue** (RAID LOADING, GROUP PICKED) pictures it under the map's name, under a small "CHECK YOUR KIT":
     **every item, in rows of eight, up to three rows** (then "+3"), in 52 px cells, with "×3" on a picture where
     several are needed; never scaled, like the cue's text. It is a reminder to take in at a glance, not a list to
     read (owner, 2026-10-04: "when many items are required, those get quickly hidden behind a +x mark while we
     still have plenty of screen space ... this should not be something that requires a lot of 'reading', it
     should be a gentle reminder of 'did I pack everything?' that might trigger a cancel when loading to stock up
     on missing items"; until then six pictures of 34 px in one row). The pictures come in one after another once
     the title stands, 50 ms apart, so the eye is led along them. **With picks, what they need comes first, each
     cell framed in its pick's colour, then a hairline and the rest in the kit's order** (owner, the same day: "it
     should also show color coded the icons first of the quests we highlighted"; read as the items of the picked
     quests, not the quests' own glyphs). **Without picks it is one plain grid** in the kit's order, what gets in
     and out of the map first ("some people are not working with highlighting missions at all, so it has to
     account for this"). A cue that pictures a kit stays 7.5 s where the others stay 5 ("The animation can be a
     bit longer"), and a quarter of a second more for each picture past the first row, up to 11 s. A transit's
     cue has none (the gear is what the raid had).
   - **The raid card** starts with CHECK YOUR KIT, in BRING's rows, while the raid loads; BRING itself steps aside
     until the raid starts, then is back in its place. A Scav loading (known early when a server raid's setup names
     the Scav profile) gets no kit, and the card's note says why: quest objectives don't count for a Scav.
   - The study log notes it (`kit.reminder`: when, map, items, items for picks).

   In a group it comes earlier: when the leader picks a raid (the notification log says so 20–70 s before loading
   starts), Shturmap shows that map with the GROUP PICKED cue and its kit (owner, 2026-10-02). The pick's time of day
   ("CURR" or "PAST", the two raid times 12 h apart) isn't shown: the clock formula checks out against the
   screenshots' raid clocks, but which of the two times each name means couldn't be checked without a group raid
   with a screenshot; the study log now records both to settle it. What a quest needs is also beside its name wherever
   it is listed for a map (Plan cards, the raid card): up to three tiny inventory cells with the items' icons,
   "+N" for more (owner, 2026-10-01: BRING alone didn't show which item is for which quest), and "×3" right after a
   cell where the quest needs several (owner, 2026-10-05: "If multiple of the same, it should say so with a number";
   beside, since a figure inside an 18 px cell would hide the item). Each quest's cell says its own number, a map's
   row of cells the whole row's (`Requirement.CountFor`); a quest that needs
   nothing shows no cell (owner, 2026-10-04: the empty, dashed cell of 2026-10-01 stood on most rows and said no
   more than its absence; the Plan cards' rank numbers "1", "2" went the same day, the order says it). ANY MAP rows show no cells:
   bringing doesn't apply there.
10. **Two clicks, two meanings.** A click on a quest keeps its card open. Its **pen** (on the row while the quest is
    pointed at or picked, and always on its card) picks it for the coming raid: lit on the
    map and first in the rail, several at once ("Picks"; owner, 2026-10-01: one click doing both was misleading; the
    study log had the player toggling quests on and off and losing held cards on the way to the map).
11. **Show when the view changes by itself.** When Shturmap changes its view without being asked (a raid
    loading, a transit, a Scav raid starting, the raid over, loading cancelled, the group picking a raid, an extract
    list read), a cue holds the middle of the map for 5 s (owner, 2026-10-01; first 2.8 s, then longer, sharper and
    with more pop). Its entrance plays at 1.8 times its first pace, about 1.6 s (owner, 2026-10-02: the elements
    should appear more slowly; a briefly tried 8 s RAID LOADING cue was too long): a dark band springs open
    behind the text with a short gold flash, gold rules shoot out from the centre with a slight overshoot, and the
    map's name (or RAID OVER) slides up while it decodes letter by letter like a terminal, undecoded letters
    flickering in gold and settling in ink (each through glyphs no wider than itself, so a title never grows while it
    decodes: QA pass 3, 2026-10-15, the roll call's title, held at its width as it ends, lost its last word at 1600×900),
    with one line under it (what the raid can do, or the next raid suggested); then it fades. Nothing with text in it is ever scaled, so the text stays sharp. It takes no clicks.
    **Its title in two lines at most** (owner, confirmed 2026-10-15 (evening page, E1); QA pass 3, the same day: at
    900×560 "YOUR EXTRACTS THIS RAID · 7 OF 12" took three lines at 48 px, and the cue ran under the preview label and
    the tour's band): where one or two lines hold it in the map's column, it breaks there; where the column is too
    narrow for two at 48 px (the smallest window), it breaks where two lines take the least width, never inside a word,
    and is drawn smaller to the column, never cut, as the roll call's rows are (`MainWindow.FitCueTitle`; at 900×560
    YOUR EXTRACTS THIS RAID stands at about 95 % of its size in English, 98 % in German).
    The map's floor picker steps aside while it is up (QA pass, 2026-10-15: at the smallest window it lay over the end
    of the title; "Screen anatomy", *Plan*).
    RAID OVER of a raid with a replay gives way after its entrance to the replay's band at the map's foot ("Map
    drawing", *The raid replay*).
    **QUEST COMPLETE** (owner, 2026-10-07: "For completed quests, also make a nice animation"): when the log reports a
    quest completed (handed in at its trader; in one week's study log all twenty-odd completions came in the menus,
    most two to four within a minute and a half), the cue says "QUEST COMPLETE · PRAPOR", the quest's name decoding as
    every title does, and what it unlocks ("Unlocks Setup and Shooter Born in Heaven", the quest card's UNLOCKS), under
    a stamp: the done objective's check on the quests' gold, popping in as a gold ring leaves it (no text in it, so
    scaling it blurs nothing). One that comes while the cue is up joins it, "QUESTS COMPLETE", "3 QUESTS", their names
    and what they unlock together, its title decoding and the stamp popping again (`Rules.CompletionWords`). It stays 5 s
    after the last quest joined, 12 s at most, then fades. It replaces the one-line notice "… completed" (a start or a
    failure keeps its notice). One that comes while a replay plays waits for it to end; never for the log read back
    at start. On the map the quest's places ring out ("Map drawing").
    **SEEN IN YOUR STASH** (owner, 2026-10-11: "a little eyecandy animation when the app recognises the player updates
    the stash to reflect the player's action and give proper feedback and look cool visually"; chose the cue and the
    rail's glow together): when a menu screenshot showed items the quests need ("What you have"), the cue pictures
    them under its title as the kit reminder pictures the kit, each with the count seen ("×3"), under the stash glyph
    stamped in as the check is; "FROM YOUR SCREENSHOT" over it, "3 keys your quests need" under it (keys when every
    one is a key). A stash is paged through in a run of screenshots, so a reading that comes while the cue is up joins
    it: its new items come in after the ones there, a newer count replaces the shown one, the line counts them all,
    the stamp pops again, 5 s more, 12 s at most (`MainWindow.StashCue`). Nothing for a reading that showed none:
    silence stays honest. While a replay plays, QUEST COMPLETE is up or the tour shows its own cue, the one-line
    notice says it instead ("Seen in your stash: 3 keys your quests need."): none of those is cut for it. **Since
    2026-10-19 the desk's arrival is the feedback** (owner, after trying the desk: "It now conflicts with the 'now seen
    in stash' animation"; decided the same day): a menu screenshot read opens the desk (§4, *The stash desk*), and the
    needed items it showed are said by that one-line notice, every time; the cue plays only as What's New's example of
    0.5.0's line (`Rules.DeskSwitch.StashSeen`; the developer script's `stash` gives the notice, as a reading does). On
    the rail and the quest card the marks that just arrived glow in by themselves, from the snapshot that brought them: a
    gold copy of the mark's line over the ink one, fading out over 1.2 s so the gold eases to ink, the need cell's bar the
    same, and the row flashes once, faintly (`Controls.Glow`); with animation effects off the mark simply stands.
    **YOUR EXTRACTS THIS RAID**, the extract list's roll call (owner, 2026-10-15, §1's fourth exception; from a panel
    of four, 2026-10-15: A, "Roll call, then fly-out"): when a screenshot taken in the last 30 s showed the game's
    extract list (§2), the cue says "FROM YOUR SCREENSHOT" over "YOUR EXTRACTS THIS RAID · 6 OF 12" (the extracts the
    list names, of the map's for your side, as the rail counts them), its title decoding as every title does, and under
    it **a row for each extract on the list**, rising in one after another, nearest you first: the extract's triangle in
    its kind's colour, its name, and "???" where the game marks it "??:??:??" (a sign, not a word to translate).
    Struck-out extracts get no row, nor do transits. Then each triangle lifts off its row and flies to its place on the
    map, which the view has meanwhile taken in, its row fading as it leaves; the band's dark backing goes with the rows,
    so it covers no landing, and the title stays on its halo until 1.5 s after the last landing ("Map drawing", *The
    player's extracts this raid*, has the timings). Both halves of the ask in one cue: the list in the
    middle, then where each one is, the ones off screen too; and the one change of view is made while the cue says
    why. A list that comes while it is up joins it: its new extracts come in as rows after the others, the title counts
    them all, and they fly after the others, the view framing them all again where one lies outside it. It replaces the
    one-line notice "Your extract list is read" (owner, confirmed 2026-10-15). With animation effects off it stands 5 s
    with all its rows and goes; the view takes the extracts in at once and they are simply lit. In the tour and What's
    New it calls out the example list, with "An example, not your raid." under its title: every cue they show says it
    is an example.
    As built (2026-10-15, `MainWindow.RollCall`): the rows stand in columns of up to four, up to three of them (a map
    lists up to twelve extracts for a side; past that the twelfth place would say "+N"), filled down the first column
    first and evened out (six stand three and three); each row is the extract's triangle as the map draws it at the size
    its flight starts from (0.8 of the map's: R 6 DIP, its 3 DIP dark collar, the kind's colour, the shared kind's split
    line), its name in the cue's 15 px and "???" muted after it, joined to its last word, so it stands after a broken
    name's last line, never inside the name (QA pass 3, 2026-10-15: "Boiler Room Basement ???" over "(Co-op)", German
    "Taxi in der ???" over "Primorsky-Straße"). A name too long for its column breaks between words, never
    inside one, nor at a hyphen in it (QA pass, 2026-10-15: "Primorsky Ave Taxi V-" over "Ex" in the pseudo-language at
    900×560; a word joiner follows each hyphen, Bahnschrift having no non-breaking hyphen; `Rules.WordBreaks`), and
    where the map's column has no room for the rows even so (twelve at the smallest window), they are drawn smaller to
    fit, never cut, as the tour's plates are; they keep clear of the map's buttons.
    **The whole cue in its room** (the coordinator's rule after QA pass 3, 2026-10-15: at 900×560 What's New's preview
    label covered the cue's top rule and eyebrow, and in the tour's YOUR SCREENSHOT KEY its band covered the rows, "An
    example, not your raid." and the rows' hand-off points; `MainWindow.FitRollCue`): eyebrow, title, the example's line
    and the rows stand 8 DIP under the notes at the map's top (What's New's or the tour's preview label, a notice) and 16
    above the tour's band, in the middle of the map above the band and lower only as far as the label needs, as What's
    New's plate does. Where that room is too short, the rows are drawn smaller, down to 60 % of the size the column's
    width leaves them; past that the whole cue is drawn smaller to the room, never cut, as the tour's plates are, and an
    example list names fewer extracts while the cue would stand under 60 %, the farthest of its lit ones going as the
    tour's plate drops them, down to two lit and the "??:??:??" one. So the rows, and the points their icons lift off
    from, are always in view. At 900×560 What's New's cue stands whole under its label, its rows at their size in
    English and at 95 % in German; the tour's has some 143 DIP between its label and its band and is drawn at about 41 %
    in English (the same in German), its example already down to two lit and the "??:??:??" one by the plate's rule.
    The title's halo is
    the dark the map's labels stand on, around its letters only (and the eyebrow's, and the example's line), so an
    icon landing under the title still shows between them; until 2026-10-15's build a halo was a dark box behind the
    title, which hid a landing in the replay's stills. A list that joins the cue holds the band where it stands, so the
    rows still waiting lift off from where they were called; one whose extract lies outside the view the roll call took
    has the view take them all in again, the same way (owner, confirmed 2026-10-15: the panel drew no joining list). The
    one-line notice "Your extract list is read: …" went with the cue (owner, confirmed 2026-10-15, as QUEST COMPLETE's
    and SEEN IN YOUR STASH's did); the rail's line under EXTRACTS AND TRANSITS still says it. Any other cue ends it, as
    it ends theirs; What's New's preview of it ends with the preview, the tour's with the chapter.
    **UPDATE READY** (owner, 2026-10-15: "A Beta-tester did not realize there is an update, which shows me that it is
    currently too subtle. I, however, do not want to have a blocking modal like a pop-up"; from a panel of four,
    2026-10-15: B, "A cue, then the band"; §8, "Updates"): a new version of Shturmap downloaded, the cue says UPDATE
    READY over the version with its release's name ("0.6.1 · OKAMI"), decoding as every title does, and under it, in
    the amber line the first raid's cue has, "RESTART NOW · TOP LEFT · NEVER IN A RAID" (DOWNLOAD AVAILABLE and
    "DOWNLOAD · TOP LEFT" in "Tell me only"). As the download completes in the menus, then **once per raid while it
    waits**, at the first menu after each raid, after RAID OVER and its replay, QUEST COMPLETE and SEEN IN YOUR STASH;
    as it fades the rail's update band glows. Like QUEST COMPLETE and SEEN IN YOUR STASH it moves no view.
    **DAILIES AND WEEKLIES**, the tasks a Tasks screenshot brought (owner, 2026-10-19, after trying the dev build,
    which read their Tasks screenshots: "they should get the same animation as the other quests. Be mindful of
    multiple missions started at the same time, we might want to have them in one animation included"; §1's fifth
    exception; until then, from the panel of four of the same morning, B, glow and notice and no cue: "a screenshot
    of the daily tasks is a routine, and a cue every day would be noise"): when a screenshot of the Tasks screen's
    OPERATIONAL table brings a task the set before it didn't have (§2), the cue says "FROM YOUR SCREENSHOT" over the
    count and the kind, "3 DAILIES AND WEEKLIES" ("1 DAILY OR WEEKLY": the reading doesn't tell a daily from a weekly;
    German „3 DAILIES UND WEEKLIES“, „1 DAILY ODER WEEKLY“), decoding as every title does, under QUEST COMPLETE's stamp
    with the generic operational glyph (the stopwatch) in place of the check; and under the title **a row for every
    task read**, rising in one after another as the roll call's rows do: the task's framed glyph as on a NEXT RAID row,
    its synopsis in the cue's 15 px, and under it in muted capitals its map or ANY MAP and its time left ("CUSTOMS · 8 H
    LEFT"; the map alone where the countdown wasn't taken; the time as read, and ENDED once the task runs out while the
    cue is up: *Operational tasks*), the one that ends soonest first; six places at most, the last "+N" past that
    (seven tasks: five rows and "+2"). Its rows and its gold rules keep clear of the map's buttons (the rules narrower
    than their 460 DIP where the column is: at 900×560 the bottom rule ran under the "−" button), and **the whole cue
    keeps to its room** as the roll call's (above; QA pass of 2026-10-19, F1 and F2: at 900×560 under a notice its title
    stood whole on two lines while its six rows shrank to about 36 %, unreadable, and without one its bottom rule ran
    through WIKI MAP; `MainWindow.FitTasksCue`): 8 DIP under the notes at the map's top (a notice), 8 above the map's
    foot (WIKI MAP and the credit), in the middle of the map and moved only as far as those need; where that room is
    too short its rows are drawn smaller, down to 60 % of the size the column's width leaves them, and past that the
    whole cue, title included, is drawn smaller to the room, never cut (at 900×560 under the tour's notice the whole cue
    at about 71 %, in English and German alike; without a notice the title at its size and the six rows at about 68 %).
    A notice that comes while it is up (SEEN IN YOUR STASH's) has it fitted to the smaller room; one that goes leaves it
    where it stands, so it doesn't jump while it is read. **One cue for a run of screenshots** (`OperationalReadings`):
    the game shows up to three rows a page and a row's objectives only while it is expanded, so a player takes a few
    screenshots in a row, and the readings of screenshots taken within a minute of the one before are one reading. The
    first of them that brings a new task calls the cue, which lists every task the run read; a later one joins it while
    it is up (its tasks not yet listed come in as rows after the others, a listed one's words become what it last read,
    the title counts them all and decodes again, the stamp pops again) and calls none once it has gone: the table, then
    a row expanded seconds later, is one cue, the objectives arriving without a second. **A reading that brings no new
    task calls none**: the same set again, a row's objectives, a task handed in; only "As of" and the rows it changed
    say it. It stays 5 s after the last reading joined it, 12 s at most, as QUEST COMPLETE does, and moves no view.
    While a replay plays, QUEST COMPLETE or SEEN IN YOUR STASH is up or the tour shows its own cue, the one-line notice
    says it instead ("Operational tasks read: 3."): none of those is cut for it, and SEEN IN YOUR STASH, which steps
    back to its notice while this cue is up, doesn't cut it either. The rows on the rail that the reading brought or
    changed glow in as it shows, as the stash's marks do (`Controls.Glow`: a gold copy of the synopsis fading to ink
    over 1.2 s, the row flashing once): its after-effect (*Operational tasks*, "Screen anatomy"; `MainWindow.TasksCue`).
    Picks the player makes (a Plan card, the map
    list) show no cue, and neither does Follow my position's glide: the player turned it on, and the toggle and the
    ping say it. With animation effects off it shows and goes without motion. Never during the log replay
    at start.
12. **Point, don't navigate.** Pointing at a quest, an item or an extract anywhere (or at a trader in the strip
    under the MAP list: their quests) lights up every other place it appears (rail rows, cards, map markers) and, for a
    quest, shows its card. Details come to the pointer; there are no detail pages to open. Finished quests appear
    nowhere. The pointer says as much as it is on: on one objective of a quest (its line in the raid card, its row on
    the quest's card, one of its places on the map) the quest stays lit and that objective is marked within it; on a
    need cell beside a quest's name, or on the gold line that names one key, it is that item (the review of
    2026-10-04; "Visual language", linked highlight).

### Design system

One set of colours, type, shapes, motion, icons and words for the app, the map, the README, the website and the
release notes (owner, 2026-10-02: "make sure the colours, fonts and general design are consistent in the app, the
readme and the future website"; written down 2026-10-03). The sections below ("Visual language", "Logo", "Map
drawing", §2's wording rules) apply it; when they say a colour or a style, it is one of these.

**Colours.** The table is the source: `Shturmap.Map.Palette` holds it in code, and `DesignTokenTests` fail when
App.xaml, a XAML colour, the map, `brand\build.cs`, the website's CSS or this table disagrees, or a colour appears that
isn't here. A colour may appear at a lower alpha (gold at 18 % behind linked rows, ground at 78–94 % behind overlays);
a new hue may not. Each kind colour has one meaning (owner, 2026-10-03: one symbol, one meaning, for colour too).

| token | hex | role | where (App.xaml · map · website) |
| --- | --- | --- | --- |
| `Ground` | `#0B0C0B` | the page and the map behind everything; dark collars and halos | GroundColor · Background · `--ground` |
| `Rail` | `#101110` | the rail and status bar | RailColor · — · `--rail` |
| `Panel` | `#151614` | cards, flyouts, tooltips, the icon's plate | PanelColor · — · `--panel`, `--grid` |
| `Raised` | `#1E1F1B` | the selected or expanded thing, one step lighter | RaisedColor · — · `--raised` |
| `Line` | `#2A2B27` | hairlines | LineColor · sheet grid · `--line` |
| `LineStrong` | `#45463F` | the stronger hairline of what is selected; frames | LineStrongColor · sheet edge · `--line-strong` |
| `Cell` | `#1A1B18` | the inventory cell behind item icons | CellColor · — · — |
| `Ink` | `#D9D5C4` | text; on the map every label, and the symbols that are neither a quest's nor the player's (spawn rings, padlocks, switches, an item's loose spots) | InkColor · labels, Ink · `--ink` |
| `Muted` | `#8A8778` | secondary text; what happens after the raid or at a trader; done objectives | MutedColor · Muted · `--muted` |
| `Amber` | `#C9AD62` | the one accent: quests, objectives, distances, "on"; on the stash desk, keep for a quest (2026-10-19) | AmberColor, SystemAccentColor · Amber · `--amber` |
| `AmberHover` | `#D6BE7E` | the accent under the pointer | SystemAccentColorLight1 · — · `--amber-hover` |
| `AmberHi` | `#E2CF9C` | focus rings (a `Ground` line inside), link hover on the web | SystemAccentColorLight2, SystemControlFocusVisualPrimaryBrush (QA pass, 2026-10-15; WinUI's white ring until then) · — · `--amber-hi` |
| `AmberDeep` | `#A88F4E` | the accent pressed | SystemAccentColorDark1 · — · — |
| `Green` | `#8DA65E` | PMC extracts, success, healthy inputs | GreenColor · Green · — |
| `Teal` | `#6F9A94` | Scav extracts | TealColor · Teal · `--teal` |
| `Khaki` | `#B7B77A` | extracts for both sides (the split triangle) | KhakiColor · Lime · — |
| `Violet` | `#9C8CC4` | transits | VioletColor · Violet · — |
| `Red` | `#B8604A` | bosses, danger | RedColor · Red · — |
| `Sand` | `#E9E2C8` | the player: the marker, the trail, the ping, the edge badge, and nothing else | SandColor · Player · `--sand` |
| `Kept` | `#3FD2E0` | the dev build's icon plate (until 2026-10-04 also every picked quest) | KeptColor · — · — |
| `Pick1` | `#8DD3C7` | the first pick: its markers, name, badges, row tint and pen | map · code |
| `Pick2` | `#FB8072` | the second pick | map · code |
| `Pick3` | `#80B1D3` | the third pick | map · code |
| `Pick4` | `#B3DE69` | the fourth pick | map · code |
| `Pick5` | `#FFFFB3` | the fifth pick | map · code |
| `Pick6` | `#FCCDE5` | the sixth pick | map · code |
| `Pick7` | `#BEBADA` | the seventh pick | map · code |
| `Pick8` | `#FDB462` | the eighth pick; a ninth takes the first colour again | map · code |
| `LightGround` | `#F1F0EC` | the logo's light background | brand |
| `LightInk` | `#1E1F1B` | the logo's ink on light | brand, README light mode |
| `LightAmber` | `#8C7436` | the logo's amber on light | brand, README light mode |
| `LogoMarks` | `#4A4A41` | the icon plate's corner marks | brand |
| `LogoGrid` | `#171815` | the social preview's grid | brand |
| `DeveloperPlateEdge` | `#1E6F78` | the dev icon plate's edge | brand |
| `DeveloperPlateMarks` | `#2A97A3` | the dev icon plate's corner marks | brand |
| `WebInkSoft` | `#BDB9A8` | long reading on the website (leads, answers) | website `--ink-soft` |
| `WebDim` | `#6E6C60` | parts of a file name Shturmap doesn't use (website) | website `--dim` |
| `WebTealText` | `#8FB8B1` | the facing in a file name, as text (website) | website `--teal-text` |
| `SheetPanel` | `#121311` | the map sheet's panel (maps without artwork) | map |
| `SheetMinor` | `#1C1D1A` | the map sheet's 10 m lines | map |

**Type.** Bahnschrift in the app and on the map (ships with Windows, DIN-like, close to the game's lettering). It
may be shown on screen but not served as a web font (Microsoft's font FAQ), so the website uses DIN-like faces with
the SIL Open Font Licence, self-hosted: Barlow Semi Condensed for what the app sets semi-condensed, Barlow for body
text, IBM Plex Mono for numbers and file names. The README uses GitHub's own type. Roles, app style and size (px) ·
website class:

| role | app | website |
| --- | --- | --- |
| section label: uppercase, spaced, muted | `EyebrowText` 11, semi-condensed semibold, 0.14 em | `.eb` 12, Barlow SC 600, 0.18 em |
| status word | `StatusText` / `StatusBarText` 12, semi-condensed semibold, 0.08 em | the route readout 13, 0.12 em |
| title (map, quest) | `TitleText` 17, semi-condensed semibold | headings, Barlow SC 600: h1 34–56, `.h` 40 (32 on phones), questions 21 |
| body | the implicit TextBlock style, 14 | body 17, line height 1.6 |
| note, secondary | `NoteText` 12.5, muted | `.note` 14, captions 13, muted |
| figure (distances) | `FigureText` 15, semi-condensed semibold, amber; 22 in the glance | IBM Plex Mono: section numbers, legends, code |
| the big cue | 13 / 48 / 15 | — |
| the tour's band | 13 / 30 / 15: eyebrow, title, the chapter's lines | — |

New text uses a role, not a new size; the few in-between sizes in XAML (10, 10.5, 11.5) are badges and hints that
fit a fixed box.

**Shape and spacing.** Square corners everywhere (`ControlCornerRadius` and `OverlayCornerRadius` 0, no
`border-radius` on the web); flat panels; 1 px hairlines in `Line`, the selected one in `LineStrong`; map symbols wear
a dark collar (see "Visual language"). Spacing in the app steps 2–3 px inside a row, 6–10 px between lines, 12–18 px
between groups, 20 px above a section label; the rail is 384 px, the status bar 40 px. The website lays out on a 96 px
grid with a 72 px reference column, rows 80 px apart (52 px on phones), gaps from 8 to 56 px.

**Motion.** Slow enough to read, never for show: the big cue takes 5 s (7.5 s when it pictures a kit, about 17 s
when RAID OVER replays the raid, §1's exception) with its entrance at 1.8 times the original
pace; the linked highlight eases over 0.18 s, waits 0.25 s before following a lost focus, pulses every 1.4 s for one
thing pointed at and breathes over 2 s for a linked set (below); a new
position pings, and with Follow my position on the view glides to it over 2.4 s, eased in and out; a notch of the
wheel zooms over 0.15 s, eased in and out, and notches in a row run on into one zoom ("Map drawing", *The wheel zooms
over 0.15 s*; owner, 2026-10-15); an extract list just
read is called out (§1's fourth exception): its rows rise in 0.1 s apart from 0.5 s, the view takes in its extracts
from 1.2 s over 1.2 s, eased in and out as the tour's moves, and from 1.6 s each triangle flies to its place over 1.5 s,
0.4 s after the one before, on an arc, eased in and out, and rings out as it lands (owner, 2026-10-16: "too fast"; until
then 0.9 s, 0.16 s apart); with six extracts the last lands at 5.1 s, the title goes from 6.6 s and the rings end at
about 7.2 s ("Map drawing", *The player's extracts this raid*); the daily and weekly tasks read (§1's fifth exception)
stand as QUEST COMPLETE does, 5 s after the last reading of the run joined, 12 s at most, their rows rising in as the
roll call's, 0.1 s apart from 0.5 s, each over 0.3 s by 8 DIP, eased out; the raid card's time doesn't move: at a
new minute its figure and rule just change (owner, 2026-10-09: its
figure decoded and a dark notch ran along its rule at each minute until then). With Windows' animation effects off
the app shows and hides without motion, and following jumps to the position, the roll call's view to its extracts,
the wheel's zoom to its notch.
**The stash desk** (owner, 2026-10-19, from a panel: A1 · B1 · C1 · D2; "Screen anatomy", *The stash desk*) moves three
ways and no more: a page just read comes in cell by cell, 50 ms apart, as the kit's pictures do; its totals count up
once; a copied cell's frame flashes gold once and its figure decodes, as a cue's title does. With the effects off the
page stands at once. The first two are built (step B, 2026-10-19, part 2: the view): each cell's opacity from 0 over
0.2 s, eased out, 50 ms after the one before in reading order (a region's cells row by row), only the regions the
reading opened (all on a first reading); the head line's two totals from what it said (nothing on the page's first
showing) over 0.9 s, eased out; and a `--snapshot` run stands the page at once, as the effects off do. The copy's flash
is built too (step C, 2026-10-19; `DeskBoard.Flash`): the clicked cell's frame, a 2 DIP amber border, in over 0.08 s and
out over 0.6 s, eased in; its figure decodes over 0.6 s as a cue's title does; with the effects off, and in a snapshot
run, neither moves, and the notice says it all the same.
**The linked set breathes** (owner, 2026-10-17: the linked highlight "really helps pre-attentively highlight elements …
However, the pre-attentiveness goes away directly after the highlight was done … the highlights are a bit too faint").
While the pointer lights a linked set (a trader's portrait, a boss's name or marker, an item; "Visual language", the
linked highlight), every lit place on the map wears a halo, a soft radial glow in its own colour under its symbol, 2.8
times its radius and so about three times at its widest, and every row the set lights with the full tint its type
glyph; halos and glyphs breathe together, in phase, from one clock (`Shturmap.Map.LinkedBreath`, which the map view
shares with `Controls.Linked`): a 2 s cycle, an eased sine, scale 0.92 → 1.08 and opacity 0.85 → 1.0 and back, from
the moment the set has lit (after the 0.18 s ease-in). For a set the breath takes the rings' place: its places don't
ring, and the thing pointed at itself (the portrait, the name, the cell) doesn't breathe; one thing pointed at (a quest,
an objective, an extract) keeps its rings. While it breathes the rest steps back a little further ("Map drawing",
"Stepping back"). **The breath stops**, settling over 0.18 s to the static look, when a click holds the focus
(`MapScene.Held`: the trader strip's hold, the one hold of the map's focus; a held card keeps none), when the pointer
has been still for 6 s (a move of the pointer starts it again; leaving ends the highlight as before), and with Windows'
animation effects off it never starts (`--snapshot` runs draw as with them off). **The static look**: the rest further
back still ("Stepping back"), every lit symbol with a 1.5 DIP ring of its own colour on its collar, every lit row with
a 3 DIP bar of the tint's gold at its left; a held trader's focus always stands in it, its quests' own rows with the
bar between raids. Why it stops: a held focus persists through a raid, and nothing may breathe in a raid but under the
pointer (the raid card's readout, "a mark that breathes" left out). Chosen from a panel of four: a deeper static
contrast (A), the lit breathe (B, taken), one pulse at entry and then contrast (C), a static mark at the edge (D); A, C
and D were not taken.
The tour (§1's second exception) moves more, in the same vocabulary: the window dims in 0.35 s, its cut-outs glide
from part to part over 0.65 s eased in and out while the map sheet's corner marks close in on them with a small
overshoot, the band's gold rule shoots out and its title decodes over 0.7 s at each chapter, a stage's lines rise in
one after another, and its staged moments are the app's own (a drawn pointer as the website clip's, the screenshot
key pressed, the ping, the pulse, the cue, the extract list's roll call); with the effects off each chapter shows its
end at once. The website follows the reader's "reduce motion": what moves only with the reader's own scrolling stays
(the logo, the route marker, without easing; owner, 2026-10-02), the rest stops.

**Icons.** Segoe Fluent Icons in the app, drawn glyphs on the map (`Glyphs`), one symbol with one meaning everywhere
("Visual language", the list of checked symbols); quest types by glyph, never by colour. Text links that leave
Shturmap end in "↗". The website draws its few symbols itself, in the same colours.

**Logo.** As in "Logo": the full mark from 48 px, the plain Ш from 16 to 40 px; the lockup at least 30 px tall (the
website's phone header; 40 px in the README and the website header); keep a free space of at least half the plate's
height around it; dark and light variants as in the table.

**Words.** Sober and short, units on numbers, no marketing and no jargon (§8, "Rules"); what Shturmap reads and never
does, the risk named plainly, none of the cheat sellers' vocabulary and no hint of Battlestate's approval (§2). The
same in the app, the README, the website and the release notes.
**Explanations in the app are terse** (owner, 2026-10-05: "The explanation text in the app and in the settings are
way too long. Make them more concise, not exactly caveman style but in that direction"). Help, settings' notes,
tooltips and the legend say what a thing is or does in as few words as stay clear: fragments are fine ("Crashes stay
on this PC"), one point per sentence, no "Shturmap does X so that Y" when X is enough, nothing the control or the
swatch beside it already shows. What a text must say (§2: what is read, kept or sent; what a setting deletes; the
legend's shape and colour, "Legend") stays, in fewer words; the reasons belong here, not on screen.

**Where each surface takes it from.** The app: `App.xaml` (colour resources, brushes and the text styles) and
`Shturmap.Map.Palette` for the map. The website: `assets/site.css`'s `:root` (colours under the tokens' names,
`--f-cond`, `--f-body`, `--f-mono`). The README and the website: `brand/` (the logo files, dark and light). Release
notes: plain Markdown, the same words. A change to the design system changes this section, `Palette`, App.xaml and the
website's CSS together, and `DesignTokenTests` check the colours.

### Visual language

The game's own UI, pared down: what a Tarkov player already reads at a glance, with nothing added for show.
No stock Windows look (no Mica, no rounded Fluent controls, no pills).

- **Shape**: square corners everywhere (`ControlCornerRadius` 0), flat near-black panels, 1 px hairlines; the
  selected/expanded thing is one step lighter with a stronger hairline, like a selected slot in the game.
- **Type**: Bahnschrift (ships with Windows, DIN-like, close to the game's lettering). Labels and status words are
  semi-condensed, semibold, uppercase and spaced (`EyebrowText`, `StatusText`); titles semi-condensed semibold
  (`TitleText`); body text regular; numbers semibold gold (`FigureText`). Styles live in `App.xaml`; cards and
  flyouts set the font themselves (popups don't get the app's implicit text style), and so does the rail, on its
  scroll view: the implicit style doesn't reach text inside list templates either, so the raid card's quest names
  and objective lines, the extract rows and BRING in a raid stood in Segoe UI and white until 2026-10-04, beside
  Plan's card in Bahnschrift.
- **Pictures**: trader portraits square in a thin frame; item icons in a dark inventory cell.
- **Map artwork recedes**: it is drawn at 38 % of its colour and 85 % of its brightness, so its own yellows and
  browns don't compete with the quest amber and Shturmap's markers are found at a glance, while streets and
  buildings still read by brightness. Colours near the amber then lose the rest of their chroma
  (`ArtworkColors`, a small SkSL colour filter: in CIELAB, within 28° of the amber's hue and 14 of its lightness
  the chroma drops to 6, easing out by 55° and 26; cartography review, 2026-10-02): after the plain recede Customs'
  dashed lines were ΔE 3.9 from the amber and 4.1 % of Factory's artwork within ΔE 10; now no pixel of any SVG
  map, floors included, is within ΔE 17 (measured at 2400 px across the sheet). The yellow shapes keep their
  outline and brightness and lose their hue (what they mark in the artwork is unverified). Every map symbol
  carries a dark collar (3 px, ground at 67 %, round at the corners) that gives it an edge on light streets, where
  amber, green and road are nearly as bright (owner, 2026-10-02: unhighlighted quest markers were hard to find
  pre-attentively; the cartography review the same day gave extracts, transits, bosses, snipers and Scav zones the
  same collar, since they measured below 3:1 against mid and light streets). Hollow symbols get a collar band
  under the ring, their middle left open.

Colours and their one meaning each: the token table in "Design system" above (gold for quests, green / teal /
khaki for extracts by side, violet for transits, red for bosses, sand for the player, `Pick1` to `Pick8` for the
picked quests, one each ("Picks", *A colour per pick*), muted for what happens after the raid). Cyan (`Kept`) is
the dev build's icon only: it was every pick's colour until 2026-10-04.
On the stash desk (owner, 2026-10-19, from a panel: B1 · C1 · D2; "Screen anatomy", *The stash desk*) amber also says
"keep for a quest" (the keep notch) and stays the figures' gold; ink is a hideout module's need; a price's rise or fall
is never green or red (PMC extracts and success, danger), only an arrow and a sign; and the status bar's gold light
("needs attention") is a warning on the desk's card.

**One symbol, one meaning** (owner, 2026-10-03: the quest card's pin button and the Place quest type were the same
pushpin, so the type beside the trader portrait read as a useless second pin). A symbol stands for one thing
everywhere: rows, cards, map markers, the legend, buttons. The pushpin `E840` is only the Place type (the card's
button that kept it as a window became a box with an arrow out, `E8A7`, that day; it went with popped-out cards on
2026-10-09). Text links that open something outside
Shturmap (LOG FOLDER, PRIVACY, LICENCES, WIKI MAP, a quest card's WIKI PAGE) end in "↗". A check of all symbols the
same day, by rendering them side by side:
- **A framed glyph is an operational task** (owner, 2026-10-19, from a panel of four: B): on a NEXT RAID row a quest
  type's glyph in a 1 DIP frame (`LineStrong`) is a daily or weekly task of the map, after the quests' glyphs and a
  hairline, never a quest. The generic operational glyph, Segoe Fluent's stopwatch `E916`, stands for an operational
  task whose kind isn't known (a kill, a survival and a hand-over take the Elimination, Survive and Find-in-raid
  glyphs), nowhere else ("Screen anatomy", *Operational tasks*).
- **The stash desk's marks** (owner, 2026-10-19, from a panel: B1; "Screen anatomy", *The stash desk*). The **keep
  notch** at a desk cell's upper left is a need to keep the item, in three weights: a filled corner triangle (now), an
  open triangle (next), a corner bracket (later); amber for a quest, the filled triangle in ink for a hideout module;
  nowhere else. **"~"** before a price: an estimate from condition, scaled to the condition a cell prints below full or,
  where it prints none, at full condition (the addendum of 2026-10-19, Da; until then "it assumes full condition"; a
  full print drops it since the owner's test the same day, armour's aside). **"+"** after
  a figure, a new mark (the addendum, C1a): "at least this, more inside", a container's contents or a weapon's mods
  unseen, and a total with such a cell in it. The sign also starts a change ("+8 %") and BRING's "+N", both "more",
  always before a figure there. Not a dashed frame: dashed stays "old" (a dashed ring is an old position, "Map
  drawing"). **↑ ↓**, text arrows, are a price's change, never a floor: the floor's caret stays the stroked chevron in
  its disc. The **"?" plate** (the small dark plate with a gold hairline, "maybe here" on the map and "One of 4 places
  it can be" on a card) widens to "one of N items": a desk cell whose label fits several. The **person** `E77B`, a
  portrait's fallback, is a trader in a desk cell too (the trader pays the cell's price); the **price tag** `E8EC` is
  the flea market there, as on item cards. **The pin's tab** (step C, 2026-10-19; owner: "It is also not possible to
  'pin' items to the side view"): a short amber bar on a desk cell's top edge says its card is pinned in the rail; the
  pin at a desk card's head is the same tab over a cell's outline, amber while pinned, muted while not. Not the pushpin:
  `E840` stays the Place quest type, the reason the quest card's pin went on 2026-10-03. A pinned card is framed in amber,
  1.5 DIP, as a held card is.
- **Two meanings, resolved:** `E719` (a shopping bag) was both the Find-in-raid quest type and the flea market as an
  item source on item cards. The quest type keeps the bag (it is on rows, map markers and the legend); the flea market
  became a price tag, `E8EC` (owner, 2026-10-03, taking the recommendation).
- **Close but distinct:** `E707` (a pin standing on a surface) marks loose spawns as an item source; it is not the
  pushpin. `E81D` (a disc in a ring, "Show my position") matches the player's own map symbol, the same meaning.
  Follow my position (2026-10-03) is another action, so it has its own symbol, `E759` (a dot in a ring between four
  arrows), used nowhere else.
- **One meaning, two symbols:** the Trader quest type is `E716` (two people); a trader portrait that hasn't loaded
  falls back to `E77B` (one person).

Quest **types are shown by glyph, never by colour**; colour stays free for state (open, done, selected).
The type glyph is always the first thing on a quest's row and the only thing inside its map marker. **Trader
portraits** are secondary: small, at the right end of Plan rows, before the quest line of Raid rows, in the card
header and in the trader strip under the MAP list (2026-10-17); never in place of the glyph, never on the map. **Linked highlight**: gold at 18 % behind the rows that show the very thing pointed at (the same quest, item,
objective or way out, here and elsewhere), and at 9 % behind what only belongs to it (the items a pointed-at quest
needs, the quests a pointed-at item is for); an item that merely shares a quest with the item pointed at is not lit
(owner, 2026-10-04, from the review: one tint stood for all three, and pointing at an item lit every other item
of its quests; `LinkStrength`); map markers
not in focus step back by kind (see "Map drawing", "Stepping back"), easing in and out over 0.18 s, and the focused
ones pulse: rings leave the marker and fade, one every 0.7 s (owner, 2026-10-04: "I really like it, but the effect
is too subtle": two rings half the 1.4 s period apart now, each 4.5 px wide at the start and travelling 38 px on a
dark band, where there was one of 2.5 px that travelled 26 px and faded with the square of the way; and the
pointed-at quest's places out of view get the same pulse at their chevron on the edge, drawn half as large
again, since the small chevron went unseen: "if a quest marker is outside of the current viewport, it should
probably indicate that") (motion is noticed before anything else; off, with the
easing, when Windows' animation effects are off, and only while something is in focus). **Rings for one thing, a
breath for a set** (owner, 2026-10-17, from a panel of four: "B"; "Design system", *Motion*): the rings are for one
thing pointed at (a quest, an objective, an extract and its switches); a linked set lit by the pointer (a trader's
portrait, a boss's name or marker, an item: what a row names as several, `Linked.Quests`, or an item;
`LinkFocus.IsSet`) breathes instead: a halo under each of its places and the type glyph of each row it lights swell and
fade together, the rest a little further back; held, with the pointer still 6 s or with the effects off it stands in a
static look, a ring on each lit collar and a bar at each lit row's left (between two rings the lit and the rest had
been the same gold, so a set stopped standing out between them). A pulse, a breath or a ping draws the
map again about 60 times a second and moves nothing, so a frame's layout (which marker, badge and label goes where)
is kept from frame to frame while what it is made from stands: the view, the display's scale, the scene's data and
the minute of the player's age tag (`MapRenderer.LayoutOf`, `MapScene.LayoutVersion`; the review of 2026-10-04: every
frame placed every marker and label anew). The marker under the pointer is looked up in the same layout. Losing the focus waits
0.25 s before the map follows, so moving from one row to the next switches the highlight straight across instead
of making every marker blink. The focus is the pointer's, and it lets go when the pointer can't be said to be there
any more: a row that is rebuilt under it (a pen click, a snapshot that changes what it says; review of 2026-10-09:
until then every snapshot, `RowLists`) lets go as it leaves, and when Shturmap's window isn't the active one any
more the focus is dropped, so the map doesn't draw its pulse behind the
game for a whole raid (2026-10-04); pointing at something lights it again, in an active window or not.
**Inside one another** (the review of 2026-10-04): linked things may lie inside a linked row, and the innermost one
around the pointer is the one pointed at; leaving it gives the pointer back to the row around it, which it never
left (`PointerNest`; before, the highlight knew one element, and leaving an inner one let go of everything). So
the highlight goes **down to the objective**: an objective's line under its quest in the raid card, its row on the
quest's card and the glance's NEXT name their objective. Pointing at one keeps the whole quest lit as before and
marks the objective: its own line takes the tint (in the raid card once more, over its quest's), and on the map
only that objective's places pulse, take the pointed-at name, and get the chevrons; the quest's other
places stay at full strength and hold still (no symbol grows when pointed at since 2026-10-05), and the doors of its keys keep their pointed-at
look without the pulse (`MapScene.FocusObjective`). It works the other way too: pointing at one of an objective's
places on the map lights its line in the raid card and its row on the quest's card. An objective with no place on
the shown map leaves its quest lit and nothing pulsing. Three smaller things link as well: a **need cell** beside
a quest's name is its item (its card, its BRING row, the item's other cells, its locks and loose spots; the tint
lies over the cell), the **gold "Key: …" line** under a raid objective is that key when it names exactly one (with
alternatives or two doors it couldn't say which, and stays part of its objective's line; `Planning.NeedKey`), and
a **quest's glyph on a folded Plan card** is that quest. A card opened from one of these small things opens beside
the row or the Plan card it lies in, at its own height, not over its neighbours. For the cards, an objective's
line is still its quest's block: it opens no card of its own, and a click on it holds the quest's.
**A card is its subject while it is read** (the review of 2026-10-04, E3): the whole body of a quest's card (its
header, the gaps between rows, the headings, the frame's padding) keeps the quest in focus, and the body of an
item's card keeps the item and the quests it is for, so moving from a row onto the card it opened no longer lets go:
the quest's places stay lit, and a key's locks and an item's loose spots stay on the map, also while the pointer is
on the card's "Loose on …" rows. The body isn't tinted for it and opens no card (`Linked.Also`, `Linked.AlsoItem`,
`LinkFocus`); rows on the card are still their own thing, the innermost around the pointer.
**A row that stands for several items is each of them** (E4): "A or B" (either key opens the door), gear worn
together, a weapon class. It pictures the first, and until 2026-10-04 it was linked to the first alone. Now it
lights up when any of them is pointed at, and pointing at it lights every row and cell of each of them
(`Linked.Items`, `Focus.Alternatives`, `LinkStrength`); its card is still the pictured item's.
**A door is its key, and it is for quests** (E5): a quest lit its doors, a door only its key. Pointing at a padlock
on the map puts the quests that need its key on this map in the focus beside the key (`LinkDoor`, from the map's
`QuestKeys` read the other way round), so their rows take the weaker tint, as the quests of any pointed-at item do.
The rail's BRING rows, the need cells and the map follow the same rules: a row of several items lights each one's
locks and loose spots, and an extract row's item picture is that item. **A boss's name is its spawn zones**: in the
raid line and under a map's name in Plan, each fact is a part of its own (`LinePart`, laid out by `WrapRow` so the
line still reads and wraps as one), and a boss's part stands for the groups of its markers on the map shown
(`GameData.BossMobsOn` gives the id those groups are made of). Pointing at "Kaban 75%" lights Kaban's zones, and
pointing at one of them tints the name with the weaker tint. While another map is shown the names are plain text.
**A trader's portrait is their active quests** (owner, 2026-10-17; "Screen anatomy", *The trader strip*): in the strip
under the MAP list, pointing at a portrait puts every active quest of that trader in the focus, as pointing at one of
them does for one: their rows and glyphs take the full tint, the BRING rows that serve them the weaker one, and on the
map their places breathe (a set; until 2026-10-17 they pulsed) while the rest steps back. Pointing at one of those
quests tints its trader's portrait with the weaker tint (`Linked.Quests`, as a boss's name). The portrait opens no card.
A click holds it: then the trader's quests are the map's focus whenever nothing else is pointed at, held still in the
static look, a ring on each collar and a bar at their rows' left (`MapScene.Held`, `Linked.Held`). The portraits on
the quests' rows and cards link nothing, as before.
Markers on another floor than the one shown are drawn at full strength, highlighted
like any other, with a dark disc at their upper right holding a bold up or down caret (owner, 2026-10-01: half
strength read as "unimportant" and hid highlighted markers): a stroked chevron, 2.2 DIP, about 9 × 5 DIP, in a disc
⌀ 14 with a 1.5 rim. Two or more floors away the disc becomes a 22 × 15 plate with the caret and the number of floors
in an 11 px digit, counted in the map's floor list ("⌃ 4": Streets has six levels, so up or down alone didn't say how
far; cartography review, 2026-10-02). **The caret is bold** (a beta tester through the owner, 2026-10-16: "the icon
indicator if a location is above or below the current position is hard to read on smaller second screens"): until
then it was a filled chevron of about 6 × 5 DIP in a disc ⌀ 11 with a 1.2 rim (an 18 × 13 plate, a 9.5 px digit),
and at 100 %, where a DIP is a pixel, so small a triangle ran into a blob whose up and down differed only in which edge
was flat; a stroke keeps its open V at that size. Chosen from a panel of five: the same shape larger (A), the bold
caret (B, taken), B with the floor in words on a lit name (C), offset outlines instead of a badge (D), and a Normal or
Large setting for the symbols' size (E). E was named for later, should testers report other small things; C is a
possible later addition for picks. The disc stands 1 DIP further out each way than the smaller one did, so it covers
no more of its own symbol (`MapRenderer.FloorBadgeCenter`), and the repel counts it 6 DIP wider than its symbol where
the other badges count 3 ("Map drawing"). Names keep 3 DIP off it (`MapRenderer.FloorBadgeClear`); one beside its own
marker's badge moves out along its side by what it lacks rather than giving the side up (QA, 2026-10-16: at 3× on
Interchange the picked "Minute of Fame" started 0 to 1 px from its ⌃2 plate and read "⌃2 Minute of Fame"). A floor
without artwork of its own isn't in the map's floor list (Customs' 4th floor in the oil rig, drawn in the base layer); a
place on it counts by its building's floors in height order (`FloorResolver.Ladder`), so it reads "⌃ 2" from the 2nd
floor (owner, 2026-10-05: the first WI-FI camera of Provide Viewership, 14 m up, said "7 M UP" in the raid card and
showed a down arrow: counted as the ground it is drawn in). A marker's badges wear the marker's own colour: the
floor badge like the count and "OPT" (the review of 2026-10-04: it was the player's sand, which says "you"). Badge places are fixed: the floor at the upper
right, a cluster's count at the lower right, an optional objective's "OPT" at the upper left; beside small symbols
(Scav and sniper zones, bosses) the floor badge moves out so the symbol stays visible. Spawn zone markers carry it
too, by the height of their centroid.
Text says only what the place doesn't: an objective "… on Streets of Tarkov" drops the map's name on that map.

### Logo

The mark is a stencilled Cyrillic **Ш** (Shturman's initial) on a dark plate with quiet map-sheet corner marks; a
chevron cut through the centre stem points the way. The wordmark is SHTUR in ink beside MAP cut out of an amber
stencil tag: the name's two halves, navigator and map (owner, 2026-10-02, after five panels of options). The files
and their generator are in `brand/` (see `brand/README.md`); `brand\build.cs` also makes
`src/Shturmap.App/Assets/Shturmap.ico`, the exe's and the windows' icon.

- **Construction.** These rules keep the mark clear of hate, war and political signs (each option was checked
  against them). The three stems stay joined to the base and end flat at one height, with no bar across the top:
  free bars read as the Three Percenters' "III". The chevron is cut only through the centre stem and points up:
  never on all three stems (the Three Arrows), never down (a V). In the wordmark the M stays whole (a freed middle
  reads as a V), and stencil bridges go only where a closed shape needs one (A, P, R). Two faint echoes are
  accepted knowingly: the cut gives the centre stem an arrow tip (Tyr rune), and a marked centre stem between two
  others recalls the tryzub's layout.
- **Sizes.** 16 to 40 px use the plain Ш fitted to whole pixels (the cut would smudge); 48 px and up the full mark.
- **Lockup.** The gap between the icon's plate and the wordmark is a third of the cap height (owner, 2026-10-02:
  half read too loose); the plate is about 1.36× the cap height; everything is centred on one axis. The README
  shows `brand/logo-dark.svg` or `logo-light.svg` through `<picture>`, 40 px tall.
- **Colours.** The design system's tokens ("Design system"). On light backgrounds ink is `LightInk` `#1E1F1B` and amber `LightAmber` `#8C7436`; the icon keeps its
  dark plate on both.
- The lettering is drawn from scratch as paths, never from font outlines (§3).

### Screen anatomy

- **The window** (owner, 2026-10-04: remember the window's monitor and size). It opens where the player left it: on the
  same monitor, at its bounds, maximised or not (`WindowPlace`; `window.place` in shturmap.db, saved 0.6 s after a move
  or resize has settled and when the window closes; a minimised window keeps what was saved before). At a first start,
  and whenever the saved monitor is no longer connected in the same place, the first start's rule applies: maximised on
  the first monitor that isn't the primary one (the game's), or on the only monitor at 1600×1000. That size, also the
  one a maximised window goes back to, is at the monitor's scale, 40 in from the corner of its work area and no larger
  than it (`WindowPlace.First`; owner, 2026-10-09: on one monitor the size was taken as pixels, 800×500 at 200 % and
  past the edge of a 1366×768 screen). Until 2026-10-04 that rule ran at every start, so a third monitor or the player's
  own size was lost each time. A maximised window sent to another monitor comes back maximised there. The window is
  never smaller than 900×560 (at the monitor's scale): the status bar's lights and three buttons stay in view, and in a
  narrow window the lights' words go first, then its last word, the last fix, trims (see "Status bar"). The minimum is
  for what must stay readable, the status bar and the rail's glance (the raid card's head, NEXT, EXIT); at that size the
  map is for a laptop beside the game, not for reading from a metre away on a second monitor (owner, 2026-10-09).
  At a larger text size the smallest window is taller, so that glance stays in view (owner, 2026-10-19; *Text size*,
  below).
  Snapshot and demo runs and a given size (`--window`) place the window themselves and remember nothing. Runs that only
  take pictures (`--snapshot`, `--demo`: every run of `tools\fake-raid.ps1` and the layout check; a developer script's,
  §8, "Developer aids", *Quiet runs*) open it off every monitor, past the right edge of the right-most one and level
  with its top, at the size it would have on the monitor it opens on (that monitor's work area where it would be
  maximised, the first start's size on one monitor, or `--window`'s), never maximised, shown without activation and
  out of the taskbar and Alt+Tab (`WindowPlace.OffScreen`, `MainWindow.Quiet`; owner, 2026-10-15: the UI is checked
  while the owner plays on the same PC, and such a run opened maximised on the second monitor and took the focus from
  the game). Off every monitor Windows gives the window the scale of the one beside it, so the size is converted to
  that scale: the content is laid out as on the monitor it would open on. Snapshots are rendered from the window's
  XAML and the map's own drawing, not from the screen, so they come out as before (checked 2026-10-15). `--visible`
  opens such a run as before, to watch it. The app log says where it opened ("Window where it was last: 1300×800 at
  …", "Window off every monitor, …: 2560×1392 at …").
  A saved place is checked at its monitor's scale, the smallest window and the inset from the corner too (review of
  2026-10-09: they were taken at 100 %, so at 150 % or 200 % a window under the smallest came back as it was).
- **Text size** (beta feedback via the owner, 2026-10-18: "on some setups the text can be hard to read"; owner,
  2026-10-19: route 1, Windows' own text size honoured everywhere, and the same day "The text-size should still be set
  by an option, not only through the windows settings"). Two sizes multiply. Windows' text size (Settings ›
  Accessibility › Text size, `UISettings.TextScaleFactor`, 100 to 225 %), which WinUI applies to every text of the
  window by itself; and settings' **Text size**, NORMAL · LARGER · LARGEST (×1, ×1.25, ×1.5; `ui.textSize` in
  shturmap.db, NORMAL unless chosen), on top of it. The text is drawn at Windows' size times the step, never larger
  than Windows' largest, 225 % (`Rules.TextSize`: Windows at 125 % and LARGEST make 187.5 %; at 200 %, LARGEST adds
  12.5 %). No setting of Shturmap's own size beyond these three steps, and no other sizes routed by hand: the step
  reaches every text as Windows' setting does, by one pass over the window and its open popups (`Controls.TextScale`)
  that gives each text (TextBlocks and their runs, rich text, text boxes, icon glyphs) the size WinUI's own curve gives
  at the step, smaller text growing more (11 DIP to 16.7 at ×1.5, 44 to 47.9), so the five text styles and the some
  170 sizes written beside them all follow. It runs after a layout that may have brought new text (a row, a card, an
  open flyout), at most every 100 ms, and not at all while the step adds nothing; it knows a text by its size, so a text
  already at its size costs one read (in a fake raid at LARGEST: 30 ms for the pass that sizes the rail's 1,100
  elements, then 115 passes a minute, 0.5 s in all; the app log says the first, each slower one, and the minute's sum). What measures text off the window (the status bar's fit, the trader strip's
  cells, help's columns, the glance, a card's place) measures it at that size. The map, which WinUI doesn't reach,
  takes the whole factor ("Map drawing"). A change, in settings or in Windows, applies at once: the window's text, the
  measured columns, the map and the smallest window follow. What grows is text and icon glyphs; spacing, frames and
  boxes don't (the status bar's 40 px row and its 28 px buttons, the map buttons, the rail's 384 px, the cards'
  widths), and hold the text up to 150 %.
  **The smallest window grows with it**: 900 wide as at 100 %, and 560 + 410 × (factor − 1) high: 663 at 125 %, 765 at
  150 %, 970 at 200 % (`WindowPlace.Smallest`), at the monitor's scale and never more than its work area; a window
  smaller than that grows to it where it stands. The rule is measured: in German, the fake raid's Streets, EXIT's foot
  stood at 497, 584 and 702 DIP in a 900 DIP wide window's content at 100, 125 and 150 %, 24 DIP above the foot of the
  900×560 window at 100 %; 410 keeps those 24 DIP at 150 % and 40 at 125 % (before, at 125 % German the EXIT row was
  below the fold at 900×560). The width stays: the rail's is fixed, and the status bar gives way by its own rules.
  **What gives way at 125 and 150 %** (the exploration of 2026-10-19 found these cut or squeezed; the layout check at
  both, English and German, Plan and a raid, finds nothing else): the glance's labels stand above their names (*Raid*,
  below); the extract list's names and kind lines wrap; the MAP list's name wraps instead of trimming, and its counts
  past 160 DIP; help's quest types take a column as wide as their longest name (`Columns.HelpKinds`, never under the
  96 DIP English had); settings' rows of choices wrap beside their name, and its ticks' names wrap; the map's credit
  wraps before the map buttons; the status bar's raid state trims with "…" as the last resort; Plan's NEXT RAID rows
  wrap a long map name, "STREETS OF / TARKOV" (UI QA pass of 2026-10-19, F3: accepted); and at the smallest window a
  wide map is narrower, for the label room that grows with the names ("Map drawing", *Room for labels on the right*);
  the stash desk's cells, fixed boxes, lay their marks out at the size the text is drawn and never cut a number (UI QA
  pass of 2026-10-19, F6; *The stash desk*, *As built*, *The cell*): at 125 % a 1×1 cell at 40 DIP gives a long
  condition ("166/300") to the card and keeps its arrow, and a figure beside the notch is set smaller; at 150 % a
  four-figure price beside the notch of a 1×1 cell at 40 DIP (the keycard's "352k") and a five-digit count ("50000") move
  to the card too (open: the owner's call whether a cell's price may leave it at 150 %, or the cell grow).
  200 % is accepted as degraded, not laid out for: Plan's rows squeeze the map's name, the clock's MIN LEFT trims, long German words break,
  the roll call's two-line names touch the next, a heading in help and LICENCES in settings run out of their panel;
  nothing crashes. In the raid card's quest lines long German words break at 150 % (open). Developer builds picture all of it without touching
  Windows (§8, *A larger text size in a run*).
- **The map on screen** (owner, 2026-10-06: "remember the last played and selected map and return to that one a) when
  a raid ends and b) remember the last open map when the app closes and re-open it"). The map last on screen is kept
  (`lastMap` in shturmap.db, `GameSession.LastMapSetting`): one picked in the MAP list or Plan's list, a raid's own map
  as it loads, a group's pick. The app opens on it; only at a first start, with none kept, on the best suggestion for
  the next raid (until 2026-10-06 the rule at every start, so the map the player had chosen was lost with each start).
  When a raid ends its own map is on screen, also when another was being looked at during it, and is the map kept. The
  log read back at start leaves the kept map alone: a raid in it, over before the app started, is older than the map
  it was closed on; a raid still running at start shows its own map.
- **Status bar** (top): mode (PvE/PvP/Seasonal), raid state, last fix, as uppercase words; on the right the inputs
  (logs, screenshots, data), each with a small square light that turns gold when something needs attention, and
  three buttons of one size (28 px, 6 px apart): **feedback** (a speech bubble, `E939`: the Report dialog, PROBLEM
  preselected; muted and disabled where the build can't report, its tooltip saying so), **?** (help, F1) and the
  **gear** (settings, `E713`, Ctrl+,). Owner, 2026-10-03: "It's not clear that the settings are with the
  questionmark on the top right. It should probably be a questionmark for the help and then a settings button next
  to it", and "there should be a separate feedback/bugreport button".
  The mode is a plain label from the game's log, with no chooser (owner, 2026-10-03, replacing a PvE/PvP/Seasonal
  dropdown). Evidence: every session in the study's application logs (a few dozen over seven weeks) has `Session
  mode: Pve | Regular | PvpSeason` 8–11 s after the game starts, a switch within a session writes a new line (seen
  twice), and the dropdown was never used (no `mode.pick` in the study log). Its tooltip says where the mode
  comes from, and only what was read: "From the game's log, 21:52" (the line's time, with the day when it isn't
  today); before any line, "The mode you last played; follows the game once it starts" (the saved `mode` setting);
  with no game on this PC, "No game on this PC: the mode you last played". A mode name Shturmap doesn't know (a
  future one) keeps the last known mode, says "The game says 'X', which Shturmap doesn't know yet…" and is logged
  as a WARN. With no game on this PC the label becomes a chooser (PvE / PvP / Seasonal, saved like the logged mode):
  there is no log to say it, and the maps and data to browse differ by mode (owner, 2026-10-03, the no-game
  fallback; see *No game* below). It goes back to the label as soon as the game is found.
  The raid state says only what the log shows (owner, 2026-10-03: "in the menus" while the game wasn't even
  running was misleading): "LOADING CUSTOMS", "IN RAID · CUSTOMS · PMC · 12 MIN", and otherwise "NOT IN A RAID",
  never "in the menus" (`RaidStatus`). The application log has no line that marks the game quitting: the sequence
  "Disposing BEClient … BEClient exit successfully … Dll released" that ends some sessions also comes right after
  startup and between raids, and about half of the sessions in the study logs (a few dozen) simply stop after an
  ordinary line. So the log can't tell the menus from a closed game, and "GAME CLOSED" can't be said truthfully; the
  tooltip says so ("The game's log shows no raid (it can't tell the menus from a closed game)"). No process checks
  (§2).
  **A raid whose end never reached the log** (review of 2026-10-04). For the same reason a raid the game was closed
  or crashed in keeps its start line and never gets its end line: read back at the next start it showed "IN RAID ·
  CUSTOMS · PMC · 1,310 MIN", and the next game start's login line then ended it as a raid of 22 hours. Only two
  things the log does show say such a raid can't still be running: a newer log session than the raid's (the game
  has started again), and the time since it began. So an open raid or load is closed, as "end not in the log", when
  the game has started again, or when it began longer ago than its map's raid length plus 30 minutes (two hours
  where the length isn't known; `UnfinishedRaid`). The margin is generous on purpose, since dropping a live raid
  would be the worse mistake: tarkov.dev's length may be behind a patch, a Scav's or a transit's timer isn't the
  map's, and the end line only comes after the screens that follow a raid. It is looked at after every batch of log
  lines, when the data arrives, and every 30 s while the app runs (a log that has fallen silent sends nothing). The
  state is then "NOT IN A RAID"; the last-raid line says "LAST RAID · CUSTOMS · END NOT IN THE LOG", with no
  length, since nothing says how long it ran; RAID OVER comes without minutes, and not at all while the logs are
  read back at start. A raid within its bound is still shown as a raid: the log shows one, and can't say more.
  Everything in the bar shares one middle line (a player's report, 2026-10-03): the status words use the tight
  line box of their capitals (`StatusBarText`, `TextLineBounds="Tight"`), so centring a word centres its capitals,
  and the lights, the three buttons' frames and their symbols are centred on the same line; vertical padding keeps the
  words with tooltips easy to point at.
  **Words where they apply, and what gives way** (review of 2026-10-04, C4; `StatusBarFit`). While there is no
  position the bar says "NO POSITION YET · PRESS PRTSC" only in a raid, where the key gives one; outside a raid and
  while one loads it says nothing there ("… IN RAID" stood there all the time). When the bar is too narrow for all
  it says (the raid state, the whole last fix, the lights with their words, the three buttons), the lights' words
  go first: the three squares stay, and each says its word and what is behind it in its tooltip. They come back
  once there is 40 px to spare, so a figure more in the last fix doesn't switch them on and off. Only then does the
  last fix trim. Past that, where even the raid state has no room (a large text size in the smallest window), it trims
  with "…" too, the last fix gone before it (2026-10-19: at 200 % it was cut off without one; `FitStatusBar`). At 900 px a raid's bar is the raid state, the last fix (whole in the fake raid's snapshot; a long
  one trims), three squares and the buttons; outside a raid the words fit.
- **Rail** (left, 384 px), content by state:
  - *The MAP list* at the top, in every state, out of a raid under PLAN | DESK (2026-10-19; *The stash desk*, *As
    built*; owner, 2026-10-08: "there is no way of selecting maps at the moment
    where you do not have quests"; from the maps panel, "A"). The list held every map already, but in the usage log
    (a week) all map picks (dozens) went through Plan's rows and none through it, and after a pick of a map without
    quests the rail stayed on the best suggestion, so nothing in it was about the map on screen. Now, open, it says what
    each map holds: the maps with your quests first, in Plan's order, each with its row's counts at the right in amber
    ("Complete 2 · progress 1"), then a hairline and OTHER MAPS (a heading, not a choice), the rest by name. A map's
    variants drawn with the same artwork are one entry, as Plan counts them (Night Factory is Factory, Ground Zero 21+
    is Ground Zero; until then 17 entries, now 13); a variant on screen shows its map's entry. Closed, it shows the map
    on screen with its counts. It is made anew only when what it says changes, and not while it is open
    (`Rules.MapList`, `SessionSnapshot.AllPlans`). Of the panel's other ways, the other maps by name under Plan's rows
    (B), every map as a folded row (C) and every map by name in place of the list (D) were not taken.
  - *The trader strip* (owner, 2026-10-17: "some players want to level a specific trader or want to work towards
    specific unlocks of these traders. Currently there are no good ways in the app to account for that, you can only
    select by map and not by trader. I at the same time want to keep the UI footprint lean"; from a panel of three: B, a
    trader strip; `Rules.TraderFocus`, `MainWindow.Traders`). One row under the MAP list, outside what scrolls, in every
    state: above the update band between raids, and above the raid card's head in a raid, since the head meets its body
    without a seam and nothing may come between them. For each trader with an active quest, in the game's trader order
    (tarkov.dev's list, as Plan's order takes it), the trader's portrait in its thin frame (`Controls.Picture`, 20 DIP)
    and beside it the count of their active quests in the figures' gold (`FigureText`), "4": every quest the log shows
    active for that trader, with work on a map or not. No heading and no words: a portrait's tooltip says them. No
    active quests, no strip.
    - **One row, never two** (owner, 2026-10-17, after trying it: "I like this, but I do not like that the layout has two
      rows where Ref falls exactly on the last line"; until then cells of at least 43 DIP wrapped, a ninth trader to a
      second row; `Rules.TraderStrip`). The cells share the rail's inner width, 351 DIP at every window size: each is
      (351 − 4 × (n − 1)) / n wide, 4 DIP apart, the portrait and its count left in it with 2 DIP around them and 3
      between. They stand at 20 DIP and 15 px while that fits and scale down together with the cell; the portrait stops
      at 16 DIP and the count goes on to 10 px. Where even that doesn't fit, from ten traders, the count stands on the
      portrait as a small badge at its lower right, standing out 4 DIP right and 3 down, in the map's count badges' style
      (a dark plate, a gold edge, a 10 px gold figure; the rail has none of its own), and the cells are portraits only,
      at up to 20 DIP. Room is kept for two digits whatever the counts are, so the layout follows the number of traders
      alone: six stand at full size, eight at 18 DIP and 13.5 px, nine at 16 DIP and 11 px, ten with badges on 20 DIP
      portraits. The game has 16 traders; past that the portraits would go on shrinking rather than wrap.
    - **Pointing** at a portrait lights that trader's quests in the rows and on the map, as a boss's name lights its zones
      (*Visual language*, the linked highlight: a trader's portrait is their active quests); leaving puts it back.
    - **A click holds the focus**, one trader at a time: the portrait is framed in amber (1.5 DIP over its frame, a held
      card's border), and until it is let go the MAP list's counts and NEXT RAID's rows' counts are that trader's quests
      on each map ("STREETS OF TARKOV · Ragman 2", `PlanList.Count`). A row with none of them steps back, its name and
      cells at 30 % and no count: "Ragman 0" on every other row would be words to read for nothing. In the rows, the map
      rows' glyphs, the open card's PICKED, COMPLETE and PROGRESS, ANY MAP and BRING, the other traders' quests and the
      BRING rows that serve none of the trader's quests step back to 30 % (the map's labels' measure in a set's static
      look, "Map drawing", "Stepping back"; 45 % until 2026-10-17, *Motion*, the linked set breathes), never hidden; a
      pick never steps back, as on the map; the trader's own quests' rows carry the static look's 3 DIP gold bar at
      their left (`Linked.Held`). On the map the trader's places are lit and the rest steps back by kind, held still in
      the static look (a ring on each lit collar): only what the pointer is on rings or breathes, and pointing at
      anything goes before the focus while it lasts (`MapScene.Held`); pointing at the held trader's own portrait
      doesn't breathe either. NEXT RAID's rows go by the trader's quests on each map,
      most first, a tie keeping the planner's order, so the maps with none of them end the list, stepped back
      (`PlanList.Ranked`; coordinator, 2026-10-17); let go, the planner's order is back. The MAP list keeps its order,
      and the open card's summary its own counts. A click on another portrait moves the focus there; a click on the held
      one, or Esc with no card open, lets go ("Keyboard"). The tooltip says what a click does: "Ragman · 4 active
      quests · click to hold", held "… · click to let go".
    - **In a raid** (and while one loads) the strip shows nothing unless a focus is held; then only the held portrait, so
      the player sees the focus and can let it go after the raid: no click is needed in a raid. The map lights only that
      trader's places and steps the others back, by the raid's measure; the raid card is unchanged.
    - **For the run**: the focus lives in the session and is never saved. It stays through raids, a new quest list and a
      change of map, until it is let go or the app closes; a trader with no active quest left keeps their portrait, "0",
      until then.
    - Of the panel's other ways, pointing at the portrait on a quest's row (A) and planning by trader in place of by map
      (C) were not taken; the traders' unlocks and rewards were left out.
  - *The update band* (owner, 2026-10-15, from a panel of four: B, "A cue, then the band"; §8, "Updates"): the row
    under the MAP list (and the trader strip), outside what scrolls, holds the band about a new version between raids
    (UPDATE READY, the version with its release's name, one note line, RESTART NOW as a button), and in a raid and while
    one loads the raid card's head (*Raid*, below), so the band hides then. It glows in when it shows, at each return to
    the menus after a raid, and as the UPDATE READY cue fades (principle 11). Until then the update was one quiet line at
    the top of what scrolls, which a beta tester missed.
  - *What's New* (owner, 2026-10-07: "how to inform the user about news and changes in the app ... ideally accompanied
    with screenshots"; from two panels, "C", then "Whats new: A"). A window was ruled out (principle 3), and so were
    screenshots bundled in a build (§3: map artwork, renders, portraits and icons) and pictures from the website (a new
    host, §2). So: a card at the top of Plan's rail, under the MAP picker (and the update band), in the panel's
    colour with a `Line` hairline. NEW IN 0.4.0 · PRAETORIAN in the eyebrow style in amber (the version and its
    release's name, below), × in its corner (`E711`, as on a notice),
    up to five lines per version, each a 56×34 picture and the thing's name over one note line, then "Point at one: the
    map shows it." The picture is the thing's map symbol drawn by `MapLegend`, as help's legend rows are, a small
    drawing of the raid card's clock, the stash grid glyph the marks carry (what you have), a sticky note (`E70B`,
    Segoe's QuickNote) for quest notes (`notes`), or, on a line about the tour (`tour`, `tour:5`), the tour's drawn
    pointer as the tour draws it, white on a dark rim at 1.2 times, in the middle of the picture's place (owner,
    2026-10-15: until then a tour line had none, the one gap in the column; `MainWindow.TourPointerShape`). **Pointing
    at a line previews it** on the map, as resting on a map's row in Plan
    does (0.6 s; the line takes the linked tint): its map with the thing staged, the preview label "PREVIEW · NEW IN
    0.4.0 · YOUR EXTRACTS THIS RAID" with "AN EXAMPLE, NOT YOUR RAID" beside it, or under it where the map leaves too
    little room for both, each whole, a plate then standing as much lower than the map's middle as it takes to keep clear
    of the label (QA pass, 2026-10-15: at 900×560 the stash lines' "AN EXAMPLE, NOT YOUR STASH" ran out of the label's
    frame, the German one by its last word) and above the map's foot, WIKI MAP and the credit; a plate taller than the
    room between them is drawn smaller to fit, never cut, as the tour's plates are, its caption whole under it (QA pass
    3, 2026-10-15: at 900×560 the `notes` example card was cut at its top, portrait, name and pen gone, its caption not
    drawn and its foot under WIKI MAP and the credit; at 900×560 it now stands at about 82 % in English, 74 % in
    German; YOUR EXTRACTS THIS RAID keeps clear of the label too, principle 11), and leaving puts the map back as it
    was: a preview that staged a map of its own gives the player's back made anew, at the view it had; one that showed only a
    cue or a plate over it (`stash`, `stash-card`, `stash-given`, `clock`, `notes`) leaves the map's scene alone, so
    the last position doesn't ping again nor, with Follow my position on, glide (found 2026-10-15, looking into the owner's report
    on the `tour:5` line below; `SceneGate.MakeAnew`). The
    previews (`replay`, `extracts`, `clock`, `joined`, `leaders`): a made-up raid replayed on Customs, an example extract
    list on Customs, called out as a list just read is (YOUR EXTRACTS THIS RAID with its rows, the view taking the
    extracts in, each flown to its place; without a position, in the data's order), on a PMC raid's map, so without the
    Scav extracts (2026-10-15: tarkov.dev's Scav twins of five of
    them, under 5 m away, stood unlit beside the lit and struck-out ones), the raid card's own clock with example
    times on a plate over the dimmed map (`RaidClock`), Gratitude's
    two objectives on one spot on Woods, Streets' Scav Checkpoint where an extract and a transit stand apart; and for
    what you have (0.5.0; the lines first opened the tour at its first chapter, which the owner reported as a bug on
    2026-10-11) `stash`, the SEEN IN YOUR STASH cue with example items (three keys and three items of the data's
    quests, the same for every player), `stash-card` and `stash-given`, an item card on the plate with a made-up
    STASH line of today's date, from a screenshot with NOT ANY MORE or from the game's log; and for quest notes (0.6.0)
    `notes`, a quest card on the plate, held so its field and each note's × show, with two made-up notes written today,
    under A QUEST CARD, WITH EXAMPLE NOTES and beside AN EXAMPLE, NOT YOUR NOTES: Ballet Lover (where the data has none
    by that name, the first quest by name with a place on a map), active whatever the player's is. Nothing of
    the player's is shown or changed, and nothing is bundled: every picture and preview is drawn when it shows, from the
    cache, by the app's own code (`MainWindow.WhatsNew`). **When:** at the first start of a version with lines, outside
    raids; the versions newer than the one last seen (`whatsNew.seen` in shturmap.db), newest first, each under its
    own NEW IN line. A first start (no `help.seen` either) shows none: help opens, and the newest version counts as
    seen. After an update from a version before the card, the newest version only (`Rules.WhatsNew.Due`). Never by
    itself in a snapshot or the demo. **Gone:** with ×, or once the first raid since the update is over; help's WHAT'S
    NEW IN 0.4.0 · PRAETORIAN brings the newest version's card back. The lines are docs/whats-new.md, built into the
    app, with each language's translation beside it (`docs/whats-new.<code>.md`; a version it lacks shows in English,
    `Rules.WhatsNew.InLanguage`): one section
    per version, "- preview · Name · What it is.", at most five (a test checks the file: known previews, short words). A line about the tour (`tour`, or `tour:5` for a
    chapter a release changed) previews nothing; a click on it opens the tour there (owner, 2026-10-09, T13; *The tour*).
    Once it is clicked the tour owns the map: the preview timer stands still until the tour ends, a request the pointer
    made before is dropped, and the line's leaving under the tour's layer asks for nothing (owner, 2026-10-15, on the
    `tour:5` line: "the highlight does weird movements and highlights the wrong things sometimes"; the leaving ended a
    preview 0.3 s later, which put the player's map back under YOUR SCREENSHOT KEY while the chapter went on staging on
    Customs, so its view moved and its cut-outs sat by a map no longer shown; `Rules.PreviewOwner`).
    0.4.0's card names the tour in place of "Symbols stand apart", which was the least a player acts on.
    **Only what is new for the player** (owner, 2026-10-19: "Don't add 'new in' entries for stuff that is not really
    relevant to the player, only add really new things, like the desk."): a line names a thing the player can now do
    or see; a fix or a refinement goes in the release notes alone, which list everything. 0.7.0's card took out "Guns,
    assembled" and "Two on one spot, steady" for the desk.
    **Release names** (owner, 2026-10-09: "I would like to have names major releases. The first one would be
    praetorian"). A release that raises the minor or major number gets a name, the owner's, written after its version
    in docs/whats-new.md (`## 0.4.0 · Praetorian`); a patch release keeps its line's name (`Rules.WhatsNew.NameOf`).
    It shows in the card's heading and help's link (in capitals, after the version), in the version line at the foot of
    settings ("Shturmap 0.4.0+… "Praetorian" · installed"), in the release's title on GitHub (`Shturmap 0.4.0
    "Praetorian" (private testing)`, `eng\publish-release.ps1`) and the release notes' first line. Not in tags, file
    names, the installer or the updater: those stay the version alone. The preview label keeps the version only.
  - *Plan*: last raid in one line, with **REPLAY** at its right while the raid can be replayed (the play symbol `E768`
    and the word, in the status words' style, as CLEAR PICKS stands beside NEXT RAID; owner, 2026-10-07: "Replay link:
    ii"; "Map drawing", *The raid replay*); **Next raid**: every map your quests have work on, ranked by what can be
    done there, as a list of rows (owner, 2026-10-09: all of them; until then the planner's best four, so the list
    stayed short above the open card; the rail scrolls, and the card now starts lower with many maps), then the open
    map's card: its line in words ("Complete 7 quests · progress 2 more") with
    PICKED, the daily and weekly tasks' OPERATIONAL (2026-10-19, *Operational tasks*), COMPLETE, PROGRESS and BRING (keys,
    items to bring). A row of the list is the map's name and its counts
    ("Complete 5 · progress 1"); the open map's row is marked with a gold bar, and with one suggested map that is
    on screen there is no list (review of 2026-10-04, H4: the open card is some 840 px tall, and the other maps'
    folded cards sat under it, below the fold). **The open card is the map on screen's** (owner, 2026-10-08, with the
    MAP list's "A"; `Rules.PlanList.Open`): its row's card; a map none of your quests is on has no row, and its card
    comes after the rows: the map's name, "None of your quests is on Factory" in muted ink, and its raid
    length and bosses (`PlanCard.CardOnly`). Only with no map on screen yet is it the best suggestion's. Until then a
    map that wasn't suggested left the best suggestion's card open and its row marked. The pen and CLEAR PICKS act on
    the open card's map, so on the map on screen. Each quest row in the card is the quest's name, then a quiet line of what it asks on
    that map in a few words, at most two lines and then "…" ("Ballet Lover" over "Find balletmeister's apartment ·
    Survive and extract"; see §5, "Quest synopsis"). The raid card keeps names only: its objective lines already
    say what to do. COMPLETE and PROGRESS are each in effort order, with a thin hairline where a later group starts
    and no headings (§7, "Plan order"); the raid card stays nearest first. Pointing at another map's row previews
    it on the map; clicking it opens its card and shows it; that click is optional.
    Every row carries enough to compare the maps without opening them (the study log: ten card
    clicks in 4.5 minutes to compare maps): one quest-type glyph per quest, all gold, those to complete first, then
    a hairline, then those to progress, and up to five cells of what to bring. **The open map's row has them too**
    (owner, 2026-10-04: "when you click on one of the icons to highlight a quest, this map is selected and all the
    icons disappear that were currently seen before because the map is now the selected one. This is a cognitive
    dissonance"). Until then the open map's row was its name and counts alone, its card below saying the rest: a
    click on a glyph holds its quest's card and, the row being a button, shows its map, and the glyph clicked was
    gone. With every row the same, what was clicked is still there with its card held beside it, the open map can
    be compared with the others glyph for glyph, and no row changes height when the map changes, so nothing moves
    under the pointer.
    **PROGRESS is not less important** (owner, 2026-10-03: muted PROGRESS rows "suggest less importance", while
    these quests are as much this raid's work). Rows in PROGRESS, in ANY MAP and in the raid card's PROGRESS look
    exactly like COMPLETE rows: gold glyph, ink name, trader portrait and bring cells at full strength. The section
    heading says which, and a PROGRESS row in Plan adds one short line in small muted capitals saying why it only
    progresses ("2 OF 5 OBJECTIVES HERE"; §7, "Raid planner"). Muted keeps one meaning in quest rows: an objective
    done after the raid, at a trader. Resting on a map's row for 0.6 s **previews** its map with its quests on it, labelled
    "PREVIEW · CUSTOMS · CLICK ITS ROW TO PLAN IT"; moving to the next row switches at once, leaving puts the
    shown map back exactly as it was (pan and zoom; fitted where it wasn't drawn yet, not at the camera's default:
    review of 2026-10-09), and a click on the row keeps it. A raid loading ends a preview.
    The picks of the map on screen stay off a preview's map (the same review: each snapshot wrote them into it, and
    into the tour's example), and so does its floor picker, which steps aside until the preview ends (review of
    2026-10-10: a tour chapter staged on Customs showed Streets' floors; approved by the owner the same day). It steps
    aside the same way while a cue (principle 11) or What's New's plate shows, and is back when that goes (QA pass,
    2026-10-15: at 900×560 it lay over the end of SEEN IN YOUR STASH's and RAID LOADING's titles and over the right edge
    of the item card on the plate; `MainWindow.ShowFloors`).
    **Only a row switches the map** (`PlanList`; review of 2026-10-04, "two clicks, two meanings"). The open map's
    card was one button until then: when its map wasn't the one on screen (another picked in the MAP list), a click
    on a quest's row or its pen inside the card also switched the map. The card is no button now; a click in it is
    a click on what is under the pointer. Resting on it still previews its map while another is on screen. So that
    there is always a row to click, the list is also there for one suggested map while another map is on screen.
  - *Operational tasks* (owner, 2026-10-17: "Add dailies and weeklies via screenshot"; from a panel of four,
    2026-10-19: B, "OPERATIONAL rows where the map's work is", which settled three things as drawn: any-map tasks in
    every open card, the planner's order untouched, a reading's feedback by glow and notice, no cue). The daily and
    weekly tasks read from the Tasks screen (§2) show where a map's work already is, never in a block or a card of their
    own (`Rules.OperationalRail`, `MainWindow.Operational`). Of the panel's other ways, one line on the map's card (A),
    a block of its own at the rail's top (C) and tasks counted as work and packed, with a cue (D) were not taken.
    **Changed the same day, after trying the dev build** (owner, 2026-10-19: "I think they should be sorted on top of
    the list always as they are time-sensitive. Also they should get the same animation as the other quests. Be
    mindful of multiple missions started at the same time, we might want to have them in one animation included"):
    the section stands on top of the cards' quest lists, the task that ends soonest first, and a reading that brings
    new tasks calls a cue, one for all of them (principle 11, DAILIES AND WEEKLIES). The planner's order and score
    stay untouched (the panel's B). **Under PICKED, above COMPLETE** (owner, 2026-10-19, later the same day:
    "Ordering - under picked."): in the open card and the raid card the section stands under the picks and above the
    planner's COMPLETE and PROGRESS, no longer above the picks.
    - **A NEXT RAID row** shows its map's tasks as glyphs after its quests' glyphs, behind a hairline, each framed (the
      frame means "operational", *Visual language*), the one that ends soonest first, as its card's rows; never the
      any-map tasks, which would stand on every row. They stay after the quests' glyphs (2026-10-19, with the section
      moved on top): the row compares the maps' quest work glyph for glyph and its order is the planner's, which the
      tasks leave alone, and a 16 DIP glyph says no time; the time-sensitive tasks stand first where their time is
      said, in the card. A map with operational work and none of your quests joins the list's end, by name, without
      counts, and its card says, muted, that none of your quests is on it. The planner's order and score are unchanged
      (§7).
    - **The open card** gets OPERATIONAL under PICKED, above COMPLETE (owner, 2026-10-19: "Ordering - under picked.";
      that morning, after trying the dev build, first, above PICKED: "sorted on top of the list always as they are
      time-sensitive"; until then after PROGRESS, before BRING): the eyebrow, with "As of 14:02" at its right
      in the eyebrow's style (the date too when read on an earlier day); one row per task: its glyph, its synopsis in
      ink, then in small muted capitals its progress and time left ("4 OF 7 · 8 H LEFT"; a weekly "2 D LEFT"; a
      hand-over on any map "ANY MAP · HAND OVER AFTER · 2 D LEFT"; a task whose countdown wasn't taken "READ 3 H AGO").
      **Every time left is rounded down**, in Plan and in a raid alike, and 1 in its last minute: a time never says
      more than there is (QA, 2026-10-19: at 24:59 left Plan said "24 MIN LEFT" and the raid card "ENDS IN 25 MIN").
      The muted line breaks only between its parts (" · "), a part too long for a line between its words.
      The synopsis comes from the objective's words (`OperationalWords`): a kill with its condition kept short ("Kill 3
      Scavs · headshots over 50 m"), "Survive and extract", a hand-over "Find in raid: SSD drive, CPU fan" with item
      cells for the items the catalogue has by exactly that whole name (the words alone otherwise: a quoted name has
      nothing to narrow it, so the stash's rule against the whole catalogue gives way only to a whole name), the name
      matched against the items' English names whatever the app's language (the table is the English game's; QA,
      2026-10-19: a German app's catalogue matched none), the cell and its card in the app's language as any item
      cell's, an item of two words never broken over two lines ("CPU fan"), a line of no known kind as read with the
      generic operational glyph, and a row never expanded its name (its kind, for the
      glyph, from the name's words: "Elimination", "Exit the location", "Find and transfer"). The progress is the one
      counted objective's "4 OF 7", else the game's percentage. The map's tasks come first, then the any-map tasks after
      a hairline, in every map's card (§7's exception), each group **the one that ends soonest first** (owner,
      2026-10-19: "time-sensitive"; a task whose countdown wasn't taken after the others, tasks that end in the same
      minute, as the dailies the game gives together do, in the table's order; `OperationalRail.Soonest`). No
      portrait (the trader isn't read), no pen, no card of its own, no click: pointing at an item cell opens its card,
      as anywhere. With a trader's focus held they step back with
      the other traders' quests.
    - **Ended:** while tasks ran out since the last table screenshot, under the eyebrow, muted: "1 ended at 22:35.
      Screenshot your tasks for the new one." (in every card's section, until the next table screenshot, a day at
      most; the game gives new ones, which only a screenshot shows). The section shows for that line alone too. **The
      cue's rows are a reading, not a clock** (the coordinator's rule after the QA pass of 2026-10-19, F5: a task that
      ran out while the cue was up still said "WOODS · 1 MIN LEFT" while the rail said "1 ended at …"): a row's time
      left stays as read, and a row whose task runs out while the cue is up says ENDED (German ABGELAUFEN) in muted
      capitals in place of it, at that moment ("WOODS · ENDED"; `OperationalCueWords.Line`, `MainWindow.WatchTasksEnds`).
    - **The raid card** has the same section under its picks, above COMPLETE and PROGRESS (owner, 2026-10-19, as the
      open card's; that morning first, before its picks), the one that ends soonest first, for the raid's map's tasks
      and the any-map ones, without "As of": ANYWHERE stands where a quest row has its distance, for an any-map task,
      and its muted line then says no "any map" (one place says it; QA, 2026-10-19); a hand-over says "HAND OVER AFTER
      THE RAID"; the time only when the task ends before the raid could ("ENDS IN 24 MIN", in the red of the clock's
      last ten minutes, rounded down as Plan's; while the raid loads, against the map's whole raid). Nothing new on the
      map: the tasks have no places.
    - **A Scav raid's card** has no OPERATIONAL section: its FIND IN RAID lists the hand-over items the catalogue
      knows by their English name (as the item cells above) after the quests' loot ("for an operational task"), in the
      app's language. That an operational task's kills and survivals count for a PMC only is the panel's assumption (as
      a quest's do), not checked in the game: to confirm.
    - A reading that brings new tasks calls the cue DAILIES AND WEEKLIES, one for a run of screenshots taken within a
      minute of each other, a row for every task read; the rows it brought or changed glow in on the rail; the same set
      again calls none and changes only "As of" (principle 11; owner, 2026-10-19, after trying the dev build: "the same
      animation as the other quests"; until then glow and notice, no cue).
  - *Desk* (owner, 2026-10-19, from a panel: A1; built, step B, 2026-10-19, part 2: the view): out of a raid, with the
    stash desk in the map's place, the rail is the desk's cards under the MAP list and the trader strip, the card of the
    cell pointed at, then the pinned ones newest on top, at the rail's foot while a cell is pointed at (step C,
    2026-10-19; the UI QA passes of 2026-10-19), PLAN | DESK at its head above the MAP list (*The stash desk*, below).
    Plan's rows, its hint and ANY MAP stand aside meanwhile; the update band pinned under MAP stays.
  - *Raid*: **the map's Plan card, live** (owner, 2026-10-01: the raid view must not be a different screen with a
    different logic). THIS RAID holds one card in the expanded Plan card's look: the map's name, the same summary
    line ("Complete 5 quests · progress 1 more"), the bosses with their spawn chances, and, right under the map's name,
    **the raid's time as the card's largest figure** (below; until 2026-10-05 a line of its own, "12 min in · 28 min
    left", in ink and the summary's size)
    (owner, 2026-10-04, in the evening: "In the raid we do not care when the start time was and all this info, we
    typically want to see how much time we're in the raid and how much time is left. This is crucial
    information". It replaces the same day's "40 min raid · started 21:02" with the game's time of day, made when
    the review called "N min left" a figure the game never states. The owner wants it back, so it is back, said
    honestly: the time in the raid counts from the raid's start line in the log, the time left is the map's raid
    length from tarkov.dev minus that, the tooltip says so and that a reconnect can throw it off; past the raid's
    length it says "47 min in · past the raid's 40 min", never a time left below one; a Scav, who joins a raid
    under way, gets "7 min in · time left not known"; a map without a raid length in the data gets the time in
    alone (`Rules.RaidTime`). While the raid loads, "40 min raid" stands with the bosses, as in Plan.)
    **The card's head stays at the top of the rail** (owner, 2026-10-08: "the remaining time is crucial information
    and when scrolling down in the quest window it gets pushed outside of view. It should always stay on top"): THIS
    RAID, the map's name with the side, and the readout stand under the MAP list (and the trader strip's held portrait,
    when a focus is held), outside what scrolls (the row the update band has between raids), and the rest of the card
    scrolls under them. The card is drawn in two parts that meet without a seam, the head open at its foot and
    the body open at its top, so unscrolled it looks as before; once anything has scrolled under it, the head closes at
    its foot with the card's edge. The rail's hint stands above THIS RAID in a raid. A copy
    of the readout pinned only once the card has scrolled away was considered and not built: it would cover the top of
    what scrolls (the ways out ALL 15 ↓ brings up), and the card would show two clocks.
    **The readout** (owner, 2026-10-05: "The remaining time is a crucial piece of information and should be more
    visible. I would make it a prominent item in the UI and maybe even have a cool animation that is in-line with the
    style of the app. It should not be too crazy though to not steer away the attention through its movement";
    `Controls.RaidClock`). The minutes left stand in 44 px, semi-condensed semibold, in ink: the largest type on the
    card, twice the glance's distances. Beside its foot "MIN LEFT", and at the right "27 MIN IN", muted, both in the
    status words' style. Under them a rule of the raid's length, 4 px: the time gone in the hairline's colour, the time
    left in ink, a mark where now is, and a tick every ten minutes (and at both ends), so it reads as a scale like the
    map's. **The last ten minutes are red** (figure, words, the rule's light part), the colour of danger and what the
    game's own timer does (the owner's screenshots: white at 0:33:47, red at 0:08:34). Where no time left is known
    the figure is the time in the raid, said as "MIN IN": a Scav's ("TIME LEFT NOT KNOWN" at the right, no rule), a
    map without a raid length (no rule), and past the raid's length ("PAST THE RAID'S 40 MIN" in red, the rule all
    gone). The tooltip says how the figures are made, as before. **It doesn't move** (principle 2): at a new minute
    the figure and the rule just change, so it never pulls at the eye from the second monitor (a mark that breathes
    all raid was considered and left out for the reason picks hold still). The linked highlight's breath (*Motion*,
    owner, 2026-10-17) keeps to this: it runs on hover only, while the pointer is on a linked set, stops after 6 s of a
    still pointer, and never in a held focus, which lasts through a raid. Until 2026-10-09 the figure decoded at
    each new minute as the big cue's title does and one dark notch ran along what was left of the rule; the owner had
    both removed in the pre-release review. The time left is still the map's raid
    length minus the time since the start line: reading the game's own timer from a screenshot was looked at the
    same day and not built (§2).
    and, when the distances aren't from a fresh screenshot, where they are from. **No loading progress** (owner,
    2026-10-04: "we really don't need the loading progress bar in the upper left, the game already shows a loading
    progress bar"): the line and the segments described next were shown from 2026-10-02 until then, and the
    tracker still keeps the steps for the study log. What was shown: while the raid loads, one quiet
    line under the raid line names the last loading step the game's log reported ("LOADING · MAP", then "MAP
    LOADED", "RAID PREPARED", "RAID CREATED", "PLAYER SPAWNED", "GAME POOLED", "GAME RUNNING"), over six thin
    segments, one per step, each gold only once its own step is in the log (owner, 2026-10-02: loading takes
    60–130 s and showed nothing; and nothing estimated, only what the log says, so a skipped step stays dark and no
    rule creeps by typical times). No time estimate. While it loads, CHECK YOUR KIT follows (the kit reminder, UX
    principle 9), and BRING waits below until the raid starts. Then **the glance**, two rows in
    big type between hairlines: NEXT, the nearest objective with a place (its text, its quest, the distance and
    direction), and EXIT, the nearest extract for your side (and what it takes), never a transit (owner, 2026-10-05:
    "It counts transits as exfils. I would show primarily exfils"; a transit leads to another map, not out; until then
    EXIT was the nearest way out of either kind; `Rules.ExitsNote.Exit`). The game opens only some extracts in each raid,
    by where the player started, and neither its logs nor tarkov.dev's data say which, so at a raid's start the
    nearest is usually not one of the player's (owner, 2026-10-04: say it in the row; a transit is open to everyone and
    gets no note). **Checked or not is said** (owner, 2026-10-05: "Since the app shows the nearest exit upon placing a
    marker it should also be visible if the exfils [are] not verified yet"; `Rules.ExitsNote`). Until a screenshot
    showed the game's own list (§2; *The extract list*, below) the row says "NEAREST · NOT CHECKED AGAINST YOUR
    LIST", muted, its tooltip saying how to check; with the reading unticked, or on a Windows without text
    recognition, the words of 2026-10-04, "NEAREST · CHECK YOUR LIST IN GAME". Once the list was read, EXIT is the
    nearest extract that is on it, never one it leaves out, and says "ON YOUR LIST THIS RAID" in
    the way out's colour, or "ON YOUR LIST · ??? IN GAME" for one the game marks "??:??:??". Under the
    direction, a small gold "ALL 15 ↓" brings the whole list of ways out, which stands under the card, to the top of
    the rail (the review of 2026-10-04, H5: with several quests that list is a long way down, and it is the one to
    read when the nearest exit isn't the player's). **OR** (owner, 2026-10-05: "also show the next one you sure is
    open and where you don't need to bring extra things or do extra things - where you can simply exfil"): under EXIT,
    a smaller row with the nearest extract the list names without "??:??:??" that takes nothing (no item, money,
    flare, climbing gear, switch or second player: an empty `ExtractRules.Needs`), "NOTHING NEEDED · ON YOUR LIST", in
    its kind's colour, with its distance and direction (`Rules.ExitsNote.Plain`). Only once the list was read (nothing
    is sure to be open before), never a transit, and not where EXIT is that one already. Both notes take a second line
    where the direction beside them leaves too little room, with "…" only past that (review of 2026-10-09: a Scav's
    "BEHIND-LEFT" cut EXIT's note off mid-word, without an ellipsis). The labels NEXT, EXIT and OR share one column,
    as wide as the longest of them in the language in use and the 10 px after it, never under 48 px, so the names
    beside them line up (review of 2026-10-10: German "EXTRACT" filled the fixed 48 px and ran into the extract's name;
    `Controls.Columns.Glance`, which the tour's example card takes too). At a larger text size, where that column and
    the figures would leave a name fewer than eight of its own ems (German at 150 %: about 130 DIP, "Markiere den /
    Stryker mit / einem / MS2000 …"), each label stands above its name, which then runs under it as well, the figures
    still at the right, the three rows alike (`Rules.GlanceFold`, `MainWindow.FitGlance`; 2026-10-19; German at 125 %
    takes it too). The glance's rows hide with
    `FallbackValue=Collapsed`: x:Bind didn't call `ShownIfSet` again when its value went to null, and an emptied row
    stood on with what it said before. In a raid the app gets
    glances, median 3.9 s in the study log, and these are what a glance is for. Then PICKED, OPERATIONAL (2026-10-19,
    under the picks, *Operational tasks*), COMPLETE, PROGRESS and BRING
    as in Plan, except that each quest line carries its objectives on this map under it: text, the key or item it
    needs (gold), and on the right the distance, direction and floor hint (the words keep at least half of the line:
    where a hand-over's mark and a long direction would take more, the direction wraps under the distance between its
    parts, the height under the direction; review of 2026-10-10: German "HINTEN LINKS · 5 M TIEFER" beside a hand-over
    left tarkov.dev's sentence a word or two a line; `Controls.ObjectiveLine`); "anywhere" for kills and finds with no
    fixed place, "after the raid" (muted) for a hand-over of its own (one that gives what another line gets is a
    handshake after that line's words, "Quest cards", *Hand-overs*). The text is the objective in a few words, in these lines
    and in NEXT: its phrase from the quest's synopsis ("Mark Stryker" for "Locate and mark the Stryker with an
    MS2000 Marker"; §5, "Quest synopsis"; `Planning.ObjectiveSynopses`), since the card is read in seconds; the
    quest's own card keeps tarkov.dev's sentence, and so do these lines when the data isn't in English (review of
    2026-10-04, C1: three full sentences were the card's longest part). The need stays on every line, also when
    three lines in a row say "Bring: MS2000 Marker": each place needs one of its own (owner, 2026-10-04: "keep the
    item with each sub-item so it's clear that one is needed"; saying it once under the quest's name was tried and
    declined). Objectives inside a quest go nearest first, and quests
    by their nearest objective, so the top of the list is still where to go next. An objective the player ticked
    as done ("Quest cards", *Ticks*) comes after the quest's open lines, muted, with "DONE" where its distance was
    and without its needs; NEXT and the guide line pass it by, and a quest with nothing open on this map isn't
    listed. Below the card, extracts and
    transits for your side, each with what it takes to leave there and, muted, what is so of it (see "Extract
    requirements").
    **The extract list** (owner, 2026-10-05). Under the heading one line in the note style says where things stand:
    "Not checked against your list yet. Screenshot the game's extract list (raid start, or O twice) to check.", then
    "Your list this raid: 6 of 12 extracts (screenshot at 16:40)." (or that this Windows has no text recognition
    language; nothing with the reading
    unticked). Once read, each extract's small line adds "· ON YOUR LIST" or "· ON YOUR LIST · ??? IN GAME" and takes
    the way out's colour; one the list leaves out says "· NOT ON YOUR LIST", its name and distance muted, without
    what it would take, and comes after everything else. The list puts extracts first, nearest first, then the
    transits, then the extracts the list left out (owner, 2026-10-05: "I would show primarily exfils"). On the map, a
    list just read is called out: its extracts in a cue across the map's middle, then each flown to its place, the view
    taking them in (YOUR EXTRACTS THIS RAID, principle 11; "Map drawing", *The player's extracts this raid*). The cue
    says it, and the line under the heading; the one-line notice that said it once ("Your extract list is read: 6
    extracts for this raid") went with the cue (owner, confirmed 2026-10-15, as QUEST COMPLETE's and SEEN IN YOUR
    STASH's went with theirs). What a raid's screenshots name adds up: a later list adds extracts and renews the "???"
    marks of the ones it names, and never takes one away (a reading that missed a row must not close an exit). A
    transit is never said to be on or off the list: it is open to everyone, and its rows aren't matched. **What counts
    as the list**: rows that name two or more of the map's
    extracts for the player's side; one named extract counts only under the header "Find an extraction point", since
    the box the game shows while the player stands in an exit ("Stay in the extraction point") has the same green bar
    and one row, and taking it for the list would call every other exit closed (so in another game language a list
    of one exit isn't taken). Names are matched in the game's language and in English, letter by letter with what a
    reader confuses folded together (the slashed zero comes back as "Ø", 1 as "I"), one letter in five may be off,
    and a row that fits two exits equally names neither (`ExitList.Match`), unless a zero at its end tells them apart:
    the reader leaves out a slashed zero that stands alone, and Factory's "Gate Ø" came back "Gate", as near to Gate 2
    and Gate 3, in each of the owner's Factory screenshots, so an open Gate 0 was struck out (2026-10-15). Every map
    is unchecked again when the raid ends or a transit loads the next map. A way out wears
    the colour of its kind here as on the map, in EXIT's label and distance and in each row's distance: a PMC
    extract green, a Scav's teal, one for both sides khaki, a transit violet (2026-10-04: every way out was green
    in the rail, the PMC extract's colour on the map). ANY MAP closes the rail in both states.
  - *The raid's map is not the map on screen* (review of 2026-10-04: a map picked in the MAP list during a raid
    took the raid with it; the next position, the status bar, the raid card and RAID OVER went to the picked map).
    The raid, its card, the status words, the cues and the last-raid line are about the raid's own map, as the
    game's log names it (the scene, then the location; `SessionSnapshot.RaidMap`). The MAP list stays usable in a
    raid, as a look: the map shows the picked map without a "you", the rail stays the raid's, and the next position
    is plotted on the raid's map and brings it back on screen, as the raid's end does (2026-10-06). A label at the top of the map says so for as long
    as it lasts, in the preview label's style: "LOOKING AT WOODS · THE RAID IS ON CUSTOMS · YOUR NEXT POSITION SHOWS
    IT AGAIN", the note beside the words or, where the map leaves too little room for both, under them, each whole (QA
    pass, 2026-10-15: laid out in one line, it ran out of its frame at 900×560 as the preview label did, the German
    note by 142 DIP). A raid on a map the data doesn't know names no map ("IN RAID · PMC · 12 MIN", "LOADING"; the raid
    card is titled "MAP NOT KNOWN") and plots no position, with a notice saying why: the map on screen is
    never taken for the raid's. A map tarkov.dev gives no scene for (Ground Zero 21+) is named by the match
    setup's or the transit line's location a moment after the scene line, and its RAID LOADING cue comes with that
    line.
  - *Scav raid* (owner, 2026-10-01: a Scav needs a different view): the same card, with SCAV beside the map's
    name (PMC in a PMC raid; nothing when the logs can't tell). Quest objectives only count for the PMC, but items
    found in raid count whoever found them, so the card's summary is "Find items for 4 quests" and its one
    section is FIND IN RAID: the items your active quests need found in raid (find/hand-over objectives marked
    found-in-raid), as BRING rows with icon, count and quests, the ones lying loose on this map first ("Loose
    here · 3 spots"; pointing at a row draws the spots), at most eight and one line for the rest. One note line
    says why. The raid line says "joined 21:02" in place of the raid's length and start (a Scav joins under way; the logs don't
    say how long the raid has run or how long is left). The
    map draws no quest objectives, and in any raid only your side's extracts. ANY MAP is hidden (a Scav's kills
    don't count). When the raid starts as a Scav, the SCAV RAID cue says so (a notice said it a third time, beside
    the cue and the note, until the review of 2026-10-09); a server raid's setup tells it while loading already, and
    then no PMC kit is shown (the kit reminder, UX principle 9). The side is set from the setup then,
    and decided again at the raid start.
    How the side is known: the menu loads the PMC profile; a server-hosted raid's match-setup line names the
    joining profile (same id: PMC, another: Scav); a raid that starts without "GameStarting" is a Scav joining
    under way. PvE raids can be either: server-hosted ones log the match setup like PvP (seen on several days),
    locally hosted ones (seen on some days, on one of them beside server-hosted ones) log neither and start with a
    zero-length "GameStarting". Those stay unknown and get the PMC view; the side tag is then a switch (PMC ⇄
    SCAV) that holds for the raid. No local Scav raid has been seen in a log yet; if one shows a line that tells,
    it replaces the switch.
  - *No game* (owner, 2026-10-03: "Yes, build the fallback 1-5"; the one 30 s notice was easy to miss, and a game
    Shturmap couldn't find left no way to point it there). While discovery finds no game, one line stands where the
    Plan card would be, for as long as that lasts: "NO GAME FOUND ON THIS PC", **CHOOSE GAME FOLDER…**, and "Or
    browse the maps above. Quests and raids follow the game once it's found; Shturmap keeps looking."
    (`GameStateLine`). A game found without log sessions says "THE GAME
    HASN'T RUN ON THIS PC YET", where it was found, and offers the same choice (a wrong install may have been
    found). NEXT RAID, its cards and the "none of your quests" hint step aside: without the game's logs there are
    no quests to plan, and nothing quest-like is shown. The map browses as always (extracts, transits, bosses,
    spawns, landmarks, hazards), and the mode is chosen by hand (status bar, above). No notice says it a second time.
    - **Choose game folder…** opens Windows' folder picker. The folder counts if discovery accepts it: the game's
      build folder with `EscapeFromTarkov.exe` or a `Logs` folder with sessions, or the folder above it (Steam's
      layout). Otherwise a notice says why ("That folder doesn't hold Escape from Tarkov: …") and nothing changes. A
      chosen folder is saved (`installFolder`) and wins over discovery. It is followed at once, without a restart:
      the old logs let go, the new ones read for quest history and followed live, the game's settings read again
      (all of it off the window's thread, as for FIND AUTOMATICALLY: reading every log session takes a while);
      the screenshot folder is the user's Documents one either way (§2's boundary unchanged). A network folder that
      holds the game works the same, but isn't offered as a feature (the laptop beside the game PC is undecided).
    - **It keeps looking.** Discovery runs again every 30 s for as long as Shturmap runs (registry and file checks
      only). While the game or its logs aren't found, a game installed or first started later is followed by itself,
      and a notice says so once ("Found Escape from Tarkov in …: quests and raids follow the game now"). While a
      game found automatically is followed, the same look notices another copy of the game started since (a test
      server, the other launcher's install): discovery's rule is that the newest log session wins, so once another
      install has a newer one, its logs are followed instead, read for quest history, and a notice says what was
      seen, "Newer game logs in …: quests and raids follow that game now" (`GameSession.FollowsInstead`; review of
      2026-10-04, A20: discovery decided once, at the start, and the install it found then was followed for the
      whole run). A followed game is never let go for nothing: a look that finds no game or no logs changes nothing.
      A folder the player chose is never switched away from (below: the hint).
    - **Find automatically** (owner, 2026-10-04, asked whether a folder chosen by mistake leaves no way back: "sounds
      good"). A chosen folder that holds *a* game, just not the one played, wins over discovery for good, and
      CHOOSE… alone can't undo that. Settings' APP AND DATA row says where the folder comes from: "GAME FOLDER: … (chosen
      by you)", "(found automatically)" or "NOT FOUND". While a folder is saved (even one that no longer holds the
      game), **FIND AUTOMATICALLY** stands beside CHOOSE…: it forgets `installFolder` and finds the game again at
      once, followed live as after a choice, and a notice says what it found ("Found Escape from Tarkov in …", "…
      hasn't run on this PC yet", or "No game found on this PC…"). When the chosen folder holds the game but another
      install discovery found has a newer log session, settings say so quietly under the row, "Newer game logs in …
      · FIND AUTOMATICALLY", and so does the LOGS light's tooltip (`GameFolder`). Nothing pops up and nothing switches
      by itself: the choice was the player's. Session dates are read from the logs' folder names, so the hint is a
      fact, not a guess about which install is played; the 30 s look-again keeps it current.
    - Developer switch `--no-game` (developer builds only): discovery looks only at a folder chosen in the session,
      with an app folder of its own so a choice never sticks in real settings; with `--fake-game` the fake game isn't
      found either, until chosen. The developer view's "No game" trigger does the same at runtime and keeps the
      fake game's screenshots, and its script step `choose <folder> | game | auto` chooses a folder, or finds the
      game automatically again (with `--no-game` that is the no-game state).
- **Map** (rest): artwork, quest markers with type glyphs, extracts, transits, spawn zones (see "Map drawing"),
  player, trail, guide line to the nearest place of the picks. Map controls bottom-right (follow my position, show my
  position, show the whole map, zoom in, zoom out), with the floor picker above them on maps with floors;
  one-line notices top-centre; bottom-left a WIKI MAP link (the map's interactive map on the EFT wiki, for loot,
  containers and the rest Shturmap doesn't draw) above the attribution. A map's scene goes into the view once its
  artwork has arrived, and a snapshot is written only into the scene of its own map (`SceneGate`; review of
  2026-10-04: the map counted as shown from the moment it was asked for, so a snapshot that came before the artwork
  put the new map's markers and position onto the old map's picture). Until then the map before stays as it was.
- **Follow my position** (owner, 2026-10-03: "It should be a toggle in the map view and the camera should smooth
  scroll to the updated player position"; it replaces 2026-10-01's "a new fix never moves the view", principle 8):
  a toggle at the top of the map controls, the same 36 px square-cornered frame, its own symbol `E759`, the tooltip
  "Follow my position (Shift+F)"; on, its symbol and frame are amber, the app's colour for "on" (2026-10-04: it was
  the picks' cyan, which then had two meanings; "Design system", one meaning per colour). Off by default, and kept
  between runs (`followPosition` in shturmap.db). It stands still while the tour is up (*The tour*; 2026-10-15).
  - **On**, each new position glides into view over 2.4 s, eased in and out, at the current zoom; the
    floor follows the height as always, and the position pings as it arrives. Turning it on glides to the last
    position at once. **Slow, and with room ahead** (owner, 2026-10-04: "that smooth panning should be way slower.
    Also, it should not dead-center on the player, but rather show more of the map in the area where the player is
    looking. Find a good ratio here"). It was 0.5 s, eased out only, so most of the way was done in the first
    moment and it read as a jump; now it sets off gently, travels and settles (`Camera.PanAt`, `MapView.FollowPan`).
    And the view's middle lies a sixth of the view ahead of the player, the way they face (`FollowState.Lead`), so
    the player stands a third in from the edge behind and two thirds of the view lie ahead: the rule of thirds. A
    quarter was the other candidate; it leaves three quarters ahead but puts the player 25 % from the edge, with
    little to see beside and behind. The lead is taken only while the facing is still shown (45 s, `Facing.Fresh`)
    and the screenshot's name gave one: an older position goes in the middle, since nothing says any more which
    way the player looks. Zooming while on the player keeps this place, and F and the button use it too. With Windows' animation effects off the view jumps there instead. While it glides the edge
    badge waits (the position is on its way into view) and no notice says the position is out of view.
  - **Only the toggle turns it off** (owner, 2026-10-04: "when you have the follow on you should be able to drag and
    zoom. When a position is updated, it should center back to the player but at the current zoom level"; until
    then a drag or showing the whole map switched following off, also for later raids and starts, so it could never
    stay on). Dragging the map or showing the whole map (0, the button) takes the view for now; the next position
    glides it back onto the player, at the zoom the view then has. Zooming (the wheel, double-click, + / −) keeps
    the player in the middle while the view is on the player, and is free, at the cursor, once the view was dragged
    away (`FollowState.Away`). F and the show-my-position button centre at once, as before, and put the view back on
    the player. Pointing at a quest or holding a card never moves the view. While the view is away and the position
    is out of it, the edge badge points to it, as without following.
  - A map opened while following (a raid loading) is fitted first, then centred on the player once there is a
    position. Snapshot and demo runs leave the saved choice alone; `--follow` turns it on from the start (snapshots,
    the dev view).
  - The glide draws through the map's own GPU surface and paint, as every other frame; each frame works out its own
    point on the glide as it is drawn. Only while it glides, a frame timer asks for frames at the system timer's pace
    (about 64 a second): the map's 16 ms animation timer and the Rendering event each gave only about 30 a second
    (measured with the dev view, 2026-10-03).
- **The stash desk** (owner, 2026-10-19: "Stash Desk Panel: A, but keep in mind that the stash also has the player
  loadout in view and when a raid ends the player might want just take a screenshot of the items they have and get an
  overview of what to do with that. Also keep in mind of stackable things, like a bag stashed in a bag that contains
  additional items. A1 · B1 · C1 · D2. Update the Design.md accordingly."; from a panel: A1 · B1 · C1 · D2; built
  (step B, 2026-10-19, part 1: reading and data; the view to come) as *Built: the model the view draws*, below, says).
  §1's third answer: between raids, the items of a screenshot the player took of their own stash or loadout,
  mirrored cell for cell, each with what it is worth, what it is kept for, and a list price one click away; read as §2
  says ("For the stash desk"), its prices fetched as §3 says. The panel's two questions on the cell went unanswered and
  take its drawn defaults: "later" marks a cell only for a found-in-raid need or an item that can't be bought (the card
  lists every need), and the head line counts the keeps. Not taken: the grid in the rail beside the map (A2), a
  full-window desk with a tab row (A3), a plate over the map (A4); a price strip (B2), a heat map (B3); a quote sheet
  without scrolling (C2); a COPY button on the card (D1), MATCH and UNDERCUT buttons on it (D3).
  **The addendum** (owner, 2026-10-19: "Stash desk addendum I go with your recommendation"; from the addendum: A(2) ·
  B1 · C1a · Da; a second panel, on the GEAR tab's five screenshots, its cells 1:1; not built) settles what the first
  left to steps B and C: the regions stacked in reading order (A(2), *What it mirrors*), the head line ON YOU · STASH ·
  KEEP with the fold (B1, *The head line and the fold*), a "+" after the figure while contents are unseen (C1a) and a
  figure scaled to a printed condition (Da; both *The cell*). Not taken: the game's two columns at every size (A(1)),
  one total (B2) or keep counts per part (B3), an unchanged region shown whole or muted, an eye struck through (C1b) or
  a dashed frame (C1c) for contents unseen, the full-condition figure with a plain "~" (Db).
  **Step B's view is built** (2026-10-19, part 2: the view; `Controls.DeskBoard`, `Controls.DeskCellView`,
  `MainWindow.Desk`, `Rules.DeskSwitch`, `Rules.DeskFold`, `Rules.DeskCells`, `Rules.DeskNumbers`, `Rules.DeskPageRules`):
  the switch, the desk in the map's place, the head line, the regions and the fold, the cells with their marks, a
  window's stem and light, the page's entry and the totals' count-up, over the model the session publishes
  (`SessionSnapshot.Desk`; part 1). The card and the click were step C; until then the rail showed the item card the
  pointer opens anywhere. Where the panels left a choice, the rules taken are under *As built*, below.
  **Step C is built** (2026-10-19; the owner, after trying step B: "We should utilize the whole screen, otherwise it will
  be hard to read. Also, there are no sparklines of the price history. The user interface on how to copy a price is not
  clear. Also I'm not clear how to get the info in which quests or hideout projects a item is used. It is also not
  possible to 'pin' items to the side view and there are no tooltip windows (which we might not need, just saying)."; then,
  on the coordinator's COPY button: "I don't think we need a visible copy button, clicking on the item suffices. But there
  should be a visual feedback and UI elements for setting what is copied clearly visible."; `Rules.DeskPageRules.Layout`,
  `Rules.DeskCards`, `Rules.DeskPins`, `Controls.DeskCardView`, `Core.Desk.DeskCopies`, `Core.Desk.DeskHistory`): the cells
  scale with the desk's width (*Its place*), the desk's own card with the price band and the keep lines (*The card*),
  pinning, and the click's copy with its rule on the card (*The click*). No tooltips over the cells, by design.
  - **Its place** (A1). Out of a raid the desk takes the map's place, and the rail becomes its card, where the item
    card's words would be: pointing at a cell puts its card there (below). PLAN | DESK at the rail's head, above the
    MAP list, switches between the two views: two words in one `LineStrong` frame, the one on screen in amber on
    `Raised` (a primary control across the rail since 2026-10-19, *As built*; until then beside the MAP list, 11 px).
    The desk opens by itself when a stash or inventory screenshot is read in the menus, and is left by the
    switch. A raid loading always takes the window back to the raid view, with RAID LOADING's cue and no click
    (principles 1 and 3); the desk keeps its page for next time. The MAP list and the trader strip stay. **The cells fill the desk** (owner, 2026-10-19, after trying step C: "there is a
    lot of wasted screen space … Find the optimal solution"; the coordinator's rule; the rail stays for the cards;
    `Rules.DeskPageRules.Layout`, `Arrange`): the cell is the largest size from 40 to 128 DIP (twice the art's own
    64 px) in steps of 8 (each a whole number of pixels at every Windows scale from 100 to 200 %) at which the page's
    widest row fits the desk's width and its tallest column the desk's height, the page flowed into columns: the gear's
    regions in reading order down a column and on into the next where the desk's height ends (a row of POCKETS and
    SPECIAL SLOTS kept whole), the stash heading a column of its own right of the gear, and what follows the stash (its
    windows, the windows attached to none) in the shortest column beside it (the gear's last, the stash's, or one after)
    where it fits; else everything in one column. What is folded counts as its fold line, so a page folded to what a
    reading changed takes larger cells than one read whole, and a name opened from a fold line may set them smaller. Only
    where even 40 doesn't fit the height does the page scroll (at 40, the gear's one column left of the stash's where
    both fit the width). Measured on the layout rule (`DeskPageRulesTests`) and the snapshots of 2026-10-19: the owner's
    page after the Keys window (the gear folded, the stash of 10 × 13 and the Keys window of 11 × 7 open) at 96 on a
    2560 monitor's desk (2176 × 1330 DIP), the stash filling the height and the window beside it, at 48 at 1600×900
    (1216 × 760), at 40 in one column at 900×560 (516 × 440), scrolling; the made-up desk read whole (every region open)
    at 80 in three columns at 2544×1401 (the gear's two and the stash's), at 40 in three at 1600×900, unscrolled. Until
    2026-10-19 the width alone (owner, 2026-10-19: "We should utilize the whole screen"): 40 to 80 DIP, the gear and the
    stash two columns from 56, 64 at 1600×900 and 80 at a 2560 monitor's, scrolling, a third of its desk empty; until
    2026-10-19: 40 DIP, 56 from a desk 600 DIP across, two columns from 980. Over the grids one
    head line in muted capitals, the totals and the keeps (*The head line and the fold*, below; until the addendum one
    total, "STASH · 6.84 M ₽ · KEEP 3 · 2 · 4"). When it opens by itself, its page coming in says so (*Fun*, below);
    how that meets SEEN IN YOUR STASH, which pictures a reading's quest items over the map (principle 11), was not on
    the panel and was settled with step B (the cue over the desk's page), then by the owner after trying it (2026-10-19:
    "It now conflicts with the 'now seen in stash' animation"): the desk's arrival is the feedback, and a reading's needed
    items are said by the one-line notice (principle 11). Built (step B, 2026-10-19, part 2: the view).
  - **What it mirrors** (the owner's notes, and five screenshots of 2026-10-19 (GEAR tab), §2; the regions' layout
    from the addendum, A(2)). Every grid of the screenshot, each under its own head, stacked in reading order in one
    column, 400 DIP at the smallest window: LOADOUT, then the middle column's regions in the game's order, TACTICAL RIG
    · POCKETS and SPECIAL SLOTS on one row (as the game has them, each under its own head) · BACKPACK · POUCH, then
    STASH (until the addendum, LOADOUT · POCKETS · BACKPACK · RIG · POUCH · STASH: the game's column starts with the
    rig). Where both hold at 56 DIP or more (*Its place*), the column breaks before STASH into the game's two
    columns, the gear left and the stash right. The game scrolls the middle column and the stash each on its own, so a
    screenshot is one page of each. So a screenshot taken right after a raid is an overview of what came back and what
    to do with each thing, and what came back stands first. With every region open the page is about 2.6 screens at
    900×560, and the stash, last, is reached past the gear: the fold answers that (*The head line and the fold*).
    The game's two columns at every size (A(1)) would cut the stash at 900×560, or take its cells under the 40 DIP
    floor, and give the view two scroll bars. The view of it is built (step B, 2026-10-19, part 2: the view; *As
    built*).
    - **LOADOUT** is a strip of slot cells, one per filled slot in the game's slot order, wrapping at the column's
      width: 80×40 DIP (two cells) a slot, 164×40 a weapon. The game's square slot boxes would double its height, and
      a cell of one doesn't hold a range or "147/147" at 40. No slot names on the strip (an eyebrow per cell would need
      8 px capitals cut to five letters): the card names the slot ("HEADWEAR · BAC Bastion"). Empty slots are left out:
      nothing to price.
    - **A container in a grid** (a bag in a bag, a Zhuk rig in the backpack) is a cell with its own price, its figure
      ending in "+" (*The cell*), until a screenshot with its floating window open (the same or a later one) is read.
      Then the window's grid stands under a head of its own, "<NAME> · IN THE <REGION>" ("ZHUK · IN THE BACKPACK"),
      right after its region, a stem from it to its cell; pointing at either lights the other (the linked highlight,
      "Visual language"). The "+" drops, the cell's figure stays the container's own, and the contents count in the
      total of the part the region is in (ON YOU for a rig in the backpack). The cells the window covered on the
      screenshot keep their last reading, unmarked; a first reading under a window leaves them empty. **Several
      containers of one name** (three wallets): the window's grid stands under its name after the region that holds
      them all, attached to none, with a muted line saying so ("Not attached: 3 wallets on the page"), and counts in
      that region's total; split between ON YOU and the stash, it stands at the page's end, counted in neither, and its
      line says so. **A weapon's mods** are unseen the same way: its figure is the bare gun's, with the "+".
    The desk never guesses contents, and a total counts only the cells read. The mirror is drawn from catalogue art
    (tarkov.dev's grid images, §3) at the positions and footprints read; no pixel of the screenshot is kept.
    **A blank reading never replaces a good one** (the owner's test of 2026-10-19: the Keys window, read whole at 20:21,
    read after the game's SORT as one "?" across six cells, which took the window's place and its keys out of the
    total; the coordinator's rule; `DeskLedger.Blank`): a window in which no cell was read (no item, no "one of N") keeps
    the page's version of it, and so does a region that comes out with fewer than a third of the cells read that the page
    has for it while more of its grid held something unread than was read (shapes no label fit, labels that fit
    nothing: the lines between its items weren't found). The page's version stays, unchanged (the fold doesn't open
    it), and the app log says so. A region the player emptied reads its empty cells as empty and still replaces the
    page's (an unloaded backpack after a raid, a stash paged to an emptier page): the coordinator's rule asked for the
    third alone; the unread share is mine, for that case.
    **A region read in part keeps the page's rest** (owner, 2026-10-25; the test of 2026-10-19: the backpack's grid ran
    past the middle column's foot, then lay under the Keys window, and its slot alone, read each time, took the page's
    grid of five by six away; `DeskRegion.Partial`, `DeskLedger`): a middle region whose grid runs past what the
    screenshot shows, cut by the column's foot or not in view at all (under a window, scrolled away), stands for the
    compartments read alone. The page keeps its cells and compartments outside them, a compartment the foot cut as tall
    as the page had it, the grid's size, and its slot where none was read (or one whose label wasn't), with the cells read
    in their place: the rows in view of an emptied backpack read empty, the rows below the foot keep their last reading.
    Not when the slot reads another container than the page's (its grid was the other one's), and a container's slot seen
    empty, its frame in view with no label in it, takes the grid away with it. The app log says what it kept and why ("cut
    at the column's foot", "under a window", "not in view"). (The decision said a grid cut at the foot keeps the page's
    version; whole, the pouch at the foot after a raid, or the backpack emptied there, would then never be read anew, so
    the rows in view are taken: step 1's reading of it, the owner's to overrule.)
    **What leaves the page** (owner, 2026-10-25; the owner's page of 2026-10-19 kept four windows made of misread headings
    and of the POUCH's grid, which no reading would ever take away): a window attached to nothing (no container of its
    name on the page) that three readings in a row didn't show is dropped; a window with no cell and no title is never
    kept, read or loaded. A window attached to its container stays however long it goes unseen. Each region keeps when the
    screenshot that last showed it was taken and how many readings have missed it since (§8, "The desk file"; for the
    head's "BACKPACK · 23:08" of the layout decided the same day).
  - **The head line and the fold** (the addendum, B1). Over the page, in muted capitals, "ON YOU 1.01 M ₽ · STASH
    6.84 M ₽ · KEEP 3 · 2 · 4": what is on the player against what is in the stash (after a raid, what came back
    against what stayed), then the page's cells with a now, next and later notch. A total counts what each cell would
    bring, the better of the flea's net after the fee and the best trader's price, not the listing price a cell shows
    (*The cell*; 2026-10-19: a key listed at 15 M nets 5 M after the fee, so the cells' listing prices would double a
    stash of rare keys). A total with an unseen container
    in it ends in "+" ("ON YOU 1.01 M+ ₽"). One total (B2) drowned a raid's haul in the stash's millions; keep counts
    per part (B3) took two lines for what the notches already say.
    **The fold**, one rule for all regions: each reading opens the regions it changed and folds the rest into one line
    where they stand, "UNCHANGED · POUCH ▸ · STASH ▸" (folded neighbours share it), each name opening its region until
    the next reading. The first reading opens all; a reading that changed nothing changes nothing. After a raid that is
    the haul and a line; while sorting the stash, "UNCHANGED · LOADOUT ▸ · POCKETS ▸ …" on top and the stash page whole
    under it at 900×560. "Unchanged" compares the new reading with the page in hand only, never with a register (§1).
    Its cost: the page changes shape between readings, and a click shows the stash after a raid. An unchanged region
    shown whole or muted would cost a scroll for nothing new; hidden, it would lose the stash without a word. Built
    (step B, 2026-10-19, part 2: the view).
  - **The cell** (B1): the game's layout, with the price where the game prints the short name. Built (step B,
    2026-10-19, part 2: the view; what gives way where a cell is narrow, *As built*).
    - **Top right**, the cell's figure: where the flea is the better sale (its net at the last low after the fee at
      least the best trader's price), the flea's listing price at the last low as the flea shows it ("1.00 M"); else the
      best trader's price (owner, 2026-10-19, after the test: the Dorm 104 key showed "~11k", its net, while the flea
      listed it at 15k; the net is the card's line, "Flea net … · fee …"; until then the net was the figure). In ink,
      semibold, at the badges' 10.5 px with the map's dark collar (12 px at 56 DIP),
      after its source, the price tag `E8EC` (the flea) or the person `E77B` (a trader), 9 DIP and muted (11 at 56); at
      56 DIP a trader's 14 DIP portrait stands in for the person. A cell with no price (the secure container) shows no
      figure.
    - **"+" after the figure** (the addendum, C1a): "at least this, more inside", on a container whose contents no
      window has shown ("34k+") and on a weapon, its mods unseen ("~83k+"); a container's drops once its window is read
      (*What it mirrors*). It takes no corner, so it never meets the notch, the "?" plate, the "~" or the arrow, and it
      fits a 1×1 wallet; it carries to the totals it is in. It is read, not seen pre-attentively: the card says the
      rest, "Contents unseen: open it in the game, then a screenshot".
    - **Bottom right**, the count, as the game prints it.
    - **Top left**, the keep notch, 11 DIP, from the quest tree (§2): a filled corner triangle for **now** (an active
      quest's BRING needs the item), an open triangle for **next** (a quest the active ones unlock), a corner bracket
      for **later** (further down), in amber; a hideout module's need, its progress unknown (§1), the filled triangle
      in ink. The strongest need sets it. A cell takes "later" only when that need is found-in-raid or the item can't
      be bought (no flea, no trader): quests far down the tree want common things, so "later" everywhere would mark
      half the stash and say nothing, while a found-in-raid need sold can't be bought back as one. The card lists every
      need. The notch never greys the price: the price says what selling gives, the notch what keeping is for.
    - **Bottom left**, ↑ or ↓ where the 48 h change is 10 % or more either way; at 56 DIP the change stands there as a
      figure on every cell ("↓ −10%", "−1%"), and where the count leaves less room the figure without its arrow
      ("−10%" beside "2/4"), then the arrow alone, as at 40 (UI QA pass of 2026-10-19, F5: the Analgin's "2/4" showed
      no change at 56 and "↓" at 40). At 40 DIP a 1×1 cell with a long condition ("166/400") has no room for the arrow,
      which gives way; the card keeps the change.
    - **"~"** before the figure: an estimate from condition (the addendum, Da; until then "a price that assumes full
      condition", the scaling left to step C). Where the cell prints a condition ("46/50", "1469/1800"), the figure is
      scaled by the printed share and keeps the "~" (a Grizzly at 1469/1800, "~31k", not its full 37k); at a full print
      ("3/3", "50/50", a key's "40/40") it drops it (owner, 2026-10-19: the Dorm 104 key at 40/40 showed "~11k"; until
      then only meds, food and drink dropped it); armour keeps it, its printed maximum not being the catalogue's
      (the screenshots' Trooper prints 147/147, the catalogue says 180). Where no condition is printed (armour, meds,
      weapons), "~" alone, on the full-condition figure. The card shows both ("Scaled to 1469/1800 · full 37 386"), and
      the click copies the scaled price (*The click*). A model: a straight share is a guess (traders' pay for meds
      tracks the resource, armour's less simply), and whether tarkov.dev's last low is for full condition only is
      unchecked. The plain "~" on the full figure (Db) was not taken: a 1/4 Analgin would read as a full one, and ON YOU
      would swell after every raid. A magazine's "60/60", printed where a condition is, is its rounds, not a condition.
    - **A cell that fits several items**: the "?" plate at its upper left, as the map draws it, the art at half
      strength, and the candidates' price range as its figure ("63–307k"); its card says "One of 3". A slot shows no
      footprint, so a slot's short name shared by variants stays a "?" (4 of the 7 filled slots on the owner's
      screenshots: BAC Bastion in three colours, Death Shadow in three, the armband in seven at 0.3–294k, a dog tag by
      level). "Zhuk" names two rigs of one size (Black and SURPAT, 4×3), so its cell is a "?", and its window's grid
      attaches to it either way. The player's pick among the candidates (*The click*) stays with the cell until its
      label changes: while the item picked is still among the candidates a newer reading gives the same slot, or the
      grid's cell at the same place (owner, 2026-10-25).
    - **Behind the art**, ink at 5, 10 or 16 % from 20k, 100k or 300k ₽ per slot, by what the better sale brings (the
      flea's net or the trader's), not the listing price.
    - **A count past what the item stacks to** (tarkov.dev's `stackMaxSize`) is a misread: one, at least (the owner's
      test of 2026-10-19: a drill, which doesn't stack, read "×310" put 53 M into a Junk window's total).
  - **The card** (C1), in the rail's 351 DIP, decision first. The item's icon and name (a slot's item after its slot,
    "HEADWEAR · BAC Bastion"; the addendum), under it in muted capitals the liquidity dot (full from 50 offers, half
    from 10 to 49, a ring under 10), the offers and the flea level ("40 offers
    · flea from level 35"); the cell's figure in gold at the glance's 22 px, its 7-day change at its right, arrow and
    sign ("↑ +1.7 % · 7 D"); right under it the plate, "A CLICK COPIES" with the rule's control (*The click*) and what a
    click copies now in short lines, "Copies 970,000 ₽", then muted "Nets 873k ₽ after the fee" (thousands from six
    digits) and its reason, "Last low 1,000,000 ₽ · Therapist pays 494,700 ₽", a line that wraps breaking only at the
    " · " (UI QA pass of step C, F5: "Kopiert 970.000 ₽ · bringt 873.000 ₽ nach Gebühr" wrapped and pushed KEEP out of
    the smallest rail; the owner, 2026-10-19: the rule's control right under the gold figure; until then the plate
    "Click the cell: copies 970 000 ₽" stood after the flea's net line); the flea's net line with the fee and the
    48 h change ("Flea net · fee 97 000", "↓ −10.3 % · 48 H"); KEEP, the needs in progress order (now, next, a hideout
    module's, later), each with its notch, count and FIR ("Private Clinic ×1 · now · FIR", "Medstation 3 ×1 · a hideout
    module"), the quests and stations by name. Then 30 DAYS, a band from the minimum to the average, a point
    every 2 h, with a mark 7 days back and the high and the low in words ("HIGH 1.05 M · LOW 0.82 M (DAY 17)"); 24 H,
    the day's range bar with the average and the last low, a high far past it said in a line, not drawn ("The high,
    9 999 999, is past the bar: one offer, likely an outlier."); TRADERS, each with portrait and price; CRAFTS AND
    BARTERS; "Prices 4 min old · tarkov.dev" (from tarkov.dev's scan of the item). About 640 DIP: the first 200 (the
    figure, the click, the keep) fit the smallest rail in English and German, no line of them wider than the rail's
    351 DIP (at 125 % text they wrap, between their parts), the rest scrolls. EVE Online's min-max channel became the min-to-average band: the history has no
    maximum. A figure with a "+" says "Contents unseen: open it in the game, then a screenshot", a scaled one "Scaled to
    1469/1800 · full 37 386" (the addendum). Built (step C, 2026-10-19; *As built*, *The card*).
  - **The click** (D2; owner, 2026-10-19, after trying step B: "The user interface on how to copy a price is not clear";
    then, on a COPY button on the card: "I don't think we need a visible copy button, clicking on the item suffices. But
    there should be a visual feedback and UI elements for setting what is copied clearly visible."): the cell is the
    button. Point at a cell to read its card; click it to copy its list price to the clipboard, and only there: nothing
    is ever typed into the game (§2). Its frame flashes gold once and its figure decodes, and a one-line notice says what
    and why, "611,100 ₽ copied · 3% under the last low", and dismisses itself after 4 s (principle 4); the player pastes
    it into the flea's price field (the clipboard gets the digits alone). Of a figure scaled to a printed condition, the
    click copies the scaled price (the addendum, Da), and the notice says ", scaled to 1469/1800". **The rule is on the
    card**, visible (the owner, 2026-10-19): the plate under the gold figure, "A CLICK COPIES", with − and + around the
    rule in amber ("3% UNDER", "THE LAST LOW", "2% OVER"), a percent a step from 20 % under to 20 % over, and under it
    what a click copies now and nets after the fee; the pointed card and the pinned ones alike, and settings' row "Flea
    price to copy" ("Settings", below) is the same control and the same state, kept in shturmap.db (`desk.copyRule`),
    3 % under unless changed; the − and +'s tips say where a press takes it ("Lower: 4% under the last low", "Lower: 1%
    over the last low"; UI QA pass of step C, F4: over the last low the − said "1% further under"). Chosen over a
    segmented choice (MATCH · 3 % UNDER · 5 % UNDER · 3 % OVER): one control
    holds every rule the setting allows, so card and settings never disagree, and it fits the plate beside its eyebrow
    in 351 DIP (the coordinator's choice; the owner's to overrule). **A right-click pins** a cell's card in the rail, and
    the pin at a card's head pins or unpins it (the owner, 2026-10-19: "It is also not possible to 'pin' items to the
    side view"; a right-click is no input to the game either): several stack, newest on top; a pinned cell carries the
    pin's tab ("Visual language"). A "?" cell's card lists its candidates, each with its art and figure, the best first;
    one click on one picks it, and the pick stays for that cell (in desk.json) until its label changes: over every newer
    reading in which the item picked is still among the candidates of the same slot, or of the grid's cell at the same
    place, whatever else of it was read anew (its count, its condition, another candidate), and gone with one in which it
    isn't (owner, 2026-10-25, the layout round; until then only over a reading that showed the cell exactly as it was, so
    a count read otherwise dropped it); a click on the "?" cell itself copies nothing and the notice says "One of 3
    items: pick it on the card first". The rule is one setting, "Flea price to copy": the last low matched, or 1–20 %
    under or over it, 3 % under unless changed. Built (step C, 2026-10-19). **Guards**, said on the card's plate: never a net under the best trader's (the plate says "Therapist pays more: 459 000 ₽" and why, and a
    click copies nothing); above the most a sale can net (the fee's curve: about 51 times the base price at PvE's
    rates) the plate carries the status bar's gold light and says so ("over the best net: …"); no flea price for what
    can't be listed, "Can't be listed on the flea" with the best trader's price (`noFlea`, the item's flea level, the
    found-in-raid rule; Shturmap doesn't read the player's level, so the card says the item's and compares nothing);
    "~" for condition. A click that copies nothing says why in its notice ("Nothing copied: Therapist pays more,
    459,000 ₽"). The flea's price field takes Ctrl+V (owner, 2026-10-19, tried in the game). The clipboard isn't input
    to the game: `SafetyTests` forbids no clipboard call (COPY DIAGNOSTICS used it already), and says why beside its list.
  - **Fun, in the app's own motion** (the owner: "it should also be fun to use"; principle 2; "Design system",
    *Motion*): a page just read comes in cell by cell, 50 ms apart, as the kit's pictures do (the regions it opened:
    only what changed; *The head line and the fold*); its totals count up once (both built, step B, 2026-10-19, part 2:
    "Design system", *Motion*); a copy flashes and decodes (built, step C, 2026-10-19). Nothing else
    moves. Colours keep their meanings ("Visual language"): no red or green for down and up, only arrows and signs.
  - **As built** (step B, 2026-10-19, part 2: the view; the coordinator's rules where the panels left a choice, for the
    owner to judge with the dev build):
    - *The switch*, a primary control (owner, 2026-10-19: "The Plan/Desk switch is way to small, needs much bigger.";
      until then PLAN | DESK at the right of the MAP heading, two words in the status words' 11 px in a frame about
      110 DIP wide): two halves across the rail's inner width, 351 DIP (174 each inside one `LineStrong` frame, a
      `LineStrong` hairline between them), 36 DIP tall as the map's buttons, at the rail's head above the
      MAP eyebrow, 16 DIP from the rail's top as from its sides and 14 above MAP. Each word in the title style, 17 px
      semi-condensed semibold, letter-spaced as the eyebrows (0.14 em), centred in its half. The view on screen: its half
      on `Raised` with the open map's row's 3 DIP amber bar at its left, its word in amber; the other half on the rail's
      ground, its word muted, `Raised` under the pointer as Plan's rows. Tab reaches each half, Space or Enter picks it,
      ← and → pick PLAN or DESK and take the focus there (`Rules.DeskSwitch.ByArrow`); the tips say what each view
      is. In Plan and on the desk alike; in a raid it is gone and MAP stands at the rail's head as before.
    - *When the desk comes and goes*: it opens by itself for a reading the window hasn't seen (the model's reading
      counter above the last one seen; a page read back from its file at start is reading 0 and opens nothing), in the
      menus and not while the tour is up (the tour owns the window; the switch shows the reading after). It goes back to
      Plan for PLAN, for Esc (once no card is open: the cards first, as ever; then the desk; then a trader's held focus),
      and for what needs the map or Plan's rail: a map picked in the MAP list, a trader's portrait clicked, What's New
      shown, the tour opening. A raid loading takes the window (principle 3), and after it Plan; DESK shows the page
      as it was, its fold too. The desk coming closes the cards, ends a map row's preview and a raid's replay (the map is
      out of sight); the cues and notices draw over the desk as over the map. SEEN IN YOUR STASH stood over the desk too
      until the owner tried it (2026-10-19): a reading's needed items are said by the one-line notice since (principle
      11). While the desk is up the map's keys (F, Shift+F, + and −, 0) leave the
      map alone, and PgUp and PgDn scroll the page.
    - *The rail* (step C, 2026-10-19; the order since the UI QA pass of step C, F2, 2026-10-19: with two pins the pointed
      card stood under them, out of sight at 900×560): under the switch, the MAP list and the trader strip, which stay
      where they are, the card of the cell pointed at, until another cell is (a pinned cell's framed as pinned), or while
      none is the hint, "Point at an item for its card. A click copies its flea price; a right-click pins its card
      here."; then the pinned cards, newest on top: whole under the hint while no cell is pointed at, and while one is,
      one compact line each (the item's icon, its name and the gold figure, framed in amber; a long name is trimmed with
      an ellipsis at its end; the whole name stands on its card, a click away, UI QA re-check, 2026-10-19), a click on
      one showing it whole in the pointed card's place, from its head (the room scrolled back to its top). **The pins'
      foot** (UI QA pass of the desk's second round, F1, 2026-10-19: under a key's card the second pinned line stood cut
      at the window's bottom at 1600×900, in German below it; the coordinator's rule): while a cell is pointed at, the
      pinned lines stand at the rail's foot under the rail's hairline, always in sight, three at most and more scrolling
      within that strip, and the pointed card scrolls in the room above them (with three pins at 900×560 a German key
      card shows its figure and the plate's head before the scroll; the owner, 2026-10-19: accepted). The rail scrolls: at 900×560 the decision
      part of the pointed card shows (with two pins at the foot, its figure and plate in English and German, KEEP under
      the hairline; on the desk the 20 DIP at the foot of what scrolls are the cards' own, so a card runs on to the
      hairline or the window's edge).
      Until step C the rail showed the item card the pointer opens anywhere.
    - *The card* (step C, 2026-10-19; `Controls.DeskCardView`, its words `Rules.DeskCards`): 351 DIP, the item card's
      ground and frame, whole (no map under it). Its head: the item's 40 DIP icon, a slot's name in muted capitals over
      the item's ("HEADWEAR" over "BAC Bastion": the addendum's "HEADWEAR · BAC Bastion" as two lines), the title, the
      liquidity dot with the offers and the flea level ("60 OFFERS · FLEA FROM LEVEL 15", or "NOT ON THE FLEA"), the pin
      at the right. The gold figure is the cell's (the better of the flea's net at the last low and the best trader, for
      the stack, scaled), "~" and "+" as the cell has them; the 7-day change from the history, "…" while it comes. The
      plate on `Raised` with a `LineStrong` frame: the eyebrow and the rule's − and + on its first line, what a click
      copies on its second (semibold ink; muted where it copies nothing; with the status bar's 6 px gold square before it
      over the best net), under it, muted, what that nets after the fee ("Nets 873k ₽ after the fee"; over the best net,
      "Over the best net: at 1.75 M ₽ it nets 708k ₽, at this 707k ₽"), then the last low and the best trader (the trader
      left out where the line says they pay more), in parts a line breaks between (QA pass of step C, F5). Then the flea's net line, the condition or contents line, KEEP (each need's notch as the cell draws it;
      a quest's or station's name from the data, "a hideout module" for a station's level); 30 DAYS: the band from the
      lowest offer up to the average in amber at 22 % with the average's line on its top, 64 DIP tall across the card, a
      dashed hairline 7 days back marked "7 D" (a text, "7 T" in German), the high and the low in words under it ("HIGH 45k · LOW 34k, 14 DAYS
      AGO"), a note on what it shows; 24 H: "LAST LOW … · AVG …" over a 4 DIP track from the day's low to its high, the
      last low's tick in amber, the average's in ink, the ends' figures under it ("›" after the end where an outlier high
      is past it, and the line saying so); TRADERS, every one with a price, the best first, per item; CRAFTS AND BARTERS,
      as the item card has them (those that give the item); "Prices 4 min old · tarkov.dev" (from two hours, in hours),
      from the item's scan (`updated`; of a "?" card its candidates' newest), the item file's fetch where none says.
      Figures in full with the language's thousands separator ("970,000 ₽", "970.000 ₽"). A "?" cell's card: its slot's
      name, "One of 3 items", "Pick the one it is: …", and a row a candidate (its icon, name and figure), the best first.
    - *Pins* (step C, 2026-10-19; `Rules.DeskPins`): a right-click on a cell or the pin on a card toggles it; pins stack
      newest on top, without a limit; a pin stays with its cell while the page holds it as read (place, items, count,
      condition: a reading that shows it as it was keeps it, a pick too) and goes when a reading changes or takes the
      cell, or the mode's page goes. Esc leaves the pins alone (it closes cards, then leaves the desk as before): they
      are cheap to make and dear to lose; the developer script's "desk unpin all" takes them all.
    - *The page*: the head line over the page in the status words' style, muted; then the regions, each under its
      eyebrow. One column as wide as the stash's ten cells (or the widest grid); flowed (*Its place*), each column as
      wide as its widest open grid, POCKETS and SPECIAL SLOTS side by side counted together, a gear's column six cells at
      the least and the stash's ten, a column of fold lines alone 240 DIP, 24 DIP apart; columns only where they all fit
      the desk (UI QA pass of 2026-10-19, F1: at 1600×900 the stash's tenth column ran past the window). A window in a
      column beside its cell's has its stem down the gap between the two, out of the cell's side that faces it, into the
      head's near end. The regions' heads grow a pixel every two steps past 80, to 16 px at 120 and 128. LOADOUT draws no grid, whatever a reading's region says (F2: a reading's eleven by one drew a row of
      empty cells under the strip and set the gear's column 616 DIP wide). A region's own slots (the loadout's, in the
      game's order; a rig's, backpack's or pouch's own container) stand in a strip over its grid, wrapping at the
      column's width; a grid's compartments are drawn as the model gives them, its empty cells the panel's ground with
      hairlines, and where it has several they stand apart as the game sets them, 2 DIP at 40, 4 at 56, each framed in
      `LineStrong`, a cell in one standing in by half the gap where it runs along its edge (F3: the Zhuk's ten pockets of
      one by two read as twenty equal cells). A fold line, "UNCHANGED · POUCH ▸ · STASH ▸", stands where its regions
      would, its names amber links; it never runs across the two columns' break, and where it wraps, each "·" goes with
      the name after it, so no line ends in one (F8). A page first shown without a reading that changed it (read back from its file at
      start) opens every region.
    - *The cell*: the art of a grid cell is its grid image (its footprint; turned a quarter where the cell stands the
      other way), of a slot the item's icon (a slot shows no footprint, and the grid image prints its short name);
      a "?" cell's first candidate's, at half strength. The grid images bring the game's own ground, so the tint lies
      over the art, not behind it. Where the grid image prints the short name, a band of the ground's dark at 88 %
      across the cell's top carries the figure, so the band is its collar (at 80 to 104 DIP the art's printed name shows
      faintly through it; the owner, 2026-10-19: left so, the figure reads); the count and the change stand on small
      plates of the ground's dark at 70 %. The figure is ink, semibold, 10.5 px (12 at 56 DIP); the count and the
      change 10 px (11.5). "~" is drawn in Segoe UI's bolder wave: Bahnschrift's, at 10.5 px, read as a minus. What
      gives way where a cell is narrow, by an estimate of the text's width at the size it is drawn (the window's text
      size included; a figure about half an em a character, a count's digits 0.45, its slash 0.3, measured on the
      pass's pictures): a number is never cut (UI QA pass of 2026-10-19, F6: at 125 % "166/300" read "166/30"). First
      the change, beside the count (so at 40 DIP a 1×1 cell with "166/400" loses its arrow, and one with "2/4" keeps
      it; at 56 the figure, then the arrow alone, *The cell*); then the source's glyph, beside the notch or the "?"
      plate (a 1×1 cell at 40 DIP with a notch shows its figure alone); a "?" plate that leaves its range no room moves
      to the cell's lower left (a "?" cell has no change there); a figure still wider than its room is set smaller,
      never drawn under 8 px; and a count or a figure that doesn't fit even so moves to the card. At 56 DIP a trader's
      14 DIP portrait stands for the person.
    - *Numbers*: three figures at most: under 1,000 as they are, under 10k with a tenth ("4.5k"), then whole
      thousands ("902k"), from a million "1.01 M", "12.3 M", "123 M"; a range in its high end's unit ("0.3–294k",
      "0.45–1.18 M"); halves round up; the language's decimal sign ("1,01 M"); the units are texts, joined to their
      numbers by a no-break space in every language ("1.01 M ₽"; the arrow to its change, "↓ −10%"; the senior
      translator, 2026-10-19; `TranslationTests`).
    - *A window's stem and light*: a hairline (`LineStrong`) from the container's cell to its window's head, along the
      grid's lines and the page's margin: out of the cell at its side toward the margin (its middle, however tall the
      cell, where it stands at the grid's edge on that side, its lower corner elsewhere, so it runs between the rows),
      into the margin, up or down to the head, and into it (UI QA pass of 2026-10-19, F7: it left a three-row Zhuk 28 DIP
      below its top, the middle of its first row; now its middle, as written). **It never crosses a cell an item covers**
      (UI QA re-check, 2026-10-19, F2: on the owner's Keys reading it ran from the box's lower corner along a row line
      straight across a 3×3 case's picture; `DeskPageRules.StemRoute`): it runs along the gaps and over empty cells only,
      an item larger than one cell covering its inner lines (an item's edges stay free, a 1×1's all four). It takes the
      nearest row line free to the grid's edge: one along the cell's own side, its lower corner first, else one reached
      down or up the column line beside the cell, below before above at an equal distance; the fewest turns first, then
      the shortest way; where every such line is blocked, along the free lines to the region's outside edge, which no
      item covers, and round it. Pointing at the cell or anywhere on the window's region lights both: gold at 18 % over
      the cell with an amber frame, gold behind the window's head, the stem amber, easing over 0.18 s (at once with the
      effects off). A window whose cell is in a folded region has no stem.
  - **Built: the model the view draws** (step B, 2026-10-19, part 1: reading and data; `Shturmap.Core.Desk`; the view,
    its texts and the tick's words are part 2). The session publishes the mode's page with its values
    (`SessionSnapshot.Desk`, a `DeskView`): the regions in reading order (`DeskSheet.Regions`: LOADOUT's slots, one cell
    each without a footprint; TACTICAL RIG, POCKETS, SPECIAL SLOTS, BACKPACK and POUCH, each with its own container's slot
    and its grid's compartments side by side; STASH; each window right after the region its container is in, one attached
    to none at the end), each cell with its item or candidates, place and footprint, count, printed condition,
    found-in-raid mark, contents unseen, figure and keep notch (`DeskCellSheet`, `DeskFigure`), each region's part (ON YOU,
    STASH or neither) and total, the head line (`DeskTotals`), the regions the last reading changed (the fold's input) and
    the prices' age. A reading is taken into the page in hand (`DeskLedger`): the regions it shows replace the old, the
    others stay, the cells a window covered keep their last reading; a region read too blank keeps the page's (*What it
    mirrors*, 2026-10-19); a region read in part keeps the page's rest, a window attached to nothing leaves after three
    readings that didn't show it, and one with no cell and no title is never kept (*What it mirrors*, 2026-10-25). A file
    written before 2026-10-19 with a cell read twice (a window's compartment found twice)
    takes each place once when read back. What the rules decided where the panels left it
    open, for the owner to overrule:
    - NOW is an active quest's need: what its hand-overs and finds take as well as BRING's rows (keys, items to plant,
      mark or use, gear to wear, mods), so that "Private Clinic ×1 · now · FIR" holds; NEXT a quest not started whose
      quests before are all done or active (the active ones unlock it, or it is open to start); LATER every other quest
      not done. The player's faction isn't known: both factions' quests count.
    - The notch's order: now, next, a hideout module's need, later (a module's need is wanted at some level for certain,
      a later quest's is far off); KEEP n · n · n counts now, next and later only.
    - A "?" cell's figure is its candidates' range and counts in a total at its least; its notch is the weakest all its
      candidates have, none when one has none.
    - "Unchanged" compares place, ids or candidates, count and condition, not the found-in-raid mark.
    - A window's grid attaching to a "?" cell narrows it to the candidates of the window's name (the Zhuk harnesses, not
      the Zhuk body armour).
    - The flea figure carries the item's flea level and compares nothing (the player's level isn't read); the fee's
      hideout levels are 0, no setting yet; tarkov.dev's sell prices include Fence's.
- **The in-raid glance** (owner, 2026-10-19, three choices: when, what and what is kept; its look from its own panel the
  same day, Variant 1 "with the looted container's or body's item layout mirrored"; its reading built the same day, the
  reader round, §2, and its plate, *Built*, below). Every
  screenshot taken in a raid that shows an inventory screen, the player's
  own loadout or a looted container or body, is read as it arrives, as the extract list is, and a plate over the map
  says what its items are worth, at a glance: the answer from a loot screenshot the owner described with the desk (§1).
  - **When:** as the screenshot arrives, the extract list's reading first (§2), then its grids, read as the desk's are
    (§2, "For the stash desk"). The menu screenshots' tick gains the words "and in a raid" (worded with the plate: "Read
    your stash, gear and tasks from menu screenshots, and loot in a raid"); unticked, no inventory in a raid is read.
  - **What:** a plate over the map in the cue pattern (principle 11): FROM YOUR SCREENSHOT, the total, the top five by
    value per slot with their keep marks, and "n unsure" (the "?" cells), for about 6 seconds. No click; the map stays
    beside it. The raid's rules are unchanged: glanceable, nothing to do (principles 1 and 2). **Its look** (owner,
    2026-10-19, from its panel of three: Variant 1, the total large and the desk's cells, at the top of the map's
    column, "with the looted container's or body's item layout mirrored"): in place of the top five, the LOOT pane as
    the game shows it, a container's grid or a body's slots and parts, so the player sees where everything is.
  - **Kept:** nothing. No in-raid pixel, position or item is stored for it. The desk's rules on nested containers
    apply (*The stash desk*, *What it mirrors*): in a looted backpack with a rig inside, the rig's contents count only
    from its open window.
  - **Fixtures:** 22 in-raid screenshots of 2026-10-19 (one raid) in the ignored `tests\fixtures\ocr\raid-loot`.
  - **Reader built** (the reader round, 2026-10-19; §2, "In a raid"): what the plate gets is `SessionSnapshot.Glance`, a
    `RaidGlance` for the raid's last inventory screenshot, null out of a raid and until one is read: its `Sheet` (the
    page with its values, as the desk's: each region's part, ON YOU for the player's own regions, LOOT for the pane's,
    NEITHER for a window attached to none; each cell's figure and keep notch; `Totals.OnYou` and `OnYouPlus`,
    `Totals.Loot` and `LootPlus`), `Loot` (the pane's title in the game's words, "LOOT · BODY", or null for the player's
    inventory alone), `TakenAt` and `Reading` (a new reading is a higher number). A LOOT region's `Search` (searched,
    searching, unsearched) and `Cut` say where contents went unseen; its region's figure ends in "+". The tick's words
    came with the plate (*Built*, below); the top five per slot gave way to the mirrored layout (*Its look*, above).
  - **Built** (2026-10-19; `Controls.GlancePlate`, `Rules.GlancePlateRules`, `MainWindow.GlancePlate`; Variant 1's
    drawing and words where the panel gave them, the coordinator's rules where it left a choice, for the owner to judge
    with the dev build). *As built*:
    - *Its place*: at the top of the map's column, centred, 16 DIP under its top, or 8 under the notes where one
      stands; in the map's column only, so the rail (the raid card, NEXT, EXIT) is never covered; as wide as it needs,
      the column's width less 16 DIP a side at the most; about half the map's height at the most. The cue's band: the
      ground at 90 % between two amber hairlines, 16 DIP of padding (12 over the eyebrow). The map's floor picker steps
      aside while it stands, as for a cue (the panel's C; at 900×560 the plate's end lay over it).
    - *Its head*, Variant 1's: FROM YOUR SCREENSHOT (13 px, muted capitals); "LOOT" in ink and the total in amber at
      the title's 48 px ("LOOT 1.87 M+ ₽"), one line, drawn smaller only where the plate is too narrow; under it, 15 px,
      "ON YOU 1.01 M+ ₽" and, muted, "· 2 unsure", the drawn "?" cells counted. The totals are the sheet's, as the
      desk's head line counts them; "+" where contents went unseen.
    - *What it draws*: the LOOT pane's regions in reading order (a body's slots, its TACTICAL RIG, POCKETS and BACKPACK;
      or a container's one grid; a window attached in the pane after its part), each under its own head in the desk's
      head style: a body's slots under the container's kind as the game prints it ("BODY"), as a strip of the desk's slot
      cells, two to a row in the least column; a part's own container as its slot cell, then its grid with its
      compartments; a container's grid under its kind ("DRAWER"; LOOT where none was read). Every cell is the desk's
      (*The cell*: art, figure after the flea's tag or a trader's sign, "~", "+", the "?" plate and range, the notch, the
      count, the change, the tint), drawn by the desk's own cell without its board, so nothing on it is pointed at,
      clicked or pinned. A part not searched yet has no grid to draw: a closed region stands there, the cells' ground in a
      strong frame, three quarters of a cell tall and as wide as its column, "UNSEARCHED +" in its middle; a part being
      searched, or a grid the pane's scrolling cut at its foot, draws what was read and says it after its head, "POCKETS ·
      SEARCHING +", "BACKPACK · MORE BELOW +". No stem from a window to its cell. A head wraps only before a "·", the words
      after it kept together, and the region's height counts its lines (the senior translator, 2026-10-19: "TASCHEN · WIRD
      DURCHSUCHT +" split its phrase in a four-cell column at 40 DIP and passed the plate's foot).
    - *What it doesn't draw*: the player's own regions (the loadout, the middle column, their windows): the gear is
      known, the glance is about the loot; ON YOU's total stands on the head's line. A screenshot of the player's
      inventory alone (no LOOT pane) puts up the head only: "ON YOU 4.63 M+ ₽" as the title and the muted line "Your gear ·
      nothing looted on this screen" (the coordinator's choice of the brief's two: it says the screenshot was read and
      what the gear is worth, in one line; drawing nothing would leave a reading unanswered).
    - *Sizes*: the desk's cell sizes (*Its place*) from 80 DIP, the desk's largest until its cells filled the screen,
      down to 40: the largest at which the regions fit the plate's room under its head, each column filled in reading
      order down to the room's foot and the next started beside it, as the desk's gear flows; a column four cells wide
      at the least (a weapon's slot widens it a little), not the desk's six. Where even 40 doesn't fit them whole, a
      region is drawn cut short at 40 (UI QA pass of 2026-10-19, F1: a body's backpack, a tall grid three cells wide, fitted
      no column and stood in the ALSO line alone, the plate a quarter of the map's height): its grid's top rows, as many as
      the room takes and at least two, under its head and state, "BACKPACK · SEARCHING +" (the search's word wins) or
      "BACKPACK · MORE BELOW +", in a column of its own beside the last where the width takes one, else in the rest of the
      last. Only a region whose head, slots and two rows don't fit gives way, with the regions after it, to one line in the
      fold line's words, "ALSO · POCKETS 2.3k+ ₽ · BACKPACK 2.89 M ₽", each with its total, every "·" with the name after
      it: nothing scrolls in a raid. On the made-up body at a 2560 monitor's window: 80 DIP in three columns; at 1600×900,
      48 DIP, three columns, nothing cut; at 900×560 the drawer at 40 whole, the body's regions no room under the 48 px
      title, so its plate is the head and that line. A real body's 3×8 backpack being searched, at 1600×900: 40 DIP, its
      top five rows beside the closed pockets (four in German, its head on two lines).
    - *Timing*: a new reading (`RaidGlance.Reading`) puts the plate up at once, without an entrance (the owner's motion
      call, below), for 6 s from its own arrival; then it fades out over 0.4 s, or goes at once with the animation
      effects off. A newer reading replaces it at once and its 6 s start again. While a cue is up (QUEST COMPLETE, the
      extract list's roll call, any other) a reading waits for the cue to go, then stands its 6 s (the coordinator's rule;
      the panel had the one-line notice in its place); a cue that comes while it stands ends it, as any cue ends another.
      The raid's end takes it, and a reading waiting with it. A snapshot run keeps it up for its picture; a reading there
      waits for a cue as a live one does, and a run with `--glance` lets its cues go in their time, so its still shows the
      plate after them, never the two together (UI QA pass of 2026-10-19, F6).
    - *Nothing kept*: it draws from the snapshot's `Glance` alone; no file, no page in hand, gone with the raid.
    - *Developer aids*: the developer script's `glance [<n>]` and `tools\fake-raid.ps1 -Glance <n>` (the app's
      `--glance <n>`, with a fake game) put up the made-up readings (`Rules.GlanceExample`: 1, a drawer with three items;
      2, a body with its slots, an UNSEARCHED rig, pockets being searched and a searched backpack); the replay
      `tools\qa\glance.devscript` with its stills and their checks in `glance.md` (drawer, body, gone, a reading waiting
      for the roll call, after it, the raid's end). `-Glance` plays a Scav raid, whose start reads no extract list, so no
      roll call comes to end the plate; the layout check's `glance` view is it with reading 2 (UI QA pass of 2026-10-19,
      F6). `shturmap-cli desk --raid --plate <w>x<h> <picture>` lays a real screenshot's LOOT regions out as the plate
      does in that room (the app log's "room"), so a real reading's plate is checked headless (F1).
    - *Decided* (owner, 2026-10-19, on the session's report page): **motion in a raid**: the plate stands at once, as
      built (principle 2 lets the desk's moves in out of a raid only); the panel's D (the band's short gold flash, the
      total counting up over 0.9 s, the cells 50 ms apart) is not built. The 48 px title leaving a body no room at
      900×560, its plate the head and the ALSO line, is accepted (owner, 2026-10-19). Still open: the plate is pictured with the made-up readings only, not yet with a real
      raid's.
- **Help** (F1 or `?`): one panel with TAKE THE TOUR and WHAT'S NEW IN 0.4.0 under its title (the tour from its start; the What's New card again), how it works with SHOW ME after the paragraphs a chapter of the tour shows (it opens the tour there; owner, 2026-10-09, T11), the shortcuts, the quest-type legend and the symbols on the map
  shown, the others behind a link (see "Map drawing", "Legend"), what Shturmap reads and that nothing is sent unless the player sends a report or allows
  crash reports, and HELP AND FEEDBACK: a line pointing to the feedback button, and COPY DIAGNOSTICS (§8,
  "Diagnostics"). Until 2026-10-09 it opened once by itself on first run, outside a raid; since then the tour does,
  in its place, and help opens only when asked for (a snapshot run still opens it for its picture). A step into a raid (it loads, it starts) closes the panel, since it stays open while the game
  has the focus and lay over the raid card; if it had opened by itself, it isn't counted as seen and comes back
  after the raid (`WhileInRaid`; review of 2026-10-04, H9).
- **The tour** (owner, 2026-10-09: "a proper first-time-opening tour of the app. This one should then later be
  accessible through the help menu itself. The tour should give the player a proper onboarding experience with the
  most important features, how everything works and what the most important aspects of the UI are. Here we can be a
  bit more flashy with nice animations"; "We need to keep in mind to update the onboarding with future updates"; from
  a panel of four forms and thirteen decisions, all taken as recommended: the briefing (A) told through an example raid
  (B), with first-time lines (E); `MainWindow.Tour`, `Rules.Tour`). Seven chapters over the real window, about a minute
  and a half: WHAT IT READS (what is read, what is never done, the risk in the README's words; review of 2026-10-09:
  its first title used a word §2 keeps off Shturmap), IT FOLLOWS THE GAME (Plan
  and Raid), NEXT RAID (Plan's rows, one resting under a drawn pointer previews its map), PICK AND POINT (an example
  quest picked, another pointed at), YOUR SCREENSHOT KEY (the one habit Shturmap needs: the key pressed large, the file
  name decoding, the position pinging; then the extract list as its own cause and effect: an example of the game's
  list, in Shturmap's look, decodes in the map's top right under "IN THE GAME: O TWICE, THEN PRTSC" (under the label
  where the two would share the map's columns, at the smallest window; whole above the band: where its room is too
  short, the example names fewer extracts, the farthest of the lit ones going, down to two lit and the "??:??:??"
  one, and the list read says so, "3 OF 11"; where even those don't fit, it is drawn smaller to fit, never cut; QA
  pass, 2026-10-15: at 900×560 the band covered its last row and the list read), a press reads it, the list goes,
  and it is called out as any list just read is: YOUR EXTRACTS THIS RAID across the map with a row
  for each of its extracts, the view taking them in with the position above the tour's band, and each triangle flown
  to its place, where it rings out, nearest the position first, while the rest are struck out (as built, 2026-10-15:
  the plate, then the cue, centred above the tour's band, with the map above the band cut out of the dim while it
  plays; the cue whole, under the label and above the band with its rows, its title in two lines at most, drawn smaller
  where that room is too short, never cut, as the plates are, and the example naming fewer extracts while it would
  stand under 60 % (owner, confirmed 2026-10-15 (evening page, E1), for the title; the coordinator's rule for the rest,
  after the third QA pass: at 900×560 the band covered its rows, "An example, not your raid." and the rows' hand-off
  points; principle 11); the view's move is the roll call's, at 1.2 s; when the cue has gone the frame goes to the
  listed extracts and the position; with the effects off, and in a snapshot, the chapter's end: the view on them, simply lit, no cue); the chapter's map is a
  PMC raid's, without the Scav extracts, as What's New's `extracts` preview is), IN THE RAID (RAID LOADING with an
  example kit, then the raid card's head with example values; a PMC raid's map too, while PICK AND POINT's, in the
  menus, has both sides' extracts, as Plan's map has) and HELP AND FEEDBACK (the story chapters aren't shown yet,
  anything else missing goes through the feedback button, F1; the three buttons named; owner, 2026-10-09: "GOOD TO
  KNOW" wasn't "really expressive").
  - **How it looks.** The window dims (ground at 86 %) except where a chapter's parts are cut out, each framed with the
    map sheet's corner marks in amber; a band at the map's foot, where the replay's band stands, on the ground colour
    with nothing showing through (review of 2026-10-09: at 94 % the map's labels read through its words, and through
    the stage's plate, opaque too), holds TOUR · 3 OF 7,
    the chapter's title, decoding as the cue's does, its lines, ← BACK, NEXT →, ESC ENDS THE TOUR and a tick per
    chapter; a stage's plate (the READS / NEVER lists, the two states, the key, the example raid card) stands in the
    middle of the map above it. The buttons stand beside the eyebrow and the title, the lines run under both across the
    band, and a title that doesn't fit beside the buttons goes under them, across the band, wrapping only past that; a
    plate larger than the room above the band is drawn smaller to fit, never cut, and the READS / NEVER lines wrap in
    their columns in a narrow map (review of 2026-10-10, at the smallest window, 900×560: "IT FOLLOWS THE GAME" was cut
    off, the lines took five rows beside the buttons, and the example raid card ran under the band). The owner approved
    both on 2026-10-10, knowing that a plate drawn smaller goes below the type roles' sizes ("Design system", *Type*):
    the German example raid card at 900×560 is drawn at about 60 %, rather than losing its caption (about 70 % while its
    NEXT was cut; the owner approved it a little smaller for the whole sentence on 2026-10-11). Motion: "Design
    system", *Motion*.
  - **How the frames move** (owner, 2026-10-09: "sometimes they transition into nothingness, like from section 3 to
    section 4"; `Rules.TourFrames`). From one set of parts to the next, each new part (the largest first) takes the
    nearest frame on screen and the frame glides to it; a frame left over glides into the new part nearest it, and a
    new part left over grows out of the frame nearest it, so nothing shrinks into a point while there is a part to go
    to. A glide starts from where the frames are drawn at that moment, also mid-glide. With no frame on screen the new
    parts fade in, and a chapter without parts lets the frames fade out under the dim. The dim leaves out the union
    of the frames, so two that cross while they glide stay clear where they overlap (until then the overlap was dimmed
    again, the dim's holes being cut even-odd). Parts that touch are one frame (the three buttons).
  - **Examples, never the player's.** What a chapter stages is drawn from the cache by the app's own code, as What's
    New's previews are: no picture, render or video is bundled (§3), and the map's label says "PREVIEW · THE TOUR ·
    CUSTOMS · AN EXAMPLE, NOT YOUR RAID". The example is the same for everyone: Customs, which every player knows, with
    up to three of its early quests that have places on the map; a cue it shows says "An example, not your raid.".
    The same in every language too: the quests are chosen by tarkov.dev's normalized names, which the data of every
    language shares, never by the names shown (`Tour.ExampleCandidates`; review of 2026-10-10: the map's quests went
    by their names in the data's language, so where fewer than three preferred ones had places German could differ).
    Nothing of the player's changes: no pick, no tick, no setting but the tour's own. NEXT RAID frames the player's own
    rows and lets one preview its map as resting on it does; without rows (no game) it frames the MAP list. When the
    tour ends, the map on screen is back as it was, view and all; at a first start, when the tour opens before any map
    is drawn, it comes back fitted (review of 2026-10-09: it came back at the camera's default, zoom 1 on the map's
    origin, from chapter 7 on).
    A glide a chapter starts stops when its map goes (the same review: Esc during YOUR SCREENSHOT KEY's glide went on
    moving the map that came back).
    While it is up the tour owns the map (owner, 2026-10-15, on What's New's `tour:5` line: "the highlight does weird
    movements and highlights the wrong things sometimes"; `Rules.PreviewOwner`): a preview the pointer asks for counts
    for nothing, one asked for before it opened is dropped, and only a chapter's own stage starts or ends what the map
    shows (*What's New*). Follow my position stands still meanwhile: each chapter frames its example itself, and the
    example's position is nobody's to follow (YOUR SCREENSHOT KEY's, set right after the chapter framed it, glided the
    view off that framing for 2.4 s under a cut-out placed before the glide; `MapView.FollowHeld`). At its end following
    is the player's again before their map comes back, so it comes back as after any preview.
    A chapter frames what it stages above the band with 90 DIP around it, room for the labels the map writes beside
    its symbols (the same review: 40 and 60 cut them at the map's edge, and IN THE RAID, fitted, ran under the band),
    and 150 on the right, the map's label room (owner, 2026-10-09: 90 still cut PICK AND POINT's picked quest there;
    "Map drawing", *Room for labels on the right*). Above the band as it stands in that chapter, its title and lines in
    the language in use, and PICK AND POINT's pointer comes up from just above it (owner, 2026-10-11: the pointer stays
    above the band in any language and window size; at 900×560 the German title takes two lines, and the band covered
    the pointer and the quest it points at: the framing read the band's height before the band was laid out for the
    chapter, the empty band's when the tour opened, and the pointer set off 240 DIP above the map's foot, under that
    band). Where the band leaves little room above it, the 90 gives way, so what is framed keeps at least 90 itself
    (PICK AND POINT's German band at 900×560 leaves 181, and 90 on each side would have left it 1).
    **The framing keeps clear of the label** at the map's top ("PREVIEW · THE TOUR · CUSTOMS" with "AN EXAMPLE, NOT YOUR
    RAID"), as What's New's plate does (QA pass, 2026-10-15: at 900×560 YOUR SCREENSHOT KEY's last cut-out reached the
    label, which, nearly opaque, hid framed extracts, struck-out Railroad Passage and the top of Trailer Park's lit
    triangle, and the cut-out's edge ran through its words): what a chapter stages is framed under the label, 8 DIP and
    a cut-out's corner marks below it, and above the band, the roll call's view too; a cut-out on the map starts there
    and ends at the band's top. The label stays where it is, in the dim unless a chapter cuts out the whole map (PICK
    AND POINT, IN THE RAID), where it is read whole. Of the QA's two ways, the label stepping aside while a chapter
    frames the map's top would have left more room at the smallest window, but the label is what says the map is an
    example once the cue has gone. The roll call's cue keeps its own place above the band and under the label, drawn
    smaller, never cut, where the room between them is too short for it (principle 11).
    IN THE RAID's example card says NEXT as the raid card does: in a few words where the data is English ("Get valuable
    folder"; `Planning.ObjectiveSynopses`), else tarkov.dev's whole sentence, wrapping (owner, 2026-10-11: until then
    it was cut at 34 characters, mid-word, German "Besorge die geschützte Dokumenten…"), and its clock reads 13 MIN
    LEFT of the map's own raid length (Customs: 35 min in tarkov.dev's data of October 2026, so 22 MIN IN). It is
    spaced as the rail's card is, its clock under the name and NEXT and EXIT between hairlines, in the card's own label
    column.
  - **When.** By itself once, at a first start (neither it nor help seen on this PC), when the data is there and
    outside a raid, in help's place; a raid starting closes it, and it comes back at its chapter once the raid is over
    (as help did, `WhileInRaid`). From help's TAKE THE TOUR (its start) and SHOW ME (a chapter), and from a What's New
    line about it (below); never in a raid, these ways too (review of 2026-10-09: they opened it over the raid card):
    TAKE THE TOUR is hidden while a raid loads or runs, and a SHOW ME or a What's New line then opens nothing and says
    "The tour waits until the raid is over.". Ending it, at its last chapter or with Esc, counts it and help as seen,
    and a notice says where it stays ("The tour stays in help: F1, then TAKE THE TOUR."). Never in the website demo;
    "--tour <n>" opens a chapter for a snapshot (`tools\fake-raid.ps1 -PlanOnly -Tour <n>`).
  - **Keys and clicks.** → or Space (or Enter) the next chapter, ← the one before, Esc ends it; every other key does
    nothing while it is up, and a click outside its band does nothing (the window is covered, by the owner's exception
    to principle 3).
  - **Words** live in docs/tour.md, built into the app: per chapter its stage, the parts it frames (their `x:Name`s in
    MainWindow.xaml), its title, at most two lines and its stage's own words. `TourTests` fail when a part it names is
    gone from the window, a stage isn't one the app draws, or a title or line is longer than the band. Its translations
    stand beside it, `docs/tour.<code>.md`, picked by the language in use each time a chapter shows (a switch while
    the tour is up shows its chapter again); one whose chapters, stages, anchors or keyed words aren't the English
    tour's isn't shown, the English one is (`Rules.Tour.InLanguage`, `Rules.BuiltDocs`; §8, "Texts").
  - **Keeping it current** (owner, 2026-10-09). A change to something a chapter shows changes the chapter in the same
    commit. Its drawing is the app's own, so a new symbol, card or cue shows in the tour by itself; what can go stale is
    its words and what it frames, and the test catches the second. A release that changed a chapter says so in What's
    New with a line `tour:N` ("- tour:5 · …"), which opens the tour at that chapter when clicked; pointing at such a line
    previews nothing. Before a release, the chapters are looked at (`fake-raid.ps1 -PlanOnly -Tour 1` … `7`).
  - **First-time lines** (the panel's E). The habit is needed in the raid, long after the tour, so after a first start
    the first raid's RAID LOADING cue carries one line under its kit, "FIRST RAID WITH SHTURMAP · PRESS PRTSC ONCE YOU'RE
    IN" (the key as the game's settings name it), and the first position plotted gets a notice, "That's you, from your
    screenshot's name. Each new one moves you." Each once; a player who used Shturmap before the tour never sees them
    (they are armed only by a first start: `firstRaid`, `firstFix` in shturmap.db), so their words stay true. The line
    keeps clear of the map's buttons, as the roll call's rows and UPDATE READY's line do, and where its two parts don't
    fit on one line it breaks at its "·", each part whole on a line of its own, centred, as the preview label's hint goes
    under its title (owner, 2026-10-15, evening page: at the smallest window it ran under the buttons and lost its last
    word, "IN", and the German one its "MIT", "DRUCK" alone on the next line; `MainWindow.ShowCueLine`, which UPDATE
    READY's line takes too). At 900 × 560 it stands in two lines in English, German and the pseudo-language.
- **Settings** (the gear, Ctrl+,): SETTINGS, then PREFERENCES: "Keep a study log" (developer builds only), "Language"
  (AUTOMATIC, then each language Shturmap is translated into, in its own name: ENGLISH, DEUTSCH; developer builds also
  list the ones still in translation, "FRANÇAIS · IN TRANSLATION", to try them; a choice applies at once, and under it
  one line says what Automatic comes to and why: "Automatic: English, from the game.", "…, from Windows.", or that
  neither the game's nor Windows' language is translated yet; §8, "The app's own language"), "Text size" (NORMAL ·
  LARGER · LARGEST, on top of Windows' text size, which each choice's tooltip names; a choice applies at once; *Text
  size* above), "Delete
  screenshots after reading" (a tick, off unless ticked, with what goes and what stays; §2), "Read the extract list from
  screenshots" (a tick, on unless unticked, with what is looked at and that nothing of the picture is kept or sent;
  §2), "Flea price to copy" (the stash desk's list price: the last low matched, or 1–20 % under or over it, 3 % under
  unless changed; owner, 2026-10-19, from a panel: D2; built in step C, 2026-10-19, as the desk card's control, − and +
  around the rule in amber, "3% UNDER", one state with the card's (owner, 2026-10-19: "UI elements for setting what is
  copied clearly visible"), with the note "Match, or 1–20% under or over the last low. The desk's card has the same
  control."; until then drawn "3 % ▾ · under ▾ · the last low"; *The stash desk*, *The click*), "Crash reports" (ASK AFTER A
  CRASH · ALWAYS SEND · NEVER) with what a crash report holds, "Updates" (AUTOMATIC · TELL ME ONLY · OFF, or "not
  available in this build"; an installed Shturmap whose updater couldn't start says that instead, with where the
  reason is and that the Setup repairs it, `Distribution.NoUpdatesText`); a row's choices wrap under each other beside
  its name where the panel is too narrow for them, and a tick's name wraps; then APP AND DATA: LOG FOLDER, PRIVACY and LICENCES, in an installed build a muted
  UNINSTALL SHTURMAP… with its question; at the foot the version and the kind of build (§8, "Study log",
  "Reports", "Distribution"). It never opens by itself; closing it drops an unanswered uninstall question.
  Split from help on 2026-10-03 (see "Status bar"); the blocks moved as they were.
- **Report dialog** (§8, "Reports"): over the whole window, square, in the card's colours: PROBLEM | IDEA, the text,
  an optional contact, "Include diagnostics" with SHOW WHAT'S SENT, one line on where it goes with a Privacy link,
  CANCEL and SEND. It fits the window: in a short one what's sent and the text box give way, each down to a few lines
  of its own scrolling, and past that the dialog scrolls, so CANCEL and SEND stay in reach (review of 2026-10-10: at
  900×560, with what's sent shown, they were below the window's edge).
- **After a crash**: one question under the notices, until answered: SEND · DON'T SEND · ALWAYS SEND · WHAT'S SENT;
  closing it asks again at the next start; once sent, ADD A NOTE opens the Report dialog. It lies over the map and
  waits for a click, so it is shown outside raids only: while a raid loads or runs it steps aside, unanswered, and
  is back when the raid is over (`WhileInRaid`; review of 2026-10-04, H9).

### Map drawing

Rules from a cartography review of the map (2026-10-02; the owner approved its twelve changes, with the change to
spawns below).

- **The text size** (owner, 2026-10-19; "Screen anatomy", *Text size*). The map draws its labels and symbols through
  one factor, `MapRenderer.Render(…, uiScale)`: the screen's pixels per DIP times the text size, Windows' text size
  times settings' step (`TextScale.Effective`; `MapView.UiScale`), so its names, distances, symbols and scale bar grow
  as the window's text does, by the factor (not WinUI's curve). WinUI's own text scaling doesn't reach a swap chain:
  until then the map stayed at 100 %. The labels' room, the repel, the hit test, the gap between two labels
  (*Labels by priority*) and a framing's label room share the factor, and so does the label room's cap, a fifth of the
  view (*Room for labels on the right*); a change redraws at once. At 150 % the symbols keep their places, a little
  more crowded.

- **Four levels, the rule for every symbol.** A reader takes in the map in order of importance; each level differs
  from the next in size, contrast or colour, and there are few things at the top.

  | level | members | treatment |
  | --- | --- | --- |
  | 1 | the player; the picked quests, the guide line and plate; the raid replay's pen while it plays | own hues (sand, the picks' colours), rings, drawn last, never faded |
  | 2 | quest objectives; extracts and transits for your side | amber discs with glyphs (10 px); 15 px triangles and diamonds; labels in ink; the extracts on your list this raid lit, their names in bold |
  | 3 | boss, sniper and Scav zones | one marker per zone; Scav zones unlabelled; bosses hollow at 70 % and named once, at their likeliest zone, until they matter (2026-10-17) |
  | 4 | the artwork, its names, the sheet grid; locks, switches, hazards, container dots | receded; names, locks and switches thinned with zoom |

  Extracts and transits are as large as the boss marker (15 px across), no longer smaller. A new symbol gets a
  level, a shape no other symbol uses, a colour from the palette (no new hues) and the collar.
  Where symbols share a place, the one that matters more lies on top: markers are drawn in the order of their
  labels' priority, least first (locks and switches, then Scav and sniper zones, ways out, quests, bosses, then
  what is selected). A Scav zone's ring used to be drawn over the boss's octagon of the same spawn zone, which
  then read as a red ring (the review of 2026-10-04, C6).
  Symbols that would cover each other are **set apart, on leaders** (below, "Symbols that cover each other"); the
  receded ones, locks, switches and Scav zones' rings, only **side by side** with their own rank
  (`MapRenderer.SideBySide`; the same review: on Streets a transit's diamond lay over an extract's triangle at Scav
  Checkpoint, and one quest's disc over another's; until 2026-10-06 every symbol was set side by side). Each of such
  a pair is moved by half of what is missing for both to show, their collars touching, and never further than its
  own width, along the line between their true places, left and right where they share one, the earlier in the data
  on the left; so zooming in draws them apart and each comes back to its own. Places of one objective still merge
  into one marker with their count. Everything follows the symbol where it is drawn: the pointer finds it there,
  its badges and its label stand by it, the guide line ends on it, and a place counts as in view for the edge
  chevrons where its symbol is. The distance on the guide's plate stays the place's own. A symbol set off its place
  may put its name a line further down than "below", under its neighbour's: two names read as a pair's caption
  ("Scav Checkpoint" over "Transit to The Lab (Dark)").

- **Symbols that cover each other** (owner, 2026-10-05: "Can there something be done about overlapping icons … Especially
  for selected Quest icons"; chosen from a panel of six ways drawn by the real map on three Streets spots: "Go for F").
  - **Nothing grows.** A pick and whatever is pointed at keep their rest size; colour, the pick's ring and the
    pointer's pulse say it, and the ring and the pulse lie under every symbol (`MapRenderer.DrawUnderlay`). Before, a
    pick grew to 14 px with a ring reaching 7.5 px further and a pointed-at quest to 12 px, over neighbours set
    side by side for 10, so the symbol that mattered most covered the ones around it. Symbols are set apart (below)
    by the sizes they are drawn at.
  - **Set apart, on leaders, without a click** (owner, 2026-10-06: "learning from the ggrepel ggplot package.
    Whenever there is overplotting going on, it automatically draws a line between the datapoint and its annotation …
    it should not be the case that I need to interact with the map with the mouse in order to see the spots
    properly"; chosen from a panel of five ways drawn by the real map on four Streets spots: "Overlap: D";
    `MapRenderer.Repel`). Quests, bosses, extracts and transits that would cover each other are pushed clear of each
    other and pulled back toward their places, as ggrepel sets labels apart: where ranks differ, the lesser one
    yields and the one that matters more keeps its place (bosses, quests, ways out); of one rank, both yield alike.
    No symbol may stand on another's place either (ggrepel's point padding, 7 px beyond its own half width), so where
    two share a spot both step off it. A symbol that has to leave its place stands at least 10 px beyond its edge
    from it (and never further than 120 DIP: below, *Settled once*) and is joined to it by a **leader**: a hairline in
    the symbol's colour on a dark band, with a dot on a
    dark rim where it belongs (the opened stack's hairline of 2026-10-05, now always there). One only nudged (by
    less than about half its width) keeps to its place without a leader, and as the view zooms in and the places
    draw apart, each symbol comes back to its own and its leader goes. A corner badge (floor, count, OPT, "?")
    counts in its symbol's width, 3 px, so no neighbour is pushed under it; the floor arrow 6 px since its disc grew
    (2026-10-16, "Visual language": 3.1 px further along the diagonal). Leaders lie under every symbol and step back
    with their symbols while something else is pointed at; no label covers a leader or its dot; pointing at the
    dot points at its symbol. **The player is a symbol too** (owner, 2026-10-06: "keep in mind that the player icon is
    also an icon - if you are right next to a objective the player icon is drawn on top of the quest icon"): level 1,
    it never moves, and no symbol stands under its ring or, once the position is a minute old, its age tag; one beside
    you steps off on a leader to its place, as from any symbol that matters more (`MapRenderer.PlayerObstacles`). The
    facing cone and the glow are see-through and hide nothing. Positions turn only on the places, where the player
    is, the zoom and the symbols' sizes and ranks at rest (and while the view moves, on where they stood the frame
    before: below), never on picks or the pointer, so nothing moves when something is picked or pointed at, and a new
    position moves only what stood in its way. Locks, switches and Scav zones' rings take no part (their leaders were a
    web of ink between the quests in the panel): they stand side by side among themselves, under the rest. A pan moves
    every place alike, so the repel's offsets are kept while it is given the same symbols the same distances apart
    (`MapScene.LastRepel`; a pick, the pointer or a list read gives it nothing new): it is most of a layout's work (3
    to 6 ms for a 1600 × 1000 view of Streets with every quest active), and a pan lays the map out for every frame.
  - **Steady while the view moves** (owner, 2026-10-15: "weird movements" in the tour's 1.2 s view move, every Follow
    glide and zoom; `MapRenderer.SetApart`, `Repel`). Laid out afresh at each frame's zoom, a crowd settled one way at
    one zoom and another way at the next: the repel's passes swing back and forth in a crowd and never come to rest, and
    the search for a free spot sent a wedged symbol far off. While the view only moves (from one frame to the next a
    pan, or less than a tenth more or less zoom, with the same position: a glide, a pinch, a pan that brings symbols
    into view or takes them out of it), the repel goes on from where it left off, one pass a frame, and each symbol is
    drawn at most 2 px a frame nearer to where that puts it (`RepelStep`), so it keeps its side of its place and a
    leader comes or goes over a few frames instead of in one jump. One on a leader keeps it until the passes bring it
    back within three quarters of the nudge that sends one off (`LeaderKept`), and the leader line itself comes 2.5 px
    past the distance at which a symbol clears its place's dot and goes 2.5 px short of it, so neither comes and goes
    from frame to frame; while the view zooms out, the line stays as long as the passes still have its symbol off its
    place, though the crowd push it back over its dot for a few frames. A symbol that comes into view or out of a
    parting cluster makes way for those already standing, and only such a one is searched a free spot for; the
    push-only passes keep symbols off each other and the player within the same 2 px a frame, and past them only so far
    as keeps their bodies apart (their collars may meet for a few frames), and off each other's places only through the
    passes the symbols follow. A wheel's notch is such a move since 2026-10-15 (owner: it zooms over
    0.15 s, below, *The wheel zooms over 0.15 s*); until then it zoomed by a fifth at once and laid the crowd out afresh,
    which could flip a symbol to the other side of its place. So are + and −, the zoom buttons and a double-click since
    the evening of 2026-10-15 (owner: over 0.2 s, below, *The keys, the buttons and a double-click zoom over 0.2 s*).
    Anything else (the map opened, a zoom at once with Windows' animation effects off: a double-click, + or −, a notch; a
    frame that zooms by a tenth or more; a new position, other quests) lays them out afresh, as a still view always
    shows them; once the view is still nothing moves. After a
    glide a crowd can stand otherwise than a fresh layout of the same view would have it, until the next fresh one. In
    a 1.2 s glide on Customs at 60 frames a second (`shturmap-cli frames customs --scene list-read --glide 1200 --every
    16`), symbols set off their places by more than 3 px more or less from one frame to the next went from 466 to 3
    (all where a cluster parted), the largest from 120 px to 4 px, and leaders that came or went from 61 (12 symbols
    more than once) to 7 (none twice); in What's New's extracts preview, Boiler Room's 17 px and a Sniper zone's 15 px
    snap as their leaders went became slides of 2 px a frame. A glide's frame costs less than before: the repel's
    median 0.2 ms against 0.9 ms afresh. **At the smallest window** (QA pass 3, 2026-10-15) the whole map is about
    500 × 481 px, and a zoom out packs a crowd tighter than its symbols fit (the roll call's on Customs with every
    quest: about 100 symbols, the view zooming out by up to 7.7 % a frame at 30 frames a second). There the search for a
    free spot, which then ran for every symbol still covering another, sent ones that only touched a neighbour's collar
    up to 144 px across the crowd and back, and the push-only passes moved standing symbols up to 9.6 px a frame: hence
    the newcomers-only search and the 2 px for pushes too, above. In `shturmap-cli frames customs --scene list-callout
    --viewport 500x481` (30 frames a second) symbols set off their places by more than 3 px from one frame to the next
    went from 235 (the largest 143.8 px) to 127 (6.0 px, all where a body would otherwise have covered another's or a
    cluster merged), and leaders that came or went from 41 (8 symbols more than once) to 22 (none twice), with no body
    covering another in any frame; at 60 frames a second, as the window draws it, from 117 (125 px) to 1 (3.1 px, where
    a cluster merged). The 1.2 s glide at that size (`--glide 1200 --every 16 --count 76`) went from 39 to 9 (5.8 px at
    most) and the wheel's notches (`--notch`) from 22 to 3; at 1600 × 1000 the roll call, the glide and the notches have
    none (3 each in the glide and the notches before), and the trace says how each frame placed the symbols (afresh,
    going on from the frame before, or as before). Still open at the smallest window: in a zoom in, a few leaders come
    and go again within one move (9 symbols in that glide, 4 in the notches). The repel's median in the roll call's
    zoom out there stays about 1.2 ms a frame; its slowest frame went from 44.7 ms to 5.5 ms. The scale bar keeps its
    place by the same rule while the view moves, and
    settles once it is still (*Distances on the map*); so does a picked or pointed-at name its side (*Room for labels
    on the right*).
  - **Settled once, and never further than 120 DIP from its place** (owner, 2026-10-17, with a screenshot of Woods:
    "There is a bug when two items are close to each other and their repel-lines are parallel they get expanded every
    time when the user pans or zooms the camera"; `MapRenderer.RepelKeys`, `LeaderMost`). While the view moves, each
    symbol goes on from its own last placement, found by its marker's id and kind (and, where even both are another's,
    by its turn among them). tarkov.dev gives two extracts one id (Woods' two Outskirts, a PMC's and a Scav's on one
    spot; its two UN Roadblocks), and found by the id alone, both went on from the first one's placement at every frame
    of a move: they pushed each other out along one line, up to 7.4 px a frame, and never settled, and after a few
    glides one stood across the view on a leader (`shturmap-cli frames woods --scene plan --sway 4 --toward
    Outskirts`: 1434 px off its place after the first time back and forth, 5699 px after the fourth; now 21 px each
    time). Symbols on one spot settle once: from the second time on, the same moves draw them at the same offsets,
    however often they are made (`SharedSpotFramesTests`). And no symbol stands further than 120 DIP from its place,
    centre to place (`LeaderMost`: twelve times `LeaderLeast`, about five times the least a symbol that leaves its place
    stands off it, 21 to 23 DIP), afresh or going on, whatever pushed it or wherever the search for a free spot would
    send it: a fresh layout of the whole of Customs, the busiest map, at 1600 × 1000 sets none further than 110 px, so
    where the view has room for its crowd the bound holds nothing back; where the view packs a crowd tighter than its
    symbols fit (the whole map at the smallest window), the search sent a symbol coming into view up to 360 px across
    it. The passes hold every symbol within the bound after each pass, and the search looks no further; where it finds
    no free spot within it, what still covers another is pushed only off its neighbours' bodies, within the bound (their
    collars may meet), as a move's frames do. In `shturmap-cli frames customs --scene plan --sway 2 --viewport 500x481`
    symbols stood further than 120 px off their places in 1249 symbol-frames before and in none after, the farthest
    360.8 px; set off their places by more than 3 px from one frame to the next 107 times before, 88 after (the largest
    4.3 px before, 5.9 after); no body over another in either. Customs' glide at 1600 × 1000 and its roll call at the
    smallest window (`--scene list-callout --viewport 500x481`) are unchanged.
  - Until 2026-10-06 **a stack opened where the pointer rested** (`MapScene.FanAt`): resting 0.35 s on a symbol
    another lay well over put every symbol touching it on a ring around its place, on a dark plate, each on a
    hairline to its own place. It needed the mouse, so it went with the leaders, which leave nothing stacked.
  - Also drawn and rejected (the panels, `shturmap-cli overlap-panel` and `repel-panel`, removed with the choices):
    the upper symbol faint where it lies over another (muddy ghosts of glyphs); neighbours stepping aside for a pick
    or what is pointed at (symbols moving whenever the pointer moves); repelled with only the lesser symbol leaving
    (its dot often lay under the symbol that stayed); every place kept in sight but with leaders as short as the
    room allows (stubs between symbols, no lines to follow); and the leaders in ink rather than the symbol's colour.
- **The player's extracts this raid** (owner, 2026-10-05: "update the map accordingly, highlighting the open exfils").
  Once a screenshot showed the game's extract list (§2), three steps:
  - **On the list: lit** (owner, 2026-10-05, the same evening: "The exits that are verified should be highlighted
    better on the map so it can be seen that they are active"; `MapScene.ExitsListed`). A steady glow in the
    extract's own colour around its solid triangle, about 2.8 times its size across, strongest near the symbol and
    gone at its edge, drawn under every symbol so it covers none; its name in the bold label face, in ink, placed with
    the bosses' names ahead of other ways out and quests. A glow because the other ways to say it are taken: a ring is
    a pick's, a pulse the pointer's, a new hue isn't allowed. Once lit it never moves (principle 2). Until then the listed
    extracts were the solid triangles they always were, found only by the others being hollow, and didn't read as
    "active". **The bold comes with the lighting** (owner, 2026-10-15): a list just read lights each extract at its turn
    (below), and until then its name is in the regular face, as a not-yet-lit extract's, on the side its bold name
    will take; that name's room is kept from the first frame, so no label moves as one lights (`PlacedLabel.Unlit`).
    Until 2026-10-15 every listed name was bold from the list's first frame, before its extract was lit.
  - **On the list but marked "??:??:??"** (it may be closed, or it needs something): solid, not lit, with a dark "?"
    in its triangle, as large as the triangle holds and in the boldest Bahnschrift, as a quest's glyph is dark on its
    disc: the "?" a possible location carries, maybe here, maybe open (`MapScene.ExitsUnsure`). On a shared extract
    the split stops above it. Until 2026-10-14 it was the possible location's badge at the lower left, 11 px, which
    went unseen (owner: "Also mark exits that have a ? better"); inside, it needs no room beside the symbol either.
  - **Left out: struck out** (owner, 2026-10-14: "Exits that are marked as not open should be marked better as
    such"): its outline on the collar's dark, and a stroke through it from upper left to lower right, a little past
    the triangle on a dark band, the sign for "not available" everywhere; both in its colour at 65 %, so they step
    back beside the solid ones. Its name in muted instead of ink, placed after the quests' and the other ways out's,
    with the Scav and sniper zones' (`MapScene.ExitsNotListed`). Until then it was the outline alone, at full
    strength and as large as the others, and at a glance still read as a way out. Nothing is hidden: a struck-out
    extract is still a place on the map, and the list could have been read wrong. Its size stays: the repel sets
    symbols apart by their sizes, and a list read would move them.
  **A list just read is called out: the roll call, then the fly-out** (owner, 2026-10-15: "a list of the extracts
  appears in the middle of the screen along with their icons. Then, the icons move where extracts are (also outside
  of the current view pane) so it contextualizes them and the player knows where they are"; from a panel of four,
  2026-10-15: A; §1's fourth exception; `MapScene.CallOutExits`, `MapRenderer.DrawExitsLit`). It takes the place of
  the ring of 2026-10-14 (owner: "animate the icons for a couple of seconds so they are visible pre-attentively when
  the exits are read"), which each extract still gets as it lands. From the moment the list is read:
  - **0 s:** the YOUR EXTRACTS THIS RAID cue (principle 11) opens across the middle of the map column, its title
    decoding letter by letter and its gold rule shooting out. **From 0.5 s** the rows of the listed extracts rise in,
    0.1 s apart, nearest you first, lit and "??:??:??" alike (where there is a position, else in the data's order;
    the ring took the lit ones first). A row of an extract a switch gates carries the map's switch badge before its
    name, 4 DIP after its triangle (owner, 2026-10-17; *Switches*). The extracts the list leaves out are struck out
    at once, get no row and don't move: motion goes to where the player can leave.
  - **1.2 s:** the view moves over 1.2 s, eased in and out (`MapView.AnimateView`), to frame every listed extract and
    you, with the label room on the right (*Room for labels on the right*); those that were out of view come into it.
    A Follow glide under way stops for it: the screenshot's own position is in the frame.
  - **1.6 s:** each triangle lifts off its row, 0.4 s after the one before and in the same order, and flies 1.5 s on
    an arc, eased in and out, to its extract's symbol where the frame draws it (on its leader where it is set apart),
    so it follows the view as the view settles. Its row fades as it leaves, and the band's dark backing goes with the
    rows, so no landing near the middle is under it (as the panel drew it, four of seven landed under the band). Until
    its triangle lands an extract is drawn as before the list, solid and unlit, its name in the regular face. Landing,
    it lights up, its name turns bold (owner, 2026-10-15), and it rings out: a lit one's glow swells in past its
    strength and settles over a second, and its triangle's own outline leaves it
    three times, 0.5 s apart, each over 1.1 s, in its colour on a dark band, under every symbol; a "??:??:??" one's
    "?" pops in at a size and a half and settles, with one echo. With six extracts the last lands at 5.1 s. Until
    2026-10-16 each flew 0.9 s, near or far, 0.16 s after the one before (the rings' turn): in `shturmap-cli frames
    customs --scene list-callout` (a 1600 × 1000 view) the farthest crossed 575 px at about 1,800 px/s at its peak (now
    about 1,100), and all eight were in the air within 1.1 s (owner, 2026-10-16: "The animation that 'flies' the exits
    to their position is too fast"). Of the three ways offered, flights paced by their distance and one icon at a time
    were not taken.
  - **After the last landing** the title stays on its halo 1.5 s more (6.6 s with six), then fades; the rings end at
    about 7.2 s (a lit extract rings 2.1 s from its landing, `MapRenderer.RingLength`).

  The echo is the triangle, not a ring: round rings that leave a symbol are the pointer's pulse, the player's ping
  and a completed quest's places; this shape says "way out". Only a list from a screenshot taken in the last 30 s is
  called out (as a position pings: not one found as the logs are read back at start), once per list (not again when
  the map comes back after another). **Players take several screenshots in a run** (owner, 2026-10-14: "updated on
  the fly with the newly added items that come in after the first animation has been started"): a list that comes
  while the cue is up adds what it lit or marked anew as rows after the others, the title counting them all, and they
  fly after the last; where one of them lies outside the view the roll call framed, the view frames every listed
  extract and you again, with the same 1.2 s move (owner, confirmed 2026-10-15). One that comes after the cue has gone
  rings what it lit or marked anew where they stand, 0.16 s apart, nearest you first, each name turning bold at its
  turn, with no cue and no change of view; the same list again changes nothing. (Until 2026-10-15 a list that came
  after the rings were over rang them all again.) With animation effects off nothing moves: the cue stands 5 s with
  all its rows, the view takes the extracts in at once, and they are simply lit, their names bold. **The cue takes
  the place of the one-line notice** "Your extract list is read: 6 extracts for this raid" (owner, confirmed
  2026-10-15), as QUEST COMPLETE and SEEN IN YOUR STASH took the place of theirs; the extract list's own line ("Your
  list this raid: 6 of 12 extracts …", "Screen anatomy") still says it, also for a list the cue doesn't call out.
  What's New's `extracts` preview and the tour call their example list out the same way. Of the panel's other ways,
  badges at the map's edge for the extracts out of view, with no change of view (B), the fly-out and then a glide
  back (C), and the cue alone with the ring of 2026-10-14 (D) were drawn and not taken: A alone does both halves of
  the ask, changes the view once and while the cue says why, and leaves the view a player at the spawn plans the route
  on.
  The roll call's map side is `MapScene.CallOutExits`: each called extract's icon flies from its row in the band to
  its place and lands as its ring starts (`MapRenderer.FlightAt`; the view zooms out to `Camera.FittingAll`). The
  window's side is `MainWindow.RollCall`: it hands each row over as the centre of its triangle in the map panel's
  pixels (`ExitCall`), at the cue's start, and moves the view with `MapView.CallOutView` (a zoom out only, the current
  zoom as the fit's closest; above the tour's band in the tour). Once a roll call ran in a raid, a later list after the
  cue rings only what it lit or marked anew (`MapScene.RingExits`). What the roll call called is kept by the window for
  the raid and its map (`Rules.RollCallRecord`: the raid's map, its id and start), not on the map's scene: a look at
  another map in the raid and back makes the scene anew, and the scene's record (`MapScene.CalledOut` until
  2026-10-15) went with it, so the next list got a whole roll call again (coordinator, 2026-10-15). A new raid, or a
  transit to another map, starts without one.
  Transits are drawn as ever. Until a list was read, and outside raids, every extract is solid. The legend has a row
  for the lit ones and one for the other two, each shown once a list was read (one each would put Transit past help's
  twelve rows).
- **The view leaves the player** (2026-10-15, with the roll call above; owner, confirmed 2026-10-15). The view the roll
  call ends on, every listed extract and you, is the one a player at the spawn plans the route on, so nothing takes it
  back when the cue goes. With Follow my position on, the next position glides it onto the player as any new position
  does (2.4 s, at the zoom the view then has; "Screen anatomy", *Follow my position*). With Follow off, principle 8
  holds, a position never moves the view: it stays as the roll call left it until the player moves it (a drag, a zoom,
  F or the show-my-position button). A position that comes while the view is moving for the roll call (its 1.2 s; a run of
  screenshots) pings where it is and lets the move finish; the one after it glides (2026-10-15, as built: the two
  pulled the view two ways at once, `MapView.Refresh`). The view is a zoom out only, and where every listed extract
  and the position are in view already, with the padding and the label room a fit keeps, it doesn't move at all, and a
  Follow glide under way goes on (coordinator, 2026-10-15, from the roll call's build: a fit that zoomed out nothing still
  took them into the view's middle, which on Customs cut off the map's west and left empty map at the top;
  `Camera.CallOut`).
- **Stepping back while a quest is highlighted** (owner, 2026-10-03: at 28 % the other markers "can be barely made
  out anymore, but are still pretty important", "especially relevant in the raid view"; `MapRenderer.StepBackOf`).
  What is pointed at, and the picks, stay at full strength; the rest steps back by kind, never out of sight, and less
  in a raid. Picks alone step nothing back (owner, 2026-10-03, "Picks"): only pointing does, for as long as it lasts,
  and a trader's held focus (owner, 2026-10-17; "Screen anatomy", *The trader strip*), whose quests stand in for what
  is pointed at while nothing is, in a raid too, without the pulse, in a set's static look (`MapScene.Held`).
  **A linked set steps the rest back further** (owner, 2026-10-17, from a panel of four: "B"; "Design system",
  *Motion*): one thing pointed at (a quest, an objective, an extract) keeps the first two columns; a set lit by the
  pointer (a trader's portrait, a boss's name, an item) takes the breathing measure while it breathes and the static
  one once it stops (held, the pointer still 6 s, Windows' animation effects off, a snapshot), eased between over
  0.18 s as the breath settles (`MapRenderer.StepBackOf(inRaid, settle)`, `MeasuresAt`). Every kind that steps back at
  all takes the same two measures; the ways out and the bosses keep theirs. Never out of sight still holds at 40 %, and
  the labels' 30 % stays above the 28 % the owner found too dark. The rail's rows step back only under a held focus,
  by the labels' static measure, 30 % (`TraderFocus.SteppedBack`).

  | kind | planning | in a raid | a set, breathing: planning, raid | a set, static: planning, raid |
  | --- | --- | --- | --- | --- |
  | your side's extracts and transits | full strength (labels 70 %) | full strength | as one thing | as one thing |
  | bosses: hollow at 70 % at rest, solid while lit (*Spawns*, below) | no further (labels 70 %) | no further | as one thing | as one thing |
  | other quests' markers and their zones | 62 % (labels 45 %) | 80 % (labels 60 %) | 55 % (40 %), 70 % (50 %) | 40 % (30 %), 60 % (45 %) |
  | Scav and sniper zones, locks and switches, hazard areas | 60 % (labels 50 %) | 75 % (labels 60 %) | 55 % (40 %), 70 % (50 %) | 40 % (30 %), 60 % (45 %) |

  A zone steps back as far as its kind's markers, and a picked quest's doors (the locks of the keys it needs) not at
  all, like the pick they belong to (`MapRenderer.ZoneStrength`, `StepBackOf`). A boss rests stepped back already
  (owner, 2026-10-17): something else pointed at leaves it as it is, and the bosses a pointed-at or picked quest
  targets are lit with it, at full strength. Until the review of 2026-10-04 other
  quests' zones fell to about 35 % whatever this table said, hazard areas to about 55 % (they weren't in it), and a
  pick's doors stepped back like any lock.
  Labels step back further than symbols, so the highlighted quest's names stand out without hiding where everything
  else is. Stepping back by colour (greying) was compared and dropped: a greyed quest marker reads as done (grey
  means done or after the raid). The candidates (all at 60 %, greyed, by kind) were
  compared side by side on Customs and Streets, in a raid and in Plan.

- **Optional objectives** (owner, 2026-10-03: they "might still be very relevant for a quest"). tarkov.dev marks 60
  of 1,418 objectives optional; 45 have places on a map (most on Customs, 15, and Streets, 11: Abandoned Cargo's
  seven cargos, Pyramid Scheme's ten). Their markers carry a small "OPT" badge at the upper left, in the badge style
  of the count (dark plate, the marker's colour), and the quest card and the raid card say "(optional)" after the
  objective. Words, because no shape, colour or ring is free to mean "optional": grey is done, a dashed ring is an
  old position, and a dotted ring, also tried, read like either (the hollow ring was a possible place until
  2026-10-04, see "Possible places" below). A label
  suffix was tried too; it disappears with the label.

- **Landmarks from tarkov.dev's data** (owner, 2026-10-03: "Do B and A"; B, landmarks, came with the question of
  backgrounds for The Lab, Labyrinth and Icebreaker, and holds on every map). All level 4, all real positions from
  the maps payload, nothing inferred:
  - **Locks**: a door's or a car trunk's (both need a key): a padlock (Segoe Fluent `E72E`) on the dark collar, no
    plate, labelled with the key's short name as printed on the key ("TGL MO", "Dorm 114", "RB-PKPM"; the full name
    when it has none) and "needs power" where the data says so. The key glyph `E8D7` stays the key itself (BRING,
    need cells); the padlock is where it opens. Pointing at a key (its BRING row, its row on a quest card, its
    need cell beside a quest's name, the gold "Key: …" line under a raid objective that names just that key) lights
    every lock it opens, at any zoom, and shows where it lies loose; pointing at a padlock opens its key's card.
    Leaving a need cell or the gold line returns to the quest's row around it ("Visual language", linked
    highlight; the review of 2026-10-04: before, a linked element inside a linked row lost the row's highlight
    when the pointer left it, so these didn't link). The key's item card still lights nothing by itself.
    Container locks would mark containers; the data has none.
    A quest brings its doors along (owner, 2026-10-03: "For the Golden Swag key the trailer park portable cabin
    marker is not highlighted when the quest is highlighted as goal quest"): pointing at a quest, or picking it,
    lights the locks of the keys it needs on that map (its `neededKeys` there and its objectives' own keys, the
    sources BRING's "key for …" reads; `MapContent.QuestKeys`), at any zoom and with their names. A picked quest's
    doors take its pick's colour (the first pick's, where several need the key), without the picks' ring, so they
    read as part of the pick. They are a means, not a
    goal: the guide line and NEXT still lead to the quest's own places, never to a door, however near.
  - **Switches**: a power symbol (`E7E8`) with the switch's name ("Med Elevator Power Button", "Alarm Switch",
    "Fire Trap Switch"); a name the data leaves untranslated is "Switch". The Lab has 15, Labyrinth 12. **An extract
    and its switches light together** (owner, 2026-10-03, from the map audit: you have to find the switch to leave;
    2026-10-17, from a panel of three: "A", a badge on the exit and the link on pointing). The steps are the hand-kept
    table's ("Extract requirements": tarkov.dev's own `switches` tell no extract apart, the verification of
    2026-10-17), else the data's `switches` where they tell an extract apart, with a switch that unlocks one of those
    (a power switch freeing a lever, up to four steps through the data's "activates"; not one that locks it). A switch
    the data lists on most of a map's extracts (more than half, `SwitchExits.OnMostExtracts`) tells none of them
    apart and links nothing; "most", not "every": with one extract put right in the data, a strict "every" would
    have lit the lever for all the others (the review of 2026-10-04). Until 2026-10-17 that was the only source, and
    on every map it gave nothing (Customs' 28 extracts all list one lever, The Lab's 7 the Med Elevator's three
    buttons), so no extract linked.
    - **The switch badge**: an extract a switch gates carries a small dark disc at its lower left, as large as the
      floor arrow's and with its rim (⌀ 14 DIP, a 1.5 rim in the extract's colour), holding the power symbol in the
      extract's colour: leaving there takes a switch first. Drawn at rest, at every zoom; faint with its extract
      where the list leaves it out (`MapRenderer.DrawSwitchBadge`, `MapMarker.SwitchSteps`). The corners of an
      extract are then: the floor arrow upper right, the switch badge lower left (a quest marker's lower left is the
      "?", which an extract never has). The repel counts it as it counts the floor arrow (6 DIP), and names keep
      3 DIP off it as off the floor arrow, but a side within its margin moves out past it whether or not it reaches
      over the badge itself: the badge reaches into the sides left of and below the extract, and in The Lab, where the
      floor arrow takes every gated extract's right, the name would have had no side left (`MapRenderer.OffBadges`).
    - **Pointing at a gated extract** (its symbol, its row in the Raid rail, its requirement line) lights every step,
      a switch or the Saferoom's keycard lock, in the extract's colour, at any zoom (below 1.5 times the overview,
      where switches aren't drawn at rest, too) and on any floor with its floor arrow, each named for it
      ("Mall Main Power Switch · Saferoom Exfil", `ShownMarker.StepOf`); the picks' dashed guide line, in the
      extract's colour, runs from it to the first step with their distance on its plate (`MapLayout.ExitGuide`; the
      picks' own guide stays). The rest steps back by kind, as for any linked highlight, and leaving puts it back.
      Pointing at a switch lights the extract it opens (and the guide where it is the first step). The steps' names
      come after the extract's and give way where they find no free place, as an unlit name does: at the Saferoom
      the last two steps stand within 5 m of the door, and on its right they covered one another and its name. The
      roll call's rows aren't pointed at: the cue lets the pointer through to the map. Links and badges come from
      `MapContent` (`MapContentBuilder.ExtractSwitchLinks`, `SwitchSteps`), added to the focus in `MainWindow.MapFocus`.
      The legend has a row for the badge on a map with a gated extract.
  - **When they show**: on a map with artwork from 1.5 times the overview (as the shops' names; Streets has 63 locks,
    Customs 36, Reserve 34), on a sheet at any zoom; their labels from 2.5 times or when pointed at; only those on
    the floor shown (The Lab's other floors would cover the sheet with floor arrows), except what is pointed at.
    Their labels come last in the label order.
  - **Hazards**: tarkov.dev's "hazard" outlines (Labyrinth's 18 traps, 2–27 m²) as a hatched ink outline, an area
    style nothing else has. The hatch is clipped to the outline, and only its lines that cross the part in view are
    drawn, at the places they have in the whole area (`MapRenderer.HatchLines`): zoomed in, a border zone's box is
    thousands of pixels wide, and every line across all of it was drawn for each frame (the review of 2026-10-04). Labyrinth's 19th "hazard", 54 × 58 m below the central hall's floor, is left out: the data
    doesn't say what it is, and hatching it would cover the hall (trap-sized means up to 50 m²). **Minefields**
    (owner, 2026-10-03: "Yes, draw the minefields as hazard areas"): tarkov.dev's "minefield" outlines in the same
    hatched style, whatever their size, wherever the map's picture doesn't draw them itself. The artwork of Woods,
    Shoreline, Lighthouse, Streets and Terminal has a group whose id starts with "mine" (`MapArtwork.ShowsMinefields`),
    so the data's outlines would only double them there; Customs (2), Reserve (1), Interchange (7) and Ground Zero (5)
    get them from the data. **Border-sniper zones** (owner, 2026-10-03, from the map audit): tarkov.dev's "sniper"
    hazards are the kill zones where the map's border snipers shoot anyone who walks in, not sniper-Scav spawns. All
    107 are named "ScavRole/Marksman", five of their eight maps have no sniper Scavs at all, and they lie at the
    edges. They get the same hatch (so the hatch means "this area kills you": traps, minefields, border snipers),
    named "SNIPER ZONE" from 1.5 times the overview, once per group of neighbouring zones. Where the artwork draws
    them (a "danger" group named "Sniper": Customs, Ground Zero, Streets; "Danger": Interchange, whose strips the
    data's zones cover exactly; `MapArtwork.ShowsSniperZones`) they are left to it; Woods (39), Lighthouse (13),
    Shoreline (10) and Reserve (3) get them from the data. **Kept to the drawn map** (owner, 2026-10-03: "What are the
    big white rectangles at the bottom of Customs, looks odd"): over artwork a hazard shows only where the picture
    draws something (ground, water, buildings), not over the empty space around the map, where many of the data's
    outlines run on. The SVGs have no background, so `MapArtwork.Ground` is the base picture's alpha, drawn once when
    the artwork loads (2048 px on the longer side) and laid under the hatch's ink as a shader placed like the artwork,
    so a frame costs nothing more. Customs' two minefields and Reserve's minefield and sniper zones lie wholly past the
    drawn map, so nothing of them shows; Woods' and Interchange's end at the map's edge. A zone is named only where its
    centre is on the drawn map. Tiles and sheets keep hazards whole (Labyrinth's traps lie inside its render). The
    hatch is quiet so the artwork reads through it: 0.8 px lines 5 px apart at 31 % ink, the outline 1 px at 47 %.
  - **Containers** on a sheet only: faint ink dots where loot containers stand, on the floor shown (The Lab has 319,
    Labyrinth 35), so rooms and corridors show from real points where no artwork draws them. On artwork they would
    be clutter (Streets has 1,282).
  - `shturmap-cli render <map> <out.png> --focus-item <key>` draws a map as pointing at a key does.

- **Spawns: one marker per zone, at its centroid** (owner, 2026-10-02: the player needs to know which area has
  Scavs, where the bosses and the snipers are, not each spawn point; "just use the centroid, do not colour code the
  potential spawn area"). tarkov.dev names a zone for each spawn point; the points of one zone get one marker at
  their centroid, the mean of X, Y and Z. A marker must stand among its points, within 25 m across and 3 m (about a
  floor) in height of one of them: where a zone's centroid doesn't, the zone is cut in two at its widest gap, until
  every group's does, and each group gets its marker (owner, 2026-10-02: on Customs and Interchange some zones spread
  over 300–450 m and their centroid landed 40–250 m from the nearest point; Customs' sniper marker stood on the
  ground between its two points, 228 m apart on the 2nd and 4th floors). Split only where needed, Woods keeps 28
  Scav markers for its 11 zones and Customs 24 (PvE). `shturmap-cli spawns [mode]` lists, per map, how far each marker stands from a real
  point. No areas are drawn. AI Scav zones (side "scav", category "bot" or "all", not "sniper") are small quiet rings in
  ink, unlabelled and not hoverable. Sniper zones (side "scav", categories "bot" and "sniper"; Ground Zero's
  player spawns tagged "sniper" don't count) are a hollow ink hexagon labelled "Sniper": a shape no other symbol
  uses, not a reticle, which is the Elimination glyph. Bosses and AI squads get one red octagon per spawn zone, hollow
  at rest and solid while lit (below, *Stepped back until it matters*): the red octagon means "boss or AI squad spawn"
  (owner, 2026-10-04, from the review: until then a red diamond with a dark centre, which at its size was mostly collar
  and centre, weaker than a quest's disc, and shared its outline with the violet transit diamond; the octagon is a
  shape nothing else uses, and all red; from then until 2026-10-17 every zone's was solid at full strength). Every mob in the data's bosses list counts except the AI PMCs
  (`pmcUSEC`, `pmcBEAR`, which come everywhere): bosses, and since the map audit (owner, 2026-10-03) Rogues
  (Lighthouse, Icebreaker), Raiders (Reserve, The Lab), cultists (Customs, Woods, Shoreline, Night Factory, Ground Zero
  21+), AF and Black Division (Terminal, Shoreline, Icebreaker), by the data's names. Each is labelled in one format
  with Plan's line and the raid card's ("Kaban 75%"): the chance on the map, then "· N% here" when less applies to
  the place: for one with several zones, that zone's share ("Kollontay 75% · 50% here"); never their product, since
  the data doesn't say the share is conditional. A squad the data lists as several groups (Raiders, Rogues) has the
  likeliest group's chance on the map, as Plan counts a boss's, and a place says its own likeliest group's when that
  is less: The Lab's 2nd floor "Raider 60%" (groups at 60, 45 and 35 %), its basement "Raider 60% · 45% here"
  (2026-10-09, from the pre-release review: such a place listed every chance, "Raider 60%, 45%, 35%", a third
  format beside Plan's). That is the label while lit. It is ink
  like every other label (the review of 2026-10-04: it was red, which read worse on the dark ground than any other
  label; the octagon is the danger sign), and red only while the marker is pointed at, as any label takes its
  marker's colour then. A zone split in groups says this once, on its largest group; the others are
  bare octagons that light with it. Markers whose groups have the same centroid share one ("Reshala 75% · 33% here /
  Knight 25%" on Customs' Stronghold), and pointing at one lights all its zones. The Lab gets 8, Terminal 16; Ground
  Zero 21+ gets 9, mostly a 2 % cultist that may spawn at any Scav spawn.
  **Stepped back until it matters** (owner, 2026-10-17: "I feel like the red dots for enemy bosses are too
  distracting when trying to parse the quest icons. Come up with a better solution."; from a panel of four, today's
  look, A stepped back until it matters, B one symbol per boss with rings, C no bosses in Plan, the owner took A; B and
  C were not taken). Until then every zone was a solid red octagon at full strength, each labelled with its share, as
  loud as a quest's disc. **At rest**, in Plan and in a raid alike, every zone keeps its place and its octagon, drawn
  hollow: a 1.5 DIP outline in `Red`, no fill, on its dark collar as before, so it is dark inside, the whole at 70 %
  with its floor badge (`MapRenderer.BossRestAlpha`, `ShownMarker.Resting`). The player still sees where every boss
  can spawn; the quests' discs are the loud ones. One label per boss, on its likeliest zone (the largest share: what
  applies to the place, or the whole chance where nothing less does; the data's first on a tie; that zone's largest
  group), its name and chance alone in the map's own names' style, caps of the streets' 11 px letter-spaced as the
  landmarks', in `Muted`: "KILLA 75%" (`MapMarker.RestLabel`, `MapRenderer.BossRestNameSize`). The other zones are
  bare: Interchange's Killa is named at his 40 % zone, and his three 20 % zones say nothing until lit. **Lit**, every zone of the boss is the solid octagon with its full
  label ("Killa 75% · 20% here"), as all were until then: while the boss is pointed at (its name in the raid line or
  under the map's name in Plan, "A boss's name is its spawn zones", or one of its zones on the map), the rest of the map
  stepping back by kind as for anything pointed at; and while a picked quest, or one pointed at (its row, a marker of
  it or its card, held or not: the quest's focus, which lights its own places), has a kill objective still open that
  targets it, on this map or on any (`MapContent.QuestBosses`: the objective's targets, read from
  before translation, name the boss's id, `bossKilla`, or its name; an objective for any boss lists, and lights, them
  all; a guard, `followerBully`, is not its boss). A pick lights them as it lights its doors, stepping nothing back,
  their names in ink; pointed at or held, the quest's focus brings them, and their names take the red. The legend's row
  shows the resting look.
- **Labels by priority** (`MapRenderer.Layout`). Every symbol is placed before any text, so no label covers a
  symbol. Then labels in this order: the kept or pointed-at quest, bosses (one label per boss at rest, its likeliest
  zone's; every zone's while lit: *Stepped back until it matters*, above), quests, extracts and transits,
  snipers, and the map's own names last, largest tarkov.dev size first. A marker label tries right, left, above
  and below its symbol, in that order, and is dropped when all four are taken (pointing at the marker still names
  it). A side is taken where it would cover a symbol or a label, or stand closer than 8 px to a label on its line, one
  whose box shares more than half its height (`MapRenderer.LabelGap`, `OnOneLine`; more than twice a word space, at the
  text size's factor; UI QA pass of 2026-10-19, F1: at 150 % German "Transit zu Interchange" and "Taxi in der
  Primorsky-Straße" stood end to end on one line, about 2 px apart, and read as one name; only an overlap took a side
  until then, and at 100 % too "Pier Boat" and "Viewer" on Shoreline, "Fuel Crisis" and "Saferoom Exfil" on
  Interchange at the smallest window, "A Fuel Matter" and "The Bunker" on Reserve read as one). A name a line above or
  below another may end just short of it: the two read as two lines. A selected label that finds no free place goes on
  the right anyway, trimmed with "…" where it would run past the map's right edge. A side that would start past the map's left edge isn't free, nor, for a picked or pointed-at
  name, one that would end past its right edge (*Room for labels on the right*). A name is said once per neighbourhood
  (250 px): eight "Abandoned Cargo" places in one block carry one label. Map names are boxed by their rotated
  extent and drawn on the marker labels' halo (the ground at 86 %, 3 px): without it Ink at 59 % measured 1.4:1 on
  Streets' light streets, with it 4.7:1.
- **Map names by tarkov.dev's label size.** Every name was 11 px, though tarkov.dev sizes them (Streets: 90 for
  landmarks such as Kilmov Shopping Mall, 80 for streets, 70 for shops, 60 for cafés; Interchange's shops are 65).
  Landmarks (90 and up) are set in 12 px semi-bold caps, letter-spaced; streets (80, and names without a size) as
  before, 11 px, at every zoom; 65–70 from 1.5 times the zoom that shows the whole map; 60 from 2.5 times. Larger
  names are placed first. Names out of view aren't placed.
- **Distances on the map.** The guide line to the nearest pick carries the card's number on a small dark plate with
  a hairline in that pick's colour at the middle of its part in view ("69 m", the same horizontal distance and rounding as the
  card, `MapRenderer.DistanceText`); so does its second use (2026-10-17), from a gated extract pointed at to the
  first switch it takes, in the extract's colour (*Switches*, above). **The distance and nothing else** (owner,
  2026-10-04: "The 'minutes' numbers when showing distances on the map are completely off. Either be precise or
  scrap them at all"): until then the
  plate added the position's age once it was a minute old ("69 m · 4 MIN"), and minutes beside a distance read as
  the time it takes to get there, which Shturmap can't know. The age is said at the player's marker, always with
  "OLD". A line too short to carry it clear of its ends has none. Where the middle would cover a
  symbol, the plate slides along the line to the nearest place that covers none, toward the place first, and keeps
  the middle only when the line has no free place (the review of 2026-10-04: it stood on a boss marker). A scale bar
  at the lower left,
  above the wiki link and the credit line, takes the longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …)
  that fits in 120 px and follows the zoom. Its metres are those along the bar, the screen's horizontal (the review of
  2026-10-04: it took the mean of both axes, and on Icebreaker, whose render is stretched 1.75× along one axis, "50 m"
  spanned 69 m; there a metre up or down the screen is 1.75 times a metre across it, so the bar holds across only).
  **It steps aside from a symbol** (owner, 2026-10-15: at the smallest window Streets' Crash Site, lit, lay under its
  "100", and its name, finding no free side, wasn't drawn): before any label is placed, a symbol under its box (the
  bar and its figures) moves it up by what it takes, its box 2 px above the symbol's, again until it covers none
  (`MapRenderer.Scale`); the labels then give way to it where it stands. The symbols it steps aside from are those the
  repel sets apart (quests, bosses, sniper zones, extracts, transits), the player's ring and the edge chevrons; the
  receded locks, switches and Scav zones' rings it may cover, as other symbols do. Where that would take it more than
  96 px (four of its heights) above its place, it stays at its place, as before. Up, not right: it stays above the
  WIKI MAP link and the credit line, and left-aligned with them. On every map's whole view with a list read
  (`shturmap-cli frames <map> --scene list-read`, 15 maps, 2026-10-15) it stands at its place at 1600 × 1000; at
  500 × 481, the map of the smallest window, it steps up on Streets (49 px, past Crash Site and a quest), Factory and
  Night Factory (42 px). **While the view only moves it keeps its place** (owner, 2026-10-15; the rule *Steady while
  the view moves* holds the symbols by: a pan, or less than a tenth more or less zoom from one frame to the next, with
  the same position, display scale and age tag; `MapRenderer.Scale`, `ScaleBar.Held`), and the first frame drawn with
  the view still places it afresh. Placed afresh in every frame, it stepped up and back as symbols passed under it: in
  a 1.2 s glide (`shturmap-cli frames streets-of-tarkov --scene list-read --glide --every 16 --viewport 500x481`) it
  changed place 59 times, now once, as the view comes to rest; in the roll call's zoom out (`--scene list-callout`)
  30 times, now once. A frame laid out while the view moved is laid out again when the next one is drawn at the same
  view, the symbols keeping their places (the repel's placement holds), and the window draws that frame: a frame whose
  bar held asks for one more (`MapView`), since the end of a move draws none by itself (`AnimateView`, the roll call's
  and the tour's; a wheel's notches; a Follow glide; a drag). At 100 ms a frame a 1.2 s glide zooms by more than a
  tenth in some frames, which places it afresh as it lays the symbols out afresh (5 changes, 12 before).
  No range rings (three circles on every view for a question the bar and
  the plate answer) and no north arrow (the artwork's orientation isn't verified; the cards give directions
  relative to the facing).
- **How far the view zooms.** Out to half the zoom that shows the whole map, in to 64 px a map unit. The limit is
  the drawn map's, set again with every frame (`Camera.LimitTo`): until the review of 2026-10-04 it was the map's that
  was fitted last, so after a preview of another map the first turn of the wheel jumped to that map's limit. A view
  outside the limits moves toward them or stays, never jumps. A map fitted while the view has no room (the window
  minimised, or not laid out yet) is fitted when it has; before, its zoom fell to almost nothing and the map stayed out
  of sight until "show the whole map".
- **The wheel zooms over 0.15 s** (owner, 2026-10-15: a notch zoomed by a fifth at once, which laid the crowded symbols
  out afresh and could flip them to the other side of their places). A notch (1.197 times, about a fifth;
  `WheelZoom.FactorOf`) zooms over 0.15 s, eased in and out on a log scale, keeping the point under the pointer in
  place (following with the view on the player, about the player), so its frames come under *Steady while the view
  moves* (above). A notch during another adds its step to where that one goes and sets off from where it is, at the
  speed it has there (a cubic from that speed to rest on the target, never past it), so a turn of several notches is
  one zoom without a stop, landing 0.15 s after the last notch where the notches at once would have put it
  (`WheelZoom`). With Windows' animation effects off a notch zooms at once. A turn while the view moves by itself (the
  replay's glide, the roll call's) is left out: that move set the view again at its next frame anyway. Three notches
  0.3 s apart on Customs at 60 frames a second (`shturmap-cli frames customs --notch`): symbols set off their places by
  more than 3 px more or less from one frame to the next went from 82 (the largest 91 px) to 3, all at the frame where
  Abandoned Cargo's places part; a frame zooms by 2.9 % at most. A quick turn keeps under the tenth a frame up to about
  a notch every 30 ms at 60 frames a second; a faster spin lays some of its frames out afresh. At 30 frames a second a
  notch's frames zoom by 6.2 % at most.
- **The keys, the buttons and a double-click zoom over 0.2 s** (owner, 2026-10-15, evening page: they zoomed at once
  and laid the crowded symbols out afresh, so crowded ones could flip sides there). + and − and the zoom buttons zoom
  by 1.5 times about the view's middle, a double-click by 2 times about the point clicked, each over 0.2 s, eased in and
  out on a log scale as a notch is (`WheelZoom.Step`), following with the view on the player about the player. A press
  during a zoom under way, its own or the wheel's, goes on from it as a notch does: it adds its step to where that one
  goes and lands 0.2 s after the press (+ twice, 0.1 s apart: one zoom to 2.25 times, landing 0.3 s after the first).
  With Windows' animation effects off they zoom at once, as before; while the view moves by itself one is left out, as
  a turn is. So their frames come under *Steady while the view moves*: at 60 frames a second a frame of + or − zooms
  by 5.2 % at most (3.4 % on average), of a double-click by 9 % (6 % on average), under the tenth. On Customs at 60
  frames a second (`shturmap-cli frames customs --step 2`, `--step 1.5`), symbols set off their places by more than
  3 px more or less from one frame to the next went from 47 (the largest 91 px) to 3 for a double-click, from 58 (92 px)
  to 3 for +, all at the frame where Abandoned Cargo's places part; at the smallest window's map (`--viewport
  500x481`) a double-click's went from 93 (272 px) to 112 moves of at most 8 px, the repel's steps while the crowd opens
  fast. **Below about 55 frames a second** a double-click's middle frames zoom by a tenth or more, and below about 32
  those of + and −; such a frame lays the symbols out afresh, as a zoom at once did. At 30 frames a second (`--every
  33`) a double-click zooms by up to 18.7 % a frame and lays 4 of its frames out afresh (150 moves over 3 px, the
  largest 111 px), + up to 10.6 % and 2 (84, 108 px): more than at once. The window asks for a frame every 15 ms
  (about 64 a second, `MapView`).
- **Room for labels on the right** (owner, 2026-10-09). The map writes a symbol's label to its right, so the whole map
  (a new map, a Plan row's preview, 0 and the button) and every framing (What's New's and the tour's previews, the raid
  replay) keep 150 DIP between what they show and the view's right edge (`Camera.LabelRoom`); the other sides keep
  their padding (24 DIP for the whole map, 40 for a preview or replay, 90 for the tour). Until then the right side had
  only the padding, and on a wide map the rightmost names ran off the window (Customs: "Transit to I…", "Crossr…"; the
  tour's picked quest past its 90 DIP). The room is at most a fifth of the view's width (`Camera.LabelRoomShare`): at
  the smallest window, the map about 500 DIP wide, 150 would shrink a wide map by more than a quarter; 100 shrink it by
  a sixth, and a long name there may still lose its last letters (not a picked or pointed-at one, below). Room and cap
  grow with the text size, as the names do (`Camera.TextScale`, `Camera.RoomRight`; UI QA pass of 2026-10-19, F2: at
  150 % the names are half as wide again, and in 100 %'s room German names at the smallest window's right edge lost
  whole words, "Beschädigte", "Abwasserk", "Klimov-St"): at 150 % 225 DIP, at most three tenths of the view, so a long
  name there loses the letters it loses at 100 %, and a wide map at the smallest window is 13 % narrower than in 100 %'s
  room. What fits with room to spare stays in the middle (a tall map such as Streets in a wide view); otherwise it moves left only as
  far as the room needs (`Camera.FramingAbove`, the one place a view is worked out). "The zoom that shows the whole
  map" (the zoom limit, when the map's smaller names show) is this view's.
  **The left edge cuts no name** (owner, 2026-10-15: at the smallest window Streets' "Transit to Interchange", its
  right taken, went on its left and read "ansit to Interchange"; Customs' "…ation Gate" before). No label starts
  less than 2 px in from the map's left edge (`MapRenderer.Inward`, `LabelEdge`). Of its four sides, one above or
  below its symbol that would start further left slides right to start there, still over or under the symbol; one
  beside it can't move without leaving the symbol or covering it, so it is not a free side, and the next is tried;
  with no side left the label is dropped, as one whose four sides are taken is (pointing at the marker still names
  it). No room is kept on the left as on the right: a name goes right of its symbol first, away from that edge. A
  symbol itself past the edge keeps its name only on its right, where that starts in view.
  **A picked or pointed-at name isn't cut at the right edge either** (owner, 2026-10-15, evening page E3: at full size
  the tour's chapter 4 in German read "Lieferung aus der Vergan": its picked quest's name, 166 DIP in the bold face,
  ran past the 150 DIP room). The room grows for that name, the bold one placed first (*Labels by priority*), and only
  for it: where its right side would end past the map's right edge (2 DIP in, `LabelEdge`), that side isn't free, and
  the name goes left of its symbol, or above or below it slid left to end at the edge, the left edge's rule mirrored
  (`MapRenderer.Inward`). It moves in whole up to 260 DIP or 40 % of the map's width, whichever is less, and trimmed
  with "…" to that beyond (`LitNameMost`, `LitNameShare`): 260 holds every quest name of tarkov.dev's data in the bold
  face but one (October 2026: English's longest 251 DIP; German's 307, "Weg des Überlebenskünstlers - Ungeschützt,
  aber gefährlich", then 256), and at the smallest window 40 %, 200 DIP, holds 95 % of the German names and 97 % of the
  English. With no other side free it stays on its right, as before, now trimmed with "…" to end at the edge, never cut
  (`MapRenderer.Trimmed`; a dash or space before the "…" left out). While the view only moves (*Steady while the view
  moves*) it keeps the side it had in the frame before, on its right trimmed with "…" at the edge rather than going
  over to its left mid-move; the first frame with the view still places it afresh, as the scale bar settles. Every
  other name keeps the 150 DIP rule. The room grows in the layout, not in the view's fit: the tour frames chapter 4
  before it picks and points, and pointing doesn't move the view, so no fit knows the lit name; the layout finds its
  room wherever the view is. Chapter 4's view at full size (2160 × 1313) now shows the name whole, left of its symbol;
  a 500 × 481 view of the same places, where that symbol's other sides are taken, "Lieferung au…".
- **The player at full strength.** The sand disc no longer fades with age (it sank to 45 % after 3.5 minutes,
  below the quest markers; transparency reads as "less important", the reason half-strength other-floor markers
  were rejected). It has the picks' vocabulary instead: a steady ring, sand on a dark band, at 12 px. Once
  the position is a minute old the ring turns dashed and a small dark tag beside it gives the age in whole units,
  as the top bar does, with "OLD" ("4 MIN OLD", "2 H OLD"). The facing cone has its arrow outside the ring, and shows for the 45 s the
  cards' directions are relative to the facing (principle 8, `Facing.Fresh`; until the review of 2026-10-04 for 60 s).
- **One meaning per colour, shape as a second cue.** Simulated colour blindness (Machado 2009) put the quest amber,
  the PMC-extract green and the shared-extract khaki within ΔE 4–7 of each other. Shared extracts are split down
  the middle by a dark line (two sides, one exit), not hollow. Done
  objectives are a smaller muted-ink disc with a check mark, no longer translucent green, so green means extracts
  only; their zones turn muted too. The trail is the player's sand, no longer the Scav-extract teal. Sand is the
  player's alone (the review of 2026-10-04: other-floor badges and the squares of an item's loose spots were sand
  too): a marker's floor badge wears the marker's colour, as its count and "OPT" badges do, and a loose spot's open
  square is ink, like the padlocks a key pointed at lights and the container dots: what the map's data has, neither
  a quest's nor the player's.
- **Clusters of one objective's places.** Places of one objective (and one kind) whose markers would overlap,
  closer than two marker widths on screen, merge into one marker at the group's medoid (a real place, so the
  marker never stands where nothing is) with a count badge at its lower right in the marker's colour: "Following the
  Bread Crumbs" lists 16 places on The Lab, which piled up with four labels. Groups split as the view zooms in;
  places of different objectives never merge; possible places keep their "?"; the kept or pointed-at quest clusters
  too. **Possible places** (one of several places a thing can be) are drawn like every quest marker, the type's
  dark glyph on the marker's colour, with a "?" at the lower left, in the badges' look (owner, 2026-10-04: "The
  'Mark' icon is the pin needle, which is black on background. The 'Get' icon is a hand that is color on
  transparent background. I think it should always be a black icon surrounded by the marker color"). Until then a
  possible place was a hollow ring with the glyph in colour, meant to read as "maybe here"; it read as another kind
  of marker, its glyph taken for the difference. The four corners are then: floor upper right, count lower right,
  "OPT" upper left, "?" lower left. **Only where there are several**: tarkov.dev lists every quest item's spot as a
  possible location, also where it is the only one (57 of 109 such objectives in PvE, 2026-10-06: Population Census's
  journal, Audit's records, Golden Swag's lighter), and those were drawn with a "?" that said nothing true (owner,
  2026-10-06: "Why does the Population Census have a question mark in the icon?"). A thing with one place is drawn at
  it as a place like any other (`MapContentBuilder.PlacesItCanBe`); with more, across every map, each is a possible
  place, and the quest's card says so ("Quest cards"). A spot counts and is drawn once: tarkov.dev lists it once per
  map variant (Factory and Night Factory, Ground Zero and Ground Zero 21+) and now and then twice under one map, and
  positions within half a metre, in three dimensions, are one spot (2026-10-13: Possessor's, All Is Revealed's, Health
  Care Privacy - Part 6's and Postman Pat - Part 1's one spot each had a "?" and "One of 2 places"; One Less Loose End's
  5 spots read 10; 12 quests in both modes). A group with any place on another floor shows the floor arrow. The largest group carries the label;
  pointing at a cluster points at its quest. Only places in view are merged, so a group that runs out of view keeps
  a marker for the ones in view, with their count (the review of 2026-10-04: merged first, the whole group went when
  its middle place was out of view).
- **Objectives of one quest on one spot: one marker that says how many** (owner, 2026-10-06: "Sometimes you have two
  objectives to do on exactly the same spot. For example, for Gratitude on Woods you have to stash Shemagh and Ray
  Bench glasses on exactly the same spot and it shows as two icons marking the same spot … we should join the two and
  annotate the icon properly"; chosen from a panel of five ways drawn by the real map on four spots: "D, count, but
  make it look nice"; `MapRenderer.JoinSpots`, `MapJoin.cs`). Places of different objectives of one quest within
  1.5 m across and up are one spot (in PvE 65 pairs on 2026-10-06: Gratitude's two stashes, Hobby Club's four,
  Bullshit's three, a "locate" and its stash or mark, as in Fishing Gear, Drug Trafficking, Humanitarian Supplies). The
  first open objective's disc stands for them, and **a tab** joined to its right says how many are open there: "×2". Disc
  and tab are one shape, gold with the marker's dark outline and collar, the tab's far end rounded as the disc is, and
  a soft dark hairline where the tab leaves the disc so the disc still reads as one; in the tab "×" a little smaller than
  its figure, both dark as the glyph. A tab, not the corner count: that one is a cluster's, places of one objective
  that part as the view zooms in, where these stay one spot. The name stands after the tab; the floor arrow moves
  above it; the tab is the marker for the pointer, and pointing at any of its objectives (a row on the card, a line in
  the raid card) points at the spot. A ticked objective leaves the count: with one left open the marker is a plain disc,
  with none a done one. "Maybe here" places don't join: a "?" couldn't say which objective is only maybe there.
  Also drawn and rejected (the panel, `shturmap-cli join-panel`, removed with the choice): each objective on its own
  disc set apart on leaders to the one spot (as before); a pile of discs; the glyphs side by side in one wider shape;
  the pile with the things' names in the label.
- **Places out of view.** While quests are picked, or one is pointed at, their places outside the view are shown
  as small chevrons 18 px in from the edge, toward them from the middle of the view, one per direction (places
  whose edge points lie within 56 px merge) with how many lie that way, in the quest's colour (its pick's colour
  when picked, gold when pointed at). Done objectives don't count. Same vocabulary as the player's edge badge, smaller and
  without a plate: the player is level 1. Nothing is drawn when nothing is highlighted.
- **The raid replay** (owner, 2026-10-07: "a post-raid view, similar to the one in Arc Raiders ... only the positions
  from the screenshots ... more on the eye-candy side of things ... automatically played after the raid finishes"; from
  the panels "B and D", then "C, but encode raid time to the color of the pen stroke. Use a fitting color map based on
  the apps design", "Replay timeline 1", "Replay link: ii"; §1's exception). What there is to replay (a week of the
  study log, about twenty raids): one position per four or five raid minutes, a third of the raids with one, the
  median four, gaps of 10 to 20 minutes in long raids, the last position up to 20 minutes before the end line; how a
  raid ended is in no log Shturmap reads. So the replay shows where and when the player took screenshots, and when
  they were last seen, never how the raid ended.
  - *What is kept* (`GameSession`): each position on the raid's own map with its minute since the raid's start
    (`WallClock`), the objectives ticked during the raid with their minutes, and when a screenshot first showed the
    extract list. A raid loading starts them anew, so after a transit only the map the raid ended on is replayed. At the
    raid's end, when the log has its end (its length is known), they make a `RaidReplay`, which comes with the RAID
    OVER cue and stays in the snapshot until the next raid loads. In memory only: never on disk, never in the app log
    (§2), gone at a restart.
  - *When it plays*: at RAID OVER, when the raid has 3 positions or more over 5 minutes or more (`RaidReplay.Plays`;
    about half of that week's raids); otherwise today's cue. Never for the log read back at start. REPLAY on Plan's
    last-raid line plays it again (the raid's map comes back on screen first) until the next raid loads.
  - *The sequence* (`ReplayTiming`, `MainWindow.Replay`): RAID OVER enters in the middle as every cue does (1.6 s), then
    fades as the band slides up to the map's foot and the view glides to the raid's positions (1 s); the raid plays in
    11 s whatever its length, holds 3 s on its end, and fades (0.6 s): about 17 s, no clicks (owner, 2026-10-08: "can
    easily be a bit longer"; until then 7.5 s of play and a 2 s hold, about 12.7 s). The band stands 46 DIP
    above the map's foot, so the WIKI MAP link and the artwork's credit stay readable, and right of the map's buttons.
    A new cue, a raid loading or a preview ends it. With Windows' animation effects off, and in snapshots, the end shows
    at once for the time it would have played (a snapshot keeps it).
  - *The view*: framed on the positions with 40 DIP around them (on the right the label room; "Map drawing", *Room for
    labels on the right*) and at least 300 m across, in the part above the band (`MapView.FramingAbove`). It stays
    there afterwards. A replay that ends while the view still glides there (a raid loading, a preview) stops the glide,
    and so does a new map in the view (review of 2026-10-09: the glide went on moving the next map). The map recedes
    under the replay (the ground at 45 % over it),
    and everything of the replay fades in and out as one (`MapScene.ReplayOpacity`).
  - *The pen* (C; `MapRenderer.DrawReplay`, `ReplayInk`): one line from position to position, drawn on as the time
    runs, growing toward the next position. Its colour is the raid's time: the player's sand mixed into the ground,
    opaque, from 22 % sand at the raid's start to all of it at its end, and it widens from 1.4 to 3.2 DIP, over a dark
    collar (the ground at 59 %, 6.2 DIP). One hue because sand is the player's alone: any map of several hues
    (viridis, magma, cividis) runs through the map's taken colours, green and teal for extracts, violet for transits,
    amber for quests, red for bosses. Light means late, as the newest position is the brightest thing on the map, and
    width is the second cue ("One meaning per colour, shape as a second cue"). Tried on a raid from the study log
    and dropped: sand by alpha (the artwork showed through, and the overlapping pieces dotted the line), a range from
    35 % sand (too little difference). Each position is a dot (2.6 DIP) in its time's colour on a dark collar, with its
    minute beside it, "8 MIN IN" (never bare minutes, which read as a time to get somewhere, owner, 2026-10-04; the
    panel's "8 MIN" changed for that), ink on a dark tag set where it covers no other tag or dot and stays above the
    band, else left out. The newest position pings as a new position does. At the end the last one's tag says "LAST
    SEEN · 2 MIN BEFORE THE END" ("JUST BEFORE THE END" within a minute), framed in sand as an old position's tag is.
    Positions on another floor are drawn as on the floor shown. A straight line between positions minutes apart reads
    as a route Shturmap doesn't know; the owner chose the pen knowing that, and the minutes and the timeline show the gaps.
  - *The timeline* (1; `Controls.ReplayBand`): the band's words, RAID OVER in amber and CUSTOMS · 33 MIN in ink in the
    cue's eyebrow, and "6 OF 9 POSITIONS" at the right; the raid's length as a 4 px rule with a tick every ten minutes
    and at its end, its played part in the pen's colours along the whole rule (so the rule is the colour key) and the
    rest `LineStrong`, the playhead (2 × 14 DIP, ink). Above the rule a dot per position, in its colour once played and
    an outline before; below it a check (`E73E`, the done objective's) per objective ticked during the raid and a green
    triangle (the PMC extract's shape and colour) where the extract list was read; "33 MIN" at the end.
  - The legend has a row for it, listed while it plays on the map.
- **A quest completed: its places ring out** (owner, 2026-10-07, with the QUEST COMPLETE cue, §4 principle 11). The
  places of a quest the log just reported completed don't simply vanish: each turns into a gold disc with the done
  objective's check that pops once, two gold rings leave it as a ping's do, and it fades, 1.6 s in all
  (`MapScene.Leaving`, `MapRenderer.DrawLeaving`). It helps the eye see which places went. The places are those the
  snapshot without the quest takes off the shown map, matched with the cue whichever comes first
  (`MainWindow.Completion`). Nothing with animation effects off: the places go. The legend has a row for it wherever
  a quest has a place.
- **Legend.** The help panel's ON THE MAP rows are drawn by `MapRenderer` itself (`MapLegend`: one small bitmap per
  symbol, made with the map's own drawing code at twice the DIP size), so they can't drift from the map; the hand-drawn
  XAML shapes showed a plain disc for the quest marker, which has a collar and a glyph. Rows go by the four levels:
  first what levels 1 and 2 always draw (you, the picks, objectives, extracts and transits), then what marks those or
  shows only at times, then levels 3 and 4 (owner, 2026-10-09: the extracts came after the ping and "Moved off a crowded
  spot", so help's twelve on Streets left them behind the link). Every symbol on the map has one (the done objective,
  quest zones, the floor arrow, the guide line and its plate, the trail, the edge badge and chevrons, clusters and the
  sheet were missing; and until the review of 2026-10-04 the gold chevrons of a pointed-at quest, the padlock in a
  pick's colour of a door it needs a key for, and the ping of a new position), and each names shape and colour, not
  colour alone. A new symbol gets a `LegendSymbol` and a row in the same change, and a line in `MapLegend.On`. Help
  lists the symbols the map on screen has under ON THIS MAP, the twelve most important of them, and keeps the rest of
  them and the symbols the map doesn't have (under NOT ON THIS MAP) behind one link, "SHOW n MORE SYMBOLS" (owner,
  2026-10-04: every symbol of every map made help 2,350 px tall; 2026-10-09, help trimmed: Streets alone listed 26;
  `LegendFold`).
  A symbol counts when the scene holds what it stands for (`MapLegend.On`): a padlock when the map has locks, the
  guide line when a pick has a place here and there is a position, the sheet when there is no artwork. What only
  shows for a moment counts where its cause can occur: the ping wherever there is a position, a pick's chevrons and
  padlock where a pick has a place or a door here, the gold chevrons wherever a quest has a place (any of
  them can be pointed at). The list
  follows the map while help is open; with no map up, all rows count as ON THE MAP (twelve, then the link).

### Quest cards

One card per quest, the same everywhere: trader portrait, type glyph and name; trader, level and Kappa /
Lightkeeper; the state and where it came from ("Active · from the game log, 25 Sep"); every objective with its
glyph, where it is, the item it is about and, in a raid on its map, how far and which way it is from the last fix
("69 m · AHEAD-LEFT · 8 M UP": the raid card's style, the figure as it is and the direction and height in capitals;
until 2026-10-09 in small letters, beside the raid card's capitals); BRING (keys and items, with icons and maps);
UNLOCKS (the quests it opens); a link to the wiki page; NOTES, the player's own words (*Notes*, below). Nothing else:
no rewards, no guides. An operational task (a daily or weekly) gets no card: the data has nothing of it, its trader
isn't read, and its rewards are never read (owner, 2026-10-19; "Screen anatomy", *Operational tasks*).
The state line is in the accent while the quest is active ("on") and muted for every other state. It was green,
which on the map and in the rail is a PMC extract (one colour, one meaning; the review of 2026-10-04, B2).
An objective says where it is once (the review, C2): the line of map names under its text is left out when the
text itself names the map as its place ("… on Streets of Tarkov" stood over "Streets of Tarkov"), alone or in a list
that holds every map the line would show. A text that names none of its maps, or only some, keeps the whole line;
"at Factory gate" is a gate, not the map (`QuestCards.SaysWhere`, with the synopsis' map pattern,
`QuestSynopsis.MapList`). The pattern is the data's language's: English, and German from 2026-10-11 (owner: German
cards kept both lines and were several lines longer), whose "auf" and "in" say where ("… auf Woods, Customs oder
Shoreline") and "nach", "aus" and "von" where to or from; in another game language both lines stay (§8, "The app's
own language").
**The map's "?" in words** (owner, 2026-10-06: "It should be displayed in the raid tooltip to explain"): an objective
whose thing can be at several places has a line under its map names, the "?" as the map draws it (a small dark plate
with a gold hairline) and "One of 4 places it can be", on every card, held or on hover in a raid
(`CardObjective.Possible`); its distance is to the nearest of them. A done one has no such line.

**Ticks: an objective the player says is done** (owner, 2026-10-04; UX principle 7). The game's logs never say that
a single objective is done, so a quest that takes several raids kept leading to places already dealt with.
- **Where.** Each objective's row on an active quest's card (hover or held) ends in a small square box, the
  look of the ticks in settings (hairline; amber with a check when ticked). One click ticks, another unticks. Its
  tooltip says what it does and that the logs don't say it. Nothing is asked, and the rail has no tick: it stays
  calm, and the card is one point away. A tick can be set any time, also in a raid, but nothing ever needs it.
- **Says why.** A ticked objective is muted, as done things are on the map, and where its distance stood it says
  "Done · ticked by you, 4 Oct" (the day of the tick). The quest's state line is untouched: it is the log's.
- **Kept.** Per game mode, in the settings beside the picks (`ticks.<mode>`, each with its day; `ObjectiveTicks`).
  A tick leaves by itself when the log reports its quest completed or failed; one whose objective the data doesn't
  know stays. Only an active quest's objective can be ticked; a tick can always be taken back.
- **What follows**, all from the one set of ticked objectives: the map draws the objective's markers and zones as
  done (the small muted disc with a check, at its rest size and without a name, also while the quest is picked or
  pointed at); its places leave the guide line, the chevrons and NEXT; the raid card mutes its line ("Screen
  anatomy"); the planner sees the quest without it (§7), so COMPLETE and PROGRESS, the summary, the synopsis, the
  progress note, the need cells, BRING, the kit and the effort order all speak of what is left; the quest card's
  BRING and the item card's uses list only what open objectives need; a Scav raid's FIND IN RAID drops what a ticked
  objective asked for. A quest with no raid work left appears on no map. Its type glyph stays the whole quest's.
- Developer builds: the study log records `tick` and `untick` (objective, quest, how: card, done, dev); the
  developer script's `tick <quest> <n>` sets one for the session only, and `show <quest>` holds a card for a snapshot.

**Notes: the player's own words on a quest** (owner, 2026-10-15). What the player wants to remember of a quest for the
next raid ("Behind the bar, ground floor"), on its card, where they look for it.
- **Where.** NOTES, the card's last section, after the wiki link: every note, newest first, its stamp over its words. The
  stamp is when it was written, "1 Jan 15:40", the day as the tick's is written and the time (`QuestCards.NoteStamp`,
  in this PC's time; German "1. Jan., 15:40"), in the note style; the words in the body's, wrapping, their line breaks
  kept. A held card adds a field under them, the Report dialog's text box, one line to start with ("Add a note…"), and
  a × at the end of each note (a notice's, "Delete this note"). A hover card shows the notes alone, and no section when
  there are none: it leaves with the pointer, no place to type or to delete in (a click holds it, and the field is
  there). A held card without notes shows the heading and the field. A card that grows long scrolls inside, as any card
  does (below).
- **Add and delete only, no editing**: the stamp always says when the words were written. Enter adds the note and
  empties the field, Shift+Enter breaks the line, Esc clears the field; the × deletes at once, without asking. A note
  is trimmed and cut at 500 characters (the field takes no more).
- **Never needed.** A note can be written any time, also in a raid (on a card held then), but nothing asks for one and
  nothing reads one: the map, the rail, the plan and the cues don't know them.
- **The keys are the field's.** While it has the focus none of the window's shortcuts fires, Esc included ("Keyboard"):
  typing a note never moves the map or changes the picks.
- **Kept.** One set per quest and game mode, in `notes.json` (§8, "The notes file"). The card follows the session's
  next snapshot, which comes after the file is written; the field is never made anew, so what is typed in it and its
  focus stay meanwhile.
- **Help** says it in a paragraph after the ticks' (`HelpNotes`): what NOTES is, the time on each note, add or delete
  only, never needed, also in a raid, and kept on this PC.
- Developer builds: the developer script's `note <quest> <text>` adds one for the session only, for a snapshot.

**Hand-overs: a mark on what gets the thing, not a line** (owner, 2026-10-05: "many quests have a thing where you
have to get something or pick up something and then at a later step you need to hand it off after the raid. That
last step is out of raid but still takes up a 'ToDo' line in the quest overview. I feel this should rather be an
icon associated with the corresponding item or quest item. It should not be AI derived per quest, it should be
detectable from the quest text or layout itself").
- **Which.** A hand-over folds into the objective of the same quest that gets the same thing (`Handovers`), by the
  data's ids, so it holds in every game language: `giveQuestItem` with the `findQuestItem` of the same quest item;
  `giveItem` with the `findItem` of the same set of items (any order), the same count and the same found-in-raid.
  Each pairs once, in the quest's order (two quests list the hand-over first). Checked on 2026-10-05: in PvE 217 of
  391 hand-overs fold (95 of 99 quest items); in PvP 217 of 403. What doesn't fold is the quest's own work at the
  trader and stays a line, as before: money, "any found in raid medicine items", figurines, and a quest item got in
  an earlier quest (Postman Pat - Part 2's letter, Kind of Sabotage's folder, A Healthy Alternative's journal,
  Shipping Delay - Part 1's package). Words weren't used: the hand-over's text is often just "Hand over the items".
- **Optional.** Where the find is optional and its hand-over isn't (the game counts the finding as optional where
  the items may be bought or found anywhere: A Bitter Victory, Reserve Expert, The Huntsman Path - Eraser), the
  line reads as required, without "(optional)" or OPT (`Handovers.Optional`): what is handed over has to be got. The
  planner still goes by the data's flag.
- **The mark: the item, to the trader** (`Controls.HandoverMark`; owner, 2026-10-06: "Right now this icon uses up an
  entire row in each quest. It should be a proper symbol, but maybe attached to the left or the right of the icon of
  the item"; chosen from a panel of five ways drawn at the app's sizes from the real data: "Hand-over: E"). Right
  after the cell of what the objective gets: its count when more than one ("×4", as after a need cell; a find's text
  from the data doesn't say it), a small solid arrow in muted (Phosphor's "arrow-right") and the portrait of the
  trader it goes to, the quest's: "battery ×4 → Therapist". No symbol to learn, and the portrait says to whom. On a
  quest card (hover or held) it follows the objective's 30 px cell, with a 22 px portrait; in the raid
  card the line gets the item's 20 px cell with the mark, a 20 px portrait, between its words and its distance (the
  cell is that item, as a need cell is). The tooltip says it in words: "Hand over ×3 to Therapist after the raid".
  NEXT has none (a glance is for where to go). A ticked line keeps its mark: what was got still goes to the trader.
  Before it: the handshake alone, 12 px and muted, in the corner of the item's cell, "barely visible and you cannot
  tell what that actually is nor what it means" (owner, 2026-10-05); then a tag with the handshake and the words
  ("HAND OVER ×3 TO THERAPIST") under the objective's lines, which took a line on every hand-over. Also drawn and
  rejected (the panel, `shturmap-cli handover-panel`, removed with the choice): a tab on the cell's right or left
  with a hand passing something up (Phosphor's "hand-arrow-up", bold), and the tab with a forward arrow; a new
  symbol to learn, where the portrait needs none.
- **What follows.** The quest card and the raid card lose the hand-over's line; a quest whose lines on this map are
  all ticked leaves the raid card (the hand-over line used to keep it there, "after the raid", with nothing to do in
  the raid). The item card says it once, "Pick up, then hand over · Ground Zero", "Find in raid ×3, then hand over";
  once the find is ticked, the hand-over is what is left and says so itself ("Hand over ×3, found in raid"). Plan
  never listed hand-overs (it counts in-raid work only), and FIND IN RAID already counted a find and its hand-over
  once.
- `shturmap-cli handovers [mode]` lists every fold and flags NEAR (a hand-over sharing items with a find or pickup
  of its quest that doesn't fold, and why) and quest items handed over with no pickup in their quest; run it after
  a tarkov.dev update ("Updates and checks", docs/UPDATES.md).

The **item card** (a key or an item): its icon and name; GET IT, easiest first: traders that sell it (loyalty level,
price, quest unlock), barters, hideout crafts, the flea market (from which level), and where it lies loose ("Loose
on Customs · 3 spots"); then which of your active quests need it and how ("Key · Customs", "Bring ×3, to plant ·
Streets of Tarkov"). While the pointer is on an item anywhere, its loose spots on the shown map are drawn as small
open squares. Every BRING row (Plan, the raid card, a quest card) says what the item is for and for which quests
("to mark, for Revision", "to wear, for Dandies", "key for Ballet Lover") and has one line with the easiest
source; the tiny need cells beside quest names say the same in their tooltips (the study log: gear item cards were
opened 10 to 21 times each to find out why and where). Gear a kill objective asks to be worn (tarkov.dev's
`wearing`: sets worn together, any set will do) is a requirement of its own, "to wear", shown as "Bomber beanie /
RayBench Hipster Reserve sunglasses" (neutral about and/or: the data's sets don't always match the quest's
wording, which the objective text gives anyway); item cards list it as "Wear, for kills".

**One row per thing, counted as Plan counts it.** A quest card's BRING and the item card's lines are gathered over
the quest's objectives the way Plan's BRING is (`RaidPlanner`'s requirements): what is used up adds up (three "mark"
objectives are "MS2000 Marker ×3" and "Bring ×3, to mark"), one item used two ways is one row saying both ("to plant
and to use"), and a key or item needed on two maps names both. Gear to wear, weapons and mods aren't used up and stay
rows of their own; an exit's items are counted once per exit. Until 2026-10-04 the card made one row per objective and
dropped a row whose text was already there, which lost the count and the second map. Only open objectives count: a
marker is one per place marked, so a place ticked as done takes its marker off ("×2" becomes one), an untick puts it
back, and a place only located keeps it (the mark is what uses it up). Checked on the owner's report of 2026-10-15
("does not show that you have to bring multiple ms2000 or jammers"; Humanitarian Supplies and Ill-Wisher, two places
each on Shoreline): ×2 on the card, the plan's row and need cell, the kit and the item card, one after a tick, in both
modes (`BringCountTests`, also against the local cache).

**A quest item is never brought** (owner, 2026-10-06: "In mission one less loose end, the lab journal is marked as a item
to bring, but it is one you actually have to collect first as part of the mission"; asked about again and confirmed,
2026-10-19). A quest item an objective stashes
(`plantQuestItem`) is got by a "locate and obtain" of the same quest (14 of the 15 such objectives on 2026-10-06) or of
one before (Hobby Club's handguard, from Fair Price - Part 2). On pickup it goes to the quest items, and from there into
every raid by itself (the game asks "You have quest items in your inventory! Proceed?"; it is lost only with a death, or
left behind when parked in the quest items stash): there is nothing to pack. So it is in no BRING row, need cell or kit,
and the item card says "Stash · Woods", not "Bring, to plant". The raid card's line says where it comes from instead,
in the place of a "Bring:" line: "With: Lab journal, found on Factory", "With: AK-50 handguard, from Fair Price - Part 2"
(`Planning.QuestItemFrom`), each map once as the card's map lines name it (2026-10-13: it read "found on Factory or
Night Factory" for One Less Loose End and Health Care Privacy - Part 6, and named both Ground Zeros for The Tarkov
Butcher). Until then each stood in BRING as an item to take along. The quest card says it on the
stashing objective, under its map line, in the raid card's amber (owner, 2026-10-13: the card didn't name the lab
journal as needed; of a line on the objective, a row under BRING marked as found, BRING renamed, or back in BRING, the
owner chose the line): gone once that objective is ticked, and never on the pickup, which pictures the item already.

**An offer behind a quest isn't a way yet** (the review of 2026-10-04: BRING named "Ragman LL2 · 41,283 ₽ · after
Dandies" as the easiest source of the beanie Dandies itself asks for). A trader's offer or a barter that tarkov.dev
ties to a quest (`taskUnlock`) which the game's log hasn't seen completed comes after every other way (other
offers, barters, crafts, the flea market, loose spots) and says "after <quest>"; so the one line under a BRING row
names it only when nothing else is known. Once the log has seen that quest completed, the offer is one like any
other, in its place by price, and the note goes. Of several items that will each do (a weapon class), the line names
the first with a way open now. Shturmap knows no loyalty level, so "LL2" stays a fact about the offer, never a claim
that the player has it (`ItemCards.Sources`).

**What kills and exits take** (owner, 2026-10-03: "There are others that still require items, such as doing kills
with certain weapons or weapon classes … It should be a coherent design so it is still clear what to bring and what
is needed to solve a quest"; and "in the cease fire quest it is not clear that we need to bring a flare"). BRING
keeps one row shape (icon, what, what for and for which quests, where to get it) and adds the verbs:
- **"to use"** for the weapons a kill objective names (`usingWeapon`, any one will do). tarkov.dev arrives with a
  class as every member, so the weapons are grouped by their item category (tarkov.dev's `categories`, most
  specific first; presets, which repeat the weapon they build, don't count) and said in at most three parts: a
  group that is at least three quarters of its category, or more than ten weapons, by the category ("Any sniper
  rifle", "Shotgun (13 of 16 kinds)", "Assault rifle (15 of 53 kinds)"); if that takes more parts and the weapons
  span several categories, every group of two or more by its category ("Any handgun or revolver (3 of 5 kinds)");
  otherwise, and for a few weapons of one category, the weapons ("Colt M4A1 or 5 others"), so there is a gun to
  name. "Any" only for the whole category; a count says how many kinds will do. Whole categories come first, then
  parts of one, then single weapons ("Any sniper rifle or MP-18 7.62x54R single-shot rifle"). The categories load
  with the item sources, after the rest; until then the list form. A class row's source line names one of them ("e.g.
  Mosin rifle · Prapor LL1 · …"). Its cell pictures the first weapon as the assembled gun, its default preset's icon
  (owner, 2026-10-19, from a panel of five, 2026-10-19: A; §3, "Trader portraits and item icons"): the bare item is a
  receiver alone. The row, its link and its card stay the weapon's.
- **"to fit"** for the mods the weapon must carry (`usingWeaponMods`), shown like gear ("Valday PS-320 1/6x scope /
  AK-12 5.45x39 sound suppressor").
- **"to leave through <exit>"** for what the exit an extract objective names takes: the objective names it by the
  game's internal name (`exitName` before translation, `ObjectiveFacts.Exit`), the same as the map's extract, and
  `ExtractRules.Items` gives the items by the rules the extract list already uses: a green signal flare at a flare exit
  (Cease Fire!, Belka and Strelka; red until 2026-10-17, "Extract requirements"), ice pick and paracord at a climbing
  exit (Payback), the roubles or item a paid
  exit asks for ("Roubles ×5,000: to leave through Primorsky Ave Taxi V-Ex"). Transits stay with their own text.
- **"to enter <map>"** for what the map itself takes to deploy (tarkov.dev's `accessKeys`: The Lab's access keycard,
  Labyrinth's, Icebreaker's marine repair kit; owner, 2026-10-03, from the map audit). It is the first row of the Plan
  card's BRING and the raid card's, the first of the map card's need cells, and shows with no quest picked or none
  there, since every raid there needs it (a Plan that ranked The Lab first never said it takes a keycard).
- **Gear a kill forbids** (`notWearing`) is nothing to bring, so no row: a note on the objective's line in the raid
  card, "Without: armor, headwear" (the item categories it spans; before they load, the items).
- One row per weapon set, mod set or exit item, listing every quest it serves; the quest rows' need cells, the raid
  card's objective lines ("Use: …", "Fit: …"), the quest card's BRING and the item card ("Use, for kills", "Fit, for
  kills", "Bring, to leave through …") say the same. Only what the data says, and never "you have it": a row may carry
  a mark saying where Shturmap learned the item was had and when ("What you have", below). `shturmap-cli bring
  [mode]` lists every map's rows (a row of things to use up for several quests with each one's own count where it
  needs more than one, "Humanitarian Supplies ×2", as its need cell says; 2026-10-15) and flags an item without a
  name, a weapon list shown as "X or N others" (with the categories it spans) and an exit no map has; run it after a
  tarkov.dev or game update. Checked 2026-10-03 (PvE and PvP): no unknown items, no unmatched exits.

**What you have** (owner, 2026-10-07: "I don't know if I already have items that are required for quests";
2026-10-11: the second design, all twelve decisions as recommended, built after a spike; docs/NEXT.md, both entries).
Marks on what the quests need, nowhere else: no stash screen, no list to browse, no value (§1: the stash desk is
a view of its own between raids, its keep mark tied to What You Have). Three sources, one mark, and every mark is
an observation with its source and date, never "you have it", "in your stash" or
"missing":

| from | says | taken away when |
| --- | --- | --- |
| the game's notification log: the items a quest hands over at its start, its rewards, an insurance return (§8, "Logs and raids"; the item and the count, nothing else of the message) | "Given with the quest · 25 Sep" ("Given with Pharmacist · …" when the row serves several quests), "Reward from Shaking Up the Teller · 1 Oct", "Returned by insurance · 3 Oct" | start items go when their quest completes or fails; a reward or a return stays (another quest may need it); a quest completed after the observation takes what it used up (below); NOT ANY MORE; FORGET ALL MARKS |
| a menu screenshot the player took, read for the items BRING lists (§2) | "Seen in your stash · 5 Oct"; "2 of 3 seen in your stash, found in raid · 5 Oct" (a hand-over that asks for the game's mark counts only cells that carry it; "…, not found in raid" when none did); "Seen in your stash · 5 Oct, not in your screenshots of 12 Oct" | a quest completed after the observation takes what it used up (below); NOT ANY MORE; FORGET ALL MARKS. Never by a picture: a reading can miss. A full pass (three or more stash pages within a quarter of an hour, no raid between) that didn't show the item adds the words, nothing more |
| the player's word (not built; the ledger knows the source) | "Marked by you · 6 Oct" | NOT ANY MORE; FORGET ALL MARKS |

Rules (`StashLedger`, `StashMarks`): one mark per item, the newest observation wins; counts are "at least" (two
overlapping pages never double a stack: within a pass an item's count is the most any one screenshot showed); a row
that needs more than was seen says its part ("3 of 7") and keeps the amber line for the rest ("4 more: Mechanic LL1
barter · …"); a row the mark covers has no amber line; marks never change the planner's order or the kit cue (a wrong
mark costs a glance, never a raid); the game's words throughout (stash, key tool, found in raid; German keeps Stash,
Key Tool, FiR, as the glossary does); the newest pass dates a mark, marks never age into silence, and "not in your
screenshots of …" is said in words, not colour (muted keeps its one meaning). **What a quest used up comes off**
(owner, 2026-10-11: "how do we deal with the inventory depleting over time and info going stale?"; of a hand-over
rule, a manual reset and a timed expiry they took the first two, the expiry being against "never into silence"):
when the log says a quest completed, what it was brought to use up (planted, marked, handed over: the quest card's
BRING counts, `Planning.ToPlan`) comes off the marks observed before the completion, from any source, and a mark
goes at zero; one observed after it stands, since the player had the item again. The mark keeps its source and date:
it is still that observation, less what the quest took. Once per completion, however often the log is read back
(`StashLedger.HandOver`, the file's `handovers`); a completion the log never showed (implied by a later quest's
start) has no time and takes nothing. Nothing else depletes: a death in a raid, a sale, a craft are invisible to
Shturmap, and a full pass over the stash is the reset for those. The surface:

- **A BRING row's third line**, in ink after the note line, with the stash grid glyph (Segoe Fluent `E80A`, used
  nowhere else): the mark. The amber "where to get" line under it says where to get the rest, or nothing.
- **The need cells** beside a quest's name: a 2 px ink bar under a cell with a mark; the cell's tooltip carries the
  mark's words.
- **The item card:** a STASH section above GET IT with the mark (with the count and the time: "2 seen in your stash,
  found in raid · 5 Oct, 21:40") and NOT ANY MORE, the one click that retracts it, no confirmation. No section when
  nothing is known (nothing to act on; I HAVE IT, the player's word, is not built: "only if gaps remain").
- **The notice** after a menu screenshot that showed something a quest needs, "Seen in your stash: 3 keys your quests
  need." (owner, 2026-10-19: the desk opening with the reading is the feedback; until then the cue SEEN IN YOUR STASH
  pictured the items with their counts, and the notice stood in for it only while a replay played or QUEST COMPLETE was
  up; principle 11), and **the glow**: the marks that just arrived (from a screenshot or the log) glow in on the BRING
  rows, the need cells and the quest card's BRING rows, gold easing to ink over a second, the row flashing once
  (`Controls.Glow`). Nothing is said for a screenshot that showed none.
- **One note** under BRING on the open card, until the first mark from a screenshot: "Screenshot your key tool in the
  game: the keys your quests need get a mark." Then gone.
- **One setting** (§2) and a line in tour chapter 1's "reads" list; under the setting, **FORGET ALL MARKS** (owner,
  2026-10-11: "a reset"): one click, no question, as NOT ANY MORE (a stash pass brings the marks back); every mark
  goes, and what the log said up to then stays forgotten at every later start (`StashLedger.ForgetAll`, the file's
  `forgottenAt`); a notice counts what went. Muted and dead while there is nothing to forget.

Cards behave like the nested tooltips in Crusader Kings III:

1. **Hover**: rest the pointer on a quest, key or item for 0.65 s on a rail row or map marker (the study log: 62 %
   of cards opened from lists closed within a second, opened by a pointer passing over), 0.4 s on a row on a card,
   and its card appears beside it, see-through (80 %) so it doesn't hide the map. It stays while the pointer is on its
   subject or on the card, and goes 0.35 s after it leaves both. Moving down a list swaps it after 0.12 s.
   **On its way to the card the pointer is only passing over** (`CardAim`; owner, 2026-10-04: "When you then move
   the mouse to the right to mouse-over the quest and you are too slow another quest opens"). A card opens beside
   its row, so the way to it leads across things with cards of their own: the other glyphs of a map's row, a need
   cell, the rows below. While the pointer keeps heading for the open card (it moved at least 3 px, came nearer,
   and its line carried on meets the card, with 16 px of slack), what would replace the card waits and what would
   close it waits, each looked at again every 0.3 s; a pointer that rests on the other thing, or turns away, gets
   that thing's card. It is the direction that counts, not the speed (the "safe triangle" of nested menus). A held
   card is spared the same way. Checked in the app by script on 2026-10-04: onto another quest while heading for
   the card, the card was still there 0.2 s later and the other quest's 0.6 s after that; not heading, it had
   swapped within 0.2 s; left into a gap and still heading, it was there after 0.45 s, where a resting pointer
   had lost it.
2. **Held**: a click holds it: on the quest (row or marker), or anywhere on the card. A held card is solid with an
   amber border and stays while the pointer is near it. It closes on a click on nothing in particular (bare rail
   or map), Esc, another click on its quest, a full rest on something else that opens a card in its place (0.65 s
   from the rail or the map, 0.4 s on a card), or the pointer moving more than 240 px away from both the card and
   what it was opened from, its row or its marker (`CardReach`; 2026-10-04: measured from the card alone, a click on
   the left of a row held a card that closed with the next move of the mouse). Nothing holds by itself (owner,
   2026-10-01: the timed hold was dropped). And every card closes at a step into a raid, when
   it begins to load and when it starts (`WhileInRaid`; review of 2026-10-04: a card held in Plan stayed over the
   map through the loading and the whole raid, with nobody at the mouse to click it away). A card opened during
   the raid stays as any held card does.
3. **Nested**: on a card, rest on a key, an item or a quest (UNLOCKS, NEEDED FOR) and that one's card opens beside
   it, and so on; a click holds it too. Moving back to an earlier card closes the unheld later ones.

Popped-out cards were removed on 2026-10-09 (owner): picks put the chosen quests first in the raid card, with live
distances, in the window already on the second monitor. The `pinned.cards` row an older database holds is no longer
read.

A card is never taller than the room there is in the window. A longer card (Collector, a quest with many
objectives at 150 % scaling) scrolls inside, so UNLOCKS and the wiki link stay in reach (2026-10-04: it was cut at
the window's edge). **Where a card opens** (owner, 2026-10-15: "When selecting a quest at the lower border of the
screen it gets cut off"; `Rules.CardPlace`): beside its row, its top 10 DIP above the row's, and up as far as its whole
height needs to stay inside the room with 8 DIP to spare, never above the room's top; at most the room's height less
16 (its MaxHeight), past which it scrolls inside. A card that grows while it is open (held, with its NOTES field; a
note added; a tick's line) moves up by the same rule, never down (2026-10-15: placed by a row near the window's foot,
the field it gets when held lay below the edge, where nothing scrolls it into view). The room is the window, less what
of it lies off its monitor's work area: a window that reaches behind the taskbar or past the screen's lower edge keeps
its cards in sight. In quiet runs at 900×560 and 1600×900, in English and German, a card held from the lowest row of
the rail (Insomnia at the foot of Streets' card, held, on hover, and held with five notes) already stood inside the
window before this; the one cut that rule could leave was the screen's, so that is the change (a guess at the owner's
window, which was not seen).

Rows on a card take part in linked highlighting (pointing at a key lights it up in BRING and the quest on the map),
but don't light up for their own card's quest, or the whole card would glow. The card's body keeps its subject in
focus all the same, without a tint: the quest on a quest's card, the item and its quests on an item's card
("Visual language", linked highlight; 2026-10-04). A BRING row that stands for several items is each of them. An objective's row is its objective:
pointing at it marks that objective within the quest (on the map only its places pulse; in the raid card its line
takes the tint), and the row lights up when one of its places on the map, or its line in the raid card, is pointed
at (the review of 2026-10-04). Rows that show the same item still light together, as before.

### Picks: the quests for the coming raid

Pointing highlights for as long as the pointer stays; the quest's pen (on its rail rows and its card) **picks** it
for the coming raid (owner, 2026-10-03: "you typically bring items for specific quest or want to run a specific
quest. Therefore you likely want to have something like the current 'mark quest' thing, but for all the quest you
want to tackle in the map. Still it should show all other quest markers."). It grew out of the single "kept"
quest (owner, 2026-10-01: keep a quest's markers easy to find while you look away): the same pen, the same look,
now for as many quests as you like, and the pen keeps its one meaning, "keep lit". A click on the quest itself
still only keeps its card open (two clicks, two meanings).

- **One click per quest while planning, none in the raid.** Picks hold from Plan through loading into the raid
  and across restarts (the settings, `picks.<mode>`; PvE and PvP keep their own, `QuestPicks`).
- **Picks belong to a map** (owner, 2026-10-04: "Store the selected quests per map and persistent between
  sessions"). A pick is "this quest, on this map": what is picked for Customs waits there while Streets is planned
  with picks of its own, and comes back when Customs is shown or loads. Until then picks were one set for all
  maps: a quest with work on five maps was picked on all five and brought all five to the top of Plan, and the
  colours of one map's picks were used up by another's. Now:
  - *The pen picks for the map the rail's quests are of*: the map whose card is open in Plan (the map shown; the
    best suggested one when the map shown isn't among them), in a raid the raid's map. One click as before; there
    is no map to choose. A quest with work on several maps is picked where the player picked it, and its pen is
    lit only there.
  - *Each map shows its own*: its picks first in its card and in its row of Plan's list, the map draws the picks
    of the map drawn, and a map counts for Plan's order only the picks made on it.
  - *Each map hands out the colours from the first* ("A colour per pick", below), so a map's two or three picks
    always get the four colours that are easiest to tell apart, however many quests are picked elsewhere.
  - *CLEAR PICKS clears the map shown*; other maps keep theirs.
  - *A map and its variants share their picks* (Ground Zero and Ground Zero 21+, Factory by day and by night:
    `Planning.PickKey`, the map as the player picks it in the game).
  - *Kept as one setting per mode*, `picks.<mode>`: "map|quest:colour" entries. Picks saved before 2026-10-04 had
    no map: each becomes a pick on every map its quest has work on, as it was shown until then, once the data is
    loaded (`QuestPicks.Adopt`); one whose quest is no longer active is let go.
- **No upkeep.** A pick stays until the log reports the quest completed or failed (it leaves every map's picks by
  itself), its pen is clicked again, or CLEAR PICKS (beside NEXT RAID, outside raids). Picks don't clear at raid end: a quest often
  takes several raids, and a plan that empties itself would have to be made again each time. Esc never touches
  picks (it closes the cards): a key that throws a plan away would be too easy to hit. Only an active quest can be
  picked; a quest whose state the log doesn't tell stays picked.
- **The look.** Picks have colours of their own, one per pick ("A colour per pick", below): owner, 2026-10-01, gold
  among gold didn't stand out. Until 2026-10-04 that was one cyan for every pick (`Kept`, now the dev build's icon
  only), chosen as the one hue nothing else on the map uses, the artwork included, and apart from gold with any
  colour vision. Their rows, and the BRING rows that serve them, keep a tint of the pick's colour; on the map their
  markers take it, at their rest size, inside a steady ring of it on a dark band that lies under every symbol
  (until 2026-10-05 they grew to 14 px, and what was pointed at to 12, over neighbours set apart for 10; "Map
  drawing", *Symbols that cover each other*), their zones take it, and a dashed line runs from your last fix to the
  nearest place of any pick, in that pick's colour, with the distance on its plate (the raid card's NEXT names the
  same objective). Their places
  out of view get chevrons. Picks hold still: a marker pulsing all raid would be motion at the edge of the
  player's eye; what the pointer is on pulses.
- **A colour per pick** (owner, 2026-10-04: with several picks "it is then, however, difficult to distinguish on a
  very quick look which items belong together"; from a panel of five ways drawn by the real map, "the A color per
  pick ... is the best solution. Make sure the 'opt' icons attached to it and others change colors as well"). The
  colours are eight soft ones, the first eight of ColorBrewer's "Set3", a table made for telling categories apart
  on maps: aqua, salmon, blue, lime, pale yellow, pink, lavender, orange (`Pick1` to `Pick8`;
  `MapRenderer.PickColors`); a ninth pick takes the first again. The ring and the larger size say "picked" without
  colour. **How they were chosen** (owner, 2026-10-04: "When selecting many quests the system chooses currently
  very similar colors ... Maybe even different shades of pastel colors helps and hand-selecting a few. Make a panel
  of the best options possible"; then, from that panel, "Go for D"). The first version had four strong colours
  (cyan, pink, lime, orange) that repeated from the fifth pick. Six palettes were drawn by the real map with ten
  picks and measured with CIEDE2000, normally, as seen with red-green colour blindness, and against the map's own
  colours: the four strong ones were 39 apart at their closest but 6 for colour-blind eyes; five colours can be
  kept 25 apart (16 colour-blind) and were suggested with a second ring from the sixth pick on; this table's eight
  are 14 apart at their closest (pale yellow and lime), 2 for colour-blind eyes (orange and lime), and its orange
  is 13 from the quests' gold. The owner chose the soft table: calm on the map, and eight different colours. The
  order picks take them puts the four easiest to tell first (aqua, salmon, blue, lime) and the orange last, so two
  to four picks, the usual number, get the best of it. Everything of a pick has its colour: marker, ring, the badges at its
  corners (floor, count, "OPT", "?"), its name, its zone, its chevron at the edge, the padlock of a door it needs a
  key for (the first pick's, where several need it), the guide line and plate when they lead to it; and in the
  rail the tint of its row and of the BRING rows that serve it, its pen, its glyph in a map's row, and the frame
  of its items in the loading cue. A pick keeps its colour until it is unpicked, across restarts
  (`QuestPicks.Slots`, kept with the pick): unpicking one quest recolours none of the others on its map, and the
  next pick takes the colour that came free. The other ways on the panel: a number per pick (one colour keeps one
  meaning, but it must be read), a line joining a pick's places (reads as a route nobody knows), the names alone
  in colours (markers without a name get nothing), and numbers in coloured circles. What it costs: no single
  colour means "picked" any more (the heading PICKED is in the headings' grey, and the dev icon keeps the old
  cyan, `Kept`), soft colours stand out less from the map than the strong cyan did, and with red-green colour
  blindness several of the eight look alike: the names, and the rows' order, still tell them apart.
- **Nothing else steps back for picks.** Every other quest marker stays at full strength (the owner's "Still it
  should show all other quest markers"); only pointing at something steps the rest back ("Stepping back"), and
  then picks don't step back either: they are the plan for this raid, as much as the ways out.
- **The rail.** A map's card shows its picks first of its quests (the daily and weekly tasks' OPERATIONAL under them,
  above COMPLETE, since 2026-10-19, §4, *Operational tasks*), as a group of their own headed PICKED (in the headings'
  grey), then COMPLETE and PROGRESS without them, with the effort hairlines drawn anew (`Planning.Sections`). A pick that only
  progresses here keeps its note ("2 of 5 objectives here"). Compared on 2026-10-03 with picks sorted first inside
  COMPLETE and PROGRESS: there they scattered over two sections (a progress-only pick ended up at the bottom), while
  a group of their own says "this raid's work" at a glance. BRING lists what the picks need first, then a hairline
  and the rest (`Planning.BringOrder`). A map with picks is suggested first, most picks first: the player's plan
  before the planner's. That holds whatever the map's rank: the maps with picks all come first, then every other
  map with work on it in the planner's order (`RaidPlanner.Rank`; until 2026-10-09 the planner's best filled up to
  four, and until 2026-10-04 the list was cut to four first, so a pick on the map ranked fifth never showed). A folded card
  shows its picks' glyphs first, each in its pick's colour, then a hairline. The raid card starts its quests with PICKED
  (OPERATIONAL under it, as in the open card), nearest first, and
  NEXT is the nearest objective among the picks.
- **Zero picks is the app as it was:** no PICKED group, no reordering, no line on the map.
- The study log records `pick` and `unpick` (with how: pen, done, …) and `picks.clear`, when it is on.

### Extract requirements

Shown under each extract in the Raid rail, short, in gold, with the item's icon when one is handed over (or the key
to its door when nothing is), and under that, muted, what is so of it:

| from | example |
| --- | --- |
| `transferItem` in roubles, dollars or euros | "Pay 5,000 ₽" (car V-Ex), "Pay €2,400" (Icebreaker heli) |
| `transferItem`, any other item | "Hand over Note with code word Onyx" (secret extracts) |
| internal name `Alpinist*` / `RedRebel*` | "Red Rebel ice pick and paracord, no armored rig" (Cliff Descent, Mountain Pass, Climber's Trail) |
| "(Flare)" in the English name, `sniper` as a word of the internal name (`E9_sniper`, `customs_sniper_exit`), or a flare exit kept by hand (`ExtractRules.FlaresByHand`: Icebreaker's helicopter, `Heli`, by id) | "Fire a green signal flare there"; the helicopter's says it before its fare ("… · Pay €2,400") |
| "(Co-op)" in the English name | "Co-op: a PMC and a player Scav leave together" |
| the hand-kept table of the extracts a switch gates (`SwitchExits`), else the data's `switches` where they tell an extract apart | "ZB-013 Power Switch first"; where it takes several steps, how many: "Mall Main Power Switch first · 4 steps" (Interchange's Saferoom Exfil) |
| the hand-kept facts (`ExitFacts`): a key at the exit's door, with the door's lock in the data (Factory's and Night Factory's Cellars and Med Tent Gate, Customs' ZB-1013, Woods' ZB-014, Interchange's Saferoom, whose keycard is a step of its chain too) | "Factory emergency exit key for the door", the key's icon beside it and the key in BRING (`ExtractRules.Items`); the key's name is the data's |
| `ExitFacts`: no backpack (Reserve's Sewer Manhole, The Lab's Ventilation Shaft) | "No backpack" |
| `ExitFacts`: single use, and how many leave together (the car extracts: up to 4, Shoreline's 5; the Saferoom and The Lab's Cargo Elevator, single use only) | after the rest of the line: "Pay 5,000 ₽ · single use · up to 4 players", "… · single use" |
| `ExitFacts`: open in some raids only, muted (the 13 the wiki marks "not always available": Customs' Old Gas Station for a PMC and Dorms V-Ex; Woods' Bridge V-Ex, RUAF Gate, ZB-014, ZB-016; Shoreline's Pier Boat; Interchange's Power Station V-Ex; Reserve's Armored Train; The Lab's Hangar Gate and Parking Gate; Streets' Courtyard and Primorsky Ave Taxi V-Ex) | "may be closed", until the game's extract list was read this raid |
| `ExitFacts`: a time it keeps, muted (Reserve's train comes with 16 to 12 min left and Lighthouse's with 20 to 15, each staying 7; The Labyrinth's The Way Up opens 15 min in; Reserve's Bunker Hermetic Door stays open 4 min after its lever; The Lab's Sewage Conduit's water sinks in about 80 s after its pump) | "Train from 16 min left · stays 7 min", "from 15 min in", "open for 4 min after the lever", "~80 s after the pump" |
| transit `conditions` | "TerraGroup Labs access keycard required (1)" |

**Gold is what to bring or do, muted what is so of the exit** (owner, 2026-10-17). The gold line says what leaving
takes: an item, money, a key, a switch, a flare, a partner, the backpack off; single use and how many may leave
together go on it after the rest, as they qualify what it takes (a car's fare pays for a group). The muted line under
it says what is so of the exit, its availability and its time: "may be closed", a train's window, The Way Up's hour,
how long the bunker's door stays open, how long the conduit's water takes. An exit the game's list leaves out shows
neither (what it would take doesn't matter). **"may be closed" goes once the list was read** this raid: the list's
lit, "??:??:??" and struck-out marks say it then (`ExtractRules.NoteLine`, `ExitState`). **The trains' and The Way
Up's words follow the raid card's clock** (its MIN IN and MIN LEFT, the raid's length minus the time since its start
line, `Rules.RaidTime`): the rail words them again at each new minute, and once a window has passed (the latest
train has gone, at 5 min left on Reserve and 8 on Lighthouse; The Way Up is open, from 15 min in) the line steps back
to 45 % (the stepped-back measure, "Map drawing", "Stepping back"). For a Scav, who joins a raid under way, and outside a raid, the
clock isn't the raid's: the words stand as they are. **No cue for a train's arrival** (deliberately; principle 11): a
cue is for the view changing by itself, the train's coming is a guess within four minutes that no log line states, and
the line already says when to look; a cue at the window's opening was weighed and left out.

**The facts are kept by hand** (owner, 2026-10-17: "exit facts" as words on the exit's Raid rail row, nothing new on
the map). The verification of 2026-10-17 read each map's "Extractions" on the wiki (escapefromtarkov.fandom.com:
faction, always available, single use, requirements, notes) against tarkov.dev's data, and none of these facts is in
it: its extracts carry `id`, `name`, `faction`, `switch`, `switches`, `position`, `outline`, `top`, `bottom`,
`transferItem` and `size`, nothing more; the keys at exit doors are only locks near the exit (4.6 to 13 m away), which
no field ties to it. `ExitFacts` (in `Shturmap.Data`, beside `SwitchExits`) keeps 30 rows the way `SwitchExits` does,
by the map's `normalizedName` and the extract's internal name, and only for the extract of the id it has (Night
Factory and Ground Zero 21+ have rows of their own, as their ids are their own; The Lab (Dark)'s borrowed extracts
keep The Lab's ids and are found under The Lab's rows, *The data's defects* below); a key's row has its door's lock
id too. `shturmap-cli bring` (EXIT FACTS) and `ExtractRulesTests` against the cache say where a row no longer finds its
extract, or its lock with its key (STALE: its words go unsaid; the test fails), and where the data starts carrying a
fact (DATA: a field on the extracts that wasn't there on 2026-10-17, a faction the data now gives itself; the test
warns): docs/UPDATES.md. Not built: Lighthouse's V-Ex, Interchange's Smugglers' Tunnel and Terminal's boat have no
place in the data, and placing them by hand goes against the data-only rule; where the green flare for Mira Ave
spawns, Raiders at The Lab's and Reserve's switches, and the per-player fare (docs/NEXT.md).

**The data's defects, by general rules** (owner, 2026-10-17: rules rather than hand fixes, so a fix upstream needs no
undoing; the report to tarkov.dev is still to do, docs/NEXT.md). `ExtractRepairs` runs as the data loads
(`GameDataLoader`), so the map, the rail, the extract list's matching and the rules see the same extracts, and each
rule does nothing where the data is right:

- **An extract outside its map that another map has is dropped**: outside the map's bounds (`svgBounds`, else
  `bounds`) and with the internal name of an extract of a map not drawn with the same artwork, it is a copy left
  behind. Ground Zero carried three, at Woods' and Streets' places: UN Roadblock (429 m out), Scav Bunker (582 m) and
  Pinewood Basement (15 m). The bounds alone would drop six real extracts as well, 1.5 to 16 m past the edge (Factory's
  and Night Factory's Courtyard Gate, Customs' Railroad Passage, Woods' RUAF Gate, Interchange's Path to River,
  Icebreaker's helicopter), and no margin tells Pinewood Basement's 15 m from Railroad Passage's 16: those stay, and
  the audit lists them.
- **A variant's base map** is the map whose entry in tarkov.dev's maps.json lists the variant among its `altMaps`, so
  is drawn with its artwork by tarkov.dev's own word: Factory for Night Factory, Ground Zero for Ground Zero 21+, The
  Lab for The Lab (Dark) (`ExtractRepairs.BaseOf`). A map the app draws with another's artwork only because its name
  starts with that map's (`GameData.DefinitionFor`'s fallback: Ground Zero Tutorial, which maps.json lists nowhere) has
  no base, and borrows nothing (coordinator, 2026-10-17: the rule follows the borrowed artwork, not a name).
- **A variant's extract with no side takes its base map's**: its extract of the same internal name, the nearest where
  it lists two (Factory's Gate 3, a Scav's and a PMC's under one id). Without one there it stays shared, as before.
  Night Factory's nine and Ground Zero 21+'s six had none, so a Scav got PMC rows and a PMC Scav rows.
- **A variant with no extracts takes its base map's**, and its switches where it has none: The Lab (Dark), where the
  Factory and Streets transits lead, gets The Lab's seven extracts and fifteen switches; Ground Zero Tutorial, empty
  too, gets nothing. The borrowed extracts keep The Lab's ids, so `SwitchExits` and `ExitFacts` find them under the
  base map's `normalizedName` (`GameData.ExtractsFrom`, `ExtractMapName`): The Lab (Dark)'s elevators, gates and
  conduit carry The Lab's switch lines, badges, lit steps and facts (coordinator, 2026-10-17: the layout is the same
  with the lights off; if the Dark variant's power turns out to differ, a row can be dropped for it).
- **One side by hand**: Customs' `ex_customs_pmc_rail_milbase` (Railroad to Reserve) is a PMC's, which the data tags
  Scav, so PMCs got no row; the row is in `ExitFacts` (`Faction`).

`shturmap-cli bring` prints what each did (OUTSIDE dropped and kept, FACTION FROM BASE, EXTRACTS FROM BASE, FACTION BY
HAND), and `ExtractRulesTests` against the cache warns where one had nothing to do (a fix upstream).

**Green, not red** (owner, 2026-10-17: "Fix"). The verification of 2026-10-17 read the wiki's "Extractions" of all
six flare exits (escapefromtarkov.fandom.com) and its RSP-30 green's "Usage": each takes a green flare fired upward in
the signal area (the RSP-30 green, `ExtractRules.GreenFlare`, or a 26x75mm green from a signal pistol). Until then the
line said red, and BRING put the red RSP-30 on a quest's "to leave through" row (Cease Fire!). A quest that itself
needs a red flare keeps it: that is the quest's item, not the exit's. Icebreaker's helicopter is a flare exit too,
which its name doesn't say (`Heli`, "Helicopter"): the one row of `ExtractRules.FlaresByHand`, by internal name and id.

**The switches are kept by hand** (owner, 2026-10-17: "Some exits still require specific actions, for example one exit
in interchange requires the power to be switched on. This is currently not represented in the map at all and should
be added as information"; from a panel of three: "A", a badge on the exit and the link on pointing; "Map drawing",
*Switches*). The verification of 2026-10-17 found that tarkov.dev's `switches` never say it: they are listed on
Customs and The Lab only, and there on every extract alike (all 28 of Customs' the one ZB-013 lever, all 7 of The
Lab's the Med Elevator's three buttons), so each fell under the rule for a switch on most of a map's extracts and
showed nowhere; Interchange's Saferoom and Reserve's D-2 and bunker list none. The name check that was to rescue the
right one never matched ("ZB-013 Power Switch" doesn't hold "ZB-1013", "Med Elevator …" not "Medical Block
Elevator"), so the example "ZB-013 Power Switch first" above had never shown. Now `SwitchExits` (in `Shturmap.Data`,
which the map's code and the rules both reach) keeps the ten extracts the verification found, by the map's
`normalizedName` and the extract's internal name, and only for the extract of the id it has: Customs' ZB-1013;
Interchange's Saferoom Exfil (the Mall Main Power Switch, the Saferoom Exfil Unlock Switch, the Object #11SR keycard's
lock, the Saferoom Exfil Switch); Reserve's D-2 (its power switch, then its door switch) and Bunker Hermetic Door; The
Lab's Cargo, Main and Medical Block Elevators (power, call, extract each), Hangar Gate, Parking Gate and Sewage
Conduit (the pump button, then the water level switch). Each step is a switch's or a lock's id, in the order of use,
and every one has a position in the data; the names shown are the data's. The line names the first step, and "· N
steps" where there are more; the map shows them all while the extract is pointed at. The data's own `switches` still
count for an extract the table doesn't have, where they tell it apart (none on 2026-10-17; `SwitchExits.FromData`,
with a switch that unlocks one of them before it). The name check is gone. `shturmap-cli bring` and
`ExtractRulesTests` against the cache say where a row no longer finds its extract or a step (it would lose its badge
and line; the test fails) and where the data now names switches of its own for a table extract (a fix upstream to
compare with the row; the test warns): docs/UPDATES.md.

Internal extract names are kept from the payload before translation (`GameData.ExtractKeys`); transit conditions
are translated although the payload's translation list misses them. **The rules never read the name in the game's
language** (review of 2026-10-04): they looked for "(Flare)" and "(Co-op)" in the translated name, and German has
"Mira-Allee" for "Mira Ave (Flare)", so the rule was lost there. The loader keeps each extract's and switch's
English name by id beside the translated one (`GameData.EnglishNames`), and `ExtractRules` reads those and the
internal names only; a test runs every exit of the cached data through the rules in English and in German and
expects the same. Items' English names are kept the same way when the data is in another language
(`GameData.EnglishItemNames`): an operational hand-over's quoted names are the English game's words, matched against
them whatever the app's language (QA, 2026-10-19). `sniper` counts as a word of the internal name, between
underscores, not anywhere in it: Customs' "Sniper Roadblock" is an ordinary exit and was told to fire a flare.

### Keyboard (window focused only)

| key | action |
| --- | --- |
| F | show my position, once |
| Shift+F | Follow my position on / off |
| + / − | zoom in / out (following, about the player); "+" with or without Shift, and the number pad's keys |
| 0 | show the whole map (following stays on: the next position centres on you at that zoom) |
| PgUp / PgDn | show the floor above / below |
| Esc | close the cards (it never drops picks); with no card open, the stash desk back to Plan, its pinned cards kept (*The stash desk*); then let go of a trader's held focus (*The trader strip*) |
| F1 or ? | help |
| Ctrl+, | settings |
| → or Space, ←, Esc | while the tour is up (*The tour*): the next chapter (Enter too), the one before, end it. Every other key does nothing then; with the tour closed, Enter is the focused button's |

While the stash desk is up the map's keys (F, Shift+F, + and −, 0) leave the map, out of sight, alone, and PgUp and PgDn
scroll the desk's page (*The stash desk*, *As built*).
Keyboard accelerators sit on the window root with their placement hidden; WinUI would otherwise show the first
one's key as a tooltip over the whole window. "+" is a key of its own on some keyboards and Shift with "=" on
others (US); both are one virtual key, and a shortcut without Shift never saw the second, so "+" didn't zoom on a
US keyboard (`ZoomKeys`; review of 2026-10-04).

**A text box has the keys** (owner, 2026-10-15, with the quest card's notes). While one has the focus, a held card's
note field, none of these shortcuts fires: its keys are text, and Esc is the field's (it clears it). The Report dialog
already kept its keys this way (Esc closes it there). Every shortcut is an accelerator on the window's root or its
one `CharacterReceived` ("?"), and each lets a key go on, unhandled, while a text box has the focus
(`MainWindow.TypingText`, asked of the window's XAML root, which the cards' popups share).

**The rail's rows by keyboard** (Down and Up through the rows, Enter as a click on one, P as its pen; the review of
2026-10-04, E6: the linked highlight needed a pointer) were removed on 2026-10-09 (owner): this section itself said
nothing in the app needed them. The tour keeps Enter as its next chapter, and Up and Down do nothing while it is up;
otherwise those keys go to whatever has the focus (a focused MAP list switches the map with its arrows, as a list
does).

Mouse: drag to pan (following stays on: the next position brings the view back), wheel to zoom at the cursor
(following with the view on the player, about the player), double-click to
zoom in, point at anything to see what belongs to
it, click a quest (in the list or on the map) to keep its card open, click its pen to pick it for the coming raid,
click the edge badge to show your position.

## 5. Quest taxonomy

Every objective gets one type, from tarkov.dev's objective `type`. The game gives each quest one hand-assigned
type on its Tasks screen (Elimination, Pickup, Exploration, Discovery, Completion, …); that type is not in any
public data and can't be derived reliably (a check against the wiki matched 62.5 %), so Shturmap types objectives
instead and borrows the game's names and look where one fits, so the labels feel familiar:

| type | glyph | objective types | in raid? |
| --- | --- | --- | --- |
| **Elimination** | crosshair (the game uses a skull) | `shoot` | yes |
| **Exploration** | magnifier (as in the game) | `visit` | yes, at a place |
| **Pickup** | open hand (the game has a hand) | `findQuestItem` | yes, at a place |
| **Place** | push pin | `plantItem`, `plantQuestItem`, `mark`, `useItem` | yes, at a place; needs an item |
| **Find in raid** | box | `findItem` | yes, anywhere; found in raid (FIR) only where the data's `foundInRaid` says so |
| **Survive** | runner | `extract`, `experience` (an in-raid health condition, not XP) | yes |
| **Trader** | handshake | `giveItem`, `giveQuestItem`, `sellItem`, `buildWeapon`, `traderLevel`, `traderStanding`, `playerLevel`, `skill`, `taskStatus`, `dialogue`, `globalVariable`, anything new | no |

A quest's type is that of its most common in-raid objective type (ties: Elimination, Pickup, Place, Exploration,
Survive, Find in raid); a quest with no in-raid objectives is a Trader quest. This is derived, not the game's own
label. Never use the game's icon artwork; the glyphs only echo it.

**The types' icons are filled shapes from Phosphor Icons** (MIT; `THIRD-PARTY-NOTICES.md`), chosen by the owner on
2026-10-04 from a panel of six sets, each tried in the real app: "B Solid Other Pictures but with the exploration
icon from A Solid and with Survive icon from A solid". Until then they were thin outlines from Windows' icon font
(Segoe Fluent Icons) and a crosshair of the app's own; inside a map marker, where the glyph is about 10 px, those
were faint, and filled shapes read at that size. All seven are the family's "fill" weight, except the magnifier,
which is its "bold" one (a filled lens reads as a dot). Each is the family's path as published, in `Glyphs`, drawn
filled and fitted by its own bounds: 15 px in the lists (`KindGlyph`), the marker's glyph size on the map. One
drawing serves both, so the two can't drift. Requirements keep Windows' font: a key glyph `E8D7` for keys, a
briefcase `E821` for items to bring, the padlock and the power symbol on the map; on Windows 10 that font has to
be installed for those (README).

The type is named after the common case. A `findItem` whose item may be bought (`foundInRaid` false) keeps the type
and its glyph, but nothing says "found in raid" of it: the planner, the plan's order and the item card go by the
data's `foundInRaid` (§7; the review of 2026-10-04 found the planner treating every `findItem` as found in raid).

### Quest synopsis

Under each quest's name in Plan's COMPLETE and PROGRESS, one quiet line says what the quest asks on that map:
"Get valuable item", "Kill Scavs ×10 with M4A1, M16, ADAR, or TX-15", "Mark first LAV III, Stryker, second LAV
III" (owner, 2026-10-02). Why: before a raid the owner pointed at every quest row to see what it wanted (the study
log: 210 quest cards opened from the rail in the menus over two evenings, 188 of them in back-to-back scans, and 53
looks at 12 quests in one 12-minute stretch). The owner chose the name first and the synopsis under it over the
synopsis in the name's place: the name is what the trader, the wiki, the BRING rows ("for Dandies") and the map's
labels use, so it stays the thing you recognise.

- **Made from the loaded data, without AI** (owner: it must keep up with tarkov.dev's quest updates without help).
  `QuestSynopsis` (Core) works on tarkov.dev's objective texts and count fields for the objectives the planner
  counts on that map (in-raid, not optional; hand-overs are left out). Nothing is stored: a new quest gets its line
  as soon as the data has it.
- **It only subtracts.** It may (1) replace a known verb phrase at the start from a fixed table ("Locate and
  obtain" → Get, "Locate and neutralize" and "Eliminate" → Kill, "Locate" → Find, "Find the item in raid:" → FIR,
  "Survive and extract through" → Extract through, "Use the transit to" → Transit to, and the term "PMC operatives"
  → PMCs); (2) remove known filler: lowercase articles (not after "of"), this map's name and lists that hold it
  ("on Woods, Ground Zero, or Customs"), "with an MS2000 Marker" (it is in the row's bring cells), "at the
  specified spot", "from the location", and the place of an objective whose marker is on this map ("in dorm room
  203"), together with a word leading into it ("hidden"); (3) add the count field as "×N", before a kill's
  conditions or at the end, as the game and the BRING rows show counts. Nothing else. A reworded text then gives a
  longer line, never a wrong one; a text with no known verb is shown as written, less the map's name.
- **Never dropped**: conditions and exclusions, word for word (brackets, "excluding", "without", "only", "while",
  "using", "wearing", "during", "in one raid", headshots). A place that holds one stays whole. A number the text
  gives that the count field contradicts is left out, and so is the count (Job for a Patriot says 20 PMCs, its
  count 10). A place stays when cutting it would leave one word ("Stash package at laboratory storage room") or
  when two objectives would then read the same (Spotter's two sniping positions).
- **Merging**: consecutive objectives with the same verb share it, and words all their objects share are said once
  ("Stash AK-50 body, handguard, barrel"; "Kill Knight, Big Pipe, Birdeye (in one raid)"), never across a count and
  a shared ending only after short names ("first, second, third yellow minibus").
- **English only.** The rules are written for English texts; in another game language the row shows the name alone.
- **Checks.** `QuestSynopsis.Problems` checks a line: every word is in the objective's text or the table, or is
  its count; every condition of the text is kept; nothing stops mid-phrase. A test runs it over every quest on
  every map in the local tarkov.dev cache (PvE and PvP; skipped without a cache, since the quest texts are
  tarkov.dev's and BSG's and stay out of the repository). On 2026-10-02: 614 PvE and 620 PvP rows, no breaks, no
  fallbacks; a quarter run past two lines and end in "…" (long item lists, kill conditions).
- **After a tarkov.dev or game update** run `shturmap-cli synopses pve` and `… regular`: they list every Plan row
  with its line and flag FALLBACK (a text with no known verb), LONG (more than two lines at the row's width) and
  BREAK (a rule broken). Add a verb or filler phrase to the table only when it subtracts.

## 6. Raid requirements

- **Keys**: each objective's `requiredKeys` (each entry a list of alternatives, shown as "A or B"; several entries
  are several doors), and of the quest's `neededKeys` for that map only the keys none of its objectives names.
  tarkov.dev's quest-level list is flat: per map, the keys the quest's objectives name (in its data of 2026-10-04
  exactly that, for all 58 PvE and 57 PvP quests with keys), so it can't say whether two keys are both needed (the
  dorm room and the cabin of one quest) or either will do (two keys to one room); the objectives' lists can. Until
  2026-10-04 both were listed, so "Room 306 key" and "Room 308 key" stood as two rows where either opens the room,
  and an objective's line repeated every key of the quest (`RaidPlanner`'s requirements, `Planning.QuestOnlyKeys`).
  A key needed alone also serves any "A or B" row that holds it: that row goes, and its quests are named on the
  key's row ("key for Quest A, Quest B"; they used to vanish with the row).
- **Bring**: items a Place objective consumes (`items` of `plantItem`, `markerItem` of `mark`, `useAny` of
  `useItem`, the quest item of `plantQuestItem`), with counts summed per item.
- Shown aggregated per map in Plan, and inline on the objective in Raid ("Key: Dorm room 114 key", "Bring: MS2000 Marker"),
  on every objective that needs it.
- Shturmap lists what is needed, not what is missing; what it learned of having an item is a dated mark on the row,
  never a claim (§4, "What you have").

## 7. Raid planner

For each map (variants sharing artwork, like Ground Zero 21+, count as one), using active quests only, and of each
quest the objectives that are left: the logs don't report objective progress, so every objective counts except
those the player ticked as done (`Planning.Open`; §4, "Quest cards", *Ticks*):

- An objective is **doable** on a map if it names that map or has a place there, or names no map and is Kill,
  Collect or Survive. It is **tied** to the map if it names the map or has a place there.
- A quest counts for a map only if at least one of its doable objectives is tied to it; work that fits any map
  doesn't argue for one map over another.
- A quest can be **finished** on a map if all its in-raid objectives are doable there, none needs items found in
  raid (they depend on luck; a `findItem` whose item may be bought, `foundInRaid` false, doesn't count as one, the
  same cut as "Plan order" below) and no kill count is above 3; otherwise it is **progressed**.
- Score = 3 per finishable quest + 1 per tied objective with a place + 0.6 per tied objective without one + 0.1 per
  untied doable objective. Maps are ranked by score, and every map with work on it is shown, the best (or the one
  on screen) expanded. Maps with picked quests come before them, whatever their score (§4, "Picks"). Until
  2026-10-09 only the top four were shown, plus every map with a pick (decided 2026-10-04, from the review); the
  owner then had every map with quests listed, as the planner's cut left maps out without a word on screen. MAP's
  list holds the same maps with the same counts.
- Quests whose in-raid work fits any map are listed once under **Any map**. The operational tasks for any map are
  the exception (owner, from a panel of four, 2026-10-19: B, as drawn): they stand in every map's open card and the
  raid card, after the map's own tasks and a hairline (§4, *Operational tasks*). Operational tasks add nothing to a
  map's score or counts; a map with only operational work joins NEXT RAID's list at its end.
- No walking time (owner, 2026-10-03: "~17 min walking" didn't say which quests it was for, players rarely just
  walk, and it was an estimate shown as a figure). A map card's line under the name holds facts only: the raid's
  length and its bosses with their chances ("40 min raid · Kaban 75% · Kollontay 75%"). The planner still measures
  a nearest-neighbour route through one place per located objective, only to break ties between maps of equal
  score; it is never shown.
- A **progressed** quest's row says why it can't be finished there, from the same tests (`Planning.ProgressNote`):
  "2 of 5 objectives here" (its other in-raid objectives are on other maps; hand-overs and optional ones don't
  count), "needs items found in raid", "25 kills in all" (a kill count above 3; the total the data gives, since the
  logs don't report kill progress). Joined by " · " when several apply.
- A key an objective names is shown on that objective's line. A key only the quest lists (§6) is shown on its
  objectives that have a place on that map, not on hand-ins or extracts.

### Plan order

**Owner, 2026-10-03:** the order of the quests in Plan wasn't clear (it was the order the quests were stored in).
Each section, COMPLETE and PROGRESS, is now ordered by what the quest's work on that map involves, from little to a
lot, then by complexity, then by the quest giver, then by name (`QuestEffort`, applied in `RaidPlanner.Plan`).

- **Effort groups**, named by the activity, not "easy" or "hard": the data supports facts (what an objective asks),
  not judgements (how hard a player finds it). A quest's group is that of its most demanding objective counted for
  the map (in-raid, not optional, as the planner counts them):
  - **Go there**: `visit`, `mark`, `plantItem`, `plantQuestItem`, `useItem`, `findQuestItem`, a `findItem` that may
    be bought, an `extract` that doesn't ask for the status "Survived" (whether it names an exit or not: the status
    is what `QuestEffort` looks at).
  - **Find or survive**: `findItem` found in raid, `extract` with the status "Survived", `experience` (staying under
    a health effect), and kills of Scavs or sniper Scavs without conditions (targets `Savage`, `Marksman`,
    `assaultGroup`, `Any`; `assaultGroup` is a Scav type: tarkov.dev's English text for it is "Scav").
  - **Fight**: kills of PMCs (`AnyPmc`, `Bear`), Rogues (`ExUsec`), Raiders (`PmcBot`), Black Division
    (`blackDivision`, `pmcBotBlackDiv`), the Labyrinth guards (`tagillaHelperAgro`), cultists (`sectant…`), bosses
    and their guards (`boss…`, `follower…`, `infected…`), and any kill with a condition set: a weapon or weapon mods,
    body parts, a distance (tarkov.dev writes 0 or null for none), gear worn or not worn, a time of day, a health
    effect on the player or the enemy, or a zone (an area of the map). A target the rules don't know counts as a
    fight, the safe side. In PvE, "kill PMCs" means AI PMCs, easier than players in PvP; they stay in Fight, the
    riskiest kind of objective either way.
- **Complexity**, within a group: a tuple compared in order, no weights: how many of the quest's objectives count
  on the map, how many kill conditions they set, and the largest kill count in buckets (none, up to 5, up to 15,
  more).
- Then the **trader**, in the order tarkov.dev lists traders (the game's own: Prapor, Therapist, Fence, …), then the
  **name**.
- Only tarkov.dev's structured fields count: the objective `type`, `targetNames`, `count`, the kill-condition
  fields, `exitStatus`, `foundInRaid`; never the description, and no AI. Kill targets and exit statuses are
  translated by tarkov.dev, so the loader keeps their keys from before translation (`GameData.ObjectiveFacts`): the
  order is the same in every language. New quests sort by themselves.
- **Display:** a thin hairline above the first row of each later group, no headings; the quest-type glyph shows
  why a row is where it is. One line in help: "Quests are ordered from going somewhere, to finding or surviving,
  to fighting; simpler first within each."
- **After a tarkov.dev or game update** run `shturmap-cli effort pve` and `… regular`: every Plan row (all quests
  active) in the plan's order with its group and complexity, kill targets the rules don't know (counted as fights;
  name them in `QuestEffort`) and objective types they don't know. On 2026-10-03: 614 PvE rows (Go there 214, Find
  or survive 134, Fight 266) and 620 PvP rows, no unknown targets or types.

## 8. Engineering

| project | role |
| --- | --- |
| `Shturmap.Core` | no Windows dependencies: screenshots, projection, floors, bearings, logs, raid tracker, quest progress, taxonomy, planner, quest synopsis. Rules without side effects, with one exception: `UnpackedCopies` deletes the 0.1.0 builds' leftover folders under %TEMP% ("Distribution") |
| `Shturmap.Game` | install discovery (BSG launcher and Steam are equal), log tailer, screenshot watcher and the screenshots' readers, game settings |
| `Shturmap.Data` | json.tarkov.dev loader (ETag cache, translations), SQLite progress store, game art cache (portraits, icons) |
| `Shturmap.Map` | SkiaSharp drawing: artwork per floor, camera, renderer, map content, glyphs |
| `Shturmap.Session` | the coordinator: inputs in, one immutable `SessionSnapshot` out |
| `Shturmap.App` | WinUI 3 window; reads snapshots, never game files |
| `tools/Shturmap.Cli` | headless runner: locate, replay, data, render, watch, simulate, quests; the readers' measurements: stash, tasks, desk; the audits after an update: synopses, effort, bring, handovers, spawns (docs/UPDATES.md) |

Rules:

- Core logic is pure and unit-tested, on log lines written by hand; a few tests also replay real sessions, which stay
  on the developer's PC and out of the repository (§3, "Test fixtures").
- Every change keeps `.\eng\dotnet.ps1 test --solution Shturmap.slnx` green, including `SafetyTests`.
- Verify UI with `Shturmap.exe --snapshot <folder>` (renders the window and the map to PNGs, and writes what Copy
  diagnostics would copy to `diagnostics.txt`) or `shturmap-cli render` (an animation: `shturmap-cli frames`, §8
  "Developer aids"); never capture the user's screens. Such a run opens its window unseen, off every monitor and
  without taking the focus, so it can run while the owner plays ("Developer aids", *Quiet runs*). A change to what
  the player sees, and every release, is then reviewed by a UI QA pass that runs these checks and judges their pictures
  against this document (owner, 2026-10-15; docs/QA.md).
  `--verbose` adds the app log's DEBUG lines; `--study` (developer builds) keeps a study log for one session; `--show-report` opens
  the Report dialog with an example and what's sent, `--show-crash` the question after a crash (for snapshots;
  snapshots send nothing); `--send-report <text> <folder>` is a release's delivery check (§8 "Reports"; with
  `--fake-game <folder>` it leaves the player's data and study log alone; its lines still go to the app log): a
  developer build sends one real report through the dialog's Send and saves the window, a release only opens the
  dialog with the text, and Send is pressed by hand (nothing is sent without a click, and a command line isn't one). For website media, `tools\fake-raid.ps1 -Window
  1600x900 -Scale 2` renders at a fixed size (the app's `--window`; the UI reads larger), in English
  (`--culture <culture>`: that run's language for everything, as if chosen in settings, without saving it; `de-DE` also
  for a language still being translated, `qps-ploc` the pseudo-language; `--switch-language <culture>` switches it while running, §8 "Developer aids") and at twice the pixel density (`--snapshot-scale`, sharp on high-DPI screens). `-GroupPick`
  plays a group's map pick in the menus (with `-PlanOnly`); `-HoldLoading` stops the raid halfway through loading;
  `-Operational <n>` (the app's `--operational <n>`, with a fake game only) gives the run the `tasks` step's made-up
  operational tasks once the data and the mode are known, and `-OperationalEnded` (`--operational-ended`) the one that
  runs out at once, so a snapshot pictures the list rows' framed glyphs, the open card's and the raid card's
  OPERATIONAL, and the cue of the tasks read over the map, which a snapshot keeps up as it keeps every cue, its row of
  the task that ran out ENDED (owner, 2026-10-19, B, and after trying the dev build; the file isn't written;
  `-Operational 8`: the cue's "+3"); `-Desk` (the app's `--desk [<n>]`, with a fake
  game only; with `-PlanOnly`) puts up the made-up stash desk as a reading in the menus brings it once the data is there
  (`Rules.DeskExample`: real items of the catalogue, so their art and cards are there, with made-up prices, counts,
  conditions and needs, built through the model as a reading's page is; a LOADOUT strip with "?" slots, a scaled
  condition and weapons, TACTICAL RIG, POCKETS beside SPECIAL SLOTS, a BACKPACK with the Zhuk's window attached, the
  POUCH, a STASH page with every mark of a cell, and a wallets' window attached to none; for the card and the click,
  step C, 2026-10-19: a cell in each of the copy's states, the LEDX that copies, a key past the best net, the blue
  keycard Therapist pays more for, ammunition that can't be listed, the Grizzly at 1469/1800, the keycards' "?", real
  quests and hideout stations for the keep lines, and a made-up price history for every item, so the card's band shows
  without a request, `DeskExample.History`), `-DeskReading 2` the reading that changed the backpack, so the rest is
  folded, `3` one that changed nothing (step B, 2026-10-19); the layout check's `desk` view is reading 1; `-Glance <n>`
  (the app's `--glance <n>`, with a fake game; not with `-PlanOnly`) puts up the made-up in-raid glance's reading n once
  the raid is on, as a raid screenshot of an inventory screen brings it (`Rules.GlanceExample`: 1, a drawer with three
  items; 2, a body with its slots, an UNSEARCHED rig, pockets being searched and a searched backpack), the snapshot
  keeping its plate up, the raid a Scav's so no roll call ends it, its cues going in their time (2026-10-19; §4, *The
  in-raid glance*; the layout check's `glance` view);
  `-MoreArgs` passes more of the app's flags on. In developer builds every picture a snapshot saves of the window or a
  popup also gets its texts, its layout problems (a text missing that the picture always shows, one that overflows or
  is trimmed, text over text, and in the pseudo-language one untranslated; a layer that lies over the window on
  purpose, a cue's halo or a glowing line's gold copy over its words, `Glow.CopyName`, isn't text over text) and a copy
  with them boxed beside it (`<picture>.texts.txt`, `.layout.json`, `.problems.png`: the layout check,
  `Dev/LayoutCheck.cs`), `--whats-new`
  shows the newest What's New card for one, and `--show-update` the update band, the next patch of the running version
  taken for downloaded (`Updater.DevReady`; a snapshot never shows the UPDATE READY cue, a script's `trigger update`
  plays it); `tools\layout-check.ps1` plays every view in each language and window size and sums it up
  (docs/LANGUAGES.md, "Layout check").
- The website's hero clip comes from a demo mode, `--demo <quest>` (fake games only; `src/Shturmap.App/Demo.cs`).
  It must never read as live tracking (owner, 2026-10-02): the position changes exactly once, and visibly after a
  drawn press of the screenshot key. The key comes in a pause: the window dims, the key stands large in the middle
  with "SCREENSHOT · POSITION FROM THE FILE NAME", it is pressed, the file name appears, and the new position is
  held back from the view (demo only) until the dim clears, so the marker moves after the press is seen. Then a
  drawn pointer keeps the quest highlighted, the map zooms to it, its objective's card stays up to be read, and the
  view goes back. It calls the app's own code; no input reaches the system. `tools\fake-raid.ps1 -Demo` writes the
  one screenshot after the press and records the clip with `tools\record-window`, which records Shturmap's own
  window only (Windows.Graphics.Capture, no cursor; outside `src/`, so `SafetyTests` still keeps capture APIs out
  of the app). Never record a monitor, the desktop or another window (owner, 2026-10-02: window capture of Shturmap
  only).
- Website media is regenerated by `tools/make-media.ps1` in the shturmap.github.io repository; see its CLAUDE.md
  and README.
- Comments explain why, not what. Match the surrounding style.
- Write user-facing text plainly: short sentences, units on numbers, no jargon.

### Distribution

Releases live in the code repository's **GitHub Releases**, and **Velopack** installs Shturmap and keeps it up to
date (owner, 2026-10-03: "the releases live with the code in the release section of the project", with an
auto-update mode; Velopack chosen over MSIX, which Windows installs only with a trusted signature, the Microsoft
Store, and a hand-made updater). 0.2.0 is the first public release. The 0.1.0 builds given to friends were one
self-unpacking exe, without updates; they need the Setup once.

- **What players download.** `Shturmap-Setup.exe` (vpk's `ShturmapApp-win-Setup.exe` under the name the README
  gives; both are on the release, with Velopack's portable zip). It installs for the Windows user, without admin
  rights, into `%LOCALAPPDATA%\ShturmapApp`, with a Start-menu and a desktop shortcut and an entry under Windows
  Settings → Apps. Measured 2026-10-03: the Setup is 106 MB, the installed app 244 MB (precompiled, like the folder
  build), a delta between two builds 1.7 MB.
- **The install folder is not the data folder.** Velopack installs to `%LOCALAPPDATA%\<pack id>`, and an uninstall
  deletes that folder. So the pack id is `ShturmapApp` (`Distribution.PackId`; `eng\release.ps1` packs with the
  same, a test checks), never `Shturmap`: the database, logs, cache, outbox and crash records in
  `%LOCALAPPDATA%\Shturmap` outlive the app. Checked 2026-10-03: installing, updating and uninstalling left the data
  folder's files all there and `shturmap.db` byte for byte the same; the uninstall removed the install folder, its
  shortcuts and its Apps entry.
- **Uninstalling from settings** (owner, 2026-10-03: "The installer should also be able to uninstall", then: an
  "Uninstall Shturmap…" in help, with the data only on a tick; in settings since help and settings were split). Windows' Settings → Apps (and the Start menu's
  Uninstall) already ran Velopack's uninstaller; the Setup itself has no repair-or-remove mode, and another installer
  around Velopack wasn't worth it. Settings' quiet link "UNINSTALL SHTURMAP…" shows only in an install Velopack made,
  the release (`ShturmapApp`) or the dev build (`ShturmapDev`) (`Uninstall.Offered`), never in a folder build. It
  asks one question, "Remove Shturmap from this PC?", with an unticked "Also delete my Shturmap data" and what that is
  (settings, quest history, logs, reports waiting, crash records, and for the release the download cache; the dev
  build's data is `%LOCALAPPDATA%\Shturmap-dev`, with its study log, and the cache it shares stays with the
  release's folder). UNINSTALL closes the session as RESTART NOW does (so a reinstall doesn't take the exit for a crash), leaves
  a note in the install folder when the box is ticked (`Uninstall.IntentFile`, removed when it isn't), starts
  Velopack's uninstaller (`Update.exe uninstall`, which shows a dialog only when something goes wrong) and ends.
  Velopack stops what still runs from the install, then starts the exe with its uninstall hook: `Program.Main`'s
  `OnBeforeUninstallFastCallback` deletes the data folder only on a note at most 5 minutes old, so an uninstall from
  Windows' Settings never deletes data, and retries for up to 20 s while the closing app still holds a file. The
  deletion is checked strictly (`Uninstall.MayDelete`): exactly this install's data folder (the release's
  `%LOCALAPPDATA%\Shturmap`, the dev build's `%LOCALAPPDATA%\Shturmap-dev`), directly in `%LOCALAPPDATA%`, not a
  link; never a `--data` folder, a parent, a child or another build's folder. Velopack's own hook rather than a
  script left behind: the deletion runs in Shturmap's code, after the app's files are let go, with the same check the
  tests cover. Developer check: `--uninstall-test keep|delete` (developer builds) runs it at once, before any
  session, with Velopack's dialogs off.
- **Updates.** `Program.Main` runs Velopack first: its Setup, updater and uninstall start the exe with their own
  arguments, and a version downloaded in an earlier session is applied there, before the app starts (the app then
  starts again with the same arguments). Then WinUI's own start (`DISABLE_XAML_GENERATED_MAIN`). An installed app
  asks GitHub at start and then every 6 hours, never more often (`UpdatePolicy`), anonymously: GitHub allows 60
  such requests an hour per address. A new version downloads in the background (a delta when there is one) and
  applies at the next start, or on RESTART NOW, between raids only: Shturmap never restarts by itself, and never
  during a raid (owner, 2026-10-03; it stands).
  **A cue, then the band** (owner, 2026-10-15: "A Beta-tester did not realize there is an update, which shows me that it
  is currently too subtle. I, however, do not want to have a blocking modal like a pop-up"; from a panel of four,
  2026-10-15: B). Until then one quiet line at the top of the Plan rail said it ("Update 0.2.1 ready: applies at next
  start", 11 px amber over an 11 px RESTART NOW link), scrolling with the rail; that line is gone. Now:
  - **The band**, pinned under the MAP picker, above what scrolls, so it never scrolls away (the row that holds the
    raid card's head in a raid; "Screen anatomy", *Rail*): a plate in the panel's colour with an amber hairline; the
    update glyph (Segoe Fluent `E777`); the eyebrow in amber, by the stage (`UpdatePolicy.Decide`, `UpdateBand`):
    DOWNLOAD AVAILABLE (a version found in "Tell me only"), DOWNLOADING, UPDATE READY, UPDATE DIDN'T APPLY; the version
    with its release's name in the rail's title type, "0.6.1 · OKAMI", the name where the app's What's New knows it (a
    patch keeps its line's name, `Rules.WhatsNew.NameOf`; a new line's name comes with its release, so "0.7.0" alone);
    one note line, "Restarts Shturmap in a few seconds. Never in a raid." (where it didn't apply "Close what Shturmap
    opened, then restart.", the line's words until then; while it downloads "Applies at the next start.", over
    DOWNLOAD "Downloads in the background. Applies at the next start."; the panel drew the ready band only, so these
    two are the build's, 2026-10-15, provisional); and RESTART NOW (or DOWNLOAD) as a real button in the app's button
    style, the Report dialog's SEND (the panel's fill, an amber hairline, amber words; the app has no filled button). It glows in as the stash marks do (`Controls.Glow`: the
    title's gold copy eases to ink over 1.2 s, the plate flashes once at 22 % for 0.9 s) when it shows, again at each
    return to the menus after a raid, and as the cue fades. In a raid and while one loads the row holds the raid
    card's head, so the band hides. It has no ×: it goes with the restart. With animation effects off it doesn't glow.
  - **The cue**, UPDATE READY (`CueKind.UpdateReady`, principle 11): the app's cue across the map's middle, its dark
    band, gold flash and rules, the title decoding, 5 s, no clicks. Eyebrow UPDATE READY, title "0.6.1 · OKAMI", under
    it the amber line "RESTART NOW · TOP LEFT · NEVER IN A RAID" (in "Tell me only", for a version found: DOWNLOAD
    AVAILABLE and "DOWNLOAD · TOP LEFT"). The panel drew "RESTART NOW IN THE RAIL"; players never see the word "rail",
    so the line names the place as the band stands, pinned at the window's top left (2026-10-15, before the merge).
    **When** (`UpdateCue`): as a download completes outside a raid (one that
    completes in a raid or while one loads waits for the menus), and once more at the first menu after each raid
    while the update waits, once per raid (a raid over live: RAID OVER; a load cancelled is no raid). It waits its
    turn behind RAID OVER and its replay, QUEST COMPLETE and SEEN IN YOUR STASH, as those wait among themselves, and
    behind the tour and a preview, then comes once the map's middle has been free for 0.5 s. Never during a raid or a
    load, never with "Off" (a version downloaded before still has its band), never for the log read back at start nor
    for a version still waiting at start (its notice says it), never after RESTART NOW, and never in a snapshot by
    itself. The band's glow plays as it fades (4.6 s). With animation effects off it stands still 5 s.
  - Not taken from the panel: A, "The update band", the band alone (only in the rail, so a player looking at the map
    between raids could still miss it); C, "Applies by itself when the game is quiet", a restart by itself with
    Automatic, between raids, after 10 minutes without a log line (it reversed the decision of 2026-10-03, and the log
    can't tell the menus from a closed game, so the installer could still meet a running game); D, "Lights and title",
    a fourth light before LOGS · SCREENSHOTS · DATA and "Shturmap · update ready" in the window's title (the tester
    already overlooked the squares, an update is no input, and it didn't say what to do). A Windows toast was declined
    before the panel: it shows on the game's monitor, over the game (§2; principle 3).
  RESTART NOW starts Velopack's updater first, which waits for the app to end (a minute at most), applies the version
  and starts it; only then is the session closed and the app ended. Where the downloaded version is gone or the
  updater can't start, the session goes on and a notice says why ("The update couldn't be applied: its download is
  gone. Shturmap keeps running."), and a version whose download is gone counts as not downloaded (review of
  2026-10-09: the session was closed first, and Velopack's own restart returned without a word, leaving a window
  that drew nothing).
  "Updates" in settings: **Automatic** (the default), **Tell me only** (it asks; the band offers DOWNLOAD), **Off** (no
  request at all). A version already downloaded applies at the next start whatever the setting.
  **When applying fails** (owner, 2026-10-05: "it tells me always that the update is available even though i
  downloaded and restarted"). Velopack applies a version by moving the install's `current` folder aside, and that
  fails while another program holds one of its files: it tried for 10 s, gave up and started the old version again,
  without a word (its log, `%LOCALAPPDATA%\velopack\velopack_<id>.log`: "being used by another process").
  **The cause that day: a browser Shturmap had started.** Velopack starts the app in the install's `current` folder,
  and what the app opens inherits its working folder: Firefox, started by a wiki link while it wasn't running, worked
  in `ShturmapDev\current` (with its crash helper) and held the folder for as long as it ran, so every update failed,
  with or without the game. No file in the folder was open, which is why Restart Manager named none; the processes'
  working folders did (read from their PEB). The game and a second Shturmap window were suspected first and ruled out
  by trying. So the app leaves that folder before its window opens: `Program` sets its working folder to the user's
  own, which nothing needs to move (`WorkingFolderTests`; every file beside the exe is found from
  `AppContext.BaseDirectory`). A browser an earlier version started keeps the old folder until it is closed.
  Velopack applies a downloaded version before every start (Program), so one still waiting when the app starts is
  one that couldn't be applied (`Updater.NotApplied`): the app log says so (WARN), a notice says it once, "Update
  0.3.1 couldn't be applied: another program kept Shturmap's folder in use, often a browser Shturmap opened. Close it,
  then restart Shturmap.", and the band says UPDATE DIDN'T APPLY over the version and "Close what Shturmap opened, then
  restart.", with RESTART NOW (until 2026-10-15 the line, "Update 0.3.1 didn't apply: close what Shturmap opened, then
  restart"); the UPDATE READY cue waits for the first raid over. Builds the Setup
  didn't install (the folder build, `dotnet run`) and developer runs (snapshots, fake games, the demo) ask nothing;
  help says "Updates: not available in this build". Checks, finds and downloads are logged at INFO; a failure is a
  WARN line and a retry at the next check, never a notice.
- **Pre-releases while in private testing.** Releases are GitHub pre-releases named "Shturmap <version> (private
  testing)" (owner, 2026-10-03: "we should state that the app is still in private testing"), so the update check
  includes pre-releases (`Distribution.PreReleases`); with the first stable release it becomes false. The README
  opens with the same note.
- **The network.** Asking for a new version is the one request besides tarkov.dev's data and art and the reports
  a player sends: `api.github.com` for the list, the packages from GitHub's release-asset hosts. Never Velopack's
  own update service (`SafetyTests`). Help, the README and PRIVACY.md say that GitHub sees the address, as with any
  download, and nothing else is sent.
- **Releasing.** `eng\release.ps1` builds into `artifacts\release` (owner, 2026-10-03: "The artifacts should contain
  a dev and release artifact"; the dev build is `artifacts\dev`, below): the folder `app\` (precompiled, with the
  Sentry DSN), and after asking GitHub for the last release so `vpk pack` can build a delta, `packages\` (the Setup,
  full and delta packages, `releases.win.json` and `RELEASES`, the portable zip), plus `Shturmap-Setup.exe` and its
  `.sha256`. The notes are `docs\release-notes\<version>.md`; what the What's New card says of the version is its
  section in `docs\whats-new.md` (up to five lines, built into the app; §4, "Screen anatomy"), written with the notes.
  `eng\publish-release.ps1` uploads it (`vpk upload github`, tag
  `v<version>`, a pre-release) and adds `Shturmap-Setup.exe` and its `.sha256`, all to a draft that it publishes once
  every file is on it (`-Draft` leaves the draft); it refuses unless the tree is clean, the commit pushed and the build
  made from that commit (`artifacts\release\app`). Both run in the Release workflow (next point) for every release
  players get; on the PC, `eng\release.ps1` builds for tries and update tests. `vpk` is a
  pinned local tool (`.config\dotnet-tools.json`). `eng\publish.ps1` builds only the folder (`artifacts\Shturmap`,
  what `tools\fake-raid.ps1` runs; it keeps the developer data folder). To test the whole update path without GitHub, `--update-feed <folder>` points
  an installed build at a local feed and lets it update even in a snapshot or a fake game. The folder must be on a
  fixed local drive (`LocalFeed`): no network share in either slash form, no device path, no mapped or removable
  drive, since an update is code that runs as the player (2026-10-04: `//server/share` passed as local).
  Velopack's Setup 1.2.161 crashes when given arguments for the app (`-- …`); install silently with `--silent` only.
- **Built and published on GitHub, with provenance** (owner, 2026-10-09: adopt every trust step that can be
  automated, so that players can check the project although they can't know its author). The Release workflow
  (`.github/workflows/release.yml`) makes every release players get. The owner's go is running it, on main only:
  `gh workflow run release.yml` (`-f draft=true` leaves a draft), or the Actions tab. On GitHub's Windows runner it
  checks the app's packages for known vulnerabilities (`eng\audit-packages.ps1`), runs `eng\release.ps1` with the
  DSN from the `SENTRY_DSN` Actions secret (it fails without one and never prints it), installs the Setup as a player
  would and takes it apart again (`eng\install-test.ps1`, next point), attests build provenance
  (`actions/attest-build-provenance`: SLSA provenance signed through Sigstore) for `Shturmap-Setup.exe` and what vpk
  uploads for the version (the Setup, full and delta packages, the portable zip), and only then runs
  `eng\publish-release.ps1`, so a release can be checked from its first minute. No package cache: it restores from
  nuget.org. GitHub attests only in public repositories on this plan: while the repository is private the step is
  skipped with a warning, so the first release that can be checked is the first built after it went public. A
  release published from the PC would have no provenance, which the README promises from 0.4.0: it isn't done.
  **Checking a download:** `gh attestation verify Shturmap-Setup.exe -R shturmap/shturmap` (the GitHub CLI, signed
  in), and `Get-FileHash` against `Shturmap-Setup.exe.sha256`; the README says how under "Check it yourself".
- **The install test** (owner, 2026-10-09, after the first release: nothing had installed a Setup before players).
  `eng\install-test.ps1` runs in the Release workflow after the build and before the attestation:
  - **Install:** the Setup with `--silent` on the runner; the installed app's version must be the commit's, with
    Windows' Apps entry and both shortcuts. If the Setup started the app, it is closed.
  - **Start:** the installed app once with `--snapshot`, on the release's data folder, with no update check and no
    report. It must save its window and exit, with no ERROR line in its log and no crash record.
  - **Uninstall:** Velopack's, as settings starts it (`Update.exe --silent uninstall`). The app, its shortcuts and
    its Apps entry must be gone, and the data folder must still be there.

  Any failure stops the run before anything is attested or published. `-f test_only=true` runs the build and this
  test alone (no delta; nothing attested or published), to try a change without a release. The script refuses on a
  PC where Shturmap is installed or has data. It doesn't cover the update from the last release: that is tried on
  the PC with `--update-feed` (above), as 0.3.0 → 0.4.0 was before 0.4.0 was published.
- **Its name doesn't matter.** WinUI looks for the app's resources (its compiled XAML) in `resources.pri` or
  `<exe name>.pri`, so the project names its PRI file `resources.pri`: named after the project, any other exe
  name made the window fail to load (2026-10-03, with the 0.1.0 single exe).
- **The 0.1.0 leftovers.** The single exe unpacked about 200 MB per version to `%TEMP%\.net\Shturmap…\<id>`. The
  installed app removes those once, in the background (`UnpackedCopies`, logged): only folders of that shape
  holding `Shturmap.dll`, each moved aside before it is deleted, which Windows refuses while an old exe still runs
  from it; a later start tries again only if one was in use.
- **Unsigned.** Windows SmartScreen asks once about the Setup ("More info → Run anyway"); the README and the
  release notes say so. Updates don't ask. Free signing from the SignPath Foundation is to be asked for once the
  repository is public (docs/NEXT.md, "2026-10-09: trust"); until it is granted, no text says it.

### Data folders

Only the installed release keeps the player's data folder, `%LOCALAPPDATA%\Shturmap`; every other build keeps its
own, `%LOCALAPPDATA%\Shturmap-dev` (owner, 2026-10-03: the release installed beside the dev build must not share a
database, settings, app log, study log or reports with it; developer starts were mixing into the owner's study log).
"Other builds" are the dev build, the folder build (`eng\publish.ps1`, what `tools\fake-raid.ps1` runs), `dotnet
run`, the CLI and tests. `--data <folder>` picks any folder (the app and the CLI; the CLI reaches the release's data
with `--data "%LOCALAPPDATA%\Shturmap"`). The app chooses at start, before anything is written: Velopack's id of the
install (`ShturmapApp` → the release's folder, anything else → the developer folder; `Distribution.DataFolderFor`,
`AppPaths.Use`). Diagnostics and the app log's first line name the folder ("Data folder: dev"). If Velopack itself
can't start, where the exe runs from says which install it is (`%LOCALAPPDATA%\ShturmapApp\current`;
`Distribution.InstalledIdByFolder`), so an installed release keeps the player's folder for that run, without
updates, and the app log says so (review of 2026-10-04: it used to open the developer folder, and the player's
history seemed gone).

**A settings file that can't be read** (review of 2026-10-09). When SQLite finds `shturmap.db` damaged, or no
database at all, the session moves it aside with the time in its name (`shturmap.db.unreadable-20261009-1530`; its
`-wal` and `-shm` go with it), opens a fresh one, writes a WARN line and says for 30 s "Shturmap's settings file
couldn't be read and was set aside: picks, ticks and settings start over." The quest history comes back from the
game's logs, as at every start. Such a file used to stop the session from starting: the window stayed up without
quests, positions or a word. A file that is only out of reach for now (another program holds it, a lock that doesn't
pass, a folder that can't be written) is never moved; that session keeps its settings in memory and says "Shturmap's
settings file couldn't be opened: picks, ticks and settings aren't kept this time." A second Shturmap opens the same
file as the first (SQLite's WAL), and Windows moves no file another Shturmap holds open (`SettingsFile`).

**The stash file** (`stash.json` in the data folder; §4, "What you have"; `StashLedger`): what Shturmap learned of
the player's possessions, item ids and counts only, no names, no pictures, no paths. Its shape, so that a later
program of the player's could read it (the panel of 2026-10-11 defined it for that; nothing beside Shturmap reads it
today): `version` 1; `passes`, each with `at`, `screenshots` and `full`; `items` by id, each with `count`, `fir` (how
many carried the found-in-raid mark, when any), `seenAt`, `source` (`log`, `screenshot`, `you`), for a log mark
`quest` and `grant` (`start`, `reward`, `insurance`), and `notSeenAt` after a full pass without it; `retracted` by id,
the time of NOT ANY MORE; `handovers` by quest id, the time of the completion that took its count from the marks
(so that the log read back takes it once); `forgottenAt`, the time of FORGET ALL MARKS, before which no observation
stands. Written as a temporary file and one move, after each change; one that can't be read is set
aside as `stash.json.unreadable` and the marks start over (the next screenshot or log message brings them back). It
is never in the shared cache, a report or the diagnostics. Deleted with the data folder.

**The operational tasks file** (`operational.json` in the data folder; owner, from a panel of four, 2026-10-19: B; §2,
"Menu screenshots"; `OperationalLedger`): the daily and weekly tasks the last screenshot of the Tasks screen's
OPERATIONAL table showed, one set per game mode (PvE and PvP apart). Its shape: `version` 1; `modes` by the mode's name
(`pve`, `pvp`), each with `readAt` (the table screenshot's time), `tasks` in the table's order and `ended` (when tasks
that ran out since did, for "1 ended at 22:35"; kept a day at most). A task: `name` and its objectives' words as the
game writes them (`objectives`, each `text` with `done` and `of` where the game shows a count), `map` (tarkov.dev's map
id; none for any location), `kind` (`kill`, `survive`, `handOver`, `unknown`), `progress` (0 to 100, when known),
`expiresAt` (none when the countdown failed its checks), `readAt` and `objectivesAt` (a folded row keeps objectives
read before). Dates to the second with the PC's offset. No reward, no pixel, no path, no other word. Written as a
temporary file and one move, after each change; a task past its expiry is dropped at load and as it runs out. One that
can't be read is set aside as `operational.json.unreadable` and the tasks start over (the next screenshot brings them
back). Never in a report or the diagnostics. A snapshot's made-up tasks (`--operational`) are never written. Deleted
with the data folder.

**The desk file** (`desk.json` in the data folder; owner, 2026-10-19; §2, "For the stash desk"; `DeskLedger`): the
stash desk's page in hand, one per game mode (PvE and PvP apart): the regions the last screenshots showed, each as its
last reading had it. Its shape: `version` 1; `modes` by the mode's name (`pve`, `pvp`), each with `readAt` (the last
reading's screenshot time) and `regions` in the order taken; a region: `key` (`loadout`, `rig`, `pockets`, `special`,
`backpack`, `pouch`, `stash`, `window:<its folded title>`), `kind`, `cols` and `rows`, its compartments (`parts`, each
`[i, j, w, h]`), a window's `title` (the container's name as printed), `titleIds` (the items its title may name),
`attachedTo` (`region`, `i`, `j`, or a `slot`) or, attached to none, `sameName` and `sameNameIn`; `readAt` (the time of
the screenshot that last showed the region) and `missed` (how many readings in a row since didn't show it, when one or
more; both owner, 2026-10-25); and `cells`, each with `i`, `j`, `w`, `h`, `ids` (one item, several for "one of N", none
for unread), a slot's `slot`, `count` when more than one, `print` ("46/50"), `fir`, `holds` (`contents`, `mods`) and a
"?" cell's `pick` (a candidate clicked on its card, step C, 2026-10-19; carried over every reading in which the item
picked is still among the cell's candidates, gone with one in which it isn't; until 2026-10-25 only over one that showed
the cell as it was). Item ids and the containers' names only: no pixel, no path, no other word. Read back, a window with
no cell and no title, and one attached to nothing that `missed` three readings, are left out (§4, *What it mirrors*); a
file written before 2026-10-25 has neither `readAt` nor `missed` on its regions: they take the page's `readAt`, and its
windows attached to nothing, unseen for how long it can't say, are left out (the owner's file of 2026-10-19 lost
"*ACE COVER", "ÄRMBANI" and two windows of no name so, and kept its Keys and Junk windows, attached to their cases).
Written as a temporary file and one move, after each change; one that can't be read is set aside as
`desk.json.unreadable` and the pages start over (the next screenshot brings them back). Never in a report or the
diagnostics. Deleted with the data folder.

**The notes file** (`notes.json` in the data folder; owner, 2026-10-15; `QuestNotes`): the notes the player writes on
a quest's card, one set per quest and game mode (PvE and PvP apart, as ticks and picks are), each with when it was
written. Add and delete only, no editing: the time always says when the words were written. A note is trimmed, keeps
its line breaks and is cut at 500 characters; an empty one isn't added. Newest first wherever they are listed.
Its shape: `version` 1; `notes`, in the order written, each with `id` (eight hex digits, by which it is deleted),
`mode` (`pve`, `pvp`), `quest` (tarkov.dev's task id), `at` (to the second, with the PC's offset:
`2026-01-01T15:40:12+01:00`) and `text`, readable as written. Written as a temporary file and one move, after each
change, before the snapshot that shows it. One that can't be read is set aside with the time in its name, as the
settings file is (`notes.json.unreadable-20260101-1540`, never over an earlier one: unlike the stash's marks, nothing
brings the notes back), and the notes start empty; one that is only out of reach for now is left as it is and not
written over that run; either is a WARN line in the app log. The notes are the player's own words: never in a report,
the diagnostics, the app log or the study log, which records `note.add` (quest, length) and `note.delete` (quest)
only. Deleted with the data folder.

The download cache (tarkov.dev's data, map artwork, the pictures drawn from it, portraits, icons and grid images)
stays shared in `%LOCALAPPDATA%\Shturmap\cache`, so nothing downloads twice. Two Shturmaps can write it at once: every download goes
to a temporary file of its own (`CachedHttp.TempFor`, the process id and a GUID) and replaces the cached file in one
move, retried for a moment while another process reads it (`CachedHttp.Replace`); a reader never sees half a file,
and a copy that can't be saved is fetched or drawn again next time. A test runs eight writers at once.

### Developer aids

**The dev build.** Beside the release, a dev build that is always current and never taken for the release (owner,
2026-10-03: "a dev artifact of the app that always carries the recent updates and is in dev mode … The dev version
should carry a different icon so it is visually clear"). `eng\dev.ps1` builds it into `artifacts\dev`; run it after
committing app changes (CLAUDE.md).

- **Dev mode.** Built with `ShturmapDev=true` (Release configuration, not precompiled: builds come often and deltas
  stay small), which defines `DEVTOOLS`, as Debug builds do (`Directory.Build.props`): the window says "Shturmap DEV"
  (`App.Title`), the exe, windows and taskbar show the dev icon, the build kind is "dev build", and the developer
  tools and the study log (§8, "Study log") compile in. Releases never define it.
- **Its own install.** Velopack's id `ShturmapDev` (`Distribution.DeveloperPackId`): `%LOCALAPPDATA%\ShturmapDev`, shortcuts
  "Shturmap DEV" on the desktop and in the Start menu, its own Apps entry. Its data is the developer folder above.
  It never mixes with the release's `ShturmapApp` (a test checks the ids and folders).
- **Always current.** The dev build updates itself from a local feed, `artifacts\dev\feed`, never from GitHub: each
  `eng\dev.ps1` run packs a new version (`<version>-dev.<UTC time>`, so each is newer) with a delta from the last,
  keeps the three newest versions in the feed (and in its lists), and writes the feed's folder to `dev-feed.txt` in
  the dev install's folder. The installed dev app reads that file at start, asks the feed (at start and every 6
  hours, `UpdatePolicy` as for the release), downloads in the background and applies at the next start, never during
  a raid; RESTART NOW applies it at once. No folder of the developer's PC is compiled in, and the dev app follows
  whichever checkout built last. The first run installs it (`--silent`); `artifacts\dev\Shturmap-DEV-Setup.exe`
  installs it on purpose. Checked 2026-10-03: two builds in a row, each found in the feed within a second, a 0.2–0.3
  MB delta downloaded in about 4 s, applied at the next start.
- **What changed.** Each dev build carries `dev\changelog.json` beside the exe: `build` (the full commit), `built` (UTC
  time) and `commits`, those since the last release tag `v*` (or the last 50), newest first, each `hash` (short),
  `date` and `subject` (the commit's title; no authors, no bodies, a user folder masked). The dev view shows it, so
  the owner can check whether a change is live and what changed lately ("No changelog in this build" without it).
- **Reports.** None by default (no DSN). `eng\dev.ps1 -WithReports` builds the DSN in; its reports carry the Sentry
  environment "dev" (`ReportEnvelopes.EnvironmentOf`), so they can be filtered apart.
- **A new game build** (owner, 2026-10-05: the patch notice). The game's log folders carry its build
  ("log_2026.01.01_15-00-00_1.1.5.1.47510"). A developer build keeps the newest it has seen (`gameBuild` in its
  settings), and when the logs come from a newer one, live or read back at start, it says once, for 20 s, "Game build
  1.1.6.0.48001 is new: time for the checks after a patch (docs/UPDATES.md)", with a line in the app log and
  `game.build` in the study log (`GameBuild`, `GameSession.NoticeGameBuilds`). The first build ever seen is only kept;
  an older one (a test server's install) is no news; snapshot and fake-game runs leave it alone. A release has none
  of it: the checks are the developer's.
- **The icon.** `brand\build.cs` makes `Shturmap-dev.ico`: the same mark, only recoloured, on the app's "kept" cyan
  (#3FD2E0) plate with the Ш in the plate's dark. `Logo.Dev` picks the variant; `.\eng\dotnet.ps1 run brand\build.cs
  -- dev-panel <png>` draws all three candidates beside the release icon (a cyan plate, a cyan band across the foot,
  an amber corner tab). No "DEV" lettering: a stencilled V beside the Ш and its chevron could read as the V of the
  war symbols the logo rules avoid (§4, "Logo").

**Continuous integration** (owner, 2026-10-05). Every push builds the solution in Debug (so the developer tools
compile too) and runs the tests, then builds it in Release and runs them again, on GitHub's Windows runner
(`.github/workflows/ci.yml`), with the same `eng\dotnet.ps1` as on the PC and the SDK global.json names. Release since
2026-10-11 (owner): a release compiles the other side of `#if DEVTOOLS`, and its tests check what only a release does;
they had never run, and one had failed unseen. The Release build takes the Debug build's restore (the packages are the
same) and its tests run on what it built. Tests that need the owner's PC skip there: the tarkov.dev cache, the game's
logs, real screenshots, a published build in `artifacts` (`DevToolsGuardTests` read it), the website beside the
repository. The repository is public and so are the run logs: nothing a test prints may be private (CLAUDE.md). A
failed run comes as GitHub's own mail. The daily data check is designed in docs/NEXT.md, not built yet. The layout
check (docs/LANGUAGES.md) doesn't run there: its views need tarkov.dev's data, which a fresh runner would download at
every push (§3, "No unnecessary load on tarkov.dev"), so it runs on the developer's PC before a release.
What else runs there (owner, 2026-10-09: every trust step that can be automated). After the tests, CI fails on a NuGet
package with a known vulnerability, direct or transitive (`eng\audit-packages.ps1`, nuget.org's advisories; also
when it couldn't check). That stands in for Dependabot: its pull requests would put commits by another identity into
the repository, whose only one is shturmap (CLAUDE.md), so a finding is fixed by hand. `RepositoryScanTests` reads
every tracked file for token shapes, a Sentry DSN, private keys, claude.ai links, paths in a user folder, email
addresses, and screenshot names or log sessions dated other than 2026-01-01, the one day examples use (owner,
2026-10-09: a real time of play with a position or a session's quests can be matched to one player), and names
file and line only. Every action in every workflow is pinned to a full commit with its version beside it. OpenSSF
Scorecard (`.github/workflows/scorecard.yml`) rates the repository's practices weekly and on
every push to main, publishes the result for the README's badge and puts its findings in the Security tab; it runs
only while the repository is public, which publishing and code scanning need on this plan. The Release workflow is
under "Distribution".

**The developer view** (owner, 2026-10-03: "a Dev view that allows to switch between modes and can fake-play a raid
that reacts to what I do in the app and 'fake-updates' the position on demand … not exposed in the production
version … to check the UI without having to play the game"). Developer builds only: its code is in `Dev` folders
(`src/Shturmap.App/Dev`, `src/Shturmap.Session/Dev`) wholly inside `#if DEVTOOLS`, and every use of it elsewhere
too. `DEVTOOLS` is defined for Debug builds and `eng\dev.ps1`'s `ShturmapDev=true` builds (Directory.Build.props,
with a Directory.Build.targets fallback for builds started without `-c`, whose Configuration the SDK only sets
after the props are read); a release never has it. `DevToolsGuardTests` check the wrapping, that no project
defines `DEVTOOLS` on its own, and that a Release build on the PC holds no developer-view type or member.
- `Shturmap.exe --dev-view` starts a session on a fake game folder of its own under `%TEMP%`
  (`shturmap-devview-<id>`, read like `--fake-game`, so nothing is sent and no study log is kept; folders older
  than a day go at the next start) and opens the view; F12 opens it in any developer build (a session on the real
  game offers a restart in the view). Chosen over switching a running session to a fake folder: re-pointing the
  session's tailer and watcher at runtime would touch the real start path for a developer aid.
- It writes what the game would: application-log lines (the mode, the scene line, the match setup or a local
  raid's transit line, the loading steps, the raid start and end, a transit, matching cancelled), push
  notifications (quests started, completed, failed; a group's map pick) and screenshot files named as the game
  names them (position, facing quaternion, raid clock). The app reads them through its real parser, tracker,
  watcher and session; tests read them back the same way. The logs say nothing of single objectives, so neither
  can the view.
- Positions on demand: with "Pick on the map" on, a click on the map is a screenshot there (a drag sets the
  facing), its height from the shown floor's band (or the nearest known place on the base map), adjustable; F9
  repeats the last position; "Age" makes the last position older without a new screenshot; "Record a path" and
  "Walk" take screenshots along clicked places.
- Triggers for what no file reaches: the Report dialog, the question after a crash, the update-ready line (which,
  as designed, stays hidden in a raid), a failed data load (offline, 404, 503; data back with "Reload data") and
  the "no game" notice.
- "What's in this build" shows `dev\changelog.json` beside the app (written by `eng\dev.ps1`: the build's commit
  and time, its commits newest first, with a filter: is a change in this build?), or "No changelog in this build"
  with the app's version and commit.
- `--dev-script <file>` plays the view's steps headless, one per line (`mode pve|pvp|seasonal`, `map <normalized
  name> [look]` (with look, the map only shown, as a pick in the MAP list shows it: in a raid, a look), `side pmc|scav`,
  `hosting server|local`, `group` (the group's leader picks the current map), `load`, `steps`,
  `start`, `end`, `cancel` (matching cancelled), `transit <normalized name>`, `quest start|complete|fail <id or name>`,
  `quest here [<n>]` (starts the first n, 8 without one, of the shown map's quests by level), `pick <id or name>` (picks or unpicks a quest, as its pen does), `trader [<name or id>]` (holds that trader's focus, as a click on its portrait in the strip under the MAP list does, and as `--trader-focus <name>` does for a snapshot, `tools\fake-raid.ps1 -TraderFocus`; alone, lets go of it; owner, 2026-10-17), `tick <id or name> <n>` (ticks or unticks its n-th objective as done, for the session only), `show <name> [at row]` (its card held, for a snapshot; with `at row`, held at the quest's own row in the rail as a click on the row holds it, the row scrolled into view first, so a card from a row near the window's foot can be pictured; owner, 2026-10-15, D10), `focus help|feedback|settings|map|rail|<x:Name>` (the keyboard focus on that control, or on the first control inside it that takes it, as Tab moves it there, so a `snapshot` after it shows the focus ring; QA pass, 2026-10-15), `point [<id or name> [<n>] | item <id or name>]` (points at the quest, at its n-th objective or at an item as the pointer on its line would, and holds it for a snapshot; alone, at nothing again), `hover [<id or name> [<n> | cell | key]]` (the pointer on the quest's block in the lists and, inside it, on its n-th objective's line, its first need cell or a gold line that is a key, through the code the pointer's own events call; alone, it leaves the innermost of them, so a script can check what a mouse does with things that lie inside one another), `place <fx> <fy>
  [<fx> <fy>]`, `trail <x> <y> [<x> <y> ...]` (where the pointer has been in the main window, for the cards to tell where it is heading), `cards` (the open cards' titles into the app log, with its time), `pos <x> <y> <z> [yaw]`, `exits <name> [<name> ...] | all` (the game's extract list as a screenshot taken now shows it, taken by the session from where text recognition hands its reading over, so the window calls it out as a real one; names with spaces "quoted", a name ending in `?` marked "??:??:??", `all` every extract of the raid's map for the side; 2026-10-15, for the roll call's replay), `repeat`, `age <min>`, `walk <s>`, `trigger report|crash|update|offline|404|503|nogame|reload`, `choose <folder> | game | auto` ("Choose game folder…" with that folder, with the view's fake game, or FIND AUTOMATICALLY), `whatsnew [<version>] [<n>]` (the newest version's What's New card, as help's link shows it, or with a version, `0.4.0`, that version's card alone, so a script reaches an older version's lines, 0.4.0's `extracts` preview among them; owner, 2026-10-15, D10; with n, the pointer on its n-th line, so its preview starts; alone, off it), `whatsnew [<version>] click <n>` (the pointer onto its n-th line and a tap there, through the code the pointer's own events call, so a line about the tour opens it there), `whatsnew leave` (the pointer off the line it is on, as leaving the card does, or the tour's layer coming over it), `replay` (the made-up raid of What's New's preview replayed on Customs, as at a raid's end), `stash` (the SEEN IN YOUR STASH cue with the example items of What's New's preview, as a reading shows it), `tasks [<n>] [ended]` (the Tasks screen's OPERATIONAL table as a screenshot taken now shows it, from where the reading hands its rows over on: n made-up tasks, 3 without a number and at most 8, a survival on Streets of Tarkov ending in 25 minutes, a kill with its condition on Customs, a weekly's two hand-overs on any map, a kill without a countdown on Shoreline, a line of no known kind on Woods, then a survival on Interchange, a kill on Lighthouse and a weekly's kill on Reserve (seven or eight reach the cue's "+N"; QA pass of 2026-10-19, F3); with `ended` one more that runs out 3 s later; the operational tasks' file isn't written that run; owner, 2026-10-19, B; it calls the cue of the tasks read as a real reading does, and a second `tasks` in the run counts its countdowns down from the first, so the same table again is the same tasks and calls none), `desk [<n>] | open <region> | point|pin|copy <region> [<i> <j> | <slot>] | unpin [<region> <i> <j> | all] | pick <region> <i> <j> | <slot> <n> | rule match|under:<n>|over:<n> | plan | show [<n>]` (step C, 2026-10-19: `pin` a cell right-clicked, its card pinned in the rail or unpinned; `copy` a cell clicked, its flash, decode and notice, the clipboard left alone in a script, the app log saying what would have been copied; `unpin` a pinned card's pin clicked, `all` or nothing named every pin; `pick` a "?" cell's n-th candidate, from 1, clicked on its card; `rule` the copy's rule as the card's − and + set it, saved in the session's settings; a slot by its key, "desk pin loadout headwear"; the made-up stash desk of `-Desk`, its reading n as a reading in the menus brings it, 1 to 3; `open` a folded region's name clicked; `point` the pointer on a region's cell at that grid place, or its first cell, as the pointer's events do, so a still shows a window's light and the rail's card; `plan` and `show` PLAN and DESK at the rail's head, `show <n>` the n-th pinned line in the rail's foot clicked, from the top (UI QA pass of the desk's second round, F3, 2026-10-19); step B, 2026-10-19; the replay `tools\qa\desk.devscript`), `glance [<n>]` (the made-up in-raid glance's reading n, 1 a drawer, 2 a body, as a raid screenshot of an inventory screen brings it, in a raid only: its plate at the top of the map's column for 6 s, or waiting while a cue is up; 2026-10-19; the replay `tools\qa\glance.devscript`), `tour end` (ends the tour as Esc does), `textsize normal|larger|largest` (settings' Text size chosen as its row chooses it and saved in the session's settings; the window's text, the map and the smallest window follow at once, so a `snapshot` before and after pictures the change while Shturmap runs; under `--text-scale` the run's factor stands whatever the step; UI QA pass of 2026-10-19, F4), `language <culture>` (settings' Language for the run, chosen as its row chooses it and not saved: `en-US`, `de-DE`, `qps-ploc`; the window's texts, the desk's page and cards among them, follow at once and the game data in the new language a moment later, so a `snapshot` before and after pictures a switch with the desk's cards up, which the layout check's `-SwitchCheck` can't; release QA pass of 2026-10-19, F1), `wait <s>`,
  `snapshot <folder>`, `exit`, `note <id or name> <text>` (adds a note to the quest as the field on its card does, for
  the session only; the quest one word or quoted, `\n` in the text a line break)), for checks without clicking.
  `DevScript.Verbs` is the one source of truth for the verbs and what each takes (the parser knows no other); this list
  follows it, and `DevToolsTests` fail when a verb there is missing here.
  `snapshot` also saves the view's two tabs, and while a cue is up the cue alone with its transparency (`cue.png`, its
  place in the map's panel in the app log: `window.png` shows that panel empty), writing over
  what the folder had (one folder per moment), and expands `%VARIABLES%` in it, so a script kept in the repository
  names no user's folder. The script's path, and a folder left relative, are read from the app's working folder,
  `%USERPROFILE%` (`Program`). A `--dev-view` session starts on settings of its own, as at a first start, so the tour
  opens by itself once the data is there; `tour end` ends it.
  A step is followed by 0.3 s for the app to read what it wrote; the pointer's steps (`hover`,
  `point`, `trail`, `cards`, `whatsnew click`, `whatsnew leave`) and `wait` write nothing and follow at once, since
  what they check lasts tenths of a second (`DevScript.FollowsAtOnce`).
  A script's run is quiet (owner, 2026-10-15; *Quiet runs*, below): the window and the view open off every
  monitor without taking the focus, and the app ends with the script; `--visible` shows both and keeps the app open
  after a script without `exit`, as before.
- `tools\qa` keeps such scripts for a QA pass, each with what its stills must show beside it: run on a developer
  build, they check in the window what no test reaches. `whatsnew-tour-line.devscript` (2026-10-15) replays the
  owner's report on What's New's `tour:5` line (*What's New*, *The tour*); `extract-roll-call.devscript` (2026-10-15)
  the roll call of a list read on Customs, in two raids, for stills of its start, its middle and its end ("Map
  drawing", *A list just read is called out*).

**Switching the language in a run** (developer builds only; 2026-10-10). `--switch-language <culture>` (`en-US`, `de-DE`,
`qps-ploc`) switches the language once the window shows the game data, exactly as a choice in settings does but for
that run only, and before any snapshot is taken (`--snapshot`); with `--culture` the run starts in one language and
ends in the other. The layout check compares such a run with a fresh one in the second language: every text must be the
same (docs/LANGUAGES.md, "Layout check"). The folder build (`eng\publish.ps1`, a release configuration) hasn't got it;
a Debug build and the dev build have.

**A larger text size in a run** (developer builds only; 2026-10-19, the beta's "text hard to read on some setups").
`--text-scale <factor>` (1 to 2.25) stands in for the whole text size (§4, "Screen anatomy", *Text size*): every text
of the window and its popups is drawn at the size Windows' text size (Settings › Accessibility › Text size) at that
factor would give it, by WinUI's own curve (smaller text grows more), and the map's labels and symbols at the factor,
without touching Windows' setting, so snapshots and their layout files show what clips (the same pass as settings'
step, `Controls.TextScale`). `--text-step <larger|largest>` takes that step of settings' "Text size" for the run, as a
choice there does, without saving it. A developer script's `textsize` step chooses a step while the app runs, as
settings' row does (*The developer view*, above). Simulations for checks; the folder build has neither.

**Quiet runs** (owner, 2026-10-15: checking the UI while playing). A run that only takes pictures, `--snapshot`,
`--demo` (so every run `tools\fake-raid.ps1` starts, and the layout check's) or a developer script's
(`--dev-view --dev-script <file>`, which the QA pass replays flows with), opens its window off every monitor, never
maximised, shows it without activation and keeps it out of the taskbar and Alt+Tab (§4, "Screen anatomy", *The
window*). A script's developer view does the same, beside the main window and not over it (`WindowPlace.Beside`), and
the run ends with its script, `exit` or not: nobody could see its windows to close them. Such runs opened maximised
on the second monitor and took the focus, so a check or a layout check of 25 minutes interrupted a game on the other
monitor. Snapshots lose nothing: they are rendered from the window's XAML (`RenderTargetBitmap`) and the map's own
drawing, never from the screen (a fake raid's window, rail, help, settings and map came out whole off every monitor,
2026-10-15). A window shown without activation is never activated by itself, and its x:Bind values are set at its
first activation: `MainWindow.Open` sets them at once (QA pass of 2026-10-15: help opening had activated the window in
a snapshot run; with the tour opened at the start instead, the window and the tour's band stood without a bound word,
in fake raids with `-Tour` and in every script's run, which starts as a first start). Its flyouts open without taking
the focus (`MainWindow.ShowWithoutFocus`: help by itself, help and settings for their pictures): a focused link shows its
tooltip, and a tooltip is a window of its own that Windows keeps on a monitor, so help's TAKE THE TOUR tip stood in the
top right corner of the monitor beside the window for about twelve seconds of every run, where the person at the PC
could click it. A snapshot run opens the tour only with `--tour` (or again after a raid closed it), whatever else asks,
and the app log says how a tour opened (release QA pass of 2026-10-19: chapter 1 came over the quest's and the desk's
pictures in one layout check, though each run starts on a database of its own in its fake game's folder, where the
first start's tour never opens in a snapshot run). The demo's clip is recorded from
Shturmap's own window (`tools\record-window`), which hasn't been tried off every monitor yet; if it can't find or
record the window there, `-MoreArgs --visible` records it as before. `--visible` opens any such run to watch
(`tools\fake-raid.ps1 -MoreArgs --visible`), a script's with its view as before, until its `exit`. Only the windows'
placement and showing change (AppWindow), no capture, input or hook in the app; normal runs and the installed app are
untouched, and the developer view without a script (`--dev-view`, F12) still opens to be used.

**Animations frame by frame** (owner, 2026-10-15: the QA pass must see animations frame by frame). A window snapshot
takes seconds and shows one moment, so what an animation does over time went unseen. The map is drawn by Skia on the
CPU and everything in it that moves is a function of time, so it can be drawn at chosen times without a window.
`MapRenderer.Render` takes the frame's time (`now`; the clock's where none is given, as in the app) and reads it once
for the whole frame, so a frame at t0 + 100 ms is the same every run; its `trace`, off in the app, records what the
frame drew of each symbol (`SymbolTrace`): where, how large, and for an extract its state and the values its ring was
drawn with (`Arrival`, `Glow`, `Echoes`, `Question`), its icon's flight from the roll call's band (`Flight`,
`Flying`, `FlightScale`; `InView` false while it flies toward a place out of view), the face its name was drawn in
(`Bold`; null where it wasn't drawn) and, for a linked set's lit place, its breathing halo (`Halo`: radius and
opacity) or its static ring (`Ring`; `BreathTests`), taken from the drawing itself, not worked out beside it.
`shturmap-cli frames <map> <folder>` builds a scene from the data cache as `render` does, reads an extract list at t0
(`RingExits`) and draws 36 frames 100 ms apart (`--every`, `--count`) in one fitted 1600 × 1000 view (`--viewport
500x481`: the map at the smallest window, 900 × 560): `frame-000.png`…, `frames.json` (per frame and symbol: its id,
name, state (listed, unsure, notListed, plain or transit), drawn centre and size, centre in map units, offset from its
own place, leader, its ring's values and its name's face; per frame where the scale bar stands) and `summary.txt` (per
frame what changed: turns, rings over, moves, states, names turning bold; frame 000's bold names; then the checks: the
largest move, anything off the list that animated, names bold before their extract was lit or struck out, the scale
bar stepped up from a symbol, how often it changed place and in how many frames it held its place while the view
moved, the first frame against the last). Four scenes: `list-read`, a list read in a PMC raid
(every other extract lit, two "??:??:??", the rest struck out, Scav extracts left out as a raid leaves them out, the
position at the map's first PMC spawn or `--fix`, pinging), `whatsnew-extracts`, What's New's `extracts` preview
as it is staged (a PMC raid's map, so without the Scav extracts, which it kept until 2026-10-15; no quests, no
position; the sets of `MainWindow.ExampleExits`, which the CLI repeats), and `list-callout`, the roll call ("Map
drawing"): `list-read`'s list called out at t0 (`MapScene.CallOutExits`) from the band's rows as the design panel
drew them (the view's middle, columns of three rows 22 px apart, nearest the position first; eight rows on Customs),
the view starting at three times the whole map's zoom on the position and zooming out at 1.2 s over 1.2 s (`--glide
[ms]`) to `Camera.FittingAll` as the window will, drawn every 33 ms 260 times (to 8.55 s, past the eight rows' last
ring), so no frame zooms by the tenth past which symbols are laid out afresh. Its `frames.json` adds the flights
(turn, lift-off, landing, from, where it landed), and `summary.txt` their lift-offs and landings and two checks: every
landing is its ring's start with nothing lit before, and the view it zoomed out to holds every called extract and the
player within the padding and the label room; and `plan`, the map as Plan shows it between raids (render's example
quests and every extract, a PMC's, a Scav's and shared; no list, a position only with `--fix`). Every other scene leaves
the Scav extracts out with `MapContent.InPmcRaid`, the rule GameSession and the app's PMC scenes hold in their own code.
`--glide [ms]` moves the view as `MapView.AnimateView`
does, from the whole map to three times the zoom on the position in 1.2 s (for `list-callout`, its zoom out), and lists
the symbols whose offset from their own place changes by more than 3 px from one frame to the next, which the view's
motion doesn't explain (while it moves a symbol goes a step of 2 px a frame toward its spot; "Map drawing", *Steady
while the view moves*); the checks count the leaders that came or went, and name the symbols whose leader did more
than once. `--notch [ms]` turns the wheel three notches in at the position (without one, the middle of the extracts on
the list), [ms] apart (300 by default) from the whole map's view, as `MapView` animates them (`WheelZoom`; "Map
drawing", *The wheel zooms over 0.15 s*), drawn every 16 ms until the last has landed, with the same checks as
`--glide` (not with it, nor with `list-callout`). `--step <factor> [ms]` zooms by <factor> over 0.2 s as + and − and
the zoom buttons (1.5, 0.667) or a double-click (2) do (`WheelZoom.Step`; "Map drawing", *The keys, the buttons and a
double-click zoom over 0.2 s*), about the position as a double-click there does (+ and − zoom about the view's middle,
which only moves where the zoom centres), with [ms] the same step again that much later; drawn every 16 ms until it
has landed, with the same checks (not with `--glide`, `--notch` or `list-callout`); `--every 33` draws it as at 30
frames a second. `--sway [n]` moves the view back and forth n times (4 by default, 4.5 s each): a glide in as `--glide`'s
and one back out, then three notches in and three out, 300 ms apart, drawn every 16 ms with the same checks (not with
`--glide`, `--notch`, `--step` or `list-callout`); `--toward <name>` glides and turns the wheel at the place of the first
symbol of that name instead of the position. Every run's checks name the farthest a symbol stood off its place
(`LeaderMost` bounds it), and with `--sway` the farthest each time back and forth, of any symbol and of the one
`--toward` names (2026-10-19: two symbols on one spot drifted further with every move, "Map drawing", *Set apart, on
leaders*: `frames woods --scene plan --sway 4 --toward Outskirts`).
With a moving view the checks also name the largest zoom from one frame to the next and count the
frames that zoom by a tenth or more, which lay the symbols out afresh. `ExitRingFramesTests` hold the ring to "Map
drawing" the same way, on a made-up raid, `GlideFramesTests` the symbols on leaders in a glide, in the wheel's notches
and in a double-click's and +'s steps, `WheelZoomTests` the notches' and the steps' zoom, and `ExitCallOutFramesTests`
the roll call's flights; the ring's and the roll call's tests read the names' face too.

### How the parts work

**Finding the game.** Candidates come from the `EscapeFromTarkov` uninstall key (HKLM 32/64, HKCU), the
launcher's `gamesRootDir` (older `gameRootDir`) and its subfolders, `C:\Battlestate Games`, Steam's own uninstall
key for the game (`Steam App 3932890`, the same three places), and Steam's `libraryfolders.vdf` +
`appmanifest_3932890.acf`. A candidate needs `EscapeFromTarkov.exe` or a `Logs\log_*`
session (also under `build`). The newest log session wins, at the start and at every look after it (every 30 s; "No
game", *It keeps looking*); all valid installs feed the quest backfill.

**Screenshot names.** `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`; the trailing number is the in-raid
time of day when a position is present. Facing: `yaw = atan2(2(qx·qz + qw·qy), 1 − 2(qx² + qy²))`.

**Menu screenshots** (§2). The watcher reports a new screenshot (`ScreenshotWatcher`, with the file's creation time to
the second); one without a position, while the log shows the menus and "Read your stash, gear and tasks from menu
screenshots" is ticked, goes to `GameSession.ReadStashAsync`, which hands it to the menus' reader with the items
BRING lists (`MenuScreenshotReader`): the picture is opened once, whole and read-only (`MenuPicture`); the tasks
reader reads the words of its left two thirds (`TasksReader`, about 110 ms) and tells the screen by them (`TaskTable`):
the OPERATIONAL table is read row by row, its bands' digits by vote (about 400 ms more), and goes to the operational
ledger (`GameSession.Operational`: the location matched to a map of the data, `OperationalLedger.ApplyTable` with
its checks, the file; `OperationalReadings`, which tells a run of screenshots and whether the reading brought a new
task, and the cue `CueKind.TasksRead` with every task the run read); a trader's tasks or another Tasks table goes
nowhere; any other picture goes to the stash reader (`StashReader`), as before, and on to the stash's ledger, the cue
and the marks' glow; and, once the item file is in, to the stash desk's reader as well (`DeskReader`, step B part 1),
on the same picture with a recognition of its own words: the whole picture's words once at its size (about 150 ms: the
headings, a window's title, a toast), the screen and its regions from them and the compartments' frames (`DeskLayout`,
`DeskFrames`, `StashGrids` for the stash), then each cell's label enlarged as the stash reader reads it, matched to the
whole catalogue at 0.9 (`DeskMatch`, `DeskData.Catalogue`), about 2 s a 1440p picture in all. The page goes to the desk's
ledger (`GameSession.Desk`: `DeskLedger.Apply`, `desk.json`, the window's attachment; a region read too blank keeps the
page's, one read in part keeps the page's rest, cut at the column's foot, under a window or not in view, and a window
attached to nothing that three readings didn't show leaves the page, and the app log says each; 2026-10-25), and a reading over ten minutes after the item file was fetched asks for it again
(`RefreshDeskPrices`; an hour until 2026-10-19). A snapshot carries the mode's page with its values
(`SessionSnapshot.Desk`: `DeskSheet.Of` over the item file's facts, `DeskData.Flea`, the keep levels `DeskData.Keep`
from the quests' states and the hideout), made again only when the page, the prices or the quests change. A snapshot (`SessionSnapshot.Operational`, `OperationalPlans`) carries the mode's tasks with their
maps as Plan counts them and the hand-overs' items, and the window makes the rows (`Rules.OperationalRail`,
`MainWindow.Operational`), glowing in the ones that arrived or changed, and the cue's rows (`Rules.OperationalCueWords`,
`MainWindow.TasksCue`). A watch over the expiries drops a task as it runs out and publishes again each minute while
tasks are kept, for the rows' words of the minute. "Delete screenshots after reading" waits for all of it.

**Projection.** tarkov.dev's math: rotate (x, z) by `coordinateRotation`, `X = a·rx + b`, `Y = −c·ry + d`, SVG
fitted into the projected `svgBounds ?? bounds`. Golden test: a made-up Streets position, x 40 and z 120, lands at
(284.1, 417.3) in `StreetsOfTarkov.svg`.

**Logs and raids.** Menu → Loading (`scene preset path:`) → InRaid (`GameStarted`) → Menu
(`PrepareSelectedProfileLocally` or matching cancelled). Map: scene path ↔ `scenePath`, then `Location:` ↔
`nameId`, then an alias table. Side: the match-setup `Profileid` equals the menu profile for PMC raids and differs
for Scav raids; locally hosted PvE raids stay Unknown. Quests: `ChatMessageReceived` type 10/11/12, and of a start
(10) and a completion (12) the items the message hands over, for the marks of §4, "What you have": the top-level
entries of `items.data[]` (those whose `parentId` is `items.stash`; a weapon's parts stay inside it), each as its
`_tpl` and `upd.StackObjectsCount` (one when absent), nothing else of the message. Loading steps:
`LocationLoaded`, `GamePrepared`, `GameCreated`, `PlayerSpawnEvent`, `GamePooled`, `GameRunned` (and
`MatchingCompleted` for the study log). Group: `GroupMatchRaidSettings` (its `raidSettings.location` and
`timeVariant`); `GroupMatchRaidReady`, `GroupMatchRaidNotReady` and `GroupMatchStartGame` by their header alone,
because their bodies (and the invites') hold other players' profiles, which Shturmap never reads. The insurer:
`ChatMessageReceived` type 2 (a trader message) with `systemData.location` when insured gear was lost, type 8 (the
insurance return) with the gear hours later (its top-level items read as a quest's, above, for a "Returned by
insurance" mark). Each notification is logged twice ("Got notification" with its body,
then "Received notification"); only the first counts. A raid or load the log leaves open (no end line: the game was
closed or crashed in it) is closed without a length once the game has started again (a newer log session) or it
began longer ago than its map's raid length plus 30 minutes (two hours without a known length), at the last line the
log has of it, never at the next session's login line (`UnfinishedRaid`, `RaidTracker.CloseUnfinished`,
`RaidEnded.EndInLog`; "Screen anatomy", status bar). The raid's map is kept apart from the map on screen
(`SessionSnapshot.RaidMap`; "Screen anatomy", *The raid's map is not the map on screen*).

**Following the logs** (`LogTailer`). The newest log session's application and notification logs are polled (every
500 ms while they grow, every 2 s when quiet), at most 4 MB of a file per poll. What a log holds when it is first
opened at Shturmap's start is replay: it sets the state and shows no cue, notice or ping. When the game starts
again, the session before is read to its end first, under its own name: the rest of a log longer than one read, a
file added since the last poll, and its last line, which nothing follows any more. Skipped, a raid's end or a quest
message was missing for the rest of the run (review of 2026-10-04, A44). A file of the old session that can't be
opened puts the new session off for three polls at most. The LOGS light says "Logs live" while the game writes: a
line since Shturmap started following, within the last ten minutes; replay doesn't count, so a start with the game
closed says "Logs" (A43: it said "live" for ten minutes after every start). Most lines are no event, so the light
is looked at again every 30 s, with the open raid.

**Clock changes** (`WallClock`; review of 2026-10-04, A41). The log's times are local wall-clock times without an
offset, a screenshot's time and the PC's clock are local too, and one taken from another is an hour off across a
clock change, two nights a year. On the spring night a raid begun at 01:50 was 95 minutes old at 03:25 (it is 35)
and was closed as one that can't still be running; on the autumn night a raid begun at 02:50 and ended at 02:10 ran
for minus 40 minutes, and a session read back had its events sorted by those times, the end before the start. So
every span of time goes through one place, which turns wall-clock times into instants by Windows' rules for the
PC's time zone: a raid's length (the cue, the last-raid line), the minutes in the status bar, whether an open raid
can still be running, a screenshot against a raid's end, the study log's timings. What the player reads stays the
local time as written ("started 21:02").
The hour the autumn change repeats can stand for two instants, and a log's time doesn't say which. No reading is
fixed; what is known of the order settles it:
- **Two times in a known order** (a start and an end, a start and now): the reading in which time doesn't run
  backwards, and of two such the one with the least time between. A raid of 20 minutes rather than 80; and an open
  raid that began in the repeated hour counts from its later reading, since closing a raid that still runs is the
  worse mistake. A raid that really ran across the whole repeated hour reads an hour short: its two times alone
  can't say otherwise.
- **Two times close together in either order** (the insurer's note around a raid's end, a screenshot around it): the
  reading that puts them nearest.
- **The lines of one log**, whose times never go backwards: each takes the earliest reading that isn't before the
  line above, starting with the first pass through the hour, because only then is the step back to 02:00 seen for
  what it is. The events of several logs are put in order by these instants (`WallClock.Sequence`, in `LogTailer`).
- A time that is an instant already counts as that: the PC's clock and a file's time, which .NET marks with the side
  of the repeated hour they are on, and a quest message's server time.
A time in the hour the spring change skips, which no clock shows, is read as if the clocks hadn't moved yet.
Not covered: which of two log sessions is the newer goes by their folder names' times, and two sessions started
within the repeated hour could be told the wrong way round; the map's age tag, the position's age in the window and
"recent" screenshots at a start still take one clock time from another (`FixAge.Of` is there for them).

**Quest progress.** Only the game's log counts: quest started/failed/completed notifications, backfilled from every
log session on disk and followed live; newest wins. A message in a log session that names no mode counts for the
mode Shturmap is in, live and read back alike (review of 2026-10-09: the backfill left such a session's quests
out). Prerequisites of active or completed quests that strictly require "complete" are shown as implied, never
stored. A quest started before the oldest log on disk is not known.
Databases from earlier versions may hold Tasks-scan and TarkovEyes-import rows, or quest states once set by hand;
they are ignored (and could never reopen a quest the log saw completed). The logs say nothing of single objectives:
the only word on those is the player's own tick (§4, "Quest cards", *Ticks*), kept apart from the quest states, in
the settings. `shturmap-cli quests` lists active quests with every observation.
`shturmap-cli synopses [mode]` lists every Plan row's synopsis with its flags (§5, "Quest synopsis"), and
`shturmap-cli effort [mode]` every Plan row's effort group and complexity (§7, "Plan order"). What to run and look at after a
game patch or a tarkov.dev update, and what each rule assumes of the data and the game: docs/UPDATES.md.

**Floors.** The floor shown is the player's, from the height of the last fix. The picker lists floors that have
artwork of their own, top first, with a dot on the player's; a pick (click or PgUp/PgDn) holds until the next
screenshot. Floors without their own artwork (Customs' 4th, Reserve's upper floors) are drawn in the base layer.
An SVG map's floors are read ahead, in the background, as soon as the map is shown (`MapArtwork.ReadFloorsAsync`,
started by the map view): a floor's picture used to be read by the first paint that showed the floor, on the
drawing thread, which stood still for that long (the review of 2026-10-04). A paint never waits now: a floor that
isn't read yet is left out of that frame and drawn when it arrives (`FloorRead`), one that can't be read is left
out and said in the app log, and a snapshot waits for the floors. The CLI and the tests, which don't read ahead,
read a floor when they ask for it, as before.
On the maps without SVG artwork the floors are tarkov.dev's tile layers: the shown floor's tiles over the base
layer's, which dims as under an SVG floor (the sheet stands in with the same floors). Map labels with heights
(tarkov.dev's bottom/top) show only on their floor, as on tarkov.dev; labels without heights show on every floor.

**Item sources.** json.tarkov.dev `items` (17 MB; only trader offers, flea level, a last price, the categories and a
gun's default preset are read: the preset pictures the gun, §3, "Trader portraits and item icons"; 2026-10-19),
`barters`, `crafts`, `hideout` (+ translations), fetched after the main data and refreshed daily; loose spawns come
from the maps payload's `lootLoose`. A download that fails for a reason that may pass (no connection, a timeout, a
server error, a page in place of the data) is asked for again by itself, after the waits of "Asking again" below,
until it loads or the mode changes; until 2026-10-04 one failed download left the item cards without sources until a
restart. A failure that won't pass by itself (data in a shape Shturmap can't read) is noted in the app log and not
asked for again in that session.
When the game language's texts arrive late (below), the sources load again in that language: station names are
part of the texts.

**Asking again** (`RetrySchedule`; owner, 2026-10-04: no unnecessary load on tarkov.dev). After a failure that may pass,
tarkov.dev is asked again after 2, 4, 8 and 16 minutes, then every 30 minutes for as long as it fails; a success, or the
player changing the game mode, starts over at 2. It was every 2 minutes without end: with tarkov.dev down, every running
Shturmap asked for up to 11 files every 2 minutes, about 700 rounds a day where there are now about 50. One schedule,
counted separately, for the game data, the game language's texts and the item sources. A try asks only for what is
missing or older than its keep time: the rest is answered from the saved copy. An answer 200 that isn't JSON at all (a
page a sign-in portal, a network filter or a CDN sends in place of the data, or a body that is empty or cut off) is
never saved (`CachedHttp`, json; owner, 2026-10-09). It counts as a failed download: a saved copy is used in its place
("Data (offline copy)") and is asked for again at its next keep time; with none the load fails as a page in place of the
data, not as unreadable (`LoadFailure.NotData`): it says nothing of tarkov.dev's format, and the page goes when the
player signs in to the network or leaves the filter, so it is asked for again after the same waits as a failure that may
pass. Until then such a page was saved like the data: it replaced a good saved copy (so once that was older than its
keep time the player saw "No game data", not the offline copy), failed as unreadable, which isn't asked for again, and
was read again at the next start for as long as it counted as fresh (review of 2026-10-09). A page saved by an earlier
version is forgotten when a load can't read it (`GameDataLoader.ForgetWhatIsNoJson`), and that load fails as NotData.
JSON in a shape Shturmap doesn't know (tarkov.dev changed its format) stays saved and is revalidated as before, and
stays unreadable, not asked for again: downloading it again would bring the same, at every start of every Shturmap. A
failed download is never a reason to forget a good saved copy.

**The app's own language** (owner, 2026-10-10: multi-language support, English and German first, every language
tarkov.dev has to follow). **One language for everything**: Shturmap's own texts, its number and date formats ("Pay
5,000 ₽" and "25 Sep" in English, "5.000 ₽" and "25. Sept." in German, never Windows' formats inside another language's
sentences; review of 2026-10-04) and the game data's names all follow one choice (owner, 2026-10-10: "the app should
switch languages entirely, otherwise that makes it weird mixing the two"). `UiLanguage.Choose` makes it:
1. the player's choice in settings, "Language: Automatic · English · Deutsch", each language in its own name;
2. else the game's language (its settings' `Language`, `GameLanguage.Common`), when Shturmap's texts are complete in it;
3. else Windows' display language (the one Windows' own menus are in, read once at start before anything sets the
   app's), when they are complete in it; Windows' regional formats don't count;
4. else English.

A game language Shturmap's texts aren't written in yet (Russian, say) keeps the game data in it: the names stay as the
game shows them, and only Shturmap's own words take Windows' language or English, as before 2026-10-10, until that
language's texts are written (owner, 2026-10-10). The pseudo-language (`UiLanguage.Pseudo`, developer runs only;
docs/LANGUAGES.md) shows English game names. A language is offered, and chosen by itself, only once every text of it is
translated and reviewed (`UiLanguage.Supported`: English, and German from 2026-10-10, once its texts, tour and What's
New were reviewed and the layout check passed in it); one being translated (`UiLanguage.InTranslation`, none at the
moment) shows only in a developer build's settings and a developer run (`--culture fr-FR`), English where a text isn't
written yet. The language **switches while Shturmap
runs** (owner, 2026-10-10), from settings at once: every text on screen, the formats and the map's labels change, the
game data is loaded in the new language as when the game language's texts arrive late (below), and what was on screen
is the same as after a fresh start in that language (the layout check compares the two; docs/LANGUAGES.md). A new
notice is said in the new language; one already shown stays as it was said. Logs, the study log, diagnostics, reports
and the CLI stay English: they are read by whoever fixes Shturmap. The rules that read English data, all but one,
still read only English: in another data language a Plan row shows the quest's name alone and the raid card an
objective's own sentence, tarkov.dev's (quest synopses, §5). The one reads German too (owner, 2026-10-11: German cards
kept both lines and were several lines longer): a quest card leaves out an objective's line of map names where its
sentence already says where, "… auf Streets of Tarkov" as "… on Streets of Tarkov" (`QuestCards.SaysWhere`; §4, "Quest
cards"). German says where with "auf" or "in" before the map's name or a list of them, as tarkov.dev's German data
writes them (the English names but for "Factory bei Nacht"; no comma before "oder"); "nach", "aus" and "von" say where
to or from. In another data language both lines stay. In tarkov.dev's German PvE data of 2026-10-11 the cards left out
363 of 936 lines (the English cards 577), none under a sentence that doesn't say where; the 573 that stay are under
sentences that name none or not all of the line's maps (most of them), say where to or from (17 extracts "aus …", 17
transits "von … nach …"), or say "on …" in English, still untranslated in the German data (151). The extract
requirements read the extracts' English names, kept beside the data in another language (`GameData.EnglishName`), so
they come out the same in every language. The floor buttons keep tarkov.dev's names in English in every language ("2nd
Floor", "Underground", Icebreaker's decks: the layer names of its maps.json, which it has in English only), beside
Shturmap's own word for the ground floor, where the map names none (`AppTexts.MapFloorGround`, German "Erdgeschoss";
owner, 2026-10-11): the game never names its floors, so there is no game word to show instead, and these are
tarkov.dev's labels for its maps, data like the rest, not Shturmap's words.

How it is made (2026-10-10). The setting is `language` in shturmap.db: `auto` (or none) or a language's code; a value
that isn't offered (a language only a developer build offers, read by another build) counts as automatic. Windows'
display language is read once, as the app starts, before anything sets the app's culture. The session opens before the
window (`GameSession.Open`: the app's settings, the game and its settings), chooses and keeps the choice
(`GameSession.Language`), and the app sets Shturmap's texts to it (`UiLanguage.Set`) before the window builds them, so
a start shows no English first. A choice while running: the session chooses again and, when the game data's language
changes, loads the data and the item sources in a new round, as a change of mode does (what the old language still
waited for is dropped, the waits of "Asking again" start over, a load in the old language that ends later is dropped),
the old data staying on screen until the new is here and the maps on screen named anew. The window switches Shturmap's
texts on its own thread, reads every x:Bind again (`Bindings.Update`, the cards' too), sets again what code sets over
XAML from its state (the tour's chapter, the report dialog's links and its example while it stands as put there, help's
legend, What's New, the question after a crash), has the controls that word their own tooltips say them again (the
pen, the tick, a quest type's glyph; `Controls.SaidAgain`), builds the stash desk's page and cards and settings' row of
the copy's rule again, with their figures in the language's format (`MainWindow.RetellDesk`, the page standing; release
QA pass of 2026-10-19: they kept the old language), makes the rows drawn from templates anew, and the session
composes its snapshot again (`GameSession.RepublishAsync`: the plans and the screenshot keys' names with it); the
tour's example is staged again once the data has come in the new language (review of 2026-10-10: the layout check's
switch found these kept from before). The snapshot takes the language in use each time it is made
(`UiLanguage.ApplyHere`), and so does a developer snapshot's picture: the current culture travels with async work, so
a loop started before a switch would sort and format in the old one. Names are sorted with the language in use's own
comparer (`StringComparer.Create(UiLanguage.Culture, …)`), never the thread's culture: the plans, a completion's
UNLOCKS, the cards' lists, a Scav raid's loot, the rail's objectives and exits. The plans and a completion are made in
the session's loops before any snapshot, so a loop started before a switch made its next plan in the old language's
order (review of 2026-10-10).

**Texts.** Every word Shturmap shows is in a project's texts file, never in code or XAML: `CoreTexts`, `MapTexts`,
`DataTexts`, `GameTexts`, `SessionTexts`, `AppTexts` (the app's code), `ViewTexts` (its XAML) and `RuleTexts` (the
app's rules, which the Core tests compile too). Each is a `.resx` with the English texts and one `<Name>.<code>.resx`
per language. A build makes a class of each English file (`Directory.Build.targets`, `ShturmapTexts`): a property per
text, a method per text with placeholders, its parameters the placeholders' names in alphabetical order, called with
named arguments (`AppTexts.CueExitsListed(count: n, total: all)`), so a misspelt or missing text, or a placeholder left out, is
a build error. A text is looked up in the language in use when it is shown (`UiLanguage.Text`), never kept from
before, which is what lets the language switch while Shturmap runs; XAML binds to the classes (`{x:Bind
local:ViewTexts.HelpAndFeedback}`) and the window updates its bindings when the language changes.
- **Whole sentences with named placeholders**, never pieces glued together or a word chosen by code: each language puts
  the words where its grammar wants them. Plurals are part of the text, in the syntax of ICU's MessageFormat that
  translators know: `{count, plural, one {# extract} other {# extracts}}`, with Unicode's plural forms of every
  language tarkov.dev has (`PluralRules`: Polish and Russian need three or four, so code never picks "extract" or
  "extracts"). `TextFormat` fills them in; a text that doesn't parse shows as written rather than failing. A quest
  type symbol's tooltip is one text per type, help's table keeping its own two (owner, 2026-10-11: it was "{label}:
  {explanation}", the table's name and explanation glued, so German, which writes lower case after a colon unless a
  whole sentence follows, read "Erkunden: Zu einem Spot gehen").
- **Game names stand where they don't change form**: "Bring: {item}", not "Bring the {item}"; Russian and Polish
  change a noun's ending with its place in the sentence, and the names come from tarkov.dev as they are. A category's
  name inside a sentence ("Any sniper rifle") is in lower case only where its language writes a noun so: German
  writes every noun with a capital (`UiLanguage.InSentence`, one rule per language).
- **Numbers** in a placeholder are written in the language's format; a number that needs grouping (prices, "5,000 ₽")
  is formatted by the caller with `UiLanguage.Culture`. Dates and times likewise; a day and month inside a
  sentence with `UiLanguage.DayMonth` ("4 Oct", German "4. Okt.") or `DayMonthTime`, one table of each language's
  patterns.
- **A comment on each English text** says where it shows and what its placeholders are, for the translator.
- **Each translated text says which English text it was made from** (its comment: `en: <the English text>`). The
  translation tests (`TranslationTests`) check every file: the English texts parse, a translation has the English
  text's placeholders and its language's plural forms, and was made from the English text as it is now; a language
  that is offered has every text. So changing an English text means translating it again in the same commit, reviewed
  as below (docs/LANGUAGES.md, "Translating"); changing a translation alone needs only the review.
- **Not translated**: internal values (settings' stored values, ids, file names, log lines), and the words of the
  game itself that Shturmap reads (the extract list's header in a screenshot): those are matched in the game's
  language, not shown. A log line or diagnostics that says what a text says takes its English
  (`UiLanguage.InEnglish`; `LoadProblem.InEnglish`, `RetrySchedule.InEnglish`), never the language in use.

**Language.** The game data comes in the language chosen above (`LanguageChoice.Data`), translated by tarkov.dev's
`<payload>_<language>` files. The extract list's reader still reads the game's language, which is what the screenshot
shows: when the data is in another language, the maps' texts in the game's language are loaded beside it (`maps_<code>`,
about 25 KB, kept for an hour as the other texts), so an exit is matched by its name in the game's language, in English and in the language shown (`GameData.GameNames`); when they can't be had, by the other two, without a word,
until the data loads again. The game names some languages its own way, and tarkov.dev answers those with 404:
`GameLanguage.Common` maps `ge`→`de`, `cz`→`cs`, `jp`→`ja`, `kr`→`ko`, `po`→`pt`, `tu`→`tr`, `ch`→`zh`,
`es-mx`→`es` (2026-10-02: a friend's German game asked for `maps_ge`, and no data loaded at all), one table for
tarkov.dev and for the extract list's text recognition (review of 2026-10-09: each had a copy). A language
tarkov.dev still lacks falls back to English for everything, so the data stays one language and always loads.
Only "not found" (404) says the language is lacking. A translation that fails any other way (no connection, a
timeout, 5xx) with no saved copy is never said as "No German texts on tarkov.dev" (until 2026-10-04 any failed
request read as a missing language, so a moment's 503 left the session in English with an untrue notice). The data
then loads in English with the failure named (`GameData.LanguageFailure`), and the session says what is true:
"Showing English: the German texts couldn't be loaded. tarkov.dev answered 503. Shturmap tries again in 2
minutes." It asks for the texts again in the background, after the waits of "Asking again" above, until they load;
then the data, the map names on screen and the item sources change to the game's language, and a quiet notice
says "German texts loaded." The first fix of 2026-10-04 failed the whole load instead, which was true but left a
player with no data at all for as long as one translation file kept failing (review of the same day, A46). A
failure that won't pass by itself (tarkov.dev answers 403, say) is said with "Please report it." and not asked for
again in that session. A load that fails as a whole also looks at the downloads it never came to await, so their
failures don't turn up later as crash records.

**Study log.** Developer builds only (below), in their data folder: `%LOCALAPPDATA%\Shturmap-dev\study\yyyy-MM-dd.jsonl`, one JSON object per line: `t`, `src` (`game` or
`ui`), `ev`, event fields, and `ctx.*` (raid phase, map, raid minutes, age of the last fix) on every line, so UI
use can be lined up with raids and quest completions later. Game: app start/exit, data loaded (with the top
suggestion), mode, raid loading (with the suggestion rank of the map actually played and the bring list), raid
start/end, quest started/completed/failed (live only), each fix, each notice. UI: window focus/blur and pointer
in/out (with durations: the closest signals to attention), map pan (one per drag), zoom (one per wheel burst,
button or key), fit, follow, floor picks, map picks (picker or plan card), selections, hovers resting ≥ 0.4 s
(where: list, card, map), card open/hold/close (level, seconds open), help open/close, keys, rail scrolls, notice
dismissals. Added after the first study (2026-10-01): the plan's
COMPLETE and PROGRESS quest ids at raid load (to check which got completed), the evidence for each side decision,
screenshots that gave no position, positions that weren't shown (no raid in the log, or the raid's map not known),
whether a new position was in view and uses of the edge arrow and F, map
previews, notices expiring vs
closed, the quests visible in the rail and the rail's scroll position when the window gets focus, stale-position
banners seen (until the banner went, 2026-10-03), side switches, why a session started (the previous one ended cleanly or not, the build's time), and
active-quest count changes outside quest events. Added 2026-10-02: group picks (location, map, time variant) and
group ready / not ready / start; each raid's loading steps with their seconds since the scene line (in
`raid.start`); the raid clock in each fix; the insurer's notes (kind, location, item count). Added 2026-10-04:
cards closed by a step into a raid (`cards.raid`), whether help was closed by the player or by a raid, and the
status bar's words going or coming back with the bar's width (`statusbar.words`). Added 2026-10-07: the raid replay
(`replay.play` with how it was asked for, its positions and minutes; `replay.end` with why; `replay.skip` for a raid
with too few positions) and What's New (`whatsnew.show`, `whatsnew.point` with the line, `whatsnew.close` with how:
×, a raid, or the line from help); a quest completed joining the QUEST COMPLETE cue on screen (`cue.join`, with how
many it says). Added 2026-10-17: a trader's focus held and let go (`trader.hold`, `trader.release`, with the trader's
id and how: a click, Esc). How a raid ended
(survived, killed) is in none of the allowed logs. The nearest thing is `raid.outcomeHint` `{ lostInsured: true }`:
the insurer's "lost" note came during the raid or within 5 minutes after it, on the same location (in the owner's
logs it came 17–20 s before the raid's end line). It is a hint for later studies of the plan's accuracy, never shown;
no note proves nothing, since gear may not have been insured. Only Shturmap's own window is observed; nothing is
sent anywhere.

The study log is **in developer builds only** (owner, 2026-10-03: "The study log should only be part of the dev
version and not be in the release version"; earlier that day it was the player's choice in every build, off by
default). In a developer build (the dev build, Debug builds: `DEVTOOLS`) it is on unless "Keep a study log" in
settings is unticked, with one line saying it is for developer builds, what it records, that it stays on this PC
(where) and that nothing is sent. The switch is saved in the app's settings (`studyLog` = `on`/`off` in
`shturmap.db`; a Debug build's `shturmap-cli study [on|off]` sets it without the app); `--study` keeps it for one
session without changing the switch; snapshot and fake-game runs never keep one. Days older than 30 are removed at
start, on or off. Ticking it mid-session writes `study.on`, unticking writes `study.off` and stops.

A release (and the folder build) compiles it out under `#if DEVTOOLS`: no switch in settings, no `--study`, no
setting read, no pruning, no line in diagnostics or the app log, so it never creates a `study` folder. The
`Study.Ui` and `Study.Game` calls stay in the code and do nothing there (`StudyLog.Available` is false, so `Enabled`
stays false and the writer is compiled out). Study days an earlier build left in `%LOCALAPPDATA%\Shturmap\study`
stay as they are; a release never reads, prunes or deletes them, and "Also delete my Shturmap data" removes them
with the rest of the folder. `DevToolsGuardTests.A_release_build_has_no_study_log` checks a Release build on the
PC (no switch, no `--study`, no setting key, no writer).

**App log** (owner, 2026-10-03). `%LOCALAPPDATA%\Shturmap\logs\shturmap-yyyy-MM-dd.log`: a short support trail,
not a trace. INFO: the start (version with commit, installed or folder build, Windows), the game, logs and
screenshots folders found, the game's language and screenshot key, mode, data loaded (from where, language, counts,
when checked), raids loading, starting, ending, the study switch (developer builds). WARN: something degraded but working (the data
from the saved copy, English instead of the game's language, a folder not found, the item sources or a map's
artwork not loaded). ERROR: what failed, with the exception. A step of the session's background work that fails (a
line of the game's log, one log session's quest history, a look for the game, a position) is one ERROR line per kind
of failure, and the work carries on (review of 2026-10-04: one database or file error used to end log following or
the quest history for the rest of the session, with no line and the LOGS light still "live"). DEBUG only with `--verbose` (and the website demo,
which times its clip by its lines): notices, the map surface, snapshots, fake games, the demo. Every line is
written with the profile folder as `%USERPROFILE%` (any case, either slash; that covers Documents, OneDrive and
AppData under it); any other folder directly under a `Users` folder (another account's, or the profile's short
"8.3" name) as `<user>`, the user's name as a folder elsewhere (Documents moved to another drive) as `<user>`, and
the host of a network path as `<host>` (`PathMask`; review of 2026-10-04: only the exact profile folder was masked);
no profile or account ids, no game file contents. At most 1 MB a day: a last WARN line says so
and nothing more is written until the next day. Seven days kept; the old `spotter-*.log` files are removed.

**Error messages** (owner, 2026-10-03). What the player sees says what failed in plain words, with the status where
there is one, and what to do; the exception goes to the app log only. Data (`LoadProblem`): "Couldn't reach tarkov.dev,
and there's no saved copy yet. Check the internet connection."; "tarkov.dev didn't answer in time…"; "tarkov.dev
answered 503. It is busy or down for a moment…"; "tarkov.dev answered 404. Please report it."; "tarkov.dev's data has
changed in a way Shturmap can't read. Please report it." (JSON in a shape Shturmap doesn't know); "tarkov.dev's answer
wasn't its data: a sign-in page or a filter in between? Shturmap tries again in a few minutes." (a body that isn't JSON
at all, with no saved copy to use: a sign-in page, a network filter's or a CDN's page sent with 200, an empty or cut-off
body; owner, 2026-10-09: it was said as unreadable, which blamed tarkov.dev, asked for a report that couldn't help, and
wasn't tried again); "Couldn't save tarkov.dev's data on this PC…". It comes as a notice (30 s) after "No game data.",
the DATA chip says "No game data" and its tooltip says why. A failure that may pass (no connection, a timeout, 5xx or
429, a page in place of the data) is tried again after growing waits ("Asking again", above: 2, 4, 8, 16 minutes, then
every 30), said once; the notice names the wait that follows it ("Shturmap tries again in 2 minutes; if it keeps
failing, please report it."), and the tooltip, which stays up while the tries go on, names none ("tries again by itself,
at first after 2 minutes, then less often"). A page in place of the data asks for no report, in the notice or the
tooltip, and its notice has no REPORT link: what sends the page is between the PC and tarkov.dev, where a report can't
change it ("…a filter in between? Shturmap tries again in 2 minutes."). A download whose answer starts and then stops
counts among them: after 30 s of silence it is a timeout, and a connection that breaks off mid-answer is "couldn't
reach", not a disk problem (`CachedHttp.BodyIdleLimit`; the client's own timeout ends with the headers, so until
2026-10-04 such a download never ended and "Loading game data…" stood for good). A notice that asks for a report carries
a REPORT link to the Report dialog (COPY DIAGNOSTICS in a build that can't send). With a saved copy the data loads from
it (DATA chip "Data (offline copy)"). No texts in the game's language: a quiet notice, "No German texts on tarkov.dev;
showing English." Game not found, or found without its Logs folder: the line in place of the Plan card says so, for as
long as it lasts, and no notice (§4, *No game*). A map's artwork that doesn't download: "No map artwork for Customs:
couldn't download it; check the internet connection. A 10 m grid stands in…", once per map; a tile render that can't be
had: "No map render for The Lab: …", the same way.

**Diagnostics** (owner, 2026-10-03). Help ends with quiet links: COPY DIAGNOSTICS, LOG FOLDER ↗ (opens
`%LOCALAPPDATA%\Shturmap\logs`), PRIVACY ↗ and LICENCES ↗. Copy diagnostics puts plain text on the clipboard and says
"Diagnostics copied: paste them into your message.": Shturmap's version with commit and whether it is the single
exe; Windows' version and build; the install type (Steam, BSG launcher, manual) and whether the game, logs and
screenshots folders were found; mode; the game's language, Windows' display language, the language in use with where
it came from (the setting, the game, Windows, the default; "this run only" for "--culture") and the game data's, and
tarkov.dev's as loaded (with "extract names also in de" when the game's own names were loaded beside it); the data's
state (loaded, offline copy
or failed with the kind, status and plain reason, and when it was checked); the active quest count; whether "Delete
screenshots after reading", "Read the extract list from screenshots" and "Read your stash, gear and tasks from menu
screenshots" are on (§2; the two readings with the recognition's language); the study log
on or off (developer builds); and today's last 200 app-log lines. Paths are masked, every 24-digit id (profiles, accounts, quests) is
cut to `<id>`, and no quest names or lists go along (`Redact`), nor anything of the stash file, the operational tasks
file or the notes file.
Copying sends nothing; a report carries the same text when the player keeps "Include diagnostics", shown first.

**Reports** (owner, 2026-10-03: "as easy as possible … one coherent easy to use thing, otherwise people won't
bother", and crash reports as a mode the player chooses; Sentry, EU region; crash reports "Ask after a crash" by
default). One way to report, from the app, with no account and no browser: no GitHub issue forms, no separate web
form. Problems and ideas both go through it.
**GitHub Issues beside it** (owner, 2026-10-09: "Keep github issues on"; "mention GitHub Issues in the README and
website"). The repository's issues stay open as a second, public route for players with a GitHub account. The README,
the website and the release notes name them after the app's own button, and say that issues are public, so the
game's logs, screenshots and ids stay out of them (the app's report leaves those out). The app itself points to its
button only. One issue form (`.github/ISSUE_TEMPLATE`; owner, the same day: "add the issue template") says it is
public before anything else, asks for a problem or an idea, what happened or would help, and the version, and can't be
sent until "I left out the game's logs, my screenshots and ids from my game" is ticked. Blank issues are off, so
every issue goes through it, and the app's report is offered beside it.

- **The Report dialog.** The feedback button at the top right, the REPORT link on notices that ask for a report, and
  ADD A NOTE after a crash report was sent all open it. PROBLEM | IDEA (the placeholder follows: "What happened,
  and what did you expect?" / "What would help, and when would you use it?"), the text (required, at most 4,000
  characters), a contact for a reply (optional, e.g. a Discord name or an email, at most 120), "Include
  diagnostics" (on; SHOW WHAT'S SENT shows exactly what goes: kind, text, contact, version and Windows, and the
  diagnostics as attached), one line on where it goes with a Privacy link, CANCEL and SEND. Cancel keeps the text
  for the session; Esc closes it; the map's keys don't fire while it is open. After Send it says "Sent. Thank you.
  Report 3f2a91c0." (the id the player can quote), or that it couldn't go now and is kept to go by itself at the
  next start, or that it was refused and is kept on the PC. It never loses the text: a report is written to
  `%LOCALAPPDATA%\Shturmap\outbox` before it is sent and deleted once it has gone; the outbox is tried again at
  each start; a refused one is kept as `.refused.txt` and not tried again.
- **Crash reports.** Unhandled exceptions (the UI's, a background thread's) and unobserved task exceptions are
  written on the PC at once as crash records in `%LOCALAPPDATA%\Shturmap\crashes` (JSON, masked): the exception
  chain (outermost first, five at most) with each frame's module, method, file name (never its folder) and line;
  version, build kind, Windows; the last 50 app-log lines. The same exception is written once; errors the app
  survives at most three times a session, the same one once. A native crash (Skia, ANGLE) or a kill leaves no
  exception: each running Shturmap holds `crashes\running-<session>.json` open and a clean end deletes it; at the
  next start a marker no Shturmap holds, from a session that left no record and from after Windows last started
  (before that it was a shutdown), becomes a "closed unexpectedly" record with that session's last log lines. The
  next start then does what "Crash reports" says (`crashReports` in `shturmap.db`): **Ask** (default) shows the one
  question ("Shturmap closed unexpectedly last time. Send a crash report? It helps to fix it."; for errors it
  survived, "ran into an error"), with SEND, DON'T SEND, ALWAYS SEND (sends and switches the setting) and WHAT'S
  SENT (the records as text, exactly what goes); closing it asks again next time. **Always send** sends at the
  start and logs it. **Never** keeps them on the PC only. A record the player said Send to that couldn't go yet is
  sent at the next start without asking again, unless the setting is Never by then: choosing Never takes back an
  earlier Send too, at once and at the next start, and such a record stays on the PC (2026-10-04: approved records
  went out before the setting was read). A sent one is deleted; records are kept 30 days, twenty at most.
  Never sent: memory dumps, screenshots, game files, ids, quest lists.
- **How it goes out.** Sentry (Functional Software, Inc.), EU data region. Only the SDK's event types and envelope
  format are used: `ReportEnvelopes` builds the envelope from what the player saw and `ReportSender` posts it, so
  nothing is added on the way (no machine name, installation id, IP, breadcrumbs, sessions or tracing) and the
  SDK's automatic client, which would capture on its own, is never created (`SafetyTests`). A problem or idea is
  Sentry user feedback (contact as email or name) with the diagnostics attached as `diagnostics.txt`; a crash is an
  event with the exception chain and frames (cause first, caller first, so Sentry groups the same crash; an
  unexpected exit is one group) and `log.txt` attached. Tags: kind (problem, idea, crash), report id, build kind,
  source; release `shturmap@<version>`; the time it happened as an extra. The Sentry package also writes the build
  folder into referencing assemblies (`Sentry.ProjectDirectory`), which holds the developer's user name;
  `Directory.Build.targets` turns that off, and `SentryNative` is off. Its source generator copied every build
  property, the project folder among them, into `Shturmap.dll`: `SentryDisableSourceGenerator` is on. And every
  DLL carried its PDB path, with the user name in it: `PathMap` in `Directory.Build.props` maps the repository to
  `/_/`. A release build's files were scanned on 2026-10-03 and none holds the user name; scan again after adding a
  package (the release must carry nothing of the developer's machine).
  **No address and no place.** Sentry sees the sender's internet address as any server does, and derives a town and
  country from it even with the project's "Prevent Storing of IP Addresses" on (its scrubbing docs; a report's "User
  Geography" showed the owner's town, 2026-10-09). So every report says `infer_ip: never` in its `sdk` and carries an
  empty `user.ip_address` (null, which Relay takes as "don't infer"; `ReportSender.Sealed`), and the project has an
  Advanced Data Scrubbing rule, [Remove] [Anything] from [`$user.geo.**`], which Sentry documents as the way to drop
  what it derived (added by the owner, 2026-10-09). IP storage is off and the default scrubbers are on
  (owner, 2026-10-09).
- **Where to.** The DSN is not in the repository: `eng\release.ps1` and `eng\publish.ps1` pass it from the untracked
  `eng\sentry.dsn` (gitignored) or `SHTURMAP_SENTRY_DSN` (which the Release workflow sets from the `SENTRY_DSN`
  Actions secret) as an assembly attribute. Only `https://…sentry.io` is
  accepted (or this PC over http, for tests). A build without one (developer builds, forks) shows the Report link
  greyed with "Reporting isn't set up in this build: copy the diagnostics and send them to whoever gave you
  Shturmap."; notices then link COPY DIAGNOSTICS. Snapshot, fake-game and demo runs never send anything.
- **Said honestly.** Help, the README and THIRD-PARTY-NOTICES say "Nothing is sent unless you send a report or
  allow crash reports". `PRIVACY.md` (bundled as `privacy.txt`, opened by PRIVACY ↗ and the dialog's Privacy link)
  says what a report and a crash report hold, who receives them, why (consent: Send, or Always send), how long and
  how to ask for deletion: a report from the app that quotes the report id. Short and plain on purpose (owner,
  2026-10-09: "Short and plain"): reports go to "Shturmap's maintainer", kept at Sentry for at most 90 days, with no
  legal name, no published address and no legal-template wording.
- **Checking a release.** `Shturmap.exe --send-report "<text>" <folder>` opens the Report dialog with the text; press
  Send: the dialog must say "Sent. Thank you.". A release never sends on a command line alone (review of
  2026-10-04: any shortcut or program could have made an installed Shturmap send a report); only a developer build
  sends by itself, saves the window to the folder and exits. Crash reports are only tested against the
  local stand-in (`FakeSentry` in the tests), never sent for real.

## 9. Status

- Done: discovery, watchers, raid tracking, map with floors, player, facing, trail, extracts, transits, quest
  markers, objectives by distance, log backfill, live quest events, safety test,
  self-contained publish, taxonomy, raid planner, requirements, raid line, help panel, keyboard shortcuts,
  occasional-position UX (fix age beside the marker, compass directions), kit reminder on raid load (cue pictures, CHECK YOUR KIT), linked
  highlighting, quest cards (hover, held, nested), item cards with sources and loose
  spots, trader portraits and item icons, floor picker, study log, Tarkov-style visual language.
- Next: read a study log from a real session and correlate it with quest completions (`shturmap-cli` command).
- Named **Shturmap** (owner, 2026-10-01; was Spotter): Shturman, the navigator, plus map, and a word of its own
  so a search finds the app rather than the Woods boss. The old data folder and database move over on first start.
- The Lab, Labyrinth and Icebreaker are drawn from tarkov.dev's tile renders (§3, "Maps without SVG artwork";
  2026-10-03), as a sheet when no tile can be had (2026-10-02).
- 0.1.0 went to friends as one self-unpacking exe (2026-10-03). From 0.2.0, the first public release, a Setup from
  GitHub Releases that keeps itself up to date (Velopack; §8, "Distribution"): 106 MB to download, 244 MB installed.
- Reports and crash reports from the app, through Sentry (2026-10-03; §8, "Reports").
- Releases built, attested and published by the Release workflow on GitHub, the package audit and the repository
  scan in CI, pinned actions, OpenSSF Scorecard, and the README's "Check it yourself" (2026-10-09; §8,
  "Distribution" and "Continuous integration").
- Objective progress: by ticks the player sets on the quest card (2026-10-04; §4, "Quest cards", *Ticks*), since
  the logs don't report it.
- "Delete screenshots after reading" (2026-10-04, "Delete position screenshots" until menu screenshots joined on
  2026-10-14; §2): off unless ticked, the one thing Shturmap changes outside its own folders.
- Open: the installed size (budget 80–120 MB, needs trimming). Declined (owner, 2026-10-04): editing quests by hand,
  opening with the game or with Windows, a text size setting. Declined (owner, 2026-10-07): shortcuts for zooming and
  panning that work while the game has the focus. They would need a global hotkey (§2), and in a week of the study log
  only about a fifth of the times Shturmap took the focus during a raid were for the view alone; over half were for
  quest rows and cards.
- The tour at a first start, with first-time lines for the first raid and position (2026-10-09; §4, "Screen anatomy",
  *The tour*).
- What's New, a card in Plan's rail with each line previewed on the map, and the raid replay in the RAID OVER cue with
  REPLAY on the last-raid line (2026-10-07; §4, "Screen anatomy"; "Map drawing", *The raid replay*). Designed and
  deferred (docs/NEXT.md): marks for what the player has. The QUEST COMPLETE cue and a completed quest's places
  ringing out (2026-10-07; §4, principle 11; "Map drawing").
- The extract list's roll call, designed and built (2026-10-15; docs/NEXT.md; §4, principle 11; "Map drawing", *The
  player's extracts this raid*): a list just read is called out in YOUR EXTRACTS THIS RAID and each extract flown to
  its place; after it, a later list rings its additions where they stand, as lists did from 2026-10-14.
- A new version said louder, without a pop-up (2026-10-15; docs/NEXT.md; §8, "Updates"; from a panel of four: B): the
  update band pinned under MAP between raids, and the UPDATE READY cue as the download completes and once per raid
  while it waits.
- The daily and weekly operational tasks, read from a screenshot of the Tasks screen's OPERATIONAL tab (2026-10-19;
  docs/NEXT.md; §2, "Menu screenshots"; §4, *Operational tasks*; from a panel of four: B): framed glyphs on NEXT RAID's
  rows, an OPERATIONAL section in the open card and the raid card, a Scav's hand-over items under FIND IN RAID; the
  same day, after the owner tried the dev build, the section on top of the quest list (to move under the picks: owner,
  2026-10-19, "Ordering - under picked."), the task that ends soonest
  first, and the cue DAILIES AND WEEKLIES for a reading that brings new tasks, one for a run of screenshots.
- Removed (owner, 2026-10-09, the pre-release review): popped-out quest cards (§4, "Quest cards"); the keyboard's
  steps through the rail (§4, "Keyboard"); the raid clock's decode at each minute ("Screen anatomy", *The readout*).
