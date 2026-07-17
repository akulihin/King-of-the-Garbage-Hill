# Phase 12 — Chess (experimental sketch behind a disabled config toggle)

You are executing Phase 12 of the Empire's Endgame completion program with Codex 5.6
Sol. Read `AGENTS.md` and `plans/empires-endgame/README.md` first. This is one
implementation change-set. The hard architectural prerequisite is Phase 2's shared
minigame envelope; because this phase is normally executed last, it must also preserve the
post-Phase-9 card/mystic schema actually present in the working tree.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex 5.6 Sol preflight and dependency audit

Before editing:

1. Run `git status --short` and preserve all unrelated changes. Read this prompt, the
   README, `docs/WEB-CLIENT.md` §12B, and every raw source named below. Maintain a live
   task plan; coordinate ownership of shared config/state/engine/page files.
2. Prove the shared minigame envelope is real and tested: locate its plan/result/session
   discriminants, campaign `'minigame'` phase, begin/resolve/abort paths, deterministic
   replay convention, reload `attempt + 1`, QA resolver, and save normalization.
3. Audit the **actual current** card schema after prior phases: suits/ranks (including
   Phase-9 mystic/`none` if present), card instances, stable hand/deck order, inverted
   faces, upgrades, missing/deferred placeholder faces, and config validation. Chess must
   not regress Durak legality, 53-card/mystic validation, or card ordering.
4. Audit current config/save migration version, Builder patterns, QA scenario/action
   catalogs, Cypress wiring, review ledger, and `GameVersion`. Search for any existing
   partial `chess` code/config before creating files; preserve useful compatible work.
5. Confirm the raw `шахматы` source defines enough semantics for each behavior you intend
   to expose. If it remains a sketch, implement only a disabled, validated experimental
   framework and pure rules that are genuinely defined. Do not label it complete or turn
   it on by default.
6. If the envelope is missing or the required raw source is absent, **stop before code
   edits and report the exact gap**. Do not invent standard-chess rules as a substitute.

## Mission

Add a deterministic, pure cards-as-pieces chess experiment through the shared minigame
envelope and a new `features/empires-endgame/chess/` module, with Builder/config support,
an accessible board UI, replay tests, and QA fast resolution. `chess.enabled` must default
to `false`. Only rules explicitly established by the raw export may become executable;
unresolved movement, ownership, victory, AI, reward, or penalty semantics stay disabled /
deferred and are documented as designer questions. This phase must not claim the designer's
sketch is a finished chess design.

## Design source — read before implementing

Resolve and read on the designer's machine:

- `DiscordExports/empire_prompt` for the core campaign/minigame loop.
- `DiscordExports/Empires_Endgame/` channel `шахматы` in full — the authoritative sketch.
- Cross-references in `карты`, `персонажи`, and `общее` for exact card/character identity,
  ownership, inverted behavior, rewards, and God interaction. If `шахматы` links a board
  sketch/image, inspect the actual asset.
- `ZBS MAKING` may explain old intent but loses to the main export on conflict.

If the export or `шахматы` channel is missing, **stop and tell the user before editing**.
The compressed notes establish only these leads:

- Important cards act as pieces. “Казна/Чистые улицы” are described as rooks; family
  members are pieces; Антон is a knight controlled by both sides once every two turns;
  the enemy has no king.
- Those phrases do not define a board size/setup, complete card-to-piece mapping, movement,
  check/checkmate, capture, shared-control timing, victory/loss/draw, entry trigger, reward,
  AI, or abort penalty.
- In the baseline config, `card-clubs-2` is Чистые улицы; “Казна” may refer to something
  other than a card; and the README associates Антон with В♠ while the baseline
  `card-spades-jack` face is a generic placeholder. Confirm the post-Phase-9 IDs from the
  raw export and current config—never dispatch on display names or assume these candidates.

## Repo anchors — inspect before editing

- `Web/VueClient/public/empires-endgame/game-config.json`: current card IDs/faces, config
  schema, and absence/presence of a `chess` section.
- `features/empires-endgame/types.ts`: card/suit/rank definitions, card instances,
  minigame unions, campaign state, and config root.
- `features/empires-endgame/config.ts`: migration chain, card catalog validation,
  `validateDeferredReasons`, and structural/reference validation.
- `features/empires-endgame/engine.ts` or extracted modules: begin/resolve/abort minigame,
  reward settlement, restore normalization, and card lookup. Chess rules themselves belong
  outside this campaign engine.
- `features/empires-endgame/rng.ts` and `qa.ts`: serialized RNG, scripted policies, digest,
  trace/stall loop, and QA fast resolution.
- `src/pages/EmpiresEndgame.vue`, `src/components/empires-endgame/BuilderDrawer.vue`, and
  existing minigame components for routing/HUD/accessibility conventions.
- `persistence.ts`, package scripts, `tools/test-empires-endgame.sh`,
  `docs/WEB-CLIENT.md` §12B, `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, and
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`.

