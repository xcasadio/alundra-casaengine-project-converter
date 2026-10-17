#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;

namespace Alundra.Audio;

/// <summary>One resolved outcome of <see cref="AlundraMusicIndexTable.ResolvePlaybackDirective(int)"/>
/// - see <see cref="MusicPlaybackDirectiveKind"/> for what each case means.</summary>
public readonly struct MusicPlaybackDirective
{
    public MusicPlaybackDirectiveKind Kind { get; }
    public int PlayIndex { get; }

    private MusicPlaybackDirective(MusicPlaybackDirectiveKind kind, int playIndex)
    {
        Kind = kind;
        PlayIndex = playIndex;
    }

    public static readonly MusicPlaybackDirective NoOp = new(MusicPlaybackDirectiveKind.NoOp, 0);
    public static readonly MusicPlaybackDirective Stop = new(MusicPlaybackDirectiveKind.Stop, 0);
    public static MusicPlaybackDirective Play(int index) => new(MusicPlaybackDirectiveKind.Play, index);
}
