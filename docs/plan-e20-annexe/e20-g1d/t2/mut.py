# E20.g1d T2: real mutants, applied by script, narrow suite (Release, neutralised copy), undone, `git diff` checked empty.
# usage: python -B mut.py <outfile> [mNN ...]
import subprocess, sys, re
R = 'D:/development/repo/alundra-casaengine-project-converter'
SK = 'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
EK = R + '/Alundra/Entities/EntityDepthKey.cs'
FS = R + '/Alundra/Maps/Loop/FrameSyncPasses.cs'
EP = R + '/Alundra/Entities/EntityScriptProxy.cs'
CR = R + '/Alundra/Hero/CarryRules.cs'
CLASSES = ['EntityDrawTieTests', 'CarriedSortKeyPassTests', 'CarriedSortKeyOracleTests', 'CarriedSortKeyTests', 'BombHandsDrawOrderTests', 'CrateHandsDrawOrderTests', 'CarryRulesThrowTests', 'FrozenAnchorTests', 'WorldProxyEntityManipulationTests']
FILTER = '|'.join('FullyQualifiedName~' + c for c in CLASSES)
SIGN = 'Math.Sign(((int)objectKey).CompareTo((int)partnerKey))'
muts = [
 ('m01 K = 13', EK, 'HeldDrawOffset = 14;', 'HeldDrawOffset = 13;'),
 ('m02 the anchor is not reset to (0, 0) when the object leaves the hands', FS, 'depthSortable.SortAnchorLocal = tie.Coordinate is { } wanted ? new Vector2(0f, EntityDepthKey.AnchorY(renderY, wanted)) : Vector2.Zero;', 'if (tie.Coordinate is { } wanted) { depthSortable.SortAnchorLocal = new Vector2(0f, EntityDepthKey.AnchorY(renderY, wanted)); }'),
 ('m03 the local offset is not reset to 0 when the object leaves the hands', FS, 'depthSortable.LocalSortOffset = tie.LocalOffset;', 'if (tie.LocalOffset != 0) { depthSortable.LocalSortOffset = tie.LocalOffset; }'),
 ('m04 the rule applied to an entity neither held nor thrown (offset +K)', EK, 'default:\n                return default;', 'default:\n                return new DrawTie(partnerCoordinate, HeldDrawOffset);'),
 ('m05 the Elevation raised for the collidable objects only', EK, 'if (proxy.PlatformEntity?.GameplayProxy is not EntityScriptProxy carrier)', 'if ((proxy.Flags & EntityFlags.Collidable) == 0 || (proxy.AnimFlags & 0x80) != 0 || proxy.PlatformEntity?.GameplayProxy is not EntityScriptProxy carrier)'),
 ('m06 the anchor computed on the logical positions instead of the rendered translations', FS, 'coordinate = partnerDepth.BuildSortKey(partner.RenderProjection.WorldMatrixNoScale.Translation).SortCoordinate;', 'coordinate = 100 * ((partner.PosY >> 16) - (partner.PosZ >> 16));'),
 ('m07 the throw without the term delta', EK, 'c + 100 * delta', 'c'),
 ('m08 -K at equality', EK, SIGN + ' * HeldDrawOffset', '(' + SIGN + ' == 0 ? -1 : ' + SIGN + ') * HeldDrawOffset'),
 ('m09 the mark not set by ThrowCarried', CR, 'item.ThrownBy = hero;', ''),
 ('m10 the mark does not fall at the landing (IsOnGround ignored)', FS, 'proxy.PlatformEntity == null && proxy.IsOnGround == 0 && proxy.Status', 'proxy.PlatformEntity == null && proxy.Status'),
 ('m11 the anchored effect does not follow the pass', EP, 'DrawnSortCoordinate ?? 100 *', '100 *'),
 ('m12 the held object not raised at all (the tick 162 left behind)', EK, 'return depthSortValue < carrierKey ? carrierKey : depthSortValue;', 'return depthSortValue;'),
 ('m13 the entities FlagToDestroy visited by the pass', FS, '\n\n            if (entity.GameplayProxy is not EntityScriptProxy proxy || proxy.Status == EntityStatus.FlagToDestroy)\n            {\n                continue;\n            }\n\n            var depthSortable', '\n\n            if (entity.GameplayProxy is not EntityScriptProxy proxy)\n            {\n                continue;\n            }\n\n            var depthSortable'),
 ('m14 a held object without the offset +K', EK, 'return new DrawTie(partnerCoordinate, HeldDrawOffset);', 'return new DrawTie(partnerCoordinate, 0);'),
 ('m15 the mark does not fall when the object is held again', FS, 'if (proxy.Flags2 == 0)', 'if (false)'),
 ('m16 the mark does not fall at another status', FS, ' && proxy.Status == EntityStatus.Normal && thrower', ' && thrower'),
 ('m17 the sign of delta reversed', EK, 'var delta = ((int)objectKey >> 16) - ((int)partnerKey >> 16);', 'var delta = ((int)partnerKey >> 16) - ((int)objectKey >> 16);'),
 ('m18 a held object without the coordinate of its carrier', EK, 'return new DrawTie(partnerCoordinate, HeldDrawOffset);', 'return new DrawTie(null, HeldDrawOffset);'),
 ('m19 the sign of K reversed for a held object', EK, 'return new DrawTie(partnerCoordinate, HeldDrawOffset);', 'return new DrawTie(partnerCoordinate, -HeldDrawOffset);'),
 ('m20 the thrown role never reached (the object ordered as a plain one)', FS, 'role = EntityDepthKey.DrawRole.Thrown;', 'role = EntityDepthKey.DrawRole.Plain;'),
]
out = sys.argv[1]
only = sys.argv[2:]
for name, path, old, new in muts:
    if only and name.split(' ')[0] not in only:
        continue
    raw = open(path, encoding='utf-8', newline='').read()
    crlf = '\r\n' in raw
    src = raw.replace('\r\n', '\n')
    if old not in src:
        res = 'PATTERN NOT FOUND'
    else:
        mutated = src.replace(old, new, 1)
        if crlf:
            mutated = mutated.replace('\n', '\r\n')
        open(path, 'w', encoding='utf-8', newline='').write(mutated)
        try:
            b = subprocess.run(['dotnet', 'build', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '--no-restore', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--nologo', '-v', 'q'], cwd=R, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=500)
            if re.search(r': error ', b.stdout):
                res = 'BUILD ERROR ' + ' | '.join(l for l in b.stdout.splitlines() if ': error ' in l)[:300]
            else:
                t = subprocess.run(['dotnet', 'test', 'Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '--no-build', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--blame-hang-timeout', '60s', '--filter', FILTER, '--nologo'], cwd=R, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=580)
                failed = sorted(set(re.sub(r'^Alundra\.Tests\.', '', m.group(1)) for m in re.finditer(r'^\s+(?:Échoué|Failed)\s+(\S+)', t.stdout, re.M)))
                tot = re.search(r'(?:Échoué|Réussi|Échec|Failed|Passed)!.*', t.stdout)
                res = ('KILLED by %d: %s' % (len(failed), '; '.join(failed))) if failed else 'SURVIVED'
                res += '   [' + (tot.group(0) if tot else 'no summary') + ']'
        finally:
            open(path, 'w', encoding='utf-8', newline='').write(raw)
    d = subprocess.run(['git', '-C', R, 'diff', '--ignore-cr-at-eol', '--stat', '--', 'Alundra', 'Alundra.Tests'], capture_output=True, text=True).stdout.strip()
    line = '%s -> %s | git diff after undo: %s' % (name, res, 'EMPTY' if not d else d)
    print(line, flush=True)
    open(out, 'a', encoding='utf-8').write(line + '\n')
