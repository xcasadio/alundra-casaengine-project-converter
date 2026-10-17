#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Session;

namespace Alundra.UI.Hud;

/// <summary>One gouraud-shaded corner colour (0-255 per channel). Not a graphics-library <c>Color</c> so
/// this file stays free of any rendering dependency (this enum/struct file's own doc: "No MGUI type
/// appears anywhere in this file" - kept true here for the same testability reason, mission item 7's
/// "fonction pure"). <see cref="HudScreen"/> converts each one to its own <c>Color</c>.</summary>
public readonly record struct HudRgb(byte R, byte G, byte B);
