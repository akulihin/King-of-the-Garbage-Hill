using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Helpers;

public class SecureRandom : IServiceTransient
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public int Random(int minValue, int maxValue)
    {
        maxValue += 1;
        if (minValue == maxValue) return minValue;
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxValue)}");

        var diff = (long)maxValue - minValue;
        var upperBound = uint.MaxValue / diff * diff;

        uint ui;
        do
        {
            ui = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(24), 0);
        } while (ui >= upperBound);

        return (int)(minValue + ui % diff);
    }
}