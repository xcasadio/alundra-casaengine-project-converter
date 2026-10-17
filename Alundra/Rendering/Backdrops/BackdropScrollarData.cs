#nullable enable
using System.Collections.Generic;
using Alundra.Events;

namespace Alundra.Rendering.Backdrops;

/// <summary>
/// A mode-1 ("Tiles") layer's parallax/auto-scroll parameters, mirrored field-for-field from
/// <c>AlundraCasaEngineProjectConverter.Readers.BackdropScrollarDocument</c> (see
/// <c>docs/formats/backdrops.md</c>): camera parallax is <c>cameraX * FactorXNum / FactorXDenom</c>
/// (and the Y equivalent), auto-scroll advances <c>ScrollXSpeed</c> per tick plus one extra pixel
/// every <c>|ScrollXPeriod|</c> ticks - see
/// <c>CasaEngine.Framework.Rendering.ScrollingLayers.ScrollingLayerService</c> (parallax and
/// auto-scroll advance, engine-side since E9.b) for the actual formulas.
/// </summary>
public sealed class BackdropScrollarData
{
    public int FactorXNum { get; set; }
    public int FactorXDenom { get; set; }
    public int FactorYNum { get; set; }
    public int FactorYDenom { get; set; }
    public int ScrollXSpeed { get; set; }
    public int ScrollXPeriod { get; set; }
    public int ScrollYSpeed { get; set; }
    public int ScrollYPeriod { get; set; }
}
