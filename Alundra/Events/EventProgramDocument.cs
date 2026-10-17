#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CasaEngine.Core.Logging;
using Alundra.Entities;

namespace Alundra.Events;

/// <summary>
/// One map's event bytecode, read back from the converter's own companion file
/// (<c>Maps/{Zone}/{Name}-{id}/events/{Name}-{id}.events.json</c> - see
/// <c>AlundraCasaEngineProjectConverter.Writers.EventCodeWriter</c>/<c>Readers.EventCodeReader</c>,
/// whose <c>EventCodeDocument</c> this mirrors field-for-field). Uninterpreted data: the six per-program
/// tables (A-F) hold offsets into <see cref="Codes"/>, resolved the same way the original does (see
/// <see cref="AlundraEventProgramRunner.InitializeEventData"/>).
/// </summary>
public sealed class EventProgramDocument
{
    public int MapIndex { get; set; }
    public int[] EventCodesATable { get; set; } = Array.Empty<int>();
    public int[] EventCodesBTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesCTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesDTable { get; set; } = Array.Empty<int>();
    public int[] EventCodesETable { get; set; } = Array.Empty<int>();
    public int[] EventCodesFTable { get; set; } = Array.Empty<int>();

    /// <summary>Raw code bytes (0-255 per source, stored as JSON numbers - see EventCodeWriter's class
    /// doc). <see cref="AlundraEventProgramRunner"/> works off <see cref="CodesAsBytes"/> instead.</summary>
    public int[] Codes { get; set; } = Array.Empty<int>();

    public byte[] CodesAsBytes() => Array.ConvertAll(Codes, value => (byte)value);

    /// <summary>Picks the A..F table for <paramref name="programSlot"/> (a <c>ScriptHelper.Program*</c>
    /// constant), mirroring the switch in the original's InitializeEventData.</summary>
    public int[] TableFor(int programSlot) => programSlot switch
    {
        ScriptHelper.ProgramALoad => EventCodesATable,
        ScriptHelper.ProgramBMap => EventCodesBTable,
        ScriptHelper.ProgramCTick => EventCodesCTable,
        ScriptHelper.ProgramDTouch => EventCodesDTable,
        ScriptHelper.ProgramEDeactivate => EventCodesETable,
        ScriptHelper.ProgramFInteract => EventCodesFTable,
        _ => Array.Empty<int>(),
    };
}
