using AlundraCasaEngineProjectConverter.Writers;
using CasaEngine.EditorServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.Textures;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

/// <summary>
/// Every texture EnsureTexture imports is pixel art (sprite/UI/font atlases, backdrop and navigation
/// tileset textures) that can land at a fractional screen position, so the emitted sampler must be
/// PointClamp: nearest-neighbour so a fractional position never blends texels (the "sharp when
/// stopped, blurry while moving" bug), and clamp addressing so an atlas edge never bleeds into a
/// neighbouring cell. See TextureAssetWriter.EnsureTexture's own sampler_state comment.
/// </summary>
public class TextureAssetWriterTests
{
    private static readonly byte[] FakePngBytes = { 137, 80, 78, 71, 13, 10, 26, 10 };

    [Fact]
    public void EnsureTexture_EmitsPointClampSamplerState_ReadableByTextureLoad()
    {
        var outputDirectory = CreateTempDirectory();
        var sourcePngPath = Path.Combine(CreateTempDirectory(), "atlas.png");
        File.WriteAllBytes(sourcePngPath, FakePngBytes);

        var previousProjectPath = EngineEnvironment.ProjectPath;
        try
        {
            EngineEnvironment.ProjectPath = outputDirectory;
            EditorAssetCatalogService.Clear();

            var cache = new Dictionary<string, Guid>();
            TextureAssetWriter.EnsureTexture(sourcePngPath, "Sprites/Textures", outputDirectory, cache);

            var wrapperPath = Path.Combine(outputDirectory, "Sprites", "Textures", "atlas.texture");
            Assert.True(File.Exists(wrapperPath));

            var wrapperDocument = JObject.Parse(File.ReadAllText(wrapperPath));
            var samplerStateDocument = (JObject)wrapperDocument["sampler_state"]!;

            // Field-by-field against EditorJsonSaveHelper.Save(this SamplerState, JObject) - the
            // shape Texture.Load's JsonHelper.GetSamplerState expects unconditionally.
            Assert.Equal(nameof(TextureFilter.Point), samplerStateDocument["texture_filter"]!.ToString());
            Assert.Equal(nameof(TextureAddressMode.Clamp), samplerStateDocument["address_u"]!.ToString());
            Assert.Equal(nameof(TextureAddressMode.Clamp), samplerStateDocument["address_v"]!.ToString());
            Assert.Equal(nameof(TextureAddressMode.Clamp), samplerStateDocument["address_w"]!.ToString());

            // Loading it back through the engine's own Texture.Load is the only way to catch a
            // schema mismatch: AssetLoader<Texture>.LoadAsset swallows any exception and returns
            // null, surfacing only as a generic "IAssetLoader can't load" error in the editor.
            var texture = new CasaEngine.Framework.Assets.Textures.Texture();
            texture.Load(wrapperDocument);
            Assert.Equal(SamplerState.PointClamp.Filter, texture.PreferredSamplerState.Filter);
            Assert.Equal(SamplerState.PointClamp.AddressU, texture.PreferredSamplerState.AddressU);
            Assert.Equal(SamplerState.PointClamp.AddressV, texture.PreferredSamplerState.AddressV);
            Assert.Equal(SamplerState.PointClamp.AddressW, texture.PreferredSamplerState.AddressW);
        }
        finally
        {
            EditorAssetCatalogService.Clear();
            EngineEnvironment.ProjectPath = previousProjectPath;
            Directory.Delete(outputDirectory, recursive: true);
            Directory.Delete(Path.GetDirectoryName(sourcePngPath)!, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AlundraCasaEngineConverterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
