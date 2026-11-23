# Varredura linear do app: indexa literais LDR (pc-relativo), movw/movt e BL
import sys,json,pickle
sys.path.insert(0,'scripts')
from fwlib import *
start=APP_BASE; end=APP_BASE+int(sys.argv[1],16)
lit={}   # endereco do literal -> [instrucoes que o carregam]
movw=[]  # (addr, reg, imm)
bl={}    # alvo -> [chamadores]
a=start
code=D[a2o(start):a2o(end)]
md2=Cs(CS_ARCH_ARM, CS_MODE_THUMB|CS_MODE_MCLASS); md2.detail=False
pos=0; n=0
while pos<len(code)-2:
    got=False
    for i in md2.disasm(code[pos:pos+0x4000], start+pos):
        got=True
        m=i.mnemonic; ops=i.op_str
        if m.startswith('ldr') and '[pc' in ops:
            try:
                off=int(ops.split('#')[-1].rstrip(']'),0) if '#' in ops else 0
                tgt=((i.address+4)&~3)+off
                lit.setdefault(tgt,[]).append(i.address)
            except Exception: pass
        elif m in('movw','movt') :
            r,imm=ops.split(', #'); movw.append((i.address,m,r,int(imm,0)))
        elif m in ('bl','b.w','b') and ops.startswith('#'):
            if m=='bl': bl.setdefault(int(ops[1:],0),[]).append(i.address)
        elif m=='adr':
            pass
        pos=i.address+i.size-start; n+=1
    if not got: pos+=2
pickle.dump({'lit':lit,'movw':movw,'bl':bl},open('work/index.pkl','wb'))
print('instr',n,'literais',len(lit),'movw/movt',len(movw),'alvos bl',len(bl))
