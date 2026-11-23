// GP-200 Tap Tempo Global Patcher
// Aplica o patch de tap tempo global no firmware OFICIAL da Valeton GP-200 V1.8.2.
// Nao contem nenhum trecho do firmware: so os offsets e os bytes do patch.
// Compilar (Windows, sem instalar nada):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:TapGlobalPatcher.exe TapGlobalPatcher.cs
// Uso pela linha de comando (opcional):
//   TapGlobalPatcher.exe <firmware_oficial.bin> <saida.bin>
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace TapGlobal
{
    public class PatchEntry
    {
        public int Offset; public byte[] Original; public byte[] Patched; public string Description;
        public PatchEntry(int off, string orig, string patched, string desc)
        { Offset = off; Original = Hex(orig); Patched = Hex(patched); Description = desc; }
        public static byte[] Hex(string s)
        {
            s = s.Replace(" ", "");
            byte[] b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }
    }

    public static class Patcher
    {
        public const string Version = "1.0.0";
        public const long ExpectedSize = 6452728;
        public const string OriginalSha256 = "c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af";
        public const string PatchedSha256 = "2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e";
        public const int ChecksumOffset = 0x0E;
        public const int ChecksumStart = 0x30;

        public static readonly PatchEntry[] Entries = new PatchEntry[] {
            new PatchEntry(0xA556, "f0b1",     "00bf",     "set_tempo: o tap sempre atualiza o BPM global"),
            new PatchEntry(0x8866, "a8b1",     "00bf",     "set_global_bpm: o knob de BPM global sempre muda o tempo atual"),
            new PatchEntry(0xA95E, "d8b1",     "00bf",     "select_patch: a troca de preset sempre aplica o BPM global"),
            new PatchEntry(0xA960, "b4f86c02", "b4f82201", "select_patch: le o BPM global (+0x122)"),
            new PatchEntry(0xA964, "a4f82201", "a4f86c02", "select_patch: grava no BPM do preset atual (+0x26C)"),
        };

        public static string Sha256Hex(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder();
                foreach (byte x in h) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        public static byte Checksum(byte[] d)
        {
            int s = 0;
            for (int i = ChecksumStart; i < d.Length; i++) s += d[i];
            return (byte)(s & 0xFF);
        }

        // Retorna o firmware modificado ou lanca excecao com mensagem clara. Escreve o relatorio em log.
        public static byte[] Apply(byte[] orig, StringBuilder log)
        {
            if (orig.Length != ExpectedSize)
                throw new Exception("Tamanho inesperado (" + orig.Length + " bytes). Use o firmware oficial GP-200 V1.8.2 (" + ExpectedSize + " bytes).");
            string sha = Sha256Hex(orig);
            if (sha == PatchedSha256)
                throw new Exception("Este arquivo JA esta com o tap tempo global. Nada a fazer.");
            if (sha != OriginalSha256)
                throw new Exception("SHA-256 nao confere com o firmware oficial GP-200 V1.8.2.\nEncontrado: " + sha + "\nEsperado:   " + OriginalSha256 + "\nBaixe novamente o arquivo no site da Valeton.");
            if (Encoding.ASCII.GetString(orig, 0, 4) != "FRMW")
                throw new Exception("Cabecalho FRMW ausente.");
            if (Checksum(orig) != orig[ChecksumOffset])
                throw new Exception("Checksum do original nao confere.");
            log.AppendLine("Original OK: GP-200 V1.8.2, SHA-256 " + sha);

            byte[] d = (byte[])orig.Clone();
            foreach (PatchEntry e in Entries)
            {
                for (int i = 0; i < e.Original.Length; i++)
                    if (d[e.Offset + i] != e.Original[i])
                        throw new Exception(string.Format("Byte inesperado em 0x{0:X}.", e.Offset + i));
                Array.Copy(e.Patched, 0, d, e.Offset, e.Patched.Length);
            }
            d[ChecksumOffset] = Checksum(d);

            // Verificacao independente: compara byte a byte e confere o resultado
            int diffs = 0;
            log.AppendLine();
            log.AppendLine("Bytes alterados (offset: original -> novo):");
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] != orig[i])
                {
                    diffs++;
                    log.AppendLine(string.Format("  0x{0:X6}: {1:x2} -> {2:x2}", i, orig[i], d[i]));
                    bool allowed = (i == ChecksumOffset);
                    foreach (PatchEntry e in Entries)
                        if (i >= e.Offset && i < e.Offset + e.Patched.Length) allowed = true;
                    if (!allowed) throw new Exception(string.Format("Diferenca fora do patch em 0x{0:X} (abortado).", i));
                }
            }
            string newSha = Sha256Hex(d);
            if (newSha != PatchedSha256)
                throw new Exception("SHA-256 do resultado inesperado: " + newSha);
            log.AppendLine();
            log.AppendLine(string.Format("{0} bytes diferentes; {1} bytes identicos ao original.", diffs, d.Length - diffs));
            log.AppendLine(string.Format("Checksum (byte 0x0E): 0x{0:X2} -> 0x{1:X2}", orig[ChecksumOffset], d[ChecksumOffset]));
            log.AppendLine("SHA-256 do resultado: " + newSha + " (confere)");
            log.AppendLine();
            foreach (PatchEntry e in Entries) log.AppendLine(string.Format("  0x{0:X5}  {1}", e.Offset, e.Description));
            return d;
        }
    }

    public class MainForm : Form
    {
        TextBox txtIn, txtOut, txtLog;
        Button btnIn, btnOut, btnGo;

        public MainForm()
        {
            Text = "GP-200 Tap Tempo Global Patcher v" + Patcher.Version;
            Width = 760; Height = 560; StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);

            Label info = new Label();
            info.Text = "Aplica o tap tempo global no firmware OFICIAL da Valeton GP-200 V1.8.2.\n" +
                        "1) Baixe o firmware no site da Valeton  2) Selecione o .bin  3) Gere o arquivo  4) Grave pelo app GP-200.\n" +
                        "Faca backup dos presets antes. Projeto independente, sem vinculo com a Valeton. Use por sua conta e risco.";
            info.SetBounds(12, 10, 720, 60);
            Controls.Add(info);

            Label l1 = new Label(); l1.Text = "Firmware oficial (.bin):"; l1.SetBounds(12, 78, 160, 22); Controls.Add(l1);
            txtIn = new TextBox(); txtIn.SetBounds(175, 76, 460, 24); Controls.Add(txtIn);
            btnIn = new Button(); btnIn.Text = "Procurar..."; btnIn.SetBounds(642, 74, 90, 28); btnIn.Click += OnBrowseIn; Controls.Add(btnIn);

            Label l2 = new Label(); l2.Text = "Salvar como:"; l2.SetBounds(12, 112, 160, 22); Controls.Add(l2);
            txtOut = new TextBox(); txtOut.SetBounds(175, 110, 460, 24); Controls.Add(txtOut);
            btnOut = new Button(); btnOut.Text = "Escolher..."; btnOut.SetBounds(642, 108, 90, 28); btnOut.Click += OnBrowseOut; Controls.Add(btnOut);

            btnGo = new Button(); btnGo.Text = "Gerar firmware com Tap Tempo Global"; btnGo.SetBounds(175, 146, 300, 34); btnGo.Click += OnGo; Controls.Add(btnGo);

            txtLog = new TextBox(); txtLog.Multiline = true; txtLog.ReadOnly = true; txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = new Font("Consolas", 9f); txtLog.SetBounds(12, 192, 720, 310); Controls.Add(txtLog);

            AllowDrop = true;
            DragEnter += delegate(object s, DragEventArgs e) { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += delegate(object s, DragEventArgs e) { string[] f = (string[])e.Data.GetData(DataFormats.FileDrop); if (f.Length > 0) SetInput(f[0]); };
        }

        void SetInput(string path)
        {
            txtIn.Text = path;
            string dir = Path.GetDirectoryName(path);
            txtOut.Text = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "_TAPGLOBAL.bin");
        }

        void OnBrowseIn(object s, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog(); d.Filter = "Firmware (*.bin)|*.bin";
            if (d.ShowDialog() == DialogResult.OK) SetInput(d.FileName);
        }

        void OnBrowseOut(object s, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog(); d.Filter = "Firmware (*.bin)|*.bin"; d.FileName = Path.GetFileName(txtOut.Text);
            if (d.ShowDialog() == DialogResult.OK) txtOut.Text = d.FileName;
        }

        void OnGo(object s, EventArgs e)
        {
            StringBuilder log = new StringBuilder();
            try
            {
                if (!File.Exists(txtIn.Text)) throw new Exception("Selecione o firmware oficial (.bin).");
                if (string.IsNullOrEmpty(txtOut.Text)) throw new Exception("Escolha onde salvar.");
                if (Path.GetFullPath(txtOut.Text) == Path.GetFullPath(txtIn.Text)) throw new Exception("Salve com outro nome: o original nao deve ser sobrescrito.");
                byte[] orig = File.ReadAllBytes(txtIn.Text);
                byte[] patched = Patcher.Apply(orig, log);
                File.WriteAllBytes(txtOut.Text, patched);
                log.AppendLine();
                log.AppendLine("Arquivo gerado: " + txtOut.Text);
                log.AppendLine("Proximo passo: backup dos presets e gravar pelo app GP-200 (Settings > Firmware Update).");
                log.AppendLine("Para voltar ao oficial: segure FS2 + FS3 + knob Para ao ligar (modo Update/Restore) e grave o .bin original.");
                txtLog.Text = log.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
                MessageBox.Show("Firmware gerado e verificado com sucesso.", "Pronto", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                log.AppendLine("ERRO: " + ex.Message);
                txtLog.Text = log.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
                MessageBox.Show(ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2)
            {
                // Modo linha de comando
                StringBuilder log = new StringBuilder();
                try
                {
                    if (Path.GetFullPath(args[0]) == Path.GetFullPath(args[1])) throw new Exception("A saida deve ser outro arquivo.");
                    byte[] patched = Patcher.Apply(File.ReadAllBytes(args[0]), log);
                    File.WriteAllBytes(args[1], patched);
                    log.AppendLine("Gerado: " + args[1]);
                    MessageBox.Show(log.ToString(), "Tap Tempo Global", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(log.ToString() + "ERRO: " + ex.Message, "Tap Tempo Global", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            Application.EnableVisualStyles();
            Application.Run(new MainForm());
            return 0;
        }
    }
}
