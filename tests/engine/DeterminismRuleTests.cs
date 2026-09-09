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
            t.FullName.StartsWith(VendoredNamespace, StringComparison.Ordinal);

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
                        if (i.OpCode == OpCodes.Ldc_R4 || i.OpCode == OpCodes.Ldc_R8)
                            offenders.Add($"literal {type.FullName}.{m.Name} : {i.OpCode}");
                }
            }

            Assert.True(offenders.Count == 0,
                "floating point in the simulation core:\n  " + string.Join("\n  ", offenders));
        }

        static bool IsFloat(TypeReference t) =>
            t.MetadataType == MetadataType.Single || t.MetadataType == MetadataType.Double;
    }
}
