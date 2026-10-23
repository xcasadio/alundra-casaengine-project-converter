#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f5 T1 (docs/plan-e20-heros.md section 1.21.1 and the "Revision au HEAD" points 3 and 10 and the bullet "Le banc de (p1) et (p2)"): the bench of the normal arrows (types 3 and 4), the model of <see cref="ChargedArrowRun"/>. The REAL world of map 14 (<see cref="HitArena"/>), the field of the scenario and its cell store on the same
/// records (<see cref="SyntheticField"/>), every entity of the map but the hero and the targets asked for removed, the native handlers of the arena OFF and the arena switch of D-F4-7 on for the type of the arrow only (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>), the hero at (<c>heroX</c>, <c>heroY</c>)
/// facing <c>direction</c> (24 east by default) with the bow (the hunter's, item 5, or William's, item 6, in the slot 2), the generator at the seed of the oracle at the head of the tick 1. The pad is the oracle's: Square at the tick 1 (P = 1) and nothing else, or the pad of the scenario. The arrow is
/// born by the TEST HOOK from the exact place of <c>MovePlayer</c> where it leaves, or, in <c>production</c>, by the production seam (the set of <see cref="PlayerManager.ProductionWiredWeaponTypes"/>). Everything the tests compare is recorded here, tick by tick.
/// </summary>
internal sealed class ArrowRun : IDisposable
{
    private const uint Square = PadState.Square;
    private const int BowSlot = 2;
    internal const uint Seed = 0x12345678;
    private const int FillerType = 5;

    private int _frameEnd;
    private uint _frameStartState;
    private int _frameSize = 1;
    private readonly bool _production;
    private readonly int? _freeSlotsAtBirth;
    private readonly Func<int, uint> _pad;
    private readonly uint _generatorAtTick1;

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The sprite type of the arrow this run shoots: 3 for the hunter's bow, 4 for William's.</summary>
    internal readonly int ArrowType;

    /// <summary>The entity of the map the run kept besides the hero and the targets (<c>alsoKeep</c>), or null.</summary>
    internal readonly EntityScriptProxy? Kept;

    /// <summary>The arrow once seen (the proxy stays valid after the entity is recycled), and the tick of its birth.</summary>
    internal EntityScriptProxy? Born;
    internal int BirthTick;
    internal int BirthSlot = -1;

    /// <summary>The last tick that ran (the last frame is whole: it can end after the <c>lastTick</c> asked).</summary>
    internal int LastTick;

    /// <summary>The static values of the arrow at the end of its birth tick: the flags, the attack (attribute, power), the box (width, depth, height in raw 16.16) and the pixel of the Z of the bottom of the box.</summary>
    internal string BirthStatics = string.Empty;
    internal int LowestFreeSlotBeforeBirth = -1;

    /// <summary>The state of the arrow at the end of each tick (the proxy stays valid after the recycling); absent while it is not in the world.</summary>
    internal readonly Dictionary<int, ArrowTick> Ticks = new();

    /// <summary>Per tick: the arrow's hit counter, the first target's cooldown (+0x1D0), the number of sparks of the pass, whether the arrow touches something, the entity the first target is touched by.</summary>
    internal readonly Dictionary<int, (int HitCounter, int TargetCooldown, int Sparks, bool Touching)> Hits = new();

    /// <summary>The controller of the arrow after each tick: the walkability mask and the gravity of its settings (they must not change at the bounce: the mask reads the classes only, the gravity of the controller is always null here).</summary>
    internal readonly Dictionary<int, (uint Mask, float Gravity)> ControllerAfter = new();

    /// <summary>The number of effects attached to an anchor (a break effect) the stage held at the end of the frames: none for these arrows (the break effect of their header is 0).</summary>
    internal int AnchoredEffectsSeen;

    /// <summary>The ticks at which the first target holds the arrow in <c>TouchingEntity</c>.</summary>
    internal readonly List<int> TargetTouchedTicks = new();

    /// <summary>The generator after each frame (key: the tick that ends the frame) and the steps it made during it.</summary>
    internal readonly Dictionary<int, uint> StateAfter = new();
    internal readonly Dictionary<int, int> StepsDuring = new();

    /// <summary>Production path only: the calls of the spawn seam of the shot, the sprite types of the entities the hero fathered, the held slots just before the birth tick (once filled), the pointer of the shot record of the hero after each tick.</summary>
    internal readonly List<PlayerManager.WeaponSpawnCall> SpawnCalls = new();
    internal readonly HashSet<int> TypesFatheredByTheHero = new();
    internal int HeldBeforeBirthTick = -1;
    internal readonly Dictionary<int, uint> PointerAfter = new();

