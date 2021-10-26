using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.DiscordFramework;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.DiscordMessages
{
    public sealed class GameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
    {
        private readonly UserAccounts _accounts;

        private readonly InGameGlobal _gameGlobal;
        private readonly Global _global;
        private readonly HelperFunctions _helperFunctions;
        private readonly LoginFromConsole _log;

        private readonly List<Emoji> _playerChoiceAttackList = new()
            {new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣"), new Emoji("5⃣"), new Emoji("6⃣")};


        public GameUpdateMess(UserAccounts accounts, Global global, InGameGlobal gameGlobal,
            HelperFunctions helperFunctions, LoginFromConsole log)
        {
            _accounts = accounts;
            _global = global;
            _gameGlobal = gameGlobal;
            _helperFunctions = helperFunctions;
            _log = log;
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }


        public async Task ShowRulesAndChar(SocketUser user, GamePlayerBridgeClass player)
        {
            var pass = "";
            var characterPassivesList = player.Character.Passive;
            foreach (var passive in characterPassivesList)
            {
                if(!passive.Visible) continue;
                pass += $"__**{passive.PassiveName}**__";
                pass += ": ";
                pass += passive.PassiveDescription;
                pass += "\n";
            }


            var embed = new EmbedBuilder();
            embed.WithColor(Color.DarkOrange);
            //if (player.Character.Avatar != null)
           //     embed.WithImageUrl(player.Character.Avatar);
            embed.AddField("Твой Персонаж:", $"Name: {player.Character.Name}\n" +
                                             $"Интеллект: {player.Character.GetIntelligence()}\n" +
                                             $"Сила: {player.Character.GetStrength()}\n" +
                                             $"Скорость: {player.Character.GetSpeed()}\n" +
                                             $"Психика: {player.Character.GetPsyche()}\n");
            embed.AddField("Пассивки", $"{pass}");
            
            //if(player.Character.Description.Length > 1)
            //    embed.WithDescription(player.Character.Description);


            await user.SendMessageAsync("", false, embed.Build());
        }

        public async Task WaitMess(GamePlayerBridgeClass player, List<GamePlayerBridgeClass> players)
        {
            if (player.DiscordId <= 1000000) return;

            var globalAccount = _global.Client.GetUser(player.DiscordId);


            await ShowRulesAndChar(globalAccount, player);

            var mainPage = new EmbedBuilder();
            mainPage.WithAuthor(globalAccount);
            mainPage.WithFooter("Preparation time...");
            mainPage.WithColor(Color.DarkGreen);
            mainPage.AddField("Game is being ready", "**Please wait for the main menu**");


            var socketMsg = await globalAccount.SendMessageAsync("", false, mainPage.Build());

            player.Status.SocketMessageFromBot = socketMsg;
        }

        public string LeaderBoard(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            if (game == null) return "ERROR 404";
            var players = "";
            var playersList = game.PlayersList;

            for (var i = 0; i < playersList.Count; i++)
            {
                players += CustomLeaderBoardBeforeNumber(player, playersList[i], game, i + 1);

                players += $"{i + 1}. {playersList[i].DiscordUsername}";

                players += CustomLeaderBoardAfterPlayer(player, playersList[i], game);

                //TODO: REMOVE || playersList[i].IsBot()
                if (player.Status.PlayerId == playersList[i].Status.PlayerId)
                    players +=
                        $" = **{playersList[i].Status.GetScore()} Score**";


                players += "\n";
            }

            return players;
        }

        public string CustomLeaderBoardBeforeNumber(GamePlayerBridgeClass player1, GamePlayerBridgeClass player2,
            GameClass game, int number)
        {
            var customString = "";

            switch (player1.Character.Name)
            {
                case "Осьминожка":
                    var octoTentacles = _gameGlobal.OctopusTentaclesList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (!octoTentacles.LeaderboardPlace.Contains(number)) customString += "🐙";


                    break;

                case "Братишка":
                    var shark = _gameGlobal.SharkJawsLeader.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == player1.Status.PlayerId);

                    if (!shark.FriendList.Contains(number)) customString += "🐙";
                    break;
            }

            return customString + " ";
        }

        public string CustomLeaderBoardAfterPlayer(GamePlayerBridgeClass me, GamePlayerBridgeClass other,
            GameClass game)
        {
            var customString = "";
            //|| me.DiscordId == 238337696316129280 || me.DiscordId == 181514288278536193


            switch (me.Character.Name)
            {
                case "AWDKA":
                    if (other.Status.PlayerId == me.Status.PlayerId) break;

                    var awdka = _gameGlobal.AwdkaTryingList.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    var awdkaTrainingHistory = _gameGlobal.AwdkaTeachToPlayHistory.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);

                    var awdkaTrying = awdka.TryingList.Find(x => x.EnemyPlayerId == other.Status.PlayerId);

                    if (awdkaTrying != null)
                    {
                        if (!awdkaTrying.IsUnique) customString += " <:bronze:565744159680626700>";
                        else customString += " <:plat:565745613208158233>";
                    }


                    if (awdkaTrainingHistory != null)
                    {
                        var awdkaTrainingHistoryEnemy =
                            awdkaTrainingHistory.History.Find(x => x.EnemyPlayerId == other.Status.PlayerId);
                        if (awdkaTrainingHistoryEnemy != null)
                        {
                            var statText = awdkaTrainingHistoryEnemy.Text switch
                            {
                                "1" => "Интеллект",
                                "2" => "Сила",
                                "3" => "Скорость",
                                "4" => "Психика",
                                _ => ""
                            };
                            customString += $" (**{statText} {awdkaTrainingHistoryEnemy.Stat}** ?)";
                        }
                    }
                    //(<:volibir:894286361895522434> сила 10 ?)


                    break;
                case "Братишка":
                    var shark = _gameGlobal.SharkJawsWin.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);
                    if (!shark.FriendList.Contains(other.Status.PlayerId) &&
                        other.Status.PlayerId != me.Status.PlayerId)
                        customString += " <:jaws:565741834219945986>";
                    break;

                case "Darksci":
                    var dar = _gameGlobal.DarksciLuckyList.Find(x =>
                        x.GameId == game.GameId &&
                        x.PlayerId == me.Status.PlayerId);

                    if (!dar.TouchedPlayers.Contains(other.Status.PlayerId) &&
                        other.Status.PlayerId != me.Status.PlayerId)
                        customString += " <:Darksci:565598465531576352>";


                    break;
                case "Вампур":
                    var vamp = _gameGlobal.VampyrHematophagiaList.Find(x => x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);
                    var target = vamp.Hematophagia.Find(x => x.EnemyId == other.Status.PlayerId);
                    if (target != null)
                        customString += " <:Y_:562885385395634196>";
                    break;

                case "HardKitty":
                    var hardKitty = _gameGlobal.HardKittyDoebatsya.Find(x => x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);
                    if (hardKitty != null)
                    {
                        var lostSeries = hardKitty.LostSeries.Find(x => x.EnemyPlayerId == other.Status.PlayerId);
                        if(lostSeries != null)
                            if (lostSeries.Series > 0)
                                customString += $" <:393:563063205811847188> - {lostSeries.Series}";
                    }

                    break;
                case "Sirinoks":
                    var siri = _gameGlobal.SirinoksFriendsList.Find(x =>
                        x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);

                    if (siri != null)
                        if (!siri.FriendList.Contains(other.Status.PlayerId) &&
                            other.Status.PlayerId != me.Status.PlayerId)
                            customString += " <:fr:563063244097585162>";
                    break;
                case "Загадочный Спартанец в маске":

                    var SpartanShame = _gameGlobal.SpartanShame.Find(x =>
                        x.GameId == game.GameId && x.PlayerId == me.Status.PlayerId);

                    if (!SpartanShame.FriendList.Contains(other.Status.PlayerId) &&
                        other.Status.PlayerId != me.Status.PlayerId)
                        customString += " <:yasuo:895819754428833833>";

                    if (SpartanShame.FriendList.Contains(other.Status.PlayerId) &&
                        other.Status.PlayerId != me.Status.PlayerId && other.Character.Name == "mylorik")
                        customString += " <:Spartaneon:899847724936089671>";


                    var SpartanMark = _gameGlobal.SpartanMark.Find(x =>
                        x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);

                    if (SpartanMark.FriendList.Contains(other.Status.PlayerId))
                        customString += " <:sparta:561287745675329567>";


                    break;


                case "DeepList":

                    //tactic
                    var deep = _gameGlobal.DeepListDoubtfulTactic.Find(x =>
                        x.PlayerId == me.Status.PlayerId && me.GameId == x.GameId);
                    if (deep != null)
                        if (deep.FriendList.Contains(other.Status.PlayerId) &&
                            other.Status.PlayerId != me.Status.PlayerId)
                            customString += " <:yo_filled:902361411840266310>";
                    //end tactic

                    //сверхразум
                    var currentList = _gameGlobal.DeepListSupermindKnown.Find(x =>
                        x.PlayerId == me.Status.PlayerId && x.GameId == me.GameId);
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(other.Status.PlayerId))
                            customString +=
                                $" PS: - {other.Character.Name} (I: {other.Character.GetIntelligence()} | " +
                                $"St: {other.Character.GetStrength()} | Sp: {other.Character.GetSpeed()} | " +
                                $"Ps: {other.Character.GetPsyche()} | J: {other.Character.Justice.GetJusticeNow()})";
                    //end сверхразум


                    //стёб
                    var currentDeepList =
                        _gameGlobal.DeepListMockeryList.Find(x =>
                            x.PlayerId == me.Status.PlayerId && game.GameId == x.GameId);

                    if (currentDeepList != null)
                    {
                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == other.Status.PlayerId);

                        if (currentDeepList2 != null)
                        {
                            if (currentDeepList2.Times % 2 == 1)
                                customString += " **лол**";
                            else
                                customString += " **кек**";
                        }
                    }

                    //end стёб


                    break;

                case "mylorik":
                    var mylorik = _gameGlobal.MylorikRevenge.Find(x =>
                        x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == other.Status.PlayerId);

                    if (find != null && find.IsUnique) customString += " <:sparta:561287745675329567>";
                    if (find != null && !find.IsUnique) customString += " ❌";

                    break;
                case "Тигр":
                    var tigr1 = _gameGlobal.TigrTwoBetterList.Find(x =>
                        x.PlayerId == me.Status.PlayerId && x.GameId == me.GameId);

                    if (tigr1 != null)
                        //if (tigr1.FriendList.Contains(other.Status.PlayerId) && other.Status.PlayerId != me.Status.PlayerId)
                        if (tigr1.FriendList.Contains(other.Status.PlayerId))
                            customString += " <:pepe_down:896514760823144478>";

                    var tigr2 = _gameGlobal.TigrThreeZeroList.Find(x =>
                        x.GameId == me.GameId && x.PlayerId == me.Status.PlayerId);

                    var enemy = tigr2?.FriendList.Find(x => x.EnemyPlayerId == other.Status.PlayerId);

                    if (enemy != null)
                    {
                        if (enemy.WinsSeries == 1)
                            customString += " 1:0";
                        else if (enemy.WinsSeries == 2)
                            customString += " 2:0";
                        else if (enemy.WinsSeries >= 3) customString += " 3:0, обоссан";
                    }

                    break;
            }

            var knownClass = me.Status.KnownPlayerClass.Find(x => x.EnemyId == other.Status.PlayerId);
            if (knownClass != null && me.Character.Name != "AWDKA")
                customString += $" {knownClass.Text}";


            if (game.RoundNo == 11)
                customString += $" (as **{other.Character.Name}**) = {other.Status.GetScore()} Score";

            if (me.PlayerType == 2)
            {
                customString += $" (as **{other.Character.Name}**) = {other.Status.GetScore()} Score";
                customString +=
                    $" (I: {other.Character.GetIntelligence()} | St: {other.Character.GetStrength()} | Sp: {other.Character.GetSpeed()} | Ps: {other.Character.GetPsyche()})";
            }

            var predicted = me.Predict.Find(x => x.PlayerId == other.Status.PlayerId);
            if (predicted != null)
                customString += $"<:e_:562879579694301184>|<:e_:562879579694301184>{predicted.CharacterName} ?";

            return customString;
        }

        public async Task EndGame(SocketMessageComponent button)
        {
            _helperFunctions.SubstituteUserWithBot(button.User.Id);
            var globalAccount = _global.Client.GetUser(button.User.Id);
            var account = _accounts.GetAccount(globalAccount);
            account.IsPlaying = false;


            //  await socketMsg.DeleteAsync();
            await globalAccount.SendMessageAsync("Спасибо за игру!");
        }

        private static IEnumerable<string> Split(string str, int chunkSize)
        {
            return Enumerable.Range(0, str.Length / chunkSize)
                .Select(i => str.Substring(i * chunkSize, chunkSize));
        }


        public string HandleCasualNormalSkillShow(string text, GamePlayerBridgeClass player, GameClass game)
        {
            if (player.PlayerType == 0)
            {
                foreach (var p in game.PlayersList)
                {
                    if (p.Status.PlayerId == player.Status.PlayerId)
                        continue;
                    foreach (var passive in p.Character.Passive)
                    {
                        if (passive.PassiveName != "Запах мусора" && passive.PassiveName != "Чернильная завеса")
                        {
                            text = text.Replace($"{passive.PassiveName}", "❓");
                        }
                    }
                }
            }

            if (text.Contains("Евреи..."))
            {
                var temp = "";
                var jewSplit = text.Split('\n');
                foreach (var line in jewSplit)
                {
                    if (line.Contains("Евреи..."))
                        temp += line + "\n";
                }
                foreach (var line in jewSplit)
                {
                    if (!line.Contains("Евреи..."))
                        temp += line + "\n";
                }
                text = temp;
            }

            return text;
        }

        //Page 1
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
                                  "<:e_:562879579694301184>\n" +
                                  $"{LeaderBoard(player)}");


            var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

            var text = "";
            if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
            {
                text = splitLogs[^2];
                text = HandleCasualNormalSkillShow(text, player, game);
                embed.AddField("События прошлого раунда:", $"{text}");
            }
            else
            {
                embed.AddField("События прошлого раунда:", "В прошлом раунде ничего не произошло. Странно...");
            }

            text = player.Status.GetInGamePersonalLogs().Length >= 2 ? $"{player.Status.GetInGamePersonalLogs()}" : "Еще ничего не произошло. Наверное...";
            text = HandleCasualNormalSkillShow(text, player, game);
            embed.AddField("События этого раунда:", text);


            if (character.Avatar != null)
                embed.WithThumbnailUrl(character.Avatar);

            return embed;
        }

        //Page 2
        public EmbedBuilder LogsPage(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);

            var embed = new EmbedBuilder();
            embed.WithTitle("Логи");
            embed.WithDescription(game.GetAllGlobalLogs());
            embed.WithColor(Color.Green);
            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.WithCurrentTimestamp();

            return embed;
        }

        //Page 3
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

            embed.WithFooter($"{GetTimeLeft(player)}");
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


        public SelectMenuBuilder GetAttackMenu(GamePlayerBridgeClass player, GameClass game)
        {
            var playerIsReady = player.Status.IsSkip || player.Status.IsReady || game.RoundNo > 10;
            var placeHolder = "Выбрать цель";

            if (player.Status.IsSkip)
            {
                placeHolder = "Что-то заставило тебя скипнуть...";
            }

            if (player.Status.IsBlock)
            {
                placeHolder = "Ты поставил блок!";
            }

            if (game.RoundNo > 10)
            {
                placeHolder = "gg wp";
            }

            if (player.Status.IsReady)
            {
                var target = game.PlayersList.Find(x => x.Status.PlayerId == player.Status.WhoToAttackThisTurn);
                if (target != null)
                {
                    placeHolder = $"Ты напал на {target.DiscordUsername}";
                }
            }

            if (!player.Status.CanSelectAttack)
            {
                playerIsReady = true;
                placeHolder = "Подтвердите свои предложение перед атакой!";
            }

            var attackMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("attack-select")
                .WithDisabled(playerIsReady)
                .WithPlaceholder(placeHolder);


            if (game != null)
                for (var i = 0; i < _playerChoiceAttackList.Count; i++)
                {
                    var playerToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == i + 1);
                    if (playerToAttack == null) continue;
                    if (playerToAttack.DiscordId != player.DiscordId)
                        attackMenu.AddOption("Напасть на " + playerToAttack.DiscordUsername, $"{i + 1}",
                            emote: _playerChoiceAttackList[i]);
                }

            return attackMenu;
        }

        public SelectMenuBuilder GetPredictMenu(GamePlayerBridgeClass player, GameClass game)
        {
            var predictMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("predict-1")
                .WithDisabled(game.RoundNo >= 9)
                .WithPlaceholder("Сделать предположение");

            if (game != null)
                for (var i = 0; i < _playerChoiceAttackList.Count; i++)
                {
                    var playerToAttack = game.PlayersList.Find(x => x.Status.PlaceAtLeaderBoard == i + 1);
                    if (playerToAttack == null) continue;
                    if (playerToAttack.DiscordId != player.DiscordId)
                        predictMenu.AddOption(playerToAttack.DiscordUsername + " это...",
                            playerToAttack.DiscordUsername,
                            emote: _playerChoiceAttackList[i]);
                }


            return predictMenu;
        }


        public async Task<SelectMenuBuilder> GetLvlUpMenu(GamePlayerBridgeClass player, GameClass game)
        {
            var charMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("char-select")
                .WithPlaceholder("Выбор прокачки")
                .AddOption("Интеллект", "1")
                .AddOption("Сила", "2")
                .AddOption("Скорость", "3")
                .AddOption("Психика", "4");


            //Да всё нахуй эту игру Part #4
            if (game.RoundNo == 9 && player.Character.GetPsyche() == 4 && player.Character.Name == "Darksci")
            {
                charMenu = new SelectMenuBuilder()
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .WithCustomId("char-select")
                    .WithPlaceholder("\"Выбор\" прокачки")
                    .AddOption("Психика", "4");
                await _helperFunctions.SendMsgAndDeleteItAfterRound(player, "Riot Games: бери smite и не выебывайся");
            }
            //end Да всё нахуй эту игру: Part #4


            return charMenu;
        }


        public ButtonBuilder GetMoralButton(GamePlayerBridgeClass player, GameClass game)
        {
            var disabled = game is not {RoundNo: <= 10};
            var extraText = "";
            if (game.RoundNo == 10) extraText = " (Конец игры)";

            if (player.Character.GetMoral() >= 15)
                return new ButtonBuilder($"Обменять 15 Морали на 10 бонусных очков{extraText}", "moral",
                    ButtonStyle.Secondary, disabled: disabled);
            if (player.Character.GetMoral() >= 10)
                return new ButtonBuilder($"Обменять 10 Морали на 6 бонусных очков{extraText}", "moral",
                    ButtonStyle.Secondary, disabled: disabled);
            if (player.Character.GetMoral() >= 5)
                return new ButtonBuilder($"Обменять 5 Морали на 2 бонусных очка{extraText}", "moral",
                    ButtonStyle.Secondary, disabled: disabled);
            if (player.Character.GetMoral() >= 3)
                return new ButtonBuilder($"Обменять 3 Морали на 1 бонусное очко{extraText}", "moral",
                    ButtonStyle.Secondary, disabled: disabled);
            return new ButtonBuilder("Недостаточно очков морали", "moral", ButtonStyle.Secondary, disabled: true);
        }


        public ButtonBuilder GetBlockButton(GamePlayerBridgeClass player, GameClass game)
        {
            var playerIsReady = player.Status.IsSkip || player.Status.IsReady || game.RoundNo > 10;
            return new ButtonBuilder("Блок", "block", ButtonStyle.Success, disabled: playerIsReady);
        }

        public ButtonBuilder GetEndGameButton()
        {
            return new("Завершить Игру", "end", ButtonStyle.Danger);
        }

        public ButtonBuilder GetPlaceHolderButton(GamePlayerBridgeClass player, GameClass game)
        {
            if (!player.Status.CanSelectAttack)
            {
                return new ButtonBuilder("Я подтверждаю свои предположения", "confirm-prefict", ButtonStyle.Primary, disabled: false, emote:
                    Emote.Parse("<a:bratishka:900962522276958298>"));
            }
            else
            {
                return new ButtonBuilder("Братишка валяется почему-то...", "boole", ButtonStyle.Secondary, disabled: true, emote:
                    Emote.Parse("<a:bratishka:900962522276958298>"));
            }

        }


        public async Task UpdateMessage(GamePlayerBridgeClass player)
        {
            if (player.IsBot()) return;

            var game = _global.GamesList.Find(x => x.GameId == player.GameId);
            var embed = new EmbedBuilder();
            var builder = new ComponentBuilder();

            switch (player.Status.MoveListPage)
            {
                case 1:
                    embed = FightPage(player);
                    builder = new ComponentBuilder();
                    builder.WithButton(GetBlockButton(player, game));
                    builder.WithButton(GetMoralButton(player, game));
                    builder.WithButton(GetEndGameButton());
                    builder.WithSelectMenu(GetAttackMenu(player, game), 1);
                    builder.WithButton(GetPlaceHolderButton(player, game), 2);
                    builder.WithSelectMenu(GetPredictMenu(player, game), 3);
                    break;
                case 2:
                    embed = LogsPage(player);
                    builder = new ComponentBuilder();
                    break;
                case 3:
                    embed = LvlUpPage(player);
                    builder = new ComponentBuilder().WithSelectMenu(await GetLvlUpMenu(player, game));

                    //Да всё нахуй эту игру Part #5
                    if (game.RoundNo == 9 && player.Character.GetPsyche() == 4 && player.Character.Name == "Darksci")
                        builder.WithButton("Riot style \"choice\"", "crutch", row: 1, style: ButtonStyle.Secondary,
                            disabled: true);
                    //end Да всё нахуй эту игру: Part #5
                    break;
            }

            embed.WithFooter($"{GetTimeLeft(player)}");

            await UpdateMessageWithEmbed(player, embed, builder);
        }


        public async Task UpdateMessageWithEmbed(GamePlayerBridgeClass player, EmbedBuilder embed,
            ComponentBuilder builder)
        {
            try
            {
                if (!player.IsBot() && !embed.Footer.Text.Contains("ERROR"))
                    await player.Status.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });
            }
            catch (Exception e)
            {
                _log.Critical(e.StackTrace);
            }
        }


        public string GetTimeLeft(GamePlayerBridgeClass player)
        {
            var game = _global.GamesList.Find(x => x.GameId == player.GameId);

            if (game == null) return "ERROR";
            var time = $"({game.TimePassed.Elapsed.Seconds}/{game.TurnLengthInSecond}с)";
            if (player.Status.IsReady)
                return $"Ожидаем других игроков • {time} | {game.GameVersion}";
            return $"{time} | {game.GameVersion}";
        }
    }
}