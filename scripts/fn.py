# Desmonta a funcao que contem um endereco: volta ate o 'push' mais proximo e segue ate pop/pc
import sys;sys.path.insert(0,'scripts')
from fwlib import *
def fstart(a):
    for s in range(a&~1, a-0x800, -2):
        o=a2o(s)
        if D[o+1]==0xb5 or (D[o]==0x2d and D[o+1]==0xe9 and D[o+3]&0x40): 
            # confirma que desmonta ate a
            ok=False
            for i in md.disasm(D[o:a2o(a)+4],s):
                if i.address==(a&~1): ok=True;break
            if ok: return s
    return None
def show(a,maxn=400):
    s=fstart(a); print('== funcao em',hex(s))
    o=a2o(s); n=0
    for i in md.disasm(D[o:o+maxn*4],s):
        note=''
        if i.mnemonic.startswith('ldr') and '[pc' in i.op_str and '#' in i.op_str:
            t=((i.address+4)&~3)+int(i.op_str.split('#')[-1].rstrip(']'),0)
            try:
                v=u32(t); st=cstr(v) if v>=0x80000000 else None
                note=f' ; ={v:#x}'+(f' "{st}"' if st else '')
            except Exception: pass
        print(f'{i.address:#010x}: {i.mnemonic:8s} {i.op_str}{note}')
        n+=1
        if n>maxn or (n>3 and (i.mnemonic.startswith('pop') and 'pc' in i.op_str) and i.address>a) : break
for x in sys.argv[1:]: show(int(x,16))
