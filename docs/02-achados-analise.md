# GP-200 FW V1.8.2: achados de 23/11/2026 (análise estática)

Complementa `Documents/NOTAS-GP200-firmware-1.8.2.md`. Nada foi gravado na pedaleira.

## Arquivos de trabalho (nesta pasta)

- `original/GP-200_USB_Firmware_V1.8.2.bin`: cópia somente leitura do original (Downloads).
- `work/fw.bin`: cópia de trabalho.
- SHA-256 (os dois idênticos ao original): `c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af`
- `scripts/`: scripts Python (só leem o .bin).

## CONFIRMADO

1. **O aplicativo principal começa no offset 0x22309 do arquivo e roda em 0x80000000.**
   `endereço = 0x80000000 + offset − 0x22309` (vale para o app; o loader continua em `offset − 0x78`).
   - O campo do cabeçalho em 0x64 (`0x22291`) + 0x78 = `0x22309`. O campo 0x74 (`0x80000000`) é a base.
   - Em 0x22309 está o `__main` do compilador ARM/Keil (`00 f0 02 f8` = `bl __scatterload`), seguido de `bl __rt_entry` e do `__scatterload`/`__decompress` padrão. Bate com o `bx 0x80000001` do loader.
   - A paridade das instruções Thumb (`push.w`, `bx lr`) é par até ~0x22300 e ímpar depois: troca exatamente aí. Por isso as tentativas antigas (base 0x78) nunca acharam referências.
   - Com essa base, as strings da UI aparecem referenciadas: `TAP` (0x803D300C) em 4 lugares, `Patch VOL/BPM`, `GLOBAL SETTINGS` etc.
   - Ponteiros de função ímpares no app caem em prólogo de função em 1248 de 5604 casos (muito acima do acaso).
2. **Tabela de strings da UI (inglês)** começa por volta do offset 0x3FD471 (endereço ~0x803DB168): é um array de ponteiros, um por ID de string (English, BPM, Patch VOL, … TAP em 0x803DB35C, … Tap em 0x803DB3B4). Há cópias para outros idiomas logo depois (refs repetidas em 0x3FDA79, 0x3FE2A1, 0x3FE6B5).
3. **A região 0x16C600–0x250000 não é código criptografado**: são imagens GIF89a 480×272 (tela do aparelho), ex. em 0x16C655 e 0x1FF66F.
4. Os valores `0x807d79xx` que pareciam tabelas de ponteiros são pixels de imagem (bytes repetidos desalinhados), não ponteiros.

## HIPÓTESES

- Cabeçalho: 0x60 = `0x20028` seria o tamanho/fim do loader; 0x70 = `0x3DD6BD` o tamanho do app (0x22309 + 0x3DD6BD = 0x3FF9C6, ~0x4E além do fim da seção A em 0x3FF978; não fecha exato). 0x6C = `0x422B9` ainda sem significado.
- Strings de debug (`KEY: type...`, `KNOB ...`) ficam na área do loader/ITCM, não do app.
- Compilador ARMCC/Keil (pela assinatura do `__scatterload`). Isso ajuda a reconhecer funções da biblioteca C.

## PRÓXIMO

1. Precisa de um desassemblador (capstone ou Ghidra) para seguir: quem lê a tabela de strings no índice de `TAP`/`BPM`, o tratador do footswitch e onde o BPM do preset é guardado.
2. Verificar CRC/assinatura (passo 4 das notas originais) agora que o layout está claro.

## Para abrir no Ghidra

Dois blocos de memória a partir do mesmo arquivo:
- loader: arquivo [0x78, 0x22309) em 0x00000000
- app: arquivo [0x22309, 0x3FF978) em 0x80000000
Linguagem `ARM:LE:32:Cortex` (Thumb).

---

# Etapa 2: tratador do tap e campo de BPM (capstone)

Endereços: `0x0000xxxx` = imagem baixa (offset = end + 0x78); `0x800xxxxx` = app (offset = end − 0x80000000 + 0x22309).

## CONFIRMADO no código

1. **Arquitetura em duas camadas.** A imagem baixa (em 0x0, ~136 KB) contém a lógica do sistema (estado, presets, tempo). O app em 0x80000000 (UI, ~200 KB de código + dados/imagens) chama a imagem baixa por *veneers* `movw ip / movt ip / bx ip` (ex. 0x8001576E → 0xA4B1) e trampolins `b.w` em 0x800284xx–0x800285xx.
2. **Cálculo do tap** (três cópias quase iguais no app: 0x800233D0, 0x80023CF4, 0x800261FE):
   `bpm = 60000 / (agora − último_tap)`; ignora se < 30; limita a 40..250; chama `set_tempo(bpm, 0)` (trampolim 0x80028510 → **0xA4B0**). O instante do tap fica em `[obj+0x10]`, flag em `[obj+0x14]`. O relógio em ms é 0x800152E6 → 0x19CED.
