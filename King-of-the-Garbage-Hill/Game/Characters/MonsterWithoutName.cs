using System;
using System.Collections.Generic;
using System.Linq;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public static class MonsterWithoutName
{
    public const string CharacterName = "Монстр без имени";
    public const string Twin = "Близнец";

    public sealed class TwinStatCopy
    {
        public Guid SourcePlayerId { get; set; } = Guid.Empty;
        public string SourcePlayerName { get; set; } = "";
        public string StatName { get; set; } = "";
        public int Value { get; set; }
    }

    public sealed class TwinState
    {
        public List<TwinStatCopy> PendingStatCopies { get; set; } = new();
    }

    public static bool HasTwin(GamePlayerBridgeClass player) =>
        player?.GameCharacter?.Name == CharacterName
        && player.GameCharacter.Passive.Any(passive => passive.PassiveName == Twin);

    public static TwinStatCopy CaptureHighestStat(GamePlayerBridgeClass attacker)
    {
        if (attacker?.FightCharacter == null) return null;

        var stats = new[]
        {
            (Name: "Интеллекта", Value: attacker.FightCharacter.GetIntelligence()),
            (Name: "Силы", Value: attacker.FightCharacter.GetStrength()),
            (Name: "Скорости", Value: attacker.FightCharacter.GetSpeed()),
            (Name: "Психики", Value: attacker.FightCharacter.GetPsyche()),
        };
        var highest = stats
            .OrderByDescending(stat => stat.Value)
            .First();
        return new TwinStatCopy
        {
            SourcePlayerId = attacker.GetPlayerId(),
            SourcePlayerName = attacker.DiscordUsername,
            StatName = highest.Name,
            Value = highest.Value,
        };
    }

    public static void QueueHighestStatCopy(
        GamePlayerBridgeClass monster,
        TwinStatCopy copy)
    {
        if (!HasTwin(monster) || copy == null) return;
        monster.Passives.MonsterTwin.PendingStatCopies.Add(copy);
    }

    public static void ApplyPendingStatCopies(GamePlayerBridgeClass monster)
    {
        if (!HasTwin(monster)) return;

        var pending = monster.Passives.MonsterTwin.PendingStatCopies;
        var strongestByStat = pending
            .GroupBy(entry => entry.StatName, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.SourcePlayerId)
                .First())
            .ToList();
        foreach (var copy in strongestByStat)
        {
            switch (copy.StatName)
            {
                case "Интеллекта":
                    monster.GameCharacter.SetIntelligence(copy.Value, Twin, isLog: false);
                    break;
                case "Силы":
                    monster.GameCharacter.SetStrength(copy.Value, Twin, isLog: false);
                    break;
                case "Скорости":
                    monster.GameCharacter.SetSpeed(copy.Value, Twin, isLog: false);
                    break;
                case "Психики":
                    monster.GameCharacter.SetPsyche(copy.Value, Twin, isLog: false);
                    break;
            }
        }

        foreach (var copy in pending)
            monster.Status.AddInGamePersonalLogs(
                $"Скопировал ({copy.Value} {copy.StatName}) с ({copy.SourcePlayerName})\n");

        pending.Clear();
    }
}
