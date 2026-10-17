#nullable enable
using Alundra.Entities;
using Alundra.Scripts;

namespace Alundra.Rendering.Effects;

/// <summary>
/// E14.a1b T2 (docs/plan-e14-ia-native.md section 1.3b, ADR-0060): the anchor of the break effect of a destroyed entity, an immutable SNAPSHOT taken by <c>DestroyEntity</c> right after it wrote the status 4: the position
/// (X, Y, and the binary's Z, <c>PosZ + 1</c>, ADR-0026), the depth key of E20.f1b (<see cref="EntityDepthKey.Compute(AlundraEntityScriptProxy)"/>) and the sort coordinate of the sprite, at the moment of the destruction.
/// Why a snapshot and not the entity: the effect operations of a tick are applied by <see cref="EffectStage.RunFrame"/> at the end of the rendered frame (ADR-0046), and in a frame of two ticks or more the destroyed
/// entity has already been recycled to the status 0 (and may have been released) when the effect is created; a live anchor would then freeze the new slot without writing its position or its key
/// (<c>MoveFollower</c>, anchor status 0). In the binary the effect is created at once, while the entity still exists with status 4, and its first update copies this very position and key and freezes the slot (mode 2).
/// </summary>
internal sealed class FrozenEffectAnchor : IEffectAnchor
{
    private readonly int _sortCoordinate;

    private FrozenEffectAnchor(int posX, int posY, int posZ, uint depthKey, int sortCoordinate)
    {
        PosX = posX;
        PosY = posY;
        PosZ = posZ;
        DepthKey = depthKey;
        _sortCoordinate = sortCoordinate;
    }

    public int PosX { get; }

    public int PosY { get; }

    /// <summary>The binary's Z of the entity (<c>PosZ + 1</c>).</summary>
    public int PosZ { get; }

    public uint DepthKey { get; }

    /// <summary>Always 4 (flagged to be destroyed): the status <c>DestroyEntity</c> wrote at <c>0x8003A5BC</c>, so the first update of the effect copies the anchor and freezes the slot.</summary>
    public int Status => (int)EntityStatus.FlagToDestroy;

    public int SortCoordinate => _sortCoordinate;

    /// <summary>The snapshot of <paramref name="entity"/> as it is now (call it after the status was written).</summary>
    internal static FrozenEffectAnchor Of(AlundraEntityScriptProxy entity)
    {
        IEffectAnchor anchor = entity;
        return new FrozenEffectAnchor(anchor.PosX, anchor.PosY, anchor.PosZ, anchor.DepthKey, anchor.SortCoordinate);
    }
}
