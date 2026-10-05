#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 R1 to R3 (docs/plan-e19-opcodes.md §1.2h.3.1), tests UJ-1 to UJ-10, UJ-DIR, UJ-0x8000 and UJ-CLAMP: an animation of impulse
/// (<c>IsZForceApplied</c>, +0xF8) throws the entity up at the TICK of the switch, as the binary's <c>UpdateAnimation</c> and physics do; never at an
/// appearance or an arrival; the NPC's vertical step takes the impulse (<c>IZF &lt;&lt; 8</c>, no decay that tick), the stop marker <c>0x8000</c> of the iron
/// grids, and bounds its force on both sides. Montage of <see cref="JumpNpcRig"/> (annex A.1 of the plan); every expected value was written by hand
/// in the plan before the code - a value that the measure contradicts is a stop, never a re-pin.
/// </summary>
public sealed class AlundraAnimationImpulseTests
{
    /// <summary>PosZ after the updates 1 to 22 of an NPC with gravity (128) thrown by IZF 1360 (UJ-1).</summary>
    private static readonly int[] Flight1360 =
    {
        348160, 663552, 946176, 1196032, 1413120, 1597440, 1748992, 1867776, 1953792, 2007040, 2027520, 2015232, 1970176, 1892352, 1781760, 1638400,
        1462272, 1253376, 1011712, 737280, 430080, 90112,
    };

    /// <summary>PosZ after the updates 1 to 20 for IZF 1280 (UJ-1b); the update 21 lands.</summary>
    private static readonly int[] Flight1280 =
    {
        327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720,
        1114112, 884736, 622592, 327680,
    };

    private static void AssertFlight(JumpNpcRig rig, int[] flight, int fromUpdate = 1)
    {
        for (var i = 0; i < flight.Length; i++)
        {
            rig.Update();
            Assert.True(flight[i] == rig.Npc.PosZ, $"update {fromUpdate + i}: PosZ {rig.Npc.PosZ}, {flight[i]} expected");
        }
    }

    [Fact]
    public void UJ1_ANpcOfGravityThrownByTheAnimationOfImpulse_FliesThe22TicksOfTheList_AndLandsAtTheUpdate23()
    {
        var rig = JumpNpcRig.Build(impulse: 1360);
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;

        for (var i = 0; i < Flight1360.Length; i++)
        {
            rig.Update();
            var n = i + 1;
            Assert.True(Flight1360[i] == rig.Npc.PosZ, $"update {n}: PosZ {rig.Npc.PosZ}, {Flight1360[i]} expected");
            Assert.Equal(0, rig.Npc.IsOnGround);
            Assert.Equal(0, rig.Npc.CollidedWithEntityZ);
            Assert.Equal(n == 1 ? 1360 : 0, rig.Npc.IsZForceApplied); // 1360 after the update 1, 0 after the 2 (one impulse, at the tick of the switch).
        }

        rig.Update(); // 23
        Assert.Equal(0, rig.Npc.PosZ);
        Assert.Equal(0, rig.Npc.ForceZ);
        Assert.Equal(1, rig.Npc.IsOnGround);
        Assert.Equal(1, rig.Npc.CollidedWithEntityZ);
    }

    [Fact]
    public void UJ1b_AnImpulseOf1280_LandsAtTheUpdate21ByTheLessOrEqualOfTheDll_WhereTheBinaryLandsAt22_TheCollisionFlagComesAt22()
    {
        var rig = JumpNpcRig.Build(impulse: 1280);
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;
        AssertFlight(rig, Flight1280);

        // The update 21. The binary has ForceZ -327680 here and lands one tick later (D-E19-40): its test is strict, 327680 - 327680 is not below 0. The
        // DLL keeps its `<=`: it lands now, with ForceZ 0 - and CollidedWithEntityZ, raised only on the strict test of R5 a, stays 0 until the update 22.
        rig.Update();
        Assert.Equal(0, rig.Npc.PosZ);
        Assert.Equal(0, rig.Npc.ForceZ);
        Assert.Equal(1, rig.Npc.IsOnGround);
        Assert.Equal(0, rig.Npc.CollidedWithEntityZ);

        rig.Update(); // 22
        Assert.Equal(1, rig.Npc.CollidedWithEntityZ);
    }

