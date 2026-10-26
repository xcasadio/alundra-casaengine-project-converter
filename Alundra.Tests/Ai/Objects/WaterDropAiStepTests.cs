#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T1, phase A, (a2) and (a4) and the pure function of (a1) (docs/plan-e20-heros.md section 1.24.5, D-G2C3-1 to D-G2C3-3): A19 (<c>0x800619D0</c>, the low byte of the program C kept in <c>+0x275</c>), the pure step of C71 for each of its five states and for the states from 5 on,
/// and the restoration of the state 4 (nothing while the animation is not at its end, the whole restoration when it is). The goldens of the same function are in <see cref="WaterDropAiGoldenTests"/>.
/// </summary>
public sealed class WaterDropAiStepTests
{
    private const uint Flags = 0x32080;

    private static WaterDropAi.DropStep Step(byte state, ushort counter = 7, bool landed = false, bool animationDone = false, uint flags = Flags, bool targetIsTorch = false)
        => WaterDropAi.Step(state, counter, landed, animationDone, flags, targetIsTorch);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a2) A19.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(157, 157)]        // the record 28 of the map 92: the program C 157.
    [InlineData(0x1B1, 0xB1)]     // only the low byte is kept (+0x54 to +0x275 is a byte store).
    [InlineData(255, 255)]
    [InlineData(-1, 255)]
    [InlineData(0x7FFFFF01, 1)]
    public void A2_A19_KeepsTheLowByteOfTheProgramCInBytes1_AndTouchesNothingElse(int program, int expected)
    {
        var drop = CarryBench.Make("drop");
        drop.ProgramIndexes[2] = program;
        drop.Bytes[0] = 0xA1;
        drop.Bytes[1] = 0xA2;
        drop.Bytes[2] = 0xA3;
        drop.Bytes[3] = 0xA4;
        drop.Flags = 0x32080;
        drop.AIValues[1] = 0x1234;

        WaterDropAi.Load(drop);

        Assert.Equal(new byte[] { 0xA1, (byte)expected, 0xA3, 0xA4 }, drop.Bytes);
        Assert.Equal(program, drop.ProgramIndexes[2]);
        Assert.Equal((0x32080u, (short)0x1234), (drop.Flags, drop.AIValues[1]));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a1) the pure step.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheState0_ArmsTheWait_State1AndAForty()
    {
        var step = Step(0);

        Assert.Equal((1, (ushort)40, (uint?)null, Flags, false, false), (step.State, step.Counter, step.Animation, step.Flags, step.Restore, step.Extinguish));
    }

    [Fact]
    public void TheState1_CountsDown_AndAtZeroStartsTheFall_Animation1State2AndGravity()
    {
        var counting = Step(1, counter: 2);
        Assert.Equal((1, (ushort)1, (uint?)null, Flags), (counting.State, counting.Counter, counting.Animation, counting.Flags));

        var last = Step(1, counter: 1);
        Assert.Equal((2, (ushort)0, (uint?)1, Flags | 0x100), (last.State, last.Counter, last.Animation, last.Flags));

        // a counter at zero wraps to 0xFFFF (a 16-bit decrement): it does not start the fall.
        var wrapped = Step(1, counter: 0);
        Assert.Equal((1, (ushort)0xFFFF, (uint?)null, Flags), (wrapped.State, wrapped.Counter, wrapped.Animation, wrapped.Flags));
    }

    [Fact]
    public void TheState2_WaitsForTheLanding_AndLandsOnTheAnimation2()
    {
        var falling = Step(2, landed: false);
        Assert.Equal((2, (uint?)null), (falling.State, falling.Animation));

        var landed = Step(2, landed: true);
        Assert.Equal((3, (uint?)2, Flags), (landed.State, landed.Animation, landed.Flags));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheState3_AdvancesAndTakesCollidableOff_WithOrWithoutATorch_BeforeTheTestOfTheTorch(bool torch)
    {
        var step = Step(3, targetIsTorch: torch);

        Assert.Equal((4, Flags & ~0x80u, (uint?)null, false, torch), (step.State, step.Flags, step.Animation, step.Restore, step.Extinguish));
    }

    [Fact]
    public void TheState4_WaitsForTheEndOfTheAnimation_AndThenRestoresTheDrop()
    {
        var waiting = Step(4, flags: 0x32100, animationDone: false);
        Assert.Equal((4, 0x32100u, false), (waiting.State, waiting.Flags, waiting.Restore));

        var done = Step(4, flags: 0x32100, animationDone: true);
        Assert.Equal((0, 0x32080u, true, (uint?)0), (done.State, done.Flags, done.Restore, done.Animation));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(255)]
    public void TheStatesFrom5On_DoNothing_NotEvenTheArmingOfTheState0(byte state)
    {
        var step = Step(state, counter: 9, landed: true, animationDone: true, flags: 0x32180, targetIsTorch: true);

        Assert.Equal((state, (ushort)9, (uint?)null, 0x32180u, false, false), (step.State, step.Counter, step.Animation, step.Flags, step.Restore, step.Extinguish));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a4) the restoration, on the proxy.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static EntityScriptProxy Restorable(int animationDone)
    {
        var drop = CarryBench.Make("drop");
        drop.SpriteType = WaterDropAi.SpriteType;
        drop.Bytes[0] = 4;
        drop.Bytes[1] = 157;
        drop.Flags = 0x32100;
        drop.ForceZ = -32768;
        drop.PosZ = 3 << 16;
        drop.TargetAnimationId = 2;
        drop.ProgramIndexes[2] = 0;
        drop.ForceResetAnimationFlag = animationDone;
        return drop;
    }

    [Fact]
    public void A4_TheState4_WithTheAnimationNotAtItsEnd_DoesNothing()
    {
        var drop = Restorable(animationDone: 0);

        WaterDropAi.Tick(drop, new PickupHost());

        Assert.Equal((4, 0x32100u, -32768, 3 << 16, 2u, 0), (drop.Bytes[0], drop.Flags, drop.ForceZ, drop.PosZ, drop.TargetAnimationId, drop.ProgramIndexes[2]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]   // C71 tests "not zero", unlike E20 which tests "equal to 1".
    [InlineData(-1)]
    public void A4_TheState4_WithTheAnimationAtItsEnd_RestoresTheDrop_160PxHigher_AndRearmsItsScript(int animationDone)
    {
        var drop = Restorable(animationDone);

        WaterDropAi.Tick(drop, new PickupHost());

        Assert.Equal((0, 0x32080u, 0, (3 << 16) + (160 << 16), 0u, 157), (drop.Bytes[0], drop.Flags, drop.ForceZ, drop.PosZ, drop.TargetAnimationId, drop.ProgramIndexes[2]));
        Assert.Equal(animationDone, drop.ForceResetAnimationFlag);
    }
}
