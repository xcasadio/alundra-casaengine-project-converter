#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using Alundra.Entities;
using Alundra.Combat;

namespace Alundra.Tests;

/// <summary>
/// E20.c1b K3 (docs/plan-e20-heros.md section 1.6): the stored HitBox (<c>+0x1FC/+0x200/+0x204</c>) of the helper <c>0x80038064</c>: <c>Pos + CollisionOffset</c>, written when the frame shown has a box, at the creation
/// (<c>0x80039F30</c>) and after the movement of every tick (<c>0x80038364</c>) for every entity, whether or not it can attack. The results of the hit pass of E20.c1a do not move (their own tests stay green unchanged).
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraStoredHitBoxTests
{
    private const int Unset = 0x5A5A5A5A;

    /// <summary>A bare entity with a sprite whose animation 0 (down) carries one box keyframe (or none), at a position, as the hit-pass tests build them.</summary>
    private static AlundraEntityScriptProxy Entity(int x, int y, int z, (int Ox, int Oy, int Oz, int W, int D, int H)? box)
    {
        var sprite = new AnimatedSpriteComponent();
        var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        var data = new Animation2dData { Name = "bank_anim0_down", AnimationType = AnimationType.Loop };
        data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
        var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
        track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(1f, spriteId));
        data.Tracks.Add(track);
        var keyframe = new Animation2dCollisionKeyframeData { TimeSeconds = 0f };
        if (box is { } b)
        {
            keyframe.Fixtures.Add(new ColliderFixture
            {
                Shape = new Box { Size = new Vector3(b.W, b.D, b.H) },
                LocalPosition = new Vector3(b.Ox + b.W / 2f, b.Oy + b.D / 2f, b.Oz + b.H / 2f),
            });
        }

        data.CollisionKeyframes.Add(keyframe);
        sprite.AddAnimation(new Animation2d(data));

        var entity = new Entity { Name = "actor", GameplayProxyClassName = typeof(AlundraEntityScriptProxy).FullName!, RootComponent = sprite };
        entity.Initialize();
        var proxy = (AlundraEntityScriptProxy)entity.GameplayProxy;
        proxy.LogicContextEntity = entity;
        proxy.Status = EntityStatus.Normal;
        proxy.PosX = x << 16;
        proxy.PosY = y << 16;
        proxy.PosZ = z << 16;
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [0] = new AnimSetEntry { Anim = 0, Acceleration = 0 } };
        proxy.HitBoxX = proxy.HitBoxY = proxy.HitBoxZ = Unset;
        return proxy;
    }

    [Fact]
    public void TheCreation_StoresTheHitBoxOfTheFirstFrame_ForAnEntityThatCannotAttack()
    {
        var proxy = Entity(100, 60, 8, (3, -2, 1, 8, 8, 8));
        proxy.SpriteType = 0x100 + 79; // a Beannoid: it has a record, but this proxy's attack is not what is under test.

        proxy.InitializeBalance(AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()), 0);

        Assert.True(proxy.HasFrameBox);
        Assert.Equal((100 << 16) + (3 << 16), proxy.HitBoxX);
        Assert.Equal((60 << 16) + (-2 << 16), proxy.HitBoxY);
        Assert.Equal((8 << 16) + (1 << 16), proxy.HitBoxZ);
    }

    [Fact]
    public void ANoBoxFrame_LeavesTheHitBoxAsItWas()
    {
        var proxy = Entity(100, 60, 8, null);
        proxy.SpriteType = 0x100 + 79;

        proxy.InitializeBalance(AlundraBalanceTables.GetOrCreate(ProjectRootFinder.Find()), 0);
        proxy.StoreHitBox();

        Assert.False(proxy.HasFrameBox);
        Assert.Equal(Unset, proxy.HitBoxX);
        Assert.Equal(Unset, proxy.HitBoxY);
        Assert.Equal(Unset, proxy.HitBoxZ);
    }

    [Fact]
    public void TheMovementOfATick_IsFollowedByTheStore_PositionPlusOffset_NotModdedPos()
    {
        var proxy = Entity(100, 60, 0, (3, -2, 1, 8, 8, 8));
        proxy.ScriptHost = new ContactHost();
        proxy.LoadAnimationSwitch(0, 0, fromClock: false);
        proxy.ModX = 7 << 16; // the body offset is not what the box adds.

        proxy.TickPhysics();

        Assert.Equal((proxy.PosX) + (3 << 16), proxy.HitBoxX);
        Assert.Equal((proxy.PosY) + (-2 << 16), proxy.HitBoxY);
        Assert.Equal((proxy.PosZ) + (1 << 16), proxy.HitBoxZ);
    }

    [Fact]
    public void TheWorldsTick_StoresTheBoxOfAnEntityWithNoAttack_AfterThePhysics()
    {
        using var arena = new HitArena("K3-stored", 232, 96, new List<int[]> { new[] { 252, 96 } });
        var beannoid = arena.Targets[0];
        beannoid.CurrentAttack = null; // the hit pass skips it, so only the store can write its HitBox.
        beannoid.HitBoxX = beannoid.HitBoxY = beannoid.HitBoxZ = Unset;

        arena.Frame(0, 1);

        Assert.True(beannoid.HasFrameBox, "the Beannoid's frame shows a box");
        Assert.Equal(beannoid.PosX + beannoid.CollisionOffsetX, beannoid.HitBoxX);
        Assert.Equal(beannoid.PosY + beannoid.CollisionOffsetY, beannoid.HitBoxY);
        Assert.Equal(beannoid.PosZ + beannoid.CollisionOffsetZ, beannoid.HitBoxZ);
        Assert.NotEqual(Unset, beannoid.HitBoxX);
    }
}
