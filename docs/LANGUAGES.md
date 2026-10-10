# Languages

How Shturmap's own texts are written, translated, reviewed and checked, and the checklist for adding a language
(owner, 2026-10-10: English and German first, every language tarkov.dev has to follow). The rules themselves are in
`docs/DESIGN.md` §8, "The app's own language", "Texts" and "Language": one language for everything (Shturmap's
words, formats and the game names), chosen in settings, else the game's, else Windows' display language, else
English; it switches while Shturmap runs.

The game data's names (quests, items, maps, extracts, traders) are tarkov.dev's, in 16 languages: cs, de, en, es,
fr, hu, it, ja, ko, pl, pt, ro, ru, sk, tr, zh (json.tarkov.dev answered for each, 2026-10-10). Shturmap's own words
(about 800 texts) are written here. A language is offered only once all of them are translated and reviewed
(`UiLanguage.Supported`); until then it is "in translation" (`UiLanguage.InTranslation`) and shows only in a
developer build's settings and a developer run, `--culture <culture>`. Offered: English, and German from 2026-10-10.

## Writing a text

Every word Shturmap shows lives in a project's texts file (`src/**/<Name>Texts.resx`), never in code or XAML:

- **A whole sentence with named placeholders**, never pieces glued together: `Bring: {item}`, `Your list this raid:
  {listed} of {extracts, plural, one {# extract} other {# extracts}} (screenshot at {time}).`
- **Plurals in the text** (`{n, plural, one {…} other {…}}`; `=0 {…}` for an exact number), never chosen in code.
- **Game names where they don't change form**: after a colon, in a list, as a heading. Not "Bring the {item}".
- **A comment** saying where the text shows, how much room it has if that's tight, and what each placeholder is
  ("{time}: the screenshot's time, 16:40").
- **Its name** says what it is, prefixed by where it shows (`ExitsHowToCheck`, `SettingsLanguageAutomatic`).
- In code: the generated class, with named arguments (`RuleTexts.ExitsListRead(extracts: n, listed: m, time: t)`).
  In XAML: `{x:Bind local:ViewTexts.<Name>}`.
- Not texts: values stored in settings, ids, file names, log lines, diagnostics and reports (English for whoever
  fixes Shturmap), and the CLI.

## Translating

Every new or changed English text is translated into every language in `UiLanguage.Supported` and
`UiLanguage.InTranslation` **in the same commit**, and reviewed before that commit. **Whoever translates or reviews,
into any language, takes the role of an expert Escape from Tarkov player and a distinguished, academically trained
expert of that language** (owner, 2026-10-10): someone who knows how that language's players talk about the game and
writes the language faultlessly.

1. Write the text in `<Name>Texts.<code>.resx`, with the comment `en: <the English text, exactly>`. That comment is
   how the translation tests know the translation is current: change the English text and the test fails until the
   translation is made again from the new one.
