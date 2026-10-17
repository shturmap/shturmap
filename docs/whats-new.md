# What's New in the app

The lines of the card at the top of Plan's rail after an update (docs/DESIGN.md §4, "Screen anatomy", *What's New*).
Built into the app. One section per version, newest first, up to five lines each:
`- preview · Name · What it is.` The preview says what pointing at the line shows on the map: `replay` (the raid replay
on an example raid), `extracts` (an example extract list), `clock` (the raid card's clock with example times), `joined`
(Gratitude's two objectives on one spot), `leaders` (symbols set apart at Streets' Scav Checkpoint), `stash` (the SEEN
IN YOUR STASH cue with example items), `stash-card` (an item card with an example mark from a screenshot and NOT ANY
MORE), `stash-given` (one with an example mark from the game's log) or `notes` (a held quest card with two example
notes). A line about the tour previews nothing and
opens it when clicked: `tour` at its start, `tour:5` at its fifth chapter (a chapter the release changed; docs/tour.md). Words as the design system's "Words": short, fragments are fine. Add the next version's
section with its release (CLAUDE.md). A release that raises the minor or major number has a name, the owner's, after
its version (`## 0.4.0 · Praetorian`); a patch release keeps its line's name without repeating it. The name shows in
the card's heading, help's link to it, the version line in settings and the release's title on GitHub.

A translation is `docs/whats-new.<code>.md` beside this file (`whats-new.de.md`), built in the same way and shown in
that language (docs/DESIGN.md §8, "Texts"): a section per version this file has, from the release the language came
with on, each with this section's lines in the same order and their previews as they are, the names and words
translated. A version the translation lacks, or whose lines aren't this section's, shows from this file.

## 0.6.2 · Okami

- leaders · Bosses, stepped back · Boss zones rest hollow with one name each; they light when pointed at or targeted by a quest.
- extracts · Exits, with their facts · A badge on an exit that needs a switch; keys, no backpack, single use and timed windows on its row.
- tour:3 · Plan by trader · A row of trader portraits under the map list: point to light their quests, click to hold.
- leaders · Lit things breathe · What lights up while you point at a trader, a boss or an item breathes, so it stays easy to find.

## 0.6.1 · Okami

- extracts · Your extracts, slower · Each icon flies 1.5 s, one after the other, so your eye can follow it to its extract.
- leaders · Above or below, bolder · The other-floor arrow is a bold caret in a larger disc, readable on a small second screen.

## 0.6.0 · Okami

- extracts · Your extracts, called out · Screenshot the extract list: yours line up mid-map, nearest first, then fly to their places.
- notes · Notes on a quest · Click a quest and write on its card what to remember next raid. Each note keeps its time.
- tour:5 · Your list, in the tour · Chapter 5 shows the call-out. A screenshot taken as the raid fades in is read now too.

## 0.5.0 · Hyena

- stash · What you have · Screenshot your key tool or stash in the game: the items your quests need say "Seen in your stash".
- stash-given · From the game's log · What a quest hands you at its start, its rewards and insurance returns get a mark too.
- stash-card · Not any more · One click on an item's card takes a mark back; settings forget them all. Never changes the plan.
- tour:5 · Your extracts, clearer · Not on your list: struck out. "??:??:??": a ? in the triangle. A new list rings yours out.
- tour:1 · What it reads · Menu screenshots too, for your stash. "Delete screenshots after reading" clears them as well.

## 0.4.0 · Praetorian

- tour · The tour · Seven short chapters on how it works. Click to take it; help brings it back.
- replay · Raid replay · After a raid with a few screenshots: where you took them, in order, faint to bright by time.
- extracts · Your extracts this raid · Screenshot the game's extract list: yours glow, the others are struck out.
- clock · Raid time, large · Minutes left as the raid card's largest figure; red for the last ten.
- joined · One spot, one marker · Several objectives of a quest on one spot: one marker that says how many.
