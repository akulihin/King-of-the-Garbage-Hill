using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using King_of_the_Garbage_Hill.Game.GameGlobalVariables;
using King_of_the_Garbage_Hill.Game.MemoryStorage;
using King_of_the_Garbage_Hill.Helpers;
using King_of_the_Garbage_Hill.LocalPersistentData.UsersAccounts;

namespace King_of_the_Garbage_Hill.Game.ReactionHandling;

public class TutorialReactions : IServiceSingleton
{
    private readonly CharactersPull _charactersPull;
    private readonly InGameGlobal _gameGlobal;

    private readonly List<Emoji> _playerChoiceAttackList = new()
        { new Emoji("1⃣"), new Emoji("2⃣"), new Emoji("3⃣"), new Emoji("4⃣"), new Emoji("5⃣"), new Emoji("6⃣") };

    private readonly SecureRandom _random;
    private readonly UserAccounts _userAccounts;

    public TutorialReactions(UserAccounts userAccounts, CharactersPull charactersPull, SecureRandom random,
        InGameGlobal gameGlobal)
    {
        _userAccounts = userAccounts;
        _charactersPull = charactersPull;
        _random = random;
        _gameGlobal = gameGlobal;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    public EmbedFieldBuilder GetLeaderBoardTutorial(TutorialGame game)
    {
        var embedField = new EmbedFieldBuilder();
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        var text = "";

        if (game.RoundNumber >= 5 && game.RoundNumber < 10)
        {
            text += "Множитель очков: **x2**\n";
            text += "<:e_:562879579694301184>\n";
        }

        if (game.RoundNumber == 10)
        {
            text += "Множитель очков: **x4**\n";
            text += "<:e_:562879579694301184>\n";
        }

        var title = "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";
        var index = 0;
        if (game.RoundNumber == 4)
        {
            var temp4 = game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 4);
            var temp5 = game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 5);
            player.PlaceAtLeaderBoard = 4;
            temp4.PlaceAtLeaderBoard = 5;
            temp5.PlaceAtLeaderBoard = 6;
        }

