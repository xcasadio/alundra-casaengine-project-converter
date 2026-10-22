#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Application.Components.Physics;
using CasaEngine.Framework.Assets;
using CasaEngine.Framework.Assets.Animations;
using CasaEngine.Framework.Configuration.Project;
using CasaEngine.Framework.UI.MGUI;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;
using static Alundra.Tests.Support.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Objects;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Rng;
using Alundra.Rendering.Backdrops;
using Alundra.Transitions;
using Alundra.Dialogues;

namespace Alundra.Tests.Support;

/// <summary>E19.d2a S3 (U3): a real portal arrival record - what <c>WarpDirector.BeginDepartureCore</c> writes and
/// <c>AdoptPlayerPawn</c> consumes. Positions are in 16.16 (<c>&lt;&lt; 16</c>) for X and Y and <c>&lt;&lt; 20</c> for Z
/// (<c>ZLevel &lt;&lt; 20</c>, not yet the ground height: the adoption raises it), the animation is <c>0x36</c> for a portal.</summary>
internal sealed record ArcArrival(int PosX, int PosY, int PosZ, uint AnimationId, uint DirectionId);
