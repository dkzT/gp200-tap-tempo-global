# Procura no firmware tratamento de SysEx de atualizacao (7E 48 ...): comparacoes com 0x7e e 0x48 proximas
import sys;sys.path.insert(0,'scripts')
from fwlib import *
def sweep(start,end):
    code=D[a2o(start):a2o(end)]; pos=0; win=[]
    m2=Cs(CS_ARCH_ARM, CS_MODE_THUMB|CS_MODE_MCLASS)
    hits=[]
    while pos<len(code)-2:
        got=False
        for i in m2.disasm(code[pos:pos+0x4000], start+pos):
            got=True
            s=i.mnemonic+' '+i.op_str
            win.append((i.address,s)); win=win[-12:]
            if i.mnemonic.startswith('cmp') and i.op_str.endswith('#0x48'):
                if any(('#0x7e' in w[1] and w[1].startswith('cmp')) for w in win):
                    hits.append(i.address)
            if ('#0x487e' in s) or ('#0x7e48' in s) or ('#0xf07e' in s): hits.append(i.address)
            pos=i.address+i.size-start
        if not got: pos+=2; win=[]
    return hits
for rng in ((0,0x22291),(APP_BASE,APP_BASE+0x33000)):
    print([hex(h) for h in sweep(*rng)])
