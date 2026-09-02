#nullable enable
using CasaEngine.Core.Logging;
using CasaEngine.Framework.Application;

namespace Alundra.Scripts;

/// <summary>
/// T4 (docs/plan-transitions-carte.md §1.2.c/§1.2.e/§3, D-T-2): SESSION-scoped singleton - same
/// contract shape as <see cref="AlundraMusicPlayer"/>/<see cref="AlundraScreenFadeDirector"/>/
/// <see cref="AlundraDialogueDirector"/> (<c>Instance</c>, <see cref="AttachToWorld"/>,
/// <see cref="InstallForMapEntry"/>, <see cref="ResetForTests"/>) - port of <c>PlayerManager.HandleWarpTransition</c>
/// (<c>PlayerManager.cs:3488-3541</c>) plus the map-entry preamble that ENDS it
/// (<c>GameEngine.cs:302</c>, §1.2.e/D-T-15). Reason it must be session-scoped, not per-world (D-T-2):
/// the departure sequence itself SPANS the world change - it starts on the departing world, and its own
/// arrival record/effect id are read back on the very next world, strictly BEFORE that world's first
/// <see cref="AlundraWorldProxy.Update"/> ever runs (see <see cref="InstallForMapEntry"/>'s own doc for
/// the exact ordering this depends on) - a per-world instance would already be a fresh, empty object by
/// the time anything could read it back.
///
/// <b>Ownership of the gel (D-T-6, §1.5's own "third mechanism").</b> The original poses NO control-flag
/// bit on the warp path at all - it short-circuits its whole entity pipeline with a dedicated loop
/// instead (<c>AdvanceWarpTransitionFrame</c>). <see cref="IsTransitionInProgress"/> is this port's own
/// stand-in predicate: <see cref="AlundraWorldProxy.Update"/> and
/// <see cref="AlundraEntityScriptProxy.Update"/> OR it into the exact same "dedans" gate T2 already
/// built (<c>GameplayBlockedMask</c>), at the exact same two sites - see those methods' own T4 comments.
/// </summary>
/// <remarks>
/// <para><b>The six pieces of session state D-T-15 names, and why each behaves as it does at map entry</b>:
/// <see cref="IsTransitionInProgress"/> (the gel gate) is reset to false; the departure-request/sequence
/// counter (<see cref="_sequenceTicks"/>, doubling as both per D-T-15's own two rows - see that field's
/// own doc) is reset to zero; the pending world-change path is reset to null; but the ARRIVAL RECORD
/// (position/animation/direction) and the TRANSITION EFFECT ID are deliberately left untouched - they
/// are consumed by T5's own <c>AdoptPlayerPawn</c>/<c>InstallScreenFadeSystems</c>, both of which run
/// AFTER this method returns (<see cref="AlundraWorldProxy.InitializeWithWorld"/>'s own installation
/// block, <c>:505-508</c> for the installs, <c>:531</c> for <c>AdoptPlayerPawn</c>) - clearing them here
/// would destroy their only reader's input before it ever runs.</para>
///
/// <para><b>The <see cref="_sequenceTicks"/> mutation trap (D-T-15's own "laisser la séquence de départ
/// armée" clause)</b>: <see cref="IsTransitionInProgress"/> is NOT an independent bool that, once cleared,
/// stays cleared - <see cref="Advance"/> RE-ASSERTS it every tick for as long as
/// <see cref="_sequenceTicks"/> is non-zero, regardless of what <see cref="InstallForMapEntry"/> did to
/// the gate field itself. So a broken map-entry install that resets the gate but forgets to zero this
/// counter re-poses the gate the very next tick - the exact falsifiable shape D-T-15's own mutation
/// table names, rather than a silent no-op.</para>
/// </remarks>
public sealed class AlundraWarpDirector
{
    /// <summary>The one session-scoped instance every <see cref="AlundraWorldProxy"/> shares (D-T-2) -
    /// never <see langword="new"/>'d per world.</summary>
    public static readonly AlundraWarpDirector Instance = new();

    private AlundraWarpDirector()
    {
    }

