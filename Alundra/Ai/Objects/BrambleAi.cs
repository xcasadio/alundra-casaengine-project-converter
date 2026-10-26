#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Entities;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g2c2 phase A (docs/plan-e20-heros.md section 1.24.5, D-G2C2-3): the native handler D34 (<c>0x8007E8AC</c>, slot D, 17 words) of the brambles, the sprite types 238 and 239 of the global page. A bramble is an obstacle until fire touches it: when the entity that touches it (<c>+0x224</c>,
/// <see cref="EntityScriptProxy.TouchingEntity"/>) holds an attack (<c>+0x1C8</c>, <see cref="EntityScriptProxy.CurrentAttack"/>) whose attribute byte has the low nibble 6 (fire), the bramble asks the animation 1 (it burns) and its flags become <c>(flags | 0x46) &amp; ~0x80</c> (it stops blocking). The
/// binary reads the byte at the pointer: it tests neither the high nibble nor a frame box. The sheets of the brambles answer only to the attribute 6; the fire spreads from bramble to bramble by their data, the end of the animation and the destruction are the generic E0. The enrolment in production is the phase B
/// (<see cref="NativeSpriteRegistry.CreateForProduction"/>).
/// </summary>
internal static class BrambleAi
{
    /// <summary>The sprite types of the brambles: 238 and 239 of the global page.</summary>
    internal static readonly int[] SpriteTypes = { 238, 239 };

    /// <summary>The program code of D34 in slot D.</summary>
    internal const int TouchCode = 34;

    /// <summary>The low nibble of the attribute of an attack of fire.</summary>
    internal const int FireAttribute = 6;

    /// <summary>The animation of the bramble that burns.</summary>
    internal const uint BurningAnimation = 1;

    private const uint BurningSetBits = 0x46;

    private const uint BurningClearBits = 0x80;

    /// <summary>The keys the class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramDTouch, TouchCode) };

    /// <summary>Registers D34 for the types 238 and 239 only; the disposable takes the key off.</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
        => registry.Register(ScriptHelper.ProgramDTouch, TouchCode, SpriteTypes, (entity, context, state) => Touch(entity));

    /// <summary>D34 (<c>0x8007E8AC</c>), one tick of a bramble that is touched: see the class. No touching entity, or none with an attack, changes nothing.</summary>
    internal static void Touch(EntityScriptProxy entity)
    {
        if (entity.TouchingEntity?.GameplayProxy is EntityScriptProxy toucher && toucher.CurrentAttack is { } attack && (attack.Attribute & 0xF) == FireAttribute)
        {
            entity.TargetAnimationId = BurningAnimation;
            entity.Flags = (entity.Flags | BurningSetBits) & ~BurningClearBits;
        }
    }
}
