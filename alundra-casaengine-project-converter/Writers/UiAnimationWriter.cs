using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 7: the three looping UI animations the screens name as image sources (engine ADR-0038, "Images are
/// named by asset"; parent ADR-0002): the inventory cursor, a full magic pip, and the money coin. Each is a
/// UI/Animations/*.anim2d built from the wind_NNN sprites <see cref="UiWriter"/> already wrote, so an MGUI image
/// plays it on the UI clock instead of the screen code swapping sprites on every logic tick.
///
/// Mapping decisions:
///  - One part, stepped sprite keyframes at the original cadence (a PSX tick is 20 ms at 50 Hz), and a final
///    keyframe one frame after the last that repeats it: <see cref="Animation2dData"/>'s duration is its last
///    keyframe's time (docs/engine/animation2d-composed-format-v1.md, "Duration computation"), so this padding
///    keyframe gives the last frame its full length before the loop wraps - the same "keyframe at the end of
///    the last displayed frame" shape <see cref="SpriteWriter"/> writes.
///  - A position track carries a per-frame pixel offset where the original moves the sprite with its frame (the
///    cursor only). The engine's UI player adds a part's position, rounded, to the image's draw position in
///    screen pixels (CasaUIAssetProvider.CasaUIAnimatedImage.CurrentDrawOffset), so these offsets are screen
///    pixels, Y down - unlike a world animation, whose part positions are Y up.
///  - The cycles, from the original's own counters: cursor 4 x 10 ticks with offsets (0,0) (0,0) (-1,+1) (-1,0)
///    (AlundraInventoryDirector.cs:520-524, AlundraInventoryComposer.cs:103-106); magic pip 4 x 10 ticks
///    (AlundraHudDirector.cs:461-468); coin 4 x 6 ticks (AlundraHudDirector.cs:627-634). The original moves the
///    cursor one tick after it changes its sprite (the composer's position phase lags the sprite phase); here
///    both change together, one 20 ms tick earlier for the offset, within the program's 20 ms tolerance.
///  - Ids are stable (<see cref="Ids.For"/> on "anim2d-ui:" + the name), so a screen can name an animation by
///    id and survive every export.
/// </summary>
public static class UiAnimationWriter
{
    public const string InventoryCursorName = "ui_inventory_cursor";
    public const string MagicPipName = "ui_hud_magic_pip";
    public const string CoinName = "ui_hud_coin";

    private const float TickSeconds = 1f / 50f;
    private const string PartId = "image";
    private static readonly string AnimationsRelativeDirectory = Path.Combine("UI", "Animations");

    private static readonly UiAnimationCycle[] Cycles =
    {
        new(InventoryCursorName, TicksPerFrame: 10,
            new[] { 159, 182, 210, 237 },
            new[] { new Point(0, 0), new Point(0, 0), new Point(-1, 1), new Point(-1, 0) }),
        new(MagicPipName, TicksPerFrame: 10, new[] { 1, 3, 10, 17 }, Offsets: null),
        new(CoinName, TicksPerFrame: 6, new[] { 126, 130, 134, 139 }, Offsets: null),
    };

    public static Guid AnimationId(string name) => Ids.For($"anim2d-ui:{name}");

    /// <param name="writtenWindIndices">The wind.json indices whose sprite <see cref="UiWriter"/> wrote this run: an
    /// animation naming any other is skipped with a warning rather than written with a dangling sprite id.</param>
    public static void WriteUiAnimations(string outputDirectory, IReadOnlySet<int> writtenWindIndices, ConversionReport report)
    {
        Directory.CreateDirectory(Path.Combine(outputDirectory, AnimationsRelativeDirectory));

        var written = 0;
        foreach (var cycle in Cycles)
        {
            var missingIndex = FindMissingIndex(cycle.WindIndices, writtenWindIndices);
            if (missingIndex >= 0)
            {
                report.Warnings.Add($"UI: animation '{cycle.Name}' skipped: sprite wind_{missingIndex:D3} was not written.");
                continue;
            }

            var animation = BuildAnimation(cycle);
            var relativePath = Path.Combine(AnimationsRelativeDirectory, $"{cycle.Name}.anim2d");

            EditorAssetWriterService.SaveAsset(relativePath, animation);
            EditorAssetCatalogService.Add(new AssetInfo(animation.Id)
            {
                Name = animation.Name,
                FileName = relativePath,
            });
            written++;
        }

        report.Increment("Assets.UiAnimation", written);
    }

    private static int FindMissingIndex(int[] windIndices, IReadOnlySet<int> writtenWindIndices)
    {
        foreach (var index in windIndices)
        {
            if (!writtenWindIndices.Contains(index))
            {
                return index;
            }
        }

        return -1;
    }

    private static Animation2dData BuildAnimation(UiAnimationCycle cycle)
    {
        var frameSeconds = cycle.TicksPerFrame * TickSeconds;
        var firstSpriteId = UiWriter.WindSpriteId(cycle.WindIndices[0]);

        var animation = new Animation2dData(AnimationId(cycle.Name))
        {
            Name = cycle.Name,
            AnimationType = AnimationType.Loop,
        };

        animation.Parts.Add(new Animation2dPartData
        {
            Id = PartId,
            Name = PartId,
            DefaultSpriteId = firstSpriteId,
            DefaultVisible = true,
        });

        var spriteTrack = new Animation2dTrackData
        {
            TargetPartId = PartId,
            Property = Animation2dTrackProperty.Sprite,
            Interpolation = Animation2dInterpolationMode.Step,
        };
        var positionTrack = new Animation2dTrackData
        {
            TargetPartId = PartId,
            Property = Animation2dTrackProperty.Position,
            Interpolation = Animation2dInterpolationMode.Step,
        };

        // One keyframe per frame, plus the padding keyframe at the end of the last frame (see the class doc).
        for (var frame = 0; frame <= cycle.WindIndices.Length; frame++)
        {
            var source = Math.Min(frame, cycle.WindIndices.Length - 1);
            var time = frame * frameSeconds;

            spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(time, UiWriter.WindSpriteId(cycle.WindIndices[source])));

            if (cycle.Offsets != null)
            {
                var offset = cycle.Offsets[source];
                positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(time, new Vector2(offset.X, offset.Y)));
            }
        }

        animation.Tracks.Add(spriteTrack);
        if (cycle.Offsets != null)
        {
            animation.Tracks.Add(positionTrack);
        }

        return animation;
    }

    /// <param name="WindIndices">The wind_NNN sprite of each frame, in play order.</param>
    /// <param name="Offsets">A screen-pixel offset per frame, or null when the image does not move.</param>
    private sealed record UiAnimationCycle(string Name, int TicksPerFrame, int[] WindIndices, Point[]? Offsets);
}
