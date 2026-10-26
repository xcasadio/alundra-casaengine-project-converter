#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3a T2 (docs/plan-e20-heros.md section 1.30.2, the tests of the world (w1) to (w4) and (w6)): the bench of the bomb born posed, the model of <see cref="Alundra.Tests.Ai.Projectiles.BallRun"/>. The REAL world of map 14 (<see cref="HitArena"/>), the synthetic field of the oracle on the same records as its cell store
/// (<see cref="SyntheticField"/>), every entity of the map but the hero and the targets asked for removed, the hero at (<c>heroX</c>, <c>heroY</c>) facing east (the hero of the new game, with the kit of the oracle: items 1, 17 and 25). The bombs are born by the interface (<c>SpawnEntityBySpriteIndex</c>, sprite 2, the parent being the hero, as the
/// <c>spawn</c> of the scripts of the oracle), with no hands and no C62; the harness puts the FIRST bomb in status 3 at the end of the tick <see cref="StatusTick"/> (the tick 0 of the keys, "k+0"). The native handlers of the world run (the registry of PRODUCTION: nothing is registered here, the bench observes what production does).
/// With <c>seedAtStatusTick</c> the generator is posed at the seed of the key at the head of the tick that follows the poke (the key's <c>rng0(k+1)</c>; the per-frame step of the engine does the rest).
/// </summary>
internal sealed class BombRun : IDisposable
{
    /// <summary>The tick at the end of which the first bomb is put in status 3: the tick "k+0" of the keys.</summary>
    internal const int StatusTick = 5;

    internal const uint Seed = 0x12345678;

    private int _frameSize = 1;
    private uint _frameStartState;
    private int _tick;
    private readonly bool _seed;
    private readonly List<EntityScriptProxy> _bombs = new();

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The bombs in the order asked for (the proxy stays valid after the entity is recycled).</summary>
    internal IReadOnlyList<EntityScriptProxy> Bombs => _bombs;

    /// <summary>The state of each bomb at the end of each tick (the key: the index of the bomb, then the tick). A bomb that is not in the world any more reads as the key's free slot (all zero).</summary>
    internal readonly List<Dictionary<int, BombTick>> Ticks = new();

    /// <summary>The state of the hero and of each target at the end of each tick.</summary>
    internal readonly Dictionary<int, HitTickSample> Samples = new();
    internal readonly Dictionary<int, bool> HeroTouchesABomb = new();

    /// <summary>The generator after each frame (key: the tick that ends the frame) and the steps it made during it.</summary>
    internal readonly Dictionary<int, uint> StateAfter = new();
    internal readonly Dictionary<int, int> StepsDuring = new();

    /// <summary>The effects the stage created (not the map-table ones): the tick, the identifier and the position in pixels.</summary>
    internal readonly List<(int Tick, int Id, double X, double Y, double Z)> Effects = new();

    /// <summary>The ticks of the sparks the hit pass asked for (one entry per call of the spark seam: one set of three per hit).</summary>
    internal readonly List<int> SparkTicks = new();

    internal int LastTick;

    /// <summary>The state of a bomb at the end of a tick. The current animation (<c>+0x90</c> of the key) is not compared: the DLL validates it at the end of the frame (it reads -1, then one frame late), the target animation (<c>+0x88</c>) is the one that moves at the tick of the key.</summary>
    internal readonly record struct BombTick(bool Present, int Status, int TargetAnimation, int End, bool Box, int HitCounter);

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

