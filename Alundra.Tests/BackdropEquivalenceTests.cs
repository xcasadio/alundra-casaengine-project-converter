#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, §3 "S1", D-E9b-10) - the coexistence harness: for each
/// of the three acceptance maps (389, 159, 321), drives the OLD path (<see cref="BackdropRenderer"/>'s
/// own <see cref="BackdropRenderer.AdvanceAnimation"/>/<see cref="BackdropRenderer.Tick"/>/
/// <see cref="BackdropRenderer.Draw"/>, layers built by reflection exactly like
/// <see cref="BackdropRendererTests"/>'s own helpers) and the NEW path
/// (<see cref="AlundraBackdropStage.BuildDefinitions"/> + <see cref="ScrollingLayerService"/> +
/// <see cref="ScrollingLayerComponent.Submit"/>, both headless) tick-for-tick over 2000 ticks, frames of
/// 0/1/2/4 ticks, a <c>Target</c> trajectory clamped to the map bounds at <c>Z = 0</c> constant - and
/// asserts every frame's layer offset, submitted texture IDENTITY (<see cref="Assert.Same"/> - both
/// paths are fed the SAME <see cref="Texture2D"/> instances through
/// <see cref="ScrollingLayerComponent.ResolveTextures"/>/<see cref="BackdropRenderer.LoadLayerFrames"/>'s
/// own delegate), quad world positions, sort keys, blend, colours and z = 0 are EQUAL - the only
/// tolerated delta is the scissor rectangle: <c>(0,0,320,240)</c> on the old path (its own fixed
/// <c>fullViewport</c>) vs. the rectangle this test itself passes to the new path's <c>Submit</c>.
///
/// Neither <see cref="AlundraBackdropStage.AttachService"/> nor <see cref="AlundraBackdropStage.PushFrame"/>
/// is exercised here - this harness talks to <see cref="ScrollingLayerService"/>/
/// <see cref="ScrollingLayerComponent"/> directly, mirroring what S2 wires those two members to.
///
/// Both real-map tests FAIL, rather than skip, when <c>alundra-project/</c> is absent - see
/// <see cref="AlundraWorldProxyGlobalFreezeTests.FindProjectRoot"/>'s own message.
/// </summary>
public class BackdropEquivalenceTests
{
    // The original's own frozen map-389 bounds (AlundraCameraMath.ClampCameraTargetToMap's own doc) -
    // reused as a plausible, non-degenerate clamp for all three maps: this harness only needs A
    // trajectory that stays clamped at Z = 0 and covers a range of scroll values, not each map's real
    // dimensions.
    private const int MapWidthPx = 1248;
    private const int MapHeightPx = 960;

    [Fact]
    public void Equivalence_RealMap389_OldAndNewPathsAgree_OverTwoThousandTicks()
    {
        RunEquivalence("Ship Klark (beginning)-389");
    }

    [Fact]
    public void Equivalence_RealMap159_OldAndNewPathsAgree_OverTwoThousandTicks()
    {
        RunEquivalence("Fairy cave (underwater)-159");
    }

    [Fact]
    public void Equivalence_RealMap321_OldAndNewPathsAgree_OverTwoThousandTicks()
    {
        RunEquivalence("Arena Zorgia (Boss)-321");
    }

