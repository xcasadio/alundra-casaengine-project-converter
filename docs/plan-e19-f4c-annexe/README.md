# E19.f4c annex: view of the speaker name box and of the dialogue portrait

Versioned inputs and outputs of the discovery (`notes*.md`, `verify.md`, `proposal-TextBoxScreen.xaml`) and of the prediction of E19.f4c2 (F4C2-0).

## Regenerating the prediction (F4C2-0)

`python gen_cases.py` (numpy, Pillow, the exported `alundra-project/`; every path is relative to the file). Inputs: the value model of the f4 annex
(`../plan-e19-f4-annexe/model`, validated on the binary), the drawn-state model of the f2b1 annex (`../plan-e19-f2b1-annexe/scripts`), `names_widths.json` and
`portraits_table.tsv` of the f4 annex, the exported PNGs. `f4c_ref.py` needs the glyph table: with the discovery's `lib` (the executable reader, kept outside the
repository) importable it is checked against ALUN_CD.EXE (France); without it a stand-in built from `../plan-e19-f2b0-annexe/glyph_table.txt` is installed (that check was
done once by f2b0). Outputs:

| file | content |
|---|---|
| `states.json` | the view-model state written for each pinned image (name box, portrait with `brightness` = Rgb / 128, the text part) |
| `classes.tsv` | per pinned image: scenario, frame, N+rel, scale k, view offset, Rgb, comparison class, portrait quad, reference file |
| `refs/*.png` | the reference images, `(320 k + 2 ox) x (240 k + 2 oy)` |
| `names-digests.tsv` | SHA-1 of the RGB bytes of the 320 x 240 image of the name box alone: 60 names x x = 64, 303, 150 |

## Comparison classes (D-E19-100, D-E19-101)

- **exact**: Rgb = 128 (the portrait at rest; the texel as it is) or no portrait. Every pixel equals the reference.
- **tint8**: Rgb != 128 (every pass of the opening and of the return, the rest pass of the close trigger T included: Rgb 127). The reference samples the portrait at the
  centre of the screen pixels at the screen resolution (`floor((i + 0.5) * src / dst)`), then modulates it like the PS1 (`psx_modulate`: 5-bit texel x Rgb / 128, saturated).
  Every pixel outside the quad of the portrait equals the reference; inside the quad each channel is within 8/255, the rows and columns on a tie of the centre rule excluded.
  The PS1 own texel rule (`floor(i * src / dst)`) is not a reference: it differs on 25 to 72 % of the texels of a flight (`samplerule.py`), accepted by D-E19-101.

`compare_gpu.py <folder>` applies the same classes to a folder of GPU captures; `tint.py` and `samplerule.py` measure the modulation and the sampling rules over the 25
portraits; `namecells.py` (needs the executable reader) checks the name frame against the binary's cells.

Order points of S1 N+16 (`refs/S1_N16_x1.png`): (19, 168) portrait (72, 48, 32) over the text frame, (17, 170) (16, 8, 8), (20, 171) (144, 120, 72); (71, 171) and (161, 171)
name frame (72, 64, 56) over the text frame; (64, 140) the clear colour (transparent baked cell), (66, 142) (152, 160, 128); first ink texel of "Jess" (110, 149) (41, 49, 16).
