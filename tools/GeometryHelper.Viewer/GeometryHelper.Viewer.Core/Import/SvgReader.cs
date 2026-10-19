using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads the picture <c>SvgWriter</c> writes, and only that: paths of M, L, A and Z, circles, ellipses with a rotation, dots, and text.
    /// The numbers of an SVG are printed to six places, so a shape comes back to within five ten-millionths of a unit in each coordinate and an
    /// arc to about a millionth over its radius; what is read is the picture, whose drawing coordinates are the file's own (the group's flip
    /// of Y is how the picture is shown, not where the shapes are).
    /// </summary>
    internal static class SvgReader
    {
        private const string Layer = "svg";

        /// <summary>
        /// How far a radius may fall short of, or pass, half the chord of its arc for the arc to be read as a half circle: the numbers are printed to six
        /// places, and the radius and the two ends each carry up to half a millionth of rounding.
        /// </summary>
        private const double HalfCircleSlack = 1.5E-6;

        private static readonly Regex Number = new Regex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "black", "#000000" }, { "white", "#FFFFFF" }, { "red", "#FF0000" }, { "green", "#008000" }, { "blue", "#0000FF" }, { "yellow", "#FFFF00" },
            { "orange", "#FFA500" }, { "purple", "#800080" }, { "gray", "#808080" }, { "grey", "#808080" }, { "silver", "#C0C0C0" }, { "maroon", "#800000" },
            { "olive", "#808000" }, { "lime", "#00FF00" }, { "aqua", "#00FFFF" }, { "cyan", "#00FFFF" }, { "teal", "#008080" }, { "navy", "#000080" },
            { "fuchsia", "#FF00FF" }, { "magenta", "#FF00FF" }, { "brown", "#A52A2A" }, { "pink", "#FFC0CB" },
        };

        /// <summary>
        /// A subpath of a path: its points, how each segment to the next point is made, and whether a Z closed it.
        /// </summary>
        private sealed class SubPath
        {
            internal readonly List<Vec3> Points = new List<Vec3>();

            // The arc of the segment that ends at each point, or null for a straight one; the first point has none.
            internal readonly List<ArcSegment> Arcs = new List<ArcSegment>();

            internal bool Closed;
        }

        private sealed class ArcSegment
        {
            internal double Radius;
            internal bool Large;
            internal bool Positive;
        }

        /// <summary>
        /// The deepest nesting of elements read; a picture nested deeper than this is refused.
        /// </summary>
        internal const int MaxDepth = 256;

        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            var b = new ImportBuilder(title);

            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

                using (var reader = XmlReader.Create(new StringReader(text), settings))
                {
                    ReadElements(reader, b, cancel);
                }
            }
            catch (XmlException e)
            {
                throw new FormatException("The picture is not well-formed XML: " + e.Message, e);
            }

            return b.Finish();
        }

        /// <summary>
        /// Walks the elements as the reader gives them, so that nothing of a picture is held but the element being read: each is given its attributes and,
        /// for a text, its words, and nothing nests deeper than <see cref="MaxDepth"/>.
        /// </summary>
        private static void ReadElements(XmlReader reader, ImportBuilder b, CancellationToken cancel)
        {
            int nodes = 0;
            bool root = true;

            while (reader.Read())
            {
                if ((++nodes & 1023) == 0)
                {
                    cancel.ThrowIfCancellationRequested();
                }

                if (reader.Depth > MaxDepth)
                {
                    throw new FormatException("The picture is nested more than " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " deep.");
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (root)
                {
                    root = false;

                    if (reader.LocalName != "svg")
                    {
                        throw new FormatException("This is not an SVG picture: its root is '" + reader.LocalName + "'.");
                    }

                    continue;
                }

                string name = reader.LocalName;
                XElement element = Snapshot(reader);

                switch (name)
                {
                    case "g":
                        if (element.Attribute("transform") != null && Regex.Replace(element.Attribute("transform").Value, @"\s+", string.Empty) != "scale(1,-1)")
                        {
                            b.Warn("SVG: the transform of a group (" + element.Attribute("transform").Value + ") is not applied");
                        }

                        break;

                    case "path":
                        ReadPath(b, element);
                        break;

                    case "circle":
                        ReadCircle(b, element);
                        break;

                    case "ellipse":
                        ReadEllipse(b, element);
                        break;

                    case "text":
                        element.Value = Words(reader, cancel);
                        ReadText(b, element);
                        break;

                    case "title":
                    case "desc":
                    case "defs":
                    case "style":
                        break;

                    default:
                        b.Warn("SVG: <" + name + "> is not something the library's SvgWriter writes and was left out");
                        break;
                }
            }

            if (root)
            {
                throw new FormatException("This is not an SVG picture: it has no element.");
            }
        }

        /// <summary>
        /// An element with the attributes of the one the reader stands on and nothing inside it.
        /// </summary>
        private static XElement Snapshot(XmlReader reader)
        {
            var element = new XElement(XName.Get(reader.LocalName));

            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    // A namespace declaration is not an attribute of the shape.
                    if (reader.Prefix != "xmlns" && reader.Name != "xmlns")
                    {
                        element.SetAttributeValue(XName.Get(reader.LocalName), reader.Value);
                    }
                }
                while (reader.MoveToNextAttribute());

                reader.MoveToElement();
            }

            return element;
        }

        /// <summary>
        /// The words of the element the reader stands on, all the text inside it however it is nested, read to the end of it.
        /// </summary>
        private static string Words(XmlReader reader, CancellationToken cancel)
        {
            if (reader.IsEmptyElement)
            {
                return string.Empty;
            }

            var words = new System.Text.StringBuilder();
            int depth = reader.Depth;

            while (reader.Read())
            {
                cancel.ThrowIfCancellationRequested();

                if (reader.Depth > MaxDepth)
                {
                    throw new FormatException("The picture is nested more than " + MaxDepth.ToString(CultureInfo.InvariantCulture) + " deep.");
                }

                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                {
                    break;
                }

                if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA || reader.NodeType == XmlNodeType.SignificantWhitespace || reader.NodeType == XmlNodeType.Whitespace)
                {
                    words.Append(reader.Value);
                }
            }

            return words.ToString();
        }

        #region Attributes

        private static bool Read(XElement element, string name, out double value)
        {
            value = 0.0;
            XAttribute attribute = element.Attribute(name);
            return attribute != null && ImportNumber.TryParse(attribute.Value.Trim(), out value);
        }

        private static string Colour(ImportBuilder b, string value)
        {
            if (value == null)
            {
                return null;
            }

            value = value.Trim();

            if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Equals("transparent", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (value[0] == '#' && value.Length == 4 && SceneColor.TryParse("#" + value[1] + value[1] + value[2] + value[2] + value[3] + value[3], out _, out _, out _))
            {
                return SceneColor.Normalize("#" + value[1] + value[1] + value[2] + value[2] + value[3] + value[3]);
            }

            if (SceneColor.TryParse(value, out _, out _, out _))
            {
                return SceneColor.Normalize(value);
            }

            if (Named.TryGetValue(value, out string hex))
            {
                return hex;
            }

            Match rgb = Regex.Match(value, @"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (rgb.Success)
            {
                return "#" + Math.Min(255, int.Parse(rgb.Groups[1].Value, CultureInfo.InvariantCulture)).ToString("X2", CultureInfo.InvariantCulture)
                    + Math.Min(255, int.Parse(rgb.Groups[2].Value, CultureInfo.InvariantCulture)).ToString("X2", CultureInfo.InvariantCulture)
                    + Math.Min(255, int.Parse(rgb.Groups[3].Value, CultureInfo.InvariantCulture)).ToString("X2", CultureInfo.InvariantCulture);
            }

            b.Warn("SVG: the colour '" + value + "' is not one this reader knows; the shapes drawn in it take the colour of the layer");
            return null;
        }

        /// <summary>
        /// The colour of a shape: its stroke, or its fill where it has no stroke.
        /// </summary>
        private static string ShapeColour(ImportBuilder b, XElement element)
            => Colour(b, (string)element.Attribute("stroke")) ?? Colour(b, (string)element.Attribute("fill"));

        private static bool OnlyScale(XElement element, string what, ImportBuilder b)
        {
            XAttribute transform = element.Attribute("transform");

            if (transform != null)
            {
                b.Warn("SVG: the transform of a " + what + " (" + transform.Value + ") is not applied");
                return false;
            }

            return true;
        }

        #endregion

        #region Shapes

        private static void ReadCircle(ImportBuilder b, XElement e)
        {
            OnlyScale(e, "circle", b);

            if (!Read(e, "cx", out double cx) || !Read(e, "cy", out double cy) || !Read(e, "r", out double r))
            {
                b.Warn("SVG: a circle without cx, cy and r was left out");
                return;
            }

            // A dot is a circle with a fill and no stroke: the writer draws a point so.
            if (e.Attribute("stroke") == null)
            {
                b.Add("GeoPoint2", ImportData.Point(new Vec3(cx, cy, 0.0), 2), Layer, null, Colour(b, (string)e.Attribute("fill")), "SVG circle (a dot)");
                return;
            }

            b.Add("GeoCircle2", ImportData.Circle(new Vec3(cx, cy, 0.0), null, r, 2), Layer, null, ShapeColour(b, e), "SVG circle");
        }

        private static void ReadEllipse(ImportBuilder b, XElement e)
        {
            if (!Read(e, "cx", out double cx) || !Read(e, "cy", out double cy) || !Read(e, "rx", out double rx) || !Read(e, "ry", out double ry))
            {
                b.Warn("SVG: an ellipse without cx, cy, rx and ry was left out");
                return;
            }

            double degrees = 0.0;
            XAttribute transform = e.Attribute("transform");

            if (transform != null)
            {
                Match rotate = Regex.Match(transform.Value, @"^\s*rotate\(\s*(" + Number + @")(?:[\s,]+(" + Number + @")[\s,]+(" + Number + @"))?\s*\)\s*$", RegexOptions.CultureInvariant);

                if (!rotate.Success || !ImportNumber.TryParse(rotate.Groups[1].Value, out degrees))
                {
                    b.Warn("SVG: the transform of an ellipse (" + transform.Value + ") is not a rotation and was not applied");
                    degrees = 0.0;
                }
            }

            double angle = degrees * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            // The major axis is the longer of the two radii: a turn of 90 degrees puts it on the y one.
            Vec3 axis = rx >= ry ? new Vec3(cos, sin, 0.0) : new Vec3(-sin, cos, 0.0);
            b.Add("GeoEllipse2", ImportData.Ellipse(new Vec3(cx, cy, 0.0), null, axis, Math.Max(rx, ry), Math.Min(rx, ry), 2), Layer, null, ShapeColour(b, e), "SVG ellipse");
        }

        private static void ReadText(ImportBuilder b, XElement e)
        {
            XAttribute transform = e.Attribute("transform");
            Match place = transform == null ? null : Regex.Match(transform.Value, @"translate\(\s*(" + Number + @")[\s,]+(" + Number + @")\s*\)", RegexOptions.CultureInvariant);

            if (place == null || !place.Success || !ImportNumber.TryParse(place.Groups[1].Value, out double x) || !ImportNumber.TryParse(place.Groups[2].Value, out double y))
            {
                b.Warn("SVG: a text without a translate() placing it was left out");
                return;
            }

            Read(e, "font-size", out double height);
            b.Add(SceneKinds.Text, ImportData.Text(new Vec3(x, y, 0.0), e.Value, height), Layer, null, Colour(b, (string)e.Attribute("fill")), "SVG text");
        }

        private static void ReadPath(ImportBuilder b, XElement e)
        {
            XAttribute d = e.Attribute("d");

            if (d == null)
            {
                return;
            }

            if (!OnlyScale(e, "path", b))
            {
                return;
            }

            List<SubPath> subpaths = ParsePath(b, d.Value, out string problem);

            if (problem != null)
            {
                b.Warn("SVG: a path was left out: " + problem);
                return;
            }

            string color = ShapeColour(b, e);
            subpaths = subpaths.Where(s => s.Points.Count >= 2).ToList();

            if (subpaths.Count == 0)
            {
                return;
            }

            // Several closed subpaths of straight edges are a face and its holes, as the writer draws a face; any other mix is read subpath by subpath.
            if (subpaths.Count > 1 && subpaths.All(s => s.Closed && s.Arcs.All(a => a == null)))
            {
                var rings = subpaths.Select(s => Ring(s)).ToList();

                if (rings.All(r => r.Length >= 3))
                {
                    b.Add("GeoFace2", ImportData.Face(rings[0], rings.Skip(1).ToArray(), 2), Layer, null, color, "SVG path (a face)");
                    return;
                }
            }

            if (subpaths.Count > 1 && subpaths.Any(s => s.Arcs.Any(a => a != null)))
            {
                b.Warn("SVG: a path of several subpaths with arcs was read as one item to each subpath");
            }

            foreach (SubPath s in subpaths)
            {
                Subpath(b, s, color);
            }
        }

        /// <summary>
        /// The corners of a closed subpath: the point a path writes again to come back to where it started is dropped.
        /// </summary>
        private static Vec3[] Ring(SubPath s)
        {
            List<Vec3> points = s.Points;
            int count = points.Count;

            if (s.Closed && count >= 2 && new BitKey(points[0]).Equals(new BitKey(points[count - 1])))
            {
                count--;
            }

            return points.Take(count).ToArray();
        }

        private static void Subpath(ImportBuilder b, SubPath s, string color)
        {
            bool hasArcs = s.Arcs.Any(a => a != null);

            if (!s.Closed)
            {
                if (s.Points.Count == 2 && !hasArcs)
                {
                    b.Add("GeoLine2", ImportData.Line(s.Points[0], s.Points[1], 2), Layer, null, color, "SVG path");
                    return;
                }

                if (s.Points.Count == 2)
                {
                    Arc(b, s, color);
                    return;
                }

                if (!hasArcs)
                {
                    b.Add("GeoPolyline2", ImportData.Chain(s.Points, 2), Layer, null, color, "SVG path");
                    return;
                }

                var open = new double[s.Points.Count];

                for (int k = 1; k < s.Points.Count; k++)
                {
                    open[k - 1] = Bulge(s.Points[k - 1], s.Points[k], s.Arcs[k]);
                }

                b.Add("GeoPolylineArc2", ImportData.Chain(s.Points, 2, open), Layer, null, color, "SVG path");
                return;
            }

            Vec3[] ring = Ring(s);

            if (ring.Length < 3)
            {
                b.Warn("SVG: a closed path of fewer than three corners was left out");
                return;
            }

            if (!hasArcs)
            {
                b.Add("GeoPolygon2", ImportData.Chain(ring, 2), Layer, null, color, "SVG path");
                return;
            }

            // The edge that ends where the path began is the closing edge, a line or an arc; with no such point, Z closes it with a line.
            var bulges = new double[ring.Length];

            for (int k = 1; k < s.Points.Count; k++)
            {
                if (k - 1 < ring.Length)
                {
                    bulges[k - 1] = Bulge(s.Points[k - 1], s.Points[k], s.Arcs[k]);
                }
            }

            b.Add("GeoPolygonArc2", ImportData.Chain(ring, 2, bulges), Layer, null, color, "SVG path");
        }

        private static void Arc(ImportBuilder b, SubPath s, string color)
        {
            Vec3 p0 = s.Points[0];
            Vec3 p1 = s.Points[1];
            double bulge = Bulge(p0, p1, s.Arcs[1]);

            if (p0.Equals(p1))
            {
                // A whole turn has the same point for its two ends, which SVG leaves out of the picture: there is no centre to find.
                b.Warn("SVG: an arc that ends where it starts (a whole turn, which an SVG path cannot draw) was left out");
                return;
            }

            if (bulge == 0.0)
            {
                b.Add("GeoLine2", ImportData.Line(p0, p1, 2), Layer, null, color, "SVG path");
                return;
            }

            Vec3 chord = p1 - p0;
            double length = chord.Length;
            double sweep = 4.0 * Math.Atan(bulge);
            Vec3 left = new Vec3(-chord.Y / length, chord.X / length, 0.0);
            double offset = length * 0.5 * (1.0 - bulge * bulge) / (2.0 * bulge);
            Vec3 centre = (p0 + p1) * 0.5 + left * offset;
            double radius = length * 0.5 / Math.Sin(Math.Abs(sweep) * 0.5);
            double start = Math.Atan2(p0.Y - centre.Y, p0.X - centre.X);

            b.Add("GeoArc2", ImportData.Arc2(centre, radius, start, sweep), Layer, null, color, "SVG path");
        }

        /// <summary>
        /// The bulge of an arc command from the point before it to the point it ends at: the tangent of a quarter of the angle it sweeps,
        /// positive when the sweep flag is set. The numbers are printed to six places, so an arc that is near a half circle to within that
        /// is taken as one, since its angle cannot be told from the radius and the chord at that precision.
        /// </summary>
        private static double Bulge(Vec3 from, Vec3 to, ArcSegment arc)
        {
            if (arc == null)
            {
                return 0.0;
            }

            double half = (to - from).Length * 0.5;
            double r = Math.Abs(arc.Radius);

            if (half == 0.0)
            {
                return 0.0;
            }

            double angle;

            if (r <= half + HalfCircleSlack)
            {
                angle = Math.PI;
            }
            else
            {
                double small = 2.0 * Math.Asin(Math.Min(1.0, half / r));
                angle = arc.Large ? 2.0 * Math.PI - small : small;
            }

            double bulge = Math.Tan(angle / 4.0);
            return arc.Positive ? bulge : -bulge;
        }

        #endregion

        #region Path data

        private static List<SubPath> ParsePath(ImportBuilder b, string d, out string problem)
        {
            problem = null;
            var subpaths = new List<SubPath>();
            SubPath current = null;
            int i = 0;

            while (true)
            {
                SkipSeparators(d, ref i);

                if (i >= d.Length)
                {
                    break;
                }

                char command = d[i];

                if (!char.IsLetter(command))
                {
                    problem = "a number where a command was expected, in '" + Clip(d, i) + "'";
                    return subpaths;
                }

                i++;

                if (command != 'M' && command != 'L' && command != 'A' && command != 'Z')
                {
                    problem = "the path command '" + command + "' is not one this reader knows (M, L, A and Z are what SvgWriter writes)";
                    return subpaths;
                }

                if (command == 'Z')
                {
                    if (current != null)
                    {
                        current.Closed = true;
                    }

                    continue;
                }

                int perPoint = command == 'A' ? 7 : 2;
                int groups = 0;

                // Numbers keep coming for the same command until the next letter: after a move, the pairs are lines.
                while (true)
                {
                    var numbers = new double[perPoint];
                    int got = 0;

                    while (got < perPoint)
                    {
                        SkipSeparators(d, ref i);
                        Match m = i < d.Length ? Number.Match(d, i) : Match.Empty;

                        if (!m.Success || m.Index != i)
                        {
                            break;
                        }

                        if (!ImportNumber.TryParse(m.Value, out numbers[got]))
                        {
                            problem = "the number '" + m.Value + "' cannot be read as a finite number";
                            return subpaths;
                        }

                        got++;
                        i += m.Length;
                    }

                    if (got == 0 && groups > 0)
                    {
                        break;
                    }

                    if (got < perPoint)
                    {
                        problem = "'" + command + "' with " + got.ToString(CultureInfo.InvariantCulture) + " of its " + perPoint.ToString(CultureInfo.InvariantCulture) + " numbers";
                        return subpaths;
                    }

                    if (command == 'M' && groups == 0)
                    {
                        current = new SubPath();
                        subpaths.Add(current);
                        current.Points.Add(new Vec3(numbers[0], numbers[1], 0.0));
                        current.Arcs.Add(null);
                    }
                    else if (current == null)
                    {
                        problem = "a path that does not begin with M";
                        return subpaths;
                    }
                    else if (command == 'A')
                    {
                        current.Points.Add(new Vec3(numbers[5], numbers[6], 0.0));
                        current.Arcs.Add(new ArcSegment { Radius = numbers[0], Large = numbers[3] != 0.0, Positive = numbers[4] != 0.0 });

                        if (numbers[0] != numbers[1] || numbers[2] != 0.0)
                        {
                            b.Warn("SVG: an arc whose two radii differ, or that is rotated, was read as a circular arc of its first radius");
                        }
                    }
                    else
                    {
                        current.Points.Add(new Vec3(numbers[0], numbers[1], 0.0));
                        current.Arcs.Add(null);
                    }

                    groups++;
                }
            }

            return subpaths;
        }

        private static void SkipSeparators(string d, ref int i)
        {
            while (i < d.Length && (char.IsWhiteSpace(d[i]) || d[i] == ','))
            {
                i++;
            }
        }

        private static string Clip(string text, int at) => text.Substring(at, Math.Min(20, text.Length - at));

        #endregion
    }
}
