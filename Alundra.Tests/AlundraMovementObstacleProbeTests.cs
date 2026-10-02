#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2b B3 (docs/plan-e19-opcodes.md §1.2h.2, D-E19-27, D-E19-28, D-E19-34, ADR-0022): the movement obstacle probe of the DLL, which
/// the character controller of the engine consults in its field stage (ADR-0047 of the engine). The rule is that of the binary's
/// <c>FindEntityCollisionCandidate</c> (<c>0x80036FE0</c>), semi-open: a flush contact does not block, one unit less does. T-R1 is the rule
/// alone on real dimensions; T-R3, T-R4, T-R-ID, T-R-LIFT, T-REG-Z and T-R9 drive real controllers in a real world, the positions written
/// on the logical fields (whole pixels where a teleport truncates them).
/// </summary>
public sealed class AlundraMovementObstacleProbeTests
{
    // ---- T-R1: the rule, on the real dimensions -------------------------------------------------------------------

    private static AlundraEntityScriptProxy Box(int posX, int posY, int posZ, int sizeX, int sizeY, int sizeZ, uint flags = EntityFlags.Collidable)
    {
        var proxy = new AlundraEntityScriptProxy { Flags = flags };
        AlundraEntitySpawnFactory.SetEntityDimensions(proxy, -10, -7, 0, sizeX, sizeY, sizeZ);
        proxy.PosX = posX;
        proxy.PosY = posY;
        proxy.PosZ = posZ;
        return proxy;
    }

    /// <summary>The hero (21 x 15 x 32) at the candidate position against one villager (20 x 14 x 32) at (<paramref name="villagerX"/>, ...).</summary>
    private static bool HeroBlocked(int heroX, int heroY, int heroZ, int villagerX, int villagerY, int villagerZ)
    {
        var hero = Box(heroX, heroY, heroZ, 21, 15, 32);
        var villager = Box(villagerX, villagerY, villagerZ, 20, 14, 32);
        var found = AlundraEntityCollision.FindEntityCollisionCandidate(hero, heroX, heroY, heroZ, new[] { villager });
        Assert.True(found == null || ReferenceEquals(found, villager));
        return found != null;
    }

    private const int HeroX = 32768000; // 500 px
    private const int HeroY = 19660800; // 300 px
    private const int VillagerX = 32833536; // 501 px
    private const int VillagerY = 19726336; // 301 px
    private const int VillagerZ = 3145729; // 48 px + 1 unit (the spawn offset of the original)

    [Theory]
    // The hero east of the villager: the left edge of the hero is flush with the right edge of the villager (hero at 521 px).
    [InlineData(34144256, HeroY, VillagerZ, false)]
    [InlineData(34144255, HeroY, VillagerZ, true)]
    // The hero west of the villager: his right edge is flush with the left edge of the villager (hero at 480 px).
    [InlineData(31457280, HeroY, VillagerZ, false)]
    [InlineData(31457281, HeroY, VillagerZ, true)]
    // The hero south of the villager (y = 315 px) ...
    [InlineData(HeroX, 20643840, VillagerZ, false)]
    [InlineData(HeroX, 20643839, VillagerZ, true)]
    // ... and north (y = 286 px).
    [InlineData(HeroX, 18743296, VillagerZ, false)]
    [InlineData(HeroX, 18743297, VillagerZ, true)]
    // Z, the hero above the villager (PosZ 3145729): one unit of margin frees it.
    [InlineData(HeroX, HeroY, 5242881, false)]
    [InlineData(HeroX, HeroY, 5242880, true)]
    [InlineData(HeroX, HeroY, 5242879, true)]
    // Z, the hero below the villager.
    [InlineData(HeroX, HeroY, 1048577, false)]
    [InlineData(HeroX, HeroY, 1048578, true)]
    public void TR1_TheSemiOpenRule_FlushContactDoesNotBlock_OneUnitLessBlocks(int heroX, int heroY, int heroZ, bool blocked)
    {
        Assert.Equal(blocked, HeroBlocked(heroX, heroY, heroZ, VillagerX, VillagerY, VillagerZ));
    }

