#nullable enable
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using Xunit;
using static Alundra.Tests.ArcChecks;

namespace Alundra.Tests;

/// <summary>The state of the world at one instruction (E19.d2b): the hero and each watched record, in 16.16 (the first entity of the record that
/// is not flagged for destruction, else the first).</summary>
internal sealed record ArcSnap(int Frame, (int X, int Y, int Z) Hero, Dictionary<int, (int X, int Y, int Z, EntityStatus Status, int ForceAdjusted)> Records)
{
    public (int X, int Y, int Z, EntityStatus Status, int ForceAdjusted) Rec(int record) => Records[record];
}

/// <summary>
/// E19.d2b (docs/plan-e19-opcodes.md section 1.2h.2): snapshots of an arc taken at "the first instruction of the program after <c>pc</c>" (a wait or a
/// walk that ends is followed by the next instruction of its program, so its effects are applied: what the script sees), and the first frame each
/// temporary flag was seen set, and cleared. It takes the arc's <see cref="ArcRun.OnInstruction"/> and <see cref="ArcRun.OnFrame"/> seams; a test that
/// needs more chains the previous ones.
/// </summary>
internal sealed class ArcSamples
{
    private readonly ArcRun _arc;
    private readonly List<(int Slot, int Program, int Pc)> _watches;
    private readonly HashSet<(int, int, int)> _armed = new();
    private readonly uint[] _flags;
    private readonly HashSet<int> _watchedRecords;

    public readonly Dictionary<(int Slot, int Program, int Pc), ArcSnap> After = new();
    public readonly Dictionary<uint, int> FirstSet = new();
    public readonly Dictionary<uint, int> Cleared = new();

    public ArcSamples(ArcRun arc, int[] records, uint[] temporaryFlags, params (int Slot, int Program, int Pc)[] watches)
    {
        _arc = arc;
        _watches = watches.ToList();
        _flags = temporaryFlags;
        _watchedRecords = records.ToHashSet();
        arc.OnInstruction = OnInstruction;
        arc.OnFrame = OnFrame;
    }

    private void OnInstruction(ArcInstruction t)
    {
        foreach (var w in _watches)
        {
            if (After.ContainsKey(w) || t.Slot != w.Slot || t.ProgramStart != w.Program)
            {
                continue;
            }

            if (t.Pc == w.Pc)
            {
                _armed.Add(w);
            }
            else if (_armed.Contains(w))
            {
                After[w] = Take();
            }
        }
    }

    private void OnFrame()
    {
        foreach (var n in _flags)
        {
            if (IsTemporarySet(n))
            {
                FirstSet.TryAdd(n, _arc.Frame);
            }
            else if (FirstSet.ContainsKey(n) && !Cleared.ContainsKey(n))
            {
                Cleared[n] = _arc.Frame;
            }
        }
    }

    public ArcSnap Take()
    {
        var records = new Dictionary<int, (int, int, int, EntityStatus, int)>();
        foreach (var e in _arc.Entities)
        {
            if (!_watchedRecords.Contains(e.EntityRefId))
            {
                continue;
            }

            if (!records.TryGetValue(e.EntityRefId, out var known) || (known.Item4 == EntityStatus.FlagToDestroy && e.Status != EntityStatus.FlagToDestroy))
            {
                records[e.EntityRefId] = (e.PosX, e.PosY, e.PosZ, e.Status, e.ForceAdjusted);
            }
        }

        return new ArcSnap(_arc.Frame, (_arc.Hero.PosX, _arc.Hero.PosY, _arc.Hero.PosZ), records);
    }

    public ArcSnap this[int slot, int program, int pc]
    {
        get
        {
            Assert.True(After.TryGetValue((slot, program, pc), out var snap), $"no instruction followed @{pc} of the program @{program} (slot {slot})");
            return snap!;
        }
    }
}
