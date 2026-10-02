#nullable enable
using System.Collections.Generic;
using System.Text;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.d2c1 (docs/plan-e19-opcodes.md §1.2h.3.1): shared montages of the jump and landing tests. The NPC montage is the one of the
/// plan (annex A.1 and C.valeurs): a real <see cref="World"/> on a flat field of height 0, one controller-driven NPC (a box of 20 x 14 x 32
/// at (200, 100, 0), offsets -10 -7 0) with <see cref="EntityFlags.Gravity"/> set AFTER <see cref="ContactWorld.AddEntity"/> (which clears
/// it), <c>MapGravityRaw</c> 128, <c>MapZViscosityRaw</c> 4096, one entity per world (the host closes the clock memo at each call).
/// </summary>
internal sealed class JumpNpcRig
{
    public const int ImpulseAnimation = 3;

    public required World World { get; init; }

    public required ContactHost Host { get; init; }

    public required AlundraEntityScriptProxy Npc { get; init; }

    /// <summary>One update of the world (one logic tick).</summary>
    public void Update(float elapsed = 0.02f)
    {
        Host.Rebuild();
        World.Update(elapsed);
        Host.Rebuild();
    }

    /// <summary>
    /// Builds the NPC montage and runs ONE settling update at rest (animation 0 current and target). <paramref name="impulse"/> is the
    /// <c>IsZForceApplied</c> of the animation <see cref="ImpulseAnimation"/> (animation 0 has speed 0 and no impulse); the animation
    /// sets are the caller's to extend through <see cref="AlundraEntityScriptProxy.AnimSetsByAnim"/>.
    /// </summary>
    public static JumpNpcRig Build(int impulse = 1360, bool gravity = true, int x = 200, int y = 100, int z = 0)
    {
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 0 }, null);
        var host = new ContactHost();
        var npc = ContactWorld.AddEntity(world, host, "Jumper", x, y, z, -10, -7, 0, 20, 14, 32);
        if (gravity)
        {
            npc.Flags |= EntityFlags.Gravity;
        }

        npc.MapGravityRaw = 128;
        npc.MapZViscosityRaw = 4096;
        npc.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Speed = 0 },
            [ImpulseAnimation] = new AnimSetEntry { Anim = ImpulseAnimation, Speed = 0, IsZForceApplied = impulse },
        };
        npc.CurrentAnimationId = 0;
        npc.TargetAnimationId = 0;
        npc.ResyncControllerFromFlags();

        var rig = new JumpNpcRig { World = world, Host = host, Npc = npc };
        rig.Update(); // the settling update.
        return rig;
    }
}

/// <summary>A synthetic flat field of cells (walkability, ground property and height 0) for the hero montages.</summary>
internal static class FlatCells
{
    /// <summary>A field of <paramref name="width"/> x <paramref name="height"/> cells, every one of walkability 0 and height 0.</summary>
    public static AlundraCellsCollisionField Create(int width = 40, int height = 40)
    {
        var count = width * height;
        string Zeros()
        {
            var sb = new StringBuilder("[");
            for (var i = 0; i < count; i++)
            {
                sb.Append(i == 0 ? "0" : ",0");
            }

            return sb.Append(']').ToString();
        }

        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(width, height) };
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":1,\"cell_count\":" + count + ",\"walkability\":" + Zeros() + ",\"ground_property\":" + Zeros()
            + ",\"slope\":" + Zeros() + ",\"height\":" + Zeros() + ",\"tile_id\":" + Zeros() + ",\"wall_tiles_offset\":" + Zeros()
            + ",\"wall_tiles\":{}}";
        Assert.True(AlundraCellsCollisionField.TryCreate(tileMapData, "flat_cells", out var field));
        return field!;
    }
}
