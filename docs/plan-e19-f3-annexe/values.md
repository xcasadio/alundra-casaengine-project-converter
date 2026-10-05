# E19.f3 value tables (model/scenarios.py; bare scenarios also executed on the real binary code, see validate.py)

N = tick of the opener (0x44 first entry). t = frame (main-loop iteration) relative to N. "pad N+k" = the raw pad word the box sees in frame N+k.

## V1 validate the default (OUI) at once

pad: N+19=0x40; default selection 0; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+37 0x44 resolved Result=1; N+37 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2]]}
Result of the program: 1

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 0 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 0 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 0 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 0 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 0 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 0 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 0 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 0 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 1 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 1 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 1 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 1 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 1 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 196 | 1 | 0 | 5,2 |
| N+20 | out | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+21 | out | yes | 185 | 201 | 249 | 205 | 2 | 0 |  |
| N+22 | out | yes | 195 | 211 | 259 | 215 | 2 | 0 |  |
| N+23 | out | yes | 204 | 220 | 268 | 224 | 2 | 0 |  |
| N+24 | out | yes | 214 | 230 | 278 | 234 | 2 | 0 |  |
| N+25 | out | yes | 224 | 240 | 288 | 244 | 2 | 0 |  |
| N+26 | out | yes | 233 | 249 | 297 | 253 | 2 | 0 |  |
| N+27 | out | yes | 243 | 259 | 307 | 263 | 2 | 0 |  |
| N+28 | out | yes | 252 | 268 | 316 | 272 | 2 | 0 |  |
| N+29 | out | yes | 262 | 278 | 326 | 282 | 2 | 0 |  |
| N+30 | out | yes | 272 | 288 | 336 | 292 | 2 | 0 |  |
| N+31 | out | yes | 281 | 297 | 345 | 301 | 3 | 0 |  |
| N+32 | out | yes | 291 | 307 | 355 | 311 | 3 | 0 |  |
| N+33 | out | yes | 300 | 316 | 364 | 320 | 3 | 0 |  |
| N+34 | out | yes | 310 | 326 | 374 | 330 | 3 | 0 |  |
| N+35 | out | yes | 320 | 336 | 384 | 340 | 3 | 0 |  |
| N+36 | out | yes | 320 | 336 | 384 | 340 | 3 | 0 |  |
| N+37 | out | closed |  |  |  |  |  |  |  |

## V2 Right at N+19 then Cross at N+22 (NON)

pad: N+19=0x2000, N+22=0x40; default selection 0; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+40 0x44 resolved Result=0; N+40 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 23, "closed": 40, "sounds": [[19, 1], [22, 5], [22, 3]]}
Result of the program: 0

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 0 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 0 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 0 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 0 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 0 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 0 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 0 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 0 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 1 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 1 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 1 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 1 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 1 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 | 1 |
| N+20 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+21 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+22 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 | 5,3 |
| N+23 | out | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+24 | out | yes | 185 | 201 | 249 | 253 | 2 | 1 |  |
| N+25 | out | yes | 195 | 211 | 259 | 263 | 2 | 1 |  |
| N+26 | out | yes | 204 | 220 | 268 | 272 | 2 | 1 |  |
| N+27 | out | yes | 214 | 230 | 278 | 282 | 2 | 1 |  |
| N+28 | out | yes | 224 | 240 | 288 | 292 | 2 | 1 |  |
| N+29 | out | yes | 233 | 249 | 297 | 301 | 2 | 1 |  |
| N+30 | out | yes | 243 | 259 | 307 | 311 | 2 | 1 |  |
| N+31 | out | yes | 252 | 268 | 316 | 320 | 3 | 1 |  |
| N+32 | out | yes | 262 | 278 | 326 | 330 | 3 | 1 |  |
| N+33 | out | yes | 272 | 288 | 336 | 340 | 3 | 1 |  |
| N+34 | out | yes | 281 | 297 | 345 | 349 | 3 | 1 |  |
| N+35 | out | yes | 291 | 307 | 355 | 359 | 3 | 1 |  |
| N+36 | out | yes | 300 | 316 | 364 | 368 | 3 | 1 |  |
| N+37 | out | yes | 310 | 326 | 374 | 378 | 3 | 1 |  |
| N+38 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+39 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+40 | out | closed |  |  |  |  |  |  |  |

