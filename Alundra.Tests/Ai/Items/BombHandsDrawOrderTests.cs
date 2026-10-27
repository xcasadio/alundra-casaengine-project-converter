#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;
using Alundra.UI.Inventory;
using static Alundra.Tests.Support.DrawOrderRig;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8, (w1), (w3), (w4), (w5)): the draw order of the bomb in the hands of the REAL hero and of the bomb he throws, on the bench <see cref="BombHandsRun"/> (the real world of map 14, the real prefabs, the pass of the frame).
/// What is read is what the renderer reads: the sort key built by the <see cref="DepthSortable2DComponent"/> on the rendered translation of the sprite, at the end of each frame. Before E20.g1d the bomb in the hands was BEHIND the hero in 15 rows of pixels out of
/// 16 (the probe P1 of the T0). The signs of the throw are the keys of the real <c>ALUN_CD.EXE</c> (<c>docs/plan-e20-annexe/e20-g1d/keys/out_throw_probe.txt</c>), tick for tick.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombHandsDrawOrderTests : IDisposable
{
    private const uint Circle = BombHandsRun.Circle;
    private const uint Square = BombHandsRun.Square;
    private const uint Cross = BombHandsRun.Cross;

    public BombHandsDrawOrderTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        InventoryDirector.Instance.ResetForTests();
    }

    private static EntityScriptProxy? Bomb(BombHandsRun run) => run.Arc.Entities.FirstOrDefault(e => e.SpriteType == 2);

    public static IEnumerable<object[]> Directions() => new[]
    {
        new object[] { "down", 0u, BombHandsRun.Down },
        new object[] { "left", 8u, BombHandsRun.Left },
        new object[] { "up", 16u, BombHandsRun.Up },
        new object[] { "right", 24u, BombHandsRun.Right },
    };

    // (w1) the bomb in the hands, standing, in the four directions, on the 16 rows of pixels of Y: in front of the hero, always.
    [Theory]
    [MemberData(nameof(Directions))]
    public void W1_TheBombInTheHands_IsInFrontOfTheHero_OnTheSixteenRowsOfY_Standing(string name, uint direction, uint pad)
    {
        _ = pad;
        var seen = 0;
        for (var y = 100; y <= 115; y++)
        {
            using var run = new BombHandsRun("g1d-w1-" + name + y, 100, y, null, direction);
            var n = 0;
            run.Arc.OnFrame = () =>
            {
                n++;
                if (n is < 40 or > 44)
                {
                    return;
                }

                var bomb = Bomb(run);
                Assert.NotNull(bomb);
                Assert.NotNull(bomb!.PlatformEntity);
                var heroKey = KeyOf(run.Arc.HeroEntity);
                var bombKey = KeyOf(ArcRun.EntityOf(bomb)!);
                Assert.True(bombKey.CompareTo(heroKey) > 0, $"{name} Y={y} tick {n}: the bomb is behind the hero (elevations {bombKey.Elevation}/{heroKey.Elevation}, coordinates {bombKey.SortCoordinate}/{heroKey.SortCoordinate})");
                seen++;
            };
            run.Go(46, t => t == 4 ? Circle : 0u);
        }

        Assert.Equal(16 * 5, seen);
    }

    // (w1) walking with the bomb: in front in every frame of the walk, from four rows of pixels in each direction.
    [Theory]
    [MemberData(nameof(Directions))]
    public void W1_TheBombInTheHands_IsInFrontOfTheHero_WhileHeWalks(string name, uint direction, uint pad)
    {
        var seen = 0;
        foreach (var y in new[] { 100, 103, 107, 111 })
        {
            using var run = new BombHandsRun("g1d-w1w-" + name + y, 100, y + 20, null, direction);
            var n = 0;
            run.Arc.OnFrame = () =>
            {
                n++;
                if (n < 41 || n > 70)
                {
                    return;
                }

                var bomb = Bomb(run)!;
                var heroKey = KeyOf(run.Arc.HeroEntity);
                var bombKey = KeyOf(ArcRun.EntityOf(bomb)!);
                Assert.True(bombKey.CompareTo(heroKey) > 0, $"{name} start Y={y + 20} tick {n}: the bomb is behind the walking hero");
                seen++;
            };
            run.Go(70, t => t == 4 ? Circle : t >= 40 ? pad : 0u);
        }

        Assert.Equal(4 * 30, seen);
    }

    // (w3) the jump facing up with the bomb, from the 16 rows of Y: in front at every frame, and the sort coordinate of the bomb EXACTLY the hero's on their rendered translations (the hero's height is fractional in the air).
    [Fact]
    public void W3_TheJumpFacingUpWithTheBomb_KeepsTheBombInFront_AtTheExactCoordinateOfTheHero_FromTheSixteenRowsOfY()
    {
        var airborneFrames = 0;
        var fractionalFrames = 0;
        var compared = 0;
        for (var y = 140; y <= 155; y++)
        {
            using var run = new BombHandsRun("g1d-w3-" + y, 100, y, null, 16);
            var n = 0;
            run.Arc.OnFrame = () =>
            {
                n++;
                if (n < 40)
                {
                    return;
                }

                var hero = run.Arc.Hero;
                if (hero.PosZ > 0)
                {
                    airborneFrames++;
                    if ((hero.PosZ & 0xFFFF) != 0)
                    {
                        fractionalFrames++;
                    }
                }

                var bomb = Bomb(run)!;
                Assert.NotNull(bomb.PlatformEntity);
                var heroKey = KeyOf(run.Arc.HeroEntity);
                var bombKey = KeyOf(ArcRun.EntityOf(bomb)!);
                Assert.True(bombKey.CompareTo(heroKey) > 0, $"Y={y} tick {n} (hero Z {hero.PosZ / 65536.0:F3}, state {hero.TargetAnimationId}): the bomb is behind the hero");
                Assert.Equal(heroKey.SortCoordinate, bombKey.SortCoordinate);
                Assert.True(bombKey.Elevation >= heroKey.Elevation, $"Y={y} tick {n}: the elevation of the bomb {bombKey.Elevation} is below the hero's {heroKey.Elevation}");
                compared++;
            };
            run.Go(100, t => t <= 3 ? BombHandsRun.Up : t == 4 ? Circle : t == 44 ? BombHandsRun.Up | Cross : t >= 38 ? BombHandsRun.Up : 0u);
        }

        Assert.Equal(16 * 61, compared);
        Assert.True(airborneFrames > 16 * 15, $"the hero was in the air in {airborneFrames} frames only");
        Assert.True(fractionalFrames > 16 * 8, $"his height was fractional in {fractionalFrames} frames only");
    }

    private static readonly (string Name, uint Direction, uint Pad)[] Throws =
    {
        ("right", 24u, BombHandsRun.Right),
        ("left", 8u, BombHandsRun.Left),
        ("down", 0u, BombHandsRun.Down),
        ("up", 16u, BombHandsRun.Up),
    };

    public static IEnumerable<object[]> ThrowDirections() => Throws.Select(t => new object[] { t.Name, t.Direction, t.Pad });

    private static Dictionary<int, string> OracleSigns(string scenario)
        => File.ReadAllLines(AnnexFiles.DrawOrderKeys("out_throw_probe.txt"))
            .Where(l => l.StartsWith(scenario + " | ", StringComparison.Ordinal))
            .Select(l => l.Split(" | "))
            .ToDictionary(p => int.Parse(p[1]), p => p[8].Trim());

    // (w4) the throw in the four directions: the sign of every tick is the binary's (in front while the key says so, behind from the 4th tick of a throw upwards), the coordinate of the bomb is the hero's plus 100 a pixel of difference of the
    // keys all along the flight, and at the landing the anchor and the offset are back to (0, 0) and 0.
    [Theory]
    [MemberData(nameof(ThrowDirections))]
    public void W4_TheBombThrown_IsOrderedAsTheKeysOfTheBinary_ItsCoordinateIsTheHeroesPlusDeltaPixels_AndTheMarkFallsAtTheLanding(string name, uint direction, uint pad)
    {
        var oracle = OracleSigns("throw_" + name);
        Assert.NotEmpty(oracle);
        using var run = new BombHandsRun("g1d-w4-" + name, 200, 200, null, direction);
        var compared = 0;
        var flight = 0;
        var landed = 0;
        var firstLanding = 0;
        var n = 0;
        run.Arc.OnFrame = () =>
        {
            n++;
            if (n < 50 || n > 90)
            {
                return;
            }

            var bomb = Bomb(run)!;
            var bombEntity = ArcRun.EntityOf(bomb)!;
            var depth = bombEntity.GetComponent<DepthSortable2DComponent>()!;
            var heroKey = KeyOf(run.Arc.HeroEntity);
            var bombKey = KeyOf(bombEntity);
            Assert.Null(bomb.PlatformEntity);

            if (bomb.IsOnGround == 0)
            {
                flight++;
                Assert.Same(run.Arc.Hero, bomb.ThrownBy);
                var delta = ((int)EntityDepthKey.Compute(bomb) >> 16) - ((int)EntityDepthKey.Compute(run.Arc.Hero) >> 16);
                Assert.Equal(heroKey.SortCoordinate + 100 * delta, bombKey.SortCoordinate);
                Assert.Equal(heroKey.SortCoordinate + 100 * delta, bomb.DrawnSortCoordinate);
            }
            else
            {
                landed++;
                firstLanding = firstLanding == 0 ? n : firstLanding;
                Assert.Null(bomb.ThrownBy);
                Assert.Equal(Vector2.Zero, depth.SortAnchorLocal);
                Assert.Equal(0, depth.LocalSortOffset);
                Assert.Null(bomb.DrawnSortCoordinate);
            }

            if (oracle.TryGetValue(n, out var sign) && sign != "EQUAL")
            {
                var cmp = bombKey.CompareTo(heroKey);
                Assert.True(sign == "FRONT" ? cmp > 0 : cmp < 0, $"{name} tick {n}: the binary has the bomb {sign}, the DLL has it {(cmp > 0 ? "FRONT" : cmp < 0 ? "BEHIND" : "EQUAL")}");
                compared++;
            }
        };
        run.Go(90, t => t <= 3 ? pad : t == 4 ? Circle : t == 50 ? Square : 0u);

        Assert.True(flight >= 20, $"{flight} frames of flight");
        Assert.Equal(72, firstLanding);
        Assert.True(landed >= 15, $"{landed} frames on the ground");
        Assert.True(compared >= 5, $"{compared} signs compared");
    }

    // (w5) a bomb that leaves the hands without being thrown (it explodes there at the end of its fuse): at the next frame the anchor and the offset are back to (0, 0) and 0.
    [Fact]
    public void W5_TheBombThatExplodesInTheHands_FindsAZeroAnchorAndAZeroOffsetAtTheNextFrame()
    {
        using var run = new BombHandsRun("g1d-w5", 100, 120, null, 24);
        EntityScriptProxy? bomb = null;
        Entity? bombEntity = null;
        DepthSortable2DComponent? depth = null;
        var heldFrames = 0;
        var freeFrames = 0;
        var n = 0;
        run.Arc.OnFrame = () =>
        {
            n++;
            if (n < 40 || n > 260)
            {
                return;
            }

            bomb ??= Bomb(run);
            if (bomb == null)
            {
                return;
            }

            bombEntity ??= ArcRun.EntityOf(bomb);
            depth ??= bombEntity?.GetComponent<DepthSortable2DComponent>();
            if (depth == null)
            {
                return;
            }

            if (bomb.PlatformEntity != null)
            {
                heldFrames++;
                Assert.Equal(EntityDepthKey.HeldDrawOffset, depth.LocalSortOffset);
                Assert.NotEqual(Vector2.Zero, depth.SortAnchorLocal);
            }
            else
            {
                freeFrames++;
                Assert.Equal(0, depth.LocalSortOffset);
                Assert.Equal(Vector2.Zero, depth.SortAnchorLocal);
                Assert.Null(bomb.ThrownBy);
            }
        };
        run.Go(262, t => t == 4 ? Circle : 0u);

        Assert.True(heldFrames >= 190, $"{heldFrames} frames in the hands");
        Assert.True(freeFrames >= 10, $"{freeFrames} frames after the explosion in the hands");
    }
}
