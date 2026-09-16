# Where this goes

LogGrep started as a knife: cut a huge combat log into pulls a chat can read. The mechanics
analysis changed what it is. It now makes a claim about a fight that nobody asked it to make, and
it does so without being told a single thing about any boss.

This is about turning that into the point of the app.

## What the neighbours do, and where each stops

Three tools own this space. None of them does what is proposed here, and the reason is the same in
all three cases.

**Archon.gg** aggregates the logs of the top percentile and shows the result as percentages: this
talent build is run by 67% of them, this stat priority, this opener, these cooldowns at these
moments. Its companion app puts that on an overlay and, after a pull, shows damage and healing
breakdowns, deaths, raid cooldowns and mechanical breakdowns. It answers *what does good look like
right now*. It does not look at you. (Archon's own site refuses automated requests, so this is
drawn from its published articles and third-party write-ups rather than read first-hand.)

**WoWAnalyzer** does look at you. Per-spec modules check your rotation, flag late cooldowns,
dropped buffs, wasted global cooldowns, and rank suggestions by impact. It is the closest thing to
what we want. Its weakness is structural: every spec is a hand-written module maintained by a
volunteer, so coverage is uneven, and a spec whose maintainer moved on gives advice that is quietly
wrong after a patch.

**Wipefest** does the raid half: mechanics defined per encounter, deaths grouped by the thing that
caused them, who failed what. Same structural weakness one level up - the rules are written per
boss, so every tier is a re-write, and a boss nobody wrote up is invisible.

The shared pattern: **correctness is written down by a person, and written-down correctness rots.**

## The bet

We already broke that pattern once. The app worked out that Possession Barrage belongs to a tank
from nothing but how the fight went: 69 of 71 applications hit a tank, tanks being a tenth of the
group. No boss knowledge, no table to maintain, and the result survives a patch because it is
recomputed from whatever log you hand it.

The bet is that most of what a coach would tell you is recoverable the same way, from two baselines
that cost nothing and never go stale:

- **The group as the baseline.** If eighteen of twenty people took no damage from a spell and two
  did, the two stood in it. If a debuff lands on a tank nineteen times out of twenty, the twentieth
  is a mistake. This is the statistic already built, pointed at different events.
- **Your own best attempt as the baseline.** On the pull you are proud of you kept a buff up 92% of
  the time and left 4 seconds of global cooldowns unused. On this one it was 61% and 31 seconds.
  Nobody has to know what your class does to see that you played worse, and *you* know what the
  buff is.

Neither needs a corpus, a subscription, or a person keeping a table current.

## What it cannot reach, honestly

Self-calibration has a hard edge and the plan should not pretend otherwise.

- **It cannot tell you the optimal thing nobody did.** If no one in your raid ever uses a cooldown
  well, no baseline says so. External reference data is the only fix, and it carries its own terms.
- **It cannot name the reason.** It can say a spell hit you that hits almost nobody. It cannot say
  "because you stood in the wrong third of the room". The log does carry positions, so some of this
  is recoverable later, but not cheaply.
- **A run of attempts is the price of admission, and it is paid.** Measured on a real log:
  thirteen attempts read together produce 20 findings; the same attempts read one at a time produce
  none in eight of them, because an attempt where a mistake repeated makes the mistake the majority
  of its own sample. The app therefore refuses to draw a rule from fewer than ten attempts. On that
  same log the floor drops three of the five rules - the ones resting on two and four attempts - and
  20 of the 24 findings survive. A short evening gets nothing, and that is the correct answer for a
  short evening.
- **Build advice needs a corpus.** The log carries the whole talent tree in COMBATANT_INFO, so what
  you ran is known exactly. Whether it was a good choice is not answerable from one raid night.

## What a finding has to carry

Today a finding says what and when. A coaching tool has to answer four questions, and the fourth is
the one that makes it worth opening.

