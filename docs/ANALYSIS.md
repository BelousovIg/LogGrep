# The report

What the app hands back once it has read a night, and how that reaches a person.

`ROADMAP.md` is the record of what was built and what each rule cost to get right. This is the
design the built rules are supposed to add up to: the numbers, what they are allowed to mean, and
the shape they are shown in. It is written to be cut into milestones.

The tree - encounters, attempts, players - stays, and stays navigation. The product is the report,
and the report is reached from the row it is about.

## There is no maximum, and asking for one is the first trap

The obvious ask is a percentage of the best possible: "you did 85% of the damage that was there".
The log cannot support it. The maximum depends on the build, the gear, the buffs, the phase, and
how long the target stood still, and none of that is knowable from the file. A number invented for
that slot would be the exact thing this app refuses to do everywhere else.

What a person actually wants is not the maximum. It is a **ceiling somebody has really shown**. So
the denominator is always observed, and the screen always says whose it is:

| Band | Ceiling | Available when |
| --- | --- | --- |
| Against yourself | your best on this encounter | 5 attempts of your own |
| Against your spec | the best anyone of your spec managed in these logs | 3 attempts from a peer |
| Against the group | the group's own best attempt | always, and it is what the cumulative uses |

100% then means "your best so far", not "perfect". `Yardstick` already works this way and already
carries the sentence naming whose ceiling it is; nothing on screen may show a percentage without it.

The consequence has to be accepted rather than smoothed over: a scale against yourself saturates.
Somebody who never improves sits at 100% forever. That is why the two bands are shown **side by
side and never averaged together** - "against yourself 100%, against your spec 61%" is the whole
conversation, and folding them into one number destroys it.

## The unit: health pools

Damage and healing are measured in damage. Mechanics, deaths and missed interrupts are measured in
nothing at all, which is why "how well did they play the mechanics" reads as an opinion rather than
a number. Everything a mistake costs is therefore converted into **the player's own health pools**.

Avoidable damage is 0.7 of a pool. A missed interrupt is however many pools that cast then put into
the raid. A death is one pool plus the time the raid lost. Why this unit and not raw damage:

- It is in the file already. `maxHP` comes off the advanced block, and `BiggestShare` and
  `SuddenShare` are built on it.
- It rescales itself. Gear and patches move the numbers and the unit follows, so thresholds do not
  need recalibrating every season.
- A person feels it. "You ate three of your own health bars of avoidable damage" lands; "you took
  4.2M" does not.

## Three layers, and a different rule for each

Nothing on any screen exists outside these three. This is the contract that keeps the rest honest.

**Facts** are dry statistics, shown without interpretation and without justification: damage per
second, healing per second, damage taken, deaths, uptime, casts. These need no defending and get no
commentary.

**Indexes** are 0-100%, always derived, always expandable. There are two kinds and they must not be
computed the same way:

- *Ratio to a ceiling*, for output - damage and healing. Fact over ceiling, from the table above.
- *Subtraction of losses*, for execution - mechanics, deaths, interrupts, cooldowns, the opening.
  Start at 100% and take away what went wrong, in health pools. **Every percentage point has a
  finding behind it.**

**Findings** are the conclusions, and they already exist: thirteen detectors produce them and each
one already carries a cost. The index is not a separate thing that needs its own arithmetic - it is
the findings, summed and sorted. That is what makes every number on screen expandable down to the
event in the log that caused it, and it is why "figures, facts, conclusions" is not three widgets
but three zoom levels of one.

Every metric shows five things, always in the same order:

```
Mechanics  38%                              <- the number
7 hits taken, 3.1 health pools              <- the facts
against your own 13 attempts at this boss   <- what it was measured against
most of it: Hollowing Strikes, four times   <- the conclusion
[expand: 7 findings]                        <- the evidence
```

And a sixth rule that is not a field: **when the sample is too thin, the number is a dash** and the
line says why - "attempt shorter than a minute, nothing to judge". A dash is more honest than 50%,
and silence over an invented number is the discipline the whole app is built on.

## Four axes for everybody, weighted by role

Roles do not get their own metrics - if they did, a raid could not be read across. They get their
own weights.

| Axis | Damage | Tank | Healer |
| --- | --- | --- | --- |
| Output | x3 | x1 | x2 |
| Survival - avoidable damage, deaths | x2 | x2 | x2 |
| Mechanics - interrupts, stacks, spreads, the opening | x3 | x2 | x2 |
| Duty of the role | x1 | x4 | x3 |

