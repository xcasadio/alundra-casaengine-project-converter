#nullable enable
using System.Numerics;
using Alundra.Entities;

namespace Alundra.Physics;

/// <summary>
/// E20.g2c1 (docs/plan-e20-heros.md section 1.24.5, D-G2C1-3): the reader of the four corner words of C4 (<c>0x80066984</c>), a function distinct from <see cref="EntityScriptProxy.UpdateGroundSlope"/> and from the vram flags of the hero: it reads 16 bits per corner
/// (the walkability byte, the ground property byte shifted by 8) at the CURRENT position, with NO qualification by height, and C4 tests <see cref="Mask"/> on each.
/// </summary>
internal static class CornerTileWords
{
    /// <summary>The bits C4 tests on a corner word: <c>(word &amp; 0x1001) == 0x1001</c>.</summary>
    internal const uint Mask = 0x1001;

    /// <summary>The four corner words of <paramref name="entity"/> (x1 y1, x2 y1, x1 y2, x2 y2, the geometry of <c>UpdateGroundSlope</c>); zeros without a cells field.</summary>
    internal static uint[] Read(EntityScriptProxy entity)
    {
        var words = new uint[4];
        if (entity.ReadCollisionField() is not CellsCollisionField field)
        {
            return words;
        }

        var x1 = (entity.PosX + entity.ModX) >> 16;
        var x2 = (entity.PosX + entity.ModX + entity.Width) >> 16;
        var y1 = (entity.PosY + entity.ModY) >> 16;
        var y2 = (entity.PosY + entity.ModY + entity.Height) >> 16;
        var corners = new[] { (x1, y1), (x2, y1), (x1, y2), (x2, y2) };
        for (var i = 0; i < corners.Length; i++)
        {
            var point = new Vector3(corners[i].Item1, corners[i].Item2, 0f);
            words[i] = (uint)((field.SampleRawWalkability(point) & 0xFF) | (field.SampleGroundProperty(point) << 8));
        }

        return words;
    }

    /// <summary>True when one of the words has both bits of <see cref="Mask"/>.</summary>
    internal static bool AnyHasMask(uint[] words)
    {
        foreach (var word in words)
        {
            if ((word & Mask) == Mask)
            {
                return true;
            }
        }

        return false;
    }
}
