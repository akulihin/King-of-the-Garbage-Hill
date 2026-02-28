# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview
King of the Garbage Hill — turn-based 6-player tactical game with 20+ characters. Hybrid **Discord bot + ASP.NET Core web server** (single process), **Vue 3 + TypeScript** web client via SignalR. Mixed Russian/English comments throughout.

## Build & Run

### Backend (C# / .NET 10)
```bash
cd King-of-the-Garbage-Hill/King-of-the-Garbage-Hill
dotnet build
dotnet run                    # Requires DataBase/config.json (Token, AnthropicApiKey)
KOTGH_PORT=3535 dotnet run    # Override port (default: 80)
```
Single-project solution. Lamar DI, Discord.Net 3.18, Newtonsoft.Json (transitive). No test project.

### Frontend (Vue 3 / Vite / pnpm)
```bash
cd Web/VueClient
pnpm dev          # Vite dev server on :5173, proxies /api + /gamehub to production
pnpm build        # Outputs to ../../King-of-the-Garbage-Hill/wwwroot
pnpm type-check   # vue-tsc --noEmit
pnpm lint         # eslint --fix
```
Env: `.env` sets `VITE_API_HOST` + `VITE_SIGNALR_HUB` (dev → production server). `.env.production` → empty (same-origin). Vitest configured but no test files yet.

### Deploy
`deploy_to_prod` script: `pnpm build` → `dotnet build --no-incremental` → tar + scp to EC2 → systemd service `kotgh`.

## Architecture

### Backend (King-of-the-Garbage-Hill/)
- **Program.cs** — Entry point: Discord bot thread + Kestrel in parallel
- **Global.cs** — Singleton: active `GamesList` + `FinishedGamesList`, shared between Discord and web
- **DI:** Lamar container, marker interfaces `IServiceSingleton` / `IServiceTransient`, auto-discovery via `AddSingletonAutomatically()`

**Game Logic (`Game/`) — CORE CODE:**
- `GameLogic/CharacterPassives.cs` — Central passive handler (~289KB, largest file)
- `GameLogic/CalculateRounds.cs` — Round resolution (reads from FightCharacter)
- `GameLogic/DoomsdayMachine.cs` — Fight execution, position swaps, end-of-round processing
- `GameLogic/BotsBehavior.cs` — Bot AI | `GameLogic/CheckIfReady.cs` — Turn readiness | `GameLogic/StartGameLogic.cs` — Game init
- `Characters/` — 36 character state files | `Classes/GameClass.cs` — Game state model
- `Classes/CharacterClass.cs` — Stats/abilities (~53KB), defines `Passive`, `JusticeClass`, `AvatarEventClass`
- `Classes/InGameStatusClass.cs` — Score tracking (`AddBonusPoints`/`GetScore`/`AddRegularPoints`), leaderboard, ForOneFight flags
- `Classes/PassivesClass.cs` — Per-player passive state | `Classes/GamePlayerBridgeClass.cs` — Links accounts to players, holds `GameCharacter` + `FightCharacter` + `Passives` + `Status`, contains `MinusPsycheLog`

**Web API (`API/`):** `GameHub.cs` (SignalR at `/gamehub`), `Controllers/GameController.cs` (`/api/game`), `Services/WebGameService.cs`, `Services/GameNotificationService.cs` (broadcasts to `game-{gameId}` groups), `Services/GameStateMapper.cs` (state→DTOs), `Services/GameStoryService.cs`

**Discord:** `DiscordFramework/CommandHandling.cs` (router), `GeneralCommands/General.cs` (`*st` starts game)

**Data:** No database — flat-file JSON via `UsersDataStorage.cs` (`File.ReadAllText`/`WriteAllText` + Newtonsoft). Accounts loaded into `ConcurrentDictionary` at startup. Files: `DataBase/UserAccounts/discordAccount-{id}.json`. Static assets: `DataBase/art/` → `/art/`, `DataBase/sound/` → `/sound/`. Character definitions: `DataBase/characters.json`.