    private static void RunEquivalence(string worldName)
    {
        var projectRoot = AlundraWorldProxyGlobalFreezeTests.FindProjectRoot();
        var document = BackdropLoader.Load(projectRoot, worldName);
        Assert.NotNull(document);

        var (oldRenderer, texturesById) = BuildOldRenderer(document!, worldName);

        var (definitions, tintDefinition, configuration) = AlundraBackdropStage.BuildDefinitions(document!);
        var component = CreateHeadlessScrollingLayerComponent();
        component.Service.SetConfiguration(configuration);
        component.Service.SetLayers(definitions);
        component.Service.SetTint(tintDefinition);
        component.ResolveTextures(id => texturesById.TryGetValue(id, out var texture) ? texture : null!);

        var tickPattern = new[] { 0, 1, 2, 4 };
        var totalTicks = 0;
        var frameIndex = 0;
        var newScissor = new Rectangle(0, 0, 1280, 944);
        var oldFullViewportScissor = new Rectangle(0, 0, 320, 240);

        while (totalTicks < 2000)
        {
            var ticks = tickPattern[frameIndex % tickPattern.Length];

            // A deterministic pseudo-trajectory (different periods on X/Y, both always clamped) - Z is
            // always 0f (D-E9b's own Target.Z == 0 invariant, §0.2).
            var rawX = 160 + (frameIndex * 37) % 900;
            var rawY = -120 - (frameIndex * 53) % 900;
            var target = AlundraCameraMath.ClampCameraTargetToMap(new Vector3(rawX, rawY, 0f), MapWidthPx, MapHeightPx);
            Assert.Equal(0f, target.Z);
            var scroll = AlundraCameraMath.ToOriginalScrollSpace(target);

            // OLD path: same order as AlundraBackdropStage.UpdateAndDrawBackdrop - AdvanceAnimation
            // first, then Tick, then Draw. elapsedTime = ticks * 0.02f gives exactly `ticks` flottant
            // ticks (0.02f/0.04f/0.08f * 50f round to exact 1f/2f/4f - see the plan's own §1.2 note).
            oldRenderer.AdvanceAnimation(ticks);
            oldRenderer.Tick(ticks * 0.02f);
            var oldSpriteRenderer = CreateSpriteRendererComponent();
            oldRenderer.Draw(
                oldSpriteRenderer, scroll.X, scroll.Y, target,
                (int)AlundraCameraMath.CameraVisibleWidth, (int)AlundraCameraMath.CameraVisibleHeight);

            // NEW path: SetFrame arms, Advance consumes (D-E9b-3) - same `ticks` int, no float clock.
            component.Service.SetFrame(scroll.X, scroll.Y, ticks, target);
            component.Service.Advance();
            var newSpriteRenderer = CreateSpriteRendererComponent();
            component.Submit(newSpriteRenderer, target, newScissor);

            // Layer offset (both companions' single Tiles layer, index 0).
            var oldOffset = oldRenderer.LastLayerOffsetForTests!.Value;
            Assert.True(component.Service.TryGetLayerState(0, out var newState));
            Assert.Equal((int)oldOffset.OffsetX, newState.LayerOffsetX);
            Assert.Equal((int)oldOffset.OffsetY, newState.LayerOffsetY);

            var oldQuads = GetSpriteDatas(oldSpriteRenderer);
            var newQuads = GetSpriteDatas(newSpriteRenderer);
            Assert.Equal(oldQuads.Count, newQuads.Count);

            for (var i = 0; i < oldQuads.Count; i++)
            {
                var oldQuad = oldQuads[i]!;
                var newQuad = newQuads[i]!;

                Assert.Same(GetField(oldQuad, "Texture"), GetField(newQuad, "Texture"));

                var oldMatrix = (Matrix)GetField(oldQuad, "WorldMatrix");
                var newMatrix = (Matrix)GetField(newQuad, "WorldMatrix");
                Assert.Equal(oldMatrix.Translation.X, newMatrix.Translation.X, 3);
                Assert.Equal(oldMatrix.Translation.Y, newMatrix.Translation.Y, 3);
                Assert.Equal(0f, oldMatrix.Translation.Z);
                Assert.Equal(0f, newMatrix.Translation.Z);

                Assert.Equal((RenderSortKey2D)GetField(oldQuad, "SortKey"), (RenderSortKey2D)GetField(newQuad, "SortKey"));
                Assert.Equal((SpriteBlendMode)GetField(oldQuad, "BlendMode"), (SpriteBlendMode)GetField(newQuad, "BlendMode"));
                Assert.Equal((Color)GetField(oldQuad, "Color"), (Color)GetField(newQuad, "Color"));

                // The one tolerated delta: full-viewport scissor (old, fixed) vs. the rectangle this
                // test itself passed to the new path.
                Assert.Equal(oldFullViewportScissor, (Rectangle)GetField(oldQuad, "ScissorRectangle"));
                Assert.Equal(newScissor, (Rectangle)GetField(newQuad, "ScissorRectangle"));
            }

            totalTicks += ticks;
            frameIndex++;
        }
    }

