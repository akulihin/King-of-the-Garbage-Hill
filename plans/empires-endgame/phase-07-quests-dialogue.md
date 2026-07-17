# Phase 7 — Quest and dialogue engine

Use this file as the opening instruction of a fresh Codex task running **5.6 Sol** on the
designer's machine. This phase is one change-set. Do not implement a later phase while doing
this work.

## Executor rules (binding)

1. **Design source discipline**: read the phase's named raw export channels in `DiscordExports/Empires_Endgame/` before implementing. On conflict: main export > `ZBS MAKING` (outdated background); `empire_prompt` defines the core loop. Export missing from the environment → stop and tell the user; do not guess.
2. **Never fabricate silently**: a mechanic without export numbers → implement with a configurable default in `game-config.json` + append a ledger entry. A mechanic whose *semantics* are undefined → keep/add `deferredReason` and add a designer question to the ledger.
3. **Un-deferral discipline**: substrate + un-deferral (delete `deferredReason`, add executable effects) + `EMPIRES_LIVE_FLAG_ALLOWLIST` additions or typed payloads + tests, all in ONE change-set. `validateLiveEffects` must keep rejecting flags nothing reads.
4. **Determinism**: no `Date.now`/`Math.random` in any simulation — only the serialized RNG streams (`features/empires-endgame/rng.ts`). Minigames replay from `(plan, seed, commandLog)`; mid-minigame real-time state is never serialized.
5. Player-facing card/passive *texts* stay deliberately vague (repo philosophy — never "fix" them; the designer writes new player wording). Exact mechanics are documented in `docs/WEB-CLIENT.md` §12B.
6. Git: do NOT commit or push. Write the commit message to `docs/commit-messages/<date>.md` (one file per change-set; `-2`, `-3` suffixes for further change-sets the same day).

## Codex preflight and prerequisite check

1. Read repository `AGENTS.md`, `plans/empires-endgame/README.md`, this entire prompt,
   `docs/WEB-CLIENT.md` §12B, and the quest/event/state sections of
   `docs/ARCHITECTURE.md` before editing. Run `git status --short`; preserve every unrelated
   change and never reset the worktree.
2. Verify Phase 0 from actual code and tests, not filenames: `migrateEmpiresConfig` exists,
   the bundled config is on the current schema, old config fixtures migrate, old saves
   normalize `quests` to an empty record, the design-review ledger exists, and the Empire
   test script discovers new specs. Re-locate symbols if line anchors drifted.
3. Inspect the current config and save schema versions after all earlier work. This phase
   advances config **current → next sequential version**; do not hard-code the obsolete
   planned value `3`, reuse an old version, or skip a migration link. Do not bump the save
   envelope merely for additive normalized fields.
4. Confirm that `event-golden-idol` and `event-witch-apprenticeship` still exist and are
   deferred. Inventory every authored effect and prerequisite before changing either one.
   If earlier phases changed their ids or semantics, stop and report the concrete drift.
5. This phase formally depends on Phase 0, but the two event carriers also touch systems
   that may have arrived in Phases 2/4/5. Verify army/fallen-warrior, typed loyalty,
   population targeting, and disease/epidemic capabilities before promising those effects.
   Missing substrate must be implemented as a typed quest resolution in this change-set or
   treated as a blocking design gap; never make an unread flag live.
6. Use a live Codex plan. Sub-agents may do bounded source, migration, and test audits, but
   keep one owner for `engine.ts`, `types.ts`, `config.ts`, and `game-config.json`.

## Mission

Ship a config-driven quest and dialogue-graph engine, deterministic trigger evaluation,
persisted quest progress, an accessible dialogue overlay and quest journal, and a Builder
graph editor. Port the authored Палач quest from its local HTML demonstrations. Convert the
Золотой идол and Ученики болотных ведьм events into fully executable event/quest flows and
only then remove their `deferredReason` values. The campaign must save, reload, autoplay,
and resume at any dialogue node without duplicating costs or effects.

