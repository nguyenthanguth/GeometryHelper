using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The symbol of a weld in one view, drawn to ISO 2553: an arrow to the weld, the reference line, the basic symbol of
    /// the weld's kind on the side it is on, its size to the left of the symbol and its length to the right, in view
    /// millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A weld is a <see cref="DrawingObject"/> of the kind <see cref="DrawingObjectKind.Weld"/>, its body where the weld
    /// metal lies, and what the symbol shows in its attributes:
    /// </para>
    /// <list type="table">
    /// <listheader><term>Attribute</term><description>What it holds</description></listheader>
    /// <item><term><see cref="TypeAttribute"/>, "WeldType"</term><description>The kind, by the names of
    /// <see cref="WeldType"/>: Fillet, SquareButt, VButt or BevelButt; Fillet where it is not given.</description></item>
    /// <item><term><see cref="SideAttribute"/>, "WeldSide"</term><description>Arrow, Other or Both, as
    /// <see cref="WeldSide"/>; Arrow where it is not given.</description></item>
    /// <item><term><see cref="SizeAttribute"/>, "WeldSize"</term><description>The size written left of the symbol as it is
    /// given: "a5" for a fillet's throat, "s8" for a butt weld's depth; none where it is not given.</description></item>
    /// <item><term><see cref="LengthAttribute"/>, "WeldLength"</term><description>The length written right of the symbol as
    /// it is given: "200", or "4 x 50 (100)" for an intermittent weld; none where it is not given.</description></item>
    /// <item><term><see cref="SecondaryPartAttribute"/>, "WeldSecondaryPart"</term><description>The id of the part welded
    /// on; the part it is welded to is the weld's <see cref="DrawingObject.ParentId"/>.</description></item>
    /// </list>
    /// <para>
    /// The kinds and sides are read with letters of either case the same; a weld of a kind or a side not known gets no
    /// symbol and is named in one Debug line. The symbol's arrow points at the middle of the box of the weld's body, seen
    /// in the view; its reference line runs to the right from a knee twice the text's height up and to the right of it.
    /// A fillet weld "a5" 200 long on the arrow side, with a text 2.5 high at 1:10, has its knee 50 up and 50 to the right
    /// of the weld's middle, the triangle 25 high standing on the reference line, "a5" left of it and "200" right.
    /// Symbols are placed by this rule only, not yet kept clear of marks and dimensions.
    /// </para>
    /// <para>A symbol does not change once made.</para>
    /// </remarks>
    public sealed class WeldSymbol
    {
        /// <summary>The attribute a weld's kind is read from: "WeldType".</summary>
        public const string TypeAttribute = "WeldType";

        /// <summary>The attribute the side a weld is on is read from: "WeldSide".</summary>
        public const string SideAttribute = "WeldSide";

        /// <summary>The attribute a weld's size is read from: "WeldSize".</summary>
        public const string SizeAttribute = "WeldSize";

        /// <summary>The attribute a weld's length is read from: "WeldLength".</summary>
        public const string LengthAttribute = "WeldLength";

        /// <summary>The attribute the id of the part welded on is read from: "WeldSecondaryPart".</summary>
        public const string SecondaryPartAttribute = "WeldSecondaryPart";

        private WeldSymbol(string weldId, WeldType type, WeldSide side, string size, string length, GeoPoint2 tip, double textHeight, DrawingLinks links)
        {
            Id = "W-" + weldId;
            WeldId = weldId;
            Type = type;
            Side = side;
            Size = size;
            Length = length;
            ArrowTip = tip;
            TextHeight = textHeight;
            Links = links;
            Knee = new GeoPoint2(tip.X + 2.0 * textHeight, tip.Y + 2.0 * textHeight);
            double sizeWidth = size.Length > 0 ? SymbolDxf.TextWidth(size, textHeight) + 0.3 * textHeight : 0.0;
            double lengthWidth = length.Length > 0 ? 0.3 * textHeight + SymbolDxf.TextWidth(length, textHeight) : 0.0;
            SymbolStart = Knee.X + 0.5 * textHeight + sizeWidth;
            ReferenceLine = new GeoLine2(Knee, new GeoPoint2(SymbolStart + textHeight + lengthWidth + 0.5 * textHeight, Knee.Y));
        }

        /// <summary>Gets the id of the symbol, unique among the weld symbols of its view: W- and the weld's id.</summary>
        public string Id { get; }

        /// <summary>Gets the id of the weld.</summary>
        public string WeldId { get; }

        /// <summary>Gets the kind of the weld, which decides the basic symbol.</summary>
        public WeldType Type { get; }

        /// <summary>Gets the side of the joint the weld is on.</summary>
        public WeldSide Side { get; }

        /// <summary>Gets the size written left of the symbol: "a5"; empty where the weld gives none.</summary>
        public string Size { get; }

        /// <summary>Gets the length written right of the symbol: "200"; empty where the weld gives none.</summary>
        public string Length { get; }

        /// <summary>Gets the point the arrow points at: the middle of the box of the weld's body, in view millimetres.</summary>
        public GeoPoint2 ArrowTip { get; }

        /// <summary>Gets where the arrow line meets the reference line, in view millimetres.</summary>
        public GeoPoint2 Knee { get; }

        /// <summary>Gets the continuous reference line, from the knee to the right, in view millimetres.</summary>
        public GeoLine2 ReferenceLine { get; }

        /// <summary>Gets the height of the text in the view, in view millimetres, which the symbol is as high as.</summary>
        public double TextHeight { get; }

        /// <summary>Gets what the symbol is tied to in the model: the weld, the part it is welded to and the part welded on, and the middle of the weld.</summary>
        public DrawingLinks Links { get; }

        /// <summary>Gets the box everything but the arrow stands in, square to the view's axes, in view millimetres.</summary>
        public GeoRectangle2 Bounds
        {
            get
            {
                double below = Side == WeldSide.Arrow ? 0.5 * TextHeight : Side == WeldSide.Other ? 1.5 * TextHeight : TextHeight;
                double above = Side == WeldSide.Other ? 0.0 : TextHeight;
                return Box2.FromCorner(Knee.X, Knee.Y - below, ReferenceLine.Length, below + above);
            }
        }

        /// <summary>Where the basic symbol starts along the reference line.</summary>
        private double SymbolStart { get; }

        /// <summary>
        /// Makes the symbols of the welds of a view: each weld with a line in it, seen or hidden.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from; those that are not welds are passed over.</param>
        /// <param name="frame">The frame the view was projected in.</param>
        /// <param name="settings">The height of the text on paper and the standard.</param>
        /// <param name="scale">The scale the view is drawn at: 20 for 1:20.</param>
        /// <returns>The symbols, in the ordinal order of their welds' ids.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view, the objects or the settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is not a frame of three unit axes square to each other, an
        /// object is null or two objects have the same id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        /// <exception cref="NotSupportedException">Thrown when the settings ask for <see cref="WeldStandard.AwsA24"/>: which
        /// standard the drawings use is open question Q3 of the drawing plan, and only ISO 2553 is drawn until it is answered.</exception>
        public static IReadOnlyList<WeldSymbol> Find(ProjectedView2 view, IEnumerable<DrawingObject> objects, GeoCoordinateSystem3 frame, SymbolSettings settings, double scale)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            if (!frame.IsValid)
            {
                throw new ArgumentException("The frame has to have three unit axes square to each other.", nameof(frame));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale has to be a finite number above nought.");
            }

            if (settings.WeldStandard != WeldStandard.Iso2553)
            {
                throw new NotSupportedException("Weld symbols are drawn to ISO 2553 only for now: whether the drawings use ISO 2553 or AWS A2.4 is open question Q3 of the drawing plan.");
            }

            var welds = new List<DrawingObject>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (DrawingObject item in objects)
            {
                if (item == null)
                {
                    throw new ArgumentException("An object of a view cannot be null.", nameof(objects));
                }

                if (!ids.Add(item.Id))
                {
                    throw new ArgumentException("The id \"" + item.Id + "\" is given to two objects.", nameof(objects));
                }

                if (item.Kind == DrawingObjectKind.Weld)
                {
                    welds.Add(item);
                }
            }

            welds.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var inView = new HashSet<string>(StringComparer.Ordinal);

            foreach (IReadOnlyList<ProjectedEdge2> list in new[] { view.VisibleEdges, view.HiddenEdges })
            {
                foreach (ProjectedEdge2 edge in list)
                {
                    foreach (string id in edge.ObjectIds)
                    {
                        inView.Add(id);
                    }
                }
            }

            double height = settings.TextHeight * scale;
            var symbols = new List<WeldSymbol>();
            var skipped = new List<string>();

            foreach (DrawingObject weld in welds)
            {
                if (!inView.Contains(weld.Id))
                {
                    continue;
                }

                if (!TryRead(weld, TypeAttribute, WeldType.Fillet, out WeldType type) || !TryRead(weld, SideAttribute, WeldSide.Arrow, out WeldSide side))
                {
                    skipped.Add(weld.Id);
                    continue;
                }

                GeoAabb3 box = weld.Solid.GetAabb();
                var middle = new GeoPoint3((box.Min.X + box.Max.X) / 2.0, (box.Min.Y + box.Max.Y) / 2.0, (box.Min.Z + box.Max.Z) / 2.0);
                GeoPoint3 local = frame.ToLocal(middle);
                var parts = new List<string> { weld.Id };

                if (weld.ParentId != null)
                {
                    parts.Add(weld.ParentId);
                }

                if (weld.Attributes.TryGetValue(SecondaryPartAttribute, out string secondary) && secondary.Length > 0)
                {
                    parts.Add(secondary);
                }

                weld.Attributes.TryGetValue(SizeAttribute, out string size);
                weld.Attributes.TryGetValue(LengthAttribute, out string length);
                var links = new DrawingLinks(parts, new[] { new DrawingLinkPoint3(weld.Id, middle) });
                symbols.Add(new WeldSymbol(weld.Id, type, side, size ?? string.Empty, length ?? string.Empty, new GeoPoint2(local.X, local.Y), height, links));
            }

            if (skipped.Count > 0)
            {
                GeometryHelperLog.Debug(string.Format(CultureInfo.InvariantCulture, "WeldSymbol.Find: {0} welds of a kind or side not known got no symbol: {1}.", skipped.Count, string.Join(", ", skipped)));
            }

            GeometryHelperLog.Info(string.Format(CultureInfo.InvariantCulture, "WeldSymbol.Find: {0} welds gave {1} symbols.", welds.Count, symbols.Count));
            return symbols;
        }

        /// <summary>
        /// Writes the symbol into the model space of a drawing: the arrow line and the reference line as LINEs, the arrow as a
        /// closed filled SOLID, the dashed identification line as a LINE with the linetype HIDDEN, the basic symbol as LINEs,
        /// and the size and the length as MTEXTs.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// As ISO 2553 system A: a weld on the arrow side has its symbol standing on the continuous line, the dashed line
        /// half the text's height under it; one on the other side has its symbol hanging from the dashed line; one on both
        /// sides has a symbol each side of the continuous line and no dashed line. A fillet is a right triangle, its upright
        /// side on the left; a square butt weld two uprights; a V butt weld a V, its point on the line; a bevel butt weld an
        /// upright and a slope from its foot. Everything is on the layer <see cref="SymbolSettings.Layer"/>, made in colour 6
        /// where the drawing has none, and tagged with <see cref="DrawingEntityTags"/>: the symbol's id and the placement's
        /// view id.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            Write(database, placement, Id);
        }

        /// <summary>Writes the symbol as <see cref="WriteTo"/> does, its entities tagged with an id the caller gives.</summary>
        internal void Write(DxfDatabase database, ViewPlacement2 placement, string id)
        {
            SymbolDxf.SymbolLayer(database);
            string layer = SymbolSettings.Layer;
            double h = TextHeight;
            SymbolDxf.Line(database, placement, Knee, ArrowTip, layer, id);
            double run = Knee.DistanceTo(ArrowTip);
            SymbolDxf.Arrow(database, placement, ArrowTip, new GeoVector2((ArrowTip.X - Knee.X) / run, (ArrowTip.Y - Knee.Y) / run), 0.5 * h, layer, id);
            SymbolDxf.Line(database, placement, ReferenceLine.StartPoint, ReferenceLine.EndPoint, layer, id);
            double dashed = Knee.Y - 0.5 * h;

            if (Side != WeldSide.Both)
            {
                SymbolDxf.HiddenLinetype(database);
                DxfLine line = SymbolDxf.Line(database, placement, new GeoPoint2(Knee.X, dashed), new GeoPoint2(ReferenceLine.EndPoint.X, dashed), layer, id);
                line.Linetype = "HIDDEN";
                line.LinetypeScale = SymbolChecks.PatternScale(h, placement);
            }

            if (Side == WeldSide.Arrow || Side == WeldSide.Both)
            {
                Band(database, placement, Knee.Y, 1.0, id);
            }

            if (Side == WeldSide.Other)
            {
                Band(database, placement, dashed, -1.0, id);
            }

            if (Side == WeldSide.Both)
            {
                Band(database, placement, Knee.Y, -1.0, id);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"Weld symbol {Id} {Type} {Side} {Size} {Length}";

        /// <summary>
        /// The strokes of the basic symbol, standing on a line at a height and rising the way a sign says, up for 1 and down
        /// for -1.
        /// </summary>
        internal GeoLine2[] Strokes(double baseY, double sign)
        {
            double x = SymbolStart, h = TextHeight, top = baseY + sign * h;

            switch (Type)
            {
                case WeldType.SquareButt:
                    return new[] { new GeoLine2(x + 0.25 * h, baseY, x + 0.25 * h, top), new GeoLine2(x + 0.75 * h, baseY, x + 0.75 * h, top) };
                case WeldType.VButt:
                    return new[] { new GeoLine2(x, top, x + 0.5 * h, baseY), new GeoLine2(x + 0.5 * h, baseY, x + h, top) };
                case WeldType.BevelButt:
                    return new[] { new GeoLine2(x, baseY, x, top), new GeoLine2(x, baseY, x + 0.7 * h, top) };
                default:
                    return new[] { new GeoLine2(x, baseY, x, top), new GeoLine2(x, top, x + h, baseY), new GeoLine2(x, baseY, x + h, baseY) };
            }
        }

        /// <summary>The basic symbol on one side of a line, with the size left of it and the length right of it.</summary>
        private void Band(DxfDatabase database, ViewPlacement2 placement, double baseY, double sign, string id)
        {
            string layer = SymbolSettings.Layer;
            double h = TextHeight;

            foreach (GeoLine2 stroke in Strokes(baseY, sign))
            {
                SymbolDxf.Line(database, placement, stroke.StartPoint, stroke.EndPoint, layer, id);
            }

            double middle = baseY + sign * 0.5 * h;

            if (Size.Length > 0)
            {
                double width = SymbolDxf.TextWidth(Size, h);
                SymbolDxf.Text(database, placement, new GeoPoint2(SymbolStart - 0.3 * h - 0.5 * width, middle), h, Size, layer, id);
            }

            if (Length.Length > 0)
            {
                double width = SymbolDxf.TextWidth(Length, h);
                SymbolDxf.Text(database, placement, new GeoPoint2(SymbolStart + 1.3 * h + 0.5 * width, middle), h, Length, layer, id);
            }
        }

        /// <summary>Reads an attribute as a value of an enum, letters of either case the same; its default where it is not given.</summary>
        private static bool TryRead<T>(DrawingObject weld, string attribute, T fallback, out T value)
            where T : struct
        {
            value = fallback;

            if (!weld.Attributes.TryGetValue(attribute, out string written))
            {
                return true;
            }

            foreach (T known in (T[])Enum.GetValues(typeof(T)))
            {
                if (string.Equals(known.ToString(), written.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    value = known;
                    return true;
                }
            }

            return false;
        }
    }
}
