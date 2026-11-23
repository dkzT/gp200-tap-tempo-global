# uso: python scripts/dis.py <addr_hex> <antes_bytes_hex> <n>
import sys
sys.path.insert(0,'scripts')
from fwlib import *
a=int(sys.argv[1],16); back=int(sys.argv[2],16) if len(sys.argv)>2 else 0x40; n=int(sys.argv[3]) if len(sys.argv)>3 else 60
for i in dis(a-back,n):
    note=''
    if i.mnemonic.startswith('ldr') and '[pc' in i.op_str and '#' in i.op_str:
        t=((i.address+4)&~3)+int(i.op_str.split('#')[-1].rstrip(']'),0)
        try:
            v=u32(t); s=cstr(v) if v>=0x80000000 else None
            note=f'  ; ={v:#x}'+(f' "{s}"' if s else '')
        except Exception: pass
    mark='>>' if i.address==a else '  '
    print(f'{mark}{i.address:#010x}: {i.mnemonic:8s} {i.op_str}{note}')
