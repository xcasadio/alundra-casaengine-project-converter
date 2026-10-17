import json,collections
f=r"D:\development\repo\alundra-casaengine-project-converter\alundra-project\Maps\Overworld\Overworld 3,1-14\tilemap\Overworld 3,1-14.tileMap"
d=json.load(open(f,encoding='utf-8'))
c=json.loads(d['custom_properties']['AlundraCells'])
w=c['walkability']; h=c['height']
cut=[(i%52,i//52,h[i]) for i,v in enumerate(w) if v&2]
print('map14 cuttable',len(cut))
rows=collections.Counter(r for _,r,_ in cut)
print('rows',sorted(rows.items()))
print('cells in rows 4..8:',[(x,y,hh) for x,y,hh in cut if 4<=y<=8])
print('keys',list(c.keys()))
print('cells rows 24..28:',[(x,y,hh) for x,y,hh in cut if 24<=y<=28])
print('cells rows 10..12:',[(x,y,hh) for x,y,hh in cut if 10<=y<=12])
print('cells col 7..9:',sorted([(x,y,hh) for x,y,hh in cut if 7<=x<=9],key=lambda t:(t[1],t[0])))
print('wave south arena: hero x=212 px -> col',212//24,' y=96 -> row',96//16)
print('near swim (40,48):',[(x,y,hh) for x,y,hh in cut if 38<=x<=42 and 46<=y<=50])
print('near (100,100)=col4,row6; run (100,96)=col4,row6; (232,96)=col9,row6; (252,80)=col10,row5: cuttable rows<=8 count =',len([1 for x,y,hh in cut if y<=8]))
