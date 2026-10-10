# German glossary (de)

The words every German text of Shturmap uses, so that translators and the reviewer say the same thing the same way
(docs/LANGUAGES.md, "Translating" step 2 and "Review"). A new term goes in here in the same commit as the text that
needs it. **open**: a choice for the owner or the reviewer, the recommendation first. **check**: not in the data or not
sure, to be looked at in a German game or on a German Windows.

**Sources.** The game's words are counted in tarkov.dev's German data (BSG's translation) in the local cache,
`pve_<payload>_de.json` beside `_en.json` with the same keys, on 2026-10-10. About half of the task texts there are
still English; a word counts as the game's when the translated ones use it. Notes name the payload and a key, or how
many translated texts use the word. Terms and short fragments only, never a whole text (DESIGN.md §3).

**Voice.** As the English: plain, short, concrete, read in a glance from a second monitor; fragments are fine. Not word
for word, never more formal than the English. German runs about a third longer: of two right words take the shorter.

**The player is "du".** The game says du: its objectives are du-imperatives (Finde in 280 texts, Eliminiere 122,
Überlebe 66, Markiere 55), its conditions "du darfst …", "während du …" (60 texts address the player as du), and only a
quest's name says Sie. So "du", "dein", lower case. Instructions in the imperative, in the standard form ("Mach einen
Screenshot …", "Zeig auf eine Quest", "Übergib", not the game's frequent "Übergebe"); buttons and labels in the
infinitive, as Windows' are ("SENDEN", "TOUR STARTEN"). Windows' words, not Windows' "Sie".

**Headings** are in capitals where the English ones are. ß becomes SS (ABSCHLIESSEN; Bahnschrift has no capital ẞ);
Ä, Ö, Ü stay. Running text follows German spelling: nouns capitalised (der Raid, die Quest, der Scav).

**A term the game leaves in English stays English**: PMC, Scav, Raid, Transit, Boss, Spawn, Co-Op, Season, Level, Kappa,
Lightkeeper, Prestige, Rogue, Raider, the map names and most trader names. A compound with an English word or an
abbreviation takes a hyphen, as the game's do (Scav-Spawn, Co-Op-Exfil): PMC-Ausgang, Scav-Raid, Raid-Dauer.

**Game names** (maps, quests, items, traders, extracts) come from the data and never change form: after a colon or in a
list ("Mitnehmen: {item}"), "für {quest}", "an {trader}", "bei {trader}", and "auf {map}" for a map (the game: auf
Shoreline in 56 texts, auf Streets of Tarkov 51, though in Customs 36 to 20; a placeholder needs one). No genitive
("die Karte von {map}", not "{map}s Karte") and no article that depends on the name's gender.

**Open:** quest, extract, card, BRING, pick, NEXT, COMPLETE and PROGRESS, kills, Replay. **Check:** the quest types,
the extract list's header, the game's key names, Windows' settings paths.

## The game's words

| English | German | Note |
| --- | --- | --- |
| quest (the game: task) | Quest (die), Quests | **open.** Shturmap's English already takes the players' word over the game's (English data: task in 60 texts, quest in 1); German players say Quest; one meaning, where Aufgabe is also everyday German. Alternative: **Aufgabe**, the game's (tasks, 35 texts: „Schließe die Aufgabe … ab“), which "as the game says them" would ask for |
| objective | Ziel (das), Ziele | tasks: „die anderen Ziele“ |
| raid; in raid; in one raid | Raid (der), Raids; im Raid; in einem Raid | tasks, 183 texts; „in einem Raid“ and the like, 19 |
| found in raid | im Raid gefunden | tasks, 88 texts („im Raid gefundenen Gegenstand“); no German abbreviation for FIR |
| PMC, PMCs; as a PMC | PMC, PMCs; als PMC | tasks, 78 texts; "PMC operatives" is PMCs |
| Scav, Scavs; player Scav | Scav, Scavs; Spieler-Scav | maps `scavs`; „als Scav“; Spieler-Scav isn't in the data (players' word) |
| Sniper Scav | Scav-Scharfschütze | tasks; maps `ScavRole/Marksman` says only Sniper |
| boss, bosses; guard; Cultist | Boss, Bosse; Wache; Kultist | tasks; maps `Follower`, `sectantPriest` „Kultisten-Priester“; also Wächter („Reshala-Wächter“): names come from the data |
| extract, exfil (noun) | Ausgang (der), Ausgänge | **open.** The game's noun (tasks, 9 texts: „den ersten Ausgang aus Factory“), plain German, and what EXIT says. Alternative: **Extract** (der), the players' word, in the game only in a few names (maps `switch_00403_floor_button (1)` „Extract-Knopf“, „Co-Op-Exfil“) |
| extract (verb); survive and extract | entkommen; überleben und entkommen | tasks, „Überlebe und entkomme“ in 49 texts; „um über den … Ausgang zu entkommen“ |
| transit | Transit (der), Transits | maps `tr_customs_reserve_DESC` „Transit zu Reserve“; tasks „Benutze den Transit von … nach …“ |
| co-op extract | Co-Op-Ausgang | maps `tunnel_shared`: German names keep „(Co-Op)“ |
| flare (signal flare) | Signalpatrone | items `62178c4d4ecf221597654e3d` „Signalpatrone (Rot)“; tasks „rote Signalpatrone“; German extract names drop „(Flare)“ |
| power switch; switch; elevator; trap | Stromschalter; Schalter; Aufzug; Falle | maps `switch_Use_reserve_electric_switcher`, `lab_Elevator_Main`, trap switches „Fallenschalter“; `DamageType_Landmine` Landmine |
| gear; climbing gear: ice pick, paracord | Ausrüstung; Kletterausrüstung: Eispickel, Paracord | items `5c0126f40db834002a125382`, `5c12688486f77426843c7d32`; extract `Alpinist` „Klippenabstieg“ |
| paid car extract (V-Ex) | Auto, kostenpflichtiger Fahrservice | maps `Sandbox_VExit` „Auto bei der Polizeiabsperrung“; tasks „kostenpflichtigen Fahrservice“ |
| map names | as the data: Customs, Woods, … | maps `<id> Name`: English, except „Factory bei Nacht“ |
| trader; trader names | Händler; as the data | items' descriptions „Händler“; traders `<id> Nickname`: Prapor, Therapist …, but Jäger, BTR Fahrer, Radiostation, Überlebender |
| level; loyalty level, LL2; skill | Level; Ansehensstufe, LL2 in compact lines; Fähigkeit | tasks „Level 30“, „Ansehensstufe 3 bei …“; items `Skill`. Stufe for skills, loyalty, prestige and hideout stations |
| hideout; crafting; barter; flea market | Versteck; Herstellung; Tausch; Flohmarkt | items `6398fd8ad3de3849057f5128 ShortName`, `HideoutManagement` „Versteckverwaltung“; `Crafting`; `5448eb774bdc2d0a728b4567` „Tauschgegenstand“; `FleaMarket` |
| item; quest item | Gegenstand (der), Gegenstände; Quest-Gegenstand | tasks, 71 texts; the quest item follows the quest choice |
| key; keycard; access keycard | Schlüssel; Schlüsselkarte; Zugangskarte | items `543be5e94bdc2df1348b4568`, `5c164d2286f774194c5e69fa`; maps `FAC_TRANSIT_14_COND` „Zugangskarte benötigt“ |
| roubles, dollars, euros | Rubel, Dollar, Euro | items `5449016a4bdc2d6f028b456f` |
| head, thorax, stomach, arm, leg; headshot | Kopf, Thorax, Bauch, linker/rechter Arm, linkes/rechtes Bein; Kopfschuss | maps `QuestCondition/Elimination/Kill/BodyPart/*`; tasks (also Kopftreffer) |
| eliminate, kill (verb) | eliminieren | tasks, Eliminiere in 122 texts; töten rarely |
| kill, kills (noun) | Kill (der), Kills | **open.** The players' word, short („höchstens 3 Kills“). Alternative: **Eliminierung**, from the game's verb, long |
| find; locate; obtain | finden; lokalisieren; besorgen | tasks, Finde 280, Lokalisiere 41, Besorge 22 |
| hand over; hand-over; HAND OVER | übergeben („×3 an Therapist übergeben“); Übergabe; ÜBERGABE | tasks, „Übergebe … an Therapist“; imperative Übergib |
| mark; MS2000 Marker | markieren; MS2000 Marker | tasks, Markiere in 55 texts |
| stash; plant | verstauen; platzieren | tasks, Verstaue 56, Platziere 54 |
| complete a quest; fail | abschließen; fehlschlagen | tasks „Schließe die Aufgabe … ab“, „schlägt fehl“; erledigen also |
| spawn; floor; basement | Spawn (der); Stockwerk (Stock); Keller | maps `BotZone` „Beliebiger Scav-Spawn“, `BotZoneFloor1` „Erstes Stockwerk“, `BotZoneBasement` |
| PvE, PvP, Seasonal | PvE, PvP, Season | tasks „Seasoncharakter“, „Season 1“ |
| the extract list's header ("Find an extraction point") | **check** | not in the data; `ExitList` knows only the English header |
| quest types: Elimination, Exploration, Pickup, Place, Find in raid, Survive, Trader | Eliminierung, Erkundung, Aufheben, Platzieren, Im Raid finden, Überleben, Händler | **check**: the English borrows the game's type names (DESIGN.md §5); take the German game's from its Tasks screen, not in the data |

