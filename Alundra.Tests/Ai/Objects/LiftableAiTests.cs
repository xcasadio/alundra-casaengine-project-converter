#nullable enable
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g1a T1 (docs/plan-e20-heros.md section 1.24.1, (c4), D-G-9, D-G-12): the program C23 (<c>0x8006B848</c>, slot C, code 23) of the crates, the jars and the other liftable objects of the seven sprite types of the export, called directly: nothing while
/// the object is not carried, the plain animation while it is carried, the release by the code of the throw (<c>+0x24</c>: 1, 2, 3 and -1 through the table <c>0x80027400</c>), and the registration in a registry of a test (the production registry
/// gets the key in phase C, T4). The vectors are those of <c>docs/plan-e20-annexe/e20-g1a/work/g1a_models.py</c> (<c>c23</c>) and of the oracle <c>g4</c>.
/// </summary>
public sealed class LiftableAiTests
{
    private const int Px = CarryBench.Px;
    private const uint CrateFlags = 0x3323E4;

    private static (EntityScriptProxy Hero, EntityScriptProxy Crate) Carried(int throwKind)
    {
        var (hero, crate) = CarryBench.Pair(new CarryRules.Triple(0, 0, 32 * Px));
        crate.Flags = CrateFlags;
        crate.Flags2 = throwKind;
        crate.TargetAnimationId = 7;
        crate.RidingEntity = hero.LogicContextEntity;
        return (hero, crate);
    }

    private static void Run(EntityScriptProxy crate) => LiftableAi.Tick(crate, new CarryBench.RecordingWorld());

    [Fact]
    public void C4_NotCarried_NothingChanges_NextToTheCarriedCrateThatChanges()
    {
        var (_, loose) = Carried(2);
        loose.PlatformEntity = null; // the code of the throw is set (a thrown crate that landed), but nothing carries it.
        var (_, carried) = Carried(2);

        Run(loose);
        Run(carried);

        Assert.Equal((CrateFlags, 2, 7u, true), (loose.Flags, loose.Flags2, loose.TargetAnimationId, loose.RidingEntity != null));
        Assert.Equal((0x332374u, 0, 1u), (carried.Flags, carried.Flags2, carried.TargetAnimationId));
    }

    [Fact]
    public void C4_CarriedWithoutAThrowCode_TheAnimation0_TheLinksAreKept_TheFlagsAreNot_NextToTheCodeThatReleases()
    {
        var (hero, held) = Carried(0);
        var (hero2, thrown) = Carried(1);

        Run(held);
        Run(thrown);

        Assert.Equal((0u, CrateFlags, 0), (held.TargetAnimationId, held.Flags, held.Flags2));
        Assert.Equal((true, true), (ReferenceEquals(held.PlatformEntity, hero.LogicContextEntity), ReferenceEquals(hero.CarriedEntity, held.LogicContextEntity)));

        // the neighbour: the code 1 releases (animation 3, links cleared).
        Assert.Equal((3u, null, null), (thrown.TargetAnimationId, thrown.PlatformEntity, hero2.CarriedEntity));
    }

    [Theory]
    [InlineData(1, 3u)]
    [InlineData(2, 1u)]
    [InlineData(3, 4u)]
    [InlineData(-1, 3u)]
    public void C4_TheCodeOfTheThrowGivesTheAnimationOfTheFlight_TheCodeGoesBackTo0_BothLinksAreCleared_TheFlagsLoseTheCarriedBit(int throwKind, uint animation)
    {
        var (hero, crate) = Carried(throwKind);

        Run(crate);

        Assert.Equal((animation, 0, 0x332374u), (crate.TargetAnimationId, crate.Flags2, crate.Flags));
        Assert.Equal((null, null, null), (crate.PlatformEntity, hero.CarriedEntity, crate.RidingEntity));

        // a second call changes nothing: the crate is not carried any more.
        crate.TargetAnimationId = 9;
        crate.Flags2 = 2;
        Run(crate);
        Assert.Equal((9u, 2, 0x332374u), (crate.TargetAnimationId, crate.Flags2, crate.Flags));
    }

