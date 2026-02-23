# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

King of the Garbage Hill is a turn-based 6-player tactical game with 20+ unique characters. It runs as a hybrid **Discord bot + ASP.NET Core web server** sharing a single process, with a **Vue 3 + TypeScript** web client communicating via SignalR.

## Build & Run Commands

### Backend (.NET 10.0)
```bash
dotnet build   
dotnet run    # Starts both Discord bot and web server on port 80 (configurable via KOTGH_PORT)
```

### Frontend (Vue 3 + Vite)
```bash
cd Web/VueClient
pnpm install
pnpm dev          # Vite dev server (port 5173)
pnpm build        # Production build → ../../King-of-the-Garbage-Hill/wwwroot/
pnpm serve        # Preview production build on port 4137
pnpm lint         # ESLint with auto-fix
pnpm type-check   # Vue type checking via vue-tsc
```

### Testing
```bash
cd Web/VueClient
npx vitest        # Unit tests (happy-dom environment, v8 coverage) — no npm script, use npx
npx cypress       # E2E tests — no npm script, use npx
```

### Deployment
`deploy_to_prod` script scps build to AWS EC2 (3.65.44.127), runs as systemd service.

## Architecture

### Backend (King-of-the-Garbage-Hill/)

- **Program.cs** — Entry point: starts Discord bot thread + Kestrel web server in parallel
- **Global.cs** — Singleton holding active `GamesList` (`List<GameClass>`) and `FinishedGamesList` (`ConcurrentDictionary<ulong, GameClass>`), shared between Discord and web
- **Config.cs** — Loads `DataBase/config.json` (bot token, settings)

**Dependency Injection:** Uses Lamar container with marker interfaces `IServiceSingleton` / `IServiceTransient` for auto-discovery via `AddSingletonAutomatically()`.

**Game Logic (`Game/`) — CRITICAL DIRECTORY:**

> **This is the most critical code in the project.** When making any changes to files in `Game/`, you MUST thoroughly read and account for all related code across the entire `Game/` directory. Character passives, game logic, classes, and behaviors are deeply interconnected — a change in one file (e.g. a passive in `CharacterPassives.cs`) can affect fight resolution (`DoomsdayMachine.cs`, `CalculateRounds.cs`), bot behavior (`BotsBehavior.cs`), character state (`CharacterClass.cs`, `PassivesClass.cs`), and game flow (`CheckIfReady.cs`, `StartGameLogic.cs`). Always verify the full impact before modifying.

- `Game/Characters/` — Individual character classes (Kratos.cs, Gleb.cs, Saitama.cs, etc. — 36 files)
- `Game/GameLogic/CharacterPassives.cs` — Central passive ability handler (~289KB, largest file)
- `Game/GameLogic/CalculateRounds.cs` — Round resolution
- `Game/GameLogic/DoomsdayMachine.cs` — Fight execution, position swaps, end-of-round processing
- `Game/GameLogic/BotsBehavior.cs` — AI behavior for bot players
- `Game/GameLogic/CheckIfReady.cs` — Turn readiness checks
- `Game/GameLogic/StartGameLogic.cs` — Character rolling and game initialization
- `Game/Classes/GameClass.cs` — Game state model (round number, players list, global logs, teams, exploit system)
- `Game/Classes/CharacterClass.cs` — Character stats/abilities model (~53KB), also defines `Passive`, `JusticeClass`, `AvatarEventClass`
- `Game/Classes/InGameStatusClass.cs` — `InGameStatus` class: score tracking (`AddBonusPoints`, `GetScore`, `AddRegularPoints`), leaderboard position, personal logs, ForOneFight flags, combat state flags
- `Game/Classes/PassivesClass.cs` — Per-player passive state (character-specific fields)
- `Game/Classes/GamePlayerBridgeClass.cs` — Links Discord accounts to game players, holds `GameCharacter` + `FightCharacter` + `Passives` + `Status`, contains `MinusPsycheLog`
- `Game/Classes/DiscordAccountClass.cs` — Persistent user account data (stats, match history, character chances)

**Web API (`API/`):**
- `GameHub.cs` — SignalR hub at `/gamehub` for real-time web client communication
- `Controllers/GameController.cs` — REST endpoints at `/api/game`
- `Services/WebGameService.cs` — Web game operations (queries, actions, web game creation)
- `Services/GameNotificationService.cs` — Broadcasts state to SignalR groups (`game-{gameId}`)
- `Services/GameStateMapper.cs` — Static class mapping internal state to DTOs
- `Services/GameStoryService.cs` — AI-generated game narrative summaries

**Discord (`DiscordFramework/` + `GeneralCommands/`):**
- `CommandHandling.cs` — Command router
- `General.cs` — Main commands (e.g., `*st` starts a game)

