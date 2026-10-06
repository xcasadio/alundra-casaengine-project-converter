"""Python port of PCSX-Redux soft GPU textured triangle path (src/gpu/soft/polys.cc: setupSections3, leftSection3,
rightSection3, nextRow3, drawPoly3Raster flat-textured span walk, drawPoly3T) and of the quad split in gpu.cc
(triangles (1,3,2) then (0,1,2)). Source: D:/development/repo/pcsx-redux (the comments there say the UV sampler was
verified on SCPH-5501: floor((accum_16_16 + 0x8000) >> 16)). int32/int64 C++ semantics emulated."""
def cdiv(a, b):
    q = abs(a) // abs(b)
    return q if (a < 0) == (b < 0) else -q
def i32(x):
    x &= 0xFFFFFFFF
    return x - (1 << 32) if x & 0x80000000 else x
def shl10idiv(x, y):
    return cdiv(x << 10, y)

class V:  # vertex
    __slots__ = ('x', 'y', 'u', 'v')
    def __init__(s, x, y, u, v):
        s.x = x << 16; s.y = y; s.u = u << 16; s.v = v << 16

class Walker:
    def tri(self, p1, p2, p3, bias=0x8000, out=None):
        """p = (x, y, u, v) in call order (x1,y1,x2,y2,x3,y3,tx..). returns list of (x,y,texU,texV)"""
        v1 = V(*p1); v2 = V(*p2); v3 = V(*p3)
        if v1.y > v2.y: v1, v2 = v2, v1
        if v1.y > v3.y: v1, v3 = v3, v1
        if v2.y > v3.y: v2, v3 = v3, v2
        height = v3.y - v1.y
        if height == 0: return []
        temp = cdiv((v2.y - v1.y) << 16, height)
        longest = temp * ((v3.x - v1.x) >> 16) + (v1.x - v2.x)
        if longest == 0: return []
        self.ra = None
        def left_section():
            a = self.la[self.ls]; b = self.la[self.ls - 1]
            h = b.y - a.y
            if h == 0: return 0
            self.lsx = a.x; self.ldx = b.x - a.x; self.lx = a.x
            self.lsu = a.u; self.ldu = b.u - a.u; self.lu = a.u
            self.lsv = a.v; self.ldv = b.v - a.v; self.lv = a.v
            self.lfh = h; self.lh = h
            return h
        def right_section():
            a = self.ra_[self.rs]; b = self.ra_[self.rs - 1]
            h = b.y - a.y
            if h == 0: return 0
            self.rsx = a.x; self.rdx = b.x - a.x; self.rx = a.x
            self.rfh = h; self.rh = h
            return h
        self.left_section = left_section; self.right_section = right_section
        if longest < 0:
            self.ra_ = [v3, v2, v1]; self.rs = 2
            self.la = [v3, v1]; self.ls = 1
            if left_section() <= 0: return []
            if right_section() <= 0:
                self.rs -= 1
                if right_section() <= 0: return []
            if longest > -0x1000: longest = -0x1000
        else:
            self.la = [v3, v2, v1]; self.ls = 2
            self.ra_ = [v3, v1]; self.rs = 1
            if right_section() <= 0: return []
            if left_section() <= 0:
                self.ls -= 1
                if left_section() <= 0: return []
            if longest < 0x1000: longest = 0x1000
        ymin = v1.y; ymax = v3.y - 1
        difX = shl10idiv(temp * ((v3.u - v1.u) >> 10) + ((v1.u - v2.u) << 6), longest)
        difY = shl10idiv(temp * ((v3.v - v1.v) >> 10) + ((v1.v - v2.v) << 6), longest)
        res = []
        for i in range(ymin, ymax + 1):
            xmin = (self.lx + 0xffff) >> 16
            xmax = (self.rx - 1) >> 16
            if xmax >= xmin:
                posX = self.lu + ((((xmin << 16) - self.lx) * difX) >> 16) + bias
                posY = self.lv + ((((xmin << 16) - self.lx) * difY) >> 16) + bias
                for j in range(xmin, xmax + 1):
                    res.append((j, i, (posX >> 16) & 0xff, (posY >> 16) & 0xff))
                    posX += difX; posY += difY
            # nextRow3
            self.lh -= 1
            if self.lh <= 0:
                self.ls -= 1
                if self.ls <= 0: return res
                if self.left_section() <= 0: return res
            else:
                row = self.lfh - self.lh
                self.lx = self.lsx + cdiv(self.ldx * row, self.lfh)
                self.lu = self.lsu + row * cdiv(self.ldu, self.lfh)
                self.lv = self.lsv + row * cdiv(self.ldv, self.lfh)
            self.rh -= 1
            if self.rh <= 0:
                self.rs -= 1
                if self.rs <= 0: return res
                if self.right_section() <= 0: return res
            else:
                row = self.rfh - self.rh
                self.rx = self.rsx + cdiv(self.rdx * row, self.rfh)
        return res

def quad(corners, uvs, bias=0x8000):
    """corners/uvs index 0..3 = TL,TR,BL,BR as PSX vertices 0..3. Redux: tri(1,3,2) then tri(0,1,2)."""
    w = Walker(); px = {}
    for tri in ((1, 3, 2), (0, 1, 2)):
        pts = [(corners[k][0], corners[k][1], uvs[k][0], uvs[k][1]) for k in tri]
        for (x, y, tu, tv) in w.tri(*pts, bias=bias):
            px[(x, y)] = (tu, tv)
    return px
