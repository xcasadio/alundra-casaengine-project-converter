#nullable enable
using System;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Session;

namespace Alundra.Hero;

/// <summary>
/// E20.e0c (docs/plan-e20-heros.md section 1.26, D-E0C-10, D-H1-1, D-H1-2): the bodies of heal of ALUN_CD.EXE (France) - the hearts, the herb, the potions, the magic drop, the crystal and the brew - which the pickup hands
/// to the hero (<c>GiveItem</c>, <c>0x80033F00</c>) when an object is taken, or drinks on the spot when the inventory is full. E20.h1 reuses them for the items drunk from the inventory. Each is one member, named after what
/// the object is, with the address of the body it ports:
/// <list type="bullet">
/// <item><description><see cref="SmallHeart"/> <c>0x80032E2C</c> (+2 life), <see cref="MediumHeart"/> <c>0x80032EEC</c> (+ max(1, HpMax / 4), a glow), <see cref="LargeHeart"/> <c>0x80032F84</c> (+ max(5, HpMax / 2), four sparks);</description></item>
/// <item><description><see cref="Herb"/> <c>0x80032E50</c> (+ max(5, HpMax / 4), a glow), <see cref="SmallPotion"/> <c>0x800330FC</c> (+ max(10, HpMax / 2), four sparks), <see cref="LargePotion"/> <c>0x80033274</c> (the life to the maximum, eight rings);</description></item>
/// <item><description><see cref="MagicDrop"/> <c>0x800335AC</c> (+1 magic, four sparks), <see cref="LargeCrystal"/> <c>0x800336F0</c> (the magic to the maximum, eight rings), <see cref="Brew"/> <c>0x80033A2C</c> (life and magic to the maximum, eight rings, eight sparks).</description></item>
/// </list>
/// The life is written on the ENTITY of the hero and never on the stats, with no fall back (D-H1-2): the stats are a mirror that <c>MovePlayer</c> writes at its end. The magic is the stats' (<see cref="PlayerManager.SetPlayerMp"/>).
/// The effects (global effect 14) go through the queue of the effect stage (D-E0C-8, ADR-0077): created, and their numbers drawn, when the stage applies its queue; a world without a stage heals all the same. The hero is read
/// now: its position (the binary's Z, the DLL's plus one raw unit) is captured at the call.
/// </summary>
internal static class HeroHeals
{
    private const int GlowForceZ = 0x20000;
    private const int RingAnimationOfLife = 0;
    private const int RingAnimationOfMagic = 2;
    private const int SparkRisingFast = 0x40000;
    private const int RingCount = 8;
    private const int SparkCount = 4;

    /// <summary><c>0x80032E2C</c>: +2 life, no effect.</summary>
    public static void SmallHeart(IEntityWorldContext world, GameState state, EntityScriptProxy hero) => AddLife(hero, 2);

    /// <summary><c>0x80032EEC</c>: + max(1, HpMax / 4) life and a glow.</summary>
    public static void MediumHeart(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        AddLife(hero, Math.Max(1, hero.HpMax / 4));
        Emit(world, hero, static (reservoir, x, y, z) => NativeEffects.CreateHealGlow(reservoir, x, y, z));
    }

    /// <summary><c>0x80032F84</c>: + max(5, HpMax / 2) life and four sparks.</summary>
    public static void LargeHeart(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        AddLife(hero, Math.Max(5, hero.HpMax / 2));
        Emit(world, hero, static (reservoir, x, y, z) => Sparks(reservoir, x, y, z, RingAnimationOfLife, GlowForceZ));
    }

    /// <summary><c>0x80032E50</c>: + max(5, HpMax / 4) life and a glow.</summary>
    public static void Herb(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        AddLife(hero, Math.Max(5, hero.HpMax / 4));
        Emit(world, hero, static (reservoir, x, y, z) => NativeEffects.CreateHealGlow(reservoir, x, y, z));
    }

