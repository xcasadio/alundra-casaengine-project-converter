#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.b T0/T3 (docs/plan-e19-opcodes.md §1.2d, §1.3): the two arcs of the vision of Lars and Melzas, on the real
/// exported map 476 (Lars and Melzas Room), in the bare montage of <see cref="ArcRun"/> (no hero walk exists in 476).
/// Expected values are written by hand from the decoded bytecode of the map before the code that satisfies them.
/// <para>
/// Samples taken at one instruction (the block's position at <c>0x8A @63</c>, the camera at <c>0x67 @71</c>, Rancune at
/// <c>0x8A @553</c>, the text of every box at its opening, the hero and the block around every <c>0x1E</c> walk) are
/// RECORDED DURING THE RUN by <see cref="ArcRun.OnInstruction"/>, which sees the instruction after its effects, and
/// are ASSERTED only after <see cref="ArcRun.RunUntil"/> reached the end signal: an arc that does not get there fails
/// naming its last instruction, never a sample.
/// </para>
/// </summary>
[Collection(AlundraMusicPlayerSingletonCollection.Name)]
public sealed class AlundraVisionArcTests
{
    private const string Zone = "Lars & Melzas Room";
    private const string Map476 = "Lars & Melzas Room (beginning Event)-476";

    private const int BProgram = ScriptHelper.ProgramBMap;

    /// <summary>The opcodes the map still skips after E19.b: none of them suspends (the original returns their size at
    /// once). 0x4C only matters to the typewriter (E12.c); 0x92, 0x93 and 0xA2 are effects (E19.g).</summary>
    private static readonly HashSet<int> AllowedSkippedOpcodes = new() { 0x4C, 0x92, 0x93, 0xA2 };

    private static ArcSpec A2Spec => new("A2", Zone, Map476, new[] { 1640 }, 0, 0, 3, 1500);

    private static ArcSpec A4Spec => new("A4", Zone, Map476, new[] { 1641 }, 0, 0, 0, 2500);

    /// <summary>A4p (E19.c1, D-E19-14): A4 with the export's real prefabs, loaded by the production spawn path.</summary>
    internal static ArcSpec A4pSpec => new("A4p", Zone, Map476, new[] { 1641 }, 0, 0, 0, 2500, RealController: true, Prefabs: true);

    internal sealed record BoxSample(int Opcode, uint OpenSerial, string Text);

    internal sealed record Walk(int Pc, (int X, int Y) HeroBefore, (int X, int Y) BlockBefore)
    {
        public (int X, int Y)? HeroAfter { get; set; }

        public (int X, int Y)? BlockAfter { get; set; }

        public int? BlockForceAdjustedBefore { get; set; }

        public int? BlockForceAdjustedAfter { get; set; }
    }

    /// <summary>The samples both arcs take: one per box opening, plus the block at <c>0x8A @63</c> and the camera at
    /// <c>0x67 @71</c> of B1.</summary>
    internal sealed class Samples
    {
        public (int X, int Y, int Z)? BlockAtSpawn;
        public bool BlockSpawnSeen;
        public bool CameraFollowsBlockAt71;
        public bool CameraSeen;
        public (int X, int Y, int Z)? RancuneAtSpawn;
        public bool RancuneSpawnSeen;
        public readonly Dictionary<int, BoxSample?> Boxes = new();
        public readonly List<Walk> Walks = new();

        // E19.c1 (A4p, real prefabs): the block right after 0x8A @63 and at the first instruction of a later frame.
        public int BlockSpawnFrame = -1;
        public bool BlockHasControllerAtSpawn;
        public bool BlockInWorldAtSpawn;
        public bool NextFrameSeen;
        public int? BlockZOnNextFrame;
        public bool BlockInWorldOnNextFrame;

        private Walk? _activeWalk;

