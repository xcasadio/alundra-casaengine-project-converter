using Xunit;
using Alundra.Rng;
using Alundra.Tests.Maps;
using Alundra.Tests.Audio;

namespace Alundra.Tests.Rng;

/// <summary>
/// xunit collection covering every test class that touches the process-wide static
/// <see cref="GameRandom.RandomSeed"/> (D7, docs/plan-e9d-mode-cellulaire.md) - today
/// <see cref="RandomTests"/> and <see cref="WorldProxyCellularRandomWiringTests"/>. Classes
/// sharing a collection never run in parallel with each other, which is what keeps them from racing on
/// that shared mutable static (xunit runs different test CLASSES in this project in parallel by
/// default) - same pattern as <see cref="MusicPlayerSingletonCollection"/>.
/// </summary>
[CollectionDefinition(Name)]
public class RandomStaticStateCollection
{
    public const string Name = "AlundraRandom static state";
}