    // EntityRecordMapper's own tile constants (StaticVariables.MapTileWidth/Height) - duplicated here,
    // same precedent as AlundraWorldProxy's own private TileWidth/TileHeight (§1.2.c's own arithmetic
    // needs both).
    private const int TileWidth = 24;
    private const int TileHeight = 16;

    // 80028DCC (StaticVariables.cs:554-559) - g_warpBehaviorTable, indexed by
    // AlundraPortalRecord.WarpBehaviorId (already masked to 0..0xF there).
    private static readonly int[] WarpBehaviorTable =
    {
        0x0000, 0x0045, 0x0000, 0x0037,
        0x018F, 0x004A, 0x004A, 0x004B,
        0x0000, 0x004C, 0x0049, 0x0003,
        0x0003, 0x0003, 0x0003, 0x0003,
    };

    private GameManager? _gameManager;
    private IAlundraSoundPlayer? _soundPlayer;
    private AlundraWorldIndexTable? _worldIndex;

    /// <summary>The gel gate - see this class' own doc. Re-derived from <see cref="_sequenceTicks"/> by
    /// <see cref="Advance"/> every tick it runs; only ever written to <see langword="false"/> by
    /// <see cref="InstallForMapEntry"/>.</summary>
    public bool IsTransitionInProgress { get; private set; }

    /// <summary>
    /// D-T-15's own "séquence de départ (compteur du fondu sortant, étape courante)" row - zero means
    /// "no departure in flight", non-zero is the tick count since <see cref="BeginDeparture"/> armed it
    /// (starting at 1, not 0, so the very frame that arms it already reads as "in flight" - the gel must
    /// cover that same frame, not start one frame late).
    /// </summary>
    private int _sequenceTicks;

    /// <summary>The chemin resolved by <see cref="AlundraWorldIndexTable.Resolve"/> at
    /// <see cref="BeginDeparture"/> time, held until the outgoing fade settles (§1.1.e: "émis seulement
    /// une fois le fondu stabilisé") - D-T-15's own "demande de changement de monde" row.</summary>
    private string? _pendingWorldPath;

    /// <summary>True once <see cref="Advance"/> has actually handed the world path to the engine, so the
    /// gate stays posted through the switch (the arrival map's own <see cref="InstallForMapEntry"/> lifts
    /// it) without the abort guard below mistaking "already emitted" for "cannot emit".</summary>
    private bool _worldChangeRequested;

    /// <summary>The player whose gravity <see cref="BeginDeparture"/> suspended, and what it was, so an
    /// aborted departure can put it back - a completed one never needs to (fresh pawn, and
    /// AdoptPlayerPawn rewrites both values at every map entry).</summary>
    private AlundraEntityScriptProxy? _gravitySuspendedPlayer;
    private (float Gravity, float MaxFallSpeed, bool VerticalOwnedExternally) _gravityBeforeDeparture;

    // ---- The arrival record + transition effect id (D-T-15: CONSERVED across InstallForMapEntry - T5's
    // own AdoptPlayerPawn/InstallScreenFadeSystems are their only readers, both running strictly AFTER
    // this class' own InstallForMapEntry call in the SAME AlundraWorldProxy.InitializeWithWorld).
    private uint _arrivalMapIndex;
    private int _arrivalPosX;
    private int _arrivalPosY;
    private int _arrivalPosZ;
    private uint _arrivalAnimationId;
    private uint _arrivalDirectionId;
    private int _arrivalEffectId;

    /// <summary>True once <see cref="BeginDeparture"/> has armed a departure this session has not yet
    /// consumed via a completed <see cref="InstallForMapEntry"/> - what T5 reads to know an arrival
    /// record is actually present (as opposed to this director's all-zero construction default).</summary>
    public bool HasPendingArrival { get; private set; }

    /// <summary>
    /// T5 (§1.4.g, D-T-7): the transition effect id carried by the pending arrival, or 0 when there is
    /// none - a PEEK, no side effect on <see cref="HasPendingArrival"/>. Read by
    /// <see cref="AlundraWorldProxy.InstallScreenFadeSystems"/>, which runs BEFORE <c>AdoptPlayerPawn</c>
    /// (the record's own consuming reader, <see cref="ConsumeArrivalRecord"/>) in the SAME
    /// <see cref="AlundraWorldProxy.InitializeWithWorld"/> call - a peek, not a consume, is required here
    /// so the id is still readable when the position/animation/direction reader runs afterward.
    /// </summary>
    public int PendingArrivalEffectId => HasPendingArrival ? _arrivalEffectId : 0;

