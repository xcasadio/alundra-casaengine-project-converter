"""mutate.py <name>...: applies each real mutation of E20.f6a T1/T2 to the DLL sources, builds Release (neutralised copy), runs the filtered tests of the slice and of the hero ground-slope tests, prints the failing names, then restores the files from the backups."""
import re
import subprocess
import sys
import shutil

REPO = r'D:/development/repo/alundra-casaengine-project-converter'
SK = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad/e20e0a3b-exec/skipcopy.targets'
SP = r'C:/Users/casad/AppData/Local/Temp/claude/D--development-repo-alundra-casaengine-project-converter/b00d1a72-420d-4acc-ab34-0c25fbdad3a1/scratchpad'
PROXY = REPO + '/Alundra/Entities/EntityScriptProxy.cs'
PROBE = REPO + '/Alundra/Physics/TerrainProbe.cs'

CALL = '''            if (HasLogicalZ)
            {
                UpdateGroundSlope();
            }
        }

        EvaluateEntitySupport('''


def read(p):
    return open(p, 'rb').read().decode('utf-8')


def write(p, s):
    open(p, 'wb').write(s.encode('utf-8'))


def sub(s, a, b, count=1):
    a = a.replace('\n', '\r\n')
    b = b.replace('\n', '\r\n')
    assert s.count(a) >= 1, a
    return s.replace(a, b, count)


MUT = {
    # file, old, new
    'M1_all_gravity_entities_any_status': (PROXY, CALL, '''        }

        UpdateGroundSlope();
        EvaluateEntitySupport('''),
    'M2_no_marker': (PROXY, '''            if (HasLogicalZ)
            {
                UpdateGroundSlope();
            }
        }

        EvaluateEntitySupport(''', '''            UpdateGroundSlope();
        }

        EvaluateEntitySupport('''),
    'M3_max_instead_of_min': (PROBE, 'if (masked < bestFlagMask)\n                {\n                    bestFlagMask = masked;\n                }\n\n                return;\n            }\n        }\n\n        bestFlagMask = 0;', 'if (masked > bestFlagMask)\n                {\n                    bestFlagMask = masked;\n                }\n\n                return;\n            }\n        }\n\n        bestFlagMask = 0;'),
    'M4_z_equality_shifted': (PROBE, 'if (height == moddedPosZ)\n            {\n                var groundProperty = field.SampleGroundProperty(position);\n                var masked = ((uint)groundProperty << 8) & 0xe00u;', 'if (height == moddedPosZ + 65536)\n            {\n                var groundProperty = field.SampleGroundProperty(position);\n                var masked = ((uint)groundProperty << 8) & 0xe00u;'),
    'M5a_corner_x1y1_ignored': (PROXY, 'TerrainProbe.ProbeSlopeCorner(cellsField, x1, y1, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y1, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c', '        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y1, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c'),
    'M5b_corner_x2y1_ignored': (PROXY, 'TerrainProbe.ProbeSlopeCorner(cellsField, x2, y1, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c', '        TerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c'),
    'M5c_corner_x1y2_ignored': (PROXY, 'TerrainProbe.ProbeSlopeCorner(cellsField, x1, y2, moddedPosZ, ref bestFlagMask);\n        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c', '        TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c'),
    'M5d_corner_x2y2_ignored': (PROXY, 'TerrainProbe.ProbeSlopeCorner(cellsField, x2, y2, moddedPosZ, ref bestFlagMask);\n\n        Slope_18c', '\n        Slope_18c'),
    'M6_call_limited_to_status_2': (PROXY, '''            if (HasLogicalZ)
            {
                UpdateGroundSlope();
            }''', '''            if (HasLogicalZ && Status == EntityStatus.Normal)
            {
                UpdateGroundSlope();
            }'''),
}


def run(cmd):
    return subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace', shell=False)


def main():
    names = sys.argv[1:]
    originals = {p: read(p) for p in (PROXY, PROBE)}
    try:
        for name in names:
            path, old, new = MUT[name]
            s = originals[path]
            old_c = old.replace('\n', '\r\n')
            if old_c not in s:
                # TerrainProbe may be LF
                s2 = s.replace(old, new, 1)
                assert s2 != s, 'no match for ' + name
            else:
                s2 = s.replace(old_c, new.replace('\n', '\r\n'), 1)
            write(path, s2)
            b = run(['dotnet', 'build', REPO + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--nologo', '-v', 'q'])
            errs = [l for l in b.stdout.splitlines() if ' error ' in l]
            if errs:
                print(name, 'BUILD ERROR', errs[0][:200])
                write(path, originals[path])
                continue
            t = run(['dotnet', 'test', REPO + '/Alundra.Tests/Alundra.Tests.csproj', '-c', 'Release', '-p:CustomAfterMicrosoftCommonTargets=' + SK, '--no-build', '--filter',
                     'FullyQualifiedName~MarkedEntityGroundSlopeTests|FullyQualifiedName~GroundSlopeTests', '--blame-hang-timeout', '60s', '--nologo', '--logger', 'console;verbosity=normal'])
            failed = sorted(set(re.sub(r'^\s+\S+\s+Alundra\.Tests\.\w+\.', '', l).split(' [')[0][:150] for l in t.stdout.splitlines()
                                if re.match(r'^  \S+ Alundra\.Tests\.', l) and not l.startswith('  R')))
            print('==', name, 'FAILED:', len(failed))
            for f in failed:
                print('   ', f)
            write(path, originals[path])
    finally:
        for p, s in originals.items():
            write(p, s)


main()
