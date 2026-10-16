"""Runs the whole enemies2 pipeline in order. Read-only on the repository (reads the EXE, data-extracted, alundra-project, the DLL and decompilation sources;
writes only in this folder). Usage: python -B run_all.py   (set PYTHONIOENCODING=utf-8 on Windows)."""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
STEPS = ["extract_maps.py", "exe_handlers.py", "census.py", "scan41.py", "build_outputs.py", "slices.py", "report_gen.py"]

for step in STEPS:
    print("==", step, flush=True)
    r = subprocess.run([sys.executable, "-B", os.path.join(HERE, step)], cwd=HERE, env={**os.environ, "PYTHONIOENCODING": "utf-8"})
    if r.returncode != 0:
        sys.exit(f"{step} failed with {r.returncode}")
print("done")
