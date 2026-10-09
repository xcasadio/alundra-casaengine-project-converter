using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E20.a A8 (docs/plan-e20-heros.md section 1.3, ADR-0053): the STRUCTURE of one logic tick of a world the engine drives, with frames of 0, 1 and 2 ticks, through a real
/// <c>World.Update</c> (the entities of the engine's loop, then the gameplay proxy of the world): the number of calls of <c>MovePlayer</c>, the order of the passes of a tick
/// (<c>UpdateWorld</c> of the binary: pad, box, map events <c>0x8003C67C</c>, then <c>UpdateEntities</c> <c>0x8003B388</c>: <c>MovePlayer</c>, programs, counters, lists, animation clock,
/// physics, hits, then the inventory), the hit-pass hook read one tick later and cleared by the counters pass, the closed gate, the freshness of the published state and the first tick of an
/// entity that appears during a frame. The passes are named by the test seam <c>AlundraWorldProxy.TickTraceForTests</c>.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraTickStructureTests : IDisposable
{
    private const float OneTick = 1f / 50f;
    private const float TwoTicks = 2f / 50f;
    private const float NoTick = 1f / 250f;

    private static readonly string[] OpenTick =
    {
        "pad", "box", "mapEvents", "movePlayer", "programs", "pending", "counters", "lists", "clock", "physics", "activeEffects", "hit", "inventory",
    };

    public AlundraTickStructureTests() => ResetGlobals();

    public void Dispose() => ResetGlobals();

    private static void ResetGlobals()
    {
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        AlundraScreenFadeDirector.Instance.ResetForTests();
        AlundraMusicPlayer.Instance.ResetForTests();
        AlundraBgmFadeDirector.Instance.ResetForTests();
    }

    private sealed class RecordingRunner : IEventProgramRunner
    {
        public readonly List<(int Tick, AlundraEntityScriptProxy Entity, int Slot)> Calls = new();
        public Func<int> CurrentTick = () => 0;
        public Action<AlundraEntityScriptProxy, int>? OnScript;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            Calls.Add((CurrentTick(), entity, programSlot));
            OnScript?.Invoke(entity, programSlot);
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity) => Calls.Add((CurrentTick(), entity, -1));
    }

    /// <summary>A real world driven by <c>World.Update</c> (a game with the runtime policy, a flat field), its gameplay proxy, a hero with a real controller and one NPC.</summary>
    private sealed class Rig
    {
        public readonly World World;
        public readonly AlundraWorldProxy Proxy = new();
        public readonly Entity HeroEntity;
        public readonly AlundraEntityScriptProxy Hero;
        public readonly Entity NpcEntity;
        public readonly AlundraEntityScriptProxy Npc;
        public readonly RecordingRunner Runner = new();
        public readonly List<string> Trace = new();
        public uint Hold;
        public int Tick = -1;
        public Action<string>? OnPass;
        private readonly List<Entity> _spawned;

        public Rig()
        {
            World = ContactWorld.BuildWorld(FlatCells.Create(), null);
            HeroWorldFixture.SetProperty(World, nameof(World.GameplayProxy), (GameplayProxy)Proxy);
            Proxy.InitializeWithWorld(World);
            Proxy.EventProgramRunner = Runner;
            Runner.CurrentTick = () => Tick;

            var controller = new AlundraPlayerController { PadStateProviderForTests = () => new AlundraPadState { ButtonsHold = Hold } };
            typeof(AlundraWorldProxy).GetField("_playerController", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Proxy, controller);
            _spawned = (List<Entity>)typeof(AlundraWorldProxy).GetField("_spawnedEntities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Proxy)!;

            var settings = HeroWorldFixture.LoadHeroControllerSettings(ProjectRootFinder.Find());
            (HeroEntity, Hero) = HeroWorldFixture.BuildHeroPawn(World, settings, new Vector3(200.25f, 100.5f, 0f), Proxy);
            Hero.MapGravity = JumpHeroRig.MapGravity;
            Hero.MapMaxFallSpeed = JumpHeroRig.MapMaxFallSpeed;
            Hero.Flags |= EntityFlags.Collidable | EntityFlags.Gravity;
            Hero.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
            {
                [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
                [1] = new AnimSetEntry { Anim = 1, Speed = 208, Acceleration = 1 },
            };
            Hero.CurrentAnimationId = 0;
            Hero.TargetAnimationId = 0;
            Hero.Status = EntityStatus.Normal;
            _spawned.Add(HeroEntity);
            Proxy.PlayerEntity = Hero;

            (NpcEntity, Npc) = AddNpc(300, 100);

            Proxy.TickTraceForTests = name =>
            {
                Trace.Add(name);
                if (name == "pad")
                {
                    Tick++;
                }

                OnPass?.Invoke(name);
            };
        }

        public (Entity Entity, AlundraEntityScriptProxy Proxy) AddNpc(int xPixels, int yPixels)
        {
            var root = new TransformComponent();
            root.LocalTransform.Position = new Vector3(xPixels, yPixels, 0f);
            var entity = new Entity { Name = "npc", RootComponent = root, GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
            entity.Initialize();
            var npc = (AlundraEntityScriptProxy)entity.GameplayProxy;
            npc.ScriptHost = Proxy;
            npc.Status = EntityStatus.Normal;
            npc.EventTrigger = ScriptHelper.ProgramUnknown; // as the spawn seeds it: nothing picked yet.
            npc.PosX = xPixels << 16;
            npc.PosY = yPixels << 16;
            npc.ProgramIndexes[ScriptHelper.ProgramCTick] = 1;
            npc.ProgramIndexes[ScriptHelper.ProgramDTouch] = 1;
            World.AddEntity(entity);
            _spawned.Add(entity);
            return (entity, npc);
        }

        /// <summary>One rendered frame (the engine's entities, then the gameplay proxy); returns the passes of that frame.</summary>
        public List<string> Frame(float dt)
        {
            var from = Trace.Count;
            World.Update(dt);
            return Trace.Skip(from).ToList();
        }

        public static int TicksOf(IEnumerable<string> passes) => passes.Count(p => p == "pad");
    }

    private static Rig BuildWarmRig()
    {
        var rig = new Rig();
        rig.Frame(OneTick); // the first frame always carries a tick (the sticky floor of the first frame); it also integrates the entities into the engine.
        rig.Frame(OneTick);
        return rig;
    }

    [Fact]
    public void MovePlayer_RunsOncePerTick_AndNotAtAll_InAFrameWithoutTick()
    {
        var rig = BuildWarmRig();
        foreach (var (dt, expectedTicks) in new[] { (OneTick, 1), (NoTick, 0), (TwoTicks, 2), (OneTick, 1), (TwoTicks, 2) })
        {
            var before = AlundraPlayerManager.MovePlayerCallCountForTests;
            var passes = rig.Frame(dt);
            Assert.Equal(expectedTicks, Rig.TicksOf(passes));
            Assert.Equal(expectedTicks, AlundraPlayerManager.MovePlayerCallCountForTests - before);
        }
    }

    [Fact]
    public void AFrameWithoutTick_DoesNotChangeTheHero()
    {
        var rig = BuildWarmRig();
        rig.Hold = 0;
        rig.Frame(OneTick);
        var animation = rig.Hero.TargetAnimationId;
        var direction = rig.Hero.TargetDirection;
        var flags = rig.Hero.Flags;
        var slope = rig.Hero.Slope_18c;
        var floor = rig.Hero.FloorHeight;
        var vram = rig.Hero.CombinedVramFlagsOR;
        var calls = AlundraPlayerManager.MovePlayerCallCountForTests;

        rig.Hold = AlundraPadState.Right; // the player pushes right, but no tick runs on this frame: MovePlayer must not see it.
        var passes = rig.Frame(NoTick);

        Assert.Equal(0, Rig.TicksOf(passes));
        Assert.Equal(calls, AlundraPlayerManager.MovePlayerCallCountForTests);
        Assert.Equal(animation, rig.Hero.TargetAnimationId);
        Assert.Equal(direction, rig.Hero.TargetDirection);
        Assert.Equal(flags, rig.Hero.Flags);
        Assert.Equal(slope, rig.Hero.Slope_18c);
        Assert.Equal(floor, rig.Hero.FloorHeight);
        Assert.Equal(vram, rig.Hero.CombinedVramFlagsOR);

        rig.Frame(OneTick); // the next tick sees the direction.
        Assert.Equal(1u, rig.Hero.TargetAnimationId);
    }

    [Fact]
    public void TheOrderOfATick_IsTheOneOfTheBinary_ForFramesOfOneAndTwoTicks()
    {
        var rig = BuildWarmRig();

        Assert.Equal(OpenTick, rig.Frame(OneTick));
        Assert.Empty(rig.Frame(NoTick));
        Assert.Equal(OpenTick.Concat(OpenTick), rig.Frame(TwoTicks));
    }

    [Fact]
    public void ADrivenWorldWithAClosedGate_RunsOnlyTheListsOfTheEntityBlock()
    {
        var rig = BuildWarmRig();
        rig.Proxy.GameState.PlayerControlFlags = AlundraGameState.PlayerControlBits.MenuOpen;

        var passes = rig.Frame(TwoTicks);

        Assert.Equal(new[] { "pad", "box", "lists", "inventory", "pad", "box", "lists", "inventory" }, passes);
        var calls = rig.Runner.Calls.Count;
        rig.Frame(OneTick);
        Assert.Equal(calls, rig.Runner.Calls.Count); // no program ran either.
    }

    [Theory]
    [InlineData(0)] // the hit is written at the first tick of a two-tick frame: seen at the second tick, the same frame.
    [InlineData(1)] // written at the second tick: seen at the first tick of the next frame.
    public void AValueOfTheHitHook_IsSeenByThePrograms_OneTickLater_ThenClearedByTheCountersOfThatTick(int tickInFrame)
    {
        var rig = BuildWarmRig();
        var frameFirstTick = rig.Tick + 1;
        var writeTick = frameFirstTick + tickInFrame;
        var seenByCounters = new Dictionary<int, bool>();
        var clearedAtLists = new Dictionary<int, bool>();
        rig.Proxy.HitPassHookForTests = () =>
        {
            if (rig.Tick == writeTick)
            {
                rig.Npc.TouchingEntity = rig.HeroEntity;
            }
        };
        rig.OnPass = name =>
        {
            if (name == "counters")
            {
                seenByCounters[rig.Tick] = rig.Npc.TouchingEntity != null;
            }
            else if (name == "lists")
            {
                clearedAtLists[rig.Tick] = rig.Npc.TouchingEntity == null;
            }
        };

        rig.Frame(TwoTicks);
        rig.Frame(TwoTicks);

        // The programs of the tick after the write pick the touch slot (D) instead of the tick slot (C).
        var slotOf = rig.Runner.Calls.Where(c => ReferenceEquals(c.Entity, rig.Npc) && c.Tick >= frameFirstTick).ToDictionary(c => c.Tick, c => c.Slot);
        Assert.Equal(ScriptHelper.ProgramCTick, slotOf[writeTick]);
        Assert.Equal(ScriptHelper.ProgramDTouch, slotOf[writeTick + 1]);
        Assert.Equal(ScriptHelper.ProgramCTick, slotOf[writeTick + 2]);

        // At the tick after the write the counters pass still finds the value (programs and counters of that tick come before the clearing) and the lists, right after, find it cleared.
        Assert.False(seenByCounters[writeTick]);
        Assert.True(seenByCounters[writeTick + 1]);
        Assert.True(clearedAtLists[writeTick + 1]);
        Assert.False(seenByCounters[writeTick + 2]);
    }

    [Fact]
    public void TheCountersPass_RunsOncePerTickOverEveryEntity_AndKeepsTheRider()
    {
        var rig = BuildWarmRig();
        rig.Hero.DamagedTickCounter = 3;
        rig.Npc.FrameCollisionTickCounter = 2;
        rig.Npc.HitCounter = 7;
        var heroAge = rig.Hero.FrameCounter;
        var npcAge = rig.Npc.FrameCounter;

        rig.Frame(TwoTicks);

        Assert.Equal(heroAge + 2, rig.Hero.FrameCounter);
        Assert.Equal(npcAge + 2, rig.Npc.FrameCounter);
        Assert.Equal(1, rig.Hero.DamagedTickCounter);
        Assert.Equal(0, rig.Npc.FrameCollisionTickCounter);
        Assert.Equal(0, rig.Npc.HitCounter);

        rig.Frame(NoTick); // a frame without tick counts nothing.
        Assert.Equal(heroAge + 2, rig.Hero.FrameCounter);
    }

    [Fact]
    public void ThePublishedState_IsTheStateAfterTheLastTickOfTheSameFrame_ForOneAndTwoTicks()
    {
        var rig = BuildWarmRig();
        var counter = 0;
        rig.Runner.OnScript = (entity, slot) =>
        {
            if (ReferenceEquals(entity, rig.Npc))
            {
                counter++;
                entity.PosX += 65536;
                entity.TargetAnimationId = (uint)(100 + counter);
            }
        };
        rig.Hold = AlundraPadState.Right;
        var heroStartX = rig.Hero.PosX;

        // The walk from rest to the east: the first tick moves 79872 (half the step of the animation 1), the following ones 159744. One tick: 79872; two ticks: 319488 each; one tick: 159744.
        var expectedSteps = new[] { 79872, 319488, 319488, 159744 };
        var frameIndex = 0;

        foreach (var dt in new[] { OneTick, TwoTicks, TwoTicks, OneTick })
        {
            rig.Frame(dt);

            // The NPC: the root shows the position after the last tick of THIS frame and the animation is resolved to the last target of this frame.
            var expected = AlundraEntitySpawnFactory.ResolveLogicalPosition(rig.Npc.PosX, rig.Npc.PosY, rig.Npc.PosZ);
            Assert.Equal(expected.X, rig.NpcEntity.RootComponent!.LocalTransform.Position.X);
            Assert.Equal((uint)(100 + counter), rig.Npc.TargetAnimationId);
            Assert.Equal(rig.Npc.TargetAnimationId, rig.Npc.CurrentAnimationId);

            // The hero: the animation MovePlayer asked for at the last tick of this frame is the one resolved at its end.
            Assert.Equal(rig.Hero.TargetAnimationId, rig.Hero.CurrentAnimationId);

            // And its published position (E20.b B7, P4 of E20.a): the hero has a controller, so the root is the truth and the tick pushes the logical position onto it (to the sub-pixel,
            // not the whole pixel of ResolveLogicalPosition, which is the NPCs' rule): the root shown after the frame is the logical position after the last tick of this frame, and it
            // moved by the sum of the steps of the ticks of the frame (the Right is held).
            Assert.Equal(rig.Hero.PosX / 65536.0, rig.HeroEntity.RootComponent!.LocalTransform.Position.X, 4);
            Assert.Equal(expectedSteps[frameIndex++], rig.Hero.PosX - heroStartX);
            heroStartX = rig.Hero.PosX;
        }

        Assert.Equal(1u, rig.Hero.CurrentAnimationId);
    }

    [Fact]
    public void AnEntityThatAppearsDuringAFrame_HasItsFirstTickAtTheFirstTickOfTheNextFrame()
    {
        var rig = BuildWarmRig();
        AlundraEntityScriptProxy? newcomer = null;
        var spawnTick = -1;
        rig.OnPass = name =>
        {
            if (name == "mapEvents" && newcomer == null && rig.Tick == rig.Trace.Count(p => p == "pad") - 1 && spawnTick < 0)
            {
                spawnTick = rig.Tick;
                (_, newcomer) = rig.AddNpc(320, 100);
            }
        };

        var frameFirstTick = rig.Tick + 1;
        rig.Frame(TwoTicks); // the newcomer appears at the map events of the first tick of this two-tick frame.
        Assert.NotNull(newcomer);
        Assert.Equal(frameFirstTick, spawnTick);
        Assert.DoesNotContain(rig.Runner.Calls, c => ReferenceEquals(c.Entity, newcomer)); // not integrated by the engine yet: no tick this frame, not even the second one.

        var nextFrameFirstTick = rig.Tick + 1;
        rig.Frame(TwoTicks);
        var ticks = rig.Runner.Calls.Where(c => ReferenceEquals(c.Entity, newcomer)).Select(c => c.Tick).ToArray();
        Assert.Equal(new[] { nextFrameFirstTick, nextFrameFirstTick + 1 }, ticks);
    }

    [Fact]
    public void AnEntityFlaggedForDestruction_IsRecycledAtTheHeadOfTheNextTick_AndNoLongerTicks()
    {
        var rig = BuildWarmRig();
        rig.Runner.OnScript = (entity, slot) =>
        {
            if (ReferenceEquals(entity, rig.Npc) && rig.Tick == 20)
            {
                entity.Status = EntityStatus.FlagToDestroy;
            }
        };

        while (rig.Tick < 19)
        {
            rig.Frame(OneTick);
        }

        rig.Frame(OneTick); // tick 20: the program flags it.
        Assert.Equal(EntityStatus.FlagToDestroy, rig.Npc.Status);
        rig.Frame(OneTick); // tick 21: recycled at its head (after the map events), before the programs.
        Assert.Equal(EntityStatus.Destroyed, rig.Npc.Status);
        Assert.DoesNotContain(((IEntityWorldContext)rig.Proxy).SpawnedEntities, e => ReferenceEquals(e, rig.Npc));
        Assert.DoesNotContain(rig.Runner.Calls, c => ReferenceEquals(c.Entity, rig.Npc) && c.Tick > 20);
    }
}
