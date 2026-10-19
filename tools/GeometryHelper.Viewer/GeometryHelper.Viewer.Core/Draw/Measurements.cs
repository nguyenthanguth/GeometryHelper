using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What is drawn over the scene for a measurement, in double precision world coordinates, until it is cleared.
    /// </summary>
    public sealed class DimensionDrawing
    {
        /// <summary>
        /// Gets the dimension lines, extension lines and arcs of the measurement itself.
        /// </summary>
        public List<DrawPolyline> Lines { get; } = new List<DrawPolyline>();

        /// <summary>
        /// Gets the segments the library gave as the shortest between two items, drawn apart from the dimension.
        /// </summary>
        public List<DrawPolyline> LibraryLines { get; } = new List<DrawPolyline>();

        /// <summary>
        /// Gets the points that were picked.
        /// </summary>
        public List<Vec3> Points { get; } = new List<Vec3>();

        /// <summary>
        /// Gets the labels; each is drawn at a size that suits the screen at any zoom.
        /// </summary>
        public List<DrawLabel> Labels { get; } = new List<DrawLabel>();
    }

    /// <summary>
    /// The outcome of one measurement.
    /// </summary>
    public sealed class MeasureResult
    {
        private readonly List<string> _lines = new List<string>();

        internal MeasureResult(string kind, double value, double secondValue)
        {
            Kind = kind;
            Value = value;
            SecondValue = secondValue;
        }

        /// <summary>
        /// Gets what was measured: distance, angle or radius.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets the number: a distance or a radius in millimetres, an angle in radians.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets the second number: the length in the plane z = 0 of a distance, the arc length of an arc, the larger radius of an ellipse; otherwise 0.
        /// </summary>
        public double SecondValue { get; }

        /// <summary>
        /// Gets the lines to say in the output pane, every number to ten significant digits.
        /// </summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// Gets what is drawn over the scene.
        /// </summary>
        public DimensionDrawing Drawing { get; } = new DimensionDrawing();

        /// <summary>
        /// Adds a line to those said in the output pane.
        /// </summary>
        /// <param name="line">The line.</param>
        public void AddLine(string line) => _lines.Add(line ?? string.Empty);

        /// <summary>
        /// Adds what the library says about the two items the measurement was taken between, and its shortest segment to the drawing.
        /// </summary>
        /// <param name="library">The library's answer.</param>
        /// <exception cref="ArgumentNullException">Thrown when the answer is null.</exception>
        public void AddLibrary(LibraryDistance library)
        {
            if (library == null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            string pair = "library, " + library.FirstName + " to " + library.SecondName;

            if (library.Distance.HasValue)
            {
                AddLine(pair + ": DistanceTo " + Measurements.N(library.Distance.Value));
            }

            if (library.From.HasValue && library.To.HasValue)
            {
                AddLine(pair + ": GetShortestLineTo " + Measurements.N((library.To.Value - library.From.Value).Length) + " long, from " + library.From.Value + " to " + library.To.Value);
                Drawing.LibraryLines.Add(new DrawPolyline(new[] { library.From.Value, library.To.Value }, false));
                Vec3 middle = (library.From.Value + library.To.Value) * 0.5;
                Drawing.Labels.Add(new DrawLabel(middle, "library " + Measurements.Short(library.Distance ?? (library.To.Value - library.From.Value).Length), 0.0));
            }

            foreach (string note in library.Notes)
            {
                AddLine(pair + ": " + note);
            }
        }
    }

    /// <summary>
    /// Measures between points and on curves in double precision, from the exact data of the scene and not from the floats the graphics
    /// card was given.
    /// </summary>
    public static class Measurements
    {
        private const double ArcStep = 2.0 * Math.PI / 180.0;

        /// <summary>
        /// Makes the result of asking the library alone about two items: the number is what <c>DistanceTo</c> gave, or the length of the shortest
        /// segment where it gave none, and the drawing is the library's segment with its length written beside it. There is no dimension of
        /// our own to draw, since no point was picked.
        /// </summary>
        /// <param name="library">The library's answer.</param>
        /// <returns>The result; its <see cref="MeasureResult.Value"/> is not a number where the library gave neither.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the answer is null.</exception>
        public static MeasureResult Items(LibraryDistance library)
        {
            if (library == null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            double value = library.Distance ?? (library.From.HasValue && library.To.HasValue ? (library.To.Value - library.From.Value).Length : double.NaN);
            var result = new MeasureResult("distance", value, 0.0);

            result.AddLine("Distance between " + library.FirstName + " and " + library.SecondName);
            result.AddLibrary(library);
            return result;
        }

        internal static string N(double value) => value.ToString("G10", CultureInfo.InvariantCulture);

        internal static string Short(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

        /// <summary>
        /// Measures the distance between two points.
        /// </summary>
        /// <param name="first">The first point.</param>
        /// <param name="second">The second point.</param>
        /// <returns>
        /// The result: <see cref="MeasureResult.Value"/> is the straight distance and <see cref="MeasureResult.SecondValue"/> its length in the
        /// plane z = 0; the lines give the offsets along each axis. The drawing is the segment between them with its length written at the middle.
        /// </returns>
        public static MeasureResult Distance(Vec3 first, Vec3 second)
        {
            Vec3 d = second - first;
            double plan = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            double length = d.Length;
            var result = new MeasureResult("distance", length, plan);

            result.AddLine("Distance " + N(length) + "  (dx " + N(d.X) + ", dy " + N(d.Y) + ", dz " + N(d.Z) + "; in plan " + N(plan) + ")");
            result.AddLine("  from " + first + " to " + second);
            result.Drawing.Lines.Add(new DrawPolyline(new[] { first, second }, false));
            result.Drawing.Points.Add(first);
            result.Drawing.Points.Add(second);
            result.Drawing.Labels.Add(new DrawLabel((first + second) * 0.5, Short(length), 0.0));
            return result;
        }

        /// <summary>
        /// Measures the angle at a vertex between two points, from 0 to 180 degrees.
        /// </summary>
        /// <param name="first">A point on the first leg.</param>
        /// <param name="vertex">The vertex.</param>
        /// <param name="second">A point on the second leg.</param>
        /// <returns>
        /// The result: <see cref="MeasureResult.Value"/> is the angle in radians, found from the arc tangent of the cross and dot product of the
        /// legs, so that it is exact for a right angle and for a hair off straight. The drawing is the two legs with an arc between them.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when a leg has no length: a point is on the vertex.</exception>
        public static MeasureResult Angle(Vec3 first, Vec3 vertex, Vec3 second)
        {
            Vec3 a = first - vertex;
            Vec3 b = second - vertex;

            if (!(a.Length > 0.0) || !(b.Length > 0.0))
            {
                throw new ArgumentException("An angle needs both points away from the vertex.");
            }

            Vec3 cross = Vec3.Cross(a, b);
            double angle = Math.Atan2(cross.Length, Vec3.Dot(a, b));
            double degrees = angle * 180.0 / Math.PI;
            var result = new MeasureResult("angle", angle, 0.0);

            result.AddLine("Angle " + N(degrees) + " deg  (" + N(angle) + " rad; the other way round " + N(360.0 - degrees) + " deg)");
            result.AddLine("  at " + vertex + ", legs " + N(a.Length) + " and " + N(b.Length) + " long");
            result.Drawing.Lines.Add(new DrawPolyline(new[] { first, vertex, second }, false));
            result.Drawing.Points.Add(first);
            result.Drawing.Points.Add(vertex);
            result.Drawing.Points.Add(second);

            Vec3 u = a.Normalized();
            Vec3 w = (b - u * Vec3.Dot(b, u));
            double radius = 0.25 * Math.Min(a.Length, b.Length);
            Vec3 labelAt = vertex + (u + b.Normalized()).Normalized() * (radius * 1.3);

            if (w.Length > 1E-12 * b.Length)
            {
                w = w.Normalized();
                int pieces = Math.Max(2, (int)Math.Ceiling(angle / ArcStep));
                var arc = new Vec3[pieces + 1];

                for (int i = 0; i <= pieces; i++)
                {
                    double t = angle * i / pieces;
                    arc[i] = vertex + (u * Math.Cos(t) + w * Math.Sin(t)) * radius;
                }

                result.Drawing.Lines.Add(new DrawPolyline(arc, false));
                labelAt = vertex + (u * Math.Cos(angle * 0.5) + w * Math.Sin(angle * 0.5)) * (radius * 1.3);
            }

            result.Drawing.Labels.Add(new DrawLabel(labelAt, Short(degrees) + " deg", 0.0));
            return result;
        }

        /// <summary>
        /// Measures the radius of a drawn arc or circle, from the numbers of the curve.
        /// </summary>
        /// <param name="curve">An arc, a circle or an ellipse.</param>
        /// <param name="picked">Where the curve was picked; the radius is shown out to the point of the curve nearest it.</param>
        /// <returns>
        /// The result: for a circle or an arc <see cref="MeasureResult.Value"/> is the radius and <see cref="MeasureResult.SecondValue"/> the
        /// length along the arc, or round the circle. For an ellipse, which has no one radius, Value is the shorter semi-axis and SecondValue the longer.
        /// The drawing is the centre, and the radius out to the curve.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the curve is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the curve is a segment, which has no radius.</exception>
        public static MeasureResult Radius(SceneCurve curve, Vec3 picked)
        {
            if (curve == null)
            {
                throw new ArgumentNullException(nameof(curve));
            }

            if (curve.Shape != CurveShape.Conic)
            {
                throw new ArgumentException("A segment has no radius.", nameof(curve));
            }

            MeasureResult result;
            Vec3 onCurve = curve.ClosestPointTo(picked);

            if (curve.IsCircular)
            {
                double radius = curve.RadiusU;
                double length = radius * Math.Abs(curve.Sweep);
                result = new MeasureResult("radius", radius, length);

                result.AddLine("Radius " + N(radius) + "  (diameter " + N(2.0 * radius) + ")");
                result.AddLine("  centre " + curve.Center + ", normal " + curve.Normal);

                if (curve.IsFull)
                {
                    result.AddLine("  circle, length round " + N(2.0 * Math.PI * radius));
                }
                else
                {
                    result.AddLine("  arc, swept " + N(Math.Abs(curve.Sweep) * 180.0 / Math.PI) + " deg, " + N(length) + " long, chord " + N((curve.End - curve.Start).Length));
                }

                result.Drawing.Labels.Add(new DrawLabel((curve.Center + onCurve) * 0.5, "R " + Short(radius), 0.0));
            }
            else
            {
                double major = Math.Max(curve.RadiusU, curve.RadiusV);
                double minor = Math.Min(curve.RadiusU, curve.RadiusV);
                result = new MeasureResult("radius", minor, major);

                result.AddLine("Ellipse, semi-axes " + N(major) + " and " + N(minor) + ": it has no one radius");
                result.AddLine("  centre " + curve.Center + ", normal " + curve.Normal);
                result.Drawing.Labels.Add(new DrawLabel((curve.Center + onCurve) * 0.5, "a " + Short(major) + " b " + Short(minor), 0.0));
            }

            result.Drawing.Lines.Add(new DrawPolyline(new[] { curve.Center, onCurve }, false));
            result.Drawing.Points.Add(curve.Center);
            result.Drawing.Points.Add(onCurve);
            return result;
        }
    }
}
