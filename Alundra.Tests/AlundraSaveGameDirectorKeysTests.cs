#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;
using Xunit;
using static Alundra.Tests.SaveGameDirectorTestSupport;

namespace Alundra.Tests;

/// <summary>
/// E16.d T2 (docs/plan-e16-etat-partie.md, K1 to K5, D-E16-33, SD6, SD7, SD13): the debug switch, the recipe keys
/// and their rising edges, the adapter over the engine's service, and the choice of the most recent slot. The
/// service is always a fake, or the adapter over a service that throws: no test reaches the real save folder
/// (D-E16-31).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraSaveGameDirectorKeysTests : IDisposable
{
    public AlundraSaveGameDirectorKeysTests()
    {
        ResetSingletons();
    }

    public void Dispose()
    {
        ResetSingletons();
    }

    private static AlundraSaveGameDirector Director => AlundraSaveGameDirector.Instance;

    private static (HeldKeys Keys, FakeSaveSlots Slots) Arm(bool enabled)
    {
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = enabled;
        var keyboard = new HeldKeys();
        var slots = new FakeSaveSlots();
        Director.KeyHeldProviderForTests = keyboard.IsHeld;
        Director.SaveSlots = slots;
        Director.RulesFactoryForTests = RealRules;

        // A New Game state already carries the sword's slot (F10): a capture of it validates.
        AlundraGameState.Instance.PlayerStats.WeaponId = 1;
        return (keyboard, slots);
    }

    private static void Frame(AlundraEntityScriptProxy? hero, string worldName = Map389WorldName)
        => Director.UpdateRecipeKeys(null, AlundraGameState.Instance, worldName, hero);

    // ---- K2: the switch ---------------------------------------------------------------------------------------

    [Fact]
    public void Switch_ForcedOn_IsLoggedOnce_AtTheFirstPass()
    {
        Arm(enabled: true);
        using var log = LogCapture.Install();

        Frame(HeroAt());
        Frame(HeroAt());
        Frame(HeroAt());

        Assert.Single(log.Infos, line => line.Contains("recipe keys ON"));
    }

    [Fact]
    public void Switch_ForcedOff_ReadsNoKey_LogsNothing_AndF5F6F9HaveNoEffect()
    {
        var (keyboard, slots) = Arm(enabled: false);
        keyboard.Held.UnionWith(new[] { Keys.F5, Keys.F6, Keys.F9 });
        var before = StateSnapshot.Take(AlundraGameState.Instance);
        using var log = LogCapture.Install();

        for (var frame = 0; frame < 5; frame++)
        {
            Frame(HeroAt());
            keyboard.Held.Clear();
            Frame(HeroAt());
            keyboard.Held.UnionWith(new[] { Keys.F5, Keys.F6, Keys.F9 });
        }

        Assert.Equal(0, keyboard.Reads);
        Assert.True(slots.NothingCalled);
        Assert.Empty(log.Infos);
        Assert.Empty(log.Warnings);
        StateSnapshot.Take(AlundraGameState.Instance).AssertSameAs(before);
    }

    /// <summary>SD7: without any forcing, the switch is the build's - on in a DLL compiled in Debug, off in
    /// Release. Also run with <c>dotnet test -c Release --filter</c>, where the test project is compiled in Release
    /// too and takes the other branch.</summary>
    [Fact]
    public void Switch_Default_WithoutForcing_IsOnInDebug_AndOffInRelease()
    {
        AlundraSaveGameDirector.RecipeKeysEnabledOverrideForTests = null;
#if DEBUG
        Assert.True(AlundraSaveGameDirector.RecipeKeysEnabledByBuild);
        Assert.True(AlundraSaveGameDirector.RecipeKeysEnabled);
#else
        Assert.False(AlundraSaveGameDirector.RecipeKeysEnabledByBuild);
        Assert.False(AlundraSaveGameDirector.RecipeKeysEnabled);
#endif
    }

    // ---- K3: rising edges, one key per frame ----------------------------------------------------------------

    [Fact]
    public void F9_HeldTenFrames_ActsOnce_AndActsAgainAfterARelease()
    {
        var (keyboard, slots) = Arm(enabled: true);
        var hero = HeroAt();

        keyboard.Held.Add(Keys.F9);
        for (var frame = 0; frame < 10; frame++)
        {
            Frame(hero);
        }

        Assert.Equal(1, slots.ListCalls);

        keyboard.Held.Clear();
        Frame(hero);
        keyboard.Held.Add(Keys.F9);
        Frame(hero);

        Assert.Equal(2, slots.ListCalls);
    }

    [Fact]
    public void F5_HeldTenFrames_SavesOnce()
    {
        var (keyboard, slots) = Arm(enabled: true);
        var hero = HeroAt();

        keyboard.Held.Add(Keys.F5);
        for (var frame = 0; frame < 10; frame++)
        {
            Frame(hero);
        }

        Assert.Single(slots.SaveCalls);
    }

    /// <summary>SD13: the edge lives in the session director, so F9 held through a world change (a new world
    /// name, a new hero proxy) does not act a second time.</summary>
    [Fact]
    public void F9_HeldThroughAWorldChange_DoesNotActASecondTime()
    {
        var (keyboard, slots) = Arm(enabled: true);

        keyboard.Held.Add(Keys.F9);
        Frame(HeroAt(), Map389WorldName);
        Frame(HeroAt(10, 40, 4), "Ship Klark (inner)-390");
        Frame(HeroAt(10, 40, 4), "Ship Klark (inner)-390");

        Assert.Equal(1, slots.ListCalls);
    }

    [Fact]
    public void F5F6F9_PressedOnTheSameFrame_OnlyF5Acts_AndNoneActsOnTheNextFrames()
    {
        var (keyboard, slots) = Arm(enabled: true);
        var hero = HeroAt();

        keyboard.Held.UnionWith(new[] { Keys.F5, Keys.F6, Keys.F9 });
        Frame(hero);
        Frame(hero);
        Frame(hero);

        var call = Assert.Single(slots.SaveCalls);
        Assert.Equal(AlundraSaveGameDirector.BinarySlotName, call.Slot);
        Assert.Equal(SaveGameFormat.Binary, call.Format);
        Assert.Equal(0, slots.ListCalls);
    }

    // ---- K5: the most recent slot -----------------------------------------------------------------------------

    private static readonly DateTime T0 = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MostRecent_DifferentTimes_TakesTheLatest()
    {
        var chosen = AlundraSaveGameDirector.SelectMostRecent(new List<AlundraSlotEntry>
        {
            new("zeta", true, T0),
            new("alpha", true, T0.AddSeconds(5)),
            new("mid", true, T0.AddSeconds(1)),
        });

        Assert.Equal("alpha", chosen?.Name);
    }

    [Fact]
    public void MostRecent_EqualTimes_TakesTheGreatestNameInOrdinalOrder()
    {
        var chosen = AlundraSaveGameDirector.SelectMostRecent(new List<AlundraSlotEntry>
        {
            new("debug-binary", true, T0),
            new("debug-json", true, T0),
            new("Debug-zz", true, T0), // ordinal: uppercase sorts before lowercase.
        });

        Assert.Equal("debug-json", chosen?.Name);
    }

    [Fact]
    public void MostRecent_SkipsUnknownTimes_AndUnreadableSlots()
    {
        var chosen = AlundraSaveGameDirector.SelectMostRecent(new List<AlundraSlotEntry>
        {
            new("no-time", true, null),
            new("unreadable", false, T0.AddHours(1)),
            new("kept", true, T0),
        });

        Assert.Equal("kept", chosen?.Name);
    }

    [Fact]
    public void MostRecent_NoSlot_OrNoCandidate_IsNull()
    {
        Assert.Null(AlundraSaveGameDirector.SelectMostRecent(new List<AlundraSlotEntry>()));
        Assert.Null(AlundraSaveGameDirector.SelectMostRecent(null));
        Assert.Null(AlundraSaveGameDirector.SelectMostRecent(new List<AlundraSlotEntry>
        {
            new("no-time", true, null),
            new("unreadable", false, T0),
        }));
    }

    [Fact]
    public void F9_WithNoReadableSlot_IsRefused_WithoutLoading()
    {
        var (keyboard, slots) = Arm(enabled: true);
        slots.Slots.Add(new AlundraSlotEntry("unreadable", false, T0));
        slots.Slots.Add(new AlundraSlotEntry("no-time", true, null));
        using var log = LogCapture.Install();

        keyboard.Held.Add(Keys.F9);
        Frame(HeroAt());

        Assert.Empty(slots.LoadCalls);
        Assert.Contains(log.Warnings, line => line.Contains("F9 refused") && line.Contains("no readable save slot"));
    }

    // ---- K4/SD6: a service that throws, behind the adapter ----------------------------------------------------

    [Fact]
    public void AServiceThatThrowsAtEveryCall_BehindTheAdapter_RefusesF5F6AndF9_WithoutException_StateIdentical()
    {
        var (keyboard, _) = Arm(enabled: true);
        Director.SaveSlots = new AlundraEngineSaveSlots(() => throw new InvalidOperationException("simulated service failure"));
        var hero = HeroAt();
        var state = AlundraGameState.Instance;
        state.PlayerStats.WeaponId = 1; // a capture of this state validates, so F5/F6 do reach the service.
        var before = StateSnapshot.Take(state);
        using var log = LogCapture.Install();

        foreach (var key in new[] { Keys.F5, Keys.F6, Keys.F9 })
        {
            keyboard.Held.Clear();
            Frame(hero);
            keyboard.Held.Add(key);
            Frame(hero);
        }

        Assert.Equal(3, log.Errors.Count(line => line.Contains("simulated service failure")));
        Assert.Contains(log.Warnings, line => line.Contains("F5 refused") && line.Contains("IoError"));
        Assert.Contains(log.Warnings, line => line.Contains("F6 refused") && line.Contains("IoError"));
        Assert.Contains(log.Warnings, line => line.Contains("F9 refused"));
        StateSnapshot.Take(state).AssertSameAs(before);
    }

    [Fact]
    public void Adapter_OverAThrowingService_ReturnsIoErrorOrAnEmptyList_ForEachCall()
    {
        var adapter = new AlundraEngineSaveSlots(() => throw new ArgumentException("bad slot"));
        using var log = LogCapture.Install();

        var save = adapter.Save("debug-json", ValidSave(), SaveGameFormat.Json, new Dictionary<string, string>());
        var load = adapter.TryLoad("debug-json", out var loaded);
        var list = adapter.ListSlots();

        Assert.Equal(SaveGameSaveStatus.IoError, save.Status);
        Assert.Equal(SaveGameLoadStatus.IoError, load.Status);
        Assert.Null(loaded);
        Assert.Empty(list);
        Assert.Equal(3, log.Errors.Count);
    }
}
