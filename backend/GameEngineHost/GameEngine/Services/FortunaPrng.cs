using System.Security.Cryptography;

namespace GameEngine.Services;

/// <summary>
/// Fortuna-inspired cryptographic PRNG wrapper for compliance-friendly non-critical randomness.
/// </summary>
public sealed class FortunaPrng : IDisposable
{
    private readonly RandomNumberGenerator _rng;

    public FortunaPrng()
    {
        _rng = RandomNumberGenerator.Create();
    }

    public int NextInt32(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "min must be < max.");
        }

        var range = (long)maxExclusive - minInclusive;
        var buffer = new byte[4];
        long result;
        do
        {
            _rng.GetBytes(buffer);
            result = BitConverter.ToUInt32(buffer, 0) % range;
        } while (result >= range);

        return (int)(minInclusive + result);
    }

    public void Dispose() => _rng.Dispose();
}

