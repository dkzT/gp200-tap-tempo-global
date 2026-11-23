# GP-200 firmware V1.8.2 — notas de engenharia reversa (tap tempo global)

Objetivo: adicionar um tap tempo **global** na Valeton GP-200.
Hoje o tap é **por preset**: o BPM fica salvo dentro de cada preset. Se no preset 2A (80 BPM) eu toco o tap e passo para 100 BPM, saio para o 1A e volto, o 2A volta para 80 BPM.

Arquivo analisado: `GP-200_USB_Firmware_V1.8.2.bin` (site oficial da Valeton, release de 30/04/2026).
Tamanho: 6.452.728 bytes (0x6275F8). Os PDFs que vêm junto são só o histórico de versões (EN/ZH).

> Tudo abaixo vem de análise estática feita em uma sessão com rede restrita. Itens marcados **[inferência]** são hipóteses, não fatos confirmados.

## 1. Contêiner

Cabeçalho de 0x78 bytes, little-endian:

| Offset | Valor | Observação |
|---|---|---|
| 0x00 | `FRMW` | magic |
| 0x04 | 0x006275C8 | tamanho declarado; o arquivo tem 48 bytes `0xFF` a mais |
| 0x08 | 0x00080003 | desconhecido |
| 0x0C | 0x00CD0000 | desconhecido |
| 0x10 | `GP-200` | modelo |
| 0x20 | `01 02 45 58` | desconhecido (`..EX`) |
| 0x30 | 0x20 | desconhecido |
| 0x34..0x4C | 0x3FF978, 0x3FF998, 0x1BFC30, 0x5BF5C8, 0x48000, 0x6075C8, 0x20000 | tabela de seções |
| 0x58 | `V182` | versão |
| 0x60 | 0x00020028 | desconhecido |
| 0x64 | 0x00022291 | desconhecido |
| 0x6C | 0x000422B9 | desconhecido |
| 0x70 | 0x003DD6BD | desconhecido |
| 0x74 | 0x80000000 | provável endereço de carga do aplicativo **[inferência]** |

Seções que fecham exatamente com o tamanho do arquivo **[inferência sobre o significado]**:
- A: `[0x000000, 0x3FF978)` — cabeçalho + loader + UI (código em claro)
- B: `[0x3FF978, 0x3FF998)` — 32 bytes. **Não é hash** (comparei MD5/SHA-1/SHA-256 de várias combinações de seções); parece um padrão repetido de 16 bits.
- C: `[0x3FF998, 0x5BF5C8)` — ~1,8 MB, entropia ~7,0, muitos blocos de 16 bytes repetidos. Parece dado (DSP/tabelas), não código.
- D: `[0x5BF5C8, 0x6075C8)` — 0x48000 bytes, entropia ~6.
- E: `[0x6075C8, 0x6275C8)` — 0x20000 bytes, quase tudo zero. Perto do fim há uma string com um nome de pessoa (omitida aqui).

Entropia por região: `0x170000–0x250000` ≈ 7,98 (comprimido ou criptografado; sem blocos repetidos, então **não** é ECB). Binwalk só gerou falsos positivos de LZMA/zlib nessas regiões.

Não achei nenhum CRC/assinatura. **Isso não prova que não exista**: a verificação pode estar no loader ou no app de atualização USB. Falta checar.

## 2. Processador e loader

- ARM **Cortex-M7**, Thumb-2.
- Tabela de vetores em **0x78** do arquivo: SP = `0x20008000`, reset = `0x4E9`. Handlers de exceção são stubs `b .`.
- Para esse trecho, `endereço = offset_no_arquivo − 0x78`.
- Reset: `cpsid i`, grava VTOR = 0, carrega SP, chama `SystemInit` (endereço `0x19E7C`, offset `0x19EF4`) e depois faz `bx 0x80000001`.
- `SystemInit`: habilita FPU (CPACR `0xE000ED88`), desabilita watchdogs em `0x400B8000` e `0x400D0000`, destrava o RTWDOG em `0x400BC000` com `0xD928C520`, liga caches I/D.
- Esses endereços e a constante de unlock batem com NXP i.MX RT10xx **[inferência]**. `0x80000000` seria SDRAM/XIP externo.

## 3. Código em claro e strings úteis

Região inicial (até ~0x1F0000) é código Thumb legível. O radare2 detectou 11.474 funções com `aap` (heurística de prólogos, não verificado).

Strings (offset no arquivo):

