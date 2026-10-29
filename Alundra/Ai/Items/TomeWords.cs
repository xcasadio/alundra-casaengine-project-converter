#nullable enable
using Alundra.Entities;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4d2 (docs/plan-e20-heros.md section 1.31.6, D-H4D2-3, D-E20-55): the 32-bit accessors of the tomes to the word <c>+0x274</c> of an entity, a signed word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3. They are proper to the tomes, on the model of
/// <see cref="Objects.PickupAi.ReadWord274"/> (D-E0C-13) and of the counter of the pillar (D-G5-2): <see cref="EntityScriptProxy"/> carries no accessor of its own (E14 makes its own). The ball of the fire tome 1 uses it for the wait counter, and the other tomes (the wind tome 1, h4f1) reuse it.
/// In production the values stay within 0 to 16 (the vectors of the oracle go beyond: the high bytes and the negative values are replayed).
/// </summary>
internal static class TomeWords
{
    /// <summary><c>+0x274</c> of <paramref name="entity"/>: the four bytes <see cref="EntityScriptProxy.Bytes"/> 0 to 3, the byte 0 lowest.</summary>
    internal static int Read274(EntityScriptProxy entity)
        => entity.Bytes[0] | (entity.Bytes[1] << 8) | (entity.Bytes[2] << 16) | (entity.Bytes[3] << 24);

    /// <summary>Writes the four bytes of <c>+0x274</c> of <paramref name="entity"/> (the byte 0 lowest); the bytes 4 and above are not touched.</summary>
    internal static void Write274(EntityScriptProxy entity, int value)
    {
        entity.Bytes[0] = (byte)value;
        entity.Bytes[1] = (byte)(value >> 8);
        entity.Bytes[2] = (byte)(value >> 16);
        entity.Bytes[3] = (byte)(value >> 24);
    }
}
