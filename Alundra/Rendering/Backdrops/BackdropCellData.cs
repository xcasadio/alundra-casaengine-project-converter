#nullable enable
using System.Collections.Generic;
using Alundra.Events;

namespace Alundra.Rendering.Backdrops;

/// <summary>
/// One cell of a Cellular-mode (mode 2) scroll layer - runtime-side mirror of
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropCellDocument</c>, field for field. See
/// <c>CasaEngine.Framework.Rendering.CellularLayers.CellularCellDefinition</c> (D-E9d) for what each
/// field drives.
/// </summary>
public sealed class BackdropCellData
{
    public int PalDex { get; set; }
    public int U0 { get; set; }
    public int V0 { get; set; }
    public int U1 { get; set; }
    public int V1 { get; set; }
    public int Type { get; set; }
    public int X0 { get; set; }
    public int Y0 { get; set; }
    public int CamXNum { get; set; }
    public int CamXDen { get; set; }
    public int CamYNum { get; set; }
    public int CamYDen { get; set; }
    public int DX { get; set; }
    public int PeriodX { get; set; }
    public int DY { get; set; }
    public int PeriodY { get; set; }
}
