# Read-only: for each production symbol/topic that E20.f4, f5 or f6 can touch, the test files (and hit counts) that reference it.
import re, os, sys, glob, collections
TESTS = 'D:/development/repo/alundra-casaengine-project-converter/Alundra.Tests'
TOPICS = collections.OrderedDict([
    ('shot-seam', r'WeaponSpawnCall|WeaponSpawnBirthHook|WeaponSpawnCallCountForTests|WiredWeaponTypes|SpawnWeaponProjectile'),
    ('weapon-tables/record', r'AlundraWeaponTables|CurrentWeaponRecord|WeaponRecordPointer|WeaponStepCounter'),
    ('interface-spawn', r'SpawnEntityBySpriteIndex|CreateEntityFromSpriteIndex|JoinsBirthTick|SpawnIndexOracle'),
    ('native-registry', r'NativeSprites|NativeHandlersEnabled|NativeHandlerRunCount|AlundraNativeSpriteRegistry|CreateForProduction'),
    ('Muruta/Beannoide AI keys', r'AlundraMurutaAi|AlundraBeannoideAi'),
    ('break-effect/DestroyEntity', r'BreakEffect|CreateBreakEffect|DestroyEntity\(\w+, ?-?\d+\)|AlundraFrozenEffectAnchor'),
    ('native-effects/stage', r'AlundraNativeEffects|EffectStage|AlundraEffectReservoir|CreatedForTests|FreeAtTick|CreateAttached'),
    ('hit-pass', r'AlundraHitPass|HitArena|HitSparksForTests|HitOracleTrace|HitCounter'),
    ('test-saves/presets', r'AlundraTestSaves|ArsenalAWeapons|WeaponPreset|test-saves'),
    ('arc give/equip', r'\.Give\(|\.Equip\(|SetPlayerWeaponId'),
    ('charge-oracle', r'ChargeOracle|ChargeRow'),
    ('attack-oracle(AT9)', r'AttackOracleTrace'),
    ('ResyncControllerFromFlags', r'ResyncControllerFromFlags'),
    ('RNG', r'AlundraRandom\.|RandomSeed'),
])
files = sorted(glob.glob(TESTS + '/*.cs') + glob.glob(TESTS + '/Scripts/*.cs') + glob.glob(TESTS + '/UI/*.cs'))
res = {t: {} for t in TOPICS}
for f in files:
    txt = open(f, encoding='utf-8', errors='replace').read()
    name = os.path.basename(f)
    for t, rx in TOPICS.items():
        n = len(re.findall(rx, txt))
        if n:
            res[t][name] = n
for t in TOPICS:
    print('== %s  (%d files)' % (t, len(res[t])))
    print('   ' + ', '.join('%s:%d' % (k, v) for k, v in sorted(res[t].items(), key=lambda kv: -kv[1])))
