#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Sprites;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.EffectQuads;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Cameras;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Maps;

namespace Alundra.Tests;

/// <summary>
/// E19.g G3c (docs/plan-e19-opcodes.md, G3C-R1 to G3C-R5, ADR-0049): the DLL pushes the effect quads of the reservoir to the engine's
/// <see cref="EffectQuadService"/> every rendered frame. The pure construction (<see cref="EffectQuadBuilder"/>), the stage (<see cref="EffectStage.AttachService"/>,
/// <see cref="EffectStage.PushFrame"/>) and the proxy wiring (<see cref="WorldProxy.Update"/>), against the values of the prediction
/// <c>docs/plan-e19-g3c-annexe/g3c_model.py</c> (the value at the pixel is proved by the out-of-repo harness, G3c-3, not here). Written red first on stub bodies.
/// Shares <see cref="AlundraMusicPlayerSingletonCollection"/>: the warp departure arms the real BGM fade director.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraEffectQuadPushTests : IDisposable
{
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();

    private static readonly int[] Plain = { -10, -20, 10, -20, -10, 20, 10, 20 };

    public AlundraEffectQuadPushTests()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    public void Dispose()
    {
        BackdropWarpDepartureSupport.ResetSingletons();
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------------------------

    private static EffectImage Img(int u, int v, int w, int h, int[]? c = null, bool semi = false, int abr = 0) =>
        new() { U = u, V = v, W = w, H = h, C = c ?? Plain, Semi = semi, Abr = abr };

    private static EffectImageSet SetOf(int idsv, params EffectImage[] images) => new() { Idsv = idsv, Images = images.ToList() };

    private static EffectSlot SlotAt(int index, int x, int y, int z, EffectImageSet? set, bool mapTable = true, uint depthKey = 0) =>
        new(index) { Active = true, X = x, Y = y, Z = z, CurrentImageSet = set, CurrentMapTable = mapTable, DepthKey = depthKey };

    private static List<EffectQuad> Build(bool hasScroll, int scrollX, int scrollY, params EffectSlot[] slots)
    {
        var quads = new List<EffectQuad>();
        EffectQuadBuilder.Build(slots, hasScroll, scrollX, scrollY, quads);
        return quads;
    }

    private static EffectQuad[] Frame(EffectQuadService service) => Enumerable.Range(0, service.QuadCount).Select(service.GetQuad).ToArray();

    private static void EmptyTheService(EffectQuadService service) => service.SetFrame(ReadOnlySpan<EffectQuad>.Empty);

    /// <summary>A bank whose map table has one Loop animation of one image set of two images (ids of the sheets as given).</summary>
    private static EffectBank QuadBank(string? mapSheet = null, string? globalSheet = null)
    {
        var table = new EffectTable
        {
            Animations = { EffectGolden.Anim("Loop", new[] { 1, 0 }) },
            ImageSets = { SetOf(3, Img(0, 0, 20, 40), Img(30, 0, 20, 40, semi: true, abr: 1)) },
        };
        return new EffectBank(
            new EffectDocument { SheetTextureAssetId = mapSheet, Effects = { table } },
            new EffectDocument { SheetTextureAssetId = globalSheet });
    }

    private static string FindDoc(params string[] relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relative).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no '{Path.Combine(relative)}' found above '{AppContext.BaseDirectory}'.");
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // G3C-R1 anchor, G3C-R2 elimination
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Anchor_TheOneOfZ_MovesTheWorldRowUp_111Against112()
    {
        var set = SetOf(0, Img(0, 0, 20, 40));
        var withOne = Build(false, 0, 0, SlotAt(0, 960 << 16, 160 << 16, (48 << 16) + 1, set));
        var without = Build(false, 0, 0, SlotAt(0, 960 << 16, 160 << 16, 48 << 16, set));

        // (y - z) >> 16: 7340031 -> 111, 7340032 -> 112; the corner (X1, Y1) = (-10, -20) is added to the anchor, the world y is flipped.
        Assert.Equal(new Vector2(950, -(111 - 20)), Assert.Single(withOne).TopLeft);
        Assert.Equal(new Vector2(950, -(112 - 20)), Assert.Single(without).TopLeft);
        Assert.Equal(new Vector2(970, -(111 - 20)), withOne[0].TopRight);
        Assert.Equal(new Vector2(950, -(111 + 20)), withOne[0].BottomLeft);
        Assert.Equal(new Vector2(970, -(111 + 20)), withOne[0].BottomRight);
    }

    [Fact]
    public void Anchor_UsesTheArithmeticShiftOfNegativeValues()
    {
        // x = -1 << 16 + 0x8000 -> -1; y - z = (-2 << 16) - 1 -> -3.
        var quads = Build(false, 0, 0, SlotAt(0, (-1 << 16) + 0x8000, -2 << 16, 1, SetOf(0, Img(0, 0, 20, 40))));
        Assert.Equal(new Vector2(-1 - 10, -(-3 - 20)), Assert.Single(quads).TopLeft);
    }

    [Theory]
    [InlineData(0, 0, -129, 100, false)]
    [InlineData(0, 0, -128, 100, true)]
    [InlineData(0, 0, 448, 100, true)]
    [InlineData(0, 0, 449, 100, false)]
    [InlineData(0, 0, 100, -129, false)]
    [InlineData(0, 0, 100, -128, true)]
    [InlineData(0, 0, 100, 368, true)]
    [InlineData(0, 0, 100, 369, false)]
    [InlineData(800, -8, -129, 100, false)]
    [InlineData(800, -8, -128, 100, true)]
    [InlineData(800, -8, 448, 368, true)]
    [InlineData(800, -8, 449, 368, false)]
    [InlineData(800, -8, 448, 369, false)]
    public void Elimination_ThePlanBoundsAreInclusive_OnTheScreenAnchor(int scrollX, int scrollY, int screenX, int screenY, bool kept)
    {
        var set = SetOf(0, Img(0, 0, 20, 40), Img(0, 0, 20, 40));
        var slot = SlotAt(0, (screenX + scrollX) << 16, (screenY + scrollY) << 16, 0, set);

        var quads = Build(true, scrollX, scrollY, slot);

        Assert.Equal(kept ? 2 : 0, quads.Count); // the whole slot, all its images, or nothing
    }

    [Fact]
    public void Elimination_WithoutACamera_NothingIsEliminated()
    {
        Assert.Single(Build(false, 12345, -6789, SlotAt(0, 30000 << 16, 30000 << 16, 0, SetOf(0, Img(0, 0, 20, 40)))));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // G3C-R3 quads
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 40, new[] { 10, -20, -10, -20, 10, 20, -10, 20 }, -1, 40)]     // mirror X at U = 0: the window starts at -1
    [InlineData(7, 40, new[] { 10, -20, -10, -20, 10, 20, -10, 20 }, 6, 40)]      // mirror X: one texel back
    [InlineData(5, 0, new[] { -10, 20, 10, 20, -10, -20, 10, -20 }, 5, -1)]       // mirror Y at V = 0
    [InlineData(5, 9, new[] { -10, 20, 10, 20, -10, -20, 10, -20 }, 5, 8)]
    [InlineData(0, 0, new[] { 10, 20, -10, 20, 10, -20, -10, -20 }, -1, -1)]      // both
    [InlineData(0, 0, new[] { -10, -20, 10, -20, -10, 20, 10, 20 }, 0, 0)]        // none
    public void Window_AMirroredAxisStepsBackOneTexel_AndNeverOnTheOther(int u, int v, int[] c, int expectedX, int expectedY)
    {
        var quad = Assert.Single(Build(false, 0, 0, SlotAt(0, 0, 0, 0, SetOf(0, Img(u, v, 24, 30, c)))));

        Assert.Equal(new Rectangle(expectedX, expectedY, 24, 30), quad.Window);
    }

    [Theory]
    [InlineData(false, 0, SpritePsxSemiTransparency.None)]
    [InlineData(false, 2, SpritePsxSemiTransparency.None)]
    [InlineData(true, 0, SpritePsxSemiTransparency.Mode0)]
    [InlineData(true, 1, SpritePsxSemiTransparency.Mode1)]
    [InlineData(true, 2, SpritePsxSemiTransparency.Mode2)]
    [InlineData(true, 3, SpritePsxSemiTransparency.Mode3)]
    public void Mode_IsAbrPlusOneWhenSemi_NoneOtherwise(bool semi, int abr, SpritePsxSemiTransparency expected)
    {
        var quad = Assert.Single(Build(false, 0, 0, SlotAt(0, 0, 0, 0, SetOf(0, Img(0, 0, 8, 8, semi: semi, abr: abr)))));

        Assert.Equal(expected, quad.Mode);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Sheet_IsTheMapCompanionForTheMapTable_TheGlobalOneOtherwise(bool mapTable, int expectedSheet)
    {
        var quad = Assert.Single(Build(false, 0, 0, SlotAt(0, 0, 0, 0, SetOf(0, Img(0, 0, 8, 8)), mapTable)));

        Assert.Equal(expectedSheet, quad.SheetIndex);
    }

    [Fact]
    public void Corners_FollowTheDataOrder_TopLeftTopRightBottomLeftBottomRight()
    {
        var c = new[] { -3, -4, 5, -6, -7, 8, 9, 10 };

        var quad = Assert.Single(Build(false, 0, 0, SlotAt(0, 100 << 16, 50 << 16, 0, SetOf(0, Img(0, 0, 8, 8, c)))));

        Assert.Equal(new Vector2(97, -46), quad.TopLeft);
        Assert.Equal(new Vector2(105, -44), quad.TopRight);
        Assert.Equal(new Vector2(93, -58), quad.BottomLeft);
        Assert.Equal(new Vector2(109, -60), quad.BottomRight);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // G3C-R4 key
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Key_OfTheRealAura_Elevation214_Coordinate11100_LocalOffsetCountsDown_StableIdIsTheSlotIndex()
    {
        var bank = EffectGolden.BankOf(Root, 476);
        var stage = new EffectStage();
        stage.Load(bank);
        var aura = stage.Reservoir.SpawnPlaced(0, 960, 160, 48)!;
        var second = stage.Reservoir.Create(true, 0, 1, 970 << 16, 160 << 16, (48 << 16) + 1)!;
        var third = stage.Reservoir.Create(true, 0, 1, 980 << 16, 160 << 16, (48 << 16) + 1)!;
        stage.RunFrame(1, new[] { false });

        // The first image set of animation 1 of effect 0 of map 476 (the export); its IDSV is read from the export, not assumed.
        var table = bank.Map!.Effects[0];
        var set = table.ImageSets[table.Animations[1].Frames[0][1]];
        Assert.Same(set, aura.CurrentImageSet);
        Assert.Equal(9, set.Images.Count);
        var expectedElevation = 16 * Math.Min(((160 << 16) + 48 + (set.Idsv << 16)) >> 20, 59) + 6;
        Assert.Equal(214, expectedElevation);

        var quads = Build(true, 800, -8, aura, second, third);

        Assert.Equal(27, quads.Count);
        for (var slotIndex = 0; slotIndex < 3; slotIndex++)
        {
            for (var k = 0; k < 9; k++)
            {
                var key = quads[slotIndex * 9 + k].SortKey;
                var where = $"slot {slotIndex} image {k}";
                Assert.True((int)RenderPass2D.YSortedWorld == key.RenderPass, where);
                Assert.Equal(300, key.RenderPass);
                Assert.Equal(300, key.SortingLayer);
                Assert.Equal(0, key.OrderInLayer);
                Assert.Equal(214, key.Elevation);
                Assert.Equal(100 * 111, key.SortCoordinate);
                Assert.Equal(11100, key.SortCoordinate);
                Assert.Equal(8 - k, key.LocalSortOffset); // image 0 is drawn last
                Assert.Equal(slotIndex, key.StableId);
            }
        }

        // The corners of image 0 at the aura anchor (world column 960, row 111).
        var c = set.Images[0].C;
        Assert.Equal(new Vector2(960 + c[0], -(111 + c[1])), quads[0].TopLeft);
    }

    [Fact]
    public void Key_ElevationIsTheEntityRowOfTheStoredKey_ClampedAt59()
    {
        var set = SetOf(0, Img(0, 0, 8, 8));
        var low = Assert.Single(Build(false, 0, 0, SlotAt(0, 0, 0, 0, set, depthKey: (3u << 20) + 5)));
        var high = Assert.Single(Build(false, 0, 0, SlotAt(0, 0, 0, 0, set, depthKey: 100u << 20)));

        Assert.Equal(16 * 3 + 6, low.SortKey.Elevation);
        Assert.Equal(16 * 59 + 6, high.SortKey.Elevation);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Frames: empty, null image set, null service
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Build_AnActiveSlotWithoutImageSet_IsSkipped_AndAnInactiveOneToo()
    {
        var inactive = SlotAt(1, 0, 0, 0, SetOf(0, Img(0, 0, 8, 8)));
        inactive.Active = false;

        var quads = Build(false, 0, 0, SlotAt(0, 0, 0, 0, null), inactive, SlotAt(2, 0, 0, 0, SetOf(0, Img(0, 0, 8, 8))));

        var only = Assert.Single(quads);
        Assert.Equal(2, only.SortKey.StableId);
    }

    [Fact]
    public void PushFrame_PushesAnEmptyFrame_WhenNothingIsActive_AndWhenTheLastSlotIsFreed()
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();
        stage.Load(QuadBank());
        stage.AttachService(service);
        var slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;
        stage.RunFrame(1, new[] { false });

        stage.PushFrameAt(false, 0, 0);
        Assert.Equal(2, service.QuadCount);

        stage.Reservoir.Free(slot);
        stage.PushFrameAt(false, 0, 0);
        Assert.Equal(0, service.QuadCount);
    }

    [Fact]
    public void PushFrame_ASlotBeforeItsFirstUpdate_HasNoImageSet_AndPushesNothing()
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();
        stage.Load(QuadBank());
        stage.AttachService(service);
        stage.Reservoir.Create(true, 0, 0, 0, 0, 0);

        stage.PushFrameAt(false, 0, 0);

        Assert.Equal(0, service.QuadCount);
    }

    [Fact]
    public void PushFrame_WithTheCameraTarget_CullsOnTheOriginalScrollSpace()
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();
        stage.Load(QuadBank());
        stage.AttachService(service);
        // anchor world (960, 111) is the screen anchor (160, 119) at the scroll (800, -8): kept; at the scroll (800, 400) its screen row is -289: culled.
        stage.Reservoir.Create(true, 0, 0, 960 << 16, 160 << 16, (48 << 16) + 1);
        stage.Reservoir.Create(true, 0, 0, 960 << 16, 160 << 16, (48 << 16) + 1);
        stage.RunFrame(1, new[] { false });

        stage.PushFrame(new Camera2dComponent { Target = new Vector3(960, -112, 0) });
        var kept = service.QuadCount;
        stage.PushFrame(new Camera2dComponent { Target = new Vector3(960, -520, 0) });
        var culled = service.QuadCount;
        stage.PushFrame(null);
        var uncut = service.QuadCount;

        Assert.Equal(4, kept);
        Assert.Equal(0, culled);
        Assert.Equal(4, uncut);
    }

    [Fact]
    public void PushFrame_WithoutAService_DoesNothing_AndDoesNotThrow()
    {
        var stage = new EffectStage();
        stage.Load(QuadBank("11111111-1111-1111-1111-111111111111"));
        stage.Reservoir.Create(true, 0, 0, 0, 0, 0);
        stage.RunFrame(1, new[] { false });

        var exception = Record.Exception(() =>
        {
            stage.PushFrame(null);
            stage.PushFrameAt(true, 0, 0);
            stage.AttachService(null);
            stage.Load(QuadBank());
            stage.PushFrame(new Camera2dComponent());
        });

        Assert.Null(exception);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // G3C-R5 sheets: AttachService and Load in both orders
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Sheets_AttachThenLoad_GivesBothIds_MapFirst()
    {
        var map = Guid.NewGuid();
        var global = Guid.NewGuid();
        var service = new EffectQuadService();
        var stage = new EffectStage();

        stage.AttachService(service);
        stage.Load(QuadBank(map.ToString(), global.ToString("D")));

        Assert.Equal(2, service.SheetCount);
        Assert.Equal(map, service.GetSheetId(0));
        Assert.Equal(global, service.GetSheetId(1));
    }

    [Fact]
    public void Sheets_LoadThenAttach_GivesBothIds_AtOnce()
    {
        var map = Guid.NewGuid();
        var global = Guid.NewGuid();
        var service = new EffectQuadService();
        var stage = new EffectStage();

        stage.Load(QuadBank(map.ToString(), global.ToString("D")));
        Assert.Equal(0, service.SheetCount);
        stage.AttachService(service);

        Assert.Equal(2, service.SheetCount);
        Assert.Equal(map, service.GetSheetId(0));
        Assert.Equal(global, service.GetSheetId(1));
    }

    [Fact]
    public void Sheets_AttachBeforeAnyLoad_PushesNothingYet()
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();

        stage.AttachService(service);

        Assert.Equal(0, service.SheetCount);
        Assert.Equal(0, service.SheetsVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Sheets_ANullOrUnreadableId_IsGuidEmpty_AndAMissingDocumentToo(string? text)
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();
        stage.AttachService(service);

        stage.Load(QuadBank(text, text));
        Assert.Equal(new[] { Guid.Empty, Guid.Empty }, new[] { service.GetSheetId(0), service.GetSheetId(1) });

        stage.Load(new EffectBank(null, null));
        Assert.Equal(2, service.SheetCount);
        Assert.Equal(new[] { Guid.Empty, Guid.Empty }, new[] { service.GetSheetId(0), service.GetSheetId(1) });
    }

    [Fact]
    public void Load_ClearsThePushedFrame_ThenSetsTheSheets()
    {
        var service = new EffectQuadService();
        var stage = new EffectStage();
        stage.AttachService(service);
        stage.Load(QuadBank());
        stage.Reservoir.Create(true, 0, 0, 0, 0, 0);
        stage.RunFrame(1, new[] { false });
        stage.PushFrameAt(false, 0, 0);
        Assert.Equal(2, service.QuadCount);
        var version = service.SheetsVersion;

        stage.Load(QuadBank());

        Assert.Equal(0, service.QuadCount);
        Assert.Equal(version + 2, service.SheetsVersion); // Clear, then SetSheets
        Assert.Equal(2, service.SheetCount);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The proxy: every rendered frame, frozen or not, during a warp departure; the real map 391
    // ---------------------------------------------------------------------------------------------------------------------

    private static (WorldProxy Proxy, EffectQuadService Service, EffectSlot Slot) HeadlessProxyWithAQuad()
    {
        var world = new World { Name = "TestWorld" };
        world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        var service = new EffectQuadService();
        proxy._effectStage.Load(QuadBank());
        proxy._effectStage.AttachService(service);
        var slot = proxy._effectStage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;
        return (proxy, service, slot);
    }

    [Fact]
    public void Update_PushesTheFrame_EveryRenderedFrame_EvenWhen0x48FreezesTheUpdate()
    {
        var (proxy, service, slot) = HeadlessProxyWithAQuad();

        proxy.Update(0.02f); // tick 0: the slot shows its image set
        Assert.Equal(0, slot.CurrentImageSetIndex);
        Assert.Equal(2, service.QuadCount);

        EmptyTheService(service);
        proxy.GameState.PlayerControlFlags = GameState.PlayerControlBits.MenuOpen | GameState.PlayerControlBits.Unused40;
        proxy.Update(0.02f);
        proxy.GameState.PlayerControlFlags = 0;

        Assert.Equal(2, service.QuadCount); // pushed again although nothing was updated or freed
        Assert.True(slot.Active);
    }

    [Fact]
    public void Update_PushesTheFrame_WithZeroTicks_AndWhenAFrameHasNoTick()
    {
        var (proxy, service, _) = HeadlessProxyWithAQuad();
        proxy.Update(0.02f);

        EmptyTheService(service);
        proxy.Update(0.0f); // a frame without tick

        Assert.Equal(2, service.QuadCount);
    }

    [Fact]
    public void Update_PushesTheFrame_DuringAWarpDeparture_Which_GivesTheEffectsNoTick()
    {
        var (world, _) = AlundraWorldProxyGlobalFreezeTests.BuildRealMap389World();
        var proxy = new WorldProxy();
        proxy.InitializeWithWorld(world);
        BackdropWarpDepartureSupport.SetPrivateField(proxy._backdropStage, "_clearColorApplied", true); // the headless montage has no view for the clear colour.
        var service = new EffectQuadService();
        var stage = proxy._effectStage;
        stage.Load(QuadBank());
        stage.AttachService(service);
        var slot = stage.Reservoir.Create(true, 0, 0, 0, 0, 0)!;

        proxy.Update(0.02f); // priming frame: tick 0
        Assert.Equal(0, slot.CurrentImageSetIndex);
        Assert.Equal(2, service.QuadCount);

        var player = BackdropWarpDepartureSupport.BeginDeparture(proxy);
        for (var frame = 0; frame < 4; frame++)
        {
            EmptyTheService(service);
            proxy.Update(0.02f);
            Assert.True(WarpDirector.Instance.IsTransitionInProgress, $"F{frame}");
            Assert.Equal(2, service.QuadCount);
        }

        GC.KeepAlive(player);
    }

    [Fact]
    public void Update_OnTheRealMap391_PushesTheSheetsAndTheFrame_QuadForQuadTheSameAsThePureConstruction()
    {
        var worldName = EffectGolden.WorldNameOf(Root, 391);
        Assert.Equal("Ship Klark (night, break, Event)-391", worldName);

        var world = new World { Name = worldName! };
        var proxy = new WorldProxy();
        var previousProjectPath = EngineEnvironment.ProjectPath;
        EngineEnvironment.ProjectPath = Root;
        try
        {
            proxy.InitializeWithWorld(world);
        }
        finally
        {
            EngineEnvironment.ProjectPath = previousProjectPath;
        }

        var service = new EffectQuadService();
        proxy._effectStage.AttachService(service); // after InitializeWithWorld: the bank is already loaded, the sheets go at once

        proxy.Update(0.02f); // the first Update spawns the load-time records and runs tick 0

        var bank = EffectGolden.BankOf(Root, 391);
        Assert.Equal(2, service.SheetCount);
        Assert.Equal(Guid.Parse(bank.Map!.SheetTextureAssetId!), service.GetSheetId(0));
        Assert.Equal(Guid.Parse(bank.Global!.SheetTextureAssetId!), service.GetSheetId(1));
        Assert.NotEqual(Guid.Empty, service.GetSheetId(0));
        Assert.NotEqual(Guid.Empty, service.GetSheetId(1));
        Assert.NotEqual(service.GetSheetId(0), service.GetSheetId(1));

        var camera = proxy._cameraDirector.ResolvedCamera;
        var scroll = CameraMath.ToOriginalScrollSpace(camera?.Target ?? Vector3.Zero);
        var expected = new List<EffectQuad>();
        EffectQuadBuilder.Build(proxy._effectStage.Reservoir.Slots, camera != null, scroll.X, scroll.Y, expected);

        Assert.True(expected.Count >= 1);
        Assert.Equal(expected.Count, service.QuadCount);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i], service.GetQuad(i));
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The harness's camera and the export guard
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ToOriginalScrollSpace_IsPinnedOnTheIntegerTargetsOfTheHarness()
    {
        foreach (var (scrollX, scrollY) in new[] { (800, -8), (220, 40), (836, 640), (0, 0) })
        {
            var target = new Vector3(scrollX + 160, -(scrollY + 120), 0);

            Assert.Equal((scrollX, scrollY), CameraMath.ToOriginalScrollSpace(target));
        }

        // every scene of the prediction (docs/plan-e19-g3c-annexe/g3c_expected.json) carries exactly this target
        using var document = JsonDocument.Parse(File.ReadAllText(FindDoc("docs", "plan-e19-g3c-annexe", "g3c_expected.json")));
        var scenes = document.RootElement.GetProperty("scenes").EnumerateArray().ToList();
        Assert.Equal(8, scenes.Count);
        foreach (var scene in scenes)
        {
            var scroll = scene.GetProperty("scroll").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            var target = scene.GetProperty("camera").GetProperty("target").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            Assert.Equal(new[] { scroll[0] + 160, -(scroll[1] + 120) }, target);
            Assert.Equal((scroll[0], scroll[1]), CameraMath.ToOriginalScrollSpace(new Vector3(target[0], target[1], 0)));
        }
    }

    [Fact]
    public void Export_EverySheetTextureAssetId_ResolvesToATextureWhosePngExists()
    {
        var assetInfos = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "AssetInfos.json"))).RootElement.GetProperty("asset_infos");
        var byId = assetInfos.EnumerateArray().ToDictionary(e => e.GetProperty("id").GetString()!, e => e);
        var paths = Directory.GetFiles(Path.Combine(Root, "Maps"), "*.effects.json", SearchOption.AllDirectories)
            .Append(Path.Combine(Root, "Data", "effects", "effects-global.json")).ToList();
        Assert.Equal(158, paths.Count);

        var withSheet = 0;
        var withoutSheet = 0;
        foreach (var path in paths)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var id = document.RootElement.TryGetProperty("SheetTextureAssetId", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (id == null)
            {
                withoutSheet++;
                continue;
            }

            withSheet++;
            Assert.True(byId.TryGetValue(id, out var texture), $"{path}: sheet {id} is not in AssetInfos.json");
            Assert.Equal("texture", texture.GetProperty("asset_type").GetString());
            var texturePath = Path.Combine(Root, texture.GetProperty("file_name").GetString()!.Replace('\\', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(texturePath), texturePath);
            using var textureDocument = JsonDocument.Parse(File.ReadAllText(texturePath));
            var pngId = textureDocument.RootElement.GetProperty("texture_asset_id").GetString()!;
            Assert.True(byId.TryGetValue(pngId, out var png), $"{texturePath}: png {pngId} is not in AssetInfos.json");
            Assert.Equal("png", png.GetProperty("asset_type").GetString());
            var pngPath = Path.Combine(Root, png.GetProperty("file_name").GetString()!.Replace('\\', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(pngPath), pngPath);
        }

        Assert.Equal(87, withSheet);   // 86 maps and the global one
        Assert.Equal(71, withoutSheet); // null by design: the maps without a table of their own
    }
}
