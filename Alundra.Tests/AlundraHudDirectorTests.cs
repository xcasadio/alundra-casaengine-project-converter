#nullable enable
using System;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E13 C1 acceptance (docs/plan-e13-hud.md, tranche "C1 - DLL : le directeur de session du HUD"). Every
/// flag constant below is re-derived independently from <see cref="AlundraGameState"/>'s own arithmetic
/// (<c>IndexOf(flag) = (flag &gt;&gt; 5) &amp; 0x3ff</c>, AlundraGameState.cs:194/196 - see the mission's
/// own item 4) rather than copied from <see cref="AlundraHudDirector"/>'s private constants, so a
/// regression in one cannot silently pass the other's test.
/// </summary>
public sealed class AlundraHudDirectorTests : IDisposable
{
    // word 0x38 bit 21 -> (0x38 &lt;&lt; 5) + 21 = 1813, mask 0x200000 - "please appear".
    private const uint ScriptOpenRequestFlag = 1813;
    private const uint ScriptOpenRequestMask = 0x200000;

    // word 0x38 bit 22 -> (0x38 &lt;&lt; 5) + 22 = 1814, mask 0x400000 - "hide instantly".
    private const uint ScriptCloseRequestFlag = 1814;
    private const uint ScriptCloseRequestMask = 0x400000;

    // word 0x33 bit 30 -> (0x33 &lt;&lt; 5) + 30 = 1662, mask 0x40000000 - the persistent "already armed" latch.
    private const uint PersistentLatchFlag = 1662;
    private const uint PersistentLatchMask = 0x40000000;

    public AlundraHudDirectorTests()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();

        // The last test in this file (Update_AdvancesTheHudDirector_EvenWhileAModalDialogueIsOpen)
        // drives AlundraWorldProxy, which reads the SESSION-scoped AlundraGameState.Instance (not a
        // fresh instance) - reset every session carrier AlundraDialogueFramePassTests also resets, so no
        // earlier test's state leaks in here and this class leaks none out either.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraHudDirector.Instance.ResetForTests();
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static AlundraHudDirector ArmedOpening(AlundraGameState state)
    {
        var director = AlundraHudDirector.Instance;
        director.AttachToWorld(state);
        state.AddFlag(ScriptOpenRequestFlag, ScriptOpenRequestMask);
        return director;
    }

    private static void TickMany(AlundraHudDirector director, int count)
    {
        for (var i = 0; i < count; i++)
        {
            director.Tick();
        }
    }

