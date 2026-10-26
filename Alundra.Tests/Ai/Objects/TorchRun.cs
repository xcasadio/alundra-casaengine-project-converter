#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Support;
using Xunit;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T2 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c2", the world tests (w1) to (w7) and (w9)): the bench of the torch, the probe bench of T0 (<c>docs/plan-e20-annexe/e20-g2c2/probes/G2c2T0ProbeTests.cs.txt</c>) made a support. The REAL world of an exported map (the map 92 of Magyscar by default: the arcs' production path, real prefabs and
/// controllers), emptied of every entity but the hero, on a synthetic field (<see cref="SyntheticField"/>, the cells of the scenario), the native handlers OFF and the arena switch on for the types asked for only (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: the torch 393 and the flame 12 by default, so that
/// the production registry decides what they do; NOTHING is registered by the bench). The torch is carried (put in the hands by <see cref="PlayerManager.PutInHands"/>, thrown by the real hero with the Square) or placed; the map programs of the records the tests ask for (the lever, the switches) run for real.
/// Every tick of every frame is sampled at its "inventory" pass, whatever the number of ticks of the frame: the tracked entities, the flames (sprite type 12) present, the hero, the sounds, the sparks and the effects the stage created.
/// </summary>
internal sealed class TorchRun : IDisposable
{
    internal const int FlameType = 12;

    private readonly List<Track> _tracks = new();
    private int _tick;
    private bool _recording;
    private int _sparksOfTheTick;

    internal readonly ArcRun Arc;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The flames (sprite type 12) present in the world at the end of each tick.</summary>
    internal readonly Dictionary<int, List<Sample>> Flames = new();

    /// <summary>The torches (sprite type 393) present in the world at the end of each tick.</summary>
    internal readonly Dictionary<int, List<Sample>> Torches = new();

    /// <summary>Every flame and every torch that was in the world at the end of a tick, in the order they were first seen (distinct entities).</summary>
    internal readonly List<EntityScriptProxy> SeenFlames = new();
    internal readonly List<EntityScriptProxy> SeenTorches = new();

    /// <summary>The state of the hero at the end of each tick: life, target animation.</summary>
    internal readonly Dictionary<int, (int Hp, uint Animation)> Hero = new();

    /// <summary>The sparks the hit pass asked for, by tick.</summary>
    internal readonly Dictionary<int, int> Sparks = new();

    /// <summary>The effects the stage held at the end of a tick, each once (anchored or not): the tick, the identifier and the position in pixels.</summary>
    internal readonly List<(int Tick, int Id, double X, double Y, double Z)> Effects = new();

    private readonly HashSet<(int Index, int Generation)> _seenEffects = new();

    /// <summary>The last tick that ran (the last frame is whole: it can end after the tick asked).</summary>
    internal int LastTick;

    /// <summary>The state of an entity at the end of a tick (positions in pixels).</summary>
    internal readonly record struct Sample(bool Present, EntityStatus Status, uint Animation, int Reset, uint Flags, int OnGround, double X, double Y, double Z, int HitCounter, byte Mark, string Codes, bool Touched)
    {
        internal static readonly Sample Absent = default;
    }

    /// <summary>The samples of one tracked entity (the proxy stays valid after the recycling of its slot).</summary>
    internal sealed class Track
    {
        internal Track(EntityScriptProxy entity) => Entity = entity;

        internal EntityScriptProxy Entity { get; }

        internal Dictionary<int, Sample> Ticks { get; } = new();

        /// <summary>The sample at the end of <paramref name="tick"/>; an entity that was not sampled then reads as absent.</summary>
        internal Sample At(int tick) => Ticks.TryGetValue(tick, out var s) ? s : Sample.Absent;

        /// <summary>The first tick the entity is sampled in the status <paramref name="status"/>, or -1.</summary>
        internal int FirstTickIn(EntityStatus status) => Ticks.Where(t => t.Value.Present && t.Value.Status == status).Select(t => t.Key).DefaultIfEmpty(-1).Min();

        /// <summary>The first tick the entity is not in the world any more after having been there, or -1.</summary>
        internal int FirstTickGone() => Ticks.Where(t => !t.Value.Present).Select(t => t.Key).DefaultIfEmpty(-1).Min();
    }

    /// <summary>The sounds the world played, with the tick of the call.</summary>
    internal sealed class Sounds : ISoundPlayer
    {
        internal int Tick;
        internal readonly List<(int Tick, int Sfx)> Played = new();

        public void PlaySfx(int sfxId) => Played.Add((Tick, sfxId));

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

