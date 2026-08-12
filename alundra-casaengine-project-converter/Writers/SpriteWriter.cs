using System.Text.Json;
using AlundraCasaEngineProjectConverter.Readers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using Microsoft.Xna.Framework;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Phase 3: converts Alundra sprite banks (deduplicated by Sector5Id) into CasaEngine .sprite and
/// .anim2d assets.
///
/// Mapping decisions (see docs/plan-conversion-agent-ia.md Phase 3 and the accompanying session
/// notes for the full reasoning):
///  - One folder per bank under Entities/, named after the entity the game itself names it (see
///    EntityNameCatalogReader): the hero bank is Entities/Alundra. Banks the name table leaves
///    empty, and banks sharing a name, fall back to or keep their bank key. Both the .anim2d and
///    the .sprite assets of a bank live there; the spritesheet textures they all share stay under
///    Sprites/Textures since they belong to no single entity.
///  - One .anim2d per (bank, AnimSet index, direction). Its parts are numbered slots (part0..N-1)
///    sized to the animation's own max simultaneous quad count; a frame with fewer quads than
///    that hides the unused slots for that frame via the Visible track. This does not require
///    "part0 is always the head" style consistency: every part's Sprite/Position/Flip/DrawOrder
///    is keyframed per frame, so a slot showing different content between frames still renders
///    correctly.
///  - One .sprite per unique (source spritesheet, Signature). Signature already combines
///    Spritesheet+Palette+Sx+Sy+Swidth+Sheight, but is only unique WITHIN one map's exported
///    spritesheet PNG (AtlasX/AtlasY are computed per map by the extractor) - so dedup keys on
///    (spritesheet file, Signature), not Signature alone.
///  - SpriteData.Origin is the crop's own center (Width/2, Height/2); the quad's actual on-screen
///    placement (from its X1..Y4 destination corners) becomes the part's per-frame Position
///    instead, at the corners' geometric center. Center-based, not corner-based, so flipping
///    (which mirrors around Origin) doesn't shift the visible position.
///  - Per-frame 3D collision (SiFrame.CollisionData) becomes Animation2dData.CollisionKeyframes,
///    one Box fixture per keyframe. Alundra stores the box's MIN CORNER plus its extents, the
///    engine poses a fixture by its CENTRE, hence the +Size/2 shift; sizes stay in pixels and the
///    box is never rotated (Alundra volumes are AABBs). Consecutive frames carrying the same volume
///    emit nothing, a frame that loses its volume emits an empty fixture set, and the trailing
///    terminator frame never emits (see ConvertCollisionKeyframes for the full rule). ProfileName
///    and Tag stay empty: CollisionData carries no attack/defence semantics - that lives in the
///    event bytecode, still out of scope - and an empty profile inherits Trigger in the engine.
///  - Hero SpriteEffectRecords (map_alundra.json) are preserved as a raw JSON companion, not
///    converted to sprites/animations in V1 (their Spritesheet indices exceed the normal 0-7
///    range, suggesting a different graphics source not covered by the atlas-packing fix).
///  - Asset ids are NOT forced deterministic here: ObjectBase.Id has a private setter, and
///    EditorAssetWriterService.SaveAsset always serializes whatever Id the object already has.
///    This matches Phase 1's actual behavior (EditorAssetImportService.ImportTiledMap also
///    generates fresh ids per run), even though it was aspirationally documented as "deterministic
///    everywhere" in the plan - see the completion notes for this session.
/// </summary>
public static class SpriteWriter
{
    private static readonly string[] DirectionNames = { "down", "up", "left", "right" };
    private const float PsxFrameSeconds = 1f / 50f; // source data is PAL (50 Hz)

