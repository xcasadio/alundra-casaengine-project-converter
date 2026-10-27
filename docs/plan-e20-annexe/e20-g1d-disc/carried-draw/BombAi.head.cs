#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Physics;
using Alundra.Rng;

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
    internal static readonly IReadOnlyList<(int Slot, int Code)> Keys = new[] { (ScriptHelper.ProgramCTick, TickCode), (ScriptHelper.ProgramDTouch, TouchCode), (ScriptHelper.ProgramEDeactivate, DeactivateCode) };

    /// <summary>Registers C62 (E20.h3b), D30 and E12 for the type 2 only; the disposable takes the two keys off (for the tests that register them themselves on a registry of their own).</summary>
    internal static IDisposable Register(NativeSpriteRegistry registry)
    {
        var tick = registry.Register(ScriptHelper.ProgramCTick, TickCode, SpriteTypes, (entity, context, state) => Tick(entity, context));
        try
        {
            var touch = registry.Register(ScriptHelper.ProgramDTouch, TouchCode, SpriteTypes, (entity, context, state) => Touch(entity));
            try
            {
                var deactivate = registry.Register(ScriptHelper.ProgramEDeactivate, DeactivateCode, SpriteTypes, (entity, context, state) => Deactivate(entity));
                return new Registrations(tick, touch, deactivate);
            }
            catch
            {
                touch.Dispose();
                throw;
            }
        }
        catch
        {
            tick.Dispose();
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

    /// <summary>The sound of every refusal of the start (<c>0x8003453C</c>, the same as the frame's).</summary>
    private const int RefusalSound = 3;

    /// <summary>The sound of the landing or the touch of a thrown bomb (<c>0x18</c>).</summary>
    private const int LandingSound = 0x18;

    /// <summary>The ground slope of the deep water: the start is refused there and the driver cancels the item.</summary>
    private const int DeepWaterSlope = 4;

    /// <summary>At most this many entities of the type 2 are in the world: the start refuses the one that would make more.</summary>
    private const int BombLimit = 3;

    /// <summary>The fuse a bomb is given right after its birth (<c>+0x274</c>): 240, so 241 ticks of C62 before the status 3.</summary>
    private const int StartFuse = 0xF0;

    /// <summary>C62 makes a spark while the fuse is under this value and a multiple of 8.</summary>
    private const int SparkWindow = 0x3C;

    private const int SparkEffect = 9;

    /// <summary>The height of a spark above the bomb: 8 px.</summary>
    private const int SparkLift = 0x80000;

    /// <summary>The effect of the smoke of a bomb put out (<c>0x80034B54</c>): the global effect 12.</summary>
    private const int SmokeEffect = 12;

    /// <summary>The half turn of the direction a bomb makes against a wall in the air (<c>+0x8C</c> + 0x10, modulo 0x20).</summary>
    private const uint BounceDirectionStep = 0x10;

    /// <summary>The animation a bomb in the hands asks for, and the one it asks for once let go (the flight).</summary>
    private const uint HeldAnimation = 0;

    private const uint FlightAnimation = 2;

    /// <summary>The fuse of the bomb, <c>+0x274</c> (a signed word held little-endian on <see cref="EntityScriptProxy.Bytes"/> 0 to 3).</summary>
    internal static int Fuse(EntityScriptProxy bomb)
        => bomb.Bytes[0] | (bomb.Bytes[1] << 8) | (bomb.Bytes[2] << 16) | (bomb.Bytes[3] << 24);

    /// <summary>Writes the fuse of the bomb (see <see cref="Fuse"/>).</summary>
    internal static void SetFuse(EntityScriptProxy bomb, int value)
    {
        bomb.Bytes[0] = (byte)value;
        bomb.Bytes[1] = (byte)(value >> 8);
        bomb.Bytes[2] = (byte)(value >> 16);
        bomb.Bytes[3] = (byte)(value >> 24);
    }

    /// <summary>The entity the hero holds when it is a bomb (the type 2), otherwise null.</summary>
    private static EntityScriptProxy? BombInTheHands(EntityScriptProxy hero)
        => hero.CarriedEntity?.GameplayProxy is EntityScriptProxy held && held.SpriteType == SpriteTypes[0] ? held : null;

    /// <summary>
    /// E20.h3b: the start of the bomb, <c>0x8003453C</c>, for the Circle with the item <c>0x23</c> equipped. Refused (the sound 3, the answer 1) when the hero already holds something (<c>+0x2C</c>), stands on the slope 4, has an item in use, or when three entities of the type 2 are already
    /// in the world, whatever their status (a destroyed bomb counts until the recycling of its slot). Otherwise the bomb is born at the hero by the interface (<c>SpawnWarpEntity</c>, sprite 2 of the global page); a full table gives no entity and the answer 1 with no sound. The fuse (<c>+0x274</c>) is then
    /// set to 240, after the birth, and the bomb is put in the hands of the hero (<see cref="PlayerManager.PutInHands"/>: the two links, the offset, the lift animation 5 on the ground or 0x0B in the air); the answer 0 puts the item in use. The bomb costs nothing and is never used up.
    /// </summary>
    internal static int Start(EntityScriptProxy hero)
    {
        var host = hero.ScriptHost;
        if (hero.CarriedEntity != null || hero.Slope_18c == DeepWaterSlope || hero.ItemInUse != 0)
        {
            host?.SoundPlayer?.PlaySfx(RefusalSound);
            return 1;
        }

        if (host is not IEntityWorldContext world)
        {
            return 1;
        }

        var bombs = 0;
        foreach (var entity in world.SpawnedEntities)
        {
            if (entity.SpriteType == SpriteTypes[0])
            {
                bombs++;
            }
        }

        if (bombs >= BombLimit)
        {
            host.SoundPlayer?.PlaySfx(RefusalSound);
            return 1;
        }

        var bomb = world.SpawnEntityBySpriteIndex(hero, false, SpriteTypes[0], hero.PosX, hero.PosY, hero.PosZ, 0);
        if (bomb == null)
        {
            return 1;
        }

        // The bomb does NOT take part in the tick of its birth (the oracle's key: status 1 at the tick of the Circle, 2 at the next, the fuse counting from the third), unlike the entities of the other births of the interface (E20.e0a3b, the marker posed last by the creation).
        bomb.JoinsBirthTick = false;
        SetFuse(bomb, StartFuse);
        PlayerManager.PutInHands(hero, bomb);
        // The bomb is at the hero's sub-pixel position (the key prints 206.09 for a hero at 206.09); the root of the creation is at a whole pixel and the first tick of the bomb pulls it back before the carrying takes over, so the root keeps the fraction.
        bomb.PushLogicalPositionToRoot(keepSubPixel: true);
        return 0;
    }

    /// <summary>
    /// E20.h3b: the driver of the bomb, <c>0x800352C4</c>, called once per free tick while the item is in use. It answers 1 ("finished": the item is freed) when the hero holds nothing or something that is not a bomb; 0 ("in use") while the bomb is in the hands;
    /// and when the hero stands on the slope 4 (the deep water) it cancels the item (<c>0x800350C0</c>: the cancellation of the bomb, then the item freed) and answers 1.
    /// </summary>
    internal static int Drive(EntityScriptProxy hero)
    {
        if (BombInTheHands(hero) == null)
        {
            return 1;
        }

        if (hero.Slope_18c != DeepWaterSlope)
        {
            return 0;
        }

        PlayerManager.CancelItemMode(hero);
        return 1;
    }

    /// <summary>
    /// E20.h3b: the cancellation of the bomb, <c>0x80034B54</c> (the lock of the control, the deep water, the states without handler): nothing without a bomb in the hands, or when the bomb already has a code of throw (<c>+0x24</c>, <see cref="EntityScriptProxy.Flags2"/>; the decompilation tests <c>+0x20</c>
    /// there, the binary <c>+0x24</c>, D1). Otherwise the hero's link is cleared, the smoke (the global effect 12) appears at the bomb, and the bomb is destroyed with the single-argument destruction (<c>0x8003A774</c>: no loot, no explosion). The bomb's own link is left as it is. Without a world nothing but the link can be done.
    /// </summary>
    internal static void Cancel(EntityScriptProxy hero)
    {
        if (BombInTheHands(hero) is not { } bomb || bomb.Flags2 != 0)
        {
            return;
        }

        hero.CarriedEntity = null;
        if (hero.ScriptHost is not IEntityWorldContext world)
        {
            return;
        }

        var (x, y, z) = (bomb.PosX, bomb.PosY, unchecked(bomb.PosZ + 1));
        world.EffectStage?.Emit(reservoir => reservoir.Create(false, SmokeEffect, 0, x, y, z));
        world.DestroyEntity(bomb);
    }

    /// <summary>
    /// E20.h3b: C62 (<c>0x8007A978</c>, slot C), one tick of the bomb of the type 2 in the status Normal. A bomb whose four corners stand on the ground property 4 (<c>+0x184 &amp; 4</c>, a hole) is destroyed at once (single-argument destruction). Otherwise the fuse (<c>+0x274</c>) goes down by one, always;
    /// at 0 the status becomes 3 (only E12 runs from then on) and the carrier lets go (the carrier's <c>+0x2C</c> and the bomb's <c>+0x28</c> cleared). While the fuse is under 0x3C (signed) and a multiple of 8 and not 0, a spark is made (the global effect 9, 8 px above the bomb) and, only if the stage gave an
    /// effect, two numbers are drawn that give its forces (<c>hi(r1 * 0x30001) - 0x18000</c> and <c>hi(r2 * 0x20001) - 0x10000</c>). A carried bomb asks for the animation 0 while it has no code of throw (<c>+0x24</c>); with any code it asks for the flight animation 2, clears the code and lets go of both links (the flags are not
    /// touched, unlike C23). A free bomb (<see cref="Free"/>) with an animation asked: blocked against a wall in the air it bounces once (<c>+0x278</c> is the lock, the direction turns half a turn, a spark with no draw); blocked on the ground it explodes where it stands when a corner stands on the band <c>0x1001</c>
    /// (the foot of a wall of the class B) and stops the flight animation; landed or touching it plays the sound <c>0x18</c>, clears the lock and, unless a corner is on the band, asks the animation 0. <paramref name="readCorners"/> is the seam of the tests (the four corner words of the cells).
    /// </summary>
    internal static void Tick(EntityScriptProxy bomb, IEntityWorldContext context, Func<EntityScriptProxy, uint[]>? readCorners = null)
    {
        if ((bomb.CombinedVramFlagsAND & 4) != 0)
        {
            context.DestroyEntity(bomb);
            return;
        }

        var fuse = Fuse(bomb);
        SetFuse(bomb, unchecked(fuse - 1));
        if (fuse == 0)
        {
            bomb.Status = EntityStatus.Deactivated;
            LetGo(bomb);
            Free(bomb, context, readCorners);
            return;
        }

        if (fuse < SparkWindow && (fuse & 7) == 0)
        {
            MakeSpark(bomb, context, draw: true);
        }

        if (bomb.PlatformEntity == null)
        {
            Free(bomb, context, readCorners);
            return;
        }

        if (bomb.Flags2 == 0)
        {
            bomb.TargetAnimationId = HeldAnimation;
            return;
        }

        bomb.TargetAnimationId = FlightAnimation;
        bomb.Flags2 = 0;
        LetGo(bomb);
    }

    /// <summary>Clears the carrier's link (<c>+0x2C</c>) and the bomb's own (<c>+0x28</c>); nothing when nobody carries it.</summary>
    private static void LetGo(EntityScriptProxy bomb)
    {
        if (bomb.PlatformEntity == null)
        {
            return;
        }

        if (bomb.PlatformEntity.GameplayProxy is EntityScriptProxy carrier)
        {
            carrier.CarriedEntity = null;
        }

        bomb.PlatformEntity = null;
    }

    /// <summary>The spark of the bomb (<c>CreateEffectEntity(0, 9, 0, x, y, z + 0x80000)</c>), made at once by the stage. With <paramref name="draw"/> and an effect obtained, two draws give its forces; a full reservoir or a world without a stage draws nothing.</summary>
    private static void MakeSpark(EntityScriptProxy bomb, IEntityWorldContext context, bool draw)
    {
        var spark = context.EffectStage?.CreateNow(false, SparkEffect, 0, bomb.PosX, bomb.PosY, unchecked(bomb.PosZ + 1 + SparkLift));
        if (spark == null || !draw)
        {
            return;
        }

        var r1 = (uint)GameRandom.Next();
        var r2 = (uint)GameRandom.Next();
        spark.ForceX = unchecked((int)(((ulong)r1 * 0x30001) >> 32) - 0x18000);
        spark.ForceY = unchecked((int)(((ulong)r2 * 0x20001) >> 32) - 0x10000);
    }

    /// <summary>The half of C62 for a bomb that nobody holds (<c>0x8007AAFC</c>): see <see cref="Tick"/>.</summary>
    private static void Free(EntityScriptProxy bomb, IEntityWorldContext context, Func<EntityScriptProxy, uint[]>? readCorners)
    {
        if (bomb.TargetAnimationId == 0)
        {
            return;
        }

        readCorners ??= CornerTileWords.Read;
        if (bomb.ForceAdjusted != 0)
        {
            if (bomb.IsOnGround == 0)
            {
                if (bomb.ForceZ <= 0 || bomb.DelayOrAngleOrEntityId != 0)
                {
                    return;
                }

                bomb.DelayOrAngleOrEntityId = 1;
                bomb.TargetDirection = (bomb.TargetDirection + BounceDirectionStep) & 0x1F;
                MakeSpark(bomb, context, draw: false);
                return;
            }

            if (CornerTileWords.AnyHasMask(readCorners(bomb)))
            {
                bomb.Status = EntityStatus.Deactivated;
            }

            bomb.TargetAnimationId = 0;
            return;
        }

        if (bomb.IsOnGround == 0 && bomb.HitCounter == 0)
        {
            return;
        }

        context.SoundPlayer?.PlaySfx(LandingSound);
        bomb.DelayOrAngleOrEntityId = 0;
        if (!CornerTileWords.AnyHasMask(readCorners(bomb)))
        {
            bomb.TargetAnimationId = 0;
        }
    }

    private sealed class Registrations : IDisposable
    {
        private readonly IDisposable _tick;
        private readonly IDisposable _touch;
        private readonly IDisposable _deactivate;

        public Registrations(IDisposable tick, IDisposable touch, IDisposable deactivate)
        {
            _tick = tick;
            _touch = touch;
            _deactivate = deactivate;
        }

        public void Dispose()
        {
            _tick.Dispose();
            _touch.Dispose();
            _deactivate.Dispose();
        }
    }
}
