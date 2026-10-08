"""O-E19-78 candidate, applied to the PHYSICAL COPY only (repo/Alundra/Scripts/AlundraWorldProxy.cs).
Variant 'order'   : the pad pass and the inventory block move into the loop of the passes: per tick k = TickPad(k), dialogue pass (k-1 words),
                    save screen (k-1 words), gate, map events + recycling, effect gate, THEN the inventory block (portrait head point, main director
                    = warp-delay decrement + trigger/head/setup or per-frame, sub-inventory, post-process, portrait step, the two presenters).
Variant 'pending' : 'order' + the pending-trigger pass reads the gate as it stood after the map events of the last tick (before its inventory block).
Usage: python apply_o78.py order|pending [--instr]   (--instr adds copy-only counters for the probes)."""
import sys

VARIANT = sys.argv[1]
INSTR = '--instr' in sys.argv
P = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78/repo/Alundra/Scripts/AlundraWorldProxy.cs'
s = open(P, 'rb').read().decode('utf-8')
assert '\r\n' in s
s = s.replace('\r\n', '\n')


def rep(old, new, count=1):
    global s
    assert s.count(old) == count, (s.count(old), old[:120])
    s = s.replace(old, new)


# 1. loop 1 keeps only the clearing of the per-tick records
if VARIANT == 'base':
    rep = lambda old, new, count=1: None
rep("""        _squareOfTick.Clear();
        _choicePadOfTick.Clear();
        for (var padTick = 0; padTick < ticksThisFrame; padTick++)
        {
            GameState.TickPad.Update(GameState.LastPadState.ButtonsHold);
            _squareOfTick.Add(((GameState.TickPad.ButtonsHold & AlundraPadState.Square) != 0, (GameState.TickPad.ButtonsJustPressed & AlundraPadState.Square) != 0));
            _choicePadOfTick.Add((GameState.TickPad.ButtonsJustPressed, GameState.TickPad.ButtonsJustPressedByInterval));
            UpdateInventoryPortraitHeadPoint();
            AlundraInventoryDirector.Instance.Tick(PlayerEntity);
            AlundraSubInventoryDirector.Instance.Tick();
            AlundraInventoryPostProcess.Instance.Run();
""", """        _squareOfTick.Clear();
        _choicePadOfTick.Clear();
        for (var padTick = 0; padTick < 0; padTick++) // O78: the pad pass and the inventory block moved into the loop of the passes below
        {
""")
rep("""            AlundraInventoryPortrait.Instance.Step();

            // E13.d D5""", """            // E13.d D5""")
rep("""            _inventoryPresenter?.Tick();

            // E13.d SI4""", """            // E13.d SI4""")
rep("""            _subInventoryPresenter?.Tick();
        }

        // E12.a wiring fix""", """        }

        // E12.a wiring fix""")

# 2. loop 2: pad first, inventory block last, no `continue`
rep("""        _effectFrozenOfTick.Clear();
        for (var tick = 0; tick < ticksThisFrame; tick++)
        {
            _effectStage.BeginSource(tick, 0);""", """        _effectFrozenOfTick.Clear();
        var gateAfterMapEvents = gameplayBlocked; // O78
        for (var tick = 0; tick < ticksThisFrame; tick++)
        {
            GameState.TickPad.Update(GameState.LastPadState.ButtonsHold); // O78: the pad of tick k (sample k), first
            _squareOfTick.Add(((GameState.TickPad.ButtonsHold & AlundraPadState.Square) != 0, (GameState.TickPad.ButtonsJustPressed & AlundraPadState.Square) != 0));
            _choicePadOfTick.Add((GameState.TickPad.ButtonsJustPressed, GameState.TickPad.ButtonsJustPressedByInterval));
            _effectStage.BeginSource(tick, 0);""")
rep("""            if (PlayerEntity == null || tickBlocked)
            {
                _effectFrozenOfTick.Add(EffectGateOfThisTick()); // E19.g G3a (G3A-R6): the gate of the tick, `continue` paths included.
                continue;
            }

            // E19.c2""", """            if (PlayerEntity != null && !tickBlocked)
            {
            // E19.c2""")
rep("""            RecycleDestroyedEntities();

            _effectFrozenOfTick.Add(EffectGateOfThisTick()); // E19.g G3a (G3A-R6): read after the map events of the tick.
        }

        _effectStage.EndSource();""", """            RecycleDestroyedEntities();
            }

            _effectFrozenOfTick.Add(EffectGateOfThisTick()); // E19.g G3a (G3A-R6): read after the map events of the tick.
            gateAfterMapEvents = (GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0
                || AlundraWarpDirector.Instance.IsTransitionInProgress; // O78
            INSTR_GATE
            // O78: the inventory block - Update(k)'s trigger (after UpdateWorld) then RenderScene(k+1)'s slots 4 and 6, tail and portrait.
            UpdateInventoryPortraitHeadPoint();
            AlundraInventoryDirector.Instance.Tick(PlayerEntity);
            AlundraSubInventoryDirector.Instance.Tick();
            AlundraInventoryPostProcess.Instance.Run();
            AlundraInventoryPortrait.Instance.Step();
            _inventoryPresenter?.Tick();
            _subInventoryPresenter?.Tick();
        }

        _effectStage.EndSource();""")
s = s.replace('            INSTR_GATE\n', '            O78GateAfterMapEvents.Add(gateAfterMapEvents);\n' if INSTR else '')

if VARIANT == 'base':
    def rep(old, new, count=1):
        global s
        assert s.count(old) == count, (s.count(old), old[:120])
        s = s.replace(old, new)
if VARIANT == 'pending':
    rep("""        gameplayBlocked = (GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0
            || AlundraWarpDirector.Instance.IsTransitionInProgress;

        // E7.b""", """        gameplayBlocked = ticksThisFrame > 0
            ? gateAfterMapEvents // O78: the pending triggers belong to UpdateEntities(k), before the trigger and RenderScene(k+1)
            : (GameState.PlayerControlFlags & AlundraGameState.PlayerControlBits.GameplayBlockedMask) != 0 || AlundraWarpDirector.Instance.IsTransitionInProgress;

        // E7.b""")
else:
    assert VARIANT in ('order', 'base')

if INSTR:
    # copy-only probes: the pending-trigger gate of the frame, and the gate after the map events of each tick
    rep("""        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")""", """        O78PendingGate.Add(gameplayBlocked);

        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")""")
    rep("""    public override void Update(float elapsedTime)
    {""", """    internal static readonly System.Collections.Generic.List<bool> O78PendingGate = new();
    internal static readonly System.Collections.Generic.List<bool> O78GateAfterMapEvents = new();

    public override void Update(float elapsedTime)
    {""")

if '--seams' in sys.argv:
    # the two test seams the proposed red-first tests read (production candidates, one line each)
    assert s.count('        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")') == 1
    s = s.replace('        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")',
                  '        PendingTriggersGateForTests = gameplayBlocked;\n\n        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")')
    assert s.count('    private bool EffectGateOfThisTick() =>') == 1
    s = s.replace('    private bool EffectGateOfThisTick() =>',
                  '    internal IReadOnlyList<bool> EffectFrozenOfTickForTests => _effectFrozenOfTick;\n\n    internal bool PendingTriggersGateForTests { get; private set; }\n\n    private bool EffectGateOfThisTick() =>')

open(P, 'wb').write(s.replace('\n', '\r\n').encode('utf-8'))
print('applied', VARIANT, 'instr' if INSTR else '')
