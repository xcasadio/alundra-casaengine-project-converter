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
using Alundra.Scripts;

namespace Alundra.Entities;

/// <summary>
/// One <see cref="AlundraEntityScriptProxy.AnimationEndByAnimDirection"/> table value - see
/// <see cref="EntitySpawnFactory.BuildAnimationEndByAnimDirection"/> and
/// <see cref="EntitySpawnFactory.OnAnimationFinished"/>.
/// </summary>
public readonly struct AnimationEndInfo
{
    public AnimationEndKind Kind { get; init; }
    public int ChainTargetAnimationId { get; init; }
}
