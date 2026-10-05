#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Application.Components;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using CasaEngine.Framework.Scene.World;
using CasaEngine.Framework.Scripting;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// Rendering instance wiring extracted out of <see cref="AlundraWorldProxy"/>
/// (docs/plan-update-caracterisation.md, slice S3), with NO behaviour change - see that plan's §3
/// "S3" for the extraction rules this class follows (the "règle de preuve étendue" defined ahead of
/// S2 and reused here) and AlundraWorldProxyUpdateCharacterizationTests for the oracle this move must
/// keep satisfying with zero assertions changed.
///
/// Owns the one-time background-clear-color lookup (<see cref="ApplyOriginalBackgroundClearColorOnce"/>)
/// and, since plan E9.b (S2), the engine-side scrolling-layers adaptation: <see cref="Load"/> (translates
/// this world's backdrop companion and pushes it to the attached <see cref="ScrollingLayerService"/>) and
/// <see cref="PushFrame"/> (pushes this frame's scroll/ticks to it) - the rendering mechanism itself now
/// lives in the engine (<c>CasaEngine.Framework.Rendering.ScrollingLayers</c>,
/// <c>docs/engine/scrolling-layers.md</c>); this class keeps only the Alundra-specific POLICY (D-E9b-8).
///
/// <c>AlundraWorldProxy.InitializeWithWorld</c> calls <see cref="AttachService"/> then <see cref="Load"/>
/// itself, in that order (D-E9b-2) - <see cref="Load"/> only parses this world's own backdrop companion
/// and pushes pure data to the engine service; texture resolution happens later, engine-side, in
/// <see cref="ScrollingLayerComponent.Update"/> (D-E9b-7), so this stage never touches a
/// <c>GraphicsDevice</c> at all any more.
///
/// The world and the resolved debug camera (owned by <see cref="AlundraCameraDirector"/>, S2) are read by
/// the caller at USE TIME and passed in per frame rather than captured here (extended proof rule
/// delta (a)): a stage that captured <c>World</c> at construction would predate
/// <c>AlundraWorldProxy.InitializeWithWorld</c> assigning it, and the resolved camera is this stage's own
/// collaborator's state, not its own - passing it in keeps this class from reaching into
/// <c>AlundraCameraDirector</c> itself.
/// </summary>
internal sealed class AlundraBackdropStage
{
    /// <summary>Cached once <see cref="ApplyOriginalBackgroundClearColorOnce"/> has set the world's
    /// runtime view <see cref="CasaEngine.Framework.Rendering.RenderView.ClearColor"/> - the view does not
    /// exist yet when <see cref="AlundraWorldProxy.InitializeWithWorld"/> runs (<c>GameManager.EndLoadContent</c>
    /// calls <c>World.LoadContent</c>, which drives the proxy, strictly before
    /// <c>IRuntimeViewBootstrapper.BootstrapViews</c>), so the lookup is retried lazily from
    /// <see cref="AlundraWorldProxy.Update"/>, mirroring <c>AlundraCameraDirector</c>'s own
    /// one-time-retry shape for the debug camera lookup.</summary>
    private bool _clearColorApplied;

    /// <summary>
    /// Plan E9.b (docs/plan-e9b-backdrops-moteur.md, D-E9b-2) - the engine-side mechanism this stage
    /// pushes frame state to via <see cref="PushFrame"/> and loads layer/tint definitions into via
    /// <see cref="Load"/>. Attached (never constructed here) by
    /// <c>AlundraWorldProxy.InitializeWithWorld</c>, same acquisition shape as
    /// <c>AlundraScreenFadeDirector.AttachToWorld</c>, BEFORE <see cref="Load"/> runs (D-E9b-2). A
    /// <see langword="null"/> service (headless montage without <see cref="AttachService"/>, or a
    /// production world whose <c>Game</c> has none) makes both <see cref="Load"/> and
    /// <see cref="PushFrame"/> no-ops - <see cref="Load"/> additionally warns once when the world's
    /// <c>Game</c> is NOT null (the production "arrêt" case: a live game with no scrolling-layers
    /// component is a bug, not a legitimate degraded mode).
    /// </summary>
    private ScrollingLayerService? _service;

