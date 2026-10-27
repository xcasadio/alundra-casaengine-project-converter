#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using Alundra.Ai;
using Alundra.Ai.Items;
using Alundra.Audio;
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
/// E20.h4c1 T1 (docs/plan-e20-heros.md section 1.31.3, rule R-EL-4): the bench of the world tests of the earth tome 1. The REAL world of map 14 (<see cref="HitArena"/>: real prefabs, the real controller of the hero), the flat field of 52 x 60 cells of the scenario
/// (<see cref="SyntheticField"/>, the cell store on the same records), every entity of the map but the hero and the targets asked for removed, the generator at the seed of the oracle at the head of the tick 1, the tome 43 given and equipped, the magic 4/4, ONE tick per frame, the Circle
/// pressed at the tick 1. The native handlers of the arena are OFF; the switch of D-F4-7 is on for the type 16 alone (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: E3 runs, a Beannoid stays inert). The three seams of h0a are those of phase A (D-H-4): the start is the
/// start of the tomes of h4a, the driver is <see cref="EarthTome1Ai.Driver"/>, the cancellation is <see cref="EarthTome1Ai.Cancel"/>; E3 is registered on the registry of the runner ONLY IF its key is absent (the phase B registers it in production). Everything the tests compare is
/// recorded here, tick by tick: the state of every rock as the inventory pass of the tick saw it, its animation row and its current animation as the END of the frame saw them (the DLL validates the switch of animation there, D-H4C1-3).
/// </summary>
internal sealed class EarthTome1Run : IDisposable
{
    internal const uint Seed = 0x12345678;
    internal const int Rock = 16;

    private IDisposable? _registration;
    private int _tick;
    private uint _stateBefore;

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>One rock as the inventory pass of the tick saw it (the evaluated box included), and as the end of the frame saw its animation.</summary>
    internal sealed record RockRow(int Tick, int Slot, int Status, int PosX, int PosY, int PosZ, int ForceX, int ForceY, int ForceZ, uint Direction, uint TargetAnimation, int AnimationEnd, uint Flags, int Adjusted, int Ground, int Slope,
        int Counter, int Width, int Depth, int Height, int Hits, uint RowAtEnd, uint CurrentAnimationAtEnd, EntityScriptProxy Entity);

    internal sealed record HeroRow(int Tick, uint Animation, uint ItemInUse, int Phase, int Counter, int PosX, int PosY, int PosZ);

    internal sealed record TargetRow(int Tick, int Status, int Counter, int? TouchedBy, int PosX, int PosY);

    /// <summary>The rocks by tick, then by slot.</summary>
    internal readonly Dictionary<int, Dictionary<int, RockRow>> Rocks = new();

    internal readonly Dictionary<int, HeroRow> Hero = new();

    /// <summary>The first target by tick (a scenario with targets only).</summary>
    internal readonly Dictionary<int, TargetRow> Target = new();

    /// <summary>The steps of the generator between the head and the end of each tick (the inventory pass).</summary>
    internal readonly Dictionary<int, int> Draws = new();

    /// <summary>The sparks of the hit pass of each tick (the centres, raw 16.16).</summary>
    internal readonly Dictionary<int, List<(int X, int Y, int Z)>> Sparks = new();

    /// <summary>The steps of the generator during each frame (the head of its tick to the end of the frame): the key's draws plus the step of the end of the frame.</summary>
    internal readonly Dictionary<int, int> FrameSteps = new();

    /// <summary>The effects of each frame, as the key writes them without what the stage changes: <c>fx(id=9,x=.,y=.,z=.)</c> three times for a hit (its centre, in pixels) and <c>attach(id=6,x=.,y=.,z=.)</c> for an effect attached to an
    /// entity (first seen on the stage at the end of the frame: the raw position of its anchor, the binary's Z).</summary>
    internal readonly Dictionary<int, List<string>> Effects = new();

    private readonly HashSet<(int Index, int Generation)> _attachedSeen = new();

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

