#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.f3a T2 (docs/plan-e20-heros.md section 1.19.1, D-F3-2): the direction word of the hero's shot, on the REAL world of map 14 (<see cref="HitArena"/>: the production path, the world's own update, frames of 1, 2 or 3 logic ticks)
/// with Hunter's Bow (item 5, slot 2). The binary passes <c>[0x80023644 + 4 * (+0x98)]</c> (<c>0x8002F7F8</c>) where <c>+0x98</c> is the direction index <c>UpdateAnimation</c> wrote at the end of the PREVIOUS tick: a turn asked
/// on the tick of the press counts, a turn asked on the tick of the shot or during the swing is ignored. The keys are those of the oracle of E20.f3a T0 (<c>docs/plan-e20-annexe/e20-f3/oracle/out/directions.json</c>, cases
/// <c>dirtest_air_cross_t1_square_left_t8</c> (d1), <c>dir_extra_air_square_t8_left_only_from_t9</c> (d2), the four <c>ground</c> cases (d3)). The shot is read in the journal of
/// <see cref="AlundraPlayerManager.WeaponSpawnCallsForTests"/> (<c>Direction</c>); no entity is born (the wired set is empty).
/// <para>
/// A frame of several ticks holds the pad of its FIRST tick for all its ticks (the arena's pad, which the DLL turns into the edges of each tick): in (d1) at two ticks per frame the press tick P (Square + Left) and the shot tick P+1
/// are in the same frame, so Square is still held at the shot tick (the oracle releases it): the binary does not read the Square again on that tick, and the test asserts the shot all the same.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraShotDirectionTests : IDisposable
{
    private const uint Square = AlundraPadState.Square;
    private const uint Cross = AlundraPadState.Cross;
    private const uint Left = AlundraPadState.Left;
    private const uint Right = AlundraPadState.Right;
    private const uint Up = AlundraPadState.Up;
    private const uint Down = AlundraPadState.Down;
    private const int HuntersBow = 5;
    private const int BowSlot = 2;

    public AlundraShotDirectionTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;
        AlundraPlayerManager.WiredWeaponTypesForTests = null;
    }

    /// <summary>One shot as the seam saw it: the tick of the scenario, the arguments of the call, and the two readings of the facing (the box cursor's direction at the shot and <see cref="AlundraPlayerManager.TickFacing"/> at the head of that tick).</summary>
    private sealed record Shot(int Tick, AlundraPlayerManager.WeaponSpawnCall Call, int BoxDirection, int HeadTickFacing);

    /// <summary>
    /// Runs a scenario on a fresh arena: the pad word of tick <c>t</c> is <paramref name="holdAt"/>(t); the ticks are grouped into the frames of <paramref name="frameSizes"/> (a frame holds the pad of its first tick), then one tick per frame,
    /// up to <paramref name="lastTick"/>. Returns every shot.
    /// </summary>
    private static List<Shot> Run(string name, Func<int, uint> holdAt, IReadOnlyList<int> frameSizes, int lastTick, bool jump = false)
    {
        using var arena = new HitArena("f3a-" + name, 212, 96, new List<int[]>());
        arena.Arc.Give(HuntersBow);
        arena.Arc.Equip(BowSlot);
        var shots = new List<Shot>();
        var tick = 0;
        var headFacing = 0;
        arena.BeforeTick = t =>
        {
            tick = t;
            headFacing = AlundraPlayerManager.TickFacing(arena.Hero);
        };
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = (hero, call) => shots.Add(new Shot(tick, call, hero.BoxDirection, headFacing));
        var journal = new List<AlundraPlayerManager.WeaponSpawnCall>();
        AlundraPlayerManager.WeaponSpawnCallsForTests = journal;

        arena.Begin();
        var next = 1;
        var frame = 0;
        while (next <= lastTick)
        {
            var size = Math.Min(frame < frameSizes.Count ? frameSizes[frame] : 1, lastTick - next + 1);
            arena.Frame(holdAt(next), size);
            next += size;
            frame++;
        }

        Assert.Equal(journal.Count, shots.Count); // the journal and the hook see the same shots.
        return shots;
    }

    private static IReadOnlyList<int> Grouping(int ticksPerFrame, int frames) => Enumerable.Repeat(ticksPerFrame, frames).ToList();

    /// <summary>The air scenario of the oracle: Cross at tick 1 (the jump), the press at tick 8, then the pad of <paramref name="after"/> from tick 9.</summary>
    private static Func<int, uint> Air(uint press, uint after) => t => t switch
    {
        1 => Cross,
        8 => press,
        _ => t >= 9 ? after : 0u,
    };

    /// <summary>Seven frames of one tick, then the frame of ticks 8 and 9 (the press tick and the shot tick in the same frame).</summary>
    private static readonly IReadOnlyList<int> PressAndShotInOneFrame = new[] { 1, 1, 1, 1, 1, 1, 1, 2 };

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d1) and (d2): in the air.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void D1_InTheAir_SquarePlusLeftAtTickP_ShotsAtPPlus1_ToTheWest_At1TickAnd2TicksPerFrame()
    {
        // 1 tick per frame: P = 8, the shot at tick 9, west (8): the turn of the press tick counts.
        var one = Run("d1-1", Air(Square | Left, Left), Grouping(1, 0), 14);
        var shotOne = Assert.Single(one);
        Assert.Equal(9, shotOne.Tick);
        Assert.Equal(8, shotOne.Call.Direction);

        // 2 ticks per frame, P and P+1 in the same frame: the end-of-frame direction is still the east one (0x18) in the phase A code; the tick's own facing (BoxDirection) is the west one.
        var two = Run("d1-2", Air(Square | Left, Left), PressAndShotInOneFrame, 14);
        var shotTwo = Assert.Single(two);
        Assert.Equal(9, shotTwo.Tick);
        Assert.Equal(2, shotTwo.BoxDirection); // the index D-F3-2 reads (2 = left): already west at the head of the shot tick, whatever the grouping (green from the phase A).
        Assert.Equal(8, shotTwo.Call.Direction);
    }

    [Fact]
    public void D2_InTheAir_SquareAtTickP_LeftOnlyFromTheShotTick_ShotsToTheEast_TheTurnOfTheShotTickIsIgnored()
    {
        var shots = Run("d2", Air(Square, Left), Grouping(1, 0), 14);

        var shot = Assert.Single(shots);
        Assert.Equal(9, shot.Tick);
        Assert.Equal(0x18, shot.Call.Direction); // east: Left asked at tick 9 is not seen by the shot of tick 9.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d3): on the ground, the four directions.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Up, 0x10)]
    [InlineData(Down, 0)]
    [InlineData(Left, 8)]
    [InlineData(Right, 0x18)]
    public void D3_OnTheGround_SquarePlusADirection_ShotsInThatDirection(uint direction, int word)
    {
        var shots = Run("d3-" + direction, t => t == 1 ? Square | direction : 0u, Grouping(1, 0), 16);

        var shot = Assert.Single(shots);
        Assert.Equal(10, shot.Tick);
        Assert.Equal(word, shot.Call.Direction);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (d4): the hypothesis of D-F3-2, green from the phase A.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(Up, 1)]
    [InlineData(Down, 1)]
    [InlineData(Left, 1)]
    [InlineData(Right, 1)]
    [InlineData(Up, 2)]
    [InlineData(Down, 2)]
    [InlineData(Left, 2)]
    [InlineData(Right, 2)]
    [InlineData(Up, 3)]
    [InlineData(Down, 3)]
    [InlineData(Left, 3)]
    [InlineData(Right, 3)]
    public void D4_TheBoxDirection_EqualsTheFacingOfTheHeadOfTheTick_AtTheTickOfTheShot(uint direction, int ticksPerFrame)
    {
        var shots = Run($"d4-{direction}-{ticksPerFrame}", t => t == 1 ? Square | direction : 0u, Grouping(ticksPerFrame, 40), 24);

        var shot = Assert.Single(shots);
        Assert.Equal(shot.HeadTickFacing, shot.BoxDirection);
        Assert.InRange(shot.BoxDirection, 0, 3);
    }
}
