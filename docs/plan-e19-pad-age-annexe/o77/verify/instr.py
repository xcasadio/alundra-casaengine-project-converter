# copy-only instrumentation: count the map-events passes the proxy reaches (O-E19-77 adjacent check)
p = r'../repo/Alundra/Scripts/AlundraWorldProxy.cs'
s = open(p, 'rb').read().decode('utf-8')
a = '            RunMapEventsPass(PlayerEntity, _mapEvents, EventProgramRunner, GameState.PlayerControlFlags);\r\n'
b = '    internal static uint O77PrevInterval;\r\n'
assert s.count(a) == 1 and s.count(b) == 1
s = s.replace(a, '            O77MapEventPasses++;\r\n' + a).replace(b, b + '    internal static int O77MapEventPasses;\r\n')
open(p, 'wb').write(s.encode('utf-8'))
print('ok')
