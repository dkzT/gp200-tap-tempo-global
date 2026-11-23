import sys,math
from collections import Counter
d=open(sys.argv[1],'rb').read()
W=int(sys.argv[2],0) if len(sys.argv)>2 else 0x8000
s,e=(int(sys.argv[3],0),int(sys.argv[4],0)) if len(sys.argv)>4 else (0,len(d))
for o in range(s,e,W):
    b=d[o:o+W]; c=Counter(b); n=len(b)
    H=-sum(v/n*math.log2(v/n) for v in c.values())
    z=c[0]/n; f=c[0xff]/n
    # thumb hint: fraction of halfwords with high byte in common opcode ranges
    print(f'{o:#08x} H={H:4.2f} z={z:4.2f} ff={f:4.2f} '+'#'*int(H*5))
