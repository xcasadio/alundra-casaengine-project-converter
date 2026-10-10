#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.EffectQuads;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// The pure construction of the quads the DLL pushes to the engine's <see cref="EffectQuadService"/> (E19.g G3c, docs/plan-e19-opcodes.md rules G3C-R1 to G3C-R4, ADR-0049): the
/// reservoir's slots in, <see cref="EffectQuad"/>s out. No engine service, no GPU, no allocation besides the growth of the output list.
/// <list type="bullet">
/// <item><b>G3C-R1 anchor</b> [binary, emitter <c>0x8002DB48</c>]: <c>ax = x &gt;&gt; 16</c>, <c>ay = (y - z) &gt;&gt; 16</c>, arithmetic shifts of 32-bit values; the <c>+ 1</c> that opcodes <c>0x93</c> and
/// <c>0xA2</c> add to z moves the row up (z raw 3145729 gives world row 111, 3145728 gives 112).</item>
/// <item><b>G3C-R2 elimination</b> [binary]: the whole slot (all its images) is skipped unless its screen anchor <c>(ax - scrollX, ay - scrollY)</c> is inside -128..448 x -128..368, bounds
/// included. Without a camera nothing is eliminated.</item>
/// <item><b>G3C-R3 quads</b>: for every image k of the slot's current image set, the corners are <c>(ax + Xi, -(ay + Yi))</c> in the data order (top-left, top-right, bottom-left, bottom-right;
/// the world is y up), the raw texture window is <c>(U - mx, V - my, W, H)</c> with <c>mx = C0 &gt; C2</c> and <c>my = C1 &gt; C5</c> (the exported cell is the extractor's window, the raw window of
/// a mirrored axis starts one texel before; 519 mirrored-X images have U = 0 and 137 mirrored-Y images V = 0, whose origin -1 the clamped sampler reads as column or row 0: the accepted overshoot
/// of G2b), the mode is <c>Semi ? Abr + 1 : None</c>, the sheet 0 (the map companion) for an image set of the map table and 1 (the global companion) otherwise.</item>
/// <item><b>G3C-R4 key</b>: pass <c>YSortedWorld</c>, layer 300, order 0 (the values the entity prefab carries, <c>alundra-project/Entities/Alundra/Alundra.entity</c> lines 544 to 553), elevation
/// <c>16 * min(DepthKey &gt;&gt; 20, 59) + 6</c> (<see cref="WallPlacementOverlay.ComputeEntityElevation"/> on the stored key), coordinate <c>100 * ay</c> (the world row, like the entities; an ANCHORED slot, modes 1 and 3, takes its anchor's sprite coordinate plus its depth offset instead, E20.f1b D-E20-29), local offset
/// <c>N - 1 - k</c> (image 0 is drawn last, as in the binary), stable id the slot index.</item>
/// </list>
/// </summary>
internal static class AlundraEffectQuadBuilder
{
    /// <summary>The sheet of the map companion (<c>SheetTextureAssetId</c> of the map's document).</summary>
    internal const int MapSheet = 0;

    /// <summary>The sheet of the global companion.</summary>
    internal const int GlobalSheet = 1;

    private const int RenderPass = (int)RenderPass2D.YSortedWorld;
    private const int SortingLayer = 300;
    private const int ScreenXMin = -128;
    private const int ScreenXMax = 448;
    private const int ScreenYMin = -128;
    private const int ScreenYMax = 368;

    /// <summary>Appends to <paramref name="into"/> the quads of every active slot with an image set, in the order of the slots then of the images. With <paramref name="hasScroll"/> the slots whose
    /// screen anchor is outside the bounds of G3C-R2 are skipped; <paramref name="scrollX"/> and <paramref name="scrollY"/> are the original scroll (<see cref="AlundraCameraMath.ToOriginalScrollSpace"/>).</summary>
    internal static void Build(IReadOnlyList<AlundraEffectSlot> slots, bool hasScroll, int scrollX, int scrollY, List<EffectQuad> into)
    {
        for (var s = 0; s < slots.Count; s++)
        {
            var slot = slots[s];
            var set = slot.CurrentImageSet;
            if (!slot.Active || set == null)
            {
                continue;
            }

            int ax;
            int ay;
            unchecked
            {
                ax = slot.X >> 16;
                ay = (slot.Y - slot.Z) >> 16;
                if (hasScroll)
                {
                    var screenX = ax - scrollX;
                    var screenY = ay - scrollY;
                    if (screenX < ScreenXMin || screenX > ScreenXMax || screenY < ScreenYMin || screenY > ScreenYMax)
                    {
                        continue;
                    }
                }
            }

            var elevation = WallPlacementOverlay.ComputeEntityElevation(unchecked((int)slot.DepthKey), 0);
            // E20.f1b (D-E20-29, amends ADR-0049): an anchored slot (modes 1 and 3) sorts on the coordinate of its anchor's sprite plus its depth offset, not on its own (higher) position, so that the aura
            // is drawn just after the hero (the binary: the anchor's key plus one). Mode 0 keeps the rule of ADR-0049; a slot frozen (mode 2) after its anchor is gone too.
            var coordinate = slot.Mode is 1 or 3 && slot.Anchor != null ? slot.Anchor.SortCoordinate + slot.DepthOffset : 100 * ay;
            var sheet = slot.CurrentMapTable ? MapSheet : GlobalSheet;
            var images = set.Images;
            for (var k = 0; k < images.Count; k++)
            {
                var image = images[k];
                var c = image.C;
                var mirrorX = c[0] > c[2] ? 1 : 0;
                var mirrorY = c[1] > c[5] ? 1 : 0;
                into.Add(new EffectQuad(
                    sheet,
                    new Rectangle(image.U - mirrorX, image.V - mirrorY, image.W, image.H),
                    new Vector2(ax + c[0], -(ay + c[1])),
                    new Vector2(ax + c[2], -(ay + c[3])),
                    new Vector2(ax + c[4], -(ay + c[5])),
                    new Vector2(ax + c[6], -(ay + c[7])),
                    image.Semi ? (SpritePsxSemiTransparency)(image.Abr + 1) : SpritePsxSemiTransparency.None,
                    new RenderSortKey2D(RenderPass, SortingLayer, 0, elevation, coordinate, images.Count - 1 - k, slot.Index)));
            }
        }
    }
}
