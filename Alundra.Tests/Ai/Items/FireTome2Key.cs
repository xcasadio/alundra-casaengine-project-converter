#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4d1 T1: a world key of the fire tome 2, the rows of the real code of <c>ALUN_CD.EXE</c> in the MIPS machine with the slot index words written and the generator stepped after each frame (R-EL-1, R-EL-2): the FAITHFUL keys of the critic
/// (<c>docs/plan-e20-annexe/e20-h4d1/critic/keys_faithful/</c>, replayed twice by T0). A cast key holds, per tick, the hero (animation asked, item in use, phase, counter, magic), the shake block and the buffer of the pilot (nine words each), the generator at the head
/// of the tick and before its frame-end step, the column of fire (the entity of the sprite 21) and the effects 19 created at that tick; a target key holds, per tick, the targets hit.
/// </summary>
internal sealed class FireTome2Key
{
    private FireTome2Key(string name, IReadOnlyDictionary<int, KeyTick> ticks)
    {
        Name = name;
        Ticks = ticks;
    }

    internal string Name { get; }

    /// <summary>The per-tick words, by tick (1-based).</summary>
    internal IReadOnlyDictionary<int, KeyTick> Ticks { get; }

    internal int LastTick => Ticks.Keys.Max();

    /// <summary>The column of fire as the key sees it at the end of a tick (the words of the entity slot; <c>Z</c> is the binary's, the DLL's plus 1).</summary>
    internal sealed record KeyColumn(int Status, int Flags, int X, int Y, int Z, int AnimationEnd, uint AnimationTarget, int HitCounter);

    /// <summary>One effect created at a tick: <c>CreateEffectEntity(0, effect, animation, x, y, z)</c>.</summary>
    internal sealed record KeyEffect(int Effect, int Animation, int X, int Y, int Z);

    internal sealed record KeyTick(int Tick, uint HeroAnimation, int Item, int Phase, int Counter, int Mp, int[] Block, int[] Buffer, uint Rng0, uint Rng1, KeyColumn? Column, IReadOnlyList<KeyEffect> Effects);

    /// <summary>The key <paramref name="name"/> of <c>critic/keys_faithful/</c>.</summary>
    internal static FireTome2Key Load(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.FireTome2Annex("critic/keys_faithful/" + name + ".json")));
        var ticks = document.RootElement.GetProperty("rows").EnumerateArray().Select(row =>
        {
            KeyColumn? column = null;
            foreach (var entity in row.GetProperty("ents").EnumerateObject())
            {
                var e = entity.Value;
                column = new KeyColumn(e.GetProperty("status").GetInt32(), e.GetProperty("flags").GetInt32(), I(e.GetProperty("x")), I(e.GetProperty("y")), I(e.GetProperty("z")), e.GetProperty("animend").GetInt32(), U(e.GetProperty("anim_t")), e.GetProperty("hitc").GetInt32());
            }

            var effects = new List<KeyEffect>();
            foreach (var entry in row.GetProperty("journal").EnumerateArray())
            {
                if (entry[0].GetString() == "effect_3BDD8")
                {
                    var a = entry[1];
                    effects.Add(new KeyEffect(a[1].GetInt32(), a[2].GetInt32(), I(a[3]), I(a[4]), I(a[5])));
                }
            }

            return new KeyTick(row.GetProperty("t").GetInt32(), U(row.GetProperty("hero_anim_t")), row.GetProperty("inuse").GetInt32(), row.GetProperty("phase").GetInt32(), row.GetProperty("counter").GetInt32(), row.GetProperty("mp").GetInt32(),
                row.GetProperty("block").EnumerateArray().Select(I).ToArray(), row.GetProperty("buf").EnumerateArray().Select(I).ToArray(), U(row.GetProperty("rng0")), U(row.GetProperty("rng1")), column, effects);
        }).ToDictionary(k => k.Tick);
        return new FireTome2Key(name, ticks);
    }

    private static int I(JsonElement element) => unchecked((int)element.GetInt64());

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());

    /// <summary>The key of the targets: per tick the generator, and for each target (by its place in the list) the ticks at which the column hit it.</summary>
    internal sealed class TargetKey
    {
        private TargetKey(string name, IReadOnlyList<int[]> targets, IReadOnlyDictionary<int, (uint Rng0, uint Rng1)> generator, IReadOnlyDictionary<int, IReadOnlyList<int>> hits)
        {
            Name = name;
            Targets = targets;
            Generator = generator;
            Hits = hits;
        }

        internal string Name { get; }

        /// <summary>The pixels (X, Y) of the targets (blocks of the sprite 25 of the class A, as the DLL's bench puts them).</summary>
        internal IReadOnlyList<int[]> Targets { get; }

        internal IReadOnlyDictionary<int, (uint Rng0, uint Rng1)> Generator { get; }

        /// <summary>The ticks at which each target was hit, by index (a target never hit has no entry).</summary>
        internal IReadOnlyDictionary<int, IReadOnlyList<int>> Hits { get; }

        internal int LastTick => Generator.Keys.Max();

        internal static TargetKey Load(string name)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.FireTome2Annex("critic/keys_faithful/" + name + ".json")));
            var root = document.RootElement;
            var targets = root.GetProperty("targets").EnumerateArray().Select(t => new[] { t[0].GetInt32(), t[1].GetInt32() }).ToList();
            var generator = new Dictionary<int, (uint, uint)>();
            var hits = new Dictionary<int, List<int>>();
            foreach (var row in root.GetProperty("log").EnumerateArray())
            {
                var tick = row.GetProperty("t").GetInt32();
                generator[tick] = (U(row.GetProperty("rng0")), U(row.GetProperty("rng1")));
                foreach (var target in row.GetProperty("hit_targets").EnumerateArray())
                {
                    if (!hits.TryGetValue(target.GetInt32(), out var list))
                    {
                        hits[target.GetInt32()] = list = new List<int>();
                    }

                    list.Add(tick);
                }
            }

            return new TargetKey(name, targets, generator, hits.ToDictionary(h => h.Key, h => (IReadOnlyList<int>)h.Value));
        }
    }
}
