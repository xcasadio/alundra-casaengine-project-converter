#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;
using Alundra.Ai.Enemies;
using Alundra.Ai.Objects;
using Alundra.Combat;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.e0c T3 (docs/plan-e20-heros.md section 1.26, phase C, (d4)): a real arc of the loot. The Beannoid of the record 1 of the map 14 (<c>Overworld 3,1-14</c>, the code <c>0xFC</c> of its header) is killed with the sword on the real hit pass, by the policy
/// of the arc G116 (<see cref="BeannoideGrateArcTests"/>: five hits of 2 points, the death is E1 = <c>DestroyEntity(e, -1)</c>); the object its creation drew (<c>0x47</c>, 10 gildas, ADR-0065) appears at the Beannoid, plays the sound 84,
/// rises, falls and bounces by itself (C255, the gravity of the map), and then either the hero takes it (the money, the sound of the object, the destruction after the state 3) or it is left alone and vanishes at the 600th tick of its life.
/// What is asserted is what the generator's flow cannot change: the kind, the position, the sound, the life and its end.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BeannoideLootArcTests
{
    private const int Item = 0x47;      // 10 gildas, a 600 tick life.
    private const int Gildas10 = 10;
    private const uint Square = 0x80;
    private readonly ITestOutputHelper _output;

    public BeannoideLootArcTests(ITestOutputHelper output) => _output = output;

    private static bool Hittable(EntityScriptProxy target)
        => target.Status == EntityStatus.Normal && target.IsOnGround != 0 && target.FrameCollisionTickCounter == 0 && target.DamagedTickCounter == 0 && (target.HitAnimByte & HitPass.InvulnerableAnimationBit) == 0;

    private sealed class Run : IDisposable
    {
        public ArcRun Arc { get; }
        public RecordingSoundPlayer Sounds { get; } = new();
        public EntityScriptProxy Beannoid { get; }
        public EntityScriptProxy? Drop { get; private set; }
        public int BirthFrame { get; private set; } = -1;
        public (int X, int Y, int Z) DeathAt { get; private set; }
        public List<int> SoundsAtBirth { get; } = new();
        public List<(int Frame, int Life, int State, int Z, int OnGround, EntityStatus Status)> Series { get; } = new();

        public Run(string name)
        {
            Arc = new ArcRun(new ArcSpec("E20e0c-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 19, 34, 3, 3000, RealController: true, Prefabs: true));
            try
            {
                typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Sounds);
                Arc.Give(1);
                Arc.Equip(1);
                for (var i = 0; i < 5; i++)
                {
                    Arc.OneFrame();
                }

                Beannoid = Arc.EntityByRecord(1)!;
                Assert.NotNull(Beannoid);
                Assert.Equal(BeannoideAi.SpriteType, Beannoid.SpriteType);
                Arc.OnFrame = Watch;
            }
            catch
            {
                Arc.Dispose();
                throw;
            }
        }

        public void Dispose() => Arc.Dispose();

        private void Watch()
        {
            if (Drop == null)
            {
                Drop = Arc.Entities.FirstOrDefault(e => e.SpriteType == Item + 0x1E);
                if (Drop != null)
                {
                    BirthFrame = Arc.Frame;
                    DeathAt = (Beannoid.PosX, Beannoid.PosY, Beannoid.PosZ);
                    SoundsAtBirth.AddRange(Sounds.Requests);
                }
            }

            if (Drop != null)
            {
                Series.Add((Arc.Frame, Drop.DelayOrAngleOrEntityId, Drop.ItemState, Drop.PosZ, Drop.IsOnGround, Drop.Status));
            }
        }

        /// <summary>The sword policy of G116: the hero west of the target, facing it, a stroke when it can be hit, a pause of 25 frames after each.</summary>
        public void KillTheBeannoid()
        {
            Arc.PlaceHero((Beannoid.PosX >> 16) - 30, Beannoid.PosY >> 16, unchecked(Beannoid.PosZ + 1) >> 16);
            Arc.Hero.TargetDirection = 24;
            var cooldown = 0;
            while (Drop == null)
            {
                if (Arc.Frame >= Arc.Spec.FrameLimit)
                {
                    Assert.Fail($"the Beannoid is not dead at the frame limit; hp {Beannoid.Hp}, status {Beannoid.Status}");
                }

                if (cooldown > 0)
                {
                    cooldown--;
                    Arc.OneFrame();
                    continue;
                }

                if (!Hittable(Beannoid))
                {
                    Arc.OneFrame();
                    continue;
                }

                var hx = (Beannoid.PosX >> 16) - 30;
                var hy = Beannoid.PosY >> 16;
                var hz = unchecked(Beannoid.PosZ + 1) >> 16;
                for (var settle = 0; settle < 2; settle++)
                {
                    Arc.PlaceHero(hx, hy, hz);
                    Arc.Hero.TargetDirection = 24;
                    Arc.OneFrame();
                }

                if (!Hittable(Beannoid) || Drop != null)
                {
                    continue;
                }

                Arc.PlaceHero(hx, hy, hz);
                Arc.Hero.TargetDirection = 24;
                Arc.Press(Square);
                cooldown = 25;
            }
        }
    }

    [Fact]
    public void TheBeannoidOfTheMap14_DiesByTheSword_ItsDrawnObjectAppearsRisesAndBounces_TheHeroTakesIt_TheMoneyGrows_AndTheObjectGoesAfterItsState3()
    {
        using var run = new Run("beannoide-loot-pickup");
        var beannoid = run.Beannoid;
        Assert.Equal((uint)Item, beannoid.ContentsItemId);
        var money = GameState.Instance.PlayerStats.Money;
        var sound = run.Arc.Proxy.ItemTables!.DropSoundSfxIndex[Item];
        Assert.InRange((int)sound, 52, 54);

        run.KillTheBeannoid();

        var drop = run.Drop!;
        _output.WriteLine($"birth at frame {run.BirthFrame}, death at {run.DeathAt}, sounds at birth [{string.Join(",", run.SoundsAtBirth)}], drop type {drop.SpriteType}");
        Assert.Equal((uint)Item, beannoid.ContentsItemId); // the container keeps its contents.
        Assert.Equal(1u, PickupAi.ReadWord274(drop)); // the kind "dropped".
        Assert.Equal(BeannoideAi.SpriteType, beannoid.SpriteType);
        Assert.Contains(84, run.SoundsAtBirth); // the sound of the drop.
        Assert.Equal((run.DeathAt.X, run.DeathAt.Y), (drop.PosX >> 0, drop.PosY >> 0)); // the Beannoid's pixel position at the tick of its death: the drop is born there.
        Assert.Equal(money, GameState.Instance.PlayerStats.Money); // nothing taken yet.

        // The object rises (the force of 10 pixels a tick), then falls and bounces by itself while the hero stays away.
        run.Arc.PlaceHero(drop.PosX >> 16, (drop.PosY >> 16) + 200, drop.PosZ >> 16);
        for (var i = 0; i < 40; i++)
        {
            run.Arc.OneFrame();
        }

        var heights = run.Series.Select(s => s.Z).ToList();
        Assert.True(heights.Max() > heights.First(), "the object rose");
        Assert.True(run.Series.Any(s => s.OnGround != 0), "the object came back to the ground");
        Assert.True(run.Series.All(s => s.State is 0 or 1), "no pickup yet: the state is 0 (the rise) then 1 (waiting for the contact), never 2 or 3");
        Assert.Equal(money, GameState.Instance.PlayerStats.Money);

        // The hero takes it: placed on the object, the money grows by 10, the sound of the object is asked, the object goes at its state 3.
        var taken = false;
        for (var i = 0; i < 200 && !taken; i++)
        {
            run.Arc.PlaceHero(drop.PosX >> 16, drop.PosY >> 16, unchecked(drop.PosZ + 1) >> 16);
            run.Arc.OneFrame();
            taken = GameState.Instance.PlayerStats.Money != money;
        }

        Assert.True(taken, "the hero took the object");
        Assert.Equal(money + Gildas10, GameState.Instance.PlayerStats.Money);
        Assert.Contains((int)sound, run.Sounds.Requests);
        for (var i = 0; i < 20 && drop.Status != EntityStatus.Destroyed; i++)
        {
            run.Arc.PlaceHero(run.Arc.Hero.PosX >> 16, (run.Arc.Hero.PosY >> 16) + 0, run.Arc.Hero.PosZ >> 16);
            run.Arc.OneFrame();
        }

        Assert.Equal(EntityStatus.Destroyed, drop.Status);
        Assert.DoesNotContain(run.Series, s => s.State == 2); // a dropped object never reaches the state 2 (no message, no fanfare).
        Assert.Equal(money + Gildas10, GameState.Instance.PlayerStats.Money); // taken once.
    }

    [Fact]
    public void TheDroppedObject_LeftAlone_VanishesAtTheEndOfItsLife_WithoutGivingAnything()
    {
        using var run = new Run("beannoide-loot-expiry");
        var money = GameState.Instance.PlayerStats.Money;

        run.KillTheBeannoid();

        var drop = run.Drop!;
        var birth = run.BirthFrame;
        for (var i = 0; i < 700 && drop.Status != EntityStatus.Destroyed; i++)
        {
            run.Arc.PlaceHero(drop.PosX >> 16, (drop.PosY >> 16) + 300, 0);
            run.Arc.OneFrame();
        }

        _output.WriteLine($"born at frame {birth}, gone at frame {run.Arc.Frame}; life series start {run.Series.First()} end {run.Series.Last()}");
        Assert.Equal(EntityStatus.Destroyed, drop.Status);
        Assert.Equal(money, GameState.Instance.PlayerStats.Money);
        Assert.InRange(run.Arc.Frame - birth, 590, 620); // the 600 ticks of its life (a tick is a frame of the arc).
        Assert.Equal(600, run.Series.First().Life);
        Assert.True(run.Series.Any(s => s.Life is > 0 and <= 120), "the mark at 120");
    }
}
