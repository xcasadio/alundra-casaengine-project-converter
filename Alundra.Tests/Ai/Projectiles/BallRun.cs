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
/// E20.f6a T1 (docs/plan-e20-heros.md section 1.22.1 and the "Revision au HEAD" points 3 and 10): the bench of the fire and ice balls (sprite types 11 and 14), the model of <see cref="ArrowRun"/>. The REAL world of map 14 (<see cref="HitArena"/>), the field of the scenario on the same records as its
/// cell store (<see cref="SyntheticField"/>, with the ground property of each cell), every entity of the map but the hero and the targets asked for removed, the hero at (100, 100) facing east with the rod (the fire rod, item 16, or the ice rod, item 14) equipped, the generator at the seed of the oracle at the head of the tick 1,
/// the pad of the oracle (Square at the tick 1, P = 1, and nothing else). The ball is born by the TEST HOOK from the exact place of <c>MovePlayer</c> where it leaves (S = P + 11 = 12); by default no native handler runs for it (E20.f6a has no C60 and no E9). Everything the tests compare is recorded here, tick by tick.
/// E20.f6b T1 (phase A): with <c>handlers</c> the native handlers of the balls (<see cref="BallAi"/>, C60 and E9) run: the run registers them in the registry of the runner when it does not hold them yet (a second registration of a key throws, so the same run is valid before and after the registration
/// in production, <see cref="BallHook"/>), turns the arena switch of D-F4-7 on for the sprite types of the run, and records the effects the stage creates, the sounds, the box of the ball and the hits. A rod held to the tick 95 and released at 96 (<c>charged</c>, the pad of the oracle, given by the test) shoots the charged ball
/// (the type 12 or 15); the normal ball of the start of the hold (the type 11 or 14) is born at P + 11 = 12 all the same, as in the oracle.
/// </summary>
internal sealed class BallRun : IDisposable
{
    private const uint Square = PadState.Square;
    internal const uint Seed = 0x12345678;

    /// <summary>The effect the pick phase gives to a destroyed entity on a deep-water slope: <c>DestroyEntity(e, 6)</c>.</summary>
    internal const int SlopeBreakEffect = 6;

    private int _frameSize = 1;
    private int _frameEnd;
    private uint _frameStartState;
    private readonly Func<int, uint> _pad;
    private readonly bool _handlers;
    private readonly uint _seed;
    private readonly IDisposable? _hook;
    private readonly HashSet<EffectSlot> _attachedSeen = new();

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;

    /// <summary>The sprite type of the ball this run follows: 11 for the fire rod, 14 for the ice rod, 12 and 15 when charged.</summary>
    internal readonly int BallType;

    /// <summary>The sprite type of the normal ball of the same rod (11 or 14): the ball itself unless the run is charged.</summary>
    internal readonly int NormalType;

    /// <summary>The ball once seen (the proxy stays valid after the entity is recycled), and the tick of its birth.</summary>
    internal EntityScriptProxy? Born;
    internal int BirthTick;

    /// <summary>The last tick that ran.</summary>
    internal int LastTick;

    /// <summary>The state of the ball at the end of each tick (the proxy stays valid after the recycling); absent while it is not in the world.</summary>
    internal readonly Dictionary<int, BallTick> Ticks = new();

    /// <summary>The ticks (the end of the frame that holds them) at which an effect <see cref="SlopeBreakEffect"/> attached to an entity is seen on the stage for the first time.</summary>
    internal readonly List<int> SlopeBreakSeenAtFrameEnd = new();

    /// <summary>The generator after each frame (key: the tick that ends the frame) and the steps it made during it.</summary>
    internal readonly Dictionary<int, uint> StateAfter = new();
    internal readonly Dictionary<int, int> StepsDuring = new();

    /// <summary>The slope (<c>Slope_18c</c>) of the targets of the run after each tick, by index of target.</summary>
    internal readonly Dictionary<int, int[]> TargetSlopes = new();

    /// <summary>Told the tick at the head of each tick (before the passes) and at its end (after the sampling).</summary>
    internal Action<int>? BeforeEachTick;
    internal Action<int>? AfterEachTick;