    /// <param name="heroDirection">The direction of the hero (24 east, 0 south, 8 west, 16 north).</param>
    /// <param name="heroX">The pixel X of the hero.</param>
    /// <param name="cell">The field (default: flat, all zero).</param>
    /// <param name="targets">The pixels (X, Y) of the Beannoids put on the bench (inert).</param>
    internal EarthTome1Run(string name, uint heroDirection = 24, int heroX = 100, Func<int, int, CutBench.Cell>? cell = null, IReadOnlyList<int[]>? targets = null)
    {
        Arena = new HitArena("h4c1-" + name, 100, 100, targets?.ToList() ?? new List<int[]>(), heroDirection, 0);
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
            Arena.Arc.Give(0x2B);
            Arena.Arc.SetMp(4, 4);
            PlayerManager.SetCurrentItemId(GameState.Instance, 0x2B);
            Arena.Hero.FrameCollisionTickCounter = 0;
            Arena.Hero.DamagedTickCounter = 0;
            Arena.Hero.HitCounter = 0;
            Arena.Hero.TouchingEntity = null;

            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { Rock };
            PlayerManager.ItemStartForTests = PlayerManager.TomeStart;
            PlayerManager.ItemDriverForTests = (hero, _) => EarthTome1Ai.Driver(hero);
            PlayerManager.ItemCancelForTests = (hero, _) => EarthTome1Ai.Cancel(hero);
            var registry = Arena.Arc.Runner.NativeSprites;
            if (!registry.TryGet(ScriptHelper.ProgramEDeactivate, EarthTome1Ai.ImpactCode, Rock, out _))
            {
                _registration = EarthTome1Ai.Register(registry);
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
    /// Runs the ticks 1 to <paramref name="last"/>, one per frame. <paramref name="interruptAt"/>: the animation 0x31 is posed on the hero before the tick (R-EL-6: the head of the tick, as the oracle's poke). <paramref name="before"/> is told the tick at its head, after the generator is seeded.
    /// </summary>
    internal void Go(int last, int? interruptAt = null, Action<int>? before = null)
    {
        Arena.BeforeTick = tick =>
        {
            _tick = tick;
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = Seed;
            }

            _stateBefore = State();
            if (interruptAt == tick)
            {
                Arena.Hero.TargetAnimationId = 0x31;
            }

            before?.Invoke(tick);
        };
        Arena.AfterTick = tick =>
        {
            Draws[tick] = ChargedArrowSteps(_stateBefore, State());
            var rows = new Dictionary<int, RockRow>();
            foreach (var rock in Arena.Arc.Entities.Where(e => e.SpriteType == Rock).OrderBy(e => e.Slot))
            {
                rock.EvaluateFrameBox();
                rows[rock.Slot] = new RockRow(tick, rock.Slot, (int)rock.Status, rock.PosX, rock.PosY, rock.PosZ, rock.ForceX, rock.ForceY, rock.ForceZ, rock.TargetDirection, rock.TargetAnimationId, rock.ForceResetAnimationFlag, rock.Flags,
                    rock.ForceAdjusted, rock.IsOnGround, rock.Slope_18c, rock.FrameCollisionTickCounter, rock.CollisionWidth, rock.CollisionDepth, rock.CollisionHeight, rock.HitCounter, 0, 0, rock);
            }

            Rocks[tick] = rows;
            var hero = Arena.Hero;
            Hero[tick] = new HeroRow(tick, hero.TargetAnimationId, hero.ItemInUse, hero.ItemUsePhase, hero.ItemUseCounter, hero.PosX, hero.PosY, hero.PosZ);
            if (Arena.Targets.Count > 0)
            {
                var target = Arena.Targets[0];
                Target[tick] = new TargetRow(tick, (int)target.Status, target.FrameCollisionTickCounter, target.TouchingEntity?.GameplayProxy is EntityScriptProxy toucher ? toucher.Slot : null, target.PosX, target.PosY);
            }

            Sparks[tick] = Arena.Samples[^1].Sparks.ToList();
        };

        Arena.Begin();
        for (var tick = 1; tick <= last; tick++)
        {
            Effects[tick] = new List<string>();
            Arena.Frame(tick == 1 ? PadState.Circle : 0, 1);
            FrameSteps[tick] = ChargedArrowSteps(_stateBefore, State());
            // The three sparks of each hit, at the centre the hit pass wrote (the stage updates the sparks at once: their place is read from the centre, their speeds are the draws the steps of the frame count).
            foreach (var (x, y, z) in Sparks[tick])
            {
                for (var k = 0; k < NativeEffects.HitSparkCount; k++)
                {
                    Effects[tick].Add($"fx(id=9,x={F3(x)},y={F3(y)},z={F3(z)})");
                }
            }

            foreach (var slot in Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Where(s => s.Anchor != null && !s.TargetMapTable))
            {
                if (_attachedSeen.Add((slot.Index, slot.Generation)))
                {
                    Effects[tick].Add($"attach(id={slot.TargetEffect},x={slot.Anchor!.PosX},y={slot.Anchor.PosY},z={slot.Anchor.PosZ})");
                }
            }

            // The END of the frame: the DLL validates the switch of animation there (FrameSyncPasses), the row and the current animation of a rock are read now (D-H4C1-3).
            var rows = Rocks[tick];
            foreach (var slot in rows.Keys.ToList())
            {
                var entity = rows[slot].Entity;
                rows[slot] = rows[slot] with { RowAtEnd = (uint)entity.AnimationDirection, CurrentAnimationAtEnd = entity.CurrentAnimationId };
            }

            LastTick = tick;
        }
    }

    private static string F3(int raw) => (raw / 65536.0).ToString("F3", CultureInfo.InvariantCulture);

    private static string F4(int raw) => (raw / 65536.0).ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>The number of steps of the generator from <paramref name="from"/> to <paramref name="to"/> (at most 64), -1 when it is not reached.</summary>
    internal static int ChargedArrowSteps(uint from, uint to)
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
        PlayerManager.ItemStartForTests = null;
        PlayerManager.ItemDriverForTests = null;
        PlayerManager.ItemCancelForTests = null;
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        _registration?.Dispose();
        Arena.Dispose();
    }
}
