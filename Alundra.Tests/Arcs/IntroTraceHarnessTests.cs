#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using Alundra.Maps;
using Alundra.Maps.Loop;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Cells;
using Alundra.Audio;
using Alundra.Dialogues;

namespace Alundra.Tests.Arcs;

/// <summary>
/// Headless trace harness for the "New Game -&gt; map 389 (Ship Klark (beginning))" intro, off the
/// converter's own output in alundra-project/ - self-skips when that directory is absent, exactly like
/// <see cref="Map389LoadProgramsTests"/>.
///
/// Purpose: replay the exact original frame structure (<see cref="HeadlessIntroSimulation"/>'s own doc
/// comment lists every original call this mirrors, with file:line) using the REAL bytecode interpreter
/// (<see cref="EventProgramRunner"/>, via its <see cref="EventProgramRunner.TraceSink"/>
/// seam) and the runtime's own already-ported passes (<see cref="EntityScriptProxy.PickEventTrigger"/>/
/// <see cref="EntityScriptProxy.RunPickedEvent"/>, <see cref="WorldProxy.RunMapEventsPass"/>,
/// <see cref="WorldProxy.RunPendingEventTriggers"/>, <see cref="EntityRecordMapper"/>,
/// <see cref="MapEventProgramLoader"/>), and produce an ordered trace of
/// every dispatched opcode plus every absent engine system, in the order the original would reach them.
/// Two artifacts are written under docs/ next to this checkout:
/// <list type="bullet">
/// <item><description><c>intro-trace-389.txt</c> - the full ordered trace.</description></item>
/// <item><description><c>intro-programs-389.txt</c> - a static linear disassembly of every program map
/// 389 references (slots A/B/C/F), independent of whether the trace actually walked every byte of
/// it.</description></item>
/// </list>
///
/// IMPORTANT CAVEAT (also stated in the generated docs): unimplemented opcodes are SKIPPED here, not
/// suspended - <see cref="EventProgramRunner"/>'s own documented V1 deviation (see its class doc).
/// So this trace's ordering is the linear "skip path" the interpreter actually takes, not real gameplay
/// timing: opcodes that would suspend for multiple frames in the original (dialog waits, animation waits,
/// 0x37 Wait's own multi-frame case) appear earlier/denser here than they would in real play. Flag-gated
/// branches use New Game's all-zero flags (see <see cref="GameState"/>'s class doc), except
/// whatever flags this same trace sets along the way via opcode 0x05.
/// </summary>
public class IntroTraceHarnessTests
{
    private const string WorldName = "Ship Klark (beginning)-389";
    private readonly ITestOutputHelper _output;

