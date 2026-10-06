"""D-E19-99 model: the age of the pad word read by the save screen, in the three host orders of AlundraWorldProxy.Update.

Host modes (one rendered frame = `ticks` logic ticks, all with the frame's hold state; the pad pass records the per-tick words first):
  A  : before E19.f3c  - loop 1 = screen of every tick (pad words of ITS OWN tick, lag 0), loop 2 = pass of every tick (pad words of the tick BEFORE)
  B0 : after E19.f3c   - per tick: pass (lag 1) then screen (words of ITS OWN tick, lag 0)
  B1 : the fix         - per tick: pass (lag 1) then screen (words of the tick BEFORE, lag 1, the same tuple as the pass)

The choice machine and the hook (answer plan) are the line-for-line ports of hostmodel.py (= AlundraChoiceBox.cs / AlundraChoiceAnswerPlan); the host pad is
choice_model.PadWords (= the binary's 0x8002E250 = AlundraTickPad). The screen is reduced to its question: it opens the question at the first tick whose
words (lag included) carry Cross in the by-interval word (RunPickerInput -> AskQuestion -> OpenChoice; the poll starts at the next tick), then polls the
result each tick (PollAnswer -> TakeChoiceResult) and arms the closing slide at the tick it takes the answer (the picker ends 18 ticks later).
"""
import sys

sys.path.insert(0, r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-f3-annexe\model")
from choice_model import PadWords  # noqa: E402
import hostmodel as hm  # noqa: E402  (Machine, Plan: ports of the choice box and the hook)

CROSS, RIGHT, LEFT, SQUARE = 0x40, 0x2000, 0x8000, 0x80


class World:
    def __init__(self, mode):
        assert mode in ('A', 'B0', 'B1')
        self.mode = mode
        self.m = hm.Machine()
        self.plan = None
        self.pad = PadWords()
        self.words = []            # per global tick: (pressed, interval) of the host pad
        self.tick = -1             # global index of the last tick run
        self.opener_tick = None
        self.awaiting = False
        self.consumed_tick = None
        self.picker_end_tick = None
        self.sounds = []           # (tick, sfx)
        self.frames_run = 0
        self.answer = None

    # ---- the hook (SelectChoiceForTests): true while the question is pending
    def select(self, index):
        if not self.awaiting:
            return False
        if self.m.stage != 'Closed' and self.plan is None:
            self.plan = hm.Plan(index)
        return True

    @property
    def pending(self):
        return self.awaiting

    def _words_before(self, g):
        return self.words[g - 1] if g >= 1 else (0, 0)

    def _screen(self, g, words):
        pressed, interval = words
        if self.opener_tick is None:
            if interval & CROSS:
                self.sounds.append((g, self.m.open()))
                self.opener_tick = g
                self.awaiting = True
            return
        if self.awaiting and self.consumed_tick is None:
            if self.m.result_word != 0:
                self.answer = self.m.result_word
                self.m.result_word = 0
                self.awaiting = False
                self.consumed_tick = g
                self.picker_end_tick = g + 18

    def _pass(self, g):
        pressed, interval = self._words_before(g)
        if self.plan is not None:
            word = self.plan.held(self.m.accepts_input, self.m.selection)
            self.plan.pad.update(word)
            pressed, interval = self.plan.pad.pressed, self.plan.pad.interval
        self.m.passe(pressed, interval)
        for s in self.m.sounds:
            self.sounds.append((g, s))
        if self.m.closed_this_pass:
            self.plan = None

    def frame(self, ticks=1, hold=0):
        """One rendered frame: the pad pass first (per-tick words), then the tick loops of the mode."""
        first = self.tick + 1
        for _ in range(ticks):
            self.pad.update(hold)
            self.words.append((self.pad.pressed, self.pad.interval))
        ticks_g = list(range(first, first + ticks))
        if self.mode == 'A':
            for g in ticks_g:
                self._screen(g, self.words[g])
            for g in ticks_g:
                self._pass(g)
        else:
            lag = 0 if self.mode == 'B0' else 1
            for g in ticks_g:
                self._pass(g)
                self._screen(g, self.words[g] if lag == 0 else self._words_before(g))
        self.tick = first + ticks - 1
        self.frames_run += 1

    def frames(self, n, ticks=1, hold=0):
        for _ in range(n):
            self.frame(ticks, hold)


def frames_to_answer(mode, ticks_per_frame, arm_after_ticks, choice, new_shape):
    """Scenario of AlundraSaveScreenTickOrderTests.FramesToTheAnswer.
    old shape : Frame(1, Cross); [assert pending]; for (t = 1; t <= arm; t += tpf) Frame(tpf); Select; Frame(tpf) until the question is taken.
    new shape : Frame(1, Cross); Frame(1) [the screen reads the press: assert pending]; same loop (arm counted from the OPENER)."""
    w = World(mode)
    w.frame(1, CROSS)
    if new_shape:
        w.frame(1)
    assert w.pending, (mode, ticks_per_frame, arm_after_ticks)
    t = 1
    while t <= arm_after_ticks:
        w.frame(ticks_per_frame)
        t += ticks_per_frame
    assert w.select(choice)
    frames = 0
    while w.pending and frames < 150:
        w.frame(ticks_per_frame)
        frames += 1
    return frames


GRID = [(1, 0, 0), (1, 1, 1), (1, 17, 0), (1, 18, 0), (1, 26, 0), (1, 26, 1),
        (2, 0, 0), (2, 2, 1), (2, 18, 0), (2, 26, 0), (2, 26, 1), (2, 28, 0)]

if __name__ == '__main__':
    print('ticksPerFrame, armAfterTicks, choice | A (before f3c) | B0 (f3c, today) | B1 (fix, old shape = FAILS pending assert) | B1 new shape')
    for tpf, a, c in GRID:
        row = [frames_to_answer('A', tpf, a, c, False), frames_to_answer('B0', tpf, a, c, False)]
        try:
            old_shape_b1 = frames_to_answer('B1', tpf, a, c, False)
        except AssertionError:
            old_shape_b1 = 'pending false'
        new_b1 = frames_to_answer('B1', tpf, a, c, True)
        print('%d, %2d, %d | %s | %s | %s | %s' % (tpf, a, c, row[0], row[1], old_shape_b1, new_b1))
