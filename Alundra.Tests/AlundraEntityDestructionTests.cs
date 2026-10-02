#nullable enable
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.d2b B2 (docs/plan-e19-opcodes.md §1.2h.2, D-E19-29): what blocking between entities exacts with it. T-R2: an entity flagged for
/// destruction leaves the collidable list and stops moving (the binary excludes it from both lists), the soulevables stay in the list
/// (the exclusion is the probe's, D-E19-28). T-R5: the native handler of slot E of index 0 and 1 destroys the entity (<c>0x8007ED10</c> =
/// <c>DestroyEntity(e, -1)</c>), nothing else changes. T-R6: <c>AnimFlags</c> is the byte 0xD of the animation set, loaded by the animation
/// sync (the bit 0x80 of it removes the entity from the collidable list for the duration of the animation).
/// </summary>
public sealed class AlundraEntityDestructionTests
{
    private static AlundraEntityScriptProxy Proxy(EntityStatus status, uint flags = EntityFlags.Collidable)
        => new() { Status = status, Flags = flags };

    // ---- T-R2 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void TR2_BuildCollidables_DropsAnEntityFlaggedForDestruction_AndKeepsTheOthers()
    {
        var normal = Proxy(EntityStatus.Normal);
        var deactivated = Proxy(EntityStatus.Deactivated);
        var loaded = Proxy(EntityStatus.Loaded);
        var destroyed = Proxy(EntityStatus.FlagToDestroy);
        var liftable = Proxy(EntityStatus.Normal, EntityFlags.Collidable | 0x200u);
        var notCollidable = Proxy(EntityStatus.Normal, 0);
        var all = new List<AlundraEntityScriptProxy> { normal, destroyed, deactivated, loaded, liftable, notCollidable };
        var buffer = new List<AlundraEntityScriptProxy>();

        EntitySupport.BuildCollidables(all, buffer);

        Assert.Equal(new[] { normal, deactivated, loaded, liftable }, buffer);
        Assert.DoesNotContain(destroyed, buffer);
        Assert.Contains(liftable, buffer); // a soulevable IS in the list: its exclusion belongs to the probe (D-E19-28).
    }

    [Fact]
    public void TR2_AnEntityFlaggedForDestruction_NoLongerMoves_WhileANormalOneStillDoes()
    {
        var host = new ContactHost();
        var world = ContactWorld.BuildWorld(new FlatGroundField { GroundZ = 48f }, probe: null);
        var walkerNormal = AddWalker(world, host, "normal", 100, EntityStatus.Normal);
        var walkerDoomed = AddWalker(world, host, "doomed", 300, EntityStatus.FlagToDestroy);
        ContactWorld.Integrate(world, host);

        var normalY0 = ContactWorld.Root(walkerNormal).Y;
        var doomedY0 = ContactWorld.Root(walkerDoomed).Y;
        for (var tick = 0; tick < 20; tick++)
        {
            ContactWorld.Integrate(world, host);
        }

        Assert.True(ContactWorld.Root(walkerNormal).Y > normalY0 + 1f,
            "a Normal walker moves south (control: the walk itself works)");
        Assert.Equal(doomedY0, ContactWorld.Root(walkerDoomed).Y);
    }

    private static AlundraEntityScriptProxy AddWalker(CasaEngine.Framework.Scene.World.World world, ContactHost host, string name, int x, EntityStatus status)
    {
        var proxy = ContactWorld.AddEntity(world, host, name, x, 100, 48, -10, -7, 0, 20, 14, 32, status: status);
        proxy.TargetDirection = 0; // south
        proxy.CurrentAnimationId = 5;
        proxy.TargetAnimationId = 5;
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry> { [5] = new AnimSetEntry { Anim = 5, Speed = 64, Acceleration = 0 } };
        return proxy;
    }

    // ---- T-R5 -----------------------------------------------------------------------------------------------------

