#nullable enable
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Unit tests for <see cref="AlundraEntityCollision.FindEntityCollisionCandidate"/> (E12.d,
/// docs/plan-e12d-interaction-joueur.md D-E12D-1) - the port of the original's asymmetric AABB probe
/// (PhysicsEngine.cs:1169-1283). The function is unit-agnostic (it only compares the caller's own
/// numbers), so these montages use small plain integers for legibility; the real-data path is covered
/// by the production-site and full-flow tests.
/// </summary>
public class AlundraEntityCollisionTests
{
    private static AlundraEntityScriptProxy NewBox(int x, int y, int z, int w, int h, int d)
        => new()
        {
            Flags = EntityFlags.Collidable,
            PosX = x,
            PosY = y,
            PosZ = z,
            Width = w,
            Height = h,
            Depth = d,
        };

    [Fact]
    public void OverlappingBoxes_AreDetected_AndFirstListMatchWins()
    {
        var subject = NewBox(10, 10, 10, 4, 4, 4);
        var first = NewBox(12, 12, 12, 4, 4, 4);
        var second = NewBox(11, 11, 11, 4, 4, 4);

        var found = AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { first, second });

        // Both overlap; the original returns on the FIRST match in list order.
        Assert.Same(first, found);
    }

    [Fact]
    public void FlushContact_Counts_TheDerivedPlusOne()
    {
        // Candidate to the LEFT by exactly its own width: dif == Width, and the original's
        // `dif < Width + 1` (⇔ dif <= Width) makes flush contact a hit; one more unit is a miss.
        var subject = NewBox(10, 0, 0, 4, 4, 4);
        var flush = NewBox(10 - 4, 0, 0, 4, 4, 4);
        var apart = NewBox(10 - 5, 0, 0, 4, 4, 4);

        Assert.Same(flush, AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { flush }));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { apart }));
    }

    [Fact]
    public void Asymmetry_NegativeDelta_TestsTheCandidatesDimension_NotTheSubjects()
    {
        // Mutation №6's kill case (plan §3): candidate WIDER than the subject, sitting to the left at a
        // distance only ITS OWN width covers. dif = 8, candidate.Width = 10 (hit: 8 <= 10) but
        // subject.Width = 2 - a symmetrized port testing the subject's dimension would answer null.
        var subject = NewBox(10, 0, 0, 2, 2, 2);
        var wideOnTheLeft = NewBox(10 - 8, 0, 0, 10, 10, 10);

        Assert.Same(wideOnTheLeft, AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { wideOnTheLeft }));

        // And the mirror image: candidate to the RIGHT at the same distance - now the SUBJECT's own
        // width (2) is the threshold and 8 > 2 must miss, even though the candidate is 10 wide.
        var wideOnTheRight = NewBox(10 + 8, 0, 0, 10, 10, 10);
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { wideOnTheRight }));
    }

    [Fact]
    public void EveryAxis_CanRejectAlone()
    {
        var subject = NewBox(0, 0, 0, 4, 4, 4);

        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { NewBox(20, 0, 0, 4, 4, 4) }));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { NewBox(0, 20, 0, 4, 4, 4) }));
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { NewBox(0, 0, 20, 4, 4, 4) }));
    }

    [Fact]
    public void SubjectGate_NotCollidable_NoEntityCollisionAnim_OrCarried_AnswersNull()
    {
        var overlapped = NewBox(0, 0, 0, 4, 4, 4);

        var notCollidable = NewBox(0, 0, 0, 4, 4, 4);
        notCollidable.Flags = 0;
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(notCollidable, new[] { overlapped }));

        var noCollisionAnim = NewBox(0, 0, 0, 4, 4, 4);
        noCollisionAnim.AnimFlags = 0x80; // EntityAnimFlags.NoEntityCollision.
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(noCollisionAnim, new[] { overlapped }));

        var carried = NewBox(0, 0, 0, 4, 4, 4);
        carried.PlatformEntity = new CasaEngine.Framework.Scene.Entities.Entity();
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(carried, new[] { overlapped }));
    }

    [Fact]
    public void PositionsAreRecomputedOnTheFly_DetectionFollowsAMovedSubject()
    {
        // Mutation №7's kill case (plan §3, D-E12D-1's P2 correction): a port reading the cached
        // ModdedPos* fields - written only at spawn in this DLL - would keep answering for the OLD
        // position. Move the subject onto the candidate AFTER construction: detection must follow.
        var subject = NewBox(100, 100, 100, 4, 4, 4);
        var candidate = NewBox(0, 0, 0, 4, 4, 4);
        // Stale-cache simulation: freeze the spawn-time cache at the FAR position, like the real
        // spawn factory does once.
        subject.ModdedPosX = 100;
        subject.ModdedPosY = 100;
        subject.ModdedPosZ = 100;

        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { candidate }));

        subject.PosX = 2;
        subject.PosY = 2;
        subject.PosZ = 2; // cached ModdedPos* deliberately NOT refreshed.

        Assert.Same(candidate, AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { candidate }));
    }

    [Fact]
    public void TheSubjectItself_IsSkipped()
    {
        var subject = NewBox(0, 0, 0, 4, 4, 4);
        Assert.Null(AlundraEntityCollision.FindEntityCollisionCandidate(subject, new[] { subject }));
    }
}
