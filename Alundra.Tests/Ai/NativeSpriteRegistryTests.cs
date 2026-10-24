#nullable enable
using System;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Objects;
using Alundra.Ai.Projectiles;
using Alundra.UI.Saving;

namespace Alundra.Tests.Ai;

/// <summary>
/// E14.a0 T2 (docs/plan-e14-ia-native.md section 1.1a, D-E14-1, D-E14-5): the registry of the native sprite handlers
/// consulted by <see cref="EventProgramRunner.RunSpriteEvent"/> after the save book and E0/E1 - an entry runs for
/// the sprite types it lists and not for another, an absent key stays a counted no-op, the switch turns everything off,
/// and the book and E0/E1 never go through it. In production it holds the Beannoid's three handlers (E14.a1b) and the Muruta's three (E14.a1c).
/// </summary>
public sealed class NativeSpriteRegistryTests : IDisposable
{
    private const int TypeA = 335;
    private const int TypeB = 347;

    public NativeSpriteRegistryTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        GameState.Instance.ResetForTests();
        SaveBook.Instance.ResetForTests();
        SaveScreenDirector.Instance.ResetForTests();
    }

    private static EventProgramRunner NewRunner() => new(null, GameState.Instance);

    private static EntityScriptProxy Entity(int spriteType, int slot, int code)
    {
        var entity = new EntityScriptProxy { SpriteType = spriteType, Status = EntityStatus.Normal, EventTrigger = slot };
        entity.SpriteProgramIndexes[slot] = code;
        return entity;
    }

    [Fact]
    public void ProductionRunner_RegistryHoldsTheHandlersOfTheBeannoidTheMurutaTheSwordWaveAndThePickupObjects_AndTheSwitchIsOn()
    {
        var runner = NewRunner();

        // E14.a1b: A1, C1 and D1 of the Beannoid (335); E14.a1c: A0, C10 and D9 of the Muruta (347); E20.f3c (D-F3-6): (C, 61) of the sword wave (27 to 29); E20.e0c: A255 and C255 of the pickup objects (31 to 118 but 72), the keys
        // being read from PickupAi so that the pin follows the handlers it counts.
        Assert.Equal(6 + 1 + PickupAi.Keys.Count + ChargedArrowAi.Keys.Count + ArrowAi.Keys.Count + PillarAi.Keys.Count + LiftableAi.Keys.Count + LiftableAi.BreakKeys.Count + LiftableAi.C4Keys.Count, runner.NativeSprites.Count); // E20.f4: and the (C, 94) of the charged arrow; E20.f5: and the (E, 17) of the normal arrows; E20.g5a: and the (C, 76) of the pillar; E20.g1a: and the (C, 23) of the liftable objects; E20.g2b: and the (E, 2) of the ten breakable types; E20.g2c1: and the (C, 4) of the 19 types of C4.
        Assert.Equal(2, PickupAi.Keys.Count);
        Assert.All(PickupAi.Keys, key => Assert.All(PickupAi.SpriteTypes, type => Assert.True(runner.NativeSprites.TryGet(key.Slot, key.Code, type, out _), $"key {key} type {type}")));
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, PickupAi.Code, 30, out _)); // the chest (type 30) has no handler: it stays a named stub (D-E0C-2).
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, PickupAi.Code, 72, out _));
        Assert.True(runner.NativeHandlersEnabled);
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramALoad, 1, TypeA, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 1, TypeA, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramDTouch, 1, TypeA, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramALoad, 0, TypeB, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 10, TypeB, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramDTouch, 9, TypeB, out _));
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 1, TypeB, out _)); // each handler only for its own type.
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 10, TypeA, out _));
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramALoad, 0, TypeA, out _));
    }

    [Fact]
    public void TheJournalOfTheNativeHandlers_CountsEachKeyThatRan()
    {
        // E14.a1c: the arcs count the calls of a handler (D9) directly from this journal, never from the life points.
        var runner = NewRunner();
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramDTouch, 25, new[] { TypeA }, (_, _, _) => { });

        Entity(TypeA, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner);
        Entity(TypeA, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner);
        Entity(TypeB, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner); // another type: a counted no-op, not in the journal.

        Assert.Equal(2, runner.NativeHandlerRunCount);
        Assert.Equal(2, runner.NativeHandlerRunCounts[(ScriptHelper.ProgramDTouch, 25)]);
        Assert.Single(runner.NativeHandlerRunCounts);
    }

    [Fact]
    public void ProductionRunner_NoRegistrationListsTheTypeMinus1()
    {
        // E14.a0b P4: Register accepts any type; the type -1 is the harnesses' bare pawn (no sprite record), which no native handler may reach.
        var runner = NewRunner();

        for (var slot = 0; slot < 6; slot++)
        {
            for (var code = 0; code < 400; code++)
            {
                Assert.False(runner.NativeSprites.TryGet(slot, code, -1, out _), $"slot {slot} code {code} lists the type -1");
            }
        }
    }

    [Fact]
    public void RegisteredEntry_RunsForItsType_AndNotForAnother()
    {
        var runner = NewRunner();
        var calls = 0;
        EntityScriptProxy? seen = null;
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeA }, (e, _, _) =>
        {
            calls++;
            seen = e;
        });

        var own = Entity(TypeA, ScriptHelper.ProgramCTick, 25);
        own.RunPickedEvent(runner);
        Assert.Equal(1, calls);
        Assert.Same(own, seen);
        Assert.Equal(1, runner.NativeHandlerRunCount);
        Assert.Equal(1, runner.SpriteEventRunCount);

        Entity(TypeB, ScriptHelper.ProgramCTick, 25).RunPickedEvent(runner);
        Assert.Equal(1, calls);
        Assert.Equal(1, runner.NativeHandlerRunCount);
        Assert.Equal(2, runner.SpriteEventRunCount); // the other type is a counted no-op.
    }

    [Fact]
    public void RegisteredEntry_ListingSeveralTypes_RunsForEach()
    {
        var runner = NewRunner();
        var calls = 0;
        // (D, 25): a free key (the key (D, 1) is the Beannoid's D1 in production since E14.a1b).
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramDTouch, 25, new[] { TypeA, TypeB }, (_, _, _) => calls++);

        Entity(TypeA, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner);
        Entity(TypeB, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner);

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(ScriptHelper.ProgramCTick, 26)] // another code of the registered slot.
    [InlineData(ScriptHelper.ProgramDTouch, 25)] // the registered code of another slot.
    public void AbsentKey_IsACountedNoOp(int slot, int code)
    {
        var runner = NewRunner();
        var calls = 0;
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeA }, (_, _, _) => calls++);

        var entity = Entity(TypeA, slot, code);
        entity.RunPickedEvent(runner);
        entity.EventTrigger = slot;
        entity.RunPickedEvent(runner);

        Assert.Equal(0, calls);
        Assert.Equal(0, runner.NativeHandlerRunCount);
        Assert.Equal(2, runner.SpriteEventRunCount);
        Assert.Equal(ScriptHelper.ProgramUnknown, entity.EventTrigger);
    }

    [Fact]
    public void EmptyRegistry_EveryEntity_IsACountedNoOp()
    {
        var runner = NewRunner();

        Entity(TypeA, ScriptHelper.ProgramCTick, 25).RunPickedEvent(runner);
        // (D, 25): a free key (the key (D, 9) is the Muruta's D9 in production since E14.a1c); an unregistered key stays a counted no-op.
        Entity(TypeB, ScriptHelper.ProgramDTouch, 25).RunPickedEvent(runner);

        Assert.Equal(2, runner.SpriteEventRunCount);
        Assert.Equal(0, runner.NativeHandlerRunCount);
        Assert.Equal(0, runner.SaveBookEventRunCount);
    }

    [Fact]
    public void Switch_Off_TurnsTheRegistryOff_AndOnAgainRestoresIt()
    {
        var runner = NewRunner();
        var calls = 0;
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeA }, (_, _, _) => calls++);

        runner.NativeHandlersEnabled = false;
        Entity(TypeA, ScriptHelper.ProgramCTick, 25).RunPickedEvent(runner);
        Assert.Equal(0, calls);
        Assert.Equal(0, runner.NativeHandlerRunCount);
        Assert.Equal(1, runner.SpriteEventRunCount);

        runner.NativeHandlersEnabled = true;
        Entity(TypeA, ScriptHelper.ProgramCTick, 25).RunPickedEvent(runner);
        Assert.Equal(1, calls);
        Assert.Equal(1, runner.NativeHandlerRunCount);
    }

    [Fact]
    public void ScriptProgramOnTheSlot_IsNeverANativeHandler()
    {
        var runner = NewRunner();
        var calls = 0;
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeA }, (_, _, _) => calls++);
        var entity = Entity(TypeA, ScriptHelper.ProgramCTick, 25);
        entity.ProgramIndexes[ScriptHelper.ProgramCTick] = 0x85;

        runner.RunSpriteEvent(entity);

        Assert.Equal(0, calls);
        Assert.Equal(1, runner.SpriteEventRunCount);
    }

    [Fact]
    public void SaveBook_DoesNotGoThroughTheRegistry_EvenWhenTheKeyIsRegistered()
    {
        var runner = NewRunner();
        var calls = 0;
        using var registration = runner.NativeSprites.Register(
            ScriptHelper.ProgramFInteract, SaveBook.InteractCode, new[] { SaveBook.SpriteType }, (_, _, _) => calls++);

        Entity(SaveBook.SpriteType, ScriptHelper.ProgramFInteract, SaveBook.InteractCode).RunPickedEvent(runner);

        Assert.Equal(0, calls);
        Assert.Equal(1, runner.SaveBookEventRunCount);
        Assert.Equal(0, runner.NativeHandlerRunCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DeactivateIndexes0And1_DoNotGoThroughTheRegistry(int code)
    {
        var runner = NewRunner();
        var calls = 0;
        using var registration = runner.NativeSprites.Register(ScriptHelper.ProgramEDeactivate, code, new[] { TypeA }, (_, _, _) => calls++);

        var entity = Entity(TypeA, ScriptHelper.ProgramEDeactivate, code);
        entity.RunPickedEvent(runner);

        Assert.Equal(0, calls);
        Assert.Equal(0, runner.NativeHandlerRunCount);
        Assert.Equal(1, runner.SpriteEventRunCount);
    }

    [Fact]
    public void Registering_TheSameKeyTwice_Throws_AndDisposingRemovesTheEntry()
    {
        var runner = NewRunner();
        var countBefore = runner.NativeSprites.Count; // the production entries stay (relative count, never an absolute 0).
        var registration = runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeA }, (_, _, _) => { });

        Assert.Throws<InvalidOperationException>(
            () => runner.NativeSprites.Register(ScriptHelper.ProgramCTick, 25, new[] { TypeB }, (_, _, _) => { }));

        registration.Dispose();
        Assert.Equal(countBefore, runner.NativeSprites.Count);
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 25, TypeA, out _));
    }
}
