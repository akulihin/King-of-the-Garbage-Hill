using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.Characters;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.DiscordMessages;

public sealed class GameUpdateMess : ModuleBase<SocketCommandContext>, IServiceSingleton
{
    private readonly UserAccounts _accounts;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;
    private readonly CalculateRounds _calculateRounds;

    private readonly List<Emoji> _playerChoiceAttackList = new()
        { new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣"), new Emoji("5⃣"), new Emoji("6⃣") };

    private readonly SecureRandom _random;

    private readonly List<string> _vampyrGarlic = new()
    {
        "Никаких статов для тебя, поешь чеснока", "Иди отсюда, Вампур позорный", "А ну хватит кусаться!",
        "Клыки наточил?"
    };


    public GameUpdateMess(UserAccounts accounts, Global global, HelperFunctions helperFunctions, SecureRandom random, CalculateRounds calculateRounds)
    {
        _accounts = accounts;
        _global = global;

        _helperFunctions = helperFunctions;

        _random = random;
        _calculateRounds = calculateRounds;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }



    public EmbedBuilder GetCharacterMessage(GamePlayerBridgeClass player)
    {
        //var allCharacters = _charactersPull.GetAllCharacters();
        var character = player.GameCharacter;


        if (UnknownBug.Is(character))
        {
            var terminal = new EmbedBuilder()
                .WithColor(new Color(0, 255, 102))
                .WithTitle("runtime://character/0x????????")
                .WithDescription("```cs\nName: unknown_bug\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\n```")
                .WithThumbnailUrl(UnknownBug.MissingAvatar)
                .WithFooter("// WARN: unmanaged player object attached");

            foreach (var passive in character.Passive.Where(passive => passive.Visible))
            {
                var source = passive.PassiveDescription.Replace("`", "");
                terminal.AddField($"// module::{passive.PassiveName}", $"```cs\n{source}\n```");
            }

            return terminal;
        }

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
        else if (character.Name == ErenYeager.CharacterName)
        {
            intStr = "Злость";
            psyStr = "Самоуверенность";
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

        return embed;
    }

    public async Task DeleteGameMessage(GamePlayerBridgeClass player)
    {
        if (player.DiscordId <= 1000000) return;
        if (player.IsWebPlayer || player.PreferWeb) return;
        await player.DiscordStatus.SocketGameMessage.DeleteAsync();
    }

    public async Task SendCharacterMessage(GamePlayerBridgeClass player, SocketUser user = null)
    {
        if (player.DiscordId <= 1000000) return;
        if (player.IsWebPlayer || player.PreferWeb) return;
        user ??= _global.Client.GetUser(player.DiscordId);
        var embed = GetCharacterMessage(player);
        var message = await user.SendMessageAsync("", false, GameLocalization.EmbedForUser(player.DiscordId, embed).Build());
        player.DiscordStatus.SocketCharacterMessage = message;
    }

    public async Task UpdateCharacterMessage(GamePlayerBridgeClass player)
    {
        if (player.DiscordId <= 1000000) return;
        if (player.IsWebPlayer || player.PreferWeb) return;
        var user = _global.Client.GetUser(player.DiscordId);
        var embed = GetCharacterMessage(player);
        await player.DiscordStatus.SocketCharacterMessage.ModifyAsync(message =>
        {
            message.Embed = GameLocalization.EmbedForUser(player.DiscordId, embed).Build();
            message.Components = null;
        });
    }

    public async Task WaitMess(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.DiscordId <= 1000000) return;
        if (player.IsWebPlayer || player.PreferWeb) return;

        var globalAccount = _global.Client.GetUser(player.DiscordId);
        if (globalAccount == null) return;

        if (!game.IsAramPickPhase && !game.IsDraftPickPhase)
        {
            await SendCharacterMessage(player, globalAccount);
        }

        var mainPage = new EmbedBuilder();
        mainPage.WithAuthor(globalAccount);
        mainPage.WithFooter("Preparation time...");
        mainPage.WithColor(Color.DarkGreen);
        mainPage.AddField("Game is being ready", "**Please wait for the main menu**");


        var socketMessage = await globalAccount.SendMessageAsync("", false,
            GameLocalization.EmbedForUser(player.DiscordId, mainPage).Build());
        //var socketSecondaryMessage = await globalAccount.SendMessageAsync("Раунд #1");

        player.DiscordStatus.SocketGameMessage = socketMessage;
        //player.DiscordStatus.SocketCharacterMessage = socketSecondaryMessage;
    }

    public string LeaderBoard(GamePlayerBridgeClass player)
    {
        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        if (game == null) return "ERROR 404";

        if (game.RoundNo >= 11 && Madara.IsEternalTsukuyomiActive(game)
            && !GordonFreeman.SeesEternalTsukuyomiReality(player, game))
        {
            var projected = Madara.GetIllusoryOrder(game, player)
                .Where(x => !x.Passives.IsDead || x.GetPlayerId() == player.GetPlayerId())
                .ToList();
            var projectedBoard = "";
            for (var i = 0; i < projected.Count; i++)
            {
                var shown = projected[i];
                var shownScore = shown.GetPlayerId() == player.GetPlayerId() && !Madara.IsMadara(player)
                    ? shown.Status.GetScore() + Madara.GetIllusoryBonus(game, player)
                    : shown.Status.GetScore();
                var username = shown.DiscordUsername.Replace("_", "\\_")
                    .Replace("*", "\\*").Replace("~", "\\~").Replace("`", "\\`");
                var shownCharacter = VisibleCharacterName(player, shown);
                projectedBoard += $"{i + 1}. {username} (as **{shownCharacter}**) = {shownScore} Score\n\n";
            }
            return projectedBoard;
        }

        var players = "";
        var playersList = game.PlayersList.Where(x => !x.Passives.IsDead).ToList();

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


            players += "\n\n";
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

        // Goblin board objects — visible only to the owner/admin
        var goblinPlayer = game.PlayersList.Find(p => p.GameCharacter.Name == "Стая Гоблинов");
        if (goblinPlayer != null && (player1.GameCharacter.Name == "Стая Гоблинов" || player1.PlayerType == 2))
        {
            if (number is 1 or 2 or 6) customString += "⛏️";
            if (goblinPlayer.Passives.GoblinZiggurat.BuiltPositions.Contains(number)) customString += "🏛️";
        }

        // Protection indicators follow the same owner/admin boundary
        if ((player1.GameCharacter.Name == "Стая Гоблинов" || player1.PlayerType == 2) && player2.Passives.GoblinZiggurat.IsInZiggurat)
            customString += "🛡️";

        // The cola is a fixed board-cell object. Only Salldorum and admins see its location.
        var salldorum = game.PlayersList.Find(player => player.GameCharacter.Name == "Salldorum");
        if (salldorum != null
            && (player1.GetPlayerId() == salldorum.GetPlayerId() || player1.PlayerType == 2)
            && salldorum.Passives.SalldorumTimeCapsule.Buried
            && salldorum.Passives.SalldorumTimeCapsule.BuriedAtPosition == number)
            customString += "🥤";

        if (game.RoundNo == 10 && player2.GameCharacter.Passive.Any(
            x => x.PassiveName == "Стримснайпят и банят и банят и банят"))
            customString += "🚫";

        if ((player1.PlayerType == 2 || player1.GetPlayerId() == player2.GetPlayerId()) && Madara.IsSealed(player2))
            customString += "🚫";

        // DooM Guy demon nests — visible only to the owner/admin.
        if ((player1.GameCharacter.Name == DoomGuy.CharacterName || player1.PlayerType == 2)
            && game.PlayersList.Any(x => x.GameCharacter.Name == DoomGuy.CharacterName && x.Passives.DoomGuy.DemonNests.Contains(player2.GetPlayerId())))
            customString += "🔥";

        // Counter-attack vulnerability is also owner/admin-only character knowledge.
        if ((player1.GameCharacter.Name == DoomGuy.CharacterName || player1.PlayerType == 2)
            && game.PlayersList.Any(x => x.GameCharacter.Name == DoomGuy.CharacterName
                && x.Passives.DoomGuy.CounterAttackMarks.GetValueOrDefault(player2.GetPlayerId()) == game.RoundNo))
            customString += "🎯";

        // Headcrabs are Gordon-private information. Краборак always looks like a crab to him,
        // but is never an active mark and therefore can never be rescued for points.
        if (GordonFreeman.Is(player1))
        {
            if (player2.GameCharacter.Name == "Краборак")
            {
                customString += "🦀";
            }
            else if (player2.Passives.GordonHeadcrab.IsZombie)
            {
                customString += "🧟";
            }
            else if (player2.Passives.GordonHeadcrab.IsActive
                     && player2.Passives.GordonHeadcrab.SourceId == player1.GetPlayerId())
            {
                var roundsLeft = Math.Max(0,
                    player2.Passives.GordonHeadcrab.ExpiresAfterRound - game.RoundNo + 1);
                customString += $"🦀{roundsLeft}";
            }
        }

        // Геральт — monster type icon
        //if (player2.Passives.GeraltMonsterType != null)
        //    customString += Geralt.GetMonsterEmoji(player2.Passives.GeraltMonsterType.Value);

        return customString + " ";
    }

    public string CustomLeaderBoardAfterPlayer(GamePlayerBridgeClass me, GamePlayerBridgeClass other, GameClass game, bool isWeb = false)
    {
        var customString = "";
        //|| me.DiscordId == 238337696316129280 || me.DiscordId == 181514288278536193
        
        if (me.PlayerType == 2 && me.GameCharacter.Passive.All(x => x.PassiveName != "AdminPlayerType"))
        {
            me.GameCharacter.Passive.Add(new Passive("AdminPlayerType", "AdminPlayerType", false));
        }


        foreach (var passive in me.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "AdminPlayerType":
                    if (isWeb) break; // web unmasks characters natively for admins
                    if (other.GetPlayerId() == me.GetPlayerId()) break;

                    customString += $" = {other.Status.GetScore()} ({VisibleCharacterName(me, other)})";
                    break;

                case "Exploit":
                    if (other.Passives.IsExploitable)
                    {
                        customString += $" **EXPLOIT {game.TotalExploit}**";
                    }
                    break;

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

                case "Гоблины":
                    if (other.GetPlayerId() == me.GetPlayerId())
                    {
                        var pop = me.Passives.GoblinPopulation;
                        customString += $" 👺{pop.TotalGoblins} (⚔️{pop.Warriors} 🧙{pop.Hobs} ⛏️{pop.Workers})";
                    }
                    break;

                case "Кошачья засада":
                    if (other.GetPlayerId() == me.GetPlayerId())
                    {
                        var ambushLb = me.Passives.KotikiAmbush;
                        var stormLb = me.Passives.KotikiStorm;
                        customString += $" 🐱 Провокаций: {stormLb.TauntedPlayers.Count}/5";
                        if (ambushLb.MinkaOnPlayer != Guid.Empty)
                            customString += " | Минька на враге";
                        if (ambushLb.StormOnPlayer != Guid.Empty)
                            customString += " | Штормяк на враге";
                    }
                    // Show cat on enemy to the enemy
                    if (other.GetPlayerId() != me.GetPlayerId() && other.Passives.KotikiCatOwnerId == me.GetPlayerId())
                    {
                        customString += $" 🐱{other.Passives.KotikiCatType}";
                    }
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

                case "Вороны":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    var crows = me.Passives.ItachiCrows;
                    if (crows.CrowCounts.TryGetValue(other.GetPlayerId(), out var crowCount2) && crowCount2 > 0)
                    {
                        for (var c = 0; c < crowCount2; c++)
                            customString += " 🐦‍⬛";
                    }
                    break;

                case "Неприметность":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    var saitamaTargets = me.Passives.SaitamaUnnoticed.SeriousTargets;
                    if (saitamaTargets.Contains(other.GetPlayerId()))
                        customString += " 👊";
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

                case ErenYeager.Fighter:
                    if (other.GetPlayerId() != me.GetPlayerId() && other.Passives.ErenHatredMark > 0)
                        customString += $" 🔥{other.Passives.ErenHatredMark}";
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

                    if (Salldorum.IsRedirectedRandomTarget(game, me, other, spartanMark.FriendList))
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
                    if (isWeb) break; // web shows auto-predictions natively
                    if (UnknownBug.Is(other)) break;
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

                case "Гигантские бобы":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    var beans = me.Passives.RickGiantBeans;
                    if (beans.IngredientsActive && beans.IngredientTargets.Contains(other.GetPlayerId()))
                        customString += " 🧪";
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

                case "Впарить говна":
                    if (other.GetPlayerId() != me.GetPlayerId())
                    {
                        if (other.Passives.SellerVparitGovnaRoundsLeft > 0)
                            customString += " **💰**";
                        else if (other.GameCharacter.Passive.Any(x => x.PassiveName == "Сомнительная тактика"))
                            customString += " 💰";

                        if (game.RoundNo == 10 && other.Passives.SellerTacticBonusEarned > 0)
                            customString += $" 💸{other.Passives.SellerTacticBonusEarned}";
                    }
                    break;

                case "Вступить в союз":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    if (me.Passives.NapoleonAlliance.AllyId == other.GetPlayerId())
                        customString += " 🤝";
                    // Show ⚔️ on the player that the ally is currently targeting
                    var napAlly = game.PlayersList.Find(x => x.GetPlayerId() == me.Passives.NapoleonAlliance.AllyId);
                    if (napAlly != null && napAlly.Status.WhoToAttackThisTurn.Contains(other.GetPlayerId()))
                        customString += " ⚔️";
                    break;

                case "Premade":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    if (me.Passives.SupportPremade.MarkedPlayerId == other.GetPlayerId())
                        customString += " 🤝";
                    break;

                case "Монстр":
                    if (other.GetPlayerId() == me.GetPlayerId())
                    {
                        var pawnCount = game.PlayersList.Count(x => x.Passives.IsJohanPawn && x.Passives.JohanPawnOwnerId == me.GetPlayerId());
                        if (pawnCount > 0) customString += $" ♟️{pawnCount}";
                    }
                    else if (other.Passives.IsJohanPawn && other.Passives.JohanPawnOwnerId == me.GetPlayerId())
                    {
                        customString += " ♟️";
                    }
                    break;

                case "Пацаны":
                    if (other.GetPlayerId() == me.GetPlayerId())
                    {
                        var tbFrancie = me.Passives.TheBoysFrancie;
                        var tbButcher = me.Passives.TheBoysButcher;
                        var tbKimiko = me.Passives.TheBoysKimiko;
                        var tbMM = me.Passives.TheBoysMM;
                        customString += $" 🔪{tbButcher.PokerCount} 🧪{tbFrancie.ChemWeaponLevel} 💚{tbKimiko.RegenLevel} 🧠{tbMM.UpgradeLevel}(📋{tbMM.KompromatTargets.Count})";
                        if (tbButcher.SuperDickActive) customString += " | 💀СуперМудень";
                        if (tbFrancie.OrderTarget != Guid.Empty)
                        {
                            var orderName = game.PlayersList.Find(x => x.GetPlayerId() == tbFrancie.OrderTarget)?.DiscordUsername ?? "?";
                            customString += $" | 🎯{orderName}({tbFrancie.OrderRoundsLeft})";
                        }
                        if (tbKimiko.LivingWeapon) customString += " | ⚔️ЖивоеОружие";
                        else if (tbKimiko.IsDisabled) customString += " | ❌Kimiko";
                        if (tbMM.IsCalm) customString += " | 🧘Спокоен";
                    }
                    else if (other.Passives.TheBoysSupMark)
                    {
                        customString += " 🦸";
                    }
                    break;

                case "Шэн":
                    if (other.GetPlayerId() == me.GetPlayerId())
                    {
                        var salShen = me.Passives.SalldorumShen;
                        customString += $" ⚡{salShen.Charges}";
                    }
                    break;
            }


        // Reciprocal alliance annotations are admin-only; owner-side marks are rendered in the switch above.
        if (me.PlayerType == 2 && other.GameCharacter.Passive.Any(p => p.PassiveName == "Вступить в союз")
            && other.Passives.NapoleonAlliance.AllyId == me.GetPlayerId())
            customString += " 🤝";

        // Admin sees ⚔️ on the player that Napoleon's ally is currently targeting
        if (me.PlayerType == 2 && other.GameCharacter.Passive.Any(p => p.PassiveName == "Вступить в союз") is false)
        {
            var napOther = game.PlayersList.Find(x =>
                x.GameCharacter.Passive.Any(p => p.PassiveName == "Вступить в союз")
                && x.Passives.NapoleonAlliance.AllyId == me.GetPlayerId());
            if (napOther != null && napOther.Status.WhoToAttackThisTurn.Contains(other.GetPlayerId()))
                customString += " ⚔️";
        }

        // Reciprocal Support marker is likewise admin-only
        if (me.PlayerType == 2 && other.GameCharacter.Passive.Any(p => p.PassiveName == "Premade")
            && other.Passives.SupportPremade.MarkedPlayerId == me.GetPlayerId())
            customString += " 🤝";

        // Saitama sees top 1 player as "King" (m22: character Name is Cyrillic "Сайтама")
        if (me.GameCharacter.Name == "Сайтама" && other.Status.GetPlaceAtLeaderBoard() == 1
            && other.GetPlayerId() != me.GetPlayerId())
            customString += " 👑 King";

        var knownClass = me.Status.KnownPlayerClass.Find(x => x.EnemyId == other.GetPlayerId());

        //if (knownClass != null && me.GameCharacter.Name != "AWDKA")
        if (knownClass != null)
            customString += $" {knownClass.Text}";

       
        foreach (var passive in me.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "AdminPlayerType":
                    if (other.GetPlayerId() == me.GetPlayerId()) break;
                    //customString += $"\n**IN: {other.GameCharacter.GetIntelligence()}** ST: {other.GameCharacter.GetStrength()} **SP: {other.GameCharacter.GetSpeed()}** PS: {other.GameCharacter.GetPsyche()} | **JS: {other.GameCharacter.Justice.GetRealJusticeNow()}** MR: {other.GameCharacter.GetMoral()} **SK: {other.GameCharacter.GetSkill()}**"; //| TG: {other.GameCharacter.GetCurrentSkillClassTarget()}
                    //var step1 = _calculateRounds.CalculateStep1(me, other);
                    //var r2 = _calculateRounds.CalculateStep2(me, other);
                    //var (r3, _, _) = _calculateRounds.CalculateStep3(me, other, step1.RandomForPoint, step1.NemesisMultiplier);
                    //customString += $"\nDoomsday: {step1.PointsWon} | {r2} | {r3}~";
                    break;

                case "Ведьмачьи заказы":
                    if (me.GameCharacter.Name == "Геральт")
                    {
                        var geraltLbContracts = me.Passives.GeraltContracts;
                        if (other.GetPlayerId() != me.GetPlayerId() && other.Passives.GeraltMonsterType != null)
                        {
                            var mType = other.Passives.GeraltMonsterType.Value;
                            var typeName = Geralt.GetMonsterTypeName(mType);
                            var color = Geralt.GetMonsterColor(mType);
                            var count = geraltLbContracts.GetCount(mType);
                            customString += $" {{color:{color}}}{typeName} x{count}{{/color}}";
                        }
                        if (other.GetPlayerId() == me.GetPlayerId())
                        {
                            var totalContracts = geraltLbContracts.Drowners + geraltLbContracts.Werewolves
                                + geraltLbContracts.Vampires + geraltLbContracts.Dragons;
                            //customString += $" ⚔️Контракты: {totalContracts}";
                        }
                    }
                    break;
            }


        //predict — web handles these natively (character unmask + prediction UI)
        if (!isWeb)
        {
            if (game.RoundNo >= 11 && !game.IsKratosEvent)
                customString += $" (as **{VisibleCharacterName(me, other)}**) = {other.Status.GetScore()} Score";

            var predicted = Sakura.Is(other)
                ? null
                : me.Predict.Find(x => x.PlayerId == other.GetPlayerId() && !Sakura.Is(x.CharacterName));
            if (predicted != null)
            {
                var predictedName = UnknownBug.Is(predicted.CharacterName) ? "???" : predicted.CharacterName;
                customString += $"<:e_:562879579694301184>|<:e_:562879579694301184>{predictedName} ?";
            }
        }
        //end predict

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
        var hiddenPassiveNames = game.PlayersList
            .Where(other => other.GetPlayerId() != player.GetPlayerId())
            .SelectMany(other => other.GameCharacter.Passive)
            .Where(passive =>
                passive.PassiveName != "Запах мусора" && passive.PassiveName != "Чернильная завеса" &&
                passive.PassiveName != "Еврей" && passive.PassiveName != "2kxaoc")
            .Select(passive => passive.PassiveName)
            .ToHashSet(StringComparer.Ordinal);
        var text = hiddenPassiveNames.Aggregate(textOriginal, (current, passiveName) =>
            current.Replace(passiveName,
                player.PlayerType == 0 ? "Неизвестно" : $"❓ {passiveName}",
                StringComparison.Ordinal));
        text = PhrasePayload.MaskPassiveNames(text, hiddenPassiveNames, player.PlayerType == 0);
        text = PhrasePayload.Resolve(text, GameLocalization.GetUserLanguage(player.DiscordId));

        var separationLine = false;
        var orderedList = new List<string>
        {
            "Вы улучшили", "|>PhraseBeforeFight<|", "Обмен Морали", "Вы использовали Авто Ход", "Вы напали на", "Вы поставили блок",  
            "дополнительного вреда", "TOO GOOD", "TOO STONK", "|>Stat<|", "|>Phrase<|", "|>SeparationLine<|", "Поражение:", "Получено вреда:", "Победа:", "Контракт:", "Лут:", "Благодарность:", "Читы",
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
                                // TryParse: glued log lines (M48) can put non-numeric text into these segments
                                if (moral.Contains("Морали") &&
                                    int.TryParse(moral.Replace("Морали", "").Replace(" ", ""), out var moralDelta))
                                    totalMoral += moralDelta;

                                if (moral.Contains("Cкилла") &&
                                    int.TryParse(moral.Replace("Cкилла", "").Replace(" ", ""), out var skillDelta))
                                    totalSkill += skillDelta;
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
                case "|>Stat<|" when text.Contains(keyword):
                {
                    var jewSplit = text.Split('\n');
                    var temp = jewSplit.Where(line => !line.Contains(keyword)).Aggregate("", (current, line) => current + line.Replace(keyword, "") + "\n");
                    text = jewSplit.Where(line => line.Contains(keyword)).Aggregate(temp, (current, line) => current + line.Replace(keyword, "") + "\n");

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


        var globalLogs = game!.GetGlobalLogs();
        if (game.RoundNo >= 11 && Madara.IsEternalTsukuyomiActive(game)
            && !Madara.IsMadara(player)
            && !GordonFreeman.SeesEternalTsukuyomiReality(player, game))
            globalLogs = Madara.GetProjectedFinalLogs(game, player);
        // Hide fight logs from non-admin players
        if (player.PlayerType != 2)
            foreach (var snippet in game.HiddenGlobalLogSnippets)
                globalLogs = globalLogs.Replace(snippet, "");
        var desc = HandleIsNewPlayerDescription(globalLogs, player, game);

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
        else if (character.Name == ErenYeager.CharacterName)
        {
            intStr = "Злость";
            psyStr = "Самоуверенность";
        }

        var splitter = "▬▬▬▬▬▬▬▬▬▬▬▬▬";
        /*
        var skillExtraText = "";
        var targetExtraText = "";
        if (player.GameCharacter.GetExtraSkillMultiplier() > 0) skillExtraText = $" (Множитель: **x{player.GameCharacter.GetExtraSkillMultiplier() + 1}**)";
        if (player.GameCharacter.GetTargetSkillMultiplier() > 0) targetExtraText = $" (Множитель: **x{player.GameCharacter.GetTargetSkillMultiplier() + 1}**)";
        */

        var isMadara = Madara.IsMadara(player);
        var statLines = UnknownBug.Is(character)
            ? "```cs\nName: unknown_bug\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\n```\n"
            : isMadara
            ? $"**{intStr}:** {character.GetIntelligenceString()}\n"
              + $"**{strStr}:** {character.GetStrengthString()}\n"
              + $"**{speStr}:** {character.GetSpeedString()}\n"
              + $"**{psyStr}:** {character.GetPsycheString()}\n"
            : $"**{intStr}:** {character.GetIntelligenceString()}{character.GetIntelligenceQualityResist()}\n"
              + $"**{strStr}:** {character.GetStrengthString()}{character.GetStrengthQualityResist()}\n"
              + $"**{speStr}:** {character.GetSpeedString()}{character.GetSpeedQualityResist()}\n"
              + $"**{psyStr}:** {character.GetPsycheString()}{character.GetPsycheQualityResist()}\n";
        var resourceLines = UnknownBug.Is(character)
            ? $"```cs\n// runtime resources\njustice = {character.Justice.GetRealJusticeNow()};\n" +
              $"moral = {character.GetMoral()};\nskill = {character.GetSkillDisplay()};\n```\n"
            : isMadara
            ? $"*Справедливость: **{character.Justice.GetRealJusticeNow()}***\n"
              + $"*Класс:* {character.GetClassStatDisplayText()}\n"
            : $"*Справедливость: **{character.Justice.GetRealJusticeNow()}***\n"
              + $"*Мораль: {character.GetMoralString()}*\n"
              + $"*Скилл: {character.GetSkillDisplay()} (Мишень: **{character.GetCurrentSkillClassTarget()}**)*\n"
              + $"*Класс:* {character.GetClassStatDisplayText()}\n";

        embed.WithDescription($"{desc}" +
                              $"**{splitter}**\n" +
                              statLines +
                              $"**{splitter}**\n" +
                              resourceLines +
                              $"**{splitter}**\n" +
                              $"Множитель очков: **x{multiplier}**\n" +
                              "<:e_:562879579694301184>\n" +
                              $"{LeaderBoard(player)}");


        var splitLogs = player.Status.InGamePersonalLogsAll.Split("|||");

        string text;
        if (splitLogs.Length > 1 && splitLogs[^2].Length > 3 && game.RoundNo > 1)
        {
            text = splitLogs[^2];
            text = SortLogs(text, player, game);
            if (text.Length < 1024)
            {
                embed.AddField("События прошлого раунда:", $"{text}");
            }
            else
            {
                // whitespace-only chunks are rejected by Discord (50035 BASE_TYPE_REQUIRED, M49)
                var textSplit = _helperFunctions.Split(text, 1020)
                    .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                for (var i = 0; i < textSplit.Count; i++)
                {
                    var t = textSplit[i];
                    embed.AddField(i == 0 ? "События прошлого раунда:" : "_", $"{t}");
                }
            }
        }
        else
        {
            embed.AddField("События прошлого раунда:", "В прошлом раунде ничего не произошло. Странно...");
        }

        text = player.Status.GetInGamePersonalLogs().Length >= 2
            ? $"{player.Status.GetInGamePersonalLogs()}"
            : "Еще ничего не произошло. Наверное...";
        text = SortLogs(text, player, game);


        if (text.Length < 1024)
        {
            embed.AddField("События этого раунда:", text);
        }
        else
        {
            // whitespace-only chunks are rejected by Discord (50035 BASE_TYPE_REQUIRED, M49)
            var textSplit = _helperFunctions.Split(text, 1020)
                .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            for (var i = 0; i < textSplit.Count; i++)
            {
                var t = textSplit[i];
                embed.AddField(i == 0 ? "События этого раунда:" : "_", $"{t}");
            }
        }
        

        if(!player.IsMobile)
            embed.WithThumbnailUrl(character.AvatarCurrent);
        
        //embed.WithImageUrl(character.AvatarCurrent);

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

        // Goblin-specific level-up page
        if (player.GameCharacter.Name == "Стая Гоблинов")
        {
            var pop = player.Passives.GoblinPopulation;
            var text = "__Выберите улучшение для Стаи:__";
            embed.WithColor(Color.DarkGreen);
            embed.WithFooter($"{GetTimeLeft(player)}");
            embed.AddField("_____",
                $"{text}\n \n" +
                $"1. **Правильное питание:** Больше Хобгоблинов (сейчас каждый {pop.HobRate}й)\n" +
                $"2. **Контрактная армия:** Больше Воинов (сейчас каждый {pop.WarriorRate}й)\n" +
                $"3. **Трудовые условия:** Больше Трудяг (сейчас каждый {pop.WorkerRate}й)\n" +
                $"4. **Праздник Гоблинов:** Удвоить гоблинов ({pop.TotalGoblins} → {pop.TotalGoblins * 2}){(pop.FestivalUsed ? " *(уже использовано)*" : "")}\n");
            embed.WithThumbnailUrl(character.AvatarCurrent);
            return embed;
        }

        var text2 = "__Подними один из статов на 1:__";
        // m3: a transformed Молодой Глеб keeps Name == "Глеб" but carries the "Main Ирелия" passive
        // (the same marker the level-up nerf uses) — key the caption off it so it matches the effect.
        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия")) text2 = "**Понизить** один из статов на 1!";
        embed.WithColor(Color.Blue);
        embed.WithFooter($"{GetTimeLeft(player)}");
        //embed.WithCurrentTimestamp();
        var intelligenceName = character.Name == ErenYeager.CharacterName ? "Злость" : "Интеллект";
        var psycheName = character.Name == ErenYeager.CharacterName ? "Самоуверенность" : "Психика";
        var levelUpStats = UnknownBug.Is(character)
            ? "```cs\n1. ERR: cant_get_stat\n2. ERR: cant_get_stat\n3. ERR: cant_get_stat\n4. ERR: cant_get_stat\n```"
            : $"1. **{intelligenceName}:** {character.GetIntelligence()}\n" +
              $"2. **Сила:** {character.GetStrength()}\n" +
              $"3. **Скорость:** {character.GetSpeed()}\n" +
              $"4. **{psycheName}:** {character.GetPsyche()}\n";
        embed.AddField("_____", $"{text2}\n \n{levelUpStats}");


        embed.WithThumbnailUrl(character.AvatarCurrent);

        return embed;
    }

    private static string VisibleCharacterName(GamePlayerBridgeClass viewer, GamePlayerBridgeClass subject)
    {
        return UnknownBug.Is(subject) && viewer?.GetPlayerId() != subject.GetPlayerId()
            ? "???"
            : subject.GameCharacter.Name;
    }


    //Page 4 - Debug

    
    //Page 5 - Aram Choice
    public EmbedBuilder AramPickPage(GamePlayerBridgeClass player)
    {
        var character = player.GameCharacter;
        var embed = new EmbedBuilder();
        embed.WithColor(Color.DarkGreen);
        embed.WithTitle("ARAM Pick Stage");
        embed.WithFooter($"Available Re-Rolls {(player.Status.AramRerolledPassivesTimes - 4) * -1}");

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
            if (passive.PassiveName == Madara.EternalTsukuyomi) continue;
            embed.AddField($"{i+1}. {passive.PassiveName}", passive.PassiveDescription);
        }


        embed.WithThumbnailUrl(character.AvatarCurrent);

        return embed;
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
            if (playerToAttack.DiscordId != player.DiscordId && !playerToAttack.Passives.IsDead
                && !Madara.IsSealed(playerToAttack)
                && !Naruto.IsNarutoPair(player, playerToAttack))
                attackMenu.AddOption("Напасть на " + playerToAttack.DiscordUsername, playerToAttack.GetPlayerId().ToString(), emote: _playerChoiceAttackList[i]);
        }

        if (attackMenu.Options.Count == 0) attackMenu.AddOption("ТЫ ВСЕХ УБИЛ", "kratos-death");

        return attackMenu;
    }


    public ButtonBuilder GetMobileButton()
    {
        return new ButtonBuilder("Mobile Device", "mobile-device", ButtonStyle.Primary, isDisabled: false);
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
            if (Sakura.Is(playerToAttack)) continue;
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
        if (player.GameCharacter.DoomRollMode)
        {
            predictMenu.WithDisabled(true);
            predictMenu.WithPlaceholder("Let's Roll!");
        }
        
        return predictMenu;
    }


    public async Task<SelectMenuBuilder> GetLvlUpMenu(GamePlayerBridgeClass player, GameClass game)
    {
        if (player.GameCharacter.Name == DoomGuy.CharacterName)
        {
            var stage = DoomGuy.StageForRound(game.RoundNo);
            var modules = DoomGuy.GetOptions(player.Passives.DoomGuy, stage);
            var doomMenu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId("lvl-up")
                .WithPlaceholder($"{stage}: выбрать модуль");
            for (var i = 0; i < modules.Count; i++)
                doomMenu.AddOption(modules[i].Name, (i + 1).ToString(),
                    modules[i].Description.Length > 90 ? modules[i].Description[..90] : modules[i].Description);
            if (modules.Count == 0)
            {
                // A select menu without options is rejected by Discord (50035 BASE_TYPE_REQUIRED, M49).
                // A module point held outside its stage round stays banked until the next module round.
                doomMenu.AddOption("Нет доступных модулей", "0");
                doomMenu.WithDisabled(true);
            }
            return doomMenu;
        }

        var placeholderText = "Выбор прокачки";
        if (player.GameCharacter.Name == "Вампур")
            placeholderText = _vampyrGarlic[_random.Random(0, _vampyrGarlic.Count - 1)];

        if (player.GameCharacter.Passive.Any(x => x.PassiveName == "Main Ирелия"))
        {
            placeholderText = "Выбор нерфа";
        }

        var intelligenceName = player.GameCharacter.Name == ErenYeager.CharacterName ? "Злость" : "Интеллект";
        var psycheName = player.GameCharacter.Name == ErenYeager.CharacterName ? "Самоуверенность" : "Психика";
        var charMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("lvl-up")
            .WithPlaceholder(placeholderText)
            .AddOption(intelligenceName, "1")
            .AddOption("Сила", "2")
            .AddOption("Скорость", "3")
            .AddOption(psycheName, "4");


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
        if (player.GameCharacter.DoomRollMode)
            return new ButtonBuilder("Let's Roll!: Мораль отключена", "moral", ButtonStyle.Secondary, isDisabled: true);
        var disabled = game is not { RoundNo: <= 10 };
        if (game.IsKratosEvent)
            disabled = false;
        var extraText = "";
        if (game.RoundNo == 10) extraText = " (Конец игры)";

        //if (player.GameCharacter.Name == "Братишка")
        //    return new ButtonBuilder($"Буууууууль", "moral", ButtonStyle.Secondary, isDisabled: true);
        if (player.GameCharacter.Name == "DeepList")
            return new ButtonBuilder("Интересует только скилл", "moral", ButtonStyle.Secondary, isDisabled: true);
        if (player.Passives.TheBoysMoralBlockedByMM)
            return new ButtonBuilder("Компромат М.М.: мораль заблокирована", "moral", ButtonStyle.Secondary, isDisabled: true);

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
        if (player.GameCharacter.DoomRollMode)
            return new ButtonBuilder("Let's Roll!: Мораль отключена", "skill", ButtonStyle.Secondary, isDisabled: true);
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
        if (player.Passives.TheBoysMoralBlockedByMM)
            return new ButtonBuilder("Компромат М.М.: мораль заблокирована", "skill", ButtonStyle.Secondary, isDisabled: true, emote: Emote.Parse("<a:bratishka:900962522276958298>"));

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

        if (game.IsRoundTransitionPaused)
        {
            var halfLife = player.Passives.Gordon.HalfLife;
            if (GordonFreeman.Is(player) && halfLife.PendingDecision)
            {
                components.WithButton(new ButtonBuilder(
                    GordonFreeman.GetFreezeLabel(player.Passives.Gordon),
                    $"gordon-hl3-freeze:{halfLife.DecisionSerial}", ButtonStyle.Danger));
                components.WithButton(new ButtonBuilder(
                    GordonFreeman.GetPostponeLabel(player.Passives.Gordon),
                    $"gordon-hl3-postpone:{halfLife.DecisionSerial}", ButtonStyle.Primary));
            }
            else
            {
                components.WithButton(new ButtonBuilder(
                    "Ожидаем решение по Halflife 3", "gordon-hl3-wait",
                    ButtonStyle.Secondary, isDisabled: true));
            }

            return components;
        }

        components.WithButton(GetBlockButton(player, game));

        if (GordonFreeman.CanWake(player, game))
            components.WithButton(new ButtonBuilder(
                "Проснуться", "gordon-wake", ButtonStyle.Primary));

        if (game.GameMode != "Aram" && player.GameCharacter.Tier > 3)
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

        if (!Madara.IsMadara(player))
            components.WithButton(GetMoralToSkillButton(player, game), 2);

        if (!Madara.IsMadara(player) && player.GameCharacter.GetMoral() >= 3)
            if (player.Status.ConfirmedPredict && player.Status.ConfirmedSkip)
                components.WithButton(GetMoralToPointsButton(player, game), 2);

        if (game.GameMode != "Aram" && !player.GameCharacter.DoomRollMode && !Madara.IsMadara(player))
        {
            if (player.GameCharacter.Passive.All(x => x.PassiveName != "AdminPlayerType"))
            {
                components.WithSelectMenu(predictMenu ?? GetPredictMenu(player, game), 3);
            }
        }


        foreach (var passive in player.GameCharacter.Passive)
            switch (passive.PassiveName)
            {
                case "Мне (не)везет":
                    var darksciType = player.Passives.DarksciTypeList;
                    if (game.RoundNo == 1 && !darksciType.Triggered)
                    {
                        components.WithButton(new ButtonBuilder("Мне никогда не везёт...", "stable-Darksci"), 4);
                        components.WithButton(new ButtonBuilder("Мне сегодня повезёт!", "not-stable-Darksci", ButtonStyle.Danger), 4);
                        if (!darksciType.Sent)
                        {
                            darksciType.Sent = true;
                            await _helperFunctions.SendMsgAndDeleteItAfterRound(player, "Нажмешь синюю кнопку - и сказке конец. Выберешь красную - и узнаешь насколько глубока нора Даркси.", 0);
                        }
                    }
                    break;

                case "Yong Gleb":
                    if (game.RoundNo == 1 && player.GameCharacter.Name != "Молодой Глеб")
                    {
                        components.WithButton(new ButtonBuilder("Вспомнить Молодость", "yong-gleb"), 4);
                    }
                    break;

                case "Rune":
                    if (player.GameCharacter.Name == DoomGuy.CharacterName && game.RoundNo == 1
                        && !player.Passives.DoomGuy.RollMode)
                        components.WithButton(new ButtonBuilder("Let's Roll!", "doom-roll", ButtonStyle.Danger), 4);
                    break;
            }

        if (player.GameCharacter.Name == DoomGuy.CharacterName
            && player.Passives.DoomGuy.ChainsawChoices.Count > 0)
        {
            var sawMenu = new SelectMenuBuilder()
                .WithMinValues(1).WithMaxValues(1)
                .WithCustomId("doom-chainsaw")
                .WithPlaceholder("Бензопила: выбрать пассивку");
            foreach (var choice in player.Passives.DoomGuy.ChainsawChoices)
                sawMenu.AddOption(choice.PassiveName, choice.PassiveName,
                    choice.PassiveDescription.Length > 90 ? choice.PassiveDescription[..90] : choice.PassiveDescription);
            components.WithSelectMenu(sawMenu, 4);
        }

        if (game.RoundNo == 1 && !player.IsMobile && player.Passives.DoomGuy.ChainsawChoices.Count == 0)
        {
            components.WithButton(GetMobileButton(), 4);
        }
        
        return components;
    }

    public ComponentBuilder GetAramPickButtons(GamePlayerBridgeClass player, GameClass game)
    {
        var components = new ComponentBuilder();

        if (!player.Status.IsAramRollConfirmed)
        {
            var isDisabled = player.Status.AramRerolledPassivesTimes >= 4;
            var isStatsDisabled = player.Status.AramRerolledStatsTimes >= 1;

            components.WithButton(new ButtonBuilder("Reroll #1", "aram_reroll_1", ButtonStyle.Secondary, isDisabled: isDisabled));
            components.WithButton(new ButtonBuilder("Reroll #2", "aram_reroll_2", ButtonStyle.Secondary, isDisabled: isDisabled));
            components.WithButton(new ButtonBuilder("Reroll #3", "aram_reroll_3", ButtonStyle.Secondary, isDisabled: isDisabled));
            components.WithButton(new ButtonBuilder("Reroll #4", "aram_reroll_4", ButtonStyle.Secondary, isDisabled: isDisabled));
            components.WithButton(new ButtonBuilder("Reroll Stats", "aram_reroll_5", ButtonStyle.Secondary, isDisabled: isStatsDisabled), row:1);
            components.WithButton(new ButtonBuilder("Confirm", "aram_roll_confirm", ButtonStyle.Success, isDisabled: false), row:2);
            components.WithButton(GetEndGameButton(player, game), row: 2);
        }
        else
        {
            components.WithButton(new ButtonBuilder("Wait for other players", "aram_roll_confirm", ButtonStyle.Success, isDisabled: true));
            components.WithButton(GetEndGameButton(player, game));
        }

        return components;
    }


    public EmbedBuilder DraftPickPage(GamePlayerBridgeClass player, GameClass game)
    {
        var embed = new EmbedBuilder();
        embed.WithColor(Color.Gold);
        embed.WithTitle("Draft Pick — Choose Your Character");
        embed.WithFooter("Draft Pick Phase");

        if (player.Status.IsDraftPickConfirmed)
        {
            if (UnknownBug.Is(player))
            {
                embed.WithColor(new Color(0, 255, 65));
                embed.WithTitle("runtime://draft/locked");
                embed.WithDescription(
                    "```cs\nName: unknown_bug\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\nERR: cant_get_stat\n```\n" +
                    "```txt\nselection_override=true; // waiting for other processes\n```");
                return embed;
            }

            embed.WithDescription($"**Ты выбрал: {player.GameCharacter.Name}**\nОжидаем остальных игроков...");
            embed.WithThumbnailUrl(player.GameCharacter.AvatarCurrent);
            return embed;
        }

        if (!game.DraftOptions.TryGetValue(player.GetPlayerId(), out var options))
        {
            embed.WithDescription("Нет доступных персонажей для выбора.");
            return embed;
        }

        var visibleOptions = options
            .Select((character, index) => (Character: character, OriginalIndex: index))
            .Where(option => !UnknownBug.Is(option.Character))
            .ToList();
        if (visibleOptions.Count == 0)
        {
            embed.WithDescription("Нет доступных персонажей для выбора.");
            return embed;
        }

        embed.WithDescription("Выбери одного из доступных персонажей:");

        for (var i = 0; i < visibleOptions.Count; i++)
        {
            var c = visibleOptions[i].Character;
            var costLabel = visibleOptions[i].OriginalIndex == 0 ? "FREE" : "cost 5 ZBS points";
            var passiveNames = string.Join(", ", c.Passive.Where(p => p.Visible).Select(p => p.PassiveName));
            if (string.IsNullOrEmpty(passiveNames)) passiveNames = "—";
            embed.AddField(
                $"{i + 1}. {c.Name} (Tier {c.Tier}) [{costLabel}]",
                $"INT: {c.GetIntelligence()} | STR: {c.GetStrength()} | SPD: {c.GetSpeed()} | PSY: {c.GetPsyche()}\n" +
                $"Passives: {passiveNames}");
        }

        return embed;
    }

    public ComponentBuilder GetDraftPickButtons(GamePlayerBridgeClass player, GameClass game)
    {
        var components = new ComponentBuilder();

        if (!player.Status.IsDraftPickConfirmed)
        {
            if (game.DraftOptions.TryGetValue(player.GetPlayerId(), out var options))
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (UnknownBug.Is(options[i])) continue;
                    var label = i == 0
                        ? $"{options[i].Name} (FREE)"
                        : $"{options[i].Name} (cost 5 ZBS points)";
                    components.WithButton(new ButtonBuilder(label, $"draft_pick_{i}", ButtonStyle.Primary));
                }
            }
            components.WithButton(GetEndGameButton(player, game), row: 1);
        }
        else
        {
            components.WithButton(new ButtonBuilder("Ожидаем остальных", "draft_pick_wait", ButtonStyle.Success, isDisabled: true));
            components.WithButton(GetEndGameButton(player, game));
        }