## V3 Cross held N+10..N+27 (no edge at N+19), pressed again N+30

pad: 19 frames; default selection 0; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+48 0x44 resolved Result=1; N+48 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 31, "closed": 48, "sounds": [[30, 5], [30, 2]]}
Result of the program: 1

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 0 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 0 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 0 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 0 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 0 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 0 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 0 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 0 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 1 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 1 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 1 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 1 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 1 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+20 | active | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+21 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+22 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+23 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+24 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+25 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+26 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+27 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+28 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+29 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+30 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 | 5,2 |
| N+31 | out | yes | 176 | 192 | 240 | 196 | 3 | 0 |  |
| N+32 | out | yes | 185 | 201 | 249 | 205 | 3 | 0 |  |
| N+33 | out | yes | 195 | 211 | 259 | 215 | 3 | 0 |  |
| N+34 | out | yes | 204 | 220 | 268 | 224 | 3 | 0 |  |
| N+35 | out | yes | 214 | 230 | 278 | 234 | 3 | 0 |  |
| N+36 | out | yes | 224 | 240 | 288 | 244 | 3 | 0 |  |
| N+37 | out | yes | 233 | 249 | 297 | 253 | 3 | 0 |  |
| N+38 | out | yes | 243 | 259 | 307 | 263 | 3 | 0 |  |
| N+39 | out | yes | 252 | 268 | 316 | 272 | 3 | 0 |  |
| N+40 | out | yes | 262 | 278 | 326 | 282 | 3 | 0 |  |
| N+41 | out | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+42 | out | yes | 281 | 297 | 345 | 301 | 0 | 0 |  |
| N+43 | out | yes | 291 | 307 | 355 | 311 | 0 | 0 |  |
| N+44 | out | yes | 300 | 316 | 364 | 320 | 0 | 0 |  |
| N+45 | out | yes | 310 | 326 | 374 | 330 | 0 | 0 |  |
| N+46 | out | yes | 320 | 336 | 384 | 340 | 0 | 0 |  |
| N+47 | out | yes | 320 | 336 | 384 | 340 | 0 | 0 |  |
| N+48 | out | closed |  |  |  |  |  |  |  |

## V4 Right held N+19..N+59, Left at N+60, Cross at N+62

pad: 43 frames; default selection 0; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+80 0x44 resolved Result=1; N+80 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 63, "closed": 80, "sounds": [[19, 1], [60, 1], [62, 5], [62, 2]]}
Result of the program: 1

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 0 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 0 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 0 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 0 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 0 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 0 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 0 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 0 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 1 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 1 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 1 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 1 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 1 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 | 1 |
| N+20 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+21 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+22 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+23 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+24 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+25 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+26 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+27 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+28 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+29 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+30 | active | yes | 176 | 192 | 240 | 244 | 2 | 1 |  |
| N+31 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+32 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+33 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+34 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+35 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+36 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+37 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+38 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+39 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+40 | active | yes | 176 | 192 | 240 | 244 | 3 | 1 |  |
| N+41 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+42 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+43 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+44 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+45 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+46 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+47 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+48 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+49 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+50 | active | yes | 176 | 192 | 240 | 244 | 0 | 1 |  |
| N+51 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+52 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+53 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+54 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+55 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+56 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+57 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+58 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+59 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+60 | active | yes | 176 | 192 | 240 | 196 | 1 | 0 | 1 |
| N+61 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+62 | active | yes | 176 | 192 | 240 | 196 | 2 | 0 | 5,2 |
| N+63 | out | yes | 176 | 192 | 240 | 196 | 2 | 0 |  |
| N+64 | out | yes | 185 | 201 | 249 | 205 | 2 | 0 |  |
| N+65 | out | yes | 195 | 211 | 259 | 215 | 2 | 0 |  |
| N+66 | out | yes | 204 | 220 | 268 | 224 | 2 | 0 |  |
| N+67 | out | yes | 214 | 230 | 278 | 234 | 2 | 0 |  |
| N+68 | out | yes | 224 | 240 | 288 | 244 | 2 | 0 |  |
| N+69 | out | yes | 233 | 249 | 297 | 253 | 2 | 0 |  |
| N+70 | out | yes | 243 | 259 | 307 | 263 | 2 | 0 |  |
| N+71 | out | yes | 252 | 268 | 316 | 272 | 3 | 0 |  |
| N+72 | out | yes | 262 | 278 | 326 | 282 | 3 | 0 |  |
| N+73 | out | yes | 272 | 288 | 336 | 292 | 3 | 0 |  |
| N+74 | out | yes | 281 | 297 | 345 | 301 | 3 | 0 |  |
| N+75 | out | yes | 291 | 307 | 355 | 311 | 3 | 0 |  |
| N+76 | out | yes | 300 | 316 | 364 | 320 | 3 | 0 |  |
| N+77 | out | yes | 310 | 326 | 374 | 330 | 3 | 0 |  |
| N+78 | out | yes | 320 | 336 | 384 | 340 | 3 | 0 |  |
| N+79 | out | yes | 320 | 336 | 384 | 340 | 3 | 0 |  |
| N+80 | out | closed |  |  |  |  |  |  |  |

