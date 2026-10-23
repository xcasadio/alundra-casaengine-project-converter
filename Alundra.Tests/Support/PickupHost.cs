#nullable enable
using System.Collections.Generic;
using CasaEngine.Framework.AI.Navigation;
using Alundra.Audio;
using Alundra.Dialogues;
using Alundra.Entities;
using Alundra.Items;
using Alundra.Maps;
using Alundra.Rendering.Effects;

namespace Alundra.Tests.Support;

/// <summary>
/// E20.e0c T1: the smallest world context the loot and the pickups need - the apparition by sprite index (recorded, or refused when <see cref="TableFull"/>), the effect stage, the sound player, the item tables and the hero.
/// A spawned entity is a bare proxy whose flags are all raised, so that a test sees the bit a drop clears.
/// </summary>
internal sealed class PickupHost : IEntityWorldContext
{
    public sealed record SpawnCall(EntityScriptProxy? Parent, bool IsMapPage, int Index, int X, int Y, int Z, int Direction);

    public List<SpawnCall> Spawns { get; } = new();

    public List<EntityScriptProxy> Spawned { get; } = new();

    public bool TableFull { get; set; }

    public EffectStage? Stage { get; set; }

    public ISoundPlayer? Sound { get; set; }

    public ItemTables? Tables { get; set; }

    /// <summary>E20.e0c2: the director of the music the fanfare of a key object stops and restarts (null: none, as for a test host).</summary>
    public IBgmFadeDirector? BgmFadeDirector { get; set; }

    public EntityScriptProxy? Hero { get; set; }

    /// <summary>E20.e0c T2: the dialogue box the pickup asks whether it is open (null: no director).</summary>
    public IDialogueDirector? Dialogue { get; set; }

    /// <summary>E20.e0c T2: the entities handed to the light destruction, in order.</summary>
    public List<EntityScriptProxy> Destroyed { get; } = new();

    /// <summary>E20.e0c T2: the entities that called <c>Unblock(self)</c>, in order.</summary>
    public List<EntityScriptProxy> Unblocked { get; } = new();

    public IReadOnlyList<EntityScriptProxy> SpawnedEntities => Spawned;

    public EntityScriptProxy? PlayerEntity => Hero;

    public EntityScriptProxy? EntityFollowedByCamera { get; set; }

    public void SetForcedCameraLookAt(int x, int y, int z) => EntityFollowedByCamera = null;

    public EntityScriptProxy? SpawnEntityByRecordId(EntityScriptProxy logicEntity, int entityRecordId) => null;

    public EntityScriptProxy? SpawnEntityBySpriteIndex(EntityScriptProxy? parent, bool isMapPage, int index, int x, int y, int z, int direction)
    {
        Spawns.Add(new SpawnCall(parent, isMapPage, index, x, y, z, direction));
        if (TableFull)
        {
            return null;
        }

        var entity = new EntityScriptProxy { Flags = 0xFFFFFFFF, SpriteType = index };
        Spawned.Add(entity);
        return entity;
    }

    public void DestroyEntity(EntityScriptProxy entity) => Destroyed.Add(entity);

    public void Unblock(EntityScriptProxy self) => Unblocked.Add(self);

    /// <summary>E20.e0c T4: the entities that called <c>Block(self)</c>, in order.</summary>
    public List<EntityScriptProxy> Blocked { get; } = new();

    public void Block(EntityScriptProxy self) => Blocked.Add(self);

    public IDialogueDirector? DialogueDirector => Dialogue;

    public NavigationGrid2D? NavigationGrid => null;

    public EffectStage? EffectStage => Stage;

    public ISoundPlayer? SoundPlayer => Sound;

    public ItemTables? ItemTables => Tables;
}
