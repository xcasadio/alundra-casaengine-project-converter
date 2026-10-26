#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Items;
using Alundra.Entities;
using Alundra.Rendering.Effects;
using Alundra.Rng;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Hero;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h3b T1, phase A (docs/plan-e20-heros.md section 1.30.3, test (c4)): C62 (<c>0x8007A978</c>, slot C), the tick of the bomb of the type 2, called directly on a bomb made by hand: the hole, the fuse, the end of the fuse, the seven sparks, the carried bomb, the released bomb and the free bomb
/// (the single bounce on a wall in the air, the explosion where it stands at the foot of a wall of the class B, the sound <c>0x18</c> at the landing). The expected values are the 25 vectors of the function that the critic of h3 made from the real code of the binary
/// (<c>docs/plan-e20-annexe/e20-h3/critic/results/goldens_critic.json</c>, replayed in T0 from an empty memory), replayed field by field, and hand cases next to them; each case without effect is set next to a positive case in the same test. The generator is written out by hand in this file.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class BombTickTests : IDisposable
{
    private const string Goldens = "e20-h3/critic/results/goldens_critic.json";
    private const uint RandomWord = 0x80098708;
    private const uint EffectSlotAddress = 0x801E8000;
    private const uint Mask = 0x1001;
    private const int Px = CarryBench.Px;

    private readonly ulong _previousSeed = GameRandom.RandomSeed;

    public BombTickTests()
    {
        GameState.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameRandom.RandomSeed = _previousSeed;
        GameState.Instance.ResetForTests();
    }

    private static uint Step(uint state) => unchecked(state * 0x7D2B89DDu + 0xE06A02E7u);

    private static int Hi(uint draw, uint factor) => (int)(((ulong)draw * factor) >> 32);

    private static uint State() => unchecked((uint)GameRandom.RandomSeed);

    private static int Raw(BombVector vector, uint offset) => unchecked((int)vector.Read(offset));

    /// <summary>The words of the corners as the pointers of the vector name them: the 16 bits at each pointer <c>+0x148 + 4 i</c> (0 where the real code did not read them: it stops at the first hit).</summary>
    private static uint[] Corners(BombVector vector)
    {
        var words = new uint[4];
        for (var i = 0; i < 4; i++)
        {
            var pointer = vector.Read(0x148 + 4u * (uint)i);
            if (pointer != 0)
            {
                words[i] = (vector.Word(pointer & ~3u) >> (int)(8 * (pointer & 2))) & 0xFFFF;
            }
        }

        return words;
    }

    private static readonly uint[] NoCorner = new uint[4];

    private static uint[] OneCornerOnTheBand => new[] { 0u, Mask, 0u, 0u };

    /// <summary>A bomb of the type 2 in the status Normal, free (in nobody's hands), at (166, 100, 4) px, with the fuse <paramref name="fuse"/>; the animation asked is <paramref name="animation"/>.</summary>
    private static EntityScriptProxy Bomb(int fuse, uint animation = 0)
    {
        var bomb = CarryBench.Make("bomb", 166 * Px, 100 * Px, 4 * Px);
        bomb.SpriteType = 2;
        bomb.TargetAnimationId = animation;
        BombAi.SetFuse(bomb, fuse);
        return bomb;
    }

    /// <summary>The bomb carried by a hero: both links set; <paramref name="throwCode"/> is the code <c>+0x24</c>.</summary>
    private static (EntityScriptProxy Bomb, EntityScriptProxy Hero) Carried(int fuse, int throwCode = 0)
    {
        var bomb = Bomb(fuse, animation: 99);
        var hero = CarryBench.Make("hero", 160 * Px, 100 * Px, 0);
        hero.IsPlayer = true;
        hero.CarriedEntity = bomb.LogicContextEntity;
        bomb.PlatformEntity = hero.LogicContextEntity;
        bomb.Flags2 = throwCode;
        return (bomb, hero);
    }

    private static BombHandsBench World(bool effectStage = true) => BombHandsBench.Create(effectStage);

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The vectors.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H4_C62_TheTwentyFiveVectorsOfTheOracle_AreReplayedWordForWord_TheStateTheSoundsTheSparksAndTheGenerator()
    {
        var vectors = BombVector.Load(Goldens, "C62");

        Assert.Equal(25, vectors.Count);
        int holes = 0, sparks = 0, sounds = 0, carriedReleased = 0;
        foreach (var vector in vectors)
        {
            var world = World();
            var bomb = Bomb(Raw(vector, 0x274));
            bomb.Flags2 = Raw(vector, 0x24);
            bomb.TargetAnimationId = vector.Read(0x88);
            bomb.ForceAdjusted = Raw(vector, 0x13C);
            bomb.IsOnGround = Raw(vector, 0x144);
            bomb.CombinedVramFlagsAND = vector.Read(0x184);
            bomb.HitCounter = Raw(vector, 0x220);
            bomb.ForceZ = Raw(vector, 0xB8);
            bomb.DelayOrAngleOrEntityId = Raw(vector, 0x278);
            bomb.TargetDirection = vector.Read(0x8C);
            bomb.PosX = Raw(vector, 0x114);
            bomb.PosY = Raw(vector, 0x118);
            bomb.PosZ = Raw(vector, 0x11C) - 1; // the DLL's frame of Z is one raw unit under the binary's.
            EntityScriptProxy? carrier = null;
            if (vector.Read(0x28) != 0)
            {
                carrier = CarryBench.Make("carrier");
                carrier.CarriedEntity = bomb.LogicContextEntity;
                bomb.PlatformEntity = carrier.LogicContextEntity;
            }

            GameRandom.RandomSeed = vector.Word(RandomWord);

            BombAi.Tick(bomb, world.Host, _ => Corners(vector));

            // the destruction with one argument (the hole), and nothing else.
            if (vector.Journal.Any(j => j.Kind == "destroy1"))
            {
                Assert.Equal(new[] { bomb }, world.Host.DestroyedAlone);
                Assert.Equal(Raw(vector, 0x274), BombAi.Fuse(bomb));
                Assert.Empty(world.Effects());
                Assert.Empty(world.Sounds);
                holes++;
                continue;
            }

            Assert.Empty(world.Host.DestroyedAlone);
            Assert.Equal(vector.After(0x274), unchecked((uint)BombAi.Fuse(bomb)));
            Assert.Equal(vector.Wrote(0x10) ? EntityStatus.Deactivated : EntityStatus.Normal, bomb.Status);
            Assert.Equal(vector.After(0x88), bomb.TargetAnimationId);
            Assert.Equal(vector.After(0x24), unchecked((uint)bomb.Flags2));
            Assert.Equal(vector.After(0x278), unchecked((uint)bomb.DelayOrAngleOrEntityId));
            Assert.Equal(vector.After(0x8C), bomb.TargetDirection);
            Assert.Equal(vector.After(0x28) == 0, bomb.PlatformEntity == null);
            if (carrier != null && vector.After(0x28) == 0)
            {
                Assert.Null(carrier.CarriedEntity); // the carrier's link is cleared with the bomb's.
                carriedReleased++;
            }

            // the sounds and the effects of the journal, in order.
            var journalSounds = vector.Journal.Where(j => j.Kind == "sound").Select(j => (int)j.Arguments[0]).ToArray();
            Assert.Equal(journalSounds, world.Sounds);
            sounds += journalSounds.Length;
            var journalEffects = vector.Journal.Where(j => j.Kind == "effect").ToArray();
            var slots = world.Effects();
            Assert.Equal(journalEffects.Length, slots.Length);
            foreach (var (call, slot) in journalEffects.Zip(slots))
            {
                Assert.Equal((false, (int)call.Arguments[1], (int)call.Arguments[2], unchecked((int)call.Arguments[3]), unchecked((int)call.Arguments[4]), unchecked((int)call.Arguments[5])), (slot.TargetMapTable, slot.TargetEffect, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
                Assert.Equal(9, slot.TargetEffect);
            }

            // the generator and the forces of the sparks: two draws per effect obtained, none otherwise.
            var expectedState = vector.Writes.TryGetValue(RandomWord, out var after) ? after : vector.Word(RandomWord);
            if (vector.Reads.ContainsKey(RandomWord) || vector.Writes.ContainsKey(RandomWord))
            {
                Assert.Equal(expectedState, State());
            }

            if (vector.Writes.ContainsKey(EffectSlotAddress + 0x54u))
            {
                var spark = Assert.Single(slots);
                Assert.Equal(vector.Writes[EffectSlotAddress + 0x54u], unchecked((uint)spark.ForceX));
                Assert.Equal(vector.Writes[EffectSlotAddress + 0x58u], unchecked((uint)spark.ForceY));
                sparks++;
            }
        }

        Assert.Equal((16, 3, 3, 2), (holes, sparks, sounds, carriedReleased)); // the holes, the sparks, the sounds and the releases are in the sample.
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The hole, the fuse.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H4_C62_ABombOnAHole_IsDestroyedAtOnceWithOneArgument_BeforeTheFuseMoves_NextToTheOnesWithNoHoleBit()
    {
        foreach (var holes in new[] { 4u, 0x84u, 0xFFFFFFFFu, 5u })
        {
            var world = World();
            var bomb = Bomb(100);
            bomb.CombinedVramFlagsAND = holes;

            BombAi.Tick(bomb, world.Host, _ => NoCorner);

            Assert.Equal(new[] { bomb }, world.Host.DestroyedAlone);
            Assert.Equal(100, BombAi.Fuse(bomb));
            Assert.Equal(EntityStatus.Normal, bomb.Status);
        }

        foreach (var holes in new[] { 0u, 1u, 0x80u, 0xFBu })
        {
            var world = World();
            var bomb = Bomb(100);
            bomb.CombinedVramFlagsAND = holes;

            BombAi.Tick(bomb, world.Host, _ => NoCorner);

            Assert.Empty(world.Host.DestroyedAlone);
            Assert.Equal(99, BombAi.Fuse(bomb));
        }
    }

    [Theory]
    [InlineData(240, 239)]
    [InlineData(100, 99)]
    [InlineData(61, 60)]
    [InlineData(1, 0)]
    public void H4_C62_EachTickTheFuseGoesDownByOne_WithNoSparkNoStatusAndNoSoundBeforeTheLast60(int fuse, int expected)
    {
        var world = World();
        var bomb = Bomb(fuse);

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(expected, BombAi.Fuse(bomb));
        Assert.Equal(EntityStatus.Normal, bomb.Status);
        Assert.Empty(world.Effects());
        Assert.Empty(world.Sounds);
    }

    [Fact]
    public void H4_C62_AFuseOf240_PutsTheStatus3_AtItsTick241_TheFuseIsThenMinus1()
    {
        var world = World();
        var bomb = Bomb(240);
        var ticks = 0;

        while (bomb.Status == EntityStatus.Normal)
        {
            BombAi.Tick(bomb, world.Host, _ => NoCorner);
            ticks++;
            Assert.True(ticks < 1000, "the fuse never ends");
        }

        Assert.Equal(241, ticks); // 240 down to 0 is 241 ticks: the tick that finds the fuse at 0 puts the status 3.
        Assert.Equal(-1, BombAi.Fuse(bomb));
        Assert.Equal(EntityStatus.Deactivated, bomb.Status);
    }

    [Fact]
    public void H4_C62_TheFuseAtZero_PutsTheStatus3_ClearsBothLinks_AndRunsTheFreeBranchWithTheLinkGone()
    {
        var (bomb, hero) = Carried(fuse: 0, throwCode: 0);
        var world = World();

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(EntityStatus.Deactivated, bomb.Status);
        Assert.Equal(-1, BombAi.Fuse(bomb));
        Assert.Null(bomb.PlatformEntity);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(99u, bomb.TargetAnimationId); // the free branch finds a bomb in flight that touches nothing: it writes nothing, and the carried branch is not run.
        Assert.Empty(world.Effects());
        Assert.Empty(world.Sounds);

        // next to it: the fuse at 1 does not.
        var (other, otherHero) = Carried(fuse: 1);
        BombAi.Tick(other, world.Host, _ => NoCorner);
        Assert.Equal(EntityStatus.Normal, other.Status);
        Assert.Same(other.LogicContextEntity, otherHero.CarriedEntity);
        Assert.Equal(0, BombAi.Fuse(other));
    }

    [Fact]
    public void H4_C62_TheFuseAtZeroOfABombNobodyCarries_PutsTheStatus3_WithoutTouchingAnyLink()
    {
        var world = World();
        var bomb = Bomb(0);

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(EntityStatus.Deactivated, bomb.Status);
        Assert.Equal(-1, BombAi.Fuse(bomb));
        Assert.Null(bomb.PlatformEntity);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The sparks.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H4_C62_ASparkIsMadeWhenTheFuseIsUnder60AndAMultipleOf8_SevenInALife_AtTheBombPlus8PxHigh_Effect9_AndTwoDrawsGiveItsForces()
    {
        var seed = 0x2468ACE1u;
        var world = World();
        var bomb = Bomb(240);
        GameRandom.RandomSeed = seed;
        var made = new List<int>();

        for (var tick = 0; tick < 240; tick++)
        {
            var fuse = BombAi.Fuse(bomb);
            var before = world.Effects().Length;
            BombAi.Tick(bomb, world.Host, _ => NoCorner);
            var after = world.Effects().Length;
            if (after != before)
            {
                made.Add(fuse);
            }
        }

        Assert.Equal(new[] { 56, 48, 40, 32, 24, 16, 8 }, made);
        var slots = world.Effects();
        Assert.Equal(7, slots.Length);
        var state = seed;
        foreach (var slot in slots)
        {
            var r1 = Step(state);
            var r2 = Step(r1);
            state = r2;
            Assert.Equal((false, 9, 0, bomb.PosX, bomb.PosY, bomb.PosZ + 1 + 0x80000), (slot.TargetMapTable, slot.TargetEffect, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
            Assert.Equal(Hi(r1, 0x30001) - 0x18000, slot.ForceX);
            Assert.Equal(Hi(r2, 0x20001) - 0x10000, slot.ForceY);
        }

        Assert.Equal(state, State()); // fourteen draws, no more.
    }

    [Fact]
    public void H4_C62_TheFuseAndTheSparkCondition_SparkIffUnder60AndAMultipleOf8_TheNegativeMultiplesToo_NeverAtZero()
    {
        for (var fuse = -24; fuse <= 70; fuse++)
        {
            var world = World();
            var bomb = Bomb(fuse);
            GameRandom.RandomSeed = 1234;

            BombAi.Tick(bomb, world.Host, _ => NoCorner);

            var expected = fuse != 0 && fuse < 0x3C && (fuse & 7) == 0;
            Assert.True(expected == (world.Effects().Length == 1), $"fuse {fuse}");
            Assert.True(expected == (State() != 1234), $"fuse {fuse}: the draws");
            Assert.Equal(fuse - 1, BombAi.Fuse(bomb));
        }
    }

    [Fact]
    public void H4_C62_ASparkThatIsNotObtained_DrawsNothing_FullReservoirOrNoStage_AndTheBombGoesOn()
    {
        // positive case: a stage with room draws twice.
        var roomy = World();
        var bomb = Bomb(40);
        GameRandom.RandomSeed = 777;
        BombAi.Tick(bomb, roomy.Host, _ => NoCorner);
        Assert.Equal(Step(Step(777)), State());

        // a full reservoir (the 128 slots busy): no effect, no draw, the fuse goes down all the same.
        var full = World();
        for (var i = 0; i < EffectReservoir.SlotCount; i++)
        {
            Assert.NotNull(full.Host.EffectStage!.CreateNow(false, 9, 0, 0, 0, 0));
        }

        var other = Bomb(40);
        GameRandom.RandomSeed = 777;
        BombAi.Tick(other, full.Host, _ => NoCorner);
        Assert.Equal(777u, State());
        Assert.Equal(39, BombAi.Fuse(other));

        // no stage at all.
        var bare = World(effectStage: false);
        var third = Bomb(40);
        GameRandom.RandomSeed = 777;
        BombAi.Tick(third, bare.Host, _ => NoCorner);
        Assert.Equal(777u, State());
        Assert.Equal(39, BombAi.Fuse(third));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The bomb in the hands.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H4_C62_ACarriedBombWithNoCodeOfThrow_AsksTheAnimation0_AndKeepsBothLinks()
    {
        var (bomb, hero) = Carried(fuse: 100, throwCode: 0);
        var world = World();

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(0u, bomb.TargetAnimationId);
        Assert.Same(hero.LogicContextEntity, bomb.PlatformEntity);
        Assert.Same(bomb.LogicContextEntity, hero.CarriedEntity);
        Assert.Equal(99, BombAi.Fuse(bomb));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(0x55)]
    public void H4_C62_ACarriedBombWithAnyCodeOfThrow_IsReleased_TheAnimation2_TheCodeBackTo0_BothLinksCleared_AndItsFlagsAreNotTouched(int code)
    {
        var (bomb, hero) = Carried(fuse: 100, throwCode: code);
        bomb.Flags = 0x132134;
        var world = World();

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(2u, bomb.TargetAnimationId);
        Assert.Equal(0, bomb.Flags2);
        Assert.Null(bomb.PlatformEntity);
        Assert.Null(hero.CarriedEntity);
        Assert.Equal(0x132134u, bomb.Flags); // unlike C23, C62 does not rewrite the flags of the released object.
        Assert.Equal(EntityStatus.Normal, bomb.Status);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The free bomb.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void H4_C62_AFreeBombWhoseAnimationAskedIs0_DoesNothing_EvenOnTheGroundOrBlocked()
    {
        foreach (var blocked in new[] { 0, 1 })
        {
            var world = World();
            var bomb = Bomb(100, animation: 0);
            bomb.ForceAdjusted = blocked;
            bomb.IsOnGround = 1;
            bomb.HitCounter = 1;

            BombAi.Tick(bomb, world.Host, _ => OneCornerOnTheBand);

            Assert.Equal(EntityStatus.Normal, bomb.Status);
            Assert.Empty(world.Sounds);
            Assert.Equal(0u, bomb.TargetAnimationId);
        }
    }

    [Fact]
    public void H4_C62_BlockedInTheAirGoingUp_BouncesOnceOnAWall_TheHalfTurn_TheEffect9_AndNeverASecondTime_NoSound()
    {
        var world = World();
        var bomb = Bomb(100, animation: 2);
        bomb.ForceAdjusted = 1;
        bomb.IsOnGround = 0;
        bomb.ForceZ = 5;
        bomb.TargetDirection = 0x18;
        bomb.DelayOrAngleOrEntityId = 0;
        GameRandom.RandomSeed = 4321;

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(1, bomb.DelayOrAngleOrEntityId);
        Assert.Equal(0x08u, bomb.TargetDirection); // (0x18 + 0x10) & 0x1F
        Assert.Equal(2u, bomb.TargetAnimationId);
        var slot = Assert.Single(world.Effects());
        Assert.Equal((false, 9, 0, bomb.PosX, bomb.PosY, bomb.PosZ + 1 + 0x80000), (slot.TargetMapTable, slot.TargetEffect, slot.TargetAnimation, slot.X, slot.Y, slot.Z));
        Assert.Equal(4321u, State()); // the bounce spark draws nothing.
        Assert.Empty(world.Sounds);

        // the second tick against the wall: the lock holds, nothing more.
        BombAi.Tick(bomb, world.Host, _ => NoCorner);
        Assert.Equal(0x08u, bomb.TargetDirection);
        Assert.Single(world.Effects());
        Assert.Equal(1, bomb.DelayOrAngleOrEntityId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void H4_C62_BlockedInTheAirButNotGoingUp_DoesNotBounce(int forceZ)
    {
        var world = World();
        var bomb = Bomb(100, animation: 2);
        bomb.ForceAdjusted = 1;
        bomb.IsOnGround = 0;
        bomb.ForceZ = forceZ;
        bomb.TargetDirection = 0x18;

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(0, bomb.DelayOrAngleOrEntityId);
        Assert.Equal(0x18u, bomb.TargetDirection);
        Assert.Empty(world.Effects());
    }

    [Fact]
    public void H4_C62_BlockedOnTheGround_StopsTheFlightAnimation_AndExplodesWhereItStandsAtTheFootOfAWallOfTheClassB()
    {
        // a corner of the four on the band (word & 0x1001 == 0x1001): the status 3, and the animation 0.
        var wall = World();
        var exploding = Bomb(100, animation: 2);
        exploding.ForceAdjusted = 1;
        exploding.IsOnGround = 1;

        BombAi.Tick(exploding, wall.Host, _ => OneCornerOnTheBand);

        Assert.Equal(EntityStatus.Deactivated, exploding.Status);
        Assert.Equal(0u, exploding.TargetAnimationId);
        Assert.Empty(wall.Sounds);

        // next to it: no corner on the band, or only one of the two bits: the animation 0 and the status stays.
        foreach (var words in new[] { NoCorner, new[] { 0x1000u, 0x0001u, 0x0FFEu, 0u } })
        {
            var open = World();
            var stays = Bomb(100, animation: 2);
            stays.ForceAdjusted = 1;
            stays.IsOnGround = 1;

            BombAi.Tick(stays, open.Host, _ => words);

            Assert.Equal(EntityStatus.Normal, stays.Status);
            Assert.Equal(0u, stays.TargetAnimationId);
        }
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void H4_C62_LandedOrTouching_PlaysTheSound0x18_ClearsTheBounceLock_AndAsksTheAnimation0(int onGround, int hits)
    {
        var world = World();
        var bomb = Bomb(100, animation: 2);
        bomb.ForceAdjusted = 0;
        bomb.IsOnGround = onGround;
        bomb.HitCounter = hits;
        bomb.DelayOrAngleOrEntityId = 1;

        BombAi.Tick(bomb, world.Host, _ => NoCorner);

        Assert.Equal(new[] { 0x18 }, world.Sounds);
        Assert.Equal(0, bomb.DelayOrAngleOrEntityId);
        Assert.Equal(0u, bomb.TargetAnimationId);
        Assert.Equal(EntityStatus.Normal, bomb.Status);
    }

    [Fact]
    public void H4_C62_StillFlyingAndTouchingNothing_DoesNothing_AndLandedOverAnyCornerOnTheBandKeepsTheFlightAnimation()
    {
        var flying = World();
        var bomb = Bomb(100, animation: 2);
        bomb.IsOnGround = 0;
        bomb.HitCounter = 0;
        bomb.DelayOrAngleOrEntityId = 1;

        BombAi.Tick(bomb, flying.Host, _ => OneCornerOnTheBand);

        Assert.Empty(flying.Sounds);
        Assert.Equal(1, bomb.DelayOrAngleOrEntityId);
        Assert.Equal(2u, bomb.TargetAnimationId);

        // landed over a corner on the band: the sound and the lock are done, the animation stays.
        var landed = World();
        var onBand = Bomb(100, animation: 2);
        onBand.IsOnGround = 1;
        onBand.DelayOrAngleOrEntityId = 1;

        BombAi.Tick(onBand, landed.Host, _ => OneCornerOnTheBand);

        Assert.Equal(new[] { 0x18 }, landed.Sounds);
        Assert.Equal(0, onBand.DelayOrAngleOrEntityId);
        Assert.Equal(2u, onBand.TargetAnimationId);
        Assert.Equal(EntityStatus.Normal, onBand.Status);
    }
}
