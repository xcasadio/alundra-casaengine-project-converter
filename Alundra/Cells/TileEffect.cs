#nullable enable
using System;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using CasaEngine.Framework.Assets.TileMap;

namespace Alundra.Cells;

/// <summary>
/// E20.e (docs/plan-e20-heros.md section 1.27, phase A - nothing in production calls it yet): <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) of ALUN_CD.EXE (France), the cut of the grass. Its four callers (the hero in
/// <c>CreatePlayerAnimationEffects</c>, the wave C61, the charged arrow C94, the bomb E12) share this one entry, <see cref="Trigger"/>.
/// <list type="bullet">
/// <item><description>Nothing happens without a frame box (<see cref="EntityScriptProxy.HasFrameBox"/>, <c>+0x1D4</c>); the box is the STORED one (<c>+0x1FC</c>.., written at the end of the previous tick, measured in
/// docs/plan-e20-annexe/e20-e/rev3/oracle/out/latency.txt).</description></item>
/// <item><description>The four corners of the box are visited in the order (columnA, rowA), (columnB, rowA), (columnA, rowB), (columnB, rowB), ONE AT A TIME, on the cells as the previous corners left them: columns
/// <c>T[(short)(x &gt;&gt; 16)]</c> and <c>T[(x + width) &gt;&gt; 16]</c> with <c>T[p] = p / 24</c>, rows <c>y &gt;&gt; 20</c> and <c>(y + depth) &gt;&gt; 20</c> (floor), each clamped to 0 to <c>0x33</c> and 0 to <c>0x3B</c> for the
/// cell. Only the bit <c>0x02</c> of the walkability counts. The band of Z: the cell is skipped when <c>zc &lt; bz</c> or <c>bz + height &lt; zc</c> (signed), <c>zc = (height byte &lt;&lt; 20) + 0x80000</c>; the DLL's Z is
/// the binary's minus one (ADR-0026), so <c>bz = HitBoxZ + 1</c> (measured on the machine, rev3/oracle/out/zedge.txt).</description></item>
/// <item><description>Each cuttable corner is cut at once (<see cref="ICellMutator.CutCell"/>), so a cell cut by an earlier corner is not cut twice. Then, for each cut, in order (S-E1): the global effect
/// <c>TileCutEffectId</c> through the effect queue (no number drawn), the loot (<see cref="LootDrop.SpawnRandomLoot"/> with the code <c>0xFF</c>: 1 or 2 words of the generator, sound 84 if an object appears), the sound 31.
/// The position is the centre of the cell with the UNCLAMPED column and row (<c>col * 0x180000 + 0xC0000</c>, <c>row * 0x100000 + 0x80000</c>), the Z the cell's height byte shifted by 20 (the +0x80000 is not added).</description></item>
/// </list>
/// Without the world context, the cell mutator or the collision field of the host, nothing is cut and nothing is played (S-E2). A host with a mutator and no effect stage cuts, rolls the loot and plays the sound.
/// Outside the table of the binary (a column before 0 or from 1248 pixels) the binary reads memory that is not defined; here a pixel before 0 is the column 0 and a pixel past the table keeps the formula.
/// </summary>
public static class TileEffect
{
    /// <summary>The global effect of the cut when the map does not say (the byte 9 of the header is 2 on the 483 maps).</summary>
    public const int DefaultCutEffectIndex = 2;

    /// <summary>The name of the map property that carries the byte 9 of the header (exported since E20.eR, ADR-0079).</summary>
    public const string CutEffectPropertyName = "TileCutEffectId";

    /// <summary>The sound of a cut (<c>PlaySoundEffect(0x1F)</c>, <c>0x8003A990</c>).</summary>
    public const int CutSfx = 0x1F;

    /// <summary>The code given to the loot roll of a cut (<c>SpawnRandomLoot(0xFF, ...)</c>, <c>0x8003A988</c>).</summary>
    public const int LootCode = 0xFF;

    private const int MaxColumn = 0x33;
    private const int MaxRow = 0x3B;
    private const int PixelsPerColumn = 24;

