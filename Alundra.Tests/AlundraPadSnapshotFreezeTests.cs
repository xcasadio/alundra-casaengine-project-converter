#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Alundra.Scripts;
using CasaEngine.Framework.Assets.TileMap;
using CasaEngine.Framework.Scene.Entities.Components;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Maps;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Events;
using Alundra.Hero;
using Alundra.Physics;
using Alundra.Audio;

namespace Alundra.Tests;

/// <summary>
/// Regression pin for the softlock T2 introduced and the user hit in play: with a MenuOpen dialogue box
/// up, NPCs stayed frozen and the player stayed uncontrollable even after the box went away.
///
/// Root cause: the pad snapshot (<see cref="GameState.LastPadState"/>) was published from INSIDE
/// the block T2 gates behind <see cref="GameState.PlayerControlBits.GameplayBlockedMask"/>. The
/// original never freezes its pad global: <c>PadManager.UpdatePads</c> runs from the main loop
/// (GameEngine.cs:1518) and even from inside the warp transition loop (GameEngine.cs:280), both outside
/// <c>UpdateEntities</c>. Freezing it froze the very input the dialogue box's own advance/close pass
/// consumes (<c>AlundraDialogueDirector.Tick</c> reads <c>LastPadState.ButtonsJustPressed</c>), so the box
/// could never be advanced by a fresh press - and MenuOpen, which only that close path clears, stayed
/// posted forever, keeping the whole entity pass frozen.
///
/// Driven through the real <c>world.Update</c> so the production call site is what is pinned, not a
/// mirror: the hero montage of <see cref="HeroWorldFixture"/> carries a real
/// <see cref="AlundraPlayerController"/> whose pad can be scripted.
/// </summary>
public sealed class AlundraPadSnapshotFreezeTests : IDisposable
{
    private const string WorldName = "Ship Klark (beginning)";

    public AlundraPadSnapshotFreezeTests()
    {
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
    }

    public void Dispose()
    {
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
    }

    [Fact]
    public void PadSnapshot_KeepsRefreshing_WhileGameplayIsBlockedByAMenuOpenBox()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return; // same self-skip as every other map-389 test when the export is absent.
        }

        var field = LoadMap389Field(LoadMap389TileMapData(projectRoot));
        var world = HeroWorldFixture.BuildWorld(field);

        var pad = new PadState();
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => pad };
        var host = new PadScriptHost { PlayerController = controller };
        HeroWorldFixture.BuildHeroPawn(world, new CharacterControllerSettings(), new Vector3(804f, 952f, 0f), host);

        // A dialogue box with controlMode 0 posts MenuOpen, which is what freezes the entity pass (T2).
        host.GameState.PlayerControlFlags |= GameState.PlayerControlBits.MenuOpen;

        // The player now presses the interact button - the press that must advance and eventually close
        // the box. It reaches the game state ONLY if the snapshot is published outside the freeze.
        pad = new PadState { ButtonsHold = PadState.Cross, ButtonsJustPressed = PadState.Cross };
        world.Update(0.02f);

        Assert.Equal(PadState.Cross, host.GameState.LastPadState.ButtonsJustPressed);
    }

    [Fact]
    public void PadSnapshot_StillRefreshes_WhenGameplayIsNotBlocked()
    {
        var projectRoot = FindProjectRoot();
        if (projectRoot == null)
        {
            return;
        }

        var field = LoadMap389Field(LoadMap389TileMapData(projectRoot));
        var world = HeroWorldFixture.BuildWorld(field);

        var pad = new PadState();
        var controller = new AlundraPlayerController { PadStateProviderForTests = () => pad };
        var host = new PadScriptHost { PlayerController = controller };
        HeroWorldFixture.BuildHeroPawn(world, new CharacterControllerSettings(), new Vector3(804f, 952f, 0f), host);

        pad = new PadState { ButtonsHold = PadState.Up, ButtonsJustPressed = PadState.Up };
        world.Update(0.02f);

        // Unchanged behaviour on the open path - the snapshot was already published here before T2.
        Assert.Equal(PadState.Up, host.GameState.LastPadState.ButtonsJustPressed);
    }

    private sealed class PadScriptHost : IScriptHost
    {
        public IEventProgramRunner Runner { get; } = new NoOpRunner();
        public AlundraEntityScriptProxy? ActiveCollisionEntity { get; set; }
        public GameState GameState { get; } = new();
        public AlundraPlayerController? PlayerController { get; init; }
        public IReadOnlyList<AlundraEntityScriptProxy> Collidables { get; } = Array.Empty<AlundraEntityScriptProxy>();

        public void DestroyEntity(AlundraEntityScriptProxy entity, int effectId)
        {
        }

        public int LogicTicksThisFrame(float elapsedTime) => 1;
    }

    private sealed class NoOpRunner : IEventProgramRunner
    {
        public void RunScript(AlundraEntityScriptProxy entity, int programSlot)
        {
        }

        public void RunSpriteEvent(AlundraEntityScriptProxy entity)
        {
        }
    }

    private static string? FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "alundra-project");
            if (Directory.Exists(Path.Combine(candidate, "Maps")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static TileMapData LoadMap389TileMapData(string projectRoot)
    {
        var tileMapPath = Path.Combine(
            projectRoot, "Maps", "The Klark", "Ship Klark (beginning)-389", "tilemap",
            "Ship Klark (beginning)-389.tileMap");
        var tileMapData = new TileMapData();
        tileMapData.Load(JObject.Parse(File.ReadAllText(tileMapPath)));
        return tileMapData;
    }

    private static CellsCollisionField LoadMap389Field(TileMapData tileMapData)
    {
        var created = CellsCollisionField.TryCreate(tileMapData, WorldName, out var field);
        Assert.True(created, "map 389 AlundraCells custom property should parse and match MapSize.");
        return field!;
    }
}
