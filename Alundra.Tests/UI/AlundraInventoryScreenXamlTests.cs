using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E13.d D5 (docs/plan-e13d-inventaire.md, D-E13D-16/17): loads the embedded <c>InventoryScreen.xaml</c>
/// through the SAME pipeline production uses (<see cref="XamlDocumentSource"/> +
/// <see cref="UIScreenLoader"/>), on the reduced headless harness (ADR-0001), and checks every named
/// element the code (<see cref="AlundraInventoryScreen"/>) looks up by name exists with the right type -
/// the exact failure mode <c>XamlUIScreenBase.FindControl</c> would otherwise only report far from its
/// cause, at first real use.
/// </summary>
public sealed class AlundraInventoryScreenXamlTests
{
    private const string XamlResourceName = "Alundra.Screens.InventoryScreen.xaml";

    private static MGWindow LoadWindow()
    {
        var (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var xaml = GeneralUtils.ReadEmbeddedResourceAsString(typeof(AlundraInventoryScreen).Assembly, XamlResourceName);
        var source = XamlDocumentSource.FromString(xaml, "InventoryScreen.xaml");
        return UIScreenLoader.Load(desktop, source);
    }

    [Fact]
    public void EmbeddedXaml_IsFoundAndParses()
    {
        var window = LoadWindow();
        Assert.NotNull(window);
    }

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
