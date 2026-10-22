#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Alundra.Tests.Support;

/// <summary>One row of the ground oracle (<c>out/h2/map_&lt;N&gt;.txt.gz</c>): a body (<see cref="E"/>) and the hero (<see cref="H"/>) after the creation (variant 0), a physics step (1) or a displacement of the body (2), and the relative position the real <c>0x8007FE20</c> gave for the pair (<see cref="R"/>).
/// Both blocks: posx posy posz modx mody modz w h d tilex tiley terrain floor c0 c1 c2 c3 (raw 16.16 words, the BINARY's values).</summary>
internal sealed record EnemyGroundRow(int Map, int Record, int Variant, int HeroDx, int HeroDy, int SweepDx, int SweepDy, int[] E, int[] H, int[] R)
{
    public const int PosX = 0, PosY = 1, PosZ = 2, ModX = 3, ModY = 4, ModZ = 5, Width = 6, Height = 7, Depth = 8, TileX = 9, TileY = 10, Terrain = 11, Floor = 12, Corner0 = 13;
}
