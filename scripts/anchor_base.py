# Para alvos conhecidos (GIFs, strings), acha deslocamentos X tais que existe
# palavra w == 0x80000000 + (alvo - X). Conta X comuns entre alvos.
import sys,struct,re
from collections import Counter,defaultdict
d=open(sys.argv[1],'rb').read()
words=defaultdict(list)
for o in range(0,len(d)-3,4):
    w=struct.unpack_from('<I',d,o)[0]
    if 0x80000000<=w<0x80800000: words[w].append(o)
W=list(words)
anchors=[m.start() for m in re.finditer(b'GIF89a',d)]
# strings de rotulo
for s in [b'TAP\x00',b'Patch VOL/BPM\x00',b'GLOBAL SETTINGS\x00',b'LOOPER\x00',b'TUNER\x00',b'Global EQ\x00',b'Dual FS\x00',b'Single FS\x00',b'CANCEL\x00']:
    anchors+= [m.start() for m in re.finditer(re.escape(s),d) if d[m.start()-1]==0]
print('ancoras',[hex(a) for a in anchors])
c=Counter()
for a in anchors:
    xs=set(a-(w-0x80000000) for w in W)
    c.update(xs)
for x,n in c.most_common(10): print('X=',hex(x) if x>=0 else '-'+hex(-x),'ancoras com ref:',n)
