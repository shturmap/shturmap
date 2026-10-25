# Next: more from the game's logs

Plan for the next session (written 2026-10-01, evening). The owner asked for items 1–4 below; they come from a
read of everything in their own application and push-notification logs (a few dozen sessions over six weeks).
Item 5 (one design system) was added on 2026-10-02, item 6 (no game installed) on 2026-10-03. Items 1, 3 and 4 are done
(2026-10-02), item 5 on 2026-10-03, item 6 the same day (`9ff9268`, merged in `d8b1bb0`); 2 is open.
`docs/DESIGN.md` stays the binding spec: update it in the same change as each item.

## 2026-10-25: the layout round: a steady desk, the reading's faults, the trader's task view

**The ask** (owner, 2026-10-25, after the test of 2026-10-19): "From the last testing session it is clear that we have to
work on the layout a lot. Sometimes it doesn't properly detect the layout so it might be a good idea to have a consistent
layout" ("the found items from the OCR and the layout of the loadout and stash"); and "when a daily or weekly is
screenshotted from the traders quest accept view, it is not added to shturmap."

**Found** (the dev app log of 2026-10-19 and `shturmap-cli desk` over the 13 menu screenshots of that evening, 20:46 to
23:15; the pictures stay outside the repository, as the git-ignored fixtures do):

- Every GEAR screenshot shows a different set of regions (the game scrolls the middle column), so the page in hand folds,
  flows and resizes anew each time: three readings in a row gave three shapes. The fold's written cost (DESIGN.md §4, *The
  head line and the fold*), now the owner's complaint.
- The reading invents regions, and the page never drops them: the owner's desk.json had 13 regions, 4 of them junk. The
  FACE COVER heading read as "ACE COVER" is no heading, so the slot box under it became a window ("*ACE COVER", 1 cell);
  the ARMBAND heading likewise (a window of 0 cells); the POUCH's grid at 23:08 came back as a window of no name (the
  region left out as cut, the lattice then taken for a window); a second nameless window the same way. The backpack ended
  as a lone slot (23:10) and then a 1×1 region of 0 cells (23:14, 23:15), replacing the page's 5×6 grid.
- Across the eleven GEAR and stash readings: 510 cells, 367 named (72 %), 83 "one of N" (16 %; mostly loadout slots
  without a footprint: the armband one of 11, the dog tag one of 27, the Trooper one of 2), 60 unread (12 %; labels the
  recognition drops). The junk regions are the layout's, not the recognition's.
- The 22:22 screenshot is Ragman's TASKS tab with the daily "Exit the location" (Shoreline, 23:54:15 left, "Survive on
  the location") selected: told as a trader's screen and left, by the 2026-10-19 rule ("its grids are the task's
  rewards"). The 21:22 screenshots show Skier's with a weekly ("Exit the location", Streets of Tarkov, 0/7, "6 day(s)
  23:07:57") and a daily running out ("Elimination", 00:48:48) in the OPERATIONAL TASKS list.

**Decided** (owner, 2026-10-25, on the session's decision page; the page stays outside the repository):

1. **The desk's layout: A, the GEAR tab mirrored and fixed.** The desk always has the game's shape: left the loadout in
   the game's slot grid (EARPIECE · HEADWEAR · FACE COVER / ARMBAND · BODY ARMOR · EYEWEAR / DOGTAG / ON SLING · HOLSTER /
   ON BACK · SHEATH), in the middle TACTICAL RIG · POCKETS and SPECIAL SLOTS · BACKPACK · POUCH, each container's slot
   beside its grid as the game draws them, right the STASH, ten wide. Every region stands in its place whether or not the
   last screenshot showed it: one never read is a dashed empty frame ("NOT IN A SCREENSHOT YET"); one not in the last
   screenshot keeps its last reading, drawn a step dimmer, its head saying since when ("BACKPACK · 23:08"). What a
   reading changed glows in, as now. Nothing folds and nothing re-flows. The cell size follows the desk's size alone,
   never the content: where even 40 DIP doesn't take the middle column's height, the page scrolls, as the game scrolls
   that column. Container windows stand under the stash, each with its stem to its cell; a window with no title and no
   container on the page is never drawn. This trades "the cells fill the desk" (2026-10-19) for a steady shape. Of the
   panel's other ways, B (today's page pinned: the strip and the flow kept, the set, order and size fixed) and C (the
   adaptive page kept, the reading fixed) were not taken.
