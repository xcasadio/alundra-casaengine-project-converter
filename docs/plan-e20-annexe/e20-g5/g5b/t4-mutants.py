import sys, subprocess, re, os
REPO = 'D:/development/repo/alundra-casaengine-project-converter'
RUN = REPO + '/Alundra/Events/EventProgramRunner.cs'
TARGETS = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
FILTER = 'FullyQualifiedName~ContactGroupOpcodesTests|FullyQualifiedName~LastTargetOpcodeTests|FullyQualifiedName~ButtonAndBlockOpcodesRealBytesTests|FullyQualifiedName~ButtonAndBlockSitesTests'
SEARCH = 'EntitySearchService.GetMatchingEntitiesBySearchType(entity, v[1], _worldContext.SpawnedEntities, _worldContext.PlayerEntity)'
SEARCH_OWNER = SEARCH.replace('(entity,', '(owner,')
SEARCH_HERO = SEARCH.replace('(entity,', '(_worldContext.PlayerEntity,')
BLOCKS = {'72': ('case 0x72:', 'case 0x86:'), 'A9': ('case 0xA9:', 'case 0xAA:'), 'AA': ('case 0xAA:', 'default:')}
# name -> (description, [(block, old, new, expected_count)])
M = {}
def add(n, d, *e): M[n] = (d, list(e))
A9_EQ = 'ReferenceEquals(entity.XCollisionEntity, found)'
AA_EQ = 'ReferenceEquals(found.XCollisionEntity, entity)'
A9_COND = 'entity.XCollisionEntity != null && ReferenceEquals(entity.XCollisionEntity, found)'
# sm3 mutants.json (0xA9, 0xAA)
add('S3-A9-swapped', 'sm3 0xA9:swapped (the found entity blocked by me instead of me blocked by it)', ('A9', A9_COND, 'found.XCollisionEntity != null && ReferenceEquals(found.XCollisionEntity, entity)', 1))
add('S3-AA-swapped', 'sm3 0xAA:swapped', ('AA', AA_EQ, 'entity.XCollisionEntity != null && ReferenceEquals(entity.XCollisionEntity, found)', 1))
add('S3-A9-first_only', 'sm3 0xA9:first_only', ('A9', SEARCH, 'System.Linq.Enumerable.Take(' + SEARCH + ', 1)', 1))
add('S3-AA-first_only', 'sm3 0xAA:first_only', ('AA', SEARCH, 'System.Linq.Enumerable.Take(' + SEARCH + ', 1)', 1))
add('S3-A9-hero_ref', 'sm3 0xA9:hero_ref (the reference is the hero, not the logic entity)', ('A9', SEARCH, SEARCH_HERO, 1), ('A9', 'entity.XCollisionEntity', '_worldContext.PlayerEntity.XCollisionEntity', 2))
add('S3-AA-hero_ref', 'sm3 0xAA:hero_ref', ('AA', SEARCH, SEARCH_HERO, 1), ('AA', AA_EQ, 'ReferenceEquals(found.XCollisionEntity, _worldContext.PlayerEntity)', 1))
add('S3-A9-keep_stale_on_false', 'sm3 0xA9:keep_stale_on_false (Result not cleared)', ('A9', 'state.Result = 0;', '', 1))
add('S3-AA-keep_stale_on_false', 'sm3 0xAA:keep_stale_on_false', ('AA', 'state.Result = 0;', '', 1))
add('S3-A9-keep_stale_on_true', 'sm3 0xA9:keep_stale_on_true (Result not set to 1)', ('A9', 'state.Result = 1;', '', 1))
add('S3-AA-keep_stale_on_true', 'sm3 0xAA:keep_stale_on_true', ('AA', 'state.Result = 1;', '', 1))
# sm3 critic extra mutants
add('S3-A9-by_ref_id', 'sm3 critic 0xA9:by_ref_id (EntityRefId compared instead of the reference)', ('A9', A9_COND, 'entity.XCollisionEntity != null && entity.XCollisionEntity.EntityRefId == found.EntityRefId', 1))
add('S3-AA-by_ref_id', 'sm3 critic 0xAA:by_ref_id', ('AA', AA_EQ, 'found.XCollisionEntity != null && found.XCollisionEntity.EntityRefId == entity.EntityRefId', 1))
add('S3-A9-excl_player', 'sm3 critic 0xA9:excl_player (the hero is dropped from the group)', ('A9', A9_COND, 'found != _worldContext.PlayerEntity && ' + A9_COND, 1))
add('S3-AA-excl_player', 'sm3 critic 0xAA:excl_player', ('AA', AA_EQ, 'found != _worldContext.PlayerEntity && ' + AA_EQ, 1))
# sm4 M72a-d
add('M72a', 'sm4 M72a the direction is not copied', ('72', 'entity.LastTargetDirection = entity.TargetDirection;', '', 1))
add('M72b', 'sm4 M72b acts on the owner, not on the logic entity', ('72', 'entity.LastTargetAnimationId = entity.TargetAnimationId;', 'owner.LastTargetAnimationId = owner.TargetAnimationId;', 1), ('72', 'entity.LastTargetDirection = entity.TargetDirection;', 'owner.LastTargetDirection = owner.TargetDirection;', 1))
add('M72c', 'sm4 M72c copies the other way (Target := Last)', ('72', 'entity.LastTargetAnimationId = entity.TargetAnimationId;', 'entity.TargetAnimationId = entity.LastTargetAnimationId;', 1), ('72', 'entity.LastTargetDirection = entity.TargetDirection;', 'entity.TargetDirection = entity.LastTargetDirection;', 1))
add('M72d', 'sm4 M72d guarded by a status 1..3', ('72', 'entity.LastTargetAnimationId = entity.TargetAnimationId;\r\n                entity.LastTargetDirection = entity.TargetDirection;', 'if (entity.Status >= (EntityStatus)1 && entity.Status <= (EntityStatus)3) { entity.LastTargetAnimationId = entity.TargetAnimationId;\r\n                entity.LastTargetDirection = entity.TargetDirection; }', 1))
# the six added by the plan
add('X72-animation-only-removed', 'added: 0x72 copies only the direction (the animation line removed)', ('72', 'entity.LastTargetAnimationId = entity.TargetAnimationId;', '', 1))
add('X72-fields-inverted', 'added: 0x72 fields crossed (animation into direction, direction into animation)', ('72', 'entity.LastTargetAnimationId = entity.TargetAnimationId;', 'entity.LastTargetAnimationId = entity.TargetDirection;', 1), ('72', 'entity.LastTargetDirection = entity.TargetDirection;', 'entity.LastTargetDirection = entity.TargetAnimationId;', 1))
add('X-A9-AA-exchanged', 'added: 0xA9 and 0xAA exchanged (both comparisons crossed)', ('A9', A9_COND, 'found.XCollisionEntity != null && ReferenceEquals(found.XCollisionEntity, entity)', 1), ('AA', AA_EQ, 'entity.XCollisionEntity != null && ReferenceEquals(entity.XCollisionEntity, found)', 1))
add('X-result-not-reset', 'added: Result not reset to 0 when nothing is found (both opcodes)', ('A9', 'state.Result = 0;', '', 1), ('AA', 'state.Result = 0;', '', 1))
add('X-A9-size1', 'added: 0xA9 of size 1 instead of 2', ('A9', 'return 2;', 'return 1;', 1))
add('X-AA-size1', 'added: 0xAA of size 1 instead of 2', ('AA', 'return 2;', 'return 1;', 1))
add('X72-size2', 'added: 0x72 of size 2 instead of 1', ('72', 'return 1;', 'return 2;', 1))
add('X-A9-owner', 'added: 0xA9 reads the owner instead of the logic entity', ('A9', SEARCH, SEARCH_OWNER, 1), ('A9', 'entity.XCollisionEntity', 'owner.XCollisionEntity', 2))
add('X-AA-owner', 'added: 0xAA reads the owner instead of the logic entity', ('AA', SEARCH, SEARCH_OWNER, 1), ('AA', AA_EQ, 'ReferenceEquals(found.XCollisionEntity, owner)', 1))
add('X72-result-cleared', 'added: 0x72 also clears Result', ('72', 'return 1;', 'state.Result = 0; return 1;', 1))

