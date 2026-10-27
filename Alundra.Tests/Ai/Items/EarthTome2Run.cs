#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1 (docs/plan-e20-heros.md section 1.31.4, rule R-EL-4): the bench of the world tests of the earth tome 2. The REAL world of map 14 (<see cref="HitArena"/>: real prefabs, the real controller of the hero), the flat field of 52 x 60 cells of the
/// scenario (<see cref="SyntheticField"/>, or the field asked), every entity of the map but the hero (and the targets asked for) removed, the generator at the seed of the oracle at the head of the tick 1, the tome 44 (<c>0x2C</c>) given and equipped, the magic 4/4,
/// <c>ticksPerFrame</c> ticks per frame, the Circle pressed at the tick 1. The native handlers of the arena are OFF; the switch of D-F4-7 is on for the type 17 alone (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>). The three seams of h0a are those
/// of phase A (D-H-4): the start is the start of the tomes of h4a, the driver is <see cref="EarthTome2Ai.Driver"/>, the cancellation is <see cref="EarthTome2Ai.Cancel"/>; E13 is registered on the registry of the runner ONLY IF its key is absent (the phase B
/// registers it in production). Everything the tests compare is recorded here, tick by tick, at the inventory pass of the tick (the end of the tick), as the oracle's keys are.
/// </summary>
internal sealed class EarthTome2Run : IDisposable
{
    internal const uint Seed = 0x12345678;
    internal const int Rock = 17;
    internal const uint Item = 0x2C;

    private IDisposable? _registration;
    private uint _stateBefore;

    internal readonly HitArena Arena;
    internal readonly Sounds Heard = new();

    /// <summary>One rock as the inventory pass of the tick saw it (the evaluated box and the attack included).</summary>
    internal sealed record RockRow(int Tick, int Slot, int Status, int PosX, int PosY, int PosZ, int ForceX, int ForceY, int ForceZ, uint Direction, uint TargetAnimation, int AnimationEnd, uint Flags, int Adjusted, int Ground, int Slope,
        int Width, int Depth, int Height, int Attribute, int Power, bool Blocked, EntityScriptProxy Entity);

    internal sealed record HeroRow(int Tick, uint Animation, uint ItemInUse, int Phase, int Counter, int PosX, int PosY, int PosZ, int Mp);

    /// <summary>The rocks by tick, then by slot (a freed slot has no row).</summary>
    internal readonly Dictionary<int, Dictionary<int, RockRow>> Rocks = new();

    internal readonly Dictionary<int, HeroRow> Hero = new();

    /// <summary>The hit cooldown (<c>+0x1D0</c>, set to 25 by a touch) of each target put on the bench, by tick: a touch is the tick at which it reads 25.</summary>
    internal readonly Dictionary<int, int[]> TargetCounters = new();

    /// <summary>The generator at the head of each tick (after the seed of the tick 1).</summary>
    internal readonly Dictionary<int, uint> StateAtHead = new();

    /// <summary>The steps of the generator between the head and the end of each frame, indexed by the LAST tick of the frame.</summary>
    internal readonly Dictionary<int, int> FrameSteps = new();

    /// <summary>The camera kick of each tick, read at its inventory pass in the list of the frame (<see cref="WorldProxy.CameraKickOfTickForTests"/>): 0 when the tick wrote none.</summary>
    internal readonly Dictionary<int, int> Kicks = new();

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

