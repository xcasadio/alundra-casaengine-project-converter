#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Maps.Loop;

/// <summary>
/// E20.g1d T1 (docs/plan-e20-heros.md section 1.24.8, (o1)): the draw order of the DLL against the keys of the sort driver of the real <c>ALUN_CD.EXE</c> (France), replayed twice in T0 (<c>docs/plan-e20-annexe/e20-g1d/keys/</c>). For each row where the object is
/// in the hands of the hero (the carrier word of the row is set) or in flight after his throw (<c>throw_*</c> scenarios, no carrier): two entities placed at the values of the row, the sort pass, then the key the renderer builds on their rendered translations;
/// the sign of the comparison is the sign of the binary's keys (FRONT: the object's key is above the hero's, it is drawn later). Skipped: the rows where the binary's keys are equal (it does not order two equal keys), the row where the key of the object is 0
/// (the bomb of the tick 1, not in the draw list yet); the tick 162 of <c>bomb_keys2</c> is the written deviation of D-G1D-2: the binary has the held bomb BEHIND for one tick at the take-off of the jump facing up, the DLL has it in front.
/// </summary>
public sealed class CarriedSortKeyOracleTests
{
    private const string Skipped162 = "bomb_keys2";
    private const int Take162 = 162;

    private sealed record Row(string Scenario, int Tick, int HeroPosY, int HeroPosZ, int HeroIdsv, double HeroZ, int ObjectPosY, int ObjectPosZ, int ObjectIdsv, double ObjectZ, uint ObjectFlags, uint Carrier, uint HeroKey, uint ObjectKey, string Sign);

    private static int Raw(string px) => (int)Math.Round(double.Parse(px, CultureInfo.InvariantCulture) * 65536.0);

    private static Row Parse(string line)
    {
        var part = line.Split(" | ");
        var hero = part[2].Split(' ');
        var obj = part[3].Split(' ');
        return new Row(
            part[0], int.Parse(part[1], CultureInfo.InvariantCulture),
            Raw(hero[3]), Raw(hero[4]) - 1, int.Parse(hero[2], CultureInfo.InvariantCulture), double.Parse(hero[4], CultureInfo.InvariantCulture),
            Raw(obj[2]), Raw(obj[3]) - 1, int.Parse(obj[1], CultureInfo.InvariantCulture), double.Parse(obj[3], CultureInfo.InvariantCulture),
            uint.Parse(obj[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture), uint.Parse(obj[6], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            uint.Parse(part[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture), uint.Parse(part[5], NumberStyles.HexNumber, CultureInfo.InvariantCulture), part[8].Trim());
    }

    private static IEnumerable<Row> Rows(string file)
        => File.ReadAllLines(AnnexFiles.DrawOrderKeys(file)).Where(l => l.Contains(" | ") && !l.StartsWith("scenario", StringComparison.Ordinal)).Select(Parse);

    [Theory]
    [InlineData("out_bomb_keys.txt", 119)]
    [InlineData("out_bomb_keys2.txt", 241)]
    [InlineData("out_crate_keys.txt", 239)]
    [InlineData("out_throw_probe.txt", 134)]
    public void TheDrawOrder_OfTheHeldAndOfTheThrownObject_HasTheSignOfTheKeysOfTheBinary(string file, int expectedCompared)
    {
        var compared = 0;
        var failures = new List<string>();
        foreach (var row in Rows(file))
        {
            var carried = row.Carrier != 0;
            var flight = !carried && row.Scenario.StartsWith("throw_", StringComparison.Ordinal);
            if ((!carried && !flight) || row.Sign == "EQUAL" || row.ObjectKey == 0)
            {
                continue;
            }

            var hero = DrawOrderRig.Make("hero", row.HeroPosY, row.HeroPosZ, row.HeroIdsv, flags: 0x0003118C);
            hero.Proxy.IsPlayer = true;
            var obj = DrawOrderRig.Make("object", row.ObjectPosY, row.ObjectPosZ, row.ObjectIdsv, row.ObjectFlags, renderZ: row.ObjectPosZ >> 16);
            if (carried)
            {
                obj.Proxy.PlatformEntity = hero.Entity;
                hero.Proxy.CarriedEntity = obj.Entity;
            }
            else
            {
                obj.Proxy.ThrownBy = hero.Proxy;
                obj.Proxy.IsOnGround = row.ObjectZ < 0.0001 ? 1 : 0;
            }

            DrawOrderRig.Pass(hero, obj);

            var expected = row.Scenario == Skipped162 && row.Tick == Take162 ? "FRONT" : row.Sign;
            var cmp = DrawOrderRig.KeyOf(obj).CompareTo(DrawOrderRig.KeyOf(hero));
            var actual = cmp > 0 ? "FRONT" : cmp < 0 ? "BEHIND" : "EQUAL";
            compared++;
            if (actual != expected)
            {
                failures.Add($"{row.Scenario} tick {row.Tick}: binary {row.Sign} (expected {expected}), DLL {actual}; keys {row.HeroKey:X8} / {row.ObjectKey:X8}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} rows differ from the binary of {compared} compared:{Environment.NewLine}{string.Join(Environment.NewLine, failures.Take(30))}");
        Assert.Equal(expectedCompared, compared);
    }

    [Fact]
    public void TheTick162OfTheJumpFacingUp_IsTheOneRowWhereTheBinaryHasTheHeldBombBehind()
    {
        var row = Rows("out_bomb_keys2.txt").Single(r => r.Scenario == Skipped162 && r.Tick == Take162);

        Assert.Equal("BEHIND", row.Sign);
        Assert.Equal(0x00C90005u, row.HeroKey);
        Assert.Equal(0x00C80025u, row.ObjectKey);
        Assert.Equal(1, Rows("out_bomb_keys2.txt").Count(r => r.Carrier != 0 && r.Sign == "BEHIND"));
    }
}
