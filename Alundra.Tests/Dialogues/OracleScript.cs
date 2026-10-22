#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CasaEngine.Framework.Dialogue.Assets;

namespace Alundra.Tests.Dialogues;

/// <summary>What a script of the oracle's host does: open, write, wait. A script is an iterator; <c>yield return</c> ends its tick.</summary>
internal sealed class OracleScript
{
    private readonly OracleHost _host;
    private readonly string _phase;

    public OracleScript(OracleHost host, string phase)
    {
        _host = host;
        _phase = phase;
    }

    public TextBoxOracle Box => _host.Box;

    public int Frame => _host.Frame;

    public void Note(string what) => _host.Events.Add((_host.Frame, _phase, what));

    /// <summary>0x0D / 0x5C / 0xC4: opens, trying again every tick while a box is active.</summary>
    public IEnumerable<int> Open(byte[] text, int mode, string label = "open")
    {
        while (!Box.TryOpen(text, mode))
        {
            yield return 0;
        }

        Note(label);
    }

    /// <summary>0x39: returns the tick the box is released.</summary>
    public IEnumerable<int> Wait39(string label = "0x39")
    {
        while (Box.IsActive)
        {
            yield return 0;
        }

        Note(label);
    }

    /// <summary>0x36 on a text flag: the tick the flag is seen.</summary>
    public IEnumerable<int> Wait36(int flag, string? label = null)
    {
        while (!Box.TemporaryFlags.Contains(flag))
        {
            yield return 0;
        }

        Note(label ?? $"0x36 T{flag}");
    }

    /// <summary>0x37 n: advances in the tick T0 + n + 1.</summary>
    public IEnumerable<int> Wait37(int n)
    {
        var count = 0;
        yield return 0;
        while (true)
        {
            var old = count;
            count++;
            if (old >= n)
            {
                yield break;
            }

            yield return 0;
        }
    }

    /// <summary>0x00: the break of the tick.</summary>
    public IEnumerable<int> Break()
    {
        yield return 0;
    }

    /// <summary>k more ticks, for a wait of the physics (0x24, 0x0B).</summary>
    public IEnumerable<int> Hold(int k)
    {
        for (var i = 0; i < k; i++)
        {
            yield return 0;
        }
    }

    public void Op(string name, params int[] args)
    {
        switch (name)
        {
            case "4C": Box.Op4C(args[0]); break;
            case "4D": Box.Op4D(); break;
            case "4E": Box.Op4E(args[0]); break;
            case "4F": Box.Op4F(); break;
            case "50": Box.Op50(args[0]); break;
            case "51": Box.Op51(); break;
            default: throw new ArgumentException("not a dialogue opcode: " + name);
        }

        Note("0x" + name + string.Concat(args.Select(a => " " + a)));
    }
}