    // -----------------------------------------------------------------------------------------
    // §1.3 - the two exact position tables, one per direction, computed by the tween, not recopied.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void Opening_PositionSequence_MatchesTheMeasuredEighteenValueTable()
    {
        var expected = new[] { -41, -38, -34, -30, -26, -22, -19, -15, -11, -7, -3, 0, 4, 8, 12, 16, 16, 16 };

        var state = new AlundraGameState();
        var director = ArmedOpening(state);

        var actual = new int[18];
        for (var i = 0; i < 18; i++)
        {
            director.Tick();
            actual[i] = director.Y;
        }

        Assert.Equal(expected, actual);
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);
    }

    [Fact]
    public void Closing_PositionSequence_MatchesTheMeasuredEighteenValueTable_DistinctFromOpening()
    {
        var expected = new[] { 16, 13, 9, 5, 1, -3, -6, -10, -14, -18, -22, -25, -29, -33, -37, -41, -41, -41 };

        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 18); // reach Displayed(1) at Y = 16, exactly like the previous test.
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);

        // The original clears the persistent latch from elsewhere while the jauge is displayed
        // (GameEngine.cs:336, a map-transition effect id - out of C1's own scope, plan §1.6/§6 point 2) -
        // simulated here at the flag level, exactly the state RunTriggerMachine's branch (i) reads.
        state.SetFlag(PersistentLatchFlag, ~PersistentLatchMask);

        var actual = new int[18];
        for (var i = 0; i < 18; i++)
        {
            director.Tick();
            actual[i] = director.Y;
        }

        Assert.Equal(expected, actual);
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);

        // The truncation-toward-zero shape (§1.3) makes closing genuinely NOT the opening reversed -
        // the reversed opening table would read 16,12,8,4,0,-3,-7,-11,-15,-19,-22,-26,-30,-34,-38,-41,
        // -41,-41, which differs from `expected` from its 3rd element on.
        var reversedOpening = new[] { -41, -38, -34, -30, -26, -22, -19, -15, -11, -7, -3, 0, 4, 8, 12, 16, 16, 16 }
            .Reverse().ToArray();
        Assert.NotEqual(reversedOpening, expected);
    }

    // -----------------------------------------------------------------------------------------
    // §1.2 - the phase machine, 0 -> 5 -> 1 -> 3 -> 0, plus the branch (iii) instant hide and the
    // harmless every-tick rearm of branch (i) while idle.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void PhaseMachine_GoesThroughAllFourValues_InOrder()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);

        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase); // before the first Tick.

        director.Tick(); // trigger machine arms Opening(5) and takes the first tween step this SAME tick.
        Assert.Equal(AlundraHudDirector.HudPhase.Opening, director.Phase);

        TickMany(director, 17); // 17 more ticks (18 total) collapses Opening(5) -> Displayed(1).
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);

        state.SetFlag(PersistentLatchFlag, ~PersistentLatchMask); // external latch clear, see previous test.
        director.Tick();
        Assert.Equal(AlundraHudDirector.HudPhase.Closing, director.Phase);

        TickMany(director, 17); // 18 total collapses Closing(3) -> Idle(0).
        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);
    }

    [Fact]
    public void BranchThree_HidesInstantly_NoAnimation_UnlikeBranchOnesAnimatedClose()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 18);
        Assert.Equal(AlundraHudDirector.HudPhase.Displayed, director.Phase);
        Assert.Equal(16, director.Y);
        var frameCounterBefore = director.FrameCounter;

        // Branch (iii): the script's "hide instantly" request - HudManager.cs's own ResetDrawFrameFlags,
        // a straight assignment to 0, never a tween arm (mission's own "ATTENTION" note).
        state.AddFlag(ScriptCloseRequestFlag, ScriptCloseRequestMask);
        director.Tick();

        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);
        Assert.Equal(16, director.Y); // position untouched - no tween ever ran.
        // wasActive was computed AFTER branch (iii) already zeroed the phase THIS tick (Tick's own doc):
        // the counter/rolled-display half of this same tick never ran either.
        Assert.Equal(frameCounterBefore, director.FrameCounter);
    }

    [Fact]
    public void BranchOne_RearmsEveryTick_ButOnlyActsWhileDisplayed_HarmlessOnceIdle()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 18); // Displayed(1).
        state.SetFlag(PersistentLatchFlag, ~PersistentLatchMask);
        TickMany(director, 18); // Idle(0) again - the latch was never re-set by branch (i) itself.

        Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);
        Assert.Equal(0u, state.GetFlag(PersistentLatchFlag) & PersistentLatchMask);

        // Branch (i) fires again every one of these ticks (latch still 0) but ArmDisappearance's own
        // guard, (Phase & 3) == 1, rejects Idle(0) - a true no-op, not just a coincidentally-idle one.
        for (var i = 0; i < 5; i++)
        {
            director.Tick();
            Assert.Equal(AlundraHudDirector.HudPhase.Idle, director.Phase);
        }
    }

    // -----------------------------------------------------------------------------------------
    // §1.4 - cadences: HP catch-up 1 point / 8 ticks, coin icon 1 frame / 6 ticks while rolling, magic
    // pip 1 frame / 10 ticks (cycle of 4), money roll +-10 then +-1.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void HpCatchUp_LosesOnePoint_EveryEightActiveTicks()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 18); // Displayed(1), FrameCounter == 18, Hp == HpMax == 10 (New-Game default).

        AlundraPlayerManager.SetPlayerHp(state, 5); // true Hp drops well below the displayed value.

        TickMany(director, 8);
        Assert.Equal(9, director.Hp); // exactly one point lost.

        TickMany(director, 8);
        Assert.Equal(8, director.Hp); // exactly one more, not two - the cadence is 8, not faster.
    }

    [Fact]
    public void CoinIconFrame_AdvancesOnce_EverySixActiveTicks_WhileRolling()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 18); // Displayed(1), FrameCounter == 18, Money settled at 0 == 0 (no roll).
        Assert.Equal(0, director.CoinIconFrame);

        AlundraPlayerManager.SetMoney(state, 1000); // far above the displayed 0 - keeps rolling throughout.

        TickMany(director, 5); // FrameCounter 19..23 - no multiple of 6 in that range.
        Assert.Equal(0, director.CoinIconFrame);

        director.Tick(); // FrameCounter == 24 - the first coin-icon advance.
        Assert.Equal(1, director.CoinIconFrame);

        TickMany(director, 5);
        director.Tick(); // FrameCounter == 30.
        Assert.Equal(2, director.CoinIconFrame);

        TickMany(director, 5);
        director.Tick(); // FrameCounter == 36.
        Assert.Equal(3, director.CoinIconFrame);

        TickMany(director, 5);
        director.Tick(); // FrameCounter == 42 - wraps 4 -> 0 (HudManager.cs's own "3 < [9] -> [9] = 0").
        Assert.Equal(0, director.CoinIconFrame);
    }

    [Fact]
    public void CoinIconFrame_IsGaragedAtZero_OnceTheMoneyRollSettles()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        AlundraPlayerManager.SetMoney(state, 5); // small target, settles within the 18-tick opening.
        TickMany(director, 18);

        Assert.Equal(5, director.Money);
        Assert.Equal(0, director.CoinIconFrame);

        // Settled: further ticks never advance the coin frame again, regardless of how many run.
        TickMany(director, 30);
        Assert.Equal(0, director.CoinIconFrame);
    }

    [Fact]
    public void MagicPipFrame_AdvancesOnce_EveryTenActiveTicks_CycleOfFour_RipplingByPipIndex()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);

        // FrameCounter counts every active tick starting at 1 (the very first Tick call). Phase per
        // HudManager.cs:634-638: (FrameCounter / 10) % 4 - checked at a representative point in each of
        // the first four ten-tick windows. Each pip shows (phase + its own index) % 4: the ripple the
        // author confirmed on 2026-09-20 (plan §6 point 4), carried by MagicPipPhaseOffset.
        TickMany(director, 9); // FrameCounter == 9 -> phase (9/10)%4 == 0.
        AssertRipple(director, basePhase: 0);

        TickMany(director, 10); // FrameCounter == 19 -> phase (19/10)%4 == 1.
        AssertRipple(director, basePhase: 1);

        TickMany(director, 10); // FrameCounter == 29 -> phase (29/10)%4 == 2.
        AssertRipple(director, basePhase: 2);

        TickMany(director, 10); // FrameCounter == 39 -> phase (39/10)%4 == 3.
        AssertRipple(director, basePhase: 3);

        TickMany(director, 10); // FrameCounter == 49 -> phase (49/10)%4 == 0 again (wrapped).
        AssertRipple(director, basePhase: 0);
    }

    /// <summary>The four pips are all different at any instant, and each one is exactly its own index
    /// ahead of the base phase - which is what makes the row ripple rather than blink together.</summary>
    private static void AssertRipple(AlundraHudDirector director, int basePhase)
    {
        Assert.Equal(4, director.MagicPipFrame.Count);

        for (var pip = 0; pip < 4; pip++)
        {
            Assert.Equal((basePhase + pip) % 4, director.MagicPipFrame[pip]);
        }

        Assert.Equal(4, director.MagicPipFrame.Distinct().Count());
    }

    [Fact]
    public void MoneyRoll_MovesByTenThenByOne_TowardTheTrueTarget()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        AlundraPlayerManager.SetMoney(state, 25);

        var progression = new int[9];
        for (var i = 0; i < progression.Length; i++)
        {
            director.Tick();
            progression[i] = director.Money;
        }

        // 0 -> 10 -> 20 (two +10 steps, staying strictly below the target) -> 21..25 (five +1 steps,
        // HudManager.cs:466-476's own `if (idx < value) += 10 else += 1`, strict `<` even at the last
        // +10-sized gap) -> settled, held at 25.
        // Settled at tick 7 (index 6): HudManager.cs:440-442's own trailing clause,
        // `[4]==value && [9]!=0`, keeps taking the FIRST branch (a harmless no-op once the numbers
        // already match) for as long as the coin frame is still non-zero from blinking during the
        // roll - true garaging (a `[9]=0` write) only happens once the frame wraps back to 0 on its own
        // %6 cadence, which the two dedicated CoinIconFrame tests above cover; this test is about the
        // Money progression only.
        Assert.Equal(new[] { 10, 20, 21, 22, 23, 24, 25, 25, 25 }, progression);
    }

    [Fact]
    public void MoneyRoll_MovesDownwardByTenThenByOne()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        AlundraPlayerManager.SetMoney(state, 2163); // C0's own debug value set, well above the New-Game 0.
        TickMany(director, 18); // Displayed(1); money is still rolling upward, far from settled.
        var afterOpening = director.Money;
        Assert.True(afterOpening > 0 && afterOpening < 2163);

        // Now drop the true target back down hard - the roll must turn around and count DOWN,
        // +-10 then +-1, the same shape mirrored (HudManager.cs:438-458).
        AlundraPlayerManager.SetMoney(state, 0);
        var previous = afterOpening;
        var sawStepOfTen = false;
        var sawStepOfOne = false;
        for (var i = 0; i < 400 && director.Money != 0; i++)
        {
            director.Tick();
            var delta = previous - director.Money;
            if (delta == 10)
            {
                sawStepOfTen = true;
            }
            else if (delta == 1)
            {
                sawStepOfOne = true;
            }
            else
            {
                Assert.Equal(0, delta); // only settled (no-op) ticks besides the two step sizes above.
            }

            previous = director.Money;
        }

        Assert.Equal(0, director.Money);
        Assert.True(sawStepOfTen);
        Assert.True(sawStepOfOne);
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 5 - InstallForMapEntry does not zero the animated state (it survives a map change,
    // like C0's own PlayerStats), and AttachToWorld re-points without resetting.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void AttachToWorld_RePointsWithoutResetting_InstallForMapEntry_DoesNotResetEither()
    {
        var state = new AlundraGameState();
        var director = ArmedOpening(state);
        TickMany(director, 10); // mid-transition: Opening(5), FrameCounter == 10, Y somewhere in the table.

        var phaseBefore = director.Phase;
        var yBefore = director.Y;
        var counterBefore = director.FrameCounter;

        director.AttachToWorld(state); // re-point (same instance here, but exercises the call).
        Assert.Equal(phaseBefore, director.Phase);
        Assert.Equal(yBefore, director.Y);
        Assert.Equal(counterBefore, director.FrameCounter);

        director.InstallForMapEntry();
        Assert.Equal(phaseBefore, director.Phase);
        Assert.Equal(yBefore, director.Y);
        Assert.Equal(counterBefore, director.FrameCounter);
    }

    // -----------------------------------------------------------------------------------------
    // Mission item 6.d - the director advances at its production call site
    // (AlundraWorldProxy.Update) even while a modal dialogue box is open.
    // -----------------------------------------------------------------------------------------

    private static AlundraWorldProxy BuildProxyWithBothDirectorsAttached()
    {
        var world = new World { Name = "TestWorld" };
        var camera = new Camera2dComponent();
        world.Entities.Add(new Entity { Name = "camera", RootComponent = camera });

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(world); // "TestWorld" has no tileMap entity - early-returns before the
                                           // real install, same seam AlundraDialogueFramePassTests documents.

        AlundraDialogueDirector.Instance.AttachToWorld(new DialogueService(), proxy.GameState);
        AlundraHudDirector.Instance.AttachToWorld(proxy.GameState);
        return proxy;
    }

    [Fact]
    public void Update_AdvancesTheHudDirector_EvenWhileAModalDialogueIsOpen()
    {
        var proxy = BuildProxyWithBothDirectorsAttached();
        var hud = AlundraHudDirector.Instance;
        var dialogue = AlundraDialogueDirector.Instance;

        proxy.GameState.AddFlag(ScriptOpenRequestFlag, ScriptOpenRequestMask); // arm the HUD open.

        // controlMode 0 = MenuOpen (AlundraGameState.PlayerControlBits.MenuOpen) - the original's own
        // "map events/world updates pause too" mode, the STRICTEST kind of dialogue freeze there is.
        dialogue.Open("bonjour", controlMode: 0);
        Assert.True(dialogue.IsOpen);
        Assert.Equal(0, hud.FrameCounter);
        Assert.False(hud.IsDrawn);

        proxy.Update(1f / 50f); // one logic tick.

        Assert.True(dialogue.IsOpen); // unaffected either way - not this test's own point.
        Assert.True(hud.IsDrawn);
        Assert.Equal(1, hud.FrameCounter);
        Assert.Equal(AlundraHudDirector.HudPhase.Opening, hud.Phase);

        for (var i = 0; i < 4; i++)
        {
            proxy.Update(1f / 50f);
        }

        Assert.True(dialogue.IsOpen); // still open - the HUD's own advance never depended on it.
        Assert.Equal(5, hud.FrameCounter);
    }
}
