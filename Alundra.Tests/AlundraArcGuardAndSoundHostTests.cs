#nullable enable
using System;
using System.Reflection;
using Alundra.Scripts;
using Xunit;
using Xunit.Sdk;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c2 D4 (docs/plan-e19-opcodes.md §1.2h.3.2, hygiene of E19.d2c1): the permanent test of the guard T-REG-0 of <see cref="ArcRun"/>, and the one of the
/// production branch of the sound player of the script host (<see cref="AlundraWorldProxy"/> hands its player out through <see cref="IAlundraScriptHost.SoundPlayer"/>).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraArcGuardAndSoundHostTests
{
    // The arc "A5" is one of those that must meet no entity (T-REG-0); the montage is the one of the arc of the tests of the air state (the real map 179).
    private static ArcSpec GuardedSpec(string name) => new(name, "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
        Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, AlundraGameState.ResetAnimationId, 0));

    [Fact]
    public void TReg0_AGreenArcOfTheGuardedSet_ThatMetAnEntity_FailsAtItsDispose()
    {
        var arc = new ArcRun(GuardedSpec("A5"));
        arc.OneFrame();
        arc.Hero.EntityBlockCount = 1; // an entity shortened a controller step of the arc.

        var failure = Assert.Throws<XunitException>(() => arc.Dispose());
        Assert.Contains("T-REG-0", failure.Message);
    }

    [Fact]
    public void TReg0_AnArcThatNeverMetAnEntity_DisposesWithoutFailure_AndSoDoesAnArcOutsideTheGuardedSet()
    {
        using (var clean = new ArcRun(GuardedSpec("A5")))
        {
            clean.OneFrame();
        }

        var outside = new ArcRun(GuardedSpec("NotGuarded"));
        outside.OneFrame();
        outside.Hero.EntityBlockCount = 1;
        outside.Dispose(); // not in the set: no check.
    }

    [Fact]
    public void TReg0_WhenTheTestIsAlreadyFailing_TheGuardDoesNotReplaceTheOriginalFailure()
    {
        var failure = Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using var arc = new ArcRun(GuardedSpec("A5"));
            arc.OneFrame();
            arc.Hero.EntityBlockCount = 1;
            throw new InvalidOperationException("the original failure");
        }));

        Assert.Equal("the original failure", failure.Message);
    }

    [Fact]
    public void TheSoundPlayerOfTheWorldProxy_IsTheOneTheScriptHostHandsOut()
    {
        var proxy = new AlundraWorldProxy();
        var host = (IAlundraScriptHost)proxy;
        Assert.Null(host.SoundPlayer); // not installed yet: the proxy's own member answers (the default member of the interface answers null too).

        var player = new RecordingSoundPlayer();
        typeof(AlundraWorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(proxy, player);

        // The interface member is the proxy's own (not the default member of the interface, which would answer null): the take-off sound of the hero reaches it.
        Assert.Same(player, host.SoundPlayer);
        Assert.Same(player, proxy.SoundPlayer);
    }
}
