namespace AlundraCasaEngineProjectConverter;

/// <summary>
/// How much of a map the player sees, expressed once for the whole converter.
///
/// The engine computes the visible world area as viewport / Zoom
/// (Camera2dComponent.ComputeProjectionMatrix: Matrix.CreateOrthographic(viewport.Width / Zoom,
/// viewport.Height / Zoom, ...)), and the viewport is the live window - CameraComponent
/// .InitializeWithWorld overwrites whatever an asset serialized with Game.ScreenSizeWidth/Height.
/// So the framing is decided by TWO values the converter writes in two different files: the window
/// size in AlundraGame.json (Phase 0) and the camera Zoom in AlundraCamera.entity (Phase 6). They
/// are one setting, not two, which is why they live here rather than in either writer: letting them
/// drift silently produces a technically valid project framed wrong, which is exactly what happened
/// when the window kept the engine's 1024x768 default while Zoom stayed at 1 - the view then showed
/// 1024x768 world pixels instead of 320x236, i.e. 10x too much map on screen.
///
/// Alundra's own screen is 320x236 (AlundraEngine.StaticVariables.ScreenWidth / ScreenHeight in the
/// decompilation - the trailing "//224" there is an earlier guess, 236 is what the engine uses).
/// Window = <see cref="PixelScale"/> x native and Zoom = <see cref="PixelScale"/> reproduces that
/// framing exactly while keeping the zoom an integer, which the engine's pixel-perfect checklist
/// (docs/engine/rendering-2d-3d-spaces.md) requires: one tileset texel then covers exactly
/// PixelScale x PixelScale screen pixels.
///
/// To show more or less of the map, change <see cref="PixelScale"/> alone - both files follow.
/// Note the whole scheme assumes square pixels: the original ran on a 4:3 TV with non-square pixels,
/// so a 1:1 render is 1.7% wider in aspect than a CRT showed it. Correcting that would require a
/// non-integer scale on one axis and would break pixel-perfect, so it is left alone deliberately.
/// </summary>
public static class AlundraDisplay
{
    /// <summary>AlundraEngine.StaticVariables.ScreenWidth.</summary>
    public const int NativeWidth = 320;

    /// <summary>AlundraEngine.StaticVariables.ScreenHeight.</summary>
    public const int NativeHeight = 236;

    /// <summary>
    /// Integer magnification of the native screen. Drives both the window size and the camera zoom,
    /// so the visible world area stays exactly the native one whatever the value.
    /// </summary>
    public const int PixelScale = 4;

    public const int WindowWidth = NativeWidth * PixelScale;

    public const int WindowHeight = NativeHeight * PixelScale;

    /// <summary>
    /// Camera2dComponent.Zoom. Equal to <see cref="PixelScale"/> by construction: the visible area
    /// is window / Zoom, and the window is native * PixelScale.
    /// </summary>
    public const float CameraZoom = PixelScale;
}
