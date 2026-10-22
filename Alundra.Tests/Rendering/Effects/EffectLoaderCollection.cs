#nullable enable
using System;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Rendering.Effects;
using Alundra.Tests.Support;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>The loader keeps a static cache of the global companion: the tests that reset or fill it do not run together.</summary>
[CollectionDefinition(Name)]
public sealed class EffectLoaderCollection
{
    public const string Name = "AlundraEffectLoader";
}
