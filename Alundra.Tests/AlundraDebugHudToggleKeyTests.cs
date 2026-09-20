#nullable enable
using System;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E13 C5.a2 (docs/plan-e13-hud.md, D-E13-12): the F1 recipe key. The author set
/// <c>ALUNDRA_HUD_DEBUG</c> in the shell that launches the game and never saw the jauge - the log showed
/// a fresh DLL and the HUD screen wired to the active UI view, but no trace of the recipe ever applying,
/// which pointed at the variable never reaching the launcher's own process environment. F1 replaces the
/// variable as the AUTHOR-FACING trigger (the variable path itself stays, D-E13-12): one press loads the
/// debug stat set and requests the animated open, exactly like a map script's own opcode 0x05; the next
/// press (jauge affichée) clears the persistent latch the SAME way a map script's opcode 0x06 would,
/// which <see cref="AlundraHudDirector.RunTriggerMachine"/>'s own branch (i) turns into the animated
/// close; the next press (jauge cachée) re-raises the open request alone, stats untouched.
///
/// <see cref="AlundraWorldProxy.ToggleDebugHud"/> is called directly here (no live F1 key, no
/// <c>CasaEngineGame</c>/<c>KeyboardManager</c> needed for the three semantic tests below) - the SAME
/// method the real key calls (mission item 2/5: "chemin de production sauf la touche elle-même"). Only
/// the rising-edge test drives <see cref="AlundraWorldProxy.UpdateDebugHudToggleKey"/> itself, through
/// <see cref="AlundraWorldProxy.DebugHudToggleKeyHeldProviderForTests"/> - never MonoGame's
/// <c>Keyboard.GetState</c>.
/// </summary>
public sealed class AlundraDebugHudToggleKeyTests : IDisposable
{
    // word 0x38 bit 21 -> (0x38 &lt;&lt; 5) + 21 = 1813, mask 0x200000 - "please appear" (re-derived
    // independently from AlundraGameState's own IndexOf arithmetic, same precedent as
    // AlundraHudDirectorTests/AlundraHudPresenterTests's own local copies of these constants).
    private const uint ScriptOpenRequestFlag = 1813;
    private const uint ScriptOpenRequestMask = 0x200000;

    // word 0x33 bit 30 -> (0x33 &lt;&lt; 5) + 30 = 1662, mask 0x40000000 - the persistent "already armed" latch.
    private const uint PersistentLatchFlag = 1662;
    private const uint PersistentLatchMask = 0x40000000;