    /// <summary>Test-only mirror of the DÉCLARÉ INERTE warp-delay counter (see
    /// <see cref="InstallForMapEntry"/>'s own doc) - internal, not part of this class' public surface,
    /// since nothing in this port ever reads it.</summary>
    internal int WarpDelayFramesForTests { get; private set; }

    /// <summary>
    /// T5 (D-T-4, [R9]): <c>AdoptPlayerPawn</c>'s sole consuming read of the arrival record - position,
    /// animation and direction, all four already written by <see cref="BeginDeparture"/> - which ALSO
    /// clears <see cref="HasPendingArrival"/> ([R9], T4's own closing-verifier reserve: without this, a
    /// later map entry that is not itself the destination of a warp would read this same STALE record
    /// forever, since this director is session-scoped and nothing else ever clears the flag). Returns
    /// <see langword="null"/> when there is no pending arrival, so the caller falls back to the New Game
    /// constants instead of the all-zero construction default.
    /// </summary>
    public (int PosX, int PosY, int PosZ, uint AnimationId, uint DirectionId)? ConsumeArrivalRecord()
    {
        if (!HasPendingArrival)
        {
            return null;
        }

        HasPendingArrival = false;
        return (_arrivalPosX, _arrivalPosY, _arrivalPosZ, _arrivalAnimationId, _arrivalDirectionId);
    }

    /// <summary>Test-only mirror of the arrival record's five fields, so a test can assert them without
    /// this class exposing setters on its public surface - see <see cref="BeginDeparture"/>'s own doc for
    /// what each one is.</summary>
    internal (uint MapIndex, int PosX, int PosY, int PosZ, uint AnimationId, uint DirectionId, int EffectId) ArrivalRecordForTests
        => (_arrivalMapIndex, _arrivalPosX, _arrivalPosY, _arrivalPosZ, _arrivalAnimationId, _arrivalDirectionId, _arrivalEffectId);

    /// <summary>Test-only: whether a departure is currently armed (<see cref="_sequenceTicks"/> != 0) -
    /// the D-T-15 "demande de départ"/"séquence de départ" rows, exposed together since this port folds
    /// them into the one counter (see that field's own doc).</summary>
    internal bool IsDepartureArmedForTests => _sequenceTicks != 0;

    /// <summary>Test-only: the world path <see cref="Advance"/> is still waiting to emit (null once
    /// emitted, or before any departure armed) - D-T-15's own "demande de changement de monde" row.</summary>
    internal string? PendingWorldPathForTests => _pendingWorldPath;

    /// <summary>
    /// Re-points this session-scoped instance at the current world's own <see cref="GameManager"/> (the
    /// seam <see cref="Advance"/> calls <c>SetWorldToLoad</c> through, §1.1.e/§1.3.e) and
    /// <see cref="IAlundraSoundPlayer"/> (the departure sfx channel, §1.2.d/D-T-8), and reloads
    /// <c>Maps/world-index.json</c> (cheap - a JSON file read, same precedent as
    /// <see cref="AlundraMusicPlayer.AttachToWorld"/> reloading its own index table on every call).
    /// Deliberately does NOT touch <see cref="IsTransitionInProgress"/>/the arrival record/anything else
    /// (D-T-2, same shape as every other session director's own <c>AttachToWorld</c>) - only
    /// <see cref="InstallForMapEntry"/> does that.
    /// </summary>
    public void AttachToWorld(GameManager? gameManager, IAlundraSoundPlayer? soundPlayer, string projectPath)
    {
        _gameManager = gameManager;
        _soundPlayer = soundPlayer;
        _worldIndex = new AlundraWorldIndexTable(projectPath);
    }