    // Z, the hero at PosZ 3145728 and the villager above him.
    [Theory]
    [InlineData(5242880, false)]
    [InlineData(5242879, true)]
    public void TR1_TheVillagerAboveTheHero_OneUnitOfMarginFreesHim(int villagerZ, bool blocked)
    {
        Assert.Equal(blocked, HeroBlocked(HeroX, HeroY, 3145728, VillagerX, VillagerY, villagerZ));
    }

    [Theory]
    [InlineData(33947648, false)] // Nestus at y = 518 px, south of Meade (504 px): flush.
    [InlineData(33947647, true)]
    [InlineData(33947649, false)]
    public void TR1_NestusAgainstMeade_FlushBlocksNotAndOneUnitLessDoes(int nestusY, bool blocked)
    {
        var meade = Box(62881792, 33030144, VillagerZ, 20, 14, 32);
        var nestus = Box(63438848, nestusY, VillagerZ, 20, 14, 32);

        var found = AlundraEntityCollision.FindEntityCollisionCandidate(nestus, nestus.PosX, nestus.PosY, nestus.PosZ, new[] { meade });

        Assert.Equal(blocked ? meade : null, found);
    }

    [Fact]
    public void TR1_TheSubjectIsNeverItsOwnObstacle_AndTheFirstOverlappingOfTheListWins()
    {
        var hero = Box(32768000, HeroY, VillagerZ, 21, 15, 32);
        var first = Box(32768000, HeroY, VillagerZ, 20, 14, 32);
        var second = Box(32768000, HeroY, VillagerZ, 20, 14, 32);

        Assert.Same(first, AlundraEntityCollision.FindEntityCollisionCandidate(hero, hero.PosX, hero.PosY, hero.PosZ, new[] { hero, first, second }));
        Assert.Same(second, AlundraEntityCollision.FindEntityCollisionCandidate(hero, hero.PosX, hero.PosY, hero.PosZ, new[] { hero, second, first }));
    }

