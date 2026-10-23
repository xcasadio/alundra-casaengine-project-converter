#nullable enable
using System.Collections.Generic;
using System.Linq;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Maps;
using Alundra.Session;

namespace Alundra.Tests.Events;

/// <summary>
/// E20.g5b T1 (docs/plan-e20-heros.md section 1.24.6; rule R-SM-4 of docs/plan-e19-opcodes.md): the bare runner world of the opcode tests, the montage M1 of
/// E19.sm3. The hero is the first entity of <see cref="IEntityWorldContext.SpawnedEntities"/> AND the <see cref="IEntityWorldContext.PlayerEntity"/>
/// (<c>IsPlayer</c>, <c>EntityRefId</c> -1, <c>Normal</c>: a bare proxy has 0); every proxy, the hero included, has its own non-null
/// <see cref="EntityScriptProxy.LogicContextEntity"/> (without it the search types 5, 6, 9 and 10 reduce to <c>null == null</c>); the program is the
/// instruction followed by <c>0xFF</c>, run by <see cref="EventProgramRunner.RunOneScriptCall"/> with a <c>Result</c> of 7 before it (neither 0 nor 1).
/// </summary>
internal sealed class OpcodeWorld
{
    public const int ResultBefore = 7;

    /// <summary>The world context of the bare runner (the one of <c>ClassAndBranchOpcodesTests</c>).</summary>
    public sealed class Context : IEntityWorldContext
    {
        public List<EntityScriptProxy> Spawned { get; } = new();
        public IReadOnlyList<EntityScriptProxy> SpawnedEntities => Spawned;
        public EntityScriptProxy? PlayerEntity { get; set; }
        public EntityScriptProxy? EntityFollowedByCamera { get; set; }
        public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;
        public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;
        public void DestroyEntity(EntityScriptProxy entity) { }
        public NavigationGrid2D? NavigationGrid => null;
    }

    public Context Ctx { get; } = new();

    public GameState GameState { get; } = new();

    /// <summary>The slots of the world by index (slot 0 is the hero); a free slot is null and is not in <see cref="IEntityWorldContext.SpawnedEntities"/>.</summary>
    public List<EntityScriptProxy?> Slots { get; } = new();

    public EntityScriptProxy Hero => Slots[0]!;

    public OpcodeWorld()
    {
        var hero = NewProxy(-1, EntityStatus.Normal, 0x80, "hero");
        hero.IsPlayer = true;
        Slots.Add(hero);
        Ctx.Spawned.Add(hero);
        Ctx.PlayerEntity = hero;
    }

    public static EntityScriptProxy NewProxy(int refId, EntityStatus status, uint flags, string name) => new()
    {
        EntityRefId = refId,
        Status = status,
        Flags = flags,
        LogicContextEntity = new Entity { Name = name },
    };

    /// <summary>Adds an entity in the next slot (and in the spawned list, in slot order).</summary>
    public EntityScriptProxy Add(int refId, EntityStatus status = EntityStatus.Normal, uint flags = 0x80, string? name = null)
    {
        var proxy = NewProxy(refId, status, flags, name ?? "e" + Slots.Count);
        Slots.Add(proxy);
        Ctx.Spawned.Add(proxy);
        return proxy;
    }

    /// <summary>Marks the next slot as free: nothing is created for it.</summary>
    public void AddHole() => Slots.Add(null);

    /// <summary>Runs <paramref name="instruction"/> followed by <c>0xFF</c> on <paramref name="owner"/> (whose logic entity is <see cref="EntityScriptProxy.LogicEntity"/> when set).</summary>
    public (EventProgramState State, List<EventTraceRecord> Trace) Run(EntityScriptProxy owner, int resultBefore, params int[] instruction)
    {
        var codes = instruction.Concat(new[] { 0xFF }).ToArray();
        var document = new EventProgramDocument { MapIndex = 1, EventCodesATable = new int[6], Codes = codes };
        var runner = new EventProgramRunner(document, GameState, Ctx);
        var trace = new List<EventTraceRecord>();
        runner.TraceSink = trace.Add;
        var state = new EventProgramState { Codes = document.CodesAsBytes(), Result = resultBefore, CodeIndex = 0 };
        runner.RunOneScriptCall(owner, state);
        return (state, trace);
    }

    /// <summary>The record the first instruction left in the trace (<c>CodeIndex</c> 0).</summary>
    public static EventTraceRecord First(List<EventTraceRecord> trace) => trace.First(t => t.CodeIndex == 0);

    /// <summary>
    /// Everything an opcode that only writes <c>Result</c> must leave alone: the fields of every entity the handlers of the slice read or could write. Two snapshots are equal when nothing moved.
    /// </summary>
    public string Snapshot()
    {
        var parts = new List<string>();
        for (var i = 0; i < Slots.Count; i++)
        {
            var e = Slots[i];
            if (e is null)
            {
                parts.Add($"slot{i}:hole");
                continue;
            }

            parts.Add($"slot{i}:ref{e.EntityRefId} st{(int)e.Status} fl{e.Flags:X} af{e.AnimFlags} fz{e.ForceZ} ta{e.TargetAnimationId} td{e.TargetDirection} la{e.LastTargetAnimationId} ld{e.LastTargetDirection}"
                + $" xc{Slots.IndexOf(e.XCollisionEntity)} logic{(e.LogicEntity is null ? "-" : Slots.IndexOf(e.LogicEntity).ToString())} pos{e.PosX},{e.PosY},{e.PosZ}");
        }

        return string.Join("|", parts);
    }

    /// <summary>Asserts the trace shows the opcode run as implemented, with the size of the table, at pc 0.</summary>
    public static void AssertImplemented(List<EventTraceRecord> trace, int opcode, int size)
    {
        var first = First(trace);
        Assert.Equal(opcode, first.Opcode);
        Assert.Equal(EventTraceKind.Implemented, first.Kind);
        Assert.Equal(size, first.Size);
    }
}
