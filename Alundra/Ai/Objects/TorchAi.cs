#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Ai.Objects;

/// <summary>
/// E20.g2c2 phase A (docs/plan-e20-heros.md section 1.24.5, D-G2C2-1, D-G2C2-2): the native handlers C70 (<c>0x8007B7B0</c>, slot C, 33 words) and E20 (<c>0x8007F6C8</c>, slot E, 65 words) of the torch, the map-page sprite 137 (the type 393), the only sprite type that holds them.
/// <list type="bullet">
/// <item><description>C70, each tick of the status Normal: the twin of C23 (the same release table <c>0x800281DC</c>, <see cref="LiftableAi.TickCarried"/> with <see cref="LiftableAi.ReleaseFlagsPolicy.C70"/>), whose flags are <c>(flags | 0x34) &amp; ~0x80</c> (C23 raises <c>0x30</c>): nothing while the torch is not carried, the animation 0 while it is carried
/// without a code of throw, and the release for a code of the table.</description></item>
/// <item><description>E20, each tick of the status Deactivated (the torch that met something): once the animation 2 asked is at its end (<c>+0x88 == 2</c>, <c>+0xAC == 1</c>) the flame is born (<c>SpawnWarpEntity(self, page 0, type 12, X, Y, Z, direction)</c>, <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>) unless the torch
/// carries the mark <c>+0x274</c> (a drop of water touched it, <see cref="HasNoFlameMark"/>); the six native codes of the flame (<c>+0x70</c> to <c>+0x84</c>) are erased, its animation becomes 3 and its flags <c>(flags &amp; ~0x10) | 2</c>; then the torch is destroyed (<c>DestroyEntity(self, -1)</c>), flame or not. Before the end of the animation
/// the torch asks the animation 2, its flags become <c>(flags | 0x40) &amp; ~0x30</c> and, when the entity it touches (<c>+0x130</c>, <see cref="EntityScriptProxy.XCollisionEntity"/>) is a drop of water (type 391), the mark is raised (a byte store: only the first byte of <c>+0x274</c>).</description></item>
/// </list>
/// A table of entities that is full gives no flame (the binary writes through the null pointer; D-G2C2-2): the torch is destroyed all the same. The enrolment in production is the phase B (<see cref="NativeSpriteRegistry.CreateForProduction"/>).
/// </summary>
internal static class TorchAi
{
    /// <summary>The sprite type of the torch: 393 (<c>0x100 + 137</c>).</summary>
    internal const int SpriteType = 393;

    /// <summary>The sprite types the two keys are registered for.</summary>
    internal static readonly int[] SpriteTypes = { SpriteType };

    /// <summary>The program code of C70 in slot C.</summary>
    internal const int TickCode = 70;

    /// <summary>The program code of E20 in slot E.</summary>
    internal const int DeactivateCode = 20;

    /// <summary>The sprite type of the flame the torch leaves: 12 (the fire of the rod, without its native codes).</summary>
    internal const int FlameSpriteType = 12;

    /// <summary>The sprite type of the drop of water (the map-page sprite 135): the one whose touch puts out the torch.</summary>
    internal const int WaterDropSpriteType = 391;

    /// <summary>The animation of the burning torch, and the animation of the nappe of fire (the flame).</summary>
    private const uint BurningAnimation = 2;

    private const uint FlameAnimation = 3;

    /// <summary>The flags of the burning torch: <c>| 0x40</c>, <c>&amp; ~0x30</c>; the flags of the flame: <c>&amp; ~0x10</c>, <c>| 2</c>.</summary>
    private const uint BurningSetBits = 0x40;

    private const uint BurningClearBits = 0x30;

    private const uint FlameClearBits = 0x10;

    private const uint FlameSetBits = 2;

    /// <summary>The keys the class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, TickCode), (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Registers C70 and E20 for the type <see cref="SpriteType"/> only; the disposable takes the two keys off (for the tests that register them themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
    {
        var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity));
        try
        {
            var deactivate = registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Deactivate(entity, context));
            return new Registrations(tick, deactivate);
        }
        catch
        {
            tick.Dispose();
            throw;
        }
    }

    /// <summary>C70 (<c>0x8007B7B0</c>), one tick of the torch: nothing while it is not carried (<c>+0x28</c> null), otherwise the release of the objects carried, with the flags of C70 (see the class).</summary>
    internal static void Tick(EntityScriptProxy entity)
    {
        if (entity.PlatformEntity == null)
        {
            return;
        }

        LiftableAi.TickCarried(entity, LiftableAi.ReleaseFlagsPolicy.C70);
    }

    /// <summary>
    /// E20 (<c>0x8007F6C8</c>), one tick of the torch in the status Deactivated: see the class. The flame is born by <paramref name="context"/> (<see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/>, at the torch's position in the DLL's Z, with its direction), the destruction is the one of the world
    /// the torch belongs to (<see cref="EntityScriptProxy.ScriptHost"/>).
    /// </summary>
    internal static void Deactivate(EntityScriptProxy entity, IEntityWorldContext context)
    {
        if (entity.TargetAnimationId == BurningAnimation && entity.ForceResetAnimationFlag == 1)
        {
            if (!HasNoFlameMark(entity))
            {
                var flame = context.SpawnEntityBySpriteIndex(entity, false, FlameSpriteType, entity.PosX, entity.PosY, entity.PosZ, unchecked((int)entity.TargetDirection));
                if (flame != null)
                {
                    for (var i = 0; i < flame.SpriteProgramIndexes.Length; i++)
                    {
                        flame.SpriteProgramIndexes[i] = 0;
                    }

                    flame.TargetAnimationId = FlameAnimation;
                    flame.Flags = (flame.Flags & ~FlameClearBits) | FlameSetBits;
                }
            }

            entity.ScriptHost?.DestroyEntity(entity, -1);
            return;
        }

        entity.TargetAnimationId = BurningAnimation;
        entity.Flags = (entity.Flags | BurningSetBits) & ~BurningClearBits;
        if (entity.XCollisionEntity is { } contact && contact.SpriteType == WaterDropSpriteType)
        {
            entity.Bytes[0] = 1;
        }
    }

    /// <summary>True when the torch carries the mark of the drop of water (<c>+0x274</c>, its first byte, <see cref="EntityScriptProxy.Bytes"/>): it goes without leaving a flame.</summary>
    internal static bool HasNoFlameMark(EntityScriptProxy torch) => torch.Bytes[0] != 0;

    private sealed class Registrations : IDisposable
    {
        private readonly IDisposable _tick;
        private readonly IDisposable _deactivate;

        public Registrations(IDisposable tick, IDisposable deactivate)
        {
            _tick = tick;
            _deactivate = deactivate;
        }

        public void Dispose()
        {
            _tick.Dispose();
            _deactivate.Dispose();
        }
    }
}
