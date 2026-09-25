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
/// E13.d SI4 (docs/plan-e13d-sous-inventaire.md), versioned as a project asset since the bound screens
/// program (parent ADR-0002): loads <c>alundra-project/UI/Screens/SubInventoryScreen.xaml</c> through the
/// SAME pipeline production uses (<see cref="UIScreenLoader"/>), on the reduced headless harness, checks
/// every named element and every binding against a real <see cref="AlundraSubInventoryViewModel"/>, and the
/// envelope and design-time data next to it - same shape as <see cref="AlundraInventoryScreenXamlTests"/>.
/// <para/>
/// Loading this XAML creates MGUI bindings, which MGUI keeps in a static, single-threaded registry
/// (<c>DataBindingManager</c>): this class runs in <see cref="MguiDataBindingCollection"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraSubInventoryScreenXamlTests
{
    private static string ScreensDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "UI", "Screens");
            if (File.Exists(Path.Combine(candidate, "SubInventoryScreen.xaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraSubInventoryScreenXamlTests: no versioned 'alundra-project/UI/Screens/SubInventoryScreen.xaml' above '{AppContext.BaseDirectory}'.");
    }

    private static MGWindow LoadWindow() => LoadWindow(out _);

    private static MGWindow LoadWindow(out MGDesktop desktop)
    {
        (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var path = Path.Combine(ScreensDirectory(), "SubInventoryScreen.xaml");
        var source = XamlDocumentSource.FromString(File.ReadAllText(path), "SubInventoryScreen.xaml");
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
        envelope.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, "SubInventoryScreen.uiscreen"))));

        Assert.Equal(Guid.Parse(AlundraSubInventoryScreen.ScreenAssetId), envelope.Id);
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    /// <summary>The design-time data names the view model by its simple type name (the engine's ElementFactory
    /// convention) and fills it the way the editor does, with <see cref="JsonConvert.PopulateObject(string, object)"/>.</summary>
    [Fact]
    public void DesignTimeData_PopulatesASubInventoryViewModel()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "SubInventoryScreen.design.json")));
        Assert.Equal(nameof(AlundraSubInventoryViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraSubInventoryViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(Visibility.Visible, viewModel.BoxArmory.Visibility);
        Assert.NotNull(viewModel.BoxArmory.Left);
        Assert.Equal(Visibility.Visible, viewModel.Cursor.Visibility);
        Assert.NotNull(viewModel.MoneyDigit0.SourceName);
        Assert.Equal("Armure en tissu", viewModel.ArmorName.Text);
        Assert.Equal("Bottes courtes", viewModel.BootsName.Text);
    }

    /// <summary>The design-time armor and boots icons sit at their frame's own position, as the game draws them
    /// (D-E13D-34: the original's top-left, no centring), so the editor preview shows what the game draws.</summary>
    [Theory]
    [InlineData("ArmorIcon", "ArmorFrame")]
    [InlineData("BootsIcon", "BootsFrame")]
    public void DesignTimeData_EquipmentIcons_SitAtTheirFramePosition(string iconName, string frameName)
    {
        var values = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "SubInventoryScreen.design.json")))["values"]!;

        Assert.Equal((int)values[frameName]!["Left"]!, (int)values[iconName]!["Left"]!);
        Assert.Equal((int)values[frameName]!["Top"]!, (int)values[iconName]!["Top"]!);
    }

    /// <summary>What never changes is named in the XAML itself: the seven boxes (by their fixed SI2 ids),
    /// the two selection frames (wind_039) and the cursor, which plays the converter's ui_inventory_cursor
    /// animation.</summary>
    [Theory]
    [InlineData("BoxArmory", "10eb721a-628f-549c-bcaa-2ab2f670dbc3")]
    [InlineData("BoxKeyItems", "af453798-29af-5757-aaa5-10f1ff92af49")]
    [InlineData("BoxArmorName", "d5a10996-bb74-5e48-967b-be8bdde2c148")]
    [InlineData("BoxBootsName", "f7468bc4-faa0-5222-947b-037b12200759")]
    [InlineData("BoxEquipmentIcons", "1070d22e-b7e8-50d5-a58c-ccf80667185e")]
    [InlineData("BoxDescription", "973a9208-c867-57fe-bee3-cf30237221ef")]
    [InlineData("BoxMoneyFalconKey", "e8d58247-f02b-57cb-9ebf-f1df2b9be874")]
    [InlineData("ArmorFrame", "5f56fba6-2abb-5163-bb1e-e5a4f6fdb499")]
    [InlineData("BootsFrame", "5f56fba6-2abb-5163-bb1e-e5a4f6fdb499")]
    [InlineData("CursorImage", "a1a280e0-4e0b-5578-a452-b3778d796b1d")]
    public void FixedSources_AreNamedInTheXaml(string name, string assetId)
    {
        var window = LoadWindow();
        Assert.Equal(assetId, Element<MGImage>(window, name).SourceName);
    }

    /// <summary>Every bound member reaches its element, through nested paths, and follows a later change of
    /// a nested view model alone - a mutation removing the data context fails this test (it never observes
    /// the applied model at all).</summary>
    [Fact]
    public void Bindings_PushTheViewModel_AndFollowItsNestedChanges()
    {
        var window = LoadWindow(out var desktop);
        var viewModel = new AlundraSubInventoryViewModel();
        window.WindowDataContext = viewModel;

        viewModel.Apply(SampleModel());
        desktop.Update();

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);

        var box = Element<MGImage>(window, "BoxArmory");
        Assert.Equal(Visibility.Visible, box.Visibility);
        Assert.Equal(8, box.CanvasLeft);
        Assert.Equal(16, box.CanvasTop);

        var armoryIcon = Element<MGImage>(window, "ArmoryIcon3");
        Assert.Equal(ItemTablesFixture.SwordIconAssetId.ToString("D"), armoryIcon.SourceName);
        Assert.Equal(Visibility.Visible, armoryIcon.Visibility);
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "ArmoryIcon0").Visibility);

        var armorIcon = Element<MGImage>(window, "ArmorIcon");
        Assert.Equal(Visibility.Visible, armorIcon.Visibility);
        var armorFrame = Element<MGImage>(window, "ArmorFrame");
        Assert.Equal(Visibility.Visible, armorFrame.Visibility);
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "BootsIcon").Visibility);
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "BootsFrame").Visibility);

        var digit = Element<MGImage>(window, "MoneyDigit1");
        Assert.Equal("01dbdef2-854c-5c76-a482-e19bc579fa80", digit.SourceName); // wind_002, the digit 1
        Assert.Equal(212, digit.CanvasLeft);

        var name = Element<MGTextBlock>(window, "ArmorNameText");
        Assert.Equal("Armure en tissu", name.Text);
        Assert.Equal(192, name.CanvasLeft);

        var cursor = Element<MGImage>(window, "CursorImage");
        Assert.Equal(74, cursor.CanvasLeft);
        Assert.Equal(12, cursor.CanvasTop);

        viewModel.MoneyDigit1.Left = 99;
        viewModel.ArmoryIcon3.Visibility = Visibility.Collapsed;
        desktop.Update();
        Assert.Equal(99, digit.CanvasLeft);
        Assert.Equal(Visibility.Collapsed, armoryIcon.Visibility);
    }

    [Fact]
    public void ViewModel_ApplyingTheSameModelAgain_NotifiesNothing()
    {
        var viewModel = new AlundraSubInventoryViewModel();
        viewModel.Apply(SampleModel());

        var notifications = 0;
        void Count(object? sender, PropertyChangedEventArgs e) => notifications++;
        viewModel.PropertyChanged += Count;
        viewModel.BoxArmory.PropertyChanged += Count;
        viewModel.ArmoryIcon3.PropertyChanged += Count;
        viewModel.MoneyDigit1.PropertyChanged += Count;
        viewModel.ArmorName.PropertyChanged += Count;
        viewModel.Cursor.PropertyChanged += Count;

        viewModel.Apply(SampleModel());
        Assert.Equal(0, notifications);

        viewModel.Apply(SampleModel(cursorX: 50));
        Assert.Equal(1, notifications); // Cursor.Left alone
    }

    /// <summary>D-E13D-34: every icon is drawn at the original's own top-left, whatever its sprite's size (the
    /// view model reads none); FUN_80052fb4 draws the armor's frame at the icon's own position as soon as the
    /// item resolves.</summary>
    [Fact]
    public void ViewModel_Icons_AtTheOriginalTopLeft_FrameWithTheArmor()
    {
        var viewModel = new AlundraSubInventoryViewModel();
        viewModel.Apply(SampleModel());

        Assert.Equal(0x08 + 0x08, viewModel.ArmoryIcon3.Left);
        Assert.Equal(0x10 + 0x44, viewModel.ArmoryIcon3.Top);
        Assert.Equal(Visibility.Visible, viewModel.ArmorIcon.Visibility);
        Assert.Equal(144, viewModel.ArmorIcon.Left);
        Assert.Equal(40, viewModel.ArmorIcon.Top);
        Assert.Equal(Visibility.Visible, viewModel.ArmorFrame.Visibility);
        Assert.Equal(144, viewModel.ArmorFrame.Left);
        Assert.Equal(40, viewModel.ArmorFrame.Top);
        Assert.Equal(Visibility.Collapsed, viewModel.BootsIcon.Visibility); // no boots in the model
        Assert.Equal(Visibility.Collapsed, viewModel.BootsFrame.Visibility);
    }

    private static SubInventoryDisplayModel SampleModel(int cursorX = 74) => new()
    {
        Visible = true,
        Boxes = new[] { new SubInventoryBoxSprite(0, 8, 16) },
        ArmoryIcons = new[] { new SubInventorySlotIcon(3, new SubInventoryIcon(ItemTablesFixture.SwordIconAssetId, 0x08 + 0x08, 0x10 + 0x44)) },
        Armor = new SubInventoryEquipmentIcon(ItemTablesFixture.SwordIconAssetId, 144, 40),
        MoneyDigits = new[] { new InventoryDigit(0, 200, 100), new InventoryDigit(1, 212, 100) },
        ArmorName = "Armure en tissu",
        ArmorNameX = 192,
        ArmorNameY = 24,
        Cursor = new SubInventoryCursorState(cursorX, 12),
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
    [InlineData("BoxArmory")]
    [InlineData("BoxKeyItems")]
    [InlineData("BoxArmorName")]
    [InlineData("BoxBootsName")]
    [InlineData("BoxEquipmentIcons")]
    [InlineData("BoxDescription")]
    [InlineData("BoxMoneyFalconKey")]
    [InlineData("ArmorFrame")]
    [InlineData("BootsFrame")]
    [InlineData("ArmoryIcon0")]
    [InlineData("ArmoryIcon1")]
    [InlineData("ArmoryIcon2")]
    [InlineData("ArmoryIcon3")]
    [InlineData("ArmoryIcon4")]
    [InlineData("ArmoryIcon5")]
    [InlineData("ArmoryIcon6")]
    [InlineData("KeyItemIcon0")]
    [InlineData("KeyItemIcon1")]
    [InlineData("KeyItemIcon2")]
    [InlineData("KeyItemIcon3")]
    [InlineData("KeyItemIcon4")]
    [InlineData("ArmorIcon")]
    [InlineData("BootsIcon")]
    [InlineData("MoneyDigit0")]
    [InlineData("MoneyDigit1")]
    [InlineData("MoneyDigit2")]
    [InlineData("MoneyDigit3")]
    [InlineData("FalconDigit0")]
    [InlineData("FalconDigit1")]
    [InlineData("KeyDigit0")]
    [InlineData("KeyDigit1")]
    [InlineData("CursorImage")]
    public void NamedImage_ExistsWithRightType(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.IsType<MGImage>(element);
    }

    [Theory]
    [InlineData("ArmorNameText")]
    [InlineData("BootsNameText")]
    [InlineData("DescriptionLine0Text")]
    [InlineData("DescriptionLine1Text")]
    public void NamedTextBlock_ExistsWithRightType(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.IsType<MGTextBlock>(element);
    }

    /// <summary>D5.f (parent plan, same font contract as the main inventory): the XAML names font3 itself.</summary>
    [Theory]
    [InlineData("ArmorNameText")]
    [InlineData("BootsNameText")]
    [InlineData("DescriptionLine0Text")]
    [InlineData("DescriptionLine1Text")]
    public void NamedTextBlock_DeclaresFont3(string name)
    {
        var window = LoadWindow();
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        Assert.Equal("font3", Assert.IsType<MGTextBlock>(element).FontFamily);
    }

    /// <summary>Plan §1.5 ("l'ordre de dessin") and SI8: the canvas draws its children in order, so the XAML's own
    /// order is the draw order - the seven boxes, then the two frames, then the icons, then the texts and digits,
    /// the cursor last (on top of everything).</summary>
    [Fact]
    public void RootCanvas_DrawOrder_BoxesFramesIconsTextsDigitsCursor()
    {
        var window = LoadWindow();
        var canvas = Element<MGCanvas>(window, "RootCanvas");

        static int Group(string name) =>
            name.StartsWith("Box", StringComparison.Ordinal) ? 0
            : name.EndsWith("Frame", StringComparison.Ordinal) ? 1
            : name.Contains("Icon", StringComparison.Ordinal) ? 2
            : name.EndsWith("Text", StringComparison.Ordinal) || name.Contains("Digit", StringComparison.Ordinal) ? 3
            : name == "CursorImage" ? 4
            : -1;

        var names = canvas.Children.Select(child => child.Name ?? string.Empty).ToList();
        var groups = names.Select(Group).ToList();

        Assert.DoesNotContain(-1, groups);
        Assert.Equal(groups.OrderBy(g => g).ToList(), groups);
        Assert.Equal("CursorImage", names[^1]);
        Assert.Equal(7, groups.Count(g => g == 0));
    }
}
