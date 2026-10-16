#nullable enable
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>
/// E20.f3c (docs/plan-e20-heros.md section 1.19.3, D-F3-6): the native handler C61 (<c>0x8007A958</c>) of the sword wave, the sprite types 27 (steel), 28 (demon) and 29 (holy) of the global page. C61 calls
/// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) and nothing else; the death of the wave (E10, <c>0x8007F23C</c>, the body of E1) is handled in line by the runner (<see cref="AlundraEventProgramRunner.IsInlineDeactivateCode"/>,
/// D-F3-5). The program A0 of these types only writes <c>AIValues[0]</c>, which no code of the wave reads: it is not registered (D-F3-7, section 1.20).
/// </summary>
internal static class AlundraWaveSwordAi
{
    /// <summary>The sprite types of the wave: steel, demon, holy sword.</summary>
    internal static readonly int[] SpriteTypes = { 27, 28, 29 };

    /// <summary>The program code of C61 in slot C.</summary>
    internal const int TickCode = 61;

    /// <summary>The program code of E10 in slot E (handled in line by the runner).</summary>
    internal const int DeactivateCode = 10;

    /// <summary>Test seam, per thread: when non-null, every call of the named stub <see cref="CheckAndTriggerTileEffect"/> appends the entity it was called for (the motif of <see cref="AlundraPlayerAnimationEffects.StubCallsForTests"/>).</summary>
    [System.ThreadStatic]
    internal static List<AlundraEntityScriptProxy>? StubCallsForTests;

    /// <summary>Registers C61 for the types 27 to 29.</summary>
    internal static void Register(AlundraNativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity));

    /// <summary>C61 (<c>0x8007A958</c>), one tick of the wave: the call of the cutting of the grass, a named stub until E20.e.</summary>
    internal static void Tick(AlundraEntityScriptProxy entity) => CheckAndTriggerTileEffect(entity);

    /// <summary>
    /// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>), named stub. The binary cuts the grass cells under the entity's box and draws at least one word of the generator per cell cut (it may drop an item); the cutting is E20.e
    /// (Q-F3-2). Until then the stub cuts nothing and draws nothing, so the generator drifts after any wave over grass (O-F3-1).
    /// </summary>
    internal static void CheckAndTriggerTileEffect(AlundraEntityScriptProxy entity) => StubCallsForTests?.Add(entity);
}
