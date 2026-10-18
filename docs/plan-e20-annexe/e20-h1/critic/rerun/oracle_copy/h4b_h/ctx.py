import sys, tools
sites = [int(x,16) for x in sys.argv[2:]]
n = int(sys.argv[1])
for s in sites:
    print('-----', hex(s))
    for a in range(s-8, s+4*n, 4):
        print(tools.text(a))
