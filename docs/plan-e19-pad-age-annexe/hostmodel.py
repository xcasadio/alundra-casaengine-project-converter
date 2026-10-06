"""Host-order model for D-E19-93 (the save screen's tick after the dialogue pass).

Not a model of the picker: only of the timing of the OUI/NON question between the save screen (opener, poll) and the dialogue director's
choice pass, in the two orders of one logic tick:
  A (today)  : screen tick, then the dialogue pass      (AlundraWorldProxy.Update loop 1 then loop 2; helper Tick of the screen tests)
  B (D-E19-93): dialogue pass, then the screen tick     (the binary's slot 3 then slot 10)

The choice machine and the answer plan are line-for-line ports of Alundra/Scripts/AlundraChoiceBox.cs (itself equal to
docs/plan-e19-f3-annexe/model/choice_model.py on 607 cases, E19.f3a verification). The pad of the machine is choice_model.PadWords
(the binary's 0x8002E250, equal to AlundraTickPad on 1512 cases, f3 census).
"""
import sys

sys.path.insert(0, r"D:\development\repo\alundra-casaengine-project-converter\docs\plan-e19-f3-annexe\model")
from choice_model import PadWords  # noqa: E402  (read-only import of the annex model)

CROSS, RIGHT, LEFT = 0x40, 0x2000, 0x8000
FRAME_X, SLIDE_FROM_X, SLIDE_STEPS, SLIDE_SETTLE = 176, 320, 15, 2


class Machine:
    """AlundraChoiceBox.cs, stage logic only (no drawing: the draw only moves the cursor counter, irrelevant to the timing)."""

    def __init__(self):
        self.stage = 'Closed'
        self.selection = 0
        self.result_at_press = 0
        self.result_word = 0
        self.closed_this_pass = False
        self.sounds = []

    def open(self):
        self.stage = 'Init'
        self.selection = 0
        self.result_word = 0
        self.result_at_press = 0
        self.closed_this_pass = False
        self.sounds = []
        return 4

    @property
    def accepts_input(self):
        return self.stage == 'Active'

    def _update_slide(self):
        if self.settle == 0:
            return True
        if self.step != SLIDE_STEPS:
            self.step += 1
        else:
            self.settle -= 1
        return False

    def passe(self, pressed, interval):
        self.sounds = []
        self.closed_this_pass = False
        st = self.stage
        if st == 'Closed':
            return
        if st == 'Init':
            self.step, self.settle = 0, SLIDE_SETTLE
            self._update_slide()
            self.stage = 'SlideIn'
            return
        if st == 'SlideIn':
            if self._update_slide():
                self.stage = 'Active'
            return
        if st == 'Active':
            if pressed & CROSS:
                self.result_at_press = self.selection + 1
                self.step, self.settle = 0, SLIDE_SETTLE
                self.sounds += [5, 2 if self.result_at_press == 1 else 3]
                self.stage = 'SlideOut'
            if interval & LEFT:
                if self.selection == 1:
                    self.sounds.append(1)
                self.selection = 0
            if interval & RIGHT:
                if self.selection == 0:
                    self.sounds.append(1)
                self.selection = 1
            return
        if st == 'SlideOut':
            if self._update_slide():
                self.stage = 'Closed'
                self.result_word = self.result_at_press
                self.closed_this_pass = True


class Plan:
    """AlundraChoiceAnswerPlan: one press per pass from the first interactive pass that follows the arming."""

    def __init__(self, index):
        self.index = index
        self.pad = PadWords()
        self.cross_sent = False

    def held(self, accepts_input, selection):
        if not accepts_input or self.cross_sent:
            return 0
        if selection != self.index:
            return RIGHT if self.index > selection else LEFT
        self.cross_sent = True
        return CROSS


