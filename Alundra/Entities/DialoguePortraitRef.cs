#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Session;

namespace Alundra.Entities;

/// <summary>
/// The dialogue portrait of a speaker prefab (E19.f4a, parent ADR-0005 extended): the <c>.sprite</c> asset it is drawn from and its
/// true size in pixels (48 x 56, or 48 x 72 for the two tall portraits). Read from the optional <c>DialoguePortrait</c> field of
/// a <c>Data/sprite-records.json</c> entry, present exactly where the header's <c>FlagsPortraitShadowType</c> has bit 0x80.
/// </summary>
public readonly record struct DialoguePortraitRef(Guid SpriteAssetId, int Width, int Height);
