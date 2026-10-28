#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Audio;
using Alundra.Cameras;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1 (docs/plan-e20-heros.md section 1.31.5, rule R-EL-4): the bench of the world tests of the fire tome 2. The REAL world of map 14 (<see cref="HitArena"/>: real prefabs, the real controller of the hero), the flat field of 52 x 60 cells of the scenario
/// (<see cref="SyntheticField"/>), every entity of the map but the hero (and the targets asked for) removed, the generator at the seed of the oracle at the head of the tick 1, the tome 48 (<c>0x30</c>) given and equipped, the magic 4/4, <c>ticksPerFrame</c> ticks per frame,
/// the Circle pressed at the tick 1, the hero at (100, 100) facing east. The native handlers of the arena are OFF; the switch of D-F4-7 is on for the sprite type 21 alone (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>). The three seams of h0a are those of
/// phase A (D-H-4): the start is the start of the tomes of h4a, the driver is <see cref="FireTome2Ai.Driver"/>, the cancellation is <see cref="FireTome2Ai.Cancel"/> ONLY when the item in use is <c>0x30</c> (neutral otherwise, as the dispatcher of the binary); E28 is
/// registered on the registry of the runner ONLY IF its key is absent (the phase B registers it in production). Everything the tests compare is recorded here, tick by tick, at the end of the tick, as the oracle's keys are.
/// </summary>
internal sealed class FireTome2Run : IDisposable
{
    internal const uint Seed = 0x12345678;
    internal const int Column = 21;
    internal const uint Item = 0x30;

    private IDisposable? _registration;
    private uint _stateBefore;
    private int _tick;

    internal readonly HitArena Arena;
    internal readonly Sounds Heard = new();

    /// <summary>The column of fire (the sprite 21) as the end of a tick saw it.</summary>
    internal sealed record ColumnRow(int Tick, int Slot, int Status, int Flags, int PosX, int PosY, int PosZ, int ForceX, int ForceY, int ForceZ, uint TargetAnimation, int AnimationEnd, int HitCounter, int Width, int Depth, int Height, int Attribute, int Power, EntityScriptProxy Entity);

    internal sealed record HeroRow(int Tick, uint Animation, uint ItemInUse, int Phase, int Counter, int Mp, int HitCounter);

    /// <summary>An effect the run created (the effect 19 of the animation 2 of E28 among them), at the tick the creator ran (exact at one tick a frame).</summary>
    internal sealed record EffectRow(int Tick, int Effect, int Animation, int X, int Y, int Z);

    internal readonly Dictionary<int, ColumnRow> Columns = new();

    internal readonly Dictionary<int, HeroRow> Hero = new();

    /// <summary>The nine words of the shake block (Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY) at the end of each tick.</summary>
    internal readonly Dictionary<int, int[]> Blocks = new();

    /// <summary>The nine words of the buffer of the pilot at the end of each tick.</summary>
    internal readonly Dictionary<int, int[]> Buffers = new();

    /// <summary>The hit cooldown (<c>+0x1D0</c>, set to 25 by a touch) of each target put on the bench, by tick: a touch is the tick at which it reads 25.</summary>
    internal readonly Dictionary<int, int[]> TargetCounters = new();

    /// <summary>The generator at the head of each tick (after the seed of the tick 1).</summary>
    internal readonly Dictionary<int, uint> StateAtHead = new();

    /// <summary>The steps of the generator between the head and the end of each frame, indexed by the LAST tick of the frame.</summary>
    internal readonly Dictionary<int, int> FrameSteps = new();

    internal readonly List<EffectRow> Effects = new();

    internal int LastTick;

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

