#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests;

/// <summary>
/// E19.f4b (docs/plan-e19-opcodes.md, F4B-1, "oracle et suites"): the oracle of the speaker name box and of the speaker portrait of the dialogue, a C# port of
/// <c>docs/plan-e19-f4-annexe/model/f4_model.py</c> (the model validated against the real binary code: ALUN_CD.EXE, France) and of its height extension
/// <c>model/f4_model_h.py</c> (D-E19-90). It is written from those two files, NEVER from the DLL's own machines (<c>AlundraDialogueNameBox</c>,
/// <c>AlundraInventoryPortrait</c>): the DLL is compared against it, so a shared mistake would not show. The generator of the random sequences
/// (<see cref="SpeakerRawRun"/>) is a port of <c>model/gen_sequences.py</c> (SplitMix64, fed to both the oracle and the DLL's machines).
/// </summary>
internal static class SpeakerAnnex
{
    /// <summary>The folder <c>docs/plan-e19-f4-annexe</c>, searched upward from the test binaries.</summary>
    public static string Directory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-f4-annexe");
            if (File.Exists(Path.Combine(candidate, "values.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("docs/plan-e19-f4-annexe not found above " + AppContext.BaseDirectory);
    }

    public static string File_(string name) => Path.Combine(Directory(), name);

    /// <summary>The 60 names of the binary with their font3 width (<c>names_widths.json</c>), by ETC index.</summary>
    public static IReadOnlyDictionary<int, (string Name, int Width)> Names()
    {
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(File_("names_widths.json")));
        var names = new Dictionary<int, (string, int)>();
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            names[Convert.ToInt32(entry.Name, 16)] = (entry.Value.GetProperty("name").GetString()!, entry.Value.GetProperty("w").GetInt32());
        }

        return names;
    }
}

internal sealed record OracleNameDraw(int FrameX, int TextX);

internal sealed record OraclePortraitDraw(int X, int Y, int W, int H, int Rgb, string Phase);

/// <summary>UpdateUiBoxesPosition 0x80047DD0 on a block {step, total = 15, settle = 2, start, target} (<c>Slide</c> of model.py).</summary>
internal sealed class OracleSlide
{
    private int _step;
    private const int Total = 15;
    private int _settle = 2;
    private readonly int _start;
    private readonly int _target;

    public OracleSlide(int start, int target)
    {
        _start = start;
        _target = target;
    }

    public (bool Done, int Pos) Update(int pos)
    {
        if (_settle == 0)
        {
            return (true, pos);
        }

        if (_step != Total)
        {
            pos = _start + (_target - _start) * _step / Total;
            _step++;
        }
        else
        {
            pos = _target;
            _settle--;
        }

        return (false, pos);
    }
}

/// <summary>Port of <c>NameBox</c> (f4_model.py): slot 12, globals 0x80180240. <paramref name="widthOf"/> gives the width of the name of an ETC id, or null for an absent or empty entry.</summary>
internal sealed class OracleNameBox
{
    private readonly Func<int, int?> _widthOf;
    private OracleSlide _slide = new(320, 64);
    private int _width;

    public OracleNameBox(Func<int, int?> widthOf) => _widthOf = widthOf;

    public int Flags { get; private set; }

    public bool Slot { get; private set; }

    public int X { get; private set; } = 64;

    public bool Open(int id)
    {
        if ((Flags & 4) != 0)
        {
            return false;
        }

        if (!(id >= 0x100 && id < 0x200))
        {
            return false;
        }

        if (_widthOf(id) is not { } width)
        {
            return false;
        }

        Slot = true;
        Flags = 5;
        _slide = new OracleSlide(320, 64);
        _width = width;
        return true;
    }

    public void Close()
    {
        if ((Flags & 4) != 0)
        {
            Flags = 6;
        }

        _slide = new OracleSlide(X, 320);
    }

