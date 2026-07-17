# Phase 8 — God presence

Use this file as the opening instruction of a fresh Codex task running **5.6 Sol** on the
designer's machine. This phase is one change-set. Do not pull Tavern/mystic-card work from
Phase 9 into it.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight and prerequisite check

1. Read repository `AGENTS.md`, `plans/empires-endgame/README.md`, this prompt,
   `docs/WEB-CLIENT.md` §12B, and the Durak/state/persistence sections of
   `docs/ARCHITECTURE.md`. Run `git status --short` and preserve unrelated changes.
2. Verify Phase 0 from code/tests: the `god` config section migrates, old saves normalize
   `durak.godInterventions` to `0`, the config migration is a real chain, and prior-version
   fixtures pass. Inspect the actual current config/save schema numbers; never assume the
   original plan's number is still current.
3. Read all Durak hooks before editing: exact-name searches for `resolveBout`,
   `finishedCardGameWinner`, draw/discard/deck mutation, `beginDivineGift`, card improve/
   restore actions, snapshot restore, QA autoplay, and all UI call sites. Preserve deck
   orientation (`deck[0]` retained trump end; `pop()` next draw unless earlier phases changed
   and documented it).
4. Confirm the current engine has one serialized campaign RNG and determine whether earlier
   phases added named streams. Cosmetic God-line selection must never perturb gameplay RNG;
   add a dedicated serialized stream if needed. Anti-bito is gameplay and must record enough
   state to replay exactly.
5. Use a live Codex plan. Sub-agents may audit raw dialogue, deck invariants, and tests, but
   keep one owner for Durak engine/state mutations.

## Mission

Make the God present in the core card loop: gated deck-memory inspection, capped anti-bito
interventions that prevent an authored too-early ending without risking an infinite game,
config-driven God dialogue, and an accessible Божественная Милость confirmation with a
persisted “do not show again” preference. Every intervention is deterministic across replay
and save/restore. This phase un-defers no existing content carrier.

## Design source (read before implementation)

Read all of the following on the designer's machine:

- `DiscordExports/empire_prompt` for the canonical Durak/con/empire loop.
- Every main-export file for channels `общее` and `карты` beneath
  `DiscordExports/Empires_Endgame/`, including messages describing face-up shuffle/deck
  memory, anti-bito, God responses, and Божественная Милость confirmation.
- Any cross-linked God/Card messages in `персонажи`; use `ZBS MAKING` only as outdated
  background when the main export is silent, never over a main-export statement.

Compressed mechanics, for navigation only: a face-up shuffle lets Том remember the deck
order under an authored availability gate; anti-bito returns a configured number of random
discarded cards to the deck when the card game would end before an authored minimum;
`durak.godInterventions` caps that rescue so termination is guaranteed; God lines fire on
authored triggers; spending Божественная Милость asks for confirmation and offers “do not
show again.” The deck already lives in serialized `state.durak.deck`. Exact thresholds,
availability, insertion order, triggers, copy, and which action spends Милость come only
from the raw export.

If a named export channel or the exact Милость/anti-bito messages are missing, **stop before
editing and tell the user**. Do not infer them from this compression.

## Repo anchors (read before editing; re-locate by symbol)

- `Web/VueClient/src/features/empires-endgame/engine.ts`: `resolveBout` (baseline `:1122`),
  `finishedCardGameWinner`, `drawToHand`, `resolveTrumpSuit`, `beginDivineGift`, card
  `improveCard`/`restoreCard`, and `validateAndCloneSnapshot` (baseline `:1002`).
- `Web/VueClient/src/features/empires-endgame/types.ts`: `EmpiresDurakState` (deck/discard/
  hands), `EmpiresRngState`, `EmpiresCampaignState`, and the Phase-0
  `godInterventions` field/default.
- `Web/VueClient/src/features/empires-endgame/rng.ts`: serialized RNG primitives. Never use
  browser time or global randomness.
- `Web/VueClient/src/features/empires-endgame/config.ts` and
  `Web/VueClient/public/empires-endgame/game-config.json`: current migration chain and `god`
  section.
- `Web/VueClient/src/features/empires-endgame/qa.ts`: deck inspection helpers, digest,
  autoplay, fixture registry, and stall detector.
