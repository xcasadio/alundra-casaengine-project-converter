#nullable enable
using System;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Rendering.Effects;
using Alundra.Tests.Rng;
using Alundra.Tests.Support;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, D-E0C-10, D-H1-1, D-H1-2): the bodies of heal of the binary (<c>0x80032E2C</c> to <c>0x80033A2C</c>), written in <c>HeroHeals</c> for the pickups and reused by h1. Their keys against the
/// real code are those of <c>ItemEffectsTests</c> (every body runs through <c>GiveItem</c> there); these tests pin what the vectors cannot name: the life is written on the ENTITY of the hero and never on the stats (D-H1-2),
/// a world without an effect stage still heals, and the amounts at the edges of the minimum.
/// </summary>
[Collection(RandomStaticStateCollection.Name)]
public sealed class HeroHealsTests : IDisposable
{
    private static readonly EffectBank Bank = EffectGolden.BankOf(SaveGameDirectorTestSupport.FindProjectRoot(), 476);

    public HeroHealsTests() => GameRandom.Reset();

    public void Dispose() => GameRandom.Reset();

    private static (PickupHost Host, GameState State, EntityScriptProxy Hero) Mount(int hp, int hpMax, bool withStage = true)
    {
        var host = new PickupHost();
        if (withStage)
        {
            host.Stage = new EffectStage();
            host.Stage.Load(Bank);
        }

        var state = new GameState();
        state.PlayerStats.Hp = 4;
        state.PlayerStats.HpMax = 10;
        var hero = new EntityScriptProxy { Hp = hp, HpMax = hpMax, PosX = 100 << 16, PosY = 100 << 16, PosZ = 0 };
        host.Hero = hero;
        return (host, state, hero);
    }

    [Fact]
    public void EveryBodyOfHeal_WritesTheEntityOfTheHero_AndLeavesTheStatsAlone_WhateverTheHeroCarries()
    {
        var bodies = new Action<PickupHost, GameState, EntityScriptProxy>[]
        {
            (h, s, e) => HeroHeals.SmallHeart(h, s, e), (h, s, e) => HeroHeals.MediumHeart(h, s, e), (h, s, e) => HeroHeals.LargeHeart(h, s, e), (h, s, e) => HeroHeals.Herb(h, s, e),
            (h, s, e) => HeroHeals.LargePotion(h, s, e), (h, s, e) => HeroHeals.SmallPotion(h, s, e), (h, s, e) => HeroHeals.Brew(h, s, e),
        };
        foreach (var pulled in new[] { true, false })
        {
            foreach (var body in bodies)
            {
                var (host, state, hero) = Mount(hp: 1, hpMax: 30);
                hero.LifePulledFromStats = pulled;
                body(host, state, hero);
                Assert.True(hero.Hp > 1, "the entity is healed");
                Assert.Equal((short)4, state.PlayerStats.Hp);
                Assert.Equal((short)10, state.PlayerStats.HpMax);
            }
        }
    }

    [Fact]
    public void WithoutAnEffectStage_TheHealIsDoneAll_TheSame_WhileWithOneTheEffectsAreQueued()
    {
        var (bare, bareState, bareHero) = Mount(hp: 1, hpMax: 30, withStage: false);
        HeroHeals.LargePotion(bare, bareState, bareHero);
        Assert.Equal(30, bareHero.Hp);

        var (host, state, hero) = Mount(hp: 1, hpMax: 30);
        HeroHeals.LargePotion(host, state, hero);
        Assert.Equal(30, hero.Hp);
        Assert.Equal(0, host.Stage!.Reservoir.ActiveCount); // queued, not created.
        host.Stage.RunFrame(0, Array.Empty<bool>());
        Assert.Equal(8, host.Stage.Reservoir.ActiveCount); // the eight rings.
    }

    [Theory]
    [InlineData(8, 5)]    // 8 / 4 = 2 < 5: the minimum
    [InlineData(20, 5)]   // 20 / 4 = 5
    [InlineData(40, 10)]  // 40 / 4 = 10 > 5
    public void TheHerb_HealsAQuarterOfTheMaximum_NeverLessThanFive(int hpMax, int healed)
    {
        var (host, state, hero) = Mount(hp: 1, hpMax: hpMax);
        HeroHeals.Herb(host, state, hero);
        Assert.Equal(1 + healed, hero.Hp);
    }

    [Fact]
    public void TheHealStopsAtTheMaximum_AndAFullHeroStaysFull()
    {
        var (host, state, hero) = Mount(hp: 29, hpMax: 30);
        HeroHeals.LargeHeart(host, state, hero);
        Assert.Equal(30, hero.Hp);

        var (host2, state2, hero2) = Mount(hp: 30, hpMax: 30);
        HeroHeals.SmallHeart(host2, state2, hero2);
        Assert.Equal(30, hero2.Hp);
    }

    [Fact]
    public void TheBrew_FillsTheLifeAndTheMagic_TheLargePotionOnlyTheLife_TheCrystalOnlyTheMagic()
    {
        var (host, state, hero) = Mount(hp: 1, hpMax: 30);
        state.PlayerStats.MpMax = 4;
        state.PlayerStats.Mp = 0;
        HeroHeals.LargePotion(host, state, hero);
        Assert.Equal((30, 0), (hero.Hp, (int)state.PlayerStats.Mp));

        var (host2, state2, hero2) = Mount(hp: 1, hpMax: 30);
        state2.PlayerStats.MpMax = 4;
        HeroHeals.LargeCrystal(host2, state2, hero2);
        Assert.Equal((1, 4), (hero2.Hp, (int)state2.PlayerStats.Mp));

        var (host3, state3, hero3) = Mount(hp: 1, hpMax: 30);
        state3.PlayerStats.MpMax = 4;
        HeroHeals.Brew(host3, state3, hero3);
        Assert.Equal((30, 4), (hero3.Hp, (int)state3.PlayerStats.Mp));
    }
}
