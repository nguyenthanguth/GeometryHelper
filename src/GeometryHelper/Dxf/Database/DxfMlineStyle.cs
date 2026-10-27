using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A style of multilines of a DXF file, as AutoCAD's <c>MlineStyle</c>: an MLINESTYLE of the dictionary of multiline styles
    /// (<see cref="DxfDatabase.MLStyleDictionary"/>), read from its record: its name, description, fill, caps, the angles of
    /// its ends and its elements, the lines it draws at their offsets.
    /// </summary>
    /// <remarks>
    /// AutoCAD's style STANDARD is two elements 0.5 either side of the line drawn, by layer in colour and linetype, its ends
    /// square to it (90 degrees), not filled and with no caps: <see cref="Elements"/> of offsets 0.5 and -0.5,
    /// <see cref="StartAngle"/> and <see cref="EndAngle"/> pi / 2. A style is added from a file of styles by
    /// <see cref="DxfDatabase.LoadMlineStyleFile"/>; its values are read, not set, here.
    /// </remarks>
    public sealed class DxfMlineStyle : DxfDBObject
    {
        internal DxfMlineStyle(DxfItem item)
            : base(item)
        {
        }

        /// <summary>Gets the name of the style (group 2), as AutoCAD's <c>Name</c>, such as <c>STANDARD</c>.</summary>
        public string Name => DxfReader.NameOf(Text(2) ?? string.Empty).Trim();

        /// <summary>Gets the description of the style (group 3), as AutoCAD's <c>Description</c>; empty where it gives none.</summary>
        public string Description => Text(3) ?? string.Empty;

        /// <summary>Gets the angle of the multiline's start to its line (group 51, in degrees) in radians, as AutoCAD's <c>StartAngle</c>: pi / 2 square across.</summary>
        public double StartAngle => Real(51, 90.0) * Math.PI / 180.0;

        /// <summary>Gets the angle of the multiline's end to its line (group 52, in degrees) in radians, as AutoCAD's <c>EndAngle</c>: pi / 2 square across.</summary>
        public double EndAngle => Real(52, 90.0) * Math.PI / 180.0;

        /// <summary>Gets whether the multiline is filled between its outer elements (flag 1 of group 70), as AutoCAD's <c>Filled</c>.</summary>
        public bool Filled => (Flags & 1) != 0;

        /// <summary>Gets whether a line is drawn across each joint (flag 2), as AutoCAD's <c>ShowMiters</c>.</summary>
        public bool ShowMiters => (Flags & 2) != 0;

        /// <summary>Gets whether a line closes the start (flag 16), as AutoCAD's <c>StartSquareCap</c>.</summary>
        public bool StartSquareCap => (Flags & 16) != 0;

        /// <summary>Gets whether arcs join the inner elements at the start (flag 32), as AutoCAD's <c>StartInnerArcs</c>.</summary>
        public bool StartInnerArcs => (Flags & 32) != 0;

        /// <summary>Gets whether an arc joins the outer elements at the start (flag 64), as AutoCAD's <c>StartRoundCap</c>.</summary>
        public bool StartRoundCap => (Flags & 64) != 0;

        /// <summary>Gets whether a line closes the end (flag 256), as AutoCAD's <c>EndSquareCap</c>.</summary>
        public bool EndSquareCap => (Flags & 256) != 0;

        /// <summary>Gets whether arcs join the inner elements at the end (flag 512), as AutoCAD's <c>EndInnerArcs</c>.</summary>
        public bool EndInnerArcs => (Flags & 512) != 0;

        /// <summary>Gets whether an arc joins the outer elements at the end (flag 1024), as AutoCAD's <c>EndRoundCap</c>.</summary>
        public bool EndRoundCap => (Flags & 1024) != 0;

        /// <summary>Gets the colour of the fill (group 62), as AutoCAD's <c>FillColor</c>: BYLAYER where the record gives none.</summary>
        public DxfColor FillColor
        {
            get
            {
                return int.TryParse(Text(62)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? DxfColor.OfIndex(index) : DxfColor.ByLayer;
            }
        }

        /// <summary>
        /// Gets the elements of the style, as AutoCAD's <c>Elements</c>: each its offset from the line drawn (49), its colour
        /// (62) and its linetype (6), in the order of the record, a new list on each call.
        /// </summary>
        public IReadOnlyList<DxfMlineStyleElement> Elements
        {
            get
            {
                var elements = new List<DxfMlineStyleElement>();

                foreach (DxfMline.Element element in DxfMline.Style.Read(Item.File.ObjectGroupsOf(Item)).Elements)
                {
                    elements.Add(new DxfMlineStyleElement(element.Offset, DxfColor.OfIndex(element.Colour), element.Linetype));
                }

                return elements.AsReadOnly();
            }
        }

        /// <summary>Gives the style as a line for debugging: its type, its handle and its name.</summary>
        /// <returns>The line, such as <c>MLINESTYLE 18 STANDARD</c>.</returns>
        public override string ToString() => "MLINESTYLE " + (Handle ?? string.Empty) + " " + Name;

        private int Flags => int.TryParse(Text(70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ? flags : 0;

        /// <summary>The first group of a code of the style's own part, before its elements; null where it has none.</summary>
        private string Text(int code)
        {
            foreach (DxfTypedValue group in Item.File.ObjectGroupsOf(Item))
            {
                if (group.TypeCode == 71)
                {
                    break;
                }

                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        private double Real(int code, double fallback) => Numbers.TryRead(Text(code), out double value) && Guard.IsFinite(value) ? value : fallback;
    }

    /// <summary>
    /// An element of a style of multilines, as AutoCAD's <c>MlineStyleElement</c>: one line of the multiline, at its offset from
    /// the line drawn, in its colour and linetype.
    /// </summary>
    public sealed class DxfMlineStyleElement
    {
        internal DxfMlineStyleElement(double offset, DxfColor color, string linetype)
        {
            Offset = offset;
            Color = color;
            Linetype = linetype;
        }

        /// <summary>Gets the offset of the element from the line drawn (group 49), as AutoCAD's <c>Offset</c>: 0.5 to its left, -0.5 to its right.</summary>
        public double Offset { get; }

        /// <summary>Gets the colour of the element (group 62), as AutoCAD's <c>Color</c>: BYBLOCK where the record gives none, as AutoCAD reads it.</summary>
        public DxfColor Color { get; }

        /// <summary>Gets the name of the element's linetype (group 6), as AutoCAD's <c>LinetypeId</c> names it: <c>BYLAYER</c> where the record gives none.</summary>
        public string Linetype { get; }
    }
}
