import sys, os
sys.path.insert(0, r"C:/Users/casad/AppData/Roaming/Python/Python314/site-packages")
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import w1
import gl
R,L,U,D,TRI,CROSS,SQ = gl.RIGHT,gl.LEFT,gl.UP,gl.DOWN,gl.TRI,gl.CROSS,gl.SQUARE
which = sys.argv[1]
if which == 'carry':
    # lift the crate, wait for the gesture, walk 9 ticks, release
    w1.show('carry', [(10,R),(1,R|SQ),(49,0),(9,R),(6,0)], hero_px=(200,200), crate_x=230, first=58)
elif which == 'run':
    w1.show('run', [(40,R|TRI),(22,0)], first=38)
elif which == 'wall':
    def cells(col,row): return dict(h=4) if col>=8 else None
    w1.show('wall', [(14,R),(6,0)], hero_px=(150,100), cells=cells, first=8)
elif which == 'jump':
    w1.show('jump', [(5,R),(1,R|CROSS),(2,R),(14,0)])
elif which == 'tap1':
    w1.show('tap 1 tick', [(1,R),(5,0)])
elif which == 'reverse':
    w1.show('reverse', [(9,R),(9,L),(4,0)], hero_px=(150,100))
