#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaEngine.Framework.AI.Navigation;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2c1 T1 (docs/plan-e20-heros.md section 1.24.5, phase A, (l1)): the 1 244 goldens of C4 (<c>0x80066984</c>, <c>docs/plan-e20-annexe/e20-g2c/disc/c4_goldens.json</c>: the real code of <c>ALUN_CD.EXE</c> (France) on the MIPS machine, 20 000 vectors
/// of the model equal to it, the sound and the effect journalled), played through <see cref="LiftableAi.C4Tick"/> by class of outcome: carried and held, carried and released, free without effect, free with the animation, with the sound 0x18, with the effect 9 (the bounce), with
/// the status 3 (the slide on a band). Each vector gives the words the handler READS (the entity, the four corner words behind the four pointers) and the words it WRITES: the entity must end with exactly the words of the golden, and the journal (sound, effect 9) must be equal.
/// The codes of the throw that the table of the binary does not give (4, 5, -2: the binary reads the words after the table, an address) are never written by the hero: the DLL does not release for them, like C23, a deviation pinned apart.
/// </summary>
public sealed class LiftableC4GoldenTests
{
    /// <summary>The address of the first tile word of the machine's run (<c>TILES</c> of <c>c4_full.py</c>): the four pointers of the entity point into the table of eight 16-bit words that starts there.</summary>
    private const uint Tiles = 0x801E0000;

    private sealed class SoundWorld : IEntityWorldContext
    {
        private readonly Recorder _recorder = new();

        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => Array.Empty<EntityScriptProxy>();
        public EntityScriptProxy? PlayerEntity => null;
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;

        public void DestroyEntity(EntityScriptProxy entity)
        {
        }

        public NavigationGrid2D? NavigationGrid => null;
        public ISoundPlayer? SoundPlayer => _recorder;

        public IReadOnlyList<int> Played => _recorder.Requests;

        private sealed class Recorder : ISoundPlayer
        {
            public List<int> Requests { get; } = new();

            public void PlaySfx(int sfxId) => Requests.Add(sfxId);

            public void StopSfx(int sfxId)
            {
            }

            public void RemixVoice(int sfxId, int left, int right)
            {
            }

            public void FlushFrameSounds()
            {
            }

            public void StopAllSfx()
            {
            }
        }
    }

    private sealed record Vector(JsonElement Json, string Class)
    {
        public uint In(uint offset) => unchecked((uint)Json.GetProperty("inp").GetProperty($"0x{offset:X}").GetInt64());

        public bool Written(uint offset, out uint value)
        {
            if (Json.GetProperty("out").TryGetProperty($"0x{offset:X}", out var element))
            {
                value = unchecked((uint)element.GetInt64());
                return true;
            }

            value = 0;
            return false;
        }

        public bool Carried => In(0x28) != 0;

        public int ThrowCode => unchecked((int)In(0x24));

        public IReadOnlyList<JsonElement> Journal => Json.GetProperty("journal").EnumerateArray().ToList();

        public uint[] Corners => Enumerable.Range(0, 4).Select(i => unchecked((uint)Json.GetProperty("tiles")[(int)((In((uint)(0x148 + 4 * i)) - Tiles) / 2)].GetInt64())).ToArray();
    }

    private static List<Vector> Load()
    {
        var text = File.ReadAllText(AnnexFiles.C4Annex("c4_goldens.json")).Replace("\r\n", "\n");
        using var document = JsonDocument.Parse(text);
        return document.RootElement.EnumerateArray().Select(e => new Vector(e.Clone(), Classify(e))).ToList();
    }

    private static string Classify(JsonElement e)
    {
        var inp = e.GetProperty("inp");
        var carried = inp.GetProperty("0x28").GetInt64() != 0;
        var code = unchecked((int)inp.GetProperty("0x24").GetInt64());
        var journal = e.GetProperty("journal").EnumerateArray().Select(j => j[0].GetString()).ToList();
        var written = e.GetProperty("out").EnumerateObject().Select(p => p.Name).ToHashSet();
        if (carried)
        {
            return code == 0 ? "carried-held" : code is 1 or 2 or 3 or -1 ? "carried-released" : "carried-code-outside-the-table";
        }

        if (journal.Contains("effect"))
        {
            return "free-effect-9";
        }

        if (journal.Contains("sound"))
        {
            return "free-sound-24";
        }

        if (written.Contains("0x10"))
        {
            return "free-status-3";
        }

        return written.Count == 0 ? "free-silent" : "free-animation";
    }

