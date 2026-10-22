"""tileMap access (read-only) for the sm2 preparation: entity records of a map and their program indexes. python -B"""
import json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import corpus2 as C

BS = chr(92)


def tilemap_path(mid):
    base, folder = C.paths(mid)
    for f in os.listdir(folder + '/tilemap'):
        if f.endswith('.tileMap'):
            return folder + '/tilemap/' + f
    raise FileNotFoundError(folder)


def load_tilemap(mid):
    return json.load(open(tilemap_path(mid), encoding='utf-8'))


def objects(mid):
    d = load_tilemap(mid)
    for layer in d['object_layers']:
        for o in layer.get('objects', []):
            yield layer['name'], o


if __name__ == '__main__':
    mid = int(sys.argv[1])
    d = load_tilemap(mid)
    print(list(d.keys()))
    n = 0
    for lname, o in objects(mid):
        if n < 2:
            print(lname, json.dumps(o)[:2500])
        n += 1
    print('objects:', n)
