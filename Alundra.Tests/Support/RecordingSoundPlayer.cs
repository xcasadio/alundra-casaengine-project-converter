#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Physics;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Entities.Spawning;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Audio;

namespace Alundra.Tests.Support;

/// <summary>A recorder of the sounds the hero's take-off asks for (R8).</summary>
internal sealed class RecordingSoundPlayer : ISoundPlayer
{
    public List<int> Requests { get; } = new();

    /// <summary>E20.g5c: the sounds the stop of one sound (<see cref="ISoundPlayer.StopSfx"/>) was asked for, in order.</summary>
    public List<int> StopRequests { get; } = new();

    public void PlaySfx(int sfxId) => Requests.Add(sfxId);

    public void StopSfx(int sfxId) => StopRequests.Add(sfxId);

    public void RemixVoice(int sfxId, int left, int right)
    {
    }

    public void FlushFrameSounds()
    {
    }

    public void StopAllSfx()
    {
    }
}