    private static (EntityScriptProxy Entity, EntityScriptProxy Hero) Build(Vector v)
    {
        var hero = CarryBench.Make("hero");
        var entity = CarryBench.Make("object");
        entity.Status = (EntityStatus)v.In(0x10);
        entity.Flags2 = unchecked((int)v.In(0x24));
        entity.SpriteType = unchecked((int)v.In(0x68));
        entity.Flags = v.In(0x6C);
        entity.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = unchecked((int)v.In(0x80));
        entity.TargetAnimationId = v.In(0x88);
        entity.TargetDirection = v.In(0x8C);
        entity.ForceZ = unchecked((int)v.In(0xB8));
        entity.PosX = unchecked((int)v.In(0x114));
        entity.PosY = unchecked((int)v.In(0x118));
        entity.PosZ = unchecked((int)v.In(0x11C));
        entity.ForceAdjusted = unchecked((int)v.In(0x13C));
        entity.IsOnGround = unchecked((int)v.In(0x144));
        entity.Slope_18c = unchecked((int)v.In(0x18C));
        entity.HitCounter = unchecked((int)v.In(0x220));
        entity.DelayOrAngleOrEntityId = unchecked((int)v.In(0x278));
        if (v.Carried)
        {
            entity.PlatformEntity = hero.LogicContextEntity;
            entity.RidingEntity = hero.LogicContextEntity;
            hero.CarriedEntity = entity.LogicContextEntity;
        }
        else if (v.Json.GetProperty("hero_carried_before").GetInt64() != 0)
        {
            hero.CarriedEntity = CarryBench.Make("something else").LogicContextEntity; // the hero carries another object: a free object must leave that link alone.
        }

        return (entity, hero);
    }

    /// <summary>Plays one vector and returns the differences with the golden (empty: equal).</summary>
    private static List<string> Play(Vector v)
    {
        var (entity, hero) = Build(v);
        var world = new SoundWorld();
        var effects = new List<(int X, int Y, int Z)>();
        var corners = v.Corners;
        LiftableAi.C4Tick(entity, world, _ => corners, (x, y, z) => effects.Add((x, y, z)));

        var diffs = new List<string>();

        void Check(string name, uint actual, uint offset)
        {
            var expected = v.Written(offset, out var written) ? written : v.In(offset);
            if (offset == 0x28)
            {
                expected = expected != 0 ? 1u : 0u; // the carrier is a pointer in the machine: only "is there one" is compared.
            }

            if (actual != expected)
            {
                diffs.Add($"{name} (0x{offset:X}): expected {expected}, got {actual}");
            }
        }

        Check("status", (uint)entity.Status, 0x10);
        Check("throw code", unchecked((uint)entity.Flags2), 0x24);
        Check("carrier", entity.PlatformEntity == null ? 0u : 1u, 0x28);
        Check("flags", entity.Flags, 0x6C);
        Check("animation", entity.TargetAnimationId, 0x88);
        Check("direction", entity.TargetDirection, 0x8C);
        Check("lock", unchecked((uint)entity.DelayOrAngleOrEntityId), 0x278);

        // the words the handler reads are not written (but the ones the golden lists).
        Check("force z", unchecked((uint)entity.ForceZ), 0xB8);
        Check("x", unchecked((uint)entity.PosX), 0x114);
        Check("y", unchecked((uint)entity.PosY), 0x118);
        Check("z", unchecked((uint)entity.PosZ), 0x11C);
        Check("adjusted", unchecked((uint)entity.ForceAdjusted), 0x13C);
        Check("ground", unchecked((uint)entity.IsOnGround), 0x144);
        Check("slope", unchecked((uint)entity.Slope_18c), 0x18C);
        Check("hit counter", unchecked((uint)entity.HitCounter), 0x220);

        var expectedLink = v.Json.GetProperty("hero_carried_after").GetInt64() != 0;
        if ((hero.CarriedEntity != null) != expectedLink)
        {
            diffs.Add($"hero link: expected {expectedLink}, got {hero.CarriedEntity != null}");
        }

        var expectedSounds = v.Journal.Where(j => j[0].GetString() == "sound").Select(j => j[1].GetInt32()).ToList();
        if (!expectedSounds.SequenceEqual(world.Played))
        {
            diffs.Add($"sounds: expected [{string.Join(",", expectedSounds)}], got [{string.Join(",", world.Played)}]");
        }

        var expectedEffects = v.Journal.Where(j => j[0].GetString() == "effect")
            .Select(j => (X: unchecked((int)j[4].GetInt64()), Y: unchecked((int)j[5].GetInt64()), Z: unchecked((int)j[6].GetInt64()))).ToList();
        if (!expectedEffects.SequenceEqual(effects))
        {
            diffs.Add($"effect 9: expected [{string.Join(",", expectedEffects)}], got [{string.Join(",", effects)}]");
        }

        if (v.Journal.Any(j => j[0].GetString() == "effect" && (j[1].GetInt32() != 0 || j[2].GetInt32() != 9 || j[3].GetInt32() != 0)))
        {
            diffs.Add("the golden's effect is not the effect 9 of the table 0 with animation 0");
        }

        return diffs;
    }

