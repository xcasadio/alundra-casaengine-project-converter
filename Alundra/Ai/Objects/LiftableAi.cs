#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g1a (docs/plan-e20-heros.md section 1.24.1, D-G-9, D-G-12): the program C23 (<c>0x8006B848</c>, slot C, code 23 of the master table <c>0x80098F4C</c>) of the crates, the jars and the other liftable objects of the seven sprite types of the
/// export (<see cref="SpriteTypes"/>). It only decides WHEN the object leaves the hands; its flight is the generic motion of the entities:
/// <list type="bullet">
/// <item><description>nothing while the object is not carried (<c>+0x28</c>, <see cref="EntityScriptProxy.PlatformEntity"/>, null);</description></item>
/// <item><description>carried without a code of throw (<c>+0x24</c>, <see cref="EntityScriptProxy.Flags2"/>, 0): the animation 0, the links kept;</description></item>
/// <item><description>carried with a code of throw (1, 2, 3 or -1 through the table <c>0x80027400</c>, <see cref="ReleaseAnimation"/>): the animation of the flight, the code back to 0, the two links cleared (the carrier's <c>+0x2C</c> and its own
/// <c>+0x28</c>), the riding relation cleared (D-G-12), and the flags <see cref="ReleasedFlags"/>.</description></item>
/// </list>
/// The code -1 is written by mode 2 of <c>UpdatePlayerCarriedEntity</c> (a wound, deep water, a death), the codes 1 to 3 by the throw of the hero (g2a). E2, the destruction on arrival, is g2b's. Not registered by <see cref="NativeSpriteRegistry.CreateForProduction"/> in
/// phase A: <see cref="Register"/> is called by the tests, and by the production registry from phase C.
/// </summary>
internal static class LiftableAi
{
    /// <summary>The program code of C23 in slot C.</summary>
    internal const int Code = 23;

    /// <summary>
    /// The sprite types whose header holds C23 in the export (D-G-8): the map-page sprites 35, 36, 38, 41, 48 and 182 (<c>0x100 +</c> the index: 291, 292, 294, 297, 304 and 438) and the sprite 234 of the global page (the very heavy stone). All seven also hold E2
    /// at the slot E. Guarded against the export by the tests.
    /// </summary>
    internal static readonly IReadOnlyList<int> SpriteTypes = new[] { 234, 291, 292, 294, 297, 304, 438 };

    /// <summary>The keys the class registers, for the pins of the production registry: C23, for the seven types.</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, Code) };

    /// <summary>The bits the release raises (<c>| 0x30</c>) and clears (<c>&amp; ~0x80</c>) in the flags of the object.</summary>
    private const uint ReleaseSetBits = 0x30;

    private const uint ReleaseClearBits = 0x80;

    /// <summary>Registers C23 for the seven types of <see cref="SpriteTypes"/> only.</summary>
    internal static void Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramCTick, Code, SpriteTypes, (entity, context, state) => Tick(entity, context));

    /// <summary>
    /// The table <c>0x80027400</c> read by the code of throw: 1 gives the animation 3, 2 the animation 1, 3 the animation 4, -1 the animation 3. Any other code (the binary reads the words around the table) is written by nothing: null, and
    /// <see cref="Tick"/> does not release.
    /// </summary>
    internal static uint? ReleaseAnimation(int throwKind) => throwKind switch
    {
        1 => 3u,
        2 => 1u,
        3 => 4u,
        -1 => 3u,
        _ => null,
    };

    /// <summary>The flags of the object once let go: <c>(flags | 0x30) &amp; ~0x80</c> (the crate <c>0x3323E4</c> becomes <c>0x332374</c>).</summary>
    internal static uint ReleasedFlags(uint flags) => (flags | ReleaseSetBits) & ~ReleaseClearBits;

    /// <summary>One tick of C23 for <paramref name="entity"/> (see the class).</summary>
    internal static void Tick(EntityScriptProxy entity, IEntityWorldContext context)
    {
        if (entity.PlatformEntity == null)
        {
            return;
        }

        if (entity.Flags2 == 0)
        {
            entity.TargetAnimationId = 0;
            return;
        }

        if (ReleaseAnimation(entity.Flags2) is not { } animation)
        {
            return;
        }

        entity.TargetAnimationId = animation;
        entity.Flags2 = 0;

        // the carrier's link (the binary writes 0 there without looking at it); a carrier without a proxy has none to clear.
        if (entity.PlatformEntity.GameplayProxy is EntityScriptProxy carrier)
        {
            carrier.CarriedEntity = null;
        }

        entity.PlatformEntity = null;
        entity.RidingEntity = null;
        entity.Flags = ReleasedFlags(entity.Flags);
    }
}
