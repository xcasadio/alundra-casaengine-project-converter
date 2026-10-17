#nullable enable
using System;
using System.Collections.Generic;
using CasaEngine.Framework.Scene.Entities;
using Alundra.Session;
using Alundra.Scripts;
using Alundra.Combat;
using Alundra.Physics;

namespace Alundra.Hero;

/// <summary>
/// E20.f2 (docs/plan-e20-heros.md section 1.10): <c>CreatePlayerAnimationEffects(mode)</c> (<c>0x8002FB14</c>), entered as a skeleton at its two call sites of <c>MovePlayer</c>:
/// mode 0 at <c>0x80031C3C</c> (after the gravity flag <c>0x80031C38</c>, before the entry of the damage <c>0x80031C44</c>) and mode 1 at <c>0x80031BDC</c> (the locked branch). The function is three dispatches on the
/// state chosen at the previous tick (<c>+0x88</c>, <see cref="AlundraEntityScriptProxy.TargetAnimationId"/>) and a tail:
/// <list type="number">
/// <item><description>The first dispatch, on <c>state - 4</c> (table <c>0x800228AC</c>): the run (<c>0x29</c>, <c>0x8002FB6C</c>) and the attack states (<c>0x8002FDBC</c>, the call <c>0x8003A7B0</c> at <c>0x8002FDC0</c>, the grass cut).</description></item>
/// <item><description>The second, on <c>state - 1</c> (table <c>0x800229CC</c>), only while the hero has a force on X or Y (<c>0x8002FDCC-0x8002FDE8</c>): the steps and the walk cycle (<c>0x8002FE24-0x800301D4</c>).</description></item>
/// <item><description>The third, on <c>state - 2</c> (table <c>0x80022A74</c>, <c>0x800301D8-0x800303D0</c>): the landing (<c>0x8003020C</c>), and the latch <c>0x800A8F30</c> that the other states clear (<c>0x800303D0</c>).</description></item>
/// <item><description>The end (<c>0x800303D8</c>), mode 0 only (<c>bnez $s5</c>): the alarm of the low life (<c>0x800303DC-0x80030430</c>), then the flail bounce (<c>0x80030434-0x80030654</c>).</description></item>
/// </list>
/// <b>Ported for real</b>: the dispatch (which part runs for which state, with the guards of the landing) and the flail bounce. <b>Named stubs</b> (empty bodies, each records its call for the tests; the alarm records its call too but is no stub): the steps and
/// the walk cycle (<see cref="PlayerRunStepEffects"/>, <see cref="PlayerWalkStepEffects"/>, whose own phase gates on <c>+0x1C</c> and the slope tables are inside the stub), the landing sound and dust
/// (<see cref="PlayerLandingEffects"/>) and the grass cut (<see cref="PlayerGrassCut"/>), all four for E20.e; the alarm of the low life (<see cref="PlayerLowHpAlarm"/>) is ported for real by E20.c2a T4, with the life of the hero entity.
/// </summary>
internal static class AlundraPlayerAnimationEffects
{
    /// <summary>The mode of the free branch (<c>0x80031C3C</c>): the whole function, alarm and flail bounce included.</summary>
    internal const int FreeMode = 0;

    /// <summary>The mode of the locked branch (<c>0x80031BDC</c>): the dispatches only, the function ends at <c>0x800303D8</c>.</summary>
    internal const int LockedMode = 1;

    internal const string RunStepStubName = nameof(PlayerRunStepEffects);
    internal const string WalkStepStubName = nameof(PlayerWalkStepEffects);
    internal const string LandingStubName = nameof(PlayerLandingEffects);
    internal const string GrassCutStubName = nameof(PlayerGrassCut);
    internal const string LowHpAlarmStubName = nameof(PlayerLowHpAlarm);

    /// <summary>PlayerAnimation 0x29 (the run): the only state of the first dispatch that is not an attack (<c>0x8002FB6C</c>).</summary>
    private const uint RunStateId = 0x29;

    /// <summary>The result bytes of the flail bounce, <c>byte[0x800228A4 + 2 * index + (air ? 1 : 0)]</c>: iron on the ground, iron in the air, steel on the ground, steel in the air.</summary>
    private static readonly byte[] BounceStates = { 0x08, 0x1F, 0x25, 0x26 };

    /// <summary>The bit of a cell's walkability that makes a wall of it for the flail (<c>0x80030620</c>, <c>andi 0x40</c>).</summary>
    private const int WallWalkabilityBit = 0x40;

