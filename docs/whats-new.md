# What's New in the app

The lines of the card at the top of Plan's rail after an update (docs/DESIGN.md §4, "Screen anatomy", *What's New*).
Built into the app. One section per version, newest first, up to five lines each:
`- preview · Name · What it is.` The preview says what pointing at the line shows on the map: `replay` (the raid replay
on an example raid), `extracts` (an example extract list), `clock` (the raid card's clock with example times), `joined`
(Gratitude's two objectives on one spot) or `leaders` (symbols set apart at Streets' Scav Checkpoint). A line about the
tour previews nothing and opens it when clicked: `tour` at its start, `tour:5` at its fifth chapter (a chapter the
release changed; docs/tour.md). Words as the design system's "Words": short, fragments are fine. Add the next version's
section with its release (CLAUDE.md). A release that raises the minor or major number has a name, the owner's, after
its version (`## 0.4.0 · Praetorian`); a patch release keeps its line's name without repeating it. The name shows in
the card's heading, help's link to it, the version line in settings and the release's title on GitHub.

## 0.4.0 · Praetorian

- tour · The tour · Seven short chapters on how it works. Click to take it; help brings it back.
- replay · Raid replay · After a raid with a few screenshots: where you took them, in order, faint to bright by time.
- extracts · Your extracts this raid · Screenshot the game's extract list: yours glow, the others go hollow.
- clock · Raid time, large · Minutes left as the raid card's largest figure; red for the last ten.
- joined · One spot, one marker · Several objectives of a quest on one spot: one marker that says how many.
