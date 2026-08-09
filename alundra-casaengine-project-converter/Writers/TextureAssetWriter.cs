using CasaEngine.EditorServices;
using CasaEngine.Framework.Assets;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;

namespace AlundraCasaEngineProjectConverter.Writers;

/// <summary>
/// Shared texture import used by every phase that needs a PNG to become referenceable by a
/// CasaEngine asset (Phase 3 spritesheets, Phase 7 UI textures).
///
/// A CasaEngine texture is two catalog entries, not one:
///  - the raw PNG itself, so the asset pipeline can find the actual image bytes;
///  - a .texture wrapper document pointing at the raw entry and carrying the sampler state.
/// Consumers such as SpriteData.SpriteSheetAssetId reference the WRAPPER id, never the raw PNG id,
/// which is why EnsureTexture returns the wrapper's id.
///
/// PNGs are copied verbatim: the extractor already produced engine-ready RGBA images, so
/// re-encoding would only risk changing pixels.
/// </summary>
public static class TextureAssetWriter
{
    /// <summary>
    /// Copies <paramref name="sourcePngPath"/> into <paramref name="relativeDirectory"/> under the
    /// output root, registers the raw PNG and its .texture wrapper in the asset catalog, and
    /// returns the wrapper asset id. <paramref name="cache"/> is keyed by source path so repeated
    /// calls for the same PNG reuse the same pair of catalog entries; the caller owns the cache so
    /// it can also use its final Count as a "distinct textures imported" counter.
    /// </summary>
    public static Guid EnsureTexture(
        string sourcePngPath,
        string relativeDirectory,
        string outputDirectory,
        Dictionary<string, Guid> cache)
    {
        if (cache.TryGetValue(sourcePngPath, out var existingId))
        {
            return existingId;
        }

        if (!File.Exists(sourcePngPath))
        {
            throw new FileNotFoundException("Texture PNG not found.", sourcePngPath);
        }

        var fileName = Path.GetFileName(sourcePngPath);
        var targetDirectory = Path.Combine(outputDirectory, relativeDirectory);
        Directory.CreateDirectory(targetDirectory);
        File.Copy(sourcePngPath, Path.Combine(targetDirectory, fileName), overwrite: true);

        var rawRelativePath = Path.Combine(relativeDirectory, fileName);
        var rawName = Path.GetFileNameWithoutExtension(fileName);
        var rawAssetInfo = new AssetInfo(Guid.NewGuid()) { Name = rawName, FileName = rawRelativePath };
        EditorAssetCatalogService.Add(rawAssetInfo);

        var wrapperRelativePath = Path.ChangeExtension(rawRelativePath, ".texture");
        var wrapperId = Guid.NewGuid();
        var wrapperDocument = new JObject
        {
            ["id"] = wrapperId.ToString(),
            ["name"] = rawName,
            ["texture_asset_id"] = rawAssetInfo.Id.ToString(),
            ["sampler_state"] = SaveSamplerState(SamplerState.AnisotropicWrap),
        };
        EditorAssetWriterService.SaveDocument(wrapperRelativePath, wrapperDocument);
        EditorAssetCatalogService.Add(new AssetInfo(wrapperId) { Name = rawName, FileName = wrapperRelativePath });

        cache[sourcePngPath] = wrapperId;
        return wrapperId;
    }

    // Mirrors CasaEngine.EditorServices.EditorJsonSaveHelper.Save(this SamplerState, JObject),
    // which is internal to that assembly and so not callable from here. Texture.Load() reads
    // every one of these fields unconditionally (JsonHelper.GetSamplerState) - a wrapper missing
    // any of them throws inside AssetLoader<Texture>.LoadAsset, which swallows the exception and
    // returns null, surfacing only as "IAssetLoader can't load ...texture" in the editor.
    private static JObject SaveSamplerState(SamplerState samplerState)
    {
        var borderColorObject = new JObject
        {
            ["r"] = samplerState.BorderColor.R,
            ["g"] = samplerState.BorderColor.G,
            ["b"] = samplerState.BorderColor.B,
            ["a"] = samplerState.BorderColor.A,
        };

        return new JObject
        {
            ["texture_filter"] = samplerState.Filter.ToString(),
            ["address_u"] = samplerState.AddressU.ToString(),
            ["address_v"] = samplerState.AddressV.ToString(),
            ["address_w"] = samplerState.AddressW.ToString(),
            ["border_color"] = borderColorObject,
            ["max_anisotropy"] = samplerState.MaxAnisotropy,
            ["max_mip_level"] = samplerState.MaxMipLevel,
            ["mip_map_level_of_detail_bias"] = samplerState.MipMapLevelOfDetailBias,
            ["comparison_function"] = samplerState.ComparisonFunction.ToString(),
            ["filter_mode"] = samplerState.FilterMode.ToString(),
        };
    }
}
