using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E2 acceptance check: the hero bank's converted animation assets (<c>Entities/Alundra/</c>,
/// <c>bankalundra_0_anim{N}_{direction}.anim2d</c> - converter naming, see
/// <see cref="AlundraWorldProxy.TrySelectAnimationByNameSuffix"/>'s own doc) must cover every (anim,
/// direction) pair <see cref="AlundraPlayerManager"/>'s ported Idle(0)/Moving(1)/LoadingMap(54) cases can
/// ever select, for all 4 directions (<see cref="AnimationTables.DirectionNames"/>) - otherwise
/// <c>SyncAnimation</c> would silently fail to find an animation and the pawn would freeze visually while
/// still moving logically. Self-skips when <c>alundra-project/</c> is not present in this checkout (same
/// pattern as <see cref="IntroTraceHarnessTests"/>).
/// </summary>
public class HeroAnimationExportTests
{
    private static string? FindHeroAnimationDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "Entities", "Alundra");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Theory]
    [InlineData(0)] // Idle
    [InlineData(1)] // Moving
    [InlineData(54)] // LoadingMap
    public void HeroBank_EveryPortedAnimationId_HasAllFourDirectionsExported(int animationId)
    {
        var heroDirectory = FindHeroAnimationDirectory();
        if (heroDirectory == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        foreach (var directionName in AnimationTables.DirectionNames)
        {
            var fileName = $"bankalundra_0_anim{animationId}_{directionName}.anim2d";
            Assert.True(
                File.Exists(Path.Combine(heroDirectory, fileName)),
                $"missing exported animation '{fileName}' for the hero bank.");
        }
    }

    /// <summary>
    /// Direction-value -&gt; suffix sanity check (deviation the E2 CONTEXT specifically calls out): a
    /// freshly spawned pawn (AnimationDirection defaults to 0/"down") turning to face TargetDirection
    /// 0x10 (up, per <see cref="AnimationTables.CardinalDirectionTable"/>) must resolve to the "up" file
    /// suffix, and TargetDirection 0x18 (east/right, the direction <see cref="AlundraPlayerManager.MovePlayer"/>'s
    /// own East scenario uses) must resolve to "right" - both via the SAME
    /// <see cref="AlundraWorldProxy.TryResolveAnimationTarget"/>/<see cref="AlundraWorldProxy.TrySelectAnimationByNameSuffix"/>
    /// chain <c>SyncAnimation</c> drives every frame, and both files must actually exist in the real
    /// export.
    /// </summary>
    [Theory]
    [InlineData(0x10u, "up")]
    [InlineData(0x18u, "right")]
    [InlineData(0x0u, "down")]
    [InlineData(0x8u, "left")]
    public void TargetDirection_ResolvesToExpectedSuffix_AndFileExists(uint targetDirection, string expectedSuffix)
    {
        var heroDirectory = FindHeroAnimationDirectory();
        if (heroDirectory == null)
        {
            return; // self-skip
        }

        var proxy = new AlundraEntityScriptProxy
        {
            TargetAnimationId = 1,
            TargetDirection = targetDirection,
            CurrentAnimationId = ~1u, // force the sync to fire
        };

        var fired = AlundraWorldProxy.TryResolveAnimationTarget(proxy, out _, out var animationDirection);

        Assert.True(fired);
        Assert.Equal(expectedSuffix, AnimationTables.DirectionNames[animationDirection]);

        var fileName = $"bankalundra_0_anim1_{expectedSuffix}.anim2d";
        Assert.True(
            File.Exists(Path.Combine(heroDirectory, fileName)),
            $"missing exported animation '{fileName}' for the hero bank.");
    }
}
