using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Draws shapes in the plane as an SVG picture, to look at in a browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The picture is fitted round everything added, with Y pointing up as in the drawing, and every line is
    /// drawn the same width however far the picture is zoomed. Arcs are drawn as arcs, not as chords. Each shape
    /// takes a stroke colour and, where it encloses something, a fill — any colour SVG knows, such as
    /// <c>"red"</c> or <c>"#3070c0"</c>.
    /// </para>
    /// <code>
    /// new SvgWriter().Add(plate, "black", "#eeeeee").Add(cut, "red").Save("cut.svg");
    /// </code>
    /// </remarks>
    public sealed class SvgWriter
    {
        private readonly List<string> _elements = new List<string>();
        private readonly List<(GeoPoint2 Point, string Colour)> _points = new List<(GeoPoint2, string)>();
        private double _minX = double.PositiveInfinity;
        private double _minY = double.PositiveInfinity;
        private double _maxX = double.NegativeInfinity;
        private double _maxY = double.NegativeInfinity;

        /// <summary>Adds a point, drawn as a dot.</summary>
        public SvgWriter Add(GeoPoint2 point, string colour = "black")
        {
            Grow(point.X, point.Y);
            _points.Add((point, colour));
            return this;
        }

        /// <summary>Adds a segment.</summary>
        public SvgWriter Add(GeoLine2 line, string colour = "black")
        {
            Grow(line.StartPoint.X, line.StartPoint.Y);
            Grow(line.EndPoint.X, line.EndPoint.Y);
            return Path("M " + Xy(line.StartPoint) + " L " + Xy(line.EndPoint), colour, "none");
        }

        /// <summary>Adds an arc.</summary>
        public SvgWriter Add(GeoArc2 arc, string colour = "black")
        {
            GrowRound(arc.Center, arc.Radius);
            return Path("M " + Xy(arc.StartPoint) + " " + ArcTo(arc), colour, "none");
        }

        /// <summary>Adds an edge, straight or curved.</summary>
        public SvgWriter Add(GeoEdge2 edge, string colour = "black")
            => edge.IsArc ? Add(edge.ToArc(), colour) : Add(edge.ToLine(), colour);

        /// <summary>Adds a circle.</summary>
        public SvgWriter Add(GeoCircle2 circle, string colour = "black", string fill = "none")
        {
            GrowRound(circle.Center, circle.Radius);
            _elements.Add(string.Format(CultureInfo.InvariantCulture,
                "<circle cx=\"{0}\" cy=\"{1}\" r=\"{2}\" {3}/>", N(circle.Center.X), N(circle.Center.Y), N(circle.Radius), Style(colour, fill)));
            return this;
        }

        /// <summary>Adds a chain.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public SvgWriter Add(GeoPolyline2 polyline, string colour = "black")
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Path(Through(polyline.Vertices, false), colour, "none");
        }

        /// <summary>Adds a chain with arcs.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public SvgWriter Add(GeoPolylineArc2 chain, string colour = "black")
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            return Path(Along(chain.GetEdges(), false), colour, "none");
        }

        /// <summary>Adds a polygon.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public SvgWriter Add(GeoPolygon2 polygon, string colour = "black", string fill = "none")
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Path(Through(polygon.Vertices, true), colour, fill);
        }

        /// <summary>Adds a loop with arcs.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public SvgWriter Add(GeoPolygonArc2 loop, string colour = "black", string fill = "none")
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            return Path(Along(loop.GetEdges(), true), colour, fill);
        }

        /// <summary>Adds a rectangle.</summary>
        public SvgWriter Add(GeoRectangle2 rectangle, string colour = "black", string fill = "none")
            => Path(Through(rectangle.GetVertices(), true), colour, fill);

        /// <summary>Adds a face, its holes left empty.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public SvgWriter Add(GeoFace2 face, string colour = "black", string fill = "none")
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var data = new StringBuilder(Through(face.Boundary.Vertices, true));

            foreach (GeoPolygon2 hole in face.Holes)
            {
                data.Append(' ').Append(Through(hole.Vertices, true));
            }

            return Path(data.ToString(), colour, fill);
        }

        /// <summary>
        /// Gets the picture as SVG text.
        /// </summary>
        public override string ToString()
        {
            double width = double.IsInfinity(_minX) ? 1.0 : _maxX - _minX;
            double height = double.IsInfinity(_minY) ? 1.0 : _maxY - _minY;
            double size = Math.Max(Math.Max(width, height), 1E-9);
            double margin = size * 0.05;
            double left = double.IsInfinity(_minX) ? 0.0 : _minX - margin;
            double top = double.IsInfinity(_maxY) ? 0.0 : _maxY + margin;
            double viewWidth = Math.Max(width, 1E-9) + 2 * margin;
            double viewHeight = Math.Max(height, 1E-9) + 2 * margin;
            int pixelsHigh = Math.Max(1, (int)Math.Round(800.0 * viewHeight / viewWidth));

            var text = new StringBuilder();
            text.Append(string.Format(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"{0}\" viewBox=\"{1} {2} {3} {4}\">\n",
                pixelsHigh, N(left), N(-top), N(viewWidth), N(viewHeight)));
            text.Append("<g transform=\"scale(1,-1)\" stroke-linejoin=\"round\" stroke-linecap=\"round\">\n");

            foreach (string element in _elements)
            {
                text.Append(element).Append('\n');
            }

            double dot = size * 0.006;

            foreach ((GeoPoint2 point, string colour) in _points)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "<circle cx=\"{0}\" cy=\"{1}\" r=\"{2}\" fill=\"{3}\"/>\n", N(point.X), N(point.Y), N(dot), Escape(colour)));
            }

            text.Append("</g>\n</svg>\n");
            return text.ToString();
        }

        /// <summary>
        /// Writes the picture to a writer.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the writer is null.</exception>
        public void WriteTo(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.Write(ToString());
        }

        /// <summary>
        /// Writes the picture to a path, replacing whatever is there.
        /// </summary>
        public void Save(string path) => File.WriteAllText(path, ToString(), new UTF8Encoding(false));

        private SvgWriter Path(string data, string colour, string fill)
        {
            _elements.Add("<path d=\"" + data + "\" " + Style(colour, fill) + (fill != "none" ? " fill-rule=\"evenodd\"" : string.Empty) + "/>");
            return this;
        }

        private string Through(IReadOnlyList<GeoPoint2> points, bool closed)
        {
            var data = new StringBuilder();

            for (int i = 0; i < points.Count; i++)
            {
                Grow(points[i].X, points[i].Y);
                data.Append(i == 0 ? "M " : " L ").Append(Xy(points[i]));
            }

            if (closed)
            {
                data.Append(" Z");
            }

            return data.ToString();
        }

        private string Along(IEnumerable<GeoEdge2> edges, bool closed)
        {
            var data = new StringBuilder();
            bool first = true;

            foreach (GeoEdge2 edge in edges)
            {
                if (first)
                {
                    data.Append("M ").Append(Xy(edge.StartPoint));
                    first = false;
                }

                if (edge.IsArc)
                {
                    GeoArc2 arc = edge.ToArc();
                    GrowRound(arc.Center, arc.Radius);
                    data.Append(' ').Append(ArcTo(arc));
                }
                else
                {
                    Grow(edge.StartPoint.X, edge.StartPoint.Y);
                    Grow(edge.EndPoint.X, edge.EndPoint.Y);
                    data.Append(" L ").Append(Xy(edge.EndPoint));
                }
            }

            if (closed && !first)
            {
                data.Append(" Z");
            }

            return data.ToString();
        }

        /// <summary>
        /// The SVG arc command to an arc's end. Drawn in the drawing's own coordinates, the positive sweep of SVG
        /// is the positive, counter-clockwise, turn of the drawing.
        /// </summary>
        private static string ArcTo(GeoArc2 arc)
        {
            double swept = arc.SweptAngle;

            return string.Format(CultureInfo.InvariantCulture, "A {0} {0} 0 {1} {2} {3}",
                N(arc.Radius), Math.Abs(swept) > Math.PI ? 1 : 0, swept > 0 ? 1 : 0, Xy(arc.EndPoint));
        }

        private void GrowRound(GeoPoint2 centre, double radius)
        {
            Grow(centre.X - radius, centre.Y - radius);
            Grow(centre.X + radius, centre.Y + radius);
        }

        private void Grow(double x, double y)
        {
            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
        }

        private static string Style(string colour, string fill)
            => "stroke=\"" + Escape(colour) + "\" fill=\"" + Escape(fill) + "\" stroke-width=\"1.5\" vector-effect=\"non-scaling-stroke\"";

        private static string Xy(GeoPoint2 p) => N(p.X) + " " + N(p.Y);

        private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string Escape(string value)
            => (value ?? "none").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