    /// <param name="bombs">The pixels of the bombs born posed (the first one gets the status 3).</param>
    /// <param name="cell">The cells of the field (default: all zero).</param>
    /// <param name="targets">The pixels where the Beannoids of the map kept as targets (records 0 to n - 1) are put, ONCE the synthetic field is installed (the real map has walls and pits where the bomb is). Each one is put in the state its program A gives it at birth in production (the dormant animation 2): the oracle's target is born by its real program A.</param>
    internal BombRun(string name, IReadOnlyList<(int X, int Y)> bombs, Func<int, int, CutBench.Cell>? cell = null, IReadOnlyList<(int X, int Y)>? targets = null, int heroX = 100, int heroY = 100, bool seedAtStatusTick = false)
    {
        _seed = seedAtStatusTick;
        Arena = new HitArena("h3a-" + name, heroX, heroY, null, 24, 0, targets?.Count ?? 0);
        try
        {
            Bench = SyntheticField.Install(Arena.Arc, cell);
            var keep = new HashSet<EntityScriptProxy>(Arena.Targets) { Arena.Hero };
            foreach (var other in Arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arena.Arc.Proxy.RecycleDestroyedEntities();
            for (var i = 0; targets != null && i < targets.Count; i++)
            {
                HitArena.Place(Arena.Targets[i], targets[i].X, targets[i].Y);
                Arena.Targets[i].TargetAnimationId = 2;
            }

            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arena.Arc.Proxy, Heard);
            Arena.EnableNativeHandlers();
            foreach (var (x, y) in bombs)
            {
                var bomb = ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(Arena.Hero, false, 2, x << 16, y << 16, 0, 0)
                    ?? throw new InvalidOperationException("the bomb was not born");
                _bombs.Add(bomb);
                Ticks.Add(new Dictionary<int, BombTick>());
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks (the last frame can end after it).</summary>
    internal void Go(int ticksPerFrame, int lastTick)
    {
        _frameSize = ticksPerFrame;
        NativeEffects.CreatedForTests = slot =>
        {
            if (!slot.TargetMapTable)
            {
                Effects.Add((_tick, slot.TargetEffect, slot.X / 65536.0, slot.Y / 65536.0, slot.Z / 65536.0));
            }
        };
        Arena.BeforeTick = tick =>
        {
            _tick = tick;
            Heard.Tick = tick;
            if (_seed && tick == StatusTick + 1)
            {
                GameRandom.RandomSeed = Seed;
            }

            if ((tick - 1) % _frameSize == 0)
            {
                _frameStartState = unchecked((uint)GameRandom.RandomSeed);
            }
        };
        Arena.AfterTick = tick =>
        {
            if (tick == StatusTick)
            {
                // The harness: the status 3 at the end of the tick k+0, before the line of that tick is read (the key prints the state after the poke).
                _bombs[0].Status = EntityStatus.Deactivated;
            }

            Samples[tick] = Arena.Samples[^1];
            for (var i = 0; i < Samples[tick].Sparks.Count; i++)
            {
                SparkTicks.Add(tick);
            }

            HeroTouchesABomb[tick] = Arena.Hero.TouchingEntity is { } touching && _bombs.Any(b => ReferenceEquals(touching, b.SelfEntity));
            for (var i = 0; i < _bombs.Count; i++)
            {
                var b = _bombs[i];
                var present = Arena.Arc.Entities.Contains(b);
                Ticks[i][tick] = present
                    ? new BombTick(true, (int)b.Status, (int)b.TargetAnimationId, b.ForceResetAnimationFlag, b.HasFrameBox, b.HitCounter)
                    : default;
            }

        };

        Arena.Begin();
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(0, ticksPerFrame);
            var end = tick + ticksPerFrame - 1;
            LastTick = end;
            var after = unchecked((uint)GameRandom.RandomSeed);
            StateAfter[end] = after;
            StepsDuring[end] = Alundra.Tests.Ai.Projectiles.ArrowRun.Steps(_frameStartState, after);
        }
    }

    /// <summary>The state of the bomb <paramref name="bomb"/> at the tick k+<paramref name="relative"/> of the key.</summary>
    internal BombTick At(int bomb, int relative) => Ticks[bomb].TryGetValue(StatusTick + relative, out var t) ? t : default;

    /// <summary>The hero and the targets at the end of the tick k+<paramref name="relative"/>.</summary>
    internal HitTickSample SampleAt(int relative) => Samples[StatusTick + relative];

    internal static string Px(int raw) => (raw / 65536.0).ToString("F2", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        NativeEffects.CreatedForTests = null;
        Arena.Dispose();
    }
}
