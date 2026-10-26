#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Ai.Items;

/// <summary>
/// E20.h3a phase A (docs/plan-e20-heros.md section 1.30.2, D-H-6): the native handlers D30 (<c>0x8007E79C</c>, slot D) and E12 (<c>0x8007F27C</c>, slot E) of the bomb, the sprite type 2 of the global page. The bomb at rest is held by nothing: these two
/// handlers alone make it explode and vanish, without the hands of the hero (the start, the pilot, the cancel and C62 are E20.h3b's).
/// <list type="bullet">
/// <item><description>D30, each tick of the status Normal: when the entity that touches the bomb (<c>+0x224</c>, <see cref="EntityScriptProxy.TouchingEntity"/>) holds an attack (<c>+0x1C8</c>, <see cref="EntityScriptProxy.CurrentAttack"/>) whose attribute byte has the low nibble 4, 6 or 10, the bomb goes to the status Deactivated (3). The binary reads the byte at the pointer: it tests neither a frame box nor a zero byte (the nibbles 4, 6 and 10 are never zero), and no touching entity or no attack changes nothing.</description></item>
/// <item><description>E12, each tick of the status Deactivated: the bomb is destroyed with <c>DestroyEntity(e, -1)</c> when the animation asked (<c>+0x88</c>) is 1 and its end (<c>+0xAC</c>) is 1, or when the four corners stand on the ground property 4 (<c>+0x184 &amp; 4</c>, the hole). Otherwise the cut of the grass is called (<see cref="CheckAndTriggerTileEffect"/>, at every such tick, while the box of the explosion is on), the animation asked becomes 1 (the explosion) and the flags become <c>(flags | 0x40) &amp; ~0x30</c> (the animation end stays the only cause of deactivation).</description></item>
/// </list>
/// The registry keys are those of <see cref="Keys"/>; the enrolment in production is the phase B (<see cref="NativeSpriteRegistry.CreateForProduction"/>).
/// </summary>
internal static class BombAi
{
    /// <summary>The sprite type of the bomb: 2.</summary>
    internal static readonly int[] SpriteTypes = { 2 };

    /// <summary>The program code of D30 in slot D.</summary>
    internal const int TouchCode = 30;

    /// <summary>The program code of E12 in slot E.</summary>
    internal const int DeactivateCode = 12;

    /// <summary>The animation asked once the explosion is on.</summary>
    internal const uint ExplosionAnimation = 1;

    /// <summary>The attack nibbles that set the bomb off: 4, 6 and 10.</summary>
    private static bool SetsOff(int nibble) => nibble is 4 or 6 or 10;

    /// <summary>Test seam, per thread: when non-null, every call of <see cref="CheckAndTriggerTileEffect"/> appends the entity it was called for (the motif of <see cref="Alundra.Ai.Projectiles.WaveSwordAi.StubCallsForTests"/>).</summary>
    [ThreadStatic]
    internal static List<EntityScriptProxy>? StubCallsForTests;

