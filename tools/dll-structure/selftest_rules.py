"""Proof of `check_names.py --rules` (R7): mutation cases on throw-away copies of the living tree, OUTSIDE the repository.

  python -B tools/dll-structure/selftest_rules.py <work folder outside the repository>

The base is a copy of the tracked `.cs` files of Alundra/ and Alundra.Tests/ (a `git init` in the work folder, nothing touches the repository).
Each case edits a copy and runs `rules.check_rules(<copy>)`: a refused case must report the expected finding tag(s) (more tags may accompany them),
an accepted case must report no finding. Two cases also run the real command line (`check_names.py --rules --root`) for the exit codes.
Exit 1 if any case does not come out as expected.
"""
import os
import shutil
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rules  # noqa: E402
import scan_types  # noqa: E402

sys.stdout.reconfigure(encoding='utf-8')
REAL = Path(scan_types.REPO)


def cls(ns, name, extra=''):
    return 'namespace %s;\n\npublic class %s { %s }\n' % (ns, name, extra)


def write(case, rel, text):
    p = case / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding='utf-8', newline='\n')


def edit(case, rel, old, new):
    p = case / rel
    t = p.read_text(encoding='utf-8-sig')
    assert old in t, (rel, old)
    p.write_text(t.replace(old, new, 1), encoding='utf-8', newline='\n')


CASES = []


def case(name, fn, want):
    """want: set of tags that must appear; None = accepted (no finding at all)"""
    CASES.append((name, fn, want))


# ---- accepted
case('accept-baseline-live-tree', lambda c: None, None)
case('accept-new-file-existing-dll-folder', lambda c: write(c, 'Alundra/Audio/NewThing.cs', cls('Alundra.Audio', 'NewThing')), None)
case('accept-new-dll-folder', lambda c: write(c, 'Alundra/Quests/QuestLog.cs', cls('Alundra.Quests', 'QuestLog')), None)
case('accept-new-test-file-existing-folder', lambda c: write(c, 'Alundra.Tests/Audio/NewThingTests.cs', cls('Alundra.Tests.Audio', 'NewThingTests')), None)
case('accept-new-folder-with-its-test-mirror', lambda c: (write(c, 'Alundra/Quests/QuestLog.cs', cls('Alundra.Quests', 'QuestLog')),
                                                          write(c, 'Alundra.Tests/Quests/QuestLogTests.cs', cls('Alundra.Tests.Quests', 'QuestLogTests'))), None)
case('accept-exempt-test-folder-new-file', lambda c: write(c, 'Alundra.Tests/Arcs/NewScenario.cs', cls('Alundra.Tests.Arcs', 'NewScenario')), None)
case('accept-partial-two-files-one-namespace', lambda c: (write(c, 'Alundra/Audio/Split.cs', 'namespace Alundra.Audio;\n\npublic partial class Split { }\n'),
                                                         write(c, 'Alundra/Audio/Split.Part.cs', 'namespace Alundra.Audio;\n\npublic partial class Split { }\n')), None)
case('accept-block-namespace', lambda c: write(c, 'Alundra/Audio/Block.cs', 'namespace Alundra.Audio\n{\n    public class Block { }\n}\n'), None)
case('accept-tests-root-file', lambda c: write(c, 'Alundra.Tests/RootThing.cs', cls('Alundra.Tests', 'RootThing')), None)
case('accept-cs-under-obj', lambda c: write(c, 'Alundra/obj/Debug/net9.0-windows/Bad.cs', cls('Totally.Wrong', 'AlundraBad')), None)
case('accept-raw-literal-with-a-declaration-line', lambda c: write(
    c, 'Alundra.Tests/Support/RawHost.cs',
    'namespace Alundra.Tests.Support;\n\npublic class RawHost\n{\n    public const string S = """\npublic class AlundraFake { }\n""";\n}\n'), None)
case('accept-raw-literal-with-an-unbalanced-brace', lambda c: write(
    c, 'Alundra.Tests/Support/RawBrace.cs',
    'namespace Alundra.Tests.Support;\n\npublic class RawBrace\n{\n    public const string S = """\n  }\n""";\n}\n'), None)
