#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Alundra.Ai.Projectiles;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>
/// E20.f4 T1 (docs/plan-e20-heros.md section 1.20, "Revision au HEAD" points 3 and 10, bullets (c8) and "Le banc de (p1) a (p3)"): the bench of the charged arrow. The REAL world of map 14 (<see cref="HitArena"/>), the field of the scenario and its cell store on the same records
/// (<see cref="SyntheticField"/>), every entity of the map but the hero and the targets asked for removed, the native handlers of the arena OFF and the arena switch of D-F4-7 on for the type 7 only (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: C94 runs,
/// a Beannoid stays inert), the hero at (<paramref name="heroX"/>, 100) facing east with William's bow (item 6, slot 2), the generator at the seed of the oracle at the head of the tick 1, the counter of the hero set so that the hold sparks fall on the ticks of the oracle (as E20.e does).
/// The Square is held from the tick 1 to the tick 95 and released at 96 (<see cref="ChargedArrowOracle.Release"/>). The shot of the type 7 is born by the TEST HOOK from the exact place of <c>MovePlayer</c> where it leaves (the type 4 of the start of the hold is not born).
/// Everything the tests compare is recorded here, tick by tick.
/// </summary>
internal sealed class ChargedArrowRun : IDisposable
{
    private const uint Square = PadState.Square;
    private const int WilliamBow = 6;
    private const int BowSlot = 2;
    internal const uint Seed = 0x12345678;
    private const int FillerType = 3;

    private int _frameEnd;
    private uint _frameStartState;
    private int _frameSize = 1;
    private readonly bool _production;
    private readonly int? _freeSlotsAtBirth;

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The arrow once seen (the proxy stays valid after the entity is recycled), and the tick of its birth.</summary>
    internal EntityScriptProxy? Born;
    internal int BirthTick;
    internal int BirthSlot = -1;

    /// <summary>The last tick that ran (the last frame is whole: it can end after the <c>lastTick</c> asked).</summary>
    internal int LastTick;

    /// <summary>The static values of the arrow at the end of its birth tick: the flags, the attack (attribute, power), the box (width, depth, height in raw 16.16) and the pixel of the Z of the bottom of the box.</summary>
    internal string BirthStatics = string.Empty;
    internal int LowestFreeSlotBeforeBirth = -1;

    /// <summary>The line of the arrow at the end of each tick (<see cref="ChargedArrowOracle.Line"/>'s format), "absent" when it is not in the world.</summary>
    internal readonly Dictionary<int, string> Lines = new();

    /// <summary>The effects the stage created, with the tick of the END of the frame (the stage applies its queue then): the trail (25), the cut (2), the others.</summary>
    internal readonly List<(int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz)> Effects = new();

    /// <summary>The first tick (end of a frame) at which an effect 8 attached to an anchor (created in mode 1, in mode 2 once the stage has updated it in the same frame) is in the reservoir, and the position of its anchor then.</summary>
    internal int BreakSeenTick;
    internal (int X, int Y) BreakAnchor;

    /// <summary>The generator after each frame (key: the tick that ends the frame) and the steps it made during it.</summary>
    internal readonly Dictionary<int, uint> StateAfter = new();
    internal readonly Dictionary<int, int> StepsDuring = new();

    /// <summary>Per tick: the arrow's hit counter, the first target's cooldown (+0x1D0), the number of sparks of the pass, whether the arrow touches something.</summary>
    internal readonly Dictionary<int, (int HitCounter, int TargetCooldown, int Sparks, bool Touching)> Hits = new();

    /// <summary>The ticks at which the stub of the cut was called for the arrow (the journal <see cref="ChargedArrowAi.StubCallsForTests"/>).</summary>
    internal readonly List<int> StubCallTicks = new();

    private readonly List<EntityScriptProxy> _stubJournal = new();

    /// <summary>Production path only: the calls of the spawn seam of the shot (<see cref="PlayerManager.WeaponSpawnCallsForTests"/>), the sprite types of the entities the hero fathered (seen at the end of any tick), the held slots just before the birth tick (once filled), the pointer of the shot record of the hero after each tick, and per tick whether the first target touches the arrow.</summary>
    internal readonly List<PlayerManager.WeaponSpawnCall> SpawnCalls = new();
    internal readonly HashSet<int> TypesFatheredByTheHero = new();
    internal int HeldBeforeBirthTick = -1;
    internal readonly Dictionary<int, uint> PointerAfter = new();
    internal readonly Dictionary<int, bool> TargetTouchedByTheArrow = new();

