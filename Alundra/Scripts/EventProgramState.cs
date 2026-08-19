#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// Port of AlundraEngine.Gameplay.Scripts.EventProgramState (alundra-datas-analyser
/// AlundraTools/AlundraEngine/Gameplay/Scripts/EventProgramState.cs) - the interpreter's per-program
/// execution cursor: which bytecode blob it is walking (<see cref="Codes"/>), where in it
/// (<see cref="CodeIndex"/>), the last-fetched opcode byte (<see cref="Sp"/> - named after the original's
/// own misleading comment, "pointer on code =&gt; use Codes instead"), a scratch register most
/// conditional/waiting opcodes stash their own state in (<see cref="Parameters"/>), and the last
/// true/false outcome a condition opcode left behind for a following conditional-goto to consume
/// (<see cref="Result"/>).
///
/// Fields <see cref="_30"/>/<see cref="_34"/> are carried over unused (as in the original, whose own
/// name for them is just the struct offset) - kept for field-layout fidelity, not read by any ported
/// opcode handler.
/// </summary>
public sealed class EventProgramState
{
    /// <summary>Last-fetched opcode byte (Codes[CodeIndex] at the start of the current fetch) - not a
    /// pointer despite the name (see original's own comment).</summary>
    public int Sp;

    public readonly int[] Parameters = new int[10];

    /// <summary>Last true/false (1/0) outcome a condition opcode (e.g. IfTrueGoto's predicate) left
    /// behind.</summary>
    public int Result;

    public int _30;
    public int _34;

    /// <summary>The bytecode blob this program is walking - null means "no program loaded" (mirrors the
    /// original's own null check in FillDataFromCommand/RunScript).</summary>
    public byte[]? Codes;

    public int CodeIndex;

    public void CopyFrom(EventProgramState other)
    {
        Sp = other.Sp;
        Codes = other.Codes;
        CodeIndex = other.CodeIndex;
        Array.Copy(other.Parameters, Parameters, Parameters.Length);
        Result = other.Result;
        _30 = other._30;
        _34 = other._34;
    }
}