**Data:**
- `LocalPersistentData/UsersAccounts/` — JSON user account files
- `DataBase/art/` — Served at `/art/`, `DataBase/sound/` — Served at `/sound/`

### Frontend (Web/VueClient/)

Vue 3 Composition API with `<script setup>`, strict TypeScript, HTML5 history mode routing.

- **`store/game.ts`** — Pinia store: connection, auth, game state, lobby state, sound coordination
- **`services/signalr.ts`** — SignalR client with typed methods (authenticate, joinGame, attack, block, levelUp, predict, etc.)
- **`services/sound.ts`** — Game sound effects manager (Web Audio API)
- **Pages:** `Game.vue` (main game, 25KB), `Lobby.vue`, `Spectate.vue`, `Home.vue`
- **Routes:** `/games` → Lobby (redirected from `/`), `/game/:gameId` → Game, `/spectate/:gameId` → Spectate, `/home` → Home

### Communication Flow

Discord users interact via bot commands and reaction buttons. Web users connect through SignalR hub, authenticate with Discord ID (passed as string to avoid JS number precision loss), join game groups, and receive real-time state broadcasts. Web-only accounts can be created via `RegisterWebAccount`.

## GameCharacter vs FightCharacter Architecture

Each player (`GamePlayerBridgeClass`) holds two `CharacterClass` instances:

- **`GameCharacter`** — The persistent character state. All lasting changes (stat gains, moral, skill) are written here. Changes take effect on the **next round** when FightCharacter is re-copied.
- **`FightCharacter`** — A **read-only snapshot** deep-copied from GameCharacter once at the start of each round's fight processing (`DeepCopyGameCharacterToFightCharacter` in DoomsdayMachine). `CalculateRounds.cs` reads exclusively from FightCharacter (lines 70-71: `var me = player.FightCharacter`).

### DeepCopy Behavior (`CharacterClass.DeepCopy()`)
- Uses `MemberwiseClone()` — all value-type fields (int, decimal, string) are independent copies
- **`Status` is SHARED** — same `InGameStatus` instance on both copies (used for logging, fight flags)
- **`Justice` is SHARED** — same `JusticeClass` instance on both copies (mutated through shared reference during fights)
- `Passive` list is deep-copied (independent)

### ForOneFight Mechanic
To modify a stat for a single fight only, use the `SetXForOneFight` / `AddSpeedForOneFight` methods. These set a temporary override value (sentinel `-228` = not set) that the getter checks first:

```csharp
// Example: GetStrength() checks override before returning base
public int GetStrength() {
    if (StrengthForOneFight != -228) return StrengthForOneFight;
    return Strength;
}
```

Available methods: `SetIntelligenceForOneFight`, `SetStrengthForOneFight`, `SetSpeedForOneFight`, `SetPsycheForOneFight`, `SetSkillForOneFight` (takes `decimal`), `SetJusticeForOneFight`, `AddSpeedForOneFight` (delta variant — reads current speed and adds delta, floors at 0).

Each `SetXForOneFight` also sets a flag on the shared `Status` (e.g., `Status.IsStrengthForOneFight = true`).

### Reset Lifecycle (per fight)
`ResetFight()` in DoomsdayMachine checks each Status flag and resets ForOneFight on **both** GameCharacter and FightCharacter, then clears the flag.

### CRITICAL RULES for Before-Fight Handlers

> **ForOneFight overrides MUST be set on `FightCharacter`**, not `GameCharacter`.
>
> Since CalculateRounds reads from FightCharacter, setting a ForOneFight value on GameCharacter has **no effect** on fight calculations — the override value lives on GameCharacter while FightCharacter still has the sentinel `-228`.

```csharp
// CORRECT — FightCharacter override visible to CalculateRounds
me.FightCharacter.SetStrengthForOneFight(0, "MyPassive");

// WRONG — GameCharacter override invisible to CalculateRounds
me.GameCharacter.SetStrengthForOneFight(0, "MyPassive");
```

> **Stat reads in before-fight handlers should use `FightCharacter`** to respect ForOneFight overrides from other passives processed earlier.

**Exception**: `Justice` is a shared reference, so `GameCharacter.Justice.SetJusticeForOneFight(...)` works correctly on either side.

**Persistent changes** (stat gains, AddIntelligence, AddSkill, etc.) should use `GameCharacter` — they take effect next round via DeepCopy.

## Key Conventions

