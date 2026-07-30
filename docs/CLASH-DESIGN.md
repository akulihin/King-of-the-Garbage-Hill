# Clash — standalone network design

This document is the source of truth for the standalone network version of
`Clash`/`Клэш` exposed at authenticated routes `/clash` and `/clash/:gameId`. It covers
the authoritative multiplayer/bot match implemented by `ClashService`,
`ClashGameEngine`, `ClashBotAI`, and the Vue Clash store/components. The older
client-only campaign scaffold remains available only as the `/clash/lab` QA route and
does not define production rules.

The normative design input is the 2026-07-29 redesign request. The unit audit in
Appendix A is grounded in the Discord export
`DiscordExports/Empires_Endgame/Empire's Endgame_ Gamble for Glory - война
[1287805881680920626].json` for the inclusive range 2025-04-09 through 2025-11-25.

## 1. Scope and invariants

The standalone game supports exactly two sides:

- **host** — creates the lobby and owns the field configuration;
- **guest** — the invited player, or the bot in a bot match.

`guest` always receives the first sequential placement and between-clash action. This
rule is independent of faction, field orientation, previous clash result, disconnects,
or whether the guest is human.

The server is authoritative for lobby membership, locked hands, hidden placement,
commands, combat results, morale, victory, and reconnect state. A client renders only the
side-filtered state sent by the server. Client animation completion never decides damage
or advances the state machine.

Version one has no spectators. If spectators are added later, they may receive only the
information visible to both players; they must never receive either hand or an
unconfirmed row.

## 2. Lobby and match creation

The lobby follows the player-facing pattern of the existing Battleship lobby:

1. The host chooses **against a bot** or **against a player**.
2. The host sets field width and length.
3. Each side independently builds and locks its hand.
4. A player match waits for the invited guest. A bot match assigns the bot to `guest`.
5. Placement starts only after both hands are valid and locked.

There is no unit budget in this design. Duplicate unit definitions are allowed. A hand
is a multiset of unit instances, not a set of unique unit types.

Changing match type or dimensions invalidates both ready states and unlocks both hands.
The host cannot change configuration after placement has started.

### 2.1. Field dimensions

`width` is the number of columns and the first number in the displayed shape.
`length` is the number of rows in each player's territory and the second number.

| Setting | Value |
|---|---:|
| Default | `5×5` |
| Width | integer `3…10` |
| Length | integer `3…5` |
| Cells per side | `width × length` |

Each side uses local rows `1…length`:

- row `1` is the front row at the initial border;
- row `length` is the last/home row;
- columns retain the same identity across both territories.

Territory belongs to its original side for the entire match even after enemy units cross
the border.

### 2.2. Hand limits

The first three rows must be filled completely, so a legal hand contains:

```text
minimum = 3 × width
maximum = width × length
```

At `length = 3`, minimum and maximum are equal. At larger lengths, extra instances remain
in reserve for reinforcement. Locking a hand freezes the exact instance multiset.

Placing an instance removes it from the hand. Before its row is confirmed, its owner may
move it, remove it, or replace it and the instance returns to the hand. Confirmation is
irreversible. A dead or consumed instance never returns to the hand.

Only strict-executable roster entries from Appendix A may enter a production match.
Complete-but-blocked and WIP entries are catalog evidence, not selectable content.

## 3. Information ownership and privacy

Each side always sees:

- lobby identities and field dimensions;
- its own complete hand and all its own placements;
- whether the other side has locked its hand or confirmed the current row;
- every row already revealed by the state machine;
- public combat events, morale, active-use counters, and terminal result.

Each side must not receive:

- the other side's hand or reserve contents;
- unit IDs, positions, targets, or edit commands in an unconfirmed row;
- bot planning state, random choices, or hidden placements;
- hidden information embedded in reconnect snapshots, logs, analytics, errors, or
  optimistic UI payloads.

