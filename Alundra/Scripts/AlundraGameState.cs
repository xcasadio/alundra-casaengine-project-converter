#nullable enable
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
    /// <c>ControlLocked</c> bridge) - the full engine bridge onto
    /// <c>PlayerInput.IsInputEnable</c>/<c>CharacterControlMode</c> stays E6's own scope.</summary>
    public uint PlayerControlFlags;

    /// <summary>Persistent save-game flags (<c>g_saveData.GameFlags</c>) - all zero, matching New Game.</summary>
    public readonly uint[] GameFlags = new uint[WordCount];

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
}