2. Use the language's glossary (`docs/glossary-<code>.md`): the words its Tarkov players use, Shturmap's terms, how
   the player is addressed. Players' words win over the game's translation (owner, 2026-10-10: "Assume the
   perspective of a Tarkov pro gamer that speaks German — much of gamer lingo in German has English terms, so stick
   with that"): German says Quest, Extract, Map, Raid, Loot, Kills, not Aufgabe, Ausgang, Karte. The game's own
   translation (tarkov.dev's data in that language, BSG's) is the source where players have no word of their own.
3. Have it reviewed (below) until the review finds nothing.
4. `.\eng\dotnet.ps1 test --solution Shturmap.slnx`: `TranslationTests` checks every file.

## Review

A translation is reviewed by someone who didn't write it: the **senior translator**, an agent in the role of an
expert Escape from Tarkov player and a distinguished, academically trained expert of that language, with long
experience localising software and games (owner, 2026-10-10). Locally it is
`.claude/agents/senior-translator.md` (the `.claude` folder isn't in the repository); its rules are these, so any
reviewer applies the same. It gets the English text, its comment, the translation, the glossary, and the screenshots
of the layout check in that language, and answers for each text either "OK" or "FIX": the corrected text and why.
The fixes go in, and it reviews again, until every text is OK. It checks:

- **Meaning**: everything the English says, nothing it doesn't; the same facts, conditions and numbers.
- **Voice**: Shturmap's: short, plain, concrete words, read in a glance from a second monitor (DESIGN.md §4,
  principle 2; §8, "Error messages": what failed in plain words). Not word for word, not more formal than the English.
- **The players' words**: as a Tarkov pro who speaks the language says them (the glossary): English gamer terms where
  players use them (German: Quest, Extract, Map, Loot, Kills, Raid), with the language's grammar around them; the
  game's translation only where players have no word of their own (owner, 2026-10-10). The player is addressed as
  the game addresses them (German: "du").
- **Consistency**: the same English term the same way everywhere, as the glossary has it; a new term goes into the
  glossary in the same commit.
- **Placeholders and plurals**: every placeholder kept, in the place the grammar wants; every plural form the
  language needs (`PluralRules`); a game name never in a position that would need another ending.
- **Room**: no longer than the place allows (the comment; the screenshots); an abbreviation only where the language
  uses one.
- **Writing**: the language's quotation marks (German „…“), numbers and units as the language writes them (German
  "5.000 ₽", "2,5 km"), upper-case headings as the English ones are (German ß stays SS in upper case: the font has no
  capital ẞ), key names as that language's keyboards label them (German: Druck for PrtSc, Pos1 for Home, Entf for
  Del), compass points (German: N, NO, O, SO, S, SW, W, NW).

## Layout check

Other languages are longer (German about a third, more in places), so a layout that fits English can cut words off
or overflow. The layout check (`tools\layout-check.ps1`) opens every view of the app in a language at the window sizes
Shturmap supports and reports each text that is cut off where it isn't meant to be, each part that sticks out of its
container or the window, and text over other text, with a screenshot of each view and the problems boxed. Run it in
every offered and in-translation language and in the pseudo-language before a release and after a change to the
layout or to many texts.

**The pseudo-language** (`--culture qps-ploc`, developer runs only) shows every English text accented, about 40%
longer and in brackets: "[Ŝéţţîñğš ···]". It finds what a longer language will break before anyone translates, and
any text without brackets on screen is one that wasn't moved into the texts files. Game names stay English in it.

**Switching while running**: the check also switches the language of a running app and compares every text on screen
with a fresh start in that language; a difference is a text that was kept from before the switch.

**Running it.**

```powershell
.\tools\layout-check.ps1 -Out <folder>                    # every view in en-US, de-DE and qps-ploc, 900x560 and 1600x900
.\tools\layout-check.ps1 -Out <folder> -Languages qps-ploc -States plan,raid -Sizes 900x560
.\tools\layout-check.ps1 -Out <folder> -Languages de-DE -SwitchCheck  # also the switch from English
```

It builds a developer build into `artifacts\layout-check\app` (the check is compiled into developer builds only: Debug,
or `ShturmapDev=true`; `-Exe` takes one already built) and plays each view with `tools\fake-raid.ps1` against the local
tarkov.dev cache, in each culture of `-Languages` (passed as `--culture`) and at each size of `-Sizes`. Sizes are in
DIP and count the whole window, as Windows does: 900x560 is the smallest Shturmap allows (DESIGN.md §4, "The window");
they are scaled to the monitor the app opens on. The views, `-States` to pick some: `plan`, `raid` (the fake raid on
Streets), `report` (the Report dialog with what's sent shown, `--show-report`), `crash` (the question after a crash,
`--show-crash`), `whatsnew` (the newest What's New card, `--whats-new`), `quest` (a quest's card and an item's,
`--show-quest`) and `tour1` to `tour7` (`--tour <n>`); settings is in each, as every snapshot opens it, and help in all
but the tour's, where it opens by itself. A view takes about 20 seconds: all of them in three languages at two sizes
take about 25 minutes.

