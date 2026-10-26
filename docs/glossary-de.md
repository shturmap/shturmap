# German glossary (de)

The words every German text of Shturmap uses, so that translators and the reviewer say the same thing the same way
(docs/LANGUAGES.md, "Translating" step 2 and "Review"). A new term goes in here in the same commit as the text that
needs it. **check**: not in the data or not sure, to be looked at in a German game or on a German Windows.

**The guiding rule: a German Tarkov pro's words** (owner, 2026-10-10: "Assume the perspective of a Tarkov pro gamer
that speaks German — much of gamer lingo in German has English terms, so stick with that"). Where German Tarkov players
say the English word (Quest, Raid, Extract, Map, Kills, Loot, Spawn, Gear, Flea, Hideout, Key, Trader, Replay, Daily,
Weekly …), the text says it too, with German grammar around it: German articles, plurals and verb endings (die Quest,
die Quests; der Extract; extracten, gekillt, gelootet). Where the game's German differs, the note gives the game's word,
but the players' word wins. Where players speak German (mitnehmen, abgeben, abschließen, Ziel), so does Shturmap.

**Sources.** The game's words are counted in tarkov.dev's German data (BSG's translation) in the local cache,
`pve_<payload>_de.json` beside `_en.json` with the same keys, on 2026-10-10. About half of the task texts there are
still English; a word counts as the game's when the translated ones use it. Notes name the payload and a key, or how
many translated texts use the word. Terms and short fragments only, never a whole text (DESIGN.md §3).

**Voice.** As the English: plain, short, concrete, read in a glance from a second monitor; fragments are fine. Not word
for word, never more formal than the English. German runs about a third longer: of two right words take the shorter.

**The player is "du"**, as the game and the players say it: the game's objectives are du-imperatives (Finde in 280
texts, Eliminiere 122), 60 texts address the player as du, none as Sie. So "du", "dein", lower case. Instructions in
the short imperative ("Mach einen Screenshot …", "Zeig auf eine Quest", "Wähl", "Prüf", "Kopier", not "Wähle"; the e
stays where German needs it: "Öffne", "starte", "melde"); buttons and labels in the infinitive, as Windows' are
("SENDEN", "TOUR STARTEN"). Windows' words, not Windows' "Sie".

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
| boss, bosses; guard; Cultist; Rogue, Raider, Goons; AI squad | Boss, Bosse; Guard, Guards; Cultist, Cultists; Rogue, Raider, Goons; KI-Squad | The game: Wache or Wächter (maps `Follower`), Kultist (`sectantPriest`); names come from the data |
| extract, exfil (noun) | Extract (der), Extracts | decided (owner, 2026-10-10). The game: Ausgang (tasks, 9 texts), Exfil and Extract in a few names (maps `switch_00403_floor_button (1)` „Extract-Knopf“) |
| extract (verb); survive and extract | extracten (ich extracte, extractet); überleben und extracten | players' verb; „rausgehen“ in speech. The game: entkommen („Überlebe und entkomme“, 49 texts) |
| transit | Transit (der), Transits | the game too: maps `tr_customs_reserve_DESC` „Transit zu Reserve“ |
| PMC extract; Scav extract; co-op extract; shared extract, extract for both sides | PMC-Extract; Scav-Extract; Co-Op-Extract; Extract für beide Seiten | maps `tunnel_shared`: German names keep „(Co-Op)“ |
| flare (signal flare); flare extract | Flare (die), Flares; Flare-Extract | „grüne Flare“ (a flare extract takes a green one; „rote Flare“ until 2026-10-17). The game: Signalpatrone (items `6217726288ed9f0845317459`); German extract names drop „(Flare)“ |
| power switch; switch; elevator; trap | Power-Switch; Schalter; Aufzug; Falle | The game: Stromschalter (maps `switch_Use_reserve_electric_switcher`), Aufzug (`lab_Elevator_Main`) |
| step (one of what leaving through an extract takes: a switch, a keycard) | Schritt (der), Schritte | „Zuerst D-2-Stromschalter · 2 Schritte“ (the switch's name is the game's) |
| gear; kit; climbing gear | Gear (das); Kit (das); Kletter-Gear (Eispickel, Paracord) | The game: Ausrüstung, Kletterausrüstung; the items' names come from the data (items `5c0126f40db834002a125382`) |
| car extract (V-Ex) | V-Ex (der) | The game: Auto … in extract names (maps `Sandbox_VExit`), kostenpflichtiger Fahrservice |
| what is so of an exit: for the door (a key); no backpack; single use; up to 4 players; may be closed | für die Tür; kein Rucksack; einmalig; bis zu 4 Spieler; kann geschlossen sein | the raid card's lines under an exit („Factory-Notausgangsschlüssel für die Tür“, „20.000 ₽ zahlen · einmalig · bis zu 4 Spieler“). The game: Rucksack (items `5ab8ee7786f7742d8f33f0b9` „VKBO Armeerucksack“) |
| the armored train; lever; pump; min left, min in (the raid clock's) | Zug (der); Hebel (der); Pumpe (die); Restzeit, min im Raid | „Zug bei 16 min Restzeit · bleibt 7 min“, „ab 15 min im Raid“, „nach dem Hebel 4 min offen“, „~80 s nach der Pumpe“; the clock's MIN ÜBRIG, MIN IM RAID. The game: Gepanzerter Zug (maps `EXFIL_Train`) |
| map names | as the data: Customs, Woods, … | maps `<id> Name`: English, except „Factory bei Nacht“ |
| trader; trader names | Trader (der), Trader; as the data | The game: Händler; traders `<id> Nickname`: Prapor, Therapist …, but Jäger, BTR Fahrer, Radiostation, Überlebender |
| loyalty level; level; skill | LL (LL2), Loyalty Level; Level; Skill (der), Skills | The game: Ansehensstufe, Level, Fähigkeit (items `Skill`) |
| hideout; craft, crafting; barter | Hideout (das); Craft (der), craften; Barter (der) | The game: Versteck (`HideoutManagement` „Versteckverwaltung“), Herstellung (items `Crafting`), Tausch (items `5448eb774bdc2d0a728b4567`) |
| flea market | Flea (der), auf dem Flea | The game: Flohmarkt (items `FleaMarket`) |
| item; quest item | Item (das), Items; Quest-Item | The game: Gegenstand (tasks, 71 texts) |
| receiver (a gun without its parts, as tarkov.dev's icon shows it); assembled | Gehäuse (das); zusammengebaut | the game's: items' category „Receiver“ is „Gehäuse“ (an upper receiver „obere Empfangseinheit“); What's New 0.7.0 „nicht nur als Gehäuse“ |
| key; keycard | Key (der), Keys; Keycard (die), Keycards | „Labs-Keycard“. The game: Schlüssel, Schlüsselkarte, Zugangskarte (items `543be5e94bdc2df1348b4568`, `5c164d2286f774194c5e69fa`); item names come from the data in German |
| loot (noun); to loot; pick up | Loot (das); looten (gelootet); looten | |
| roubles, dollars, euros | Rubel, Dollar, Euro | the game too (items `5449016a4bdc2d6f028b456f`) |
| head, thorax, stomach, arm, leg; headshot | Kopf, Thorax, Bauch, linker/rechter Arm, linkes/rechtes Bein; Headshot | maps `QuestCondition/Elimination/Kill/BodyPart/*`. The game: Kopfschuss |
| kill (verb); kill, kills (noun) | killen (gekillt); Kill (der), Kills | decided („höchstens 3 Kills“). The game: eliminieren (Eliminiere in 122 texts) |
| find; locate; obtain | finden; finden; holen | tasks, Finde 280; the game also Lokalisiere, Besorge |
| hand over; hand-over | abgeben („×3 bei Therapist abgeben“); Abgabe | players' word. The game: übergeben („Übergebe … an Therapist“). No text names a HAND OVER label any more: the hand-over mark is an item followed by → and the trader's portrait |
| the hand-over mark: an item followed by → and a trader's portrait; portrait | „Item mit → und Trader-Porträt“; das Trader-Porträt | help's HelpTicks (owner, 2026-10-10) |
| mark; MS2000 Marker | markieren; MS2000 Marker | the game too (Markiere, 55 texts) |
| stash; plant | verstecken; platzieren | The game: verstauen (56 texts), platzieren (54) |
| stash (the player's storage); STASH | Stash (der); STASH | players' word (2026-10-11, DESIGN.md §4 "What you have"): „in deinem Stash“. Not in the data: its „Lager“ and „Versteck“ are storerooms and the Hideout |
| key tool; case (a container); found in raid (on a mark) | Key Tool (das); Case (das); FiR | players' words. The game: Schlüssel-Organizer („Schl.Orga.“), Koffer, Behälter |
| insurance; returned by insurance | Versicherung (die); aus der Versicherung zurück | the game: Versicherung (hideout); players say it too |
| complete a quest; fail | abschließen; failen (gefailt) | The game: abschließen („Schließe die Aufgabe … ab“), fehlschlagen |
| spawn; spot; floor, floors; basement | Spawn (der); Spot (der), Spots; Stock (der), Stockwerke (never Stöcke), in compounds Stockwerk- (STOCKWERKDATEN); Keller | maps `BotZone` „Beliebiger Scav-Spawn“, `BotZoneFloor1` „Erstes Stockwerk“, `BotZoneBasement` |
| PvE, PvP, Seasonal; group | PvE, PvP, Season; Gruppe | tasks „Seasoncharakter“, „Season 1“ |
| the extract list's header ("Find an extraction point") | **check** | not in the data; `ExitList` knows only the English header (docs/NEXT.md, "2026-10-11: German, left to check in the game") |
| quest types: Elimination, Exploration, Pickup, Place, Find in raid, Survive, Trader | Kills, Erkunden, Pickup, Platzieren, FiR, Überleben, Trader | players' words; **check** the German game's Tasks screen (not in the data) and note its names (docs/NEXT.md) |

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
| YOUR EXTRACTS THIS RAID (the feature's name: What's New since 0.4.0, the extract list's cue) | DEINE EXTRACTS IM RAID; Deine Extracts im Raid | a name, as wide as the English in the cue's 48 px title; in a sentence „this raid“ stays „in diesem Raid“ |
| called out; the call-out (the extract list's roll call, What's New 0.6.0) | aufgerufen; der Aufruf | names called one at a time („der Reihe nach aufrufen“): „Deine Extracts, aufgerufen“, „Kapitel 5 zeigt den Aufruf“; no other text says „aufrufen“, so it doesn't read as „open“ |
| nearest first | die nächsten zuerst; nächste zuerst where room is short | as sort options say „Neueste zuerst“ |
| fade in (the raid's picture coming up from black at its start) | einblenden; beim Einblenden des Raids | software's ein-/ausblenden; not „aufblenden“ (headlights) |
| ??? IN GAME; NOTHING NEEDED; ALL 15 ↓ | ??? IM SPIEL; NICHTS NÖTIG; ALLE 15 ↓ | |
| EXTRACTS AND TRANSITS; ways out | EXTRACTS UND TRANSITS; Extracts und Transits | |
| the game's extract list; to check (against it); your side; for your side | Extract-Liste; abgleichen; deine Seite; für deine Seite | side: PMC ⇄ SCAV; „Mach zum Abgleichen einen Screenshot der Extract-Liste“ |
| anywhere; after the raid; raid start | überall; nach dem Raid; Raid-Start (der) | the raid card's place for kills and finds; hand-overs; „beim Raid-Start oder zweimal O“ |
| done; DONE; tick, untick | erledigt; ERLEDIGT; abhaken, Haken entfernen | „Erledigt · von dir abgehakt, 4. Okt.“ |
| note (the player's own words: on a quest card, or added to a sent crash report); NOTES; Add a note…; ADD A NOTE; Delete this note | Notiz (die), Notizen; NOTIZEN; Notiz hinzufügen…; NOTIZ HINZUFÜGEN; Diese Notiz löschen | the quest card's last section (DESIGN.md §4, "Quest cards", *Notes*) and the crash question's link; never Hinweis, which is the app's notice |
| place; loose (lying on the map) | Spot; Loot, Loot-Spot | „Einer von 4 möglichen Spots“; „Loot auf Customs · 3 Spots“, „Loot hier · 3 Spots“; the legend: „Loot-Spot eines Items“ |
| marker (on the map) | Marker (der) | the MS2000 Marker too; the context tells |
| guide line; facing | Linie; Blickrichtung | on screen: gestrichelte Linie |
| the legend's shapes: disc, ring, cone, triangle, diamond, octagon, hexagon, square, dot, chevron, badge, padlock, trunk, check, glyph, power symbol | Scheibe, Ring, Kegel, Dreieck, Raute, Achteck, Sechseck, Quadrat, Punkt, Pfeil, Badge, Schloss, Kofferraum, Haken, Symbol, Ein/Aus-Symbol | MapTexts' legend |
| hollow, open, outline; hatched | Umriss (der); schraffiert | „Sechseck-Umriss“, „Quadrat-Umriss“; not „hohl“ |
| struck out (a symbol with a diagonal stroke through it) | durchgestrichen | „die übrigen werden durchgestrichen“, „durchgestrichen, Name blass“; as German interfaces say „durchgestrichenes Mikrofon“; not „durchgekreuzt“ (that is an X) |
| the legend's colours: sand, gold, green, teal, khaki, violet, red, grey; faint; lit, bright | sandfarben, golden, grün, türkisfarben, khakifarben, violett, rot, grau; blass; hell | „sandfarbene Scheibe“, „geteiltes khakifarbenes Dreieck“ |
| preview; PREVIEW ·; AN EXAMPLE, NOT YOUR RAID | Vorschau; VORSCHAU ·; EIN BEISPIEL, NICHT DEIN RAID | |
| point at; point (alone, a heading); rest on; click; hold, keep (a card); row; the list on the left; TOP LEFT (where the update band is pinned) | zeigen auf; draufzeigen; kurz bleiben auf; klicken; festhalten; Zeile; die Liste links; OBEN LINKS | „Zeig auf eine Quest“; PICKEN UND DRAUFZEIGEN (ZEIGEN alone is SHOW ME); „hält ihre Karte fest“; never „links in der Liste“ (the left edge of a list); "the rail" is the project's word, never on screen |
| position, fix; last fix; 7 MIN OLD | Position; letzte Position; 7 MIN ALT | |
| Follow my position; Show my position; YOUR NEW POSITION · PRESS F | Meiner Position folgen; Meine Position zeigen; DEINE NEUE POSITION · TASTE F | |
| ahead, ahead-left, left, behind-left, behind, behind-right, right, ahead-right; 8 M UP, DOWN | vorne, vorne links, links, hinten links, hinten, hinten rechts, rechts, vorne rechts; 8 M HÖHER, TIEFER | facing-relative for 45 s; caps VORNE LINKS |
| MIN LEFT; MIN IN; raid length; time left | MIN ÜBRIG; MIN IM RAID; Raid-Dauer; Restzeit | figure first: „28 MIN ÜBRIG“ |
| RAID LOADING; RAID OVER; LOADING CANCELLED; SCAV RAID; GROUP PICKED | RAID LÄDT; RAID VORBEI; LADEN ABGEBROCHEN; SCAV-RAID; GRUPPE HAT GEPICKT | the cues |
| QUEST COMPLETE; QUESTS COMPLETE; UNLOCKS | QUEST ABGESCHLOSSEN; QUESTS ABGESCHLOSSEN; SCHALTET FREI | |
| NOT IN A RAID; IN RAID; LOADING; LAST RAID · END NOT IN THE LOG | NICHT IM RAID; IM RAID; LÄDT; LETZTER RAID · ENDE NICHT IM LOG | status bar, Plan's last-raid line |
| REPLAY; raid replay; in order | REPLAY; Raid-Replay (das); der Reihe nach | decided |
| Active, Completed, Failed, Not started, Locked | Aktiv, Abgeschlossen, Gefailt, Nicht gestartet, Gesperrt | notice: „{quest}: gestartet / abgeschlossen / gefailt“ |
| from the game log, 25 Sep | aus dem Log des Spiels, 25. Sept. | |
| the game's logs; game logs; the game's log shows …, the game's logs show …; LOGS, SCREENSHOTS, DATA | die Logs des Spiels; Spiel-Logs; Laut Log des Spiels …, Laut den Logs des Spiels …; LOGS, SCREENSHOTS, DATEN | Log, not Protokoll; „Neuere Spiel-Logs in {folder}“; „Laut Log des Spiels läuft ein Raid“, „Laut den Logs des Spiels ist keine Quest aktiv.“ |
| notice | Hinweis | |
| the tour; TAKE THE TOUR; SHOW ME; TOUR · 3 OF 7; ESC ENDS THE TOUR | die Tour; TOUR STARTEN; ZEIGEN; TOUR · 3 VON 7; ESC BEENDET DIE TOUR | ← ZURÜCK, WEITER →; „Esc beendet die Tour“; help brings it back: „die Hilfe holt sie zurück“ |
| What's New; NEW IN 0.4.0 · PRAETORIAN | Neuigkeiten; NEU IN 0.4.0 · PRAETORIAN | release names stay |
| HOW SHTURMAP WORKS; QUEST TYPES; ON THE MAP; legend; WIKI MAP ↗ | SO FUNKTIONIERT SHTURMAP; QUEST-TYPEN; AUF DER MAP; Legende; WIKI-MAP ↗ | help |
| KEYS · WHEN THIS WINDOW HAS FOCUS; story chapters; aren't shown yet | TASTEN · WENN DIESES FENSTER DEN FOKUS HAT; Story-Kapitel; fehlen noch | help and the tour: „Story-Kapitel (…) fehlen noch“ |
| GET IT; after {quest}; from level 10; needed for Kappa | BESORGEN; nach {quest}; ab Level 10; nötig für Kappa | item and quest cards |
| A or B; 13 of 16 kinds; or 5 others | A oder B; 13 von 16 Arten; oder 5 weitere | |
| try again (to the player); again; Shturmap tries again | versuch es noch mal; noch mal; Shturmap versucht es … erneut | the du-voice says „noch mal“, not „erneut“ or „noch einmal“ |
| dev build, developer builds; developer run; folder build; installed; study log | Dev-Build (der), Dev-Builds; Entwickler-Testlauf (der); Ordner-Build; installiert; Studien-Log (das) | the build is what was built („Nur in Dev-Builds“, the version line); a run is a developer's session: snapshots, fake games, the demo (DESIGN.md §8): „Entwickler-Testläufe senden nichts“ |
| deadly area; SNIPER ZONE | tödlicher Bereich; SNIPER-ZONE | |
| map artwork; map render; sheet; 10 m grid | Map-Grafik (in the legend: Grafik); Map-Render; Blatt; 10-m-Raster | „Keine Map-Grafik für {map}: stattdessen ein 10-m-Raster …“; credit line „Raster 10 m“; caps RASTER 10 M |
| settings rows | Screenshots nach dem Lesen löschen; Extract-Liste aus Screenshots lesen; Stash, Gear, Dailies und Weeklies aus Menü-Screenshots lesen, im Raid auch Loot; Absturzberichte; Updates | options: NACH ABSTURZ FRAGEN · IMMER SENDEN · NIE; AUTOMATISCH · NUR BENACHRICHTIGEN · AUS |
| a mark (what you have); gets a mark; takes a mark back; NOT ANY MORE, Not any more | Markierung (die); bekommt eine Markierung; nimmt eine Markierung zurück; NICHT MEHR, Nicht mehr | Shturmap's own mark on a BRING row or a card (DESIGN.md §4 "What you have"); apart from "mark" the objective (markieren) |
| Seen in your stash · {date}; "…, found in raid" and "…, not found in raid" on a mark; {seen} of {needed}; "…, not in your screenshots of {date}" | In deinem Stash gesehen · {date}; „, FiR“ and „, nicht FiR“; {seen} von {needed}; „, nicht in deinen Screenshots vom {date}“ | the mark's fixed wording (SessionTexts Mark*), quoted in What's New; the date in the dative, „vom 12. Okt.“ |
| Given with the quest; given with {quest}; reward from {quest}; {count} as reward from {quest}; {count} more: {source} | Mit der Quest bekommen; mit {quest} bekommen; Belohnung aus {quest}; {count} als Belohnung aus {quest}; Noch {count}: {source} | the log's marks, and the amber line under a part mark |
| menu screenshot; item cells; What you have; From the game's log | Menü-Screenshot (der); Item-Zellen; Was du hast; Aus dem Log des Spiels | the setting's note; What's New's names |
| the desk (the stash desk, PLAN \| DESK); your gear (the GEAR tab); what an item is worth; prices; fetched again | Desk (der), auf dem Desk; Gear (das); was ein Item wert ist; Preise; neu geladen | the menu screenshots' tick and note (2026-10-19): „Stash, Gear, Dailies und Weeklies …“, „auf dem Desk dafür, was jedes Item wert ist (Preise von tarkov.dev, höchstens alle zehn Minuten neu geladen …)“; the desk's view brings its own words: keep them in step |
| operational task (a daily or weekly: the game's Tasks screen, its OPERATIONAL tab); Tasks screen; OPERATIONAL (the heading); As of {time}; {count} ended at {time}; 8 h left, 2 d left; read 3 h ago; ends in 25 min; hand over after; ENDED (a cue's row whose task ran out) | Daily (die), Weekly (die), Dailies und Weeklies; where a text doesn't know which: Daily oder Weekly (one), Dailies und Weeklies (several); Aufgaben-Bildschirm; OPERATIONAL (the game's English tab name, as PROGRESS stays); Stand {time}; endete um {time}; noch 8 h, noch 2 Tage; vor 3 h gelesen; endet in 25 min; danach abgeben; ABGELAUFEN | decided (owner, 2026-10-19): the players' words, as Quest; never „operative Aufgabe“, nor „Aufgabe“ alone, which is the game's word for a quest. Feminine, as players say it after die Quest, die Mission („meine Daily“): „für eine Daily oder Weekly“, „Mach einen Screenshot deiner Dailies und Weeklies für die neue“; plural as players write it, Dailies, Weeklies (Duden's rule for English -y, Hobbys, would give Dailys). „Dailies und Weeklies gelesen: 3.“ The reading needs the English game, so the tab is OPERATIONAL on the player's screen; a task's time left is „noch 8 h“ (the raid clock keeps MIN ÜBRIG); a synopsis kills as every text does („3 Scavs killen · Headshots über 50 m“). **check** the German game's names for the Tasks screen and the tab |
| a trader's TASKS tab (a trader's screen in the game's TRADERS menu, its TASKS tab, where a daily or weekly selected is read; owner, 2026-10-25); never their text or rewards | TASKS-Tab eines Traders (der TASKS-Tab); nie ihre Beschreibung oder Belohnungen | reviewed (senior translator, 2026-10-25): the game's English tab name, as OPERATIONAL (the reading needs the English game); Trader, the players' word, never Händler; der Tab as Windows says it. In the settings note and help: „im TASKS-Tab eines Traders“; the glyph's tip: „des TASKS-Tabs eines Traders“; a task's text (never read) is „Beschreibung“, never „Text“: the note's „die Texte der Dailies und Weeklies“ are the tasks' words, which are kept |
| the game in English (what the screenshot readings need), in brackets: (English game) | (Spiel auf Englisch) | senior translator, 2026-10-25: never „(Englisch)“ alone, which reads as the language of the tab or of the text |
| readings log (one text file per menu screenshot read, in the data folder's `readings`; on screen "a log of the last 50 screenshots read") | Lese-Log (das); „ein Log der letzten 50 gelesenen Screenshots“ | reviewed (senior translator, 2026-10-25): Log, not Protokoll, neuter; on screen described, not named, so the name is for a text that needs one, hyphenated like Studien-Log, with „lesen“ as in „Screenshots nach dem Lesen löschen“; the folder name `readings` is never translated |
| a log message (one the game wrote, as Shturmap reads it); a mark; FORGET ALL MARKS; the plan (what Shturmap planned) | Log-Nachricht (die); Markierung; ALLE MARKIERUNGEN VERGESSEN; die Planung | never „der Plan“ (reads as a Lageplan); „Die Planung bleibt.“ |
| PLAN, DESK (the switch at the rail's head); The desk: … (its tip) | PLANUNG, DESK; Der Desk: … | step B, 2026-10-19; senior translator, 2026-10-19: PLANUNG as Plan's state elsewhere ("Plan, Raid" above; the tour's „Planung: wohin, was mitnehmen“), never PLAN, which beside MAP reads as a drawn plan („der Plan“ above); it fits (each half of the switch 174 DIP of the rail's 351, the words in the title style; owner, 2026-10-19). DESK as the panel drew it; the desk in a sentence as its own row says (der Desk, auf dem Desk) |
| the desk's heads: LOADOUT; TACTICAL RIG; POCKETS; SPECIAL SLOTS; BACKPACK; POUCH; STASH; {title} · IN THE BACKPACK (and the other regions) | LOADOUT; RIG; TASCHEN; SPEZIAL-SLOTS; RUCKSACK; POUCH; STASH; {title} · IM RUCKSACK (IM RIG, IN DEN TASCHEN, IN DEN SPEZIAL-SLOTS, IN DER POUCH, IM STASH, IM LOADOUT) | players' words over the game's: das Rig, die Pouch (as players say it, „ab in die Pouch“), das Loadout. The game's data names a chest rig „Koppeltragesystem“ (items `5448e5284bdc2dcb718b4567`) and a secure container „Tragbarer Behälter“ (`5448bf274bdc2dfc2f8b456a`); SPEZIAL-SLOTS with the hyphen of a compound with an English word. The window's title is a game name, never declined. **check** the German game's gear heads (TASCHEN, SPEZIAL-SLOTS, its rig and secure container): not in the data (senior translator, 2026-10-19) |
| ON YOU {total} · STASH {total} · KEEP n · n · n; Not attached: {name} ×3 on the page; counted in neither; One of 3 items; Point at an item for its card. | AN DIR {total} · STASH {total} · BEHALTEN n · n · n; Nicht zugeordnet: {name} ×3 auf der Seite; in keiner Summe; Eins von 3 Items; Zeig auf ein Item: Hier erscheint seine Karte. | the desk's head line, fold line and rail (step B, 2026-10-19; senior translator, 2026-10-19). AN DIR, not BEI DIR, which also reads „at your place“, the stash it is set against. Prices short as players write them: 902k, 1,01 M (the decimal comma), 63–307k, a no-break space before M, ₽ and % („1,01 M+ ₽“, „↓ −10 %“; "Formats"); the head line at 900×560 is about 310 DIP of the 480 it has; UNCHANGED / UNVERÄNDERT gone with the fold (2026-10-25); the variants that leave out a part no screenshot has shown yet (senior translator, 2026-10-26: ok, the same words): AN DIR {onYou} · BEHALTEN n · n · n; STASH {stash} · BEHALTEN n · n · n; BEHALTEN n · n · n |
| the desk's dashed frame and dated head: NOT IN A SCREENSHOT YET; {head} · {time} (a region not in the last screenshot, "BACKPACK · 23:08", on an earlier day the short date and time) | NOCH IN KEINEM SCREENSHOT; {head} · {time} („RUCKSACK · 23:08“, an einem früheren Tag „19. Okt., 23:08“) | reviewed (senior translator, 2026-10-25): the layout round's step 2; the no-break space after the "·" kept in the German and its en: comment; about 175 of the frame's 188 DIP at 40 DIP a cell, two lines in SPECIAL SLOTS' three cells as the English |
| the in-raid glance's plate: FROM YOUR SCREENSHOT; LOOT {total}; ON YOU {total}; 2 unsure; Your gear · nothing looted on this screen; UNSEARCHED +, SEARCHING +, MORE BELOW +; ALSO · BACKPACK {total}; …, and loot in a raid (the menu screenshots' tick); a body, a container (what is looted) | AUS DEINEM SCREENSHOT; LOOT {total}; AN DIR {total}; 2 unsicher; Dein Gear · auf diesem Screenshot kein Loot; NICHT DURCHSUCHT +, WIRD DURCHSUCHT +, MEHR DARUNTER +; DAZU · RUCKSACK {total}; …, im Raid auch Loot; Body (der), Container (der) | 2026-10-19, the plate's own words; senior translator, 2026-10-19: reviewed, unchanged. Loot as players say it (das Loot, des Loots), AN DIR as the desk's head line; „2 unsicher“ as German status lines count („3 ungelesen“), the adjective the same in both plural forms; durchsuchen for searching a body, as players say it („den Body durchsuchen“) and the game's own verb (its skill `Search` is „Durchsuchen“ in the items and tasks data); WIRD DURCHSUCHT as the update band's WIRD HERUNTERGELADEN, never DURCHSUCHEN alone, which would be the English game's SEARCH link on a part not searched; „ein Container, ein Body“ in the setting's note, „Body oder Container“ in What's New. The region heads are the desk's (RIG, TASCHEN, RUCKSACK); a body's slots stand under the game's kind as read („BODY“, „DRAWER“: the reading needs the English game). Room: LOOT 3,06 M+ ₽ at 48 px is as wide as the English (about 282 DIP), DAZU · RUCKSACK 2,89 M ₽ too (about 108 DIP, one line); at the least cell size, 40 DIP, TASCHEN · WIRD DURCHSUCHT + (about 177 DIP at 11 px) and RUCKSACK · MEHR DARUNTER + (174) are wider than a four-cell column (161 to 165) and wrap, where the English fits (135, 152). **check** the German game's UNSEARCHED and SEARCHING (not in the data) |
| the desk's copy (step C): A CLICK COPIES; the rule: THE LAST LOW, 3% UNDER, 2% OVER; the last low; Flea price to copy; Copies {price}; Nets {net} after the fee; flea net; fee; over the best net; at {best} it nets {bestNet}, at this {net}; Lower: …, Higher: … (the stepper's tips); {price} copied; Nothing copied; can't be listed on the flea | EIN KLICK KOPIERT; LETZTER TIEFSTPREIS, 3 % DARUNTER, 2 % DARÜBER; der letzte Tiefstpreis; Flea-Preis zum Kopieren; Kopiert {price}; Bringt {net} nach Gebühr; Flea netto; Gebühr; über dem besten Netto; bei {best} bringt es {bestNet}, bei diesem Preis {net}; Niedriger: …, Höher: …; {price} kopiert; Nichts kopiert; kann nicht auf dem Flea angeboten werden | senior translator, 2026-10-19: the flea is the Flea here too („Flea ab Level“ on the item card), never Flohmarkt (the game's word); the last low (tarkov.dev's lastLowPrice) is der letzte Tiefstpreis everywhere, also over the day's bar (LETZTER TIEFSTPREIS … · SCHNITT …), never TIEFST alone; a no-break space before % („3 % DARUNTER“). Room: LETZTER TIEFSTPREIS is about 120 DIP in the stepper (92 at least), and EIN KLICK KOPIERT beside it still one line |
| the desk's card: cell; pin, unpin; KEEP; now, next, later; a hideout module; 30 DAYS; 24 H; 7 D, 48 H; HIGH, LOW; TRADERS; CRAFTS AND BARTERS; Prices 4 min old, Prices 2 h old, Prices just in; scaled to 1469/1800 · full; One of 3 items: pick it on the card first | Zelle (die), as Item-Zellen; anheften, lösen (Windows' words: „An Taskleiste anheften“, „Von Taskleiste lösen“); BEHALTEN; jetzt, als Nächstes, später; ein Hideout-Modul; 30 TAGE; 24 H; 7 T, 48 H; HOCH, TIEF; TRADER; CRAFTS UND BARTER; Preise 4 min alt, Preise 2 h alt, Preise gerade geholt; skaliert auf 1469/1800 · voll; Eins von 3 Items: wähl es zuerst auf der Karte | senior translator, 2026-10-19: never Feld for a cell, nor the game's Versteck, Händler, Herstellung, Tausch (the players' words in „The game's words“ above win); units as symbols after a no-break space (min, h, not Min., Std.); FiR on a KEEP line („Private Clinic ×1 · jetzt · FiR“), as in all running text; the rail stays unnamed on screen („Diese Karte hier anheften“) |
| the loadout's slots over an item on the desk's card: EARPIECE, HEADWEAR, FACE COVER, ARMBAND, BODY ARMOR, EYEWEAR, DOGTAG, ON SLING, HOLSTER, ON BACK, SHEATH | HEADSET, KOPFBEDECKUNG, GESICHTSBEDECKUNG, ARMBAND, SCHUTZWESTE, BRILLE, DOGTAG, AM TRAGEGURT, HOLSTER, AUF DEM RÜCKEN, MESSERSCHEIDE | senior translator, 2026-10-19: the game's words where its data has them (items' categories „Kopfbedeckung“, „Gesichtsbedeckung“; items „Armband (Weiß)“, „BNTI Gzhel-K Schutzweste“, „Taktische Brille“, „BEAR PMC Dogtag“), the players' HEADSET (the data: „… Kopfhörer“, category „Headsets“; Ohrhörer are earbuds); MESSERSCHEIDE, as a bare SCHEIDE reads two ways. The reading needs the English game, so the player's screen says EARPIECE … SHEATH. **check** the German game's gear screen, AM TRAGEGURT and AUF DEM RÜCKEN above all (not in the data); since 2026-10-25 also the names over LOADOUT's slot boxes (80 DIP at 40 DIP a cell, the weapons' 200): GESICHTSBEDECKUNG drawn at 8.5 px without letter spacing, KOPFBEDECKUNG and MESSERSCHEIDE about 74 of 80 DIP at 9 px (senior translator, 2026-10-25) |
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
| apply (an update); the update band's stages: DOWNLOAD AVAILABLE; DOWNLOADING; UPDATE READY; UPDATE DIDN'T APPLY | installieren; UPDATE VERFÜGBAR; WIRD HERUNTERGELADEN; UPDATE BEREIT; UPDATE NICHT INSTALLIERT | decided (2026-10-15): VERFÜGBAR as German update screens say it, with HERUNTERLADEN under it, not „DOWNLOAD VERFÜGBAR“ |
| App and data; Privacy; Licences | App und Daten; Datenschutz; Lizenzen | |
| crash; crash report; error report | Absturz; Absturzbericht; Fehlerbericht | |
| report (noun); to report; Report a problem or idea; Reporting isn't set up in this build | Bericht; melden; Problem oder Idee melden; Melden ist in diesem Build nicht eingerichtet | PROBLEM · IDEE; not „Berichte sind … nicht eingerichtet“; whoever gave you Shturmap: die Person, von der du Shturmap hast |
| diagnostics; Include diagnostics | Diagnosedaten; Diagnosedaten anhängen | COPY DIAGNOSTICS: DIAGNOSEDATEN KOPIEREN |
| folder; log folder; choose a folder | Ordner; Log-Ordner; Ordner wählen | Windows' picker is titled „Ordner auswählen“ |
| this PC; Documents; user folder | dieser PC; Dokumente; Benutzerordner | Explorer's names; the path stays `Documents` |
| screenshot; screenshot key; internet address | Screenshot (der); Screenshot-Taste; IP-Adresse | einen Screenshot machen; with a key: „Screenshot mit {keys}“ (Keys) |
| Zoom in; Zoom out; Show the whole map; status bar | Vergrößern; Verkleinern; Ganze Map zeigen; Statusleiste | also in running text: „Vergrößern trennt sie“, „beim Vergrößern“; not „reinzoomen“ |
| pan (the map); as you pan or zoom | verschieben, by dragging: ziehen; beim Ziehen oder Zoomen | help: „Ziehen zum Verschieben“; zoomen only where both ways are meant (What's New 0.7.0: „bleiben beim Ziehen oder Zoomen ruhig“) |
| email; (optional); (Required) | E-Mail; (optional); (Pflichtfeld) | |
| Language: Automatic · English · Deutsch | Sprache: Automatisch · English · Deutsch | each language in its own name |
| Windows Settings → Apps | Windows-Einstellungen → Apps → Installierte Apps | Windows 11's German Settings texts (checked 2026-10-11); „Windows-“ because a bare „Einstellungen“ is Shturmap's own |
| Windows Settings → Time & language → Language | Windows-Einstellungen → Zeit und Sprache → Sprache und Region | Windows 11 (checked 2026-10-11): „und“, never „&“; Windows' own help names „Zeit und Sprache“, then „Sprache und Region“. Windows 10: Sprache (**check**) |
| text recognition (language feature) | Texterkennung; „Optische Zeichenerkennung“ | Shturmap's sentences say Texterkennung; Windows 11 names the feature „Optische Zeichenerkennung“ among a language's features in „Sprachoptionen“ (checked 2026-10-11) |
| PaddleOCR (the second text recognition that comes with the app, for an item's name or a task's time left Windows' can't read; owner, 2026-10-26); comes with Shturmap | PaddleOCR (a name, never changed); die Texterkennung von PaddleOCR; die Shturmap mitbringt | reviewed (senior translator, 2026-10-26): „die Shturmap mitbringt“, not „die mit Shturmap kommt“ (a calque of „comes with“); in the settings note, inside the brackets after Windows' recognition: „(und der von PaddleOCR, die Shturmap mitbringt, wo die von Windows einen Item-Namen oder die Restzeit einer Daily oder Weekly gar nicht liest)“ |
| animation effects | Animationseffekte | Einstellungen → Barrierefreiheit → Visuelle Effekte → Animationseffekte (Windows 11, checked 2026-10-11) |
| text size (Windows' setting, and the settings row on top of it); Settings › Accessibility › Text size; NORMAL · LARGER · LARGEST | Textgröße; Windows-Einstellungen › Barrierefreiheit › Textgröße; NORMAL · GRÖSSER · AM GRÖSSTEN | Windows 11 names the page „Textgröße“ under „Barrierefreiheit“ (checked 2026-10-19), and the row takes its word; „Windows-“ because the tooltip stands in Shturmap's own EINSTELLUNGEN; AM GRÖSSTEN a superlative as LARGEST is, and it fits beside the other two in the 480 px panel |

## Keys

As German keyboards label them. Shturmap names the screenshot key from the game's settings (`GameKey.Name`, each word
a text in GameTexts: "PrtSc", "Home"); German needs these labels. The game's own German key names aren't in the data:
**check** in its settings (docs/NEXT.md). A sentence that names the screenshot key says „Screenshot mit {keys}“, not
„Taste {keys} drücken“: that takes every name, two keys („mit Druck oder Pos1“) and a combination („mit Strg+Druck“)
as they are, and never says „Druck drücken“ or „Taste Leertaste“ („Noch keine Position · Screenshot mit Druck“, „Mach
im Raid einen Screenshot mit Druck: …“, „DANN SCREENSHOT MIT DRUCK“). Where the game names no key, the stand-in is
„deiner Screenshot-Taste“. A fixed letter keeps „Taste“: DEINE NEUE POSITION · TASTE F.

| English | German | Note |
| --- | --- | --- |
| PrtSc | Druck | many keyboards: Druck S-Abf |
| Home; End; Delete; Insert | Pos1; Ende; Entf; Einfg | |
| PgUp; PgDn | Bild↑; Bild↓ | |
| Ctrl; Right Ctrl | Strg; Strg rechts | Ctrl + , is Strg + , |
| Shift; Right Shift | Umschalt; Umschalt rechts | keycap often only ⇧; Shift + F is Umschalt + F |
| Alt; Alt Gr; Esc; Tab; F1; Num 5 (keypad) | Alt; Alt Gr; Esc; Tab; F1; Num 5 | unchanged |
| Enter (Return); Num Enter; Backspace; Space | Enter; Num Enter; Rücktaste; Leertaste | Enter is the players' word and often the keycap (↵ on the main keys), not Windows' „Eingabe“; Backspace's keycap ⟵ |
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
| 25 Sep; 25 Sep 16:40 | 25. Sept.; 25. Sept., 16:40 | `UiLanguage.DayMonth`, `DayMonthTime`: .NET's de-DE with `d. MMM` (Jan., März, Mai, Juni, Juli, Sept., Okt.); `d MMM` gives „25 Sept.“, wrong in German |
| 25/09; 16:40 | 25.09.; 16:40 | 24 h; no „Uhr“ in labels |
| 12 min; 45 s; 6 h | 12 min; 45 s; 6 h | unit symbols, after a no-break space (a line never breaks between 1 and min; review of 2026-10-10); caps 12 MIN |
| 69 m; 2.5 km; 38 % | 69 m; 2,5 km; 38 % | a no-break space before the unit and before % (a line never breaks between 75 and %) |
| after a colon | lower case; a capital only where a whole sentence follows | „Keine Map-Grafik für {map}: stattdessen ein 10-m-Raster …“; „…: Der Server antwortete mit {status}.“ |
| N, NE, E, SE, S, SW, W, NW | N, NO, O, SO, S, SW, W, NW | map-up is north |
| "…" and '…' | „…“ and ‚…‘ | |
| apostrophe; e.g. | ’; z. B. | genitive without an apostrophe: Prapors; z. B. with a no-break space |
| dash; range | „ – “; 21:00–05:00 | en dash: with spaces between words, none in a range |
| CHOOSE… | WÄHLEN… | the ellipsis close to the word, as in Windows' menus |
| ·, ↗, ↓, → | unchanged | |
| upper case | ß → SS; Ä, Ö, Ü stay | `UiLanguage.Upper` (`Caps.Of`) writes ß as SS, also in a data name; .NET's `ToUpper` keeps it („STRAßE“) |
| plural | one, other | `{n, plural, one {# Extract} other {# Extracts}}` |
