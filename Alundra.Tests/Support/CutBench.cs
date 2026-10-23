#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using Xunit;
using Alundra.Audio;
using Alundra.Cells;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Session;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e T1 (S-E5): the bench of the cut of the grass, next to <see cref="FlatCells"/>. It builds ONE set of cell records and from it the collision field AND the cell store (the production pairing, the arrays aliased), a host that is
/// both the script host and the world context (the effect stage, the sound player and the entities born by the interface are recorded), and an entity with a stored box. E20.f4 (the charged arrow) reuses it.
/// </summary>
internal sealed class CutBench
{
    public const int Width = 52;
    public const int Height = 60;

    public CellsCollisionField Field { get; }

    public CellStore Store { get; }

    public CutHost Host { get; }

    public EntityScriptProxy Entity { get; }

    private CutBench(CellsCollisionField field, CellStore store, CutHost host, EntityScriptProxy entity)
    {
        Field = field;
        Store = store;
        Host = host;
        Entity = entity;
    }

    /// <summary>One cell of the bench: its walkability byte, its height byte and the tiles of its stack of wall tiles (null: no stack).</summary>
    public readonly record struct Cell(int Walkability, int Height, int[]? Stack = null);

    /// <summary>
    /// A 52 x 60 map of the cells that <paramref name="cell"/> gives (default: all zero), the field and the store on the same records, a host with a sound recorder and a (bank-less) effect stage, and an entity with a frame box:
    /// the stored box is (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) of size (<paramref name="width"/>, <paramref name="depth"/>, <paramref name="height"/>) in raw 16.16 words, as <c>+0x1FC..+0x21C</c>.
    /// </summary>
    public static CutBench Create(Func<int, int, Cell>? cell = null, int x = 0, int y = 0, int z = 0, int width = 0, int depth = 0, int height = 0, bool frame = true, int cutEffect = TileEffect.DefaultCutEffectIndex)
    {
        var count = Width * Height;
        var walk = new int[count];
        var heights = new int[count];
        var offsets = new int[count];
        var stacks = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var c = cell?.Invoke(i % Width, i / Width) ?? default;
            walk[i] = c.Walkability;
            heights[i] = c.Height;
            offsets[i] = c.Stack == null ? -1 : 2;
            if (c.Stack != null)
            {
                stacks.Append(stacks.Length == 0 ? "" : ",").Append('"').Append(i).Append("\":{\"offset\":2,\"tiles\":[").Append(string.Join(",", c.Stack)).Append("]}");
            }
        }

        string Col(int[] a) => "[" + string.Join(",", a) + "]";
        var zeros = Col(new int[count]);
        var tileMapData = new TileMapData { MapSize = new CasaEngine.Core.Math.Size(Width, Height) };
        tileMapData.CustomProperties["AlundraCells"] =
            "{\"map_index\":14,\"cell_count\":" + count + ",\"walkability\":" + Col(walk) + ",\"ground_property\":" + zeros + ",\"slope\":" + zeros + ",\"height\":" + Col(heights)
            + ",\"tile_id\":" + zeros + ",\"wall_tiles_offset\":" + Col(offsets) + ",\"wall_tiles\":{" + stacks + "}}";
        Assert.True(CellsCollisionField.TryCreate(tileMapData, "cut_bench", out var field, out var records));
        Assert.True(CellStore.TryCreate(records!, Width, Height, "cut_bench", out var store));

        var host = new CutHost(field!, store!) { TileCutEffectIndex = cutEffect };
        var entity = new EntityScriptProxy { ScriptHost = host, JoinsBirthTick = true, HasFrameBox = frame, HitBoxX = x, HitBoxY = y, HitBoxZ = z, CollisionWidth = width, CollisionDepth = depth, CollisionHeight = height };
        return new CutBench(field!, store!, host, entity);
    }

    /// <summary>The walkability, the height and the wall-stack word of a cell as the shared arrays hold them now.</summary>
    public (int Walkability, int Height, bool HasStack) Read(int column, int row)
    {
        Assert.True(Field.TryReadBinaryCell(column, row, out var height, out var walkability));
        return (walkability, height, Store.GetWallTileStack(column, row) != null);
    }

    /// <summary>The host of the bench: script host and world context in one, recording what the cut asks for.</summary>
    internal sealed class CutHost : IScriptHost, IEntityWorldContext
    {
        private readonly CellsCollisionField _field;

        public CutHost(CellsCollisionField field, CellStore store)
        {
            _field = field;
            CellMutator = store;
            GameState = new GameState();
            SoundPlayer = Sounds;
            Spawns = new List<(int Index, int X, int Y, int Z)>();
        }

        /// <summary>Everything the cut did, in the order of the calls: a sound, an object born by the interface (the effect is in the stage's queue, not here).</summary>
        public List<string> Journal { get; } = new();

        public RecordingSoundPlayer Sounds { get; } = new();

        public List<(int Index, int X, int Y, int Z)> Spawns { get; }

        public int TileCutEffectIndex { get; set; }

        public GameState GameState { get; }

        public ICellMutator? CellMutator { get; set; }

        public EffectStage? EffectStage { get; set; }

        public ISoundPlayer? SoundPlayer { get; set; }

        public ICollisionField? CollisionField => _field;

        public EntityScriptProxy? ActiveCollisionEntity { get; set; }

        public IReadOnlyList<EntityScriptProxy> Collidables { get; } = new List<EntityScriptProxy>();

        public HeroController? PlayerController => null;

        public IEventProgramRunner Runner { get; } = new NoOpRunner();

        public int LogicTicksThisFrame(float elapsedTime) => 1;

        public void DestroyEntity(EntityScriptProxy entity, int effectId)
        {
        }

        public void DestroyEntity(EntityScriptProxy entity)
        {
        }

        private sealed class NoOpRunner : IEventProgramRunner
        {
            public void RunScript(EntityScriptProxy entity, int programSlot)
            {
            }

            public void RunSpriteEvent(EntityScriptProxy entity)
            {
            }
        }

        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => Born;

        public List<EntityScriptProxy> Born { get; } = new();

        public EntityScriptProxy? PlayerEntity => null;

        public EntityScriptProxy? EntityFollowedByCamera { get; set; }

        public NavigationGrid2D? NavigationGrid => null;

        public void SetForcedCameraLookAt(int x, int y, int z)
        {
        }

        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;

        public EntityScriptProxy? SpawnEntityBySpriteIndex(EntityScriptProxy? parent, bool isMapPage, int index, int x, int y, int z, int direction)
        {
            Spawns.Add((index, x, y, z));
            Journal.Add("spawn(" + index + ")");
            var born = new EntityScriptProxy { Flags = 0xFFFFFFFF, SpriteType = index };
            Born.Add(born);
            return born;
        }
    }
}
