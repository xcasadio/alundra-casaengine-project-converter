#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Session;
using Alundra.Hero;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.UI.Hud;
using Alundra.UI.Inventory;
using Alundra.UI.Saving;
using Alundra.UI.Shared;
using Alundra.UI.SubInventory;

namespace Alundra.Tests;

/// <summary>
/// E19.u (docs/plan-e19-opcodes.md section 1.2y, D-E19-103, open point O-E19-78, ADR-0050): the inventory's trigger runs in the binary's Update(k), AFTER RenderScene(k)
/// (the text box's release 0x8004501C clears bits 0x18 of 0x800DC4B8) and after UpdateWorld(k) (map events 0x8003C67C, entities 0x8003B388, effects 0x8003C410);
/// the port's tick k is Update(k) followed by RenderScene(k+1) for the inventory (AlundraInventoryDirector's class doc), so the inventory block runs at the end of
/// the tick, after the dialogue pass, the save screen and the map events of the same tick. The expected values were measured on ALUN_CD.EXE (France) by its real
/// code; the measurements and the disassembly are in docs/plan-e19-u-annexe/ (bin-out.txt, notes.md, verify/verify.md). The montage is the dialogue one
/// (map-event script as a lambda, one tick per 0.02 s frame).
/// </summary>
public sealed class AlundraInventoryTriggerFrameOrderTests : IDisposable
{
    private const int P = 5;

    private sealed class NullSounds : IAlundraSoundPlayer
    {
        public void PlaySfx(int sfxId) { }
        public void RemixVoice(int sfxId, int left, int right) { }
        public void FlushFrameSounds() { }
        public void StopAllSfx() { }
    }

    public AlundraInventoryTriggerFrameOrderTests() => Reset();

    public void Dispose() => Reset();

    private static void Reset()
    {
        AlundraInventoryDirector.Instance.ResetForTests();
        AlundraSubInventoryDirector.Instance.ResetForTests();
        AlundraInventoryPostProcess.Instance.ResetForTests();
        AlundraInventoryPortrait.Instance.ResetForTests();
        AlundraSaveScreenDirector.Instance.ResetForTests();
        AlundraHudDirector.Instance.ResetForTests();
        AlundraWarpDirector.Instance.ResetForTests();
        // The warp case reads the screen fade: a fade an earlier test class left settled at the departure colour
        // makes the departure's own fade settle on its first tick, and the warp director then aborts it (no
        // GameManager in this montage), so the test depended on the order of the test classes.
        AlundraScreenFadeDirector.Instance.ResetForTests();
    }

    private static DialogueBoxMontage Build()
    {
        var m = new DialogueBoxMontage();
        var tables = ItemTablesFixture.LoadReal();
        AlundraInventoryDirector.Instance.AttachToWorld(m.State, tables, new NullSounds());
        AlundraSubInventoryDirector.Instance.AttachToWorld(m.State, tables, new NullSounds());
        return m;
    }

    /// <summary>The ticks (relative to P) on which the map events did not run, as "a..b" runs.</summary>
    private static string SkippedRuns(Dictionary<int, uint> presses, int frames)
    {
        using var m = Build();
        var calls = 0;
        m.MapEventScript = () => calls++;
        m.PadForFrame = f => presses.TryGetValue(f - P, out var w) ? w : 0u;
        var skipped = new List<int>();
        for (var f = 0; f <= P + frames; f++)
        {
            var before = calls;
            m.RunFrame();
            if (calls == before)
            {
                skipped.Add(f - P);
            }
        }

        var runs = new List<string>();
        for (var i = 0; i < skipped.Count; i++)
        {
            var start = skipped[i];
            while (i + 1 < skipped.Count && skipped[i + 1] == skipped[i] + 1)
            {
                i++;
            }

            runs.Add($"{start}..{skipped[i]}");
        }

        return string.Join(",", runs);
    }

    [Fact]
    public void MapEvents_AreFrozenOnTheBinaryTicks_AroundAnInventoryOpenedAndClosed()
    {
        // binary: RunMapEvents skipped in Update(P+1..P+37) (MenuOpen posed by the slot-6 setup in RS(P+1), cleared by the close completion in RS(P+38)).
        Assert.Equal("1..37", SkippedRuns(new() { [0] = PadState.Start, [19] = PadState.Start }, 45));
    }

    [Fact]
    public void MapEvents_AreFrozenOnTheBinaryTicks_ThroughTheSubInventoryAndBack()
    {
        // binary: P+1..P+74 (L1 at P+19, sub-inventory closed by Start at P+56); P+1..P+112 (L1 back to the main at P+56, closed at P+94).
        Assert.Equal("1..74", SkippedRuns(new() { [0] = PadState.Start, [19] = PadState.L1, [56] = PadState.Start }, 90));
        Reset();
        Assert.Equal("1..112", SkippedRuns(new() { [0] = PadState.Start, [19] = PadState.L1, [56] = PadState.L1, [94] = PadState.Start }, 130));
    }