Re-locate symbols if earlier phases moved them.

## Work items (in order)

1. **Turn the raw sketch into a bounded rules table.** List every sourced piece mapping,
   board/setup rule, controller, movement/capture rule, special cadence, turn rule,
   victory/loss/draw condition, entry trigger, reward, failure, abort, and AI behavior.
   Mark each missing semantic explicitly. A familiar chess convention is not evidence.

2. **Add a disabled typed config section and migration.** Advance the current config schema
   one step only if required; do not assume a version number. A target shape is:

   ```ts
   interface EmpiresChessConfig {
     enabled: boolean
     board: EmpiresChessBoardConfig
     pieceMappings: EmpiresChessPieceMapping[]
     moveRules: EmpiresChessMoveRule[]
     turnRules: EmpiresChessTurnRules
     victory: EmpiresChessVictoryConfig
     opponent: EmpiresChessOpponentConfig
     entryPoints: EmpiresChessEntryPoint[]
     deferredReason?: string
   }

   interface EmpiresChessPieceMapping {
     id: string
     cardDefinitionId: string
     roleId: string
     initialOwner: 'player' | 'god' | 'shared'
     moveRuleId: string
     controlPeriodTurns?: number
     deferredReason?: string
   }
   ```

   The bundled config must set `chess.enabled: false`. Validate unique IDs, board bounds,
   complete start squares, known card IDs/move rules/controllers, positive cadence/caps,
   and an executable victory rule before an imported config may enable chess. Migrate the
   previous config to the disabled section. Undefined mappings/rules carry non-empty
   `deferredReason` rather than placeholder behavior.

3. **Create `features/empires-endgame/chess/`.** Add `types.ts`, `rules.ts`, `engine.ts`,
   `replay.ts`, policies, and specs. Expose pure functions such as `createChessState`,
   `legalCommands`, `applyChessCommand`, and
   `resolveChess(plan, seed, commandLog)`. A command either deterministically produces the
   next immutable state or a typed rejection. Stable-sort legal moves and AI ties. Use
   `rng.ts` only if the raw rules genuinely require randomness; otherwise the seed is inert.

4. **Do not smuggle in standard chess.** Movement, capture, checking, promotion, turns, and
   winning are config/rule-table driven. “Enemy has no king” requires an authored alternative
   victory condition; never silently reinterpret checkmate as eliminating all pieces. The
   Антон/shared-control cadence is implemented only after exact ownership and “once every
   two turns” timing are established.

5. **Reuse the shared minigame envelope.** Add the chess plan/result arms and route entry,
   canonical replay validation, reward/penalty settlement, reload attempts, and abort through
   existing engine methods. Do not serialize the board separately: reconstruct it from
   plan/seed/commandLog. If a command log is intentionally not persisted mid-session under
   the shared contract, reload restarts from plan/seed with `attempt + 1`.

6. **Keep campaign effects typed and one-shot.** A resolved result may carry only authored
   rewards/consequences through existing typed effect/resolution funnels. Do not toggle or
   invert campaign cards merely because they served as pieces unless the export explicitly
   says so. Guard duplicate resolution by session/revision identity.

7. **Build `src/components/empires-endgame/ChessMinigame.vue`.** Render a keyboard- and
   pointer-operable board, selected piece/legal destinations, controller/turn, sourced
   special cadence, outcome, and abort confirmation. Do not show an entry point when the
   bundled toggle is off. Under `?qa=1`, expose a fast-resolve action using a scripted policy;
   Cypress must never play a full board manually.

8. **Builder support.** Add a clearly experimental Chess section showing the disabled
   toggle, board, mappings, move/turn/victory rules, unresolved `deferredReason`s, and
   reference errors. Prevent enabling/saving an internally incomplete ruleset. Preserve
   vague player-facing card text.

9. **QA and termination.** Add a deterministic scripted resolver with a configured
   move/repetition cap. Repetition or cap exhaustion yields a typed draw/inconclusive result
   only if the raw/config contract defines it; it must never hang or silently award a win.
   Add a QA scenario using a complete test-only fixture even while the bundled config remains
   off.

10. **Migrate/backfill saves only as needed.** The board is not first-class saved campaign
    state. Normalize any new campaign summary/result field conservatively; bump the save
    envelope only for semantic change and restore the immediately previous fixture.

11. **Final program reconciliation.** Re-run a config-wide deferred inventory after all
    phases. For every remaining marker—especially generic card faces—record its exact ID/
    side, owning source channel, missing semantic or substrate, and designer question in
    the append-only ledger. If a prior phase accidentally left a now-complete carrier
    deferred, report it as a follow-up change-set rather than smuggling non-Chess work into
    P12. Never remove a marker merely to make the completion count smaller.

## Un-deferral list

