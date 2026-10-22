#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using CasaEngine.Framework.Audio;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>One streaming voice the backend created (E19.L-b3): the music now plays as a stream, so this is what
/// the music tests read where they read <see cref="FakeAudioBackend.PlayCalls"/> before.</summary>
public sealed record FakeStreamVoice(AudioVoiceHandle Voice, int SampleRate, int ChannelCount, AudioVoiceParameters Parameters);
