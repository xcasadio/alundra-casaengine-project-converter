#nullable enable
using System;
using System.Reflection;
using CasaEngine.Framework.Application;
using CasaEngine.Framework.Rendering.CellularLayers;
using CasaEngine.Framework.Rendering.ScrollingLayers;
using CasaEngine.Framework.Scene.Entities;
using Xunit;
using World = CasaEngine.Framework.Scene.World.World;
using Alundra.Session;
using Alundra.Entities;
using Alundra.Rng;
using Alundra.Rendering.Backdrops;
using Alundra.Audio;
using Alundra.Transitions;
using Alundra.Maps;
using Alundra.Tests.Maps;
using Alundra.Tests.Rng;

namespace Alundra.Tests.Rendering.Backdrops;

internal static class BackdropWarpDepartureSupport
{
    internal static void ResetSingletons()
    {
        GameState.Instance.ResetForTests();
        SpriteRecordCatalog.ResetForTests();
        SfxBank.ResetForTests();
        WarpDirector.Instance.ResetForTests();
        ScreenFadeDirector.Instance.ResetForTests();
        BgmPlayer.Instance.ResetForTests();
        BgmFadeDirector.Instance.ResetForTests();
    }

    /// <summary>Arms a departure through portal 0 of map 389 (the pattern of <c>AlundraWarpDepartureTests</c>), on a
    /// non-degraded montage: the director is attached to a real <see cref="GameManager"/> so that the gate stays posted
    /// through the 16 frames.</summary>
    internal static EntityScriptProxy BeginDeparture(WorldProxy proxy)
    {
        var entity = new Entity { Name = "player", GameplayProxyClassName = typeof(EntityScriptProxy).FullName! };
        entity.Initialize();
        var player = (EntityScriptProxy)entity.GameplayProxy;
        player.IsPlayer = true;
        player.Status = EntityStatus.Normal;
        player.PosX = (18 * 24 + 12) << 16;
        player.PosY = (38 * 16 + 8) << 16;
        player.PosZ = 0;
        player.ScriptHost = proxy;
        proxy.PlayerEntity = player;

        WarpDirector.Instance.AttachToWorld(
            new GameManager(null), soundPlayer: null, WorldProxyGlobalFreezeTests.FindProjectRoot());

        var portal = new PortalRecord
        {
            Index = 0,
            X1 = 18,
            Y1 = 38,
            X2 = 18,
            Y2 = 38,
            DestMapId = 390,
            DestTileX = 10,
            DestTileY = 40,
            ZLevel = 0,
            Flags = 0x5001,
        };

        WarpDirector.Instance.BeginDeparture(portal, 0x10, player, proxy.GameState);
        Assert.True(WarpDirector.Instance.IsTransitionInProgress);
        return player;
    }

    internal static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