The server stores row-one commands in side-private state and returns place/remove
snapshots only to their author; those edits change neither the public revision nor the
public last-activity timestamp. While one player is waiting
after confirmation, their client receives only the opponent's ready flag, never the
opponent's partial row. Sequential rows are revealed only when their owner presses the
required confirmation button.

Reconnect restores exactly the view allowed by the current state. It does not reveal
data that was hidden when the disconnect occurred. A bot uses the same command and reveal
boundaries as a human guest.

## 4. Authoritative state machine

The canonical wire phase values are:

```text
Lobby
InitialFrontPlacement
GuestSecondRowPlacement
HostSecondRowPlacement
GuestThirdRowPlacement
HostThirdRowPlacement
ResolvingClash
GuestReinforcement
HostReinforcement
ActiveExchange
Finished
```

Every accepted mutation carries the match ID, latest public state revision, idempotency
key, and phase-specific payload. Public transitions monotonically increment the revision;
private setup place/remove edits deliberately share that revision until confirmation.
The server derives the actor and side from the authenticated SignalR connection. A stale
revision, wrong actor, illegal cell, unavailable hand instance, or command for another
phase is rejected without mutation.

### 4.1. Initial placement

#### `blind-row-1`

- Both sides fill every cell of local row `1`.
- They work concurrently with no turn-taking or per-unit delay.
- Neither side sees the other placement.
- The button is enabled only when the owner's row is full.
- Each side confirms with **«Сблизиться!»**.
- Confirmation locks that side's row. The first confirmer waits privately.
- When both have confirmed, both row-one layouts are revealed atomically.

The match then enters `guest-row-2`.

#### `guest-row-2` and `host-row-2`

- Guest fills every cell of local row `2`.
- Host cannot edit row `2` yet.
- Guest confirms with **«Становись!»**; the full guest row is revealed and the state
  becomes `host-row-2`.
- Host fills every cell of local row `2`.
- Host confirms with **«Становись!»**; the full host row is revealed and the state
  becomes `guest-row-3`.

#### `guest-row-3` and `host-row-3`

- Guest fills every cell of local row `3`.
- Guest confirms with **«Вступить в бой!»**; the full guest row is revealed.
- Host fills every cell of local row `3`.
- Host confirms with **«Вступить в бой!»**; the full host row is revealed.
- With all six rows confirmed, the first `clash-timeline` starts automatically.

Rows `4` and `5`, when present, start empty. There is no initial placement in them.

## 5. Clash resolution

At clash start, input is locked. Persistent bleed resolves first; if that is non-terminal,
the server snapshots every remaining living unit's effective speed and currently
available default action. Effective speed is clamped to `1…9` for scheduling; a value
above 9 acts as 9 and a value below 1 acts as 1.

Combat uses one **global** speed timeline. It is not resolved lane-by-lane. All units at
speed 9 across the entire battlefield resolve before all units at speed 8, and so on.

### 5.1. Deterministic timeline

The authoritative impact time for speed `s` is:

```text
impactAtMs(s) = (9 - s) × 500
```

| Effective speed | Impact time |
|---:|---:|
| 9 | `0 ms` |
| 8 | `500 ms` |
| 7 | `1,000 ms` |
| 6 | `1,500 ms` |
| 5 | `2,000 ms` |
| 4 | `2,500 ms` |
| 3 | `3,000 ms` |
| 2 | `3,500 ms` |
| 1 | `4,000 ms` |

Thus speed 9 has an immediate impact and speed 1 visibly waits four seconds. The server
publishes a timeline with a common server start timestamp. Clients may begin anticipation
frames before an impact, but the authored unit animation must put its hit, projectile
arrival, heal pulse, summon, or other decisive visual exactly at `impactAtMs`.

Each animation declares its local impact offset. Playback starts at
`max(0, impactAtMs - impactOffsetMs)`. Recovery may overlap the next speed band, but it
cannot delay or reorder authoritative impacts. After the speed-1 band, the server allows
up to `1,000 ms` of visual recovery before post-clash movement is presented.

