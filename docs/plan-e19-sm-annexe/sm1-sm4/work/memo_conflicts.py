"""For every C program that contains 0x6A..0x6D, list the opcodes that write Parameters[2..4] (as the DLL ports them) and
check, in linear program order, whether one sits between a 0x6A and a later 0x6B/0x6C/0x6D (read-only on the corpus).
Writers of Parameters[2..4] in the DLL (AlundraEventProgramRunner.cs:2700-3050): 0x37 [2], 0x0B/0x1E/0x1F [2][3], 0x20/0x21 [2], 0x22/0x23 [2],
0x1C/0x1D [2] and 0x5F (not ported) [2..6]."""
import csv, collections
import corpus

WRITERS = {0x37: '[2]', 0x0B: '[2][3]', 0x1E: '[2][3]', 0x1F: '[2][3]', 0x20: '[2]', 0x21: '[2]', 0x22: '[2]', 0x23: '[2]',
           0x1C: '[2]', 0x1D: '[2]', 0x5F: '[2..6]'}
rows = list(csv.DictReader(open('D:/development/repo/alundra-casaengine-project-converter/docs/plan-e19-sm-annexe/skipped-sites.tsv', encoding='utf-8'), delimiter='\t'))
progs = collections.OrderedDict()
for r in rows:
    op = int(r['opcode'], 16)
    if 0x6A <= op <= 0x6D:
        progs[(int(r['map']), r['slot'], int(r['program']))] = 1
print('programs with 6A..6D:', len(progs))
conflicts = 0
reads_without_save = []
for (m, s, p) in progs:
    base, d = corpus.load(m)
    codes = d['Codes']
    entry = d[corpus.TABLE_KEYS[s]][p]
    seq = corpus.decode(codes, entry, count=4000, stop_at_end=True)
    last6a = None
    seen_writer = []
    has6a = any(o == 0x6A for _, o, _ in seq)
    for pc, o, ops in seq:
        if o == 0x6A:
            last6a = pc
            seen_writer = []
        elif o in WRITERS and last6a is not None:
            seen_writer.append((pc, o))
        elif 0x6B <= o <= 0x6D:
            if seen_writer:
                conflicts += 1
                print('  CONFLICT? map %d %s[%d]: 6A@%d ... writers %s ... %02X@%d' % (m, s, p, last6a, [(a, '%02X' % b) for a, b in seen_writer], o, pc))
            if last6a is None:
                reads_without_save.append((m, s, p, pc, o))
    if not has6a:
        print('  no 6A in program: map %d %s[%d] (reads a stale/zero memo)' % (m, s, p))
print('linear-order conflicts:', conflicts)
print('reads before any 6A in linear order:', [(m, s, p, pc, '%02X' % o) for m, s, p, pc, o in reads_without_save][:20])