    /// <summary>
    /// D-E9d: the engine-side mechanism behind Cellular (mode 2) backdrop layers, sibling to
    /// <see cref="_service"/> - same attach/load/push shape, same null-is-a-no-op contract. Attached
    /// (never constructed here) by <c>AlundraWorldProxy.InitializeWithWorld</c> alongside
    /// <see cref="_service"/>, BEFORE <see cref="Load"/> runs.
    /// </summary>
    private CellularLayerService? _cellularService;

    /// <summary>Attaches (or detaches, with <see langword="null"/>) this stage's engine-side scrolling-
    /// layers mechanism - see <see cref="_service"/>'s own doc. Internal so <c>Alundra.Tests</c> (via
    /// <c>InternalsVisibleTo</c>) can inject a real <see cref="ScrollingLayerService"/> on a headless
    /// montage, exactly the shape D-E9b-2 specifies for production.</summary>
    internal void AttachService(ScrollingLayerService? service)
    {
        _service = service;
    }

    /// <summary>Attaches (or detaches, with <see langword="null"/>) this stage's engine-side cellular-
    /// layers mechanism - see <see cref="_cellularService"/>'s own doc. Internal so <c>Alundra.Tests</c>
    /// (via <c>InternalsVisibleTo</c>) can inject a real <see cref="CellularLayerService"/> on a headless
    /// montage, mirroring <see cref="AttachService(ScrollingLayerService?)"/>.</summary>
    internal void AttachCellularService(CellularLayerService? service)
    {
        _cellularService = service;
    }

    /// <summary>Set once the "no layer service attached" warning of <see cref="SetLayerMask"/> was logged.</summary>
    private bool _loggedMaskWithoutService;

    /// <summary>
    /// Opcode 0xA4's mask (E19.k2, docs/plan-e19-opcodes.md section 1.2k.2, rule K2-R2): for each layer identifier of the
    /// binary <c>id</c> from 0 to 1, <c>SetLayerActive(id, (mask &amp; (1 &lt;&lt; id)) != 0)</c> on the attached scrolling
    /// and cellular services - the binary tests bit 0 for the layer of identifier 0 and bit 1 for the layer of identifier 1,
    /// whatever their kind, and a masked layer is frozen and not drawn (engine side, ADR-0049). A layer is addressed by its
    /// identifier (<c>StableId</c> / <c>LayerId</c>, set from the binary's own id by <see cref="BuildDefinitions"/> /
    /// <see cref="BuildCellularDefinitions"/>), never by its place in the service: the export drops some layers. A new
    /// <see cref="Load"/> makes every layer active again (the binary's own <c>SetScrollingMode(3, 0)</c> at map load).
    /// With no service attached (headless montage) it logs once and still reports handled, so the opcode does not trace
    /// as degraded for that reason alone.
    /// </summary>
    internal bool SetLayerMask(int mask)
    {
        if (_service == null && _cellularService == null)
        {
            if (!_loggedMaskWithoutService)
            {
                _loggedMaskWithoutService = true;
                Logs.WriteWarning("AlundraBackdropStage: background layer mask set but no layer service attached; ignored.");
            }

            return true;
        }

        for (var id = 0; id <= 1; id++)
        {
            var active = (mask & (1 << id)) != 0;
            _service?.SetLayerActive(id, active);
            _cellularService?.SetLayerActive(id, active);
        }

        return true;
    }

