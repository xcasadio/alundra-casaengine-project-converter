#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Physics;
using Alundra.Rendering.Effects;
using Alundra.Audio;
using Alundra.Hero;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.d2b (docs/plan-e19-opcodes.md §1.2h.2): shared support of the entity contact tests - a flat ground field, a script host that
/// rebuilds its collidable list the way the world does at the end of each frame, and a builder of real worlds holding real
/// controller-driven entities (the logical position fields written by hand, the controller and the root those of production).
/// </summary>
internal sealed class FlatGroundField : ICollisionField
{
    public float GroundZ { get; init; }

    public bool TrySampleGround(in Vector3 worldPosition, float maxDropDistance, out GroundSample sample)
    {
        sample = new GroundSample(GroundZ, Vector3.UnitZ, true, "flat");
        return true;
    }
}
