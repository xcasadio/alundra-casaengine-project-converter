h4c2-critic keys (earth 2, item 0x2C): produced on the REAL code of ALUN_CD.EXE (France, SHA-1 a705f8dab665645af5dfaf8fb348c7926b5caa58) by COPIES of the e20-h4 harness
(work/oracle = h4c2/h4copy/oracle + h4c2/tools), with the two corrections of the h4c2 discovery (slot index +0 = i for the 64 slots; per-frame generator step _pf) and seed 0x12345678.
Run from work/oracle:  python -B cast_rich.py --pf --hero-x 600 --hero-y 480 --out ../../keys/cast_2c_pf_mid_x600y480.json
                       python -B my_plateau.py --pf --plateau-hb N --plateau-col 7 --hero-x 100 --hero-y 100 --out ...   (plateau of height byte N for the columns >= 7, i.e. x >= 168 px)
                       python -B my_cancel_snd.py T_HIT   (the cancel of h4c2/tools/cancel_pf.py plus the sound journal around the cancel)
cast_2c_pf_mid_x600y480.json/.txt   hero at (600,480): 15 interior rocks, no map-edge rock; birth 18..242, st3 = b+33, ground = b+32, +0xAC = b+79, st4 = b+80 for all 15; 14 kick ticks.
cast_2c_pf_plateau_hb16.json/.txt   plateau 256 px (height byte 16): the rocks born on it are KEPT (terrain 0x1000000 < hero raw Z + 0x1000000 is false); rock id 8 meets the plateau wall in the air
                                    (+0x13C at tick 142 = b+28, status 3 at 143 = b+29, ground 146, status 4 at 190 = b+76); rocks 9, 13, 14 are born on the plateau (ground at b, status 3 at b+1, status 4 at b+48).
cast_2c_pf_plateau_hb17.json/.txt   plateau 272 px (height byte 17): rocks 9, 13, 14 are DESTROYED AT ONCE (status 4 at the birth tick, +0x138 = 17825792, appearance sound 55 played, no sound 45, no kick);
                                    the draws are made all the same (generator and later rocks identical to the dry key); 27 sounds, 12 kick ticks.
cancel_2c_pf_t100_sounds.txt, cancel_2c_pf_t60_sounds.txt   the interruption keys of h4c2 with the sound journal: the hero hurt sound 23 and ONE sound 45 PER CANCELLED ROCK at the cancel tick.
Terrain byte -> +0x138: height byte N gives N * 16 px (16 -> 256 px, 17 -> 272 px).
