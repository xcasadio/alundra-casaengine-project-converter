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
/// How an (anim, direction)'s trailing control frame ends playback - mirrors the converter's own
/// <c>AnimationEndClassifier.AnimationEndKind</c> (alundra-casaengine-project-converter/Readers/
/// AnimationEndClassifier.cs), duplicated here rather than shared since this DLL does not reference
/// the converter project. See <see cref="EntitySpawnFactory.SubscribeAnimationEndBridge"/> for how
/// this drives the engine's Once-finished event back to the original's Hold/Chain semantics
/// (EntityManager.cs:257-281).
/// </summary>
public enum AnimationEndKind
{
    Loop,
    Hold,
    Chain,
}
