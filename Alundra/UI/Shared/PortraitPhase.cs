using Alundra.Dialogues;

namespace Alundra.UI.Shared;

/// <summary>Where a portrait flight is at the pass just run (E19.f4b): the flight in, the rest, the flight back, or the last (degenerate) pass of the return.</summary>
public enum AlundraPortraitPhase
{
    /// <summary>Nothing drawn (idle).</summary>
    None,

    /// <summary>Flying from the head to the rest position, growing.</summary>
    In,

    /// <summary>At rest, full size.</summary>
    Rest,

    /// <summary>Flying back to the head, shrinking.</summary>
    Out,

    /// <summary>The last pass of a return: a 0x0 quad at the rest position, then nothing.</summary>
    Gone,
}
