#nullable enable
using System.Collections.Generic;
using Alundra.Events;

namespace Alundra.Rendering.Backdrops;

/// <summary>
/// A mode-2 ("Cellular") layer's shared wave-shimmer coefficients plus its cells - runtime-side mirror
/// of <c>AlundraCasaEngineProjectConverter.Readers.BackdropCellularDocument</c>, field for field. See
/// <c>CasaEngine.Framework.Rendering.CellularLayers.CellularLayerDefinition</c> (D-E9d) for what each
/// field drives; <see cref="AWaveY"/>/<see cref="AWavePhase"/>/<see cref="AWaveAmp"/>/<see cref="BWaveY"/>/
/// <see cref="BWavePhase"/>/<see cref="BWaveWeight"/> feed the <c>WaveX</c> cell type's formula, which
/// also consumes the map-level <see cref="BackdropDocument.WaveLut"/> table.
/// </summary>
public sealed class BackdropCellularData
{
    public int CountBase { get; set; }
    public int AWaveY { get; set; }
    public int AWavePhase { get; set; }
    public int AWaveAmp { get; set; }
    public int BWaveY { get; set; }
    public int BWavePhase { get; set; }
    public int BWaveWeight { get; set; }
    public int Divisions { get; set; }
    public List<BackdropCellData> Cells { get; set; } = new();
}
