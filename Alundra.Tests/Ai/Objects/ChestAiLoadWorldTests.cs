#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0d T1 (docs/plan-e20-heros.md section 1.26.2, phase A, test (a1), D-E0D-7): A255 (<c>0x80061BD4</c>) on the CHEST (sprite type 30), in the REAL world of the Debug zone (map 11: the chest of record 30 holds the life vessel, flag 0; the
/// chest of record 37 holds the herbs, flag 2000). At the load the binary writes <c>+0x88 = 1</c> (the animation 1, the chest drawn open) when the contents (<c>+0x3C</c>) are zero, and nothing else: a full chest stays closed (animation 0).
/// The contents are zero when the flag of the chest is already raised (<c>InitializeContents</c>, <c>0x80032A40</c>) or when the record holds none.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChestAiLoadWorldTests : IDisposable
{
    private const int VaseChestRecord = 30;
    private const int HerbsChestRecord = 37;
    private const int HerbsChestFlag = 2000;

    public ChestAiLoadWorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    private static PickupDropRun DebugZone(string name) => new(name, "Debug zone", "Debug zone-11", 54, 30, -1, -1, clearTheWorld: false);

    [Fact]
    public void A1_AChestWithNoContents_IsDrawnOpen_AFullChestIsDrawnClosed()
    {
        using var run = new PickupDropRun("a1", "Debug zone", "Debug zone-11", 54, 30, -1, -1, clearTheWorld: false, flags: new[] { HerbsChestFlag }); // the chest of the herbs was opened in an earlier session: the roll of its creation gives it no contents.
        var empty = run.Arc.EntityByRecord(HerbsChestRecord)!;
        var full = run.Arc.EntityByRecord(VaseChestRecord)!;

        run.Run(6, 1);

        Assert.Equal((30, 0u), (empty.SpriteType, empty.ContentsItemId));
        Assert.Equal((1u, 1u), (empty.TargetAnimationId, empty.CurrentAnimationId)); // the chest open, at the load.
        Assert.Equal((30, 0x53u), (full.SpriteType, full.ContentsItemId));
        Assert.Equal((0u, 0u), (full.TargetAnimationId, full.CurrentAnimationId));   // the chest closed.
    }

    [Fact]
    public void A1_TheHerbsChestOfTheSameMap_IsClosedWhileItsFlagIsDown()
    {
        using var run = DebugZone("a1b");
        var chest = run.Arc.EntityByRecord(HerbsChestRecord)!;

        run.Run(6, 1);

        Assert.Equal((0x24u, 0u, 0u), (chest.ContentsItemId, chest.TargetAnimationId, chest.CurrentAnimationId));
    }

    [Fact]
    public void A1_TheProductionRegistry_HoldsA255ForTheChest()
    {
        using var run = DebugZone("a1c");
        var registry = run.Arc.Runner.NativeSprites;

        Assert.True(registry.TryGet(ScriptHelper.ProgramALoad, PickupAi.Code, 30, out _));
    }
}