        public void Take(ArcRun arc, ArcInstruction t, int[] boxPcs)
        {
            if (BlockSpawnFrame >= 0 && !NextFrameSeen && t.Frame > BlockSpawnFrame)
            {
                NextFrameSeen = true;
                var next = arc.EntityByRecord(1);
                BlockZOnNextFrame = next?.PosZ;
                BlockInWorldOnNextFrame = next != null && arc.RealWorld != null && arc.RealWorld.Entities.Contains(ArcRun.EntityOf(next)!);
            }

            if (t.Slot != BProgram)
            {
                return;
            }

            if (t.Pc == 63 && t.Opcode == 0x8A && !BlockSpawnSeen)
            {
                BlockSpawnSeen = true;
                var block = arc.EntityByRecord(1);
                BlockAtSpawn = block == null ? null : (block.PosX, block.PosY, block.PosZ);
                BlockSpawnFrame = t.Frame;
                BlockHasControllerAtSpawn = block?.Controller != null;
                BlockInWorldAtSpawn = block != null && arc.RealWorld != null && arc.RealWorld.Entities.Contains(ArcRun.EntityOf(block)!);
            }

            if (t.Pc == 71 && t.Opcode == 0x67 && !CameraSeen)
            {
                CameraSeen = true;
                var block = arc.EntityByRecord(1);
                CameraFollowsBlockAt71 = block != null && ReferenceEquals(arc.Proxy.EntityFollowedByCamera, block);
            }

            if (t.Pc == 553 && t.Opcode == 0x8A && !RancuneSpawnSeen)
            {
                RancuneSpawnSeen = true;
                var rancune = arc.EntityByRecord(0);
                RancuneAtSpawn = rancune == null ? null : (rancune.PosX, rancune.PosY, rancune.PosZ);
            }

            if (Array.IndexOf(boxPcs, t.Pc) >= 0 && t.Opcode is 0xC4 or 0x5C && !Boxes.ContainsKey(t.Pc))
            {
                Boxes[t.Pc] = t.Kind is EventTraceKind.Implemented or EventTraceKind.Degraded && AlundraDialogueDirector.Instance.IsOpen
                    ? new BoxSample(t.Opcode, (uint)AlundraDialogueDirector.Instance.OpenSerial, AlundraDialogueDirector.Instance.CurrentLineForTests?.Text ?? string.Empty)
                    : null;
            }

            // The walks of the camera block (B5, program @772): a walk starts at the first 0x1E and ends at the first
            // other instruction of the program, which the interpreter reaches once the 0x1E advanced.
            if (t.ProgramStart == 772)
            {
                if (_activeWalk != null && t.Opcode != 0x1E)
                {
                    _activeWalk.HeroAfter = (arc.Hero.PosX, arc.Hero.PosY);
                    var blockAfter = arc.EntityByRecord(1);
                    _activeWalk.BlockAfter = blockAfter == null ? null : (blockAfter.PosX, blockAfter.PosY);
                    _activeWalk.BlockForceAdjustedAfter = blockAfter?.ForceAdjusted;
                    _activeWalk = null;
                }

                if (_activeWalk == null && t.Opcode == 0x1E)
                {
                    var block = arc.EntityByRecord(1);
                    _activeWalk = new Walk(t.Pc, (arc.Hero.PosX, arc.Hero.PosY), block == null ? (0, 0) : (block.PosX, block.PosY))
                    {
                        BlockForceAdjustedBefore = block?.ForceAdjusted,
                    };
                    Walks.Add(_activeWalk);
                }
            }
        }
    }

    internal static void AssertNoOtherSkippedOpcode(ArcRun arc)
    {
        var offenders = arc.SkippedOrExceeded
            .Where(t => t.Kind == EventTraceKind.LoopBudgetExceeded || !AllowedSkippedOpcodes.Contains(t.Opcode))
            .Select(t => $"0x{t.Opcode:X2} @{t.Pc} ({t.Kind})")
            .Distinct()
            .ToList();
        Assert.True(offenders.Count == 0, "unexpected skipped or exceeded instructions: " + string.Join("; ", offenders));
    }

    internal static void AssertBox(Samples samples, int pc, int opcode, string fragment, string? notFragment = null)
    {
        Assert.True(samples.Boxes.TryGetValue(pc, out var box) && box != null, $"no box was opened by 0x{opcode:X2} @{pc}");
        Assert.Equal(opcode, box!.Opcode);
        Assert.Contains(fragment, box.Text);
        if (notFragment != null)
        {
            Assert.DoesNotContain(notFragment, box.Text);
        }
    }

