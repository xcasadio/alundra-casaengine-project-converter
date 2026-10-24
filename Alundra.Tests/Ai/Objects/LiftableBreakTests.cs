#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2b T1 (docs/plan-e20-heros.md section 1.24.3, (b1)): the program E2 (<c>0x8007ED30</c>, slot E, code 2) of the crates, the jars and the other thrown objects of the ten sprite types of the export, called directly. The table of the binary
/// (the model <c>e2</c> of <c>docs/plan-e20-annexe/e20-g1a/work/g1a_models.py</c>, 6 000 vectors without a gap): <c>(+0x88 == 2 and +0xAC == 1) or (+0x184 &amp; 4)</c> destroys with <c>DestroyEntity(e, -1)</c>; otherwise
/// <c>+0x88 = 2</c> and <c>+0x6C = (+0x6C | 0x40) &amp; ~0x30</c>. The three lines are read two by two: each line next to a vector that does not take it.
/// </summary>
public sealed class LiftableBreakTests
{
    private const uint Thrown = 0x332374;
    private const uint Broken = 0x332344;

    private static (EntityScriptProxy Entity, ContactHost Host) Object(uint target, int reset, uint vramAnd, uint flags = Thrown)
    {
        var host = new ContactHost();
        var entity = new EntityScriptProxy
        {
            ScriptHost = host,
            Status = EntityStatus.Deactivated,
            EventTrigger = ScriptHelper.ProgramEDeactivate,
            TargetAnimationId = target,
            ForceResetAnimationFlag = reset,
            CombinedVramFlagsAND = vramAnd,
            Flags = flags,
        };
        host.All.Add(entity);
        return (entity, host);
    }

    [Fact]
    public void B1_TheAnimation2AtItsEnd_Destroys_AndTouchesNothing_NextToTheVectorsThatAreOneStepShortOfIt()
    {
        var (end, endHost) = Object(target: 2, reset: 1, vramAnd: 0, flags: Broken);

        LiftableAi.Break(end);

        Assert.Equal((end, -1), Assert.Single(endHost.Destroyed));
        Assert.Equal((2u, Broken, 1), (end.TargetAnimationId, end.Flags, end.ForceResetAnimationFlag));

        // one step short: the animation 2 asked but the end not reached (+0xAC 0 or 2: the binary tests for 1), or the end flag with another animation asked.
        foreach (var (target, reset) in new[] { (2u, 0), (2u, 2), (2u, -1), (0u, 1), (3u, 1), (7u, 1) })
        {
            var (e, host) = Object(target, reset, vramAnd: 0);

            LiftableAi.Break(e);

            Assert.Empty(host.Destroyed);
            Assert.Equal((2u, Broken), (e.TargetAnimationId, e.Flags));
        }
    }

    [Fact]
    public void B1_TheBit4OfTheGroundPropertyOfTheFourCorners_Destroys_WhateverTheAnimation_NextToTheOtherBits()
    {
        foreach (var target in new[] { 0u, 3u })
        {
            var (e, host) = Object(target, reset: 0, vramAnd: 4);

            LiftableAi.Break(e);

            Assert.Equal((e, -1), Assert.Single(host.Destroyed));
            Assert.Equal((target, Thrown), (e.TargetAnimationId, e.Flags)); // a destruction writes neither the animation nor the flags.
        }

        var (withOthers, othersHost) = Object(target: 0, reset: 0, vramAnd: 0xFFFFFFFF);
        LiftableAi.Break(withOthers);
        Assert.Single(othersHost.Destroyed);

        // every other bit, and the bits around it, leave the line alone.
        foreach (var vram in new uint[] { 0, 1, 2, 3, 8, 0x8000, 0x8003, 0xFFFFFFFB })
        {
            var (e, host) = Object(target: 0, reset: 0, vramAnd: vram);

            LiftableAi.Break(e);

            Assert.Empty(host.Destroyed);
            Assert.Equal((2u, Broken), (e.TargetAnimationId, e.Flags));
        }
    }

    [Fact]
    public void B1_Otherwise_TheAnimation2IsAsked_AndTheFlagsBecomeOr0x40AndNot0x30_NextToTheLinesThatDestroyInstead()
    {
        // (flags | 0x40) & ~0x30: the crate once let go becomes the crate in its break; the vectors carry the bit 0 (an equivalent mask ~0x31 would lose it), the bits 0x10 and 0x20 alone, and every bit.
        foreach (var (before, after) in new (uint, uint)[]
                 {
                     (Thrown, Broken),
                     (0x3323E4, 0x3323C4),
                     (0x232374, 0x232344),
                     (0, 0x40),
                     (0x10, 0x40),
                     (0x20, 0x40),
                     (0x30, 0x40),
                     (0x31, 0x41),
                     (0x40, 0x40),
                     (0xFFFFFFFF, 0xFFFFFFCF),
                 })
        {
            var (e, host) = Object(target: 5, reset: 0, vramAnd: 0, flags: before);

            LiftableAi.Break(e);

            Assert.Empty(host.Destroyed);
            Assert.Equal((2u, after, 0), (e.TargetAnimationId, e.Flags, e.ForceResetAnimationFlag));
        }

        // the same entity, one tick later: the animation 2 now asked and its end reached - the line that destroys.
        var (next, nextHost) = Object(target: 5, reset: 0, vramAnd: 0);
        LiftableAi.Break(next);
        next.ForceResetAnimationFlag = 1;
        LiftableAi.Break(next);
        Assert.Equal((next, -1), Assert.Single(nextHost.Destroyed));
    }