    /// <summary>Test seam: when non-null, every named stub appends (mode, stub name, state) to it when it is called. Per thread, so tests running in parallel do not see each other's calls.</summary>
    [ThreadStatic]
    internal static List<(int Mode, string Stub, uint State)>? StubCallsForTests;

    /// <summary>
    /// <c>CreatePlayerAnimationEffects(mode)</c>. <paramref name="mode"/> is <see cref="FreeMode"/> or <see cref="LockedMode"/>.
    /// </summary>
    internal static void Create(AlundraEntityScriptProxy player, int mode)
    {
        var state = player.TargetAnimationId;

        // 0x8002FB14-0x8002FDC8: the first dispatch. The states that are in neither list fall to 0x8002FDC8.
        if (state == RunStateId)
        {
            PlayerRunStepEffects(player, mode);
        }
        else if (IsGrassCutState(state))
        {
            PlayerGrassCut(player, mode);
        }

        // 0x8002FDCC-0x8002FDE8: the second dispatch is skipped (0x800301D8) when both horizontal forces are zero.
        if ((player.ForceX != 0 || player.ForceY != 0) && IsWalkStepState(state))
        {
            PlayerWalkStepEffects(player, mode);
        }

        // 0x800301D8-0x800303D0: the third dispatch.
        if (IsLandingState(state))
        {
            // 0x8003020C-0x80030244: mode 0 goes straight on; the locked mode waits for the latch to be clear (0x80030214-0x8003021C). Then the hero must be on the ground (+0x144) and not rising (+0xB8 <= 0).
            if ((mode == FreeMode || player.LandingLatch == 0) && player.IsOnGround != 0 && player.ForceZ <= 0)
            {
                PlayerLandingEffects(player, mode);
                player.LandingLatch = 1; // 0x800303C8 (the three paths of the landing end here).
            }
        }
        else
        {
            player.LandingLatch = 0; // 0x800303D0: every other state, and a state outside the table (0x800301E8), clears it.
        }

        // 0x800303D8: the end of the locked mode.
        if (mode != FreeMode)
        {
            return;
        }

        PlayerLowHpAlarm(player);
        BounceFlail(player);
    }

    /// <summary>
    /// The flail bounce, <c>0x80030434-0x80030654</c> (mode 0 only): for the state <c>0x12</c> or <c>0x16</c> (the iron flail, index 0) or <c>0x40</c> or <c>0x42</c> (the steel flail, index 1), a hit registered by the
    /// tick before (<see cref="AlundraEntityScriptProxy.HitCounter"/>), or, with a frame box, a wall under one of the four corners of the stored hit box, gives the state <c>byte[0x800228A4 + 2 * index + (air ? 1 : 0)]</c>.
    /// No force is added.
    /// </summary>
    private static void BounceFlail(AlundraEntityScriptProxy player)
    {
        int index;
        switch (player.TargetAnimationId)
        {
            case 0x12:
            case 0x16:
                index = 0;
                break;
            case 0x40:
            case 0x42:
                index = 1;
                break;
            default:
                return;
        }

        // 0x80030488-0x80030494 and 0x8003049C-0x800304D8: a hit chooses the state at once; +0x144 == 0 (in the air) takes the second byte of the pair.
        if (player.HitCounter != 0)
        {
            player.TargetAnimationId = BounceStates[2 * index + (player.IsOnGround == 0 ? 1 : 0)];
            return;
        }

        // 0x800304F4-0x800304FC: without a box for the frame shown (+0x1D4), nothing more.
        if (!player.HasFrameBox || !HasWallUnderACorner(player))
        {
            return;
        }

        // 0x8003062C-0x80030644.
        player.TargetAnimationId = BounceStates[2 * index + (player.IsOnGround == 0 ? 1 : 0)];
    }