Duty is the only axis whose content differs, and what the log can actually say about each:

- **Tank**: how much of the fight the boss spent looking at somebody who is not a tank. This is the
  opening rule generalised to the whole fight - whoever takes the enemy's blows is holding its
  attention - and the per-player first-hit times the scanner now keeps are the same measurement
  read over a longer window. Plus active mitigation uptime, which is already parsed as self-buffs.
- **Healer**: **how many deaths could have been pulled back**. The healing ceiling is already
  measured - the most healing this group ever landed on one person inside five seconds - so for
  every death the log can answer whether the incoming rate was above anything this group has ever
  sustained. Above it, the death is not the healer's question. Below it, it is. This is a far
  better healer metric than healing per second, which rewards overhealing and punishes a quiet
  fight.
- **Damage**: uptime - the share of the fight spent doing nothing beyond the global cooldown, which
  is already counted as dead seconds.

## The raid score is subtraction, not an average

Averaging the players hides the one thing the app is opened for: one person loses the attempt and
the mean stays at 84%.

So the pull gets its own score, computed the same way an execution index is - start whole, subtract
what was lost - and then **attributed**, with a line for what was collective. Individual against
collective is already a distinction the app draws for deaths.

```
Nek'zali the Soulcoiler - attempt 7 of 13 - wipe at 0:13 - Mythic
-----------------------------------------------------------------
Raid  41%      boss at 78%, 4.2 pools lost of 5 the roster could afford

Output     63%   784K/s against 1.24M, the group's best attempt
Healing     -    attempt shorter than a minute, nothing to judge
Mechanics  38%   7 hits taken that were avoidable, 3.1 pools
Tank       12%   the boss spent 9.4s of 13 looking at somebody else

Where it went:
  Epshteiner    1.8 pools   opened the pull, died at 0:13
  collective    1.1 pools   Hollowing Strikes, four people in it
  Deathelfio    0.7 pools   did not interrupt Soul Drain
```

## Where each screen lives

Seven things a raid leader and a player want to see do not form a tree. They form a grid of two
axes - **who it is about** by **how much of the night it covers**:

|  | One attempt | One encounter, over the night | Every log |
| --- | --- | --- | --- |
| **The raid** | the attempt, pulled apart | progress against this boss | the state of the roster |
| **One player** | them in this attempt | them against this boss | their own record |

Navigation is one step along one axis: clicking a name in the raid table walks down the column,
clicking "over the night" walks along the row. The breadcrumb reads `Nek'zali > attempt 7 >
Deathelfio`. The left-hand tree is how a log is entered, and nothing more.

**All six are the same component**: a score, four axes, the findings under them, the facts under
those. Only the range summed over changes. It is cheaper to build and, more to the point, a person
learns to read the screen once.

## The mistakes column cannot be widened into working

Findings currently live in a column of text, one cell per player. Making that cell bigger fixes
nothing, because what fails is not the width. A sentence in a cell loses five things at once:

- **Size.** "Stood in the mechanic" and "died and lost the attempt" look identical.
- **Repetition.** Four hits and one hit take the same line.
- **Time.** When it happened is gone, so it cannot be tied to what the boss was doing.
- **Comparability.** No eye scans twenty sentences and comes away knowing whose night was worst.
- **Room to grow.** Every detector added writes another sentence, so the cell has to grow with the
  number of rules - which is exactly the thing that must not happen.

One root: **a cell holds a sentence, and sentences do not compare at a glance**. The replacement has
to put a *shape* in the cell - something with a position, a size and a colour, which compares down
the table without being read.

## The fight as a score sheet

The mistakes column becomes **the player's lane on a shared time axis**, and above the table stands
**the enemy's lane** carrying what it cast.

```
                     Out Srv Mch Dty | 0:00 ---------- attempt 7, 3:21 ---------- 3:21 |
 BOSS  Nek'zali                      |  ....H.....S...S.......H.......S...S.......H....|
 ====================================|########  Hollowing Strikes 1:47 - four in it  ##|
 Kondratushka   tank   -   72  91  64|  -------o--------------------------------o-----|
 Ruscum         tank   -   68  88  71|  -------------------------o--------------------|
 Ivarpriest     heal   -   90  95  77|  ----------------------------------------------|
 Deathelfio     dps   71  44  38  82 |  --o--------O-----------o------------x---------|
 Mokhgar        dps   88  91  74  90 |  ------------------o---------------------------|
 Epshteiner     dps   64  81  90  88 |  ----------------------------------------------|
```

