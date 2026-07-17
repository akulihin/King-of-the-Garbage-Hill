# CLAUDE.md

Guidance for Claude Code in this repository.

## Project

King of the Garbage Hill — turn-based 6-player tactical game, 36 characters. Hybrid **Discord bot + ASP.NET Core web server** (single process, .NET 10, Lamar DI, Discord.Net 3.20), **Vue 3 + TypeScript** client via SignalR. Mixed Russian/English; **Russian passive/character names are load-bearing string identifiers** — never paraphrase or "fix" them.

## The docs are the source of truth — read them first, not the codebase

The codebase is ~19k lines of game logic; do **not** try to load it into context. `docs/` contains a code-verified model of the whole game. Use its code references to inspect the relevant symbols and nearby implementation instead of loading whole files:

| You are… | Read |
|---|---|
| Understanding rules/systems (fight math, score, justice, moral, round pipeline) | `docs/GAME-DESIGN.md` |
| Touching code structure, hooks, state model, web plumbing | `docs/ARCHITECTURE.md` (§3 hook order, §7 the 14-file per-character pattern) |
| Changing/creating a character, widget or skill | `docs/CHARACTERS.md` (per-passive actual behavior) + `docs/ARCHITECTURE.md` §7 + `docs/INTERACTION-MATRIX.md` (add your row to every applicable table) |
| Balancing numbers | `docs/BALANCE-CONSTANTS.md` (every tunable with anchor) |
| Touching the web API, SignalR hub, DTOs/state mapping, push, web auth, replays, mini-game services | `docs/WEB-BACKEND.md` (endpoint/hub/event catalogs, hidden-info rules, WebGameService bridge) |
| Touching the Vue client (pages, stores, SignalR client, components, widget UI, sounds, themes) | `docs/WEB-CLIENT.md` (routes, stores, GameState TS contract, widget inventory) |
| Touching Discord commands or the in-game Discord UI (buttons/selects, DM messages, lobby flow) | `docs/DISCORD-INTERFACE.md` (command + custom-id catalogs, dispatch/round flow, privacy) |
| Checking name/passive wiring | run `bash tools/audit-passives.sh` (regenerates `docs/PASSIVE-MAP.md`; new ORPHAN/GHOST/BAD-NAME = you broke a string) |
| Fixing any bug — by finding ID **or** free-form report | invoke the **`/fix-finding`** skill — it triages against `docs/AUDIT-FINDINGS.md` (known issues C1/M1-M16/m1-m24/D1-D11) and the designer verdicts in `docs/DESIGNER-REVIEW.md`, catalogues new bugs itself, and enforces the whole contract |
| Adding a character / brand-new passive / widget | invoke the **`/new-character`** skill |
| Changing how an existing passive works (rework per intent notes) | invoke the **`/rework-passive`** skill |
| Changing a tunable number (buff/nerf) | invoke the **`/balance`** skill |
| The user edited docs/ by hand and wants the game to match | invoke the **`/sync-docs`** skill (docs as spec: diff docs/, implement via the anchors, repair ripples) |
| Checking that doc anchors still match the code | `bash tools/verify-docs.sh [--changed]` (hard-fails on dead anchors; DRIFT list is advisory) |

`Game/GameDesign.txt` is the designer's raw intent notes (incl. unbuilt characters); root-level `*_update` files are recent change intents; past commit messages live in `docs/commit-messages/`.

## Documentation maintenance contract (mandatory)

Documentation describes behavior and contracts, not the fact that code was edited. Update only the affected document sections, in the same change-set, when their behavior, public contract, invariant, inventory, or code reference changed. Do not create documentation churn for formatting, tooling, generated output, or an internal refactor that leaves documented behavior and references intact.

| Change | Required documentation work |
|---|---|
| Gameplay/passive behavior | Update the affected character/system/interaction sections. |
| Tunable value | Update the affected `docs/BALANCE-CONSTANTS.md` row. |
| Web or Discord interface contract | Update only the matching interface document. |
| Bug fix | Update the finding plus any behavioral description that is no longer true. |
| Behavior-preserving refactor | Repair only claims or references made stale by the refactor. |
| Formatting, tooling, or internal cleanup | No gameplay-doc update unless a documented workflow or reference changed. |

Reference style is deliberately mixed:

- In very large or centralized files (especially `CharacterPassives.cs`), use `File.cs:line` or `File.cs:line-line` so `tools/verify-docs.sh` can validate and drift-check the destination.
- In smaller files, prefer the file plus a unique backticked type/method/property name when that remains unambiguous and is less likely to churn than a line number.
- References are navigation and evidence; the prose must still state the rule, invariant, or behavior an agent needs. A bare reference is not documentation.
- Do not bulk-convert working references just to adopt this style. Apply it when adding or already repairing a reference.

Specific triggers:

