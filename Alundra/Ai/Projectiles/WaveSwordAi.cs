#nullable enable
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Entities;

namespace Alundra.Ai.Projectiles;

/// <summary>
/// E20.f3c (docs/plan-e20-heros.md section 1.19.3, D-F3-6): the native handler C61 (<c>0x8007A958</c>) of the sword wave, the sprite types 27 (steel), 28 (demon) and 29 (holy) of the global page. C61 calls
/// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>, the cut of the grass, <see cref="Alundra.Cells.TileEffect"/>, E20.e) and nothing else; the death of the wave (E10, <c>0x8007F23C</c>, the body of E1) is handled in line by the runner (<see cref="EventProgramRunner.IsInlineDeactivateCode"/>,
/// D-F3-5). The program A0 of these types only writes <c>AIValues[0]</c>, which no code of the wave reads: it is not registered (D-F3-7, section 1.20).
/// </summary>
internal static class WaveSwordAi
{
    /// <summary>The sprite types of the wave: steel, demon, holy sword.</summary>
    internal static readonly int[] SpriteTypes = { 27, 28, 29 };

    /// <summary>The program code of C61 in slot C.</summary>
    internal const int TickCode = 61;

    /// <summary>The program code of E10 in slot E (handled in line by the runner).</summary>
    internal const int DeactivateCode = 10;

    /// <summary>Test seam, per thread: when non-null, every call of the named stub <see cref="CheckAndTriggerTileEffect"/> appends the entity it was called for (the motif of <see cref="PlayerAnimationEffects.StubCallsForTests"/>).</summary>
    [System.ThreadStatic]
    internal static List<EntityScriptProxy>? StubCallsForTests;

    /// <summary>Registers C61 for the types 27 to 29.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity));

    /// <summary>C61 (<c>0x8007A958</c>), one tick of the wave: the call of the cutting of the grass.</summary>
    internal static void Tick(EntityScriptProxy entity) => CheckAndTriggerTileEffect(entity);

    /// <summary>
    /// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) for the wave: the journal of the call (<see cref="StubCallsForTests"/>, kept from the time it was a stub), then the port (E20.e, docs/plan-e20-heros.md section 1.27,
    /// <see cref="Alundra.Cells.TileEffect.Trigger"/>): the bushes under the box of the wave are cut, with the effect, the loot and the sound of each cut. The charged arrow (E20.f4) and the bomb (E20.h3) make this same call.
    /// </summary>
    internal static void CheckAndTriggerTileEffect(EntityScriptProxy entity)
    {
        StubCallsForTests?.Add(entity);
        Alundra.Cells.TileEffect.Trigger(entity);
    }
}
