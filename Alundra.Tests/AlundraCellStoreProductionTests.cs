using System;
using System.IO;
using Alundra.Scripts;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Acceptance 3 of docs/plan-e7-mutation-tuiles.md, slice E7.a: drives the REAL production call site -
/// <see cref="AlundraWorldProxy.RunMapEventsPass"/>/<see cref="AlundraEventProgramRunner.Dispatch"/> -
/// through <see cref="HeadlessIntroSimulation"/> (Alundra.Tests\IntroTraceHarnessTests.cs), not a
/// synthetic document, and proves the mutation actually flows end-to-end from opcode dispatch down to
/// <see cref="AlundraCellsCollisionField"/>'s own read side. Twinned with a NEUTRALIZATION run
/// (<c>installCellMutator: false</c>) per the plan's own "rule 2" (production or neutralization proof):
/// the same drive, with <see cref="IEntityWorldContext.CellMutator"/> forced null, must leave the export's
/// original values untouched - proving the main test's change comes from this exact seam, not some other
/// accidental code path.
///
/// E7.b (docs/plan-e7-mutation-tuiles.md, acceptance item 9, picking up an E7.a deferral): these used to
/// self-skip silently when alundra-project/ was absent - they now throw, naming the missing export,
/// instead (same fix as <see cref="AlundraCellStoreTests"/>).
/// </summary>
public class AlundraCellStoreProductionTests
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
            $"AlundraCellStoreProductionTests: no 'alundra-project/Maps' directory found above "
            + $"'{AppContext.BaseDirectory}' - these tests need the real converter export of map 389 and "
            + "cannot self-skip without one (docs/plan-e7-mutation-tuiles.md, slice E7.b, acceptance item 9).");
    }

    [Fact]
    public void MapEntry_Frame1_RealMutator_ClosesDoorHatchAndReplacesStackTail()
    {
        var projectRoot = FindProjectRoot();

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installCellMutator: true);
        sim.RunFramesForTest(1);

        Assert.NotNull(sim.GroundField);
        Assert.NotNull(sim.CellStore);

        // Same map-entry mutation as AlundraCellStoreTests.CopyCellRectangle_HatchTemplateOntoDoor_18_37 -
        // here reached through the REAL 0x85/0x55 dispatch inside RunMapEventsPass's own map-entry pass on
        // frame 1, not a direct AlundraCellStore call.
        Assert.Equal(0, sim.GroundField!.SampleGroundProperty(new Vector3(18 * 24, 38 * 16, 0f)));

        var stack1837 = sim.CellStore!.GetWallTileStack(18, 37);
        Assert.NotNull(stack1837);
        Assert.Equal(new[] { 12434, 12444, 53249, 53259, 53269 }, stack1837!.Value.Tiles);
    }

    [Fact]
    public void MapEntry_Frame1_NeutralizedNullMutator_LeavesExportValuesUntouched()
    {
        var projectRoot = FindProjectRoot();

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        // Same drive as the main test above, but CellMutator forced null (installCellMutator: false) -
        // the SAME production RunMapEventsPass call now takes AlundraEventProgramRunner's degraded
        // "CellMutator null" fallback for 0x54/0x55/0x85 (skip by size, no mutation). Proves the main
        // test's mutation is actually caused by this seam - not some other, accidental code path
        // (docs/plan-e7-mutation-tuiles.md's own "rule 2": production or neutralization proof).
        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!, installCellMutator: false);
        sim.RunFramesForTest(1);

        Assert.NotNull(sim.GroundField);
        Assert.NotNull(sim.CellStore); // the real store IS built - only never handed to the interpreter.

        Assert.Equal(128, sim.GroundField!.SampleGroundProperty(new Vector3(18 * 24, 38 * 16, 0f)));

        var stack1837 = sim.CellStore!.GetWallTileStack(18, 37);
        Assert.NotNull(stack1837);
        Assert.Equal(new[] { 12434, 12444, 53251, 53261, 53271 }, stack1837!.Value.Tiles);

        // E7.b (docs/plan-e7-mutation-tuiles.md, acceptance item 9, picking up an E7.a deferral): the
        // neutralization twin only asserted export VALUES stayed untouched - it never asserted that the
        // trace itself actually took the degraded ("CellMutator null") path rather than, say, some
        // accidental UnknownSkipped path that happened to also leave values untouched. 0x55/0x85 are both
        // dispatched on frame 1 (map entry, one per hatch) - each must trace as EventTraceKind.Degraded at
        // least once here.
        Assert.True(sim.DegradedOpcodeCounts.GetValueOrDefault(0x55) > 0, "expected 0x55 to trace Degraded on frame 1.");
        Assert.True(sim.DegradedOpcodeCounts.GetValueOrDefault(0x85) > 0, "expected 0x85 to trace Degraded on frame 1.");
    }
}