    [Fact]
    public void B1_AnEntityWithoutAHost_StillTakesTheLinesThatWriteAndNothingThrows_NextToOneWithAHost()
    {
        var (withHost, host) = Object(target: 2, reset: 1, vramAnd: 0);
        var bare = new EntityScriptProxy { TargetAnimationId = 5, Flags = Thrown };
        var bareDestroyed = new EntityScriptProxy { TargetAnimationId = 2, ForceResetAnimationFlag = 1, Flags = Broken };

        LiftableAi.Break(withHost);
        Assert.Null(Record.Exception(() => LiftableAi.Break(bare)));
        Assert.Null(Record.Exception(() => LiftableAi.Break(bareDestroyed)));

        Assert.Single(host.Destroyed);
        Assert.Equal((2u, Broken), (bare.TargetAnimationId, bare.Flags));
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // The key and the ten types.
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void B1_TheCodeIsE2_TheSlotIsTheDeactivation_AndTheTenTypesAreTheOnesOfTheExport()
    {
        Assert.Equal(2, LiftableAi.BreakCode);
        Assert.Equal(new[] { (ScriptHelper.ProgramEDeactivate, 2) }, LiftableAi.BreakKeys.ToArray());
        Assert.Equal(new[] { (ScriptHelper.ProgramCTick, 23) }, LiftableAi.Keys.ToArray()); // the keys of g1a are not touched: the two pins derive from the two lists.

        // the export, read as the DLL reads it: the types of the global page and (plus 0x100) of the map page whose header has E2 - never skipped, a missing file fails.
        var catalog = new SpriteRecordCatalog(ProjectRootFinder.Find());
        var exported = new List<int>();
        foreach (var isMapPage in new[] { false, true })
        {
            for (var index = 0; index < 256; index++)
            {
                if (catalog.TryGetPrefab(isMapPage, index, out var prefab) && catalog.TryGet(prefab, out var header) && header.ProgramDeactivate == LiftableAi.BreakCode)
                {
                    var type = (isMapPage ? 0x100 : 0) + index;
                    exported.Add(type);

                    // the seven types of C23 and the three of the family C4 (the stone, the barrel, the trunk); the other slots are empty.
                    Assert.Equal(LiftableAi.SpriteTypes.Contains(type) ? 23 : 4, header.ProgramTick);
                    Assert.Equal((0, 0, 0), (header.ProgramLoad, header.ProgramTouch, header.ProgramInteract));
                }
            }
        }

        Assert.Equal(new[] { 234, 290, 291, 292, 293, 294, 297, 304, 373, 438 }, exported.OrderBy(t => t).ToArray());
        Assert.Equal(exported.OrderBy(t => t).ToArray(), LiftableAi.BreakSpriteTypes.OrderBy(t => t).ToArray());
        Assert.All(LiftableAi.SpriteTypes, type => Assert.Contains(type, LiftableAi.BreakSpriteTypes)); // every type of C23 breaks.
    }

    [Fact]
    public void B1_RegisteredInARegistryOfATest_TheKeyIsE2_ForTheTenTypesAlone_AndOnlyInTheSlotE_AndTheDisposableTakesItOut()
    {
        var registry = new NativeSpriteRegistry();
        var registration = LiftableAi.RegisterBreak(registry);

        Assert.Equal(LiftableAi.BreakKeys.Count, registry.Count);
        foreach (var type in LiftableAi.BreakSpriteTypes)
        {
            Assert.True(registry.TryGet(ScriptHelper.ProgramEDeactivate, 2, type, out var handler), $"type {type}");
            Assert.NotNull(handler);
        }

        // the neighbours of the ten types, the pillar and the arrow, the other slots and the other codes do not have it.
        foreach (var type in new[] { 233, 235, 289, 295, 296, 298, 303, 305, 372, 374, 437, 439, 430, 7, 3, 0, 335 })
        {
            Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 2, type, out _), $"type {type}");
        }

        Assert.False(registry.TryGet(ScriptHelper.ProgramCTick, 2, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramALoad, 2, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 1, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 3, 292, out _));
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 23, 292, out _));

        // the key can be taken once; the disposable gives it back.
        Assert.Throws<InvalidOperationException>(() => LiftableAi.RegisterBreak(registry));
        registration.Dispose();
        Assert.Equal(0, registry.Count);
        Assert.False(registry.TryGet(ScriptHelper.ProgramEDeactivate, 2, 292, out _));
    }

    [Fact]
    public void B1_TheHandlerOfTheRegistryIsTheBreak_ForTheTypeItIsRegisteredFor_NextToATypeItIsNot()
    {
        // through the runner, as the pick phase reaches it: a Deactivated entity of the slot E whose header holds E2.
        var runner = new EventProgramRunner(null, new GameState());
        using var hook = BreakHook.Install(runner);

        EntityScriptProxy Make(int type)
        {
            var host = new ContactHost();
            var entity = new EntityScriptProxy
            {
                ScriptHost = host,
                Status = EntityStatus.Deactivated,
                EventTrigger = ScriptHelper.ProgramEDeactivate,
                SpriteType = type,
                TargetAnimationId = 5,
                Flags = Thrown,
            };
            entity.SpriteProgramIndexes[ScriptHelper.ProgramEDeactivate] = LiftableAi.BreakCode;
            host.All.Add(entity);
            return entity;
        }

        var crate = Make(292);
        var other = Make(600); // a type outside the ten: the key does not name it.

        crate.RunPickedEvent(runner);
        other.RunPickedEvent(runner);

        Assert.Equal((2u, Broken), (crate.TargetAnimationId, crate.Flags));
        Assert.Equal((5u, Thrown), (other.TargetAnimationId, other.Flags));
    }
}