### Frontend (Web/VueClient/)
Vue 3 Composition API `<script setup>`, strict TypeScript, Pinia `useGameStore()`. Key files: `store/game.ts`, `services/signalr.ts`, `services/sound.ts`. Pages: `Game.vue` (25KB), `Lobby.vue`, `Spectate.vue`, `Home.vue`. Routes: `/games`→Lobby, `/game/:gameId`→Game, `/spectate/:gameId`→Spectate. Web auth via Discord ID (string to avoid JS precision loss); web-only accounts via `RegisterWebAccount`.

## GameCharacter vs FightCharacter Architecture

Each `GamePlayerBridgeClass` holds two `CharacterClass` instances:
- **`GameCharacter`** — Persistent state. Lasting changes written here, take effect **next round** via DeepCopy.
- **`FightCharacter`** — Snapshot deep-copied from GameCharacter at fight start (`DeepCopyGameCharacterToFightCharacter`). `CalculateRounds.cs` reads **exclusively** from FightCharacter.

**DeepCopy:** `MemberwiseClone()` — value types independent. **`Status` is SHARED** (same instance). **`Justice` is SHARED**. `Passive` list is deep-copied.

**ForOneFight:** Temp stat override (sentinel `-228` = not set). Methods: `SetIntelligenceForOneFight`, `SetStrengthForOneFight`, `SetSpeedForOneFight`, `SetPsycheForOneFight`, `SetSkillForOneFight` (decimal), `SetJusticeForOneFight`, `AddSpeedForOneFight` (delta). Each sets a flag on shared `Status`. `ResetFight()` clears both copies.

### CRITICAL RULES
- **ForOneFight overrides MUST be set on `FightCharacter`**, not `GameCharacter` — CalculateRounds reads FightCharacter, so GameCharacter overrides have **no effect**: `me.FightCharacter.SetStrengthForOneFight(0, "MyPassive")` (correct) vs `me.GameCharacter.SetStrengthForOneFight(0, "MyPassive")` (WRONG)
- **Stat reads** in before-fight handlers: use `FightCharacter` to respect earlier ForOneFight overrides
- **Exception:** `Justice` is shared, so either side works for `SetJusticeForOneFight`
- **Persistent changes** (AddIntelligence, AddSkill, etc.): use `GameCharacter` — takes effect next round

## Key Conventions
- **Namespaces:** `King_of_the_Garbage_Hill.*` | **JSON:** `CamelCase` | **CORS:** localhost:5173, :3535, :80, kotgh.ozvmusic.com

## New Character Implementation Guide (~14 files)

1. **State Class** — `Game/Characters/{Name}.cs`: Inner classes for character-specific state (counters, cooldowns)
2. **Character Data** — `DataBase/characters.json`: Name, stats, Avatar, Tier, Description, Passive array (`PassiveName`, `PassiveDescription`, `Visible`)
3. **Per-Player State** — `Game/Classes/PassivesClass.cs`: Owner-only fields + per-player fields (debuffs/marks on ANY player)
4. **Phrases** — `Game/MemoryStorage/CharactersPhrases.cs`: `PhraseClass` fields, `SendLog(player, bool delete, prefix, isRandomOrder, suffix)`
5. **Passive Logic** — `Game/GameLogic/CharacterPassives.cs`: Add `case "PassiveName":` in handler methods (see hooks below)
6. **Fight Resolution** — `Game/GameLogic/DoomsdayMachine.cs`: Only if changing core fight mechanics. Block/skip section (~279), win branch (~581)
7. **Turn Injection** — `Game/GameLogic/CheckIfReady.cs`: Forced-attack logic, inject into `WhoToAttackThisTurn`
8. **Level-Up Override** — `Game/ReactionHandling/GameReactions.cs`: Custom level-up; use `Justice.AddJusticeForNextRoundFromSkill(int)`
9. **Bot Behavior** — `Game/GameLogic/BotsBehavior.cs`: `HandleBotMoralForPoints` + action-selection switch
10. **Discord Display** — `Game/DiscordMessages/GameUpdateMess.cs`: `CustomLeaderBoardBeforeNumber`/`AfterPlayer`
11. **DTOs** — `API/DTOs/GameStateDto.cs`: Create DTO, add to `PassiveAbilityStatesDto`. Per-player: follow `SellerMark` pattern
12. **Mapper** — `API/Services/GameStateMapper.cs`: In `MapPlayer()`, owner state in passive switch, per-player after SellerMark
13. **Frontend Types** — `Web/VueClient/src/services/signalr.ts`: TS interfaces on `PassiveAbilityStates`
14. **Frontend Widget** — `Web/VueClient/src/components/PlayerCard.vue`: Passive abilities section

