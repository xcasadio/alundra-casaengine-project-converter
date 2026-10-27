#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c2 T1: a world key of the earth tome 2, the rows of the real code of <c>ALUN_CD.EXE</c> in the MIPS machine with the slot index words written (<c>docs/plan-e20-annexe/e20-h4c2/disc/keys/</c>, the mid-map and plateau keys of the critic in
/// <c>critic/keys/</c>, the four keys of the corrected game of D-H4C2-4 in <c>rockfix/</c>, which are INDICATIVE, not faithful to the byte). A cast key holds, per rock (by the order of the births), its track tick by tick, and per tick the generator, the camera kick,
/// the hero and the item; the sounds are (tick, id) pairs.
/// </summary>
internal sealed class EarthTome2Key
{
    private EarthTome2Key(string name, IReadOnlyList<KeyRock> rocks, IReadOnlyDictionary<int, KeyTick> ticks, IReadOnlyList<(int Tick, int Sfx)> sounds)
    {
        Name = name;
        Rocks = rocks;
        Ticks = ticks;
        Sounds = sounds;
    }

    internal string Name { get; }

    /// <summary>The rocks in the order of their births (tick, then slot).</summary>
    internal IReadOnlyList<KeyRock> Rocks { get; }

    /// <summary>The per-tick words, by tick (1-based).</summary>
    internal IReadOnlyDictionary<int, KeyTick> Ticks { get; }

    internal IReadOnlyList<(int Tick, int Sfx)> Sounds { get; }

    internal int LastTick => Ticks.Keys.Max();

    /// <summary>One row of the track of a rock at the end of a tick (the words of the entity slot of the machine; <c>Z</c> is the binary's, the DLL's plus 1).</summary>
    internal sealed record Row(int Tick, int Status, uint TargetAnimation, int X, int Y, int Z, int AnimationEnd, int Adjusted, int Ground, uint Flags);

    internal sealed record KeyRock(int Slot, int Id, int Birth, int R1, int R2, int Direction, int Terrain, IReadOnlyList<Row> Track)
    {
        internal Row? At(int tick) => Track.FirstOrDefault(r => r.Tick == tick);

        internal int LastSeen => Track[^1].Tick;

        /// <summary>The first tick the rock is at the status 3, 0 if never (the key's <c>st3</c>).</summary>
        internal int Status3 => Track.Where(r => r.Status == 3).Select(r => r.Tick).FirstOrDefault();

        internal int Status4 => Track.Where(r => r.Status == 4).Select(r => r.Tick).FirstOrDefault();

        /// <summary>The first tick whose animation asked is 1 (the tick of E13).</summary>
        internal int Impact => Track.Where(r => r.TargetAnimation == 1).Select(r => r.Tick).FirstOrDefault();
    }

    /// <summary>The words of a tick: the generator at the head (<c>Rng0</c>) and before the end of the frame's own step (<c>Rng1</c>), the camera kick, the hero (animation asked, Z), the item in use with its phase and counter, the magic.</summary>
    internal sealed record KeyTick(int Tick, uint Rng0, uint Rng1, int Kick, uint HeroAnimation, int HeroZ, int Item, int Phase, int Counter, int Mp);

