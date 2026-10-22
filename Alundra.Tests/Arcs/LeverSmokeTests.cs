#nullable enable
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.c1b-2 L0 (docs/plan-e20-heros.md section 1.7): the smoke of the five lever rooms. Each room is built through <see cref="ArcRun"/> on the real
/// prefabs and the real controller, from the arrival that puts the hero inside the spawn zone of the lever records (the zone gate is read once, at the
/// load, section 1.7 "Faits porteurs"), and run six frames. The records that must be there are there, the records whose bit 0x40 is clear are not, and the
/// log holds no error. Every expected value was written by hand before the code, from the plan and from check.md.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LeverSmokeTests
{
    private readonly ITestOutputHelper _output;

    public LeverSmokeTests(ITestOutputHelper output) => _output = output;

    private (long Constructed, long SixFrames) Smoke(ArcSpec spec, int[] present, int[] absent, params (int Record, int X, int Y)[] positions)
    {
        var clock = Stopwatch.StartNew();
        using var arc = new ArcRun(spec);
        var constructed = clock.ElapsedMilliseconds;
        for (var i = 0; i < 6; i++)
        {
            arc.OneFrame();
        }

        var total = clock.ElapsedMilliseconds;
        ArcChecks.AssertNoUnexpectedError(arc);
        ArcChecks.AssertNothingSkippedOrExceeded(arc);
        foreach (var record in present)
        {
            Assert.NotNull(arc.EntityByRecord(record));
        }

        foreach (var (record, x, y) in positions)
        {
            // The pixel of a record: (XPos * 12 + 12, YPos * 8 + 8) (section 1.7, "Faits porteurs").
            var entity = arc.EntityByRecord(record)!;
            Assert.Equal((x, y), (entity.PosX >> 16, entity.PosY >> 16));
        }

        foreach (var record in absent)
        {
            Assert.Null(arc.EntityByRecord(record));
        }

        _output.WriteLine($"{spec.Name}: constructed in {constructed} ms, six frames at {total} ms");
        return (constructed, total);
    }

    [Fact]
    public void Map115_TheNorthWestLeverIsThere_AndTheTwoRecordsThatNeverAppearAreNot()
    {
        // 115: record 2 (the lever, zone x 0..9 by y 5..16) present; records 0 and 1 (sprite 42, bit 0x40 clear) absent.
        Smoke(LeverArcs.Spec("E20c1b2-smoke115", LeverArcs.Zone, LeverArcs.World115, Array.Empty<int>(), 84, 120, 1, 8, 1500), new[] { 2 }, new[] { 0, 1 }, (2, 60, 120));
    }

    [Fact]
    public void Map116_TheTwoLeversAreThere_AndThePlatesAreNot()
    {
        // 116: records 39 and 40 (zone x 0..7 by y 0..27) present; records 7 and 8 (the plates, bit 0x40 clear) absent. Arrival: tile (3,20), pixel (84,328).
        Smoke(LeverArcs.Spec("E20c1b2-smoke116", LeverArcs.Zone, LeverArcs.World116, Array.Empty<int>(), 84, 328, 0, 8, 1500), new[] { 39, 40 }, new[] { 7, 8 }, (39, 864, 424), (40, 1104, 424));
    }

    [Fact]
    public void Map117_TheBasculeRoomHasTheBascule_AndNotTheLever22()
    {
        // 117, bascule room: record 0 (zone x 17..34 by y 4..16) present, record 22 (zone x 0..16 by y 33..59) absent.
        Smoke(LeverArcs.Spec("E20c1b2-smoke117b", LeverArcs.Zone, LeverArcs.World117, Array.Empty<int>(), 500, 192, 1, 24, 1500), new[] { 0 }, new[] { 22 }, (0, 528, 192));
    }

    [Fact]
    public void Map117_TheLever22RoomHasTheLeverAndThePillar_AndNotTheBascule()
    {
        // 117, lever 22 room: record 22 present, the pillar (record 2) present, record 0 (the bascule) absent.
        Smoke(LeverArcs.Spec("E20c1b2-smoke117l", LeverArcs.Zone, LeverArcs.World117, Array.Empty<int>(), 72, 640, 2, 24, 1500), new[] { 22, 2 }, new[] { 0 }, (22, 96, 640));
    }

    [Fact]
    public void Map44_TheSixLeversAreThere_WithG1633()
    {
        // 44: records 7 to 12 (the levers), 0 to 6 (the doors); zone = the whole map.
        Smoke(LeverArcs.Spec("E20c1b2-smoke44", LeverArcs.Zone44, LeverArcs.World44, new[] { 1633 }, 120, 440, 8, 24, 3000),
            new[] { 7, 8, 9, 10, 11, 12, 0, 1, 2, 3, 4, 5, 6 }, Array.Empty<int>(),
            (7, 144, 440), (8, 432, 472), (9, 384, 936), (10, 672, 376), (11, 1080, 904), (12, 936, 408));
    }
}