### Passive Handler Hooks (execution order)
| Handler | When | Uses |
|---------|------|------|
| `HandleEventsBeforeFirstRound` | Game start | Initial setup |
| `HandleDefenseBeforeFight` / `HandleAttackBeforeFight` | Before fight | Buffs, ForOneFight overrides |
| `HandleAttackAfterFight` / `HandleDefenseAfterFight` | After fight | Outplay, counter-attack |
| `HandleDefenseAfterBlockOrFight` | After block or fight | Block-inclusive effects |
| `HandleDefenseAfterBlockOrFightOrSkip` | After any interaction | Always-trigger effects |
| `HandleCharacterAfterFight` | After fight (both sides) | Rewards, stat stealing |
| `HandleEndOfRound` | End of round (flags still set) | Cleanup, cooldowns |
| `HandleNextRound` | After `RoundNo++` | Per-round setup |
| `HandleNextRoundAfterSorting` | After leaderboard sort | Position-dependent effects |
| `HandleBotPredict` | After sorting | Bot AI predictions |
| `HandleShark` | During fight (from DoomsdayMachine) | Shark tracking |

### DoomsdayMachine End-of-Round Order
`HandleEndOfRound` → reset flags → `RoundNo++` → `HandleNextRound` → score sort → Tigr/PortalGun/HardKitty position swaps → LvlUp (rounds 3,5,7,9) + `SetPlaceAtLeaderBoard` + `RollSkillTargetForNextRound` → Ziggurat restore → Quality Drop → Round 10 win check → `SortGameLogs` → `HandleNextRoundAfterSorting` → `HandleBotPredict` → `RollExploit`

### Common Pitfalls
- **`Passive` constructor:** `new Passive(name, description, visible)` — NOT object initializer. Has `Standalone` property.
- **No `AddJustice`:** Use `AddJusticeForNextRoundFromSkill(int)` or `AddJusticeForNextRoundFromFight(int)` on `JusticeClass`
- **Score:** `AddBonusPoints(decimal, string)` → total; `AddRegularPoints(int, string, bool)` → per-round bucket; `GetScore()` → total
- **Psyche loss MUST use `MinusPsycheLog`:** `player.MinusPsycheLog(player.GameCharacter, game, -N, "PassiveName")` — checks "Спокойствие" immunity + writes global log. Never call `AddPsyche(-N)` directly (exceptions: passives with unique global logs like "Дизмораль", or non-rage buff reversals).
- **Transferred passives:** Add immunity checks to prevent unintended behavior (infinite loops)
- **Block/skip bypass:** If forcing fights on blocking players, check `WhoToAttackThisTurn.Count` in DoomsdayMachine's block/skip `continue`
- **Don't double-log:** `AddStrength`/`AddPsyche`/`AddExtraSkill`/`AddMoral`/`AddBonusPoints` auto-log to personal logs (default `isLog: true`). Don't also call `AddInGamePersonalLogs`. Pass `isLog: false` to suppress.
- **Personal vs Global logs:** `AddInGamePersonalLogs`/`SendLog` → personal (player only). `AddGlobalLogs` (on `GameClass`) → global (all players). Stat methods auto-log personal only.
