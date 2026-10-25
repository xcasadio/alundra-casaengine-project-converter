#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Items;
using Alundra.Session;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.e0d T2 (docs/plan-e20-heros.md section 1.26.2, phase B, tests (b1) and (b2)): the handler F255 (<c>0x8007FC88</c>) of the chest, taken from the production registry, against the 244 vectors that the discovery made from the REAL code of the binary
/// (<c>docs/plan-e20-annexe/e20-e0d-disc/f255_vectors.tsv</c>, <c>f255_oracle.py</c>: 12 000 random vectors, 0 gap, 102 of 102 instructions, 15 mutants of the model killed): the four guards, the chest empty or already opened, the birth of the object
/// (type <c>contents + 0x1E</c>, the chest as the parent, Z + <c>0x100000</c>), <c>Block</c>, the music cut for a key object, the words written on the object, the message of the failed birth. (b2) <c>CanTake</c> (<c>0x80033D34</c>) is read from the answer
/// that F255 leaves on the object (<c>+0x284</c>) for every object of the table (1 to 97) and every stock around the cap: the cap of 9, 1, 1, 1, 1 for the herb, the large potion, the magic extract, the brew and the small potion
/// (<c>0x24</c> to <c>0x27</c> and <c>0x29</c>, the column 4 of <c>item-drop-properties.json</c>) and nothing else.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class ChestAiInteractVectorTests : IDisposable
{
    private static readonly ItemTables Tables = new(ProjectRootFinder.Find());

    private const uint ChestX = 0x220000;
    private const uint ChestY = 0x330000;
    private const int ChestZ = 0x440000;
    private const uint CollidableGravityInteract = 0x8180;

    private sealed record Row(string Guard, int Contents, int Chest88Before, bool SpawnOk, int? Stock, string Outcome, int Chest88After, int RiseForce, int Delay, int Kind, int State, int CanTake, int Fanfare, string[] Calls, int FlagsAfter);

    public ChestAiInteractVectorTests()
    {
        Reset();
        EtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc((0x48, "E48"), (0x49, "E49")));
    }

    public void Dispose() => Reset();

    private static void Reset()
    {
        WarpDirector.Instance.ResetForTests();
        EtcStringTable.ResetForTests();
    }

    private static IReadOnlyList<Row> ReadVectors()
    {
        var lines = File.ReadAllLines(AnnexFiles.ChestAnnex("f255_vectors.tsv"));
        var rows = new List<Row>();
        foreach (var line in lines.Skip(1).Where(l => l.Length != 0))
        {
            var c = line.Split('\t');
            int Number(string s) => s.StartsWith("0x", StringComparison.Ordinal) ? int.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : int.Parse(s, CultureInfo.InvariantCulture);
            rows.Add(new Row(c[0], Number(c[1]), Number(c[2]), c[3] == "ok", c[4].Length == 0 ? null : Number(c[4]), c[5], Number(c[6]), Number(c[7]), Number(c[8]), Number(c[9]), Number(c[10]), Number(c[11]), Number(c[12]),
                c[13].Length == 0 ? Array.Empty<string>() : c[13].Split('|'), Number(c[14])));
        }

        return rows;
    }

    private sealed class Rig
    {
        public EntityScriptProxy Chest { get; }

        public EntityScriptProxy Hero { get; } = new() { Hp = 5, HpMax = 10, LifePulledFromStats = true };

        public PickupHost Host { get; }

        public FakeDialogueDirector Dialogue { get; } = new();

        public RecordingBgmFadeDirector Bgm { get; }

        public GameState State { get; } = new();

        public NativeSpriteHandler Handler { get; }

        public Rig(int contents, int chest88, bool spawnOk)
        {
            Chest = new EntityScriptProxy { SpriteType = 30, ContentsItemId = (uint)contents, ContentsGameFlag = 1234, TargetAnimationId = (uint)chest88, PosX = (int)ChestX, PosY = (int)ChestY, PosZ = ChestZ, Flags = CollidableGravityInteract };
            Host = new PickupHost { Tables = Tables, Sound = new RecordingSoundPlayer(), Dialogue = Dialogue, Hero = Hero, TableFull = !spawnOk };
            Bgm = new RecordingBgmFadeDirector(Host, Dialogue);
            Host.BgmFadeDirector = Bgm;
            Assert.True(NativeSpriteRegistry.CreateForProduction().TryGet(ScriptHelper.ProgramFInteract, PickupAi.Code, 30, out var handler), "the production registry holds (F, 255) for the chest");
            Handler = handler!;
        }

        public void Run() => Handler(Chest, Host, State);
    }

    private static void RaiseTheMapChangeRequest(Rig rig)
    {
        var warp = WarpDirector.Instance;
        warp.AttachToWorld(new CasaEngine.Framework.Application.GameManager(null), soundPlayer: null, ProjectRootFinder.Find());
        warp.BeginDepartureFromChangeMapOpcode(desiredMapIndex: 14, posX: 100, posY: 100, posZ: 0, effectId: 0, sfxId: 0, rig.Hero, rig.State);
        Assert.True(warp.IsTransitionInProgress);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b1) the 244 vectors.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B1_TheTwoHundredAndFortyFourVectorsOfF255_AreFollowedWordForWordAndCallForCall()
    {
        var rows = ReadVectors();
        Assert.Equal(244, rows.Count);
        Assert.Equal((4, 240), (rows.Count(r => r.Guard != "none"), rows.Count(r => r.Guard == "none")));

        foreach (var row in rows)
        {
            var id = $"{row.Guard} contents {row.Contents:X} chest88 {row.Chest88Before} spawn {(row.SpawnOk ? "ok" : "fail")} stock {row.Stock}";
            var rig = new Rig(row.Contents, row.Chest88Before, row.SpawnOk);
            if (row.Stock is { } stock)
            {
                rig.State.NumberOfItems[row.Contents * 2 + 1] = (short)stock;
            }

            switch (row.Guard)
            {
                case "mask":
                    rig.State.PlayerControlFlags = GameState.PlayerControlBits.MenuOpen;
                    break;
                case "menu":
                    RaiseTheMapChangeRequest(rig);
                    break;
                case "carried":
                    rig.Hero.CarriedEntity = new Entity();
                    break;
                case "dialog":
                    rig.Dialogue.IsOpen = true;
                    break;
            }

            rig.Run();

            var spawnCalls = rig.Host.Spawns;
            var expectedSpawn = row.Calls.Any(c => c.StartsWith("spawn", StringComparison.Ordinal));
            Assert.True(expectedSpawn == (spawnCalls.Count == 1), $"{id}: the object is born exactly when the vector says so ({spawnCalls.Count} births)");
            Assert.True(row.Calls.Contains("block") == (rig.Host.Blocked.Count == 1), $"{id}: Block(chest)");
            Assert.True((row.Calls.Contains("bgm(0)") ? new[] { "LoadBgm(0)" } : Array.Empty<string>()).SequenceEqual(rig.Bgm.Calls), $"{id}: LoadBgm(0) for a key object only ({string.Join(",", rig.Bgm.Calls)})");
            var opened = row.Calls.FirstOrDefault(c => c.StartsWith("open", StringComparison.Ordinal));
            if (opened == null)
            {
                Assert.True(rig.Dialogue.Opens.Count == 0, $"{id}: no message");
            }
            else
            {
                var expectedText = opened.Contains("etc=0x48", StringComparison.Ordinal) ? "E48" : "E49";
                Assert.Equal(new[] { (expectedText, 0) }, rig.Dialogue.Opens.ToArray());
            }

            if (row.Guard != "none")
            {
                Assert.Equal((uint)row.Chest88Before, rig.Chest.TargetAnimationId);
                Assert.Empty(spawnCalls);
                Assert.Empty(rig.Host.Blocked);
                Assert.Empty(rig.Bgm.Calls);
                Assert.Empty(rig.Dialogue.Opens);
                continue;
            }

            Assert.Equal((uint)row.Chest88After, rig.Chest.TargetAnimationId);
            if (expectedSpawn)
            {
                var call = spawnCalls[0];
                Assert.Equal((rig.Chest, false, row.Contents + 0x1E, (int)ChestX, (int)ChestY, unchecked(ChestZ + 0x100000), 0), (call.Parent, call.IsMapPage, call.Index, call.X, call.Y, call.Z, call.Direction));
                Assert.True(row.Calls[0].StartsWith("spawn", StringComparison.Ordinal), $"{id}: the birth comes first");
            }

            if (row.Outcome != "opens")
            {
                continue;
            }

            var born = rig.Host.Spawned[0];
            Assert.Equal((row.RiseForce, row.Delay, (uint)row.Kind, row.State, (uint)row.CanTake, (uint)row.Fanfare), (born.ForceZ, born.DelayOrAngleOrEntityId, PickupAi.ReadWord274(born), born.ItemState, PickupAi.ReadWord284(born), Word288(born)));
            Assert.Equal((uint)row.FlagsAfter, born.Flags & CollidableGravityInteract); // the object loses Collidable and Gravity, whatever else it had.
            Assert.Null(born.BlockedByEntity);                                           // Block(chest) does not reach the object born after it (o + 0x20 = 0).
            Assert.Equal(new[] { rig.Chest }, rig.Host.Blocked.ToArray());
            if (row.Fanfare != 0)
            {
                Assert.Equal((1, 0), rig.Bgm.HostAtCall[0]); // after Block(chest), before any message: the music is cut when the object is already born and the world frozen.
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (b2) CanTake, the whole table.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary><c>+0x288</c>, the key-object mark: <see cref="EntityScriptProxy.AIValues"/> 4 (low half) and 5 (high half).</summary>
    private static uint Word288(EntityScriptProxy entity) => (ushort)entity.AIValues[4] | ((uint)(ushort)entity.AIValues[5] << 16);

    private static int CapOf(int item) => item switch { 0x24 => 9, 0x25 or 0x26 or 0x27 or 0x29 => 1, _ => 0 };

    [Fact]
    public void B2_CanTake_IsTheCapOfTheFiveStackedObjects_AndTrueForEveryOtherObject_ForTheWholeTable()
    {
        var checkedCases = 0;
        for (var item = 1; item < ItemTables.DropRecordCount; item++)
        {
            foreach (var stock in new[] { 0, 1, 2, 8, 9, 10 })
            {
                var rig = new Rig(item, 0, spawnOk: true);
                rig.State.NumberOfItems[item * 2 + 1] = (short)stock;

                rig.Run();

                var cap = CapOf(item);
                var expected = cap == 0 || stock < cap ? 1u : 0u;
                var born = Assert.Single(rig.Host.Spawned);
                Assert.True(expected == PickupAi.ReadWord284(born), $"CanTake({item:X2}) with {stock} in the bag: expected {expected}");
                checkedCases++;
            }
        }

        Assert.Equal(97 * 6, checkedCases);
    }

    [Fact]
    public void B2_TheCapsAreTheColumn4OfTheDropTable_NoOtherObjectHasOne()
    {
        // The five caps of CanTake are the octet 7 of the row of the object table, whose column 4 is 9, 1, 1, 1, 1 for 0x24 to 0x27 and 0x29; other rows hold a 1 too (the weapons, the keys) that CanTake never reads.
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectRootFinder.Find(), "Data", "item-drop-properties.json")));
        var rows = document.RootElement.EnumerateArray().Select(r => r.EnumerateArray().Select(c => c.GetInt32()).ToArray()).ToArray();

        Assert.Equal(98, rows.Length);
        Assert.Equal(new[] { 9, 1, 1, 1, 1 }, new[] { 0x24, 0x25, 0x26, 0x27, 0x29 }.Select(i => rows[i][3]).ToArray());
        Assert.Equal(new[] { 0x24, 0x25, 0x26, 0x27, 0x29 }.Select(CapOf), new[] { 9, 1, 1, 1, 1 });
    }
}
