using System.Collections.Generic;

namespace Alundra.Entities;

/// <summary>
/// Value of the 2 bit <see cref="EntityFlags.PickupKindMask"/> field, as decoded by
/// <c>PlayerManager.PlayerTryInteractWithEntity</c> @ 0x8002EDBC.
/// </summary>
public enum EntityPickupKind
{
    /// <summary>The player cannot lift this entity at all.</summary>
    NotLiftable = 0,

    /// <summary>The player can always lift this entity.</summary>
    Liftable = 1,

    /// <summary>
    /// Liftable only when the player OWNS item 0x3B (the power glove: <c>GetNumberOfItem(0x3B)</c> at <c>0x8002EDF8</c>, E20.g1a); once in the hands it is carried like a kind 1 (walks, jumps). The name of the member
    /// predates the reading of the binary and says the opposite of its gate.
    /// </summary>
    LiftableUnlessItem3B = 2,

    /// <summary>
    /// Same gate as <see cref="LiftableUnlessItem3B"/> (the player owns item 0x3B), but heavy: once in the hands the hero keeps the standing state of the carrying
    /// (0x0C on the ground, 0x30 in the air); the pad ignores the direction and the Cross (<c>0x8003241C</c>, E20.g1a).
    /// </summary>
    LiftableHeavy = 3
}