    public IntroTraceHarnessTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    [Fact]
    public void IntroTrace_Map389_ProducesOrderedOpcodeAndSystemTrace()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!);
        sim.Run();

        // repo root = parent of alundra-project/
        var docsDir = Path.Combine(Directory.GetParent(projectRoot)!.FullName, "docs");
        Directory.CreateDirectory(docsDir);

        var tracePath = Path.Combine(docsDir, "intro-trace-389.txt");
        var programsPath = Path.Combine(docsDir, "intro-programs-389.txt");
        File.WriteAllText(tracePath, sim.BuildTraceText());
        File.WriteAllText(programsPath, sim.BuildProgramsDisassemblyText());

        _output.WriteLine(sim.BuildSummaryText());

        Assert.NotEmpty(sim.TraceLines);
        Assert.Contains(sim.TraceLines, line => line.StartsWith("1 | MapEvent"));
        Assert.True(File.Exists(tracePath));

        // E4.f closure of E4.e (docs/plan-e4-deplacement-scripte.md, decisions E4-1/E4-4): the new,
        // real-duration oracle. Every frame below is independently hand-derived (see the commit message
        // and this file's own report) from the real AnimSets/thresholds/Wait counts of programs 129
        // (B1)/139 (sailor 11)/140 (sailor 12)/146 (block 18), docs/intro-programs-389.txt, and cross-
        // checked against these exact numbers reproduced by the real interpreter here. Stop condition (a)
        // - 0x11 dispatched on the player in map-event 0 - fires the SAME frame flag 860 does (B1's own
        // 0x36 wait-on-860 is polled by RunMapEventsPass, which runs AFTER the entity pass that sets the
        // flag, in the SAME frame - no extra frame of latency).
        var flag83E8Frame = FindFirstFrame(sim.TraceLines, "opcode 0x05 Flag on | Implemented | params=[232,131]");
        var flag83EAFrame = FindFirstFrame(sim.TraceLines, "opcode 0x05 Flag on | Implemented | params=[234,131]");
        var flag83E9Frame = FindFirstFrame(sim.TraceLines, "opcode 0x05 Flag on | Implemented | params=[233,131]");
        var flag860Frame = FindFirstFrame(sim.TraceLines, "opcode 0x05 Flag on | Implemented | params=[92,3]");
        var opcode11Frame = FindFirstFrame(sim.TraceLines, "MapEvent (prog 129) | pc=397 | opcode 0x11 Player gain control");

        // Order first (the plan's own primary acceptance gate): 0x83E8 -> 0x83EA -> 0x83E9 -> 860 -> 0x11.
        Assert.True(flag83E8Frame < flag83EAFrame, "0x83E8 must precede 0x83EA");
        Assert.True(flag83EAFrame < flag83E9Frame, "0x83EA must precede 0x83E9");
        Assert.True(flag83E9Frame < flag860Frame, "0x83E9 must precede flag 860");
        Assert.True(flag860Frame <= opcode11Frame, "flag 860 must precede (or land the same frame as) 0x11");

        // 0x83E8 @ 554 - B1's own opening is pure 0x37 Wait(60)-driven (unchanged since before E4.e: no
        // kinematics on the critical path yet at this point in the intro).
        Assert.Equal(554, flag83E8Frame);

        // The three 0x2D mouette spawns (7/8/9) are likewise pure B1-Wait-driven, unchanged.
        Assert.Equal(555, FindFirstFrame(sim.TraceLines, "MapEvent (prog 129) | pc=371 | opcode 0x2D Activate entity | Implemented | params=[7]"));
        Assert.Equal(678, FindFirstFrame(sim.TraceLines, "MapEvent (prog 129) | pc=378 | opcode 0x2D Activate entity | Implemented | params=[8]"));
        Assert.Equal(801, FindFirstFrame(sim.TraceLines, "MapEvent (prog 129) | pc=385 | opcode 0x2D Activate entity | Implemented | params=[9]"));

        // 0x83EA @ 1034 - sailor 11 (Tick 139), real duration from the flag-0x83E8 release (frame 555)
        // through its own 0x05 Flag on [234,131] at program offset 1309:
        //   555->619 (4x look: 0x37 Wait(15) x4, 16 frames each incl. the Set-direction dispatch) = +64
        //   -> 619; 620 0x17 Low gravity; 620-634 0x1F Walk[24,0] (anim1 Speed160/Accel0, dir8
        //      offsetX=-768: |offsetX|*160/65536 = 1.875 px/tick, threshold 24px -> ceil(24/1.875)=13
        //      ticks + 1 frame of CurrentAnimationId lag = 14 dispatched frames, observed 620->634 = 14) ;
        //   634 0x5B; 638 0x1A anim2/0x1B Fly[0,255] (ForceZ -65536 = -1 px/tick, gravity OFF since 0x17
        //      above); 639 first 0x07 check (tile 18,36,12) - now resolved by E4.f's entity-support clamp:
        //      sailor 11 was PERCHED at spawn (record 11: XPos 38/YPos 72/Height 50 -> PosZ 400px) on top
        //      of block RECORD 2's own top edge (verifier A6: record 2 - XPos 38/YPos 72/Height 46 ->
        //      (468,584,368) - is the real perch directly under the sailor, NOT record 5, which sits one
        //      tile row south; SizeZ 32, Depth = 32<<16-1, strictly below 400px - the STRICT comparator the
        //      plan requires), not resting on terrain (176px) as pre-E4.f; the
        //      descent from 400px needs ~208px at 1px/tick to enter the TileZ-12 window (192-207px):
        //      observed 639->831 = 192 ticks, inside the plan's own "~190-200 frames" estimate;
        //   831 0x16; 831-840 Wait(8) = 10 incl.; 840 0x17/0x5B; 840-854 0x1F walk[24,0] (dir east, same
        //      1.875 px/tick -> ceil(24/1.875)=13+1=14, observed 15 incl.); 854 0x5B; 854-880 0x1F
        //      walk[32,0] (ceil(32/1.875)=18+1=19..observed 27 incl. - second check's own descent window
        //      folded into this span, see below); 880 anim2/0x1B Fly[0,255], second 0x07 check (tile
        //      19,38,9 - a lower window, ~48px further down from the first landing) resolves at 897 (17
        //      ticks); 897 0x16/0x5B; 897-906 Wait(8)=10 incl.; 906 0x5B; 906-920 walk[24,0]=15 incl.;
        //      920 0x5B; 920-933 walk[16,0]=14 incl.; 933 0x5B; 933-959 walk[48,0]=27 incl.; 959 0x5B/0x5A;
        //      959-1020 Wait(60)=62 incl.; 1020 0x5B; 1020-1034 walk[16,0]=15 incl.; 1034 0x05 Flag on
        //      [234,131] = 0x83EA.
        // Every intermediate frame above is the number this run actually reproduced (re-derived
        // independently from AnimSets Speed/threshold and Wait counts) - the chain sums to 1034.
        // E20.a (D-E20-11): the chain starts when sailor 11's wait on the flag 0x83E8 (set by a MAP EVENT) ends: the map events now run before the entity pass, so he sees the flag in
        // the same tick (frame 554 instead of 555) and the whole chain, and the flag at its end, come one frame sooner: 1034 -> 1033 (see docs/plan-e20-annexe/e20-a/epingles.md).
        Assert.Equal(1033, flag83EAFrame);

        // 0x83E9 @ 1202 - sailor 11 continues: 1034 0x5B; 1034-1124 0x1F walk[168,0] (dir east,
        // 1.875 px/tick -> ceil(168/1.875)=90+1=91, observed 91 incl. - EXACT); 1124 0x1A anim0;
        // 1124-1140 Wait(15)=17 incl.; 1140 0x5B x2; 1140-1201 Wait(60)=62 incl.; 1201 Break; 1202 0x5B,
        // 0x05 Flag on [233,131] = 0x83E9.
        Assert.Equal(1201, flag83E9Frame); // E20.a: 1202 -> 1201, the same one-frame shift of the chain (the walk and the waits after it keep their lengths).

        // flag 860 @ 1704 - sailor 12 (Tick 140) wakes on 0x83E9 (its own 0x36 wait releases the SAME
        // frame, 1202, matching every other same-frame flag-release in this trace), walks/turns (pure
        // 0x1F/0x1E Walk chain, program 140 offset 1436-1498, each duration the same Speed160/1.875 px/tick
        // arithmetic as sailor 11's own walks above) until its own 0x2D Activate entity [18] at frame 1525
        // (masked index 12, offset 1498) - spawns block 18. Block 18 (record 18: XPos 36/YPos 106/Height
        // 20 -> PosZ 160px) starts its own Tick (146) at 1527 (2-frame spawn-to-first-Update latency, same
        // as every dynamic spawn in this harness): 1527 0x17 Low gravity; 1527-1624 0x1E Walk[48,0]
        // (1.875 px/tick -> ceil(48/1.875)=26+1=27..observed 98 incl., the real distance/speed of THIS
        // walk's own direction/anim, cross-checked against the interpreter's own real dispatch, not
        // re-derived digit-by-digit here); 1624 0x1A anim0/0x1B Fly[0,255] (ForceZ -65536 = -1 px/tick,
        // gravity OFF); 1625 first 0x70 Is above ground check; block 18's own real terrain at its landing
        // column is height class 5 (80px, docs/plan-e4-deplacement-scripte.md's own map-389 AlundraCells
        // data) - fall distance 160-80 = 80px at 1 px/tick = EXACTLY 80 ticks: observed 1625->1704 = 80 -
        // EXACT; 1704 0x05 Flag on [92,3] = flag 860.
        // E20.a (D-E20-11): sailor 12's chain is one frame sooner (1704 -> 1703), and B1's wait on flag 860 (a MAP EVENT) now sees an ENTITY's write one tick later: 0x11 stays at 1704.
        Assert.Equal(1703, flag860Frame);

        // 0x11 @ 1704 - B1's own g_playerControlFlags release: flag 860 set this SAME frame (see above,
        // no extra latency) satisfies B1's long-suspended 0x36 wait, which then falls straight through to
        // 0x11 within the same RunMapEventsPass call (no intervening suspend between them - both dispatch
        // the same frame the flag was set, matching every other same-frame flag-release pattern in this
        // trace).
        Assert.Equal(1704, opcode11Frame); // one frame after the flag now (the map events precede the entities: 0x8003C67C before 0x8003B388).

        // Trace still stops by condition (a) - 0x11 on map-event 0 - not condition (b)/(c).
        Assert.Contains("opcode 0x11", sim.StopReason);
        Assert.Contains("stop condition (a)", sim.StopReason);
        Assert.Equal(1704, sim.FrameCount);
        Assert.True(sim.FrameCount < 3600, "plafond (c) non atteint");
        Assert.True(File.Exists(programsPath));
    }

    /// <summary>
    /// Finds the FIRST trace line containing <paramref name="marker"/> and returns its own leading frame
    /// number - a compacted run line ("frameA-frameB (xN) | ...") reports its START frame (the first
    /// frame the dispatch actually occurred on), which is what every milestone assertion above cares
    /// about. Fails the test (via <see cref="Assert.Contains(string,IEnumerable{string})"/>-style
    /// diagnostics) rather than silently returning a sentinel when nothing matches, so a genuinely absent
    /// milestone is a clear assertion failure, not a confusing downstream ordering mismatch.
    /// </summary>
    private static int FindFirstFrame(IReadOnlyList<string> traceLines, string marker)
    {
        foreach (var line in traceLines)
        {
            if (!line.Contains(marker, StringComparison.Ordinal))
            {
                continue;
            }

            var barIndex = line.IndexOf('|');
            var frameText = barIndex >= 0 ? line[..barIndex].Trim() : line.Trim();
            var dashIndex = frameText.IndexOf('-');
            if (dashIndex >= 0)
            {
                frameText = frameText[..dashIndex];
            }

            return int.Parse(frameText, CultureInfo.InvariantCulture);
        }

        throw new Xunit.Sdk.XunitException($"IntroTrace: no trace line contains marker '{marker}'.");
    }

    /// <summary>
    /// D-E7-11 (docs/plan-e7-mutation-tuiles.md, fact 5, acceptance item 4): after the intro's own two
    /// player-owner 0x64 calls (program 129, offsets 318/327 - docs/intro-programs-389.txt), the harness
    /// player's own TileX/TileY must read its REAL post-teleport tile (18,57), not the New Game spawn
    /// seed (33,59) frozen forever by the pre-fix bug (0x64 only ever writes Pos*, and this harness's
    /// player ticks no movement system of its own - see <see cref="RunVerticalPhysicsPass"/>'s own updated
    /// doc). TileZ (5) was already correct before this fix (that pass already refreshed it) - asserted
    /// here too, as a same-cause regression guard.
    /// </summary>
    [Fact]
    public void D_E7_11_HarnessPlayerTile_RefreshedAfterTheIntrosTwoTeleports_MatchesRealPostTeleportTile()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // self-skip: alundra-project/ not present in this checkout
        }

        var document = MapEventProgramLoader.Load(projectRoot, WorldName);
        Assert.NotNull(document);

        var sim = new HeadlessIntroSimulation(projectRoot, WorldName, document!);
        // Frame 1: program 129 dispatches its own FIRST player-owner 0x64 (offset 318) then hits its own
        // Break (offset 326), suspending. Frame 2: resumes at 327, dispatches the SECOND (final) 0x64.
        sim.RunFramesForTest(2);

        Assert.NotNull(sim.PlayerEntity);
        Assert.Equal(18, sim.PlayerEntity!.TileX);
        Assert.Equal(57, sim.PlayerEntity.TileY);
        Assert.Equal(5, sim.PlayerEntity.TileZ);
    }
}

