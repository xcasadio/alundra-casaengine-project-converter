#nullable enable
using System;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Rendering.Effects;
using Alundra.Tests.Support;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>
/// E19.g G3a (docs/plan-e19-opcodes.md, section 1.2o.6, G3A-R1): the loader of the effect companions - the map one found through <c>Maps/world-index.json</c> like the
/// backdrops, the global one cached for the game. A map without companion has no effect and no warning; an unreadable companion has one warning and no effect.
/// </summary>
[Collection(EffectLoaderCollection.Name)]
public sealed class EffectBankLoaderTests : IDisposable
{
    private readonly string _projectPath = Path.Combine(Path.GetTempPath(), "EffectBankLoaderTests_" + Guid.NewGuid());

    public EffectBankLoaderTests()
    {
        EffectBankLoader.ResetForTests();
    }

    public void Dispose()
    {
        EffectBankLoader.ResetForTests();
        if (Directory.Exists(_projectPath))
        {
            Directory.Delete(_projectPath, recursive: true);
        }
    }

    private string MapFolder => Path.Combine(_projectPath, "Maps", "Zone", "Some Map-5");

    private void WriteIndex()
    {
        Directory.CreateDirectory(Path.Combine(_projectPath, "Maps"));
        File.WriteAllText(Path.Combine(_projectPath, "Maps", "world-index.json"), "{\"5\":\"Maps\\\\Zone\\\\Some Map-5\\\\Some Map-5.world\"}");
    }

    private void WriteCompanion(string content)
    {
        Directory.CreateDirectory(Path.Combine(MapFolder, "effects"));
        File.WriteAllText(Path.Combine(MapFolder, "effects", "Some Map-5.effects.json"), content);
    }

    private void WriteGlobal(string content)
    {
        Directory.CreateDirectory(Path.Combine(_projectPath, "Data", "effects"));
        File.WriteAllText(Path.Combine(_projectPath, "Data", "effects", "effects-global.json"), content);
    }

    private const string MapCompanion =
        "{\"MapIndex\":5,\"SheetTextureAssetId\":null,\"Records\":[{\"X1\":0,\"X2\":51,\"Y1\":0,\"Y2\":59,\"Flags\":192,\"Effect\":0,\"X\":20,\"Y\":18,\"Z\":2,\"Anim\":1}],"
        + "\"Effects\":[{\"Animations\":[{\"Frames\":[[10,0]],\"End\":\"Loop\"},{\"Frames\":[[10,1]],\"End\":\"Destroy\"}],"
        + "\"ImageSets\":[{\"Idsv\":3,\"Images\":[{\"U\":25,\"V\":0,\"W\":24,\"H\":31,\"C\":[-12,-40,12,-40,-12,-9,12,-9],\"Semi\":true,\"Abr\":1}]},{\"Idsv\":0,\"Images\":[]}]}]}";

    private const string GlobalCompanion =
        "{\"SheetTextureAssetId\":null,\"Effects\":[{\"Animations\":[{\"Frames\":[[30,0]],\"End\":\"Loop\"}],\"ImageSets\":[{\"Idsv\":20,\"Images\":[]}]}]}";

    private static string[] EffectWarnings(SaveGameDirectorTestSupport.LogCapture log) => log.Warnings.Where(w => w.Contains("EffectBankLoader")).ToArray();

    [Fact]
    public void NoWorldNameOrNoMapId_GivesAnEmptyBank_WithoutWarning()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();