    public AlundraDebugHudToggleKeyTests()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
    }

    private static bool ScriptOpenRequestIsRaised()
        => (AlundraGameState.Instance.GetFlag(ScriptOpenRequestFlag) & ScriptOpenRequestMask) != 0;

    private static bool PersistentLatchIsSet()
        => (AlundraGameState.Instance.GetFlag(PersistentLatchFlag) & PersistentLatchMask) != 0;

    private static void TickHudDirector(int count)
    {
        for (var i = 0; i < count; i++)
        {
            AlundraHudDirector.Instance.Tick();
        }
    }

    // -----------------------------------------------------------------------------------------------
    // Mission item 4, first bullet: first press -> debug stats + bit 0x200000 of word 0x38 (id 1813).
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void ToggleDebugHud_FirstPress_LoadsDebugStatsAndRaisesScriptOpenRequest()
    {
        AlundraHudDirector.Instance.AttachToWorld(AlundraGameState.Instance);
        var proxy = new AlundraWorldProxy();

        proxy.ToggleDebugHud();

        var stats = AlundraGameState.Instance.PlayerStats;
        Assert.Equal(38, stats.Hp);
        Assert.Equal(45, stats.HpMax);
        Assert.Equal(2, stats.Mp);
        Assert.Equal(3, stats.MpMax);
        Assert.Equal(2163, stats.Money);

        Assert.True(ScriptOpenRequestIsRaised());
        Assert.True(AlundraGameState.Instance.DebugHudRecipeApplied);
    }

    // -----------------------------------------------------------------------------------------------
    // Mission item 4, second bullet: second press, jauge affichée -> latch cleared, and ticking the
    // director through it reproduces the exact Closing table (docs/plan-e13-hud.md §1.3), ending Idle -
    // same table AlundraHudDirectorTests.Closing_PositionSequence_MatchesTheMeasuredEighteenValueTable
    // proves the director itself produces from a directly-cleared latch; this test proves ToggleDebugHud
    // reaches the SAME latch through the SAME flag/mask, i.e. through the production entry point.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void ToggleDebugHud_SecondPress_WhileDisplayed_ClearsLatchAndDirectorClosesThroughTheMeasuredTable()
    {
        var expectedClosingTable = new[] { 16, 13, 9, 5, 1, -3, -6, -10, -14, -18, -22, -25, -29, -33, -37, -41, -41, -41 };

        AlundraHudDirector.Instance.AttachToWorld(AlundraGameState.Instance);
        var proxy = new AlundraWorldProxy();

        proxy.ToggleDebugHud(); // first press - opens.
        TickHudDirector(18); // reach Displayed(1) at Y = 16 (the Opening table's own 18th value).
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, AlundraHudDirector.Instance.Phase);
        Assert.True(PersistentLatchIsSet());

        proxy.ToggleDebugHud(); // second press - jauge affichée.

        Assert.False(PersistentLatchIsSet());
        // Not yet Idle: RunTriggerMachine's branch (i) only ARMS the close on the director's own NEXT Tick().
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, AlundraHudDirector.Instance.Phase);

        var actual = new int[18];
        for (var i = 0; i < 18; i++)
        {
            AlundraHudDirector.Instance.Tick();
            actual[i] = AlundraHudDirector.Instance.Y;
        }

        Assert.Equal(expectedClosingTable, actual);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, AlundraHudDirector.Instance.Phase);
    }

    // -----------------------------------------------------------------------------------------------
    // Mission item 4, third bullet: third press, jauge cachée -> 1813 re-raised alone, stats unchanged
    // (proven by mutating Hp away from the debug value between the second and third press - a reload
    // would stomp it back to 38, exactly like AlundraHudDebugRecipeTests' own no-replay proof).
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void ToggleDebugHud_ThirdPress_WhileHidden_ReRaisesRequestWithoutReloadingStats()
    {
        AlundraHudDirector.Instance.AttachToWorld(AlundraGameState.Instance);
        var proxy = new AlundraWorldProxy();

        proxy.ToggleDebugHud(); // first press - opens, loads 38/45/2/3/2163.
        TickHudDirector(18); // Displayed.
        proxy.ToggleDebugHud(); // second press - clears the latch.
        TickHudDirector(18); // Idle.
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, AlundraHudDirector.Instance.Phase);
        Assert.False(ScriptOpenRequestIsRaised()); // branch (ii) consumes its own request bit on the way in.

        AlundraPlayerManager.SetPlayerHp(AlundraGameState.Instance, 5); // away from the debug value.

        proxy.ToggleDebugHud(); // third press - jauge cachée.

        Assert.True(ScriptOpenRequestIsRaised());
        Assert.Equal(5, AlundraGameState.Instance.PlayerStats.Hp); // NOT reloaded back to 38.
        Assert.Equal(45, AlundraGameState.Instance.PlayerStats.HpMax);
        Assert.Equal(2163, AlundraGameState.Instance.PlayerStats.Money);
    }

    // -----------------------------------------------------------------------------------------------
    // Mission item 2's own dead-path guard: a press while Opening/Closing must not reach ArmDisappearance
    // mid-transition (see ToggleDebugHud's own doc on the documented dead path at Phase & 3 == 1 for
    // Opening = 5 too) - proven here by pressing again one tick INTO the opening animation.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void ToggleDebugHud_PressWhileOpening_IsIgnored()
    {
        AlundraHudDirector.Instance.AttachToWorld(AlundraGameState.Instance);
        var proxy = new AlundraWorldProxy();

        proxy.ToggleDebugHud(); // first press - opens.
        TickHudDirector(1); // one tick into the Opening animation - still Opening, not yet Displayed.
        Assert.Equal(AlundraHudDirector.HudPhase.Opening, AlundraHudDirector.Instance.Phase);

        var latchBefore = PersistentLatchIsSet();
        proxy.ToggleDebugHud(); // pressed again, mid-transition.

        Assert.Equal(AlundraHudDirector.HudPhase.Opening, AlundraHudDirector.Instance.Phase);
        Assert.Equal(latchBefore, PersistentLatchIsSet()); // untouched.
    }

    // -----------------------------------------------------------------------------------------------
    // Mission item 4, fourth bullet: rising-edge detection - a key held for 10 straight frames toggles
    // exactly once, via the injectable seam, never Keyboard.GetState/a real KeyboardState.
    // -----------------------------------------------------------------------------------------------

    [Fact]
    public void UpdateDebugHudToggleKey_KeyHeldTenFrames_TogglesExactlyOnce()
    {
        AlundraHudDirector.Instance.AttachToWorld(AlundraGameState.Instance);
        var proxy = new AlundraWorldProxy
        {
            DebugHudToggleKeyHeldProviderForTests = () => true,
        };

        for (var frame = 0; frame < 10; frame++)
        {
            proxy.UpdateDebugHudToggleKey();
        }

        // One toggle only: the first press's own effect (debug stats loaded, open request raised), not
        // ten - a rafale would have re-entered ToggleDebugHud's own "first press" branch nine more times
        // (harmless here since it is idempotent past the first) or, once past it, ten conflicting
        // open/close alternations instead of the single one this test isolates.
        Assert.True(AlundraGameState.Instance.DebugHudRecipeApplied);
        Assert.True(ScriptOpenRequestIsRaised());

        // Releasing then pressing again is a second, distinct rising edge.
        proxy.DebugHudToggleKeyHeldProviderForTests = () => false;
        proxy.UpdateDebugHudToggleKey();
        TickHudDirector(18); // reach Displayed.
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, AlundraHudDirector.Instance.Phase);

        proxy.DebugHudToggleKeyHeldProviderForTests = () => true;
        for (var frame = 0; frame < 10; frame++)
        {
            proxy.UpdateDebugHudToggleKey();
        }

        // Exactly one more toggle (the close request) - not ten.
        Assert.False(PersistentLatchIsSet());
    }
}
