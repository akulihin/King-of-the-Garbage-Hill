---
name: new-character
description: Implement a NEW King of the Garbage Hill character, or add a brand-new passive/widget to an existing one, following the verified 14-file pattern with docs updated in the same change. Use for "add character X", "new passive/widget". Changing how an EXISTING passive works is /rework-passive.
---

# New character / passive / widget

**Prime rule: docs are the map, anchors are the territory — never edit a hook/case you haven't read in full.**

## Preparation (read before writing anything)

1. `docs/GAME-DESIGN.md` — the systems the new kit touches (fight math §4, score §6, Вред §7, pipeline §8; forced fights / kills / swaps have established carve-out patterns).
2. `docs/ARCHITECTURE.md` §3 (hook execution order + which hook fires when) and §7 (the 14-file per-character pattern).
3. Pick **1–2 reference characters** with similar mechanics from `docs/CHARACTERS.md` and read their **actual code** end to end (state class → PassivesClass fields → every CP case → DTO → mapper → signalr.ts → PlayerCard widget). They are the living template; copy their shapes, not fragments from memory.
4. `docs/INTERACTION-MATRIX.md` — every table the new kit will touch (does it force fights? kill? move positions? steal/copy? intercept moral/psyche/Harm? is anything `Standalone: true` and therefore Ziggurat-copyable?).
5. If the designer wrote intent notes (root-level `*_update` files or `Game/GameDesign.txt`), treat them as the spec alongside `characters.json` descriptions.

## Implementation checklist (ARCHITECTURE.md §7, verified order)

1. `DataBase/characters.json` — exact-string `PassiveName`s (they are identifiers), `Visible`, `Standalone` only if safe to Ziggurat-copy. `PassiveDescription`s are the **designer's voice**: use their wording from the intent notes verbatim; keep them vague/flavorful — never write technical spec into them (that goes in `docs/CHARACTERS.md`).
2. `Game/Characters/<Name>.cs` — state classes.
3. `Game/Classes/PassivesClass.cs` — owner state + per-player marks (follow the SellerMark naming pattern).
4. `Game/MemoryStorage/CharactersPhrases.cs` — phrases.
5. `Game/GameLogic/CharacterPassives.cs` — `case "…":` per hook; add immunity checks for transferred passives; use the round-10 ban carve-out pattern (`CheckIfReady.cs:1270`) for any forced action.
6–9. Only if needed: `DoomsdayMachine.cs` (core fight mechanics), `CheckIfReady.cs` (turn injection), `GameReactions.cs` (level-up override), `BotsBehavior.cs` (bot moral + targeting + the Name-keyed cases — use the **exact** JSON Name).
10. `GameUpdateMess.cs` — leaderboard icons.
11–14. Web: DTO (`GameStateDto.cs`), mapper (`GameStateMapper.cs` — gate by character Name if the passive name is shared), `signalr.ts`, `PlayerCard.vue`/`SkillsPanel.vue` (+ `sound.ts` if audio; compare the Name against the **exact JSON string** — see finding m22 for how that goes wrong).

## Docs in the same change (mandatory)

- `docs/CHARACTERS.md` — full entry with file:line anchors, ⚠ on any known deviation.
- `docs/INTERACTION-MATRIX.md` — a row in every applicable table.
- `docs/BALANCE-CONSTANTS.md` — every tunable number.
- `bash tools/audit-passives.sh` — every new passive must appear as `ok` (no ORPHAN unless intentionally name-keyed; then whitelist with a comment), zero new GHOST/BAD-NAME.

## Verify

`dotnet build` + `pnpm build` (type-check is broken in this environment) + `bash tools/audit-passives.sh` + `bash tools/verify-docs.sh --changed`; then a targeted play-test plan (which rounds/actions exercise each passive). Write the commit message to `docs/commit-messages/<date>.md` (`-2`, `-3` on same-day collisions); never `git commit`.