        Assert.Empty(EffectBankLoader.Load(_projectPath, null).Records);
        Assert.Empty(EffectBankLoader.Load(_projectPath, "no id here").Records);
        Assert.Empty(EffectBankLoader.Load(null, "Some Map-5").Records);
        Assert.Empty(EffectWarnings(log));
    }

    [Fact]
    public void NoWorldIndex_OrAnIdAbsentFromIt_OrNoCompanionFile_GivesNoEffect_WithoutWarning()
    {
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();

        Assert.Empty(EffectBankLoader.Load(_projectPath, "Some Map-5").Records);

        WriteIndex();
        Assert.Empty(EffectBankLoader.Load(_projectPath, "Other Map-6").Records);
        Assert.Empty(EffectBankLoader.Load(_projectPath, "Some Map-5").Records);
        Assert.Empty(EffectWarnings(log));
    }

    [Fact]
    public void AWellFormedCompanion_GivesItsRecordsAndTables_AndTheGlobalTablesAreTheGlobalOnes()
    {
        WriteIndex();
        WriteCompanion(MapCompanion);
        WriteGlobal(GlobalCompanion);

        var bank = EffectBankLoader.Load(_projectPath, "Some Map-5");

        var record = Assert.Single(bank.Records);
        Assert.Equal((0, 51, 0, 59, 192, 0, 20, 18, 2, 1), (record.X1, record.X2, record.Y1, record.Y2, record.Flags, record.Effect, record.X, record.Y, record.Z, record.Anim));
        Assert.True(bank.TryGetTable(true, 0, out var map));
        Assert.Equal(2, map.Animations.Count);
        Assert.Equal("Destroy", map.Animations[1].End);
        Assert.Equal(new[] { 10, 1 }, map.Animations[1].Frames[0]);
        Assert.Equal(3, map.ImageSets[0].Idsv);
        var image = Assert.Single(map.ImageSets[0].Images);
        Assert.Equal((25, 0, 24, 31, true, 1), (image.U, image.V, image.W, image.H, image.Semi, image.Abr));
        Assert.Equal(new[] { -12, -40, 12, -40, -12, -9, 12, -9 }, image.C);
        Assert.True(bank.TryGetTable(false, 0, out var global));
        Assert.Equal(20, global.ImageSets[0].Idsv);
        Assert.False(bank.TryGetTable(true, 1, out _));
        Assert.False(bank.TryGetTable(false, -1, out _));
    }

    [Fact]
    public void AnUnreadableCompanion_IsOneWarning_AndNoEffect()
    {
        WriteIndex();
        WriteCompanion("{ this is not json");
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();

        var bank = EffectBankLoader.Load(_projectPath, "Some Map-5");

        Assert.Empty(bank.Records);
        Assert.False(bank.TryGetTable(true, 0, out _));
        Assert.Single(EffectWarnings(log));
    }

    [Fact]
    public void TheGlobalCompanion_IsReadOncePerGame_ThenServedFromTheCache()
    {
        WriteIndex();
        WriteCompanion(MapCompanion);
        WriteGlobal(GlobalCompanion);
        Assert.True(EffectBankLoader.Load(_projectPath, "Some Map-5").TryGetTable(false, 0, out var first));
        Assert.Equal(20, first.ImageSets[0].Idsv);

        WriteGlobal(GlobalCompanion.Replace("\"Idsv\":20", "\"Idsv\":21"));
        Assert.True(EffectBankLoader.Load(_projectPath, "Some Map-5").TryGetTable(false, 0, out var second));
        Assert.Same(first, second);

        EffectBankLoader.ResetForTests();
        Assert.True(EffectBankLoader.Load(_projectPath, "Some Map-5").TryGetTable(false, 0, out var third));
        Assert.Equal(21, third.ImageSets[0].Idsv);
    }

    [Fact]
    public void AnUnreadableGlobalCompanion_IsOneWarning_AndTheMapStillLoads()
    {
        WriteIndex();
        WriteCompanion(MapCompanion);
        WriteGlobal("not json at all");
        using var log = SaveGameDirectorTestSupport.LogCapture.Install();

        var first = EffectBankLoader.Load(_projectPath, "Some Map-5");
        var second = EffectBankLoader.Load(_projectPath, "Some Map-5");

        Assert.Single(first.Records);
        Assert.False(first.TryGetTable(false, 0, out _));
        Assert.Single(second.Records);
        Assert.Single(EffectWarnings(log));
    }

    [Fact]
    public void TheRealExport_LoadsTheCompanionsOfMaps163And391_AndTheGlobalTables()
    {
        var root = SaveGameDirectorTestSupport.FindProjectRoot();
        var rays = EffectBankLoader.Load(root, EffectGolden.WorldNameOf(root, 163));
        Assert.Equal(4, rays.Records.Count);
        Assert.All(rays.Records, r => Assert.Equal(0xC0, r.Flags));
        Assert.True(rays.TryGetTable(true, 0, out var table));
        Assert.Equal(3, table.Animations.Count);
        Assert.True(rays.TryGetTable(false, 28, out _));
        Assert.False(rays.TryGetTable(false, 29, out _));

        var ship = EffectBankLoader.Load(root, EffectGolden.WorldNameOf(root, 391));
        Assert.Equal(5, ship.Records.Count(r => (r.Flags & 0x40) != 0));
    }
}
