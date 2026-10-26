#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T2 (docs/plan-e20-heros.md section 1.24.5, "E20.g2c3", the world tests (w1) to (w10)): the bench of the drops of water, the probe bench of T0 (<c>docs/plan-e20-annexe/e20-g2c3/probes/G2c3T0ProbeTests.cs.txt</c>) made a support. The REAL world of an exported map (the map 92 of Magyscar by default: real prefabs and controllers, the drop's
/// <c>CharacterControllerComponent</c> of the profile <c>Pawn</c> attached at the birth), emptied of every entity but the hero, on a synthetic flat field, the native handlers OFF and the arena switch on for the types asked for only (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: the drop 391, the torch 393 and the flame 12 by default, so that the
/// production registry decides what they do; NOTHING is registered by the bench). A drop is born the way the opcode <c>0x8A</c> makes one (<see cref="BornAt"/>: the record, the absolute position, the push to the root, the tick joined). Every tick of every frame is sampled at its "inventory" pass, whatever the number of ticks of the frame.
/// </summary>
internal sealed class WaterDropRun : IDisposable
{
    internal const int DropType = 391;

    internal const int TorchType = 393;

    internal const int FlameType = 12;

    private readonly List<Track> _tracks = new();
    private readonly HashSet<(int Index, int Generation)> _seenEffects = new();
    private int _tick;

    internal readonly ArcRun Arc;
    internal readonly Sounds Heard = new();

    /// <summary>The torch the drops are compared with when the support is named (<see cref="Sample.Rides"/>).</summary>
    internal EntityScriptProxy? Torch { get; set; }

    /// <summary>The number of flames (sprite type 12) in the world at the end of each tick.</summary>
    internal readonly Dictionary<int, int> Flames = new();

    /// <summary>The hero at the end of each tick.</summary>
    internal readonly Dictionary<int, HeroSample> Hero = new();

    /// <summary>The effects the stage held at the end of a tick, each once: the tick and the identifier.</summary>
    internal readonly List<(int Tick, int Id)> Effects = new();

    /// <summary>The state of an entity at the end of a tick (positions in pixels; the raw values are the 16.16 words).</summary>
    internal readonly record struct Sample(
        bool Present, EntityStatus Status, byte State, byte Saved, short Counter, uint Animation, int Reset, uint Flags, int OnGround, int CollidedZ, int ForceZ, int PosZRaw, int Program, char Rides, float RootZ, float WantedZ, float RootX, float WantedX)
    {
        internal static readonly Sample Absent = default;

        internal double Z => PosZRaw / 65536.0;

        /// <summary>True when the root of the entity is where its logical position says (the push to the root).</summary>
        internal bool RootFollows => RootZ == WantedZ && RootX == WantedX;
    }

    internal readonly record struct HeroSample(int Hp, uint TargetAnimation, bool Carrying);

    /// <summary>The samples of one tracked entity.</summary>
    internal sealed class Track
    {
        internal Track(EntityScriptProxy entity) => Entity = entity;

        internal EntityScriptProxy Entity { get; }

        internal Dictionary<int, Sample> Ticks { get; } = new();

        internal Sample At(int tick) => Ticks.TryGetValue(tick, out var s) ? s : Sample.Absent;

        /// <summary>The first tick the entity is sampled satisfying <paramref name="predicate"/>, or -1.</summary>
        internal int First(Func<Sample, bool> predicate) => Ticks.Where(t => predicate(t.Value)).Select(t => t.Key).DefaultIfEmpty(-1).Min();

        /// <summary>Every tick the entity is sampled satisfying <paramref name="predicate"/>, in order.</summary>
        internal List<int> All(Func<Sample, bool> predicate) => Ticks.Where(t => predicate(t.Value)).Select(t => t.Key).OrderBy(t => t).ToList();
    }

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

