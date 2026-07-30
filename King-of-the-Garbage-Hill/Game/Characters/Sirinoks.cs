using System;
using System.Collections.Generic;
using King_of_the_Garbage_Hill.Game.Classes;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class Sirinoks
{
    public const string CharacterName = "Sirinoks";
    public const string TrainingPassive = "Обучение";
    public const string DragonPassive = "Дракон";
    public const int TrainingSkillReward = 42;
    public const decimal DragonAutowinProtectionSkill = 228m;

    public static bool IsAutowinProtectedDragon(GamePlayerBridgeClass player, GameClass game) =>
        player != null
        && game?.RoundNo >= 10
        && player.GameCharacter.Name == CharacterName
        && player.GameCharacter.Passive.Exists(passive => passive.PassiveName == DragonPassive)
        && player.GameCharacter.GetSkill() >= DragonAutowinProtectionSkill;

    public static bool BlocksAutowinFrom(
        GamePlayerBridgeClass dragon,
        GamePlayerBridgeClass source,
        GameClass game) =>
        IsAutowinProtectedDragon(dragon, game) && !UnknownBug.Is(source);

    public static bool TryCompleteTraining(GamePlayerBridgeClass player)
    {
        var trainingState = player?.Passives.SirinoksTraining;
        var training = trainingState?.Training.Count > 0
            ? trainingState.Training[0]
            : null;
        if (training == null) return false;

        var currentStat = training.StatIndex switch
        {
            1 => player.GameCharacter.GetIntelligence(),
            2 => player.GameCharacter.GetStrength(),
            3 => player.GameCharacter.GetSpeed(),
            4 => player.GameCharacter.GetPsyche(),
            _ => int.MinValue,
        };
        if (currentStat < training.StatNumber) return false;

        player.GameCharacter.AddMoral(3, TrainingPassive);
        player.GameCharacter.AddExtraSkill(TrainingSkillReward, TrainingPassive);
        trainingState!.Training.Clear();
        return true;
    }

    public class SirinoksFriendsClass
    {
        public Guid EnemyId = Guid.Empty;
    }


    public class StatsClass
    {
        public int Index;
        public int Number;

        public StatsClass(int index, int number)
        {
            Index = index;
            Number = number;
        }
    }

    public class TrainingClass
    {
        public Guid EnemyId;
        public List<TrainingSubClass> Training = new();
        public List<int> TriggeredBonusFromStat = new();
    }

    public class TrainingSubClass
    {
        public int StatIndex;
        public int StatNumber;


        public TrainingSubClass(int statIndex, int statNumber)
        {
            StatIndex = statIndex;
            StatNumber = statNumber;
        }
    }
}
