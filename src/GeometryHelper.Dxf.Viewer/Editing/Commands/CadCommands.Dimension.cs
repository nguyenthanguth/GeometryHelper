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

        private static IEnumerable<CadPrompt> DimLine(CadCommandContext c, bool aligned)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first extension line origin or", null, null, "select object");
            GeoPoint2 p1;
            GeoPoint2 p2;

            if (c.Input.IsPoint)
            {
                p1 = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second extension line origin", p1);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                p2 = c.Input.Point;
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

            GeoPoint2 location = c.Input.Point;

            // A linear dimension measures along the X or Y axis of the UCS, as AutoCAD's: in a UCS turned 30 degrees it is a
            // rotated dimension of 30 or 120 degrees.
            CadUcs ucs = c.Session.Ucs;
            double angle = aligned ? Math.Atan2(p2.Y - p1.Y, p2.X - p1.X) : ucs.AngleToWorld(fixedAngle ?? LinearAngle(ucs.ToUcs(p1), ucs.ToUcs(p2), ucs.ToUcs(location)));
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            double measured = Math.Abs((p2 - p1).X * along.X + (p2 - p1).Y * along.Y);

            if (measured <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("*Invalid* The dimension measures nothing.");
                yield break;
            }

            GeoPoint2 q1 = location + along * ((p1 - location).X * along.X + (p1 - location).Y * along.Y);
            GeoPoint2 q2 = location + along * ((p2 - location).X * along.X + (p2 - location).Y * along.Y);
            DxfDimension made = aligned
                ? (DxfDimension)new DxfAlignedDimension(P3(p1), P3(p2), P3(q2), text ?? string.Empty, null)
                : new DxfRotatedDimension(angle, P3(p1), P3(p2), P3(q2), text ?? string.Empty, null);
            string words = Words(style, made, measured, text, string.Empty);
            c.Write("Dimension text = " + words);
            double textHeight = TextHeight(c, style);
            Place(c, style, textHeight, made, Linear(style, textHeight, p1, p2, q1, q2, words));
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

            GeoPoint2 location = c.Input.Point;
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
            Place(c, style, textHeight, made, Radial(style, textHeight, center, radius, chord, far, location, diameter, words));
        }

        private static IEnumerable<CadPrompt> DimAngular(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);

            if (style == null)
            {
                c.Write("The drawing has no dimension style to draw in.");
                yield break;
            }

            yield return CadPrompt.ForEntity("Select arc, circle, line, or", null, "specify vertex");
            GeoPoint2 vertex;
            DxfLine first = null;
            DxfLine second = null;

            // The two rays the angle lies between, and the stretch of each its object covers, as distances from the vertex.
            var rays = new GeoVector2[2];
            var near = new double[2];
            var far = new double[2];
            var points = new GeoPoint2[2];

            if (c.Input.Kind == CadInputKind.Selection)
            {
                DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                if (picked is DxfArc arc && arc.Normal.Z > 0.0)
                {
                    vertex = new GeoPoint2(arc.Center.X, arc.Center.Y);
                    points[0] = new GeoPoint2(arc.StartPoint.X, arc.StartPoint.Y);
                    points[1] = new GeoPoint2(arc.EndPoint.X, arc.EndPoint.Y);
                }
                else if (picked is DxfLine line)
                {
                    first = line;
                    yield return CadPrompt.ForEntity("Select second line");

                    if (c.Input.Kind != CadInputKind.Selection || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfLine other) || ReferenceEquals(other, line))
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

                for (int i = 0; i < 2; i++)
                {
                    yield return CadPrompt.ForPoint(i == 0 ? "Specify first angle endpoint" : "Specify second angle endpoint", vertex);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points[i] = c.Input.Point;
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
            Place(c, style, textHeight, made, Angular(style, textHeight, vertex, radius, from, sweep, near, far, words));
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
        private static bool Meet(DxfLine a, DxfLine b, Tolerance tolerance, out GeoPoint2 point)
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

        /// <summary>
        /// The picture of an angular dimension: the arc of the dimension line about the vertex, an arrowhead at each end, the
        /// extension lines from the objects out to the arc (or in to it), and the words outside the arc at its middle.
        /// </summary>
        internal static List<DxfEntity> Angular(DxfDimStyleTableRecord style, double textHeight, GeoPoint2 vertex, double radius, double from, double sweep, double[] near, double[] far, string words)
        {
            Sizes(style, textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond);
            var picture = new List<DxfEntity>();
            double to = from + sweep;
            picture.Add(Lined(new DxfArc(P3(vertex), radius, Turned(from), Turned(to)), style.Dimclrd, style.Dimlwd));
            bool[] suppressed = { style.Dimse1, style.Dimse2 };

            for (int i = 0; i < 2; i++)
            {
                double angle = i == 0 ? from : to;
                var ray = new GeoVector2(Math.Cos(angle), Math.Sin(angle));

                if (suppressed[i])
                {
                    continue;
                }

                if (radius > far[i] + offset)
                {
                    picture.Add(Lined(new DxfLine(P3(vertex + ray * (far[i] + offset)), P3(vertex + ray * (radius + beyond))), style.Dimclre, style.Dimlwe));
                }
                else if (radius < near[i] - offset)
                {
                    picture.Add(Lined(new DxfLine(P3(vertex + ray * (near[i] - offset)), P3(vertex + ray * Math.Max(0.0, radius - beyond))), style.Dimclre, style.Dimlwe));
                }
            }

            GeoPoint2 start = vertex + new GeoVector2(Math.Cos(from), Math.Sin(from)) * radius;
            GeoPoint2 end = vertex + new GeoVector2(Math.Cos(to), Math.Sin(to)) * radius;
            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, start, new GeoVector2(Math.Sin(from), -Math.Cos(from)), arrow);
            AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, end, new GeoVector2(-Math.Sin(to), Math.Cos(to)), arrow);
            double middle = from + sweep / 2.0;
            double angleOfWords = Math.Atan2(Math.Cos(middle), -Math.Sin(middle));

            if (angleOfWords > Math.PI / 2.0 + 1E-9 || angleOfWords <= -Math.PI / 2.0 + 1E-9)
            {
                angleOfWords += Math.PI;
            }

            GeoPoint2 at = vertex + new GeoVector2(Math.Cos(middle), Math.Sin(middle)) * (radius + gap + height / 2.0);
            picture.Add(Words(style, at, angleOfWords, height, words, scale));
            picture.Add(Definition(vertex));
            picture.Add(Definition(start));
            picture.Add(Definition(end));
            return picture;
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
        /// the words above the line.
        /// </summary>
        internal static List<DxfEntity> Linear(DxfDimStyleTableRecord style, double textHeight, GeoPoint2 p1, GeoPoint2 p2, GeoPoint2 q1, GeoPoint2 q2, string words)
        {
            Sizes(style, textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond);
            var picture = new List<DxfEntity>();
            bool[] suppressed = { style.Dimse1, style.Dimse2 };
            int side = 0;

            foreach (KeyValuePair<GeoPoint2, GeoPoint2> pair in new[] { new KeyValuePair<GeoPoint2, GeoPoint2>(p1, q1), new KeyValuePair<GeoPoint2, GeoPoint2>(p2, q2) })
            {
                GeoVector2 up = pair.Value - pair.Key;
                double length = up.Length;

                if (length > offset && !suppressed[side])
                {
                    GeoVector2 unit = up / length;
                    picture.Add(Lined(new DxfLine(P3(pair.Key + unit * offset), P3(pair.Value + unit * beyond)), style.Dimclre, style.Dimlwe));
                }

                side++;
            }

            GeoVector2 along = (q2 - q1) / (q2 - q1).Length;
            DimensionLine(picture, style, q1, q2, along, scale);
            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, q1, -along, arrow);
            AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, q2, along, arrow);
            double angle = Math.Atan2(along.Y, along.X);

            // The words read from the left or from below, as AutoCAD turns them, and stand above the line by the gap.
            if (angle > Math.PI / 2.0 + 1E-9 || angle <= -Math.PI / 2.0 + 1E-9)
            {
                angle += Math.PI;
            }

            var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var above = new GeoVector2(-readable.Y, readable.X);
            GeoPoint2 middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0) + above * (gap + height / 2.0);
            picture.Add(Words(style, middle, angle, height, words, scale));
            picture.Add(Definition(p1));
            picture.Add(Definition(p2));
            picture.Add(Definition(q2));
            return picture;
        }

        /// <summary>The picture of a radius or a diameter: the line to the words, the arrowheads on the rim, the words, a centre mark.</summary>
        internal static List<DxfEntity> Radial(DxfDimStyleTableRecord style, double textHeight, GeoPoint2 center, double radius, GeoPoint2 chord, GeoPoint2 far, GeoPoint2 location, bool diameter, string words)
        {
            Sizes(style, textHeight, out double scale, out double arrow, out double height, out double gap, out _, out _);
            var picture = new List<DxfEntity>();
            GeoVector2 outward = (chord - center) / radius;
            bool outside = (location - center).Length > radius;

            // Outside the rim, the line runs from the rim to the words; inside, from the centre (or the far rim) to the rim.
            GeoPoint2 from = outside ? location : diameter ? far : center;
            picture.Add(Lined(new DxfLine(P3(from), P3(chord)), style.Dimclrd, style.Dimlwd));
            AddArrow(picture, style, DxfDimArrowFlag.FirstArrow, chord, outside ? -outward : outward, arrow);

            if (diameter)
            {
                AddArrow(picture, style, DxfDimArrowFlag.SecondArrow, far, -outward, arrow);
            }

            double angle = Math.Atan2(outward.Y, outward.X);

            if (angle > Math.PI / 2.0 + 1E-9 || angle <= -Math.PI / 2.0 + 1E-9)
            {
                angle += Math.PI;
            }

            var readable = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var above = new GeoVector2(-readable.Y, readable.X);
            picture.Add(Words(style, location + above * (gap + height / 2.0), angle, height, words, scale));
            double mark = Math.Abs(style.Dimcen) * scale;

            if (mark > 0.0 && !diameter)
            {
                picture.Add(Lined(new DxfLine(P3(center + new GeoVector2(-mark, 0.0)), P3(center + new GeoVector2(mark, 0.0))), style.Dimclrd, style.Dimlwd));
                picture.Add(Lined(new DxfLine(P3(center + new GeoVector2(0.0, -mark)), P3(center + new GeoVector2(0.0, mark))), style.Dimclrd, style.Dimlwd));
            }

            picture.Add(Definition(diameter ? far : center));
            picture.Add(Definition(chord));
            return picture;
        }

        private static void Sizes(DxfDimStyleTableRecord style, double textHeight, out double scale, out double arrow, out double height, out double gap, out double offset, out double beyond)
        {
            scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
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
        /// by DIMDLE where that end is a tick, as AutoCAD runs it.
        /// </summary>
        private static void DimensionLine(List<DxfEntity> picture, DxfDimStyleTableRecord style, GeoPoint2 q1, GeoPoint2 q2, GeoVector2 along, double scale)
        {
            double dle = Math.Max(0.0, style.Dimdle) * scale;
            GeoPoint2 a = CadArrowheads.IsTick(ArrowOf(style, DxfDimArrowFlag.FirstArrow)) && dle > 0.0 ? q1 - along * dle : q1;
            GeoPoint2 b = CadArrowheads.IsTick(ArrowOf(style, DxfDimArrowFlag.SecondArrow)) && dle > 0.0 ? q2 + along * dle : q2;
            bool first = !style.Dimsd1;
            bool second = !style.Dimsd2;
            var middle = new GeoPoint2((q1.X + q2.X) / 2.0, (q1.Y + q2.Y) / 2.0);

            if (first && second)
            {
                picture.Add(Lined(new DxfLine(P3(a), P3(b)), style.Dimclrd, style.Dimlwd));
            }
            else if (first)
            {
                picture.Add(Lined(new DxfLine(P3(a), P3(middle)), style.Dimclrd, style.Dimlwd));
            }
            else if (second)
            {
                picture.Add(Lined(new DxfLine(P3(middle), P3(b)), style.Dimclrd, style.Dimlwd));
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
        /// Adds a dimension with its picture to the space: the picture appended to an anonymous block *D, which the block
        /// table names as AutoCAD names its own (*D1, *D2), and given to the dimension as its block, its text height an
        /// override where it is not the style's.
        /// </summary>
        private static void Place(CadCommandContext c, DxfDimStyleTableRecord style, double textHeight, DxfDimension dimension, List<DxfEntity> picture)
        {
            CadSpace space = c.Space;
            c.Apply(null, s =>
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

                // A text height other than the style's is an override, as DIMOVERRIDE writes one, so that a CAD drawing the
                // dimension again from its style draws its text as high as its picture has it.
                dimension.Dimtxt = textHeight;
                s.Added(space, dimension);
            });
        }
    }
}
