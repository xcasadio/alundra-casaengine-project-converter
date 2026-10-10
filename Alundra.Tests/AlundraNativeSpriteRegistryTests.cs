#nullable enable
using System;
using Alundra.Scripts;
using CasaEngine.Framework.Scene.Entities;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E14.a0 T2 (docs/plan-e14-ia-native.md section 1.1a, D-E14-1, D-E14-5): the registry of the native sprite handlers
/// consulted by <see cref="AlundraEventProgramRunner.RunSpriteEvent"/> after the save book and E0/E1 - an entry runs for
/// the sprite types it lists and not for another, an absent key stays a counted no-op, the switch turns everything off,
/// and the book and E0/E1 never go through it. In production it holds the Beannoid's three handlers (E14.a1b).
/// </summary>
public sealed class AlundraNativeSpriteRegistryTests : IDisposable
{
    private const int TypeA = 335;
    private const int TypeB = 347;

    public AlundraNativeSpriteRegistryTests()
    {
        Reset();
    }

    public void Dispose()
    {
        Reset();
    }

    private static void Reset()
    {
        AlundraGameState.Instance.ResetForTests();
        AlundraSaveBook.Instance.ResetForTests();
        AlundraSaveScreenDirector.Instance.ResetForTests();
    }

    private static AlundraEventProgramRunner NewRunner() => new(null, AlundraGameState.Instance);

    private static AlundraEntityScriptProxy Entity(int spriteType, int slot, int code)
    {
        var entity = new AlundraEntityScriptProxy { SpriteType = spriteType, Status = EntityStatus.Normal, EventTrigger = slot };
        entity.SpriteProgramIndexes[slot] = code;
        return entity;
    }

    [Fact]
    public void ProductionRunner_RegistryHoldsTheBeannoidsThreeHandlers_AndTheSwitchIsOn()
    {
        var runner = NewRunner();

        Assert.Equal(3, runner.NativeSprites.Count); // E14.a1b: A1, C1 and D1 of the Beannoid (E14.a1c adds the Muruta's three: 6).
        Assert.True(runner.NativeHandlersEnabled);
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramALoad, 1, TypeA, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 1, TypeA, out _));
        Assert.True(runner.NativeSprites.TryGet(ScriptHelper.ProgramDTouch, 1, TypeA, out _));
        Assert.False(runner.NativeSprites.TryGet(ScriptHelper.ProgramCTick, 1, TypeB, out _)); // only the type 335.
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
        AlundraEntityScriptProxy? seen = null;
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
        Entity(TypeB, ScriptHelper.ProgramDTouch, 9).RunPickedEvent(runner);

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
            ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode, new[] { AlundraSaveBook.SpriteType }, (_, _, _) => calls++);

        Entity(AlundraSaveBook.SpriteType, ScriptHelper.ProgramFInteract, AlundraSaveBook.InteractCode).RunPickedEvent(runner);

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