## Shturmap's words

| English | German | Note |
| --- | --- | --- |
| map | Karte (die) | the drawn map and the place; the game's texts say Ort or Bereich for a location |
| card (quest, item, raid card) | Infokarte (Quest-Infokarte, Raid-Infokarte) | **open.** Karte is the map, and „Raidkarte“ would read as the raid's map. Alternative: Karte for the card and **Map** for the map (players' word), an English word on every map label |
| Plan, Raid (the states); to plan | Planung, Raid; planen | |
| NEXT RAID; THIS RAID | NÄCHSTER RAID; DIESER RAID | |
| COMPLETE; PROGRESS | ABSCHLIESSEN; VORANBRINGEN | **open.** Verbs as the English, abschließen being the game's; rows „5 abschließen · 1 voranbringen“. Alternative: **ERLEDIGEN; TEILWEISE**, 9 letters each where the rail is tight |
| BRING; Bring: {item} | MITNEHMEN; Mitnehmen: {item} | **open.** What you take into the raid. Alternative: **MITBRINGEN** (`SampleTexts.de.resx`), bringing something to someone |
| to mark, to plant, to use, to fit, to wear | zum Markieren, zum Platzieren, zum Benutzen, zum Anbauen, zum Tragen | BRING's "what for"; the item card's verbs: Mitnehmen, Im Raid finden, Aufheben, Verstauen, Übergeben, Verkaufen |
| to leave through {exit}; to enter {map}; With: …; Without: … | um über {exit} zu entkommen; um {map} zu betreten; Mit: …; Ohne: … | |
| CHECK YOUR KIT; ALSO USEFUL; FIND IN RAID | AUSRÜSTUNG PRÜFEN; AUCH NÜTZLICH; IM RAID FINDEN | |
| ANY MAP; OTHER MAPS; NOT ON THIS MAP | JEDE KARTE; ANDERE KARTEN; NICHT AUF DIESER KARTE | |
| pick, picked, unpick; CLEAR PICKS; pen | wählen, gewählt, abwählen; ALLE ABWÄHLEN; Stift | **open.** Windows' words for choosing, short; heading PICKED: GEWÄHLT. Alternative: **vormerken, VORGEMERKT**, says "for later", longer |
| NEXT (the glance's nearest objective) | ZIEL | **open.** Says what the row is, as short as AUSGANG under it. Alternative: **ALS NÄCHSTES** |
| EXIT; OR; nearest | AUSGANG; ODER; nächster, nächste | NÄCHSTER · NICHT MIT DEINER LISTE ABGEGLICHEN |
| ON YOUR LIST (THIS RAID); NOT ON YOUR LIST | AUF DEINER LISTE (IN DIESEM RAID); NICHT AUF DEINER LISTE | |
| ??? IN GAME; NOTHING NEEDED; ALL 15 ↓ | ??? IM SPIEL; NICHTS NÖTIG; ALLE 15 ↓ | |
| EXTRACTS AND TRANSITS; ways out | AUSGÄNGE UND TRANSITS; Ausgänge und Transits | |
| the game's extract list; your side | Ausgangsliste; deine Seite | the list follows the extract choice (Extract-Liste); side: PMC ⇄ SCAV |
| anywhere; after the raid | überall; nach dem Raid | the raid card's place for kills and finds; hand-overs |
| done; DONE; tick, untick | erledigt; ERLEDIGT; abhaken, Haken entfernen | „Erledigt · von dir abgehakt, 4. Okt.“ |
| place; spot; loose (lying on the map) | Ort; Stelle; Fundort | „Einer von 4 möglichen Orten“; „Fundorte auf Customs · 3“ (not „Lose“: lottery tickets) |
| marker, symbol (on the map) | Symbol | not Markierung or Marker: those are the MS2000's |
| guide line; facing | Leitlinie; Blickrichtung | on screen the line is a gestrichelte Linie |
| preview; PREVIEW ·; AN EXAMPLE, NOT YOUR RAID | Vorschau; VORSCHAU ·; EIN BEISPIEL, NICHT DEIN RAID | |
| point at; rest on; click; hold (a card); row; the list on the left | zeigen auf; kurz bleiben auf; klicken; festhalten; Zeile; die Liste links | |
| position, fix; last fix; 7 MIN OLD | Position; letzte Position; 7 MIN ALT | |
| Follow my position; Show my position; YOUR NEW POSITION · PRESS F | Meiner Position folgen; Meine Position zeigen; DEINE NEUE POSITION · TASTE F | |
| ahead, ahead-left, left, behind-left, behind, behind-right, right, ahead-right; 8 M UP, DOWN | vorn, vorn links, links, hinten links, hinten, hinten rechts, rechts, vorn rechts; 8 M HÖHER, TIEFER | facing-relative for 45 s; caps VORN LINKS |
| MIN LEFT; MIN IN; raid length; time left | MIN ÜBRIG; MIN IM RAID; Raid-Dauer; Restzeit | figure first: „28 MIN ÜBRIG“ |
| RAID LOADING; RAID OVER; LOADING CANCELLED; SCAV RAID; GROUP PICKED | RAID LÄDT; RAID VORBEI; LADEN ABGEBROCHEN; SCAV-RAID; GRUPPE HAT GEWÄHLT | the cues |
| QUEST COMPLETE; QUESTS COMPLETE; UNLOCKS | QUEST ABGESCHLOSSEN; QUESTS ABGESCHLOSSEN; SCHALTET FREI | |
| NOT IN A RAID; IN RAID; LOADING; LAST RAID · END NOT IN THE LOG | NICHT IM RAID; IM RAID; LÄDT; LETZTER RAID · ENDE NICHT IM LOG | status bar, Plan's last-raid line |
| REPLAY; raid replay | REPLAY; Raid-Replay | **open.** Players' word, short. Alternative: **WIEDERGABE** |
| Active, Completed, Failed, Not started, Locked | Aktiv, Abgeschlossen, Fehlgeschlagen, Nicht begonnen, Gesperrt | notice: „{quest}: begonnen / abgeschlossen / fehlgeschlagen“ |
| from the game log, 25 Sep | aus dem Log des Spiels, 25. Sept. | |
| the game's logs; LOGS, SCREENSHOTS, DATA | die Logs des Spiels; LOGS, SCREENSHOTS, DATEN | Log, not Protokoll: short, and Shturmap's own Log-Ordner |
| notice | Hinweis | |
| the tour; TAKE THE TOUR; SHOW ME; TOUR · 3 OF 7; ESC ENDS THE TOUR | die Tour; TOUR STARTEN; ZEIGEN; TOUR · 3 VON 7; ESC BEENDET DIE TOUR | ← ZURÜCK, WEITER → |
| What's New; NEW IN 0.4.0 · PRAETORIAN | Neuigkeiten; NEU IN 0.4.0 · PRAETORIAN | release names stay |
| HOW SHTURMAP WORKS; QUEST TYPES; ON THE MAP; legend; WIKI MAP ↗ | SO FUNKTIONIERT SHTURMAP; QUEST-TYPEN; AUF DER KARTE; Legende; WIKI-KARTE ↗ | help |
| KEYS · WHEN THIS WINDOW HAS FOCUS; story chapters | TASTEN · WENN DIESES FENSTER DEN FOKUS HAT; Story-Kapitel | help |
| GET IT; after {quest}; from level 10; needed for Kappa | BESCHAFFEN; nach {quest}; ab Level 10; nötig für Kappa | item and quest cards |
| A or B; 13 of 16 kinds; or 5 others | A oder B; 13 von 16 Arten; oder 5 weitere | |
| deadly area; SNIPER ZONE | tödlicher Bereich; SCHARFSCHÜTZENZONE | |
| settings rows | Positions-Screenshots löschen; Ausgangsliste aus Screenshots lesen; Absturzberichte; Updates | options: NACH ABSTURZ FRAGEN · IMMER SENDEN · NIE; AUTOMATISCH · NUR BENACHRICHTIGEN · AUS |
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
| Zoom in; Zoom out; Show the whole map; status bar | Vergrößern; Verkleinern; Ganze Karte zeigen; Statusleiste | |
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
| plural | one, other | `{n, plural, one {# Ausgang} other {# Ausgänge}}` |
