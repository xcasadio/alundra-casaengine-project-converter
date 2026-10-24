"""compare_contacts.py - E20.g1b T0 (b): compares the contact per tick of the DLL probe (probes/dll-probe.txt, G1bT0ProbeTests) with the oracle
(../../e20-g1a/critic-scenarios/out/c3-contact-ticks.txt for the lines A..G, out/air-ticks.txt for the air) at the ticks the plan compares.
Prints one verdict line per case and per ticks-per-frame; exit code 1 on the first difference (STOP of the plan).  Run: python -B compare_contacts.py"""
import re, sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
COMPARED = {'A': 9, 'B': 10, 'C': 9, 'D': 15, 'D2': 15, 'E': 16, 'E2': 5, 'F': 16, 'G': 16}   # the plan: D (Square t15): t1..t16 ; D2 (Square t16): t1..t15 ; E (Square t5): t1..t16
COMPARED['D'] = 16


def parse(path, header_re):
    cases, cur = {}, None
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if line.startswith('== '):
            m = header_re.match(line)
            cur = None
            if m:
                cur = m.groups()
                cases[cur] = {}
            continue
        m = re.match(r't(\d+) .*?xcoll=(\S+)(?: anim=(\d+))?', line)
        if m and cur is not None:
            cases[cur][int(m.group(1))] = m.group(2) != '-'
    return cases


def main():
    bad = 0
    orc = parse(os.path.join(HERE, '..', '..', 'e20-g1a', 'critic-scenarios', 'out', 'c3-contact-ticks.txt'), re.compile(r'== (\S+) '))
    dll = parse(os.path.join(HERE, 'probes', 'dll-probe.txt'), re.compile(r'== b2 ground (\S+) .*?, (\d) tick'))
    for (tag, tpf), rows in sorted(dll.items(), key=lambda kv: (kv[0][0], kv[0][1])):
        o = orc[(tag,)]
        n = min(COMPARED[tag], max(o))   # the oracle script of E has 15 ticks (4 + 1 + 10): the plan's t1..t16 is capped at the length of the oracle run
        diff = [t for t in range(1, n + 1) if rows.get(t) != o.get(t)]
        print('line %-3s %s tick(s)/frame: ticks t1..t%d compared, %s' % (tag, tpf, n, 'EQUAL' if not diff else 'DIFFERENT at ' + str(diff)))
        bad += bool(diff)
    # air: oracle without Square
    air_o = {}
    cur = None
    for line in open(os.path.join(HERE, 'out', 'air-ticks.txt'), encoding='utf-8'):
        if line.startswith('== '):
            cur = line.split(': ')[1].strip()
            air_o[cur] = {}
            continue
        m = re.match(r't(\d+) .* xcoll=(\S+) anim=(\d+)', line)
        z = re.search(r'z=([\d.]+)', line)
        if m:
            air_o[cur][int(m.group(1))] = (m.group(2) != '-', float(z.group(1)), int(m.group(3)))
    air_d, cur = {}, None
    for line in open(os.path.join(HERE, 'probes', 'dll-probe.txt'), encoding='utf-8'):
        if line.startswith('== '):
            m = re.match(r'== b1 air, no Square, (\d) tick', line)
            cur = m.group(1) if m else None
            if cur: air_d[cur] = {}
            continue
        m = re.match(r't(\d+) x=\S+ z=([\d.]+) xcoll=(\S+) anim=(\d+)', line)
        if m and cur:
            air_d[cur][int(m.group(1))] = (m.group(3) != '-', float(m.group(2)), int(m.group(4)))
    for tpf, rows in sorted(air_d.items()):
        diff = [t for t in range(4, 10) if rows.get(t) != air_o['none'].get(t)]
        print('air, no Square, %s tick(s)/frame: t4..t9 (contact, z, anim) %s' % (tpf, 'EQUAL' if not diff else 'DIFFERENT at %s: dll %s oracle %s' % (diff, [rows.get(t) for t in diff], [air_o['none'].get(t) for t in diff])))
        bad += bool(diff)
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