    /// <param name="heroX">The pixel X of the hero.</param>
    /// <param name="heroY">The pixel Y of the hero.</param>
    /// <param name="cell">The field (default: flat, all zero).</param>
    /// <param name="targets">The pixels (X, Y) of the Beannoids put on the bench (inert).</param>
    internal EarthTome2Run(string name, int heroX = 100, int heroY = 100, Func<int, int, CutBench.Cell>? cell = null, IReadOnlyList<int[]>? targets = null)
    {
        Arena = new HitArena("h4c2-" + name, 100, 100, targets?.ToList() ?? new List<int[]>(), 24, 0);
        try
        {
            SyntheticField.Install(Arena.Arc, cell);
            if (heroX != 100 || heroY != 100)
            {
                Arena.Arc.PlaceHero(heroX, heroY, 0);
            }

            var keep = new HashSet<EntityScriptProxy>(Arena.Targets) { Arena.Hero };
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

            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { Rock };
            PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
            PlayerManager.ItemDriverForTests = (hero, _) => EarthTome2Ai.Driver(hero);
            PlayerManager.ItemCancelForTests = (hero, _) => EarthTome2Ai.Cancel(hero);
            var registry = Arena.Arc.Runner.NativeSprites;
            if (!registry.TryGet(ScriptHelper.ProgramEDeactivate, EarthTome2Ai.ImpactCode, Rock, out _))
            {
                _registration = EarthTome2Ai.Register(registry);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static uint State() => unchecked((uint)GameRandom.RandomSeed);

    /// <summary>
    /// Runs the ticks 1 to <paramref name="last"/>, <paramref name="ticksPerFrame"/> per frame. <paramref name="interruptAt"/>: the animation 0x31 is posed on the hero before the tick (R-EL-6: the head of the tick, as the oracle's poke).
    /// <paramref name="before"/> is told the tick at its head, after the generator is seeded.
    /// </summary>
    internal void Go(int last, int ticksPerFrame = 1, int? interruptAt = null, Action<int>? before = null)
    {
        Arena.BeforeTick = tick =>
        {
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
            var rows = new Dictionary<int, RockRow>();
            foreach (var rock in Arena.Arc.Entities.Where(e => e.SpriteType == Rock && e.Status != EntityStatus.Destroyed).OrderBy(e => e.Slot))
            {
                rock.EvaluateFrameBox();
                rows[rock.Slot] = new RockRow(tick, rock.Slot, (int)rock.Status, rock.PosX, rock.PosY, rock.PosZ, rock.ForceX, rock.ForceY, rock.ForceZ, rock.TargetDirection, rock.TargetAnimationId, rock.ForceResetAnimationFlag, rock.Flags,
                    rock.ForceAdjusted, rock.IsOnGround, rock.Slope_18c, rock.CollisionWidth, rock.CollisionDepth, rock.CollisionHeight, rock.CurrentAttack?.Attribute ?? -1, rock.CurrentAttack?.Power ?? -1, rock.BlockedByEntity != null, rock);
            }

            Rocks[tick] = rows;
            TargetCounters[tick] = Arena.Targets.Select(t => t.FrameCollisionTickCounter).ToArray();
            var hero = Arena.Hero;
            Hero[tick] = new HeroRow(tick, hero.TargetAnimationId, hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, hero.PosX, hero.PosY, hero.PosZ, GameState.Instance.PlayerStats.Mp);
            var list = Arena.Arc.Proxy.CameraKickOfTickForTests;
            var position = (tick - 1) % ticksPerFrame;
            Kicks[tick] = position < list.Count ? list[position] : 0;
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

    /// <summary>The lives of the rocks the DLL run saw, by the order of their births (tick, slot): a life starts at the first row of a slot that has no row of the tick before, or whose row of the tick before was the status 4.</summary>
    internal List<Life> Lives()
    {
        var lives = new List<Life>();
        var open = new Dictionary<int, Life>();
        for (var tick = 1; tick <= LastTick; tick++)
        {
            foreach (var (slot, row) in Rocks[tick].OrderBy(r => r.Key))
            {
                if (!open.TryGetValue(slot, out var life) || life.Rows[^1].Tick != tick - 1 || life.Rows[^1].Status == 4)
                {
                    life = new Life(slot, tick);
                    open[slot] = life;
                    lives.Add(life);
                }

                life.Rows.Add(row);
            }
        }

        return lives.OrderBy(l => l.Birth).ThenBy(l => l.Slot).ToList();
    }

    internal sealed class Life
    {
        internal Life(int slot, int birth)
        {
            Slot = slot;
            Birth = birth;
        }

        internal int Slot { get; }

        internal int Birth { get; }

        internal List<RockRow> Rows { get; } = new();

        internal RockRow? At(int tick) => Rows.FirstOrDefault(r => r.Tick == tick);

        internal int Last => Rows[^1].Tick;

        /// <summary>The first tick with the ground contact, 0 if never.</summary>
        internal int Ground => Rows.Where(r => r.Ground == 1).Select(r => r.Tick).FirstOrDefault();

        /// <summary>The first tick at the status 3, 0 if never.</summary>
        internal int Status3 => Rows.Where(r => r.Status == 3).Select(r => r.Tick).FirstOrDefault();

        /// <summary>The tick of E13, the first with the animation asked 1, 0 if never.</summary>
        internal int Impact => Rows.Where(r => r.TargetAnimation == 1).Select(r => r.Tick).FirstOrDefault();

        /// <summary>The first tick with the end of the animation held (<c>+0xAC</c> at 1), 0 if never.</summary>
        internal int AnimationEnd => Rows.Where(r => r.AnimationEnd == 1).Select(r => r.Tick).FirstOrDefault();

        internal int Death => Rows.Where(r => r.Status == 4).Select(r => r.Tick).FirstOrDefault();
    }

    public void Dispose()
    {
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        _registration?.Dispose();
        Arena.Dispose();
    }
}
