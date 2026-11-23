# Lista chamadores (bl/b.w) e veneers do app para alvos dados
import sys;sys.path.insert(0,'scripts')
from fwlib import *
targets=[int(x,16) for x in sys.argv[1:]]
res={t:[] for t in targets}
def sweep(start,end):
    code=D[a2o(start):a2o(end)]; pos=0
    m2=Cs(CS_ARCH_ARM, CS_MODE_THUMB|CS_MODE_MCLASS)
    prev=None
    while pos<len(code)-2:
        got=False
        for i in m2.disasm(code[pos:pos+0x4000], start+pos):
            got=True
            if i.mnemonic in('bl','b.w','b') and i.op_str.startswith('#'):
                t=int(i.op_str[1:],0)
                if t in res: res[t].append((i.mnemonic,i.address))
            if i.mnemonic=='movw' and prev is None: pass
            # veneer: movw ip,#lo ; movt ip,#hi ; bx ip
            if i.mnemonic=='movt' and i.op_str.startswith('ip') and prev and prev.mnemonic=='movw' and prev.op_str.startswith('ip'):
                v=(int(i.op_str.split('#')[1],0)<<16)|int(prev.op_str.split('#')[1],0)
                if (v&~1) in res: res[v&~1].append(('veneer',prev.address))
            prev=i
            pos=i.address+i.size-start
        if not got: pos+=2; prev=None
sweep(0,0x22291); sweep(APP_BASE,APP_BASE+0x33000)
for t,v in res.items(): print(hex(t),[(m,hex(a)) for m,a in v])
