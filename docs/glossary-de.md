# German glossary (de)

The words every German text of Shturmap uses, so that translators and the reviewer say the same thing the same way
(docs/LANGUAGES.md, "Translating" step 2 and "Review"). A new term goes in here in the same commit as the text that
needs it. **check**: not in the data or not sure, to be looked at in a German game or on a German Windows.

**The guiding rule: a German Tarkov pro's words** (owner, 2026-10-10: "Assume the perspective of a Tarkov pro gamer
that speaks German — much of gamer lingo in German has English terms, so stick with that"). Where German Tarkov players
say the English word (Quest, Raid, Extract, Map, Kills, Loot, Spawn, Gear, Flea, Hideout, Key, Trader, Replay …), the
text says it too, with German grammar around it: German articles, plurals and verb endings (die Quest, die Quests; der
Extract; extracten, gekillt, gelootet). Where the game's German differs, the note gives the game's word, but the
players' word wins. Where players speak German (mitnehmen, abgeben, abschließen, Ziel), so does Shturmap.

**Sources.** The game's words are counted in tarkov.dev's German data (BSG's translation) in the local cache,
`pve_<payload>_de.json` beside `_en.json` with the same keys, on 2026-10-10. About half of the task texts there are
still English; a word counts as the game's when the translated ones use it. Notes name the payload and a key, or how
many translated texts use the word. Terms and short fragments only, never a whole text (DESIGN.md §3).

**Voice.** As the English: plain, short, concrete, read in a glance from a second monitor; fragments are fine. Not word
for word, never more formal than the English. German runs about a third longer: of two right words take the shorter.

**The player is "du"**, as the game and the players say it: the game's objectives are du-imperatives (Finde in 280
texts, Eliminiere 122), 60 texts address the player as du, none as Sie. So "du", "dein", lower case. Instructions in
the imperative ("Mach einen Screenshot …", "Zeig auf eine Quest"); buttons and labels in the infinitive, as Windows'
are ("SENDEN", "TOUR STARTEN"). Windows' words, not Windows' "Sie".

**Headings** are in capitals where the English ones are. ß becomes SS (ABSCHLIESSEN; Bahnschrift has no capital ẞ);
Ä, Ö, Ü stay. Running text follows German spelling: nouns capitalised, English ones too (die Map, das Gear). A compound
with an English word or an abbreviation takes a hyphen, as the game's do (Scav-Spawn, Co-Op-Exfil): Quest-Karte,
PMC-Extract, Raid-Dauer.

