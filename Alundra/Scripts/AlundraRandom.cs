#nullable enable

namespace Alundra.Scripts;

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
    public static ulong RandomSeed = 0xB017C93D;

    public static void Reset()
    {
        RandomSeed = 0xB017C93D;
    }

    public static ulong Next()
    {
        RandomSeed = RandomSeed * 0x7d2b89dd + 0xe06a02e7;
        return (uint)RandomSeed;
    }
}