        if (game.RoundNumber == 11)
        {
            var temp4 = game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 1);
            player.PlaceAtLeaderBoard = 1;
            temp4.PlaceAtLeaderBoard = 2;
        }

        if (game.RoundNumber == 6)
        {
            var temp4 = game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 2);
            player.PlaceAtLeaderBoard = 2;
            temp4.PlaceAtLeaderBoard = 4;

            game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 1).ClassString = "(**Быстрый** ?)";
            game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 3).ClassString = "(**Умный** ?)";
            game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 4).ClassString = "(**Сильный** ?)";
            game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 5).ClassString = "(**Быстрый** ?)";
            game.PlayersList.Find(x => x.PlaceAtLeaderBoard == 6).ClassString = "(**Сильный** ?)";
        }


        foreach (var p in game.PlayersList.OrderBy(x => x.PlaceAtLeaderBoard))
        {
            index++;

            if (p.PlayerId == game.DiscordPlayerId)
                text += $"{index}. {p.DiscordUsername} = **{p.Score} Score**\n";
            else if (player.Predicted && p.DiscordUsername == "MegaVova99")
                text += $"{index}. {p.DiscordUsername} {p.ClassString} | AWDKA ?\n";
            else
                text += $"{index}. {p.DiscordUsername} {p.ClassString}\n";
        }

        embedField.WithName(title);
        embedField.WithValue(text);

        return embedField;
    }


    public EmbedFieldBuilder GetSecondaryStatsBoardTutorial(TutorialGame game)
    {
        var embedField = new EmbedFieldBuilder();
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        var text = "";
        var title = "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";


        if (game.RoundNumber > 0) text += $"*Справедливость: {player.Justice}*\n";
        if (game.RoundNumber > 2) text += $"*Мораль: {player.Moral}*\n";

        if (game.RoundNumber == 7) text += $"*Скилл: {player.Skill} (Мишень: **Умный**)*\n";
        if (game.RoundNumber >= 8) text += $"*Скилл: {player.Skill}*\n";

        if (game.RoundNumber > 5)
        {
            var classText = "";
            if (player.Intelligence >= player.Strength && player.Intelligence >= player.Speed)
                player.ClassString = "**Умный**";
            else if (player.Strength >= player.Intelligence && player.Strength >= player.Speed)
                player.ClassString = "**Сильный**";
            else if (player.Speed >= player.Intelligence && player.Speed >= player.Strength)
                player.ClassString = "**Быстрый**";

            text += $"*Класс: {player.ClassString}*\n";
        }


        embedField.WithName(title);
        embedField.WithValue(text);

        return embedField;
    }

    public EmbedFieldBuilder GetStatsBoardTutorial(TutorialGame game)
    {
        var embedField = new EmbedFieldBuilder();
        var text = "";
        var title = "▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬";
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        if (game.RoundNumber > 0)
            text += $"**Интеллект:** {player.Intelligence}\n" +
                    $"**Сила:** {player.Strength}\n" +
                    $"**Скорость:** {player.Speed}\n" +
                    $"**Психика:** {player.Psyche}\n";


        embedField.WithName(title);
        embedField.WithValue(text);

        return embedField;
    }


    public EmbedBuilder LvlUpPageTutorial(TutorialGame game)
    {
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);


        var embed = new EmbedBuilder();


        embed.WithColor(Color.Blue);

        embed.WithCurrentTimestamp();
        embed.AddField("_____",
            "__Подними один из статов на 1:__\n \n" +
            $"1. **Интеллект:** {player.Intelligence}\n" +
            $"2. **Сила:** {player.Strength}\n" +
            $"3. **Скорость:** {player.Speed}\n" +
            $"4. **Психика:** {player.Psyche}\n");

        return embed;
    }


    public SelectMenuBuilder GetPredictMenuTutorial(TutorialGame game, bool isDisabled = false)
    {
        var predictMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("predict-tutorial-1")
            .WithDisabled(isDisabled)
            .WithPlaceholder("Сделать предположение");

        predictMenu.AddOption("MegaVova99 " + " это...", "MegaVova99", emote: _playerChoiceAttackList[0]);

        return predictMenu;
    }

    public SelectMenuBuilder GetPredict2MenuTutorial(TutorialGame game, bool isDisabled = false)
    {
        var predictMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("predict-tutorial-2")
            .WithDisabled(isDisabled)
            .WithPlaceholder("MegaVova99 это...");

        predictMenu.AddOption("AWDKA", "AWDKA");

        return predictMenu;
    }


    public SelectMenuBuilder GetAttackMenuTutorial(TutorialGame game, bool isDisabled = false)
    {
        var placeHolder = "Выбрать цель";


        var attackMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("attack-select-tutorial")
            .WithDisabled(isDisabled)
            .WithPlaceholder(placeHolder);


        if (game != null)
            for (var i = 0; i < game.PlayersList.Count; i++)
            {
                var playerToAttack = game.PlayersList.Find(x => x.PlaceAtLeaderBoard == i + 1);
                if (playerToAttack == null) continue;
                if (playerToAttack.PlayerId != game.DiscordPlayerId)
                    attackMenu.AddOption("Напасть на " + playerToAttack.DiscordUsername, $"{i + 1}",
                        emote: _playerChoiceAttackList[i]);
            }

        return attackMenu;
    }

    public SelectMenuBuilder GetLvlUpMenuTutorial(TutorialGame game)
    {
        var charMenu = new SelectMenuBuilder()
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithCustomId("lvlup-select-tutorial")
            .WithPlaceholder("Выбор прокачки")
            .AddOption("Интеллект", "1")
            .AddOption("Сила", "2")
            .AddOption("Скорость", "3")
            .AddOption("Психика", "4");


        return charMenu;
    }

    public ButtonBuilder GetMoralButtonTutorial(TutorialGame game, bool isDisabled = false)
    {
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        var extraText = "";
        if (game.RoundNumber == 10) extraText = " (Конец игры)";

        if (player.Moral >= 15)
            return new ButtonBuilder($"Обменять 15 Морали на 10 бонусных очков{extraText}", "moral-tutorial",
                ButtonStyle.Secondary, isDisabled: isDisabled);
        if (player.Moral >= 10)
            return new ButtonBuilder($"Обменять 10 Морали на 6 бонусных очков{extraText}", "moral-tutorial",
                ButtonStyle.Secondary, isDisabled: isDisabled);
        if (player.Moral >= 5)
            return new ButtonBuilder($"Обменять 5 Морали на 2 бонусных очка{extraText}", "moral-tutorial",
                ButtonStyle.Secondary, isDisabled: isDisabled);
        if (player.Moral >= 3)
            return new ButtonBuilder($"Обменять 3 Морали на 1 бонусное очко{extraText}", "moral-tutorial",
                ButtonStyle.Secondary, isDisabled: isDisabled);
        return new ButtonBuilder("Недостаточно очков морали", "moral-tutorial", ButtonStyle.Secondary,
            isDisabled: true);
    }

    public ButtonBuilder GetBlockButtonTutorial(bool isDisabled = false)
    {
        return new("Блок", "block-tutorial", ButtonStyle.Success, isDisabled: isDisabled);
    }

    /*
        new(5, 5, "MegaVova99"),
        new(4, 4, "YasuoOnly"),
        new(3, 3, "PETYX"),
        new(2, 2, "Drone"),
        new(1, 1, "EloBoost")
*/
    public string GetDescriptionTutorial(TutorialGame game)
    {
        var text = $"__**Раунд #{game.RoundNumber}:**__\n";
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        var target = game.PlayersList.Find(x => x.PlayerId == player.WhoToAttackThisTurn);
        if (game.RoundNumber == 2)
        {
            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ {target.DiscordUsername}\n";
            text += "MegaVova99 <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text += "PETYX <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text += "EloBoost <:war:561287719838547981> Drone ⟶ EloBoost\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ YasuoOnly\n";
            text += "Drone <:war:561287719838547981> YasuoOnly ⟶ Drone\n";
        }

        if (game.RoundNumber == 3)
        {
            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}**\n";
            text += "MegaVova99 <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text +=
                $"PETYX <:war:561287719838547981> **{player.DiscordUsername}** ⟶ **{player.DiscordUsername}**\n";
            text += "EloBoost <:war:561287719838547981> Drone ⟶ EloBoost\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ YasuoOnly\n";
            text += "Drone <:war:561287719838547981> YasuoOnly ⟶ Drone\n";
        }

        if (game.RoundNumber == 4)
        {
            player.Score += 1;

            var target1 = game.PlayersList.Find(x => x.PlayerId == player.WhoToAttackThisTurn);
            var moral = player.PlaceAtLeaderBoard - target1.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral += moral;


            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}**\n";
            text += "MegaVova99 <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text += "PETYX <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text += "EloBoost <:war:561287719838547981> Drone ⟶ EloBoost\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ YasuoOnly\n";
            text += "Drone <:war:561287719838547981> YasuoOnly ⟶ Drone\n";
        }

        if (game.RoundNumber == 5)
        {
            text +=
                $"EloBoost <:war:561287719838547981> **{player.DiscordUsername}**⟶ *Бой не состоялся (Блок)...*\n";
            text +=
                $"Drone <:war:561287719838547981> **{player.DiscordUsername}** ⟶ *Бой не состоялся (Блок)...*\n";
            text +=
                $"MegaVova99 <:war:561287719838547981> **{player.DiscordUsername}** ⟶ *Бой не состоялся (Блок)...*\n";
            text += "PETYX <:war:561287719838547981> Drone ⟶ PETYX\n";
            text += "YasuoOnly <:war:561287719838547981> Drone ⟶ Drone\n";
        }

        if (game.RoundNumber == 6)
        {
            player.Score += 6;
            player.Justice = 0;


            var target1 = game.PlayersList.Find(x => x.PlayerId == player.WhoToAttackThisTurn);
            var moral = player.PlaceAtLeaderBoard - target1.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral += moral;

            target1 = game.PlayersList.Find(x => x.DiscordUsername == "PETYX");
            moral = player.PlaceAtLeaderBoard - target1.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral += moral;

            target1 = game.PlayersList.Find(x => x.DiscordUsername == "YasuoOnly");
            moral = player.PlaceAtLeaderBoard - target1.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral += moral;


            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}**\n";
            text +=
                $"PETYX <:war:561287719838547981> **{player.DiscordUsername}** ⟶ **{player.DiscordUsername}**\n";
            text +=
                $"YasuoOnly <:war:561287719838547981> **{player.DiscordUsername}** ⟶ **{player.DiscordUsername}**\n";
            text += "Drone <:war:561287719838547981> PETYX ⟶ Drone\n";
            text += "EloBoost <:war:561287719838547981> PETYX ⟶ EloBoost\n";
            text += "MegaVova99 <:war:561287719838547981> PETYX ⟶ MegaVova99\n";
        }

        if (game.RoundNumber == 7)
        {
            player.Justice = 2;
            if (player.ClassString.Contains("Умный") && target.ClassString.Contains("Быстрый"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
            }
            else if (player.ClassString.Contains("Быстрый") && target.ClassString.Contains("Сильный"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
            }
            else if (player.ClassString.Contains("Сильный") && target.ClassString.Contains("Умный"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
            }
            else
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ {target.DiscordUsername} \n";
                player.Justice += 1;
            }

            if (player.ClassString.Contains("Умный"))
            {
                text += $"Drone <:war:561287719838547981> **{player.DiscordUsername}** ⟶ Drone\n";
                text += $"MegaVova99 <:war:561287719838547981> **{player.DiscordUsername}** ⟶ MegaVova99\n";
                text += "EloBoost <:war:561287719838547981> PETYX ⟶ PETYX\n";
                text += "PETYX <:war:561287719838547981> EloBoost ⟶ PETYX\n";
                text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ PETYX\n";
            }

            if (player.ClassString.Contains("Сильный"))
            {
                text += $"EloBoost <:war:561287719838547981> **{player.DiscordUsername}** ⟶ EloBoost\n";
                text += $"YasuoOnly <:war:561287719838547981> **{player.DiscordUsername}** ⟶ YasuoOnly\n";
                text += "Drone <:war:561287719838547981> PETYX ⟶ PETYX\n";
                text += "PETYX <:war:561287719838547981> YasuoOnly ⟶ PETYX\n";
                text += "MegaVova99 <:war:561287719838547981> PETYX ⟶ PETYX\n";
            }

            if (player.ClassString.Contains("Быстрый"))
            {
                text += $"PETYX <:war:561287719838547981> **{player.DiscordUsername}** ⟶ PETYX\n";
                text += $"Drone <:war:561287719838547981> **{player.DiscordUsername}** ⟶ Drone\n";
                text += "MegaVova99 <:war:561287719838547981> PETYX ⟶ PETYX\n";
                text += "EloBoost <:war:561287719838547981> PETYX ⟶ PETYX\n";
                text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ PETYX\n";
            }
        }

        if (game.RoundNumber == 8)
        {
            player.Justice += 2;

            var target1 = game.PlayersList.Find(x => x.PlayerId == player.WhoToAttackThisTurn);
            var moral = target1.PlaceAtLeaderBoard - player.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral -= moral;

            target1 = game.PlayersList.Find(x => x.DiscordUsername == "PETYX");
            moral = target1.PlaceAtLeaderBoard - player.PlaceAtLeaderBoard;
            if (moral > 0)
                player.Moral -= moral;

            if (player.Moral < 0)
                player.Moral = 0;

            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ {target.DiscordUsername}\n";
            text += $"PETYX <:war:561287719838547981> **{player.DiscordUsername}** ⟶ PETYX\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ PETYX\n";
            text += "Drone <:war:561287719838547981> PETYX ⟶ Drone\n";
            text += "EloBoost <:war:561287719838547981> PETYX ⟶ EloBoost\n";
            text += "MegaVova99 <:war:561287719838547981> PETYX ⟶ MegaVova99\n";

            text += "\nТоля запизделся и спалил, что MegaVova99  - AWDKA\n";
        }

        if (game.RoundNumber == 9)
        {
            if (player.ClassString.Contains("Умный") && target.ClassString.Contains("Быстрый"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
                player.Justice = 0;
            }
            else if (player.ClassString.Contains("Быстрый") && target.ClassString.Contains("Сильный"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
                player.Justice = 0;
            }
            else if (player.ClassString.Contains("Сильный") && target.ClassString.Contains("Умный"))
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}** \n";
                player.Score += 2;
                player.Justice = 0;
            }
            else
            {
                text +=
                    $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ {target.DiscordUsername} \n";
                player.Justice += 1;
            }

            player.Justice += 2;

            text += $"Drone <:war:561287719838547981> **{player.DiscordUsername}** ⟶ Drone\n";
            text += $"MegaVova99 <:war:561287719838547981> **{player.DiscordUsername}** ⟶ MegaVova99\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ YasuoOnly\n";
            text += "PETYX <:war:561287719838547981> EloBoost ⟶ EloBoost\n";
            text += "EloBoost <:war:561287719838547981> PETYX ⟶ EloBoost\n";
        }

        if (game.RoundNumber == 10)
        {
            player.Justice += 3;


            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ {target.DiscordUsername}\n";
            text += $"PETYX <:war:561287719838547981> **{player.DiscordUsername}** ⟶ PETYX\n";
            text += $"YasuoOnly <:war:561287719838547981> **{player.DiscordUsername}** ⟶ YasuoOnly\n";
            text += "Drone <:war:561287719838547981> PETYX ⟶ Drone\n";
            text += "EloBoost <:war:561287719838547981> PETYX ⟶ EloBoost\n";
            text += "MegaVova99 <:war:561287719838547981> PETYX ⟶ MegaVova99\n";
        }

        if (game.RoundNumber == 11)
        {
            player.Score += 11;
            player.Justice = 1;
            text = "";
            text +=
                $"**{player.DiscordUsername}** <:war:561287719838547981> {target.DiscordUsername} ⟶ **{player.DiscordUsername}**\n";
            text +=
                $"MegaVova99 <:war:561287719838547981> **{player.DiscordUsername}** ⟶ **{player.DiscordUsername}**\n";
            text += $"EloBoost <:war:561287719838547981> **{player.DiscordUsername}** ⟶ EloBoost\n";
            text += "Drone <:war:561287719838547981> PETYX ⟶ Drone\n";
            text += "YasuoOnly <:war:561287719838547981> PETYX ⟶ YasuoOnly\n";
            text += "PETYX <:war:561287719838547981> MegaVova99 ⟶ MegaVova99\n\n";
            text += $"Предположение: +3 __бонусных__ очка\n\n**{player.DiscordUsername}** Победил!";
        }

        text += "\n";

        return text;
    }

    public string GetTitleTutorial()
    {
        return "King of the Garbage Hill";
    }

    public string GetFooter()
    {
        return "Спасибо, что пробуете нашу игру! | Обучение";
    }


    public async Task SendMessageTutorial(TutorialGame game)
    {
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        switch (game.RoundNumber)
        {
            case 1:
                var msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Выберите цель для нападения, посмотрим что будет...");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 2:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Кажется, враг сильнее по Статам... Вы проиграли, но получили *Справедливость* - она повысит шансы на победу против тех, у кого ее меньше. Прибавляется за поражения, но полностью отнимается после победы. Выберите цель из тех, кто победил в прошлом раунде (сбросил) и не проиграл (не получил новую).");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 3:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Вы получили *Мораль*! Она дается за победу над вышестоящим в таблице игроком. Но отнимается за поражения против нижестоящих.\nЧем дальше враги по таблице, тем больше *Морали* прибавится или отнимется.\n*Мораль* можно обменять на __бонусные__ очки! Чем больше обмениваем, тем выгоднее!");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 4:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Иногда, когда нет *Справедливости* или шансов на победу, выгоднее всего подгадать атаку врага и поставить блок!\n(Вместо нападения вы можете встать в блок, он отнимет у каждого нападающего 1 __бонусное__ очко, атак же заберет у каждого 1 *Справедливости* и прибавит ее вам за каждого напавшего!)");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 5:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Ого, кажется множитель очков подъехал! Начиная с пятого хода, получаемые **обычные** очки умножаются в 2 раза! Пришло время атаковать!");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;

            case 6:
                var classText = "";
                if (player.Intelligence >= player.Strength && player.Intelligence >= player.Speed)
                    classText = "Умный";
                else if (player.Strength >= player.Intelligence && player.Strength >= player.Speed)
                    classText = "Сильный";
                else if (player.Speed >= player.Intelligence && player.Speed >= player.Strength)
                    classText = "Быстрый";
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    $"Судя по вашей прокачке, ваш *Класс:* **{classText}**. Класс определяется наивысшим статом из первых трех. (Умный персонаж может обмануть Быстрого / Быстрый может обогнать Сильного / Сильный может пресануть Умного) - это немного повлияет на исход боя.");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 7:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Похоже нам нужно стать чуть-чуть сильнее. Для этого есть *Скилл* - он незначительно увеличивает эффективность всех статов. Эта полезная мелочь может повлиять на исход. Каждый раунд *Скилл* будет предлагать вам *Мишень* - это *Класс* врага, нападение на которого прибавит вам *Скилла*! Сейчас *Мишень* указывает на **Умного**. Если вы узнали кто из врагов **Умный**, вы можете атаковать его и получить *Скилл* за мишень. Сейчас у вас как раз больше *Справедливости*. Самое время получить немного *Скилла*!");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 8:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Ого, обратите внимание на верхние логи. Кажется, мы узнали, что MegaVova99  играет за **AWDKA**.\nМы узнали какой персонаж ему выпал. Профессионалы игры могли бы сами это отгадать, но нам помогла болтливость **Толи**. Давайте укажем что  ник бота - это **AWDKA**, в конце игры вы получите по 3 __бонусных__ очка за каждое верное предположение.\nВнизу появился **список предположений**...");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 9:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "А вот и еще один способ заработать *Скилл* - квест *Класса*. Умный получает *Скилл*, когда атакует врагов, у которых 0 *Справедливости* на момент нападения / Сильный получает за победы / Быстрый за каждое сражение в котором он поучаствовал.");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 10:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Вооот она! Последний раунд. *Множитель очков* **x4** !\nСамое время вырваться вперед!");
                player.MessageToDeleteNextRound.Add(msg.Id);
                break;
            case 11:
                msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(
                    "Игра окончена, туториал пройден. Надеюсь, вы разобрались в механиках и готовы их ломать с помощью наших великолепных персонажей в обычном режиме.\n" +
                    "Для запуска игры напишите `*st` прямо здесь\n" +
                    "Если хотите посмотреть всех персонажей - напишите `*лор` или `*lore`\n" +
                    "Для смены сложности - напишите `*сложность`\n" +
                    "Ваша сложность \"**Обычная**\" - она показывает ровно столько информации, сколько было задумано разработчиками\n" +
                    "Доступная сложность \"**Казуальная**\" - она показывает больше информации упрощая механику **предположений**\n" +
                    "Удачной игры!");
                player.MessageToDeleteNextRound.Add(msg.Id);
                _userAccounts.GetAccount(player.PlayerId).PassedTutorial = true;
                break;
        }
    }

    public async Task SendMessageTutorial(TutorialGame game, string text)
    {
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);
        var msg = await game.SocketMessageFromBot.Channel.SendMessageAsync(text);
        player.MessageToDeleteNextRound.Add(msg.Id);
    }

    public async Task DeleteMessagesNextRoundTutorial(TutorialGame game)
    {
        var player = game.PlayersList.Find(x => x.PlayerId == game.DiscordPlayerId);

        for (var i = player.MessageToDeleteNextRound.Count - 1; i >= 0; i--)
        {
            var m = await game.SocketMessageFromBot.Channel.GetMessageAsync(player.MessageToDeleteNextRound[i]);
            await m.DeleteAsync();
            player.MessageToDeleteNextRound.RemoveAt(i);
        }
    }


    public async Task ReactionAddedTutorial(SocketMessageComponent button)
    {
        try
        {
            switch (button.Data.CustomId)
            {
                case "attack-select-tutorial":
                    var game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;
                    var attackSelection = string.Join("", button.Data.Values);

                    if (game.RoundNumber == 2)
                        if (attackSelection != "1")
                        {
                            await SendMessageTutorial(game, "Не того выбираешь, давай по новой");
                            return;
                        }

                    if (game.RoundNumber == 7)
                        if (attackSelection != "3")
                        {
                            await SendMessageTutorial(game, "Не того выбираешь, давай по новой");
                            return;
                        }


                    var player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);
                    player.WhoToAttackThisTurn = Convert.ToUInt64(attackSelection);
                    game.RoundNumber++;

                    if (game.RoundNumber == 8)
                        player.Skill += 20;

                    var embed = new EmbedBuilder();
                    var builder = new ComponentBuilder();


                    if (game.RoundNumber == 1 || game.RoundNumber == 2)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithSelectMenu(GetAttackMenuTutorial(game));
                    }

                    if (game.RoundNumber == 3)
                    {
                        player.Score = 2;
                        player.Justice = 0;
                        embed = LvlUpPageTutorial(game);
                        builder.WithSelectMenu(GetLvlUpMenuTutorial(game));
                    }

                    if (game.RoundNumber == 4)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial());
                        builder.WithSelectMenu(GetAttackMenuTutorial(game, true), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                    }


                    if (game.RoundNumber == 6)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                    }


                    if (game.RoundNumber == 7)
                    {
                        player.Justice = 0;
                        embed = LvlUpPageTutorial(game);
                        builder.WithSelectMenu(GetLvlUpMenuTutorial(game));
                    }

                    if (game.RoundNumber == 8)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game, true), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredictMenuTutorial(game), 2);
                    }


                    if (game.RoundNumber == 9)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredictMenuTutorial(game, true), 2);
                    }

                    if (game.RoundNumber == 10)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredictMenuTutorial(game, true), 2);
                    }

                    if (game.RoundNumber == 11)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game, true), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredictMenuTutorial(game, true), 2);
                    }

                    await game.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });


                    await DeleteMessagesNextRoundTutorial(game);

                    if (game.RoundNumber != 3 && game.RoundNumber != 7) await SendMessageTutorial(game);

                    break;
                case "lvlup-select-tutorial":
                    game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;
                    var lvlupSelection = Convert.ToUInt64(string.Join("", button.Data.Values));
                    player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);

                    switch (lvlupSelection)
                    {
                        case 1:
                            player.Intelligence++;
                            break;
                        case 2:
                            player.Strength++;
                            break;
                        case 3:
                            player.Speed++;
                            break;
                        case 4:
                            player.Psyche++;
                            break;
                    }

                    embed = new EmbedBuilder();
                    builder = new ComponentBuilder();

                    if (game.RoundNumber == 3)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithSelectMenu(GetAttackMenuTutorial(game, true));
                        builder.WithButton(GetMoralButtonTutorial(game));
                    }


                    if (game.RoundNumber == 5)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                    }

                    if (game.RoundNumber == 7)
                    {
                        embed.WithTitle(GetTitleTutorial());
                        embed.WithDescription(GetDescriptionTutorial(game));
                        embed.AddField(GetStatsBoardTutorial(game));
                        embed.AddField(GetSecondaryStatsBoardTutorial(game));
                        embed.AddField(GetLeaderBoardTutorial(game));
                        embed.WithFooter(GetFooter());

                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                    }

                    await game.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });
                    await SendMessageTutorial(game);

                    break;
                case "moral-tutorial":
                    game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;
                    player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);

                    embed = new EmbedBuilder();
                    builder = new ComponentBuilder();

                    if (player.Moral >= 15)
                    {
                        player.Score += 10;
                        player.Moral -= 15;
                    }
                    else if (player.Moral >= 10)
                    {
                        player.Score += 6;
                        player.Moral -= 10;
                    }
                    else if (player.Moral >= 5)
                    {
                        player.Score += 2;
                        player.Moral -= 5;

                        if (game.RoundNumber == 3)
                        {
                            embed.WithTitle(GetTitleTutorial());
                            embed.WithDescription(GetDescriptionTutorial(game));
                            embed.AddField(GetStatsBoardTutorial(game));
                            embed.AddField(GetSecondaryStatsBoardTutorial(game));
                            embed.AddField(GetLeaderBoardTutorial(game));
                            embed.WithFooter(GetFooter());


                            builder.WithSelectMenu(GetAttackMenuTutorial(game, true), 1);
                            builder.WithButton(GetMoralButtonTutorial(game));
                        }
                    }
                    else if (player.Moral >= 3)
                    {
                        player.Score += 1;
                        player.Moral -= 3;

                        if (game.RoundNumber == 3)
                        {
                            embed.WithTitle(GetTitleTutorial());
                            embed.WithDescription(GetDescriptionTutorial(game));
                            embed.AddField(GetStatsBoardTutorial(game));
                            embed.AddField(GetSecondaryStatsBoardTutorial(game));
                            embed.AddField(GetLeaderBoardTutorial(game));
                            embed.WithFooter(GetFooter());


                            builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                            builder.WithButton(GetMoralButtonTutorial(game));
                        }
                    }


                    await game.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });

                    break;

                case "block-tutorial":
                    game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;

                    embed = new EmbedBuilder();
                    builder = new ComponentBuilder();
                    player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);

                    if (game.RoundNumber == 4)
                    {
                        game.RoundNumber++;
                        player.Justice = 3;

                        embed = LvlUpPageTutorial(game);
                        builder.WithSelectMenu(GetLvlUpMenuTutorial(game));
                    }

                    await game.SocketMessageFromBot.ModifyAsync(message =>
                    {
                        message.Embed = embed.Build();
                        message.Components = builder.Build();
                    });
                    await DeleteMessagesNextRoundTutorial(game);
                    break;

                case "predict-tutorial-1":
                    game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;
                    player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);

                    embed = new EmbedBuilder();
                    builder = new ComponentBuilder();

                    if (game.RoundNumber == 8)
                    {
                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game, true), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredict2MenuTutorial(game), 2);
                    }


                    await game.SocketMessageFromBot.ModifyAsync(message => { message.Components = builder.Build(); });
                    break;
                case "predict-tutorial-2":
                    game = _gameGlobal.Tutorials.Find(x => x.DiscordPlayerId == button.User.Id);
                    if (game == null) return;
                    player = game.PlayersList.Find(x => x.PlayerId == button.User.Id);
                    player.Predicted = true;
                    embed = new EmbedBuilder();
                    builder = new ComponentBuilder();

                    if (game.RoundNumber == 8)
                    {
                        builder.WithButton(GetBlockButtonTutorial(true));
                        builder.WithSelectMenu(GetAttackMenuTutorial(game), 1);
                        builder.WithButton(GetMoralButtonTutorial(game, true));
                        builder.WithSelectMenu(GetPredictMenuTutorial(game, true), 2);
                    }


                    await game.SocketMessageFromBot.ModifyAsync(message => { message.Components = builder.Build(); });
                    break;
            }
        }
        catch
        {
            //ingored
        }
    }


    public class TutorialGame
    {
        public TutorialGame(IUser discordPlayer, IUserMessage socketMessageFromBot)
        {
            DiscordPlayerId = discordPlayer.Id;
            RoundNumber = 1;
            SocketMessageFromBot = socketMessageFromBot;
            PlayersList = new List<TutorialGamePlayers>
            {
                new(DiscordPlayerId, 6, discordPlayer.Username),
                new(5, 5, "MegaVova99"),
                new(4, 4, "YasuoOnly"),
                new(3, 3, "PETYX"),
                new(2, 2, "Drone"),
                new(1, 1, "EloBoost")
            };
        }

        public ulong DiscordPlayerId { get; set; }
        public int RoundNumber { get; set; }
        public IUserMessage SocketMessageFromBot { get; set; }
        public List<TutorialGamePlayers> PlayersList { get; set; }
    }

    public class TutorialGamePlayers
    {
        public TutorialGamePlayers(ulong playerId, int placeAtLeaderBoard, string playerName)
        {
            PlayerId = playerId;
            PlaceAtLeaderBoard = placeAtLeaderBoard;
            DiscordUsername = playerName;
            Score = 0;
            Intelligence = 5;
            Strength = 5;
            Speed = 5;
            Psyche = 5;
            Justice = 1;
            Moral = 5 + 3;
            ClassString = "";
            Skill = 0;
            LogsCurrent = "";
            LogsFight = "";
            LogsPrevious = "";
            WhoToAttackThisTurn = 0;
            MessageToDeleteNextRound = new List<ulong>();
            Predicted = false;
        }

        public ulong PlayerId { get; set; }
        public string DiscordUsername { get; set; }
        public int PlaceAtLeaderBoard { get; set; }
        public int Score { get; set; }
        public int Intelligence { get; set; }
        public int Strength { get; set; }
        public int Speed { get; set; }
        public int Psyche { get; set; }
        public int Justice { get; set; }
        public int Moral { get; set; }
        public string ClassString { get; set; }
        public int Skill { get; set; }
        public string LogsCurrent { get; set; }
        public string LogsPrevious { get; set; }
        public string LogsFight { get; set; }
        public ulong WhoToAttackThisTurn { get; set; }
        public List<ulong> MessageToDeleteNextRound { get; set; }
        public bool Predicted { get; set; }
    }
}