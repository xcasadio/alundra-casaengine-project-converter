#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c2 T1, phase A (docs/plan-e20-heros.md section 1.24.5, tests (a1), (a2) and (a4), D-G2C2-1, D-G2C2-2): the native handlers C70 (<c>0x8007B7B0</c>, the carried half of the torch) and E20 (<c>0x8007F6C8</c>, the torch that met something and its flame) of the sprite type 393, on entities made by hand,
/// and their keys on a registry of their own. The expected values are the 90 vectors that T0 made from the real code of the binary (<c>docs/plan-e20-annexe/e20-g2c2/vectors/goldens_g2c2.json</c>: 30 of C70, 30 of E20, 30 of D34; every data word read, every word written when it changed, the journal of the calls).
/// A field the real code did not read is posed with a sentinel, so that a stray write shows.
/// </summary>
public sealed class TorchAiTests
{
    private const string GoldenSha256 = "40166f5d2989eb87ee56866c8b933c34abd36ce20d67a130e2821984968d418f";

    /// <summary>The sentinels of the fields a vector leaves free: flags with 0x10 and 0x20 raised and 0x04, 0x40 and 0x80 clear (so that every bit of the three writes of the torch shows), an animation no vector uses, a direction.</summary>
    private const uint FreeFlags = 0x00AB5A31;

    private const uint FreeAnimation = 9;

    private const uint FreeDirection = 0x13;

    private static readonly byte[] FreeBytes = { 0xA1, 0xA2, 0xA3, 0xA4 };

