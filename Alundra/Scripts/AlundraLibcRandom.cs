#nullable enable

namespace Alundra.Scripts;

/// <summary>
/// The C library <c>rand()</c> of the original executable (<c>ALUN_CD.EXE</c>, France), at
/// <c>0x80081E6C</c>: <c>s = s * 0x41C64E6D + 0x3039</c> (32 bits, <c>0x80081E7C</c>-<c>0x80081E84</c>), stored at
/// <c>0x801EEB48</c>, and the result is <c>(s &gt;&gt; 16) &amp; 0x7FFF</c> (<c>0x80081E90</c>/<c>0x80081E98</c>).
/// It is a stream of its own, not <see cref="AlundraRandom"/> (the game's shared stream): the binary draws from it in
/// exactly three places, the respawn of a type 2 cellular cell (<c>0x8005D31C</c>, this DLL's use, ADR-0032) and the two
/// writes of the memory-card block (<c>0x80061150</c>, <c>0x80061584</c>; this DLL's saves draw nothing, O-E19-49).
///
/// The state is 0 when the process starts (the binary clears its BSS at <c>0x8008B548</c>), is never reseeded
/// (<c>srand</c>, <c>0x80081E9C</c>, has no caller) and is never reset in production: the original zeroes it only
/// at boot. The tests reach it through <see cref="State"/>.
/// </summary>
public static class AlundraLibcRandom
{
    internal static uint State;

    /// <summary>The next value of the C library <c>rand()</c>, 0 to 0x7FFF.</summary>
    public static int Next()
    {
        State = unchecked(State * 0x41C64E6D + 0x3039);
        return (int)((State >> 16) & 0x7FFF);
    }
}
