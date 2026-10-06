#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.h1b2 (docs/plan-e19-opcodes.md §1.2n.1c, H1B2-R1): the opcodes <c>0x20</c> (<c>0x8003D9BC</c>, size 3), <c>0x22</c> (<c>0x8003DA70</c>, size 1) and
/// <c>0x23</c> (<c>0x8003DB28</c>, size 1). Every value is a row of <c>docs/plan-e19-h1b2-annexe/handlers_emu.txt</c> (the real code of the binary run in an
/// interpreter), written before the code. Same montage as <see cref="AlundraZWaitOpcodesTests"/>: the opcode sits at pc 1 behind a <c>0x01</c>.
/// </summary>
public class AlundraHeightWaitOpcodesTests
{
    private const string NoRecordWarning = "Opcode 0x22 without an entity record: the wait ends.";

    private static EventProgramDocument NewDocument(params int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    private static (AlundraEventProgramRunner Runner, List<EventTraceRecord> Trace) NewRunner(EventProgramDocument document)
    {
        var runner = new AlundraEventProgramRunner(document, new AlundraGameState(), null);
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        return (runner, trace);
    }

    private static EventProgramState StateFor(EventProgramDocument document) => new() { Codes = document.CodesAsBytes() };

    private static AlundraEntityScriptProxy NewEntity(int? recordHeight = null) =>
        new() { EntityRefId = 1, Status = EntityStatus.Normal, RecordHeight = recordHeight };

    private static (int CodeIndex, int Opcode, int Size)[] Shape(List<EventTraceRecord> trace) =>
        trace.Select(r => (r.CodeIndex, r.Opcode, r.Size)).ToArray();

    // ---- 0x20 ----

    [Fact]
    public void TZ20_WaitsForTheZDistance_FromTheFirstCall_AndDoesNotReadTheContact()
    {
        var document = NewDocument(0x01, 0x20, 0x10, 0x00, 0xFF); // 16 px
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x20, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.Implemented, trace[^1].Kind);
        Assert.Equal(1, state.CodeIndex);
        Assert.Equal(1, state.Parameters[1]);
        Assert.Equal(3145728, state.Parameters[2]);

        owner.PosZ = 4194303; // 15.99 px up: still waiting.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 0) }, Shape(trace));

        owner.PosZ = 3145728;
        owner.CollidedWithEntityZ = 1; // a contact alone does not end it (unlike 0x21).
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 0) }, Shape(trace));

        owner.CollidedWithEntityZ = 0;
        owner.PosZ = 4194304; // 16 px up: ends (size 3), then 0xFF at 4.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 3), (4, 0xFF, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.End, trace[^1].Kind);
    }

    [Fact]
    public void TZ20_TheDistanceIsAbsolute_DownwardEndsToo_AndSixteenPixelsDownIsExact()
    {
        var document = NewDocument(0x01, 0x20, 0x10, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);

        owner.PosZ = 2097153; // 15.99 px down: still waiting.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 0) }, Shape(trace));

        owner.PosZ = 2097152; // 16 px down.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ20_TheHighByteOfTheDistanceCounts_256Px()
    {
        var document = NewDocument(0x01, 0x20, 0x00, 0x01, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);

        owner.PosZ = 19922943;
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 0) }, Shape(trace));

        owner.PosZ = 19922944; // 256 px up.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    [Fact]
    public void TZ20_ALimitOfZeroStillEndsOnlyAtTheSecondCall()
    {
        var document = NewDocument(0x01, 0x20, 0x00, 0x00, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity();
        owner.PosZ = 3145728;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x20, 0) }, Shape(trace));

        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x20, 3), (4, 0xFF, 0) }, Shape(trace));
    }

    // ---- 0x22 ----

    /// <summary>The first call (memo) at (<paramref name="firstPosZ"/>, 0), then the call under test at the given state: the result of the opcode (0 or 1) and the force it leaves.</summary>
    private static (int Result, int ForceZ, int Memo) SecondCall22(int height, int posZ, int forceZ, int contact = 0)
    {
        var document = NewDocument(0x01, 0x22, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity(height);
        owner.PosZ = posZ;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x22, 0) }, Shape(trace));

        owner.ForceZ = forceZ;
        owner.CollidedWithEntityZ = contact;
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        var result = trace.Count == 2 && trace[0].Opcode == 0x22 && trace[0].Size == 1 ? 1 : 0;
        Assert.Equal(result == 1 ? new[] { (1, 0x22, 1), (2, 0xFF, 0) } : new[] { (1, 0x22, 0) }, Shape(trace));
        return (result, owner.ForceZ, state.Parameters[2]);
    }

    [Fact]
    public void TZ22_TheFirstCallMemorisesTheLiteralTargetOfTheRecordHeight_AndLeavesTheForce()
    {
        var document = NewDocument(0x01, 0x22, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity(20);
        owner.PosZ = 10485000;
        owner.ForceZ = 32768;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);

        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x22, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.Implemented, trace[^1].Kind);
        Assert.Equal(1, state.Parameters[1]);
        Assert.Equal(10485760, state.Parameters[2]); // 20 << 19, no conversion of convention.
        Assert.Equal(32768, owner.ForceZ);
    }

    [Fact]
    public void TZ22_ClampsTheForceToTheGapWhenItWouldOvershoot_AndNeverPushes()
    {
        // (PosZ, ForceZ) -> (result, ForceZ out), height 20 (target 10485760): rows of handlers_emu.txt.
        Assert.Equal((0, 760), Drop(SecondCall22(20, 10485000, 32768)));
        Assert.Equal((0, 700), Drop(SecondCall22(20, 10485000, 700)));
        Assert.Equal((1, 0), Drop(SecondCall22(20, 10485760, 0)));
        Assert.Equal((0, -1), Drop(SecondCall22(20, 10485761, -5)));
        Assert.Equal((0, 485760), Drop(SecondCall22(20, 10000000, 600000))); // 485760: the gap.

        // The faithful stalls: no force is created or reversed.
        Assert.Equal((0, 0), Drop(SecondCall22(20, 10485761, 0)));
        Assert.Equal((0, -32768), Drop(SecondCall22(20, 10000000, -32768)));

        // Equality ends the wait at once, the force untouched; the contact does not matter to 0x22.
        Assert.Equal((1, 32768), Drop(SecondCall22(20, 10485760, 32768, contact: 1)));
    }

    private static (int Result, int ForceZ) Drop((int Result, int ForceZ, int Memo) row) => (row.Result, row.ForceZ);

    [Fact]
    public void TZ22_WithoutARecord_TheWaitEndsAtOnce_WithOneWarning()
    {
        using var warnings = CapturingWarnings.Install();
        var document = NewDocument(0x01, 0x22, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity(); // no record: the hero and the bare proxies.
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x22, 1), (2, 0xFF, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.Implemented, trace[1].Kind);

        var again = StateFor(document);
        trace.Clear();
        runner.RunOneScriptCall(owner, again);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x22, 1), (2, 0xFF, 0) }, Shape(trace));

        Assert.Equal(1, warnings.Messages.Count(m => m.Contains(NoRecordWarning)));
    }

    // ---- 0x23 ----

    [Fact]
    public void TZ23_IsTheHeightWaitOrAZContact_AndTheContactEndsItFarFromTheTarget()
    {
        var document = NewDocument(0x01, 0x23, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity(30); // target 15728640.
        owner.PosZ = 15000000;
        var state = StateFor(document);

        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x23, 0) }, Shape(trace));
        Assert.Equal(EventTraceKind.Implemented, trace[^1].Kind);
        Assert.Equal(15728640, state.Parameters[2]);

        owner.ForceZ = 32768; // the gap is 728640: no clamp.
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x23, 0) }, Shape(trace));
        Assert.Equal(32768, owner.ForceZ);

        owner.CollidedWithEntityZ = 1; // a contact far from the target ends it; the force is still clamped first (here: untouched).
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x23, 1), (2, 0xFF, 0) }, Shape(trace));
        Assert.Equal(32768, owner.ForceZ);
    }

    [Fact]
    public void TZ23_TheTargetEndsItToo_AndAContactAlreadyPostedEndsItAtTheFirstCall_WithTheMemoWritten()
    {
        var document = NewDocument(0x01, 0x23, 0xFF);
        var (runner, trace) = NewRunner(document);
        var owner = NewEntity(30);
        owner.PosZ = 15728640;
        var state = StateFor(document);
        runner.RunOneScriptCall(owner, state);
        trace.Clear();
        runner.RunOneScriptCall(owner, state);
        Assert.Equal(new[] { (1, 0x23, 1), (2, 0xFF, 0) }, Shape(trace));

        var (runner2, trace2) = NewRunner(document);
        var far = NewEntity(30);
        far.PosZ = 15000000;
        far.CollidedWithEntityZ = 1;
        var state2 = StateFor(document);
        runner2.RunOneScriptCall(far, state2);
        Assert.Equal(new[] { (0, 0x01, 1), (1, 0x23, 1), (2, 0xFF, 0) }, Shape(trace2));
        Assert.Equal(EventTraceKind.Implemented, trace2[1].Kind);
        Assert.Equal(15728640, state2.Parameters[2]);
    }

    /// <summary>Captures every warning of the process while the test runs (the suite runs its collections one after the other).</summary>
    private sealed class CapturingWarnings : CasaEngine.Core.Logging.ILogger, System.IDisposable
    {
        public List<string> Messages { get; } = new();

        public void Close()
        {
        }

        public void WriteTrace(string msg)
        {
        }

        public void WriteDebug(string msg)
        {
        }

        public void WriteInfo(string msg)
        {
        }

        public void WriteWarning(string msg) => Messages.Add(msg);

        public void WriteError(string msg)
        {
        }

        public static CapturingWarnings Install()
        {
            var logger = new CapturingWarnings();
            CasaEngine.Core.Logging.Logs.AddLogger(logger);
            return logger;
        }

        public void Dispose()
        {
            var field = typeof(CasaEngine.Core.Logging.Logs).GetField("_loggers", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(null) is List<CasaEngine.Core.Logging.ILogger> loggers)
            {
                loggers.Remove(this);
            }
        }
    }
}