    /// <param name="cell">The cells of the field (default: all zero, flat ground).</param>
    /// <param name="types">The sprite types the native handlers run for (default: the torch and the flame).</param>
    /// <param name="synthetic">False: the field of the real map stays (the torches placed on its floor stay at rest), and <paramref name="cell"/> is ignored.</param>
    internal TorchRun(string name, Func<int, int, CutBench.Cell>? cell = null, int heroX = 109, int heroY = 100, int[]? types = null, string zone = "Magyscar", string world = "Magyscar-92", bool synthetic = true)
    {
        var spec = new ArcSpec("E20g2c2-" + name, zone, world, Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        Arc = new ArcRun(spec);
        try
        {
            Frames(4);
            Bench = synthetic ? SyntheticField.Install(Arc, cell) : null!;
            foreach (var other in Arc.Entities.Where(e => !ReferenceEquals(e, Arc.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Heard);
            Arc.Runner.NativeHandlersEnabled = false;
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int>(types ?? new[] { 393, FlameType });
            Arc.Hero.TargetDirection = 24;
            Arc.PlaceHero(heroX, heroY, 0);
            Frames(2);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal IEntityWorldContext World => Arc.Proxy;

    /// <summary>Frames of one tick with nothing pressed (the run is not recording).</summary>
    internal void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>A torch (map-page sprite 137) born on the head of the hero by the interface, put in his hands, the carry stand reached (the probe P5 of T0).</summary>
    internal EntityScriptProxy CarryATorch()
    {
        var torch = World.SpawnEntityBySpriteIndex(null, true, 137, Arc.Hero.PosX, Arc.Hero.PosY, 32 << 16, 0x10) ?? throw new InvalidOperationException("the torch was not born");
        torch.RidingEntity = Arc.Hero.LogicContextEntity;
        PlayerManager.PutInHands(Arc.Hero, torch);
        Frames(8);
        Assert.Equal(12u, Arc.Hero.TargetAnimationId);
        Assert.NotNull(Arc.Hero.CarriedEntity);
        return torch;
    }

    /// <summary>A torch born at rest by the interface (the marker of the entities born during play) at the pixels given, <paramref name="deactivateOnImpact"/> posing the bit 0x10 (what the rain of the lever does).</summary>
    internal EntityScriptProxy PlaceATorch(int x, int y, int z, bool deactivateOnImpact)
    {
        var torch = World.SpawnEntityBySpriteIndex(null, true, 137, x << 16, y << 16, z << 16, 0x10) ?? throw new InvalidOperationException("the torch was not born");
        if (deactivateOnImpact)
        {
            torch.Flags |= EntityFlags.DeactivateOnImpact;
        }

        return torch;
    }

    /// <summary>A target of the class A born at the pixels given: the "Interrupteur (allumage)" of the page of the map 92 (map-page sprite 51), born by the interface so that it has no program (inert here): the target of a thrown torch, as the switch of the oracle's <c>p_hitswitch</c>.</summary>
    internal EntityScriptProxy PlaceATarget(int x, int y)
        => World.SpawnEntityBySpriteIndex(null, true, 51, x << 16, y << 16, 0, 0x10) ?? throw new InvalidOperationException("the target was not born");

    /// <summary>Starts to sample <paramref name="entity"/> at every tick of the next <see cref="Go"/>.</summary>
    internal Track Watch(EntityScriptProxy entity)
    {
        var track = new Track(entity);
        _tracks.Add(track);
        return track;
    }

    private Sample Snapshot(EntityScriptProxy e) => new(
        Arc.Entities.Contains(e), e.Status, e.TargetAnimationId, e.ForceResetAnimationFlag, e.Flags, e.IsOnGround,
        e.PosX / 65536.0, e.PosY / 65536.0, e.PosZ / 65536.0, e.HitCounter, e.Bytes[0], string.Join("/", e.SpriteProgramIndexes), e.TouchingEntity != null);

    /// <summary>
    /// Plays the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks; the pad of a frame is <paramref name="pad"/> of the tick that opens it (default: nothing); <paramref name="beforeFrame"/> is called at the head of each frame with the tick that opens it.
    /// </summary>
    internal void Go(int ticksPerFrame, int lastTick, Func<int, uint>? pad = null, Action<int>? beforeFrame = null)
    {
        Arc.Proxy.HitSparksForTests = (_, _, _) => _sparksOfTheTick++;
        Arc.Proxy.TickTraceForTests = OnPass;
        _tick = 0;
        _recording = true;
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            beforeFrame?.Invoke(tick);
            Arc.HoldDirections(pad == null ? 0u : pad(tick));
            Arc.OneFrame(ticksPerFrame * 0.02f);
            LastTick = tick + ticksPerFrame - 1;
        }

        _recording = false;
        Arc.Proxy.TickTraceForTests = null;
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
            Heard.Tick = _tick;
            _sparksOfTheTick = 0;
        }
        else if (pass == "inventory")
        {
            foreach (var track in _tracks)
            {
                track.Ticks[_tick] = Snapshot(track.Entity);
            }

            Flames[_tick] = Arc.Entities.Where(e => e.SpriteType == FlameType).Select(Snapshot).ToList();
            Torches[_tick] = Arc.Entities.Where(e => e.SpriteType == 393).Select(Snapshot).ToList();
            foreach (var flame in Arc.Entities.Where(e => e.SpriteType == FlameType && !SeenFlames.Contains(e)))
            {
                SeenFlames.Add(flame);
            }

            foreach (var torch in Arc.Entities.Where(e => e.SpriteType == 393 && !SeenTorches.Contains(e)))
            {
                SeenTorches.Add(torch);
            }

            foreach (var slot in Arc.Proxy.EffectStage!.Reservoir.ActiveSlots)
            {
                if (_seenEffects.Add((slot.Index, slot.Generation)))
                {
                    Effects.Add((_tick, slot.TargetEffect, slot.X / 65536.0, slot.Y / 65536.0, slot.Z / 65536.0));
                }
            }

            Hero[_tick] = (Arc.Hero.Hp, Arc.Hero.TargetAnimationId);
            Sparks[_tick] = _sparksOfTheTick;
        }
    }

    /// <summary>The ticks at whose end at least one flame is in the world.</summary>
    internal List<int> FlameTicks() => Flames.Where(f => f.Value.Count > 0).Select(f => f.Key).OrderBy(t => t).ToList();

    /// <summary>The sound <paramref name="sfx"/> played at the tick <paramref name="tick"/>, how many times.</summary>
    internal int SoundsAt(int tick, int sfx) => Heard.Played.Count(p => p.Tick == tick && p.Sfx == sfx);

    public void Dispose()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        Arc.Dispose();
    }
}