| | |
|---|---|
| **What** | `2:51 Possession Barrage` |
| **Why it counts** | `went to a healer; 69 of 71 hit a tank, over 13 attempts` |
| **What it cost** | `1.2M of the 1.9M that killed you four seconds later` |
| **What to do** | `this one follows the tank; if it is on you, it was swapped or you were closest` |

Cost is what makes a list of forty findings usable, because it is what sorts them. A dropped buff
worth two percent and a death worth a wipe must not sit next to each other as equals.

The fourth line is the only one a person has to write, and it is written **per rule** - a dozen
sentences - not per boss or per spec, which is the table that grows with the game.

## What the app hands back

Two audiences, one engine.

**A player, after the night.** One page, their own fight, ranked by what it cost:

```
Sunwell - Priest, Holy - 13 attempts on Nek'zali the Soulcoiler

  Mechanics
    x2  took a tank mechanic          2:51 and 9:35, ~2.4M taken, one death
        Possession Barrage went to you; 69 of 71 applications hit a tank
    x5  stood in Creeping Rot         ~900k taken
        18 of 20 took none of it on the same attempts

  Execution
        31 seconds of nothing         pull 9, against 4 seconds on your best attempt
        Flash Concentration at 61%    92% on your best attempt, pull 3

  Nothing found: interrupts, defensives, talents
```

That last line matters as much as the rest. A report that only lists faults teaches nobody where
they are already fine, and a silent category is indistinguishable from one nobody implemented.

**A raid leader, between pulls.** The same findings the other way up: per attempt, who cost the
most, and whether a mechanic is failed by one person repeatedly or by everyone once. Those are
different problems, and the `2/3` column already hints at the distinction.

## The milestones

Each ends with something usable. None is a rewrite of what exists.

### 1. A finding worth reading

Give the finding its four fields: category, evidence, estimated cost, written fix. Turn
`MechanicAnalyzer` into a rule engine with pluggable detectors rather than one function, because
everything after this adds detectors. Replace the findings panel with the per-player report.

Ends with: the same mechanics findings, ranked by what they cost, each with a sentence of advice.

### 2. Mechanics, widened

The enrichment statistic already written, pointed at three more event shapes:

- **Avoidable damage.** A damage spell most of the group takes none of, on the attempts where you
  took it. The same maths as the debuff rule, a different event.
- **Missed interrupts.** A cast that completes where it usually does not.
- **Deaths, re-framed.** The death breakdown exists; it should say whether what killed you was
  avoidable, which the rule above can now answer.

Ends with: most of what a raid leader reads Wipefest for, with no per-boss rules to maintain.

### 3. Why somebody died

A death is the loudest thing in a log and the app currently says only what landed beforehand. The
first question is not what killed them, it is **whether it was their death at all**.

**Collective, or alone.** When most of the raid dies inside a few seconds, the attempt ended - the
damage check was missed, the timer ran out - and nobody made a personal mistake worth reporting.
When one player dies at 8:01 and the rest live to 9:30, that death is theirs. The rule is a window
and a share: deaths clustered in time across a large fraction of the group are one event, not
twenty findings. Getting this wrong in the other direction is worse than missing it - a tool that
reports eighteen mistakes for one wipe will be closed and not reopened.

**Then, how.** Three shapes, all readable from the log and none needing a word about the boss:

- **A burst.** One or two hits took a large share of the player's health. The advanced parameters
  of every damage event carry the target's current and maximum health, so "that hit took 52% of
  them" is a fact sitting in the log rather than an estimate. This is a defensive that was not
  pressed, or a hit that should not have been taken at all - which the avoidable-damage rule from
  the previous milestone can often settle.
- **Attrition.** No single large hit, but a stack count that climbed - the dose is in the log - or a
  steady stream that outran the healing. This is a different conversation: it belongs to the healers
  as much as to the person who died.
- **Unhealable.** The time between dropping low and dying. Under about two seconds no healer could
  have reacted, and saying so protects the healer from a finding that was never theirs. Over ten,
  somebody was not watching.

