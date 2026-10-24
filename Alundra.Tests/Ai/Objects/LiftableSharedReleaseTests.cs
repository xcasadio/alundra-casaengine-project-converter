#nullable enable
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Tests.Hero;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c1 T1 (docs/plan-e20-heros.md section 1.24.5, phase A, (l2), D-G2C1-8): the release of the carried half, shared by C23 and C4 (the same table <c>0x80027400</c> / <c>0x800270C0</c>) with one flags policy per handler: C23 writes <c>(flags | 0x30) &amp; ~0x80</c> for every
/// code; C4 leaves the flags for the codes 1 to 3 (an object it throws stays collidable) and, for the code -1 (a wound, deep water, a death), writes <c>(flags | 0x10) &amp; ~0x80</c> only when its native E code (<c>+0x80</c>) is 2. The program C23 behaves as before (its pins stay
/// in <see cref="LiftableAiTests"/>).
/// </summary>
public sealed class LiftableSharedReleaseTests
{
    private const uint CrateFlags = 0x3323E4;

    private static EntityScriptProxy Carried(int throwKind, int nativeECode = 0, uint flags = CrateFlags)
    {
        var (hero, item) = CarryBench.Pair(new CarryRules.Triple(0, 0, 32 * CarryBench.Px));
        item.Flags = flags;
        item.Flags2 = throwKind;
        item.TargetAnimationId = 7;
        item.RidingEntity = hero.LogicContextEntity;
        item.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = nativeECode;
        return item;
    }

    [Theory]
    [InlineData(1, 3u)]
    [InlineData(2, 1u)]
    [InlineData(3, 4u)]
    [InlineData(-1, 3u)]
    public void C23_TheSharedRelease_WritesTheFlagsOfC23_WhateverTheNativeCode_AndTheProgramC23GivesTheSame(int code, uint animation)
    {
        foreach (var nativeECode in new[] { 0, 2 })
        {
            var shared = Carried(code, nativeECode);
            var program = Carried(code, nativeECode);

            LiftableAi.TickCarried(shared, LiftableAi.ReleaseFlagsPolicy.C23);
            LiftableAi.Tick(program, new CarryBench.RecordingWorld());

            Assert.Equal((animation, 0, 0x332374u, null, null), (shared.TargetAnimationId, shared.Flags2, shared.Flags, shared.PlatformEntity, shared.RidingEntity));
            Assert.Equal((shared.TargetAnimationId, shared.Flags2, shared.Flags), (program.TargetAnimationId, program.Flags2, program.Flags));
        }
    }

    [Theory]
    [InlineData(1, 3u)]
    [InlineData(2, 1u)]
    [InlineData(3, 4u)]
    public void C4_TheCodes1To3_ReleaseWithTheTableAndLeaveTheFlagsAlone_EvenWithTheNativeCode2_NextToC23ThatWritesThem(int code, uint animation)
    {
        var c4 = Carried(code, nativeECode: 2);
        var c23 = Carried(code, nativeECode: 2);

        LiftableAi.TickCarried(c4, LiftableAi.ReleaseFlagsPolicy.C4);
        LiftableAi.TickCarried(c23, LiftableAi.ReleaseFlagsPolicy.C23);

        Assert.Equal((animation, 0, CrateFlags, null, null), (c4.TargetAnimationId, c4.Flags2, c4.Flags, c4.PlatformEntity, c4.RidingEntity));
        Assert.Equal(0x332374u, c23.Flags);
    }

    [Theory]
    [InlineData(2, 0x3323E4u, 0x332364u)]
    [InlineData(2, 0x80u, 0x10u)]
    [InlineData(2, 0xFFFFFFFFu, 0xFFFFFF7Fu)]
    [InlineData(0, 0x3323E4u, 0x3323E4u)]
    [InlineData(1, 0x3323E4u, 0x3323E4u)]
    [InlineData(3, 0x3323E4u, 0x3323E4u)]
    public void C4_TheCodeMinus1_WritesFlagsOr0x10AndNot0x80_OnlyWhenTheNativeCodeIs2(int nativeECode, uint flags, uint expected)
    {
        var item = Carried(-1, nativeECode, flags);

        LiftableAi.TickCarried(item, LiftableAi.ReleaseFlagsPolicy.C4);

        Assert.Equal((3u, 0, expected, null), (item.TargetAnimationId, item.Flags2, item.Flags, item.PlatformEntity));
    }

    [Fact]
    public void TheSharedRelease_AHeldObjectKeepsItsLinks_AnUnlistedCodeDoesNotRelease_ForBothPolicies()
    {
        foreach (var policy in new[] { LiftableAi.ReleaseFlagsPolicy.C23, LiftableAi.ReleaseFlagsPolicy.C4 })
        {
            var held = Carried(0);
            LiftableAi.TickCarried(held, policy);
            Assert.Equal((0u, CrateFlags, 0), (held.TargetAnimationId, held.Flags, held.Flags2));
            Assert.NotNull(held.PlatformEntity);
            Assert.NotNull(held.RidingEntity);

            var unlisted = Carried(4);
            LiftableAi.TickCarried(unlisted, policy);
            Assert.Equal((7u, CrateFlags, 4), (unlisted.TargetAnimationId, unlisted.Flags, unlisted.Flags2));
            Assert.NotNull(unlisted.PlatformEntity);
        }

        // and a code that releases does clear the links, next to the held object above.
        var thrown = Carried(1);
        LiftableAi.TickCarried(thrown, LiftableAi.ReleaseFlagsPolicy.C4);
        Assert.Null(thrown.PlatformEntity);
    }
}