    private static int Signed(uint word) => unchecked((int)word);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The file.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGoldenFile_IsTheOneT0Pinned_30VectorsOfEachFunction_TheJournalOfE20IsASpawnThenADestroyOrADestroyAlone()
    {
        var text = File.ReadAllText(AnnexFiles.TorchAnnex("vectors/goldens_g2c2.json")).Replace("\r\n", "\n");
        Assert.Equal(GoldenSha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());

        foreach (var function in new[] { "C70", "E20", "D34" })
        {
            Assert.Equal(30, TorchVector.Load(function).Count);
        }

        foreach (var vector in TorchVector.Load("E20"))
        {
            Assert.Contains(string.Join(",", vector.Journal.Select(j => j.Kind)), new[] { "spawn,destroy2", "destroy2", "" });
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a1) C70.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (EntityScriptProxy Torch, EntityScriptProxy? Carrier) BuildC70(TorchVector v)
    {
        var torch = CarryBench.Make("torch");
        torch.SpriteType = TorchAi.SpriteType;
        torch.Flags2 = v.HasRead(0x24) ? Signed(v.Read(0x24)) : 1; // a free code is a throw: a torch that is not carried must not let go for it.
        torch.Flags = v.HasRead(0x6C) ? v.Read(0x6C) : FreeFlags;
        torch.TargetAnimationId = FreeAnimation;
        if (v.Read(0x28) == 0)
        {
            return (torch, null);
        }

        var carrier = CarryBench.Make("carrier");
        carrier.CarriedEntity = torch.LogicContextEntity;
        torch.PlatformEntity = carrier.LogicContextEntity;
        torch.RidingEntity = carrier.LogicContextEntity;
        return (torch, carrier);
    }

    /// <summary>The codes of throw the table <c>0x800281DC</c> gives an animation for (1, 2, 3 and -1): the others are read by the binary past the table, as an address.</summary>
    private static bool IsInTheTable(int code) => code is 1 or 2 or 3 or -1;

    [Fact]
    public void A1_C70_TheThirtyVectorsOfTheOracle_AreReplayedWordForWord_TheAnimationTheCodeTheLinksAndTheFlagsWithTheBit0x34()
    {
        var vectors = TorchVector.Load("C70");
        var released = 0;
        var kept = 0;
        var idle = 0;
        var offTable = 0;

        foreach (var v in vectors)
        {
            var (torch, carrier) = BuildC70(v);
            var flags = torch.Flags;
            var code = torch.Flags2;
            var why = $"vector {v.Index}";

            TorchAi.Tick(torch);

            if (carrier == null)
            {
                // posed: nothing happens, whatever the code and the animation asked.
                idle++;
                Assert.Empty(v.Writes);
                Assert.Equal((flags, code, FreeAnimation), (torch.Flags, torch.Flags2, torch.TargetAnimationId));
                continue;
            }

            if (code == 0)
            {
                // carried without a code: the animation 0, the links kept.
                kept++;
                Assert.Equal(0u, v.After(0x88, FreeAnimation));
                Assert.Equal((flags, 0u), (torch.Flags, torch.TargetAnimationId));
                Assert.Same(carrier.LogicContextEntity, torch.PlatformEntity);
                Assert.Same(torch.LogicContextEntity, carrier.CarriedEntity);
                continue;
            }

            if (!IsInTheTable(code))
            {
                // the binary reads the words after the table: the animation is an address and everything is written. The DLL does not release for these codes (they are never written by the hero), like C23 and C4: a deviation pinned apart.
                offTable++;
                Assert.True(v.After(0x88, FreeAnimation) > 100, why);
                Assert.Equal((flags, code, FreeAnimation), (torch.Flags, torch.Flags2, torch.TargetAnimationId));
                Assert.Same(carrier.LogicContextEntity, torch.PlatformEntity);
                Assert.Same(torch.LogicContextEntity, carrier.CarriedEntity);
                continue;
            }

            released++;
            Assert.True(v.Wrote(0x24) && v.Wrote(0x28), why);
            var tableAnimation = code switch { 1 => 3u, 2 => 1u, 3 => 4u, _ => 3u }; // the table 0x800281DC: the oracle lists the animation only when it changed in the machine.
            Assert.Equal(tableAnimation, v.After(0x88, tableAnimation));
            Assert.Equal(tableAnimation, torch.TargetAnimationId);
            Assert.Equal(v.After(0x6C, flags), torch.Flags);
            Assert.Equal((flags | 0x34u) & ~0x80u, torch.Flags);
            Assert.Equal(0, torch.Flags2);
            Assert.Null(torch.PlatformEntity);
            Assert.Null(torch.RidingEntity);
            Assert.Null(carrier.CarriedEntity);
        }

        Assert.Equal(30, released + kept + idle + offTable);
        Assert.True(released >= 10 && kept >= 1 && idle >= 4 && offTable >= 5, $"the vectors cover every class: released {released}, kept {kept}, idle {idle}, off the table {offTable}");
    }

    [Theory]
    [InlineData(1, 3u)]
    [InlineData(2, 1u)]
    [InlineData(3, 4u)]
    [InlineData(-1, 3u)]
    public void A1_C70_EachCodeOfTheTableReleases_TheAnimationOfTheTable_AndTheFlagsBecomeTheOnesWithTheBit0x34(int code, uint animation)
    {
        foreach (var flags in new uint[] { 0, 0x80, 0xFFFFFFFF, 0x3323E4, 0x3323E2, 0x4, 0x20, 0x10, 0x34, FreeFlags })
        {
            var (torch, carrier) = BuildC70(TorchVector.Load("C70").First(v => v.Read(0x28) != 0));
            torch.Flags = flags;
            torch.Flags2 = code;

            TorchAi.Tick(torch);

            Assert.Equal(animation, torch.TargetAnimationId);
            Assert.Equal((flags | 0x34u) & ~0x80u, torch.Flags);
            Assert.Equal(0, torch.Flags2);
            Assert.Null(torch.PlatformEntity);
            Assert.Null(carrier!.CarriedEntity);
        }
    }

    [Fact]
    public void A1_C70_TheCodesOffTheTable_ReleaseNothing()
    {
        foreach (var code in Enumerable.Range(-6, 15).Where(c => !IsInTheTable(c) && c != 0))
        {
            var (torch, carrier) = BuildC70(TorchVector.Load("C70").First(v => v.Read(0x28) != 0));
            torch.Flags2 = code;
            var flags = torch.Flags;

            TorchAi.Tick(torch);

            Assert.Equal((flags, code, FreeAnimation), (torch.Flags, torch.Flags2, torch.TargetAnimationId));
            Assert.Same(torch.LogicContextEntity, carrier!.CarriedEntity);
        }
    }

    [Fact]
    public void A1_C70_APosedTorchWhoseAnimationAskedIs2_IsLeftAloneWhateverItsCode_TheGuardOfTheCarrierIsWhatSaysSo()
    {
        // révision 2: a torch with no carrier whose animation asked is 2 (the burning one) would be sent back to the animation 0 if the guard `+0x28 == 0 -> return` were lost.
        foreach (var code in new[] { 0, 1, 2, 3, -1 })
        {
            var torch = CarryBench.Make("torch");
            torch.SpriteType = TorchAi.SpriteType;
            torch.TargetAnimationId = 2;
            torch.Flags2 = code;
            torch.Flags = 0x3323E4;

            TorchAi.Tick(torch);

            Assert.Equal((2u, code, 0x3323E4u), (torch.TargetAnimationId, torch.Flags2, torch.Flags));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a4) the switch of TickCarried.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static EntityScriptProxy CarriedWithCode(int code, uint flags, int nativeECode)
    {
        var hero = CarryBench.Make("hero");
        var item = CarryBench.Make("item");
        item.Flags = flags;
        item.Flags2 = code;
        item.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = nativeECode;
        item.PlatformEntity = hero.LogicContextEntity;
        hero.CarriedEntity = item.LogicContextEntity;
        return item;
    }

    [Fact]
    public void A4_TheC70Policy_IsNotTakenForC4_NorForC23_TheThreeWriteThreeDifferentFlags()
    {
        // 0x81: the bit 0x04 and the bits 0x10 and 0x20 are clear, the bit 0x80 is raised: C70 gives 0x35, C23 gives 0x31, C4 leaves 0x81 (a code 1) and gives 0x91 for the code -1 of an object whose native E is 2.
        var c70 = CarriedWithCode(1, 0x81, 2);
        var c23 = CarriedWithCode(1, 0x81, 2);
        var c4 = CarriedWithCode(1, 0x81, 2);

        LiftableAi.TickCarried(c70, LiftableAi.ReleaseFlagsPolicy.C70);
        LiftableAi.TickCarried(c23, LiftableAi.ReleaseFlagsPolicy.C23);
        LiftableAi.TickCarried(c4, LiftableAi.ReleaseFlagsPolicy.C4);

        Assert.Equal(0x35u, c70.Flags);
        Assert.Equal(0x31u, c23.Flags);
        Assert.Equal(0x81u, c4.Flags);

        var dead70 = CarriedWithCode(-1, 0x81, 2);
        var dead4 = CarriedWithCode(-1, 0x81, 2);
        LiftableAi.TickCarried(dead70, LiftableAi.ReleaseFlagsPolicy.C70);
        LiftableAi.TickCarried(dead4, LiftableAi.ReleaseFlagsPolicy.C4);
        Assert.Equal(0x35u, dead70.Flags); // C70 does not look at the native E code: the code -1 and the code 1 give the same flags.
        Assert.Equal(0x11u, dead4.Flags); // C4: (0x81 | 0x10) & ~0x80.
    }

    [Fact]
    public void A4_APolicyNoCaseNamesIsRefused_ItIsNeverTakenForC4()
    {
        var item = CarriedWithCode(1, 0x81, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => LiftableAi.TickCarried(item, (LiftableAi.ReleaseFlagsPolicy)99));
    }

    [Fact]
    public void A4_C23AndC4_KeepTheirFlags_TheCrateAndTheBarrelAreNotTouched()
    {
        var crate = CarriedWithCode(2, 0x3323E4, 0);
        LiftableAi.TickCarried(crate, LiftableAi.ReleaseFlagsPolicy.C23);
        Assert.Equal(0x332374u, crate.Flags);

        var barrel = CarriedWithCode(2, 0x3323E4, 2);
        LiftableAi.TickCarried(barrel, LiftableAi.ReleaseFlagsPolicy.C4);
        Assert.Equal(0x3323E4u, barrel.Flags);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (a2) E20.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CutBench BenchFor(TorchVector v)
    {
        var bench = CutBench.Create(frame: false);
        var torch = bench.Entity;
        torch.SpriteType = TorchAi.SpriteType;
        torch.Status = EntityStatus.Deactivated;
        torch.Flags = v.HasRead(0x6C) ? v.Read(0x6C) : FreeFlags;
        torch.TargetAnimationId = v.HasRead(0x88) ? v.Read(0x88) : FreeAnimation;
        torch.ForceResetAnimationFlag = v.HasRead(0xAC) ? Signed(v.Read(0xAC)) : 1; // free: 1, so that a test of the animation alone would take the first branch.
        torch.TargetDirection = v.HasRead(0x8C) ? v.Read(0x8C) : FreeDirection;
        torch.PosX = v.HasRead(0x114) ? Signed(v.Read(0x114)) : 0x111111;
        torch.PosY = v.HasRead(0x118) ? Signed(v.Read(0x118)) : 0x222222;
        torch.PosZ = v.HasRead(0x11C) ? Signed(v.Read(0x11C)) : 0x333333;
        var word = v.HasRead(0x274) ? v.Read(0x274) : 0u;
        var bytes = v.HasRead(0x274) ? BitConverter.GetBytes(word) : FreeBytes;
        bytes.CopyTo(torch.Bytes, 0);
        var contact = v.Read(0x130);
        if (contact != 0)
        {
            torch.XCollisionEntity = new EntityScriptProxy { SpriteType = Signed(v.WordAt(contact + 0x68)) };
        }

        return bench;
    }

    [Fact]
    public void A2_E20_TheThirtyVectorsOfTheOracle_AreReplayedWordForWord_TheFlameTheDestructionTheMarkTheAnimationAndTheFlags()
    {
        var flames = 0;
        var nullSpawns = 0;
        var seconds = 0;
        var marks = 0;
        var consumed = 0;

        foreach (var v in TorchVector.Load("E20"))
        {
            var bench = BenchFor(v);
            var torch = bench.Entity;
            var host = bench.Host;
            var flags = torch.Flags;
            var animation = torch.TargetAnimationId;
            var bytes = torch.Bytes.ToArray();
            var why = $"vector {v.Index}";
            var spawn = v.Journal.FirstOrDefault(j => j.Kind == "spawn");
            var destroyedAtBirth = -1;
            var flameCodes = new uint[6];
            host.TableIsFull = v.SpawnReturn == 0;
            host.OnBirth = flame =>
            {
                destroyedAtBirth = host.Destroyed.Count;
                flame.Flags = v.WordAt(v.SpawnReturn + 0x6C);
                flame.TargetAnimationId = FreeAnimation;
                for (var i = 0; i < 6; i++)
                {
                    // the six codes at +0x70..+0x84: the vector lists a code only when it changed, so a listed one was not zero.
                    var listed = v.Writes.ContainsKey(v.SpawnReturn + 0x70 + (uint)(4 * i));
                    flame.SpriteProgramIndexes[i] = listed ? 3 + 5 * i : 0;
                }
            };

            TorchAi.Deactivate(torch, host);

            if (v.Journal.Any(j => j.Kind == "destroy2"))
            {
                // the first branch: the torch is consumed.
                consumed++;
                var gone = Assert.Single(host.Destroyed);
                Assert.Equal((torch, -1), (gone.Entity, Signed(v.Journal.Last().Arguments[1])));
                Assert.Equal((flags, animation), (torch.Flags, torch.TargetAnimationId)); // the torch itself is not written.
                Assert.Equal(bytes, torch.Bytes);
                if (spawn.Arguments == null)
                {
                    Assert.Empty(host.SpawnCalls);
                    continue;
                }

                var call = Assert.Single(host.SpawnCalls);
                Assert.Equal((torch, false, 12, torch.PosX, torch.PosY, torch.PosZ, Signed(torch.TargetDirection)), call);
                Assert.Equal(new[] { 0u, 12u, v.Read(0x114), v.Read(0x118), v.Read(0x11C), v.Read(0x8C) }, spawn.Arguments.Skip(1).ToArray()); // the journal of the real code: page 0, type 12, X, Y, Z, direction.
                if (v.SpawnReturn == 0)
                {
                    nullSpawns++;
                    Assert.Empty(host.Born);
                    continue;
                }

                Assert.Equal(0, destroyedAtBirth); // the flame is born BEFORE the torch is destroyed.

                flames++;
                var flame = Assert.Single(host.Born);
                Assert.Equal(12, flame.SpriteType);
                Assert.All(flame.SpriteProgramIndexes, code => Assert.Equal(0, code));
                Assert.Equal(3u, flame.TargetAnimationId);
                var seed = v.WordAt(v.SpawnReturn + 0x6C);
                var expected = (seed & ~0x10u) | 2u;
                Assert.Equal(expected, flame.Flags);
                Assert.Equal(v.Writes.TryGetValue(v.SpawnReturn + 0x6C, out var written) ? written : seed, expected); // the oracle agrees.
                continue;
            }

            // the second branch: the animation 2, the flags without the deactivation bits, and the mark when a drop of water touches.
            seconds++;
            Assert.Empty(host.Destroyed);
            Assert.Empty(host.SpawnCalls);
            Assert.Equal(2u, torch.TargetAnimationId);
            Assert.Equal(v.After(0x6C, flags), torch.Flags);
            Assert.Equal((flags | 0x40u) & ~0x30u, torch.Flags);
            var contact = v.Read(0x130);
            var onDrop = contact != 0 && Signed(v.WordAt(contact + 0x68)) == 391;
            Assert.True(!v.Wrote(0x274) || onDrop, why); // the oracle writes the mark only on a drop (a byte already at 1 is not listed).
            if (onDrop)
            {
                marks++;
                Assert.Equal(1, torch.Bytes[0]);
                Assert.Equal(bytes.Skip(1), torch.Bytes.Skip(1)); // a byte store: the other three bytes of the word are untouched.
            }
            else
            {
                Assert.Equal(bytes, torch.Bytes);
            }
        }

        Assert.True(flames >= 3 && nullSpawns == 1 && consumed >= 8 && seconds >= 15 && marks >= 5, $"the vectors cover every class: flames {flames}, null spawn {nullSpawns}, consumed {consumed}, second branch {seconds}, marks {marks}");
    }

    [Fact]
    public void A2_E20_TheFlameBornWithSixNonZeroCodes_ShowsAllSixAtZero_BothEndsOfTheLoopComprised()
    {
        // révision 2: the real flame has the codes at both ends already at 0, so an erasure that stops one short would not show on it; here all six are non-zero.
        var bench = CutBench.Create(frame: false);
        var torch = bench.Entity;
        torch.SpriteType = TorchAi.SpriteType;
        torch.TargetAnimationId = 2;
        torch.ForceResetAnimationFlag = 1;
        torch.SpriteProgramIndexes[0] = 70;
        torch.SpriteProgramIndexes[5] = 20;
        bench.Host.OnBirth = flame =>
        {
            for (var i = 0; i < 6; i++)
            {
                flame.SpriteProgramIndexes[i] = 10 + i;
            }

            flame.Flags = 0x00132156;
        };

        TorchAi.Deactivate(torch, bench.Host);

        var born = Assert.Single(bench.Host.Born);
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0 }, born.SpriteProgramIndexes.ToArray());
        Assert.Equal(3u, born.TargetAnimationId);
        Assert.Equal((0x00132156u & ~0x10u) | 2u, born.Flags);
        Assert.Equal(new[] { 70, 0, 0, 0, 0, 20 }, torch.SpriteProgramIndexes.ToArray()); // the torch's own codes are not the flame's.
    }

    [Fact]
    public void A2_E20_TheFlagsOfTheFlame_LoseTheBit0x10AndKeepTheOthers_WithTheBit2Raised()
    {
        foreach (var flags in new uint[] { 0, 0x10, 0x12, 0xFFFFFFFF, 0x00132146, 0x00132156, 0x80000010 })
        {
            var bench = CutBench.Create(frame: false);
            bench.Entity.TargetAnimationId = 2;
            bench.Entity.ForceResetAnimationFlag = 1;
            bench.Host.OnBirth = flame => flame.Flags = flags;

            TorchAi.Deactivate(bench.Entity, bench.Host);

            Assert.Equal((flags & ~0x10u) | 2u, Assert.Single(bench.Host.Born).Flags);
        }
    }

    [Fact]
    public void A2_E20_TheTableOfEntitiesFull_TheTorchIsDestroyedAllTheSame_NoFlame()
    {
        // D-G2C2-2: the binary writes through the null pointer; the DLL skips the flame and the torch goes.
        var bench = CutBench.Create(frame: false);
        bench.Entity.TargetAnimationId = 2;
        bench.Entity.ForceResetAnimationFlag = 1;
        bench.Host.TableIsFull = true;

        TorchAi.Deactivate(bench.Entity, bench.Host);

        Assert.Single(bench.Host.SpawnCalls);
        Assert.Empty(bench.Host.Born);
        var gone = Assert.Single(bench.Host.Destroyed);
        Assert.Equal((bench.Entity, -1), (gone.Entity, gone.EffectId));
    }

    [Fact]
    public void A2_E20_TheMarkOfTheWaterDropIsTheType391Alone_AndAMarkedTorchIsDestroyedWithoutFlame()
    {
        for (var type = 0; type < 600; type++)
        {
            var bench = CutBench.Create(frame: false);
            bench.Entity.TargetAnimationId = 0;
            bench.Entity.XCollisionEntity = new EntityScriptProxy { SpriteType = type };

            TorchAi.Deactivate(bench.Entity, bench.Host);

            Assert.True((type == 391) == (bench.Entity.Bytes[0] == 1), $"type {type}");
        }

        // the mark is the byte +0x274: one byte, the first; a torch that carries it goes without a flame; a torch whose other bytes are non-zero but whose first is zero still makes one.
        var marked = CutBench.Create(frame: false);
        marked.Entity.TargetAnimationId = 2;
        marked.Entity.ForceResetAnimationFlag = 1;
        marked.Entity.Bytes[0] = 1;
        TorchAi.Deactivate(marked.Entity, marked.Host);
        Assert.Empty(marked.Host.SpawnCalls);
        Assert.Single(marked.Host.Destroyed);

        var high = CutBench.Create(frame: false);
        high.Entity.TargetAnimationId = 2;
        high.Entity.ForceResetAnimationFlag = 1;
        high.Entity.Bytes[1] = 7;
        high.Entity.Bytes[2] = 7;
        high.Entity.Bytes[3] = 7;
        TorchAi.Deactivate(high.Entity, high.Host);
        Assert.Single(high.Host.SpawnCalls);
        Assert.True(TorchAi.HasNoFlameMark(marked.Entity));
        Assert.False(TorchAi.HasNoFlameMark(high.Entity));
    }

    [Fact]
    public void A2_E20_TheSecondBranchNeedsNoHost_TheTorchIsWrittenAllTheSame()
    {
        var second = new EntityScriptProxy { SpriteType = TorchAi.SpriteType, TargetAnimationId = 0, Flags = FreeFlags };

        TorchAi.Deactivate(second, CutBench.Create(frame: false).Host);

        Assert.Equal((2u, (FreeFlags | 0x40u) & ~0x30u), (second.TargetAnimationId, second.Flags));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The keys.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTorchRegistersC70AndE20_ForTheType393Alone_AndTheDisposableTakesBothKeysOff()
    {
        var registry = new NativeSpriteRegistry();

        var registration = TorchAi.Register(registry);

        Assert.Equal(2, registry.Count);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 70), (ScriptHelper.ProgramEDeactivate, 20) }, TorchAi.Keys);
        Assert.Equal(new[] { 393 }, TorchAi.SpriteTypes);
        for (var type = 0; type < 600; type++)
        {
            Assert.True((type == 393) == registry.TryGet(ScriptHelper.ProgramCTick, 70, type, out _), $"C70 type {type}");
            Assert.True((type == 393) == registry.TryGet(ScriptHelper.ProgramEDeactivate, 20, type, out _), $"E20 type {type}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 70, 393, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramDTouch, 20, 393, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 70, 393, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 20, 393, out _));
        Assert.Throws<InvalidOperationException>(() => TorchAi.Register(registry));
        Assert.Equal(2, registry.Count); // a second registration leaves the first intact.

        registration.Dispose();

        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void TheRegisteredHandlers_AreTheTorchsOwn_C70LetsGoOfACarriedTorch_E20ConsumesAnAnimationEndedOne()
    {
        var registry = new NativeSpriteRegistry();
        using var registration = TorchAi.Register(registry);
        Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 70, 393, out var tick));
        Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 20, 393, out var deactivate));

        var (torch, carrier) = BuildC70(TorchVector.Load("C70").First(v => v.Read(0x28) != 0));
        torch.Flags2 = 2;
        tick!(torch, CutBench.Create(frame: false).Host, new Alundra.Session.GameState());
        Assert.Equal((1u, 0), (torch.TargetAnimationId, torch.Flags2));
        Assert.Null(carrier!.CarriedEntity);

        var bench = CutBench.Create(frame: false);
        bench.Entity.TargetAnimationId = 2;
        bench.Entity.ForceResetAnimationFlag = 1;
        deactivate!(bench.Entity, bench.Host, bench.Host.GameState);
        Assert.Single(bench.Host.Born);
        Assert.Single(bench.Host.Destroyed);
    }
}