The engineering note: the scanner reads the damage payload today but skips the health fields in the
advanced block. They are the whole basis of this milestone and cost nothing to start keeping.

Ends with: a death that explains itself - whether it was the raid's or the player's, and if the
player's, whether they were bursted, ground down, or beyond saving.

### 4. Execution, without knowing the class

Three signals that need no class knowledge at all:

- **Dead time.** Seconds in which a player cast nothing, against their own median.
- **Cooldown under-use.** A spell's recharge is *observable*: the shortest gap between its casts
  across a whole log is its cooldown. Compare uses against the length of the fight.
- **Self-buff uptime.** From aura applications on the player, against their own best attempt.

Ends with: rotation findings for every spec in the game, including the ones nobody wrote a module
for, at the price of being less specific than one that was.

### 5. The yardstick

Formalise the baselines so every detector can pick one: the group on this attempt, the player across
their own attempts, and where it exists, another player of the same spec in the same log.

Ends with: findings phrased as "you usually do this, and this time you did not", which is the form
advice is actually accepted in.

### 6. Talents and builds

The tree is in the log, so what was run is known exactly. Without a corpus we can still say what
changed between attempts and whether output followed, and whether a talent sat unused - a node taken
and its spell never cast is a finding needing no reference data whatsoever.

Ends with: build findings that are certain, and an honest blank where certainty is not available.

### 7. The night, summed up

Everything so far answers "what went wrong here". An evening also has a shape, and it is the shape
a raid leader argues about afterwards.

At the encounter, across all of its attempts:

- **Mistakes**, totalled and split - by player, by mechanic, and by half of the evening, because
  somebody who made four in the first six attempts and none in the last seven is the good news and
  currently reads the same as somebody who made four throughout.
- **Deaths**, and more usefully **who died first**. The first death of an attempt is often the one
  that caused the rest, and a player who is first in nine attempts out of thirteen is a finding on
  their own.
- **Consistency.** The spread of a player's output across the attempts. Steady at a fair number
  beats spiky at a high one, and the spread says which it was.
- **What a player led.** Top damage in nine of thirteen, top healing in eleven - a fact, plainly
  counted.

And the counterweight, which matters as much: **the things that went right.** Survived every
attempt. Took nothing that was not theirs all evening. Improved the most across the night. A report
that only ever accuses is a report people stop opening, and these cost nothing to compute from data
already gathered.

One rule for the praise: it is held to the same standard as the blame. "Top damage in 9 of 13" is a
count. "Best player" is an opinion and does not belong here.

Ends with: an evening a raid leader can read in one screen, with the good and the bad held to the
same evidence.

### 8. Rules that may rot, and the log that catches them

A derived rule needs ten attempts. A written one works from the first pull and can carry the one
thing derivation never will: what the mechanic is *for*. The two are not rivals - they fail in
opposite directions, which is exactly why both are worth having.

**Where the writing comes from.** Not from guides: prose has to be read by a language model, which
invents confidently at that volume, and it rots faster than anything. Not from BigWigs or DBM
either - checked, and neither permits it. BigWigs states `All Rights Reserved: You are free to fork
and modify on GitHub, please ask us about anything else`, which puts this squarely in "ask". DBM
ships an `All Rights Reserved` file granting nothing at all. Method Raid Tools has no public
licensed source. Their curated tables are theirs; the underlying facts are not, but a compilation
can be, so the line is: derive the same facts ourselves, do not copy their work.

Which leaves the source that is both authoritative and clean: **Blizzard's own journal**, through
the Battle.net Game Data API. Its encounter endpoints give the section structure - Tank, DPS,
Healer - the spell ids named in each, and `body_text`, which is Blizzard's own tactical advice and
therefore the "what to do" line we thought a person would have to write. No addon needed.

