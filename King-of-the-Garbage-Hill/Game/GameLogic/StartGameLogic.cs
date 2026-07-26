using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic;

public class StartGameLogic : IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly CharactersPull _charactersPull;
    private readonly GameReaction _gameReaction;
    private readonly HelperFunctions _helperFunctions;
    private readonly SecureRandom _secureRandom;

    public StartGameLogic(SecureRandom secureRandom, CharactersPull charactersPull, GameReaction gameReaction,
        UserAccounts accounts, HelperFunctions helperFunctions)
    {
        _secureRandom = secureRandom;
        _charactersPull = charactersPull;
        _gameReaction = gameReaction;
        _accounts = accounts;
        _helperFunctions = helperFunctions;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    private static bool CanNaturallyRollNaruto(DiscordAccountClass assignee, int strictBotCount, int team)
    {
        if (team > 0) return false;

        // A human can be the original while two bots become clones. If the original
        // is itself a bot, it must be a third bot so two other bot slots remain.
        return Naruto.CanUseRoster(strictBotCount, assignee.PlayerType == 404);
    }

    public static bool AreMutuallyExclusiveCharacters(string firstName, string secondName)
    {
        return firstName == "LeCrisp" && secondName == "Толя"
               || firstName == "Толя" && secondName == "LeCrisp"
               || firstName == "HardKitty" && secondName == ErenYeager.CharacterName
               || firstName == ErenYeager.CharacterName && secondName == "HardKitty";
    }

    private static void RemoveMutuallyExclusiveCharacters(
        List<CharacterClass> characters,
        string assignedName)
    {
        characters.RemoveAll(character =>
            AreMutuallyExclusiveCharacters(assignedName, character.Name));
    }

    private static (string Name, bool IsLootBoxReward) PeekNextCharacterAssignment(
        DiscordAccountClass account)
    {
        lock (account)
        {
            if (!string.IsNullOrWhiteSpace(account.CharacterToGiveNextTime))
                return (account.CharacterToGiveNextTime, false);

            account.LootBoxCharacterQueue ??= new List<string>();
            return account.LootBoxCharacterQueue.Count > 0
                ? (account.LootBoxCharacterQueue[0], true)
                : (null, false);
        }
    }

    private static void ConsumeNextCharacterAssignment(
        DiscordAccountClass account,
        string characterName,
        bool isLootBoxReward)
    {
        lock (account)
        {
            if (!isLootBoxReward)
            {
                if (string.Equals(account.CharacterToGiveNextTime, characterName, StringComparison.Ordinal))
                    account.CharacterToGiveNextTime = null;
                return;
            }

            account.LootBoxCharacterQueue ??= new List<string>();
            if (account.LootBoxCharacterQueue.Count > 0
                && string.Equals(account.LootBoxCharacterQueue[0], characterName, StringComparison.Ordinal))
                account.LootBoxCharacterQueue.RemoveAt(0);
        }
    }


    public int GetRangeFromTier(int tier)
    {
        return tier switch
        {
            6 => 150,
            5 => 100,
            4 => 90,
            3 => 80,
            2 => 70,
            1 => 60,
            0 => 50,
            -1 => 40,
            _ => 0
        };
    }


    public List<GamePlayerBridgeClass> HandleCharacterRoll(List<IUser> players, ulong gameId, int team = 0,
        string mode = "normal", List<string> forcedCharacters = null,
        DiscordAccountClass accountForFirstBotSlot = null,
        bool recordNaturalUnknownBugRoll = true,
        bool ignoreNextCharacterAssignments = false)
    {
        var allCharacters2 = _charactersPull.GetRollableCharacters();
        var allCharacters = _charactersPull.GetRollableCharacters();

        if (team > 0)
        {
            allCharacters2 = allCharacters2
                .Where(x => x.Name != "HardKitty" && x.Name != Naruto.CharacterName).ToList();
            allCharacters = allCharacters
                .Where(x => x.Name != "HardKitty" && x.Name != Naruto.CharacterName).ToList();
        }
        else
        {
            allCharacters2 = allCharacters2.Where(x => !x.TeamModeOnly).ToList();
            allCharacters = allCharacters.Where(x => !x.TeamModeOnly).ToList();
        }

        var reservedCharacters = new List<CharacterClass>();
        var reservedCharacterOwners = new HashSet<ulong>();
        var reservedAssignments = new Dictionary<ulong, (string Name, bool IsLootBoxReward)>();
        var playersList = new List<GamePlayerBridgeClass>();


        var participantAccounts = players
            .Select(player => player == null ? null : _accounts.GetAccount(player))
            .ToList();
        if (accountForFirstBotSlot != null)
        {
            if (participantAccounts.Any(account =>
                    account?.DiscordId == accountForFirstBotSlot.DiscordId))
                throw new ArgumentException("The supplied roll account is already a participant.");

            var firstBotSlot = participantAccounts.FindIndex(account => account == null);
            if (firstBotSlot < 0)
                throw new ArgumentException("The supplied roll account requires an empty bot slot.");
            participantAccounts[firstBotSlot] = accountForFirstBotSlot;
        }

        var strictBotCount = participantAccounts.Count(account =>
            account == null || account.PlayerType == 404);

        var humanParticipantAccounts = participantAccounts
            .Where(account => account != null)
            .Where(account => !account.IsBot())
            .ToList();
        var everyHumanHasRolledUnknownBug = humanParticipantAccounts.Count > 0
                                             && humanParticipantAccounts.All(account =>
                                                 account.HasNaturallyRolledUnknownBug);

        participantAccounts = SecureRandom.Shuffle(participantAccounts); // single game RNG (seeded-sim deterministic)

        //handle custom selected character part #1 (uses unfiltered pool so admins can force TeamModeOnly characters)
        var unfilteredCharacters = _charactersPull.GetRollableCharacters();
        foreach (var account in participantAccounts.Where(account =>
                     account != null && !ignoreNextCharacterAssignments))
        {
            var assignment = PeekNextCharacterAssignment(account);
            if (assignment.Name == null) continue;
            if (assignment.Name == Naruto.CharacterName
                && !CanNaturallyRollNaruto(account, strictBotCount, team))
                continue;

            var character = unfilteredCharacters.Find(x => x.Name == assignment.Name);
            if (character == null || reservedCharacters.Any(existing =>
                    existing.Name == character.Name
                    || AreMutuallyExclusiveCharacters(existing.Name, character.Name)))
                continue;
            reservedCharacters.Add(character);
            reservedCharacterOwners.Add(account.DiscordId);
            reservedAssignments[account.DiscordId] = assignment;
            allCharacters.RemoveAll(x => x.Name == character.Name);
        }
        foreach (var reservedCharacter in reservedCharacters)
            RemoveMutuallyExclusiveCharacters(allCharacters, reservedCharacter.Name);
        //end


        double topLaner = 1;
        var forcedIndex = 0;
        foreach (var account in participantAccounts.Select(account =>
                     account ?? _helperFunctions.GetFreeBot(playersList)))
        {
            account.IsPlaying = true;

            try
            {
                if (!account.IsBot())
                {
                    var temp = players.Where(x => x != null).ToList().Find(x => x.Id == account.DiscordId);
                    if (temp != null)
                        account.DiscordUserName = temp.Username;
                }
            }
            catch
            {
                //ignored
            }


            //выдать персонажей если их нет на аккаунте
            foreach (var character in from character in allCharacters2
                     let knownCharacter = account.CharacterChance.Find(x => x.CharacterName == character.Name)
                     where knownCharacter == null
                     select character)
                account.CharacterChance.Add(new DiscordAccountClass.CharacterChances(character.Name));
            //end

            //forced line-up (simulation harness): assign directly, in list order, bypassing the roll.
            //CharacterToGiveNextTime cannot be used for bot slots (part #1 reserves only for non-null IUsers).
            if (forcedCharacters != null && forcedIndex < forcedCharacters.Count)
            {
                var forcedName = forcedCharacters[forcedIndex];
                forcedIndex++;
                if (forcedName == Naruto.CharacterName
                    && !CanNaturallyRollNaruto(account, strictBotCount, team))
                    throw new ArgumentException(
                        "Forced Наруто requires two other strict bot seats in a free-for-all game.");
                var forcedCharacter = unfilteredCharacters.Find(x => x.Name == forcedName)
                                      ?? throw new ArgumentException($"Forced character not found: {forcedName}");
                allCharacters.RemoveAll(x => x.Name == forcedName);
                playersList.Add(new GamePlayerBridgeClass(
                    forcedCharacter,
                    new InGameStatus(),
                    account.DiscordId,
                    gameId,
                    account.DiscordUserName,
                    account.PlayerType));
                playersList.Last().CharacterMasteryPoints =
                    account.CharacterMastery.GetValueOrDefault(forcedName, 0);
                DoomGuy.InitializeForGame(playersList.Last(), account);
                account.CharacterPlayedLastTime = forcedName;
                RemoveMutuallyExclusiveCharacters(allCharacters, forcedName);
                continue;
            }
            //end forced line-up

            //handle custom selected character part #2
            if (reservedCharacterOwners.Contains(account.DiscordId)
                && reservedAssignments.TryGetValue(account.DiscordId, out var reservedAssignment))
            {
                var reservedCharacter = reservedCharacters.Find(x => x.Name == reservedAssignment.Name);
                playersList.Add(new GamePlayerBridgeClass
                    (reservedCharacter,
                        new InGameStatus(),
                        account.DiscordId,
                        gameId,
                        account.DiscordUserName,
                        account.PlayerType)
                );
                playersList.Last().CharacterMasteryPoints =
                    account.CharacterMastery.GetValueOrDefault(reservedAssignment.Name, 0);
                playersList.Last().IsLootBoxCharacterReward = reservedAssignment.IsLootBoxReward;
                DoomGuy.InitializeForGame(playersList.Last(), account);
                account.CharacterPlayedLastTime = reservedAssignment.Name;
                ConsumeNextCharacterAssignment(
                    account,
                    reservedAssignment.Name,
                    reservedAssignment.IsLootBoxReward);
                if (reservedAssignment.IsLootBoxReward)
                    _accounts.SaveAccount(account);
                continue;
            }
            //end

            var allAvailableCharacters = new List<DiscordAccountClass.CharacterRollClass>();
            var totalPool = 1;
            var newcomerDoom = allCharacters.Find(x => x.Name == DoomGuy.CharacterName);
            var newcomerDoomEligible = !account.IsBot() && account.TotalPlays < 10
                                      && account.CharacterPlayedLastTime != DoomGuy.CharacterName
                                      && newcomerDoom != null;
            var newcomerDoomWon = newcomerDoomEligible && _secureRandom.Luck(30);

            foreach (var character in allCharacters.Where(x => x.Name != account.CharacterPlayedLastTime).ToList())
            {
                if (character.Name == Naruto.CharacterName
                    && !CanNaturallyRollNaruto(account, strictBotCount, team))
                    continue;
                if (Cthulhu.Is(character)
                    && !Cthulhu.CanNaturallyRoll(playersList, reservedCharacters, team))
                    continue;

                // The newcomer roll is an exact 30% branch. Do not leave DooM Guy in the
                // weighted fallback pool when that branch misses, or the real chance exceeds 30%.
                if (newcomerDoomEligible && character.Name == DoomGuy.CharacterName) continue;
                var rollTier = mode == "bot" && account.IsBot() && character.Tier >= 4
                    ? 6
                    : character.Tier;
                var range = GetRangeFromTier(rollTier);
                if (mode != "bot" && character.Tier == 4 && account.IsBot()) range *= 3;
                if (character.Tier < 4 && account.IsBot()
                    && character.Name != "Кира" && !Cthulhu.Is(character)) continue;
                if (character.Name == "Кира" && account.IsBot()) range = GetRangeFromTier(1) / 2;
                if (character.Passive.Any(x => x.PassiveName == "Top Laner")) range = (int)(range * topLaner);
                var pityBonus = account.TierPity.GetValueOrDefault(rollTier, 0);
                range = (int)(range * (1.0 + pityBonus * 0.03));
                var chanceEntry = account.CharacterChance.Find(x => x.CharacterName == character.Name);
                var rollMultiplier = UnknownBug.Is(character.Name)
                    ? everyHumanHasRolledUnknownBug ? 100.0 : 1.0
                    : chanceEntry?.GetEffectiveMultiplier() ?? 1.0;
                var temp = totalPool + Convert.ToInt32(range * rollMultiplier) - 1;
                allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                totalPool = temp + 1;
            }

            CharacterClass characterToAssign;
            if (newcomerDoomWon)
            {
                characterToAssign = newcomerDoom;
            }
            else
            {
                var randomIndex = _secureRandom.Random(1, totalPool - 1);
                var rolledCharacter = allAvailableCharacters.Find(x =>
                    randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
                characterToAssign = allCharacters.Find(x => x.Name == rolledCharacter!.CharacterName);
            }

            // Bot-only games historically normalize every public high-tier result to Tier 6.
            // Keep that contract on bot seats without mutating a human web creator's shared pool.
            if (mode == "bot" && account.IsBot() && characterToAssign.Tier >= 4)
                characterToAssign.Tier = 6;

            if (characterToAssign.Passive.Any(x => x.PassiveName == "Top Laner"))
            {
                topLaner -= 0.2;
                if (topLaner < 0)
                    topLaner = 0;
            }


            RemoveMutuallyExclusiveCharacters(allCharacters, characterToAssign.Name);

            switch (characterToAssign.Tier)
            {
                case 4:
                    allCharacters = allCharacters.Where(x => x.Tier != 4).ToList();
                    break;
            }

            playersList.Add(new GamePlayerBridgeClass
            (
                characterToAssign,
                new InGameStatus(),
                account.DiscordId,
                gameId,
                account.DiscordUserName,
                account.PlayerType
            ));
            playersList.Last().CharacterMasteryPoints = account.CharacterMastery.GetValueOrDefault(characterToAssign.Name, 0);
            DoomGuy.InitializeForGame(playersList.Last(), account);
            account.CharacterPlayedLastTime = characterToAssign.Name;
            if (recordNaturalUnknownBugRoll
                && !account.IsBot()
                && UnknownBug.Is(characterToAssign)
                && !account.HasNaturallyRolledUnknownBug)
            {
                account.HasNaturallyRolledUnknownBug = true;
                _accounts.SaveAccount(account);
            }
            allCharacters.Remove(characterToAssign);
        }

        //Добавить персонажа в магазин человека
        foreach (var player in players)
        {
            if (player == null) continue;
            var account = _accounts.GetAccount(player);

            foreach (var playerInGame in playersList.Where(playerInGame =>
                         !UnknownBug.Is(playerInGame.GameCharacter.Name)
                         && !account.SeenCharacters.Contains(playerInGame.GameCharacter.Name)))
                if (playerInGame.DiscordId == player.Id)
                    account.SeenCharacters.Add(playerInGame.GameCharacter.Name);
        }

        return playersList;
    }

    public List<CharacterClass> RollDraftOptions(DiscordAccountClass account,
        List<CharacterClass> excludedCharacters, int strictBotCount, int count = 3,
        bool isTeamMode = false)
    {
        var allCharacters = _charactersPull.GetRollableCharacters();
        allCharacters.RemoveAll(character => UnknownBug.Is(character.Name));
        allCharacters.RemoveAll(character => Cthulhu.Is(character.Name));

        // Remove team-mode-only characters for non-team games
        allCharacters = allCharacters.Where(x => !x.TeamModeOnly).ToList();

        // Draft alternatives are rolled for humans, so two strict bot slots are
        // sufficient. Naruto is never a natural team-mode option.
        if (strictBotCount < 2 || isTeamMode)
            allCharacters.RemoveAll(x => x.Name == Naruto.CharacterName);

        // Remove already-assigned characters
        foreach (var excluded in excludedCharacters)
        {
            allCharacters.RemoveAll(x => x.Name == excluded.Name);
            RemoveMutuallyExclusiveCharacters(allCharacters, excluded.Name);
        }

        // Remove character played last time
        allCharacters = allCharacters.Where(x => x.Name != account.CharacterPlayedLastTime).ToList();

        // Ensure account has chance entries for all characters
        var allCharacters2 = _charactersPull.GetRollableCharacters();
        foreach (var character in allCharacters2)
        {
            if (account.CharacterChance.Find(x => x.CharacterName == character.Name) == null)
                account.CharacterChance.Add(new DiscordAccountClass.CharacterChances(character.Name));
        }

        var result = new List<CharacterClass>();

        // Newcomer protection: DooM Guy occupies the first alternative with an exact 30% roll.
        var newcomerDoom = allCharacters.Find(x => x.Name == DoomGuy.CharacterName);
        if (account.TotalPlays < 10 && newcomerDoom != null && _secureRandom.Luck(30))
        {
            result.Add(newcomerDoom);
            allCharacters.Remove(newcomerDoom);
            if (newcomerDoom.Tier == 4)
                allCharacters = allCharacters.Where(x => x.Tier != 4).ToList();
        }

        for (var pick = result.Count; pick < count && allCharacters.Count > 0; pick++)
        {
            var allAvailableCharacters = new List<DiscordAccountClass.CharacterRollClass>();
            var totalPool = 1;

            foreach (var character in allCharacters)
            {
                var range = GetRangeFromTier(character.Tier);
                var pityBonus = account.TierPity.GetValueOrDefault(character.Tier, 0);
                range = (int)(range * (1.0 + pityBonus * 0.03));
                var chanceEntry = account.CharacterChance.Find(x => x.CharacterName == character.Name);
                if (chanceEntry == null) continue;
                var temp = totalPool + Convert.ToInt32(range * chanceEntry.GetEffectiveMultiplier()) - 1;
                allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                totalPool = temp + 1;
            }

            if (totalPool <= 1 || allAvailableCharacters.Count == 0) break;

            var randomIndex = _secureRandom.Random(1, totalPool - 1);
            var rolledCharacter = allAvailableCharacters.Find(x =>
                randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
            if (rolledCharacter == null) break;

            var characterToAdd = allCharacters.Find(x => x.Name == rolledCharacter.CharacterName);
            if (characterToAdd == null) break;

            result.Add(characterToAdd);
            allCharacters.Remove(characterToAdd);

            // Respect tier-4 uniqueness: if we rolled a tier-4, remove all other tier-4s
            if (characterToAdd.Tier == 4)
                allCharacters = allCharacters.Where(x => x.Tier != 4).ToList();

            RemoveMutuallyExclusiveCharacters(allCharacters, characterToAdd.Name);
        }

        return result;
    }

    public List<GamePlayerBridgeClass> HandleAramRoll(List<IUser> players, ulong gameId)
    {
        var playersList = new List<GamePlayerBridgeClass>();
        var passives = _charactersPull.GetAramPassives();
        passives = SecureRandom.Shuffle(passives);   // single game RNG (seeded-sim deterministic)

        foreach (var account in players.Select(player =>
                     player != null ? _accounts.GetAccount(player.Id) : _helperFunctions.GetFreeBot(playersList)))
        {
            account.IsPlaying = true;
            account.CharacterPlayedLastTime = "ARAM";
            var intelligence = _gameReaction.GetRandomStat();
            var strength = _gameReaction.GetRandomStat();
            var speed = _gameReaction.GetRandomStat();
            var psyche = _gameReaction.GetRandomStat();

            var character = new CharacterClass(intelligence, strength, speed, psyche, "ARAM", "ARAM", 0,
                "https://media.discordapp.net/attachments/895072182051430401/1057078633317023855/mylorik_avatar_for_an_rpg_game_where_players_are_forced_to_pick_386de9dc-62ca-491c-ae63-54324a8c95d9.png")
            {
                Passive = new List<Passive>(),
                Name = "ARAM",
                Description = "ARAM"
            };


            for (var i = 0; i < 4; i++)
            {
                var randomNumber = _secureRandom.Random(0, passives.Count - 1);
                var newPassive = passives[randomNumber];
                character.Passive.Add(newPassive);
                passives.RemoveAt(randomNumber);
            }


            playersList.Add(new GamePlayerBridgeClass
            (
                character,
                new InGameStatus(),
                account.DiscordId,
                gameId,
                account.DiscordUserName,
                account.PlayerType
            ));
        }

        return playersList;
    }

    public EmbedBuilder GetStatsEmbed(DiscordAccountClass account)
    {
        var embed = new EmbedBuilder();
        var characterStatistics = account.CharacterStatistics
            .Where(stat => !UnknownBug.Is(stat.CharacterName))
            .ToList();
        var matchHistory = account.MatchHistory
            .Where(match => !UnknownBug.Is(match.CharacterName))
            .ToList();
        var characterChances = account.CharacterChance
            .Where(chance => !UnknownBug.Is(chance.CharacterName))
            .ToList();
        var mostWins = characterStatistics.OrderByDescending(x => x.Wins).FirstOrDefault();
        var leastWins = characterStatistics.OrderByDescending(x => x.Wins).LastOrDefault();
        var mostPlays = characterStatistics.OrderByDescending(x => x.Plays).FirstOrDefault();
        var leastPlays = characterStatistics.OrderByDescending(x => x.Plays).LastOrDefault();
        var mostPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place).ElementAtOrDefault(0);
        var leastPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place)
            .ElementAtOrDefault(account.PerformanceStatistics.Count - 1);
        var topPoints = matchHistory.OrderByDescending(x => x.Score).FirstOrDefault();
        var mostChance = characterChances.OrderByDescending(x => x.GetEffectiveMultiplier()).FirstOrDefault();
        var leastChance = characterChances.OrderByDescending(x => x.GetEffectiveMultiplier()).LastOrDefault();

        ulong totalPoints = 0;

        foreach (var v in matchHistory)
            if (v.Score > 0)
                totalPoints += (ulong)v.Score;
            else
                totalPoints += (ulong)(v.Score * -1);

        //embed.WithAuthor(Context.User);
        // embed.WithDescription("буль-буль");

        embed.AddField("ZBS Points", $"{account.ZbsPoints}", true);
        embed.AddField("Тип Пользователя", $"{account.PlayerType}", true);
        embed.AddField("Всего Игр", $"{account.TotalPlays}", true);
        embed.AddField("Всего Топ 1", $"{account.TotalWins}", true);

        if (totalPoints > 0 && account.TotalWins > 0)
            embed.AddField("Среднее количество очков за игру",
                $"{totalPoints / account.TotalWins} - ({totalPoints}/{account.TotalWins})");
        if (topPoints != null)
            embed.AddField("Больше всего очков за игру",
                $"{topPoints.CharacterName} - {topPoints.Score} (#{topPoints.Place}) {topPoints.Date.Month}.{topPoints.Date.Day}.{topPoints.Date.Year}",
                true);
        if (mostWins != null)
            embed.AddField("Больше всего побед", $"{mostWins.CharacterName} - {mostWins.Wins}/{mostWins.Plays}",
                true);
        if (leastWins != null)
            embed.AddField("Меньше всего побед", $"{leastWins.CharacterName} - {leastWins.Wins}/{leastWins.Plays}",
                true);
        if (mostPlays != null)
            embed.AddField("Больше всего игр", $"{mostPlays.CharacterName} - {mostPlays.Wins}/{mostPlays.Plays}",
                true);
        if (leastPlays != null)
            embed.AddField("Меньше всего игр", $"{leastPlays.CharacterName} - {leastPlays.Wins}/{leastPlays.Plays}",
                true);
        if (mostPlace != null)
            embed.AddField("Самое частое место", $"Топ {mostPlace.Place} - {mostPlace.Times}/{account.TotalPlays}",
                true);
        if (leastPlace != null)
            embed.AddField("Самое редкое место",
                $"Топ {leastPlace.Place} - {leastPlace.Times}/{account.TotalPlays}",
                true);
        if (mostChance != null)
            embed.AddField("Самый большой шанс",
                $"{mostChance.CharacterName} - {mostChance.GetEffectiveMultiplier()} ",
                true);
        if (leastChance != null)
            embed.AddField("Самый маленький шанс",
                $"{leastChance.CharacterName} - {leastChance.GetEffectiveMultiplier()} ",
                true);

        embed.WithFooter("циферки");
        embed.WithCurrentTimestamp();

        return embed;
    }
}
