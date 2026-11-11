using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What a dimension draws, as the layout and the overlap count read it: its dimension line, its extension lines or
    /// leader, and the box of its text, in view millimetres.
    /// </summary>
    internal sealed class DimensionShape
    {
        private GeoRectangle2[] _arrowheads;

        public DimensionShape(
            GeoLine2? line,
            GeoLine2[] extensions,
            double minX,
            double minY,
            double maxX,
            double maxY,
            double textRotation,
            double textHeight
        )
        {
            Line = line;
            Extensions = extensions;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            TextRotation = textRotation;
            TextHeight = textHeight;
        }

        public GeoLine2? Line { get; }

        public GeoLine2[] Extensions { get; }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

        public double TextRotation { get; }

        /// <summary>The height of the text, which the arrows are as long as; the text keeps a quarter of it clear round it.</summary>
        public double TextHeight { get; }

        /// <summary>The boxes round the arrowheads, as <see cref="DrawingDimension.ArrowheadsOf"/> gives them; none without a line.</summary>
        public GeoRectangle2[] Arrowheads =>
            _arrowheads
            ?? (
                _arrowheads = Line.HasValue
                    ? DrawingDimension.ArrowheadsOf(Line.Value, TextHeight)
                    : new GeoRectangle2[0]
            );

        /// <summary>Whether the text stands past an end of the dimension line, as a text too long for its line does.</summary>
        public bool TextOutside
        {
            get
            {
                if (!Line.HasValue)
                {
                    return false;
                }

                GeoLine2 line = Line.Value;
                double dx = line.EndPoint.X - line.StartPoint.X,
                    dy = line.EndPoint.Y - line.StartPoint.Y;
                double at =
                    (0.5 * (MinX + MaxX) - line.StartPoint.X) * dx + (0.5 * (MinY + MaxY) - line.StartPoint.Y) * dy;
                return at < 0.0 || at > dx * dx + dy * dy;
            }
        }

        public GeoRectangle2 TextBox =>
            new GeoRectangle2(new GeoPoint2(0.5 * (MinX + MaxX), 0.5 * (MinY + MaxY)), MaxX - MinX, MaxY - MinY);

        public static DimensionShape Of(DrawingDimension dimension)
        {
            GeoRectangle2 box = dimension.TextBox;
            var extensions = new GeoLine2[dimension.ExtensionLines.Count];

            for (int i = 0; i < extensions.Length; i++)
            {
                extensions[i] = dimension.ExtensionLines[i];
            }

            return new DimensionShape(
                dimension.DimensionLine,
                extensions,
                box.Center.X - 0.5 * box.Width,
                box.Center.Y - 0.5 * box.Height,
                box.Center.X + 0.5 * box.Width,
                box.Center.Y + 0.5 * box.Height,
                dimension.TextRotation,
                dimension.TextHeight
            );
        }

        /// <summary>
        /// Says how two dimensions overlap, or null where they do not: a dimension line on or across another, an extension
        /// line or leader across a dimension line, a text over a line, over an arrowhead or over another text, or two texts
        /// closer than a quarter of the height of each, so that two texts 50 side by side never read as 5050. Extension lines
        /// meeting extension lines are not counted, nor anything where two lines only meet at their ends.
        /// </summary>
        public static string Conflict(DimensionShape a, DimensionShape b, double tol)
        {
            if (a.Line.HasValue && b.Line.HasValue && LineOverLine(a.Line.Value, b.Line.Value, tol))
            {
                return "dimension line over dimension line";
            }

            if (b.Line.HasValue)
            {
                foreach (GeoLine2 extension in a.Extensions)
                {
                    if (ExtensionOverLine(extension, b.Line.Value, tol))
                    {
                        return "extension line over dimension line";
                    }
                }
            }

            if (a.Line.HasValue)
            {
                foreach (GeoLine2 extension in b.Extensions)
                {
                    if (ExtensionOverLine(extension, a.Line.Value, tol))
                    {
                        return "dimension line under extension line";
                    }
                }
            }

            if (
                Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX) > tol
                && Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY) > tol
            )
            {
                return "text over text";
            }

            // The texts in the middle of two neighbouring dimension lines can be as close as a gap each; none comes closer.
            double clear = 0.25 * (a.TextHeight + b.TextHeight);

            if (
                Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX) + clear > tol
                && Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY) + clear > tol
            )
            {
                return "text against text";
            }

            if (TextOverArrows(a, b, tol))
            {
                return "text over an arrowhead";
            }

            if (TextOverArrows(b, a, tol))
            {
                return "arrowhead over the text";
            }

            // A text put past the end of its own line does not stand by another dimension's line, where it would read as
            // that one's value.
            if (
                a.TextOutside
                && Math.Abs(a.TextRotation - b.TextRotation) <= 1E-9
                && b.Line.HasValue
                && CrossesBox(b.Line.Value, Grown(a, 0.5 * a.TextHeight), tol)
            )
            {
                return "text past its line by another dimension line";
            }

            if (
                b.TextOutside
                && Math.Abs(a.TextRotation - b.TextRotation) <= 1E-9
                && a.Line.HasValue
                && CrossesBox(a.Line.Value, Grown(b, 0.5 * b.TextHeight), tol)
            )
            {
                return "dimension line by a text past its own line";
            }

            if (TextOverLines(a, b, tol))
            {
                return "text over a line";
            }

            if (TextOverLines(b, a, tol))
            {
                return "line over the text";
            }

            return null;
        }

        private static DimensionShape Grown(DimensionShape text, double by) =>
            new DimensionShape(
                null,
                new GeoLine2[0],
                text.MinX - by,
                text.MinY - by,
                text.MaxX + by,
                text.MaxY + by,
                text.TextRotation,
                text.TextHeight
            );

        private static bool TextOverArrows(DimensionShape text, DimensionShape arrows, double tol)
        {
            foreach (GeoRectangle2 box in arrows.Arrowheads)
            {
                double x0 = box.Center.X - 0.5 * box.Width,
                    x1 = box.Center.X + 0.5 * box.Width;
                double y0 = box.Center.Y - 0.5 * box.Height,
                    y1 = box.Center.Y + 0.5 * box.Height;

                if (
                    Math.Min(text.MaxX, x1) - Math.Max(text.MinX, x0) > tol
                    && Math.Min(text.MaxY, y1) - Math.Max(text.MinY, y0) > tol
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TextOverLines(DimensionShape text, DimensionShape lines, double tol)
        {
            if (lines.Line.HasValue && CrossesBox(lines.Line.Value, text, tol))
            {
                return true;
            }

            foreach (GeoLine2 extension in lines.Extensions)
            {
                if (CrossesBox(extension, text, tol))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether two dimension lines lie on one another for more than the tolerance, or cross at a point inside either.
        /// </summary>
        private static bool LineOverLine(GeoLine2 p, GeoLine2 q, double tol)
        {
            int meet = Meet(p, q, tol, out double atP, out double atQ, out double lengthP, out double lengthQ);
            return meet == 2 || (meet == 1 && (Inside(atP, lengthP, tol) || Inside(atQ, lengthQ, tol)));
        }

        /// <summary>Whether an extension line lies on a dimension line, or meets it at a point inside the dimension line.</summary>
        private static bool ExtensionOverLine(GeoLine2 extension, GeoLine2 line, double tol)
        {
            int meet = Meet(line, extension, tol, out double atLine, out _, out double length, out _);
            return meet == 2 || (meet == 1 && Inside(atLine, length, tol));
        }

        private static bool Inside(double at, double length, double tol) => at > tol && at < length - tol;

        /// <summary>
        /// How two segments meet: 0 not at all; 1 at one point, at a distance along each from its start; 2 lying on one
        /// another for more than the tolerance.
        /// </summary>
        private static int Meet(
            GeoLine2 p,
            GeoLine2 q,
            double tol,
            out double atP,
            out double atQ,
            out double lengthP,
            out double lengthQ
        )
        {
            atP = 0.0;
            atQ = 0.0;
            double px = p.EndPoint.X - p.StartPoint.X,
                py = p.EndPoint.Y - p.StartPoint.Y;
            double qx = q.EndPoint.X - q.StartPoint.X,
                qy = q.EndPoint.Y - q.StartPoint.Y;
            lengthP = Math.Sqrt(px * px + py * py);
            lengthQ = Math.Sqrt(qx * qx + qy * qy);

            if (lengthP <= tol || lengthQ <= tol)
            {
                return 0;
            }

            double ux = px / lengthP,
                uy = py / lengthP,
                vx = qx / lengthQ,
                vy = qy / lengthQ;
            double wx = q.StartPoint.X - p.StartPoint.X,
                wy = q.StartPoint.Y - p.StartPoint.Y;
            double cross = ux * vy - uy * vx;

            if (Math.Abs(cross) <= 1E-9)
            {
                // Parallel: on one line where q's start lies on p's line, then lying on one another as far as they overlap.
                if (Math.Abs(ux * wy - uy * wx) > tol)
                {
                    return 0;
                }

                double s0 = ux * wx + uy * wy;
                double s1 = s0 + ux * qx + uy * qy;
                double overlap = Math.Min(lengthP, Math.Max(s0, s1)) - Math.Max(0.0, Math.Min(s0, s1));
                return overlap > tol ? 2 : 0;
            }

            atP = (wx * vy - wy * vx) / cross;
            atQ = (wx * uy - wy * ux) / cross;
            return atP >= -tol && atP <= lengthP + tol && atQ >= -tol && atQ <= lengthQ + tol ? 1 : 0;
        }

        /// <summary>Whether a segment runs through the box of a text, shrunk by the tolerance, for more than the tolerance.</summary>
        private static bool CrossesBox(GeoLine2 line, DimensionShape box, double tol)
        {
            double x0 = box.MinX + tol,
                y0 = box.MinY + tol,
                x1 = box.MaxX - tol,
                y1 = box.MaxY - tol;

            if (x0 >= x1 || y0 >= y1)
            {
                return false;
            }

            double sx = line.StartPoint.X,
                sy = line.StartPoint.Y;
            double dx = line.EndPoint.X - sx,
                dy = line.EndPoint.Y - sy;
            double t0 = 0.0,
                t1 = 1.0;

            if (
                !Clip(-dx, sx - x0, ref t0, ref t1)
                || !Clip(dx, x1 - sx, ref t0, ref t1)
                || !Clip(-dy, sy - y0, ref t0, ref t1)
                || !Clip(dy, y1 - sy, ref t0, ref t1)
            )
            {
                return false;
            }

            return (t1 - t0) * Math.Sqrt(dx * dx + dy * dy) > tol;
        }

        /// <summary>One side of Liang and Barsky's clipping of a segment by a box.</summary>
        private static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0.0)
            {
                return q >= 0.0;
            }

            double r = q / p;

            if (p < 0.0)
            {
                if (r > t1)
                {
                    return false;
                }

                t0 = Math.Max(t0, r);
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }

                t1 = Math.Min(t1, r);
            }

            return true;
        }

        /// <summary>Every pair of dimensions that overlap, said in words, in the order of the pairs.</summary>
        public static List<string> FindAll(IReadOnlyList<DrawingDimension> dimensions, double tol)
        {
            var shapes = new DimensionShape[dimensions.Count];

            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = Of(dimensions[i]);
            }

            var found = new List<string>();

            for (int i = 0; i < shapes.Length; i++)
            {
                for (int j = i + 1; j < shapes.Length; j++)
                {
                    string reason = Conflict(shapes[i], shapes[j], tol);

                    if (reason != null)
                    {
                        found.Add(
                            dimensions[i].Id
                                + " "
                                + reason
                                + " of "
                                + dimensions[j].Id
                                + ": "
                                + dimensions[i]
                                + " / "
                                + dimensions[j]
                        );
                    }
                }
            }

            return found;
        }
    }
}
