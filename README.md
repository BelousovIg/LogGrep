# LogGrep

A WPF desktop app that slices a huge World of Warcraft combat log into per-pull files small
enough to hand to an AI chat. Built for retail (Midnight, combat log version 22), but the parser
also handles the older 11.x layout.

## Build & run

```
dotnet build
dotnet run --project src/LogGrep
```

Requires the .NET 8 desktop runtime. The window draws on the CPU by default, because some display
drivers hand WPF a hardware surface that never paints and leave a blank white window; pass
`--hardware` to use the GPU instead. You can also pass a log on the command line, or drop a
`.txt` onto the window:

```
LogGrep.exe "C:\...\Logs\WoWCombatLog-091526_120136.txt"
```

## What it does

* **Open log** streams the file once and fills the table as it goes. An 800 MB log takes a few
  seconds; memory use stays flat because only byte offsets are kept, never the text.
* Each row is an **encounter**: a raid boss with all of its attempts grouped together, or a single
  keystone run. The row shows the difficulty, the number of pulls, whether any pull was a kill,
  and the smallest/largest group size seen across the attempts.
* The **toggle** opens the list of pulls. It is enabled for raid bosses and for anything with more
  than one attempt; a keystone run is a single pull, so its toggle stays off.
* Each pull lists its **roster** as "Name - Realm". The text is trimmed to the column width; hover
  it for the full list, or use the copy button next to it to put the whole roster on the clipboard.
* The encounter **checkbox is three-state**: all pulls selected, none, or some.
* **Export** writes the selected pulls, either into one file (`as single file`) or one file per
  pull into a folder you pick. Per-pull names are
  `<source log>_<Encounter>_<yyyy-MM-dd>_<HH-mm-ss>.txt`.

Exported files are byte-for-byte copies of the original lines and open like a log the game wrote
itself: the `COMBAT_LOG_VERSION` header, the `ZONE_CHANGE` / `MAP_CHANGE` that were in effect, then
the fight from `ENCOUNTER_START` to `ENCOUNTER_END` (or `CHALLENGE_MODE_START`/`_END`).

## Parsing notes

* Segments come from `ENCOUNTER_START`/`ENCOUNTER_END`; a keystone run is bracketed by
  `CHALLENGE_MODE_START`/`CHALLENGE_MODE_END` and boss pulls inside it belong to the run.
  A log that was cut off mid-fight still yields that pull, marked `wiped`.
* Duration comes from the fight time the game reports on the END line, falling back to the
  timestamp difference.
* The roster is collected from every unit event of the fight, not just damage and healing, so a
  player who spent the whole pull dead is still listed. Only group members count (raid, party or
  self affiliation). Realm names are written without spaces in the log, so "TarrenMill" is shown
  as "Tarren Mill" and the region suffix is dropped.
* Player count comes from `COMBATANT_INFO` when advanced logging is on, otherwise from the group
  size on `ENCOUNTER_START`.
* DPS/HPS cover sources that are player-controlled and in your group (players, pets, guardians).
  Healing is effective healing - overhealing is subtracted, absorb shields (`SPELL_ABSORBED`) are
  not counted. Support events (`*_SUPPORT`) are skipped so augmentation damage is not counted twice.
* The block of advanced unit-info fields grew from 17 to 19 entries in log version 22, so the
  parser locates its end by the position fields rather than a fixed offset.

## Layout

```
src/LogGrep/
  Parsing/     streaming scanner, allocation-free field splitter, timestamp parsing
  Models/      pull records, byte ranges, difficulty tables
  Export/      raw byte-range copier
  ViewModels/  encounter/pull tree, tri-state selection, commands
  Themes/      dark theme
  Interop/     dark title bar (DWM)
```
