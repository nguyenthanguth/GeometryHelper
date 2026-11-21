using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryHelper.PyStubs
{
    /// <summary>The stubs written for one assembly: how many types and members, and in how many modules (namespaces).</summary>
    public sealed class AssemblyStubs
    {
        internal AssemblyStubs(string name, int types, int members, int modules)
        {
            Name = name;
            Types = types;
            Members = members;
            Modules = modules;
        }

        /// <summary>The simple name of the assembly, <c>GeometryHelper.Cad</c>.</summary>
        public string Name { get; }

        /// <summary>How many public types of the assembly were written.</summary>
        public int Types { get; }

        /// <summary>
        /// How many members were written: each constructor, property, indexer, event, method, operator, field and enum
        /// member, each overload counted once.
        /// </summary>
        public int Members { get; }

        /// <summary>In how many namespaces the assembly's types are.</summary>
        public int Modules { get; }
    }

    /// <summary>What <see cref="StubGenerator.Generate"/> wrote and what it left out.</summary>
    public sealed class StubReport
    {
        internal StubReport(
            IReadOnlyList<AssemblyStubs> assemblies,
            IReadOnlyList<string> modules,
            IReadOnlyList<string> leftOut,
            IReadOnlyList<string> anyMapped,
            string folder
        )
        {
            Assemblies = assemblies;
            Modules = modules;
            LeftOut = leftOut;
            AnyMapped = anyMapped;
            Folder = folder;
        }

        /// <summary>One entry per assembly, by assembly name.</summary>
        public IReadOnlyList<AssemblyStubs> Assemblies { get; }

        /// <summary>The namespaces written, sorted, each one a module (<c>GeometryHelper.Dxf</c>).</summary>
        public IReadOnlyList<string> Modules { get; }

        /// <summary>
        /// The members and types left out, sorted, each as <c>reason: Full.Name</c>: a name that is a Python keyword, a
        /// compiler-generated member, an operator Python has no form for, a nested or generic type.
        /// </summary>
        public IReadOnlyList<string> LeftOut { get; }

        /// <summary>The .NET types written as <c>typing.Any</c> because the table of the stubs does not know them, by full name, sorted.</summary>
        public IReadOnlyList<string> AnyMapped { get; }

        /// <summary>The folder the stubs were written to, <c>GeometryHelper-stubs</c> inside the output folder.</summary>
        public string Folder { get; }

        /// <summary>
        /// The summary the program prints: a line per assembly, the total, the members left out with their reasons and the
        /// types written as <c>typing.Any</c>. Lines end in a line feed.
        /// </summary>
        public string Summary()
        {
            var text = new StringBuilder();
            foreach (AssemblyStubs assembly in Assemblies)
            {
                text.Append(assembly.Name)
                    .Append(": ")
                    .Append(assembly.Types)
                    .Append(" types, ")
                    .Append(assembly.Members)
                    .Append(" members, ")
                    .Append(assembly.Modules)
                    .Append(" modules\n");
            }

            text.Append("Total: ")
                .Append(Assemblies.Sum(a => a.Types))
                .Append(" types, ")
                .Append(Assemblies.Sum(a => a.Members))
                .Append(" members, ")
                .Append(Modules.Count)
                .Append(" modules, written to ")
                .Append(Folder)
                .Append('\n');
            text.Append("Left out (").Append(LeftOut.Count).Append("):\n");
            foreach (string item in LeftOut)
            {
                text.Append("  ").Append(item).Append('\n');
            }

            text.Append("Written as typing.Any (").Append(AnyMapped.Count).Append("):\n");
            foreach (string name in AnyMapped)
            {
                text.Append("  ").Append(name).Append('\n');
            }

            return text.ToString();
        }
    }
}
