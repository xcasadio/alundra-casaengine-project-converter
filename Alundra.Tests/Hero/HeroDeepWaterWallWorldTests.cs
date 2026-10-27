#nullable enable
using System;
using Xunit;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.i2a T1 (docs/plan-e20-heros.md section 1.14.2): the wall of the deep water on the REAL world (the arcs' real map 14, the real prefabs, the world's own <c>Update</c>), where <see cref="HeroDeepWaterWallTests"/> uses the
/// montage: (r4) the adoption of the hero after a portal gives the bit of the wall, (r7) an entity that is not the hero is not blocked by it.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroDeepWaterWallWorldTests : IDisposable
{
    /// <summary>The pseudo-bit of the walkability mask (T0 (d)).</summary>
    private const uint Bit = 0x10000;

    public HeroDeepWaterWallWorldTests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    [Fact]
    public void R4_TheAdoptionOfTheHero_GivesTheBitWithoutTheBoots_AndNotWithThem()
    {
        Assert.NotEqual(0u, AdoptedMask(boots: false) & Bit);
        Assert.Equal(0u, AdoptedMask(boots: true) & Bit);
    }

    /// <summary>
    /// The mask of the controller of the hero after the adoption of the pawn (the arrival of a portal builds a new world and adopts the hero again). The owned boots are put in the state right before the world is initialised, as
    /// a hero that carries them through a portal has them. The montage re-syncs the controller from the flags after the adoption (the header of the hero is applied late in this process): the resync keeps the bit
    /// the adoption gave (r3), so the bit read here is the adoption's.
    /// </summary>
    private static uint AdoptedMask(bool boots)
    {
        var spec = new ArcSpec("E20i2a-adopt", "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        using var arc = new ArcRun(spec, beforeInitialize: _ =>
        {
            if (boots)
            {
                GameState.Instance.NumberOfItems[0x1B * 2 + 1] = 1;
            }
        });
        Assert.Equal(boots ? 2 : 0, PlayerManager.BootsLevelOf(GameState.Instance));
        return arc.Hero.Controller!.Settings.WalkabilityMask;
    }

    [Fact]
    public void R7_AnEntityThatIsNotTheHero_OnADeepWaterCellWithoutTheBoots_IsNotBlocked()
    {
        using var arena = new HitArena("i2a-witness", 40 * 24 + 12, 48 * 16 + 8, targetPixels: null, heroDirection: 0, heroPixelZ: 16, naturalTargetCount: 1);
        var target = arena.Targets[0];
        Assert.NotNull(target.Controller);
        Assert.Equal(0, PlayerManager.BootsLevelOf(GameState.Instance));

        HitArena.Place(target, 42 * 24 + 12, 52 * 16 + 8); // the middle of the pond of the map 14: deep water (0x18)
        target.ResyncControllerFromFlags();

        Assert.Equal(0, ScriptedMotion.BlockedCorners(target, 0, 0));
        Assert.Equal(0u, target.Controller!.Settings.WalkabilityMask & Bit);
    }
}
