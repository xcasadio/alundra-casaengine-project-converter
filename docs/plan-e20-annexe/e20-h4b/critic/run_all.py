"""run_all.py - runs every critic script (python -B, from this folder) and writes run_summary.txt.  Read-only on the repository and on bin/."""
import subprocess, sys, os, time

HERE = os.path.dirname(os.path.abspath(__file__))
JOBS = [
    ("c0_loaddelay.py", []),
    ("c1_push.py", []),
    ("c1b_physpass.py", []),
    ("c2_loops.py", []),
    ("c3_query.py", ["4000"]),
    ("c4_slots.py", []),
    ("c5_camera.py", []),
    ("c6_fire2.py", []),
    ("c9_replay.py", []),
    ("c9b_vecpower.py", []),
    ("c10_extra_vectors.py", []),
]
out = open(os.path.join(HERE, "run_summary.txt"), "w", encoding="utf-8")
for name, args in JOBS:
    t = time.time()
    p = subprocess.run([sys.executable, "-B", os.path.join(HERE, name)] + args, cwd=HERE, capture_output=True, text=True)
    out.write("##### %s %s (%.0fs, exit %d)\n" % (name, " ".join(args), time.time() - t, p.returncode))
    out.write(p.stdout)
    if p.returncode:
        out.write(p.stderr[-2000:])
    out.write("\n")
    out.flush()
out.close()
print("done")
