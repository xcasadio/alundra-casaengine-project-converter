#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using static Alundra.Tests.Support.SaveGameDirectorTestSupport;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Combat;
using Alundra.Physics;
using Alundra.Tests.Support;

namespace Alundra.Tests.Entities;

/// <summary>The comparison of the projection of an oracle vector with an entity of the DLL, shared by the tests of the core and of the full path of the interface.</summary>
internal static class SpawnProjectionAsserts
{
    /// <summary>The fields the plan compares (README of the annex). <paramref name="withTerrain"/> false leaves out the ones that depend on the terrain of the field: <c>PosZ</c>, <c>ModdedPosZ</c>, the terrain, the floor and the corner heights.</summary>
    internal static void Check(string id, SpawnProjection p, EntityScriptProxy proxy, ICollisionField? field, Entity hero, Entity? aux, bool withTerrain)
    {
        Assert.True(p.Hp == proxy.Hp, $"{id}: Hp {proxy.Hp} (oracle {p.Hp})");
        Assert.True(p.HpMax == proxy.HpMax, $"{id}: HpMax {proxy.HpMax} (oracle {p.HpMax})");
        Assert.True(p.Ref == proxy.EntityRefId, $"{id}: EntityRefId {proxy.EntityRefId} (oracle {p.Ref})");
        Assert.True(p.Id == proxy.SpriteType, $"{id}: SpriteType {proxy.SpriteType} (oracle {p.Id})");
        Assert.True(p.Flags == proxy.Flags, $"{id}: Flags {proxy.Flags:X} (oracle {p.Flags:X})");
        Assert.True(p.SpriteProgram.SequenceEqual(proxy.SpriteProgramIndexes), $"{id}: SpriteProgramIndexes [{string.Join(",", proxy.SpriteProgramIndexes)}] (oracle [{string.Join(",", p.SpriteProgram)}])");
        Assert.True(p.Program.SequenceEqual(proxy.ProgramIndexes), $"{id}: ProgramIndexes [{string.Join(",", proxy.ProgramIndexes)}] (oracle [{string.Join(",", p.Program)}])");
        Assert.True(p.AnimTarget == proxy.TargetAnimationId, $"{id}: TargetAnimationId {proxy.TargetAnimationId} (oracle {p.AnimTarget})");
        Assert.True(p.DirTarget == proxy.TargetDirection, $"{id}: TargetDirection {proxy.TargetDirection} (oracle {p.DirTarget})");
        Assert.True(p.Px == proxy.PosX && p.Py == proxy.PosY, $"{id}: PosX/PosY {proxy.PosX}/{proxy.PosY} (oracle {p.Px}/{p.Py})");
        Assert.True(p.ModdedX == proxy.ModdedPosX && p.ModdedY == proxy.ModdedPosY, $"{id}: ModdedPosX/Y {proxy.ModdedPosX}/{proxy.ModdedPosY} (oracle {p.ModdedX}/{p.ModdedY})");
        Assert.True(p.ModX == proxy.ModX && p.ModY == proxy.ModY && p.ModZ == proxy.ModZ, $"{id}: ModX/Y/Z {proxy.ModX}/{proxy.ModY}/{proxy.ModZ} (oracle {p.ModX}/{p.ModY}/{p.ModZ})");
        Assert.True(p.Width == proxy.Width && p.Height == proxy.Height && p.Depth == proxy.Depth, $"{id}: Width/Height/Depth {proxy.Width}/{proxy.Height}/{proxy.Depth} (oracle {p.Width}/{p.Height}/{p.Depth})");
        Assert.True(p.TileX == proxy.TileX && p.TileY == proxy.TileY, $"{id}: TileX/TileY {proxy.TileX}/{proxy.TileY} (oracle {p.TileX}/{p.TileY})");
        Assert.True((p.HasAttack != 0) == (proxy.CurrentAttack != null), $"{id}: the attack of the animation 0 ({proxy.CurrentAttack != null}; oracle {p.HasAttack})");
        Assert.True(p.AnimFlags == proxy.HitAnimByte, $"{id}: HitAnimByte {proxy.HitAnimByte} (oracle {p.AnimFlags})");
        Assert.Same(Resolve(p.Parent, hero, aux), proxy.ParentEntity);
        Assert.Same(Resolve(p.Root, hero, aux), proxy.ChildEntity);
        if (!withTerrain)
        {
            return;
        }

        // The binary's Z (and floor, corner heights) is the DLL's plus one raw unit (ADR-0026).
        Assert.True(p.Pz - 1 == proxy.PosZ, $"{id}: PosZ {proxy.PosZ} (oracle {p.Pz} - 1)");
        Assert.True(p.ModdedZ - 1 == proxy.ModdedPosZ, $"{id}: ModdedPosZ {proxy.ModdedPosZ} (oracle {p.ModdedZ} - 1)");
        Assert.True(p.Terrain == proxy.ComputeTerrainHeight(field), $"{id}: terrain {proxy.ComputeTerrainHeight(field)} (oracle {p.Terrain})");
        Assert.True(p.Floor - 1 == proxy.FloorHeight, $"{id}: FloorHeight {proxy.FloorHeight} (oracle {p.Floor} - 1)");
        for (var k = 0; k < 4; k++)
        {
            Assert.True(p.Corners[k] - 1 == proxy.MapHeights[k], $"{id}: MapHeights[{k}] {proxy.MapHeights[k]} (oracle {p.Corners[k]} - 1)");
        }
    }

    private static Entity? Resolve(string who, Entity hero, Entity? aux) => who switch
    {
        "none" => null,
        "hero" => hero,
        "parent" => aux ?? throw new InvalidOperationException("the vector has no spawned parent"),
        _ => throw new InvalidOperationException(who),
    };
}
