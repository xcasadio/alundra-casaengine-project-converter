#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Ai.Objects;
using Alundra.Entities;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Enemies;

/// <summary>
/// E20.e0c T3 (docs/plan-e20-heros.md section 1.26, phase C, (d2)): the loot half of the E1 oracle tests of the Beannoid and of the Muruta. E1 is <c>DestroyEntity(e, -1)</c>, and its first act is the drop
/// (<c>spawn_39F58</c>: the type, the position and the Z of the object that appears, then the sound 84). The oracle's journal says what the real code did; this helper makes the DLL's world answer it: the REAL world
/// of the map 14 with its real prefabs and the catalogue (<see cref="PickupDropRun"/>, the only kind of world that makes an object appear), the hero without magic as in the oracle (the veto of <c>0x80032A00</c>), and the
/// entities and the sounds the call added compared with the entries of the journal. The compared half is not "nothing happened" any more: 80 drops, 48 vetoes and 16 empty contents are told apart by the oracle.
/// </summary>
internal static class EnemyDeathLoot
{
    /// <summary>The world of a test: the map 14, every entity but the hero removed, the hero without magic (<c>MpMax == 0</c>), nothing playing.</summary>
    public static PickupDropRun NewWorld(string name)
    {
        var run = PickupDropRun.OnMap14(name);
        GameState.Instance.PlayerStats.MpMax = 0;
        return run;
    }

    /// <summary>What the world holds before the call; given back to <see cref="Compare"/>.</summary>
    public static HashSet<EntityScriptProxy> Snapshot(PickupDropRun run)
    {
        run.Sounds.Requests.Clear();
        return new HashSet<EntityScriptProxy>(run.Context.SpawnedEntities, ReferenceEqualityComparer.Instance);
    }

    /// <summary>
    /// Compares the drop with the oracle's journal: as many objects as <c>spawn_39F58</c> entries (0 or 1), and for each the type (the binary's index), the position, the Z (the container's, raised by the raw unit of the apparition, D-E0A-1) and the kind "dropped"
    /// (<c>+0x274 == 1</c>); the sounds asked are the journal's <c>sound</c> entries, in order, after the sound of the appearance of the object (if the object has one). Removes the object born so that the next call starts clean.
    /// Returns the number of objects that appeared.
    /// </summary>
    public static int Compare(string where, PickupDropRun run, HashSet<EntityScriptProxy> before, IReadOnlyList<HandlerJournalEntry> journal, EntityScriptProxy container)
    {
        var spawns = journal.Where(j => j.Kind == "spawn_39F58").ToList();
        var sounds = journal.Where(j => j.Kind == "sound").Select(j => (int)j.Arguments[0]).ToList();
        var born = run.Context.SpawnedEntities.Where(e => !before.Contains(e)).ToList();
        Xunit.Assert.True(born.Count == spawns.Count, $"{where}: the oracle dropped {spawns.Count} object(s), the DLL {born.Count}");
        Xunit.Assert.True(spawns.Count == sounds.Count, $"{where}: the journal has {spawns.Count} drops and {sounds.Count} sounds");
        if (spawns.Count == 1)
        {
            var a = spawns[0].Arguments;
            var b = born[0];
            Xunit.Assert.True(a[0] == 0 && a[1] == 0 && a[6] == 0, $"{where}: the oracle's drop has a parent, a map page or a direction");
            Xunit.Assert.True(b.SpriteType == a[2], $"{where}: the object has the type {b.SpriteType}, the oracle's {a[2]}");
            Xunit.Assert.True(b.PosX == (int)a[3] && b.PosY == (int)a[4], $"{where}: the object is at ({b.PosX}, {b.PosY}), the oracle's ({a[3]}, {a[4]})");
            Xunit.Assert.True(a[5] == unchecked(container.PosZ + 1), $"{where}: the oracle's Z of the drop is {a[5]}, the container's (the binary's, the DLL's plus one raw unit) is {unchecked(container.PosZ + 1)}");
            // The world is the map 14, not the map of the oracle's call: where its floor is above the container the apparition lifts the object to it (the apparition is E20.e0a1's, pinned by its own tests).
            Xunit.Assert.True(b.PosZ >= unchecked(container.PosZ + 1), $"{where}: the object is at Z {b.PosZ}, below the container's plus one raw unit {unchecked(container.PosZ + 1)}");
            Xunit.Assert.True(PickupAi.ReadWord274(b) == 1, $"{where}: the object is not the kind 'dropped'");
        }

        Xunit.Assert.True(spawns.Count == 1 || run.Sounds.Requests.Count == 0, $"{where}: nothing dropped, yet the sounds are [{string.Join(",", run.Sounds.Requests)}]");
        Xunit.Assert.True(run.Sounds.Requests.Skip(run.Sounds.Requests.Count - sounds.Count).SequenceEqual(sounds), $"{where}: the sounds are [{string.Join(",", run.Sounds.Requests)}], the oracle's end with [{string.Join(",", sounds)}]");
        foreach (var entity in born)
        {
            run.Context.DestroyEntity(entity);
        }

        run.Arc.Proxy.RecycleDestroyedEntities();
        return born.Count;
    }
}