- `Web/VueClient/src/components/empires-endgame/DurakTable.vue` and
  `Web/VueClient/src/pages/EmpiresEndgame.vue`: deck rendering and council/card action call
  sites. Add `DeckMemoryPanel.vue` beside these components.
- `Web/VueClient/src/features/empires-endgame/persistence.ts`: campaign storage. UI
  preferences require a separate versioned key/module and do not belong in the snapshot.

## Work items (in order)

1. **Typed God config.** Expand the migrated `god` section with shapes equivalent to:

   ```ts
   interface EmpiresGodConfig {
     deckMemory: {
       enabled: boolean
       availability: EmpiresDeckMemoryAvailability
       inspectionsPerCon?: number
     }
     antiBito: {
       enabled: boolean
       minimumCons: number
       returnCount: number
       maxInterventions: number
       source: 'discard'
       insertion: EmpiresDeckInsertionRule
     }
     lines: EmpiresGodLineDefinition[]
     mercyConfirmation: { enabled: boolean, message: string }
   }
   interface EmpiresGodLineDefinition {
     id: string
     trigger: EmpiresGodLineTrigger
     text: string
     weight?: number
     once?: boolean
   }
   ```

   Use the raw export's exact gates and copy. Validate finite nonnegative thresholds,
   `maxInterventions`, trigger ids, positive line weights, and an insertion rule the engine
   actually implements. Every invented number is configurable and ledgered.
2. **Config compatibility.** Inspect the current schema after prerequisites. If this additive
   section requires a schema change, add only the next sequential migration and a previous-
   version fixture; otherwise extend the current migration/backfill so old bundled/custom
   configs receive safe God defaults. Never make an old migrated `{ god: {} }` fail the new
   validator, and never overwrite existing custom values.
3. **Deck-memory API.** Add pure engine queries `canInspectDeck()` and an immutable inspection
   projection. The gate—not component visibility alone—decides access. Inspection must show
   the authored orientation without mutating deck/RNG and must not reveal anything when
   denied. Add `src/components/empires-endgame/DeckMemoryPanel.vue` with accessible closed,
   denied, and available states.
4. **Capped deterministic anti-bito.** Implement one authoritative helper, for example
   `applyAntiBito()`, in the winner-resolution path. It may intervene only when every raw
   condition is true, before a would-be terminal result is committed. Select without
   replacement from eligible discard ids using a serialized gameplay RNG, insert in the
   authored deterministic orientation/order, increment `durak.godInterventions` exactly
   once, then resume normal play. It must never intervene when no eligible cards exist or
   when `godInterventions >= maxInterventions`; after the cap, winner resolution proceeds
   normally, proving termination. Do not recursively call bout resolution.
5. **Replay evidence and save compatibility.** Persist normalized fields sufficient to
   reproduce interventions, for example:

   ```ts
   godInterventionLog: Array<{
     con: number
     bout: number
     returnedCardIds: string[]
     insertion: EmpiresDeckInsertionRule
   }>
   godLineHistory: { shownLineIds: string[], triggerCounts: Record<string, number> }
   godDialogueRng: EmpiresRngState
   ```

   Refine to existing state conventions, but keep gameplay/cosmetic RNG independent.
   Normalize old saves to zero/empty/deterministically seeded values. A restore immediately
   before the intervention must produce the same returned ids, deck, RNG state, and winner.
6. **God dialogue.** Route authored engine triggers through one deterministic selector.
   Weighted choice uses only the serialized God-dialogue RNG; `once` and occurrence state
   are serialized. UI rendering never consumes RNG. Expose the selected line as transient
   presentation derived from committed state/history, with keyboard/screen-reader support
   and no effect on action availability.
7. **Милость confirmation.** Identify the exact action(s) from the raw export; do not assume
   it means `restoreCard` merely because that action spends points today. Wrap only those UI
   calls in a proper confirmation dialog. Persist
   `{ schemaVersion: 1, skipDivineMercyConfirmation: boolean }` under a dedicated,
   namespaced UI-preference localStorage key. It is device UI state, not campaign state,
   config, or replay input. Corrupt/unknown preference data falls back safely.