    /// <summary>
    /// The four corners (<c>0x80030528-0x80030588</c>, then the loop <c>0x8003058C-0x80030650</c> in the order (X, Y), (X + width, Y), (X, Y + depth), (X + width, Y + depth)) of the stored hit box
    /// (<c>+0x1FC</c>, <c>+0x200</c>, size <c>+0x214</c>, <c>+0x218</c>): the first one on a wall decides. Z is the stored hit box's.
    /// </summary>
    private static bool HasWallUnderACorner(AlundraEntityScriptProxy player)
    {
        if (player.ReadCollisionField() is not AlundraCellsCollisionField cells)
        {
            return false;
        }

        int columnA, columnB, rowA, rowB;
        unchecked
        {
            columnA = BinaryColumn(player.HitBoxX >> 16);
            columnB = BinaryColumn((player.HitBoxX + player.CollisionWidth) >> 16);
            rowA = BinaryRow(player.HitBoxY >> 20);
            rowB = BinaryRow((player.HitBoxY + player.CollisionDepth) >> 20);
        }

        return IsWall(cells, columnA, rowA, player.HitBoxZ)
            || IsWall(cells, columnB, rowA, player.HitBoxZ)
            || IsWall(cells, columnA, rowB, player.HitBoxZ)
            || IsWall(cells, columnB, rowB, player.HitBoxZ);
    }

    /// <summary>
    /// <c>0x80030600-0x80030624</c>: a cell is a wall when the box is below its height (<c>HitBoxZ &lt; Height &lt;&lt; 20</c>, signed) or when its walkability has the bit <c>0x40</c>. The DLL's Z is the binary's minus 1
    /// (ADR-0026): the stored box gets one raw unit on this comparison only, as the opcode <c>0x95</c> does (<see cref="AlundraHitOpcodes"/>). A cell outside the field (a test field narrower than the 52 x 60 of
    /// every real map, <see cref="AlundraCellsCollisionField.TryReadBinaryCell"/>) is not a wall.
    /// </summary>
    private static bool IsWall(AlundraCellsCollisionField cells, int column, int row, int hitBoxZ)
    {
        if (!cells.TryReadBinaryCell(column, row, out var height, out var walkability))
        {
            return false;
        }

        return unchecked(hitBoxZ + 1) < (height << 20) || (walkability & WallWalkabilityBit) != 0;
    }

    /// <summary>
    /// The column of a pixel, <c>0x8003052C-0x800305B0</c>: the table at <c>0x80127168</c> (2 bytes per pixel, <c>pixel / 24</c>) read at the pixel, then clamped to 0 to <c>0x33</c>. A pixel before the table
    /// (negative) reads memory the binary does not define: it gives 0 here, the clamp's own floor.
    /// </summary>
    internal static int BinaryColumn(int pixel) => pixel <= 0 ? 0 : Math.Min(pixel / 24, 0x33);

    /// <summary>The row of a Y in 16.16 shifted by 20 (16 px per row), clamped to 0 to <c>0x3B</c> (<c>0x800305B4-0x800305DC</c>).</summary>
    internal static int BinaryRow(int shifted) => shifted <= 0 ? 0 : Math.Min(shifted, 0x3B);

    // -----------------------------------------------------------------------------------------
    // The dispatch tables, read from ALUN_CD.EXE (France): the states that lead to each part.
    // -----------------------------------------------------------------------------------------

    /// <summary>Table <c>0x800228AC</c> (state - 4), the entries that lead to <c>0x8002FDBC</c>.</summary>
    internal static bool IsGrassCutState(uint state) => state is 0x04 or 0x10 or 0x12 or 0x14 or 0x16 or 0x18 or 0x19 or 0x3F or 0x40 or 0x41 or 0x42 or 0x44 or 0x46 or 0x49 or 0x4B;

    /// <summary>Table <c>0x800229CC</c> (state - 1), the entries that lead to a step part: <c>0x8002FE24</c> (1, 7), <c>0x8002FE68</c> (4, 0x2A), <c>0x8002FE70</c> (3), <c>0x8002FFE4</c> (0x28), <c>0x80030074</c> (0x23) and <c>0x80030084</c> (0x24).</summary>
    internal static bool IsWalkStepState(uint state) => state is 0x01 or 0x07 or 0x04 or 0x2A or 0x03 or 0x28 or 0x23 or 0x24;

    /// <summary>Table <c>0x80022A74</c> (state - 2), the entries that lead to the landing <c>0x8003020C</c>.</summary>
    internal static bool IsLandingState(uint state) =>
        state is 0x02 or 0x06 or 0x0A or 0x0B or 0x14 or 0x15 or 0x16 or 0x17 or 0x1F or 0x26 or 0x2B or 0x2C or 0x2D or 0x2E or 0x2F or 0x30 or 0x41 or 0x42 or 0x46 or 0x4B;

    // -----------------------------------------------------------------------------------------
    // The named stubs.
    // -----------------------------------------------------------------------------------------

