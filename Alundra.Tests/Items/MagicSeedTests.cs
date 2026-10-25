#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Rendering.Effects;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Items;

/// <summary>
/// E20.e0d T3 (docs/plan-e20-heros.md section 1.26.2, phase C, tests (c1) and (c2)): the magic seed (<c>0x8003382C</c>, the twin of the life vessel <c>0x800333AC</c> but for 13 instructions) by its three entries - <see cref="ItemEffects.GiveItem"/>,
/// <see cref="ItemEffects.ApplyItem"/> and the sub-case <c>0x50</c> of the opcode <c>0x82</c> (<c>0x80034108</c>) - against the table of <c>seed_oracle.out</c> (docs/plan-e20-annexe/e20-e0d-disc/: the real code replayed, MpMax 0 to 5): <c>MpMax = min(MpMax + 1, 4)</c>,
/// <c>Mp = MpMax</c>, the sound <c>0x36</c>, 8 rings and 4 sparks of the effect 14 in the ANIMATION 2 (the vessel: 0), 8 draws of the generator. The effects go through the queue of the effect stage (D-E0C-8). The vessel (c2) is the guard: its body is shared, its animation stays 0.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class MagicSeedTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly EffectBank Bank = EffectGolden.BankOf(Root, 476);

    /// <summary>The generator of the oracle (<c>seed_oracle.py</c>) and the hero of its journal, in the binary's frame of Z.</summary>
    private const uint OracleSeed = 0xB017C93D;
    private const int HeroX = 0x100000;
    private const int HeroY = 0x200000;
    private const int HeroBinaryZ = 0x300000;

    /// <summary>The 15 rows of <c>seed_oracle.out</c>: (Mp, MpMax) before, (Mp, MpMax) after.</summary>
    public static TheoryData<int, int, int, int> Table => new()
    {
        { 0, 0, 1, 1 }, { 0, 1, 2, 2 }, { 1, 1, 2, 2 }, { 0, 2, 3, 3 }, { 1, 2, 3, 3 }, { 2, 2, 3, 3 }, { 0, 3, 4, 4 }, { 1, 3, 4, 4 }, { 3, 3, 4, 4 },
        { 0, 4, 4, 4 }, { 2, 4, 4, 4 }, { 4, 4, 4, 4 }, { 0, 5, 4, 4 }, { 2, 5, 4, 4 }, { 5, 5, 4, 4 },
    };

    /// <summary>The 12 effects of the journal of <c>seed_oracle.out</c> (<c>fx00</c> to <c>fx11</c>): the offset from the hero in X and Y, the forces in X, Y and Z. The eight rings come first, the four sparks last (their forces come from the generator).</summary>
    private static readonly (int Dx, int Dy, int ForceX, int ForceY, int ForceZ)[] Journal =
    [
        (0, 4194304, 0, -262144, 0),
        (-4448256, 2965504, 278016, -185344, 0),
        (-6291456, 0, 393216, 0, 0),
        (-4448256, -2965504, 278016, 185344, 0),
        (0, -4194304, 0, 262144, 0),
        (4448256, -2965504, -278016, 185344, 0),
        (6291456, 0, -393216, 0, 0),
        (4448256, 2965504, -278016, -185344, 0),
        (0, 0, -37946, 55378, 262144),
        (0, 0, 51203, 6754, 262144),
        (0, 0, -56837, -72413, 262144),
        (0, 0, -51174, 53567, 262144),
    ];

    public MagicSeedTests()
    {
        GameRandom.Reset();
    }

    public void Dispose()
    {
        GameRandom.Reset();
    }

    private sealed class Rig
    {
        public PickupHost Host { get; } = new();
        public RecordingSoundPlayer Sound { get; } = new();
        public GameState State { get; } = new();
        public EntityScriptProxy Hero { get; }
        public EffectStage Stage { get; } = new();

        public Rig(int mp, int mpMax)
        {
            Hero = new EntityScriptProxy { Hp = 4, HpMax = 10, PosX = HeroX, PosY = HeroY, PosZ = unchecked(HeroBinaryZ - 1), LifePulledFromStats = true };
            State.PlayerStats.Mp = (short)mp;
            State.PlayerStats.MpMax = (short)mpMax;
            Stage.Load(Bank);
            Host.Stage = Stage;
            Host.Sound = Sound;
            Host.Hero = Hero;
            GameRandom.RandomSeed = OracleSeed;
        }

        public void Apply() => Stage.RunFrame(0, Array.Empty<bool>());

        public List<EffectSlot> Created() => Stage.Reservoir.ActiveSlots.OrderBy(x => x.Index).ToList();
    }

    private static uint SeedAfterEightDraws()
    {
        GameRandom.RandomSeed = OracleSeed;
        for (var i = 0; i < 8; i++)
        {
            GameRandom.Next();
        }

        var after = (uint)GameRandom.RandomSeed;
        GameRandom.RandomSeed = OracleSeed;
        return after;
    }

    private static void AssertTheJournal(Rig rig, int animation, string where)
    {
        var effects = rig.Created();
        Assert.True(effects.Count == 12, $"{where}: {effects.Count} effects, the oracle 12");
        for (var i = 0; i < 12; i++)
        {
            var (dx, dy, fx, fy, fz) = Journal[i];
            var slot = effects[i];
            Assert.True(slot.TargetEffect == 14 && !slot.TargetMapTable && slot.TargetAnimation == animation, $"{where}: effect {i} identity (animation {slot.TargetAnimation}, the oracle {animation})");
            Assert.True((slot.X, slot.Y, slot.Z) == (HeroX + dx, HeroY + dy, 0x400000), $"{where}: effect {i} position ({slot.X:X}, {slot.Y:X}, {slot.Z:X})");
            Assert.True((slot.ForceX, slot.ForceY, slot.ForceZ) == (fx, fy, fz), $"{where}: effect {i} forces ({slot.ForceX}, {slot.ForceY}, {slot.ForceZ}), the oracle ({fx}, {fy}, {fz})");
        }
    }

    private void AssertSeedGiven(Rig rig, int mpAfter, int mpMaxAfter, string where)
    {
        Assert.True((rig.State.PlayerStats.Mp, rig.State.PlayerStats.MpMax) == (mpAfter, mpMaxAfter), $"{where}: (Mp, MpMax) = ({rig.State.PlayerStats.Mp}, {rig.State.PlayerStats.MpMax}), the oracle ({mpAfter}, {mpMaxAfter})");
        Assert.Equal(new[] { 0x36 }, rig.Sound.Requests.ToArray());
        Assert.Equal(0, rig.Stage.Reservoir.ActiveCount);             // the effects are on the queue: nothing created at the tick of the pickup ...
        Assert.Equal(OracleSeed, (uint)GameRandom.RandomSeed);         // ... and no number drawn.
        rig.Apply();
        AssertTheJournal(rig, 2, where);
        var seedAfter = (uint)GameRandom.RandomSeed;
        Assert.Equal(SeedAfterEightDraws(), seedAfter);                // the eight draws of the four sparks.
        Assert.Equal((10, 4), (rig.Hero.HpMax, rig.Hero.Hp));          // the life is not touched.
    }

    // ---- (c1) the three entries ---------------------------------------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Table))]
    public void C1_GiveItem_0x50_RaisesTheMagicMaximumUpToFour_FillsTheMagic_PlaysTheSound0x36_AndMakesTwelveEffectsInTheAnimation2(int mp, int mpMax, int mpAfter, int mpMaxAfter)
    {
        var rig = new Rig(mp, mpMax);

        var answer = ItemEffects.GiveItem(rig.Host, rig.State, rig.Hero, 0x50);

        Assert.Equal(1, answer);
        AssertSeedGiven(rig, mpAfter, mpMaxAfter, $"GiveItem ({mp}, {mpMax})");
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void C1_ApplyItem_0x50_IsTheSameBody(int mp, int mpMax, int mpAfter, int mpMaxAfter)
    {
        var rig = new Rig(mp, mpMax);

        ItemEffects.ApplyItem(rig.Host, rig.State, rig.Hero, 0x50);

        AssertSeedGiven(rig, mpAfter, mpMaxAfter, $"ApplyItem ({mp}, {mpMax})");
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void C1_TheOpcode0x82_WithTheId0x50_IsTheSameBody_AnswersOne_AndIsNoLongerDegraded(int mp, int mpMax, int mpAfter, int mpMaxAfter)
    {
        var rig = new Rig(mp, mpMax);
        var trace = new List<EventTraceRecord>();

        var result = RunOpcode0x82(rig, 0x50, trace);

        Assert.Equal(1, result);
        AssertSeedGiven(rig, mpAfter, mpMaxAfter, $"0x82 ({mp}, {mpMax})");
        Assert.NotEqual(EventTraceKind.Degraded, trace.First(record => record.Opcode == 0x82).Kind);
    }

    [Fact]
    public void C1_TheOpcode0x82_WithTheId0x50_AndNoEffectStage_StillRaisesTheMagicAndPlaysTheSound()
    {
        var rig = new Rig(0, 1);
        rig.Host.Stage = null;

        var result = RunOpcode0x82(rig, 0x50, null);

        Assert.Equal(1, result);
        Assert.Equal((2, 2), ((int)rig.State.PlayerStats.Mp, (int)rig.State.PlayerStats.MpMax));
        Assert.Equal(new[] { 0x36 }, rig.Sound.Requests.ToArray());
        Assert.Equal(OracleSeed, (uint)GameRandom.RandomSeed);
    }

    [Fact]
    public void C1_TheForcesOfTheFourSparks_ComeFromTheEightDrawsOfTheGenerator_AndTheGeneratorEndsAfterThem()
    {
        var rig = new Rig(0, 0);

        ItemEffects.GiveItem(rig.Host, rig.State, rig.Hero, 0x50);
        rig.Apply();

        var after = (uint)GameRandom.RandomSeed;
        Assert.Equal(SeedAfterEightDraws(), after);
        Assert.NotEqual(OracleSeed, after);
    }

    private static int RunOpcode0x82(Rig rig, int id, List<EventTraceRecord>? trace)
    {
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 }, Codes = new[] { 0x82, id, 0x1A, 9, 0xFF } };
        var runner = new EventProgramRunner(document, rig.State, rig.Host);
        if (trace != null)
        {
            runner.TraceSink = trace.Add;
        }

        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = 7 };
        var entity = new EntityScriptProxy { Status = EntityStatus.Normal, LogicContextEntity = new Entity { Name = "logic" } };
        runner.RunOneScriptCall(entity, state);
        Assert.Equal(9u, entity.TargetAnimationId);
        return state.Result;
    }

    // ---- (c2) the guard: the life vessel is unchanged ------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void C2_TheLifeVessel_StaysInTheAnimation0_WithTheSameTwelveEffects_TheSound0x31_AndTheLifeNotTheMagic()
    {
        var rig = new Rig(1, 2);

        ItemEffects.GiveItem(rig.Host, rig.State, rig.Hero, 0x53);
        rig.Apply();

        Assert.Equal(new[] { 0x31 }, rig.Sound.Requests.ToArray());
        Assert.Equal((11, 11), (rig.Hero.HpMax, rig.Hero.Hp));
        Assert.Equal((1, 2), ((int)rig.State.PlayerStats.Mp, (int)rig.State.PlayerStats.MpMax));
        AssertTheJournal(rig, 0, "the life vessel");
    }

    [Fact]
    public void C2_TheLifeVessel_ByApplyItem_IsTheSameAsByGiveItem()
    {
        var give = new Rig(1, 2);
        ItemEffects.GiveItem(give.Host, give.State, give.Hero, 0x53);
        give.Apply();
        var apply = new Rig(1, 2);
        ItemEffects.ApplyItem(apply.Host, apply.State, apply.Hero, 0x53);
        apply.Apply();

        AssertTheJournal(apply, 0, "ApplyItem 0x53");
        Assert.Equal(give.Created().Select(s => (s.X, s.Y, s.Z, s.ForceX, s.ForceY, s.ForceZ, s.TargetAnimation)), apply.Created().Select(s => (s.X, s.Y, s.Z, s.ForceX, s.ForceY, s.ForceZ, s.TargetAnimation)));
    }
}
