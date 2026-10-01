#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Alundra.Scripts;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;

namespace Alundra.Tests;

/// <summary>
/// E19.c2 (docs/plan-e19-opcodes.md §1.2f): the exact ends of the animations as the event programs see them, driven through the
/// PRODUCTION <see cref="AlundraEntityScriptProxy.Update"/> (the real-time component pass first, then the proxy) over the real
/// interpreter, a real <see cref="AnimatedSpriteComponent"/> and synthetic animations carrying the export's durations. Only the
/// tick count of a frame is scripted (a fake <see cref="IAlundraScriptHost"/>). Map-event programs (B) are emulated by calling the
/// interpreter on the logic entity once per tick after the entity's update, the order of the world's own pass. Every expected value
/// was written by hand before the code, from two independent models of the binary (<c>UpdateAnimation</c> @ <c>0x80038AB4</c>,
/// <c>0x1C</c> @ <c>0x8003D7FC</c>); a contradicted value is a stop, never a re-pin. The frame <c>s</c> is the frame that runs the
/// <c>0x1A</c> of the program; the entity plays another animation before <c>s</c> (without that, <c>0x1A</c> changes nothing and the
/// wait would depend on the phase of the animation already playing).
/// <para>
/// The 60 Hz column is the tick pattern [1,1,1,1,1,0] with <c>s</c> at the start of the pattern; "2 t/i" is two ticks per frame, the
/// <c>0x1A</c> at the first tick of <c>s</c>.
/// </para>
/// </summary>
public sealed class AlundraAnimationClockDriveTests
{
    /// <summary>The frame that runs the <c>0x1A</c>: a multiple of six, so the 60 Hz pattern starts on it.</summary>
    private const int S = 24;

