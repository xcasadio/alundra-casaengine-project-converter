#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.c2a, the parts that need a real world: the life pulled at the adoption (group E3 of the oracle), kept across a first tick, a load and a second map, the sheet and boots computed at the adoption without the
/// side effects of the fill. The ArcRun montage resets and shares the session singletons, hence the collection.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class HeroLifeArcTests
{
    private static ArcSpec Map14(string name) =>
        new("E20c2a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);

    /// <summary>A real arrival on the map 179 (the preset of AnimationImpulseSpawnTests UJ-7): the new-game kit does not run, the inventory is the one the test gives.</summary>
    private static ArcSpec Inoa179(string name) =>
        new("E20c2a-" + name, "Inoa", "Inoa (inner)-179", new[] { 203, 1651, 1660 }, 0, 0, 0, 100, RealController: true, Prefabs: true,
            Arrival: new ArcArrival((17 * 24 + 12) << 16, (7 * 16 + 8) << 16, 1 << 20, GameState.ResetAnimationId, 0));

    public static IEnumerable<object[]> E3Runs() => HeroLifeTests.RunNames("E3.json");

    [Theory]
    [MemberData(nameof(E3Runs))]
    public void E3_TheHeroIsCreatedFromTheStats_AndTheStatsSurviveTheFirstTick(string run)
    {
        var oracle = HeroLifeTests.RunNamed("E3.json", run);
        var spec = oracle.GetProperty("spec");
        var spawn = oracle.GetProperty("meta").GetProperty("spawn_from_stats");

        using var arc = new ArcRun(Map14(run), _ =>
        {
            // The oracle's order: the real SetPlayerHpMax, then SetPlayerHp.
            PlayerManager.SetPlayerHpMax(ArcRun.State, spec.GetProperty("hpmax").GetInt32());
            PlayerManager.SetPlayerHp(ArcRun.State, (short)spec.GetProperty("hp").GetInt32());
        });

        Assert.Equal((spawn.GetProperty("entity_hp").GetInt32(), spawn.GetProperty("entity_hpmax").GetInt32()), (arc.Hero.Hp, arc.Hero.HpMax));
        Assert.True(arc.Hero.LifePulledFromStats);

        arc.OneFrame();

        Assert.Equal(oracle.GetProperty("hero").GetProperty("hp")[1].GetInt32(), arc.Hero.Hp);
        Assert.Equal(oracle.GetProperty("g").GetProperty("stats_hp")[1].GetInt32(), (int)ArcRun.State.PlayerStats.Hp);
        Assert.Equal(oracle.GetProperty("g").GetProperty("stats_hpmax")[1].GetInt32(), (int)ArcRun.State.PlayerStats.HpMax);
    }

    [Fact]
    public void ALoadAt4Over12_SurvivesTheFirstTick_AndTwoMapsInARow_KeepTheStats()
    {
        // Map 14: the stats as a load leaves them (ApplyPendingLoad runs before the adoption); the hero is created at 4/12 and the first ticks do not put the stats back to 10/10.
        short hp;
        short hpMax;
        using (var first = new ArcRun(Map14("two-maps-1"), _ =>
        {
            PlayerManager.SetPlayerHpMax(ArcRun.State, 12);
            PlayerManager.SetPlayerHp(ArcRun.State, 4);
        }))
        {
            Assert.Equal((4, 12), (first.Hero.Hp, first.Hero.HpMax));
            for (var frame = 0; frame < 30; frame++)
            {
                first.OneFrame();
            }

            (hp, hpMax) = (ArcRun.State.PlayerStats.Hp, ArcRun.State.PlayerStats.HpMax);
            Assert.Equal((4, 12), ((int)hp, (int)hpMax));
        }

        // The second map: the montage resets the session singletons at the start of every arc, so the stats the first map ended with are given back by hand (in the game they are the session's);
        // what is tested is the adoption of the second world and its ticks.
        using var second = new ArcRun(Inoa179("two-maps-2"), _ =>
        {
            PlayerManager.SetPlayerHpMax(ArcRun.State, hpMax);
            PlayerManager.SetPlayerHp(ArcRun.State, hp);
        });
        Assert.Equal((4, 12), (second.Hero.Hp, second.Hero.HpMax));
        for (var frame = 0; frame < 30; frame++)
        {
            second.OneFrame();
        }

        Assert.Equal((4, 12), ((int)ArcRun.State.PlayerStats.Hp, (int)ArcRun.State.PlayerStats.HpMax));
        Assert.Equal((4, 12), (second.Hero.Hp, second.Hero.HpMax));
    }

    [Fact]
    public void TheAdoption_ComputesTheSheetAndTheBootsAtOnce_WithTheNewGameKit()
    {
        using var arc = new ArcRun(Map14("adoption-sheet"));

        var records = arc.Proxy.EquipmentRecords;
        Assert.NotNull(records.HeroSheet);
        Assert.All(records.HeroSheet!.DamageResponses, b => Assert.Equal(0x10, b)); // cloth, dagger, nothing: the kit's sheet, before any tick.
        Assert.Equal(0, records.BootsLevel);
        Assert.Null(records[EquipmentRecords.Armour]); // the map load left the pointers empty, the first tick fills them.
    }

    [Fact]
    public void TheAdoption_DoesNotRewriteTheEquippedAccessory_TheFirstTickDoes_AndUsesTheOldId()
    {
        // K1 rings2: the rings 0x33 and 0x34 owned, 0x33 equipped. The binary's first tick builds its record from the OLD id and rewrites the equipped one with the best of the slot (0x34).
        using var arc = new ArcRun(Inoa179("adoption-rings"), _ =>
        {
            ArcRun.State.NumberOfItems[0x33 * 2 + 1] = 1;
            ArcRun.State.NumberOfItems[0x34 * 2 + 1] = 1;
            ArcRun.State.PlayerStats.ItemId = 0x33;
        });

        Assert.Equal(0x33, ArcRun.State.PlayerStats.ItemId); // the adoption's own fill restored it.
        Assert.Null(arc.Proxy.EquipmentRecords[EquipmentRecords.Accessory]);

        arc.OneFrame();

        Assert.Equal(0x34, ArcRun.State.PlayerStats.ItemId);
        Assert.Equal(0x33, arc.Proxy.EquipmentRecords.ItemIdOf(EquipmentRecords.Accessory));
    }
}