## V5 Cross+Right together at N+19

pad: N+19=0x2040; default selection 0; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+37 0x44 resolved Result=1; N+37 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2], [19, 1]]}
Result of the program: 1

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 0 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 0 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 0 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 0 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 0 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 0 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 0 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 0 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 1 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 1 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 1 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 1 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 1 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 1 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 | 5,2,1 |
| N+20 | out | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+21 | out | yes | 185 | 201 | 249 | 253 | 2 | 1 |  |
| N+22 | out | yes | 195 | 211 | 259 | 263 | 2 | 1 |  |
| N+23 | out | yes | 204 | 220 | 268 | 272 | 2 | 1 |  |
| N+24 | out | yes | 214 | 230 | 278 | 282 | 2 | 1 |  |
| N+25 | out | yes | 224 | 240 | 288 | 292 | 2 | 1 |  |
| N+26 | out | yes | 233 | 249 | 297 | 301 | 2 | 1 |  |
| N+27 | out | yes | 243 | 259 | 307 | 311 | 2 | 1 |  |
| N+28 | out | yes | 252 | 268 | 316 | 320 | 2 | 1 |  |
| N+29 | out | yes | 262 | 278 | 326 | 330 | 2 | 1 |  |
| N+30 | out | yes | 272 | 288 | 336 | 340 | 2 | 1 |  |
| N+31 | out | yes | 281 | 297 | 345 | 349 | 3 | 1 |  |
| N+32 | out | yes | 291 | 307 | 355 | 359 | 3 | 1 |  |
| N+33 | out | yes | 300 | 316 | 364 | 368 | 3 | 1 |  |
| N+34 | out | yes | 310 | 326 | 374 | 378 | 3 | 1 |  |
| N+35 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+36 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+37 | out | closed |  |  |  |  |  |  |  |

## V6 variant opener, default NON, Cross at N+19

