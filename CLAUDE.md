# Shturmap

Read `docs/DESIGN.md` before changing anything. It is binding: product scope (one job, no feature bloat), the
game terms-of-service boundary, copyright rules, UX principles (second monitor, no clicks in a raid, minimal),
the visual language and taxonomy, and engineering rules. If a change conflicts with it, stop and ask the owner;
if a decision changes, update `docs/DESIGN.md` in the same change.

## Push back on the design (owner, 2026-10-07)

Requests and ideas, the owner's included, are judged against the design, not agreed with. Push back before building:

- **Against the design**: when one conflicts with `docs/DESIGN.md` (the one job, the not-goals, no clicks in a raid,
  the terms-of-service and copyright rules, the visual language), name the section and the conflict, and stop for
  the owner's decision.
- **Better features**: when a planned or existing feature could be a simpler, more robust or better answer to the
  two questions (DESIGN.md §1), say what and why, with the trade-offs, and ask before building it differently.

Once the owner has decided, follow it and update `docs/DESIGN.md` if the decision changes it.

## Secrets and private data: the repository is public (owner, 2026-10-03)

Everything committed is public for good: history can't be taken back once pushed. So never put any of these into a
tracked file, a test or fixture, a commit message, release notes, an issue or a build:

- **Secrets:** access tokens of any kind (GitHub, Sentry `sntryu_…`, API keys), passwords, private keys,
  certificates, `.env` files, and the Sentry DSN (only in the untracked `eng\sentry.dsn` and the `SENTRY_DSN` Actions
  secret). A token the owner pastes into the chat is used for that task only, never written to a file, and the owner
  is reminded to revoke it.
