#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alundra.Ai.Objects;
using Alundra.Audio;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Tests.Support;
using Xunit;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g2b (docs/plan-e20-heros.md section 1.24.3, tests (b2) to (b7)): the test hook of E2. The registry of the runner holds the key (E, 2) once phase B is in; before it, a test registers it itself, through the disposable that
/// <see cref="LiftableAi.RegisterBreak"/> returns, and ONLY when no type of the ten has the key (a second registration of a key throws). The same test is valid before and after phase B, without exception; <c>hook: false</c> is the "null hook" of the
/// production tests (T2): nothing is registered by the test, the production registry alone decides.
/// </summary>
internal static class BreakHook
{
    /// <summary>Registers E2 in the registry of <paramref name="runner"/> when it does not hold it; the disposable removes what this call registered (a no-op when the key was already there).</summary>
    internal static IDisposable Install(EventProgramRunner runner)
    {
        var registry = runner.NativeSprites;
        return LiftableAi.BreakSpriteTypes.Any(type => registry.TryGet(ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode, type, out _))
            ? new Nothing()
            : LiftableAi.RegisterBreak(registry);
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>
/// E20.g2b T1 (docs/plan-e20-heros.md section 1.24.3, "Revision au HEAD" point g2b): the bench of the world tests of the break. The REAL world of an exported map (the arcs' production path: real prefabs, the world's own Update, the effect stage, the
/// sound player, the slot table), emptied of every entity but the hero, on a synthetic field (<see cref="SyntheticField"/>, the cells of the scenario), the native handlers OFF and the arena switch of D-F4-7 on for the ten types of E2 only
/// (<see cref="EventProgramRunner.NativeSpriteTypeFilterForTests"/>: the crates run C23 and E2, a Beannoid stays inert). The crate is born by the interface (<c>SpawnEntityBySpriteIndex</c>, map page) on the head of the hero and put in his hands by
/// <see cref="PlayerManager.PutInHands"/>; the Square is pressed at the tick 1 (T), the real hero throws it through <see cref="CarryRules.ThrowCarried"/>. Everything the tests compare is recorded here, tick by tick.
/// </summary>
internal sealed class BreakRun : IDisposable
{
    private const uint Square = PadState.Square;
    private const float OneTick = 0.02f;

    private readonly IDisposable _hook;
    private int _tick;
    private bool _recording;
    private int _heroX;
    private int _heroY;

    internal readonly ArcRun Arc;
    internal readonly CutBench Bench;
    internal readonly Sounds Heard = new();

    /// <summary>The crate (or jar) of the scenario; stays valid after the entity is recycled.</summary>
    internal EntityScriptProxy Crate = null!;

    /// <summary>The second object born at rest (see <c>restAt</c>), or null.</summary>
    internal EntityScriptProxy? Rest;

    /// <summary>The entities of the world when the ticks start; what the break makes appear is the difference with the world after them.</summary>
    internal IReadOnlySet<EntityScriptProxy> EntitiesBefore = new HashSet<EntityScriptProxy>();

    /// <summary>The target born next to it (a Beannoid), or null.</summary>
    internal EntityScriptProxy? Target;

    /// <summary>The state of the crate at the end of each tick (the proxy stays valid after the recycling).</summary>
    internal readonly Dictionary<int, CrateTick> Ticks = new();

    /// <summary>The state of the hero at the end of each tick.</summary>
    internal readonly Dictionary<int, (uint State, bool Carries)> Hero = new();

    /// <summary>The number of sparks the hit pass made at each tick.</summary>
    internal readonly Dictionary<int, int> Sparks = new();

    /// <summary>The effects attached to an anchor (a break effect) the stage held at the end of a frame: (the tick that ends the frame, the effect).</summary>
    internal readonly List<(int Tick, int Effect)> AnchoredEffects = new();

    /// <summary>The last tick that ran (the last frame is whole: it can end after the tick asked).</summary>
    internal int LastTick;

    internal readonly record struct CrateTick(
        bool Present, int Slot, EntityStatus Status, int X, int Y, int Z, uint Target, uint Current, uint Flags, int Flags2, int ResetFlag, bool Touching, int HitCounter, int OnGround, int E2Calls, int C23Calls);

    /// <summary>
    /// The world of the scenario. <paramref name="index"/>: the index of the map page of the object (36 the crate, 41 the jar), born on the head of the hero at (<paramref name="heroX"/>, <paramref name="heroY"/>) and put in his hands.
    /// <paramref name="cell"/>: the cells of the field (null: all zero). <paramref name="targetX"/>: a Beannoid (index 79) born at that pixel of the row of the hero, or null. <paramref name="hook"/>: the test registers E2 when the registry lacks it.
    /// <paramref name="contents"/>: the item the crate holds (<see cref="EntityScriptProxy.ContentsItemId"/>, drawn at its creation) when the scenario fixes it, instead of the draw. <paramref name="restAt"/>: a second object of the same page born at rest on that pixel.
    /// </summary>
    internal BreakRun(
        string name, string zone, string worldName, int index, Func<int, int, CutBench.Cell>? cell = null, int heroX = 109, int heroY = 100, int? targetX = null, bool hook = true, bool carry = true, int crateX = 0, int crateY = 0, uint? contents = null, (int X, int Y)? restAt = null)
    {
        var spec = new ArcSpec("E20g2b-" + name, zone, worldName, Array.Empty<int>(), 8, 6, 0, 2000, RealController: true, Prefabs: true);
        Arc = new ArcRun(spec);
        try
        {
            _heroX = heroX;
            _heroY = heroY;
            Frames(4);
            Bench = SyntheticField.Install(Arc, cell);
            foreach (var other in Arc.Entities.Where(e => !ReferenceEquals(e, Arc.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Heard);
            Arc.Runner.NativeHandlersEnabled = false;
            EventProgramRunner.NativeSpriteTypeFilterForTests = new HashSet<int>(LiftableAi.BreakSpriteTypes);
            _hook = hook ? BreakHook.Install(Arc.Runner) : new NoHook();

            Arc.Hero.TargetDirection = 24;
            Arc.PlaceHero(heroX, heroY, 0);
            Frames(2);

            var world = (IEntityWorldContext)Arc.Proxy;
            if (targetX is { } tx)
            {
                Target = world.SpawnEntityBySpriteIndex(null, true, 79, tx << 16, heroY << 16, 0, 0x10)
                    ?? throw new InvalidOperationException("the target was not born");
            }

            Crate = world.SpawnEntityBySpriteIndex(null, true, index, (carry ? heroX : crateX) << 16, (carry ? heroY : crateY) << 16, carry ? 32 << 16 : 0, 0x10)
                ?? throw new InvalidOperationException("the crate was not born");
            if (contents is { } item)
            {
                Crate.ContentsItemId = item;
            }

            if (restAt is { } rest)
            {
                Rest = world.SpawnEntityBySpriteIndex(null, true, index, rest.X << 16, rest.Y << 16, 0, 0x10)
                    ?? throw new InvalidOperationException("the object at rest was not born");
            }

            if (carry)
            {
                Crate.RidingEntity = Arc.Hero.LogicContextEntity; // the object stands on the head of the hero: the lift gives the carrying stand at once.
                PlayerManager.PutInHands(Arc.Hero, Crate);
            }

            Frames(carry ? 8 : 6);
            if (carry)
            {
                Assert.Equal(12u, Arc.Hero.TargetAnimationId);
                Assert.NotNull(Arc.Hero.CarriedEntity);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private sealed class NoHook : IDisposable
    {
        public void Dispose()
        {
        }
    }

    /// <summary>The sounds the world played, with the tick of the sampling.</summary>
    internal sealed class Sounds : ISoundPlayer
    {
        internal int Tick;
        internal readonly List<(int Tick, int Sfx)> Played = new();

        public void PlaySfx(int sfxId) => Played.Add((Tick, sfxId));

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

    /// <summary>Frames of one tick with nothing pressed (the run is not recording).</summary>
    private void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>
    /// Plays the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> ticks; the pad of a frame is <paramref name="pad"/> of the tick that opens it (default: Square at the tick 1, the front of the throw).
    /// </summary>
    internal void Go(int ticksPerFrame, int lastTick, Func<int, uint>? pad = null)
    {
        pad ??= tick => tick == 1 ? Square : 0u;
        EntitiesBefore = new HashSet<EntityScriptProxy>(Arc.Entities, ReferenceEqualityComparer.Instance);
        Arc.Proxy.TickTraceForTests = OnPass;
        Arc.Proxy.HitSparksForTests = (_, _, _) => _sparksOfTheTick++;
        _tick = 0;
        _recording = true;
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arc.HoldDirections(pad(tick));
            Arc.OneFrame(ticksPerFrame * OneTick);
            LastTick = tick + ticksPerFrame - 1;
            foreach (var slot in Arc.Proxy.EffectStage!.Reservoir.ActiveSlots.Where(s => s.Anchor != null))
            {
                AnchoredEffects.Add((LastTick, slot.TargetEffect));
            }
        }

        _recording = false;
        Arc.Proxy.TickTraceForTests = null;
    }

    private int _sparksOfTheTick;

    private void OnPass(string pass)
    {
        if (!_recording)
        {
            return;
        }

        if (pass == "pad")
        {
            _tick++;
            Heard.Tick = _tick;
            _sparksOfTheTick = 0;
        }
        else if (pass == "inventory")
        {
            var counts = Arc.Runner.NativeHandlerRunCounts;
            Ticks[_tick] = new CrateTick(
                Arc.Entities.Contains(Crate), Crate.Slot, Crate.Status, Crate.PosX, Crate.PosY, Crate.PosZ, Crate.TargetAnimationId, Crate.CurrentAnimationId, Crate.Flags, Crate.Flags2, Crate.ForceResetAnimationFlag,
                Crate.TouchingEntity != null, Crate.HitCounter, Crate.IsOnGround,
                counts.GetValueOrDefault((ScriptHelper.ProgramEDeactivate, LiftableAi.BreakCode)), counts.GetValueOrDefault((ScriptHelper.ProgramCTick, LiftableAi.Code)));
            Hero[_tick] = (Arc.Hero.TargetAnimationId, Arc.Hero.CarriedEntity != null);
            Sparks[_tick] = _sparksOfTheTick;
        }
    }

    public void Dispose()
    {
        _hook?.Dispose();
        EventProgramRunner.NativeSpriteTypeFilterForTests = null;
        Arc.Dispose();
    }
}
