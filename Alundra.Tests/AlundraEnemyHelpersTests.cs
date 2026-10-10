#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E14.a0b T3 (docs/plan-e14-ia-native.md section 1.2): the shared helpers of the enemy AI (<see cref="AlundraEnemyHelpers"/>) against the real code of <c>ALUN_CD.EXE</c> (France) run in the emulator on 10,000 random states each
/// (<c>docs/plan-e14-annexe/e14a/oracle/out/h1/&lt;helper&gt;.txt.gz</c>): every state, value by value. The expected values are the oracle's, never computed by the code under test.
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraEnemyHelpersTests
{
    private const int MarkAnim = 0x7777;
    private const int MarkDir = 0x5555;
    private const int MarkForce = 0x11111111;
    private const short MarkHalf = 0x1234;

    private static void ForEachRow(string helper, Action<EnemyHelperRow, string> check)
    {
        var rows = AlundraEnemyOracle.Helper(helper);
        Assert.True(rows.Count >= 10000, $"{helper}: at least 10,000 states in the oracle (found {rows.Count})");
        for (var i = 0; i < rows.Count; i++)
        {
            check(rows[i], $"{helper} state {i} in [{string.Join(' ', rows[i].In)}] expected [{string.Join(' ', rows[i].Out)}]");
        }
    }

    private static void AssertWords(int[] expected, ReadOnlySpan<int> actual, string where)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(expected[i] == actual[i], $"{where}: word {i} is {actual[i]}");
        }
    }

    [Fact]
    public void RelativePosition_EqualsTheBinary_OnEveryState()
    {
        ForEachRow("relpos", (row, where) =>
        {
            var self = new AlundraEntityScriptProxy { TileX = row.In[0], TileY = row.In[1], FloorHeight = row.In[2] };
            var hero = new AlundraEntityScriptProxy { TileX = row.In[3], TileY = row.In[4], FloorHeight = row.In[5] };
            Span<int> output = stackalloc int[6];

            AlundraEnemyHelpers.RelativePosition(self, hero, output);

            AssertWords(row.Out, output, where);
        });
    }

    [Fact]
    public void RelativePositionFromPosZ_EqualsTheBinary_OnEveryState()
    {
        ForEachRow("relpos2", (row, where) =>
        {
            var self = new AlundraEntityScriptProxy { TileX = row.In[0], TileY = row.In[1], PosZ = row.In[2] };
            var hero = new AlundraEntityScriptProxy { TileX = row.In[3], TileY = row.In[4], FloorHeight = row.In[5] };
            Span<int> output = stackalloc int[6];

            AlundraEnemyHelpers.RelativePositionFromPosZ(self, hero, output);

            AssertWords(row.Out, output, where);
        });
    }

    [Fact]
    public void TryAttackPlayer2_EqualsTheBinary_OnEveryState()
    {
        ForEachRow("tap2", (row, where) =>
        {
            var rel = row.In.AsSpan(0, 6);

            var result = AlundraEnemyHelpers.TryAttackPlayer2(rel, row.In[6], row.In[7], row.In[8], row.In[9]);

            Assert.True(row.Out[0] == (result ? 1 : 0), $"{where}: answered {result}");
        });
    }

    [Fact]
    public void TryAttackPlayerFront_EqualsTheBinary_OnEveryState_ReadingBoxDirectionNeverAnimationDirection()
    {
        ForEachRow("tapfront", (row, where) =>
        {
            // +0x9C (the DLL's AnimationDirection, an end-of-frame value) holds a decoy that differs from the direction of the tick in every state (the oracle writes the same decoy)
            var self = new AlundraEntityScriptProxy { BoxDirection = row.In[0], AnimationDirection = (row.In[0] + 1) & 3 };
            var rel = row.In.AsSpan(1, 6);

            var result = AlundraEnemyHelpers.TryAttackPlayerFront(self, rel, row.In[7], row.In[8], row.In[9]);

            Assert.True(row.Out[0] == (result ? 1 : 0), $"{where}: answered {result}");
        });
    }

    /// <summary>The trap of D-E14-6 stated on its own: the same window answers by the direction of the tick, whatever the end-of-frame row says.</summary>
    [Fact]
    public void TryAttackPlayerFront_ReadsBoxDirection_NotAnimationDirection()
    {
        // the target is east of the entity at the same height, in reach: only direction 3 (|dy| <= a, dx <= 0) accepts it
        int[] rel = { 10, 2, 0, -10, 2, 0 };
        var east = new AlundraEntityScriptProxy { BoxDirection = 3, AnimationDirection = 0 };
        var other = new AlundraEntityScriptProxy { BoxDirection = 0, AnimationDirection = 3 };

        Assert.True(AlundraEnemyHelpers.TryAttackPlayerFront(east, rel, 5, 12, 0));
        Assert.False(AlundraEnemyHelpers.TryAttackPlayerFront(other, rel, 5, 12, 0));
    }

    [Fact]
    public void HandleAnimationDirection_EqualsTheBinary_OnEveryState()
    {
        ForEachRow("had", (row, where) =>
        {
            var self = new AlundraEntityScriptProxy { FloorHeight = row.In[0], TargetDirection = (uint)row.In[5], TargetAnimationId = MarkAnim };
            for (var k = 0; k < 4; k++)
            {
                self.MapHeights[k] = row.In[1 + k];
            }

            self.TargetForceX = self.TargetForceY = self.ForceX = self.ForceY = self.ForceStepX = self.ForceStepY = MarkForce;

            var turned = AlundraEnemyHelpers.HandleAnimationDirection(self, (uint)row.In[6], row.In[7]);

            // the outputs in the oracle's order: result, +0x88, +0x8C, then +0xBC +0xC0 +0xC4 +0xC8 +0xD4 +0xD8
            int[] actual = { turned ? 1 : 0, (int)self.TargetAnimationId, (int)self.TargetDirection, self.TargetForceX, self.TargetForceY, self.ForceX, self.ForceY, self.ForceStepX, self.ForceStepY };
            AssertWords(row.Out, actual, where);
        });
    }

    [Fact]
    public void StartWalk_EqualsTheBinary_OnEveryState_IncludingTheRandomWord()
    {
        var saved = AlundraRandom.RandomSeed;
        try
        {
            ForEachRow("startwalk", (row, where) =>
            {
                AlundraRandom.RandomSeed = (uint)row.In[0];
                var self = new AlundraEntityScriptProxy { PosX = row.In[1], PosY = row.In[2], TargetDirection = MarkDir, TargetAnimationId = MarkAnim };
                self.AIValues[1] = MarkHalf;
                var hero = new AlundraEntityScriptProxy { PosX = row.In[3], PosY = row.In[4] };

                AlundraEnemyHelpers.StartWalk(self, hero, (uint)row.In[5], row.In[6], row.In[7]);

                // the outputs in the oracle's order: the random word, +0x8C, +0x88, +0x282 (16 bits)
                int[] actual = { unchecked((int)(uint)AlundraRandom.RandomSeed), (int)self.TargetDirection, (int)self.TargetAnimationId, (ushort)self.AIValues[1] };
                AssertWords(row.Out, actual, where);
            });
        }
        finally
        {
            AlundraRandom.RandomSeed = saved;
        }
    }

    [Fact]
    public void GetDirectionToTarget_EqualsTheBinarysDirectionRoutine_OnEveryState()
    {
        ForEachRow("atan", (row, where) =>
        {
            var direction = ScriptHelper.GetDirectionToTarget(row.In[0], row.In[1]);

            Assert.True(row.Out[0] == (int)direction, $"{where}: answered {direction}");
        });
    }

    /// <summary>The generator draws, as the binary's inline pattern does: the state advances once, the draw is the high word of (state * n). Vectors computed from the multiplication constants alone.</summary>
    [Theory]
    [InlineData(0UL, 0x65, 88)]
    [InlineData(0UL, 0x20, 28)]
    [InlineData(0UL, 16, 14)]
    [InlineData(0x12345678UL, 0x65, 72)]
    [InlineData(0x12345678UL, 0x20, 23)]
    [InlineData(0xB017C93DUL, 16, 3)]
    public void Below_IsTheHighWordOfTheProduct(ulong seed, int n, int expected)
    {
        var saved = AlundraRandom.RandomSeed;
        try
        {
            AlundraRandom.RandomSeed = seed;
            Assert.Equal(expected, AlundraEnemyHelpers.Below(n));
        }
        finally
        {
            AlundraRandom.RandomSeed = saved;
        }
    }

    /// <summary>The high bits of the 64-bit state (<see cref="AlundraRandom.RandomSeed"/> is a ulong that keeps them) never reach the draw.</summary>
    [Fact]
    public void Below_IgnoresTheHighBitsOfTheState()
    {
        var saved = AlundraRandom.RandomSeed;
        try
        {
            AlundraRandom.RandomSeed = 0xFFFF_FFFF_0000_0000UL;
            var withHigh = AlundraEnemyHelpers.Below(0x65);
            AlundraRandom.RandomSeed = 0;
            Assert.Equal(AlundraEnemyHelpers.Below(0x65), withHigh);
        }
        finally
        {
            AlundraRandom.RandomSeed = saved;
        }
    }

    [Fact]
    public void QuarterTurnTable_IsTheBinarys32Bytes()
    {
        // 0x80028B34: 18, 00 x8, 08 x8, 10 x8, 18 x7 (dumped from the executable; the decompilation's table differs at indices 14-16, 19-23, 25-27)
        var expected = new List<byte> { 0x18 };
        expected.AddRange(Enumerable.Repeat((byte)0x00, 8));
        expected.AddRange(Enumerable.Repeat((byte)0x08, 8));
        expected.AddRange(Enumerable.Repeat((byte)0x10, 8));
        expected.AddRange(Enumerable.Repeat((byte)0x18, 7));
        Assert.Equal(expected, AlundraEnemyHelpers.QuarterTurnTable);
    }
}
