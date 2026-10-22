#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CasaEngine.Core.Logging;
using CasaEngine.Engine.Environment;
using CasaEngine.Engine.Geometry;
using CasaEngine.Engine.Physics;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Gameplay;
using CasaEngine.Framework.Scene.Entities;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using CasaEngine.Framework.SaveGames;
using Microsoft.Xna.Framework.Input;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Cameras;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Dialogues;
using Alundra.UI.Hud;
using Alundra.UI.Inventory;
using Alundra.UI.Saving;
using Alundra.UI.Shared;
using Alundra.UI.SubInventory;
using Alundra.SaveGames;
using Alundra.Maps;

namespace Alundra.Tests.Support;

/// <summary>E16.d T3/T4 (K6 and K7 step 1): one precondition of the recipe keys, posed on its own by
/// <see cref="SaveGameDirectorTestSupport.PoseBlocker"/>.</summary>
public enum SaveGameBlocker
{
    NoHero,
    HeroInTheAir,
    DialogueOpen,
    InventoryOpen,
    SubInventoryOpen,
    PostProcessPending,
    PortraitOpening,
    PortraitAtRest,
    PortraitReturning,
    TransitionInProgress,
    MasterMusicFadeArmed,
    ControlFlagsNonZero,
}
