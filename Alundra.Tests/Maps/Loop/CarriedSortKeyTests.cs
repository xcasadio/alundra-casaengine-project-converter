#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Maps.Loop;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Maps.Loop;

/// <summary>
/// E20.g1a T2 (docs/plan-e20-heros.md section 1.24.1, D-G-5 and "Révision au HEAD" on the sort key; (w10)): the sort pass of the entities of the frame (<see cref="FrameSyncPasses.RunWallInterleaveSortKeyPass"/>, the draw order of the sprites) draws an object in the hands of
/// another at least as its carrier (<c>0x8003981C-0x80039858</c>: the key of the object is raised to the carrier's when it is below it). Next to it, the object nobody carries and the carried one that is already in front keep their own key. Written BEFORE the raise (W6): red.
/// E20.g1d (D-G1D-2, D-E20-89 (2)) rewrote the last assertions by derivation: the binary raises the collidables only, the DLL raises every held object, so the held one that is not a collidable (the flag 0x80 clear) and the one that asks for no collision (the animation
/// flag 0x80) are now raised to the carrier's key as well.
/// </summary>
public sealed class CarriedSortKeyTests
{
    private const int Px = 65536;

    private static (Entity Entity, EntityScriptProxy Proxy, DepthSortable2DComponent Depth) Make(string name, int posY, uint flags = CarryRig.CrateFlags)
    {
        var entity = new Entity { Name = name, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        var depth = new DepthSortable2DComponent { Elevation = -1 };
        entity.AddComponent(depth);
        entity.Initialize();
        var proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.LogicContextEntity = entity;
        proxy.Status = EntityStatus.Normal;
        proxy.PosY = posY;
        proxy.Flags = flags;
        return (entity, proxy, depth);
    }

    private static int Elevation(int posY) => WallPlacementOverlay.ComputeEntityElevation(posY, 0);

    [Fact]
    public void AnObjectInTheHandsOfTheHero_IsSortedAtLeastAsItsCarrier_WhereTheOthersKeepTheirOwnKey()
    {
        var hero = Make("hero", 160 * Px, flags: 0);
        hero.Proxy.IsPlayer = true;

        // Held, north of the hero (30 rows of 16 px are far: the offset of a lift is 15 px, a throw or a step can put it further): its own key is below the hero's.
        var behind = Make("behind", 100 * Px);
        behind.Proxy.PlatformEntity = hero.Entity;
        var loose = Make("loose", 100 * Px);
        Assert.True(Elevation(behind.Proxy.PosY) < Elevation(hero.Proxy.PosY));

        // Held and already in front of the hero: its own key is above the hero's and stays.
        var inFront = Make("inFront", 220 * Px);
        inFront.Proxy.PlatformEntity = hero.Entity;

        // Held but not a collidable (the flag 0x80 clear) or asking for no collision (the animation flag 0x80): raised as well since E20.g1d (the binary leaves them at their own key: written deviation D-G1D-2).
        var notCollidable = Make("notCollidable", 100 * Px, CarryRig.CrateFlags & ~EntityFlags.Collidable);
        notCollidable.Proxy.PlatformEntity = hero.Entity;
        var noCollision = Make("noCollision", 100 * Px);
        noCollision.Proxy.PlatformEntity = hero.Entity;
        noCollision.Proxy.AnimFlags = 0x80;

        FrameSyncPasses.RunWallInterleaveSortKeyPass(new List<Entity> { hero.Entity, behind.Entity, loose.Entity, inFront.Entity, notCollidable.Entity, noCollision.Entity });

        Assert.Equal(Elevation(160 * Px), hero.Depth.Elevation);
        Assert.Equal(hero.Depth.Elevation, behind.Depth.Elevation);
        Assert.Equal(Elevation(100 * Px), loose.Depth.Elevation);
        Assert.Equal(Elevation(220 * Px), inFront.Depth.Elevation);
        Assert.True(inFront.Depth.Elevation > hero.Depth.Elevation);
        Assert.Equal(hero.Depth.Elevation, notCollidable.Depth.Elevation);
        Assert.Equal(hero.Depth.Elevation, noCollision.Depth.Elevation);
    }
}
