#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Pure math behind <see cref="BackdropRenderer"/>'s per-frame layer scroll (see
/// <c>docs/formats/backdrops.md</c> and <see cref="BackdropScrollarData"/>'s class doc for the
/// authority these formulas mirror). Kept free of any engine/graphics dependency so it is
/// unit-testable headless.
/// </summary>
public static class BackdropOffsetMath
{
    /// <summary>The layer canvas is always a fixed 640x480 (40x30 16px tiles) that both the camera
    /// parallax offset and the auto-scroll offset wrap within.</summary>
    public const int CanvasWidth = 640;

    public const int CanvasHeight = 480;

    /// <summary>Original engine tick rate the auto-scroll speed/period are expressed against.</summary>
    public const float TicksPerSecond = 50f;

    /// <summary>
    /// <c>scroll * factorNum / factorDenom</c>, in TRUNCATED INTEGER division - the original's own
    /// arithmetic (docs/plan-e9-backdrops-residus.md §1.1.b, D-E9-1: <c>GraphicManager.cs:868-899</c>),
    /// not a float division rounded afterwards (1/3 of scroll 5 is 1, not 1.667). <paramref name="scroll"/>
    /// is the original's own <c>g_cameraScrollingX/Y</c> (see
    /// <see cref="AlundraCameraMath.ToOriginalScrollSpace"/>, the sole producer), already clamped
    /// non-negative, so this truncation matches the original's on every input actually reachable
    /// (§1.1.b). A zero denominator disables this layer's camera parallax (contributes 0) instead of
    /// dividing by zero.
    /// </summary>
    public static int ComputeParallaxOffset(int scroll, int factorNum, int factorDenom)
    {
        return factorDenom == 0 ? 0 : scroll * factorNum / factorDenom;
    }

    /// <summary>
    /// Auto-scroll offset at <paramref name="tickCount"/> elapsed ticks: <c>speed</c> pixels per tick,
    /// plus one further pixel every <c>|period|</c> ticks - direction given by the XOR of the signs of
    /// <c>speed</c> and <c>period</c> (a period of 0 contributes nothing beyond the flat per-tick
    /// speed).
    /// </summary>
    public static float ComputeAutoScrollOffset(int speed, int period, long tickCount)
    {
        var offset = (float)speed * tickCount;

        if (period != 0)
        {
            var direction = (speed < 0) != (period < 0) ? -1 : 1;
            offset += (tickCount / Math.Abs(period)) * direction;
        }

        return offset;
    }

    /// <summary>Wraps <paramref name="value"/> into <c>[0, canvasSize)</c>.</summary>
    public static float WrapOffset(float value, int canvasSize)
    {
        var wrapped = value % canvasSize;
        return wrapped < 0 ? wrapped + canvasSize : wrapped;
    }

    /// <summary>
    /// Combines <see cref="ComputeParallaxOffset"/> (integer, on the original's own scroll space - see
    /// its own doc) and <see cref="ComputeAutoScrollOffset"/> (float, its own clock kept as-is by
    /// D-E9-6), wrapped into <c>[0, canvasSize)</c> - the canvas-space coordinate visible at screen
    /// position 0 along this axis.
    /// </summary>
    public static float ComputeLayerOffset(
        int scroll, int factorNum, int factorDenom, int speed, int period, long tickCount, int canvasSize)
    {
        var offset = ComputeParallaxOffset(scroll, factorNum, factorDenom)
            + ComputeAutoScrollOffset(speed, period, tickCount);
        return WrapOffset(offset, canvasSize);
    }

    /// <summary>
    /// One axis of <see cref="ComputeCoveringQuadOrigins"/>: the set of tile-local origins (each
    /// spaced exactly <paramref name="tileSize"/> apart) whose <c>[origin, origin+tileSize)</c>
    /// intervals union to fully cover <c>[0, viewportSize)</c>, given that screen position 0 samples
    /// canvas coordinate <paramref name="offset"/> (already wrapped into <c>[0, tileSize)</c>).
    /// </summary>
    public static List<int> ComputeCoveringOrigins1D(int viewportSize, float offset, int tileSize)
    {
        var origins = new List<int>();
        if (tileSize <= 0 || viewportSize <= 0)
        {
            return origins;
        }

        var wrapped = WrapOffset(offset, tileSize);
        var start = (int)Math.Floor(-wrapped);

        for (var x = start; x < viewportSize; x += tileSize)
        {
            origins.Add(x);
        }

        return origins;
    }

    /// <summary>
    /// Every quad origin (viewport-local, top-left anchored) needed to tile-cover a
    /// <paramref name="viewportWidth"/>x<paramref name="viewportHeight"/> viewport with
    /// <paramref name="tileWidth"/>x<paramref name="tileHeight"/> copies of the wrapping canvas
    /// texture, given the canvas-space offset visible at screen (0,0). Allocation-light: called once
    /// per layer per frame by <see cref="BackdropRenderer"/>.
    /// </summary>
    public static List<Point> ComputeCoveringQuadOrigins(
        int viewportWidth, int viewportHeight, float offsetX, float offsetY, int tileWidth = CanvasWidth, int tileHeight = CanvasHeight)
    {
        var xs = ComputeCoveringOrigins1D(viewportWidth, offsetX, tileWidth);
        var ys = ComputeCoveringOrigins1D(viewportHeight, offsetY, tileHeight);

        var origins = new List<Point>(xs.Count * ys.Count);
        foreach (var y in ys)
        {
            foreach (var x in xs)
            {
                origins.Add(new Point(x, y));
            }
        }

        return origins;
    }
}
