// Núcleo do patch: valida o firmware oficial, aplica os bytes e verifica o resultado.
// Não contém nenhum trecho do firmware da Valeton: só offsets e bytes do patch.
using System;
using System.Security.Cryptography;
using System.Text;

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

    public enum FirmwareKind { Official, AlreadyPatched, WrongSize, Unknown }

    public static class Patcher
    {
        public const string Version = "1.1.0";
        public const string FirmwareVersion = "V1.8.2";
        public const long ExpectedSize = 6452728;
        public const string OriginalSha256 = "c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af";
        public const string PatchedSha256 = "2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e";
        public const int ChecksumOffset = 0x0E;
        public const int ChecksumStart = 0x30;

        public static readonly PatchEntry[] Entries = new PatchEntry[] {
            new PatchEntry(0xA556, "f0b1",     "00bf",     "set_tempo: o tap sempre atualiza o BPM global"),
            new PatchEntry(0x8866, "a8b1",     "00bf",     "set_global_bpm: o knob de BPM global sempre muda o tempo atual"),
            new PatchEntry(0xA95E, "d8b1",     "00bf",     "select_patch: a troca de preset sempre aplica o BPM global"),
            new PatchEntry(0xA960, "b4f86c02", "b4f82201", "select_patch: lê o BPM global (+0x122)"),
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

        public static FirmwareKind Identify(byte[] d, out string sha)
        {
            sha = Sha256Hex(d);
            if (d.Length != ExpectedSize) return FirmwareKind.WrongSize;
            if (sha == OriginalSha256) return FirmwareKind.Official;
            if (sha == PatchedSha256) return FirmwareKind.AlreadyPatched;
            return FirmwareKind.Unknown;
        }

        // Retorna o firmware modificado ou lança exceção com mensagem clara. Escreve o relatório em log.
        public static byte[] Apply(byte[] orig, StringBuilder log)
        {
            string sha;
            FirmwareKind k = Identify(orig, out sha);
            if (k == FirmwareKind.WrongSize)
                throw new Exception("Tamanho inesperado (" + orig.Length + " bytes). Use o firmware oficial GP-200 " + FirmwareVersion + ".");
            if (k == FirmwareKind.AlreadyPatched)
                throw new Exception("Este arquivo já está com o Tap Tempo Global. Nada a fazer.");
            if (k != FirmwareKind.Official)
                throw new Exception("Este não é o firmware oficial GP-200 " + FirmwareVersion + " (SHA-256 não confere). Baixe novamente no site da Valeton.");
            if (Encoding.ASCII.GetString(orig, 0, 4) != "FRMW")
                throw new Exception("Cabeçalho FRMW ausente.");
            if (Checksum(orig) != orig[ChecksumOffset])
                throw new Exception("Checksum do original não confere.");
            log.AppendLine("Original OK: GP-200 " + FirmwareVersion + ", SHA-256 " + sha);

            byte[] d = (byte[])orig.Clone();
            foreach (PatchEntry e in Entries)
            {
                for (int i = 0; i < e.Original.Length; i++)
                    if (d[e.Offset + i] != e.Original[i])
                        throw new Exception(string.Format("Byte inesperado em 0x{0:X}.", e.Offset + i));
                Array.Copy(e.Patched, 0, d, e.Offset, e.Patched.Length);
            }
            d[ChecksumOffset] = Checksum(d);

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
                    if (!allowed) throw new Exception(string.Format("Diferença fora do patch em 0x{0:X} (abortado).", i));
                }
            }
            string newSha = Sha256Hex(d);
            if (newSha != PatchedSha256)
                throw new Exception("SHA-256 do resultado inesperado: " + newSha);
            log.AppendLine();
            log.AppendLine(string.Format("{0} bytes diferentes; {1} bytes idênticos ao original.", diffs, d.Length - diffs));
            log.AppendLine(string.Format("Checksum (byte 0x0E): 0x{0:X2} -> 0x{1:X2}", orig[ChecksumOffset], d[ChecksumOffset]));
            log.AppendLine("SHA-256 do resultado: " + newSha + " (confere)");
            log.AppendLine();
            foreach (PatchEntry e in Entries) log.AppendLine(string.Format("  0x{0:X5}  {1}", e.Offset, e.Description));
            return d;
        }
    }
}
