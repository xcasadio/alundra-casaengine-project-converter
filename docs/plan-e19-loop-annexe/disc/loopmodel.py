"""Read-only. Loop-point model of every BGM sequence (46 tracks), built on the verified integer timing model
(recipe-1005/ding-verify/v_seq.py, which predicts bgm.json Frames exactly) and extended with the binary's full
CC 99 / CC 98 loop semantics:

  0x8008C1B8  CC dispatcher (jump table 0x8002A83C): ctl 0x62 (98) -> 0x8008C918, ctl 0x63 (99) -> 0x8008CA40.
  0x8008C918  CC 98: if seq+0x27 == 1 and seq+0x10 == 0: seq+0x28 (loop count) = value, seq+0x10 = 1.
  0x8008CA40  CC 99: value 0x14: +0x16 = 0x14, +0x27 = 1, read next delta -> +0x88, +0xC (loop pos) = +0x4 (pos).
                     value 0x1E: +0x16 = 0x1E; count == 0 -> +0x10 = 0, no jump;
                                 0 < count < 0x7F -> count--, delay = next delta, jump only if count != 0
                                 (else +0x10 = 0); count >= 0x7F -> delta read and discarded, delay = 0, jump.
  0x8008D77C  meta 0x2F: TimesPlayed++ ; l_count (+0x46) == 0 -> restart; TimesPlayed < l_count -> restart;
              else stop (flags |= 0x204, keys off 0x80093EF4). PlaySeq(seq, 1, 1) (0x80049B60) sets l_count = 1
              (0x8008F10C), so a BGM that reaches its end-of-track plays ONCE and stops.

For each track it simulates frames (60 Hz driver ticks, 735 output samples per frame at 44100 Hz) for 3 loop
iterations and reports: loop-start event frame (first pass), jump frames J1..J3, per-iteration event-frame jitter,
musical state (tempo, programs, controllers) at loop entry vs at each jump, notes held across the loop start and
across the jump. Writes loops.tsv next to this script.
Usage: python loopmodel.py [idx ...]"""
import sys, os, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "recipe-1005", "ding-verify"))
import v_seq
from v_seq import Seq, seq_bytes, BGMJSON

SPF = 44100 // 60  # 735, Program.cs:255 samplesPerFrame = OutputSampleRate(44100, SpuMixerSoundPlaybackBackend.cs:18) / 60


class M(Seq):
    def __init__(self, data):
        super().__init__(data)
        self.loops = 0       # +0x28
        self.f10 = 0         # +0x10 loop active
        self.f27 = 0         # +0x27 loop start seen
        self.f16 = 0         # +0x16 last NRPN MSB
        self.loop_pos = None
        self.markers = []    # (frame, tick, kind, value)
        self.jumps = []      # frames of backward jumps
        self.ended_frame = None
        self.state = {}      # musical state: key -> value
        self.active = set()  # (ch, key)
        self.log = []        # (frame, pos) of each processed event start
        self.lp_first_frame = None
        self.snap = {}       # name -> (state copy, active copy, T)
        self.jump_pending_snapshot = False
        self.setlog = []     # (frame, key)
        self.offlog = []     # (frame, (ch, key))

    def _set(self, key, val):
        self.state[key] = val
        self.setlog.append((self.frame, key))

    def _snapshot(self, name):
        st = dict(self.state)
        st["T"] = self.T
        self.snap[name] = (st, set(self.active))

    def event(self):
        d = self.d
        start = self.pos
        self.log.append((self.frame, start))
        if self.loop_pos is not None and start == self.loop_pos[0]:
            # the event at the stored loop position is about to run
            if self.lp_first_frame is None:
                self.lp_first_frame = self.frame
                self.lp_first_tick = self.tick
                self._snapshot("entry")
        ins = d[self.pos]
        st = ins & 0xF0 if ins & 0x80 else self.status
        if st == 0xB0:
            p = self.pos + (1 if ins & 0x80 else 0)
            ctl, val = d[p], d[p + 1]
            if ins & 0x80:
                self.ch = ins & 0x0F
                self.status = 0xB0
            ch = self.ch
            self.pos = p + 2
            if ctl == 0x63:
                if val == 0x14:
                    self.f16 = 0x14; self.f27 = 1
                    self.D = self.delta(); self.tick += self.D // 10
                    self.loop_pos = (self.pos, self.tick)
                    self.markers.append((self.frame, self.tick, "99=20", ch))
                    return
                if val == 0x1E:
                    self.f16 = 0x1E
                    self.markers.append((self.frame, self.tick, "99=30 count=%d" % self.loops, ch))
                    if self.loops == 0:
                        self.f10 = 0
                        self.D = self.delta(); self.tick += self.D // 10
                        return
                    if self.loops < 0x7F:
                        self.loops -= 1
                        self.D = self.delta(); self.tick += self.D // 10
                        if self.loops == 0:
                            self.f10 = 0
                        else:
                            self._jump()
                        return
                    self.delta()
                    self.D = 0
                    self._jump()
                    return
                self.f16 = val
                self._set((ch, "nrpn99"), val)
                self.D = self.delta(); self.tick += self.D // 10
                return
            if ctl in (0x62, 0x06):
                # both CC 98 (0x8008C96C-0x8008C994) and CC 6 data entry (0x8008CD08-0x8008CD2C) latch the
                # loop count once, when +0x27 == 1 and +0x10 == 0
                if self.f27 == 1 and self.f10 == 0:
                    self.loops = val; self.f10 = 1
                    self.markers.append((self.frame, self.tick, "%d=%d(loop count)" % (ctl, val), ch))
                else:
                    self._set((ch, "cc%d" % ctl), val)
                self.D = self.delta(); self.tick += self.D // 10
                return
            self._set((ch, "cc%d" % ctl), val)
            self.D = self.delta(); self.tick += self.D // 10
            return
        if st == 0xC0:
            if ins & 0x80:
                self.ch = ins & 0x0F; self.status = 0xC0; self.pos += 1
            self._set((self.ch, "prog"), d[self.pos])
            self.pos += 1
            self.D = self.delta(); self.tick += self.D // 10
            return
        if st == 0xE0:
            if ins & 0x80:
                self.ch = ins & 0x0F; self.status = 0xE0; self.pos += 1
            self._set((self.ch, "bend"), (d[self.pos], d[self.pos + 1]))
            self.pos += 2
            self.D = self.delta(); self.tick += self.D // 10
            return
        if st == 0x90:
            if ins & 0x80:
                self.ch = ins & 0x0F; self.status = 0x90; self.pos += 1
            k, v = d[self.pos] & 0x7F, d[self.pos + 1] & 0x7F
            self.pos += 2
            if v > 0:
                self.active.add((self.ch, k))
                self.notes.append((self.frame, self.tick, self.ch, k, v))
            else:
                self.active.discard((self.ch, k))
                self.offlog.append((self.frame, (self.ch, k)))
            self.D = self.delta(); self.tick += self.D // 10
            return
        # meta / others: defer to the verified model
        before_alive = self.alive
        super().event()
        if before_alive and not self.alive:
            self.ended_frame = self.frame
            self.markers.append((self.frame, self.tick, "end-of-track", -1))

    def _jump(self):
        self.jumps.append(self.frame)
        self._snapshot("jump%d" % len(self.jumps))
        self.pos, self.tick = self.loop_pos


