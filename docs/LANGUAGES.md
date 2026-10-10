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
developer run, `--culture <culture>`.

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
`UiLanguage.InTranslation` **in the same commit**, and reviewed before that commit:

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

A translation is reviewed by someone who didn't write it: the **senior translator**, an agent in the role of a
senior software and game localiser for that language (owner, 2026-10-10). Locally it is
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
