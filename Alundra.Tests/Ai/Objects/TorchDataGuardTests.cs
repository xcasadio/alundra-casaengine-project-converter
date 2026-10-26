#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T1, phase A (docs/plan-e20-heros.md section 1.24.5, test (a5), D-G2C2-8): the guards of the data. The sprite types whose header holds C70 or E20 are exactly {393} (the torch, the map-page sprite 137), those that hold D34 exactly {238, 239} (the brambles, the sprites 238 and 239 of the global page),
/// those that hold A19 or C71 exactly {391} (the drop of water, the map-page sprite 135: E20.g2c3's). The export is read as the DLL reads it (<see cref="SpriteRecordCatalog"/>, never skipped: a missing file fails).
/// </summary>
public sealed class TorchDataGuardTests
{
    /// <summary>The types (0 to 255 of the global page, 256 to 511 of the map page) whose header holds <paramref name="code"/> in the program of <paramref name="slot"/>.</summary>
    private static IReadOnlyList<int> HeldBy(int slot, int code)
    {
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var held = new List<int>();
        foreach (var isMapPage in new[] { false, true })
        {
            for (var index = 0; index < 256; index++)
            {
                if (!catalog.TryGetPrefab(isMapPage, index, out var prefab) || !catalog.TryGet(prefab, out var header))
                {
                    continue;
                }

                var program = slot switch
                {
                    ScriptHelper.ProgramALoad => header.ProgramLoad,
                    ScriptHelper.ProgramCTick => header.ProgramTick,
                    ScriptHelper.ProgramDTouch => header.ProgramTouch,
                    _ => header.ProgramDeactivate,
                };
                if (program == code)
                {
                    held.Add((isMapPage ? 0x100 : 0) + index);
                }
            }
        }

        return held.OrderBy(t => t).ToArray();
    }

    [Fact]
    public void A5_C70AndE20_AreHeldByTheTorchAlone_TheType393()
    {
        Assert.Equal(new[] { 393 }, HeldBy(ScriptHelper.ProgramCTick, 70));
        Assert.Equal(new[] { 393 }, HeldBy(ScriptHelper.ProgramEDeactivate, 20));
    }

    [Fact]
    public void A5_D34_IsHeldByTheTwoBrambleTypesAlone_238And239()
    {
        Assert.Equal(new[] { 238, 239 }, HeldBy(ScriptHelper.ProgramDTouch, 34));
    }

    [Fact]
    public void A5_A19AndC71_AreHeldByTheDropOfWaterAlone_TheType391_NotPortedHere()
    {
        Assert.Equal(new[] { 391 }, HeldBy(ScriptHelper.ProgramALoad, 19));
        Assert.Equal(new[] { 391 }, HeldBy(ScriptHelper.ProgramCTick, 71));
    }
}
