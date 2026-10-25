#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0d T2 (docs/plan-e20-heros.md section 1.26.2, phase B): (b8, D-E0D-4) what a <c>Block</c> does to the SCREEN. The binary takes the blocked entities out of the list of the active ones (<c>0x8003B3D8</c>): their programs, their physics and
/// their animations stop; the list of the visible ones does not test the flag, so they stay drawn, on the image they had. T0 measured that the DLL blocked the logic only: the sprites kept animating (<c>probe-dll/probeC_block_draw_animate.txt</c>).
/// Here, in the arena of the map 14 (23 animated entities), a block by an entity of the world freezes the playback of every blocked sprite - still drawn - and an unblock gives it back. (b7) The witnesses: a chest whose record has a script at F is left to
/// the script (the registry answers only for a native F), and a native F255 runs for a chest that has none.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChestAiBlockWorldTests : IDisposable
{
    public ChestAiBlockWorldTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        DialogueDirector.Instance.ResetForTests();
    }

    [Fact]
    public void B8_ABlockedEntity_StaysDrawn_ButItsAnimationStops_AndAnUnblockGivesItBack()
    {
        using var run = PickupDropRun.OnMap14("b8", clearTheWorld: false);
        var animated = run.Arc.Entities.Where(e => !ReferenceEquals(e, run.Hero) && e.AnimatedSprite != null && e.Status.IsActive()).ToList();
        Assert.True(animated.Count >= 8, $"the arena holds animated entities ({animated.Count})");
        var blocker = run.DropAt(0x24, run.Hero.PosX >> 16, (run.Hero.PosY >> 16) + 40);
        PickupAi.WriteWord274(blocker, 2); // the handlers return at once for the kind of the chest: the blocker is inert.
        blocker.Flags &= ~(EntityFlags.Collidable | EntityFlags.Gravity);
        run.Run(10, 1);
        var watched = animated.Where(e => e.Status.IsActive()).Take(4).ToList();
        Assert.Equal(4, watched.Count);
        Assert.True(watched.All(e => e.AnimatedSprite!.CurrentAnimationTimeSeconds > 0f), "the sprites were playing");

        run.Context.Block(blocker);
        run.Run(3, 1); // the freeze is applied at the end of the frame: the engine's own sprite update stops from the frame after.
        var frozen = watched.Select(e => e.AnimatedSprite!.CurrentAnimationTimeSeconds).ToArray();
        run.Run(30, 1);

        Assert.All(watched, e => Assert.NotNull(e.BlockedByEntity));
        Assert.Equal(frozen, watched.Select(e => e.AnimatedSprite!.CurrentAnimationTimeSeconds).ToArray()); // the image does not move,
        Assert.All(watched, e => Assert.True(ArcRun.EntityOf(e)!.IsVisible));                                // it is still drawn,
        Assert.All(watched, e => Assert.True(e.AnimatedSprite!.IsPlaybackPaused));

        run.Context.Unblock(blocker);
        run.Run(6, 1);

        Assert.All(watched, e => Assert.Null(e.BlockedByEntity));
        Assert.All(watched, e => Assert.False(e.AnimatedSprite!.IsPlaybackPaused));
        Assert.NotEqual(frozen, watched.Select(e => e.AnimatedSprite!.CurrentAnimationTimeSeconds).ToArray()); // the animations run again.
    }

    [Fact]
    public void B7_AChestWhoseRecordHasAScriptAtF_IsLeftToTheScript_TheNativeF255IsNotRun()
    {
        var host = new PickupHost { Tables = new ItemTables(ProjectRootFinder.Find()), Dialogue = new FakeDialogueDirector(), Hero = new EntityScriptProxy() };
        var state = new GameState { PlayerControlFlags = GameState.PlayerControlBits.MenuOpen }; // the guard of F255 answers at once: the run is counted, nothing else happens.
        var runner = new EventProgramRunner(null, state, host);

        var scripted = new EntityScriptProxy { SpriteType = 30, EventTrigger = ScriptHelper.ProgramFInteract, ContentsItemId = 0x24 };
        scripted.ProgramIndexes[ScriptHelper.ProgramFInteract] = 3;
        scripted.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = 255;
        runner.RunSpriteEvent(scripted);

        Assert.DoesNotContain(runner.NativeHandlerRunCounts.Keys, key => key == (ScriptHelper.ProgramFInteract, PickupAi.Code));
        Assert.Empty(host.Spawns);
    }

    [Fact]
    public void B1_AChestWithNoScriptAtF_RunsTheNativeF255()
    {
        var host = new PickupHost { Tables = new ItemTables(ProjectRootFinder.Find()), Dialogue = new FakeDialogueDirector(), Hero = new EntityScriptProxy() };
        var state = new GameState { PlayerControlFlags = GameState.PlayerControlBits.MenuOpen };
        var runner = new EventProgramRunner(null, state, host);

        var native = new EntityScriptProxy { SpriteType = 30, EventTrigger = ScriptHelper.ProgramFInteract, ContentsItemId = 0x24 };
        native.SpriteProgramIndexes[ScriptHelper.ProgramFInteract] = 255;
        runner.RunSpriteEvent(native);

        Assert.Equal(1, runner.NativeHandlerRunCounts.GetValueOrDefault((ScriptHelper.ProgramFInteract, PickupAi.Code))); // the control: a chest with no script runs the native F255.
    }
}