3. **Estado global em RAM: 0x2025F370.**
   - `+0x000..+0x24B`: **bloco de Global Settings** (0x24C bytes, versionado; os padrões ficam em 0x80032F88; a função de migração entre versões em 0x7CDC copia campo a campo).
   - `+0x24C..`: **preset atual carregado** (0x3D0 bytes, magic `0xA55A`). Lido do armazenamento em `0x4E0000 + slot*0x400` pela função **0xA620**.
   - **`+0x26C` (u16) = BPM do preset atual** (= campo **+0x20 do preset**). É o tempo efetivo: o tap escreve aqui, e os efeitos leem daqui (0x992E, 0x9A18).
   - **`+0x122` (u16) = BPM global** (padrão 120), salvo nas Global Settings.
   - **`+0x124` (u8) = flag de ligação global/preset** (padrão 0), salvo nas Global Settings.
   - `+0x125` (u8) = Patch VOL (0..100, padrão 80). `+0x91` (u8) = provável fonte de clock (padrão 4).
4. **set_tempo (0xA4B0):** grava `+0x26C = bpm`; se `+0x124 != 0`, copia também para `+0x122` (global), marca `+0xB00` e envia mensagem 0x1001. Por fim, chama 0xA560 e envia a mensagem 6.
5. **Troca de preset (0xA888):** grava o nº do preset em `+0x0C`, envia a mensagem 0x1008, carrega o preset (0xA620), o que **sobrescreve `+0x26C` com o BPM salvo no preset**. Depois, **se `+0x124 != 0`, faz `global(+0x122) = preset(+0x26C)`**, ou seja, o global segue o preset, e não o contrário. É exatamente por isso que o tempo "volta" para 80 ao reentrar no 2A.
6. Tratador de mensagens de parâmetro (0x13080), grupo 0x1001: índice 0 = (+0x11E/+0x120), 1 = Patch VOL (+0x125), 2 = BPM global (+0x122).
7. Setter da flag `+0x124`: função 0x8A1C, exportada pelo veneer 0x8001546C / trampolim 0x80028396. **Não achei chamada direta no app** (nem ponteiro).

## HIPÓTESES

- `+0x124` seria uma opção existente (do tipo "sincronizar BPM global/MIDI clock com o preset"). O BPM global (`+0x122`) alimentaria a bateria/looper/MIDI clock (0x21A1C, 0x120FC). Ainda não sei como o usuário liga essa flag pela UI.
- 0xA560 aplicaria o tempo aos efeitos (o tap o chama com 1, a troca de preset com 0).

## Patch candidato (NÃO aplicado, NÃO gerado como .bin)

Na troca de preset, inverter a cópia, de modo que o **global sobrescreva o preset** quando `+0x124` estiver ligada:

| Offset no arquivo | Original | Novo |
|---|---|---|
| 0xA960 (end. 0xA8E8) | `b4 f8 6c 02` = `ldrh.w r0,[r4,#0x26c]` | `b4 f8 22 01` = `ldrh.w r0,[r4,#0x122]` |
| 0xA964 (end. 0xA8EC) | `a4 f8 22 01` = `strh.w r0,[r4,#0x122]` | `a4 f8 6c 02` = `strh.w r0,[r4,#0x26c]` |

Com a flag ligada, o tap já atualiza o global (item 4), e a troca de preset passaria a impor o global. São 8 bytes, sem código novo nem RAM nova. Pendências: (a) como ligar `+0x124` (via UI ou patch extra); (b) confirmar que 0xA560(0) reaplica o tempo aos efeitos depois da troca; (c) **verificação de integridade do arquivo/bootloader, que ainda não foi verificada**.

---

# Etapa 3: Ghidra 12.2 + JDK 25

Ferramentas: `rea doctor` mostra o Ghidra completo e ok (Java 25, headless, decompilador). Os itens restantes (Hopper, IDA, jadx, Kuna e registro em clientes) são opcionais. O REA só aceita binário cru com `--architecture arm` e **uma** região/endereço. Como não consigo descrever as duas imagens (0x0 e 0x80000000) nem Cortex-M, uso o Ghidra diretamente em modo headless, com scripts Java (`ghidra/scripts`, cópia em `<pasta>\gp200`).
Ajuste necessário: o Ghidra falhava com o erro OSGi "data file must be inside the data dir" no diretório de configurações padrão. Apontei `application.settingsdir` para `<pasta>\ghidra_settings` em `support/launch.properties` (backup em `launch.properties.orig`).
O código decompilado das funções de tempo está em `ghidra/decomp_tempo.c`.

## CONFIRMADO (decompilação)