- **C# namespaces:** `King_of_the_Garbage_Hill.*`
- **JSON serialization:** `JsonNamingPolicy.CamelCase` for API responses
- **CORS origins:** localhost:5173 (Vite), localhost:3535 (C# dev), localhost (port 80), http + https kotgh.ozvmusic.com
- **Comments/docs:** Mixed Russian and English throughout codebase
- **Frontend state:** Pinia `useGameStore()` pattern with `ref()` / `computed()`

## New Character Implementation Guide

Adding a new character touches ~14 files across backend, data, and frontend. Follow this order:

### Step 1: State Class — `Game/Characters/{Name}.cs`
Create a file with inner classes holding character-specific state (counters, trackers, cooldowns). Follow existing patterns (e.g., `GoblinSwarm.cs`, `Kotiki.cs`). Keep state minimal — only what passives need between rounds.

### Step 2: Character Data — `DataBase/characters.json`
Add a JSON entry with Name, stats (Intelligence, Psyche, Speed, Strength), Avatar, Tier, Description, and Passive array. Each passive has `PassiveName`, `PassiveDescription`, `Visible`. The avatar string must match an image filename in the art directory.

### Step 3: Per-Player State — `Game/Classes/PassivesClass.cs`
Add fields for character-specific state (referencing the Step 1 classes). Two categories:
- **Owner-only fields** (e.g., `KotikiStorm`, `KotikiAmbush`) — only meaningful for the character's player
- **Per-player fields** (e.g., `KotikiCatType`, `KotikiCatOwnerId`) — state that can exist on ANY player (debuffs, marks, transferred effects)

### Step 4: Phrases — `Game/MemoryStorage/CharactersPhrases.cs`
Add `PhraseClass` fields for log messages + initialize them in the constructor. Follow existing pattern — each phrase wraps a `SendLog(player, bool, ...)` call. Primary overload: `SendLog(GamePlayerBridgeClass player, bool delete, string prefix = "", bool isRandomOrder = true, string suffix = "")`.

### Step 5: Passive Logic — `Game/GameLogic/CharacterPassives.cs` (LARGEST CHANGE)
Add `case "PassiveName":` blocks in the relevant handler methods. Available hooks (in execution order):

| Handler | When it fires | Common uses |
|---------|--------------|-------------|
| `HandleEventsBeforeFirstRound` | Once at game start, before round 1 | Initial character state setup |
| `HandleDefenseBeforeFight` | Before fight begins (defender) | Defensive buffs, ForOneFight overrides |
| `HandleAttackBeforeFight` | Before fight begins (attacker) | Stat buffs, pre-fight effects |
| `HandleAttackAfterFight` | After fight resolves (attacker) | Outplay marks, attack rewards |
| `HandleDefenseAfterFight` | After fight resolves (defender) | Counter-attack effects |
| `HandleDefenseAfterBlockOrFight` | After block or fight (defender) | Effects that trigger on block too |
| `HandleDefenseAfterBlockOrFightOrSkip` | After any interaction (defender) | Effects that always trigger |
| `HandleCharacterAfterFight` | After each fight resolves (both) | Rewards, stat stealing, deployments |
| `HandleEndOfRound` | End of round (before flag reset) | Cooldown ticks, cleanup, IsBlock still true here |
| `HandleNextRound` | Start of next round (after RoundNo++) | Per-round setup |
| `HandleNextRoundAfterSorting` | After score-based leaderboard sort | Position-dependent effects |
| `HandleBotPredict` | After sorting, before exploit roll | Bot AI prediction logic (guess opponent characters) |
| `HandleShark` | During fight resolution (called from DoomsdayMachine) | Shark character-specific victory/defeat tracking |

### Step 6: Fight Resolution — `Game/GameLogic/DoomsdayMachine.cs`
Modify only if the character changes core fight mechanics (e.g., skipping harm, allowing forced fights for blocking players, custom win/loss conditions). Key locations:
- **Block/skip section** (~line 279): Players who block/skip get `HandleCharacterAfterFight` + `ResetFight`, then `continue` only if `WhoToAttackThisTurn.Count == 0` — forced-fight bypass already implemented
- **Win branch** (~line 581): Moral changes, `LowerQualityResist` damage — add skip conditions if character is harmless
- **Execution order** (approximate line numbers, may drift):
  1. `HandleEndOfRound` (~915)
  2. Reset flags (~917-960)
  3. `RoundNo++` (~974)
  4. `HandleNextRound` (~982)
  5. Score sort (~995)
  6. Tigr forced-top swap (~998)
  7. Portal Gun forced-position swap (~1023)
  8. HardKitty forced-last (~1052)
  9. LvlUp point distribution on rounds 3, 5, 7, 9 + `SetPlaceAtLeaderBoard` + `RollSkillTargetForNextRound` (~1066)
  10. Ziggurat restore — immovable players return to locked positions (~1079)
  11. Quality Drop — players drop leaderboard positions based on `StrengthQualityDropTimes` (~1096)
  12. Round 10 Ziggurat win condition check (~1131)
  13. `SortGameLogs` (~1145)
  14. `HandleNextRoundAfterSorting` (~1146)
  15. `HandleBotPredict` (~1147)
  16. `RollExploit` (~1148)

### Step 7: Turn Injection — `Game/GameLogic/CheckIfReady.cs`
Add forced-attack or action-override logic if the character manipulates other players' turns (e.g., taunt, aggress). Inject targets into `WhoToAttackThisTurn`. Add after the Aggress section, before the safety-net block.

### Step 8: Level-Up Override — `Game/ReactionHandling/GameReactions.cs`
If the character has custom level-up behavior, add a check by character name before the default stat selection code. Use `player.GameCharacter.Justice.AddJusticeForNextRoundFromSkill(int)` for Justice (there is no generic `AddJustice` method).

### Step 9: Bot Behavior — `Game/GameLogic/BotsBehavior.cs`
Add AI logic in `HandleBotMoralForPoints` (moral handling) and the action-selection switch. Bots need: attack target preferences, block/skip conditions, and moral usage rules.

### Step 10: Discord Display — `Game/DiscordMessages/GameUpdateMess.cs`
Add leaderboard customizations in `CustomLeaderBoardBeforeNumber` (icons/prefixes) and `CustomLeaderBoardAfterPlayer` (character-specific info text).

### Step 11: DTOs — `API/DTOs/GameStateDto.cs`
Create a DTO class (e.g., `KotikiStateDto`) and add it to `PassiveAbilityStatesDto`. For per-player state visible to non-owners, create a separate DTO (e.g., `KotikiCatOnMeDto`) — follow the `SellerMark` pattern.

### Step 12: Mapper — `API/Services/GameStateMapper.cs`
Map state in the `MapPlayer()` method (passive ability state population is inline within `MapPlayer`, inside the `if (isMe && game != null)` block). Owner-specific state goes inside the passive name switch case. Per-player state (visible to any player) goes outside the passive loop, after the SellerMark check.

### Step 13: Frontend Types — `Web/VueClient/src/services/signalr.ts`
Add TypeScript interfaces matching the DTOs. Add as optional fields on `PassiveAbilityStates`.

### Step 14: Frontend Widget — `Web/VueClient/src/components/PlayerCard.vue`
Add character widget in the passive abilities section (similar to goblin swarm widget). For per-player effects, add a standalone widget outside the character-specific block.

### Common Pitfalls
- **`Passive` class uses a constructor**: `new Passive(name, description, visible)` — NOT object initializer syntax. Also has a `Standalone` property (default false) for standalone passive display.
- **No `AddJustice` method**: Use `AddJusticeForNextRoundFromSkill(int)` or `AddJusticeForNextRoundFromFight(int)` (both on `JusticeClass`)
- **Score methods** (on `InGameStatus`): `AddBonusPoints(decimal, string)` adds to total; `GetScore()` returns total; `AddRegularPoints(int, string, bool)` adds to per-round bucket
- **Psyche loss MUST use `MinusPsycheLog`**: When reducing Psyche, NEVER call `AddPsyche(-N)` directly. Always use `player.MinusPsycheLog(player.GameCharacter, game, -N, "PassiveName")` instead. `MinusPsycheLog` does two things bare `AddPsyche` does not: (1) checks for "Спокойствие" passive immunity and skips the loss entirely if present, (2) writes a global log (`"X психанул"`) visible to all players. The only exceptions are passives with intentionally unique global log messages (e.g., "Дизмораль", "Стримснайпят и банят") or temporary buff reversals that aren't conceptually "rage". Signature (on `GamePlayerBridgeClass`): `MinusPsycheLog(CharacterClass, GameClass, int, string)`
- **Transferred passives**: When adding a Passive to another player's list, existing switch-case handlers will process it — add immunity checks to prevent unintended behavior (e.g., infinite taunt loops)
- **Block/skip players skip fight loop**: If your character forces fights on blocking/skipping players, you must modify the block/skip `continue` in DoomsdayMachine to check `WhoToAttackThisTurn.Count`
- **Don't double-log stat changes**: Methods like `AddStrength(int, string, bool isLog = true)`, `AddPsyche(int, string, bool isLog = true)`, `AddExtraSkill(decimal, string, bool isLog = true)`, `AddMoral(decimal, string, bool isLog = true)`, `AddBonusPoints(decimal, string)` automatically write to **Personal logs** when `isLog` is true (the default). Do NOT manually call `AddInGamePersonalLogs` for the same change — that produces duplicate log entries. Pass `isLog: false` to suppress auto-logging.
- **Personal vs Global logs**: `AddInGamePersonalLogs` / `PhraseClass.SendLog` writes to the player's **personal** log (only they see it). `AddGlobalLogs` (on `GameClass`) writes to the **global** log (visible to all players). Stat-change methods auto-log to personal logs only. Be mindful of which log you want to write to when calling these methods.
