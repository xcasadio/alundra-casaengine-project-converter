import pickle, sys
sys.stdout.reconfigure(encoding="utf-8")
rows,tot=pickle.load(open('census.pkl','rb'))
story=set(range(162,183))|{44,10}
for r in rows:
    if r['map'] in story:
        d=r.get('detail',{})
        print(r['map'], r['zone'], r['pc'], r['reach'], r.get('class'), 'lock' if r.get('lock') else '', '|', d.get('root'), d.get('actor'), 'anim',d.get('anim'),'sp',d.get('speed'),'dir',d.get('dir'), d.get('from'), d.get('res'), d.get('why'), 'nstates', len(r.get('all',[])))
