#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Audio;
using Alundra.Dialogues;
using Alundra.Dialogues.Yarn;

namespace Alundra.Tests.Support;

/// <summary>A sound player that records the ids it is asked.</summary>
internal sealed class SpeakerSoundRecorder : ISoundPlayer
{
    public List<int> Played { get; } = new();

    public void PlaySfx(int sfxId) => Played.Add(sfxId);

    public void RemixVoice(int sfxId, int left, int right) { }

    public void FlushFrameSounds() { }

    public void StopAllSfx() { }
}
