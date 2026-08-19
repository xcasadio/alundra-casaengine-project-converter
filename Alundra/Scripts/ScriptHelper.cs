namespace Alundra.Scripts;

/// <summary>
/// Event-program slot constants, ported from
/// alundra-datas-analyser/AlundraTools/AlundraEngine/Gameplay/Scripts/ScriptHelper.cs (only the
/// constants used by the entity event status machine - the rest of the original helper belongs to the
/// bytecode interpreter, a later chantier). Slots A/C/D/E/F map 1:1 to
/// <see cref="AlundraEntityScriptProxy.ProgramIndexes"/> / <see cref="AlundraEntityScriptProxy.SpriteProgramIndexes"/>
/// index 0/2/3/4/5 - the mapper (<c>EntityRecordMapper</c>) already relies on that A..F = 0..5 layout.
/// </summary>
public static class ScriptHelper
{
    /// <summary>No program should run this frame - not a valid array index into ProgramIndexes.</summary>
    public const int ProgramUnknown = -1;

    public const int ProgramALoad = 0;
    public const int ProgramBMap = 1;
    public const int ProgramCTick = 2;
    public const int ProgramDTouch = 3;
    public const int ProgramEDeactivate = 4;
    public const int ProgramFInteract = 5;
}
