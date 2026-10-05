#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Dialogue.Presentation;
using CasaEngine.Framework.Dialogue.Runtime;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>A sound player that records what it is asked, with the frame of the montage it was asked in.</summary>
internal sealed class PassStampedSoundPlayer : IAlundraSoundPlayer
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

/// <summary>
/// E19.f2a F2A-3: the production order of a logic tick, at the proxy's own <see cref="AlundraWorldProxy.Update"/> (the same headless world as
/// <see cref="AlundraDialogueFramePassTests"/>), with the scripts as lambdas: the montage runs, each frame, <b>the scripts of the entities first</b> (the engine
/// updates every entity, scripts included, BEFORE the world's own proxy, <c>World.Update</c>, which is why an entity script sees a box one tick early), under the
/// gate the entities obey (<c>GameplayBlockedMask</c>), then <see cref="AlundraWorldProxy.Update"/> - the pad pass, then at each tick the pass of the box, the gate
/// read again, and the map events (each map-event script is called once per tick the gate lets the map events run: a record whose zone holds the player).
/// A test writes the square button of a frame with <see cref="PadForFrame"/>; <see cref="Frame"/> is the number of frames done, so inside a script it is the index of
/// the frame being run.
/// </summary>
internal sealed class DialogueBoxMontage : IDisposable
{
    private sealed class ScriptedRunner : IEventProgramRunner
    {
        private readonly DialogueBoxMontage _montage;

        public ScriptedRunner(DialogueBoxMontage montage) => _montage = montage;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (programSlot == ScriptHelper.ProgramBMap)
            {
                _montage.MapEventScript?.Invoke();
            }
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity) { }
    }

    private readonly AlundraEntityScriptProxy _player;

    public DialogueBoxMontage()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();

        var world = new World { Name = "TestWorld" };
        world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });

        Proxy = new AlundraWorldProxy();
        Proxy.InitializeWithWorld(world); // no "tileMap" entity: the install returns early, PlayerEntity stays null (the montage seeds it below).

        _player = new AlundraEntityScriptProxy { Status = EntityStatus.Normal, PosX = 1 << 16, PosY = 2 << 16, PosZ = 0, TileX = 0, TileY = 0 };
        Proxy.PlayerEntity = _player;
        Proxy.EntityFollowedByCamera = _player;

        var record = new TileMapObjectData();
        record.CustomProperties["EventCodesBIndex"] = "129";
        record.CustomProperties["Index"] = "1";
        record.CustomProperties["X1"] = "0";
        record.CustomProperties["Y1"] = "0";
        record.CustomProperties["X2"] = "100";
        record.CustomProperties["Y2"] = "100";
        var layer = new TileMapObjectLayerData();
        layer.Objects.Add(record);
        Proxy.BuildMapEvents(layer);
        Proxy.EventProgramRunner = new ScriptedRunner(this);

        Sounds = new PassStampedSoundPlayer(() => Frame, () => AlundraDialogueDirector.Instance.PassCountForTests);
        Presenter = new PrefixRecordingPresenter(() => AlundraDialogueDirector.Instance.PassCountForTests);
        AlundraDialogueDirector.Instance.AttachToWorld(Presenter, State, Sounds);
        AlundraDialogueDirector.Instance.InstallForMapEntry();
    }

    public AlundraWorldProxy Proxy { get; }

    public AlundraGameState State => Proxy.GameState;

    public AlundraDialogueDirector Director => AlundraDialogueDirector.Instance;

    public PassStampedSoundPlayer Sounds { get; }

    public PrefixRecordingPresenter Presenter { get; }

    /// <summary>The number of frames done: inside a script, the index of the frame being run.</summary>
    public int Frame { get; private set; }

    /// <summary>The scripts of the entities (run before the proxy's update, under the entities' gate); receives the index of the frame.</summary>
    public Action<int>? EntityScript { get; set; }

    /// <summary>The map-event script, called once per tick the map events run.</summary>
    public Action? MapEventScript { get; set; }

    /// <summary>The hold bits written for a frame (before its update), or null for no button at all.</summary>
    public Func<int, uint>? PadForFrame { get; set; }

    /// <summary>One frame: the entities' scripts, the pad, then the proxy's update of <paramref name="seconds"/> (0.02 is one logic tick, 0.06 three).</summary>
    public void RunFrame(float seconds = 0.02f)
    {
        // The pad of the frame is decided at its start, before any script (the helper of the arcs reads the director there), and read by the proxy's update.
        State.LastPadState = new AlundraPadState { ButtonsHold = PadForFrame?.Invoke(Frame) ?? 0 };
        if ((State.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) == 0)
        {
            EntityScript?.Invoke(Frame);
        }

        Proxy.Update(seconds);
        Frame++;
    }

    public void RunFrames(int count, float seconds = 0.02f)
    {
        for (var i = 0; i < count; i++)
        {
            RunFrame(seconds);
        }
    }

    /// <summary>The first frame at which <paramref name="condition"/> holds after its update, scanning up to <paramref name="frames"/> frames from now; -1 if never.</summary>
    public int FirstFrameWhere(Func<bool> condition, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            RunFrame();
            if (condition())
            {
                return Frame - 1;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        AlundraDialogueDirector.Instance.ResetForTests();
        AlundraWorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        AlundraGameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        AlundraSoundBank.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
    }
}
