#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.f3a (docs/plan-e20-heros.md section 1.19.1, D-F3-3, D-F3-4): the seam that wires the shot of the hero to <see cref="IEntityWorldContext.SpawnEntityBySpriteIndex"/> without wiring any type.
/// <see cref="AlundraPlayerManager.SpawnWeaponProjectile"/> keeps, in this order, the counter, the journal, then the test hook INSTEAD of the production call (never two apparitions), then - for a type of the set
/// <see cref="AlundraPlayerManager.WiredWeaponTypes"/> only, and when the host is an <see cref="IEntityWorldContext"/> - the interface. The set is empty in production in this slice; a test replaces it
/// (<see cref="AlundraPlayerManager.WiredWeaponTypesForTests"/>). The world is map 14 (<see cref="ArcRun"/>, real prefabs): a "no effect" case is always set next to the positive case in the same test, so a seam that
/// never works cannot pass. The shot is the one of Hunter's Bow (record 8, ground): the pointer is armed with its counter at the delay, so that the next tick of <c>MovePlayer</c> reaches the spawn.
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraWeaponSpawnSeamTests : IDisposable
{
    private const int Arrow = 3;

    public AlundraWeaponSpawnSeamTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        AlundraPlayerManager.WiredWeaponTypesForTests = null;
        AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;
    }

    private static ArcRun Arc(string name) => new(new ArcSpec("E20f3a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static IEntityWorldContext Context(ArcRun arc) => arc.Proxy;

    /// <summary>Arms the hero's bow so that the NEXT tick of <c>MovePlayer</c> reaches the spawn of the shot (the record of Hunter's Bow, the animation that spawns after the delay).</summary>
    private static void Arm(AlundraEntityScriptProxy hero)
    {
        hero.WeaponRecordPointer = AlundraWeaponTables.AnimationAddress(8, air: false);
        hero.WeaponStepCounter = AlundraWeaponTables.RecordByte(hero.WeaponRecordPointer + 2);
        hero.TargetAnimationId = 0x13;
    }

    /// <summary>What one shot did: the counter and the journal of the stub, the entities that were not there before, the pointer after.</summary>
    private sealed record ShotResult(int CounterDelta, IReadOnlyList<AlundraPlayerManager.WeaponSpawnCall> Journal, IReadOnlyList<AlundraEntityScriptProxy> Born, uint PointerAfter, int LowestFreeSlotBefore);

    private static ShotResult Fire(ArcRun arc)
    {
        arc.OneFrame(); // settles.
        var before = arc.Entities.ToList();
        var lowestFree = Enumerable.Range(0, AlundraEntitySlotTable.SlotCount).FirstOrDefault(i => !arc.Proxy.SlotTable.IsHeld(i), -1);
        var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
        var journal = new List<AlundraPlayerManager.WeaponSpawnCall>();
        AlundraPlayerManager.WeaponSpawnCallsForTests = journal;
        Arm(arc.Hero);
        arc.OneFrame();
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;
        return new ShotResult(AlundraPlayerManager.WeaponSpawnCallCountForTests - counter, journal, arc.Entities.Where(e => !before.Contains(e)).ToList(), arc.Hero.WeaponRecordPointer, lowestFree);
    }

    private static void Wire(params int[] types) => AlundraPlayerManager.WiredWeaponTypesForTests = new HashSet<int>(types);

    /// <summary>Fills the table of the entity slots with arrows born through the interface, up to <paramref name="free"/> free slots.</summary>
    private static void FillSlots(ArcRun arc, int free)
    {
        while (AlundraEntitySlotTable.SlotCount - arc.Proxy.SlotTable.HeldCount > free)
        {
            Assert.NotNull(Context(arc).SpawnEntityBySpriteIndex(arc.Hero, false, Arrow, 100 << 16, 100 << 16, 0, 0x10));
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s1) the empty set creates nothing, the set {3} creates the arrow.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S1_TheEmptySet_CountsAndJournalsTheShotAndCreatesNothing_TheSetWithTheArrow_CreatesOneEntityInTheLowestFreeSlot()
    {
        using (var arc = Arc("s1-empty"))
        {
            var shot = Fire(arc);

            Assert.Equal(1, shot.CounterDelta);
            Assert.Single(shot.Journal);
            Assert.Equal(Arrow, shot.Journal[0].Type);
            Assert.Empty(shot.Born);
            Assert.Equal(0u, shot.PointerAfter);
        }

        using (var arc = Arc("s1-arrow"))
        {
            Wire(Arrow);
            var shot = Fire(arc);

            Assert.Equal(1, shot.CounterDelta);
            Assert.Single(shot.Journal);
            var born = Assert.Single(shot.Born);
            Assert.Equal(Arrow, born.SpriteType);
            Assert.Same(arc.HeroEntity, born.ParentEntity);
            Assert.True(shot.LowestFreeSlotBefore > 0, "map 14 leaves a free slot to the shot");
            Assert.Equal(shot.LowestFreeSlotBefore, born.Slot);
            Assert.True(born.JoinsBirthTick);
            Assert.Equal(0u, shot.PointerAfter);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s2) the hook replaces the production call.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S2_WithTheHook_TheHooksEntityIsTheOnlyOne_WithoutTheHook_TheProductionCallMakesOne()
    {
        using (var arc = Arc("s2-hook"))
        {
            Wire(Arrow);
            var hookCalls = 0;
            AlundraEntityScriptProxy? fromHook = null;
            AlundraPlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
            {
                hookCalls++;
                fromHook = Context(arc).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
            };

            var shot = Fire(arc);

            Assert.Equal(1, hookCalls);
            Assert.NotNull(fromHook);
            var born = Assert.Single(shot.Born); // never two apparitions: the hook replaces the production call.
            Assert.Same(fromHook, born);
            Assert.Equal(1, shot.CounterDelta);
            Assert.Single(shot.Journal);
        }

        using (var arc = Arc("s2-production"))
        {
            Wire(Arrow);
            AlundraPlayerManager.WeaponSpawnBirthHookForTests = null;

            var shot = Fire(arc);

            var born = Assert.Single(shot.Born);
            Assert.Equal(Arrow, born.SpriteType);
            Assert.Equal(1, shot.CounterDelta);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s3) a host that is not a world context: the shot is counted and journalled, nothing is created, nothing throws.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S3_OnAMontageHost_TheShotIsCountedAndJournalled_NoInterfaceIsCalled_AndTheSameShotInTheWorldCreatesOne()
    {
        Wire(Arrow);
        var rig = JumpHeroRig.Build(freePad: true, attackClock: true);
        Assert.IsNotAssignableFrom<IEntityWorldContext>(rig.Host);
        var hero = rig.Hero;
        hero.Status = EntityStatus.Normal;
        hero.TargetDirection = 24;
        hero.AnimationDirection = 3;
        rig.Step();

        var journal = new List<AlundraPlayerManager.WeaponSpawnCall>();
        AlundraPlayerManager.WeaponSpawnCallsForTests = journal;
        var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
        Arm(hero);
        var exception = Record.Exception(() => rig.Step());
        AlundraPlayerManager.WeaponSpawnCallsForTests = null;

        Assert.Null(exception);
        Assert.Equal(counter + 1, AlundraPlayerManager.WeaponSpawnCallCountForTests);
        Assert.Single(journal);
        Assert.Equal(0u, hero.WeaponRecordPointer);

        using var arc = Arc("s3-world");
        Wire(Arrow);
        var shot = Fire(arc);
        Assert.Single(shot.Born);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s4) a full table loses the shot silently, as the binary does (it ignores the return).
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S4_WithAFullTable_NothingIsCreatedAndThePointerIsCleared_WithOneFreeSlot_OneEntityIsCreated()
    {
        using (var arc = Arc("s4-full"))
        {
            Wire(Arrow);
            arc.OneFrame();
            FillSlots(arc, free: 0);
            Assert.Equal(AlundraEntitySlotTable.SlotCount, arc.Proxy.SlotTable.HeldCount);
            var count = arc.Entities.Count;
            var counter = AlundraPlayerManager.WeaponSpawnCallCountForTests;
            var fullLines = arc.Log!.Warnings.Count(w => w.Contains("slots is full", StringComparison.Ordinal));

            Arm(arc.Hero);
            var exception = Record.Exception(() => arc.OneFrame());

            Assert.Null(exception);
            Assert.Equal(counter + 1, AlundraPlayerManager.WeaponSpawnCallCountForTests);
            Assert.Equal(count, arc.Entities.Count);
            Assert.Equal(0u, arc.Hero.WeaponRecordPointer);
            Assert.Equal(fullLines + 1, arc.Log.Warnings.Count(w => w.Contains("slots is full", StringComparison.Ordinal))); // the interface refused the shot with its one line.
        }

        using (var arc = Arc("s4-one-free"))
        {
            Wire(Arrow);
            arc.OneFrame();
            FillSlots(arc, free: 1);
            var count = arc.Entities.Count;
            var freeSlot = Enumerable.Range(0, AlundraEntitySlotTable.SlotCount).Single(i => !arc.Proxy.SlotTable.IsHeld(i));

            Arm(arc.Hero);
            arc.OneFrame();

            Assert.Equal(count + 1, arc.Entities.Count);
            Assert.Equal(freeSlot, arc.Entities.Single(e => e.Slot == freeSlot).Slot);
            Assert.Equal(AlundraEntitySlotTable.SlotCount, arc.Proxy.SlotTable.HeldCount);
            Assert.Equal(0u, arc.Hero.WeaponRecordPointer);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s5) the invariant "never before its handler E": every code C and E of the header of a wired type is handled.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S5_TheProductionSetPassesTheInvariant_TheFakeSetWithTheArrowFailsOnE17()
    {
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var registry = new AlundraEventProgramRunner(null, AlundraGameState.Instance).NativeSprites;

        Assert.Empty(AlundraPlayerManager.ProductionWiredWeaponTypes); // this slice wires no type.
        Assert.Empty(AlundraPlayerManager.UnhandledWeaponSpriteCodes(AlundraPlayerManager.ProductionWiredWeaponTypes, catalog, registry));

        var unhandled = AlundraPlayerManager.UnhandledWeaponSpriteCodes(new[] { Arrow }, catalog, registry);
        Assert.Contains((ScriptHelper.ProgramEDeactivate, 17), unhandled); // the arrow's deactivation program (E17) has no handler yet.
        Assert.DoesNotContain(unhandled, u => u.Slot == ScriptHelper.ProgramCTick); // its C code is 0.

        // The same checker holds a handled code as handled: a registered key clears (E, 17) for the arrow only, and the inline code E1 of the type 7 is handled by the runner itself (its C94 is not).
        using (registry.Register(ScriptHelper.ProgramEDeactivate, 17, new[] { Arrow }, (_, _, _) => { }))
        {
            Assert.Empty(AlundraPlayerManager.UnhandledWeaponSpriteCodes(new[] { Arrow }, catalog, registry));
        }

        var seven = AlundraPlayerManager.UnhandledWeaponSpriteCodes(new[] { 7 }, catalog, registry);
        Assert.Contains((ScriptHelper.ProgramCTick, 94), seven);
        Assert.DoesNotContain((ScriptHelper.ProgramEDeactivate, 1), seven);
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // (s6) the index of the table of directions is bounded: a direction of the tick outside 0..3 reads the nearest entry instead of throwing.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void S6_ADirectionOutsideTheTable_IsClampedToTheNearestEntry_AndANormalDirectionIsUnchanged()
    {
        foreach (var (boxDirection, expected) in new[] { (9, 0x18), (-4, 0), (3, 0x18), (0, 0) })
        {
            using var arc = Arc("s6-" + boxDirection);
            arc.OneFrame(); // settles.
            var journal = new List<AlundraPlayerManager.WeaponSpawnCall>();
            AlundraPlayerManager.WeaponSpawnCallsForTests = journal;
            Arm(arc.Hero);
            arc.Hero.BoxDirection = boxDirection;
            AlundraPlayerManager.SpawnWeaponProjectile(arc.Hero, arc.Hero.WeaponRecordPointer, null);
            AlundraPlayerManager.WeaponSpawnCallsForTests = null;

            Assert.Equal(expected, Assert.Single(journal).Direction);
        }
    }
}