**Game names** (maps, quests, items, traders, extracts) come from the data and never change form: after a colon or in a
list ("Mitnehmen: {item}"), "für {quest}", "bei {trader}", and "auf {map}" for every map, as players say it ("auf
Customs", "auf Woods"). No genitive ("die Map von {map}", not "{map}s Map") and no article that depends on the name's
gender.

## The game's words

| English | German | Note |
| --- | --- | --- |
| quest (the game: task) | Quest (die), Quests | decided (owner, 2026-10-10). The game: Aufgabe (tasks, 35 texts: „Schließe die Aufgabe … ab“) |
| objective | Ziel (das), Ziele | what the game says too (tasks: „die anderen Ziele“) |
| raid; in raid; in one raid | Raid (der), Raids; im Raid; in einem Raid | tasks, 183 texts; „in einem Raid“ and the like, 19 |
| found in raid | FiR; found in raid | „FiR-Items“; caps FIR. The game: im Raid gefunden (tasks, 88 texts) |
| PMC, PMCs; as a PMC | PMC, PMCs; als PMC | tasks, 78 texts |
| Scav, Scavs; player Scav; Sniper Scav | Scav, Scavs; Player-Scav; Sniper-Scav | maps `scavs`. The game: Scav-Scharfschütze; Spieler-Scav isn't in the data |
| boss, bosses; guard; Cultist; Rogue, Raider, Goons | Boss, Bosse; Guard, Guards; Cultist, Cultists; Rogue, Raider, Goons | The game: Wache or Wächter (maps `Follower`), Kultist (`sectantPriest`); names come from the data |
| extract, exfil (noun) | Extract (der), Extracts | decided (owner, 2026-10-10). The game: Ausgang (tasks, 9 texts), Exfil and Extract in a few names (maps `switch_00403_floor_button (1)` „Extract-Knopf“) |
| extract (verb); survive and extract | extracten (ich extracte, extractet); überleben und extracten | players' verb; „rausgehen“ in speech. The game: entkommen („Überlebe und entkomme“, 49 texts) |
| transit | Transit (der), Transits | the game too: maps `tr_customs_reserve_DESC` „Transit zu Reserve“ |
| PMC extract; Scav extract; co-op extract | PMC-Extract; Scav-Extract; Co-Op-Extract | maps `tunnel_shared`: German names keep „(Co-Op)“ |
| flare (signal flare); flare extract | Flare (die), Flares; Flare-Extract | „rote Flare“. The game: Signalpatrone (items `62178c4d4ecf221597654e3d`); German extract names drop „(Flare)“ |
| power switch; switch; elevator; trap | Power-Switch; Schalter; Aufzug; Falle | The game: Stromschalter (maps `switch_Use_reserve_electric_switcher`), Aufzug (`lab_Elevator_Main`) |
| gear; kit; climbing gear | Gear (das); Kit (das); Kletter-Gear (Eispickel, Paracord) | The game: Ausrüstung, Kletterausrüstung; the items' names come from the data (items `5c0126f40db834002a125382`) |
| car extract (V-Ex) | V-Ex (der) | The game: Auto … in extract names (maps `Sandbox_VExit`), kostenpflichtiger Fahrservice |
| map names | as the data: Customs, Woods, … | maps `<id> Name`: English, except „Factory bei Nacht“ |
| trader; trader names | Trader (der), Trader; as the data | The game: Händler; traders `<id> Nickname`: Prapor, Therapist …, but Jäger, BTR Fahrer, Radiostation, Überlebender |
| loyalty level; level; skill | LL (LL2), Loyalty Level; Level; Skill (der), Skills | The game: Ansehensstufe, Level, Fähigkeit (items `Skill`) |
| hideout; craft, crafting; barter | Hideout (das); Craft (der), craften; Barter (der) | The game: Versteck (`HideoutManagement` „Versteckverwaltung“), Herstellung (items `Crafting`), Tausch (items `5448eb774bdc2d0a728b4567`) |
| flea market | Flea (der), auf dem Flea | The game: Flohmarkt (items `FleaMarket`) |
| item; quest item | Item (das), Items; Quest-Item | The game: Gegenstand (tasks, 71 texts) |
| key; keycard | Key (der), Keys; Keycard (die), Keycards | „Labs-Keycard“. The game: Schlüssel, Schlüsselkarte, Zugangskarte (items `543be5e94bdc2df1348b4568`, `5c164d2286f774194c5e69fa`); item names come from the data in German |
| loot (noun); to loot; pick up | Loot (das); looten (gelootet); looten | |
| roubles, dollars, euros | Rubel, Dollar, Euro | the game too (items `5449016a4bdc2d6f028b456f`) |
| head, thorax, stomach, arm, leg; headshot | Kopf, Thorax, Bauch, linker/rechter Arm, linkes/rechtes Bein; Headshot | maps `QuestCondition/Elimination/Kill/BodyPart/*`. The game: Kopfschuss |
| kill (verb); kill, kills (noun) | killen (gekillt); Kill (der), Kills | decided („höchstens 3 Kills“). The game: eliminieren (Eliminiere in 122 texts) |
| find; locate; obtain | finden; finden; holen | tasks, Finde 280; the game also Lokalisiere, Besorge |
| hand over; hand-over; HAND OVER | abgeben („×3 bei Therapist abgeben“); Abgabe; ABGABE | players' word. The game: übergeben („Übergebe … an Therapist“) |
| mark; MS2000 Marker | markieren; MS2000 Marker | the game too (Markiere, 55 texts) |
| stash; plant | verstecken; platzieren | The game: verstauen (56 texts), platzieren (54) |
| complete a quest; fail | abschließen; failen (gefailt) | The game: abschließen („Schließe die Aufgabe … ab“), fehlschlagen |
| spawn; spot; floor; basement | Spawn (der); Spot (der), Spots; Stock (der); Keller | maps `BotZone` „Beliebiger Scav-Spawn“, `BotZoneFloor1` „Erstes Stockwerk“, `BotZoneBasement` |
| PvE, PvP, Seasonal; group | PvE, PvP, Season; Gruppe | tasks „Seasoncharakter“, „Season 1“ |
| the extract list's header ("Find an extraction point") | **check** | not in the data; `ExitList` knows only the English header |
| quest types: Elimination, Exploration, Pickup, Place, Find in raid, Survive, Trader | Kills, Erkunden, Pickup, Platzieren, FiR, Überleben, Trader | players' words; **check** the German game's Tasks screen (not in the data) and note its names |

## Shturmap's words

| English | German | Note |
| --- | --- | --- |
| map | Map (die), Maps | decided (owner, 2026-10-10); the drawn map and the place |
| card (quest, item, raid card) | Karte (die): Quest-Karte, Item-Karte, Raid-Karte | decided (owner, 2026-10-10) |
| Plan, Raid (the states); to plan | Planung, Raid; planen | |
| NEXT RAID; THIS RAID | NÄCHSTER RAID; DIESER RAID | |
| COMPLETE; PROGRESS | ABSCHLIESSEN; PROGRESS | decided: players finish a quest („abschließen“) and make Progress on it; rows „5 abschließen · 1 Progress“, „2 VON 5 ZIELEN HIER“ |
| BRING; Bring: {item} | MITNEHMEN; Mitnehmen: {item} | decided: what players say about taking things into the raid |
| to mark, to plant, to use, to fit, to wear | zum Markieren, zum Platzieren, zum Benutzen, zum Anbauen, zum Tragen | BRING's "what for"; the item card's verbs: Mitnehmen, FiR finden, Looten, Verstecken, Abgeben, Verkaufen |
| to leave through {exit}; to enter {map}; With: …; Without: … | um über {exit} zu extracten; um {map} zu betreten; Mit: …; Ohne: … | |
| CHECK YOUR KIT; ALSO USEFUL; FIND IN RAID | KIT CHECKEN; AUCH NÜTZLICH; FIR FINDEN | |
| ANY MAP; OTHER MAPS; NOT ON THIS MAP | JEDE MAP; ANDERE MAPS; NICHT AUF DIESER MAP | |
| pick, picked, unpick; a pick; CLEAR PICKS; pen | picken, gepickt, Pick entfernen; der Pick, die Picks; PICKS LÖSCHEN; Stift | decided; heading PICKED: GEPICKT |
| NEXT (the glance's nearest objective) | ZIEL | decided: what the row is, as short as EXTRACT under it |
| EXIT; OR; nearest | EXTRACT; ODER; nächster, nächste | decided; NÄCHSTER · NICHT MIT DEINER LISTE ABGEGLICHEN |
| ON YOUR LIST (THIS RAID); NOT ON YOUR LIST | AUF DEINER LISTE (IN DIESEM RAID); NICHT AUF DEINER LISTE | |
| ??? IN GAME; NOTHING NEEDED; ALL 15 ↓ | ??? IM SPIEL; NICHTS NÖTIG; ALLE 15 ↓ | |
| EXTRACTS AND TRANSITS; ways out | EXTRACTS UND TRANSITS; Extracts und Transits | |
| the game's extract list; your side | Extract-Liste; deine Seite | side: PMC ⇄ SCAV |
| anywhere; after the raid | überall; nach dem Raid | the raid card's place for kills and finds; hand-overs |
| done; DONE; tick, untick | erledigt; ERLEDIGT; abhaken, Haken entfernen | „Erledigt · von dir abgehakt, 4. Okt.“ |
| place; loose (lying on the map) | Spot; Loot-Spot | „Einer von 4 möglichen Spots“; „Loot-Spots auf Customs · 3“ |
| marker (on the map) | Marker (der) | the MS2000 Marker too; the context tells |
| guide line; facing | Linie; Blickrichtung | on screen: gestrichelte Linie |
| preview; PREVIEW ·; AN EXAMPLE, NOT YOUR RAID | Vorschau; VORSCHAU ·; EIN BEISPIEL, NICHT DEIN RAID | |
| point at; rest on; click; hold (a card); row; the list on the left | zeigen auf; kurz bleiben auf; klicken; festhalten; Zeile; die Liste links | |
| position, fix; last fix; 7 MIN OLD | Position; letzte Position; 7 MIN ALT | |
| Follow my position; Show my position; YOUR NEW POSITION · PRESS F | Meiner Position folgen; Meine Position zeigen; DEINE NEUE POSITION · TASTE F | |
| ahead, ahead-left, left, behind-left, behind, behind-right, right, ahead-right; 8 M UP, DOWN | vorne, vorne links, links, hinten links, hinten, hinten rechts, rechts, vorne rechts; 8 M HÖHER, TIEFER | facing-relative for 45 s; caps VORNE LINKS |
| MIN LEFT; MIN IN; raid length; time left | MIN ÜBRIG; MIN IM RAID; Raid-Dauer; Restzeit | figure first: „28 MIN ÜBRIG“ |
| RAID LOADING; RAID OVER; LOADING CANCELLED; SCAV RAID; GROUP PICKED | RAID LÄDT; RAID VORBEI; LADEN ABGEBROCHEN; SCAV-RAID; GRUPPE HAT GEPICKT | the cues |
| QUEST COMPLETE; QUESTS COMPLETE; UNLOCKS | QUEST ABGESCHLOSSEN; QUESTS ABGESCHLOSSEN; SCHALTET FREI | |
| NOT IN A RAID; IN RAID; LOADING; LAST RAID · END NOT IN THE LOG | NICHT IM RAID; IM RAID; LÄDT; LETZTER RAID · ENDE NICHT IM LOG | status bar, Plan's last-raid line |
| REPLAY; raid replay | REPLAY; Raid-Replay (das) | decided |
| Active, Completed, Failed, Not started, Locked | Aktiv, Abgeschlossen, Gefailt, Nicht gestartet, Gesperrt | notice: „{quest}: gestartet / abgeschlossen / gefailt“ |
| from the game log, 25 Sep | aus dem Log des Spiels, 25. Sept. | |
| the game's logs; LOGS, SCREENSHOTS, DATA | die Logs des Spiels; LOGS, SCREENSHOTS, DATEN | Log, not Protokoll |
| notice | Hinweis | |
| the tour; TAKE THE TOUR; SHOW ME; TOUR · 3 OF 7; ESC ENDS THE TOUR | die Tour; TOUR STARTEN; ZEIGEN; TOUR · 3 VON 7; ESC BEENDET DIE TOUR | ← ZURÜCK, WEITER → |
| What's New; NEW IN 0.4.0 · PRAETORIAN | Neuigkeiten; NEU IN 0.4.0 · PRAETORIAN | release names stay |
| HOW SHTURMAP WORKS; QUEST TYPES; ON THE MAP; legend; WIKI MAP ↗ | SO FUNKTIONIERT SHTURMAP; QUEST-TYPEN; AUF DER MAP; Legende; WIKI-MAP ↗ | help |
| KEYS · WHEN THIS WINDOW HAS FOCUS; story chapters | TASTEN · WENN DIESES FENSTER DEN FOKUS HAT; Story-Kapitel | help |
| GET IT; after {quest}; from level 10; needed for Kappa | BESORGEN; nach {quest}; ab Level 10; nötig für Kappa | item and quest cards |
| A or B; 13 of 16 kinds; or 5 others | A oder B; 13 von 16 Arten; oder 5 weitere | |
| deadly area; SNIPER ZONE | tödlicher Bereich; SNIPER-ZONE | |
| settings rows | Positions-Screenshots löschen; Extract-Liste aus Screenshots lesen; Absturzberichte; Updates | options: NACH ABSTURZ FRAGEN · IMMER SENDEN · NIE; AUTOMATISCH · NUR BENACHRICHTIGEN · AUS |
| GAME FOLDER; CHOOSE GAME FOLDER…; FIND AUTOMATICALLY | SPIELORDNER; SPIELORDNER WÄHLEN…; AUTOMATISCH SUCHEN | „(von dir gewählt)“, „(automatisch gefunden)“, NICHT GEFUNDEN |

## Windows and UI words

Microsoft's German terms; Windows says "Sie", Shturmap "du".

| English | German | Note |
| --- | --- | --- |
| Settings; Preferences | Einstellungen; Optionen | PREFERENCES as OPTIONEN, so EINSTELLUNGEN isn't said twice |
| Help; Feedback | Hilfe; Feedback | |
| Cancel; Close; Dismiss | Abbrechen; Schließen; Schließen | caps SCHLIESSEN; "Dismiss the notice": Hinweis schließen |
| Send; Don't send; Always send | Senden; Nicht senden; Immer senden | |
| Copy; Download; Install; installed; Uninstall; Restart now | Kopieren; Herunterladen; Installieren; installiert; Deinstallieren; Jetzt neu starten | |
| update, updates; Automatic; On; Off; Never; Always | Update, Updates; Automatisch; Ein; Aus; Nie; Immer | Update is Windows Update's word |
| App and data; Privacy; Licences | App und Daten; Datenschutz; Lizenzen | |
| crash; crash report | Absturz; Absturzbericht | |
| report (noun); to report; Report a problem or idea | Bericht; melden; Problem oder Idee melden | PROBLEM · IDEE |
| diagnostics; Include diagnostics | Diagnosedaten; Diagnosedaten anhängen | COPY DIAGNOSTICS: DIAGNOSEDATEN KOPIEREN |
| folder; log folder; choose a folder | Ordner; Log-Ordner; Ordner wählen | Windows' picker is titled „Ordner auswählen“ |
| this PC; Documents; user folder | dieser PC; Dokumente; Benutzerordner | Explorer's names; the path stays `Documents` |
| screenshot; screenshot key; internet address | Screenshot (der); Screenshot-Taste; IP-Adresse | einen Screenshot machen |
| Zoom in; Zoom out; Show the whole map; status bar | Vergrößern; Verkleinern; Ganze Map zeigen; Statusleiste | |
| email; (optional); (Required) | E-Mail; (optional); (Pflichtfeld) | |
| Language: Automatic · English · Deutsch | Sprache: Automatisch · English · Deutsch | each language in its own name |
| Windows Settings → Apps | Einstellungen → Apps → Installierte Apps | **check** on a German Windows 11 |
| Windows Settings → Time & language → Language | Einstellungen → Zeit und Sprache → Sprache und Region | **check**; Windows 10: Sprache |
| text recognition (language feature) | Texterkennung; „Optische Zeichenerkennung“ | **check** the feature's name in the language's options |
| animation effects | Animationseffekte | **check**: Einstellungen → Barrierefreiheit → Visuelle Effekte |

## Keys

As German keyboards label them. Shturmap names the screenshot key from the game's settings (`GameSettingsReader`, in
English: "PrtSc", "Home"); German needs these labels. The game's own German key names aren't in the data: **check** in
its settings. In a sentence put "Taste" before a key that is also a word ("TASTE DRUCK", not "DRUCK DRÜCKEN").

| English | German | Note |
| --- | --- | --- |
| PrtSc | Druck | many keyboards: Druck S-Abf |
| Home; End; Delete; Insert | Pos1; Ende; Entf; Einfg | |
| PgUp; PgDn | Bild↑; Bild↓ | |
| Ctrl; Right Ctrl | Strg; Strg rechts | Ctrl + , is Strg + , |
| Shift; Right Shift | Umschalt; Umschalt rechts | keycap often only ⇧; Shift + F is Umschalt + F |
| Alt; Alt Gr; Esc; Tab; F1; Num 5 (keypad) | Alt; Alt Gr; Esc; Tab; F1; Num 5 | unchanged |
| Enter; Backspace; Space | Eingabe; Rücktaste; Leertaste | keycaps ↵ and ⟵ |
| Caps Lock; Scroll Lock; Num Lock; Pause | Feststelltaste; Rollen; Num; Pause | |
| arrow keys ← → ↑ ↓ | Pfeiltasten ← → ↑ ↓ | |
| mouse; wheel; double-click | Maus; Mausrad; Doppelklick | |
| O twice | zweimal O | the game's key for the extract list |

## Formats and typography

| English | German | Note |
| --- | --- | --- |
| 5,000; 41,283; 2.5 | 5.000; 41.283; 2,5 | `N0` with `UiLanguage.Culture` |
| 5,000 ₽ | 5.000 ₽; 1.000 $; 1.000 € | the number, a no-break space, the sign |
| ×3; 2× Bolts | ×3; 2× | as the game and the English |
| 25 Sep | 25. Sept. | .NET's de-DE with `d. MMM` (Jan., März, Mai, Juni, Juli, Sept., Okt.); `d MMM` gives „25 Sept.“, wrong in German |
| 25/09; 16:40 | 25.09.; 16:40 | 24 h; no „Uhr“ in labels |
| 12 min; 45 s; 6 h | 12 min; 45 s; 6 h | unit symbols; caps 12 MIN |
| 69 m; 2.5 km; 38 % | 69 m; 2,5 km; 38 % | a space before the unit and % |
| N, NE, E, SE, S, SW, W, NW | N, NO, O, SO, S, SW, W, NW | map-up is north |
| "…" and '…' | „…“ and ‚…‘ | |
| apostrophe; e.g. | ’; z. B. | genitive without an apostrophe: Prapors; z. B. with a no-break space |
| dash; range | „ – “; 21:00–05:00 | en dash: with spaces between words, none in a range |
| CHOOSE… | WÄHLEN… | the ellipsis close to the word, as in Windows' menus |
| ·, ↗, ↓, → | unchanged | |
| upper case | ß → SS; Ä, Ö, Ü stay | `Caps.Of` and `ToUpper` keep ß („STRAßE“): a data name with ß needs SS in capitals |
| plural | one, other | `{n, plural, one {# Extract} other {# Extracts}}` |