    [Fact]
    public void TR1_AMobilThatIsNotEligible_FindsNoObstacle()
    {
        var villager = Box(32768000, HeroY, VillagerZ, 20, 14, 32);

        var notCollidable = Box(32768000, HeroY, VillagerZ, 21, 15, 32, flags: 0);
        var noEntityCollisionAnimation = Box(32768000, HeroY, VillagerZ, 21, 15, 32);
        noEntityCollisionAnimation.AnimFlags = 0x80;

        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(notCollidable, notCollidable.PosX, notCollidable.PosY, notCollidable.PosZ, new[] { villager }));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(
            noEntityCollisionAnimation, noEntityCollisionAnimation.PosX, noEntityCollisionAnimation.PosY, noEntityCollisionAnimation.PosZ, new[] { villager }));
    }

    [Fact]
    public void TR1_SkippedCandidateFlags_LeaveTheSoulevablesOutOfTheSearch()
    {
        var hero = Box(32768000, HeroY, VillagerZ, 21, 15, 32);
        var crate = Box(32768000, HeroY, VillagerZ, 20, 14, 32, EntityFlags.Collidable | 0x200u);

        Assert.Same(crate, AlundraEntityCollision.FindEntityCollisionCandidate(hero, hero.PosX, hero.PosY, hero.PosZ, new[] { crate }));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(hero, hero.PosX, hero.PosY, hero.PosZ, new[] { crate }, EntityFlags.PickupKindMask));
    }

    // ---- T-REG-Z: the Z of the mover is the logical integer, never the float root -----------------------------------

    [Theory]
    [InlineData(26214401, false)] // the margin of one unit that the entity support of E4.f leaves a supported entity.
    [InlineData(26214400, true)]
    [InlineData(26214399, true)]
    public void TRegZ_APlatformAndAMoverOneUnitAbove_TheRuleKeepsTheOneUnitMargin(int moverPosZ, bool blocked)
    {
        var platform = Box(32768000, HeroY, 24117249, 20, 14, 32);
        var mover = Box(32768000, HeroY, moverPosZ, 20, 14, 32);

        var found = AlundraEntityCollision.FindEntityCollisionCandidate(mover, mover.PosX, mover.PosY, mover.PosZ, new[] { platform });

        Assert.Equal(blocked, found != null);
    }

    // ---- worlds with real controllers -----------------------------------------------------------------------------

    private sealed class Rig
    {
        public required World World { get; init; }

        public required ContactHost Host { get; init; }

        public AlundraEntityScriptProxy Add(string name, int x, int y, int z = 48, int sizeX = 20, int sizeY = 14, uint extraFlags = 0, bool controller = true)
        {
            var proxy = ContactWorld.AddEntity(World, Host, name, x, y, z, -10, -7, 0, sizeX, sizeY, 32, withController: controller);
            proxy.Flags |= extraFlags;
            return proxy;
        }

        public void Integrate() => ContactWorld.Integrate(World, Host);
    }

    private static Rig NewRig()
    {
        var host = new ContactHost();
        var probe = new AlundraMovementObstacleProbe(host);
        return new Rig { World = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 48f }, probe), Host = host };
    }

    private static Entity? ObstacleOf(AlundraEntityScriptProxy proxy)
    {
        var contact = proxy.Controller!.LastContact;
        return contact.H2Obstacle ?? contact.H1Obstacle;
    }

    // ---- T-R3: Nestus against Meade, on the northward step of -1 px per tick -------------------------------------

    [Fact]
    public void TR3_NestusWalksNorthAndStopsAtMeade_ThePositionIsTheContact()
    {
        var rig = NewRig();
        var meade = rig.Add("Meade", 960, 504);
        var nestus = rig.Add("Nestus", 968, 552);
        rig.Integrate();
        var meadeEntity = meade.OwnerEntity;

        for (var tick = 0; tick <= 33; tick++)
        {
            nestus.ForceAdjusted = 0;
            nestus.MoveControllerAndPullPosition(0f, -1f);
            Assert.Null(ObstacleOf(nestus));
            Assert.Equal(0, nestus.ForceAdjusted);
        }

        Assert.Equal(33947648, nestus.PosY);
        Assert.Equal(nestus.PosY - (7 << 16), meade.PosY + (7 << 16)); // Nestus.PosY - 7 px = Meade.PosY + 7 px.

        for (var tick = 34; tick <= 40; tick++)
        {
            nestus.ForceAdjusted = 0;
            nestus.MoveControllerAndPullPosition(0f, -1f);
            Assert.Same(meadeEntity, ObstacleOf(nestus));
            Assert.Equal(1, nestus.ForceAdjusted);
            Assert.Equal(33947648, nestus.PosY);
        }

        Assert.Equal(63438848, nestus.PosX); // 968 px: the walk was a pure northward one.
    }

    [Fact]
    public void TR3_WhenMeadeClearsCollidable_NestusIsNotStoppedAtTheFrameFollowingTheRebuildOfTheList()
    {
        var rig = NewRig();
        var meade = rig.Add("Meade", 960, 504);
        var nestus = rig.Add("Nestus", 968, 552);
        rig.Integrate();
        for (var tick = 0; tick <= 34; tick++)
        {
            nestus.ForceAdjusted = 0;
            nestus.MoveControllerAndPullPosition(0f, -1f);
        }

        Assert.Equal(1, nestus.ForceAdjusted);

        meade.Flags &= ~EntityFlags.Collidable; // 0x63 on the entity; the list still has it until the end of the frame ...
        nestus.ForceAdjusted = 0;
        nestus.MoveControllerAndPullPosition(0f, -1f);
        Assert.Equal(1, nestus.ForceAdjusted);

        rig.Host.Rebuild(); // ... then the list is rebuilt.
        nestus.ForceAdjusted = 0;
        nestus.MoveControllerAndPullPosition(0f, -1f);

        Assert.Equal(0, nestus.ForceAdjusted);
        Assert.Equal(33947648 - 65536, nestus.PosY);
    }

    // ---- T-R4: the contact to the east, flush to one ULP -----------------------------------------------------------

    [Fact]
    public void TR4_AMoverPushingEastEndsFlushAgainstTheObstacle_ReportsItAndRaisesForceAdjustedTheTickAfter()
    {
        var rig = NewRig();
        var mover = rig.Add("Mover", 100, 100);
        var obstacle = rig.Add("Obstacle", 140, 100);
        rig.Integrate();
        const float flush = 120f; // the right edge of the mover (x + 10) meets the left edge of the obstacle (140 - 10).
        var ulp = MathF.BitIncrement(flush) - flush;

        for (var tick = 0; tick < 6; tick++) // 100 -> 118
        {
            mover.ForceAdjusted = 0;
            mover.MoveControllerAndPullPosition(3f, 0f);
            Assert.Null(ObstacleOf(mover));
        }

        mover.ForceAdjusted = 0;
        mover.MoveControllerAndPullPosition(3f, 0f); // asks 118 -> 121: the step is shortened to the contact and still advances.

        var x = ContactWorld.Root(mover).X;
        Assert.InRange(x, flush - ulp, flush + ulp);
        Assert.Same(obstacle.OwnerEntity, ObstacleOf(mover)); // "XCollisionEntity" of the tick that shortens (B4 reads it from here).
        Assert.Equal(0, mover.ForceAdjusted);
        // The binary leaves 0 to 3 units and raises the flag 0 to 2 ticks later; here the contact is exact to a float ULP.

        mover.ForceAdjusted = 0;
        mover.MoveControllerAndPullPosition(3f, 0f); // the next tick: no progress.

        Assert.Equal(1, mover.ForceAdjusted);
        Assert.Same(obstacle.OwnerEntity, ObstacleOf(mover));
        Assert.InRange(ContactWorld.Root(mover).X, flush - ulp, flush + ulp);
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(mover, new[] { obstacle })); // the logical fields: flush does not overlap.
    }

    // ---- T-R-ID: two obstacles on the path: the closest to the contact is the one reported -------------------------

    [Fact]
    public void TRId_TwoObstaclesOnThePath_TheOneReportedIsTheNearestToTheContact()
    {
        var rig = NewRig();
        var mover = rig.Add("Mover", 100, 100);
        var far = rig.Add("Far", 190, 100); // first in the list: blocks the whole step (candidate 180).
        var near = rig.Add("Near", 150, 100); // second: the closest to the contact (130).
        rig.Integrate();
        var ulp = MathF.BitIncrement(130f) - 130f;

        mover.MoveControllerAndPullPosition(80f, 0f);

        Assert.Same(near.OwnerEntity, ObstacleOf(mover));
        Assert.NotSame(far.OwnerEntity, ObstacleOf(mover));
        Assert.InRange(ContactWorld.Root(mover).X, 130f - ulp, 130f + ulp);
    }

    // ---- T-R-LIFT: soulevables are not obstacles, iron-ball walls are (D-E19-28, D-E19-34) -------------------------

    [Fact]
    public void TRLift_ACrateOnThePathDoesNotBlock_AnIronBallWallDoes()
    {
        var rig = NewRig();
        var walker = rig.Add("Walker", 100, 100);
        var crate = rig.Add("Crate", 140, 100, extraFlags: 0x200u); // Flags & 0x600: soulevable.
        var ironWall = rig.Add("Wall", 100, 200); // collidable, no pickup bits: the wall of D-E19-34, solid until E14.
        var walker2 = rig.Add("Walker2", 60, 200);
        rig.Integrate();

        for (var tick = 0; tick < 20; tick++)
        {
            walker.MoveControllerAndPullPosition(3f, 0f);
        }

        Assert.Equal(160f, ContactWorld.Root(walker).X); // through the crate: 100 + 20 * 3.
        Assert.Null(ObstacleOf(walker));

        for (var tick = 0; tick < 20; tick++)
        {
            walker2.MoveControllerAndPullPosition(3f, 0f);
        }

        var ulp = MathF.BitIncrement(80f) - 80f;
        Assert.InRange(ContactWorld.Root(walker2).X, 80f - ulp, 80f + ulp); // 100 - 10 - 10: flush with the wall's left edge.
        Assert.Same(ironWall.OwnerEntity, ObstacleOf(walker2));
        Assert.NotNull(crate);
    }

    // ---- T-REG-Z in a world: a supported mover walks over a platform with the one unit of margin ----------------------

    [Fact]
    public void TRegZ_ASupportedSailorWalksOverItsPlatform_TheOneUnitMarginIsKeptAndNothingBlocks()
    {
        var rig = NewRig();
        // The platform, without controller (its PosZ is not re-pulled from a float root): top at 24117249 + 2097151 = 26214400.
        var platform = rig.Add("Platform", 468, 584, 368, sizeX: 200, sizeY: 60, controller: false);
        platform.PosZ = 24117249;
        var sailor = rig.Add("Sailor", 600, 584, 400, extraFlags: EntityFlags.Gravity);
        sailor.ResyncControllerFromFlags();
        sailor.MapGravityRaw = 4; // the per-tick decay of ForceZ that makes the reach test of the support satisfiable for a resting entity.
        sailor.MapZViscosityRaw = 64;
        sailor.TargetDirection = 8; // west
        sailor.CurrentAnimationId = 1;
        sailor.TargetAnimationId = 1;
        sailor.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [1] = new AnimSetEntry { Anim = 1, Speed = 160, Acceleration = 0 } };
        sailor.PosZ = 26214401; // as the spawn leaves it (the offset of one unit of the original): above the top, so the support finds it.
        rig.Host.Rebuild();
        sailor.PushLogicalPositionToRoot();
        sailor.EvaluateEntitySupport(rig.Host.Collidables, immediateAtSpawn: true);
        Assert.Equal(26214401, sailor.PosZ);
        Assert.True(sailor.WasEntitySupportedLastTick);
        var startX = ContactWorld.Root(sailor).X;

        for (var frame = 0; frame < 20; frame++)
        {
            rig.Integrate();
            Assert.Equal(26214401, sailor.PosZ);
            Assert.Null(ObstacleOf(sailor));
            Assert.True(sailor.WasEntitySupportedLastTick);
        }

        Assert.True(ContactWorld.Root(sailor).X < startX - 20f, "the sailor walked west over the platform, never stopped by it");
    }

    // ---- T-R9: a diagonal push against an entity slides (accepted deviation from the binary) -----------------------

    [Fact]
    public void TR9_AHeroPushingObliquelyAgainstAVillager_SlidesAlongHim_TheBinaryKeepsHimStill()
    {
        var rig = NewRig();
        var hero = rig.Add("Hero", 500, 300, sizeX: 21, sizeY: 15);
        var villager = rig.Add("Villager", 521, 296);
        rig.Integrate();

        for (var tick = 1; tick <= 5; tick++)
        {
            hero.MoveControllerAndPullPosition(139008f / 65536f, -92672f / 65536f); // direction 20, speed 256.
            var root = ContactWorld.Root(hero);
            Assert.Equal(500f, root.X); // X blocked by the villager (flush)
            Assert.Equal(300f - 1.4140625f * tick, root.Y); // Y goes on: -1.4140625 px per tick
            Assert.Same(villager.OwnerEntity, ObstacleOf(hero));
        }

        // Deviation from the binary, where the joint division of both axes keeps the hero still: E19.h (O-E19-28).
    }

    // ---- DrawDebug -----------------------------------------------------------------------------------------------

    private sealed class RecordingDrawer : IPhysicsDebugDrawer
    {
        public List<(Vector3 From, Vector3 To)> Lines { get; } = new();

        public PhysicsDebugDrawModes DebugMode { get; set; }

        public void Draw3dText(ref Vector3 location, string textString)
        {
        }

        public void DrawContactPoint(ref Vector3 pointOnB, ref Vector3 normalOnB, float distance, int lifeTime, Color color)
        {
        }

        public void DrawLine(ref Vector3 from, ref Vector3 to, Color color) => Lines.Add((from, to));

        public void ReportErrorWarning(string warningString)
        {
        }
    }

    [Fact]
    public void DrawDebug_DrawsTheTwelveEdgesOfTheBoxOfEachCollidable_InLogicalPixels()
    {
        var rig = NewRig();
        var meade = rig.Add("Meade", 960, 504);
        rig.Add("Nestus", 968, 552);
        rig.Integrate();
        var drawer = new RecordingDrawer();

        new AlundraMovementObstacleProbe(rig.Host).DrawDebug(drawer);

        Assert.Equal(24, drawer.Lines.Count);
        var min = new Vector3(950f, 497f, 48f);
        var max = new Vector3(970f, 511f, 80f);
        Assert.Contains(drawer.Lines, l => l.From == min || l.To == min);
        Assert.Contains(drawer.Lines, l => l.From == max || l.To == max);
        Assert.NotNull(meade);
    }
}
