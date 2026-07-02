---
name: sync-docs
description: Implement manual edits made to docs/ into the game code — docs as the editing surface. Use for "apply the docs changes", "я поменял числа в BALANCE-CONSTANTS", "make the game match the docs", "implement what I changed in CHARACTERS.md".
---

# Apply manual docs/ edits to the game

The user edits `docs/` by hand as a way of specifying changes; this skill makes the code match. Direction matters: here the **doc is the spec and the code is wrong** — the opposite of normal verification.

## 1. Find what changed

- `git diff docs/` (working tree) is the primary source. If docs/ is untracked or the diff is empty, ask the user what they changed (or accept it from the prompt).
- Ignore generated/irrelevant paths: `docs/PASSIVE-MAP.md`, `docs/commit-messages/`.
- List every changed hunk back to the user in the summary — each one must end up as: implemented / already-matched / needs-clarification.

## 2. Route each change by type

- **`BALANCE-CONSTANTS.md` row (number change)** → follow the `/balance` procedure: the row's own anchor points at the code; read the whole case/method, check for sibling-case duplicates of the same constant, implement. Remember `characters.json` descriptions stay untouched (design-voice rule) — flag if the old number is spelled out there.
- **`CHARACTERS.md` behavior prose (mechanics change)** → follow the `/rework-passive` procedure: the edited sentence is the intent note; load the passive's whole world (all cases, bots, widgets), implement, run the leftover sweep.
- **`INTERACTION-MATRIX.md` cell (rule change, e.g. a missing carve-out marked as required)** → implement the rule at the referenced site; mirror the established patterns (e.g. the round-10 ban carve-out `CheckIfReady.cs:1270`).
- **`AUDIT-FINDINGS.md` / `DESIGNER-REVIEW.md` verdict edits** → that's `/fix-finding` territory: treat filled-in verdicts as go-signals for those IDs.
- **`GAME-DESIGN.md` / `ARCHITECTURE.md` edits** → system-level change; stop and confirm scope before touching the pipeline/fight math (these are the highest-blast-radius files).

## 3. Repair the ripples (docs must be true again afterwards)

After implementing: update the *other* docs that cite the same fact (a number lives in both BALANCE-CONSTANTS and the CHARACTERS entry); refresh anchors that shifted (`bash tools/verify-docs.sh --changed`); the user's edited text itself is kept — only made consistent, never reverted.

## 4. Verify

`dotnet build` (+ `pnpm build` if web touched) + `bash tools/audit-passives.sh` + `bash tools/verify-docs.sh --changed`. Commit message to `docs/commit-messages/<date>.md` listing each doc-edit → code-change pair; never `git commit`.

If a doc edit is ambiguous ("faster", "чуть реже") — ask for the concrete value; never guess numbers into the game.
