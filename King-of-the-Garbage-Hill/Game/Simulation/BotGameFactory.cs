using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using King_of_the_Garbage_Hill.Game.Classes;
using King_of_the_Garbage_Hill.Game.DiscordMessages;
using King_of_the_Garbage_Hill.Game.GameLogic;
using King_of_the_Garbage_Hill.Helpers;

namespace King_of_the_Garbage_Hill.Game.Simulation;

/// <summary>
/// Single source of truth for creating an all-bot game. Extracted verbatim from the
/// *stb command loop body (General.cs) so the Discord command and the headless
/// simulation harness (--sim) drive the exact same creation path.
/// </summary>
public class BotGameFactory : IServiceSingleton
{
    private readonly CharacterPassives _characterPassives;
    private readonly Global _global;
    private readonly HelperFunctions _helperFunctions;
    private readonly StartGameLogic _startGameLogic;
    private readonly GameUpdateMess _upd;

    public BotGameFactory(Global global, StartGameLogic startGameLogic, CharacterPassives characterPassives,
        GameUpdateMess upd, HelperFunctions helperFunctions)
    {
        _global = global;
        _startGameLogic = startGameLogic;
        _characterPassives = characterPassives;
        _upd = upd;
        _helperFunctions = helperFunctions;
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates one 6-bot game and adds it to Global.GamesList (running: IsCheckIfReady = true).
    /// <paramref name="forcedCharacters"/> assigns characters by list order instead of rolling
    /// (caller owns line-up validity: LeCrisp/Толя apart, ≤1 Tier-4, no TeamModeOnly).
    /// </summary>
    public async Task<GameClass> CreateBotGameAsync(ulong creatorId, string mode = "Bot",
        uint testFightNumber = 0, List<string> forcedCharacters = null, int aiDifficulty = 3,
        int aiProbe = -1, string aiProbeChar = null)
    {
        var players = new List<IUser>
        {
            null,
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
        var playersList = _startGameLogic.HandleCharacterRoll(players, gameId, mode: "bot",
            forcedCharacters: forcedCharacters);


        //тасуем игроков — seeded sim uses the deterministic RNG shuffle so a fixed --seed
        //reproduces seating; real games keep the Guid.NewGuid() shuffle untouched.
        playersList = SecureRandom.IsSeeded
            ? SecureRandom.Shuffle(playersList)
            : playersList.OrderBy(_ => Guid.NewGuid()).ToList();
        playersList = playersList.OrderByDescending(x => x.Status.GetScore()).ToList();
        playersList = _characterPassives.HandleEventsBeforeFirstRound(playersList);

        //выдаем место в таблице
        for (var i = 0; i < playersList.Count; i++) playersList[i].Status.SetPlaceAtLeaderBoard(i + 1);


        //создаем игру
        var game = new GameClass(playersList, gameId, creatorId, 300, mode) { IsCheckIfReady = false };
        game.AiDifficulty = Math.Clamp(aiDifficulty, 0, 3);

        // --ai-probe: run one bot at a different level than the field (A/B measurement). By character name
        // if given (so it aggregates across coverage games), else the first slot. -1 leaves the whole field
        // on the game default.
        if (aiProbe >= 0)
        {
            var probe = aiProbeChar != null
                ? playersList.Find(p => p.GameCharacter.Name == aiProbeChar)
                : playersList.ElementAtOrDefault(0);
            if (probe != null) probe.AiDifficulty = Math.Clamp(aiProbe, 0, 3);
        }

        //отправить меню игры
        foreach (var player in playersList) await _upd.WaitMess(player, game);

        game.TestFightNumber = testFightNumber;

        //это нужно для ботов
        game.NanobotsList.Add(new BotsBehavior.NanobotClass(playersList));

        //start the timer
        game.TimePassed.Start();
        _global.GamesList.Add(game);


        //handle predict
        if (mode == "Bot")
        {
            foreach (var player in game.PlayersList)
            {
                foreach (var enemy in game.PlayersList.Where(x => x.GetPlayerId() != player.GetPlayerId()))
                {
                    player.Predict.Add(new PredictClass(enemy.GameCharacter.Name, enemy.GetPlayerId()));
                }
            }
        }

        //handle round #0
        await _characterPassives.HandleNextRound(game);

        game.IsCheckIfReady = true;

        return game;
    }
}
