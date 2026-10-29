#nullable enable
using Alundra.Entities;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h4d2 (docs/plan-e20-heros.md section 1.31.6, D-H4D2-3, D-E20-55): the 32-bit accessors of the tomes to the word <c>+0x274</c> of an entity. Skeleton of the tests-first commit (the real body comes with the code).
/// </summary>
internal static class TomeWords
{
    /// <summary><c>+0x274</c> of <paramref name="entity"/>.</summary>
    internal static int Read274(EntityScriptProxy entity) => entity.Bytes[0];

    /// <summary>Writes <c>+0x274</c> of <paramref name="entity"/>.</summary>
    internal static void Write274(EntityScriptProxy entity, int value) => entity.Bytes[0] = (byte)value;
}
