#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c2 D4, test UJ-LOCK (docs/plan-e19-opcodes.md §1.2h.3.2, hygiene of E19.d2c1): the lock of the impulse taken
/// (<see cref="AlundraEntityScriptProxy.ZImpulseTaken"/>) goes down when nothing is pending at the end of the frame, so a switch seen at a tick then
/// cancelled by the entity's own script before the validation does not make the next switch lose its impulse (the binary gives one at every switch).
/// </summary>
public sealed class AlundraAnimationImpulseLockTests
{
    /// <summary>A tick program written by the test: it runs once per logic tick of the NPC and writes the target animation by the plan of the test.</summary>
    private sealed class ScriptedTargets : IEventProgramRunner
    {
        public required Func<int, AlundraEntityScriptProxy, uint?> TargetOfCall { get; init; }

        public bool Enabled { get; set; }

        public int Calls { get; private set; }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (!Enabled)
            {
                return;
            }

            Calls++;
            var target = TargetOfCall(Calls, entity);
            if (target.HasValue)
            {
                entity.TargetAnimationId = target.Value;
            }
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    [Fact]
    public void UJLOCK_ASwitchSeenAtATickThenCancelledByTheScript_DoesNotMakeTheNextSwitchLoseItsImpulse()
    {
        // Call 1 (tick 1 of the first frame): the script asks for the animation 3 (IZF 1360). Call 2 (tick 2): it puts the current target back. Call 3 (the
        // single tick of the second frame): it asks for 3 again.
        var runner = new ScriptedTargets
        {
            TargetOfCall = (call, npc) => call switch
            {
                1 => (uint)JumpNpcRig.ImpulseAnimation,
                2 => npc.CurrentAnimationId,
                3 => (uint)JumpNpcRig.ImpulseAnimation,
                _ => null,
            },
        };
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f), JumpNpcRig.Loop(JumpNpcRig.ImpulseAnimation, 1.6f, impulse: 1360) }, runner);
        runner.Enabled = true;

        rig.Update(0.04f); // two ticks.
        Assert.Equal(2, runner.Calls);
        Assert.Equal(0u, rig.Npc.TargetAnimationId); // the script put it back: nothing is pending at the end of the frame.
        Assert.False(rig.Npc.ZImpulseTaken, "the lock of the impulse taken stays down once nothing is pending");

        rig.Update(); // one tick: the switch to 3 again, which must give its impulse.
        Assert.Equal(3, runner.Calls);
        Assert.Equal(348160, rig.Npc.ForceZ); // IZF << 8, the impulse with no decay (not the decay that continues the first one).
        Assert.Equal(1360, rig.Npc.IsZForceApplied);
    }
}
