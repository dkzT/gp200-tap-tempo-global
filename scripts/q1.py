import sys,pickle
sys.path.insert(0,'scripts')
from fwlib import *
ix=pickle.load(open('work/index.pkl','rb'))
lit=ix['lit']
vals={}
for la,users in lit.items():
    try: v=u32(la)
    except Exception: continue
    vals.setdefault(v,[]).extend(users)
print('== literais apontando para a regiao da tabela de strings 0x803DA000-0x803DC800')
for v in sorted(vals):
    if 0x803DA000<=v<0x803DC800: print(hex(v),[hex(x) for x in vals[v][:8]])
print('== constantes de tempo')
for c,name in [(60000,'60000'),(0x476A6000,'60000.0f'),(0x42700000,'60.0f'),(0x40ED4C00,'60000.0d hi'),(600000,'600000'),(6000000,'6000000')]:
    print(name,'literal:',[hex(x) for x in vals.get(c,[])][:10])
mw=[x for x in ix['movw'] if x[1]=='movw' and x[3]==60000]
print('movw 60000:',[hex(x[0]) for x in mw])