    /// <summary>
    /// D-T-15's own map-entry disposition - called from <see cref="AlundraWorldProxy.InitializeWithWorld"/>
    /// alongside its session-carrier peers (<see cref="AlundraGameState.InstallForMapEntry"/>,
    /// <see cref="AlundraScreenFadeDirector.InstallForMapEntry"/>), BEFORE <c>AdoptPlayerPawn</c> runs
    /// (T5's own reader of the arrival record this method deliberately leaves untouched - see this
    /// class' own doc). Port of <c>GameEngine.cs:302</c>'s <c>_isWarpTransitionRunning = false</c>,
    /// which the original runs strictly BEFORE its own map re-initialization - without this, joueur and
    /// PNJ would stay frozen on the arrival map forever, since that map already receives its own first
    /// <see cref="AlundraWorldProxy.Update"/> in the very same frame as the switch (§1.3.b).
    /// </summary>
    /// <summary>Undoes everything <see cref="BeginDeparture"/> armed, for a departure that will never
    /// reach an arrival: lifts the gate, drops the arrival record no one will read, and puts the hero's
    /// gravity back - the one path where no map entry will do it for us.</summary>
    private void AbortDeparture()
    {
        IsTransitionInProgress = false;
        _sequenceTicks = 0;
        _pendingWorldPath = null;
        _worldChangeRequested = false;
        HasPendingArrival = false;

        if (_gravitySuspendedPlayer != null)
        {
            AlundraPlayerManager.RestoreGravityAfterAbortedWarpDeparture(_gravitySuspendedPlayer, _gravityBeforeDeparture);
            _gravitySuspendedPlayer = null;
        }
    }

    public void InstallForMapEntry()
    {
        IsTransitionInProgress = false;
        _sequenceTicks = 0;
        _pendingWorldPath = null;
        _worldChangeRequested = false;

        // The arrival this entry IS the completion of: the pawn is fresh and AdoptPlayerPawn rewrites
        // gravity from this map's own properties, so there is nothing to put back - just drop the
        // reference to the departure map's now-dead player proxy.
        _gravitySuspendedPlayer = null;

        // T5 (§1.2.f, DÉCLARÉ INERTE - same shape as D-T-8/D-T-9): port of WarpPlayer's own
        // g_warpDelayFrames = 10 (GameEngine.cs:890), set at EVERY map entry regardless of warp-or-not -
        // NOT one of D-T-15's own six states (that table's clause d'exhaustivité covers the departure
        // sequence only), a separate, brand-new piece of structure T5 itself introduces. Its only two
        // original consumers - the Start+Select combo and the inventory-open gate (GameEngine.cs:1523-1528
        // and :1567-1574) - are NOT ported by this chantier (this port has no button-driven inventory
        // path at all, MenuOpen is only ever posed by AlundraDialogueDirector), so nothing ever reads this
        // field: posed here for structural fidelity only, never covered by acceptance.
        WarpDelayFramesForTests = 10;

        // Arrival record + effect id: CONSERVED - see this class' own doc and D-T-15's own table.
    }