**Guaranteed existing carriers to un-defer in Phase 12: none.** There is no current chess
config carrier, and the named card candidates must not have their existing face
`deferredReason`s removed merely to use stable definition IDs as chess pieces. The bundled
`chess.enabled` toggle remains `false`.

Never delete unrelated card, building, technology, event, gift, unit, or relic markers.
New unresolved chess mappings/rules receive their own `deferredReason`. If the raw source
requires an existing deferred card face's campaign passive to become live, that is a
separate substrate/un-deferral task unless this phase also implements its complete campaign
effect, allowlist/typed payload, UI, and tests; do not conflate piece identity with passive
execution.

## Verification

- Config migration/validation: previous config loads with `chess.enabled === false`;
  duplicate/dangling mappings, bad squares/cadences, incomplete victory rules, and attempts
  to enable an incomplete ruleset fail clearly.
- Pure rule-table tests for every movement/capture/ownership/turn/victory behavior actually
  sourced from `шахматы`; no untested conventional-chess branch.
- Replay determinism: identical `(plan, seed, commandLog)` produces the same state/result
  digest; illegal/stale commands reject identically; no `Date.now`/`Math.random`.
- Shared-control/cadence boundary tests for Антон only if confirmed; enemy-without-king
  victory tests use the authored alternative condition.
- Termination tests for three seeds/policies under the configured move/repetition cap; no
  policy can stall the campaign loop.
- Envelope/save tests for entry, resolve-once, abort penalty, reload `attempt + 1`, and
  previous-version restore.
- QA scenario `chess` (or catalog-consistent name) uses a complete test fixture and fast
  scripted resolution. Cypress proves the bundled toggle hides entry, then enables a test
  fixture to assert board/controller/outcome through QA fast-resolve. Wire new specs.
- Full-campaign autoplay remains green both with bundled chess disabled and with the
  complete QA fixture enabled.
- Deferred-inventory reconciliation accounts for every remaining marker and proves no
  carrier is both executable and silently deferred; generic/undefined faces remain
  explicitly ledgered rather than guessed.

Complete every standing gate:

- `bash tools/test-empires-endgame.sh` green (Vitest + Cypress); wire every new spec into
  `Web/VueClient/package.json` `test:empires` / `test:empires:e2e` based on the actual
  Phase-0 test-script state.
- `pnpm --dir Web/VueClient build` green; do **not** use the broken `type-check` command.
- `bash tools/verify-docs.sh --changed` green.
- Update `docs/WEB-CLIENT.md` §12B; catalogue new bugs in `docs/AUDIT-FINDINGS.md` with the
  next free ID.
- Append every invented number to `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, keyed by exact
  JSON pointer.
- Every config/save schema bump includes a previous-version migration/restore fixture.
- Write `docs/commit-messages/<date>.md` or the next same-day suffix; no commit/push.

## Docs & ledger contract

- Document that Chess is experimental and disabled by default, exactly which raw rules are
  implemented, deterministic replay/QA, toggle validation, entry/reward/abort behavior,
  and every unresolved semantic in `docs/WEB-CLIENT.md` §12B. Do not call the design
  complete.
- Resolve ledger seed #12 only to the degree the designer/raw export answers it. Ledger
  every invented board dimension, setup, cadence, movement value, AI policy, repetition/
  move cap, reward, penalty, and QA limit by JSON pointer.
- Increment only the patch component of the current `GameVersion` in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs`, sequentially by one after the
  implementation is complete. Do not copy a target version from this prompt.
- The commit-message file summarizes the disabled experimental config, pure deterministic
  rules/replay, envelope/UI/Builder/QA integration, migrations, tests, docs, and version
  bump—without claiming finished chess semantics.

## Designer questions

1. What starts Chess, when can it occur, which campaign actor/opponent participates, and
   what are the win/loss/draw rewards and abort/reload penalties?
2. What are the board dimensions, coordinates, initial layout, turn order, and exact set of
   participating card definition IDs? How do inverted cards and upgrade levels affect pieces?
3. What exactly are “Казна”, “Чистые улицы”, and “семья” in the current card catalog, and
   which specific pieces/owners/move rules do they map to?
4. Is Антон exactly `card-spades-jack` after prior phases; what does shared control mean;
   which side controls him first; and does “once every two turns” count global turns,
   rounds, or that piece's activations?
5. Which movement, blocking, capture, check, promotion, special-move, and repetition rules
   exist? Which standard chess rules are explicitly excluded?
6. With no enemy king, what is the exact player victory condition? What causes player loss,
   draw, stalemate, or an inconclusive cap result?
7. How does the God choose moves; are ties random; what serialized RNG stream/policy and
   difficulty parameters apply?
8. Is the feature intended to remain experimental/off until a later redesign, or what exact
   completeness criteria authorize enabling it?

If the raw source does not answer a semantic question, leave that rule disabled/deferred,
keep the bundled toggle off, and state the blocker plainly rather than filling it with
ordinary chess conventions.