    /// <summary>
    /// <paramref name="production"/>: the shot goes through the PRODUCTION path (E20.f4 T2, phase B): no birth hook and no test set of wired types, so the arrow is born only if <see cref="PlayerManager.ProductionWiredWeaponTypes"/> holds its type.
    /// <paramref name="freeSlotsAtBirth"/> (production only): the entity table is filled with arrows of type 3 just before the birth tick, down to that number of free slots (0: full).
    /// </summary>
    internal ChargedArrowRun(string name, Func<int, int, CutBench.Cell>? cell, int heroX = 100, IReadOnlyList<int[]>? targets = null, bool production = false, int? freeSlotsAtBirth = null)
    {
        _production = production;
        _freeSlotsAtBirth = freeSlotsAtBirth;
        Arena = new HitArena("f4-" + name, 100, 100, targets?.ToList() ?? new List<int[]>(), 24, 0);
        try
        {
            Bench = SyntheticField.Install(Arena.Arc, cell);
            if (heroX != 100)
            {
                Arena.Arc.PlaceHero(heroX, 100, 0);
            }

            var keep = new HashSet<EntityScriptProxy>(Arena.Targets) { Arena.Hero };
            foreach (var other in Arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arena.Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arena.Arc.Proxy, Heard);
            Arena.Arc.Give(WilliamBow);
            Arena.Arc.Equip(BowSlot);
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { ChargedArrowAi.SpriteTypes[0] };
        }
        catch
        {
            Dispose();
            throw;
        }
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

    /// <summary>The line of the arrow in the format of the key.</summary>
    internal static string Line(EntityScriptProxy? arrow, bool present)
        => arrow is null || !present
            ? "absent"
            : string.Create(CultureInfo.InvariantCulture, $"st={(int)arrow.Status} x={arrow.PosX} y={arrow.PosY} zpx={arrow.PosZ >> 16} fx={arrow.ForceX} adj={arrow.ForceAdjusted}");

    private static string F3(int raw) => (raw / 65536.0).ToString("F3", CultureInfo.InvariantCulture);

    private static string F4(int raw) => (raw / 65536.0).ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks (the pad of a frame is the one of its first tick).</summary>
    internal void Go(int ticksPerFrame, int lastTick)
    {
        _frameSize = ticksPerFrame;
        ChargedArrowAi.StubCallsForTests = _stubJournal;
        if (_production)
        {
            PlayerManager.WeaponSpawnCallsForTests = SpawnCalls;
        }
        else
        {
            PlayerManager.WiredWeaponTypesForTests = new HashSet<int> { ChargedArrowAi.SpriteTypes[0] };
            PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
            {
                if (call.Type == ChargedArrowAi.SpriteTypes[0])
                {
                    ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
                }
            };
        }
        NativeEffects.CreatedForTests = slot =>
        {
            if (!slot.TargetMapTable)
            {
                Effects.Add((_frameEnd, slot.TargetEffect, slot.TargetAnimation, F3(slot.X), F3(slot.Y), F3(slot.Z), F4(slot.ForceX), F4(slot.ForceY), F4(slot.ForceZ)));
            }
        };

        var stubSeen = 0;
        Arena.BeforeTick = tick =>
        {
            _frameEnd = FrameEnd(tick, _frameSize);
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = Seed;
                Arena.Hero.FrameCounter = 12;
            }

            if ((tick - 1) % _frameSize == 0)
            {
                _frameStartState = unchecked((uint)GameRandom.RandomSeed);
            }

            if (_freeSlotsAtBirth is { } free && tick == ChargedArrowOracle.Birth)
            {
                var world = (IEntityWorldContext)Arena.Arc.Proxy;
                while (EntitySlotTable.SlotCount - Arena.Arc.Proxy.SlotTable.HeldCount > free)
                {
                    if (world.SpawnEntityBySpriteIndex(Arena.Hero, false, FillerType, 100 << 16, 100 << 16, 0, 0x10) is null)
                    {
                        throw new InvalidOperationException("the filler arrow was not born");
                    }
                }

                HeldBeforeBirthTick = Arena.Arc.Proxy.SlotTable.HeldCount;
            }

            if (Born is null)
            {
                LowestFreeSlotBeforeBirth = Enumerable.Range(0, EntitySlotTable.SlotCount).FirstOrDefault(i => !Arena.Arc.Proxy.SlotTable.IsHeld(i), -1);
            }
        };
        Arena.AfterTick = tick =>
        {
            if (Born is null)
            {
                Born = Arena.Arc.Entities.FirstOrDefault(e => e.SpriteType == ChargedArrowAi.SpriteTypes[0] && ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity));
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
            TargetTouchedByTheArrow[tick] = Born is not null && Arena.Targets.FirstOrDefault()?.TouchingEntity is { } touching && ReferenceEquals(touching, Born);
            var present = Born is not null && Arena.Arc.Entities.Contains(Born);
            Lines[tick] = Line(Born, present);
            var sample = Arena.Samples[^1];
            var target = Arena.Targets.FirstOrDefault();
            Hits[tick] = (Born?.HitCounter ?? 0, target?.FrameCollisionTickCounter ?? 0, sample.Sparks.Count, Born?.TouchingEntity is not null);
            for (; stubSeen < _stubJournal.Count; stubSeen++)
            {
                if (ReferenceEquals(_stubJournal[stubSeen], Born))
                {
                    StubCallTicks.Add(tick);
                }
            }
        };

        Arena.Begin();
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(tick <= 95 ? Square : 0, ticksPerFrame);
            var end = tick + ticksPerFrame - 1;
            LastTick = end;
            var after = unchecked((uint)GameRandom.RandomSeed);
            StateAfter[end] = after;
            StepsDuring[end] = Steps(_frameStartState, after);
            if (BreakSeenTick == 0)
            {
                var slot = Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.FirstOrDefault(s => s.TargetEffect == 8 && !s.TargetMapTable && s.RecordIndex == -1 && s.Mode is 1 or 2 && s.Anchor != null);
                if (slot is not null)
                {
                    BreakSeenTick = end;
                    BreakAnchor = (slot.Anchor!.PosX >> 16, slot.Anchor.PosY >> 16);
                }
            }
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
        ChargedArrowAi.StubCallsForTests = null;
        NativeEffects.CreatedForTests = null;
        Arena.Dispose();
    }
}