    /// <summary>Told the tick at the end of each tick BEFORE it is sampled: a change made here (the injection of a slope, as the keys of the branch A do) is in the line of that tick and is seen by the next tick's passes.</summary>
    internal Action<int>? InjectEachTick;

    /// <summary>
    /// The state of a ball at the end of a tick, in the units of the DLL (16.16 raw). <c>ResetAnimation</c> is <c>+0xAC</c> (the end of the animation); <c>Box</c> is the frame box ("WxDxH hbz=Z" in pixels), filled with the handlers only.
    /// </summary>
    internal readonly record struct BallTick(EntityStatus Status, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, int Adjusted, int IsOnGround, int Slope, uint Flags, bool Present, int ResetAnimation = 0, string Box = "");

    /// <summary>The effects the stage created (handlers only): the tick of the END of the frame that holds them, the identifier, the animation, the position in pixels (3 decimals) and the speed (4 decimals), as the oracle prints them.</summary>
    internal readonly List<(int Tick, int Id, int Direction, string X, string Y, string Z, string Vx, string Vy, string Vz, int Slot)> Effects = new();

    /// <summary>The sounds the world played, with the tick of the sampling (handlers only).</summary>
    internal readonly Sounds Heard = new();

    /// <summary>The frame ends at which an effect attached to an entity (<c>attach(id=N)</c>: the break effect 6 of the water, the effects 4 and 5 of a hit) is first seen on the stage, by identifier (handlers only).</summary>
    internal readonly Dictionary<int, List<int>> AttachedSeenAtFrameEnd = new();

    /// <summary>The slot of the reservoir of each effect attached to an entity, by identifier, in the order they were first seen (handlers only): the order of creation is the order of the lowest free slots.</summary>
    internal readonly Dictionary<int, List<int>> AttachedSlots = new();

    /// <summary>The number of sets of sparks the hit pass made at each tick, the hit counter of the ball and whether the ball touches something (handlers only).</summary>
    internal readonly Dictionary<int, (int Sparks, int HitCounter, bool Touching)> Hits = new();

    /// <summary>The sounds the world played.</summary>
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

    /// <summary>The cell of a field of ground properties: the walkability, the height and the ground property of each cell (the ground property of the oracle's cell tuple: 0x08 is deep water, 0x02 the slope 1).</summary>
    internal static Func<int, int, CutBench.Cell> Field(Func<int, int, (int Height, int GroundProperty)> cell)
        => (column, row) =>
        {
            var (height, groundProperty) = cell(column, row);
            return new CutBench.Cell(0, height, null, groundProperty);
        };

    /// <summary>The columns 5 to 9 on the ground property <paramref name="groundProperty"/> at height 0, every other cell dry (the field of the scenarios gp8, gp2, gp4 and gp6 of the oracle).</summary>
    internal static Func<int, int, CutBench.Cell> Columns(int firstColumn, int lastColumn, int groundProperty, int height = 0)
        => Field((column, row) => column >= firstColumn && column <= lastColumn ? (height, groundProperty) : (0, 0));

