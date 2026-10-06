#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using CasaEngine.Framework.UI.Backend.MonoGame;
using CasaEngine.Framework.UI.Backend.MonoGame.Assets;
using FontStashSharp;
using MGUI.Core.UI;
using MGUI.FontStashSharp;
using MGUI.Shared.Assets;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace Alundra.Tests.UI;

/// <summary>
/// E19.f2b1c T5 (docs/plan-e19-opcodes.md): the real-GPU harness of the pixel test of the text box screen, from the public API only (the engine's own
/// <c>GpuDeviceHost</c> is internal to <c>CasaEngine.Tests</c>); the skeleton of the discovery's probe (<c>docs/plan-e19-f2b1-annexe/view-notes.md</c> section 3). It
/// owns one dedicated thread and one hidden <see cref="Game"/> (MonoGame draws only on the thread that first touched it), builds the engine's own
/// <see cref="CasaDesktopRuntime"/> (<see cref="CasaMonoGameBackendBootstrap"/>) with a stand-in asset provider that reads the exported project, a real
/// <see cref="FontStashSharpTextEngine"/> with the exported font3 registered, and draws an <see cref="MGDesktop"/> into a <see cref="RenderTarget2D"/> read back with
/// <c>GetData</c>. The production <c>CasaUIAssetProvider</c> and <c>UIRoot</c> path is not exercised: only the recipe proves the whole chain.
/// </summary>
internal static class TextBoxGpu
{
    public const int ClearR = 100;
    public const int ClearG = 149;
    public const int ClearB = 237;

    private static readonly BlockingCollection<Action> Work = new();
    private static readonly Lazy<(bool Available, string Reason)> Probe = new(DoProbe, LazyThreadSafetyMode.ExecutionAndPublication);
    private static Thread? _thread;
    private static HiddenGame? _game;
    private static Session? _session;

    public static bool IsAvailable => Probe.Value.Available;

    public static string UnavailableReason => Probe.Value.Reason;

