// Prepara o programa GP-200: adiciona a imagem do app em 0x80000000 e a RAM em 0x20200000,
// marca pontos de entrada conhecidos. Roda como -preScript no analyzeHeadless.
// @category GP200
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.mem.*;
import ghidra.program.model.symbol.SourceType;
import java.io.*;

public class SetupGP200 extends GhidraScript {
    @Override
    public void run() throws Exception {
        Memory mem = currentProgram.getMemory();
        String appPath = getScriptArgs().length > 0 ? getScriptArgs()[0] : null;
        if (appPath == null) { printerr("falta caminho do app"); return; }
        File f = new File(appPath);
        Address appBase = toAddr(0x80000000L);
        if (mem.getBlock(appBase) == null) {
            try (InputStream in = new FileInputStream(f)) {
                MemoryBlock b = mem.createInitializedBlock("app", appBase, in, f.length(), monitor, false);
                b.setRead(true); b.setWrite(false); b.setExecute(true);
            }
        }
        Address ram = toAddr(0x20200000L);
        if (mem.getBlock(ram) == null) {
            MemoryBlock r = mem.createUninitializedBlock("ocram", ram, 0x80000, false);
            r.setRead(true); r.setWrite(true); r.setExecute(false);
        }
        Address dtcm = toAddr(0x20000000L);
        if (mem.getBlock(dtcm) == null) {
            MemoryBlock r = mem.createUninitializedBlock("dtcm", dtcm, 0x20000, false);
            r.setRead(true); r.setWrite(true); r.setExecute(false);
        }
        MemoryBlock low = mem.getBlock(toAddr(0));
        if (low != null) { low.setName("low"); low.setExecute(true); }
        long[][] fns = {
            {0x4E8L}, {0x80000000L},
            {0xA4B0L}, {0xA4A4L}, {0x87BCL}, {0x8A1CL}, {0xA888L}, {0xA620L}, {0xA560L}, {0x13080L}, {0x7CDCL},
            {0x800233D0L}, {0x80023CF4L}, {0x800261FEL}
        };
        String[] names = {"reset", "app_main", "set_tempo", "get_tempo", "set_global_bpm", "set_bpm_link_flag",
            "select_patch", "load_patch", "apply_tempo", "patch_param_msg", "migrate_global_settings",
            "tap_a", "tap_b", "tap_c"};
        for (int i = 0; i < fns.length; i++) {
            Address a = toAddr(fns[i][0]);
            disassemble(a);
            if (getFunctionAt(a) == null) createFunction(a, names[i]);
            else getFunctionAt(a).setName(names[i], SourceType.USER_DEFINED);
        }
        createLabel(toAddr(0x2025F370L), "g_state", true);
        println("SetupGP200 ok");
    }
}
