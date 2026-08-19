#nullable enable
namespace Alundra.Scripts;

/// <summary>
/// V1 sliver of the original's game-flag storage, just enough for the event-program interpreter's
/// flag opcodes (0x05 FlagOn, 0x06 FlagOff, 0x31 IfFlagOff and siblings). Ported from
/// <c>GameEngine.GetFlag</c>/<c>AddFlag</c>/<c>SetFlag</c>/<c>XorFlag</c> (GameEngine.cs:2828-2926) and
/// <c>GameInitializer.ResetGameFlags</c> @ 0x800814a0 (GameInitializer.cs:483-494), which zeroes every
/// flag word for a New Game - this class starts zeroed the same way, so it needs no explicit reset.
///
/// The original selects between two flag banks by the flag id's 0x8000 bit: below it, the persistent
/// save-game flags (<c>g_saveData.GameFlags</c>); at/above it, session-only <c>g_temporaryFlags</c>.
/// Both are indexed the same way, <c>(flag &gt;&gt; 5) &amp; 0x3ff</c> (the original computes this as
/// <c>((flag &gt;&gt; 3) &amp; 0xffc) &gt;&gt; 2</c>, an equivalent formulation left as a comment on the
/// original's own GetFlag), giving up to 1024 32-bit words per bank - both arrays here are sized to
/// that upper bound. This is the full extent of E3's InitializeGameState port pulled forward; anything
/// else that method sets up (items, HP, map index tables, ...) is out of scope for this interpreter.
/// </summary>
public sealed class AlundraGameState
{
    private const int WordCount = 1024;
    private const uint TemporaryFlagBit = 0x8000;

    /// <summary>Persistent save-game flags (<c>g_saveData.GameFlags</c>) - all zero, matching New Game.</summary>
    public readonly uint[] GameFlags = new uint[WordCount];

    /// <summary>Session-only flags (<c>g_temporaryFlags</c>) - all zero at construction.</summary>
    public readonly uint[] TemporaryFlags = new uint[WordCount];

    private static int IndexOf(uint flag) => (int)((flag >> 5) & 0x3ff);

    private uint[] BankFor(uint flag) => (flag & TemporaryFlagBit) == 0 ? GameFlags : TemporaryFlags;

    /// <summary>GameEngine.GetFlag (GameEngine.cs:2828-2845).</summary>
    public uint GetFlag(uint flag) => BankFor(flag)[IndexOf(flag)];

    /// <summary>GameEngine.AddFlag (GameEngine.cs:2871-2888) - ORs <paramref name="mask"/> in.</summary>
    public void AddFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] |= mask;

    /// <summary>GameEngine.SetFlag (GameEngine.cs:2890-2907) - ANDs <paramref name="mask"/> in (the
    /// caller passes the complement of the bit it wants cleared, e.g. Script_6_006's <c>~mask</c>).</summary>
    public void SetFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] &= mask;

    /// <summary>GameEngine.XorFlag (GameEngine.cs:2909-2926).</summary>
    public void XorFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] ^= mask;
}
