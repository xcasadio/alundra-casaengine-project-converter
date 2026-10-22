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

/// <summary>One instruction of the arc's trace: the frame it ran in, the program it belongs to (slot and start
/// code index, the identity a trace record can give without its owner), and its outcome.</summary>
internal readonly record struct ArcInstruction(int Frame, int Slot, int ProgramStart, int Pc, int Opcode, EventTraceKind Kind);
