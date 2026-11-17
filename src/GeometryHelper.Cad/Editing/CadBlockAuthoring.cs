using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// A parameter or an action of the block open in the Block Editor, as the editor draws it, picks it and lists its properties:
    /// the library's reading of the evaluation graph (<see cref="DxfBlockElement"/>) with the handle that selects it.
    /// </summary>
    internal sealed class CadAuthoringElement
    {
        internal CadAuthoringElement(DxfBlockElement element)
        {
            Element = element;
        }

        /// <summary>The element as the library reads it.</summary>
        internal DxfBlockElement Element { get; }

        /// <summary>The handle of the element's object, which selects it.</summary>
        internal string Handle => Element.Handle;

        /// <summary>Whether the element is a parameter, else an action.</summary>
        internal bool IsParameter => Element.IsParameter;

        /// <summary>The kind of the element.</summary>
        internal DxfBlockElementKind Kind => Element.Kind;

        /// <summary>The name of the element, as the Name row of its properties shows it.</summary>
        internal string Name => Element.Name;

        /// <summary>The kind in words, as the selection box of Properties shows it: "Linear parameter", "Stretch action".</summary>
        internal string TypeName => CadBlockAuthoring.TypeName(Element.Kind);

        /// <summary>The labels of the properties a parameter gives; none for an action.</summary>
        internal IReadOnlyList<string> Labels => Element.Labels;
    }

    /// <summary>
    /// What the Block Editor draws for a parameter or an action: lines in the block's coordinates, arrowheads and grips and the
    /// action's lightning bolt sized in pixels by the window, and a label. It answers how far a point is from it, which picks it.
    /// </summary>
    internal sealed class CadAuthoringGlyph
    {
        internal CadAuthoringGlyph(string handle, bool isParameter, string label)
        {
            Handle = handle;
            IsParameter = isParameter;
            Label = label;
        }

        /// <summary>The handle of the element's object.</summary>
        internal string Handle { get; }

        /// <summary>Whether it is a parameter's glyph, else an action's.</summary>
        internal bool IsParameter { get; }

        /// <summary>The words by the glyph: the property label of a parameter, the name of an action.</summary>
        internal string Label { get; }

        /// <summary>Where the words stand, in the block's coordinates; null for none.</summary>
        internal GeoPoint2? LabelAt { get; set; }

        /// <summary>The lines, each a chain of points in the block's coordinates.</summary>
        internal List<GeoPoint2[]> Lines { get; } = new List<GeoPoint2[]>();

        /// <summary>The arrowheads, each the point it stands at and the point it points away from.</summary>
        internal List<KeyValuePair<GeoPoint2, GeoPoint2>> Arrows { get; } =
            new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

        /// <summary>The grips, small squares at points of the block.</summary>
        internal List<GeoPoint2> Grips { get; } = new List<GeoPoint2>();

        /// <summary>Where an action's lightning bolt stands; null for a parameter.</summary>
        internal GeoPoint2? Bolt { get; set; }

        /// <summary>The box that holds the glyph's lines and the points it stands at, so that a view fitted to the editor takes it in.</summary>
        internal DisplayBox Bounds
        {
            get
            {
                DisplayBox box = DisplayBox.Empty;

                foreach (GeoPoint2[] line in Lines)
                {
                    foreach (GeoPoint2 point in line)
                    {
                        box = box.Add(point.X, point.Y);
                    }
                }

                foreach (GeoPoint2 grip in Grips)
                {
                    box = box.Add(grip.X, grip.Y);
                }

                foreach (KeyValuePair<GeoPoint2, GeoPoint2> arrow in Arrows)
                {
                    box = box.Add(arrow.Key.X, arrow.Key.Y);
                }

                if (Bolt.HasValue)
                {
                    box = box.Add(Bolt.Value.X, Bolt.Value.Y);
                }

                if (LabelAt.HasValue)
                {
                    box = box.Add(LabelAt.Value.X, LabelAt.Value.Y);
                }

                return box;
            }
        }

        /// <summary>The distance from a point to the glyph, to its lines and the points it stands at; the point's own distance to a lone label.</summary>
        internal double DistanceTo(GeoPoint2 point)
        {
            double best = double.MaxValue;

            foreach (GeoPoint2[] line in Lines)
            {
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    best = Math.Min(best, Segment(point, line[i], line[i + 1]));
                }
            }

            foreach (GeoPoint2 grip in Grips)
            {
                best = Math.Min(best, (point - grip).Length);
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> arrow in Arrows)
            {
                best = Math.Min(best, (point - arrow.Key).Length);
            }

            if (Bolt.HasValue)
            {
                best = Math.Min(best, (point - Bolt.Value).Length);
            }

            if (LabelAt.HasValue)
            {
                best = Math.Min(best, (point - LabelAt.Value).Length);
            }

            return best;
        }

        private static double Segment(GeoPoint2 p, GeoPoint2 a, GeoPoint2 b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double length2 = dx * dx + dy * dy;
            double t =
                length2 <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length2));
            double x = a.X + t * dx - p.X;
            double y = a.Y + t * dy - p.Y;
            return Math.Sqrt(x * x + y * y);
        }
    }

    public sealed partial class CadBlockEditor
    {
        private int _readStamp = -1;
        private DxfBlockTableRecord _readBlock;
        private List<CadAuthoringElement> _elements = new List<CadAuthoringElement>();
        private List<CadAuthoringGlyph> _glyphs = new List<CadAuthoringGlyph>();

        /// <summary>
        /// The parameters and actions of the block open, in the order of the evaluation graph, read anew after any change of the
        /// drawing; none for &lt;Current Drawing&gt; and for a block that is not dynamic.
        /// </summary>
        internal IReadOnlyList<CadAuthoringElement> Elements
        {
            get
            {
                Refresh();
                return _elements;
            }
        }

        /// <summary>What the editor draws for the parameters and actions, one glyph each, in the order of <see cref="Elements"/>.</summary>
        internal IReadOnlyList<CadAuthoringGlyph> Glyphs
        {
            get
            {
                Refresh();
                return _glyphs;
            }
        }

        /// <summary>The box that holds the glyphs of every parameter and action; empty where there are none.</summary>
        internal DisplayBox GlyphBounds
        {
            get
            {
                DisplayBox box = DisplayBox.Empty;

                foreach (CadAuthoringGlyph glyph in Glyphs)
                {
                    box = box.Union(glyph.Bounds);
                }

                return box;
            }
        }

        /// <summary>The parameter or action of a handle; null where the block has none of it.</summary>
        internal CadAuthoringElement ElementOf(string handle)
        {
            if (handle == null)
            {
                return null;
            }

            foreach (CadAuthoringElement element in Elements)
            {
                if (string.Equals(element.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>Reads the evaluation graph again where the drawing has changed since it was read.</summary>
        private void Refresh()
        {
            DxfBlockTableRecord block = IsCurrentDrawing ? null : Space.Block;

            if (block == null || !block.IsResident)
            {
                _readStamp = -1;
                _readBlock = null;
                _elements = new List<CadAuthoringElement>();
                _glyphs = new List<CadAuthoringGlyph>();
                return;
            }

            int stamp = block.File.EditCount;

            if (_readStamp == stamp && ReferenceEquals(_readBlock, block))
            {
                return;
            }

            _readStamp = stamp;
            _readBlock = block;
            var elements = new List<CadAuthoringElement>();
            var glyphs = new List<CadAuthoringGlyph>();

            try
            {
                List<DxfBlockElement> read = DxfBlockAuthoring.Elements(block);

                foreach (DxfBlockElement element in read)
                {
                    elements.Add(new CadAuthoringElement(element));
                }

                // The size of the block is worked out when a glyph first needs it, which a parameter of one point alone does.
                double extent = -1.0;
                Func<double> size = () => extent >= 0.0 ? extent : (extent = CadBlockAuthoring.ExtentOf(block));

                foreach (CadAuthoringElement element in elements)
                {
                    CadAuthoringGlyph glyph = CadBlockAuthoring.GlyphOf(element, size);

                    if (glyph != null)
                    {
                        glyphs.Add(glyph);
                    }
                }
            }
            catch (Exception exception)
            {
                // A graph written some way the reader does not know leaves the block with no glyphs, as it was before: the drawing
                // must not fail to show for what is only drawn over it.
                GeometryHelperLog.Warn("CadBlockEditor: the parameters and actions could not be read.", exception);
                elements.Clear();
                glyphs.Clear();
            }

            _elements = elements;
            _glyphs = glyphs;
        }
    }

    /// <summary>
    /// The authoring side of the Block Editor, as AutoCAD 2021's: the parameters and actions of the block open, how they are drawn
    /// and picked, the rows of their Properties and the edits those rows make. The commands (BPARAMETER, BACTION, BVSTATE,
    /// BVHIDE, BVSHOW) are in <see cref="CadCommands"/>; the evaluation graph they write is the library's.
    /// </summary>
    internal static partial class CadBlockAuthoring
    {
        /// <summary>The kind in words: "Linear parameter", "Stretch action".</summary>
        internal static string TypeName(DxfBlockElementKind kind)
        {
            switch (kind)
            {
                case DxfBlockElementKind.Point:
                    return "Point parameter";
                case DxfBlockElementKind.Linear:
                    return "Linear parameter";
                case DxfBlockElementKind.Polar:
                    return "Polar parameter";
                case DxfBlockElementKind.XY:
                    return "XY parameter";
                case DxfBlockElementKind.Rotation:
                    return "Rotation parameter";
                case DxfBlockElementKind.Flip:
                    return "Flip parameter";
                case DxfBlockElementKind.Alignment:
                    return "Alignment parameter";
                case DxfBlockElementKind.Visibility:
                    return "Visibility parameter";
                case DxfBlockElementKind.Lookup:
                    return "Lookup parameter";
                case DxfBlockElementKind.BasePoint:
                    return "Base point parameter";
                case DxfBlockElementKind.BlockTable:
                    return "Block table";
                case DxfBlockElementKind.Move:
                    return "Move action";
                case DxfBlockElementKind.Scale:
                    return "Scale action";
                case DxfBlockElementKind.Stretch:
                    return "Stretch action";
                case DxfBlockElementKind.PolarStretch:
                    return "Polar stretch action";
                case DxfBlockElementKind.Array:
                    return "Array action";
                case DxfBlockElementKind.Rotate:
                    return "Rotate action";
                case DxfBlockElementKind.FlipAction:
                    return "Flip action";
                case DxfBlockElementKind.LookupAction:
                    return "Lookup action";
                case DxfBlockElementKind.OtherAction:
                    return "Action";
                default:
                    return "Parameter";
            }
        }

        /// <summary>The longer side of the box of the block's entities, nought for a block of none; the glyphs' sizes follow it.</summary>
        internal static double ExtentOf(DxfBlockTableRecord block)
        {
            double minX = double.MaxValue,
                minY = double.MaxValue,
                maxX = double.MinValue,
                maxY = double.MinValue;

            foreach (DxfEntity entity in block)
            {
                if (entity.IsErased)
                {
                    continue;
                }

                try
                {
                    GeoAabb3 box = entity.GeometricExtents;

                    if (!box.IsEmpty)
                    {
                        minX = Math.Min(minX, box.Min.X);
                        minY = Math.Min(minY, box.Min.Y);
                        maxX = Math.Max(maxX, box.Max.X);
                        maxY = Math.Max(maxY, box.Max.Y);
                    }
                }
                catch (Exception exception)
                    when (exception is InvalidOperationException
                        || exception is NotSupportedException
                        || exception is ArgumentException
                    )
                {
                    // An entity that cannot say where it is gives the glyphs no size.
                }
            }

            return maxX >= minX ? Math.Max(maxX - minX, maxY - minY) : 0.0;
        }

        private static GeoPoint2 Flat(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>
        /// What the editor draws for an element, as AutoCAD draws it: a parameter of two points as the line between them with an
        /// arrowhead at each end and a grip at the second, an XY parameter as its two legs, a rotation parameter as a circle about
        /// its base point, a flip parameter as its reflection line, a parameter of one point as a grip; its label by it. An action
        /// is a lightning bolt with its name. Null for an element that stands nowhere.
        /// </summary>
        internal static CadAuthoringGlyph GlyphOf(CadAuthoringElement owner, Func<double> size)
        {
            DxfBlockElement element = owner.Element;
            string label = element.IsParameter
                ? (element.Labels.Count > 0 && element.Labels[0].Length > 0 ? element.Labels[0] : element.Name)
                : element.Name;
            var glyph = new CadAuthoringGlyph(element.Handle, element.IsParameter, label);

            if (!element.IsParameter)
            {
                GeoPoint3? at = element.At;

                if (!at.HasValue)
                {
                    return null;
                }

                glyph.Bolt = Flat(at.Value);
                glyph.LabelAt = Flat(at.Value);
                return glyph;
            }

            GeoPoint3? b = element.Base;
            GeoPoint3? e = element.End;

            switch (element.Kind)
            {
                case DxfBlockElementKind.Linear:
                case DxfBlockElementKind.Polar:
                    if (!b.HasValue || !e.HasValue)
                    {
                        return null;
                    }

                    Dimension(glyph, Flat(b.Value), Flat(e.Value));
                    glyph.Grips.Add(Flat(e.Value));
                    break;
                case DxfBlockElementKind.XY:
                    if (!b.HasValue || !e.HasValue)
                    {
                        return null;
                    }

                    GeoPoint2 corner = new GeoPoint2(e.Value.X, b.Value.Y);
                    Dimension(glyph, Flat(b.Value), corner);
                    Dimension(glyph, corner, Flat(e.Value));
                    glyph.Grips.Add(Flat(e.Value));
                    break;
                case DxfBlockElementKind.Rotation:
                    if (!b.HasValue || !e.HasValue)
                    {
                        return null;
                    }

                    GeoPoint2 centre = Flat(b.Value);
                    double radius = (Flat(e.Value) - centre).Length;
                    var ring = new GeoPoint2[49];

                    for (int i = 0; i < ring.Length; i++)
                    {
                        double angle = 2.0 * Math.PI * i / (ring.Length - 1);
                        ring[i] = new GeoPoint2(
                            centre.X + radius * Math.Cos(angle),
                            centre.Y + radius * Math.Sin(angle)
                        );
                    }

                    glyph.Lines.Add(ring);
                    glyph.Lines.Add(new[] { centre, Flat(e.Value) });
                    glyph.Grips.Add(Flat(e.Value));
                    break;
                case DxfBlockElementKind.Flip:
                case DxfBlockElementKind.Alignment:
                    if (!b.HasValue || !e.HasValue)
                    {
                        return null;
                    }

                    glyph.Lines.Add(new[] { Flat(b.Value), Flat(e.Value) });
                    glyph.Grips.Add(element.Kind == DxfBlockElementKind.Flip ? Flat(e.Value) : Flat(b.Value));
                    break;
                default:
                    if (!b.HasValue)
                    {
                        return null;
                    }

                    GeoPoint2 mark = Flat(b.Value);
                    double arm = Math.Max(size() * 0.02, 0.0);
                    glyph.Grips.Add(mark);

                    if (arm > 0.0)
                    {
                        glyph.Lines.Add(
                            new[] { new GeoPoint2(mark.X - arm, mark.Y), new GeoPoint2(mark.X + arm, mark.Y) }
                        );
                        glyph.Lines.Add(
                            new[] { new GeoPoint2(mark.X, mark.Y - arm), new GeoPoint2(mark.X, mark.Y + arm) }
                        );
                    }

                    break;
            }

            GeoPoint3? where = element.At;

            if (where.HasValue)
            {
                glyph.LabelAt = Flat(where.Value);
            }
            else if (b.HasValue)
            {
                glyph.LabelAt = e.HasValue ? Flat(b.Value.GetMiddlePoint(e.Value)) : Flat(b.Value);
            }

            return glyph;
        }

        /// <summary>A dimension line from one point to another, an arrowhead at each end pointing outwards.</summary>
        private static void Dimension(CadAuthoringGlyph glyph, GeoPoint2 a, GeoPoint2 b)
        {
            glyph.Lines.Add(new[] { a, b });
            glyph.Arrows.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(a, b));
            glyph.Arrows.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(b, a));
        }

        /// <summary>A box grown to take in the glyphs of the parameters and actions, where the space is the Block Editor's: what a view fitted to the space shows.</summary>
        internal static DisplayBox WithGlyphs(CadDocument document, CadSpace space, DisplayBox box) =>
            ReferenceEquals(document.BlockEditor?.Space, space) ? box.Union(document.BlockEditor.GlyphBounds) : box;

        /// <summary>The element nearest a point within an aperture of the block open; null where none is.</summary>
        internal static CadAuthoringElement ElementAt(CadBlockEditor editor, GeoPoint2 point, double aperture)
        {
            if (editor == null)
            {
                return null;
            }

            CadAuthoringElement best = null;
            double nearest = aperture;
            IReadOnlyList<CadAuthoringElement> elements = editor.Elements;
            IReadOnlyList<CadAuthoringGlyph> glyphs = editor.Glyphs;

            foreach (CadAuthoringGlyph glyph in glyphs)
            {
                double distance = glyph.DistanceTo(point);

                // The nearest wins, an action ahead of a parameter where the two stand as near.
                if (distance <= nearest && (best == null || distance < nearest || !glyph.IsParameter))
                {
                    nearest = distance;

                    foreach (CadAuthoringElement element in elements)
                    {
                        if (string.Equals(element.Handle, glyph.Handle, StringComparison.OrdinalIgnoreCase))
                        {
                            best = element;
                            break;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>The block open in the editor; null where there is none, or it is &lt;Current Drawing&gt;.</summary>
        internal static DxfBlockTableRecord BlockOf(CadDocument document)
        {
            CadBlockEditor editor = document?.BlockEditor;
            return editor == null || editor.IsCurrentDrawing ? null : editor.Space.Block;
        }

        /// <summary>The first number from 1 on that a label made of a word and the number is free at, as AutoCAD numbers its labels: Distance1, Distance2.</summary>
        internal static string FreeLabel(DxfBlockTableRecord block, string word)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfBlockElement element in DxfBlockAuthoring.Elements(block))
            {
                foreach (string label in element.Labels)
                {
                    taken.Add(label);
                }
            }

            for (int i = 1; ; i++)
            {
                string label = word + i.ToString(CultureInfo.InvariantCulture);

                if (!taken.Contains(label))
                {
                    return label;
                }
            }
        }

        /// <summary>The element of a handle in a block read now, which an edit looks for in the drawing it works on; null for none.</summary>
        internal static DxfBlockElement Find(DxfBlockTableRecord block, string handle)
        {
            foreach (DxfBlockElement element in DxfBlockAuthoring.Elements(block))
            {
                if (string.Equals(element.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return element;
                }
            }

            return null;
        }
    }
}
