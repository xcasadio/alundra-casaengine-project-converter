#nullable enable
using System;

namespace Alundra.Scripts;

/// <summary>
/// V1 sliver of the original's game-flag storage, just enough for the event-program interpreter's
/// flag opcodes (0x05 FlagOn, 0x06 FlagOff, 0x31 IfFlagOff and siblings). Ported from
/// <c>GameEngine.GetFlag</c>/<c>AddFlag</c>/<c>SetFlag</c>/<c>XorFlag</c> (GameEngine.cs:2828-2926) and
/// <c>GameInitializer.ResetGameFlags</c> @ 0x800814a0 (GameInitializer.cs:483-494), which zeroes every
/// flag word for a New Game - this class starts zeroed the same way, so it needs no explicit reset.
///
/// The original selects between two flag banks by the flag id's 0x8000 bit: below it, the persistent
/// save-game flags (<c>g_saveData.GameFlags</c>); at/above it, session-only <c>g_temporaryFlags</c>.
/// Both are indexed the same way, <c>(flag &gt;&gt; 5) &amp; 0x3ff</c> (the original computes this as
/// <c>((flag &gt;&gt; 3) &amp; 0xffc) &gt;&gt; 2</c>, an equivalent formulation left as a comment on the
/// original's own GetFlag), giving up to 1024 32-bit words per bank - both arrays here are sized to
/// that upper bound. This is the full extent of E3's InitializeGameState port pulled forward; anything
/// else that method sets up (items, HP, map index tables, ...) is out of scope for this interpreter.
/// </summary>
public sealed class AlundraGameState
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares (D-T-3,
    /// docs/plan-transitions-carte.md) - same shape as <see cref="AlundraMusicPlayer.Instance"/>/
    /// <see cref="AlundraScreenFadeDirector.Instance"/>/<see cref="AlundraDialogueDirector.Instance"/>.
    /// Unlike those three, this class KEEPS its public parameterless constructor (two test harnesses -
    /// <see cref="AlundraEventProgramRunnerTests"/> and <see cref="IntroTraceHarnessTests"/> among others -
    /// build their own private instance directly, never through this session carrier) - so <see cref="Instance"/>
    /// is just one more instance, reachable through the same public constructor, that <see cref="AlundraWorldProxy.GameState"/>
    /// happens to always resolve to.</summary>
    public static readonly AlundraGameState Instance = new();

    private const int WordCount = 1024;
    private const uint TemporaryFlagBit = 0x8000;

    // New Game constants - port of the New Game branch of GameInitializer.InitializeGameState
    // (GameInitializer.cs:331-436, the SlotData==0 branch only; SlotData==1 "load save" and the
    // SlotData==else "debug" branch are out of scope). Only the fields E1 needs (hero spawn map/tile,
    // reset animation/direction) are ported here - items/HP/MP/money/weapon (InitializePlayerStatsAndItems,
    // the do/while item-unlock loop, SetPlayerWeaponId) are E2's own scope (a real PlayerManager).
    /// <summary>GameInitializer.cs:363 - <c>g_saveData.InitialMapId = 389</c> (Ship Klark, beginning).</summary>
    public const uint InitialMapId = 389;

    /// <summary>GameInitializer.cs:364 - <c>g_saveData.CameraTileX = 33</c>.</summary>
    public const int CameraTileX = 33;

    /// <summary>GameInitializer.cs:365 - <c>g_saveData.CameraTileY = 59</c>.</summary>
    public const int CameraTileY = 59;

    /// <summary>GameInitializer.cs:366 - <c>g_saveData.CameraTileZ = 0</c>.</summary>
    public const int CameraTileZ = 0;

    /// <summary>GameInitializer.cs:414 - <c>g_resetAnimationId = 0x36</c> (set unconditionally, after the
    /// SlotData branch, for every New Game/Load alike).</summary>
    public const uint ResetAnimationId = 0x36;

    /// <summary>GameInitializer.cs:367,414 - <c>g_resetDirectionId = 0</c> (set both inside the New Game
    /// branch and again unconditionally afterward - same value either way).</summary>
    public const uint ResetDirectionId = 0;

    /// <summary>
    /// Port of <see cref="AlundraEngine.PlayerControlFlags"/> (alundra-datas-analyser
    /// AlundraTools/AlundraEngine/PlayerControlFlags.cs) - named bits of <see cref="PlayerControlFlags"/>
    /// below. Kept as a nested static class (rather than a separate file) since this V1 sliver has no
    /// other consumer yet - see that class's own doc for the Ghidra-verified meaning of each bit.
    /// </summary>
    public static class PlayerControlBits
    {
        /// <summary>Bit 2 - script-driven player lock (event opcode 0x10 sets it, 0x11 clears it).</summary>
        public const uint ControlLocked = 0x04;

        /// <summary>Bit 3 - a full-screen UI owns the game (inventory, memory card, debug menu).</summary>
        public const uint MenuOpen = 0x08;

        /// <summary>Bit 4 - "message with background" box in its keep-control variant.</summary>
        public const uint MessageBox = 0x10;

        /// <summary>Bit 5 - forced sequence (warp departure, sand-cape ride, boss choreography).</summary>
        public const uint ForcedSequence = 0x20;

        /// <summary>Bit 6 - dead bit in the retail binary (see the original's own doc); only appears
        /// inside <see cref="GameplayBlockedMask"/>.</summary>
        public const uint Unused40 = 0x40;

        /// <summary>Bit 7 - scripted weapon lock (event opcodes 0xC0/0xC1).</summary>
        public const uint ForcedWeapon = 0x80;

        /// <summary>Mask 0x48 - map events and world updates pause while any of these bits is set
        /// (<c>RunMapEvents</c>, GameEngine.cs:1667-1671; <c>UpdateEntities</c>, EntityManager.cs:367-395).</summary>
        public const uint GameplayBlockedMask = MenuOpen | Unused40;

        /// <summary>Mask 0x34 - normal player input processing is skipped while any of these bits is set
        /// (<c>PlayerManager.MovePlayer</c>, PlayerManager.cs:38).</summary>
        public const uint InputBlockedMask = ControlLocked | MessageBox | ForcedSequence;
    }

    /// <summary>Port of <c>StaticVariables.g_playerControlFlags</c> - zero at New Game (see
    /// docs/intro-roadmap.md §1.4: nothing explicitly zeroes it at boot, it is simply BSS-zero; E1's own
    /// port starts every world the same way, since only a New Game flow is covered so far). Read by
    /// <see cref="AlundraWorldProxy.RunMapEventsPass"/>'s <see cref="PlayerControlBits.GameplayBlockedMask"/>
    /// gate and by <see cref="AlundraPlayerManager.MovePlayer"/>'s <see cref="PlayerControlBits.InputBlockedMask"/>
    /// gate; written by event opcodes 0x10/0x11 (E4.c, <see cref="AlundraEventProgramRunner"/>'s own
    /// <c>ControlLocked</c> bridge). E6 is closed (docs/plan-conversion-totale.md §4 E6) and its
    /// decision E6-1 DECLINES the engine bridge onto <c>PlayerInput.IsInputEnable</c>/
    /// <c>CharacterControlMode</c>: the original has no global input switch - it tests this mask at each
    /// consumer site - and cutting input upstream would silently defeat
    /// <see cref="AlundraPlayerManager.DebugIgnoreControlLockEnvVar"/>, since
    /// <c>AlundraPlayerController.BuildPadState</c> already reads through <c>PlayerInput</c>.</summary>
    public uint PlayerControlFlags;

    /// <summary>
    /// D-E7-8 seam (docs/plan-e7-mutation-tuiles.md, slice E7.c): last pad snapshot published by
    /// <see cref="AlundraEntityScriptProxy.Update"/>'s own player branch, just before it calls
    /// <see cref="AlundraPlayerManager.MovePlayer"/> - the fidelity pendant of the original's global
    /// <c>g_padState1</c> (read by <c>Script_47_02F</c>/<see cref="AlundraEventProgramRunner"/>'s own case
    /// 0x2F). Chosen over widening <see cref="IEntityWorldContext"/> or the runner's own constructor:
    /// this class is the one seam <see cref="AlundraEntityScriptProxy"/> (the writer) and
    /// <see cref="AlundraEventProgramRunner"/> (the reader) already BOTH hold a reference to, so no other
    /// signature needs to change. Defaults to an all-zero <see cref="AlundraPadState"/> (nothing held) -
    /// the same safe no-op a 0x2F dispatched before this world's first player Update would see the
    /// original's own zero-initialized pad global read as.
    /// </summary>
    public AlundraPadState LastPadState;

    /// <summary>E13.d D1 (docs/plan-e13d-inventaire.md, D-E13D-9): the same pad advanced once per LOGIC
    /// tick, with the original's press edges and key repeat - see <see cref="AlundraTickPad"/>'s own doc.
    /// Fed from <see cref="LastPadState"/> by <see cref="AlundraWorldProxy.Update"/>'s own per-tick pad
    /// pass; read by the per-tick consumers that need the original's frame-exact edges (the inventory).</summary>
    public readonly AlundraTickPad TickPad = new();

    /// <summary>
    /// T3 (docs/plan-transitions-carte.md, point 5): port of the original's <c>g_isWarpDisabled</c>
    /// global, tested at the head of <c>PlayerManager.HandleWarpTransition</c> (T4's own scope) and set
    /// by event opcodes <c>0x9B</c>/<c>0x9C</c> (T7's own scope, out of this slice). T3's own
    /// <see cref="AlundraPortalTrigger.TryGetTrigger"/> already respects it (folded into the predicate
    /// itself - see that method's own doc on why), so the flag is wired here BEFORE either opcode exists.
    /// Zero at construction (New Game default, same rationale as <see cref="PlayerControlFlags"/> above),
    /// and reset to <c>false</c> at every map entry (<see cref="InstallForMapEntry"/>) - the original does
    /// the same in <c>InitializeEntitySlots</c> (<c>GameEngine.cs</c>, New Game/map-load preamble).
    /// </summary>
    public bool IsWarpDisabled;

    /// <summary>
    /// E12.d (D-E12D-3): the INTERACT LATCH - port of the original's <c>g_lastValidWarp*</c> globals
    /// (decompiler-artifact names; PlayerManager.cs:1605-1643 shows they are the interaction memory:
    /// while the player stands still against an InteractRequiresButton entity, contact may read null,
    /// and these eight stored values let the button still find it). Lives here because this object has
    /// the original globals' scope (the E11.c lesson: guard state lives where the original's lives),
    /// and the eight equality checks self-invalidate any stale value - including a cross-map
    /// coincidence, exactly like the original. Only <c>AlundraPlayerManager.CheckEntityInteraction</c>
    /// reads or writes these.
    /// </summary>
    public AlundraEntityScriptProxy? InteractLatchEntity;
    public int InteractLatchFacing;       // g_lastWarpFacing  <- entity.Index2
    public int InteractLatchEntityX;      // g_lastWarpTargetX/Y/Z <- entity.Pos*
    public int InteractLatchEntityY;
    public int InteractLatchEntityZ;
    public int InteractLatchPlayerX;      // g_lastWarpCamX/Y/Z <- player.Pos*
    public int InteractLatchPlayerY;
    public int InteractLatchPlayerZ;
    public uint InteractLatchDirection;   // g_lastWarpDirection <- player.TargetDirection

    /// <summary>
    /// E13 C0 (docs/plan-e13-hud.md §1.5 bis): port of the original's <c>g_playerStats</c>/
    /// <c>g_saveData.PlayerStats</c> - a SINGLE aliased object in the original (GameInitializer.cs:444-445),
    /// so a single field here, never a separate "save" and "current" pair. See
    /// <see cref="AlundraPlayerStats"/>'s own doc for why the object lives here and its setters live on
    /// <see cref="AlundraPlayerManager"/>.
    /// </summary>
    public readonly AlundraPlayerStats PlayerStats = new();

    /// <summary>
    /// E13.c S3 (docs/plan-e13c-icones-hud.md): port of <c>g_saveData.NumberOfItems</c>
    /// (SaveData.cs:20, <c>short[256]</c>) - how many of each item the player owns, read and written at
    /// <c>[itemId * 2 + 1]</c> everywhere (PlayerManager.cs:4342, :4388, :4677-4683); the even slot of each
    /// pair is only ever zeroed. "Owned" means a count above zero - there is no separate bit. All zero at
    /// construction, like the original after <c>InitializePlayerStatsAndItems</c> (GameInitializer.cs:469-479);
    /// the New Game then fills it through <see cref="AlundraPlayerManager.InitializeNewGameInventory"/>.
    /// </summary>
    public readonly short[] NumberOfItems = new short[256];

    /// <summary>
    /// E13.c S3: once-only session latch for <see cref="AlundraPlayerManager.InitializeNewGameInventory"/>,
    /// run by <see cref="AlundraWorldProxy.AdoptPlayerPawn"/> at the map entry it recognizes as a New Game.
    /// A belt-and-suspenders latch: the real gate is "no pending warp arrival", which a session satisfies
    /// once; this latch keeps the unlock loop from running twice even if something ever produced a second
    /// such entry. The inventory is the game's own New Game state, so it is not gated on any debug switch.
    /// </summary>
    public bool NewGameInventoryInitialized;

    /// <summary>Persistent save-game flags (<c>g_saveData.GameFlags</c>) - all zero, matching New Game.</summary>
    public readonly uint[] GameFlags = new uint[WordCount];

    /// <summary>
    /// E15.c T3 (docs/plan-e15-yarn.md §5.7, T2): port of <c>g_textCategoryIndex</c> @ 0x80149CD8. The
    /// original places this in the BSS, zeroed once at boot by the entry point (0x8008b538) and never
    /// again - not by New Game, not by loading a save, since it is NOT part of <c>g_saveData</c>
    /// (0x801EB2E8-0x801EBA40). It is written only by <see cref="AlundraTextProgress.UpdatePlayerProgressState"/>
    /// and read only by the <c>\X2</c>/<c>\X3</c>/<c>\X4</c>/<c>\X5</c> text codes (via
    /// <see cref="AlundraTextProgress"/>'s functions), so this field is process-lifetime state like the
    /// rest of this class' construction-time defaults - PRODUCTION never resets it (no New Game reset
    /// path touches it in the binary either); <see cref="ResetForTests"/> resets it anyway, because that
    /// method is a TEST-ONLY seam whose job is to leave <see cref="Instance"/> clean between tests that
    /// mutate it through the public API, not to reproduce the binary's own (lack of) reset.
    /// </summary>
    public int TextCategoryIndex;

    /// <summary>
    /// E15.c T3 (docs/plan-e15-yarn.md §5.7, T2): port of <c>INT_ARRAY_80191908</c>, 4 ints in the BSS,
    /// zero at boot and never reset - also outside <c>g_saveData</c>. Written only by the two mini-game
    /// programs the decompilation names (<c>AI_FUN_80064294</c>, <c>AI_FUN_80064d90</c>), neither of
    /// which is ported yet, so nothing in this DLL writes it; read only by the <c>\V&lt;n&gt;</c> text
    /// code (<c>game_var(n)</c>, docs/plan-e15-yarn.md §1/§5.7), unchecked bounds like the original
    /// (<c>c - '0'</c>). PRODUCTION never resets this array either; like <see cref="TextCategoryIndex"/>
    /// above, only the TEST-ONLY <see cref="ResetForTests"/> seam clears it.
    /// </summary>
    public readonly int[] GameVariables = new int[4];

    /// <summary>Session-only flags (<c>g_temporaryFlags</c>) - all zero at construction.</summary>
    public readonly uint[] TemporaryFlags = new uint[WordCount];

    /// <summary>
    /// Port of <c>g_saveData.MapIdToInternalMapIndexTable</c> (<c>SaveData.cs:18</c>, <c>ushort[500]</c>) -
    /// written by event opcode 0x38 (<c>Script_SetSaveMapIdToInternalMapIndex_038</c>,
    /// EntityEventHandlers.cs:1202-1207) and read by portal travel (<c>PlayerManager.cs:3497</c>, out of
    /// this DLL's own scope). <c>GameInitializer.ResetGameFlags</c> (GameInitializer.cs:490-493) seeds it
    /// to the identity mapping (<c>table[i] = i</c>) for every New Game - reproduced here at construction
    /// so this state starts New-Game-equivalent without needing a separate reset call, same rationale as
    /// <see cref="GameFlags"/>/<see cref="TemporaryFlags"/> starting zeroed.
    /// </summary>
    public readonly ushort[] MapIdToInternalMapIndexTable = CreateIdentityMapIndexTable();

    /// <summary>E16.c C8 (docs/plan-e16-etat-partie.md, D-E16-23): game-time units per real second. The
    /// original's end-of-frame function (<c>0x80042798</c>) adds one unit per displayed frame, and
    /// <c>UpdateMenuStatusText</c> (<c>0x800311D4</c>) shows the counter in sixtieths of a second (§2, Q3);
    /// the author settled the port on 60 units per real second.</summary>
    public const int GameTimeUnitsPerSecond = 60;

    /// <summary>E16.c C8: the counter's ceiling, <c>0x14996C4</c> = 99:59:59 in sixtieths of a second -
    /// the cap of the original's end-of-frame increment (<c>0x80042834</c>, <c>GameEngine.cs:1471-1474</c>).</summary>
    public const uint GameTimeMax = 0x14996C4;

    /// <summary>
    /// E16.c C1/C8 (docs/plan-e16-etat-partie.md, D-E16-23): port of <c>g_gameplayTime</c> @ 0x8013FB4C,
    /// saved as <c>g_saveData.GameTime</c> (<c>+0x048</c>) by <c>UpdateSavedData</c>. Counts sixtieths of a
    /// second, capped at <see cref="GameTimeMax"/>; advanced only by <see cref="AdvanceGameTime"/>. Part of
    /// <c>g_saveData</c>, so <see cref="InstallForMapEntry"/> keeps it (F9: this DLL had no game time before).
    /// </summary>
    public uint GameTime;

    /// <summary>
    /// E16.c C1 (docs/plan-e16-etat-partie.md, D-E16-22): port of <c>g_saveData.SaveSlotIndex</c>
    /// (<c>+0x756</c>, one byte), renamed after what it counts: the retries after death. The original
    /// increments it only in <c>InitializeMapWarpPosition</c> (<c>0x800315B0</c>, capped at <c>0xFF</c>),
    /// zeroes it at New Game (<c>0x80031860</c>), and reads it in opcode <c>0xC2</c> (<c>0x80041D34</c>) and in
    /// <c>Script_187_0BB</c>'s "Retry =" debug log (§2, Q4). Nothing increments it before E18 ("Retry" is not
    /// ported); it is saved and restored. Kept by <see cref="InstallForMapEntry"/>, like the rest of
    /// <c>g_saveData</c>.
    /// </summary>
    public byte DeathRetryCount;

    /// <summary>E16.c C8: the fraction of a game-time unit <see cref="AdvanceGameTime"/> has not yet passed
    /// to <see cref="GameTime"/>. Not saved; zeroed by <see cref="ResetGameTimeFraction"/>.</summary>
    private double _gameTimeFraction;

    /// <summary>
    /// E16.c C8 (docs/plan-e16-etat-partie.md, D-E16-23): advances <see cref="GameTime"/> by
    /// <paramref name="elapsedSeconds"/> real seconds, at <see cref="GameTimeUnitsPerSecond"/> units per second.
    /// Called once per frame at the head of <see cref="AlundraWorldProxy.Update"/>. The DLL's logic clock runs
    /// at 50 Hz (F9, <see cref="AlundraLogicClock"/>), so the units are counted on elapsed time, not on ticks.
    /// A non-finite, zero or negative duration adds nothing. The whole units are passed on and the counter is
    /// capped at <see cref="GameTimeMax"/> in <see cref="double"/>, BEFORE the conversion to <see cref="uint"/>,
    /// so a huge finite duration reaches the cap instead of wrapping (SC11).
    /// </summary>
    public void AdvanceGameTime(float elapsedSeconds)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds <= 0f)
        {
            return;
        }

        _gameTimeFraction += elapsedSeconds * (double)GameTimeUnitsPerSecond;
        var wholeUnits = Math.Floor(_gameTimeFraction);
        _gameTimeFraction -= wholeUnits;

        var total = GameTime + wholeUnits;
        GameTime = total >= GameTimeMax ? GameTimeMax : (uint)total;
    }

    /// <summary>E16.c C6/C8: drops the unsaved fraction of a game-time unit - used when a save's
    /// <see cref="GameTime"/> is applied (<c>AlundraSaveGame.ApplyTo</c>, E16.c C6) and by <see cref="ResetForTests"/>.</summary>
    internal void ResetGameTimeFraction()
    {
        _gameTimeFraction = 0d;
    }

    private static ushort[] CreateIdentityMapIndexTable()
    {
        var table = new ushort[500];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (ushort)i;
        }

        return table;
    }

    private static int IndexOf(uint flag) => (int)((flag >> 5) & 0x3ff);

    private uint[] BankFor(uint flag) => (flag & TemporaryFlagBit) == 0 ? GameFlags : TemporaryFlags;

    /// <summary>GameEngine.GetFlag (GameEngine.cs:2828-2845).</summary>
    public uint GetFlag(uint flag) => BankFor(flag)[IndexOf(flag)];

    /// <summary>GameEngine.AddFlag (GameEngine.cs:2871-2888) - ORs <paramref name="mask"/> in.</summary>
    public void AddFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] |= mask;

    /// <summary>GameEngine.SetFlag (GameEngine.cs:2890-2907) - ANDs <paramref name="mask"/> in (the
    /// caller passes the complement of the bit it wants cleared, e.g. Script_6_006's <c>~mask</c>).</summary>
    public void SetFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] &= mask;

    /// <summary>GameEngine.XorFlag (GameEngine.cs:2909-2926).</summary>
    public void XorFlag(uint flag, uint mask) => BankFor(flag)[IndexOf(flag)] ^= mask;

    /// <summary>
    /// D-T-3/D-T-13 (docs/plan-transitions-carte.md, slice T1): applies this session carrier's own
    /// map-entry disposition, called from <see cref="AlundraWorldProxy.InitializeWithWorld"/> at the
    /// START of that method's own installation block (before <see cref="AlundraWorldProxy.InstallCellAndOverlaySystems"/>,
    /// so before <see cref="AlundraWorldProxy.InstallDialogueSystems"/> goes on to clean up the
    /// MessageBox/MenuOpen bits of <see cref="PlayerControlFlags"/> below). The table is EXHAUSTIVE
    /// (D-T-13) - only the two rows below move; every other field survives untouched across the map
    /// change, exactly like the original's own globals:
    /// <list type="bullet">
    /// <item><description><see cref="GameFlags"/>, <see cref="MapIdToInternalMapIndexTable"/>,
    /// <see cref="PlayerControlFlags"/>, <see cref="LastPadState"/> and the eight numeric interact-latch
    /// fields all stay CONSERVED - nothing to do here for them.</description></item>
    /// <item><description>E16.c C8: <see cref="GameTime"/> (with its unsaved fraction) and
    /// <see cref="DeathRetryCount"/> stay CONSERVED too - both are in <c>g_saveData</c>, which no map entry
    /// of the original touches.</description></item>
    /// </list>
    /// </summary>
    public void InstallForMapEntry()
    {
        // D-T-13: TemporaryFlags is VIDÉ - port of ClearTemporaryFlags (GameEngine.cs:429-438), the
        // original's own map-entry preamble for the session-only flag bank.
        Array.Clear(TemporaryFlags);

        // D-T-13: InteractLatchEntity is VIDÉ - a strong reference into the world just destroyed, so
        // keeping it would retain that whole dead world graph and could run an interaction against an
        // entity from a torn-down map. The original stores this in a slot table InitializeEntitySlots
        // re-initializes at every map entry - clearing here is the faithful equivalent. The eight numeric
        // latch fields (InteractLatchFacing/.../InteractLatchDirection) stay CONSERVED - they
        // self-invalidate via their own eight equality checks, exactly like the original.
        InteractLatchEntity = null;

        // T3 (docs/plan-transitions-carte.md, point 5): IsWarpDisabled is reset here - port of the
        // original's own InitializeEntitySlots preamble, which zeroes g_isWarpDisabled at every map
        // entry. NOTE for the main session: this row is not yet reflected in D-T-13's own exhaustive
        // table (that table predates this field) - it needs one.
        IsWarpDisabled = false;
    }

    /// <summary>
    /// E16.d K8, step 1 (docs/plan-e16-etat-partie.md, D-E16-19, SC7): the session half of loading a save -
    /// run by <c>AlundraSaveGameDirector.ApplyPendingLoad</c> on the arrival map, right after
    /// <see cref="InstallForMapEntry"/> and BEFORE <see cref="AlundraSaveGame.ApplyTo"/>. It puts back what a
    /// fresh process would hold for the fields the save does NOT carry, so nothing of the session in progress
    /// leaks into the loaded game - the original only loads from a fresh process (<c>LOADER.EXE</c>) or after
    /// death ("Retry"), never from the middle of a session (§0.2):
    /// <list type="bullet">
    /// <item><description><see cref="PlayerControlFlags"/> 0, <see cref="LastPadState"/> default,
    /// <see cref="TickPad"/> reset;</description></item>
    /// <item><description>the interact latch, its entity and its eight numbers;</description></item>
    /// <item><description><see cref="IsWarpDisabled"/> false;</description></item>
    /// <item><description><see cref="TextCategoryIndex"/> 0 and <see cref="GameVariables"/> zeroed (D-E16-19:
    /// both are BSS outside <c>g_saveData</c>, zero in the fresh process the original loads from);</description></item>
    /// <item><description><see cref="NewGameInventoryInitialized"/> TRUE, unlike a New Game (SC7): a later map
    /// entry without a warp arrival must not run the New Game inventory over the loaded items
    /// (<see cref="AlundraWorldProxy.AdoptPlayerPawn"/>).</description></item>
    /// </list>
    /// Nothing the save carries is touched here (flags, map table, item counters, stats, game time, death
    /// retries): <see cref="AlundraSaveGame.ApplyTo"/> writes them next. <see cref="TemporaryFlags"/> is already
    /// cleared by <see cref="InstallForMapEntry"/>.
    /// </summary>
    internal void ResetSessionForLoad()
    {
        PlayerControlFlags = 0;
        LastPadState = default;
        TickPad.Reset();

        InteractLatchEntity = null;
        InteractLatchFacing = 0;
        InteractLatchEntityX = 0;
        InteractLatchEntityY = 0;
        InteractLatchEntityZ = 0;
        InteractLatchPlayerX = 0;
        InteractLatchPlayerY = 0;
        InteractLatchPlayerZ = 0;
        InteractLatchDirection = 0;

        IsWarpDisabled = false;

        TextCategoryIndex = 0;
        Array.Clear(GameVariables);

        NewGameInventoryInitialized = true;
    }

    /// <summary>Test-only: restores this session carrier to its New-Game-equivalent construction state,
    /// so tests do not leak state into each other through <see cref="Instance"/> - same seam as
    /// <see cref="AlundraMusicPlayer.ResetForTests"/>/<see cref="AlundraScreenFadeDirector.ResetForTests"/>.</summary>
    internal void ResetForTests()
    {
        Array.Clear(GameFlags);
        Array.Clear(TemporaryFlags);

        for (var i = 0; i < MapIdToInternalMapIndexTable.Length; i++)
        {
            MapIdToInternalMapIndexTable[i] = (ushort)i;
        }

        PlayerControlFlags = 0;
        LastPadState = default;
        TickPad.Reset();

        InteractLatchEntity = null;
        InteractLatchFacing = 0;
        InteractLatchEntityX = 0;
        InteractLatchEntityY = 0;
        InteractLatchEntityZ = 0;
        InteractLatchPlayerX = 0;
        InteractLatchPlayerY = 0;
        InteractLatchPlayerZ = 0;
        InteractLatchDirection = 0;

        // T3 (docs/plan-transitions-carte.md, point 5) - see InstallForMapEntry's own comment above.
        IsWarpDisabled = false;

        // E13 C0: PlayerStats is a session-scoped object like everything else above - reset it too so
        // tests do not leak stat values into each other through Instance.
        PlayerStats.ResetForTests();

        // E13.c S3: the item counters and their once-only latch are session state too.
        Array.Clear(NumberOfItems);
        NewGameInventoryInitialized = false;

        // E15.c T3: TextCategoryIndex/GameVariables are BSS in the original, never reset by any
        // production code path (see their own field docs) - but this method is a TEST-ONLY seam meant
        // to reset everything a test can mutate through Instance, so tests do not leak state into each
        // other through it. Resetting them here is this seam's own job, not a reproduction of the
        // binary's (lack of) reset.
        TextCategoryIndex = 0;
        Array.Clear(GameVariables);

        // E16.c C8: the game time, its unsaved fraction and the death-retry counter are session state.
        GameTime = 0;
        DeathRetryCount = 0;
        ResetGameTimeFraction();
    }
}
