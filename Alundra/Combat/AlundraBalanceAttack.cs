#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Framework.Assets.TileMap;

namespace Alundra.Combat;

/// <summary>One attack of a balance record: the attribute byte (low nibble = the attack type, bit 7 = the power adds the equipment's) and the power byte. A reference type on purpose: the binary's
/// <c>CurrentAttack</c> (<c>+0x1C8</c>) is a pointer to one of these two-byte entries, and "none" is a null pointer.</summary>
public sealed class AlundraBalanceAttack
{
    public AlundraBalanceAttack(int attribute, int power)
    {
        Attribute = attribute & 0xFF;
        Power = power & 0xFF;
    }

    /// <summary>The attribute byte (<c>AttackAttribute</c> of the export).</summary>
    public int Attribute { get; }

    /// <summary>The power byte.</summary>
    public int Power { get; }
}
