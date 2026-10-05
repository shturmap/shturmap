Shturmap: privacy notice for reports and crash reports
======================================================

DRAFT, 2026-10-03. [OWNER: fill in everything in square brackets before the first public release, and have the
whole notice checked.]

Shturmap runs on your PC. It reads the game's log files, your screenshots' file names and public data from
tarkov.dev, and it keeps its own log on your PC. Of a screenshot you take in a raid it also looks at the top right
corner of the picture: if the game's extract list shows there, it reads which extracts are yours this raid, with the
text recognition built into Windows. That happens on your PC; nothing of the picture is kept or sent, and unticking
"Read the extract list from screenshots" in settings stops it. Nothing about you leaves your PC, with two exceptions that you
control; and it asks GitHub for new versions of itself (3) and downloads public data and pictures (4).

1. A report you send
--------------------
When you press Send in "Report a problem or idea" (the feedback button at the top right), Shturmap sends:

- whether it is a problem or an idea, the text you wrote, and the contact you gave, if any;
- Shturmap's version, whether it is installed or a folder build, and your Windows version;
- if "Include diagnostics" is ticked: the diagnostics that "Show what's sent" displays first. They say whether the
  game, its logs and its screenshots folder were found, the game mode and language, the state of tarkov.dev's data,
  how many quests are active, whether "Delete position screenshots" and "Read the extract list from screenshots" are
  ticked (and the language Windows reads the list in), and the last 200 lines of Shturmap's own log.

2. A crash report, if you allow it
----------------------------------
After Shturmap crashed or ran into an error, it asks at the next start whether to send a report ("Crash reports" in
settings, the gear: Ask after a crash, the default; Always send; Never). A crash report holds the error's type and
message, where in Shturmap's code it happened, Shturmap's and Windows' versions and the last 50 lines of Shturmap's
own log.

3. Checking for a new version
-----------------------------
Installed with its Setup, Shturmap asks GitHub (operated by GitHub, Inc., USA) for a newer version of itself at
start and every 6 hours, and downloads it from there. GitHub sees your internet address, as with any download;
Shturmap sends nothing else. "Updates" in settings: Automatic, the default (a new version downloads in the background and
applies at the next start); Tell me only (it asks, and downloads only when you click); Off (no request at all).

4. Downloading public data and pictures
---------------------------------------
Quest, map and item data, map artwork, trader portraits, item icons and the pictures of three maps (The Lab,
Labyrinth, Icebreaker) are downloaded from tarkov.dev's services and from GitHub's file hosting, and kept in the
download cache on your PC. Those servers see your internet address, as with any download, and which files are asked
for. Portraits, icons and the parts of those three maps are fetched when they are first shown, so the requests show
which of them your Shturmap displayed. No account, profile, quest list or position is sent with them. On those
three maps the part displayed in a raid is usually the part you are in, so the requests for its picture can show
roughly where on the map that is, to tarkov.dev's image service and to no one else.

What is never sent
------------------
Your Windows user folder is masked in everything that is sent (written as %USERPROFILE%), as are your user name
where it is a folder elsewhere and the name of a network PC in a folder path, and every game account, profile or
quest id is cut out. Nothing is sent from memory, no screenshots, no game files, no list of your
quests, no machine name and no IP address is stored with a report. Sentry (below) sees your internet address
when a report arrives, as any server does; each report tells it not to take anything from that address, and the
project is set not to store it. [OWNER: open a received report in Sentry and confirm it shows no address and no
location.]

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
in %LOCALAPPDATA%\Shturmap\outbox until it has gone. A report the service refused stays there as a ".refused.txt"
file, with its text and contact, and isn't sent again: delete it yourself, or with the rest of the data (below).

Your rights
-----------
You can ask for a copy of a report you sent, or for it to be corrected or deleted: write to the contact above and
quote the report id Shturmap showed after sending (8 letters and digits). You can also complain to a data
protection authority.

Everything Shturmap keeps on your PC (settings, quest history, logs, reports waiting to be sent, crash records and
the download cache) is in %LOCALAPPDATA%\Shturmap. It keeps no study log of how you use it: only developer builds
of Shturmap have one. To delete all of it, use settings (the gear) →
"Uninstall Shturmap…" and tick "Also delete my Shturmap data"; or delete that folder yourself after uninstalling.
