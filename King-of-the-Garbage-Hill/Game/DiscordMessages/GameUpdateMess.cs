using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.DiscordMessages;

public sealed class GameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly CharactersPull _charactersPull;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;

    private readonly List<Emoji> _playerChoiceAttackList = new()
        { new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣"), new Emoji("5⃣"), new Emoji("6⃣") };

    private readonly SecureRandom _random;

    private readonly List<string> _vampyrGarlic = new()
    {
        "Никаких статов для тебя, поешь чеснока", "Иди отсюда, Вампур позорный", "А ну хватит кусаться!",
        "Клыки наточил?"
    };


    public GameUpdateMess(UserAccounts accounts, Global global, HelperFunctions helperFunctions, SecureRandom random,
        CharactersPull charactersPull)
    {
        _accounts = accounts;
        _global = global;

        _helperFunctions = helperFunctions;

        _random = random;
        _charactersPull = charactersPull;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public async Task ShowRulesAndChar(SocketUser user, GamePlayerBridgeClass player)
    {
        //var allCharacters = _charactersPull.GetAllCharacters();
        var character = player.GameCharacter;


        var intStr = "Интеллект";
        var strStr = "Сила";
        var speStr = "Скорость";
        var psyStr = "Психика";

        //Sakura
        if (character!.Name == "Sakura")
        {
            intStr = "Сексуальность";
            strStr = "Грубость";
            speStr = "Скорость";
            psyStr = "Нытье";
        }

        var sakuraText = "";
        if (player.GameCharacter.Passive.Count == 0) sakuraText = "\nИх... нет...\n";
        //end Sakura

        var embed = new EmbedBuilder();
        embed.WithColor(Color.DarkOrange);
        //if (character.Avatar != null)
        //     embed.WithImageUrl(character.Avatar);
        embed.AddField("Твой Персонаж:", $"Name: {character.Name}\n" +
                                         $"{intStr}: {character.GetIntelligenceString()}\n" +
                                         $"{strStr}: {character.GetStrength()}\n" +
                                         $"{speStr}: {character.GetSpeed()}\n" +
                                         $"{psyStr}: {character.GetPsyche()}\n" +
                                         $"\n**Пассивки:**{sakuraText}");

        foreach (var passive in player.GameCharacter.Passive)
        {
            if (!passive.Visible) continue;
            embed.AddField(passive.PassiveName, passive.PassiveDescription);
        }


        //if(character.Description.Length > 1)
        //    embed.WithDescription(character.Description);


        await user.SendMessageAsync("", false, embed.Build());
    }

    public async Task WaitMess(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.DiscordId <= 1000000) return;

        var globalAccount = _global.Client.GetUser(player.DiscordId);

        if (!game.IsAramPickPhase)
        {
            await ShowRulesAndChar(globalAccount, player);
        }

        var mainPage = new EmbedBuilder();
        mainPage.WithAuthor(globalAccount);
        mainPage.WithFooter("Preparation time...");
        mainPage.WithColor(Color.DarkGreen);
        mainPage.AddField("Game is being ready", "**Please wait for the main menu**");


        var socketMessage = await globalAccount.SendMessageAsync("", false, mainPage.Build());
        //var socketSecondaryMessage = await globalAccount.SendMessageAsync("Раунд #1");

        player.DiscordStatus.SocketMessageFromBot = socketMessage;
        //player.DiscordStatus.SocketSecondaryMessageFromBot = socketSecondaryMessage;
    }

    public string LeaderBoard(GamePlayerBridgeClass player)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        if (game == null) return "ERROR 404";
        var players = "";
        var playersList = game.PlayersList.Where(x => !x.Passives.KratosIsDead).ToList();

        for (var i = 0; i < playersList.Count; i++)
        {
            players += CustomLeaderBoardBeforeNumber(player, playersList[i], game, i + 1);
            var sanitizedDiscordUsername = playersList[i].DiscordUsername.Replace("_", "\\_")
                .Replace("*", "\\*")
                .Replace("~", "\\~")
                .Replace("`", "\\`");

            var teamString = "";
            if (playersList[i].TeamId > 0)
                teamString = player.TeamId == playersList[i].TeamId
                    ? $"**[{playersList[i].TeamId}]** "
                    : $"[{playersList[i].TeamId}] ";

            players += $"{teamString}{i + 1}. {sanitizedDiscordUsername}";

            players += CustomLeaderBoardAfterPlayer(player, playersList[i], game);

            if (player.GetPlayerId() == playersList[i].GetPlayerId())
                players += $" = **{playersList[i].Status.GetScore()} Score**";


            players += "\n";
        }

        return players;
    }

    public string CustomLeaderBoardBeforeNumber(GamePlayerBridgeClass player1, GamePlayerBridgeClass player2,
        GameClass game, int number)
    {
        var customString = "";

        foreach (var passive in player1.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "Раскинуть щупальца":
                    if (!player1.Passives.OctopusTentaclesList.LeaderboardPlace.Contains(number)) customString += "🐙";
                    break;

                case "Челюсти":
                    if (!player1.Passives.SharkJawsLeader.FriendList.Contains(number)) customString += "🐙";
                    break;
            }


        return customString + " ";
    }

    public string CustomLeaderBoardAfterPlayer(GamePlayerBridgeClass me, GamePlayerBridgeClass other, GameClass game)
    {
        var customString = "";
        //|| me.DiscordId == 238337696316129280 || me.DiscordId == 181514288278536193

        foreach (var passive in me.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "Следит за игрой":
                    foreach (var metaPlayer in me.Passives.YongGlebMetaClass)
                    {
                        if (other.GetPlayerId() == metaPlayer)
                        {
                            customString += " **META**";
                        }
                    }
                    break;

                case "Weedwick Pet":
                    if (other.GameCharacter.Passive.Any(x => x.PassiveName == "DeepList Pet"))
                        customString += " <:pet:1046330623498911744>";
                    break;

                case "Weed":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    if (other.Passives.WeedwickWeed > 0)
                        customString += $" <:weed:1005884006866354196>: {other.Passives.WeedwickWeed}";
                    break;

                case "Безжалостный охотник":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    if (other.GameCharacter.Justice.GetRealJusticeNow() == 0)
                        customString += " <:WUF:1005886339335598120>";
                    break;

                case "Ценная добыча":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    if (other.GameCharacter.GetWinStreak() > 0)
                        customString += $" <:bong:1046462826539130950>: {other.GameCharacter.GetWinStreak()}";
                    break;

                case "Я пытаюсь!":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    var awdka = me.Passives.AwdkaTryingList;
                    var awdkaTrying = awdka.TryingList.Find(x => x.EnemyPlayerId == other.GetPlayerId());

                    if (awdkaTrying != null)
                    {
                        if (!awdkaTrying.IsUnique) customString += " <:bronze:565744159680626700>";
                        else customString += " <:plat:565745613208158233>";
                    }

                    break;

                case "Научите играть":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    var awdkaTrainingHistory = me.Passives.AwdkaTeachToPlayHistory;
                    if (awdkaTrainingHistory != null)
                    {
                        var awdkaTrainingHistoryEnemy =
                            awdkaTrainingHistory.History.Find(x => x.EnemyPlayerId == other.GetPlayerId());
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

                case "Челюсти":
                    var shark = me.Passives.SharkJawsWin;
                    if (!shark.FriendList.Contains(other.GetPlayerId()) && other.GetPlayerId() != me.GetPlayerId())
                        customString += " <:jaws:565741834219945986>";
                    break;

                case "Повезло":
                    var dar = me.Passives.DarksciLuckyList;

                    if (!dar.TouchedPlayers.Contains(other.GetPlayerId()) &&
                        other.GetPlayerId() != me.GetPlayerId())
                        customString += " <:luck:1051721236322988092>";


                    break;
                case "Гематофагия":
                    var vamp = me.Passives.VampyrHematophagiaList;
                    var target = vamp.HematophagiaCurrent.Find(x => x.EnemyId == other.GetPlayerId());
                    if (target != null)
                        customString += " <:Y_:562885385395634196>";
                    break;

                case "Доебаться":
                    var hardKitty = me.Passives.HardKittyDoebatsya;
                    if (hardKitty != null)
                    {
                        var lostSeries = hardKitty.LostSeriesCurrent.Find(x => x.EnemyPlayerId == other.GetPlayerId());
                        if (lostSeries != null)
                            switch (lostSeries.Series)
                            {
                                case > 6:
                                    customString += $" <:LoveLetter:998306315342454884>: {lostSeries.Series}";
                                    break;
                                case > 0:
                                    customString += $" <:393:563063205811847188>: {lostSeries.Series}";
                                    break;
                            }
                    }

                    break;

                case "Обучение":
                    var siriTraining = me.Passives.SirinoksTraining;
                    if (siriTraining != null && siriTraining.Training.Count > 0)
                    {
                        var training = siriTraining.Training.First();
                        if (other.GetPlayerId() == siriTraining.EnemyId)
                        {
                            switch (training.StatIndex)
                            {
                                case 1:
                                    customString += " <:edu:1003751490290204753>";
                                    break;
                                case 2:
                                    customString += " <:edu:1003751490290204753>";
                                    break;
                                case 3:
                                    customString += " <:edu:1003751490290204753>";
                                    break;
                                case 4:
                                    customString += " <:edu:1003751490290204753>";
                                    break;
                            }

                            if (other.GameCharacter.Name is "Братишка" or "Осьминожка" or "Краборак" or "mylorik")
                                customString += " **Буль!**";
                        }
                    }

                    break;

                case "Заводить друзей":
                    var siri = me.Passives.SirinoksFriendsList;
                    if (siri != null)
                        if (!siri.FriendList.Contains(other.GetPlayerId()) && other.GetPlayerId() != me.GetPlayerId())
                            customString += " <:fr:563063244097585162>";
                    break;

                case "Они позорят военное искусство":

                    var spartanShame = me.Passives.SpartanShame;

                    if (!spartanShame.FriendList.Contains(other.GetPlayerId()) &&
                        other.GetPlayerId() != me.GetPlayerId())
                        customString += " <:yasuo:895819754428833833>";

                    if (spartanShame.FriendList.Contains(other.GetPlayerId()) &&
                        other.GetPlayerId() != me.GetPlayerId() && other.GameCharacter.Name == "mylorik")
                        customString += " <:Spartaneon:899847724936089671>";
                    break;

                case "Им это не понравится":
                    var spartanMark = me.Passives.SpartanMark;

                    if (spartanMark.FriendList.Contains(other.GetPlayerId()))
                        customString += " <:sparta:561287745675329567>";
                    break;

                case "DeepList Pet":
                    if (other.GameCharacter.Passive.Any(x => x.PassiveName == "Weedwick Pet"))
                        customString += " <:pet:1046330623498911744>";
                    break;

                case "Сомнительная тактика":
                    //tactic
                    var deep = me.Passives.DeepListDoubtfulTactic;
                    if (deep != null)
                        if (deep.FriendList.Contains(other.GetPlayerId()) &&
                            other.GetPlayerId() != me.GetPlayerId())
                            customString += " <:yo_filled:902361411840266310>";
                    //end tactic
                    break;

                case "Сверхразум":
                    //сверхразум
                    var currentList = me.Passives.DeepListSupermindKnown;
                    if (currentList != null)
                        if (currentList.KnownPlayers.Contains(other.GetPlayerId()))
                            customString +=
                                $" PS: - {other.GameCharacter.Name} (I: {other.GameCharacter.GetIntelligence()} | " +
                                $"St: {other.GameCharacter.GetStrength()} | Sp: {other.GameCharacter.GetSpeed()} | " +
                                $"Ps: {other.GameCharacter.GetPsyche()} | J: {other.GameCharacter.Justice.GetRealJusticeNow()})";
                    //end сверхразум

                    break;

                case "Стёб":
                    //стёб
                    var currentDeepList = me.Passives.DeepListMockeryList;

                    if (currentDeepList != null)
                    {
                        var currentDeepList2 =
                            currentDeepList.WhoWonTimes.Find(x => x.EnemyPlayerId == other.GetPlayerId());

                        if (currentDeepList2 != null)
                        {
                            if (currentDeepList2.Times == 1)
                                customString += " **лол**";
                            if (currentDeepList2.Triggered)
                                customString += " **кек**";
                        }
                    }

                    //end стёб
                    break;

                case "Месть":
                    var mylorik = me.Passives.MylorikRevenge;
                    var find = mylorik?.EnemyListPlayerIds.Find(x =>
                        x.EnemyPlayerId == other.GetPlayerId());

                    if (find is { IsUnique: true }) customString += " <:sparta:561287745675329567>";
                    if (find is { IsUnique: false }) customString += " ❌";
                    break;

                case "Спарта":
                    var mylorikSpartan = me.Passives.MylorikSpartan;

                    var mylorikEnemy = mylorikSpartan.Enemies.Find(x => x.EnemyId == other.GetPlayerId());

                    if (mylorikEnemy is { LostTimes: > 0 })
                        switch (mylorikEnemy.LostTimes)
                        {
                            case 1:
                                customString += " <:broken_shield:902044789917241404>";
                                break;
                            case 2:
                                customString +=
                                    " <:broken_shield:902044789917241404><:broken_shield:902044789917241404>";
                                break;
                            case 3:
                                customString +=
                                    " <:broken_shield:902044789917241404><:broken_shield:902044789917241404>🍰🍰";
                                break;
                            case 4:
                            case 5:
                            case 6:
                            case 7:
                            case 8:
                            case 9:
                            case 10:
                                customString +=
                                    " <:broken_shield:902044789917241404><:broken_shield:902044789917241404><:broken_shield:902044789917241404><:broken_shield:902044789917241404><:broken_shield:902044789917241404><:broken_shield:902044789917241404><:broken_shield:902044789917241404>🎂 **НЯМ!**";
                                break;
                        }

                    break;

                case "Лучше с двумя, чем с адекватными":
                    var tigr1 = me.Passives.TigrTwoBetterList;

                    if (tigr1 != null)
                        //if (tigr1.FriendList.Contains(other.GetPlayerId()) && other.GetPlayerId() != me.GetPlayerId())
                        if (tigr1.FriendList.Contains(other.GetPlayerId()))
                            customString += " <:pepe_down:896514760823144478>";
                    break;

                case "3-0 обоссан":
                    var tigr2 = me.Passives.TigrThreeZeroList;

                    var enemy = tigr2?.FriendList.Find(x => x.EnemyPlayerId == other.GetPlayerId());

                    if (enemy != null)
                    {
                        switch (enemy.WinsSeries)
                        {
                            case 1:
                                customString += " 1:0";
                                break;
                            case 2:
                                customString += " 2:0";
                                break;
                            default:
                                if (enemy.WinsSeries >= 3 || !enemy.IsUnique) 
                                    customString += " 3:0, обоссан";
                                break;
                        }
                    }
                    break;
            }

        var knownClass = me.Status.KnownPlayerClass.Find(x => x.EnemyId == other.GetPlayerId());

        //if (knownClass != null && me.GameCharacter.Name != "AWDKA")
        if (knownClass != null)
            customString += $" {knownClass.Text}";


        if (game.RoundNo >= 11 && !game.IsKratosEvent)
            customString += $" (as **{other.GameCharacter.Name}**) = {other.Status.GetScore()} Score";

        if (me.PlayerType == 2)
        {
            customString += $" (as **{other.GameCharacter.Name}**) = {other.Status.GetScore()} Score";
            customString +=
                $" (I: {other.GameCharacter.GetIntelligence()} | St: {other.GameCharacter.GetStrength()} | Sp: {other.GameCharacter.GetSpeed()} | Ps: {other.GameCharacter.GetPsyche()})";
        }

        var predicted = me.Predict.Find(x => x.PlayerId == other.GetPlayerId());
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
        await globalAccount.SendMessageAsync(
            "Спасибо за игру!\nА вы заметили? Это многопользовательская игра до 6 игроков! Вы можете начать игру с другом пинганув его! Например `*st @Boole`");
    }

    /*
    private static IEnumerable<string> Split(string str, int chunkSize)
    {
        return Enumerable.Range(0, str.Length / chunkSize).Select(i => str.Substring(i * chunkSize, chunkSize));
    }
    */

    public string SortLogs(string textOriginal, GamePlayerBridgeClass player, GameClass game)
    {
        var text = textOriginal;
        if (player.PlayerType == 0)
            text = game.PlayersList.Where(p => p.GetPlayerId() != player.GetPlayerId()).Aggregate(text,
                (current1, p) => p.GameCharacter.Passive
                    .Where(passive =>
                        passive.PassiveName != "Запах мусора" && passive.PassiveName != "Чернильная завеса" &&
                        passive.PassiveName != "Еврей" && passive.PassiveName != "2kxaoc").Aggregate(current1,
                        (current, passive) => current.Replace($"{passive.PassiveName}", "Неизвестно")));
        else
            text = game.PlayersList.Where(p => p.GetPlayerId() != player.GetPlayerId()).Aggregate(text,
                (current1, p) => p.GameCharacter.Passive
                    .Where(passive =>
                        passive.PassiveName != "Запах мусора" && passive.PassiveName != "Чернильная завеса" &&
                        passive.PassiveName != "Еврей" && passive.PassiveName != "2kxaoc").Aggregate(current1,
                        (current, passive) => current.Replace($"{passive.PassiveName}", $"❓ {passive.PassiveName}")));

        var separationLine = false;
        var orderedList = new List<string>
        {
            "Вы улучшили", "|>PhraseBeforeFight<|", "Обмен Морали", "Вы использовали Авто Ход", "Вы напали на", "Вы поставили блок",  
            "дополнительного вреда", "TOO GOOD", "TOO STONK", "|>Phrase<|", "|>SeparationLine<|", "Поражение:", "Получено вреда:", "Победа:", "Читы",
            "Справедливость", "Класс:", "Мишень", "__**бонусных**__ очков", "Евреи...", "**обычных** очков", "**очков**"
        };


        foreach (var keyword in orderedList)
        {
            switch (keyword)
            {
                case "Класс:" when text.Contains(keyword):
                {
                    var temp = "";
                    var jewSplit = text.Split('\n');
                    var totalClass = 0;
                    var enemyType = "";

                    foreach (var l in jewSplit)
                    {
                        var line = l;
                        if (!line.Contains("Класс:"))
                        {
                            temp += line + "\n";
                        }
                        else
                        {
                            if (line.Contains("(за "))
                            {
                                enemyType = $"({line.Split("(")[1].Split(")")[0]})";
                                line = line.Replace(enemyType, "");
                                enemyType = $" {enemyType}";
                            }

                            //Класс: +20 *Cкилла* (за **умного** врага). +2 *Cкилла*
                            var classSplit = line.Replace("*", "").Replace("+", "").Split(":")[1].Split(".").ToList();
                            foreach (var classText in classSplit)
                            {
                                    try
                                    {

                                        totalClass += Convert.ToInt32(classText.Replace("Cкилла", "").Replace(" ", ""));
                                    }
                                    catch
                                    {
                                        var error_boole = 1;
                                    }
                            }

                        }
                    }


                    temp = temp.Remove(temp.Length - 1);
                    temp += $"Класс: +{totalClass} *Cкилла*{enemyType}\n";
                    text = temp;
                    break;
                }
                case "Обмен Морали" when text.Contains(keyword):
                {
                    var temp = "";
                    var jewSplit = text.Split('\n');
                    var totalSkill = 0;
                    var totalMoral = 0;

                    foreach (var line in jewSplit)
                        if (!line.Contains("Обмен Морали"))
                        {
                            temp += line + "\n";
                        }
                        else
                        {
                            var moralChangeSplit =
                                line.Replace("*", "").Replace("+", "").Split(":")[1].Split(".").ToList();
                            foreach (var moral in moralChangeSplit)
                            {
                                if (moral.Contains("Морали"))
                                    totalMoral += Convert.ToInt32(moral.Replace("Морали", "").Replace(" ", ""));

                                if (moral.Contains("Cкилла"))
                                    totalSkill += Convert.ToInt32(moral.Replace("Cкилла", "").Replace(" ", ""));
                            }
                        }

                    temp = temp.Remove(temp.Length - 1);
                    if (totalSkill > 0)
                        temp += $"Обмен Морали: +{totalSkill} *Cкилла* и {totalMoral} *Морали*\n";
                    else
                        temp += $"Обмен Морали: {totalMoral} *Морали*\n";

                    text = temp;
                    break;
                }
                case "|>SeparationLine<|":
                {
                    separationLine = true;
                    break;
                }
                case "|>Phrase<|" when text.Contains(keyword):
                {
                    var jewSplit = text.Split('\n');
                    var temp = jewSplit.Where(line => !line.Contains(keyword)).Aggregate("", (current, line) => current + line.Replace(keyword, "") + "\n");
                    text = jewSplit.Where(line => line.Contains(keyword)).Aggregate(temp, (current, line) => current + line.Replace(keyword, "") + "\n");

                    break;
                }
                case "Получено вреда:" when text.Contains("Поражение:"):
                {
                    var jewSplit = text.Split('\n');
                    var temp = "";

                    foreach (var jew in jewSplit)
                    {
                        if(jew.Contains("Поражение:") || jew.Contains("Получено вреда:"))
                            continue;
                        temp += $"{jew}\n";
                    }

                    foreach (var jew in jewSplit)
                    {
                        if (!jew.Contains("Поражение:"))
                            continue;
                        temp += $"{jew}\n";
                    }

                    temp = temp.Substring(0, temp.Length - 1);

                    foreach (var jew in jewSplit)
                    {
                        if (!jew.Contains("Получено вреда:"))
                            continue;
                        temp += $" ({jew.Replace(":", "")})\n";
                    }

                    text = temp;

                    break;
                }
                default:
                    if (text.Contains(keyword))
                    {
                        var jewSplit = text.Split('\n');
                        var temp = jewSplit.Where(line => !line.Contains(keyword)).Aggregate("", (current, line) => current + line + "\n");
                        text = jewSplit.Where(line => line.Contains(keyword)).Aggregate(temp, (current, line) => current + line + "\n");
                    }
                    break;
            }

            if (!separationLine) continue;
            separationLine = false;
            text += "　\n";
        }

        return text.Replace("\n\n", "\n").Split('\n').Where(line => line != "" && line != " ")
            .Aggregate("", (current, line) => current + line + "\n");
    }


    public string HandleIsNewPlayerDescription(string text, GamePlayerBridgeClass me, GameClass game)
    {
        text = text.Replace($"{me.DiscordUsername} <:war:561287719838547981>", $"{me.DiscordUsername} \\<\\>");
        var logsSplit = text.Split("\n").ToList();
        var sortedGameLogs = "";

        if (game.RoundNo > 1)
        {
            for (var i = 0; i < logsSplit.Count; i++)
            {
                var stdout = false;

                foreach (var player in game.PlayersList)
                {
                    if (logsSplit[i].Contains($"{player.DiscordUsername}"))
                    {
                        var fightLine = logsSplit[i];

                        var fightLineSplit = fightLine.Split("⟶");

                        var fightLineSplitSplit = fightLineSplit.First().Split("<:war:561287719838547981>");


                        if (fightLineSplitSplit.Length > 1)
                        {
                            stdout = true;
                            fightLine = fightLineSplitSplit.First().Contains($"{player.DiscordUsername}")
                                ? $"{fightLineSplitSplit.First()} <:war:561287719838547981> {fightLineSplitSplit[1]}"
                                : $"{fightLineSplitSplit[1]} <:war:561287719838547981> {fightLineSplitSplit.First()}";


                            fightLine += $" ⟶ {fightLineSplit[1]}";

                            sortedGameLogs += $"{fightLine}\n";
                            logsSplit.RemoveAt(i);
                            i--;
                        }
                    }
                }

                if (!stdout)
                {
                    sortedGameLogs += $"{logsSplit[i]}";
                    if (i < logsSplit.Count - 1)
                    {
                        sortedGameLogs += "\n";
                    }
                }
            }
        }
        else
        {
            sortedGameLogs = text;
        }

        
        var account = _accounts.GetAccount(me.DiscordId);
        if (account.IsNewPlayer) sortedGameLogs = sortedGameLogs.Replace("⟶", "⟶ победил");

        sortedGameLogs = sortedGameLogs.Replace(me.DiscordUsername, $"**{me.DiscordUsername}**");

        return sortedGameLogs;
    }

    //Page 1 - fight
    public EmbedBuilder FightPage(GamePlayerBridgeClass player)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        var character = player.GameCharacter;

        var embed = new EmbedBuilder();
        embed.WithColor(Color.Blue);
        embed.WithTitle("King of the Garbage Hill");
        embed.WithFooter($"{GetTimeLeft(player)}");
        var roundNumber = game!.RoundNo;


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


        var desc = HandleIsNewPlayerDescription(game!.GetGlobalLogs(), player, game);

        if (player.TeamId > 0) desc = desc.Replace($"Команда #{player.TeamId}", $"**Команда #{player.TeamId}**");

        var intStr = "Интеллект";
        var strStr = "Сила";
        var speStr = "Скорость";
        var psyStr = "Психика";
        if (character.Name == "Sakura")
        {
            intStr = "Сексуальность";
            strStr = "Грубость";
            speStr = "Скорость";
            psyStr = "Нытье";
        }

        /*
        var skillExtraText = "";
        var targetExtraText = "";
        if (player.GameCharacter.GetExtraSkillMultiplier() > 0) skillExtraText = $" (Множитель: **x{player.GameCharacter.GetExtraSkillMultiplier() + 1}**)";
        if (player.GameCharacter.GetTargetSkillMultiplier() > 0) targetExtraText = $" (Множитель: **x{player.GameCharacter.GetTargetSkillMultiplier() + 1}**)";
        */

        embed.WithDescription($"{desc}" +
                              "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                              $"**{intStr}:** {character.GetIntelligenceString()}{character.GetIntelligenceQualityResist()}\n" +
                              $"**{strStr}:** {character.GetStrengthString()}{character.GetStrengthQualityResist()}\n" +
                              $"**{speStr}:** {character.GetSpeedString()}{character.GetSpeedQualityResist()}\n" +
                              $"**{psyStr}:** {character.GetPsycheString()}{character.GetPsycheQualityResist()}\n" +
                              "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                              $"*Справедливость: **{character.Justice.GetRealJusticeNow()}***\n" +
                              $"*Мораль: {character.GetMoralString()}*\n" +
                              $"*Скилл: {character.GetSkillDisplay()} (Мишень: **{character.GetCurrentSkillClassTarget()}**)*\n" +
                              //$"*Скилл: {character.GetSkillDisplay()}{skillExtraText}*\n" +
                              //$"*Мишень: **{character.GetCurrentSkillClassTarget()}**{targetExtraText}*\n" +
                              $"*Класс:* {character.GetClassStatDisplayText()}\n" +
                              "**▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬**\n" +
                              $"Множитель очков: **x{multiplier}**\n" +
                              "<:e_:562879579694301184>\n" +
                              $"{LeaderBoard(player)}");


        var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

        string text;
        if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
        {
            text = splitLogs[^2];
            text = SortLogs(text, player, game);
            embed.AddField("События прошлого раунда:", $"{text}");
        }
        else
        {
            embed.AddField("События прошлого раунда:", "В прошлом раунде ничего не произошло. Странно...");
        }

        text = player.Status.GetInGamePersonalLogs().Length >= 2
            ? $"{player.Status.GetInGamePersonalLogs()}"
            : "Еще ничего не произошло. Наверное...";
        text = SortLogs(text, player, game);

        embed.AddField("События этого раунда:", text);


        embed.WithThumbnailUrl(character.AvatarCurrent);

        return embed;
    }

    //Page 2 - logs
    /*public EmbedBuilder LogsPage(GamePlayerBridgeClass player)
   {
      var game = _global.GamesList.Find(x => x.GameId == player.GameId);

       var embed = new EmbedBuilder();
       embed.WithTitle("Логи");
       embed.WithDescription(game.GetAllGlobalLogs());
       embed.WithColor(Color.Green);
       embed.WithFooter($"{GetTimeLeft(player)}");
       embed.WithCurrentTimestamp();

       return embed;
}*/

    //Page 3 - lvl up
    public EmbedBuilder LvlUpPage(GamePlayerBridgeClass player)
    {
        var character = player.GameCharacter;
        var embed = new EmbedBuilder();
        var text = "__Подними один из статов на 1:__";
        if (player.GameCharacter.Name == "Молодой Глеб") text = "**Понизить** один из статов на 1!";
        embed.WithColor(Color.Blue);
        embed.WithFooter($"{GetTimeLeft(player)}");
        //embed.WithCurrentTimestamp();
        embed.AddField("_____",
            $"{text}\n \n" +
            $"1. **Интеллект:** {character.GetIntelligence()}\n" +
            $"2. **Сила:** {character.GetStrength()}\n" +
            $"3. **Скорость:** {character.GetSpeed()}\n" +
            $"4. **Психика:** {character.GetPsyche()}\n");


        embed.WithThumbnailUrl(character.AvatarCurrent);

        return embed;
    }


    //Page 4 - Debug
    public EmbedBuilder DebugPage(GamePlayerBridgeClass player)
    {
        var gameCharacter = player.GameCharacter;
        var fightCharacter = player.FightCharacter;
        var status = player.Status;
        var embed = new EmbedBuilder();
        embed.WithColor(Color.DarkGreen);
        embed.WithFooter($"{GetTimeLeft(player)}");
        //embed.WithCurrentTimestamp();
        var extraDebug = status.GetFightingData();
        var text = $"Debug Info:\n" +
                   $"Stat: gameCharacter (fightCharacter)\n" +
                   $"\n" +

                   $"GetIntelligence: {gameCharacter.GetIntelligence()} ({fightCharacter.GetIntelligence()})\n" +
                   $"GetIntelligenceString: {gameCharacter.GetIntelligenceString()} ({fightCharacter.GetIntelligenceString()})\n" +
                   $"GetIntelligenceQualityResist: {gameCharacter.GetIntelligenceQualityResist()} ({fightCharacter.GetIntelligenceQualityResist()})\n" +
                   $"GetIntelligenceQualitySkillBonus: {gameCharacter.GetIntelligenceQualitySkillBonus()} ({fightCharacter.GetIntelligenceQualitySkillBonus()})\n" +

                   $"\n" +

                   $"GetStrength: {gameCharacter.GetStrength()} ({fightCharacter.GetStrength()})\n" +
                   $"GetStrengthString: {gameCharacter.GetStrengthString()} ({fightCharacter.GetStrengthString()})\n" +
                   $"GetStrengthQualityResist: {gameCharacter.GetStrengthQualityResist()} ({fightCharacter.GetStrengthQualityResist()})\n" +
                   $"GetStrengthQualityDropBonus: {gameCharacter.GetStrengthQualityDropBonus()} ({fightCharacter.GetStrengthQualityDropBonus()})\n" +
                   $"GetStrengthQualityDropTimes: {gameCharacter.GetStrengthQualityDropTimes()} ({fightCharacter.GetStrengthQualityDropTimes()})\n" +

                   $"\n" +

                   $"GetSpeed: {gameCharacter.GetSpeed()} ({fightCharacter.GetSpeed()})\n" +
                   $"GetSpeedString: {gameCharacter.GetSpeedString()} ({fightCharacter.GetSpeedString()})\n" +
                   $"GetSpeedQualityResist: {gameCharacter.GetSpeedQualityResist()} ({fightCharacter.GetSpeedQualityResist()})\n" +
                   $"GetIsSpeedQualityKiteBonus: {gameCharacter.GetIsSpeedQualityKiteBonus()} ({fightCharacter.GetIsSpeedQualityKiteBonus()})\n" +
                   $"GetSpeedQualityResistInt: {gameCharacter.GetSpeedQualityResistInt()} ({fightCharacter.GetSpeedQualityResistInt()})\n" +
                   $"GetIsSpeedQualityKiteBonus: {gameCharacter.GetIsSpeedQualityKiteBonus()} ({fightCharacter.GetIsSpeedQualityKiteBonus()})\n" +

                   $"\n" +

                   $"GetPsyche: {gameCharacter.GetPsyche()} ({fightCharacter.GetPsyche()})\n" +
                   $"GetPsycheString: {gameCharacter.GetPsycheString()} ({fightCharacter.GetPsycheString()})\n" +
                   $"GetPsycheQualityResist: {gameCharacter.GetPsycheQualityResist()} ({fightCharacter.GetPsycheQualityResist()})\n" +

                   $"\n" +

                   $"GetRealJusticeNow: {gameCharacter.Justice.GetRealJusticeNow()} ({fightCharacter.Justice.GetRealJusticeNow()})\n" +
                   $"GetSeenJusticeNow: {gameCharacter.Justice.GetSeenJusticeNow()} ({fightCharacter.Justice.GetSeenJusticeNow()})\n" +

                   $"\n" +

                   $"GetMoral: {gameCharacter.GetMoral()} ({fightCharacter.GetMoral()})\n" +
                   $"GetMoralString: {gameCharacter.GetMoralString()} ({fightCharacter.GetMoralString()})\n" +
                   $"GetBonusPointsFromMoral: {gameCharacter.GetBonusPointsFromMoral()} ({fightCharacter.GetBonusPointsFromMoral()})\n" +
                   $"GetLastMoralRound: {gameCharacter.GetLastMoralRound()} ({fightCharacter.GetLastMoralRound()})\n" +

                   $"\n" +

                   $"GetSkill: {gameCharacter.GetSkill()} ({fightCharacter.GetSkill()})\n" +
                   $"GetSkillMainOnly: {gameCharacter.GetSkillMainOnly()} ({fightCharacter.GetSkillMainOnly()})\n" +
                   $"GetSkillForOneFight: {gameCharacter.GetSkillForOneFight()} ({fightCharacter.GetSkillForOneFight()})\n" +

                   $"GetExtraSkillMultiplier: {gameCharacter.GetExtraSkillMultiplier()} ({fightCharacter.GetExtraSkillMultiplier()})\n" +
                   $"GetTargetSkillMultiplier: {gameCharacter.GetTargetSkillMultiplier()} ({fightCharacter.GetTargetSkillMultiplier()})\n" +
                   $"GetSkillFightMultiplier: {gameCharacter.GetSkillFightMultiplier()} ({fightCharacter.GetSkillFightMultiplier()})\n" +

                   $"GetSkillClass: {gameCharacter.GetSkillClass()} ({fightCharacter.GetSkillClass()})\n" +
                   $"GetSkillDisplay: {gameCharacter.GetSkillDisplay()} ({fightCharacter.GetSkillDisplay()})\n" +

                   $"GetCurrentSkillClassTarget: {gameCharacter.GetCurrentSkillClassTarget()} ({fightCharacter.GetCurrentSkillClassTarget()})\n" +
                   $"GetClassStatDisplayText: {gameCharacter.GetClassStatDisplayText()} ({fightCharacter.GetClassStatDisplayText()})\n" +

                   $"GetWhoIContre: {gameCharacter.GetWhoIContre()} ({fightCharacter.GetWhoIContre()})\n" +

                   $"\n" +

                   $"GetWinStreak: {gameCharacter.GetWinStreak()} ({fightCharacter.GetWinStreak()})\n" +
                   $"GetWonTimes: {gameCharacter.GetWonTimes()} ({fightCharacter.GetWonTimes()})\n" +

                   $"\n" +

                   $"GetScore: {status.GetScore()}\n" +
                   $"GetPlaceAtLeaderBoard: {status.GetPlaceAtLeaderBoard()}\n" +
                   $"GetScoresToGiveAtEndOfRound: {status.GetScoresToGiveAtEndOfRound()}\n" +
                   $"AutoMoveTimes: {status.AutoMoveTimes}\n" +
                   $"ChangeMindWhat: {status.ChangeMindWhat}\n" +
                   $"IsAbleToChangeMind: {status.IsAbleToChangeMind}\n" +
                   $"IsAbleToWin: {status.IsAbleToWin}\n" +
                   $"IsAutoMove: {status.IsAutoMove}\n" +

                   $"\n" +

                   $"ConfirmedPredict: {status.ConfirmedPredict}\n" +
                   $"ConfirmedSkip: {status.ConfirmedSkip}\n" +
                   $"IsBlock: {status.IsBlock}\n" +
                   $"IsReady: {status.IsReady}\n" +
                   $"IsSkip: {status.IsSkip}\n" +
                   $"IsArmorBreak: {status.IsArmorBreak}\n" +
                   $"IsSkipBreak: {status.IsSkipBreak}\n" +

                   $"\n" +

                   $"IsIntelligenceForOneFight: {status.IsIntelligenceForOneFight}\n" +
                   $"IsStrengthForOneFight: {status.IsStrengthForOneFight}\n" +
                   $"IsSpeedForOneFight: {status.IsSpeedForOneFight}\n" +
                   $"IsPsycheForOneFight: {status.IsPsycheForOneFight}\n" +
                   $"IsJusticeForOneFight: {status.IsJusticeForOneFight}\n" +
                   $"IsSkillForOneFight: {status.IsSkillForOneFight}\n" +

                   $"\n" +

                   $"LvlUpPoints: {status.LvlUpPoints}\n" +
                   $"IsPsycheForOneFight: {status.IsPsycheForOneFight}\n" +
                   $"RoundNumber: {status.RoundNumber}\n" +
                   
                   $"\n" +
                   $"----------------" +
                   $"{extraDebug}";
        

        if (text.Length < 4090)
        {
            embed.WithDescription(text);
        }
        else
        {
            var split = Split(text, 4096).ToList();
            for (var i = 0; i < split.Count; i++)
            {
                var t = split[i];
                if (i == 0)
                {
                    embed.WithDescription(t);
                }
                else
                {
                    embed.AddField("___", t);
                }
            }
        }

        
        embed.WithThumbnailUrl(gameCharacter.AvatarCurrent);
        return embed;
    }

    //Page 5 - Aram Choice
    public EmbedBuilder AramPickPage(GamePlayerBridgeClass player)
    {
        var character = player.GameCharacter;
        var embed = new EmbedBuilder();
        embed.WithColor(Color.DarkGreen);
        embed.WithTitle("ARAM Pick Stage");
        embed.WithCurrentTimestamp();
        embed.WithFooter($"{GetTimeLeft(player)}");

        var intelligence = character.GetIntelligence();
        var strength = character.GetStrength();
        var speed = character.GetSpeed();
        var psyche = character.GetPsyche();

        var realIntelligence = "";
        var realStrength = "";
        var realSpeed = "";
        var realPsyche = "";

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
        {
            intelligence = 8;
            strength = 8;
            speed = 8;
            psyche = 8;
        }

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Дерзкая школота"))
        {
            intelligence = 9;
            strength = 9;
            speed = 9;
            psyche = 9;
        }

        if (intelligence != character.GetIntelligence() || strength != character.GetStrength() ||
            speed != character.GetSpeed() || psyche != character.GetPsyche())
        {
            realIntelligence = $" ({character.GetIntelligence()})";
            realStrength = $" ({character.GetStrength()})";
            realSpeed = $" ({character.GetSpeed()})";
            realPsyche = $" ({character.GetPsyche()})";
        }

        embed.WithDescription($"**Твой ARAM Персонаж:**\n" +
                              $"Интеллект:{intelligence}{realIntelligence}\n" +
                              $"Сила: {strength}{realStrength}\n" +
                              $"Скорость: {speed}{realSpeed}\n" +
                              $"Психика: {psyche}{realPsyche}\n");


        for (var i = 0; i < player.GameCharacter.Passive.Count; i++)
        {
            var passive = player.GameCharacter.Passive[i];
            embed.AddField($"{i+1}. {passive.PassiveName}", passive.PassiveDescription);
        }


        embed.WithThumbnailUrl(character.AvatarCurrent);

        return embed;
    }
    static IEnumerable<string> Split(string str, int maxChunkSize)
    {
        for (int i = 0; i < str.Length; i += maxChunkSize)
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }

    public SelectMenuBuilder GetAttackMenu(GamePlayerBridgeClass player, GameClass game)
    {
        var isDisabled = player.Status.IsBlock || player.Status.IsSkip || player.Status.IsReady;

        //Возвращение из мертвых
        if (game.RoundNo > 10 && game.IsKratosEvent &&
            player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых"))
        {
        }
        //end Возвращение из мертвых
        else if (game.RoundNo > 10)
        {
            isDisabled = true;
        }

        var placeHolder = "Выбрать цель";

        if (player.Status.IsSkip) placeHolder = "Что-то заставило тебя скипнуть...";

        if (player.Status.IsBlock) placeHolder = "Вы поставили блок!";

        if (player.Status.IsAutoMove) placeHolder = "Вы использовали Авто Ход!";

        if (game.RoundNo > 10) placeHolder = "gg wp";

        //Возвращение из мертвых
        if (game.IsKratosEvent && player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых"))
            placeHolder = "УБИТЬ!";
        else if (game.IsKratosEvent) placeHolder = "ЭТО БОГ ВОЙНЫ! БЕГИ!";
        //end Возвращение из мертвых

        if (player.Status.IsReady)
        {
            var target = game.PlayersList.Find(x => player.Status.WhoToAttackThisTurn.Contains(x.GetPlayerId()));
            if (target != null) placeHolder = $"Вы напали на {target.DiscordUsername}";
        }

        if (!player.Status.ConfirmedPredict)
        {
            isDisabled = true;
            placeHolder = "Подтвердите свои предложение перед атакой!";
        }

        if (!player.Status.ConfirmedSkip)
        {
            isDisabled = true;
            placeHolder = "Что-то заставило тебя скипнуть...";
        }

        if (!player.Status.ConfirmedSkip &&
            player.GameCharacter.Passive.Any(x => x.PassiveName == "Стримснайпят и банят и банят и банят"))
        {
            isDisabled = true;
            placeHolder = "Обжаловать бан...";
        }

        var attackMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("attack-select")
            .WithDisabled(isDisabled)
            .WithPlaceholder(placeHolder);


        for (var i = 0; i < _playerChoiceAttackList.Count; i++)
        {
            var playerToAttack = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == i + 1);
            if (playerToAttack == null) continue;
            if (playerToAttack.DiscordId != player.DiscordId && !playerToAttack.Passives.KratosIsDead)
                attackMenu.AddOption("Напасть на " + playerToAttack.DiscordUsername, $"{i + 1}", emote: _playerChoiceAttackList[i]);
        }

        if (attackMenu.Options.Count == 0) attackMenu.AddOption("ТЫ ВСЕХ УБИЛ", "kratos-death");

        return attackMenu;
    }

    public SelectMenuBuilder GetDopaMenu(GamePlayerBridgeClass player, GameClass game)
    {
        var isDisabled = !(player.Status.IsBlock || player.Status.WhoToAttackThisTurn.Count != 0);

        var placeHolder = "Второе Действие";

        if (player.Status.IsSkip) placeHolder = "당신을 건너 뛰게 만든 무언가"; //сон

        if (game.RoundNo > 10) placeHolder = "ㅈㅈ"; //gg

        if (player.Status.IsReady)
        {
            var target = game.PlayersList.Find(x => player.Status.WhoToAttackThisTurn.Contains(x.GetPlayerId()));
            if (target != null) placeHolder = $"Вы напали на {target.DiscordUsername}";
        }

        if (!player.Status.ConfirmedPredict)
        {
            isDisabled = true;
            placeHolder = "Подтвердите свои предложение перед атакой!";
        }

        if (!player.Status.ConfirmedSkip)
        {
            isDisabled = true;
            placeHolder = "당신을 건너 뛰게 만든 무언가"; //сон
        }


        var attackMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("dopa-attack-select")
            .WithDisabled(isDisabled)
            .WithPlaceholder(placeHolder);


        for (var i = 0; i < _playerChoiceAttackList.Count; i++)
        {
            var playerToAttack = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == i + 1);
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

       



        for (var i = 0; i < _playerChoiceAttackList.Count; i++)
        {
            var playerToAttack = game.PlayersList.Find(x => x.Status.GetPlaceAtLeaderBoard() == i + 1);
            if (playerToAttack == null) continue;
            if (playerToAttack.DiscordId != player.DiscordId)
                predictMenu.AddOption(playerToAttack.DiscordUsername + " это...",
                    playerToAttack.DiscordUsername,
                    emote: _playerChoiceAttackList[i]);
        }


        if (predictMenu.Options.Count == 0)
        {
            predictMenu.AddOption("ТЫ ВСЕХ УБИЛ", "kratos-death");
        }

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Булькает"))
        {
            predictMenu.WithDisabled(true);
            predictMenu.WithPlaceholder("Бууууууль");
        }
        
        return predictMenu;
    }


    public async Task<SelectMenuBuilder> GetLvlUpMenu(GamePlayerBridgeClass player, GameClass game)
    {
        var placeholderText = "Выбор прокачки";
        if (player.GameCharacter.Name == "Вампур_")
            placeholderText = _vampyrGarlic[_random.Random(0, _vampyrGarlic.Count - 1)];

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
        {
            placeholderText = "Выбор нерфа";
        }

        var charMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("lvl-up")
            .WithPlaceholder(placeholderText)
            .AddOption("Интеллект", "1")
            .AddOption("Сила", "2")
            .AddOption("Скорость", "3")
            .AddOption("Психика", "4");


        //Да всё нахуй эту игру Part #4
        if (game.RoundNo == 9 && player.GameCharacter.GetPsyche() == 5 &&
            player.GameCharacter.Passive.Any(x => x.PassiveName == "Дизмораль"))
        {
            charMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("lvl-up")
                .WithPlaceholder("\"Выбор\" прокачки")
                .AddOption("Психика", "4");
            await _helperFunctions.SendMsgAndDeleteItAfterRound(player, "Riot Games: бери smite и не выебывайся", 0);
        }
        //end Да всё нахуй эту игру: Part #4


        return charMenu;
    }


    public ButtonBuilder GetMoralToPointsButton(GamePlayerBridgeClass player, GameClass game)
    {
        var disabled = game is not { RoundNo: <= 10 };
        if (game.IsKratosEvent)
            disabled = false;
        var extraText = "";
        if (game.RoundNo == 10) extraText = " (Конец игры)";

        //if (player.GameCharacter.Name == "Братишка")
        //    return new ButtonBuilder($"Буууууууль", "moral", ButtonStyle.Secondary, isDisabled: true);
        if (player.GameCharacter.Name == "DeepList")
            return new ButtonBuilder("Интересует только скилл", "moral", ButtonStyle.Secondary, isDisabled: true);

        if (player.GameCharacter.GetMoral() >= 20)
            return new ButtonBuilder($"на 10 бонусных очков{extraText}", "moral", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 13)
            return new ButtonBuilder($"на 5 бонусных очков{extraText}", "moral", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 8)
            return new ButtonBuilder($"на 2 бонусных очков{extraText}", "moral", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 5)
            return new ButtonBuilder($"на 1 бонусных очка{extraText}", "moral", ButtonStyle.Secondary,
                isDisabled: disabled);
        return new ButtonBuilder("Недостаточно очков Морали", "moral", ButtonStyle.Secondary, isDisabled: true);
    }

    public ButtonBuilder GetMoralToSkillButton(GamePlayerBridgeClass player, GameClass game)
    {
        if (!player.Status.ConfirmedPredict)
            return new ButtonBuilder("Я подтверждаю свои предположения", "confirm-prefict", ButtonStyle.Primary,
                isDisabled: false, emote: Emote.Parse("<a:bratishka:900962522276958298>"));
        if (!player.Status.ConfirmedSkip)
            return new ButtonBuilder("Я подтверждаю пропуск хода", "confirm-skip", ButtonStyle.Primary,
                isDisabled: false, emote: Emote.Parse("<a:bratishka:900962522276958298>"));


        var disabled = game is not { RoundNo: <= 10 };
        if (game.IsKratosEvent)
            disabled = false;
        var extraText = "";
        if (game.RoundNo == 10 && player.GameCharacter.GetMoral() < 3) extraText = " (Конец игры)";

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Булькает"))
            return new ButtonBuilder("Ничего не понимает, но булькает!", "skill", ButtonStyle.Secondary, isDisabled: true, emote: Emote.Parse("<a:bratishka:900962522276958298>"));

        if (player.GameCharacter.GetMoral() >= 20)
            return new ButtonBuilder($"Обменять 20 Морали на 100 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 13)
            return new ButtonBuilder($"Обменять 13 Морали на 50 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 7 && player.GameCharacter.Passive.Any(x => x.PassiveName == "Еврей"))
            return new ButtonBuilder($"Обменять 7 Морали на 40 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 8)
            return new ButtonBuilder($"Обменять 8 Морали на 30 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 5)
            return new ButtonBuilder($"Обменять 5 Морали на 18 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 3)
            return new ButtonBuilder($"Обменять 3 Морали на 10 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 2)
            return new ButtonBuilder($"Обменять 2 Морали на 6 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);
        if (player.GameCharacter.GetMoral() >= 1)
            return new ButtonBuilder($"Обменять 1 Морали на 2 Cкилла{extraText}", "skill", ButtonStyle.Secondary,
                isDisabled: disabled);


        return new ButtonBuilder("Недостаточно очков Морали", "skill", ButtonStyle.Secondary, isDisabled: true);
    }

    public async Task<ComponentBuilder> GetGameButtons(GamePlayerBridgeClass player, GameClass game,
        SelectMenuBuilder predictMenu = null)
    {
        var components = new ComponentBuilder();
        components.WithButton(GetBlockButton(player, game));

        if (game.GameMode != "Aram")
        {
            components.WithButton(GetAutoMoveButton(player, game));
        }

        components.WithButton(GetChangeMindButton(player, game));
        components.WithButton(GetEndGameButton(player, game));
        if (player.DiscordId is 238337696316129280 or 181514288278536193)
        {
            components.WithButton(GetAdditionalStatsButton(player, game));
        }

        components.WithSelectMenu(GetAttackMenu(player, game), 1);

        components.WithButton(GetMoralToSkillButton(player, game), 2);

        if (player.GameCharacter.GetMoral() >= 3)
            if (player.Status.ConfirmedPredict && player.Status.ConfirmedSkip)
                components.WithButton(GetMoralToPointsButton(player, game), 2);

        if (game.GameMode != "Aram")
        {
            components.WithSelectMenu(predictMenu ?? GetPredictMenu(player, game), 3);
        }


        foreach (var passive in player.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "Мне (не)везет":
                    var darksciType = player.Passives.DarksciTypeList;
                    if (game.RoundNo == 1 && !darksciType.Triggered)
                    {
                        components.WithButton(new ButtonBuilder("Мне никогда не везёт...", "stable-Darksci"), 4);
                        components.WithButton(
                            new ButtonBuilder("Мне сегодня повезёт!", "not-stable-Darksci", ButtonStyle.Danger), 4);
                        if (!darksciType.Sent)
                        {
                            darksciType.Sent = true;
                            await _helperFunctions.SendMsgAndDeleteItAfterRound(player, "Нажмешь синюю кнопку - и сказке конец. Выберешь красную - и узнаешь насколько глубока нора Даркси.", 0);
                        }
                    }

                    break;

                case "Dopa":
                    components.WithSelectMenu(GetDopaMenu(player, game), 4);
                    break;
            }

        return components;
    }

    public ComponentBuilder GetAramPickButtons(GamePlayerBridgeClass player, GameClass game)
    {
        var components = new ComponentBuilder();

        if (!player.Status.IsAramRollConfirmed)
        {
            var isRerolled1 = player.Status.AramRerolled.Contains(1);
            var isRerolled2 = player.Status.AramRerolled.Contains(2);
            var isRerolled3 = player.Status.AramRerolled.Contains(3);
            var isRerolled4 = player.Status.AramRerolled.Contains(4);
            var isRerolled5 = player.Status.AramRerolled.Contains(5);

            components.WithButton(new ButtonBuilder("Reroll #1", "aram_reroll_1", ButtonStyle.Secondary, isDisabled: isRerolled1));
            components.WithButton(new ButtonBuilder("Reroll #2", "aram_reroll_2", ButtonStyle.Secondary, isDisabled: isRerolled2));
            components.WithButton(new ButtonBuilder("Reroll #3", "aram_reroll_3", ButtonStyle.Secondary, isDisabled: isRerolled3));
            components.WithButton(new ButtonBuilder("Reroll #4", "aram_reroll_4", ButtonStyle.Secondary, isDisabled: isRerolled4));
            components.WithButton(new ButtonBuilder("Reroll Stats", "aram_reroll_5", ButtonStyle.Secondary, isDisabled: isRerolled5), row:1);
            components.WithButton(new ButtonBuilder("Confirm", "aram_roll_confirm", ButtonStyle.Success, isDisabled: false), row:2);
            components.WithButton(GetEndGameButton(player, game), row: 2);
        }
        else
        {
            components.WithButton(new ButtonBuilder("Wait for other players", "aram_roll_confirm", ButtonStyle.Success, isDisabled: true));
            //components.WithButton(GetEndGameButton(player, game));
        }

        return components;
    }


    public ButtonBuilder GetBlockButton(GamePlayerBridgeClass player, GameClass game)
    {
        var playerIsReady = player.Status.IsBlock || player.Status.IsSkip || player.Status.IsReady;
        //Возвращение из мертвых
        if (game.RoundNo > 10 && game.IsKratosEvent &&
            player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых"))
        {
        }
        //end Возвращение из мертвых
        else if (game.RoundNo > 10)
        {
            playerIsReady = true;
        }

        return new ButtonBuilder("Блок", "block", ButtonStyle.Success, isDisabled: playerIsReady);
    }

    public ButtonBuilder GetEndGameButton(GamePlayerBridgeClass player, GameClass game)
    {
        var disabled = false;
        //Возвращение из мертвых
        if (game.RoundNo > 10 && game.IsKratosEvent &&
            player.GameCharacter.Passive.Any(x => x.PassiveName == "Возвращение из мертвых"))
        {
        }
        //end Возвращение из мертвых
        else if (game.RoundNo > 10)
        {
            disabled = true;
        }

        return new ButtonBuilder("Завершить Игру", "end", ButtonStyle.Danger, isDisabled: disabled);
    }

    public ButtonBuilder GetAdditionalStatsButton(GamePlayerBridgeClass player, GameClass game)
    {
        return new ButtonBuilder("Дебаг", "debug_info", ButtonStyle.Primary, isDisabled: false);
    }


    public ButtonBuilder GetChangeMindButton(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.GameCharacter.Name == "Dopa")
            return new ButtonBuilder("선택 변경", "change-mind", ButtonStyle.Secondary, isDisabled: true);

        if (player.Status.IsReady && player.Status.IsAbleToChangeMind && !player.Status.IsSkip && game.RoundNo <= 10)
            return new ButtonBuilder("Изменить свой выбор", "change-mind", ButtonStyle.Secondary, isDisabled: false);

        return new ButtonBuilder("Изменить свой выбор", "change-mind", ButtonStyle.Secondary, isDisabled: true);
    }

    public ButtonBuilder GetAutoMoveButton(GamePlayerBridgeClass player, GameClass game)
    {
        var disabled = player.Status.IsAutoMove || player.Status.IsSkip || player.Status.IsReady ||
                       player.GameCharacter.Tier <= 3;

        if (game.TimePassed.Elapsed.TotalSeconds < 29 && player.DiscordId != 238337696316129280 &&
            player.DiscordId != 181514288278536193) disabled = true;

        return new ButtonBuilder("Авто Ход", "auto-move", ButtonStyle.Secondary, isDisabled: disabled);
    }

    public async Task UpdateMessage(GamePlayerBridgeClass player, string extraText = "")
    {
        if (player.IsBot()) return;

        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        var embed = new EmbedBuilder();
        var builder = new ComponentBuilder();

        switch (player.Status.MoveListPage)
        {
            //fight
            case 1:
                embed = FightPage(player);
                builder = await GetGameButtons(player, game);
                break;
            
            //logs
            case 2:
                // RESERVED
                /*embed = LogsPage(player);
                builder = new ComponentBuilder();*/
                break;
            
            //lvl up
            case 3:
                embed = LvlUpPage(player);
                builder = new ComponentBuilder().WithSelectMenu(await GetLvlUpMenu(player, game));

                //Да всё нахуй эту игру Part #5
                if (game!.RoundNo == 9 && player.GameCharacter.GetPsyche() == 5 &&
                    player.GameCharacter.Passive.Any(x => x.PassiveName == "Дизмораль"))
                    builder.WithButton("Riot style \"choice\"", "crutch", row: 1, style: ButtonStyle.Secondary,
                        disabled: true);
                //end Да всё нахуй эту игру: Part #5
                break;

            //debug
            case 4:
                embed = DebugPage(player);
                builder = await GetGameButtons(player, game);
                break;

            //aram pick
            case 5:
                embed = AramPickPage(player);
                builder = GetAramPickButtons(player, game);
                break;
        }


        await _helperFunctions.ModifyGameMessage(player, embed, builder, extraText);
    }


    public string GetTimeLeft(GamePlayerBridgeClass player)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);

        if (game == null)
            return "ERROR";
        var time = $"({(int)game.TimePassed.Elapsed.TotalSeconds}/{game.TurnLengthInSecond}с)";
        if (player.Status.IsReady)
            return $"Ожидаем других игроков • {time} | {game.GameVersion}";
        var toReturn = $"{time} | {game.GameVersion}";
        if (player.GameCharacter.Name is "mylorik" or "DeepList") toReturn += " | (x+х)*19";
        return toReturn;
    }
}