    /// <summary>
    /// Port of <c>PlayerManager.HandleWarpTransition</c> (<c>PlayerManager.cs:3488-3541</c>, §1.2.c) -
    /// called from <see cref="AlundraWorldProxy"/>'s own <see cref="IAlundraScriptHost.OnPortalTriggerDetected"/>
    /// override, i.e. from INSIDE <paramref name="player"/>'s own <see cref="AlundraEntityScriptProxy.Update"/>
    /// this same frame (<see cref="AlundraPlayerManager.MovePlayer"/>'s own call site) - so
    /// <see cref="IsTransitionInProgress"/> is already true by the time this SAME frame's
    /// <see cref="AlundraWorldProxy.Update"/> reads its own gate, freezing map events/pending triggers
    /// starting this very frame, not one frame late.
    /// </summary>
    public void BeginDeparture(
        AlundraPortalRecord portal,
        uint arrivalDirectionId,
        AlundraEntityScriptProxy player,
        AlundraGameState state)
    {
        // [R8] (T3's own note, §1.2.g/T3's "Déviation consignée"): the original only tests
        // g_isWarpDisabled INSIDE HandleWarpTransition - T3's predicate folds the same test upstream for
        // the portal-floor/hole path, but opcode 0x53 (T7) calls this exact method WITHOUT ever going
        // through that predicate, so the test is repeated here too (PlayerManager.cs:3488-3490).
        if (state.IsWarpDisabled)
        {
            return;
        }

        // D-T-7: the raw id is transported and LOGGED, but every non-zero value is then treated
        // identically to 0 (the "normal warp" branch below) - the original's own effect-3 same-map
        // teleport special case is §0.3's explicit "hors périmètre" (no same-map portal exists in this
        // chantier's scope), so it is never reachable here regardless.
        var effectId = portal.TransitionEffectId;
        Logs.WriteInfo(
            $"AlundraWarpDirector: departure through portal {portal.Index} to map {portal.DestMapId}, "
            + $"TransitionEffectId={effectId} (treated as effect 0 - D-T-7).");

        var desiredMapIndex = state.MapIdToInternalMapIndexTable[portal.DestMapId];

        // §1.2.c arithmetic, PlayerManager.cs:3497-3509 - EXACT port. g_tileToWorldXTable is a pure
        // division-by-TileWidth table (GameInitializer.cs:304-319: 52 blocks of TileWidth entries, each
        // valued its own block index) - a plain truncating division reproduces it faithfully over its
        // whole domain without needing the table itself.
        var deltaX = portal.DestTileX * TileWidth + (player.PosX >> 16) - portal.X1 * TileWidth;
        var deltaY = portal.DestTileY * TileHeight + (player.PosY >> 16) - portal.Y1 * TileHeight;
        var tileX = deltaX / TileWidth;
        deltaY /= TileHeight;

        _arrivalMapIndex = desiredMapIndex;
        _arrivalPosX = (tileX * TileWidth + TileWidth / 2) << 16;
        _arrivalPosY = (deltaY * TileHeight + TileHeight / 2) << 16;
        _arrivalPosZ = portal.ZLevel << 20;
        _arrivalAnimationId = 0x36; // PlayerManager.cs:3480/3484's own literal (LoadingMap).
        _arrivalDirectionId = arrivalDirectionId;
        _arrivalEffectId = effectId;
        HasPendingArrival = true;

        // D-T-8 (§1.2.d): departure sound channel, structural - see PlayDepartureSound's own doc.
        PlayDepartureSound(desiredMapIndex, portal.WarpBehaviorId);

        // D-T-5: outgoing fade, persistence latch held through the map switch itself - validated line by
        // line in §1.4.f, no AlundraScreenFadeDirector change needed.
        AlundraScreenFadeDirector.Instance.BeginFadeEffect(0xff, 0xff, 0xff, tpage: 2, duration: 16, persistLock: 1);

        // [R6] reserve #1 (T2's own closing-verifier note, this class' own remarks): suspend the hero's
        // engine-driven gravity for the duration of the departure - see
        // AlundraPlayerManager.SuspendGravityForWarpDeparture's own doc for why this, not an engine
        // change, closes the gap.
        var previousGravity = AlundraPlayerManager.SuspendGravityForWarpDeparture(player);
        if (previousGravity != null)
        {
            _gravitySuspendedPlayer = player;
            _gravityBeforeDeparture = previousGravity.Value;
        }

        // §1.1.e: resolved now (this world's own AttachToWorld already loaded the table), but NOT
        // requested yet - Advance emits it only once the fade above has settled.
        _pendingWorldPath = _worldIndex?.Resolve((int)desiredMapIndex);

        // D-T-6: the gel starts THIS frame - see this method's own doc.
        _sequenceTicks = 1;
        IsTransitionInProgress = true;
    }

