"""Critic-own: print the entity records of a map (read-only on alundra-project/). Usage: python -B mapinfo.py <mapId> [recordIndex...]"""
import json, os, sys

PROJECT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
idx = json.load(open(PROJECT + '/Maps/world-index.json', encoding='utf-8'))
mid = sys.argv[1]
rel = idx[mid].replace(chr(92), '/')
print('index entry:', rel)
folder = os.path.dirname(rel)
path = PROJECT + '/' + folder
print('folder:', path, os.path.isdir(path))
if os.path.isdir(path):
    for root, dirs, files in os.walk(path):
        for f in files:
            print('  ', os.path.relpath(os.path.join(root, f), PROJECT).replace(chr(92), '/'), os.path.getsize(os.path.join(root, f)))
        break