    private sealed class HiddenGame : Game
    {
        public HiddenGame()
        {
            _ = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1024,
                PreferredBackBufferHeight = 768,
                PreferredDepthStencilFormat = DepthFormat.Depth24Stencil8,
                SynchronizeWithVerticalRetrace = false,
            };
            Content.RootDirectory = "Content";
            IsFixedTimeStep = false;
        }
    }

    private static (bool, string) DoProbe()
    {
        _thread = new Thread(() =>
        {
            foreach (var action in Work.GetConsumingEnumerable())
            {
                action();
            }
        })
        { IsBackground = true, Name = "Alundra-TextBox-Pixel-GPU-Thread" };
        _thread.Start();

        try
        {
            Invoke(() =>
            {
                _game = new HiddenGame();
                _game.RunOneFrame();
                if (_game.GraphicsDevice == null)
                {
                    throw new InvalidOperationException("The hidden game has no GraphicsDevice after RunOneFrame().");
                }
            });
            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"No real GraphicsDevice could be created in this process (no GPU or display?): {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Runs <paramref name="action"/> on the GPU thread and waits for it, re-throwing its exception here.</summary>
    public static void Invoke(Action action)
    {
        if (Thread.CurrentThread == _thread)
        {
            action();
            return;
        }

        ExceptionDispatchInfo? error = null;
        using var done = new ManualResetEventSlim(false);
        Work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
            finally
            {
                done.Set();
            }
        });
        done.Wait();
        error?.Throw();
    }

    public static T Invoke<T>(Func<T> func)
    {
        T result = default!;
        Invoke(() => { result = func(); });
        return result;
    }

    public static GraphicsDevice Device => _game!.GraphicsDevice;

    /// <summary>The one session (runtime, assets, font) of the process, built on first use on the GPU thread.</summary>
    public static Session GetSession() => Invoke(() => _session ??= new Session(_game!));

    // ---- the host the desktop runtime needs ---------------------------------------------------------------------------------------

    internal sealed class TestHost : IRenderHost, IRawInputSource
    {
        private readonly Game _game;
        private TimeSpan _time = TimeSpan.Zero;

        public TestHost(Game game) => _game = game;

        public Rectangle Bounds { get; set; }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;

        public Rectangle GetBounds() => Bounds;

        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);

        public MouseState GetMouseState() => default;

        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan>? PreviewUpdate;

        public event EventHandler<EventArgs>? EndUpdate;

        public void Tick()
        {
            _time += TimeSpan.FromMilliseconds(20);
            PreviewUpdate?.Invoke(this, _time);
            EndUpdate?.Invoke(this, EventArgs.Empty);
        }
    }

    // ---- the assets of the exported project -----------------------------------------------------------------------------------------

    /// <summary>A stand-in <see cref="IUIAssetProvider"/>: a sprite name or id of <c>AssetInfos.json</c> to its sheet texture and source rectangle, read from the exported project.</summary>
    internal sealed class ProjectAssets : IUIAssetProvider
    {
        private readonly string _root;
        private readonly GraphicsDevice _device;
        private readonly Dictionary<Guid, (string Name, string File)> _byId = new();
        private readonly Dictionary<string, Guid> _byName = new(); // the last writer wins, as AssetCatalog does
        private readonly Dictionary<string, Texture2D> _textures = new();

        public IUIAssetProvider? Inner { get; set; }

        public ProjectAssets(string projectRoot, GraphicsDevice device)
        {
            _root = projectRoot;
            _device = device;
            using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(projectRoot, "AssetInfos.json")));
            foreach (var e in doc.RootElement.GetProperty("asset_infos").EnumerateArray())
            {
                var id = Guid.Parse(e.GetProperty("id").GetString()!);
                var name = e.GetProperty("name").GetString()!;
                _byId[id] = (name, e.GetProperty("file_name").GetString()!);
                _byName[name] = id;
            }
        }

        public Texture2D LoadPng(string relativeFile)
        {
            if (!_textures.TryGetValue(relativeFile, out var texture))
            {
                using var stream = File.OpenRead(Path.Combine(_root, relativeFile));
                texture = Texture2D.FromStream(_device, stream);
                _textures[relativeFile] = texture;
            }

            return texture;
        }

        /// <summary>The sheet texture and the source rectangle of a sprite named by <paramref name="nameOrId"/>.</summary>
        public (Texture2D Texture, Rectangle Rect) ResolveSprite(string nameOrId)
        {
            var id = Guid.TryParse(nameOrId, out var guid) ? guid : _byName[nameOrId];
            using var sprite = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root, _byId[id].File)));
            var sheetId = Guid.Parse(sprite.RootElement.GetProperty("sprite_sheet_asset_id").GetString()!);
            var location = sprite.RootElement.GetProperty("location");
            var rect = new Rectangle(location.GetProperty("x").GetInt32(), location.GetProperty("y").GetInt32(), location.GetProperty("w").GetInt32(), location.GetProperty("h").GetInt32());
            using var sheet = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root, _byId[sheetId].File)));
            var textureId = Guid.Parse(sheet.RootElement.GetProperty("texture_asset_id").GetString()!);
            return (LoadPng(_byId[textureId].File), rect);
        }

        public IUIImageResource LoadImage(string assetName) => Inner!.LoadImage(assetName);

        public bool TryLoadImage(string assetName, out IUIImageResource image) => Inner!.TryLoadImage(assetName, out image);

        public bool TryResolveImage(string name, out IUIImageResource? image, out Rectangle? sourceRect)
        {
            image = null;
            sourceRect = null;
            try
            {
                var (texture, rect) = ResolveSprite(name);
                image = new CasaMonoGameImageResource(texture);
                sourceRect = rect;
                return true;
            }
            catch (Exception ex) when (ex is KeyNotFoundException or FileNotFoundException)
            {
                return false;
            }
        }
    }

    internal sealed class Session
    {
        public TestHost Host { get; }

        public CasaDesktopRuntime Runtime { get; }

        public ProjectAssets Assets { get; }

        public FontStashSharpTextEngine Engine { get; }

        public string ProjectRoot { get; }

        public Session(Game game)
        {
            ProjectRoot = SaveGameDirectorTestSupport.FindProjectRoot();
            Host = new TestHost(game) { Bounds = new Rectangle(0, 0, 320, 240) };
            Assets = new ProjectAssets(ProjectRoot, game.GraphicsDevice);
            var backend = CasaMonoGameBackendBootstrap.Create(Host, Host, surface: null, assetProvider: Assets);
            Runtime = backend.Runtime;
            Assets.Inner = new CasaUIAssetProvider(Runtime.Content, Runtime.FontManager, null);

            var page = Assets.LoadPng(Path.Combine("UI", "Textures", "font3.png"));
            Engine = new FontStashSharpTextEngine();
            var tahoma = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Content", "Fonts", "tahoma.ttf"));
            var fontSystem = new FontSystem();
            fontSystem.AddFont(tahoma);
            Engine.AddFontSystem(Runtime.DefaultFontFamily, CustomFontStyles.Normal, fontSystem, tahoma);
            Engine.MatchSpriteFontSizing(Runtime.FontManager);
            var font3 = StaticSpriteFont.FromBMFont(File.ReadAllText(Path.Combine(ProjectRoot, "UI", "font3.fnt")), _ => new TextureWithOffset(page));
            Engine.AddStaticFont("font3", CustomFontStyles.Normal, font3);
        }

        /// <summary>A fresh desktop on the runtime, with the real font3 engine.</summary>
        public MGDesktop NewDesktop() => new(Runtime) { TextEngine = Engine };

        /// <summary>Draws <paramref name="desktop"/> into a target of <c>(w + 2 ox) x (h + 2 oy)</c> cleared to the background, its view at <c>(ox, oy, w, h)</c> (the view
        /// offset of a window that is not 4:3, engine ADR-0054; the host's bounds are view-local), and returns the pixels of the whole target.</summary>
        public Color[] Draw(MGDesktop desktop, int w, int h, int ox = 0, int oy = 0)
        {
            var device = Host.GraphicsDevice;
            Host.Bounds = new Rectangle(0, 0, w, h);
            var targetWidth = w + 2 * ox;
            var targetHeight = h + 2 * oy;
            using var target = new RenderTarget2D(device, targetWidth, targetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            device.SetRenderTarget(target);
            device.Viewport = new Viewport(0, 0, targetWidth, targetHeight);
            device.ScissorRectangle = new Rectangle(0, 0, targetWidth, targetHeight);
            device.Clear(ClearOptions.Target | ClearOptions.Stencil, new Color(ClearR, ClearG, ClearB, 255), 1f, 0);
            device.Viewport = new Viewport(ox, oy, w, h);
            for (var i = 0; i < 3; i++)
            {
                Host.Tick();
                desktop.Update();
            }

            desktop.Draw();
            device.SetRenderTarget(null);
            var pixels = new Color[targetWidth * targetHeight];
            target.GetData(pixels);
            return pixels;
        }

        /// <summary>The pixels of a PNG (a versioned reference image).</summary>
        public Color[] ReadPng(string path, out int width, out int height)
        {
            using var stream = File.OpenRead(path);
            using var texture = Texture2D.FromStream(Host.GraphicsDevice, stream);
            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            width = texture.Width;
            height = texture.Height;
            return pixels;
        }
    }

    // ---- an independent reference compositor (the discovery's, for the synthetic states) -------------------------------------------

    internal sealed class Img
    {
        public int W { get; }
        public int H { get; }
        public Color[] P { get; }

        public Img(Texture2D texture)
        {
            W = texture.Width;
            H = texture.Height;
            P = new Color[W * H];
            texture.GetData(P);
        }
    }

    internal sealed record Glyph(int X, int Y, int W, int H, int XOffset, int YOffset, int XAdvance);

    internal static class Fnt
    {
        public static Dictionary<int, Glyph> Parse(string text)
        {
            var glyphs = new Dictionary<int, Glyph>();
            foreach (var line in text.Split('\n'))
            {
                if (!line.StartsWith("char ", StringComparison.Ordinal))
                {
                    continue;
                }

                int Field(string key) => int.Parse(Regex.Match(line, key + @"=(-?\d+)").Groups[1].Value);
                glyphs[Field("id")] = new Glyph(Field("x"), Field("y"), Field("width"), Field("height"), Field("xoffset"), Field("yoffset"), Field("xadvance"));
            }

            return glyphs;
        }
    }

    internal sealed class Compositor
    {
        public Color[] P { get; }
        public int W { get; }
        public int H { get; }
        public Rectangle? Clip { get; set; }

        public Compositor(int w, int h)
        {
            W = w;
            H = h;
            P = new Color[w * h];
            Array.Fill(P, new Color(ClearR, ClearG, ClearB, 255));
        }

        public void Blit(Img source, Rectangle rect, int dx, int dy, Rectangle? bandClip = null)
        {
            for (var y = 0; y < rect.Height; y++)
            {
                for (var x = 0; x < rect.Width; x++)
                {
                    var px = dx + x;
                    var py = dy + y;
                    if (px < 0 || py < 0 || px >= W || py >= H)
                    {
                        continue;
                    }

                    if (Clip.HasValue && !Clip.Value.Contains(px, py))
                    {
                        continue;
                    }

                    if (bandClip.HasValue && !bandClip.Value.Contains(px, py))
                    {
                        continue;
                    }

                    var c = source.P[(rect.Y + y) * source.W + rect.X + x];
                    if (c.A == 0)
                    {
                        continue;
                    }

                    P[py * W + px] = new Color(c.R, c.G, c.B, (byte)255);
                }
            }
        }

        /// <summary>A line as the binary draws it: each glyph rectangle at (pen + xoffset, y + yoffset), the pen moving by xadvance, cut at <paramref name="bandWidth"/> pixels from x.</summary>
        public void DrawText(Img page, Dictionary<int, Glyph> fnt, string text, int x, int y, int bandWidth)
        {
            var pen = x;
            var band = new Rectangle(x, y - 1000, bandWidth, 3000);
            foreach (var ch in text)
            {
                if (!fnt.TryGetValue(ch, out var g))
                {
                    continue;
                }

                Blit(page, new Rectangle(g.X, g.Y, g.W, g.H), pen + g.XOffset, y + g.YOffset, band);
                pen += g.XAdvance;
            }
        }

        /// <summary>The same picture at an integer scale, each texel a k x k block (the root canvas scale draws every sprite and glyph quad point-sampled).</summary>
        public Color[] Scaled(int k)
        {
            var scaled = new Color[W * k * H * k];
            for (var y = 0; y < H * k; y++)
            {
                for (var x = 0; x < W * k; x++)
                {
                    scaled[y * W * k + x] = P[(y / k) * W + x / k];
                }
            }

            return scaled;
        }
    }
}

