#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Rendering.EffectQuads;
using CasaEngine.Framework.Scene.Entities.Components;
using Alundra.Cameras;
using Alundra.Scripts;

namespace Alundra.Rendering.Effects;

/// <summary>
/// The effect stage of a world (E19.g G3a, docs/plan-e19-opcodes.md section 1.2o.6, ADR-0045): the bank read from the exported companions, the reservoir, the load-time
/// spawn, the queue of operations and the per-tick loop. Owned by <see cref="AlundraWorldProxy"/> as a FIELD INITIALIZER like <c>_backdropStage</c> (trap 9: <c>Clone()</c> returns a bare
/// proxy); one world, one reservoir (the binary clears the pool at every map load, <c>0x8003C1A4</c>, and an effect is never saved). The opcodes are G3b; the draw is E19.g G3c (last paragraph).
/// <para>
/// <b>Load-time spawn</b> (<see cref="SpawnLoadEffects"/>): once, at the first <c>Update</c> of the proxy, before its tick loop and before any queued operation is applied, with the tile of
/// the hero captured when the hero was created (<see cref="CaptureHeroTile"/>, in <c>InitializeWithWorld</c> right after <c>AdoptPlayerPawn</c>): the binary creates the hero and writes its
/// tile before <c>0x8003C1A4</c>, and the scripts and the physics of the hero rewrite the tile from the tick 0. Without a hero the tile is (0, 0), with one warning.
/// </para>
/// <para>
/// <b>Queue</b> (<see cref="Queue"/>): the scripts of the entities run before the proxy in the engine's order (the reverse of the binary's), so an operation on the reservoir is queued with
/// the logic tick that emits it and a source rank, and applied in <see cref="RunFrame"/> before the update of that tick, in the order (tick, rank, emission). An operation of a frozen
/// tick is applied too (the binary writes the reservoir at once; only the update is skipped). What is left at the end of the frame (a frame without tick, a tick beyond the frame) is applied
/// in the same order without update: the queue is empty when <see cref="RunFrame"/> returns.
/// </para>
/// <para>
/// <b>Draw</b> (E19.g G3c, ADR-0049): the stage pushes the quads of the active slots to the engine's <see cref="EffectQuadService"/> (<see cref="AttachService"/>, <see cref="PushFrame(Camera2dComponent?)"/>):
/// the two sheets of the bank at <see cref="Load(AlundraEffectBank)"/> (or at <see cref="AttachService"/> when a bank is already loaded), the frame at every rendered frame. The construction is
/// <see cref="AlundraEffectQuadBuilder"/>. Without a service (headless montages) nothing is built or logged.
/// </para>
/// </summary>
public sealed class AlundraEffectStage
{
    private readonly List<PendingOperation> _queue = new();
    private AlundraEffectBank _bank = AlundraEffectBank.Empty;
    private AlundraEffectReservoir _reservoir;
    private int _heroTileX;
    private int _heroTileY;
    private bool _heroTileCaptured;
    private bool _loadSpawned;
    private bool _warnedNoHero;
    private EffectQuadService? _quadService;
    private bool _bankLoaded;
    private readonly List<EffectQuad> _quads = new();
    private readonly Guid[] _sheetIds = new Guid[2];
    private long _sequence;
    private int _sourceTick;
    private int _sourceRank;

    public AlundraEffectStage()
    {
        _reservoir = new AlundraEffectReservoir(_bank, Logs.WriteWarning);
    }

    /// <summary>The reservoir of the world: the active slots, in the order of the slots, are what G3c draws.</summary>
    public AlundraEffectReservoir Reservoir => _reservoir;

    /// <summary>True once <see cref="CaptureHeroTile"/> has been called since the last load.</summary>
    public bool HeroTileCaptured => _heroTileCaptured;

    /// <summary>Loads the bank of the world named <paramref name="worldName"/> from the exported project (<see cref="AlundraEffectBankLoader"/>) and starts a fresh reservoir.</summary>
    public void Load(string? worldName, string? projectPath) => Load(AlundraEffectBankLoader.Load(projectPath, worldName));

    /// <summary>Starts a fresh reservoir over <paramref name="bank"/> (the world's own, or a test's): nothing is spawned, queued or captured any more.</summary>
    public void Load(AlundraEffectBank bank)
    {
        _bank = bank;
        _bankLoaded = true;
        _reservoir = new AlundraEffectReservoir(bank, Logs.WriteWarning);
        _queue.Clear();
        _sourceTick = 0;
        _sourceRank = 0;
        _heroTileCaptured = false;
        _loadSpawned = false;
        _warnedNoHero = false;
        PushSheets();
    }

