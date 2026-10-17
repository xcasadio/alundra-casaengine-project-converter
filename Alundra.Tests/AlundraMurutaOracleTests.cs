#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai;
using Alundra.Ai.Enemies;
using Alundra.Hero;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Rendering.Effects;
using Alundra.Audio;

namespace Alundra.Tests;

/// <summary>
/// E14.a1c T1 (docs/plan-e14-ia-native.md section 1.3c): the Muruta's A0, C10 and D9 (registry, type 347) and E1 (<c>DestroyEntity(e, -1)</c>) against the REAL code of <c>ALUN_CD.EXE</c> (France) run in the E14.a1 oracle
/// (<c>docs/plan-e14-annexe/e14a/oracle-a1/out</c>): the 4,913 single-call vectors (A0 6, C10 4,043, D9 720, E1 144) and the 3,675 calls of the scenarios MU-* (A0 16, C10 3,637, D9 15, E1 7), each replayed alone from its
/// <c>before</c> to its <c>after</c>. For every call: the changed words, the generator's word, the sounds (the claw sound <c>0xCD</c> of C10 is compared, in order), the hero untouched. E1 is compared by the projection of the plan
/// (section 1.3b). The montage is that of <see cref="AlundraBeannoideOracleTests"/> with the fields of the Muruta (<c>+0x98</c> <see cref="AlundraEntityScriptProxy.BoxDirection"/>, the four corner heights, <c>+0x274</c>, <c>+0x280</c>) and the
/// hero's frame box (the FIELD <see cref="AlundraEntityScriptProxy.HasFrameBox"/>) and current attack. Shares the generator's collection (it sets the word).
/// </summary>
[Collection(AlundraRandomStaticStateCollection.Name)]
public sealed class AlundraMurutaOracleTests : IDisposable
{
    private const long Marker = 0x5A5A5A5A;
    private static readonly string Root = SaveGameDirectorTestSupport.FindProjectRoot();
    private static readonly string[] ForceNames = { "tforcex_BC", "tforcey_C0", "forcex_C4", "forcey_C8", "fstepx_D4", "fstepy_D8" };
    private static readonly Lazy<AlundraEffectBank> Bank = new(() => EffectGolden.BankOf(Root, 14));
    private readonly ulong _seed = AlundraRandom.RandomSeed;

    public void Dispose() => AlundraRandom.RandomSeed = _seed;

    // ---------------------------------------------------------------------------------------------------------------------
    // The fields: the oracle's names tied to the DLL's (the table of the plan, sections 1.3b and 1.3c)
    // ---------------------------------------------------------------------------------------------------------------------

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

