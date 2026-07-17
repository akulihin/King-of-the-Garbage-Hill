# Phase 8 — God presence

Read the common contract and coverage matrix. Execute after P3A so shared RNG/history/
compatibility rules are established. Do not pull Tavern or mystic content from P9.

## Guaranteed deliverable

Ship gated deck-memory inspection, capped deterministic anti-bito, authored God dialogue,
and accessible Божественная Милость confirmation with versioned device-local preference.
This phase removes no existing content deferral.

## Required raw sources

Read `DiscordExports/empire_prompt`; channels `общее`, `карты`, `персонажи`; and every
message describing face-up shuffle/deck memory, anti-bito, God responses, and Милость.
Inspect current deck orientation, draw/discard/winner paths, gift/Mercy actions, UI, restore,
and QA before editing.

## Work items

1. Add validated God config for deck-memory availability/inspections, anti-bito minimum/
   return count/cap/source/insertion, authored lines with ID/trigger/weight/once behavior,
   and Mercy confirmation copy. Missing numbers use config+ledger; missing semantics block.

2. Add deck-memory runtime usage when availability is limited: serialized inspections used
   in the current con and deterministic reset boundary. Consume a use only when an allowed
   inspection successfully opens—not on render, denied click, reload, or repeated component
   mount.

3. Add engine `canInspectDeck` plus immutable projection. Show ordered card identity and
   each instance's inverted state in the authored deck orientation; never mutate deck/RNG or
   reveal data when denied. Build accessible `DeckMemoryPanel` states.

4. Implement anti-bito exactly once in the winner path. Select eligible discard instances
   without replacement using serialized gameplay RNG, insert in confirmed order/orientation,
   increment capped intervention state, and then resume normal winner resolution. Empty/
   insufficient discard and cap boundaries terminate safely.

5. Persist bounded intervention records sufficient for replay: con/bout, returned stable
   instance IDs, insertion, trigger, and resulting digest. Same pre-intervention snapshot
   reproduces the same result.

6. Route confirmed dialogue triggers through one selector. Use a dedicated serialized
   cosmetic RNG so lines never perturb gameplay. Audit at least `boutWon`, `boutLost`,
   `take`, `antiBito`, `giftOffered`, and `inversion`, but enable only raw-authored triggers.
   Persist an ordered/bounded dialogue log or pending line plus once/occurrence state so
   simultaneous/repeated triggers render deterministically after restore.

7. Identify the exact action(s) that spend Божественная Милость. Wrap those UI calls in an
   accessible confirmation; engine validation remains authoritative. Persist a versioned
   namespaced UI preference such as `skipDivineMercyConfirmation` outside campaign state.
   Corrupt/future prefs fall back safely.

8. Add Builder JSON support, `anti-bito` QA scenario, and digest/trace fields for inspection
   usage, interventions, and dialogue order. Full autoplay must still terminate under a
   strict cap.

## Carrier gate

- No guaranteed existing carrier is un-deferred.
- `card-joker-jester` inverted remains deferred unless the authoritative source gives an
  exact empire effect completely executed here.
- Mystic cards are excluded from inspection/anti-bito eligibility unless P9 source and
  integration explicitly define otherwise.

## Verification additions

- Deck-memory availability, successful-open consumption, per-con reset, denied/reload cases,
  order and inverted-state projection.
- Anti-bito threshold/cap, empty/fewer cards, eligibility, insertion order, deterministic
  replay, restore, and guaranteed winner after cap.
- Dialogue trigger/weight/once/order/pending restore and cosmetic RNG isolation; bounded log.
- Mercy confirmation, “do not show again” persistence/reload/corrupt prefs, QA/Cypress,
  migration, and common standing gate.

## Designer questions

- Deck-memory unlock, inspections per con, reset, and exact visible information?
- Anti-bito terminal conditions, eligible owner/discards, return count/location, and cap?
- Full authored trigger list, repeat/priority behavior, and canonical line copy?
- Exact Божественная Милость action(s) and confirmation text?