def simulate(n, iterations=3, max_frames=60 * 60 * 30):
    off, data = seq_bytes(n)
    s = M(data)
    f = 0
    for f in range(max_frames):
        s.frame = f
        s.advance()
        if len(s.jumps) >= iterations or not s.alive:
            break
    return off, s


def iteration_offsets(s, start_frame, end_frame):
    """event (pos) -> frame offset from start_frame, for events processed in [start_frame, end_frame)"""
    out = []
    for fr, pos in s.log:
        if start_frame <= fr < end_frame:
            out.append((pos, fr - start_frame))
    return out


def diff_state(a, b):
    keys = set(a) | set(b)
    return sorted(((k, a.get(k), b.get(k)) for k in keys if a.get(k) != b.get(k)), key=str)


def main(idxs):
    bj = {r["SoundIndex"]: r for r in json.load(open(BGMJSON))}
    rows = []
    for n in idxs:
        off, s = simulate(n)
        r = bj.get(n, {})
        row = {"idx": n, "seq_off": "%#x" % off, "res": s.res, "T0": s.T0}
        row["bgm_frames"] = r.get("Frames")
        row["bgm_loop_detected"] = r.get("LoopDetected")
        row["first_note_frame"] = s.notes[0][0] if s.notes else None
        ls_marker = [m for m in s.markers if m[2] == "99=20"]
        cnt = [m for m in s.markers if "(loop count)" in m[2]]
        row["n_99_20_events_seen"] = len(ls_marker)
        row["loop_count"] = cnt[0][2] if cnt else None
        row["marker_99_20_frame"] = ls_marker[0][0] if ls_marker else None
        row["loop_start_tick"] = getattr(s, "lp_first_tick", None)
        row["loop_start_frame"] = s.lp_first_frame
        row["jumps"] = s.jumps
        row["ended_frame"] = s.ended_frame
        if s.jumps and s.lp_first_frame is not None:
            J = s.jumps
            starts = [s.lp_first_frame] + J
            periods = [starts[i + 1] - starts[i] for i in range(len(starts) - 1)]
            row["periods"] = periods
            it = [iteration_offsets(s, starts[i], starts[i + 1]) for i in range(len(starts) - 1)]
            # jitter: compare per-event frame offsets of iteration 1 vs 2 (and 2 vs 3)
            def jit(a, b):
                m = min(len(a), len(b))
                dif = [(a[i][1], b[i][1] - a[i][1]) for i in range(m) if a[i][0] == b[i][0] and a[i][1] != b[i][1]]
                same_seq = all(a[i][0] == b[i][0] for i in range(m)) and len(a) == len(b)
                return same_seq, dif
            j12 = jit(it[0], it[1]) if len(it) > 1 else (None, [])
            j23 = jit(it[1], it[2]) if len(it) > 2 else (None, [])
            row["iter_event_seq_identical_1_2"] = j12[0]
            row["n_events_shifted_1_2"] = len(j12[1])
            row["shifts_1_2"] = sorted(set(x[1] for x in j12[1]))
            row["first_shifted_offset_1_2"] = j12[1][0][0] if j12[1] else None
            row["n_events_shifted_2_3"] = len(j23[1])
            entry = s.snap["entry"]
            j1 = s.snap["jump1"]
            row["state_diff_entry_vs_jump1"] = diff_state(entry[0], j1[0])
            row["held_at_entry"] = sorted(entry[1])
            row["held_at_jump1"] = sorted(j1[1])
            if "jump2" in s.snap:
                row["state_diff_jump1_vs_jump2"] = diff_state(j1[0], s.snap["jump2"][0])
                row["held_at_jump2"] = sorted(s.snap["jump2"][1])
        row["markers"] = s.markers[:8]
        row["tempo_changes"] = [(tk, tv, T, fr) for tk, tv, T, fr in s.tempo_changes][:12]
        rows.append(row)
    return rows


if __name__ == "__main__":
    idxs = [int(a) for a in sys.argv[1:]] or list(range(1, 47))
    rows = main(idxs)
    for r in rows:
        print(json.dumps(r, default=str))
