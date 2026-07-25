# Ктулху (Cthulhu) — New Character Implementation Plan


> **For agentic workers:** Execute task-by-task in order (superpowers:executing-plans or subagent-driven). All CLAUDE.md rules apply — especially: never edit a passive's logic without reading ALL its case blocks first; no `git commit` (commit message goes to `docs/commit-messages/`); docs updated in the same change-set.

**Executor note:** this plan is self-contained. Repo root = `/mnt/d/git/King-of-the-Garbage-Hill`. `B` = `King-of-the-Garbage-Hill/King-of-the-Garbage-Hill` (the C# project), `W` = `Web/VueClient`. All line anchors code-verified 2026-07-25 (spot-checked: `StartGameLogic.cs:306`, `DoomsdayMachine.cs:1609-1614`, `GameReactions.cs:789-800` — exact); re-locate by the quoted code if lines drifted.

## Context

The designer specced a new secret character **Ктулху** (full verbatim spec in the Appendix — its Russian texts are load-bearing and must be inserted with **no editing whatsoever**). Ктулху is a T-1 "god" who never fights himself: his player immediately becomes one of four adepts (Вестник конца) carrying a new passive **Морок**; wins drive enemies mad; total madness summons a cosmetic 7th board entity **Нечто**; and **Космический ужас** can end the game early with the Вестник forced to 1st. His presence must be hidden (secret roll, no achievements, no replays, masked identity). Three exploration agents mapped every precedent; the designer answered the open questions (see Appendix §Designer decisions) — notably: Нечто wins give **regular** points, the «Зов глубин» safety-valve stays even though it's unreachable in natural rolls, and the Ктулху→adept transform deliberately bypasses the ≤1-Tier-4 invariant.

## Goal

Add secret character **Ктулху** (Tier −1, `"BrowserCatalog": false`): a god who never fights. At game start his player picks one of four adepts ({mylorik, Братишка, Осьминожка, Краборак} minus those already in the game) via a blocking pre-game phase; the pick fully transforms him into that adept (**including `Name`** — his presence is invisible to others) plus a new passive **Морок**. Морок drives every fight-win into madness (psyche 0, capped, mad mark, 1 bonus-point steal, anonymous log). When all enemies are mad, a cosmetic 7th leaderboard row **Нечто** (10/10/10/10, justice 0) appears and can be attacked for 2 regular points. **Космический ужас** ends the game early (veil animation, Вестник forced 1st, no replay) when everyone has lost to Нечто at least once OR nobody attacked it for more than 1 turn. Safety-valve **«Зов глубин»**: if all four adepts are in one game (admin/sim only), each gets a full-screen да/нет prompt; four "да" summon Нечто with the same horror triggers (winner = current leader). Owner-only underwater UI theme. No achievements; stats recorded under the adept's name.

## Architecture

- **State home:** all match state lives on `GameClass.CthulhuState` (`Cthulhu.GameState`), because the adept transform is a **bridge replacement** (fresh `PassivesClass`) and the draft phase also swaps bridges. Nothing Ктулху-critical lives on `PassivesClass` except the `TransformedFromCthulhu` achievement-tracker bool (set post-swap).
- **Pre-game phases reuse the Draft-Pick machinery** (`game.IsDraftPickPhase` + `CheckIfReady` wait loop + deferred `HandleEventsBeforeFirstRound`). Every normal game already runs a draft phase (`WebGameService.EnableDraftPick = true`, `WebGameService.cs:28`; `General.cs:290`), so the adept choice is a "second draft stage" for the Ктулху player after everyone confirms. Non-draft creation sites (AdminPanel, BotGameFactory, the dormant non-draft branches) get a deferral guard.
- **Нечто is NOT a `GamePlayerBridgeClass`** in `PlayersList` (rejected: `PlayersList.Count==6` assumptions at `CheckIfReady.cs:315,1182`, 6-emote attack menu, phantom account settlement). It is: a Tier −2 helper entry in `characters.json` (never rollable: `GetRollableCharacters` filters `Tier >= -1`, `CharactersPull.cs:56-63`), a synthetic server-side `PlayerDto` (web), a post-loop appended line (Discord), and a cached off-roster bridge used only for read-only fight math.
- **Fight-win observer:** single-site `Cthulhu.HandleResolvedFight` next to `JonSnow.HandleResolvedFight` (`DoomsdayMachine.cs:1609-1614`) — chosen over duplicated hook cases because winner/loser are already resolved there for both attacker- and defender-side wins, and blocks/skips never reach it.
- **Secrecy:** unknown_bug precedent. `BrowserCatalog:false` → the vite plugin (`W/vite.config.ts:53-108`) strips the name, descriptions and all four passive texts from the public bundle. **Никогда** не пишите `'Ктулху'`, `'Морок'`, `'Нечто'` в `.vue`/`.ts` — client uses only neutral DTO fields (`isDeepSession`, `isBoardEntity`, `abyssSerial`, `statDisplayOverride`, `theme`, `draftPickHeading`, `depthsCallPromptActive`).

## Tech stack

.NET 10 (Lamar DI, Discord.Net 3.20) + ASP.NET Core SignalR · Vue 3 + TS + Pinia via `signalr.ts` · flat JSON storage · no test project (verification = `dotnet build`, `pnpm build`, `bash tools/audit-passives.sh`, `bash tools/verify-docs.sh --changed`, `bash tools/simulate.sh`).

## Global constraints (apply to every task)

1. **Verbatim texts.** Every player-facing Russian string in §"Verbatim texts" below is inserted **exactly as written, no edits** (designer: "Вставить текст описаний и фраз без какой-либо редактуры"). Descriptions stay vague; the precise mechanics go only to `docs/CHARACTERS.md`. Never edit `PassiveDescription`/`Description` beyond pasting these.
2. **Secrecy.** No private names in client code (see Architecture). Server-side C# may name Ктулху freely. The winner line already prints the adept name (`CheckIfReady.cs:662-664`) — no change needed. Do **NOT** add `Морок` to the passive-masking exemption list at `GameUpdateMess.cs:923` (so enemies see "Неизвестно"). All victim-facing mutators run with `isLog:false` or neutral source names (web personal logs are NOT passive-masked — `GameStateMapper.cs:1271-1273`).
3. **FightCharacter vs GameCharacter.** Persistent changes on `GameCharacter`; per-fight overrides on `FightCharacter`. The adept transform is a bridge replacement whose constructor re-snapshots `FightCharacter` (`GamePlayerBridgeClass.cs:17`), and rounds re-snapshot at `DoomsdayMachine.DeepCopyGameCharacterToFightCharacter` (`DoomsdayMachine.cs:161-167`) — the transform lands strictly before round 1's first DeepCopy because the phase blocks the game.
4. **DeepCopy.** `PsycheCappedAtZero` is a plain bool → carried by `MemberwiseClone` automatically; **no** `DeepCopy()` edit. Do not add new `List`/`Dictionary` fields to `CharacterClass`; all collection state goes on `GameClass.CthulhuState`.
5. **Psyche.** Морок's zeroing uses `SetPsyche(0, …, isLog:false)` + `PsycheCappedAtZero` — the `Tigr.ApplyRoundTenBan` direct-set precedent (`Tigr.cs:29`), **deliberately bypassing** the `MinusPsycheLog` immunity chain (documented exception like Дизмораль). Setter-level immunities (unknown_bug `CharacterClass.cs:1456`, Madara reanimated `:1457`) still reject the set — the separate mad **mark is always set** so the Нечто trigger works vs psyche-immune targets.
6. **No `AddJustice`.** Нечто fights change no justice at all (no `AddJusticeForNextRoundFromFight`).
7. **Points.** Нечто win = `AddRegularPoints(2, "Нечто")` (buffered, ×1/×2/×4 — designer decision). Морок steal = `AddBonusPoints(-1/+1, …)` (immediate, floors at 0, `InGameStatusClass.cs:227-253`), guarded `!UnknownBug.Is(target)` (Dopa Роум pattern `CharacterPassives.cs:2394-2413`).
8. **Stringly-typed dispatch.** New passive strings `"Культ"`, `"Морок"`, `"Нечто"`, `"Космический ужас"` are identifiers; declare them once as consts in `Cthulhu.cs` and use the consts everywhere. Run `bash tools/audit-passives.sh` after every string-bearing change; new ORPHAN/GHOST/BAD-NAME must be fixed or whitelisted with a finding ID.
9. **Docs in the same change-set** (Task 19). Commit message file only; **no `git commit`**.
10. Mad leaderboard icon and the Нечто row are **public by design** — documented exceptions (like 🚫/🔴🐯, WEB-BACKEND §7).

## Verbatim texts (single source of truth — copy exactly)

- Character `Description` (Ктулху): `Является богом`
  (The spec lines "[Тема игрового интерфейса — подводные глубины.]" and "Тир: T-1…" are implementation notes, not card text.)
- Passive `Культ`: `В начале игры предстоит выбрать одного из адептов и сделать его Вестником конца.`
- Passive `Морок`: `Победа Вестника над врагами сводит их с ума.\nТак же **крадет** 1 __бонусное__ очко.`
- Passive `Нечто`: `Когда весь мир сойдет с ума, с морских глубин придет Ң̷є̵ҫ̸т̶һ̴о̷`
- Passive `Космический ужас`: `Рядом с вечностью и смерть порою умирает.\nИ тогда придҀⷶ꜇ⷩⷬⷩⷴ҆ ꙁⷫⷩꚍⷬⷱ҆ꜿ ꚙ҆ꝕⷴꜽꝁⷫ҆ꚜꞇꝁⷴ ҩⷫ ꙉ҆ꚍ. Ҁⷱ꜇ⷴ ҏⷴꜽꚁꚛ҆ꙉ ꜽ ꚍⷱ꜇҆ꚃ҆ꚙꞇꝁ҆҆ Ꚋ ꜙꙉⷱꚍꞇꝁ҆ ꝕꚙꚍꚙ҆҆ ꙋꙉ҆ꚍꚍⷫⷱꚜꞇ.`
- Adept-choice heading (web overlay + Discord embed title): `Выбери адепта`
- Post-choice phrase to the Вестник (personal log): `Долго он в Р'льехе спит и видит сны...`
- Anonymous log to a maddened victim (no source): `ухлутк идубзар и нокимоноркен идйан`
- Depths prompt title/button: `Откликнуться на зов глубин`; answers `Да` / `Нет`
- Board bot display name: `Нечто`
- Reused generic action templates (existing game voice, not new flavor): `Вы напали на Нечто` (mirrors `Вы напали на игрока X`, `GameReactions.cs:919`), attack-menu option `Напасть на Нечто` (mirrors `"Напасть на " + username`, `GameUpdateMess.cs:1570`).
- ⚠ Two small UI strings had no designer text and are **flagged for designer review** in the commit message: the psyche-upgrade refusal message (use neutral `Выбери другой стат`) and we deliberately emit **no** global-log announcement when Нечто appears or is attacked (silent/spooky; WebFightLog row still animates).

## Identifier glossary (define once, use everywhere)

**C# — new file `B/Game/Characters/Cthulhu.cs`, static class `Cthulhu`:**
```csharp
public const string CharacterName = "Ктулху";
public const string Cult = "Культ";
public const string Morok = "Морок";
public const string Nechto = "Нечто";                 // passive AND helper-character AND bot display name
public const string CosmicHorror = "Космический ужас";
public const string NechtoAttackOption = "nechto-attack";   // Discord attack-select value
public const int NechtoPlace = 7;                            // web/bot sentinel place
public static readonly Guid NechtoRowId = new("00000000-0000-0000-0000-000000000007");
public static readonly string[] AdeptNames = { "mylorik", "Братишка", "Осьминожка", "Краборак" };

public sealed class GameState {
    public bool RosterHadCthulhu;            // set in GameClass ctor; survives transform
    public Guid HeraldPlayerId = Guid.Empty; // set by ApplyAdeptChoice (new bridge id)
    public bool AdeptStageActive;
    public bool DepthsCallStageActive;
    public bool DepthsCallResolved;
    public Dictionary<Guid, bool?> DepthsCallAnswers = new();   // adept playerId → null/да/нет
    public bool NechtoActive;
    public int NechtoActiveSinceRound;
    public HashSet<Guid> MadPlayerIds = new();
    public HashSet<Guid> NechtoLosses = new();
    public List<Guid> PendingNechtoAttackers = new();
    public bool NechtoAttackedThisRound;
    public int IdleRoundsWithoutNechtoAttack;
    public bool HorrorFired;
    public int AbyssSerial;                  // client veil watcher
    public GamePlayerBridgeClass NechtoBridge; // cached, NEVER added to PlayersList
}
```
Methods (implemented across tasks): `Is(string|CharacterClass|GamePlayerBridgeClass)`, `IsUntransformed(player)`, `IsHerald(GameClass, player)` (id match; Морок dispatch additionally checks `!player.Passives.PassiveAbilitiesDisabledByKimiko`, Sakura.cs:11-13 pattern), `FindHerald`, `IsNechtoActive(game)` (`NechtoActive && !HorrorFired`), `AllFourAdeptsPresent(IEnumerable<GamePlayerBridgeClass>)`, `RequiresPreGameStage(playersList)`, `AvailableAdepts(game, pull)`, `InjectMorok(CharacterClass adeptTemplate, CharactersPull pull)`, `ApplyAdeptChoice(game, player, adeptName, accounts, pull)`, `EnsureBotAdeptAutoPick(game, pull, random, accounts)`, `TryBeginAdeptStage(game, pull)`, `TryBeginDepthsCallStage(game)`, `SubmitDepthsAnswer(game, player, agree)`, `SubmitNechtoAttack(game, player)`, `ClearPendingNechtoAttack(game, player)`, `ResolveNechtoAttacks(game, calc, pull)`, `HandleResolvedFight(game, attacker, defender, winner, loser)`, `HandleEndOfRound(game)`, `FireCosmicHorror(game)`, `ApplyHeraldFinalPlacement(game)`, `ExcludeFromReplaysAndStory(game)` (bool helper: `RosterHadCthulhu || HorrorFired`).

**Other C# members:** `CharacterClass.PsycheCappedAtZero` (bool, `B/Game/Classes/CharacterClass.cs` next to `IntelligenceCappedAtZero` at `:66`) · `GameClass.CthulhuState` (`B/Game/Classes/GameClass.cs`, init in ctor) · `InGameAchievementTracker.TransformedFromCthulhu` (bool, next to `TransformedFromMylorik`, `B/Game/Classes/AchievementClass.cs` — grep `TransformedFromMylorik` for the class) · `WebGameService.DepthsCallChoice(ulong gameId, ulong discordId, bool agree)` · `GameHub.DepthsCallChoice(ulong gameId, bool agree)`.

**DTO (`B/API/DTOs/GameStateDto.cs`):** `GameStateDto.DraftPickHeading` (string, null default; requester-scoped) · `GameStateDto.AbyssSerial` (int, public) · `PlayerDto.IsBoardEntity` (bool, `[JsonIgnore(WhenWritingDefault)]`) · `PlayerDto.IsDeepSession` (bool, owner-only, `WhenWritingDefault`) · `PlayerDto.DepthsCallPromptActive` (bool, owner-only) · `CharacterDto.StatDisplayOverride` (string, null default, owner-only `"∞"`) · `PassiveDto.Theme` (string, null default; `"deep"` on the herald's Морок card, owner rows only).

**TS (`W/src/services/signalr.ts` types, camelCase):** `draftPickHeading`, `abyssSerial`, `isBoardEntity`, `isDeepSession`, `depthsCallPromptActive`, `statDisplayOverride`, `theme`; service method `depthsCallChoice(gameId, agree)`. **Store (`W/src/store/game.ts`):** computeds `isDeepSession`, `depthsCallPromptActive`; action `depthsCallChoice(agree: boolean)`.

**Discord custom-ids:** `draft_pick_3` (new switch case), `depths-yes`, `depths-no`; attack-select option value `nechto-attack`.

**CSS/Vue:** root class `is-deep-session` + layer `deep-caustics-layer` (`W/src/App.vue`) · `deep-highlight` skill-card class (`W/src/components/SkillsPanel.vue`) · ritual draft layout class `draft-ritual-layout` (`W/src/pages/Game.vue`) · new component `W/src/components/DeepVeil.vue`.

---

## Task 1 — characters.json entries + avatars

**Files:** `B/DataBase/characters.json`; avatar assets under `B/DataBase/art/avatars/`.

Add **two** entries (model: unknown_bug entry — it has `"BrowserCatalog": false`):

1. **Ктулху**: `Name:"Ктулху"`, `Intelligence:10, Psyche:10, Speed:10, Strength:10` (real values never matter — he cannot reach round 1 untransformed because the phase blocks the game; display is ∞ via Task 7), `Tier:-1`, `"BrowserCatalog": false`, `StoryAgent:""`, `Description` and 4 passives (`Культ`, `Морок`, `Нечто`, `Космический ужас`) with the **verbatim** texts above, all `"Visible": true`, **no** `Standalone` (must default false — Морок must NOT be Ziggurat-copyable). `Avatar`: `https://r2.ozvmusic.com/kotgh/art/avatars/cthulhu.png` (drop a placeholder file `cthulhu.png` into `B/DataBase/art/avatars/`; **flag in commit message: designer must supply art**).
2. **Нечто** (helper for the board row / fight math, never rollable): `Name:"Нечто"`, stats `10/10/10/10`, `Tier:-2` (excluded from rollable `Tier >= -1`, visible `Tier >= 0`, and admin pools; Tier −2 precedent: Молодой Глеб), `"BrowserCatalog": false`, `Description:""`, `StoryAgent:""`, `Passive: []`, `Avatar`: `…/nechto.png` (placeholder file; flag for designer).

Notes: `GetAramPassives` already excludes nothing from Tier −2 with an empty passive list (`CharactersPull.cs:72-91`) — no aram leak. Justice defaults to 0 on deserialization — "справедливости никогда нет" holds.

**Verify:** `dotnet build`; `bash tools/audit-passives.sh` — the four new passives will appear; expect ORPHAN warnings **until Task 2 lands the consts** (finish Tasks 1–2 before running the audit clean).

## Task 2 — Cthulhu.cs skeleton, GameClass state, PsycheCappedAtZero

**Files:** `B/Game/Characters/Cthulhu.cs` (new), `B/Game/Classes/GameClass.cs`, `B/Game/Classes/CharacterClass.cs`, `B/Game/Classes/AchievementClass.cs`.

1. Create `Cthulhu.cs` with the glossary consts, `GameState`, and pure helpers (`Is*`, `AllFourAdeptsPresent`, `RequiresPreGameStage`, `FindHerald`, `IsNechtoActive`). `AvailableAdepts(game, pull)`: `pull.GetAllCharactersNoFilter()` filtered to `AdeptNames` minus names present in `game.PlayersList` (compare `GameCharacter.Name`); if empty (admin forced Ктулху + all four — degenerate), return all four (duplicate allowed as admin-abuse fallback; document).
2. `GameClass` (`GameClass.cs:12-32` ctor): add
   ```csharp
   public Cthulhu.GameState CthulhuState { get; set; } = new();
   // in ctor, after PlayersList assignment:
   CthulhuState.RosterHadCthulhu = PlayersList.Any(Cthulhu.Is);
   ```
3. `CharacterClass`: add `public bool PsycheCappedAtZero { get; set; }` next to `IntelligenceCappedAtZero` (`:66`). Mirror the three intelligence cap lines into the psyche mutators, placed before the log/diff computation exactly like `:1299-1300`, `:1350-1351`, `:1382-1383`:
   - `AddPsyche` (`:1404`): `if (PsycheCappedAtZero && howMuchToAdd > 0) howMuchToAdd = 0;` (after the unknown_bug/Madara rejects at `:1406-1407`).
   - `SetPsyche` (`:1454`): `if (PsycheCappedAtZero && howMuchToSet > 0) howMuchToSet = 0;` — note: a **set to 0** from negative still works (DeepList `Безумие` negatives, `:1480-1481`), and the cap only blocks positive values, matching "не дает подняться выше нуля".
   - `SetPsycheForOneFight` (`:1495`): same guard.
   No `DeepCopy()` change (bool, per Global constraint 4).
4. `AchievementClass`: add `public bool TransformedFromCthulhu { get; set; }` to `InGameAchievementTracker` (same class as `TransformedFromMylorik`, used at `:835-836`).

**Verify:** `dotnet build` clean; `bash tools/audit-passives.sh` — no ORPHAN for the four passives (consts count as code references; the audit greps fixed strings across `B/Game`, `B/API`, `W/src`).

## Task 3 — Roll integration, draft exclusion, auto-confirm

**Files:** `B/Game/GameLogic/StartGameLogic.cs`, `B/Game/MemoryStorage/CharactersPull.cs`, `B/GeneralCommands/General.cs`, `B/API/Services/WebGameService.cs`, `B/Game/GameLogic/CheckIfReady.cs`.

1. **Natural-roll guard** — in the pool loop of `HandleCharacterRoll` (guards block `StartGameLogic.cs:292-307`), before the bot-tier line, add a guard modeled on `CanNaturallyRollNaruto` (`:38-45`, used at `:294-296`): implement `Cthulhu.CanNaturallyRoll(playersList, reservedCharacters, team)` returning false when all four adepts are already assigned/reserved or `team > 0` (no team games — документируемое ограничение), and `continue` when it returns false. (Sequential assignment means "already in the game" = assigned-so-far + reserved — good enough; the four-adepts case is unreachable naturally anyway due to the Tier-4 rule, `:348-353`.)
2. **Bot carve-out** — `StartGameLogic.cs:306`:
   ```csharp
   if (character.Tier < 4 && account.IsBot()
       && character.Name != "Кира" && !Cthulhu.Is(character.Name)) continue;
   ```
   Ктулху keeps his natural Tier −1 weight 40 (`GetRangeFromTier`, `:111`) and ordinary `CharacterChance` multipliers (Sakura model; store never lists him so it is 1.0 in practice) — no extra code.
3. **Draft options exclusion** — `RollDraftOptions` next to the unknown_bug removal (`:399`): `allCharacters.RemoveAll(x => Cthulhu.Is(x.Name));` (Нечто is Tier −2, already outside the pool).
4. **Draft-lobby auto-confirm** (silent lock, unknown_bug pattern): extend the three sites to also match Ктулху:
   - `General.cs:460`: `if (UnknownBug.Is(originalCharacter) || Cthulhu.Is(originalCharacter) || player.IsLootBoxCharacterReward)`
   - `WebGameService.cs:364-369`: add `|| Cthulhu.Is(originalCharacter)` to the confirmed branch.
   - `CheckIfReady.cs:1214` defensive loop — **careful, cult-stage interplay**: replace the selector with
     ```csharp
     foreach (var lockedPlayer in game.PlayersList.Where(p => UnknownBug.Is(p)
         || (Cthulhu.IsUntransformed(p) && !game.DraftOptions.ContainsKey(p.GetPlayerId()))))
     ```
     so the loop confirms Ктулху **only while he has no adept options** (otherwise it would re-confirm him every tick and skip the choice).
5. **Admin selectability** — `CharactersPull.GetAdminSelectableCharacters` (`:44-54`): after the unknown_bug append, also append the Ктулху entry (`allCharacters.FirstOrDefault(x => Cthulhu.Is(x.Name))`) so admin test games can force him. Do **not** append Нечто.

**Verify:** `dotnet build`. (Do not run sims yet — bot-Ктулху would deadlock round 1 until Task 4.)

## Task 4 — Adept-choice blocking stage + transform (server core)

**Files:** `B/Game/Characters/Cthulhu.cs`, `B/Game/GameLogic/CheckIfReady.cs` (`:1208-1257`), `B/GeneralCommands/General.cs` (`:291-295`, `:441-499`), `B/API/Services/WebGameService.cs` (`:397-413`), `B/GeneralCommands/AdminPanel.cs` (`:215-221`), `B/Game/Simulation/BotGameFactory.cs` (`:80-104`).

1. **`Cthulhu.InjectMorok(adeptTemplate, pull)`** — fetch the Морок `Passive` from the Ктулху JSON entry (`pull.GetAllCharactersNoFilter()`, find `Ктулху`, `.Passive.First(p => p.PassiveName == Morok).DeepCopy()` — single-sources the verbatim description; `.DeepCopy()` per convention `CharacterClass.cs:1714`). Then:
   - Осьминожка: **in-place slot replacement** (JonSnow.TryBecomeKing pattern, `JonSnow.cs:78-106`): `var i = template.Passive.FindIndex(p => p.PassiveName == "Чернильная завеса"); template.Passive[i] = morok;` (ink removal is clean: every ink path is PassiveName-gated — `CharacterPassives.cs:8037+`, `RestoreOctopusInk`, widget mapper `GameStateMapper.cs:668-673`).
   - Other adepts: `template.Passive.Insert(1, morok);` (visible second slot).
2. **`Cthulhu.ApplyAdeptChoice(game, player, adeptName, accounts, pull)`** — must be called **inside `lock(game)`** at its call sites. Sequence (bridge-replacement, mirrors `WebGameService.DraftSelect` `:508-556` minus the ZBS cost):
   ```csharp
   var template = pull.GetAllCharactersNoFilter().First(x => x.Name == adeptName); // fresh per-call instance
   InjectMorok(template, pull);
   var idx = game.PlayersList.IndexOf(player);
   var newBridge = new GamePlayerBridgeClass(template, new InGameStatus(),
       player.DiscordId, player.GameId, player.DiscordUsername, player.PlayerType)
   { IsWebPlayer = player.IsWebPlayer, PreferWeb = player.PreferWeb, TeamId = player.TeamId,
     Predict = player.Predict, DiscordStatus = player.DiscordStatus };
   newBridge.Status.IsDraftPickConfirmed = true;
   newBridge.Status.MoveListPage = 6;
   var account = accounts.GetAccount(player.DiscordId);
   if (account != null) lock (account) {
       newBridge.CharacterMasteryPoints = account.CharacterMastery.GetValueOrDefault(adeptName, 0);
       DoomGuy.InitializeForGame(newBridge, account);
       account.CharacterPlayedLastTime = adeptName;      // recorded under the ADEPT (decision #9)
   }
   game.PlayersList[idx] = newBridge;
   var exploitIdx = game.ExploitPlayersList.IndexOf(player);   // ExploitPlayersList re-point (GameReactions.cs:658-663)
   if (exploitIdx >= 0) game.ExploitPlayersList[exploitIdx] = newBridge;
   // Herald effects:
   var st = game.CthulhuState;
   st.HeraldPlayerId = newBridge.GetPlayerId();
   st.AdeptStageActive = false;
   game.DraftOptions.Remove(player.GetPlayerId());
   newBridge.Passives.AchievementTracker.TransformedFromCthulhu = true;
   newBridge.GameCharacter.SetPsyche(0, Cthulhu.Morok, false);   // Вестник сам лишается всей психики
   newBridge.GameCharacter.PsycheCappedAtZero = true;             // не может улучшать/поднимать психику
   st.MadPlayerIds.Add(newBridge.GetPlayerId());                  // знак безумца с начала игры
   foreach (var dl in game.PlayersList.Where(x => x.GameCharacter.Name == "DeepList"))
       st.MadPlayerIds.Add(dl.GetPlayerId());                     // DeepList: STATUS only, psyche untouched (decision #4)
   newBridge.Status.AddInGamePersonalLogs("Долго он в Р'льехе спит и видит сны...\n");  // verbatim
   ```
   NanobotsList is rebuilt by the existing draft-completion block (`CheckIfReady.cs:1232-1233`) — the stage completes before init, so no stale bridge remains.
   Deliberate consequence to document: the transform **bypasses the ≤1-Tier-4 invariant** (e.g. Краборак rolled + Ктулху picks Братишка → two Boole T4s; `StartGameLogic.cs:348-353`, `ARCHITECTURE.md:277`, Boole note near `CHARACTERS.md:299`).
3. **`Cthulhu.EnsureBotAdeptAutoPick(game, pull, random, accounts)`** — **unconditional** (DoomGuy.ApplyRandomModule model, `DoomGuy.cs:271-277`; NOT the L2-gated `EnsureBotPlaystyle`, `BotsBehavior.cs:145-147`): for every `IsUntransformed(p) && p.IsBot()` bridge, `ApplyAdeptChoice(..., options[random.Random(0, options.Count-1)].Name, ...)` with `options = AvailableAdepts(game, pull)`.
4. **`Cthulhu.TryBeginAdeptStage(game, pull)`** — returns true if it just opened the stage: requires a human `IsUntransformed` player with no `DraftOptions` entry; sets `game.DraftOptions[id] = AvailableAdepts(...)`, `player.Status.IsDraftPickConfirmed = false`, `MoveListPage = 6`, `st.AdeptStageActive = true`.
5. **CheckIfReady wait-loop wiring** — inside the `if (game.IsDraftPickPhase)` block (`CheckIfReady.cs:1210-1257`), after the (modified, Task 3.4) defensive loop:
   ```csharp
   Cthulhu.EnsureBotAdeptAutoPick(game, _charactersPull, _secureRandom, _accounts); // add DI deps to CheckIfReady
   if (game.PlayersList.All(x => x.Status.IsDraftPickConfirmed))
   {
       if (Cthulhu.TryBeginAdeptStage(game, _charactersPull))
       { foreach (var p in game.PlayersList.Where(p => p.PlayerType != 404)) await _upd.UpdateMessage(p); continue; }
       if (Cthulhu.TryBeginDepthsCallStage(game))                      // Task 13
       { foreach (...) await _upd.UpdateMessage(p); continue; }
       if (game.CthulhuState.DepthsCallStageActive && !game.CthulhuState.DepthsCallResolved)
           continue;                                                    // wait for да/нет
       /* existing completion block :1223-1254 unchanged — deferred
          HandleEventsBeforeFirstRound runs AFTER the transform, so the adept's
          round-1 passives (Искусство, Повторяет за myloran, …) fire for the herald */
   }
   continue;
   ```
6. **Creation-site deferral** — `RequiresPreGameStage(playersList)` = `Any(IsUntransformed) || AllFourAdeptsPresent`:
   - `General.cs:291`: `if (mode != "aram" && !isDraftPick && !Cthulhu.RequiresPreGameStage(playersList))` around the init; after game creation (`:441`), if `!isDraftPick && RequiresPreGameStage` → `game.IsDraftPickPhase = true; foreach p → p.Status.IsDraftPickConfirmed = true;` and extend `:495` to `if (mode == "normal" && !isDraftPick && !deferred)`. (Dead code while `EnableDraftPick` is const-true, but keeps the toggle safe.)
   - `WebGameService.CreateGame` else-branch (`:397-413`): same guard/deferral.
   - `AdminPanel.cs:215-221`: wrap the `HandleEventsBeforeFirstRound` call in the guard; after `new GameClass`, apply the same phase flags when required.
   - `BotGameFactory.cs:80`: wrap init in the guard; after game creation (`:87`), apply the phase flags. All-bot sims then resolve in one `TickAsync` pass (auto-pick → completion) — no deadlock; the stage costs ≤2 `ReadinessLoopVisits`, far under the 5-visit stuck threshold.

**Verify:** `dotnet build`; `bash tools/simulate.sh --characters "Ктулху,DeepList,mylorik,Братишка,Глеб,Sakura" --games 10` — must exit 0 (bot-Ктулху auto-picks Осьминожка or Краборак; herald plays a full game). Also `--games 40` natural smoke.

## Task 5 — Discord cult UI

**Files:** `B/Game/DiscordMessages/GameUpdateMess.cs` (`DraftPickPage :1939-1995`, `GetDraftPickButtons :1997-2023`), `B/Game/ReactionHandling/GameReactions.cs` (switch `:498-504`, `HandleDraftPick :576-678`).

1. `DraftPickPage`: before the confirmed branch, add a cult-stage branch: `if (game.CthulhuState.AdeptStageActive && Cthulhu.IsUntransformed(player))` → embed title **`Выбери адепта`** (verbatim), deep color (`new Color(12, 60, 90)`), one field per option (reuse the stats/passives field format `:1988-1991`, **no** cost labels). Confirmed-Ктулху branch (auto-confirm before the stage): keep the default confirmed rendering (`Ты выбрал: Ктулху` is only shown to the owner — acceptable).
2. `GetDraftPickButtons`: in the unconfirmed branch, when cult stage → buttons `draft_pick_0..N-1` labeled with adept names only (no `(FREE)`/cost).
3. `GameReactions` switch (`:500-503`): add `case "draft_pick_3":` to the fallthrough list.
4. `HandleDraftPick` (`:576`): inside `lock(game)`, immediately after `var selected = options[optionIndex];` (`:591`) — extract a cult branch BEFORE the normal validation (duplicates are ALLOWED here by design; restructure cleanly rather than using goto):
   ```csharp
   if (game.CthulhuState.AdeptStageActive && Cthulhu.IsUntransformed(player))
   {
       Cthulhu.ApplyAdeptChoice(game, player, selected.Name, _accounts, _charactersPull);
       // fall through to the trailing UpdateMessage only; skip the purchase path
   }
   ```
   The trailing `UpdateMessage` at `:676-677` then shows the waiting page; the wait-loop completes the game next tick.

**Verify:** `dotnet build`; manual: `*st <int>` admin test game with Ктулху (Task 3.5) — pick each adept, confirm Осьминожка loses Чернильная завеса, others gain Морок at slot 2, name/avatar/stats fully adept.

## Task 6 — Web cult UI

**Files:** `B/API/DTOs/GameStateDto.cs`, `B/API/Services/GameStateMapper.cs` (draft options mapping near `:105`), `B/API/Services/WebGameService.cs` (`DraftSelect :467-558`), `W/src/services/signalr.ts` (`:14-15` + `DraftOptionDto` type), `W/src/pages/Game.vue` (`:1330-1411`).

1. DTO: add `GameStateDto.DraftPickHeading`. Mapper: when the requesting player is the cult chooser (`game.CthulhuState.AdeptStageActive && Cthulhu.IsUntransformed(requestingPlayer)`) set `dto.DraftPickHeading = "Выбери адепта"` and map his `DraftOptions` (existing code already maps `game.DraftOptions` per player — it keys by `GetPlayerId()`, `GameStateDto.cs:20` + mapper `:105`), with `Cost = 0` for every option.
2. `WebGameService.DraftSelect`: inside `lock(game)` after `var selected = options.Find(...)` (`:484`), insert the same interception as Task 5.4 (call `Cthulhu.ApplyAdeptChoice`, `return (true, null)`), **before** the duplicate-character validation at `:493-497` (duplicates allowed) and before any ZBS logic.
3. `signalr.ts`: add `draftPickHeading?: string` to `GameState`.
4. `Game.vue`: extend the draft overlay (`:1330-1403`): when `store.gameState.draftPickHeading` is set, render an alternate `draft-ritual-layout` — heading `{{ store.gameState.draftPickHeading }}`, a responsive grid of up to 4 cards (avatar, name, stats, passives — same fields as the center panel `:1355-1380`), each with a PLAY button calling `store.draftSelect(option.name)`; **no** cost/Switch labels. Deep-sea styling (dark blue/teal gradient, subtle caustics via CSS only). Other players keep the standard "Waiting for other players..." overlay (`:1406-1411`) — indistinguishable from a normal draft.

**Verify:** `pnpm build` (from `W`); manual web game as Ктулху (auto-confirm path `WebGameService.cs:364` from Task 3.4): overlay appears, pick works, game starts, no private strings in the bundle: `grep -R "Ктулху\|Морок" King-of-the-Garbage-Hill/wwwroot/assets | wc -l` → 0.

## Task 7 — ∞ stat display (owner, pre-choice)

**Files:** `B/API/DTOs/GameStateDto.cs`, `B/API/Services/GameStateMapper.cs` (`MapCharacter :1022-1066`), `B/Game/DiscordMessages/GameUpdateMess.cs` (`FightPage` stats block `:1243-1251`), `W/src/services/signalr.ts`, `W/src/components/PlayerCard.vue`.

Chosen mechanism: a **neutral string override**, because real stats are irrelevant (he never reaches a fight) and a typed owner-only DTO string is the cleanest privacy-safe render path (no client name checks, no fragile `ExtraText` concatenation which renders `10∞`).

1. `CharacterDto.StatDisplayOverride` (glossary). In `MapCharacter`, when `isMe && Cthulhu.Is(character)` → `dto.StatDisplayOverride = "∞"`.
2. Discord: in the stats block at `GameUpdateMess.cs:1243-1251`, prepend a branch: if the player is `Cthulhu.IsUntransformed` → render all four stat lines as `∞` (server-side name check is fine).
3. `PlayerCard.vue`: where the four stat numbers render, if `player.character.statDisplayOverride` is set, show it instead of each numeric value (and skip resist/bonus sublabels). `signalr.ts`: add `statDisplayOverride?: string` to the character type.

**Verify:** `dotnet build` + `pnpm build`; web/Discord: owner sees ∞ ∞ ∞ ∞ pre-choice; after choice — normal adept stats.

## Task 8 — Морок runtime (madness, steal, cap, refusal, icon)

**Files:** `B/Game/Characters/Cthulhu.cs`, `B/Game/GameLogic/DoomsdayMachine.cs` (`:1609-1616`), `B/Game/ReactionHandling/GameReactions.cs` (`GetLvlUp` case 4 `:1452-1458`), `B/Game/GameLogic/BotsBehavior.cs` (`HandleLvlUpBot :4623-4670`), `B/Game/DiscordMessages/GameUpdateMess.cs` (`CustomLeaderBoardBeforeNumber :252-348`).

1. **Observer** — right after `JonSnow.HandleResolvedFight(...)` at `DoomsdayMachine.cs:1609-1614` (verified exact), add:
   ```csharp
   Cthulhu.HandleResolvedFight(game, player, playerIamAttacking, resolvedWinner, resolvedLoser);
   ```
   Implementation:
   ```csharp
   public static void HandleResolvedFight(GameClass game, GamePlayerBridgeClass attacker,
       GamePlayerBridgeClass defender, GamePlayerBridgeClass winner, GamePlayerBridgeClass loser)
   {
       var st = game.CthulhuState;
       if (st.HeraldPlayerId == Guid.Empty || winner == null || loser == null) return;
       if (winner.GetPlayerId() != st.HeraldPlayerId) return;
       if (winner.Passives.PassiveAbilitiesDisabledByKimiko) return;   // Морок is a passive
       if (loser.GetPlayerId() == winner.GetPlayerId()) return;

       // (a) madness — mark ALWAYS set, even vs psyche-immune targets (unknown_bug/Madara)
       var newlyMad = st.MadPlayerIds.Add(loser.GetPlayerId());
       loser.GameCharacter.SetPsyche(0, Morok, isLog: false);   // Tigr direct-set exception; setter immunities self-reject
       loser.GameCharacter.PsycheCappedAtZero = true;

       // (b) steal 1 bonus point (Dopa Роум model, CP:2394-2413)
       if (!UnknownBug.Is(loser)) {
           loser.Status.AddBonusPoints(-1, "Неизвестно");   // neutral source: web logs are unmasked
           winner.Status.AddBonusPoints(1, Morok);          // owner may see his own passive
       }

       // (c) anonymous personal log, verbatim, no source
       if (newlyMad) loser.Status.AddInGamePersonalLogs("ухлутк идубзар и нокимоноркен идйан\n");

       // Нечто appearance: all eligible enemies mad
       if (!st.NechtoActive && game.PlayersList
               .Where(p => p.GetPlayerId() != st.HeraldPlayerId && !p.Passives.IsDead)
               .All(p => st.MadPlayerIds.Contains(p.GetPlayerId())))
       { st.NechtoActive = true; st.NechtoActiveSinceRound = game.RoundNo; }
   }
   ```
   (Dead players are excluded from the trigger; mad players who die stay mad. No global log — silent by design, flagged.)
2. **Psyche-upgrade refusal (herald)** — `GetLvlUp` case 4 (`GameReactions.cs:1452-1458`), before the existing ≥10 check:
   ```csharp
   if (Cthulhu.IsHerald(game, player))
   { await _help.SendMsgAndDeleteItAfterRound(player, "Выбери другой стат", 0); return; }   // LvlUp10 pattern (:952-956): refuses WITHOUT consuming the point
   ```
   Web funnels through the same method (`WebGameService.LevelUp :782-795` → `HandleLvlUp` → `GetLvlUp`) — one site covers both.
3. **Bots skip psyche** — `HandleLvlUpBot` (`BotsBehavior.cs:4623`): in the Dumb path, gate `options.Add(4)` (`:4635`) with `&& !Cthulhu.IsHerald(game, player)`; in the smart path, remove the psyche entry from the `stats` list when herald (the existing M50 bank-guard `:4639` prevents spins if everything else is maxed).
4. **Mad icon** — `CustomLeaderBoardBeforeNumber` (`GameUpdateMess.cs:252-348`), viewer-independent (public exception):
   ```csharp
   if (game.CthulhuState.MadPlayerIds.Contains(player2.GetPlayerId())) customString += "🌀";
   ```
   🌀 proposed; **note in commit message that the designer may swap the emoji**. Unicode passes `ConvertDiscordToWeb` untouched (only `<:name:id>` are mapped, `WebGameService.cs:169-190`), so web renders it via the same `CustomLeaderboardPrefix` (`PopulateCustomLeaderboard`, `WebGameService.cs:196-228` — null-guards non-roster ids at `:210-211`).
5. **Masking checks** (no code, verify): `Морок` is NOT in the exemption list at `GameUpdateMess.cs:922-924` → Discord logs mask it as "Неизвестно" for enemies; the fight engine's own победа/поражение lines never name passives; nothing herald-side writes "Морок" into enemy-visible text (steal uses "Неизвестно", zeroing uses `isLog:false`).

**Verify:** `dotnet build`; sim matchup A (Task 17) — grep the sim report for exceptions; manual game: beat an enemy as herald → victim shows 🌀, psyche 0, stuck at 0 after level-ups, victim log has the reversed line and `-1 __бонусных__` from "Неизвестно"; unknown_bug victim: mark yes, psyche/steal no; herald cannot upgrade psyche and keeps his point.

## Task 9 — Нечто leaderboard row (Discord + web)

**Files:** `B/Game/DiscordMessages/GameUpdateMess.cs` (`LeaderBoard :191-250`), `B/API/Services/GameStateMapper.cs` (`ToDto` players loop), `W/src/services/signalr.ts`, `W/src/components/Leaderboard.vue`, `W/src/components/DeathNote.vue` (target list, if it iterates players).

1. Discord: at the end of `LeaderBoard` (after the `for` at `:222-247`, before `return players;`):
   ```csharp
   if (Cthulhu.IsNechtoActive(game) && !game.IsFinished)
       players += $"{Cthulhu.NechtoPlace}. Нечто\n\n";
   ```
   (Madara projected-board branch `:196-217` returns earlier — acceptable: rounds ≥11 are post-game.)
2. Web: in `GameStateMapper.ToDto` (both player and spectator overloads), after `dto.Players` is filled:
   ```csharp
   if (Cthulhu.IsNechtoActive(game) && !game.IsFinished)
       dto.Players.Add(new PlayerDto {
           PlayerId = Cthulhu.NechtoRowId, DiscordUsername = "Нечто", IsBoardEntity = true,
           Character = new CharacterDto { Name = "Нечто", Avatar = <nechto avatar url>, AvatarCurrent = ...,
               Intelligence = 10, Strength = 10, Speed = 10, Psyche = 10, Passives = new() },
           Status = new PlayerStatusDto { Place = Cthulhu.NechtoPlace, Score = 0 } });
   ```
   Real `PlayersList` is untouched → `Naruto.OrderLeaderboard` (`Naruto.cs:474-486`), predictions, Death Note server-side (id not in roster → naturally rejected) and `HandleLastRound` are unaffected. `PopulateCustomLeaderboard` skips the fake id via its null-guard (`WebGameService.cs:210-211`).
3. `signalr.ts`: `isBoardEntity?: boolean` on `Player`. `Leaderboard.vue`: rows sort by place (`:37-41`) — place 7 lands last automatically. For `isBoardEntity` rows: suppress the predict dropdown and stat-guess UI, never treat as masked (`isMasked` returns name!=='???' → fine), keep/enable the attack affordance when `canAttack` (emits `attack(7)` — existing emit shape `:25-28`). In `DeathNote.vue` and any other component iterating `players` for targeting, skip `isBoardEntity` rows.

**Verify:** `pnpm build`; manual: once all enemies mad, both UIs show a 7th row "Нечто"; predictions/Death Note never offer it.

## Task 10 — Attacking Нечто (sentinels, change-mind, bots)

**Files:** `B/Game/ReactionHandling/GameReactions.cs` (`HandleAttack :761-923`, change-mind `:238-260`), `B/Game/DiscordMessages/GameUpdateMess.cs` (`GetAttackMenu :1498-1576`), `B/API/Services/WebGameService.cs` (`ChangeMind :727+` — `Attack :579-609` needs no change), `B/Game/GameLogic/BotsBehavior.cs` (`HandleBotAttack` rule block `:1010-1012`, `TryForceRoundTenBossAttack :5017-5050`).

1. **Discord menu option** — in `GetAttackMenu` after the 6-emote loop (`:1563-1571`, before the `kratos-death` fallback `:1573`):
   ```csharp
   if (Cthulhu.IsNechtoActive(game) && game.RoundNo <= 10)
       attackMenu.AddOption("Напасть на Нечто", Cthulhu.NechtoAttackOption);
   ```
2. **Sentinel branch in `HandleAttack`** — replace the target resolution (`:789-798`, verified exact) with a form that checks the sentinel **before `Guid.Parse`** (`:792`; precedent for non-Guid values: `"kratos-death"`, `GameUpdateMess.cs:1573`):
   ```csharp
   GamePlayerBridgeClass whoToAttack = null;
   var nechtoIntent = false;
   if (!player.IsBot() && !player.Status.IsAutoMove) {
       var raw = string.Join("", button.Data.Values);
       if (raw == Cthulhu.NechtoAttackOption) nechtoIntent = true;
       else whoToAttack = game!.PlayersList.Find(x => x.GetPlayerId() == Guid.Parse(raw));
   } else if (botChoice == Cthulhu.NechtoPlace && Cthulhu.IsNechtoActive(game)) {
       nechtoIntent = true;    // web humans and bots both arrive here (web sets IsAutoMove, WebGameService.cs:594-596)
   } else whoToAttack = game!.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == botChoice);
   if (nechtoIntent) return Cthulhu.SubmitNechtoAttack(game, player);
   ```
   `SubmitNechtoAttack`: guards (`IsNechtoActive`, `game.RoundNo <= 10`, `!player.Passives.IsDead`, `!Madara.IsSealed(player)`, herald allowed too); effects: `Status.WhoToAttackThisTurn = new();` `IsBlock = false; IsSkip = false; IsReady = true;` `st.PendingNechtoAttackers.Add(player.GetPlayerId());` personal log + `ChangeMindWhat` = `"Вы напали на Нечто\n"`; returns true. **Consumes the whole turn** — Dopa Макро simplification: attacking Нечто completes the turn regardless of Макро (document). The web place-7 path needs no `WebGameService.Attack` edit (it already funnels `targetPlace` into `HandleAttack` as `botChoice`).
3. **Change-mind cleanup** — both handlers reset the pending mark: `GameReactions.cs` case `"change-mind"` (`:238-260`) and `WebGameService.ChangeMind` (`:727+`): `game.CthulhuState.PendingNechtoAttackers.Remove(player.GetPlayerId());`.
4. **Bots attack eagerly** — new sibling in `BotsBehavior`:
   ```csharp
   private async Task<bool> TryForceNechtoAttack(GamePlayerBridgeClass bot, GameClass game)
   {
       if (!Cthulhu.IsNechtoActive(game) || game.RoundNo > 10) return false;
       return await AttackPlayer(bot, Cthulhu.NechtoPlace);   // lands in the sentinel branch
   }
   ```
   Call it in `HandleBotAttack` **after** the round-10 boss rule (`:1012`) so the existing Monster/Eren designer exception keeps priority: `if (await TryForceNechtoAttack(bot, game)) return;` — all AI levels, unconditional ("боты охотно нападают").

**Verify:** `dotnet build`; sim matchup A: report shows games ending before round 11 (horror) with exit 0; manual Discord: menu shows the extra option; change-mind restores a normal turn.

## Task 11 — Нечто fight resolution (no side effects)

**Files:** `B/Game/Characters/Cthulhu.cs`, `B/Game/GameLogic/DoomsdayMachine.cs` (insert at `:1758-1760`, next to `HandleRumblingAfterFights`).

Insert after the fight loop, before `HandleEndOfRound` (`:1817`):
```csharp
_characterPassives.HandleRumblingAfterFights(game);
Naruto.SettleShadowClones(game);
Cthulhu.ResolveNechtoAttacks(game, _calculateRounds, _charactersPull);   // NEW
Cthulhu.HandleEndOfRound(game);                                          // NEW (Task 12)
```
`ResolveNechtoAttacks`:
```csharp
public static void ResolveNechtoAttacks(GameClass game, CalculateRounds calc, CharactersPull pull)
{
    var st = game.CthulhuState;
    if (st.PendingNechtoAttackers.Count == 0) { return; }
    st.NechtoBridge ??= new GamePlayerBridgeClass(
        pull.GetAllCharactersNoFilter().First(x => x.Name == Nechto),
        new InGameStatus(), 0, game.GameId, "Нечто", 404);
    foreach (var id in st.PendingNechtoAttackers.ToList())
    {
        var attacker = game.PlayersList.Find(x => x.GetPlayerId() == id);
        if (attacker == null || attacker.Passives.IsDead) continue;
        st.NechtoBridge.FightCharacter = st.NechtoBridge.GameCharacter.DeepCopy(); // fresh 10/10/10/10, Justice 0
        // Read-only resolution: mirror the attacker-vs-defender Step1→Step2→Step3 aggregation
        // from the real fight block (DoomsdayMachine.cs ~1120-1240), isLog:false, using
        // calc.CalculateStep1/2/3 (CalculateRounds.cs:57,418,441) — NO justice/moral/resist/
        // WhoToLost/skill mutations, NO hooks. Sum pointsWined; tie falls through to Step3's roll.
        var attackerWon = /* pointsWined > 0 */;
        if (attackerWon) attacker.Status.AddRegularPoints(2, Nechto);      // ×1/×2/×4 at end of round
        else st.NechtoLosses.Add(id);                                      // horror trigger (a)
        st.NechtoAttackedThisRound = true;
        game.WebFightLog.Add(new FightEntryDto {                            // synthetic row for the web animation
            AttackerName = attacker.DiscordUsername, AttackerCharName = attacker.GameCharacter.Name,
            AttackerAvatar = GameStateMapper.GetLocalAvatarUrl(attacker.GameCharacter.AvatarCurrent ?? attacker.GameCharacter.Avatar),
            DefenderName = "Нечто", DefenderCharName = "Нечто", DefenderAvatar = <nechto avatar>,
            Outcome = attackerWon ? "win" : "loss",
            WinnerName = attackerWon ? attacker.DiscordUsername : "Нечто",
            TotalPointsWon = attackerWon ? 2 : 0 });
    }
    st.PendingNechtoAttackers.Clear();
}
```
Implementation note for the executor: read the real fight block (`DoomsdayMachine.cs:~1120-1240`) end-to-end first (CLAUDE.md rule) and copy ONLY the step-orchestration arithmetic — `CalculateStep1(attacker, nechtoBridge, false)` is pure with `isLog:false` (verified: `CalculateRounds.cs:57-190` only mutates via `Status.AddFightingData` behind `isLog`). Step2 compares shared `Justice` instances (attacker's real justice vs Нечто's 0 — intended asymmetry); Step3 uses the game RNG (seeded-sim deterministic). Verify the `FightEntryDto` field names against the real construction site (`DoomsdayMachine.cs:807-822`) before writing. No game side effects either way beyond the two lines specified.

**Verify:** `dotnet build`; sim matchup A with `--seed 7 --games 5` twice → identical reports (determinism preserved); web: fight animation shows the synthetic entry.

## Task 12 — Космический ужас: triggers, early end, forced 1st, veil

**Files:** `B/Game/Characters/Cthulhu.cs`, `B/Game/GameLogic/CheckIfReady.cs` (`HandleLastRound`, insert near `:517-563`), `B/API/DTOs/GameStateDto.cs` + `B/API/Services/GameStateMapper.cs` (`AbyssSerial`), `W/src/services/signalr.ts`, `W/src/pages/Game.vue` (watcher near `:82-90`), `W/src/components/DeepVeil.vue` (new).

1. **`HandleEndOfRound(game)`** (called from Task 11's insertion point — runs after all fights each round):
   ```csharp
   var st = game.CthulhuState;
   if (!st.NechtoActive || st.HorrorFired || game.RoundNo >= 11) { st.NechtoAttackedThisRound = false; return; }
   if (game.RoundNo > st.NechtoActiveSinceRound)                       // only FULL rounds after appearance count
       st.IdleRoundsWithoutNechtoAttack = st.NechtoAttackedThisRound ? 0 : st.IdleRoundsWithoutNechtoAttack + 1;
   st.NechtoAttackedThisRound = false;
   var eligible = game.PlayersList.Where(p => !p.Passives.IsDead
       && p.GetPlayerId() != st.HeraldPlayerId).ToList();              // «Зов глубин»: HeraldPlayerId is Empty → all players
   var horrorA = eligible.Count > 0 && eligible.All(p => st.NechtoLosses.Contains(p.GetPlayerId()));
   var horrorB = st.IdleRoundsWithoutNechtoAttack >= 2;                // "дольше чем 1 ход" = 2 consecutive idle rounds; reset on any attack
   if (horrorA || horrorB) FireCosmicHorror(game);
   ```
   `FireCosmicHorror`: `st.HorrorFired = true; st.AbyssSerial++; game.IsFinished = true;` — `TickAsync` re-checks `IsFinished` and runs `HandleLastRound` (`CheckIfReady.cs:1170-1174`); the 300ms push broadcasts the serial before/with the finish (`GameNotificationService` timer). No global log (silent; documented).
2. **Forced placement** — in `HandleLastRound`, add a block alongside the Premade/Goblin forced-win precedents (`CheckIfReady.cs:521-563`), before `JonSnow.HandleFinalPosition` (`:565`):
   ```csharp
   Cthulhu.ApplyHeraldFinalPlacement(game);
   // = if (st.HorrorFired && st.HeraldPlayerId != Guid.Empty) {
   //     var herald = find; if herald null/dead → skip;
   //     var top = game.PlayersList.First().Status.GetScore();
   //     var diff = top - herald.Status.GetScore() + 1;
   //     if (diff > 0) herald.Status.AddBonusPoints(diff, CosmicHorror);
   //     game.PlayersList = Naruto.OrderLeaderboard(game.PlayersList);
   //     for (var k = 0; k < game.PlayersList.Count; k++) game.PlayersList[k].Status.SetPlaceAtLeaderBoard(k + 1);
   //   }
   ```
   **Score-inflation chosen over `JonSnow.MoveExactlyToIndex`**: (a) it is the exact precedent living at this call site; (b) `MoveExactlyToIndex` is private to JonSnow and a raw positional move would be undone by the score-ordered `Naruto.OrderLeaderboard` re-sorts that follow in `HandleLastRound` (`:423-427`, `:538`, `:559`); (c) the score column stays coherent (top + 1, not absurd). Others shift down automatically. «Зов глубин» games (`HeraldPlayerId == Empty`): **no shuffle** — the current leader is already 1st (designer decision #3).
3. **Veil animation** — DTO `AbyssSerial` mapped from `st.AbyssSerial`; `signalr.ts` `abyssSerial: number`. `Game.vue`: HL3-pattern watcher (model `:82-90`):
   ```ts
   watch(() => store.gameState?.abyssSerial, (s, prev) => {
     if (s == null || prev == null || s <= prev) return
     deepVeilVisible.value = true
     setTimeout(() => { deepVeilVisible.value = false }, 6000)
   })
   ```
   `DeepVeil.vue`: full-screen fixed overlay, z-index above the game-over podium (≥ 12000, HL3Release precedent), CSS-only: a black veil falling from the top (~1.5s) then the interface "sinking" (backdrop blur + blue-teal tint + slow translateY, ~4s), `prefers-reduced-motion` → simple fade. Plays for everyone (public serial), spectators included.

**Verify:** `dotnet build` + `pnpm build`; sim matchups A and B end early with exit 0; manual: veil plays, then the podium shows the herald's adept 1st; score column reads top+1.

## Task 13 — «Зов глубин» safety-valve

**Files:** `B/Game/Characters/Cthulhu.cs`, `B/Game/GameLogic/CheckIfReady.cs` (wired in Task 4.5), `B/API/Services/WebGameService.cs`, `B/API/GameHub.cs` (next to `DarksciChoice`, `:424-433`), `B/Game/DiscordMessages/GameUpdateMess.cs` (`DraftPickPage`/`GetDraftPickButtons`), `B/Game/ReactionHandling/GameReactions.cs` (component switch), `B/API/DTOs/GameStateDto.cs` + `GameStateMapper.cs`, `W/src/services/signalr.ts`, `W/src/store/game.ts`, `W/src/pages/Game.vue`.

**Pattern choice:** pre-game **draft-phase gate**, not the HL3 mid-round pause — it reuses the identical deferred-init machinery Task 4 already builds (one phase system, one completion path), has no deadline/timer edge cases (draft gate has no timeout by design, `CheckIfReady.cs:1221`), honors "с начала игры", and the scenario is admin/sim-only so blocking on a human answer is acceptable.

1. `TryBeginDepthsCallStage(game)` (called from Task 4.5 wiring): requires `AllFourAdeptsPresent(game.PlayersList) && !st.RosterHadCthulhu && !st.DepthsCallResolved && !st.DepthsCallStageActive`. Sets `DepthsCallStageActive = true`; for each of the four adept players: bots → `DepthsCallAnswers[id] = true` (**auto-"да"**); humans → `= null`. If all four already answered (all-bot) → resolve immediately. Resolution: when no `null` remains → `DepthsCallResolved = true; DepthsCallStageActive = false;` if **all four true** → `NechtoActive = true; NechtoActiveSinceRound = 1;` else nothing. The wait-loop `continue`s while `StageActive && !Resolved` (Task 4.5).
2. `SubmitDepthsAnswer(game, player, agree)`: guard stage active, player is an adept with a pending `null`; record; try resolve. `WebGameService.DepthsCallChoice(gameId, discordId, agree)`: `FindGameAndPlayer`, `lock(game)`, call it. `GameHub.DepthsCallChoice(ulong gameId, bool agree)`: clone of `DarksciChoice` (`GameHub.cs:424-433`), action name `"depthsCallChoice"`.
3. Discord UI: in `DraftPickPage`, branch when the requesting player has a pending depths answer → embed title **`Откликнуться на зов глубин`** (verbatim), deep color; `GetDraftPickButtons` → two buttons `Да` (`depths-yes`, Success) / `Нет` (`depths-no`, Danger). `GameReactions` component switch (next to `draft_pick_*`, `:498-504`): cases `"depths-yes"`/`"depths-no"` → `Cthulhu.SubmitDepthsAnswer(game, player, id == "depths-yes")` under `lock(game)` + `UpdateMessage`.
4. Web: `PlayerDto.DepthsCallPromptActive` set for `isMe` when pending. Store computed + action (glossary). `Game.vue`: when `store.gameState.isDraftPickPhase && store.myPlayer?.depthsCallPromptActive` → render a full-screen prompt overlay (reuse the draft overlay shell; focus-trap style from `HalfLife3Transition.vue` optional): title `Откликнуться на зов глубин`, buttons `Да`/`Нет` → `store.depthsCallChoice(true|false)`. Non-adepts keep the waiting overlay.
5. After resolution the standard completion block runs; if summoned, Нечто is visible from round 1, same 2-point attacks, same two triggers, horror winner = current table leader (Task 12.2 already handles `HeraldPlayerId == Empty`), same veil + replay exclusion.

**Verify:** `dotnet build` + `pnpm build`; sim matchup B (all-bot: four auto-да → Нечто from round 1 → horror ends the game) exit 0; manual admin test game with a human adept: prompt blocks until answered; "нет" → normal game.

## Task 14 — Underwater theme (owner-only) + Морок deep highlight

**Files:** `B/API/Services/GameStateMapper.cs`, `W/src/services/signalr.ts`, `W/src/store/game.ts` (`:145-147` selector block), `W/src/App.vue` (`:17`, `:176`, `:350-447`), `W/src/components/SkillsPanel.vue` (`:189-219`, CSS `:375-394`).

1. Mapper: on `isMe` rows set `PlayerDto.IsDeepSession = true` when `Cthulhu.IsUntransformed(player)` **or** `player.GetPlayerId() == game.CthulhuState.HeraldPlayerId`. On the herald's own `PassiveDto` for Морок set `Theme = "deep"` (in `MapCharacter`'s passive loop `:1085-1096`, only when `isMe` — pass the herald knowledge via a parameter or set it post-loop at the calling site that knows `game`).
2. `game.ts`: `const isDeepSession = computed(() => myPlayer.value?.isDeepSession ?? false)` next to `isTerminalMode` (`:145`); export it (`:1004` block).
3. `App.vue`: `const deepSession = computed(() => route.name === 'game' && store.isDeepSession)` (model `:17`); root class `{ 'is-deep-session': deepSession }` (`:176`); add a `deep-caustics-layer` div when active (model: `terminal-crt-layer`). CSS: copy the full `.app.is-terminal-session` override block (`:350-447`) as `.app.is-deep-session` with an abyssal palette (backgrounds `#020a14/#04121f`, text `#a8d8e8/#6fb3c9`, accents teal `#19c2b8` / bioluminescent `#3ee6c8`, borders `rgba(25,194,184,.25)`); caustics layer = slow-moving repeating radial-gradient shimmer, `prefers-reduced-motion` → static.
4. `SkillsPanel.vue`: add `'deep-highlight': passive.theme === 'deep'` to the skill-card class binding (`:194-199`; persistent, unlike the one-shot `jon-king-highlight`); CSS modeled on `.skill-card.jon-king-highlight` (`:375-394`) with the deep palette (teal glow border, name color `#3ee6c8`). `signalr.ts`: `theme?: string` on the passive type, `isDeepSession?: boolean` on `Player`.

**Verify:** `pnpm build`; owner sees the theme pre-choice AND after transform; other players (second browser as another seat) see the normal theme and NO Морок highlight.

## Task 15 — Replay / AI-story exclusion + text sanitization

**Files:** `B/API/Services/GameNotificationService.cs` (`:56`, `:89-91`), `B/API/Services/ReplayService.cs` (`:164-165`, `:322-327`), `B/API/Services/GameStoryService.cs` (`:52`), `B/API/Services/GameStateMapper.cs` (`SanitizePrivateCharacterText :1565-1576`).

**Recommendation implemented (state clearly in docs): exclude ALL games whose roster ever contained Ктулху, plus any game the horror ended** — `Cthulhu.ExcludeFromReplaysAndStory(game)` = `game.CthulhuState.RosterHadCthulhu || game.CthulhuState.HorrorFired`. Rationale: post-transform no player is named "Ктулху", so name-based roster checks cannot detect these games; a whole-roster exclusion is 3 edits, simpler, and hides the presence entirely (unknown_bug precedent). The spec minimum (event-ended games only) is strictly weaker and rejected.

1. `GameNotificationService.cs:56`: `if (game.PlayersList.Any(UnknownBug.Is) || Cthulhu.ExcludeFromReplaysAndStory(game)) return;`
2. Same guard added to the story call at `:89-91` (belt) and `GameStoryService.GenerateStoryAsync :52` (braces).
3. `ReplayService.BuildReplayData :164`: extend the throw condition with `|| Cthulhu.ExcludeFromReplaysAndStory(game)`. `ContainsPrivateRoster` (`:322-327`): also match `CharacterName == "Ктулху"` (legacy-file hygiene only; none will be saved).
4. `SanitizePrivateCharacterText` (`:1565`): extend the name array with `Cthulhu.CharacterName` (pre-transform logs only contain it in the owner's own view, but this is cheap insurance for chronicles).

**Verify:** `dotnet build`; finish a Ктулху game → no new replay file saved, no story generated; a normal game still saves.

## Task 16 — Achievements & statistics policy

**Files:** `B/Game/Classes/AchievementClass.cs` (`TrackGameEnd :820+`, adept flags `:833-836`).

- **No achievement definitions name Ктулху** — nothing to add; absence = no achievements (unknown_bug precedent, no guard needed).
- **Suppress adept character-story achievements for the Вестник** (mirror `TransformedFromMylorik` usage `:835-836`): wherever `TrackGameEnd` derives per-character flags for the four adepts (grep `"mylorik"`, `"Братишка"`, `"Осьминожка"`, `"Краборак"` inside `AchievementClass.cs`), AND-in `&& !tracker.TransformedFromCthulhu`. Global (g_*) achievements remain earnable. Read the whole `TrackGameEnd` before editing (CLAUDE.md rule).
- **Statistics/mastery decision (stated):** recorded **under the adept's name as a normal game, nothing under Ктулху** — this already falls out of the bridge swap (`GameCharacter.Name` = adept at settlement `CheckIfReady.cs:692-881`; mastery/`CharacterPlayedLastTime` set in Task 4.2). Document it.

**Verify:** `dotnet build`; finish a herald game → account gains adept mastery; no adept character achievement unlocks for the herald.

## Task 17 — Simulation support + forced line-ups

**Files:** `B/Game/Simulation/SimulationRunner.cs` (`ValidateMatchup :579-590`, `Fits :636-643`).

1. `ValidateMatchup`: allow the multi-Tier-4 «Зов глубин» exception — replace the `> 1` Tier-4 rejection (`:587-588`) with: reject unless (≤1 Tier-4) OR (the line-up contains **all four** of `Cthulhu.AdeptNames`). Leave `Fits` (coverage generator) unchanged — coverage keeps the ≤1-T4 invariant; only explicit `--characters` reaches the exception (mirrors the "admin force path" note in `ARCHITECTURE.md` §10).
2. Coverage already includes Tier −1 (`:123-124`) — bot-Ктулху resolves via Task 4 (auto-pick never empty in coverage: ≤1 T4 adept + possibly mylorik leaves ≥2 free adepts).
3. **Behavioral runs (record results in the commit message):**
   - `bash tools/simulate.sh --characters "Ктулху,DeepList,mylorik,Братишка,Глеб,Sakura" --games 25` — exercises: bot adept auto-pick (only Осьминожка/Краборак available), Морок vs DeepList (status-only seed) and normal victims, Нечто appearance, eager bot attacks, horror (a) and occasionally (b), veil finish, forced herald 1st. Expect exit 0; some games end before round 11 (visible in report `rounds`). Note: bots eagerly attack Нечто, so trigger (b) fires only rarely — trigger (a) is the organic path for bots; both matchups must simply complete without exceptions/freezes.
   - `bash tools/simulate.sh --characters "mylorik,Братишка,Осьминожка,Краборак,DeepList,Глеб" --games 25` — «Зов глубин»: four auto-да, Нечто from round 1, horror ends with the current leader winning.
   - `bash tools/simulate.sh --games 100 --coverage 1` — full regression incl. natural bot-Ктулху rolls.

**Verify:** all three runs exit 0; report JSONs in `B/DataBase/Simulations/` contain no failures/stuck games.

## Task 18 — Localization (mandatory)

**Files:** `B/DataBase/localization.en.json`.

Per LOCALIZATION.md §3 (startup joins `characters`/`passives` against `characters.json` — missing entries break the contract): add
- `characters`: `"Ктулху"` and `"Нечто"` → **literal copies of the Russian descriptions** (flagged for the designer in the commit message).
- `passives`: `"Культ"`, `"Морок"`, `"Нечто"`, `"Космический ужас"` → literal Russian copies (flagged).
- `exact`: `"Долго он в Р'льехе спит и видит сны..."`, `"ухлутк идубзар и нокимоноркен идйан"` (identical EN — reversed gibberish is intentional), `"Выбери адепта"`, `"Откликнуться на зов глубин"`, `"Напасть на Нечто"`, `"Вы напали на Нечто"`, `"Выбери другой стат"` → literal copies, flagged.
- **No `PhraseClass` group is added** (both phrases are emitted via `AddInGamePersonalLogs`) — deliberately avoiding the `phrases.en.json` startup contract (`PhraseLocalization.Populate` fails on missing/extra groups).
- Privacy note: the vite plugin strips catalog entries containing the new private terms (`vite.config.ts:34-48,79-97`) from the public bundle — intended; verify no unrelated catalog entry containing the substrings "Нечто"/"Морок"/"Культ" got collaterally stripped: run `bash tools/audit-localization.sh` and `pnpm build`, then grep the built bundle as in Task 6.

**Verify:** `dotnet run` starts without localization errors (or `dotnet build` + `bash tools/audit-localization.sh`); `pnpm build` clean.

## Task 19 — Documentation (maintenance contract) + audit + commit message

**Files:** `docs/CHARACTERS.md`, `docs/INTERACTION-MATRIX.md`, `docs/BALANCE-CONSTANTS.md`, `docs/ARCHITECTURE.md`, `docs/GAME-DESIGN.md`, `docs/WEB-BACKEND.md`, `docs/WEB-CLIENT.md`, `docs/DISCORD-INTERFACE.md`, `docs/PASSIVE-MAP.md` (generated), `tools/known-warnings.txt` (only if the audit demands), `docs/commit-messages/<today>.md`.

1. **CHARACTERS.md** — full Ктулху entry (Tier −1 secret section, near Sakura/unknown_bug): exact mechanics with `File.cs:line` anchors for: roll rules (bots allowed — Кира-style carve-out `StartGameLogic.cs:306`; cannot roll if all four adepts present; excluded from team games; excluded from draft options; auto-confirm), the adept stage (blocking, deferred init, full transform **including Name**, Осьминожка ink slot-swap, Морок not Standalone), Морок (win → SetPsyche(0)+`PsycheCappedAtZero`+mad mark always+steal 1 bonus (`!UnknownBug`)+anonymous verbatim log; herald psyche 0/capped/no upgrades; DeepList status-only; Kimiko disable), Нечто (appearance condition incl. dead-player exclusion, row place 7, attack = whole turn, 10/10/10/10 justice 0, win = 2 **regular** points, no other effects), Космический ужас (both triggers, idle threshold = 2 consecutive rounds, early end, herald forced 1st via score-inflation, «Зов глубин» winner = current leader), «Зов глубин», theme owner-only, no achievements, stats under adept, replays excluded. Mark the ⚠ deliberate exceptions (Tier-4 bypass; MinusPsycheLog bypass; Макро simplification; team-game exclusion).
2. **INTERACTION-MATRIX.md** — rows in every applicable table: *forces game end* (Космический ужас), *steals/copies points* (Морок −1 bonus, unknown_bug immune), *intercepts psyche* (Морок direct-set exception; unknown_bug/Madara setter immunity; mad mark unaffected), §6 transferred-passives note (Морок non-Standalone, never Ziggurat-copyable; Ктулху-born adept inherits all Name-keyed adept logic — `GameUpdateMess.cs:565`, `GordonFreeman.cs:142`, `Geralt.cs:344-361`, `CharacterPassives.cs:436`).
3. **BALANCE-CONSTANTS.md** — rows: Ктулху roll weight 40 (Tier −1, `StartGameLogic.cs:111`), Нечто reward 2 regular points, Нечто stats 10/10/10/10 justice 0, horror idle threshold 2 rounds, Морок steal 1 bonus.
4. **ARCHITECTURE.md** — §7: the documented **exception** to the ≤1-Tier-4 invariant (`:277` area); the new pre-game stage (cult/depths) in the draft-phase description; `GameClass.CthulhuState`; §10 sim ValidateMatchup exception.
5. **GAME-DESIGN.md** — madness/Нечто/early-end system section (public mad icon 🌀 as a deliberate public exception).
6. **WEB-BACKEND.md** — new DTO fields (glossary), hub method `DepthsCallChoice`, synthetic place-7 row + sentinel attack, `AbyssSerial`, replay/story exclusion extension, §7 hidden-info exceptions (mad icon public; Нечто public; everything else owner-only neutral flags).
7. **WEB-CLIENT.md** — `is-deep-session` theme, ritual draft layout, `DeepVeil.vue`, board-entity row handling, new `GameState` TS fields.
8. **DISCORD-INTERFACE.md** — custom-id catalog additions: `draft_pick_3`, `depths-yes`, `depths-no`, attack-select value `nechto-attack`; the cult/depths draft pages.
9. `bash tools/audit-passives.sh` — commit the regenerated `docs/PASSIVE-MAP.md`; if any of the four passives are flagged despite the consts, add `tools/known-warnings.txt` lines with a new finding ID (next free ID in `docs/AUDIT-FINDINGS.md`) — expected clean.
10. `bash tools/verify-docs.sh --changed` — fix any dead anchors introduced by the edits above.
11. **Commit message** → `docs/commit-messages/<today's date>.md` (add `-2` if the file exists). Include: the designer-review flags (both avatars are placeholders; EN localization entries are literal copies; 🌀 swappable; "Выбери другой стат" refusal text; silent Нечто appearance/attacks). **Do NOT `git commit`.**

## Task 20 — Final verification pass (run in order)

1. `cd King-of-the-Garbage-Hill/King-of-the-Garbage-Hill && dotnet build` — clean.
2. `cd Web/VueClient && pnpm build` — clean (type-check is broken; build is the check); then `grep -RIl "Ктулху\|Морок\|Космический" ../../King-of-the-Garbage-Hill/wwwroot/assets` → empty.
3. `bash tools/audit-passives.sh` — zero NEW warnings; PASSIVE-MAP committed.
4. `bash tools/audit-localization.sh` — clean.
5. The three sims from Task 17 — exit 0.
6. `bash tools/verify-docs.sh --changed` — clean.
7. Targeted play-test script (manual, listed in the commit message): admin test game exercising: adept pick each of 4; Морок win chain incl. unknown_bug + DeepList; Нечто attack + change-mind; horror (b) by not attacking 2 rounds; «Зов глубин» да and нет branches; deep theme + ∞ + veil on web; Discord parity.

---

## Trap checklist (verified mitigations)

| Trap | Mitigation |
|---|---|
| Transform must precede first `FightCharacter` DeepCopy | Bridge ctor re-snapshots (`GamePlayerBridgeClass.cs:17`); stage blocks the game until transform; rounds re-snapshot at `DoomsdayMachine.cs:161-167` |
| `PassivesClass` replaced on bridge swap | All Ктулху state on `GameClass.CthulhuState`; only `TransformedFromCthulhu` set post-swap on the new tracker |
| `CheckIfReady` wait-loop deadlock (bots/sims) | `EnsureBotAdeptAutoPick` is unconditional and runs at the top of the gate; depths answers auto-да for bots; defensive confirm loop gated on absent DraftOptions (Task 3.4) |
| Enemy passive masking must not expose Морок | NOT added to the exemption list (`GameUpdateMess.cs:923`); victim-side sources are `isLog:false`/"Неизвестно" (web logs unmasked, `GameStateMapper.cs:1271-1273`) |
| `PopulateCustomLeaderboard` fake ids | Existing null-guard `WebGameService.cs:210-211` skips `NechtoRowId` |
| 6-emote `_playerChoiceAttackList` bound | Нечто option added outside the loop (`GameUpdateMess.cs:1563-1573`) |
| `Guid.Parse` on Discord values | Sentinel checked before parse (`GameReactions.cs:792`; precedent `"kratos-death"`) |
| Нечто row breaking sorts/predictions/Death Note | Never in `PlayersList`; DTO-only; `isBoardEntity` client suppression; server rejects unknown ids naturally |
| Horror end vs `HandleLastRound` settlement | `IsFinished=true` only; settlement runs on the real 6 bridges; forced placement uses the in-file precedent block |
| Sim ≤1-T4 validation blocks «Зов глубин» line-up | `ValidateMatchup` exception for the exact four-adept quartet (Task 17.1) |

## Self-review checklist (spec coverage vs verbatim spec)

- [x] Stats ∞ ×4 (Task 7); "Является богом" description (Task 1); Tier −1 secret, **drops to bots** (`StartGameLogic.cs:306` carve-out, Task 3.2); underwater theme (Task 14).
- [x] Культ: 4 adepts minus taken ("если не заняты"), blocking pre-first-move widget titled "Выбери адепта", full transform replacing Ктулху, Морок added (deep-sea color, Task 14.4), Осьминожка loses Чернильная завеса via slot replacement, phrase "Долго он в Р'льехе спит и видит сны..." (Task 4).
- [x] Морок: win → psyche removed and capped at 0 (`PsycheCappedAtZero` mirrored into all 3 mutators), mad table icon (🌀, public exception), steal 1 **бонусное** очко, DeepList изначально безумен (status only, decision #4), Вестник psyche 0 + mad icon from start + cannot upgrade psyche (refusal without spending, bots skip), anonymous verbatim phrase to victims (Tasks 2, 8).
- [x] Нечто: appears when every enemy is mad (immune targets counted via mark; dead excluded — documented), cosmetic 7th row / not a character / no passives interaction, attackable (event, not a game attack; consumes the turn), 10/10/10/10, justice always 0, win = 2 points (regular, decision #2) and nothing else, never moves/attacks, always fake place 7 (Tasks 9–11).
- [x] Космический ужас: trigger (a) everyone lost ≥1 to Нечто; trigger (b) no attacks for more than 1 turn (crisp: 2 consecutive full idle rounds, reset on any attack); black veil + sinking animation for all; game ends on the current round; Вестник → 1st, others shift down; hidden from replays (whole-roster recommendation stated); боты охотно нападают (Tasks 10.4, 12, 15).
- [x] Roll restriction (all four adepts present) + «Зов глубин» alternative: full-screen да/нет "Откликнуться на зов глубин", bots auto-да, all-да → Нечто at place 7, same triggers, winner = current leader (Tasks 3.1, 13).
- [x] Прочее: no achievements, presence hidden (name transform, masking, replay/story exclusion, bundle privacy) (Tasks 15–16).
- [x] No placeholders; identifier usage cross-checked (`nechto-attack`, `NechtoPlace=7`, `NechtoRowId`, `depths-yes/no`, `draft_pick_3`, `is-deep-session`, `deep-highlight`, `abyssSerial`, `isBoardEntity`, `isDeepSession`, `depthsCallPromptActive`, `statDisplayOverride`, `draftPickHeading`, `PsycheCappedAtZero`, `TransformedFromCthulhu`, `CthulhuState`).
- [x] Flagged-for-designer items explicitly listed (avatars, EN literals, 🌀, refusal text, silent-appearance choice) — nothing invented silently.

---

# Appendix A — Verbatim designer spec (source of truth, НЕ РЕДАКТИРОВАТЬ)

Ктулху
Интеллект: ∞
Сила: ∞
Скорость: ∞
Психика: ∞

[Является богом]

Тир: T-1
[Находится среди секретных персонажей, но при этом может выпадать ботам!]

[Тема игрового интерфейса должна быть оформлена под подводные глубины.]

**Культ**
В начале игры предстоит выбрать одного из адептов и сделать его Вестником конца.

[На выбор предстоит 4 адепта: Братишка, Осьминожка, Краборак, mylorik. (если не заняты)]
[В начале игры требуется выбрать одного из них прежде чем совершить ход: "Выбери адепта". При выборе адепта, выбранный персонаж становится играбельным персонажем вместо Ктулху, однако у выбранного появляется новая пассивка Морок, выделенная глубинно морским цветом. (Осьминожка при этом теряет пассивку Чернильная завеса и заменяется на Морок)]

[Фраза после выбора адепта:]
"Долго он в Р'льехе спит и видит сны..."

**Морок**
Победа Вестника над врагами сводит их с ума.
Так же **крадет** 1 __бонусное__ очко.

[Победа над врагом лишает его психики и не дает ей подняться выше нуля. В таблице отмечается значком сумасшедшего.]
[DeepList изначально считается сумасшедшим.]
[Игровой персонаж Вестник так же лишается всей своей психики, помечаемый знаком безумца с самого начала игры, и не может улучшать психику.]

Фраза для врагов при сведении с ума (не указывая источник):
"ухлутк идубзар и нокимоноркен идйан"

**Нечто**
Когда весь мир сойдет с ума, с морских глубин придет Ң̷є̵ҫ̸т̶һ̴о̷

[Когда каждый из врагов находится в статусе безумца, появляется седьмое место таблицы и на нем бот под ником "Нечто". Это седьмое место является косметическим, оно не влияет на механику мест в таблице. "Нечто" не является персонажем, не учавствует в пассивках персонажей и не имеет никакой другой функции кроме этого ивента.]
[На Нечто можно напасть, но это будет не игровое нападение, а часть ивента. Статы Нечто 10, 10, 10, 10. И никогда нет справедливости. Победы над Нечто приносят 2 очка, но больше ничего! Само Нечто не ходит и не атакует, всегда на фэйковом 7м месте таблицы.]

**Космический ужас**
Рядом с вечностью и смерть порою умирает.
И тогда придҀⷶ꜇ⷩⷬⷩⷴ҆ ꙁⷫⷩꚍⷬⷱ҆ꜿ ꚙ҆ꝕⷴꜽꝁⷫ҆ꚜꞇꝁⷴ ҩⷫ ꙉ҆ꚍ. Ҁⷱ꜇ⷴ ҏⷴꜽꚁꚛ҆ꙉ ꜽ ꚍⷱ꜇҆ꚃ҆ꚙꞇꝁ҆҆ Ꚋ ꜙꙉⷱꚍꞇꝁ҆ ꝕꚙꚍꚙ҆҆ ꙋꙉ҆ꚍꚍⷫⷱꚜꞇ.

[Если каждый из врагов (не считая адепта Вестника) проиграет против Нечто хотя бы один раз, на игровой интерфейс для всех участников падает черная пелена, с анимацией, будто весь интерфейс ушел под воду и через несколько секунд утонул на самом дне пучины. Игра обрывается на том ходу, который был сейчас. Адепт Вестник перемещается на первое место, опуская всех остальных на один. И Ктулху выигрывает, остальные заканчивают игру на соответствующих местах. Игра не показывается в реплеях. Боты в игре охотно должны нападать на Нечто ради заработка очков.]
[Если никто не нападал на Нечто дольше чем 1 ход, то Космический ужас так же срабатывает и завершает игру.]

[Ктулху не может выпасть в игре, где есть и mylorik, и Братишка, и Осьминожка, и Краборак одновременно. Вместо этого, если эта четверка соберется в одной игре, у каждого из них с самого начала появиться кнопка на весь экран "Откликнуться на зов глубин". и выбор "да/нет". Боты автоматически выбирают "Да". Если каждый из четырех выбрал "да", то на седьмом месте таблицы появляется "Нечто".]

[Не делать для персонажа никаких ачивок! Его присутствие должно быть скрыто в игре.]

# Appendix B — Designer decisions (Q&A, 2026-07-25)

1. **«Зов глубин» / Tier-4:** implement the event AS-IS; do NOT touch the Tier-4 uniqueness rule. Designer's mental model: Ктулху **спавнит буля в игру** — the adept choice list = the four adepts MINUS those already in the game (no duplicate characters ever). The Ктулху→adept transform therefore deliberately BYPASSES the ≤1-Tier-4-per-game invariant (intended; document as exception). «Зов глубин» is a deliberate safety-valve («затычка») for the unforeseen case where all four are taken — keep it even though natural rolls can't reach it.
2. **Очки за Нечто: REGULAR points** — buffered, multiplied ×1/×2/×4 by round.
3. **«Зов глубин» game (no Вестник): Космический ужас FIRES** — same triggers; winner = the current table leader at the moment the horror fires.
4. **DeepList: status only** — mad icon + counts toward the Нечто trigger from round 1; psyche follows normal rules until the Вестник actually beats him.
5. **Underwater theme:** owner-only (recommendation, not asked) — visible to the Ктулху player alone; keeps presence hidden.