1. Character/passive change → update its entry in `docs/CHARACTERS.md` (+ `docs/INTERACTION-MATRIX.md` rows if it forces fights, kills, moves positions, steals/copies, or intercepts moral/psyche/Harm).
2. Any tunable number change → update the row in `docs/BALANCE-CONSTANTS.md`.
3. Any name/passive/string change → `bash tools/audit-passives.sh` and commit the regenerated `docs/PASSIVE-MAP.md`; new warnings must be fixed or added to `tools/known-warnings.txt` with a finding ID.
4. System-level change (fight math, pipeline, plumbing) → update `docs/GAME-DESIGN.md` / `docs/ARCHITECTURE.md`.
5. Interface change → update the matching interface doc: web API/hub/DTO/mapper/push → `docs/WEB-BACKEND.md`; Vue client → `docs/WEB-CLIENT.md`; Discord commands/in-game UI → `docs/DISCORD-INTERFACE.md`. (New docs must also be added to the scan list + `resolve()` map in `tools/verify-docs.sh`.)
6. Fixed a finding → mark it in `docs/AUDIT-FINDINGS.md` (don't delete; note the fix) and remove its line from `tools/known-warnings.txt`.
7. New bug discovered → add a finding with the next free ID.

Docs drift is a bug. The audit files exist so changes can be made *and verified* without re-reading the codebase.

After implementation, run `bash tools/verify-docs.sh --changed`; run the full check when documentation structure, the verifier, or broad cross-cutting behavior changes. The script validates resolvable line anchors and exact catalogs, but semantic accuracy still requires reviewing the affected prose against the code.

Two project hooks enforce this automatically (`.claude/settings.json`): after any edit to passive-bearing files the passive audit re-runs and reports NEW warnings; on stop, a reminder fires if game code changed without a docs update. Don't be surprised by their output — act on it.

## Build & run

```bash
cd King-of-the-Garbage-Hill/King-of-the-Garbage-Hill
dotnet build
dotnet run                    # needs DataBase/config.json (Token, AnthropicApiKey)
KOTGH_PORT=3535 dotnet run    # override port (default 80)
```

```bash
cd Web/VueClient
pnpm dev          # Vite on :5173, proxies /api + /gamehub per .env
pnpm build        # outputs to ../../King-of-the-Garbage-Hill/wwwroot
pnpm lint         # eslint --fix
```

- **`pnpm type-check` is broken in this environment — use `pnpm build` to verify frontend changes.**
- No test project; verification = `dotnet build` + `pnpm build` + the audit script + `bash tools/simulate.sh` + targeted play-testing. The simulate script is the behavioral safety net: headless mass bot games (no Discord/web), ~15s for the default 106 games. Exit 0 = clean; 1 = game exceptions and/or frozen games (the report JSON in `King-of-the-Garbage-Hill/DataBase/Simulations/` names each with line-up + stack — treat as findings, triage via `/fix-finding`); 2 = harness failure. `--characters "6 names" --games 20` replays a fixed matchup (e.g. the character you just changed); details in `docs/ARCHITECTURE.md` §10.
- Deploy: `deploy_to_prod` (build → tar → scp to EC2 → systemd `kotgh`).

### Git

Do NOT `git commit` or `git push`. Write the commit message to `docs/commit-messages/<date>.md` (e.g. `2026-07-01.md`; add `-2`, `-3` for further change-sets the same day — one file per commit, content = the message itself). The user commits. The folder is gitignored.

## Correctness rules that must never be violated

(These cause the classic bugs; full rationale in `docs/ARCHITECTURE.md` §2/§9.)

- Each player has `GameCharacter` (persistent) and `FightCharacter` (per-round snapshot; `CalculateRounds` reads **only** this). **ForOneFight overrides go on `FightCharacter`** (`me.FightCharacter.SetStrengthForOneFight(...)`) — on `GameCharacter` they do nothing. Exception: `Justice` is shared, either side works. Stat *reads* in before-fight hooks: use `FightCharacter`.
- Persistent changes (`AddIntelligence`, `AddExtraSkill`, …) go on `GameCharacter`.
- `Status` and `Justice` are the SAME instance on both copies; a new List/Dictionary field on `CharacterClass` needs a line in `DeepCopy()`.
- Psyche loss goes through `player.MinusPsycheLog(player.GameCharacter, game, -N, "PassiveName")` (immunities + global log), never raw `AddPsyche(-N)` (documented exceptions: Дизмораль-style unique logs).
- No `AddJustice` — use `AddJusticeForNextRoundFromSkill/FromFight` (buffered; any win zeroes justice first).
- Score: `AddBonusPoints` = immediate, never multiplied, floors at 0; `AddRegularPoints`/`AddWinPoints` = buffered, ×1/×2/×4 by round at end of round. After the round-10 flush, `BonusPointsFromMoral` must be flushed manually (see Saitama's reclaim, `CP:4844-4851`).
- Stat/moral/skill mutators auto-log personally — don't also `AddInGamePersonalLogs` (pass `isLog: false` to suppress). Personal logs = player-only; `game.AddGlobalLogs` = everyone.
- `Passive` uses the 3-arg constructor `new Passive(name, description, visible)` (+ `Standalone` property). Transferred/copied passives (Ziggurat, cats, transforms) dispatch for their new holder — add immunity checks (see `docs/INTERACTION-MATRIX.md` §6).
- Forcing fights on blocking/skipping players works via `WhoToAttackThisTurn` (the fight loop processes forced fights); respect the round-10 Тигр-ban carve-out pattern (`CheckIfReady.cs:1283`).
- **Never edit `PassiveDescription`/`Description` texts in `characters.json`** — they are deliberately vague, written for players to interpret; that vagueness is game design. The precise mechanics belong in `docs/CHARACTERS.md` (which you DO keep exact). If a change genuinely needs new player-facing wording, ask — the designer writes it (or hands you exact text to paste verbatim).
- Passive dispatch is stringly-typed (`case "PassiveName"`, `Name == "…"`). Renames silently orphan logic — run the audit script.
- **Never edit a passive's logic without first reading ALL its `case` blocks across every hook plus its state class** — grep the exact passive name (and the character `Name`) first. Reading more context is always preferred over a blind edit; the docs tell you *where* to read, not what to skip.

## Conventions

Namespaces `King_of_the_Garbage_Hill.*` · JSON CamelCase · CORS: localhost:5173/:3535/:80, kotgh.ozvmusic.com · no database — flat JSON (`DataBase/UserAccounts/…`), accounts in a `ConcurrentDictionary` · static assets `DataBase/art|sound` → `/art`, `/sound` · web auth by Discord ID as string · `*st` starts a Discord game.
