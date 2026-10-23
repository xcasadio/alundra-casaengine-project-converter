#nullable enable
using System.Linq;
using Xunit;
using Alundra.Entities;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T1 (docs/plan-e20-heros.md section 1.24.6, D-G5B-1), the tests taken from E19.sm3 (docs/plan-e19-opcodes.md section 1.4.4, <c>docs/plan-e19-sm-annexe/sm3/tests.tsv</c>) that bear on the opcodes
/// <c>0xA9</c> (Script_169_0A9 @ 0x80041174, size 2: <c>Result</c> = 1 when the logic entity's <c>XCollisionEntity</c> (+0x130) is one of the entities the search finds) and <c>0xAA</c> (Script_170_0AA @ 0x80041200,
/// size 2: <c>Result</c> = 1 when one of them has +0x130 equal to the logic entity). Expected values are the binary's: each vector of <see cref="ContactVectors"/> is one execution of the real handler in the MIPS interpreter
/// (R1 and R2 of <c>sm3/report.md</c>: identity, never <c>EntityRefId</c>; <c>Result</c> written on every path; the logic entity, not the owner). The montage is the bare runner of <see cref="OpcodeWorld"/> (M1, R-SM-4); every vector
/// checks the length (2), the trace (implemented, size 2) and that nothing but <c>Result</c> moved. All red at the start HEAD, where the two opcodes are skipped by their size and <c>Result</c> stays at 7.
/// </summary>
public sealed class ContactGroupOpcodesTests
{
    /// <summary>U-A9-01: A9-01-hero-blocks-me=1; A9-22-stale-zero-then-true=1.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_HeroGroup_ContactIsTheHero_GivesOne()
    {
        ContactVectors.Check("A9-01-hero-blocks-me", 1);
        ContactVectors.Check("A9-22-stale-zero-then-true", 1);
    }

    /// <summary>U-A9-02: A9-02-null-contact=0; A9-21-stale-one-then-false=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_NullContact_GivesZero_AndOverwritesAStaleOne()
    {
        ContactVectors.Check("A9-02-null-contact", 0);
        ContactVectors.Check("A9-21-stale-one-then-false", 0);
    }

    /// <summary>U-A9-03: A9-03-other-contact=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_ContactOutsideTheGroup_GivesZero()
    {
        ContactVectors.Check("A9-03-other-contact", 0);
    }

    /// <summary>U-A9-04: A9-04-rawid24-hero-is-reference=1; A9-05-rawid-no-entity=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_RawId24_HeroIsTheReference_Real440B9Shape()
    {
        ContactVectors.Check("A9-04-rawid24-hero-is-reference", 1);
        ContactVectors.Check("A9-05-rawid-no-entity", 0);
    }

    /// <summary>U-A9-05: A9-07-rawid-reference-loaded=1; A9-08-rawid-reference-deactivated=1; A9-06-rawid-reference-not-active=0.</summary>
    [Theory]
    [InlineData("A9-07-rawid-reference-loaded", 1)]
    [InlineData("A9-08-rawid-reference-deactivated", 1)]
    [InlineData("A9-06-rawid-reference-not-active", 0)]
    public void BlockedByGroup_0xA9_RawId_ReferenceMustBeLoadedNormalOrDeactivated(string vector, int expected) => ContactVectors.Check(vector, expected);

    /// <summary>U-A9-06: A9-09-rawid-candidate-status-ignored=1.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_RawId_TheCandidateStatusIsNotTested()
    {
        ContactVectors.Check("A9-09-rawid-candidate-status-ignored", 1);
    }

    /// <summary>U-A9-07: A9-10-rawid-second-of-two=1.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_RawId_ContactIsTheSecondOfTwoEntities()
    {
        ContactVectors.Check("A9-10-rawid-second-of-two", 1);
    }

    /// <summary>U-A9-08: A9-11-self-group=1; A9-12-self-group-other=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_Type0_SelfAndNotSelf()
    {
        ContactVectors.Check("A9-11-self-group", 1);
        ContactVectors.Check("A9-12-self-group-other", 0);
    }

    /// <summary>U-A9-09: A9-13-all-contact-destroyed=0; A9-14-all-contact-loaded=1; A9-15-all-contact-deactivated=1.</summary>
    [Theory]
    [InlineData("A9-13-all-contact-destroyed", 0)]
    [InlineData("A9-14-all-contact-loaded", 1)]
    [InlineData("A9-15-all-contact-deactivated", 1)]
    public void BlockedByGroup_0xA9_Type2_ContactStatus(string vector, int expected) => ContactVectors.Check(vector, expected);

    /// <summary>U-A9-10: A9-16-all-but-hero-hero-contact=0; A9-17-all-but-hero-npc-contact=1.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_Type3_ExcludesTheHero()
    {
        ContactVectors.Check("A9-16-all-but-hero-hero-contact", 0);
        ContactVectors.Check("A9-17-all-but-hero-npc-contact", 1);
    }

