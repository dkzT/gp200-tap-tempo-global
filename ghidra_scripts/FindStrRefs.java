// Acha strings (substring) no programa, lista referencias e decompila as funcoes que as usam.
// Args: <saida> <substr1> [substr2 ...]   (prefixo "imp:" = lista funcoes importadas que contem o texto)
// @category GP200
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;

public class FindStrRefs extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] a = getScriptArgs();
        PrintWriter out = new PrintWriter(new FileWriter(a[0], true));
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        Set<Function> funcs = new LinkedHashSet<>();
        for (int i = 1; i < a.length; i++) {
            String s = a[i];
            if (s.startsWith("imp:")) {
                String k = s.substring(4).toLowerCase();
                for (Symbol sym : currentProgram.getSymbolTable().getExternalSymbols()) {
                    if (sym.getName().toLowerCase().contains(k)) {
                        out.println("// import " + sym.getName() + " (" + sym.getParentNamespace() + ")");
                        for (Reference r : getReferencesTo(sym.getAddress())) {
                            Function f = getFunctionContaining(r.getFromAddress());
                            out.println("//    usado em " + r.getFromAddress() + " " + (f == null ? "?" : f.getName()));
                        }
                    }
                }
                continue;
            }
            DataIterator it = currentProgram.getListing().getDefinedData(true);
            while (it.hasNext()) {
                Data dt = it.next();
                Object v = dt.getValue();
                if (v instanceof String && ((String) v).contains(s)) {
                    out.println("// string \"" + v + "\" @ " + dt.getAddress());
                    for (Reference r : getReferencesTo(dt.getAddress())) {
                        Function f = getFunctionContaining(r.getFromAddress());
                        out.println("//    ref " + r.getFromAddress() + " em " + (f == null ? "?" : f.getName() + " @ " + f.getEntryPoint()));
                        if (f != null) funcs.add(f);
                    }
                }
            }
        }
        for (Function f : funcs) {
            out.println("// ===== " + f.getName() + " @ " + f.getEntryPoint());
            DecompileResults r = d.decompileFunction(f, 180, monitor);
            out.println(r.decompileCompleted() ? r.getDecompiledFunction().getC() : "// falhou");
        }
        out.close();
    }
}