    /// <summary>
    /// A2 (the outward vision, <c>G1640</c>): B1 teleports the hero, spawns the camera block (record 1) with 0x8A @63
    /// and makes the camera follow it (0x67 @71); B4 opens three boxes (0xC4 @622, @632, @739, nodes S101 to S103, each
    /// closed by its script), then <c>0x53 [222,1,22,57,1,2,0]</c> @758 departs towards map 478.
    /// </summary>
    [Fact]
    public void A2_TheVisionOutward_ThreeBoxesOpenAndClose_ThenTheWarpToMap478Departs()
    {
        using var arc = new ArcRun(A2Spec);
        Assert.Equal(256u, ArcRun.State.GameFlags[51]);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;

        var samples = new Samples();
        var boxPcs = new[] { 622, 632, 739 };
        arc.OnInstruction = t => samples.Take(arc, t, boxPcs);

        arc.RunUntil(() => arc.Has(BProgram, 758, 0x53), "B4 executes 0x53 @758 towards map 478");

        Assert.NotEqual(0u, ArcRun.State.GetFlag(1641) & (1u << (1641 & 0x1f)));
        Assert.Equal(0u, ArcRun.State.GetFlag(1640) & (1u << (1640 & 0x1f)));
        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(478u, arrival.MapIndex);
        Assert.Equal(35389440, arrival.PosX);
        Assert.Equal(60293120, arrival.PosY);
        Assert.Equal(1048576, arrival.PosZ);
        Assert.Equal(2, arrival.EffectId);

        // 0x8A @63: the camera block (record 1), then 0x67 @71 makes the camera follow it.
        Assert.True(samples.BlockSpawnSeen, "0x8A @63 never ran");
        Assert.True(samples.BlockAtSpawn.HasValue, "record 1 (the camera block) is absent after 0x8A @63");
        Assert.Equal((972 << 16, 112 << 16, (48 << 16) + 1), samples.BlockAtSpawn!.Value);
        Assert.True(samples.CameraSeen, "0x67 @71 never ran");
        Assert.True(samples.CameraFollowsBlockAt71, "the camera does not follow the block after 0x67 @71");

        // Three boxes, closed by the script (the arc never presses a button).
        Assert.Equal(3u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        AssertBox(samples, 622, 0xC4, "Est-ce que");
        // The whole text of node M476_S102 (15 characters, its glyph included); the first box carries "tu" and a glyph after it.
        AssertBox(samples, 632, 0xC4, "Tu\u0012 m'entends ?", notFragment: "Est-ce que");
        AssertBox(samples, 739, 0xC4, "Je suis connu sous le nom de Lars");
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);

        // The block's parent is the hero (the logic entity of B1).
        Assert.Same(arc.HeroEntity, arc.EntityByRecord(1)!.ParentEntity);

        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, ArcRun.State.PlayerControlFlags);
        AssertNoOtherSkippedOpcode(arc);
    }

    /// <summary>
    /// A4 (the return vision, <c>G1641</c>): B5 walks the camera block twice west (<c>0x1E @84</c>) and twice east
    /// (<c>0x1E @101</c>), spawns Rancune (record 0, 0x8A @553), opens eight boxes (0xC4 @786, @820, @842, @873 and
    /// 0x5C @810, @832, @854, @974, nodes S104 to S111), then <c>0x53 [136,1,29,13,4,4,73]</c> @986 departs towards
    /// map 392.
    /// </summary>
    [Fact]
    public void A4_TheVisionOnTheReturn_TheBlockWalksFourTimes_EightBoxesOpen_ThenTheWarpToMap392Departs() => RunA4(A4Spec);

    /// <summary>A4 on the export's real prefabs (E19.c1, D-E19-14): the same end values and the same eight boxes, plus what
    /// only a real controller shows (the block spawned by <c>0x8A @63</c> is a world entity, the four pans measured on it).</summary>
    [Fact]
    public void A4p_TheSameVisionWithRealPrefabs_TheBlockIsAControllerEntity_AndPansLikeA4()
    {
        RunA4(A4pSpec);
    }

    /// <summary>The body of A4 and A4p (the latter adds the checks of the real prefabs).</summary>
    internal static void RunA4(ArcSpec spec)
    {
        var prefabs = spec.Prefabs;
        using var arc = new ArcRun(spec);
        Assert.Equal(512u, ArcRun.State.GameFlags[51]);
        var serialAtStart = AlundraDialogueDirector.Instance.OpenSerial;

        var samples = new Samples();
        var boxPcs = new[] { 786, 810, 820, 832, 842, 854, 873, 974 };
        arc.OnInstruction = t => samples.Take(arc, t, boxPcs);

        arc.RunUntil(() => arc.Has(BProgram, 986, 0x53), "B5 executes 0x53 @986 towards map 392");

        // The skipped set comes right after the end signal (docs/plan-e19-opcodes.md §1.2e, T2: order of the checks).
        AssertNoOtherSkippedOpcode(arc);

        Assert.True(AlundraWarpDirector.Instance.HasPendingArrival);
        var arrival = AlundraWarpDirector.Instance.ArrivalRecordForTests;
        Assert.Equal(392u, arrival.MapIndex);
        Assert.Equal(46399488, arrival.PosX);
        Assert.Equal(14155776, arrival.PosY);
        Assert.Equal(4194304, arrival.PosZ);
        Assert.Equal(4, arrival.EffectId);

        // The four walks of the block (0x1E @84 twice, @101 twice): the block moved at least 48 px each time, and the
        // hero did not, which proves the walk acts on the block.
        Assert.Equal(new[] { 84, 84, 101, 101 }, samples.Walks.Select(w => w.Pc).ToArray());
        foreach (var walk in samples.Walks)
        {
            Assert.True(walk.HeroAfter.HasValue && walk.BlockAfter.HasValue, $"the walk of 0x1E @{walk.Pc} never ended");
            Assert.Equal(walk.HeroBefore, walk.HeroAfter!.Value);
            var moved = Math.Abs(walk.BlockAfter!.Value.X - walk.BlockBefore.X) + Math.Abs(walk.BlockAfter.Value.Y - walk.BlockBefore.Y);
            Assert.True(moved >= 48 << 16, $"the block moved {moved / 65536.0} px during 0x1E @{walk.Pc}, 48 expected at least");
        }

        // Rancune (record 0) spawns at @553.
        Assert.True(samples.RancuneSpawnSeen, "0x8A @553 never ran");
        Assert.True(samples.RancuneAtSpawn.HasValue, "record 0 (Rancune) is absent after 0x8A @553");
        Assert.Equal((792 << 16, 176 << 16, (48 << 16) + 1), samples.RancuneAtSpawn!.Value);

        // Eight boxes, closed by the script.
        Assert.Equal(8u, (uint)AlundraDialogueDirector.Instance.OpenSerial - (uint)serialAtStart);
        AssertBox(samples, 786, 0xC4, "Au nord d'Inoa");
        AssertBox(samples, 810, 0x5C, "Tu me fais rire");
        AssertBox(samples, 820, 0xC4, "Dis-moi, Melzas");
        AssertBox(samples, 832, 0x5C, "Ne comprends-tu pas, sorcier");
        AssertBox(samples, 842, 0xC4, "Je rigole car");
        AssertBox(samples, 854, 0x5C, "Je ne suis pas cruel");
        AssertBox(samples, 873, 0xC4, "Mon temps dans ce monde");
        AssertBox(samples, 974, 0x5C, "Ha, ha, ha");
        Assert.False(AlundraDialogueDirector.Instance.IsOpen);

        // Parents: the block's is the hero, Rancune's is the block (0x43 [0x80] leaves the block as the logic entity of B5).
        var blockProxy = arc.EntityByRecord(1)!;
        Assert.Same(arc.HeroEntity, blockProxy.ParentEntity);
        Assert.Same(ArcRun.EntityOf(blockProxy), arc.EntityByRecord(0)!.ParentEntity);

        Assert.Equal(AlundraGameState.PlayerControlBits.ControlLocked, ArcRun.State.PlayerControlFlags);

        if (prefabs)
        {
            // The block spawned by 0x8A @63 is a real prefab: a controller, queued by the spawn and a world entity from
            // the next frame on; its Z loses the spawn "+1" at the first adjustment of the root.
            Assert.True(samples.BlockHasControllerAtSpawn, "the block spawned by 0x8A @63 has no controller");
            Assert.False(samples.BlockInWorldAtSpawn, "the block is already a world entity at 0x8A @63");
            Assert.Equal((63700992, 7340032, 3145729), samples.BlockAtSpawn!.Value);
            Assert.True(samples.NextFrameSeen, "no instruction ran in the frame after 0x8A @63");
            Assert.True(samples.BlockInWorldOnNextFrame, "the block is not a world entity one frame after 0x8A @63");
            Assert.Equal(3145728, samples.BlockZOnNextFrame);

            // The four pans, measured on the block (16.16): the second overshoot is the kept animation lag (D-E19-13).
            var expectedPans = new[] { (63700992, 60555264), (60555264, 57409536), (57360384, 60506112), (60506112, 63651840) };
            for (var i = 0; i < 4; i++)
            {
                var walk = samples.Walks[i];
                Assert.Equal(expectedPans[i], (walk.BlockBefore.X, walk.BlockAfter!.Value.X));
                Assert.Equal(7340032, walk.BlockBefore.Y);
                Assert.Equal(7340032, walk.BlockAfter.Value.Y);
                Assert.Equal(0, walk.BlockForceAdjustedBefore);
                Assert.Equal(0, walk.BlockForceAdjustedAfter);
            }

            // No exception was logged beyond the sprite resolutions the test cannot avoid (no SpriteData loader).
            var unexpected = arc.Log!.Errors.Where(e => !e.StartsWith("AnimatedSpriteComponent : can't resolve sprite", StringComparison.Ordinal)).ToList();
            Assert.True(unexpected.Count == 0, "errors logged: " + string.Join(" | ", unexpected.Take(3)));
        }
    }
}
