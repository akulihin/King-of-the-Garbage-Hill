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

    public int Random(int minValue, int maxExclusiveValue)
    {
        if (minValue == maxExclusiveValue) return minValue;
        if (minValue > maxExclusiveValue)
            throw new ArgumentOutOfRangeException($"{nameof(minValue)} must be lower than {nameof(maxExclusiveValue)}");

        var diff = (long)maxExclusiveValue - minValue;
        var upperBound = uint.MaxValue / diff * diff;

        uint ui;
        do
        {
            ui = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(24), 0);
        } while (ui >= upperBound);

        return (int)(minValue + ui % diff);
    }
}