    /// <summary>Faithful port (E2, docs/plan-e2-rendu.md) of the original engine's own background clear
    /// (<c>AlundraGame.Draw</c>'s <c>GraphicsDevice.Clear(Color.Black)</c>, both for the game's off-screen
    /// render target and the final backbuffer blit -
    /// alundra-datas-analyser/AlundraTools/AlundraGame/AlundraGame.cs:199,236) instead of the engine's
    /// default <c>Color.CornflowerBlue</c> (<see cref="CasaEngine.Framework.Application.DefaultRuntimeViewBootstrapper"/>):
    /// without this, every pixel no cell tile (or, now, no scrolling background layer) covers
    /// shows turquoise instead of the black the original always drew there. Retried lazily once per world
    /// from <see cref="AlundraWorldProxy.Update"/> (see <see cref="_clearColorApplied"/>'s own doc for why
    /// <see cref="AlundraWorldProxy.InitializeWithWorld"/> is too early to find the view).
    ///
    /// <paramref name="world"/> is read at USE TIME only, never captured (extended proof rule delta (a)) -
    /// the caller passes <c>AlundraWorldProxy</c>'s own <c>_world</c> field, which this class never
    /// stores.
    /// </summary>
    internal void ApplyOriginalBackgroundClearColorOnce(World? world)
    {
        if (_clearColorApplied || world?.Game == null)
        {
            return;
        }

        foreach (var view in world.Game.GameManager.ViewManager.Views)
        {
            if (view.World == world)
            {
                view.ClearColor = Color.Black;
                _clearColorApplied = true;
                break;
            }
        }
    }

    /// <summary>
    /// Loads this map's backdrop companion (if any) and pushes its translation to <see cref="_service"/>
    /// (D-E9b-9: <c>Clear()</c> then <c>SetConfiguration</c>/<c>SetLayers</c>/<c>SetTint</c> from
    /// <see cref="BuildDefinitions"/> - always in that order, once per <c>AlundraWorldProxy.InitializeWithWorld</c>
    /// call, so a world with no companion at all still clears out whatever the PREVIOUS world left
    /// behind). Called from <c>AlundraWorldProxy.InitializeWithWorld</c> AFTER <see cref="AttachService"/>,
    /// never from the frame path.
    ///
    /// A <see langword="null"/> <see cref="_service"/> is a no-op - EXCEPT that a production world (one
    /// whose <paramref name="world"/>.<c>Game</c> is not <see langword="null"/>) with no service attached
    /// is a bug (D-E9b-2's own "arrêt" clause: <c>world.Game.ScrollingLayerComponent</c> did not exist,
    /// or its <c>Service</c> was null) - logged ONCE per call as a warning ("lire le journal d'abord"),
    /// never thrown, so a degraded/no-op frame is still visibly diagnosable.
    /// </summary>
    internal void Load(World world, string projectPath)
    {
        if (_service == null)
        {
            if (world.Game != null)
            {
                Logs.WriteWarning(
                    $"AlundraBackdropStage: world '{world.Name}' has a live Game but no ScrollingLayerService "
                    + "was attached (AttachService); scrolling background layers will not render.");
            }

            return;
        }

        _service.Clear();
        _cellularService?.Clear();

        var document = BackdropLoader.Load(projectPath, world.Name);
        if (document == null)
        {
            return;
        }

        var (layers, tint, configuration) = BuildDefinitions(document);
        _service.SetConfiguration(configuration);
        _service.SetLayers(layers);
        _service.SetTint(tint);

        // D-E9d: same "always push, even with zero layers" contract as the sibling above - a world
        // with no Cellular layer still clears out whatever the PREVIOUS world's cellular service held.
        if (_cellularService != null)
        {
            var cellularLayers = BuildCellularDefinitions(document);
            _cellularService.SetLayers(cellularLayers);
            _cellularService.SetWaveLut(document.WaveLut ?? Array.Empty<int>());
        }
    }

