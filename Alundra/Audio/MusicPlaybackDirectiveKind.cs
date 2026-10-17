#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Audio;

/// <summary>What <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective(int)"/> says to do with the
/// currently owned music voice - the CONSUMER-side half of docs/plan-e11c-musique.md's D-C-2 (the
/// exported table is raw; interpreting it is this type's job), straight off fact 1.1's own filter
/// order (<c>SoundManager.cs</c> <c>LoadMapSoundsCore</c>, lines 5168/5173/5183/5186). Deliberately
/// does NOT cover the "same index as currently playing" guard (fact 1.1's second row) - that
/// comparison needs SESSION state (the previously resolved index) this pure table has no business
/// holding (D-C-6); <see cref="AlundraMusicPlayer"/> applies it itself, against the RAW value below,
/// before ever calling this method.</summary>
public enum MusicPlaybackDirectiveKind
{
    /// <summary>Raw value 0 - total short-circuit, the original never even looks at what is currently
    /// playing (fact 1.1, <c>SoundManager.cs:5168</c>).</summary>
    NoOp,

    /// <summary>Raw value 45 (0x2D) - stop whatever is playing, load nothing new
    /// (<c>SoundManager.cs:5183</c>).</summary>
    Stop,

    /// <summary>Play <see cref="MusicPlaybackDirective.PlayIndex"/> - either the raw value itself, or 1
    /// when the raw value was -1 (<c>LoadMapSequenceCore:532-535</c> remaps it).</summary>
    Play,
}
