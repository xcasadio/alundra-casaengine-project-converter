#nullable enable
using System.Collections.Generic;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e0c2: a director of the music that only records its calls ("LoadBgm(i)", "StopAllSound"), each with the state of the host the moment it was called
/// (the number of <c>Block</c> calls and of opened messages), so that a test proves the order of the binary: Block, the music, then the message.
/// </summary>
internal sealed class RecordingBgmFadeDirector : IBgmFadeDirector
{
    private readonly PickupHost _host;
    private readonly FakeDialogueDirector _dialogue;

    public RecordingBgmFadeDirector(PickupHost host, FakeDialogueDirector dialogue)
    {
        _host = host;
        _dialogue = dialogue;
    }

    /// <summary>The calls, in order.</summary>
    public List<string> Calls { get; } = new();

    /// <summary>For each call, the number of <c>Block</c> calls and of messages opened on the host when it happened.</summary>
    public List<(int Blocked, int Opened)> HostAtCall { get; } = new();

    public bool IsArmed => false;

    public void StopAllSound() => Record("StopAllSound");

    public void LoadBgm(int bgmIndex) => Record("LoadBgm(" + bgmIndex + ")");

    public void Advance(int ticks)
    {
    }

    private void Record(string call)
    {
        Calls.Add(call);
        HostAtCall.Add((_host.Blocked.Count, _dialogue.Opens.Count));
    }
}