    /// <summary>Attaches (or detaches, with <see langword="null"/>) the engine's effect quad service (G3C-R5), before or after <see cref="Load(AlundraEffectBank)"/>: when a bank is already loaded its
    /// sheets are pushed at once, exactly as <see cref="Load(AlundraEffectBank)"/> does. Public so that a harness outside the repository can drive the stage.</summary>
    public void AttachService(EffectQuadService? service)
    {
        _quadService = service;
        PushSheets();
    }

    /// <summary>
    /// Pushes this frame's quads to the attached service (G3C-R5): the slots are walked by index, the buffers are reused, an empty frame is pushed when nothing is active. Called by
    /// <see cref="AlundraWorldProxy.Update"/> at every rendered frame, whatever the number of ticks (a tick frozen by <c>0x48</c>, a warp departure and a frame without tick included).
    /// <paramref name="camera"/> gives the original scroll for the elimination (<see cref="CameraMath.ToOriginalScrollSpace"/>); without a camera nothing is eliminated. A null service does nothing.
    /// </summary>
    public void PushFrame(Camera2dComponent? camera)
    {
        if (camera == null)
        {
            PushFrameAt(false, 0, 0);
            return;
        }

        var scroll = CameraMath.ToOriginalScrollSpace(camera.Target);
        PushFrameAt(true, scroll.X, scroll.Y);
    }

    internal void PushFrameAt(bool hasScroll, int scrollX, int scrollY)
    {
        var service = _quadService;
        if (service == null)
        {
            return;
        }

        _quads.Clear();
        AlundraEffectQuadBuilder.Build(_reservoir.Slots, hasScroll, scrollX, scrollY, _quads);
        service.SetFrame(CollectionsMarshal.AsSpan(_quads));
    }

    /// <summary><c>Clear</c> then <c>SetSheets(map, global)</c> on the attached service, once a bank is loaded; the ids are the companions' strings, <see cref="Guid.Empty"/> for a null or unreadable one.</summary>
    private void PushSheets()
    {
        var service = _quadService;
        if (service == null || !_bankLoaded)
        {
            return;
        }

        _sheetIds[AlundraEffectQuadBuilder.MapSheet] = ParseSheetIdOrEmpty(_bank.Map?.SheetTextureAssetId);
        _sheetIds[AlundraEffectQuadBuilder.GlobalSheet] = ParseSheetIdOrEmpty(_bank.Global?.SheetTextureAssetId);
        service.Clear();
        service.SetSheets(_sheetIds);
    }

    private static Guid ParseSheetIdOrEmpty(string? assetIdString) => Guid.TryParse(assetIdString, out var guid) ? guid : Guid.Empty;

    /// <summary>The tile of the hero when it was created, the one the zone test of the load-time spawn reads.</summary>
    public void CaptureHeroTile(int tileX, int tileY)
    {
        _heroTileX = tileX;
        _heroTileY = tileY;
        _heroTileCaptured = true;
    }

    /// <summary>The map-entry spawn (<see cref="AlundraEffectReservoir.SpawnAtLoad"/>), once: later calls do nothing.</summary>
    public void SpawnLoadEffects()
    {
        if (_loadSpawned)
        {
            return;
        }

        _loadSpawned = true;
        var tileX = _heroTileX;
        var tileY = _heroTileY;
        if (!_heroTileCaptured)
        {
            tileX = 0;
            tileY = 0;
            if (!_warnedNoHero && _bank.Records.Any(r => (r.Flags & 0x40) != 0))
            {
                _warnedNoHero = true;
                Logs.WriteWarning("AlundraEffectStage: no hero to read the arrival tile from; the effects that appear at load are placed for the tile (0, 0).");
            }
        }

        _reservoir.SpawnAtLoad(tileX, tileY);
    }

    /// <summary>Queues an operation on the reservoir, marked with the logic <paramref name="tick"/> of the frame that emits it and the <paramref name="rank"/> of its source (the map events
    /// first, then the entities, then the pending triggers, like the binary's order).</summary>
    public void Queue(int tick, int rank, Action<AlundraEffectReservoir> operation)
    {
        _queue.Add(new PendingOperation(tick, rank, _sequence++, operation));
    }

    /// <summary>Marks the operations emitted from now on with the logic <paramref name="tick"/> of the loop that runs the script and the <paramref name="rank"/> of its source (0 map events, 1 entities, 2 pending triggers).</summary>
    public void BeginSource(int tick, int rank)
    {
        _sourceTick = tick;
        _sourceRank = rank;
    }

