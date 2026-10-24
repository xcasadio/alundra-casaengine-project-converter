"""classify_survivors.py - merges ../results/mut_words_*.txt and classifies every surviving word mutant of the REAL code as equivalent (with the reason) or UNEXPLAINED.
Reasons are rules on the mutated instruction (stack frame words, register copies) or entries of MANUAL (address, operator) judged equivalent by reading the code (see report.md section 12)."""
import glob, re, sys, collections
MANUAL = {
    ('start_bomb', 0x800345AC, 'slti<->sltiu'): 'slti on a counter in 0..64: signed and unsigned compares agree',
    ('dispatch', 0x800350D8, 'imm-1'): 'tome entry 0x32 of the cancel table (h4 domain, the effect of its cancel needs an entity of type 0x18 that these vectors do not place)',
    ('dispatch', 0x800350D8, 'imm+1'): 'bound 0x14 -> 0x15 reads the word after the table: unreachable item (the start refuses it first)',
    ('dispatch', 0x800350D8, 'slti<->sltiu'): 'compare of a value already tested unsigned',
    ('C62', 0x8007A9C4, 'imm+1'): 'the spark rule is v < 60 and v % 8 == 0: v < 61 gives the same set',
    ('C62', 0x8007A9C4, 'imm-1'): 'v < 59 gives the same set (59 is not a multiple of 8)',
    ('C62', 0x8007AA58, 'store removed'): 'intermediate store of the random word, overwritten by the final store 0x8007AA74 before any read',
    ('C62', 0x8007AA60, 'offset+4'): 'dead spill of the high/low product',
    ('C62', 0x8007AA60, 'store removed'): 'dead spill of the high/low product',
    ('C62', 0x8007AA68, 'offset+4'): 'dead reload of the spill',
    ('C62', 0x8007AA7C, 'shamt+1'): 'sra by 31: the sign extension of a 32-bit product, never read',
    ('C62', 0x8007AA94, 'shamt+1'): 'sra by 31: the sign extension of a 32-bit product, never read',
    ('C62', 0x8007ABC4, 'slti<->sltiu'): 'loop counter 0..4',
    ('C62', 0x8007AC30, 'slti<->sltiu'): 'loop counter 0..4',
    ('C62', 0x8007AC28, 'imm+1'): 'delay-slot constant 4 only compared with the loop index, which is < 4 when a corner matched',
}
rows = []
for f in sorted(x for x in glob.glob('../results/mut_words_*.txt') if 'summary' not in x):
    for ln in open(f, encoding='utf-8'):
        m = re.match(r'(?!TOTAL)(\w+)\s+mutants\s+(\d+) killed\s+(\d+) survivors\s+(\d+)', ln)
        if m: rows.append(('F', m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4))))
        m = re.match(r'SURVIVOR (\S+)\s+([0-9A-F]{8}) (\S+(?: \S+)?)\s+(.*?)\s+->\s+(.*)$', ln.rstrip())
        if m: rows.append(('S', m.group(1), int(m.group(2), 16), m.group(3).strip(), m.group(4).strip(), m.group(5).strip()))
tot = sum(r[2] for r in rows if r[0] == 'F'); kil = sum(r[3] for r in rows if r[0] == 'F'); sur = sum(r[4] for r in rows if r[0] == 'F')
print('per function:')
for r in rows:
    if r[0] == 'F': print('  %-13s mutants %4d killed %4d survivors %3d' % r[1:])
print('TOTAL mutants %d killed %d survivors %d' % (tot, kil, sur))
cls = collections.Counter(); unexplained = []
for r in rows:
    if r[0] != 'S': continue
    _, fn, addr, op, ins, mut = r
    reason = None
    if re.match(r'addiu \$sp, \$sp, ', ins): reason = 'stack frame size'
    elif re.search(r'(sw|lw) \$(ra|s\d), 0x[0-9a-f]+\(\$sp\)', ins): reason = 'callee-saved register save/restore slot'
    elif ins.startswith('move ') and op == 'addu<->subu': reason = 'register copy written addu/subu with $zero'
    elif (fn, addr, op) in MANUAL: reason = MANUAL[(fn, addr, op)]
    elif ins.startswith('lw $a0, 0x7164($v0)') : reason = 'argument of the cancel handlers, never read by them'
    if reason: cls[reason] += 1
    else: unexplained.append(r)
print('survivors by reason:')
for k, v in cls.most_common(): print('  %3d  %s' % (v, k))
print('UNEXPLAINED survivors: %d' % len(unexplained))
for r in unexplained: print('   ', r[1:])
