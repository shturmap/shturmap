# The tour

The first-start tour's chapters (docs/DESIGN.md §4, "Screen anatomy", *The tour*). Built into the app. A chapter is
`## stage · anchors`, then its title on a line of its own, then at most two lines of words, then the stage's own words
as `- key · text` lines where it has any (`{key}`, `{n}` and `{all}` are filled in: the screenshot key, the extracts
on the example list, all of the map's). The stage is what the chapter draws (`safe`, `follows`, `next`, `pick`,
`key`, `raid`, `know`); the anchors are the `x:Name`s in `MainWindow.xaml` of the parts it frames, comma-separated,
with `|` between a part and the one framed when the first isn't shown. Words as the design system's "Words": short,
fragments are fine. A change to something a chapter shows changes its chapter here, in the same commit, and a
release that changed a chapter says so in docs/whats-new.md with a `tour:N` line (CLAUDE.md). `TourTests` check the
stages, the anchors and the lengths.

## safe
SAFE TO RUN
Reads the game's logs and your screenshots, on this PC. Never touches the game.
An unofficial fan project, not made or endorsed by Battlestate Games. Use at your own risk.
- reads · The game's logs
- reads · Your screenshots' names
- reads · The extract list in a screenshot
- never · Touches the game
- never · Sends keys or clicks
- never · Captures your screen

## follows · RaidWord, Rail
IT FOLLOWS THE GAME
Put it on your second monitor. In the menus it plans; in a raid it follows the raid.
It switches by itself: nothing to click while you play.
- NOT IN A RAID · Plan: where to go, what to bring
- IN RAID · Raid: the same card, live, with the time left

## next · PlanRows | MapPicker, Map
NEXT RAID
Maps ranked by what your quests can do there.
Rest on a row: its map previews. Click it to plan that raid.

## pick · Map
PICK AND POINT
The pen beside a quest picks it for the raid: its own colour on the map.
Point at anything: every place it appears lights up.

## key · Map
YOUR SCREENSHOT KEY
In a raid, press it now and then: the file's name says where you are.
Open the extract list first (O twice): Shturmap reads which extracts are yours.
- list · IN THE GAME: O TWICE, THEN {key}
- read · YOUR LIST THIS RAID: {n} OF {all} EXTRACTS

## raid · Map
IN THE RAID
Loading: what to bring, while you can still cancel.
In the raid: the time left, the next objective, the nearest extract.

## know · FeedbackButton, HelpButton, SettingsButton
GOOD TO KNOW
Story chapters (Falling Skies, Batya, …) aren't shown yet: tarkov.dev doesn't publish them.
F1 brings help, the legend and this tour back.
- FeedbackButton · A PROBLEM OR AN IDEA
- HelpButton · HELP · F1
- SettingsButton · SETTINGS
