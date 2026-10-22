#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>What one entity looks like after the passes of one logic tick (the columns of the hit oracle, E20.c1a C5), read from the DLL.</summary>
internal readonly record struct HitEntitySample(
    uint AnimTarget, int AnimByte, bool HasBox, int BoxOffsetX, int BoxOffsetY, int BoxOffsetZ, int BoxWidth, int BoxDepth, int BoxHeight, int AttackAttribute, int AttackPower,
    int Touching, int HitCounter, int DamagedTicks, int CooldownTicks, int Hp, int PosX, int PosY);

/// <summary>The state after one logic tick: the hero, the targets in slot order, the latch of the hero's animation, the sparks the pass asked for this tick (centres) and the programs the events pass of this tick ran.</summary>
internal sealed record HitTickSample(
    int Tick, HitEntitySample Hero, IReadOnlyList<HitEntitySample> Targets, uint Latch, IReadOnlyList<(int X, int Y, int Z)> Sparks, IReadOnlyList<(int Slot, int Letter)> Handlers);

/// <summary>
/// The arena of the hit-pass tests (E20.c1a C5, docs/plan-e20-heros.md section 1.5): the REAL world of map 14 (<c>Overworld 3,1-14</c>, the map the oracle's constants come from), built by the arcs' production path
/// (<see cref="ArcRun"/>, real prefabs, the world's own <c>Update</c> and <see cref="WorldProxy"/>), in which the hero and real Beannoid prefabs (records 0 to 6 of the map, sprite 79) are carried to the flat free
/// cells of the oracle (rows 2 to 7 of the map, height 0, on the ground), at the pixel positions of an oracle trace. Every tick of every frame is sampled after its hit pass (the <c>inventory</c> pass of the tick),
/// whatever the number of ticks of the frame, with the programs the events pass ran (the Beannoid's C and D are native, counted no-ops, as the oracle's stubs).
/// </summary>
internal sealed class HitArena : IDisposable
{
    public const float OneTick = 0.02f;
    private const int Square = 0x80;

    private readonly List<(int X, int Y, int Z)> _tickSparks = new();
    private readonly List<(int Slot, int Letter)> _tickHandlers = new();
    private int _tick;
    private bool _recording;

    public ArcRun Arc { get; }

    public EntityScriptProxy Hero => Arc.Hero;

    public List<EntityScriptProxy> Targets { get; } = new();

    public List<HitTickSample> Samples { get; } = new();

    /// <summary>Told the number of the tick (1 = the first tick of the scenario) at the head of each tick, before the pad: the place for a change of the harness that the binary's passes of that tick must see.</summary>
    public Action<int>? BeforeTick { get; set; }

    /// <summary>Told the number of the tick after it is sampled (after its hit pass and its inventory): a change made here is seen by the NEXT tick's passes, and by the end of the frame's validation of the animations.</summary>
    public Action<int>? AfterTick { get; set; }

    public int HeroStartX { get; private set; }

    public int HeroStartY { get; private set; }