    [Fact]
    public void UJ1c_ANpcWalkingWhileItJumps_LosesTheFractionOfItsXAtTheLanding_PinnedAsTheKnownGapOfOE19_29()
    {
        // The jumper walks east at 0.375 px per tick (speed 64, direction 24: 24576 per tick) from update 2 (the motion reads the CURRENT animation, one frame
        // behind the target, D-E19-13). At the landing (update 21) the last tick in the air was 5 px high: the magnet of the engine (4 px) does not take it, and
        // the landing goes through PushLogicalPositionToRoot, which truncates X to the pixel. The binary keeps the fraction: gap pinned and commented
        // (O-E19-29); a future fix of O-E19-29 will make this test change on purpose.
        var rig = JumpNpcRig.Build(impulse: 1280);
        rig.SetAnimSet(new AnimSetEntry { Anim = JumpNpcRig.ImpulseAnimation, Speed = 64, Acceleration = 0, IsZForceApplied = 1280 });
        rig.Npc.TargetDirection = 24;
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;
        for (var i = 0; i < 20; i++)
        {
            rig.Update();
        }

        Assert.Equal(327680, rig.Npc.PosZ); // 5 px up at the last tick in the air: beyond the 4 px of the magnet.
        var beforeLanding = ContactWorld.Root(rig.Npc).X;
        Assert.NotEqual(MathF.Floor(beforeLanding), beforeLanding); // a fraction of pixel at the take-off.

        rig.Update(); // 21: the landing.
        Assert.Equal(0, rig.Npc.PosZ);
        Assert.Equal(1, rig.Npc.IsOnGround);
        var landed = ContactWorld.Root(rig.Npc).X;
        Assert.Equal(MathF.Floor(landed), landed); // a whole number of pixels: the fraction is gone (the binary keeps it) ...
        Assert.True(landed <= beforeLanding + rig.Npc.ForceX / 65536f && landed > beforeLanding + rig.Npc.ForceX / 65536f - 1f, $"... truncated from {beforeLanding + rig.Npc.ForceX / 65536f}, got {landed}");
    }

    [Fact]
    public void UJ5_UnderCatchUp_TwoTicksInTheFrame_TheImpulseIsGivenOnce()
    {
        var rig = JumpNpcRig.Build(impulse: 1360);
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;

        var expected = new[] { 663552, 1196032, 1597440 }; // the ticks 2, 4 and 6 of the list.
        for (var call = 0; call < expected.Length; call++)
        {
            rig.Update(0.04f);
            Assert.Equal(expected[call], rig.Npc.PosZ); // an impulse given twice would give 696320 at the first call.
            if (call == 0)
            {
                Assert.Equal(0, rig.Npc.IsZForceApplied);
            }
        }
    }

    [Fact]
    public void UJ8_ASwitchValidatedOnAFrameWithoutTick_GivesItsImpulseAtTheFirstTickThatFollows_WithoutSprite()
    {
        var rig = JumpNpcRig.Build(impulse: 1360);
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;

        rig.Update(0.001f); // no logic tick: the end of the frame validates the switch.
        Assert.Equal((uint)JumpNpcRig.ImpulseAnimation, rig.Npc.CurrentAnimationId);
        Assert.Equal(0, rig.Npc.PosZ);

        rig.Update();
        Assert.Equal(348160, rig.Npc.PosZ);
        Assert.Equal(1360, rig.Npc.IsZForceApplied);
        AssertFlight(rig, Flight1360.Skip(1).ToArray(), fromUpdate: 2);
    }