    public static void ConvertSprites(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var banks = SpriteBankReader.ReadAllBanks(inputDirectory, report);

        var entityNamesPath = Path.Combine(AppContext.BaseDirectory, "EntityNames.csv");
        IReadOnlyDictionary<string, string> folderNamesByBankKey;
        if (File.Exists(entityNamesPath))
        {
            var catalog = EntityNameCatalogReader.Read(entityNamesPath, banks);
            folderNamesByBankKey = catalog.FolderNamesByBankKey;
            foreach (var warning in catalog.Warnings)
            {
                report.Warnings.Add(warning);
            }
        }
        else
        {
            report.Errors.Add(
                $"EntityNames.csv not found at '{entityNamesPath}'; entity folders would lose their names.");
            return;
        }

        var textureAssetIdsBySpritesheet = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var spriteAssetIdsByKey = new Dictionary<(string Spritesheet, long Signature), Guid>();

        foreach (var bank in banks)
        {
            ConvertBank(
                bank, folderNamesByBankKey[bank.BankKey], inputDirectory, outputDirectory,
                textureAssetIdsBySpritesheet, spriteAssetIdsByKey, report);
        }

        EditorAssetCatalogService.Save();

        report.Increment("Assets.Sprite", spriteAssetIdsByKey.Count);
        report.Increment("Sprites.Textures", textureAssetIdsBySpritesheet.Count);

        PreserveHeroEffects(inputDirectory, outputDirectory, report);
    }

    private static void ConvertBank(
        SpriteBank bank,
        string entityFolderName,
        string inputDirectory,
        string outputDirectory,
        Dictionary<string, Guid> textureAssetIdsBySpritesheet,
        Dictionary<(string Spritesheet, long Signature), Guid> spriteAssetIdsByKey,
        ConversionReport report)
    {
        Guid textureAssetId;
        try
        {
            textureAssetId = EnsureSpritesheetTexture(
                inputDirectory, outputDirectory, bank.SourceSpritesheetFileName, textureAssetIdsBySpritesheet);
        }
        catch (Exception exception)
        {
            report.Errors.Add($"bank_{bank.BankKey}: failed to import spritesheet texture - {exception.Message}");
            return;
        }

        var bankRelativeDirectory = Path.Combine("Entities", entityFolderName);
        Directory.CreateDirectory(Path.Combine(outputDirectory, bankRelativeDirectory));
        var quadsRead = 0;
        var quadsConverted = 0;

        for (var animSetIndex = 0; animSetIndex < bank.AnimSets.Count; animSetIndex++)
        {
            var directions = bank.AnimSets[animSetIndex];
            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                var animation = directions[directionIndex];
                if (animation == null || animation.Frames.Count == 0)
                {
                    continue;
                }

                foreach (var frame in animation.Frames)
                {
                    quadsRead += frame.Quads.Count;
                }

                var animationName = $"bank{bank.BankKey}_anim{animSetIndex}_{DirectionNames[directionIndex]}";
                quadsConverted += ConvertAnimation(
                    animation, animationName, bankRelativeDirectory, bank.SourceSpritesheetFileName,
                    textureAssetId, spriteAssetIdsByKey, outputDirectory, report);
            }
        }

