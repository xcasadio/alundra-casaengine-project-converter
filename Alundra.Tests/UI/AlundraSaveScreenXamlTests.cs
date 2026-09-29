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
/// E16.e T4 (docs/plan-e16-etat-partie.md, L5, SE10, D-E16-38): loads the versioned
/// <c>alundra-project/UI/Screens/SaveScreen.xaml</c> and <c>DialogueScreen.xaml</c> through the pipeline production
/// uses (<see cref="UIScreenLoader"/>) into a headless <see cref="MGDesktop"/> (the author's rule: never a
/// <c>UIRoot</c>), and checks the elements, the bindings (the box tint included) and the envelope and design-time
/// data next to them - the shape of <see cref="AlundraSubInventoryScreenXamlTests"/>.
/// </summary>
[Collection(MguiDataBindingCollection.Name)]
public sealed class AlundraSaveScreenXamlTests
{
    private const string BoxSourceName = "973a9208-c867-57fe-bee3-cf30237221ef";

    private static readonly string[] BoxNames = { "RecordBox0", "RecordBox1", "RecordBox2", "RecordBox3", "MessageBox" };

    private static readonly string[] TextNames =
    {
        "Record0Line0Text", "Record0Line1Text", "Record1Line0Text", "Record1Line1Text",
        "Record2Line0Text", "Record2Line1Text", "Record3Line0Text", "Record3Line1Text",
        "MessageLine0Text", "MessageLine1Text",
    };

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
            $"AlundraSaveScreenXamlTests: no versioned 'alundra-project/UI/Screens/SaveScreen.xaml' above '{AppContext.BaseDirectory}'.");
    }

    private static MGWindow LoadWindow(string fileName, out MGDesktop desktop)
    {
        (desktop, _) = HeadlessUiTestHarness.NewDesktop();
        var source = XamlDocumentSource.FromString(File.ReadAllText(Path.Combine(ScreensDirectory(), fileName)), fileName);
        var window = UIScreenLoader.Load(desktop, source);
        desktop.Windows.Add(window);
        return window;
    }

    private static MGWindow LoadSaveScreen() => LoadWindow("SaveScreen.xaml", out _);

    private static T Element<T>(MGWindow window, string name) where T : MGElement
    {
        Assert.True(window.TryGetElementByName(name, out MGElement element), $"missing '{name}'");
        return Assert.IsType<T>(element);
    }

    [Fact]
    public void Envelope_CarriesTheScreenIdTheDllLoads_AndNamesFilesThatExist()
    {
        var directory = ScreensDirectory();
        var envelope = new UIScreenAsset();
        envelope.Load(JObject.Parse(File.ReadAllText(Path.Combine(directory, "SaveScreen.uiscreen"))));

        Assert.Equal(Guid.Parse(AlundraSaveScreen.ScreenAssetId), envelope.Id);
        Assert.True(File.Exists(Path.Combine(directory, envelope.SourceXamlFile)));
        Assert.True(File.Exists(Path.Combine(directory, envelope.DesignTimeDataFile)));
    }

    [Fact]
    public void DesignTimeData_PopulatesASaveScreenViewModel()
    {
        var data = JObject.Parse(File.ReadAllText(Path.Combine(ScreensDirectory(), "SaveScreen.design.json")));
        Assert.Equal(nameof(AlundraSaveScreenViewModel), data["view_model_type"]!.ToString());

        var viewModel = new AlundraSaveScreenViewModel();
        JsonConvert.PopulateObject(data["values"]!.ToString(), viewModel);

        Assert.Equal(Visibility.Visible, viewModel.RootVisibility);
        Assert.Equal(Visibility.Visible, viewModel.RecordBox1.Visibility);
        Assert.Equal(64, viewModel.RecordBox1.Top);
        Assert.Null(viewModel.RecordBox1.TextureColor); // the tint is left out: Newtonsoft cannot read an XNA Color.
        Assert.Equal("Un Nouveau Départ", viewModel.Record1Line0.Text);
        Assert.Equal("Sélectionne une fente pour l'enregistrement.", viewModel.MessageLine0.Text);
    }

    /// <summary>D-E16-38: no title bar, so no close button (the original has none).</summary>
    [Fact]
    public void Window_HasNoTitleBar()
    {
        var window = LoadSaveScreen();
        Assert.False(window.IsTitleBarVisible);
    }

    /// <summary>D-E16-38: Alundra's dialogue window hides its title bar's close button, whose click would end a box
    /// out of band and leave a lone choice (the save screen's OUI/NON) waiting forever.</summary>
    [Fact]
    public void DialogueWindow_HidesItsCloseButton()
    {
        var window = LoadWindow("DialogueScreen.xaml", out _);
        Assert.False(window.IsCloseButtonVisible);
    }

    [Fact]
    public void RootCanvas_IsNativeThreeTwentyByTwoForty()
    {
        var canvas = Element<MGCanvas>(LoadSaveScreen(), "RootCanvas");
        Assert.Equal(320, canvas.PreferredWidth);
        Assert.Equal(240, canvas.PreferredHeight);
    }

    /// <summary>J4: the four picker boxes and the message box are the inventory's description box drawing.</summary>
    [Fact]
    public void Boxes_AreTheDescriptionBoxDrawing()
    {
        var window = LoadSaveScreen();
        foreach (var name in BoxNames)
        {
            Assert.Equal(BoxSourceName, Element<MGImage>(window, name).SourceName);
        }
    }

    /// <summary>SE10/L3: every text of the screen is font3 with inline formatting off - it never interprets
    /// markup.</summary>
    [Fact]
    public void EveryTextBlock_IsFont3_WithInlineFormattingOff()
    {
        var window = LoadSaveScreen();
        var canvas = Element<MGCanvas>(window, "RootCanvas");
        var textBlocks = canvas.TraverseVisualTree().OfType<MGTextBlock>().ToList();

        Assert.Equal(TextNames.Length, textBlocks.Count);
        foreach (var textBlock in textBlocks)
        {
            Assert.False(textBlock.AllowsInlineFormatting, $"'{textBlock.Name}' allows inline formatting");
            Assert.Equal("font3", textBlock.FontFamily);
        }

        foreach (var name in TextNames)
        {
            Element<MGTextBlock>(window, name);
        }
    }

    /// <summary>The canvas draws its children in order: the four boxes, their lines, then the message box and its
    /// lines.</summary>
    [Fact]
    public void RootCanvas_DrawOrder_BoxesThenTheirLinesThenTheMessageBox()
    {
        var names = Element<MGCanvas>(LoadSaveScreen(), "RootCanvas").Children.Select(child => child.Name ?? string.Empty).ToList();
        Assert.Equal(
            new[]
            {
                "RecordBox0", "RecordBox1", "RecordBox2", "RecordBox3",
                "Record0Line0Text", "Record0Line1Text", "Record1Line0Text", "Record1Line1Text",
                "Record2Line0Text", "Record2Line1Text", "Record3Line0Text", "Record3Line1Text",
                "MessageBox", "MessageLine0Text", "MessageLine1Text",
            },
            names);
    }

    /// <summary>Every bound member reaches its element - the tint through the image's <c>TextureColor</c> - and
    /// follows a later change.</summary>
    [Fact]
    public void Bindings_PushTheViewModel_TintIncluded()
    {
        var window = LoadWindow("SaveScreen.xaml", out var desktop);
        var viewModel = new AlundraSaveScreenViewModel { RootVisibility = Visibility.Visible };
        window.WindowDataContext = viewModel;

        viewModel.RecordBox1.Left = 16;
        viewModel.RecordBox1.Top = 64;
        viewModel.RecordBox1.Visibility = Visibility.Visible;
        viewModel.RecordBox1.TextureColor = AlundraSaveScreenViewModel.TintColor(0x40);
        viewModel.Record1Line0.Text = "Wendell Succombe";
        viewModel.Record1Line0.Left = 32;
        viewModel.Record1Line0.Top = 72;
        viewModel.MessageBox.Top = 168;
        viewModel.MessageBox.Visibility = Visibility.Visible;
        viewModel.MessageLine0.Text = "Enregistrer tes exploits ici?";
        desktop.Update();

        Assert.Equal(Visibility.Visible, Element<MGCanvas>(window, "RootCanvas").Visibility);
        var box = Element<MGImage>(window, "RecordBox1");
        Assert.Equal(Visibility.Visible, box.Visibility);
        Assert.Equal(16, box.CanvasLeft);
        Assert.Equal(64, box.CanvasTop);
        Assert.Equal(new Color(128, 128, 128, 255), box.TextureColor);
        var line = Element<MGTextBlock>(window, "Record1Line0Text");
        Assert.Equal("Wendell Succombe", line.Text);
        Assert.Equal(72, line.CanvasTop);
        Assert.Equal(168, Element<MGImage>(window, "MessageBox").CanvasTop);
        Assert.Equal("Enregistrer tes exploits ici?", Element<MGTextBlock>(window, "MessageLine0Text").Text);
        Assert.Equal(Visibility.Collapsed, Element<MGImage>(window, "RecordBox0").Visibility);

        viewModel.RecordBox1.TextureColor = AlundraSaveScreenViewModel.TintColor(0x80);
        viewModel.RecordBox1.Top = -57;
        desktop.Update();
        Assert.Equal(new Color(255, 255, 255, 255), box.TextureColor);
        Assert.Equal(-57, box.CanvasTop);
    }
}
