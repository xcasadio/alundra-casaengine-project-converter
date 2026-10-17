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

    /// <summary>Liftable only while the player does not own item 0x3B.</summary>
    LiftableUnlessItem3B = 2,

    /// <summary>
    /// Same gate as <see cref="LiftableUnlessItem3B"/>, but the player switches to the hold /
    /// jump-with-object animations while carrying it.
    /// </summary>
    LiftableHeavy = 3
}
