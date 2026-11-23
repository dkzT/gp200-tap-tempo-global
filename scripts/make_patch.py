# Gera a copia modificada (tap tempo global) a partir do original e verifica byte a byte.
# Uso: python scripts/make_patch.py
# Nao grava nada na pedaleira. Le original/ e escreve em work/.
import hashlib, sys
sys.path.insert(0, 'scripts')
from capstone import Cs, CS_ARCH_ARM, CS_MODE_THUMB, CS_MODE_MCLASS

ORIG = 'original/GP-200_USB_Firmware_V1.8.2.bin'
OUT = 'work/GP-200_V1.8.2_TAPGLOBAL_REVISAO.bin'
ORIG_SHA = 'c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af'

# (offset no arquivo, bytes originais, bytes novos, descricao)
PATCH = [
    (0xA556, 'f0b1',     '00bf',     'set_tempo: sempre copiar o tap para o BPM global (cbz -> nop)'),
    (0x8866, 'a8b1',     '00bf',     'set_global_bpm: sempre aplicar o BPM global ao tempo atual (cbz -> nop)'),
    (0xA95E, 'd8b1',     '00bf',     'select_patch: sempre aplicar o BPM global na troca de preset (cbz -> nop)'),
    (0xA960, 'b4f86c02', 'b4f82201', 'select_patch: ldrh r0,[r4,#0x26c] -> ldrh r0,[r4,#0x122] (le o global)'),
    (0xA964, 'a4f82201', 'a4f86c02', 'select_patch: strh r0,[r4,#0x122] -> strh r0,[r4,#0x26c] (grava no preset atual)'),
]

orig = open(ORIG, 'rb').read()
assert hashlib.sha256(orig).hexdigest() == ORIG_SHA, 'original diferente do esperado!'
assert sum(orig[0x30:]) & 0xFF == orig[0x0E], 'checksum do original nao confere'

new = bytearray(orig)
for off, o, n, _ in PATCH:
    assert orig[off:off+len(o)//2] == bytes.fromhex(o), f'bytes originais inesperados em {off:#x}'
    new[off:off+len(n)//2] = bytes.fromhex(n)
new[0x0E] = sum(new[0x30:]) & 0xFF
open(OUT, 'wb').write(new)

# ---- verificacao independente: rele os dois arquivos do disco ----
a = open(ORIG, 'rb').read(); b = open(OUT, 'rb').read()
print('tamanho original:', len(a), ' modificado:', len(b), ' iguais:', len(a) == len(b))
diff = [i for i in range(len(a)) if a[i] != b[i]]
print('bytes diferentes:', len(diff))
allowed = {0x0E}
for off, o, n, _ in PATCH:
    allowed |= set(range(off, off + len(n)//2))
print('todas as diferencas estao na lista do patch:', set(diff) <= allowed)
print('bytes identicos fora do patch:', len(a) - len(diff))
print('checksum (soma 8 bits de 0x30..fim) original %#04x / byte 0x0E %#04x' % (sum(a[0x30:]) & 0xFF, a[0x0E]))
print('checksum (soma 8 bits de 0x30..fim) modific. %#04x / byte 0x0E %#04x' % (sum(b[0x30:]) & 0xFF, b[0x0E]))
print('SHA-256 original  :', hashlib.sha256(a).hexdigest())
print('SHA-256 modificado:', hashlib.sha256(b).hexdigest())
print()
print('offset   original  -> novo')
for i in diff:
    print(f'{i:#08x}  {a[i]:02x}  -> {b[i]:02x}')
print()
md = Cs(CS_ARCH_ARM, CS_MODE_THUMB | CS_MODE_MCLASS)
for off, o, n, desc in PATCH:
    addr = off - 0x78
    ia = ' ; '.join(f'{x.mnemonic} {x.op_str}' for x in md.disasm(a[off:off+len(o)//2], addr))
    ib = ' ; '.join(f'{x.mnemonic} {x.op_str}' for x in md.disasm(b[off:off+len(n)//2], addr))
    print(f'{off:#07x} (end. {addr:#06x}): {desc}\n    antes : {ia}\n    depois: {ib}')
