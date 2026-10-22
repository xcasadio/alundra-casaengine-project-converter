#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Entities;

/// <summary>
/// The decoded fields one creation by <c>SpawnWarpEntity</c> (<c>0x80039F58</c>) leaves in the entity of the binary, as the E20.e0a1 oracle read them (<c>docs/plan-e20-annexe/e20-e0a/oracle/out</c>): the binary's own frame
/// (<see cref="Pz"/>, <see cref="ModZ"/> and <see cref="Floor"/> are one raw unit above the DLL's, ADR-0026; <see cref="Corners"/> as well). <see cref="Root"/> and <see cref="Parent"/> are <c>none</c>, <c>hero</c> or <c>parent</c>
/// (the entity that was created first as the parent of the vector).
/// </summary>
internal sealed record SpawnProjection(
    int Hp, int HpMax, int Ref, int Id, uint Flags, IReadOnlyList<int> SpriteProgram, IReadOnlyList<int> Program, uint AnimTarget, uint DirTarget,
    int Px, int Py, int Pz, int ModdedX, int ModdedY, int ModdedZ, int ModX, int ModY, int ModZ, int Width, int Height, int Depth,
    int TileX, int TileY, int Terrain, int Floor, IReadOnlyList<int> Corners, string Root, string Parent, int HasAttack, int AnimFlags, IReadOnlyList<int> Loot);
