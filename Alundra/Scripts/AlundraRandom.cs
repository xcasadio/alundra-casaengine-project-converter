#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// The original engine's ONE shared random stream (D7, docs/plan-e9d-mode-cellulaire.md) -
/// transcribed verbatim from <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Random.cs</c>. Not
/// present anywhere else in the DLL before this slice - a <c>CellularCellType.FallRespawn</c> cell's
/// respawn abscissa draws from THIS stream, not a private generator, because the original game shares
/// one seed across every random draw (dialogue, AI, combat, and this).
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
