# Shturmap

Read `docs/DESIGN.md` before changing anything. It is binding: product scope (one job, no feature bloat), the
game terms-of-service boundary, copyright rules, UX principles (second monitor, no clicks in a raid, minimal),
the visual language and taxonomy, and engineering rules. If a change conflicts with it, stop and ask the owner;
if a decision changes, update `docs/DESIGN.md` in the same change.

## Secrets and private data: the repository is public (owner, 2026-10-03)

Everything committed is public for good: history can't be taken back once pushed. So never put any of these into a
tracked file, a test or fixture, a commit message, release notes, an issue or a build:

- **Secrets:** access tokens of any kind (GitHub, Sentry `sntryu_…`, API keys), passwords, private keys,
  certificates, `.env` files, and the Sentry DSN (only in the untracked `eng\sentry.dsn`). A token the owner pastes
  into the chat is used for that task only, never written to a file, and the owner is reminded to revoke it.
- **The owner's private data:** the only identity in the repository is the commit author, `shturmap
  <339214139+shturmap@users.noreply.github.com>`. No email addresses, postal address, phone number, Windows user
  name, machine name, IP addresses, or paths under the user folder (write `%USERPROFILE%`); no screenshots of the
  owner's screen.
- **Players' data:** game profile and account ids, nicknames, IPs, session tokens. Log fixtures come only through
  `tools\make-log-fixtures.ps1`, which replaces ids by placeholders; never copy a backend log, a study log, a
  diagnostics text or a Sentry report into the repository.
- **Links to private workspaces:** no claude.ai links (sessions, artifacts), so commit messages end with the
  `Co-Authored-By` line only, without a `Claude-Session` link.
- **Copyrighted data:** quest texts, tarkov.dev payloads or Battlestate art (see below).

Before each commit, read the staged diff (`git diff --cached`) for the list above. Before a release, scan the built
files for the Windows user name (DESIGN.md §8, "Reports"). When unsure whether something is private, stop and ask
the owner instead of committing it.

Essentials:

- Never touch the game process, send input, hook, register global hotkeys or capture the screen. `SafetyTests`
  enforces this.
- Never bundle tarkov.dev data, map artwork or Battlestate art; download at runtime, credit, personal use only.
- Never capture the user's monitors to check UI; use `Shturmap.exe --snapshot <folder>` or `shturmap-cli render`.
- Build and test: `.\eng\dotnet.ps1 build Shturmap.slnx`, `.\eng\dotnet.ps1 test --solution Shturmap.slnx`
  (the wrapper finds the per-user .NET 10 SDK). Publish the folder build (`artifacts\Shturmap`, what
  `tools\fake-raid.ps1` runs): `.\eng\publish.ps1`. A release (Velopack; DESIGN.md §8, "Distribution"): raise
  `<Version>` in `Directory.Build.props` by yourself, every release (owner, 2026-10-04): the patch number (0.3.0 →
  0.3.1) when only small things changed since the last release (fixes, wording, small UI tweaks), the minor number
  (0.3.x → 0.4.0) when something big did (a new feature, a visible redesign); say which and why. Write
  `docs\release-notes\<version>.md` (what Shturmap is, plus what's new since the last release), commit and push, then
  `.\eng\release.ps1` (builds `artifacts\release`: `Shturmap-Setup.exe`, `packages\`, `app\`; nothing uploaded) and,
  with the owner's go, `.\eng\publish-release.ps1` (a GitHub pre-release "Shturmap <version> (private testing)";
  `-Draft` to review it on GitHub first). Before publishing, scan `artifacts\release\packages` (unpack the `.nupkg`)
  for the Windows user name. Test updates locally with an installed build and `--update-feed <folder>`, never by
  publishing a test release.
- **After committing app changes, run `.\eng\dev.ps1`** (owner, 2026-10-03): it builds the dev build ("Shturmap DEV",
  cyan icon, `%LOCALAPPDATA%\Shturmap-dev` data, developer tools) into its local feed `artifacts\dev\feed`, and the
  owner's installed dev app takes it at its next start, so the owner always tries the latest. The first run installs
  it. It never touches the release's install or the player's data (DESIGN.md §8, "Developer aids").
- Data folders: only the installed release uses `%LOCALAPPDATA%\Shturmap` (the owner's own data); every other build,
  the CLI and tests use `%LOCALAPPDATA%\Shturmap-dev`; `--data <folder>` picks another (DESIGN.md §8, "Data folders").
  To read or change the release's data with the CLI, give `--data "%LOCALAPPDATA%\Shturmap"`.
- Reports go to Sentry through the DSN in the untracked `eng\sentry.dsn` (gitignored), which `eng\release.ps1` and
  `eng\publish.ps1` build in. Never commit the DSN or write its value into docs, tests or commit messages; tests use
  the local `FakeSentry`. Check a release with `Shturmap.exe --send-report "<text>" <folder>` (it must say "Sent.
  Thank you."); never send a real crash report to test (DESIGN.md §8, "Reports").
- The CLI (`tools/Shturmap.Cli`) runs every part headless; `simulate` plays a scripted raid against a fake game.
- After a tarkov.dev or game update, run `shturmap-cli synopses pve` and `shturmap-cli synopses regular` and look at
  the FALLBACK, LONG and BREAK rows: the quest synopses in Plan are made from tarkov.dev's texts by fixed rules
  (DESIGN.md §5, "Quest synopsis"). Never commit quest texts or a dump of them; tests read the local cache. Also run
  `shturmap-cli effort pve` and `… regular`: Plan's order by effort group (DESIGN.md §7, "Plan order"); name any
  UNKNOWN TARGET in `QuestEffort` and check any unknown objective type.
- The website's screenshots and hero clip are re-recorded only when the owner says so (owner, 2026-10-02); remind
  them when a release goes up on GitHub. Then use `tools\make-media.ps1` in `..\shturmap.github.io` (see its
  CLAUDE.md) and go through its hand-check list.
