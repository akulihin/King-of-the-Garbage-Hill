using System;

namespace King_of_the_Garbage_Hill.Game.Classes;

public class PredictClass
{
    public string CharacterName;
    public Guid PlayerId;

    public PredictClass(string characterName, Guid playerId)
    {
        CharacterName = characterName;
        PlayerId = playerId;
    }
}