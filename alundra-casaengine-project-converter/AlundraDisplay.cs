namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// How much of a map the player sees, expressed once for the whole converter.
///
/// Alundra's own screen is 320x240: ALUN_CD.EXE (France) sets the draw and display environments to
/// 0x140 x 0xF0 (SetDefDrawEnv / SetDefDispEnv at 0x800424AC) and sizes both full-screen TILEs the same
/// (0x800429C8-0x800429EC). The 236 that earlier versions of this file used came from the decompilation
/// (AlundraEngine.StaticVariables.ScreenHeight, whose trailing "//224" is another guess), which writes 236
/// where the binary writes 0xF0: the binary wins (plan E19.s, D-E19-47).
///
/// The image is fitted to the window by the ENGINE (engine ADR-0048), not here. This converter declares the
/// virtual resolution in AlundraGame.json (<see cref="NativeWidth"/> x <see cref="NativeHeight"/>, mode
/// IntegerFit, black bands); at run time the engine picks the largest whole factor k that fits the window,
/// centres the image, frames it with a camera whose Zoom is k, and follows the window as it is resized. What
/// the converter still writes is the starting state:
/// <list type="bullet">
/// <item>the window size in AlundraGame.json (Phase 0), <see cref="WindowWidth"/> x <see cref="WindowHeight"/>
/// = native x <see cref="PixelScale"/>, so the game opens with no band;</item>
/// <item>the camera Zoom in AlundraCamera.entity (Phase 6), <see cref="CameraZoom"/> = <see cref="PixelScale"/>
/// (a placeholder the engine overwrites with k, but one that keeps the file consistent with the window).</item>
/// </list>
/// They stay one setting rather than two, which is why they live here rather than in either writer: the
/// whole-factor framing keeps the engine's pixel-perfect checklist satisfied
/// (docs/engine/rendering-2d-3d-spaces.md), one tileset texel covering exactly k x k screen pixels.
///
/// To open the game at another factor, change <see cref="PixelScale"/> alone - both files follow.
/// 320x240 is exactly 4:3 with square pixels, so the old note about a 1.7% aspect difference with a CRT
/// no longer applies.
/// </summary>
public static class AlundraDisplay
{
    /// <summary>The width of the original screen (SetDefDispEnv w = 0x140).</summary>
    public const int NativeWidth = 320;

    /// <summary>The height of the original screen (SetDefDispEnv h = 0xF0).</summary>
    public const int NativeHeight = 240;

    /// <summary>
    /// Integer magnification of the native screen the game opens at. Drives both the window size and the
    /// camera zoom placeholder.
    /// </summary>
    public const int PixelScale = 4;

    public const int WindowWidth = NativeWidth * PixelScale;

    public const int WindowHeight = NativeHeight * PixelScale;

    /// <summary>
    /// Camera2dComponent.Zoom as written in the camera asset. Equal to <see cref="PixelScale"/> by
    /// construction (window / Zoom = native); the engine sets the live zoom itself.
    /// </summary>
    public const float CameraZoom = PixelScale;
}
