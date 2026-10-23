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
/// E20.f6a T1 (docs/plan-e20-heros.md section 1.22.1 and the "Revision au HEAD" points 3 and 10): the bench of the fire and ice balls (sprite types 11 and 14), the model of <see cref="ArrowRun"/>. The REAL world of map 14 (<see cref="HitArena"/>), the field of the scenario on the same records as its
/// cell store (<see cref="SyntheticField"/>, with the ground property of each cell), every entity of the map but the hero and the targets asked for removed, the hero at (100, 100) facing east with the rod (the fire rod, item 16, or the ice rod, item 14) equipped, the generator at the seed of the oracle at the head of the tick 1,
/// the pad of the oracle (Square at the tick 1, P = 1, and nothing else). The ball is born by the TEST HOOK from the exact place of <c>MovePlayer</c> where it leaves (S = P + 11 = 12), no native handler is registered for it (E20.f6a has no C60 and no E9). Everything the tests compare is recorded here, tick by tick.
/// </summary>
internal sealed class BallRun : IDisposable
{
    private const uint Square = PadState.Square;
    internal const uint Seed = 0x12345678;

    /// <summary>The effect the pick phase gives to a destroyed entity on a deep-water slope: <c>DestroyEntity(e, 6)</c>.</summary>
    internal const int SlopeBreakEffect = 6;

    private int _frameSize = 1;
    private uint _frameStartState;
    private readonly Func<int, uint> _pad;

    internal readonly HitArena Arena;
    internal readonly CutBench Bench;

    /// <summary>The sprite type of the ball this run shoots: 11 for the fire rod, 14 for the ice rod.</summary>
    internal readonly int BallType;

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

    /// <summary>The state of a ball at the end of a tick, in the units of the DLL (16.16 raw).</summary>
    internal readonly record struct BallTick(EntityStatus Status, int X, int Y, int Z, int ForceX, int ForceY, int ForceZ, int Adjusted, int IsOnGround, int Slope, uint Flags, bool Present);

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
    internal BallRun(string name, int item, Func<int, int, CutBench.Cell>? cell, IReadOnlyList<int[]>? targets = null, Func<int, uint>? pad = null)
    {
        _pad = pad ?? (tick => tick == 1 ? Square : 0u);
        BallType = item == 16 ? 11 : 14;
        Arena = new HitArena("f6a-" + name, 100, 100, targets?.ToList() ?? new List<int[]>(), 24, 0);
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

    /// <summary>Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks (the pad of a frame is the one of its first tick).</summary>
    internal void Go(int ticksPerFrame, int lastTick)
    {
        _frameSize = ticksPerFrame;
        PlayerManager.WiredWeaponTypesForTests = new HashSet<int> { BallType };
        PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
        {
            if (call.Type == BallType)
            {
                ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
            }
        };

        Arena.BeforeTick = tick =>
        {
            if (tick == 1)
            {
                GameRandom.RandomSeed = Seed;
            }

            if ((tick - 1) % _frameSize == 0)
            {
                _frameStartState = unchecked((uint)GameRandom.RandomSeed);
            }

            BeforeEachTick?.Invoke(tick);
        };
        Arena.AfterTick = tick =>
        {
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
                Ticks[tick] = new BallTick(b.Status, b.PosX, b.PosY, b.PosZ, b.ForceX, b.ForceY, b.ForceZ, b.ForceAdjusted, b.IsOnGround, b.Slope_18c, b.Flags, Arena.Arc.Entities.Contains(b));
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
        Arena.Dispose();
    }
}
