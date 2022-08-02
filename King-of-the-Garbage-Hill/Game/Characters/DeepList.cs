using System;
using System.Collections.Generic;

namespace King_of_the_Garbage_Hill.Game.Characters;

public class DeepList
{
    public class Mockery
    {
        public List<MockerySub> WhoWonTimes = new();
    }

    public class MockerySub
    {
        public Guid EnemyPlayerId;
        public int Times;
        public bool Triggered;

        public MockerySub(Guid enemyPlayerId, int times)
        {
            EnemyPlayerId = enemyPlayerId;
            Times = times;
            Triggered = false;
        }
    }

    public class SuperMindKnown
    {
        public List<Guid> KnownPlayers = new();
    }

    public class Madness
    {
        public List<MadnessSub> MadnessList = new();
        public int RoundItTriggered = 0;
    }

    public class MadnessSub
    {
        public int Index;
        public int Intel;
        public int Psyche;
        public int Speed;
        public int Str;

        public MadnessSub(int index, int intel, int str, int speed, int psyche)
        {
            Index = index;
            Intel = intel;
            Str = str;
            Speed = speed;
            Psyche = psyche;
        }
    }
}