    /// <summary>The keys this class registers (the pin of the registry follows them).</summary>
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramDTouch, TouchCode), (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Registers D30 and E12 for the type 2 only; the disposable takes the two keys off (for the tests that register them themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
    {
        var touch = registry.Register(ScriptHelper.ProgramDTouch, TouchCode, SpriteTypes, (entity, context, state) => Touch(entity));
        try
        {
            var deactivate = registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Deactivate(entity));
            return new Registrations(touch, deactivate);
        }
        catch
        {
            touch.Dispose();
            throw;
        }
    }

    /// <summary>D30 (<c>0x8007E79C</c>), one tick of the bomb in the status Normal: see the class.</summary>
    internal static void Touch(EntityScriptProxy entity)
    {
        if (entity.TouchingEntity?.GameplayProxy is EntityScriptProxy toucher && toucher.CurrentAttack is { } attack && SetsOff(attack.Attribute & 0xF))
        {
            entity.Status = EntityStatus.Deactivated;
        }
    }

    /// <summary>E12 (<c>0x8007F27C</c>), one tick of the bomb in the status Deactivated: see the class. The destruction is the one of the world the bomb belongs to (<see cref="EntityScriptProxy.ScriptHost"/>); an entity without a host does nothing.</summary>
    internal static void Deactivate(EntityScriptProxy entity)
    {
        if (entity.ScriptHost is not { } host)
        {
            return;
        }

        if ((entity.TargetAnimationId == ExplosionAnimation && entity.ForceResetAnimationFlag == 1) || (entity.CombinedVramFlagsAND & 4) != 0)
        {
            host.DestroyEntity(entity, -1);
            return;
        }

        CheckAndTriggerTileEffect(entity);
        entity.TargetAnimationId = ExplosionAnimation;
        entity.Flags = (entity.Flags | 0x40u) & ~0x30u;
        entity.ResyncControllerFromFlags();
    }

    /// <summary>
    /// <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) for the bomb: the journal of the call (<see cref="StubCallsForTests"/>), then the cut of the grass under the stored box (<see cref="Alundra.Cells.TileEffect.Trigger"/>, E20.e), the form of the call
    /// of the wave and of the charged arrow.
    /// </summary>
    internal static void CheckAndTriggerTileEffect(EntityScriptProxy entity)
    {
        StubCallsForTests?.Add(entity);
        Alundra.Cells.TileEffect.Trigger(entity);
    }

    /// <summary>The program code of C62 in slot C.</summary>
    internal const int TickCode = 62;

    /// <summary>E20.h3b T1 skeleton: the fuse of the bomb, <c>+0x274</c> (a signed word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3).</summary>
    internal static int Fuse(EntityScriptProxy bomb)
        => bomb.Bytes[0] | (bomb.Bytes[1] << 8) | (bomb.Bytes[2] << 16) | (bomb.Bytes[3] << 24);

    /// <summary>E20.h3b T1 skeleton: writes the fuse of the bomb (see <see cref="Fuse"/>).</summary>
    internal static void SetFuse(EntityScriptProxy bomb, int value)
    {
        bomb.Bytes[0] = (byte)value;
        bomb.Bytes[1] = (byte)(value >> 8);
        bomb.Bytes[2] = (byte)(value >> 16);
        bomb.Bytes[3] = (byte)(value >> 24);
    }

    /// <summary>E20.h3b T1 skeleton: the start of the bomb, <c>0x8003453C</c>.</summary>
    internal static int Start(EntityScriptProxy hero) => throw new NotImplementedException("E20.h3b T1: the start of the bomb");

    /// <summary>E20.h3b T1 skeleton: the driver of the bomb, <c>0x800352C4</c>.</summary>
    internal static int Drive(EntityScriptProxy hero) => throw new NotImplementedException("E20.h3b T1: the driver of the bomb");

    /// <summary>E20.h3b T1 skeleton: the cancellation of the bomb, <c>0x80034B54</c>.</summary>
    internal static void Cancel(EntityScriptProxy hero) => throw new NotImplementedException("E20.h3b T1: the cancellation of the bomb");

    /// <summary>E20.h3b T1 skeleton: C62, <c>0x8007A978</c>.</summary>
    internal static void Tick(EntityScriptProxy bomb, IEntityWorldContext context, Func<EntityScriptProxy, uint[]>? readCorners = null)
        => throw new NotImplementedException("E20.h3b T1: C62");

    private sealed class Registrations : IDisposable
    {
        private readonly IDisposable _touch;
        private readonly IDisposable _deactivate;

        public Registrations(IDisposable touch, IDisposable deactivate)
        {
            _touch = touch;
            _deactivate = deactivate;
        }

        public void Dispose()
        {
            _touch.Dispose();
            _deactivate.Dispose();
        }
    }
}
