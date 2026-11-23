# Comparação: firmware original × cópia modificada (tap tempo global)

Arquivo para revisão (NÃO gravar ainda): `work/GP-200_V1.8.2_TAPGLOBAL_REVISAO.bin`
Gerado e verificado por `scripts/make_patch.py` (saída completa em `work/VERIFICACAO_DIFF.txt`).

## Resultado da verificação (CONFIRMADO)

- Tamanho igual: 6.452.728 bytes nos dois.
- **Apenas 11 bytes diferentes** e todos dentro da lista do patch. Os outros **6.452.717 bytes são idênticos**.
- Checksum que o software de PC confere: original 0xCD = byte 0x0E; modificado 0x87 = byte 0x0E (válido).
- SHA-256 original: `c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af`
- SHA-256 modificado: `2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e`

| Offset | Antes | Depois | Onde |
|---|---|---|---|
| 0x00000E | cd | 87 | checksum do cabeçalho |
| 0x008866–67 | a8 b1 | 00 bf | `set_global_bpm` (botão de BPM global) |
| 0x00A556–57 | f0 b1 | 00 bf | `set_tempo` (tap) |
| 0x00A95E–5F | d8 b1 | 00 bf | `select_patch` (troca de preset) |
| 0x00A962–63 | 6c 02 | 22 01 | `select_patch` |
| 0x00A966–67 | 22 01 | 6c 02 | `select_patch` |

Desmontagem antes/depois:
- 0xA4DE `cbz r0,#0xa51e` → `nop`: o tap sempre atualiza o BPM global.
- 0x87EE `cbz r0,#0x881c` → `nop`: o botão de BPM global sempre muda o tempo atual.
- 0xA8E6 `cbz r0,#0xa920` → `nop`, 0xA8E8 `ldrh.w r0,[r4,#0x26c]` → `ldrh.w r0,[r4,#0x122]`, 0xA8EC `strh.w r0,[r4,#0x122]` → `strh.w r0,[r4,#0x26c]`: na troca de preset, o tempo atual recebe o BPM global.

O que **não** foi tocado: cabeçalho (exceto o checksum), app/UI em 0x80000000, imagens, dados do DSP, código de USB/comunicação, presets. Todas as mudanças ficam dentro de 3 funções de tempo da imagem baixa.

Ordem de boot (CONFIRMADO, função 0x1D93E): as Global Settings são carregadas **antes** da seleção do preset inicial. O BPM global já é válido quando o código alterado roda no boot (padrão 120; a UI limita a 40..250).

## Recuperação se algo der errado

CONFIRMADO:
- O firmware (app e imagem baixa) **não** contém o código que recebe e valida o arquivo de atualização: não há leitura de `FRMW`, nem telas de "atualizando". Quem aceita o .bin é outro código que **não está no arquivo** (bootloader residente), e a atualização não o sobrescreve.
- O software de PC só aceita arquivos com `FRMW`, modelo `GP-200` e checksum correto. Ele avisa que **a atualização apaga todos os presets do usuário**. Faça backup dos presets antes de qualquer atualização, mesmo com o firmware oficial.

HIPÓTESE (não dá para confirmar só com o .bin):
- Se o firmware modificado travar no boot, o bootloader continua lá. Mas não sei se o software de PC consegue falar com ele sozinho ou se primeiro pede ao firmware em execução para reiniciar em modo de atualização. No segundo caso, seria preciso entrar no modo de atualização à mão (combinação de tecla ao ligar), e isso não está documentado no que temos.

Próxima análise sugerida: rastrear no GP-200.exe qual comando inicia a atualização e, no firmware, quem trata esse comando. Isso diz se a recuperação depende do firmware estar funcionando.

---

# Rastreio da atualização no software de PC (23/11, Ghidra no GP-200.exe 1.8.1)

## CONFIRMADO

