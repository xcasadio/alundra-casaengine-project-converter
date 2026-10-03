#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 A10J (docs/plan-e19-opcodes.md section 1.2h.3.1, C4): map 10 (<c>Overworld 2,1-10</c>), B[20] (the hero walks through the basin, jumps the cliff with
/// <c>1A [2] @2451</c>, waits for the landing with <c>0x25 @2455</c>, walks on and reaches the corner), with Giles (C[74], record 98) jumping the same cliff
/// (<c>1A [3] @6389</c>, <c>0x25 @6393</c>). The hero's values are those of the binary, the water included (the basin slows him for 18 ticks, R7), in the DLL's
/// convention (no +1 of the binary on PosZ); Giles's are relational (D-E19-13 and D-E19-40: +-1 image, +-1 px). The arc starts from the portal arrival the
/// plan writes, with the real controller and the real prefabs; F0 is the image of the first execution of <c>0x0B @2441</c>.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraHeroJumpArcTests
{
    private const int B = ScriptHelper.ProgramBMap;
    private const int C = ScriptHelper.ProgramCTick;

    private static ArcSpec A10JSpec => new(
        "A10J", "Overworld", "Overworld 2,1-10", new[] { 1654, 203 }, 0, 0, 0, 450,
        RealController: true, Prefabs: true,
        Arrival: new ArcArrival(16515072, 61341696, 0, AlundraGameState.ResetAnimationId, 16));

    private sealed record Pose(int X, int Y, int Z, int IsOnGround, int CollidedWithEntityZ, int ForceZ, int ForceAdjusted, uint Animation);

    private static Pose PoseOf(AlundraEntityScriptProxy e) =>
        new(e.PosX, e.PosY, e.PosZ, e.IsOnGround, e.CollidedWithEntityZ, e.ForceZ, e.ForceAdjusted, e.TargetAnimationId);

    [Fact]
    public void A10J_TheHeroJumpsTheCliffOfMap10_TheWaterSlowsHim_GilesJumpsToo_TheSceneLeavesFor135()
    {
        using var arc = new ArcRun(A10JSpec);

        // The state of the hero at each instruction of B and C (the pose when the instruction ran), at the end of each image (the pose after the whole image),
        // and the state of Giles (record 98) at the end of each image.
        var atInstruction = new Dictionary<(int Slot, int Pc, int Frame), Pose>();
        var afterImage = new Dictionary<int, Pose>();
        var giles = new Dictionary<int, (Pose Pose, EntityStatus Status)>();
        AlundraEntityScriptProxy? gilesProxy = null;
        var gilesGoneAt = -1;
        arc.OnInstruction = t => atInstruction.TryAdd((t.Slot, t.Pc, t.Frame), PoseOf(arc.Hero));
        arc.OnFrame = () =>
        {
            afterImage[arc.Frame - 1] = PoseOf(arc.Hero);
            if (arc.EntityByRecord(98) is { } g)
            {
                gilesProxy = g;
                giles[arc.Frame - 1] = (PoseOf(g), g.Status);
            }
            else if (gilesProxy is not null && gilesGoneAt < 0)
            {
                gilesGoneAt = arc.Frame - 1; // the first image at the end of which record 98 is no longer listed (E19.r R3).
            }
        };

        arc.RunUntilPressingTheButtonOnEveryDialogueFrame(() => arc.Has(B, 2463, 0x53), "B[20] executes 0x53 @2463");

        // 1. Nothing is skipped but what the measure shows (no opcode of the scene is missing: the jump waits for its landing).
        AssertSkippedWithin(arc, new HashSet<(int, int)> { (0x90, 6406), (0x2B, 6413), (0x95, 6418), (0x90, 2689), (0x90, 2828) });

        var f0 = FrameOf(arc, B, 2441);
        Pose Hero(int slot, int pc, int image) => atInstruction[(slot, pc, image)];

        // 2. The walk to the basin and through it. `0x0B @2441` ends and `@2445`, `@2447` start at F0+50 in (16515072; 56070144; 0).
        Assert.Equal(f0 + 50, FrameOf(arc, B, 2445));
        Assert.Equal(f0 + 50, FrameOf(arc, B, 2447));
        var start = Hero(B, 2445, f0 + 50);
        Assert.Equal((16515072, 56070144, 0), (start.X, start.Y, start.Z));

        // PosX at F0+51 is 16594944, then +79872 per image (the water, 18 ticks) until 18032640 at F0+69, then +159744 (the walk of the animation 1).
        var x = Hero(B, 2447, f0 + 51).X;
        Assert.Equal(16594944, x);
        for (var image = f0 + 52; image <= f0 + 158; image++)
        {
            x += image <= f0 + 69 ? 79872 : 159744;
            Assert.True(x == Hero(B, 2447, image).X, $"PosX at F0+{image - f0}: {Hero(B, 2447, image).X}, {x} expected");
        }

        // `0x0B @2447` ends, `@2451` and `@2453` start at F0+158 in (32249856; 56016896; 0); the animation 2 is written by `@2451`.
        Assert.Equal(f0 + 158, FrameOf(arc, B, 2451));
        Assert.Equal(f0 + 158, FrameOf(arc, B, 2453));
        var edge = Hero(B, 2453, f0 + 158);
        Assert.Equal((32249856, 56016896, 0), (edge.X, edge.Y, edge.Z));
        Assert.Equal(2u, edge.Animation);

        // 3. The jump: PosZ after F0+159 to F0+175, PosX, the animation 44 from F0+160, in the air all along.
        int[] jump =
        {
            327680, 622592, 884736, 1114112, 1310720, 1474560, 1605632, 1703936, 1769472, 1802240, 1802240, 1769472, 1703936, 1605632, 1474560, 1310720, 1114112,
        };
        for (var i = 0; i < jump.Length; i++)
        {
            var pose = afterImage[f0 + 159 + i];
            Assert.True(jump[i] == pose.Z, $"PosZ after F0+{159 + i}: {pose.Z}, {jump[i]} expected");
            Assert.Equal(0, pose.IsOnGround);
            if (i >= 1)
            {
                Assert.Equal(44u, pose.Animation);
            }
        }

        Assert.Equal(new[] { 32409600, 32564736, 32715264 }, new[] { afterImage[f0 + 159].X, afterImage[f0 + 160].X, afterImage[f0 + 161].X });
        var xAir = afterImage[f0 + 161].X;
        for (var image = f0 + 162; image <= f0 + 176; image++)
        {
            xAir += 150528;
            Assert.True(xAir == afterImage[image].X, $"PosX after F0+{image - f0}: {afterImage[image].X}, {xAir} expected");
        }

        Assert.Equal(34973184, xAir);

        // 4. The wait of the landing: `37` ends at F0+162, `0x25 @2455` executes from F0+162 to F0+176 (15 times, 14 return 0) and returns 1 at F0+176 with the hero landed.
        Assert.Equal(f0 + 162, FrameOf(arc, B, 2455));
        Assert.Equal(Enumerable.Range(f0 + 162, 15).ToList(), FramesOf(arc, B, 2455, 0x25));
        var landing = Hero(B, 2455, f0 + 176);
        Assert.Equal((1, 1, 0, 1048576), (landing.CollidedWithEntityZ, landing.IsOnGround, landing.ForceZ, landing.Z));
        Assert.Equal(f0 + 176, FrameOf(arc, B, 2456));
        var walk = Hero(B, 2456, f0 + 176);
        Assert.Equal((34973184, 56016896, 1048576), (walk.X, walk.Y, walk.Z));

        // 5. `0x0B @2456` ends at F0+256 in (47748096; 56016896; 1048576); `@2460` and `@2462` start there.
        Assert.Equal(f0 + 256, FrameOf(arc, B, 2460));
        Assert.Equal(f0 + 256, FrameOf(arc, B, 2462));
        var corner = Hero(B, 2460, f0 + 256);
        Assert.Equal((47748096, 56016896, 1048576), (corner.X, corner.Y, corner.Z));

        // 6. `0x24 @2462` ends between F0+303 and F0+309 (the original: F0+308: the hero slides +0.75 px along the corner (29,48), E19.h4), the hero exactly at (47877120; 50790400),
        // PosZ 2097152, ForceAdjusted 1; `0x53 @2463` sends him to the map 135 in (30670848; 54001664; 1048576), direction 16.
        var leave = FrameOf(arc, B, 2463);
        Assert.InRange(leave, f0 + 303, f0 + 309);
        var at2463 = Hero(B, 2463, leave);
        Assert.Equal(47877120, at2463.X);
        Assert.Equal(50790400, at2463.Y);
        Assert.Equal((2097152, 1), (at2463.Z, at2463.ForceAdjusted));
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(135u, arrival.MapIndex);
        Assert.Equal((30670848, 54001664, 1048576), (arrival.PosX, arrival.PosY, arrival.PosZ));
        Assert.Equal(16u, arrival.DirectionId);

        // 7. Giles (record 98), the same cliff, relational (+-1 image, +-1 px): the impulse at F0+101 (`1A [3] @6389`), the landing at F0+119, `0x25 @6393` returns 1 at the
        // image of the landing or the next, `@6394` starts at x in [521.5; 522.5] px and ends at x in [732.3; 733.3] px; PosX is not a whole pixel at the landing
        // (no truncation). Then O-E19-29 (the pixel truncation of an NPC landing on a new terrain height, known and left for later): on the ramp (30,49) his height
        // changes at every tick, each landing rounds X down to 732.0 and makes him climb 2.0 px per image instead of 1.625 (F0+241 to F0+244), so he reaches
        // y = 775 at F0+253 and `0x24 @6400` returns at F0+255 (the plan derived F0+257 without the truncation), `0x19 @6410` at F0+266 (37 [10] returns at s+11),
        // and he is flagged for destruction by the native E at F0+267, the tick after his deactivation. They move back to F0+257, F0+268 and F0+269 with O-E19-29.
        static void Near(int expected, int actual, string what) => Assert.True(Math.Abs(expected - actual) <= 1, $"{what}: {actual}, {expected} (+-1) expected");
        Near(f0 + 101, FrameOf(arc, C, 6389), "Giles 1A [3] @6389");
        Near(f0 + 119, FramesOf(arc, C, 6393, 0x25)[^1], "Giles 0x25 @6393 returns 1");
        Near(f0 + 119, FrameOf(arc, C, 6394), "Giles @6394 starts");
        var gilesStart = giles[FrameOf(arc, C, 6394) - 1].Pose;
        Assert.InRange(gilesStart.X / 65536.0, 521.5, 522.5);
        Assert.NotEqual(0, gilesStart.X & 0xFFFF);
        var gilesEnd = giles[FrameOf(arc, C, 6398)].Pose;
        Assert.InRange(gilesEnd.X / 65536.0, 732.3, 733.3);
        // The mechanism of O-E19-29 itself: his Y step is 2.0 px (131072) at the images F0+241 to F0+244 (each landing on a new height rounds X down), then the 1.625 px (106496)
        // of the ramp; he goes towards the smaller Y, so the steps are negative.
        for (var image = f0 + 241; image <= f0 + 245; image++)
        {
            var step = giles[image].Pose.Y - giles[image - 1].Pose.Y;
            Assert.True(step == -(image <= f0 + 244 ? 131072 : 106496), $"Giles's Y step at F0+{image - f0}: {step}");
        }

        Near(f0 + 255, FrameOf(arc, C, 6401), "Giles 0x24 @6400 returns");
        Assert.InRange(giles[FrameOf(arc, C, 6401)].Pose.Y / 65536.0, 775 - 2.5, 775 + 2.5);
        Assert.Equal(0, giles[FrameOf(arc, C, 6401)].Pose.X & 0xFFFF); // the truncation of O-E19-29 on the ramp.
        Near(f0 + 266, FrameOf(arc, C, 6410), "Giles 0x19 @6410");
        // E19.r R3: flagged for destruction by the native E in his update at F0+267 and recycled at the end of that same image: the end-of-image sample never
        // sees FlagToDestroy; from F0+267 his proxy is Destroyed and record 98 is no longer listed.
        Assert.DoesNotContain(giles.Values, g => g.Status == EntityStatus.FlagToDestroy);
        Assert.NotNull(gilesProxy);
        Assert.Equal(EntityStatus.Destroyed, gilesProxy!.Status);
        Assert.Null(arc.EntityByRecord(98));
        Near(f0 + 267, gilesGoneAt, "Giles recycled (no longer listed)");

        AssertNoUnexpectedError(arc);
    }
}
