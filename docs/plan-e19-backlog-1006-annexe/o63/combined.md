| transition | track | Debug: load ms (min-max) | Debug: max Tick ms (median / max) | Debug: max 2-frame fill gap ms | Release: load ms (min-max) | Release: max Tick ms (median / max) | Release: max 2-frame fill gap ms | Ticks > 100 ms per sample |
|---|---|---|---|---|---|---|---|---|
| 389 -> 390 | 25 | 131-148 | 179 / 187 | 220 | 93-128 | 148 / 169 | 201 | 1 |
| 390 -> 389 | 25 | 117-123 | 162 / 167 | 198 | 95-100 | 139 / 143 | 174 | 1 |
| 2 -> 6 | 7 | 150-153 | 191 / 193 | 226 | 106-113 | 150 / 151 | 183 | 1 |
| 6 -> 7 | 7 | 181-188 | 223 / 227 | 258 | 137-144 | 176 / 183 | 214 | 1 |
| 7 -> 6 | 7 | 145-156 | 189 / 195 | 227 | 104-115 | 146 / 155 | 187 | 1 |
| 6 -> 2 | 7 | 166-172 | 213 / 223 | 256 | 120-123 | 166 / 175 | 208 | 1 |
| 162 -> 164 | 16 | 169-175 | 206 / 212 | 243 | 136-139 | 171 / 176 | 209 | 1 |
| 164 -> 162 | 16 | 217-225 | 262 / 264 | 295 | 167-219 | 232 / 257 | 288 | 1 |
| 162 -> 168 | 16 | 113-117 | 150 / 157 | 189 | 88-95 | 125 / 130 | 160 | 1 |
| 168 -> 162 | 16 | 204-263 | 255 / 301 | 352 | 160-164 | 200 / 201 | 232 | 1 |

Debug: 40 samples; worst load 263.2 ms; worst Tick 301.1 ms; worst fill gap 320.2 ms; worst 2-frame fill gap 351.5 ms; median of per-sample max Tick 199.4 ms; samples with max Tick > 278.6 ms: 1; > 185.8 ms: 28; > 95.9 ms: 40
  music state (index + voice handle) identical before/after: 40/40
  setup (track change) max Tick: 389->2 395, 2->162 387, 162->389 180, 389->2 214, 2->162 270, 162->389 181, 389->2 393, 2->162 379, 162->389 177, 389->2 210, 2->162 269, 162->389 175

Release: 40 samples; worst load 218.9 ms; worst Tick 257.3 ms; worst fill gap 256.7 ms; worst 2-frame fill gap 288.0 ms; median of per-sample max Tick 162.8 ms; samples with max Tick > 278.6 ms: 0; > 185.8 ms: 8; > 95.9 ms: 40
  music state (index + voice handle) identical before/after: 40/40
  setup (track change) max Tick: 389->2 229, 2->162 240, 162->389 186, 389->2 168, 2->162 216, 162->389 155, 389->2 226, 2->162 233, 162->389 183, 389->2 166, 2->162 210, 162->389 150
