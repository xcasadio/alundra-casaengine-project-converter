#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Transitions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E19.d2c2 D5b T1 (docs/plan-e19-opcodes.md §1.2h.3.2b, D-E19-117, D-E19-124): the arcs of the lift of the map 22 (Lars' Crypt, record 2: "Plateforme flottante (grande)" 48 x 32 x 8 at 24 px,
/// top 32 px, flush with the floor of the room, in a hollow 16 px deep), on the real exported map, the real controller of the hero and the real script of the lift (<c>C[3]</c>: it waits for
/// <c>T1</c> off, <c>T3</c> on and the hero on it, then <c>ForceZ = +65536</c> for 64 ticks and the warning <c>G129</c>). Written BEFORE the code: in the binary the hero goes up with the
/// platform in the very tick of its carrier (foot minus top 0 at every image, <c>docs/plan-e19-d5b-annexe/exec-2026-10-22/report.md</c> sections 5 and 6); the DLL lets the platform pass
/// through the hero, who ends at the bottom of the hollow. The values of the recipe are the measured ones of the discovery (<c>final_baseline_probe_out.txt</c>, probes P7, P9, P14).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftMap22ArcTests : IDisposable
{
    private readonly string _previousProjectPath = CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath;

    public LiftMap22ArcTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        CasaEngine.Engine.Environment.EngineEnvironment.ProjectPath = _previousProjectPath;
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static ArcSpec Spec(string name, int frames, int arrivalX, int arrivalY, int arrivalLevel, uint direction) =>
        new(name, "Lars' Crypt", "Lars' Crypt-22", Array.Empty<int>(), 0, 0, 0, frames, RealController: true, Prefabs: true,
            Arrival: new ArcArrival(arrivalX << 16, arrivalY << 16, arrivalLevel << 20, GameState.ResetAnimationId, direction));

    private static int Top(EntityScriptProxy p) => p.PosZ + p.ModZ + p.Depth + 1;

    private static int Rel(EntityScriptProxy rider, EntityScriptProxy carrier) => rider.PosZ + rider.ModZ - Top(carrier);

    /// <summary>A temporary flag of the script (T1, T3, T5...): word 0x8000 | n, bit n.</summary>
    private static bool Temp(uint n) => (GameState.Instance.GetFlag(0x8000 | n) & (1u << (int)(n & 31))) != 0;

    [Fact]
    public void A_D5b_22_FromTheLiftItself_TheHeroOnThePlatformRisesWithIt_FootMinusTopIsZeroFromTheFirstImageOfTheRise_ToTheWarning()
    {
        // P7: the arrival is on the platform at the level 2 (32 px); the lever 2 has been hit (T3); the lift starts at the image 12 and goes up 64 px by 1 px per image; G129 and the warning at the image 76.
        using var arc = new ArcRun(Spec("D5b-22-lift", 900, 1008, 176, 2, GameState.ResetAnimationId));
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var hero = arc.Hero;
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(24 << 16, platform.PosZ);
        Assert.Same(platform.LogicContextEntity, hero.RidingEntity);
        GameState.Instance.AddFlag(0x8003, 1u << 3); // T3 on: the lever 2 has been hit.
        var rising = 0;
        for (var f = 1; f <= 200; f++)
        {
            arc.OneFrame();
            if (WarpDirector.Instance.IsTransitionInProgress)
            {
                break;
            }

            if (platform.PosZ > 24 << 16)
            {
                rising++;
                Assert.True(Rel(hero, platform) == 0, $"f{f} (rise {rising}): foot minus top {Rel(hero, platform)}");
                Assert.True(ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity), $"f{f}: riding");
                Assert.True(hero.IsOnGround == 1, $"f{f}: onGround {hero.IsOnGround}");
            }
        }

        Assert.True(rising >= 60, $"the lift rose during {rising} images");
        Assert.Equal(1, hero.IsOnGround);
    }

    [Fact]
    public void A_D5b_22_TheRecipeOfTheAuthor_HitTheLeverJumpIntoTheHollowJumpOntoTheLiftAndWait_TheHeroRisesWithThePlatform()
    {
        // The pad script of the report (section 6): arrival south of the right lever (tile 46, 8, level 2, facing north); 30 images at rest, Square (T3, T4 on); Down until y >= 186; Left until x <= 1094;
        // Cross + Left lands in the hollow (x 1042, z 16); Cross + Left again lands on the platform; the platform leaves 12 images later (T5), 64 px of rise, then G129 and the warning.
        using var arc = new ArcRun(Spec("D5b-22-recipe", 1500, 1116, 136, 2, 16));
        var hero = arc.Hero;
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        for (var i = 0; i < 30; i++)
        {
            arc.OneFrame();
        }

        hero.TargetDirection = 16;
        arc.Press(PadState.Square);
        for (var i = 0; i < 30; i++)
        {
            arc.OneFrame();
        }

        Assert.True(Temp(3), "the lever 2 was not hit (T3)");
        arc.HoldDirections(PadState.Down);
        var guard = 0;
        while (hero.PosY < 186 * 65536 && guard++ < 120)
        {
            arc.OneFrame();
        }

        arc.HoldDirections(PadState.Left);
        guard = 0;
        while (hero.PosX > 1094 * 65536 && guard++ < 120)
        {
            arc.OneFrame();
        }

        arc.Press(PadState.Cross | PadState.Left);
        for (var i = 0; i < 40; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(16 << 16, hero.PosZ); // in the hollow, 16 px.
        arc.HoldDirections(PadState.Left);
        arc.Press(PadState.Cross | PadState.Left);
        var landed = false;
        for (var i = 0; i < 40; i++)
        {
            arc.OneFrame();
            landed |= ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity);
        }

        Assert.True(landed, "the hero did not land on the lift");
        arc.ReleaseDirections();
        var rising = 0;
        for (var f = 0; f < 200 && !WarpDirector.Instance.IsTransitionInProgress; f++)
        {
            arc.OneFrame();
            if (platform.PosZ > 24 << 16 && !WarpDirector.Instance.IsTransitionInProgress)
            {
                rising++;
                Assert.True(Rel(hero, platform) == 0, $"wait {f} (rise {rising}): foot minus top {Rel(hero, platform)}");
                Assert.True(ReferenceEquals(platform.LogicContextEntity, hero.RidingEntity), $"wait {f}: riding");
            }
        }

        Assert.True(rising >= 50, $"the lift rose during {rising} images");
        Assert.Equal(1, hero.IsOnGround);
    }

    [Fact]
    public void A_D5b_22_ThePileTheHeroOnTheBarrelOnTheLiftMovingEastAndUp_EveryoneKeepsItsPlaceForFourteenImages()
    {
        // P14: the barrel (record 33, a gravity crate 21 x 15 x 16) is put on the lift and the hero on the barrel; the test drives the lift 1 px per image east and 1 px per image up (the script is idle,
        // T3 off). In the binary every passenger goes with its carrier in the very tick, in X as in Z; the pile corrected for the double force of the crate (D-D5B-7) keeps them exactly in place.
        using var arc = new ArcRun(Spec("D5b-22-pile", 900, 1020, 176, 3, GameState.ResetAnimationId));
        arc.OneFrame();
        var platform = arc.EntityByRecord(2)!;
        var barrel = arc.EntityByRecord(33)!;
        var hero = arc.Hero;
        barrel.PosX = 1020 << 16;
        barrel.PosY = 176 << 16;
        barrel.PosZ = (32 << 16) - barrel.ModZ;
        barrel.PushLogicalPositionToRoot();
        hero.PosX = 1020 << 16;
        hero.PosY = 176 << 16;
        hero.PosZ = (48 << 16) - hero.ModZ;
        hero.PushLogicalPositionToRoot();
        for (var i = 0; i < 20; i++)
        {
            arc.OneFrame();
        }

        Assert.Same(platform.LogicContextEntity, barrel.RidingEntity);
        Assert.Same(barrel.LogicContextEntity, hero.RidingEntity);
        platform.TargetDirection = platform.CurrentDirection;
        platform.TargetForceX = 65536;
        platform.ForceX = 65536;
        platform.ForceStepX = 0;
        platform.ForceZ = 65536;
        var platformX = platform.PosX;
        var barrelX = barrel.PosX;
        var heroX = hero.PosX;
        var platformZ = platform.PosZ;
        for (var f = 1; f <= 14; f++)
        {
            arc.OneFrame();
            var label = $"f{f}";
            Assert.True(platform.PosX - platformX == f * 65536 && platform.PosZ - platformZ == f * 65536, $"{label}: platform dx {(platform.PosX - platformX) / 65536.0} dz {(platform.PosZ - platformZ) / 65536.0}");
            Assert.True(barrel.PosX - barrelX == f * 65536, $"{label}: barrel dx {(barrel.PosX - barrelX) / 65536.0}");
            Assert.True(hero.PosX - heroX == f * 65536, $"{label}: hero dx {(hero.PosX - heroX) / 65536.0}");
            Assert.True(Rel(barrel, platform) == 0, $"{label}: barrel foot minus top {Rel(barrel, platform)}");
            Assert.True(Rel(hero, barrel) == 0, $"{label}: hero foot minus barrel top {Rel(hero, barrel)}");
            Assert.True(ReferenceEquals(platform.LogicContextEntity, barrel.RidingEntity), $"{label}: barrel riding");
            Assert.True(ReferenceEquals(barrel.LogicContextEntity, hero.RidingEntity), $"{label}: hero riding");
        }
    }
}
