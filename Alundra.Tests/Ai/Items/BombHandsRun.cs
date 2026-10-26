#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Ai.Items;
using Alundra.Audio;
using Alundra.Hero;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Tests.Support;
using Xunit;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3b T2 (docs/plan-e20-heros.md section 1.30.3, the tests of the world (w1) to (w11)): the bench of the bomb in the hands of the REAL hero. The real world of map 14 built by the arcs' production path
/// (<see cref="ArcRun"/>: real prefabs, the real hero controller, the world's own <c>Update</c> and <see cref="WorldProxy"/>), the cells of the scenario installed by <see cref="SyntheticField"/>, every entity of the
/// map but the hero removed, the native handlers ON (the registry of production decides what the bombs do: NOTHING is registered by the bench), the hero put at the pixel of the scenario and the bomb given
/// (<see cref="ArcRun.Give"/> 35) and equipped on the Circle (<see cref="ArcRun.SetItem"/> 35). The pad of a tick is the oracle's <c>hold(t)</c>: a lone Circle or Square goes through <see cref="ArcRun.Press"/>,
/// anything else through <see cref="ArcRun.HoldDirections"/> (one tick per frame, so the per-frame step of the generator is the oracle's own). Every tick is sampled at its "inventory" pass: the hero, every bomb
/// (the sprite type 2, numbered in the order of their births: a slot that is recycled and born again is a new bomb), the sounds the world played, the effects the stage created (with the forces the code wrote into
/// them) and the sparks of the hit pass.
/// </summary>
internal sealed class BombHandsRun : IDisposable
{
    internal const uint Circle = PadState.Circle;
    internal const uint Square = PadState.Square;
    internal const uint Cross = PadState.Cross;
    internal const uint Up = PadState.Up;
    internal const uint Down = PadState.Down;
    internal const uint Left = PadState.Left;
    internal const uint Right = PadState.Right;
    internal const uint Start = PadState.Start;

    /// <summary>The seed of the generator of the oracle at the first tick of every key (<c>spellworld.make(seed=0x12345678)</c>).</summary>
    internal const uint Seed = 0x12345678;

    private readonly List<EntityScriptProxy> _births = new();
    private readonly HashSet<EntityScriptProxy> _present = new();
    private readonly HashSet<(int Index, int Generation)> _seenEffects = new();
    private int _tick;
    private bool _recording;
    private int _sparksOfTheTick;

    internal readonly ArcRun Arc;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The state of the hero at the end of each tick.</summary>
    internal readonly Dictionary<int, HeroTick> Hero = new();

    /// <summary>The state of each bomb at the end of each tick: the key is the tick, the list is indexed by the order of the births (a bomb that has left the world reads as absent).</summary>
    internal readonly Dictionary<int, List<BombTick>> Bombs = new();

    /// <summary>The effects the stage held for the first time at the end of a tick.</summary>
    internal readonly List<EffectTick> Effects = new();

    /// <summary>The sparks of the hit pass (the wound of the hero), by tick: one set of three per hit.</summary>
    internal readonly Dictionary<int, List<(int X, int Y, int Z)>> HitSparks = new();

    /// <summary>The last tick that ran.</summary>
    internal int LastTick;

    /// <summary>The state of the hero at the end of a tick (positions in pixels).</summary>
    internal readonly record struct HeroTick(int Hp, uint State, double X, double Y, double Z, bool Carrying, uint ItemInUse, int DamagedTickCounter);

    /// <summary>The state of a bomb at the end of a tick (positions in pixels; the fuse is <c>+0x274</c>).</summary>
    internal readonly record struct BombTick(bool Present, int Status, uint TargetAnimation, int End, int Fuse, double X, double Y, double Z, bool Linked, uint Direction, int OnGround)
    {
        internal static readonly BombTick Absent = default;
    }

