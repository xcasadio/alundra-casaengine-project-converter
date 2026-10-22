#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>One row of an E20.f1a charge probe (<c>docs/plan-e20-annexe/e20-f1/oracle/out/C*.txt|json</c> and <c>out_events/</c>): the state AFTER one tick of the real <c>MovePlayer</c> of <c>ALUN_CD.EXE</c> (France).</summary>
/// <param name="Tick">The 1-based tick.</param>
/// <param name="Hold">The pad word held on that tick.</param>
/// <param name="Charge">The charge counter <c>0x80126FFC</c> after the tick.</param>
/// <param name="FrameCounterIn">The counter <c>+0x1C</c> as <c>MovePlayer</c> saw it (before the counters pass of the tick).</param>
/// <param name="RngBefore">The generator word at the head of the tick (the oracle never advances it per frame).</param>
/// <param name="RngAfter">The generator word after the passes of the tick.</param>
/// <param name="AuraHandle">The handle of the aura (<c>0x80126FF8</c>) after the tick, 0 when none.</param>
/// <param name="Events">The ordered events of the tick: <c>c&lt;id&gt;</c> a creation of effect id, <c>s&lt;id&gt;</c> a sound (id 0: "no sound"), <c>x:spawn</c> a shot.</param>
/// <param name="Calls">The creations of the tick with their fields, in order (the aura's included).</param>
internal sealed record ChargeRow(
    int Tick, uint Hold, uint AnimTarget, uint AnimCurrent, int Charge, long RecPtr, int RecCnt, int FrameCounterIn, uint RngBefore, uint RngAfter, long AuraHandle,
    IReadOnlyList<string> Events, IReadOnlyList<ChargeCall> Calls, IReadOnlyList<ChargeSpawn> Spawns)
{
    /// <summary>The sounds asked on the tick, in the binary's order; a call with the id 0 is the function with "no sound", never a sound.</summary>
    public IEnumerable<int> Sounds => Events.Where(e => e[0] == 's').Select(e => int.Parse(e[1..], CultureInfo.InvariantCulture)).Where(id => id > 0);

    /// <summary>The creations of the effect 16 (the hold spark and the burst) that succeeded, in order: what the DLL creates (the aura, effect 3, is the stub of E20.f1a).</summary>
    public IEnumerable<ChargeCall> ChargeEffects => Calls.Where(c => c.Effect == NativeEffects.ChargeEffect && c.Created);

    /// <summary>The hold sparks of the tick (the creations of the effect 16 before the last 16 when there are 16 or more, as <c>probe_ext.check_effects</c> splits them).</summary>
    public IEnumerable<ChargeCall> Sparks
    {
        get
        {
            var all = Calls.Where(c => c.Effect == NativeEffects.ChargeEffect).ToList();
            return all.Count >= 16 ? all.Take(all.Count - 16) : all;
        }
    }
}