    [Fact]
    public void EffectAndPendingTriggerGates_FollowTheMapEvents_NotTheInventoryOfTheSameTick()
    {
        using var m = Build();
        m.PadForFrame = f => f - P == 0 || f - P == 19 ? PadState.Start : 0u;
        var effect = new List<bool>();
        var pending = new List<bool>();
        for (var f = 0; f <= P + 40; f++)
        {
            m.RunFrame();
            effect.Add(m.Proxy.EffectFrozenOfTickForTests.Single());
            pending.Add(m.Proxy.PendingTriggersGateForTests);
        }

        // binary: effects 0x8003C410 and UpdateEntities 0x8003B388 (pending triggers) run inside UpdateWorld(k), before the trigger: free at P and P+38.
        Assert.Equal((false, true, true, false), (effect[P], effect[P + 1], effect[P + 37], effect[P + 38]));
        Assert.Equal((false, true, true, false), (pending[P], pending[P + 1], pending[P + 37], pending[P + 38]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Start_OnTheTickTheBoxIsReleased_OpensTheInventory(int controlMode)
    {
        // binary: Square at sample S -> sound 7 in RS(S+1) -> release (bits 0x18 cleared) in RS(S+19); a Start sampled in that frame's Update opens.
        const int S = 46, R = 65;
        using var m = Build();
        m.Director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: controlMode);
        m.PadForFrame = f => f == S ? PadState.Square : f == R ? PadState.Start : 0u;
        m.RunFrames(R);
        Assert.True(m.Director.IsOpen);
        m.RunFrame(); // frame R: the pass releases the box, then the trigger reads the Start of this tick
        Assert.False(m.Director.IsOpen);
        Assert.True(AlundraInventoryDirector.Instance.IsActive);
        Assert.Equal(GameState.PlayerControlBits.MenuOpen, m.State.PlayerControlFlags & GameState.PlayerControlBits.MenuOpen);
    }

    [Theory]
    [InlineData(0, GameState.PlayerControlBits.MenuOpen)]
    [InlineData(1, GameState.PlayerControlBits.MessageBox)]
    public void MapEventOpeningABox_OnTheTriggerTick_WinsOverTheInventory(int controlMode, uint expectedFlags)
    {
        // binary: a box opened inside UpdateWorld(P) (map events) poses 0x08/0x10 before the trigger tests 0x800DC4B8 == 0: the Start of P is lost.
        using var m = Build();
        var opened = false;
        m.MapEventScript = () =>
        {
            if (!opened && m.Frame == P)
            {
                m.Director.Open(DialogueTestAssets.SinglePage("Bonjour", "bonjour"), "Start", controlMode: controlMode);
                opened = true;
            }
        };
        m.PadForFrame = f => f == P ? PadState.Start : 0u;
        m.RunFrames(P + 1);
        Assert.True(m.Director.IsOpen);
        Assert.Equal(expectedFlags, m.State.PlayerControlFlags);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        m.RunFrames(40);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
    }

    [Fact]
    public void MapEventStartingAWarp_OnTheTriggerTick_StartsTheWarp_AndTheTriggerRefusesStart()
    {
        // Behaviour change of E19.u (ADR-0050, gap 2), added with the fix and values measured on the fixed code (it is also red on the code before the fix: the departure does not start). A map event that starts a departure on the tick
        // Start is pressed: the trigger now runs after it and refuses Start through its warp guard (D-E13D-30). Before E19.u the inventory opened first,
        // posted MenuOpen and froze the map events, so the departure did not start on that tick. The binary starts the warp and leaves the inventory armed
        // (the defect D-E13D-30 corrects).
        using var m = Build();
        var started = false;
        m.MapEventScript = () =>
        {
            if (!started && m.Frame == P)
            {
                AlundraWarpDirector.Instance.BeginDepartureFromChangeMapOpcode(1, 0, 0, 0, 0, 0, m.Proxy.PlayerEntity!, m.State);
                started = true;
            }
        };
        m.PadForFrame = f => f == P ? PadState.Start : 0u;
        m.RunFrames(P + 1);
        Assert.True(started);
        Assert.True(AlundraWarpDirector.Instance.IsTransitionInProgress);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
        Assert.Equal(0u, m.State.PlayerControlFlags & GameState.PlayerControlBits.MenuOpen);
        m.RunFrames(2);
        Assert.False(AlundraInventoryDirector.Instance.IsActive);
    }
}
