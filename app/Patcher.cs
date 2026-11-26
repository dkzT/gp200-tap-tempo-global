// Núcleo do patch: valida o firmware oficial, aplica os bytes e verifica o resultado.
// Não contém nenhum trecho do firmware da Valeton: só offsets e bytes do patch.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace TapGlobal
{
    public class PatchEntry
    {
        public int Offset; public byte[] Original; public byte[] Patched; string descPt, descEn;
        public string Description { get { return L.T(descPt, descEn); } }
        public PatchEntry(int off, string orig, string patched, string pt, string en)
        { Offset = off; Original = Hex(orig); Patched = Hex(patched); descPt = pt; descEn = en; }
        public static byte[] Hex(string s)
        {
            s = s.Replace(" ", "");
            byte[] b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }
    }

    // Idioma da interface: português se o Windows estiver em português, senão inglês
    public static class L
    {
        public static bool En = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "pt";
        public static string T(string pt, string en) { return En ? en : pt; }
    }

    public enum FirmwareKind { Official, AlreadyPatched, OldPatched, WrongSize, Unknown }

    public static class Patcher
    {
        public const string Version = "1.3.0";
        public const string FirmwareVersion = "V1.8.2";
        public const long ExpectedSize = 6452728;
        public const string OriginalSha256 = "c368618773097b07c556c951eb10792f9d57be929323366648eb6e083d0d23af";
        // Resultados esperados (o app só salva se o resultado for exatamente um destes)
        public const string Sha_Tap = "c90f5ee191237df8645d216f6833435ed7914967142ac2bdcba648a8fcc62793";
        public const string Sha_TapFast = "adececced3511ba9866752885c0cfdd49ddbd8af8ff863b8b3e8b9aeb03011b8";
        // Gerado pelas versões 1.0 a 1.2 do app (tap global sem as melhorias da 1.3)
        public const string Sha_OldV1 = "2c60f211b2ab76d57f385205b5674bd44dac41b133d8550b1a4e8711c882ff8e";
        public const int ChecksumOffset = 0x0E;
        public const int ChecksumStart = 0x30;

        // Tap tempo global (sempre aplicado)
        public static readonly PatchEntry[] TapEntries = new PatchEntry[] {
            new PatchEntry(0xA556, "f0b1",     "00bf",     "set_tempo: o tap sempre atualiza o BPM global", "set_tempo: tapping always updates the global BPM"),
            new PatchEntry(0x8866, "a8b1",     "00bf",     "set_global_bpm: o knob de BPM global sempre muda o tempo atual", "set_global_bpm: the global BPM knob always changes the current tempo"),
            new PatchEntry(0xA95E, "d8b1",     "00bf",     "select_patch: a troca de preset sempre aplica o BPM global", "select_patch: switching presets always applies the global BPM"),
            new PatchEntry(0xA960, "b4f86c02", "b4f82201", "select_patch: lê o BPM global (+0x122)", "select_patch: reads the global BPM (+0x122)"),
            new PatchEntry(0xA964, "a4f82201", "a4f86c02", "select_patch: grava no BPM do preset atual (+0x26C)", "select_patch: writes it to the current preset BPM (+0x26C)"),
            new PatchEntry(0xA54E, "00f043f8", "00bf00bf", "set_tempo: o tap não marca o preset como editado (sem asterisco)", "set_tempo: tapping no longer marks the preset as edited (no asterisk)"),
            new PatchEntry(0x886E, "01f0b3fe", "00bf00bf", "set_global_bpm: o knob de BPM global não marca o preset como editado", "set_global_bpm: the global BPM knob no longer marks the preset as edited"),
        };

        // Troca rápida de preset (opcional): passo da rampa de fade-in no DSP, ±0,0002 -> ±0,0025 (113 ms -> 9 ms)
        public static readonly PatchEntry[] FastEntries = new PatchEntry[] {
            new PatchEntry(0x5BF5F0, "17b75139 17b751b9", "0ad7233b 0ad723bb", "DSP: fade-in após troca de preset de 113 ms para 9 ms", "DSP: fade-in after patch change from 113 ms to 9 ms"),
        };

        public static PatchEntry[] Entries(bool fast)
        {
            List<PatchEntry> l = new List<PatchEntry>(TapEntries);
            if (fast) l.AddRange(FastEntries);
            return l.ToArray();
        }

        public static string ExpectedSha(bool fast) { return fast ? Sha_TapFast : Sha_Tap; }

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
            if (sha == Sha_Tap || sha == Sha_TapFast) return FirmwareKind.AlreadyPatched;
            if (sha == Sha_OldV1) return FirmwareKind.OldPatched;
            return FirmwareKind.Unknown;
        }

        // Retorna o firmware modificado ou lança exceção com mensagem clara. Escreve o relatório em log.
        public static byte[] Apply(byte[] orig, bool fast, StringBuilder log)
        {
            string sha;
            FirmwareKind k = Identify(orig, out sha);
            if (k == FirmwareKind.WrongSize)
                throw new Exception(L.T("Tamanho inesperado (", "Unexpected size (") + orig.Length + L.T(" bytes). Use o firmware oficial GP-200 ", " bytes). Use the official GP-200 firmware ") + FirmwareVersion + ".");
            if (k == FirmwareKind.AlreadyPatched || k == FirmwareKind.OldPatched)
                throw new Exception(L.T("Este arquivo já está modificado. Use o firmware oficial da Valeton como entrada.", "This file is already modified. Use the official Valeton firmware as input."));
            if (k != FirmwareKind.Official)
                throw new Exception(L.T("Este não é o firmware oficial GP-200 ", "This is not the official GP-200 firmware ") + FirmwareVersion + L.T(" (SHA-256 não confere). Baixe novamente no site da Valeton.", " (SHA-256 does not match). Download it again from the Valeton website."));
            if (Encoding.ASCII.GetString(orig, 0, 4) != "FRMW")
                throw new Exception(L.T("Cabeçalho FRMW ausente.", "FRMW header missing."));
            if (Checksum(orig) != orig[ChecksumOffset])
                throw new Exception(L.T("Checksum do original não confere.", "Original checksum does not match."));
            log.AppendLine(L.T("Original OK: GP-200 ", "Original OK: GP-200 ") + FirmwareVersion + ", SHA-256 " + sha);
            log.AppendLine(L.T("Opções: tap tempo global", "Options: global tap tempo") + (fast ? L.T(" + troca rápida de preset", " + fast patch change") : ""));

            PatchEntry[] entries = Entries(fast);
            byte[] d = (byte[])orig.Clone();
            foreach (PatchEntry e in entries)
            {
                for (int i = 0; i < e.Original.Length; i++)
                    if (d[e.Offset + i] != e.Original[i])
                        throw new Exception(string.Format(L.T("Byte inesperado em 0x{0:X}.", "Unexpected byte at 0x{0:X}."), e.Offset + i));
                Array.Copy(e.Patched, 0, d, e.Offset, e.Patched.Length);
            }
            d[ChecksumOffset] = Checksum(d);

            int diffs = 0;
            log.AppendLine();
            log.AppendLine(L.T("Bytes alterados (offset: original -> novo):", "Changed bytes (offset: original -> new):"));
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] != orig[i])
                {
                    diffs++;
                    log.AppendLine(string.Format("  0x{0:X6}: {1:x2} -> {2:x2}", i, orig[i], d[i]));
                    bool allowed = (i == ChecksumOffset);
                    foreach (PatchEntry e in entries)
                        if (i >= e.Offset && i < e.Offset + e.Patched.Length) allowed = true;
                    if (!allowed) throw new Exception(string.Format(L.T("Diferença fora do patch em 0x{0:X} (abortado).", "Difference outside the patch at 0x{0:X} (aborted)."), i));
                }
            }
            string newSha = Sha256Hex(d);
            if (newSha != ExpectedSha(fast))
                throw new Exception(L.T("SHA-256 do resultado inesperado: ", "Unexpected result SHA-256: ") + newSha);
            log.AppendLine();
            log.AppendLine(string.Format(L.T("{0} bytes diferentes; {1} bytes idênticos ao original.", "{0} bytes differ; {1} bytes identical to the original."), diffs, d.Length - diffs));
            log.AppendLine(string.Format("Checksum (byte 0x0E): 0x{0:X2} -> 0x{1:X2}", orig[ChecksumOffset], d[ChecksumOffset]));
            log.AppendLine(L.T("SHA-256 do resultado: ", "Result SHA-256: ") + newSha + L.T(" (confere)", " (matches)"));
            log.AppendLine();
            foreach (PatchEntry e in entries) log.AppendLine(string.Format("  0x{0:X6}  {1}", e.Offset, e.Description));
            return d;
        }

        public static int CountChanged(bool fast)
        {
            return fast ? 26 : 18;
        }
    }
}
