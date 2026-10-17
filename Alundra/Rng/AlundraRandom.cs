#nullable enable

using Alundra.Scripts;

namespace Alundra.Rng;

/// <summary>
/// The game's shared random stream (D7, docs/plan-e9d-mode-cellulaire.md) - transcribed verbatim from
/// <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Random.cs</c>. It is NOT the stream a
/// <c>CellularCellType.FallRespawn</c> cell's respawn abscissa draws from: the binary draws that from the C
/// library <c>rand()</c> (<see cref="AlundraLibcRandom"/>, E19.m2, D-E19-66, ADR-0032), which replaced the
/// choice D7 made. D7 and the decompilation were wrong on that point; the rest of the game's draws (dialogue,
/// AI, combat) still use this stream.
///
/// <see cref="Next"/> wraps the multiplication in 64 bits (matching the original's own <c>ulong</c>
/// arithmetic) and returns only the LOW 32 bits, held in a <see cref="ulong"/> so callers reading it as
/// <see cref="uint"/> get the same value the original's own truncating cast produced.
///
/// <see cref="Reset"/> is ported but deliberately never called from anywhere in this slice - the
/// original calls it once, from <c>GameEngine.InitializeEngine</c> (game/engine startup, not a world
/// load), which is out of this slice's scope; wiring it is a separate decision.
/// </summary>
public static class AlundraRandom
{
    /// <summary>The value of the word in the image of the executable (<c>0x80098708</c>, octet <c>0x78F08</c> of the file), the one <see cref="Reset"/> restores.</summary>
    internal const ulong InitialSeed = 0xB017C93D;

    private static ulong _seed = InitialSeed;

    /// <summary>
    /// The state of the stream (64 bits, only the low 32 count). A property since E20.e0b1 (D-E0B-7, source-compatible: nothing passes it by <c>ref</c> or reflects on it) so that the
    /// measuring sink (<see cref="AlundraRandomShadow"/>) sees the assignments and the reads; <see cref="Next"/> works on the private field alone.
    /// </summary>
    public static ulong RandomSeed
    {
        get
        {
            AlundraRandomShadow.OnSeedRead(_seed);
            return _seed;
        }
        set
        {
            _seed = value;
            AlundraRandomShadow.OnEpoch(value);
        }
    }

    public static void Reset()
    {
        _seed = InitialSeed;
        AlundraRandomShadow.OnEpoch(InitialSeed);
    }

    public static ulong Next()
    {
        _seed = _seed * 0x7d2b89dd + 0xe06a02e7;
        AlundraRandomShadow.OnDraw(_seed);
        return (uint)_seed;
    }

    /// <summary>
    /// E20.e0b1: the advance of the binary's frame function (<c>0x8002BD0C-0x8002BD34</c>), one step of the stream per logic tick (the same expression as <see cref="Next"/>), result never read.
    /// It also feeds the virtual word of the measuring sink (a no-op while the sink is inactive).
    /// </summary>
    public static void AdvanceFrame()
    {
        _seed = _seed * 0x7d2b89dd + 0xe06a02e7;
        AlundraRandomShadow.OnVirtualAdvance();
    }
}
