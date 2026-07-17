# Battleship — designer review and approval

**Status:** approved 2026-07-17 and implemented in game version 5.0.12.
**Prepared:** 2026-07-14  
**Review owner:** Game Designer  
**Sources reviewed:** [Google GDD `МорскойБой`](https://docs.google.com/document/d/1b0MbvMmCp78fUXWt9PX3BZcFRI_l6WdbTXtxQeZUuTw/edit?tab=t.ip6m4linjo3m) — all 30 tabs and all embedded images — plus [`BattleShip_update`](../BattleShip_update).

## How to review

For every numbered item, select one result and add a comment when the recommendation is rejected or needs adjustment.

- `[ ] Approve` — make the recommended behavior the implementation target.
- `[ ] Reject` — preserve current behavior or provide a replacement rule.
- `[ ] Approve with edits` — write the exact replacement rule in the comment block.

Prefixes used in this document:

- `D` — a design decision needed because the specification conflicts with itself or omits an outcome.
- `I` — a confirmed implementation correction; the intended behavior is already sufficiently clear.
- `X` — dormant/dead content requiring a keep, remove or defer decision.

## Executive summary

The 40-coin budget, exact 10-ship fleet template, most catalog prices, deck counts, armor, speed and Space values substantially match the GDD. The largest problems are state-machine and information-flow issues rather than number drift:

1. Final Boarding can retrigger indefinitely, grant unlimited ammunition/crew, leave ghost source ships and prevent the second player's transition.
2. CAPTURE is not reliably playable from the client and can softlock a match.
3. A destroyed deck can be shot repeatedly for unlimited turn resets.
4. Poison can affect mirrored coordinates on a different physical board.
5. Scout/reveal accounting and several UI status fields expose incorrect or incomplete information.
6. Destroyed weapon modules usually continue to function.
7. Per-instance free-ship upgrades collapse on the server, while Discus is sold despite having no mechanic.

The source hierarchy below should be approved before implementation begins because the GDD and appendix disagree on several rules.

---

## Part A — source hierarchy

### D00 — Authoritative material

**Observed conflict**

- The standard Battleship tab describes a baseline that later mechanic tabs intentionally override.
- `AI summ` is a derivative summary and contradicts primary tabs.
- `Вкладка 28` explicitly says its information is obsolete.
- Appendix headings, dated resolutions and `Required behavior` blocks add rules not always repeated in the body.

**Recommendation**

1. Explicit appendix `Required behavior` and designer-resolution notes override older conflicting GDD prose.
2. Detailed current mechanic and unit tabs override the standard-rules baseline and `AI summ`.
3. `AI summ` is non-normative reference material.
4. `Вкладка 28`, its old fleet rules and Neptune diagrams are excluded from implementation.
5. An image is normative only when text relies on it for geometry or declares it a final UI asset.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

---

## Part B — rules requiring a designer decision

### D01 — When does a hit grant another shot?

**Conflict:** baseline/loop prose says any enemy-ship hit resets the shot; detailed armor/shot rules imply that only destroying a deck resets it.

**Recommendation:** only destruction of a previously live enemy deck grants another shot. A scratch, repeat shot into a dead deck, friendly hit, own-board shot, miss or summon hit ends the turn unless a named rule says otherwise.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D02 — Normal Ballista/Buckshot eligibility

**Conflict:** different passages require a surviving Mid/Close ship, a surviving compatible weapon, or both. The range tab says there are three classes but defines five: Close, Close melee, Mid, Tetra and Far.

**Recommendation:** normal Ballista/Buckshot fire requires at least one living Mid or Close ship with a living compatible weapon module. Close melee does not enable normal fire. Use `Mid` as the canonical identifier.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**

Внести правки, но только для балисты. Дробь - это альтернативный снаряд для Тетракамнемета, а не обычный выстрел (иправить если не так). Дробь должна быть доступна только для оружия Тетракамнемет. Дробь является альтернативным снарядом, выбирающимся вместо Белого Камня.

> 

### D03 — Final Boarding trigger and conversion

**Unspecified:** simultaneous triggers, whether boarding is global or per player, what happens when a fleet has no Mid ship, and which source-ship properties survive conversion.

**Recommendation:** Final Boarding is a single global, one-time transition triggered immediately after the first state change that leaves either player with no living Mid ships. Resolve both players in that transition. Remove every converted Close/Close melee source from its original board and create one pending boarding unit retaining only documented speed, Space/reveal and collision damage. Source armor, modules, statuses and passive effects do not remain active unless named explicitly.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D04 — Boarding movement without a legal shot

**Problem:** boarding units move only after shots. A player with only Close melee/boarding units and no ammunition can have no legal action that advances them.

**Recommendation:** during Final Boarding, a player with no legal shot receives a `Move/Pass` action that advances summons once and ends the turn. It is not a shot and cannot grant another turn.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**

Ты прав во всём, хочу лишь добавить, что такие суммоны, как пираты могут захватить у врага корабль типа Close и тогда этот игрок, захвативший корабль сможет стрелять, пока противник не убьёт этот корабль с баллистой.

> 

### D05 — Artillery-ammunition defeat condition

**Conflict:** `remaining artillery ammo < remaining enemy decks` ignores armor, Buckshot, explosions, Burn and Destroy. Less ammunition may sometimes win, while more ammunition may still be insufficient.

**Recommendation:** do not predict mathematical sufficiency. A player loses when all non-summon ships are destroyed, or when the player has no living/usable weapon and no living/pending unit capable of damaging the opponent.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D06 — CAPTURE lifecycle

**Unspecified:** target board, ownership of captured weapons/passives/regions, Nimble and Desiccator interactions, repeat capture, death summons and victory counting.

**Recommendation:** CAPTURE is a compulsory debuff on the original owner's ship. The owner must target that ship on their own board with an otherwise legal damaging shot. A captured ship supplies no weapon, passive, region or victory value to either player. A second Pirate does not toggle CAPTURE off. Destroying the ship removes the status normally and produces no death summon unless explicitly stated.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**

Когда у корабля появляется статус Capture - закрасить его нетронутые палубы фиолетовым цветом для обоих игроков. Если палубы повреждены, убиты и т.д. - закрась соответствующим цветом.
Если пират захватывает корабль и он становится Capture, То игрок 2, чей корабль захватили не может стрелять никуда кроме этого корабля, даже в суммонов на своей карте он не может стрелять, пока не убьет корабль и не пропустит ход после убийства своего корабля.

> 

### D07 — Burn versus Incendiary destruction

**Conflict:** the primary GDD says Incendiary burns a ship; appendix §18 says only Greek Fire creates `Burn/Горит`, while Incendiary kills without igniting.

**Recommendation:** follow the appendix. Greek Fire alone creates persistent `Burn/Горит`. Incendiary ammunition and explosions destroy without adding Burn. Death-summon suppression should name the killing effect rather than reuse a Burn flag.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**
Выстрел Горючки, Уничтожение Горючей баржи, Греческий огонь накладывают статус burn. Но только греческий огонь оставляет его на поле.

> 

### D08 — Incendiary Barge trigger

**Conflict:** the GDD says the Barge explodes on any damage; the appendix scenario frames it as destruction by a player shot.

**Recommendation:** the Barge detonates when one of its live decks is damaged by a player shot or Ram collision. On trigger, both decks die and one Space=2 blast resolves from the union of its occupied cells. Freeze and unrelated passive effects do not detonate it unless named explicitly.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**
Любое получение урона или уничтожение любой палубы Горючей баржи ведет к твоей рекомендации.
> 

### D09 — Brander removal and detonation

**Conflict:** generic text says the Brander explodes on death; explicit exceptions say Freeze extinguishes it and live-deck collision breaks it without detonation.

**Recommendation**

| Removal cause | Detonates? | Explosion sound? |
|---|---:|---:|
| Owner/player shot | Yes | Yes |
| Greek Fire | Yes | Yes |
| Another explosion | Yes | Yes |
| Freeze/Drakkar | No | No |
| Collision with a live deck | No | No |
| Leaving the board/expiry | No | No |

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D10 — White Stone: Destroy or ×4 damage?

**Conflict:** `Destroy` implies unconditional destruction, while ×4 base damage is 8 and a 9-HP deck survives.

**Recommendation:** White Stone deals 8 damage, destroys the targeted deck's module and applies Stun even if armor survives. Remove `Destroy` from the mechanical description unless unconditional destruction is intended.

- [ ] Approve
- [ ] Reject
- [x] Approve with edits

**Designer comment**
Урон белого камня = базовый урон умножить на 4, сейчас это 8, палуба с 9 HP выживет. Однако оглушение вражеского игрока и уничтожение модуля пораженной палубы происходит всегда.
> 

### D11 — Discus availability

**Conflict:** the purchase table sells Discus for 1 coin, but its own tab says not to implement it yet. Current code charges the coin and adds no usable behavior.

**Recommendation:** hide and server-reject the upgrade until its complete mechanic is approved.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D12 — First-player final tiebreak

**Conflict:** `prettier nickname` is subjective. Current code uses random selection and does not consistently count upgrades on free units.

**Recommendation:** after coins, upgraded-unit count and Home-unit count, use a seeded random tiebreak. Count a unit as upgraded when it is a paid replacement or has any paid upgrade, including free Triple/Tetranavis units.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D13 — Home and multi-region units

**Conflict:** Home is described as future/non-mechanical but already affects first-turn order. The GDD does not say whether a dual-region ship consumes one or two of the three regions.

**Recommendation:** retain `Home` only as a first-turn tag for now. A unit consumes every non-Tetracor region listed on it; a dual-region unit therefore consumes two region slots. The UI shows all consumed regions.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D14 — Summon timing and use accounting

**Unspecified:** cooldown counting, invalid deployment, free ship-produced summons, Brander's extra use, movement after skipped turns and immediate-spawn penalty grace.

**Recommendation:** four ordinary successful deployments per player; invalid attempts consume nothing. Free pending/ship-produced summons consume neither use nor cooldown. Brander has one separate successful use. Cooldown advances on completed global shots only. A summon first moves after the next completed shot, never after Penalty/Stun or another no-shot turn. Spawn grace prevents the row 1–3 death penalty until that next completed shot.

- [ ] Approve
- [x] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D15 — Movement and effect precedence

**Unspecified:** crossing summons, multiple units entering one cell, poison/Freeze/collision order, explosion chains and repeated effects.

**Recommendation:** resolve a completed shot in this order:

1. Resolve the shot and all chained explosions.
2. Check immediate victory/boarding transition.
3. Move summons in stable spawn order, one movement step at a time.
4. For each entered cell: permanent fire → Freeze → live/dead deck collision → poison/reveal effects.
5. Resolve chained deaths before the next summon moves.
6. Check victory again, then continue or switch the turn.

Dead decks are passable and cannot be damaged, killed or counted again.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D16 — Poison cone

**Recommendation:** the image defines a two-row cone on one physical board: three cells in the first row and five in the second, clipped at board edges and rotated with travel direction. Iceberg and Alchemical Barge use the same geometry unless a unit entry explicitly differs. Poison never applies to mirrored coordinates on another board.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D17 — Canonical names and exact messages

**Conflict:** `Пират`, `Пиратская лодка` and `Пираты` are used interchangeably. Maneuver has both `Maneuvering Double маневрирует!` and `Маневрирующая двойка маневрирует!` as exact text.

**Recommendation:** use `Пираты` only for the source ship, `Пиратская лодка` for the summon and `Пират` only as informal prose. Use `Маневрирующая двойка маневрирует!` as the exact owner-only movement message. Preserve `Даёт по вёслам!` as the shooter-facing Mast message.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

### D18 — Armor information visibility

**Conflict:** primary tabs do not clearly prohibit armor values; `AI summ` says they are hidden. The current owner fleet UI shows exact current/max HP.

**Recommendation:** owners see exact armor for their fleet. Opponents see only unknown/intact, scratched and destroyed states unless a reveal mechanic explicitly exposes armor.

- [x] Approve
- [ ] Reject
- [ ] Approve with edits

**Designer comment**

> 

---

## Part C — confirmed implementation corrections

Approving an `I` item means “make code and UI match the behavior stated here.”

| ID | Correction to approve | Evidence |
|---|---|---|
| I01 | Make Final Boarding idempotent; resolve both players and grant bonuses exactly once. | `BattleshipGameEngine.cs:1095-1214`, `BattleshipService.cs:557-567` |
| I02 | Remove converted source ships from their board, active abilities and victory accounting. | `BattleshipGameEngine.cs:1129-1147` |
| I03 | Reject repeat damage to a 0-HP deck; it cannot grant another turn. | `BattleshipGameEngine.cs:123-143`, `:674-731` |
| I04 | Apply White Stone and Greek Fire effects even when a cell was previously revealed/hit, subject to normal legality. | `BattleshipGameEngine.cs:123-162` |
| I05 | Expose CAPTURE to the client, permit its own-board target, consume ammunition normally and remove the undocumented second-Pirate toggle. | `BattleshipGameEngine.cs:53-80`, `:1483-1501`; `CombatPhase.vue:220-231` |
| I06 | Constrain poison to one physical board and remove Iceberg's undocumented third row. | `BattleshipGameEngine.cs:1617-1715` |
| I07 | Credit reveal thresholds to the correct player; show intact occupancy in Scout-revealed cells without marking a hit. | `BattleshipGameEngine.cs:773-824`; `BattleshipService.cs:1611-1640` |
| I08 | Preserve source ship/deck identity for weapons. A destroyed module cannot fire, and multiple identical weapons must be individually selectable. | `BattleshipModels.cs:199-213`; `ShipCatalog.cs:211-221`; `BattleshipService.cs:731-775` |
| I09 | Send/render destroyed, frozen, devastated, captured and permanent-fire cell states. | `BattleshipService.cs:1749-1765`; `signalr.ts:945-949`; `CellComponent.vue:34-49` |
| I10 | Preserve per-instance upgrades for both Triple slots and validate mutually exclusive boiler upgrades for free Tetranavis. | `FleetValidator.cs:35-52`, `:104-145` |
| I11 | Spawn Triple crew only once, from a surviving eligible Triple, during the one-time boarding transition; never create an invisible summon. | `BattleshipGameEngine.cs:832-849`, `:1149-1167` |
| I12 | Do not move summons on Penalty/Stun/invalid no-shot turns; run victory checks after valid-shot movement deaths. | `BattleshipService.cs:487-505`, `:603-619`, `:1314-1320` |
| I13 | Register summons in the board grid immediately on spawn/reentry and apply Drakkar Freeze before movement. | `BattleshipService.cs:842-946`, `:958-1021` |
| I14 | Damage the deck at the collision coordinate and center Scout's collision reveal on that coordinate. | `BattleshipGameEngine.cs:1419-1528` |
| I15 | Make Light Wood dodge only into the documented free cell behind; remove opposite fallback and stale hit metadata. | `BattleshipGameEngine.cs:1737-1798` |
| I16 | Include the physical target board and all AoE cells in SignalR shot events. | `GameHub.cs:1409-1465`; `battleship.ts:210-221` |
| I17 | Enforce setup phases; lock/revalidate confirmed placement; restore a previous placement after failed reposition; do not replace bots after setup starts. | `BattleshipService.cs:198-225`, `:361-456`, `:1111-1127` |
| I18 | Remove bot access to hidden ship positions; add legal summon-targeting and maneuver parity where applicable. | `BattleshipBotAI.cs:818+` |
| I19 | Show only legal weapons, label Aim in revealed cells, and retain Buckshot during Boarding unless explicitly prohibited. | `WeaponBar.vue:51-68`; `BattleshipService.cs:1538+` |
| I20 | Decide whether the GDD's nine embedded UI icons are final assets; current inline SVGs differ materially for Ram and Scout. | `battleship-icons.ts` |

### Approval checklist for Part C

- [x] I01
- [Reject] I02
- [x] I03
- [x] I04
- [x] I05
- [x] I06
- [x] I07
- [x] I08
- [x] I09
- [x] I10
- [x] I11
- [x] I12
- [x] I13
- [x] I14
- [x] I15
- [Reject] I16
- [x] I17
- [x] I18
- [x] I19

**I20 — icon decision**

- [x] Replace current icons with the GDD images
- [ ] Keep current icons; GDD images are references only
- [ ] Request a separate art pass

**Part C comments by ID**

> 

---

## Part D — dormant and dead mechanics

### X01 — Alliance selector

Current state: UI concept only; runtime supports Empire alone.

- [x] Retain as future-facing/disabled UI
- [ ] Hide until Alliance exists
- [ ] Other

**Comment:**
>

### X02 — Discus

Current state: costs one coin but adds only an unused ability flag.

- [x] Hide and reject until designed
- [ ] Remove completely
- [ ] Design and implement now — attach exact rules

**Comment:**
>

### X03 — Neptune and obsolete content

Current state: Neptune is ranked/listed but unavailable; detailed mechanics and seven diagrams exist only in the explicitly obsolete tab.

- [x] Keep unavailable; old rules remain archival
- [ ] Remove Neptune references from current tabs
- [ ] Restore Neptune — attach updated rules

**Comment:**  
> 

### X04 — Tetracor region

Current state: only an exemption tag for default Triple/Tetranavis units; no playable Tetracor system exists.

- [X] Keep as an internal exemption tag
- [ ] Remove and model defaults without a region
- [ ] Expand later

**Comment:**  
> 

### X05 — Future armor/Home/resource systems

Current state: armor-type multipliers and Freeze/Devastated resource rewards are future-only; Home affects only first-turn order.

- [x] Keep dormant hooks, clearly marked non-live
- [ ] Remove unused hooks until designed
- [ ] Other

**Comment:**  
> 

### X06 — Desiccator boarding behavior

Current state: one surviving Desiccator auto-wins at boarding, so its speed-three boarding behavior is normally unreachable. It matters only when both players have Desiccator and passives are disabled.

- [x] Intended niche behavior
- [ ] Desiccator should board instead of auto-win
- [ ] Other

**Comment:**  
> 

### X07 — Dead implementation scaffolding

Currently unused or redundant: `CellState`; `ShotType.Catapult/Tetracatapult`; status values Stun/Penalty/Freeze/Destroy; mutable `DamageMultiplier`; `Weapon.Damage`; `Summon.Damage`; `nimble`, `stationary`, `discus_thrower`, `greek_fire_summon`; and untransmitted `AoECells`.

**Recommendation:** retain only fields with an approved near-term use. Remove or explicitly document the remainder during implementation cleanup so dormant fields do not appear to be working mechanics.

- [x] Approve cleanup
- [ ] Retain all as future hooks
- [ ] Review individually

**Comment:**  
> 
Дизайн документа был произведен людьми, работающими над проектом. Термины кода были сгенерированв через AI. Там где это возможно, измени и приблизь термины кода к терминам документа для большей разборчивости.

---

## Part E — edge-case defaults

| Area | Recommended default | Designer exception/comment |
|---|---|---|
| Penalty rows | Highlight while at least one enemy summon occupies rows 1–3. Tooltip includes summon name and spawn-grace rule. | |
| Trails | Viewer-relative; include entry/start cell; persist as historical information after death/exit unless explicitly cleared. | |
| Mast | Gate messages on a living Mast deck at event time; use canonical letter+number coordinates. | |
| Explosions | Use the images' square/Chebyshev Space geometry, clipped to the board. Resolve each ship once per blast; dead decks cannot retrigger effects. | |
| Nimble marker | A Ballista attempt leaves the light-green historical marker in both views, including after movement/destruction. | |
| Pirate collision | `Original 3/4-deck` means catalog deck count, not remaining live decks. Pirate dies without damage. | |
| Brander collision | Live deck destroys Brander without deck damage, detonation or sound. Dead deck is passable. | |
| Maneuver | Once per ship instance, start of owner's turn, 1–2 cells, valid straight translation only; killed-deck identity follows the ship. | |
| Greek Fire | May target either board; successful use ends the turn and resets selection to Ballista after resolution. Invalid attempts consume nothing. | |
| Multiple statuses | Summon icon overlays rather than replaces hit/miss/fire/background state. Tooltip lists all relevant states. | |

**Part E approval**

- [ ] Approve all defaults
- [ ] Approve except rows annotated above
- [x] Revision required

**Comment:**
>
Одобрить все кроме пункта: | Greek Fire | May target either board; successful use ends the turn and resets selection to Ballista after resolution. Invalid attempts consume nothing. | |
Греческий может быть нацелен только на свою доску и стрелять может только по своей доске. Успешное использование завершает ход и сбрасывает выбор на Баллисту после разрешения.
---

## Part F — final sign-off

- [x] Approve all recommendations and implementation corrections
- [ ] Approve all except the IDs listed below
- [ ] Revision required before implementation

**Exception/blocking IDs**

> 

**Additional comments**

> 

**Approved by:**  
**Date:** 17.07.2026

## Implementation record

Implemented 2026-07-17 as finding M112. The implementation follows the checked decision on each item, with the item-level choices taking precedence over the broad Part F checkbox:

- I02 remains rejected: converted Close/Close melee source ships stay on their original board, retain active behavior and remain in victory accounting; the one-time conversion guard prevents duplicates.
- I16 remains rejected: shot events still omit physical-board/AoE metadata; the client tracks its own-board action locally for presentation.
- D14 remains rejected except for the independently approved I11–I13 corrections.
- Part E uses the designer exception: Greek Fire targets only the shooter's own board.

The live rule description is [BattleshipGDD.md](../BattleshipGDD.md); hub/DTO and client contracts are in [WEB-BACKEND.md](WEB-BACKEND.md) §4/§10 and [WEB-CLIENT.md](WEB-CLIENT.md) §12.

## Implementation order after approval

1. Final Boarding and victory ordering: `D03–D05`, `I01–I02`.
2. Dead-deck resolution, special shots and CAPTURE: `D01`, `D06–D10`, `I03–I05`.
3. Poison, movement, summons and effect precedence: `D14–D16`, `I06`, `I11–I15`.
4. Reveal, DTOs, modules and weapon identity: `I07–I09`, `I16`, `I19`.
5. Fleet validation and dormant paid mechanics: `D11`, `D13`, `I10`, `X02`.
6. Setup and bot integrity: `I17–I18`.
7. UI assets and dormant-code cleanup: `I20`, Part D.

After implementation, update affected Battleship documentation, increment `GameVersion`, build backend and frontend, and run targeted Battleship play tests.
