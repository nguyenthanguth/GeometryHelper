using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;
using GeometryHelper.Meshing;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Draws shapes in the plane as an SVG picture, to look at in a browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The picture is fitted round everything added, with Y pointing up as in the drawing, and every line is
    /// drawn the same width however far the picture is zoomed. Arcs and ellipses are drawn as arcs and ellipses,
    /// not as chords, and a spline as the Bézier curves it is made of wherever SVG has them. Each shape takes a stroke
    /// colour and, where it encloses something, a fill — any colour SVG knows, such as <c>"red"</c> or <c>"#3070c0"</c>. A ray is drawn on to the edge of the picture, a line without ends across it both ways, and
    /// <see cref="Text"/> names a shape in it.
    /// </para>
    /// <code>
    /// new SvgWriter().Add(plate, "black", "#eeeeee").Add(cut, "red").Save("cut.svg");
    /// </code>
    /// </remarks>
    public sealed class SvgWriter
    {
        /// <summary>
        /// The least width or height of the view box drawn as it is: a millionth, the last of the six places it is written to.
        /// </summary>
        private const double Smallest = 1E-6;

        private readonly List<string> _elements = new List<string>();
        private readonly List<(GeoPoint2 Point, string Colour)> _points = new List<(GeoPoint2, string)>();
        private readonly List<(GeoRay2 Ray, string Colour)> _rays = new List<(GeoRay2, string)>();
        private readonly List<(GeoXLine2 XLine, string Colour)> _xlines = new List<(GeoXLine2, string)>();
        private readonly List<(GeoPoint2 At, string Text, string Colour)> _labels = new List<(GeoPoint2, string, string)>();
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
        /// <remarks>
        /// An arc of a whole turn is drawn as a circle: an SVG arc command that ends where it starts draws nothing, so a path
        /// from (10, 0) round to (10, 0) would leave the picture empty. So is an arc of more than half a turn whose end prints
        /// as its start, since the picture has six places: a radius 10 arc turning 1E-9 short of a whole turn is a circle.
        /// </remarks>
        public SvgWriter Add(GeoArc2 arc, string colour = "black")
        {
            if (Math.Abs(Math.Abs(arc.SweptAngle) - Math.PI * 2.0) <= 1E-12 || (Math.Abs(arc.SweptAngle) > Math.PI && Xy(arc.StartPoint) == Xy(arc.EndPoint)))
            {
                return Add(new GeoCircle2(arc.Center, arc.Radius), colour);
            }

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

        /// <summary>
        /// Adds an ellipse, drawn as an ellipse, turned by the direction of its major axis.
        /// </summary>
        /// <remarks>
        /// The picture grows by the ellipse's bounding box, not by a square of its major radius: an ellipse of radii 30 and 10
        /// (60 across by 20) turned 30 degrees is 52.915 wide and 34.641 high, and the picture is fitted to that.
        /// </remarks>
        public SvgWriter Add(GeoEllipse2 ellipse, string colour = "black", string fill = "none")
        {
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double halfWidth = Math.Sqrt(a * a * ellipse.MajorAxis.X * ellipse.MajorAxis.X + b * b * ellipse.MajorAxis.Y * ellipse.MajorAxis.Y);
            double halfHeight = Math.Sqrt(a * a * ellipse.MajorAxis.Y * ellipse.MajorAxis.Y + b * b * ellipse.MajorAxis.X * ellipse.MajorAxis.X);

            Grow(ellipse.Center.X - halfWidth, ellipse.Center.Y - halfHeight);
            Grow(ellipse.Center.X + halfWidth, ellipse.Center.Y + halfHeight);

            // Under the group's scale(1,-1) a positive turn is the drawing's, counter-clockwise; adding nought drops the sign of -0.
            double degrees = Math.Atan2(ellipse.MajorAxis.Y, ellipse.MajorAxis.X) * 180.0 / Math.PI + 0.0;

            _elements.Add(string.Format(CultureInfo.InvariantCulture,
                "<ellipse cx=\"{0}\" cy=\"{1}\" rx=\"{2}\" ry=\"{3}\" transform=\"rotate({4} {0} {1})\" {5}/>",
                N(ellipse.Center.X), N(ellipse.Center.Y), N(a), N(b), N(degrees), Style(colour, fill)));
            return this;
        }

        /// <summary>
        /// Adds a spline: as the Bézier curves it is made of where SVG has them, and through chords no further from it than a
        /// tolerance where it has none.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and the spline where it is drawn through chords, in drawing units; above nought.</param>
        /// <param name="colour">The colour of the line.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <remarks>
        /// A spline that is not rational and of degree 3 or less is drawn exactly, to the six places the picture's numbers have, cut
        /// at its knots into the Bézier pieces it is made of: a C command for each piece of a cubic, a Q for each of a quadratic and an L for each of a spline of degree 1,
        /// so that the cubic with the control points (0, 0), (100, 150), (200, 150), (300, 0) is <c>M 0 0 C 100 150 200 150 300 0</c>,
        /// and the tolerance is not used. SVG has no curve for a rational spline, a circle among them, nor for a degree above 3:
        /// such a spline is drawn through the corners <see cref="GeoSpline2.Flatten(double)"/> cuts it at, every one kept, so that
        /// the circle of radius 100 within 0.01 is 256 chords. The picture grows by the box the curve fills, not by its control points,
        /// and not at all where the spline is refused.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the spline is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or a spline drawn through chords would need more than 1 048 576 of them.
        /// </exception>
        public SvgWriter Add(GeoSpline2 spline, double chordTolerance, string colour = "black")
        {
            if (spline == null)
            {
                throw new ArgumentNullException(nameof(spline));
            }

            Chords.Require(chordTolerance);

            // The path is made before the picture grows by the spline, so that a tolerance that needs too many chords leaves the picture as it was.
            string data = spline.IsRational || spline.Degree > 3 ? ThroughChords(spline, chordTolerance) : AlongPieces(spline);

            var min = new double[2];
            var max = new double[2];
            spline.Curve.Bounds(min, max);
            Grow(min[0], min[1]);
            Grow(max[0], max[1]);
            return Path(data, colour, "none");
        }

        private static string ThroughChords(GeoSpline2 spline, double chordTolerance)
        {
            List<GeoPoint2> corners = Chords.Of(spline, chordTolerance);
            var chords = new StringBuilder("M ").Append(Xy(corners[0]));

            for (int i = 1; i < corners.Count; i++)
            {
                chords.Append(" L ").Append(Xy(corners[i]));
            }

            return chords.ToString();
        }

        /// <summary>
        /// The path of a spline that is not rational and of degree 3 or less: a command for each of its Bézier pieces. The pieces run on from
        /// one another, so each command takes the control points of its piece but the first.
        /// </summary>
        private static string AlongPieces(GeoSpline2 spline)
        {
            BezierPieces pieces = spline.Curve.Pieces;
            int degree = pieces.Degree;
            string command = degree == 3 ? " C" : degree == 2 ? " Q" : " L";
            var data = new StringBuilder("M ").Append(Xy(spline.StartPoint));

            for (int piece = 0; piece < pieces.Count; piece++)
            {
                int at = pieces.PointOffset(piece);
                data.Append(command);

                for (int j = 1; j <= degree; j++)
                {
                    data.Append(' ').Append(Xy(new GeoPoint2(pieces.Points[at + 2 * j], pieces.Points[at + 2 * j + 1])));
                }
            }

            return data.ToString();
        }

        /// <summary>Adds a triangle.</summary>
        public SvgWriter Add(GeoTriangle2 triangle, string colour = "black", string fill = "none")
            => Path(Through(new[] { triangle.A, triangle.B, triangle.C }, true), colour, fill);

        /// <summary>Adds triangles, each a closed path of its own, such as what a triangulation gives.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the triangles are null.</exception>
        public SvgWriter Add(IEnumerable<GeoTriangle2> triangles, string colour = "black", string fill = "none")
        {
            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            foreach (GeoTriangle2 triangle in triangles)
            {
                Add(triangle, colour, fill);
            }

            return this;
        }

        /// <summary>Adds a mesh, each face a closed path of its own through the corners it has.</summary>
        /// <remarks>A mesh of a grid of 4 cells is 4 paths, so that the joints between the cells show.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the mesh is null.</exception>
        public SvgWriter Add(GeoMesh2 mesh, string colour = "black", string fill = "none")
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] indexes = mesh.GetFaceIndices(f);
                var corners = new GeoPoint2[indexes.Length];

                for (int i = 0; i < indexes.Length; i++)
                {
                    corners[i] = mesh.Vertices[indexes[i]];
                }

                Path(Through(corners, true), colour, fill);
            }

            return this;
        }

        /// <summary>
        /// Adds a ray, drawn from its origin to where it leaves the picture.
        /// </summary>
        /// <remarks>
        /// A ray has no end, so how far it runs is settled when the picture is read: to the edge of the picture fitted round
        /// everything else. Its origin counts towards the fit and its length does not. A ray along +X from (0, 0) in a picture
        /// of a square 0 to 100 is drawn to x = 105, the edge with its margin; a ray added to nothing else is drawn in the
        /// picture a unit across centred on its origin, so that the ray along +X from (5, 5) runs to x = 5.55.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the ray has no direction, as a <c>default</c> one has.</exception>
        public SvgWriter Add(GeoRay2 ray, string colour = "black")
        {
            Chords.Require(ray);
            Grow(ray.Origin.X, ray.Origin.Y);
            _rays.Add((ray, colour));
            return this;
        }

        /// <summary>
        /// Adds a line without ends, drawn across the picture from edge to edge.
        /// </summary>
        /// <remarks>
        /// A line has no end, so how far it runs is settled when the picture is read: both ways to the edge of the picture fitted
        /// round everything else, as a ray's one way. Its origin counts towards the fit, as a ray's does.
        /// The line along X through (0, 0) in a picture of a square 0 to 100 is drawn from x = -5 to x = 105, the edges with their margin;
        /// a line added to nothing else is drawn in the picture a unit across centred on its origin, from x = -0.55 to x = 0.55.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the line has no direction, as a <c>default</c> one has.</exception>
        public SvgWriter Add(GeoXLine2 xline, string colour = "black")
        {
            Chords.Require(xline);
            Grow(xline.Origin.X, xline.Origin.Y);
            _xlines.Add((xline, colour));
            return this;
        }

        /// <summary>
        /// Adds a label, the text starting at a point and reading upright however the picture is zoomed.
        /// </summary>
        /// <param name="at">Where the text starts, at its baseline.</param>
        /// <param name="text">
        /// The text; it is escaped, so that <c>a &lt; b</c> is shown as it is written, and every character XML 1.0 forbids, such
        /// as a control character or an unpaired surrogate, is shown as U+FFFD, so that the picture still parses.
        /// </param>
        /// <param name="colour">The colour of the letters.</param>
        /// <remarks>
        /// The letters are 3 % of the larger side of what is drawn high, margin not counted, as a dot is 0.6 % of it: a
        /// picture of content 100 wide has letters 3 high. The point grows the picture, and the length of the text does not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public SvgWriter Text(GeoPoint2 at, string text, string colour = "black")
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Grow(at.X, at.Y);
            _labels.Add((at, Printable(text), colour));
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
        /// <remarks>
        /// The view box is fitted round everything added with a margin of 5 % of its larger side all round: a segment 100 long
        /// along X is a view box 110 by 10. An axis the view box would hold less than a millionth of, margin and all, nought at
        /// the six places it is written to, is given a unit centred on what is drawn, since a view box of no width or height
        /// shows nothing: a point, a ray or a line without ends alone is a view box 1.1 by 1.1, and a segment 2E-6 long along X
        /// one 0.100002 by 1.1.
        /// </remarks>
        public override string ToString()
        {
            double minX = _minX;
            double maxY = _maxY;
            double width = double.IsInfinity(_minX) ? 1.0 : _maxX - _minX;
            double height = double.IsInfinity(_minY) ? 1.0 : _maxY - _minY;
            double size = Math.Max(Math.Max(width, height), 1E-9);
            double margin = size * 0.05;

            // An axis of no size to six places, margin and all, would be nought wide or high in the view box, which shows
            // nothing: it is given a unit centred on what is drawn, and the margin is worked out again.
            bool narrow = width + 2 * margin < Smallest;
            bool low = height + 2 * margin < Smallest;

            if (narrow)
            {
                minX = 0.5 * (_minX + _maxX) - 0.5;
                width = 1.0;
            }

            if (low)
            {
                maxY = 0.5 * (_minY + _maxY) + 0.5;
                height = 1.0;
            }

            if (narrow || low)
            {
                size = Math.Max(width, height);
                margin = size * 0.05;
            }
            double left = double.IsInfinity(minX) ? 0.0 : minX - margin;
            double top = double.IsInfinity(maxY) ? 0.0 : maxY + margin;
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

            // A ray runs on to the edge of the picture, margin and all: the first of the four edges it meets.
            double right = left + viewWidth;
            double bottom = top - viewHeight;

            foreach ((GeoRay2 ray, string colour) in _rays)
            {
                double reach = Reach(ray.Origin, ray.Direction, left, right, top, bottom);

                GeoPoint2 end = ray.Origin.Add(new GeoVector2(ray.Direction.X * reach, ray.Direction.Y * reach));
                text.Append("<path d=\"M ").Append(Xy(ray.Origin)).Append(" L ").Append(Xy(end)).Append("\" ").Append(Style(colour, "none")).Append("/>\n");
            }

            // A line without ends runs on both ways, to the edge it meets first each way.
            foreach ((GeoXLine2 xline, string colour) in _xlines)
            {
                double ahead = Reach(xline.Origin, xline.Direction, left, right, top, bottom);
                double behind = Reach(xline.Origin, new GeoVector2(-xline.Direction.X, -xline.Direction.Y), left, right, top, bottom);

                GeoPoint2 from = xline.Origin.Add(new GeoVector2(-xline.Direction.X * behind, -xline.Direction.Y * behind));
                GeoPoint2 to = xline.Origin.Add(new GeoVector2(xline.Direction.X * ahead, xline.Direction.Y * ahead));
                text.Append("<path d=\"M ").Append(Xy(from)).Append(" L ").Append(Xy(to)).Append("\" ").Append(Style(colour, "none")).Append("/>\n");
            }

            double dot = size * 0.006;

            foreach ((GeoPoint2 point, string colour) in _points)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "<circle cx=\"{0}\" cy=\"{1}\" r=\"{2}\" fill=\"{3}\"/>\n", N(point.X), N(point.Y), N(dot), Escape(colour)));
            }

            // The letters are turned back over, the group having flipped Y for the drawing's sake.
            foreach ((GeoPoint2 at, string label, string colour) in _labels)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture,
                    "<text transform=\"translate({0} {1}) scale(1,-1)\" font-size=\"{2}\" font-family=\"sans-serif\" fill=\"{3}\">{4}</text>\n",
                    N(at.X), N(at.Y), N(size * 0.03), Escape(colour), Escape(label)));
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

        /// <summary>
        /// Gets how far along a direction of unit length, from a point inside the picture, the first of its four edges is.
        /// </summary>
        private static double Reach(GeoPoint2 origin, GeoVector2 direction, double left, double right, double top, double bottom)
        {
            double reach = double.PositiveInfinity;

            if (direction.X > 0.0)
            {
                reach = Math.Min(reach, (right - origin.X) / direction.X);
            }
            else if (direction.X < 0.0)
            {
                reach = Math.Min(reach, (left - origin.X) / direction.X);
            }

            if (direction.Y > 0.0)
            {
                reach = Math.Min(reach, (top - origin.Y) / direction.Y);
            }
            else if (direction.Y < 0.0)
            {
                reach = Math.Min(reach, (bottom - origin.Y) / direction.Y);
            }

            return reach;
        }

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

            // An arc of more than half a turn whose end prints as its start would be left out of the picture: two arcs, each a half
            // of it and so under half a turn, through its middle, are not.
            if (Math.Abs(swept) > Math.PI && Xy(arc.StartPoint) == Xy(arc.EndPoint))
            {
                string half = string.Format(CultureInfo.InvariantCulture, "A {0} {0} 0 0 {1} ", N(arc.Radius), swept > 0 ? 1 : 0);

                return half + Xy(arc.MidPoint) + " " + half + Xy(arc.EndPoint);
            }

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

        /// <summary>
        /// A number to six places. A negative one that rounds to nought is printed as 0, not -0, as .NET Framework prints it:
        /// .NET 10 prints -0.
        /// </summary>
        private static string N(double value)
        {
            string text = value.ToString("0.######", CultureInfo.InvariantCulture);
            return text == "-0" ? "0" : text;
        }

        /// <summary>
        /// The text with every character XML 1.0 forbids replaced by U+FFFD: the control characters but tab, line feed and
        /// carriage return, a surrogate that is not half of a pair, and U+FFFE and U+FFFF.
        /// </summary>
        private static string Printable(string text)
        {
            var clean = new StringBuilder(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    clean.Append(c).Append(text[i + 1]);
                    i++;
                }
                else if (c == '\t' || c == '\n' || c == '\r' || (c >= 0x20 && c <= 0xD7FF) || (c >= 0xE000 && c <= 0xFFFD))
                {
                    clean.Append(c);
                }
                else
                {
                    clean.Append('�');
                }
            }

            return clean.ToString();
        }

        private static string Escape(string value)
            => (value ?? "none").Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