    /// <summary>U-A9-11: A9-18-type11-carried-contact=1; A9-19-type11-not-carried=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_Type11_CarriedContact()
    {
        ContactVectors.Check("A9-18-type11-carried-contact", 1);
        ContactVectors.Check("A9-19-type11-not-carried", 0);
    }

    /// <summary>U-A9-12: A9-20-empty-group=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_OnlyTheHeroExists_Type3Empty()
    {
        ContactVectors.Check("A9-20-empty-group", 0);
    }

    /// <summary>U-A9-13: A9-23-hero-reference-self-contact=1; A9-24-hero-reference-group-hero-null=0.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_HeroIsTheLogicEntity()
    {
        ContactVectors.Check("A9-23-hero-reference-self-contact", 1);
        ContactVectors.Check("A9-24-hero-reference-group-hero-null", 0);
    }

    /// <summary>U-A9-14: A9-25-holes-in-the-slots=1.</summary>
    [Fact]
    public void BlockedByGroup_0xA9_FreeSlotsBetweenAreAbsent()
    {
        ContactVectors.Check("A9-25-holes-in-the-slots", 1);
    }

    /// <summary>U-AA-01: AA-01-hero-blocked-by-me=1; AA-18-stale-zero-then-true=1.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_HeroContactIsMe_GivesOne()
    {
        ContactVectors.Check("AA-01-hero-blocked-by-me", 1);
        ContactVectors.Check("AA-18-stale-zero-then-true", 1);
    }

    /// <summary>U-AA-02: AA-02-hero-null-contact=0; AA-17-stale-one-then-false=0.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_HeroNullContact_GivesZero_AndOverwritesAStaleOne()
    {
        ContactVectors.Check("AA-02-hero-null-contact", 0);
        ContactVectors.Check("AA-17-stale-one-then-false", 0);
    }

    /// <summary>U-AA-03: AA-03-hero-other-contact=0.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_HeroContactIsAnotherEntity()
    {
        ContactVectors.Check("AA-03-hero-other-contact", 0);
    }

    /// <summary>U-AA-04: AA-04-all-last-only=1; AA-05-all-first-only=1; AA-06-all-none=0.</summary>
    [Theory]
    [InlineData("AA-04-all-last-only", 1)]
    [InlineData("AA-05-all-first-only", 1)]
    [InlineData("AA-06-all-none", 0)]
    public void GroupBlockedByMe_0xAA_Type2_AnyMemberCounts(string vector, int expected) => ContactVectors.Check(vector, expected);

    /// <summary>U-AA-05: AA-07-all-but-hero-excludes-hero=0.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_Type3_ExcludesTheHero()
    {
        ContactVectors.Check("AA-07-all-but-hero-excludes-hero", 0);
    }

    /// <summary>U-AA-06: AA-08-self=1; AA-09-self-not=0.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_Type0_SelfAndNotSelf()
    {
        ContactVectors.Check("AA-08-self", 1);
        ContactVectors.Check("AA-09-self-not", 0);
    }

    /// <summary>U-AA-07: AA-10-member-destroyed=0; AA-11-member-deactivated=1; AA-12-member-loaded=1.</summary>
    [Theory]
    [InlineData("AA-10-member-destroyed", 0)]
    [InlineData("AA-11-member-deactivated", 1)]
    [InlineData("AA-12-member-loaded", 1)]
    public void GroupBlockedByMe_0xAA_MemberStatus(string vector, int expected) => ContactVectors.Check(vector, expected);

    /// <summary>U-AA-08: AA-13-rawid-member=1; AA-14-rawid-reference-not-active=0; AA-15-rawid-candidate-destroyed=1.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_RawId_ReferenceAndCandidateStatus()
    {
        ContactVectors.Check("AA-13-rawid-member", 1);
        ContactVectors.Check("AA-14-rawid-reference-not-active", 0);
        ContactVectors.Check("AA-15-rawid-candidate-destroyed", 1);
    }

    /// <summary>U-AA-09: AA-19-real-440-shape=1; AA-20-real-440-other-block=0.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_Real440Shape_ThreeBlocks_TheHeroPushesOne()
    {
        ContactVectors.Check("AA-19-real-440-shape", 1);
        ContactVectors.Check("AA-20-real-440-other-block", 0);
    }

    /// <summary>U-AA-10: AA-21-type11-member=1.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_Type11_CarriedMember()
    {
        ContactVectors.Check("AA-21-type11-member", 1);
    }

    /// <summary>U-AA-11: AA-16-hero-reference-hero-group=1.</summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_HeroIsTheLogicEntity()
    {
        ContactVectors.Check("AA-16-hero-reference-hero-group", 1);
    }


