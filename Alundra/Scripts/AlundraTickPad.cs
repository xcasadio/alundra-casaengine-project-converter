#nullable enable
namespace Alundra.Scripts;

/// <summary>
/// E13.d D1 (docs/plan-e13d-inventaire.md, D-E13D-8, D-E13D-9): the original's <c>g_padState1</c> as the
/// inventory reads it - press edges and key repeat - advanced ONCE PER LOGIC TICK, the port's equivalent
/// of the original's 50 Hz frame, where <c>PadManager.UpdatePads</c> refreshes it at the head of every
/// main-loop frame (GameEngine.cs:1518).
///
/// <para><b>Why a second pad next to <see cref="AlundraGameState.LastPadState"/></b>: that snapshot is
/// rebuilt once per RENDERED frame (AlundraEntityScriptProxy.Update), and the port runs zero, one or
/// several logic ticks per rendered frame (AlundraLogicClock). An edge or a repeat counter computed per
/// rendered frame would depend on the display rate: a consumer ticking twice in one frame would see the
/// same edge twice, and a frame with no tick would lose it (plan §1.3, measured by D0.10). This pad only
/// SAMPLES that per-frame hold state, then derives its own edges and repeat once per tick, so one press
/// gives exactly one edge and 20 ticks are 20 frames of the original, whatever the display rate. The
/// existing per-frame consumers (the hero, the dialogue box, opcode 0x2F) keep their own clock.</para>
///
/// <para>Ported line by line from <c>PadManager.UpdatePad</c> (PadManager.cs:27-74), fields from
/// <c>PadState</c> (PadState.cs:24-31). <see cref="RepeatInterval"/> keeps the original's only
/// assignment, 0 (GameInitializer.cs:169, measured by D0.4): after the first edge, a held state stays
/// silent for <see cref="MaxNbFrameHeld"/> ticks, then repeats on EVERY tick until it changes.</para>
/// </summary>
public sealed class AlundraTickPad
{
    /// <summary><c>PadState.MaxNbFrameHeld</c>, 20 (PadState.cs:25).</summary>
    public const uint MaxNbFrameHeld = 20;

    /// <summary><c>PadState.RepeatInterval</c>, set to 0 by <c>GameInitializer.cs:169</c> and written
    /// nowhere else in the decompilation (plan §6 point 8).</summary>
    public uint RepeatInterval { get; private set; }

    public uint IsOverThanMaxNbFrameHeld { get; private set; }

    public uint NumberOfFrameHold { get; private set; }

    /// <summary>The hold state this pad last sampled.</summary>
    public uint ButtonsHold { get; private set; }

    /// <summary>Buttons that went down on this tick.</summary>
    public uint ButtonsJustPressed { get; private set; }

    /// <summary>Buttons that went up on this tick.</summary>
    public uint ButtonsReleased { get; private set; }

    /// <summary>The edges with key repeat: <see cref="ButtonsJustPressed"/> on a change of state, then,
    /// while the state holds, nothing for <see cref="MaxNbFrameHeld"/> ticks, then the held buttons
    /// every <see cref="RepeatInterval"/> + 1 ticks - every tick, with the original's 0.</summary>
    public uint ButtonsJustPressedByInterval { get; private set; }

    /// <summary>Port of <c>PadManager.UpdatePad</c> (PadManager.cs:27-74) for one tick, from the latest
    /// sampled hold state.</summary>
    public void Update(uint buttonState)
    {
        uint numberOfFrameHold;

        ButtonsJustPressed = buttonState & (buttonState ^ ButtonsHold);
        ButtonsReleased = ButtonsHold & (buttonState ^ ButtonsHold);

        if (ButtonsHold != buttonState || ButtonsHold == 0)
        {
            IsOverThanMaxNbFrameHeld = 0;
            NumberOfFrameHold = 0;
            ButtonsJustPressedByInterval = ButtonsJustPressed;
            ButtonsHold = buttonState;
            return;
        }

        if (IsOverThanMaxNbFrameHeld == 0)
        {
            numberOfFrameHold = NumberOfFrameHold;
            if (numberOfFrameHold < MaxNbFrameHeld)
            {
                NumberOfFrameHold = numberOfFrameHold + 1;
                ButtonsJustPressedByInterval = 0;
                ButtonsHold = buttonState;
                return;
            }

            IsOverThanMaxNbFrameHeld = 1;
        }
        else
        {
            numberOfFrameHold = NumberOfFrameHold;
            if (numberOfFrameHold < RepeatInterval)
            {
                NumberOfFrameHold = numberOfFrameHold + 1;
                ButtonsJustPressedByInterval = 0;
                ButtonsHold = buttonState;
                return;
            }
        }

        NumberOfFrameHold = 0;
        ButtonsJustPressedByInterval = buttonState;
        ButtonsHold = buttonState;
    }

    /// <summary>Back to the zero-initialised state of the original's global.</summary>
    public void Reset()
    {
        RepeatInterval = 0;
        IsOverThanMaxNbFrameHeld = 0;
        NumberOfFrameHold = 0;
        ButtonsHold = 0;
        ButtonsJustPressed = 0;
        ButtonsReleased = 0;
        ButtonsJustPressedByInterval = 0;
    }
}