1. **Toda a atualização passa pela conexão MIDI USB da pedaleira** (WinMM `midiOutLongMsg`/`midiIn*`, mensagens SysEx `F0 7E 48 …`). O software não usa HID, WinUSB nem libusb para isso.
2. Fluxo (máquina de estados em 0x5C2830; código em `ghidra/pc_update_protocolo.c`):
   - O PC confere o arquivo (`FRMW`, modelo, soma de 8 bits) e **ele mesmo separa as 4 seções** pela tabela do cabeçalho (0x34–0x4C).
   - Envia uma mensagem de início e espera até 10 s por uma resposta de identificação da pedaleira (≥ 0x27 bytes).
   - Para cada seção, envia `7E 48 7F <seção>` e depois os blocos, esperando um ACK (`…7F`) por bloco. No fim de cada seção, envia `7E 48 05 7B`. Depois da última, lê o resultado.
   - Uma mensagem `F0 7E 48 06 F7` vinda da pedaleira aborta a atualização com erro.
   - **Não existe etapa de "reiniciar em modo bootloader e reconectar"**: é uma sessão contínua na mesma conexão MIDI.
3. **O descritor USB da pedaleira está dentro do firmware que é substituído** (VID 0x84EF, PID 0x002A, strings "Valeton"/"GP-200", interface de áudio, no offset 0x3ED845 do .bin). Ou seja, a porta USB/MIDI que o software de PC usa para atualizar é criada pelo próprio firmware em execução.
4. O .bin não tem IVT/cabeçalho de boot do i.MX RT. Logo, existe um bootloader residente na flash que carrega esta imagem, e ele não faz parte do arquivo.

## O QUE ISSO SIGNIFICA PARA A RECUPERAÇÃO

- **Se o firmware modificado iniciar normalmente até o USB funcionar** (o que acontece antes de qualquer tap ou troca de preset feita por você), o software de PC consegue regravar o firmware oficial do jeito de sempre.
- **Se o firmware modificado travar antes de o USB subir**, o software de PC **não** consegue falar com a pedaleira, porque não há modo alternativo no fluxo do PC. Nesse caso, a recuperação dependeria de:
  (a) um modo de emergência do bootloader residente (ex.: ligar segurando um botão), que **não é visível** nos arquivos que temos (HIPÓTESE: pode ou não existir); ou
  (b) gravação por hardware (SWD/JTAG na placa, com um programador), que exige abrir a pedaleira.

## Avaliação do risco do patch (HIPÓTESE fundamentada)

- O código alterado só roda em três situações: tap, botão de BPM global e seleção de preset. No boot, a seleção do preset inicial roda **depois** de carregar as Global Settings. A troca de instruções é do mesmo tamanho e com os mesmos registradores, então não muda o fluxo do programa, a pilha nem o uso de memória.
- O valor que passa a ser usado (BPM global) já é um valor que o firmware original grava e lê normalmente (padrão 120, limitado a 40..250 pela interface).
- Não encontrei nenhum caminho pelo qual essas mudanças afetem o USB. Ainda assim, "não encontrei" não é prova. O risco não é zero.

## Recomendações antes de gravar

1. Perguntar ao suporte da Valeton se existe **modo de recuperação/atualização forçada** da GP-200 (combinação de botões ao ligar). Se existir, o risco cai muito.
2. Fazer backup de todos os presets e das configurações (a atualização apaga os presets).
3. Antes do firmware modificado, gravar uma vez o **1.8.2 oficial** e anotar o que aparece na tela durante a atualização (se surge uma tela própria de "update", é sinal de um modo de atualização separado).

## Atualização com a foto da tela de atualização (enviada pelo Daniel)

A tela durante a atualização mostra "Firmware Update/Restore", "Updating/restoring in progress", "Please don't shut down!" e "Restart to exit", em inglês e chinês.

CONFIRMADO: **nenhum desses textos existe no .bin** (procurei em ASCII, UTF-8 e UTF-16). As únicas imagens do .bin são duas GIFs de animação de boot (`work/gifs`). O firmware normal usa outra frase ("Please do not shut down!"), com redação diferente.

Conclusão: essa tela é desenhada por **outro programa, que não é substituído pela atualização**: o bootloader residente, com display próprio e um modo chamado **"Update/Restore"**. Isso muda a avaliação de risco para melhor:
- A gravação em si é feita por esse modo separado, e não pelo firmware que vamos alterar.
- "Restore" e "Restart to exit" indicam um modo de recuperação em que se entra e do qual se sai reiniciando.

HIPÓTESE que ainda falta fechar: **como entrar nesse modo sem o firmware funcionando** (provavelmente uma combinação de botões ao ligar). Essa é a pergunta certa para o suporte da Valeton.

