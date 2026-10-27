#nullable enable
using System;
using System.Linq;
using System.Reflection;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Tests.Support;
using Xunit;

namespace Alundra.Tests.Hero;

/// <summary>
/// E20.h4a T1 (docs/plan-e20-heros.md section 1.31.1, the bench of the tests of the states and of the oracle): the real world of map 14 built by the production path of the arcs (<see cref="ArcRun"/>: real prefabs, the real
/// controller of the hero with the animations of the export, the world's own <c>Update</c>), a flat field of height 0 (<see cref="SyntheticField"/>), every entity of the map but the hero removed, the hero put at a pixel of the
/// field, ONE logic tick per frame. The sounds are told to a recorder in place of the world's player. The magic is the real one (<see cref="ArcRun.SetMp"/>), the objects are given by the real giver (<see cref="ArcRun.Give"/>).
/// </summary>
internal sealed class TomeBench : IDisposable
{
    internal const uint Circle = PadState.Circle;

    internal readonly ArcRun Arc;
    internal readonly RecordingSoundPlayer Sounds = new();

    /// <param name="name">The name of the arc (the failure message quotes it).</param>
    /// <param name="mpMax">The maximum of the magic (the ceiling is 4).</param>
    /// <param name="mp">The magic.</param>
    /// <param name="items">The items given (the tomes <c>0x2B</c> to <c>0x32</c> are 43 to 50, the staff is 7).</param>
    internal TomeBench(string name, int mpMax = 4, int mp = 4, params int[] items)
    {
        Arc = new ArcRun(new ArcSpec("E20h4a-" + name, "Overworld", "Overworld 3,1-14", Array.Empty<int>(), 8, 6, 0, 4000, RealController: true, Prefabs: true));
        try
        {
            Frames(4);
            SyntheticField.Install(Arc, null);
            foreach (var other in Arc.Entities.Where(e => !ReferenceEquals(e, Arc.Hero)).ToList())
            {
                other.Status = EntityStatus.FlagToDestroy;
            }

            Arc.Proxy.RecycleDestroyedEntities();
            typeof(WorldProxy).GetField("<SoundPlayer>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Arc.Proxy, Sounds);
            Arc.PlaceHero(100, 100, 0);
            Frames(6);
            Assert.Equal(0u, Hero.TargetAnimationId);
            Assert.Equal(1, Hero.IsOnGround);
            Arc.Give(items);
            Arc.SetMp(mpMax, mp);
            Hero.FrameCollisionTickCounter = 0;
            Hero.DamagedTickCounter = 0;
            Hero.HitCounter = 0;
            Hero.TouchingEntity = null;
            Sounds.Requests.Clear();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal EntityScriptProxy Hero => Arc.Hero;

    internal static PlayerStats Stats => GameState.Instance.PlayerStats;

    /// <summary>The weapon the hero holds, the binary's <c>0x8004E030</c>.</summary>
    internal uint WeaponId => PlayerManager.GetItemIdFromCurrentWeapon(GameState.Instance, ((IScriptHost)Arc.Proxy).ItemTables!);

    /// <summary>Ticks with nothing pressed.</summary>
    internal void Frames(int count)
    {
        for (var i = 0; i < count; i++)
        {
            Arc.HoldDirections(0);
            Arc.OneFrame();
        }
    }

    /// <summary>One tick with the pad <paramref name="hold"/>: a lone Circle (or any lone button) is an edge, the directions are held.</summary>
    internal void Tick(uint hold = 0)
    {
        if (hold == Circle || hold == PadState.Square || hold == PadState.Cross || hold == PadState.Triangle)
        {
            Arc.HoldDirections(0);
            Arc.Press(hold);
        }
        else
        {
            Arc.HoldDirections(hold);
            Arc.OneFrame();
        }
    }

    public void Dispose()
    {
        Arc.HoldDirections(0);
        Arc.Dispose();
    }
}
