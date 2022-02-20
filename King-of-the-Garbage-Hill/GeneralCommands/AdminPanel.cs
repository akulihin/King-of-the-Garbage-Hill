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

namespace King_of_the_Garbage_Hill.GeneralCommands;

public class AdminPanel : ModuleBaseCustom
{
    private readonly UserAccounts _accounts;
    private readonly CharacterPassives _characterPassives;
    private readonly CharactersPull _charactersPull;
    private readonly InGameGlobal _gameGlobal;
    private readonly General _general;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;
    private readonly GameUpdateMess _upd;


    public AdminPanel(UserAccounts accounts, HelperFunctions helperFunctions,
        Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
        CharactersUniquePhrase phrase, InGameGlobal gameGlobal, General general)
    {
        _accounts = accounts;

        _helperFunctions = helperFunctions;

        _global = global;
        _upd = upd;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
        _gameGlobal = gameGlobal;
        _general = general;
    }

    [Command("restart")]
    [Alias("reboot")]
    [Summary("Restarts")]
    public async Task Restart()
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            return;
        }
        await SendMessageAsync("Exiting... try running *uptime in 30 seconds.");
        Environment.Exit(228);
    }

    [Command("игра")]
    [Alias("st", "start", "start game")]
    [Summary("запуск игры (Admin only)")]
    public async Task StartGameTestMode(int choice)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }

        var allCharacters = _charactersPull.GetAllCharacters();

        if (choice + 1 > allCharacters.Count)
        {
            await SendMessageAsync(
                $"ERROR: 404 - no such Character. Please select between 0 and {allCharacters.Count - 1}");
            return;
        }

        var account = _accounts.GetAccount(Context.User);
        account.CharacterToGiveNextTime = allCharacters[choice].Name;

        var players = new List<IUser>
        {
            Context.User,
            null,
            null,
            null,
            null,
            null
        };

        //Заменить игрока на бота
        foreach (var player in players.Where(p => p != null)) _helperFunctions.SubstituteUserWithBot(player.Id);

        //получаем gameId
        var gameId = _global.GetNewtGamePlayingAndId();

        //ролл персонажей для игры
        var playersList = _general.HandleCharacterRoll(players, gameId);


        //тасуем игроков
        playersList = playersList.OrderBy(a => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        playersList = _general.HandleEventsBeforeFirstRound(playersList);

        //выдаем место в таблице
        for (var i = 0; i < playersList.Count; i++) playersList[i].Status.PlaceAtLeaderBoard = i + 1;

        //это нужно для ботов
        _gameGlobal.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

        //отправить меню игры
        foreach (var player in playersList) await _upd.WaitMess(player, playersList);

        //создаем игру
        var game = new GameClass(playersList, gameId, Context.User.Id) { IsCheckIfReady = false };


        //start the timer
        game.TimePassed.Start();
        _global.GamesList.Add(game);

        //get all the chances before the game starts
        _gameGlobal.CalculatePassiveChances(game);

        //handle round #0
        await _characterPassives.HandleNextRound(game);

        foreach (var player in playersList) await _upd.UpdateMessage(player);
        game.IsCheckIfReady = true;
    }


    [Command("SetCharacter")]
    [Summary("set character to roll next game (Admin only)")]
    public async Task CharacterToGiveNextTime(string character, IUser player = null)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }

        var allCharacters = _charactersPull.GetAllCharacters();
        var account = _accounts.GetAccount(Context.User);

        if (player != null) account = _accounts.GetAccount(player);

        var foundCharacter = allCharacters.Find(x => x.Name.ToLower() == character.ToLower());

        if (foundCharacter == null)
        {
            var extra = allCharacters.Aggregate("", (current, c) => current + $"`{c.Name}`\n");
            await SendMessageAsync($"ERROR: 404 - No Such Character ({character})\nAvailable:\n{extra}");
            return;
        }

        account.CharacterToGiveNextTime = foundCharacter.Name;

        await SendMessageAsync($"Done. {player.Mention} будет играть на {foundCharacter.Name} в следующей игре");
    }


    [Command("SetType")]
    [Summary("setting type of account: 0, 1, 2  (Admin only)")]
    public async Task SetType(IUser user, int userType)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }

        var account = _accounts.GetAccount(user);

        if (userType != 0 && userType != 1 && userType != 2)
        {
            await SendMessageAsync("0 == **Normal**\n" +
                                "1 == **Casual**\n" +
                                "2 == **Admin**\n" +
                                "are the only available options");
            return;
        }

        account.PlayerType = userType;
        var game = _global.GamesList.Find(x => x.PlayersList.Any(y => y.DiscordId == user.Id));
        if (game != null)
        {
            var playing = game.PlayersList.Find(y => y.DiscordId == user.Id);
            playing.PlayerType = userType;
        }

        await SendMessageAsync($"done. {user.Username} is now **{userType}**");
    }


    [Command("SetRound")]
    [Alias("sr")]
    [Summary("Select round 1-10 (Admin only)")]
    public async Task SelectRound(int roundNo)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }

        if (roundNo < 1 || roundNo > 10)
        {
            await SendMessageAsync("select between 1 and 10");
            return;
        }

        var game = _global.GamesList.Find(
            l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

        if (game == null) return;

        game.RoundNo = roundNo;

        foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
    }


    [Command("SetScore")]
    [Summary("Set your score (Admin only)")]
    public async Task SetScore(int number)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }

        var game = _global.GamesList.Find(
            l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

        if (game == null) return;


        game.PlayersList.Find(x => x.DiscordId == Context.User.Id).Status
            .SetScoreToThisNumber(number);

        foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
    }


    [Command("SetStat")]
    [Alias("set")]
    [Summary("Set a stat (in, sp, st, ps, js, sk, mr) (Admin only)")]
    public async Task SetCharacteristic(string name, int number)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193)
        {
            await SendMessageAsync("only owners can use this command");
            return;
        }


        var game = _global.GamesList.Find(
            l => l.PlayersList.Any(x => x.DiscordId == Context.User.Id));

        if (game == null) return;

        var player = game.PlayersList.Find(x => x.DiscordId == Context.User.Id);
        switch (name.ToLower())
        {
            case "in":
                player.Character
                    .SetIntelligence(player.Status, number, "Читы");
                break;
            case "sp":
                player.Character
                    .SetSpeed(player.Status, number, "Читы");
                break;
            case "st":
                player.Character
                    .SetStrength(player.Status, number, "Читы");
                break;
            case "ps":
                player.Character
                    .SetPsyche(player.Status, number, "Читы");
                break;
            case "js":
                player.Character
                    .Justice.SetFullJusticeNow(player.Status, number, "Читы");
                break;
            case "sk":
                player.Character
                    .SetMainSkill(player.Status, number, "Читы");
                break;
            case "mr":
                player.Character
                    .SetMoral(player.Status, number, "Читы");
                break;
            default:
                return;
        }


        foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
    }
}