## Modo de recuperação manual (fonte: Sweetwater, artigo #1791816, atualizado em 08/09/2026)

Seção "If the Update Won't Work" do artigo (resumo):
1. Feche o app.
2. Desligue a fonte de alimentação e mantenha o cabo USB ligado ao computador.
3. Segure os **footswitches 2 e 3** (os marcados com "+" e "CTRL") **e o knob Para**, ligue a fonte e, ainda segurando tudo, aperte o botão liga/desliga. A pedaleira entra em **modo Firmware Update**.
4. Abra o app, vá em Settings > Firmware Update, escolha o .bin e repita a instalação.

O artigo diz que esse procedimento também aparece na tela Firmware Update do app (não confirmei; no .exe não há esse texto em ASCII, então provavelmente está numa imagem).

Isso fecha a lacuna da recuperação: o modo Update/Restore é do bootloader (a tela que o Daniel fotografou não está no .bin) e **pode ser ativado por botões ao ligar, sem depender do firmware instalado**. Esta parte é CONFIRMADA por fonte externa e coerente com a análise, mas não testada por nós.

Teste sugerido, sem risco de gravação: com o firmware oficial atual, entrar no modo pela combinação acima, ver a tela "Firmware Update/Restore" e sair desligando e ligando ("Restart to exit"), sem enviar nenhum arquivo. Assim você confirma a saída de emergência antes de qualquer firmware modificado.

## Comparação com o produto "Tap Tempo Global" (Pactcher, tapglobal.lovable.app), lido em 23/11

O que o site afirma (não verificável por nós, é produto fechado):
- Funciona na GP-200 V1.8.2 e na GP-200LT V1.8.0; aplica a mudança no .bin oficial que o usuário baixa.
- "Guarda o tempo que está tocando e reaplica a cada troca de preset, usando a rotina oficial de tempo do firmware".
- O delay só segue o tap com **Sync ligado** (com Sync desligado, ele usa o tempo fixo em ms do preset).
- Efeito colateral declarado: **o asterisco de "editado" não aparece ao trocar de preset**.
- "O modo de atualização nunca é modificado; dá para regravar o oficial."

Como isso se compara à nossa análise:
- **Coerente:** o asterisco é exatamente o `+0xB01` que identificamos (função 0xA560). Chamar a rotina oficial `set_tempo` marca o preset como editado, e por isso eles precisaram suprimir isso. No nosso patch, o tempo é gravado antes de `set_patch_dirty(0)`, então o preset também não fica marcado como editado, sem mudança extra.
- **Coerente:** "modo de atualização nunca é modificado" bate com o bootloader residente fora do .bin.
- **Diferença de desenho:** pela descrição, eles mantêm o "tempo que está tocando". Nós usamos o **BPM global que já existe nas Global Settings** (`+0x122`): o tap e o knob de BPM global gravam nele e toda troca de preset o aplica. Efeito extra no nosso: o tempo **sobrevive a desligar e ligar** (é salvo na flash, com o atraso que vimos). O deles, pela descrição, não é possível saber.
- **Por que o tempo chega ao delay no nosso desenho:** no firmware original, a troca de preset também não envia a mensagem de tempo separada. O BPM do preset novo vai para o DSP junto com o preset inteiro (0x6CC/0x6D4). Como escrevemos o BPM global no mesmo campo antes desse envio, o DSP recebe o nosso valor pelo mesmo caminho que o original já usa. (Inferência forte a partir do código, não testada no aparelho.)
- **Vale para os dois:** o delay precisa estar com **Sync ligado** em cada preset para seguir o tempo.

## Teste do modo de recuperação (Daniel, 23/11, firmware oficial)

CONFIRMADO NO APARELHO: a combinação FS2 + FS3 + knob Para ao ligar levou a pedaleira à tela "Firmware Update/Restore" (foto enviada), sem gravar nada. A saída de emergência existe e funciona independentemente do firmware instalado.

## Resultado no aparelho (Daniel, 23/11 20:17)

O firmware modificado (`work/GP-200_V1.8.2_TAPGLOBAL_REVISAO.bin`, SHA-256 2c60f211…) foi gravado pelo app oficial e a pedaleira iniciou normalmente. **O BPM é preservado na troca de preset** (CONFIRMADO no aparelho).
