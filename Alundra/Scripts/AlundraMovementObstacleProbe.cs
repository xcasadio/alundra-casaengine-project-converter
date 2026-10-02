#nullable enable
using System;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using Microsoft.Xna.Framework;

namespace Alundra.Scripts;

/// <summary>
/// E19.d2b B3 (docs/plan-e19-opcodes.md §1.2h.2, D-E19-27, D-E19-28, D-E19-34, ADR-0022): the movement obstacle probe the character
/// controller consults in its field stage (<see cref="IMovementObstacleProbe"/>, ADR-0047 of the engine), installed on
/// <c>World.MovementObstacleProbe</c> at each world load next to the cell field. It gives the controller the rule of the binary: a mobile
/// stops at the first entity of the collidable list its box overlaps (<see cref="AlundraEntityCollision.FindEntityCollisionCandidate(AlundraEntityScriptProxy,int,int,int,System.Collections.Generic.IReadOnlyList{AlundraEntityScriptProxy},uint)"/>,
/// <c>FindEntityCollisionCandidate</c> <c>0x80036FE0</c>, semi-open), the entity tested before the cell.
/// <para>
/// The candidate X and Y are the root the controller proposes, converted to 16.16 EXACTLY as <see cref="AlundraEntityScriptProxy.MoveControllerAndPullPosition"/>
/// pulls the position back (<c>Math.Round((double)root * 65536.0)</c>): the contact the controller finds by bisection is then the contact
/// of the fields, to the float resolution of the root (4 to 8 units of 16.16 between 512 and 1024 px, an accepted gap, O-E19-28). Z is the
/// LOGICAL <see cref="AlundraEntityScriptProxy.PosZ"/> of the mover, never the float root: the one-unit margin of a supported entity
/// (E4.f) is below the float resolution at 400 px. The soulevables (<c>Flags &amp; 0x600</c>) are not obstacles (D-E19-28, until the
/// grab and the blows exist, E14); every other collidable is, the iron-ball walls included (D-E19-34). A mover that already overlaps an
/// entity stays blocked (D-E19-36): no exit rule, as in the binary.
/// </para>
/// No allocation, no state: it reads <see cref="IAlundraScriptHost.Collidables"/>, rebuilt by the world at the end of each frame.
/// </summary>
public sealed class AlundraMovementObstacleProbe : IMovementObstacleProbe
{
    private static readonly Color DebugColor = new(255, 128, 0);

    private readonly IAlundraScriptHost _host;

    public AlundraMovementObstacleProbe(IAlundraScriptHost host)
    {
        _host = host;
    }

    public bool TryFindObstacle(Entity mover, in Vector3 candidateRootPosition, out Entity obstacle)
    {
        obstacle = null!;
        if (mover.GameplayProxy is not AlundraEntityScriptProxy proxy)
        {
            return false;
        }

        var candidateX = (int)Math.Round((double)candidateRootPosition.X * 65536.0);
        var candidateY = (int)Math.Round((double)candidateRootPosition.Y * 65536.0);
        var found = AlundraEntityCollision.FindEntityCollisionCandidate(
            proxy, candidateX, candidateY, proxy.PosZ, _host.Collidables, EntityFlags.PickupKindMask);
        if (found?.OwnerEntity is not { } foundEntity)
        {
            return false;
        }

        obstacle = foundEntity;
        return true;
    }

    /// <summary>The box of each entity of the list, in the logical pixels of the physics debug view (no allocation).</summary>
    public void DrawDebug(IPhysicsDebugDrawer drawer)
    {
        var collidables = _host.Collidables;
        for (var i = 0; i < collidables.Count; i++)
        {
            var entity = collidables[i];
            var min = new Vector3(
                (entity.PosX + entity.ModX) / 65536f,
                (entity.PosY + entity.ModY) / 65536f,
                (entity.PosZ + entity.ModZ) / 65536f);
            var max = new Vector3(
                (entity.PosX + entity.ModX + entity.Width + 1) / 65536f,
                (entity.PosY + entity.ModY + entity.Height + 1) / 65536f,
                (entity.PosZ + entity.ModZ + entity.Depth + 1) / 65536f);
            drawer.DrawAabb(min, max, DebugColor);
        }
    }
}