    /// <summary>
    /// Plan E9.b (D-E9b-8) - PURE translation of a loaded <see cref="BackdropDocument"/> into the engine
    /// mechanism's own data (<c>CasaEngine.Framework.Rendering.ScrollingLayers</c>), applying EXACTLY the
    /// rules the now-retired <c>BackdropRenderer.Load</c> applied: a layer is translated only if
    /// <c>Mode == "Tiles" &amp;&amp; Scrollar != null &amp;&amp; TextureAssetId</c> is non-empty (the same
    /// two guards as that method's own early-`continue` - a <c>Disabled</c>/<c>Cellular</c> layer, a
    /// <c>Tiles</c> layer with a null <see cref="BackdropScrollarData"/>, or one with an empty
    /// <see cref="BackdropLayerData.TextureAssetId"/> is silently absent from the result, never an
    /// exception); <c>Ground</c> only chooses the pass (<see cref="RenderPass2D.Effects"/> or
    /// <see cref="RenderPass2D.Background"/>) and <c>BlendMode</c> resolves to the PSX mode of the layer through
    /// <see cref="ResolveLayerPsxSemiTransparency"/> (E19.g G2c, D-E19-68: the layer is an opaque white one, its
    /// <c>Blend</c> and <c>Tint</c> are not used, the engine draws the opaque and the STP texels of its sheet in
    /// two passes); <c>SortingLayer</c> is always 0, <c>OrderInLayer</c> is
    /// <see cref="BackdropLayerData.DepthOrder"/>, <c>StableId</c> is <see cref="BackdropLayerData.LayerId"/>;
    /// frame ids reuse <see cref="ResolveFrameAssetIds"/> (same
    /// <c>FrameTextureAssetIds ?? [TextureAssetId]</c> fallback already tested there) then convert each
    /// string to a <see cref="Guid"/> of the SAME LENGTH - a null, empty or unparsable id becomes
    /// <see cref="Guid.Empty"/>, NEVER an exception and NEVER a shortened array (D-E9-9's own fallback
    /// runs afterward, engine-side - see <see cref="ScrollingLayerComponent.ResolveTextures"/>). The
    /// overlay tint is independent of any layer (mirrors the old renderer's own "layers and/or tint"
    /// <c>HasContent</c> contract) - <c>(R, G, B, 255)</c> with the PSX mode of the map's <c>BGColorA</c>
    /// (<see cref="BackdropDocument.OverlayBlendMode"/>, E19.g G2d; the average when it is 0 or past the table) at
    /// <see cref="RenderPass2D.Effects"/> sorting −1, strictly below every <c>Ground=1</c> layer's <c>SortingLayer</c> 0 key. The
    /// configuration is always the fixed 640x480 canvas / 320x240 view (D-E9b-5).
    /// </summary>
    internal static (ScrollingLayerDefinition[] Layers, ScrollingTintDefinition? Tint, ScrollingLayerConfiguration Configuration) BuildDefinitions(
        BackdropDocument document)
    {
        var layers = new List<ScrollingLayerDefinition>();

        foreach (var layer in document.Layers)
        {
            if (layer.Mode != "Tiles" || layer.Scrollar == null || string.IsNullOrEmpty(layer.TextureAssetId))
            {
                continue;
            }

            var scrollar = layer.Scrollar;
            var renderPass = layer.Ground ? RenderPass2D.Effects : RenderPass2D.Background;

            var frameAssetIds = ResolveFrameAssetIds(layer);
            var frameTextureAssetIds = new Guid[frameAssetIds.Length];
            for (var frameIndex = 0; frameIndex < frameAssetIds.Length; frameIndex++)
            {
                frameTextureAssetIds[frameIndex] = ParseFrameAssetIdOrEmpty(frameAssetIds[frameIndex]);
            }

            layers.Add(new ScrollingLayerDefinition
            {
                FrameTextureAssetIds = frameTextureAssetIds,
                FactorXNum = scrollar.FactorXNum,
                FactorXDenom = scrollar.FactorXDenom,
                FactorYNum = scrollar.FactorYNum,
                FactorYDenom = scrollar.FactorYDenom,
                ScrollXSpeed = scrollar.ScrollXSpeed,
                ScrollXPeriod = scrollar.ScrollXPeriod,
                ScrollYSpeed = scrollar.ScrollYSpeed,
                ScrollYPeriod = scrollar.ScrollYPeriod,
                AnimTimer = layer.AnimTimer,
                Pass = renderPass,
                SortingLayer = 0,
                OrderInLayer = layer.DepthOrder,
                StableId = layer.LayerId,
                Blend = SpriteBlendMode.Opaque,
                Tint = Color.White,
                PsxSemiTransparency = ResolveLayerPsxSemiTransparency(layer.BlendMode),
            });
        }

        ScrollingTintDefinition? tintDefinition = null;
        if (document.OverlayEnabled)
        {
            // E19.g G2d (O-E19-58, ADR-0037): the colour is opaque and the mode of the map (BGColorA) alone decides the blend
            // state; the engine bakes the alpha of the average itself. No mode (0: an export that predates the field, or any
            // value past the table of the binary) keeps today's drawing, the average.
            var tintColor = new Color(document.OverlayColorR, document.OverlayColorG, document.OverlayColorB, (byte)255);
            var tintSortKey = new RenderSortKey2D((int)RenderPass2D.Effects, -1, 0, 0, 0, 0, 0);
            var tintMode = ResolveLayerPsxSemiTransparency(document.OverlayBlendMode);
            if (tintMode == SpritePsxSemiTransparency.None)
            {
                tintMode = SpritePsxSemiTransparency.Mode0;
            }

            tintDefinition = new ScrollingTintDefinition(tintColor, tintSortKey, tintMode);
        }

        // 640x480: the original's own wrapping canvas size (D-E9b-5) - was BackdropOffsetMath.CanvasWidth/
        // Height before that file's retirement (plan E9.b, S2); the engine's own ScrollingLayerService
        // carries no such constant since the canvas size is per-configuration, not fixed mechanism-side.
        // BackgroundDepth 1: a Background-pass layer recedes to cameraTarget.Z - 1 (plan
        // plan-e9c-defauts-321.md, D-E9c-5), reproducing the original's own policy of placing
        // Ground=false layers behind every floor, wall and entity (GraphicManager.cs:825-826).
        var configuration = new ScrollingLayerConfiguration(
            640, 480, (int)AlundraCameraMath.CameraVisibleWidth, (int)AlundraCameraMath.CameraVisibleHeight,
            backgroundDepth: 1f);

        return (layers.ToArray(), tintDefinition, configuration);
    }

