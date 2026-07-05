---
name: balance
description: Change a tunable game number (buff/nerf/rebalance a chance, threshold, cooldown, cost, multiplier, stat) for King of the Garbage Hill. Use for "buff X", "nerf Y", "change Z from 3 to 5", "сделай шанс 20%".
---

# Balance change

**Prime rule: docs are the map, anchors are the territory — never edit a hook/case you haven't read in full.**

1. **Locate the number**: find its row in `docs/BALANCE-CONSTANTS.md` (grep the character or mechanic). If it's not there, treat that as a docs gap: find it in code via the CHARACTERS.md entry, then *add* the row.
2. **Read the anchored code in full** — the whole `case`/method containing the constant, not just the line. Check whether the same number appears in sibling cases (some constants repeat across attack/defense branches — e.g. goblin death % exists in two places; change all occurrences or none).
3. **Do NOT touch `characters.json` descriptions** — they are deliberately vague player-facing text (AGENTS.md rule). If the old number is spelled out in the RU description ("+2 очка", "50%"), flag it in your summary so the designer can decide whether to reword; the exact new value goes into `docs/CHARACTERS.md` and `docs/BALANCE-CONSTANTS.md` only.
4. Apply the code change (respect AGENTS.md correctness rules — e.g. regular vs bonus points changes the round-multiplier behavior).
5. **Docs in the same change**: update the `docs/BALANCE-CONSTANTS.md` row (value + anchor if lines shifted); update the number in the `docs/CHARACTERS.md` entry; touch `docs/INTERACTION-MATRIX.md` only if a threshold changes an interaction rule (e.g. a range that gates forced fights or Harm).
6. **Verify**: `dotnet build` (+ `pnpm build` if a widget shows the number), `bash tools/audit-passives.sh` (must stay clean), `bash tools/verify-docs.sh --changed`.
7. Commit message to `docs/commit-messages/<date>.md` (`-2`, `-3` on same-day collisions) stating old → new values; never `git commit`.

For sweeping balance passes (many numbers), work top-down through BALANCE-CONSTANTS.md sections and batch the verification.
