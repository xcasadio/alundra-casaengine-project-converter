#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Session;
using Alundra.Entities.Spawning;

namespace Alundra.Entities;

/// <summary>
/// One <see cref="SpriteRecordHeader.IdsvAnimDirs"/> entry - see its doc comment. <see cref="Frames"/>
/// holds the per-frame Images.DepthSortValue list of every displayed frame (terminator frames
/// excluded), in animation-frame order. <see cref="End"/>/<see cref="ChainTo"/> are the same
/// classification <c>SpriteWriter</c> already used to pick this animation's own AnimationType
/// (Loop stays engine Loop; Hold/Chain became engine Once) - see
/// <see cref="EntitySpawnFactory.BuildAnimationEndByAnimDirection"/>.
/// </summary>
public readonly struct AnimDirIdsv
{
    public int Anim { get; init; }
    public int Direction { get; init; }
    public IReadOnlyList<int>? Frames { get; init; }
    public AnimationEndKind End { get; init; }
    public int ChainTo { get; init; }
}
