#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;
using Alundra.Scripts;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Combat;
using Alundra.Items;
using Alundra.Cells;
using Alundra.Cameras;
using Alundra.Rendering.Effects;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Dialogues;
using Alundra.SaveGames;

namespace Alundra.Maps;

/// <summary>
/// Seam over the world-level entity services the entity search/manipulation opcodes need
/// (0x2D ActivateEntity, 0x2E DestroyEntity, 0x62/0x63/0x64/0x65/0xAC, all of which fan out through
/// <see cref="EntitySearchService"/>). Implemented by <see cref="AlundraWorldProxy"/> (which owns
/// <c>_spawnedEntities</c> and the live <c>World</c>); <see cref="EventProgramRunner"/> depends on
/// this interface rather than the concrete world proxy, so the interpreter stays unit-testable with a
/// fake context instead of a live <c>World</c>.
/// </summary>
public interface IEntityWorldContext
{
    /// <summary>
    /// Every entity this world has spawned so far this session, in the order of their slots (E20.e0a2; the hero's slot 0 first) - mirrors the
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
    /// <see cref="EventProgramRunner.RunScript"/>'s slot F policy (EntityEventHandlers.cs:268-273:
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
    /// Dynamic spawn by entity-record id - backs opcodes 0x2D (Script_45_02D), 0x8A and 0x8B, which always call the
    /// original's <c>GameEngine.SpawnEntity(logicEntity, entityRecordId, notCheckSpawnZone: 1)</c>.
    /// Returns null when the record is disabled/missing or the spawn otherwise fails (prefab loader
    /// unavailable, etc.) - the original breakpoints (debug-only trap) in that case instead.
    /// </summary>
    AlundraEntityScriptProxy? SpawnEntityByRecordId(AlundraEntityScriptProxy logicEntity, int entityRecordId);

    /// <summary>
    /// E20.e0a1 (docs/plan-e20-heros.md section 1.16.1, D-E0A-1 to D-E0A-5, ADR-0063): the apparition of an entity by sprite index - <c>SpawnWarpEntity</c> (<c>0x80039F58</c>, 102 call sites in the binary: the shot of the hero,
    /// the use of the items, the loot, the enemies' handlers). <paramref name="isMapPage"/> and <paramref name="index"/> name the sprite in the table of the global page or of the current map; <paramref name="x"/> and
    /// <paramref name="y"/> are the 16.16 position; <paramref name="z"/> is in the frame of the DLL, the one a caller reads in <c>PosZ</c> (the interface adds the raw unit the binary's argument has, D-E0A-1: an arrow
    /// spawned at the Z of the hero is one raw unit above him, as in the binary; a constant of the binary is passed minus one); <paramref name="direction"/> is the direction word (0, 8, 0x10, 0x18). <paramref name="parent"/> may be null.
    /// Returns null - with one line of log and nothing added to the world - for an index outside 0 to 255, a number the table of the page does not hold (the map page is checked against the current map), a sprite whose prefab the
    /// export does not hold, and (E20.e0a2) a full table of cases: the caller MUST test it (the binary writes through the null pointer, A11 <c>0x80061870</c>, A21 <c>0x80061A2C</c>). A default interface member, null, so every existing
    /// implementer keeps compiling; <see cref="AlundraWorldProxy"/> implements it. Not called by any production path in E20.e0a1.
    /// </summary>
    AlundraEntityScriptProxy? SpawnEntityBySpriteIndex(AlundraEntityScriptProxy? parent, bool isMapPage, int index, int x, int y, int z, int direction) => null;

    /// <summary>
    /// Marks <paramref name="entity"/> for destruction - backs the single-argument
    /// <c>GameEngine.DestroyEntity(Entity)</c> @ 0x8003A774 every search-driven destroy opcode (0x2E)
    /// calls per match, distinct from <see cref="AlundraWorldProxy.DestroyEntity(AlundraEntityScriptProxy,int)"/>
    /// (the two-argument overload the pick phase's destroy branches (slope, VRAM flags) and E1 of the native sprites
    /// call, which since E14.a1b also creates the break effect; the loot stays the named stub of E20.e0).
    /// </summary>
    void DestroyEntity(AlundraEntityScriptProxy entity);

