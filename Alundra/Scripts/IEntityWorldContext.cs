#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;

namespace Alundra.Scripts;

/// <summary>
/// Seam over the world-level entity services the entity search/manipulation opcodes need
/// (0x2D ActivateEntity, 0x2E DestroyEntity, 0x62/0x63/0x64/0x65/0xAC, all of which fan out through
/// <see cref="EntitySearchService"/>). Implemented by <see cref="AlundraWorldProxy"/> (which owns
/// <c>_spawnedEntities</c> and the live <c>World</c>); <see cref="AlundraEventProgramRunner"/> depends on
/// this interface rather than the concrete world proxy, so the interpreter stays unit-testable with a
/// fake context instead of a live <c>World</c>.
/// </summary>
public interface IEntityWorldContext
{
    /// <summary>
    /// Every entity this world has spawned so far this session, in creation order - mirrors the
    /// original's flat <c>g_entitySlots</c> array (see <see cref="EntitySearchService"/>'s class doc).
    /// A snapshot taken fresh on each read: an entity dynamically spawned by 0x2D earlier in the same
    /// script call is visible to a following search within that same call, exactly like the original's
    /// live array.
    /// </summary>
    IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; }

    /// <summary>
    /// The New Game hero entity (port of <c>ResetEntityState</c>, GameEngine.cs:648-670 - see
    /// <see cref="AlundraWorldProxy.PlayerEntity"/>'s own doc), or null when this world spawned none (no
    /// hero asset in the catalog, no prefab loader, etc. - see that same doc). Needed by
    /// <see cref="AlundraEventProgramRunner.RunScript"/>'s slot F policy (EntityEventHandlers.cs:268-273:
    /// slot F always zeroes the PLAYER's own forces, not the entity being run).
    /// </summary>
    AlundraEntityScriptProxy? PlayerEntity { get; }

    /// <summary>
    /// Port of <c>g_entityFollowedByCamera</c> (GameEngine.cs) - the entity the camera's per-tick
    /// look-at follows (<see cref="AlundraWorldProxy"/>'s own camera-follow pass, E5.a). Written by
    /// opcode 0x67 (first match of an <see cref="EntitySearchService"/> search, or <c>null</c> when
    /// nothing matched - faithful, no fallback), nulled by 0x68 and by 0x69 (which also forces the
    /// look-at position directly - see <see cref="SetForcedCameraLookAt"/>). Settable so the
    /// opcode handlers below can write it without depending on the concrete world proxy.
    /// </summary>
    AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

    /// <summary>
    /// Port of opcode 0x69 (Script_105_069, EntityEventHandlers.cs:2082-2089): nulls
    /// <see cref="EntityFollowedByCamera"/> and imposes <paramref name="x"/>/<paramref name="y"/>/
    /// <paramref name="z"/> as the camera's look-at position directly (already plain pixel ints, not
    /// 16.16 fixed-point - same units <c>g_cameraLookAtX/Y/Z</c> carries).
    /// </summary>
    void SetForcedCameraLookAt(int x, int y, int z);

    /// <summary>
    /// Dynamic spawn by entity-record id - backs opcode 0x2D (Script_45_02D), which always calls the
    /// original's <c>GameEngine.SpawnEntity(logicEntity, entityRecordId, notCheckSpawnZone: 1)</c>.
    /// Returns null when the record is disabled/missing or the spawn otherwise fails (prefab loader
    /// unavailable, etc.) - the original breakpoints (debug-only trap) in that case instead.
    /// </summary>
    AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId);

    /// <summary>
    /// Marks <paramref name="entity"/> for destruction - backs the single-argument
    /// <c>GameEngine.DestroyEntity(Entity)</c> @ 0x8003A774 every search-driven destroy opcode (0x2E)
    /// calls per match, distinct from <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy,int)"/>
    /// (the two-argument overload the pick-phase status machine uses, which also spawns break-effect
    /// contents).
    /// </summary>
    void DestroyEntity(AlundraEntityScriptProxy entity);

    /// <summary>
    /// This world's navigation grid (E4.d, docs/plan-e4-deplacement-scripte.md decision E4-2), built once
    /// by <see cref="AlundraWorldProxy.InitializeWithWorld"/> right after <c>World.CollisionField</c> from
    /// the SAME <c>TileMapData</c>, in "cell space" (<c>cellSize = 1</c>) - the DLL, not the engine's own
    /// <c>CharacterControllerNavigationDriverComponent</c> (not used in E4), does its own px&lt;-&gt;cell
    /// conversion (see <see cref="AlundraEventProgramRunner"/>'s own 0x1E walk-detour helpers). Null in
    /// degraded mode (missing navigation layer/tileset, or a world with no tilemap at all) - every reader
    /// treats that as "no detour available, keep pushing" (0x1E's own original behavior). Settable by
    /// fakes so a test can inject a synthetic grid without a live <see cref="AlundraWorldProxy"/> (map 389
    /// itself has 0 blocked navigation cells - E4.a's own finding - so an obstacle test needs one).
    /// </summary>
    NavigationGrid2D? NavigationGrid { get; }

    /// <summary>
    /// This world's cell-mutation seam (E7.a, docs/plan-e7-mutation-tuiles.md) - backs opcodes
    /// 0x54/0x55/0x85 in <see cref="AlundraEventProgramRunner.Dispatch"/>. A default interface member
    /// (rather than a required one) so every EXISTING implementer - <see cref="NoOpEntityWorldContext"/>,
    /// every test fake, <see cref="AlundraWorldProxy"/> itself - keeps compiling unmodified, defaulting to
    /// null (the same "degraded, skip by size" fallback <see cref="AlundraEventProgramRunner"/> already
    /// applies when a world has no mutator installed). Only the intro trace harness's
    /// <c>HeadlessIntroSimulation</c> overrides this, backed by a real <see cref="AlundraCellStore"/> built
    /// from the SAME parsed records its collision field aliases (E7.a's "production call site"
    /// acceptance) - E7.b wires a real one into <see cref="AlundraWorldProxy"/> itself.
    /// </summary>
    IAlundraCellMutator? CellMutator => null;

    /// <summary>
    /// This world's sound-effect playback seam (E11.a, docs/plan-e11-audio.md) - backs opcodes
    /// 0xBD/0xBE/0x12/0x75 in <see cref="AlundraEventProgramRunner.Dispatch"/>. A default interface
    /// member (rather than a required one) so every EXISTING implementer - <see cref="NoOpEntityWorldContext"/>,
    /// every test fake, <see cref="AlundraWorldProxy"/> itself - keeps compiling unmodified, defaulting
    /// to null (the same "degraded, skip by size" fallback <see cref="AlundraEventProgramRunner"/> already
    /// applies when a world has no sound player installed). <see cref="AlundraWorldProxy"/> installs a
    /// real <see cref="AlundraSoundPlayer"/> once a <c>Game</c>'s <c>AudioSystemComponent</c> exists
    /// (<see cref="AlundraWorldProxy.InstallAudioSystems"/>); the intro trace harness's
    /// <c>HeadlessIntroSimulation</c> installs a fake instead, gated by its own
    /// <c>installSoundPlayer</c> flag (same neutralization shape as <see cref="CellMutator"/>).
    /// </summary>
    IAlundraSoundPlayer? SoundPlayer => null;

    /// <summary>
    /// This session's background-music playback seam (docs/plan-e11c-musique.md, slice C1) - NOT
    /// opcode-backed (fact 1.5: nothing in the intro's own programs changes music), driven instead at
    /// map entry by <see cref="AlundraWorldProxy.InitializeWithWorld"/> itself (item 4 of the plan's own
    /// contract). A default interface member, same "degraded, skip" shape as <see cref="SoundPlayer"/>
    /// above, defaulting to null so every EXISTING implementer keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> installs the session-scoped <see cref="AlundraMusicPlayer.Instance"/>
    /// once a <c>Game</c>'s <c>AudioSystemComponent</c> exists (see that class's own doc for why it is a
    /// singleton rather than a per-world instance - D-C-6).
    /// </summary>
    IAlundraMusicPlayer? MusicPlayer => null;

    /// <summary>
    /// This session's screen fade/tint seam (E10.b, docs/plan-e10-fondu.md) - backs opcodes
    /// 0xAF/0xB0/0xB1 in <see cref="AlundraEventProgramRunner.Dispatch"/>. A default interface member,
    /// same "degraded, skip" shape as <see cref="SoundPlayer"/>/<see cref="MusicPlayer"/> above,
    /// defaulting to null so every EXISTING implementer keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> installs the session-scoped <see cref="AlundraScreenFadeDirector.Instance"/>
    /// (D-E10-6 - see that class's own doc for why it is a singleton rather than a per-world instance).
    /// </summary>
    IAlundraScreenFadeDirector? ScreenFadeDirector => null;

    /// <summary>
    /// This session's dialogue-flow seam (E12.a, docs/plan-e12-dialogues.md) - backs opcodes
    /// 0x0D/0x39/0x44/0x50/0x51/0x5C in <see cref="AlundraEventProgramRunner.Dispatch"/>. A default
    /// interface member, same "degraded, skip" shape as <see cref="SoundPlayer"/>/<see cref="MusicPlayer"/>/
    /// <see cref="ScreenFadeDirector"/> above, defaulting to null so every EXISTING implementer
    /// (<see cref="NoOpEntityWorldContext"/>, every existing test fake) keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> installs the session-scoped <see cref="AlundraDialogueDirector.Instance"/>
    /// (see that class's own doc for why it is a singleton rather than a per-world instance - same D-C-6/
    /// D-E10-6 lesson as <see cref="AlundraMusicPlayer"/>/<see cref="AlundraScreenFadeDirector"/>) via
    /// <see cref="AlundraWorldProxy.InstallDialogueSystems"/>. Unlike <see cref="ScreenFadeDirector"/>, this
    /// member stays NULLABLE even on <see cref="AlundraWorldProxy"/> itself: <see cref="IAlundraDialogueDirector.HasPresenter"/>
    /// is what actually distinguishes real from degraded dispatch (the singleton is attached with a null
    /// presenter, not omitted, when no UI view is available - see <see cref="AlundraDialogueDirector.AttachToWorld"/>'s
    /// own doc), so a null CONTEXT member here only ever means "this context has no dialogue system wired at
    /// all" (most synthetic interpreter tests, <see cref="NoOpEntityWorldContext"/>).
    /// </summary>
    IAlundraDialogueDirector? DialogueDirector => null;
}

/// <summary>
/// V1 default <see cref="IEntityWorldContext"/>: no entities, every spawn/destroy call is a logged no-op.
/// Used when an <see cref="AlundraEventProgramRunner"/> is constructed without a real context (e.g. most
/// synthetic interpreter tests, which do not exercise the search/manipulation opcodes).
/// </summary>
public sealed class NoOpEntityWorldContext : IEntityWorldContext
{
    public static readonly NoOpEntityWorldContext Instance = new();

    public IReadOnlyList<AlundraEntityScriptProxy> SpawnedEntities { get; } = System.Array.Empty<AlundraEntityScriptProxy>();

    public AlundraEntityScriptProxy? PlayerEntity => null;

    public AlundraEntityScriptProxy? EntityFollowedByCamera { get; set; }

    public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;

    public AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId) => null;

    public void DestroyEntity(AlundraEntityScriptProxy entity)
    {
        // No-op: no world to mutate.
    }

    public NavigationGrid2D? NavigationGrid => null;
}
