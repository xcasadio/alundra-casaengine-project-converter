import json, re, subprocess, sys
from collections import Counter
REPO = sys.argv[1]
OUT = open(sys.argv[2], 'w', encoding='utf-8')
table = json.load(open(REPO + '/tools/dll-structure/table.json', encoding='utf-8'))
ren = {}
for t in table['types']:
    if t['old_name'] != t['final']['name']:
        ren[t['old_name']] = t['final']['name']
names = sorted(ren, key=len, reverse=True)
pat = re.compile(r'\b(' + '|'.join(map(re.escape, names)) + r')\b')
BOM = chr(0xFEFF)

def old(path):
    return subprocess.run(['git', '-C', REPO, 'show', '2010ec3:' + path], capture_output=True, text=True, encoding='utf-8').stdout

def norm(text, rename):
    out = []
    for line in text.splitlines():
        line = line.replace(BOM, '')
        s = line.strip()
        if s.startswith('using ') or s.startswith('namespace '):
            continue
        if rename:
            line = pat.sub(lambda m: ren[m.group(1)], line)
        out.append(line.strip())
    return out

pairs = [
    ('Alundra/Scripts/AlundraLootRoll.cs', ['Alundra/Items/LootRoll.cs']),
    ('Alundra/Scripts/AlundraWorldProxy.cs', ['Alundra/Maps/WorldProxy.cs']),
    ('Alundra/Scripts/AlundraRandom.cs', ['Alundra/Rng/GameRandom.cs']),
    ('Alundra/Scripts/AlundraEntityScriptProxy.cs', ['Alundra/Entities/EntityScriptProxy.cs']),
    ('Alundra/Scripts/AlundraEntitySpawnFactory.cs', ['Alundra/Entities/Spawning/EntitySpawnFactory.cs']),
    ('Alundra/Scripts/IEntityWorldContext.cs', ['Alundra/Maps/IEntityWorldContext.cs', 'Alundra/Maps/NoOpEntityWorldContext.cs']),
    ('Alundra/Scripts/AlundraEventProgramRunner.cs', ['Alundra/Events/EventProgramRunner.cs', 'Alundra/Events/EventTraceKind.cs', 'Alundra/Events/EventTraceRecord.cs']),
    ('Alundra/Scripts/AlundraPlayerManager.cs', ['Alundra/Hero/PlayerManager.cs']),
    ('Alundra/Scripts/AlundraNativeEffects.cs', ['Alundra/Rendering/Effects/NativeEffects.cs']),
    ('Alundra/Scripts/AlundraNativeSpriteRegistry.cs', ['Alundra/Ai/NativeSpriteRegistry.cs']),
    ('Alundra/Scripts/AlundraWaveSwordAi.cs', ['Alundra/Ai/Projectiles/WaveSwordAi.cs']),
    ('Alundra/Scripts/AlundraSaveBook.cs', ['Alundra/Ai/Objects/SaveBook.cs']),
    ('Alundra/Scripts/AlundraItemTables.cs', ['Alundra/Items/ItemTables.cs']),
    ('Alundra/Scripts/AlundraHitPass.cs', ['Alundra/Combat/HitPass.cs']),
    ('Alundra.Tests/AlundraDestroyEntityBreakEffectTests.cs', ['Alundra.Tests/AlundraDestroyEntityBreakEffectTests.cs']),
    ('Alundra.Tests/AlundraBeannoideOracleTests.cs', ['Alundra.Tests/AlundraBeannoideOracleTests.cs']),
    ('Alundra.Tests/AlundraMurutaOracleTests.cs', ['Alundra.Tests/AlundraMurutaOracleTests.cs']),
    ('Alundra.Tests/AlundraEnemyHandlerOracle.cs', ['Alundra.Tests/AlundraEnemyHandlerOracle.cs']),
    ('Alundra.Tests/AlundraLootRollTests.cs', ['Alundra.Tests/AlundraLootRollTests.cs']),
    ('Alundra.Tests/AlundraLootRollWorldTests.cs', ['Alundra.Tests/AlundraLootRollWorldTests.cs']),
    ('Alundra.Tests/AlundraLootRollWorldTests.Rolls.cs', ['Alundra.Tests/AlundraLootRollWorldTests.Rolls.cs']),
    ('Alundra.Tests/AlundraItemAndMoneyOpcodesTests.cs', ['Alundra.Tests/AlundraItemAndMoneyOpcodesTests.cs']),
    ('Alundra.Tests/AlundraNativeSpriteRegistryTests.cs', ['Alundra.Tests/AlundraNativeSpriteRegistryTests.cs']),
    ('Alundra.Tests/AlundraItemTablesTests.cs', ['Alundra.Tests/AlundraItemTablesTests.cs']),
]
for o, news in pairs:
    a = norm(old(o), True)
    b = []
    for n in news:
        b += norm(open(REPO + '/' + n, encoding='utf-8').read(), False)
    ca, cb = Counter(x for x in a if x), Counter(x for x in b if x)
    only_old = list((ca - cb).elements())
    only_new = list((cb - ca).elements())
    code_old = [l for l in only_old if not l.startswith('//')]
    code_new = [l for l in only_new if not l.startswith('//')]
    OUT.write('=== %s : residue old %d new %d ; non-comment old %d new %d\n' % (o, len(only_old), len(only_new), len(code_old), len(code_new)))
    for l in code_old:
        OUT.write('   OLD| ' + l[:220] + '\n')
    for l in code_new:
        OUT.write('   NEW| ' + l[:220] + '\n')
OUT.close()
