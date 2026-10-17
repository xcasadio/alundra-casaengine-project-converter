#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Enemies;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Rendering.Effects;

namespace Alundra.Tests;

/// <summary>
/// E14.a1b T4 (docs/plan-e14-ia-native.md section 1.3b): the Beannoid's A1, C1 and D1 (registry, type 335) and E1 (<c>DestroyEntity(e, -1)</c>) against the REAL code of <c>ALUN_CD.EXE</c> (France) run in the E14.a1 oracle
/// (<c>docs/plan-e14-annexe/e14a/oracle-a1/out</c>): the 2,052 single-call vectors (A1 3, C1 1,185, D1 720, E1 144) and the 3,140 calls of the scenarios BN-* and ARENA-* (A1 12, C1 3,102, D1 22, E1 4), each replayed alone from its
/// <c>before</c> to its <c>after</c>. For every call: the changed words, the generator's word, the sounds (the three handlers play none), the hero untouched. E1 is compared by the PROJECTION of the plan (compared C1 to C7,
/// excluded X1 to X6): the loot (<c>spawn_39F58</c>) and its sound are the named stub of E20.e0, the release of the attached effect (<c>+0x1B8</c>) is E20.e's, the carrier clearing E20.g's, the header pointer is replaced by
/// <see cref="AlundraEntityScriptProxy.BreakEffect"/>. Shares the generator's collection (it sets the word).
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraBeannoideOracleTests : IDisposable
{
    private const long Marker = 0x5A5A5A5A;
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly string[] ForceNames = { "tforcex_BC", "tforcey_C0", "forcex_C4", "forcey_C8", "fstepx_D4", "fstepy_D8" };
    private static readonly Lazy<AlundraEffectBank> Bank = new(() => EffectGolden.BankOf(Root, 14));
    private readonly ulong _seed = AlundraRandom.RandomSeed;

    public void Dispose() => AlundraRandom.RandomSeed = _seed;

    // ---------------------------------------------------------------------------------------------------------------------
    // The fields: the oracle's names tied to the DLL's (the table of the plan, section 1.3b)
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>A decoded field of the oracle and its DLL member: how to read it (in the oracle's terms), how to write it, the width compared, and whether it is a pointer (compared as null / not null).</summary>
    private sealed record Field(Func<AlundraEntityScriptProxy, long> Get, Action<AlundraEntityScriptProxy, long> Set, long Mask = 0xFFFFFFFFL, bool Pointer = false);

    // The binary's Z (and floor, corner heights) is the DLL's plus one raw unit (ADR-0026): H(v) = v - 1.
    private static int H(long v) => unchecked((int)v - 1);

    private static readonly Dictionary<string, Field> Fields = new(StringComparer.Ordinal)
    {
        ["status"] = new(e => (int)e.Status, (e, v) => e.Status = (EntityStatus)(int)v),
        ["hp"] = new(e => e.Hp, (e, v) => e.Hp = unchecked((int)v)),
        ["contents_3C"] = new(e => e.ContentsItemId, (e, v) => e.ContentsItemId = unchecked((uint)v)),
        ["flags6C"] = new(e => e.Flags, (e, v) => e.Flags = unchecked((uint)v)),
        ["anim_target88"] = new(e => e.TargetAnimationId, (e, v) => e.TargetAnimationId = unchecked((uint)v)),
        ["dir_target8C"] = new(e => e.TargetDirection, (e, v) => e.TargetDirection = unchecked((uint)v)),
        ["animdir98"] = new(e => e.BoxDirection, (e, v) => e.BoxDirection = unchecked((int)v)),
        ["anim_end_AC"] = new(e => e.ForceResetAnimationFlag, (e, v) => e.ForceResetAnimationFlag = unchecked((int)v)),
        ["posx"] = new(e => e.PosX, (e, v) => e.PosX = unchecked((int)v)),
        ["posy"] = new(e => e.PosY, (e, v) => e.PosY = unchecked((int)v)),
        ["posz"] = new(e => unchecked(e.PosZ + 1), (e, v) => e.PosZ = H(v)),
        ["tilex_120"] = new(e => e.TileX, (e, v) => e.TileX = unchecked((int)v)),
        ["tiley_124"] = new(e => e.TileY, (e, v) => e.TileY = unchecked((int)v)),
        ["floor_134"] = new(e => unchecked(e.FloorHeight + 1), (e, v) => e.FloorHeight = H(v)),
        ["forceadj_13C"] = new(e => e.ForceAdjusted, (e, v) => e.ForceAdjusted = unchecked((int)v)),
        ["onground_144"] = new(e => e.IsOnGround, (e, v) => e.IsOnGround = unchecked((int)v)),
        ["mh0_158"] = new(e => unchecked(e.MapHeights[0] + 1), (e, v) => e.MapHeights[0] = H(v)),
        ["mh1_15C"] = new(e => unchecked(e.MapHeights[1] + 1), (e, v) => e.MapHeights[1] = H(v)),
        ["mh2_160"] = new(e => unchecked(e.MapHeights[2] + 1), (e, v) => e.MapHeights[2] = H(v)),
        ["mh3_164"] = new(e => unchecked(e.MapHeights[3] + 1), (e, v) => e.MapHeights[3] = H(v)),
        ["b0_274"] = new(e => e.Bytes[0], (e, v) => e.Bytes[0] = unchecked((byte)v), 0xFF),
        ["b1_275"] = new(e => e.Bytes[1], (e, v) => e.Bytes[1] = unchecked((byte)v), 0xFF),
        ["b3_277"] = new(e => e.Bytes[3], (e, v) => e.Bytes[3] = unchecked((byte)v), 0xFF),
        ["ai0_280"] = new(e => e.AIValues[0], (e, v) => e.AIValues[0] = unchecked((short)v), 0xFFFF),
        ["ai1_282"] = new(e => e.AIValues[1], (e, v) => e.AIValues[1] = unchecked((short)v), 0xFFFF),
        ["tforcex_BC"] = new(e => e.TargetForceX, (e, v) => e.TargetForceX = unchecked((int)v)),
        ["tforcey_C0"] = new(e => e.TargetForceY, (e, v) => e.TargetForceY = unchecked((int)v)),
        ["forcex_C4"] = new(e => e.ForceX, (e, v) => e.ForceX = unchecked((int)v)),
        ["forcey_C8"] = new(e => e.ForceY, (e, v) => e.ForceY = unchecked((int)v)),
        ["fstepx_D4"] = new(e => e.ForceStepX, (e, v) => e.ForceStepX = unchecked((int)v)),
        ["fstepy_D8"] = new(e => e.ForceStepY, (e, v) => e.ForceStepY = unchecked((int)v)),
        ["letter_228"] = new(e => e.EventTrigger, (e, v) => e.EventTrigger = unchecked((int)v)),
        ["platform_28"] = new(e => e.PlatformEntity == null ? 0 : 1, (e, v) => e.PlatformEntity = v != 0 ? new Entity { Name = "platform" } : null, Pointer: true),
        ["touching_224"] = new(e => e.TouchingEntity == null ? 0 : 1, (_, _) => { }, Pointer: true),
    };

    /// <summary>The words of the entity that are not fields of the oracle's 27 and have a DLL member: marked on entry, compared unchanged (the marker proves no handler touches them).</summary>
    private static readonly (string Name, Action<AlundraEntityScriptProxy, long> Set, Func<AlundraEntityScriptProxy, long> Get)[] UntouchedWords =
    {
        ("forcez_B8", (e, v) => e.ForceZ = unchecked((int)v), e => e.ForceZ),
        ("prevadjx_CC", (e, v) => e.PreviousAdjustedForceX = unchecked((int)v), e => e.PreviousAdjustedForceX),
        ("prevadjy_D0", (e, v) => e.PreviousAdjustedForceY = unchecked((int)v), e => e.PreviousAdjustedForceY),
        ("adjx_DC", (e, v) => e.AdjustedForceX = unchecked((int)v), e => e.AdjustedForceX),
    };

    private static long Normalize(Field field, long value) => field.Pointer ? (value != 0 ? 1 : 0) : value & field.Mask;

    // ---------------------------------------------------------------------------------------------------------------------
    // The montage
    // ---------------------------------------------------------------------------------------------------------------------

    private sealed class OracleContext : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public AlundraEquipmentRecords? EquipmentRecords { get; set; }
        public uint HeroAnimationLatch { get; set; }
    }

    private static AlundraEquipmentRecords? _kit;

    /// <summary>The new-game kit as the oracle's D-calls had it: armour item 17 without attack, weapon item 1 with the attack (1, 1), no accessory (<c>kit.meta.equipment</c> of the E20.c1b oracle K2.json, checked by <see cref="TheKit_EqualsTheEquipmentOfTheOracle"/>).</summary>
    private static AlundraEquipmentRecords Kit()
    {
        if (_kit != null)
        {
            return _kit;
        }

        var state = new AlundraGameState();
        var items = AlundraItemTables.GetOrCreate(ProjectRootFinder.Find());
        AlundraPlayerManager.InitializeNewGameInventory(state, items);
        var records = new AlundraEquipmentRecords();
        AlundraPlayerManager.FillEquipmentRecords(state, items, AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()), 0, records);
        return _kit = records;
    }

    private static AlundraBalanceRecord Sheet() => AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()).Select(AlundraBeannoideAi.SpriteType, 0)!;

    private static (Entity Entity, AlundraEntityScriptProxy Proxy) NewHero(IReadOnlyDictionary<string, long> words, long[]? attack)
    {
        var entity = new Entity { Name = "hero", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.PosX = unchecked((int)words["posx"]);
        proxy.PosY = unchecked((int)words["posy"]);
        proxy.TileX = unchecked((int)words["tilex_120"]);
        proxy.TileY = unchecked((int)words["tiley_124"]);
        proxy.FloorHeight = H(words["floor_134"]);
        if (attack != null)
        {
            proxy.CurrentAttack = new AlundraBalanceAttack((int)attack[0], (int)attack[1]);
        }

        return (entity, proxy);
    }

    /// <summary>The seven hero fields of a scenario call under the names of the vectors.</summary>
    private static IReadOnlyDictionary<string, long> HeroOf(IReadOnlyDictionary<string, long> hero)
        => new Dictionary<string, long> { ["posx"] = hero["posx"], ["posy"] = hero["posy"], ["tilex_120"] = hero["tilex"], ["tiley_124"] = hero["tiley"], ["floor_134"] = hero["floor"] };

    private static string HeroState(AlundraEntityScriptProxy hero) => $"{hero.PosX} {hero.PosY} {hero.TileX} {hero.TileY} {hero.FloorHeight} {hero.CurrentAttack?.Attribute} {hero.CurrentAttack?.Power}";

    /// <summary>The entity of a call: every field of <paramref name="before"/> the DLL has, the forces as given (or marked), the words nobody touches marked, the registry's keys (type 335, the slot, the code).</summary>
    private static AlundraEntityScriptProxy NewBeannoid(IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> forceBefore, int letter, Entity heroEntity, int status)
    {
        var entity = new AlundraEntityScriptProxy { SpriteType = AlundraBeannoideAi.SpriteType, Status = (EntityStatus)status, BreakEffect = 8 };
        foreach (var (name, value) in before)
        {
            if (Fields.TryGetValue(name, out var field))
            {
                field.Set(entity, value);
            }
        }

        foreach (var name in ForceNames.Where(n => !before.ContainsKey(n)))
        {
            Fields[name].Set(entity, forceBefore.TryGetValue(name, out var v) ? v : Marker);
        }

        foreach (var (_, set, _) in UntouchedWords)
        {
            set(entity, Marker);
        }

        entity.BalanceRecord = Sheet();
        entity.TouchingEntity = before.GetValueOrDefault("touching_224") != 0 ? heroEntity : null;
        entity.ProgramIndexes[letter] = 0;
        entity.SpriteProgramIndexes[letter] = AlundraBeannoideAi.Code;
        entity.EventTrigger = letter;
        return entity;
    }

    /// <summary>The names to compare and their expected value: the entity's fields and forces before, overwritten by what changed.</summary>
    private static Dictionary<string, long> Expected(IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> forceBefore, IReadOnlyList<HandlerChange> changed)
    {
        var expected = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var (name, value) in before)
        {
            if (Fields.ContainsKey(name))
            {
                expected[name] = value;
            }
        }

        foreach (var name in ForceNames.Where(n => !before.ContainsKey(n)))
        {
            expected[name] = forceBefore.TryGetValue(name, out var v) ? v : Marker;
        }

        foreach (var change in changed)
        {
            Assert.True(AlundraEnemyHandlerOracle.BeannoideClosure.Contains(change.Name), $"the oracle changed '{change.Name}', outside the closure set of the Beannoid");
            if (Fields.ContainsKey(change.Name))
            {
                expected[change.Name] = change.After;
            }
        }

        return expected;
    }

    private static void AssertFields(AlundraEntityScriptProxy entity, IReadOnlyDictionary<string, long> expected, string where)
    {
        foreach (var (name, value) in expected)
        {
            var field = Fields[name];
            var actual = Normalize(field, field.Get(entity));
            var wanted = Normalize(field, value);
            Assert.True(actual == wanted, $"{where}: '{name}' is {actual}, the oracle has {wanted}");
        }

        foreach (var (name, _, get) in UntouchedWords)
        {
            Assert.True(get(entity) == Marker, $"{where}: the untouched word '{name}' changed to {get(entity)}");
        }
    }

    private static Dictionary<string, long> ForcesBefore(IReadOnlyList<HandlerChange> changed)
        => changed.Where(c => ForceNames.Contains(c.Name)).ToDictionary(c => c.Name, c => c.Before, StringComparer.Ordinal);

    // ---------------------------------------------------------------------------------------------------------------------
    // A1, C1, D1: the registry
    // ---------------------------------------------------------------------------------------------------------------------

    private static int LetterOf(string handler) => handler switch { "A1" => ScriptHelper.ProgramALoad, "C1" => ScriptHelper.ProgramCTick, "D1" => ScriptHelper.ProgramDTouch, _ => throw new InvalidOperationException(handler) };

    /// <summary>Runs one A1/C1/D1 call through the production runner (registry, the real <c>RunPickedEvent</c> path) and compares it with the oracle's.</summary>
    private void RunHandlerCall(
        string where, string handler, IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> heroWords, long[]? heroAttack, IReadOnlyList<HandlerChange> changed,
        IReadOnlyList<HandlerJournalEntry> journal, long latch, long rngIn, long rngOut)
    {
        var (heroEntity, hero) = NewHero(heroWords, heroAttack);
        var context = new OracleContext { PlayerEntity = hero, EquipmentRecords = Kit(), HeroAnimationLatch = unchecked((uint)latch) };
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), context);
        var letter = LetterOf(handler);
        var forcesBefore = ForcesBefore(changed);
        var entity = NewBeannoid(before, forcesBefore, letter, heroEntity, 2);
        var expected = Expected(before, forcesBefore, changed);
        expected.Remove("letter_228"); // the pick of the oracle's slot; the real RunPickedEvent clears it afterwards (only E1's letter is compared).
        var heroBefore = HeroState(hero);
        AlundraRandom.RandomSeed = unchecked((ulong)rngIn);

        entity.RunPickedEvent(runner);

        Assert.True(runner.NativeHandlerRunCount == 1, $"{where}: the registry did not run the handler ({runner.NativeHandlerRunCount} native runs)");
        Assert.True(unchecked((uint)AlundraRandom.RandomSeed) == unchecked((uint)rngOut), $"{where}: the generator is {unchecked((uint)AlundraRandom.RandomSeed)}, the oracle has {unchecked((uint)rngOut)}");
        Assert.True(journal.Count == 0, $"{where}: the oracle journal has {journal.Count} entries; the handlers play no sound");
        AssertFields(entity, expected, where);
        Assert.Equal(heroBefore, HeroState(hero));
    }

    [Theory]
    [InlineData("A1", 3)]
    [InlineData("C1", 1185)]
    [InlineData("D1", 720)]
    public void TheVectors_OfTheRegistryHandlers_HaveNoDifference(string handler, int count)
    {
        var vectors = AlundraEnemyHandlerOracle.Vectors("beannoide").Where(v => v.Handler == handler).ToList();
        Assert.Equal(count, vectors.Count);

        for (var i = 0; i < vectors.Count; i++)
        {
            var v = vectors[i];
            RunHandlerCall($"{handler} vector {i}", handler, v.Entity, v.Hero, handler == "D1" ? v.AttackerAttack : null, v.Changed, v.Journal, v.Latch, v.RngIn, v.RngOut);
        }
    }

    [Theory]
    [InlineData("A1", 12)]
    [InlineData("C1", 3102)]
    [InlineData("D1", 22)]
    public void TheCallsOfTheScenarios_OfTheRegistryHandlers_ReplayedOneByOne_HaveNoDifference(string handler, int count)
    {
        var done = 0;
        foreach (var scenario in AlundraEnemyHandlerOracle.Scenarios().Where(s => s.Name.StartsWith("BN-", StringComparison.Ordinal) || s.Name.StartsWith("ARENA-", StringComparison.Ordinal)))
        {
            foreach (var call in scenario.Calls.Where(c => c.Handler == handler))
            {
                var attack = handler == "D1" ? new long[] { 0x81, 1 } : null;
                if (handler == "D1")
                {
                    Assert.True(call.Hero["atk"] != 0, $"{scenario.Name} tick {call.Tick}: a D1 call without an attack");
                }

                RunHandlerCall($"{scenario.Name} tick {call.Tick} {handler}", handler, call.Before, HeroOf(call.Hero), attack, call.Changed, call.Journal, call.Latch, call.RngIn, call.RngOut);
                var after = Expected(call.Before, ForcesBefore(call.Changed), call.Changed);
                foreach (var (name, value) in after.Where(kv => call.After.ContainsKey(kv.Key) && !Fields[kv.Key].Pointer))
                {
                    Assert.True(Normalize(Fields[name], value) == Normalize(Fields[name], call.After[name]), $"{scenario.Name} tick {call.Tick}: before + changed != after for '{name}'");
                }

                done++;
            }
        }

        Assert.Equal(count, done);
    }

    [Fact]
    public void D1_AppliesTheLatchOfTheContext_ToTheEquipmentTerm()
    {
        // The attack (0x81, 1) adds the power of Attacks[latch + 1] of each equipment record: with the kit's weapon (1, 1) the damage is 2 at the latches -1, 0, 1, 3 and 16 alike (one attack in the record), and 1 without the bit 0x80.
        var hero = NewHero(new Dictionary<string, long> { ["posx"] = 0, ["posy"] = 0, ["tilex_120"] = 0, ["tiley_124"] = 0, ["floor_134"] = 1 }, new long[] { 0x81, 1 });
        var context = new OracleContext { PlayerEntity = hero.Proxy, EquipmentRecords = Kit(), HeroAnimationLatch = 16 };
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), context);
        var entity = NewBeannoid(new Dictionary<string, long> { ["hp"] = 9, ["touching_224"] = 1, ["posx"] = 1 << 20, ["posy"] = 0 }, new Dictionary<string, long>(), ScriptHelper.ProgramDTouch, hero.Entity, 2);

        entity.RunPickedEvent(runner);

        Assert.Equal(7, entity.Hp);
        Assert.Equal(5u, entity.TargetAnimationId);
        Assert.Equal(1, runner.NativeHandlerRunCount);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // E1: DestroyEntity(e, -1) by the projection of the plan
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Runs E1 through <c>RunPickedEvent</c> (the real path) on a world with a bank, <paramref name="occupied"/> slots taken, and compares C1 to C7.</summary>
    private void RunE1Call(string where, IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> heroWords, IReadOnlyList<HandlerChange> changed, IReadOnlyList<HandlerJournalEntry> journal,
        long rngIn, long? fx, int occupied, int status)
    {
        var (heroEntity, hero) = NewHero(heroWords, null);
        var world = new AlundraWorldProxy();
        world.EffectStage!.Load(Bank.Value);
        for (var i = 0; i < occupied; i++)
        {
            Assert.NotNull(world.EffectStage.Reservoir.Create(false, 9, 0, 0, 0, 0));
        }

        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), world);
        var letter = ScriptHelper.ProgramEDeactivate;
        var forcesBefore = ForcesBefore(changed);
        var entity = NewBeannoid(before, forcesBefore, letter, heroEntity, status);
        entity.BreakEffect = (int)(fx ?? 8);
        entity.ScriptHost = world;
        entity.ProgramIndexes[letter] = 0;
        entity.SpriteProgramIndexes[letter] = 1;
        var expected = Expected(before, forcesBefore, changed);
        expected["status"] = (long)EntityStatus.FlagToDestroy; // C1.
        expected["letter_228"] = -1; // C2.
        var platformBefore = entity.PlatformEntity;
        var heroBefore = HeroState(hero);
        AlundraRandom.RandomSeed = unchecked((ulong)rngIn);

        entity.RunPickedEvent(runner);

        Assert.True(unchecked((uint)AlundraRandom.RandomSeed) == unchecked((uint)rngIn), $"{where}: E1 drew a number (C3)"); // C3.
        AssertFields(entity, expected, where); // C4 (the contents, hp, flags, forces, the rest: unchanged).
        Assert.Same(platformBefore, entity.PlatformEntity);
        Assert.Equal(heroBefore, HeroState(hero)); // C6.
        Assert.Empty(((IEntityWorldContext)world).SpawnedEntities); // C7: no entity from the loot (the stub of E20.e0); the sound 84 and the spawn are the excluded entries X1 and X2.

        // C5: the break effect, from the journal's effect entries.
        var effects = journal.Where(j => j.Kind == "effect_3BE74").ToList();
        Assert.True(effects.Count <= 1, $"{where}: more than one effect in the oracle journal");
        var stage = world.EffectStage;
        stage.RunFrame(0, Array.Empty<bool>()); // applies the queue WITHOUT an update: the mode 1 can be read.
        var slots = stage.Reservoir.ActiveSlots.Where(s => s.Index >= occupied).ToList();
        if (effects.Count == 0)
        {
            Assert.True(slots.Count == 0, $"{where}: the oracle created no effect, the DLL created {slots.Count}");
            return;
        }

        var arguments = effects[0].Arguments;
        Assert.True(slots.Count == 1, $"{where}: the DLL created {slots.Count} effects, the oracle one");
        var slot = slots[0];
        Assert.True(slot.Index == occupied, $"{where}: the slot is {slot.Index}, the lowest free one is {occupied}");
        Assert.True(slot.Active && slot.RecordIndex == -1 && slot.TargetEffect == arguments[1] && slot.TargetMapTable == (arguments[0] != 0) && slot.TargetAnimation == arguments[2], $"{where}: the slot is not the oracle's effect");
        Assert.True(slot.Mode == 1 && slot.Anchor != null && slot.Anchor.Status == 4 && slot.DepthOffset == 1 && slot.OffsetX == 0 && slot.OffsetY == 0 && slot.OffsetZ == 0, $"{where}: not a mode 1 slot of the frozen anchor");
        Assert.True(slot.X == entity.PosX && slot.Y == entity.PosY && slot.Z == unchecked(entity.PosZ + 1), $"{where}: the slot is not at the entity");
        var key = AlundraEntityDepthKey.Compute(entity);

        stage.RunFrame(1, new[] { false });

        Assert.True(slot.Mode == 2 && slot.X == entity.PosX && slot.Y == entity.PosY && slot.Z == unchecked(entity.PosZ + 1) && slot.DepthKey == key + 1u, $"{where}: after its first update the slot is not frozen at the entity");
    }

    [Fact]
    public void E1_TheVectors_FollowTheProjectionOfThePlan()
    {
        var vectors = AlundraEnemyHandlerOracle.Vectors("beannoide").Where(v => v.Handler == "E1").ToList();
        Assert.Equal(144, vectors.Count);
        var effects = 0;
        var excluded = 0;
        for (var i = 0; i < vectors.Count; i++)
        {
            var v = vectors[i];
            RunE1Call($"E1 vector {i}", v.Entity, v.Hero, v.Changed, v.Journal, v.RngIn, v.InputFx, i % 2 == 0 ? 0 : 3, (int)v.Entity.GetValueOrDefault("status", 2));
            effects += v.Journal.Count(j => j.Kind == "effect_3BE74");
            excluded += v.Journal.Count(j => j.Kind != "effect_3BE74");
            Assert.True(v.Draws == 0 && v.RngIn == v.RngOut, $"E1 vector {i}: the oracle drew a number");
        }

        Assert.Equal(108, effects);
        Assert.Equal(160, excluded);
    }

    [Fact]
    public void E1_TheCallsOfTheScenarios_FollowTheProjectionOfThePlan()
    {
        var done = 0;
        foreach (var scenario in AlundraEnemyHandlerOracle.Scenarios().Where(s => s.Name.StartsWith("BN-", StringComparison.Ordinal) || s.Name.StartsWith("ARENA-", StringComparison.Ordinal)))
        {
            foreach (var call in scenario.Calls.Where(c => c.Handler == "E1"))
            {
                RunE1Call($"{scenario.Name} tick {call.Tick} E1", call.Before, HeroOf(call.Hero), call.Changed, call.Journal, call.RngIn, null, done % 2 == 0 ? 0 : 3, (int)call.Before["status"]);
                done++;
            }
        }

        Assert.Equal(4, done);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The reader and the montage themselves
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReader_CountsTheVectorsAndTheCallsOfThePlan()
    {
        var vectors = AlundraEnemyHandlerOracle.Vectors("beannoide");
        Assert.Equal(2052, vectors.Count);
        Assert.Equal(new[] { ("A1", 3), ("C1", 1185), ("D1", 720), ("E1", 144) }, vectors.GroupBy(v => v.Handler).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToArray());

        var scenarios = AlundraEnemyHandlerOracle.Scenarios();
        Assert.Equal(21, scenarios.Count);
        var calls = scenarios.Where(s => s.Name.StartsWith("BN-", StringComparison.Ordinal) || s.Name.StartsWith("ARENA-", StringComparison.Ordinal)).SelectMany(s => s.Calls).ToList();
        Assert.Equal(3140, calls.Count);
        Assert.Equal(new[] { ("A1", 12), ("C1", 3102), ("D1", 22), ("E1", 4) }, calls.GroupBy(c => c.Handler).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToArray());
        Assert.All(vectors.Concat(Array.Empty<HandlerVector>()), v => Assert.All(v.Changed, c => Assert.Contains(c.Name, AlundraEnemyHandlerOracle.BeannoideClosure)));
        Assert.All(calls, c => Assert.All(c.Changed, ch => Assert.Contains(ch.Name, AlundraEnemyHandlerOracle.BeannoideClosure)));
    }

    [Fact]
    public void TheKit_EqualsTheEquipmentOfTheOracle()
    {
        var path = FindUp(Path.Combine("docs", "plan-e20-annexe", "e20-c1b", "oracle", "out", "K2.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var expected = document.RootElement.GetProperty("kit").GetProperty("meta").GetProperty("equipment").GetString();
        var records = Kit();
        var actual = string.Join(", ", Enumerable.Range(0, AlundraEquipmentRecords.Count).Select(slot =>
            $"[{slot}] " + (records[slot] is { } r ? $"item {records.ItemIdOf(slot)} [{string.Join(", ", r.Attacks.Select(a => $"({a.Attribute}, {a.Power})"))}]" : "null")));

        Assert.Equal(expected, actual);
    }

    private static string FindUp(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
