using System;
using System.IO;
using System.Linq;
using CasaEngine.Framework.Assets.Animations;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E19.m7 (O-E19-9) guard on the real export: the five Loop animations whose only displayed frame lasts
/// 0 ticks (Delay 0x80) must keep every part visible at t = 0 with a duration of 0, because the binary's
/// UpdateAnimation holds such a frame for 2^32 - 1 ticks. Before the fix the terminator wrote a second
/// Visible key at the same instant that hid every part. Throws, naming the missing export, when
/// alundra-project/ is absent (same pattern as the other export-reading tests).
/// </summary>
public class AlundraZeroDurationLoopExportGuardTests
{
    private static string FindEntitiesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project", "Entities");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"AlundraZeroDurationLoopExportGuardTests: no 'alundra-project/Entities' directory found above "
            + $"'{AppContext.BaseDirectory}' - this guard needs the real converter export.");
    }

    [Theory]
    [InlineData("Interrupteur tricolore", "bank127_anim0_left.anim2d", 1)]
    [InlineData("Interrupteur tricolore", "bank127_anim0_right.anim2d", 1)]
    [InlineData("YM08 barrière u=eau d=feu l=sable r=glace", "bank151_anim1_down.anim2d", 4)]
    [InlineData("YM08 barrière u=eau d=feu l=sable r=glace", "bank151_anim1_left.anim2d", 4)]
    [InlineData("YM08 barrière u=eau d=feu l=sable r=glace", "bank151_anim1_up.anim2d", 4)]
    public void ZeroDurationLoop_EveryPartKeepsASingleVisibleKeyAtZero(string folder, string file, int partCount)
    {
        var path = Path.Combine(FindEntitiesDirectory(), folder, file);
        Assert.True(File.Exists(path), $"Missing export file '{path}'.");

        var data = new Animation2dData();
        data.Load(JObject.Parse(File.ReadAllText(path)));

        Assert.Equal(AnimationType.Loop, data.AnimationType);
        Assert.Equal(0f, data.GetDurationSeconds());

        var visibleTracks = data.Tracks.Where(t => t.Property == Animation2dTrackProperty.Visible).ToList();
        Assert.Equal(partCount, visibleTracks.Count);
        foreach (var track in visibleTracks)
        {
            var key = Assert.Single(track.VisibleKeyframes);
            Assert.Equal(0f, key.TimeSeconds);
            Assert.True(key.Value);
        }
    }
}