A unit whose action has no legal target, is reloading, or has become unavailable emits a
visible `wait` or `reload` timeline event at its scheduled band. It does not silently
disappear from the timeline.

### 5.2. Equal speed is simultaneous

All actions in one speed band use the same pre-band snapshot:

1. Determine the living actors and legal actions at the start of the band.
2. Calculate every outcome without applying another same-speed outcome first.
3. Apply the collected damage, healing, statuses, summons, and resource changes
   atomically.
4. Resolve deaths only after all same-speed outcomes have landed.

A unit killed by another unit of the same speed still completes its action. Mutual kills
are therefore possible. Stable IDs may order log entries and animation layers, but they
must not change gameplay.

A unit killed by a faster band does not act later. Speed changes received after clash
start do not reschedule the current clash; they affect the next clash. A stun, disarm, or
other explicit action-denial effect received before impact may turn the scheduled action
into the visible `idle/wait` event.

Finite defenses are allocated once per hit, not once per speed band. A shield charge
blocks one strongest incoming hit; dodge charges then reduce the strongest remaining
hits. An equal-damage hit that also carries a status is treated as the stronger gameplay
outcome. Stable instance IDs order only otherwise equivalent events.

### 5.3. Default targeting and death

Unit descriptions override the default action. For the strict live roster:

- a melee unit attacks only when it is its side's front unit in the column, and targets
  the opposing front unit in that column even when earlier casualties left empty cells
  between the two fronts;
- a ranged unit attacks the current opposing front unit in its column from any depth;
- a unit performs at most one default action per clash unless its authored rule explicitly
  grants multiple hits or actions.

If a snapshotted target is killed by a faster band, a slower unit does not retarget in
the same clash and visibly waits. A ranged reload counter likewise produces a visible
reload event rather than an attack.

When health reaches zero, the unit dies. It is removed from occupancy after its speed band.
A corpse may exist as a visual or unit-specific effect marker, but it does not occupy or
block the cell under this standalone ruleset.

## 6. Melee advance and territory crossing

After all speed bands have resolved, the server computes advancement once from the final
clash state. A unit receives at most one advancement event per clash.

A melee unit advances when all of the following are true:

1. it was its side's snapshotted front melee unit for its column;
2. the snapshotted opposing front died during this clash;
3. the defeated unit's cell is empty after death resolution;
4. the advancing unit is still alive and able to move.

The unit enters the defeated unit's cell even when that cell belongs to enemy territory.
If earlier casualties had left a gap, this is still one kill-advance event even though it
crosses more than one empty board cell.
Ranged or support units do not use this kill advance. They may still use the empty-column
march below. If both opposing front units die, neither advances. A kill does not permit a
second attack or chain advance in the same clash.

Once across the border, a unit continues toward the enemy's increasing local row number.
Entering an enemy cell in row `length` is a breach and participates in the atomic victory
check.

There is one deliberate deadlock-closure rule. If the opposing side has no living unit
anywhere in a column, that side's surviving front unit — melee or ranged — marches one
adjacent empty cell toward the enemy home row after the clash. It cannot also use a kill
advance in the same clash, never marches more than one cell, and receives no second
attack. This preserves the classic Clash lane advance while preventing disjoint surviving
columns from stalling forever.

## 7. Between clashes

If the post-clash victory check is non-terminal, the game runs two subphases in order:

```text
reinforcement → actives → next clash
```

Guest receives first priority in both subphases.

### 7.1. Reinforcement

Each side gets exactly one reinforcement opportunity:

1. Guest may place one remaining hand instance or press **«Продолжить»**.
2. Priority passes automatically to host.
3. Host may place one remaining hand instance or press **«Продолжить»**.

A legal reinforcement cell:

- belongs to the acting side;
- is empty;
- is in local row `3…length`.

