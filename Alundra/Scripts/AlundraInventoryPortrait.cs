namespace Alundra.Scripts;

/// <summary>
/// The inventory's opening portrait (docs/plan-portrait-inventaire.md, PI7): a port of the original's single
/// "character portrait" quad, the block at <c>0x80180070</c> driven by <c>0x80057b64..0x800581fc</c> of the France
/// executable, as the inventories use it (entry point <c>0x80057c18</c>: rest (248, 104), 48x56).
///
/// <para>States (the original's own values): 0 idle (nothing drawn); 5 opening (flies from Alundra's head to the rest
/// position while growing); 4 at rest (48x56 at (248, 104) for as long as a menu is open); 2 returning (flies back to
/// the head while shrinking, then 0).</para>
///
/// <para>One step per tick (<see cref="Step"/>, the original's <c>UpdateHudTransitionVariables</c> <c>0x80057ebc</c>,
/// called from <c>DisplayInventoryCharacterPortrait</c> <c>0x80058134</c> inside <c>RenderScene</c>). With the step
/// counter <c>s</c> going 15..1: <c>X = current + trunc(s * span / 15)</c> (same for Y); opening size
/// <c>trunc(48 * (15 - s) / 15)</c> x <c>trunc(56 * (15 - s) / 15)</c>, return size <c>trunc(48 * s / 15)</c> x
/// <c>trunc(56 * s / 15)</c>; C#'s integer division truncates toward zero like the original's signed division
/// (magic <c>0x88888889</c>). At <c>s = 0</c> an opening becomes the rest state and a return becomes idle. The quad is
/// anchored at its top-left corner. The vertex colour ramp of the original (255 to 128 while opening, 127 to 246 while
/// returning) is NOT ported: the tint stays normal (author's decision D2).</para>
///
/// <para>One instance (<see cref="Instance"/>) is shared by the main and the sub-inventory directors, like the
/// original's single block (P3): a start requested while the previous menu's return is still flying is ignored, as
/// the original's own <c>state == 0</c> guard (<c>0x80057d30</c>) ignores it.</para>
/// </summary>
public sealed class AlundraInventoryPortrait
{
    public static readonly AlundraInventoryPortrait Instance = new();

    public const int StateIdle = 0;
    public const int StateReturning = 2;
    public const int StateAtRest = 4;
    public const int StateOpening = 5;

    /// <summary>The rest position the inventory entry point sets (<c>0x80057c18</c>: <c>0xf8, 0x68</c>).</summary>
    public const int RestX = 248;
    public const int RestY = 104;

    /// <summary>The size the inventory entry point hard-codes (<c>0x30 x 0x38</c>).</summary>
    public const int FullWidth = 48;
    public const int FullHeight = 56;

    /// <summary>The number of flight steps (<c>0x80057d74</c>: step = 15).</summary>
    public const int Steps = 15;

    /// <summary>How far above the player's feet the flight starts and ends (<c>PosY - scrollY - PosZ - 32</c>).</summary>
    public const int HeadOffsetY = 32;

    private int _step;
    private int _currentX;
    private int _currentY;
    private int _spanX;
    private int _spanY;

    /// <summary>One of <see cref="StateIdle"/>, <see cref="StateOpening"/>, <see cref="StateAtRest"/>, <see cref="StateReturning"/>.</summary>
    public int State { get; private set; }

    /// <summary>The top-left corner of the quad drawn by the last <see cref="Step"/>, in native 320x240 pixels.</summary>
    public int X { get; private set; }

    public int Y { get; private set; }

    /// <summary>The size of the quad drawn by the last <see cref="Step"/>; 0 when nothing shows.</summary>
    public int DrawnWidth { get; private set; }

    public int DrawnHeight { get; private set; }

    /// <summary>Whether the last <see cref="Step"/> drew a quad with an area: false while idle and on the two 0x0 calls
    /// (the first opening step and the last return step).</summary>
    public bool IsVisible => DrawnWidth > 0 && DrawnHeight > 0;

    /// <summary>The player's head point on the original's 320x240 screen, where every flight starts or ends:
    /// <c>(PosX_hi - scrollX, PosY_hi - scrollY - PosZ_hi - 32)</c> (<c>0x80057e08-0x80057e84</c>). The positions are the
    /// original's 16.16 fixed-point values, whose integer part the original reads with <c>lh 2(ptr)</c>: an arithmetic
    /// shift by 16, with no pixel offset (the decompilation's "+2" in X is that halfword offset, not pixels).</summary>
    public static (int X, int Y) ComputeHeadPoint(int posX, int posY, int posZ, int scrollX, int scrollY)
    {
        return ((posX >> 16) - scrollX, (posY >> 16) - scrollY - (posZ >> 16) - HeadOffsetY);
    }

    /// <summary>Starts the opening flight from <paramref name="headX"/>, <paramref name="headY"/> to the rest position
    /// (<c>0x80057cf0</c>); ignored unless idle, like the original.</summary>
    public void Start(int headX, int headY)
    {
        if (State != StateIdle)
        {
            return;
        }

        State = StateOpening;
        _step = Steps;
        _currentX = RestX;
        _currentY = RestY;
        _spanX = headX - RestX;
        _spanY = headY - RestY;
    }

    /// <summary>Starts the return flight from the rest position to the head point read NOW, at exit time
    /// (<c>UpdateHudTransitionState</c> <c>0x80057b84</c>, which re-reads the player's and the camera's positions);
    /// ignored while idle.</summary>
    public void BeginReturn(int headX, int headY)
    {
        if (State == StateIdle)
        {
            return;
        }

        State = StateReturning;
        _step = Steps;
        _currentX = headX;
        _currentY = headY;
        _spanX = RestX - headX;
        _spanY = RestY - headY;
    }

    /// <summary>One tick of the portrait: computes the quad to draw and advances the flight. Nothing is drawn while
    /// idle (the draw guard at <c>0x8005817c</c>).</summary>
    public void Step()
    {
        if (State == StateIdle)
        {
            DrawnWidth = 0;
            DrawnHeight = 0;
            return;
        }

        if (_step == 0)
        {
            X = RestX;
            Y = RestY;
            if ((State & StateReturning) == 0)
            {
                // Opening done (5 -> 4), or already at rest (4 stays 4): the full portrait at rest.
                State &= ~1;
                DrawnWidth = FullWidth;
                DrawnHeight = FullHeight;
            }
            else
            {
                // Return done: one last 0x0 quad, then nothing.
                State = StateIdle;
                DrawnWidth = 0;
                DrawnHeight = 0;
            }

            return;
        }

        X = _currentX + _spanX * _step / Steps;
        Y = _currentY + _spanY * _step / Steps;
        if (State == StateOpening)
        {
            DrawnWidth = FullWidth * (Steps - _step) / Steps;
            DrawnHeight = FullHeight * (Steps - _step) / Steps;
        }
        else
        {
            DrawnWidth = FullWidth * _step / Steps;
            DrawnHeight = FullHeight * _step / Steps;
        }

        _step--;
    }

    /// <summary>Test-only: restores the idle state.</summary>
    internal void ResetForTests()
    {
        State = StateIdle;
        _step = 0;
        _currentX = 0;
        _currentY = 0;
        _spanX = 0;
        _spanY = 0;
        X = 0;
        Y = 0;
        DrawnWidth = 0;
        DrawnHeight = 0;
    }
}
