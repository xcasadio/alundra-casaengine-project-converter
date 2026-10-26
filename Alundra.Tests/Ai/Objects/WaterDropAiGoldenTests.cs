#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c3 T1, phase A, (a1) (docs/plan-e20-heros.md section 1.24.5, D-G2C3-1): the 22 goldens of C71 (<c>0x8007B834</c>, 89 words) that T0 made from the real code of the binary, on 11 classes (every state, the state 3 with and without a torch, the state at 5 and over), and the four vectors of the
/// state 2 whose pair (<c>CollidedWithEntityZ</c>, <c>IsOnGround</c>) is (1, 0), (0, 1), (1, 1), (0, 0): on the head of the hero and on a posed torch the two flags rise at the same tick, only these vectors tell them apart. Each vector is played through <see cref="WaterDropAi.Tick"/>:
/// every word the real code wrote is compared, and every word it did not write is compared with what was read (a stray write shows).
/// </summary>
public sealed class WaterDropAiGoldenTests
{
    private const string GoldenSha256 = "40071ab5e80cbf4dae6bf6d33bcf067958abb78cf918f0847f312c9ffac6214b";

    private const string State2Sha256 = "b9f8585c363d619b5bf6dadaff41797674b5956b21e4880eec9aecb1e8f416ef";

    private static string Sha(string relative)
    {
        var text = File.ReadAllText(AnnexFiles.WaterDropAnnex(relative)).Replace("\r\n", "\n");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    [Fact]
    public void TheGoldenFiles_AreTheOnesT0Pinned_22VectorsOver11Classes_AndTheFourVectorsOfTheState2()
    {
        Assert.Equal(GoldenSha256, Sha("e20-g2c3-disc/g2c3/replay/c71_goldens.json"));
        Assert.Equal(State2Sha256, Sha("e20-g2c3/vectors/a1_state2_flags.json"));

        var goldens = WaterDropVector.Goldens();
        Assert.Equal(22, goldens.Count);
        Assert.Equal(11, goldens.Select(v => v.Class).Distinct().Count());
        Assert.Equal(4, WaterDropVector.State2().Count);
    }

    /// <summary>Plays one vector and compares every word.</summary>
    private static void Replay(WaterDropVector v)
    {
        var layout = v.Build();
        var drop = layout.Drop;
        var why = $"{v.Class} (vector {v.Index})";

        WaterDropAi.Tick(drop, layout.Host);

        Assert.True(v.Expected("0x274") % 256 == drop.Bytes[0], $"{why}: the state");
        Assert.True(v.DropIn("0x275") == drop.Bytes[1], $"{why}: the byte +0x275 (the index kept by A19) is only read");
        Assert.True(WaterDropVector.FreeByte == drop.Bytes[2] && WaterDropVector.FreeByte == drop.Bytes[3], $"{why}: the other bytes of the word");
        Assert.True(v.ExpectedCounter == drop.AIValues[1], $"{why}: the counter +0x282");
        Assert.True(WaterDropVector.FreeCounterLow == drop.AIValues[0], $"{why}: the other half of the word +0x280");
        Assert.True(v.Expected("0x6C") == drop.Flags, $"{why}: the flags");
        Assert.True(v.Expected("0x88") == drop.TargetAnimationId, $"{why}: the animation asked");
        Assert.True(v.Expected("0xB8") == unchecked((uint)drop.ForceZ), $"{why}: ForceZ");
        Assert.True(v.Expected("0x11C") == unchecked((uint)drop.PosZ), $"{why}: PosZ");
        Assert.True(v.Expected("0x54") == unchecked((uint)drop.ProgramIndexes[2]), $"{why}: the program C asked");
        Assert.True(v.DropIn("0xAC") == unchecked((uint)drop.ForceResetAnimationFlag), $"{why}: the end of the animation is only read");
        Assert.True(v.DropIn("0x140") == unchecked((uint)drop.CollidedWithEntityZ) && v.DropIn("0x144") == unchecked((uint)drop.IsOnGround), $"{why}: the contact flags are only read");
        Assert.True(v.DropIn("0x10") == (uint)(int)drop.Status, $"{why}: the status of the drop");

        // the entities the drop does not own: the others. Only the target of a torch hit changes, to status 3 and to the mark 1.
        foreach (var slot in new[] { 5, 6, 7 })
        {
            var candidate = layout.Slots[slot];
            if (v.IsTorchHit && slot == v.TargetSlot)
            {
                Assert.True(EntityStatus.Deactivated == candidate.Status, $"{why}: the torch is put out (status 3)");
                Assert.True(1 == candidate.Bytes[0], $"{why}: the torch carries the mark of the drop");
                Assert.True(!v.CandidateChanged(slot, "0x10") || v.CandidateOut(slot, "0x10") == 3, $"{why}: the status the real code wrote");
                Assert.True(!v.CandidateChanged(slot, "0x274") || v.CandidateOut(slot, "0x274") % 256 == 1, $"{why}: the mark the real code wrote");
            }
            else
            {
                Assert.True(EntityStatus.Normal == candidate.Status, $"{why}: the candidate {slot} is left alone (status)");
                Assert.True(WaterDropVector.FreeByte == candidate.Bytes[0], $"{why}: the candidate {slot} is left alone (mark)");
            }
        }
    }

    [Fact]
    public void TheTwentyTwoGoldens_AreFollowedWordForWord_EveryStateTheTorchHitTheOtherTypeAndTheStateOver4()
    {
        var hits = 0;
        foreach (var v in WaterDropVector.Goldens())
        {
            Replay(v);
            hits += v.IsTorchHit ? 1 : 0;
        }

        Assert.Equal(2, hits);
    }

    [Fact]
    public void TheFourVectorsOfTheState2_FollowTheOracle_EitherFlagLandsTheDropAndNeitherDoesNot()
    {
        var vectors = WaterDropVector.State2();
        foreach (var v in vectors)
        {
            Replay(v);
        }

        // (CollidedWithEntityZ, IsOnGround) = (1, 0), (0, 1), (1, 1) land (animation 2, state 3); (0, 0) does nothing.
        var landed = vectors.Select(v => (v.DropIn("0x140"), v.DropIn("0x144"), v.DropOutHas("0x274"))).ToArray();
        Assert.Equal(new[] { (1u, 0u, true), (0u, 1u, true), (1u, 1u, true), (0u, 0u, false) }, landed);
    }

    [Fact]
    public void TheStateOver4_DoesNothing_WhateverTheOtherWords()
    {
        var overs = WaterDropVector.Goldens().Where(v => v.Class == "state>=5").ToArray();
        Assert.Equal(2, overs.Length);
        Assert.All(overs, v =>
        {
            Assert.True(v.DropIn("0x274") >= 5);
            Assert.Empty(new[] { "0x274", "0x6C", "0x88", "0x11C", "0xB8", "0x54", "0x280" }.Where(v.DropOutHas));
            Replay(v);
        });
    }
}
