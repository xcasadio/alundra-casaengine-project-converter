"""Rig-free helpers for the f4b design scripts: names from names_widths.json (the annex's names.py needs the binary rig), the scenario runner of
annex scenarios.py with a pluggable box class and an optional image height per entity."""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import model as M
import f4_model as F
import f4_model_h as FH


def load_names():
    d = json.load(open(os.path.join(HERE, '..', 'names_widths.json')))
    return {int(k, 16): v['name'].encode('cp1252') for k, v in d.items()}


def load_widths():
    d = json.load(open(os.path.join(HERE, '..', 'names_widths.json')))
    return {int(k, 16): v['w'] for k, v in d.items()}


NAMES = load_names()


def entity(id, portrait, x=200, y=150, z=0, h=56):
    return dict(id=id, flags=F.FLAG_HAS_PORTRAIT if portrait else 0, x=x, y=y, z=z, h=h)


def run(dialogues, frames=130, cam=(40, 20), pad=M.pad_every_frame, n0=3, box_cls=F.F4Box):
    """Same loop as the annex scenarios.run(): per frame f: the pass of the box (text box, then name, then portrait), THEN the script phase
    (the opening attempt of the current dialogue from frame n0 on, retried every frame until it returns true)."""
    box = box_cls(NAMES)
    box.cam = cam
    rows = []
    i = 0
    for f in range(0, frames):
        box.frame = f
        box.sfx, box.flags_set, box.notes = [], [], []
        held, pressed = pad(f, box)
        box.render(held, pressed)
        nd = box.nb.pass_()
        pd = box.pt.pass_()
        ev = []
        if i < len(dialogues) and f >= n0:
            kind, ent, text, matched, nid = dialogues[i]
            ok = box.op_dialog(kind, ent, matched, text, 1, nid)
            ev.append('opcode %s -> %s' % (hex(kind), 'opened' if ok else 'retry (0)'))
            if ok:
                i += 1
        ev += list(box.notes)
        rows.append(dict(frame=f, y=box.y, phase=box.phase(), nb=nd, pt=pd, nb_flags=box.nb.flags, pt_state=box.pt.state, events=ev,
                         sfx=list(box.sfx)))
    return rows