    public OracleNameDraw? Pass()
    {
        if (!Slot)
        {
            return null;
        }

        if ((Flags & 3) != 0)
        {
            var (done, x) = _slide.Update(X);
            X = x;
            if (done)
            {
                if ((Flags & 1) != 0)
                {
                    Flags &= ~1;
                }

                if ((Flags & 2) != 0)
                {
                    X = 64;
                    Flags = 0;
                    Slot = false;
                    return null;
                }
            }
        }

        return new OracleNameDraw(X, X + (112 - _width) / 2);
    }
}

/// <summary>The speaker the oracle portrait reads: the integer parts of its 16.16 position (<c>x</c>, <c>y</c>, <c>z</c>), its flags and its image height.</summary>
internal sealed class RawEntity
{
    public int Id;
    public uint Flags;
    public int X;
    public int Y;
    public int Z;
    public int H = 56;
}

/// <summary>Port of <c>Portrait</c> (f4_model.py) with the image height of <c>PortraitH</c> (f4_model_h.py): rest (8, 172 - h), 48 x h at rest.</summary>
internal sealed class OraclePortrait
{
    private const int Steps = 15;
    private int _c;
    private (int X, int Y) _dst;
    private (int X, int Y) _d;
    private (int X, int Y) _rest = (8, 116);
    private int _h = 56;

    public int State { get; private set; }

    public static (int X, int Y) Screen(RawEntity e, (int X, int Y) cam) => (e.X - cam.X, e.Y - cam.Y - e.Z - 0x20);

    public bool Open(RawEntity e, (int X, int Y) cam, int h)
    {
        if (State != 0)
        {
            return false;
        }

        _h = h;
        _rest = (8, 172 - h);
        var s = Screen(e, cam);
        _dst = _rest;
        _d = (s.X - _rest.X, s.Y - _rest.Y);
        _c = Steps;
        State = 5;
        return true;
    }

    public bool Close(RawEntity e, (int X, int Y) cam)
    {
        if (State == 0)
        {
            return false;
        }

        var s = Screen(e, cam);
        _dst = s;
        _d = (_rest.X - s.X, _rest.Y - s.Y);
        State = 2;
        _c = Steps;
        return true;
    }

    public OraclePortraitDraw? Pass()
    {
        if (State == 0)
        {
            return null;
        }

        var c = _c;
        if (c == 0)
        {
            if ((State & 2) != 0)
            {
                State = 0;
                return new OraclePortraitDraw(_rest.X, _rest.Y, 0, 0, 0, "gone");
            }

            State &= ~1;
            return new OraclePortraitDraw(_rest.X, _rest.Y, 48, _h, 0x80, "rest");
        }

        var x = _dst.X + _d.X * c / Steps;
        var y = _dst.Y + _d.Y * c / Steps;
        int w = 0, h = 0, rgb = 0;
        var phase = string.Empty;
        if ((State & 1) != 0)
        {
            w = 48 * (Steps - c) / Steps;
            h = _h * (Steps - c) / Steps;
            rgb = 0x7F + ((c << 7) / Steps);
            phase = "in";
        }
        else if ((State & 2) != 0)
        {
            w = 48 * c / Steps;
            h = _h * c / Steps;
            rgb = 0x7F + (((Steps - c) << 7) / Steps);
            phase = "out";
        }

        _c--;
        return new OraclePortraitDraw(x, y, w, h, rgb, phase);
    }
}

/// <summary>SplitMix64 of <c>model/gen_sequences.py</c>: the generator a test can port in six lines.</summary>
internal sealed class SplitMix64
{
    private ulong _s;

    public SplitMix64(ulong seed) => _s = seed;

