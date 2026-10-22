#nullable enable
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScreenEffects;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Support;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Transitions;

/// <summary>
/// xunit collection covering every test class that touches the SESSION-scoped
/// <see cref="ScreenFadeDirector.Instance"/> singleton (D-E10-6, docs/plan-e10-fondu.md, slice
/// E10.b) - same rationale as <see cref="MusicPlayerSingletonCollection"/>: classes sharing a
/// collection never run in parallel with each other, which is what keeps them from racing on that
/// shared mutable state (xunit runs different test CLASSES in this project in parallel by default).
/// </summary>
[CollectionDefinition(Name)]
public class ScreenFadeDirectorSingletonCollection
{
    public const string Name = "AlundraScreenFadeDirector singleton";
}
