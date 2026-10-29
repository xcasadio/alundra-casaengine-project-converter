#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Audio;
using Alundra.Combat;
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
/// E20.h4d2 T1 bis (docs/plan-e20-heros.md section 1.31.6, rule R-EL-4): the bench of the world tests of the fire tome 1. The REAL world of map 14 (<see cref="HitArena"/>: real prefabs, the real controller of the hero), the flat field of 52 x 60 cells of the scenario
/// (<see cref="SyntheticField"/>, or the field asked), every entity of the map but the hero removed, the blocks of the sprite 25 asked put at their pixels in the first free cells (class A, <c>Hp</c> 5000), the generator at the seed of the oracle at the head of the tick 1, the tome 47
/// (<c>0x2F</c>) given and equipped, the magic 4/4, <c>ticksPerFrame</c> ticks per frame, the Circle pressed at the tick 1. The native handlers of the arena are OFF; the switch of D-F4-7 is on for the type 23 alone (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: C63 and E3
/// run for the balls, the column's programs are the runner's own, E1 in line). The three seams of h0a are those of the phase A (D-H-4): the start is the start of the tomes of h4a, the driver is <see cref="FireTome1Ai.Drive"/>, the cancellation is <see cref="FireTome1Ai.Cancel"/> when the item in
/// use is <c>0x2F</c> and neutral otherwise (the dispatcher of the binary); E3 and C63 are registered on the registry of the runner ONLY IF their key is absent (the phase B registers C63 in production). In the BINARY mode the query of C63 is asked with the settings of the binary
/// (<c>InclusiveBound</c> false, <c>SortThreshold</c> 3; D-H4D2-6) through a handler of the test; otherwise the handler is the one of production. Everything the tests compare is recorded here, tick by tick, at the inventory pass of the tick, as the oracle's keys are; the animations of the entities
/// are read at the END of the frame (the DLL validates the switch of animation there, D-H4C1-3).
/// </summary>
internal sealed class FireTome1Run : IDisposable
{
    internal const uint Seed = 0x12345678;
    internal const int Column = 20;
    internal const int Ball = 23;
    internal const int Block = 25;
    internal const uint Item = 0x2F;

    private readonly List<IDisposable> _registrations = new();
    private int _tick;
    private uint _stateBefore;
    private readonly HashSet<(int Index, int Generation)> _attachedSeen = new();
    private readonly List<(int Tick, string Event)> _events = new();
    private int _pointerSlot = -1;
    private EntityScriptProxy? _pointer;

    internal readonly HitArena Arena;
    internal readonly Sounds Heard = new();

    /// <summary>One entity as the inventory pass of the tick saw it (the evaluated box included) and as the END of the frame saw its animation.</summary>
    internal sealed record EntityRow(int Tick, int Slot, EntityStatus Status, int SpriteType, uint Flags, int PosX, int PosY, int PosZ, int ForceX, int ForceY, int ForceZ, uint TargetAnimation, uint TargetDirection, int AnimationEnd, int Wait, int TargetSlot,
        bool Blocked, int DamagedCounter, int CollisionCounter, int? TouchedBy, int Slope, int Ground, int Adjusted, int[] Box, int? Attribute, int? Power, int Hp, uint AnimationAtEnd, uint DirectionAtEnd, int RowAtEnd, EntityScriptProxy Entity);

    internal sealed record HeroRow(int Tick, uint TargetAnimation, uint AnimationAtEnd, int PosX, int PosY, int PosZ, uint ItemInUse, int Phase, int Counter, int Mp, int? PointerSlot);

    /// <summary>The entities (the column, the balls, the blocks) by tick, then by slot; a freed slot has no row.</summary>
    internal readonly Dictionary<int, Dictionary<int, EntityRow>> Entities = new();

    internal readonly Dictionary<int, HeroRow> Hero = new();

    /// <summary>The blocks put on the bench: the hits (their cooldown at 25) by tick, as the indices in <see cref="HitArena.Targets"/>.</summary>
    internal readonly Dictionary<int, List<int>> Hits = new();

    /// <summary>The generator at the head of each tick (after the seed of the tick 1).</summary>
    internal readonly Dictionary<int, uint> StateAtHead = new();

    /// <summary>The steps of the generator between the head and the end of each frame, indexed by the LAST tick of the frame.</summary>
    internal readonly Dictionary<int, int> FrameSteps = new();

    /// <summary>The events of each tick in the normal form of <see cref="FireTome1Key.NormalEvents"/>, sorted.</summary>
    internal readonly Dictionary<int, List<string>> Events = new();

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