Placement commits immediately and automatically ends that side's reinforcement
participation. No confirmation button follows it. **«Продолжить»** permanently ends the
side's participation in this reinforcement subphase. A side with no reserve instance or
legal cell uses the same **«Продолжить»** action; the server does not invent a placement.

### 7.2. Active exchange

Morale determines how many active selections a side may make in this subphase. After
every selection, priority automatically passes to the other side that has not ended.
When the other side has ended, priority returns immediately, allowing the remaining side
to spend its remaining selections consecutively.

Pressing **«Продолжить»** permanently ends that side's participation in the current
active subphase. It cannot re-enter after the opponent acts. A side with no remaining
selection uses the same **«Продолжить»** action. Reaching the selection limit ends the
side automatically. The next clash starts when both sides have ended.

An active selection includes one concrete unit ability instance, target, cell, direction,
and every other required parameter. It is rejected if any parameter is illegal at the
current revision.

Victory is checked after each atomic active selection. A morale-5 double application is
one atomic selection for this purpose.

## 8. Боевой дух / morale

Code and comments may call this stat either `Боевой дух` or `Мораль`; they are the same
side-level value.

| Rule | Value |
|---|---:|
| Initial morale | `1` |
| Minimum | `0` |
| Maximum | `5` |

Morale is an allowance, not a spendable pool. Selecting an active does not lower morale
unless that ability explicitly changes morale. Values are clamped to `0…5`.

| Morale | Active rule for one between-clash subphase |
|---:|---|
| 0 | no active selections; the side can only press «Продолжить» |
| 1 | up to one selection |
| 2 | up to two selections |
| 3 | up to three selections |
| 4 | up to four selections; the fourth may repeat one concrete ability instance already selected by this side |
| 5 | up to four selections; every selected application resolves twice automatically; there is no fifth selection |

At morale 1–3, the same concrete ability instance cannot be selected twice in the
subphase. At morale 4, selections one through three follow that restriction; selection
four may be new or may repeat exactly one instance used earlier in the subphase. The UI
must present that fourth repeat as a visually distinct special choice.

At morale 5, selected ability instances remain distinct. The automatic duplicate is not
a second selection and does not violate the uniqueness rule. It repeats the same ability
with the same target and parameters. If the first application makes the target illegal,
the second has no further effect; it does not retarget. The UI marks every pending
selection with `×2`.

## 9. Victory, defeat, and draw

Terminal conditions are evaluated atomically:

1. after same-speed death resolution when a side may have lost every unit;
2. after post-clash simultaneous advancement;
3. after an atomic active selection.

A side loses if either:

- an enemy unit reaches that side's static home row `length`; or
- it has no living allied unit anywhere on the battlefield.

The result is a draw if both sides satisfy a loss condition in the same atomic resolution.
This explicitly includes:

- mutual elimination in one speed band;
- simultaneous breaches of both home rows;
- one side being eliminated while its own simultaneous effect eliminates the other;
- simultaneous combinations of elimination and breach.

If exactly one side satisfies any loss condition, that side loses. Reinforcement never
rescues a side after a terminal check.

## 10. Network and reconnect contract

The current implementation stores canonical games, hidden state, the last public
resolution, revisions, and a bounded idempotency-key window in process memory.
`expectedRevision` rejects stale sequential commands; `commandId` makes a recently
accepted command safe to retry. Parallel first-row edit/confirm commands are the sole
revision exception because both players intentionally act at once.

Private setup place/remove commands do not increment the public revision or
`LastActivity` and generate no opponent, lobby, or active-game-ID traffic. The author
receives a fresh personalized snapshot; confirmation increments the revision and is the
only public row-reveal boundary.

There is no durable Clash command log or replay export in this release. A process restart
therefore loses active matches. Lobby and finished games that remain inactive for 30
minutes may be reclaimed; an active match in placement, combat, reinforcement, or active
exchange is not removed merely because the players disconnected.