    /// <summary>
    /// Builds an OLD <see cref="BackdropRenderer"/> with its <c>_layers</c> populated exactly like
    /// <see cref="BackdropRenderer.Load"/> would (same guards, same
    /// <see cref="BackdropRenderer.ResolveFrameAssetIds"/>/<see cref="BackdropRenderer.LoadLayerFrames"/>/
    /// <see cref="BackdropRenderer.ResolveGroundLayerBlend"/> calls), via reflection into the private
    /// <c>LayerRuntime</c> nested type - same technique as <c>BackdropRendererTests</c>'s own helpers,
    /// duplicated here rather than shared (that class' helpers are private). Every frame texture id
    /// string encountered is resolved to ONE <see cref="Texture2D"/> instance per distinct
    /// <see cref="Guid"/>, returned in <paramref name="worldName"/>'s own <see cref="Dictionary{TKey,TValue}"/>
    /// so the NEW path's <see cref="ScrollingLayerComponent.ResolveTextures"/> can be fed the exact same
    /// instances (D-E9b-10's own "mêmes instances Texture2D").
    /// </summary>
    private static (BackdropRenderer Renderer, Dictionary<Guid, Texture2D> TexturesById) BuildOldRenderer(
        BackdropDocument document, string worldName)
    {
        var texturesById = new Dictionary<Guid, Texture2D>();
        var renderer = new BackdropRenderer();

        var layerRuntimeType = typeof(BackdropRenderer).GetNestedType("LayerRuntime", BindingFlags.NonPublic);
        Assert.NotNull(layerRuntimeType);
        var layersField = typeof(BackdropRenderer).GetField("_layers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(layersField);
        var layers = (System.Collections.IList)layersField!.GetValue(renderer)!;

        foreach (var layer in document.Layers)
        {
            if (layer.Mode != "Tiles" || layer.Scrollar == null || string.IsNullOrEmpty(layer.TextureAssetId))
            {
                continue;
            }

            var frameAssetIds = BackdropRenderer.ResolveFrameAssetIds(layer);

            Texture2D? LoadFrameTexture(string assetIdString)
            {
                if (!Guid.TryParse(assetIdString, out var guid))
                {
                    return null;
                }

                if (!texturesById.TryGetValue(guid, out var texture))
                {
                    texture = CreateTexture();
                    texturesById[guid] = texture;
                }

                return texture;
            }

            var frames = BackdropRenderer.LoadLayerFrames(frameAssetIds, LoadFrameTexture, worldName, layer.LayerId);
            Assert.NotNull(frames); // none of the three acceptance maps degrade.

            var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;
            var sortKey = new RenderSortKey2D((int)renderPass, 0, layer.DepthOrder, 0, 0, 0, layer.LayerId);
            var (blendMode, tint) = BackdropRenderer.ResolveGroundLayerBlend(layer.Ground, layer.BlendMode);

            var layerRuntime = Activator.CreateInstance(
                layerRuntimeType!, layer.Scrollar, frames, layer.AnimTimer, sortKey, tint, blendMode)!;
            layers.Add(layerRuntime);
        }

        return (renderer, texturesById);
    }

    private static Texture2D CreateTexture()
    {
        return (Texture2D)RuntimeHelpers.GetUninitializedObject(typeof(Texture2D));
    }

    private static SpriteRendererComponent CreateSpriteRendererComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());

        return new SpriteRendererComponent(game);
    }

    private static ScrollingLayerComponent CreateHeadlessScrollingLayerComponent()
    {
        var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
        var componentsField = typeof(Game).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(componentsField);
        componentsField!.SetValue(game, new GameComponentCollection());

        return new ScrollingLayerComponent(game);
    }

    private static System.Collections.IList GetSpriteDatas(SpriteRendererComponent component)
    {
        var field = typeof(SpriteRendererComponent).GetField("_spriteDatas", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (System.Collections.IList)field!.GetValue(component)!;
    }

    private static object GetField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance)!;
    }
}