case('accept-nested-type-named-like-a-segment-is-a-warning', lambda c: write(
    c, 'Alundra/Hero/Holder.cs', 'namespace Alundra.Hero;\n\npublic class Holder\n{\n    public class Audio { }\n}\n'), None)
# ---- refused
case('refuse-wrong-namespace-in-the-dll', lambda c: edit(c, 'Alundra/Audio/BgmPlayer.cs', 'namespace Alundra.Audio;', 'namespace Alundra.Combat;'), {'[namespace]'})
case('refuse-wrong-namespace-in-a-test', lambda c: edit(c, 'Alundra.Tests/Audio/MusicPlayerTests.cs', 'namespace Alundra.Tests.Audio;', 'namespace Alundra.Tests;'), {'[namespace]'})
case('refuse-wrong-namespace-in-plugin-cs', lambda c: edit(c, 'Alundra/Plugin.cs', 'namespace Alundra;', 'namespace Alundra.Foo;'), {'[namespace]'})
case('refuse-global-namespace-type', lambda c: write(c, 'Alundra/Audio/Glob.cs', 'public class Glob { }\n'), {'[namespace]'})
case('refuse-two-namespaces-in-a-file', lambda c: write(
    c, 'Alundra/Audio/Two.cs', 'namespace Alundra.Audio\n{\n    public class Two { }\n}\nnamespace Alundra.Hero\n{\n    public class Three { }\n}\n'), {'[namespace]'})
case('refuse-g-cs-outside-obj', lambda c: write(c, 'Alundra/Audio/Thing.g.cs', cls('Wrong.Ns', 'Gen')), {'[namespace]'})
case('refuse-folder-not-an-identifier', lambda c: write(c, 'Alundra/Foo-Bar/Thing.cs', cls('Alundra.Foo_Bar', 'Thing')), {'[dossier]'})
case('refuse-folder-world', lambda c: write(c, 'Alundra/World/WorldThing.cs', cls('Alundra.World', 'WorldThing')), {'[dossier]'})
case('refuse-prefix-on-a-type', lambda c: write(c, 'Alundra/Audio/AlundraNewThing.cs', cls('Alundra.Audio', 'AlundraNewThing')), {'[nom]'})
case('refuse-prefix-on-a-nested-type', lambda c: write(
    c, 'Alundra.Tests/Audio/NestedHost.cs', 'namespace Alundra.Tests.Audio;\n\npublic class NestedHost\n{\n    public class AlundraInner { }\n}\n'), {'[nom]'})
case('refuse-prefix-on-a-file-name', lambda c: write(c, 'Alundra/Audio/AlundraHelper.cs', cls('Alundra.Audio', 'Helper')), {'[nom]', '[principal]'})
case('refuse-prefix-on-a-test-class', lambda c: edit(c, 'Alundra.Tests/Audio/MusicPlayerTests.cs', 'public class MusicPlayerTests', 'public class AlundraMusicPlayerTests'), {'[nom]', '[principal]'})
case('refuse-collision-vector2', lambda c: write(c, 'Alundra/Audio/Vector2.cs', cls('Alundra.Audio', 'Vector2')), {'[collision]'})
case('refuse-collision-random', lambda c: write(c, 'Alundra/Rng/Random.cs', cls('Alundra.Rng', 'Random')), {'[collision]'})
case('refuse-collision-xunit-assert', lambda c: write(c, 'Alundra.Tests/Support/Assert.cs', cls('Alundra.Tests.Support', 'Assert')), {'[collision]'})
case('refuse-collision-yarn-dialogue-compiler', lambda c: write(c, 'Alundra.Tests/Support/YarnDialogueCompiler.cs', cls('Alundra.Tests.Support', 'YarnDialogueCompiler')), {'[collision]'})
case('refuse-duplicate-in-the-dll', lambda c: write(c, 'Alundra/Combat/BgmPlayer.cs', cls('Alundra.Combat', 'BgmPlayer')), {'[doublon]'})
case('refuse-test-named-like-a-dll-type', lambda c: write(c, 'Alundra.Tests/Support/BgmPlayer.cs', cls('Alundra.Tests.Support', 'BgmPlayer')), {'[doublon]'})
case('refuse-partial-on-two-namespaces', lambda c: (write(c, 'Alundra/Audio/Split.cs', 'namespace Alundra.Audio;\n\npublic partial class Split { }\n'),
                                                    write(c, 'Alundra/Hero/Split.cs', 'namespace Alundra.Hero;\n\npublic partial class Split { }\n')), {'[doublon]'})