## Design source (read before implementation)

Read all of the following on the designer's machine:

- `DiscordExports/empire_prompt` for the Durak → gift → empire loop and where quest
  interruptions may occur.
- Every main-export file for channel `квесты` under
  `DiscordExports/Empires_Endgame/`, including cross-linked messages in `события`, `лор`,
  and `общее` for the quests implemented here.
- Every `DiscordExports/Empires_Endgame/Palach*.html` interactive demo and its adjacent
  assets/data. If the export manifest places the demos in another subdirectory beneath
  `DiscordExports/`, follow the manifest and record the exact source paths in the ledger.
- The raw entries for Золотой идол and Ученики болотных ведьм, not just their compressed
  config descriptions. The main export wins over `ZBS MAKING`.

Compressed mechanics, for navigation only: quests have triggers, ordered stages, dialogue
nodes, and choices with requirements, costs, effects, and a next-node/stage/terminal target.
Triggers are checked when an empire phase starts and after a minigame resolves. Progress and
quest-local memory survive reloads. Палач already has interactive HTML demonstrations that
must be ported rather than re-authored. Other named quest families remain future content
unless the raw export makes them a required dependency of this slice.

If any named export channel, the Палач demos, or the authoritative event messages are
missing, **stop before editing and tell the user**. Do not implement from this compression,
the existing vague event descriptions, or memory.

## Repo anchors (read before editing; re-locate by symbol)

- `Web/VueClient/src/features/empires-endgame/types.ts`: `EmpiresEffect`,
  `EmpiresEventChoiceDefinition`, `EmpiresEventDefinition`, `EmpiresCampaignState`, and the
  Phase-0 `quests` state field.
- `Web/VueClient/src/features/empires-endgame/engine.ts`: `startEmpirePhase` (baseline
  `:1193`), Phase-2 `resolveMinigame`, `firstMissingDependency` (baseline `:1477`), event
  choice resolution around baseline `:667`, `eventIsEligible`, and
  `validateAndCloneSnapshot` (baseline `:1002`). Extend these paths; do not create a second
  campaign resolver.
- `Web/VueClient/src/features/empires-endgame/config.ts`: `migrateEmpiresConfig`, quest/event
  validators, `validateDeferredReasons`, `EMPIRES_LIVE_FLAG_ALLOWLIST`, and
  `validateLiveEffects`.
- `Web/VueClient/public/empires-endgame/game-config.json`: current `quests` section plus
  `event-golden-idol` (baseline around `:8207`) and `event-witch-apprenticeship` (baseline
  around `:8516`).
- `Web/VueClient/src/features/empires-endgame/qa.ts`: `digestEmpiresQaState`, fixture
  construction, scripted actions, trace/stall detection, and full-campaign autoplay.
- `Web/VueClient/src/components/empires-endgame/TechTree.vue`: authored node positions,
  edges, drag/pan, and editable graph conventions to reuse.
- `Web/VueClient/src/components/empires-endgame/EventDialog.vue`,
  `TargetResolutionDialog.vue`, `BuilderDrawer.vue`, and
  `Web/VueClient/src/pages/EmpiresEndgame.vue`: existing modal, target-resolution, Builder,
  and page integration patterns.

## Work items (in order)

1. **Typed config graph.** Make top-level `quests` an array of definitions shaped along
   these lines, refining names only when the raw source requires it:

   ```ts
   interface EmpiresQuestDefinition {
     id: string
     title: string
     trigger: EmpiresQuestTrigger
     initialStageId: string
     stages: EmpiresQuestStageDefinition[]
     repeatable?: boolean
     deferredReason?: string
   }
   interface EmpiresQuestStageDefinition {
     id: string
     entryNodeId: string
     nodes: EmpiresDialogueNodeDefinition[]
   }
   interface EmpiresDialogueNodeDefinition {
     id: string
     speaker: string
     text: string
     choices: Array<{
       id: string
       label: string
       requires: EmpiresDependency[]
       costs: EmpiresQuestCost[]
       effects: EmpiresQuestEffect[]
       goto: { kind: 'node' | 'stage' | 'complete' | 'fail', id?: string }
     }>
   }
   ```

   Reuse existing dependency/resource/effect types when their semantics match. Add typed
   quest effects for quest memory, target selection, quest transitions, delayed
   consequences, loyalty, disease, or army restoration; do not encode lifecycle state as
   anonymous flags. Validate unique ids, all graph references, entry nodes, terminal paths,
   known dependency/effect targets, and intentionally allowed cycles.
