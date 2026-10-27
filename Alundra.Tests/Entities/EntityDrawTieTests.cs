#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Entities;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8, D-G1D-1 to D-G1D-5; (k1) and (p1)): the pure rule of the draw order of an object in the hands of the hero or thrown by him
/// (<see cref="EntityDepthKey.ResolveDrawTie"/>), the anchor it is written through (<see cref="EntityDepthKey.AnchorY"/>) and the constant <see cref="EntityDepthKey.HeldDrawOffset"/> (K) against the export.
/// </summary>
public sealed class EntityDrawTieTests
{
    private const int K = EntityDepthKey.HeldDrawOffset;

    // (k1) a held object: the coordinate of the carrier and +K, whatever the keys are.
    [Fact]
    public void AHeldObject_TakesTheCoordinateOfItsCarrier_AndPlusK()
    {
        var tie = EntityDepthKey.ResolveDrawTie(EntityDepthKey.DrawRole.Carried, 20600, 0x00D10020u, 0x00D20000u);

        Assert.Equal(20600, tie.Coordinate);
        Assert.Equal(K, tie.LocalOffset);
        Assert.True(K > 0);
    }

    // (k1) a thrown object: the coordinate of the thrower plus 100 per pixel of difference of the high halves of the two keys; no offset when they differ.
    [Theory]
    [InlineData(0x00C70024u, 0x00C50000u, 19000 + 200)] // the throw_up row of tick 50: 0xC7 against 0xC5, two pixels in front
    [InlineData(0x00C30000u, 0x00C50000u, 19000 - 200)] // an object two pixels behind
    [InlineData(0x00C50024u, 0x00C50000u, 19000)]       // the same pixel (the offset below decides)
    public void AThrownObject_IsDeltaPixelsFromItsThrower_InHundredths(uint objectKey, uint throwerKey, int expected)
    {
        var tie = EntityDepthKey.ResolveDrawTie(EntityDepthKey.DrawRole.Thrown, 19000, objectKey, throwerKey);

        Assert.Equal(expected, tie.Coordinate);
        if (objectKey >> 16 != throwerKey >> 16)
        {
            Assert.Equal(0, tie.LocalOffset);
        }
    }

    // (k1) at the same pixel the sign of K follows the keys: the object above the thrower's key gets +K, below it -K.
    [Theory]
    [InlineData(0x00CD0020u, 0x00CD0000u, +1)]
    [InlineData(0x00CD0000u, 0x00CD0020u, -1)]
    public void AThrownObjectAtTheSamePixel_GetsPlusKOrMinusK_AsItsKeyIsAboveOrBelow(uint objectKey, uint throwerKey, int sign)
    {
        var tie = EntityDepthKey.ResolveDrawTie(EntityDepthKey.DrawRole.Thrown, 19800, objectKey, throwerKey);

        Assert.Equal(19800, tie.Coordinate);
        Assert.Equal(sign * K, tie.LocalOffset);
    }

    // (k1, revision 1) the two keys exactly equal (a landed object of throw_probe, 00CD0000 on both sides): the binary does not order them, the offset is 0 and the coordinate the thrower's.
    [Fact]
    public void AThrownObjectWithTheSameKeyAsItsThrower_GetsNoOffset()
    {
        var tie = EntityDepthKey.ResolveDrawTie(EntityDepthKey.DrawRole.Thrown, 19800, 0x00CD0000u, 0x00CD0000u);

        Assert.Equal(19800, tie.Coordinate);
        Assert.Equal(0, tie.LocalOffset);
    }

    // (k1) neither held nor thrown: nothing is written.
    [Fact]
    public void AnEntityNeitherHeldNorThrown_HasNoAnchorAndNoOffset()
    {
        var tie = EntityDepthKey.ResolveDrawTie(EntityDepthKey.DrawRole.Plain, 20600, 0x00D10020u, 0x00D20000u);

        Assert.Null(tie.Coordinate);
        Assert.Equal(0, tie.LocalOffset);
    }

    // The anchor puts the sprite at EXACTLY the wanted coordinate through the engine's own computation, on fractional heights too.
    [Fact]
    public void TheAnchor_MakesTheEngineSortCoordinateExactlyTheWantedOne()
    {
        var depth = new DepthSortable2DComponent();
        var checkedCount = 0;
        for (var renderY = -300f; renderY <= 0f; renderY += 0.0625f * 7)
        {
            foreach (var wanted in new[] { 0, 6800, 17400, 20609, 20600 + 200, 12345, -100 })
            {
                depth.SortAnchorLocal = new Vector2(0f, EntityDepthKey.AnchorY(renderY, wanted));
                Assert.Equal(wanted, depth.BuildSortKey(new Vector3(100f, renderY, 0f)).SortCoordinate);
                checkedCount++;
            }
        }

        Assert.True(checkedCount > 4000);
    }

    // (p1) K against the export: above every DrawOrder of the pieces of the hero and of the lifted objects, and the smallest such constant that is also at least 14.
    [Fact]
    public void K_IsAboveEveryDrawOrderOfTheHeroAndOfTheLiftableObjects_OfTheExport()
    {
        var root = ProjectRootFinder.Find();
        var hero = MaxDrawOrder(Path.Combine(root, "Entities", "Alundra"), "bankalundra_0_anim*.anim2d");
        var liftables = LiftableBankKeys(root);
        var objects = liftables.Select(bank => MaxDrawOrder(Path.Combine(root, "Entities"), "bank" + bank + "_anim*.anim2d", recursive: true)).Max();

        Assert.Equal(13, hero);
        Assert.Equal(17, liftables.Count);
        Assert.Equal(6, objects);
        Assert.True(K > hero, $"K {K} must be above the hero's {hero}");
        Assert.True(K > objects, $"K {K} must be above the objects' {objects}");
        Assert.Equal(Math.Max(14, objects + 1), K);
    }

    private static List<string> LiftableBankKeys(string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Data", "sprite-records.json")));
        var banks = new List<string>();
        foreach (var record in document.RootElement.EnumerateObject())
        {
            var value = record.Value;
            var flags = value.GetProperty("MoreFlags").GetInt32() | (value.GetProperty("CanPickup").GetInt32() << 8) | (value.GetProperty("FlagsPortraitShadowType").GetInt32() << 16);
            var bank = value.GetProperty("BankKey").GetString()!;
            if ((flags & 0x600) >> 9 != 0 && !banks.Contains(bank))
            {
                banks.Add(bank);
            }
        }

        return banks;
    }

    private static int MaxDrawOrder(string folder, string pattern, bool recursive = false)
    {
        var files = Directory.GetFiles(folder, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(files);
        var max = -1;
        foreach (var file in files)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    max = Math.Max(max, part.TryGetProperty("default_draw_order", out var order) ? order.GetInt32() : 0);
                }
            }

            if (document.RootElement.TryGetProperty("tracks", out var tracks))
            {
                foreach (var track in tracks.EnumerateArray())
                {
                    foreach (var member in track.EnumerateObject())
                    {
                        if (member.Name.EndsWith("_keyframes", StringComparison.Ordinal) && member.Name.Contains("draw", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var keyframe in member.Value.EnumerateArray())
                            {
                                max = Math.Max(max, keyframe.GetProperty("value").GetInt32());
                            }
                        }
                    }
                }
            }
        }

        return max;
    }
}
