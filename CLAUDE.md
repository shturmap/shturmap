# Shturmap

Read `docs/DESIGN.md` before changing anything. It is binding: product scope (one job, no feature bloat), the
game terms-of-service boundary, copyright rules, UX principles (second monitor, no clicks in a raid, minimal),
the visual language and taxonomy, and engineering rules. If a change conflicts with it, stop and ask the owner;
if a decision changes, update `docs/DESIGN.md` in the same change.

Essentials:

- Never touch the game process, send input, hook, register global hotkeys or capture the screen. `SafetyTests`
  enforces this.
- Never bundle tarkov.dev data, map artwork or Battlestate art; download at runtime, credit, personal use only.
- Never capture the user's monitors to check UI; use `Shturmap.exe --snapshot <folder>` or `shturmap-cli render`.
- Build and test: `.\eng\dotnet.ps1 build Shturmap.slnx`, `.\eng\dotnet.ps1 test --solution Shturmap.slnx`
  (the wrapper finds the per-user .NET 10 SDK). Publish the folder build (`artifacts\Shturmap`, what
  `tools\fake-raid.ps1` runs): `.\eng\publish.ps1`. The release to send, one exe plus a zip:
  `.\eng\release.ps1` (DESIGN.md §8, "Distribution").
- The CLI (`tools/Shturmap.Cli`) runs every part headless; `simulate` plays a scripted raid against a fake game.
- After a tarkov.dev or game update, run `shturmap-cli synopses pve` and `shturmap-cli synopses regular` and look at
  the FALLBACK, LONG and BREAK rows: the quest synopses in Plan are made from tarkov.dev's texts by fixed rules
  (DESIGN.md §5, "Quest synopsis"). Never commit quest texts or a dump of them; tests read the local cache.
- The website's screenshots and hero clip are re-recorded only when the owner says so (owner, 2026-10-02); remind
  them when a release goes up on GitHub. Then use `tools\make-media.ps1` in `..\shturmap.github.io` (see its
  CLAUDE.md) and go through its hand-check list.