    /// <summary>The harness changes of a scenario, the way the oracle's scripts make them (<c>after_tick</c> of the harness): told the tick at the head of each tick (before the passes) and at its end (after the sampling); a change made at the end is seen by the next tick.</summary>
    internal Action<int>? BeforeEachTick;
    internal Action<int>? AfterEachTick;

    /// <summary>The state of an arrow at the end of a tick, in the units of the DLL (16.16 raw).</summary>
    internal readonly record struct ArrowTick(EntityStatus Status, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, int Adjusted, uint Direction, uint Animation, uint Flags, int ResetFlag, bool Present)
    {
        /// <summary>The line of the oracle's format (<see cref="ArrowOracle.Line"/>), "absent" when the arrow is not in the world.</summary>
        internal string Line => !Present
            ? "absent"
            : Status == EntityStatus.FlagToDestroy
                ? string.Create(CultureInfo.InvariantCulture, $"st={(int)Status} x={X} y={Y} z={Z} fx={ForceX} fy={ForceY} fz={ForceZ} adj={Adjusted}") // a destroyed arrow: the key's line stops at the wall flag (the writes of the binary's bounce on the dead entity are not made, D-F5-3).
                : string.Create(CultureInfo.InvariantCulture, $"st={(int)Status} x={X} y={Y} z={Z} fx={ForceX} fy={ForceY} fz={ForceZ} adj={Adjusted} dir={Direction} anim={Animation} flags=0x{Flags:X} reset={ResetFlag}");
    }

