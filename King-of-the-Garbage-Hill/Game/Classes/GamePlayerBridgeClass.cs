using System;
using System.Collections.Generic;
using System.Linq;

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
    public List<DeleteMessagesClass> DeleteMessages { get; set; } = new();
    public List<PredictClass> Predict { get; set; } = new();

    public int TeamId { get; set; }
    public bool IsMobile {get; set; }

    public bool IsWebPlayer { get; set; }

    public int CharacterMasteryPoints { get; set; }

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
        if (playerCharacter.Passive.Any(x => x.PassiveName == "Спокойствие"))
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