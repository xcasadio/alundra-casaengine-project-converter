#nullable enable
using Alundra.Transitions;
using Alundra.Maps;

namespace Alundra.Cameras;

/// <summary>
/// E19.k1 (docs/plan-e19-opcodes.md, section 1.2k) - the camera sway (screen shake) state of the original: ONE
/// global structure at <c>0x800E4338</c> (Flag, LimitX/Y, SpeedX/Y, OffsetX/Y, ReachX/Y), armed by opcode
/// <c>0x8E</c> (<see cref="Start"/>, handler <c>0x80040534</c>), disarmed by <c>0x8F</c> (<see cref="Stop"/>,
/// handler <c>0x80040598</c>) and advanced once per logic tick by <see cref="Step"/> (<c>0x8002C894</c>, first call
/// of the camera follow <c>0x8002CDA0</c>). The camera follow adds <see cref="OffsetX"/>/<see cref="OffsetY"/> to
/// the scroll at every tick (<see cref="CameraMath.ComputeSmoothedCameraTarget"/>).
///
/// <para>Session-scoped, like <see cref="ScreenFadeDirector.Instance"/>, and never saved. A map load
/// only clears <see cref="Flag"/> (<see cref="OnMapLoad"/>, <c>0x8002CD54</c>): the first <see cref="Step"/>
/// afterwards clears Limit, Speed and Offset, and <see cref="ReachX"/>/<see cref="ReachY"/> persist from one
/// map to the next - faithful to the binary, kept.</para>
///
/// <para>Operands are the unsigned bytes of the opcode (0 to 255), stored as read.</para>
/// </summary>
public sealed class CameraSway
{
    /// <summary>The one session-scoped instance every <see cref="WorldProxy"/> shares - never
    /// <see langword="new"/>'d per world.</summary>
    public static readonly CameraSway Instance = new();

    /// <summary>Internal rather than private so pure tests can build a fresh instance (all nine fields at 0).</summary>
    internal CameraSway()
    {
    }

    public int Flag { get; internal set; }
    public int LimitX { get; internal set; }
    public int LimitY { get; internal set; }
    public int SpeedX { get; internal set; }
    public int SpeedY { get; internal set; }
    public int OffsetX { get; internal set; }
    public int OffsetY { get; internal set; }
    public int ReachX { get; internal set; }
    public int ReachY { get; internal set; }

    /// <summary>Opcode 0x8E <c>[speedX, speedY, limitX, limitY]</c>: Flag = 1 and the four parameters; touches
    /// neither the offsets nor the reach flags.</summary>
    public void Start(int speedX, int speedY, int limitX, int limitY)
    {
        Flag = 1;
        SpeedX = speedX;
        SpeedY = speedY;
        LimitX = limitX;
        LimitY = limitY;
    }

    /// <summary>Opcode 0x8F: Flag = 0, nothing else (the next <see cref="Step"/> clears Limit, Speed, Offset).</summary>
    public void Stop() => Flag = 0;

    /// <summary>Map load (<c>0x8002CD54</c>): only Flag returns to 0.</summary>
    internal void OnMapLoad() => Flag = 0;

    /// <summary>One logic tick of the sway (<c>0x8002C894</c>). With Flag 0, Limit, Speed and Offset go to 0
    /// (not Reach). Axis X: LimitX = 0 or SpeedX = 0 clears OffsetX. Axis Y: only LimitY = 0 clears OffsetY
    /// (<c>0x8002C944</c>; a zero SpeedY freezes OffsetY inside the bounds). Per axis, a triangle wave that
    /// starts toward the negative: Reach 0 -> <c>Off -= Speed</c>, and at <c>Off &lt;= -Limit</c> clamp and
    /// Reach = 1; Reach 1 -> <c>Off += Speed</c>, and at <c>Off &gt;= Limit</c> clamp and Reach = 0.</summary>
    internal void Step()
    {
        if (Flag == 0)
        {
            LimitX = LimitY = SpeedX = SpeedY = OffsetX = OffsetY = 0;
        }

        if (LimitX == 0 || SpeedX == 0)
        {
            OffsetX = 0;
        }
        else
        {
            (OffsetX, ReachX) = StepAxis(OffsetX, ReachX, SpeedX, LimitX);
        }

        if (LimitY == 0)
        {
            OffsetY = 0;
        }
        else
        {
            (OffsetY, ReachY) = StepAxis(OffsetY, ReachY, SpeedY, LimitY);
        }
    }

    private static (int Offset, int Reach) StepAxis(int offset, int reach, int speed, int limit)
    {
        if (reach == 0)
        {
            offset -= speed;
            if (offset <= -limit)
            {
                offset = -limit;
                reach = 1;
            }
        }
        else
        {
            offset += speed;
            if (offset >= limit)
            {
                offset = limit;
                reach = 0;
            }
        }

        return (offset, reach);
    }

    /// <summary>
    /// E20.h4d1 (D-H4D1-2): the buffer of the session of the pilot of the fire tome 2 (<c>0x80127138</c>, <c>0x80035C64</c>): nine words in the order of the block (Flag, LimitX, LimitY, SpeedX, SpeedY, OffsetX, OffsetY, ReachX, ReachY), zero at the start, written by
    /// <see cref="CaptureToBuffer"/>, read by <see cref="RestoreFromBuffer"/>. Session-scoped like the block, never saved, kept by <see cref="OnMapLoad"/> and cleared by <see cref="ResetForTests"/> only.
    /// </summary>
    internal int[] Buffer { get; } = new int[9];

    /// <summary>E20.h4d1 (D-H4D1-2): copies the nine words of the block to the buffer, in order (the pilot of the fire tome 2 at the birth of the column, before it writes the block). The block is left as it is.</summary>
    internal void CaptureToBuffer()
    {
        Buffer[0] = Flag;
        Buffer[1] = LimitX;
        Buffer[2] = LimitY;
        Buffer[3] = SpeedX;
        Buffer[4] = SpeedY;
        Buffer[5] = OffsetX;
        Buffer[6] = OffsetY;
        Buffer[7] = ReachX;
        Buffer[8] = ReachY;
    }

    /// <summary>
    /// E20.h4d1 (D-H4D1-2): copies the buffer to the nine words of the block, in order, WITH NO GUARD (<c>0x80034E08</c> and the end of the pilot): an empty buffer (no cast ever captured one) zeroes the block, a stale one is restored as it is. The buffer is not cleared.
    /// The step of the shake is not run.
    /// </summary>
    internal void RestoreFromBuffer()
    {
        Flag = Buffer[0];
        LimitX = Buffer[1];
        LimitY = Buffer[2];
        SpeedX = Buffer[3];
        SpeedY = Buffer[4];
        OffsetX = Buffer[5];
        OffsetY = Buffer[6];
        ReachX = Buffer[7];
        ReachY = Buffer[8];
    }

    /// <summary>Test-only: the nine fields to 0, Reach included, and the buffer of the fire tome 2 (E20.h4d1).</summary>
    internal void ResetForTests()
    {
        Flag = LimitX = LimitY = SpeedX = SpeedY = OffsetX = OffsetY = ReachX = ReachY = 0;
        System.Array.Clear(Buffer);
    }
}