case('refuse-type-named-like-a-segment', lambda c: write(c, 'Alundra/Hero/Audio.cs', cls('Alundra.Hero', 'Audio')), {'[segment]'})
case('refuse-orphan-brace', lambda c: write(c, 'Alundra/Audio/Orphan.cs', 'namespace Alundra.Audio;\n\npublic class Orphan\n{\n    public void M()\n    {\n    }\n'), {'[lecture]'})
case('refuse-declaration-not-at-line-start', lambda c: write(
    c, 'Alundra.Tests/Audio/NestedHost.cs', 'namespace Alundra.Tests.Audio;\n\npublic class NestedHost { public class AlundraInner { } }\n'), {'[lecture]'})
case('refuse-declaration-after-namespace-line', lambda c: write(c, 'Alundra/Audio/Hidden.cs', 'namespace Alundra.Audio; public class Hidden { }\n'), {'[lecture]'})
case('refuse-public-secondary-type', lambda c: write(
    c, 'Alundra/Audio/Main.cs', 'namespace Alundra.Audio;\n\npublic class Main { }\n\npublic class Second { }\n'), {'[principal]'})
case('refuse-file-name-borne-by-no-type', lambda c: write(c, 'Alundra/Audio/Wrong.cs', cls('Alundra.Audio', 'Other')), {'[principal]'})
case('refuse-test-folder-without-a-dll-counterpart', lambda c: write(c, 'Alundra.Tests/Nonsense/Thing.cs', cls('Alundra.Tests.Nonsense', 'Thing')), {'[miroir]'})


def build_base(work):
    base = work / 'base'
    if base.exists():
        shutil.rmtree(base)
    base.mkdir(parents=True)
    out = subprocess.run(['git', '-C', str(REAL), 'ls-files', '--', 'Alundra', 'Alundra.Tests'], capture_output=True, check=True).stdout.decode().splitlines()
    for p in out:
        if p.endswith('.cs'):
            d = base / p
            d.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(REAL / p, d)
    (base / '.gitignore').write_text('[Bb]in/\n[Oo]bj/\n', encoding='utf-8')
    return base


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    work = Path(sys.argv[1]).resolve()
    if str(work).replace('\\', '/').lower().startswith(str(REAL).replace('\\', '/').lower() + '/'):
        print('ERROR: the work folder must be outside the repository')
        return 2
    work.mkdir(parents=True, exist_ok=True)
    base = build_base(work)
    bad = 0
    for name, fn, want in CASES:
        c = work / ('case-' + name)
        if c.exists():
            shutil.rmtree(c)
        shutil.copytree(base, c)
        subprocess.run(['git', 'init', '-q'], cwd=str(c), check=True)
        fn(c)
        findings, warnings, _head = rules.check_rules(str(c))
        tags = {f.split(' ', 1)[0] for f in findings}
        ok = (not findings) if want is None else (want <= tags)
        bad += 0 if ok else 1
        print('%-4s %-58s %s' % ('OK' if ok else 'FAIL', name, 'no finding' if not findings else sorted(tags)) + ('  (+%d warning)' % len(warnings) if warnings else ''))
        if not ok:
            for f in findings[:4] or ['(no finding)']:
                print('       ', f[:220])
    # the real command line: exit codes
    cli = os.path.join(HERE, 'check_names.py')
    for name, code in (('accept-baseline-live-tree', 0), ('refuse-wrong-namespace-in-the-dll', 1)):
        r = subprocess.run([sys.executable, '-B', cli, '--rules', '--root', str(work / ('case-' + name))], capture_output=True)
        out = r.stdout.decode('utf-8', 'replace')
        ok = r.returncode == code and ('finding(s)' in out)
        bad += 0 if ok else 1
        print('%-4s %-58s exit %d (expected %d)' % ('OK' if ok else 'FAIL', 'cli: ' + name, r.returncode, code))
    print('selftest_rules: %d case(s) + 2 command-line runs, %d unexpected' % (len(CASES), bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