What reading this is like. Ivarpriest's lane is empty, and that is visible instantly, without
reading - silence became something you can see rather than the absence of text. Deathelfio has four
marks and a death, so his attempt is obviously the heavy one, again without reading. And the marks
**line up vertically**: four diamonds on the same second at 1:47 are not four people's four
mistakes, they are one thing that happened to the raid.

Above it all is what the enemy was doing, so the cause sits directly over the consequence. That is
what makes the picture explain itself: a person does not read "you took avoidable damage", they see
that Hollowing Strikes was going out at that moment and that two people beside them got out of it.

## What a mark carries

Five channels, none of them redundant:

| Channel | Carries |
| --- | --- |
| Position across | when it happened |
| Size | what it cost, in health pools |
| Shape | the category - mechanic, idle, missed interrupt, death |
| Colour | the same category, for speed; shape repeats it for anyone who cannot use colour |
| A band down the whole table | a collective event - it caught more than half the group |

The last one is the important decision. **A personal mistake is a mark on one lane; a collective one
is a band across every lane.** Two different conversations get two different visual classes, and a
raid leader stops taking somebody apart for what was everybody's.

## Four layers of disclosure

The sentence does not disappear. It stops living in the cell.

1. **The cell** - shape only. No text, pure scanning.
2. **Hover on a mark** - one line: `Hollowing Strikes - 1:47 - 0.6 pools`.
3. **Click** - the finding card: what, what it cost, how we know, what to do, and "show it in the
   log".
4. **Expand the row** - the lane opens to full width, marks take labels, and the player's health
   line is drawn along it, so how they arrived at the death is visible. This is what the window
   before a death was collected for in the first place.

```
 Deathelfio  dps  expanded
 hp   ~~~~~\___/~~~~~~~\______/~~~~\_____________
           o           O            o           x
           |           |            |           +- died 2:58 - Soul Drain, three ticks
           |           |            +- did not interrupt Soul Drain - 2:14 - 0.9 pools
           |           +- idle 11s - 1:12
           +- Hollowing Strikes - 0:22 - 0.6 pools
```

The health line is the evidence: the dips are visible, and it is visible that the death was not a
sudden hit but a long stretch spent low. Explaining that in prose takes a paragraph nobody reads.

## One sentence stays

Strip the text out entirely and the table becomes handsome and mute. So one text element survives,
and it is not a list - it is **the single most expensive thing that happened**, which fits a narrow
column for good:

```
 Deathelfio   dps   71 44 38 82   x died to Soul Drain, 2.1 pools   | --o---O--o--x |
```

A column holding a list does not scale. A column holding the worst one sentence scales forever: the
rules can grow to forty and the line stays one line, because it shows a maximum rather than an
enumeration.

The full ranked list is not lost, it moves - **below the table, one per attempt, sorted by cost**.
A raid leader reads the top five and is done, which is the actual behaviour; reading twenty cells
is not.

## The same control at three scales

The axis is always "when". Only the unit changes, and one control serves the whole grid of screens:

| Range | Unit of the axis | What it shows |
| --- | --- | --- |
| One attempt | seconds of the fight | the score sheet above |
| One encounter over the night | attempt number | which mistake repeats from pull to pull |
| Every log | evenings | whether the lane is thinning out |

```
 Encounter: Nek'zali - 13 attempts
                                 1  2  3  4  5  6  7  8  9 10 11 12 13
 Deathelfio  no interrupt, S.D.  o  o  o  .  o  o  o  .  o  .  o  .  .   9 of 13, thinning
 Deathelfio  stands in H.S.      o  .  o  o  .  .  o  o  .  .  o  .  o   6 of 13, unchanged
 Mokhgar     opens the pull      .  .  .  .  .  .  .  .  .  .  o  o  .   2 of 13, new
```

A systematic mistake stops being a statistical conclusion and becomes **a pattern you look at**.
"Thinning" is read off the gaps before the label is read at all, and the label is only there to
confirm it.

## What was rejected, and why

