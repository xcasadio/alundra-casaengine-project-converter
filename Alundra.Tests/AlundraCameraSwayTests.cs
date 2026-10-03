#nullable enable
using System;
using System.Collections.Generic;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.k1 K1 (docs/plan-e19-opcodes.md, section 1.2k.1): the camera sway state and its per-tick step
/// (<see cref="AlundraCameraSway"/>), the opcodes 0x8E/0x8F through the real interpreter, and the map-load
/// reset. Every sequence below was reproduced by the independent model of the audit (binary
/// <c>0x8002C894</c>).
/// </summary>
public sealed class AlundraCameraSwayTests : IDisposable
{
    public AlundraCameraSwayTests()
    {
        AlundraCameraSway.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        AlundraCameraSway.Instance.ResetForTests();
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }

    private static (int[] X, int[] Y) Steps(AlundraCameraSway sway, int count)
    {
        var xs = new int[count];
        var ys = new int[count];
        for (var i = 0; i < count; i++)
        {
            sway.Step();
            xs[i] = sway.OffsetX;
            ys[i] = sway.OffsetY;
        }

        return (xs, ys);
    }

    // ---------------------------------------------------------------------------------------
    // The step
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Step_1_1_3_2_TrianglesOnBothAxes_ReachFlagsFlipAtTheBounds()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 1, 3, 2);

        var reachX = new List<int>();
        var reachY = new List<int>();
        var xs = new List<int>();
        var ys = new List<int>();
        for (var i = 0; i < 12; i++)
        {
            sway.Step();
            xs.Add(sway.OffsetX);
            ys.Add(sway.OffsetY);
            reachX.Add(sway.ReachX);
            reachY.Add(sway.ReachY);
        }

        Assert.Equal(new[] { -1, -2, -3, -2, -1, 0, 1, 2, 3, 2, 1, 0 }, xs);
        // ReachX passes to 1 at step 3 and back to 0 at step 9.
        Assert.Equal(new[] { 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0 }, reachX);
        Assert.Equal(new[] { -1, -2, -1, 0, 1, 2, 1, 0 }, ys.GetRange(0, 8));
        // ReachY passes to 1 at step 2 and back to 0 at step 6.
        Assert.Equal(new[] { 0, 1, 1, 1, 1, 0 }, reachY.GetRange(0, 6));
    }

    [Fact]
    public void Step_3_1_6_2_ReachesTheBoundsExactly()
    {
        var sway = new AlundraCameraSway();
        sway.Start(3, 1, 6, 2);

        Assert.Equal(new[] { -3, -6, -3, 0, 3, 6, 3, 0 }, Steps(sway, 8).X);
    }

    [Fact]
    public void Step_8_1_8_2_BouncesBetweenTheBoundsAndZero()
    {
        var sway = new AlundraCameraSway();
        sway.Start(8, 1, 8, 2);

        Assert.Equal(new[] { -8, 0, 8, 0 }, Steps(sway, 4).X);
    }

    [Fact]
    public void Step_13_11_1_2_ASpeedBeyondTheLimitClampsToTheLimit()
    {
        var sway = new AlundraCameraSway();
        sway.Start(13, 11, 1, 2);

        var (xs, ys) = Steps(sway, 4);

        Assert.Equal(new[] { -1, 1, -1, 1 }, xs);
        Assert.Equal(new[] { -2, 2, -2, 2 }, ys);
    }

    [Fact]
    public void Step_ChangeInTheMiddle_KeepsOffsetAndReachAndClampsToTheNewLimit()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 1, 3, 2);
        sway.Step();
        sway.Step();
        Assert.Equal(-2, sway.OffsetX);

        sway.Start(8, 1, 8, 2);

        sway.Step();
        Assert.Equal(-8, sway.OffsetX);
        Assert.Equal(1, sway.ReachX);
        Assert.Equal(new[] { 0, 8, 0 }, Steps(sway, 3).X);
    }

    [Fact]
    public void Stop_ThenAStep_ClearsSpeedLimitAndOffset_ButKeepsReach()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 1, 3, 2);
        for (var i = 0; i < 3; i++)
        {
            sway.Step();
        }

        Assert.Equal(1, sway.ReachX);

        sway.Stop();
        Assert.Equal(0, sway.Flag);
        sway.Step();

        Assert.Equal(0, sway.Flag);
        Assert.Equal(0, sway.SpeedX);
        Assert.Equal(0, sway.SpeedY);
        Assert.Equal(0, sway.LimitX);
        Assert.Equal(0, sway.LimitY);
        Assert.Equal(0, sway.OffsetX);
        Assert.Equal(0, sway.OffsetY);
        Assert.Equal(1, sway.ReachX);
        Assert.Equal(1, sway.ReachY);
    }

    [Fact]
    public void Step_ASpeedXOfZeroClearsOffsetX()
    {
        var sway = new AlundraCameraSway();
        sway.Start(0, 1, 3, 2);
        sway.OffsetX = 2;

        sway.Step();

        Assert.Equal(0, sway.OffsetX);
    }

    [Fact]
    public void Step_ASpeedYOfZeroFreezesOffsetYInsideTheBounds()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 0, 3, 2);
        sway.OffsetY = 1;
        sway.ReachY = 0;

        sway.Step();

        Assert.Equal(1, sway.OffsetY);
        Assert.Equal(0, sway.ReachY);
    }

    [Fact]
    public void Step_ASpeedYOfZeroStillClampsAnOffsetYBeyondTheBound_AndFlipsReach()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 0, 3, 2);
        sway.OffsetY = -3;
        sway.ReachY = 0;

        sway.Step();

        Assert.Equal(-2, sway.OffsetY);
        Assert.Equal(1, sway.ReachY);
    }

    [Fact]
    public void Start_AnOperandOf200IsReadAs200_Unsigned()
    {
        var sway = new AlundraCameraSway();

        sway.Start(200, 201, 202, 203);

        Assert.Equal((200, 201, 202, 203), (sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY));
        sway.Step();
        Assert.Equal(-200, sway.OffsetX);
    }

    // ---------------------------------------------------------------------------------------
    // Map load
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void InitializeWithWorld_ClearsOnlyTheFlag_ReachAndTheRestStay()
    {
        var sway = AlundraCameraSway.Instance;
        sway.Start(1, 1, 3, 2);
        sway.ReachX = 1;
        sway.ReachY = 1;
        sway.OffsetX = -3;

        var proxy = new AlundraWorldProxy();
        proxy.InitializeWithWorld(new World { Name = "TestWorld" });

        Assert.Equal(0, sway.Flag);
        Assert.Equal(1, sway.ReachX);
        Assert.Equal(1, sway.ReachY);
        Assert.Equal(3, sway.LimitX);
        Assert.Equal(-3, sway.OffsetX);
    }

    [Fact]
    public void ResetForTests_ClearsAllNineFields_ReachIncluded()
    {
        var sway = AlundraCameraSway.Instance;
        sway.Start(1, 1, 3, 2);
        sway.ReachX = 1;
        sway.ReachY = 1;
        sway.OffsetX = -3;
        sway.OffsetY = -1;

        SaveGameDirectorTestSupport.ResetSingletons();

        Assert.Equal(0, sway.Flag + sway.LimitX + sway.LimitY + sway.SpeedX + sway.SpeedY
            + sway.OffsetX + sway.OffsetY + sway.ReachX + sway.ReachY);
    }

    // ---------------------------------------------------------------------------------------
    // The opcodes through the real interpreter
    // ---------------------------------------------------------------------------------------

    private sealed class SwayContext : IEntityWorldContext
    {
        public AlundraCameraSway? Sway { get; set; }
        public AlundraCameraSway? CameraSway => Sway;
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    private static (EventTraceKind? Kind, int CodeIndex) RunOne(IEntityWorldContext context, int opcode, params int[] codes)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), context);
        var state = new EventProgramState { Codes = document.CodesAsBytes() };
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == opcode)
            {
                kind = record.Kind;
            }
        };
        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);
        return (kind, state.CodeIndex);
    }

    [Fact]
    public void Opcode_0x8E_SetsTheFourValuesAndFlag_Size5()
    {
        var sway = new AlundraCameraSway();
        var context = new SwayContext { Sway = sway };

        var (kind, codeIndex) = RunOne(context, 0x8E, 0x8E, 1, 2, 3, 4, 0xFF);

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(5, codeIndex);
        Assert.Equal((1, 1, 2, 3, 4), (sway.Flag, sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY));
    }

    [Fact]
    public void Opcode_0x8E_ReadsOperandsAsUnsignedBytes()
    {
        var sway = new AlundraCameraSway();

        RunOne(new SwayContext { Sway = sway }, 0x8E, 0x8E, 200, 255, 128, 0, 0xFF);

        Assert.Equal((200, 255, 128, 0), (sway.SpeedX, sway.SpeedY, sway.LimitX, sway.LimitY));
    }

    [Fact]
    public void Opcode_0x8F_ClearsTheFlag_Size1()
    {
        var sway = new AlundraCameraSway();
        sway.Start(1, 1, 3, 2);
        var context = new SwayContext { Sway = sway };

        var (kind, codeIndex) = RunOne(context, 0x8F, 0x8F, 0xFF);

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(1, codeIndex);
        Assert.Equal(0, sway.Flag);
        Assert.Equal(3, sway.LimitX);
    }

    [Theory]
    [InlineData(0x8E, 5)]
    [InlineData(0x8F, 1)]
    public void Opcodes_WithoutASwayState_SkipBySizeAsDegraded(int opcode, int size)
    {
        var codes = new int[size + 1];
        codes[0] = opcode;
        codes[size] = 0xFF;

        var (kind, codeIndex) = RunOne(new SwayContext { Sway = null }, opcode, codes);

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(size, codeIndex);
    }
}
