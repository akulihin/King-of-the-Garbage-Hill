using System;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class UnknownBug
{
    public const string CharacterName = "unknown_bug";
    public const string LegacyCharacterName = "Баг";
    public const string AdminPlayerType = "AdminPlayerType";
    public const string AutoWin = "AutoWin";
    public const string PointFunnel = "PointFunnel";
    public const string Exploit = "Exploit";
    public const string MissingAvatar =
        "https://r2.ozvmusic.com/kotgh/art/avatars/unknown_bug.png";

    public sealed class State
    {
        public Guid StreamTargetPlayerId { get; set; } = Guid.Empty;
        public int CommitSerial { get; set; }
        public int LastCommitPoints { get; set; }
    }

    public static bool Is(string characterName) =>
        characterName is CharacterName or LegacyCharacterName;

    public static bool Is(CharacterClass character) =>
        character != null && Is(character.Name);

    public static bool Is(GamePlayerBridgeClass player) =>
        player != null && Is(player.GameCharacter);

    public static bool HasSpecialPassive(string passiveName) =>
        passiveName is AdminPlayerType or AutoWin or PointFunnel or Exploit;

    public static bool HasSpecialPassive(Passive passive) =>
        passive != null && HasSpecialPassive(passive.PassiveName);

    public static string PublicName(GamePlayerBridgeClass player) =>
        Is(player) ? "???" : player?.GameCharacter?.Name ?? "???";

    public static GamePlayerBridgeClass FindOwner(GameClass game) =>
        game?.PlayersList?.FirstOrDefault(Is);

    public static void EnsureExploitMarker(GameClass game)
    {
        if (game == null || game.ExploitClosed) return;

        var owner = FindOwner(game);
        if (owner == null || owner.Passives.IsDead) return;

        var current = game.PlayersList.FirstOrDefault(player =>
            player.GetPlayerId() != owner.GetPlayerId()
            && !player.Passives.IsDead
            && player.Passives.IsExploitable);
        if (current != null)
        {
            foreach (var player in game.PlayersList.Where(player =>
                         player.GetPlayerId() != current.GetPlayerId()))
                player.Passives.IsExploitable = false;
            game.CurrentExploitTargetPlayerId = current.GetPlayerId();
            return;
        }

        game.RollExploit();
    }

    public static void SelectStreamTarget(GameClass game, GamePlayerBridgeClass owner)
    {
        if (game == null || !Is(owner) || owner.Passives.IsDead)
        {
            if (owner != null)
                owner.Passives.UnknownBug.StreamTargetPlayerId = Guid.Empty;
            return;
        }

        owner.Passives.UnknownBug.StreamTargetPlayerId = owner.Status.WhoToAttackThisTurn
            .Where(targetId => targetId != owner.GetPlayerId())
            .Select(targetId => game.PlayersList.FirstOrDefault(player =>
                player.GetPlayerId() == targetId && !player.Passives.IsDead))
            .FirstOrDefault(target => target != null)
            ?.GetPlayerId() ?? Guid.Empty;
    }

    public static void RecordResolvedFight(
        GameClass game,
        GamePlayerBridgeClass winner,
        GamePlayerBridgeClass loser)
    {
        if (game == null || winner == null || loser == null) return;

        var owner = FindOwner(game);
        if (owner == null || owner.Passives.IsDead) return;

        var copiedWinner = owner.Passives.UnknownBug.StreamTargetPlayerId == winner.GetPlayerId();
        var bugDefeatedCarrier = Is(winner);
        if (!game.ExploitClosed
            && game.CurrentExploitTargetPlayerId == loser.GetPlayerId()
            && loser.Passives.IsExploitable
            && (copiedWinner || bugDefeatedCarrier))
            game.TotalExploit++;

        if (!copiedWinner) return;

        owner.Status.AddRegularPoints(1, PointFunnel);
        owner.Status.AddInGamePersonalLogs(
            $"```cs\nPointFunnel.CopyWin({winner.DiscordUsername}); // +1\n```\n");
    }

    public static bool TryCommitExploit(
        GameClass game,
        GamePlayerBridgeClass attacker,
        GamePlayerBridgeClass target,
        bool _attackerWon)
    {
        if (game == null || game.ExploitClosed || !Is(attacker) || target == null
            || game.CurrentExploitTargetPlayerId != target.GetPlayerId()
            || !target.Passives.IsExploitable)
            return false;

        var rawPoints = game.TotalExploit;
        var state = attacker.Passives.UnknownBug;
        state.LastCommitPoints = rawPoints * attacker.Status.GetRoundScoreMultiplier(game);
        state.CommitSerial++;

        game.CloseExploit(target);
        if (game.PlayersList.Any(player => player.GameCharacter.Name == "DeepList"))
            game.AddGlobalLogs("DeepList: Что за баг? Раньше его не было! Раньше было лучше.");
        if (game.PlayersList.Any(player => player.GameCharacter.Name == "mylorik"
                                           || player.Passives.AchievementTracker.TransformedFromMylorik))
            game.AddGlobalLogs("mylorik: Это что, опять баг? Надо его пофиксить. ММММ!!!");
        if (rawPoints > 0)
            attacker.Status.AddRegularPoints(rawPoints, Exploit);
        attacker.Status.AddInGamePersonalLogs(
            $"```cs\nExploit.Commit(); // +{state.LastCommitPoints} pts\n```\n");
        game.TotalExploit = 0;
        return true;
    }
}
