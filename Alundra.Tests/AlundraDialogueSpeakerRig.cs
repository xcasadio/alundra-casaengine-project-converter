#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Alundra.Scripts;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Dialogue.Assets;
using CasaEngine.Framework.Dialogue.Runtime;

namespace Alundra.Tests;

/// <summary>A sound player that records the ids it is asked.</summary>
internal sealed class SpeakerSoundRecorder : IAlundraSoundPlayer
{
    public List<int> Played { get; } = new();

    public void PlaySfx(int sfxId) => Played.Add(sfxId);

    public void RemixVoice(int sfxId, int left, int right) { }

    public void FlushFrameSounds() { }

    public void StopAllSfx() { }
}

/// <summary>
/// E19.f4b (docs/plan-e19-opcodes.md, F4B-1, "T1, contrat fermé"): the montage at the level of the DIRECTOR, in the binary's order - the real session-scoped
/// <see cref="AlundraDialogueDirector"/> over a presenter and a game state, the real <see cref="AlundraEventProgramRunner"/> on synthetic bytecode (<c>0x0D</c>,
/// <c>0x5C</c>, <c>0xC4</c>) over a context whose <see cref="IEntityWorldContext.SpawnedEntities"/> the test sets, the names from an ETC asset of the 60 names of the
/// binary, the advances of the glyphs from the oracle of the text box, the scroll of the camera from <see cref="AlundraDialogueDirector.ScrollSource"/>. The text ids
/// <c>0x81</c> and <c>0x82</c> are the pages "AB" and "CD" of the map-1 asset.
/// </summary>
internal sealed class SpeakerRig : IDisposable
{
    public const int TextAb = 0x81;
    public const int TextCd = 0x82;

    /// <summary>A world with a settable list of spawned entities (the searches read it) and no hero.</summary>
    internal sealed class Context : IEntityWorldContext
    {
        public List<AlundraEntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities => Spawned;
        public AlundraEntityScriptProxy? PlayerEntity { get; set; }
        public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(AlundraEntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
        public IAlundraDialogueDirector? DialogueDirector { get; set; }
    }

    private static readonly IReadOnlyDictionary<int, (string Name, int Width)> NamesOfTheBinary = SpeakerAnnex.Names();

    public SpeakerRig(bool attachPresenter = true, bool loadEtc = true)
    {
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
        AlundraEtcStringTable.ResetForTests();
        if (loadEtc)
        {
            AlundraEtcStringTable.SetEtcDialogueAssetForTests(DialogueTestAssets.BuildEtc(NamesOfTheBinary.Select(n => (n.Key, n.Value.Name)).ToArray()));
        }

        Director.AdvanceProviderForTests = Advance;
        Director.AttachToWorld(attachPresenter ? new DialogueService() : null, GameState, Sound);
        Director.InstallForMapEntry();
        Director.ScrollSource = () => Camera;
        World.DialogueDirector = Director;
        Runner = new AlundraEventProgramRunner(Document(new[] { 0xFF }), GameState, World) { MapDialogueAsset = MapAsset() };
    }

    public static int Advance(char c) => AlundraTextBoxOracle.GlyphWidth(OraclePages.ByteOf(c));

    public AlundraDialogueDirector Director => AlundraDialogueDirector.Instance;

    public AlundraGameState GameState { get; } = new();

    public SpeakerSoundRecorder Sound { get; } = new();

    public Context World { get; } = new();

    public AlundraEventProgramRunner Runner { get; }

    /// <summary>The scroll of the camera the director reads at the opening and at the return of a portrait.</summary>
    public (int X, int Y) Camera { get; set; } = (40, 20);

    public static EventProgramDocument Document(int[] codes) => new()
    {
        MapIndex = 1,
        EventCodesATable = new[] { 0, 0, 0, 0, 0, 0 },
        Codes = codes,
    };

    public static DialogueAsset MapAsset()
        => DialogueTestAssets.BuildMultiNode("Map1", ("M1_S001", new[] { "AB" }), ("M1_S002", new[] { "CD" }));

    public static int[] Codes0D(int textId) => new[] { 0x0D, textId, 1, 0xFF };

    public static int[] Codes5C(int search, int textId) => new[] { 0x5C, search, textId, 1, 0xFF };

    public static int[] CodesC4(int search, int nameId, int textId) => new[] { 0xC4, search, nameId & 0xFF, nameId >> 8, textId, 1, 0xFF };

    /// <summary>A speaker: the sprite type of its record (the id of its name), the flag and the field of its portrait, a position (integer parts; the 16.16 words are those shifted).</summary>
    public static AlundraEntityScriptProxy Speaker(int spriteType, bool portrait, int height = 56, int x = 200, int y = 150, int z = 0, int refId = 0)
        => new()
        {
            SpriteType = spriteType,
            Flags = portrait ? EntityFlags.HasPortrait : 0,
            DialoguePortrait = portrait ? new DialoguePortraitRef(Guid.NewGuid(), 48, height) : null,
            PosX = x << 16,
            PosY = y << 16,
            PosZ = z << 16,
            EntityRefId = refId,
            Status = EntityStatus.Normal,
        };

    /// <summary>One call of the interpreter for <paramref name="owner"/> on a fresh state: true when the box opened (<see cref="AlundraDialogueDirector.OpenSerial"/> grew), false for "retry (0)".</summary>
    public bool Run(AlundraEntityScriptProxy owner, int[] codes)
    {
        var state = new EventProgramState { Codes = Document(codes).CodesAsBytes(), Result = 7 };
        var serial = Director.OpenSerial;
        Runner.RunOneScriptCall(owner, state);
        return Director.OpenSerial != serial;
    }

    /// <summary>One pass of the director on the square of the model's pad (held and just pressed at every frame).</summary>
    public void Pass() => Director.Pass(true, true);

    /// <summary>Passes until a pass plays sound 7 (the close trigger of the box); returns the number of passes run, or -1 after <paramref name="limit"/> passes.</summary>
    public int PassUntilCloseTrigger(int limit = 200)
    {
        for (var i = 1; i <= limit; i++)
        {
            Sound.Played.Clear();
            Pass();
            if (Sound.Played.Contains(7))
            {
                return i;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        Director.AdvanceProviderForTests = null;
        Director.ResetForTests();
        AlundraDialogueCapturePresenter.ResetForTests();
        AlundraEtcStringTable.ResetForTests();
    }
}