    /// <summary>
    /// D-E9d - PURE translation of a loaded <see cref="BackdropDocument"/> into the engine mechanism's
    /// own <c>CellularLayerDefinition[]</c> (<c>CasaEngine.Framework.Rendering.CellularLayers</c>): a
    /// layer is translated only if <c>Mode == "Cellular" &amp;&amp; Cellular != null</c> (same
    /// early-<c>continue</c> shape <see cref="BuildDefinitions"/> applies for <c>Tiles</c>); <c>BlendMode</c>
    /// resolves to the PSX mode of the layer through the SAME <see cref="ResolveLayerPsxSemiTransparency"/> policy
    /// the <c>Tiles</c> path uses (D6 - do not write a second policy; E19.g G2c: an opaque white layer, the mode
    /// alone decides, the render pass derives from <c>Ground</c> engine-side); <c>SortingLayer</c> is always 0,
    /// <c>OrderInLayer</c> is <see cref="BackdropLayerData.DepthOrder"/>, <c>LayerId</c> is
    /// <see cref="BackdropLayerData.LayerId"/>.
    ///
    /// A MAPPING TRAP, verified against the original (<c>GraphicManager.cs:999</c>): <c>AnimNum</c> is
    /// read at LINING level - <paramref name="document"/>'s own <see cref="BackdropDocument.AnimNum"/> -
    /// while <c>AnimTimer</c> is read from the LAYER - <see cref="BackdropLayerData.AnimTimer"/>. Do NOT
    /// swap these; they are two different scopes in the original despite both landing on the same
    /// engine-side struct.
    ///
    /// <c>SheetTextureAssetIds</c> comes from the DOCUMENT-level
    /// <see cref="BackdropDocument.CellularSheetTextureAssetIds"/> (the sheet and palettes are per map,
    /// shared by both layers) - each entry parses the same way <see cref="ParseFrameAssetIdOrEmpty"/>
    /// parses a frame id (null/empty/unparsable -&gt; <see cref="Guid.Empty"/>, never an exception). A
    /// null document-level array (no Cellular layer in this map) yields an empty
    /// <c>SheetTextureAssetIds</c>.
    /// </summary>
    internal static CellularLayerDefinition[] BuildCellularDefinitions(BackdropDocument document)
    {
        var layers = new List<CellularLayerDefinition>();

        var sheetIds = document.CellularSheetTextureAssetIds;
        var sheetGuids = sheetIds == null
            ? Array.Empty<Guid>()
            : sheetIds.Select(ParseFrameAssetIdOrEmpty).ToArray();

        foreach (var layer in document.Layers)
        {
            if (layer.Mode != "Cellular" || layer.Cellular == null)
            {
                continue;
            }

            var cellular = layer.Cellular;

            var cells = new CellularCellDefinition[cellular.Cells.Count];
            for (var cellIndex = 0; cellIndex < cellular.Cells.Count; cellIndex++)
            {
                var cell = cellular.Cells[cellIndex];
                cells[cellIndex] = new CellularCellDefinition
                {
                    PalDex = cell.PalDex,
                    U0 = cell.U0,
                    V0 = cell.V0,
                    U1 = cell.U1,
                    V1 = cell.V1,
                    Type = (CellularCellType)cell.Type,
                    X0 = cell.X0,
                    Y0 = cell.Y0,
                    CamXNum = cell.CamXNum,
                    CamXDen = cell.CamXDen,
                    CamYNum = cell.CamYNum,
                    CamYDen = cell.CamYDen,
                    DX = cell.DX,
                    PeriodX = cell.PeriodX,
                    DY = cell.DY,
                    PeriodY = cell.PeriodY,
                };
            }

            layers.Add(new CellularLayerDefinition
            {
                LayerId = layer.LayerId,
                AnimTimer = layer.AnimTimer, // LAYER level (mapping trap - see class doc above).
                AnimNum = document.AnimNum, // DOCUMENT level (mapping trap - see class doc above).
                Ground = layer.Ground,
                Blend = SpriteBlendMode.Opaque,
                Tint = Color.White,
                PsxSemiTransparency = ResolveLayerPsxSemiTransparency(layer.BlendMode),
                AWaveY = cellular.AWaveY,
                AWavePhase = cellular.AWavePhase,
                AWaveAmp = cellular.AWaveAmp,
                BWaveY = cellular.BWaveY,
                BWavePhase = cellular.BWavePhase,
                BWaveWeight = cellular.BWaveWeight,
                Cells = cells,
                SheetTextureAssetIds = sheetGuids,
                SortingLayer = 0,
                OrderInLayer = layer.DepthOrder,
            });
        }

        return layers.ToArray();
    }

