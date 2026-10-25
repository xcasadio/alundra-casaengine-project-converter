#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Rendering.Effects;

namespace Alundra.Ai.Projectiles;

/// <summary>
/// E20.f6b phase A (docs/plan-e20-heros.md section 1.22.2, D-F6-3 to D-F6-5): the native handlers C60 (<c>0x8007A8A0</c>, slot C, code 60 of the master table <c>0x80098F4C</c>) and E9 (<c>0x8007EF50</c>, slot E, code 9) of the fire and ice balls, the sprite types 10 to 15 of the global page (11 and 12 fire,
/// 14 and 15 ice; 10 and 13 have no weapon sheet but share the codes). The A0 of the types only writes <c>AIValues[0]</c>, which no code of the balls reads: it is not registered (D-F3-7).
/// <list type="bullet">
/// <item><description>C60, at each tick of the status Normal: when <c>(+0x1C &amp; 3) == 0</c>, one trail effect (17 for the fire, 18 for the ice) at the ball's position of the end of the previous tick, with the speed opposite to the ball's forces (<see cref="NativeEffects.CreateBallTrail"/>). No number is drawn.</description></item>
/// <item><description>E9, at each tick of the status Deactivated, once the ball has met something. The FIRST call (the animation asked, <c>+0x88</c>, is not 1): if the slope <c>+0x18C</c> is 1 or 4 (deep water) the ball is destroyed with the effect 6 (<c>DestroyEntity(e, 6)</c>) and nothing else is done; otherwise
/// five effects with two draws each (<see cref="NativeEffects.CreateBallExplosion"/>), then <c>+0x88 = 1</c> and the flags <c>(flags | 0x40) &amp; ~0x30</c> (the animation end stays the only cause of deactivation). The LATER calls (<c>+0x88 == 1</c>): the same slope test destroys the ball with the effect 6 and, WITHOUT returning,
/// the code goes on; then <c>+0xAC == 1</c> (the end of the animation) destroys the ball with <c>-1</c> and returns, otherwise every 8 ticks (<c>+0x1C &amp; 7 == 0</c>) a puff (<see cref="NativeEffects.CreateBallPuff"/>, two draws). A ball on deep water at a multiple of 8 therefore dies AND puffs in the same tick.</description></item>
/// </list>
/// The effects are queued on the stage (<see cref="EffectStage.Emit"/>) with the values captured at the call; the numbers are drawn when the stage applies its queue (D-E0B-3(a)). The registry keys are those of <see cref="Keys"/>; the enrolment in production is the phase B (<see cref="NativeSpriteRegistry.CreateForProduction"/>).
/// </summary>
internal static class BallAi
{
    /// <summary>The sprite types of the balls: 10 to 15 (11 fire, 12 charged fire, 14 ice, 15 charged ice; 10 and 13 have no weapon sheet).</summary>
    internal static readonly int[] SpriteTypes = { 10, 11, 12, 13, 14, 15 };

    /// <summary>The program code of C60 in slot C.</summary>
    internal const int TickCode = 60;

    /// <summary>The program code of E9 in slot E.</summary>
    internal const int DeactivateCode = 9;

    /// <summary>The effect that <c>DestroyEntity</c> attaches to a ball dying on deep water (the effect of the water).</summary>
    internal const int WaterEffect = 6;

    /// <summary>The value of the animation asked (<c>+0x88</c>) once the explosion is made.</summary>
    internal const uint ExplodedAnimation = 1;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, TickCode), (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Registers C60 and E9 for the types 10 to 15 only; the disposable takes the two keys off (for the tests that register them themselves when the registry of the runner does not hold them yet).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
    {
        var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Trail(entity));
        try
        {
            var deactivate = registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Explode(entity));
            return new Registrations(tick, deactivate);
        }
        catch
        {
            tick.Dispose();
            throw;
        }
    }

    /// <summary>The binary's test <c>(type - 11) &lt; 2</c> (unsigned): the types 11 and 12 are fire, every other type of the class is ice.</summary>
    private static bool IsFire(EntityScriptProxy entity) => (uint)(entity.SpriteType - 11) < 2u;

    /// <summary>
    /// C60 (<c>0x8007A8A0</c>), one tick of a ball in the status Normal: when the counter <c>+0x1C</c> is a multiple of four, the trail at the position of the end of the previous tick (Z in the binary's frame, <c>PosZ + 1</c>, ADR-0026) with the speed opposite to the forces; the values are captured now and the effect is created
    /// when the stage applies its queue. Without a world context nothing is made.
    /// </summary>
    internal static void Trail(EntityScriptProxy entity)
    {
        if ((entity.FrameCounter & 3) != 0 || entity.ScriptHost is not IEntityWorldContext world)
        {
            return;
        }

        var fire = IsFire(entity);
        var (x, y, z) = (entity.PosX, entity.PosY, unchecked(entity.PosZ + 1));
        var (forceX, forceY, forceZ) = (entity.ForceX, entity.ForceY, entity.ForceZ);
        world.EffectStage?.Emit(reservoir => NativeEffects.CreateBallTrail(reservoir, fire, x, y, z, forceX, forceY, forceZ));
    }

    /// <summary>
    /// E9 (<c>0x8007EF50</c>), one tick of a ball in the status Deactivated: the explosion at the first call, then the puffs and the end (see the class). The destruction is the one of the world the ball belongs to (<see cref="EntityScriptProxy.ScriptHost"/>, as in <see cref="LiftableAi.Break"/>);
    /// the effects need a world context with a stage (none: the destructions and the writes are made, no effect).
    /// </summary>
    internal static void Explode(EntityScriptProxy entity)
    {
        if (entity.ScriptHost is not { } host)
        {
            return;
        }

        var world = host as IEntityWorldContext;
        var onWater = entity.Slope_18c is 1 or 4;
        var fire = IsFire(entity);
        var (x, y, z) = (entity.PosX, entity.PosY, unchecked(entity.PosZ + 1));
        if (entity.TargetAnimationId != ExplodedAnimation)
        {
            if (onWater)
            {
                host.DestroyEntity(entity, WaterEffect);
                return;
            }

            world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallExplosion(reservoir, fire, x, y, z));
            entity.TargetAnimationId = ExplodedAnimation;
            entity.Flags = (entity.Flags | 0x40u) & ~0x30u;
            entity.ResyncControllerFromFlags();
            return;
        }

        if (onWater)
        {
            host.DestroyEntity(entity, WaterEffect);
        }

        if (entity.ForceResetAnimationFlag == 1)
        {
            host.DestroyEntity(entity, -1);
            return;
        }

        if ((entity.FrameCounter & 7) != 0)
        {
            return;
        }

        world?.EffectStage?.Emit(reservoir => NativeEffects.CreateBallPuff(reservoir, fire, x, y, z));
    }

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