The server sends per-participant snapshots and a public timeline only to the current
registered connections of the two match participants. Opponent `ArmySize`, `HandCount`,
selected definitions, reserve DTOs, and unconfirmed units are redacted rather than
approximated. A non-participant cannot join the SignalR match group or request a match
snapshot. If one transport authenticates as a different identity, the hub removes it
from the previously tracked Clash group before binding the new identity.

During `clash-timeline`, disconnects do not pause or change authoritative combat. During
nonterminal combat, the canonical phase remains `ResolvingClash` until
`StartedAtUtc + DurationMs`; reinforcement, active input, and bot continuation are
rejected or withheld before that server deadline. Reconnect returns the trace and resumes
presentation at elapsed server time. During an input phase, the match waits for a legal
command or reconnect unless a separately configured timeout policy exists. This design
does not invent a timeout, surrender penalty, or disconnect winner.

## 11. Validation requirements

A conforming implementation must cover at least:

- every legal field dimension and both hand boundaries;
- duplicate units and exact three-row initial consumption;
- row-one privacy before both «Сблизиться!» commands;
- reveal timing for «Становись!» and «Вступить в бой!»;
- guest-first order in both player and bot matches;
- global speed ordering across different columns;
- same-speed mutual kills;
- faster kill suppressing a slower action;
- damage event timestamps matching animation impact timestamps;
- melee occupancy of the fallen opposing front across the territory border;
- one-cell empty-enemy-column march and a disjoint-column no-stall simulation;
- simultaneous breach draw and mutual-elimination draw;
- reinforcement placement only in rows `3…length`;
- permanent per-subphase effect of «Продолжить»;
- morale 0 through 5, the morale-4 fourth repeat, and morale-5 automatic double;
- reconnect snapshots that preserve every hidden-information boundary.

## Appendix A. Discord unit audit, 2025-04-09—2025-11-25

### A.1. Boundary and classification

The inclusive audit window contains 164 export messages. Classification is deliberately
stricter than “a named idea exists”:

- **strict executable** — stable name and numeric triple, with every included effect
  expressible by the current live Clash engine and no `deferredReason` or `reviewReason`;
- **complete card but blocked** — the author supplied a recognizable complete unit card,
  but at least one required shared mechanic, ordering rule, campaign lifecycle, or target
  contract is missing; these entries are not playable;
- **WIP/review** — missing or contradictory stats/effects, an explicitly tentative
  value, an unfinished sentence, an unnamed sketch, or a boss sequence rather than a
  complete selectable unit.

Catalog presence is not playability. In particular, a `complete card but blocked` row
must not enter a hand, immutable match plan, bot roster, or network payload until all of
its blockers are resolved and tested.

### A.2. Eight strict-executable live units

| ID | Exact name | АТК-ХП-СКР | Source message |
|---|---|---:|---|
| `shield-bearer` | Щитарь | `1-5-1` | `1359427160133341205` |
| `legionary` | Легионер | `1-4-1` | `1359427160133341205` |
| `archer` | Лучник | `1-1-3` | `1359472078700413070` |
| `shield-bow` | Щито-лук | `2-4-2` | `1381226429123137577` |
| `dancer` | Танцор | `1-4-5` | `1359468214072905859` |
| `nimble-gek` | Проворный Гек | `1-3-7` | `1365283831325921351` |
| `mechanical-crossbow-08` | Мехакинетический Самострел 0.8 | `2-3-2` | `1442090262489989201` |
| `mechanical-bolt-thrower-10` | Мехакинетический Стреломёт 1.0 | `2-2-5` | `1442090262489989201` |

These eight are the only unit definitions eligible for the standalone hand until another
row graduates from blocked to strict executable. They are present in the production
SignalR catalog, human hand builder, bot army builder, server resolver, and visual asset
map.

### A.2.1. Implemented mechanics and visual feedback