8. **Builder/config UI.** Expose the God config in the Builder with validation and JSON
   round-trip. Exact God lines are player-facing authored copy: preserve them verbatim and do
   not generate replacements.

## Un-deferral list (exact)

- None. This phase adds the God substrate and UI but removes no existing `deferredReason`.
- `card-joker-jester` inverted (`Мрачный Шут`) is only a conditional source-audit target:
  its current config explicitly says the empire mechanic is undefined. Un-defer it only if
  the authoritative `карты`/God export supplies an exact effect that this phase completely
  executes; otherwise assert that its marker remains and ledger the gap.
- Do not opportunistically un-defer cards, technologies, gifts, or events merely because a
  new God trigger can observe them. Undefined semantics stay deferred with a ledger question.

## Verification

- Add focused engine/config tests for every deck-memory gate, denied access, non-mutation,
  anti-bito threshold boundaries, empty discard, fewer-than-returnCount discard, exact cap,
  no recursive intervention, and guaranteed winner after cap.
- Determinism/replay test: restore the same pre-intervention snapshot twice and assert the
  same returned ids, insertion order, full state/RNG digest, and final outcome. Restore a
  legacy save without God fields and prove deterministic normalization.
- Test God-line trigger/weight/once behavior and prove cosmetic line rendering does not
  consume gameplay RNG. Test valid, corrupt, and future-version UI preference payloads and
  verify the preference is absent from campaign exports.
- Assert `card-joker-jester` inverted remains deferred unless an exact raw mechanic was
  ported with a typed consumer, cleanup, UI, and focused tests.
- Add QA scenario `anti-bito`; extend digest/trace data with intervention count/log and God
  line state. Full autoplay must terminate under a strict action cap for boundary seeds.
- Add `cypress/e2e/empires-endgame-god.cy.ts`, wire it into `test:empires:e2e`, and cover
  deck-memory denied/allowed states, deterministic QA anti-bito resolution, God line, and
  Милость confirmation plus “do not show again.” Do not play random bouts in Cypress.
- Run `pnpm --dir Web/VueClient run test:empires`; confirm new specs are discovered. Run
  `bash tools/test-empires-endgame.sh` with all Vitest + Cypress suites green.
- Run `pnpm --dir Web/VueClient build` — not the broken environment-wide `type-check`.
- Run `bash tools/verify-docs.sh --changed`.
- Update `docs/WEB-CLIENT.md` §12B; catalogue any new bug in `docs/AUDIT-FINDINGS.md` with the
  next free ID; ledger every invented number by JSON pointer. Any config/schema bump ships a
  previous-version import/restore spec.

## Docs & ledger contract

- Document deck-memory visibility, anti-bito ordering/cap/replay contract, God-line triggers
  and RNG isolation, UI preference storage, QA scenario, and the fact that no content was
  un-deferred in `docs/WEB-CLIENT.md` §12B.
- Ledger the anti-bito minimum, return count, cap, insertion rule, deck-memory availability/
  inspections, line weights, and any confirmation default not stated in the export.
- Increment the patch component of `GameVersion` sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` after implementation and docs are
  complete.
- Write the proposed commit message to `docs/commit-messages/<date>.md` (or next same-day
  suffix). Do not stage, commit, push, reset, or discard files.

## Designer questions

- Exactly what unlocks deck memory: always available, only after a face-up shuffle, a card/
  character state, or a limited number of inspections per con?
- What does anti-bito's “too quickly” threshold measure—bouts, cons, deck exhaustion, or
  campaign progress—and how many discarded cards return per intervention?
- Where in the deck are returned cards inserted, may inverted/mystic cards be eligible, and
  does the intervention belong to the God, the player, or both terminal outcomes?
- What is the hard maximum number of interventions, and does it reset per con or campaign?
- Which exact events/actions trigger each God line, may lines repeat, and may their selection
  ever influence gameplay?
- Which exact action is Божественная Милость, what confirmation text is canonical, and is
  “do not show again” a device-wide preference or reset with a new campaign?
- Does the authoritative export define an empire effect for inverted `card-joker-jester`,
  or is `Мрачный Шут` intentionally a permanent deferred/visual-only face?