    private static Guid ParseFrameAssetIdOrEmpty(string? assetIdString)
    {
        return Guid.TryParse(assetIdString, out var guid) ? guid : Guid.Empty;
    }

    /// <summary>
    /// D-E9-9 (docs/plan-e9-backdrops-residus.md §2/§3 slice B3), moved here from the now-retired
    /// <c>BackdropRenderer</c> (plan E9.b, S2) - the layer's frames actually load from, pure and
    /// static so it is testable without a <see cref="Microsoft.Xna.Framework.Graphics.GraphicsDevice"/>.
    /// A layer with no (or an empty) <see cref="BackdropLayerData.FrameTextureAssetIds"/> - every
    /// non-animated layer, and every companion written before this feature existed - resolves to
    /// exactly one id, <see cref="BackdropLayerData.TextureAssetId"/>; otherwise the array is returned
    /// as given (its <c>[0]</c> already equals <see cref="BackdropLayerData.TextureAssetId"/> by
    /// construction on the converter side).
    /// </summary>
    internal static string?[] ResolveFrameAssetIds(BackdropLayerData layer)
    {
        if (layer.FrameTextureAssetIds == null || layer.FrameTextureAssetIds.Length == 0)
        {
            return new[] { layer.TextureAssetId };
        }

        return layer.FrameTextureAssetIds;
    }

