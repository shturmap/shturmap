Shturmap: privacy notice for reports and crash reports
======================================================

DRAFT, 2026-10-03. [OWNER: fill in everything in square brackets before the first public release, and have the
whole notice checked.]

Shturmap runs on your PC. It reads the game's log files, your screenshots' file names and public data from
tarkov.dev, and it keeps its own log on your PC. Nothing about you leaves your PC, with two exceptions that you
control; and it asks GitHub for new versions of itself (3).

1. A report you send
--------------------
When you press Send in "Report a problem or idea" (help, ?), Shturmap sends:

- whether it is a problem or an idea, the text you wrote, and the contact you gave, if any;
- Shturmap's version, whether it is installed or a folder build, and your Windows version;
- if "Include diagnostics" is ticked: the diagnostics that "Show what's sent" displays first. They say whether the
  game, its logs and its screenshots folder were found, the game mode and language, the state of tarkov.dev's data,
  how many quests are active, and the last 200 lines of Shturmap's own log.

2. A crash report, if you allow it
----------------------------------
After Shturmap crashed or ran into an error, it asks at the next start whether to send a report ("Crash reports" in
help: Ask after a crash, the default; Always send; Never). A crash report holds the error's type and message, where
in Shturmap's code it happened, Shturmap's and Windows' versions and the last 50 lines of Shturmap's own log.

3. Checking for a new version
-----------------------------
Installed with its Setup, Shturmap asks GitHub (operated by GitHub, Inc., USA) for a newer version of itself at
start and every 6 hours, and downloads it from there. GitHub sees your internet address, as with any download;
Shturmap sends nothing else. "Updates" in help: Automatic, the default (a new version downloads in the background and
applies at the next start); Tell me only (it asks, and downloads only when you click); Off (no request at all).

What is never sent
------------------
Your Windows user folder is masked in everything that is sent (written as %USERPROFILE%), and every game account,
profile or quest id is cut out. Nothing is sent from memory, no screenshots, no game files, no study log, no list of
your quests, no machine name and no IP address is stored with a report. Sentry (below) also sees your internet
address when a report arrives and keeps your country and town from it; the address itself isn't kept.

Who receives it
---------------
[OWNER: name] ("the controller"), who develops Shturmap. Contact: [OWNER: contact address].

Reports are kept with Sentry (Functional Software, Inc., USA) in its EU data region (Frankfurt, Germany), which
processes them on the developer's behalf. The Sentry project is set not to store IP addresses.
[OWNER: confirm Sentry's data processing agreement applies to your account, and name its safeguards for transfers
outside the EU.]

Why
---
To find and fix problems in Shturmap and to decide what to build next. The legal basis is your consent (Art. 6(1)(a)
GDPR): you give it by pressing Send, or by choosing "Always send" for crash reports. You can withdraw it at any time
by choosing "Never"; what was sent before stays lawful.

How long
--------
[OWNER: the retention of your Sentry plan, e.g. 30 or 90 days], then reports are deleted automatically. On your
PC, crash records are kept for 30 days (%LOCALAPPDATA%\Shturmap\crashes), and a report that couldn't be sent waits
in %LOCALAPPDATA%\Shturmap\outbox until it has gone.

Your rights
-----------
You can ask for a copy of a report you sent, or for it to be corrected or deleted: write to the contact above and
quote the report id Shturmap showed after sending (8 letters and digits). You can also complain to a data
protection authority.

Everything Shturmap keeps on your PC (settings, quest history, logs, the study log, reports waiting to be sent,
crash records and the download cache) is in %LOCALAPPDATA%\Shturmap. To delete all of it, use Help (?) →
"Uninstall Shturmap…" and tick "Also delete my Shturmap data"; or delete that folder yourself after uninstalling.
