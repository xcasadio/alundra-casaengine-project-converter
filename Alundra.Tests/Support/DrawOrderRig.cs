#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Alundra.Entities;
using Alundra.Maps.Loop;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8): two or three entities without a world, each with the three things the draw order reads: its proxy (position, IDSV, flags, links), its
/// <see cref="DepthSortable2DComponent"/> and a <see cref="RenderProjectionComponent"/> whose translation is set by hand to where the game puts the sprite, <c>(X, -(Y - Z))</c>. The sort pass is run on them
/// and the key the renderer builds is read back on the rendered translation, exactly as <c>AnimatedSpriteComponent</c> builds it.
/// </summary>
internal static class DrawOrderRig
{
    internal const int Px = 65536;

    internal sealed record Piece(Entity Entity, EntityScriptProxy Proxy, DepthSortable2DComponent Depth, RenderProjectionComponent Projection);

    /// <summary>
    /// An entity at <paramref name="posY"/> (16.16) and <paramref name="posZ"/> (16.16, the DLL's), showing a sprite of IDSV <paramref name="idsv"/>. <paramref name="renderZ"/> is the height, in pixels, the sprite is drawn at
    /// (the hero's root follows <c>PosZ / 65536f</c>, the root of a held object <c>PosZ &gt;&gt; 16</c>): null is the exact <c>PosZ / 65536f</c>.
    /// </summary>
    internal static Piece Make(string name, int posY, int posZ = 0, int idsv = 0, uint flags = 0, float? renderZ = null, int posX = 100 * Px, EntityStatus status = EntityStatus.Normal)
    {
        var entity = new Entity { Name = name, GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        var depth = new DepthSortable2DComponent { Elevation = -1 };
        var projection = new RenderProjectionComponent();
        entity.AddComponent(depth);
        entity.AddComponent(projection);
        entity.Initialize();
        var proxy = (EntityScriptProxy)entity.GameplayProxy;
        proxy.LogicContextEntity = entity;
        proxy.RenderProjection = projection;
        proxy.Status = status;
        proxy.PosX = posX;
        proxy.PosY = posY;
        proxy.PosZ = posZ;
        proxy.Flags = flags;
        proxy.IdsvByAnimDirection = new Dictionary<int, int> { [0] = idsv };
        Place(new Piece(entity, proxy, depth, projection), renderZ);
        return new Piece(entity, proxy, depth, projection);
    }

    /// <summary>Puts the rendered translation of <paramref name="piece"/> where the game puts the sprite of its proxy.</summary>
    internal static void Place(Piece piece, float? renderZ = null)
    {
        var z = renderZ ?? piece.Proxy.PosZ / 65536f;
        piece.Projection.LocalTransform.Position = new Vector3(piece.Proxy.PosX / 65536f, -(piece.Proxy.PosY / 65536f - z), 0f);
    }

    /// <summary>The key the renderer builds for the entity, on its rendered translation.</summary>
    internal static RenderSortKey2D KeyOf(Piece piece) => KeyOf(piece.Entity);

    internal static RenderSortKey2D KeyOf(Entity entity)
    {
        var depth = entity.GetComponent<DepthSortable2DComponent>()!;
        var projection = entity.GetComponent<RenderProjectionComponent>()!;
        return depth.BuildSortKey(projection.WorldMatrixNoScale.Translation);
    }

    /// <summary>The pass of the frame over the pieces, in the order given.</summary>
    internal static void Pass(params Piece[] pieces)
    {
        var entities = new List<Entity>();
        foreach (var piece in pieces)
        {
            entities.Add(piece.Entity);
        }

        FrameSyncPasses.RunWallInterleaveSortKeyPass(entities);
    }
}
