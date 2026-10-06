using Alundra.Scripts;
using CasaEngine.Framework.UI.MGUI;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
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
/// E19.f2b1c T3 (docs/plan-e19-opcodes.md, F2B1C-R3): loads the versioned <c>alundra-project/UI/Screens/TextBoxScreen.xaml</c> through the pipeline production uses
/// (<see cref="UIScreenLoader"/>) into a headless <see cref="MGDesktop"/> (never a <c>UIRoot</c>) and checks the elements, the attributes that carry the pixels (the
/// view notes of the annex, section 1.2: <c>Padding="0"</c>, <c>WrapText="False"</c>, <c>Width="255"</c>, the clip canvas), the bindings and, next to them, the
/// envelope and the design-time data - the shape of <see cref="AlundraSaveScreenXamlTests"/>. The pixels themselves are proved by
/// <see cref="AlundraTextBoxPixelTests"/> on a real GPU.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraTextBoxScreenXamlTests
{
    private const string FrameSourceName = "973a9208-c867-57fe-bee3-cf30237221ef";
    private const string NameFrameSourceName = "91ca17ae-e279-5a45-bb45-b15fbabdde68";   // the baked 112 x 32 frame of the name (g_textTilesConfiguration)
    private const string JessPortraitId = "56f5809a-a47b-564e-b7f7-a66be1f98b04";        // bank 4, 48 x 56

    private static readonly string[] RowNames = { "Row0", "Row1", "Row2" };

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
            $"AlundraTextBoxScreenXamlTests: no versioned 'alundra-project/UI/Screens' above '{AppContext.BaseDirectory}'.");
    }

    private static MGWindow LoadWindow(out MGDesktop desktop) => LoadWindow(out desktop, out _);

    private static MGWindow LoadWindow(out MGDesktop desktop, out HeadlessUiTestHarness.HeadlessRuntime runtime)
    {
        // The frame is the 288 x 56 description box of the inventory: the headless provider answers 16 x 16 for anything it is not told about.
        (desktop, runtime) = HeadlessUiTestHarness.NewDesktop(imageSizes: new Dictionary<string, (int, int)>
        {
            [FrameSourceName] = (288, 56),
            [NameFrameSourceName] = (112, 32),
            [JessPortraitId] = (48, 56),
            ["wind_150"] = (16, 16),
            ["wind_173"] = (16, 16),
            ["wind_201"] = (16, 16),
            ["wind_228"] = (16, 16),
        });
        var source = XamlDocumentSource.FromString(File.ReadAllText(Path.Combine(ScreensDirectory(), "TextBoxScreen.xaml")), "TextBoxScreen.xaml");
        var window = UIScreenLoader.Load(desktop, source);
        desktop.Windows.Add(window);
        return window;
    }

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
        var envelopeJson = JObject.Parse(File.ReadAllText(Path.Combine(directory, "TextBoxScreen.uiscreen")));
        var envelope = new UIScreenAsset();
        envelope.Load(envelopeJson);

        Assert.Equal(Guid.Parse("5a6a4f8c-bcfc-4abf-9581-a687e4ad2228"), envelope.Id);
        Assert.Equal(Guid.Parse(AlundraTextBoxScreen.ScreenAssetId), envelope.Id);
        Assert.Equal("TextBoxScreen", envelopeJson["name"]!.ToString());
        Assert.Equal("TextBoxScreen.xaml", envelope.SourceXamlFile);
        Assert.Equal("TextBoxScreen.design.json", envelope.DesignTimeDataFile);
        Assert.Equal((320, 240), ((int)envelopeJson["preview_resolution"]!["x"]!, (int)envelopeJson["preview_resolution"]!["y"]!));
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    [Fact]
    public void DesignTimeData_PopulatesATextBoxViewModel_WithTheRestState()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "TextBoxScreen.design.json")));
        Assert.Equal(nameof(AlundraTextBoxViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraTextBoxViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(168, viewModel.Frame.Top);
        Assert.Equal((172, 50), (viewModel.Clip.Top, viewModel.Clip.Height));
        Assert.Equal(("Bonjour, Alundra !", 0, 1), (viewModel.Row0.Text, viewModel.Row0.Left, viewModel.Row0.Top));
        Assert.Equal(("deuxième ligne", 0, 17), (viewModel.Row1.Text, viewModel.Row1.Left, viewModel.Row1.Top));
        Assert.Equal((string.Empty, 0, 33), (viewModel.Row2.Text, viewModel.Row2.Left, viewModel.Row2.Top));
        Assert.Equal(("wind_150", 200, Visibility.Visible), (viewModel.Cursor.SourceName, viewModel.Cursor.Top, viewModel.Cursor.Visibility));

        // E19.f4c2: the preview shows the portrait only (as the inventory's design data does: no translation, no scale, no brightness); the name box and the visibilities of the frame and the clip keep their defaults.
        Assert.Equal((JessPortraitId, Visibility.Visible), (viewModel.Portrait.SourceName, viewModel.Portrait.Visibility));
        Assert.Equal((Visibility.Collapsed, Visibility.Visible, Visibility.Visible), (viewModel.NameBox.Visibility, viewModel.Frame.Visibility, viewModel.Clip.Visibility));
    }

    /// <summary>No title bar and no close button (the original has none); the window is a bare 320 x 240 shell: no border, no padding, no background.</summary>
    [Fact]
    public void Window_IsABareModalShell_320By240()
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
        Assert.Equal((0, 0, 320, 240), (window.Left, window.Top, window.WindowWidth, window.WindowHeight));
    }

    [Fact]
    public void RootCanvas_IsNativeThreeTwentyByTwoForty_AndHoldsTheFrameTheCursorTheTextClipThePortraitTheNameFrameThenTheNameText()
    {
        var window = LoadWindow(out _);
        var canvas = Element<MGCanvas>(window, "RootCanvas");

        Assert.Equal(320, canvas.PreferredWidth);
        Assert.Equal(240, canvas.PreferredHeight);

        // The draw order of the binary's slot 0 then slot 2: the frame, the cursor over it, then the text, over the cursor; then (E19.f4c2) slot 3, the portrait over the frame and the
        // text, then slots 5 and 6, the name frame over the portrait and the name text over its frame.
        Assert.Equal(new[] { "Frame", "Cursor", "TextClip", "PortraitImage", "NameFrame", "NameText" }, canvas.Children.Select(child => child.Name ?? string.Empty).ToList());
    }

    [Fact]
    public void TheFrame_IsTheDescriptionBoxByItsId_AndTheCursorIsNamedByItsSprite()
    {
        var window = LoadWindow(out _);
        var frame = Element<MGImage>(window, "Frame");
        var cursor = Element<MGImage>(window, "Cursor");

        Assert.Equal(FrameSourceName, frame.SourceName);
        Assert.Equal(16, frame.CanvasLeft);
        Assert.Equal(288, cursor.CanvasLeft);
    }

    /// <summary>The three rows are font3 texts drawn pixel for pixel: no padding, no line padding, no wrap, a 255-pixel band, inline formatting off.</summary>
    [Fact]
    public void TheRows_CarryTheAttributesThatDecideTheirPixels()
    {
        var window = LoadWindow(out _);
        var clip = Element<MGCanvas>(window, "TextClip");

        Assert.Equal(258, clip.PreferredWidth);
        Assert.Equal(32, clip.CanvasLeft);
        Assert.True(clip.ClipToBounds);
        Assert.Equal(RowNames, clip.Children.Select(child => child.Name ?? string.Empty).ToList());

        foreach (var name in RowNames)
        {
            var row = Element<MGTextBlock>(window, name);
            Assert.Equal("font3", row.FontFamily);
            Assert.False(row.AllowsInlineFormatting, $"'{name}' allows inline formatting");
            Assert.False(row.WrapText, $"'{name}' wraps");
            Assert.Equal(0f, row.LinePadding);
            Assert.Equal(0, row.Padding.Left);
            Assert.Equal(0, row.Padding.Top);
            Assert.Equal(0, row.Padding.Right);
            Assert.Equal(0, row.Padding.Bottom);
            Assert.Equal(255, row.PreferredWidth);
            Assert.Equal(VerticalAlignment.Top, row.VerticalContentAlignment);
        }
    }

    /// <summary>E19.f4c2 F4C2-R1: the three elements of the speaker. The portrait sits at its rest position (8, 116) and the flight moves it through the bound render transform
    /// (MGUI ADR-0020), its tint through the bound <c>Brightness</c> (MGUI ADR-0021); the name frame is the baked 112 x 32 frame at y 140; the name is a font3 text at y 148, no
    /// padding, no wrap, no width (the binary's clip never cuts a name).</summary>
    [Fact]
    public void ThePortraitAndTheNameBox_CarryTheAttributesThatDecideTheirPixels()
    {
        var window = LoadWindow(out _);
        var portrait = Element<MGImage>(window, "PortraitImage");
        var nameFrame = Element<MGImage>(window, "NameFrame");
        var nameText = Element<MGTextBlock>(window, "NameText");

        Assert.Equal(Stretch.None, portrait.Stretch);
        Assert.Equal((8, 116), (portrait.CanvasLeft, portrait.CanvasTop));
        Assert.Equal(Stretch.None, nameFrame.Stretch);
        Assert.Equal(NameFrameSourceName, nameFrame.SourceName);
        Assert.Equal(140, nameFrame.CanvasTop);
        Assert.Equal("font3", nameText.FontFamily);
        Assert.False(nameText.AllowsInlineFormatting);
        Assert.False(nameText.WrapText);
        Assert.Equal(0f, nameText.LinePadding);
        Assert.Equal((0, 0, 0, 0), (nameText.Padding.Left, nameText.Padding.Top, nameText.Padding.Right, nameText.Padding.Bottom));
        Assert.Equal(VerticalAlignment.Top, nameText.VerticalContentAlignment);
        Assert.Equal(148, nameText.CanvasTop);
        Assert.Null(nameText.PreferredWidth);
    }

    [Fact]
    public void TheSpeakerBindings_PushTheViewModel_ToThePortraitAndTheNameBox_AndTheFrameAndTheClipFollowTheirVisibility()
    {
        var window = LoadWindow(out var desktop, out var runtime);
        var viewModel = new AlundraTextBoxViewModel { RootVisibility = Visibility.Visible };
        window.WindowDataContext = viewModel;

        viewModel.NameBox.Left = 64;
        viewModel.NameBox.TextLeft = 109;
        viewModel.NameBox.Text = "Jess";
        viewModel.NameBox.Visibility = Visibility.Visible;
        viewModel.Portrait.SourceName = JessPortraitId;
        viewModel.Portrait.Translation = new Vector2(81, -9);
        viewModel.Portrait.Scale = new Vector2(22f / 48f, 26f / 56f);
        viewModel.Portrait.Brightness = 195f / 128f;
        viewModel.Portrait.Visibility = Visibility.Visible;
        viewModel.Frame.Visibility = Visibility.Collapsed;
        viewModel.Clip.Visibility = Visibility.Collapsed;
        Settle(runtime, desktop);

        var portrait = Element<MGImage>(window, "PortraitImage");
        var nameFrame = Element<MGImage>(window, "NameFrame");
        var nameText = Element<MGTextBlock>(window, "NameText");
        Assert.Equal(JessPortraitId, portrait.SourceName);
        Assert.Equal(new Vector2(81, -9), portrait.RenderTransform.Translation);
        Assert.Equal(new Vector2(22f / 48f, 26f / 56f), portrait.RenderTransform.Scale);
        Assert.Equal(195f / 128f, portrait.Brightness);
        Assert.Equal(Visibility.Visible, portrait.Visibility);
        Assert.Equal(new Rectangle(64, 140, 112, 32), nameFrame.LayoutBounds);
        Assert.Equal(Visibility.Visible, nameFrame.Visibility);
        Assert.Equal(("Jess", 109, Visibility.Visible), (nameText.Text, nameText.CanvasLeft, nameText.Visibility));
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "Frame").Visibility);
        Assert.Equal(Visibility.Collapsed, Element<MGCanvas>(window, "TextClip").Visibility);

        // The name leaves and the text box comes back.
        viewModel.NameBox.Visibility = Visibility.Collapsed;
        viewModel.Portrait.Visibility = Visibility.Collapsed;
        viewModel.Frame.Visibility = Visibility.Visible;
        viewModel.Clip.Visibility = Visibility.Visible;
        Settle(runtime, desktop);

        Assert.Equal(Visibility.Collapsed, nameFrame.Visibility);
        Assert.Equal(Visibility.Collapsed, nameText.Visibility);
        Assert.Equal(Visibility.Collapsed, portrait.Visibility);
        Assert.Equal(Visibility.Visible, Element<MGImage>(window, "Frame").Visibility);
        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "TextClip").Visibility);
    }

    /// <summary>Every bound member reaches its element, and the elements sit at the bounds of the original at rest: the frame (16, 168, 288, 56), the cursor
    /// (288, 200, 16, 16), the clip canvas (32, 172, 258, 50) and the rows at (32, 173), (32, 189), (32, 205).</summary>
    [Fact]
    public void Bindings_PushTheViewModel_AndTheBoundsAtRestAreTheOriginals()
    {
        var window = LoadWindow(out var desktop, out var runtime);
        var viewModel = new AlundraTextBoxViewModel { RootVisibility = Visibility.Visible };
        window.WindowDataContext = viewModel;

        viewModel.Frame.Top = 168;
        viewModel.Clip.Top = 172;
        viewModel.Clip.Height = 50;
        viewModel.Row0.Text = "Bonjour, Alundra !";
        viewModel.Row0.Left = 0;
        viewModel.Row0.Top = 1;
        viewModel.Row1.Text = "deuxième ligne";
        viewModel.Row1.Left = 0;
        viewModel.Row1.Top = 17;
        viewModel.Row2.Text = "Troisieme"; // an empty text takes no room: the bounds of a row are those of its text
        viewModel.Row2.Left = 0;
        viewModel.Row2.Top = 33;
        viewModel.Cursor.SourceName = "wind_150";
        viewModel.Cursor.Top = 200;
        viewModel.Cursor.Visibility = Visibility.Visible;
        Settle(runtime, desktop);

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);
        var frame = Element<MGImage>(window, "Frame");
        var cursor = Element<MGImage>(window, "Cursor");
        var clip = Element<MGCanvas>(window, "TextClip");
        Assert.Equal(new Rectangle(16, 168, 288, 56), frame.LayoutBounds);
        Assert.Equal(new Rectangle(288, 200, 16, 16), cursor.LayoutBounds);
        Assert.Equal(Visibility.Visible, cursor.Visibility);
        Assert.Equal("wind_150", cursor.SourceName);
        Assert.Equal(new Rectangle(32, 172, 258, 50), clip.LayoutBounds);
        var rows = RowNames.Select(name => Element<MGTextBlock>(window, name)).ToArray();
        Assert.Equal("Bonjour, Alundra !", rows[0].Text);
        Assert.Equal("deuxième ligne", rows[1].Text);
        Assert.Equal(new[] { (32, 173, 255), (32, 189, 255), (32, 205, 255) }, rows.Select(r => (r.LayoutBounds.X, r.LayoutBounds.Y, r.LayoutBounds.Width)).ToArray());

        // A later change follows: the slide-out, the scroll and the hidden cursor.
        viewModel.Frame.Top = 182;
        viewModel.Clip.Top = 181;
        viewModel.Row0.Top = -15;
        viewModel.Cursor.SourceName = "wind_228";
        viewModel.Cursor.Visibility = Visibility.Collapsed;
        viewModel.RootVisibility = Visibility.Collapsed;
        Settle(runtime, desktop);

        Assert.Equal(Visibility.Collapsed, Element<MGCanvas>(window, "RootCanvas").Visibility);
        Assert.Equal(182, frame.CanvasTop);
        Assert.Equal(181, clip.CanvasTop);
        Assert.Equal(-15, rows[0].CanvasTop);
        Assert.Equal("wind_228", cursor.SourceName);
        Assert.Equal(Visibility.Collapsed, cursor.Visibility);
    }
}
