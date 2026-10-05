# E19.f2b1 discovery, surface "engine / MGUI view side" (read-only)

Parent repo `chantier/e19-suite` at `897504b` (the other agent's test-only m6 edits ignored), engine submodule
`CasaEngineMonogame` at `ebeb81c9` (`integration/merge-2026-10-05`; its working tree only holds the author's
`CasaEngine.Launcher/Program.cs`, untouched). Nothing built, run, exported, edited, staged or committed in the repository.
Everything I built and ran lives under this folder (`probe/`, `bin/`, `out/`, `refs/`). The probe reads the already-built DLLs
of `Alundra.Tests/bin/Debug/net9.0-windows` (copied once) and the exported `alundra-project/` (read-only).

Tags: **[code]** read in the repository; **[decomp]** read in the decompilation of the shipped FontStashSharp 1.5.7 DLL
(`ilspycmd` into `fss/src-monogame/`, scratch only); **[probe]** measured by the throwaway real-GPU probe below;
**[hyp]** not proven.

## Summary

1. **A real-GPU pixel test of the proposed screen is feasible and already works** in a throwaway probe: an `MGDesktop` built on the
   engine's own `CasaMonoGameBackendBootstrap.Create` runtime (public API only), real FontStashSharp text engine with the exported
   font3, the XAML below loaded through `UIScreenLoader` (Strict), drawn by `desktop.Draw()` into a `RenderTarget2D`, read back with
   `GetData`, compared texel for texel with an independent compositor (frame PNG + sprite rects + `.fnt` rectangles). **0 mismatching
   texels** at x1, x2 and x3, with a view offset in a larger target (ADR-0054), for the rest state, the slide-out clip lag (Y 182 /
   clip 181..230), a scroll of 6, the closed box, control glyphs 0x10/0x1C..., brackets and spaces, the 255-px band, the four
   cursor sprites by bound `SourceName` switch, and one-Update dynamic changes. 3.3 s for the whole run including the GPU start.
   **[probe]** (section 6).
2. **The proposed XAML is exact with these TextBlock attributes: `Padding="0" LinePadding="0" WrapText="False"
   VerticalContentAlignment="Top" Width="255"`** plus `AllowsInlineFormatting="False" Foreground="White" FontFamily="font3"`.
   Negative controls: no `Width` -> 12 texels differ at x 287..288 (band truncation lost); theme-default wrap -> the 40-W row wraps to 2
   lines (height 34); default padding -> text drawn (+1, +1). **[probe]**
3. **O-E19-64 is confirmed on a real GPU: today's font3 texts are drawn 1 native pixel right and 1 down of their Canvas position**
   (default `MGTextBlock` padding (1,1,1,1), `MGTextBlock.cs:1363`): the real `SaveScreen.xaml` + the real `AlundraSaveScreenViewModel`
   at x1 and x2 give "text drawn (+1,+1) px from the rule's position -> 0 mismatching texels". `Padding="0"` alone removes it for a
   one-line text. **[probe]**
4. **FontStashSharp/MGUI places a static bitmap glyph at `pen + xoffset, y + yoffset` of the text origin; pen += xadvance**
   **[decomp]** and **[probe]** for today's `.fnt` and for a `.fnt` rewritten with the annex rectangles (f2b0): same 0-texel result.
   Not exercised: non-zero xoffset/yoffset (the annex has every yoff 0 and xoffset 0), kerning (font3.fnt has none), characters absent
   from the `.fnt` (draw nothing, advance nothing: `StaticSpriteFont.GetGlyph` returns null, no default character).
5. **A bound change reaches the pixels only at the next `Desktop.Update()` (layout)**, except an `Image.SourceName` change, which
   applies at once. The engine runs the UI update BEFORE the world update (`CasaEngineGame.cs:537` vs `:555`), where the presenters
   write the view model. So the view is one render frame behind the box tick, and during that frame a changed cursor sprite is drawn
   with the previous layout. Measured. **[probe]** (gap G1)
6. **`Alundra.Tests` cannot create the engine's UI backend as the project stands**: `Alundra.Tests.csproj:17` pins
   `MonoGame.Framework.DesktopGL 3.8.4.1`, the engine pins 3.8.5.1 since 2026-09-13 (`Directory.Packages.props:18`), and the engine's
   `Content/Shaders/TexturedPrimitive.xnb` (MGFX version 11, built with the 3.8.5.1 content builder) is refused by the 3.8.4.1 runtime:
   `Effect.ReadHeader: "This MGFX effect seems to be for a newer release of MonoGame"`, thrown inside `CasaDesktopRuntime`'s constructor
   (`CasaDesktopRuntime.cs:98`). With the 3.8.5.1 `MonoGame.Framework.dll` (NuGet cache) everything above runs. **[probe]** (gap G5)
7. **Modal = `Layer => UILayer.Modal`, `IsModal => true`** like `DialogueScreen` (`DialogueScreen.cs:130-131`); it only skips the
   *screens'* `Update(GameTime)` callbacks below it and raises `HasModalInput` (pointer/keyboard routing); MGUI windows are all updated
   every frame by `Desktop.Update()` (`UIRoot.cs:131`) and drawn in `Desktop.Windows` order (`MGDesktop.cs:1715-1725`, stable
   `OrderBy(IsTopmost)`), `Layer` does not order anything. The earlier note "modal freezing of the HUD's MGUI update" is therefore
   inexact: only `IUIScreen.Update` callbacks freeze, and the HUD's is empty (`AlundraHudScreen.cs:168`). **[code]**
8. A screen written only in XAML needs `BorderThickness="0"` on the `Window` (the existing screens set it in code, `AlundraSaveScreen.cs:81-82`):
   without it the window draws a border and insets the canvas (13698 texels differ, first at (0,0) black); with it, and no code
   tweak at all, x1 and x2 are exact. **[probe]**

## 1. Exact XAML, properties, bindings, gaps

### 1.1 Where the authored sources live **[code]**
`alundra-project/UI/Screens/` is the only versioned part of the generated project (`.gitignore:62-68`): per screen `X.xaml`,
`X.uiscreen` (envelope with a fixed `id`, `source_xaml_file`, `preview_resolution`, `design_time_data_file`) and `X.design.json`
(`view_model_type` + `values`). The converter never writes there; `UiWriter.RegisterVersionedScreens` (`UiWriter.cs:161-196`) rebuilds
`AssetInfos.json` on each export and catalogues every `*.uiscreen` it finds under that folder with the envelope's `id`
(`ProjectWriter.SetDialogueScreenAsset` only for `UI/Screens/DialogueScreen.uiscreen`). Existing: Hud, Inventory, SubInventory, Save,
Dialogue. A new `TextBoxScreen.{xaml,uiscreen,design.json}` is found by the next export with no converter change (the catalogue name is
the envelope's `name`). Code side: `AlundraSaveScreen : XamlUIScreenBase` (`Alundra/Scripts/AlundraSaveScreen.cs`, not `Alundra/Screens/`,
which is empty), view model `AlundraSaveScreenViewModel`, presenter `AlundraSaveScreenPresenter`.

### 1.2 The XAML (the probed one, plus `BorderThickness="0"`) **[probe]**
```xml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
        xmlns:dataBinding="clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core"
        Left="0" Top="0" Width="320" Height="240"
        TitleText="" IsTitleBarVisible="False" IsTopmost="True" IsUserResizable="False"
        Padding="0" BorderThickness="0" Background="rgba(0,0,0,0)">
  <Canvas Name="RootCanvas" Width="320" Height="240" Visibility="{dataBinding:MGBinding Path=RootVisibility}">
    <!-- slot 0 of the binary's local table: the frame cells, then the cursor; never clipped by the text clip -->
    <Image Name="Frame" Stretch="None" SourceName="973a9208-c867-57fe-bee3-cf30237221ef"
           CanvasLeft="16" CanvasTop="{dataBinding:MGBinding Path=Frame.Top}" />
    <Image Name="Cursor" Stretch="None" SourceName="{dataBinding:MGBinding Path=Cursor.SourceName}"
           CanvasLeft="288" CanvasTop="{dataBinding:MGBinding Path=Cursor.Top}"
           Visibility="{dataBinding:MGBinding Path=Cursor.Visibility}" />
    <!-- slot 2: DR_AREA clip then the three bands. x 32..289, y bound, height bound -->
    <Canvas Name="TextClip" Width="258" Height="{dataBinding:MGBinding Path=Clip.Height}"
            CanvasLeft="32" CanvasTop="{dataBinding:MGBinding Path=Clip.Top}" ClipToBounds="True">
      <TextBlock Name="Row0" Text="{dataBinding:MGBinding Path=Row0.Text}"
                 CanvasLeft="{dataBinding:MGBinding Path=Row0.Left}" CanvasTop="{dataBinding:MGBinding Path=Row0.Top}"
                 FontFamily="font3" Foreground="White" AllowsInlineFormatting="False"
                 Padding="0" LinePadding="0" WrapText="False" VerticalContentAlignment="Top" Width="255" />
      <!-- Row1, Row2 identical -->
    </Canvas>
  </Canvas>
</Window>
```
View model shape the probe used (same style as `AlundraSaveScreenViewModel`, notify only on change): `RootVisibility`; `Frame.Top`;
`Clip.Top/Height`; `Row0..2.Text/Left/Top` with `Left = x - 32` and `Top = Y + 5 + 16 i - scroll - ClipTop`; `Cursor.SourceName/Top/Visibility`.
The row positions are relative to the clip canvas, negative values are fine (a row above the clip top, scroll 6: exact).
LayoutBounds read from the probe at x1/x2/x3, layout space (the scale is a render transform): Frame (16,168,288,56), Cursor
(288,200,16,16), TextClip (32,172,258,50), Row0 (32,173,255,16), Row1 (32,189,...), Row2 (32,205,...); RootCanvas takes the window's
bounds (640 x 480 at x2), not 320 x 240 (as the existing screens; their tests read `PreferredWidth/Height`).

### 1.3 Which properties exist and are bindable **[code]** (all also exercised by the probe)
| Need | Exists | Bindable | Evidence |
|---|---|---|---|
| `CanvasLeft`/`CanvasTop` (attribute form, ints) | yes | yes, `int?` typed, name = CLR name | `XAML/Element.cs:541-550`, `MGElement.cs:3275-3300` (comment: "{MGBinding} (ProcessBindings targets the CLR property name directly)"), `MGCanvas.cs:39-44`; the `Canvas.Left` attached-property element form is not used anywhere |
| `Width`/`Height` | yes | yes, mapped to `PreferredWidth/Height` | `XAML/Element.cs` `BindingPathMappings` (`Width`, `Height`) |
| `ClipToBounds` | yes, default **true on every element** | settable (`bool?`), not needed bound | `MGElement.cs:3537`, `XAML/Element.cs:286, :826-828`, `MGElement.cs:5228` |
| `Visibility`, `Text`, `SourceName` | yes | yes | existing screens |
| `Padding`, `LinePadding` (float), `WrapText`, `AllowsInlineFormatting`, `TextAlignment`, `VerticalContentAlignment`, `FontFamily` | yes on the XAML `TextBlock`/`Element` | attributes | `XAML/Controls.cs:3234-3360`, `Element.cs:198-203` |
| Image by sprite **id** | yes | yes (house convention, hard-coded `Guid.Parse("...") // wind_NNN`, `AlundraHudViewModel.cs:177-185`) | `CasaUIAssetProvider.cs:129-140` |
| Image by sprite **name** | yes (`wind_150`, `wind_173`, `wind_201`, `wind_228` are each the only asset of that name in `AssetInfos.json`) | yes | same line; but the catalogue name lookup is last-writer-wins (`AssetCatalog.cs:24-28`, `:38`) and `g_uiBoxesInventoryDescriptionBackground` names 3 assets (png, texture, sprite), so the frame must stay a GUID |

The four cursor ids (from `AssetInfos.json`, `UI/wind_NNN.sprite`, sheet `e49e074f-...`, 16 x 16 at u 176/192/208/224, v 56):
wind_150 `785b80e4-317b-5851-b7e8-80658fc4b05b`, wind_173 `a68a47ff-92a7-5479-9535-afef63c6fd13`, wind_201
`4d7dff2a-992e-5aa7-8650-4c6c28f1aeaf`, wind_228 `3d59f23b-1963-52b5-8e89-154977817e2a`. Frame sprite hotspot (144,28) is ignored by
`MGImage` (drawn at its CanvasLeft/Top, 0 mismatches).

### 1.4 MGUI/engine behaviour a screen author must know (defaults, with measured effect) **[code] + [probe]**
- `MGTextBlock` constructor defaults: `WrapText = theme default (true)` (`MGTextBlock.cs:1346`, `MGTheme.cs:721`), `LinePadding = 2` (`:1361`),
  `Padding = (1,1,1,1)` (`:1363`), `VerticalContentAlignment = Center` (`:1364`). Measured on one row, Padding alone matters
  (+1,+1); LinePadding and VCA have no visible effect on a single line whose block is its own height; wrap matters for long text.
- Every element draws inside its own layout bounds (`ClipToBounds` true, `MGElement.cs:5228-5274`): a TextBlock with `Width="255"` cuts
  its glyphs at 255 px by itself; without `Width` only the canvas clip (258) applies.
- The clip rectangle of a child under the root canvas scale is recomputed in render-target space (`MGElement.cs:5255`, `:5270`, `RoundUp`),
  and ADR-0054 shifts it by the viewport origin: integer scales and an offset view are exact [probe].
- A TextBlock builds its `Lines` in `OnLayoutUpdated` (`MGTextBlock.cs:1378`, `UpdateLines :1197`): before the first `Desktop.Update()` a
  fresh window draws nothing (S8) and `Lines` is empty. Headless tests must call `desktop.Update()` before reading `Lines`/bounds.
- Window defaults: draws a border unless `BorderThickness="0"` (S10 vs S10b/c).
- `UseLinearFilteringWhenDownscaling` (gap G8, known): irrelevant here, the images are only magnified by the root scale (`MGImage.cs:495-499`
  needs a destination smaller than the source, in unscaled layout space).

### 1.5 Gaps and frictions (reported, none worked around in the probe)
- **G1 view one frame behind the tick** (summary 5): the UI update runs before the world update, a bound value written after it is laid
  out at the next frame; `SourceName` is applied at once, so a frame can show a new cursor sprite on the old layout (63 texels in the
  measured A->B case). Same for every Alundra screen today. Engine-side choice (order of `UIView.Update` vs `UpdateWorld`) or accepted.
- **G3 XAML-only shell**: `BorderThickness="0"` must be declared; the three existing screens do it in code. Not a lack, a missing
  convention.
- **G4 name vs GUID** for the frame (above).
- **G5 test pin** (summary 6).
- **G6 no full-desktop draw test exists** anywhere: `rg "Desktop.Draw|desktop.Draw"` over `CasaEngine.Tests` and `MGUI.Tests` finds nothing;
  the only real-GPU tests are `UiClipViewSpaceGpuTests` (draw transaction primitives) and `PrimitiveBatchBlendStateGpuTests`.
- Not a gap: there is no MGUI notion of "pixel-exact bitmap text"; the four attributes of 1.2 are the way, per TextBlock (a `Style` could
  factor them, not probed).

## 2. Pushing a modal screen and following the window **[code]**
- Push/remove: `IUIViewRuntime.PushScreen/RemoveScreen` (`IUIViewRuntime.cs`) = `UIRoot.PushScreen/RemoveScreen` (`UIRoot.cs:159, :165`) =
  `ScreenStack.Push/Remove` (`ScreenStack.cs:58, :97`). `Push` calls `screen.Initialize(root)` once (`UIScreenBase.Initialize` is guarded),
  adds `GetWindows()` to `Desktop.Windows` and `Show()`; `Remove` calls `Hide()` and removes the windows; a re-push adds the window again
  (the save screen is reused that way). The presenter pattern: `AlundraSaveScreenPresenter.Tick` (`:45-64`) pushes on the tick the director
  becomes active, applies the view model, removes on the tick it stops; it is ticked in `AlundraWorldProxy.Update`'s per-tick loop
  (`:2072-2081`) and wired by a retry `TryWireSaveScreenOnce` (`:1399-1420`, the view model exists only after the UI view does). The current
  engine box is pushed the same way by `AlundraDialoguePresenter` (`:123-158`, Closed -> Open and `Close`).
- Modal: `public override UILayer Layer => UILayer.Modal; public override bool IsModal => true;` (`DialogueScreen.cs:130-131`;
  the four existing Alundra screens use `Menu` modal, `AlundraSaveScreen.cs:76-77`, the HUD `HUD` non-modal). `BlocksViewsBelow => IsModal`
  (`UIScreenBase.cs:20`). `ScreenStack.Update` calls `Update(GameTime)` only on the topmost blocking screen and above (`ScreenStack.cs:137-`);
  `HasModalInput` (`ScreenStack.cs:28`, `UIRoot.cs:58`) feeds `InputRouter.ResolveModalView` (`InputRouter.cs:360-376`) and
  `ScriptArcBallCamera.cs:172` (pointer/keyboard only; the pad path was not read).
- Follow the window: `UIRoot.Update` (`:122-137`) = `Desktop.Update()`, then `ScreenStack.NotifyScreenBounds(Desktop.ValidScreenBounds)` for every
  `XamlUIScreenBase` on the stack, frozen or not (`ScreenStack.cs:120`), which calls `OnScreenBoundsChanged` when the bounds differ from the
  last ones seen (`XamlUIScreenBase.cs:135-149`). `AlundraSaveScreen.OnWindowLoaded` (`:79-92`) and `OnScreenBoundsChanged` (`:97`) both call
  `ApplyScreenBounds` (`:107`): window = desktop bounds, `RootCanvas.RenderTransform.Scale = Math.Max(1, bounds.Width / 320)`. The new screen
  copies it. Test pattern: `AlundraScreensFollowTheWindowTests.cs` (`NewAssets` :60 with a `ScreenEnvelopeLoader` and `CpuFont3Loader`,
  `AssertFollowsTheDesktop` :88 = build at 640 x 480 -> scale 2, `runtime.Resize(960, 720)` + `NotifyScreenBounds` -> scale 3, same bounds again
  = nothing moves, back to x2; one `[Fact]` per screen :116-148) on `HeadlessUiTestHarness.NewDesktop` (`:29`, `Resize` :80).
  A pixel test of the same flow (x1/x2/x3, view offset) passed in the probe by calling the same `window.Window*`/`Scale` assignments.

## 3. GPU harness options **[code] + [probe]**
- `Alundra.Tests` today: only `AlundraFont3GlyphTests` has GPU code, all **private nested** types: `HiddenGame : Game` (`:130`, 128 x 128 back
  buffer), `GpuThread` (`:147`, background thread + `BlockingCollection`, lazy `EnsureDevice` :163, `Invoke` re-entrant),
  `RealFontRuntime : IUIDesktopRuntime` (`:230`) whose `CreateDrawTransaction` throws `NotSupportedException "...never draws."` (`:265`).
  `HeadlessUiTestHarness.cs:76` throws the same. So: a real device, real FontStashSharp font3 (`NewFont3Desktop` :333: `Texture2D.FromStream`
  + `StaticSpriteFont.FromBMFont` + `AddStaticFont`), **no render target, no readback, no draw, no skip**: with no GPU `EnsureDevice` throws and
  the test fails.
- The engine's harness (`CasaEngine.Tests/UI/Backend/GpuDeviceHost.cs`, `UiClipViewSpaceGpuTests.cs`) is `internal sealed` (`HeadlessGame :15`,
  `GpuDeviceHost :44`) in an assembly `Alundra.Tests` does not reference; `CasaEngine` exposes its internals only to `CasaEngine.Tests`,
  `CasaEngine.EditorServices`, `CasaEngine.AposShapes` (`InternalsVisibleTo.*.cs`). Its pattern is the one to copy: lazy one-time probe of a
  device (`IsAvailable`/`UnavailableReason`), `[GpuFact] : FactAttribute` that sets `Skip` in its constructor (`:143-150`), a collection with
  `DisableParallelization` (`:156`), render target + `SetRenderTarget`/`Viewport`/`Clear` + `GetData` (`UiClipViewSpaceGpuTests.cs:61-90`).
  xunit is 2.9.3 on both sides (`Directory.Packages.props:27`, `Alundra.Tests.csproj`), so the `Skip`-in-constructor attribute works unchanged;
  `xunit.runner.json` already serialises collections.
