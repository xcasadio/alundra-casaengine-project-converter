#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
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
using Alundra.UI.Choice;
using Alundra.UI.Inventory;
using Alundra.UI.Messages;
using Alundra.Tests.Support;
using Alundra.Tests.UI.Inventory;

namespace Alundra.Tests.UI.Shared;

/// <summary>E19.f2b1c: the asset manager of the tests that build the real <see cref="Alundra.UI.Messages.TextBoxScreen"/> (and, E19.f3b, <see cref="Alundra.UI.Choice.ChoiceScreen"/>: the envelopes of the exported project and the
/// font3 of the registry, loaded on the CPU), as <c>ScreensFollowTheWindowTests</c> builds it.</summary>
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
        var screenInfo = new CasaEngine.Framework.Assets.AssetInfo(Guid.Parse(Alundra.UI.Messages.TextBoxScreen.ScreenAssetId))
        {
            Name = "TextBoxScreen",
            FileName = Path.Combine("UI", "Screens", "TextBoxScreen.uiscreen"),
        };
        // E19.f3b: the envelope of the choice screen too; `withTheScreen: false` still means "no screen asset" (neither of the two).
        var choiceInfo = new CasaEngine.Framework.Assets.AssetInfo(Guid.Parse(Alundra.UI.Choice.ChoiceScreen.ScreenAssetId))
        {
            Name = "ChoiceScreen",
            FileName = Path.Combine("UI", "Screens", "ChoiceScreen.uiscreen"),
        };
        var font3Info = new CasaEngine.Framework.Assets.AssetInfo(Alundra.UI.Inventory.InventoryScreen.Font3FontAssetId) { Name = "font3", FileName = Path.Combine("UI", "font3.fnt") };
        var assets = new CasaEngine.Framework.Assets.AssetContentManager
        {
            RuntimeContext = new CasaEngine.Framework.Application.EngineRuntimeContext(
                null,
                SaveGameDirectorTestSupport.FindProjectRoot(),
                id => withTheScreen && id == screenInfo.Id ? screenInfo : withTheScreen && id == choiceInfo.Id ? choiceInfo : id == font3Info.Id ? font3Info : null),
        };
        assets.RegisterAssetLoader(typeof(CasaEngine.Framework.UI.MGUI.UIScreenAsset), new ScreenEnvelopeLoader());
        assets.RegisterAssetLoader(typeof(CasaEngine.Framework.Assets.Fonts.BitmapFont), new InventoryScreenFontTests.CpuFont3Loader());
        return assets;
    }
}