- **The owner's private data:** the only identity in the repository is the commit author, `shturmap
  <339214139+shturmap@users.noreply.github.com>`, and every commit is signed with that account's SSH key; never
  commit or push with another identity or GitHub login (owner, 2026-10-07). No email addresses, postal address,
  phone number, Windows user name, machine name, IP addresses, or paths under the user folder (write
  `%USERPROFILE%`); no screenshots of the owner's screen.
- **Players' data:** game profile and account ids, nicknames, IPs, session tokens. **No game log goes into the
  repository, scrubbed or not** (owner, 2026-10-04): the sessions a few tests replay stay in `tests\fixtures\logs`,
  which git ignores (`tools\make-log-fixtures.ps1` makes them; those tests skip without them). A test that needs a
  log line writes it by hand with made-up ids. Never copy a backend log, a study log, a diagnostics text or a Sentry
  report into the repository either.
- **Links to private workspaces:** no claude.ai links (sessions, artifacts), so commit messages end with the
  `Co-Authored-By` line only, without a `Claude-Session` link.
- **Copyrighted data:** a quest's full text or a dump of quest texts (the one-line objective samples in tests are the
  limit, DESIGN.md §3), tarkov.dev payloads or Battlestate art (see below).

Before each commit, read the staged diff (`git diff --cached`) for the list above. `RepositoryScanTests` checks every
tracked file for token shapes, a Sentry DSN, private keys, claude.ai links, user-folder paths, email addresses, and
screenshot names and log sessions dated other than 2026-01-01 (every example uses that day: a real time of play, with
a position or a session's quests, can be matched to the player), not for everything on the list. After adding a
package, scan a local release build for the Windows user name (DESIGN.md §8, "Reports"). When unsure whether
something is private, stop and ask the owner instead of committing it.

Essentials:

- Never touch the game process, send input, hook, register global hotkeys or capture the screen. `SafetyTests`
  enforces this.
- A screenshot's picture is opened for one thing only: the game's extract list in its top right corner, read on the PC
  with Windows' own text recognition (`ExitListReader`; owner, 2026-10-05; DESIGN.md §2). Reading anything else from
  a picture is the owner's decision, never a side effect of a change. Real screenshots never go into the repository:
  the reader's tests draw their own list, and read the ones in the git-ignored `tests\fixtures\ocr` where they exist.
- Never bundle tarkov.dev data, map artwork or Battlestate art; download at runtime, credit, personal use only.
- Never capture the user's monitors to check UI; use `Shturmap.exe --snapshot <folder>` or `shturmap-cli render`.
- Build and test: `.\eng\dotnet.ps1 build Shturmap.slnx`, `.\eng\dotnet.ps1 test --solution Shturmap.slnx`
  (the wrapper finds the per-user .NET 10 SDK). Every push runs both on GitHub (`.github/workflows/ci.yml`), and
  `eng\audit-packages.ps1` for packages with known vulnerabilities (no Dependabot: its commits would be another
  identity); logs are public, so a test never prints anything private. Publish the folder build (`artifacts\Shturmap`,
  what `tools\fake-raid.ps1` runs): `.\eng\publish.ps1`. A release (Velopack; DESIGN.md §8, "Distribution"): raise
  `<Version>` in `Directory.Build.props` by yourself, every release (owner, 2026-10-04): the patch number (0.3.0 →
  0.3.1) when only small things changed since the last release (fixes, wording, small UI tweaks), the minor number
  (0.3.x → 0.4.0) when something big did (a new feature, a visible redesign); say which and why. Write
  `docs\release-notes\<version>.md` (what Shturmap is, plus what's new since the last release) and the version's
  section in `docs\whats-new.md` (the What's New card in the app: up to five short lines), commit and push. A release
  that raises the minor or major number gets a name (owner, 2026-10-09; the first, 0.4.0, is "Praetorian"): ask the
  owner for it, and write it after the version in the section's heading (`## 0.4.0 · Praetorian`) and in the notes'
  first line; a patch release keeps its line's name (DESIGN.md §4, *What's New*, "Release names"). The
  owner's go is running the Release workflow (`.github/workflows/release.yml`; owner, 2026-10-09): `gh workflow run
  release.yml` (`-f draft=true` leaves a draft to review on GitHub first), or the Actions tab. On GitHub's runner it
  checks the app's packages, builds with `.\eng\release.ps1` (the DSN from the `SENTRY_DSN` Actions secret), attests
  build provenance for the Setup and packages, then publishes with `.\eng\publish-release.ps1` (a GitHub pre-release
  "Shturmap <version> "<name>" (private testing)", with `Shturmap-Setup.exe` and its `.sha256`). Don't publish from
  the PC: such a release has no provenance, which the README promises from 0.4.0. `.\eng\release.ps1` still builds
  `artifacts\release` (`Shturmap-Setup.exe`, `packages\`, `app\`; nothing uploaded) on the PC, for tries and update
  tests. Test updates locally with an installed build and `--update-feed <folder>`, never by publishing a test
  release. A download is checked with `gh attestation verify Shturmap-Setup.exe -R shturmap/shturmap` (README,
  "Check it yourself").
- **After committing app changes, run `.\eng\dev.ps1`** (owner, 2026-10-03): it builds the dev build ("Shturmap DEV",
  cyan icon, `%LOCALAPPDATA%\Shturmap-dev` data, developer tools) into its local feed `artifacts\dev\feed`, and the
  owner's installed dev app takes it at its next start, so the owner always tries the latest. The first run installs
  it. It never touches the release's install or the player's data (DESIGN.md §8, "Developer aids").
- Data folders: only the installed release uses `%LOCALAPPDATA%\Shturmap` (the owner's own data); every other build,
  the CLI and tests use `%LOCALAPPDATA%\Shturmap-dev`; `--data <folder>` picks another (DESIGN.md §8, "Data folders").
  To read or change the release's data with the CLI, give `--data "%LOCALAPPDATA%\Shturmap"`.
- Reports go to Sentry through the DSN in the untracked `eng\sentry.dsn` (gitignored), which `eng\release.ps1` and
  `eng\publish.ps1` build in; the Release workflow takes it from the `SENTRY_DSN` Actions secret. Never commit the
  DSN or write its value into docs, tests or commit messages; tests use the local `FakeSentry`. Check a release with
  `Shturmap.exe --send-report "<text>" <folder>` (it must say "Sent. Thank you."); never send a real crash report to
  test (DESIGN.md §8, "Reports").
- The CLI (`tools/Shturmap.Cli`) runs every part headless; `simulate` plays a scripted raid against a fake game.
- After a tarkov.dev or game update, and before a release, go through `docs\UPDATES.md` (owner, 2026-10-05): refresh
  the data, run the audits in both modes (`synopses`, `effort`, `bring`, `handovers`, `spawns`) and the tests against
  the fresh cache, and after a game patch the played-raid steps. It lists what each rule assumes of the data and the
  game, and what has no check yet. Fix drift with a test on made-up ids, update DESIGN.md, and add a line to its log.
  Never commit a quest's full text or a dump; the one-line objective samples in tests are the limit (DESIGN.md §3);
  tests read the local cache.
- The website's screenshots and hero clip are re-recorded only when the owner says so (owner, 2026-10-02); remind
  them when a release goes up on GitHub. Then use `tools\make-media.ps1` in `..\shturmap.github.io` (see its
  CLAUDE.md) and go through its hand-check list.
- **Keep the tour current** (owner, 2026-10-09; DESIGN.md §4, *The tour*): a change to something a chapter shows
  changes that chapter in `docs/tour.md` in the same commit, and a release that changed a chapter adds a `tour:N` line
  to its What's New section. Before a release, look at each chapter: `tools\fake-raid.ps1 -Exe
  artifacts\Shturmap\Shturmap.exe -Out <folder> -PlanOnly -Tour <n>` for n = 1 to 7. `TourTests` catch a part that's
  gone from the window; whether the words are still true is yours to check.
