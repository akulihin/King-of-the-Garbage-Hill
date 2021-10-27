using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.GeneralCommands
{
    public class Tutorial : ModuleBaseCustom
    {
        private readonly UserAccounts _accounts;
        private readonly CharacterPassives _characterPassives;
        private readonly CharactersPull _charactersPull;

        private readonly CommandsInMemory _commandsInMemory;
        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly HelperFunctions _helperFunctions;

        
        private readonly SecureRandom _secureRandom;
        private readonly GameUpdateMess _upd;


        public Tutorial(UserAccounts accounts, SecureRandom secureRandom, 
             HelperFunctions helperFunctions, CommandsInMemory commandsInMemory,
            Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
            CharactersUniquePhrase phrase, InGameGlobal gameGlobal)
        {
            _accounts = accounts;
            _secureRandom = secureRandom;
            _helperFunctions = helperFunctions;
            _commandsInMemory = commandsInMemory;
            _global = global;
            _upd = upd;
            _charactersPull = charactersPull;
            _characterPassives = characterPassives;
            _gameGlobal = gameGlobal;
        }
        public EmbedBuilder LvlUpPage(GamePlayerBridgeClass player)
        {
            //    var status = player.Status;
            var account = _accounts.GetAccount(player.DiscordId);
            var character = player.Character;

            //   status.MoveListPage = 3;
            //    _accounts.SaveAccounts(discordAccount.PlayerDiscordId);

            var embed = new EmbedBuilder();

            var desc = _global.GamesList.Find(x => x.GameId == player.GameId).GetGlobalLogs();
            if (desc == null)
                return null;
            embed.WithDescription(desc.Replace(player.DiscordUsername,
                $"**{player.DiscordUsername}**"));

            embed.WithColor(Color.Blue);

           
            embed.WithCurrentTimestamp();
            embed.AddField("_____",
                "__Подними один из статов на 1:__\n \n" +
                $"1. **Интеллект:** {character.GetIntelligence()}\n" +
                $"2. **Сила:** {character.GetStrength()}\n" +
                $"3. **Скорость:** {character.GetSpeed()}\n" +
                $"4. **Психика:** {character.GetPsyche()}\n");

            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        public EmbedBuilder FightPage(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var character = player.Character;

            var embed = new EmbedBuilder();
            embed.WithColor(Color.Blue);
            embed.WithTitle("King of the Garbage Hill");
            var roundNumber = game.RoundNo;


            if (roundNumber > 10) roundNumber = 10;

            var multiplier = roundNumber switch
            {
                <= 4 => 1,
                <= 9 => 2,
                _ => 4
            };
            //Претендент русского сервера
            if (player.Status.GetInGamePersonalLogs().Contains("Претендент русского сервера")) multiplier *= 3;
            //end Претендент русского сервера

            game = _global.GamesList.Find(x => x.GameId == player.GameId);


            var desc = game.GetGlobalLogs();
            desc = desc.Replace(player.DiscordUsername, $"**{player.DiscordUsername}**");

            /*
            if (player.PlayerType == 0)
            {
                desc = desc.Replace(" (Блок)...", "");
                desc = desc.Replace(" (Скип)...", "");
            }
            */


            embed.WithDescription($"{desc}" +
                                  "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                                  $"**Интеллект:** {character.GetIntelligenceString()}\n" +
                                  $"**Сила:** {character.GetStrengthString()}\n" +
                                  $"**Скорость:** {character.GetSpeedString()}\n" +
                                  $"**Психика:** {character.GetPsycheString()}\n" +
                                  "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                                  $"*Справедливость: **{character.Justice.GetJusticeNow()}***\n" +
                                  $"*Мораль: {character.GetMoral()}*\n" +
                                  $"*Скилл: {character.GetSkill()} (Мишень: **{character.GetCurrentSkillTarget()}**)*\n" +
                                  $"*Класс:* {character.GetClassStatString()}\n" +
                                  "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                                  $"Множитель очков: **X{multiplier}**\n" +
                                  "<:e_:562879579694301184>\n");


            var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

            var text = "";
            if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
            {
                text = splitLogs[^2];
                embed.AddField("События прошлого раунда:", $"{text}");
            }
            else
            {
                embed.AddField("События прошлого раунда:", "В прошлом раунде ничего не произошло. Странно...");
            }

            text = player.Status.GetInGamePersonalLogs().Length >= 2 ? $"{player.Status.GetInGamePersonalLogs()}" : "Еще ничего не произошло. Наверное...";
            embed.AddField("События этого раунда:", text);


            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }


        [Command("tutorial")]
        [Alias("tt")]
        [Summary("Tutorial")]
        public async Task TutorialCommand()
        {



        }


        public class TutorialGame
        {
            public ulong DiscordPlayer { get; set; }
            public int RoundNumber { get; set; }
            public IUserMessage SocketMessageFromBot { get; set; }

            public TutorialGame(ulong discordPlayer, int roundNumber)
            {
                DiscordPlayer = discordPlayer;
                RoundNumber = roundNumber;
            }
        }

        public class TutorialGamePlayers
        {
            public int PlayerId;
            public int PlaceAtTheLeaderBoard;

            public TutorialGamePlayers(int playerId, int placeAtTheLeaderBoard)
            {
                PlayerId = playerId;
                PlaceAtTheLeaderBoard = placeAtTheLeaderBoard;
            }
        }
    }
}