    /// <param name="targets">The pixels (X, Y) of the blocks (the global sprite 25, class A, 5000 HP) put on the bench as targets.</param>
    /// <param name="block">The nine words of the shake block before the tick 1 (null: the world's, all 0).</param>
    /// <param name="buffer">The nine words of the buffer of the pilot before the tick 1 (null: 0).</param>
    /// <param name="production">The phase B (T2): no seam and no registration of the test, the item runs through the entries of the object in <see cref="PlayerManager"/> and the registry of production alone.</param>
    internal FireTome2Run(string name, IReadOnlyList<int[]>? targets = null, int[]? block = null, int[]? buffer = null, bool production = false)
    {
        Arena = new HitArena("h4d1-" + name, 100, 100, new List<int[]>(), 24, 0);
        try
        {
            SyntheticField.Install(Arena.Arc, null);
            var keep = new HashSet<EntityScriptProxy> { Arena.Hero };
            foreach (var other in Arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arena.Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arena.Arc.Proxy, Heard);
            Arena.Arc.Give((int)Item);
            Arena.Arc.SetMp(4, 4);
            PlayerManager.SetCurrentItemId(GameState.Instance, Item);
            Arena.Hero.FrameCollisionTickCounter = 0;
            Arena.Hero.DamagedTickCounter = 0;
            Arena.Hero.HitCounter = 0;
            Arena.Hero.TouchingEntity = null;
            foreach (var pixel in targets ?? new List<int[]>())
            {
                var target = ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(Arena.Hero, false, 25, pixel[0] << 16, pixel[1] << 16, 0, 0);
                if (target is null)
                {
                    throw new InvalidOperationException("no target born");
                }

                target.Flags |= EntityFlags.ClassA;
                target.Hp = 5000;
                Arena.Targets.Add(target);
            }

            if (block is not null)
            {
                Post(CameraSway.Instance, block);
            }

            if (buffer is not null)
            {
                for (var i = 0; i < 9; i++)
                {
                    CameraSway.Instance.Buffer[i] = buffer[i];
                }
            }

            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { Column };
            if (!production)
            {
                PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
                PlayerManager.ItemDriverForTests = (hero, _) => FireTome2Ai.Driver(hero);
                PlayerManager.ItemCancelForTests = (hero, item) =>
                {
                    if (item == Item)
                    {
                        FireTome2Ai.Cancel(hero);
                    }
                };
                var registry = Arena.Arc.Runner.NativeSprites;
                if (!registry.TryGet(ScriptHelper.ProgramEDeactivate, FireTome2Ai.ColumnEndCode, Column, out _))
                {
                    _registration = FireTome2Ai.Register(registry);
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The nine words of a sway, in the order of the block of the binary (<c>0x800E4338</c>).</summary>
    internal static int[] Words(CameraSway sway) => new[] { sway.Flag, sway.LimitX, sway.LimitY, sway.SpeedX, sway.SpeedY, sway.OffsetX, sway.OffsetY, sway.ReachX, sway.ReachY };

    internal static void Post(CameraSway sway, IReadOnlyList<int> words)
    {
        sway.Flag = words[0];
        sway.LimitX = words[1];
        sway.LimitY = words[2];
        sway.SpeedX = words[3];
        sway.SpeedY = words[4];
        sway.OffsetX = words[5];
        sway.OffsetY = words[6];
        sway.ReachX = words[7];
        sway.ReachY = words[8];
    }

    private static uint State() => unchecked((uint)GameRandom.RandomSeed);

    /// <summary>
    /// Runs the ticks 1 to <paramref name="last"/>, <paramref name="ticksPerFrame"/> per frame. <paramref name="interruptAt"/>: the animation 0x31 is posed on the hero before the tick (R-EL-6: the head of the tick, as the oracle's poke).
    /// <paramref name="before"/> is told the tick at its head, after the generator is seeded.
    /// </summary>
    internal void Go(int last, int ticksPerFrame = 1, int? interruptAt = null, Action<int>? before = null)
    {
        NativeEffects.CreatedForTests = slot => Effects.Add(new EffectRow(_tick, slot.TargetEffect, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
        Arena.BeforeTick = tick =>
        {
            _tick = tick;
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = Seed;
            }

            StateAtHead[tick] = State();
            if ((tick - 1) % ticksPerFrame == 0)
            {
                _stateBefore = State();
            }

            if (interruptAt == tick)
            {
                Arena.Hero.TargetAnimationId = 0x31;
            }

            before?.Invoke(tick);
        };
        Arena.AfterTick = tick =>
        {
            var column = Arena.Arc.Entities.FirstOrDefault(e => e.SpriteType == Column && e.Status != EntityStatus.Destroyed);
            if (column is not null)
            {
                column.EvaluateFrameBox();
                Columns[tick] = new ColumnRow(tick, column.Slot, (int)column.Status, (int)column.Flags, column.PosX, column.PosY, column.PosZ, column.ForceX, column.ForceY, column.ForceZ, column.TargetAnimationId, column.ForceResetAnimationFlag,
                    column.HitCounter, column.CollisionWidth, column.CollisionDepth, column.CollisionHeight, column.CurrentAttack?.Attribute ?? -1, column.CurrentAttack?.Power ?? -1, column);
            }

            var hero = Arena.Hero;
            Hero[tick] = new HeroRow(tick, hero.TargetAnimationId, hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, GameState.Instance.PlayerStats.Mp, hero.HitCounter);
            Blocks[tick] = Words(CameraSway.Instance);
            Buffers[tick] = CameraSway.Instance.Buffer.ToArray();
            TargetCounters[tick] = Arena.Targets.Select(t => t.FrameCollisionTickCounter).ToArray();
        };

        Arena.Begin();
        for (var tick = 1; tick <= last; tick += ticksPerFrame)
        {
            var ticks = Math.Min(ticksPerFrame, last - tick + 1);
            Arena.Frame(tick == 1 ? PadState.Circle : 0, ticks);
            FrameSteps[tick + ticks - 1] = Steps(_stateBefore, State());
            LastTick = tick + ticks - 1;
        }
    }

    /// <summary>The number of steps of the generator from <paramref name="from"/> to <paramref name="to"/> (at most 64), -1 when it is not reached.</summary>
    internal static int Steps(uint from, uint to)
    {
        var s = from;
        for (var k = 0; k <= 64; k++)
        {
            if (s == to)
            {
                return k;
            }

            s = unchecked(s * 0x7D2B89DDu + 0xE06A02E7u);
        }

        return -1;
    }

    /// <summary>The ticks at which the target <paramref name="index"/> was touched: the hit cooldown reads 25 and did not at the tick before.</summary>
    internal List<int> HitTicks(int index)
    {
        var ticks = new List<int>();
        for (var tick = 2; tick <= LastTick; tick++)
        {
            if (TargetCounters[tick][index] == 25 && TargetCounters[tick - 1][index] != 25)
            {
                ticks.Add(tick);
            }
        }

        return ticks;
    }

    /// <summary>The effects 19 of the animation 2 (the eight of E28), by tick.</summary>
    internal List<EffectRow> ColumnBlasts() => Effects.Where(e => e.Effect == NativeEffects.FireColumnBlastEffect && e.Animation == 2).ToList();

    public void Dispose()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        _registration?.Dispose();
        Arena.Dispose();
    }
}
