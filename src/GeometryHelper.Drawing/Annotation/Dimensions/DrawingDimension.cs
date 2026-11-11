using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// One dimension of a view: the points of the model it measures between, its value worked out from them, and where it is
    /// drawn, in view millimetres at 1:1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A linear dimension measures along the view's X or Y between two points: the overall length of a beam 3 000 long
    /// measures from (0, 0, 0) to (3000, 0, 0) of the beam, 3 000 along X, and is drawn on a line above the view with an
    /// extension line down to each point. A cumulative or ordinate dimension measures from an origin to one point along X
    /// or Y and is drawn as a leader from the point out to its tier, its text at the leader's end.
    /// </para>
    /// <para>
    /// The value is worked out from the model points, along the axis of the view, so that a dimension is right to the bit of
    /// the model whatever the lines of the view were rounded to; its text is rounded as the settings say. A dimension does
    /// not change once made.
    /// </para>
    /// </remarks>
    public sealed class DrawingDimension
    {
        /// <summary>The name of the layer dimensions are written on.</summary>
        public const string Layer = "Dimension";

        private readonly DimensionPoint[] _points;
        private readonly GeoLine2[] _extensionLines;
        private GeoRectangle2[] _arrowheads;

        /// <summary>
        /// Initializes a dimension placed in its view.
        /// </summary>
        internal DrawingDimension(
            string id,
            DimensionKind kind,
            DimensionPoint[] points,
            double value,
            int count,
            string text,
            GeoVector2 direction,
            int tier,
            double offset,
            GeoLine2? dimensionLine,
            GeoLine2[] extensionLines,
            GeoRectangle2 textBox,
            double textRotation,
            double textHeight,
            double scale,
            double rounding
        )
        {
            Id = id;
            Kind = kind;
            _points = points;
            Value = value;
            Count = count;
            Text = text;
            Direction = direction;
            Tier = tier;
            Offset = offset;
            DimensionLine = dimensionLine;
            _extensionLines = extensionLines;
            TextBox = textBox;
            TextRotation = textRotation;
            TextHeight = textHeight;
            Scale = scale;
            Rounding = rounding;

            var ids = new List<string>();

            foreach (DimensionPoint point in points)
            {
                if (!ids.Contains(point.ObjectId))
                {
                    ids.Add(point.ObjectId);
                }
            }

            var links = new DrawingLinkPoint3[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                links[i] = points[i].ToLink();
            }

            Links = new DrawingLinks(ids, links);
        }

        /// <summary>Gets the id of the dimension, unique among the dimensions of its view: D-1, D-2 and on.</summary>
        public string Id { get; }

        /// <summary>Gets which rule made the dimension.</summary>
        public DimensionKind Kind { get; }

        /// <summary>
        /// Gets the points measured: for a linear dimension the two it runs between, the lower along its direction first;
        /// for a cumulative or ordinate one the origin, then the point measured to.
        /// </summary>
        public IReadOnlyList<DimensionPoint> Points => _points;

        /// <summary>Gets what the dimension is tied to in the model: the objects of its points, in their order, and the points.</summary>
        public DrawingLinks Links { get; }

        /// <summary>
        /// Gets the value, in model millimetres: the distance between the model points along <see cref="Direction"/>, every
        /// digit kept.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets how many equal steps the dimension stands for: 3 for "3 x 100 = 300", 1 for a plain one.
        /// </summary>
        public int Count { get; }

        /// <summary>Gets the text written: the value rounded as the settings say, "2800" or "3 x 100 = 300".</summary>
        public string Text { get; }

        /// <summary>Gets the direction measured along in the view: (1, 0) along its X, (0, 1) along its Y.</summary>
        public GeoVector2 Direction { get; }

        /// <summary>Gets the tier the dimension stands on, nought nearest the view, counted on its side of the view.</summary>
        public int Tier { get; }

        /// <summary>
        /// Gets how far the dimension's line, or the end of its leader, stands off the box of the view's lines, in view
        /// millimetres.
        /// </summary>
        public double Offset { get; }

        /// <summary>
        /// Gets the line the arrows are drawn on, in view millimetres; null for a cumulative or ordinate dimension, which has
        /// a leader only.
        /// </summary>
        public GeoLine2? DimensionLine { get; }

        /// <summary>
        /// The dimension line as it is drawn, for keeping clear of it: one shorter than two arrows, which has its arrows outside,
        /// run on by an arrow's length, the text's height, at each end; null for a dimension with no line.
        /// </summary>
        internal GeoLine2? DrawnLine
        {
            get
            {
                if (!DimensionLine.HasValue)
                {
                    return null;
                }

                GeoLine2 line = DimensionLine.Value;
                double length = line.Length,
                    arrow = TextHeight;

                if (length >= 2.0 * arrow || !(length > 0.0))
                {
                    return line;
                }

                double ux = (line.EndPoint.X - line.StartPoint.X) / length * arrow,
                    uy = (line.EndPoint.Y - line.StartPoint.Y) / length * arrow;
                return new GeoLine2(
                    line.StartPoint.X - ux,
                    line.StartPoint.Y - uy,
                    line.EndPoint.X + ux,
                    line.EndPoint.Y + uy
                );
            }
        }

        /// <summary>
        /// The arrowheads as they are drawn, for keeping clear of them: the box round each, square to the view's axes, as long
        /// as the text is high and a third of that wide, its tip at an end of the dimension line; pointing out of a line at
        /// least two arrows long, into a shorter one from outside it, as <see cref="DimensionBlockBuilder"/> draws them. The
        /// arrows of a dimension 300 long at 1:10, its text 2.5 high on paper and so 25 in the view, are boxes 25 by 8.33
        /// inside its ends. None for a dimension with no line.
        /// </summary>
        internal IReadOnlyList<GeoRectangle2> Arrowheads
        {
            get
            {
                if (_arrowheads != null)
                {
                    return _arrowheads;
                }

                return _arrowheads = DimensionLine.HasValue
                    ? ArrowheadsOf(DimensionLine.Value, TextHeight)
                    : new GeoRectangle2[0];
            }
        }

        /// <summary>
        /// Gets the extension lines, from just off each point out to just past the dimension line; for a cumulative or ordinate
        /// dimension the one leader, from just off its point to its tier.
        /// </summary>
        public IReadOnlyList<GeoLine2> ExtensionLines => _extensionLines;

        /// <summary>Gets the middle of the text, in view millimetres.</summary>
        public GeoPoint2 TextPosition => TextBox.Center;

        /// <summary>Gets the angle of the text from the view's X, in radians: nought, or a right angle to read from the right.</summary>
        public double TextRotation { get; }

        /// <summary>
        /// Gets the box the text is taken to fill, square to the view's axes, in view millimetres: each character 0.8 of the
        /// height wide.
        /// </summary>
        public GeoRectangle2 TextBox { get; }

        /// <summary>Gets the height of the text in the view, in view millimetres: the height on paper times <see cref="Scale"/>.</summary>
        public double TextHeight { get; }

        /// <summary>Gets the scale the dimension was laid out for: 20 for 1:20.</summary>
        public double Scale { get; }

        /// <summary>Gets what the text is rounded to, in model millimetres.</summary>
        public double Rounding { get; }

        /// <summary>
        /// Writes the dimension into the model space of a drawing as a DIMENSION, drawn by a block of its own so that the
        /// presenter and AutoCAD show it as it is laid out.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="placement">Where the view is drawn on the sheet, and at what scale.</param>
        /// <remarks>
        /// <para>
        /// A linear dimension is written as a rotated DIMENSION, along the sheet's X or Y, a cumulative or ordinate one as an
        /// ordinate DIMENSION from its origin, both on the layer <see cref="Layer"/> (made in colour 3 where the drawing has
        /// none) and tagged with <see cref="DrawingEntityTags"/>: the dimension's id and the placement's view id. The
        /// points go onto the sheet at the placement's scale, and the dimension is drawn in the dimension style GH_ and the
        /// scale, GH_20 at 1:20, whose DIMLFAC is the scale, so that it reads the model's length: the 3 000 of a beam drawn
        /// at 1:20 is 150 on the sheet and reads 3000. The style is made from the first dimension written at its scale,
        /// where the drawing has none of its name: the text and arrows as high as this dimension's on paper, the extension
        /// lines a quarter of that off their points and half of it past their line, rounded as this dimension is, trailing
        /// zeros left out.
        /// </para>
        /// <para>
        /// A merged dimension keeps its count in its text, "3 x 100 = &lt;&gt;", so that AutoCAD writes the total it measures.
        /// The block that draws it, the extension lines, the dimension line, the arrows as SOLIDs and the text as an MTEXT,
        /// is made with it, an anonymous block named as AutoCAD names its own: *D1, *D2 and on.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public void WriteTo(DxfDatabase database, ViewPlacement2 placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (!database.LayerTable.Has(Layer))
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = Layer, ColorIndex = 3 });
            }

            DxfDimStyleTableRecord style = Style(database, placement.Scale);
            string text =
                Count > 1
                    ? Count.ToString(CultureInfo.InvariantCulture) + " x " + Format(Value / Count, Rounding) + " = <>"
                    : "<>";
            DxfDimension dimension;

            if (DimensionLine.HasValue)
            {
                GeoLine2 line = DimensionLine.Value;
                double rotation = Direction.X > 0.5 ? 0.0 : Math.PI / 2.0;
                dimension = new DxfRotatedDimension(
                    rotation,
                    placement.ToSheet(_points[0].ViewPoint),
                    placement.ToSheet(_points[1].ViewPoint),
                    placement.ToSheet(line.EndPoint),
                    text,
                    style
                );
            }
            else
            {
                var ordinate = new DxfOrdinateDimension(
                    Direction.X > 0.5,
                    placement.ToSheet(_points[1].ViewPoint),
                    placement.ToSheet(_extensionLines[0].EndPoint),
                    text,
                    style
                )
                {
                    Origin = placement.ToSheet(_points[0].ViewPoint),
                };

                dimension = ordinate;
            }

            dimension.Layer = Layer;
            dimension.TextPosition = placement.ToSheet(TextPosition);
            dimension.DimBlock = DimensionBlockBuilder.Build(database, this, placement);
            database.ModelSpace.AppendEntity(dimension);
            DrawingEntityTags.Tag(dimension, Id, placement.ViewId);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"{Kind} {Id} {Text} ({string.Join(" - ", Array.ConvertAll(_points, p => p.ToString()))})";

        /// <summary>
        /// A value rounded to a step and written in as few decimals as the step has, trailing zeros left out: 2 800.4 to 1 is
        /// 2800, to 0.5 is 2800.5.
        /// </summary>
        internal static string Format(double value, double rounding)
        {
            double rounded = Math.Round(value / rounding, MidpointRounding.AwayFromZero) * rounding;
            int decimals = Decimals(rounding);
            string format = decimals == 0 ? "0" : "0." + new string('#', decimals);
            string written = rounded.ToString(format, CultureInfo.InvariantCulture);
            return written == "-0" ? "0" : written;
        }

        /// <summary>The decimals a step of rounding has, up to eight: 1 has none, 0.5 one, 0.25 two.</summary>
        internal static int Decimals(double rounding)
        {
            for (int d = 0; d < 8; d++)
            {
                double scaled = rounding * Math.Pow(10.0, d);

                if (Math.Abs(scaled - Math.Round(scaled)) <= 1E-9 * Math.Max(1.0, scaled))
                {
                    return d;
                }
            }

            return 8;
        }

        /// <summary>
        /// The boxes round the two arrowheads of a dimension line, each an arrow's length long: pointing out of a line at least
        /// two arrows long, into a shorter one from outside it.
        /// </summary>
        internal static GeoRectangle2[] ArrowheadsOf(GeoLine2 line, double arrow)
        {
            double length = line.Length;
            double ux = length > 0.0 ? (line.EndPoint.X - line.StartPoint.X) / length : 1.0;
            double uy = length > 0.0 ? (line.EndPoint.Y - line.StartPoint.Y) / length : 0.0;
            double inward = length >= 2.0 * arrow ? 1.0 : -1.0;
            return new[]
            {
                ArrowBox(line.StartPoint, inward * ux, inward * uy, arrow),
                ArrowBox(line.EndPoint, -inward * ux, -inward * uy, arrow),
            };
        }

        /// <summary>The box round an arrow, its tip at a point, its back an arrow's length along a unit direction from it.</summary>
        private static GeoRectangle2 ArrowBox(GeoPoint2 tip, double bx, double by, double length)
        {
            double backX = tip.X + bx * length,
                backY = tip.Y + by * length;
            double acrossX = Math.Abs(by) * length / 6.0,
                acrossY = Math.Abs(bx) * length / 6.0;
            double x0 = Math.Min(tip.X, backX - acrossX),
                x1 = Math.Max(tip.X, backX + acrossX);
            double y0 = Math.Min(tip.Y, backY - acrossY),
                y1 = Math.Max(tip.Y, backY + acrossY);
            return Box2.FromCorner(x0, y0, x1 - x0, y1 - y0);
        }

        private DxfDimStyleTableRecord Style(DxfDatabase database, double scale)
        {
            string name = "GH_" + scale.ToString("0.######", CultureInfo.InvariantCulture);

            if (database.DimStyleTable.Has(name))
            {
                return (DxfDimStyleTableRecord)database.DimStyleTable[name];
            }

            double height = TextHeight / scale;
            var style = new DxfDimStyleTableRecord
            {
                Name = name,
                Dimlfac = scale,
                Dimtxt = height,
                Dimasz = height,
                Dimexo = height / 4.0,
                Dimexe = height / 2.0,
                Dimgap = height / 4.0,
                Dimlunit = 2,
                Dimdec = Decimals(Rounding),
                Dimrnd = Rounding,
                Dimzin = 8,
                Dimdsep = '.',
                Dimtad = 1,
                Dimtih = false,
                Dimtoh = false,
                Dimtofl = true,
            };

            database.DimStyleTable.Add(style);
            return style;
        }
    }
}