| ID | Exact executable behavior | Authored visual feedback |
|---|---|---|
| `shield-bearer` | Melee. Starts the match with one `Блок` charge, which blocks one hit and is not refreshed between clashes. A full occupied row made only from current `legion-candidate` units grants that row +2 effective speed; in the live catalog only Щитарь has that tag. | Heavy shield-bearer portrait; melee lunge, shield flare/block, hit, death, wait, and advance states. |
| `legionary` | Melee. Starts with one persistent `Блок` charge. Легионер is not a `legion-candidate` and does not activate `Легион!`. | Armored legionary portrait; melee lunge and distinct shield flare/block plus common hit/death/wait/advance states. |
| `archer` | Ranged. Each clash attacks the opposing front unit in its column from any row. | Archer portrait and projectile-volley recoil timed to the speed-3 impact. |
| `shield-bow` | Ranged from any row; starts with one persistent `Блок` charge. | Shield-archer portrait, projectile volley, and shield flare/block. |
| `dancer` | Melee. Starts with four dodge charges; each exact hit covered by a charge deals at most 1 damage. Its first damaging attack adds one persistent bleed stack, which deals 1 damage at each later clash start. Any offensive AOE kills it immediately. | Blade-dancer portrait; spinning attack/passive motion, bleed pulse, hit, death, wait, and advance. |
| `nimble-gek` | Melee. Starts with three dodge charges; each covered exact hit deals at most 1 damage. Any offensive AOE kills it immediately. | Gek portrait with a rapid dash/dodge motion timed to speed 7. |
| `mechanical-crossbow-08` | Ranged from any row. After firing, it spends the next two clashes reloading, then may fire again. | Mechanical crossbow portrait, hard recoil/projectile action, and visible reload wait. |
| `mechanical-bolt-thrower-10` | Ranged from any row and fires every clash. | Bolt-thrower portrait and faster mechanical recoil timed to speed 5. |

All portraits use individual square WebP assets over the battlefield backdrop.
Animation state is driven by authoritative `startOffsetMs`/`impactOffsetMs` events:
speed-9 effects arrive immediately, damage/status counters change at impact, death and
advance follow their matching events, reduced-motion clients receive the same final
state without delayed transitions, and reconnect playback starts at elapsed server time.

### A.3. Seventy-eight complete cards with blockers

The categories below assign one primary blocker to every row so the list is auditable.
Many rows also have secondary blockers.