    /// <param name="types">The sprite types the native handlers run for (default: the drop, the torch and the flame).</param>
    internal WaterDropRun(string name, int heroX = 100, int heroY = 100, int[]? types = null, string zone = "Magyscar", string world = "Magyscar-92")
    {
        var spec = new ArcSpec("E20g2c3-" + name, zone, world, Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        Arc = new ArcRun(spec);
        try
        {
            Frames(4);
            SyntheticField.Install(Arc, null);
            foreach (var other in Arc.Entities.Where(e => !ReferenceEquals(e, Arc.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Heard);
            Arc.Runner.NativeHandlersEnabled = false;
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int>(types ?? new[] { DropType, TorchType, FlameType });
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

    internal void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>A drop born the way the opcode <c>0x8A</c> makes one: the record, the absolute position, the push to the root, the tick joined.</summary>
    internal EntityScriptProxy BornAt(int record, int xPx, int yPx, int zPx)
    {
        var drop = World.SpawnEntityByRecordId(Arc.Hero, record) ?? throw new InvalidOperationException("the record " + record + " was not born");
        drop.PosX = xPx << 16;
        drop.PosY = yPx << 16;
        drop.PosZ = zPx << 16;
        drop.PushLogicalPositionToRoot();
        drop.JoinsBirthTick = true;
        return drop;
    }

    /// <summary>A torch (map-page sprite 137) born at rest by the interface at the pixels given.</summary>
    internal EntityScriptProxy PlaceATorch(int x, int y, int z)
        => World.SpawnEntityBySpriteIndex(null, true, 137, x << 16, y << 16, z << 16, 0x10) ?? throw new InvalidOperationException("the torch was not born");

    /// <summary>A torch born on the head of the hero by the interface and put in his hands, the carry stand reached.</summary>
    internal EntityScriptProxy CarryATorch()
    {
        var torch = World.SpawnEntityBySpriteIndex(null, true, 137, Arc.Hero.PosX, Arc.Hero.PosY, 32 << 16, 0x10) ?? throw new InvalidOperationException("the torch was not born");
        torch.RidingEntity = Arc.Hero.LogicContextEntity;
        PlayerManager.PutInHands(Arc.Hero, torch);
        Frames(8);
        Torch = torch;
        return torch;
    }

    internal Track Watch(EntityScriptProxy entity)
    {
        var track = new Track(entity);
        _tracks.Add(track);
        return track;
    }

    private char RidesOf(EntityScriptProxy e)
    {
        var support = e.RidingEntity;
        if (support == null)
        {
            return '-';
        }

        if (ReferenceEquals(support, Arc.Hero.LogicContextEntity))
        {
            return 'H';
        }

        return Torch != null && ReferenceEquals(support, Torch.LogicContextEntity) ? 'T' : '?';
    }

    private Sample Snapshot(EntityScriptProxy e)
    {
        var root = e.OwnerEntity?.RootComponent;
        var wanted = EntitySpawnFactory.ResolveLogicalPosition(e.PosX, e.PosY, e.PosZ);
        return new Sample(
            Arc.Entities.Contains(e), e.Status, e.Bytes[0], e.Bytes[1], (short)e.AIValues[1], e.TargetAnimationId, e.ForceResetAnimationFlag, e.Flags, e.IsOnGround, e.CollidedWithEntityZ, e.ForceZ, e.PosZ, e.ProgramIndexes[2], RidesOf(e),
            root?.LocalTransform.Position.Z ?? float.NaN, wanted.Z, root?.LocalTransform.Position.X ?? float.NaN, wanted.X);
    }

    /// <summary>Plays the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks; <paramref name="pad"/> is the pad of the frame from the tick that opens it; <paramref name="beforeFrame"/> is called at the head of each frame with that tick.</summary>
    internal void Go(int ticksPerFrame, int lastTick, Func<int, uint>? pad = null, Action<int>? beforeFrame = null)
    {
        Arc.Proxy.TickTraceForTests = OnPass;
        _tick = 0;
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            beforeFrame?.Invoke(tick);
            Arc.HoldDirections(pad == null ? 0u : pad(tick));
            Arc.OneFrame(ticksPerFrame * 0.02f);
        }

        Arc.Proxy.TickTraceForTests = null;
    }

    private void OnPass(string pass)
    {
        if (pass == "pad")
        {
            _tick++;
            Heard.Tick = _tick;
        }
        else if (pass == "inventory")
        {
            foreach (var track in _tracks)
            {
                track.Ticks[_tick] = Snapshot(track.Entity);
            }

            Flames[_tick] = Arc.Entities.Count(e => e.SpriteType == FlameType);
            Hero[_tick] = new HeroSample(Arc.Hero.Hp, Arc.Hero.TargetAnimationId, Arc.Hero.CarriedEntity != null);
            foreach (var slot in Arc.Proxy.EffectStage!.Reservoir.ActiveSlots)
            {
                if (_seenEffects.Add((slot.Index, slot.Generation)))
                {
                    Effects.Add((_tick, slot.TargetEffect));
                }
            }
        }
    }

    /// <summary>The sound <paramref name="sfx"/> played at the tick <paramref name="tick"/>, how many times.</summary>
    internal int SoundsAt(int tick, int sfx) => Heard.Played.Count(p => p.Tick == tick && p.Sfx == sfx);

    /// <summary>The ticks the sound <paramref name="sfx"/> was played, in order.</summary>
    internal List<int> SoundTicks(int sfx) => Heard.Played.Where(p => p.Sfx == sfx).Select(p => p.Tick).ToList();

    public void Dispose()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        Arc.Dispose();
    }
}
