using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Broodline.Sim.Tests
{
    public class DeterminismRuleTests
    {
        // FixPointCS ships conversion helpers to and from float and double at its
        // API edge. They are never called from the tick loop, and the exclusion is
        // deliberately namespace-scoped so it cannot silently cover our own code.
        const string VendoredNamespace = "FixPointCS";

        static string AssemblyPath =>
            typeof(SimVersion).Assembly.Location;

        static bool IsVendored(TypeDefinition t) =>
            t.Namespace == VendoredNamespace ||
            t.Namespace.StartsWith(VendoredNamespace + ".", StringComparison.Ordinal);

        [Fact]
        public void SimulationCore_ContainsNoFloatingPoint()
        {
            var offenders = new List<string>();
            using var asm = AssemblyDefinition.ReadAssembly(AssemblyPath);

            foreach (var type in asm.MainModule.GetTypes())
            {
                if (IsVendored(type)) continue;

                foreach (var f in type.Fields)
                    if (IsFloat(f.FieldType))
                        offenders.Add($"field {type.FullName}.{f.Name} : {f.FieldType.Name}");

                foreach (var m in type.Methods)
                {
                    if (IsFloat(m.ReturnType))
                        offenders.Add($"return {type.FullName}.{m.Name} : {m.ReturnType.Name}");

                    foreach (var p in m.Parameters)
                        if (IsFloat(p.ParameterType))
                            offenders.Add($"param {type.FullName}.{m.Name}({p.Name}) : {p.ParameterType.Name}");

                    if (!m.HasBody) continue;

                    foreach (var v in m.Body.Variables)
                        if (IsFloat(v.VariableType))
                            offenders.Add($"local {type.FullName}.{m.Name} : {v.VariableType.Name}");

                    foreach (var i in m.Body.Instructions)
                    {
                        if (i.OpCode == OpCodes.Ldc_R4 || i.OpCode == OpCodes.Ldc_R8 ||
                            i.OpCode == OpCodes.Conv_R4 || i.OpCode == OpCodes.Conv_R8 ||
                            i.OpCode == OpCodes.Conv_R_Un)
                            offenders.Add($"float op {type.FullName}.{m.Name} : {i.OpCode}");

                        // Opcodes alone are blind to float that arrives through an
                        // OPERAND. `(int)FixPointCS.Fixed64.ToDouble(raw)` compiles to
                        // `call float64 …::ToDouble(int64)` followed by `conv.i4` — not
                        // one of the opcodes above appears, yet a double crossed the
                        // boundary into non-vendored code and was rounded by the
                        // platform. FixPointCS ships ToDouble/FromDouble/ToFloat/
                        // FromFloat and System.BitConverter is not banned, so this is
                        // the reachable route, not a hypothetical one — and it is
                        // exactly the route someone reaches for to work around Mul's
                        // documented silent overflow. Fix64's header comment promises
                        // "not in a signature, not in a body, not in a cast"; without
                        // this the scan could not back that promise.
                        //
                        // Vendored types never reach here — the IsVendored guard above
                        // skips them wholesale — so FixPointCS's own internal calls to
                        // its own float helpers stay exempt, while a call INTO them
                        // from our code does not.
                        switch (i.Operand)
                        {
                            // MethodReference and CallSite (calli) both implement
                            // IMethodSignature; GenericInstanceMethod is a
                            // MethodReference, and its type arguments are checked too.
                            case IMethodSignature sig when IsFloatSignature(sig):
                                offenders.Add($"float via call {type.FullName}.{m.Name} -> {i.Operand}");
                                break;
                            case FieldReference fr when IsFloat(fr.FieldType):
                                offenders.Add($"float via field {type.FullName}.{m.Name} -> {fr.FullName}");
                                break;
                            // newarr/box/ldtoken/castclass carry a bare TypeReference;
                            // a float one there is floating point by construction.
                            case TypeReference tr when IsFloat(tr):
                                offenders.Add($"float via type operand {type.FullName}.{m.Name} -> {tr.FullName}");
                                break;
                        }
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "floating point in the simulation core:\n  " + string.Join("\n  ", offenders));
        }

        // A called method is a float boundary if float appears anywhere in its
        // signature — the return type is the case the opcode scan misses entirely,
        // since the caller may consume the value with a plain conv.i4.
        static bool IsFloatSignature(IMethodSignature sig)
        {
            if (IsFloat(sig.ReturnType)) return true;

            if (sig.HasParameters)
                foreach (var p in sig.Parameters)
                    if (IsFloat(p.ParameterType)) return true;

            if (sig is GenericInstanceMethod gim)
                foreach (var arg in gim.GenericArguments)
                    if (IsFloat(arg)) return true;

            // Return type, parameters and generic arguments all miss a call
            // whose only float is its DECLARING type, e.g.
            // `new List<float>(4).Capacity`: neither `.ctor(int32)` nor
            // `get_Capacity()` has a float anywhere in its own signature.
            // CallSite (calli) is not a MethodReference and has no declaring
            // type, so it correctly falls through untouched.
            if (sig is MethodReference mr && IsFloat(mr.DeclaringType)) return true;

            return false;
        }

        static bool IsFloat(TypeReference t)
        {
            if (t == null) return false;
            if (t.MetadataType == MetadataType.Single || t.MetadataType == MetadataType.Double)
                return true;
            if (t is ArrayType a) return IsFloat(a.ElementType);
            if (t is ByReferenceType r) return IsFloat(r.ElementType);
            if (t is PointerType p) return IsFloat(p.ElementType);
            if (t is GenericInstanceType g)
            {
                foreach (var arg in g.GenericArguments)
                    if (IsFloat(arg)) return true;
            }
            return false;
        }

        // Permanent regression test for the array-recursion fix: constructs a
        // Mono.Cecil ArrayType with a float element directly (no production code
        // involved) and asserts IsFloat follows the recursion into it. IsFloat stays
        // private — this test lives in the same class, so no InternalsVisibleTo is
        // needed to reach it.
        [Fact]
        public void IsFloat_RecursesIntoArrayElementType()
        {
            using var asm = AssemblyDefinition.ReadAssembly(AssemblyPath);
            var floatElement = asm.MainModule.ImportReference(typeof(float));
            var intElement = asm.MainModule.ImportReference(typeof(int));

            Assert.True(IsFloat(new ArrayType(floatElement)));
            Assert.False(IsFloat(new ArrayType(intElement)));
        }
    }
}
