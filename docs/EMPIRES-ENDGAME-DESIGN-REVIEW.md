# Empire's Endgame — Designer Review Ledger

This is the append-only record for configurable defaults, unresolved semantics, and
designer verdicts in the Empire's Endgame completion program. Each entry is keyed to a
JSON Pointer in `Web/VueClient/public/empires-endgame/game-config.json`. If a later phase
needs to correct, accept, reject, or supersede an entry, append a new row with the same
pointer and a new ID; do not rewrite or delete the earlier row.

## Entry template

| ID | JSON Pointer | Phase | Raw source | Chosen default | Rationale | Status | Designer verdict |
|---|---|---:|---|---|---|---|---|
| `P?-NN` | `/path/to/value` | P? | Exact export channel/message/file | Configured value, or “not chosen” | Why the fallback is necessary | Open / Accepted / Rejected / Superseded | Pending, or the dated verbatim verdict |

JSON Pointer tokens use RFC 6901 escaping (`~0` for `~`, `~1` for `/`). Broad questions
that affect a catalog are keyed to that catalog root until an owning phase introduces the
more specific field.

## Seeded open questions

| ID | JSON Pointer | Phase | Raw source | Chosen default | Rationale | Status | Designer verdict |
|---|---|---:|---|---|---|---|---|
| `P0-01` | `/empire/loyalty/workforceDivisors` | P4 | `plans/empires-endgame/README.md` §H compression of the loyalty notes; exact Discord message must be re-verified in P4 | Not chosen; `/19`, `/9`, `/1` are candidates only | The loyalty-to-workforce curve is authored as a tuning note, not an affirmed final curve. | Open | Pending |
| `P0-02` | `/td/waveEveryCons` | P2 | `plans/empires-endgame/README.md` §H compression of channel `тд`; exact message must be re-verified in P2 | Not chosen; `2` cons is the program candidate only | The note says waves every four months, but the con-to-month clock is unresolved. | Open | Pending |
| `P0-03` | `/td/towers` | P2/P3 | `plans/empires-endgame/README.md` §H compression of channel `тд` and the `EE_TD` sketch | Not chosen | The four-by-four grades and “208 builds” establish structure but not tower statistics. | Open | Pending |
| `P0-04` | `/god` | P8 | `plans/empires-endgame/README.md` §H compression of the God/deck-memory notes | Not chosen | Anti-bito thresholds and deck-memory availability have no confirmed limits or cadence. | Open | Pending |
| `P0-05` | `/tavern` | P9 | `plans/empires-endgame/README.md` §H compression of channels `таверна` and `карты` | Not chosen | The Пиковая Дама combo, neighbor-inversion period, and gameplay meaning of hand order require reconciliation. | Open | Pending |
| `P0-06` | `/combat/equipment` | P1/P3 | `plans/empires-endgame/README.md` §H compression of channel `сталь`; `Steel-c748ae22139d6401.txt` is named as the latest source | Not chosen | Only partial per-weapon damage-type levels are authored; remaining levels must be configurable and reviewed. | Open | Pending |
| `P0-07` | `/combat/steelPricing` | P3 | `plans/empires-endgame/README.md` §H compression of channel `сталь`; latest steel file must be re-read in P3 | Not chosen | Branch-crossing and elite gates are described, but complete prices are absent. | Open | Pending |
| `P0-08` | `/empire/economy/jewishBank/surroundingProxy` | P6 | `plans/empires-endgame/README.md` §H compression of channel `здания` | Not chosen | “Окружение” has no current siege-state equivalent, so the executable proxy needs a designer verdict. | Open | Pending |
| `P0-09` | `/empire/epidemics` | P5/P10 | `plans/empires-endgame/README.md` §H compression of epidemic and alchemy notes | Not chosen | Severity, spread, and the typed consequence of an alchemy explosion are not numerically complete. | Open | Pending |
| `P0-10` | `/td/morale` | P2 | `plans/empires-endgame/README.md` §H compression of `ZBS MAKING` morale notes | Not chosen | The older source establishes a minimal scalar/floor concept but not a final scale. | Open | Pending |
| `P0-11` | `/external/allianceStrength` | P2 | `plans/empires-endgame/README.md` §H completion-program seed | Not chosen; a linear curve is a candidate only | The Alliance progression curve has no confirmed authored formula. | Open | Pending |
| `P0-12` | `/chess` | P12 | `plans/empires-endgame/README.md` §H compression of channel `шахматы` and its sketch | Disabled; no gameplay defaults chosen | The source is explicitly sketch-level and is expected to need redesign. | Open | Pending |
| `P0-13` | `/td/battleAbort` | P2 | `plans/empires-endgame/README.md` §H completion-program seed | Not chosen | Abort must prevent save-scumming, but its penalties have no confirmed values. | Open | Pending |
| `P0-14` | `/durak/boutsPerCon` | P0 | `DiscordExports/empire_prompt`; `docs/WEB-CLIENT.md` §12B before P0; bundled `game-config.json` before P0 | `3` remains the bundled compatibility value; it is not accepted as the design verdict | The raw source says one con is “several turns” and configurable, the bundled config uses three, and the pre-P0 document said ten. Phase 0 must not choose among them. | Open | Pending: does one con contain 3 bouts, 10 bouts, or another authored value? |
| `P0-15` | `/quests` | P0 | `plans/empires-endgame/phase-00-scaffolding.md` designer question | Disabled scaffold remains present in exported/custom JSON; no dedicated Builder hiding rule chosen | Schema-v2 compatibility requires the empty future homes, but whether the Builder should hide all disabled homes is a presentation decision. | Open | Pending: are empty disabled future sections acceptable in exported custom configs, or should the Builder hide them until enabled? |