class Host:
    """One logic tick = the two halves in `order`. The screen is reduced to its question: it opens it at the tick of the Cross
    (RunPickerInput -> AskQuestion -> OpenChoice) and polls at each later tick (PollAnswer -> TakeChoiceResult), arming the closing slide at
    the tick it takes the answer; the picker ends 18 ticks after that tick (the message box's 18th call, AdvanceSlide)."""

    def __init__(self, order):
        assert order in ('A', 'B')
        self.order = order
        self.m = Machine()
        self.plan = None
        self.tick = -1                 # index of the last tick run; the first tick() is tick 0
        self.opener_tick = None
        self.awaiting = False
        self.consumed_tick = None
        self.picker_end_tick = None
        self.sounds = []               # (tick, sfx)
        self.cross_now = False

    # ---- the hook
    def select(self, index):
        if not self.awaiting:
            return False
        if self.m.stage != 'Closed' and self.plan is None:
            self.plan = Plan(index)
        return True

    # ---- halves
    def _screen(self):
        if self.cross_now and self.opener_tick is None:
            self.sounds.append((self.tick, self.m.open()))
            self.opener_tick = self.tick
            self.awaiting = True
            return
        if self.awaiting and self.opener_tick is not None and self.consumed_tick is None:
            if self.m.result_word != 0:
                self.m.result_word = 0          # TakeChoiceResult clears the word
                self.awaiting = False
                self.consumed_tick = self.tick
                self.picker_end_tick = self.tick + 18

    def _pass(self):
        pressed = interval = 0
        if self.plan is not None:
            word = self.plan.held(self.m.accepts_input, self.m.selection)
            self.plan.pad.update(word)
            pressed, interval = self.plan.pad.pressed, self.plan.pad.interval
        self.m.passe(pressed, interval)
        for s in self.m.sounds:
            self.sounds.append((self.tick, s))
        if self.m.closed_this_pass:
            self.plan = None

    def run_tick(self, cross=False):
        self.tick += 1
        self.cross_now = cross
        if self.order == 'A':
            self._screen()
            self._pass()
        else:
            self._pass()
            self._screen()
        self.cross_now = False

    def run(self, n):
        for _ in range(n):
            self.run_tick()

    def run_until(self, pred, limit=150):
        taken = 0
        while taken < limit and pred():
            self.run_tick()
            taken += 1
        return taken


def scenarios(order):
    out = {}

    # AnswerAndClose(choice): Press(Cross) = tick 0; Select(choice); Tick(n) ... the picker ends at consumed + 18 (ticks counted from tick 0)
    for choice, label in ((0, 'OUI'), (1, 'NON')):
        h = Host(order)
        h.run_tick(cross=True)
        assert h.select(choice)
        h.run(70)
        out['AnswerAndClose_' + label] = {'consumed_tick': h.consumed_tick, 'picker_end_tick': h.picker_end_tick}

    # Carousel_Closing: Press(Cross); Select(0); loop while the question is pending -> taken
    h = Host(order)
    h.run_tick(cross=True)
    h.select(0)
    out['Carousel_Closing_taken'] = h.run_until(lambda: h.consumed_tick is None)

    # Cross_AsksOuiNon: Press(Cross); Press(Down); Select(1); loop while awaiting -> taken
    h = Host(order)
    h.run_tick(cross=True)
    h.run_tick()
    h.select(1)
    out['Cross_AsksOuiNon_taken'] = h.run_until(lambda: h.awaiting)

    # DownDuringTheQuestion: Press(Cross); Press(Down); Tick(25); Select(0); loop while pending -> taken
    h = Host(order)
    h.run_tick(cross=True)
    h.run_tick()
    h.run(25)
    h.select(0)
    out['DownDuringTheQuestion_taken'] = h.run_until(lambda: h.consumed_tick is None)

    # Presenter test: Tick(hold Cross); Select(1); loop while awaiting -> taken
    h = Host(order)
    h.run_tick(cross=True)
    h.select(1)
    out['PresenterQuestion_taken'] = h.run_until(lambda: h.awaiting)

    # the arming grid: arm after tick a (a = 0..40), OUI: ticks after the arming until the screen takes the answer
    grid = {}
    for choice in (0, 1):
        for a in range(0, 41):
            h = Host(order)
            h.run_tick(cross=True)
            h.run(a)
            h.select(choice)
            taken = h.run_until(lambda: h.consumed_tick is None, limit=200)
            grid[(choice, a)] = taken
    out['grid'] = grid
    return out


if __name__ == '__main__':
    A, B = scenarios('A'), scenarios('B')
    for k in A:
        if k == 'grid':
            continue
        print('%-32s A=%-34s B=%s' % (k, A[k], B[k]))
    print()
    print('arming grid (ticks after the arming until the screen takes the answer): a = arming after tick a (tick 0 = the Cross)')
    for choice, label in ((0, 'OUI'), (1, 'NON')):
        print(label)
        print(' a :', ' '.join('%2d' % a for a in range(0, 41)))
        print(' A :', ' '.join('%2d' % A['grid'][(choice, a)] for a in range(0, 41)))
        print(' B :', ' '.join('%2d' % B['grid'][(choice, a)] for a in range(0, 41)))