        report.Increment("Sprites.Banks");
        report.Increment("Sprites.QuadsRead", quadsRead);
        report.Increment("Sprites.QuadsConverted", quadsConverted);
    }

    private static int ConvertAnimation(
        SpriteAnimation animation,
        string animationName,
        string bankRelativeDirectory,
        string spritesheetFileName,
        Guid textureAssetId,
        Dictionary<(string Spritesheet, long Signature), Guid> spriteAssetIdsByKey,
        string outputDirectory,
        ConversionReport report)
    {
        // Frame start times. The trailing terminator frame carries a control code rather than a
        // duration (SpriteFrame.TerminatorCode), so it contributes nothing to the timeline - it
        // just lands on the end of the last displayed frame, where its all-parts-hidden keyframe
        // gives Animation2dData the animation's real duration.
        var frameTimes = new float[animation.Frames.Count];
        var cumulativeSeconds = 0f;
        for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
        {
            var frame = animation.Frames[frameIndex];
            frameTimes[frameIndex] = cumulativeSeconds;
            if (!frame.IsTerminator)
            {
                cumulativeSeconds += frame.DelayFrames * PsxFrameSeconds;
            }
        }

        var maxQuadCount = 0;
        foreach (var frame in animation.Frames)
        {
            maxQuadCount = Math.Max(maxQuadCount, frame.Quads.Count);
        }

        if (maxQuadCount == 0)
        {
            return 0;
        }

        var animationAsset = new Animation2dData
        {
            Name = animationName,
            AnimationType = AnimationType.Loop,
        };

        var convertedQuadCount = 0;

        for (var partIndex = 0; partIndex < maxQuadCount; partIndex++)
        {
            var partId = $"part{partIndex}";

            // Depth order. Alundra draws a frame's images in reverse array order at a single shared
            // entity depth (GraphicManager.RenderEntities: for idex = NumberOfImages-1 downto 0),
            // so image[0] is painted last and ends up frontmost, image[N-1] backmost. CasaEngine
            // draws higher DrawOrder in front (SpriteRendererComponent sorts higher z to the back;
            // zOrder = Z - DrawOrder*k, and the depth-sortable path adds DrawOrder to
            // LocalSortOffset the same way). So part[0] (=image[0]) must get the highest DrawOrder.
            // This ordering is purely index-based and identical for every frame, so it lives on the
            // part default rather than a per-frame track.
            animationAsset.Parts.Add(new Animation2dPartData
            {
                Id = partId,
                Name = partId,
                DefaultVisible = false,
                DefaultDrawOrder = maxQuadCount - 1 - partIndex,
            });

            var spriteTrack = NewTrack(partId, Animation2dTrackProperty.Sprite);
            var positionTrack = NewTrack(partId, Animation2dTrackProperty.Position);
            var visibleTrack = NewTrack(partId, Animation2dTrackProperty.Visible);
            var flipXTrack = NewTrack(partId, Animation2dTrackProperty.FlipX);
            var flipYTrack = NewTrack(partId, Animation2dTrackProperty.FlipY);
            var usesFlipX = false;
            var usesFlipY = false;

            for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
            {
                var frame = animation.Frames[frameIndex];
                var time = frameTimes[frameIndex];

                if (partIndex >= frame.Quads.Count)
                {
                    visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(time, false));
                    continue;
                }

                var quad = frame.Quads[partIndex];
                convertedQuadCount++;

                var spriteId = EnsureSpriteData(
                    quad, spritesheetFileName, textureAssetId, bankRelativeDirectory,
                    spriteAssetIdsByKey, outputDirectory);

                var minX = Math.Min(Math.Min(quad.X1, quad.X2), Math.Min(quad.X3, quad.X4));
                var maxX = Math.Max(Math.Max(quad.X1, quad.X2), Math.Max(quad.X3, quad.X4));
                var minY = Math.Min(Math.Min(quad.Y1, quad.Y2), Math.Min(quad.Y3, quad.Y4));
                var maxY = Math.Max(Math.Max(quad.Y1, quad.Y2), Math.Max(quad.Y3, quad.Y4));

                // Alundra's destination corners are screen-space Y-down (Y1 negative = above the
                // entity anchor). CasaEngine composes parts in a Y-up local space: the engine
                // renderer and Animation2dBoundsCalculator both place a part's sprite so that its
                // Origin lands at part.Position with the sprite extending up by Origin.Y, and a
                // part sitting above the anchor needs a positive Position.Y. So X carries over
                // directly but Y must be negated. Missing this negation mirrors every part
                // vertically across the anchor: harmless-looking on single-part animations (the
                // editor preview recenters on the bounds, absorbing the global offset) but visibly
                // wrong on multi-part ones, where it flips the parts' arrangement relative to each
                // other.
                //
                // Half-pixel compensation. SpriteData.Origin is a Point, so a crop with an odd
                // width or height cannot store its true centre (a 23x31 crop centres on 11.5/15.5
                // but Origin holds 11/15). Both the renderer and Animation2dBoundsCalculator draw
                // the sprite centred on Position + (Width/2f - Origin.X, Origin.Y - Height/2f), so
                // that lost half pixel shifts the part unless Position absorbs it. The shift is
                // per-part - it only happens on odd dimensions - so parts of the same frame slide
                // relative to each other: bank0_anim1_down pairs a 23x31 body with a 15x24 legs
                // crop, dropping the body half a pixel onto the legs and eating their top row.
                var originOffset = new Vector2(
                    quad.Width / 2f - quad.Width / 2,
                    quad.Height / 2f - quad.Height / 2);
                var center = new Vector2(
                    (minX + maxX) / 2f - originOffset.X,
                    -(minY + maxY) / 2f + originOffset.Y);
                var flipX = quad.X1 > quad.X2;
                var flipY = quad.Y1 > quad.Y3;

                spriteTrack.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(time, spriteId));
                positionTrack.PositionKeyframes.Add(new Animation2dVector2KeyframeData(time, center));
                visibleTrack.VisibleKeyframes.Add(new Animation2dBoolKeyframeData(time, true));
                flipXTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(time, flipX));
                flipYTrack.FlipKeyframes.Add(new Animation2dBoolKeyframeData(time, flipY));
                usesFlipX |= flipX;
                usesFlipY |= flipY;
            }

            animationAsset.Tracks.Add(spriteTrack);
            animationAsset.Tracks.Add(positionTrack);
            animationAsset.Tracks.Add(visibleTrack);
            if (usesFlipX)
            {
                animationAsset.Tracks.Add(flipXTrack);
            }

            if (usesFlipY)
            {
                animationAsset.Tracks.Add(flipYTrack);
            }
        }

        var collisionKeyframeCount = ConvertCollisionKeyframes(animation, frameTimes, cumulativeSeconds, animationAsset);
        if (collisionKeyframeCount > 0)
        {
            report.Increment("Sprites.CollisionKeyframes", collisionKeyframeCount);
            report.Increment("Sprites.AnimationsWithCollision");
        }

        var relativePath = Path.Combine(bankRelativeDirectory, $"{animationName}.anim2d");
        EditorAssetWriterService.SaveAsset(relativePath, animationAsset);
        EditorAssetCatalogService.Add(new AssetInfo(animationAsset.Id)
        {
            Name = animationAsset.Name,
            FileName = relativePath,
        });
        report.Increment("Assets.Animation2d");

        return convertedQuadCount;
    }

    /// <summary>
    /// Turns the frames' SiFrame.CollisionData into Animation2dData.CollisionKeyframes, collapsing
    /// consecutive frames that repeat the same volume - the large majority of animations carry one
    /// constant hitbox and end up with a single keyframe at t=0.
    ///
    /// The trailing terminator frame never emits: it carries a control code rather than a duration
    /// and sits exactly on the animation's end, where the loop-wrapping sampler can never reach it.
    /// A volume active on the last displayed frame therefore stays active until the wrap, and the
    /// keyframe at t=0 (or its absence) decides what the next cycle starts with.
    /// </summary>
    private static int ConvertCollisionKeyframes(
        SpriteAnimation animation, float[] frameTimes, float durationSeconds, Animation2dData animationAsset)
    {
        SpriteFrameCollision? activeCollision = null;
        var emittedCount = 0;

        for (var frameIndex = 0; frameIndex < animation.Frames.Count; frameIndex++)
        {
            var frame = animation.Frames[frameIndex];
            if (frame.IsTerminator)
            {
                continue;
            }

            var time = frameTimes[frameIndex];

            // A keyframe landing on the duration is unreachable (the sampler wraps CurrentTime at
            // the duration), which only happens on a zero-length trailing frame. Skip it rather
            // than write a set no sample can ever select.
            if (durationSeconds > 0f && time >= durationSeconds)
            {
                continue;
            }

            var collision = frame.Collision;

            if (collision == null)
            {
                if (activeCollision == null)
                {
                    continue;
                }

                // Deactivation: an empty fixture set clears the volumes of the previous frames.
                animationAsset.CollisionKeyframes.Add(new Animation2dCollisionKeyframeData { TimeSeconds = time });
                activeCollision = null;
                emittedCount++;
                continue;
            }

            if (activeCollision != null && collision.HasSameVolumeAs(activeCollision))
            {
                continue;
            }

            var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = time };
            keyframe.Fixtures.Add(CreateColliderFixture(collision));
            animationAsset.CollisionKeyframes.Add(keyframe);
            activeCollision = collision;
            emittedCount++;
        }

        return emittedCount;
    }

    /// <summary>
    /// Alundra gives the box's MIN CORNER (Offset) plus its extents (Width along X/east, Depth
    /// along Y/ground depth, Height along Z/elevation) in pixels, with the origin at the entity's
    /// feet; the engine poses a fixture by the box's CENTRE, hence the +Size/2 shift. Sizes stay
    /// unscaled: the whole conversion works in Alundra pixels.
    ///
    /// No Y negation here, unlike the parts' Position above. These fixtures live in LOGICAL space -
    /// AnimatedSpriteComponent poses the timeline bodies from the entity root's logical transform,
    /// never from the space it renders in - whereas the parts live in RENDER space and keep the
    /// historical Y negation. The two spaces coexist inside one asset by design; it only reads as
    /// an inconsistency.
    ///
    /// ProfileName and Tag stay empty on purpose: CollisionData carries no attack/defence
    /// semantics (that lives in the event bytecode, out of scope for this converter), so inventing
    /// an AttackVolume/DamageableVolume profile would be fiction. An empty profile makes the engine
    /// fall back to Trigger.
    /// </summary>
    private static ColliderFixture CreateColliderFixture(SpriteFrameCollision collision)
    {
        return new ColliderFixture
        {
            Shape = new Box { Size = new Vector3(collision.Width, collision.Depth, collision.Height) },
            LocalPosition = new Vector3(
                collision.OffsetX + collision.Width / 2f,
                collision.OffsetY + collision.Depth / 2f,
                collision.OffsetZ + collision.Height / 2f),
            LocalRotation = Quaternion.Identity,
            ProfileName = string.Empty,
            Tag = string.Empty,
        };
    }

    private static Animation2dTrackData NewTrack(string partId, Animation2dTrackProperty property)
    {
        return new Animation2dTrackData
        {
            TargetPartId = partId,
            Property = property,
            Interpolation = Animation2dInterpolationMode.Step,
        };
    }

    private static Guid EnsureSpriteData(
        SpriteQuad quad,
        string spritesheetFileName,
        Guid textureAssetId,
        string bankRelativeDirectory,
        Dictionary<(string Spritesheet, long Signature), Guid> spriteAssetIdsByKey,
        string outputDirectory)
    {
        var key = (spritesheetFileName, quad.Signature);
        if (spriteAssetIdsByKey.TryGetValue(key, out var existingId))
        {
            return existingId;
        }

        var spriteData = new SpriteData
        {
            SpriteSheetAssetId = textureAssetId,
            PositionInTexture = new Rectangle(quad.AtlasX, quad.AtlasY, quad.Width, quad.Height),
            Origin = new Point(quad.Width / 2, quad.Height / 2),
            Name = $"sprite_{quad.Signature}",
        };
        spriteData.FileName = Path.Combine(bankRelativeDirectory, $"{spriteData.Name}.sprite");

        EditorAssetWriterService.SaveAsset(spriteData.FileName, spriteData);
        EditorAssetCatalogService.Add(new AssetInfo(spriteData.Id)
        {
            Name = spriteData.Name,
            FileName = spriteData.FileName,
        });

        spriteAssetIdsByKey[key] = spriteData.Id;
        return spriteData.Id;
    }

    // Spritesheets stay under Sprites/Textures because they belong to no single entity: several
    // banks share one map spritesheet. The actual copy/catalog work is TextureAssetWriter's, which
    // Phase 7 reuses for the UI textures.
    private static Guid EnsureSpritesheetTexture(
        string inputDirectory, string outputDirectory, string spritesheetFileName,
        Dictionary<string, Guid> textureAssetIdsBySpritesheet)
    {
        var sourcePngPath = Path.Combine(inputDirectory, "data", spritesheetFileName);
        return TextureAssetWriter.EnsureTexture(
            sourcePngPath, Path.Combine("Sprites", "Textures"), outputDirectory, textureAssetIdsBySpritesheet);
    }

    private static void PreserveHeroEffects(string inputDirectory, string outputDirectory, ConversionReport report)
    {
        var heroPath = Path.Combine(inputDirectory, "data", "map_alundra.json");
        if (!File.Exists(heroPath))
        {
            return;
        }

        using var stream = File.OpenRead(heroPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("SpriteInfo", out var spriteInfoElement)
            || !spriteInfoElement.TryGetProperty("SpriteEffectRecords", out var effectsElement)
            || effectsElement.ValueKind != JsonValueKind.Array)
        {
            report.Warnings.Add("Hero SpriteEffectRecords not found in map_alundra.json.");
            return;
        }

        var heroDirectory = Path.Combine(outputDirectory, "Sprites", "hero");
        Directory.CreateDirectory(heroDirectory);
        File.WriteAllText(Path.Combine(heroDirectory, "hero_effects.json"), effectsElement.GetRawText());

        report.Increment("Sprites.HeroEffectsPreserved", effectsElement.GetArrayLength());
    }
}
