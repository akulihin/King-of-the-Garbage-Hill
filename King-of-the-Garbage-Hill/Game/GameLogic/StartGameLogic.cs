using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
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
        string mode = "normal")
    {
        var allCharacters2 = _charactersPull.GetRollableCharacters();
        var allCharacters = _charactersPull.GetRollableCharacters();

        if (mode == "bot")
        {
            foreach (var c in allCharacters.Where(c => c.Tier >= 4)) c.Tier = 6;

            foreach (var c in allCharacters2.Where(c => c.Tier >= 4)) c.Tier = 6;
        }

        if (team > 0)
        {
            allCharacters2 = allCharacters2.Where(x => x.Name != "HardKitty").ToList();
            allCharacters = allCharacters.Where(x => x.Name != "HardKitty").ToList();
        }
        else
        {
            allCharacters2 = allCharacters2.Where(x => !x.TeamModeOnly).ToList();
            allCharacters = allCharacters.Where(x => !x.TeamModeOnly).ToList();
        }

        var reservedCharacters = new List<CharacterClass>();
        var playersList = new List<GamePlayerBridgeClass>();


        players = players.OrderBy(_ => Guid.NewGuid()).ToList();

        //handle custom selected character part #1 (uses unfiltered pool so admins can force TeamModeOnly characters)
        var unfilteredCharacters = _charactersPull.GetRollableCharacters();
        foreach (var character in from player in players
                 where player != null
                 select _accounts.GetAccount(player)
                 into account
                 where account.CharacterToGiveNextTime != null
                 select unfilteredCharacters.Find(x => x.Name == account.CharacterToGiveNextTime))
        {
            if (character == null) continue;
            reservedCharacters.Add(character);
            allCharacters.RemoveAll(x => x.Name == character.Name);
        }
        //end


        double topLaner = 1;
        foreach (var account in players.Select(player =>
                     player != null ? _accounts.GetAccount(player.Id) : _helperFunctions.GetFreeBot(playersList)))
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

            //handle custom selected character part #2
            if (account.CharacterToGiveNextTime != null)
            {
                playersList.Add(new GamePlayerBridgeClass
                    (reservedCharacters.Find(x => x.Name == account.CharacterToGiveNextTime),
                        new InGameStatus(),
                        account.DiscordId,
                        gameId,
                        account.DiscordUserName,
                        account.PlayerType)
                );
                playersList.Last().CharacterMasteryPoints = account.CharacterMastery.GetValueOrDefault(account.CharacterToGiveNextTime, 0);
                account.CharacterPlayedLastTime = account.CharacterToGiveNextTime;
                account.CharacterToGiveNextTime = null;
                continue;
            }
            //end

            var allAvailableCharacters = new List<DiscordAccountClass.CharacterRollClass>();
            var totalPool = 1;

            foreach (var character in allCharacters.Where(x => x.Name != account.CharacterPlayedLastTime).ToList())
            {
                var range = GetRangeFromTier(character.Tier);
                if (character.Tier == 4 && account.IsBot()) range *= 3;
                if (character.Tier < 4 && account.IsBot()) continue;
                if (character.Passive.Any(x => x.PassiveName == "Top Laner")) range = (int)(range * topLaner);
                var pityBonus = account.TierPity.GetValueOrDefault(character.Tier, 0);
                range = (int)(range * (1.0 + pityBonus * 0.03));
                var temp = totalPool +
                    Convert.ToInt32(range * account.CharacterChance.Find(x => x.CharacterName == character.Name)
                        .Multiplier) - 1;
                allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                totalPool = temp + 1;
            }

            var randomIndex = _secureRandom.Random(1, totalPool - 1);
            var rolledCharacter = allAvailableCharacters.Find(x =>
                randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
            var characterToAssign = allCharacters.Find(x => x.Name == rolledCharacter!.CharacterName);

            if (characterToAssign.Passive.Any(x => x.PassiveName == "Top Laner"))
            {
                topLaner -= 0.2;
                if (topLaner < 0)
                    topLaner = 0;
            }


            switch (characterToAssign.Name)
            {
                case "LeCrisp":
                {
                    var characterToRemove = allCharacters.Find(x => x.Name == "Толя");
                    allCharacters.Remove(characterToRemove);
                    break;
                }
                case "Толя":
                {
                    var characterToRemove = allCharacters.Find(x => x.Name == "LeCrisp");
                    allCharacters.Remove(characterToRemove);
                    break;
                }
            }

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
            account.CharacterPlayedLastTime = characterToAssign.Name;
            allCharacters.Remove(characterToAssign);
        }

        //Добавить персонажа в магазин человека
        foreach (var player in players)
        {
            if (player == null) continue;
            var account = _accounts.GetAccount(player);

            foreach (var playerInGame in playersList.Where(playerInGame =>
                         !account.SeenCharacters.Contains(playerInGame.GameCharacter.Name)))
                if (playerInGame.DiscordId == player.Id)
                    account.SeenCharacters.Add(playerInGame.GameCharacter.Name);
        }

        return playersList;
    }

    public List<CharacterClass> RollDraftOptions(DiscordAccountClass account, List<CharacterClass> excludedCharacters, int count = 3)
    {
        var allCharacters = _charactersPull.GetRollableCharacters();

        // Remove team-mode-only characters for non-team games
        allCharacters = allCharacters.Where(x => !x.TeamModeOnly).ToList();

        // Remove already-assigned characters
        foreach (var excluded in excludedCharacters)
            allCharacters.RemoveAll(x => x.Name == excluded.Name);

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

        for (var pick = 0; pick < count && allCharacters.Count > 0; pick++)
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
                var temp = totalPool + Convert.ToInt32(range * chanceEntry.Multiplier) - 1;
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

            // Respect LeCrisp/Толя exclusion
            switch (characterToAdd.Name)
            {
                case "LeCrisp":
                    allCharacters.RemoveAll(x => x.Name == "Толя");
                    break;
                case "Толя":
                    allCharacters.RemoveAll(x => x.Name == "LeCrisp");
                    break;
            }
        }

        return result;
    }

    public List<GamePlayerBridgeClass> HandleAramRoll(List<IUser> players, ulong gameId)
    {
        var playersList = new List<GamePlayerBridgeClass>();
        var passives = _charactersPull.GetAramPassives();
        passives = passives.OrderBy(_ => Guid.NewGuid()).ToList();

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
        var mostWins = account.CharacterStatistics.OrderByDescending(x => x.Wins).ToList().ElementAtOrDefault(0);
        var leastWins = account.CharacterStatistics.OrderByDescending(x => x.Wins)
            .ElementAtOrDefault(account.CharacterStatistics.Count - 1);
        var mostPlays = account.CharacterStatistics.OrderByDescending(x => x.Plays).ElementAtOrDefault(0);
        var leastPlays = account.CharacterStatistics.OrderByDescending(x => x.Plays)
            .ElementAtOrDefault(account.CharacterStatistics.Count - 1);
        var mostPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place).ElementAtOrDefault(0);
        var leastPlace = account.PerformanceStatistics.OrderByDescending(x => x.Place)
            .ElementAtOrDefault(account.PerformanceStatistics.Count - 1);
        var topPoints = account.MatchHistory.OrderByDescending(x => x.Score).ElementAtOrDefault(0);
        var mostChance = account.CharacterChance.OrderByDescending(x => x.Multiplier).ElementAtOrDefault(0);
        var leastChance = account.CharacterChance.OrderByDescending(x => x.Multiplier)
            .ElementAtOrDefault(account.CharacterChance.Count - 1);

        ulong totalPoints = 0;

        foreach (var v in account.MatchHistory)
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
                $"{mostChance.CharacterName} - {mostChance.Multiplier} ",
                true);
        if (leastChance != null)
            embed.AddField("Самый маленький шанс",
                $"{leastChance.CharacterName} - {leastChance.Multiplier} ",
                true);

        embed.WithFooter("циферки");
        embed.WithCurrentTimestamp();

        return embed;
    }
}