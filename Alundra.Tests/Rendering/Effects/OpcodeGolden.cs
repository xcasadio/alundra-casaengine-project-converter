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

/// <summary>The values of <c>docs/plan-e19-g1g3-annexe/g3b-golden.tsv</c> (generated, committed before the code): the nine effect opcodes run on the real operands of the real programs.</summary>
internal static class OpcodeGolden
{
    internal static readonly string[][] Rows = Read();

    internal static IEnumerable<string[]> Of(string tag) => Rows.Where(r => r[0] == tag);

    internal static IEnumerable<string[]> Of(string tag, string key) => Rows.Where(r => r[0] == tag && r[1] == key);

    internal static string[] One(string tag, string key) => Of(tag, key).Single();

    internal static long[] Numbers(string[] row, int from) => row.Skip(from).Select(long.Parse).ToArray();

    /// <summary>A row <c>tag slot record x y z</c> as (slot, record, x, y, z).</summary>
    internal static (int Index, int Record, int X, int Y, int Z) Slot(string[] row) =>
        ((int)long.Parse(row[1]), (int)long.Parse(row[2]), (int)long.Parse(row[3]), (int)long.Parse(row[4]), (int)long.Parse(row[5]));

    private static string[][] Read()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e19-g1g3-annexe", "g3b-golden.tsv");
            if (File.Exists(candidate))
            {
                return File.ReadAllLines(candidate).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t')).ToArray();
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"OpcodeGolden: no 'docs/plan-e19-g1g3-annexe/g3b-golden.tsv' found above '{AppContext.BaseDirectory}'.");
    }
}
