#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Combat;

/// <summary>
/// E20.c2b T1 (docs/plan-e20-heros.md section 1.12.1, tests e1 to e4): the entry of the damage of the hero (<see cref="HeroInjury.Enter"/>, <c>0x80031A68</c>) against the 105 vectors of
/// <c>docs/plan-e20-annexe/e20-c2b/oracle/out/vectors_entry.json</c>: each is ONE call of the real code of <c>ALUN_CD.EXE</c> (France) in the hurt-hero world of the E20.c2 oracle, with the return and the life read
/// back (and every changed word of the hero, the toucher and the stats block). The vectors are the ground truth: the expected values below are theirs, never computed by the code under test. The vectors
/// cross-check the tick-level cases A1, A2 (the armour), A4 (the bearings), B1 (the cost by the boots), B2 (the floor) and D1 (see <c>vectors_entry-crosscheck.txt</c>).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroInjuryTests
{
    private const uint OrcAttribute = 2;
    private const int OrcPower = 4;

    // -----------------------------------------------------------------------------------------
    // The vectors.
    // -----------------------------------------------------------------------------------------

    private static JsonElement Load()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c2b", "oracle", "out", "vectors_entry.json");
            if (File.Exists(candidate))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                return document.RootElement.Clone();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c2b/oracle/out/vectors_entry.json' found above '{AppContext.BaseDirectory}': this test needs the vectors of E20.c2b");
    }

    private static JsonElement Vector(string name) => Load().GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("name").GetString() == name);

    private static IEnumerable<object[]> Names(Func<string, bool> keep)
        => Load().GetProperty("vectors").EnumerateArray().Select(v => v.GetProperty("name").GetString()!).Where(keep).Select(n => new object[] { n });

    /// <summary>The vectors of the toucher branch, but the real Beannoid of the map (its attack is the spawner's, covered by the arena tests of b1).</summary>
    public static IEnumerable<object[]> ToucherNames() => Names(n => n.StartsWith("T-", StringComparison.Ordinal) && n != "T-beannoid-real");

    public static IEnumerable<object[]> BearingNames() => Names(n => n.StartsWith("B-", StringComparison.Ordinal));

    public static IEnumerable<object[]> CellNames() => Names(n => n.StartsWith("H-", StringComparison.Ordinal) || n == "N-bit100-set"); // the bit 0x100 alone is the cost 2.

    public static IEnumerable<object[]> NothingNames() => Names(n => n.StartsWith("N-", StringComparison.Ordinal) && n != "N-bit100-set");

    private sealed class Rig
    {
        public EntityScriptProxy Hero = null!;
        public EntityScriptProxy? Toucher;
        public BalanceRecord Sheet = null!;
        public int Boots;
        public int ExpectedReturn;
        public int ExpectedHp;
        public int StartHp;
    }

    private static EntityScriptProxy NewProxy(string name)
    {
        var entity = new Entity { Name = name, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        return (EntityScriptProxy)entity.GameplayProxy;
    }

    /// <summary>The state the oracle called the function in, built from the inputs of the vector (the hero's sheet is the vector's eleven responses).</summary>
    private static Rig Build(JsonElement vector)
    {
        var inputs = vector.GetProperty("inputs");
        var outputs = vector.GetProperty("outputs");
        var responses = inputs.GetProperty("hero_sheet_r").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
        var rig = new Rig
        {
            Sheet = new BalanceRecord(0, 14, 10, responses, Array.Empty<BalanceAttack>(), null),
            Boots = inputs.GetProperty("boots").GetInt32(),
            ExpectedReturn = outputs.GetProperty("ret").GetInt32(),
            ExpectedHp = outputs.GetProperty("hero_hp_after").GetInt32(),
            StartHp = inputs.GetProperty("hero_hp_before").GetInt32(),
        };

        var hero = NewProxy("hero");
        hero.Hp = rig.StartHp;
        hero.PosX = 232 << 16;
        hero.PosY = 96 << 16;
        hero.TargetDirection = inputs.GetProperty("hero_8C").GetUInt32();
        hero.CombinedVramFlagsOR = inputs.GetProperty("hero_180").GetUInt32();
        hero.FrameCollisionTickCounter = inputs.GetProperty("hero_1D0").GetInt32();
        hero.DamagedTickCounter = inputs.GetProperty("hero_cc1CC").GetInt32();
        rig.Hero = hero;

        if (inputs.GetProperty("toucher").ValueKind == JsonValueKind.String)
        {
            var entity = new Entity { Name = "toucher", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
            entity.Initialize();
            var toucher = (EntityScriptProxy)entity.GameplayProxy;
            var attack = inputs.GetProperty("attack");
            toucher.CurrentAttack = attack.ValueKind == JsonValueKind.Array
                ? new BalanceAttack(attack[0].GetInt32(), attack[1].GetInt32())
                : new BalanceAttack((int)OrcAttribute, OrcPower); // "attack": null on an Orc = its real attack, attribute 0x02 power 4 (results.md (c)).
            var bearing = inputs.GetProperty("bearing_hero_minus_toucher");
            var (vx, vy) = bearing.ValueKind == JsonValueKind.Array ? (bearing[0].GetInt32(), bearing[1].GetInt32()) : (-20, 0); // the oracle's default: the toucher 20 px east of the hero.
            toucher.PosX = hero.PosX - (vx << 16);
            toucher.PosY = hero.PosY - (vy << 16);
            toucher.DamagedTickCounter = inputs.GetProperty("toucher_1CC").GetInt32();
            hero.TouchingEntity = entity;
            rig.Toucher = toucher;
        }

        return rig;
    }

    private static (long, long, long, long, long, long) Fingerprint(EntityScriptProxy hero, EntityScriptProxy? toucher)
        => (hero.TargetDirection, hero.DamagedTickCounter, hero.FrameCollisionTickCounter, hero.CombinedVramFlagsOR, hero.PosX ^ ((long)hero.PosY << 20), toucher == null ? -1 : toucher.DamagedTickCounter + ((long)toucher.PosX ^ toucher.PosY));

    private static void AssertVector(string name)
    {
        var rig = Build(Vector(name));
        var before = Fingerprint(rig.Hero, rig.Toucher);

        var result = HeroInjury.Enter(rig.Hero, rig.Sheet, null, 0, rig.Boots);

        Assert.Equal(rig.ExpectedReturn, result);
        Assert.Equal(rig.ExpectedHp, rig.Hero.Hp);
        Assert.Equal(before, Fingerprint(rig.Hero, rig.Toucher)); // the only word the binary writes is the hero's +0x14 (hero_changed_words): the direction, the counters and the positions are the caller's.
    }

    // -----------------------------------------------------------------------------------------
    // (e1) the damage of the toucher by the armour, the bearing, the cost of the cell by the boots, the floor.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ToucherNames))]
    public void E1_TheToucherBranch_LosesTheLifeOfTheSheetAndReturnsTheDirection_AsTheBinaryDoes(string name) => AssertVector(name);

    [Theory]
    [MemberData(nameof(BearingNames))]
    public void E1_TheBearingAwayFromTheToucher_IsTheDirectionReturned_At14PixelsAndAtOne(string name) => AssertVector(name);

    [Theory]
    [MemberData(nameof(CellNames))]
    public void E1_TheCellBranch_CostsOneTwoOrThree_UnlessTheBootsAreOfLevel3_WithTheFloorAt0AndTheArmourIgnored(string name) => AssertVector(name);

    [Fact]
    public void E1_TheDamageOfTheOrcByTheArmour_Is4_3_2_1_1_3_ForTheKitTheLeatherTheAncientTheSilverNoArmourAndTheBracelet()
    {
        // The A2 oracle's life loss on a 12 life, one Orc touch (attribute 0x02, power 4): written here from the vectors' names, so a renamed vector fails loudly.
        var expected = new Dictionary<string, int> { ["kit"] = 4, ["leather"] = 3, ["ancient"] = 2, ["silver"] = 1, ["noarmour"] = 1, ["bracelet"] = 3 };
        foreach (var (armour, loss) in expected)
        {
            var rig = Build(Vector($"T-orc-{armour}-12-12"));

            HeroInjury.Enter(rig.Hero, rig.Sheet, null, 0, rig.Boots);

            Assert.Equal(12 - loss, rig.Hero.Hp);
        }
    }

    [Fact]
    public void E1_TheCostTable_Is0_1_2_3_NotTheDecompilationsNumbers_AndTheBootsOfLevel3ReturnMinus1WithoutWriting()
    {
        // B1: the cell word 0x80, 0x100, 0x180 cost 1, 2, 3 for the boots of level 0, 1 and 2; the boots of level 3 spare the hero. Life 10.
        foreach (var (word, cost) in new[] { (0x80u, 1), (0x100u, 2), (0x180u, 3) })
        {
            foreach (var boots in new[] { 0, 1, 2 })
            {
                var hero = NewProxy("hero");
                hero.Hp = 10;
                hero.TargetDirection = 24;
                hero.CombinedVramFlagsOR = word;

                var result = HeroInjury.Enter(hero, null, null, 0, boots);

                Assert.Equal(8, result); // (24 + 0x10) & 0x1F
                Assert.Equal(10 - cost, hero.Hp);
            }

            var spared = NewProxy("hero");
            spared.Hp = 10;
            spared.CombinedVramFlagsOR = word;

            Assert.Equal(HeroInjury.NoInjury, HeroInjury.Enter(spared, null, null, 0, 3));
            Assert.Equal(10, spared.Hp);
        }
    }

    // -----------------------------------------------------------------------------------------
    // (e2) a response 0x80 or 0xC0 hurts without taking life; (e3) a bearing of (0, 0).
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void E2_AResponse0x80_ReturnsTheDirection_AndLeavesTheLife()
    {
        foreach (var name in new[] { "T-charm-resp80", "T-resp-80", "T-resp-C0" })
        {
            var rig = Build(Vector(name));

            var result = HeroInjury.Enter(rig.Hero, rig.Sheet, null, 0, rig.Boots);

            Assert.Equal(8, result); // the Orc stands east of the hero: away from it is west (8).
            Assert.Equal(rig.StartHp, rig.Hero.Hp);
        }
    }

    [Fact]
    public void E2_AResponse0x40_TakesTheLifeToZero_AndStillReturnsTheDirection()
    {
        var rig = Build(Vector("T-resp-40"));

        var result = HeroInjury.Enter(rig.Hero, rig.Sheet, null, 0, rig.Boots);

        Assert.Equal(8, result);
        Assert.Equal(0, rig.Hero.Hp);
    }

    [Fact]
    public void E3_ATouchOnTheSamePoint_BearsDirection24()
    {
        var rig = Build(Vector("B-same-0-0"));
        Assert.Equal(rig.Hero.PosX, rig.Toucher!.PosX);
        Assert.Equal(rig.Hero.PosY, rig.Toucher.PosY);

        Assert.Equal(24, HeroInjury.Enter(rig.Hero, rig.Sheet, null, 0, rig.Boots));
    }

    // -----------------------------------------------------------------------------------------
    // (e4) nothing to answer: -1 and nothing written; the sheet-less host.
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(NothingNames))]
    public void E4_WithoutAToucherAndWithoutTheBits0x180_TheEntryReturnsMinus1_AndWritesNothing(string name) => AssertVector(name);

    [Fact]
    public void E4_ABareHero_NullHostValues_ReturnsMinus1_AndWritesNothing()
    {
        var hero = NewProxy("hero");
        hero.Hp = 7;
        hero.TargetDirection = 5;

        var result = HeroInjury.Enter(hero, null, null, 0, 0);

        Assert.Equal(HeroInjury.NoInjury, result);
        Assert.Equal((7, 5u, 0, 0u), (hero.Hp, hero.TargetDirection, hero.DamagedTickCounter, hero.CombinedVramFlagsOR));
    }

    [Fact]
    public void E4_AToucherWithoutASheet_ReturnsTheDirection_AndLeavesTheLife_D_C2B_9()
    {
        // D-C2B-9: a montage without tables (no HeroSheet) keeps the life, as Damage.ResolveBalanceTarget does; the binary would take 1 (documented deviation, invisible in production).
        var rig = Build(Vector("T-orc-kit-12-12"));

        var result = HeroInjury.Enter(rig.Hero, null, null, 0, 0);

        Assert.Equal(8, result);
        Assert.Equal(12, rig.Hero.Hp);
    }
}
