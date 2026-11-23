# Estima o deslocamento arquivo<->0x80000000 via ponteiros de funcao vs prologos
import sys,struct,random
from collections import Counter
d=open(sys.argv[1],'rb').read()
END=0x3ff978
prol=set()
for o in range(0x78,END-4,2):
    h=d[o+1]; 
    if h==0xb5: prol.add(o)
    elif d[o]==0x2d and d[o+1]==0xe9 and (d[o+3]&0x40): prol.add(o)
ptrs=[]
for o in range(0x78,END,4):
    w=struct.unpack_from('<I',d,o)[0]
    if 0x80000000<=w<0x80400000 and w&1: ptrs.append(w-0x80000001)
print('prologos',len(prol),'ponteiros impares 0x80..',len(ptrs))
random.seed(1)
samp=random.sample(ptrs,min(300,len(ptrs)))
pl=sorted(prol)
c=Counter()
for a in samp:
    for p in pl: c[p-a]+=1
for k,v in c.most_common(10): print('delta(file_off - (addr-0x80000000))=',hex(k) if k>=0 else '-'+hex(-k),'hits',v)
