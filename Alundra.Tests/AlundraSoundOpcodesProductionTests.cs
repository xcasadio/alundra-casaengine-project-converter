using System;
using System.IO;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// T1/T2 of docs/plan-e11-audio.md, slice E11.a: drives the REAL production call site -
/// <see cref="AlundraWorldProxy.RunMapEventsPass"/>/<see cref="AlundraEventProgramRunner.Dispatch"/> -
/// through <see cref="HeadlessIntroSimulation"/> (Alundra.Tests\IntroTraceHarnessTests.cs), not a
/// synthetic document, exactly like <see cref="AlundraCellStoreProductionTests"/> does for E7.a. The
/// exact counts are §1.1's own recount, not <c>&gt; 0</c> assertions (§2.1: the goldens are BLIND to
/// this work - a wrong or missing dispatch produces the identical trace text, so this test is the only
/// oracle for "the request happened at all").
///
/// Twinned with a NEUTRALIZATION run (<c>installSoundPlayer: false</c>) per the repo's own "rule 2"
/// (production or neutralization proof): the same drive, with <see cref="IEntityWorldContext.SoundPlayer"/>
/// forced null, must produce ZERO requests and trace 0xBD as Degraded - proving the main test's requests
/// come from this exact seam, not some other accidental code path.
/// </summary>
public class AlundraSoundOpcodesProductionTests
{
    private const string WorldName = "Ship Klark (beginning)-389";

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraSoundOpcodesProductionTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export of map 389 and "
            + "cannot self-skip without one (docs/plan-e11-audio.md, slice E11.a).");
    }

    [Fact]
    public void Intro_RealSoundPlayer_Requests23SoundsAtTheExactFramesAndCounts()
    {
        var projectRoot = FindProjectRoot();

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        // id 300/301 keep dispatching up to frame 1589 (§1.1) - past id 61's own single frame-1087
        // dispatch, so the drive must run at least that far to see all 23.
        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installSoundPlayer: true);
        sim.RunFramesForTest(1589);

        // §1.1's own recount: 23 dispatches total - 15x id 300, 6x id 301, 1x id 302 (frame 1), 1x id 61
        // (frame 1087).
        Assert.Equal(23, sim.SoundRequests.Count);

        var id300Count = 0;
        var id301Count = 0;
        var id302Count = 0;
        var id61Count = 0;

        foreach (var (frame, sfxId) in sim.SoundRequests)
        {
            switch (sfxId)
            {
                case 300:
                    id300Count++;
                    break;
                case 301:
                    id301Count++;
                    break;
                case 302:
                    id302Count++;
                    Assert.Equal(1, frame);
                    break;
                case 61:
                    id61Count++;
                    Assert.Equal(1087, frame);
                    break;
                default:
                    Assert.Fail($"Unexpected sfxId {sfxId} requested at frame {frame} - only 300/301/302/61 are dispatched in the intro (§1.1).");
                    break;
            }
        }

        Assert.Equal(15, id300Count);
        Assert.Equal(6, id301Count);
        Assert.Equal(1, id302Count);
        Assert.Equal(1, id61Count);
    }

    [Fact]
    public void Intro_NeutralizedNullSoundPlayer_RequestsNothingAndDegrades0xBD()
    {
        var projectRoot = FindProjectRoot();

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        // Same drive as the main test above, but SoundPlayer forced null (installSoundPlayer: false) -
        // the SAME production RunMapEventsPass call now takes AlundraEventProgramRunner's degraded
        // "SoundPlayer null" fallback for 0xBD (skip by size, no request). Proves the main test's
        // requests are actually caused by this seam, not some other accidental code path.
        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installSoundPlayer: false);
        sim.RunFramesForTest(1);

        Assert.Empty(sim.SoundRequests);
        Assert.True(sim.DegradedOpcodeCounts.GetValueOrDefault(0xBD) > 0, "expected 0xBD to trace Degraded on frame 1.");
    }
}