    /// <summary>
    /// E19.g G2c (D-E19-68, ADR-0035; replaces the E10.b mapping of <c>ResolveGroundLayerBlend</c> of
    /// docs/plan-e10-fondu.md §1.8, which gave one blend per layer and only to the <c>Ground</c> ones) - the PSX
    /// semi-transparency mode of a backdrop layer, from its <c>BlendMode</c> ALONE. [binary] The tiles
    /// (<c>0x8005BB24</c>) and the cells (<c>0x8005BDD4</c>) are set once at load with the semi bit equal to
    /// <c>BlendMode != 0</c> (<c>SetSemiTrans</c>, <c>0x8005BC34</c>, <c>0x8005BC64</c>, <c>0x8005BFB4</c>, <c>0x8005C03C</c>) and the
    /// rate of the layer is <c>BlendMode - 1</c>, by the primitive of mode set at the head of its slot
    /// (<c>0x8005B958</c>-<c>0x8005B9E4</c>); <c>Ground</c> only chooses the slot (<c>0x8005B8B0</c>). 0 = not semi-transparent
    /// (<see cref="SpritePsxSemiTransparency.None"/>), 1 = average (<c>Mode0</c>), 2 = additive (<c>Mode1</c>), 3 = subtractive
    /// (<c>Mode2</c>), 4 = quarter (<c>Mode3</c>). Any other value is <c>None</c>: the binary would read a table past its end and
    /// no layer of the corpus has one. A semi-transparent primitive only blends its STP texels (bit 15 of the colour word, alpha 128
    /// in the exported sheets), the others are drawn opaque: the engine does that per texel.
    /// </summary>
    internal static SpritePsxSemiTransparency ResolveLayerPsxSemiTransparency(int blendMode)
    {
        return blendMode switch
        {
            1 => SpritePsxSemiTransparency.Mode0,
            2 => SpritePsxSemiTransparency.Mode1,
            3 => SpritePsxSemiTransparency.Mode2,
            4 => SpritePsxSemiTransparency.Mode3,
            _ => SpritePsxSemiTransparency.None,
        };
    }

    /// <summary>
    /// Plan E9.b (D-E9b-2) - pushes this frame's original-scroll-space state to <see cref="_service"/>
    /// (a no-op while <see cref="_service"/> is <see langword="null"/>, see <see cref="_service"/>'s own
    /// doc): <see cref="AlundraCameraMath.ToOriginalScrollSpace"/> on <paramref name="resolvedCamera"/>'s
    /// <c>Target</c> (or <see cref="Vector3.Zero"/> with no resolved camera), fed to
    /// <see cref="ScrollingLayerService.SetFrame"/> alongside <paramref name="ticksThisFrame"/> and the
    /// UNCONVERTED <c>Target</c> (still needed engine-side to place the covering quads in world space).
    /// Called from <c>AlundraWorldProxy.Update</c> at the exact site the retired
    /// <c>UpdateAndDrawBackdrop</c> used to occupy (D-E9b-2, S2): every frame, outside the gameplay
    /// freeze gate, independent of <c>world.Game</c> or of whether this world has any layers at all.
    /// E19.m4 (D-E19-67, ADR-0034): the caller passes 0 ticks while a warp departure is in progress (the binary's
    /// transition loop never reaches its backdrop driver); the frame itself is still pushed.
    ///
    /// D-E9d: also pushes the SAME <paramref name="ticksThisFrame"/>/scroll/target to
    /// <see cref="_cellularService"/> (independently a no-op while <see langword="null"/>) - this is the
    /// ONE push site for both mechanisms, never a second call site elsewhere.
    /// </summary>
    internal void PushFrame(int ticksThisFrame, Camera2dComponent? resolvedCamera)
    {
        var cameraTarget = resolvedCamera?.Target ?? Vector3.Zero;
        var scroll = AlundraCameraMath.ToOriginalScrollSpace(cameraTarget);

        _service?.SetFrame(scroll.X, scroll.Y, ticksThisFrame, cameraTarget);
        _cellularService?.SetFrame(scroll.X, scroll.Y, ticksThisFrame, cameraTarget);
    }
}
