#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.TileMap;
using Microsoft.Xna.Framework;
using Alundra.Maps;
using Alundra.Events;
using Alundra.Physics;

namespace Alundra.Cells;

/// <summary>
/// The three cell-mutation primitives 0x54/0x55/0x85 need (docs/plan-e7-mutation-tuiles.md, slice
/// E7.a) - implemented by <see cref="CellStore"/>, consumed by
/// <see cref="EventProgramRunner.Dispatch"/> through <see cref="IEntityWorldContext.CellMutator"/>.
/// A separate interface (rather than exposing <see cref="CellStore"/> itself through the seam)
/// so a synthetic interpreter test can inject a fake without any real <see cref="CellsRecords"/>.
/// </summary>
public interface ICellMutator
{
    /// <summary>Opcode 0x85 (Script_133_085) - port of <c>GameEngine.ChangeAreaTileProperties</c>
    /// (GameEngine.cs:2239-2321). See <see cref="CellStore.CopyCellRectangle"/> for the exact
    /// field list and the documented no-clamp deviation.</summary>
    void CopyCellRectangle(int srcX, int srcY, int width, int height, int dstX, int dstY);

    /// <summary>Opcode 0x54 (Script_84_054) - OR's <paramref name="walkabilityMask"/>/
    /// <paramref name="groundPropertyMask"/> into one cell's Walkability/GroundProperty. See
    /// <see cref="CellStore.SetCellBits"/> for the hardcoded clamp.</summary>
    void SetCellBits(int x, int y, int walkabilityMask, int groundPropertyMask);

    /// <summary>Opcode 0x55 (Script_85_055) - AND's the COMPLEMENT of
    /// <paramref name="walkabilityMask"/>/<paramref name="groundPropertyMask"/> into one cell's
    /// Walkability/GroundProperty (bit clear). See <see cref="CellStore.ClearCellBits"/> for the
    /// hardcoded clamp.</summary>
    void ClearCellBits(int x, int y, int walkabilityMask, int groundPropertyMask);

    /// <summary>
    /// E20.e (docs/plan-e20-heros.md section 1.27, <c>0x8003A924-0x8003A938</c>): the cut of a grass cell. Clears the bit <c>0x02</c> of the cell's walkability, sets its wall-stack word to -1 (the binary's
    /// <c>0xFFFF</c>) and destroys its stack of wall tiles; the height, the slope, the ground property and the floor tile id stay. Distinct from <see cref="ClearCellBits"/>, which never touches the stack.
    /// <paramref name="column"/> and <paramref name="row"/> are the already clamped cell of the binary (0 to <c>0x33</c>, 0 to <c>0x3B</c>) inside the field. Fires <see cref="CellStore.CellsMutated"/> once.
    /// </summary>
    void CutCell(int column, int row);
}
