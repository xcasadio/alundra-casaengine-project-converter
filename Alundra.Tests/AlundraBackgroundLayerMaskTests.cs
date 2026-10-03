#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Scripts;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.Depth;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.k2 (docs/plan-e19-opcodes.md, section 1.2k.2, rule K2-R2): the background layer mask opcode <c>0xA4 [b1, b2]</c>.
/// The runner turns <c>b1 &amp; 3</c> into <see cref="IEntityWorldContext.SetBackgroundLayerMask"/>; the backdrop stage
/// turns the two bits into <c>SetLayerActive(id, bit)</c> on the scrolling and cellular services (a layer is addressed by
/// the identifier of the binary, not by its place in the service). A palette bank <c>b2 &gt; 0</c> (the palette cycle,
/// O-E19-43) is not ported: logged once, traced as degraded.
/// </summary>
public sealed class AlundraBackgroundLayerMaskTests
{
    // ---------------------------------------------------------------------------------------
    // The opcode through the real interpreter
    // ---------------------------------------------------------------------------------------

    private sealed class MaskContext : IEntityWorldContext
    {
        public List<int> Masks { get; } = new();
        public bool Handles { get; set; } = true;

        public bool SetBackgroundLayerMask(int mask)
        {
            Masks.Add(mask);
            return Handles;
        }

        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    /// <summary>A context that does not override the member: the default interface implementation ("not handled").</summary>
    private sealed class PlainContext : IEntityWorldContext
    {
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = Array.Empty<AlundraEntityScriptProxy>();
        public AlundraEntityScriptProxy? PlayerEntity => null;
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    private static AlundraEventProgramRunner NewRunner(IEntityWorldContext context, int[] codes, out EventProgramState state)
    {
        var document = new EventProgramDocument
        {
            MapIndex = 1,
            EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
            Codes = codes,
        };
        state = new EventProgramState { Codes = document.CodesAsBytes() };
        return new AlundraEventProgramRunner(document, new AlundraGameState(), context);
    }

