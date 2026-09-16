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
  it for the full list.
* Each pull has a **toggle** of its own that opens a table of everyone in the group: class, spec,
  DPS, HPS, damage taken per second, when they died as `m:ss` (`-:--` for a survivor, several
  deaths comma separated), and what had been hitting them over the last 10 seconds before each
  death. With more than one death the causes are bracketed per death; hover for the full list.
  The class is painted in Blizzard's class colour, the palette the game and the log sites use.
* Every column header **sorts** its table, and the sort holds for that whole level: ordering the
  players of one pull orders them the same way in the others. Numbers, durations, dates and death
  times sort on their values rather than on the text in the cell, so a 17 second pull lands below
  a 10 minute one instead of next to it. Clicking a header again reverses it; until one is clicked
  the rows keep the order the log gave them.
* A blank cell sinks to the bottom whichever way the arrow points, except in the death column,
  where a survivor counts as having outlasted everyone rather than as a blank. Reversed, that
  column lists everyone still standing first, by name, and then the dead from the last one
  backwards.
* Columns are **resized** by dragging the right edge of a header. A width belongs to the level, not
  to one table, so every pull and every roster stays lined up with the header above it.
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
* Class and spec come from the specialization ID in `COMBATANT_INFO`, read as the field just before
  the talent array: anchoring on the array keeps it right across log versions, which have added
  stats to that block more than once.
  The ID table follows <https://warcraft.wiki.gg/wiki/SpecializationID>. An ID it does not know is
  shown as `Spec <id>` rather than left blank, so a specialization added by a patch is visible
  instead of silently turning into a dash.
* Per-player damage and healing count pets and guardians towards their owner, whose GUID only the
  advanced parameter block carries.
* Deaths come from `UNIT_DIED` and are timed against the start of the pull; the hits behind each
  one are read off a 10 second rolling window of damage the player took, cleared after a death so
  a later one is not blamed on the previous.

## Layout

```
src/LogGrep/
  Parsing/     streaming scanner, allocation-free field splitter, timestamp parsing
  Models/      pull records, byte ranges, difficulty and specialization tables, per-player stats
  Export/      raw byte-range copier
  ViewModels/  encounter/pull/player tree, sort state, shared column widths, commands
  Controls/    sortable column header
  Themes/      dark theme
  Interop/     dark title bar (DWM)
```
