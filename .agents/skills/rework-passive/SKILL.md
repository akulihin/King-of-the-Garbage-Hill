---
name: rework-passive
description: Change how an existing passive/ability works (mechanics rework per designer intent) — e.g. "переделай Огурчик Рик так, чтобы…", "rework Butcher's marks", implementing a *_update intent note. Not for bugs (/fix-finding), brand-new passives (/new-character) or pure number tweaks (/balance).
---

# Rework an existing passive

**Prime rule: docs are the map, anchors are the territory — never edit a hook/case you haven't read in full.**

## Spec

The designer's intent is the spec: a root-level `*_update` note, `Game/GameDesign.txt` `[[mechanics]]` blocks, or the prompt itself. Where intent contradicts current code/docs, **intent wins**. Ambiguities → ask before implementing, don't guess design.

## Before editing — load the passive's whole world

1. `docs/CHARACTERS.md` entry (current actual behavior + anchors) and the character's rows in `docs/INTERACTION-MATRIX.md` / `docs/BALANCE-CONSTANTS.md`.
2. **Grep the exact passive name AND character `Name`** across `King-of-the-Garbage-Hill/Game`, `API`, `Web/VueClient/src` — read every hit in full: all `CharacterPassives.cs` cases, state class, `PassivesClass` fields, `DoomsdayMachine`/`CheckIfReady`/`GameReactions` special-casing, **`BotsBehavior` (bots often encode the OLD behavior)**, `GameUpdateMess` icons, mapper/DTO/TS/widget, `CharactersPhrases`.
3. `docs/AUDIT-FINDINGS.md`: open findings on this passive. A rework may fix, obsolete, or silently *preserve* them — decide each explicitly (fixing → mark `**Fixed:**`; obsoleted → note it; preserved bug the designer didn't mention → flag it in your summary).

## Implement

- Choose hooks by `docs/ARCHITECTURE.md` §3 execution order; respect all AGENTS.md correctness rules (FightCharacter/ForOneFight, MinusPsycheLog, buffered vs bonus points, ban carve-outs, transferred-passive immunity).
- `characters.json`: mechanical fields only (`Visible`, `Standalone`, stats). **Descriptions are untouched** — if the rework needs new player text, the designer supplies exact wording; `PassiveName` renames are allowed but ripple everywhere (exact strings + `tools/known-warnings.txt` + web) — the audit script is your safety net.
- **Leftover sweep (reworks' #1 failure mode)**: after the change, re-grep the passive — no orphaned state fields in `PassivesClass`/the state class, no stale phrases, no bot logic or widget still assuming the old behavior, no dead `case` blocks. Delete or update; don't leave a second Saldorum.

## Docs in the same change (mandatory)

Rewrite the `docs/CHARACTERS.md` entry (new behavior, fresh anchors); update `docs/INTERACTION-MATRIX.md` rows (add/remove per new capabilities) and `docs/BALANCE-CONSTANTS.md` rows; settle affected findings in `docs/AUDIT-FINDINGS.md`; system-level side effects → `docs/GAME-DESIGN.md`/`ARCHITECTURE.md`.

## Verify

`dotnet build` + `pnpm build` (if web touched) + `bash tools/audit-passives.sh` (clean; renames reflected in `tools/known-warnings.txt`) + `bash tools/verify-docs.sh --changed`; a targeted play-test plan (which rounds/actions exercise the new behavior, including the interactions from the matrix rows). Commit message to `docs/commit-messages/<date>.md`; never `git commit`.
