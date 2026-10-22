#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Cells;
using Alundra.Audio;
using Alundra.Maps;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>
/// E20.c1b-2 (docs/plan-e20-heros.md section 1.7): a recorder of the sounds asked, stamped with the frame of the arc.
/// </summary>
internal sealed class LeverStampedSounds : ISoundPlayer
{
    private readonly Func<int> _frame;

    public LeverStampedSounds(Func<int> frame) => _frame = frame;

    public List<(int Frame, int Id)> Requests { get; } = new();

    public void PlaySfx(int sfxId) => Requests.Add((_frame(), sfxId));

    public void RemixVoice(int sfxId, int left, int right)
    {
    }

    public void FlushFrameSounds()
    {
    }

    public void StopAllSfx()
    {
    }

    /// <summary>Whether the sound <paramref name="id"/> was asked at the frame <paramref name="frame"/> (the frame during which the request ran).</summary>
    public bool Contains(int frame, int id) => Requests.Contains((frame, id));

    /// <summary>Installs a recorder on the world proxy of the arc (the proxy of the headless montage has no sound player).</summary>
    public static LeverStampedSounds Inject(ArcRun arc)
    {
        var player = new LeverStampedSounds(() => arc.Frame);
        typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(arc.Proxy, player);
        return player;
    }
}