    private static (AlundraEntityScriptProxy Entity, ContactHost Host, AlundraEventProgramRunner Runner) NativeE(int slot, int spriteIndex, int scriptedIndex = 0)
    {
        var host = new ContactHost();
        var runner = new AlundraEventProgramRunner(null, new AlundraGameState());
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Deactivated, Flags = EntityFlags.Collidable, ScriptHost = host, EventTrigger = slot };
        entity.SpriteProgramIndexes[slot] = spriteIndex;
        entity.ProgramIndexes[slot] = scriptedIndex;
        host.All.Add(entity);
        return (entity, host, runner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TR5_NativeEOfIndexZeroAndOne_DestroysTheEntity(int spriteIndex)
    {
        var (entity, host, runner) = NativeE(ScriptHelper.ProgramEDeactivate, spriteIndex);

        entity.RunPickedEvent(runner);

        Assert.Equal(EntityStatus.FlagToDestroy, entity.Status);
        Assert.Equal((entity, -1), Assert.Single(host.Destroyed));
        Assert.Equal(ScriptHelper.ProgramUnknown, entity.EventTrigger);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(255)]
    [InlineData(-1)]
    public void TR5_NativeEOfAnotherIndex_DoesNotDestroy(int spriteIndex)
    {
        var (entity, host, runner) = NativeE(ScriptHelper.ProgramEDeactivate, spriteIndex);

        entity.RunPickedEvent(runner);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Empty(host.Destroyed);
    }

    [Theory]
    [InlineData(ScriptHelper.ProgramALoad)]
    [InlineData(ScriptHelper.ProgramCTick)]
    [InlineData(ScriptHelper.ProgramDTouch)]
    [InlineData(ScriptHelper.ProgramFInteract)]
    public void TR5_NativeOfAnotherSlot_DoesNotDestroy(int slot)
    {
        var (entity, host, runner) = NativeE(slot, spriteIndex: 0);

        entity.RunPickedEvent(runner);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Empty(host.Destroyed);
    }

    [Fact]
    public void TR5_ScriptedEProgram_DoesNotDestroy_EvenWhenReachedDirectly()
    {
        var (entity, host, runner) = NativeE(ScriptHelper.ProgramEDeactivate, spriteIndex: 0, scriptedIndex: 0x85);

        runner.RunSpriteEvent(entity);

        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Empty(host.Destroyed);
    }

    [Fact]
    public void TR5_AChainOf0x19_DeactivatesThenDestroys_AndTheEntityLeavesTheListOnTheFollowingFrame()
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            EventCodesCTable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = new[] { 0x19, 0xFF },
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState());
        var host = new ContactHost(runner);
        var entity = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, Flags = EntityFlags.Collidable, ScriptHost = host };
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x81; // the program of slot C is 0x19 (Deactivate), no program on slot E.
        host.All.Add(entity);
        host.Rebuild();
        Assert.Contains(entity, host.Collidables);

        // Frame 1: the tick program runs 0x19.
        entity.PickEventTrigger();
        entity.RunPickedEvent(runner);
        host.Rebuild();
        Assert.Equal(EntityStatus.Deactivated, entity.Status);
        Assert.Contains(entity, host.Collidables); // Deactivated stays an obstacle.

        // Frame 2: slot E is empty on the entity: the native handler destroys it.
        entity.PickEventTrigger();
        entity.RunPickedEvent(runner);
        Assert.Equal(EntityStatus.FlagToDestroy, entity.Status);
        Assert.Contains(entity, host.Collidables); // the list is rebuilt at the end of the frame ...

        host.Rebuild(); // ... so the entity is gone one frame after the destroy.
        Assert.DoesNotContain(entity, host.Collidables);
    }

    // ---- T-R6 -----------------------------------------------------------------------------------------------------

    private static (Entity Entity, AlundraEntityScriptProxy Proxy) SpritelessEntity()
    {
        var entity = new Entity
        {
            Name = "e",
            GameplayProxyClassName = nameof(AlundraEntityScriptProxy),
            RootComponent = new TransformComponent(),
        };
        entity.Initialize();
        var proxy = Assert.IsType<AlundraEntityScriptProxy>(entity.GameplayProxy);
        proxy.Status = EntityStatus.Normal;
        proxy.CurrentAnimationId = ~0u;
        proxy.AnimSetsByAnim = new Dictionary<int, AnimSetEntry>
        {
            [0] = new AnimSetEntry { Anim = 0, Acceleration = 0x00 },
            [1] = new AnimSetEntry { Anim = 1, Acceleration = 0xD0 },
        };
        return (entity, proxy);
    }

    [Fact]
    public void TR6_SyncAnimation_CopiesTheWholeByteOfTheAnimationSetIntoAnimFlags()
    {
        var (entity, proxy) = SpritelessEntity();

        proxy.TargetAnimationId = 1;
        AlundraFrameSyncPasses.SyncAnimation(entity);
        Assert.Equal(1u, proxy.CurrentAnimationId);
        Assert.Equal(0xD0, proxy.AnimFlags);

        proxy.TargetAnimationId = 0;
        AlundraFrameSyncPasses.SyncAnimation(entity);
        Assert.Equal(0u, proxy.CurrentAnimationId);
        Assert.Equal(0, proxy.AnimFlags);
    }

    [Fact]
    public void TR6_AnimFlags_IsZeroForAnAnimationTheSetDoesNotHave_AndForNoSet()
    {
        var (entity, proxy) = SpritelessEntity();
        proxy.AnimFlags = 0x80;
        proxy.TargetAnimationId = 7;

        AlundraFrameSyncPasses.SyncAnimation(entity);

        Assert.Equal(7u, proxy.CurrentAnimationId);
        Assert.Equal(0, proxy.AnimFlags);

        var (entity2, proxy2) = SpritelessEntity();
        proxy2.AnimSetsByAnim = null;
        proxy2.AnimFlags = 0x80;
        proxy2.TargetAnimationId = 1;

        AlundraFrameSyncPasses.SyncAnimation(entity2);

        Assert.Equal(0, proxy2.AnimFlags);
    }

    [Fact]
    public void TR6_TheBit0x80OfTheAnimation_RemovesTheEntityFromTheCollidableList()
    {
        var (entity, proxy) = SpritelessEntity();
        proxy.Flags = EntityFlags.Collidable;
        var buffer = new List<AlundraEntityScriptProxy>();

        proxy.TargetAnimationId = 0;
        AlundraFrameSyncPasses.SyncAnimation(entity);
        EntitySupport.BuildCollidables(new[] { proxy }, buffer);
        Assert.Single(buffer);

        proxy.TargetAnimationId = 1; // 0xD0 has the bit 0x80
        AlundraFrameSyncPasses.SyncAnimation(entity);
        EntitySupport.BuildCollidables(new[] { proxy }, buffer);
        Assert.Empty(buffer);
    }
}