    /// <summary>The arena: <paramref name="heroPixels"/> and the pixels of each of <paramref name="targetPixels"/> as in an oracle trace (<c>meta</c>), the hero facing <paramref name="heroDirection"/> (24 east, 8 west).</summary>
    /// <summary>
    /// <paramref name="naturalTargetCount"/> above 0 (with a null <paramref name="targetPixels"/>): the first records of the map are the targets, WHERE THE MAP PUT THEM (their own pixel and height), and the hero is carried to
    /// (<paramref name="heroPixelX"/>, <paramref name="heroPixelY"/>, <paramref name="heroPixelZ"/>).
    /// </summary>
    public HitArena(string name, int heroPixelX, int heroPixelY, IReadOnlyList<int[]>? targetPixels, uint heroDirection = 24, int heroPixelZ = 0, int naturalTargetCount = 0)
    {
        var spec = new ArcSpec("E20c1a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        Arc = new ArcRun(spec);
        try
        {
            Arc.Proxy.HitSparksForTests = (x, y, z) => _tickSparks.Add((x, y, z));
            // E14.a0 (D-E14-5): the oracle's arena keeps the Beannoids as its stubs (counted no-ops): the registry of the native handlers is off.
            if (Arc.Proxy.EventProgramRunner is EventProgramRunner realRunner)
            {
                realRunner.NativeHandlersEnabled = false;
            }

            Arc.Proxy.EventProgramRunner = new RecordingRunner(Arc.Proxy.EventProgramRunner, this);
            Arc.Proxy.TickTraceForTests = OnPass;

            Frames(4);
            var targetCount = targetPixels?.Count ?? naturalTargetCount;
            for (var i = 0; i < targetCount; i++)
            {
                var proxy = Arc.EntityByRecord(i);
                Assert.NotNull(proxy);
                Assert.Equal(0x100 + 79, proxy!.SpriteType); // a Beannoid of the export (balance index 335).
                Targets.Add(proxy);
            }

            // The arena: the hero and the Beannoids on the flat free cells of the oracle (tile row 6), on the ground (the DLL's Z is the binary's minus 1: 0).
            Hero.TargetDirection = heroDirection;
            Arc.PlaceHero(heroPixelX, heroPixelY, heroPixelZ);
            for (var i = 0; targetPixels != null && i < Targets.Count; i++)
            {
                Place(Targets[i], targetPixels[i][0], targetPixels[i][1]);
            }

            Frames(6);
            Assert.Equal(0u, Hero.TargetAnimationId);
            Assert.Equal(0u, Hero.CurrentAnimationId);
            Assert.Equal(heroPixelX << 16, Hero.PosX);
            Assert.Equal(heroPixelY << 16, Hero.PosY);
            Assert.Equal(heroPixelZ << 16, Hero.PosZ);
            Assert.Equal(1, Hero.IsOnGround);
            foreach (var target in Targets)
            {
                Assert.Equal(targetPixels != null ? 0 : target.PosZ, target.PosZ);
                Assert.Equal(0u, target.TargetAnimationId);
            }

            // The oracle starts from clean counters: a hero carried into the body of a Beannoid was already hit during the settling frames.
            ResetHitState(Hero);
            foreach (var target in Targets)
            {
                ResetHitState(target);
            }

            HeroStartX = Hero.PosX;
            HeroStartY = Hero.PosY;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose() => Arc.Dispose();

    private static void ResetHitState(EntityScriptProxy entity)
    {
        entity.FrameCollisionTickCounter = 0;
        entity.DamagedTickCounter = 0;
        entity.HitCounter = 0;
        entity.TouchingEntity = null;
    }

    /// <summary>Carries an entity to a pixel of the arena, on the ground, as <see cref="ArcRun.PlaceHero"/> does the hero.</summary>
    public static void Place(EntityScriptProxy entity, int pixelX, int pixelY)
    {
        entity.PosX = pixelX << 16;
        entity.PosY = pixelY << 16;
        entity.PosZ = 0;
        entity.TileX = pixelX / 24;
        entity.TileY = pixelY / 16;
        entity.TileZ = 0;
        entity.PushLogicalPositionToRoot();
    }

    /// <summary>Frames of one tick with nothing pressed (the arena is not recording).</summary>
    private void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>Starts the recording: the next tick is the tick 1 of the scenario.</summary>
    public void Begin()
    {
        _tick = 0;
        _recording = true;
        Samples.Clear();
        _tickSparks.Clear();
        _tickHandlers.Clear();
    }

    /// <summary>One frame of <paramref name="ticks"/> logic ticks with the pad word <paramref name="hold"/> held during it (the edges are the tick pad's own).</summary>
    public void Frame(uint hold, int ticks)
    {
        Arc.HoldDirections(hold);
        Arc.OneFrame(ticks * OneTick);
    }

    /// <summary>Runs <paramref name="ticks"/> ticks of one tick per frame with Square pressed at the first tick and nothing else (the scenario of the reach sweep), and returns the samples.</summary>
    public IReadOnlyList<HitTickSample> RunSwing(int ticks)
    {
        Begin();
        Frame(Square, 1);
        for (var i = 1; i < ticks; i++)
        {
            Frame(0, 1);
        }

        return Samples;
    }

    /// <summary>
    /// Runs the ticks of <paramref name="trace"/> in frames of <paramref name="frameSizes"/> ticks (repeated; the last frame is cut to the number of rows): the pad of a frame is the oracle's pad of its first
    /// tick. Returns the samples.
    /// </summary>
    public IReadOnlyList<HitTickSample> Run(HitOracleTrace trace, params int[] frameSizes)
    {
        Begin();
        var tick = 1;
        var frame = 0;
        while (tick <= trace.Rows.Count)
        {
            var size = Math.Min(frameSizes[Math.Min(frame, frameSizes.Length - 1)], trace.Rows.Count - tick + 1);
            Frame(trace.Rows[tick - 1].Hold, size);
            tick += size;
            frame++;
        }

        return Samples;
    }

    /// <summary>What an entity looks like now: the columns the hit oracle holds, read from the DLL (the frame box is evaluated for the tick, as the pass does for an attacker).</summary>
    public HitEntitySample Snapshot(EntityScriptProxy entity)
    {
        entity.EvaluateFrameBox();
        var touching = -1;
        if (entity.TouchingEntity != null)
        {
            if (ReferenceEquals(entity.TouchingEntity, Hero.SelfEntity))
            {
                touching = 0;
            }
            else
            {
                for (var i = 0; i < Targets.Count; i++)
                {
                    if (ReferenceEquals(entity.TouchingEntity, Targets[i].SelfEntity))
                    {
                        touching = i + 1;
                    }
                }
            }
        }

        return new HitEntitySample(
            entity.TargetAnimationId, entity.HitAnimByte, entity.HasFrameBox, entity.CollisionOffsetX, entity.CollisionOffsetY, entity.CollisionOffsetZ, entity.CollisionWidth, entity.CollisionDepth,
            entity.CollisionHeight, entity.CurrentAttack?.Attribute ?? -1, entity.CurrentAttack?.Power ?? -1, touching, entity.HitCounter, entity.DamagedTickCounter,
            entity.FrameCollisionTickCounter, entity.Hp, entity.PosX, entity.PosY);
    }

    private void OnPass(string pass)
    {
        if (!_recording)
        {
            return;
        }

        if (pass == "pad")
        {
            _tick++;
            BeforeTick?.Invoke(_tick);
        }
        else if (pass == "inventory")
        {
            Samples.Add(new HitTickSample(
                _tick, Snapshot(Hero), Targets.Select(Snapshot).ToList(), Arc.Proxy.HeroAnimationLatch, _tickSparks.ToList(), _tickHandlers.ToList()));
            _tickSparks.Clear();
            _tickHandlers.Clear();
            AfterTick?.Invoke(_tick);
        }
    }

    /// <summary>Wraps the world's runner to note which program the events pass ran for each target (<c>EventTrigger</c> at the call: 2 = C, 3 = D).</summary>
    private sealed class RecordingRunner : IEventProgramRunner
    {
        private readonly IEventProgramRunner _inner;
        private readonly HitArena _arena;

        public RecordingRunner(IEventProgramRunner inner, HitArena arena)
        {
            _inner = inner;
            _arena = arena;
        }

        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
            Note(entity);
            _inner.RunScript(entity, programSlot);
        }

        public void RunSpriteEvent(EntityScriptProxy entity)
        {
            Note(entity);
            _inner.RunSpriteEvent(entity);
        }

        private void Note(EntityScriptProxy entity)
        {
            var index = _arena.Targets.IndexOf(entity);
            if (_arena._recording && index >= 0)
            {
                _arena._tickHandlers.Add((index + 1, entity.EventTrigger));
            }
        }
    }
}
