#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Owns the four static per-corner terrain samplers formerly on <see cref="AlundraEntityScriptProxy"/>:
/// <c>SampleTerrainHeightCorner</c>, <c>ProbeSlopeCorner</c>, <c>SampleRawTileHeightCorner</c> and
/// <c>SampleGroundCorner</c>. Pure `static`, stateless, moved by slice R4 of
/// docs/plan-decoupage-proxies.md - a behaviour-preserving relocation only, see that plan's §3 for the
/// exact delta rule (call qualification and the private-to-internal widening this move used on all
/// four methods, per §1 constraint 4). The instance terrain methods that call these
/// (<c>ComputeTerrainHeight</c>, <c>UpdateGroundSlope</c>, <c>GetTileHeightAtOffset</c>) stay on
/// <see cref="AlundraEntityScriptProxy"/>, per R-1: they read <c>Owner</c> (`protected` on
/// `GameplayProxy`) and form a call cycle with the controller bridge, which is out of scope for this
/// plan.
/// </summary>
internal static class AlundraTerrainProbe
{
    internal static void SampleTerrainHeightCorner(ICollisionField field, int px, int py, ref int best)
    {
        if (field.TrySampleGround(new Vector3(px, py, 0f), float.MaxValue, out var sample) && sample.HasGround)
        {
            var height = (int)Math.Round((double)sample.GroundHeight * 65536.0);
            if (height > best)
            {
                best = height;
            }
        }
    }

    internal static void ProbeSlopeCorner(
        AlundraCellsCollisionField field, int px, int py, int moddedPosZ, ref uint bestFlagMask)
    {
        var position = new Vector3(px, py, 0f);
        if (field.TrySampleGround(position, float.MaxValue, out var sample) && sample.HasGround)
        {
            var height = (int)Math.Round((double)sample.GroundHeight * 65536.0);
            if (height == moddedPosZ)
            {
                var groundProperty = field.SampleGroundProperty(position);
                var masked = ((uint)groundProperty << 8) & 0xe00u;
                if (masked < bestFlagMask)
                {
                    bestFlagMask = masked;
                }

                return;
            }
        }

        bestFlagMask = 0;
    }

    internal static void SampleRawTileHeightCorner(AlundraCellsCollisionField field, int px, int py, ref int best)
    {
        var height = field.SampleRawCellHeight(new Vector3(px, py, 0f));
        if (height > best)
        {
            best = height;
        }
    }

    internal static void SampleGroundCorner(ICollisionField field, float x, float y, ref bool hasGround, ref float groundMax)
    {
        if (!field.TrySampleGround(new Vector3(x, y, 0f), float.MaxValue, out var sample) || !sample.HasGround)
        {
            return;
        }

        if (!hasGround || sample.GroundHeight > groundMax)
        {
            groundMax = sample.GroundHeight;
            hasGround = true;
        }
    }
}