    /// <summary><c>0x80033274</c>: the life to the maximum and eight rings of the animation 0.</summary>
    public static void LargePotion(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        hero.Hp = hero.HpMax;
        Emit(world, hero, static (reservoir, x, y, z) => Rings(reservoir, x, y, z, static _ => RingAnimationOfLife));
    }

    /// <summary><c>0x800330FC</c>: + max(10, HpMax / 2) life and four sparks.</summary>
    public static void SmallPotion(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        AddLife(hero, Math.Max(10, hero.HpMax / 2));
        Emit(world, hero, static (reservoir, x, y, z) => Sparks(reservoir, x, y, z, RingAnimationOfLife, GlowForceZ));
    }

    /// <summary><c>0x800335AC</c>: +1 magic (up to the maximum) and four sparks of the animation 2.</summary>
    public static void MagicDrop(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        PlayerManager.SetPlayerMp(state, (short)(state.PlayerStats.Mp + 1));
        Emit(world, hero, static (reservoir, x, y, z) => Sparks(reservoir, x, y, z, RingAnimationOfMagic, GlowForceZ));
    }

    /// <summary><c>0x800336F0</c>: the magic to the maximum and eight rings of the animation 2.</summary>
    public static void LargeCrystal(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        PlayerManager.SetPlayerMp(state, state.PlayerStats.MpMax);
        Emit(world, hero, static (reservoir, x, y, z) => Rings(reservoir, x, y, z, static _ => RingAnimationOfMagic));
    }

    /// <summary><c>0x80033A2C</c>: life and magic to the maximum, eight rings alternating the animations 2 and 0, then four times a spark of the animation 0 (rising fast) and a spark of the animation 2.</summary>
    public static void Brew(IEntityWorldContext world, GameState state, EntityScriptProxy hero)
    {
        hero.Hp = hero.HpMax;
        PlayerManager.SetPlayerMp(state, state.PlayerStats.MpMax);
        Emit(world, hero, static (reservoir, x, y, z) =>
        {
            Rings(reservoir, x, y, z, static i => i % 2 == 0 ? RingAnimationOfMagic : RingAnimationOfLife);
            for (var i = 0; i < SparkCount; i++)
            {
                NativeEffects.CreateHealSpark(reservoir, x, y, z, RingAnimationOfLife, SparkRisingFast);
                NativeEffects.CreateHealSpark(reservoir, x, y, z, RingAnimationOfMagic, GlowForceZ);
            }
        });
    }

    /// <summary><c>Hp = min(HpMax, Hp + amount)</c> on the entity (<c>0x80032E58-0x80032EA0</c>): the cap is the only clamp, on the signed words.</summary>
    private static void AddLife(EntityScriptProxy hero, int amount)
    {
        var sum = unchecked(hero.Hp + amount);
        hero.Hp = hero.HpMax < sum ? hero.HpMax : sum;
    }

    private static void Sparks(EffectReservoir reservoir, int x, int y, int z, int animation, int forceZ)
    {
        for (var i = 0; i < SparkCount; i++)
        {
            NativeEffects.CreateHealSpark(reservoir, x, y, z, animation, forceZ);
        }
    }

    private static void Rings(EffectReservoir reservoir, int x, int y, int z, Func<int, int> animationOf)
    {
        for (var i = 0; i < RingCount; i++)
        {
            NativeEffects.CreateHealRing(reservoir, x, y, z, i, animationOf(i));
        }
    }

    /// <summary>Queues the creation of the effects on the stage, at the hero's position of now (the binary's Z); nothing without a stage.</summary>
    private static void Emit(IEntityWorldContext world, EntityScriptProxy hero, Action<EffectReservoir, int, int, int> creators)
    {
        if (world.EffectStage is not { } stage)
        {
            return;
        }

        var (x, y, z) = (hero.PosX, hero.PosY, unchecked(hero.PosZ + 1));
        stage.Emit(reservoir => creators(reservoir, x, y, z));
    }
}
