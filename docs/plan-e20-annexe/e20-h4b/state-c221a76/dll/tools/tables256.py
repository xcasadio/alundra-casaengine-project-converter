import math, exelib
sin_b = [exelib.s16(0x800204F0 + 2*i) for i in range(256)]
cos_b = [exelib.s16(0x800206F0 + 2*i) for i in range(256)]
print("sin[0..9]", sin_b[:10], "sin[63..65]", sin_b[63:66], "sin[128]", sin_b[128], "sin[192]", sin_b[192])
print("cos[0..9]", cos_b[:10], "cos[64]", cos_b[64], "cos[128]", cos_b[128])
# candidate formulas
def rnd(x): return int(math.floor(x + 0.5))
for name, f in (("round", rnd), ("trunc", int), ("floor", math.floor)):
    s_ok = sum(1 for i in range(256) if f(256*math.sin(2*math.pi*i/256)) == sin_b[i])
    c_ok = sum(1 for i in range(256) if f(256*math.cos(2*math.pi*i/256)) == cos_b[i])
    print(name, "sin matches", s_ok, "/256", "cos matches", c_ok, "/256")
print("cos[i] == sin[(i+64)&255] for all i:", all(cos_b[i] == sin_b[(i+64) & 255] for i in range(256)))
print("sin[(i+128)&255] == -sin[i] for all i:", all(sin_b[(i+128) & 255] == -sin_b[i] for i in range(256)))
# the 32-direction tables of the DLL for contrast: dump 0x80023654 / 0x80023694 first entries
print("dir tables X[0..8]", [exelib.s16(0x80023654 + 2*i) for i in range(9)], "Y[0..8]", [exelib.s16(0x80023694 + 2*i) for i in range(9)])
