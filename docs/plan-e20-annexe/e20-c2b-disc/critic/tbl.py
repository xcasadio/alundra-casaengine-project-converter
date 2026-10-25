exec(open('bx.py').read().split("if __name__")[0])
print([s16(0x80028B8C+2*i) for i in range(4)])