pad: N+19=0x40; default selection 1; anim0 0

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+37 0x44 resolved Result=0; N+37 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 3]]}
Result of the program: 0

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 379 | 0 | 1 |  |
| N+3 | in | yes | 301 | 317 | 365 | 369 | 0 | 1 |  |
| N+4 | in | yes | 292 | 308 | 356 | 360 | 0 | 1 |  |
| N+5 | in | yes | 282 | 298 | 346 | 350 | 0 | 1 |  |
| N+6 | in | yes | 272 | 288 | 336 | 340 | 0 | 1 |  |
| N+7 | in | yes | 263 | 279 | 327 | 331 | 0 | 1 |  |
| N+8 | in | yes | 253 | 269 | 317 | 321 | 0 | 1 |  |
| N+9 | in | yes | 244 | 260 | 308 | 312 | 0 | 1 |  |
| N+10 | in | yes | 234 | 250 | 298 | 302 | 0 | 1 |  |
| N+11 | in | yes | 224 | 240 | 288 | 292 | 1 | 1 |  |
| N+12 | in | yes | 215 | 231 | 279 | 283 | 1 | 1 |  |
| N+13 | in | yes | 205 | 221 | 269 | 273 | 1 | 1 |  |
| N+14 | in | yes | 196 | 212 | 260 | 264 | 1 | 1 |  |
| N+15 | in | yes | 186 | 202 | 250 | 254 | 1 | 1 |  |
| N+16 | in | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+17 | in | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+18 | in | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+19 | active | yes | 176 | 192 | 240 | 244 | 1 | 1 | 5,3 |
| N+20 | out | yes | 176 | 192 | 240 | 244 | 1 | 1 |  |
| N+21 | out | yes | 185 | 201 | 249 | 253 | 2 | 1 |  |
| N+22 | out | yes | 195 | 211 | 259 | 263 | 2 | 1 |  |
| N+23 | out | yes | 204 | 220 | 268 | 272 | 2 | 1 |  |
| N+24 | out | yes | 214 | 230 | 278 | 282 | 2 | 1 |  |
| N+25 | out | yes | 224 | 240 | 288 | 292 | 2 | 1 |  |
| N+26 | out | yes | 233 | 249 | 297 | 301 | 2 | 1 |  |
| N+27 | out | yes | 243 | 259 | 307 | 311 | 2 | 1 |  |
| N+28 | out | yes | 252 | 268 | 316 | 320 | 2 | 1 |  |
| N+29 | out | yes | 262 | 278 | 326 | 330 | 2 | 1 |  |
| N+30 | out | yes | 272 | 288 | 336 | 340 | 2 | 1 |  |
| N+31 | out | yes | 281 | 297 | 345 | 349 | 3 | 1 |  |
| N+32 | out | yes | 291 | 307 | 355 | 359 | 3 | 1 |  |
| N+33 | out | yes | 300 | 316 | 364 | 368 | 3 | 1 |  |
| N+34 | out | yes | 310 | 326 | 374 | 378 | 3 | 1 |  |
| N+35 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+36 | out | yes | 320 | 336 | 384 | 388 | 3 | 1 |  |
| N+37 | out | closed |  |  |  |  |  |  |  |

## V7 anim0 = 17 (counter left by an earlier box), Cross at N+19

pad: N+19=0x40; default selection 0; anim0 17

events: N+0 0x44 first entry; N+0 sfx 4 (open); N+37 0x44 resolved Result=1; N+37 continues
summary: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2]]}
Result of the program: 1

