using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Audio;
using CasaEngine.Framework.Audio.Mixing;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Support;
using Alundra.Tests.Maps;

namespace Alundra.Tests.Audio;

/// <summary>
/// xunit collection covering every test class that touches the SESSION-scoped
/// <see cref="BgmPlayer.Instance"/> singleton (D-C-6, docs/plan-e11c-musique.md, slice C1) -
/// today <see cref="MusicPlayerTests"/> and <see cref="WorldProxyAudioInstallationTests"/>
/// (whose <c>InstallAudioSystems</c> call now re-points that same instance). Classes sharing a
/// collection never run in parallel with each other, which is what keeps them from racing on that
/// shared mutable state (xunit runs different test CLASSES in this project in parallel by default).
/// </summary>
[CollectionDefinition(Name)]
public class MusicPlayerSingletonCollection
{
    public const string Name = "AlundraMusicPlayer singleton";
}