    /// <summary>
    /// <paramref name="production"/>: the shot goes through the PRODUCTION path (no birth hook and no test set of wired types), so the arrow is born only if <see cref="PlayerManager.ProductionWiredWeaponTypes"/> holds its type.
    /// <paramref name="freeSlotsAtBirth"/> (production only): the entity table is filled with arrows just before the birth tick, down to that number of free slots.
    /// <paramref name="pad"/>: the pad word of the frame that starts at a tick (default: Square at the tick 1). <paramref name="alsoKeep"/>: an entity of the map, other than the hero and the Beannoid targets, that stays in the world (its first match is <see cref="Kept"/>).
    /// </summary>
    internal ArrowRun(string name, int item, Func<int, int, CutBench.Cell>? cell, int heroX = 100, int heroY = 100, uint direction = 24, IReadOnlyList<int[]>? targets = null, bool production = false, int? freeSlotsAtBirth = null, Func<int, uint>? pad = null, Func<EntityScriptProxy, bool>? alsoKeep = null, uint? generatorAtTick1 = null)
    {
        _production = production;
        _freeSlotsAtBirth = freeSlotsAtBirth;
        _pad = pad ?? (tick => tick == ArrowOracle.Press ? Square : 0u);
        _generatorAtTick1 = generatorAtTick1 ?? Seed;
        ArrowType = item == 5 ? 3 : 4;
        Arena = new HitArena("f5-" + name, 100, 100, targets?.ToList() ?? new List<int[]>(), direction, 0);
        try
        {
            Bench = SyntheticField.Install(Arena.Arc, cell);
            if (heroX != 100 || heroY != 100)
            {
                Arena.Arc.PlaceHero(heroX, heroY, 0);
            }

            var keep = new HashSet<EntityScriptProxy>(Arena.Targets) { Arena.Hero };
            if (alsoKeep is not null)
            {
                Kept = Arena.Arc.Entities.FirstOrDefault(e => !keep.Contains(e) && alsoKeep(e));
                if (Kept is not null)
                {
                    keep.Add(Kept);
                }
            }

            foreach (var other in Arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arena.Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arena.Arc.Proxy, Heard);
            Arena.Arc.Give(item);
            Arena.Arc.Equip(BowSlot);
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { ArrowType };
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Removes <paramref name="entity"/> from the world before the scenario (its slot is freed): the way to put the arrow in a lower slot than a target, as the oracle does.</summary>
    internal void Recycle(EntityScriptProxy entity)
    {
        entity.Status = EntityStatus.FlagToDestroy;
        Arena.Arc.Proxy.RecycleDestroyedEntities();
    }

    /// <summary>The sounds the world played, with the tick of the sampling.</summary>
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

    /// <summary>The tick that ends the frame that holds <paramref name="tick"/>, for frames of <paramref name="ticksPerFrame"/> ticks from the tick 1.</summary>
    internal static int FrameEnd(int tick, int ticksPerFrame) => ((tick - 1) / ticksPerFrame + 1) * ticksPerFrame;

    /// <summary>The state of the arrow at the end of the tick <paramref name="tick"/>, the line of the oracle's format.</summary>
    internal string Line(int tick) => Ticks.TryGetValue(tick, out var t) ? t.Line : "absent";

    /// <summary>Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks (the pad of a frame is the one of its first tick).</summary>
    internal void Go(int ticksPerFrame, int lastTick)
    {
        _frameSize = ticksPerFrame;
        if (_production)
        {
            PlayerManager.WeaponSpawnCallsForTests = SpawnCalls;
        }
        else
        {
            PlayerManager.WiredWeaponTypesForTests = new HashSet<int> { ArrowType };
            PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
            {
                if (call.Type == ArrowType)
                {
                    ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
                }
            };
        }

        Arena.BeforeTick = tick =>
        {
            _frameEnd = FrameEnd(tick, _frameSize);
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = _generatorAtTick1;
            }

            if ((tick - 1) % _frameSize == 0)
            {
                _frameStartState = unchecked((uint)GameRandom.RandomSeed);
            }

            if (_freeSlotsAtBirth is { } free && tick == ArrowOracle.Birth)
            {
                var world = (IEntityWorldContext)Arena.Arc.Proxy;
                while (EntitySlotTable.SlotCount - Arena.Arc.Proxy.SlotTable.HeldCount > free)
                {
                    if (world.SpawnEntityBySpriteIndex(Arena.Hero, false, FillerType, 100 << 16, 100 << 16, 0, 0x10) is null)
                    {
                        throw new InvalidOperationException("the filler was not born");
                    }
                }

                HeldBeforeBirthTick = Arena.Arc.Proxy.SlotTable.HeldCount;
            }

            BeforeEachTick?.Invoke(tick);
            if (Born is null)
            {
                LowestFreeSlotBeforeBirth = Enumerable.Range(0, EntitySlotTable.SlotCount).FirstOrDefault(i => !Arena.Arc.Proxy.SlotTable.IsHeld(i), -1);
            }
        };
        Arena.AfterTick = tick =>
        {
            if (Born is null)
            {
                Born = Arena.Arc.Entities.FirstOrDefault(e => e.SpriteType == ArrowType && ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity));
                if (Born is not null)
                {
                    BirthTick = tick;
                    BirthSlot = Born.Slot;
                    Born.EvaluateFrameBox();
                    BirthStatics = string.Create(CultureInfo.InvariantCulture, $"flags=0x{Born.Flags:X} atk=({Born.CurrentAttack?.Attribute ?? -1},{Born.CurrentAttack?.Power ?? -1}) box=({Born.CollisionWidth},{Born.CollisionDepth},{Born.CollisionHeight}) hbz={(Born.PosZ + Born.CollisionOffsetZ) >> 16}");
                }
            }

            foreach (var fathered in Arena.Arc.Entities.Where(e => ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity)))
            {
                TypesFatheredByTheHero.Add(fathered.SpriteType);
            }

            PointerAfter[tick] = Arena.Hero.WeaponRecordPointer;
            var present = Born is not null && Arena.Arc.Entities.Contains(Born);
            var target = Arena.Targets.FirstOrDefault();
            if (Born is not null && target?.TouchingEntity is { } touching && ReferenceEquals(touching, Born.SelfEntity))
            {
                TargetTouchedTicks.Add(tick);
            }

            if (Born?.Controller is { } controller)
            {
                ControllerAfter[tick] = (controller.Settings.WalkabilityMask, controller.Settings.Gravity);
            }

            if (Born is { } b)
            {
                Ticks[tick] = new ArrowTick(b.Status, b.PosX, b.PosY, b.PosZ, b.ForceX, b.ForceY, b.ForceZ, b.ForceAdjusted, b.TargetDirection, b.TargetAnimationId, b.Flags, b.ForceResetAnimationFlag, present);
            }

            var sample = Arena.Samples[^1];
            Hits[tick] = (Born?.HitCounter ?? 0, target?.FrameCollisionTickCounter ?? 0, sample.Sparks.Count, Born?.TouchingEntity is not null);
            AfterEachTick?.Invoke(tick);
        };

        Arena.Begin();
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(_pad(tick), ticksPerFrame);
            var end = tick + ticksPerFrame - 1;
            LastTick = end;
            var after = unchecked((uint)GameRandom.RandomSeed);
            StateAfter[end] = after;
            StepsDuring[end] = Steps(_frameStartState, after);
            AnchoredEffectsSeen += Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Count(slot => slot.Anchor != null);
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

    public void Dispose()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        PlayerManager.WeaponSpawnCallsForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        Arena.Dispose();
    }
}
