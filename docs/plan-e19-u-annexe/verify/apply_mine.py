"""Counter-check: build the variants of AlundraWorldProxy.cs in the PHYSICAL COPY only.
usage: python apply_mine.py head|cand [--seams] [--probe]
 head : the HEAD file (copied from verify/AlundraWorldProxy.cs.head)
 cand : HEAD + the discovery's candidate-pending.diff.txt applied with GNU patch (exactly the proposed diff)
 --seams : the two one-line test seams the proposed red-first tests read (same text as the discovery's apply_o78.py --seams)
 --probe : copy-only counters for my own probes (map-event pass count per tick, pending gate per frame)
"""
import sys, subprocess, os, tempfile

V = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/d1008-o78/verify'
DISC = os.path.dirname(V)
DST = V + '/repo/Alundra/Scripts/AlundraWorldProxy.cs'
mode = sys.argv[1]
raw = open(V + '/AlundraWorldProxy.cs.head', 'rb').read()
bom = raw.startswith(b'\xef\xbb\xbf')
s = raw.decode('utf-8-sig').replace('\r\n', '\n')

if mode == 'cand':
    with tempfile.TemporaryDirectory() as td:
        p = os.path.join(td, 'AlundraWorldProxy.cs')
        open(p, 'w', encoding='utf-8', newline='\n').write(s)
        r = subprocess.run(['patch', '--no-backup-if-mismatch', '-p0', p, DISC + '/candidate-pending.diff.txt'], capture_output=True, text=True)
        print(r.stdout, r.stderr)
        assert r.returncode == 0
        s = open(p, encoding='utf-8').read()
else:
    assert mode == 'head'

def rep(old, new):
    global s
    assert s.count(old) == 1, (s.count(old), old[:100])
    s = s.replace(old, new)

if '--seams' in sys.argv:
    rep('        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")',
        '        PendingTriggersGateForTests = gameplayBlocked;\n\n        // E7.b (docs/plan-e7-mutation-tuiles.md, "coalescer par frame")')
    rep('    private bool EffectGateOfThisTick() =>',
        '    internal IReadOnlyList<bool> EffectFrozenOfTickForTests => _effectFrozenOfTick;\n\n    internal bool PendingTriggersGateForTests { get; private set; }\n\n    private bool EffectGateOfThisTick() =>')

if '--probe' in sys.argv:
    # per tick: did the map events pass run (copy-only); RunMapEventsPass is called once per open tick
    rep('            RunMapEventsPass(PlayerEntity, _mapEvents, EventProgramRunner, GameState.PlayerControlFlags);',
        '            VerifyMapEventTicks.Add(VerifyTickCounter);\n            RunMapEventsPass(PlayerEntity, _mapEvents, EventProgramRunner, GameState.PlayerControlFlags);')
    rep('            _effectStage.BeginSource(tick, 0);',
        '            VerifyTickCounter++;\n            _effectStage.BeginSource(tick, 0);')
    rep('    public override void Update(float elapsedTime)\n    {',
        '    internal static int VerifyTickCounter;\n    internal static readonly System.Collections.Generic.List<int> VerifyMapEventTicks = new();\n\n    public override void Update(float elapsedTime)\n    {')

out = s.replace('\n', '\r\n').encode('utf-8')
if bom:
    out = b'\xef\xbb\xbf' + out
open(DST, 'wb').write(out)
print('written', mode, sys.argv[2:])
