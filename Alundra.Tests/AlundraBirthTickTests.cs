#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Xunit.Abstractions;

namespace Alundra.Tests;

/// <summary>
/// E20.e0a3b (docs/plan-e20-heros.md section 1.16.3b, revision 3, D-E0A-5, D-E0A-8, D-E20-34, ADR-0065): an entity born by <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> in <c>MovePlayer</c> (the place of the shot, by the test
/// seam <see cref="AlundraPlayerManager.WeaponSpawnBirthHookForTests"/>) takes part in the tick that sees it born, as the rows <c>BT-M1</c> to <c>BT-M3</c> of the oracle V6 (<c>docs/plan-e20-annexe/e20-e0a/oracle/out/v6.json</c>);
/// one born in the native program of another entity runs from the next tick (the gap of <c>BT-H1</c>, <c>BT-H4</c>, <c>BT-H6</c>, D-E20-34); the entities from a record are untouched by the interface (E20.e0a4 gave the marker to the opcodes
/// <c>0x2D</c>, <c>0x8A</c> and <c>0x8B</c> too, see <see cref="AlundraRecordBirthTickTests"/>; the map load and the direct call of <c>SpawnEntityByRecordId</c> never pose it). The real world
/// of map 14 (<see cref="ArcRun"/>, real prefabs), the passes of the tick named by <see cref="AlundraWorldProxy.TickTraceForTests"/> (the state seen at the head of a pass is the state after the pass before it).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraBirthTickTests : IDisposable
{
    private const int Arrow = 3;
    private const int Direction = 0x18;

    private readonly ITestOutputHelper _output;

    public AlundraBirthTickTests(ITestOutputHelper output)
    {
        _output = output;
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
    }

    public void Dispose() => AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

    private static ArcRun Arc(string name) => new(new ArcSpec("E20e0a3b-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    /// <summary>The state of the newborn at the head of a pass.</summary>
    private sealed record Snapshot(EntityStatus Status, int EventTrigger, int FrameCounter, bool InUpdateList, bool InHitList, bool ClockRan, int ClockTicks, int PosX, int Aruns, bool Integrated);

    private static bool Contains(ArcRun arc, string field, AlundraEntityScriptProxy proxy)
        => ((IEnumerable)typeof(AlundraWorldProxy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(arc.Proxy)!).Cast<object>().Any(o => ReferenceEquals(o, proxy));

    /// <summary>The montage of a shot: a handler of the program A of the arrow counts its runs, the hook calls the interface from the exact place of <c>MovePlayer</c>, and the passes of every tick are sampled.</summary>
    private sealed class Shot
    {
        public readonly ArcRun Arc;
        public int ARuns;
        public int HookCalls;
        public AlundraEntityScriptProxy? Born;
        public readonly List<(int Tick, string Pass, Snapshot State)> Samples = new();
        public int Tick;
        public int ClockTicks;
        public int SpriteIndex = Arrow;

        public Shot(ArcRun arc, int spriteIndex = Arrow)
        {
            Arc = arc;
            SpriteIndex = spriteIndex;
            WrapRunner(arc, e => { if (e.SpriteType == spriteIndex) { ARuns++; } });
            AlundraPlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
            {
                HookCalls++;
                Born = Context(arc).SpawnEntityBySpriteIndex(hero, false, SpriteIndex, call.X, call.Y, call.Z, Direction);
            };
            arc.Proxy.TickTraceForTests = pass =>
            {
                if (pass == "pad")
                {
                    Tick++;
                }

                if (Born == null)
                {
                    return;
                }

                Samples.Add((Tick, pass, Sample()));
            };
        }

        private Snapshot Sample()
        {
            var b = Born!;
            var entity = ArcRun.EntityOf(b)!;
            return new Snapshot(b.Status, b.EventTrigger, b.FrameCounter, Contains(Arc, "_updateProxies", b), Contains(Arc, "_hitList", b), b.BoxSwitchLoaded && b.BoxAnimationId == b.TargetAnimationId, b.BoxTicks, b.PosX, ARuns, entity.World != null);
        }

        /// <summary>Arms the hero's bow so that the NEXT tick of <c>MovePlayer</c> reaches the spawn of the shot (the record of Hunter's Bow, the animation that spawns after the delay).</summary>
        public void Arm()
        {
            var hero = Arc.Hero;
            hero.WeaponRecordPointer = AlundraWeaponTables.AnimationAddress(8, air: false);
            hero.WeaponStepCounter = AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 2);
            hero.TargetAnimationId = 0x13;
        }

        public Snapshot At(int tick, string pass) => Samples.Last(s => s.Tick == tick && s.Pass == pass).State;
    }

    /// <summary>Sees the program A (slot 0, trigger 0) of the entities of the global page that reach the sprite event (the production registry already holds the key (0, 0) for the Muruta, so the test wraps the runner instead).</summary>
    private sealed class SpriteEventRunner : IEventProgramRunner
    {
        private readonly IEventProgramRunner _inner;
        private readonly Action<AlundraEntityScriptProxy> _onProgramA;

        public SpriteEventRunner(IEventProgramRunner inner, Action<AlundraEntityScriptProxy> onProgramA)
        {
            _inner = inner;
            _onProgramA = onProgramA;
        }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot) => _inner.RunScript(entity, programSlot);

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
            if (entity.EventTrigger == ScriptHelper.ProgramALoad && entity.SpriteType < 0x100)
            {
                _onProgramA(entity);
            }

            _inner.RunSpriteEvent(entity);
        }
    }

    private static void WrapRunner(ArcRun arc, Action<AlundraEntityScriptProxy> onProgramA)
        => arc.Proxy.EventProgramRunner = new SpriteEventRunner(arc.Proxy.EventProgramRunner, onProgramA);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a) BT-M1..BT-M3: born in MovePlayer.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void BT_M_BornInMovePlayer_TakesPartInTheTickOfItsBirth_ProgramListsClockPhysics()
    {
        using var arc = Arc("m");
        var shot = new Shot(arc);
        arc.OneFrame(); // settles (no shot yet)
        shot.Tick = 0;
        shot.Arm();
        arc.OneFrame();

        Assert.Equal(1, shot.HookCalls);
        var born = shot.Born!;
        Assert.NotNull(born);
        var tick = shot.Tick;

        Assert.Equal(EntityStatus.Loaded, shot.At(tick, "programs").Status);   // born in MovePlayer, first of the block: Loaded when the programs begin.
        var afterPrograms = shot.At(tick, "pending");
        Assert.Equal(EntityStatus.Normal, afterPrograms.Status);               // BT-M: status 2 at the events pass of the tick.
        Assert.Equal(-1, afterPrograms.EventTrigger);                          // BT-M: trigger -1 once the program ran.
        Assert.Equal(1, afterPrograms.Aruns);                                  // BT-M: A runs once at tick 1.
        Assert.Equal(0, shot.At(tick, "counters").FrameCounter);
        Assert.Equal(1, shot.At(tick, "lists").FrameCounter);                  // BT-M: +0x1C = 1 at the counters pass.
        var afterLists = shot.At(tick, "clock");
        Assert.True(afterLists.InUpdateList);                                  // BT-M: in the lists after the lists pass.
        Assert.True(afterLists.InHitList);
        Assert.False(afterLists.ClockRan);
        Assert.Equal(BirthClockOracle.Cursor("WAVE", 0), shot.At(tick, "physics").ClockTicks); // E20.e0a5: the clock saw the entity once, and that tick COUNTS (call number 1 of the binary, cnt1c; ClockRan is only the mark of an uncounted switch tick).
        var startX = shot.At(tick, "physics").PosX;
        Assert.Equal(startX + (3 << 16), shot.At(tick, "activeEffects").PosX); // the physics of the tick moved it: 3 px for the arrow facing right.
        Assert.False(shot.At(tick, "hit").Integrated);                         // not integrated by the engine during its birth tick (D-E20-35).

        // Tick 2: A does not run again (BT-M: 1 / 0), the arrow moves 6 px.
        arc.OneFrame();
        Assert.True(ArcRun.EntityOf(born)!.World != null);
        Assert.Equal(1, shot.ARuns);
        Assert.Equal(startX + (9 << 16), born.PosX);
        Assert.Equal(2, born.FrameCounter);
    }

    [Fact]
    public void BT_M_BornInTheFirstTickOfATwoTickFrame_ClockAndPhysicsRunTwice_AndANativeBirthIsNotRunTwice()
    {
        using var arc = Arc("m2");
        var shot = new Shot(arc);
        arc.OneFrame();
        shot.Tick = 0;
        shot.Arm();
        arc.OneFrame(0.04f);

        var born = shot.Born!;
        Assert.NotNull(born);
        var first = shot.Tick - 1;
        var second = shot.Tick;
        var startX = shot.At(first, "physics").PosX;
        Assert.False(shot.At(second, "hit").Integrated);                        // still not integrated at the second tick of the frame.
        Assert.Equal(1, shot.ARuns);
        Assert.Equal(BirthClockOracle.Cursor("WAVE", 1), shot.At(second, "physics").ClockTicks); // E20.e0a5: two counted ticks, the first call and the second (cnt1c 1 then 2), no uncounted switch tick.
        Assert.Equal(2, shot.At(second, "activeEffects").FrameCounter);
        Assert.Equal(startX + (9 << 16), born.PosX);                            // 3 px, then 6 px: the physics ran twice.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b) BT-H1, BT-H4, BT-H6 (gap D-E20-34): born in the native program of another entity.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class Carrier
    {
        public AlundraEntityScriptProxy? Child;
        public int ChildRuns;
        public bool Spawned;
        public int CarrierTick;
    }

    private static Carrier MountCarrier(ArcRun arc, out AlundraEntityScriptProxy carrier)
    {
        var c = new Carrier();
        const int childIndex = 7;
        void Body(AlundraEntityScriptProxy entity)
        {
            if (entity.SpriteType == Arrow)
            {
                if (!c.Spawned)
                {
                    c.Spawned = true;
                    c.Child = Context(arc).SpawnEntityBySpriteIndex(entity, false, childIndex, entity.PosX, entity.PosY, entity.PosZ, 0x10);
                }
            }
            else if (entity.SpriteType == childIndex)
            {
                c.ChildRuns++;
            }
        }

        WrapRunner(arc, Body);
        carrier = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 100 << 16, 100 << 16, 0, 0x10)!;
        Assert.NotNull(carrier);
        return c;
    }

    [Fact]
    public void BT_H_BornInTheNativeProgramOfAnotherEntity_RunsFromTheNextTick_TheGapOfD_E20_34_OneTickFrames()
    {
        using var arc = Arc("h1");
        var c = MountCarrier(arc, out _);
        arc.OneFrame(); // the carrier is integrated at the head of this frame; its program A (tick 1) spawns the child.
        Assert.True(c.Spawned);
        var child = c.Child!;
        Assert.NotNull(child);
        Assert.Equal(EntityStatus.Loaded, child.Status);   // Loaded at the end of the tick of its birth.
        Assert.Equal(0, c.ChildRuns);                      // A: not in the tick of the birth.
        Assert.Equal(1, child.FrameCounter);               // as the oracle: the counters pass has no status filter, +0x1C is 1 at tick 1 even for the reported rows.

        arc.OneFrame();
        Assert.Equal(EntityStatus.Normal, child.Status);   // A at the next tick (integrated at the head of this frame).
        Assert.Equal(1, c.ChildRuns);
        Assert.Equal(2, child.FrameCounter);
    }

    [Fact]
    public void BT_H_BornInTheNativeProgramOfAnotherEntity_InATwoTickFrame_RunsAtTheSecondTick_NotInTheFirst()
    {
        using var arc = Arc("h2");
        var c = MountCarrier(arc, out _);
        var childRunsByTick = new List<(int Tick, int Runs)>();
        var tick = 0;
        arc.Proxy.TickTraceForTests = pass =>
        {
            if (pass == "pad")
            {
                tick++;
            }

            if (pass == "counters")
            {
                childRunsByTick.Add((tick, c.ChildRuns));
            }
        };

        arc.OneFrame(0.04f);
        Assert.NotNull(c.Child);
        Assert.Equal(new[] { (1, 0), (2, 1) }, childRunsByTick); // nothing at the tick of the birth, A at the tick after.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (c) a world with ticks of its own: no participation.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class SelfDrivenHost : IAlundraScriptHost
    {
        public IEventProgramRunner Runner { get; set; } = null!;
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    [Fact]
    public void AHostWithTicksOfItsOwn_HasNoCollisionFieldByDefault_AndTheMarkerChangesNothingToThe_Read()
    {
        IAlundraScriptHost host = new SelfDrivenHost();
        Assert.False(host.DrivesEntityTicks);
        Assert.Null(host.CollisionField);
        var entity = new Entity { Name = "marked", GameplayProxyClassName = nameof(AlundraEntityScriptProxy) };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.ScriptHost = host;
        proxy.JoinsBirthTick = true;
        Assert.Null(entity.World);
        Assert.Null(proxy.ReadCollisionField()); // nothing to fall back on, and no exception.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d) destroyed in the tick of its birth.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void DestroyedInTheTickOfItsBirth_InATwoTickFrame_IsRecycledAtTheHeadOfTheNextTick_ItsSlotIsGivenBack_AndTheEngineIntegratesNothingWrong()
    {
        using var arc = Arc("d");
        var shot = new Shot(arc);
        arc.OneFrame();
        shot.Tick = 0;
        var destroyedAtTick = -1;
        var slotBefore = -1;
        // The handler of A (already registered by the Shot) counts; a second hook of the passes destroys at the programs of the birth tick, once the newborn has run.
        var passHook = arc.Proxy.TickTraceForTests;
        arc.Proxy.TickTraceForTests = pass =>
        {
            passHook?.Invoke(pass);
            if (pass == "pending" && shot.Born != null && destroyedAtTick < 0)
            {
                destroyedAtTick = shot.Tick;
                slotBefore = shot.Born.Slot;
                Context(arc).DestroyEntity(shot.Born);
            }
        };

        shot.Arm();
        arc.OneFrame(0.04f);
        var born = shot.Born!;
        Assert.NotNull(born);
        Assert.Equal(1, shot.ARuns);                       // it ran in the tick of its birth ...
        Assert.DoesNotContain(born, arc.Entities);         // ... and was recycled at the head of the next tick (the map-events step of tick 2).
        var next = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 140 << 16, 100 << 16, 0, Direction)!;
        Assert.NotNull(next);
        Assert.Equal(slotBefore, next.Slot);               // the slot was given back.
        Assert.Null(Record.Exception(() => arc.OneFrame())); // the engine integrates what is left without an exception.
        Assert.Null(Record.Exception(() => arc.OneFrame()));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (e) the sound of the creation, once.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private sealed class RecordingSoundPlayer : IAlundraSoundPlayer
    {
        public List<int> Ids { get; } = new();

        public void PlaySfx(int sfxId) => Ids.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    [Fact]
    public void TheSoundOfTheCreation_IsRequestedOnce_WhenTheEntityTakesPartInTheTickOfItsBirth()
    {
        using var arc = Arc("sound");
        var player = new RecordingSoundPlayer();
        typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, player);
        var shot = new Shot(arc, spriteIndex: 7); // the sprite 7 asks the sound 0x21 at its creation.
        arc.OneFrame();
        shot.Arm();
        arc.OneFrame(0.04f);
        arc.OneFrame();
        arc.OneFrame();
        Assert.NotNull(shot.Born);
        Assert.Single(player.Ids, id => id == 0x21);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (f) the fallback on the collision field of the host.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCreation_NeverReadsTheCollisionFieldOfTheHostByTheFallback_TheBirthTickDoes_AndAnEntityFromARecordOnlyWhenAScriptOpcodeMadeIt()
    {
        using var arc = Arc("spy");
        var loaded = arc.Entities.ToList(); // everything the load of the map made, from records.
        var reads = 0;
        arc.Proxy.CollisionFieldFallbackReadForTests = () => reads++;

        // Between the call and the return of the interface: zero reads (the marker is posed last).
        var direct = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 150 << 16, 100 << 16, 0, Direction)!;
        Assert.NotNull(direct);
        Assert.Equal(0, reads);
        Assert.True(direct.JoinsBirthTick);

        // The entity born in the tick of MovePlayer reads it (the physics of the tick, before the engine integrated it).
        var shot = new Shot(arc);
        arc.OneFrame();
        reads = 0;
        shot.Arm();
        arc.OneFrame();
        Assert.NotNull(shot.Born);
        Assert.True(reads > 0);

        // Once integrated, the world's own field is read: no more reads by the fallback.
        reads = 0;
        arc.OneFrame();
        Assert.Equal(0, reads);

        // An entity from a record: the load of the map never poses the marker; the opcodes of the scripts that made the others of the map pose it (E20.e0a4: 0x2D, 0x8A and 0x8B, posed last, so no fallback during the
        // creation); the direct call of SpawnEntityByRecordId, which is not an opcode, never does: no marker, no fallback, ever.
        Assert.All(loaded, e => Assert.False(e.JoinsBirthTick));
        var byScript = arc.Entities.Where(e => e.EntityRefId >= 0 && !loaded.Contains(e)).ToList();
        Assert.NotEmpty(byScript);
        Assert.All(byScript, e => Assert.True(e.JoinsBirthTick));
        var byRecord = arc.Proxy.SpawnEntityByRecordId(arc.Hero, 0);
        Assert.NotNull(byRecord);
        Assert.False(byRecord!.JoinsBirthTick);
        Assert.Null(ArcRun.EntityOf(byRecord)!.World);
        Assert.Null(byRecord.ReadCollisionField());
        arc.OneFrame();
        Assert.False(byRecord.JoinsBirthTick);
    }

    [Fact]
    public void AnEntityWithoutTheMarker_KeysItsKinematicsOnItsCurrentAnimation_NeverOnTheTarget_EvenWithCurrentStillNotValidated()
    {
        // The line of D-E0A-8 is guarded by the marker: the same entity without it does not move (the measurement of P-A: X = 150, 150, 150). The entities of the map load never carry the marker; an entity made
        // from a record by a script opcode does since E20.e0a4 and takes the speed of its target (B6 of AlundraRecordBirthTickTests).
        using var arc = Arc("guard");
        var proxy = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 150 << 16, 100 << 16, 0, Direction)!;
        proxy.JoinsBirthTick = false;
        Assert.Equal(~0u, proxy.CurrentAnimationId);
        var x = new List<int>();
        for (var t = 0; t < 3; t++)
        {
            AlundraScriptedMotion.TickScriptedNpc(proxy);
            x.Add(proxy.PosX >> 16);
        }

        Assert.Equal(new[] { 150, 150, 150 }, x);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (g) the four directions of T0'.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(3, 0x00, 0, 0x41)]
    [InlineData(3, 0x08, 2, 0x41)]
    [InlineData(3, 0x10, 1, 0x41)]
    [InlineData(3, 0x18, 3, 0x41)]
    [InlineData(20, 0x00, 0, 0xC0)]
    [InlineData(20, 0x08, 2, 0xC0)]
    [InlineData(20, 0x10, 1, 0xC0)]
    [InlineData(20, 0x18, 3, 0xC0)]
    public void T0Prime_AnimFlagsAtBirth_ThenTheFirstFrameValidatesThePendingSwitch_InTheFourDirections(int sprite, int direction, int animationDirection, int animFlags)
    {
        using var arc = Arc($"t0p-{sprite}-{direction}");
        var proxy = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, sprite, 150 << 16, 100 << 16, 0, direction)!;
        Assert.NotNull(proxy);

        // At birth: the byte of the animation 0 is loaded as the first switch of the binary does; the switch itself stays pending.
        Assert.Equal(animFlags, proxy.AnimFlags);
        Assert.True(proxy.SpawnAnimationActive);
        Assert.Equal(~0u, proxy.CurrentAnimationId);
        Assert.Equal(~(uint)direction, proxy.CurrentDirection);
        Assert.True(AlundraFrameSyncPasses.TryResolveAnimationTarget(proxy, out _, out _)); // pending in the four directions (a switch with Current = Target would not be for the direction 0).

        arc.OneFrame();
        Assert.False(proxy.SpawnAnimationActive);
        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(animationDirection, proxy.AnimationDirection);
        Assert.Equal(animFlags, proxy.AnimFlags); // reloaded by the validation, the same value.
        Assert.Equal(animFlags, proxy.HitAnimByte);
        Assert.Equal(0, proxy.IsZForceApplied); // no impulse for the animation of an appearance.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The pins of the confinement.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheMarker_IsPosedByTheInterface_NeverByTheLoadOfTheMap_AndNotCloned()
    {
        using var arc = Arc("marker");
        Assert.All(arc.Entities, e => Assert.False(e.JoinsBirthTick)); // everything the load of the map spawned, from records (the opcodes that make an entity from a record pose it, E20.e0a4).
        var born = Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 150 << 16, 100 << 16, 0, Direction)!;
        Assert.True(born.JoinsBirthTick);
        var clone = ArcRun.EntityOf(born)!.Clone();
        Assert.False(((AlundraEntityScriptProxy)clone.GameplayProxy!).JoinsBirthTick);
    }
}