2. **Sequential config migration.** Add exactly one current→next migration link and update
   the bundled schema. Preserve valid existing quest arrays; backfill a structurally valid
   empty array for older custom configs without overwriting their data. Add previous-version
   bundled/custom fixtures and prove parse → migrate → validate → export/import round trips.
3. **Persisted runtime state.** Use first-class campaign state, not flags:

   ```ts
   type EmpiresQuestStatus = 'inactive' | 'active' | 'completed' | 'failed'
   interface EmpiresQuestRuntimeState {
     stageId: string
     nodeId: string | null
     status: EmpiresQuestStatus
     memory: Record<string, string | number | boolean | null>
   }
   quests: Record<string, EmpiresQuestRuntimeState>
   activeDialogue: { questId: string, nodeId: string } | null
   ```

   Normalize missing legacy fields to `{}`/`null`, validate ids against config, and decide
   explicitly how removed or deferred definitions are handled. A save at a node must reload
   at that node. Applying a choice is one atomic engine commit so refresh/retry cannot charge
   or apply it twice.
4. **`features/empires-endgame/quests.ts`.** Implement pure, deterministic helpers for
   trigger matching, graph lookup, choice availability, and transition validation. Implement
   `evaluateQuestTriggers(context)` through the campaign engine at `startEmpirePhase` and
   immediately after successful `resolveMinigame`; stable config/id order breaks ties. Any
   randomized authored trigger uses a serialized RNG stream and records its result.
5. **Dialogue actions.** Add engine methods such as `advanceDialogue(questId, nodeId,
   choiceId, target?)`. Recheck requirements and costs server-style inside the engine, route
   dependency checks through `firstMissingDependency`, apply typed effects once, update
   memory/status/goto, and prevent unrelated campaign actions while a mandatory dialogue is
   active. Optional journal viewing must not mutate state.
6. **Port Палач.** Transcribe its graph, exact text, conditions, targets, and consequences
   from every `Palach*.html` demo into config. Preserve authored Russian strings verbatim.
   Record demo-to-node traceability in the design-review ledger or nearby config metadata;
   do not embed or execute the HTML at runtime.
7. **Event bridge and un-deferral.** Preserve each event's authored choice labels and
   immediate resource effects. Replace unsupported placeholder flags with typed quest
   starts/resolutions that implement every raw consequence. Золотой идол must cover all
   authored monument/sell/destroy branches, including target selection and delayed
   consequences. Ученики болотных ведьм must cover send/refuse, population targeting/loss,
   alchemy progress, and typed regional loyalty. If the raw export does not define a needed
   semantic, stop and ask instead of inventing it.
8. **UI.** Add `src/components/empires-endgame/DialogueOverlay.vue` with focus trap,
   keyboard operation, requirements/cost preview, target-resolution continuation, and no
   unsafe dismissal; add `QuestJournal.vue` for active/completed/failed quests. Integrate
   both in `EmpiresEndgame.vue` without duplicating engine state.
9. **Builder.** Add a quests section to `BuilderDrawer.vue`, reusing the `TechTree.vue`
   canvas/node-position model for stages and dialogue edges. JSON import/export must retain
   every node, edge, condition, effect, and position and run the same validator as bundled
   config loading.

## Un-deferral list (exact)

