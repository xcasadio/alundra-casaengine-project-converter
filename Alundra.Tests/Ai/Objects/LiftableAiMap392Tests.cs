#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Session;
using Alundra.Tests.Arcs;
using Alundra.Tests.Audio;
using Alundra.Tests.Support;
using Alundra.Transitions;

namespace Alundra.Tests.Ai.Objects;

/// <summary>
/// E20.g1a T4 (docs/plan-e20-heros.md section 1.24.1, phase C, (r2)): a whole map of crates at rest, the real exported map 392 (Ship Klark, night, inner) with the real prefabs, keeps every field of every liftable object for 100 frames once C23 is
/// registered for them (the handler runs, and does nothing while nothing is carried: the registration is inert at rest), next to a crate that IS carried and released by the code of the throw, which C23 does change (animation, code, links, flags).
/// Written before the line of <c>NativeSpriteRegistry.CreateForProduction</c>.
/// </summary>
[Collection(MusicPlayerSingletonCollection.Name)]
public sealed class LiftableAiMap392Tests : IDisposable
{
    private const int Frames = 100;

    /// <summary>The clocks that every entity advances at every tick whatever its program (measured: they are the only six that differ); everything else must be equal.</summary>
    private static readonly string[] Clocks = { "field AnimCompleteCounter", "field BoxTicks", "field FrameCounter", "field MotionTickCount", "field PhysicsDoneInPass", "field SupportTickCount" };

    public LiftableAiMap392Tests()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        WarpDirector.Instance.ResetForTests();
    }

    private static ArcSpec Spec(string name) => new(
        name, "The Klark", "Ship Klark (night, inner, break)-392", new[] { 1641 }, 29, 13, 4, 400, RealController: true, Prefabs: true);

    private static List<EntityScriptProxy> Crates(ArcRun arc) => arc.Entities.Where(e => LiftableAi.SpriteTypes.Contains(e.SpriteType)).ToList();

    /// <summary>Every simple readable property and field of the proxy (numbers, booleans, enums): the state of the object, read the same way before and after.</summary>
    private static Dictionary<string, object?> Fields(EntityScriptProxy entity)
    {
        var fields = new Dictionary<string, object?>();
        foreach (var property in typeof(EntityScriptProxy).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var type = property.PropertyType;
            if (property.GetIndexParameters().Length != 0 || property.GetMethod == null || !(type.IsPrimitive || type.IsEnum))
            {
                continue;
            }

            fields[property.Name] = property.GetValue(entity);
        }

        foreach (var field in typeof(EntityScriptProxy).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (field.FieldType.IsPrimitive || field.FieldType.IsEnum)
            {
                fields["field " + field.Name] = field.GetValue(entity);
            }
        }

        fields["PlatformEntity"] = entity.PlatformEntity == null ? null : "set";
        fields["RidingEntity"] = entity.RidingEntity == null ? null : "set";
        return fields;
    }

    [Fact]
    public void R2_TheCratesAtRestOfTheMap392_KeepEveryFieldFor100Frames_WhileTheHandlerRunsForThem()
    {
        using var arc = new ArcRun(Spec("E20g1a-r2-rest"));
        arc.OneFrame();
        var crates = Crates(arc);
        Assert.NotEmpty(crates);
        var before = crates.Select(Fields).ToList();

        for (var i = 0; i < Frames; i++)
        {
            arc.OneFrame();
        }

        Assert.Equal(crates, Crates(arc));
        var changed = new SortedSet<string>();
        for (var i = 0; i < crates.Count; i++)
        {
            var after = Fields(crates[i]);
            foreach (var (name, value) in before[i])
            {
                if (!Clocks.Contains(name) && !Equals(value, after[name]))
                {
                    changed.Add(name);
                }
            }
        }

        Assert.True(changed.Count == 0, string.Join(", ", changed));

        // the handler ran for them (the registration is there), and found nothing carried.
        Assert.True(arc.Runner.NativeHandlerRunCounts.TryGetValue((ScriptHelper.ProgramCTick, LiftableAi.Code), out var runs), "C23 never ran");
        Assert.True(runs >= crates.Count * Frames / 2, $"C23 ran {runs} times for {crates.Count} crates");
    }

    [Fact]
    public void R2_ACarriedCrateOfTheSameMap_IsReleasedByTheCodeOfTheThrow_WhichChangesItsFields()
    {
        using var arc = new ArcRun(Spec("E20g1a-r2-carried"));
        arc.OneFrame();
        var crate = Crates(arc).First();
        var before = Fields(crate);
        PlayerManager.PutInHands(arc.Hero, crate);
        crate.Flags2 = -1; // the code that mode 2 writes (a wound, deep water, a death).
        Assert.NotNull(crate.PlatformEntity);

        arc.OneFrame();

        var after = Fields(crate);
        Assert.Null(crate.PlatformEntity);
        Assert.Equal(0, crate.Flags2);
        Assert.Equal(3u, crate.TargetAnimationId);
        Assert.Null(arc.Hero.CarriedEntity);
        Assert.NotEqual(before["field Flags"], after["field Flags"]);
    }
}
