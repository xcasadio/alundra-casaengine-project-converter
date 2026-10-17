#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Entities;

namespace Alundra.Tests;

/// <summary>
/// E20.f1b B3 (docs/plan-e20-heros.md section 1.9, D-E20-29, annex <c>docs/plan-e20-annexe/e20-f1/depth-key.md</c>): the depth key <c>+0x1BC</c> of an entity as an effect anchor, <c>((PosY + (IDSV &lt;&lt; 16)) &amp; 0xFFFF0000) +
/// (short)(PosZ &gt;&gt; 16)</c>. By hand first, then against the oracle (<c>keyprobe.txt</c>: the real sort driver <c>0x800399B8</c> run after each tick of C2-steel), then on the real hero of the world through its anchor.
/// </summary>
public sealed class AlundraEntityDepthKeyTests
{
    [Theory]
    // PosY (16.16), IDSV, PosZ (16.16, binary frame), expected key: written by hand from the formula.
    [InlineData(100 << 16, 5, 1, 0x00690000u)]              // the hero of the oracle at rest: (100 + 5) << 16 = 0x690000, the integer Z is 0.
    [InlineData(100 << 16, 43, 5 << 16, 0x008F0005u)]       // IDSV 43: (100 + 43) << 16 = 0x8F0000, plus the integer Z of 5.
    [InlineData(100 << 16, 43, -3 << 16, 0x008EFFFDu)]      // a negative integer Z is added as a signed short: 0x8F0000 - 3.
    [InlineData((100 << 16) + 0xFFFF, 0, 0, 0x00640000u)]   // the fraction of PosY never survives the mask.
    [InlineData(100 << 16, 0, (7 << 16) + 0xFFFF, 0x00640007u)] // nor the fraction of PosZ: only its high half.
    [InlineData(100 << 16, 0, 0x7FFF << 16, 0x00647FFFu)]   // the largest short.
    [InlineData(100 << 16, 0, unchecked((int)0x80000000), 0x0063_8000u)] // the smallest short: 0x640000 - 0x8000.
    public void Compute_IsTheFormulaOfTheSortDriver_ByHand(int posY, int idsv, int posZ, uint expected)
        => Assert.Equal(expected, AlundraEntityDepthKey.Compute(posY, idsv, posZ));

    private static readonly Regex Row = new(@"^t\s*(\d+) anim88=([0-9A-F]+) idsv=(-?\d+) y=(-?\d+) z=(-?\d+) key=([0-9A-F]{8}) formula=([0-9A-F]{8}) (OK|DIFF)$", RegexOptions.Compiled);

    private static List<(int Tick, int Idsv, int Y, int Z, uint Key)> KeyProbe()
    {
        var file = Path.Combine(ChargeOracle.Folder(), "keyprobe.txt");
        var rows = new List<(int, int, int, int, uint)>();
        foreach (var line in File.ReadAllLines(file))
        {
            var m = Row.Match(line);
            Assert.True(m.Success, $"a row of keyprobe.txt does not parse: {line}");
            Assert.Equal("OK", m.Groups[8].Value); // the oracle's own check: the sort driver equals the formula.
            rows.Add((int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
                uint.Parse(m.Groups[6].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    [Fact]
    public void Compute_EqualsTheSortDriverOfTheBinary_OnThe150TicksOfC2Steel()
    {
        var rows = KeyProbe();

        Assert.Equal(150, rows.Count);
        foreach (var row in rows)
        {
            Assert.True(row.Key == AlundraEntityDepthKey.Compute(row.Y, row.Idsv, row.Z), $"tick {row.Tick}: oracle {row.Key:X8}, Compute {AlundraEntityDepthKey.Compute(row.Y, row.Idsv, row.Z):X8}");
        }

        Assert.All(rows.Where(r => r.Tick is >= 22 and <= 91), r => Assert.Equal(0x00690000u, r.Key)); // the charge: the swing is over, the hero idles with the IDSV 5.
    }
}