    private static (EventTraceKind? Kind, int CodeIndex) RunOne(IEntityWorldContext context, params int[] codes)
    {
        var runner = NewRunner(context, codes, out var state);
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == 0xA4)
            {
                kind = record.Kind;
            }
        };
        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);
        return (kind, state.CodeIndex);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(7, 3)]
    [InlineData(0xFC, 0)]
    public void Opcode_0xA4_PassesTheLowTwoBitsOfTheModeToTheContext_Size3(int mode, int expectedMask)
    {
        var context = new MaskContext();

        var (kind, codeIndex) = RunOne(context, 0xA4, mode, 0, 0xFF);

        Assert.Equal(EventTraceKind.Implemented, kind);
        Assert.Equal(3, codeIndex);
        Assert.Equal(new[] { expectedMask }, context.Masks);
    }

    [Fact]
    public void Opcode_0xA4_WithAPaletteBank_StillSetsTheMask_ButTracesDegraded()
    {
        var context = new MaskContext();

        var (kind, codeIndex) = RunOne(context, 0xA4, 3, 5, 0xFF);

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, codeIndex);
        Assert.Equal(new[] { 3 }, context.Masks);
    }

    [Fact]
    public void Opcode_0xA4_WithAContextWithoutBackdrops_TracesDegraded_AndLogsOnce()
    {
        using var capture = CapturingLogger.Install();
        var runner = NewRunner(new PlainContext(), new[] { 0xA4, 1, 0, 0xFF }, out var state);
        EventTraceKind? kind = null;
        runner.TraceSink = record =>
        {
            if (record.Opcode == 0xA4)
            {
                kind = record.Kind;
            }
        };

        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);
        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, state.CodeIndex);

        state.CodeIndex = 0; // the same runner runs the opcode a second time.
        runner.RunOneScriptCall(new AlundraEntityScriptProxy(), state);

        Assert.Single(capture.Messages.Where(message => message.Contains("0xa4", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Opcode_0xA4_WithAContextThatReportsNotHandled_TracesDegraded()
    {
        var context = new MaskContext { Handles = false };

        var (kind, codeIndex) = RunOne(context, 0xA4, 1, 0, 0xFF);

        Assert.Equal(EventTraceKind.Degraded, kind);
        Assert.Equal(3, codeIndex);
        Assert.Equal(new[] { 1 }, context.Masks);
    }

    // ---------------------------------------------------------------------------------------
    // The backdrop stage: bits to layer identifiers
    // ---------------------------------------------------------------------------------------

    // The shape of map 475: layer 0 cellular, layer 1 scrolling.
    private static (AlundraBackdropStage Stage, ScrollingLayerService Scrolling, CellularLayerService Cellular) NewStageLikeMap475()
    {
        var scrolling = new ScrollingLayerService();
        scrolling.SetLayers(new[]
        {
            new ScrollingLayerDefinition
            {
                StableId = 1,
                FrameTextureAssetIds = new[] { Guid.NewGuid() },
                Pass = RenderPass2D.Background,
                Blend = SpriteBlendMode.Opaque,
            },
        });

        var cellular = new CellularLayerService();
        cellular.SetLayers(new[]
        {
            new CellularLayerDefinition
            {
                LayerId = 0,
                AnimNum = 1,
                Cells = Array.Empty<CellularCellDefinition>(),
                SheetTextureAssetIds = Array.Empty<Guid>(),
            },
        });

        var stage = new AlundraBackdropStage();
        stage.AttachService(scrolling);
        stage.AttachCellularService(cellular);
        return (stage, scrolling, cellular);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(3, true, true)]
    [InlineData(1, true, false)] // bit 0 = layer 0 (the cellular one), bit 1 = layer 1 (the scrolling one).
    [InlineData(2, false, true)]
    public void SetLayerMask_TranslatesTheBitsToTheLayerIdentifiers(int mask, bool cellularActive, bool scrollingActive)
    {
        var (stage, scrolling, cellular) = NewStageLikeMap475();

        var handled = stage.SetLayerMask(mask);

        Assert.True(handled);
        Assert.Equal(cellularActive, cellular.IsLayerActive(0));
        Assert.Equal(scrollingActive, scrolling.IsLayerActive(0));
    }

    [Fact]
    public void SetLayerMask_AfterAMask_ALaterMaskOfThreeTurnsEverythingBackOn()
    {
        var (stage, scrolling, cellular) = NewStageLikeMap475();

        stage.SetLayerMask(0);
        stage.SetLayerMask(3);

        Assert.True(cellular.IsLayerActive(0));
        Assert.True(scrolling.IsLayerActive(0));
    }

    [Fact]
    public void SetLayerMask_WithoutAnyService_LogsOnce_AndStillReportsHandled()
    {
        using var capture = CapturingLogger.Install();
        var stage = new AlundraBackdropStage();

        Assert.True(stage.SetLayerMask(0));
        Assert.True(stage.SetLayerMask(3));

        Assert.Single(capture.Messages.Where(message => message.Contains("no layer service attached", StringComparison.Ordinal)));
    }

    [Fact]
    public void SetLayerMask_WithOnlyOneKindOfService_StillMasksThatKind()
    {
        var (_, scrolling, _) = NewStageLikeMap475();
        var stage = new AlundraBackdropStage();
        stage.AttachService(scrolling);

        Assert.True(stage.SetLayerMask(1)); // layer 1 (scrolling) off.

        Assert.False(scrolling.IsLayerActive(0));
    }

    [Fact]
    public void WorldProxy_ImplementsTheMaskThroughItsBackdropStage()
    {
        var proxy = new AlundraWorldProxy();
        var (stage, scrolling, cellular) = NewStageLikeMap475();
        proxy._backdropStage.AttachService(scrolling);
        proxy._backdropStage.AttachCellularService(cellular);

        IEntityWorldContext context = proxy;
        var handled = context.SetBackgroundLayerMask(0);

        Assert.True(handled);
        Assert.False(cellular.IsLayerActive(0));
        Assert.False(scrolling.IsLayerActive(0));
        Assert.NotNull(stage);
    }

    /// <summary>Captures every log call of one test, then unregisters itself (same private-field reflection precedent as
    /// <c>BackdropStageLoadTests.CapturingWarningLogger</c>).</summary>
    private sealed class CapturingLogger : ILogger, IDisposable
    {
        private readonly object _gate = new();
        private readonly List<string> _messages = new();

        public List<string> Messages
        {
            get
            {
                lock (_gate)
                {
                    return _messages.ToList();
                }
            }
        }

        private void Add(string msg)
        {
            lock (_gate)
            {
                _messages.Add(msg);
            }
        }

        public void Close() { }
        public void WriteTrace(string msg) => Add(msg);
        public void WriteDebug(string msg) => Add(msg);
        public void WriteInfo(string msg) => Add(msg);
        public void WriteWarning(string msg) => Add(msg);
        public void WriteError(string msg) => Add(msg);

        public static CapturingLogger Install()
        {
            var logger = new CapturingLogger();
            Logs.AddLogger(logger);
            return logger;
        }

        public void Dispose()
        {
            var field = typeof(Logs).GetField("_loggers", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            ((List<ILogger>)field!.GetValue(null)!).Remove(this);
        }
    }
}
