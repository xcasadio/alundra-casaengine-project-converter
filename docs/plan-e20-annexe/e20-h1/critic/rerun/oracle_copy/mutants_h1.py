"""mutants_h1.py - E20.h1 discovery: mutation testing of MY model (model_h1.py) against the vectors of ./vec.  Each mutant is a textual change of the model's source (one change, at the
n-th occurrence of an anchor).  A mutant is KILLED when at least one vector shows a difference with the real code.  Two modes:
  strict  : an unlisted byte read, a different write set, return value or stub journal all count (the replay convention of the vector files)
  lenient : an unlisted byte reads as 0 and does not count; only a different value (write, return, journal) counts  -> a mutant killed only in strict mode changes the READ SET, not the values
usage: python -B mutants_h1.py [vecdir]"""
import glob
import json
import os
import sys
import types

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import check_model as CM

SRC = open(os.path.join(HERE, 'model_h1.py'), encoding='utf-8').read()

NL = "\n"
GAP = NL + NL + NL

# (name, anchor, replacement, occurrence (1-based) of the anchor to change)
MUTANTS = [
    # --- herb heal 0x80032E50
    ('herb: minimum 5 -> 4', "return 5 if q < 5 else q", "return 4 if q < 4 else q", 1),
    ('herb: minimum 5 -> 6', "return 5 if q < 5 else q", "return 6 if q < 6 else q", 1),
    ('herb: quarter -> third', "q = trunc_div(hpmax, 4)", "q = trunc_div(hpmax, 3)", 1),
    ('herb: quarter -> eighth', "q = trunc_div(hpmax, 4)", "q = trunc_div(hpmax, 8)", 1),
    ('herb: no minimum', "return 5 if q < 5 else q", "return q", 1),
    ('herb: effect z force 0x20000 -> 0x40000', "    if e:" + NL + "        c.ww(e + 0x5C, 0x20000)", "    if e:" + NL + "        c.ww(e + 0x5C, 0x40000)", 1),
    ('herb: effect z lift 0x100000 -> 0x80000', "e = create_effect(c, 0, x, y, (z + 0x100000) & M)" + NL + "    if e:", "e = create_effect(c, 0, x, y, (z + 0x80000) & M)" + NL + "    if e:", 1),
    ('herb: z force written without the effect', "    if e:" + NL + "        c.ww(e + 0x5C, 0x20000)", "    if True:" + NL + "        c.ww(e + 0x5C, 0x20000)", 1),
    ('herb: effect animation 0 -> 2', "e = create_effect(c, 0, x, y, (z + 0x100000) & M)" + NL + "    if e:", "e = create_effect(c, 2, x, y, (z + 0x100000) & M)" + NL + "    if e:", 1),
    # --- the shared capped sum
    ('cap: removed', "    if hpmax < new:" + NL + "        new = hpmax" + NL + "    c.ww(hero + 0x14, new)", "    c.ww(hero + 0x14, new)", 1),
    ('cap: compares unsigned', "    if hpmax < new:" + NL + "        new = hpmax", "    if (hpmax & M) < (new & M):" + NL + "        new = hpmax", 1),
    ('cap: the sum is not wrapped to 32 bits', "new = s32(hp + amount_of(hpmax))", "new = hp + amount_of(hpmax)", 1),
    ('cap: writes the life only when it changes', "    c.ww(hero + 0x14, new)" + GAP + "def heal_herb", "    if new != hp:" + NL + "        c.ww(hero + 0x14, new)" + GAP + "def heal_herb", 1),
    # --- water 1 / S potion (heal_half and its sparks)
    ('water1: minimum 5 -> 4', "heal_half(c, hero, 5)", "heal_half(c, hero, 4)", 1),
    ('water1: minimum 5 -> 10', "heal_half(c, hero, 5)", "heal_half(c, hero, 10)", 1),
    ('spotion: minimum 10 -> 9', "heal_half(c, hero, 10)", "heal_half(c, hero, 9)", 1),
    ('spotion: minimum 10 -> 5', "heal_half(c, hero, 10)", "heal_half(c, hero, 5)", 1),
    ('half: half -> quarter', "q = trunc_div(hpmax, 2)", "q = trunc_div(hpmax, 4)", 1),
    ('half: half -> third', "q = trunc_div(hpmax, 2)", "q = trunc_div(hpmax, 3)", 1),
    ('half: three sparks', "    for _ in range(4):" + NL + "        spark(c, 0, 0x100000, 0x20000)", "    for _ in range(3):" + NL + "        spark(c, 0, 0x100000, 0x20000)", 1),
    ('half: five sparks', "    for _ in range(4):" + NL + "        spark(c, 0, 0x100000, 0x20000)", "    for _ in range(5):" + NL + "        spark(c, 0, 0x100000, 0x20000)", 1),
    ('half: spark z force 0x20000 -> 0x40000', "spark(c, 0, 0x100000, 0x20000)", "spark(c, 0, 0x100000, 0x40000)", 1),
    ('half: spark z lift 0x100000 -> 0x80000', "spark(c, 0, 0x100000, 0x20000)", "spark(c, 0, 0x80000, 0x20000)", 1),
    ('half: spark animation 0 -> 2', "spark(c, 0, 0x100000, 0x20000)", "spark(c, 2, 0x100000, 0x20000)", 1),
    # --- the spark
    ('spark: draws although the effect was refused', "    if e == 0:" + NL + "        return" + NL + "    s = c.rw(RNG)", "    s = c.rw(RNG)", 1),
    ('spark: first multiplier 0x20001 -> 0x30001', "hi1 = (r1 * 0x20001) >> 32", "hi1 = (r1 * 0x30001) >> 32", 1),
    ('spark: second multiplier 0x30001 -> 0x20001', "hi2 = (r2 * 0x30001) >> 32", "hi2 = (r2 * 0x20001) >> 32", 1),
    ('spark: x offset 0x10000 -> 0x18000', "c.ww(e + 0x54, (hi1 + 0xFFFF0000) & M)", "c.ww(e + 0x54, (hi1 + 0xFFFE8000) & M)", 1),
    ('spark: y offset 0x18000 -> 0x10000', "c.ww(e + 0x58, (hi2 + 0xFFFE8000) & M)", "c.ww(e + 0x58, (hi2 + 0xFFFF0000) & M)", 1),
    ('spark: x force written to the y slot', "c.ww(e + 0x54, (hi1 + 0xFFFF0000) & M)", "c.ww(e + 0x58, (hi1 + 0xFFFF0000) & M)", 1),
    ('spark: the generator keeps the first state', "    c.ww(RNG, r1)" + NL + "    c.ww(RNG, r2)", "    c.ww(RNG, r1)", 1),
    ('spark: the second draw reuses the first state', "r2 = (r1 * LCG_A + LCG_C) & M", "r2 = (s * LCG_A + LCG_C) & M", 1),
    ('spark: generator constant A', "LCG_A = 0x7D2B89DD", "LCG_A = 0x7D2B89DF", 1),
    ('spark: generator constant C', "LCG_C = 0xE06A02E7", "LCG_C = 0xE06A02E5", 1),
    ('spark: no z force', "    c.ww(e + 0x5C, force_z)", "    pass", 1),
    ('spark: first draw taken from the second state', "hi1 = (r1 * 0x20001) >> 32", "hi1 = (r2 * 0x20001) >> 32", 1),
    # --- L potion 0x80033274 and the ring
    ('lpotion: life not filled', "    c.ww(hero + 0x14, c.rw(hero + 0x18))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 0)", "    for i in range(8):" + NL + "        ring(c, i, 0)", 1),
    ('lpotion: life filled from the life', "    c.ww(hero + 0x14, c.rw(hero + 0x18))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 0)", "    c.ww(hero + 0x14, c.rw(hero + 0x14))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 0)", 1),
    ('lpotion: ring animation 0 -> 2', "        ring(c, i, 0)" + GAP + "def heal_elixir", "        ring(c, i, 2)" + GAP + "def heal_elixir", 1),
    ('lpotion: seven rings', "    for i in range(8):" + NL + "        ring(c, i, 0)", "    for i in range(7):" + NL + "        ring(c, i, 0)", 1),
    ('ring: offset shift 11 -> 12', "(px << 11)", "(px << 12)", 1),
    ('ring: offset shift of y 11 -> 10', "(py << 11)", "(py << 10)", 1),
    ('ring: force multiplier 448 -> 512', "c.ww(e + 0x54, (vx * 448) & M)", "c.ww(e + 0x54, (vx * 512) & M)", 1),
    ('ring: y force multiplier 448 -> 384', "c.ww(e + 0x58, (vy * 448) & M)", "c.ww(e + 0x58, (vy * 384) & M)", 1),
    ('ring: z force 0x30000 -> 0x20000', "c.ww(e + 0x5C, 0x30000)", "c.ww(e + 0x5C, 0x20000)", 1),
    ('ring: force index 8 + 4i -> 4 + 4i', "k = (8 + 4 * i) & 31", "k = (4 + 4 * i) & 31", 1),
    ('ring: force index wrapped at 64', "k = (8 + 4 * i) & 31", "k = (8 + 4 * i) & 63", 1),
    ('ring: position index 4i -> 2i', "px = s16(c.rh(TAB_X + 2 * (4 * i)))", "px = s16(c.rh(TAB_X + 2 * (2 * i)))", 1),
    ('ring: y position from the x table', "py = s16(c.rh(TAB_Y + 2 * (4 * i)))", "py = s16(c.rh(TAB_X + 2 * (4 * i)))", 1),
    ('ring: x force from the y table', "vx = s16(c.rh(TAB_X + 2 * k))", "vx = s16(c.rh(TAB_Y + 2 * k))", 1),
    ('ring: z lift 0x80000 -> 0x100000', "e = create_effect(c, anim, x, y, (z + 0x80000) & M)", "e = create_effect(c, anim, x, y, (z + 0x100000) & M)", 1),
    ('ring: offset replaced instead of added (x)', "c.ww(e + 0x3C, (c.rw(e + 0x3C) + (px << 11)) & M)", "c.ww(e + 0x3C, ((px << 11)) & M)", 1),
    ('ring: offset replaced instead of added (y)', "c.ww(e + 0x40, (c.rw(e + 0x40) + (py << 11)) & M)", "c.ww(e + 0x40, ((py << 11)) & M)", 1),
    ('ring: table entry read as unsigned', "px = s16(c.rh(TAB_X + 2 * (4 * i)))", "px = c.rh(TAB_X + 2 * (4 * i))", 1),
    ('ring: written although the effect was refused', "    e = create_effect(c, anim, x, y, (z + 0x80000) & M)" + NL + "    if e == 0:" + NL + "        return" + NL + "    px", "    e = create_effect(c, anim, x, y, (z + 0x80000) & M)" + NL + "    px", 1),
    # --- elixir 0x800336F0
    ('elixir: ring animation 2 -> 0', "        ring(c, i, 2)" + GAP + "def heal_cureale", "        ring(c, i, 0)" + GAP + "def heal_cureale", 1),
    ('elixir: magic not filled', "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", 1),
    ('elixir: magic set to 0', "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", "    set_mp_to(c, 0)" + NL + "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", 1),
    ('elixir: magic filled to the life maximum', "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", "    set_mp_to(c, c.rw(HERO + 0x18))" + NL + "    for i in range(8):" + NL + "        ring(c, i, 2)" + GAP + "def heal_cureale", 1),
    ('mp: negative maximum not floored', "    elif s32(value) < 0:" + "                                # 0x8004DEE4 bgez" + NL + "        c.wh(stats + 4, 0)" + NL, "    elif False:" + NL + "        c.wh(stats + 4, 0)" + NL, 1),
    ('mp: written to the maximum slot', "        c.wh(stats + 4, value)", "        c.wh(stats + 6, value)", 1),
    ('mp: maximum read as unsigned', "return s16(c.rh(c.rw(STATS_PTR) + 6))               # 0x8004DDDC", "return c.rh(c.rw(STATS_PTR) + 6)", 1),
    ('mp: current magic read as unsigned', "return s16(c.rh(c.rw(STATS_PTR) + 4))               # 0x8004DEA4", "return c.rh(c.rw(STATS_PTR) + 4)", 1),
    # --- cure ale 0x80033A2C
    ('cureale: life not filled', "    c.ww(hero + 0x14, c.rw(hero + 0x18))" + NL + "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):", "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):", 1),
    ('cureale: magic not filled', "    c.ww(hero + 0x14, c.rw(hero + 0x18))" + NL + "    set_mp_to(c, get_mp_max(c))" + NL + "    for i in range(8):", "    c.ww(hero + 0x14, c.rw(hero + 0x18))" + NL + "    for i in range(8):", 1),
    ('cureale: ring animations start at 0', "ring(c, i, 2 if i % 2 == 0 else 0)", "ring(c, i, 0 if i % 2 == 0 else 2)", 1),
    ('cureale: all rings animation 2', "ring(c, i, 2 if i % 2 == 0 else 0)", "ring(c, i, 2)", 1),
    ('cureale: seven rings', "    for i in range(8):" + NL + "        ring(c, i, 2 if i % 2 == 0 else 0)", "    for i in range(7):" + NL + "        ring(c, i, 2 if i % 2 == 0 else 0)", 1),
    ('cureale: first spark z force 0x40000 -> 0x20000', "spark(c, 0, 0x100000, 0x40000)", "spark(c, 0, 0x100000, 0x20000)", 1),
    ('cureale: second spark z force 0x20000 -> 0x40000', "spark(c, 2, 0x100000, 0x20000)", "spark(c, 2, 0x100000, 0x40000)", 1),
    ('cureale: second spark animation 2 -> 0', "spark(c, 2, 0x100000, 0x20000)", "spark(c, 0, 0x100000, 0x20000)", 1),
    ('cureale: sparks swapped', "        spark(c, 0, 0x100000, 0x40000)" + NL + "        spark(c, 2, 0x100000, 0x20000)", "        spark(c, 2, 0x100000, 0x20000)" + NL + "        spark(c, 0, 0x100000, 0x40000)", 1),
    ('cureale: three spark pairs', "    for _ in range(4):" + NL + "        spark(c, 0, 0x100000, 0x40000)", "    for _ in range(3):" + NL + "        spark(c, 0, 0x100000, 0x40000)", 1),
    ('cureale: only the first spark', "        spark(c, 2, 0x100000, 0x20000)" + GAP + "# ---- the start", "        pass" + GAP + "# ---- the start", 1),
    # --- the starts
    ('herb start: sound 0x31 -> 0x32', "play_sound(c, 0x31)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_lpotion", "play_sound(c, 0x32)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_lpotion", 1),
    ('lpotion start: sound 0x32 -> 0x31', "play_sound(c, 0x32)", "play_sound(c, 0x31)", 1),
    ('elixir start: sound 0x30 -> 0x31', "play_sound(c, 0x30)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_cureale", "play_sound(c, 0x31)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_cureale", 1),
    ('cureale start: sound 0x30 -> 0x32', "play_sound(c, 0x30)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_spotion", "play_sound(c, 0x32)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_spotion", 1),
    ('spotion start: sound 0x31 -> 0x32', "play_sound(c, 0x31)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "# ---- the dispatcher", "play_sound(c, 0x32)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "# ---- the dispatcher", 1),
    ('herb start: refusal sound 3 -> 4', "play_sound(c, 3)", "play_sound(c, 4)", 1),
    ('lpotion start: refusal sound 3 -> 4', "play_sound(c, 3)", "play_sound(c, 4)", 2),
    ('elixir start: refusal sound 3 -> 4', "play_sound(c, 3)", "play_sound(c, 4)", 3),
    ('cureale start: refusal sound 3 -> 4', "play_sound(c, 3)", "play_sound(c, 4)", 4),
    ('spotion start: refusal sound 3 -> 4', "play_sound(c, 3)", "play_sound(c, 4)", 5),
    ('herb start: no consumption', "play_sound(c, 0x31)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_lpotion", "play_sound(c, 0x31)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_lpotion", 1),
    ('lpotion start: no consumption', "play_sound(c, 0x32)" + NL + "        use_item(c, item)", "play_sound(c, 0x32)", 1),
    ('elixir start: no consumption', "play_sound(c, 0x30)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_cureale", "play_sound(c, 0x30)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_cureale", 1),
    ('cureale start: no consumption', "play_sound(c, 0x30)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_spotion", "play_sound(c, 0x30)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_spotion", 1),
    ('spotion start: no consumption', "play_sound(c, 0x31)" + NL + "        use_item(c, item)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "# ---- the dispatcher", "play_sound(c, 0x31)" + NL + "    else:" + NL + "        play_sound(c, 3)" + NL + "    return 1" + GAP + "# ---- the dispatcher", 1),
    ('herb start: consumes before the sound', "        heal_herb(c, HERO, item)" + NL + "        play_sound(c, 0x31)" + NL + "        use_item(c, item)", "        heal_herb(c, HERO, item)" + NL + "        use_item(c, item)" + NL + "        play_sound(c, 0x31)", 1),
    ('herb start: sound before the heal', "        heal_herb(c, HERO, item)" + NL + "        play_sound(c, 0x31)", "        play_sound(c, 0x31)" + NL + "        heal_herb(c, HERO, item)", 1),
    ('life test: < -> <=', "return s32(c.rw(HERO + 0x14)) < s32(c.rw(HERO + 0x18))", "return s32(c.rw(HERO + 0x14)) <= s32(c.rw(HERO + 0x18))", 1),
    ('life test: unsigned', "return s32(c.rw(HERO + 0x14)) < s32(c.rw(HERO + 0x18))", "return c.rw(HERO + 0x14) < c.rw(HERO + 0x18)", 1),
    ('life test: always true', "return s32(c.rw(HERO + 0x14)) < s32(c.rw(HERO + 0x18))", "return True", 1),
    ('magic test: < -> <=', "    return mp < get_mp_max(c)", "    return mp <= get_mp_max(c)", 1),
    ('magic test: always true', "    return mp < get_mp_max(c)", "    return True", 1),
    ('cureale start: and instead of or', "if life_below_max(c) or magic_below_max(c):", "if life_below_max(c) and magic_below_max(c):", 1),
    ('cureale start: life only', "if life_below_max(c) or magic_below_max(c):", "if life_below_max(c):", 1),
    ('cureale start: magic only', "if life_below_max(c) or magic_below_max(c):", "if magic_below_max(c):", 1),
    ('elixir start: life test instead of the magic test', "    if magic_below_max(c):" + NL + "        heal_elixir", "    if life_below_max(c):" + NL + "        heal_elixir", 1),
    ('herb start: answers 0', "play_sound(c, 3)" + NL + "    return 1" + GAP + "def start_lpotion", "play_sound(c, 3)" + NL + "    return 0" + GAP + "def start_lpotion", 1),
    # --- use_item
    ('use_item: decrement 2', "c.wh(entry + 2, count - 1)" + NL + "    return s16(count - 1)", "c.wh(entry + 2, count - 2)" + NL + "    return s16(count - 1)", 1),
    ('use_item: decrements at count 0 too', "    if count == 0:                                      # 0x8004E614" + NL + "        return -1" + NL, "    if False:" + NL + "        return -1" + NL, 1),
    ('use_item: wrong halfword', "count = s16(c.rh(entry + 2))", "count = s16(c.rh(entry + 0))", 1),
    ('use_item: entry stride 4 -> 8', "entry = (c.rw(INV_PTR) + 4 * item) & M", "entry = (c.rw(INV_PTR) + 8 * item) & M", 1),
    ('use_item: size test <= instead of <', "if not (s32(item) < size):", "if not (s32(item) <= size):", 1),
    ('use_item: no size test', "    if not (s32(item) < size):" + "                          # 0x8004E5E0 slt" + NL + "        c.stub(0x8004DB90, [0x80026674])" + NL + "        return 0" + NL, "", 1),
    ('use_item: no negative test', "    if s32(item) < 0:" + "                                   # 0x8004E5CC bltz" + NL + "        c.stub(0x8004DB90, [0x80026674])" + NL + "        return 0" + NL, "", 1),
    ('use_item: error message address', "c.stub(0x8004DB90, [0x80026674])" + NL + "        return 0" + NL + "    entry", "c.stub(0x8004DB90, [0x80026670])" + NL + "        return 0" + NL + "    entry", 1),
    # --- the dispatcher
    ('dispatch: in-use rule without the same-item pass', "if cur != 0 and cur != s0:", "if cur != 0:", 1),
    ('dispatch: in-use rule ignored', "if cur != 0 and cur != s0:", "if False:", 1),
    ('dispatch: in-use rule inverted', "if cur != 0 and cur != s0:", "if cur != 0 and cur == s0:", 1),
    ('dispatch: table lower bound forgotten', "elif s0 - 0x1F >= 0x14 or s0 < 0x1F:", "elif s0 - 0x1F >= 0x14:", 1),
    ('dispatch: table size 0x14 -> 0x13', "elif s0 - 0x1F >= 0x14 or s0 < 0x1F:", "elif s0 - 0x1F >= 0x13 or s0 < 0x1F:", 1),
    ('dispatch: hole 0x28 forgotten', "elif s0 in (0x21, 0x22, 0x28, 0x2A):", "elif s0 in (0x21, 0x22, 0x2A):", 1),
    ('dispatch: hole 0x2A forgotten', "elif s0 in (0x21, 0x22, 0x28, 0x2A):", "elif s0 in (0x21, 0x22, 0x28):", 1),
    ('dispatch: hole 0x21 forgotten', "elif s0 in (0x21, 0x22, 0x28, 0x2A):", "elif s0 in (0x22, 0x28, 0x2A):", 1),
    ('dispatch: hole 0x22 forgotten', "elif s0 in (0x21, 0x22, 0x28, 0x2A):", "elif s0 in (0x21, 0x28, 0x2A):", 1),
    ('dispatch: refusal sound 3 -> 4', "    if fail:" + NL + "        play_sound(c, 3)", "    if fail:" + NL + "        play_sound(c, 4)", 1),
    ('dispatch: instant items set the item in use', "        c.stub(INSTANT[s0], [s0])" + "                       # the answer is ignored: the instant items answer the constant 1 (0x80034A38...)" + NL + "        return 1", "        c.stub(INSTANT[s0], [s0])" + NL + "        c.ww(IN_USE, s0)" + NL + "        return 1", 1),
    ('dispatch: instant items answer the start answer', "        c.stub(INSTANT[s0], [s0])" + "                       # the answer is ignored: the instant items answer the constant 1 (0x80034A38...)" + NL + "        return 1", "        return c.stub(INSTANT[s0], [s0])", 1),
    ('dispatch: instant start not called', "        c.stub(INSTANT[s0], [s0])" + "                       # the answer is ignored: the instant items answer the constant 1 (0x80034A38...)" + NL + "        return 1", "        return 1", 1),
    ('dispatch: in-use set on every answer', "    if v == 0:" + "                                          # 0x80034A88-0x80034A9C", "    if True:", 1),
    ('dispatch: in-use set on answer 1', "    if v == 0:" + "                                          # 0x80034A88-0x80034A9C", "    if v == 1:", 1),
    ('dispatch: counter not zeroed', "        c.ww(COUNTER, 0)" + NL, "", 1),
    ('dispatch: phase not zeroed', "        c.ww(PHASE, 0)" + NL, "", 1),
    ('dispatch: in-use not written', "        c.ww(IN_USE, s0)" + NL + "        c.ww(COUNTER, 0)", "        c.ww(COUNTER, 0)", 1),
    ('dispatch: answer lost', "    return v" + GAP + "CTRL = 0x800DC4B8", "    return 1" + GAP + "CTRL = 0x800DC4B8", 1),
    ('dispatch: other start receives the wrong argument', "v = c.stub(OTHER.get(s0, 0), [s0]) & M", "v = c.stub(OTHER.get(s0, 0), [s0 + 1]) & M", 1),
    # --- the two divergences of the decompilation (the binary prevails): the vectors must tell them from the binary
    ('DECOMPILATION D-H1-2: ring position from the force index (8 + 4i) & 31', "px = s16(c.rh(TAB_X + 2 * (4 * i)))" + NL + "    py = s16(c.rh(TAB_Y + 2 * (4 * i)))", "px = s16(c.rh(TAB_X + 2 * ((8 + 4 * i) & 31)))" + NL + "    py = s16(c.rh(TAB_Y + 2 * ((8 + 4 * i) & 31)))", 1),
    ('DECOMPILATION D-H1-1: the S Potion start is the cure-ale body', "    if life_below_max(c):" + NL + "        heal_spotion(c, HERO, item)" + NL + "        play_sound(c, 0x31)", "    if life_below_max(c) or magic_below_max(c):" + NL + "        heal_cureale(c, HERO, item)" + NL + "        play_sound(c, 0x30)", 1),
    # --- TryUseItem 0x8002ED64
    ('try: front mask 0x20 -> 0x10', "if (pressed & 0x20) == 0:", "if (pressed & 0x10) == 0:", 1),
    ('try: front mask 0x20 -> 0x30', "if (pressed & 0x20) == 0:", "if (pressed & 0x30) == 0:", 1),
    ('try: no front still tries', "if (pressed & 0x20) == 0:", "if False:", 1),
    ('try: no front answers 0', "    if (pressed & 0x20) == 0:" + NL + "        return 1", "    if (pressed & 0x20) == 0:" + NL + "        return 0", 1),
    ('try: control flags test only bit 0x34', "if c.rw(CTRL) != 0:", "if c.rw(CTRL) & 0x34:", 1),
    ('try: control flags test only bit 4', "if c.rw(CTRL) != 0:", "if c.rw(CTRL) & 4:", 1),
    ('try: control flags ignored', "if c.rw(CTRL) != 0:", "if False:", 1),
    ('try: locked refusal sound 3 -> 4', "play_sound(c, 3)" + NL + "        return 1" + NL + "    return c.stub(0x8003499C", "play_sound(c, 4)" + NL + "        return 1" + NL + "    return c.stub(0x8003499C", 1),
    ('try: locked refusal answers 0', "play_sound(c, 3)" + NL + "        return 1" + NL + "    return c.stub(0x8003499C", "play_sound(c, 3)" + NL + "        return 0" + NL + "    return c.stub(0x8003499C", 1),
    ('try: dispatcher answer lost', "    return c.stub(0x8003499C, []) & M", "    c.stub(0x8003499C, [])" + NL + "    return 1", 1),
]


# Mutants that are EQUIVALENT on the values (they survive in lenient mode) and why:
#  - the life read as unsigned / the floor division of a negative maximum: the sum is wrapped to 32 bits and a negative quarter or half is below the minimum anyway, so the result is the same
#  - the bound 0x62 of the dispatcher: redundant with the table bound (s0 - 0x1F < 0x14, unsigned), only the set of bytes read differs (the in-use word is not read for s0 >= 0x62)
#  - magic test before the life test: the same result, only the order of the two reads differs
EQUIVALENT = [
    ('cap: reads the life as unsigned', "hp = s32(c.rw(hero + 0x14))" + NL + "    new", "hp = c.rw(hero + 0x14)" + NL + "    new", 1),
    ('cap: floor division of a negative maximum', "q = abs(x) // d" + NL + "    return q if x >= 0 else -q", "return x // d", 1),
    ('cureale start: magic first', "if life_below_max(c) or magic_below_max(c):", "if magic_below_max(c) or life_below_max(c):", 1),
    ('dispatch: bound 0x62 -> 0x61', "if s0 >= 0x62:", "if s0 >= 0x61:", 1),
    ('dispatch: bound 0x62 -> 0x63', "if s0 >= 0x62:", "if s0 >= 0x63:", 1),
    ('dispatch: bound as signed', "if s0 >= 0x62:", "if s32(s0) >= 0x62:", 1),
]


def mutate(src, anchor, repl, occ):
    idx = -1
    for _ in range(occ):
        idx = src.find(anchor, idx + 1)
        if idx < 0:
            raise ValueError('anchor not found (occurrence %d): %r' % (occ, anchor[:70]))
    return src[:idx] + repl + src[idx + len(anchor):]


def build(src, name):
    mod = types.ModuleType('mut')
    exec(compile(src, name, 'exec'), mod.__dict__)
    return mod


def killed(src, name, vecs, lenient):
    mod = build(src, name)
    mod.LENIENT = lenient
    for v in vecs:
        r = CM.check_vector(mod, v)
        if r:
            return r, v['label']
    return None, None


def main(vecdir):
    vecs = []
    for f in sorted(glob.glob(os.path.join(vecdir, 'vec_*.json'))):
        vecs += json.load(open(f))['vectors']
    base = build(SRC, 'baseline')
    for lenient in (False, True):
        base.LENIENT = lenient
        bad = [v['label'] for v in vecs if CM.check_vector(base, v)]
        assert not bad, ('baseline fails', lenient, bad[:3])
    print('baseline: 0 mismatches on %d vectors (strict and lenient)' % len(vecs))
    survivors = []
    only_read = []
    crashes = []
    n = 0
    for name, anchor, repl, occ in MUTANTS:
        src = mutate(SRC, anchor, repl, occ)
        assert src != SRC, name
        n += 1
        r_strict, lab = killed(src, name, vecs, False)
        r_len, lab2 = killed(src, name, vecs, True)
        if r_strict is None:
            survivors.append(name)
            print('SURVIVES  %s' % name)
        elif r_len is None:
            only_read.append(name)
            print('killed (read set only)  %-62s by "%s": %s' % (name, lab, r_strict))
        else:
            if 'raised' in r_len:
                crashes.append(name)
            print('killed    %-62s by "%s": %s' % (name, lab2, r_len))
    print()
    print('%d mutants: %d killed by a value difference, %d killed only by the read set, %d killed by a crash of the mutated model, %d survivors' % (
        n, n - len(only_read) - len(survivors), len(only_read), len(crashes), len(survivors)))
    for s_ in survivors:
        print('  survivor:', s_)
    print()
    print('EQUIVALENT mutants (must survive in lenient mode; strict mode may still tell them by the read set):')
    bad_eq = []
    for name, anchor, repl, occ in EQUIVALENT:
        src = mutate(SRC, anchor, repl, occ)
        r_len, lab2 = killed(src, name, vecs, True)
        r_strict, lab = killed(src, name, vecs, False)
        print('  %-52s lenient: %s   strict: %s' % (name, 'survives' if r_len is None else 'KILLED (not equivalent!) ' + str(r_len), 'survives' if r_strict is None else 'read set (' + r_strict + ')'))
        if r_len is not None:
            bad_eq.append(name)
    return 1 if (survivors or bad_eq) else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'vec')))
