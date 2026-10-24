#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Physics;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1): a script host that is also the world context of the single-argument <c>DestroyEntity</c> (what <c>UpdatePlayerCarriedEntity</c> mode 1 calls through <c>player.ScriptHost</c>):
/// <see cref="ContactHost"/> plus the destruction that records the entity, flags it (the status 4) and clears the carrier as every destruction of the binary does (the production clearing is <c>WorldProxy.DestroyEntity</c>'s, W5, tested on the real world proxy).
/// </summary>
internal sealed class CarryHost : ContactHost, IEntityWorldContext
{
    public CarryHost(uint playerControlFlags, HeroController controller)
        : base(runner: new LiftableRunner(), playerControlFlags: playerControlFlags, playerController: controller)
    {
        ((LiftableRunner)Runner).Host = this;
    }

    /// <summary>The crate of the montage (the entity whose native program C23 the host runs).</summary>
    public EntityScriptProxy? Crate { get; set; }

    /// <summary>The host runs the program C23 (<see cref="LiftableAi.Tick"/>) of <see cref="Crate"/> when the pick phase of its tick chooses the slot C, as the registry of production would (T4 registers it).</summary>
    public bool RunLiftableProgram { get; set; }

    /// <summary>The native handler of the sprite of the crate: C23 only (the native programs of the other entities of the montage do nothing, as with the no-op runner of <see cref="ContactHost"/>).</summary>
    private sealed class LiftableRunner : IEventProgramRunner
    {
        public CarryHost? Host { get; set; }

        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
            if (Host is { RunLiftableProgram: true, Crate: { } crate } host && ReferenceEquals(entity, crate) && entity.EventTrigger == ScriptHelper.ProgramCTick)
            {
                LiftableAi.Tick(entity, host);
            }
        }
    }

    /// <summary>The entities the single-argument <c>DestroyEntity</c> was called for, in order.</summary>
    public List<EntityScriptProxy> SearchDestroyed { get; } = new();

    /// <summary>The tick (of the montage) at which each entity of <see cref="SearchDestroyed"/> was destroyed, set by <see cref="CarryRig"/>.</summary>
    public List<int> SearchDestroyedAtTick { get; } = new();

    internal int CurrentTick { get; set; }

    IReadOnlyList<EntityScriptProxy> IEntityWorldContext.SpawnedEntities => All;

    EntityScriptProxy? IEntityWorldContext.PlayerEntity => All.Find(proxy => proxy.IsPlayer);

    public EntityScriptProxy? EntityFollowedByCamera { get; set; }

    public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;

    public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;

    void IEntityWorldContext.DestroyEntity(EntityScriptProxy entity)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        SearchDestroyed.Add(entity);
        SearchDestroyedAtTick.Add(CurrentTick);

        // The clearing of the carrier the binary ends every destruction with (0x8003A74C): the double of this montage, the production one is WorldProxy.DestroyEntity's (W5, tested by DestroyEntityCarrierTests).
        if (entity.PlatformEntity?.GameplayProxy is EntityScriptProxy carrier)
        {
            carrier.CarriedEntity = null;
        }
    }

    NavigationGrid2D? IEntityWorldContext.NavigationGrid => null;
}

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1): the montage of the world tests of the carrying - <see cref="JumpHeroRig"/> (the real hero controller, a field of synthetic cells, the animation sets and the chains of <see cref="HeroAttackClock"/> for
/// the carrying states) with a crate of the original (flags <c>0x3323E4</c>, a sheet of balance with one attack) and a host that plays a frame TICK BY TICK, over all the entities, as <c>WorldProxy.RunEntityBlock</c> does: <c>MovePlayer</c>, the programs, the
/// lists, the animation clock, the physics pass in the order of the list (the hero first), the end pass; then the publication of the frame. A frame of <see cref="TicksPerFrame"/> ticks (1, 2 or 3) is one update of the world of that many hundredths of a second.
/// The program C23 of the crate is run by the montage in its programs pass (<see cref="RunLiftableProgram"/>), as the registry of production would run it (T4 registers it).
/// </summary>
internal sealed class CarryRig
{
    /// <summary>One pixel in raw units (16.16).</summary>
    public const int Px = 65536;

    /// <summary>The flags of the crates, jars and herbs of the export (<c>0x3323E4</c>): collidable (<c>0x80</c>), kind 1 (liftable, <c>0x200</c>), the shadow and the pickup-time bits.</summary>
    public const uint CrateFlags = 0x3323E4;

    private readonly Func<int, uint> _hold;

    private CarryRig(JumpHeroRig rig, CarryHost host, int ticksPerFrame, Func<int, uint> hold)
    {
        Rig = rig;
        Host = host;
        TicksPerFrame = ticksPerFrame;
        _hold = hold;
    }

    public JumpHeroRig Rig { get; }

    public CarryHost Host { get; }

    public EntityScriptProxy Hero => Rig.Hero;

    /// <summary>The crate added by <see cref="AddCrate"/> (null before).</summary>
    public EntityScriptProxy? Crate { get; private set; }

    /// <summary>Ticks per frame of the montage: 1, 2 or 3.</summary>
    public int TicksPerFrame { get; }

    /// <summary>The number of logic ticks played since the montage was settled (the tick just played).</summary>
    public int Tick { get; private set; }

    /// <summary>The montage runs the program C23 (<see cref="LiftableAi.Tick"/>) of the crate in the programs pass of each tick (the pick phase of the crate chooses the slot C).</summary>
    public bool RunLiftableProgram
    {
        get => Host.RunLiftableProgram;
        set => Host.RunLiftableProgram = value;
    }

    /// <summary>Told the number of the tick right after <c>MovePlayer</c> of that tick and before the programs, the clock and the physics: where the binary's <c>TryLift</c> puts the object in the hands (the animation clock of the tick then starts the lift gesture).</summary>
    public Action<int>? AfterMovePlayer { get; set; }

    /// <summary>
    /// Plays ONE tick, the tick L of the lift: <see cref="PlayerManager.PutInHands"/> runs inside it, after <c>MovePlayer</c> (where the binary's <c>PlayerTryAction</c> lifts) and before the clock and the physics of the tick. Returns L.
    /// The mode 0 of that tick found nothing carried; the physics of L finds the object where it was; the series of the lift starts at L + 1. <paramref name="fromTheHead"/>: the object stands on the head of the hero
    /// (32 px over him, <c>RidingEntity</c> the hero), so the lift gives the carrying stand 0x0C with nothing to draw in.
    /// </summary>
    public int LiftNow(bool fromTheHead = false)
    {
        var lift = Tick + 1;
        AfterMovePlayer = tick =>
        {
            if (tick == lift)
            {
                if (fromTheHead)
                {
                    Crate!.RidingEntity = Hero.LogicContextEntity; // the object stood on the head of the hero: the lift gives the carrying stand at once (D-G-2).
                }

                PlayerManager.PutInHands(Hero, Crate!);
            }
        };
        Run(1);
        return lift;
    }

    /// <summary>
    /// The montage: the hero at (<paramref name="heroX"/>, <paramref name="heroY"/>) px with a free pad (or the pad locked by <c>ControlLocked</c>), the animation sets of the carrying; <paramref name="hold"/> gives the pad word of each tick (the number of the tick
    /// being played, from 1); <paramref name="configure"/> adds the entities of the scene to the world before the hero (as <see cref="JumpHeroRig.Build"/>).
    /// </summary>
    public static CarryRig Build(
        int ticksPerFrame,
        Func<int, uint>? hold = null,
        ICollisionField? field = null,
        Func<ContactHost, IMovementObstacleProbe?>? probeFactory = null,
        float heroX = 109f,
        float heroY = 100f,
        float heroZ = 0f,
        bool freePad = true)
    {
        CarryHost? host = null;
        var rig = JumpHeroRig.Build(
            field,
            probeFactory,
            x: heroX,
            y: heroY,
            z: heroZ,
            freePad: freePad,
            attackClock: true,
            hostFactory: (flags, controller) => host = new CarryHost(flags, controller));
        var carry = new CarryRig(rig, host!, ticksPerFrame, hold ?? (_ => 0u));
        rig.Hero.TargetDirection = 24; // east, as the oracle.
        rig.Hero.LogicContextEntity = rig.HeroEntity; // what the world sets on every entity: the link of an object in the hands is the logic entity of the hero.
        return carry;
    }

    /// <summary>
    /// Adds a crate whose box starts <paramref name="x"/>, <paramref name="y"/> (the logical position, whole pixels) at height <paramref name="z"/> px, the box of the original <paramref name="sizeX"/> x <paramref name="sizeY"/> x <paramref name="sizeZ"/> around the
    /// position; the flags of the export, a sheet of balance with <paramref name="attackCount"/> attacks and its logic entity set. Added after the hero (the hero is the first of the physics list, as in the world).
    /// </summary>
    public EntityScriptProxy AddCrate(int x, int y, int z = 0, int sizeX = 24, int sizeY = 16, int sizeZ = 16, int offsetX = -12, int offsetY = -8, int attackCount = 1, string name = "crate")
    {
        var crate = ContactWorld.AddEntity(Rig.World, Host, name, x, y, z, offsetX, offsetY, 0, sizeX, sizeY, sizeZ);
        crate.LogicContextEntity = crate.OwnerEntity!;
        crate.Flags = CrateFlags;
        crate.BalanceRecord = CarryBench.Sheet(attackCount);
        if (Crate == null)
        {
            Crate = crate;
            Host.Crate = crate;
        }

        return crate;
    }

    /// <summary>A collidable object of the scene without the crate's flags (a wall, a platform, a chest): the box <paramref name="sizeX"/> x <paramref name="sizeY"/> x <paramref name="sizeZ"/> given by offset and size, the logic entity set.</summary>
    public EntityScriptProxy AddObject(string name, int x, int y, int sizeX, int sizeY, int sizeZ, int offsetX = 0, int offsetY = 0, int z = 0)
    {
        var obj = ContactWorld.AddEntity(Rig.World, Host, name, x, y, z, offsetX, offsetY, 0, sizeX, sizeY, sizeZ);
        obj.LogicContextEntity = obj.OwnerEntity!;
        return obj;
    }

    /// <summary>Plays <paramref name="ticks"/> logic ticks, in frames of <see cref="TicksPerFrame"/> (the last one cut); <paramref name="afterTick"/> is told the number of each tick right after it is played.</summary>
    public void Run(int ticks, Action<int>? afterTick = null)
    {
        var remaining = ticks;
        while (remaining > 0)
        {
            var frame = Math.Min(TicksPerFrame, remaining);
            PlayFrame(frame, afterTick);
            remaining -= frame;
        }
    }

    /// <summary>Plays ticks until <paramref name="done"/> is true right after a tick (at most <paramref name="limit"/>); the frame in progress is cut at that tick. Returns the number of ticks played.</summary>
    public int RunUntil(Func<bool> done, int limit)
    {
        var start = Tick;
        _stopWhen = done;
        try
        {
            while (Tick - start < limit && !_stopped)
            {
                PlayFrame(Math.Min(TicksPerFrame, limit - (Tick - start)), null);
            }
        }
        finally
        {
            _stopWhen = null;
            _stopped = false;
        }

        return Tick - start;
    }

    private Func<bool>? _stopWhen;

    private bool _stopped;

    private void PlayFrame(int ticks, Action<int>? afterTick)
    {
        Host.DrivesTicks = true;
        try
        {
            var state = Host.GameState;
            Rig.Pad.Hold = _hold(Tick + 1);
            Rig.Pad.Pressed = 0;
            Host.Rebuild();
            Rig.World.Update(ticks * 0.02f);
            for (var i = 0; i < ticks; i++)
            {
                Tick++;
                Host.CurrentTick = Tick;
                var hold = _hold(Tick);
                state.TickPad.Update(hold);
                Hero.MovePlayerOnTickPad(PadState.FromTickPad(state.TickPad, in state.LastPadState));
                AfterMovePlayer?.Invoke(Tick);

                foreach (var proxy in Host.All.ToArray())
                {
                    proxy.TickPrograms();
                }

                Host.Rebuild();
                foreach (var proxy in Host.All)
                {
                    proxy.TickAnimationClock();
                }

                Host.BeginPhysicsPass();
                foreach (var proxy in Host.All.ToArray())
                {
                    proxy.TickPhysicsOnce();
                }

                Host.EndPhysicsPass();
                afterTick?.Invoke(Tick);
                if (_stopWhen != null && _stopWhen())
                {
                    _stopped = true;
                    break;
                }
            }

            foreach (var proxy in Host.All)
            {
                if (proxy.SyncAnimationDue)
                {
                    FrameSyncPasses.SyncAnimation(proxy.OwnerEntity!);
                }

                FrameSyncPasses.SyncTransform(proxy.OwnerEntity!);
                proxy.TookPartInFrame = false;
                proxy.SyncAnimationDue = false;
            }
        }
        finally
        {
            Host.DrivesTicks = false;
        }
    }
}
