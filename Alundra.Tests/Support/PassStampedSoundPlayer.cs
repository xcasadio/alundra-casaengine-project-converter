#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Dialogues;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>A sound player that records what it is asked, with the frame of the montage it was asked in.</summary>
internal sealed class PassStampedSoundPlayer : ISoundPlayer
{
    private readonly Func<int> _frame;
    private readonly Func<int> _pass;

    public PassStampedSoundPlayer(Func<int> frame, Func<int> pass)
    {
        _frame = frame;
        _pass = pass;
    }

    /// <summary>Each sound asked: the index of the frame of the montage, the sound, and the number of passes of the box done when it was asked (the tick of a pass is that number minus 1).</summary>
    public List<(int Frame, int Sfx, int Pass)> Played { get; } = new();

    public void PlaySfx(int sfxId) => Played.Add((_frame(), sfxId, _pass()));

    public void RemixVoice(int sfxId, int left, int right) { }

    public void FlushFrameSounds() { }

    public void StopAllSfx() { }
}

/// <summary>A presenter that records the lines it is shown and the closes it is asked, on top of the engine's own service (so choices work as in production).</summary>
internal sealed class PrefixRecordingPresenter : IDialoguePresenter
{
    private readonly DialogueService _service = new();
    private readonly Func<int> _pass;

    public PrefixRecordingPresenter(Func<int> pass) => _pass = pass;

    /// <summary>Each line shown, with the number of the pass of the box that sent it (0: the opening).</summary>
    public List<(int Pass, string Text)> Shown { get; } = new();

    public int Closes { get; private set; }

    public DialogueRuntimeState State => _service.State;
    public DialogueLine CurrentLine => _service.CurrentLine;
    public bool IsOpen => _service.IsOpen;
    public IReadOnlyList<string> Choices => _service.Choices;
    public bool HasChoices => _service.HasChoices;

    public event EventHandler<DialoguePresentationChangedEventArgs> PresentationChanged
    {
        add => _service.PresentationChanged += value;
        remove => _service.PresentationChanged -= value;
    }

    public event EventHandler<DialogueChoiceSelectedEventArgs> ChoiceSelected
    {
        add => _service.ChoiceSelected += value;
        remove => _service.ChoiceSelected -= value;
    }

    public bool ShowLine(DialogueLine line)
    {
        Shown.Add((_pass(), line.Text));
        return _service.ShowLine(line);
    }

    public bool ShowChoices(IReadOnlyList<string> labels) => _service.ShowChoices(labels);

    public bool SelectChoice(int index) => _service.SelectChoice(index);

    public bool Close()
    {
        Closes++;
        return _service.Close();
    }
}
