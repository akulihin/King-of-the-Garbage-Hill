using Discord;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Game.ReactionHandling;
using King_of_the_Garbage_Hill.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.GameLogic
{
    public class StartGameLogic : IServiceSingleton
    {
        private readonly GameReaction _gameReaction;
        private readonly CharactersPull _charactersPull;
        private readonly SecureRandom _secureRandom;
        private readonly UserAccounts _accounts;
        private readonly HelperFunctions _helperFunctions;

        public StartGameLogic(SecureRandom secureRandom, CharactersPull charactersPull,GameReaction gameReaction, UserAccounts accounts, HelperFunctions helperFunctions)
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
            switch (tier)
            {
                case 6:
                    return 100;
                case 5:
                    return 50;
                case 4:
                    return 40;
                case 3:
                    return 30;
                case 2:
                    return 20;
                case 1:
                    return 10;
                default:
                    return 0;
            }
        }


        public List<GamePlayerBridgeClass> HandleCharacterRoll(List<IUser> players, ulong gameId, int team = 0, string mode = "normal")
        {
            var allCharacters2 = _charactersPull.GetAllCharacters();
            var allCharacters = _charactersPull.GetAllCharacters();

            if (mode == "bot")
            {
                foreach (var c in allCharacters.Where(c => c.Tier >= 4))
                {
                    c.Tier = 6;
                }

                foreach (var c in allCharacters2.Where(c => c.Tier >= 4))
                {
                    c.Tier = 6;
                }
            }

            if (team > 0)
            {
                allCharacters2 = allCharacters2.Where(x => x.Name != "HardKitty").ToList();
                allCharacters = allCharacters.Where(x => x.Name != "HardKitty").ToList();
            }

            var reservedCharacters = new List<CharacterClass>();
            var playersList = new List<GamePlayerBridgeClass>();


            players = players.OrderBy(_ => Guid.NewGuid()).ToList();

            //handle custom selected character part #1
            var characters = allCharacters;
            foreach (var character in from player in players
                                      where player != null
                                      select _accounts.GetAccount(player)
                     into account
                                      where account.CharacterToGiveNextTime != null
                                      select characters!.Find(x => x.Name == account.CharacterToGiveNextTime))
            {
                reservedCharacters.Add(character);
                allCharacters.Remove(character);
            }
            //end


            double topLaner = 1;
            foreach (var account in players.Select(player => player != null ? _accounts.GetAccount(player.Id) : _helperFunctions.GetFreeBot(playersList)))
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
                    if (character.Passive.Any(x => x.PassiveName == "Top Laner"))
                    {
                        range = (int)(range * topLaner);
                    }
                    var temp = totalPool + Convert.ToInt32(range * account.CharacterChance.Find(x => x.CharacterName == character.Name).Multiplier) - 1;
                    allAvailableCharacters.Add(new DiscordAccountClass.CharacterRollClass(character.Name, totalPool, temp));
                    totalPool = temp + 1;
                }

                var randomIndex = _secureRandom.Random(1, totalPool - 1);
                var rolledCharacter = allAvailableCharacters.Find(x => randomIndex >= x.CharacterRangeMin && randomIndex <= x.CharacterRangeMax);
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

        public List<GamePlayerBridgeClass> HandleAramRoll(List<IUser> players, ulong gameId)
        {
            var playersList = new List<GamePlayerBridgeClass>();
            var passives = _charactersPull.GetAllVisiblePassives();
            passives = passives.OrderBy(_ => Guid.NewGuid()).ToList();

            foreach (var account in players.Select(player => player != null ? _accounts.GetAccount(player.Id) : _helperFunctions.GetFreeBot(playersList)))
            {
                account.IsPlaying = true;
                account.CharacterPlayedLastTime = "ARAM";
                var intelligence = _gameReaction.GetRandomStat();
                var strength = _gameReaction.GetRandomStat();
                var speed = _gameReaction.GetRandomStat();
                var psyche = _gameReaction.GetRandomStat();

                var character = new CharacterClass(intelligence, strength, speed, psyche, "ARAM", "ARAM", 0, "https://media.discordapp.net/attachments/895072182051430401/1057078633317023855/mylorik_avatar_for_an_rpg_game_where_players_are_forced_to_pick_386de9dc-62ca-491c-ae63-54324a8c95d9.png")
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

            if (totalPoints > 0)
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
}
