"""Read-only. Static census of each BGM SEQ, walked linearly to its end-of-track with the binary's own event
decoding (loopmodel.M, built on v_seq.Seq: running status 0xFF persists after a meta event, like
SoundManager.cs:1507-1522 / 0x8008BDD0), with the 0x1E jump disabled so the walk reaches the end:
every controller number used, CC 6/98/99 events that are NOT the loop-count latch, the number of CC99=20 / 30
markers (nesting / multiple loops), tempo metas and where they sit relative to the loop."""
import sys, os
from collections import Counter
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import loopmodel as lm


class S(lm.M):
    def _jump(self):
        self.jumps.append(self.frame)  # record, but keep walking linearly


tot = Counter()
for n in range(1, 47):
    off, data = lm.seq_bytes(n)
    s = S(data)
    k = 0
    while s.alive and s.pos < len(data) and k < 10 ** 6:
        s.event()
        k += 1
    ccs = Counter(key[1] for fr, key in s.setlog if isinstance(key, tuple))
    special = [(key, ) for fr, key in s.setlog if isinstance(key, tuple) and key[1] in ("cc6", "cc98", "nrpn99", "cc100", "cc101")]
    marks = Counter(m[2].split()[0] if not m[2].startswith("9") else m[2].split("(")[0].split()[0] for m in s.markers)
    tot.update(ccs)
    print(n, "markers", dict(marks), "ccs", dict(sorted(ccs.items())), "non-latch NRPN/RPN/data-entry", special[:4],
          "tempo metas", len(s.tempo_changes), "ended", not s.alive)
print("all", dict(sorted(tot.items())))