**A stacked loss bar** - segments by category, width by cost. It compares down the table beautifully
and destroys time, so a mistake can no longer be tied to what the enemy was doing and collective
cannot be told from individual. It is the same one-dimensional cell, in colour.

**Chips with counters** (`x4  skull1`). Compact, but still a list typeset as icons: it grows with
the number of rules and carries neither size nor moment.

**A findings panel instead of the column.** The row's context is lost - who this is against everyone
else stops being visible. The panel is needed, but under the table, not in place of the lane.

## The awkward cases, decided in advance

- **A ten-minute fight.** Marks collapse into each other. Nearby marks merge into one with a count
  and come apart under a zoom or a brush on the axis.
- **A thirteen-second wipe.** The lane is nearly empty and that is correct - a short attempt has
  little to say, which is the same reason its indexes show a dash.
- **An empty lane.** It means nothing was found, and it has to look deliberate rather than unloaded -
  a drawn baseline, not blank space.
- **Colour.** Shape carries the category as well, so nothing depends on hue alone.
- **The temptation.** The score sheet invites putting everything on screen at once. The rule is the
  opposite: **a collapsed row gets one sentence and one lane**, and all depth is a click away. Let
  mark labels onto the first screen and it is the unreadable cell again, better dressed.

## Order of work

Everything here rests on the unit and the index, and neither needs new analysis - thirteen detectors
already compute costs. They need a common denominator and a normalisation.

1. Health pools as the unit, and the four axes built from the findings that already exist.
2. The attempt screen - the most valuable one, and the one that tests the whole model against the
   real log.
3. The lane, first with bare marks on a shared axis and no health line and no enemy lane. The
   vertical alignment - the main prize - arrives at this step.
4. The enemy lane above it. This is the step that makes the picture explain itself.
5. The health line inside the expanded row.
6. The player's own record, with the repeat pattern.
7. The cumulative view over a night.

## What the real log changed

The first four axes and the lane were built against the generated log and then pointed at a real
evening - twenty players, thirteen attempts, the longest of them ten minutes. Four things were
wrong, and none of them was visible in a scenario.

**The health pool was the boss's.** The advanced block on a combat log line describes the unit that
*caused* the event - its second field is the owner GUID, which is how a pet's damage finds its
player. Reading it as the victim's put seven hundred million on every damage dealer in the raid, and
every score built on a pool quietly became nonsense: people scored five per cent for surviving a
fight they walked out of. The pool is now credited to whoever the block says it is about, which is
right in both directions, and read off casts as well so a healer who dodged everything still gets
measured. The generated log writes that block as the target's, which is exactly why no scenario ever
caught this.

**A death does not belong in a share of damage.** Survival began as one minus the avoidable damage
and the deaths over everything that landed. A ten-minute fight puts forty health pools through
somebody, so a death moved the score three points and the whole raid scored ninety-seven - a formula
saturating, not a measurement. Survival is now purely the share of what hit you that was yours to
avoid. The death is named in the same sentence, counted in the pools, and drawn on the lane; three
places is enough without a percentage pretending to carry it too.

**The group's mechanics need the group's denominator.** Twenty people's hits over one group's
castings scored a clean attempt at seven per cent. The chances are per person: a mechanic that goes
out eleven times in a raid of twenty offered two hundred and twenty chances to be caught.

**A boss comes with adds.** Counting every enemy's swings put two thirds of the melee on people who
were never meant to hold anything and read as though the tanks had lost the boss for most of the
fight. Only the thing the fight is named after is counted now.

Two things are still open, and are written down here rather than quietly shipped. The tank's number
is **28% on that attempt even after the fix**, which is either a real reading of a loose boss or a
measure that still needs narrowing - it has not been confirmed either way. And the walk back to the
last moment somebody was whole still reads the attacker's health, because it was measured and tuned
against the real log as it stands; correcting it is its own piece of work with its own measurement.

## What would make this wrong

**A single overall rating per player.** The four axes stay separate, always. One number turns
immediately into a leaderboard, and a leaderboard drawn from a combat log is a way to set a raid
against itself rather than to make it better.

**Cost shown as a grade.** "This cost the raid a death" starts a conversation about the fight;
"you scored 38 out of 100" starts one about the methodology. The index exists for sorting and
navigation; the sentence is what a person is given.

**A percentage with no band named.** Any number whose ceiling is not stated on screen is the
invented maximum coming back in through a side door.
