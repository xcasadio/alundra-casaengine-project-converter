#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Alundra.Maps;
using Alundra.Entities;
using Alundra.Ai.Projectiles;
using Alundra.Hero;
using Alundra.Tests.Support;

namespace Alundra.Tests.Ai.Projectiles;

/// <summary>What one run of the wave in the arena leaves: the line of the wave at every tick from <see cref="WaveOracle.FirstTick"/> to <see cref="WaveOracle.LastTick"/>, the entity once seen, the table of slots.</summary>
internal sealed class WaveRun : IDisposable
{
    private const uint Square = PadState.Square;

    internal readonly HitArena Arena;
    internal readonly Dictionary<int, string> Lines = new();
    internal EntityScriptProxy? Born;
    internal int FirstTick;
    internal int LowestFreeSlotBeforeBirth = -1;
    internal readonly List<(int Tick, EntityScriptProxy Wave)> Present = new();

    /// <summary>The arena of the charge: the hero at (212, <paramref name="heroY"/>) facing <paramref name="direction"/> (the word of <c>TargetDirection</c>: 24 east, 8 west, 16 north, 0 south) with the sword of the item <paramref name="item"/>
    /// (2 steel, 3 demon, 4 holy) in the slot 1; the Beannoids at the pixels <paramref name="targets"/>.</summary>
    internal WaveRun(string name, int item, uint direction, IReadOnlyList<int[]>? targets = null, int heroY = 96)
    {
        Arena = new HitArena("f3c-" + name, 212, heroY, targets?.ToList() ?? new List<int[]>(), direction);
        Arena.Arc.Give(item);
        Arena.Arc.Equip(1);
    }

    /// <summary>The entity of the wave if one exists in the world now: a sprite 27 to 29 whose parent is the hero.</summary>
    internal EntityScriptProxy? Find()
        => Arena.Arc.Entities.FirstOrDefault(e => WaveSwordAi.SpriteTypes.Contains(e.SpriteType) && ReferenceEquals(e.ParentEntity, Arena.Arc.HeroEntity));

    /// <summary>
    /// Runs the ticks 1 to <paramref name="lastTick"/> in frames of <paramref name="ticksPerFrame"/> (the Square held to tick 90, released at 91). With <paramref name="useHook"/> the shot is born by the test hook, from the exact place of
    /// <c>MovePlayer</c>; without it, by the production seam. <paramref name="afterTick"/> is told every tick after its inventory pass.
    /// </summary>
    internal void Go(int ticksPerFrame, bool useHook, int lastTick = WaveOracle.LastTick, Action<int>? afterTick = null)
    {
        if (useHook)
        {
            PlayerManager.WeaponSpawnBirthHookForTests = (hero, call) =>
                ((IEntityWorldContext)Arena.Arc.Proxy).SpawnEntityBySpriteIndex(hero, false, call.Type, call.X, call.Y, call.Z, call.Direction);
        }

        Arena.AfterTick = tick =>
        {
            if (Born is null)
            {
                var lowest = Enumerable.Range(0, EntitySlotTable.SlotCount).FirstOrDefault(i => !Arena.Arc.Proxy.SlotTable.IsHeld(i), -1);
                Born = Find();
                if (Born is not null)
                {
                    FirstTick = tick;
                }
                else
                {
                    LowestFreeSlotBeforeBirth = lowest;
                }
            }

            var present = Born is not null && Arena.Arc.Entities.Contains(Born);
            if (present)
            {
                Present.Add((tick, Born!));
            }

            if (tick is >= WaveOracle.FirstTick and <= WaveOracle.LastTick)
            {
                Lines[tick] = WaveOracle.Describe(Born, Arena.Hero, present);
            }

            afterTick?.Invoke(tick);
        };

        Arena.Begin();
        for (var tick = 1; tick <= lastTick; tick += ticksPerFrame)
        {
            Arena.Frame(tick <= 90 ? Square : 0, ticksPerFrame);
        }
    }

    public void Dispose() => Arena.Dispose();
}
