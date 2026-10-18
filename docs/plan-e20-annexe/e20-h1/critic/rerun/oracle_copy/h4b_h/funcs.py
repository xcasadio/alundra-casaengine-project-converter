"""funcs.py - function starts = every jal target + every handler-table entry + jump-table entries nearby; nearest preceding start of an address."""
import tools, bisect, handler_map, struct
_starts = None
def starts():
    global _starts
    if _starts is None:
        s = set()
        for a in range(tools.T_ADDR, tools.T_END, 4):
            w = tools.w32(a)
            if (w >> 26) == 3:
                s.add(((w & 0x03FFFFFF) << 2) | 0x80000000)
        for k, (base, codes) in handler_map.tables().items():
            s.update(codes.values())
        s.add(tools.PC0)
        _starts = sorted(x for x in s if tools.T_ADDR <= x < tools.T_END)
    return _starts
def func_of(a):
    st = starts()
    i = bisect.bisect_right(st, a) - 1
    return st[i]
if __name__ == '__main__':
    import sys
    for x in sys.argv[1:]:
        print(x, hex(func_of(int(x, 16))))