- `event-golden-idol` — remove its `deferredReason` only after every live choice has a typed,
  consumed quest/resolution payload and focused tests. Do not merely allowlist
  `goldenIdolMonument`, `fallenWarriorRestored`, `bloodyDisease`, `enemyIdolRisk`,
  `nationalUnity`, `churchUnity`, or `goldenIdolDestroyed`.
- `event-witch-apprenticeship` — remove its `deferredReason` only after send/refuse choices
  execute their population, knowledge/alchemy, and typed loyalty consequences. Do not make
  `swampAlchemy` or `loyaltyEast` an unread live flag.
- No other existing carrier is promised by this phase. New Палач quest definitions may be
  live because this phase supplies their complete substrate. Any other imported quest with
  undefined semantics retains `deferredReason` and gets a ledger question.

## Verification

- Add `features/empires-endgame/quests.spec.ts`: graph validation; deterministic trigger
  ordering; requirement/cost rejection; each goto kind; target continuation; exact-once
  effects; completion/failure; save/reload at a dialogue node; invalid legacy/orphan state;
  and both un-deferred events through every choice.
- Add QA scenario `quest-dialogue` and QA action `advance-dialogue`. Extend the campaign
  digest with quest/dialogue state and prove autoplay reaches and resolves quests without a
  repeated digest or stall. The scripted policy must not bypass engine validation.
- Add `cypress/e2e/empires-endgame-quests.cy.ts`, wire it into `test:empires:e2e`, load
  `?qa=1&scenario=quest-dialogue&seed=...`, assert overlay focus/choice state, advance via QA,
  reload mid-node, and assert journal completion. Do not rely on timing.
- Run `pnpm --dir Web/VueClient run test:empires`; confirm the new spec is actually
  discovered. Run `bash tools/test-empires-endgame.sh` and keep all Vitest + Cypress suites
  green.
- Run `pnpm --dir Web/VueClient build` — not the broken environment-wide `type-check`.
- Run `bash tools/verify-docs.sh --changed`.
- Update `docs/WEB-CLIENT.md` §12B for every changed behavior; catalogue any newly found bug
  in `docs/AUDIT-FINDINGS.md` with the next free ID.
- Append every invented number to `docs/EMPIRES-ENDGAME-DESIGN-REVIEW.md`, keyed by JSON
  pointer. A config/schema bump must include a previous-version restore/import spec.

## Docs & ledger contract

- Document the quest config graph, runtime state, trigger order, dialogue atomicity, Builder
  surface, Палач port, event bridges, QA scenario/action, and exact un-deferral list in
  `docs/WEB-CLIENT.md` §12B. Update other docs only when their documented contract changed.
- Ledger every invented trigger threshold, target rule, graph fallback, timeout, ordering
  choice, or value. Include the exact raw export/HTML source for each Палач node. Do not use
  the ledger to excuse undefined semantics.
- Increment the patch component of `GameVersion` sequentially in
  `King-of-the-Garbage-Hill/Game/Classes/GameClass.cs` after the implementation and docs are
  complete.
- Write the proposed commit message to `docs/commit-messages/<date>.md` (or the next `-2`,
  `-3` suffix). Do not stage, commit, push, reset, or discard files.

## Designer questions

- Which exact Палач HTML demo/version is canonical when the demos disagree, and which quest
  endings are terminal versus hooks for later content?
- For Золотой идол, how is the fallen warrior selected, which living character receives the
  bloody disease, how long does it last, and when/how may a sold idol strengthen an enemy?
- What exact systems do `nationalUnity` and `churchUnity` modify, rather than merely record?
- For Ученики болотных ведьм, which city supplies apprentices, how is the lost population
  selected, and is `swampAlchemy` quest memory, research progress, or an alchemy unlock?
- May multiple mandatory dialogues queue at the same trigger point; if so, what is the
  authored priority and can the player postpone any of them?
- Which quest-local memory values are visible in the journal, and may completed/failed quests
  be restarted in the same campaign?