    /// <summary>Stub (E20.e) of the run's steps and dust, <c>0x8002FB6C-0x8002FDC4</c> (state <c>0x29</c>): the sound of the slope every 8 ticks (<c>+0x1C</c> and 7) and the dust effects. Not ported.</summary>
    private static void PlayerRunStepEffects(AlundraEntityScriptProxy player, int mode) => Record(mode, RunStepStubName, player);

    /// <summary>Stub (E20.e) of the steps and the walk cycle, <c>0x8002FE24-0x800301D4</c> (a force on X or Y, the states of <see cref="IsWalkStepState"/>): the sound of the slope, the dust, their phase gates on <c>+0x1C</c>. Called at every tick that reaches it; the gates are inside. Not ported.</summary>
    private static void PlayerWalkStepEffects(AlundraEntityScriptProxy player, int mode) => Record(mode, WalkStepStubName, player);

    /// <summary>Stub (E20.e) of the landing, <c>0x80030224-0x800303CC</c>: the sound of the slope (11 on a flat ground) and the dust effect 7 (<c>0x8003BDD8</c>, which draws in the generator). Called only when the hero lands (the guards are ported in <see cref="Create"/>). Not ported.</summary>
    private static void PlayerLandingEffects(AlundraEntityScriptProxy player, int mode) => Record(mode, LandingStubName, player);

    /// <summary>Stub (E20.e) of the grass cut, <c>0x8002FDC0</c> (the call <c>0x8003A7B0</c> with the hero), called at every tick of an attack state. Not ported.</summary>
    private static void PlayerGrassCut(AlundraEntityScriptProxy player, int mode) => Record(mode, GrassCutStubName, player);

    /// <summary>The sound of the alarm of the low life (<c>0x80030424</c>: <c>PlaySound(0x190)</c>).</summary>
    internal const int LowHpAlarmSfx = 0x190;

    /// <summary>The value the alarm counter is reloaded with when it reaches -1 (<c>0x80030428</c>).</summary>
    internal const int LowHpAlarmReload = 0x2D;

    /// <summary>
    /// The alarm of the low life, <c>0x800303DC-0x80030430</c> (E20.c2a T4; mode 0 only, the call is made from <see cref="Create"/> after the dispatches). Read again in the binary: the life of the HERO ENTITY
    /// (<c>0x80127D30 +0x14</c>, <see cref="AlundraEntityScriptProxy.Hp"/>) must not be 0 (<c>beqz</c>, <c>0x800303EC</c>) and the maximum (<c>+0x18</c>) must not be below five times the life
    /// (<c>sll 2; addu</c>, <c>slt</c>, <c>0x800303F0-0x80030400</c>, signed 32-bit). When both hold, the counter <c>[0x80098F2C]</c> (<see cref="AlundraGameState.LowHpAlarmCounter"/>; the address
    /// <c>0x800A8F2C</c> of the plan is a slip: <c>lui 0x800A</c> with <c>-0x70D4</c> is <c>0x80098F2C</c>) is decremented and stored (<c>0x80030414-0x8003041C</c>); when the new value is -1 the sound
    /// <see cref="LowHpAlarmSfx"/> plays and the counter is reloaded with <see cref="LowHpAlarmReload"/> (<c>0x80030420-0x8003042C</c>), so the sound repeats every 46 ticks. Otherwise (life 0, or the life above a fifth of the
    /// maximum) nothing is read or written: the counter keeps its value and resumes where it was. The counter is a global that starts at 0 and nothing else writes (a scan of the image finds three
    /// accesses, all here), so the first alarm tick sounds at once. A hero without a script host (a bare proxy) has no session state: no alarm.
    /// </summary>
    private static void PlayerLowHpAlarm(AlundraEntityScriptProxy player)
    {
        Record(FreeMode, LowHpAlarmStubName, player); // the call is still recorded for the dispatch tests (the name keeps the word 'stub' of E20.f2).

        var life = player.Hp;
        if (life == 0 || player.HpMax < unchecked(life * 5))
        {
            return;
        }

        var host = player.ScriptHost;
        if (host == null)
        {
            return;
        }

        var state = host.GameState;
        var counter = state.LowHpAlarmCounter - 1;
        if (counter == -1)
        {
            host.SoundPlayer?.PlaySfx(LowHpAlarmSfx);
            counter = LowHpAlarmReload;
        }

        state.LowHpAlarmCounter = counter;
    }

    private static void Record(int mode, string stub, AlundraEntityScriptProxy player) => StubCallsForTests?.Add((mode, stub, player.TargetAnimationId));
}
