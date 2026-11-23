// Acha instrucoes com um escalar imediato e decompila a funcao que as contem + funcoes chamadas (1 nivel).
// Args: <saida> <imm_hex> [imm_hex...]
// @category GP200
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.scalar.Scalar;
import java.io.*;
import java.util.*;

public class FindImm extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] a = getScriptArgs();
        PrintWriter out = new PrintWriter(new FileWriter(a[0], true));
        Set<Long> imms = new HashSet<>();
        for (int i = 1; i < a.length; i++) imms.add(Long.parseLong(a[i], 16));
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        Set<Function> done = new LinkedHashSet<>();
        InstructionIterator it = currentProgram.getListing().getInstructions(true);
        while (it.hasNext() && !monitor.isCancelled()) {
            Instruction ins = it.next();
            for (int op = 0; op < ins.getNumOperands(); op++) {
                for (Object o : ins.getOpObjects(op)) {
                    if (o instanceof Scalar && imms.contains(((Scalar) o).getUnsignedValue())) {
                        Function f = getFunctionContaining(ins.getAddress());
                        out.println("// imediato " + o + " em " + ins.getAddress() + " : " + ins + " funcao " + (f == null ? "?" : f.getName()));
                        if (f != null) done.add(f);
                    }
                }
            }
        }
        Set<Function> all = new LinkedHashSet<>(done);
        for (Function f : done) all.addAll(f.getCalledFunctions(monitor));
        for (Function f : all) {
            if (f.isThunk() || f.isExternal()) continue;
            out.println("// ===== " + f.getName() + " @ " + f.getEntryPoint());
            for (Function c : f.getCallingFunctions(monitor)) out.println("//   chamada por " + c.getName() + " @ " + c.getEntryPoint());
            DecompileResults r = d.decompileFunction(f, 180, monitor);
            out.println(r.decompileCompleted() ? r.getDecompiledFunction().getC() : "// falhou");
        }
        out.close();
    }
}