def read():
    b = open(RUN, 'rb').read()
    return b.decode('utf-8')

def apply(name):
    text = read()
    crlf = '\r\n' in text
    s = text.replace('\r\n', '\n')
    for blk, old, new, cnt in M[name][1]:
        old = old.replace('\r\n', '\n'); new = new.replace('\r\n', '\n')
        a, b = BLOCKS[blk]
        i = s.index(a); j = s.index(b, i)
        seg = s[i:j]
        assert seg.count(old) == cnt, (name, blk, old, seg.count(old))
        s = s[:i] + seg.replace(old, new) + s[j:]
    if crlf: s = s.replace('\n', '\r\n')
    open(RUN, 'wb').write(s.encode('utf-8'))

def git(*a):
    return subprocess.run(['git', '-C', REPO] + list(a), capture_output=True, text=True)

for name in sys.argv[1:]:
    assert git('diff', '--quiet', '--', 'Alundra/Events/EventProgramRunner.cs').returncode == 0, 'runner dirty before ' + name
    apply(name)
    p = subprocess.run(['dotnet', 'test', REPO + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + TARGETS,
                        '--filter', FILTER, '--blame-hang-timeout', '60s', '--nologo'], capture_output=True, text=True, encoding='utf-8', errors='replace')
    out = p.stdout + p.stderr
    git('checkout', '--', 'Alundra/Events/EventProgramRunner.cs')
    clean = git('diff', '--quiet', '--', 'Alundra').returncode == 0
    comp = 'error CS' in out
    mm = re.search(r'(?:[ée]chec|Failed)\s*:\s*(\d+),\s*(?:r[ée]ussite|Passed)\s*:\s*(\d+)', out, re.I)
    failed = sorted(set(re.findall(r'^\s+(?:[ÉE]chec|[ÉE]chou[ée]|Failed) (\S+)', out, re.M)))
    nf = int(mm.group(1)) if mm else -1
    m = mm; ok = None
    print(f'### {name} | {M[name][0]} | compile_error={comp} | failed={nf} | passed={mm.group(2) if mm else "?"} | diff_clean_after={clean}')
    for f in failed[:6]: print('    ', f)
    if len(failed) > 6: print('     ... +%d' % (len(failed) - 6))
    sys.stdout.flush()
