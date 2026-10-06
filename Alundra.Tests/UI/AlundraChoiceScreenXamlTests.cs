using Alundra.Scripts;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.f3b F3B-1 (docs/plan-e19-opcodes.md, F3B-R1): loads the versioned <c>alundra-project/UI/Screens/ChoiceScreen.xaml</c> through the pipeline production uses
/// (<see cref="UIScreenLoader"/>) into a headless <see cref="MGDesktop"/> (never a <c>UIRoot</c>) and checks the envelope, the design-time data, the elements, the attributes that
/// carry the pixels (<c>docs/plan-e19-f3-annexe/view/view-notes.md</c> section 1: <c>Padding="0"</c> of the labels, <c>BorderThickness="0"</c> and <c>Padding="0"</c> of the
/// window), the bindings and the bounds at rest - the shape of <see cref="AlundraTextBoxScreenXamlTests"/>. The pixels themselves are proved by
/// <see cref="AlundraChoicePixelTests"/> on a real GPU.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraChoiceScreenXamlTests
{
    private const string FrameSourceName = "829f31a7-39d1-5fc5-b436-befd59b50985";

    private static readonly string[] LabelNames = { "Label0", "Label1" };

    private static string ScreensDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "UI", "Screens");
            if (File.Exists(Path.Combine(candidate, "SaveScreen.xaml")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraChoiceScreenXamlTests: no versioned 'alundra-project/UI/Screens' above '{AppContext.BaseDirectory}'.");
    }

    private static MGWindow LoadWindow(out MGDesktop desktop, out HeadlessUiTestHarness.HeadlessRuntime runtime)
    {
        // The frame is the baked 128 x 32 sprite of the choice box: the headless provider answers 16 x 16 for anything it is not told about.
        (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(imageSizes: new Dictionary<string, (int, int)>
        {
            [FrameSourceName] = (128, 32),
            ["wind_150"] = (16, 16),
            ["wind_173"] = (16, 16),
            ["wind_201"] = (16, 16),
            ["wind_228"] = (16, 16),
        });
        var source = XamlDocumentSource.FromString(File.ReadAllText(Path.Combine(ScreensDirectory(), "ChoiceScreen.xaml")), "ChoiceScreen.xaml");
        var window = UIScreenLoader.Load(desktop, source);
        desktop.Windows.Add(window);
        return window;
    }

    private static MGWindow LoadWindow(out MGDesktop desktop) => LoadWindow(out desktop, out _);

    private static T Element<T>(MGWindow window, string name) where T : MGElement
    {
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        return Assert.IsType<T>(element);
    }

    /// <summary>A bound change reaches the layout at the next update; a few updates settle the layout of a fresh window.</summary>
    private static void Settle(HeadlessUiTestHarness.HeadlessRuntime runtime, MGDesktop desktop)
    {
        for (var frame = 1; frame <= 3; frame++)
        {
            runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(16 * frame),
                TimeSpan.FromMilliseconds(16),
                new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
                new KeyboardState()));
            desktop.Update();
        }
    }

    [Fact]
    public void Envelope_CarriesTheScreenIdTheDllLoads_AndNamesFilesThatExist()
    {
        var directory = ScreensDirectory();
        var envelopeJson = JObject.Parse(File.ReadAllText(Path.Combine(directory, "ChoiceScreen.uiscreen")));
        var envelope = new UIScreenAsset();
        envelope.Load(envelopeJson);

        Assert.Equal(Guid.Parse("a623e495-f197-475c-887d-042140ec7248"), envelope.Id);
        Assert.Equal(Guid.Parse(AlundraChoiceScreen.ScreenAssetId), envelope.Id);
        Assert.Equal("ChoiceScreen", envelopeJson["name"]!.ToString());
        Assert.Equal("ChoiceScreen.xaml", envelope.SourceXamlFile);
        Assert.Equal("ChoiceScreen.design.json", envelope.DesignTimeDataFile);
        Assert.Equal((320, 240), ((int)envelopeJson["preview_resolution"]!["x"]!, (int)envelopeJson["preview_resolution"]!["y"]!));
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    [Fact]
    public void DesignTimeData_PopulatesAChoiceViewModel_WithTheRestState()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "ChoiceScreen.design.json")));
        Assert.Equal(nameof(AlundraChoiceViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraChoiceViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(176, viewModel.Frame.Left);
        Assert.Equal(("OUI", 192), (viewModel.Label0.Text, viewModel.Label0.Left));
        Assert.Equal(("NON", 240), (viewModel.Label1.Text, viewModel.Label1.Left));
        Assert.Equal(("wind_173", 196), (viewModel.Cursor.SourceName, viewModel.Cursor.Left));
    }

    /// <summary>No title bar and no close button (the original has none); the window is a bare 320 x 240 shell: no border, no padding, no background.</summary>
    [Fact]
    public void Window_IsABareModalShell_320By240_ThatLetsNoClickThrough()
    {
        var window = LoadWindow(out _);

        Assert.False(window.IsTitleBarVisible);
        Assert.True(window.IsTopmost);
        Assert.False(window.IsUserResizable);
        Assert.Equal(0, window.BorderThickness.Left);
        Assert.Equal(0, window.BorderThickness.Top);
        Assert.Equal(0, window.BorderThickness.Right);
        Assert.Equal(0, window.BorderThickness.Bottom);
        Assert.Equal(0, window.Padding.Left);
        Assert.Equal(0, window.Padding.Top);
        Assert.Equal((0, 0, 320, 240), (window.Left, window.Top, window.WindowWidth, window.WindowHeight));

        // view-notes.md section 4.5: with AllowsClickThrough a press reaches the text box window below and flips the order of the two windows.
        Assert.False(window.AllowsClickThrough);
    }

    [Fact]
    public void RootCanvas_IsNativeThreeTwentyByTwoForty_AndHoldsTheFrameTheCursorThenTheTwoLabels()
    {
        var window = LoadWindow(out _);
        var canvas = Element<MGCanvas>(window, "RootCanvas");

        Assert.Equal(320, canvas.PreferredWidth);
        Assert.Equal(240, canvas.PreferredHeight);

        // The draw order of the binary: the frame cells, the cursor over them (slot 5), then the labels (slot 6).
        Assert.Equal(new[] { "Frame", "Cursor", "Label0", "Label1" }, canvas.Children.Select(child => child.Name ?? string.Empty).ToList());
    }

    [Fact]
    public void TheFrame_IsTheBakedChoiceBoxByItsId_AndTheCursorIsNamedByItsSprite_AtTheFixedRows()
    {
        var window = LoadWindow(out _);
        var frame = Element<MGImage>(window, "Frame");
        var cursor = Element<MGImage>(window, "Cursor");

        Assert.Equal(FrameSourceName, frame.SourceName);
        Assert.Equal(144, frame.CanvasTop);
        Assert.Equal(136, cursor.CanvasTop);
        foreach (var name in LabelNames)
        {
            Assert.Equal(152, Element<MGTextBlock>(window, name).CanvasTop);
        }
    }

    /// <summary>The two labels are font3 texts drawn pixel for pixel: no padding (O-E19-64), no line padding, no wrap, inline formatting off.</summary>
    [Fact]
    public void TheLabels_CarryTheAttributesThatDecideTheirPixels()
    {
        var window = LoadWindow(out _);

        foreach (var name in LabelNames)
        {
            var label = Element<MGTextBlock>(window, name);
            Assert.Equal("font3", label.FontFamily);
            Assert.False(label.AllowsInlineFormatting, $"'{name}' allows inline formatting");
            Assert.False(label.WrapText, $"'{name}' wraps");
            Assert.Equal(0f, label.LinePadding);
            Assert.Equal(0, label.Padding.Left);
            Assert.Equal(0, label.Padding.Top);
            Assert.Equal(0, label.Padding.Right);
            Assert.Equal(0, label.Padding.Bottom);
            Assert.Equal(VerticalAlignment.Top, label.VerticalContentAlignment);
        }
    }

    /// <summary>Every bound member reaches its element, and the elements sit at the bounds of the original at rest: the frame (176, 144, 128 x 32), the cursor (196, 136, 16 x 16) and
    /// the labels at (192, 152) and (240, 152) (their widths, 19 and 23, are font3's: only the GPU session measures them).</summary>
    [Fact]
    public void Bindings_PushTheViewModel_AndTheBoundsAtRestAreTheOriginals()
    {
        var window = LoadWindow(out var desktop, out var runtime);
        var viewModel = new AlundraChoiceViewModel { RootVisibility = Visibility.Visible };
        window.WindowDataContext = viewModel;

        viewModel.Frame.Left = 176;
        viewModel.Label0.Text = "OUI";
        viewModel.Label0.Left = 192;
        viewModel.Label1.Text = "NON";
        viewModel.Label1.Left = 240;
        viewModel.Cursor.SourceName = "wind_173";
        viewModel.Cursor.Left = 196;
        Settle(runtime, desktop);

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);
        var frame = Element<MGImage>(window, "Frame");
        var cursor = Element<MGImage>(window, "Cursor");
        Assert.Equal(new Rectangle(176, 144, 128, 32), frame.LayoutBounds);
        Assert.Equal(new Rectangle(196, 136, 16, 16), cursor.LayoutBounds);
        Assert.Equal("wind_173", cursor.SourceName);
        var labels = LabelNames.Select(name => Element<MGTextBlock>(window, name)).ToArray();
        Assert.Equal(("OUI", "NON"), (labels[0].Text, labels[1].Text));
        Assert.Equal(new[] { (192, 152), (240, 152) }, labels.Select(l => (l.LayoutBounds.X, l.LayoutBounds.Y)).ToArray());

        // A later change follows: the slide-out, the other cursor sprite and the hidden window.
        viewModel.Frame.Left = 320;
        viewModel.Cursor.SourceName = "wind_228";
        viewModel.Cursor.Left = 340;
        viewModel.RootVisibility = Visibility.Collapsed;
        Settle(runtime, desktop);

        Assert.Equal(Visibility.Collapsed, Element<MGCanvas>(window, "RootCanvas").Visibility);
        Assert.Equal(320, frame.CanvasLeft);
        Assert.Equal(340, cursor.CanvasLeft);
        Assert.Equal("wind_228", cursor.SourceName);
    }
}