    /// <summary>The key <paramref name="name"/> of the folder <paramref name="folder"/> (relative to the annex of h4c2).</summary>
    internal static EarthTome2Key Load(string folder, string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome2Annex(folder + "/" + name + ".json")));
        var root = document.RootElement;
        var rocks = root.GetProperty("rows").EnumerateArray().Select(r => new KeyRock(
            r.GetProperty("slot").GetInt32(), r.GetProperty("id").GetInt32(), r.GetProperty("birth").GetInt32(), r.GetProperty("r1").GetInt32(), r.GetProperty("r2").GetInt32(), r.GetProperty("dir").GetInt32(), r.GetProperty("t138").GetInt32(),
            r.GetProperty("track").EnumerateArray().Select(t => new Row(t[0].GetInt32(), t[1].GetInt32(), U(t[2]), t[3].GetInt32(), t[4].GetInt32(), t[5].GetInt32(), t[6].GetInt32(), t[7].GetInt32(), t[8].GetInt32(), U(t[9]))).ToList()))
            .OrderBy(r => r.Birth).ThenBy(r => r.Slot).ToList();
        var ticks = root.GetProperty("per_tick").EnumerateArray().Select(p => new KeyTick(
            p.GetProperty("t").GetInt32(), U(p.GetProperty("rng0")), U(p.GetProperty("rng1")), p.GetProperty("kick").GetInt32(), U(p.GetProperty("hero_ta")), p.GetProperty("hero_z").GetInt32(),
            p.GetProperty("item").GetInt32(), p.GetProperty("phase").GetInt32(), p.GetProperty("counter").GetInt32(), p.GetProperty("mp").GetInt32())).ToDictionary(k => k.Tick);
        var sounds = root.GetProperty("sounds").EnumerateArray().Select(s => (s[0].GetInt32(), s[1].GetInt32())).ToList();
        return new EarthTome2Key(name, rocks, ticks, sounds);
    }

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());

    /// <summary>The key of an interruption: from the tick before it (T - 1) on, per tick, the hero, the item and the rocks by slot.</summary>
    internal sealed class CancelKey
    {
        private CancelKey(string name, IReadOnlyDictionary<int, CancelTick> ticks, IReadOnlyList<(int Tick, int Sfx)> sounds)
        {
            Name = name;
            Ticks = ticks;
            Sounds = sounds;
        }

        internal string Name { get; }

        internal IReadOnlyDictionary<int, CancelTick> Ticks { get; }

        /// <summary>The sounds from the tick T - 2 to the end, without the sound 0 the oracle's journal also lists (a silent stop of a track): 55, 45 and the hero's 23.</summary>
        internal IReadOnlyList<(int Tick, int Sfx)> Sounds { get; }

        internal int FirstTick => Ticks.Keys.Min();

        internal int LastTick => Ticks.Keys.Max();

        /// <summary>A rock of a row: the status (3 deactivated, 2 normal), the animation asked, the Z (binary), the end of the animation, the flags and whether another entity blocks it (<c>+0x20</c>).</summary>
        internal sealed record CancelRock(int Slot, int Id, int Status, uint TargetAnimation, int Z, int AnimationEnd, uint Flags, bool Blocked);

        internal sealed record CancelTick(int Tick, int Item, int Phase, uint HeroAnimation, int Kick, IReadOnlyDictionary<int, CancelRock> Rocks);

        /// <param name="folder">Where the json is (relative to the annex of h4c2).</param>
        /// <param name="name">The json, without the extension.</param>
        /// <param name="soundsFolder">Where the sound journal is, and its file name with the extension.</param>
        internal static CancelKey Load(string folder, string name, string soundsFolder, string soundsFile)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome2Annex(folder + "/" + name + ".json")));
            var ticks = document.RootElement.EnumerateArray().Select(row => new CancelTick(
                row.GetProperty("t").GetInt32(), row.GetProperty("item").GetInt32(), row.GetProperty("phase").GetInt32(), U(row.GetProperty("hero_ta")), row.GetProperty("kick").GetInt32(),
                row.GetProperty("ents").EnumerateArray().ToDictionary(e => e.GetProperty("slot").GetInt32(), e => new CancelRock(
                    e.GetProperty("slot").GetInt32(), e.GetProperty("id").GetInt32(), e.GetProperty("st").GetInt32(), U(e.GetProperty("ta")), (int)Math.Round(e.GetProperty("z").GetDouble() * 65536.0), e.GetProperty("ae").GetInt32(),
                    U(e.GetProperty("fl")), e.GetProperty("b20").GetInt32() != 0)))).ToDictionary(t => t.Tick);

            // "all sounds from t_hit-2 to end: [(98, 55), (99, 45), (100, 23), ...]"
            var line = File.ReadAllLines(AnnexFiles.EarthTome2Annex(soundsFolder + "/" + soundsFile)).Single(l => l.StartsWith("all sounds from", StringComparison.Ordinal));
            var sounds = Regex.Matches(line, @"\((\d+), (\d+)\)").Select(m => (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))).Where(s => s.Item2 != 0).ToList();
            return new CancelKey(name, ticks, sounds);
        }
    }
}