    /// <summary>
    /// D-T-8/§1.2.d, structural: "coupure des voix, bascule BGM si l'index musical de destination
    /// diffère, lecture du sfx de départ". This port has no persistent SFX-voice concept the original's
    /// own <c>ResetSoundEffectRuntime</c> voice-stop loop would have anything to cut (<see cref="AlundraSoundPlayer"/>
    /// plays fire-and-forget one-shots, never a long-running voice a later frame must silence) - so
    /// "coupure" reduces to whatever <see cref="AlundraMusicPlayer.PlayMapMusic"/> already does to ITS
    /// own current voice when the destination's music index differs (it stops-then-restarts, fact 1.1's
    /// own guard), called unconditionally here exactly like the original calls
    /// <c>HandleMapSoundEffects</c> unconditionally. Measured inert on the 389&lt;-&gt;390 acceptance
    /// path (D-T-8): identical music index 25 both sides, and warp-behaviour-1's own sfx 69 has zero
    /// playable tones (<c>sfx-manifest.json</c>) - so <see cref="IAlundraSoundPlayer.PlaySfx"/> is a
    /// guaranteed no-op there too, without this method needing to special-case it.
    /// </summary>
    private void PlayDepartureSound(uint desiredMapIndex, int warpBehaviorId)
    {
        AlundraMusicPlayer.Instance.PlayMapMusic((int)desiredMapIndex);

        var sfxId = WarpBehaviorTable[warpBehaviorId & 0xF];
        _soundPlayer?.PlaySfx(sfxId);
    }

    /// <summary>
    /// Called once per frame from <see cref="AlundraWorldProxy.Update"/> (a "dehors" pass, same
    /// unconditional shape as <see cref="AlundraScreenFadeDirector.Advance"/>/<c>PushToAttachedService</c>
    /// right next to it - the departure sequence itself must keep running WHILE the gel it drives is
    /// posed). See this class' own remarks for why <see cref="IsTransitionInProgress"/> is re-derived
    /// here every tick instead of being a one-shot latch.
    /// </summary>
    public void Advance(int ticks)
    {
        if (_sequenceTicks == 0)
        {
            return;
        }

        IsTransitionInProgress = true;
        _sequenceTicks += ticks;

        // §1.1.e: "émis seulement une fois le fondu stabilisé" - AlundraScreenFadeDirector.Advance always
        // runs before this call in AlundraWorldProxy.Update's own frame order, so this reads the SAME
        // tick's settled state, never a frame-stale one.
        if (!AlundraScreenFadeDirector.Instance.IsSettled || _worldChangeRequested)
        {
            return;
        }

        if (_pendingWorldPath != null && _gameManager != null)
        {
            _gameManager.SetWorldToLoad(_pendingWorldPath);
            _pendingWorldPath = null;
            _worldChangeRequested = true;
            return;
        }

        // ABORT GUARD. The fade has settled but this departure can never be handed to the engine - no
        // resolvable world path (a missing or unreadable Maps/world-index.json, or a DestMapId absent
        // from it) or no GameManager to hand it to. Doing nothing here would leave the gate posted
        // forever: player and NPCs frozen for the rest of the session, in silence, with no box left to
        // dismiss and nothing in the log. Unreachable on the shipped export - world-index.json carries
        // 483 contiguous entries and every DestMapId falls inside it - so this turns a data-degraded
        // case into a loud, recoverable failure rather than a mute lock.
        Logs.WriteWarning(
            "AlundraWarpDirector: departure aborted - "
            + (_pendingWorldPath == null
                ? "no world path resolved for the destination map"
                : "no GameManager attached to request the world change")
            + ". Lifting the transition gate so the world keeps running.");
        AbortDeparture();
    }

    /// <summary>Test-only: clears every piece of session state so tests do not leak into each other
    /// through this singleton - same seam as <see cref="AlundraScreenFadeDirector.ResetForTests"/>.</summary>
    internal void ResetForTests()
    {
        _gameManager = null;
        _soundPlayer = null;
        _worldIndex = null;

        IsTransitionInProgress = false;
        _sequenceTicks = 0;
        _pendingWorldPath = null;
        _worldChangeRequested = false;
        _gravitySuspendedPlayer = null;
        _gravityBeforeDeparture = default;

        HasPendingArrival = false;
        _arrivalMapIndex = 0;
        _arrivalPosX = 0;
        _arrivalPosY = 0;
        _arrivalPosZ = 0;
        _arrivalAnimationId = 0;
        _arrivalDirectionId = 0;
        _arrivalEffectId = 0;
        WarpDelayFramesForTests = 0;
    }
}
