# List the 44 texels that change in font3.png (index 4 of FONT3.TIM), as (x, y) on the 256x256 page.
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
a = np.array(Image.open(r'D:\development\repo\alundra-casaengine-project-converter\data-extracted\ui\font3.png').convert('RGBA')).astype(int)
b = np.array(Image.open(os.path.join(HERE, 'out-lb', 'ui', 'font3.png')).convert('RGBA')).astype(int)
ys, xs = np.nonzero(np.any(a != b, axis=2))
pts = sorted(zip(xs.tolist(), ys.tolist()), key=lambda p: (p[1], p[0]))
print(len(pts))
print(';'.join(f'{x},{y}' for x, y in pts))
old = (a[:, :, 0] == 82) & (a[:, :, 1] == 90) & (a[:, :, 2] == 57) & (a[:, :, 3] == 255)
new = (b[:, :, 0] == 74) & (b[:, :, 1] == 82) & (b[:, :, 2] == 57) & (b[:, :, 3] == 255)
print('count (82,90,57,255) today', int(old.sum()), 'count (74,82,57,255) today', int(((a[:, :, 0] == 74) & (a[:, :, 1] == 82) & (a[:, :, 2] == 57)).sum()))
print('count (74,82,57,255) new', int(new.sum()), 'count (82,90,57,255) new', int(((b[:, :, 0] == 82) & (b[:, :, 1] == 90) & (b[:, :, 2] == 57)).sum()))
print('distinct colours today', len({tuple(c) for c in a.reshape(-1, 4)}), 'new', len({tuple(c) for c in b.reshape(-1, 4)}))
