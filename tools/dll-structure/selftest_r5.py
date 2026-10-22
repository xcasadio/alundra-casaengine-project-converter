"""Proofs of the R5 proof (docs/plan-restructuration-dll.md, section 9, T0c): mutations that prove_r5.py must refuse.

  python -B tools/dll-structure/selftest_r5.py --repo <throw-away copy> --domain <D> [--only-domain] --suite support|audio

The copy holds, in its working tree, the result of `apply_r5.py <D> --code` (and `--move` committed before), as the executor would have it before
committing. The state of before is the HEAD of the copy. Every mutation is made in memory on a copy of the dictionary of the files of after and
handed to prove_r5.prove_code; a refusal is the expected answer, except for the cases named `accepted`. Exit 0 when every case answers as expected.

Suite `audio` (domain Audio): a mutated value of a test, a literal that is not in the survey, a comment, an omitted L1 entry, a cut block changed
by one byte, a cut made at another domain, a type cut twice, the literal of the data file, and the L1 entries inside a cut block
(`SoundBankTests.cs`, the blocks `SoundBankAttributeTests` and `SoundBankChainTests`) and under a cut block (`MusicPlayerTests.cs`, below the
collection): accepted when they change as the survey says, refused when they stay or change otherwise.
Suite `support` (domain Support): a Q1 entry omitted, a `namespace` line not changed, a using added without a reason, a using removed, and an
internal type that is not shared cut into its own file.
"""
import argparse
import copy
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import r5_lib as L  # noqa: E402
import prove_r5 as P  # noqa: E402

A = L.A


def once(text, old, new, count=1):
    if text.count(old) < 1:
        raise SystemExit('selftest: %r not found' % old[:60])
    return text.replace(old, new, count)


def prove(tbl, domain, before, after, only):
    rep = P.Report()
    P.prove_code(tbl, domain, before, after, rep, only)
    return rep.problems


def suite_audio(after):
    """[(name, expected, mutated after)]"""
    cases = []

    def case(name, expected, fn):
        a = copy.deepcopy(after)
        fn(a)
        cases.append((name, expected, a))

    sb = 'Alundra.Tests/Audio/SoundBankTests.cs'
    sba = 'Alundra.Tests/Audio/SoundBankAttributeTests.cs'
    sbc = 'Alundra.Tests/Audio/SoundBankChainTests.cs'
    mp = 'Alundra.Tests/Audio/MusicPlayerTests.cs'
    case('baseline', 'accepted', lambda a: None)
    # a value expected by a test
    def val(a):
        p = 'Alundra.Tests/Audio/MusicIndexTableTests.cs'
        t = a[p]
        m = re.search(r'Assert\.Equal\((\d+)', t)
        a[p] = t[:m.start(1)] + str(int(m.group(1)) + 1) + t[m.end(1):]
    case('a value expected by a test changed', 'refused', val)
    case('a string literal that is not in the survey changed', 'refused', lambda a: a.__setitem__(mp, once(a[mp], 'Ship Klark (beginning)-389', 'Ship Klark (beginning)-388')))
    def comment(a):
        t = a[mp]
        m = re.search(r'^( *// )(\w)', t, re.M)
        a[mp] = t[:m.start(2)] + ('X' if m.group(2) != 'X' else 'Y') + t[m.end(2):]
    case('a comment changed', 'refused', comment)
    bgm = 'Alundra.Tests/Audio/BgmFadeDirectorTests.cs'
    case('an L1 entry omitted (temp folder prefix of a file that is not cut)', 'refused', lambda a: a.__setitem__(bgm, once(a[bgm], '"BgmFadeDirectorTests_Corpus_"', '"AlundraBgmFadeDirectorTests_Corpus_"')))
    # the cut blocks
    case('a cut block changed by one byte', 'refused', lambda a: a.__setitem__(sba, once(a[sba], 'public class SoundBankAttributeTests', 'public class SoundBankAttributeTestz')))
    def twice(a):   # the block of SoundBankChainTests also stays in its source
        blk = a[sbc][len(L.split_head(A.split_bom(a[sbc])[1])[0]) + len(A.split_bom(a[sbc])[0]):]
        a[sb] = a[sb].rstrip('\n') + '\n' + blk
    case('a type cut twice (also left in its source)', 'refused', twice)
    def missing_new(a):
        del a[sbc]
    case('a cut file missing', 'refused', missing_new)
    # the data file literal (the audit file is checked in every domain)
    au = 'Alundra.Tests/AlundraStoryChainOpcodeAudit.cs'
    case('the literal that names the data file changed', 'refused', lambda a: a.__setitem__(au, once(a[au], 'story-chain-skipped-opcodes', 'story-chain-skipped-opcodez')))
    # L1 entries in and under cut blocks
    case('L1 in a cut block: unchanged', 'refused', lambda a: a.__setitem__(sba, once(a[sba], '"SoundBankAttributeTests_"', '"AlundraSoundBankAttributeTests_"')))
    case('L1 in a cut block: changed otherwise', 'refused', lambda a: a.__setitem__(sba, once(a[sba], '"SoundBankAttributeTests_"', '"SoundBankAttrTests_"')))
    case('L1 in the second cut block: unchanged', 'refused', lambda a: a.__setitem__(sbc, once(a[sbc], '"SoundBankChainTests_"', '"AlundraSoundBankChainTests_"')))
    case('L1 under a cut block: unchanged', 'refused', lambda a: a.__setitem__(mp, once(a[mp], '"MusicPlayerTests_"', '"AlundraMusicPlayerTests_"')))
    case('L1 under a cut block: changed otherwise', 'refused', lambda a: a.__setitem__(mp, once(a[mp], '"MusicPlayerTests_"', '"MusicPlayer_"')))
    return cases


