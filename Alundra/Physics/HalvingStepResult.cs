#nullable enable

namespace Alundra.Physics;

/// <summary>The outcome of <see cref="HalvingStepSearch.Search"/>: the accumulated offset of the accepted sub-steps (16.16 raw units) and whether at least one sub-step was accepted.</summary>
public readonly record struct HalvingStepResult(int X, int Y, bool Accepted);
