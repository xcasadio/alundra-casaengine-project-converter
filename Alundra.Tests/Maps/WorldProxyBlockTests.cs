#nullable enable
using System;
using System.Linq;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;

namespace Alundra.Tests.Maps;

/// <summary>
/// E20.e0c T1 (docs/plan-e20-heros.md section 1.26, (l6), D-E0C-6): <c>Block(self)</c> (<c>0x8003AD30</c>) and <c>Unblock(self)</c> (<c>0x8003ADAC</c>), the members of the world context that freeze the other entities for a key
/// object's message. Block puts the identity of <c>self</c> in <c>BlockedByEntity</c> (<c>+0x20</c>) of every entity of status Normal or Deactivated, the hero included, except <c>self</c> and those already blocked; Unblock
/// clears the ones blocked by <c>self</c>. Only the members are tested here: the world still does not read the field in its clock and its physics (phase D).
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class WorldProxyBlockTests
{
    private static ArcRun Arc14(string name) => new(new ArcSpec("E20e0c-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true));

    private static EntityScriptProxy Spawn(ArcRun arc, int x)
        => ((IEntityWorldContext)arc.Proxy).SpawnEntityBySpriteIndex(arc.Hero, false, 3, x << 16, 100 << 16, 0, 0x10)!;

    [Fact]
    public void Block_FreezesEveryEntityOfStatusNormalOrDeactivated_TheHeroIncluded_ButNotTheBlockerNorTheOtherStatuses()
    {
        using var arc = Arc14("block1");
        arc.OneFrame(); // the entities of the map load take their status 2.
        var context = (IEntityWorldContext)arc.Proxy;
        var blocker = Spawn(arc, 100);
        var normal = Spawn(arc, 110);
        var deactivated = Spawn(arc, 120);
        var loaded = Spawn(arc, 130);
        var dying = Spawn(arc, 140);
        blocker.Status = EntityStatus.Normal;
        normal.Status = EntityStatus.Normal;
        deactivated.Status = EntityStatus.Deactivated;
        loaded.Status = EntityStatus.Loaded;
        dying.Status = EntityStatus.FlagToDestroy;
        arc.Hero.Status = EntityStatus.Normal;
        var named = new[] { blocker, normal, deactivated, loaded, dying };
        var others = arc.Entities.Where(e => !named.Contains(e) && e.Status.IsActive()).ToList();

        context.Block(blocker);

        Assert.NotNull(normal.BlockedByEntity);
        Assert.NotNull(deactivated.BlockedByEntity);
        Assert.NotNull(arc.Hero.BlockedByEntity); // the hero is blocked too.
        Assert.Null(blocker.BlockedByEntity);     // self is excluded,
        Assert.Null(loaded.BlockedByEntity);      // a newborn (status 1) and
        Assert.Null(dying.BlockedByEntity);       // a dying one (status 4) are not in the lists of the binary.
        Assert.Same(normal.BlockedByEntity, deactivated.BlockedByEntity);
        Assert.Same(normal.BlockedByEntity, arc.Hero.BlockedByEntity);
        Assert.All(others, e => Assert.NotNull(e.BlockedByEntity));
    }

    [Fact]
    public void Block_LeavesAnEntityAlreadyBlockedByAnotherOne_WithItsFirstBlocker()
    {
        using var arc = Arc14("block2");
        var context = (IEntityWorldContext)arc.Proxy;
        var first = Spawn(arc, 100);
        var second = Spawn(arc, 110);
        var victim = Spawn(arc, 120);
        first.Status = second.Status = victim.Status = EntityStatus.Normal;

        context.Block(first);
        var firstBlocker = victim.BlockedByEntity;
        Assert.NotNull(firstBlocker);
        Assert.Null(first.BlockedByEntity);
        Assert.NotNull(second.BlockedByEntity);

        context.Block(second);
        Assert.Same(firstBlocker, victim.BlockedByEntity);   // already blocked: excluded.
        Assert.NotNull(first.BlockedByEntity);               // `first` was not blocked: `second` blocks it now,
        Assert.NotSame(firstBlocker, first.BlockedByEntity); // by its own identity.
        Assert.Same(firstBlocker, second.BlockedByEntity);   // `second` itself was blocked by `first` and stays so: the blocker is only excluded from its own blocking.
    }

    [Fact]
    public void Unblock_ClearsOnlyTheEntitiesBlockedByTheCaller_AndAnUnblockOfAnEntityThatBlockedNobodyChangesNothing()
    {
        using var arc = Arc14("block3");
        var context = (IEntityWorldContext)arc.Proxy;
        var first = Spawn(arc, 100);
        var second = Spawn(arc, 110);
        var victim = Spawn(arc, 120);
        first.Status = second.Status = victim.Status = EntityStatus.Normal;

        context.Block(first);                // blocks second, victim and the hero.
        context.Block(second);               // blocks `first` only (the others are already blocked).
        var byFirst = victim.BlockedByEntity;

        context.Unblock(victim);             // victim blocked nobody.
        Assert.Same(byFirst, victim.BlockedByEntity);
        Assert.NotNull(first.BlockedByEntity);

        context.Unblock(second);             // clears `first`, which `second` blocked, and none of the others.
        Assert.Null(first.BlockedByEntity);
        Assert.Same(byFirst, victim.BlockedByEntity);
        Assert.Same(byFirst, arc.Hero.BlockedByEntity);

        context.Unblock(first);              // clears second, victim and the hero.
        Assert.Null(second.BlockedByEntity);
        Assert.Null(victim.BlockedByEntity);
        Assert.Null(arc.Hero.BlockedByEntity);
    }

    [Fact]
    public void Unblock_OfAnEntityThatBlockedNobody_ClearsNothing_WhileAnUnblockOfTheRealBlockerDoes()
    {
        using var arc = Arc14("block4");
        var context = (IEntityWorldContext)arc.Proxy;
        var blocker = Spawn(arc, 100);
        var other = Spawn(arc, 110);
        blocker.Status = other.Status = EntityStatus.Normal;
        var foreign = new Entity { Name = "foreign" };
        other.BlockedByEntity = foreign; // blocked by something that is not an entity of this world: only its own Unblock could clear it.

        context.Unblock(blocker);
        Assert.Same(foreign, other.BlockedByEntity);

        context.Block(blocker);              // `other` is already blocked: it keeps its blocker.
        Assert.Same(foreign, other.BlockedByEntity);
        Assert.NotNull(arc.Hero.BlockedByEntity);
        context.Unblock(blocker);
        Assert.Same(foreign, other.BlockedByEntity);
        Assert.Null(arc.Hero.BlockedByEntity);
    }
}
