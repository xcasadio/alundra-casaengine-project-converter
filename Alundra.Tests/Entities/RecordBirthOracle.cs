#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Entities;

/// <summary>
/// The oracle of E20.e0a4 (docs/plan-e20-heros.md section 1.16.4, <c>docs/plan-e20-annexe/e20-e0a4/oracle/out/</c>) read BY KEY, never pasted: what an entity born from a RECORD by a script (the real record spawner
/// <c>0x8003A1B8</c>) does after its birth tick S. <c>rec_birth_clock.json</c> (rows <c>REC-H2</c>, <c>REC-SW*</c>: births in the programs pass of another entity; keys <c>call_no</c>, <c>end_AC</c>, <c>k</c> = tick - S) and
/// <c>rec_tick.json</c> (row <c>REC-H1</c>, <c>creation_end</c>: the box and the byte <c>animflags_B4</c> at the end of the creation).
/// </summary>
internal static class RecordBirthOracle
{
    private static JsonElement Row(string file, string label)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(BirthClockOracle.Find("docs", "plan-e20-annexe", "e20-e0a4", "oracle", "out", file)));
        return document.RootElement.GetProperty("rows").EnumerateArray().First(r => r.GetProperty("label").GetString() == label).Clone();
    }

    private static IEnumerable<JsonElement> Ticks(string label) => Row("rec_birth_clock.json", label).GetProperty("ticks").EnumerateArray();

    /// <summary>The first tick (relative to S) at which the end-of-animation flag of the row is raised.</summary>
    internal static int FirstEnd(string label) => Ticks(label).First(t => t.GetProperty("end_AC").GetInt32() == 1).GetProperty("k").GetInt32();

    /// <summary>The number of the call of the animation pass (the cursor of the box of the DLL, <c>BoxTicks</c>) at S + <paramref name="k"/>: 0 before the first call.</summary>
    internal static int CallNo(string label, int k) => Ticks(label).First(t => t.GetProperty("k").GetInt32() == k).GetProperty("call_no").GetInt32();

    /// <summary>The box at the end of the creation (<c>0x80039F30</c>): the offset from the entity and the sizes, raw 16.16 (the sizes are <c>(n &lt;&lt; 16) - 1</c>), or null when the oracle says nothing.</summary>
    internal static (long Dx, long Dy, long Dz, long W, long D, long H) CreationBox(string label)
    {
        var t = Row("rec_tick.json", label).GetProperty("creation_end");
        return (t.GetProperty("hbx").GetInt64() - t.GetProperty("px").GetInt64(), t.GetProperty("hby").GetInt64() - t.GetProperty("py").GetInt64(), t.GetProperty("hbz").GetInt64() - t.GetProperty("pz").GetInt64(),
            t.GetProperty("bw").GetInt64(), t.GetProperty("bd").GetInt64(), t.GetProperty("bh").GetInt64());
    }

    /// <summary>The byte <c>+0xB4</c> (<c>AnimFlags</c>) the binary holds at the end of the creation.</summary>
    internal static int CreationAnimFlags(string label) => (int)Row("rec_tick.json", label).GetProperty("creation_end").GetProperty("animflags_B4").GetInt64();
}
