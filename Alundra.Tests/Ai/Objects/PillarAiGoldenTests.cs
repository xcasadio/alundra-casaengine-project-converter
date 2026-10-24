#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g5a T1 (docs/plan-e20-heros.md section 1.24.6, phase A, (a1) and (a2)): the 14 goldens of C76 that the discovery made from the real code of the binary
/// (<c>docs/plan-e20-annexe/e20-g5/disc/c76_goldens.json</c>, one vector per outcome class, run on the MIPS machine with the sound and its stop as journalled stubs). Each vector is played through <see cref="PillarAi.Tick"/>: the three words
/// the handler writes (<c>+0x274</c>, <c>+0x88</c>, <c>+0x8C</c>) and the journal (the sound 25, the stop of the sound 25) are compared, the other fields read are left as they were.
/// </summary>
public sealed class PillarAiGoldenTests
{
    /// <summary>The address of the pillar in the machine's run (<c>ENT0 + 3 * STRIDE</c> of <c>c76_full.py</c>): the value of <c>hero.+0x130</c> that means "the hero is blocked by the pillar".</summary>
    private const uint PillarAddress = 2148697324;

    private static uint U(JsonElement e) => unchecked((uint)e.GetInt64());

    private static uint Word(JsonElement fields, string name) => U(fields.GetProperty(name));

    [Fact]
    public void TheGoldenFile_IsTheOneTheDiscoveryPinned_14Vectors_10WithTheContactAnd4Without()
    {
        var path = AnnexFiles.PillarAnnex("c76_goldens.json");
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        Assert.Equal("c80ffbe3def37599c6671f32bf2020153c6b782c235707eceecce09f4ad90d5a", sha);

        using var document = JsonDocument.Parse(text);
        var vectors = document.RootElement.EnumerateArray().ToList();
        Assert.Equal(14, vectors.Count);
        var contact = vectors.Where(v => Word(v.GetProperty("hero_in"), "0x130") == PillarAddress).ToList();
        Assert.Equal(10, contact.Count);
        Assert.All(contact, v => Assert.StartsWith("contact/", v.GetProperty("cls").GetString()));
        Assert.All(vectors.Except(contact), v => Assert.StartsWith("other/", v.GetProperty("cls").GetString()));
    }

    [Fact]
    public void TheGoldens_AreFollowedWordForWord_TheCounterTheAnimationTheDirectionTheSoundAndItsStop()
    {
        var path = AnnexFiles.PillarAnnex("c76_goldens.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var sounds = 0;
        var stops = 0;
        var directions = new HashSet<uint>();

        foreach (var vector in document.RootElement.EnumerateArray())
        {
            var cls = vector.GetProperty("cls").GetString()!;
            var pillarIn = vector.GetProperty("pillar_in");
            var heroIn = vector.GetProperty("hero_in");
            var pillarOut = vector.GetProperty("pillar_out");

            var pillar = new EntityScriptProxy
            {
                PosX = unchecked((int)Word(pillarIn, "0x114")),
                PosY = unchecked((int)Word(pillarIn, "0x118")),
                ModdedPosX = unchecked((int)Word(pillarIn, "0x1D8")),
                Width = unchecked((int)Word(pillarIn, "0x1F0")),
                TargetAnimationId = Word(pillarIn, "0x88"),
                TargetDirection = Word(pillarIn, "0x8C"),
            };
            PillarAi.WriteCounter(pillar, unchecked((int)Word(pillarIn, "0x274")));

            var hero = new EntityScriptProxy
            {
                PosX = unchecked((int)Word(heroIn, "0x114")),
                PosY = unchecked((int)Word(heroIn, "0x118")),
                ModdedPosX = unchecked((int)Word(heroIn, "0x1D8")),
                Width = unchecked((int)Word(heroIn, "0x1F0")),
            };
            var other = new EntityScriptProxy();
            var xCollision = Word(heroIn, "0x130");
            hero.XCollisionEntity = xCollision == PillarAddress ? pillar : xCollision == 0 ? null : other;

            var sound = new RecordingSoundPlayer();
            PillarAi.Tick(pillar, new PickupHost { Hero = hero, Sound = sound });
            var stubCalls = sound.StopRequests;

            uint Expected(string name) => pillarOut.TryGetProperty(name, out var value) ? U(value) : Word(pillarIn, name);

            Assert.True(Expected("0x274") == unchecked((uint)PillarAi.ReadCounter(pillar)), $"{cls}: the counter");
            Assert.True(Expected("0x88") == pillar.TargetAnimationId, $"{cls}: the animation asked");
            Assert.True(Expected("0x8C") == pillar.TargetDirection, $"{cls}: the direction asked");

            // the journal: the sound 25 and its stop, the machine's own order of events.
            var journal = vector.GetProperty("journal").EnumerateArray().Select(e => (e[0].GetString()!, e[1].GetInt32())).ToList();
            Assert.Equal(journal.Where(e => e.Item1 == "sound").Select(e => e.Item2).ToList(), sound.Requests);
            Assert.Equal(journal.Where(e => e.Item1 == "stop").Select(e => e.Item2).ToList(), stubCalls);
            sounds += sound.Requests.Count;
            stops += stubCalls.Count;

            // the fields that are only read stay as they were.
            Assert.Equal(unchecked((int)Word(pillarIn, "0x114")), pillar.PosX);
            Assert.Equal(unchecked((int)Word(pillarIn, "0x118")), pillar.PosY);
            Assert.Equal(unchecked((int)Word(pillarIn, "0x1D8")), pillar.ModdedPosX);
            Assert.Equal(unchecked((int)Word(pillarIn, "0x1F0")), pillar.Width);
            Assert.Equal(unchecked((int)Word(heroIn, "0x114")), hero.PosX);
            Assert.Equal(unchecked((int)Word(heroIn, "0x118")), hero.PosY);

            if (pillarOut.TryGetProperty("0x8C", out var direction))
            {
                directions.Add(U(direction));
            }
        }

        // the four sides of (a2) are all among the goldens (the direction written is the side plus 0x10, modulo 0x20) and both kinds of journal are present.
        Assert.Equal(new uint[] { 0, 8, 16, 24 }, directions.OrderBy(d => d).ToArray());
        Assert.Equal((8, 2), (sounds, stops));
    }
}
