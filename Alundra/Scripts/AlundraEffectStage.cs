#nullable enable
using System;
using System.Collections.Generic;

namespace Alundra.Scripts;

/// <summary>The effect stage of a world: bank, reservoir, load-time spawn, queue of operations and the per-tick loop.</summary>
internal sealed class AlundraEffectStage
{
    public AlundraEffectReservoir Reservoir => throw new NotImplementedException();

    public bool HeroTileCaptured => throw new NotImplementedException();

    public void Load(string? worldName, string? projectPath) => throw new NotImplementedException();

    public void Load(AlundraEffectBank bank) => throw new NotImplementedException();

    public void CaptureHeroTile(int tileX, int tileY) => throw new NotImplementedException();

    public void SpawnLoadEffects() => throw new NotImplementedException();

    public void Queue(int tick, int rank, Action<AlundraEffectReservoir> operation) => throw new NotImplementedException();

    public void RunFrame(int ticks, IReadOnlyList<bool> frozenOfTick) => throw new NotImplementedException();
}
