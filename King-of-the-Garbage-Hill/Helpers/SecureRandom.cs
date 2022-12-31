using System;
//using System.Security.Cryptography;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Helpers; 

public class SecureRandom : IServiceSingleton
{
    private Random _random;
    public Task InitializeAsync()
    {
        _random = new Random();
        return Task.CompletedTask;
    }

    public int Random(int minValue, int maxValue)
    {
        maxValue += 1;
        if (minValue == maxValue) return minValue;
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxValue)}");


        return _random.Next(minValue, maxValue);
        /*
        //Regular Random
        if (!isSecure)
        {
            return _random.Next(minValue, maxValue);
        }

        //Secure random
        var diff = (long)maxValue - minValue;
        var upperBound = uint.MaxValue / diff * diff;

        uint ui;
        do
        {
            var randomBytes = RandomNumberGenerator.GetBytes(555);
            ui = BitConverter.ToUInt32(randomBytes, 0);
        } while (ui >= upperBound);

        var result = (int)(minValue + ui % diff);
        return result;
        */
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

        var number = _random.Next(0, 101);
        return percentage >= number;
    }

}