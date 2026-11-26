// GP-200 Tap Tempo Global Patcher
// Aplica o patch de tap tempo global no firmware OFICIAL da Valeton GP-200 V1.8.2.
// Compilar: app\build.bat (usa o compilador C# que já vem no Windows).
// Linha de comando (opcional): TapGlobalPatcher.exe <firmware_oficial.bin> <saida.bin> [--no-fast]
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TapGlobal
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length == 2 || args.Length == 3)
            {
                StringBuilder log = new StringBuilder();
                try
                {
                    if (Path.GetFullPath(args[0]) == Path.GetFullPath(args[1])) throw new Exception(L.T("A saída deve ser outro arquivo.", "The output must be a different file."));
                    bool fast = !(args.Length == 3 && args[2] == "--no-fast");
                    byte[] patched = Patcher.Apply(File.ReadAllBytes(args[0]), fast, log);
                    File.WriteAllBytes(args[1], patched);
                    log.AppendLine(L.T("Gerado: ", "Generated: ") + args[1]);
                    MessageBox.Show(log.ToString(), "Tap Tempo Global", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(log.ToString() + L.T("ERRO: ", "ERROR: ") + ex.Message, "Tap Tempo Global", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