- What a test needs, all **public API** (the probe compiled against the test bin without InternalsVisibleTo): a `Game` + `GraphicsDeviceManager`
  + `RunOneFrame()` on one dedicated thread; a host implementing `MGUI.Shared.Rendering.IRenderHost` + `IRawInputSource`
  (`GraphicsDevice`, `GetBounds`, `GetService` -> `game.Services`, `PreviewUpdate`/`EndUpdate` events, mouse/keyboard state);
  `CasaMonoGameBackendBootstrap.Create(host, host, surface: null, assetProvider: ...)` -> `.Runtime` (`CasaDesktopRuntime`; its constructor loads
  `Content/Icons/ScrollMarker` and `Content/Shaders/TexturedPrimitive` through `new ContentManager(host, "Content")`,
  `CasaRuntimeBackendServices.cs:31`, `CasaDesktopRuntime.cs:97-98`; the engine `Content/` folder is already copied next to the test assembly);
  `new MGDesktop(runtime)`; a `FontStashSharpTextEngine` with the default family registered from `Content/Fonts/tahoma.ttf` (`AddFontSystem` +
  `MatchSpriteFontSizing(runtime.FontManager)`, as `UIRoot.cs:93-112`; `MGTextBlock`'s constructor throws "Default font not found" otherwise)
  and `AddStaticFont("font3", ...)`; `desktop.TextEngine = engine`; a stand-in `IUIAssetProvider` (about 40 lines: id/name -> sprite JSON ->
  `.texture` JSON -> png -> `CasaMonoGameImageResource(texture)` + source rect; the production `CasaUIAssetProvider` needs a populated
  `AssetCatalog` + an `AssetContentManager` with sprite loaders, so its resolution stays covered by `CasaEngine.Tests`' own tests, not by this
  pixel test); the screen window loaded with `UIScreenLoader.Load(desktop, XamlDocumentSource...)` (or through `XamlUIScreenBase.BuildWindow`);
  per frame `host` raises `PreviewUpdate`/`EndUpdate`, `desktop.Update()`, then `desktop.Draw()` between `device.SetRenderTarget(rt)` and
  `device.SetRenderTarget(null)`; `rt.GetData`. Probe skeleton: `probe/Program.cs` (`HiddenGame`, `TestHost`, `ProbeAssets`, `BuildEngine`, `RenderN`).
- Pixel exactness: the reference needs only `font3.png`, the frame png, the cursor sheet and the `.fnt`/annex rectangles; `Texture2D.FromStream`
  is also what `Texture2DLoader.cs:12` and so `BitmapFontLoader.cs:29-34` use in production; every texel of the exported PNGs has alpha 0 or 255.
- Cost: 3.3 s for 120 comparisons including device creation, on an AMD Radeon RX 9060 XT (DesktopGL, profile Reach). **[hyp]** other GPUs/drivers
  not tried; PointClamp sprites at integer coordinates and integer scales should be exact everywhere.
- **Prerequisite (G5)**: the `MonoGame.Framework.DesktopGL` pin of `Alundra.Tests` (3.8.4.1) must become 3.8.5.1 for a `CasaDesktopRuntime` to start
  (measured refusal of the engine's effect). The vstest host runs the existing tests despite the mismatch (2647 pass in
  `merge-1005/suites.log`, Release); **[hyp]** because it resolves a missing assembly version by file name; plain `dotnet exec` does not (my probe
  needed an `AssemblyLoadContext.Resolving` handler to run against 3.8.4.1, and then failed on the MGFX header). Whether anything else differs between
  3.8.4.1 and 3.8.5.1 for the existing GPU tests was not examined. **[hyp]** changing the pin is safe for them (`Alundra.csproj:10` already
  uses 3.8.5.1).
- Skipping without a GPU: the engine's technique above; `AlundraFont3GlyphTests` would keep failing without a GPU unless it also moved to it
  (not in scope).

## 4. Glyph placement chain (question 4) **[code]/[decomp]/[probe]**
`MGTextBlock.DrawSelf` (`MGTextBlock.cs:1556-1690`): `CurrentY = GetRenderedTextStartY(LayoutBounds, Lines)` (`:1563`; padded top, then
`VerticalContentAlignment` inside the padded height, which equals the text height for a canvas child), `LineBounds = (padded.Left, CurrentY,
padded.Width, (int)LineTotalHeight)`, `CurrentX = ApplyAlignment(LineBounds, TextAlignment, ...).Left` (`:1568`), `TextYPosition = ...Y` (`:1569`),
`engineAdjustedDrawPosition = visual + resolved.DrawOrigin * drawScale` (`:1605`) with, for a static font, `DrawOrigin = Vector2.Zero`, scales 1,
`LineHeight = font.LineHeight` (`FontStashSharpTextEngine.cs:538-554`) -> `DT.DrawTextViaEngine(resolved, text, Position, color, origin, scale)`
(`MGTextBlock.cs:1644`, `CasaDrawTransaction.cs:222-240`) -> `FontStashSharpTextEngine.DrawText` (`:737-790`, `h.Font.DrawText(spriteBatch, ...)` :780)
-> FontStashSharp `SpriteFontBase.InternalDrawText`: per glyph `v = (penX, penY) + glyph.RenderOffset`, `v.Transform(BuildTransform(position, 0,
origin, scale))` = `position - origin*scale + v*scale`, `SpriteBatch.Draw(glyph.Texture, v, glyph.TextureRectangle, color)`; `StaticSpriteFont.FromBMFont`
sets `RenderOffset = (xoffset, yoffset)`, `TextureOffset = (x, y)`, `Size = (width, height)`, `XAdvance`; `StaticSpriteFont.PreDraw` = ascent 0,
lineHeight = font line height; `UseKernings` true, no `kerning` line in `font3.fnt`; `DefaultCharacter` null; `IsEmpty` (zero width or height)
glyphs are skipped but still advance. Sampler default `PointClamp` (`DrawSettings.cs:82`), blend `AlphaBlend`.
**Only a pixel test proves**: that this holds through SpriteBatch with a root `Matrix` scale (it does at x2/x3, 2x2 / 3x3 blocks), with the
scissor of a clipped child, with the actual GPU; the probe proves it for the cases listed in summary 4 and 1. **Not proven**: non-zero offsets,
kerning, missing characters, other GPUs.

## 5. Corrections to the earlier notes (f2b-disc/notes.md, f2b-disc-verify/verify.md)
- "no test of a clipped child under an ancestor scale for this shape": now measured (S3/S4/S9/S11, 0 texels).
- "[hyp] today's font3 texts are 1 px right and down": confirmed (summary 3), also at x2 (+1,+1 native).
- "GPU harness available: GpuDeviceHost (public)": it is internal (verify.md C-GPU already says so); and the full-desktop draw is new.
- "modal freezing of the HUD": see summary 7.
- Not in the earlier notes: G1, G3, G5, the BorderThickness finding, and that wrap must be turned off for rows wider than their block.

## 6. The probe (throwaway, off-repo)
`probe/Program.cs` (+ `Probe.csproj`, built into `bin/Probe.dll`, run with `dotnet Probe.dll` from `bin/`), logs in `out/probe_final_3851.log`
(121 lines), PNGs in `out/` (`s1_*` rest x1/x2/x3, `s3_*` slide-out and long rows, `s4_*` scroll 6, `s7_*` markers, `s9_*` offset view, `s6_*`
today's SaveScreen render and its reference). `bin/` is a copy of the DLLs and `Content/` of `Alundra.Tests/bin/Debug/net9.0-windows`
(copied 2026-10-05 ~23:09) with `MonoGame.Framework.dll` replaced by the 3.8.5.1 file of the NuGet cache
(`MonoGame.Framework.3.8.4.1.dll.keep` keeps the original for the refusal experiment). `refs/` prints assembly references (shows
CasaEngine/MGUI/Alundra.dll reference MonoGame.Framework 3.8.5.1 while the test bin holds 3.8.4.1). Two `font3.fnt` variants run side by side:
`today` (exported file) and `annex` (same file with every glyph rectangle from `docs/plan-e19-f2b0-annexe/glyph_table.txt`, xoffset 0,
xadvance = w, as f2b0 will write it, `out/font3_annex.fnt`).

Scenarios (reference = independent C# compositor in the probe: frame/cursor/glyph rectangles blitted with alpha test, clip rectangle and
255-px band applied, then nearest-neighbour scaled by k):
| id | what | result |
|---|---|---|
| S1 | rest state, Y 168, clip (172,50), cursor wind_150, rows "Bonjour, Alundra !" / "Deuxieme ligne: 1-2 'y' z" / "Troisieme", x1 x2 x3 | 0 / 0 / 0 |
| S2 | one row, no frame, six attribute sets | today's attributes: text at (+1,+1); `Padding=0` and every set containing it: (0,0), 0 texels |
| S3 | slide-out lag (Y 182, clip 181..230, third row cut) x1 x2 x3; 40-W row with `Width=255`; without `Width` | 0 x3; 0; without Width: matches the unbanded reference (0) and differs from the 255-band one by 12 texels |
| S4 | scroll 6 (top row partly above the clip), wind_201; four cursors by `SourceName` switch; x1 x2 x3 | all 0 |
| S5 | closed box (Y 240, clip (239,1)) | 0 (bare background) |
| S6 | real `SaveScreen.xaml` + real `AlundraSaveScreenViewModel`, x1 and x2 | text drawn (+1,+1) from the rule, 0 texels at that shift |
| S7 | markers U+0010 U+001C U+0012 U+0013 U+001D, `[b]{c}\H~`, leading/trailing/multiple spaces, rows at Left 0/16/65 | 0 |
| S8 | update order: fresh load Draw with no Update; one Update; A->B change then Draw with/without Update | no Update: bare background / old layout with the new cursor sprite (63 texels); one Update: 0 |
| S9 | view at (97,41) in a larger target, x2 and x3, clip lag + scroll | view 0 texels; 0 texels changed outside the view |
| S10 | pure XAML, no code on the window | without `BorderThickness="0"`: 13698 texels differ (first (0,0) black); with it: 0 at x1 and x2 |
| S11 | dynamic: clip Top/Height, cursor shown/hidden/switched, row emptied, Y 240 and back, one Update each, x1 x2 | all 0 |
| S12 | wrap: theme default vs `WrapText="False"` on the 40-W row | default: 2 lines, height 34, 1191 texels differ; False: 1 line, 0 |

Caveats **[hyp]**: stand-in asset provider (the production `CasaUIAssetProvider`/`UIRoot`/`ViewRenderHost`/`CasaRenderSurfaceAdapter` path is
not exercised: the recipe stays the proof of the whole chain); desktop built with `new MGDesktop(runtime)` only, as `UIRoot` does (the headless
harness also calls `LoadDefaultResources`; not needed here); one machine/GPU.

## 7. Open questions
No genuine product decision is needed from this surface. Two plan decisions to record (technical): (a) align the `Alundra.Tests` MonoGame pin to
3.8.5.1 as part of f2b1 T5 (recommended; needs a full `Alundra.Tests` run after, Release then Debug); (b) accept the one-frame view lag (G1) as
every other Alundra screen has it today (recommended), or ask for an engine-side change of the update order (out of f2b1).
