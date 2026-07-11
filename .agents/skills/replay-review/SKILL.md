---
name: replay-review
description: Review King of the Garbage Hill replay bugs from a replay ID, player-facing replay URL, or replay API URL. Use when troubleshooting a replay, checking what players saw, comparing replay state with raw JSON, or investigating a reported replay discrepancy; always derive and inspect both the anonymous Web UI and REST API forms from any one input.
---

# Review a replay

## Resolve both sources

Accept a replay ID, a UI URL, or an API URL. Extract the replay ID, then always inspect both:

- raw JSON: `https://kotgh.ozvmusic.com/api/game/replay/{id}`
- player-facing UI: `https://kotgh.ozvmusic.com/replay/{id}`

Preserve `round`, `player`, and `fight` from a supplied UI URL. Given only an ID or API URL, begin with the final round and player 0, then visit perspectives implicated by the JSON. Run `scripts/resolve-replay.py` for deterministic URL construction.

## Inspect both representations

1. Fetch the raw JSON before reaching a diagnosis. Record the game version, mode, player summaries, relevant rounds, per-player snapshots, global logs, fight logs, chronicle, and story.
2. Open the Web UI without signing in. Replay pages are public. Never create a Discord or web account merely to review one. If an older deployment still presents login, use **Play without Discord** only as a temporary fallback and report that friction separately.
3. Reproduce the reported round, player perspective, and fight. Also inspect the previous round when the UI shifts pre-fight stats from the prior snapshot.
4. Compare rendered information and interactions with the corresponding JSON. Check every relevant player perspective; one player's private snapshot does not represent everyone.
5. Read `docs/WEB-BACKEND.md` section 9 and the replay sections of `docs/WEB-CLIENT.md`, then follow their cited code anchors. Avoid broad codebase reads.

## Classify discrepancies

- **Capture bug:** persisted JSON is already wrong or incomplete.
- **Projection/privacy bug:** a player's stored perspective exposes or hides the wrong state.
- **Reconstruction bug:** `store/replay.ts` combines saved snapshots incorrectly.
- **Rendering/interaction bug:** the data and reconstructed state are correct, but the UI behaves incorrectly.
- **Historical behavior:** the replay's `gameVersion` predates current behavior.

Do not use the current working tree as proof of historical behavior without checking `gameVersion`. Do not present private per-player blocks as public data.

If fetching fails, state the exact HTTP, DNS, browser, or tool failure. Do not infer that production is down. Try the available direct-fetch methods before asking the user for JSON, and still inspect the UI when access becomes possible.

## Report or fix

Lead with the player-visible symptom and evidence-backed diagnosis. Cite the replay ID, round, player perspective, JSON fields/events, and observed UI behavior. If asked to implement a fix, invoke `fix-finding` and follow its catalogue, documentation, version-bump, and verification contract.
