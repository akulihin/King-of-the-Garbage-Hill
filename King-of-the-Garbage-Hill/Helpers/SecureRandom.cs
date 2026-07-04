using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Helpers;

public class SecureRandom : IServiceSingleton
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    //Inclusive of maxValue. The single RNG for the whole game (m21):
    //RandomNumberGenerator is cryptographic and thread-safe, unlike the
    //previous per-service System.Random instance.
    public static int Next(int minValue, int maxValue)
    {
        maxValue += 1;
        if (minValue == maxValue) return minValue;
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxValue)}");

        return RandomNumberGenerator.GetInt32(minValue, maxValue);
    }

    public int Random(int minValue, int maxValue)
    {
        return Next(minValue, maxValue);
    }


    //Usage example Luck(20%)
    //Usage example Luck(1, 5) this also means 20%
    public bool Luck(decimal percentage, decimal range = 0)
    {
        if (range > 0)
        {
            var result = percentage / range * 100 + (decimal)0.1;
            percentage = (int)Math.Round(result);
        }

        var number = Next(0, 100);
        return percentage >= number;
    }

}
