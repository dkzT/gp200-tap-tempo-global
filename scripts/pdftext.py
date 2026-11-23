# Extrai texto simples de PDF (streams Flate) e mostra contexto de palavras-chave.
# uso: python -I pdftext.py <glob_do_pdf> palavra...
import sys, re, zlib, glob
path = glob.glob(sys.argv[1])[0]
d = open(path, 'rb').read()
txt = []
for m in re.finditer(rb'stream\r?\n', d):
    s = m.end(); e = d.find(b'endstream', s)
    try:
        t = zlib.decompress(d[s:e])
    except Exception:
        continue
    parts = re.findall(rb'\(((?:[^()\\]|\\.)*)\)', t)
    if parts:
        txt.append(b''.join(parts))
all_ = b'\n'.join(txt)
print(path, 'bytes de texto', len(all_))
for kw in sys.argv[2:]:
    k = kw.encode()
    for mm in list(re.finditer(k, all_))[:15]:
        print('>>', all_[max(0, mm.start()-200):mm.start()+200].replace(b'\n', b' ').decode('latin1'))
    print('----')