- **Correção da etapa 2:** 0xA560 não aplica tempo. Ela grava `+0xB01` (provável indicador de "preset editado") e envia a mensagem 0x1011. O tap chama com 1 (preset fica editado) e a troca de preset chama com 0. Renomeei para `set_patch_dirty`.
- O tempo vai para o DSP/UI pela mensagem tipo 6 (`set_tempo`) e, na troca de preset, junto com o preset inteiro (0x6CC/0x6D4, chamadas **depois** do trecho que o patch altera). Portanto, gravar `+0x26C` antes delas faz o DSP receber o BPM global.
- O botão de BPM global na UI (app 0x80016D40) varia o valor de 1 em 1 ou de 5 em 5, limitado a 40..250, e chama `set_global_bpm` (0x87BC).
- **A flag `+0x124` não tem nenhum caminho pela UI.** O setter (0x8A1C) só é alcançável por um trampolim (0x80028396) que ninguém chama, e não há referência a ele em tabelas. O valor só vem da gravação/migração das Global Settings (padrão 0).
- Nenhum CRC32/CRC16/MD5/SHA/AES conhecido (tabelas ou constantes) no arquivo. Nenhuma soma simples (CRC32, Adler, soma 8/16/32, CRC-CCITT) de nenhuma seção bate com o cabeçalho. A "seção B" é só a continuação de uma tabela de floats (não é checksum).

## Patch proposto, versão 2 (sem depender da flag): 14 bytes, NÃO aplicado

| Offset | End. | Original | Novo | Efeito |
|---|---|---|---|---|
| 0xA556 | 0xA4DE | `f0 b1` (`cbz r0`) | `00 bf` (`nop`) | o tap sempre atualiza o BPM global |
| 0x8866 | 0x87EE | `a8 b1` (`cbz r0`) | `00 bf` | mexer no BPM global sempre muda o tempo atual |
| 0xA95E | 0xA8E6 | `d8 b1` (`cbz r0`) | `00 bf` | a troca de preset sempre aplica o global |
| 0xA960 | 0xA8E8 | `b4 f8 6c 02` | `b4 f8 22 01` | `ldrh r0,[r4,#0x122]` (lê o global) |
| 0xA964 | 0xA8EC | `a4 f8 22 01` | `a4 f8 6c 02` | `strh r0,[r4,#0x26c]` (grava no preset atual) |

Hipóteses e riscos deste desenho:
- `+0xB00 = 1` parece marcar "Global Settings precisam ser salvas". Com o patch, cada tap marcaria isso. Se o salvamento na flash for imediato a cada tap, há desgaste da flash. Falta ver quem consome `+0xB00`.
- O BPM salvo em cada preset deixa de valer (fica sempre o global). Se você salvar um preset, ele grava o global no lugar.
- Se o clock externo (MIDI) estiver ativo, `set_tempo` já filtra pela fonte (`+0x91`), e isso não muda.

**Atualização sobre `+0xB00` (CONFIRMADO, 0x9F5C):** é um salvamento com atraso. Uma tarefa periódica (a cada >10 ms) vê `+0xB00`, zera a flag e reinicia um contador. Depois de ~10 ciclos sem nova alteração, ela grava os 0x24C bytes das Global Settings (via 0x28A0). Ou seja, uma sequência de taps gera **uma** gravação, logo depois do último tap. E a troca de preset **original já marca `+0xB00 = 1` sempre**, então o firmware de fábrica já grava as Global Settings a cada troca de preset. O desgaste extra com o patch é pequeno.

## Verificação de integridade no software de PC (CONFIRMADO, GP-200.exe 1.8.1, função 0x485EC0)

O programa (JUCE, 32 bits) lê o cabeçalho do .bin e:
1. exige o magic `FRMW` em 0x00;
2. compara os 16 bytes em 0x10 com o modelo conectado (`GP-200`, `GP-200LT` ou `GP-200JR`);
3. calcula uma **soma de 8 bits de todos os bytes de 0x30 até o fim** (o comprimento vem do campo 0x04 = 0x6275C8) e compara com o **byte 0x0E do cabeçalho**.

Conferido no arquivo original: soma = **0xCD**, e o byte 0x0E = **0xCD**. Ou seja, o campo 0x0C (`0x00CD0000`) contém o checksum. Não há hash criptográfico nem assinatura no lado do PC.

Consequência: um firmware alterado precisa ter o byte 0x0E recalculado. Com o patch v2, a soma passaria a ser **0x87** (calculado só em memória; nenhum arquivo foi gerado).

## HIPÓTESES / NÃO VERIFICADO

- **O bootloader da pedaleira não está neste arquivo** (o .bin traz só a imagem baixa + app + dados). Não dá para saber estaticamente se ele refaz essa soma, usa outra verificação ou não verifica nada. Também não sei como ele se recupera de uma imagem que não funciona (modo de atualização forçado ao ligar segurando alguma tecla?). Esse é o maior risco antes de qualquer gravação.
- Código decompilado da verificação: `ghidra/pc_frmw_check.c`.
