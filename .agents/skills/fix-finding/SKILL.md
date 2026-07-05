---
name: fix-finding
description: Fix any game bug — by audit finding ID ("fix M9 M11") OR from a free-form bug report ("Тигр получает халявные очки на старте"). Triages against docs/AUDIT-FINDINGS.md, catalogues new bugs as findings automatically, reads ALL related code before editing, updates docs in the same change, verifies, writes the commit message.
---

# Fix a game bug (by finding ID or free-form report)

**Prime rule: docs are the map, anchors are the territory — never edit a hook/case you haven't read in full.**

## Mode A — input is finding ID(s) (C*, M*, m*, D*)

Go straight to the Steps below. If a D-item is given, check `docs/DESIGNER-REVIEW.md` for the designer's verdict first — D-items are decisions, not bugs; without a verdict, stop and ask.

## Mode B — input is a free-form bug report (the normal case in future sessions)

Triage BEFORE deciding anything is a bug — the user never edits the catalogue manually; this skill maintains it:

1. **Check the catalogue**: grep `docs/AUDIT-FINDINGS.md` (and `docs/DESIGNER-REVIEW.md` verdicts) for the character/passive involved. Already catalogued → continue as Mode A with that ID. Catalogued as *intended* (designer verdict «ОК» or a D-item explanation) → report that back and STOP; don't "fix" design.
2. **Verify it's real**: read the character's `docs/CHARACTERS.md` entry and the anchored code. Three outcomes: (a) code contradicts the documented/intended behavior → real bug; (b) code matches docs and the docs match `characters.json` → likely intended, explain to the user and ask before changing; (c) docs themselves are wrong → doc bug: fix the doc, note it in the Verification addendum, no code change.
3. **Catalogue it**: add a new finding to `docs/AUDIT-FINDINGS.md` with the next free ID (C/M/m by severity — same rubric as the header), citing evidence `file:line` and a failure scenario, and bump the "Summary count" line. This happens even if the fix follows immediately — the catalogue must stay complete.
4. Proceed with the Steps below for the new ID.

## Steps (per finding; batch findings that touch the same files)

1. **Load the finding**: read its section in `docs/AUDIT-FINDINGS.md` — expected behavior, actual behavior, cited anchors, fix direction.
2. **Load the design context**: the affected character's entry in `docs/CHARACTERS.md`; the applicable `docs/INTERACTION-MATRIX.md` tables (forced fights / kills / movers / steal chains / interceptors) if the mechanic appears there; the relevant `docs/BALANCE-CONSTANTS.md` rows if numbers are involved.
3. **Load ALL the code**: grep the exact passive name (and character `Name`) across `King-of-the-Garbage-Hill/Game`, `API`, `Web/VueClient/src`; read every `case`/branch for it in full — not just the cited lines — plus the character's state class. The same passive usually has cases in several hooks (see ARCHITECTURE.md §3); a fix in one can break another.
4. **Implement the minimal fix.** Respect the correctness rules in AGENTS.md (FightCharacter vs GameCharacter, MinusPsycheLog, buffered vs bonus points, exact strings). **Never edit `characters.json` descriptions** — they are deliberately vague by design; document the precise behavior in `docs/CHARACTERS.md` instead, and if the finding's resolution is "reword the player text", hand that back to the designer.
5. **Update the docs in the same change**:
   - `docs/CHARACTERS.md`: correct the entry, refresh anchors.
   - `docs/AUDIT-FINDINGS.md`: append `**Fixed:** <date>, <one-line what changed>` to the finding — never delete it. Update the ⚠ cross-references in other docs if they pointed at this finding.
   - `docs/BALANCE-CONSTANTS.md` / `docs/INTERACTION-MATRIX.md`: update affected rows (remove the ⚠ hole marks).
   - If the fix resolves a known warning (renamed string, removed dead branch): remove its line from `tools/known-warnings.txt` — the audit script then enforces that it stays fixed.
6. **Verify** (all must pass):
   - `cd King-of-the-Garbage-Hill/King-of-the-Garbage-Hill && dotnet build`
   - `cd Web/VueClient && pnpm build` — only if frontend files were touched (`pnpm type-check` is broken in this environment).
   - `bash tools/audit-passives.sh` — no NEW warnings; fixed known-warnings gone.
   - `bash tools/verify-docs.sh --changed` — anchors into edited files still resolve; review DRIFT candidates.
7. **Commit message**: write it to `docs/commit-messages/<date>.md` (add `-2`, `-3` if files for that date already exist — one file per change-set), listing the finding IDs fixed. **Never `git commit`/`git push`.**

## Batching

When given multiple IDs or reports, group by file locality (e.g. all CharacterPassives.cs string renames together), implement the whole batch, then run verification once. List per-item outcomes in the final summary: fixed / catalogued-as-<ID> / intended-behavior (explained) / needs-designer-verdict / blocked (with reason).
