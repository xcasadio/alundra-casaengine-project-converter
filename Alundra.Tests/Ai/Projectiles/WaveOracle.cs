#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Ai.Projectiles;
using Alundra.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests;

/// <summary>
/// E20.f3c (docs/plan-e20-heros.md section 1.19.3): the oracle of the sword wave read BY KEY from <c>docs/plan-e20-annexe/e20-f3/oracle/out</c> (never copied), and the run of the wave in the real world of map 14
/// (<see cref="HitArena"/>). The arena releases the charge at tick 91 (the oracle at 96): an oracle tick minus <see cref="ArenaOffset"/> is an arena tick, and the wave is born at <c>S = R + 21 = 112</c>.
/// </summary>
internal static class WaveOracle
{
    /// <summary>An oracle tick minus this is an arena tick.</summary>
    internal const int ArenaOffset = 5;

    /// <summary>The arena's tick of the first apparition of a wave (release at 91 + 21).</summary>
    internal const int Birth = 112;

    /// <summary>The first and the last tick of the arena that the rows of <c>wave_*.json</c> cover (the oracle's 100 to 150).</summary>
    internal const int FirstTick = 95;
    internal const int LastTick = 145;

    internal sealed record Row(int Tick, JsonElement Hero, JsonElement? Wave);

    internal static string Path(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-f3", "oracle", "out", file);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-f3/oracle/out/{file}' found above '{AppContext.BaseDirectory}'");
    }

    private static JsonElement Json(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path(file)));
        return document.RootElement.Clone();
    }

    /// <summary>The rows of <c>wave_&lt;sword&gt;_&lt;direction&gt;.json</c>: the hero (slot 0) and the wave (slot 1, null while there is none), by ARENA tick.</summary>
    internal static IReadOnlyDictionary<int, Row> Rows(string sword, string direction)
    {
        var root = Json($"wave_{sword}_{direction}.json");
        var rows = new Dictionary<int, Row>();
        foreach (var row in root.GetProperty("rows").EnumerateArray())
        {
            var slots = row.GetProperty("slots");
            var wave = slots.TryGetProperty("1", out var one) && one.ValueKind == JsonValueKind.Object ? one : (JsonElement?)null;
            var tick = row.GetProperty("tick").GetInt32() - ArenaOffset;
            rows[tick] = new Row(tick, slots.GetProperty("0"), wave);
        }

        return rows;
    }

    /// <summary>The line of the wave at an arena tick as the DLL describes it, from the keys of the oracle (the DLL's frame of Z is the oracle's minus one raw unit, ADR-0026), or "absent".</summary>
    internal static string Describe(Row row)
    {
        if (row.Wave is not { } w)
        {
            return "absent";
        }

        int I(string key) => w.GetProperty(key).GetInt32();
        int HeroI(string key) => row.Hero.GetProperty(key).GetInt32();
        return $"type={I("type68")} flags=0x{I("flags6C"):X} atk=({I("atk_attr")},{I("atk_pow")}) loot={I("loot3c")} status={I("status")} cnt={I("cnt1c")} ac={I("reset_AC")} "
            + $"d=({I("px") - HeroI("px")},{I("py") - HeroI("py")}) z={I("pz") - 1} f=({I("fx")},{I("fy")},{I("fz")}) adj={I("adj_13C")} ground={I("ground_144")} "
            + $"hb=({I("hbx") - I("px")},{I("hby") - I("py")},{I("hbz") - I("pz")}) size=({I("bw")},{I("bd")},{I("bh")})";
    }

    /// <summary>The same line from the DLL's entity (the box offsets and sizes of the frame box evaluation, the oracle's hit box minus the position and its <c>+0x1D8</c> words), or "absent".</summary>
    internal static string Describe(EntityScriptProxy? wave, EntityScriptProxy hero, bool present)
    {
        if (wave is null || !present)
        {
            return "absent";
        }

        wave.EvaluateFrameBox(); // the frame box of the tick, as the hit pass evaluates it for an attacker (the fields keep their last values on a frame without a box, as the oracle's words do).
        return $"type={wave.SpriteType} flags=0x{wave.Flags:X} atk=({wave.CurrentAttack?.Attribute ?? -1},{wave.CurrentAttack?.Power ?? -1}) loot={wave.ContentsItemId} status={(int)wave.Status} cnt={wave.FrameCounter} ac={wave.ForceResetAnimationFlag} "
            + $"d=({wave.PosX - hero.PosX},{wave.PosY - hero.PosY}) z={wave.PosZ} f=({wave.ForceX},{wave.ForceY},{wave.ForceZ}) adj={wave.ForceAdjusted} ground={wave.IsOnGround} "
            + $"hb=({wave.CollisionOffsetX},{wave.CollisionOffsetY},{wave.CollisionOffsetZ}) size=({wave.CollisionWidth},{wave.CollisionDepth},{wave.CollisionHeight})";
    }

    /// <summary>The ARENA ticks at which the wave called the stub of the cutting of the grass (<c>wave_grass2.txt</c>, the line "wave (slot 1) calls from ticks", east).</summary>
    internal static IReadOnlyList<int> GrassCallTicks()
    {
        var line = File.ReadLines(Path("wave_grass2.txt")).First(l => l.StartsWith("  wave (slot 1) calls from ticks:", StringComparison.Ordinal));
        return Regex.Matches(line.Substring(line.IndexOf('[')), @"\d+").Select(m => int.Parse(m.Value) - ArenaOffset).ToList();
    }

    /// <summary>The first hit of an east target at the relative X <paramref name="relX"/> in the sweep of the hero and the wave (<c>wave_reach_sweep.txt</c>): the ARENA tick and the attacker ("hero" or "wave"), or null.</summary>
    internal static (int Tick, string Attacker)? ReachFirstHit(int relX)
    {
        var pattern = new Regex(@"^east\s+target px=\(\s*\d+,\s*\d+\) rel=\(\s*" + Regex.Escape(relX.ToString("+0;-0")) + @",\s*-4\) hits \(tick,attacker slot\)=\[(?<hits>.*)\]$");
        foreach (var line in File.ReadLines(Path("wave_reach_sweep.txt")))
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var hit = Regex.Match(match.Groups["hits"].Value, @"\((?<t>\d+), '(?<a>\w+)'\)");
            return hit.Success ? (int.Parse(hit.Groups["t"].Value) - ArenaOffset, hit.Groups["a"].Value) : null;
        }

        throw new InvalidOperationException($"no east row with rel x {relX} in wave_reach_sweep.txt");
    }

    /// <summary>The contacts of the target of the charge arena in <c>arena_probe.json</c>, run <paramref name="run"/> ("control" or "E10_removed"): ARENA tick and attacker.</summary>
    internal static IReadOnlyList<(int Tick, string Attacker)> ArenaContacts(string run)
    {
        var root = Json("arena_probe.json");
        var found = root.GetProperty("runs").EnumerateArray().First(r => r.GetProperty("name").GetString() == run);
        return found.GetProperty("hits").EnumerateArray().Select(h => (h.GetProperty("tick").GetInt32() - ArenaOffset, h.GetProperty("attacker").GetString()!)).OrderBy(h => h.Item1).ToList();
    }
}