    private static void AssertClass(string className, int count)
    {
        var vectors = Load().Where(v => v.Class == className).ToList();
        Assert.Equal(count, vectors.Count);
        var failures = vectors.Select(v => (v, diffs: Play(v))).Where(p => p.diffs.Count > 0).Take(3).ToList();
        Assert.True(failures.Count == 0, string.Join(" | ", failures.Select(f => $"{f.v.Json.GetProperty("cls").GetString()}: {string.Join("; ", f.diffs)}")));
    }

    [Fact]
    public void TheGoldenFile_IsTheOneTheDiscoveryPinned_1244Vectors_AndEachClassHasItsCount()
    {
        var text = File.ReadAllText(AnnexFiles.C4Annex("c4_goldens.json")).Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(text))).ToLowerInvariant();
        Assert.StartsWith("59167009", sha);

        var vectors = Load();
        Assert.Equal(1244, vectors.Count);
        var counts = vectors.GroupBy(v => v.Class).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}").ToArray();
        Assert.Equal(
            new[]
            {
                "carried-code-outside-the-table=118",
                "carried-held=259",
                "carried-released=436",
                "free-animation=111",
                "free-effect-9=12",
                "free-silent=188",
                "free-sound-24=72",
                "free-status-3=48",
            },
            counts);
    }

    [Fact]
    public void Carried_Held_TheAnimation0_TheLinksAreKept() => AssertClass("carried-held", 259);

    [Fact]
    public void Carried_Released_TheTableOfTheBinary_TheLinksCleared_TheFlagsOnlyForTheDeathWithTheNativeCode2() => AssertClass("carried-released", 436);

    [Fact]
    public void Free_AnimationIsReset_WhenTheObjectLandsOrIsTouched_AndNoCornerIsOnABand() => AssertClass("free-animation", 111);

    [Fact]
    public void Free_TheSound24_OnTheLandingOrTheTouch_TheLockIsCleared() => AssertClass("free-sound-24", 72);

    [Fact]
    public void Free_TheBounce_TheEffect9_AtTheCurrentPositionPlus8px_TheDirectionTurnsBy0x10_TheLockIsSet() => AssertClass("free-effect-9", 12);

    [Fact]
    public void Free_BlockedOnTheGroundOnABand_TheStatus3_AndTheAnimation0() => AssertClass("free-status-3", 48);

    [Fact]
    public void Free_Silent_NothingChanges_NextToTheClassesThatChange()
    {
        AssertClass("free-silent", 188);

        // a silent vector is only silent next to the vectors of the same shape that DO something: a positive of each class changes its entity.
        var all = Load();
        foreach (var positive in new[] { "free-animation", "free-sound-24", "free-effect-9", "free-status-3", "carried-released" })
        {
            var v = all.First(x => x.Class == positive);
            var (entity, _) = Build(v);
            var before = (entity.Status, entity.TargetAnimationId, entity.TargetDirection, entity.DelayOrAngleOrEntityId, entity.Flags, entity.Flags2);
            var world = new SoundWorld();
            var effects = new List<(int, int, int)>();
            var corners = v.Corners;
            LiftableAi.C4Tick(entity, world, _ => corners, (x, y, z) => effects.Add((x, y, z)));
            var after = (entity.Status, entity.TargetAnimationId, entity.TargetDirection, entity.DelayOrAngleOrEntityId, entity.Flags, entity.Flags2);
            Assert.True(!before.Equals(after) || world.Played.Count > 0 || effects.Count > 0, $"the class {positive} did nothing");
        }
    }

    [Fact]
    public void Carried_ACodeOutsideTheTable_IsNeverReleased_TheDeviationFromTheBinaryThatTheHeroNeverWrites()
    {
        // the binary reads the words after the release table (an address): 4, 5 and -2 release it with a nonsense animation. The hero writes 1, 2, 3 and -1 only; the DLL (like C23) leaves the object carried for the others.
        var vectors = Load().Where(v => v.Class == "carried-code-outside-the-table").ToList();
        Assert.Equal(118, vectors.Count);

        // next to a code of the table, which does release (the positive of the negatives below).
        var listed = Load().First(v => v.Class == "carried-released");
        var (listedEntity, _) = Build(listed);
        var listedCorners = listed.Corners;
        LiftableAi.C4Tick(listedEntity, new SoundWorld(), _ => listedCorners, (x, y, z) => { });
        Assert.Null(listedEntity.PlatformEntity);

        foreach (var v in vectors)
        {
            var (entity, hero) = Build(v);
            var flags = entity.Flags;
            var animation = entity.TargetAnimationId;
            var corners = v.Corners;
            LiftableAi.C4Tick(entity, new SoundWorld(), _ => corners, (x, y, z) => { });
            Assert.True(
                entity.PlatformEntity != null && hero.CarriedEntity != null && entity.Flags2 == v.ThrowCode && entity.Flags == flags && entity.TargetAnimationId == animation,
                $"code {v.ThrowCode}: released");
        }
    }

    [Fact]
    public void Carried_TheReleaseClearsTheRidingRelation_NextToTheHeldObjectThatKeepsIt()
    {
        var all = Load();
        var released = all.First(v => v.Class == "carried-released");
        var held = all.First(v => v.Class == "carried-held");
        var (releasedEntity, _) = Build(released);
        var (heldEntity, _) = Build(held);
        var releasedCorners = released.Corners;
        var heldCorners = held.Corners;

        LiftableAi.C4Tick(releasedEntity, new SoundWorld(), _ => releasedCorners, (x, y, z) => { });
        LiftableAi.C4Tick(heldEntity, new SoundWorld(), _ => heldCorners, (x, y, z) => { });

        Assert.Null(releasedEntity.RidingEntity);
        Assert.NotNull(heldEntity.RidingEntity);
    }

    [Fact]
    public void TheCodeIsC4_TheSlotIsTheTick_TheSevenTypesAreTheOnesOfTheExport()
    {
        Assert.Equal(4, LiftableAi.C4Code);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 4) }, LiftableAi.C4Keys.ToArray());
        Assert.Equal(new[] { 290, 293, 363, 367, 368, 369, 373 }, LiftableAi.C4SpriteTypes.ToArray());

        // the export, read as the DLL reads it (never skipped: a missing file fails).
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var exported = new List<int>();
        foreach (var isMapPage in new[] { false, true })
        {
            for (var index = 0; index < 256; index++)
            {
                if (catalog.TryGetPrefab(isMapPage, index, out var prefab) && catalog.TryGet(prefab, out var header) && header.ProgramTick == LiftableAi.C4Code)
                {
                    exported.Add((isMapPage ? 0x100 : 0) + index);
                }
            }
        }

        Assert.Equal(LiftableAi.C4SpriteTypes.OrderBy(t => t).ToArray(), exported.OrderBy(t => t).ToArray());
    }
}
