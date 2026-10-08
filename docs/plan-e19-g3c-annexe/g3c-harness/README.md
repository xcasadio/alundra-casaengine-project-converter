# G3c-3: the out-of-repository pixel harness (2026-10-08)

- `G3cHarness.csproj.txt`, `Program.cs.txt` (renamed so no build picks them up): a console host of `CasaEngineGame` on the real
  `AlundraGame.json`, referencing the Debug engine assemblies and the deployed `alundra-project/Alundra.dll` by HintPath (no
  ProjectReference). Back-buffer exactly 320k x 240k (`DebugWidth`/`DebugHeight` overridden); an empty `World` with one `Camera2dComponent`
  (no map drawn) that is the active view's camera; `RenderView.ClearColor = (24, 40, 56)`; `Zoom = k` (also set by the virtual
  resolution layout) and `Target = (scrollX + 160, -(scrollY + 120))`, the scroll recomputed by the harness and checked per capture.
  Sequence per scene from `../g3c_expected.json`: bank loader, `AlundraEffectStage`, `AttachService(game.EffectQuadComponent.Service)`,
  `Load(bank)`, the setup calls, `RunFrame(1, [false])` N times, `PushFrame(camera)`, one engine Update and Draw, `GetBackBufferData`
  dumped as int32 w, h, k then RGBA.
- `run1.sh <scene> <k>`: one capture per process; `residuals.py`: the informational residuals (hardware walker at k = 1, D-E19-92 at x3).
- `out/report_*.txt`: per capture the back-buffer size, zoom, recomputed scroll, camera identity, sheet ids and quad count;
  `out/cmp_*.txt`: the output of `../g3c_compare.py` (all 16 exit 0, 0 differing pixel). Dumps and diff images were not versioned.
