# Tap tempo global para a Valeton GP-200 (firmware V1.8.2)

Pesquisa de engenharia reversa (análise estática) e um patch de **11 bytes** que faz o tap tempo da Valeton GP-200 valer para **todos os presets**. Com o patch, você bate o tempo uma vez e troca de preset sem perder o BPM.

> **Status:** testado em uma GP-200 real com o firmware V1.8.2: o BPM é preservado na troca de preset.
> **Aviso:** projeto independente, sem vínculo com a Valeton. Modificar firmware pode afetar a garantia. Use por sua conta e risco.
> Este repositório **não contém** o firmware da Valeton nem binários modificados. O aplicativo e o script aplicam o patch no arquivo oficial que você baixa do site da Valeton.

## English summary

**Global Tap Tempo for the Valeton GP-200 (firmware V1.8.2).** On the stock firmware, the BPM is stored inside each preset, so the tempo you tap is lost whenever you switch presets. This project is the result of static reverse engineering of the firmware. It adds an **11-byte patch** that makes tap tempo **global**: tap once and switch presets freely. The tempo is also kept after a power cycle. Tested on real hardware.

- **Download:** `TapGlobalPatcher.exe` from the [Releases](https://github.com/dkzT/gp200-tap-tempo-global/releases) page. It needs no installation, and the interface is available in English and Portuguese.
- **Use:** download the official GP-200 V1.8.2 firmware from Valeton, drag the `.bin` into the app and click **Generate**. Back up your presets first, then flash the generated file with the official GP-200 app (Settings > Firmware Update).
- **Safety:** the app only accepts the official file (SHA-256 checked) and verifies the result byte by byte. No Valeton firmware is distributed here. The executable is built by GitHub Actions from this source, with a SHA-256 checksum and a build provenance attestation. It is **not code-signed**, so Windows SmartScreen may warn about it. See the verification steps below.
- **Recovery:** hold footswitches 2 and 3 plus the Para knob while powering on to enter the pedal's own *Firmware Update/Restore* mode, then flash the official firmware.
- The delay follows the tempo only when its **Sync** parameter is on.

Independent project, not affiliated with Valeton. Use at your own risk. Licensed under MIT.

---

## O problema

No firmware original, o BPM fica salvo **dentro de cada preset**. Se você bate 100 BPM no preset 2A, vai para o 1A e volta, o 2A volta ao BPM gravado nele (por exemplo, 80).

## A solução (resumo técnico)

O firmware mantém em RAM (`0x2025F370`) as **Global Settings** (`+0x000..+0x24B`, salvas na flash) e o **preset atual** (`+0x24C..`).

| Campo | Significado |
|---|---|
| `+0x26C` (u16) | BPM efetivo = campo `+0x20` do preset carregado |
| `+0x122` (u16) | **BPM global**, que já existe nas Global Settings (padrão 120) |
| `+0x124` (u8) | flag de ligação global/preset (padrão 0, sem opção na UI) |

Na troca de preset, o firmware original carrega o BPM do preset e, se a flag estiver ligada, copia **preset → global**. O patch faz o contrário, sempre: **global → preset**. Além disso, o tap e o knob de BPM global passam a sempre atualizar o global.

| Offset no .bin | Endereço | Original | Novo | Efeito |
|---|---|---|---|---|
| 0x00000E | (cabeçalho) | `cd` | `87` | checksum recalculado |
| 0x00A556 | 0xA4DE | `f0 b1` `cbz` | `00 bf` `nop` | o tap sempre atualiza o BPM global |
| 0x008866 | 0x87EE | `a8 b1` `cbz` | `00 bf` `nop` | o knob de BPM global sempre muda o tempo atual |
| 0x00A95E | 0xA8E6 | `d8 b1` `cbz` | `00 bf` `nop` | a troca de preset sempre aplica o global |
| 0x00A960 | 0xA8E8 | `b4 f8 6c 02` | `b4 f8 22 01` | `ldrh r0,[r4,#0x122]` (lê o global) |
| 0x00A964 | 0xA8EC | `a4 f8 22 01` | `a4 f8 6c 02` | `strh r0,[r4,#0x26c]` (grava no preset atual) |

Na prática, 11 bytes mudam. Os outros 6.452.717 bytes são idênticos (veja `docs/04-verificacao-diff.txt`).

Comportamento resultante:
- O tempo batido vale para todos os presets e **sobrevive a desligar e ligar** (é o BPM global, salvo na flash).
- O knob de BPM em Global Settings controla o tempo de todos os presets.
- O delay só segue o tempo com o parâmetro **Sync ligado** (com Sync desligado, ele usa o tempo fixo em ms).
- O preset **não** fica marcado como editado (sem asterisco) na troca.
- Ao salvar um preset, ele grava o BPM global atual.

## Como aplicar: aplicativo (recomendado)

O **TapGlobalPatcher.exe** (Windows, sem instalação) aplica o patch no firmware oficial que você baixa da Valeton. O aplicativo não contém nenhum trecho do firmware, só os offsets e os bytes da tabela acima.

1. Baixe o firmware oficial **GP-200 V1.8.2** no site da Valeton e extraia o .zip.
2. Abra o `TapGlobalPatcher.exe` (aba *Releases* do GitHub) e arraste o `GP-200_USB_Firmware_V1.8.2.bin` para a janela, ou clique em **Procurar...**.
3. Clique em **Gerar firmware com Tap Tempo Global**. O aplicativo:
   - recusa qualquer arquivo que não seja o oficial (confere tamanho, SHA-256, `FRMW` e checksum);
   - aplica o patch, recalcula o checksum e compara o resultado byte a byte com o original;
   - só salva se o SHA-256 do resultado for exatamente `2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e`;
   - nunca sobrescreve o arquivo original.
4. **Faça backup dos seus presets** pelo app GP-200 (apenas por precaução).
5. Grave pelo app oficial: Settings > Firmware Update > selecione o `_TAPGLOBAL.bin` > Update Now.

Também funciona pela linha de comando: `TapGlobalPatcher.exe <oficial.bin> <saida.bin>`.

**Compilar você mesmo** (o compilador C# já vem no Windows, não precisa instalar nada): rode `app\build.bat`. O executável sai em `dist\TapGlobalPatcher.exe`. O código-fonte está em `app/` (`Patcher.cs` = lógica do patch, `MainForm.cs` = interface, `tools/MakeIcon.cs` = gerador do ícone).

### Aviso do Windows/Edge ao baixar

O executável **não é assinado digitalmente**. Por isso o SmartScreen do Windows/Edge pode mostrar "não é baixado com frequência" ou "o Windows protegeu o computador". Esse é um aviso de **reputação** (arquivo novo, de editor desconhecido). Ele não indica vírus.

- **Edge:** na lista de downloads, clique em "..." > **Manter** > **Mostrar mais** > **Manter mesmo assim**.
- **Windows (ao abrir):** clique em **Mais informações** > **Executar assim mesmo**.

### Como verificar que o .exe é seguro antes de abrir

Faça um ou mais destes testes. Todos funcionam **sem executar** o programa.

1. **Conferir o hash (SHA-256)** com o publicado na release (`SHA256SUMS.txt` e notas da release). No PowerShell, na pasta do download:
   ```powershell
   Get-FileHash .\TapGlobalPatcher.exe
   ```
   O valor tem que ser **idêntico** ao publicado. Se mudar um único caractere, o arquivo foi alterado. Não use.
2. **Conferir que o GitHub compilou o arquivo a partir deste código.** Os `.exe` das releases são gerados pelo GitHub Actions (`.github/workflows/build.yml`), com um **atestado de origem**:
   - Pelo navegador: aba **Actions** do repositório > execução da versão > o resumo mostra o SHA-256 e o atestado. A lista de atestados fica em https://github.com/dkzT/gp200-tap-tempo-global/attestations
   - Pelo GitHub CLI: `gh attestation verify TapGlobalPatcher.exe --repo dkzT/gp200-tap-tempo-global` (precisa terminar com "Verification succeeded").
3. **Antivírus local:** clique com o botão direito no arquivo > **Verificar com o Microsoft Defender** (no Windows 11: "Mostrar mais opções" primeiro).
4. **VirusTotal (dezenas de antivírus):** em https://www.virustotal.com, aba **Search**, cole o SHA-256 para ver se o arquivo já foi analisado sem precisar enviá-lo. Ou envie o arquivo na aba **File**.
5. **Compilar você mesmo:** baixe o código (botão **Code** > Download ZIP), leia `app/*.cs` e rode `app\build.bat`. Não precisa instalar nada. O seu `.exe` terá outro hash, porque o compilador grava data e identificadores, mas o comportamento é o mesmo.

O programa **não acessa a internet** e não coleta dados. Ele só lê o firmware escolhido e grava o arquivo novo onde você indicar.

## Como aplicar: script Python (alternativa)

Pré-requisitos: Python 3 e `pip install capstone` (usado só para mostrar a desmontagem antes/depois).

1. Baixe o firmware oficial **GP-200 V1.8.2** no site da Valeton e confira o SHA-256:
   `c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af`
2. Coloque o arquivo em `original/GP-200_USB_Firmware_V1.8.2.bin` (dentro deste repositório).
3. Rode:
   ```
   python scripts/make_patch.py
   ```
   O script recusa um arquivo diferente do esperado, aplica o patch, recalcula o checksum e gera `work/GP-200_V1.8.2_TAPGLOBAL_REVISAO.bin`. Depois ele relê os dois arquivos e lista **cada byte diferente**.
   SHA-256 esperado do resultado: `2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e`
4. **Faça backup dos seus presets** pelo app GP-200 (a atualização apaga todos).
5. Grave pelo app oficial: Settings > Firmware Update > selecione o .bin > Update Now.

## Como voltar ao firmware oficial (recuperação)

A atualização é gravada por um **bootloader residente**, que não faz parte do .bin e não é substituído. Mesmo que o firmware não inicie:

1. Desligue a fonte e mantenha o USB ligado ao computador.
2. Segure os **footswitches 2 e 3** ("+" e "CTRL") **e o knob Para**, ligue a fonte e aperte o botão liga/desliga.
3. Aparece a tela **"Firmware Update/Restore"**. Grave o .bin oficial pelo app.
4. Para sair sem gravar, basta reiniciar ("Restart to exit").

Procedimento conferido na prática (fonte: artigo de suporte da Sweetwater sobre atualização da GP-200).

## Como chegamos aqui (passo a passo)

Detalhes completos em `docs/`.

1. **Contêiner** (`docs/01`, `docs/02`): cabeçalho `FRMW` de 0x78 bytes. O app principal começa no offset **0x22309** e roda em **0x80000000** (`addr = 0x80000000 + off − 0x22309`). Achamos isso porque o `__main` do compilador ARM/Keil (`bl __scatterload`) está nesse offset e porque a paridade das instruções Thumb muda nesse ponto. A "imagem baixa" (`off − 0x78`, endereço 0) tem a lógica do sistema. O app chama a imagem baixa por veneers `movw/movt ip; bx ip`.
2. **Dados:** a região de alta entropia (0x16C600–0x250000) são GIFs 480×272 (animação de boot), e não código criptografado.
3. **Tap:** busca pela constante 60000 (`movw #0xea60`) levou ao cálculo `BPM = 60000/intervalo` (limitado a 40..250) no app, que chama `set_tempo` (0xA4B0) na imagem baixa.
4. **Estado e presets:** decompilação no Ghidra de `set_tempo`, `select_patch` (0xA888), `load_patch` (0xA620), `set_global_bpm` (0x87BC) e da migração das Global Settings (0x7CDC). Confirmou os campos da tabela acima e que a flag `+0x124` não tem caminho pela interface.
5. **Gravação na flash:** `+0xB00` agenda uma gravação das Global Settings com atraso (0x9F5C). Uma sequência de taps gera uma gravação, e o firmware original já grava a cada troca de preset.
6. **Integridade:** decompilando o `GP-200.exe` (JUCE, 32 bits), o app exige `FRMW`, o modelo (`GP-200`) e uma **soma de 8 bits dos bytes 0x30..fim igual ao byte 0x0E** do cabeçalho. Não há hash nem assinatura.
7. **Protocolo de atualização:** SysEx MIDI (`F0 7E 48 …`) pela conexão USB. O PC separa o .bin em 4 seções e envia blocos com ACK.
8. **Recuperação:** a tela "Firmware Update/Restore" não existe no .bin, então é do bootloader. Acesso manual pelos botões (seção acima), testado no aparelho antes de gravar.
9. **Verificação:** o arquivo gerado foi comparado byte a byte com o original (11 bytes diferentes, todos listados) e depois gravado e testado no aparelho.

## Ferramentas usadas

- Python 3.12 + [capstone](https://www.capstone-engine.org/) (varredura e desmontagem Thumb-2): `scripts/`
- Ghidra 12.2 (headless, JDK 25) com scripts Java próprios: `ghidra_scripts/`
  - `SetupGP200.java`: monta as duas imagens (baixa em 0x0, app em 0x80000000) + RAM e nomeia funções.
  - `DecompileList.java`, `FindImm.java`, `FindStrRefs.java`: decompilação e busca de referências.
- O REA (rea.tools) foi instalado, mas o modo de binário cru dele aceita só uma região e ARM genérico. Por isso usamos o Ghidra diretamente.

Para montar o projeto no Ghidra: separe `low_0x0.bin = fw[0x78:0x22309]` e `app_0x80000000.bin = fw[0x22309:0x3FF978]`, importe `low` com `-loader BinaryLoader -processor ARM:LE:32:Cortex` e use `SetupGP200.java` como pre-script, passando o caminho do `app`. Use um caminho **sem espaços** e, se o Ghidra der o erro OSGi "data file must be inside the data dir", defina `-Dapplication.settingsdir=` em `support/launch.properties`.

## Scripts

| Script | Função |
|---|---|
| `make_patch.py` | gera e verifica o firmware modificado |
| `fwlib.py` | mapeamento endereço↔offset e desmontagem |
| `index_code.py`, `q1.py` | indexa literais/movw/bl do app; busca constantes |
| `fn.py`, `da.py` | desmonta a função que contém um endereço |
| `callers.py` | acha chamadores (bl/b.w/veneers) |
| `scan_field.py` | acha acessos a um deslocamento de campo (ex.: `#0x26c]`) |
| `entropy.py`, `find_base.py`, `anchor_base.py` | perfil de entropia e testes de base de carga |
| `find_sysex.py`, `pdftext.py` | buscas auxiliares |

Os scripts esperam o firmware em `work/fw.bin` (cópia do original) e rodam a partir da raiz do repositório.

## Licença

Código e documentação sob a licença [MIT](LICENSE). A licença **não** cobre o firmware da Valeton, que não está neste repositório e pertence ao seu dono.
