using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Characters;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class GamePlayerBridgeClass
{

    public GamePlayerBridgeClass(CharacterClass gameCharacter, InGameStatus status, ulong discordId, ulong gameId, string discordUsername, int playerType)
    {
        Status = status;
        gameCharacter.SetStatus(Status);
        GameCharacter = gameCharacter;
        Status.GameCharacter = GameCharacter;
        FightCharacter = GameCharacter.DeepCopy();

        DiscordId = discordId;
        GameId = gameId;
        DiscordUsername = discordUsername;
        PlayerType = playerType;
        DiscordStatus = new InGameDiscordStatus();
        GameCharacter.SetIntelligenceResist();
        GameCharacter.SetStrengthResist(this);
        GameCharacter.SetSpeedResist();
        GameCharacter.SetPsycheResist();
        Passives = new PassivesClass();
    }

    public CharacterClass FightCharacter { get; set; }
    public CharacterClass GameCharacter { get; set; }

    public PassivesClass Passives { get; set; }

    public InGameStatus Status { get; set; }

    public InGameDiscordStatus DiscordStatus { get; set; }

    public ulong DiscordId { get; set; }
    public ulong GameId { get; set; }

    public string DiscordUsername { get; set; }

/*
0 == Normal
1 == Casual
2 == Admin
404 == Bot
*/
    public int PlayerType { get; set; }

    /// <summary>Per-player bot AI difficulty override (sim measurement probe only). -1 = inherit the game's
    /// AiDifficulty. Set by the sim's --ai-probe to run one bot at a different level than the rest of the
    /// field, so a character's piloting can be A/B-measured in an otherwise-identical line-up.</summary>
    public int AiDifficulty { get; set; } = -1;

    /// <summary>Persistent L2/L3 bot plan selected once per match. Empty for legacy L1 bots and
    /// humans; the simulation report records it so individual builds can be measured.</summary>
    public string AiPlaystyle { get; set; } = "";

    /// <summary>
    /// What a strict bot has actually observed as an ordinary player. Target selection for AI levels 2/3
    /// must go through this memory instead of reading another bridge's hidden character/action state.
    /// This belongs to the persistent seat, not CharacterClass/FightCharacter, so DeepCopy is unaffected.
    /// </summary>
    public BotKnowledgeState AiKnowledge { get; set; } = new();

    public List<DeleteMessagesClass> DeleteMessages { get; set; } = new();
    public List<PredictClass> Predict { get; set; } = new();

    public int TeamId { get; set; }
    public bool IsMobile {get; set; }

    public bool IsWebPlayer { get; set; }

    public int CharacterMasteryPoints { get; set; }

    /// <summary>True only when this seat consumed a queued loot-box character reward.</summary>
    public bool IsLootBoxCharacterReward { get; set; }

    /// <summary>When true, suppress Discord messages and only use the web UI.</summary>
    public bool PreferWeb { get; set; }

    /// <summary>Ephemeral messages for web display (equivalent of SendMsgAndDeleteItAfterRound in Discord).</summary>
    public List<string> WebMessages { get; set; } = new();

    /// <summary>Media messages for web display (equivalent of SendLogSeparate / SendLogSeparateWithFile in Discord).</summary>
    public List<WebMediaEntry> WebMediaMessages { get; set; } = new();

    /// <summary>Represents a character phrase message that can include text, audio, or images.</summary>
    public class WebMediaEntry
    {
        public string PassiveName { get; set; }
        public string Text { get; set; }
        public string PassiveNameEnglish { get; set; }
        public string TextEnglish { get; set; }
        /// <summary>URL path to the file (e.g. /art/events/kratos_death.jpg or /sound/Kratos_PLAY_ME.mp3). Null for text-only.</summary>
        public string FileUrl { get; set; }
        /// <summary>One of: "text", "audio", "image"</summary>
        public string FileType { get; set; } = "text";
        /// <summary>How many rounds this media should keep playing. 1 = current round only (default). For audio, continues looping across rounds.</summary>
        public int RoundsToPlay { get; set; } = 1;
        /// <summary>Tracks how many rounds this entry has been alive (incremented each round).</summary>
        public int RoundsPlayed { get; set; } = 0;
    }

    public bool IsBot()
    {
        if (IsWebPlayer) return false;
        return PlayerType == 404 || DiscordStatus.SocketGameMessage == null;
    }

    public void MinusPsycheLog(CharacterClass playerCharacter, GameClass game, int howMuchToRemove, string skillName)
    {
        if (UnknownBug.Is(playerCharacter))
        {
            return;
        }
        if (Madara.HasReanimatedBody(playerCharacter))
        {
            return;
        }
        if (playerCharacter.Passive.Any(x => x.PassiveName == "Спокойствие"))
        {
            return;
        }
        // TheBoys — after M.M.'s first upgrade he is calm; СуперМудень disables M.M. entirely.
        if (Passives.TheBoysMM.IsCalm && !Passives.TheBoysButcher.SuperDickActive)
        {
            return;
        }
        game.AddGlobalLogs($"\n{DiscordUsername} психанул");
        playerCharacter.AddPsyche(howMuchToRemove, skillName);
    }

    public Guid GetPlayerId()
    {
        return Status.PlayerId;
    }

    public bool IsTeamMember(GameClass game, Guid player2)
    {
        var team = game.Teams.Find(x => x.TeamPlayers.Contains(GetPlayerId()));
        return team != null && team.TeamPlayers.Contains(player2);
    }

    public class DeleteMessagesClass
    {
        public ulong MessageId;
        public int DelayMs;

        public DeleteMessagesClass(ulong messageId, int delayMs)
        {
            MessageId = messageId;
            DelayMs = delayMs;
        }
    }

    public bool IsSolo(GameClass game)
    {
        if (!Status.ConfirmedSkip)
            return false;

        var humans = game.PlayersList.Where(x => !x.IsBot());
        if (humans.Count() == 1)
            return true;

        var ready = humans.Where(x => x.Status.IsReady);
        return (ready.Count() == humans.Count());
    }
}

