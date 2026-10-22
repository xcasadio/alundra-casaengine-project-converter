#nullable enable
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Alundra.Tests.Arcs.ArcChecks;
using Alundra.Entities;
using Alundra.Tests.Support;

namespace Alundra.Tests.Arcs;

/// <summary>The state of the world at one instruction (E19.d2b): the hero and each watched record, in 16.16 (the first entity of the record that
/// is not flagged for destruction, else the first).</summary>
internal sealed record ArcSnap(int Frame, (int X, int Y, int Z) Hero, Dictionary<int, (int X, int Y, int Z, EntityStatus Status, int ForceAdjusted)> Records)
{
    public (int X, int Y, int Z, EntityStatus Status, int ForceAdjusted) Rec(int record) => Records[record];
}
