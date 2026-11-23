# Procura acessos a deslocamentos de campo (ex.: #0x26c) nas duas imagens
import sys
sys.path.insert(0,'scripts')
from fwlib import *
pats=[s.lower() for s in sys.argv[1:]]
def sweep(start,end):
    code=D[a2o(start):a2o(end)]; pos=0
    m2=Cs(CS_ARCH_ARM, CS_MODE_THUMB|CS_MODE_MCLASS)
    while pos<len(code)-2:
        got=False
        for i in m2.disasm(code[pos:pos+0x4000], start+pos):
            got=True
            for p in pats:
                if p in i.op_str and i.mnemonic[:2] in ('st','ld','ad'):
                    print(f'{i.address:#010x}: {i.mnemonic} {i.op_str}')
            pos=i.address+i.size-start
        if not got: pos+=2
sweep(0,0x22291)
sweep(APP_BASE,APP_BASE+0x33000)