    /// <summary>An effect the stage held for the first time at the end of the tick <paramref name="Tick"/> (the words are the raw 16.16 words of the slot).</summary>
    internal readonly record struct EffectTick(int Tick, int Id, int X, int Y, int Z, int ForceX, int ForceY, int Mode);

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
    /// <param name="heroDirection">24 east, 8 west, 16 up, 0 down.</param>
    /// <param name="extraItems">The other items given (the boots <c>0x1B</c> of the swim).</param>
    internal BombHandsRun(string name, int heroX = 100, int heroY = 100, Func<int, int, CutBench.Cell>? cell = null, uint heroDirection = 24, int[]? extraItems = null)
    {
        var spec = new ArcSpec("E20h3b-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        Arc = new ArcRun(spec);
        try
        {
            Frames(4);
            Bench = SyntheticField.Install(Arc, cell);
            foreach (var other in Arc.Entities.Where(e => !ReferenceEquals(e, Arc.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Heard);
            Arc.Runner.NativeHandlersEnabled = true;
            Arc.Hero.TargetDirection = heroDirection;
            Arc.PlaceHero(heroX, heroY, 0);
            Frames(6);
            Assert.Equal(0u, Arc.Hero.TargetAnimationId);
            Assert.Equal(heroX << 16, Arc.Hero.PosX);
            Assert.Equal(heroY << 16, Arc.Hero.PosY);
            Assert.Equal(1, Arc.Hero.IsOnGround);

            // The bomb in the inventory and on the Circle (the oracle's make(0x23)); the boots of the swim when asked for.
            Arc.Give(35);
            foreach (var item in extraItems ?? Array.Empty<int>())
            {
                Arc.Give(item);
            }

            Arc.SetItem(35);
            Arc.Hero.FrameCollisionTickCounter = 0;
            Arc.Hero.DamagedTickCounter = 0;
            Arc.Hero.HitCounter = 0;
            Arc.Hero.TouchingEntity = null;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal EntityScriptProxy HeroProxy => Arc.Hero;

    /// <summary>Frames of one tick with nothing pressed (the run is not recording).</summary>
    private void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>
    /// Plays the ticks 1 to <paramref name="lastTick"/>, one tick per frame; the pad of the tick t is <paramref name="pad"/>(t); <paramref name="beforeTick"/> is told the number of the tick at the head of the tick,
    /// right after the pad, before the passes of the binary (the place for a write of the harness the tick must see); with <paramref name="seeded"/> the generator is posed at the seed of the oracle at the first tick.
    /// </summary>
    internal void Go(int lastTick, Func<int, uint> pad, Action<int>? beforeTick = null, bool seeded = false)
    {
        Arc.Proxy.HitSparksForTests = (x, y, z) =>
        {
            _sparksOfTheTick++;
            if (!HitSparks.TryGetValue(_tick, out var list))
            {
                HitSparks[_tick] = list = new List<(int, int, int)>();
            }

            list.Add((x, y, z));
        };
        Arc.Proxy.TickTraceForTests = pass => OnPass(pass, beforeTick, seeded);
        _tick = 0;
        _recording = true;
        for (var tick = 1; tick <= lastTick; tick++)
        {
            var hold = pad(tick);
            if (hold == Circle || hold == Square)
            {
                Arc.HoldDirections(0);
                Arc.Press(hold);
            }
            else
            {
                Arc.HoldDirections(hold);
                Arc.OneFrame();
            }

            LastTick = tick;
            ScanEffects(tick);
        }

        _recording = false;
        Arc.Proxy.TickTraceForTests = null;
        Arc.HoldDirections(0);
    }

    private void OnPass(string pass, Action<int>? beforeTick, bool seeded)
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
            if (seeded && _tick == 1)
            {
                GameRandom.RandomSeed = Seed;
            }

            beforeTick?.Invoke(_tick);
        }
        else if (pass == "inventory")
        {
            var hero = Arc.Hero;
            Hero[_tick] = new HeroTick(hero.Hp, hero.TargetAnimationId, hero.PosX / 65536.0, hero.PosY / 65536.0, hero.PosZ / 65536.0, hero.CarriedEntity != null, hero.ItemInUse, hero.DamagedTickCounter);

            var inWorld = Arc.Entities.Where(e => e.SpriteType == 2).ToList();
            foreach (var bomb in inWorld.Where(b => !_present.Contains(b)))
            {
                _births.Add(bomb);
            }

            _present.Clear();
            foreach (var bomb in inWorld)
            {
                _present.Add(bomb);
            }

            var row = new List<BombTick>(_births.Count);
            foreach (var bomb in _births)
            {
                // A proxy that is recycled and born again is a new birth: the list keeps one entry per birth; a birth whose proxy has been reborn is read as absent (the newest entry owns the proxy).
                var newest = _births.LastIndexOf(bomb) == row.Count;
                row.Add(newest && _present.Contains(bomb) ? Snapshot(bomb) : BombTick.Absent);
            }

            Bombs[_tick] = row;

            ScanEffects(_tick);
        }
    }

    /// <summary>
    /// Notes the effects the stage holds for the first time, as created at the tick <paramref name="tick"/>. Called at the "inventory" pass (what <see cref="Alundra.Rendering.Effects.EffectStage.CreateNow"/> made in the tick) and again right after
    /// the frame (what the entities queued by <c>Emit</c> and the stage applied at the end of the frame): the key's tick of an effect is the tick of the code that asked for it.
    /// </summary>
    private void ScanEffects(int tick)
    {
        if (Arc.Proxy.EffectStage is not { } stage)
        {
            return;
        }

        foreach (var slot in stage.Reservoir.ActiveSlots)
        {
            if (_seenEffects.Add((slot.Index, slot.Generation)))
            {
                Effects.Add(new EffectTick(tick, slot.TargetEffect, slot.X, slot.Y, slot.Z, slot.ForceX, slot.ForceY, slot.Mode));
            }
        }
    }

    private static BombTick Snapshot(EntityScriptProxy bomb) => new(
        true, (int)bomb.Status, bomb.TargetAnimationId, bomb.ForceResetAnimationFlag, BombAi.Fuse(bomb),
        bomb.PosX / 65536.0, bomb.PosY / 65536.0, bomb.PosZ / 65536.0, bomb.PlatformEntity != null, bomb.TargetDirection, bomb.IsOnGround);

    /// <summary>The bomb born <paramref name="birth"/>th (0 based) at the end of <paramref name="tick"/>: absent before its birth and after it left the world.</summary>
    internal BombTick BombAt(int birth, int tick)
        => Bombs.TryGetValue(tick, out var row) && birth < row.Count ? row[birth] : BombTick.Absent;

    /// <summary>How many bombs were born during the run.</summary>
    internal int Births => Bombs.Count == 0 ? 0 : Bombs.Values.Max(r => r.Count);

    /// <summary>The bombs present in the world at the end of <paramref name="tick"/>.</summary>
    internal List<BombTick> PresentAt(int tick) => Bombs.TryGetValue(tick, out var row) ? row.Where(b => b.Present).ToList() : new List<BombTick>();

    /// <summary>The sounds of the world at the tick <paramref name="tick"/>, in order, the silence (<c>0</c>) left out, as every key does.</summary>
    internal List<int> SoundsAt(int tick) => Heard.Played.Where(p => p.Tick == tick && p.Sfx != 0).Select(p => p.Sfx).ToList();

    /// <summary>Every sound of the run with its tick, the silence left out.</summary>
    internal List<(int Tick, int Sfx)> AllSounds() => Heard.Played.Where(p => p.Sfx != 0).ToList();

    /// <summary>The effects of the identifier <paramref name="id"/> first held at the end of the tick <paramref name="tick"/>.</summary>
    internal List<EffectTick> EffectsAt(int tick, int id) => Effects.Where(e => e.Tick == tick && e.Id == id).ToList();

    public void Dispose()
    {
        Arc.Proxy.HitSparksForTests = null;
        Arc.Proxy.TickTraceForTests = null;
        Arc.Dispose();
    }
}