    [Fact]
    public void C4_TheReleasedFlags_SetTheBits0x30_ClearTheBit0x80_AndKeepEveryOtherBit()
    {
        Assert.Equal((0x332374u, 0x332374u, 0x30u, 0xFFFFFF7Fu), (LiftableAi.ReleasedFlags(0x3323E4), LiftableAi.ReleasedFlags(0x332374), LiftableAi.ReleasedFlags(0x80), LiftableAi.ReleasedFlags(0xFFFFFFFF)));
    }

    [Fact]
    public void C4_TheTableOfTheAnimations_IsTheWordsOf0x80027400_AndAnythingElseDoesNotRelease()
    {
        Assert.Equal((3u, 1u, 4u, 3u), (LiftableAi.ReleaseAnimation(1), LiftableAi.ReleaseAnimation(2), LiftableAi.ReleaseAnimation(3), LiftableAi.ReleaseAnimation(-1)));
        Assert.Equal((null, null, null), (LiftableAi.ReleaseAnimation(0), LiftableAi.ReleaseAnimation(4), LiftableAi.ReleaseAnimation(-2)));
    }

    [Fact]
    public void C4_ACarrierWithoutProxy_TheCrateIsStillReleased_AndNothingThrows_NextToTheCarrierWithOneThatIsCleared()
    {
        var (_, withProxy) = Carried(2);
        var (_, bareCarrier) = Carried(2);
        bareCarrier.PlatformEntity = new Entity { Name = "a bare carrier" }; // the oracle projections of E14 put such an entity there.

        Run(withProxy);
        var exception = Record.Exception(() => Run(bareCarrier));

        Assert.Equal((null, 1u, 0x332374u, null), (exception, bareCarrier.TargetAnimationId, bareCarrier.Flags, bareCarrier.PlatformEntity));
        Assert.Equal((1u, 0x332374u), (withProxy.TargetAnimationId, withProxy.Flags));
    }

    [Fact]
    public void C4_TheCodeIsC23_TheSlotIsTheTick_TheSevenTypesAreTheOnesOfTheExport()
    {
        Assert.Equal(23, LiftableAi.Code);
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 23) }, LiftableAi.Keys.ToArray());

        // the export, read as the DLL reads it: the types of the global page and (plus 0x100) of the map page whose header has C23 - never skipped, a missing file fails.
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var exported = new List<int>();
        foreach (var isMapPage in new[] { false, true })
        {
            for (var index = 0; index < 256; index++)
            {
                if (catalog.TryGetPrefab(isMapPage, index, out var prefab) && catalog.TryGet(prefab, out var header) && header.ProgramTick == LiftableAi.Code)
                {
                    exported.Add((isMapPage ? 0x100 : 0) + index);
                    Assert.Equal(2, header.ProgramDeactivate); // E2 of the same types (g2b).
                    Assert.Equal((0, 0, 0), (header.ProgramLoad, header.ProgramTouch, header.ProgramInteract));
                }
            }
        }

        Assert.Equal(new[] { 234, 291, 292, 294, 297, 304, 438 }, exported.OrderBy(t => t).ToArray());
        Assert.Equal(exported.OrderBy(t => t).ToArray(), LiftableAi.SpriteTypes.OrderBy(t => t).ToArray());
    }

    [Fact]
    public void C4_RegisteredInARegistryOfATest_TheKeyIsC23_ForTheSevenTypesAlone_AndOnlyInTheSlotC()
    {
        var registry = new NativeSpriteRegistry();
        LiftableAi.Register(registry);

        Assert.Equal(LiftableAi.Keys.Count, registry.Count);
        foreach (var type in LiftableAi.SpriteTypes)
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramCTick, 23, type, out var handler), $"type {type}");
            Assert.NotNull(handler);
        }

        // the neighbours: the types around them, the pillar and the arrow, the other slots and the other codes do not have it.
        foreach (var type in new[] { 233, 235, 290, 293, 298, 303, 305, 437, 439, 430, 7, 3, 0 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 23, type, out _), $"type {type}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramALoad, 23, 291, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 23, 291, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 22, 291, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 24, 291, out _));
    }
}
