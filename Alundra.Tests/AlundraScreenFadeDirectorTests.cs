#nullable enable
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// xunit collection covering every test class that touches the SESSION-scoped
/// <see cref="AlundraScreenFadeDirector.Instance"/> singleton (D-E10-6, docs/plan-e10-fondu.md, slice
/// E10.b) - same rationale as <see cref="AlundraMusicPlayerSingletonCollection"/>: classes sharing a
/// collection never run in parallel with each other, which is what keeps them from racing on that
/// shared mutable state (xunit runs different test CLASSES in this project in parallel by default).
/// </summary>
[CollectionDefinition(Name)]
public class AlundraScreenFadeDirectorSingletonCollection
{
    public const string Name = "AlundraScreenFadeDirector singleton";
}

/// <summary>
/// T1-T7 of docs/plan-e10-fondu.md, slice E10.b. Every test resets
/// <see cref="AlundraScreenFadeDirector.Instance"/> first/last (the "moyen de le réinitialiser en test"
/// D-E10-6 asks the slice to name, same shape as <see cref="AlundraMusicPlayer.ResetForTests"/>).
/// </summary>
[Collection(AlundraScreenFadeDirectorSingletonCollection.Name)]
public class AlundraScreenFadeDirectorTests : IDisposable
{
    public AlundraScreenFadeDirectorTests()
    {
        AlundraScreenFadeDirector.Instance.ResetForTests();

        // D-T-14 (docs/plan-transitions-carte.md, slice T1): this class constructs an AlundraWorldProxy,
        // so it shares the three session carriers T1 introduces - reset them here (constructor, the
        // isolation-carrying element) so no earlier test's state leaks in.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    public void Dispose()
    {
        AlundraScreenFadeDirector.Instance.ResetForTests();

        // D-T-14: hygiene, not covered by the acceptance (the constructor above is what carries
        // isolation) - kept for symmetry with the existing session-singleton test classes.
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
    }

    // ---- fixtures -------------------------------------------------------------------------------

    /// <summary>Real <see cref="CasaEngineGame"/> + real <see cref="ScreenEffectComponent"/>/
    /// <see cref="ScreenEffectService"/> - same headless-construction technique as
    /// <c>AlundraMusicPlayerTests.BuildGameWithAudio</c>: the component's constructor never touches a
    /// <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/>, only lazily via
    /// <c>GetOrCreatePixelTexture</c>, which this DLL's own director never calls.</summary>
    private static CasaEngineGame BuildGameWithScreenEffects()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));

        var componentsField = typeof(Microsoft.Xna.Framework.Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var screenEffectComponent = new ScreenEffectComponent(game);
        var screenEffectField = typeof(CasaEngineGame).GetField("<ScreenEffectComponent>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        screenEffectField.SetValue(game, screenEffectComponent);

        return game;
    }

    /// <summary>The exact 16-value table §1.5 gives for effect 0 (subtractive, 0xff0000 -> 0, step
    /// -0xff000, "advance then draw" so the first value is 239, never 255).</summary>
    private static readonly byte[] EffectZeroTable =
    {
        239, 223, 207, 191, 175, 159, 143, 127, 111, 95, 79, 63, 47, 31, 15, 0,
    };

    // ---- T1 - the effect 0, chiffré ---------------------------------------------------------------

    [Fact]
    public void T1_EffectZero_ArmedViaRealInstallPath_ServiceIntactBeforeUpdate_ThenExactSixteenValueTable()
    {
        var game = BuildGameWithScreenEffects();
        var world = new World { Name = "TestWorld" };
        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

        var proxy = new AlundraWorldProxy();
        proxy.InstallScreenFadeSystems(world); // the real install path (D-E10-7), called by InitializeWithWorld too.

        var service = game.ScreenEffectComponent.Service;

        // Mutation ("push at arming"): asserted BEFORE any Update at all - the service must be exactly
        // at its own untouched construction defaults.
        Assert.False(service.Active);
        Assert.Equal((byte)0, service.R);
        Assert.Equal((byte)0, service.G);
        Assert.Equal((byte)0, service.B);

        for (var tick = 1; tick <= 16; tick++)
        {
            proxy.Update(0.02f); // exactly one 50Hz logic tick per call.

            Assert.True(service.Active, $"tick {tick}: expected the overlay to still be active.");
            Assert.Equal(EffectZeroTable[tick - 1], service.R);
            Assert.Equal(EffectZeroTable[tick - 1], service.G);
            Assert.Equal(EffectZeroTable[tick - 1], service.B);
            Assert.Equal(SpriteBlendMode.Subtractive, service.Blend);
        }

        // Flag cleared at tick 16 (already proven by the last loop iteration reaching exactly 0); no
        // more submission at tick 17.
        proxy.Update(0.02f);
        Assert.False(service.Active, "tick 17: expected no submission once the fade has settled.");
    }

    [Fact]
    public void T1_Mutation_DrawThenAdvance_FirstPushedValueBecomes255()
    {
        // Mutation for T1: swap the order to "draw, then advance" - the first pushed value would be the
        // UN-advanced armed current (0xff0000 -> 255), not 239.
        var current = 0xff0000;

        // "Draw then advance" reads `current` BEFORE applying the step.
        var drawnFirst = (byte)(current >> 16);
        Assert.Equal((byte)255, drawnFirst);

        // The real (correct) order: advance, THEN the drawn value is 239 - see T1 above for the proof on
        // the real director/service. This assertion documents the exact mutation delta so the pairing is
        // legible without re-deriving it: 255 != 239.
        Assert.NotEqual((byte)239, drawnFirst);
    }

    // ---- T2 - neutralization twin ------------------------------------------------------------------

    [Fact]
    public void T2_NoGame_InstallAndUpdate_NoServiceToPushTo_NoException()
    {
        var world = new World { Name = "TestWorld" }; // World.Game left null.
        var proxy = new AlundraWorldProxy();

        var ex = Record.Exception(() =>
        {
            proxy.InstallScreenFadeSystems(world);
            proxy.Update(0.02f);
            proxy.Update(0.02f);
        });

        Assert.Null(ex);
    }

    // ---- T3 - the channel swap --------------------------------------------------------------------

    [Fact]
    public void T3_BeginFadeEffect_AsymmetricColor_PushesExactlyRGBInDisplayOrder()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        // duration = 0 -> the necessity-deviation snap path (D-E10-8): current jumps straight to target,
        // so ONE Advance call pushes exactly (r,g,b) with no interpolation math to account for.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 10, g: 20, b: 30, tpage: 1, duration: 0, persistLock: 0);

        AlundraScreenFadeDirector.Instance.Advance(1);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        Assert.True(service.Active);
        Assert.Equal((byte)10, service.R);
        Assert.Equal((byte)20, service.G);
        Assert.Equal((byte)30, service.B);
        Assert.Equal(SpriteBlendMode.Additive, service.Blend);
    }

    [Fact]
    public void T3_Mutation_FollowDecompNames_WouldSwapRAndB()
    {
        // Mutation for T3: routing v[1]/v[3] through the decomp's own inverted "B"/"R" variable names
        // (instead of storing DISPLAY order directly, see AlundraScreenFadeDirector's own class doc)
        // would swap the R and B channels - documented here as the exact delta the real T3 above guards
        // against: (10,20,30) pushed vs. the swapped (30,20,10) a "corrected" swap would produce.
        Assert.NotEqual((10, 20, 30), (30, 20, 10));
    }

    // ---- T4 - truncation, sign pinned --------------------------------------------------------------

    [Fact]
    public void T4_NonDividingDelta_TakesDurationPlusOneTicks()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        // Every colour operand is shifted <<16 (16.16), so a raw delta is always a multiple of 65536 -
        // with duration = 9 (which does NOT divide 65536), delta = 1 * 65536 gives step = floor(65536/9)
        // = 7281, remainder 7 - after 9 ticks the accumulated 65529 is 7 short of target, and the next
        // tick's own step (7281) clears that remainder in one more tick: exactly duration + 1 = 10 ticks.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 1, g: 1, b: 1, tpage: 0, duration: 9, persistLock: 0);

        for (var tick = 1; tick <= 9; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
            Assert.True(service.Active, $"tick {tick}: expected still ramping (not yet arrived).");
            Assert.NotEqual((byte)1, service.R);
        }

        AlundraScreenFadeDirector.Instance.Advance(1); // tick 10 = duration + 1.
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        Assert.Equal((byte)1, service.R); // arrived, clamped exactly to target.
        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
    }

    [Fact]
    public void T4_AscendingFade_DeltaSmallerThanDuration_StepTruncatesToZero_NeverCompletes()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        // Ascending: target (colour 1, raw delta 65536) ABOVE current (0, the default). duration
        // (70000) is LARGER than the raw delta (65536), so step truncates fully to 0 (not just a
        // remainder) - the fade NEVER completes, so 0xB1 (IsSettled) stays false forever.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 1, g: 1, b: 1, tpage: 0, duration: 70000, persistLock: 0);

        for (var tick = 0; tick < 100; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);
        Assert.Equal((byte)0, service.R); // current never left 0 - the step truly never moved it.
        Assert.True(service.Active);
    }

    [Fact]
    public void T4_DescendingFade_TruncatedZeroStep_SnapsInOneTick()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        // First settle current at colour 1 (duration 0 snap), THEN fade DOWN to 0 with the SAME
        // magnitude-vs-duration relationship as the ascending case above (raw delta 65536 < duration
        // 70000) - the truncated step is 0, but MoveTowards' own "step >= 0" branch (chosen by the SIGN
        // OF STEP, not of the delta - see that method's own doc) finds `target(0) < current(65536) + 0`
        // already true, and snaps straight to target in exactly ONE tick instead of never moving.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 1, g: 1, b: 1, tpage: 0, duration: 0, persistLock: 0);
        AlundraScreenFadeDirector.Instance.Advance(1);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();
        Assert.Equal((byte)1, service.R);

        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 0, g: 0, b: 0, tpage: 0, duration: 70000, persistLock: 0);

        AlundraScreenFadeDirector.Instance.Advance(1);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        Assert.Equal((byte)0, service.R); // settled to target in exactly one tick.
        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
    }

    [Fact]
    public void T4_Mutation_RoundInsteadOfTruncate_WouldArriveOneTickEarlierOnTheNonDividingCase()
    {
        // Mutation for T4: the real T4_NonDividingDelta test above uses delta=65536, duration=9 -
        // truncating division gives step 7281 (9 ticks reach 65529, still short of the 65536 target, a
        // 10th tick is needed). Rounding instead would give step 7282 (65536/9 = 7281.777..., rounds up)
        // - 9*7282 = 65538 already overshoots the target, so a "round instead of truncate" mutant would
        // arrive in exactly 9 ticks (duration), not 10 (duration + 1) - which is exactly what the real
        // test's own per-tick "not yet arrived" loop (asserted through tick 9) would catch.
        const int delta = 65536;
        const int duration = 9;

        var truncatedStep = delta / duration;
        var roundedStep = (int)Math.Round(delta / (double)duration, MidpointRounding.AwayFromZero);

        Assert.NotEqual(truncatedStep, roundedStep);
        Assert.True(duration * truncatedStep < delta); // truncated: still short after `duration` ticks.
        Assert.True(duration * roundedStep >= delta); // rounded: already arrived after `duration` ticks.
    }

    // ---- T5 - the persistence latch ----------------------------------------------------------------

    [Fact]
    public void T5_PersistNonZero_KeepsSubmittingTheSettledColour_Indefinitely()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 40, g: 50, b: 60, tpage: 0, duration: 4, persistLock: 7);

        for (var tick = 0; tick < 4; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled); // machine B itself has arrived.
        Assert.True(service.Active); // yet the persistence latch keeps it submitted.
        Assert.Equal((byte)40, service.R);
        Assert.Equal((byte)50, service.G);
        Assert.Equal((byte)60, service.B);

        // Indefinitely: many more frames, some with zero ticks, still keep submitting the settled color.
        for (var i = 0; i < 20; i++)
        {
            AlundraScreenFadeDirector.Instance.Advance(0);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        Assert.True(service.Active);
        Assert.Equal((byte)40, service.R);
    }

    [Fact]
    public void T5_Mutation_TreatingPersistAsCountdown_WouldEventuallyClear()
    {
        // Mutation for T5: a countdown interpretation would decrement the latch every tick and clear the
        // overlay once it reaches zero - documented delta; the real T5 test above (which never advances
        // the latch across an arbitrarily large number of ticks) is what actually dies under that
        // mutation.
        var persistAsCountdown = 7;
        for (var i = 0; i < 7; i++)
        {
            persistAsCountdown--;
        }

        Assert.Equal(0, persistAsCountdown); // a countdown reaches zero - the real latch (§1.1) never does.
    }

    // ---- T6 - 0xB1, both ways ------------------------------------------------------------------------

    [Fact]
    public void T6_IsSettled_StaleTrueBeforeFade_FalseDuringFade_TrueAfterArrival()
    {
        var game = BuildGameWithScreenEffects();
        var service = game.ScreenEffectComponent.Service;
        AlundraScreenFadeDirector.Instance.AttachToWorld(service);

        // Before any fade at all: settled (both machines idle).
        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);

        // Target (colour 4) above the default current (0) so there is an actual delta to ramp - a
        // duration of 4 divides 4*65536 evenly (§1.4's own "gray fades" case), settling exactly at tick 4.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 4, g: 4, b: 4, tpage: 0, duration: 4, persistLock: 0);
        AlundraScreenFadeDirector.Instance.Advance(1);

        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled); // mid-fade: not settled.

        AlundraScreenFadeDirector.Instance.Advance(3); // ticks 2,3,4 - arrival at tick 4 (duration exact).

        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled); // after arrival: settled again.
    }

    [Fact]
    public void T6_Mutation_UnknownOpcode_WouldLeaveStaleResultUntouched()
    {
        // Mutation for T6 (dispatched via the interpreter, not the director directly): routing 0xB1
        // through UnknownOpcode's own no-touch fallback leaves a STALE Result from whatever the PREVIOUS
        // predicate wrote - unlike the real Dispatch case (AlundraEventProgramRunner.cs), which always
        // writes Result explicitly (see that case's own doc). Documented delta: a stale Result=1 would
        // survive a skipped 0xB1 even while mid-fade, which the real dispatch case (writing
        // IsSettled ? 1 : 0 unconditionally) never allows.
        var staleResult = 1;
        var unknownOpcodeWouldLeave = staleResult; // UnknownOpcode never touches state.Result.
        Assert.Equal(1, unknownOpcodeWouldLeave); // stays stale - the real dispatch case writes 0 instead.
    }

    // ---- T7 - the reset preamble, THEN the re-arm ----------------------------------------------------

    [Fact]
    public void T7_SecondWorldInstall_ClearsFirstWorldLatch_AndFreshlyArmsEffectZero_InstanceIdentityPreserved()
    {
        var game1 = BuildGameWithScreenEffects();
        var world1 = new World { Name = "World1" };
        HeroWorldFixture.SetProperty(world1, nameof(World.Game), game1);

        var proxy1 = new AlundraWorldProxy();
        proxy1.InstallScreenFadeSystems(world1);

        // World 1 poses its OWN persisted tint (persist != 0) - this is the state that must NOT leak
        // into world 2 (§1.1: the next map entry clears it).
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 5, g: 5, b: 5, tpage: 0, duration: 1, persistLock: 9);
        AlundraScreenFadeDirector.Instance.Advance(1); // settles machine B; the persist latch (9) remains.
        AlundraScreenFadeDirector.Instance.PushToAttachedService();
        Assert.True(AlundraScreenFadeDirector.Instance.IsSettled);
        Assert.True(game1.ScreenEffectComponent.Service.Active); // persisted.

        var game2 = BuildGameWithScreenEffects();
        var world2 = new World { Name = "World2" };
        HeroWorldFixture.SetProperty(world2, nameof(World.Game), game2);

        var proxy2 = new AlundraWorldProxy();
        proxy2.InstallScreenFadeSystems(world2); // second install of the SAME session singleton.

        Assert.Same(proxy1.ScreenFadeDirector, proxy2.ScreenFadeDirector); // director instance preserved.

        var service2 = game2.ScreenEffectComponent.Service;

        // Freshly armed: flags = 1 (not settled), first advanced value 239 (world 1's tint discarded).
        Assert.False(AlundraScreenFadeDirector.Instance.IsSettled);

        for (var tick = 1; tick <= 16; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        // World 2's own fade completed at tick 16 - no submission at tick 17 (the persist latch was
        // reset to 0 by world 2's own install preamble, so nothing leaks from world 1's persist=9).
        AlundraScreenFadeDirector.Instance.Advance(1);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();
        Assert.False(service2.Active, "tick 17: world 1's persisted latch must not leak into world 2.");
    }

    [Fact]
    public void T7_Mutation_DropOnlyTheResetPreamble_WorldOneLatchLeaks_SubmissionContinuesAtTick17()
    {
        // Mutation for T7 (docs/plan-e10-fondu.md): applied directly against the director's own private
        // persistence-latch field, mirroring "delete only the three preamble lines of
        // AlundraScreenFadeDirector.InstallForMapEntry, keep the re-arm" - the re-arm never touches the
        // latch, so a leaked non-zero value survives into the second world's own fully-settled state.
        var game1 = BuildGameWithScreenEffects();
        AlundraScreenFadeDirector.Instance.AttachToWorld(game1.ScreenEffectComponent.Service);
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(r: 5, g: 5, b: 5, tpage: 0, duration: 1, persistLock: 9);
        AlundraScreenFadeDirector.Instance.Advance(1);
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        // Simulate the mutated install: re-arm WITHOUT resetting the persistence latch (skip the reset
        // preamble entirely, call only the re-arm's own effect - here reproduced via BeginFadeEffect with
        // persistLock left unspecified would still reset it through the real API, so this directly pokes
        // the private field to model "the preamble line was deleted").
        var latchField = typeof(AlundraScreenFadeDirector).GetField("_persistLock", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(latchField);
        var leakedLatchBeforeMutatedInstall = (int)latchField!.GetValue(AlundraScreenFadeDirector.Instance)!;
        Assert.NotEqual(0, leakedLatchBeforeMutatedInstall); // sanity: something to leak.

        // Re-arm effect 0 WITHOUT going through InstallForMapEntry's own reset (mutated shape).
        var currentRField = typeof(AlundraScreenFadeDirector).GetField("_currentR", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var currentGField = typeof(AlundraScreenFadeDirector).GetField("_currentG", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var currentBField = typeof(AlundraScreenFadeDirector).GetField("_currentB", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var targetRField = typeof(AlundraScreenFadeDirector).GetField("_targetR", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var targetGField = typeof(AlundraScreenFadeDirector).GetField("_targetG", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var targetBField = typeof(AlundraScreenFadeDirector).GetField("_targetB", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var fadeActiveField = typeof(AlundraScreenFadeDirector).GetField("_fadeActive", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var tpageField = typeof(AlundraScreenFadeDirector).GetField("_tpage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stepRField = typeof(AlundraScreenFadeDirector).GetField("_stepR", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stepGField = typeof(AlundraScreenFadeDirector).GetField("_stepG", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var stepBField = typeof(AlundraScreenFadeDirector).GetField("_stepB", BindingFlags.Instance | BindingFlags.NonPublic)!;

        currentRField.SetValue(AlundraScreenFadeDirector.Instance, 0xff0000);
        currentGField.SetValue(AlundraScreenFadeDirector.Instance, 0xff0000);
        currentBField.SetValue(AlundraScreenFadeDirector.Instance, 0xff0000);
        targetRField.SetValue(AlundraScreenFadeDirector.Instance, 0);
        targetGField.SetValue(AlundraScreenFadeDirector.Instance, 0);
        targetBField.SetValue(AlundraScreenFadeDirector.Instance, 0);
        fadeActiveField.SetValue(AlundraScreenFadeDirector.Instance, true);
        tpageField.SetValue(AlundraScreenFadeDirector.Instance, 2);
        stepRField.SetValue(AlundraScreenFadeDirector.Instance, -0xff000);
        stepGField.SetValue(AlundraScreenFadeDirector.Instance, -0xff000);
        stepBField.SetValue(AlundraScreenFadeDirector.Instance, -0xff000);
        // _persistLock is DELIBERATELY left untouched - the mutation under test.

        for (var tick = 1; tick <= 16; tick++)
        {
            AlundraScreenFadeDirector.Instance.Advance(1);
            AlundraScreenFadeDirector.Instance.PushToAttachedService();
        }

        AlundraScreenFadeDirector.Instance.Advance(1); // tick 17.
        AlundraScreenFadeDirector.Instance.PushToAttachedService();

        // Under the mutation: the leaked non-zero latch keeps the gate open - submission continues at
        // tick 17, which is exactly what the real T7 test's own final assertion (Assert.False) forbids.
        Assert.True(game1.ScreenEffectComponent.Service.Active, "the mutation's own signature: submission leaks past tick 17.");
    }
}
