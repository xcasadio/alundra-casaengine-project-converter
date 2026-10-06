namespace Alundra.Scripts;

/// <summary>Where a portrait flight is at the pass just run (E19.f4b): the flight in, the rest, the flight back, or the last (degenerate) pass of the return.</summary>
public enum AlundraPortraitPhase
{
    /// <summary>Nothing drawn (idle).</summary>
    None,

    /// <summary>Flying from the head to the rest position, growing.</summary>
    In,

    /// <summary>At rest, full size.</summary>
    Rest,

    /// <summary>Flying back to the head, shrinking.</summary>
    Out,

    /// <summary>The last pass of a return: a 0x0 quad at the rest position, then nothing.</summary>
    Gone,
}

/// <summary>
/// The original's single "character portrait" quad (docs/plan-portrait-inventaire.md, PI7): a port of the block at <c>0x80180070</c> driven by
/// <c>0x80057b64..0x800581fc</c> of the France executable, as the inventories use it (entry point <c>0x80057c18</c>: rest (248, 104), 48x56) and, since E19.f4b
/// (docs/plan-e19-opcodes.md, F4B-R3), as the dialogue uses it (entry point <c>0x80057c84</c>: rest (8, 172 - h), 48 x h), through a SECOND instance
/// (<see cref="AlundraDialogueDirector"/>'s own: the binary's shared block is crossed between the inventory and the dialogue only in cases that cannot be reached).
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
/// returning) is NOT rendered: the tint stays normal (author's decision D2, D-E19-89); since E19.f4b it is computed and exposed
/// (<see cref="Rgb"/>: opening <c>127 + trunc(128 * s / 15)</c>, rest 128, return <c>127 + trunc(128 * (15 - s) / 15)</c>, last pass 0) so the view can use it.</para>
///
/// <para>The inventories' instance (<see cref="Instance"/>) is shared by the main and the sub-inventory directors, like the
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
    private int _restX = RestX;
    private int _restY = RestY;
    private int _fullWidth = FullWidth;
    private int _fullHeight = FullHeight;

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

    /// <summary>Whether the last <see cref="Step"/> put a quad in the ordering table at all (E19.f4b): true on every pass of a flight, the two 0x0 passes included; false while idle.</summary>
    public bool DrawnThisStep { get; private set; }

    /// <summary>The vertex colour of the quad of the last <see cref="Step"/>, the same on the three channels (128 = normal, see the class doc); 0 on the last pass of a return and while idle.
    /// Computed and exposed, not rendered (D-E19-89).</summary>
    public int Rgb { get; private set; }

    /// <summary>Where the last <see cref="Step"/> was in the flight (<see cref="AlundraPortraitPhase.None"/> while idle).</summary>
    public AlundraPortraitPhase Phase { get; private set; }

    /// <summary>The player's head point on the original's 320x240 screen, where every flight starts or ends:
    /// <c>(PosX_hi - scrollX, PosY_hi - scrollY - PosZ_hi - 32)</c> (<c>0x80057e08-0x80057e84</c>). The positions are the
    /// original's 16.16 fixed-point values, whose integer part the original reads with <c>lh 2(ptr)</c>: an arithmetic
    /// shift by 16, with no pixel offset (the decompilation's "+2" in X is that halfword offset, not pixels).</summary>
    public static (int X, int Y) ComputeHeadPoint(int posX, int posY, int posZ, int scrollX, int scrollY)
    {
        return ((posX >> 16) - scrollX, (posY >> 16) - scrollY - (posZ >> 16) - HeadOffsetY);
    }

    /// <summary>The player's head point as of the current tick (<see cref="SetHeadPoint"/>). The original stores
    /// POINTERS to the player's and the camera's positions and dereferences them when a flight starts or returns
    /// (<c>0x80057d58-0x80057d70</c>, <c>0x80057b9c-0x80057bf0</c>); the port refreshes this point every tick instead
    /// and reads it at those same two moments.</summary>
    public int HeadX { get; private set; }

    public int HeadY { get; private set; }

    /// <summary>Called by the world every tick, before the inventory directors, with
    /// <see cref="ComputeHeadPoint"/>'s result for the player and the camera of that tick.</summary>
    public void SetHeadPoint(int headX, int headY)
    {
        HeadX = headX;
        HeadY = headY;
    }

    /// <summary>The inventories' start (<c>DisplayInventory</c> <c>0x800556b0</c>, <c>StartFadeOut</c>
    /// <c>0x800526ac</c>): <see cref="Start(int, int)"/> from the current head point.</summary>
    public void StartFromHead() => Start(HeadX, HeadY);

    /// <summary>The inventories' exits (<c>UpdateHudTransitionState</c> at <c>0x80056938</c>, <c>0x80056974</c>,
    /// <c>0x80053648</c>, <c>0x80053684</c>): <see cref="BeginReturn"/> to the current head point.</summary>
    public void ReturnToHead() => BeginReturn(HeadX, HeadY);

    /// <summary>Starts the opening flight from <paramref name="headX"/>, <paramref name="headY"/> to the rest position
    /// (<c>0x80057cf0</c>); ignored unless idle, like the original. The inventories' own wrapper (<c>0x80057c18</c>): rest (248, 104), 48 x 56.</summary>
    public void Start(int headX, int headY)
    {
        Start(headX, headY, RestX, RestY, FullWidth, FullHeight);
    }

    /// <summary>E19.f4b (F4B-R3): the same start with the rest position and the size of the caller - the dialogue's wrapper (<c>0x80057c84</c>) gives rest (8, 116) and 48 x 56, and, for a tall
    /// portrait, rest (8, 100) and 48 x 72 (D-E19-90). Returns whether the start was accepted: ignored (false) unless idle, whatever the arguments; the rest and the size are
    /// kept only for an accepted start.</summary>
    public bool Start(int headX, int headY, int restX, int restY, int width, int height)
    {
        if (State != StateIdle)
        {
            return false;
        }

        State = StateOpening;
        _step = Steps;
        _restX = restX;
        _restY = restY;
        _fullWidth = width;
        _fullHeight = height;
        _currentX = _restX;
        _currentY = _restY;
        _spanX = headX - _restX;
        _spanY = headY - _restY;
        return true;
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
        _spanX = _restX - headX;
        _spanY = _restY - headY;
    }

    /// <summary>One tick of the portrait: computes the quad to draw and advances the flight. Nothing is drawn while
    /// idle (the draw guard at <c>0x8005817c</c>).</summary>
    public void Step()
    {
        if (State == StateIdle)
        {
            DrawnWidth = 0;
            DrawnHeight = 0;
            DrawnThisStep = false;
            Rgb = 0;
            Phase = AlundraPortraitPhase.None;
            return;
        }

        DrawnThisStep = true;
        if (_step == 0)
        {
            X = _restX;
            Y = _restY;
            if ((State & StateReturning) == 0)
            {
                // Opening done (5 -> 4), or already at rest (4 stays 4): the full portrait at rest.
                State &= ~1;
                DrawnWidth = _fullWidth;
                DrawnHeight = _fullHeight;
                Rgb = 0x80;
                Phase = AlundraPortraitPhase.Rest;
            }
            else
            {
                // Return done: one last 0x0 quad, then nothing.
                State = StateIdle;
                DrawnWidth = 0;
                DrawnHeight = 0;
                Rgb = 0;
                Phase = AlundraPortraitPhase.Gone;
            }

            return;
        }

        X = _currentX + _spanX * _step / Steps;
        Y = _currentY + _spanY * _step / Steps;
        if (State == StateOpening)
        {
            DrawnWidth = _fullWidth * (Steps - _step) / Steps;
            DrawnHeight = _fullHeight * (Steps - _step) / Steps;
            Rgb = 0x7F + ((_step << 7) / Steps);
            Phase = AlundraPortraitPhase.In;
        }
        else
        {
            DrawnWidth = _fullWidth * _step / Steps;
            DrawnHeight = _fullHeight * _step / Steps;
            Rgb = 0x7F + (((Steps - _step) << 7) / Steps);
            Phase = AlundraPortraitPhase.Out;
        }

        _step--;
    }

    /// <summary>Test-only: restores the idle state, through <see cref="ResetSessionForLoad"/>.</summary>
    internal void ResetForTests()
    {
        ResetSessionForLoad();
    }

    /// <summary>E16.d K8, step 4 (docs/plan-e16-etat-partie.md): loading a save puts the portrait back to
    /// <see cref="StateIdle"/> with nothing drawn - a flight started during the load's fade (SD5) must not
    /// survive into the loaded game. The head point is refreshed by the world every tick anyway. Also the reset of the dialogue's
    /// instance at a map entry (E19.f4b).</summary>
    internal void ResetSessionForLoad()
    {
        State = StateIdle;
        _step = 0;
        _currentX = 0;
        _currentY = 0;
        _spanX = 0;
        _spanY = 0;
        _restX = RestX;
        _restY = RestY;
        _fullWidth = FullWidth;
        _fullHeight = FullHeight;
        X = 0;
        Y = 0;
        DrawnWidth = 0;
        DrawnHeight = 0;
        DrawnThisStep = false;
        Rgb = 0;
        Phase = AlundraPortraitPhase.None;
        HeadX = 0;
        HeadY = 0;
    }
}
