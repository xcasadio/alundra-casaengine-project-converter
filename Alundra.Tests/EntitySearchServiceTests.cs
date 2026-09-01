using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// Covers <see cref="EntitySearchService.GetMatchingEntitiesBySearchType"/> - the port of
/// <c>GameEngine.GetMatchingEntityBySearchType</c> every entity-manipulation opcode fans out through. One
/// fact per search type (see the class's own doc for the full list), plus the raw entity-id branch.
/// </summary>
public class EntitySearchServiceTests
{
    private static AlundraEntityScriptProxy NewSpawnedProxy(EntityStatus status = EntityStatus.Normal)
    {
        var entity = new Entity { Name = "e" };
        var proxy = new AlundraEntityScriptProxy { Status = status };
        // Mirrors AlundraWorldProxy.ApplySpawnInitialization: every spawned proxy carries this back-link.
        proxy.LogicContextEntity = entity;
        return proxy;
    }

    // -----------------------------------------------------------------------------------------
    // Raw entity-id search (0x80 clear)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void RawEntityId_MatchesByEntityRefId_WhenOwnerIsActive()
    {
        var owner = NewSpawnedProxy();
        var target = NewSpawnedProxy();
        target.EntityRefId = 42;
        var other = NewSpawnedProxy();
        other.EntityRefId = 7;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 42, new[] { target, other });

