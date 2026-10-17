#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using Alundra.Session;
using Alundra.Scripts;
using Alundra.Hero;

namespace Alundra.Entities;

/// <summary>
/// One <see cref="SpriteRecordHeader.AnimSets"/> entry - a bank's <c>SiAnimationSet</c> header fields
/// (converter E2-A, <c>SpriteBankReader.ReadAnimSetHeader</c>), read-only mirror of
/// <c>Data/sprite-records.json</c>'s per-AnimSet JSON object.
/// </summary>
public readonly struct AnimSetEntry
{
    /// <summary>The AnimSet index this entry describes (matches <see cref="AlundraEntityScriptProxy.TargetAnimationId"/>'s
    /// domain) - also the dictionary key in <see cref="SpriteRecordHeader.AnimSets"/>, duplicated here so
    /// a caller iterating the dictionary's values still has it.</summary>
    public int Anim { get; init; }

    /// <summary>Walk speed, pre-multiplied by <see cref="AnimationTables.OffsetXList"/>/<see cref="AnimationTables.OffsetYList"/>
    /// in <see cref="PlayerManager"/>'s kinematic tick (<c>PhysicsEngine.UpdateEntityPhysics</c>,
    /// PhysicsEngine.cs:1593-1594).</summary>
    public int Speed { get; init; }

    /// <summary>Right-shift amount applied to the force step (<c>&amp; 0xf</c> at the call site,
    /// PhysicsEngine.cs:1591/1596-1597) - larger values mean slower acceleration toward the target
    /// speed.</summary>
    public int Acceleration { get; init; }

    /// <summary>Not consumed by V1's kinematic tick (no Z-axis movement yet) - carried for a future
    /// jump/fall chantier.</summary>
    public int IsZForceApplied { get; init; }

    /// <summary>Sound-effect id associated with this AnimSet (e.g. footstep sfx) - not consumed yet (no
    /// audio system, E11's own scope).</summary>
    public int Sfx { get; init; }

    /// <summary>Raw AnimSet flags byte, meaning not yet reverse-engineered beyond <see cref="IsZForceApplied"/>.</summary>
    public int Flags { get; init; }

    /// <summary>Unidentified trailing field of the source AnimSet header - carried for completeness.</summary>
    public int Unknown { get; init; }
}
