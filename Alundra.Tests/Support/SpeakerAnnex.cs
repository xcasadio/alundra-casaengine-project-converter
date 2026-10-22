#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Alundra.Tests.Support;

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