/// <summary>What one run of the wave in the arena leaves: the line of the wave at every tick from <see cref="WaveOracle.FirstTick"/> to <see cref="WaveOracle.LastTick"/>, the entity once seen, the table of slots.</summary>
internal sealed class WaveRun : IDisposable
{
    private const uint Square = PadState.Square;

    internal readonly HitArena Arena;
    internal readonly Dictionary<int, string> Lines = new();
    internal EntityScriptProxy? Born;
    internal int FirstTick;
    internal int LowestFreeSlotBeforeBirth = -1;
    internal readonly List<(int Tick, EntityScriptProxy Wave)> Present = new();

    /// <summary>The arena of the charge: the hero at (212, <paramref name="heroY"/>) facing <paramref name="direction"/> (the word of <c>TargetDirection</c>: 24 east, 8 west, 16 north, 0 south) with the sword of the item <paramref name="item"/>
    /// (2 steel, 3 demon, 4 holy) in the slot 1; the Beannoids at the pixels <paramref name="targets"/>.</summary>
    internal WaveRun(string name, int item, uint direction, IReadOnlyList<int[]>? targets = null, int heroY = 96)
    {
        Arena = new HitArena("f3c-" + name, 212, heroY, targets?.ToList() ?? new List<int[]>(), direction);
        Arena.Arc.Give(item);
        Arena.Arc.Equip(1);
    }

    /// <summary>The entity of the wave if one exists in the world now: a sprite 27 to 29 whose parent is the hero.</summary>
    internal EntityScriptProxy? Find()
        => Arena.Arc.Entities.FirstOrDefault(e => WaveSwordAi.SpriteTypes.Contains(e.SpriteType) && ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity));

    /// <summary>
    /// Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> (the Square held to tick 90, released at 91). With <paramref name="useHook"/> the shot is born by the test hook, from the exact place of
    /// <c>MovePlayer</c>; without it, by the production seam. <paramref name="afterTick"/> is told every tick after its inventory pass.
    /// </summary>
    internal void Go(int ticksPerFrame, bool useHook, int lastTick = WaveOracle.LastTick, Action<int>? afterTick = null)
    {
        if (useHook)
        {
            PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
                ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
        }

        Arena.AfterTick = tick =>
        {
            if (Born is null)
            {
                var lowest = Enumerable.Range(0, EntitySlotTable.SlotCount).FirstOrDefault(i => !Arena.Arc.Proxy.SlotTable.IsHeld(i), -1);
                Born = Find();
                if (Born is not null)
                {
                    FirstTick = tick;
                }
                else
                {
                    LowestFreeSlotBeforeBirth = lowest;
                }
            }

            var present = Born is not null && Arena.Arc.Entities.Contains(Born);
            if (present)
            {
                Present.Add((tick, Born!));
            }

            if (tick is >= WaveOracle.FirstTick and <= WaveOracle.LastTick)
            {
                Lines[tick] = WaveOracle.Describe(Born, Arena.Hero, present);
            }

            afterTick?.Invoke(tick);
        };

        Arena.Begin();
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(tick <= 90 ? Square : 0, ticksPerFrame);
        }
    }

    public void Dispose() => Arena.Dispose();
}