/// <summary>Viewer-scoped, persistent bot memory. It is never serialized to clients or replays.</summary>
public sealed class BotKnowledgeState
{
    public int LastCapturedRound { get; set; }
    public Dictionary<int, string> VisibleGlobalLogsByRound { get; set; } = new();
    public Dictionary<Guid, BotOpponentKnowledge> Opponents { get; set; } = new();
    public Dictionary<Guid, BotPredictionEvidence> PredictionEvidence { get; set; } = new();

    public BotOpponentKnowledge Opponent(Guid playerId)
    {
        if (!Opponents.TryGetValue(playerId, out var knowledge))
        {
            knowledge = new BotOpponentKnowledge();
            Opponents[playerId] = knowledge;
        }

        return knowledge;
    }
}

/// <summary>Facts derived from resolved, viewer-visible fights and public action history.</summary>
public sealed class BotOpponentKnowledge
{
    public Dictionary<int, int> PlacesByRound { get; set; } = new();
    public int? LastObservedJustice { get; set; }
    public int LastObservedJusticeRound { get; set; }
    public decimal LastObservedFightEdge { get; set; }
    public int LastObservedFightRound { get; set; }
    public string LastObservedClass { get; set; } = "";
    public Dictionary<int, int> AttacksByRound { get; set; } = new();
    public Dictionary<int, int> TimesTargetedByRound { get; set; } = new();
    public Dictionary<int, int> NonFightsByRound { get; set; } = new();
    public Dictionary<int, int> AttacksOnViewerByRound { get; set; } = new();
    public Dictionary<int, int> FightsWithViewerByRound { get; set; } = new();
    public Dictionary<int, int> WinsByRound { get; set; } = new();
    public Dictionary<int, int> LossesByRound { get; set; } = new();
}

/// <summary>A bot's own hypothesis or an identity explicitly revealed to that bot.</summary>
public sealed class BotPredictionEvidence
{
    public string CharacterName { get; set; } = "";
    public int Confidence { get; set; }
    public string Evidence { get; set; } = "";
    public int RoundUpdated { get; set; }
    public bool IsExactReveal { get; set; }
}
