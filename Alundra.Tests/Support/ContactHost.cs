#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Audio;
using Alundra.Hero;

namespace Alundra.Tests.Support;

/// <summary>
/// A script host with a real logic clock (one tick per 0.02 s update), a no-op interpreter (or the one given), the destroy of the world
/// (the status goes to <see cref="EntityStatus.FlagToDestroy"/>) and a collidable list rebuilt by <see cref="Rebuild"/> from
/// <see cref="All"/> with <see cref="EntitySupport.BuildCollidables"/>, as <c>WorldProxy.RefreshUpdateProxiesAndCollidables</c>.
/// </summary>
internal class ContactHost : IScriptHost
{
    /// <summary>
    /// E20.g1a T2: true while a montage plays a frame TICK BY TICK (<see cref="Alundra.Tests.Hero.CarryRig"/>): the host then drives the ticks of its entities, as <c>WorldProxy</c> does (the entities only pull their pose at the
    /// frame, and the montage runs the passes of each tick over all of them). False (the default) is the self-driven mode every other montage uses, where each entity plays all its ticks of a frame in its own update.
    /// </summary>
    public bool DrivesTicks { get; set; }

    bool IScriptHost.DrivesEntityTicks => DrivesTicks;

    private readonly LogicClock _clock = new();

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
        }
    }

    public ContactHost(IEventProgramRunner? runner = null, uint playerControlFlags = 0, HeroController? playerController = null)
    {
        Runner = runner ?? new NoOpRunner();
        GameState = new GameState { PlayerControlFlags = playerControlFlags };
        PlayerController = playerController;
    }

    public IEventProgramRunner Runner { get; }

    public EntityScriptProxy? ActiveCollisionEntity { get; set; }

    public GameState GameState { get; }

    public HeroController? PlayerController { get; }

    /// <summary>E19.d2c1 R8: the sound player the host hands to the hero's take-off (null: none, as the default member of the interface).</summary>
    public ISoundPlayer? SoundPlayer { get; set; }

    /// <summary>E20.b B1: the item tables the host hands to the hero's <c>MovePlayer</c> (null: none, as the default member of the interface, so the hero has no weapon and swings the basic sword).</summary>
    public ItemTables? ItemTables { get; set; }

    /// <summary>E20.c1b K2: the balance tables, the threshold and the equipment records the host hands to the hero's tick (null: none, as the default members of the interface).</summary>
    public BalanceTables? BalanceTables { get; set; }

    public int BalanceThreshold { get; set; }

    public EquipmentRecords? EquipmentRecords { get; set; }

    /// <summary>E20.f1a: the effect stage the host hands to the hero's tick (null: none, as the default member of the interface, so the hold spark and the burst of a charge create nothing).</summary>
    public EffectStage? EffectStage { get; set; }

    /// <summary>Every proxy of the world, in world order (the hero first).</summary>
    public List<EntityScriptProxy> All { get; } = new();

    public List<EntityScriptProxy> Collidables { get; } = new();

    IReadOnlyList<EntityScriptProxy> IScriptHost.Collidables => Collidables;

    public List<(EntityScriptProxy Entity, int EffectId)> Destroyed { get; } = new();

    public void DestroyEntity(EntityScriptProxy entity, int effectId)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        Destroyed.Add((entity, effectId));
    }

    /// <summary>E20.b B5: called when an entity asks for the ticks of the frame, i.e. after the head-of-frame pull of its ground state and before its first tick: where a map event of the tick (a mutation of the cells) lands.</summary>
    public Action? BeforeTicks { get; set; }

    public int LogicTicksThisFrame(float elapsedTime)
    {
        BeforeTicks?.Invoke();
        var ticks = _clock.TicksThisFrame(elapsedTime);
        _clock.CloseFrame();
        return ticks;
    }

    /// <summary>
    /// E19.d2c2 D5b T1 (docs/plan-e19-opcodes.md §1.2h.3.2b): the number of the current physics pass, which the world increments once per tick before its physics loop
    /// (<c>WorldProxy.PhysicsPassId</c>, D-D5B-1). A montage is self-driven (each entity plays all its ticks in its own update, one tick per frame at 0.02 s), so a
    /// pass is a frame: <see cref="BeginPhysicsPass"/> is called once per frame by the montages before the update of the world. It implements the default member
    /// <see cref="IScriptHost.PhysicsPassId"/> (added by T3).
    /// </summary>
    public int PhysicsPassId { get; private set; }

    /// <summary>Opens the next physics pass (one per frame of a self-driven montage, before the update of the world).</summary>
    public void BeginPhysicsPass() => PhysicsPassId++;

    /// <summary>The end pass of the frame (<c>EntityScriptProxy.EndPhysicsTick</c>, D-D5B-5): called by the montages after the update of the world, as <c>WorldProxy.RunEntityBlock</c> does after its physics loop.</summary>
    public void EndPhysicsPass()
    {
        foreach (var proxy in All)
        {
            proxy.EndPhysicsTick();
        }
    }

    /// <summary>
    /// Rebuilds <see cref="Collidables"/> (the end of the frame of the world) and, as <c>WorldProxy.RefreshUpdateProxiesAndCollidables</c> does at the lists step, gives every
    /// passenger its <see cref="EntityScriptProxy.RidingEntity"/> (D5b T1). The hero is excluded: it keeps what its own tick head computed
    /// (<see cref="EntitySupport.UpdateRidingEntityOfHero"/>), which the tests of the hero observe at the end of the frame (a first attempt that included it turned UH-10, UH-15 and UH-17 red).
    /// </summary>
    public void Rebuild()
    {
        EntitySupport.BuildCollidables(All, Collidables);
        foreach (var proxy in All)
        {
            if (!proxy.IsPlayer)
            {
                proxy.RidingEntity = null;
            }
        }

        var hero = All.Find(proxy => proxy.IsPlayer);
        var heroRiding = hero?.RidingEntity;
        EntitySupport.UpdateRidingEntities(Collidables);
        if (hero != null)
        {
            hero.RidingEntity = heroRiding;
        }
    }
}
