#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Rendering.Effects;
using Alundra.Transitions;
using Alundra.Hero;
using Alundra.Tests.Support;
using Alundra.Tests.Maps;

namespace Alundra.Tests.Rendering.Effects;

/// <summary>
/// E19.g G3b: what every arc that runs effect opcodes checks at the end of each frame, on the real operands of the real program of its map: a <c>0x90</c> or <c>0xA2</c> executed in the frame leaves
/// a slot of its record, a <c>0x92</c> leaves the animation of its operand on the slots of its record (the effect operations are applied at the end of the tick that emitted them, so what an
/// instruction wrote is read at the end of its frame). Attach it after the arc's own hooks: it chains them.
/// </summary>
internal sealed class EffectArcChecks
{
    private static readonly HashSet<int> EffectOpcodes = new() { 0x90, 0x91, 0x92, 0x93, 0x94, 0xA0, 0xA1, 0xA2, 0xA3 };
    private readonly ArcRun _arc;
    private readonly int[] _codes;
    private readonly List<ArcInstruction> _thisFrame = new();

    /// <summary>Every effect instruction the arc executed, in order.</summary>
    public List<ArcInstruction> Sites { get; } = new();

    public List<string> Violations { get; } = new();

    /// <summary>Called at the end of every frame with the frame index, after the checks.</summary>
    public Action<EffectStage, int>? PerFrame { get; set; }

    private EffectArcChecks(ArcRun arc, int[] codes)
    {
        _arc = arc;
        _codes = codes;
    }

    public EffectStage Stage => _arc.Proxy._effectStage;

    public static EffectArcChecks Attach(ArcRun arc, int map)
    {
        var checks = new EffectArcChecks(arc, EffectOpcodeTests.Codes(map));
        var onInstruction = arc.OnInstruction;
        var onFrame = arc.OnFrame;
        arc.OnInstruction = t =>
        {
            onInstruction?.Invoke(t);
            if (EffectOpcodes.Contains(t.Opcode) && t.Kind is EventTraceKind.Implemented)
            {
                checks._thisFrame.Add(t);
                checks.Sites.Add(t);
            }
        };
        arc.OnFrame = () =>
        {
            onFrame?.Invoke();
            checks.EndOfFrame(arc.Frame - 1);
        };
        return checks;
    }

    public bool Has(int pc) => Sites.Any(t => t.Pc == pc);

    private void EndOfFrame(int frame)
    {
        var exists = new HashSet<int>();
        var animation = new Dictionary<int, int>();
        foreach (var t in _thisFrame)
        {
            var record = _codes[t.Pc + 1];
            switch (t.Opcode)
            {
                case 0x90 or 0xA2:
                    exists.Add(record);
                    animation.Remove(record); // a slot created after the 0x92 carries the animation of its record
                    break;
                case 0x91:
                    exists.Remove(record);
                    animation.Remove(record);
                    break;
                case 0x92:
                    animation[record] = _codes[t.Pc + 2];
                    break;
            }
        }

        _thisFrame.Clear();
        var slots = Stage.Reservoir.ActiveSlots.ToList();
        foreach (var record in exists)
        {
            if (slots.All(s => s.RecordIndex != record) && slots.Count < EffectReservoir.SlotCount)
            {
                Violations.Add($"frame {frame}: no slot of the record {record} after its creation");
            }
        }

        foreach (var (record, expected) in animation)
        {
            foreach (var slot in slots.Where(s => s.RecordIndex == record && s.TargetAnimation != expected))
            {
                Violations.Add($"frame {frame}: the slot {slot.Index} of the record {record} aims the animation {slot.TargetAnimation}, 0x92 asked {expected}");
            }
        }

        PerFrame?.Invoke(Stage, frame);
    }

    public void AssertClean()
    {
        Assert.True(Violations.Count == 0, "effect instructions without their effect: " + string.Join("; ", Violations.Take(5)));
    }
}
