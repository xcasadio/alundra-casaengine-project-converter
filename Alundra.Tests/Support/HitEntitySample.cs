#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Alundra.Events;
using Alundra.Entities;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>What one entity looks like after the passes of one logic tick (the columns of the hit oracle, E20.c1a C5), read from the DLL.</summary>
internal readonly record struct HitEntitySample(
    uint AnimTarget, int AnimByte, bool HasBox, int BoxOffsetX, int BoxOffsetY, int BoxOffsetZ, int BoxWidth, int BoxDepth, int BoxHeight, int AttackAttribute, int AttackPower,
    int Touching, int HitCounter, int DamagedTicks, int CooldownTicks, int Hp, int PosX, int PosY);
