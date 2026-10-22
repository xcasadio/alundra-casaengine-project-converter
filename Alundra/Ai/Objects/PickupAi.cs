#nullable enable
using Alundra.Entities;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.e0c (docs/plan-e20-heros.md section 1.26): the native handlers of the pickup object - C255 (<c>0x8007C174</c>) and A255 (<c>0x80061BD4</c>) of the binary, phases B and D - and, since T1, the 32-bit words they keep on the
/// entity. The proxy holds those words in pieces (<c>+0x274</c> on the four bytes of <see cref="EntityScriptProxy.Bytes"/>, <c>+0x280</c> and <c>+0x284</c> on four halfwords of <see cref="EntityScriptProxy.AIValues"/>), and its
/// own file is not changed (D-E20-55, D-E0C-13: the 32-bit accessors are the pickup object's alone; the tomes and E14 make theirs). The words are little-endian, as the binary stores them: <c>+0x274</c> is 0 for an object placed on a
/// map, 1 for a dropped one and 2 for the chest (not ported); <c>+0x280</c> is the game flag the pickup raises (the container's flag, or -1 for a placed object); <c>+0x284</c> is the speed of the bounce.
/// </summary>
internal static class PickupAi
{
    /// <summary><c>+0x274</c>: the kind of the object (0 placed, 1 dropped, 2 chest).</summary>
    internal static uint ReadWord274(EntityScriptProxy entity)
        => (uint)(entity.Bytes[0] | (entity.Bytes[1] << 8) | (entity.Bytes[2] << 16) | (entity.Bytes[3] << 24));

    internal static void WriteWord274(EntityScriptProxy entity, uint value)
    {
        entity.Bytes[0] = (byte)value;
        entity.Bytes[1] = (byte)(value >> 8);
        entity.Bytes[2] = (byte)(value >> 16);
        entity.Bytes[3] = (byte)(value >> 24);
    }

    /// <summary><c>+0x280</c>: <see cref="EntityScriptProxy.AIValues"/> 0 (low half) and 1 (high half).</summary>
    internal static uint ReadWord280(EntityScriptProxy entity) => ReadWord(entity, 0);

    internal static void WriteWord280(EntityScriptProxy entity, uint value) => WriteWord(entity, 0, value);

    /// <summary><c>+0x284</c>: <see cref="EntityScriptProxy.AIValues"/> 2 (low half) and 3 (high half).</summary>
    internal static uint ReadWord284(EntityScriptProxy entity) => ReadWord(entity, 2);

    internal static void WriteWord284(EntityScriptProxy entity, uint value) => WriteWord(entity, 2, value);

    private static uint ReadWord(EntityScriptProxy entity, int index)
        => (ushort)entity.AIValues[index] | ((uint)(ushort)entity.AIValues[index + 1] << 16);

    private static void WriteWord(EntityScriptProxy entity, int index, uint value)
    {
        entity.AIValues[index] = unchecked((short)(value & 0xFFFF));
        entity.AIValues[index + 1] = unchecked((short)(value >> 16));
    }
}
