#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alundra.Scripts;
using Xunit;

namespace Alundra.Tests;

/// <summary>
/// E20.c2a (docs/plan-e20-heros.md section 1.12): the hero's life carried by the entity, the end of <c>MovePlayer</c> on every exit, <c>UpdateItemEffectState</c> steps 1, 6 and 7, and the alarm of the low life,
/// replayed against the oracle of E20.c2-0 (<c>docs/plan-e20-annexe/e20-c2/oracle/out/</c>: the real code of <c>ALUN_CD.EXE</c> (France) in an emulator; groups E, F, G and D1) and, for the period of the
/// alarm, <c>oracle/out-c2a/alarm.txt</c> (<c>oracle/alarm.py</c>, a new file of E20.c2a). Every expected value below is the oracle's or is written from the binary's rule; none is computed by the code under test.
/// </summary>
public sealed class AlundraHeroLifeTests
{
    // -----------------------------------------------------------------------------------------
    // The oracle files.
    // -----------------------------------------------------------------------------------------

    private static string OracleFolder(string sub)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "plan-e20-annexe", "e20-c2", "oracle", sub);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"no 'docs/plan-e20-annexe/e20-c2/oracle/{sub}' directory found above '{AppContext.BaseDirectory}': this test needs the E20.c2-0 oracle");
    }

    private static JsonElement Load(string name)
    {
        var path = Path.Combine(OracleFolder("out"), name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"the E20.c2-0 oracle file '{name}' is missing", path);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static JsonElement RunNamed(string file, string name) =>
        Load(file).GetProperty("runs").EnumerateArray().Single(r => r.GetProperty("spec").GetProperty("name").GetString() == name);

    private sealed class RecordingSound : IAlundraSoundPlayer
    {
        public List<int> Played { get; } = new();

        public void PlaySfx(int sfxId) => Played.Add(sfxId);

        public void RemixVoice(int sfxId, int left, int right)
        {
        }

        public void FlushFrameSounds()
        {
        }

        public void StopAllSfx()
        {
        }
    }

    // -----------------------------------------------------------------------------------------
    // T4: the alarm of the low life (0x800303DC-0x80030430), against oracle/out-c2a/alarm.txt
    // -----------------------------------------------------------------------------------------

    /// <summary>name, Hp, HpMax, ticks, then the ticks at which the entity's Hp is set (tick, value): the setups of oracle/alarm.py.</summary>
    public static IEnumerable<object[]> AlarmCases()
    {
        yield return new object[] { "AL1", 2, 12, 200, "" };
        yield return new object[] { "AL2a", 2, 10, 60, "" };
        yield return new object[] { "AL2b", 3, 10, 60, "" };
        yield return new object[] { "AL2c", 10, 50, 60, "" };
        yield return new object[] { "AL2d", 11, 50, 60, "" };
        yield return new object[] { "AL2e", 1, 50, 60, "" };
        yield return new object[] { "AL2f", 0, 10, 30, "" };
        yield return new object[] { "AL3", 2, 12, 100, "21:10,41:2" };
    }

    private static int[] OracleAlarmTicks(string name)
    {
        var line = File.ReadAllLines(Path.Combine(OracleFolder("out-c2a"), "alarm.txt"))
            .Single(l => Regex.IsMatch(l, @"^PASS\s+" + name + @"\s"));
        var match = Regex.Match(line, @"sound\(400\) at \[(?<ticks>[^\]]*)\] \(expected");
        Assert.True(match.Success, line);
        var text = match.Groups["ticks"].Value;
        return text.Length == 0 ? Array.Empty<int>() : text.Split(',').Select(t => int.Parse(t.Trim())).ToArray();
    }

    private static List<int> AlarmTicksOf(int hp, int hpMax, int ticks, string hpSets, bool locked = false)
    {
        var sound = new RecordingSound();
        var host = new ContactHost { SoundPlayer = sound };
        var hero = new AlundraEntityScriptProxy { ScriptHost = host, Hp = hp, HpMax = hpMax, LifePulledFromStats = true };
        var sets = hpSets.Length == 0
            ? new Dictionary<int, int>()
            : hpSets.Split(',').Select(p => p.Split(':')).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]));

        var at = new List<int>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            if (sets.TryGetValue(tick, out var value))
            {
                hero.Hp = value;
            }

            var before = sound.Played.Count;
            AlundraPlayerAnimationEffects.Create(hero, locked ? AlundraPlayerAnimationEffects.LockedMode : AlundraPlayerAnimationEffects.FreeMode);
            at.AddRange(sound.Played.Skip(before).Where(id => id == 0x190).Select(_ => tick));
        }

        return at;
    }

    [Theory]
    [MemberData(nameof(AlarmCases))]
    public void TheLowLifeAlarm_SoundsAtTheTicksOfTheBinary(string name, int hp, int hpMax, int ticks, string hpSets)
    {
        Assert.Equal(OracleAlarmTicks(name), AlarmTicksOf(hp, hpMax, ticks, hpSets));
    }

    [Fact]
    public void TheLowLifeAlarm_RuleOfTheBinary_PeriodIs46_FirstSoundAtOnce_AndTheLockedModeNeverSounds()
    {
        // Written from the rule re-read in the binary (0x800303DC-0x80030430), independently of the oracle file: counter 0 -> -1 at the first tick, reload 0x2D, again 46 ticks later.
        Assert.Equal(new[] { 1, 47, 93 }, AlarmTicksOf(2, 12, 100, ""));
        Assert.Empty(AlarmTicksOf(2, 12, 100, "", locked: true)); // the end of the function is mode 0 only (bnez $s5, 0x800303D8).
    }

    [Fact]
    public void TheLowLifeAlarm_AHeroWithoutAScriptHost_HasNoSessionCounter_AndIsSilent()
    {
        var hero = new AlundraEntityScriptProxy { Hp = 1, HpMax = 10 };

        AlundraPlayerAnimationEffects.Create(hero, AlundraPlayerAnimationEffects.FreeMode);

        Assert.Equal(1, hero.Hp); // nothing to assert but that it does not throw: the bare proxy of the montages.
    }

    [Fact]
    public void TheLowLifeAlarm_TheCounterIsSessionState_AndSurvivesTheHeroBeingRebuilt()
    {
        var sound = new RecordingSound();
        var host = new ContactHost { SoundPlayer = sound };
        var first = new AlundraEntityScriptProxy { ScriptHost = host, Hp = 2, HpMax = 12, LifePulledFromStats = true };
        AlundraPlayerAnimationEffects.Create(first, AlundraPlayerAnimationEffects.FreeMode); // tick 1: the sound, the counter 0x2D.
        Assert.Equal(AlundraPlayerAnimationEffects.LowHpAlarmReload, host.GameState.LowHpAlarmCounter);

        var second = new AlundraEntityScriptProxy { ScriptHost = host, Hp = 2, HpMax = 12, LifePulledFromStats = true }; // the hero of the next map: the global is not reset by a map load.
        AlundraPlayerAnimationEffects.Create(second, AlundraPlayerAnimationEffects.FreeMode);

        Assert.Equal(AlundraPlayerAnimationEffects.LowHpAlarmReload - 1, host.GameState.LowHpAlarmCounter);
        Assert.Single(sound.Played, id => id == 0x190);
    }
}
