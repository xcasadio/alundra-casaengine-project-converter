#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>The state after one logic tick: the hero, the targets in slot order, the latch of the hero's animation, the sparks the pass asked for this tick (centres) and the programs the events pass of this tick ran.</summary>
internal sealed record HitTickSample(
    int Tick, HitEntitySample Hero, IReadOnlyList<HitEntitySample> Targets, uint Latch, IReadOnlyList<(int X, int Y, int Z)> Sparks, IReadOnlyList<(int Slot, int Letter)> Handlers);
