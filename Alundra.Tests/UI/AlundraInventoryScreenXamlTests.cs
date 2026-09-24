using System.ComponentModel;
using Alundra.Scripts;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-16/17), versioned as a project asset since the bound screens
/// program (parent ADR-0002): loads <c>alundra-project/UI/Screens/InventoryScreen.xaml</c> through the SAME pipeline
/// production uses (<see cref="UIScreenLoader"/>), on the reduced headless harness, checks every named element and
/// every binding against a real <see cref="AlundraInventoryViewModel"/>, and the envelope and design-time data next
/// to it.
/// <para/>
/// Loading this XAML creates MGUI bindings, which MGUI keeps in a static, single-threaded registry
/// (<c>DataBindingManager</c>): this class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraInventoryScreenXamlTests
{
    private static string ScreensDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "UI", "Screens");
            if (File.Exists(Path.Combine(candidate, "InventoryScreen.xaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraInventoryScreenXamlTests: no versioned 'alundra-project/UI/Screens/InventoryScreen.xaml' above '{AppContext.BaseDirectory}'.");
    }

    private static MGWindow LoadWindow() => LoadWindow(out _);

    private static MGWindow LoadWindow(out MGDesktop desktop)
    {
        (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var path = Path.Combine(ScreensDirectory(), "InventoryScreen.xaml");
        var source = XamlDocumentSource.FromString(File.ReadAllText(path), "InventoryScreen.xaml");
        var window = UIScreenLoader.Load(desktop, source);
        desktop.Windows.Add(window);
        return window;
    }

    private static T Element<T>(MGWindow window, string name) where T : MGElement
    {
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        return Assert.IsType<T>(element);
    }

    [Fact]
    public void VersionedXaml_IsFoundAndParses()
    {
        var window = LoadWindow();
        Assert.NotNull(window);
    }

    [Fact]
    public void Envelope_CarriesTheScreenIdTheDllLoads_AndNamesFilesThatExist()
    {
        var directory = ScreensDirectory();
        var envelope = new UIScreenAsset();
        envelope.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, "InventoryScreen.uiscreen"))));

        Assert.Equal(Guid.Parse(AlundraInventoryScreen.ScreenAssetId), envelope.Id);
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    /// <summary>The design-time data names the view model by its simple type name (the engine's ElementFactory
    /// convention) and fills it the way the editor does, with <see cref="JsonConvert.PopulateObject(string, object)"/>.</summary>
    [Fact]
    public void DesignTimeData_PopulatesAnInventoryViewModel()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "InventoryScreen.design.json")));
        Assert.Equal(nameof(AlundraInventoryViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraInventoryViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(Visibility.Visible, viewModel.BoxWeapon.Visibility);
        Assert.NotNull(viewModel.BoxWeapon.Left);
        Assert.Equal(Visibility.Visible, viewModel.Cursor.Visibility);
        Assert.NotNull(viewModel.MoneyDigit0.SourceName);
    }

    /// <summary>What never changes is named in the XAML itself: the boxes, the selection frames (wind_039) and the
    /// cursor, which plays the converter's ui_inventory_cursor animation.</summary>
    [Theory]
    [InlineData("BoxWeapon", "c2447262-414f-5b78-9def-b54b080636f4")]
    [InlineData("BoxDescription", "973a9208-c867-57fe-bee3-cf30237221ef")]
    [InlineData("WeaponSelectionFrame", "5f56fba6-2abb-5163-bb1e-e5a4f6fdb499")]
    [InlineData("CursorImage", "a1a280e0-4e0b-5578-a452-b3778d796b1d")]
    public void FixedSources_AreNamedInTheXaml(string name, string assetId)
    {
        var window = LoadWindow();
        Assert.Equal(assetId, Element<MGImage>(window, name).SourceName);
    }

    /// <summary>Every bound member reaches its element, through nested paths (<c>IconSlot3.SourceName</c>, point O1 of
    /// the parent plan), and follows a later change of a nested view model alone.</summary>
    [Fact]
    public void Bindings_PushTheViewModel_AndFollowItsNestedChanges()
    {
        var window = LoadWindow(out var desktop);
        var viewModel = new AlundraInventoryViewModel(_ => new Point(16, 16));
        window.WindowDataContext = viewModel;

        viewModel.Apply(SampleModel());
        desktop.Update();

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);

        var box = Element<MGImage>(window, "BoxWeapon");
        Assert.Equal(Visibility.Visible, box.Visibility);
        Assert.Equal(8, box.CanvasLeft);
        Assert.Equal(16, box.CanvasTop);

        var icon = Element<MGImage>(window, "IconSlot3");
        Assert.Equal(ItemTablesFixture.SwordIconAssetId.ToString("D"), icon.SourceName);
        Assert.Equal(Visibility.Visible, icon.Visibility);
        Assert.Equal(100 + (24 - 16) / 2, icon.CanvasLeft);
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "IconSlot4").Visibility);

        var digit = Element<MGImage>(window, "MoneyDigit1");
        Assert.Equal("01dbdef2-854c-5c76-a482-e19bc579fa80", digit.SourceName); // wind_002, the digit 1
        Assert.Equal(212, digit.CanvasLeft);

        var name = Element<MGTextBlock>(window, "WeaponNameText");
        Assert.Equal("Sword", name.Text);
        Assert.Equal(192, name.CanvasLeft);

        // The cursor sits at the composer's base position: the animation carries the phase offset.
        var cursor = Element<MGImage>(window, "CursorImage");
        Assert.Equal(34, cursor.CanvasLeft);
        Assert.Equal(16, cursor.CanvasTop);

        viewModel.MoneyDigit1.Left = 99;
        viewModel.IconSlot3.Visibility = Visibility.Collapsed;
        desktop.Update();
        Assert.Equal(99, digit.CanvasLeft);
        Assert.Equal(Visibility.Collapsed, icon.Visibility);
    }

    [Fact]
    public void ViewModel_ApplyingTheSameModelAgain_NotifiesNothing()
    {
        var viewModel = new AlundraInventoryViewModel(_ => new Point(16, 16));
        viewModel.Apply(SampleModel());

        var notifications = 0;
        void Count(object? sender, PropertyChangedEventArgs e) => notifications++;
        viewModel.PropertyChanged += Count;
        viewModel.BoxWeapon.PropertyChanged += Count;
        viewModel.IconSlot3.PropertyChanged += Count;
        viewModel.MoneyDigit1.PropertyChanged += Count;
        viewModel.WeaponName.PropertyChanged += Count;
        viewModel.Cursor.PropertyChanged += Count;

        viewModel.Apply(SampleModel());
        Assert.Equal(0, notifications);

        viewModel.Apply(SampleModel(cursorBaseX: 50));
        Assert.Equal(1, notifications); // Cursor.Left alone
    }

    private static InventoryDisplayModel SampleModel(int cursorBaseX = 34) => new()
    {
        Visible = true,
        Boxes = new[] { new InventoryBoxSprite(0, 8, 16) },
        SlotIcons = new[] { new InventorySlotIcon(3, new AlundraHudIcon(ItemTablesFixture.SwordIconAssetId, 100, 40, 24, 32)) },
        MoneyDigits = new[] { new InventoryDigit(0, 200, 100), new InventoryDigit(1, 212, 100) },
        WeaponName = "Sword",
        WeaponNameX = 192,
        WeaponNameY = 24,
        Cursor = new InventoryCursorState(cursorBaseX - 1, 17, 2, cursorBaseX, 16),
    };

    [Fact]
    public void RootCanvas_IsNativeThreeTwentyByTwoForty()
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName("RootCanvas", out MGElement element));
        var canvas = Assert.IsType<MGCanvas>(element);
        Assert.Equal(320, canvas.PreferredWidth);
        Assert.Equal(240, canvas.PreferredHeight);
    }

    [Theory]
    [InlineData("BoxWeapon")]
    [InlineData("BoxItem")]
    [InlineData("BoxWeaponName")]
    [InlineData("BoxItemName")]
    [InlineData("BoxMoneyFalconKey")]
    [InlineData("BoxDescription")]
    [InlineData("HerbCountDigit")]
    [InlineData("MoneyDigit0")]
    [InlineData("MoneyDigit1")]
    [InlineData("MoneyDigit2")]
    [InlineData("MoneyDigit3")]
    [InlineData("FalconDigit0")]
    [InlineData("FalconDigit1")]
    [InlineData("KeyDigit0")]
    [InlineData("KeyDigit1")]
    [InlineData("WeaponSelectionFrame")]
    [InlineData("ItemSelectionFrame")]
    [InlineData("CursorImage")]
    public void NamedImage_ExistsWithRightType(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.IsType<MGImage>(element);
    }

    [Fact]
    public void AllTwentyFourSlotIcons_Exist()
    {
        var window = LoadWindow();
        for (var i = 0; i < AlundraInventoryComposer.SlotCount; i++)
        {
            var name = $"IconSlot{i}";
            Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
            Assert.IsType<MGImage>(element);
        }
    }

    [Theory]
    [InlineData("WeaponNameText")]
    [InlineData("ItemNameText")]
    [InlineData("DescriptionLine0Text")]
    [InlineData("DescriptionLine1Text")]
    public void NamedTextBlock_ExistsWithRightType(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.IsType<MGTextBlock>(element);
    }

    /// <summary>D5.f: the XAML names font3 itself; the screen only holds it through the engine's UI font
    /// registry. The headless harness's text engine resolves every family, so this pins the declaration.</summary>
    [Theory]
    [InlineData("WeaponNameText")]
    [InlineData("ItemNameText")]
    [InlineData("DescriptionLine0Text")]
    [InlineData("DescriptionLine1Text")]
    public void NamedTextBlock_DeclaresFont3(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.Equal("font3", Assert.IsType<MGTextBlock>(element).FontFamily);
    }
}
