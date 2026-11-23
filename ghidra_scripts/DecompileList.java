// Decompila funcoes (enderecos hex) e grava em um arquivo. Args: <saida> <addr1> <addr2> ...
// Tambem lista referencias de entrada de cada funcao.
// @category GP200
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.Reference;
import java.io.*;

public class DecompileList extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] a = getScriptArgs();
        PrintWriter out = new PrintWriter(new FileWriter(a[0], true));
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        for (int i = 1; i < a.length; i++) {
            Address addr = toAddr(Long.parseLong(a[i].replace("0x", ""), 16));
            Function f = getFunctionContaining(addr);
            if (f == null) { disassemble(addr); f = createFunction(addr, null); }
            if (f == null) { out.println("// sem funcao em " + addr); continue; }
            out.println("// ===== " + f.getName() + " @ " + f.getEntryPoint());
            for (Reference r : getReferencesTo(f.getEntryPoint())) {
                Function c = getFunctionContaining(r.getFromAddress());
                out.println("//   ref de " + r.getFromAddress() + " (" + r.getReferenceType() + ") em " + (c == null ? "?" : c.getName()));
            }
            DecompileResults res = d.decompileFunction(f, 120, monitor);
            out.println(res.decompileCompleted() ? res.getDecompiledFunction().getC() : "// falhou: " + res.getErrorMessage());
        }
        out.close();
    }
}
