#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>One entity of a row of an E20.c1 hit-oracle trace (<c>docs/plan-e20-annexe/e20-c1/oracle/out/&lt;name&gt;.json</c>): the integer columns of the binary's entity, by name (<c>anim_target</c>,
/// <c>anim_flags_B4</c>, <c>frame_box_1D4</c>, <c>boff_x</c>, <c>bsize_x</c>, <c>atk_attr</c>, <c>c1cc</c>, <c>c1d0</c>, <c>hit_counter</c>, <c>touching_slot</c>...). A null cell reads as -1.</summary>
internal sealed class HitOracleEntity
{
    private readonly Dictionary<string, long> _values = new();

    public HitOracleEntity(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            _values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Number => property.Value.GetInt64(),
                JsonValueKind.True => 1,
                _ => -1,
            };
        }
    }

    public long this[string name] => _values.TryGetValue(name, out var value) ? value : throw new KeyNotFoundException($"the oracle column '{name}' does not exist");

    /// <summary>True when the entity carries a frame box (<c>+0x1D4</c> is not null).</summary>
    public bool HasBox => this["frame_box_1D4"] != 0;

    /// <summary>The slot of the entity this one is touching (<c>+0x224</c>), -1 for none.</summary>
    public int TouchingSlot => (int)this["touching_slot"];
}

/// <summary>One row of a hit-oracle trace: the state AFTER the passes of one tick of the real code of <c>ALUN_CD.EXE</c> (France): the hero, the targets in slot order, the latch of the hero's animation
/// (<c>0x80146DF8</c>), the sparks the stub of <c>0x8003BDD8</c> was asked for (effect 9) and the program letters (slot, letter: 2 = C, 3 = D) the events pass chose.</summary>
internal sealed record HitOracleRow(
    int Tick, uint Hold, uint Latch, HitOracleEntity Hero, IReadOnlyList<HitOracleEntity> Targets, IReadOnlyList<(int Id, int X, int Y, int Z)> Sparks, IReadOnlyList<(int Slot, int Letter)> Handlers)
{
    public HitOracleEntity Entity(int index) => index == 0 ? Hero : Targets[index - 1];
}