    /// <summary>
    /// This world's navigation grid (E4.d, docs/plan-e4-deplacement-scripte.md decision E4-2), built once
    /// by <see cref="AlundraWorldProxy.InitializeWithWorld"/> right after <c>World.CollisionField</c> from
    /// the SAME <c>TileMapData</c>, in "cell space" (<c>cellSize = 1</c>) - the DLL, not the engine's own
    /// <c>CharacterControllerNavigationDriverComponent</c> (not used in E4), does its own px&lt;-&gt;cell
    /// conversion (see <see cref="EventProgramRunner"/>'s own 0x1E walk-detour helpers). Null in
    /// degraded mode (missing navigation layer/tileset, or a world with no tilemap at all) - every reader
    /// treats that as "no detour available, keep pushing" (0x1E's own original behavior). Settable by
    /// fakes so a test can inject a synthetic grid without a live <see cref="AlundraWorldProxy"/> (map 389
    /// itself has 0 blocked navigation cells - E4.a's own finding - so an obstacle test needs one).
    /// </summary>
    NavigationGrid2D? NavigationGrid { get; }

    /// <summary>
    /// This world's cell-mutation seam (E7.a, docs/plan-e7-mutation-tuiles.md) - backs opcodes
    /// 0x54/0x55/0x85 in <see cref="EventProgramRunner.Dispatch"/>. A default interface member
    /// (rather than a required one) so every EXISTING implementer - <see cref="NoOpEntityWorldContext"/>,
    /// every test fake, <see cref="AlundraWorldProxy"/> itself - keeps compiling unmodified, defaulting to
    /// null (the same "degraded, skip by size" fallback <see cref="EventProgramRunner"/> already
    /// applies when a world has no mutator installed). Only the intro trace harness's
    /// <c>HeadlessIntroSimulation</c> overrides this, backed by a real <see cref="AlundraCellStore"/> built
    /// from the SAME parsed records its collision field aliases (E7.a's "production call site"
    /// acceptance) - E7.b wires a real one into <see cref="AlundraWorldProxy"/> itself.
    /// </summary>
    IAlundraCellMutator? CellMutator => null;

    /// <summary>
    /// This world's sound-effect playback seam (E11.a, docs/plan-e11-audio.md) - backs opcodes
    /// 0xBD/0xBE/0x12/0x75 in <see cref="EventProgramRunner.Dispatch"/>. A default interface
    /// member (rather than a required one) so every EXISTING implementer - <see cref="NoOpEntityWorldContext"/>,
    /// every test fake, <see cref="AlundraWorldProxy"/> itself - keeps compiling unmodified, defaulting
    /// to null (the same "degraded, skip by size" fallback <see cref="EventProgramRunner"/> already
    /// applies when a world has no sound player installed). <see cref="AlundraWorldProxy"/> installs a
    /// real <see cref="AlundraSoundPlayer"/> once a <c>Game</c>'s <c>AudioSystemComponent</c> exists
    /// (<see cref="AlundraWorldProxy.InstallAudioSystems"/>); the intro trace harness's
    /// <c>HeadlessIntroSimulation</c> installs a fake instead, gated by its own
    /// <c>installSoundPlayer</c> flag (same neutralization shape as <see cref="CellMutator"/>).
    /// </summary>
    IAlundraSoundPlayer? SoundPlayer => null;

    /// <summary>
    /// E20.c1b K2 (docs/plan-e20-heros.md section 1.6): this world's three equipment records (<c>0x80146E00-0x80146E14</c>), read by the damage calculation (<see cref="Damage"/>) of the native handlers that
    /// decide on a hit (E14.a) and of the hero's entry (E20.c2). A default interface member, null like <see cref="ItemTables"/>.
    /// </summary>
    AlundraEquipmentRecords? EquipmentRecords => null;

    /// <summary>
    /// E14.a0b T4 (docs/plan-e14-ia-native.md section 1.2): the frozen animation of the hero (<c>0x80146DF8</c>, <see cref="HitPass.HeroAnimationLatch"/>), the second input the damage calculation of the native hit handlers needs next to
    /// <see cref="EquipmentRecords"/> (<see cref="Damage.ComputeNewHp"/>). A default interface member, 0 like a hero that has not swung, so every existing implementer keeps compiling; <see cref="AlundraWorldProxy"/> returns its hit pass's latch.
    /// </summary>
    uint HeroAnimationLatch => 0;