| t | update | drawn | frame x | label0 x | label1 x | cursor x | cursor img | sel | sounds |
|---|---|---|---|---|---|---|---|---|---|
| N+0 | (opener runs in the script phase) | no |  |  |  |  |  |  | 4 |
| N+1 | init | no |  |  |  |  |  |  |  |
| N+2 | in | yes | 311 | 327 | 375 | 331 | 1 | 0 |  |
| N+3 | in | yes | 301 | 317 | 365 | 321 | 1 | 0 |  |
| N+4 | in | yes | 292 | 308 | 356 | 312 | 2 | 0 |  |
| N+5 | in | yes | 282 | 298 | 346 | 302 | 2 | 0 |  |
| N+6 | in | yes | 272 | 288 | 336 | 292 | 2 | 0 |  |
| N+7 | in | yes | 263 | 279 | 327 | 283 | 2 | 0 |  |
| N+8 | in | yes | 253 | 269 | 317 | 273 | 2 | 0 |  |
| N+9 | in | yes | 244 | 260 | 308 | 264 | 2 | 0 |  |
| N+10 | in | yes | 234 | 250 | 298 | 254 | 2 | 0 |  |
| N+11 | in | yes | 224 | 240 | 288 | 244 | 2 | 0 |  |
| N+12 | in | yes | 215 | 231 | 279 | 235 | 2 | 0 |  |
| N+13 | in | yes | 205 | 221 | 269 | 225 | 2 | 0 |  |
| N+14 | in | yes | 196 | 212 | 260 | 216 | 3 | 0 |  |
| N+15 | in | yes | 186 | 202 | 250 | 206 | 3 | 0 |  |
| N+16 | in | yes | 176 | 192 | 240 | 196 | 3 | 0 |  |
| N+17 | in | yes | 176 | 192 | 240 | 196 | 3 | 0 |  |
| N+18 | in | yes | 176 | 192 | 240 | 196 | 3 | 0 |  |
| N+19 | active | yes | 176 | 192 | 240 | 196 | 3 | 0 | 5,2 |
| N+20 | out | yes | 176 | 192 | 240 | 196 | 3 | 0 |  |
| N+21 | out | yes | 185 | 201 | 249 | 205 | 3 | 0 |  |
| N+22 | out | yes | 195 | 211 | 259 | 215 | 3 | 0 |  |
| N+23 | out | yes | 204 | 220 | 268 | 224 | 3 | 0 |  |
| N+24 | out | yes | 214 | 230 | 278 | 234 | 0 | 0 |  |
| N+25 | out | yes | 224 | 240 | 288 | 244 | 0 | 0 |  |
| N+26 | out | yes | 233 | 249 | 297 | 253 | 0 | 0 |  |
| N+27 | out | yes | 243 | 259 | 307 | 263 | 0 | 0 |  |
| N+28 | out | yes | 252 | 268 | 316 | 272 | 0 | 0 |  |
| N+29 | out | yes | 262 | 278 | 326 | 282 | 0 | 0 |  |
| N+30 | out | yes | 272 | 288 | 336 | 292 | 0 | 0 |  |
| N+31 | out | yes | 281 | 297 | 345 | 301 | 0 | 0 |  |
| N+32 | out | yes | 291 | 307 | 355 | 311 | 0 | 0 |  |
| N+33 | out | yes | 300 | 316 | 364 | 320 | 0 | 0 |  |
| N+34 | out | yes | 310 | 326 | 374 | 330 | 1 | 0 |  |
| N+35 | out | yes | 320 | 336 | 384 | 340 | 1 | 0 |  |
| N+36 | out | yes | 320 | 336 | 384 | 340 | 1 | 0 |  |
| N+37 | out | closed |  |  |  |  |  |  |  |

## V8 sailor 12 (map 389) with the real choice box

text pad A: choice opens at N+94 (T999 step), Cross first seen at N+113
  events: 0 open S001; 0 0x50 4; 94 0x36 T999; 94 0x44 first entry; 94 sfx 4 (open); 131 0x44 resolved Result=1; 131 0x51; 150 open S002; 333 0x39 released
  text box: {"open": 0, "first_glyph": 19, "last_glyph": 93, "typing_done": 95, "close_trigger": 132, "dialog_closed": 150, "release_0x39": null, "glyphs": 73, "blips": 37}
  choice: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2]]}

text pad B: choice opens at N+319 (T999 step), Cross first seen at N+338
  events: 0 open S001; 0 0x50 4; 319 0x36 T999; 319 0x44 first entry; 319 sfx 4 (open); 356 0x44 resolved Result=1; 356 0x51; 375 open S002
  text box: {"open": 0, "first_glyph": 19, "last_glyph": 315, "typing_done": 323, "close_trigger": 357, "dialog_closed": 375, "release_0x39": null, "glyphs": 73, "blips": 37}
  choice: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2]]}

## V9 save book: choice opens over a box still typing

events: 0 open ETC0x40; 0 0x50 4; 61 0x44 first entry; 61 sfx 4 (open); 98 0x44 resolved Result=1; 98 0x51; 134 0x39 released
text: {"open": 0, "first_glyph": 19, "last_glyph": 111, "typing_done": 115, "close_trigger": 116, "dialog_closed": 134, "release_0x39": 134, "glyphs": 24, "blips": 0}
choice: {"init": 1, "first_draw": 2, "interactive": 19, "first_out": 20, "closed": 37, "sounds": [[19, 5], [19, 2]]}
text rows when the choice opens: ['Enregistrer', '', ''] (glyphs 11), at the Cross: ['Enregistrer tes ', '', ''], at the result: ['Enregistrer tes prog', '', '']
