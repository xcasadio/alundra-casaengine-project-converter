# Read-only: the exported prefabs of the hero projectiles (names from AssetInfos.json, header words from Data/sprite-records.json).
import json, sys
sys.stdout.reconfigure(encoding='utf-8')
ROOT = 'D:/development/repo/alundra-casaengine-project-converter/alundra-project'
infos = json.load(open(ROOT + '/AssetInfos.json', encoding='utf-8'))['asset_infos']
names = {n['id']: (n.get('name') or n.get('Name') or '') for n in infos}
sr = json.load(open(ROOT + '/Data/sprite-records.json', encoding='utf-8'))
for k, v in sr.items():
    nm = names.get(k, '?')
    if any(w in nm for w in ('Fl\u00e8ches', 'Boule de feu', 'Boule de glace', 'Bombe', '\u00c9p\u00e9e d')):
        fl = v['MoreFlags'] | (v['CanPickup'] << 8) | (v['FlagsPortraitShadowType'] << 16)
        print(k[:8], nm, 'flags %06X' % fl, 'C', v['ProgramTick'], 'E', v['ProgramDeactivate'], 'BreakEffect', v.get('BreakEffect'), 'size', (v['SizeX'], v['SizeY'], v['SizeZ']))
