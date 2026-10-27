#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Items;

/// <summary>
/// E20.h4c1 T1: the vectors of the oracle of the earth tome 1 (the real code of <c>ALUN_CD.EXE</c> in the MIPS machine, replayed in T0 from copies): the 689 vectors of the driver <c>0x80035320</c> reached through the wrapper <c>0x8003634C</c>
/// (<c>disc/h4copy/vec/vec_driver_2B.json</c>), the 384 of the base directions 4, 12, 20 and 28 (<c>t0/vec/vec_driver_2B_dirs.json</c>) and the 224 of E3 <c>0x8007EDA0</c> (<c>vec_e3.json</c>).
/// </summary>
internal static class EarthTome1Vectors
{
    /// <summary>One vector of the driver: the input words of the hero and the item, and what the real code left (the wrapper increments the counter and frees the item when the driver answers non-zero).</summary>
    internal sealed record Driver(int Phase, int Counter, uint HeroAnim, uint HeroDirection, int HeroX, int HeroY, int HeroZ, int Slope, bool SpawnReturnsEntity,
        int Return, int PhaseAfter, int CounterAfter, uint ItemAfter, uint HeroAnimAfter, IReadOnlyList<Spawn> Spawns, IReadOnlyList<int> ForceZWritten);

    internal sealed record Spawn(int Type, int X, int Y, int Z, int Direction);

    internal sealed record Impact(uint TargetAnim, int AnimEnd, uint Flags, uint TargetAnimAfter, uint FlagsAfter, int AnimEndAfter, bool Destroyed);

    internal static IReadOnlyList<Driver> Drivers(string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome1Annex(relative)));
        return document.RootElement.EnumerateArray().Select(v =>
        {
            var i = v.GetProperty("inp");
            var o = v.GetProperty("out");
            return new Driver(
                i.GetProperty("phase").GetInt32(), i.GetProperty("counter").GetInt32(), U(i.GetProperty("hero_anim")), U(i.GetProperty("hero_dir")), i.GetProperty("hero_x").GetInt32(), i.GetProperty("hero_y").GetInt32(),
                i.GetProperty("hero_z").GetInt32(), i.GetProperty("slope").GetInt32(), i.GetProperty("spawn_returns_entity").GetBoolean(),
                o.GetProperty("ret").GetInt32(), o.GetProperty("phase").GetInt32(), o.GetProperty("counter").GetInt32(), U(o.GetProperty("inuse")), U(o.GetProperty("hero_anim")),
                o.GetProperty("spawns").EnumerateArray().Select(s => new Spawn(s.GetProperty("type").GetInt32(), (int)(s.GetProperty("x").GetDouble() * 65536), (int)(s.GetProperty("y").GetDouble() * 65536), s.GetProperty("z").GetInt32(), s.GetProperty("dir").GetInt32())).ToList(),
                o.GetProperty("force_z_written").EnumerateArray().Select(f => f.GetInt32()).ToList());
        }).ToList();
    }

    internal static IReadOnlyList<Impact> Impacts()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(AnnexFiles.EarthTome1Annex("disc/h4copy/vec/vec_e3.json")));
        return document.RootElement.EnumerateArray().Select(v =>
        {
            var i = v.GetProperty("inp");
            var o = v.GetProperty("out");
            return new Impact(U(i.GetProperty("target_anim")), i.GetProperty("anim_end").GetInt32(), U(i.GetProperty("flags")), U(o.GetProperty("target_anim")), U(o.GetProperty("flags")), o.GetProperty("anim_end").GetInt32(), o.GetProperty("destroy").GetArrayLength() > 0);
        }).ToList();
    }

    private static uint U(JsonElement element) => unchecked((uint)element.GetInt64());
}