    /// <summary>The words of the entity that are not fields of the oracle's 27 and have a DLL member: marked on entry, compared unchanged.</summary>
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
        public IAlundraSoundPlayer? SoundPlayer { get; set; }
    }

    /// <summary>The sounds asked, in order (the oracle's <c>sound</c> journal entries).</summary>
    private sealed class SoundRecorder : IAlundraSoundPlayer
    {
        public List<int> Ids { get; } = new();

        public void PlaySfx(int sfxId) => Ids.Add(sfxId);

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

    private static AlundraEquipmentRecords? _kit;

    /// <summary>The new-game kit as the oracle's D-calls had it (armour item 17 without attack, weapon item 1 with the attack (1, 1)), checked against the oracle by <see cref="AlundraBeannoideOracleTests"/>.</summary>
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

    private static AlundraBalanceRecord Sheet() => AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()).Select(AlundraMurutaAi.SpriteType, 0)!;

    /// <summary>The hero: position, tile and floor; the frame box FIELD (<paramref name="box"/>) and the current attack (<paramref name="attack"/>, null for a null pointer).</summary>
    private static (Entity Entity, AlundraEntityScriptProxy Proxy) NewHero(IReadOnlyDictionary<string, long> words, long[]? attack, bool box)
    {
        var entity = new Entity { Name = "hero", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName! };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.PosX = unchecked((int)words["posx"]);
        proxy.PosY = unchecked((int)words["posy"]);
        proxy.TileX = unchecked((int)words["tilex_120"]);
        proxy.TileY = unchecked((int)words["tiley_124"]);
        proxy.FloorHeight = H(words["floor_134"]);
        proxy.HasFrameBox = box;
        if (attack != null)
        {
            proxy.CurrentAttack = new AlundraBalanceAttack((int)attack[0], (int)attack[1]);
        }

        return (entity, proxy);
    }

    /// <summary>The seven hero fields of a scenario call under the names of the vectors.</summary>
    private static IReadOnlyDictionary<string, long> HeroOf(IReadOnlyDictionary<string, long> hero)
        => new Dictionary<string, long> { ["posx"] = hero["posx"], ["posy"] = hero["posy"], ["tilex_120"] = hero["tilex"], ["tiley_124"] = hero["tiley"], ["floor_134"] = hero["floor"] };

    /// <summary>
    /// The hero's attack of a scenario call, from the pointer of the oracle's row: <c>0x80144229</c> is the attack <c>(0x81, 1)</c> (615 C10 calls, the hero strikes, the frame box is set); <c>0x80144209</c> (3,022 C10 calls) is never
    /// dereferenced by C10 (the frame box is clear and the short circuit stops before the attribute), so it is given the attribute 0x55, which would not be the oracle's if it were read.
    /// </summary>
    private static long[] ScenarioHeroAttack(HandlerCall call) => call.HeroAttackPointer switch
    {
        0x80144229 => new long[] { 0x81, 1 },
        0x80144209 => new long[] { 0x55, 1 },
        _ => throw new InvalidOperationException($"unknown hero attack pointer 0x{call.HeroAttackPointer:X}"),
    };

    private static string HeroState(AlundraEntityScriptProxy hero) => $"{hero.PosX} {hero.PosY} {hero.TileX} {hero.TileY} {hero.FloorHeight} {hero.HasFrameBox} {hero.CurrentAttack?.Attribute} {hero.CurrentAttack?.Power}";

    private static AlundraEntityScriptProxy NewMuruta(IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> forceBefore, int letter, int code, Entity heroEntity, int status)
    {
        var entity = new AlundraEntityScriptProxy { SpriteType = AlundraMurutaAi.SpriteType, Status = (EntityStatus)status, BreakEffect = 8 };
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
        entity.SpriteProgramIndexes[letter] = code;
        entity.EventTrigger = letter;
        return entity;
    }

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
            Assert.True(AlundraEnemyHandlerOracle.MurutaClosure.Contains(change.Name), $"the oracle changed '{change.Name}', outside the closure set of the Muruta");
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

    /// <summary>
    /// The force words as they were before the call: the <c>before</c> of those the oracle changed. The handlers stop an entity by zeroing the six words together (<see cref="AlundraEnemyHelpers.ZeroForces"/>); the oracle's junk
    /// can leave one of them at 0 already, which then is not listed as changed - when a changed force word went to 0, the unlisted ones were 0 before (and stay 0). Otherwise they keep the marker (a word nobody writes).
    /// </summary>
    private static Dictionary<string, long> ForcesBefore(IReadOnlyList<HandlerChange> changed)
    {
        var before = changed.Where(c => ForceNames.Contains(c.Name)).ToDictionary(c => c.Name, c => c.Before, StringComparer.Ordinal);
        if (changed.Any(c => ForceNames.Contains(c.Name) && c.After == 0))
        {
            foreach (var name in ForceNames.Where(n => !before.ContainsKey(n)))
            {
                before[name] = 0;
            }
        }

        return before;
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // A0, C10, D9: the registry
    // ---------------------------------------------------------------------------------------------------------------------

    private static (int Letter, int Code) KeyOf(string handler) => handler switch
    {
        "A0" => (ScriptHelper.ProgramALoad, AlundraMurutaAi.LoadCode),
        "C10" => (ScriptHelper.ProgramCTick, AlundraMurutaAi.TickCode),
        "D9" => (ScriptHelper.ProgramDTouch, AlundraMurutaAi.TouchCode),
        _ => throw new InvalidOperationException(handler),
    };

    /// <summary>Runs one A0/C10/D9 call through the production runner (registry, the real <c>RunPickedEvent</c> path) and compares it with the oracle's.</summary>
    private void RunHandlerCall(
        string where, string handler, IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> heroWords, long[]? heroAttack, bool heroBox, IReadOnlyList<HandlerChange> changed,
        IReadOnlyList<HandlerJournalEntry> journal, long latch, long rngIn, long rngOut)
    {
        var (heroEntity, hero) = NewHero(heroWords, heroAttack, heroBox);
        var sounds = new SoundRecorder();
        var context = new OracleContext { PlayerEntity = hero, EquipmentRecords = Kit(), HeroAnimationLatch = unchecked((uint)latch), SoundPlayer = sounds };
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), context);
        var (letter, code) = KeyOf(handler);
        var forcesBefore = ForcesBefore(changed);
        var entity = NewMuruta(before, forcesBefore, letter, code, heroEntity, 2);
        var expected = Expected(before, forcesBefore, changed);
        expected.Remove("letter_228"); // the pick of the oracle's slot; the real RunPickedEvent clears it afterwards (only E1's letter is compared).
        var heroBefore = HeroState(hero);
        AlundraRandom.RandomSeed = unchecked((ulong)rngIn);

        entity.RunPickedEvent(runner);

        Assert.True(runner.NativeHandlerRunCount == 1, $"{where}: the registry did not run the handler ({runner.NativeHandlerRunCount} native runs)");
        Assert.True(unchecked((uint)AlundraRandom.RandomSeed) == unchecked((uint)rngOut), $"{where}: the generator is {unchecked((uint)AlundraRandom.RandomSeed)}, the oracle has {unchecked((uint)rngOut)}");
        Assert.True(journal.All(j => j.Kind == "sound"), $"{where}: the oracle journal has an entry that is not a sound");
        Assert.True(
            journal.Select(j => (int)j.Arguments[0]).SequenceEqual(sounds.Ids),
            $"{where}: the sounds are [{string.Join(", ", sounds.Ids)}], the oracle has [{string.Join(", ", journal.Select(j => j.Arguments[0]))}]");
        AssertFields(entity, expected, where);
        Assert.Equal(heroBefore, HeroState(hero));
    }

    /// <summary>The hero's attack and box of a vector: the explicit pair when the pointer is not null, the box from <c>box_1D4</c>.</summary>
    private static (long[]? Attack, bool Box) HeroOf(HandlerVector v)
        => (v.Hero["curattack_1C8"] != 0 ? v.HeroAttack : null, v.Hero["box_1D4"] != 0);

    [Theory]
    [InlineData("A0", 6)]
    [InlineData("C10", 4043)]
    [InlineData("D9", 720)]
    public void TheVectors_OfTheRegistryHandlers_HaveNoDifference(string handler, int count)
    {
        var vectors = AlundraEnemyHandlerOracle.Vectors("muruta").Where(v => v.Handler == handler).ToList();
        Assert.Equal(count, vectors.Count);

        for (var i = 0; i < vectors.Count; i++)
        {
            var v = vectors[i];
            var (attack, box) = HeroOf(v);
            RunHandlerCall($"{handler} vector {i}", handler, v.Entity, v.Hero, handler == "D9" ? v.AttackerAttack : attack, box, v.Changed, v.Journal, v.Latch, v.RngIn, v.RngOut);
        }
    }

    [Theory]
    [InlineData("A0", 16)]
    [InlineData("C10", 3637)]
    [InlineData("D9", 15)]
    public void TheCallsOfTheScenarios_OfTheRegistryHandlers_ReplayedOneByOne_HaveNoDifference(string handler, int count)
    {
        var done = 0;
        foreach (var scenario in AlundraEnemyHandlerOracle.Scenarios().Where(s => s.Name.StartsWith("MU-", StringComparison.Ordinal)))
        {
            foreach (var call in scenario.Calls.Where(c => c.Handler == handler))
            {
                long[]? attack = null;
                var box = call.Hero["box"] != 0;
                if (handler == "D9")
                {
                    Assert.True(call.Hero["atk"] != 0, $"{scenario.Name} tick {call.Tick}: a D9 call without an attack");
                    attack = new long[] { 0x81, 1 };
                }
                else if (handler == "C10")
                {
                    attack = ScenarioHeroAttack(call);
                }

                RunHandlerCall($"{scenario.Name} tick {call.Tick} {handler}", handler, call.Before, HeroOf(call.Hero), attack, box, call.Changed, call.Journal, call.Latch, call.RngIn, call.RngOut);
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
    public void D9_AppliesTheLatchOfTheContext_ToTheEquipmentTerm_AndHitsForTwo()
    {
        // The attack (0x81, 1) adds the power of Attacks[latch + 1] of each equipment record: with the kit's weapon (1, 1) the damage of the Muruta (level 1, 3 HP) is 2 at the latch 16, as the oracle's hall shows.
        var hero = NewHero(new Dictionary<string, long> { ["posx"] = 0, ["posy"] = 0, ["tilex_120"] = 0, ["tiley_124"] = 0, ["floor_134"] = 1 }, new long[] { 0x81, 1 }, false);
        var context = new OracleContext { PlayerEntity = hero.Proxy, EquipmentRecords = Kit(), HeroAnimationLatch = 16 };
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), context);
        var entity = NewMuruta(new Dictionary<string, long> { ["hp"] = 3, ["touching_224"] = 1, ["posx"] = 1 << 20, ["posy"] = 0 }, new Dictionary<string, long>(), ScriptHelper.ProgramDTouch, AlundraMurutaAi.TouchCode, hero.Entity, 2);

        entity.RunPickedEvent(runner);

        Assert.Equal(1, entity.Hp);
        Assert.Equal(2u, entity.TargetAnimationId);
        Assert.Equal(0, entity.Bytes[3]);
        Assert.Equal(1, runner.NativeHandlerRunCount);
    }

    [Theory]
    [InlineData(0xFFFF, 1)]
    [InlineData(0xFFFE, 0)]
    [InlineData(0x1FFFF, 2)]
    [InlineData(-1, 0)]
    public void A0_TakesTheHighHalfOfTheZOfTheBinary_ThatIsThePosZOfTheDllPlusOne(int posZ, int expected)
    {
        var entity = new AlundraEntityScriptProxy { SpriteType = AlundraMurutaAi.SpriteType, PosZ = posZ };

        AlundraMurutaAi.Load(entity);

        Assert.Equal((short)expected, entity.AIValues[0]);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // E1: DestroyEntity(e, -1) by the projection of the plan (section 1.3b), for the Muruta
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Runs E1 through <c>RunPickedEvent</c> (the real path) on a world with a bank, <paramref name="occupied"/> slots taken, and compares C1 to C7.</summary>
    private void RunE1Call(string where, IReadOnlyDictionary<string, long> before, IReadOnlyDictionary<string, long> heroWords, IReadOnlyList<HandlerChange> changed, IReadOnlyList<HandlerJournalEntry> journal,
        long rngIn, long? fx, int occupied, int status)
    {
        var (heroEntity, hero) = NewHero(heroWords, null, false);
        var world = new AlundraWorldProxy();
        world.EffectStage!.Load(Bank.Value);
        for (var i = 0; i < occupied; i++)
        {
            Assert.NotNull(world.EffectStage.Reservoir.Create(false, 9, 0, 0, 0, 0));
        }

        var runner = new AlundraEventProgramRunner(null, new AlundraGameState(), world);
        var letter = ScriptHelper.ProgramEDeactivate;
        var forcesBefore = ForcesBefore(changed);
        var entity = NewMuruta(before, forcesBefore, letter, 1, heroEntity, status);
        entity.BreakEffect = (int)(fx ?? 8);
        entity.ScriptHost = world;
        var expected = Expected(before, forcesBefore, changed);
        expected["status"] = (long)EntityStatus.FlagToDestroy; // C1.
        expected["letter_228"] = -1; // C2.
        var platformBefore = entity.PlatformEntity;
        var heroBefore = HeroState(hero);
        AlundraRandom.RandomSeed = unchecked((ulong)rngIn);

        entity.RunPickedEvent(runner);

        Assert.True(unchecked((uint)AlundraRandom.RandomSeed) == unchecked((uint)rngIn), $"{where}: E1 drew a number (C3)");
        AssertFields(entity, expected, where); // C4.
        Assert.Same(platformBefore, entity.PlatformEntity);
        Assert.Equal(heroBefore, HeroState(hero)); // C6.
        Assert.Empty(((IEntityWorldContext)world).SpawnedEntities); // C7.

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
        var vectors = AlundraEnemyHandlerOracle.Vectors("muruta").Where(v => v.Handler == "E1").ToList();
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
        foreach (var scenario in AlundraEnemyHandlerOracle.Scenarios().Where(s => s.Name.StartsWith("MU-", StringComparison.Ordinal)))
        {
            foreach (var call in scenario.Calls.Where(c => c.Handler == "E1"))
            {
                RunE1Call($"{scenario.Name} tick {call.Tick} E1", call.Before, HeroOf(call.Hero), call.Changed, call.Journal, call.RngIn, null, done % 2 == 0 ? 0 : 3, (int)call.Before["status"]);
                done++;
            }
        }

        Assert.Equal(7, done);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The reader and the montage themselves
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReader_CountsTheMurutaVectorsAndCallsOfThePlan()
    {
        var vectors = AlundraEnemyHandlerOracle.Vectors("muruta");
        Assert.Equal(4913, vectors.Count);
        Assert.Equal(new[] { ("A0", 6), ("C10", 4043), ("D9", 720), ("E1", 144) }, vectors.GroupBy(v => v.Handler).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToArray());

        var calls = AlundraEnemyHandlerOracle.Scenarios().Where(s => s.Name.StartsWith("MU-", StringComparison.Ordinal)).SelectMany(s => s.Calls).ToList();
        Assert.Equal(3675, calls.Count);
        Assert.Equal(new[] { ("A0", 16), ("C10", 3637), ("D9", 15), ("E1", 7) }, calls.GroupBy(c => c.Handler).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToArray());
        Assert.All(vectors, v => Assert.All(v.Changed, c => Assert.Contains(c.Name, AlundraEnemyHandlerOracle.MurutaClosure)));
        Assert.All(calls, c => Assert.All(c.Changed, ch => Assert.Contains(ch.Name, AlundraEnemyHandlerOracle.MurutaClosure)));
        Assert.Equal(18, AlundraEnemyHandlerOracle.MurutaClosure.Count);

        // The hero's attack pointer of the C10 calls: 615 strike calls with the frame box, the others never dereferenced.
        var c10 = calls.Where(c => c.Handler == "C10").ToList();
        Assert.Equal(615, c10.Count(c => c.HeroAttackPointer == 0x80144229));
        Assert.Equal(3022, c10.Count(c => c.HeroAttackPointer == 0x80144209));
        Assert.All(c10, c => Assert.Equal(c.HeroAttackPointer == 0x80144229, c.Hero["box"] != 0));
    }
}
