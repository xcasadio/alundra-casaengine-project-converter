# Sample pixel points (x1, native pixels; RGBA with A = 255), from the INDEPENDENT reference (ref.py + the binary tables)

Each point is also checked in the GPU render by compare.py (the whole image is: 0 texels differ). "wrong" is what a plausible mistake would give there.

## choice rest OUI (V1 N+19, active pass)

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (176, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (178, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (240, 160) | (144, 136, 112, 255) | frame/cursor colour | choice frame, interior |  |
| (303, 175) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, bottom-right texel |  |
| (194, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 192, y 152, no padding) | (152, 160, 128, 255) with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down) |
| (240, 154) | (41, 49, 16, 255) | ink | first ink texel of "NON" (glyph N, row-major; the label is at x 240, y 152, no padding) | (152, 160, 128, 255) with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down) |
| (197, 138) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, first opaque texel (row-major) at the cursor x 196, y 136 |  |
| (204, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, centre texel |  |

## choice rest NON (V2 N+22, active pass)

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (176, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (178, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (240, 160) | (144, 136, 112, 255) | frame/cursor colour | choice frame, interior |  |
| (303, 175) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, bottom-right texel |  |
| (194, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 192, y 152, no padding) | (152, 160, 128, 255) with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down) |
| (240, 154) | (41, 49, 16, 255) | ink | first ink texel of "NON" (glyph N, row-major; the label is at x 240, y 152, no padding) | (152, 160, 128, 255) with the default 1-pixel padding of a TextBlock (the glyphs one texel right and down) |
| (245, 137) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_201, first opaque texel (row-major) at the cursor x 244, y 136 |  |
| (252, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_201, centre texel |  |

## choice slide-in (V1 N+5, frame x 282, cursor image 0)

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (282, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (284, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (300, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 298, y 152, no padding) |  |
| (303, 137) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_150, first opaque texel (row-major) at the cursor x 302, y 136 |  |
| (310, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_150, centre texel |  |

## choice slide-out (V1 N+25, frame x 224, cursor image 2)

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (224, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (226, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (288, 160) | (144, 136, 112, 255) | frame/cursor colour | choice frame, interior |  |
| (242, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 240, y 152, no padding) |  |
| (288, 154) | (41, 49, 16, 255) | ink | first ink texel of "NON" (glyph N, row-major; the label is at x 288, y 152, no padding) |  |
| (245, 137) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_201, first opaque texel (row-major) at the cursor x 244, y 136 |  |
| (252, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_201, centre texel |  |

## choice over the text box at rest (V1 N+19 over "Bonjour, Alundra !")

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (176, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (178, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (240, 160) | (144, 136, 112, 255) | frame/cursor colour | choice frame, interior |  |
| (303, 175) | (88, 96, 72, 255) | frame/cursor colour | choice frame, bottom-right texel |  |
| (194, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 192, y 152, no padding) |  |
| (240, 154) | (41, 49, 16, 255) | ink | first ink texel of "NON" (glyph N, row-major; the label is at x 240, y 152, no padding) |  |
| (197, 138) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, first opaque texel (row-major) at the cursor x 196, y 136 |  |
| (204, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, centre texel |  |
| (286, 168) | (184, 176, 144, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (88, 96, 72, 255) |
| (242, 171) | (184, 176, 144, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (88, 96, 72, 255) |
| (227, 172) | (144, 136, 112, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (168, 168, 136, 255) |
| (218, 173) | (88, 96, 72, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (144, 136, 112, 255) |
| (296, 175) | (72, 64, 56, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (152, 160, 128, 255) |
| (180, 167) | (184, 176, 144, 255) | frame/cursor colour | the row above the text box frame (y 168): still only the choice frame |  |
| (180, 176) | (152, 160, 128, 255) | frame/cursor colour | the first row under the choice frame: what the text box shows there (the choice frame ends at y 175) |  |

## choice over a full first text row (V1 N+19 over 18 x the control glyph 0x1A, 14 px wide with ink on its first rows: the text passes under the choice frame)

| (x, y) | expected RGBA | class | what it pins | wrong |
|---|---|---|---|---|
| (176, 144) | (100, 149, 237, 255) | bg (transparent texel shows the clear colour) | choice frame, top-left texel (a transparent texel of the baked cells: what is under shows) |  |
| (178, 146) | (152, 160, 128, 255) | frame/cursor colour | choice frame, border texel near the top-left |  |
| (240, 160) | (144, 136, 112, 255) | frame/cursor colour | choice frame, interior |  |
| (303, 175) | (88, 96, 72, 255) | frame/cursor colour | choice frame, bottom-right texel |  |
| (194, 154) | (41, 49, 16, 255) | ink | first ink texel of "OUI" (glyph O, row-major; the label is at x 192, y 152, no padding) |  |
| (240, 154) | (41, 49, 16, 255) | ink | first ink texel of "NON" (glyph N, row-major; the label is at x 240, y 152, no padding) |  |
| (197, 138) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, first opaque texel (row-major) at the cursor x 196, y 136 |  |
| (204, 144) | (192, 192, 192, 255) | frame/cursor colour | cursor wind_173, centre texel |  |
| (286, 168) | (184, 176, 144, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (88, 96, 72, 255) |
| (244, 171) | (184, 176, 144, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (88, 96, 72, 255) |
| (231, 172) | (144, 136, 112, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (152, 160, 128, 255) |
| (225, 173) | (72, 64, 56, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (184, 176, 144, 255) |
| (296, 175) | (72, 64, 56, 255) | frame/cursor colour | order of the windows: the choice frame covers the text box here (text box windows above the choice would give the "wrong" colour) | (152, 160, 128, 255) |
| (180, 167) | (184, 176, 144, 255) | frame/cursor colour | the row above the text box frame (y 168): still only the choice frame |  |
| (180, 176) | (66, 66, 66, 255) | frame/cursor colour | the first row under the choice frame: what the text box shows there (the choice frame ends at y 175) |  |
| (260, 173) | (72, 64, 56, 255) | frame/cursor colour | an ink texel of the first text row (y 173 to 175) that the choice frame covers | (99, 107, 74, 255) |