    /// <summary>
    /// U-A9-15 (sm3 critic C2): two distinct entities with the same <c>EntityRefId</c> (-1 here, as the hero's): the logic entity is blocked by the one that is not in the group (the hero): 0. A comparison
    /// of <c>EntityRefId</c> would answer 1.
    /// </summary>
    [Fact]
    public void BlockedByGroup_0xA9_TwoDistinctEntitiesWithTheSameEntityRefId_TheCollisionIsOnTheOneOutsideTheGroup_GivesZero()
    {
        var world = new OpcodeWorld();
        var logic = world.Add(5);
        var twin = world.Add(-1);
        logic.XCollisionEntity = twin;

        var (state, trace) = world.Run(logic, OpcodeWorld.ResultBefore, 0xA9, 0x81);

        Assert.Equal(0, state.Result);
        OpcodeWorld.AssertImplemented(trace, 0xA9, 2);
    }

    /// <summary>
    /// U-AA-12 (sm3 critic C2): the hero (the group) is blocked by an entity that has the logic entity's <c>EntityRefId</c> but is another entity: 0. A comparison of <c>EntityRefId</c> would answer 1.
    /// </summary>
    [Fact]
    public void GroupBlockedByMe_0xAA_TheHeroIsBlockedByADistinctEntityOfTheSameEntityRefId_GivesZero()
    {
        var world = new OpcodeWorld();
        var logic = world.Add(5);
        var twin = world.Add(5);
        world.Hero.XCollisionEntity = twin;

        var (state, trace) = world.Run(logic, OpcodeWorld.ResultBefore, 0xAA, 0x81);

        Assert.Equal(0, state.Result);
        OpcodeWorld.AssertImplemented(trace, 0xAA, 2);
    }

    /// <summary>
    /// U-ALL-01 reduced to its lines A9-01 and AA-01 (sm3): both opcodes return the size of the table (2, never 0) and write nothing but <c>Result</c>; the program <c>[op, 0x81, 0xFF]</c> ends at the 0xFF.
    /// </summary>
    [Fact]
    public void TheTwoOpcodes_ReturnTheirTableSize_AndLeaveEverythingButResultAlone()
    {
        ContactVectors.Check("A9-01-hero-blocks-me", 1);
        ContactVectors.Check("AA-01-hero-blocked-by-me", 1);
    }

    /// <summary>U-ALL-02 (sm3): the search runs from the logic entity, not from the hero (A9-01 answers 1, AA-20 answers 0).</summary>
    [Fact]
    public void TheSearchRunsFromTheLogicEntity_NotFromTheHero()
    {
        ContactVectors.Check("A9-01-hero-blocks-me", 1);
        ContactVectors.Check("AA-20-real-440-other-block", 0);
    }

    /// <summary>
    /// U-ALL-03 reduced to <c>A9 81</c> and <c>AA 81</c> (sm3 critic C7, values deduced from R1 and R2 and replayed on the interpreter of the critique): the owner is a proxy P with no contact, the logic entity a
    /// character L; <c>A9 81</c> with L.XCollisionEntity = the hero: 1; <c>AA 81</c> with the hero's XCollisionEntity = L: 1. A read of the owner instead of the logic entity answers 0 for both.
    /// </summary>
    [Fact]
    public void TheTwoOpcodesReadTheLogicEntity_NotTheOwner()
    {
        var world = new OpcodeWorld();
        var owner = world.Add(7, name: "owner");
        var logic = world.Add(8, name: "logic");
        owner.LogicEntity = logic;
        logic.XCollisionEntity = world.Hero;

        var (a9, a9Trace) = world.Run(owner, OpcodeWorld.ResultBefore, 0xA9, 0x81);

        Assert.Equal(1, a9.Result);
        OpcodeWorld.AssertImplemented(a9Trace, 0xA9, 2);

        logic.XCollisionEntity = null;
        world.Hero.XCollisionEntity = logic;

        var (aa, aaTrace) = world.Run(owner, OpcodeWorld.ResultBefore, 0xAA, 0x81);

        Assert.Equal(1, aa.Result);
        OpcodeWorld.AssertImplemented(aaTrace, 0xAA, 2);
    }

    /// <summary>The 103 vectors of <c>0xA9</c> of the annex (25 written by hand, 48 of the sweep of the search types, 30 seeded random worlds) all replay on the DLL.</summary>
    [Fact]
    public void TheVectorsOfTheOpcode_0xA9_ReplayOnTheDll()
    {
        var vectors = ContactVectors.Vectors.Where(v => v.Opcode == 0xA9).ToList();
        Assert.Equal(103, vectors.Count);
        foreach (var vector in vectors)
        {
            ContactVectors.Check(vector.Id, vector.ExpectResult);
        }
    }

    /// <summary>The 99 vectors of <c>0xAA</c> of the annex (21 written by hand, 48 of the sweep, 30 seeded random worlds) all replay on the DLL.</summary>
    [Fact]
    public void TheVectorsOfTheOpcode_0xAA_ReplayOnTheDll()
    {
        var vectors = ContactVectors.Vectors.Where(v => v.Opcode == 0xAA).ToList();
        Assert.Equal(99, vectors.Count);
        foreach (var vector in vectors)
        {
            ContactVectors.Check(vector.Id, vector.ExpectResult);
        }
    }
}