    public ulong Next()
    {
        unchecked
        {
            _s += 0x9E3779B97F4A7C15UL;
            var z = _s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public int Below(int n) => (int)(Next() % (ulong)n);

    public int RandInt(int lo, int hi) => lo + Below(hi - lo + 1);

    public bool Chance(int permille) => Below(1000) < permille;

    public T Choice<T>(IReadOnlyList<T> sequence) => sequence[Below(sequence.Count)];
}

/// <summary>The two machines a raw sequence drives: the oracle's, or the DLL's (through an adapter). The strings of the passes are the digest cells of <c>gen_sequences.py</c>.</summary>
internal interface ISpeakerMachines
{
    int NameFlags { get; }

    bool NameSlot { get; }

    int PortraitState { get; }

    bool NameOpen(int id);

    void NameClose();

    bool PortraitOpen(RawEntity entity, (int X, int Y) cam, int height);

    void PortraitClose(RawEntity entity, (int X, int Y) cam);

    /// <summary>"frameX|textX", or null when nothing is drawn.</summary>
    string? NamePass();

    /// <summary>"x|y|w|h|rgb|phase", or null when nothing is drawn.</summary>
    string? PortraitPass();
}

internal sealed class OracleSpeakerMachines : ISpeakerMachines
{
    private readonly OracleNameBox _name;
    private readonly OraclePortrait _portrait = new();

    public OracleSpeakerMachines(Func<int, int?> widthOf) => _name = new OracleNameBox(widthOf);

    public int NameFlags => _name.Flags;

    public bool NameSlot => _name.Slot;

    public int PortraitState => _portrait.State;

    public bool NameOpen(int id) => _name.Open(id);

    public void NameClose() => _name.Close();

    public bool PortraitOpen(RawEntity entity, (int X, int Y) cam, int height) => _portrait.Open(entity, cam, height);

    public void PortraitClose(RawEntity entity, (int X, int Y) cam) => _portrait.Close(entity, cam);

    public string? NamePass() => _name.Pass() is { } d ? $"{d.FrameX}|{d.TextX}" : null;

    public string? PortraitPass() => _portrait.Pass() is { } d ? $"{d.X}|{d.Y}|{d.W}|{d.H}|{d.Rgb}|{d.Phase}" : null;
}

/// <summary>One raw sequence of <c>raw_sequence</c> (gen_sequences.py), in the exact order of its draws, driving <paramref name="machines"/> and counting the same coverage.</summary>
internal static class SpeakerRawRun
{
    private static readonly int[] Ids = { 0x104, 0x10C, 0x1AB, 0x1AC, 0x0FF, 0x200, 0x1FD, 0x14B };
    private static readonly int[] Heights = { 56, 56, 72 };
    private const uint HasPortrait = 0x800000;

    /// <summary>The ETC the generator models: the 60 names, 0x1AB present but empty and 0x1AC absent (both refused).</summary>
    public static Func<int, int?> ModelWidths(IReadOnlyDictionary<int, (string Name, int Width)> names)
        => id => names.TryGetValue(id, out var n) ? n.Width : null;

    public static string[] CoverageKeys { get; } =
    {
        "opens", "name_open_ok", "name_refused_busy", "name_refused_range", "name_refused_empty", "portrait_open_ok", "portrait_open_ignored", "portrait_flagless",
        "name_closes", "name_close_while_sliding_in", "name_close_while_closing", "portrait_closes", "portrait_close_while_in", "portrait_close_while_out",
        "portrait_retarget_ignored", "frames_name", "frames_portrait", "frames_72", "speaker_moved_between_open_and_close",
    };

    public static (List<string> Lines, Dictionary<string, int> Coverage) Run(
        int seed, ISpeakerMachines machines, Func<int, int?> modelWidths, int frames = 400)
    {
        var rnd = new SplitMix64((ulong)seed);
        var pool = new List<RawEntity>();
        for (var i = 0; i < 4; i++)
        {
            var id = rnd.Choice(Ids);
            var flags = rnd.Choice(new uint[] { 0, HasPortrait, HasPortrait, HasPortrait });
            var x = rnd.RandInt(-100, 500);
            var y = rnd.RandInt(-100, 500);
            var z = rnd.RandInt(-20, 60);
            var h = rnd.Choice(Heights);
            pool.Add(new RawEntity { Id = id, Flags = flags, X = x, Y = y, Z = z, H = h });
        }

        var cam = (X: rnd.RandInt(0, 200), Y: rnd.RandInt(0, 200));
        RawEntity? speaker = null;
        var lines = new List<string>();
        var cov = CoverageKeys.ToDictionary(k => k, _ => 0);
        var openPos = new Dictionary<RawEntity, (int, int, int)>();
        for (var f = 0; f < frames; f++)
        {
            foreach (var e in pool)
            {
                if (rnd.Chance(500))
                {
                    e.X += rnd.RandInt(-6, 6);
                    e.Y += rnd.RandInt(-6, 6);
                    e.Z = Math.Max(-30, Math.Min(80, e.Z + rnd.RandInt(-2, 2)));
                }
            }

            if (rnd.Chance(500))
            {
                cam.X += rnd.RandInt(-5, 5);
                cam.Y += rnd.RandInt(-5, 5);
            }

            var cn = rnd.Chance(40);
            var cp = rnd.Chance(40);
            var attempt = rnd.Chance(50);
            var ent = attempt ? rnd.Choice(pool) : null;

            if (cn)
            {
                cov["name_closes"]++;
                if ((machines.NameFlags & 1) != 0)
                {
                    cov["name_close_while_sliding_in"]++;
                }

                if ((machines.NameFlags & 2) != 0)
                {
                    cov["name_close_while_closing"]++;
                }

                machines.NameClose();
            }

            if (cp && machines.PortraitState != 0 && speaker is not null)
            {
                cov["portrait_closes"]++;
                if ((machines.PortraitState & 1) != 0)
                {
                    cov["portrait_close_while_in"]++;
                }

                if ((machines.PortraitState & 2) != 0)
                {
                    cov["portrait_close_while_out"]++;
                }

                if (!openPos.TryGetValue(speaker, out var at) || at != (speaker.X, speaker.Y, speaker.Z))
                {
                    cov["speaker_moved_between_open_and_close"]++;
                }

                machines.PortraitClose(speaker, cam);
            }

            var nd = machines.NamePass();
            var pd = machines.PortraitPass();
            if (attempt)
            {
                cov["opens"]++;
                if ((ent!.Flags & HasPortrait) != 0)
                {
                    if (machines.PortraitOpen(ent, cam, ent.H))
                    {
                        speaker = ent;
                        openPos[ent] = (ent.X, ent.Y, ent.Z);
                        cov["portrait_open_ok"]++;
                        if (ent.H == 72)
                        {
                            cov["frames_72"]++;
                        }
                    }
                    else
                    {
                        cov["portrait_open_ignored"]++;
                        if (!ReferenceEquals(ent, speaker))
                        {
                            cov["portrait_retarget_ignored"]++;
                        }
                    }
                }
                else
                {
                    cov["portrait_flagless"]++;
                }

                var nid = ent.Id;
                if ((machines.NameFlags & 4) != 0)
                {
                    cov["name_refused_busy"]++;
                }
                else if (!(nid >= 0x100 && nid < 0x200))
                {
                    cov["name_refused_range"]++;
                }
                else if (modelWidths(nid) is null)
                {
                    cov["name_refused_empty"]++;
                }
                else
                {
                    cov["name_open_ok"]++;
                }

                machines.NameOpen(nid);
            }

            if (nd is not null)
            {
                cov["frames_name"]++;
            }

            if (pd is not null)
            {
                cov["frames_portrait"]++;
            }

            lines.Add($"{f}|{nd ?? "-"}|{pd ?? "-"}|{machines.NameFlags}|{(machines.NameSlot ? 1 : 0)}|{machines.PortraitState}");
        }

        return (lines, cov);
    }

    public static string Digest(IEnumerable<string> lines)
        => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', lines)))).ToLowerInvariant();
}