2. **The trader's task view is read.** A menu screenshot told as a trader's TASKS tab whose header names a task that also
   stands in the OPERATIONAL TASKS list at the lower left (same name, a countdown beside it) is an operational task: its
   name (the header), its location (the header's right end), its status word, its objectives with their counts, and the
   countdown of the list entry of that name, read by the same vote as the table's bands; progress from the counts. Never
   the task's text (DESIGN.md §3), the rewards, the traders' row with the player's name and money; of the picture's right
   third, the header line's band and one band per objective line (their counts stand there). The one task is added or updated by name and map; the mode's other tasks stay; a
   Tasks table screenshot later replaces the set, as now. The same cue, glow and notice. Two details, both taken: a task
   screenshotted before ACCEPT counts (the status isn't "active" yet; the countdown runs either way); the other
   operational tasks in the trader's list refresh the countdowns of tasks already kept, by name, never adding one.
3. **The reading:** the fixes (no window from a misread heading: a window needs its title bar's glyphs and never stands
   where a loadout heading belongs; a heading one letter off still counts; a grid cut at the column's foot keeps the
   page's version instead of becoming a nameless window; a lone slot keeps the page's grid; a window attached to nothing
   that three readings in a row don't show is dropped, and one with no cells and no title is never kept); the 13
   screenshots as a measured, git-ignored test set with truths, so every change reports named / one of N / unread before
   and after; a slot's pick kept while the picked item stays among the slot's candidates ("until its label changes").
   Not now: the quest marks' stash reader through the desk's lattice (the open point of 2026-10-19); another pass at the
   unread labels.
4. **Go now** ("We still have a lot of quota and we can spend it, go for it.").
5. **The second test, the same evening** (owner, 21:00, on the dev build with step 1: "It's still quite buggy in the
   latest build"; the four GEAR screenshots of 20:46 to 20:50 measured: a window swallowing the grid under its edge, a
   tooltip over a window, a rounds count as a window title, a grid under a window "not in view", lookalike digits in a
   label, cut labels, and 83 of 366 cells "one of N" by a shared short name and footprint): step 1b (the faults), and
   **a cell's picture against its candidates' icons** to settle "one of N" (step 1c; the coordinator's proposal, the
   owner: "We can use all of the remaining quota we have, go ham"). **A readings log** (owner: "Can't you write out logs
   of what was detected and link it to the screenshot so we can troubleshoot it afterwards?"): one text file per menu
   screenshot read, named after it, in the data folder's `readings`, the last 50 kept, never in a report. The desk's UI
   QA pass of step 2 (FAIL, 0 blocks, 5 should, 1 nit) is fixed in step 2b.

**The plan:** step 1 (the reading's fixes, the test set, the slot's pick) and step 3 (the trader's task view) in parallel
in worktrees of their own (separate files), step 2 (the layout, A) after step 1 (it needs a region's reading time in the
model); each merged into main and into the dev build before the next; the senior translator for the texts of steps 2 and
3; the UI QA pass after step 2 and for step 3's settings note; DESIGN.md updated in each step.

**Built (step 1).** (2026-10-25; binding: DESIGN.md §2, "For the stash desk", *The layout round*; §4, *What it
mirrors* ("A region read in part keeps the page's rest", "What leaves the page"), *The cell*, *The click*; §8, "Menu
screenshots", "The desk file".)

- The reading (`DeskLayout`, `DeskFrames`, `DeskReader`): a heading one letter off counts (`DeskLayout.HeadingWord`:
  "*ACE COVER", "ÄRMBANI"); a word level with the loadout's headings is a title only after the magnifier, and left of the
  stash pane a title in capitals needs its bar's glyphs, or a two-pixel lattice that is no region's grid; a lattice under
  a middle heading is that region's grid; a compartment framed down to the column's foot is read as its rows in view
  (`DeskFrames.OpenAt`) and the region said read in part (`DeskRegion.Partial`); a frame whose heading a window may hide
  is placed under none; a container's slot is boxed from its heading where no frame shows it; compartments within a
  quarter of a cell stand on one row; the QUICK USE bar's word is found at last (it folds to "0u1ck"). In a raid the glance
  reads as before: the 22 raid screenshots read the same regions and cells.
- The page (`DeskLedger`): a region read in part keeps the page's cells and compartments outside those read, its size
  and its slot, unless the slot reads another container; a window attached to nothing leaves after three readings that
  didn't show it (`missed` in desk.json), one with no cell and no title is never kept; each region keeps its `readAt` (for
  step 2's "BACKPACK · 23:08"); a "?" cell's pick stays while the item is among its candidates. The app log says what was
  kept and why ("cut at the column's foot", "under a window", "not in view") and what left (`GameSession.Desk`). The
  owner's desk.json of 2026-10-19, loaded, loses "*ACE COVER", "ÄRMBANI" and the two windows of no name and keeps the Keys
  and Junk windows.
- The test set: the 13 screenshots, and the ARMBAND case of 20:25 found among the evening's others, as
  `00-gear-armband.png` … `13-gear-keys.png` in the git-ignored `tests\fixtures\ocr\desk\session` (the main checkout's
  too), with `regions-<picture>.txt` for each and `truth-<picture>.txt`, by the page's regions, for 23:08, 23:14 and 23:15
  (158 items); `DeskRealPictureTests` holds the reading to them, `shturmap-cli desk` scores both (and prints the layout as
  found with `--why`). Drawn tests for each fault, each failing on the reading of before.
- Measured (`shturmap-cli desk`, before → after): regions of their size 50 → 64 of 66, missing 7 → 0, invented 5 → 0, of
  another size 9 → 2; the three truths' items 123 → 127 right, 17 → 18 one of N, 8 → 10 unread, no cell read 10 → 3, 0
  wrong. Cells named / one of N / unread per picture: 20:46 (0) 41/9/7 → 44/9/9 (the backpack's rows in view now read),
  20:46 (1) 39/8/9 → 39/9/9 (the POUCH, no window), 21:28 11/6/4 → 15/6/5, 22:25 17/4/6 → 20/4/6, 22:27 14/4/6 →
  17/4/6, 23:08 23/4/5 → 23/5/5, 23:09 29/5/6 → 29/6/6; 22:24, 23:10, 23:14 and 23:15 unchanged (67/20/1, 22/10/5,
  13/5/7, 91/8/4), 22:22 and 23:07 none; 20:25 86/6/7 → 85/6/7 (a pocket under the Keys window no longer the rig's). The
  page in hand after 23:07 to 23:15: loadout, rig 5×4, pockets 4×2, special 3×2, backpack 5×6 (emptied after the raid:
  rows 0 to 3 read empty at 23:10 and 23:14, rows 4 and 5 as at 23:09), pouch 3×3, stash 10×13 and the Keys window 11×7,
  no other (before: a backpack of no cell, no pouch, "*ACE COVER" and a window of no name). The GEAR truth and the Keys
  page's truth unchanged (30 and 61 right, 0 wrong); about 1.3 s a picture, as before.
- Not as the decision wrote it: a title in small letters still counts by its size left of the stash pane (the decision
  asked for the glyphs there), else the Zhuk window over the middle column at 13:49 is lost; a region cut at the foot
  takes its rows in view and keeps only the page's rows below them (the decision said "keeps the page's version"), else
  the pouch and the backpack, whole or emptied at the foot after a raid, would never be read anew. The owner's to
  overrule.
- Open: the stash's rows under a page of large boxes (20:46 (1): 10 of 13 found) and a highlighted pocket's frame (21:28),
  the two regions of another size; the 23:14 stash read too blank, the page's kept (its pistol split in two, and its label
  a preset's name, "USP .45 Expert", which the catalogue leaves out); the face cover's label read "Mome)" at 23:14; an
  item whose corner lies under a window isn't read (two Junk boxes, the backpack's slot with its label showing, 23:15); in
  a raid the player's regions the foot cuts are still left out (they could be read in part with a "+", as the LOOT
  pane's are: the glance's to decide).

**Built (step 3, 2026-10-25: the trader's task view).** Binding: DESIGN.md §2 (the OPERATIONAL paragraph: which
pictures, how much, what is kept, the player's say), §1, §3 (the fixtures), §4 (principle 7, principle 11's DAILIES AND
WEEKLIES, *Operational tasks*: "As of" and the cue), §8 ("The operational tasks file", "Menu screenshots", the
diagnostics) and §9.

- The reading (`TaskTable.Trader`, `TaskTable.TakeHeader`, `TasksReader.ReadTraderAsync`): on a trader's tab, the
  entries under the list's OPERATIONAL TASKS (a name at the list's left edge, its glyph aside, down to the next heading
  in capitals), each with a countdown band at the list's right end (the edge from COMPLETE | REPLACE, 749 px at 1440p;
  the band 14 units wide, so the game's version line under the list stays out of it); the header's task (right of its
  glyph on REPLACE's line) when an entry has its name, else none (a story quest's tab); for that task alone the header
  line's band from 0.665 of the width to the picture's edge, read once at its size: the location, then the status word
  apart from it (a lone run is the location unless it reads "active"). Two entries of the header's name: the task
  without a countdown.
- **The objectives** (the decision's "its objectives with their counts"; `TaskTable.ObjectiveBands`, `ObjectiveOf`,
  `CountOf`): they stand centred across the screen in a bar under "Objective(s)" (776 to 2522 px at 1440p), a line's
  words over the left part's edge (the daily's "Survive on the location" from 1542 to 1820), every count in the right
  third (1938 to 1980). Each line the left part's words show under the label and above "Rewards" (or the menu bar at the
  foot where no rewards show) is one band from the label's left edge to the picture's right edge, the line's height
  with half a unit above and below, never above the label nor into the rewards; its words from a plain look at the
  band, up to the progress bar's gap (a plain look reads the weekly's "Ø/7" as "øn"), without the line's icon or TURN
  IN; its count at its end by the 14-way vote. Read only for a task the header names; the objectives read replace the
  task's, as a table's expanded row's do, and the progress comes from their counts.
- A weekly's countdown is "6 day(s) 23:07:57" in the table too: the table's "hours as written" was an assumption
  (UPDATES.md). `TaskTable.TimeLeft` reads days and a time of day under 24 h; and as the recognition read "6 day(s)" and
  dropped the time after it in 13 of 14 ways on the selected list entry, `TasksReader.VoteCountdownAsync` votes on the
  time again in a band of its own right of "day(s)" when most ways read only days (then 10 of 14), the days most ways
  read in front; a value whose days they contradict (one way's "S day(s) 2307:57", five days) is not taken. The
  table's bands go the same way.
- The set (`OperationalLedger.ApplyTrader`, beside `ApplyTable`): the task added, or the kept one of its name and map
  updated (another map: another task; two on one map: the one whose expiry agrees, else neither); its countdown checked
  with the list's other entries as one screenshot's rows, then against the kept expiry as a table's row; its
  objectives replace the kept ones when read, the progress from their counts (none read: the kept ones and their
  progress stay); its `readAt` the screenshot's. The list's other entries refresh
  the one task kept by their name, whatever its map (two kept, or a name twice in the list: neither), through the same
  checks, never adding one. **The set's `readAt` stays the last table's** (null while only trader's tabs brought
  tasks; `operational.json` writes none then, and older files read as before): a trader's screenshot older than it is
  left, a merge never holds a later table back (the table replaces the set, the merged task with it), and "As of" says
  nothing until a table is read, each row its own time left.
- The session (`GameSession.TakeTrader`): the location to a map as a table's row (none read, or no known map: left
  out, counted); the same cue through `OperationalReadings` for a task the set didn't have, none for an update or a
  refresh alone; the app log "Operational task read from a trader's screen: 1 new, 0 countdowns refreshed, 0 on no
  known map, 0 countdowns not taken (… ms)", the study log `operational.trader` with counts, never names.
- `shturmap-cli tasks` prints a trader's tab (the task, its map as read, status, its countdown with its votes and
  expiry, its objectives with their counts, each other entry with its countdown and expiry) and scores it against
  truths, whose format takes `task` and `entry` lines and the task's `objective` lines; `--raw` prints every way's text
  beside the votes.
- Texts (English and German, for the senior translator): the values of AppTexts `SettingsReadStashNote`, ViewTexts
  `HelpPrivacy` and `OperationalGlyphTip` ("a trader's TASKS tab", „TASKS-Tab eines Traders“, never their text or
  rewards); the English comments of ViewTexts `SettingsReadStash`, RuleTexts `OperationalAsOf`, `OperationalCueEyebrow`,
  `OperationalCueTitle`, SessionTexts `OperationalRead`, AppTexts `ScavLootOperational`; the glossary's proposed term.
  README, PRIVACY.md, UPDATES.md (the table's row, a row for a trader's tab, the location row, a ⚠ row for a task before
  ACCEPT, the log). The tour's chapter 1 names no Tasks screen ("Your stash, gear and tasks in a menu screenshot"):
  unchanged.
- Measured (`shturmap-cli tasks`; the owner's five screenshots of that evening, renamed, in the ignored
  `tests\fixtures\ocr\operational\trader` with hand-written truths, and the two trader's tabs of that morning in
  `operational`, whose truths now name their task):
  - Ragman's daily: "Exit the location", Shoreline, active, 23:54:15 from the page's words; its objective "Survive on
    the location", no count (none read in any of the 14 ways, rightly: it has none).
  - Skier's weekly: Streets of Tarkov, active, "6 day(s) 23:07:57" by 10 of 14 ways with the days apart; its expiry
    agrees with the three tables' within a second. Its objective "Survive on the location 0/7": the words right, the
    count 0/7 by 1 of 14 ways (binary at 1x; seven ways read "øn", six nothing after the words): read, on one vote. The
    list's other daily, "Elimination" (orange "00:48:48"), read in no way, nor in a band of the time alone.
  - The other three of that minute aren't trader's tabs but the OPERATIONAL table, 18 to 24 s later, a row expanded in
    each: names, locations and statuses 9 of 9, countdowns 8 of 9 (the weekly's days 3 of 3).
  - The morning's: Therapist's "Exit the location", Shoreline, 08:33:03 by 4 votes, "Survive on the location 4/7" (the
    count by 13 of 14); Peacekeeper's "Find and transfer", Any location, 10:13:48 by 3 under the hand-over's icon, its
    two hand-overs ("SSD drive", and "CPU fan" with TURN IN after it, left out) without counts. So "Any location" is
    seen in a trader's header.
  - Objectives on the four tabs: words 5 of 5, counts 2 of 2, none wrong. A trader's tab reads in 350 to 730 ms with its
    objective bands (150 to 260 without).
- Tests: `TaskTableTests` (11 more: the header's task with its countdown, a band per objective line between the label
  and the rewards, never into them, the menu bar where no rewards show, an objective line's words and voted count,
  the count at a band's end, the entries' bands clear of the version line, a story quest's tab, two entries of one name,
  the header band's words, the days' countdowns and the days alone, and the real trader's screenshots; the real-picture
  test takes the trader's truths, objectives included), `OperationalTasksTests` (8 more: added,
  updated by name and map, objectives and progress, the checks, older than the table and a later table, a set a
  trader's tab began and its file, the list's refreshes, a name kept twice or listed twice), `OperationalSessionTests`
  (4 more: onto its map with the cue and the file, a refresh alone calls no cue and a story quest's tab adds nothing, no
  known map, unticked).

**Open (step 3).**

- The weekly's count 0/7 on its trader's tab reads on one vote of 14 (seven ways read "øn"): right here, thin.
- A list entry's orange countdown (not the selected one's) reads in no way: a refresh works where it reads (the
  selected entries' white ones did, 4 of 4).
- Not seen: a task before ACCEPT on a trader's tab (UPDATES.md's ⚠ row), two entries of one name in a list, the German
  game, other resolutions.
- Done the same evening: the senior translator's review (one fix, a task's text as „Beschreibung“, 1ae2b36) and the UI QA
  pass on the settings note, help's privacy text and the glyph's tip (on e3b0ebc, en and de at 900×560 and 1600×900, the
  layout check of Plan with and without operational tasks: PASS, no findings; the tip judged by its texts, 105 and 98
  characters, within the other tips' range).
- CLAUDE.md's "five things" a picture is read for doesn't name this sixth reading yet.

**Built (step 3, the table's two misreads, 2026-10-25).** Open in step 3: the table's own faults these screenshots
showed, the weekly's "Ø/7" read as "øn" (an objective "Survive on the location 0n" without its count), a "0/3" unread,
every lone "0%" unread. Binding: DESIGN.md §2, the OPERATIONAL paragraph's "How much of it".

- The objective counts (`TaskTable.Read`, `TaskTable.Objective`, `TasksReader.VoteObjectiveCountsAsync`): a misread
  count is no word of the objective (the line's last word, five characters at most, starting with the slashed zero or
  a 0, or with another digit after the progress bar's gap of more than four units; a distance's "5øm" a word's gap
  after "than" stays); each objective line of an expanded row has a band (`TaskRow.ObjectiveBands`) from its first
  word, its icon left out, to the left part's edge, the line's height with half a unit above and below, and where the
  page's words gave no count its count is voted on at the band's end in the 14 ways (`CountOf`, as on a trader's tab).
  Measured (`shturmap-cli tasks --raw`): the "Ø/7" by binary at 1x and 2x (2 of 14; six ways read "øn", six nothing);
  the dropped "Ø/3" by binary at 1x, 1.5x, 2x and 4x, inverted at 1x, 1.5x and 2x and stretched (8 of 14); the six
  hand-over lines read no count in any way. The band from the "Objective(s)" label's left edge, as a trader's tab has
  it, read the "Ø/7" in no way and the "Ø/3" in 6; with the line's icon in it, the "Ø/7" in at most one.
- The progress (`TaskTable.Percent`): the percent sign reads as "0/0" (its rings and bar), so the game's "0%" reads
  "00/0" or "ØO/O" and its "57%" "570/0"; the number takes all its digits before the sign ("500/0" is 50, never 0).
  Of the 23 bands of a 0 %: "00/0" 40 times, "0%" 5, "ØO/O" 4, in binary and inverted at 0.75x, stretched and inverted
  at 1x and inverted at 1.5x.
- Measured over `tests\fixtures\ocr\operational` and its `trader` folder (14 truths), before → after: objectives' words
  16 → 17 of 17 (the "0n" gone), counts 15 → 17 of 17 (the evening's weekly 0/7 and daily 0/3), progresses 12 → 30
  of 30 (every 0 %: 1 to 4 votes each; the 57 % now by 7 to 9 instead of 1 or 2), none wrong; the rest unchanged
  (countdowns 29 of 30 on the tables, the trader's tabs as before). A table whose expanded row shows two hand-overs
  reads in 1.15 to 1.3 s (before 0.75 to 0.95 s): their lines' bands are read too, finding nothing; one with a count
  the page dropped 0.95 to 1.05 s (before 0.9 to 1.0), the others as before (0.6 to 0.8 s).
- Tests: `TaskTableTests` (a misread count no word of the objective and a band per line, the misread counts as the
  recognition gives them, the progress through its sign's "0/0" and its digits kept; the real-picture tests hold the
  progresses to nine in ten and the trader folder's tables to their objectives, counts and progresses).
- Open: the table's "Ø/7" reads on 2 votes of 14, as thin as the trader's tab's (1 of 14; its band starts at the label's
  left edge, where the table's read in none: a band from the line's first word there is untried); a single way's
  misread now votes too ("30/0" as 3, "510/0" as 51, each outvoted by 57's 7 to 9 here; where the right value has one
  vote, a tie goes to the earlier way). The rail now shows "0 %" for a task at 0 % where it showed nothing, and the
  objective without "0n": the UI QA pass's to look at.

**Built (step 2, 2026-10-25: the layout, A).** Binding: DESIGN.md §4, *The stash desk* (the layout round's paragraph,
*Its place*, *What it mirrors* with LOADOUT, *A region never read* and *A region not in the last screenshot*, *The head
line*, *Fun*, *As built*: *The page*, *The cell*, *A window's stem and light*), "Design system" (*Motion*), "Visual
language" (*The stash desk's marks*, a region's place), *Text size*, §8 (`-Desk`, `-DeskReading`, the script's `desk`).

- The page (`Rules.DeskPageRules`: `Blocks`, `Layout`, `Arrange`, `LoadoutBoxes`, `SlotHead`, `Kept`, `HeadOf`,
  `Entering`; `Controls.DeskBoard`, `Controls.DeskCellView`): the seven fixed regions always in the game's places, LOADOUT
  in the first column, TACTICAL RIG, POCKETS beside SPECIAL SLOTS, BACKPACK and POUCH in the second (a container's slot
  box two cells square left of its grid), STASH ten cells wide in the third, the windows in a fourth where the desk's
  width allows, else under the stash. LOADOUT is the game's slot grid: boxes two cells square in three columns a cell
  apart, the armband and the dog tag two short boxes in one box's room, the weapons five cells wide, each under its
  slot's name (the card's `DeskSlot*` words, fitted to the box: 9 px at 40, the German FACE COVER 8.5 px without its
  spacing), drawn as cells at the box's size with the item's grid image at the box's top; empty slots empty boxes. A
  region no reading showed is its head over a dashed frame with "NOT IN A SCREENSHOT YET"; one older than the page is
  drawn at 70 % with its head dated ("BACKPACK · 23:08", "· 1 JAN 14:02" on another day, a no-break space after the
  "·"). What a reading changed comes in as before; the head line and its count-up as before.
- The cell size by the desk alone: the largest at which the three columns fit the width and the stash's column (with
  the windows under it) the height; else 40, scrolling; one column at 40 where three don't fit. Measured
  (`DeskPageRulesTests`, the snapshots): the owner's page of 2026-10-19 at 56 on a 2560 monitor's desk (2176 × 1330 DIP)
  with the Keys window in a fourth column, at 40 at 1600×900 with it under the stash; the made-up desk at 64 on the
  2560 monitor's desk (four columns, unscrolled; 72 on a desk of 2544 × 1401), at 40 at 1600×900 in three columns (its
  two windows under the stash take that column past the desk, so the page scrolls; LOADOUT, 426 DIP, and the middle
  column, 552, fit the 760), at 40 in one column at 900×560 (LOADOUT fills the first screen). Its three readings take one
  shape at every desk; a page of its cells taken out too (one size and one set of columns, not one place for every row:
  a head its date made wrap moved what was under it, until the UI QA pass, F1, below).
- The fold is gone: `Rules.DeskFold` and its tests, the board's `Unfolded`, the fold line and its text `DeskUnchanged`,
  the developer script's `desk open <region>` (now a script error). The replay's `fold` and `unfold` stills became
  `kept`; its `kept` after the raid is `back`. The stems: a window two or more columns right of its cell (the backpack's
  Zhuk in the windows' column) is reached over the page's 12 DIP lane over the columns; a stem leaving a container's grid
  level with its slot box goes round the box.
- The made-up desk (`Rules.DeskExample`): no POUCH, so the placeholder is on every snapshot (the in-raid glance's made-up
  readings keep it, `DeskExample.Pouch`); reading 2, seven minutes after the first, shows the backpack alone, so the rest
  is dimmed and dated; reading 3, two minutes later, the same.
- Texts (English and German, for the senior translator): RuleTexts `DeskNotRead` (new: "NOT IN A SCREENSHOT YET",
  „NOCH IN KEINEM SCREENSHOT“), `DeskHeadSince` (new: "{head} · {time}", a no-break space after the "·"), the English
  comments of `DeskSlotEarpiece` … `DeskSlotSheath` (now also the names over LOADOUT's boxes, their room); `DeskUnchanged`
  removed. The tour's chapter 2 frames the switch, not the page: unchanged; What's New's desk line names no fold; the
  0.7.0 notes' desk paragraph rewritten for the fixed shape.
- Not as the decision wrote it: POCKETS' and SPECIAL SLOTS' placeholders four and three cells wide, not five (five each
  made their row the middle column's widest, so a page of the stash alone stood in one column at 1600×900); LOADOUT
  eight cells wide, not "seven and gaps" (the boxes a cell apart, so a weapon's five cells span two columns and the gap,
  as in the game); the dog tag's box under the armband's in the body armour's band, as the game has it, not a row of its
  own; a never-read LOADOUT draws its boxes dashed under the words. The owner's to overrule.
- Checked: the snapshots of the three readings at 900×560, 1600×900 and 2560×1400 (English; German at 1600×900); the
  replay (`tools\qa\desk.devscript`, its stills `kept` and `same` for the fold's, `back` for the old `kept`) at the
  work area's size, 1600×900, 900×560 and in German; the layout check of the desk view in en-US, de-DE and qps-ploc at
  900×560 and 1600×900: no problem found (its first screen only: F4, below). Tests: `DeskPageRulesTests` rewritten
  (the size and the columns at the four desks, one shape over the three readings, every region in its place, a
  placeholder and a kept region counted, the windows' column, the slot grid, a slot's name fitted, the dated heads,
  what comes in), `DeskExampleTests`, `DeskSwitchTests`, `DevToolsTests`; `DeskFoldTests` gone.
- Open: at 1600×900 the made-up desk scrolls by its windows under the stash (the decision's rule; a real page without
  windows fits); at 900×560 LOADOUT takes the first screen and the stash is two screens down; a weapon's grid image
  leaves the lower half of its box empty; the replay's `entry` still shows every cell already in while the totals
  count up (not compared with the build before); the UI QA pass and the senior translator's review.

**Built (the readings log, 2026-10-25).** The owner, after testing the dev build: "Can't you write out logs of what was
detected and link it to the screenshot so we can troubleshoot it afterwards?"; the coordinator's answer: a readings log,
one text file per menu screenshot read, named after the screenshot, in the data folder. Binding: DESIGN.md §8, "Data
folders" (*The readings log*) and "Developer aids" (*Readings log*), §2's three "What is kept".

- The writer (`Shturmap.Game.Screenshots.ReadingLog`, `ReadingLogEntry`, `OperationalTaken`): `readings\<screenshot's
  file name>.txt`, plain lines in the order the reading went (the head; the Tasks screen with its rows or a trader's
  header, entries and objectives, each band's votes, what the set did and the set after it; What You Have's items; the
  desk's screen, layout in pixels, each cell's place, label as read, fit and match, and what the page in hand did),
  written as a temporary file and one move, the last 50 kept by their time written. A picture that couldn't be opened, or
  that showed none of the screens, gets a short file.
- What reaches it: the menus' reader hands what it saw on the way (`MenuReading.Seen`, `MenuSeen`: the picture's size,
  the Tasks screen told, the bands' votes from `TasksReader.ReadPageAsync`'s note, the desk's look); for that
  `MenuScreenshotReader` calls `DeskReader.LookAsync` and makes the `DeskReading` from it as `DeskReader.ReadAsync` does
  (the readers' logic unchanged). The session (`GameSession.TakeMenu`, `ReadingLogOf`, `WriteReadingLog`) adds the head
  (the build kind from the app, `GameSession.BuildKind`) and what it did (`TakeDesk`'s `DeskChange`, `TakeTasks`' and
  `TakeTrader`'s `OperationalTaken`, the marks changed), and writes the file after the gate. A file that can't be written
  is a WARN line.
- Kept out: pixels; the quoted words of the layout's notes other than the game's headings and the page's window titles
  (a word refused as a title is written "a word of 8 characters"), so nothing of the player's name or money could slip in
  by a note; the screenshot's folder (its file name only). Never in a report or the diagnostics.
- Texts (English and German in the same commit, for the senior translator): AppTexts `SettingsReadStashNote` (", a log
  of the last 50 screenshots read" in its parenthesis; „, ein Log der letzten 50 gelesenen Screenshots“; its comment
  says what the log is) and ViewTexts `HelpPrivacy` ("which items, how many and where are kept, with a log of the last
  50 screenshots read" for "only which items, how many and where are kept"; „gespeichert wird, welche Items, wie viele
  und wo, dazu ein Log der letzten 50 gelesenen Screenshots“); PRIVACY.md's "What stays on your PC" and the README's
  "It never" list name the folder.
- Tests: `ReadingLogTests` (a drawn GEAR reading cell by cell, the layout's lines, a refused word said by its length;
  a table's rows, bands and set; a trader's tab; a screenshot that gave nothing; the 50 kept and one read again written
  over; nothing the log is given can hold a picture), `ReadingLogSessionTests` (a desk reading's file named after its
  screenshot, which deleting the screenshot leaves, nothing of it in the diagnostics; a table's rows and set; the tick
  off: no file), `SafetyTests.Nothing_that_makes_a_report_or_the_diagnostics_reads_the_readings_log`.
- Tried on four of the owner's pictures (outside the session, the readers as the app has them; the layout round's test
  set and the trader folder): two GEAR tabs with the Keys window, 98 and 103 cells, 20 and 22 KB (the headings' note
  shows ARMBAND read "ÄRMBANI" under the window's edge); a trader's tab with its daily, 1 KB; an OPERATIONAL table of
  three rows, 1.3 KB. Fifty GEAR pages are about a megabyte.
- Not as the ask wrote it: the log is in every build, the release's too (the ask's data folder, and the player's texts
  say it); the study log stays the developer builds' alone. The owner's to overrule.
- Open: `shturmap-cli desk --why` and `tasks --bands` print the same facts in their own words, not the writer's lines
  (a later change); What You Have's reading lists the items it matched, not the labels it read (the stash reader's
  looks don't leave it); raid screenshots (the extract list, the in-raid glance) have no readings log; if the desk's
  `ReadAsync` changes, `MenuScreenshotReader` makes its reading the same way and must follow; the UI QA pass on the
  settings note and help's privacy text (en and de, 900×560 and 1600×900) and the senior translator's review.

**UI QA pass of step 2 (2026-10-25, on 121e0f2) and its fixes.** The pass (its pictures outside the repository): FAIL, 0
blocks, 5 should, 1 nit. Binding: DESIGN.md §4, *The stash desk* (*Its place*: the scroll mark; *What it mirrors*:
LOADOUT's names, *A region not in the last screenshot*: the heads' room; *As built*, *The page*), *Text size*, "Design
system" (*Motion*), §8 (`-Desk`: the layout check's screens); docs/QA.md; `tools\qa\desk.md`.

- F1 (should), a dated head that wraps moved the rows under it: after reading 2 at 40 DIP SPECIAL SLOTS' head, "SPECIAL
  SLOTS · 1 JAN 14:02" in UTC, wrapped to a second line and everything under the POCKETS and SPECIAL SLOTS row stood
  13 px lower (POCKETS' grid at 227, then 240; BACKPACK's head 293, then 306; POUCH 534, then 547); at 125 % the
  German TASCHEN's too. Fixed: every head takes the lines of its name with the longest date the language writes ("28 SEP
  23:58", „28. SEPT., 23:58“) from the first reading, whether it is dated or not (`DeskPageRules.HeadRoomLines`,
  `Reserved`, `WidestSince`); the layout counts them and no longer reads the dates, so a date can't change the shape
  either; the page holds them with an unseen copy of that dated head under each head (`DeskBoard.HeadCell`: transparent,
  no pointer, nothing for a screen reader, passed over by the layout check). On the first reading POCKETS and SPECIAL
  SLOTS stand over a line left empty for their date: the rows under them where 121e0f2's reading 2 had them. Measured
  on the snapshots by the rows where the brightness steps (the dimming leaves them in place): readings 1 and 2 in
  English and German at 900×560 (each of its five screens) and 1600×900, in German at 1600×900 at 125 %, and the
  replay's `first` and `kept` at 1600×900 and the work area's size: no row moved; 121e0f2's middle column 13 px.
  `DeskPageRulesTests`: SPECIAL SLOTS' room two lines at 40 where its name takes one, every fixed region's room holding
  every month's date at a day of one and of two figures at every cell size and at 100, 125 and 150 % in English and
  German, the columns as tall at reading 2 as at reading 1 at the four desks and three text sizes.
- F3 (should), nothing said the page scrolls (at 900×560 the first screen LOADOUT alone, its last boxes flush with the
  window's foot; at 1600×900 the windows past it). Fixed by the coordinator's rule, the owner's to overrule: where the
  page runs past the desk's foot, "MORE BELOW ▾" at the foot, over the page, muted in the eyebrows' style at the page's
  left margin, on the desk's ground with a 16 DIP fade of the page under it, gone once the page is scrolled to its end
  (`DeskPageRules.MoreBelow`, `DeskBoard.MoreBelowMark`); no pointer, no motion; PgUp and PgDn as before. In the replay
  at 900×560 and 1600×900 it stands in `first` and is gone in the new still `end` (the WZ window's cell, the page's
  last, pointed at); at 2560×1440 the page fits and it doesn't stand. `DeskPageRulesTests`: the mark's rule, and the
  made-up desk's page at the three desks.
- F4 (should), the layout check didn't look below the first screen (at 900×560 PASSED with 95 texts not in view, 68 in
  qps-ploc). Fixed: in a developer build's `--snapshot` with the desk up, where the page runs past the desk, the page is
  scrolled down by the desk's room less the scroll mark's band at a time (`DeskPageRules.ScreenOffsets`), so what the
  mark covers at one screen's foot stands in the next, and the window pictured and checked at each,
  `screen-2\window.png` and on, until the page's end shows (`MainWindow.LayoutCheck.DeskScreensAsync`);
  `tools\layout-check.ps1` takes each as a view of its own, `desk-2`, `desk-3` …, and its summary says "desk in 5
  screens"; the scroll mark is a layer to the check (`LayoutCheck.Layers`). Run for the desk in en-US, de-DE and
  qps-ploc at 900×560 (five screens) and 1600×900 (two): passed, no problem in any screen; every text below the first
  screen is in view in some screen (by its words; the figures that stand several times by the screens' steps). The
  rail, help and settings, whose lists scroll too, are not taken in screens (docs/QA.md says so): their texts below
  the fold are checked for overflow and translation, not for text over text, and are in no picture. Open.
- F5 (nit), the smaller slot name's baseline: "GESICHTSBEDECKUNG" a pixel lower than its row at 100 %, two higher at
  125 %. Fixed: a row's names stand on one baseline, each name's top moved by its baseline as drawn
  (`DeskBoard.OnOneBaseline`); the pixel at 100 % was WinUI's: a text of no letter spacing is laid out with its baseline
  a pixel lower (its `BaselineOffset` saying 0.2 DIP higher), so a name without its spacing keeps a thousandth of an
  em. The fit is now measured as drawn and in 64ths of a pixel as written (`DeskPageRules.SlotHead`'s `wide`,
  `DeskBoard.NameWidth`), so the name stands at its row's 9 px without the spacing at 100 % (8.5 before) and 9.4 px
  at 125 % (8.8; its row 12). Measured by the ink: at 100 % and 125 % the row's names end on one pixel row (121e0f2: one
  lower; three higher). At 150 % WinUI's curve draws nothing written at 4 px or more under 11 px, so the name stands
  about 14 DIP past its box, on the row's baseline, short of TACTICAL RIG (DESIGN.md *Text size*; open, the owner's
  call).
- F2 (should, not this change's): the replay's `entry` shows every cell whole 0.4–1.1 s after `desk 1` while the totals
  count up. Unchanged, and found why: a still (`RenderTargetBitmap`, `--snapshot` and the developer script's
  `snapshot`) draws a storyboard's opacity whole, the compositor running it where that render doesn't look. Measured with
  a probe build (not committed): the window's thread read the 26th of 52 cells at 0 until 1.4 s and the 52nd until
  2.4 s, each then easing to 1, while the still taken in the first second showed both whole, the head line mid-count. So
  the cells' entry is unchecked headless and awaits the owner's eye on the dev build (DESIGN.md *Motion*, docs/QA.md,
  `desk.md`'s `entry` say so). A headless check would need the entry driven from the window's thread in developer runs
  (a timer setting each cell's opacity) or a capture of Shturmap's own window: the owner's call. Open.
- F6 (not run): the replay run on the Debug build of this branch at the work area's size (2544×1401 inside), 1600×900,
  2560×1440 and 900×560, English: 21 stills each, no WARN or ERROR; `entry`, `first`, `end`, `window`, `kept` and the
  rail's stills looked at, as `desk.md` says (the stems, the linked head's light as wide as its words, the dated heads,
  the scroll mark). Nothing new found.
- Texts (English and German, for the senior translator): RuleTexts `DeskMoreBelow` (new: "MORE BELOW ▾", „MEHR
  DARUNTER ▾“, the glance's `GlanceMoreBelow` words with an arrow).
- Noticed, not this pass's: at 150 % text in German at 1600×900 the layout check finds a cell's figure over its count in
  three 1×1 cells at 40 DIP ("27k" over "60", "5k" over "4/4", "54k" over "120"; *Text size*'s open point on a cell's
  price at 150 %); docs/LANGUAGES.md's "Layout check" lists no `desk` view and no screens.
- Open: F2 (the owner's eye on the entry); F4's other scrolling views; F5 at 150 %; the review of `DeskMoreBelow`; the
  owner's say on the scroll mark (the coordinator's rule).

## 2026-10-19: text size: Windows' own, honoured everywhere

**The ask** (beta feedback, relayed by the owner 2026-10-18): "on some setups the text can be hard to read. We should
support different text-rendering sizes, which should probably translate into an overall UI scale setting. Explore the
feasibility of this, we don't want to create a huge overhead because of this while still providing this feature".

**Found** (an exploration on 2026-10-19, its notes and pictures kept outside the repository; the window pictured at
Windows' text sizes with the developer aid `--text-scale`):

- WinUI already draws every XAML text of Shturmap at Windows' text size (Settings › Accessibility › Text size); the map
  (Skia, a swap chain) stayed at 100 %.
- 125 %: one extract-list line cut without "…"; in German NEXT took four lines and pushed EXIT below the 900×560 fold.
- 150 %: NEXT a word or two a line; the closed MAP list "Streets of Ta…"; the extract list's kind lines cut; help's
  96 DIP quest types cut "Elimination"; settings' reports row out of the panel ("NIE" by 40 DIP).
- 200 %: Plan's rows squeezed, the raid state cut without "…", the roll call's lines overlapping: layout work of its own.
- Routes by overhead: 1, Windows' own (the map and these fixes); 2, a setting of the app's own (some 180 sizes); 3, a
  whole UI scale (no LayoutTransform in WinUI; popups, map input). Windows' display scale of the second monitor works
  today.

**Decided** (owner, 2026-10-19): route 1, plus an option of the app's own, three steps ("The text-size should still be
set by an option, not only through the windows settings"). The map follows the text size; the smallest window grows
with it, so the raid card's EXIT stays in view; what clips at 125 and 150 % takes the larger text (wraps, grows, trims
with "…"), never smaller text; 200 % is accepted as degraded. Questions for the tester: (1) the monitor Shturmap runs on,
its size, resolution and Windows' display scale, the game's or a second one; (2) Windows' text size, left at 100 % or
raised, and did Shturmap clip; (3) Shturmap's window, maximised, half the screen or the smallest, and how far away;
(4) which text was hard to read: the rail, the cards, the status bar or the map's labels; (5) would a larger display
scale on that monitor do, or does it make their other windows there too large.

**Built (2026-10-19).** Binding: DESIGN.md §4, "Screen anatomy" (*The window*, *Text size*, "Status bar", the raid card's
glance, "Settings"), "Map drawing" (*The text size*), §8 "Developer aids" (*A larger text size in a run*).

- `Rules.TextSize`: settings' step (NORMAL ×1, LARGER ×1.25, LARGEST ×1.5; `ui.textSize` in shturmap.db), the whole
  factor (Windows' × the step, at most 2.25), the window's share of it, WinUI's curve. `Controls.TextScale` (the
  developer aid's pass, now in every build): reads Windows' text size and follows its change, gives every text of the
  window and its popups the step's share after a layout (at most every 100 ms, nothing at NORMAL), and the fits' probes
  too. Settings' row "Text size" (`MainWindow.TextSize`), live; its four words and tooltip in `ViewTexts`, en and de.
- The map's `uiScale` is the pixels per DIP times the whole factor (`MapView.UiScale`).
- The smallest window: 900 × (560 + 410 × (factor − 1)) DIP, 663 high at 125 %, 765 at 150 % (`WindowPlace.Smallest`),
  never past the work area; a smaller window grows to it. Measured in German, the fake raid: EXIT's foot at 497, 584
  and 702 DIP at 100, 125 and 150 %.
- What clipped: the glance's labels stand above their names where beside them a name keeps fewer than eight ems
  (`Rules.GlanceFold`, `MainWindow.FitGlance`); the extract list's names, kind lines and needs wrap; the MAP list's name
  wraps, its counts past 160 DIP; help's quest types get a measured column (`Columns.HelpKinds`); settings' rows of
  choices wrap (`WrapRow`), and its ticks' names; the map's credit keeps clear of the map buttons; the status bar's raid
  state trims with "…" as the last resort.
- Developer aids: `--text-scale <factor>` stands in for the whole factor; `--text-step <larger|largest>` takes a step
  for the run.
- Tests: `TextSizeTests` (the setting read back, the whole factor and the window's share with their bounds, WinUI's
  curve, the smallest window, the glance's fold).
- Checked on the developer build, English and German, Plan and the fake raid, at 125 and 150 % (`--text-scale` and
  `--text-step`) at 900×560 and the grown minimum (900×663, 900×765), and at 200 %: the layout check finds nothing at
  125 %, and at 150 % only the quest lines below; EXIT in view at 900×663 and 900×765 in German. In a fake raid at
  LARGEST the pass ran 115 times a minute, 0.5 s in all.

**Open.**

- The raid card's quest lines (`ObjectiveLine`, `Rules.LineRoom`): at 150 % German, "Informationen" and
  "Finanzunterlagen" break inside the word (lines of 113 and 119 DIP beside a hand-over and a direction).
- 200 %, degraded as decided: Plan's rows squeeze the map's name, MIN LEFT trims, the roll call's two-line names touch
  the next, help's keys heading and settings' LICENCES run out of their panel.
- To check: Windows' own text size on a PC (the pictures simulate it), with a step on top, and a change of either while
  Shturmap runs; the UI QA pass's re-check of the fixes below. The senior translator reviewed the five texts
  (2026-10-19): the tooltip now names Windows-Einstellungen; the rest held.
- **UI QA pass of 2026-10-19** (on 7431ddd; 125 and 150 %, English and German, Plan and the fake raid) and its fixes.
  F1, two labels end to end on one line read as one (150 % German: "Transit zu Interchange" and "Taxi in der
  Primorsky-Straße"; not the factor: at 100 % too, on Reserve, The Lab, Shoreline and Interchange): a side within 8 px
  (× the factor) of a label on its line isn't free (`MapRenderer.LabelGap`); four of 40 `frames` pictures at 100 %
  change, each such a pair parted (Shoreline's Pier Boat unlabelled at the smallest window), and the layout check's
  eight map pictures at 100 % are as before. F2, names at the right edge lost whole words at 150 % (900×765): the
  label room's cap, a fifth of the view, grows with the text size (`Camera.TextScale`), so they are cut where 100 %
  cuts them, and a wide map there is 13 % narrower. F3, Plan's NEXT RAID row wraps "STREETS OF / TARKOV": accepted.
  F4, the developer script's `textsize normal|larger|largest` pictures the setting's change while Shturmap runs.

## 2026-10-19: the stash desk, a price view from a screenshot (designed; reading and data built, the view to build)

**The ask** (owner, 2026-10-19): "If you take a screenshot from items, provides a view in the app that mirrors the grid
(to keep the mental model of the currently seen items) and augments them with the current price at the auction house
and the best price at traders. Prices should be gathered via tarkov.dev. When interacting with the item (mouse-over)
it should get details, such as recent price history and prices in all traders, associated recipies and quests. A
percentage icon indicating the development in the last x days would also be cool to judge if the price recently went
up or was volatile. Regarding the visualization we can probably learn a lot from stock visualization systems and take
the best pieces here. It would be cool if there is a easy way of defining a automatic price for the items, for example
the same price as the current auction house price or X% above or below so you can just click the icon and then paste
it into the auction house." It went against DESIGN.md §1 and §2, so before any UI it went to research (a survey of
the existing tools, and the data facts: tarkov.dev's API, the app's cache, the stash reader) and a discussion page
with five questions.

**What was found** (2026-10-19):

- **Nobody ships it.** Of sixteen tools surveyed none has more than three of the vision's six parts; only a Discord bot
  takes a picture of the inventory as its input (to a text list), the desktop tools capture the screen, and none
  copies a list price.
- **The nearest, Tarkov Stash Helper** (first release July 2026, no licence), reads the whole stash and says sell to
  a trader, list on the flea or keep, with a list price it computes; it captures the screen continuously, registers a
  global hotkey, and has no price history, trend, hover detail or clipboard price.
- **The prices are already on disk:** tarkov.dev's daily item file (17 MB, about 1.4 MB transferred) carries the last
  low, the 24 h average, low and high, the 48 h change, the offer count, every trader's price, `noFlea`, the item's
  flea level and the flea's fee rates; the loader keeps six fields of it. History is free on json.tarkov.dev, a host
  Shturmap already asks: `/{mode}/prices/{id}` (LEDX: 1,811 points since 2022-03 in PvP, 952 since 2024-07 in PvE,
  every 2 h over the last 30 days, daily before). The GraphQL API was down all day: the JSON API only.
- **Naming every cell is the hard part, not the numbers:** 2,289 of the catalogue's 5,480 items (41.8 %) share their
  short name with another, and of the 3,159 flea-priced items that aren't guns or presets, 672 share short name and
  footprint. Today's reader, against the quest set (60 candidates): keys 46 of 48, a medical case 43 of 43, a stash
  page 50 of 57, none wrong; against the whole catalogue it is unmeasured. 1440p only; no price knows durability or
  uses left.
- **It went against §1 head-on** ("item prices or a flea-market view" and "a stash register or inventory browser" were
  not-goals) and against §2's boundary (the grids read "never against the whole catalogue"). The recommendation was a
  separate app in the same repository, sharing the reader and the cache as libraries.

**Decided** (owner, 2026-10-19, on the page's five questions: Q1 against the recommendation, the others as
recommended, "the rest"):

- **Q1, the framing: one app, the desk a view of its own.** Against the separate app: "I would like to push-back on
  this. It makes sense to keep it in one app since shturmap idea is to be mostly as hands off as possible. So when you
  for example take a screenshot in-game from your stash or when looting something, the app could display the info and
  a brief overview without having to leave the map you, just by augmenting the info. The map could still be visible,
  the app just helps with additional information. Then, out of raid, a separate view could be feasible, one raid
  planning view and one stash desk view or how we might call it. Separate apps would mean less screenspace if you have
  them open at the same time and one feedback we already got was that readability might be an issue. I still feel we
  can leverage a lot with the tooltip-modes we already have implemented. I really like the ideas of sparklines, we
  might borrow other ideas from stock visualization (maybe also look into how Eve online does this). In the end, it's
  a app made for gamers, so it should also be fun to use."
- **Q2, the feature set: C "Desk", in two steps, B "Glance" first.** B: a screenshot to a mirrored grid; per cell the
  better of the flea's net after the fee and the best trader, and which; a value-per-slot tint; the 48 h % from the
  item file; a keep mark for quest and hideout needs; "?" cells with a range. C adds a hover card (a 7- and 30-day
  sparkline of the average and the minimum, a 24 h range bar, all traders, quests, hideout, crafts, barters, the fee
  and the net), a 7-day % badge, and one click to copy a list price. D (watchlists, alerts, stash value over time,
  automation) stays out.
- **Q3, unsure cells:** the group's price range and a "?"; the hover card lists the candidates and one click picks
  (672 flea-priced items share short name and footprint).
- **Q4, the list-price rule:** "match the last low", or "X % under/over the last low", always guarded: never below the
  best trader's net after the fee; a warning above the maximum profitable price; no flea price when the item can't be
  listed (`noFlea`, the item's flea level, the found-in-raid rule).
- **Q5, 1080p:** before the desk reaches the beta testers. This is the coordinator's assumption: the owner took "the
  rest" of the recommendations, and the page gave none for Q5.
- **The page's changes to the vision**, taken with "the rest": the % badge over 7 days with arrow and sign, never
  colour alone; a "volatile" mark when the 7-day range is large against the price (a mark, not a statistic); the
  mirror drawn from catalogue art, not the screenshot's pixels; a "~" on armour, meds and weapons (prices assume full
  condition); the keep mark tied to What You Have; a liquidity dot from the offer count; no hotkeys, capture,
  overlays, alerts, watchlists, stash value over time or automation.
- **Keep for later** (owner, 2026-10-19, after the five questions): the desk's keep mark has three levels from the
  quest dependency tree, NOW (an active quest's BRING needs the item), NEXT (a quest the active ones unlock needs it)
  and LATER (further down the tree), plus a hideout module's need, regardless of the hideout's progress (its level
  isn't in the logs, and hideout tracking stays a not-goal), each with its found-in-raid flag. The cell shows the
  mark's strength; the hover card lists the needs in progress order. It comes from a concept by a friend of the owner
  (2026-10-19, an "item planner": rows per story, trader and hideout against the columns Now · Next · Later, each cell
  a quest or module with its needed items as slot-size tiles, a FIR check, a yellow "keep, don't sell" frame, counts
  in the headers). The owner: "This uses a different angle along the lines of which item might be useful for later
  quests. This should be something we can make based on a quest dependency tree. Might be something we can incorporate
  into the design, but I do not want to complicate the design too much based on this. If there is a easy way to
  incorporate this into the existing design, do it." Decided: the keep mark's levels, not a planner screen of their
  own. The data (checked in the code, 2026-10-19): the loader keeps each quest's `taskRequirements`
  (`ApiTask.TaskRequirements`; the quest card's UNLOCKS and the QUEST COMPLETE cue's unlocks are read backwards from
  it), so the tree is on hand; objectives carry `foundInRaid` (`ApiObjective.FoundInRaid`); the hideout file is
  downloaded and its levels list their item needs with a found-in-raid flag, but the loader keeps only the stations'
  names (`ApiStation`), so a module's needs are new reading of a file already on disk.
- **Scoping, the coordinator's:** the in-raid glance from a loot screenshot (the owner's "when looting something")
  is a later step with its own design and its own boundary (a raid screenshot's picture is read for its extract
  list's corner only; DESIGN.md §2), not part of B or C. For its design (owner, 2026-10-19, on the GEAR tab's nested
  containers below): "This will also happen in-game btw": looting a body's backpack with a rig inside opens the same
  floating windows in a raid.

Nothing is built. Binding: DESIGN.md §1 (the third answer beside the two questions; the not-goals amended), §2 ("For
the stash desk": the grids against the whole catalogue, for the desk alone; the keep mark's levels), §3 (grid images
for its mirror), §4 ("What you have" points at §1).

**Decided (UI, 2026-10-19)**, on a panel of the desk's place, cell, card and click, each part's options drawn 1:1 in
the app's visual language (the page stays outside the repository): "Stash Desk Panel: A, but keep in mind that the
stash also has the player loadout in view and when a raid ends the player might want just take a screenshot of the
items they have and get an overview of what to do with that. Also keep in mind of stackable things, like a bag stashed
in a bag that contains additional items. A1 · B1 · C1 · D2. Update the Design.md accordingly."

- **A1, the place:** out of raid the desk takes the map's place and the rail becomes its card; PLAN | DESK at the
  rail's head; it opens by itself when a stash or inventory screenshot is read in the menus, and a raid loading always
  takes the window back (RAID LOADING's cue, no click), the desk keeping its page; the MAP list and the trader strip
  stay; cells 40 DIP at 900×560, 56 from a 600 DIP area; the head line "STASH · <total> ₽ · KEEP n · n · n".
- **B1, the cell:** the price top right, where the game prints the short name, after the flea's tag or a trader's
  person (a 14 DIP portrait at 56); the count bottom right; the keep notch top left in three weights (now, next,
  later) in amber, in ink for a hideout module; ↑ ↓ from ±10 % in 48 h; "~" for full condition; the "?" plate with the
  candidates' range; ink behind the art at 5, 10, 16 % from 20k, 100k, 300k ₽ per slot.
- **C1, the card:** decision first (the net figure with its 7-day change, the flea's net with the fee and the 48 h
  change, what a click copies and why, KEEP in progress order with FIR), then the 30-day band from minimum to average,
  the 24 h range bar, the traders, crafts and barters, the prices' age; about 640 DIP, the first 200 in the smallest
  rail.
- **D2, the click:** the cell is the button; a click copies to the clipboard only, its frame flashes gold and its
  figure decodes, a one-line notice says what and why; a "?" cell's click opens its candidates and the pick stays; the
  rule is one setting, "Flea price to copy" (match, or 1–20 % under or over the last low; 3 % under by default); the
  guards as Q4.
- **B's two questions**, unanswered, take the drawn defaults: "later" marks a cell only when the need is found-in-raid
  or the item can't be bought (the card lists every line); the head line counts the keeps.
- **The owner's two notes, as rules:** the inventory screen's loadout is mirrored too (the slots, the pockets, the
  equipped backpack, rig and pouch, each under its own head beside the stash), so a screenshot right after a raid is
  an overview of what came back and what to do with it; a container in a grid (a bag in a bag) is a cell with its own
  price and a "contents unseen" mark until a screenshot with its window open adds its grid to the page under its own
  head, attached to its cell. The desk never guesses contents, and in-raid screenshots stay unread.
- **Seen on the GEAR tab** (five screenshots of 2026-10-19 (GEAR tab), 2560×1440, PvE, kept in the ignored
  `tests\fixtures\ocr\desk`): three regions, the loadout's slots (each with its item's short name and, where it has
  one, its condition), a middle column (POCKETS, SPECIAL SLOTS and the equipped BACKPACK, TACTICAL RIG and POUCH, each
  a slot with its grid inline) and the stash, the middle column and the stash scrolling each on its own; a nested
  container's contents only in a floating window titled with its name, which the player places anywhere, over what lies
  under it (attached to its cell under "ZHUK · in the backpack"; the cells under it keep their last reading; of several
  containers of one name, shown under its name, unattached); a condition ("46/50", "1469/1800") printed where a stack's
  count is; the stash pane's head prints the player's money and the game's toasts the screenshot's path, both skipped.
  To design with step C: a price that follows a printed condition (a scaled estimate, still "~").

Binding: DESIGN.md §4, "Screen anatomy", *The stash desk* (and the rail's *Desk*, "Settings"), principles 2 and 3,
"Design system" (*Motion*, Amber's role), "Visual language" (the colours, "One symbol, one meaning"), §2 ("For the
stash desk": the GEAR tab, nested containers, what a reading keeps), §3 (the desk's prices per reading).

**Decided (addendum, 2026-10-19)**, on a second panel, drawn 1:1 from the five GEAR-tab screenshots: the regions'
layout, the page after a raid, a container in a grid, condition on the cell (the page stays outside the repository).
The owner: "Stash desk addendum I go with your recommendation": A(2) · B1 with the fold · C1a · Da.

- **A(2), the regions:** every grid under its own head, stacked in reading order in one 400 DIP column at the
  smallest window: LOADOUT (a strip of 80×40 slot cells in the game's slot order, a weapon 164×40, no slot names, the
  card names the slot, empty slots left out), then TACTICAL RIG · POCKETS and SPECIAL SLOTS on one row · BACKPACK ·
  POUCH, then STASH; from a desk 980 DIP across the column breaks before STASH into the game's two columns. "A stash
  page of 10 × 11 fits at 40 DIP" holds for the stash alone: with the gear above, the page scrolls unless it is folded.
- **B1, the head line, with the fold:** "ON YOU <total> ₽ · STASH <total> ₽ · KEEP n · n · n", a "+" on a total with an
  unseen container in it. Each reading opens the regions it changed and folds the rest into one line, "UNCHANGED ·
  POUCH ▸ · STASH ▸", each name opening its region until the next reading; the first reading opens all, one that
  changed nothing changes nothing; "unchanged" against the page in hand only, never a register (§1).
- **C1a, contents unseen:** a container cell's figure ends in "+" ("34k+": at least this, more inside) until a
  screenshot with its window open is read; then its grid stands under "<NAME> · IN THE <REGION>" right after its
  region, stemmed to its cell, each lighting the other; the "+" drops, the cell's figure stays the container's own, the
  contents count in the region's total; covered cells keep their last reading, empty on a first reading. Several of one
  name: after the region that holds them all, unattached, "Not attached: 3 wallets on the page"; split between ON YOU
  and the stash, at the page's end, counted in neither. A weapon's mods are unseen the same way. The card: "Contents
  unseen: open it in the game, then a screenshot". No dashed frame: dashed means old.
- **Da, condition:** a printed condition (46/50, 1469/1800) scales the figure by its share, still "~", which now means
  "an estimate from condition"; meds, food and drink at a full print drop the "~", armour keeps it (its printed maximum
  isn't the catalogue's); the card shows both ("Scaled to 1469/1800 · full 37 386"); D2's click copies the scaled
  price. A model: a straight share, and whether tarkov.dev's last low is for full condition only is unchecked.
- **Found on the screenshots, now rules:** a slot cell carries no footprint, so a short name shared by variants stays
  a "?" (4 of the 7 filled slots); "Zhuk" names two rigs of one size, so its cell is a "?" whose window's grid attaches
  either way; a magazine's 60/60 is not a condition; at 40 DIP a 1×1 cell with a long condition loses its arrow (the
  card keeps the change); a cell with no price (the secure container) shows no figure; the game's middle column starts
  with the TACTICAL RIG, so the heads take the game's order. The "+" after a figure is a new mark (the sign also starts
  "+8 %" and BRING's "+N").

Binding: DESIGN.md §4, "Screen anatomy", *The stash desk* (*Its place*, *What it mirrors*, *The head line and the
fold*, *The cell*, *The card*, *The click*, *Fun*), "Design system" (*Motion*), "Visual language" ("One symbol, one
meaning": the "+", the "~").

**Decided (in-raid glance, 2026-10-19)**, the owner's three choices on the answer from a loot screenshot in a raid
(until then the coordinator's scoping above: a later step, its boundary for the owner to decide):

- **When:** every in-raid screenshot that shows an inventory screen (the player's own loadout, or a looted container
  or body) is read as it arrives, like the extract list, whose reading comes first; the menu screenshots' tick gains
  the words "and in a raid" (texts to be worded then).
- **What:** a plate over the map in the cue pattern: FROM YOUR SCREENSHOT, the total, the top five by value per slot
  with their keep marks, "n unsure", for about 6 seconds; no click; the map stays beside it; the raid's rules
  unchanged (glanceable, nothing to do).
- **Kept:** nothing; no in-raid pixels, positions or items are stored. The desk's nested-container rules apply (a
  looted backpack with a rig inside: the rig's contents only from its open window).
- Fixtures: 22 in-raid screenshots of 2026-10-19 (one raid) in the ignored `tests\fixtures\ocr\raid-loot`.
- Its look on a panel of its own after step B; built after step C.

Binding: DESIGN.md §1 (the third answer's paragraph), §2 ("For the stash desk": "Which pictures"; the boundary's list),
§4, "Screen anatomy", *The in-raid glance*.

**Next.**

- **Decided after the owner tried the dev build (2026-10-19):** the desk's cells scale with the desk's width (40 DIP at
  900, up to about 80 on a 2560 monitor, the game's grid proportions kept), the rail stays for the cards; a click on a cell
  COPIES the price as D2 drew it (the flash, the decoding figure, the notice as the feedback; owner, 2026-10-19, later:
  "clicking on the item suffices"), the rule for what a click copies is a visible control on the card (match / under /
  over the last low, mirrored in settings), and a pin glyph on the card or a right-click on the cell PINS the card in
  the rail (several stack, × unpins); the sparkline, the 24 h bar, every trader, the keep
  lines (which quests and hideout modules want the item) and the copy are step C, which the owner wants before 0.7.0
  ships ("Wait for C"). No tooltips, by design.

- **Measured (2026-10-19): the label alone against the whole catalogue** (`shturmap-cli stash --set all --no-art`: 5,480
  items, 3,872 short names; no image fetched, no request). On four hand-labelled pages (the key container, the medical
  case, a stash page, a GEAR-tab stash page; 183 items): at the fit 0.8 125 right, 1 wrong, 10 unread, 47 "one of N"; at
  0.9 125, 0, 12, 46; the spike's quest set (3,921 items) 136, 1, 10, 36; each page's own names alone 171, 0, 12, 0.
- Over the 39 pictures read (1,908 rectangles, 46 a picture, 69 on the GEAR tab): named 51 %, "one of N" 23 %, nothing
  said 20 %, odd shapes 6 %; 1.1 s a picture (1.6 s on the GEAR tab), 0.08 s more at 0.9. Of the ties 86 % are items
  sharing one short name (magazines by capacity and colour, the two bandages, two cabin keys, the Hot Rod can and bag),
  14 % the cut-label rule ("Weapons" fits 49 sealed weapon cases); the item file's footprint leaves one for 38 %; where
  it leaves several, they span a median 1.6× in price, 30 % over 2×.
- At 0.8 four labels read a letter off named another item (a dog tag's nickname as a pistol slide, a TC-2002 helmet as
  the TP-200 brick, the slim diary as the diary, the dog tag case as a dog tag: up to 500× off in price), found where
  the footprint contradicted the cell or the run at 0.9 named it otherwise; at 0.9 all four go (the reader reads again
  until a label fits closely), for 1 % fewer names.
- **Conclusion:** the label alone is safe for a price at a fit of 0.9, not at 0.8; it names half the cells and leaves a
  quarter "one of N" with wide ranges, so the "?" will be common. Step B takes 0.9 and the item file's footprint (no
  download) as the tie-break; the art isn't needed for safety, only for the same-name, same-footprint variants left,
  judged by the "?" count after B. The report stays outside the repository:
  `%USERPROFILE%\source\repos\Shturmap-catalogue-measurement-2026-10-19.md`.
- Step B, "Glance": the reader generalised (the whole catalogue; positions and footprints; the GEAR tab's loadout
  slots and grids; container windows, attached to their cells); the data fields kept (the item file's price fields and
  the hideout levels' item needs, which the loader drops today); the desk view with its cells and head, as the
  addendum has them: the loadout strip, the region heads (a container window's stemmed to its cell), the head line ON
  YOU · STASH · KEEP, the fold, the "+" and the condition scaling; the PLAN | DESK switch; the menu screenshots' tick
  worded for the desk (the panel left it out).
- Step C, once B's naming has held up: the card, the price history, the click and its setting (the card's lines for a
  "+" and a scaled figure, the click's scaled price). Built 2026-10-19 (below).
- The in-raid glance: its panel after step B, its build after step C. Both 2026-10-19: Variant 1 with the looted
  layout mirrored, built (below, *Built (the in-raid glance)*).
- The flea's price field takes Ctrl+V (owner, 2026-10-19: "Copy and paste worked"). `SafetyTests` forbids no clipboard
  call (step C, 2026-10-19: checked, and said beside its list).

**Built (step B part 1, 2026-10-19: the reading and the data; no view, no texts, no setting changed).** Binding:
DESIGN.md §2 ("For the stash desk", *Built*), §3 (the desk's prices, *Built otherwise*), §4 (*The stash desk*, *Built:
the model the view draws*), §8 ("The desk file", "How the parts work", *Menu screenshots*).

- **Where it lives.** The model and its rules in `Shturmap.Core.Desk`: `DeskPage`, `DeskRegion`, `DeskCell` (the page),
  `DeskLedger` (desk.json, the merge, the fold's changed regions, a window's attachment), `DeskValues` (the fee by
  tarkov-api's `fleaMarketFee`, read 2026-10-19; the figure; condition scaling; tint, arrow, liquidity), `DeskKeep` (the
  levels), `DeskSheet` (the values over a page, the head line), `DeskView` (what is published). The reading in
  `Shturmap.Game.Screenshots`: `DeskReader`, `DeskLayout` (the screen and its regions), `DeskFrames` (compartments),
  `DeskMatch` (the label against the catalogue), `DeskWords`; `MenuScreenshotReader` runs it after the stash reader on the
  same picture. The data: `ApiItem`'s price fields, footprint and `hasGrid`, `ApiFleaMarket`, `ItemSources.HideoutNeeds`,
  `PricesAt`, `GameDataLoader.DeskPricesAge`; `Shturmap.Session.DeskData` (catalogue, facts, flea rules, keep levels);
  `GameSession.Desk` (the reading taken, the file, the prices refreshed, `SessionSnapshot.Desk`). The CLI: `shturmap-cli
  desk <png|folder>` (page, values, page in hand over the pictures, truths; `--audit` the DESK line, which `bring` prints
  too). Tests: `DeskValuesTests`, `DeskKeepTests`, `DeskLedgerTests`, `DeskSheetTests` (made-up ids), `DeskReaderTests`
  (drawn GEAR tab, window, frames, match), `DeskSessionTests`, `DeskRealPictureTests` (skip without the ignored
  pictures and the cache).
- **Measured** (`shturmap-cli desk`, cache only, no request): the GEAR tab's truth (its stash, 37 items) 29 right, 2 "one
  of N" (the truth among them), 6 unread, 0 wrong (the spike's reading at 0.8 against the whole catalogue, without the
  footprint: 23 named); the three
  stash truths of 2026-10-11 (148 items) 129 right, 16 "one of N", 3 unread, 0 wrong. Over the five GEAR pictures: 60 to
  129 cells a picture, about three in five named, a fifth "one of N" (the slots' variants: the armband one of 11, the dog tag one
  of 32, BAC Bastion one of 3; the two bandages; the Hot Rod can and bag), a sixth unread (labels the art hides, a label
  read a letter short: "Appl", "ALOK-B", "TC-200"); regions: the loadout (7 slots filled) and the stash on each, the middle
  column's parts in view (POCKETS, SPECIAL SLOTS, BACKPACK, POUCH; TACTICAL RIG where scrolled up; a grid the scrolling
  cuts left out); the Zhuk window found on both pictures that show it, attached to its cell in the backpack across the
  three readings; 1.9 to 2.4 s a picture. The DESK audit line (PvE): 5,006 items with a short name, 608 with no price at
  all, 24 without a footprint, 759 of 3,344 flea-priced (not guns) sharing short name and footprint; PvP 690 of 3,159.
- **For the view** (part 2): bind `SessionSnapshot.Desk` (`DeskView`: `Mode`, `Sheet`, `Changed`, `First`, `Reading`,
  `PricesAt`). `Sheet.Regions` are in reading order, each a `DeskRegionSheet` (`Region` with `Key`, `Kind`, `Cols` ×
  `Rows`, `Parts`, a window's `Title`, `AttachedTo` or `SameName`; `Part` ON YOU, STASH or neither; `Total`, `Plus`).
  Each `DeskCellSheet`: `Cell` (`I`, `J`, `W`, `H` in grid units; `Slot` for a slot, no footprint; `Ids`: one named, more
  "one of N", none unread; `Count`, `Print`, `FoundInRaid`), `Figure` (`Value`, `High` for the range, `Source` Flea,
  Trader with `TraderName`, None for a range; `Estimate` "~", `Plus` "+", `Unsure`, `Full`, `Tint` 0 to 3, `Arrow`,
  `Liquidity`, `FleaLevel`, `FleaNet`, `Fee`, `TraderPrice`), `Keep` (Now, Next, Hideout, Later, None) with `Needs` (the
  card's KEEP lines), `Unseen`. `Sheet.Totals` is the head line. A view can make a page of made-up cells with
  `DeskSheet.Of`.
- **Open.** The tick's words, note, help, README and PRIVACY.md don't say the desk yet, while the reading runs under the
  tick in a developer build (part 2's texts; before any release). The prices per reading are the item file again, not
  the items a screenshot showed (json.tarkov.dev has no price per item; DESIGN.md §3): at most one request an hour of
  desk use, about 1.4 MB when it changed: the owner's word on it. The GEAR tab's words are the English game's; other
  languages, 1080p and other aspect ratios unmeasured (the slot boxes' sizes are the 1440p screenshots' in cells). The
  rules' choices listed in DESIGN.md (*Built: the model the view draws*: NOW with hand-overs, the notch's order, a "?"
  cell's keep and total, the fold ignoring the found-in-raid mark) are mine, for the owner to overrule. Not read: a
  container slot's own label when the art covers it (the backpack's "Trooper 35" read "ATcoop,er 35"), the quick-use
  bar (never: its slots repeat what is on the player). The cyan outline, a hover highlight across items, and the art's
  tie-break for the same-name, same-footprint "?" (judged by the "?" count after part 2) stay as the measurement left
  them.

**Built (step B part 2, 2026-10-19: the view)**, over part 1's model (`SessionSnapshot.Desk`, `Shturmap.Core.Desk`):

- **Where it lives:** `MainWindow.Desk` (the state: PLAN | DESK, a reading opening the desk, a raid taking the window,
  the fold kept, the rail's card), `Controls.DeskBoard` (the page: head line, regions, fold lines, stems, entry and
  count-up), `Controls.DeskCellView` (a cell and its marks), `Rules.DeskSwitch`, `Rules.DeskFold`, `Rules.DeskCells`,
  `Rules.DeskNumbers`, `Rules.DeskPageRules` (each tested in `tests\Shturmap.Core.Tests\Desk`), `Rules.DeskExample`
  (the made-up desk); the switch and the rail's desk part in `MainWindow.xaml`; texts in `RuleTexts`, `ViewTexts`,
  `AppTexts` (en, de; the German for the senior translator's review); help's privacy paragraph widened for the desk
  (the tick's words and note are the texts agent's, merged in main).
- **Dev aids:** `--desk [<n>]` and `tools\fake-raid.ps1 -Desk [-DeskReading n]`; the script's `desk [<n>] | open
  <region> | point <region> [<i> <j>] | plan | show`; the layout check's `desk` view; the replay `tools\qa\desk`.
- **Rules taken where the panels left a choice:** DESIGN.md §4, *The stash desk*, *As built* (the switch's look, when
  the desk comes and goes, the rail, the page's columns, what gives way in a narrow cell, the band under the figure, the
  "?" plate's move, the numbers' form, the stem's route and light).
- **Open:** the desk's own card and the click (step C); the website on what the desk reads; the tour's pictures of
  chapters 1 and 2 (the release prep); the German gear heads to check in the German game
  (TASCHEN, SPEZIAL-SLOTS; the desk's German reviewed, senior translator, 2026-10-19); a turned cell's picture turns a
  quarter clockwise, unchecked against the game; the reading counter's start: the view takes a page read back from the
  file for reading 0.

**Built (step C, 2026-10-19: the card, pinning, the click's copy, the cells scaling)**, as the owner asked after trying step
B (the bullet under *Next* above; then "I don't think we need a visible copy button, clicking on the item suffices. But
there should be a visual feedback and UI elements for setting what is copied clearly visible."). Binding: DESIGN.md §3
(the history built), §4 (*The stash desk*: *Its place*, *The card*, *The click*, *Fun*, *As built*: *The rail*, *The
card*, *Pins*; "Visual language", the pin's tab; *Motion*; "Settings", "Flea price to copy"; the keys' Esc), §8 (the
desk file's pick, the script's `desk` verbs, `-Desk`).
- **Where it lives:** the scale rule `Rules.DeskPageRules.Layout` (40 to 80 DIP in steps of 8; two columns where both fit
  at 56 or more) with the marks' sizes in `Rules.DeskCells` (`FigureSizeAt`, `SmallSizeAt`, `GlyphAt`, `PortraitAt`,
  `NotchAt`, `PlateAt`) and the heads' `DeskPageRules.HeadSize`; the copy `Core.Desk.DeskCopies` and its rule
  `DeskCopyRule` (`desk.copyRule` in shturmap.db: `match`, `under:3`, `over:2`); the history `Core.Desk.DeskHistory`
  (the points, the band, the 7-day change, the high and low) and the day's bar `DeskDayRange`; the fetch
  `GameDataLoader.LoadPriceHistoryAsync` and `GameSession.DeskHistoryAsync`; the pick `GameSession.PickDeskAsync` over
  `DeskLedger.Pick` (slots too; carried over a reading that shows the cell as it was); the card's words `Rules.DeskCards`,
  the pins `Rules.DeskPins`, the card `Controls.DeskCardView` (with the rule's stepper, which settings' row shows too), the
  cell's click, right-click, pin tab and flash in `Controls.DeskCellView` and `DeskBoard`; the rail, the copy, the rule,
  the picks and the histories in `MainWindow.Desk`; texts in `RuleTexts` (the card, the plate, the notices, the slots'
  names) and `ViewTexts` (settings' row, the pin's and the stepper's tips, help's desk paragraph, the rail's hint), en and
  de; the made-up desk's states and histories in `Rules.DeskExample`. The DESK audit line gains HISTORY (the cached
  histories read as the card reads them: UNREAD, FEW).
- **Tests:** `DeskCopyTests` (the rule, rounding, scaling, every guard, the best net), `DeskHistoryTests` (the file, the
  7-day change, the band, the day's bar), `DeskCardTests` (the card's words from the made-up desk, the plate's states, the
  "?" card, the notices, the clipboard's digits, the age), `DeskPinsTests`, `DeskPageRulesTests` (the scale rule's
  numbers, the marks' sizes), `DeskLedgerTests` (the pick kept and dropped, a slot's pick), `PriceHistoryTests` (one file
  per item, its ETag, PvP's own file), `DevToolsTests` (the new `desk` steps).
- **Dev aids:** the script's `desk pin|copy|point <region> <i> <j> | <slot>`, `desk unpin [<region> <i> <j> | all]`,
  `desk pick <region> <i> <j> | <slot> <n>`, `desk rule match|under:<n>|over:<n>`; a script's click leaves the clipboard
  alone (the app log says what it would have copied); the replay `tools\qa\desk` gained the card, the copy, the rule, two
  pins, the guards and a pick.
- **Rules taken where the brief left a choice** (DESIGN.md *As built*; the owner's to overrule): the rule's control is a
  stepper, − and + around "3% UNDER", not four fixed choices (one control holds every rule the setting allows, so card and
  settings never disagree); the pin's symbol is a tab, a short amber bar on a cell's top edge and the same tab over a
  cell's outline on the card's head, not the pushpin (the Place quest type's); a pinned card is framed in amber as a held
  card is; Esc leaves the pins alone; a right-click on a pinned cell unpins it; the pins have no limit; the slot's name
  stands over the item's name, not before it; KEEP's order is now, next, a hideout module's, later; the 7-day change is
  the last day's averages against the day a week before's; an outlier high is one more than twice the average and the
  last low; the history is had again after an hour, a failure after two minutes, at a card's showing only; traders' prices
  per item at full condition.
- **Measured** (`shturmap-cli desk --audit`, cache only): HISTORY none cached here (a card fetches one
  when it first shows an item; the made-up desk asks nothing). One history fetched by hand for the shape, 2026-10-19:
  the LEDX's PvE file, 85 KB, `{"data":[{priceMin, price, offerCount, timestamp}], "translations":[]}`, a weak ETag,
  `Cache-Control: max-age=691200`.
- **Pictures** (`fake-raid -Desk -PlanOnly`, the folder build; the replay at 1600×900 in English and 900×560 in German):
  900×560 one column at 40, 1600×900 two at 64, 2560×1440 (the window 2544×1401 inside) two at 80; the card, a copy's
  flash and decode with its notice, two pins, each guard, a "?" card and its pick. The layout check of the desk at
  900×560, English and German: PASSED.
- **Open:** the pointed card stands under the pins (the brief's order): with two pins at 900×560 it is out of sight until
  the rail is scrolled; at 2560×1440 the desk at 80 DIP leaves about a third of its width empty (the cap of 80); the cells
  step down from 80 to 56 where the second column comes in; the German slot names (OHRHÖRER … SCHEIDE) to check in the
  German game; the texts for the senior translator; the UI QA pass of step C; the layout check's `desk` view shows no
  card (no pointed cell in a snapshot run); whether the game's fee for a damaged item scales its base price as the copy
  assumes; the website, the tour's pictures and What's New for 0.7.0.

**The UI QA pass of step B (2026-10-19, on c7b3a87) and its fixes.** The pass read a GEAR screenshot dropped into a
Plan-only fake raid at 1600×900, then the one with the Zhuk window, and the replay `tools\qa\desk` at both sizes and at
125 % text. Fixed (DESIGN.md §2, *Built*; §4, *The stash desk*, *The cell* and *As built*; "Text size"):
- F1, F2: the reader gave LOADOUT an eleven by one grid, which the view drew under the strip and counted in the gear's
  column (616 DIP), so the stash's tenth column ran past the window. LOADOUT is a strip with no grid now, in the reader
  and in the view whatever a page says; two columns only where both fit, POCKETS and SPECIAL SLOTS side by side counted
  in the gear's (`DeskPageRules.Grid`, `ColumnWidth`, `ColumnBreak`).
- F3: a grid's compartments stand apart, 2 DIP at 40, 4 at 56 (`DeskPageRules.Inset`); the made-up Zhuk is the game's
  ten pockets of one by two.
- F4: a slot's label against its slot's kind of items (`DeskSlotKind`, by tarkov.dev's categories; the special slots'
  type), a special slot without footprint, a label under a quick slot's plate a name's end, a print read again by its
  brightest channel and in the wider band. The pockets were laid out right (1×1, 1×2, 1×2, 1×1, as the game has them);
  their misses were the reading's. `shturmap-cli desk`, the GEAR picture's loadout, pockets and special slots (16
  cells), before → after: 5 right, 2 wrong (the Splint for the Alu splint, the lubricant "SurvL" for the Surv12), 7 one
  of N, 2 unread → 9 right, 0 wrong, 6 one of N, 1 unread (the V40, which the recognition doesn't read); the armour's
  "147/147" and the meds' red "1/3", "3/3" read, the Surv12's red "8/9" still not (nothing in some 150 tries of band,
  zoom, sampling and contrast). The stash truths unchanged: GEAR 29 right, 2 one of N, 6 unread, 0 wrong; the stash
  pages 129, 16, 3, 0; about 2.1 s a GEAR picture (2.0 before).
- F5: at 56 DIP the change figure where the count leaves room, then the figure without its arrow, then the arrow.
- F6: the cells' marks laid out at the drawn text size, a number never cut (DESIGN.md "Text size" lists what moves to
  the card at 125 and 150 %).
- F7: the stem leaves a cell at its middle, as written. F8: a fold line breaks before its "·". The translator's note:
  the English units with no-break spaces, held by `TranslationTests`. F11: the replay waits out the tour's notice, and
  `desk.md` says "0.3–294k".
- Not done: F9 (the weapon slots' art small and dark) and F10 (the tint's steps barely seen), the owner's calls; F12, a
  script verb for Esc (it would take the script, the controller and the window, more than a few lines).
- Open: at 150 % a four-figure price beside the notch of a 1×1 cell at 40 DIP moves to the card (the keycard's "352k"):
  the owner's call whether it may, or the cell grow; the figures' width is an estimate (half an em a character, a
  count's digits 0.45), measured on one picture; a slot whose best reading is two characters ("35" of "Trooper 35") is
  left out as empty.

**The switch, a primary control** (owner, 2026-10-19: "The Plan/Desk switch is way to small, needs much bigger."):
PLAN | DESK across the rail's 351 DIP above MAP, 36 DIP tall, its words in the title style, the view on screen raised
with the open map's row's amber bar; ← and → pick (`DeskSwitch.ByArrow`). DESIGN.md principle 3, *The stash desk*.

**After the owner's first test (2026-10-19)**, on four GEAR-tab screenshots of the Keys container (kept in the ignored
`tests\fixtures\ocr\desk\keys`), with the UI QA pass of step C (F2 to F5) and the senior translator's items. Binding:
DESIGN.md §2 ("For the stash desk": a window's lattice, the prices every ten minutes), §3 (ten minutes, the age from the
scan), §4 principle 11 and "What you have" (the notice), *The stash desk* (*Its place*: the cells fill the desk; *What it
mirrors*: a blank reading; *The head line*: the totals; *The cell*: the listing price, "~", a count past the stack; *The
card*; *The click*; *As built*: the rail, the card, the page, the stems), §8 (how the parts work).

- **A sorted container read as one "?"** (owner: the Keys window read right at 20:21 and wrong after the game's SORT at
  20:22). The stash's grid finding snapped the window's lattice onto the window's thin bright outline, 6 px left of its
  first cells (811 for 817 at 1440p): every line between the cells was missed, the 71 keys joined into odd shapes, and
  one 6×1 "?" was left; at 20:21 the frame found on the cells' lines stood beside the lattice as a second compartment
  (6 px apart, over the old 4 px tolerance), so the keys were read once and a "?" across the other. Fixed in the desk's
  reader (`DeskLayout.Aligned`, `SameCompartment`, `Overlapping`; `DeskReader`): a window's lattice moved onto the lines
  between its cells, a frame within a quarter of a cell of it the same compartment, two compartments lying over each
  other read once; a shape no label fits (the hover highlight over RB-AK joined five keys) read cell by cell.
  `shturmap-cli desk`, KEYS window, before → after: 20:21 12×7, 2 parts, 73 cells (72 keys and the "?") → 11×7, 1 part,
  72; 20:22 11×7, 1 cell (the 6×1 "?"), 27 on the page, 11 unread → 11×7, 72 cells, 98 on the page (78 named, 10 one
  of N, 10 nothing said). The truths unchanged: the GEAR tab's 29 right, 2 one of N, 6 unread, 0 wrong; the stash pages'
  129, 16, 3, 0. A drawn test (`DeskReaderTests`).
- **A blank reading never replaces a good one** (`DeskLedger.Blank`, `DeskChange.Refused`, the app log's "Desk reading
  kept the page's …"): a window with no cell read, or a region with fewer than a third of the page's cells read while
  more of it held something unread than was read, keeps the page's version, unchanged. The brief asked for the third
  alone; I added the unread share so an emptied region (a backpack unloaded after a raid, a stash paged to an emptier
  page) still replaces the page's: the owner's or the coordinator's to overrule. Tests on made-up pages
  (`DeskLedgerTests`).
- **"STASH 64.6 M+" while the CLI said 4.40 M**: the page in hand kept a Junk window read at 19:10 (the others stay),
  counted in STASH (two junk boxes on the stash page), at 60 M: 53 M of it a drill read "×310" (it doesn't stack), and
  its GPUs, hoses and fuel twice (its compartment found twice, lying over itself). Neither "?" ranges nor the merge
  summed anything twice. Fixed: the compartments read once (above); a count past the item's `stackMaxSize` counts one
  (`DeskSheet.CellOf`); a file with a cell read twice takes each place once (`DeskLedger.Load`). The owner's dev
  desk.json of 20:26 read with the fix: STASH 64.1 M → 8.24 M. Tests (`DeskSheetTests`, `DeskLedgerTests`).
- **The desk's arrival replaces SEEN IN YOUR STASH** (owner: "It now conflicts with the 'now seen in stash' animation";
  decided: the desk's arrival is the feedback): a reading's needed items are said by the existing notice ("Seen in your
  stash: 3 keys your quests need.", its plurals and German as reviewed), the BRING rows' marks glow as before, the cue
  plays only as What's New's 0.5.0 example; the developer script's `stash` gives the notice (`Rules.DeskSwitch.StashSeen`,
  `DeskSwitchTests`).
- **The cells fill the desk** (owner: "there is a lot of wasted screen space … Find the optimal solution"; the
  coordinator's rule): 40 to 128 DIP in steps of 8, the largest at which the widest row fits the width and the tallest
  column the height; the page flowed: the gear down a column and on into the next where the height ends, the stash a
  column of its own, its windows in the shortest column beside it (the rule's two columns alone left a window under the
  stash twenty rows tall, and the gear read whole one column 1,100 DIP tall at 40); scrolling at 40 only where nothing
  fits (`DeskPageRules.Layout`, `Arrange`; stems between neighbouring columns down the gap between them). The owner's
  page after the Keys reading: 96 on a 2560 monitor's desk (2176 × 1330), 48 at 1600×900, 40 at 900×560 (one column,
  scrolling); the made-up desk read whole: 80 in three columns at 2544×1401 (the largest the tool allows here), 40 in
  three at 1600×900, unscrolled (64 in two before, scrolling). Tests (`DeskPageRulesTests`).
- **The pointed card first** (QA F2): the pointed cell's card under the switch, the pinned cards under it, one line each
  (icon, name, gold figure) while it stands, whole when none does; a click on a line shows that card whole on top.
- **The card's first lines fit the rail in German** (QA F5): the plate in short lines ("Copies 970,000 ₽", muted "Nets
  873k ₽ after the fee", the last low and the trader in parts that wrap only between them), the flea's net and fee in
  parts too; at 900×560 the figure, the plate and KEEP show in English and German; at 125 % text they wrap between
  their parts.
- **The stepper's tips** (QA F4) say where a press goes on either side of the last low ("Lower: 1% over the last low");
  **QA F5's** `tools\qa\desk.md` (the totals 5.56 / 7.46, the `pinned` still); the translator's items: the band's "7 D"
  a text ("7 T"), `DeskCards.Change` spelling its no-break space.
- **The owner's additions after the test**: the cell shows the flea's listing price at the last low where the flea is
  the better sale (the Dorm 104 key "~11k", its net, where the flea listed 15k), the trader's price where a trader pays
  more; the net is the card's line; the head line's totals and the tint count what the sales bring (net or trader), not
  the listing prices (a key listed at 15.0 M nets 5.09 M: the listing prices would have doubled the Keys window's
  total; the owner's to overrule). A full print drops the "~" (keys at 40/40, meds, food; armour keeps it). The card's
  "Prices n min old" counts from tarkov.dev's scan of the item (`updated`). The item file is asked for again when a
  reading arrives and the saved copy is over ten minutes old (an hour before; the owner: "Maybe we need to fetch the
  prices more often then?"); README, PRIVACY.md, the setting's note and UPDATES.md say so.
- **Reviewed** (senior translator, 2026-10-19): the round's German holds but one line, the warning's „bei diesem
  {net}“ → „bei diesem Preis {net}“ (`DeskCardOverBest`; „diesem“ had nothing to point at); the glossary carries the
  split plate line, the stepper's tips and the ten minutes. The plate's lines measured in Bahnschrift against the
  plate's 299 DIP: „Kopiert 970.000 ₽“ 110 (133 at 12.500.000 ₽), „Bringt 873k ₽ nach Gebühr“ 150, both one line; the
  reasons line „Letzter Tiefstpreis 970.000 ₽ · Therapist zahlt 650.000 ₽“ 311, wraps between its parts (about 16 DIP
  taller than English; „Letzter Tiefstpreis“ is not shortened, glossary).
- **Open:** the UI QA pass of these changes; the quest marks' stash reader (`StashReader`) reads windows through
  the same lattice without the alignment: on the two Keys pictures it finds 23 and 28 rectangles of the window's 72 keys
  (`shturmap-cli stash --set all --no-art`), so needed keys in a sorted container go unmarked (not changed: outside the
  brief); the made-up page read whole is smaller at 1600×900 than before (40 for 64), unscrolled; the third-or-unread
  rule's narrowing; the totals by what the sales bring.
- **UI QA pass of the second round (2026-10-19):** FAIL 0/4/2 → F1 fixed (the pins' foot, three lines at most,
  `DeskPins.FootRoom`; a line's click shows its card from its head), F3 fixed (`desk.md`; `desk show <n>`, stills
  `pinshown`, `pinmore`); F2 (the art's printed name shows through the 88 % band at 80–104 DIP) for the owner; F4–F6
  (the real Keys reading, the layout check, the pin click, 125 %, settings after a switch) in the re-check. Open: at
  900×560 three pins leave the card its figure and copy line; a fourth line has no hint but the scroll bar.
- **Re-check (2026-10-19):** the pins' foot, the real Keys reading in the fake game (90 and 93 cells, the KEYS window
  both times, STASH 14.7 M+), the layout check at 900×560 en+de, 125 % text and the settings after a language switch
  pass; F1 the compact line's trim recorded as the rule; F2 the stem across a 3×3 item's picture fixed (the route over
  empty cells only); F3 the version line's "NAME" is the release name, the owner's.

**0.7.0 is named Kaomoji** (owner, 2026-10-19, on the session's report page) and waits for the owner's further
testing (owner, 2026-10-19: "Do not release, will run further testing later"); the release QA pass reruns on the final
tree before the go.

**The release QA pass of 0.7.0 (2026-10-19, on 63d9c06) and its fixes.** Fixed:
- F1: a language switch left the desk's page (head line, region heads, "Not attached", "5.45 M", "−3%") and settings' row
  of the copy's rule in the old language: they are built in code, and are built again at a switch now
  (`MainWindow.RetellDesk`; DESIGN.md §8, "The app's own language"). The script's new `language <culture>` pictures a
  switch with the cards up. `-SwitchCheck` of the desk at 1600×900, en → de: PASSED; a script with two pins and a pointed
  card, en → de → en: page, cards and figures follow.
- F3: the tour over the quest's and the desk's pictures was not the first start's (each run has a database of its own in
  its fake game's folder; `--data` holds only the logs, and a snapshot run never opens the first start's tour). Help,
  opened by itself, put the focus on TAKE THE TOUR, whose tooltip is a window of its own that Windows keeps on a monitor:
  it stood in the top right corner of the monitor beside the window for twelve seconds of every run (measured), where a
  click could reach it (inferred; not reproduced, no input was sent). A quiet run's flyouts open without the focus now,
  a snapshot run opens the tour only with `--tour`, and the app log says how a tour opened (DESIGN.md §8, *Quiet runs*).
  The en+de layout check at 900×560, twice: PASSED, the quest's and the desk's pictures each time, 42 panels drawn.
- F4: the notes, DESIGN.md and the diagnostics name the setting as the app does ("Read your stash, gear and tasks …").
- F6: the pseudo-language padded a paragraph with one word of 130 dots and more; it pads in words of twelve now
  (`PseudoText.PaddingWord`), and the desk's window titles ("WZ") count as the game's names. Pseudo-language at 900×560,
  plan and desk: PASSED, no warnings.
- Open: F5 (tour chapter 2's staging), the owner's call.

**Built (the reader round, 2026-10-19)**: the grid finder's splits of wide items, the quest marks' lattice, a label
rule, a truth put right, and the reading half of the in-raid glance (the plate is another step). Binding: DESIGN.md §2
("For the stash desk": *The reader round*, "In a raid" and its *Built*), §4 (*The in-raid glance*, *Reader built*);
UPDATES.md's two rows. Measured with `shturmap-cli desk` (now with `footprints-<picture>.txt` beside a truth, `--raid`,
`--lattice`) and `shturmap-cli stash --set all --no-art`, before → after:

- **A wide item read whole** (`StashGrids.Items`, `StashLines.VerticalHeight`): the 34 "?" cells of the 20:38 page
  were pieces of 11 items the grid's own line cut, because that line shows through an item's background (median 18
  grey levels at most against the frames' 25 to 130; `--lattice` prints both). A line under 0.45 of the grid's frames
  is no frame; a dark cell beside one (a receiver read as empty) is the item's; such joins stand only as a whole
  rectangle, and where the rectangle's label names nothing of its size its pieces are read (the keys case over the AVS
  at 20:22 made a 3×4 rectangle). Guns fit cells larger than the item file's bare receiver (the M4A1 is 1×1 there).
  The 20:38 page against its footprints (72 items): read whole 61 → 72, right 51 → 61, one of N 10 → 11, wrong 0 → 0,
  split 11 (34 "?" pieces) → 0. The truths: GEAR 29 / 2 / 6 unread / 0 wrong → 30 / 2 / 5 / 0 (37 items), 30 / 2 / 2 /
  0 on the corrected 34; the stash pages 129 / 16 / 3 / 0 unchanged. The Keys pictures 20:21 and 20:22: 72 cells each,
  attached to its cell in the stash, as before; the other GEAR pictures of that folder say nothing on 22 cells for 30
  and 3 for 23. About 2 s a picture, as before. `StashGridsTests` (drawn).
- **One lattice for both readers** (`StashGrids.Aligned`, moved from `DeskLayout`): the grid finding moves every
  window's lattice onto its cells' lines, and the quest marks' reader reads an odd shape cell by cell as the desk does
  and a joined rectangle's pieces where a candidate's art says another size. `stash --set all --no-art` on the Keys
  pictures: the window's 72 keys on both (the pictures' items 23 → 89 and 28 → 89); the three stash truths (all, no
  art) right / says nothing / one of several: 26 / 2 / 27 → 27 / 1 / 27, 36 / 0 / 7 → 37 / 0 / 6, 45 / 0 / 3
  unchanged, 0 wrong; with the truths' names, named 55 / 43 / 48 → 55 / 44 / 48, about 2 s a picture (the odd shapes
  read cost 0.3 s on one).
- **A cut label's digits** (`StashLabels.Fit`): a label cut before a digit no longer fits as the name's beginning
  ("Marl 21" for "Marl 214", made-up names in `StashReaderTests`); the desk's matcher had it already. No tally moved
  by it alone that could be told apart; 0 wrong stays 0.
- **The 13:49 truth** (ignored, put right in place): the three Water bottles at A 4,0, 5,0 and 6,0 went: their labels
  lie above the visible grid (the stash scrolled), so no reading can name them.
- **The raid's inventory screen** (`DeskReader.ReadRaidAsync`, `DeskLoot`, `GameSession.Glance`, `RaidGlance`): a
  position screenshot in a raid, after its extract list, under the menu screenshots' tick; the character screen's tabs
  read first in a strip (about 30 ms), so the world's pictures go no further. On the 22 raid screenshots: 9
  inventories told (a drawer, a safe, a weapon box, six bodies), 13 not (the world, and the menus' picture after the
  raid). LOOT regions named / one of N / unread: the drawer 1/0/0; the safe 3/0/0 (USD ×70, the clock, the horse); the
  weapon box 2/0/1; the bodies' slots 0/1/0, 1/1/0, 2/0/1, 1/0/1, 1/0/1 (a slot whose heading scrolled away isn't
  read), rigs UNSEARCHED (2) and 0/4/1, pockets SEARCHING, UNSEARCHED (2), 0/0/0 and 0/0/2 twice (two "PM" magazines,
  two letters the recognition doesn't read), backpacks UNSEARCHED, 1/0/1 and SEARCHING 4/4/0: 16 named, 12 one of N,
  11 unread in all, no truth written. What the pane needed: its head word and the kind at the screen's edge (a
  weapon's label stood level with the head); its untitled grids and a part's "SEARCHING ✕" bar are no windows; a
  container's frames on one lattice are one grid starting under the head (the safe's frame round all didn't show, a
  rifle's red ground hid the weapon box's top line); a body's parts from heading to heading, a slot box under each as
  in the loadout; the experience toasts are toasts. Not kept: `GlanceSessionTests` hold that no desk.json is written
  and the glance goes with the raid.
- **For the plate** (the raid page's shape): `SessionSnapshot.Glance` is a `RaidGlance(Sheet, Loot, TakenAt,
  Reading)`, null out of a raid; `Sheet` is a `DeskSheet` as the desk's: regions with `Part` OnYou (loadout, rig,
  pockets, special, backpack, pouch), Loot (`loot:slots`, `loot:rig`, `loot:pockets`, `loot:backpack`, or one `loot`
  grid) or Neither (a window attached to none); `Totals.OnYou`/`OnYouPlus`, `Totals.Loot`/`LootPlus`; each cell's
  figure and keep notch. A LOOT region has `Title` "LOOT · BODY" (the game's words), `Search` (Searched, Searching,
  Unsearched) and `Cut`, whose "+" is the region's `Plus`. `Loot` is that title, null for the player's inventory
  alone; `Reading` counts up per raid.
- **Open:** the tick's words and note gain "and in a raid" with the plate (README, PRIVACY.md, help, and CLAUDE.md's
  "four things" a screenshot is read for), and until then raid inventories are read under the present words (done with
  the plate, below); a
  backpack whose heading scrolled out of view, its items highlighted after looting, reads as a window of no name
  (counted in neither; one picture), and a body's rig opened in a window ("CR MK2 (R)", read "CR MQ (R)") attaches to
  nothing; the bodies' 2-letter labels ("PM", "PB") and "ScavBP" after a glyph stay unread; a raid screenshot is
  opened twice (its corner, then the glance; the second opening's cost not measured); the 3 s budget of
  `StashReaderTests`' real pictures failed in two of three full parallel runs (3.2 s on a picture read in 2.0 s alone,
  its time unchanged by this round).

**Built (the in-raid glance, 2026-10-19)**: the plate the reader round's reading was for. From its panel the owner took
Variant 1 (the total large and the desk's cells, at the top of the map's column) "with the looted container's or body's
item layout mirrored", in place of the top five per slot. Binding: DESIGN.md §4, *The in-raid glance* (*Its look*,
*Built* and its *As built*), §2 ("In a raid": the tick's words), §8 ("Developer aids": `-Glance`, `glance`).

- **The plate** (`Controls.GlancePlate`, `Rules.GlancePlateRules`, `MainWindow.GlancePlate`): at the top of the map's
  column, under the notes, the cue's band (the ground at 90 % between amber hairlines); FROM YOUR SCREENSHOT, "LOOT
  1.87 M+ ₽" at 48 px with the total in amber, "ON YOU 1.01 M+ ₽ · 2 unsure"; then the LOOT pane's regions in reading
  order, each under its own head (BODY, TACTICAL RIG, POCKETS, BACKPACK; DRAWER), with the desk's own cells, which take
  no pointer there; a part not searched a closed region, "UNSEARCHED +"; "POCKETS · SEARCHING +", "· MORE BELOW +" for a
  cut grid. The player's own regions never drawn; with no LOOT pane, the head alone and "Your gear · nothing looted on
  this screen". Cells 40 to 80 DIP (the desk's sizes, the largest that fits), flowed into columns, about half the map's
  height at the most; what doesn't fit even at 40 is named in "ALSO · BACKPACK 2.89 M ₽". 6 s, then a 0.4 s fade (at
  once with the effects off); a newer reading replaces it at once; a reading waits while a cue is up, a cue ends the
  plate; the raid's end takes it; the floor picker steps aside while it stands.
- **The tick's words** (en, de): "Read your stash, gear and tasks from menu screenshots, and loot in a raid", "Stash,
  Gear, Dailies und Weeklies aus Menü-Screenshots lesen, im Raid auch Loot"; its note, help's privacy paragraph, the
  README, PRIVACY.md and CLAUDE.md's list (five things a picture is read for) say the raid's inventory screen.
- **Developer aids**: `Rules.GlanceExample` (1, a drawer with three items; 2, a body: slots, an UNSEARCHED rig,
  pockets being searched, a searched backpack), the script's `glance [<n>]`, `fake-raid.ps1 -Glance <n>` (`--glance`),
  the replay `tools\qa\glance.devscript` and `glance.md`. Tests: `GlancePlateRulesTests` (what is drawn, the head's
  words, the heads and states, the sizes and the cut, in English and German), `DevToolsTests`.
- **UI QA pass (2026-10-19):** FAIL 0/5/1 → F1 fixed (the cut rule; `shturmap-cli desk --raid --plate`), F3 the owner's
  (accepted, DESIGN.md), F6 fixed (`-Glance` a Scav raid; the layout check's `glance` view); F2 the looted body's rig with
  its head scrolled off is neither drawn nor named (the reader's rule: a region without its head is not read; for the
  reader's next round: infer the part from its place in the LOOT pane); F4 (a cue ending the plate, QUEST COMPLETE with
  the plate) and F5 (effects off, a frame-by-frame fade; one still at 6.1 s showed the plate evenly at about 80 %, gone
  at 7.6 s) not run, for the owner's test. Translator: the German heads' wrap fixed.
- **Open**: the owner's call on **motion in a raid** (the plate stands at once, as built, or comes in with the panel's
  D: the band's flash, the total counting up, the cells 50 ms apart); at 900×560 a body's regions have no room under the
  48 px title and are named in the ALSO line only (a smaller title there, or a taller plate, would draw its slots); the
  plate not yet pictured with a real raid's reading (the 22 screenshots: `shturmap-cli desk --raid` reads them, nothing
  renders their plate yet); the panel had the one-line notice while another cue is up, as built a reading waits for the
  cue (the coordinator's brief); the tour's chapter 1 ("Your stash, gear and tasks in a menu screenshot") doesn't name
  the raid's inventory screen; the senior translator's review of the new German words; the UI QA pass. The reader
  round's open points stand: a backpack whose heading scrolled out of view reads as a window of no name, a body's rig
  opened in a window attaches to nothing, the 2-letter labels ("PM", "PB") stay unread, a raid screenshot is opened
  twice, and `StashReaderTests`' 3 s budget fails now and then in full parallel runs.

The research, the data facts and the discussion page stay outside the repository:
`%USERPROFILE%\source\repos\Shturmap-price-view-research-2026-10-19.md`,
`%USERPROFILE%\source\repos\Shturmap-price-view-data-2026-10-19.md` and
`%USERPROFILE%\source\repos\Shturmap-price-view-discussion-2026-10-19.html`.

## 2026-10-19: a required weapon pictured as the assembled gun

**The ask** (owner, 2026-10-17): "When a weapon is required for a quest, it shows just the icon for the weapon. For
example, the Punisher - Part 1. This is technically the correct icon. However, weapons consist of parts and the icon is
always missing the other parts, like the stock or scope or grip and is barely recognisable as a weapon using that icon.
We need to find the best solution for that." tarkov.dev's icon of a gun's item is its receiver alone. Checked for the
panel (the app's PvE cache, six requests to tarkov.dev): presets are in the items file the app already downloads (497
of 5,480 items; 164 of 172 guns carry `properties.defaultPreset`), their icons follow every item's pattern (64×64, 0.9
to 1.7 KB); quests' kills name 198 weapons, 37 without a preset (knives, grenades, under-barrel launchers, already
whole); 40 are pictured, 35 with a preset. A panel of five, drawn 1:1 in the rail and the quest card:

- A. The default preset's icon, the gun as the traders sell it, in the same square cell (recommended).
- B. A drawn glyph per weapon class and the short name in the cell's corner (a glyph per class; DESIGN.md §3, "Icons").
- C. The bare icon with the class's name under it (the receiver stays).
- D. Today's cells, the preset only on the item card, at 60 DIP.
- E. A in a wider cell, 72×40 with the 512 px picture.

The owner chose A ("Your recommendation", 2026-10-19). The terms the to-do asked to check: a preset's icon is an item
icon from the same service, fetched at runtime and kept like every other (DESIGN.md §3's accepted terms), so no new
conflict, as the panel read it.

**Built (2026-10-19).** Binding: DESIGN.md §3 ("Trader portraits and item icons": a gun is pictured by its default
preset), §4 ("What kills and exits take", "to use": its cell) and §8 ("Item sources": what is read).

- Data: `ApiItem.Properties` (`ApiItemProperties.DefaultPreset`, from the items payload the item sources already
  download; presets stay out of the class counts) and `ItemSources.PictureId`: a gun's preset, else the item itself
  (`ItemSources.PictureOf`: the item itself while the sources haven't loaded). It lives on the item sources, not on
  `GameData`: the items payload (17 MB) loads after the rest, so a gun shows its own icon for the second or two before
  (from the cache), as a class row says its weapons as a list until then.
- The cell: `Picture.PictureId` tries the preset's icon, then the gun's own, then the glyph; `GameArt` unchanged. Set
  on BRING's rows (`RequirementLine`) and need cells (`NeedChip`, the operational hand-over cells too), the kit cue, the
  rail's hand-over cell (`ObjectiveItem`), the quest card's objective and BRING cells (`CardObjective`, `CardNeed`) and
  the item card (`ItemCardView`); links, cards and texts keep the gun's id. No new text.
- The `bring` audit's PRESETS line and two flags, GUN WITHOUT PRESET and PRESET NOT AN ITEM (PvE today: 34 weapons
  pictured in BRING, 31 by a preset; bare: a bayonet and two under-barrel launchers; both flags none); UPDATES.md: the
  assumption's row and the audit's.
- Tests: `WeaponPictureTests` in Data.Tests (3: the parse, the mapping, presets out of the classes) and Session.Tests
  (3: the quest card's cells, a cell before the sources load and a knife's and a medkit's, the item card).
- Seen (a developer script: The Punisher - Part 1 started and picked, Customs in Plan, its card held): BRING's 40 DIP
  cell and the quest card's 34 and 30 DIP cells show the assembled AKS-74UN, stock and magazine; the PICKED row's 18
  DIP need cell is a small dark shape, as the panel drew it.

**Open.**

- The UI QA pass (the coordinator's, after this).
- `shturmap-cli render` draws the map only, no cells, so it had nothing to change.
- A long rifle letterboxes to a line in the 18 DIP cell; E is the step if that bothers.


## 2026-10-19: operational tasks, dailies and weeklies, in the rail

**The ask** (owner, 2026-10-17): "Add dailies and weeklies via screenshot." The game's daily and weekly operational
tasks are in neither its logs nor tarkov.dev's data; the game lists them on Character › TASKS › OPERATIONAL. The spike
of 2026-10-19 (`shturmap-cli tasks`, on the owner's six screenshots of that day, 2560×1440, PvE, kept in the ignored
`tests\fixtures\ocr\operational` with hand-written truths) read that table from the picture's left two thirds: the
screen 6 of 6, rows, names, locations and statuses 12 of 12, an expanded row's objectives and counts 4 of 4; the digits
(Windows' recognition drops most of the game's small orange countdowns and percentages, whatever is done to them) by a
14-way vote per band: the countdowns 12 of 12, the percentages 7 of 12, none wrong. The expiry is the file's time plus
the time left, so it needs the capture time to the second. A panel of four (rows drawn at the rail's 351 DIP):

- A. One line on the map's card, in the PROGRESS note's muted capitals.
- B. OPERATIONAL rows where the map's work is: framed glyphs on NEXT RAID's rows, a section in the open card and the
  raid card (recommended).
- C. A block of its own at the rail's top, every task at once.
- D. B, counted as work by the planner, the hand-over items in BRING and the kit, and a cue for a reading.

The owner chose B as drawn ("Use B, the recommended one"), which settled its three questions as drawn: any-map tasks in
every open card (after a hairline), the planner's order untouched, a reading's feedback by glow and notice, no cue.

**Built (2026-10-19).** Binding: DESIGN.md §2 ("Menu screenshots": the OPERATIONAL table's paragraph and the player's
say), §4 (principle 7, principle 11: no cue; "Screen anatomy", *Operational tasks*; *Visual language*, the framed
glyph; "Quest cards": no card), §7 (the any-map exception), §8 ("The operational tasks file", "Menu screenshots" in "How
the parts work", the script verb and the snapshot flags, the diagnostics) and §9.

- The reading moved out of the CLI into the library beside the stash's (`Shturmap.Game.Screenshots`: `TaskTable`,
  `TasksReader`, `TaskTruth` for measuring); `shturmap-cli tasks` reads with it and prints whether each countdown
  passes the app's checks. One menu screenshot's picture is opened once for both readings (`MenuPicture`,
  `MenuScreenshotReader`): the tasks reader first, and a picture of the Tasks screen goes to it alone (a trader's tab,
  now told by two words, TRADING with SERVICES or REPLACE with TASKS, to neither); every other one to `StashReader` as
  before. The session's path is the stash's (`GameSession.ReadStashAsync`, the menus only, the one tick), its taking
  `GameSession.Operational`: the location matched to a map of the data (a variant's base map; "Any location" none; an
  unknown location left out, counted in the app log), the notice "Operational tasks read: 3.", the file, a watch that
  drops a task as it runs out and publishes each minute while tasks are kept.
- The checks and the set (`Shturmap.Core.Operational`): `OperationalChecks` (a countdown within 0 and 7 days; rows
  within a minute of each other agree to 2 s, else the one more ways read keeps its own, a tie neither; one task's expiry
  from two screenshots within 5 s, else none; progress: the percentage when it passes, else the counts), the ledger
  `OperationalLedger` (per mode; a newer table replaces the set, a folded row keeps the objectives read before, a
  failing countdown keeps an earlier screenshot's expiry; ended tasks said until the next table, a day at most;
  `operational.json`), the words `OperationalWords` (kinds from the objective's words, or a folded row's name; the
  synopses, CoreTexts `Operational*`).
- The rail (`Rules.OperationalRail`, `MainWindow.Operational`, the templates `OperationalGlyphTemplate` and
  `OperationalTemplate`): a row's framed glyphs after its quests', a map with only operational work at the list's end
  without counts, the open card's OPERATIONAL with "As of", the ended line, the rows and the any-map ones after a
  hairline, the raid card's (ANYWHERE, "ENDS IN 25 MIN" in red only before the raid's end), a Scav's hand-over items
  under FIND IN RAID; the rows a reading brought glow (`Controls.Glow`). Texts: RuleTexts `Operational*`, ViewTexts
  `CardOperational`, `OperationalGlyphTip`, AppTexts `ScavLootOperational`, SessionTexts `OperationalRead`; the tick's
  words widened, "Read your stash and tasks from menu screenshots" (German „Stash, Dailies und Weeklies aus
  Menü-Screenshots lesen“), its note and help's HelpPrivacy; tour chapter 1's line; README, PRIVACY.md, UPDATES.md (two
  rows of what the reading assumes), the German glossary (proposed terms). English and German, for the senior
  translator's review.
- For the QA pass: the script verb `tasks [<n>] [ended]` and, with a fake game, `--operational <n>`
  (`tools\fake-raid.ps1 -Operational 5 -OperationalEnded`): made-up tasks from where the reading hands its rows over
  on; the file isn't written that run.
- Tests: `OperationalTasksTests` (31 cases: the checks, the set, the objectives, the expiries, the ended line, the
  file, kinds and synopses), `OperationalRailTests` (11: the row's glyphs, the section, Plan's and the raid's lines,
  the time-left steps, "As of" and the ended line), `OperationalSessionTests` (4: the table onto its maps, the file,
  the notice and a restart; a trader's tab to neither reading; unticked; made-up tasks never in the file),
  `TaskTableTests` (moved; one more: a lone trader's word is no trader's screen; the real-picture test passes on the
  owner's six screenshots), `DevToolsTests` (the `tasks` step).

**Changed (2026-10-19, after the dev build)**, which read the owner's Tasks screenshots: "I think they should be sorted
on top of the list always as they are time-sensitive. Also they should get the same animation as the other quests. Be
mindful of multiple missions started at the same time, we might want to have them in one animation included."
Binding: DESIGN.md §1 (the fifth exception), §4 (principle 2, principle 11's DAILIES AND WEEKLIES, "Motion", "Screen
anatomy", *Operational tasks*, the picks' rail), §8 (the script verb, `-Operational`, how the parts work) and §9.

1. **On top.** OPERATIONAL is the first section of the open map's card and of the raid card, before PICKED,
   COMPLETE and PROGRESS (the owner's "always": above the picks too); within it, and on a NEXT RAID row, the task
   that ends soonest first (one without a countdown last, the ones ending in the same minute in the table's order;
   `OperationalRail.Soonest`). A NEXT RAID row's framed glyphs stay after its quests' glyphs: the row compares the
   maps' quest work glyph for glyph in the planner's order, and a glyph says no time. The planner's order and score
   are untouched (the panel's B).
2. **A cue, like the quests'.** DAILIES AND WEEKLIES (`MainWindow.TasksCue`, `Rules.OperationalCueWords`; the
   session's `CueKind.TasksRead`): "FROM YOUR SCREENSHOT" over "3 DAILIES AND WEEKLIES" („3 DAILIES UND WEEKLIES“;
   one: "1 DAILY OR WEEKLY", „1 DAILY ODER WEEKLY“), QUEST COMPLETE's stamp with the stopwatch, and a row per task
   in the roll call's row style, rising in as its rows do: the framed glyph, the synopsis, the map or ANY MAP and
   the time left in muted capitals, soonest first, six at most. It stays 5 s after the last reading joined, 12 s at
   most, and steps back to the old notice while a replay, QUEST COMPLETE, SEEN IN YOUR STASH or the tour's cue is up
   (SEEN IN YOUR STASH now steps back to its notice while this cue is up). The rail's glow is its after-effect, now
   only for the rows a reading brought or changed (a row's key is the task and what was read of it, no longer the
   reading's time). Texts: RuleTexts `OperationalCueEyebrow`, `OperationalCueTitle` (en and de, for the senior
   translator's review); SessionTexts `OperationalRead` is the stand-in notice now.
3. **Several at once** (`OperationalReadings`): screenshots taken within a minute of the one before are one reading;
   its first that brings a new task calls the cue with every task the run read, a later one joins the cue while it
   is up and calls none after it (the table, then a row expanded: its objectives, no second cue). A reading that brings
   no new task calls none: the same set again, a row's objectives, a task handed in; "As of" says it.

- Dev aids: `tasks` and `--operational` call the cue (a snapshot keeps it up); a second `tasks` in a run counts its
  countdowns down from the first, so it is the same table again. `tools\qa\operational-feedback` has the still `cue`
  (the cue whole), between t0 and t1.
- Real pictures: three more of the owner's (the same table seconds apart, each with another row expanded) with
  hand-written truths in the ignored `tests\fixtures\ocr\operational`: read 9 of 9 rows, names, locations, statuses
  and countdowns, 4 of 4 objectives and counts, progresses 5 of 9 read and none wrong; the real-picture test passes on
  nine.
- Tests: `OperationalCueTests` (13: the run of screenshots, the same set, a new run after a minute, two tasks of one name
  and map, the title's plural forms in English and German, a row's line, the rows' order, the sections' order in
  MainWindow.xaml), `OperationalRailTests` (one more: soonest first), `OperationalSessionTests` (the cue in place of
  the notice; a run of three screenshots, one cue and one join; made-up tasks call it).

**Changed (owner, 2026-10-19, on the build: "Ordering - under picked."):** OPERATIONAL stands under PICKED and above
COMPLETE in the open card and the raid card; the build put it above PICKED; the code change follows in the fix round.

**Open.**

- **The UI QA pass on the cue (2026-10-19, on b549b08) and its fixes.** One blocker: F1, at 900×560 under a notice
  (the tour's, the stash's) the title stood whole on two lines while the six rows shrank to about 36 % (synopses some
  5 px), unreadable. Four more: F2, without a notice the cue wasn't fitted above the map's foot, its bottom rule through
  WIKI MAP and under the "−" button; F3, `tasks 5 ended` and `-Operational 5 -OperationalEnded` gave six tasks at most,
  so "+N" couldn't be reached; F4, the rows' rise can't be checked frame by frame; F5, a task that ran out while the cue
  was up still said "WOODS · 1 MIN LEFT" beside the rail's "1 ended at …". Fixed:
  - F1, F2: the whole cue keeps to its room as the roll call's (`MainWindow.FitTasksCue`): 8 DIP under a notice, 8
    above WIKI MAP and the credit, its rows smaller down to 60 %, past that the whole cue, title included; its gold
    rules clear of the map's buttons, as its rows. At 900×560 under the tour's notice it stands at about 71 % in English
    and German, title and rows alike; without a notice the title at its size and the rows at about 68 %. A notice
    that comes while it is up has it fitted again; one that goes leaves it where it stands.
  - F3: `tasks <n>` and `-Operational <n>` take up to 8 (a survival on Interchange, a kill on Lighthouse, a weekly's
    kill on Reserve after the five). Eight give five rows and "+3", seven "+2": six places, the last "+N", as DESIGN.md
    has it (the pass's wish, six rows and "+2" for eight, would be a seventh place). `OperationalCueTests` hold the
    places and the "+N" words; `tools\qa\operational-feedback.md` names the snapshot that pictures it.
  - F4: accepted as unchecked, one line in docs/QA.md ("Replay scripts").
  - F5, the coordinator's rule: the rows are a reading, not a clock; a row whose task runs out while the cue is up says
    ENDED („ABGELAUFEN“) in place of its time left, the others keep the time read (`OperationalCueWords.Line`,
    `MainWindow.WatchTasksEnds`; tested, and pictured by `-Operational 5 -OperationalEnded`, whose snapshot keeps the
    cue up past the task's end).
  - With them: OPERATIONAL under PICKED, above COMPLETE, in the open card and the raid card (owner, 2026-10-19:
    "Ordering - under picked."; `OperationalSectionOrderTests`); and a number never parted from its unit: the English
    rule texts keep a no-break space between them as the German do ("8 H LEFT", "24 MIN LEFT", "2 D LEFT", "READ 3 H
    AGO", "ENDS IN 25 MIN", the raid time's and the position's age), German „vor 3 Tagen gelesen“ too (translator,
    2026-10-19).
- **For the owner: the fold.** At 900×560 the OPERATIONAL section is below the fold whether it stands first in the card
  or under PICKED: the rail scrolls, and the open card starts under NEXT RAID's rows (four in the replay), so its
  sections begin at the window's foot either way. Under PICKED it moves down by the picks' rows only.
- To re-check: a QA pass on F1, F2, F3, F5 and the order; the senior translator's look at `OperationalCueEnded`
  („abgelaufen“) and the English no-break spaces.
- Found on the way, fixed (2026-10-19): an exit within 1.6 s of any cue (this one, the roll call, QUEST COMPLETE) ended
  with 0xC000027B in CoreMessagingXP in about 3 runs of 4: a timer of the window (the title's decode, the roll call's
  frames) ticked after the exit had taken the XAML down. Each now does nothing once `StopForExit` ran (`WindowTimer`).

- **The UI QA pass (2026-10-19, on ea0cb16) and the senior translator's review (2a75488).** The pass found one blocker
  and four nits; fixed after it: F1, with the app in German the catalogue's German names matched no hand-over item read
  from the English game (no cells, nothing under a Scav's FIND IN RAID): now matched against the items' English names
  (`GameData.EnglishItemNames`) whatever the app's language, the cell in the app's language; F2, the raid card's
  hand-over row said "ANY MAP" beside ANYWHERE and broke "HAND OVER AFTER THE / RAID": no "any map" there, a row's
  muted line breaks only between its parts, a two-word item stays whole; F3, Plan said 24 and the raid card 25 minutes
  at the same moment: both round down; F5, the layout check took a glow's gold copy for text over text: named
  `GlowCopy`, a layer on purpose (the marks' copies too); the translator's date, "18 Okt." for „18. Okt.“:
  `UiLanguage.DayMonth`. F4 (German synopses keep the game's "PMC operatives") stays: the synopsis keeps a kill's target
  as read and has no table of targets to translate. The pass's replay is `tools\qa\operational-feedback`. To re-check:
  a QA pass on the fixes.
- That a Scav's raid counts no operational kill or survival is the panel's assumption, written into DESIGN.md as one:
  to confirm in the game (a Scav raid with a kill daily open).
- The German game (its header words and tab names) and other resolutions than 2560×1440 are unread; the reading is
  English-only, so a German game reads no table.
- More rows than fit (scrolling) and a weekly's countdown past a day ("hours as written") are still unverified.
- The ended line counts the mode's tasks that ran out, whatever their map, and stands in every card's section.

## 2026-10-19: two symbols on one spot drift apart

**The ask** (owner, 2026-10-17, with a screenshot of Woods kept outside the repository): "There is a bug when two items
are close to each other and their repel-lines are parallel they get expanded every time when the user pans or zooms the
camera." One of Woods' two Outskirts stood at the view's top left corner, outside the map, on a leader running across
the whole view to its place by the sniper zone, the other's leader under it.

**Built (2026-10-19).** Binding: DESIGN.md, "Map drawing" (*Settled once, and never further than 120 DIP from its
place*; *Set apart, on leaders*), §8 "Developer aids" (`frames`: `--scene plan`, `--sway`, `--toward`).

- The cause was the continued repel's bookkeeping, not its passes. Since 2026-10-15 a frame of a move goes on from the
  last placement, each symbol found by its marker's id (`RepelPlacement.From`), and tarkov.dev gives two extracts one
  id: Woods' Outskirts, a PMC's and a Scav's less than a metre apart (and its two UN Roadblocks). Both went on from the
  first one's placement at every frame: they started on one spot, pushed each other out along one line (the parallel
  leaders), and the first kept what it gained, 7.4 px a frame without end. Symbols with ids of their own on one spot
  settle (a probe of 3,300 frames of glides, notches and pans: two on one spot, two 1.5 m apart, three with quests
  round them; none grew). The placement now keys each symbol by its marker's id and kind, and by its turn among those
  where even both are shared (`MapRenderer.RepelKeys`).
- The bound the to-do asked for, kept as a backstop: no symbol stands further than 120 DIP from its place, centre to
  place (`MapRenderer.LeaderMost`). In `Repel` the passes hold every symbol within it after each pass, the search for a
  free spot looks no further, and where it finds no free spot within it, what still covers another is pushed only off
  its neighbours' bodies, within it, as a move's frames do (left covering, the next frame of a move pushed it up to
  22 px at once). 120 is twelve times `LeaderLeast`, about five times the least a symbol that leaves its place stands
  off it (21 to 23 DIP). A fresh layout of the whole of Customs at 1600 × 1000, the busiest view measured, sets none
  further than 110 px, so it holds nothing back there; it binds where the view packs a crowd tighter than its symbols
  fit, the whole map at the smallest window, where the search for a free spot sent a symbol coming into view up to
  360 px across the view. `frames customs --scene plan --sway 2 --viewport 500x481`: symbols further than 120 px off
  their places in 1,249 symbol-frames before (the farthest 360.8 px), none after; set off their places by more than
  3 px from one frame to the next 107 times before, 88 after (the largest change 4.3 px before, 5.9 after); no body
  over another in either. Customs' glide at 1600 × 1000 (none over 3 px, the farthest 110.1 px) and the roll call at
  the smallest window (359, 116.1 px) are the same as before.
- The frames tool: `--scene plan` (the map as Plan shows it between raids, every extract, no list), `--sway [n]` (n
  times back and forth, 4.5 s each: a glide in and out, three notches in and three out), `--toward <name>` (glide and
  turn the wheel at that symbol's place); every summary names the farthest a symbol stood off its place, and with
  `--sway` the farthest each time.
- Tests: `SharedSpotFramesTests` (4). Two extracts with one made-up id on one spot, the same 1.5 m apart, and the two
  with a transit on the spot, each with six quests round them, through six times back and forth (a glide in and out,
  three notches in and three out, a pan out and back; 1,657 frames): never more than 2.5 times a leader's least
  length off their places (53 px), never more than 3 px from one frame to the next, and from the second time on no
  further than the time before and ending where it left them. Eighty quests on one spot: none further than
  `LeaderMost` off its place. With the ids alone as keys the first three fail (120 px, held at the bound); without the
  bound the last (133 px). `GlideFramesTests` unchanged and green.
- Numbers, `shturmap-cli frames woods <out> --scene plan --sway 4 --toward Outskirts` (1,126 frames 16 ms apart,
  1600 × 1000): the farther Outskirts stood off its place, time by time back and forth, 1,434, 2,861, 4,272 and 5,699 px
  before; 21.2 px each time after. Symbols set off their places by more than 3 px from one frame to the next: 1,402 in
  836 frames before, 3 after (in 3 frames). The farthest any symbol stood off its place after: 32.8 px.

**Open.**

- Two extracts with one id are tarkov.dev's doing; worth adding to the report of its extract defects ("To do later").
  Elsewhere the app keys extracts by id too (the extract guide's end, `ExitsListed`): there two with one id take the
  first's place or both; not looked at further.
- Where the bound binds (the smallest window) a crowd's symbols stand closer: their collars may meet. The roll call at
  the smallest window has 359 symbols set off their places by more than 3 px from one frame to the next (DESIGN.md's
  measure of 2026-10-15 says 127): the same before and after this change, so something since then; not looked into.
- No UI QA pass was run for this change (the coordinator's call); the two frames looked at (Woods, frame 059 of the
  sway, before and after) show the pair on parallel leaders across the view before and on short leaders either side of
  their spot after.

## 2026-10-17: the linked highlight breathes

**The ask** (owner, 2026-10-17): the linked highlight "really helps pre-attentively highlight elements … However, the
pre-attentiveness goes away directly after the highlight was done … the highlights are a bit too faint". While a set was
lit (a trader's portrait, a boss's name, an item cell pointed at), its places rang with the pointer's thin rings, one
every 0.7 s and gone again; between two rings the lit places and the rest were the same gold, the rail's lit rows had
only the 18 % tint, and a held focus stood still at the same measures. A panel of four:

- A. A deeper static contrast.
- B. The lit breathe.
- C. One pulse at entry, then contrast.
- D. A static mark at the edge.

The owner chose B.

**Built (2026-10-17).** Binding: DESIGN.md, "Design system" (*Motion*), "Visual language" (the linked highlight, a
trader's portrait), "Screen anatomy" (the raid card's readout, *The trader strip*), "Map drawing" ("Stepping back").

- One clock, `Shturmap.Map.LinkedBreath`, owned by the map view (`MapView.Breath`) and shared with the rows
  (`Linked.Breath`), so the map and the rail breathe in phase: a 2 s cycle, an eased sine, scale 0.92 → 1.08 and
  opacity 0.85 → 1.0 and back (`LinkedBreath.At`), from 0.18 s after the set lit.
- Which focus is a set (`LinkFocus.IsSet`, `Focus.IsSet`, `MapScene.LinkedSet`): what an element names as several
  (`Linked.Quests`: a trader's portrait, a boss's name, a BRING row's quests) and an item (a need cell, a BRING row, a
  key's line, an item card's body), a boss's marker or a padlock on the map; never an objective. One quest, an
  objective, an extract and its switches stay one thing: they ring as before (`MapScene.Pulsing`).
- On the map every lit place of a set gets a halo under its symbol, a radial glow in its own colour, 2.8 times its
  radius at scale 1 (about 3 at its widest), 60 % in its middle at full opacity (`MapRenderer.DrawHalo`), and no rings;
  its chevron out of view breathes too. The rest steps back to 55 % (labels 40 %) while planning, 70 % (50 %) in a raid,
  every kind that steps back at all; the ways out and the bosses keep their measure (`MapRenderer.StepBackOf(inRaid,
  settle)`, `MeasuresAt`).
- In the rail, the rows a set lights with the full tint (not the portrait, the name or the cell pointed at) breathe
  their type glyph (`KindGlyph.Breathe`, on each frame while it runs, `CompositionTarget.Rendering`); item rows have no
  type glyph and only take the bar below.
- The stops, each settling over 0.18 s: a click that holds what is pointed at (the trader strip's portrait:
  `LinkedBreath.Hold` when the held quests are the pointer's set; a card held is no hold of the map's focus), the
  pointer still for 6 s (any move in the window counts, measured from the last move or the set's start, whichever is
  later; a move after the stop starts the breath again from the start of its curve), Windows' animation effects off
  and `--snapshot` runs (`MapView.StaticHighlight`), where it never starts.
- The static look: the rest at 40 % (labels 30 %) while planning, 60 % (45 %) in a raid; a 1.5 DIP ring of each lit
  symbol's own colour on its collar's outer edge (`DrawCollarRings`); a 3 DIP bar of the tint's gold at the left of each
  lit row (`Linked.Bar`, an absolute gradient over the row's fill). A held trader's focus always has it: their own rows
  between raids carry the bar (`Linked.Held`), the other rows step back to 30 % (45 % until then;
  `TraderFocus.SteppedBack`).
- Tests: `BreathTests` (21: the curve at 0, 0.5, 1, 1.5 and 2 s, the start after the ease, the same set and another, the
  held stop and its release, held from the start, the 6 s stop and the move after it, a move before it, the effects
  off, the four measures, the map's measures for one quest and for a set breathing, still and held, one quest ringing
  without a halo, the halo's frames 1 s apart breathing and the static look's, the held focus without motion);
  `LinkFocusTests` (a set and one thing).

**Open for the owner.**

- The rows' 40 % "while breathing": rows step back only under a held focus, which never breathes, so no row ever stands
  at 40 %; stepping the unlit rows back while a set is pointed at would be new behaviour, not built.
- Pointing at another trader's portrait while one is held breathes (it is the pointer's, not the hold); only the held
  trader's own portrait stands still.
- The Scav and sniper zones, locks, switches and hazard areas take the same set measures as other quests' markers
  (their own table row had 60 / 75 %).
- No pictures were taken: the dev build is the check.

## 2026-10-17: a trader focus: the strip

**The ask** (owner, 2026-10-17): "some players want to level a specific trader or want to work towards specific
unlocks of these traders. Currently there are no good ways in the app to account for that, you can only select by map
and not by trader. I at the same time want to keep the UI footprint lean." A panel of three:

- A. Pointing at the trader's portrait on a quest's row.
- B. A trader strip under the MAP list.
- C. Plan by trader in place of by map.

The owner chose B. The traders' unlocks and rewards are not built.

**Built (2026-10-17).** Binding: DESIGN.md, "Screen anatomy" (*The trader strip*, the update band, the raid card's
head), *Visual language* (the linked highlight: a trader's portrait is their active quests), "Map drawing" ("Stepping
back"), "Keyboard" (Esc) and §8's script verbs.

- The strip: one row under the MAP list, outside what scrolls, above the update band between raids and above the raid
  card's head in a raid (the head meets its body without a seam, so nothing may come between them). Per trader with an
  active quest, in tarkov.dev's trader order, the portrait (`Controls.Picture`, 20 DIP) and the count of their active
  quests in `FigureText` gold (`Rules.TraderFocus.Strip`, `MainWindow.Traders`). The count is every quest the log shows
  active for the trader, with work on a map or not. Tooltip "Ragman · 4 active quests · click to hold", held "… · click
  to let go".
- One row, never two (owner, after trying the dev build: "I like this, but I do not like that the layout has two rows
  where Ref falls exactly on the last line"; the first build's cells of at least 43 DIP wrapped a ninth trader). The
  cells share the rail's 351 DIP, (351 − 4 × (n − 1)) / n each; portrait and count scale down with them to 16 DIP and
  10 px, room kept for two digits, so the layout follows n alone; from ten traders the count stands on the portrait as
  a badge in the map's count badges' style and the cells are portraits only (`Rules.TraderStrip`, `TraderStripTests`
  for 1, 6, 8, 9, 10, 12 and 16). Measured with eight traders, Ref the last, and with ten, at 900×560.
- Pointing at a portrait puts the trader's active quests in the linked focus (`Linked.Quests`, as a boss's name does):
  their rows lit, their places pulsing, the rest of the map stepping back; pointing at one of those quests tints the
  portrait. It opens no card.
- A click holds it: the portrait framed in amber (1.5 DIP, `Picture.Held`); a click on another portrait moves it, one on
  the held one lets go, and so does Esc with no card open (the cards first: `TraderFocus.Escape`). Held, the MAP list's
  and NEXT RAID's counts are the trader's ("Ragman 2"; `PlanList.Count`); a row with none of them steps back to 45 %
  with no count, chosen over "Ragman 0". The other traders' quests (the map rows' glyphs, PICKED, COMPLETE, PROGRESS,
  ANY MAP) and the BRING rows that serve none of the trader's quests step back to 45 %; a pick never does. On the map
  `MapScene.Held` is drawn as the focus while nothing is pointed at, by the stepping-back table and without the pulse;
  the pointer goes first while it lasts.
- Held, NEXT RAID's rows go by the trader's quests on each map, most first, a tie keeping the planner's order, the maps
  with none of them last and stepped back (`PlanList.Ranked`; the coordinator's follow-up, the same day); let go, the
  planner's order is back. The MAP list keeps its order.
- In a raid only the held portrait shows, or nothing; the map lights the trader's places by the raid's measure; the raid
  card is unchanged.
- The focus is the run's and never saved: it stays through raids, a new quest list and a change of map, until let go
  or the app closes. A held trader with no active quest left keeps their portrait with "0".
- Texts: `TraderStripTip`, `TraderStripTipHeld` and `PlanRowTrader` (`RuleTexts`, English and German), for the senior
  translator's review.
- Snapshots: `--trader-focus <name>` (`tools\fake-raid.ps1 -TraderFocus Ragman`); the script verb `trader [<name>]`.
- Tests: `TraderFocusTests` (13: the strip's traders, order and counts, the held trader at 0, the raid's strip, the
  tooltip's plurals and its click, hold, move, release, Esc, what steps back, the map's quests, `PlanList.ByTrader`,
  `Count` and `Ranked`),
  `HeldFocusTests` (lit without a pulse in Plan and in a raid, the pointer first and back, the fade out, nothing on this
  map, picks).
- The tour: chapter 3 frames NEXT RAID's rows, and the strip lies under the shade; its words still hold.

**Open for the owner.**

- The count is every active quest of the trader, also those with nothing on a map; counting only those Plan has rows
  for would match the rows' counts.
- The BRING rows that serve none of the trader's quests step back as well (the ask named the quests), and Esc closes
  the cards before it lets go of the focus.
- The MAP list keeps the planner's order under a focus, while NEXT RAID's rows re-rank.
- What's New's line comes with the release.

## 2026-10-17: boss markers stepped back until they matter

**The ask** (owner, 2026-10-17): "I feel like the red dots for enemy bosses are too distracting when trying to parse
the quest icons. Come up with a better solution." Every boss and AI squad zone was a solid red octagon at full strength,
each zone's largest group labelled ("Killa 75% · 20% here"): on Interchange 7 octagons, 5 of them labelled, for two
bosses, Killa's four zones spread through the mall among the quests. A panel of four:

- Today: as it is.
- A. Stepped back until it matters: hollow and quiet at rest, one label per boss, solid while it matters.
- B. One symbol per boss, its zones as rings.
- C. No bosses in Plan.

The owner chose A.

**Built (2026-10-17).** Binding: DESIGN.md, "Map drawing" (*Spawns*, *Stepped back until it matters*; the strength
table, the levels, the label priority).

- At rest, in Plan and in a raid: every zone keeps its place and octagon, drawn hollow, a 1.5 DIP outline in `Red` on
  its dark collar as before, the whole at 70 % with its floor badge (`MapRenderer.BossRestAlpha`, `BossRestStroke`,
  `ShownMarker.Resting`; one small layer per resting octagon).
- One label per boss, on its likeliest zone (the largest share, the data's first on a tie), its name and chance alone
  in 11 px letter-spaced caps in `Muted`, the map's own names' style: "KILLA 75%" (`MapMarker.RestLabel`, made in
  `MapContentBuilder.SpawnZones`). Interchange's boss octagons now carry 2 labels (Killa at his 40 % zone, Tagilla),
  Streets' 2 (Kollontay at Klimov, his two zones a tie; Kaban), where they carried 5 and 3.
- Lit, every zone solid with its full label as before: while the boss is pointed at (in the raid line, under the map's
  name in Plan, or one of its zones on the map), and while a picked, pointed-at or held quest has an open kill
  objective whose targets name the boss's id or name (`MapContentBuilder.QuestBosses`, `MapScene.QuestBosses`,
  `KeptBosses`; brought into the focus as a quest's doors are). A pick steps nothing back and leaves the names in ink;
  pointed at or held, the names take the red. An objective for any boss lights them all; a guard's
  (`followerBully`) doesn't light its boss.
- The legend's boss row shows the resting look. `shturmap-cli render <map> <out.png> --pick "<quest>"` draws a pick's
  bosses lit.

**Open for the owner.** The legend's words ("red octagon, with the chance (and this zone's share)") still hold and
were left as they are; whether a guard's kill objective lights its boss; at rest the name reads like a place name
("KABAN 75%" by "CONSTRUCTION" on Streets), told apart by its chance and its octagon.

## 2026-10-17: exit facts and the data rules

**The ask** (owner, 2026-10-17, on the coordinator's recommendation after the exits verification): "exit facts" as
words on the exit's Raid rail row, nothing new on the map; and general rules for the data's defects, with the report
to tarkov.dev itself left to do (below, "To do later").

**Built (2026-10-17).** Binding: DESIGN.md, "Extract requirements" (the table's rows, *Gold is what to bring or do,
muted what is so of the exit*, *The facts are kept by hand*, *The data's defects, by general rules*).

- A hand-kept table beside `SwitchExits`, `Data.TarkovDev.ExitFacts`: 30 rows by map, internal name and id, as none of
  these facts is in tarkov.dev's data. In gold under the exit: the key at its door with the key's icon, and the key in
  BRING ("Factory emergency exit key for the door": Factory's and Night Factory's Cellars and Med Tent Gate, Customs'
  ZB-1013, Woods' ZB-014, Interchange's Saferoom, each with its door's lock id); "No backpack" (Reserve's Sewer
  Manhole, The Lab's Ventilation Shaft); "· single use · up to 4 players" after a car's fare (Shoreline's 5), "· single
  use" for the Saferoom and The Lab's Cargo Elevator.
- Muted under that: "may be closed" on the 13 exits the wiki marks "not always available", until the game's list was
  read this raid; the times: "Train from 16 min left · stays 7 min" (Reserve; Lighthouse's from 20), "from 15 min in"
  (The Way Up), "open for 4 min after the lever" (Bunker Hermetic Door), "~80 s after the pump" (Sewage Conduit). The
  trains' and The Way Up's lines follow the raid card's clock and step back to 45 % once the window has passed; no cue.
- The rules for the data's defects, in the loader (`Data.TarkovDev.ExtractRepairs`), each a no-op on right data: a copy
  of another map's extract outside the map is dropped (Ground Zero's three); a variant's extract without a side takes
  its base map's (Night Factory's nine, Ground Zero 21+'s six); a variant without extracts takes its base map's, with
  its switches (The Lab (Dark) The Lab's seven and fifteen); the Customs rail is a PMC's by hand (a row of `ExitFacts`).
  A variant's base is the map maps.json lists it under (`altMaps`), and borrowed extracts keep their ids, so the tables
  find them under the base's name: The Lab (Dark)'s elevators carry The Lab's switch chains, badges and facts.
- `shturmap-cli bring` prints EXIT FACTS against the data and a line per rule with what it did; `ExtractRulesTests`
  against the cache fails on a stale row and warns where the data carries a fact or a rule did less (a fix upstream);
  `ExtractRepairsTests` hold the rules on made-up maps.

**Chosen where the ask left room.** The bounds alone would have dropped six real extracts 1.5 to 16 m past their map's
edge (Courtyard Gate on Factory and Night Factory, Railroad Passage, RUAF Gate, Path to River, Icebreaker's
helicopter), and Ground Zero's Pinewood Basement is only 15 m out, so no margin separates them: a dropped extract must
also have the internal name of another map's extract. Units on numbers ("stays 7 min", "up to 4 players"; DESIGN.md,
*Words*). Ground Zero 21+'s Police Cordon V-Ex has a row of its own, as Night Factory's doors do. The glance's EXIT
keeps its gold line only. On the coordinator's follow-up the same day: a variant borrows only from the map tarkov.dev's
maps.json draws it with (`altMaps`), so Ground Zero Tutorial, empty in the data too, takes nothing (the app draws it
with Ground Zero's artwork, but only by its name's start, which nothing in the data backs); and The Lab (Dark)'s
borrowed extracts carry The Lab's switch lines, badges and facts, with The Lab's switches (the same layout with the
lights off; a row can be dropped for it if its power differs in the game). The texts after the translator's review:
"Zug bei 16 min Restzeit · bleibt 7 min", "open for 4 min after the lever".

**Deliberately left out.** Lighthouse's V-Ex, Interchange's Smugglers' Tunnel and Terminal's boat: no position in the
data, and placing them by hand goes against the data-only rule. Where Mira Ave's green flare spawns (a Scav body by
the yellow bus, the wiki says). Raiders at The Lab's and Reserve's switches and the train. A cue when a train comes
(principle 11). The per-player fare and the fare's Scav-karma discount.

**Open for the owner.** Whether The Lab (Dark)'s power and elevators work as The Lab's: in a raid there, by eye.

## 2026-10-17: switch-gated exits on the map

**The ask** (owner, 2026-10-17): "Some exits still require specific actions, for example one exit in interchange
requires the power to be switched on. This is currently not represented in the map at all and should be added as
information." A panel of three:

- A. A badge on the exit, the link on pointing (recommended).
- B. Words on the exit's label.
- C. The switch as part of the exit: in the exit's colour, joined to it by a dashed line at all times.

The owner chose A.

**What the verification of 2026-10-17 found.** tarkov.dev lists `switches` on Customs (all 28 extracts, the one ZB-013
lever) and The Lab (all 7, the Med Elevator's three buttons) only, so every entry fell under the "most extracts" rule,
and the name check meant to rescue the right one never matched ("ZB-013" against "ZB-1013", "Med Elevator" against
"Medical Block Elevator"): no extract ever showed a switch line or lit a switch, on any map, in either mode.
Interchange's Saferoom and Reserve's D-2 and bunker list none, though the data places their switches. The same check
found that the flare exits take a green flare, not the red the rules said, and that Icebreaker's helicopter is one too
(owner, the same day: "Fix").

**Built (2026-10-17).** Binding: DESIGN.md, "Extract requirements", "Map drawing" (*Switches*, *Distances on the map*,
the roll call's rows).

- A hand-kept table of the ten gated extracts (`Data.TarkovDev.SwitchExits`, below the map's code and the rules, which
  both read it): Customs' ZB-1013; Interchange's Saferoom Exfil (Mall Main Power Switch, Saferoom Exfil Unlock Switch,
  the Object #11SR keycard's lock, Saferoom Exfil Switch); Reserve's D-2 (power, door) and Bunker Hermetic Door; The
  Lab's three elevators (power, call, extract), Hangar Gate, Parking Gate and Sewage Conduit (pump, water level). By
  map and internal name, for that id only; the steps' ids in the order of use. The data's own `switches` still count
  where they tell an extract apart (none today); the name check is gone.
- The line under the exit in the Raid rail: "ZB-013 Power Switch first", "Mall Main Power Switch first · 4 steps".
- The badge: a dark disc ⌀ 14 DIP with a 1.5 rim at the extract's lower left (the floor arrow's size), the power symbol
  in the extract's colour, at rest at every zoom; the roll call's row of such an extract carries it before the name.
  Names keep off it and move out past it; in The Lab, where the floor arrow takes the right, they stand left of it.
- Pointed at (its symbol, its row): every step lights in the extract's colour at any zoom, named for it ("Mall Main
  Power Switch · Saferoom Exfil"), and the guide line runs from the extract to the first step with the distance (430 m
  at the Saferoom). The steps' names come after the extract's and give way where there is no room.
- The legend has a row for the badge. `shturmap-cli render <map> <out.png> --point-exit "<extract>"` draws the pointed
  state; `bring` prints SWITCH EXITS and FLARE EXITS against the data (both "as the data has them" on 2026-10-17), and
  `ExtractRulesTests` against the cache fails on a stale row and warns where the data names switches of its own.
- The flare exits say "Fire a green signal flare there", and BRING's row brings the RSP-30 green; Icebreaker's
  helicopter says it before its fare, by internal name and id.

**Open for the owner.** The roll call's rows wear the badge but can't be pointed at: the cue lets the pointer through
to the map, and its rows leave as their icons fly. The steps' names give way in a crowd: at 3× the Saferoom's last two
steps, within 5 m of its door, show lit and unnamed, and at the whole map so does D-2's power switch in the bunker.
Only the Saferoom's keycard is a step; the keys at other exit doors (ZB-1013's, Factory's emergency exit key) are not
in the table. The verification's other findings (exits not always open, timed windows, no-backpack exits, wrong
factions in the data) are not built. (Built the same day, keys at the doors too: "Exit facts and the data rules",
above.)

## 2026-10-16: the other-floor arrow, bolder

**The ask** (owner, 2026-10-16): "One of the beta testers said that the icon indicator if a location is above or below
the current position is hard to read on smaller second screens." Measured in `MapRenderer.DrawFloorArrow`: a dark disc
⌀ 11 DIP with a 1.2 rim in the marker's colour holding a filled chevron of about 6.4 × 4.7 DIP; two or more floors away
an 18 × 13 plate with a chevron of 5.6 × 4.1 and a 9.5 px digit. At 100 %, as on a small second screen, a DIP is a
pixel, and a filled triangle of 6 × 5 px runs into a blob whose up and down differ only in which edge is flat. A panel
of five:

- A. The same shape, larger.
- B. A bold caret, a stroked chevron, in a larger disc (recommended).
- C. B, and the floor in words on a lit name.
- D. Offset outlines of the symbol instead of a badge.
- E. A Normal or Large setting for the symbols' size.

The owner chose B ("Go ahead with your recommendation").

**Built (2026-10-16).** Binding: DESIGN.md, *Visual language* (the other-floor paragraph) and "Map drawing" (the
repel).

- The caret: a line through (−3.4, +1.4), (0, −1.4) and (+3.4, +1.4) DIP from the disc's centre for up, mirrored for
  down, stroked 2.2 DIP with round caps and joins in the marker's colour, about 9 × 5 DIP outside, in a disc ⌀ 14 with
  a 1.5 rim. Two or more floors away a 22 × 15 plate, the caret 6.75 DIP in from its left end and an 11 px bold digit
  5.25 in from its right; the plate's box (left c.x − 6.75, top c.y − 7.5) puts its caret where the disc's centre is.
  The sizes are named constants (`MapRenderer.FloorDisc` …), and `FloorTests` hold them at 200 %.
- The badge stands 1 DIP further out each way, so the larger disc covers no more of its own symbol. Labels keep clear
  of it as before (its box, the disc's square ± 7.5 DIP, is among what they avoid); an extract one floor away now
  loses the place right of it for its name, which the disc reaches into, and the name goes left: in The Lab, where
  every extract is on another floor than the ground, all five names moved to the left. A transit's, a boss's and a
  sniper zone's had gone left before.
- The repel counts a floor arrow 6 DIP wider than its symbol (3 before, as the count, OPT and "?" still are): along
  the diagonal the badge reaches 3.1 DIP further than it did, so a neighbour set as close as the repel allows comes no
  further into it. On `shturmap-cli render interchange` at the whole map the mall's crowd opens a little more and the
  picked Minute of Fame stands off on a leader; at 3× a few symbols move a few pixels. Kept at 3, the larger badges
  lie partly under their neighbours' (there, the pick's plate over Killa's disc).
- The legend's swatch is drawn by the same code; its two quests are drawn at 0.8 of their size instead of 0.85, so the
  larger badges clear the second quest and the swatch's edge.

**Left for later.** E, a Normal or Large setting for every symbol, should testers report other small things; C, the
floor in words on a lit name, as a possible addition for picks. Open for the owner: the repel's 6 DIP against 3 (the
renders of both, with the UI QA pass), and the extracts' names on the left in The Lab.

**QA pass, 2026-10-16** (a targeted UI QA pass on 1193e67, the day's two changes; not a full one): 0 blocks, 4 should,
3 nits. Fixed in the commit that adds this paragraph:

- The roll call's replay took t1 too early at the smallest window: 3.60 s after the list (3.66 in German), with
  Railroad Passage still unlit and ZB-1013 in flight, as a snapshot saves in about 0.3 s at 900×560 against about 1.0 s
  at the default window. Its wait is 2.2 s (was 1.5), for about 4.3 s at 900×560 and 5.0 s at the default window
  (*the fly-out slower*, below).
- At 3× on Interchange the picked "Minute of Fame" started 0 to 1 px from its ⌃2 plate's rim and read "⌃2 Minute of
  Fame" (about 6 px at 1551313); the name's halo cut into the plate's corner. Names keep 3 DIP off a floor badge now
  (`MapRenderer.FloorBadgeClear`, in the boxes labels avoid), and a name beside its own marker's badge moves out along
  its side by what it lacks (`OffFloorBadge`, as a name after a tab does): the margin in those boxes alone sent every
  name beside a one-floor disc to its left and left the pick, every other side of it taken, where it was. On `render
  interchange` with the position at 3×, 3 px of clear ground between the plate's rim and the "M", the names beside
  one-floor discs 1 to 2 px further right; at the whole map one name moves, Inventory Files, from left of its quest to
  above it, clear of The Key to Success's disc. `FloorTests` hold it at 100 and 200 %; DESIGN.md, *Visual language*.
- The legend's OTHER FLOOR swatch: the two quests' collars ran into one another with no clear pixel between them, and
  the first quest's disc touched the second's collar. The quests at 0.75 of their size (was 0.8) and 23 DIP apart (was
  20): at 100 % 3 clear pixels between the collars, at least one between the disc and the collar, the plate's rim 2 px
  inside the swatch's edge.

Open for the owner:

- The CLI's `frames --scene whatsnew-extracts` only rings the listed extracts 0.16 s apart (`MapScene.RingExits`);
  since the roll call of 2026-10-15 the app's What's New `extracts` preview calls its list out with the roll call
  (`StageExtractList`, `RollCallExample`), so DESIGN.md §8 *Animations frame by frame* ("What's New's `extracts`
  preview as it is staged") no longer holds for that scene. The fix: the scene calls the example list out as
  `list-callout` does.
- At 900×560, What's New's `extracts` preview pointed at 2 s after the tour ended, while the notice "The tour stays in
  help: F1, then TAKE THE TOUR." is up: the cue stands lower (341 DIP of a room of 341 under 124) and the last "1" of
  "7 OF 11" runs under the map's locate button; at 1600×900 it is clear (DESIGN.md §4 *The cue*: the whole cue in its
  room, the title never cut, the rows clear of the map's buttons). Not from the day's commits.
- Interchange's whole-map view: the floor badge's repel room of 6 DIP (was 3) opens the mall's crowd and drops two
  names there, "Saferoom Exfil" and "Killa 75% · 20% here" (allowed by *Labels by priority*; both show at 3×). Whether
  6 stays is the owner's call.
- Not run in the targeted pass: `frames` list-read with `--glide`, `--notch`, `--step` and `--viewport 500x481`, the
  layout check, the other replay scripts, the preview in de-DE.

## 2026-10-16: the fly-out slower

**The ask** (owner, 2026-10-16): "The animation that 'flies' the exits to their position is too fast." Measured on
`shturmap-cli frames customs --scene list-callout` (a 1600 × 1000 view, eight rows): each icon flew a fixed 0.9 s
whatever the distance, the farthest (Crossroads) 575 px at about 1,800 px/s at its peak measured frame to frame (first
reckoned at 1,300), the nearest (ZB-1013) 69 px in the same time; the lift-offs were 0.16 s apart, the rings' turn, so
all eight were in the air within 1.1 s and the fly-out was over 2 s after it began, while the view zoomed out until
2.4 s. Of three options (A, flights of 1.5 s, lift-offs 0.4 s apart; B, flights paced by their distance; C, one icon at
a time) the owner chose A. Nothing else of the roll call changes.

**Built (2026-10-16).** Binding: DESIGN.md, *Motion* and "Map drawing", *A list just read is called out*.

- `MapScene.FlightLength` 0.9 → 1.5 s, and the lift-offs have a turn of their own, `MapScene.CallOutTurn`, 0.4 s; they
  shared the rings' `MapRenderer.ExitTurn` (0.16 s), which a list after the cue still rings by. Lift-offs from 1.6 s
  as before, the view's fit from 1.2 s over 1.2 s, the title 1.5 s past the last landing. Six rows: the last lands at
  5.1 s, the title goes from 6.6 s, the rings end at about 7.2 s; eight: 5.9, 7.4 and 8.0 s; twelve: 7.5, 9.0, 9.6 s.
- In the Customs example the lift-offs are at +1600, +2000 … +4400 ms, each landing 1500 ms later and a ring start,
  the first after the view has settled; the farthest's peak is about 1,100 px/s, and the rings are over at +8019 ms.
- The window follows by itself: the title's hold and the rows' fades are worked out from the flights. The tour's
  chapter 5 waits for the cue's end (up to 15 s; twelve rows end at 9.6 s); What's New's `extracts` preview has no end
  of its own.
- `shturmap-cli frames --scene list-callout` draws 260 frames by default (to 8.55 s, past the eight rows' last ring).
  The replay `tools\qa\extract-roll-call.devscript` waits longer between its stills, so each still shows what its
  `.md` says at the new pace (about t0 0.6, t1 4.0, t2 2.5, t3 4.7, t4 7.5 s; reckoned, not yet measured).
- `ExitCallOutFramesTests` hold the design's 1.6, 0.4 and 1.5 s, and the rings' 0.16 s.
- Measured in the QA pass of 2026-10-16 (seconds after the list): at the default window t0 0.65, t2 2.52, t3 4.79, t4
  8.13; at 900×560 t0 0.65, t2 2.53, t3 4.31, t4 7.08; t1 came at 4.31 and 3.60, too early at the smaller, and now
  waits 2.2 s for about 4.3 and 5.0 (*the other-floor arrow, bolder*, "QA pass").

**Settled 2026-10-15, evening** (the owner, on a decision page of eight points, E1 to E8; the page stays outside the
repository):

- **E1.** The title rule, confirmed.
- **E2.** The floor names stay the data's English: the decision of 2026-10-11 stands (DESIGN.md §8, *The app's own
  language*: tarkov.dev has its maps' layer names in English only, and the game names no floors).
- **E3.** The label room, as proposed; another agent builds it.
- **E4.** + and −, the zoom buttons and a double-click zoom over 0.2 s, eased, keeping the view's middle or the point
  clicked in place and going on from a zoom under way, as the wheel's notches do; at once with Windows' animation
  effects off (DESIGN.md "Map drawing", *The keys, the buttons and a double-click zoom over 0.2 s*). Built 2026-10-15.
  At 60 frames a second a frame zooms by 5.2 % at most for + and −, 9 % for a double-click, under the tenth the steady
  placement holds to; **open**: below about 55 frames a second a double-click's middle frames pass it, below about 32
  those of + and −, and such a frame lays the symbols out afresh (at 30 a second a double-click 4 frames, + 2: more jumps
  than at once). A longer double-click (about 0.37 s holds it at 30 frames a second) or a flatter ease would cover it;
  the owner's call.
- **E5.** Twelve rows in the roll call, drawn smaller at the smallest window, accepted as drawn.
- **E6.** The feedback button's focus ring stays unpictured in developer builds.
- **E7.** The first raid's line in the RAID LOADING cue wraps clear of the map's buttons, as the other labels do: it
  breaks at its "·", each part whole on a line of its own, in English, German and the pseudo-language (DESIGN.md §4,
  "Screen anatomy", *The tour*, *First-time lines*). Built 2026-10-15.
- **E8.** The release goes once the re-check passes. **Re-checked 2026-10-16** (a targeted UI QA pass on c6618a2, not a
  full one; the owner's call of 2026-10-15): every fix of the third pass holds at 900×560 and 1600×900 in English and
  German (F1 to F6, F11, E3, E4, E7), nothing else changed, and the empty `settings.png` of 2026-10-15 no longer
  reproduces. 0.6.0 "Okami" goes out from this state the same day. Left open by the pass, none in a player's path:
  - `--snapshot` failed twice ("Snapshot failed: ArgumentException: Value does not fall within the expected range" in
    `RenderToPngAsync`) while three snapshot apps rendered at once (the layout check, a replay and a chapter run); run
    alone, each view rendered whole, and passes 1 to 3 never saw it. The QA role's "one app at a time" stands; why the
    renderer fails under load is not known.
  - `tools\layout-check.ps1` says PASSED for a view whose `window.layout.json` exists even when its other pictures
    (`card.png`, `map.png`, `settings.png`) are missing; exit 2 is meant for that (docs/LANGUAGES.md, "Layout check").
  - Of the pass's 2,122 pictures about 95 were opened (every moment a fix is judged at, the problem pictures, a sample
    of the layout check's); the rest were judged from the tools' summaries.
  - Five replay scripts drafted for the pass (What's New 0.6.0's previews, chapter 5's rows, a long Streets roll call, a
    picked name at the right edge, the first raid's loading cue) are proposed for `tools\qa\`, each with its `.md`.
  - For the owner to confirm: an untrimmed lit name over a door label at the smallest window ("Delivery From the Past"
    over "Director's" when picked or pointed at in Plan; chapter 4's "The Extortionist" over "Unknown", the same at
    3bc6d3f).
- **Accepted for 0.6.0, open** (owner, 2026-10-16: "Accept all four"): the tour's cue at 900×560 drawn at 0.41 (rows
  about 6 px); the 2 px push limit lets two collars touch for a few frames mid-move; below about 55 frames a second a
  double-click's middle frames pass the steady rule (E4 above); a trimmed lit name may overlap a door label at 500×481.
  Unanswered: which screen showed BRING without the x2 of Ill Wisher's and Humanitarian Supplies' items, and how the
  held quest card was cut off at the window's foot (a rail row, a map marker or a raid card line; maximised?).

## 2026-10-15: a new version, said louder: a cue, then the band

**The ask** (owner, 2026-10-15): "A Beta-tester did not realize there is an update, which shows me that it is currently
too subtle. I, however, do not want to have a blocking modal like a pop-up." The one sign was a line at the top of what
the rail scrolls, in the 11 px eyebrow type and amber like NEW IN 0.5.0 · HYENA beside it, with an 11 px RESTART NOW
link; no glyph, frame or motion, and it scrolled away. From a panel of four, drawn in the app's look on the same day,
the owner chose B, "A cue, then the band" (no notes). Not taken: A, the band alone; C, applying by itself when the game
is quiet (it reversed the decision of 2026-10-03); D, a fourth status light and the window's title. A Windows toast was
declined before the panel (it shows over the game). The panel stays outside the repository.

**Built (2026-10-15), as B was drawn.** DESIGN.md §8, "Updates", is the binding description; §4, principle 11 (UPDATE
READY) and "Screen anatomy", *Rail* (*The update band*) say the rest.

- The band in the rail's pinned row under MAP, which holds the raid card's head in a raid and while one loads
  (`MainWindow.xaml`, `UpdatePlate`): a Panel plate with an Amber hairline, the glyph `E777`, the eyebrow by stage
  (`UpdatePolicy.Decide` now gives `UpdateBand`: DOWNLOAD AVAILABLE, DOWNLOADING, UPDATE READY, UPDATE DIDN'T APPLY;
  `UpdatePolicy.Words`), "0.6.1 · OKAMI" (the name from What's New, `Rules.WhatsNew.NameOf`, else the version alone),
  the note, and RESTART NOW or DOWNLOAD in the Report dialog's SEND style. `Controls.Glow` on it when it shows, at each
  return to the menus, and 4.6 s into the cue. The old line and its four texts are gone.
- The cue `CueKind.UpdateReady`, made by the window (`MainWindow.Updates`), its amber line in `CueFirst`, kept clear of
  the map's buttons. The rule is `UpdateCue` in Shturmap.Session, with tests (`UpdateTests`): due in the menus for a
  stage not said yet (Ready; Found in Tell me only), or once more after a raid over live (RAID OVER); never with Off,
  for the version still waiting at start (its notice), after RESTART NOW, or in a snapshot run. The window waits for the
  map's middle (a cue up, a replay or the completions waiting for it, the tour, a preview) and shows it once that has
  been free 0.5 s.
- Decided in the build, not drawn on the panel (provisional): the band's notes while downloading ("Applies at the next
  start.") and over DOWNLOAD ("Downloads in the background. Applies at the next start."); a cue cut short by another cue
  counts as shown; a load cancelled is no raid (no cue after it); the band glows both as it shows and as the cue fades
  (A's glow and B's together, as B said "the band works as in A").
- Developer aid: `trigger update` (and the developer view's "Update ready") now takes the next patch of the running
  version for downloaded (`Updater.DevReady`), so the band and the cue read the real stage; `--show-update` shows the
  band in a snapshot (the layout check's plan and raid views). The replay `tools\qa\update-ready.devscript` and its
  `.md`: the cue's entrance with the band glowing in (t0), the cue whole (t1), the band alone (t2), the raid card's head
  in the row in a raid (t3), the cue again after RAID OVER (t4); run in English and German at 1600×900 and 900×560.
- Changed from the panel before the merge: its cue line said "RESTART NOW IN THE RAIL", the project's word for the left
  column, which a player sees nowhere on screen (the senior translator noticed it too). The line names the place as the
  band stands, pinned at the window's top left: "RESTART NOW · TOP LEFT · NEVER IN A RAID", "DOWNLOAD · TOP LEFT"
  (German „JETZT NEU STARTEN · OBEN LINKS · NIE IM RAID“, „HERUNTERLADEN · OBEN LINKS“).

## 2026-10-15: the extract list's roll call

**The ask** (owner, 2026-10-15): "When the extraction list is read, I want a nice fancy animation in line with the
others where a list of the extracts appears in the middle of the screen along with their icons. Then, the icons move
where extracts are (also outside of the current view pane) so it contextualizes them and the player knows where they
are." From a panel of four, drawn in the app's look on the same day, the owner chose A, "Roll call, then fly-out"
(no notes). Not taken: B, badges at the map's edge for the extracts out of view, with no change of view; C, the
fly-out and then a glide back; D, the cue alone with the ring of 2026-10-14. The panel stays outside the repository.

**The decision, as A was drawn.** DESIGN.md is the binding description: §1 (the fourth exception for eye candy),
§4 principles 2 and 11 (YOUR EXTRACTS THIS RAID), "Design system", *Motion*, "Screen anatomy" (the extract list, the
tour's chapter 5, What's New's `extracts`) and "Map drawing", *The player's extracts this raid* and *The view leaves
the player*. In short, from the list's arrival (a screenshot taken in the last 30 s, once per list):

- 0 s: the cue across the middle of the map column, "FROM YOUR SCREENSHOT" over "YOUR EXTRACTS THIS RAID · 6 OF 12",
  the title decoding, the gold rule shooting out; from 0.5 s a row per listed extract rises in, 0.1 s apart, nearest
  you first: its triangle in its kind's colour, its name, "???" for a "??:??:??" one. No row for a struck-out extract
  or a transit.
- 1.2 s: the view frames every listed extract and you over 1.2 s, eased in and out, with the label room on the right.
- 1.6 s: each triangle lifts off its row, 0.16 s apart, and flies 0.9 s on an eased arc to its extract, landing with
  the ring of 2026-10-14; its row fades as it leaves, and the band's dark backing goes with the rows (the panel's
  drawing had four of seven landings under the band). With six, the last lands at 3.3 s; the title stays on its halo
  until 4.8 s, the rings end at about 5.4 s.
- After: the view stays fitted. Follow my position on: the next position glides it back as usual (2.4 s). Follow off:
  principle 8 holds, it stays until the player moves the view (a drag, a zoom, F): the one point the owner didn't
  note on the panel, confirmed 2026-10-15.
- A list while the cue is up joins it (rows after the others, flown after them); one after it has gone rings what it
  lit anew where they stand; the same list again changes nothing. Animation effects off: the cue stands 5 s with all
  its rows, the view fits at once, the extracts are simply lit. The tour's chapter 5 and What's New's `extracts` show
  the example list the same way.

**Built (2026-10-15).** The map side (`MapScene.CallOutExits`, `FlightAt`, `Camera.FittingAll`, the frames scene
`list-callout`) and then the window's side (`MainWindow.RollCall`): the cue kind `ExitsListed`, its rows in `CuePanel`
(`CueExits` in a Viewbox that draws them smaller only where the map's column has no room, as the tour's plates; the
title's halo of letters, `CueTitleHalo`), each row handed to the map as the centre of its triangle in the map panel's
pixels, the fit at 1.2 s (`MapView.CallOutView`, a zoom out only), the rows fading at their lift-offs and the band's dark
with them, the cue going 1.5 s after the last landing. The points this entry left open, as built (DESIGN.md says them):
the notice went with the cue (as QUEST COMPLETE's and SEEN IN YOUR STASH's); a joining extract outside the fitted view
has the view take them all in again; chapter 5's plate keeps its YOUR LIST THIS RAID line (the cue comes after the
plate has gone). The owner confirmed all three on 2026-10-15 (below, "Settled 2026-10-15"). A list after the cue rings only its
additions (`MapScene.CalledOut` then; since the QA follow-ups of 2026-10-15 the window keeps it for the raid,
`Rules.RollCallRecord`, as a look at another map made the scene anew and lost it), with a test. What wasn't drawn on
the panel and was decided in
the build: columns evened out (six stand three and three), names broken between words in a narrow column, twelve rows
at the smallest window drawn smaller (German about two thirds), the title's halo the dark around its letters (a box hid
a landing under the title), a position that comes while the view moves for the roll call pings without gliding.
The replay `tools\qa\extract-roll-call.devscript` with the script step `exits` (the session's exit-list path after
the reader, as a screenshot taken now): a list read on Customs in two raids, stills of the band with its rows rising
(about 0.6 s), the icons near landing with the band gone (2.5), the hand-off (rows leaving, 1.9, second raid), all
landed with the title on its halo (3.6) and the end (5.9); its `.md` says what each still shows. What's New's
`extracts` preview isn't reached by a script (`whatsnew <n>` reaches the newest version's lines only): looked at in
the code, not in the window. The UI QA role reviews it before it is reported done.

**Why A:** the only option doing both halves of the ask; one change of view, made while the cue says why (principle
11); the fitted view is the one a player at the spawn plans the route on; built from what is there: the cue's band,
the ring, the camera's framing and glide.

**What changes of the ring of 2026-10-14:** the rows and flights go nearest you first, lit and "??:??:??" alike, as
drawn (the ring took the lit ones first, at most six turns each); a list after the cue has gone rings only what it lit
or marked anew (the ring's rule had a list after the rings were over ring them all again). Read into the decision and
written so in DESIGN.md: the title's count follows the rows a joining list adds, as the stash cue's line does.

**Settled 2026-10-15** (not drawn on the panel; the owner confirmed each as proposed, on a decision page the same
day; DESIGN.md says so):

- An extract a joining list adds that lies outside the fitted view: the view frames every listed extract and the
  player again, with the same 1.2 s move ("Map drawing", *The player's extracts this raid*; principle 11). A list
  after the cue has gone still rings in place with no change of view.
- The notice "Your extract list is read: 6 extracts for this raid" goes: the cue takes its place, as QUEST COMPLETE
  and SEEN IN YOUR STASH took the place of theirs. The extract list's own line still says the list was read.
- Chapter 5 keeps its plate's line "YOUR LIST THIS RAID: {n} OF {all} EXTRACTS" beside the cue (docs/tour.md's `read`
  and its `TourTests` check stay).

**Decided 2026-10-15 from the first QA pass** (owner, on the same decision page; built with the confirmations above):

- **The bold comes with the lighting.** Every listed name was bold from the list's first frame, before its extract was
  lit (`shturmap-cli frames customs --scene list-read`: Old Gas Station, ZB-1013, Trailer Park, Crossroads bold at
  +0 ms). Now a listed name is in the regular face until its extract lights, from its landing in the roll call and
  from its turn in a plain ring, and bold from then on, on the same side and in the bold name's kept room, so no label
  moves (`PlacedLabel.Unlit`, `SymbolTrace.Bold`; the frames' summary lists frame 000's bold names and checks that none
  is bold before its extract is lit).
- **No name cut at the map's left edge, and the scale bar steps aside.** At the smallest window Streets read
  "ansit to Interchange", and the scale bar lay over a lit Crash Site, whose name then had no free side. Now a side of
  a label that would start past the left edge isn't free (above and below slide in to start at it), and a symbol under
  the scale bar moves it up by what it takes, at most 96 px ("Map drawing", *Room for labels on the right*, *Distances
  on the map*). `shturmap-cli frames <map> --viewport 500x481` draws the map of the smallest window. Placed afresh in
  every frame, the bar stepped up and back while the view moved, as symbols passed under it (the roll call's zoom out
  at 500 × 481: 30 changes of place on Streets, 17 on Factory; at 1600 × 1000, 7 on Interchange). Built the same day,
  as proposed: while the view only moves (the steady placement's rule) it keeps its place, and the first frame drawn
  with the view still places it afresh; a frame whose bar held asks the window for one more, since the end of
  `MapView.AnimateView`, of a wheel's notches or of a drag draws none by itself. Streets at 500 × 481: a 1.2 s glide
  at 16 ms a frame (`--scene list-read --glide --every 16`) 59 changes of place, now 1; the roll call 30, now 1
  (`GlideFramesTests`).

**What is built where** (this commit has the docs, the cue's texts and the tour's words; the rest is to build, and
the UI QA role reviews it before it is reported done):

- **Map side** (`Shturmap.Map`): `MapScene.CallOutExits` in place of `RingExits`: the turns, nearest you first; each
  triangle's flight from where its row hands it over (a point on screen) to where the frame draws its symbol (on its
  leader where it is set apart, so it follows the view still settling), 0.9 s on an eased arc; and from its landing
  the ring as `MapRenderer.DrawExitsLit` draws it today. A listed extract stays solid and unlit, its name in the
  regular face, until its landing (the face: built 2026-10-15, above). A
  list joining while the cue is up takes the turns after the last; one after the cue rings in place by the ring's
  rule. The flight is never drawn under the band's backing.
- **The cue's body** (`MainWindow`, `CuePanel`): the rows, each the extract's triangle (as the legend draws it), its
  name and "???", rising in from 0.5 s; each row's triangle handed to the map as it lifts off, the row fading; the
  backing going with the rows; the title on its halo until 1.5 s after the last landing; rows a joining list adds.
  Its texts, `AppTexts.CueExitsListedEyebrow` and `AppTexts.CueExitsListed`, are in (English and German).
- **Orchestration** (`MainWindow`, where a list just read calls `RingExits` today): the 30 s rule and once per list as
  before; the cue (a kind of its own, joining itself as the stash cue does); the fit at 1.2 s (`Map.AnimateView` to
  `Map.FramingOf` the listed extracts and the position), which stops a Follow glide under way; nothing moves the view
  back afterwards; a joining list with an extract outside the fitted view frames them all again with the same move
  (settled above); the one-line notice goes, its texts with it (both built, "Built (2026-10-15)" above); animation
  effects off: the cue still for 5 s and `Map.Jump`. The tour's chapter 5
  (`MainWindow.Tour`, framed above the tour's band, with "An example, not your raid.") and What's New's `extracts`
  (`StageExtractList`, no position) call out the example list the same way.
- **The frames scene `list-callout`** (`shturmap-cli frames`; DESIGN.md §8, *Animations frame by frame*, names it once
  it is built): the call-out drawn at chosen times from t0, with the fit; per frame each triangle's place in flight,
  its landing and its ring's values; the checks: every listed extract lands on its symbol, nothing off the list moves,
  the last landing and the rings' end at the times above. A frames test beside `ExitRingFramesTests`.
- **The replay script** in `tools\qa\`, with what its stills must show beside it: a raid on Customs, a position, a
  list read, with Follow off and with Follow on, stills at 0.6, 1.4, 2.4, 3.3 and 5 s and after the next position. It
  needs a script step that reads a list (none does: `pos` gives a position only).
- The release that ships it adds a `tour:5` line to What's New (CLAUDE.md, the tour rule): chapter 5 changes.

## 2026-10-15: What's New's tour line tore down the tour's staging

The owner, on 0.5.0's What's New line "Your extracts, clearer" (`tour:5`): "the highlight does weird movements and
highlights the wrong things sometimes". Found in the code (not seen in the app yet):

- **The cause.** A click on the line opens the tour at YOUR SCREENSHOT KEY, which stages its example on Customs as a
  preview (`TourScene` sets `_previewing`). The tour's layer then covers the line, so at the pointer's next move at the
  latest the line's `PointerExited` asks for the end of a preview, `Preview(null, 0.3 s)`, as for any line;
  nothing in the preview machinery looked at the tour. 0.3 s later `EndPreview` ran: the label went, the player's own
  map came back under the tour, and the chapter went on against the Customs scene that had gone: its ping, its
  framing and its 1.2 s move were worked out on Customs and applied to the player's map, its cut-outs placed by it, its
  extracts rung out on a scene not drawn. "Sometimes": it waits for the pointer to leave, and what it spoils depends on
  where the chapter is by then. 0.4.0's tour line opened chapter 1, which stages nothing, so it never showed.
- **A second fault in the same chapter**, with Follow my position on: the example position, set right after the chapter
  framed it, glided the view for 2.4 s toward it (a sixth of the view ahead, the way it faces), under a cut-out placed
  before the glide.
- **The fix.** While the tour is up it owns the map (`Rules.PreviewOwner`, DESIGN.md §4, *What's New* and *The tour*):
  the pointer's preview requests count for nothing, the timer's end does nothing, and opening the tour drops a request
  still waiting; the chapter's own `EndTourStage` ends what it staged, as before. The tapped line lets go of its tint.
  Follow my position stands still while the tour is up (`MapView.FollowHeld`) and is the player's again before their
  map comes back at the end.
- **Found on the way, fixed too.** Every preview's end made the shown map's scene anew, also after a cue or a plate
  that never took the view (What's New's stash lines and clock): the last position counted as new, pinged again and,
  following, glided; now only a preview that put its own map in the view has the player's made anew
  (`SceneGate.MakeAnew`), at its view as before; the map still comes back fitted after a preview that began before any
  map was drawn, and picks still stay off a preview's map. The cue's stamp (QUEST COMPLETE, SEEN IN YOUR STASH) was
  started anew on the same parts while the one before still ran (a quest joining within 1.6 s, the stash line pointed
  at again); a stamp now stops the one before. The chapter's map, and What's New's `extracts` preview with the same
  example list, kept both sides' extracts: Customs' 17 Scav ones stood solid and unlit, five of them under 5 m from
  a lit or struck-out PMC extract of the same name, set apart from it on a leader (frames drawn of the example; the
  places checked in tarkov.dev's data, PvE and PvP). Both are a PMC raid's map now, as in a raid: no Scav extracts;
  so is IN THE RAID's. PICK AND POINT (in the menus, as Plan's map), NEXT RAID's previews of the player's own rows and
  What's New's `replay` (a raid's replay plays after it, in the menus) keep both sides', as the app shows them.
  The extracts ring out nearest the example position first: it is set at the ping, long before the list is read.
- **Left as it is.** While the view moves (the chapter's 1.2 s move, a replay's framing), symbols set apart on leaders
  are placed anew at every frame for the zoom of that frame, so one can jump; the chapter already works out its framing
  once, before the move, and its last cut-out after the move has settled, so only not moving the zoom, or placing the
  symbols once for the move, would avoid it: the map's general behaviour, not this report's. What's New's `extracts`
  preview has no position, so its extracts ring out in the data's order, from one end of the map to the other.
- **The check in the app** is the QA pass's: `tools\qa\whatsnew-tour-line.devscript` on a developer build, with what each
  still must show and what the fault looked like in `tools\qa\whatsnew-tour-line.md`. New script steps for it:
  `whatsnew click <n>`, `whatsnew leave` and `tour end` (a `--dev-view` session starts as a first start, so the tour
  opens by itself); `snapshot` expands `%VARIABLES%` (DESIGN.md §8, "Developer aids"). `whatsnew <n>` on a line about
  the tour previews nothing now, as the pointer: it put up the line's preview label over nothing staged, a state no
  player sees (`Rules.WhatsNew.ShowsPreview`).

## 2026-10-14: before 0.5.0 "Hyena"

The owner's answers to what was left for the release (2026-10-14):

- **The name:** "Hyena" (docs/whats-new.md, its German file, the notes' first line).
- **German:** stays in the build and is chosen as before (Automatic takes it from the game or Windows), but isn't
  announced: no What's New line, nothing in the notes ("Keep it, don't announce"). So the German in-game check below
  stays for later.
- **0.4.0's extracts line:** says "struck out" now, as its preview shows since the exits' new look (e9c2adf).
- **DESIGN.md §2's text on menu screenshots:** "Adjust it": checked against the code (which pictures, the candidates
  BRING lists, what is kept: all as built); it now says that "Delete screenshots after reading" takes them too.
- **"Delete position screenshots" is "Delete screenshots after reading"** (owner: "rename … and also check if it does
  so - because otherwise the folder is littered with screenshots from the menu"). It didn't: only screenshots with a
  position went, and each 5 s after it was seen, whether its picture's reading was done or not. Now every new
  screenshot the game names goes, raid or menu, once its reading is done and at least 5 s after it was seen
  (`ScreenshotCleaner`, `GameSession.ReadScreenshotAsync`); the saved key `screenshots.delete` stays.
- **What's New** for 0.5.0: the three stash lines, the extracts' new look (`tour:5`) and what it reads (`tour:1`, with
  the deletion). The notes add the extracts, the deletion, NEXT RAID's every map and the quest card's "With:" line.
- **The website's words and media:** updated in the site repository, committed with the release (the site says what
  the released app does, so it goes up when 0.5.0 does).

## 2026-10-11: German, left to check in the game

German was merged on 2026-10-11 (not released). The Windows Settings names in its texts were checked the same day in
Windows 11's own German Settings texts (docs/glossary-de.md). Three things only the German game shows are left (owner,
2026-10-11: "Note the other ToDos down for later"). Shturmap never touches the game, so the owner takes these steps,
in one session of about 15 minutes, and can set the game back to English afterwards:

1. Set the game's language to Deutsch in its settings (it may need a restart).
2. In a raid (a Scav run will do): press O twice so the extract list shows, then take a screenshot with the game's
   key. The dev build reads that list too: the first German list from the game itself, not one a test drew.
3. In the menu: screenshots of the controls settings with the screenshot key's row, and of Character → Tasks with
   the quests' types (Win+Shift+S if the game's key takes none in menus).

From those screenshots, read on the PC and never committed:

- **The extract list's header.** The only one that changes behaviour: a reading that names one exit counts as the
  list only under the list's header (`ExitList.IsList`), and `ExitList` knows only the English "Find an extraction
  point"; a German list that names two or more exits is read already. Add the German header with a test on its
  text, and to the glossary.
- **The game's German key names** (glossary, "Keys"). Likely no change: the texts use what German keyboards print
  (Druck, Pos1), which stays even if the game's settings write "PrintScreen", since that is where the player looks
  for the key. Note the game's names.
- **Quest types** (glossary, the quest types row). A note only: the players' words win (Kills, Erkunden, Pickup …).
  Note the game's names.
- **Windows 10, if a German one is at hand:** the name of its Settings → Time & language → Language page
  (glossary: „Sprache“). Low: the English text names Windows 11's pages too.

Noticed during the German work, not fixed (owner to decide):

- **Dev tools sort by the thread's culture** (`DevController.cs`, `LayoutCheck.cs`), not by the language in use as
  the player's views do. Players never see these.
- **The raid card's map at a sentence's end:** `ObjectiveView.TextOn` strips only the English " on <map>", so a
  German objective probably keeps its „… auf <map>“. Not checked against the data.
- **The quest card takes English "from", German doesn't take „aus“:** "extract from Customs" drops the map line in
  English, „aus Customs“ keeps it in German (17 objectives).
- **The English map-line pattern** would take "The Lab" from "on The Lab (Dark)" (`\s*\(`). No English text has
  "(Dark)" today.
- **Tour chapter 4, German, 900×560:** the example quest's map label "Lieferung aus der Vergangenheit" runs off the
  map's right edge. The layout check doesn't look at map labels.
- **Tour chapter 3's pointer** starts at a fixed spot (55 % down the map), which a tall band may cover at small
  sizes. A guess, not checked.
- **The tour title's decode** empties the title until its first tick (about 35 ms) at every chapter, so the band
  shrinks for a frame.

## 2026-10-11: what you have, the spike

The second design of "what you have" (its panel is private; the first is below, "Designed 2026-10-07, deferred")
ended in twelve decisions, which the owner took as recommended on 2026-10-11, and a spike before anything is built:
a headless reading of the owner's own menu screenshots, to learn on evidence whether the screenshot source is worth
building. The spike is `shturmap-cli stash <png|folder>` (`tools/Shturmap.Cli/Stash/StashSpike.cs`): it finds the
item grids in a screenshot, cuts the cells, matches each against tarkov.dev's grid images and reads the cell's label
with Windows' text recognition, and scores itself against a hand-written truth (`--truth`). Nothing of it runs in the
app; DESIGN.md §2 is unchanged (the app still never opens a menu screenshot). The owner's 33 screenshots (2560×1440,
the gear screen with containers open, a 12×12 window, two trading screens) stay in the git-ignored
`tests\fixtures\ocr\stash` with the truth files; they never enter the repository.

**Found, 2026-10-11:**

- **The game's screenshot key works in the menus:** 33 files, named without a position, as `ScreenshotName` expects.
- **The label is the identity, the picture is the family.** Read with Windows' recognition from the cell's top band
  at four times its size, matched to short names with the extract list's folding, a rule for names the game cuts at
  the cell's edge, and digits that must agree (lookalike keys differ by one digit): a container of 48 keys came out
  46 right, 0 wrong, 2 unread; a medical case 43 of 43; a stash page 51 of 55, 0 wrong. The picture, compared as
  art pixels only against tarkov.dev's grid images at four sizes and small shifts, is right for 4 of the 48 keys
  (lookalikes share their art exactly), 18 of 43 medical items, 33 of 55 on the stash page: the game draws the art
  about 15 % larger than tarkov.dev's images, some of those images are stale (one key lies horizontal there and
  diagonal in the game), and both hatch the cell's field. The picture's use: a check (a label that contradicts it
  says nothing) and the few cells whose label can't be read but whose art is unique (68 of the 770 names below).
- **Over all 33 screenshots:** 1,110 item rectangles, 770 named (702 by the label, 68 by the picture alone), 287
  with nothing said, 53 of an odd shape (cells merged by a hover highlight, a wide item cut into cells); about 1.4 s
  per screenshot on the owner's PC, recognition included, with 3,905 templates loaded.
- **The grids:** cells are 84 px at 1440p (63 × 4⁄3), with a one-pixel line in the stash and a two-pixel line in a
  window; many cells carry the game's own cyan outline (what it marks wasn't checked), which a naive line rule
  misses. Found on 31 of 33; the two misses are stash pages of mostly large items (helmets, cases), where the rule
  wants framed one-by-one cells: the fix is the stash's known column positions, not more searching. The trader's
  grid on the trading screen is told by its place (left) and skipped; a container window over the stash hides the
  cells under it.
- **Trading screens are blind:** there the stash's cells carry the sell price at the top instead of the name, so the
  label reads nothing (7 and 10 of 38 cells named, by the picture). A reading should skip them.
- **The set is bigger than the panel counted:** keys 259, items objectives take or use 3,774 (many list whole
  classes), quest items 105: 3,922, not about 1,150; the spike fetched 3,905 grid images once (42 MB as PNG in the
  cache). The consequence for the design: match against what the active quests need (dozens of items), and fetch
  templates for those only, a few kilobytes each; the label match against the whole set is what produces false
  names.

**Not covered:** other resolutions (only 1440p), the hideout screen (no screenshot of it), counts and the
found-in-raid mark (not read), the German game (tarkov.dev has German short names; the recognition's language would
follow the game's, as the extract list's does).

**Where it leaves the design:** the screenshot source is worth building, label first; I5 (the template set) changes
to the needed items only; the §2 widening is still the owner's to write before the reading goes into the app.

## 2026-10-11: what you have, built (phases 1 and 2)

The owner the same evening: "push it, then start phase 1. Continue with phase 2 as well." Both were built as one
release, 0.5.0 (the minor number raised: a new feature; **its name is still to be asked of the owner**, the heading in
docs/whats-new.md and the first line of docs/release-notes/0.5.0.md have none yet). The §2 text the panel drafted went
into DESIGN.md on that word; the owner reads it there.

Built: the ledger (`StashLedger`, `stash.json`; the newest observation wins, counts at least, passes, retractions,
start items gone with their quest); the log's grants (types 10, 12 and 8, `QuestEvent.Items`,
`InsuranceNoticeEvent.Items`); the reader (`StashReader`, label first against the items BRING lists, the picture a
veto, trading screens skipped); the session (`GameSession.Stash`: the setting `screenshots.readStash`, the needed set
and its grid images through `GameArt`, the notice, NOT ANY MORE, the file); the words (`StashMarks`, SessionTexts
`Mark*`, English and German, reviewed); the surface (the BRING row's ink line with the stash glyph, the amber line for
the rest, the need cells' bar, the quest card's BRING, the item card's STASH section with NOT ANY MORE, the note under
BRING, the settings row, tour chapter 1, help's note); the docs (DESIGN.md §1, §2, §3, §4 "What you have", §6, §8;
README, PRIVACY.md, UPDATES.md, the glossary); tests (`StashLedgerTests`, `GrantedItemsTests`, `StashSessionTests`,
`StashReaderTests`). `tools\fake-raid.ps1` hands two quests their items, so a Plan snapshot shows a whole mark and a
part mark.

The reader, measured on the three truths with the whole quest set as candidates: the key container 46 of 48 right,
0 wrong; the medical case 43 of 43; the stash page 50 of 57, 0 wrong (three of the seven silent are two items of one
short name, which the reading won't choose between; two a hover highlight merged). Both trading screens recognised.
The found-in-raid mark is read (a drawn circle-and-check correlated at its place: marked cells 0.43–0.54, unmarked
under 0.3). A count of one digit isn't read by Windows' recognition and counts as one ("at least" holds); counts of
two or more digits read. 0.75–0.9 s per 1440p screenshot with 60 candidates.

Left, for the owner to decide or do:

- **The release name** (above), and whether 0.5.0 also ships German (the What's New and the notes draft say so; the
  owner left shipping German out on 2026-10-11, so the lines are a proposal).
- **Two needed items of one short name** (two keycards both called "Access") stay silent: the reading won't choose,
  and the session passes both. A rule for that (the art decides when it can; else the first) is a small change if it
  turns out to matter.
- **The messages screen** (owner, 2026-10-11, testing: "When you get items delivered from the traders through the
  messages screen, these items will be on the left hand side of the screen"): built the same evening from the owner's
  two screenshots of it. A message's attachments stand in a grid of the stash's width at the left, set in from the
  edge by most of a cell (64 px at 2560) where the trader's grid hangs on it (11 px); the reader tells the two apart,
  never reads the attachments (not the player's until taken) and reads the stash beside them as on any other screen
  (`StashGrids.PlaceOf`, `StashGridPlace.Message`; a drawn test). On the owner's two screenshots the attachments'
  faint lines give no grid at all (`shturmap-cli stash --grids`: one grid, the stash), so they read as a stash page
  either way; the rule holds for when they do. Done.
- **In a raid, nothing goes into the ledger** (owner, 2026-10-11: "the player still can die"). Holds, and
  `StashSessionTests` pins it now: a menu-named screenshot while the raid runs opens no picture and marks nothing.
  Done.
- **Feedback with motion** (owner, 2026-10-11: "a little eyecandy animation when the app recognises the player updates
  the stash … give proper feedback and look cool visually"; of the cue, the rail's glow or both, "C"): built the
  same evening. The SEEN IN YOUR STASH cue pictures the needed items a screenshot showed with their counts, readings
  in a run join it, and the marks that just arrived glow in on the rail and the quest card (DESIGN.md §4, principle
  11 and "What you have"; `MainWindow.StashCue`, `Controls.Glow`). What's New's 0.5.0 lines preview it (`stash`,
  `stash-card`, `stash-given`) instead of opening the tour, which the owner reported as a bug the same evening. The
  developer view's `stash` shows the cue with the example items. Done.
- **Marks going stale** (owner, 2026-10-11: "how do we deal with the inventory depleting over time and info going
  stale? Can we kind of make a 'reset' of the info after a while?"). Offered: a hand-over rule from the log, a manual
  FORGET ALL MARKS, a timed expiry (advised against: it drops right marks as silently as wrong ones and reverses "never
  into silence"); the owner took the first two ("1 and 2"). Built the same evening: a quest the log says completed
  takes what it used up (planted, marked, handed over) from the marks observed before the completion, once per
  completion (`StashLedger.HandOver`, `GameSession.Stash.StashHandovers`, the file's `handovers`); FORGET ALL MARKS
  under the stash setting, one click, with `forgottenAt` in the file so the log read back brings nothing old
  (`StashLedger.ForgetAll`). DESIGN.md §4 "What you have" and §8 "The stash file". Done.
- **The layout check in both languages** before the release (docs/LANGUAGES.md; about 45 minutes), and the website's
  words (the website says what Shturmap reads).
- **I HAVE IT** (the player's word, phase 3): not built; the ledger knows the source. Build it only if the first
  release leaves gaps that matter (weapons, worn gear).
- **Other resolutions and screens:** only the owner's 2560×1440 was read. A 1080p screenshot and one of the hideout
  crafting screen would close two unknowns of the spike (the stash's column rule, the hideout's grids).
- **The game's language:** the labels are matched to tarkov.dev's short names in the language the data is loaded in,
  which is the game's by default (the language setting). A player who sets Shturmap to another language than the
  game's gets no marks from screenshots; a later change could keep the game language's short names beside, as
  `GameData.GameNames` does for extracts.
- **What the game's cyan outline marks** on stash cells (seen in the spike): if it is what the hideout or quests
  need, the words could say so. Not checked.

## 2026-10-09: the pre-release review

Before going public, six reviewers (privacy, public-repository content, UX, engine code, UI code, features) went
through the app, the docs and the website; the owner decided each finding the same day, and all were built then.

- **Privacy and content.** Every example screenshot name, log session and log time in tests, comments, tools and the
  website is made up and dated 2026-01-01: they held times of real play with a position or a session's quests, which
  the game's servers can match to the player. `RepositoryScanTests` fails on one dated any other day. The docs give
  the study logs in rough numbers and log lines as shapes (owner: coarsen). DESIGN.md §3 says the one-line objective
  samples in tests are the limit for quest texts (owner: they stay). THIRD-PARTY-NOTICES.md says why the ML package is
  referenced. The tour's first chapter is WHAT IT READS: "SAFE TO RUN" used the word §2 keeps off Shturmap.
- **Removed** (owner): popped-out quest-card windows (picks come first in the raid card, with live distances); the
  keyboard's steps through the rail's rows (the doc itself said nothing needed them); the raid clock's decode at each
  minute. QUEST COMPLETE is named as §1's third exception, beside the replay and the tour. Help and settings say less,
  and the legend lists twelve of the map's symbols with the rest folded.
- **Fixed in the UI.** The map comes back fitted after a tour or preview that began before it was drawn (the first
  start's tour gave back the camera's default); a new map or a replay's end stops a glide under way; the tour never
  opens in a raid, from help or What's New either; EXIT's and OR's notes take a second line instead of being cut;
  RESTART NOW starts the updater before it closes the session; the rail's lists keep their rows while a snapshot says
  the same (every snapshot rebuilt them, and a pointer on a row lost its hover); a saved window place is checked at
  its monitor's scale; icon-only buttons have names for screen readers; the player's picks stay off a preview's map;
  the tour's example card says NEXT as the raid card does.
- **Fixed in the engine.** A row of the extract list that folds to nothing is passed over (it threw, and the whole
  list was lost); a settings file that can't be read is set aside and the session starts on a fresh one (it started
  none); a page sent in place of tarkov.dev's data is downloaded again, not read from the cache; a screenshot that
  comes back (a sync tool, a restore) is neither placed nor deleted; quests are worked out once for a run of quest
  messages; closing the session no longer disposes the token its loops read; the quest history counts a log session
  without a mode line; dead code goes, one table holds the game's language codes, the CLI takes its folders from
  `AppPaths`, and downloads wait for a reader without sleeping a pool thread.
- **Consistency.** Boss chances in one format; a quest card's distance in the raid card's capitals; DESIGN.md says
  picks wear Pick1 to Pick8 and what the 900×560 minimum is for (owner: it stays); a Scav raid's start is said by its
  cue and the card's note, without a notice as well.
- **Checked, no change.** "22 MIN IN · 13 LEFT" on Customs was right: tarkov.dev gives Customs 35 minutes. The badges
  at a crowded spot don't overlap ("?" and the count 4.5 px apart, OPT and the floor badge 1.2 px). NEXT keeps
  optional objectives (owner). Up and Down switch the map while the MAP list has the focus, a dropdown's own keys
  that the rail's steps used to take; they stay (owner).
- **Decided after the renders** (owner, 2026-10-09): help's twelve symbols list the extracts and transits before what
  shows only at times; the first window is 1600×1000 at the monitor's scale, within its work area; the whole map and
  every framing keep up to 150 DIP on the right for the map's labels; an answer that isn't JSON at all is "tarkov.dev's
  answer wasn't its data", tried again by itself, with no report asked for, and it never replaces a good saved copy,
  which is used in its place.

Open:
- Measure the renderer's hot path before changing it.
- DESIGN.md §4 doesn't name the extract list's cost in the game (two key presses for a list on demand).
- Labels placed left of their symbols are cut at the window's left edge ("…ation Gate" on Customs); at 900×560 the
  room on the right is capped at 100 DIP, so a long name there can still be cut. The left edge: done 2026-10-15 (no
  label starts past it; "Map drawing", *Room for labels on the right*); the right edge stays as it was.
- The review's proposals, for the owner: a glide to a position out of view after a while without input, Follow on by
  default, items given with a quest, the raid clock from the screenshot name.

## Designed and built 2026-10-09: the tour, and the story chapters said

**The ask** (owner, 2026-10-09): "a proper first-time-opening tour of the app. This one should then later be
accessible through the help menu itself ... with the most important features, how everything works and what the most
important aspects of the UI are. Here we can be a bit more flashy with nice animations ... We need to keep in mind to
update the onboarding with future updates"; and "we also need to tell the player that the primary quests are not
available right now" (the story chapters, docs/UPDATES.md). From a panel of four forms (A briefing, B dry run, C field
marks, D a manual in help), an add-on (E first-time lines) and thirteen decisions, the owner took every recommendation:
"Tour: I go with your recommendation and I want to put an exception to the design for the tour." The panel stays
outside the repository.

**The decisions.** T1 the briefing told through an example raid; T2 first-time lines, yes; T3 the window dimmed with
cut-outs (an exception to principle 3); T4 the cues' motion plus the tour's own moves (an exception to principle 2 and
"Motion"); T5 by itself at the first start, in help's place; T6 the player steps; T7 seven chapters, about 90 s; T8 a
band at the map's foot; T9 the example on Customs; T10 a raid closes it, and it resumes at its chapter after; T11 TAKE
THE TOUR atop help and SHOW ME beside its paragraphs; T12 the story chapters said in the tour, help, the README and
website FAQ, and the release notes; T13 a What's New line opens a chapter a release changed.

**Built the same day**; DESIGN.md §4, "Screen anatomy", *The tour*, is the binding description. `docs/tour.md` holds the
words, `Rules.Tour` and `MainWindow.Tour` the rest, `TourTests` the checks; `fake-raid.ps1 -PlanOnly -Tour <n>` renders
a chapter (the window's picture leaves the tour out, `tour.png` holds it with its transparency, to lay over the
composite). Changed in the building: the first chapter's line lost "no overlay, no input" (the plate under it says it,
and the line was longer than the band); 0.4.0's What's New names the tour in place of "Symbols stand apart" (five lines
at most); parts that touch are one cut-out (the three buttons). Open: the owner's look at it in the dev build, its
words above all; the story chapters' line in the README's and the website's FAQ (with the trust section there).

## 2026-10-09: trust

The owner decided to adopt every trust step that can be automated, so that players can check the project although
they can't know its author. Built the same day (DESIGN.md §8, "Distribution" and "Continuous integration"; README,
"Check it yourself"):

- **The Release workflow** (`.github/workflows/release.yml`), run by hand, on main only: the app's package audit,
  `eng\release.ps1` with the DSN from the `SENTRY_DSN` secret, build provenance for `Shturmap-Setup.exe` and vpk's
  files for the version, then `eng\publish-release.ps1`, which now also uploads `Shturmap-Setup.exe.sha256` and
  publishes the draft only once every file is on it.
- **Pinned actions:** every `uses:` names a full commit with its version (checkout 7.0.1, setup-dotnet 6.0.0, cache
  6.1.0, attest-build-provenance 4.2.2, scorecard-action 2.4.4, codeql-action/upload-sarif 4.38.2). Nothing updates
  them: look for newer versions at a release, and pin a version at least two weeks old by its commit.
- **The package audit** (`eng\audit-packages.ps1`) in CI after the tests and before a release build, in place of
  Dependabot, whose pull requests would be commits by another identity.
- **OpenSSF Scorecard** (`.github/workflows/scorecard.yml`), weekly and on pushes to main; skipped while private.
- **`RepositoryScanTests`**, over every tracked file. Its first run found nothing but the tests' made-up profile
  names, which it allows by name.
- **The README's "Check it yourself"** with CI's and Scorecard's badges, and the same facts, shorter, on the website.

None of the workflows has run yet: the first release from the workflow is the test. Watch vpk on the runner, the
delta download with the workflow's token, the attestations, and the draft being published.

**Left for the owner:**

1. **The `SENTRY_DSN` Actions secret** (the lead sets it). The Release workflow fails without it.
2. **Going public** turns on what needs it on this plan: attestations (the first release that can be checked is the
   first built after that), Scorecard's published result and badge, code scanning. A push to main runs Scorecard.
3. **The first release from the workflow** (0.4.0): `gh workflow run release.yml -f draft=true`, download the
   Setup from the draft, `gh attestation verify Shturmap-Setup.exe -R shturmap/shturmap`, then publish the draft on
   GitHub.
4. **SignPath Foundation**, once the repository is public: free code signing for open-source projects under an
   OSI-approved licence (MIT is one). The certificate is the foundation's, so Windows names "SignPath Foundation" as
   the publisher, not Shturmap. Their conditions, to read again when applying (signpath.org): builds on a CI system
   from the public source (the Release workflow), a code-signing policy on the project's homepage (draft below), the
   team named with roles, and nothing malicious or unwanted in the software. Velopack signs while it packs (`vpk pack
   --signTemplate`), so the signing request has to sit inside the workflow's build, before the Setup is made; to be
   worked out with SignPath's GitHub action. Whether and when SmartScreen stops asking depends on the certificate's
   reputation: check before any text says so. Until it is granted, the README and the website keep "isn't signed".
5. **VirusTotal**: off for now (owner, 2026-10-09: "VirusTotal off for now"). If wanted later: with an API key as an Actions secret, the Release workflow could upload the Setup and
   link the report in the release notes. The free key is for non-commercial use and limited in requests. An unsigned
   installer may draw a few heuristic detections, which would then stand on a public page beside the release.
6. **Issues: on** (owner, 2026-10-09: "Keep github issues on"). What was weighed: The release checklist below (item 4) has them off: reports go through the app. On, players
   could report and read others' reports in public, which some take as a sign of an open project; off keeps one
   route. Related, Scorecard's Security-Policy check: a SECURITY.md saying where to report a vulnerability (GitHub's
   private vulnerability reporting, or the app's Report).
7. **Scorecard's checks that will stay low**, each raised only on the owner's word: Code-Review (one maintainer, no
   pull requests), Dependency-Update-Tool (no Dependabot, on purpose), SAST (CodeQL for C# is free for public
   repositories), Security-Policy (above), Branch-Protection (a ruleset on main), Fuzzing, CII-Best-Practices.
   Signed-Releases probably too: it looks for signature or provenance files among a release's assets, and the
   attestations live in GitHub's attestation store; the workflow could upload the attestation bundle beside the
   Setup (check which file names Scorecard counts first).

**The code-signing policy: a draft for the homepage**, to publish only once SignPath has approved the project (until
then it isn't true). The bracketed parts are the owner's.

> **Code signing policy**
>
> Free code signing provided by SignPath.io, certificate by SignPath Foundation.
>
> - **What is signed:** only the files the Release workflow builds from this repository's main branch on GitHub's
>   runners (the app, Velopack's updater and the Setup). Nothing built on a PC is signed.
> - **Committers:** shturmap. The code is written with an AI coding assistant (Claude, by Anthropic) and committed
>   under shturmap's account, which every commit names and signs; the assistant is credited as co-author in the
>   commits.
> - **Reviewers:** [who reviews a change before it goes into a release, and how].
> - **Approvers:** shturmap, who approves each signing request by hand.
> - **Privacy:** Shturmap sends nothing about you or your game unless you send a report or allow crash reports
>   ([PRIVACY.md](https://github.com/shturmap/shturmap/blob/main/PRIVACY.md)). Its other traffic downloads public data
>   and map artwork from tarkov.dev and GitHub, and asks GitHub for a newer version, which settings can turn off.

SignPath's own template says the program transfers nothing "unless specifically requested by the user"; Shturmap's
downloads and update check don't fit that sentence, so the privacy line above says what happens instead. Whether
SignPath accepts it is theirs to say.

## Designed and built 2026-10-07: What's New, a card in Plan's rail

**Built the same day** (owner: "Implement the items we agreed on"); DESIGN.md §4, "Screen anatomy", is the binding
description now. Changed in the building: the lines live in `docs\whats-new.md`, one section per version, not in the
release notes (the notes go to GitHub as they are, and a 0.4.0 notes file doesn't exist before the release); the
plate preview shows the raid card's real clock control. To try in the dev build: the card shows at its next start
(no version was seen there yet); point at each line; × and then help's WHAT'S NEW IN 0.4.0; a raid's end hides it.
`--dev-script` steps `whatsnew [<n>]` show it and its previews. The design as written before the building:

The owner, before the public release: "how to inform the user about news and changes in the app … a 'What's new'
window that showcases the new features and changes, ideally accompanied with screenshots". A window was ruled out (§4,
principle 3: no modal dialogs), and so were bundled screenshots (§3: map artwork, renders, portraits and icons never go
into a build) and pictures fetched from the website (a new host, §2). From the first panel the owner took "C" (a card in
Plan's rail whose lines are previewed on the map), from the second, four forms of it drawn in the real rail, "Whats
new: A". The panels (`artifacts\whatsnew-panel.png`) stay outside the repository.

- **Where.** A card at the top of Plan's rail, under the MAP picker and above NEXT RAID, after the update line when
  both show. Never in a raid: it steps aside while a raid loads or runs and is back after it (`WhileInRaid`, as help's
  first opening).
- **When.** At the first start of a version whose notes have items, outside a raid. A first install gets help (its
  first-run rule) and no card. A player who skipped versions sees the items of every version since the one last seen,
  newest first, each under its own NEW IN line. The version last seen is kept (`whatsNewSeen` in shturmap.db).
- **Gone.** × closes it, and it goes by itself once the first raid since the update is over. Until the next version,
  help keeps WHAT'S NEW IN 0.4.0, a link that brings the card back.
- **The look (A).** The panel's colour with a `Line` hairline; NEW IN 0.4.0 in the eyebrow style, amber, and × at its
  right (`E711`, 10 px, muted, as on a notice); one note line, "Point at one: the map shows it."; up to five lines per
  version, each a 56×34 picture and the thing's name in body text over one note line of what it is. The picture is the
  thing's map symbol drawn by `MapLegend`, as help's legend rows are (the lit extract, the joined marker), or for a thing
  in the rail a small drawing of the app's own control (the clock's figure over its rule, a cell and the trader).
- **Pointing at a line previews it** on the map, as resting on a map's row in Plan does (0.6 s; the line takes the
  linked tint): the thing's map with the thing staged, and the preview label "PREVIEW · NEW IN 0.4.0 · YOUR EXTRACTS THIS
  RAID" in amber with "AN EXAMPLE, NOT YOUR RAID" in muted. Leaving puts the map back exactly as it was. A thing that
  isn't on the map shows the app's own control, with example values, on a plate in the middle of the dimmed map. A
  preview changes nothing of the player's: no position, picks or ticks; what it stages (a list of extracts, a quest's
  spot) is an example.
- **Nothing bundled.** Every picture and preview is drawn when it shows, by the app's own code from the cache, so
  nothing of §3 enters a build. Without cached artwork a preview shows the sheet, as any map would.
- **Where the items come from.** `docs/release-notes/<version>.md` gets a list "In the app": up to five items, each a
  name, one line, and the preview's recipe (the map and its view, what is staged, or the control). The build makes the
  app's list from it; the notes' prose stays for GitHub. A test checks every version's list (at most five, short
  enough for the card, recipes that name a known map or control). The words follow the design system's "Words": short,
  fragments are fine. The items in the panel, the real changes since 0.3.0: "Your extracts this raid · Screenshot the
  game's extract list: yours glow, the others go hollow."; "Raid time, large · Minutes left as the card's largest
  figure; red for the last ten."; "One spot, one marker · Several objectives of a quest on one spot: one marker that says
  ×2."; "Hand-overs · What goes to which trader after the raid: the item, an arrow, the portrait."
- **Study log.** `whatsnew.show`, `whatsnew.point` (which item, how long), `whatsnew.close` (×, by a raid, from help).
- **When to build.** Before the second public release: at the first one every player installs fresh and gets help.
- **DESIGN.md when built:** §4 "Screen anatomy" (Rail · Plan, Help), §8 "Distribution" (the notes' list).

## Designed and built 2026-10-07: the raid replay

**Built the same day**; DESIGN.md "Map drawing", *The raid replay*, is the binding description now. Changed or decided
in the building: the minutes say "8 MIN IN", not the panel's "8 MIN" (the owner's rule of 2026-10-04 against bare
minutes on the map); the band stands 46 DIP above the map's foot, so the WIKI MAP link and the artwork's credit stay
readable; after a transit only the map the raid ended on is replayed; positions on other floors are drawn as on the
one shown; nothing ends it early but a new cue, a raid loading or a preview (no key: cues take no input). To try in
the game: a raid with a few screenshots, its end (the cue, the band, the pen), REPLAY on the last-raid line, a raid
with one screenshot (today's cue), a transit raid. `--dev-script` step `replay` plays a made-up raid on Customs. The
design as written before the building:

The owner: "a post-raid view, similar to the one in Arc Raiders … only the positions from the screenshots … a timeline
of the rough player path … more on the eye-candy side of things like the loading animations … automatically played
after the raid finishes". It answers neither of §1's two questions; the owner takes it as eye candy, an exception to §1
like the cues' entrance. From the panels (`artifacts\replay-panel.png`, `replay-chosen.png`, outside the repository):
"B and D" (a trace in the RAID OVER cue, and REPLAY on the last-raid line), then "Replay: C, but encode raid time to the
color of the pen stroke. Use a fitting color map based on the apps design", "Replay timeline 1", "Replay link: ii".

**What there is to replay** (a week of the study log, about twenty raids): one position per four or five raid minutes;
a third of the raids had one position, the median four. Long raids had gaps of 10 to 20 minutes, and the last position
came up to 20 minutes before the raid's end line. How the raid ended (survived, killed) is in no log Shturmap reads. So
the replay shows where and when the player took screenshots, and says where they were last seen, never how the raid
ended.

- **When.** At a raid's end, in place of today's RAID OVER cue, when the raid has 3 positions or more over 5 minutes or
  more (about half of that week's raids); otherwise today's cue. Never for the log read back at start.
- **The sequence.** 0–1.6 s: RAID OVER enters in the middle as every cue does. 1.6–2.6 s: the title goes, and the band
  slides to the foot of the map and becomes the timeline. Then the trace plays, the raid's length in about 7.5 s, and
  holds 2 s on the end before it fades: about 12 s in all (cues today: 5 s, 7.5 s with a kit, up to 11 s). It takes no
  clicks. With Windows' animation effects off, the end frame shows for the cue's time without motion.
- **The view.** Fitted to the positions with room around them (at least 300 m across), in the part above the band; the
  map recedes under the trace (the ground at 45 % over it).
- **The pen (C).** One line from position to position, drawn on as the time runs. Its colour is the raid's time: the
  player's sand mixed into the ground, opaque, from 22 % sand at the raid's start to all sand at its end, and it widens
  from 1.4 to 3.2 DIP, over a dark collar (the ground at 59 %, 6.2 DIP). Each position is a dot (2.6 DIP) in its time's
  colour on a dark collar, with its minute beside it ("8 MIN", ink on a dark tag) where the tag covers nothing (right,
  left, above, below; dropped when nothing is free, the timeline still has it). The newest position pings as a new
  position does. The last one's tag is "LAST SEEN · 2 MIN BEFORE THE END", framed in sand, as an old position's tag is.
  - *Why this colour map.* Sand is the player's alone, and a sequential map of one hue keeps "one meaning per colour":
    any multi-hue map (viridis, magma, cividis) runs through the map's taken hues, green and teal (extracts), violet
    (transits), amber (quests), red (bosses). Light means late, as the newest position is the brightest thing on the map.
    One hue has little range on a dark map, so width is the second cue (§4, "One meaning per colour, shape as a second
    cue"). Tried on the owner's raid and dropped: sand by alpha (the artwork shows through, and the overlapping pieces
    dot the line), a range from 35 % sand (too little difference), plain sand (no time).
  - *What it claims.* A straight line between positions minutes apart reads as a route Shturmap doesn't know (11
    minutes between two positions in the example). The owner chose it knowing; the minutes and the timeline show the
    gaps.
- **The timeline (1).** In the band: RAID OVER (amber) and CUSTOMS · 33 MIN (ink), in the cue's eyebrow, and "6 OF 9
  POSITIONS" at the right. The raid's length as a 4 px rule, its played part in the pen's colours (so the rule is the
  colour key), the rest `LineStrong`, a tick every ten minutes and at the end, and the playhead (2 × 14 DIP, ink). Above
  the rule a dot per position, in its colour once played and an outline before. Below it a check (`E73E`) per objective
  ticked during the raid and a green triangle where the extract list was read. "33 MIN" at the end.
- **REPLAY (D, link ii).** On Plan's last-raid line, at its right: the play symbol (`E768`) and REPLAY in the status
  words' style, 11 px, ink, as CLEAR PICKS stands beside NEXT RAID. Shown when the last raid qualifies; it plays the
  same cue on the raid's map. Gone when the next raid loads, and at a restart.
- **Positions are kept in memory only**, with their times (today `GameSession._trail` keeps points without times and
  is cleared at the raid's end), until the next raid loads. Never on disk and never in the app log (§2).
- **Study log.** `replay.play` (by itself or by the link, positions, minutes), `replay.skip` (too few positions).
- **DESIGN.md when built:** §1 (the exception), §4 principle 8 (the trail goes at the raid's end: now kept for this),
  principle 11 and "Design system", Motion (the cue's length and its band at the foot), "Map drawing" (the pen, a level
  1 symbol of the player's, and its legend row).
- **Open:** a raid with a transit (replay only the map it ended on, or each map in turn); positions on another floor
  (the floor badge, or drawn as on the ground as in the panel); whether Esc ends a replay early.

## Designed 2026-10-07, deferred: what you have

The owner: "One thing I constantly stumble upon is the fact that I don't know if I already have items that are
required for quests", with screenshots of the key case in the menus as the idea, and two problems named: it would be
unclear why only keys, and the information goes stale. From the first panel's options the owner asked for D (the log,
screenshots and the player's word together) to be designed, and took look A from its panel
(`artifacts\have-panel.png`, outside the repository): "only write down design and defer this one until we decide.
Probably we don't do this." Nothing is built.

**What the logs already say** (the study's push-notification logs, a few dozen sessions over seven weeks): a few dozen
quest-start messages (type 10) came with items, mostly what the quest needs: items to plant or mark with, flares, keys
and keycards, gear a quest asks to be worn, the weapons of some quests. Several dozen completion messages (type 12)
carried rewards, and insurance returns (type 8) the gear that came back. No flea-market sale message was in them (PvE).
On Customs with every quest active, these cover about a quarter of BRING's rows, mostly quest items, and few of the
keys and weapons: screenshots would be for keys and weapons.

**Three sources, one mark.** A mark says where Shturmap learned it and when, never "in your stash" or "missing".

| from | says | taken away when | needs |
| --- | --- | --- | --- |
| the notification log: items handed at a quest's start, rewards, insurance returns (the item and the count, nothing else of the message) | "Given with the quest · 25 Sep" | its quest completes or fails; the objective that uses it is ticked; NOT ANY MORE | nothing new: the log is read already |
| a screenshot the player takes in the menus (the key tool, a case, a stash page): the names on the item cells, read with Windows' text recognition, matched only to what active quests need; a name that fits two items marks neither | "Seen in your screenshot · 5 Oct" | NOT ANY MORE; never a later screenshot that doesn't show it (a reading can miss) | the owner widening §2 (menu screenshots, the inventory), a setting, the texts (README, PRIVACY.md, help, website), a sentence in §1's not-goals (it isn't item scanning by hotkey), and a test that the reading finds keys reliably (one key-tool screenshot and one stash page at the owner's resolution) |
| the player's word: I HAVE IT on the item card, outside raids | "Marked by you · 6 Oct" | NOT ANY MORE; its quests complete or fail | a second thing the player may tell Shturmap (§4, principle 7 names ticks only) |

A count says what part is known ("3 of 7"); a gift counts for its own quest only. Read from screenshots, anything the
picture shows counts, so it isn't "only keys". In the study's own data the rule for taking marks away already shows:
a quest that handed over the cameras it asks to place had its camera objectives ticked during a raid days later.

**How it shows (A).** On a BRING row the line that says where to get the item (amber) says what is known instead: the
stash grid (Segoe Fluent `E80A`, used nowhere else), where it comes from and when, in ink ("Given with the quest · 25
Sep"). A part of a count says its part, then where to get the rest in amber ("4 more: Mechanic LL1 barter · …"). The need
cells beside a quest's name get a thin ink bar under the cell. The item card gets YOURS above GET IT: each source on a
line with the stash grid, then NOT ANY MORE, or I HAVE IT when nothing is known. After a screenshot that showed
something the quests need, one notice ("Seen in your screenshot: 3 keys your quests need."); until the first such
mark, one note under BRING ("Screenshot your key tool in the game: the keys your quests need get a mark."). Not in the
CHECK YOUR KIT cue (it asks whether something is packed, not owned); the planner's order doesn't change.

**If it is ever built:** the log first (no new reading, real coverage today), then the player's word, then
screenshots, after the test and the owner's decision on §2.

**Why probably not:** §1's one job (it edges toward the inventory and hideout trackers the not-goals decline), a new
kind of fact that goes stale, and for screenshots a wider reading of pictures than §2 allows.

## Designed 2026-10-05, to build later: the daily data check

The owner asked for "something … through github automations that regularly checks if new content is out and if we
need to update the app", chose daily and an issue, and said: "The schedule should only be designed now and added to
NEXT.md, we will do this later". Built the same day: CI on every push and the dev build's notice of a new game build
(DESIGN.md §8, "Developer aids" and "Continuous integration"). This is the rest.

**What it does.** Once a day a GitHub Actions run on a Windows runner refreshes tarkov.dev's data and runs what
docs/UPDATES.md steps 1–3 run by hand: the five audits in both modes and the tests that read the data. It compares the
result with the previous run's. When something changed that the rules may not cover, or something broke, it opens one
GitHub issue, or comments on the one still open. It can't see game patches (logs and screenshots are on the owner's PC):
the dev build's notice covers those, with UPDATES.md step 4.

**`.github/workflows/data-check.yml`**
- `on: schedule` at `17 4 * * *` (04:17 UTC, 06:17 CEST; off the full hour, when GitHub's scheduler is busiest), and `workflow_dispatch` for a run by hand. Never on pushes or pull requests: a fork's pull request must
  not reach the data cache or a token that can write issues.
- `runs-on: windows-latest`, `timeout-minutes: 30`, `permissions: contents: read, issues: write`.
- Steps:
  1. Checkout, setup-dotnet from global.json, the NuGet cache (as in ci.yml).
  2. Restore the tarkov.dev cache with `actions/cache` (key `tarkov-dev-<run id>`, restore keys `tarkov-dev-`) into
     `%LOCALAPPDATA%\Shturmap\cache\tarkov-dev`, so unchanged files are asked for with their ETags.
  3. Build (Debug).
  4. `shturmap-cli check --out check.json`: refresh `pve`, `regular` and `pve de`, run the audits in process, write
     the summary below and print it in the safe form.
  5. The tests, against the fresh cache: the cache-reading tests run here; their results go into the summary.
  6. Restore the last summary (`actions/cache`, key `data-check-<run id>`, restore keys `data-check-`) and run
     `shturmap-cli check --compare last.json check.json --issue issue.md`, which writes the issue's text and says
     whether to report.
  7. To report: with `gh`, comment on the open issue labelled `data-check`, or open one. If an earlier step failed
     (the build, a download), report that instead, once ("Data check didn't run: …").
  8. Save `check.json` as the next run's "last".

**The summary (`check.json`).** Per mode:
- Counts: quests, objectives, maps, extracts, transits, traders, items the quests name.
- A fingerprint per quest: a hash over its objectives' ids, types, item and quest-item ids, maps, counts,
  found-in-raid and optional. Per map: its extracts' and transits' ids and names, and its bosses' ids. No prices and
  no texts, so the flea market's prices and new translations don't count as changes.
- The audits' flags, each as audit, kind and the quest's or item's name (FALLBACK, LONG, BREAK, UNKNOWN TARGET,
  unknown objective type, UNKNOWN ITEM, LIST, NO EXIT, NEAR, a quest item handed over with no pickup, a spawn marker
  far from its spawn points), and the totals UPDATES.md's log keeps (effort groups, hand-over folds).
- The tests: passed, failed, skipped, and the failing tests' names.

**When to report.** New, changed or removed quests, maps, extracts or transits; an objective type or kill target the
rules don't know; a flag the last run didn't have; a failing test. Nothing when only prices, texts or the order of
things changed. The first run only keeps its summary.

**The issue.** One open issue at a time, labelled `data-check`, titled like "Data check 2026-10-12: 3 new quests, 2
flags". Its text: the modes; quests new, changed and gone (names); maps and extracts new, changed and gone; the flags by
audit with names; failing tests; a link to the run and to docs/UPDATES.md ("run the audits on the PC for the
details"). A later run comments only with what is new against its own last run. The owner closes the issue after
going through UPDATES.md, which gets a line in its log; it is never closed by itself.

**Public by design.** The repository, its runs' logs and its issues are public. So the check prints and writes
names and counts only: no quest texts, objective descriptions or synopsis phrases (the audits print those on the PC
only), no ids beyond the game's own. tarkov.dev's files stay in the Actions cache, never in an artifact, which anyone
signed in can download from a public repository. No secret beyond the run's own GitHub token.

**Load and cost** (DESIGN.md §3: count before downloading more). One more client, once a day: about 15 requests
(tasks, maps, traders and item names for two modes, the item sources, barters, crafts and hideout, maps.json, German
texts). An unchanged file costs a "not modified" of a few hundred bytes; the item sources change every few minutes
with the flea market's prices, so about 2–3 MB a day. Nothing per player. GitHub's Windows runners are free for public
repositories; about 10 minutes a day.

**To build.**
1. `shturmap-cli check [--out <file>] [--compare <last> <now> --issue <file>]`, in the CLI, with the audits moved
   into functions that return their flags instead of printing them; tests on made-up data for the comparison (what
   counts as a change, what doesn't, the first run).
2. `.github/workflows/data-check.yml` as above, and the `data-check` label.
3. DESIGN.md §8 ("Continuous integration") and docs/UPDATES.md ("When") say it.
4. Two runs by hand (`workflow_dispatch`) on the current data: the first keeps its summary without an issue, the
   second finds nothing new and reports nothing.

**Open for the owner:** should a clean run close an open issue by itself (proposed: no, the owner closes it after the
checks)?

## 2026-10-05: the raid's time as a readout, and the extract list from a screenshot

Built on the owner's word (DESIGN.md §2, "One corner of a screenshot's picture is read", and §4, "Screen anatomy" and
"Map drawing"): the raid card's time is its largest figure over a rule of the raid's length; a screenshot taken in a
raid is looked at for the game's extract list, and the rail and the map say which extracts are the player's. The
example raid of `tools\fake-raid.ps1` is a real walk now, 27 minutes into the raid, with the list read.

**Decided here, for the owner to confirm:** the reading is on unless unticked (settings), which changes what the
README, PRIVACY.md, help and the website say Shturmap reads ("the images are never read" no longer holds).

**To try in the game** (checked against three real screenshots and drawn lists at 1080p, 1440p, 4K and ultrawide;
never in a running raid):
- a screenshot at a raid's start while the list still shows, and one after O twice: the notice, the rail's rows, the
  hollow triangles;
- a screenshot while standing in an extract (the "Stay in the extraction point" box must not be taken for the list);
- a long list (Streets as a Scav, Customs): whether every row fits the corner that is read (62 % of the height);
- the game in another language, with and without that language's text recognition in Windows;
- "Delete position screenshots" ticked at the same time (the list is read within the 5 seconds);
- the readout through a minute change (the figure decodes, one notch runs along the rule) and into the last ten
  minutes (red); with Windows' animation effects off (nothing moves).

**Open:**
- The time left is still the map's raid length minus the time since the start line. The game's own timer stands
  beside the list and alone in many screenshots, but Windows' text recognition doesn't read the game's digits; a
  small digit reader of our own would give a Scav a time left and survive a reconnect. Owner to decide (it reads one
  more thing from the picture).
- Whether "???" should also change which extract EXIT names (today: the nearest on the list, marked or not).
- The website's hero clip still shows the old raid card (re-record when the owner says so). Re-recorded 2026-10-09.

**Words for the owner to check:** "13 MIN LEFT" / "27 MIN IN" / "PAST THE RAID'S 40 MIN" / "TIME LEFT NOT KNOWN";
"NEAREST · NOT CHECKED AGAINST YOUR LIST", "ON YOUR LIST THIS RAID", "ON YOUR LIST · ??? IN GAME"; the line under
EXTRACTS AND TRANSITS ("Not checked against your list yet. Take a screenshot while the game shows your extracts (at
the raid's start, or O twice by default) and Shturmap reads them from it." / "Your list this raid: 6 of 12 extracts,
read from your screenshot at 16:40."); "· NOT ON YOUR LIST"; the notice "Your extract list is read: 6 extracts for
this raid"; the setting "Read the extract list from screenshots" and its note; the legend's new row; help's two
changed sentences.

## Review of 2026-10-04: findings (owner: "Add all findings to docs/NEXT.md and start fixing them")

A review of the code, the design's consistency and hands-off rule, the design elements, missing features, and the
visual language with its linked highlight. The test suite was green (614 tests); the views were checked in the app's
own snapshots (Plan, a raid, a held quest, The Lab, a local raid, a Scav raid). Each item says what is wrong, where,
and the direction of the fix. **Status** is one of: open, in work, done (with the date), owner to decide, needs a run
(a claim from reading the code that a test on a PC has to confirm first). Paths are under `src\`.

**Status after two rounds of fixes (2026-10-04):** the items marked done below are merged, with tests (the suite
went from 614 to 899); DESIGN.md has each change. The second round built D1 (ticking an objective) and E1 (the
highlight down to the objective). The owner then decided the open questions (2026-10-04): P1 (no game logs in the
repository; the history rewrite is the owner's to run), P2 (say it precisely), A5, A21, A27, B4, H3, C3, C5, C9 and
E2 are done as decided, and D3 (deleting position screenshots) was added and built. H4 and C1 were shown as before
and after pictures and merged on the owner's word, and B14 was measured and decided: it stays as it is, said
precisely. The owner ran the history rewrite (P1) and force-pushed it.

**Third round (2026-10-04; owner: "start with the caching check. Go on with the smaller open review items"):** the
caching check (B15) and some 25 of the smaller items, by five agents in worktrees of their own and the main session:
A17, A20, A32–A36, A39, A40, A42–A46, B2, B3, B5, B9, B11, B13, C2, C4, C5, E3–E5, H7, H9, and C6 for symbols of
different rank. The suite is at 1,063 tests. Found on the way and fixed: the raid card's lines stood in Segoe UI
and white instead of the app's typeface; a Scav zone's ring lay over the boss's octagon; "Sniper Roadblock" on
Customs was told to fire a flare. The leftovers followed the same day (owner: "Go on with the small leftovers"): A37, A41, C6, E3's boss names,
E6, H5 and A40's settings text; the suite is at 1,111 tests. Still open: A10 and A26 (need a run). The website's
wording followed the same day (owner: "build the website if something needs updating and push it"): the installer
and running programs, the delete setting, the three tile maps, ticking an objective, and where the game is
installed, each in the README's words.

**To try with a mouse** (built and checked through the developer script's `hover` and `point` steps, which call the
code behind the pointer's events; no real pointer moved over them): a raid card's objective line, a need cell, a
gold "Key: …" line and a folded card's glyph, and a click on each (where their cards open); a quest's name clicked
at its start (A3); closing the window with Alt+F4 while the pointer rests on a quest (A47).
From the third round, with a real mouse and keyboard: "+" on a US layout (Shift and "=") and on the number pad;
Esc in a popped-out card with a nested card open; with another map picked in the MAP list, a click on a quest row
and on a pen in the open Plan card (the map must stay), then on the map's row in the list (it switches); a card
held in Plan while a raid starts (it closes); the window narrowed to 900 px in a raid, and the three lights'
tooltips; moving from a quest's row onto its card and across its gaps and headings (the quest stays lit), and from
a BRING row onto the item's card (locks and loose squares stay); a padlock on the map (its quests take the weaker
tint); a second copy of the game started while Shturmap runs (it follows the newer logs, with a notice).
From the leftovers: Down and Up through the rail's rows in Plan and in a raid, Enter on a quest row, an item row
and a map row, P on a quest row, Esc; the mouse moved after stepping (the highlight jumps to what is under it) and
left resting over the rail while stepping (the keyboard's row keeps it); typing P and Enter in the Report dialog
(they must stay text); the MAP list opened (its arrows choose a map; closed, they step through the rows now);
Enter on a focused button with no row active (it must still click); a boss's name in the raid line and under a
map's name in Plan (its spawn zones light up, and a zone tints the name); "ALL 15 ↓" in the EXIT row (the list of
ways out comes to the top of the rail); two ways out at one place on the map, each with its own tooltip and click.
From the evening of 2026-10-04: in Plan's map list, rest on a glyph until its card opens, then move right across
the other glyphs and need cells to the card, slowly (the card must stay, and no other quest's card may take its
place; resting on another glyph still opens that one); the same from a quest's row down and right across the rows
below; a click on a glyph of a map that isn't open (its map shows, the row keeps its glyphs, the card is held).

**Words for the owner to check, from the third round:** "Newer game logs in <folder>: quests and raids follow that
game now."; "NO POSITION YET · PRESS PRTSC OR MOUSE3" (status bar, in a raid only); "CLICK ITS ROW TO PLAN IT"; the
lights' tooltips when their words are hidden; the legend's new rows (the ping, a pick's cyan padlock, the gold
chevrons) and "for the first 45 seconds a cone shows…"; "No game data. … Shturmap tries again in 2 minutes; if it
keeps failing, please report it."; "Showing English: the German texts couldn't be loaded. tarkov.dev answered 503.
Shturmap tries again in 2 minutes."; "German texts loaded.".
From the leftovers: help's four new key lines (↓ / ↑, ENTER, P, ESC); "ALL 15 ↓" and its tooltip; settings'
"Updates: the updater couldn't start this time, so Shturmap isn't asking for new versions. Its log says why (LOG
FOLDER, below). Running Shturmap's Setup again repairs it; your data stays."; the README's new header.

**Found while fixing:**
- **A47. The app ended with an access violation when it exited while the map was pulsing** (a quest pointed at): the
  pulse's timer asked for a frame while the drawing surface was taken down. Every exit path now stops the window's
  timers and the map's drawing first (`MainWindow.StopForExit`). Status: done (2026-10-04; a script that exits while
  pointing at a quest ends with exit code 0, before 0xC0000005).
- **A43.** The LOGS light says "Logs live" for ten minutes after every start, also with the game closed: reading the
  old session at start counts as activity (`LogTailer.LastActivityUtc`, `GameSession.LogsHealth`). Status: done
  (2026-10-04: only what the game writes while it is followed counts).
- **A44.** A log longer than one read (4 MB) is cut short when the game starts a new session before it is read
  through: the rest of the old session is skipped. Status: done (2026-10-04: a session is read to its end first).
- **A45.** With A6, a picked quest that has work on five or more maps gives more than four map cards. Status: done (2026-10-04): kept and said, a
  pick's maps are always listed (Plan shows the maps as a short list). Since that evening picks are kept per map
  (owner: "Store the selected quests per map"), so a pick brings up only the map it was made on.
- **A46.** With A31, a translation file that keeps answering with a server error on a first load means no data
  until it recovers (a notice and a retry every two minutes), where it used to fall back to English with an untrue
  notice. Decide whether English with a true notice would serve better. Status: done (2026-10-04): the data loads in English with a notice that
  says so truly, and the game language's texts are asked for again in the background, with a growing wait.
- **Words for the owner to check** (new with A1 and A2): "Got a position, but the game's log shows no raid, so it
  isn't shown."; "Got a position, but Shturmap can't tell which map this raid is on, so it isn't shown."; the
  last-raid line's "end not in the log"; the map's label "LOOKING AT WOODS · THE RAID IS ON CUSTOMS · YOUR NEXT
  POSITION SHOWS IT AGAIN"; the raid card's title "MAP NOT KNOWN".
- **D1 changed the README's sentence on quest tracking** ("Nothing has to be ticked off", with what a tick is
  for). The website says the same since 2026-10-04.
- **Words for the owner to check, from D1:** the tick's tooltip, "Done · ticked by you, 4 Oct" on the card, "DONE"
  on the raid card's line, and the new paragraph in help.

### P. Before the repository is public

- **P1. The fixture scrubber missed ids.** `tools\make-log-fixtures.ps1` masks profile ids only after "profileid";
  the committed log fixtures kept ids on `[Transit]` lines and the push channel's ids in the notification logs.
  Fix: mask every id that isn't public game data (quests, traders, item templates), scrub the fixtures, and a test
  that fails on any id that is neither public nor a placeholder. Then rewrite the history, as on 2026-10-02, before
  the repository is made public (owner's go needed: it changes every commit). Status: done in the tree (2026-10-04, owner: "I don't think logs should be in the repo at all if not needed"): the game
  logs are out of the repository (ignored by git, kept on the PC; the replay tests skip without them). The history
  was rewritten and force-pushed by the owner the same day: no commit holds `tests/fixtures/logs` any more, and a
  check of every commit against the list of the real ids found none. Left for the day the repository goes public:
  GitHub can keep the old commits reachable by their hashes for a while, so ask GitHub's support to remove them, or
  publish from a fresh repository. Every commit's hash changed with the rewrite. Done since by the second way
  (2026-10-07): the repositories under `shturmap` are new ones that got only the rewritten history, and the old
  organization was deleted with its repositories, so none of the old commits is in a repository that goes public.
- **P2. Velopack's updater lists the running processes.** Its `Update.exe` imports `EnumProcesses`, `OpenProcess`,
  `QueryFullProcessImageNameW` and `TerminateProcess` and logs "Checking for running processes" (it closes what runs
  from the install folder, at install, when an update is applied, on RESTART NOW and at uninstall). README and
  DESIGN.md §2 say Shturmap never opens a handle to the game and checks no processes; `SafetyTests` reads `src\` only.
  Which access right it asks for is unchecked. Status: reworded (owner, 2026-10-04): README "The installer and running programs", DESIGN.md §2, help, the release
  notes, and the website since the same day; the updater opens every process with the rights to read its path and
  to end it. Open: applying updates without looking at other processes.

### A. Code

The first twelve are the ones a player meets.

- **A1. A map picked in the MAP list during a raid takes the raid with it.** The next position, the status bar, the
  raid card and RAID OVER go to the picked map (`Shturmap.Session/GameSession.cs`: `MapForFix`, `SelectMapAsync`).
  Fix: the raid's map comes from the tracker; a pick in a raid is a look, and the raid's map is back with the next
  position. Status: done (2026-10-04; a map picked in a raid is a look, and a label on the map says so).
- **A2. A raid whose end never reached the log stays "in raid".** After a game crash or Alt+F4 in a raid, the next
  start shows "IN RAID · … · 1,310 MIN", and later a day-long last raid (`Shturmap.Core/Raid/RaidTracker.cs`,
  `Shturmap.Session/RaidStatus.cs`). Fix: an open raid that can't still be running (older than the map's raid length,
  or followed by a newer log session) is closed as "end not in the log", with no length. Status: done (2026-10-04; closed once it is older than the map's raid length plus 30 minutes, 2 hours when the
  length isn't known, or when a newer log session starts).
- **A3. A held card closes on the next mouse move** when the click was on the left of its row: the 240 px rule
  measures from the card alone (`Shturmap.App/Controls/CardStack.cs`: `PointerAt`). Fix: measure from the card and
  the row or marker it opened from. Status: done (2026-10-04).
- **A4. "Distances from your screenshot 1 min ago" stays at 1 min** until the next snapshot
  (`Shturmap.App/MainWindow.xaml.cs`: `RaidFixNote`). Fix: set it with the clock. Status: done (2026-10-04; it says the age the status bar says).
- **A5. Follow my position switches itself off for good on any drag**, also between raids with no position on the
  map (`Shturmap.App/Controls/MapView.cs`, `MainWindow.xaml.cs`: `FollowStopped`). Fix now: a drag or fit with no
  position on the map leaves following alone. Owner to decide: whether a drag in a raid should switch it off for
  later raids too (today), or only until the next raid starts. Status: done (2026-10-04; owner: only the toggle turns following off, a drag or a zoom is free, and the next position
  centres the view on the player at the current zoom).
- **A6. A pick on a map outside the planner's best four never shows** (`Shturmap.Core/Planning/RaidPlanner.cs`:
  `Rank` cuts to four before `Shturmap.Session/Planning.cs`: `Suggest` puts picks first). Status: done (2026-10-04; every map with a pick is shown, also beyond four).
- **A7. The Lab, Labyrinth and Icebreaker opened offline stay a sheet until restart**: once the tiles count as
  unavailable, nothing asks for them again (`Shturmap.Map/MapScene.cs`: `IsSheet`, `MapRenderer.Render`). Status: done
  (2026-10-04; asked for again every 30 s).
- **A8. BRING's source line can need the quest it serves** ("Ragman LL2 · 41,283 ₽ · after Dandies" for the beanie
  Dandies asks for; `Shturmap.Session/ItemCards.cs`: `Sources`). Fix: an offer that needs a quest the log hasn't
  seen completed comes after every other way. Status: done (2026-10-04; barters behind a quest too).
- **A9. One unexpected notification ends log following without a sign**: the tail loop catches only file errors,
  the parser throws on shapes it doesn't expect, and the backfill has no catch (`Shturmap.Game/Logs/LogTailer.cs`,
  `Shturmap.Core/Logs/GameLogParser.cs`). Also: what is already in the log at start must all count as replay, the
  last line and anything past the first 4 MB included. Status: done (2026-10-04).
- **A10. An ordinary Windows shutdown with Shturmap open likely reads as "closed unexpectedly"** at the next start:
  with Fast Startup the tick count isn't reset, and nothing handles the Windows session ending
  (`Shturmap.App/App.xaml.cs`). Status: needs a run (shut down with the app open, then start it).
- **A11. Popped-out cards all close, and are forgotten, when the mode changes** (the data is empty for a moment;
  `MainWindow.xaml.cs`: `RefreshPinned`); closing the main window may save an empty list the same way. Status: done
  (2026-10-04; a card whose quest isn't active in the mode shown closes but stays saved).
- **A12. The quest card's BRING doesn't add up counts** ("MS2000 Marker" where Plan says "×3";
  `Shturmap.Session/QuestCards.cs`: `Needs`). Status: done (2026-10-04).

Smaller, in the map:

- **A13.** In a raid, picked markers are drawn under other quests' markers (`MapRenderer.Render` groups by step-back
  measure). Status: done (2026-10-04).
- **A14.** A previewed map's zoom limit stays after the preview ends (`Camera.Restore`). Status: done (2026-10-04).
- **A15.** The scale bar averages both axes: up to 27 % off on Icebreaker (`MapRenderer.Scale`). Status: done (2026-10-04).
- **A16.** The tile cache (192) is smaller than a 4K view needs, which reloads without end; a tile that doesn't decode
  counts as missing for the session (`MapTiles`). Status: done (2026-10-04; the view's own tiles are never dropped, and a saved tile that isn't an image is
  thrown away).
- **A17.** While anything is pointed at, the whole map is drawn again about 60 times a second; a row rebuilt under the
  pointer keeps its focus (`MapView`, `Linked`). Fix: stop when the window isn't active, let go when the row goes.
  Status: done (2026-10-04): the focus, and the layout is kept between frames while nothing it is made
  from changes (how much a frame gains wasn't measured).
- **A37.** A cluster is left out when its middle place is out of view though others are in view; the hazard hatch
  runs over a zone's whole box; an SVG floor is read during the first paint that shows it. Status: done (2026-10-04): the cluster; the hatch was clipped already and now draws only its lines in view; floors are read ahead, off the drawing thread.
- **A38.** "A switch listed for every extract links nothing" uses a strict rule where `ExtractRules` uses "most".
  Status: done in the map (2026-10-04); `ExtractRules` still counts by itself.

Smaller, in planning and data:

- **A18.** Every `findItem` objective is planned as found in raid, also where the data says it may be bought
  (`RaidPlanner`: `Plan`, `WhyProgress`). Status: done (2026-10-04).
- **A19.** A download that stalls after its headers never ends: "Loading game data…" for good
  (`Shturmap.Data/Http/CachedHttp.cs`). Status: done (2026-10-04; 30 s without a byte ends it).
- **A20.** Another install with a newer log session (a test server, a leftover install) is followed for the whole
  run; discovery decides once (`Shturmap.Game/Install/InstallLocator.cs`, `GameSession.LookAgainAsync`). Status: done
  (2026-10-04): discovery runs every 30 s for the whole run, another install's newer log session is followed with a
  notice, and a folder the player chose is never left.
- **A21.** "N min left" is the map's raid length minus the time since the raid's start line: computed, and wrong
  after a reconnect or for a local Scav raid shown as a PMC's. Status: done (2026-10-04: "40 min raid · started 21:02"),
  and changed again that evening on the owner's word ("how much time we're in the raid and how much time is left.
  This is crucial information"): "12 min in · 28 min left" on a line of its own, with a tooltip that says how it
  is made; a Scav gets no time left.
- **A31.** The session's background loops (backfill, following, looking again) catch only a cancel: one database or
  file error ends them silently. Translation downloads left unwatched after a failed load become crash records.
  Status: done (2026-10-04).
- **A33.** Key rows: an "A or B" row dropped because A is also needed alone doesn't hand its quests over; quest-level
  keys are listed one by one even where they are alternatives. Status: done (2026-10-04): the data's quest-level list is exactly
  the objectives' keys as one flat list, so a key an objective names is that objective's row, with its alternatives
  ("306 or 308" as one row), and a key needed alone takes over the quests of an "A or B" row it satisfies.
- **A34.** "(Flare)" and "(Co-op)" are looked for in the translated extract name, so the rules are lost in another
  game language (`Shturmap.Session/ExtractRules.cs`). Status: done (2026-10-04; the rules read the English and internal names. Found on
  the way: Customs' "Sniper Roadblock" was told to fire a flare).
- **A35.** CHOOSE… and FIND AUTOMATICALLY read every log session on the UI thread. Status: done (2026-10-04).
- **A36.** Closing isn't safe to run twice (`ProgressStore`, `GameSession.DisposeAsync`); the uninstall path can do
  that. Status: done (2026-10-04).
- **A41.** Log times are local without an offset: an hour off across a clock change. Status: done (2026-10-04): spans of time go through one place (WallClock); a raid
  that runs across the whole repeated autumn hour still reads an hour short.

Smaller, in the app:

- **A29.** Cards have no largest height: a long quest's card is cut at the window's edge. Status: done (2026-10-04).
- **A32.** A scene's key is set before its artwork arrives, so a second snapshot can fill the old scene. Status: done (2026-10-04).
- **A42.** A held card stays over the map through loading and the raid; "+" doesn't zoom on a US keyboard; a
  popped-out window has no Esc. Status: done (2026-10-04): cards in the main window close at each step into a raid,
  Shift with "+" zooms too, and Esc in a popped-out window closes the cards opened from it. The two keys are to try
  by hand.

Smaller, in privacy and distribution:

- **A22.** `--update-feed` (in release builds, for testing updates) takes `//server/share` as a local folder
  (`Shturmap.App/Updater.cs`: `LocalFolder`). Status: done (2026-10-04).
- **A23.** "Never" doesn't stop crash records already approved (`App.xaml.cs`: `HandleReportsAsync`). Status: done (2026-10-04).
- **A24.** Only the exact profile folder is masked in the app log: Documents on another drive, a network host's
  name and short (8.3) names pass (`Shturmap.Session/Redact.cs`). Status: done (2026-10-04).
- **A25.** Paths from maps.json and wiki links from tarkov.dev are used without checking the host or the scheme.
  Status: done (2026-10-04: downloads only to tarkov.dev's two hosts and GitHub's raw host, over https; a wiki link
  only when it is an https address on the wiki).
- **A26.** A copy unpacked from the portable zip offers the in-app uninstall; what Velopack then removes is
  unchecked. Status: needs a run.
- **A28.** `ScreenshotWatcher.WaitUntilCompleteAsync` (unused, from the removed OCR feature) would open a screenshot
  image; the watcher's rescan catches too little and isn't re-armed after the folder is recreated. Status: done (2026-10-04).
- **A30.** The User-Agent says "Shturmap/0.1" whatever the version. Status: done (2026-10-04).
- **A39.** `eng\publish-release.ps1`'s "built from this commit" checks can pass on a build from a changed tree;
  `--send-report` in a release sends without a click. Status: done (2026-10-04): `eng\release.ps1` refuses a changed
  tree (`-AllowDirty` for a local try) and leaves a note that `eng\publish-release.ps1` checks; in a release
  `--send-report` only opens the Report dialog with the text, and Send is pressed by hand. Neither script was run to
  the end for this (a release build takes the DSN and packs installers); the next release is their first real run.
- **A40.** If Velopack fails to start, an installed release silently uses the developer data folder. Status: done
  (2026-10-04): where the exe runs from then says which install it is, so the release keeps the player's folder,
  and the app log says that this run doesn't update. Settings say in that case that the updater couldn't start, where the reason is and that the Setup repairs it.

Wording: **A27.** Numbers and dates follow Windows' language inside English text ("5.000 ₽", "3 Okt" on a Windows set
to German); snapshots hide it, since they run in en-US. Status: done (2026-10-04: the formats belong to the app's language, English today, in one place, `UiLanguage`,
for the languages to come).

### B. The design's consistency

- **B1. Cyan has three meanings**: picks, Follow my position "on", the dev icon; the colour table gives "on" to
  amber. Fix: the toggle's "on" in amber. Status: done (2026-10-04).
- **B2. Green means "PMC extract" on the map and "any way out" in the rail**: EXIT and every extract row's distance
  are green, transits and Scav extracts included; a quest card's "ACTIVE" is green too. Fix: the rail's rows in the
  colour of their kind. Status: done for the ways out (2026-10-04); a quest card's "ACTIVE" is amber since the same day.
- **B3. Sand** is the player's, but floor badges and the squares for loose items use it too. Status: done
  (2026-10-04): a floor badge wears its marker's colour, a loose item's square is ink.
- **B4. "Primary text ≥ 14 px"**: the raid card's objective lines are 13 px, the status bar 12, directions 11, and
  about fifty places are under 12; "numbers in a monospaced face" isn't true either (figures are Bahnschrift).
  Status: done (2026-10-04; owner: "the 14px rule is not necessary"; the principle no longer says either).
- **B5. The legend has "a done objective"**, which can't happen yet (D1 makes it real), and lacks the gold chevrons
  of a pointed-at quest, a picked quest's cyan padlock and the ping. Status: done (2026-10-04).
- **B6. "Never ask" and "no modal dialogs"** have exceptions the principles don't name: the Report dialog, the
  uninstall and crash questions, the side switch. Status: done (2026-10-04).md).
- **B7. DESIGN.md housekeeping**: principles are numbered 1–8, 11, 9, 10, 12; the rail is 380 px in one place and
  384 in another; §9 still says "fading marker"; §6 says "needs Dorm room 114 key" where the app says "Key: …"; help
  says "pen" and "highlighter"; the quest card's wiki link lacks "↗"; a held card is replaced after 0.65 s from a
  list, not 0.4 s. Status: done (2026-10-04).
- **B8. Stepping back**: other quests' zones fall to about 35 % (the table says 62 % and 80 %), hazards aren't in the
  table, and a picked quest's doors step back like any lock. Status: done (2026-10-04).
- **B9.** The facing cone shows for 60 s, the cards' facing-relative directions for 45 s. Status: done (2026-10-04): 45 s for both.
- **B10. PRIVACY.md and README** still place reports in help, say Sentry keeps country and town (it is told not to),
  and don't say that tarkov.dev's image service sees which icons and map tiles are asked for. Status: done for
  PRIVACY.md (2026-10-04: the feedback button and settings, what Sentry is told, refused reports, a section on
  downloads). The owner checked a received report in Sentry on 2026-10-09: no address, but its "User Geography"
  held the owner's town. Sentry derives it from the connection with IP storage off; fixed by an empty
  `user.ip_address` in every report (`ReportSender.Sealed`) and a scrubbing rule for `$user.geo.**` in the project
  (the owner added it, 2026-10-09). Checked the same day: the second test report's "User Geography" is empty (owner).
- **B14. On the three tile maps the tile requests follow the view**, and with Follow my position on, the view
  follows the player: the image service could tell roughly where on the map the player is, while DESIGN.md §2 says
  no position is sent anywhere. Fix: when such a map opens, fetch the whole map's tiles at the zoom levels used (they
  stay in the cache for a month), so later requests say nothing about the view. Status: decided (owner,
  2026-10-04): it stays as it is and is said precisely, in PRIVACY.md and in DESIGN.md §2 under "Never share a
  position". The fix as proposed was measured first: a whole map at every zoom level is far too much (The Lab:
  3,525 tiles a layer, about 290 MB, three layers; Labyrinth 4,486 tiles; Icebreaker 17 layers). Fetching each
  layer whole up to zoom 4 only (about 10 MB a layer for The Lab) would have ended the view-dependent requests,
  but the picture turns soft when zooming in further, and the owner didn't want that ("I would like to avoid
  that"); the same plus deeper tiles outside raids would have left soft and sharp patches side by side in a raid.
  The website says it too, where it says what is sent (2026-10-04).
- **B15. The caching check (owner, 2026-10-04: "Check if we properly cache the maps so we don't put unneccessary
  load on tarkov.dev").** Status: done (2026-10-04; DESIGN.md §3, "No unnecessary load on tarkov.dev", has the
  numbers). The cache works as meant: requests go out only at a start or a mode change, a saved copy within its
  age costs nothing, and all three hosts answer "not modified" to an older copy's ETag (tried against each). A
  start a day after the last is some 15 requests and about 3 MB compressed; the item sources are the largest part
  (1.9 MB, they change every few minutes, fetched once a day). Two wastes were found and fixed: a tile or picture
  the server doesn't have was asked for again in every session (now remembered for a week), and while the data
  couldn't be loaded every running Shturmap asked again every 2 minutes without end (the wait now grows: 2, 4, 8,
  16 minutes, then every 30). Left as they are, with the reasons in DESIGN.md: the hour for quests and maps, the
  30 days for tiles (the service says 7 days, but tiles are files that haven't changed since January 2025 and
  are checked with their ETag after the 30).
- **B11. DESIGN.md against itself and the code**: §5 calls every `findItem` found-in-raid while §7 has one "that may
  be bought" (A18); §7's "a named exit" isn't what `QuestEffort` tests; §5's Trader types lack `playerLevel`; §8's
  project table gives Core "name matching" (gone) and calls it pure (`UnpackedCopies` deletes folders); the Steam
  uninstall key isn't mentioned. Status: done (2026-10-04; the first point had gone with A18, the other four are
  corrected in DESIGN.md).
- **B12. Quest states set by hand** still have a code path (`SetQuestStateAsync`, "set by you") though the states
  come from the log alone and the owner declined editing quests by hand (below). Status: done (2026-10-04; old databases' rows of that kind are ignored).
- **B13.** A Plan card is one button: a click on a row or a pen inside an expanded card of a map that isn't shown
  also switches the map ("two clicks, two meanings"). Status: done (2026-10-04): the open card is no button;
  only a row of the map list switches the map.

### H. Hands-off

- **H1.** The window's place isn't remembered (D2). **H2.** Follow can't stay on (A5); off, a position out of view
  needs F, and F needs the window's focus. **H6.** The MAP list is live in a raid (A1).
- **H3. EXIT names the nearest exit for your side**, but on most maps the game opens only some exits, by where you
  spawned, and at the raid's start the nearest is usually not one of yours. The data doesn't say which are open.
  Status: done (2026-10-04: "NEAREST · CHECK YOUR LIST IN GAME" under an extract in the glance, with a tooltip and a
  sentence in help).
- **H4. Plan's other maps are below the fold**: the expanded card is about 840 px with six quests, so the folded
  cards to compare it with sit under it. Status: done (2026-10-04, approved by the owner from a before and after
  picture): the suggested maps are a short list on top (name and counts; the rows of the maps that aren't open keep
  the folded card's glyphs and bring cells), then the open map's card. The fake game has two maps; a look with
  four real ones is still due.
- **H5.** In a PMC raid the extract list is below the card. Status: done (2026-10-04): the list stays where it is, and the
  EXIT row carries "ALL 15 ↓", which brings it to the top of the rail. Another place for the list (above the
  quests, or folded) was not tried: say so if one click is one too many.
- **H7.** What needs a restart today: tiles after an offline start (A7), item sources after one failed download,
  another install's newer session (A20). Status: done (2026-10-04; all three recover by themselves now).
- **H9.** The first-run help can open during a raid; the crash question stays over the map until answered. Status:
  done (2026-10-04): the first-run help opens only outside a raid, a raid's start closes an open help panel, and
  the crash question waits while a raid loads or runs.

### C. Design elements

- **C1. The raid card's objective lines** are tarkov.dev's full sentences plus "Bring: MS2000 Marker" on each of three
  lines; Plan has the short synopsis. Status: done (2026-10-04, approved by the owner from a before and after
  picture): each line and NEXT say the objective by its synopsis phrase ("Mark Stryker"). The need stays on every
  line (owner: "keep the item with each sub-item so it's clear that one is needed"); saying it once under the
  quest's name was tried and declined.
- **C2.** A quest card says the map twice per objective ("… on Streets of Tarkov" over "Streets of Tarkov"). Status: done (2026-10-04, English texts; German texts 2026-10-11).
- **C3. Plan card**: the rank numbers "1", "2" say what the order says; the dashed empty cell stands on most rows;
  the kit cue shows one item twice (to plant, to wear). Status: done (2026-10-04: all three gone).
- **C4. Status bar**: "NO POSITION YET · PRESS … IN RAID" also outside raids; three green lights with words for a
  state that is nearly always fine; below about 1,090 px the three buttons leave the window, which has no smallest
  size. Status: done for the two faults (2026-10-04): the position hint shows only in a raid, and a narrow bar
  drops the lights' words first (they stay in the tooltips). The three lights themselves are as they were.
- **C5. The boss symbol** is mostly its dark collar around a thin red ring, weaker than a quest's disc, and shares
  the diamond's outline with transits; boss labels are red where every other label is ink. Status: done
  (2026-10-04: a solid red octagon of its own, larger than before; its label is ink like every other, red only
  while pointed at).
- **C6.** Symbols at one place hide each other (an extract's triangle under a transit's diamond on Streets). Status:
  done for symbols of different rank (2026-10-04: the one that matters more lies on top; a Scav zone's ring lay
  over the boss's octagon and made it read as a ring). Two of the same rank at one place: done the same day, they stand side by side, each a few pixels beside its place.
- **C7.** The guide line's distance plate can stand on a symbol (it covered a boss marker in a snapshot). Status: done
  (2026-10-04).
- **C9. Help** is 2,350 px tall with 25 legend rows, and opens by itself at the first start. Status: done
  (2026-10-04, owner: "Rest I follow your suggestions"): help lists the symbols on the map shown, the others behind
  one link (`MapLegend.On`). In a Streets raid that is 18 rows and 2,280 px; a map with fewer kinds of things is
  shorter. The texts above the legend are as long as before.
- **C10. The quest types' icons** (owner, 2026-10-04: "the iconography of the quests can be improved. Find the best
  possible icons for each type used, compile a panel of different solutions and how they would look in the app to
  let me decide"). Status: done (2026-10-04, owner, from the panel: "B Solid Other Pictures but with the
  exploration icon from A Solid and with Survive icon from A solid"). The panel was made outside the repository:
  the glyphs until then (Windows' icon font and the app's own crosshair) and five sets from open icon families,
  each drawn in the real app (help's legend, Plan, the map) by a throwaway build. What it showed: thin outlines
  are faint in a map marker, where the glyph is about 10 px; filled shapes read at that size.
  - Now, all from Phosphor Icons (MIT): crosshair, magnifier, open hand, push pin, box, runner, handshake. Seven
    paths in `Glyphs`, drawn filled and fitted by their bounds on the map (`SKPath`) and in the lists
    (`KindGlyph`); the licence is in `THIRD-PARTY-NOTICES.md`, the credit in the README, the table in DESIGN.md §5.
  - Keys, the bring glyph, padlocks and switches still come from the icon font, so the README's Windows 10 note
    stays unless those are redrawn too.
  - The website's pictures and clip, and with them the README's picture, were re-recorded the same day (owner:
    "Make it so") and published. What had to follow in the site repository: Plan is rendered in a taller window
    (the map list pushed BRING out of a 900 px one), the quest card is shorter, the floor picker sits one button
    higher, and four callouts moved.

### D. Features (owner, 2026-10-04: these two; "the rest of d) not")

- **D1. Tick an objective done by hand.** The logs never say an objective is done, so a quest that takes several
  raids keeps leading to places already dealt with. The drawing is there (the done marker, the legend row). To
  build: a tick on the objective (quest card, outside a raid), kept per mode until the log reports the quest
  completed or failed, shown as done "ticked by you", left out of NEXT, the guide line and the planner's count.
  Status: done (2026-10-04: a tick box at the end of each objective's row on the quest card; DESIGN.md "Quest cards",
  "Ticks").
- **D2. Remember the window's monitor, size and whether it is maximised**; first start as today; a saved monitor
  that is gone falls back to today's rule. Status: done (2026-10-04; with a smallest size of 900 × 560).
- **D3. Delete position screenshots after reading** (owner, 2026-10-04: "a mode that auto-deletes screenshots after
  a couple of seconds grace period"). Status: done (2026-10-04): a tick in settings, off unless ticked; a screenshot
  that gave a position is deleted 5 seconds after its name was read, for good; what was in the folder before, menu
  screenshots and every other file stay (DESIGN.md §2, `ScreenshotCleaner`). For the owner to check: the 5 seconds;
  for good rather than to the Recycle Bin; menu screenshots staying; the setting's words. The website's "It never"
  list and a question of its own say it since 2026-10-04. Needs a run with
  the game: that the game's own write is over well within the 5 seconds (a file still open is tried again, so the
  worst case is a screenshot that stays).
- **Declined** (owner, 2026-10-04): adding or removing a quest by hand; opening with the game or with Windows; a
  text size setting. Don't propose them again.

### E. The linked highlight

- **E1. It stops at the quest.** An objective line in the raid card, NEXT and a quest card's objective light the
  whole quest, so the "69 m" line can't be told from the quest's other markers, though markers carry the
  objective's id. Status: done (2026-10-04: the objective's own places pulse, its line carries the tint, both ways).
- **E2. One tint for three things**: the row pointed at, the same thing elsewhere, and what is only related (an item
  row lights its quests and, through them, every other BRING row of those quests). Status: done (2026-10-04: the
  same thing keeps the full tint, what is related gets a weaker one, and an item no longer lights the other items
  of its quests; `LinkStrength`).
- **E3. Places that link nothing**: need cells, folded cards' glyphs, boss names in the raid line and on Plan cards,
  the gold "Key: …" line under an objective, an extract's need item, the item card's "Loose on …" row; entering an
  item card drops the item's locks and loose spots; a quest card's frame and headings let the quest go while it is
  read. Status: done for need cells, the "Key: …" line (when it names one key) and folded cards' glyphs (2026-10-04), and
  since then for a card's whole body, the item card's "Loose on …" row and an extract's need item. Boss names in the raid line and on Plan cards: done the same day (each links to its spawn zones on the map shown).
- **E4.** An "A or B" row links only A (`Planning`: `Alternatives[0]`). **E5.** A quest lights its doors, a door only
  its key. **E6.** The highlight needs a pointer: rows can't be reached by keyboard. Status: done (2026-10-04; E6: Down and Up step through the rail's rows, Enter is the click, P the pen).

### F. Floor plans for the maps without SVG artwork

- **F1. The Lab, Labyrinth and Icebreaker in the style of the SVG maps** (owner, 2026-10-04: "convert it to the
  same style and color coding as the ones where we have proper maps … only do this if we are sure that we can
  achieve high quality"). Status: on hold (owner, 2026-10-04: "Write this up and put in on hold for now"); don't
  take it up or propose it until the owner says so. A trial the same day traced a floor plan out of
  the cached tile renders (`tools\map-trace`): to scale and with straight walls on The Lab's main level (the plan
  differs from the render's floor by 0.003 % beyond 2 px of an edge), ragged on Labyrinth. That settles the
  geometry, not the look: a hand-drawn map chooses obstacles, stairs and landmarks, and the traced plan has
  floor and not-floor only. `docs/MAP-TRACE.md` has what was measured, the steps, the gates a map must pass and
  what the owner decides; it is written so that a map keeps its render unless every gate passes. Icebreaker is
  left out. Pictures of the trial are outside the repository. Looked up the same day: re3mr has hand-drawn plans
  of Labyrinth and Icebreaker under CC BY-NC-SA 4.0, which could be traced in place of the renders (the file's
  "Other maps that exist"); whether they are to scale is not measured, and using them is the owner's decision.
  The file's "Where this stands" has what is known, what isn't, and the order to take it up in.

## Queued for Monday, 2026-10-05 (owner, 2026-10-03: record now, implement when there's quota again)

**Status, 2026-10-03 evening:** the quota came back the same day and the owner said "go ahead with the open TODOs".
Done and merged (DESIGN.md has each decision): 1, 2, 3, 5, 6, 7, 8, 9, 11, 12, 13, 14 (with the changelog the
owner added), 15, 16, and 17 (the design system, §4). Still open:
- **4, click checks:** in the installed dev build ("Shturmap DEV", F12 for the developer view), with the owner.
- **18:** item 2 below (local vs server raids), and the release checklist (on hold until the release).
- Version: still 0.2.0, though much has changed since the build sent to a friend; ask before the next one goes out.

**Later the same evening, also done:** 10 (the FAQ, heading "Is this allowed? Can I get banned?", published with
the website update on 2026-10-03: new media from the newest app, release wording, design-system CSS; the README's
"Before you install"); tarkov.dev's tile renders for The Lab, Labyrinth and Icebreaker; landmarks (locked doors and
keys, switches, Labyrinth's traps); minefields and border-sniper zones, clipped to the drawn map; AI squads (Rogues,
Raiders, cultists…) as spawn markers; entry items in BRING; extract–switch links (none link with today's data);
"Uninstall Shturmap…" with optional data deletion; feedback · help · settings as three buttons; the no-game
fallback (item 6 below); the kit reminder while a raid loads.

The items below are kept as written, for their reasons and quotes.

1. **A separate data folder for dev builds** (proposed, waiting for the owner's go). Today the installed release
   (`%LOCALAPPDATA%\ShturmapApp`) and the folder build (`artifacts\Shturmap`) share `%LOCALAPPDATA%\Shturmap`: one
   database and settings, one app log (diagnostics mix both), one study log (dev starts pollute the owner's data),
   one outbox; folder builds have the DSN, so their crashes reach Sentry too. Proposal: installed copies keep
   `%LOCALAPPDATA%\Shturmap`, every other build uses `%LOCALAPPDATA%\Shturmap-dev` (`AppPaths.CreateDefault`), the
   download cache stays shared (`CacheRoot`), plus a `--data <folder>` switch. Quest states rebuild from the game logs;
   only manual quest states stay with the release. About 20 minutes with tests. The owner then installs the 0.2.0
   Setup beside the dev build.
2. **One symbol, one meaning: the flea market's shopping bag** (owner to decide). `E719` is both the Find-in-raid
   quest type and the flea-market source on item cards (found by the symbol check, DESIGN.md "one symbol, one
   meaning"). Suggested: a price tag for the flea market.
3. **Old release files** in `artifacts`: `Shturmap-0.1.0-win-x64.exe`/`.zip` (+ `.sha256`) are superseded by
   `Shturmap-Setup.exe` 0.2.0; delete them on the owner's OK so there's one file to send.
4. **Not yet verified by a click** (snapshots and tests only): the pop-out button, RESTART NOW / DOWNLOAD and the
   Updates setting, the study-log switch (dev build only since 2026-10-03), Copy diagnostics, LOG FOLDER, LICENCES, PRIVACY, the report dialog's Send,
   the crash question's buttons, the effort hairlines and new labels at 125 % and 150 % display scaling. Ask the owner
   to try them in the installed 0.2.0, or walk through them together.
5. **PROGRESS rows look less important than they are** (owner, 2026-10-03: "the quests under progress have a
   different styling, which in some way makes sense since they are not to be completed during this raid. However,
   there is a bit of a weird cognitive mismatch since this encoding suggests less importance. This should be fixed.")
   Today the Plan card draws PROGRESS rows muted (name, glyph and trader portrait dimmed), which reads as "disabled"
   or "unimportant", while these quests are as much the player's work this raid as COMPLETE ones. Direction to
   propose before changing anything: same ink and strength as COMPLETE rows; the section heading alone says
   "progress", and if a row needs more, a factual note instead of dimming (e.g. "2 of 5 objectives here", or why it
   can't finish: "needs found-in-raid items", "kills over several raids"), taken from what the planner already knows.
   Check the raid card's rows and the folded cards' glyphs (gold to complete, muted to progress) for the same issue,
   and keep "muted = after the raid / at a trader" (objective rows) as the one meaning of muted. DESIGN.md decision.
6. **BRING should also cover weapons and gear that kill objectives require** (owner, 2026-10-03: "There are others
   that still require items, such as doing kills with certain weapons or weapon classes. This is not reflected in
   the app, but should still be there. It should be a coherent design so it is still clear what to bring and what is
   needed to solve a quest. Find the best solution here.") To work out on Monday, then propose before building.
   Starting points:
   - **Data** (tarkov.dev `shoot` objectives, PvE, counted 2026-10-03): `usingWeapon` on 64, `usingWeaponMods` on 9,
     `wearing` on 13, `notWearing` on 5 (item lists, some long: a "class" like bolt-action rifles arrives as every
     member's id). Check whether tarkov.dev gives a category to name a class by ("any bolt-action rifle, 18 kinds"),
     and how other objective types carry gear (e.g. `buildWeapon`, `useItem`).
   - **Today's BRING** lists keys ("key for …"), items to plant or mark ("to plant, for Dandies", "to mark, for
     Revision"), found-in-raid items, with icon, source (trader level, flea price) and loose spots. A coherent
     extension keeps that row shape and adds the verb: "to use, for Wet Job" (one of several weapons, or a class),
     "to fit, for Job for a Patriot" (mods), "to wear, for Dandies" (gear). "Don't wear" (`notWearing`: no armor, no
     helmet) isn't an item to bring: a short note on the quest instead, not a BRING row.
   - **Extracts that need an item** (owner, 2026-10-03: "For example in the cease fire quest it is not clear that we
     need to bring a flare from the app"). Cease Fire! asks to extract through Klimov Street (Flare); the extract
     list already says what each exit takes (`ExtractRules`: flare, paracord and ice pick, money, …), but the
     quest's BRING doesn't. An `extract` objective that names an exit (internal key, `GameData.ExtractKeys`) whose
     requirement is an item gets a BRING row: "to leave through Klimov Street, for Cease Fire!" (the flare, with
     icon and source). The same for `useItem` objectives that use an item (e.g. Airmail's red signal flare), if
     BRING doesn't list them yet. Check which quests this touches with an audit over the data.
   - **Rules that hold:** only what the data says (Shturmap knows no inventory, so never "you have it"); one row per
     item or class, listing the quests it serves; the same in Plan's BRING, the raid card's BRING and the quest card;
     the synopsis line already names the weapon in words, BRING adds icon, source and price. Check the effort
     grouping (kill conditions) and the synopsis stay consistent with it.
7. **"~17 min walking" on the map cards** (owner, 2026-10-03: "probably misleading since it is not clear on which
   quests this is related to, also it's also not realistic since you rarely constantly walk in the game. Either get
   rid of this or find a better solution.") It comes from `RaidPlanner`'s route length over the places of the
   quests it lists, at a walking pace. It is also an estimate shown as a figure, which the owner's truthfulness rule
   (only what was read; no estimates in the UI) already argues against. Recommendation to bring on Monday: remove
   it, from the cards, the cue and anywhere else it appears (and its DESIGN.md mentions). If the owner wants
   something in its place, only a fact with a clear subject, e.g. "objectives in 3 places" or the straight distance
   between the two farthest places of the listed quests, and never a time.
8. **"IN THE MENUS" while the game isn't even running** (owner, 2026-10-03: "This is misleading. Find a better
   wording that is coherent.") The status bar shows the raid phase `Menu` by default, so a closed game reads as "in
   the menus". To work out on Monday, from the logs only: no process checks, since the boundary (DESIGN.md §2) rules
   out touching the game's process, and listing processes is close enough to keep out. Check the owner's
   application logs for a line the game writes when it quits, and whether a session's log simply stops. Likely
   shape: the status says only what the log shows. "GAME CLOSED" after a logged quit; "IN THE MENUS" only while
   the current log session is live and in the menus; otherwise the plain "NOT IN A RAID", which is always true.
   Keep the words coherent with the other states (raid loading, in raid, raid over) and the tooltips, and update
   help and DESIGN.md.
9. **The status lights and words at the top right don't line up with the "?" button** (owner, 2026-10-03). After the
   lights were raised 2 px onto the middle of their capitals (`baeb938`, Sentry SHTURMAN-2), the row of lights and
   capitals and the 28 px help button are still centred differently: the words' text box versus the button's box,
   and the "?" glyph inside it. Fix so the capitals, the lights, the "?" and the button's frame share one middle line
   (the left side too: logo, mode label, raid state). Check with an enlarged snapshot crop, as for SHTURMAN-2, and at
   125 % and 150 % scaling.
10. **An FAQ at the end of the website, for new and experienced players, and maybe in the README** (owner,
    2026-10-03: "At the end of the website, put a FAQ that related to new and expert gamers and go into the 'is this
    cheating' thing based on our research. Maybe also add this to the GH readme if it makes sense.") This is the
    owner's go for a website change; do it in one pass with the release's website items ("At the production release",
    item 5: risk line, article 437, PvP sentence; the "nothing is sent" wording; media refresh), and draft before
    publishing.
    - **New players:** what it is and what it isn't (a quest planner and a map of where your last screenshot was
      taken); how the position works (press your screenshot key); PvE and PvP; which
      maps; game languages; what it costs (nothing) and what it sends (only reports you send, the update check).
      "Do I need to know the maps?" was in the first version and came out again (owner, 2026-10-04: "Remove the
      'Do I need to know the maps?' question from the FAQ"); don't put it back.
    - **Experienced players, "is this cheating?":** answered from the research (DESIGN.md §2): what it reads and
      never does; BSG's support article 437 describes the coordinates in screenshot names (a source, not an
      endorsement); the License Agreement 4.3.4 and no guarantee against sanctions; no BSG statement on such tools
      and no verified ban found, as of 2026-10-03; the PvP debate; never shares a position, never takes a
      screenshot for you; open source, so anyone can check.
    - **Open for the owner:** earlier the owner wanted the site to avoid the word "cheating" and instead say what it
      relies on and never does (2026-10-02). An FAQ answers players' own question, so propose the question's
      wording (e.g. "Is this allowed? Can I get banned?" versus "Is this cheating?") before writing it.
    - **README:** if it makes sense, the three or four questions that matter before installing (allowed / banned /
      PvP / what it sends), linking to the website's full FAQ; no duplication beyond that.
    - Follow DESIGN.md §2's wording rule (no cheat-seller vocabulary, no hint of approval).
11. **No big "POSITION 7 MIN OLD" over the map; the age goes beside the marker** (owner, 2026-10-03: "Get rid of the
    'Position X minutes old' big title feedback in the app. Put it next to the marker"). Today, in a raid, a position
    older than `StaleAfter` (MainWindow.xaml.cs, about line 375) shows in big type over the map (MainWindow.xaml,
    about line 717; an earlier decision from the study log: positions came about every 8 minutes and were often
    minutes old when the app was looked at). The marker already carries a dashed ring and an age tag ("4 MIN") once
    a position is a minute old (cartography change 5), and the guide line's plate says "69 m · 4 MIN". So: remove the
    big type; check the marker's age tag reads well enough at a glance on its own (size, contrast, the collar), maybe
    "7 MIN OLD" once past `StaleAfter`; keep the raid card's note ("Distances from your screenshot 7 min ago").
    DESIGN.md: record that the owner replaced the earlier decision, with the date.
12. **Other markers fade too far while a quest is highlighted** (owner, 2026-10-03: "the other markers are rendered
    too faintly, they can be barely made out anymore, but are still pretty important"). `MapRenderer.Render` draws
    everything not in focus in one layer at `1 - 0.72 × Dim`, so at full dim other markers keep 28 %, labels
    included. Rework so the focused quest still stands out, but the rest stays readable:
    - fade much less, around 55–65 %, and check it on the real artwork, which is already receded;
    - or step back by colour, not opacity: desaturate other quests' markers and keep their shape and collar crisp;
    - never fade what's always needed: extracts and transits for your side, the player, boss and sniper markers
      maybe less than quest markers;
    - fade the labels of the others more than their symbols.
    Compare candidates side by side in snapshots (a highlighted quest on Customs and Streets) before choosing, and
    record the decision in DESIGN.md (the four-level rule in "Map drawing"). **The raid view matters most** (owner:
    "This is probably especially relevant in the raid view"): there a quest is often kept highlighted for a long
    time, while extracts, other objectives nearby and bosses still matter at a glance. So judge the candidates in a
    fake-raid snapshot with a kept quest (`tools\fake-raid.ps1 -ShowQuest …`), and consider fading less, or not at
    all, in a raid than in Plan.
13. **Mark optional objectives on the map** (owner, 2026-10-03: "In the quest markers, encode somehow when a marker is
    optional. It might still be very relevant for a quest."). tarkov.dev flags objectives as `optional`; the planner
    already leaves them out of "can be completed" (`RaidPlanner`), but the map draws their markers like required
    ones. To do on Monday:
    - Count how many optional objectives have places on the map, per map, and which quests they belong to.
    - Find an encoding that respects "one symbol, one meaning" (DESIGN.md). Already taken: the hollow gold ring
      (one of the possible places), muted grey (done or after the raid), the dashed ring (an old position), and the
      badge corners (floor at the upper right, cluster count at the lower right). So a new, distinct mark is needed,
      e.g. a small "OPT" tag beside the label, or a lighter collar style. Mock up two or three and compare them in
      snapshots.
    - Say the same in words where objectives are listed (quest card, raid card): "optional".
    - Add a legend row, and record the decision in DESIGN.md.
14. **A dev view to play a fake raid from inside the app** (owner, 2026-10-03: "Implement a Dev view that allows to
    switch between modes and can fake-play a raid that reacts to what I do in the app and 'fake-updates' the
    position on demand. This one should not be exposed in the production version, only in the dev version. I want
    to use this to check the UI without having to play the game.") Builds on what exists: `--fake-game <folder>`
    (the app reads a fake game folder's Logs and Screenshots), `tools\fake-raid.ps1` (scripted lines),
    `shturmap-cli simulate`, `Demo.cs`. Plan to propose on Monday:
    - **A side panel, only in dev builds,** compiled out of releases (a build property or `DEBUG`, not just
      hidden). A test checks that the release assembly has none of it. It runs against its own fake game folder
      and the dev data folder (item 1), never the real game or the owner's data.
    - **It writes real log lines and screenshot names** into the fake folder, so the whole pipeline is exercised
      as in a game:
      - mode: PvE, PvP, Seasonal (the `Session mode` line, now that the app has no chooser);
      - map; group pick; loading steps; raid start (PMC or Scav, local or server); raid end;
      - quests started, objectives done, quests completed or failed (push notifications);
      - transit.
    - **The position on demand:** click a spot on the map in the dev view (or press a key) and it writes a
      screenshot name with that position, the facing (drag for the direction) and the floor height, as the game
      would. Also "age the position" to check the old-position look, and walk a short path.
    - **Also useful:** trigger the report dialog, a crash question, the update-ready line, a data-load failure
      (offline, 404, 503), and a missing game.
    - Keep it plain and developer-styled. It doesn't follow the product's visual rules, but must never leak into a
      release; DESIGN.md §8 "Developer aids" describes it.
15. **A dev build next to the release, always current, with its own icon** (owner, 2026-10-03: "I want to have a dev
    artifact of the app that always carries the recent updates and is in dev mode. The artifacts should contain a
    dev and release artifact. The dev version should carry a different icon so it is visually clear.") Plan to
    propose on Monday, together with items 1 (dev data folder) and 14 (dev view):
    - **`artifacts\` holds two things:** `artifacts\release\` (the Velopack Setup and packages, as
      `eng\release.ps1` makes today) and `artifacts\dev\` (the dev build). The old 0.1.0 files go (item 3).
    - **The dev build is in dev mode:**
      - the dev view (item 14) and the `Shturmap-dev` data folder (item 1);
      - no reports to Sentry, or reports marked as environment "dev";
      - no update check against GitHub;
      - a window title "Shturmap DEV" and its own app id, Start-menu entry and shortcut names, so it never mixes
        with the installed release.
    - **"Always the recent updates":** either (a) Claude rebuilds `artifacts\dev` after every change it commits (a
      CLAUDE.md rule, with `eng\dev.ps1`), or (b) the dev build is itself a Velopack install on a "dev" channel that
      updates from a local feed in `artifacts\dev\feed`, so the installed dev app picks up each new build at its
      next start. (b) is closer to "always current" without thinking about it; propose both.
    - **Its own icon:** a dev variant made by `brand\build.cs` (exe, window and taskbar icon). Keep the mark and
      change only something unmistakable but safe, e.g. the plate in the app's cyan, or a small "DEV" band. The
      owner checks logo changes against symbol resemblances (DESIGN.md §4, "Logo"), so show a panel of two or
      three variants first.
16. **Pick several quests for the coming raid** (owner, 2026-10-03: "I like how easy to use the app is right now
    and how little interaction you have to make with it in order to get a benefit. I feel we're missing one thing
    though. When preparing a raid, you typically bring items for specific quest or want to run a specific quest.
    Therefore you likely want to have something like the current 'mark quest' thing, but for all the quest you want
    to tackle in the map. Still it should show all other quest markers. It should incorporate hazzle-free and well
    designed and fitting into the existing app. Choose the best design possible.") Work out the design on Monday
    and show it as mocks before building. Starting points:
    - **Today:** the highlighter keeps one quest lit: cyan markers and a guide line to its nearest place. Picking
      several would grow out of that same control: the pen on a Plan row toggles the quest into "this raid's
      picks", so it costs one click per quest while planning, and none in the raid (DESIGN.md: no clicks in a raid).
    - **On the map:** picks in the kept look (cyan, ringed); every other quest marker stays at normal strength, not
      faded (ties in with item 12); the guide line goes to the nearest place among all picks.
    - **In the rail:** the map's card shows the picks first, as their own small group or with a mark; BRING puts
      what the picks need first ("for your picks"); the raid card then starts from the picks, nearest first.
    - **Lifetime, without upkeep:** picks hold from Plan through loading into the raid; a quest the log reports
      completed leaves the picks by itself; picks for another map wait for that map; decide whether picks clear at
      raid end or stay until done. One quiet "Clear picks" outside raids.
    - **Zero picks = today's behaviour,** so nobody has to use it. Check it against the effort order (item list
      order), the synopsis lines and the folded cards; one symbol, one meaning (the pen keeps its meaning: "keep
      lit", now for several). DESIGN.md decision, help text, legend.
17. **Done (2026-10-03): item 5 below, one design system** (owner: "Yes, do the design system after this round"): DESIGN.md §4 "Design system", `Shturmap.Map.Palette`, `DesignTokenTests`; the website's CSS follows it on the unpublished FAQ branch.
18. Still open from before: items 2, 5 and 6 below, and the release checklist ("At the production release on GitHub").
   Done (2026-10-09): the repo-scan test, `RepositoryScanTests`, over every file git tracks: token shapes, a Sentry
   DSN, private keys, claude.ai links, paths in a user folder and email addresses; it reports file and line only.

## Before starting

- **Releases** (2026-10-03): 0.1.0 went to friends by hand as one exe. From **0.2.0**, the first public release,
  Velopack: `eng\release.ps1` builds the Setup and update packages into `artifacts\releases` (plus
  `artifacts\Shturmap-Setup.exe`), `eng\publish-release.ps1` publishes them as a GitHub pre-release, and the
  installed app keeps itself up to date (DESIGN.md §8, "Distribution"). `eng\release.ps1` refuses while Shturmap
  runs from `artifacts\Shturmap`; `eng\publish.ps1` alone builds only that folder and doesn't empty it first.
- **Boundary (owner's rule).** Read only `*application_*.log` and `*push-notifications_*.log` in an EFT log
  session folder, never `*.log`: that glob also matches `backend_000.log`, which is off limits. No memory, no
  input, no screen capture.
- **Verify without the owner's screen**: `Shturmap.exe --snapshot <folder> [seconds] [--show-quest <name>]`,
  `tools\fake-raid.ps1` (`-Scav`, `-LocalRaid`, `-PlanOnly`, `-ShowQuest`, `-SnapshotAfter`), and
  `shturmap-cli render` for map drawing. The fake raid writes application and push-notification lines; extend
  it with writers for the new lines below.
- Parsing lives in `src/Shturmap.Core/Logs/GameLogParser.cs` (application lines, and `ParseNotification` for
  push lines; `UserConfirmed` is already read as a `MatchSetupEvent`), events in `GameEvents.cs`, raid state in
  `src/Shturmap.Core/Raid/RaidTracker.cs`, the session in `src/Shturmap.Session/GameSession.cs` (`Apply`,
  `Announce` for big cues, `Say` for notices, `StudyRaid` for the study log), the UI in
  `src/Shturmap.App/MainWindow.xaml(.cs)`. Tests for parsing and the tracker go in
  `tests/Shturmap.Core.Tests/LogTests.cs`.

## 1. Group raids: show the leader's map before loading

**Done (2026-10-02).** In the study logs the pick came 20–70 s before loading (a handful of picks in two sessions,
each followed by that map loading). Day/night is left out: the Tarkov clock formula matches all but one of a dozen
screenshot raid clocks, but no screenshot exists from a raid whose time variant is known, so which time "CURR" and
"PAST" mean is unchecked; the study log now records the variant (`group.pick`) and the raid clock (`fix`) to settle
it. `fake-raid.ps1 -PlanOnly -GroupPick` shows it.

**Why.** In a group, the leader picks the map in the menus; Shturmap only learns it when loading starts, too late
to change gear. The push log says it earlier.

**Evidence** (push-notifications log, several times in one session).
- `<time> | ... | Got notification | GroupMatchRaidSettings`, then a JSON body whose `raidSettings` name the
  `location` (a location id, e.g. `Sandbox_high`), the `timeVariant` (`CURR` or `PAST`) and the `raidMode`, beside
  the group's other raid settings (spawn place, time and weather).

Related kinds seen: `GroupMatchInviteSend`, `GroupMatchRaidReady`, `GroupMatchRaidNotReady`, `GroupMatchStartGame`,
`UserMatchCreated`. The application log has `Matching with group id: <n>` at
matching (the number is empty when solo).

**Plan.**
- Core: `GroupRaidSettingsEvent(At, LocationId, TimeVariant)` from `ParseNotification`; optionally
  `GroupStatusEvent` for ready / not ready / start. Location ids are the same kind as in the match setup
  ("Sandbox_high", "bigmap", "TarkovStreets"): resolve with `data.CreateResolver().Resolve(null, locationId)`.
- Session: on a live (not replayed) group pick while in the menus, show that map (as `SelectMapAsync` does), say
  "Your group picked Ground Zero · bring: …" with the same bring list as the loading notice, and `Announce` a new
  `CueKind.GroupPick` (eyebrow "GROUP PICKED", the map, the plan's summary). A later pick replaces it; loading
  follows as today.
- `timeVariant` is "CURR" or "PAST": one of the two raid times 12 h apart. Turning it into "day" or "night" needs
  the in-game clock (Tarkov time runs at 7× real time). Work out the formula, check it against the raid clock the
  screenshots give (`RaidClockHours`), and only then show "night raid". Until then, leave it out.
- Study log: `group.pick` (map, time variant), group ready / not ready / start.
- Tests: parser test with the sample above (ids and times made up).

## 2. Say whether a raid runs on the player's machine

**Why.** The PMC ⇄ SCAV switch is only needed for raids hosted on the player's machine. PvE uses both kinds, at times
both on the same day.

**Evidence** (application log).
- Server-hosted: during loading `TRACE-NetworkGameCreate profileStatus: 'Profileid: <id>, Status: Busy,
  RaidMode: Online, Ip: …'`. Right after `GameStarted` comes ``[Transit] `<profileId>` Count:0,
  EventPlayer:False``, naming the profile that plays; in PvP the Scav profile's id is the PMC's plus one.
  Durations are non-zero, e.g. `GameStarted:90.6(10.33)`.
- Local: no `NetworkGameCreate`. `GameStarting:55.73(0) … GameStarted:55.73(0)` (zero in brackets), then
  `[Transit] Flag:Common, RaidId:<id>, Count:0, Locations:bigmap -> `. A cancelled match says
  `Local game matching cancelled.` instead of `Network game matching cancelled.`

**Plan.**
- Core: parse the bracketed duration of `GameStarting`/`GameStarted`, and the two cancel lines.
  `RaidState.Hosting` is `Unknown | Server | Local`: Server on a `MatchSetupEvent`; Local on a zero-length start
  with no setup, or "Local game matching cancelled.".
- Side: unchanged for server raids (the log tells). For local raids keep the switch, and say why in its tooltip
  and in `sideEvidence` (already logged as `setup:none,starting:yes`); add `hosting` to `raid.start`.
- Tests: tracker replays with both shapes (fixtures from the lines above).

## 3. Loading progress in the RAID LOADING cue

**Taken out again on 2026-10-04** (owner: "we really don't need the loading progress bar in the upper left, the game
already shows a loading progress bar"); the tracker still keeps the steps for the study log.

**Done (2026-10-02)**, as a line in the raid card naming the last step the log reported, over one segment per step
that lights only when the log reports it; nothing estimated (owner). The cue stays 5 s, with a slower entrance. Over
a few dozen loads (medians): location loaded at 25 s, spawned 42 s, pooled 47 s, raid start 71 s (31–139 s). Each
raid's step timings go to the study log in `raid.start`. `fake-raid.ps1 -HoldLoading` shows it.

**Why.** Loading takes 60–130 s and Shturmap shows nothing between the 5 s cue and the raid start.

**Evidence** (application log, a local Customs raid, about 72 s in all). The lines in order, each
`<time> | ... | application | <message>`, with the seconds since matching completed:
- `MatchingCompleted:0 real:0 diff:0` (0 s), then `scene preset path:maps/customs_preset.bundle …` (1.5 s) and
  `TRACE-NetworkGameMatching G / H / I` (1–3 s).
- `LocationLoaded:<s> real:<s> diff:<s>` (30 s), `GamePrepared…` (31 s), `GameCreated…` (32 s), then
  `[Transit] Flag:Common, RaidId:<id>, Count:0, Locations:bigmap -> ` (32 s).
- `PlayerSpawnEvent…` (38 s), `GamePooled…` (54 s), and `GameRunned`, `GameSpawn`, `GameSpawned`, `GameStarting`,
  `GameStarted` together (72 s).

**Plan.**
- Core: a `LoadingStepEvent(At, Step)` for LocationLoaded, GamePrepared, GameCreated, PlayerSpawnEvent,
  GamePooled; `RaidState.LoadingStep`.
- UI: after the cue, one quiet line in the raid card until the raid starts: "LOADING · SPAWNING", with a thin
  rule that fills by step. Optionally a rough "about 40 s" from typical step offsets (median of past raids in the
  study log, or fixed numbers from the sample). Keep it to one line (minimal-UI rule).
- Study log: per-raid step timings, matching time.

## 4. A hint of how the raid ended (study log only)

**Done (2026-10-02).** The logs held a couple of insurer notes (type 2, Prapor, with the raid's location) and as many
returns (type 8). The notes came 17–20 s *before* the raid's end line, not after it, so a note counts during the raid
and up to 5 minutes after it (`RaidOutcomeHints`); the Streets fixture gives one hint. Never shown in the UI.

**Why.** The study log can't tell a survived raid from a death, so plan accuracy can't be judged. Nothing in the
allowed logs says it directly, but the insurer writes when insured gear was lost.

**Evidence** (push-notifications log).
- A trader message, `message.type` 2, arriving right after a raid, with
  `"systemData": { "date": "<dd.MM.yyyy>", "time": "<HH:mm>", "location": "bigmap" }`, `"templateId": "<id> 0"` and
  `"hasRewards": false`. This is most likely the insurer's "items lost" note (seen twice).
- `message.type` 8 (insurance return) comes later with `"items"` and the same kind of `systemData`.
- Message type numbers to verify against what is seen: 2 trader, 8 insurance return, 10 quest started, 11 quest
  failed, 12 quest completed (10–12 are already parsed as quest events).

**Plan.**
- Core: `InsuranceNoticeEvent(At, Kind: Lost | Returned, LocationId, ItemCount)` from types 2 and 8 when
  `systemData.location` is present.
- Session: a "lost" note within about 5 minutes after a raid ended on the same location gives the study log a
  `raid.outcomeHint` event `{ lostInsured: true }`. Never show it as a fact: no note proves nothing, since gear may
  not have been insured.
- With item 1's quest ids at raid load (already logged as `complete` / `progress`), this lets a later study
  compare plans with what happened.

## 5. One design system for the app, the README and the website

**Done (2026-10-03):** DESIGN.md §4 "Design system" (colour tokens with roles, type roles for app and web, shape and
spacing, motion, icons, logo use, words, and where each surface takes them from); `Shturmap.Map.Palette` as the
colour table in code; `DesignTokenTests` keep App.xaml, XAML colours, the map, the brand files, the design doc and
the website's CSS equal to it.

**Why** (owner, 2026-10-02). With the logo settled (DESIGN.md §4, "Logo"), the colours, type and general look must
be the same in the app, the README and the coming website, and DESIGN.md has to say how.

**Plan.** Write one design system into DESIGN.md and align `App.xaml` and the README to it:
- colours: the palette, plus the light-background variants that so far exist only for the logo;
- type: Bahnschrift in the app; a DIN-like font with a licence that allows web use for the website, since
  Bahnschrift may be used on screen but not served as a web font (Microsoft's font FAQ); how the README fits in;
- shape, spacing and motion rules, the logo's use (clear space, minimum sizes), and the tone of the text.

## 6. A useful default mode without the game installed

**Why** (owner, 2026-10-03, "for later"). Someone without Escape from Tarkov on this PC still opens Shturmap: to
look at it before installing, or on a laptop beside the PC that runs the game. Today they get one 30 s notice
("Couldn't find Escape from Tarkov on this PC, so quests and raids won't follow the game…", `GameSession.
ReportGameFolders`) and then an app built around quests it can't know.

**What still works without the game:** tarkov.dev's data (all maps with artwork, extracts and transits with their
requirements, spawn zones, bosses with chances, map names, loose loot spots) and the help. What doesn't: active
quests, raid state, positions (all from the game's logs and screenshot names).

**Done (2026-10-03, owner: "Yes, build the fallback 1-5"; DESIGN.md "Screen anatomy", *No game*):**
1. One line where the Plan card would be, for as long as no game is found: "NO GAME FOUND ON THIS PC" ·
   **CHOOSE GAME FOLDER…** · "or browse the maps"; a variant when the game is found but hasn't run. No 30 s notice.
2. **Choose game folder…**: Windows' folder picker, checked with discovery's rules, saved, and followed at once
   without a restart (logs re-pointed and read for history).
3. Browse mode: the map as always, a PvE / PvP / Seasonal chooser in place of the mode label, nothing quest-like.
4. Discovery runs again every 30 s while the game or its logs are missing, and switches over by itself.
5. Developer switch `--no-game` and the developer view's "No game" trigger, plus its script step `choose`.

**Still open:**
- The laptop case: reading the game PC's logs over a network folder. A network folder chosen with **Choose game
  folder…** that holds the game already works; whether to say so (README, FAQ) and support it is the owner's call.
  Screenshots stay the local Documents folder's, so positions wouldn't arrive over the network.
- A quest lookup without the game (every quest on a map, labelled as all quests, not "yours") was not built; the
  browse mode shows nothing quest-like.
- Done since: settings have the "GAME FOLDER: <path or 'not found'>" row with CHOOSE… and FIND AUTOMATICALLY, so
  a found-but-wrong install can be changed too.

## At the production release on GitHub (owner, 2026-10-03)

Raise these with the owner when the app gets its first public GitHub Release (not for builds passed to friends):

1. **Website media.** Re-record the screenshots and the hero clip (`tools\make-media.ps1` in the site repository),
   only when the owner says so.
   **Done 2026-10-09** (owner: "update the Website with the latest media captures so it reflects the current state
   of the app"): every picture and the clip re-recorded from the app of that day, the hand checks gone through (the
   callouts, every quoted number, the crops, the sizes, the loop). What follows is the history of what they lacked.
   Owner, 2026-10-04: "Only update the assets for the website when I say so, but flag if it needs doing when we
   want to do a release and the assets are showing outdated information." So: never by itself, and at a release
   say what the pictures no longer show. Last recorded on 2026-10-04 (the quest types' new icons). Outdated since,
   to flag then: every map row in Plan's list carries its glyphs and need cells, the open map's too (the Plan
   picture); a possible place is a filled marker with a "?" where it was a hollow ring (the raid picture's Audit
   marker, the clip); the pulse of a pointed-at quest is two wider rings (the clip).
   The raid card says "12 min in · 28 min left" where the raid picture and the clip say "40 min raid · started".
   And since then: each pick has a colour of its own (the Plan picture and the clip show one cyan pick, which is
   still what a single pick looks like), and following leaves room ahead of the player (the clip).
   Decided the same evening, from mock-ups made in the real renderer: how picked quests are told apart on the map
   is a colour per pick (DESIGN.md, "A colour per pick").
   Decided from a second panel (outside the repository; owner: "Go for D", the cartographers' pastel table, eight
   colours; DESIGN.md has the measurements): which colours, once many quests
   are picked (owner: "when many quests are selected we need to be a bit better at distinguishing them"). Measured
   with CIEDE2000, also as seen with red-green colour blindness, and against the map's own colours: today's four
   are 39 apart at their closest but 6 for colour-blind eyes (cyan and pink), and repeat from the fifth pick; five
   colours can be kept 25 apart (16 colour-blind); eight only 15 (9); today's four with their pastels 12 (3); the
   standard tables (ColorBrewer Set3, Okabe-Ito) 13 to 14 here, and they sit on the quests' gold. Suggested had
   been five far-apart colours with a second ring from the sixth pick.
2. **Reporting is one route: in the app, to Sentry** (owner, 2026-10-03: "don't want to mix github issues and other
   reporting methods, it should be one coherent easy to use thing"; done, DESIGN.md §8, "Reports"). This replaces
   the earlier plan of GitHub issue forms plus a separate web form. Since 2026-10-09 GitHub Issues stay open beside it
   (owner), named in the README, the website and the release notes after the app's button (DESIGN.md §8, "Reports").
   Before the release:
   - the release is built with the DSN (`eng\sentry.dsn`, untracked; `eng\release.ps1` warns without it), and
     `Shturmap.exe --send-report "<text>" <folder>` from the release exe opens the Report dialog with the text;
     after a click on Send it says "Sent. Thank you."; the path checked end to end on 2026-10-09 with a developer
     build carrying the DSN (it sends by itself): "Sent. Thank you."; in Sentry (environment dev) the report showed no
     address, but its "User Geography" held a town (see B10). Still to do with the first release the workflow builds;
   - in the Sentry project, IP addresses aren't stored (Settings → Security & Privacy → "Prevent Storing of IP
     Addresses") and the data scrubbers are on; done (owner, 2026-10-09: "Sentry web-ui changes are in");
   - the README and the website point to "Help (?) → Report a problem or idea" (the website only on the owner's
     word), and the website's "sends nothing" wording becomes "Nothing is sent unless you send a report or allow
     crash reports";
   - `PRIVACY.md`: short and plain, done (owner, 2026-10-09: "Do we need the GDPR stuff at all? I would honestly just
     omit this", then "Short and plain"): the maintainer as who receives reports, deletion through the app's Report
     with the report id, at most 90 days at Sentry, no legal-template wording, no lawyer's review. Accepting Sentry's
     data processing agreement stays an optional click for the owner.
3. **Delivery: GitHub Releases with Velopack** (owner, 2026-10-03: releases in the code repository, an auto-update
   mode, and "we can move for the first public release, but we should state that the app is still in private
   testing"). Done: the Setup, updates (Automatic / Tell me only / Off, never during a raid), `eng\release.ps1` and
   `eng\publish-release.ps1`, the README's private-testing note, `docs\release-notes\0.2.0.md`, version 0.2.0; the
   whole path (install, update to a newer version from a local feed, uninstall with the data folder untouched) was
   tested on this PC on 2026-10-03. The owner's steps:
   - make `github.com/shturmap/shturmap` public (the app's update check asks GitHub anonymously, which only works
     for a public repository);
   - commit and push, then `.\eng\release.ps1` and `.\eng\publish-release.ps1` (or `-Draft` to look at it on GitHub
     before publishing). It uses `GITHUB_TOKEN`, `gh`'s login or Git's stored GitHub login;
   - the first real update check against GitHub happens with the second release (0.2.1): install 0.2.0 from the
     release, publish 0.2.1, and see that 0.2.0 offers it. Until then the GitHub path is untested; the local feed
     path is;
   - friends with the 0.1.0 exe run the Setup once (the release notes say so).
4. **Repository settings and who publishes** (on hold until the release, owner, 2026-10-03). Before the repository
   goes public: turn off GitHub Issues and Discussions (reports go through the app), decide whether releases are
   published by the owner's account or by GitHub Actions, and whether the maintainer's identity is shown on the
   repository's pages. Decided 2026-10-09: by GitHub Actions, the Release workflow ("2026-10-09: trust", above);
   Issues stay on (owner, 2026-10-09: "Keep github issues on"), beside the app's own Report.
5. **Wording and risk, from the web research of 2026-10-03** (owner: "put points 1–5 on the release checklist";
   DESIGN.md §2 has the findings and rules). Done in the README and DESIGN.md on 2026-10-03; still to do on the
   website (only on the owner's word) and in the release notes (`docs\release-notes\0.2.0.md`):
   1. the precise risk line: not made or endorsed by Battlestate; its License Agreement (4.3.4) lets it decide which
      outside software is allowed; no guarantee against sanctions; use at your own risk;
   2. where the coordinates come from: Battlestate's support article 437 (reporting a bug) describes them in the
      screenshot name; a source, not an endorsement;
   3. one honest sentence on PvP: some players see a map with your own position as an unfair advantage in PvP; most
      useful for learning maps and in PvE; and the two rules (never shares a position, never takes a screenshot for
      the player);
   4. done: the two lasting rules and the wording rule in DESIGN.md §2;
   5. a change by Battlestate: if the screenshot names lose their coordinates, or its planned "simplified GPS"
      (roadmap, December 2026, reported only second-hand) changes things, Shturmap must keep working as a quest
      planner without a position. Before the release, check what a screenshot name without coordinates does (a
      fake-raid run with such a name), and say it in the README and on the website.

## To do later (owner, 2026-10-17)

- **Dailies and weeklies from a screenshot**: built 2026-10-19, see "2026-10-19: operational tasks, dailies and weeklies, in the rail" at the top.

- **Report the defects in tarkov.dev's extract data** (the exits verification of 2026-10-17, kept beside the
  repositories; owner, 2026-10-17: "Note this down for later"; owner, 2026-10-19, on the session's report page: later
  still, the draft stands). The app's side is built ("2026-10-17: exit facts and the
  data rules", above; DESIGN.md, "Extract requirements", *The data's defects*): general rules in `ExtractRepairs`, each
  a no-op once the data is right, so nothing is undone when it is corrected. A copy of another map's extract outside its
  map is dropped; a variant's extract without a side takes its base map's; a variant without extracts takes its base
  map's; the Customs rail is a PMC's by hand (`ExitFacts`); the missing exits stay out. `bring` prints what each did,
  and `ExtractRulesTests` warns where one did less (strike that defect from the report). What remains is the report to
  tarkov.dev (its data manager, or an issue on the-hideout's repositories), with the six defects:
  1. Ground Zero carries three extracts of other maps at their places, outside its own bounds: UN Roadblock and Scav
     Bunker (Woods'), Pinewood Basement (Co-Op) (Streets').
  2. Night Factory's nine and Ground Zero 21+'s six extracts have no `faction` (Factory's and Ground Zero's have).
  3. Customs' `ex_customs_pmc_rail_milbase` (Railroad to Reserve) is tagged `scav`; it is the PMC's.
  4. The Lab (Dark) (`laboratory_dark`), where the Factory and Streets transits lead, has no extracts.
  5. Three extracts are missing: Interchange's Smugglers' Tunnel, Lighthouse's Road to Military Base V-Ex, Terminal's
     Zubr boat.
  6. The extracts' `switches` tell none apart: Customs lists the ZB-013 lever on all 28, The Lab the Med Elevator's
     three buttons on all 7, and Interchange's Saferoom and Reserve's D-2 and Bunker Hermetic Door list none, though
     the map places their switches (the ten gated extracts are kept by hand in `SwitchExits`).
  7. Four extract ids are each shared by two entries of one map: Woods' Outskirts (PMC and Scav), Woods' UN
     Roadblock (PMC and Scav), Factory's Gate 3 (Scav and PMC), Night Factory's Gate 3 (two entries without a
     faction). Found 2026-10-19 by the repel fix ("2026-10-19: two symbols on one spot drift apart", above), which now
     keys a symbol by id and kind; other places still key an extract by id alone.

  Drafted 2026-10-19, outside the repository (`%USERPROFILE%\source\repos\Shturmap-tarkov-dev-report-2026-10-19.md`):
  where to file (the-hideout/tarkov-data-manager, no template, no existing issue covers any of the seven), the
  duplicate-id check, and six ready issue texts. The owner files them.

- **Two symbols on one spot drift apart with every pan or zoom** (owner, 2026-10-17): built, see "2026-10-19: two symbols
  on one spot drift apart" at the top.

- **A required weapon's icon is barely a weapon** (owner, 2026-10-17): built, see "2026-10-19: a required weapon pictured
  as the assembled gun" at the top.

- **Text hard to read on some setups: a text size, or a UI scale** (beta feedback, relayed by the owner 2026-10-18):
  decided and built, see "2026-10-19: text size: Windows' own, honoured everywhere" at the top.

## Not asked for (owner to decide)

- Lines like `Reason:Lift, Position:(x, y, z), SpeedLimit:…, CurrentState:Run…` carry a world position (after an
  elevator ride, a speed check, a network hiccup). There were about a dozen in some twenty sessions, which is too rare
  to rely on.
  They look like the game's movement-check diagnostics: ask the owner before using them.
- History from the logs (maps played, raid lengths, quest starts and completions) could ground Plan in the player's
  own record.
- Low value: quest reward messages (Shturmap shows no rewards), server region and raid short id, graphics and
  sound settings.
- Not in the allowed logs: survived or killed, kills, loot, XP, money, inventory, a continuous position.
