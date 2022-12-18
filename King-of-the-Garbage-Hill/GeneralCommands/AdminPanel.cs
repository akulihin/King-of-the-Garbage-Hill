using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using King_of_the_Garbage_Hill.DiscordFramework.Extensions;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
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
    
    private readonly General _general;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;
    private readonly GameUpdateMess _upd;


    public AdminPanel(UserAccounts accounts, HelperFunctions helperFunctions,
        Global global, GameUpdateMess upd, CharactersPull charactersPull, CharacterPassives characterPassives,
        General general)
    {

        _accounts = accounts;
        _helperFunctions = helperFunctions;
        _global = global;
        _upd = upd;
        _charactersPull = charactersPull;
        _characterPassives = characterPassives;
        _general = general;
    }

    [Command("getInvite")]
    [Alias("inv")]
    [RequireOwner]
    [Summary("Get invite to a guild")]
    public async Task GetInviteToTheServer(ulong guildId)
    {
        var inviteUrl = _global.Client.GetGuild(guildId).DefaultChannel.CreateInviteAsync();
        await SendMessageAsync($"{inviteUrl.Result.Url}");
    }


    [Command("leaveGuild")]
    [Alias("lGuild")]
    [RequireOwner]
    [Summary("Leave a guild")]
    public async Task LeaveGuild(ulong guildId)
    {
        var guild = _global.Client.GetGuild(guildId);
        await guild.LeaveAsync();
        await SendMessageAsync($"{guild.Name} Left");
    }


    [Command("ShowGuildInfo")]
    [Alias("guildInfo")]
    [RequireOwner]
    [Summary("Show guild/server info")]
    public async Task ShowGuildInfo(ulong guildId)
    {
        var guild = _global.Client.GetGuild(guildId);
        var bot = guild.GetUser(_global.Client.CurrentUser.Id);
        var dateTimeOffset = bot!.JoinedAt;
        if (dateTimeOffset != null)
        {
            var joinedAt = dateTimeOffset.Value;

            await SendMessageAsync($"{guild.Name} - {guild.Id}\n" +
                                   $"Created: {guild.CreatedAt.Day}/{guild.CreatedAt.Month}/{guild.CreatedAt.Year} (Joined on {joinedAt.Day}/{joinedAt.Month}/{joinedAt.Year})\n" +
                                   $"Members: {guild.MemberCount}\n" +
                                   $"Owner: {guild.Owner.Username} ({guild.Owner.Id})\n" +
                                   $"Description: {guild.Description}\n" +
                                   $"Banner: {guild.BannerUrl}\n");
        }
    }


    [Command("ShowGuilds")]
    [Alias("guilds")]
    [RequireOwner]
    [Summary("Shows bot's guilds/servers")]
    public async Task ShowConnectedGuilds()
    {
        var guilds = _global.Client.Guilds;
        var text = "";
        foreach (var guild in guilds)
        {
            text += $"{guild.Name} - {guild.Id}\n";
        }
        await SendMessageAsync($"{text}");
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
    [Alias("st")]
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
                $"ERROR: 404 - no such GameCharacter. Please select between 0 and {allCharacters.Count - 1}");
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
        playersList = playersList.OrderBy(_ => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);

        //выдаем место в таблице
        for (var i = 0; i < playersList.Count; i++) playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);

        //отправить меню игры
        foreach (var player in playersList) await _upd.WaitMess(player, playersList);

        //создаем игру
        var game = new GameClass(playersList, gameId, Context.User.Id) { IsCheckIfReady = false };
        
        //это нужно для ботов
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

        //start the timer
        game.TimePassed.Start();
        _global.GamesList.Add(game);


        //handle round #0
        await _characterPassives.HandleNextRound(game);
        _characterPassives.HandleBotPredict(game);

        foreach (var player in playersList) await _upd.UpdateMessage(player);
        game.IsCheckIfReady = true;
    }


    [Command("SetCharacter")]
    [Summary("set character to roll next game (Admin only)")]
    public async Task CharacterToGiveNextTime(string character = null, IUser player = null)
    {
        //238337696316129280 == DeepList
        if (character == null)
        {
            _accounts.GetAccount(238337696316129280).CharacterToGiveNextTime = "Mit*suki*";
            _accounts.GetAccount(181514288278536193).CharacterToGiveNextTime = "Загадочный Спартанец в маске";
            return;
        }

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
            await SendMessageAsync($"ERROR: 404 - No Such FightCharacter ({character})\nAvailable:\n{extra}");
            return;
        }

        account.CharacterToGiveNextTime = foundCharacter.Name;

        await SendMessageAsync($"Done. {player!.Username} будет играть на {foundCharacter.Name} в следующей игре");
    }


    [Command("SetType")]
    [Summary("setting type of account: 0 (Normal), 1 (Casual), 2 (Admin)")]
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
            playing!.PlayerType = userType;
        }

        await SendMessageAsync($"done. {user.Username} is now **{userType}**");
    }


    [Command("SetStat")]
    [Alias("set")]
    [Summary("cheats: Set a stat, score or round (in, sp, st, ps, js, sk, mr, sc, rn, cr) (Admin only)")]
    public async Task SetCharacteristic(string name, int number)
    {
        if (Context.User.Id != 238337696316129280 && Context.User.Id != 181514288278536193 && Context.User.Id != 284802743493853184)
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
            case "win":
                player!.GameCharacter.SetIntelligence(number, "Читы");
                player.GameCharacter.SetSpeed(number, "Читы");
                player.GameCharacter.SetStrength(number, "Читы");
                player.GameCharacter.SetPsyche(number, "Читы");
                player.GameCharacter.Justice.SetRealJusticeNow(number, "Читы");
                player.GameCharacter.SetMainSkill(number*100, "Читы");
                player.GameCharacter.SetMoralBonus();
                break;
            case "in":
                player!.GameCharacter.SetIntelligence(number, "Читы");
                break;
            case "sp":
                player!.GameCharacter.SetSpeed(number, "Читы");
                break;
            case "st":
                player!.GameCharacter.SetStrength(number, "Читы");
                break;
            case "ps":
                player!.GameCharacter.SetPsyche(number, "Читы");
                break;
            case "js":
                player!.GameCharacter.Justice.SetRealJusticeNow(number, "Читы");
                break;
            case "sk":
                player!.GameCharacter.SetMainSkill(number, "Читы");
                break;
            case "mr":
                player!.GameCharacter.SetMoral(number, "Читы");
                player.GameCharacter.SetMoralBonus();
                break;
            case "sc":
                player!.Status.SetScoreToThisNumber(number, "Читы");
                break;
            case "rn":
                game.RoundNo = number;
                break;
            case "cr":
                var character = _charactersPull.GetAllCharacters()[number];
                player.GameCharacter.Name = character.Name;
                player.GameCharacter.Passive = new List<Passive>();
                player.GameCharacter.Passive = character.Passive;
                player.Status.AddInGamePersonalLogs($"Читы: Ты стал {character.Name}\n");
                break;
            default:
                return;
        }


        foreach (var t in game.PlayersList) await _upd.UpdateMessage(t);
    }
}