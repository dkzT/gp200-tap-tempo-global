# Utilitarios: mapeamento endereco<->offset e desmontagem Thumb com capstone
import struct
from capstone import Cs, CS_ARCH_ARM, CS_MODE_THUMB, CS_MODE_MCLASS
from capstone.arm import ARM_OP_IMM, ARM_OP_MEM, ARM_REG_PC
FW='work/fw.bin'
D=open(FW,'rb').read()
APP_OFF=0x22309; APP_BASE=0x80000000
LDR_OFF=0x78
def a2o(a):
    if a>=APP_BASE: return a-APP_BASE+APP_OFF
    return a+LDR_OFF
def o2a(o):
    return o-APP_OFF+APP_BASE if o>=APP_OFF else o-LDR_OFF
def u32(a): return struct.unpack_from('<I',D,a2o(a))[0]
def cstr(a,maxlen=80):
    o=a2o(a)
    if not 0<=o<len(D): return None
    e=D.find(b'\0',o)
    t=D[o:e]
    if 0<len(t)<=maxlen and all(32<=c<127 or c in (9,10) for c in t): return t.decode()
    return None
md=Cs(CS_ARCH_ARM, CS_MODE_THUMB|CS_MODE_MCLASS); md.detail=True
def dis(a,n=40):
    o=a2o(a); out=[]
    for i in md.disasm(D[o:o+n*4],a):
        out.append(i)
        if len(out)>=n: break
    return out
