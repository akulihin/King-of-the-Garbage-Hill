using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.API.DTOs;
using King_of_the_Garbage_Hill.API.Services;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class Cthulhu
{
    public const string CharacterName = "Ктулху";
    public const string Cult = "Культ";
    public const string Morok = "Морок";
    public const string Nechto = "Нечто";
    public const string CosmicHorror = "Космический ужас";
    public const string NechtoAttackOption = "nechto-attack";
    public const int NechtoPlace = 7;
    public static readonly Guid NechtoRowId =
        new("00000000-0000-0000-0000-000000000007");
    public static readonly string[] AdeptNames =
        { "mylorik", "Братишка", "Осьминожка", "Краборак" };

    public sealed class GameState
    {
        public bool RosterHadCthulhu;
        public Guid HeraldPlayerId = Guid.Empty;
        public bool AdeptStageActive;
        public bool DepthsCallStageActive;
        public bool DepthsCallResolved;
        public Dictionary<Guid, bool?> DepthsCallAnswers = new();
        public bool NechtoActive;
        public int NechtoActiveSinceRound;
        public HashSet<Guid> MadPlayerIds = new();
        public HashSet<Guid> NechtoLosses = new();
        public List<Guid> PendingNechtoAttackers = new();
        public bool NechtoAttackedThisRound;
        public int IdleRoundsWithoutNechtoAttack;
        public bool HorrorFired;
        public int AbyssSerial;
        public GamePlayerBridgeClass NechtoBridge;
    }

    public static bool Is(string characterName) => characterName == CharacterName;
    public static bool Is(CharacterClass character) => Is(character?.Name);
    public static bool Is(GamePlayerBridgeClass player) => Is(player?.GameCharacter);
    public static bool IsUntransformed(GamePlayerBridgeClass player) => Is(player);

    public static bool IsHerald(GameClass game, GamePlayerBridgeClass player) =>
        game != null
        && player != null
        && game.CthulhuState.HeraldPlayerId != Guid.Empty
        && player.GetPlayerId() == game.CthulhuState.HeraldPlayerId;

    public static GamePlayerBridgeClass FindHerald(GameClass game) =>
        game?.PlayersList.Find(player => IsHerald(game, player));

    public static bool IsNechtoActive(GameClass game) =>
        game?.CthulhuState.NechtoActive == true
        && !game.CthulhuState.HorrorFired;

    public static bool AllFourAdeptsPresent(IEnumerable<GamePlayerBridgeClass> players)
    {
        var names = players?.Select(player => player.GameCharacter.Name).ToHashSet()
                    ?? new HashSet<string>();
        return AdeptNames.All(names.Contains);
    }

    public static bool RequiresPreGameStage(IEnumerable<GamePlayerBridgeClass> players)
    {
        var list = players?.ToList() ?? new List<GamePlayerBridgeClass>();
        return list.Any(IsUntransformed) || AllFourAdeptsPresent(list);
    }

    public static bool CanNaturallyRoll(
        IEnumerable<GamePlayerBridgeClass> assignedPlayers,
        IEnumerable<CharacterClass> reservedCharacters,
        int team)
    {
        if (team > 0) return false;
        var names = (assignedPlayers ?? Enumerable.Empty<GamePlayerBridgeClass>())
            .Select(player => player.GameCharacter.Name)
            .Concat((reservedCharacters ?? Enumerable.Empty<CharacterClass>())
                .Select(character => character.Name))
            .ToHashSet();
        return !AdeptNames.All(names.Contains);
    }

    public static List<CharacterClass> AvailableAdepts(GameClass game, CharactersPull pull)
    {
        var occupied = game.PlayersList
            .Where(player => !IsUntransformed(player))
            .Select(player => player.GameCharacter.Name)
            .ToHashSet();
        var available = pull.GetAllCharactersNoFilter()
            .Where(character => AdeptNames.Contains(character.Name)
                                && !occupied.Contains(character.Name))
            .ToList();
        return available.Count > 0
            ? available
            : pull.GetAllCharactersNoFilter()
                .Where(character => AdeptNames.Contains(character.Name))
                .ToList();
    }

    public static void InjectMorok(CharacterClass adeptTemplate, CharactersPull pull)
    {
        var source = pull.GetAllCharactersNoFilter().First(Is);
        var morok = source.Passive.First(passive => passive.PassiveName == Morok).DeepCopy();
        if (adeptTemplate.Name == "Осьминожка")
        {
            var inkIndex = adeptTemplate.Passive.FindIndex(
                passive => passive.PassiveName == "Чернильная завеса");
            if (inkIndex >= 0)
                adeptTemplate.Passive[inkIndex] = morok;
            else
                adeptTemplate.Passive.Insert(Math.Min(1, adeptTemplate.Passive.Count), morok);
            return;
        }

        adeptTemplate.Passive.Insert(Math.Min(1, adeptTemplate.Passive.Count), morok);
    }

    public static GamePlayerBridgeClass ApplyAdeptChoice(
        GameClass game,
        GamePlayerBridgeClass player,
        string adeptName,
        UserAccounts accounts,
        CharactersPull pull)
    {
        if (!IsUntransformed(player) || !AdeptNames.Contains(adeptName))
            return null;

        var available = AvailableAdepts(game, pull);
        var template = available.FirstOrDefault(character => character.Name == adeptName);
        if (template == null) return null;
        InjectMorok(template, pull);

        var playerIndex = game.PlayersList.IndexOf(player);
        if (playerIndex < 0) return null;

        var newBridge = new GamePlayerBridgeClass(
            template,
            new InGameStatus(),
            player.DiscordId,
            player.GameId,
            player.DiscordUsername,
            player.PlayerType)
        {
            IsWebPlayer = player.IsWebPlayer,
            PreferWeb = player.PreferWeb,
            TeamId = player.TeamId,
            Predict = player.Predict,
            DiscordStatus = player.DiscordStatus,
            IsMobile = player.IsMobile,
            IsLootBoxCharacterReward = player.IsLootBoxCharacterReward,
            AiDifficulty = player.AiDifficulty,
            AiPlaystyle = player.AiPlaystyle,
            ConsecutiveBotBlocks = player.ConsecutiveBotBlocks,
            AiKnowledge = player.AiKnowledge
        };
        newBridge.Status.IsDraftPickConfirmed = true;
        newBridge.Status.MoveListPage = 6;

        var account = accounts.GetAccount(player.DiscordId);
        if (account != null)
        {
            lock (account)
            {
                newBridge.CharacterMasteryPoints =
                    account.CharacterMastery.GetValueOrDefault(adeptName, 0);
                DoomGuy.InitializeForGame(newBridge, account);
                account.CharacterPlayedLastTime = adeptName;
            }
        }

        game.PlayersList[playerIndex] = newBridge;
        var exploitIndex = game.ExploitPlayersList.IndexOf(player);
        if (exploitIndex >= 0)
            game.ExploitPlayersList[exploitIndex] = newBridge;

        var state = game.CthulhuState;
        state.HeraldPlayerId = newBridge.GetPlayerId();
        state.AdeptStageActive = false;
        game.DraftOptions.Remove(player.GetPlayerId());
        newBridge.Passives.AchievementTracker.TransformedFromCthulhu = true;
        newBridge.GameCharacter.SetPsyche(0, Morok, false);
        newBridge.GameCharacter.PsycheCappedAtZero = true;
        state.MadPlayerIds.Add(newBridge.GetPlayerId());
        foreach (var deepList in game.PlayersList.Where(
                     candidate => candidate.GameCharacter.Name == "DeepList"))
            state.MadPlayerIds.Add(deepList.GetPlayerId());
        newBridge.Status.AddInGamePersonalLogs(
            "Долго он в Р'льехе спит и видит сны...\n");
        return newBridge;
    }

    public static void EnsureBotAdeptAutoPick(
        GameClass game,
        CharactersPull pull,
        SecureRandom random,
        UserAccounts accounts)
    {
        foreach (var bot in game.PlayersList
                     .Where(player => IsUntransformed(player) && player.IsBot())
                     .ToList())
        {
            var options = AvailableAdepts(game, pull);
            if (options.Count == 0) continue;
            var selected = options[random.Random(0, options.Count - 1)];
            ApplyAdeptChoice(game, bot, selected.Name, accounts, pull);
        }
    }

    public static bool TryBeginAdeptStage(GameClass game, CharactersPull pull)
    {
        var player = game.PlayersList.FirstOrDefault(candidate =>
            IsUntransformed(candidate)
            && !candidate.IsBot()
            && !game.DraftOptions.ContainsKey(candidate.GetPlayerId()));
        if (player == null) return false;

        game.DraftOptions[player.GetPlayerId()] = AvailableAdepts(game, pull);
        player.Status.IsDraftPickConfirmed = false;
        player.Status.MoveListPage = 6;
        game.CthulhuState.AdeptStageActive = true;
        return true;
    }

    public static bool TryBeginDepthsCallStage(GameClass game)
    {
        var state = game.CthulhuState;
        if (!AllFourAdeptsPresent(game.PlayersList)
            || state.RosterHadCthulhu
            || state.DepthsCallResolved
            || state.DepthsCallStageActive)
            return false;

        state.DepthsCallStageActive = true;
        state.DepthsCallAnswers.Clear();
        foreach (var adept in game.PlayersList.Where(player =>
                     AdeptNames.Contains(player.GameCharacter.Name)))
            state.DepthsCallAnswers[adept.GetPlayerId()] =
                adept.IsBot() ? true : null;
        TryResolveDepthsCall(game);
        return true;
    }

    public static bool SubmitDepthsAnswer(
        GameClass game,
        GamePlayerBridgeClass player,
        bool agree)
    {
        var state = game.CthulhuState;
        if (!state.DepthsCallStageActive
            || !state.DepthsCallAnswers.TryGetValue(player.GetPlayerId(), out var answer)
            || answer != null)
            return false;
        state.DepthsCallAnswers[player.GetPlayerId()] = agree;
        TryResolveDepthsCall(game);
        return true;
    }

    private static void TryResolveDepthsCall(GameClass game)
    {
        var state = game.CthulhuState;
        if (state.DepthsCallAnswers.Count != AdeptNames.Length
            || state.DepthsCallAnswers.Values.Any(answer => answer == null))
            return;
        state.DepthsCallStageActive = false;
        state.DepthsCallResolved = true;
        if (state.DepthsCallAnswers.Values.All(answer => answer == true))
        {
            state.NechtoActive = true;
            state.NechtoActiveSinceRound = 1;
        }
    }

    public static bool SubmitNechtoAttack(GameClass game, GamePlayerBridgeClass player)
    {
        if (!IsNechtoActive(game)
            || game.RoundNo > 10
            || player.Passives.IsDead
            || Madara.IsSealed(player))
            return false;

        player.Status.WhoToAttackThisTurn = new List<Guid>();
        player.Status.IsBlock = false;
        player.Status.IsSkip = false;
        player.Status.IsReady = true;
        if (!game.CthulhuState.PendingNechtoAttackers.Contains(player.GetPlayerId()))
            game.CthulhuState.PendingNechtoAttackers.Add(player.GetPlayerId());
        player.Status.AddInGamePersonalLogs("Вы напали на Нечто\n");
        player.Status.ChangeMindWhat = "Вы напали на Нечто\n";
        return true;
    }

    public static void ClearPendingNechtoAttack(
        GameClass game,
        GamePlayerBridgeClass player)
    {
        game?.CthulhuState.PendingNechtoAttackers.Remove(player.GetPlayerId());
    }

    public static void ResolveNechtoAttacks(
        GameClass game,
        CalculateRounds calculateRounds,
        CharactersPull pull)
    {
        var state = game.CthulhuState;
        if (state.PendingNechtoAttackers.Count == 0) return;

        state.NechtoBridge ??= new GamePlayerBridgeClass(
            pull.GetAllCharactersNoFilter().First(character => character.Name == Nechto),
            new InGameStatus(),
            0,
            game.GameId,
            Nechto,
            404);

        foreach (var playerId in state.PendingNechtoAttackers.ToList())
        {
            var attacker = game.PlayersList.Find(player =>
                player.GetPlayerId() == playerId);
            if (attacker == null || attacker.Passives.IsDead) continue;

            attacker.FightCharacter = attacker.GameCharacter.DeepCopy();
            state.NechtoBridge.FightCharacter =
                state.NechtoBridge.GameCharacter.DeepCopy();
            var step1 = calculateRounds.CalculateStep1(
                attacker, state.NechtoBridge, false);
            var step2Points = calculateRounds.CalculateStep2(
                attacker, state.NechtoBridge, false);
            var pointsWon = step1.PointsWon + step2Points;
            var usedRandom = false;
            var randomNumber = 0;
            var maxRandom = 0m;
            if (pointsWon == 0)
            {
                var step3 = calculateRounds.CalculateStep3(
                    attacker,
                    state.NechtoBridge,
                    step1.RandomForPoint,
                    step1.NemesisMultiplier,
                    false);
                pointsWon += step3.pointsWined;
                usedRandom = true;
                randomNumber = step3.randomNumber;
                maxRandom = step3.maxRandomNumber;
            }

            var attackerWon = pointsWon > 0;
            if (attackerWon)
                attacker.Status.AddRegularPoints(2, Nechto);
            else
                state.NechtoLosses.Add(playerId);
            state.NechtoAttackedThisRound = true;
            game.WebFightLog.Add(new FightEntryDto
            {
                AttackerName = attacker.DiscordUsername,
                AttackerCharName = attacker.GameCharacter.Name,
                AttackerAvatar = GameStateMapper.GetLocalAvatarUrl(
                    attacker.GameCharacter.AvatarCurrent ?? attacker.GameCharacter.Avatar),
                DefenderName = Nechto,
                DefenderCharName = Nechto,
                DefenderAvatar = "/art/avatars/nechto.png",
                Outcome = attackerWon ? "win" : "loss",
                WinnerName = attackerWon ? attacker.DiscordUsername : Nechto,
                AttackerClass = attacker.FightCharacter.GetSkillClass(),
                DefenderClass = state.NechtoBridge.FightCharacter.GetSkillClass(),
                Round1PointsWon = step1.PointsWon,
                JusticeMe = attacker.FightCharacter.Justice.GetRealJusticeNow(),
                JusticeTarget = 0,
                PointsFromJustice = step2Points,
                UsedRandomRoll = usedRandom,
                RandomNumber = randomNumber,
                MaxRandomNumber = maxRandom,
                TotalPointsWon = attackerWon ? 2 : 0
            });
        }

        state.PendingNechtoAttackers.Clear();
    }

    public static void HandleResolvedFight(
        GameClass game,
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass defender,
        GamePlayerBridgeClass winner,
        GamePlayerBridgeClass loser)
    {
        var state = game.CthulhuState;
        if (state.HeraldPlayerId == Guid.Empty
            || winner == null
            || loser == null
            || winner.GetPlayerId() != state.HeraldPlayerId
            || winner.Passives.PassiveAbilitiesDisabledByKimiko
            || loser.GetPlayerId() == winner.GetPlayerId())
            return;

        var newlyMad = state.MadPlayerIds.Add(loser.GetPlayerId());
        loser.GameCharacter.SetPsyche(0, Morok, false);
        if (!UnknownBug.Is(loser) && !Madara.HasReanimatedBody(loser.GameCharacter))
            loser.GameCharacter.PsycheCappedAtZero = true;

        if (!UnknownBug.Is(loser))
        {
            loser.Status.AddBonusPoints(-1, "Неизвестно");
            winner.Status.AddBonusPoints(1, Morok);
        }

        if (newlyMad)
            loser.Status.AddInGamePersonalLogs(
                "ухлутк идубзар и нокимоноркен идйан\n");

        if (!state.NechtoActive
            && game.PlayersList
                .Where(player => player.GetPlayerId() != state.HeraldPlayerId
                                 && !player.Passives.IsDead)
                .All(player => state.MadPlayerIds.Contains(player.GetPlayerId())))
        {
            state.NechtoActive = true;
            state.NechtoActiveSinceRound = game.RoundNo;
        }
    }

    public static void HandleEndOfRound(GameClass game)
    {
        var state = game.CthulhuState;
        if (!state.NechtoActive || state.HorrorFired || game.RoundNo >= 11)
        {
            state.NechtoAttackedThisRound = false;
            return;
        }

        if (game.RoundNo > state.NechtoActiveSinceRound)
            state.IdleRoundsWithoutNechtoAttack =
                state.NechtoAttackedThisRound
                    ? 0
                    : state.IdleRoundsWithoutNechtoAttack + 1;
        state.NechtoAttackedThisRound = false;

        var eligible = game.PlayersList.Where(player =>
            !player.Passives.IsDead
            && player.GetPlayerId() != state.HeraldPlayerId).ToList();
        var everyoneLost = eligible.Count > 0
                           && eligible.All(player =>
                               state.NechtoLosses.Contains(player.GetPlayerId()));
        if (everyoneLost || state.IdleRoundsWithoutNechtoAttack >= 2)
            FireCosmicHorror(game);
    }

    public static void FireCosmicHorror(GameClass game)
    {
        var state = game.CthulhuState;
        if (state.HorrorFired) return;
        state.HorrorFired = true;
        state.AbyssSerial++;
        game.IsFinished = true;
    }

    public static void ApplyHeraldFinalPlacement(GameClass game)
    {
        var state = game.CthulhuState;
        if (!state.HorrorFired || state.HeraldPlayerId == Guid.Empty) return;
        var herald = FindHerald(game);
        if (herald == null || herald.Passives.IsDead) return;

        // HandleLastRound orders the authoritative table before this settlement.
        // The design awards exactly enough to overtake that current leader.
        var topScore = game.PlayersList.First().Status.GetScore();
        var difference = topScore - herald.Status.GetScore() + 1;
        if (difference > 0)
            herald.Status.AddBonusPoints(difference, CosmicHorror);
        game.PlayersList = Naruto.OrderLeaderboard(game.PlayersList);
        for (var index = 0; index < game.PlayersList.Count; index++)
            game.PlayersList[index].Status.SetPlaceAtLeaderBoard(index + 1);
    }

    public static bool ExcludeFromReplaysAndStory(GameClass game) =>
        game?.CthulhuState.RosterHadCthulhu == true
        || game?.CthulhuState.HorrorFired == true;
}