**Decided: the file ships with the app and nothing talks to Blizzard at run time.** The API wants
client credentials, and a desktop program that carries them hands them to anyone with a decompiler,
who then spends the quota or earns the ban for everybody. The alternatives are worse: asking each
person to register their own key is a wall almost nobody climbs, and running a proxy means running a
server. So a script fetches once per tier, writes the file, and the file is versioned here and built
into the release. No key, no network, no new way for opening a log to fail.

The file going stale mid-tier is handled without any of that. The log audit mutes a rule that stops
matching, and a file placed next to the exe overrides the built-in one, so a guild updates by
passing a file around. If self-updating is ever wanted, the thing to fetch is our own generated file
from a static URL - one unauthenticated request, content we control - and never the API itself.

**And the log audits all of it.** A section flagged for tanks means tanks should care, which is not
the same claim as "it lands on a tank" - an ability the whole raid takes while the tank must react
is flagged the same way. So every written rule is checked against what the log says, and where they
disagree the app says so instead of quietly reporting nonsense:

```
Possession Barrage - the file says tank, thirteen attempts say damage (64 of 71)
The rule is stale or it was never about who it lands on. Findings from it are muted.
```

Nobody else can do this, for the plain reason that nobody else has a second, independent source of
truth to check the first against. We do, and it is free.

The worked example that proves the need: Possession Barrage is two spell ids. One marks a single
player - a tank, 69 times in 71. The other damages the whole raid. A damage dealer hit by the second
is normal; hit by the first, it is a mistake. The engine already keys rules on id rather than name
and so gets this right by construction, but a file naming the wrong half of the ability would flag
every damage dealer in every pull, and only the log would ever notice.

**Also derivable, and worth having on its own:** how many players an ability lands on per cast. One
is a mark, most of the group is raid-wide, a handful is a spread. Only a mark can produce "you took
somebody else's mechanic"; a raid-wide ability can only produce "you took it when eighteen others
did not", which is a different detector and a different sentence.

Ends with: coverage from the first pull, an advice line written by the people who made the fight,
and a rule file that announces its own decay.

### 9. Reference data, if it turns out to be wanted

Only here, and only if "what should good look like" is still missing by then. This is where an
Archon-style aggregate would plug in, and the terms of use of whoever provides it are a real
question rather than a footnote.

Ends with: "against the top percentile" as an option, never as the foundation.

## What this means for the window

The tree - encounters, attempts, players - is the spine and stays. It is how a night is shaped and
the app reads it well.

What changes is that the tree becomes the navigation and stops being the product. The product is the
report, and the report is reached from the row it is about.

**The findings button goes.** A rule with nobody attached to it is not what anyone opened the app
to read, and a panel off to one side is the wrong place for an answer about a fight.

**Analyse sits on the attempt.** A mistake is always made in a particular pull, and that is where
somebody goes looking for it. That the rule behind it was drawn from all thirteen attempts is an
implementation detail of the evidence line, not a reason to make the person navigate elsewhere.

**And on the encounter, for the evening as a whole** - who made what, who died first, who led, who
improved. Milestone seven is what that page shows.

None of this needs the window thrown away.

## Order of work, and why

The first milestone is not the most interesting one, and it is still first. Every milestone after it
adds a detector, and detectors that each invent their own shape of output are how a tool ends up
with forty findings nobody can sort. Cost and evidence have to exist before there is anything to
rank.

The second is next because it is nearly free: the statistic is written, tested, and proven on a real
log. Pointing it at damage taken is a day of work for the largest single gain in the plan.

Deaths come third because they are what a raid leader opens the app for, and because the collective
versus individual split is the difference between a report worth reading and one that cries wolf on
every wipe.

The fourth is where the app stops being a mechanics tool and starts being a coach, and it is also
where it will be wrong most often. Dead time on a fight with a forced break in it is not a mistake.
Expect to spend as long tuning thresholds as writing detectors, and expect the ignore control to
earn its place there rather than in the mechanics list.