    /// <param name="item">16 for the fire rod, 14 for the ice rod.</param>
    /// <param name="cell">The cells of the scenario, or null for a dry flat field.</param>
    /// <param name="targets">The pixels of the Beannoids of the map kept as targets (records 0 to n - 1), as for <see cref="HitArena"/>.</param>
    /// <param name="pad">The pad word of the frame that starts at a tick (default: Square at the tick 1).</param>
    /// <param name="heroX">The pixel X of the hero (100 in the oracle; 105 to 109 for the balls shot near a wall).</param>
    /// <param name="charged">The ball of the run is the charged one (12 or 15); the pad is the test's (Square to the tick 95, released at 96).</param>
    /// <param name="handlers">E20.f6b: the native handlers C60 and E9 run for the types of the run (see the class).</param>
    /// <param name="seed">The word of the generator at the head of the tick 1 (the oracle's 0x12345678; the keys with a target hold the word two steps on, the target having drawn twice before the first tick: the key's <c>Rng0(1)</c>).</param>
    internal BallRun(string name, int item, Func<int, int, CutBench.Cell>? cell, IReadOnlyList<int[]>? targets = null, Func<int, uint>? pad = null, int heroX = 100, bool charged = false, bool handlers = false, uint seed = Seed)
    {
        _seed = seed;
        _pad = pad ?? (tick => tick == 1 ? Square : 0u);
        _handlers = handlers;
        NormalType = item == 16 ? 11 : 14;
        BallType = charged ? (item == 16 ? 12 : 15) : NormalType;
        Arena = new HitArena("f6a-" + name, heroX, 100, targets?.ToList() ?? new List<int[]>(), 24, 0);
        try
        {
            Bench = SyntheticField.Install(Arena.Arc, cell);
            var keep = new HashSet<EntityScriptProxy>(Arena.Targets) { Arena.Hero };
            foreach (var other in Arena.Arc.Entities.Where(e => !keep.Contains(e)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arena.Arc.Proxy.RecycleDestroyedEntities();
            Arena.Arc.Give(item);
            EquipSlotOf(item);
            if (handlers)
            {
                typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arena.Arc.Proxy, Heard);
                _hook = BallHook.Install(Arena.Arc.Runner);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void EquipSlotOf(int item)
    {
        for (var slot = 1; slot <= 6; slot++)
        {
            Arena.Arc.Equip(slot);
            if (PlayerManager.GetItemIdFromCurrentWeapon(ArcRun.State, ((IScriptHost)Arena.Arc.Proxy).ItemTables!) == (uint)item)
            {
                return;
            }
        }

        throw new InvalidOperationException($"the item {item} is in no weapon slot");
    }

    /// <summary>The tick that ends the frame that holds <paramref name="tick"/>, for frames of <paramref name="ticksPerFrame"/> ticks from the tick 1.</summary>
    internal static int FrameEnd(int tick, int ticksPerFrame) => ((tick - 1) / ticksPerFrame + 1) * ticksPerFrame;

    internal static string F3(int raw) => (raw / 65536.0).ToString("F3", CultureInfo.InvariantCulture);

    internal static string F4(int raw) => (raw / 65536.0).ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks (the pad of a frame is the one of its first tick).</summary>
    internal void Go(int ticksPerFrame, int lastTick)
    {
        _frameSize = ticksPerFrame;
        PlayerManager.WiredWeaponTypesForTests = new HashSet<int> { BallType, NormalType };
        PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
        {
            if (call.Type == BallType || call.Type == NormalType)
            {
                ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
            }
        };
        if (_handlers)
        {
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int> { BallType, NormalType };
            NativeEffects.CreatedForTests = slot =>
            {
                if (!slot.TargetMapTable)
                {
                    Effects.Add((_frameEnd, slot.TargetEffect, slot.TargetAnimation, F3(slot.X), F3(slot.Y), F3(slot.Z), F4(slot.ForceX), F4(slot.ForceY), F4(slot.ForceZ), slot.Index));
                }
            };
        }

        Arena.BeforeTick = tick =>
        {
            _frameEnd = FrameEnd(tick, _frameSize);
            Heard.Tick = tick;
            if (tick == 1)
            {
                GameRandom.RandomSeed = _seed;
            }

            if ((tick - 1) % _frameSize == 0)
            {
                _frameStartState = unchecked((uint)GameRandom.RandomSeed);
            }

            BeforeEachTick?.Invoke(tick);
        };
        Arena.AfterTick = tick =>
        {
            InjectEachTick?.Invoke(tick);
            if (Born is null)
            {
                Born = Arena.Arc.Entities.FirstOrDefault(e => e.SpriteType == BallType && ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity));
                if (Born is not null)
                {
                    BirthTick = tick;
                }
            }

            if (Born is { } b)
            {
                var present = Arena.Arc.Entities.Contains(b);
                var box = string.Empty;
                if (_handlers && present)
                {
                    b.EvaluateFrameBox();
                    box = string.Create(CultureInfo.InvariantCulture, $"{(b.CollisionWidth + 1) >> 16}x{(b.CollisionDepth + 1) >> 16}x{(b.CollisionHeight + 1) >> 16} hbz={(b.PosZ + b.CollisionOffsetZ) >> 16}");
                }

                Ticks[tick] = new BallTick(b.Status, b.PosX, b.PosY, b.PosZ, b.ForceX, b.ForceY, b.ForceZ, b.ForceAdjusted, b.IsOnGround, b.Slope_18c, b.Flags, present, b.ForceResetAnimationFlag, box);
            }

            if (_handlers)
            {
                var sample = Arena.Samples[^1];
                Hits[tick] = (sample.Sparks.Count, Born?.HitCounter ?? 0, Born?.TouchingEntity is not null);
            }

            TargetSlopes[tick] = Arena.Targets.Select(t => t.Slope_18c).ToArray();
            AfterEachTick?.Invoke(tick);
        };

        Arena.Begin();
        var seenBreak = false;
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(_pad(tick), ticksPerFrame);
            var end = tick + ticksPerFrame - 1;
            LastTick = end;
            var after = unchecked((uint)GameRandom.RandomSeed);
            StateAfter[end] = after;
            StepsDuring[end] = ArrowRun.Steps(_frameStartState, after);
            var breakNow = Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Any(slot => slot.Anchor != null && slot.TargetEffect == SlopeBreakEffect);
            if (breakNow && !seenBreak)
            {
                SlopeBreakSeenAtFrameEnd.Add(end);
            }

            seenBreak = breakNow;
            if (_handlers)
            {
                foreach (var slot in Arena.Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Where(s => s.Anchor != null && !s.TargetMapTable))
                {
                    if (!_attachedSeen.Add(slot))
                    {
                        continue;
                    }

                    if (!AttachedSeenAtFrameEnd.TryGetValue(slot.TargetEffect, out var ends))
                    {
                        AttachedSeenAtFrameEnd[slot.TargetEffect] = ends = new List<int>();
                    }

                    ends.Add(end);
                    if (!AttachedSlots.TryGetValue(slot.TargetEffect, out var slots))
                    {
                        AttachedSlots[slot.TargetEffect] = slots = new List<int>();
                    }

                    slots.Add(slot.Index);
                }
            }
        }
    }

    /// <summary>The state of the ball at the end of <paramref name="tick"/>, or null when the ball has not been seen yet.</summary>
    internal BallTick? At(int tick) => Ticks.TryGetValue(tick, out var t) ? t : null;

    /// <summary>The slope of the ball at the end of <paramref name="tick"/>, -1 when it is not in the world.</summary>
    internal int SlopeAt(int tick) => Ticks.TryGetValue(tick, out var t) && t.Present ? t.Slope : -1;

    /// <summary>The line of the oracle's format (<see cref="BallOracle.Line"/>) for the state at <paramref name="tick"/>, "absent" when the ball is not in the world.</summary>
    internal string Line(int tick)
        => Ticks.TryGetValue(tick, out var t) && t.Present
            ? string.Create(CultureInfo.InvariantCulture, $"st={(int)t.Status} x={t.X} z={t.Z} slope={t.Slope} grounded={t.IsOnGround}")
            : "absent";

    public void Dispose()
    {
        PlayerManager.WeaponSpawnBirthHookForTests = null;
        PlayerManager.WiredWeaponTypesForTests = null;
        if (_handlers)
        {
            EventProgramRunner.NativeSpriteTypeFilterForTests = null;
            NativeEffects.CreatedForTests = null;
        }

        _hook?.Dispose();
        Arena.Dispose();
    }
}

/// <summary>
/// E20.f6b T1: the test hook of C60 and E9, the model of <c>BreakHook</c> (E20.g2b). The registry of the runner holds the keys (C, 60) and (E, 9) once phase B is in; before it, a test registers them itself, through the disposable that <see cref="BallAi.Register"/> returns, and ONLY when
/// the registry already holds them for no type of the six (a second registration of a key throws). The same test is valid before and after phase B, without exception.
/// </summary>
internal static class BallHook
{
    /// <summary>Registers C60 and E9 in the registry of <paramref name="runner"/> when it does not hold them; the disposable removes what this call registered (a no-op when the keys were already there).</summary>
    internal static IDisposable Install(EventProgramRunner runner)
    {
        var registry = runner.NativeSprites;
        var held = BallAi.SpriteTypes.Any(type => registry.TryGet(ScriptHelper.ProgramCTick, BallAi.TickCode, type, out _) || registry.TryGet(ScriptHelper.ProgramEDeactivate, BallAi.DeactivateCode, type, out _));
        return held ? new Nothing() : BallAi.Register(registry);
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
