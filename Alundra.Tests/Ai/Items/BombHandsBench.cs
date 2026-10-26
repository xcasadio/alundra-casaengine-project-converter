#nullable enable
using System;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Entities;
using Alundra.Rendering.Effects;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3b T1 (docs/plan-e20-heros.md section 1.30.3, phase A): the montage of the direct tests of the start, the driver and the cancellation of the bomb and of C62. The hero and the bombs are real proxies of real engine entities (<see cref="CarryBench.Make"/>, the logic entity set),
/// the world is the host of <see cref="CutBench"/> (it is both the script host and the world context: the sounds, the destructions, the entities born by the interface and the effect stage are recorded). The hero is the first of the entities the world lists, as the first slot of the table.
/// </summary>
internal sealed class BombHandsBench
{
    /// <summary>The world and its host (script host and world context at once).</summary>
    internal CutBench.CutHost Host { get; }

    /// <summary>The hero (slot 0 of the world).</summary>
    internal EntityScriptProxy Hero { get; }

    private BombHandsBench(CutBench.CutHost host, EntityScriptProxy hero)
    {
        Host = host;
        Hero = hero;
    }

    /// <summary>A world with an effect stage and a hero at (100, 90, 5) px, on the ground, on the slope 0, with nothing in the hands and no item in use.</summary>
    internal static BombHandsBench Create(bool effectStage = true)
    {
        var cut = CutBench.Create(frame: false);
        if (effectStage)
        {
            cut.Host.EffectStage = new EffectStage();
        }

        var hero = CarryBench.Make("hero", 100 * CarryBench.Px, 90 * CarryBench.Px, 5 * CarryBench.Px);
        hero.IsPlayer = true;
        hero.ScriptHost = cut.Host;
        hero.TargetAnimationId = 99;
        cut.Host.Born.Add(hero);
        return new BombHandsBench(cut.Host, hero);
    }

    /// <summary>The sounds the world heard, in order.</summary>
    internal int[] Sounds => Host.Sounds.Requests.ToArray();

    /// <summary>A bomb the world lists (type 2) in the status <paramref name="status"/>, in nobody's hands.</summary>
    internal EntityScriptProxy AddBomb(EntityStatus status = EntityStatus.Normal)
    {
        var bomb = CarryBench.Make("bomb", 10 * CarryBench.Px, 20 * CarryBench.Px, 0);
        bomb.SpriteType = 2;
        bomb.Status = status;
        bomb.ScriptHost = Host;
        Host.Born.Add(bomb);
        return bomb;
    }

    /// <summary>An entity of another type the world lists.</summary>
    internal EntityScriptProxy AddOther(int spriteType)
    {
        var other = AddBomb();
        other.SpriteType = spriteType;
        return other;
    }

    /// <summary>A bomb in the hands of the hero, linked the way the start leaves it (the hero's <c>+0x2C</c>, the bomb's <c>+0x28</c>), listed by the world.</summary>
    internal EntityScriptProxy Carry(int spriteType = 2)
    {
        var bomb = AddBomb();
        bomb.SpriteType = spriteType;
        Hero.CarriedEntity = bomb.LogicContextEntity;
        bomb.PlatformEntity = Hero.LogicContextEntity;
        return bomb;
    }

    /// <summary>Makes the birth by the interface give an entity with its logic entity set, three pixels east, five north and seven above the hero.</summary>
    internal void BornAtAnOffset()
    {
        Host.OnBirth = born =>
        {
            born.LogicContextEntity = new Entity { Name = "born bomb" };
            born.PosX = Hero.PosX + 3 * CarryBench.Px;
            born.PosY = Hero.PosY - 5 * CarryBench.Px;
            born.PosZ = Hero.PosZ + 7 * CarryBench.Px;
        };
    }

    /// <summary>The effects the stage holds once its queue is applied (a frame without tick: the operations are applied and nothing is updated).</summary>
    internal EffectSlot[] Effects()
    {
        Host.EffectStage!.RunFrame(0, Array.Empty<bool>());
        return Host.EffectStage.Reservoir.ActiveSlots.ToArray();
    }
}
