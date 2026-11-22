using System;
using System.Collections.Generic;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>vlax-dump-object</c>: what AutoCAD 2021 writes for an object, from the members of the presenter's own object model
    /// (measured in the full AutoCAD, probes gd1 and gd2 of the C4a measurements). AutoCAD lists every member of the interface,
    /// the presenter those it has.
    /// </summary>
    internal sealed partial class LispActiveX
    {
        // The line after the interface's name, as the type library of AutoCAD 2021 words it (probe gd2); an interface not here
        // is "AutoCAD" and its name without IAcad and "Interface", which is how most entities are worded.
        private static readonly Dictionary<string, string> InterfaceDescriptions = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["IAcad3DFace"] = "AutoCAD 3dFace Interface",
            ["IAcadAcCmColor"] = "AutoCAD AcCmColor Interface",
            ["IAcadApplication"] = "An instance of the AutoCAD application",
            ["IAcadBlock"] = "A block definition containing a name and a set of objects",
            ["IAcadBlockReference"] = "AutoCAD Block Reference Interface",
            ["IAcadBlocks"] = "The collection of all blocks in the drawing",
            ["IAcadDimStyle"] = "A group of dimension settings that determines the appearance of a dimension",
            ["IAcadDimStyles"] = "The collection of all dimension styles in the drawing",
            ["IAcadDocument"] = "An AutoCAD drawing",
            ["IAcadLayer"] = "A logical grouping of data, similar to transparent acetate overlays on a drawing",
            ["IAcadLayers"] = "The collection of all layers in the drawing",
            ["IAcadLineType"] = "The line characteristics consisting of combinations of dashes, dots, and spaces",
            ["IAcadLineTypes"] = "The collection of all linetypes in the drawing",
            ["IAcadLWPolyline"] = "AutoCAD Lightweight Polyline Interface",
            ["IAcadModelSpace"] = "A special Block object containing all model space entities",
            ["IAcadMText"] = "AutoCAD MText Interface",
            ["IAcadPaperSpace"] = "A special Block object containing all the entities in the active paper space layout",
            ["IAcadSelectionSet"] = "A group of one or more AutoCAD objects specified for processing as a single unit",
            ["IAcadSelectionSets"] = "The collection of all selection sets in the drawing",
            ["IAcadTextStyle"] =
                "A named, saved collection of settings that determines the appearance of text characters",
            ["IAcadTextStyles"] = "The collection of all text styles in the drawing",
        };

        /// <summary>
        /// The text <c>vlax-dump-object</c> writes: the interface and what it is, then <c>Property values:</c> and each property
        /// the object has, by name, as <c>;   Name (RO) = value</c> (<c>(RO)</c> for one that cannot be set) with the value as
        /// <c>prin1</c> writes it, a Boolean as -1 or 0 and a point as a list; a value the object does not have is
        /// <c>AutoCAD.Application</c>, as AutoCAD writes it. With <paramref name="methods"/>, <c>Methods supported:</c> and each
        /// method by name with the number of its parameters.
        /// </summary>
        /// <param name="target">The object.</param>
        /// <param name="methods">Whether to list the methods too.</param>
        /// <returns>The lines, each ending in a new line.</returns>
        internal string DumpText(LispVlaObject target, bool methods)
        {
            EnsureLive(target);
            var text = new StringBuilder();
            text.Append("; ")
                .Append(target.InterfaceName)
                .Append(": ")
                .Append(Describe(target.InterfaceName))
                .Append('\n');
            text.Append("; Property values:\n");
            var properties = new List<string>();
            var calls = new List<string>();

            foreach (string name in Names)
            {
                if (Find(target, name, m => m.Get != null) != null)
                {
                    properties.Add(name);
                }

                if (Find(target, name, m => m.Call != null) != null)
                {
                    calls.Add(name);
                }
            }

            properties.Sort(CompareNames);
            calls.Sort(CompareNames);

            foreach (string name in properties)
            {
                LispAxMember member = Find(target, name, m => m.Get != null);
                object value;

                try
                {
                    value = ToLisp(member.Get(target), true);
                }
                catch (LispException)
                {
                    continue;
                }

                text.Append(";   ").Append(name).Append(member.Put == null ? " (RO)" : string.Empty).Append(" = ");
                text.Append(value == null ? "AutoCAD.Application" : LispInterpreter.Print(value, true)).Append('\n');
            }

            if (methods)
            {
                text.Append("; Methods supported:\n");

                foreach (string name in calls)
                {
                    LispAxMember member = Find(target, name, m => m.Call != null);
                    text.Append(";   ").Append(name).Append(" (").Append(member.Parameters.Length).Append(")\n");
                }
            }

            return text.ToString();
        }

        private static string Describe(string interfaceName) =>
            InterfaceDescriptions.TryGetValue(interfaceName, out string description)
                ? description
                : "AutoCAD "
                    + (
                        interfaceName.StartsWith("IAcad", StringComparison.Ordinal)
                            ? interfaceName.Substring(5)
                            : interfaceName
                    )
                    + " Interface";

        // By name without regard to case, then as written, so that the order never depends on how the members were registered.
        private static int CompareNames(string x, string y)
        {
            int order = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : string.CompareOrdinal(x, y);
        }
    }
}
