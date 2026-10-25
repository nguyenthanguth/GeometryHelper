using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An MLINE as its record writes it, read in one pass for <see cref="DxfUnknownEntity.Explode"/>: its style, its flags and
    /// each vertex with the direction of the segment it starts, its miter and the parameters of each element there; and the
    /// MLINESTYLE it is drawn by, its flags and its elements.
    /// </summary>
    /// <remarks>
    /// The geometry is the record's, its scale, justification and style already worked into it, as AutoCAD writes it: an
    /// element stands at a vertex at its first parameter along the miter, and runs along the segment from there.
    /// </remarks>
    internal sealed class DxfMline
    {
        private DxfMline()
        {
        }

        /// <summary>The name of the style (group 2).</summary>
        internal string StyleName { get; private set; }

        /// <summary>The handle of the style (group 340); null where the record gives none.</summary>
        internal string StyleHandle { get; private set; }

        /// <summary>The flags (group 71): 2 closed, 4 its start caps left off, 8 its end caps.</summary>
        internal int Flags { get; private set; }

        /// <summary>The vertices, in their order.</summary>
        internal List<Vertex> Vertices { get; } = new List<Vertex>();

        /// <summary>Reads the own groups of an MLINE record: its head, then from each group 11 a vertex, from each 74 an element of it.</summary>
        internal static DxfMline Read(IReadOnlyList<DxfTypedValue> groups)
        {
            var mline = new DxfMline { StyleName = DxfFollowers.Text(groups, 2)?.Trim() ?? string.Empty, Flags = DxfFollowers.Whole(groups, 71, 1) };
            string handle = DxfFollowers.Text(groups, 340)?.Trim();
            mline.StyleHandle = string.IsNullOrEmpty(handle) ? null : handle;
            Vertex vertex = null;
            List<double> element = null;
            int left = 0;

            foreach (DxfTypedValue group in groups)
            {
                int code = group.TypeCode;

                if (code == 11)
                {
                    vertex = new Vertex();
                    mline.Vertices.Add(vertex);
                    element = null;
                    left = 0;
                }

                if (vertex == null)
                {
                    continue;
                }

                if (code >= 11 && code <= 33 && code % 10 >= 1 && code % 10 <= 3)
                {
                    // 11, 21, 31 the vertex; 12, 22, 32 the direction; 13, 23, 33 the miter.
                    double[] point = code % 10 == 1 ? vertex.At : code % 10 == 2 ? vertex.Along : vertex.Miter;
                    point[code / 10 - 1] = Number(group);
                }
                else if (code == 74)
                {
                    element = new List<double>();
                    vertex.Parameters.Add(element);
                    left = Whole(group);
                }
                else if (code == 41 && element != null && left > 0)
                {
                    element.Add(Number(group));
                    left--;
                }
            }

            return mline;
        }

        private static double Number(DxfTypedValue group) => Numbers.TryRead(group.Value, out double value) && Guard.IsFinite(value) ? value : 0.0;

        private static int Whole(DxfTypedValue group) => int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        /// <summary>A vertex of an MLINE: where it is, the direction of the segment it starts and its miter, in the world, and the parameters of each element.</summary>
        internal sealed class Vertex
        {
            /// <summary>The vertex (11, 21, 31).</summary>
            internal double[] At { get; } = new double[3];

            /// <summary>The direction of the segment it starts (12, 22, 32).</summary>
            internal double[] Along { get; } = new double[3];

            /// <summary>The miter (13, 23, 33).</summary>
            internal double[] Miter { get; } = new double[3];

            /// <summary>
            /// The parameters of each element (41, counted by 74): the first how far along the miter the element stands, the
            /// others where it runs and where it is broken along the segment, from that point, a start and a stop by turns.
            /// </summary>
            internal List<List<double>> Parameters { get; } = new List<List<double>>();

            /// <summary>Where an element stands at the vertex: the vertex moved along the miter by its first parameter, nought where it has none.</summary>
            internal GeoPoint3 PointOf(int element)
            {
                List<double> parameters = Parameters[element];
                double length = parameters.Count > 0 ? parameters[0] : 0.0;
                return new GeoPoint3(At[0] + Miter[0] * length, At[1] + Miter[1] * length, At[2] + Miter[2] * length);
            }
        }

        /// <summary>An MLINESTYLE as its record writes it: its flags and its elements in their order.</summary>
        internal sealed class Style
        {
            private Style()
            {
            }

            /// <summary>
            /// The flags (group 70): 1 filled, 2 a miter line at each joint, 16 a line across the start, 32 inner arcs at the
            /// start, 64 an outer arc at the start; 256, 512 and 1024 the same at the end.
            /// </summary>
            internal int Flags { get; private set; }

            /// <summary>The elements, each its offset (49), its colour (62) and its linetype (6), in the order of the record.</summary>
            internal List<Element> Elements { get; } = new List<Element>();

            /// <summary>Reads the own groups of an MLINESTYLE record: from its group 71 on, an element from each 49.</summary>
            internal static Style Read(IReadOnlyList<DxfTypedValue> groups)
            {
                var style = new Style { Flags = DxfFollowers.Whole(groups, 70, 0) };
                bool counted = false;
                Element element = null;

                foreach (DxfTypedValue group in groups)
                {
                    if (group.TypeCode == 71)
                    {
                        counted = true;
                    }
                    else if (counted && group.TypeCode == 49)
                    {
                        element = new Element(Number(group));
                        style.Elements.Add(element);
                    }
                    else if (element != null && group.TypeCode == 62)
                    {
                        element.Colour = Whole(group);
                    }
                    else if (element != null && group.TypeCode == 6)
                    {
                        string name = DxfReader.NameOf(group.Value ?? string.Empty);
                        element.Linetype = name.Length == 0 ? "BYLAYER" : name;
                    }
                }

                return style;
            }

            /// <summary>The elements by their offsets, the least first, two of one offset in the order of the record.</summary>
            internal List<int> Ordered()
            {
                var order = new List<int>();

                for (int i = 0; i < Elements.Count; i++)
                {
                    order.Add(i);
                }

                order.Sort((a, b) =>
                {
                    int by = Elements[a].Offset.CompareTo(Elements[b].Offset);
                    return by != 0 ? by : a.CompareTo(b);
                });

                return order;
            }
        }

        /// <summary>An element of an MLINESTYLE: its offset, colour and linetype.</summary>
        internal sealed class Element
        {
            internal Element(double offset)
            {
                Offset = offset;
            }

            /// <summary>Its offset from the line drawn (49).</summary>
            internal double Offset { get; }

            /// <summary>Its colour (62); 0, BYBLOCK, where the record gives none, as AutoCAD reads one.</summary>
            internal int Colour { get; set; }

            /// <summary>Its linetype (6); BYLAYER where the record gives none.</summary>
            internal string Linetype { get; set; } = "BYLAYER";
        }
    }
}
