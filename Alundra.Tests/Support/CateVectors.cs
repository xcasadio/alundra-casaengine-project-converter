#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e T1: the 4 000 vectors of <c>CheckAndTriggerTileEffect</c> (<c>0x8003A7B0</c>) that T0 made by running the real instructions of the binary on a synthetic memory
/// (<c>docs/plan-e20-annexe/e20-e/rev3/vectors/cate-vectors.json</c>, <c>oracle/export_vectors.py</c>). Words are the raw 32-bit values of the entity fields.
/// </summary>
internal sealed class CateVectors
{
    public sealed record Cell(int Column, int Row, int Flags, int Height, int Wall, int FlagsAfter, int WallAfter);

    public sealed record Call(string Kind, int Flag, int Id, int Anim, int X, int Y, int Z, int Code, int ModelItem, int ModelDraws, uint StateBefore, uint StateAfter);

    public sealed record Vector(int Id, bool Frame, int BoxX, int BoxY, int BoxZ, int BoxWidth, int BoxDepth, int BoxHeight, int CutEffectId, uint GeneratorWord, IReadOnlyList<Cell> Cells, IReadOnlyList<Call> Calls)
    {
        public int Cuts => Calls.Count(c => c.Kind == "loot");
    }

    public IReadOnlyList<Vector> Vectors { get; }

    public int MismatchesOfTheOracle { get; }

    public int TotalCuts { get; }

    private static readonly Lazy<CateVectors> Loaded = new(() => new CateVectors(File.ReadAllText(AnnexFiles.CutAnnex("vectors/cate-vectors.json"))));

    public static CateVectors Load() => Loaded.Value;

    private static int I(JsonElement e) => unchecked((int)e.GetInt64());

    private CateVectors(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        MismatchesOfTheOracle = root.GetProperty("mismatches_vs_pseudocode").GetInt32() + root.GetProperty("two_step_mismatches").GetInt32();
        TotalCuts = root.GetProperty("total_cuts").GetInt32();
        var list = new List<Vector>();
        foreach (var v in root.GetProperty("vectors").EnumerateArray())
        {
            var box = v.GetProperty("box");
            var cells = v.GetProperty("cells").EnumerateArray()
                .Select(c => new Cell(c.GetProperty("col").GetInt32(), c.GetProperty("row").GetInt32(), c.GetProperty("flags").GetInt32(), c.GetProperty("height").GetInt32(), c.GetProperty("wall").GetInt32(),
                    c.GetProperty("flags_after").GetInt32(), c.GetProperty("wall_after").GetInt32())).ToList();
            var calls = new List<Call>();
            foreach (var c in v.GetProperty("calls").EnumerateArray())
            {
                var kind = c.GetProperty("call").GetString()!;
                calls.Add(kind switch
                {
                    "effect" => new Call(kind, c.GetProperty("flag").GetInt32(), c.GetProperty("id").GetInt32(), c.GetProperty("anim").GetInt32(), I(c.GetProperty("x")), I(c.GetProperty("y")), I(c.GetProperty("z")), 0, 0, 0, 0, 0),
                    "loot" => new Call(kind, 0, 0, 0, I(c.GetProperty("x")), I(c.GetProperty("y")), I(c.GetProperty("z")), c.GetProperty("code").GetInt32(), c.GetProperty("model_item").GetInt32(),
                        c.GetProperty("model_draws").GetInt32(), unchecked((uint)c.GetProperty("model_state_before").GetInt64()), unchecked((uint)c.GetProperty("model_state_after").GetInt64())),
                    _ => new Call(kind, 0, c.GetProperty("id").GetInt32(), 0, 0, 0, 0, 0, 0, 0, 0, 0),
                });
            }

            list.Add(new Vector(v.GetProperty("id").GetInt32(), v.GetProperty("frame").GetInt32() != 0, I(box.GetProperty("x")), I(box.GetProperty("y")), I(box.GetProperty("z")), I(box.GetProperty("w")), I(box.GetProperty("d")),
                I(box.GetProperty("h")), v.GetProperty("tile_cut_effect_id").GetInt32(), unchecked((uint)v.GetProperty("generator_word").GetInt64()), cells, calls));
        }

        Vectors = list;
    }
}