def suite_support(after, tbl):
    cases = []

    def case(name, expected, fn):
        a = copy.deepcopy(after)
        fn(a)
        cases.append((name, expected, a))

    case('baseline', 'accepted', lambda a: None)
    f = 'Alundra.Tests/AlundraSaveBookTests.cs'
    case('a Q1 entry omitted (using static)', 'refused', lambda a: a.__setitem__(f, once(a[f], 'using static Alundra.Tests.Support.SaveGameDirectorTestSupport;', 'using static Alundra.Tests.SaveGameDirectorTestSupport;')))
    def ns(a):
        p = 'Alundra.Tests/Support/FakeAudioBackend.cs'
        a[p] = once(a[p], 'namespace Alundra.Tests.Support;', 'namespace Alundra.Tests;')
    case('a namespace line not changed', 'refused', ns)
    case('a using added without a reason', 'refused', lambda a: a.__setitem__(f, once(a[f], 'using static Alundra.Tests.Support.SaveGameDirectorTestSupport;', 'using static Alundra.Tests.Support.SaveGameDirectorTestSupport;\nusing Alundra.Tests.Hero;')))
    def rm(a):
        t = a[f]
        m = re.search(r'^using Alundra\.[\w.]+;\r?\n', t, re.M)
        a[f] = t[:m.start()] + t[m.end():]
    case('a using removed', 'refused', rm)
    def cut_internal(a):   # HeroPad is internal and not shared: it stays in JumpNpcRig.cs
        p = 'Alundra.Tests/Support/JumpNpcRig.cs'
        newtext, rest = A.cut_type(a[p], 'HeroPad', 'Alundra.Tests.Support')
        a['Alundra.Tests/Support/HeroPad.cs'] = newtext
        a[p] = rest
    case('an internal type that is not shared cut into its own file', 'refused', cut_internal)
    def other_domain_file(a):
        a['Alundra.Tests/Support/Extra.cs'] = 'namespace Alundra.Tests.Support;\n\npublic class Extra { }\n'
    case('a file that the table does not make', 'refused', other_domain_file)
    return cases


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--repo', required=True)
    ap.add_argument('--domain', required=True)
    ap.add_argument('--only-domain', action='store_true')
    ap.add_argument('--suite', choices=('audio', 'support'), required=True)
    a = ap.parse_args()
    sys.stdout.reconfigure(encoding='utf-8')
    A.REPO = L.REPO = os.path.abspath(a.repo)
    tbl = L.R5(HERE)
    before = L.read_rev('HEAD')
    after = L.read_dir_tree(L.REPO)
    cases = suite_audio(after) if a.suite == 'audio' else suite_support(after, tbl)
    bad = 0
    for name, expected, tree in cases:
        problems = prove(tbl, a.domain, before, tree, a.only_domain)
        got = 'accepted' if not problems else 'refused'
        ok = got == expected
        bad += 0 if ok else 1
        print('%-6s %-70s expected %-8s got %-8s%s' % ('OK' if ok else 'FAIL', name, expected, got, '' if problems else ''))
        if not ok or (expected == 'refused' and os.environ.get('VERBOSE')):
            for p in problems[:3]:
                print('         ', p[:200])
    if a.suite == 'audio':   # a cut made at another domain: the same commit proven as the Rendering domain
        problems = prove(tbl, 'Rendering', before, after, True)
        ok = bool(problems)
        bad += 0 if ok else 1
        print('%-6s %-70s expected %-8s got %-8s' % ('OK' if ok else 'FAIL', 'the cuts of Audio proven as another domain (Rendering)', 'refused', 'refused' if ok else 'accepted'))
        cases = cases + [None]
    print('selftest_r5 %s %s: %d case(s), %d unexpected' % (a.suite, a.domain, len(cases), bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