| Primary blocker category | Complete source cards that remain non-playable |
|---|---|
| Cross-territory movement, depth, targeting, or cell geometry (14) | `anti-horse` — Противоконь; `phalanxer` — Фалангер; `halberdier` — Алебардщик; `lancer` — Лансер; `trained-spearman` — Обученный копейщик; `master-spearman` — Мастер-копейщик; `guard-spearman` — Страж-копейщик; `storm-spear` — Грозовое копье; `weapon-thrower` — Метатель оружия; `tridenter` — Трайдэнёр; `sand-snake` — Песчаный змей; `widow` — Вдова; `death-chaldean` — Халдей смерти; `priest-buffer` — Жрец - Баффер |
| Cavalry, hero identity, acquisition, or cross-battle persistence (12) | `mounted-anti-horse-ramrods` — Противоконь на коне с железными шомполами; `mounted-anti-horse-heavy` — Противоконь на коне в тяжелой броне; `knight` — Рыцарь; `holy-paladin` — Святой паладин; `holy-cavalry` — Святая конница; `tenth-perst` — Десятый; `scytheman` — Косиньер; `eagle` — Орёл; `vampire-lord` — Вампир-Лорд; `vampire` — Вампир; `many-faced-killer` — Многоликий убийца; `stalker` — Сталкер |
| Typed armor, shield, weapon, counter, or damage precedence (14) | `warrior` — Воин; `hammerer` — Молотобой; `battle-hammer` — Боевой молоток; `heron` — Цапля; `ice-pick` — Ледоруб; `white-stone-hammer` — Молот из Белого Камня; `champion` — Чемпион; `master-fencer` — Мастер Фехтавания; `jugger` — Джаггер; `former-tetrakor-gladiator` — Бывший Гладиатор Тетракора; `zweihander-warrior` — Воин - Цвайхандер; `hammer-warrior` — Воин - Молот; `bear` — Медведь; `bracovermin` — Браковермин |
| Status, reload, AOE, morale, or within-clash timing (19) | `slingers` — Пращи; `monk` — Монах; `punisher` — Каратель; `long-cross` — Длинный крест; `double-cross` — Двойной крест; `heretic` — Еретик; `reaper` — Жнец; `black-swordsman` — Черный мечник; `falling-star` — Падающая Звезда; `berserker` — Берсеркер; `cold-hand-knight` — Рыцарь Хладной Руки; `downpour` — Ливень; `sadist` — Садист; `assassin` — Ассассин; `desert-demon` — Пустынный демон; `loud-gek` — Громогласный Гек; `utilizer` — Утилизатор; `regenerator` — Регенератор; `fire-snow` — Огненный снег |
| Corpse, reserve, spawn, trap, regeneration, or transformation state (9) | `nail` — Гвоздь; `barbarian-raider` — Варвар, мародер, налетчик; `poacher` — Браконьер; `scorpion` — Скорпион; `gek-tamer` — Укротитель Геков; `gek-corpse-eater` — Гек-трупоед; `hexagek` — ГексаГек; `hirudian` — Хирудианец; `hirud-child` — Дитя Хируда |
| Shared aura, interception, healing choice, marks, inventory, mode, or activation contract (10) | `eternal-cold` — Вечный холод; `forest-guard` — Страж; `cur` — Шавка; `healer` — Врачиватель; `cultist` — Культист; `hirudian-cult-father` — Отец культа Хирудиана; `zero` — Нулевой; `uncle-vova` — Дядя Вова; `mechanical-crossbow-20` — Мехакинетический Самострел 2.0; `executioner` — Палач |

The category counts total exactly 78. “Complete” here describes the source card as a
design artifact; it does not waive the named blocker.

### A.4. Fifteen WIP/review carriers

| ID | Exact name | Why it is not a complete executable card |
|---|---|---|
| `fool-long-stick` | Дурачок с длинной палкой | one hidden rule contains «какое-то кол-во» |
| `medical-healer` | Медицинский хиллер с легким арбалетом | full-heal charges are «видимо два заряда» |
| `abbot` | Настоятель Святой Церкви | sequence ends with unfinished «Убив 7 целей…» |
| `scarecrow` | Пугало | displayed `2-2-4` conflicts with no damage and death from any hit |
| `hell-salamander` | Адская Саламандра | passive is cut off at «Выкапывается …» |
| `ranger` | Рейнджер | no combat triple or exact battle values |
| `mounted-rangers` | Отряд Рейнджеров на беговых конях | no combat triple or cavalry lifecycle |
| `gek-crowd` | Толпа маленьких геков | replacement stock size is missing |
| `cobra` | Кобра | no triple or cost; only analogies and poison notes |
| `moonshine-grandmother` | Бабка Самогонщица | «Варит самогон» has no quantity, timing, target, or inventory result |
| `he` | Он | boss encounter/second-form sequence rather than a selectable unit contract |
| `slave-owner` | Рабовладелец | no triple, slave count, targeting, or reward lifecycle |
| `organ` | ОргАн | source explicitly says «Статы бы придумать» |
| `shield-archer-sketch` | щито-лучники / метатели дисков | visual/equipment sketch without one stable card |
| `alliance-charger-sketch` | альянсовый «заряжающий» юнит | no name, triple, charge duration, target, or consequence |

The correct OrgАн source is message `1369418274797781012` from 2025-05-06. Message
`1382288986579664916` belongs to Настоятель Святой Церкви and must not be used as OrgАн
provenance. The corrected catalog defect is recorded as finding `m67` in
`AUDIT-FINDINGS.md`.