    [Fact]
    public void UJ8_ASwitchValidatedOnAFrameWithoutTick_GivesItsImpulseAtTheFirstTickThatFollows_WithTheSpriteClock()
    {
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f), JumpNpcRig.Loop(JumpNpcRig.ImpulseAnimation, 1.6f, impulse: 1360) });
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;

        rig.Update(0.001f);
        Assert.Equal((uint)JumpNpcRig.ImpulseAnimation, rig.Npc.CurrentAnimationId);
        Assert.Equal(0, rig.Npc.PosZ);

        rig.Update();
        Assert.Equal(348160, rig.Npc.PosZ);
        AssertFlight(rig, Flight1360.Skip(1).ToArray(), fromUpdate: 2);
    }

    [Fact]
    public void UJ9_TheEndOfAChainGivesTheImpulseOfTheChainedAnimationAtTheTickOfTheEnd_ThenTheList()
    {
        // A (animation 4, no impulse, a Once that chains to B) then B (animation 3, impulse 1360).
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f), JumpNpcRig.Chain(4, 0.1f, to: JumpNpcRig.ImpulseAnimation), JumpNpcRig.Loop(JumpNpcRig.ImpulseAnimation, 1.6f, impulse: 1360) });
        rig.Npc.TargetAnimationId = 4;

        var impulseUpdate = -1;
        uint targetBeforeTheImpulseUpdate = 0;
        for (var update = 1; update <= 60 && impulseUpdate < 0; update++)
        {
            var targetBefore = rig.Npc.TargetAnimationId;
            rig.Update();
            if (rig.Npc.IsZForceApplied != 0)
            {
                impulseUpdate = update;
                targetBeforeTheImpulseUpdate = targetBefore;
            }
        }

        // The impulse comes at the very tick where the clock raises the end of A (the target was still A before this update), not one tick later.
        Assert.Equal(4u, targetBeforeTheImpulseUpdate);

        Assert.True(impulseUpdate > 2, "the chained impulse never came");
        Assert.Equal(1360, rig.Npc.IsZForceApplied);
        Assert.Equal((uint)JumpNpcRig.ImpulseAnimation, rig.Npc.TargetAnimationId); // the clock raised the end of A and chained to B at this very tick.
        Assert.Equal(348160, rig.Npc.PosZ);
        AssertFlight(rig, Flight1360.Skip(1).ToArray(), fromUpdate: impulseUpdate + 1);
    }

    private sealed class RelaunchRunner : IEventProgramRunner
    {
        private readonly AlundraEventProgramRunner _inner;

        public RelaunchRunner(AlundraEventProgramRunner inner) => _inner = inner;

        /// <summary>The tick program starts when the test says (after the settling update of the montage).</summary>
        public bool Enabled { get; set; }

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (Enabled)
            {
                _inner.RunScript(entity, programSlot);
            }
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    [Fact]
    public void UJ10_AZeroX1CRelaunchOfTheAnimationGivesTheImpulseAgain()
    {
        // The tick program: `1A 03 ; 1C 01 ; 1C 01 ; FF`: the animation 3 (a Hold of 30 ticks, impulse 1360) starts, the first 1C counts its end and relaunches it
        // (0x1C: CurrentAnimationId = ~TargetAnimationId), the second waits for the end of the relaunched animation.
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0 },
            EventCodesBTable = new[] { 0, 0 },
            EventCodesCTable = new[] { 0, 0 },
            Codes = new[] { 0x01, 0x1A, JumpNpcRig.ImpulseAnimation, 0x1C, 0x01, 0x1C, 0x01, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        var gated = new RelaunchRunner(runner);
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f), JumpNpcRig.Hold(JumpNpcRig.ImpulseAnimation, 0.6f, impulse: 1360) }, gated);
        gated.Enabled = true;

        var impulseUpdates = new List<int>();
        var posZ = new List<int>();
        for (var update = 1; update <= 90; update++)
        {
            rig.Update();
            posZ.Add(rig.Npc.PosZ);
            if (rig.Npc.IsZForceApplied != 0)
            {
                impulseUpdates.Add(update);
            }
        }

        Assert.Equal(1, impulseUpdates[0]); // the switch of the program's own 1A.
        Assert.True(impulseUpdates.Count >= 2, "the relaunch gave no second impulse");
        var relaunch = impulseUpdates[1];
        Assert.True(relaunch > 23, $"the relaunch at {relaunch} comes after the first landing");
        for (var i = 0; i < Flight1360.Length; i++)
        {
            Assert.True(Flight1360[i] == posZ[relaunch - 1 + i], $"update {relaunch + i}: PosZ {posZ[relaunch - 1 + i]}, {Flight1360[i]} expected");
        }
    }

    [Fact]
    public void UJ10b_ARelaunchByX1COfTheAppearanceAnimation_ClearsTheAppearanceFlag_AndGivesItsImpulse()
    {
        // The montage of UJ-10, plus the appearance flag raised on the animation 3 right after the first landing (the gesture of UJ-6b). Production never
        // reaches this state (the flag drops at an entity's first validation); the montage forces it to pin that the 0x1C relaunch is an ordinary switch
        // and clears the flag (E19.d2c1 R2), so that the clock gives the impulse at the relaunch: with the flag still up TakeZImpulse returns early.
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0 },
            EventCodesBTable = new[] { 0, 0 },
            EventCodesCTable = new[] { 0, 0 },
            Codes = new[] { 0x01, 0x1A, JumpNpcRig.ImpulseAnimation, 0x1C, 0x01, 0x1C, 0x01, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        var gated = new RelaunchRunner(runner);
        var rig = JumpNpcRig.BuildWithSprite(new[] { JumpNpcRig.Loop(0, 1.6f), JumpNpcRig.Hold(JumpNpcRig.ImpulseAnimation, 0.6f, impulse: 1360) }, gated);
        gated.Enabled = true;

        var impulseUpdates = new List<int>();
        var posZ = new List<int>();
        var flag = new List<bool>();
        for (var update = 1; update <= 90; update++)
        {
            rig.Update();
            posZ.Add(rig.Npc.PosZ);
            if (rig.Npc.IsZForceApplied != 0)
            {
                impulseUpdates.Add(update);
            }

            if (update == 23)
            {
                rig.Npc.SpawnAnimationActive = true;
                rig.Npc.SpawnAnimationId = JumpNpcRig.ImpulseAnimation;
            }

            flag.Add(rig.Npc.SpawnAnimationActive);
        }

        Assert.Equal(1, impulseUpdates[0]);
        Assert.True(flag[30], "nothing is pending from the update 24 to 31: no validation clears the flag"); // flag[i] is the flag after the update i + 1.
        Assert.Equal(32, impulseUpdates[1]); // the 1C of the update 32 relaunches; the flag is down, so the clock gives the impulse at once (63 with the flag left up).
        Assert.False(flag[31]);
        for (var i = 0; i < Flight1360.Length; i++)
        {
            Assert.True(Flight1360[i] == posZ[31 + i], $"update {32 + i}: PosZ {posZ[31 + i]}, {Flight1360[i]} expected");
        }
    }

    [Fact]
    public void UJDir_AChangeOfTheDirectionRowGivesTheImpulseAgain()
    {
        var rig = JumpNpcRig.Build(impulse: 1360);
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;
        for (var i = 0; i < 23; i++)
        {
            rig.Update(); // the whole jump: landed at the update 23.
        }

        Assert.Equal(0, rig.Npc.PosZ);
        Assert.Equal(0, rig.Npc.AnimationDirection);

        rig.Npc.TargetDirection = 16; // up: another row of the direction table (0 -> 1), same animation 3.
        rig.Update();
        Assert.Equal(1360, rig.Npc.IsZForceApplied);
        Assert.Equal(348160, rig.Npc.PosZ);
        Assert.Equal(1, rig.Npc.AnimationDirection);
        AssertFlight(rig, Flight1360.Skip(1).ToArray(), fromUpdate: 2);
    }

    [Fact]
    public void UJ0x8000_TheStopMarkerOfTheIronGridsHoldsAnEntityWithoutGravity_AndIsAnOrdinaryImpulseWithGravity()
    {
        // IZF stored as the signed integer -32768: the rule compares the 16 low bits. No gravity, animation 3 of impulse 256 then animation 0 of impulse -32768.
        var rig = JumpNpcRig.Build(impulse: 256, gravity: false);
        rig.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, IsZForceApplied = -32768 });
        rig.Npc.TargetAnimationId = JumpNpcRig.ImpulseAnimation;
        var z = 0;
        for (var tick = 1; tick <= 8; tick++) // 8 px up: beyond the 4 px magnet of the engine, which would take a motionless entity back to the ground.
        {
            rig.Update();
            z += 65536;
            Assert.Equal(65536, rig.Npc.ForceZ); // IZF 256 << 8, no decay: 1 px per tick.
            Assert.Equal(z, rig.Npc.PosZ);
        }

        // The marker: animation 0 written. Without the rule the force would be -8388608 and the entity would fall to the ground; with it, ForceZ 0 and PosZ held.
        rig.Npc.TargetAnimationId = 0;
        rig.Update();
        Assert.Equal(0, rig.Npc.ForceZ);
        Assert.Equal(z, rig.Npc.PosZ);
        for (var tick = 0; tick < 5; tick++)
        {
            rig.Update();
            Assert.Equal(0, rig.Npc.ForceZ);
            Assert.Equal(z, rig.Npc.PosZ);
        }

        // With gravity, at rest on the ground: the same animation 0 written gives the ordinary impulse -8388608 at the tick of the change, which lands at once
        // (PosZ 0, ForceZ 0), CollidedWithEntityZ 1 by R5 a: the rule does not play with gravity, as in the binary.
        var withGravity = JumpNpcRig.Build(impulse: 256, gravity: true);
        withGravity.SetAnimSet(new AnimSetEntry { Anim = 0, Speed = 0, IsZForceApplied = -32768 });
        withGravity.Npc.CurrentAnimationId = JumpNpcRig.ImpulseAnimation; // the animation 3 is the one playing; the target 0 is written: a switch is pending.
        withGravity.Update(); // the tick of the change: IZF -32768 << 8 = -8388608, landed at once.
        Assert.Equal(0, withGravity.Npc.PosZ);
        Assert.Equal(0, withGravity.Npc.ForceZ);
        Assert.Equal(1, withGravity.Npc.CollidedWithEntityZ);
        Assert.Equal(-8388608, withGravity.Npc.TickForceZ); // IZF -32768 << 8, no decay, no bound (0x80036AF4-0x80036B04).
    }

    [Fact]
    public void UJClamp_TheForceOfAnNpcWithGravityIsBoundedOnBothSides()
    {
        var rig = JumpNpcRig.Build();
        rig.Npc.Flags &= ~EntityFlags.Collidable; // nothing to land on: the force alone.
        rig.Npc.ForceZ = 1966080; // posed before the tick: decays by 32768 to 1933312, bounded to 4096 << 8 = 1048576.
        rig.Update();
        Assert.Equal(1048576, rig.Npc.ForceZ); // the DLL before E19.d2c1 gave 1933312.
    }
}
