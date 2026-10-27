#nullable enable
using System;
using Xunit;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;
using static Alundra.Tests.Support.DrawOrderRig;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8, (w2)): a crate in the hands of the REAL hero (a collidable, the path of W6 of E20.g1a) is drawn in front of him on the 16 rows of pixels of Y, on the bench <see cref="BreakRun"/> (the real world of the Coal Mine, the
/// crate born on his head and put in his hands). What is read is the sort key the renderer builds, at the end of each frame, on the rendered translations.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class CrateHandsDrawOrderTests : IDisposable
{
    private const string Mine = "Coal Mine";
    private const string MineMap = "Coal Mine-67";
    private const int CrateIndex = 36;

    public CrateHandsDrawOrderTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        NativeEffects.CreatedForTests = null;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void W2_TheCrateInTheHands_IsInFrontOfTheHero_OnTheSixteenRowsOfY_AtTheSameSortCoordinate()
    {
        var seen = 0;
        for (var y = 100; y <= 115; y++)
        {
            using var run = new BreakRun("g1d-w2-" + y, Mine, MineMap, CrateIndex, heroX: 109, heroY: y, contents: 0);
            Assert.NotNull(run.Arc.Hero.CarriedEntity);
            Assert.True((run.Crate.Flags & Alundra.Entities.EntityFlags.Collidable) != 0, "the crate is a collidable");
            var n = 0;
            run.Arc.OnFrame = () =>
            {
                n++;
                var heroKey = KeyOf(run.Arc.HeroEntity);
                var crateKey = KeyOf(ArcRun.EntityOf(run.Crate)!);
                Assert.NotNull(run.Crate.PlatformEntity);
                Assert.True(crateKey.CompareTo(heroKey) > 0, $"Y={y} frame {n}: the crate is behind the hero (elevations {crateKey.Elevation}/{heroKey.Elevation}, coordinates {crateKey.SortCoordinate}/{heroKey.SortCoordinate})");
                Assert.Equal(heroKey.SortCoordinate, crateKey.SortCoordinate);
                seen++;
            };
            run.Go(1, 6, _ => 0u);
        }

        Assert.Equal(16 * 6, seen);
    }
}