        return components;
    }

    public ButtonBuilder GetBlockButton(GamePlayerBridgeClass player, GameClass game)
    {
        if (GordonFreeman.Is(player))
            return new ButtonBuilder(
                "Halflife 3",
                "gordon-hl3-announce",
                ButtonStyle.Success,
                isDisabled: player.Status.IsReady
                            || !GordonFreeman.CanAnnounceHalfLife3(player, game));

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
        var disabled = player.Status.IsAutoMove || player.Status.IsSkip || player.Status.IsReady;

        if (game.TimePassed.Elapsed.TotalSeconds < 29 && player.DiscordId != 238337696316129280 &&
            player.DiscordId != 181514288278536193) disabled = true;

        return new ButtonBuilder("Авто Ход", "auto-move", ButtonStyle.Secondary, isDisabled: disabled);
    }

    public async Task UpdateMessage(GamePlayerBridgeClass player, string extraText = "")
    {
        if (player.IsBot() || player.IsWebPlayer || player.PreferWeb)
        {
            // Still deliver extraText to web messages even when Discord is suppressed
            if (!player.IsBot() && extraText.Length > 0)
                player.WebMessages.Add(extraText);
            return;
        }

        var game = _global.GamesList.Find(x => x.GameId == player.GameId);
        if (game == null) return;
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
                //embed = DebugPage(player);
                //builder = await GetGameButtons(player, game);
                break;

            //aram pick
            case 5:
                embed = AramPickPage(player);
                builder = GetAramPickButtons(player, game);
                break;

            //draft pick
            case 6:
                embed = DraftPickPage(player, game);
                builder = GetDraftPickButtons(player, game);
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