    /// <summary>Ends the marking of <see cref="BeginSource"/>: what is emitted afterwards carries the tick 0 and the rank 0.</summary>
    public void EndSource()
    {
        _sourceTick = 0;
        _sourceRank = 0;
    }

    /// <summary>What the opcode handlers call: <see cref="Queue"/> with the mark of <see cref="BeginSource"/>.</summary>
    public void Emit(Action<AlundraEffectReservoir> operation) => Queue(_sourceTick, _sourceRank, operation);

    /// <summary>
    /// E20.f1b (D-E20-28, amends ADR-0046 for the hero only): creates a mode 0 effect AT ONCE in the reservoir, like <see cref="AlundraEffectReservoir.Create"/>, and returns its slot (null when the reservoir is
    /// full), so that the hero keeps the slot as its handle and reads it at its next tick. The slot is marked with the logic tick of the current source (<see cref="BeginSource"/>): <see cref="RunFrame"/> does not
    /// update it before that tick. Deviation for frames of several ticks, written here: the hero's slot index is taken before the queued operations of earlier ticks of the frame are applied.
    /// </summary>
    public AlundraEffectSlot? CreateNow(bool mapTable, int effect, int animation, int x, int y, int z)
        => Mark(_reservoir.Create(mapTable, effect, animation, x, y, z));

    /// <summary>E20.f1b (D-E20-28): <see cref="CreateNow"/> for the creator of mode 1 (<see cref="AlundraEffectReservoir.CreateAttached"/>).</summary>
    public AlundraEffectSlot? CreateAttachedNow(bool mapTable, int effect, int animation, IAlundraEffectAnchor anchor, int depthOffset, int offsetX, int offsetY, int offsetZ)
        => Mark(_reservoir.CreateAttached(mapTable, effect, animation, anchor, depthOffset, offsetX, offsetY, offsetZ));

    private AlundraEffectSlot? Mark(AlundraEffectSlot? slot)
    {
        if (slot != null)
        {
            slot.FirstUpdateTick = _sourceTick;
        }

        return slot;
    }

    /// <summary>
    /// E20.f1b (ADR-0059): releases <paramref name="slot"/> at the position of the current source's tick, as the binary's effect pass does after <c>MovePlayer</c>: a queued operation applied by
    /// <see cref="RunFrame"/> in the order of the ticks, so that a creation queued at an earlier tick of the same frame cannot take the slot first. Guarded by <paramref name="generation"/> (D-E20-30): the slot is
    /// freed only if it still holds the effect the caller meant.
    /// </summary>
    public void FreeAtTick(AlundraEffectSlot slot, int generation)
        => Emit(reservoir => reservoir.FreeIfGeneration(slot, generation));

    /// <summary>
    /// One rendered frame of <paramref name="ticks"/> logic ticks: at each tick the operations marked with it are applied, then the reservoir is updated unless that tick is frozen
    /// (<paramref name="frozenOfTick"/>: <c>PlayerControlFlags &amp; 0x48</c> read after the map events of the tick); the operations left are applied at the end, and the queue is empty.
    /// E20.e0b1 (D-E0B-1): <paramref name="afterTick"/>, when given, is called once per tick AFTER the update of the reservoir, whether the tick is frozen or not (the binary's advance of the generator ends
    /// its frame function); never called for 0 ticks. Null by default: a direct call of the stage advances nothing.
    /// </summary>
    public void RunFrame(int ticks, IReadOnlyList<bool> frozenOfTick, Action? afterTick = null)
    {
        var pending = _queue.OrderBy(o => o.Tick).ThenBy(o => o.Rank).ThenBy(o => o.Sequence).ToList();
        _queue.Clear();

        var next = 0;
        for (var tick = 0; tick < ticks; tick++)
        {
            while (next < pending.Count && pending[next].Tick <= tick)
            {
                pending[next++].Operation(_reservoir);
            }

            _reservoir.Tick(tick < frozenOfTick.Count && frozenOfTick[tick], tick);
            afterTick?.Invoke();
        }

        while (next < pending.Count)
        {
            pending[next++].Operation(_reservoir);
        }

        // E20.f1b: the first-update marks of the hero's immediate creations are local to this frame, also when it has no tick.
        _reservoir.ClearFirstUpdateMarks();
    }

    private readonly record struct PendingOperation(int Tick, int Rank, long Sequence, Action<AlundraEffectReservoir> Operation);
}
