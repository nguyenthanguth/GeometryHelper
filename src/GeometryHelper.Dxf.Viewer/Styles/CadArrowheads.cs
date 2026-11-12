using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// AutoCAD's arrowheads of dimensions and leaders: the names its Dimension Style Manager lists, the blocks it makes for
    /// them when a style or a leader first uses one (<c>_ArchTick</c>, <c>_Dot</c>), and their shapes, one unit long with the
    /// tip at the origin and the body towards -X, entity for entity as AutoCAD 2021 writes them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Closed filled has no block: a style names none for it. Each other block holds what AutoCAD 2021 puts in it, on layer 0,
    /// colour and linetype ByBlock, its lines, circles and arcs of lineweight ByBlock: Closed is the three sides of a triangle
    /// one long and a third wide with a line down its middle; Dot a polyline of two half circles 0.5 wide about the tip, which
    /// fills a disc of radius 0.5, and a line from -0.5 to -1; Architectural tick a polyline 0.15 wide from (-0.5, -0.5) to
    /// (0.5, 0.5). A dimension's picture and the presenter draw an arrowhead from the same shapes, placed at its tip and scaled
    /// by its size.
    /// </para>
    /// <para>
    /// A style that names a block of another name (a user arrow) is listed under that name and drawn closed filled in a
    /// dimension's picture.
    /// </para>
    /// </remarks>
    public static class CadArrowheads
    {
        /// <summary>The name of AutoCAD's own arrowhead, which needs no block.</summary>
        public const string ClosedFilled = "Closed filled";

        // Half the width of the closed arrowheads and Open, the half height of Open 30 and of the datum triangles, as AutoCAD
        // 2021 writes them (tan 15 and tan 30 degrees to eight places).
        private const double Sixth = 1.0 / 6.0;
        private const double Open30 = 0.26794919;
        private const double Datum = 0.57735027;

        private static readonly KeyValuePair<string, string>[] Table =
        {
            new KeyValuePair<string, string>(ClosedFilled, string.Empty),
            new KeyValuePair<string, string>("Closed blank", "_ClosedBlank"),
            new KeyValuePair<string, string>("Closed", "_Closed"),
            new KeyValuePair<string, string>("Dot", "_Dot"),
            new KeyValuePair<string, string>("Architectural tick", "_ArchTick"),
            new KeyValuePair<string, string>("Oblique", "_Oblique"),
            new KeyValuePair<string, string>("Open", "_Open"),
            new KeyValuePair<string, string>("Origin indicator", "_Origin"),
            new KeyValuePair<string, string>("Origin indicator 2", "_Origin2"),
            new KeyValuePair<string, string>("Right angle", "_Open90"),
            new KeyValuePair<string, string>("Open 30", "_Open30"),
            new KeyValuePair<string, string>("Dot small", "_DotSmall"),
            new KeyValuePair<string, string>("Dot blank", "_DotBlank"),
            new KeyValuePair<string, string>("Dot small blank", "_Small"),
            new KeyValuePair<string, string>("Box", "_BoxBlank"),
            new KeyValuePair<string, string>("Box filled", "_BoxFilled"),
            new KeyValuePair<string, string>("Datum triangle", "_DatumBlank"),
            new KeyValuePair<string, string>("Datum triangle filled", "_DatumFilled"),
            new KeyValuePair<string, string>("Integral", "_Integral"),
            new KeyValuePair<string, string>("None", "_None"),
        };

        /// <summary>Gets the names of the arrowheads, in the order of AutoCAD's list: Closed filled, Closed blank, Closed, Dot, ... None.</summary>
        public static IReadOnlyList<string> Names { get; } = Array.ConvertAll(Table, p => p.Key);

        /// <summary>Gets the name of the arrowhead a style's block draws: Closed filled for none, the block's own name for a user arrow.</summary>
        /// <param name="block">The block a style names, or null.</param>
        /// <returns>The name, as "Architectural tick".</returns>
        public static string NameOf(DxfBlockTableRecord block)
        {
            if (block == null)
            {
                return ClosedFilled;
            }

            string name = block.Name ?? string.Empty;

            foreach (KeyValuePair<string, string> pair in Table)
            {
                if (pair.Value.Length > 0 && string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Key;
                }
            }

            return name;
        }

        /// <summary>
        /// The name AutoCAD 2021's LIST gives the arrowhead a dimension's override names: the name of its own block without the
        /// underscore, as ArchTick for <c>_ArchTick</c> and Open90 for <c>_Open90</c>; ClosedFilled for none; the block's own
        /// name for a user arrow.
        /// </summary>
        internal static string ListedName(DxfBlockTableRecord block)
        {
            if (block == null)
            {
                return "ClosedFilled";
            }

            string name = block.Name ?? string.Empty;

            foreach (KeyValuePair<string, string> pair in Table)
            {
                if (pair.Value.Length > 0 && string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value.Substring(1);
                }
            }

            return name;
        }

        /// <summary>
        /// Gets the block of an arrowhead in a drawing, made as AutoCAD makes it the first time a style or a leader uses it:
        /// <c>_ArchTick</c> for Architectural tick, its shape one unit long (<see cref="CadArrowheads"/>); null for Closed
        /// filled. A drawing that has the block, by a name of either case, keeps its own. A user arrow is the block of that
        /// name, where the drawing has it. Call it inside an edit of the document, as it may add a block.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The name of the arrowhead, as <see cref="Names"/> lists it, letters of either case the same.</param>
        /// <returns>The block, or null for Closed filled and for a name the drawing has no block of.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static DxfBlockTableRecord Ensure(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            string block = BlockOf(name);

            if (block == null)
            {
                return name != null && database.BlockTable.Has(name) ? database.BlockTable[name] : null;
            }

            if (block.Length == 0)
            {
                return null;
            }

            if (database.BlockTable.Has(block))
            {
                return database.BlockTable[block];
            }

            var record = new DxfBlockTableRecord { Name = block };
            database.BlockTable.Add(record);

            // AutoCAD's lineweight ByBlock from R2000, which brought lineweights; a POLYLINE before R14, which has no LWPOLYLINE.
            string version = database.Version ?? string.Empty;
            bool weights = string.CompareOrdinal(version, "AC1015") >= 0;
            bool lightweight = string.CompareOrdinal(version, "AC1014") >= 0;

            foreach (
                DxfEntity entity in Shape(
                    block,
                    new GeoPoint2(0.0, 0.0),
                    new GeoVector2(1.0, 0.0),
                    1.0,
                    weights,
                    lightweight
                )
            )
            {
                record.AppendEntity(entity);
            }

            return record;
        }

        /// <summary>Whether the dimension line runs to the tip of the arrowhead and on past the extension lines by DIMDLE: the ticks, Oblique and Architectural tick.</summary>
        internal static bool IsTick(string blockName) =>
            string.Equals(blockName, "_ARCHTICK", StringComparison.OrdinalIgnoreCase)
            || string.Equals(blockName, "_OBLIQUE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Whether a dimension's line runs on to the tip of the arrowhead, which takes no room between the extension lines and
        /// never moves out: the ticks, Dot small, Dot small blank, Integral and None, as AutoCAD 2021 draws them. Every other
        /// arrowhead, closed filled too, has the line start at its back, an arrowhead in from the tip.
        /// </summary>
        internal static bool TakesNoRoom(string blockName)
        {
            switch ((blockName ?? string.Empty).ToUpperInvariant())
            {
                case "_ARCHTICK":
                case "_OBLIQUE":
                case "_DOTSMALL":
                case "_SMALL":
                case "_INTEGRAL":
                case "_NONE":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The entities of an arrowhead of a block name, letters of either case the same, its tip at a point, pointing a way of
        /// unit length, a size long, as AutoCAD's block of it holds them (see <see cref="CadArrowheads"/>): closed filled, the
        /// SOLID of a triangle a third as wide as long, for no name and for a name not AutoCAD's; none for <c>_None</c>. A
        /// dimension's picture takes them with the lineweight it is drawn with (<paramref name="weights"/> false) and its dots
        /// and ticks as LWPOLYLINEs; a block of a file before R14 holds them as POLYLINEs.
        /// </summary>
        internal static List<DxfEntity> Shape(
            string blockName,
            GeoPoint2 tip,
            GeoVector2 way,
            double size,
            bool weights = false,
            bool lightweight = true
        )
        {
            var parts = new Shapes(tip, way, size, weights, lightweight);

            switch ((blockName ?? string.Empty).ToUpperInvariant())
            {
                case "_CLOSEDBLANK":
                    parts.Line(-1, Sixth, 0, 0).Line(0, 0, -1, -Sixth).Line(-1, Sixth, -1, -Sixth);
                    break;
                case "_CLOSED":
                    parts.Line(-1, Sixth, 0, 0).Line(0, 0, -1, -Sixth).Line(-1, Sixth, -1, -Sixth).Line(0, 0, -1, 0);
                    break;
                case "_DOT":
                    parts.Donut(0.25).Line(-0.5, 0, -1, 0);
                    break;
                case "_ARCHTICK":
                    parts.Wide(0.15, -0.5, -0.5, 0.5, 0.5);
                    break;
                case "_OBLIQUE":
                    parts.Line(-0.5, -0.5, 0.5, 0.5);
                    break;
                case "_OPEN":
                    parts.Line(-1, Sixth, 0, 0).Line(-1, -Sixth, 0, 0).Line(0, 0, -1, 0);
                    break;
                case "_ORIGIN":
                    parts.Circle(0.5).Line(0, 0, -1, 0);
                    break;
                case "_ORIGIN2":
                    parts.Circle(0.5).Circle(0.25).Line(-0.5, 0, -1, 0);
                    break;
                case "_OPEN90":
                    parts.Line(-0.5, 0.5, 0, 0).Line(0, 0, -0.5, -0.5).Line(0, 0, -1, 0);
                    break;
                case "_OPEN30":
                    parts.Line(-1, Open30, 0, 0).Line(0, 0, -1, -Open30).Line(0, 0, -1, 0);
                    break;
                case "_DOTSMALL":
                    parts.Donut(0.0625);
                    break;
                case "_DOTBLANK":
                    parts.Circle(0.5).Line(-0.5, 0, -1, 0);
                    break;
                case "_SMALL":
                    parts.Circle(0.25);
                    break;
                case "_BOXBLANK":
                    parts
                        .Line(-0.5, -0.5, 0.5, -0.5)
                        .Line(0.5, -0.5, 0.5, 0.5)
                        .Line(0.5, 0.5, -0.5, 0.5)
                        .Line(-0.5, 0.5, -0.5, -0.5)
                        .Line(-0.5, 0, -1, 0);
                    break;
                case "_BOXFILLED":
                    parts.Solid(-0.5, 0.5, 0.5, 0.5, -0.5, -0.5, 0.5, -0.5).Line(-0.5, 0, -1, 0);
                    break;
                case "_DATUMBLANK":
                    parts.Line(0, Datum, -1, 0).Line(-1, 0, 0, -Datum).Line(0, -Datum, 0, Datum);
                    break;
                case "_DATUMFILLED":
                    parts.Solid(0, Datum, -1, 0, 0, -Datum, 0, -Datum);
                    break;
                case "_INTEGRAL":
                    // 102 to 168 and 282 to 348 degrees, in AutoCAD's radians of ten places.
                    parts
                        .Arc(0.44488802, -0.09133463, 0.45416667, 1.780235837, 2.932153143)
                        .Arc(-0.44488802, 0.09133463, 0.45416667, 4.921828491, 6.073745797);
                    break;
                case "_NONE":
                    break;
                default:
                    parts.Solid(0, 0, -1, Sixth, -1, -Sixth, -1, -Sixth);
                    break;
            }

            return parts.All;
        }

        /// <summary>The block name of an arrowhead's name, letters of either case the same: empty for Closed filled, null for a name not AutoCAD's.</summary>
        internal static string BlockOf(string name)
        {
            foreach (KeyValuePair<string, string> pair in Table)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// The entities of one arrowhead, each given in the frame of the unit arrowhead (x along the arrow from its tip, y to its
        /// left) and placed at the tip, turned the way it points and scaled by its size.
        /// </summary>
        private sealed class Shapes
        {
            private readonly GeoPoint2 _tip;
            private readonly GeoVector2 _x;
            private readonly GeoVector2 _y;
            private readonly double _size;
            private readonly double _turn;
            private readonly bool _weights;
            private readonly bool _lightweight;

            internal Shapes(GeoPoint2 tip, GeoVector2 way, double size, bool weights, bool lightweight)
            {
                _weights = weights;
                _lightweight = lightweight;
                _tip = tip;
                _x = way * size;
                _y = new GeoVector2(-_x.Y, _x.X);
                _size = size;
                _turn = Math.Atan2(way.Y, way.X);
            }

            internal List<DxfEntity> All { get; } = new List<DxfEntity>();

            internal Shapes Line(double x1, double y1, double x2, double y2) =>
                Add(new DxfLine(At(x1, y1), At(x2, y2)), true);

            internal Shapes Circle(double radius) =>
                Add(new DxfCircle(At(0, 0), new GeoVector3(0, 0, 1), radius * _size), true);

            /// <summary>An arc about a centre, its angles in radians in the unit frame, counter-clockwise from the start.</summary>
            internal Shapes Arc(double cx, double cy, double radius, double start, double end) =>
                Add(new DxfArc(At(cx, cy), radius * _size, Turned(start + _turn), Turned(end + _turn)), true);

            /// <summary>A SOLID of four corners in DXF's order (10, 11, 12, 13).</summary>
            internal Shapes Solid(
                double x1,
                double y1,
                double x2,
                double y2,
                double x3,
                double y3,
                double x4,
                double y4
            ) => Add(new DxfSolid(At(x1, y1), At(x2, y2), At(x3, y3), At(x4, y4)), false);

            /// <summary>A filled dot as AutoCAD writes one: a closed polyline of two half circles through (-h, 0) and (h, 0), 0.5 wide.</summary>
            internal Shapes Donut(double half) => Polyline(0.5, true, 1.0, -half, 0.0, half, 0.0);

            /// <summary>A straight polyline of a constant width through points given as x, y pairs.</summary>
            internal Shapes Wide(double width, params double[] xy) => Polyline(width, false, 0.0, xy);

            private Shapes Polyline(double width, bool closed, double bulge, params double[] xy)
            {
                double wide = width * _size;
                var points = new List<GeoPoint3>();

                for (int i = 0; i + 1 < xy.Length; i += 2)
                {
                    points.Add(At(xy[i], xy[i + 1]));
                }

                if (!_lightweight)
                {
                    return Add(
                        new DxfPolyline2d(
                            DxfPoly2dType.SimplePoly,
                            points,
                            0.0,
                            closed,
                            wide,
                            wide,
                            points.ConvertAll(p => bulge)
                        ),
                        false
                    );
                }

                var polyline = new DxfPolyline();

                // The width of the whole polyline (43) and of each vertex (40, 41), as AutoCAD writes both.
                polyline.ConstantWidth = wide;

                foreach (GeoPoint3 p in points)
                {
                    polyline.AddVertexAt(polyline.NumberOfVertices, new GeoPoint2(p.X, p.Y), bulge, wide, wide);
                }

                polyline.Closed = closed;
                return Add(polyline, false);
            }

            /// <summary>Gives a part AutoCAD's look of an arrowhead's: colour and linetype ByBlock, and a curve its lineweight ByBlock where weights are written.</summary>
            private Shapes Add(DxfEntity part, bool weighted)
            {
                part.Color = DxfColor.ByBlock;
                part.Linetype = "ByBlock";

                if (weighted && _weights)
                {
                    part.LineWeight = DxfLineWeight.ByBlock;
                }

                All.Add(part);
                return this;
            }

            private GeoPoint3 At(double x, double y) => CadCommands.P3(_tip + _x * x + _y * y);

            private static double Turned(double angle)
            {
                angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
                return angle >= 2.0 * Math.PI ? 0.0 : angle;
            }
        }
    }
}
