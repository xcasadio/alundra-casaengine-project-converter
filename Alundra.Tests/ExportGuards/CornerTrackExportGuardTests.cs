using System;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Assets.Animations;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.g G2b-3 guard on the REAL export: the opening of the chest of Anzes (<c>bankalundra_227_anim1_down</c>, chain 163,
/// 170, 177, 184) carries a <c>Corners</c> track on each of its 10 deformed parts, and the corner track does not change the
/// duration of the animation (the logical clock of the DLL reads <c>DurationSeconds</c>). The values are those of
/// docs/plan-e19-g2b-annexe/g2b3-prediction.json, written by g2b3-predict.py before the converter code. The file is looked
/// for with the exact path of line 222 of predicted_anim2d_changes.tsv (typographic apostrophe U+2019 in the folder name
/// <c>Coffre d’Anzes</c>); a missing file or tsv FAILS the guard, it is never skipped.
/// </summary>
public class AlundraCornerTrackExportGuardTests
{
    private const string GuardPath = "Entities/Coffre d’Anzes/bankalundra_227_anim1_down.anim2d";

    private static string FindUpwards(string relativePath, bool directory)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (directory ? Directory.Exists(candidate) : File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"AlundraCornerTrackExportGuardTests: fichier introuvable '{relativePath}' above '{AppContext.BaseDirectory}'. "
            + "The guard never skips: run the export (alundra-project/) and keep docs/ next to it.");
    }

    [Fact]
    public void GuardPath_IsTheOneOfLine222OfThePredictedChangesList()
    {
        var tsv = FindUpwards("docs/plan-e19-g2b-annexe/predicted_anim2d_changes.tsv", directory: false);
        var line = File.ReadAllLines(tsv)[221].Split('\t')[0]; // line 222, 1-based (line 1 is the header)
        Assert.Equal(GuardPath, line);
        Assert.Contains('’', line);
    }

    [Fact]
    public void ChestOpening_CarriesTheCornerTracksOfThePrediction_AndKeepsItsDuration()
    {
        var root = FindUpwards("alundra-project", directory: true);
        var file = Path.Combine(root, "Entities", "Coffre d’Anzes", "bankalundra_227_anim1_down.anim2d");
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"AlundraCornerTrackExportGuardTests: fichier introuvable '{file}' (guard file of the export).");
        }

        var data = new Animation2dData();
        data.Load(JObject.Parse(File.ReadAllText(file)));

        var corners = data.Tracks.Where(t => t.Property == Animation2dTrackProperty.Corners).ToArray();
        Assert.Equal(
            Enumerable.Range(0, 10).Select(i => $"part{i}").ToArray(),
            corners.Select(t => t.TargetPartId).ToArray());
        Assert.Equal(new[] { 7, 4, 1, 1, 1, 1, 1, 1, 3, 2 }, corners.Select(t => t.CornerKeyframes.Count).ToArray());

        // first key of each track: (time, +-half-size), every one an active key with the source window not moved
        var expected = new (float Time, float Half)[]
        {
            (0.36f, 7), (0.59999996f, 14), (0.78f, 3), (0.78f, 3), (0.78f, 3), (0.78f, 3), (0.78f, 3), (0.78f, 3),
            (0.65999997f, 18), (0.65999997f, 18),
        };
        for (var index = 0; index < corners.Length; index++)
        {
            var key = corners[index].CornerKeyframes[0];
            var (time, half) = expected[index];
            Assert.True(key.Enabled, $"part{index}: first key active");
            Assert.Equal(time, key.TimeSeconds);
            Assert.Equal(new Vector2(-half, half), key.TopLeft);
            Assert.Equal(new Vector2(half, half), key.TopRight);
            Assert.Equal(new Vector2(-half, -half), key.BottomLeft);
            Assert.Equal(new Vector2(half, -half), key.BottomRight);
            Assert.Equal(new Point(0, 0), key.SourceOffset);
        }

        Assert.Equal(1.1f, data.GetDurationSeconds());
    }
}
