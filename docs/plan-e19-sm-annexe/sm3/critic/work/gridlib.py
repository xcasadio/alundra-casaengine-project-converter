import random
W, H = 52, 60
def make_grid(vec):
    g = [0] * (W * H)
    seed = vec.get('grid_seed')
    if seed is not None:
        rng = random.Random(seed)
        for y in range(H):
            for x in range(W):
                g[y * W + x] = rng.choice([0, 0, 1, 2, 3, 0x100, 0x102, 0x4, 0x6, 0x8])
    for x, y, v in vec.get('cells', []):
        g[y * W + x] = v
    return g
