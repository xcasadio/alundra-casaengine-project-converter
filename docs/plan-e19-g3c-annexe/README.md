# E19.g G2e / G3c annex (effect quads: engine service, DLL push, pixel proof)

Plan: `docs/plan-e19-opcodes.md`, sections "E19.g G2e" and "E19.g G3c".

## `discovery/` (read-only discovery of 2026-10-06, night, and its counter-check)

- `discovery/notes.md`: engine and DLL facts at HEAD (parent `bbea80a`, engine `dd1f8afa`), binary rules with their proof, reference
  images, hazards.
- `discovery/verify/verify.md`: the fresh counter-check. **Where it disagrees with `notes.md`, the counter-check wins:**
  - **R1 (decision-carrying)**: the aura reference images of `notes.md` section 4 were rendered with `g2b1_predict.tie_sign`, which breaks
    exact coverage ties with a perturbation of the sample point. That is not the top-left rule of the plan and of the engine ADR-0068,
    nor the hardware walker's rule. The aura references (and the G2e demo quad "with the aura's real structure") must be regenerated
    with the **top-left rule**; `tie_sign` must not be reused. The 163 and 391 digests of `notes.md` stand. The top-left digests of
    the aura are listed in `verify.md` section 2.
  - **R2**: the three real-data scenes use ABR1 and opaque quads only (no ABR0, no ABR2).
  - **R3**: only 87 of the 158 effect documents carry a `SheetTextureAssetId` (86 maps and the global one); 71 are null by design.
  - **M2**: none of the five real-data scenes contains a mirrored quad or a window origin below 0; replacement scenes with mirrored
    quads (476 effect 0 animation 3 frame 2, 327 effect 0 animation 3 frame 3, 378 or 7 effect 1) are proposed in `verify.md` section 3
    (digests from the counter-check's model, not checked against the DLL).
  - **M4**: the G2e component needs a public `ResolveTextures(Func<Guid, Texture2D>)` seam, as `CellularLayerComponent` has.
- The scripts are copies of the discovery's working files. They read the game binary, `DATAS.BIN` and the export through absolute
  paths of the session scratchpad and of the author's machine (`fxm.py`, `emit_fuzz.py`): adapt the paths before running them.
  `g2b1_predict.py` and `redux.py` live in `docs/plan-e19-g2b-annexe/`.
