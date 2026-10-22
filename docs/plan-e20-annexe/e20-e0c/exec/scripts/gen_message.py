"""gen_message.py - E20.e0c T0 (h): the exact text buffer of the pickup message. The binary builds it at 0x8007C5xx with strcpy(buffer, name(item)) then strcat(buffer, ETC 0x45), where name(item)
is ETC string 0x200 + item and ETC 0x45 is the string "gagne!" of the resource ETC_RES.R. This reads the RAW strings (data-extracted/data/ETC_RES.R.json through EtcIndexTable.csv: the offset of each
index) and compares them with the text the DLL has (alundra-project/Dialogues/Etc.dialogue, line_texts, after the Yarn compiler). Writes ../vectors/pickup-message.json.
usage: python -B gen_message.py <repo root>"""
import json
import sys

root = sys.argv[1]
table = {}
for line in open(root + '/alundra-datas-analyser/AlundraTools/AlundraTools/EtcIndexTable.csv', encoding='utf-8-sig').read().splitlines()[1:]:
    c = line.split(';')
    if len(c) >= 2:
        table[int(c[0])] = int(c[1])
raw = json.load(open(root + '/data-extracted/data/ETC_RES.R.json', encoding='utf-8'))
dll = json.load(open(root + '/alundra-project/Dialogues/Etc.dialogue', encoding='utf-8'))['line_texts']


def raw_text(i):
    return raw.get(str(table[i]))


def dll_text(i):
    return dll.get('line:Etc_%04d_p0' % i)


items = []
differ = []
for item in range(0x62):
    r = raw_text(0x200 + item)
    d = dll_text(0x200 + item)
    items.append(dict(item=item, raw=r, dll=d, trailingSpaceInRaw=(r is not None and r != r.rstrip(' ')), leadingSpaceInRaw=(r is not None and r != r.lstrip(' '))))
    if r != d:
        differ.append((item, r, d))
gained_raw = raw_text(0x45)
gained_dll = dll_text(0x45)
doc = dict(
    source='E20.e0c T0 (h): ETC_RES.R strings (raw, data-extracted/data/ETC_RES.R.json via EtcIndexTable.csv) against the compiled Etc.dialogue (alundra-project/Dialogues/Etc.dialogue)',
    etc45Raw=gained_raw, etc45Dll=gained_dll,
    expectedBuffer='name(item) + ETC 0x45 (raw)',
    note='ETC 0x45 is " gagné!" in the resource: a LEADING space. The Yarn compiler drops it (Etc_0069 is "gagné!"), so the DLL must put the space back between the name and the string.',
    items=items)
json.dump(doc, open('pickup-message.json', 'w'), indent=1, ensure_ascii=True)
print('ETC 0x45 raw', repr(gained_raw), 'dll', repr(gained_dll))
print('items with a trailing space in the raw name:', [i['item'] for i in items if i['trailingSpaceInRaw']])
print('items with a leading space in the raw name:', [i['item'] for i in items if i['leadingSpaceInRaw']])
print('names that differ raw / dll (count %d):' % len(differ), differ[:12])
