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
using Alundra.Tests.Dialogues;

namespace Alundra.Tests.Support;

/// <summary>
/// E19.f2a F2A-3: the production order of a logic tick, at the proxy's own <see cref="WorldProxy.Update"/> (the same headless world as
/// <see cref="DialogueFramePassTests"/>), with the scripts as lambdas: the montage runs, each frame, <b>the scripts of the entities first</b> (the engine
/// updates every entity, scripts included, BEFORE the world's own proxy, <c>World.Update</c>, which is why an entity script sees a box one tick early), under the
/// gate the entities obey (<c>GameplayBlockedMask</c>), then <see cref="WorldProxy.Update"/> - at each tick the pad pass, the pass of the box, the gate
/// read again, and the map events (each map-event script is called once per tick the gate lets the map events run: a record whose zone holds the player), and last the inventory block (E19.u).
/// A test writes the square button of a frame with <see cref="PadForFrame"/>; <see cref="Frame"/> is the number of frames done, so inside a script it is the index of
/// the frame being run.
/// </summary>
internal sealed class DialogueBoxMontage : IDisposable
{
    private sealed class ScriptedRunner : IEventProgramRunner
    {
        private readonly DialogueBoxMontage _montage;

        public ScriptedRunner(DialogueBoxMontage montage) => _montage = montage;

        public void RunScript(EntityScriptProxy entity, int programSlot)
        {
            if (programSlot == ScriptHelper.ProgramBMap)
            {
                _montage.MapEventScript?.Invoke();
            }
        }

        public void RunSpriteEvent(EntityScriptProxy entity) { }
    }

    private readonly EntityScriptProxy _player;

    public DialogueBoxMontage()
    {
        DialogueDirector.Instance.ResetForTests();
        WorldProxy.SetDebugCameraPanEnabledOverrideForTests(true);
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();

        var world = new World { Name = "TestWorld" };
        world.Entities.Add(new Entity { Name = "camera", RootComponent = new Camera2dComponent() });

        Proxy = new WorldProxy();
        Proxy.InitializeWithWorld(world); // no "tileMap" entity: the install returns early, PlayerEntity stays null (the montage seeds it below).

        _player = new EntityScriptProxy { Status = EntityStatus.Normal, PosX = 1 << 16, PosY = 2 << 16, PosZ = 0, TileX = 0, TileY = 0 };
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

        Sounds = new PassStampedSoundPlayer(() => Frame, () => DialogueDirector.Instance.PassCountForTests);
        Presenter = new PrefixRecordingPresenter(() => DialogueDirector.Instance.PassCountForTests);
        DialogueDirector.Instance.AttachToWorld(Presenter, State, Sounds);
        DialogueDirector.Instance.InstallForMapEntry();
    }

    public WorldProxy Proxy { get; }

    public GameState State => Proxy.GameState;

    public DialogueDirector Director => DialogueDirector.Instance;

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
        State.LastPadState = new PadState { ButtonsHold = PadForFrame?.Invoke(Frame) ?? 0 };
        if ((State.PlayerControlFlags & GameState.PlayerControlBits.GameplayBlockedMask) == 0)
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
        DialogueDirector.Instance.ResetForTests();
        WorldProxy.SetDebugCameraPanEnabledOverrideForTests(null);
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }
}
