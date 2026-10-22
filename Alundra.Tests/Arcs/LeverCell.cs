#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Cells;
using Alundra.Audio;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>One cell as the lever programs rewrite it: walkability, height, ground property, floor tile and the wall stack.</summary>
internal readonly record struct LeverCell(int Walkability, int Height, int GroundProperty, int FloorTile, string WallStack)
{
    public override string ToString() => $"[W{Walkability} H{Height} GP{GroundProperty} T{FloorTile} S{WallStack}]";
}