        Assert.Equal(new[] { target }, matches);
    }

    [Fact]
    public void RawEntityId_OwnerNotActive_NoMatches()
    {
        var owner = NewSpawnedProxy(EntityStatus.FlagToDestroy);
        var target = NewSpawnedProxy();
        target.EntityRefId = 42;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 42, new[] { target });

        Assert.Empty(matches);
    }

    // -----------------------------------------------------------------------------------------
    // Canned queries (0x80 set)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void FunctionId0_GetOwner_MatchesOwnerOnly()
    {
        var owner = NewSpawnedProxy();
        var other = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x80, new[] { other, owner });

        Assert.Equal(new[] { owner }, matches);
    }

    [Fact]
    public void FunctionId1_GetPlayer_NoPlayerPassed_NoMatches()
    {
        var owner = NewSpawnedProxy();
        var other = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x81, new[] { owner, other });

        Assert.Empty(matches);
    }

    [Fact]
    public void FunctionId1_GetPlayer_ReturnsThePassedPlayerEntity()
    {
        var owner = NewSpawnedProxy();
        var player = NewSpawnedProxy();
        player.IsPlayer = true;
        var other = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(
            owner, 0x81, new[] { owner, player, other }, player);

        Assert.Equal(new[] { player }, matches);
    }

    [Fact]
    public void FunctionId2_AllActiveSpawnedEntities_PlayerIncluded()
    {
        var owner = NewSpawnedProxy();
        var player = NewSpawnedProxy();
        player.IsPlayer = true;
        var destroyed = NewSpawnedProxy(EntityStatus.FlagToDestroy);

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(
            owner, 0x82, new[] { owner, player, destroyed }, player);

        Assert.Equal(new[] { owner, player }, matches);
    }

    [Fact]
    public void FunctionId3_AllActiveSpawnedEntities_PlayerExcluded()
    {
        var owner = NewSpawnedProxy();
        var player = NewSpawnedProxy();
        player.IsPlayer = true;
        var destroyed = NewSpawnedProxy(EntityStatus.FlagToDestroy);

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(
            owner, 0x83, new[] { owner, player, destroyed }, player);

        Assert.Equal(new[] { owner }, matches);
    }

    [Fact]
    public void FunctionId4_OnTheGround_CollidableAndNotFlaggedAndNoPlatform()
    {
        var owner = NewSpawnedProxy();

        var onGround = NewSpawnedProxy();
        onGround.Flags = EntityFlags.Collidable;

        var notCollidable = NewSpawnedProxy();

        var noEntityCollision = NewSpawnedProxy();
        noEntityCollision.Flags = EntityFlags.Collidable;
        noEntityCollision.AnimFlags = 0x80; // EntityAnimFlags.NoEntityCollision

        var onPlatform = NewSpawnedProxy();
        onPlatform.Flags = EntityFlags.Collidable;
        onPlatform.PlatformEntity = new Entity();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(
            owner, 0x84, new[] { onGround, notCollidable, noEntityCollision, onPlatform });

        Assert.Equal(new[] { onGround }, matches);
    }

    [Fact]
    public void FunctionId5_RidingOwner_MatchesByLogicContextEntity()
    {
        var owner = NewSpawnedProxy();
        var mount = NewSpawnedProxy();
        owner.RidingEntity = mount.LogicContextEntity;
        var other = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x85, new[] { mount, other });

        Assert.Equal(new[] { mount }, matches);
    }

    [Fact]
    public void FunctionId6_RidersOfOwner_MatchesByLogicContextEntity()
    {
        var owner = NewSpawnedProxy();
        var rider = NewSpawnedProxy();
        rider.RidingEntity = owner.LogicContextEntity;
        var other = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x86, new[] { rider, other });

        Assert.Equal(new[] { rider }, matches);
    }

    [Fact]
    public void FunctionId7_OwnerXCollisionEntity()
    {
        var owner = NewSpawnedProxy();
        var target = NewSpawnedProxy();
        owner.XCollisionEntity = target; // proxy-typed since E12.d (D-E12D-8).

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x87, new[] { target, NewSpawnedProxy() });

        Assert.Equal(new[] { target }, matches);
    }

    /// <summary>E12.d D-E12D-8's pinning test (docs/plan-e12d-interaction-joueur.md §1.9): the
    /// proxy-typed <c>XCollisionEntity</c> makes functions 7/8 REACHABLE with the player as owner (the
    /// permanent-null field was our deviation, not the original's), and the old latent trap - a null
    /// field ReferenceEquals-matching bare candidates whose <c>LogicContextEntity</c> was null - is
    /// structurally gone: a null field can never equal a candidate reference.</summary>
    [Fact]
    public void FunctionId7_PlayerOwnerWithLiveContact_ReturnsThePointedEntity_AndANullFieldMatchesNothing()
    {
        var player = NewSpawnedProxy();
        player.IsPlayer = true;
        var pointed = NewSpawnedProxy();
        var bare = new AlundraEntityScriptProxy { Status = EntityStatus.Normal }; // LogicContextEntity null.

        player.XCollisionEntity = pointed;
        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(player, 0x87, new[] { pointed, bare, NewSpawnedProxy() });
        Assert.Equal(new[] { pointed }, matches);

        player.XCollisionEntity = null;
        matches = EntitySearchService.GetMatchingEntitiesBySearchType(player, 0x87, new[] { pointed, bare });
        Assert.Empty(matches);
    }

    [Fact]
    public void FunctionId8_EntitiesWhoseXCollisionEntityIsOwner()
    {
        var owner = NewSpawnedProxy();
        var candidate = NewSpawnedProxy();
        candidate.XCollisionEntity = owner; // proxy-typed since E12.d (D-E12D-8).

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x88, new[] { candidate, NewSpawnedProxy() });

        Assert.Equal(new[] { candidate }, matches);
    }

    [Fact]
    public void FunctionId9_ChildrenOfOwner()
    {
        var owner = NewSpawnedProxy();
        var child = NewSpawnedProxy();
        child.ParentEntity = owner.LogicContextEntity;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x89, new[] { child, NewSpawnedProxy() });

        Assert.Equal(new[] { child }, matches);
    }

    [Fact]
    public void FunctionId10_OwnersParent()
    {
        var owner = NewSpawnedProxy();
        var parent = NewSpawnedProxy();
        owner.ParentEntity = parent.LogicContextEntity;

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x8A, new[] { parent, NewSpawnedProxy() });

        Assert.Equal(new[] { parent }, matches);
    }

    [Fact]
    public void FunctionId11_EveryEntityOnAPlatform()
    {
        var owner = NewSpawnedProxy();
        var onPlatform = NewSpawnedProxy();
        onPlatform.PlatformEntity = new Entity();
        var notOnPlatform = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x8B, new[] { onPlatform, notOnPlatform });

        Assert.Equal(new[] { onPlatform }, matches);
    }

    [Fact]
    public void IllegalFunctionId_NoMatches_DoesNotThrow()
    {
        var owner = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0xFF, new[] { owner });

        Assert.Empty(matches);
    }

    [Fact]
    public void EmptySpawnedList_NoMatches()
    {
        var owner = NewSpawnedProxy();

        var matches = EntitySearchService.GetMatchingEntitiesBySearchType(owner, 0x84, System.Array.Empty<AlundraEntityScriptProxy>());

        Assert.Empty(matches);
    }
}