    /// <summary>
    /// The effect of a cut for a map: its property <see cref="CutEffectPropertyName"/> (a string like <c>Gravity</c>), 2 when the map or the property is missing or not a number.
    /// </summary>
    public static int ResolveCutEffectIndex(TileMapData? tileMapData)
    {
        if (tileMapData != null && tileMapData.CustomProperties.TryGetValue(CutEffectPropertyName, out var raw) && int.TryParse(raw, out var effect))
        {
            return effect;
        }

        return DefaultCutEffectIndex;
    }

    /// <summary>The binary's table <c>T[p] = p / 24</c> (<c>0x80127168</c>, built at <c>0x8003B190</c>): the column of a pixel, not clamped.</summary>
    internal static int ColumnOf(int pixel) => pixel <= 0 ? 0 : pixel / PixelsPerColumn;

    /// <summary>
    /// <c>CheckAndTriggerTileEffect(entity)</c>: cuts the grass under the stored box of <paramref name="entity"/> and plays the effect, the loot and the sound of each cut. See the class.
    /// </summary>
    public static void Trigger(EntityScriptProxy entity)
    {
        if (!entity.HasFrameBox
            || entity.ScriptHost is not IEntityWorldContext world
            || world.CellMutator is not { } mutator
            || entity.ReadCollisionField() is not CellsCollisionField cells)
        {
            return;
        }

        Span<CutSite> sites = stackalloc CutSite[4];
        var count = CutCorners(entity, cells, mutator, sites);
        if (count == 0)
        {
            return;
        }

        var host = entity.ScriptHost!;
        var effect = host.TileCutEffectIndex;
        for (var i = 0; i < count; i++)
        {
            var site = sites[i];
            var (x, y, z) = (site.X, site.Y, site.Z);
            world.EffectStage?.Emit(reservoir => NativeEffects.CreateTileCutEffect(reservoir, effect, x, y, z));
            LootDrop.SpawnRandomLoot(world, host.GameState, LootCode, x, y, z);
            world.SoundPlayer?.PlaySfx(CutSfx);
        }
    }

    /// <summary>One cut cell: the centre of the cell (16.16, the unclamped column and row) and the Z of the effect and of the loot.</summary>
    internal readonly record struct CutSite(int X, int Y, int Z);

    /// <summary>
    /// The first pass: reads the four corners of the stored box one at a time on the cells as they are, cuts each cuttable one inside the band of Z, and fills <paramref name="sites"/> in order. Returns the number of sites.
    /// Touches nothing but the cells (the effect, the loot and the sound never change a cell, which is why the two passes equal the binary's interleaving, rev3/oracle/export_vectors.py).
    /// </summary>
    internal static int CutCorners(EntityScriptProxy entity, CellsCollisionField cells, ICellMutator mutator, Span<CutSite> sites)
    {
        int columnA, columnB, rowA, rowB, bandBottom, bandTop;
        unchecked
        {
            columnA = ColumnOf((short)(entity.HitBoxX >> 16));
            columnB = ColumnOf((entity.HitBoxX + entity.CollisionWidth) >> 16);
            rowA = entity.HitBoxY >> 20;
            rowB = (entity.HitBoxY + entity.CollisionDepth) >> 20;
            bandBottom = entity.HitBoxZ + 1;
            bandTop = bandBottom + entity.CollisionHeight;
        }

        var count = 0;
        for (var corner = 0; corner < 4; corner++)
        {
            var column = (corner & 1) == 0 ? columnA : columnB;
            var row = corner < 2 ? rowA : rowB;
            var cellColumn = Math.Clamp(column, 0, MaxColumn);
            var cellRow = Math.Clamp(row, 0, MaxRow);
            if (!cells.TryReadBinaryCell(cellColumn, cellRow, out var height, out var walkability)
                || (walkability & CellStore.CuttableWalkabilityBit) == 0)
            {
                continue;
            }

            var cellTop = unchecked((height << 20) + 0x80000);
            if (cellTop < bandBottom || bandTop < cellTop)
            {
                continue;
            }

            mutator.CutCell(cellColumn, cellRow);
            unchecked
            {
                sites[count++] = new CutSite(column * 0x180000 + 0xC0000, row * 0x100000 + 0x80000, height << 20);
            }
        }

        return count;
    }
}
