using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Helpers;

public class SecureRandom : IServiceSingleton
{
    // Deterministic mode for the seeded simulation A/B harness (SimulationRunner --seed).
    // When _seeded is non-null EVERY draw routes through it instead of the crypto RNG, so a
    // fixed seed reproduces a whole game bit-for-bit — the basis for common-random-numbers
    // A/B measurement (L1-probe vs L3-probe over identical games). Only safe when ONE game
    // runs at a time (the sim's seeded path guarantees this; the CheckIfReady tick is globally
    // non-reentrant, so single-game processing is fully serialized). Real Discord/web games
    // never call SetSeed → always cryptographic.
    private static Random _seeded;

    public static bool IsSeeded => _seeded != null;
    public static void SetSeed(int seed) => _seeded = new Random(seed);
    public static void ClearSeed() => _seeded = null;

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

        var seeded = _seeded;
        return seeded != null
            ? seeded.Next(minValue, maxValue)
            : RandomNumberGenerator.GetInt32(minValue, maxValue);
    }

    // Deterministic Guid when seeded, Guid.NewGuid() otherwise. Player identities are random
    // Guids; game logic that iterates Guid-keyed sets/dictionaries would order by their hash,
    // so unseeded Guids make even single-threaded runs nondeterministic. Seeding the identities
    // too closes that hole. Real games keep Guid.NewGuid().
    public static Guid NextGuid()
    {
        var seeded = _seeded;
        if (seeded == null) return Guid.NewGuid();
        var bytes = new byte[16];
        seeded.NextBytes(bytes);
        return new Guid(bytes);
    }

    // Fisher-Yates shuffle over the shared RNG: deterministic when seeded, crypto-random
    // otherwise. Used by the sim so seeded runs reproduce seating order (real games keep
    // their Guid.NewGuid() shuffle untouched).
    public static List<T> Shuffle<T>(IEnumerable<T> source)
    {
        var arr = source.ToList();
        for (var i = arr.Count - 1; i > 0; i--)
        {
            var j = Next(0, i);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }

        return arr;
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