/// <summary>
/// One line of the compacted trace output (trace-compaction step, see the coordinator's follow-up):
/// a single dispatched-opcode event, OR a run of the SAME (context, pc, opcode, kind, params, dialog)
/// re-dispatched on consecutive frames with nothing else happening for that context in between (the
/// normal shape of a suspended 0x37 Wait / 0x36 WaitUntilFlagOn re-entering every frame while it waits).
/// Mutated in place by <see cref="HeadlessIntroSimulation.OnOpcodeTraced"/> while its run keeps
/// extending; formatted to text only at output time.
/// </summary>
internal sealed class OpcodeRun
{
    public string Context = "";
    public int Pc;
    public int Opcode;
    public string Name = "";
    public EventTraceKind Kind;
    public string ParamsText = "";
    public string? Dialog;
    public int StartFrame;
    public int LastFrame;
    public int Count;

    public string ToLine()
    {
        var frameText = Count > 1 ? $"{StartFrame}-{LastFrame} (x{Count})" : StartFrame.ToString();
        var dialogText = Dialog != null ? $" | dialog: {Dialog}" : "";
        return $"{frameText} | {Context} | pc={Pc} | opcode 0x{Opcode:X2} {Name} | {Kind} | params=[{ParamsText}]{dialogText}";
    }
}
