#nullable enable
using Xunit;
using Alundra.Ai.Objects;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T1 (docs/plan-e20-heros.md section 1.24.6, phase A, (a1)): the step of the counter of C76 (<c>0x8007C024</c>, the pushable pillar), a pure function of the counter (<c>+0x274</c>, a signed word), of the contact
/// (<c>hero.+0x130 == pillar</c>) and of the animation asked (<c>+0x88</c>). Each "without effect" case is next to the positive case it is the neighbour of.
/// </summary>
public sealed class PillarAiStepTests
{
    [Fact]
    public void A1_WithTheContactUnderTheThreshold_TheCounterGoesUpByOne_AndNothingElseHappens_AtTheThresholdTheSoundAndTheAnimationStart()
    {
        foreach (var counter in new[] { 0, 1, 29, 30, -1, int.MinValue })
        {
            foreach (var animation in new uint[] { 0, 1, 2 })
            {
                // without effect: no sound, no animation, no stop, the animation asked is not cleared.
                Assert.Equal(new PillarAi.CounterStep(counter + 1, false, false, false, false), PillarAi.Step(counter, true, animation));
            }
        }

        // the positive neighbour: at 31 the sound is asked and the animation 1 is posed; the counter stays at 31.
        Assert.Equal(new PillarAi.CounterStep(31, true, true, false, false), PillarAi.Step(31, true, 0));
        Assert.Equal(new PillarAi.CounterStep(31, true, true, false, false), PillarAi.Step(31, true, 1));
    }

    [Fact]
    public void A1_TheThresholdIs31Exactly_30IsStillCounting_32AndMoreAreAlreadyPushing_NoOverflowAtTheTop()
    {
        Assert.Equal(new PillarAi.CounterStep(31, false, false, false, false), PillarAi.Step(30, true, 0));
        Assert.Equal(new PillarAi.CounterStep(31, true, true, false, false), PillarAi.Step(31, true, 0));
        Assert.Equal(new PillarAi.CounterStep(32, true, true, false, false), PillarAi.Step(32, true, 0));
        Assert.Equal(new PillarAi.CounterStep(40, true, true, false, false), PillarAi.Step(40, true, 0));
        Assert.Equal(new PillarAi.CounterStep(int.MaxValue, true, true, false, false), PillarAi.Step(int.MaxValue, true, 0));
    }

    [Fact]
    public void A1_WithoutTheContact_TheCounterIsReset_TheAnimationIsCleared_AndTheStopOfTheSoundIsAskedOnlyIfTheAnimationWas1()
    {
        foreach (var counter in new[] { 0, 5, 30, 31, 40, -1, int.MaxValue })
        {
            // the positive neighbour: the animation was 1, the sound of the push is stopped.
            Assert.Equal(new PillarAi.CounterStep(0, false, false, true, true), PillarAi.Step(counter, false, 1));

            // without effect on the sound: any other animation asked leaves the sound alone (2, the maximum, 0), but the reset is the same.
            foreach (var animation in new uint[] { 0, 2, uint.MaxValue })
            {
                Assert.Equal(new PillarAi.CounterStep(0, false, false, false, true), PillarAi.Step(counter, false, animation));
            }
        }
    }
}
