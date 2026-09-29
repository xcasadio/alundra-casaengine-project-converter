#nullable enable
using System;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests;

/// <summary>
/// E16.d T3 (docs/plan-e16-etat-partie.md, K6, SD4, SD12): the save keys F5 and F6 - each precondition refused
/// on its own with nothing written and the state untouched, the capture of the hero's map and tile written with
/// its metadata, a capture the validation refuses never written, and every service status but "saved" logged
/// without an exception. The rules are injected (K1): the real export folder, a catalog predicate that answers
/// yes, the export's own item tables; the test fails naming the export when it is missing. The service is a fake
/// (D-E16-31).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveGameDirectorSaveTests : IDisposable
{
    private readonly HeldKeys _keys = new();
    private readonly FakeSaveSlots _slots = new();

    public AlundraSaveGameDirectorSaveTests()
    {
        ResetSingletons();
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = true;
        Director.KeyHeldProviderForTests = _keys.IsHeld;
        Director.SaveSlots = _slots;
        Director.RulesFactoryForTests = RealRules;

        // A New Game state already carries the sword's slot (F10): a capture of it validates.
        State.PlayerStats.WeaponId = 1;
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static AlundraSaveGameDirector Director => AlundraSaveGameDirector.Instance;

    private static AlundraGameState State => AlundraGameState.Instance;

    /// <summary>One press of <paramref name="key"/>: a frame with it released, then a frame with it held.</summary>
    private void Press(Keys key, AlundraEntityScriptProxy? hero, string worldName = Map389WorldName)
    {
        _keys.Held.Clear();
        Director.UpdateRecipeKeys(null, State, worldName, hero);
        _keys.Held.Add(key);
        Director.UpdateRecipeKeys(null, State, worldName, hero);
        _keys.Held.Clear();
    }

    // ---- Preconditions (K6 step 1) ------------------------------------------------------------------------------

    [Theory]
    [InlineData(SaveGameBlocker.NoHero, Keys.F5)]
    [InlineData(SaveGameBlocker.HeroInTheAir, Keys.F5)]
    [InlineData(SaveGameBlocker.DialogueOpen, Keys.F5)]
    [InlineData(SaveGameBlocker.InventoryOpen, Keys.F5)]
    [InlineData(SaveGameBlocker.SubInventoryOpen, Keys.F5)]
    [InlineData(SaveGameBlocker.PostProcessPending, Keys.F5)]
    [InlineData(SaveGameBlocker.PortraitOpening, Keys.F5)]
    [InlineData(SaveGameBlocker.PortraitAtRest, Keys.F5)]
    [InlineData(SaveGameBlocker.PortraitReturning, Keys.F5)]
    [InlineData(SaveGameBlocker.TransitionInProgress, Keys.F5)]
    [InlineData(SaveGameBlocker.MasterMusicFadeArmed, Keys.F5)]
    [InlineData(SaveGameBlocker.ControlFlagsNonZero, Keys.F5)]
    [InlineData(SaveGameBlocker.NoHero, Keys.F6)]
    [InlineData(SaveGameBlocker.HeroInTheAir, Keys.F6)]
    [InlineData(SaveGameBlocker.DialogueOpen, Keys.F6)]
    [InlineData(SaveGameBlocker.InventoryOpen, Keys.F6)]
    [InlineData(SaveGameBlocker.SubInventoryOpen, Keys.F6)]
    [InlineData(SaveGameBlocker.PostProcessPending, Keys.F6)]
    [InlineData(SaveGameBlocker.PortraitOpening, Keys.F6)]
    [InlineData(SaveGameBlocker.PortraitAtRest, Keys.F6)]
    [InlineData(SaveGameBlocker.PortraitReturning, Keys.F6)]
    [InlineData(SaveGameBlocker.TransitionInProgress, Keys.F6)]
    [InlineData(SaveGameBlocker.MasterMusicFadeArmed, Keys.F6)]
    [InlineData(SaveGameBlocker.ControlFlagsNonZero, Keys.F6)]
    public void EachPreconditionNotHeld_WritesNothing_AndLeavesTheStateIdentical(SaveGameBlocker blocker, Keys key)
    {
        var (hero, reason) = PoseBlocker(blocker);
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        Press(key, hero);

        Assert.Empty(_slots.SaveCalls);
        Assert.Contains(log.Warnings, line => line.Contains($"{key} refused") && line.Contains(reason));
        StateSnapshot.Take(State).AssertSameAs(before);
    }

    // ---- Success (K6 steps 2 to 4, C5) ----------------------------------------------------------------------

    [Theory]
    [InlineData(Keys.F5, AlundraSaveGameDirector.BinarySlotName, SaveGameFormat.Binary)]
    [InlineData(Keys.F6, AlundraSaveGameDirector.JsonSlotName, SaveGameFormat.Json)]
    public void Save_HeroAtAKnownTileOfMap389_WritesThatMapAndTile_WithItsMetadata(Keys key, string slot, SaveGameFormat format)
    {
        State.AddFlag(860, 1u << (0x5c & 0x1f));
        State.PlayerStats.Money = 321;
        State.GameTime = 216000 + 3600 + 60; // 01:01:01.
        var hero = HeroAt(tileX: 20, tileY: 30, tileZ: 1);
        using var log = LogCapture.Install();

        Press(key, hero);

        var call = Assert.Single(_slots.SaveCalls);
        Assert.Equal(slot, call.Slot);
        Assert.Equal(format, call.Format);
        Assert.Equal(389, call.Data.InitialMapId);
        Assert.Equal((20, 30, 1), (call.Data.CameraTileX, call.Data.CameraTileY, call.Data.CameraTileZ));
        Assert.Equal(State.GameFlags.Take(64), call.Data.GameFlags);
        Assert.Equal(321, call.Data.Money);
        Assert.Equal(State.GameTime, call.Data.GameTime);
        Assert.Equal(call.Data.BuildMetadata(), call.Metadata);
        Assert.Equal("  HP 10       TIME 01:01:01   ", call.Metadata["summary"]);
        Assert.True(call.Data.TryValidate(RealRules(), out var error), error);
        Assert.Contains(log.Infos, line => line.Contains($"{key}: saved map 389 tile (20, 30, 1)"));
    }

    /// <summary>A capture the validation refuses (a stat outside its domain, posed in the state) is never
    /// written - with the catalog predicate still answering yes, so the refusal comes from the field.</summary>
    [Theory]
    [InlineData(Keys.F5)]
    [InlineData(Keys.F6)]
    public void Save_ACaptureTheValidationRefuses_IsNeverWritten(Keys key)
    {
        State.PlayerStats.Money = -1;
        using var log = LogCapture.Install();

        Press(key, HeroAt());

        Assert.Empty(_slots.SaveCalls);
        Assert.Contains(log.Warnings, line => line.Contains($"{key} refused") && line.Contains("playerStats.money = -1"));
    }

    [Fact]
    public void Save_InAWorldWithoutAMapId_WritesNothing()
    {
        using var log = LogCapture.Install();

        Press(Keys.F5, HeroAt(), worldName: "TestWorld");

        Assert.Empty(_slots.SaveCalls);
        Assert.Contains(log.Warnings, line => line.Contains("F5 refused") && line.Contains("nothing captured"));
    }

    // ---- Service results (K6 step 4, K9) ----------------------------------------------------------------------

    public static TheoryData<SaveGameSaveStatus> EveryStatusButSaved()
    {
        var data = new TheoryData<SaveGameSaveStatus>();
        foreach (var status in Enum.GetValues<SaveGameSaveStatus>().Where(s => s != SaveGameSaveStatus.Saved))
        {
            data.Add(status);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusButSaved))]
    public void Save_EveryServiceStatusButSaved_IsOneLogLine_WithoutException(SaveGameSaveStatus status)
    {
        _slots.SaveStatus = status;
        var before = StateSnapshot.Take(State);
        using var log = LogCapture.Install();

        Press(Keys.F6, HeroAt());

        Assert.Single(_slots.SaveCalls);
        Assert.Single(log.Warnings, line => line.Contains("F6 refused") && line.Contains(status.ToString()));
        Assert.DoesNotContain(log.Infos, line => line.Contains("saved map"));
        StateSnapshot.Take(State).AssertSameAs(before);
    }
}