    /// <summary>Ticks of the frames: "1" one per frame, "60" the pattern [1,1,1,1,1,0], "2" two per frame.</summary>
    private static int TicksOf(string pattern, int frame) => pattern switch
    {
        "1" => 1,
        "60" => frame % 6 == 5 ? 0 : 1,
        "2" => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null),
    };

    // ----------------------------------------------------------------------------------------------------------
    // The export's animations (durations in seconds as the converter writes them) and the harness
    // ----------------------------------------------------------------------------------------------------------

    private readonly record struct Anim(uint Id, AnimationType Type, float Seconds, AnimationEndKind End = AnimationEndKind.Loop, int ChainTo = 0);

    private static Anim Loop(uint id, float seconds) => new(id, AnimationType.Loop, seconds);

    private static Anim Hold(uint id, float seconds) => new(id, AnimationType.Once, seconds, AnimationEndKind.Hold);

    private static Anim Chain(uint id, float seconds, int to) => new(id, AnimationType.Once, seconds, AnimationEndKind.Chain, to);

    // Ronan (record 16 of map 478).
    private static readonly Anim[] Ronan = { Hold(6, 0.48f), Loop(0, 1.6000001f) };

    // The hero.
    private static readonly Anim[] Hero =
    {
        Hold(83, 0.64f), Hold(85, 0.79999995f), Loop(0, 1.0799999f), Chain(1, 0.59999996f, 1),
    };

    // Jess (Chain 12 -> 0, then her Loop of 85 ticks).
    private static readonly Anim[] Jess = { Chain(12, 0.48f, 0), Loop(0, 1.7f) };

    // Wendell (record 6 of map 172).
    private static readonly Anim[] Wendell = { Loop(10, 0.06f), Loop(11, 1.7999999f) };

    // A Loop of 7 ticks (the animation played before the wait) and a Loop of 10 ticks (the one waited on).
    private static readonly Anim[] LoopsOf7And10 = { Loop(3, 0.14f), Loop(7, 0.2f) };

    private sealed class Host : IAlundraScriptHost
    {
        public int Ticks = 1;
        public IEventProgramRunner Runner { get; set; } = null!;
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public AlundraGameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController => null;
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => Ticks;
    }

    /// <summary>Runs the interpreter only for the slots the test enabled: the tick program (C) starts at the frame the test says.</summary>
    private sealed class GatedRunner : IEventProgramRunner
    {
        public AlundraEventProgramRunner Inner = null!;
        public bool TickProgramEnabled;

        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
            if (programSlot != ScriptHelper.ProgramCTick || TickProgramEnabled)
            {
                Inner.RunScript(entity, programSlot);
            }
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private sealed class Drive
    {
        public readonly Entity Entity;
        public readonly AlundraEntityScriptProxy Proxy;
        public readonly AnimatedSpriteComponent Sprite;
        public readonly AlundraEventProgramRunner Runner;
        public readonly Host Host = new();
        public readonly List<(int Frame, int Opcode, int Result)> Calls = new();
        private readonly GatedRunner _gated = new();
        private readonly Func<int, int> _ticksOf;
        private readonly List<Entity> _entities;

        /// <summary>The frame from which the map-event program (B) runs, after the entity's update; -1 never.</summary>
        public int MapProgramFromFrame = -1;

        public int Frame { get; private set; }

        public Drive(Anim[] anims, uint initialAnimation, int[] program, Func<int, int> ticksOf, bool isPlayer = false)
        {
            _ticksOf = ticksOf;

            var document = new EventProgramDocument
            {
                MapIndex = 1,
                EventCodesATable = new[] { 0, 0 },
                EventCodesBTable = new[] { 0, 0 },
                EventCodesCTable = new[] { 0, 0 },
                Codes = program,
            };
            Runner = new AlundraEventProgramRunner(document, Host.GameState);
            _entities = new List<Entity>();
            Runner.TraceSink = record =>
            {
                if (record.Opcode is 0x1C or 0x1D)
                {
                    Calls.Add((Frame, record.Opcode, record.Size));
                }
            };
            _gated.Inner = Runner;
            Host.Runner = _gated;

            var world = new World();
            var game = (CasaEngineGame)RuntimeHelpers.GetUninitializedObject(typeof(CasaEngineGame));
            game.ExecutionPolicy = GameplayExecutionPolicies.Runtime;
            typeof(Microsoft.Xna.Framework.Game).GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());
            HeroWorldFixture.SetProperty(world, nameof(World.Game), game);

            Sprite = new AnimatedSpriteComponent();
            var endTable = new Dictionary<int, AnimationEndInfo>();
            foreach (var anim in anims)
            {
                Sprite.AddAnimation(Make(anim));
                if (anim.End != AnimationEndKind.Loop)
                {
                    endTable[(int)anim.Id * AlundraEntitySpawnFactory.IdsvDirectionStride] =
                        new AnimationEndInfo { Kind = anim.End, ChainTargetAnimationId = anim.ChainTo };
                }
            }

            Entity = new Entity { Name = "e", GameplayProxyClassName = nameof(AlundraEntityScriptProxy), RootComponent = Sprite };
            HeroWorldFixture.SetProperty(Entity, nameof(Entity.World), world);
            Entity.Initialize();

            Proxy = Assert.IsType<AlundraEntityScriptProxy>(Entity.GameplayProxy);
            Proxy.ScriptHost = Host;
            Proxy.IsPlayer = isPlayer;
            Proxy.Status = EntityStatus.Normal;
            Proxy.AnimationEndByAnimDirection = endTable.Count == 0 ? null : endTable;
            Proxy.ProgramIndexes[ScriptHelper.ProgramCTick] = 1;
            Proxy.ProgramIndexes[ScriptHelper.ProgramBMap] = 1;
            Proxy.TargetAnimationId = initialAnimation;
            Proxy.CurrentAnimationId = ~initialAnimation;
            AlundraEntitySpawnFactory.SubscribeAnimationEndBridge(Entity);
            _entities.Add(Entity);
        }

        private static Animation2d Make(Anim anim)
        {
            var spriteId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
            var data = new Animation2dData { Name = $"bank_anim{anim.Id}_down", AnimationType = anim.Type };
            data.Parts.Add(new Animation2dPartData { Id = "sprite", DefaultSpriteId = spriteId });
            var track = new Animation2dTrackData { TargetPartId = "sprite", Property = Animation2dTrackProperty.Sprite };
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(0f, spriteId));
            track.SpriteKeyframes.Add(new Animation2dGuidKeyframeData(anim.Seconds, spriteId));
            data.Tracks.Add(track);
            return new Animation2d(data);
        }

        /// <summary>One frame: the entity's update (real-time component pass, then the proxy), then the map-event program once per tick.</summary>
        public void RunFrame()
        {
            var ticks = _ticksOf(Frame);
            Host.Ticks = ticks;
            Entity.Update(0.02f);

            if (MapProgramFromFrame >= 0 && Frame >= MapProgramFromFrame)
            {
                for (var tick = 0; tick < ticks; tick++)
                {
                    // The world's own pass, between the passes of a catch-up frame.
                    if (tick > 0)
                    {
                        AlundraFrameSyncPasses.ClearHoldFlagsOfPendingSwitches(_entities);
                    }

                    Runner.RunScript(Proxy, ScriptHelper.ProgramBMap);
                }
            }

            Frame++;
        }

        /// <summary>Runs frames until <see cref="Frame"/> reaches <paramref name="untilFrame"/> (exclusive), calling <paramref name="afterFrame"/> with the frame just run.</summary>
        public void RunTo(int untilFrame, Action<int>? afterFrame = null)
        {
            while (Frame < untilFrame)
            {
                var frame = Frame;
                RunFrame();
                afterFrame?.Invoke(frame);
            }
        }

        public void EnableTickProgram() => _gated.TickProgramEnabled = true;

        /// <summary>The frames (relative to <see cref="S"/>) at which a <c>0x1C</c> or <c>0x1D</c> returned 2.</summary>
        public int[] EndFramesFromS() => Calls.Where(c => c.Result == 2).Select(c => c.Frame - S).ToArray();

        public int[] CallFramesFromS() => Calls.Select(c => c.Frame - S).ToArray();
    }

    /// <summary>A tick program (C) of an NPC that plays <paramref name="initial"/> until <see cref="S"/>, then runs <paramref name="program"/>.</summary>
    private static Drive NpcWithTickProgram(Anim[] anims, uint initial, int[] program, string pattern)
    {
        var drive = new Drive(anims, initial, program, f => TicksOf(pattern, f));
        drive.RunTo(S);
        drive.EnableTickProgram();
        return drive;
    }

    /// <summary>A map-event program (B) running on the hero from <see cref="S"/>.</summary>
    private static Drive HeroWithMapProgram(Anim[] anims, uint initial, int[] program, string pattern)
    {
        var drive = new Drive(anims, initial, program, f => TicksOf(pattern, f), isPlayer: true);
        drive.MapProgramFromFrame = S;
        drive.RunTo(S);
        return drive;
    }

    private static void RunUntilFrame(Drive drive, int frameFromS) => drive.RunTo(S + frameFromS + 1);

    // ----------------------------------------------------------------------------------------------------------
    // Tick programs (C)
    // ----------------------------------------------------------------------------------------------------------

    /// <summary>T-D1: Ronan on map 478, <c>1A 06 ; 1C 01 ; 1A 00</c>: a Hold of 24 ticks, counted one tick after it ends.</summary>
    [Theory]
    [InlineData("1", 25)]
    [InlineData("60", 30)]
    [InlineData("2", 12)]
    public void TD1_Ronan_HoldOf24Ticks_IsSeenByTheCallOfTheTickAfterItsEnd(string pattern, int expectedEnd)
    {
        var drive = NpcWithTickProgram(Ronan, 0, new[] { 0x01, 0x1A, 0x06, 0x1C, 0x01, 0x1A, 0x00, 0xFF }, pattern);
        var ticksAtFrameEnd = new List<int>();
        var holdFlagAtFrameEnd = new List<int>();

        drive.RunTo(S + expectedEnd + 3, frame =>
        {
            if (frame >= S)
            {
                ticksAtFrameEnd.Add(drive.Sprite.LogicalTick);
                holdFlagAtFrameEnd.Add(drive.Proxy.ForceResetAnimationFlag);
            }
        });

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());

        if (pattern == "1")
        {
            // The logical tick at the end of frame s+k is k, for k = 0 to 24; the Hold flag is 1 at the end of s+24 and 0 at the end of s+25.
            Assert.Equal(Enumerable.Range(0, 25), ticksAtFrameEnd.Take(25));
            Assert.Equal(1, holdFlagAtFrameEnd[24]);
            Assert.Equal(0, holdFlagAtFrameEnd[25]);
            Assert.Equal(0u, drive.Proxy.CurrentAnimationId);
            Assert.Equal(0, ticksAtFrameEnd[25]);
            Assert.EndsWith("_anim0_down", drive.Sprite.CurrentAnimation!.Animation2dData.Name);
        }
    }

    /// <summary>T-D2: <c>1A 53 ; 1C 03</c>, a Hold of 32 ticks counted three times (each count needs its own restart of the animation).</summary>
    [Theory]
    [InlineData("1", 99)]
    [InlineData("60", 118)]
    [InlineData("2", 49)]
    public void TD2_HeroLike_HoldOf32Ticks_ThreeCounts(string pattern, int expectedEnd)
    {
        var drive = NpcWithTickProgram(Hero, 0, new[] { 0x01, 0x1A, 0x53, 0x1C, 0x03, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D3: from a finished Hold of 10 ticks, <c>1A L ; 1C 02</c> on a Loop of 10 ticks: two turns.</summary>
    [Theory]
    [InlineData("1", 21)]
    [InlineData("60", 25)]
    [InlineData("2", 10)]
    public void TD3_FromAFinishedHold_TwoTurnsOfALoopOf10Ticks(string pattern, int expectedEnd)
    {
        var anims = new[] { Hold(5, 0.2f), Loop(7, 0.2f) };
        var drive = NpcWithTickProgram(anims, 5, new[] { 0x01, 0x1A, 0x07, 0x1C, 0x02, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D4: Wendell on map 172, from his animation 10 (a Loop of 3 ticks), <c>1A 0B ; 1C 01 ; 1A 0A</c> on a Loop of 90 ticks.</summary>
    [Theory]
    [InlineData("1", 91)]
    [InlineData("60", 109)]
    [InlineData("2", 45)]
    public void TD4_Wendell_LoopOf90Ticks_OneTurn(string pattern, int expectedEnd)
    {
        var drive = NpcWithTickProgram(Wendell, 10, new[] { 0x01, 0x1A, 0x0B, 0x1C, 0x01, 0x1A, 0x0A, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D5: from a Loop of 7 ticks, <c>1A L ; 1C n</c> on a Loop of 10 ticks, n = 1, 2, 3: s + 10 n + 1.</summary>
    [Theory]
    [InlineData("1", 1, 11)]
    [InlineData("1", 2, 21)]
    [InlineData("1", 3, 31)]
    [InlineData("60", 1, 13)]
    [InlineData("60", 2, 25)]
    [InlineData("60", 3, 37)]
    [InlineData("2", 1, 5)]
    [InlineData("2", 2, 10)]
    [InlineData("2", 3, 15)]
    public void TD5_LoopOf10Ticks_NTurns(string pattern, int turns, int expectedEnd)
    {
        var drive = NpcWithTickProgram(LoopsOf7And10, 3, new[] { 0x01, 0x1A, 0x07, 0x1C, turns, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D6: <c>1A 01 ; 1C 03</c>, the walk of the hero: a Chain onto itself of 30 ticks, three ends.</summary>
    [Theory]
    [InlineData("1", 91)]
    [InlineData("60", 109)]
    [InlineData("2", 45)]
    public void TD6_SelfChainOf30Ticks_ThreeEnds(string pattern, int expectedEnd)
    {
        var drive = NpcWithTickProgram(Hero, 0, new[] { 0x01, 0x1A, 0x01, 0x1C, 0x03, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D7: <c>1A 0C ; 37 17 ; 1C 01</c>: Jess, a Chain of 24 ticks onto her Loop, then the wait: the first call of <c>0x1C</c> is
    /// on the tick of the end of the Chain (s + 24), and the Loop of 85 ticks must not end it.</summary>
    [Fact]
    public void TD7_AChainEndOnTheTickOfTheFirstCall_IsNotLost_AndTheLoopThatFollowsIsNotCounted()
    {
        var drive = NpcWithTickProgram(Jess, 0, new[] { 0x01, 0x1A, 0x0C, 0x37, 0x17, 0x1C, 0x01, 0xFF }, "1");

        RunUntilFrame(drive, 40);

        Assert.Equal(24, drive.CallFramesFromS().First());
        Assert.Equal(new[] { 25 }, drive.EndFramesFromS());
    }

    /// <summary>T-D19: Aida, <c>1A 04 ; 1C 03</c>: a Chain of 16 ticks onto a Loop of 10 ticks: the Chain end, then two turns (s + 16 + 1, ...).</summary>
    [Fact]
    public void TD19_AChainOf16Ticks_ThenALoopOf10Ticks()
    {
        var anims = new[] { Chain(4, 0.32f, 0), Loop(0, 0.2f) };
        var drive = NpcWithTickProgram(anims, 0, new[] { 0x01, 0x1A, 0x04, 0x1C, 0x03, 0xFF }, "1");

        RunUntilFrame(drive, 45);

        Assert.Equal(new[] { 37 }, drive.EndFramesFromS());
    }

    /// <summary>T-D20: the switch of map 113, <c>1A 04 ; 1C 01 ; 37 0A ; 00 ; 1C 01</c>: a Chain of 15 ticks onto a Loop of 10 ticks, a wait of 11 ticks,
    /// then a second wait for one turn of the Loop that is playing.</summary>
    [Fact]
    public void TD20_TwoWaitsInARow_TheSecondOnTheLoopTheChainLeadsTo()
    {
        var anims = new[] { Chain(4, 0.29999998f, 1), Loop(1, 0.2f), Loop(0, 0.2f) };
        var drive = NpcWithTickProgram(anims, 0, new[] { 0x01, 0x1A, 0x04, 0x1C, 0x01, 0x37, 0x0A, 0x00, 0x1C, 0x01, 0xFF }, "1");

        RunUntilFrame(drive, 45);

        Assert.Equal(new[] { 16, 36 }, drive.EndFramesFromS());
    }

    // ----------------------------------------------------------------------------------------------------------
    // Map-event programs (B), on the hero
    // ----------------------------------------------------------------------------------------------------------

    /// <summary>T-D8: the hero on map 163, <c>1A 53 ; 1C 03 ; 1A 55 ; 1C 01 ; 1A 00</c>: s + 99, then s + 140.</summary>
    [Theory]
    [InlineData("1", 99, 140)]
    [InlineData("60", 118, 168)]
    public void TD8_HeroMapProgram_TwoWaits(string pattern, int firstEnd, int secondEnd)
    {
        var drive = HeroWithMapProgram(Hero, 0, new[] { 0x01, 0x1A, 0x53, 0x1C, 0x03, 0x1A, 0x55, 0x1C, 0x01, 0x1A, 0x00, 0xFF }, pattern);

        RunUntilFrame(drive, secondEnd + 5);

        Assert.Equal(new[] { firstEnd, secondEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D9: Jess, a map-event program <c>1A 0C ; 1C 01</c>: a Chain of 24 ticks, seen at s + 25.</summary>
    [Theory]
    [InlineData("1", 25)]
    [InlineData("60", 30)]
    public void TD9_JessMapProgram_ChainOf24Ticks(string pattern, int expectedEnd)
    {
        var drive = HeroWithMapProgram(Jess, 0, new[] { 0x01, 0x1A, 0x0C, 0x1C, 0x01, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    /// <summary>T-D10: a map-event program <c>1A L ; 1C n</c> on a Loop of 10 ticks, from a Loop of 7 ticks.</summary>
    [Theory]
    [InlineData("1", 1, 11)]
    [InlineData("1", 2, 21)]
    [InlineData("1", 3, 31)]
    [InlineData("60", 1, 13)]
    [InlineData("60", 2, 25)]
    [InlineData("60", 3, 37)]
    public void TD10_MapProgram_LoopOf10Ticks_NTurns(string pattern, int turns, int expectedEnd)
    {
        var drive = HeroWithMapProgram(LoopsOf7And10, 3, new[] { 0x01, 0x1A, 0x07, 0x1C, turns, 0xFF }, pattern);

        RunUntilFrame(drive, expectedEnd + 5);

        Assert.Equal(new[] { expectedEnd }, drive.EndFramesFromS());
    }

    // ----------------------------------------------------------------------------------------------------------
    // The tick owed to a switch, the freeze gate, the hero without a controller, the loop of the hero's idle, a missing animation
    // ----------------------------------------------------------------------------------------------------------

    private static readonly Anim[] ThreeHolds = { Hold(0, 0.8f), Hold(1, 0.8f), Hold(2, 0.8f) };

    /// <summary>T-D11: the target is 1 after frame 3, frame 4 has no tick, the script of frame 5 sets the target 2: the tick owed to the
    /// switch of frame 4 is cleared by the switch the script makes before its step.</summary>
    [Fact]
    public void TD11_TheOwedTick_IsClearedByASwitchTheScriptMakesBeforeTheStep()
    {
        var drive = new Drive(ThreeHolds, 0, new[] { 0x01, 0x1A, 0x02, 0xFF }, f => f == 4 ? 0 : 1);
        var observed = new List<(int Frame, uint Animation, int Tick)>();

        drive.RunTo(10, frame =>
        {
            if (frame == 3)
            {
                drive.Proxy.TargetAnimationId = 1;
            }

            if (frame == 4)
            {
                drive.EnableTickProgram();
            }

            if (frame >= 4)
            {
                observed.Add((frame, drive.Proxy.CurrentAnimationId, drive.Sprite.LogicalTick));
            }
        });

        Assert.Equal(
            new[] { (4, 1u, 0), (5, 2u, 0), (6, 2u, 1), (7, 2u, 2), (8, 2u, 3) },
            observed.Take(5));
    }

    /// <summary>T-D12: the target is 1 after frame 3, frames 4 to 9 have the ticks 0, 1, 1, 1, 1, 1: the first tick after a switch made on a
    /// frame without tick is the tick the switch owes (the new animation shows its frame 0 without counting).</summary>
    [Fact]
    public void TD12_ASwitchMadeOnAFrameWithoutATick_OwesItsTickToTheNextFrame()
    {
        var pattern = new[] { 1, 1, 1, 1, 0, 1, 1, 1, 1, 1 };
        var drive = new Drive(ThreeHolds, 0, new[] { 0x01, 0xFF }, f => pattern[f]);
        var observed = new List<(int Frame, int Tick)>();

        drive.RunTo(10, frame =>
        {
            if (frame == 3)
            {
                drive.Proxy.TargetAnimationId = 1;
            }

            if (frame >= 4)
            {
                observed.Add((frame, drive.Sprite.LogicalTick));
            }
        });

        Assert.Equal(new[] { (4, 0), (5, 0), (6, 1), (7, 2), (8, 3), (9, 4) }, observed);
    }

    /// <summary>T-D13: T-D1 with <c>MenuOpen</c> posed on the frames s + 11 to s + 20: nothing advances while the game is frozen.</summary>
    [Fact]
    public void TD13_WhileTheGameplayIsFrozen_TheLogicalClockStops()
    {
        var drive = NpcWithTickProgram(Ronan, 0, new[] { 0x01, 0x1A, 0x06, 0x1C, 0x01, 0x1A, 0x00, 0xFF }, "1");
        var ticksOfFrozenFrames = new List<int>();

        drive.RunTo(S + 40, frame =>
        {
            if (frame == S + 10)
            {
                drive.Host.GameState.PlayerControlFlags |= AlundraGameState.PlayerControlBits.MenuOpen;
            }

            if (frame >= S + 11 && frame <= S + 20)
            {
                ticksOfFrozenFrames.Add(drive.Sprite.LogicalTick);
            }

            if (frame == S + 20)
            {
                drive.Host.GameState.PlayerControlFlags &= ~AlundraGameState.PlayerControlBits.MenuOpen;
            }
        });

        Assert.Equal(
            "ticks of the frozen frames 10 x 10; ends s+35",
            $"ticks of the frozen frames {string.Join(",", ticksOfFrozenFrames.Distinct())} x {ticksOfFrozenFrames.Count}; ends s+{string.Join(",", drive.EndFramesFromS())}");
    }

    /// <summary>T-D14: a hero without a controller, on a Loop of 10 ticks: the clock still runs (the binary animates it whatever its input).</summary>
    [Fact]
    public void TD14_AHeroWithoutAController_StillAdvancesItsLogicalClock()
    {
        var drive = new Drive(new[] { Loop(0, 0.2f) }, 0, new[] { 0x01, 0xFF }, _ => 1, isPlayer: true);
        var ticks = new List<int>();
        var counterAfterFrame30 = -1;

        drive.RunTo(36, frame =>
        {
            ticks.Add(drive.Sprite.LogicalTick);
            if (frame == 30)
            {
                counterAfterFrame30 = drive.Proxy.AnimCompleteCounter;
            }
        });

        var ticksAreKModTen = ticks.SequenceEqual(Enumerable.Range(0, 36).Select(k => k % 10));
        Assert.Equal(
            "counter 3 after frame 30; ticks k mod 10",
            $"counter {counterAfterFrame30} after frame 30; ticks {(ticksAreKModTen ? "k mod 10" : "all " + ticks.Max())}");
    }

    /// <summary>T-D15: map 396, a map-event program <c>1D 01</c> waits on the hero's idle animation (a Loop of 54 ticks played from frame 0):
    /// the turn is counted at the tick it happens, whatever the frame of the first call.</summary>
    [Theory]
    [InlineData(0, 54)]
    [InlineData(1, 54)]
    [InlineData(10, 54)]
    [InlineData(53, 54)]
    [InlineData(54, 108)]
    [InlineData(55, 108)]
    [InlineData(100, 108)]
    public void TD15_AMapEventWaitOnTheHeroIdleLoop_EndsOnTheTurn(int firstCallFrame, int expectedFrame)
    {
        var drive = new Drive(new[] { Loop(0, 1.0799999f) }, 0, new[] { 0x01, 0x1D, 0x01, 0xFF }, _ => 1, isPlayer: true);
        drive.MapProgramFromFrame = firstCallFrame;

        drive.RunTo(expectedFrame + 5);

        Assert.Equal(new[] { expectedFrame }, drive.Calls.Where(c => c.Result == 2).Select(c => c.Frame).ToArray());
    }

    /// <summary>T-D16: a Flame of the lair of Nirude asks for an animation its prefab does not have (<c>1A 09</c>): the animation that plays
    /// starts again from its frame 0, and the wait ends on its first turn, like the original that reads past its table.</summary>
    [Fact]
    public void TD16_AnAnimationTheEntityDoesNotHave_RestartsTheOneThatPlays()
    {
        var drive = new Drive(new[] { Loop(0, 0.24f) }, 0, new[] { 0x01, 0x1A, 0x09, 0x1C, 0x01, 0xFF }, _ => 1);
        const int start = 29; // any phase of the Loop of 12 ticks.
        drive.RunTo(start);
        drive.EnableTickProgram();
        var tickAtEndOfS = -1;
        var counterAfterTheWait = -1;

        drive.RunTo(start + 20, frame =>
        {
            if (frame == start)
            {
                tickAtEndOfS = drive.Sprite.LogicalTick;
            }

            if (frame == start + 13)
            {
                counterAfterTheWait = drive.Proxy.AnimCompleteCounter;
            }
        });

        Assert.Equal(0, tickAtEndOfS);
        Assert.Equal(new[] { start + 13 }, drive.Calls.Where(c => c.Result == 2).Select(c => c.Frame).ToArray());
        Assert.Equal(0, counterAfterTheWait);
    }

    /// <summary>T-D17: the step allocates nothing, wraps of the Loop (and the counter the bridge increments) included.</summary>
    [Fact]
    public void TD17_StepAnimationClock_AllocatesNothing()
    {
        var drive = new Drive(new[] { Loop(0, 0.2f) }, 0, new[] { 0x01, 0xFF }, _ => 1);
        drive.RunTo(5);
        AlundraFrameSyncPasses.StepAnimationClock(drive.Proxy); // once before the measure: nothing is lazily built inside it.
        var counterBefore = drive.Proxy.AnimCompleteCounter;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            AlundraFrameSyncPasses.StepAnimationClock(drive.Proxy);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0L, allocated);
        Assert.Equal(100, drive.Proxy.AnimCompleteCounter - counterBefore); // 1000 ticks of a Loop of 10 ticks: 100 turns, all counted.
    }
}
