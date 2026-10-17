#nullable enable
using System;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Input;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Input;
using Alundra.Session;
using Alundra.Scripts;

namespace Alundra.Hero;

/// <summary>
/// One PSX pad snapshot subset (see <c>alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/PadState.cs</c>
/// for the full original bit layout), built once per frame by
/// <see cref="AlundraPlayerController.BuildPadState"/> from the converted "AlundraButtons"
/// <see cref="ButtonsMapping"/> (converter E2-A, <c>Data/Alundra.buttonsMapping</c>) and fed to
/// <see cref="AlundraPlayerManager.MovePlayer"/>. Every bit a mapped action can reach is ported here, with
/// the original's own values (PadState.cs:5-19): the nine gameplay buttons, and since E13.d D1
/// (docs/plan-e13d-inventaire.md, D-E13D-11) L1/L2/R1/R2/Select, which open and switch the inventories.
/// </summary>
public readonly struct AlundraPadState
{
    public const uint L2 = 0x0001;
    public const uint R2 = 0x0002;
    public const uint L1 = 0x0004;
    public const uint R1 = 0x0008;
    public const uint Triangle = 0x0010;
    public const uint Circle = 0x0020;
    public const uint Cross = 0x0040;
    public const uint Square = 0x0080;
    public const uint Select = 0x0100;
    public const uint Start = 0x0800;
    public const uint Up = 0x1000;
    public const uint Right = 0x2000;
    public const uint Down = 0x4000;
    public const uint Left = 0x8000;

    /// <summary>Buttons currently held, packed the same way as the original's own <c>ButtonsHold</c>.</summary>
    public uint ButtonsHold { get; init; }

    /// <summary>Buttons pressed THIS frame (not held last frame), packed like the original's own
    /// <c>ButtonsJustPressed</c>. Not yet consumed by <see cref="AlundraPlayerManager"/> (E2's own ported
    /// subset is direction-only), kept for the next opcodes/animations that will need it.</summary>
    public uint ButtonsJustPressed { get; init; }

    /// <summary>E20.a A1 (docs/plan-e20-heros.md section 1.3): buttons that went up on this tick, the original's <c>ButtonsReleased</c> (pad +0x14). Only a pad built from
    /// <see cref="AlundraTickPad"/> (<see cref="FromTickPad"/>) carries it; the per-frame snapshot of <see cref="AlundraPlayerController.BuildPadState"/> leaves it 0.</summary>
    public uint ButtonsReleased { get; init; }

    /// <summary>E20.d2a S1 (D-E20-32): the direction bits (<see cref="Up"/> to <see cref="Left"/>) held by the KEYBOARD and the D-PAD (the digital actions <c>MoveUp</c> to <c>MoveRight</c>). <see cref="ButtonsHold"/> keeps the OR of these and of
    /// <see cref="StickBitsHold"/>; 0 by default so an initializer that does not know the field keeps its meaning.</summary>
    public uint DigitalDirectionBitsHold { get; init; }

    /// <summary>E20.d2a S1: the direction bits held by the left STICK (the analog actions <c>Move*Stick</c>, pressed from the 0.5 dead zone). 0 by default.</summary>
    public uint StickBitsHold { get; init; }

    /// <summary>E20.d2a S1: the signed horizontal deflection of the left stick, positive to the right (<c>ButtonState.Value</c> of <c>MoveRightStick</c>, never a difference of the two halves, which would double it). 0 by default.</summary>
    public float StickX { get; init; }

    /// <summary>E20.d2a S1: the signed vertical deflection of the left stick, positive upward (<c>ButtonState.Value</c> of <c>MoveUpStick</c>). 0 by default.</summary>
    public float StickY { get; init; }

    /// <summary>
    /// E20.a A1 (D-E20-2): the pad of ONE logic tick, as the binary's <c>MovePlayer</c> reads it (<c>g_padState1</c>, refreshed once per tick just before <c>UpdateWorld</c>,
    /// <c>0x8002BB5C</c>): hold (<c>0x80126F28</c>), pressed edge (<c>0x80126F2A</c>) and released edge, from <paramref name="tickPad"/> (<see cref="GameState.TickPad"/> in a driven world).
    /// </summary>
    public static AlundraPadState FromTickPad(AlundraTickPad tickPad) => FromTickPad(tickPad, default);

    /// <summary>E20.d2a S1: the pad of the tick as <see cref="FromTickPad(AlundraTickPad)"/> builds it, plus the stick sample of the FRAME (<paramref name="frameSample"/>, <see cref="GameState.LastPadState"/>):
    /// the digital and stick direction bits and the signed deflection, copied as they are (the tick pad itself does not carry them).</summary>
    public static AlundraPadState FromTickPad(AlundraTickPad tickPad, in AlundraPadState frameSample) => new()
    {
        ButtonsHold = tickPad.ButtonsHold,
        ButtonsJustPressed = tickPad.ButtonsJustPressed,
        ButtonsReleased = tickPad.ButtonsReleased,
        DigitalDirectionBitsHold = frameSample.DigitalDirectionBitsHold,
        StickBitsHold = frameSample.StickBitsHold,
        StickX = frameSample.StickX,
        StickY = frameSample.StickY,
    };
}