/// <summary>Like <see cref="FactAttribute"/>, but skipped (not failed) when no real <see cref="GraphicsDevice"/> can be created in this process (a headless agent): the pattern of the
/// engine's own <c>GpuFactAttribute</c>.</summary>
public sealed class AlundraGpuFactAttribute : FactAttribute
{
    public AlundraGpuFactAttribute()
    {
        if (!TextBoxGpu.IsAvailable)
        {
            Skip = TextBoxGpu.UnavailableReason;
        }
    }
}

/// <summary>E19.f2b1c: the asset manager of the tests that build the real <see cref="Alundra.Scripts.AlundraTextBoxScreen"/> (the envelope of the exported project and the
/// font3 of the registry, loaded on the CPU), as <c>AlundraScreensFollowTheWindowTests</c> builds it.</summary>
internal static class TextBoxScreenAssets
{
    private sealed class ScreenEnvelopeLoader : CasaEngine.Framework.Assets.IAssetLoader
    {
        public object LoadAsset(string fileName, CasaEngine.Framework.Assets.AssetContentManager assetContentManager)
        {
            var asset = new CasaEngine.Framework.UI.MGUI.UIScreenAsset();
            asset.Load(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(fileName)));
            return asset;
        }

        public bool IsFileSupported(string fileName) => true;
    }

    public static CasaEngine.Framework.Assets.AssetContentManager New(bool withTheScreen = true)
    {
        var screenInfo = new CasaEngine.Framework.Assets.AssetInfo(Guid.Parse(Alundra.Scripts.AlundraTextBoxScreen.ScreenAssetId))
        {
            Name = "TextBoxScreen",
            FileName = Path.Combine("UI", "Screens", "TextBoxScreen.uiscreen"),
        };
        var font3Info = new CasaEngine.Framework.Assets.AssetInfo(Alundra.Scripts.AlundraInventoryScreen.Font3FontAssetId) { Name = "font3", FileName = Path.Combine("UI", "font3.fnt") };
        var assets = new CasaEngine.Framework.Assets.AssetContentManager
        {
            RuntimeContext = new CasaEngine.Framework.Application.EngineRuntimeContext(
                null,
                SaveGameDirectorTestSupport.FindProjectRoot(),
                id => withTheScreen && id == screenInfo.Id ? screenInfo : id == font3Info.Id ? font3Info : null),
        };
        assets.RegisterAssetLoader(typeof(CasaEngine.Framework.UI.MGUI.UIScreenAsset), new ScreenEnvelopeLoader());
        assets.RegisterAssetLoader(typeof(CasaEngine.Framework.Assets.Fonts.BitmapFont), new AlundraInventoryScreenFontTests.CpuFont3Loader());
        return assets;
    }
}