    /// <param name="blocks">The pixels (X, Y) of the blocks of the sprite 25 (class A, <c>Hp</c> 5000) put on the bench, in the first free cells of the table.</param>
    /// <param name="cell">The field (default: flat, all zero).</param>
    /// <param name="heroZ">The pixel Z of the hero (0: on the flat ground).</param>
    /// <param name="binary">The settings of the query of C63 are those of the binary (<c>InclusiveBound</c> false, <c>SortThreshold</c> 3, D-H4D2-6), not those of production.</param>
    internal FireTome1Run(string name, IReadOnlyList<int[]>? blocks = null, Func<int, int, CutBench.Cell>? cell = null, int heroZ = 0, bool binary = false)
    {
        Arena = new HitArena("h4d2-" + name, 100, 100, new List<int[]>(), 24, 0);
        try
        {
            SyntheticField.Install(Arena.Arc, cell);
            if (heroZ != 0)
            {
                Arena.Arc.PlaceHero(100, 100, heroZ);
            }

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
            foreach (var pixel in blocks ?? new List<int[]>())
            {
                var block = ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(Arena.Hero, false, Block, pixel[0] << 16, pixel[1] << 16, 0, 0);
                Assert.NotNull(block);
                block!.PosZ = 0; // the key's blocks rest at the binary's height 1: the DLL's 0.
                block.Flags |= EntityFlags.ClassA;
                block.Hp = 5000;
                Arena.Targets.Add(block);
            }

            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { Ball };
            PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
            PlayerManager.ItemDriverForTests = (hero, _) => FireTome1Ai.Drive(hero);
            PlayerManager.ItemCancelForTests = (hero, item) =>
            {
                if (item == Item)
                {
                    FireTome1Ai.Cancel(hero);
                }
            };
            Arena.Arc.Runner.NativeHandlersEnabled = false;
            var registry = Arena.Arc.Runner.NativeSprites;
            if (!registry.TryGet(ScriptHelper.ProgramEDeactivate, EarthTome1Ai.ImpactCode, Ball, out _))
            {
                _registrations.Add(EarthTome1Ai.Register(registry));
            }

            if (!registry.TryGet(ScriptHelper.ProgramCTick, FireTome1Ai.BallCode, Ball, out _))
            {
                _registrations.Add(binary
                    ? registry.Register(ScriptHelper.ProgramCTick, FireTome1Ai.BallCode, FireTome1Ai.SpriteTypes, (entity, context, state) => FireTome1Ai.BallTick(entity, context.SpawnedEntities, TargetQuery.StoredCount(context.SpawnedEntities), inclusiveBound: false, sortThreshold: 3))
                    : FireTome1Ai.Register(registry));
            }

            NativeEffects.CreatedForTests = slot => _events.Add((_tick, $"fx({slot.TargetEffect},{slot.TargetAnimation},{slot.X},{slot.Y},{slot.Z})"));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static uint State() => unchecked((uint)GameRandom.RandomSeed);

    /// <summary>
    /// Runs the ticks 1 to <paramref name="last"/>, <paramref name="ticksPerFrame"/> per frame. <paramref name="interruptAt"/>: the animation 0x31 is posed on the hero before the tick (R-EL-6: the head of the tick, as the oracle's poke). <paramref name="remove"/>: the blocks (by their index in
    /// <see cref="HitArena.Targets"/>) taken off the world at the head of a tick. <paramref name="seed"/>: the generator at the head of the tick 1.
    /// </summary>
    internal void Go(int last, int ticksPerFrame = 1, int? interruptAt = null, Dictionary<int, int[]>? remove = null, uint seed = Seed)
    {
        Arena.BeforeTick = tick =>
        {
            _tick = tick;
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = seed;
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

            if (remove != null && remove.TryGetValue(tick, out var gone))
            {
                foreach (var index in gone)
                {
                    Arena.Targets[index].Status = EntityStatus.FlagToDestroy;
                }
            }
        };
        Arena.AfterTick = tick =>
        {
            var rows = new Dictionary<int, EntityRow>();
            foreach (var entity in Arena.Arc.Entities.Where(e => e != Arena.Hero && e.Status != EntityStatus.Destroyed && e.Slot >= 1).OrderBy(e => e.Slot))
            {
                entity.EvaluateFrameBox();
                rows[entity.Slot] = new EntityRow(tick, entity.Slot, entity.Status, entity.SpriteType, entity.Flags, entity.PosX, entity.PosY, entity.PosZ, entity.ForceX, entity.ForceY, entity.ForceZ, entity.TargetAnimationId, entity.TargetDirection,
                    entity.ForceResetAnimationFlag, TomeWords.Read274(entity), entity.DelayOrAngleOrEntityId, entity.BlockedByEntity != null, entity.DamagedTickCounter, entity.FrameCollisionTickCounter,
                    entity.TouchingEntity?.GameplayProxy is EntityScriptProxy toucher ? toucher.Slot : null, entity.Slope_18c, entity.IsOnGround, entity.ForceAdjusted,
                    new[] { entity.HitBoxX, entity.HitBoxY, entity.HitBoxZ, entity.CollisionWidth, entity.CollisionDepth, entity.CollisionHeight }, entity.CurrentAttack?.Attribute, entity.CurrentAttack?.Power, entity.Hp, 0, 0, 0, entity);
            }

            Entities[tick] = rows;
            var hero = Arena.Hero;
            if (hero.ItemSpawnedEntity is { } kept)
            {
                if (!ReferenceEquals(_pointer, kept))
                {
                    _pointer = kept;
                    _pointerSlot = -1;
                }

                if (kept.Slot >= 1)
                {
                    _pointerSlot = kept.Slot;
                }
            }
            else
            {
                _pointer = null;
                _pointerSlot = -1;
            }

            Hero[tick] = new HeroRow(tick, hero.TargetAnimationId, 0, hero.PosX, hero.PosY, hero.PosZ, hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, GameState.Instance.PlayerStats.Mp, _pointer is null ? null : _pointerSlot);
            Hits[tick] = Arena.Targets.Select((t, index) => (t, index)).Where(p => p.t.Status != EntityStatus.Destroyed && p.t.FrameCollisionTickCounter == 25).Select(p => p.index).ToList();

            // The sparks of the hit pass of this tick: three effects 9 at the centre of each (the stage updates them at once; their place is the centre the hit pass wrote).
            foreach (var (x, y, z) in Arena.Samples[^1].Sparks)
            {
                for (var k = 0; k < NativeEffects.HitSparkCount; k++)
                {
                    _events.Add((tick, $"fx(9,0,{x},{y},{z + 1})"));
                }
            }
        };

        Arena.Begin();
        for (var tick = 1; tick <= last; tick += ticksPerFrame)
        {
            var ticks = Math.Min(ticksPerFrame, last - tick + 1);
            Arena.Frame(tick == 1 ? PadState.Circle : 0, ticks);
            FrameSteps[tick + ticks - 1] = Steps(_stateBefore, State());
            LastTick = tick + ticks - 1;

            // The effects attached to an entity (the first sight of each on the stage), at the last tick of the frame.
            foreach (var slot in Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Where(s => s.Anchor != null && !s.TargetMapTable))
            {
                if (_attachedSeen.Add((slot.Index, slot.Generation)) && slot.Anchor is EntityScriptProxy anchor)
                {
                    _events.Add((LastTick, $"attach({slot.TargetEffect},{anchor.Slot})"));
                }
            }

            // The END of the frame: the DLL validates the switch of animation there (D-H4C1-3); the animation, the direction and the row are read now.
            for (var t = tick; t <= LastTick; t++)
            {
                foreach (var (slotNumber, row) in Entities[t].ToList())
                {
                    var entity = row.Entity;
                    Entities[t][slotNumber] = row with { AnimationAtEnd = entity.CurrentAnimationId, DirectionAtEnd = entity.CurrentDirection, RowAtEnd = entity.AnimationDirection };
                }

                Hero[t] = Hero[t] with { AnimationAtEnd = Arena.Hero.CurrentAnimationId };
            }
        }

        foreach (var tick in Enumerable.Range(1, LastTick))
        {
            var list = _events.Where(e => e.Tick == tick).Select(e => e.Event).ToList();
            list.AddRange(Heard.Played.Where(p => p.Tick == tick && p.Sfx != 0).Select(p => $"snd({p.Sfx})"));
            list.Sort(StringComparer.Ordinal);
            Events[tick] = list;
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

    /// <summary>The lives of the entities of a sprite type, by the order of their births (tick, slot): the rows of a slot from the first tick it is present until it is absent (or until the tick of the status 4, which frees the slot for the tick after).</summary>
    internal List<List<EntityRow>> Lives(int spriteType)
    {
        var lives = new List<List<EntityRow>>();
        var open = new Dictionary<int, List<EntityRow>>();
        for (var tick = 1; tick <= LastTick; tick++)
        {
            foreach (var (slot, row) in Entities[tick].Where(e => e.Value.SpriteType == spriteType).OrderBy(r => r.Key))
            {
                if (!open.TryGetValue(slot, out var life) || !ReferenceEquals(life[^1].Entity, row.Entity))
                {
                    life = new List<EntityRow>();
                    open[slot] = life;
                    lives.Add(life);
                }

                life.Add(row);
            }
        }

        return lives.OrderBy(l => l[0].Tick).ThenBy(l => l[0].Slot).ToList();
    }

    public void Dispose()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        Arena.Dispose();
    }
}
