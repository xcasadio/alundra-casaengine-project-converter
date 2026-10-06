"""Scratch-copy-only mutant M2 (the E19.f3d-style change applied to the inventories): the per-frame input of both inventory
directors reads the by-interval word of the PREVIOUS tick; the trigger keeps the tick's pressed word. Switched by O77MUT=2."""
import io
R = r'repo/Alundra/Scripts/'
def edit(path, pairs):
    b = open(path, 'rb').read()
    crlf = b'\r\n' in b
    s = b.decode('utf-8')
    for old, new in pairs:
        assert s.count(old) >= 1, (path, old)
        s = s.replace(old, new)
    open(path, 'wb').write(s.encode('utf-8'))
    print('edited', path, 'crlf' if crlf else 'lf')
nl = '\r\n'
edit(R + 'AlundraWorldProxy.cs', [
    ('            GameState.TickPad.Update(GameState.LastPadState.ButtonsHold);' + nl,
     '            O77PrevInterval = GameState.TickPad.ButtonsJustPressedByInterval;' + nl + '            GameState.TickPad.Update(GameState.LastPadState.ButtonsHold);' + nl),
    ('    private readonly List<(bool Held, bool Pressed)> _squareOfTick = new();',
     '    internal static uint O77PrevInterval;' + nl + '    internal static readonly bool O77Mut2 = System.Environment.GetEnvironmentVariable("O77MUT") == "2";' + nl + '    private readonly List<(bool Held, bool Pressed)> _squareOfTick = new();'),
])
for f in ('AlundraInventoryDirector.cs', 'AlundraSubInventoryDirector.cs'):
    b = open(R + f, 'rb').read().decode('utf-8')
    i = b.index('private void RunInput(')
    j = b.index('/// <summary>', i)
    body = b[i:j]
    body2 = body.replace('pad.ButtonsJustPressedByInterval', '(AlundraWorldProxy.O77Mut2 ? AlundraWorldProxy.O77PrevInterval : pad.ButtonsJustPressedByInterval)')
    print(f, body.count('pad.ButtonsJustPressedByInterval'), 'reads switched')
    open(R + f, 'wb').write((b[:i] + body2 + b[j:]).encode('utf-8'))