**What it writes.** A folder per language, size and view (`<folder>\de-DE\900x560\plan`) with the snapshot, and beside
each picture of the window or a popup (`window.png`, `tour.png`, `help.png`, `settings.png`, `card.png`):

- `<picture>.texts.txt`: every text shown, one per line, in the order of the window's elements: a text block's words
  (its runs together; a button's or link's words are text blocks too), `tooltip: …`, `name: …` (what a screen reader
  says), `input: …` (a text box's own text) and `english: …` (what a report sends, English by design and holding the
  run's own log, which the switch check leaves out). Shown means not collapsed, not transparent and not of no size; the
  texts outside every viewport around them (scrolled out of a list, below a popup's fold) follow under
  `--- not in view ---`: a scroll shows them, so they are checked too. The senior translator reads these with the
  pictures.
- `<picture>.layout.json`: the language and culture, the window's size and scale, and the problems, numbered, each with
  its kind, its element (its `x:Name`, else its type and the elements around it), its text, its bounds in window pixels
  and what is wrong.
- `<picture>.problems.png`: the picture with each problem boxed and numbered in its kind's colour (overflow red, trimmed
  orange, overlap magenta, untranslated cyan); one outside the picture has its number at the edge it lies beyond.

`<folder>\summary.md` counts the problems per language and size and lists each once, with the views it shows in and
links to the boxed pictures; the pseudo-language's untranslated texts come as one list, each text once. The exit code
is 1 when a language other than the pseudo-language has a problem or a switch differs, 2 when a view couldn't be
checked; the pseudo-language's problems are warnings. `-SummaryOnly` sums up an earlier run's folder again. The folder
holds pictures of the app and the fake raid's paths: it stays on the PC and goes into no commit.

**The problems** (`src\Shturmap.App\Dev\LayoutCheck.cs`):

- *overflow*: a text or a control that sticks out of the nearest element that clips it (an element given less room
  than it takes is cut at its slot; content in a scrolling list must fit the list's whole extent, not its viewport) or
  out of the window; a text that needs more width or height than it was given and is cut without "…"; a word too wide
  for its line, which breaks inside the word (long German compounds; a break after a hyphen is a line's own, as in
  "SCREENSHOT-TASTE", and what a report sends, with its paths and log lines, may break anywhere).
- *trimmed*: a text that ends in "…" where the design doesn't let it give way. `controls:Fit.MayTrim="True"`
  (`src\Shturmap.App\Controls\Fit.cs`) marks where it does, and only there: the status bar's last fix, Plan's
  synopses after two lines, the notes of EXIT and OR after two lines (DESIGN.md §4). A new place needs the design to say
  so first.
- *overlap*: two texts over each other, where both can be seen. The deliberate layers (the tour, the Report dialog, a
  cue's band, What's New's plate; `LayoutCheck.Layers`) are compared within themselves, not with what they cover.
- *untranslated*, in the pseudo-language only: a text, or part of one, outside the pseudo-language's brackets that isn't
  a game name (the loaded game data's names, tarkov.dev's sentences and a transit's conditions), a release's name, a
  language's own name (settings: ENGLISH, DEUTSCH), a number with its unit, a key, a month or day of the culture, a
  path, or one of the few words that stay the same everywhere (`LayoutWords.Neutral`: Shturmap, PMC, Scav, PvE, PvP,
  Escape from Tarkov, tarkov.dev, GitHub, Sentry, Windows). What a report or a crash report sends, shown before it
  goes, is English by design and isn't looked at (`Controls.Words.English`; DESIGN.md §8, "The app's own language"). A
  game name of one word counts only beside names and figures ("Kaban 75%"): among English words it is one of them, since
  many are items too ("Map", "Raid", "Report"); items' short names, which only the map draws, don't count at all. So a
  word that is also an item's name hides only where it stands alone. The quest synopses, made by rules that read
  English data, are English phrases in the pseudo-language, whose data is English, and in another language their rows
  show tarkov.dev's sentence instead (DESIGN.md §8, "The app's own language"): the phrases the snapshot holds (Plan's
  lines, the raid card's objectives) and the tour's example count as game names. The extract requirements read English
  data too, but are worded with Shturmap's own texts.

Not checked: what the map draws (its labels are a picture), and a tooltip's own layout (only its words are listed).

**`-SwitchCheck`** also starts each view in English and switches to each other language of `-Languages` once the data
is there (`--switch-language <culture>`), then compares each picture's texts with the fresh start in that language,
figures aside (a time or a fix's age differs from run to run) and the fake game's folder (new in each run): a line only
after the switch was kept from before it, a line only in the fresh start is missing after it. A view whose language or
culture after the switch isn't the fresh start's counts as not checked.

**Not in CI.** The views need tarkov.dev's game data, which a fresh runner would download at every push (DESIGN.md §3,
"No unnecessary load on tarkov.dev"), and all of them take about 25 minutes: the check runs on the developer's PC,
before a release and after a change to the layout or to many texts (docs/UPDATES.md).

## Adding a language

Run this checklist from top to bottom; tick each line in the commit that adds the language.

**1. Can it be done**
- [ ] tarkov.dev has it: `maps_`, `tasks_`, `traders_`, `items_` and `hideout_<code>` answer 200 in both modes
      (`regular` and `pve`).
- [ ] The game's code for it is in `GameLanguage.Common` (the game says `ge` for German, `jp` for Japanese), with a
      test in `GameLanguageTests`.
- [ ] The font has its letters. Bahnschrift has Latin with Polish, Czech, Romanian, Hungarian and Turkish letters,
      Cyrillic and Greek (checked 2026-10-10), but no Japanese, Korean or Chinese, no capital ẞ and no narrow no-break
      space (French groups numbers with it): those need a fallback font chosen and checked in screenshots.
- [ ] It is written left to right (all of tarkov.dev's languages are).
- [ ] `PluralRules` knows its plural forms (all of tarkov.dev's languages are in it), with a test.

**2. Translate**
- [ ] `docs/glossary-<code>.md`: the words that language's Tarkov players use (raid, extract, transit, quest, Scav,
      PMC, loot, kills …; often the English ones), the game's translation from tarkov.dev's data where players have
      none; how the game addresses the player; Shturmap's terms.
- [ ] A `<Name>Texts.<code>.resx` beside every `<Name>Texts.resx`, every text with its `en:` comment, every plural form.
- [ ] The tour (`docs/tour.<code>.md`), help, the What's New card from the current release on, notices, dialogs,
      tooltips and screen-reader names (all in the texts files, the tour and What's New beside their English files).
- [ ] Short forms: compass points, key names, units, upper-case headings.
- [ ] Reviewed by the senior translator until every text is OK.

**3. Wire up**
- [ ] The language in `UiLanguage.InTranslation` while it is translated, then moved to `UiLanguage.Supported` (settings
      list it in its own name: "Français"), and its culture in `UiLanguage.CultureFor`.
- [ ] Numbers and dates read right inside its sentences (the layout check's screenshots).
- [ ] Sorting and search follow the language (accents; Turkish i and İ).
- [ ] Notices that name a language ("No French texts on tarkov.dev") name it in the language in use.
- [ ] Help names the Windows text recognition language the extract list needs for a game in that language.

**4. Check**
- [ ] `TranslationTests` pass with the language offered (every text, placeholders, plural forms, made from the current
      English).
- [ ] The layout check passes in it, and the senior translator has looked at its screenshots.
- [ ] Switching to it while running shows the same as a fresh start in it.
- [ ] The extract list: a list drawn by the tests in that game language is read and matched (`ExitListReader` tests).
- [ ] The rule checks of `docs/UPDATES.md` run against the cached data in that language (`shturmap-cli data pve
      <code>`), and the English-only rules (synopses, extract requirements) fall back to tarkov.dev's sentence.
- [ ] Diagnostics show the language and where it came from.

**5. Ship**
- [ ] DESIGN.md (this list of languages), the README, the website and help name the language.
- [ ] A line in What's New and the release notes; the version as CLAUDE.md says.
- [ ] `docs/UPDATES.md` runs its language checks in it too.
