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

/// <summary>What an arc loads and runs (E19.a T5, docs/plan-e19-opcodes.md §1.3): one real exported map, the
/// flags already set, the hero placed, and the frame budget after which the arc FAILS naming where each program
/// stopped.</summary>
/// <param name="Name">The arc's name in the plan (A0, A0b, A1...), quoted by its failure message.</param>
/// <param name="Zone">The folder of the map under <c>Maps/</c> (<c>The Klark</c>).</param>
/// <param name="WorldName">The world's own name (<c>Ship Klark (inner)-390</c>), also its folder name.</param>
/// <param name="Flags">The persistent game flags set before the first frame (<c>G866</c> is 866).</param>
/// <param name="HeroTileX">The hero's tile, set with its position before the first frame.</param>
/// <param name="HeroTileY">See <paramref name="HeroTileX"/>.</param>
/// <param name="HeroTileZ">See <paramref name="HeroTileX"/>; <c>TileZ = PosZ &gt;&gt; 20</c> exactly.</param>
/// <param name="FrameLimit">The frame budget of the arc.</param>
/// <param name="RealController">E19.a2: the hero is a real <see cref="CharacterControllerComponent"/> set like the
/// export's, and every frame is the world's own <c>Update</c> (which moves the entities), so walks meet the map's
/// cells and rails. Off (the default), the entities are bare and <see cref="ArcRun.OneFrame"/> updates them one by
/// one.</param>
/// <param name="Prefabs">E19.c1 (D-E19-14): the NPCs are the export's real prefabs, loaded by the production spawn path
/// through an <see cref="AssetContentManager"/> built by the test (<see cref="ArcPrefabAssets"/>). Requires
/// <see cref="RealController"/>. Off (the default), every NPC is a bare entity.</param>
/// <param name="Arrival">E19.d2a S3 (U3): when given, the arc starts from a real portal arrival: the record is posed on the warp
/// director before the map entry, <c>AdoptPlayerPawn</c> (and its <c>ClampToGround</c>) places the hero as in production, and the
/// arc writes neither the hero's position nor its <c>Tile*</c> (<see cref="HeroTileX"/> to <see cref="HeroTileZ"/> are then unused).</param>
internal sealed record ArcSpec(
    string Name, string Zone, string WorldName, int[] Flags, int HeroTileX, int HeroTileY, int HeroTileZ, int FrameLimit,
    bool RealController = false, bool Prefabs = false, ArcArrival? Arrival = null);
