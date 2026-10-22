#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CasaEngine.Framework.AI.Navigation;
using CasaEngine.Framework.SaveGames;
using CasaEngine.Framework.UI;
using Xunit;
using static Alundra.Tests.Support.SaveGameDirectorTestSupport;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Ai.Objects;
using Alundra.Hero;
using Alundra.Items;
using Alundra.Audio;
using Alundra.Dialogues;
using Alundra.UI.Inventory;
using Alundra.UI.Saving;
using Alundra.UI.Speech;
using Alundra.UI.SubInventory;
using Alundra.SaveGames;
using Alundra.Tests.Support;
using Alundra.Tests.Maps;
using Alundra.Tests.Ai.Objects;
using Alundra.Tests.Audio;

namespace Alundra.Tests.UI.Saving;

/// <summary>E16.e T3/T4: a headless world with a game whose active render view carries a given UI runtime - the
/// recipe of <c>AlundraDialoguePresenterWiringTests</c>.</summary>
internal static class SaveScreenTestWorlds
{
    internal static World WorldWithUIView(string name, IUIViewRuntime uiView)
    {
        var world = new World { Name = name };
        var game = (CasaEngine.Framework.Application.CasaEngineGame)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Application.CasaEngineGame));
        var componentsField = typeof(Microsoft.Xna.Framework.Game)
            .GetField("_components", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        componentsField.SetValue(game, new Microsoft.Xna.Framework.GameComponentCollection());

        var gameManager = (CasaEngine.Framework.Application.GameManager)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Application.GameManager));
        var viewManager = new CasaEngine.Framework.Rendering.ViewManager();
        typeof(CasaEngine.Framework.Application.GameManager)
            .GetField("<ViewManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(gameManager, viewManager);
        typeof(CasaEngine.Framework.Application.CasaEngineGame)
            .GetField("<GameManager>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(game, gameManager);

        var view = (CasaEngine.Framework.Rendering.RenderView)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CasaEngine.Framework.Rendering.RenderView));
        view.UIView = uiView;
        view.Enabled = true;
        view.IsVisible = true;
        viewManager.Add(view);
        viewManager.SetActive(view);

        HeroWorldFixture.SetProperty(world, nameof(World.Game), game);
        return world;
    }
}