| Offset | String | Significado provável |
|---|---|---|
| 0x1FC0 | `SIGABRT: Abnormal termination` | runtime C |
| 0x277C5 | `SEGGER emWin V61603` | biblioteca gráfica |
| 0x3E969 | `ADC: id(%d) data(%d)` | debug de ADC |
| 0x3EAF1 | `KNOB [%d] %d >%d %s` | debug dos knobs |
| 0x3ED25 | `KEY: type(%d) id(%d) keyVal(%d) data(%d)` | debug de teclas/footswitches |
| 0x3F52F9 | `Patch VOL/BPM` | rótulo de função de footswitch |
| 0x3F5315 | **`TAP`** | rótulo de função de footswitch |

Tabela de rótulos de função em 0x3F5251–0x3F5339, na ordem: `EXP 1-A`, `Press PARA`, `DATA`, `EXP 1-B`, `EXP1 A/B`, `Aux To USB`, `CMD`, `1/2 SPD`, `SAVE`, `EXP 1 CURVE`, `ON/OFF`, `OFF`, `CH`, `BACK`, `BANK`, `OK`, `CANCEL`, `Rec VOL`, `Patch VOL`, `Play VOL`, `Patch VOL/BPM`, `DRUM`, `ON`, `TAP`, `EXP`, `Global EQ`, `TUNER`, `LOOPER`, `Single FS`, `Dual FS`, `GLOBAL SETTINGS`.

Há também tabelas de texto em outros idiomas (ex.: italiano `Tempo di registrazione`, `Sett. Tempo` perto de 0x3F9119/0x3F93C5). Não achei "Tap" em UTF-16.

## 4. Como reproduzir

Fatiar a região de código e abrir como Thumb em endereço 0:

```python
d = open("GP-200_USB_Firmware_V1.8.2.bin", "rb").read()
open("img0.bin", "wb").write(d[0x78:0x3ff978])
```

```
r2 -a arm -b 16 -e asm.cpu=cortex -m 0 img0.bin
> e anal.from=0; e anal.to=0x200000
> aap
```

No Ghidra: linguagem `ARM:LE:32:Cortex`, carregar como binário cru, início do arquivo em 0x78, base 0.

## 5. Becos sem saída (não repetir)

- Referências absolutas de 32 bits às strings usando `addr = off − 0x78`: **nenhuma encontrada**.
- Interseção de deslocamentos possíveis entre várias strings: só ruído, nenhum deslocamento coerente.
- Pares `movw`/`movt` na região de código: só **100**, todos apontando para `0x80xxxxxx`. Sem pico de deslocamento contra as strings.
- Correlação de ponteiros com prólogos de função: no nível do acaso.
- `binwalk`: falsos positivos.

Hipótese de trabalho: o arquivo tem **várias imagens com bases de carga diferentes** (loader em 0, aplicativo em 0x80000000), e os campos 0x60–0x74 do cabeçalho descrevem isso. Ainda não decodificados.

## 6. Próximos passos

1. Decodificar os campos 0x60–0x74 e a tabela 0x34–0x4C: base de carga e tamanho de cada sub-imagem.
2. Com a base certa, achar referências à tabela de rótulos (0x3F5251) e seguir até o tratador de footswitch.
3. Achar onde o BPM do preset é guardado e a conversão BPM → tempo de delay/modulação.
4. Descobrir se o loader ou o app de atualização valida o arquivo (CRC/hash/assinatura).
5. Só então projetar o patch.

## 7. Projeto do patch (a decidir)

Duas semânticas possíveis para "global":
- **A**: o tap sobrescreve o BPM de todos os presets na hora (exige gravar no formato dos presets).
- **B**: uma variável global em RAM (BPM global) que se sobrepõe ao BPM do preset enquanto estiver ativa. Provavelmente menos invasiva, mas precisa de RAM livre e de um gancho no tratador do footswitch e na leitura do BPM.

## 8. Riscos

- Não sei se o aparelho tem bootloader recuperável nem se existe SWD acessível. **Fazer backup do firmware original e confirmar a recuperação antes de gravar qualquer coisa.**
- A análise é estática: nada foi testado no hardware.
- Uso pessoal, no próprio aparelho. Não redistribuir o firmware modificado.

## 9. Ferramentas

- REA 6.10.1 (`npx rea-agents setup`) precisa de um motor: Ghidra **12.2 ou superior** (`GHIDRA_INSTALL_DIR`), Hopper, IDA ou Kuna. A documentação do REA não confirma cobertura de Cortex-M; testar.
- Plano B: radare2 (apt) ou Ghidra diretamente.
- Para a parte de ler código sem símbolos e escrever o patch, vale usar um modelo mais forte (Opus).