    /// <summary>
    /// This world's item tables (E19.l2, docs/plan-e19-opcodes.md section 1.2m.2) - back the "give item" branch of opcode
    /// 0x82 (max count per item). A default interface member, same "degraded, skip" shape as <see cref="SoundPlayer"/>:
    /// null makes that branch a degraded no-op. <see cref="AlundraWorldProxy"/> returns its own tables.
    /// </summary>
    AlundraItemTables? ItemTables => null;

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
    /// This session's master BGM fade-machine seam (B2, docs/plan-e11b-opcodes-audio.md, D-B-5) - backs
    /// opcodes 0xA5/0xA6 in <see cref="EventProgramRunner.Dispatch"/> (0xA7's own conditional
    /// stop-all also goes through this, orchestrated at the dispatch site - see
    /// <see cref="AlundraBgmFadeDirector"/>'s own class doc). A default interface member, same
    /// "degraded, skip" shape as <see cref="SoundPlayer"/>/<see cref="MusicPlayer"/> above, defaulting
    /// to null so every EXISTING implementer keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> installs the session-scoped
    /// <see cref="AlundraBgmFadeDirector.Instance"/> (see that class's own doc for why it is a singleton
    /// rather than a per-world instance - the same D-C-6/D-E10-6 lesson as
    /// <see cref="AlundraMusicPlayer"/>/<see cref="AlundraScreenFadeDirector"/>).
    /// </summary>
    IAlundraBgmFadeDirector? BgmFadeDirector => null;

    /// <summary>
    /// This session's screen fade/tint seam (E10.b, docs/plan-e10-fondu.md) - backs opcodes
    /// 0xAF/0xB0/0xB1 in <see cref="EventProgramRunner.Dispatch"/>. A default interface member,
    /// same "degraded, skip" shape as <see cref="SoundPlayer"/>/<see cref="MusicPlayer"/> above,
    /// defaulting to null so every EXISTING implementer keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> installs the session-scoped <see cref="AlundraScreenFadeDirector.Instance"/>
    /// (D-E10-6 - see that class's own doc for why it is a singleton rather than a per-world instance).
    /// </summary>
    IAlundraScreenFadeDirector? ScreenFadeDirector => null;

    /// <summary>
    /// This session's camera sway seam (E19.k1, docs/plan-e19-opcodes.md section 1.2k) - backs opcodes 0x8E/0x8F in
    /// <see cref="EventProgramRunner.Dispatch"/>. A default interface member, same "degraded, skip" shape as
    /// <see cref="ScreenFadeDirector"/> above. <see cref="AlundraWorldProxy"/> installs the session-scoped
    /// <see cref="AlundraCameraSway.Instance"/>.
    /// </summary>
    AlundraCameraSway? CameraSway => null;

    /// <summary>
    /// Sets the background layer mask of opcode 0xA4 (E19.k2, docs/plan-e19-opcodes.md section 1.2k.2): bit 0 switches
    /// the layer of identifier 0 on or off, bit 1 the layer of identifier 1 (the identifiers of the binary). Returns
    /// <see langword="true"/> when this context handles backdrops, <see langword="false"/> ("not handled") otherwise -
    /// a default interface member, same "degraded, skip" shape as <see cref="CameraSway"/> above, so every EXISTING
    /// implementer keeps compiling unmodified. <see cref="AlundraWorldProxy"/> forwards it to its backdrop stage.
    /// </summary>
    bool SetBackgroundLayerMask(int mask) => false;

    /// <summary>
    /// This world's effect stage (E19.g G3b, docs/plan-e19-opcodes.md section 1.2o.6) - backs opcodes 0x90 to 0x94 and 0xA0 to 0xA3 in
    /// <see cref="EventProgramRunner.Dispatch"/>, which queue their operation on it. A default interface member, same "degraded, skip by size"
    /// shape as <see cref="CameraSway"/> above, defaulting to null so every EXISTING implementer keeps compiling unmodified.
    /// <see cref="AlundraWorldProxy"/> returns its own stage.
    /// </summary>
    AlundraEffectStage? EffectStage => null;

    /// <summary>
    /// This session's dialogue-flow seam (E12.a, docs/plan-e12-dialogues.md) - backs opcodes
    /// 0x0D/0x39/0x44/0x50/0x51/0x5C in <see cref="EventProgramRunner.Dispatch"/>. A default
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

    /// <summary>
    /// E16.e L4 (docs/plan-e16-etat-partie.md): the current world's name, whose "-{id}" suffix is the current map
    /// (<see cref="AlundraSaveGame.TryCaptureFromWorld"/>, F4) - what the save book's state 5 captures the save
    /// with, the same source the recipe keys use (<see cref="AlundraSaveGameDirector"/>). A default interface
    /// member, same "degraded" shape as the seams above: null means "no world name", and a capture then refuses.
    /// </summary>
    string? WorldName => null;
}
