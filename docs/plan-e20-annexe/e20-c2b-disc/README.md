# E20.c2b: the hurt hero read in the binary, in the DLL and in the plans (2026-10-25)

Provenance: read-only discovery of 2026-10-25 (HEAD `e9e8c58a` for the three reading agents, `ffc505b1` for the critic; the production code is the same
between the two), paid into the repository by T0 of E20.c2b (branch `chantier/theme2-attaques-objets`). Binary `ALUN_CD.EXE` (France), read with
capstone and run in the E20.c2 hurt-hero oracle. Frozen evidence: paths, addresses and line numbers refer to that state; the scripts still carry the
absolute paths of the session's scratchpad.

- `binary/`: the binary angle (`report-main.md`, facts F1 to F42; scripts `s1` to `s12`, `bx.py`, `mx.py`, outputs `.out`, `dis_core.txt`).
- `dll/`: the DLL angle (`report-main.md`, D1 to D33 and T1 to T16; the disassemblies of the entry, the hurt block and the HUD routine, `head/` = the
  sources of `Alundra/` read at that HEAD for the line numbers, `hit-traces-summary.*`).
- `plan/`: the plans and dependencies angle (`report-main.md`; the disassemblies of the entry, the hurt block, the counters pass, the blink lists pass and the tick dispatcher).
- `critic/`: the fresh critic (`report-main.md`, verdicts A1 to A17, B1 to B11, corrections C1 to C6; scripts, outputs, `pm_head.cs`, `wp_head.cs`). Binding over the
  three reports where they differ (notably the knock-back total, -2 168 832 raw).

Left out on purpose: the copy of the E20.c2 oracle (`binary/oracle/`, equal to `../e20-c2/oracle/`) and every `__pycache__`. `SHA1SUMS.txt` lists the files.
