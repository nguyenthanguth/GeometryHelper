using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The dimension commands: DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR.</summary>
    /// <remarks>
    /// The library does not draw a dimension: what draws one is its block (<c>*D1</c>), which AutoCAD draws and names itself.
    /// So a dimension is made with its picture (its extension lines, dimension line, arrowheads as SOLIDs, its words as an
    /// MTEXT and its points of definition on Defpoints, from the variables of the current dimension style) in an anonymous
    /// block added as <c>*D</c>, which the block table names as AutoCAD names its own, the first <c>*D</c> number the drawing
    /// has not. The height of the words is the style's DIMTXT, or the host's default where it sets one ("dim.text.height" of
    /// <see cref="CadCommandSession.Remembered"/>, as the presenter sets Global settings' text height), written then as an
    /// override of the dimension.
    /// </remarks>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> DimLinear(CadCommandContext c) => DimLine(c, false);

        private static IEnumerable<CadPrompt> DimAligned(CadCommandContext c) => DimLine(c, true);

        /// <summary>
        /// DIMLINEAR and DIMALIGNED: two origins (or a line selected), then where the line goes. DIM gives the first origin it
        /// was answered with as <paramref name="first"/>, and the command goes on from there.
        /// </summary>
        private static IEnumerable<CadPrompt> DimLine(CadCommandContext c, bool aligned, GeoPoint2? first = null)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            if (!first.HasValue)
            {
                yield return CadPrompt.ForPoint("Specify first extension line origin or", null, null, "select object");
            }

            GeoPoint2 p1;
            GeoPoint2 p2;
            var tied = new DxfOsnapPointRef[2];

            if (first.HasValue || c.Input.IsPoint)
            {
                p1 = first ?? c.Input.Point;
                // DIM gives its own answer as the first origin, whose snap is still the input here.
                tied[0] = CadDimAssociation.FromSnap(c.Document, 0, c.Input.Snap);
                yield return CadPrompt.ForPoint("Specify second extension line origin", p1);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                p2 = c.Input.Point;
                tied[1] = CadDimAssociation.FromSnap(c.Document, 1, c.Input.Snap, p1, tied[0]);
            }
            else
            {
                yield return CadPrompt.ForEntity("Select object to dimension");

                if (c.Input.Kind != CadInputKind.Selection || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfLine line))
                {
                    c.Write("Object selected is not a line.");
                    yield break;
                }

                p1 = new GeoPoint2(line.StartPoint.X, line.StartPoint.Y);
                p2 = new GeoPoint2(line.EndPoint.X, line.EndPoint.Y);
                tied = CadDimAssociation.LineEnds(line);
            }

            if ((p2 - p1).Length <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The two origins are one point.");
                yield break;
            }

            string text = null;
            double? fixedAngle = null;

            while (true)
            {
                string[] keywords = aligned ? new[] { "Text" } : new[] { "Text", "Horizontal", "Vertical" };
                yield return CadPrompt.ForPoint("Specify dimension line location", p2, keywords);

                if (c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                if (c.Input.Is("Horizontal"))
                {
                    fixedAngle = 0.0;
                    continue;
                }

                if (c.Input.Is("Vertical"))
                {
                    fixedAngle = Math.PI / 2.0;
                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadDimAssociation.Tie(c, MakeLinear(c, style, aligned, p1, p2, c.Input.Point, text, fixedAngle, null), tied);
        }

        /// <summary>
        /// Makes a linear (or an aligned) dimension of two origins, its line through a point, as DIMLINEAR and DIMALIGNED make
        /// it; its words given or the measurement, turned where <paramref name="textAngle"/> says. The handle of the dimension,
        /// or null where it measures nothing.
        /// </summary>
        private static string MakeLinear(CadCommandContext c, DxfDimStyleTableRecord style, bool aligned, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 location, string text, double? fixedAngle, double? textAngle)
        {
            // A linear dimension measures along the X or Y axis of the UCS, as AutoCAD's: in a UCS turned 30 degrees it is a
            // rotated dimension of 30 or 120 degrees.
            CadUcs ucs = c.Session.Ucs;
            double angle = aligned ? Math.Atan2(p2.Y - p1.Y, p2.X - p1.X) : ucs.AngleToWorld(fixedAngle ?? LinearAngle(ucs.ToUcs(p1), ucs.ToUcs(p2), ucs.ToUcs(location)));
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);

            if (measured <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The dimension measures nothing.");
                return null;
            }

            GeoPoint2 q1 = location + along * ((p1 - location).X * along.X + (p1 - location).Y * along.Y);
            GeoPoint2 q2 = location + along * ((p2 - location).X * along.X + (p2 - location).Y * along.Y);
            DxfDimension made = aligned
                ? (DxfDimension)new DxfAlignedDimension(P3(p1), P3(p2), P3(q2), text ?? string.Empty, null)
                : new DxfRotatedDimension(angle, P3(p1), P3(p2), P3(q2), text ?? string.Empty, null);
            string words = Words(style, made, measured, text, string.Empty);
            c.Write("Dimension text = " + words);
            double textHeight = TextHeight(c, style);
            return AddedHandle(Place(c, style, textHeight, made, WithTextAngle(made, Linear(style, Dimscale(c), textHeight, p1, p2, q1, q2, words), textAngle)));
        }

        /// <summary>Gives a dimension to be made the angle its words were given (Text angle), and its picture's words that turn.</summary>
        private static List<DxfEntity> WithTextAngle(DxfDimension made, List<DxfEntity> picture, double? textAngle)
        {
            if (textAngle.HasValue)
            {
                made.TextRotation = textAngle.Value;

                foreach (DxfEntity entity in picture)
                {
                    if (entity is DxfMText words)
                    {
                        words.Rotation = textAngle.Value;
                    }
                }
            }

            return picture;
        }

        private static IEnumerable<CadPrompt> DimRadial(CadCommandContext c, bool diameter)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            yield return CadPrompt.ForEntity("Select arc or circle");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);
            GeoPoint2 center;
            double radius;

            switch (picked)
            {
                case DxfCircle circle:
                    center = new GeoPoint2(circle.Center.X, circle.Center.Y);
                    radius = circle.Radius;
                    break;
                case DxfArc arc:
                    center = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    radius = arc.Radius;
                    break;
                default:
                    c.Write("Object selected is not a circle or arc.");
                    yield break;
            }

            double measured = diameter ? 2.0 * radius : radius;
            string prefix = diameter ? "%%c" : "R";
            DxfDimension probe = diameter
                ? (DxfDimension)new DxfDiametricDimension(P3(center), P3(center), 0.0, string.Empty, null)
                : new DxfRadialDimension(P3(center), P3(center), 0.0, string.Empty, null);
            string text = null;
            c.Write("Dimension text = " + Words(style, probe, measured, null, prefix));

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify dimension line location", center, new[] { "Text" });

                if (c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            CadDimAssociation.Tie(c, MakeRadial(c, style, center, radius, diameter, c.Input.Point, text, null), CadDimAssociation.CenterOf(picked));
        }

        /// <summary>
        /// Makes a radius (or a diameter) dimension of a circle, its line toward a point, as DIMRADIUS and DIMDIAMETER make it;
        /// its words given or the measurement, turned where <paramref name="textAngle"/> says. The handle of the dimension.
        /// </summary>
        private static string MakeRadial(CadCommandContext c, DxfDimStyleTableRecord style, GeoPoint2 center, double radius, bool diameter, GeoPoint2 location, string text, double? textAngle)
        {
            double measured = diameter ? 2.0 * radius : radius;
            string prefix = diameter ? "%%c" : "R";
            GeoVector2 toward = location - center;

            if (toward.Length <= c.Session.Tolerance.EqualPoint)
            {
                toward = new GeoVector2(1.0, 0.0);
            }

            GeoVector2 unit = toward / toward.Length;
            GeoPoint2 chord = center + unit * radius;
            GeoPoint2 far = center - unit * radius;
            double leader = Math.Max(0.0, (location - chord).Length);
            DxfDimension made = diameter
                ? (DxfDimension)new DxfDiametricDimension(P3(chord), P3(far), leader, text ?? string.Empty, null)
                : new DxfRadialDimension(P3(center), P3(chord), leader, text ?? string.Empty, null);
            string words = Words(style, made, measured, text, prefix);
            double textHeight = TextHeight(c, style);
            return AddedHandle(Place(c, style, textHeight, made, WithTextAngle(made, Radial(style, Dimscale(c), textHeight, center, radius, chord, far, location, diameter, words), textAngle)));
        }

        private static IEnumerable<CadPrompt> DimAngular(CadCommandContext c) => DimAngularFrom(c, null, null);

        /// <summary>
        /// DIMANGULAR: an arc, two lines or a vertex and two points, then where the arc goes. DIM gives the object it was
        /// answered with as <paramref name="given"/>, and the second line where one was picked as <paramref name="secondGiven"/>,
        /// and the command goes on from there.
        /// </summary>
        private static IEnumerable<CadPrompt> DimAngularFrom(CadCommandContext c, DxfEntity given, DxfLine secondGiven)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            if (given == null)
            {
                yield return CadPrompt.ForEntity("Select arc, circle, line, or", null, "specify vertex");
            }

            GeoPoint2 vertex;
            DxfLine first = null;
            DxfArc tiedArc = null;
            DxfLine second = null;

            // The two rays the angle lies between, and the stretch of each its object covers, as distances from the vertex.
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            var points = new GeoPoint2[2];
            var snapped = new DxfOsnapPointRef[3];

            if (given != null || c.Input.Kind == CadInputKind.Selection)
            {
                DxfEntity picked = given ?? c.Document.FindEntity(c.Input.Handles[0]);

                if (picked is DxfArc arc && arc.Normal.Z > 0.0)
                {
                    vertex = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    points[0] = new GeoPoint2(arc.StartPoint.X, arc.StartPoint.Y);
                    points[1] = new GeoPoint2(arc.EndPoint.X, arc.EndPoint.Y);
                    tiedArc = arc;
                }
                else if (picked is DxfLine line)
                {
                    first = line;
                    DxfLine other = secondGiven;

                    if (other == null)
                    {
                        yield return CadPrompt.ForEntity("Select second line");
                        other = c.Input.Kind == CadInputKind.Selection ? c.Document.FindEntity(c.Input.Handles[0]) as DxfLine : null;
                    }

                    if (other == null || ReferenceEquals(other, line))
                    {
                        c.Write("Object selected is not a second line.");
                        yield break;
                    }

                    second = other;

                    if (!Meet(first, second, c.Session.Tolerance, out vertex))
                    {
                        c.Write("Lines are parallel.");
                        yield break;
                    }
                }
                else
                {
                    c.Write("Object selected is not an arc or a line.");
                    yield break;
                }
            }
            else if (c.Input.IsNone)
            {
                yield return CadPrompt.ForPoint("Specify angle vertex");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                vertex = c.Input.Point;
                snapped[2] = CadDimAssociation.FromSnap(c.Document, 2, c.Input.Snap);

                for (int i = 0; i < 2; i++)
                {
                    yield return CadPrompt.ForPoint(i == 0 ? "Specify first angle endpoint" : "Specify second angle endpoint", vertex);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points[i] = c.Input.Point;
                    snapped[i] = CadDimAssociation.FromSnap(c.Document, i, c.Input.Snap);
                }
            }
            else
            {
                yield break;
            }

            string text = null;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify dimension arc line location", vertex, new[] { "Text" });

                if (c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter dimension text", "<>");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        text = c.Input.Text;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 location = c.Input.Point;
            double radius = (location - vertex).Length;
            AngularSector(first, second, vertex, points, location, rays, near, far, out double from, out double sweep);

            if (sweep <= c.Session.Tolerance.EqualAngleRad / 1000.0 || radius <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The angle measures nothing.");
                yield break;
            }

            DxfDimension made = first != null
                ? (DxfDimension)new DxfLineAngularDimension2(first.StartPoint, first.EndPoint, second.StartPoint, second.EndPoint, P3(location), text ?? string.Empty, null)
                : new DxfPoint3AngularDimension(P3(vertex), P3(points[0]), P3(points[1]), P3(location), text ?? string.Empty, null);
            string words = Words(style, made, sweep, text, string.Empty);
            c.Write("Dimension text = " + words);
            double textHeight = TextHeight(c, style);
            string handle = AddedHandle(Place(c, style, textHeight, made, Angular(style, Dimscale(c), textHeight, vertex, radius, from, sweep, near, far, words)));

            CadDimAssociation.Tie(c, handle, tiedArc != null ? CadDimAssociation.ArcEnds(tiedArc) : first != null ? CadDimAssociation.TwoLines(first, second) : snapped);
        }

        /// <summary>
        /// The angle an angular dimension measures, from where its arc is put: of two lines, the one of their four angles the
        /// point lies in, each ray taking the line that runs along it; of a vertex and two points, the angle between them or the
        /// one outside it. The rays and how far along each its object reaches are written into the arrays given.
        /// </summary>
        private static void AngularSector(DxfLine first, DxfLine second, GeoPoint2 vertex, GeoPoint2[] points, GeoPoint2 location, GeoVector2[] rays, double[] near, double[] far, out double from, out double sweep)
        {
            double towards = Math.Atan2(location.Y - vertex.Y, location.X - vertex.X);

            if (first != null)
            {
                // Of the four angles two lines make, the one the arc is put in.
                GeoVector2 u1 = Along(first);
                GeoVector2 u2 = Along(second);
                from = 0.0;
                sweep = 0.0;

                foreach (double s1 in new[] { 1.0, -1.0 })
                {
                    foreach (double s2 in new[] { 1.0, -1.0 })
                    {
                        Sector(u1 * s1, u2 * s2, out double a, out double b, out double turn);

                        if (CadGrips.Sweep(a, towards) <= turn)
                        {
                            from = a;
                            sweep = turn;
                            rays[0] = new GeoVector2(Math.Cos(a), Math.Sin(a));
                            rays[1] = new GeoVector2(Math.Cos(b), Math.Sin(b));
                        }
                    }
                }

                // Each ray takes the line that runs along it.
                for (int i = 0; i < 2; i++)
                {
                    DxfLine along = Math.Abs(Cross(rays[i], Along(first))) <= Math.Abs(Cross(rays[i], Along(second))) ? first : second;
                    Reach(along, vertex, rays[i], out near[i], out far[i]);
                }
            }
            else
            {
                double a1 = Math.Atan2(points[0].Y - vertex.Y, points[0].X - vertex.X);
                double a2 = Math.Atan2(points[1].Y - vertex.Y, points[1].X - vertex.X);
                double turn = CadGrips.Sweep(a1, a2);
                bool inside = CadGrips.Sweep(a1, towards) <= turn;
                from = inside ? a1 : a2;
                sweep = inside ? turn : 2.0 * Math.PI - turn;
                rays[0] = new GeoVector2(Math.Cos(from), Math.Sin(from));
                rays[1] = new GeoVector2(Math.Cos(from + sweep), Math.Sin(from + sweep));
                GeoPoint2 p0 = inside ? points[0] : points[1];
                GeoPoint2 p1 = inside ? points[1] : points[0];
                near[0] = far[0] = (p0 - vertex).Length;
                near[1] = far[1] = (p1 - vertex).Length;
            }
        }

        private static GeoVector2 Along(DxfLine line)
        {
            var way = new GeoVector2(line.EndPoint.X - line.StartPoint.X, line.EndPoint.Y - line.StartPoint.Y);
            return way / way.Length;
        }

        private static double Cross(GeoVector2 a, GeoVector2 b) => a.X * b.Y - a.Y * b.X;

        /// <summary>Where two lines, drawn on as far as needed, meet; false where they are parallel within the tolerance.</summary>
        internal static bool Meet(DxfLine a, DxfLine b, Tolerance tolerance, out GeoPoint2 point)
        {
            point = default;
            var p = new GeoPoint2(a.StartPoint.X, a.StartPoint.Y);
            var q = new GeoPoint2(b.StartPoint.X, b.StartPoint.Y);
            GeoVector2 r = Along(a);
            GeoVector2 s = Along(b);
            double d = Cross(r, s);

            if (Math.Abs(d) <= tolerance.EqualVector)
            {
                return false;
            }

            double t = Cross(q - p, s) / d;
            point = p + r * t;
            return true;
        }

        /// <summary>The smaller angle between two ways, counter-clockwise from <paramref name="from"/> to <paramref name="to"/> angles.</summary>
        private static void Sector(GeoVector2 u, GeoVector2 v, out double from, out double to, out double sweep)
        {
            double a = Math.Atan2(u.Y, u.X);
            double b = Math.Atan2(v.Y, v.X);
            double turn = CadGrips.Sweep(a, b);

            if (turn <= Math.PI)
            {
                from = a;
                to = b;
                sweep = turn;
            }
            else
            {
                from = b;
                to = a;
                sweep = 2.0 * Math.PI - turn;
            }
        }

        /// <summary>How far along a ray from the vertex a line reaches, nearest and farthest, nought where it reaches back past the vertex.</summary>
        private static void Reach(DxfLine line, GeoPoint2 vertex, GeoVector2 ray, out double near, out double far)
        {
            double s = (line.StartPoint.X - vertex.X) * ray.X + (line.StartPoint.Y - vertex.Y) * ray.Y;
            double e = (line.EndPoint.X - vertex.X) * ray.X + (line.EndPoint.Y - vertex.Y) * ray.Y;
            near = Math.Max(0.0, Math.Min(s, e));
            far = Math.Max(0.0, Math.Max(s, e));
        }

        private static double Turned(double angle)
        {
            angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return angle >= 2.0 * Math.PI ? 0.0 : angle;
        }

        /// <summary>The current dimension style of the drawing, else Standard, else the first; null where it has none.</summary>
        private static DxfDimStyleTableRecord DimStyle(CadCommandContext c)
        {
            if (string.Equals(c.Database.Version, "AC1009", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string name = null;

            try
            {
                name = c.Database.Dimstyle;
            }
            catch (Exception)
            {
                name = null;
            }

            DxfDimStyleTableRecord style = string.IsNullOrEmpty(name) || !c.Database.DimStyleTable.Has(name) ? null : c.Database.DimStyleTable[name];

            if (style == null && c.Database.DimStyleTable.Has("Standard"))
            {
                style = c.Database.DimStyleTable["Standard"];
            }

            if (style == null)
            {
                foreach (DxfDimStyleTableRecord any in c.Database.DimStyleTable)
                {
                    return any;
                }
            }

            return style;
        }

        /// <summary>
        /// The angle of a linear dimension AutoCAD takes from where its line is put: level where the point lies above or below
        /// the two origins, upright where it lies to their left or right, else by which way it lies farther out.
        /// </summary>
        private static double LinearAngle(GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 location)
        {
            double outX = Math.Max(Math.Min(p1.X, p2.X) - location.X, location.X - Math.Max(p1.X, p2.X));
            double outY = Math.Max(Math.Min(p1.Y, p2.Y) - location.Y, location.Y - Math.Max(p1.Y, p2.Y));
            return outX > outY ? Math.PI / 2.0 : 0.0;
        }

        /// <summary>The words a dimension shows: the measurement as the style writes it, put in the place of a &lt;&gt; of text typed.</summary>
        private static string Words(DxfDimStyleTableRecord style, DxfDimension dimension, double measured, string typed, string prefix)
        {
            string number;

            try
            {
                dimension.DimensionStyle = style;

                // A dimension in no drawing yet reads AutoCAD's own variables, not its style's: the measurement is written by a
                // copy given the style's variables as its own, as the dimension reads them once it is in the drawing.
                var probe = (DxfDimension)dimension.Clone();
                probe.SetDimstyleData(style);
                number = probe.FormatMeasurement(measured, string.Empty);
            }
            catch (Exception)
            {
                number = measured.ToString("0.##", CultureInfo.InvariantCulture);
            }

            number = prefix + number;

            if (string.IsNullOrEmpty(typed))
            {
                return number;
            }

            return typed.Replace("<>", number);
        }

        /// <summary>
        /// The picture of a linear dimension: the extension lines (each left out where DIMSE1 or DIMSE2 says), the dimension
        /// line (each half left out where DIMSD1 or DIMSD2 says, run on by DIMDLE past ticks), two arrowheads of the style and
        /// the words, placed as AutoCAD places them from the style's variables (see <see cref="LinearLayout"/>).
        /// </summary>
        /// <remarks>
        /// <paramref name="placed"/> says the words were put somewhere by hand (DIMTEDIT): they are drawn where the caller
        /// puts them, so the line is not broken for them and they are not moved outside. <paramref name="jog"/> is the point of
        /// a jog of DIMJOGLINE on the line, drawn <paramref name="jogHeight"/> high.
        /// </remarks>
        internal static List<DxfEntity> Linear(DxfDimStyleTableRecord style, double dimscale, double textHeight, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 q1, GeoPoint2 q2, string words, bool placed = false, GeoPoint2? jog = null, double jogHeight = 0.0)
        {
            Sizes(style, dimscale, textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond);
            var picture = new List<DxfEntity>();
            GeoVector2 along = (q2 - q1) / (q2 - q1).Length;
            double angle = Math.Atan2(along.Y, along.X);

            // The words read from the left or from below, as AutoCAD turns them.
            if (angle > Math.PI / 2.0 + 1E-9 || angle <= -Math.PI / 2.0 + 1E-9)
            {
                angle += Math.PI;
            }

            DxfEntity text = Words(style, default, angle, height, words, scale);
            double width = WordsWidth(new List<DxfEntity> { text }, words, height);
            LinearLayout layout = LayOut(style, p1, p2, q1, q2, along, angle, width, height, gap, arrow, beyond, placed);
            bool[] suppressed = { style.Dimse1, style.Dimse2 };
            int side = 0;

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> pair in new[] { new KeyValuePair<GeoPoint2, GeoPoint2>(p1, q1), new KeyValuePair<GeoPoint2, GeoPoint2>(p2, q2) })
            {
                GeoVector2 up = pair.Value - pair.Key;
                double length = up.Length;

                if (length > offset && !suppressed[side])
                {
                    GeoVector2 unit = up / length;
                    double longer = layout.OverExtension == side + 1 ? layout.Longer : 0.0;
                    picture.Add(Lined(new DxfLine(P3(pair.Key + unit * offset), P3(pair.Value + unit * (beyond + longer))), style.Dimclre, style.Dimlwe));
                }

                side++;
            }

            // The line between the extension lines, drawn where the arrowheads stand inside or DIMTOFL says, broken about words
            // centred on it and at a jog; and outside, the short lines the arrowheads stand on and the line run on to words there.
            var segments = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

            if (layout.ArrowsInside || style.Dimtofl)
            {
                DimensionLine(segments, style, q1, q2, along, scale, layout.ArrowsInside ? arrow : 0.0);
            }

            if (layout.Break > 0.0)
            {
                segments = Broken(segments, layout.Middle, along, layout.Break);
            }

            if (jog.HasValue && jogHeight > 0.0)
            {
                GeoPoint2 at = Foot(jog.Value, q1, along, 0.0);
                segments = Broken(segments, at, along, jogHeight / 2.0);
                var square = new GeoVector2(-along.Y, along.X);

                // AutoCAD 2021's jog: a jog 3.75 high about x = 25 runs 23.125, 24.375 up 1.875, 25.625 down 1.875, 26.875.
                GeoPoint2[] zigzag = { at - along * (jogHeight / 2.0), at - along * (jogHeight / 6.0) + square * (jogHeight / 2.0), at + along * (jogHeight / 6.0) - square * (jogHeight / 2.0), at + along * (jogHeight / 2.0) };

                for (int i = 0; i + 1 < zigzag.Length; i++)
                {
                    segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(zigzag[i], zigzag[i + 1]));
                }
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in layout.Outside)
            {
                segments.Add(segment);
            }

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in segments)
            {
                picture.Add(Lined(new DxfLine(P3(segment.Key), P3(segment.Value)), style.Dimclrd, style.Dimlwd));
            }

            if (!layout.ArrowsSuppressed)
            {
                AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, q1, layout.ArrowsInside ? -along : along, arrow);
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, q2, layout.ArrowsInside ? along : -along, arrow);
            }

            var mtext = (DxfMText)text;
            mtext.Location = P3(layout.At);

            if (layout.Angle != angle)
            {
                mtext.Rotation = layout.Angle < 0.0 ? layout.Angle + 2.0 * Math.PI : layout.Angle;
            }

            picture.Add(text);
            AddFrame(picture, style, layout.At, mtext.Rotation, width, height, gap);
            picture.Add(Definition(p1));
            picture.Add(Definition(p2));
            picture.Add(Definition(q2));
            return picture;
        }

        /// <summary>Where the parts of a linear dimension stand, as <see cref="LayOut"/> works them out.</summary>
        private sealed class LinearLayout
        {
            /// <summary>The middle of the words.</summary>
            public GeoPoint2 At { get; set; }

            /// <summary>The angle the words are turned to, in radians.</summary>
            public double Angle { get; set; }

            public bool ArrowsInside { get; set; }

            /// <summary>Whether the arrowheads are left out, as DIMSOXD leaves them out where they do not fit.</summary>
            public bool ArrowsSuppressed { get; set; }

            /// <summary>The point of the line the words stand centred on, and half the length taken out of the line for them; nought for none.</summary>
            public GeoPoint2 Middle { get; set; }

            public double Break { get; set; }

            /// <summary>The extension line the words stand over (DIMJUST 3 or 4), 1 or 2; nought for none.</summary>
            public int OverExtension { get; set; }

            /// <summary>How much further that extension line runs, on under the words.</summary>
            public double Longer { get; set; }

            /// <summary>The lines outside the extension lines: those the arrowheads stand on, and the line run on to words there.</summary>
            public List<KeyValuePair<GeoPoint2, GeoPoint2>> Outside { get; } = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();
        }

        /// <summary>
        /// Works out where the words and the arrowheads of a linear dimension stand, from the style's variables, as AutoCAD 2021
        /// draws them. DIMTAD puts the words on the line, breaking it by DIMGAP about them (0), above it (1, and JIS, 3), on the
        /// side away from the points measured (2) or below it (4). Words kept level by DIMTIH on a line that is not level stand
        /// on it for 0, 1 and 4, and off it, clear of it by DIMGAP, for 2 and 3. DIMJUST puts them in the middle (0), next to
        /// the first or the second extension line (1, 2), or over and along one of them (3, 4). Where the words and the
        /// arrowheads do not both fit between the extension lines, DIMTIX keeps the words inside (DIMSOXD then leaves out
        /// arrowheads that do not fit), else DIMATFIT moves both out (0), the arrowheads first (1), the words first (2), or (3)
        /// the words where the arrowheads fit, else the arrowheads; words outside stand past the second extension line (the
        /// first for DIMJUST 1), the line run on to them, and words kept level by DIMTOH stand on a level landing. DIMTVP with
        /// DIMTAD 0 lifts the words by that many times their height, off the line as DIMTAD 1 puts them from 0.7 up; a DIMGAP
        /// below nought, the box round the words, keeps words above the line clear of it by the gap again.
        /// </summary>
        /// <remarks>
        /// With arrowheads of 2.5 and DIMGAP 0.625, a dimension of 100 has its words "100", 5.3 wide, in the middle; of 5, its
        /// words "5" inside and its arrowheads outside on lines 2.5 long; of 7, under DIMATFIT 3, its arrowheads inside and its
        /// words 3.9 past the second extension line, as AutoCAD 2021 draws them. Words need their width between the extension
        /// lines, and DIMGAP each side where the line is broken for them; the arrowheads two lengths and DIMGAP; ticks, and the
        /// other arrowheads that take no room (<see cref="CadArrowheads.TakesNoRoom"/>), never move out.
        /// </remarks>
        private static LinearLayout LayOut(DxfDimStyleTableRecord style, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 q1, GeoPoint2 q2, GeoVector2 along, double angle, double width, double height, double gap, double arrow, double beyond, bool placed)
        {
            var layout = new LinearLayout { Angle = angle };
            double length = (q2 - q1).Length;
            int tad = style.Dimtad;
            int just = style.Dimjust;
            bool box = style.Dimgap < 0.0;

            // DIMTVP with DIMTAD 0 lifts the words by that many times their height: from 0.7 up they stand off the line as
            // DIMTAD 1 puts them, below that on it, the line broken about them.
            double raised = tad == 0 ? style.Dimtvp * height : 0.0;

            if (tad == 0 && Math.Abs(style.Dimtvp) >= 0.7)
            {
                tad = 1;
                raised = 0.0;
            }

            bool ticks = CadArrowheads.TakesNoRoom(ArrowOf(style, DxfDimArrowFlag.FirstArrow)) && CadArrowheads.TakesNoRoom(ArrowOf(style, DxfDimArrowFlag.SecondArrow));
            double inside = style.Dimtih ? 0.0 : angle;
            bool level = Parallel(inside, angle);
            bool centred = tad == 0 || (!level && (tad == 1 || tad == 4));
            double half = Chord(width, height, gap, inside - angle);
            double needText = centred ? 2.0 * half : width;
            double needArrows = ticks ? 0.0 : 2.0 * arrow + gap;
            double needBoth = centred ? needText + (ticks ? 0.0 : 2.0 * arrow) : Math.Max(needText, needArrows);
            bool textInside;
            bool arrowsInside;

            if (placed || just == 3 || just == 4)
            {
                textInside = true;
                arrowsInside = needArrows <= length;
            }
            else if (style.Dimtix || needBoth <= length)
            {
                textInside = true;
                arrowsInside = needBoth <= length;
            }
            else
            {
                switch (style.Dimatfit)
                {
                    case 0:
                        textInside = false;
                        arrowsInside = false;
                        break;
                    case 1:
                        textInside = needText <= length;
                        arrowsInside = false;
                        break;
                    case 2:
                        textInside = false;
                        arrowsInside = needArrows <= length;
                        break;
                    default:
                        arrowsInside = needArrows <= length;
                        textInside = !arrowsInside && needText <= length;
                        break;
                }
            }

            layout.ArrowsInside = arrowsInside;
            layout.ArrowsSuppressed = !arrowsInside && style.Dimtix && style.Dimsoxd;
            bool arrowsOut = !arrowsInside && !layout.ArrowsSuppressed && !ticks;
            var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var above = new GeoVector2(-readable.Y, readable.X);
            var middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0);

            // Words above the line stand clear of it by the gap, and a box round them (DIMGAP below nought) by the gap again.
            double lift = tad != style.Dimtad ? style.Dimtvp * height : gap + height / 2.0 + (box ? gap : 0.0);
            int outerSide = just == 1 ? 1 : 2;

            for (int end = 1; end <= 2 && arrowsOut; end++)
            {
                // The short line an arrowhead outside stands on, an arrowhead long; on the side of words outside the line run on
                // to them is drawn instead.
                if (textInside || end != outerSide)
                {
                    GeoPoint2 tip = end == 1 ? q1 : q2;
                    GeoVector2 outward = end == 1 ? -along : along;
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(tip + outward * arrow, tip + outward * (2.0 * arrow)));
                }
            }

            if (placed)
            {
                layout.At = middle + above * lift;
                return layout;
            }

            if (just == 3 || just == 4)
            {
                // Over an extension line: along it, past its end by DIMEXE, DIMGAP and half the words; beside it on the side
                // above them, the extension line run on under them, or with DIMTAD 0 on it.
                GeoPoint2 foot = just == 3 ? q1 : q2;
                GeoVector2 up = foot - (just == 3 ? p1 : p2);
                GeoVector2 unit = up.Length > 1E-12 ? up / up.Length : new GeoVector2(-along.Y, along.X);
                double turned = Math.Atan2(unit.Y, unit.X);

                if (turned > Math.PI / 2.0 + 1E-9 || turned <= -Math.PI / 2.0 + 1E-9)
                {
                    turned += Math.PI;
                }

                var beside = new GeoVector2(-Math.Sin(turned), Math.Cos(turned));
                layout.OverExtension = just - 2;
                layout.Longer = tad == 0 ? 0.0 : width + 2.0 * gap;
                layout.Angle = turned;
                layout.At = foot + unit * (beyond + gap + width / 2.0) + beside * (tad == 0 ? 0.0 : tad == 4 ? -lift : lift);
                return layout;
            }

            if (textInside)
            {
                // Next to an extension line, an arrowhead and twice DIMGAP in from it, where the words and that much each side
                // fit; else in the middle.
                double from = (centred ? half : width / 2.0 + gap) + gap + (ticks ? 0.0 : arrow);
                GeoPoint2 on = just == 1 && 2.0 * from <= length ? q1 + along * from : just == 2 && 2.0 * from <= length ? q2 - along * from : middle;
                layout.Angle = inside;

                if (centred)
                {
                    layout.At = level ? on + above * raised : on;

                    // Words forced inside that do not fit leave the line whole.
                    if (needText <= length)
                    {
                        layout.Middle = on;
                        layout.Break = half;
                    }
                }
                else
                {
                    // Level words off a slanting line stand clear of it by DIMGAP, their box square to it.
                    double off = level ? lift : (width / 2.0 + gap) * Math.Abs(Math.Sin(inside - angle)) + (height / 2.0 + gap) * Math.Abs(Math.Cos(inside - angle));
                    layout.At = on + above * (Side(tad, above, p1, p2, middle) * off);
                }

                return layout;
            }

            // Outside, past the extension line on the words' side: the line runs on under words above it, or up to words on it;
            // words kept level off a slanting line stand on a level landing from the end of the line run on.
            double outward2 = style.Dimtoh ? 0.0 : angle;
            GeoPoint2 start = outerSide == 1 ? q1 : q2;
            GeoVector2 away = outerSide == 1 ? -along : along;
            double clear = arrowsOut ? 2.0 * arrow : ticks ? 0.0 : arrow;
            layout.Angle = outward2;

            if (!Parallel(outward2, angle))
            {
                GeoPoint2 bend = start + away * clear;
                var flat = new GeoVector2(away.X < -1E-9 ? -1.0 : 1.0, 0.0);
                var up2 = new GeoVector2(0.0, 1.0);
                double landing = tad == 0 ? arrow : width + gap + (box ? gap : 0.0);

                if (clear > (arrowsOut ? arrow : 0.0))
                {
                    layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start + away * (arrowsOut ? arrow : 0.0), bend));
                }

                layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(bend, bend + flat * landing));
                layout.At = tad == 0 ? bend + flat * (landing + gap + width / 2.0) : bend + flat * (gap + width / 2.0) + up2 * (Side(tad, up2, p1, p2, middle) * lift);
                return layout;
            }

            bool onLine = tad == 0;
            double centre = clear + width / 2.0 + gap;
            double reach = onLine ? clear : centre + width / 2.0 + gap;
            layout.At = start + away * centre + (onLine ? above * raised : above * (Side(tad, above, p1, p2, middle) * lift));
            layout.Outside.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(start + away * (arrowsOut ? arrow : 0.0), start + away * reach));
            return layout;
        }

        /// <summary>Whether words turned to one angle run along a line at another, either way.</summary>
        private static bool Parallel(double words, double line) => Math.Abs(Math.Sin(words - line)) <= 1E-9;

        /// <summary>
        /// Half the length of a line through the middle of words, at an angle to them, that lies within their box grown by the
        /// gap: half their width and the gap for a line along them.
        /// </summary>
        private static double Chord(double width, double height, double gap, double turn)
        {
            double along = Math.Abs(Math.Cos(turn));
            double across = Math.Abs(Math.Sin(turn));
            double a = along > 1E-9 ? (width / 2.0 + gap) / along : double.MaxValue;
            double b = across > 1E-9 ? (height / 2.0 + gap) / across : double.MaxValue;
            return Math.Min(a, b);
        }

        /// <summary>
        /// The side of the dimension line words off it stand on, toward <paramref name="above"/> (1) or away from it (-1): above
        /// for DIMTAD 1 and JIS (3), below for 4, and for 2 on the side away from the points measured.
        /// </summary>
        private static double Side(int tad, GeoVector2 above, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 middle)
        {
            switch (tad)
            {
                case 2:
                    var measured = new GeoPoint2((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);
                    return (middle - measured).X * above.X + (middle - measured).Y * above.Y < 0.0 ? -1.0 : 1.0;
                case 4:
                    return -1.0;
                default:
                    return 1.0;
            }
        }

        /// <summary>Takes the length of the line within <paramref name="half"/> of a point on it out of segments along it.</summary>
        private static List<KeyValuePair<GeoPoint2, GeoPoint2>> Broken(List<KeyValuePair<GeoPoint2, GeoPoint2>> segments, GeoPoint2 at, GeoVector2 along, double half)
        {
            var kept = new List<KeyValuePair<GeoPoint2, GeoPoint2>>();

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> segment in segments)
            {
                double s = (segment.Key - at).X * along.X + (segment.Key - at).Y * along.Y;
                double e = (segment.Value - at).X * along.X + (segment.Value - at).Y * along.Y;
                double low = Math.Min(s, e);
                double high = Math.Max(s, e);

                if (high <= -half || low >= half)
                {
                    kept.Add(segment);
                    continue;
                }

                GeoPoint2 lowPoint = s <= e ? segment.Key : segment.Value;
                GeoPoint2 highPoint = s <= e ? segment.Value : segment.Key;

                if (low < -half)
                {
                    kept.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(lowPoint, at - along * half));
                }

                if (high > half)
                {
                    kept.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(at + along * half, highPoint));
                }
            }

            return kept;
        }

        /// <summary>The sizes of a picture: the style's times the scale of <paramref name="dimscale"/>, a DIMSCALE of nought or less drawn as 1.</summary>
        private static void Sizes(DxfDimStyleTableRecord style, double dimscale, double textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond)
        {
            scale = dimscale > 0.0 ? dimscale : 1.0;
            arrow = style.Dimasz * scale;
            height = textHeight * scale;
            gap = Math.Abs(style.Dimgap) * scale;
            offset = style.Dimexo * scale;
            beyond = style.Dimexe * scale;
        }

        /// <summary>A closed filled arrowhead with its tip at a point, pointing a way, a third as wide as long, as AutoCAD's.</summary>
        private static DxfEntity Arrowhead(GeoPoint2 tip, GeoVector2 way, double size)
        {
            GeoPoint2 back = tip - way * size;
            var side = new GeoVector2(-way.Y, way.X) * (size / 6.0);
            return Picture(new DxfSolid(P3(tip), P3(back + side), P3(back - side)));
        }

        /// <summary>
        /// The arrowhead of one end as the style draws it (DIMBLK, or DIMBLK1 and DIMBLK2 where DIMSAH parts them): its shape
        /// drawn into the picture (<see cref="CadArrowheads"/>), as AutoCAD's block of it would draw; closed filled where the
        /// style names none, or a block of its own the presenter does not draw.
        /// </summary>
        private static void AddArrow(List<DxfEntity> picture, DxfDimStyleTableRecord style, DxfDimArrowFlag which, GeoPoint2 tip, GeoVector2 way, double size)
        {
            string name = ArrowOf(style, which);

            if (name.Length == 0)
            {
                picture.Add(Arrowhead(tip, way, size));
                return;
            }

            foreach (DxfEntity part in CadArrowheads.Shape(name, tip, way, size))
            {
                picture.Add(Lined(part, style.Dimclrd, style.Dimlwd));
            }
        }

        /// <summary>The block name of an arrowhead of a style, upper case: empty for closed filled and for one that cannot be read.</summary>
        private static string ArrowOf(DxfDimStyleTableRecord style, DxfDimArrowFlag which)
        {
            try
            {
                return (style.GetArrow(which)?.Name ?? string.Empty).ToUpperInvariant();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The dimension line between its two ends: whole, or the half of each end DIMSD1 or DIMSD2 leaves; run on past an end
        /// by DIMDLE where that end is a tick, as AutoCAD runs it. Where the arrowheads stand inside, the line starts at the back
        /// of each, <paramref name="arrow"/> in from its tip, but for one that takes no room (<see cref="CadArrowheads.TakesNoRoom"/>).
        /// </summary>
        private static void DimensionLine(List<KeyValuePair<GeoPoint2, GeoPoint2>> segments, DxfDimStyleTableRecord style, GeoPoint2 q1, GeoPoint2 q2, GeoVector2 along, double scale, double arrow)
        {
            double dle = Math.Max(0.0, style.Dimdle) * scale;
            string firstName = ArrowOf(style, DxfDimArrowFlag.FirstArrow);
            string secondName = ArrowOf(style, DxfDimArrowFlag.SecondArrow);
            GeoPoint2 a = CadArrowheads.IsTick(firstName) && dle > 0.0 ? q1 - along * dle : CadArrowheads.TakesNoRoom(firstName) ? q1 : q1 + along * arrow;
            GeoPoint2 b = CadArrowheads.IsTick(secondName) && dle > 0.0 ? q2 + along * dle : CadArrowheads.TakesNoRoom(secondName) ? q2 : q2 - along * arrow;
            bool first = !style.Dimsd1;
            bool second = !style.Dimsd2;
            var middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0);

            if (first && second)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(a, b));
            }
            else if (first)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(a, middle));
            }
            else if (second)
            {
                segments.Add(new KeyValuePair<GeoPoint2, GeoPoint2>(middle, b));
            }
        }

        /// <summary>
        /// Gives a line of the picture the colour and the lineweight of the style (DIMCLRD, DIMLWD; DIMCLRE, DIMLWE for the
        /// extension lines) where the style sets one other than ByBlock, the default it is drawn with otherwise.
        /// </summary>
        private static T Lined<T>(T entity, DxfColor color, DxfLineWeight weight)
            where T : DxfEntity
        {
            if (color != null && !color.IsByBlock)
            {
                entity.Color = color;
            }

            if (weight != DxfLineWeight.ByBlock)
            {
                entity.LineWeight = weight;
            }

            return entity;
        }

        /// <summary>The words of the dimension: an MTEXT in the style's text style (DIMTXSTY) and text colour (DIMCLRT), centred on its point.</summary>
        private static DxfEntity Words(DxfDimStyleTableRecord style, GeoPoint2 at, double angle, double height, string words, double scale)
        {
            var text = new DxfMText { Location = P3(at), TextHeight = height > 0.0 ? height : 2.5 * scale, Attachment = DxfAttachmentPoint.MiddleCenter, Contents = words };
            text.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
            string textStyle = null;

            try
            {
                textStyle = style.Dimtxsty?.Name;
            }
            catch (Exception)
            {
                textStyle = null;
            }

            if (!string.IsNullOrEmpty(textStyle) && !string.Equals(textStyle, text.TextStyleName, StringComparison.OrdinalIgnoreCase))
            {
                text.TextStyleName = textStyle;
            }

            Picture(text);
            DxfColor color = style.Dimclrt;

            if (color != null && !color.IsByBlock)
            {
                text.Color = color;
            }

            return text;
        }

        private static DxfEntity Definition(GeoPoint2 at) => new DxfDBPoint(P3(at)) { Layer = "Defpoints" };

        private static T Picture<T>(T entity)
            where T : DxfEntity
        {
            entity.Color = DxfColor.ByBlock;
            return entity;
        }


        /// <summary>
        /// The height of the text of a dimension to be made, before the style's scale: the host's default where it gives one
        /// ("dim.text.height", as Global settings' text height), else the style's DIMTXT.
        /// </summary>
        private static double TextHeight(CadCommandContext c, DxfDimStyleTableRecord style) => c.Remembered("dim.text.height", style.Dimtxt);

        /// <summary>
        /// The DIMSCALE of a dimension to be made: the drawing's current one, its header's <c>$DIMSCALE</c>, as AutoCAD's new
        /// dimension takes the current value and not its style's; <c>Place</c> writes it as an override where they differ.
        /// DIMSCALE 100 typed with the style's 1 draws arrowheads of 2.5 as 250.
        /// </summary>
        private static double Dimscale(CadCommandContext c) => c.Database.Dimscale;

        /// <summary>
        /// Adds a dimension with its picture to the space: the picture appended to an anonymous block *D, which the block
        /// table names as AutoCAD names its own (*D1, *D2), and given to the dimension as its block, its text height and the
        /// drawing's DIMSCALE overrides where they are not the style's.
        /// </summary>
        private static CadEditResult Place(CadCommandContext c, DxfDimStyleTableRecord style, double textHeight, DxfDimension dimension, List<DxfEntity> picture)
        {
            CadSpace space = c.Space;
            string layer = c.Session.DimLayer;
            double dimscale = Dimscale(c);
            return c.Apply(null, s =>
            {
                var block = new DxfBlockTableRecord { Name = "*D" };
                s.Database.BlockTable.Add(block);

                foreach (DxfEntity entity in picture)
                {
                    if (entity.Layer == null || entity.Layer.Length == 0)
                    {
                        entity.Layer = "0";
                    }

                    block.AppendEntity(entity);
                }

                dimension.DimensionStyle = style;
                dimension.DimBlock = block;
                dimension.Layer = s.Database.Clayer;
                space.Block.AppendEntity(dimension);
                dimension.SetDatabaseDefaults(s.Database);

                // DIM's LAyer (AutoCAD's DIMLAYER) puts it on a layer of its own where that layer is still in the drawing.
                if (!string.IsNullOrEmpty(layer) && s.Database.LayerTable.Has(layer))
                {
                    dimension.Layer = layer;
                }

                // A text height other than the style's is an override, as DIMOVERRIDE writes one, so that a CAD drawing the
                // dimension again from its style draws its text as high as its picture has it.
                dimension.Dimtxt = textHeight;

                // The current DIMSCALE other than the style's is an override too, as AutoCAD writes the one typed at the prompt.
                dimension.Dimscale = dimscale;
                s.Added(space, dimension);
            });
        }
    }
}
