using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A stroke of a snap marker: a chain of points in pixels about the snap point, X to the right and Y down as the screen
    /// has them, drawn open or closed, solid or dotted, or filled.
    /// </summary>
    public sealed class CadMarkerStroke
    {
        internal CadMarkerStroke(IReadOnlyList<GeoPoint2> points, bool closed, bool dotted = false, bool filled = false)
        {
            Points = points;
            IsClosed = closed;
            IsDotted = dotted;
            IsFilled = filled;
        }

        /// <summary>Gets the points, in pixels from the snap point.</summary>
        public IReadOnlyList<GeoPoint2> Points { get; }

        /// <summary>Gets whether the chain closes back to its first point.</summary>
        public bool IsClosed { get; }

        /// <summary>Gets whether the stroke is drawn dotted, as the line of an extension.</summary>
        public bool IsDotted { get; }

        /// <summary>Gets whether the stroke is filled, as the dot of a geometric centre.</summary>
        public bool IsFilled { get; }
    }

    /// <summary>
    /// AutoCAD's AutoSnap markers as data, so that the canvas, Drafting Settings and a test draw and read the same: each snap
    /// kind's shape in pixels about the snap point, <see cref="Size"/> across, drawn <see cref="LineWidth"/> wide in
    /// <see cref="ColorOn"/>. Endpoint is a square, Midpoint a triangle, Center a circle, Geometric Center a circle with a dot,
    /// Node a circle with a cross, Quadrant a diamond, Intersection a cross, Extension a small cross on a dotted line,
    /// Insertion two squares, Perpendicular a right angle, Tangent a circle with a line on top, Nearest an hourglass, Apparent
    /// Intersection a cross in a square, Extended Apparent Intersection the same with three dots below, Parallel two slanted
    /// lines and a point of object snap tracking a small cross, 4.5 across. A circle is a ring of 24 points.
    /// </summary>
    public static class CadSnapMarker
    {
        /// <summary>The size of a marker across, in pixels: 9.</summary>
        public const double Size = 9.0;

        /// <summary>The width of a marker's lines, in pixels: 1.5.</summary>
        public const double LineWidth = 1.5;

        private const int CircleSides = 24;

        /// <summary>Gets the strokes of a snap kind's marker; none for a kind with no marker (none, grid, ortho, polar).</summary>
        /// <param name="kind">The snap kind.</param>
        /// <returns>The strokes, in pixels about the snap point, Y down.</returns>
        public static IReadOnlyList<CadMarkerStroke> Strokes(CadSnapKind kind)
        {
            const double h = Size / 2.0;

            switch (kind)
            {
                case CadSnapKind.Endpoint:
                    return new[] { Square(-h, -h, h, h) };
                case CadSnapKind.Midpoint:
                    return new[] { Chain(true, 0, -h, h, h, -h, h) };
                case CadSnapKind.Center:
                    return new[] { Circle(h) };
                case CadSnapKind.GeometricCenter:
                    return new[] { Circle(h), new CadMarkerStroke(Ring(1.2), true, filled: true) };
                case CadSnapKind.Node:
                    return new[]
                    {
                        Circle(h),
                        Chain(false, -h * 0.7, -h * 0.7, h * 0.7, h * 0.7),
                        Chain(false, -h * 0.7, h * 0.7, h * 0.7, -h * 0.7),
                    };
                case CadSnapKind.Quadrant:
                    return new[] { Chain(true, 0, -h, h, 0, 0, h, -h, 0) };
                case CadSnapKind.Intersection:
                    return new[] { Chain(false, -h, -h, h, h), Chain(false, -h, h, h, -h) };
                case CadSnapKind.Extension:
                    return new[]
                    {
                        Chain(false, -h * 0.6, -h * 0.6, h * 0.6, h * 0.6),
                        Chain(false, -h * 0.6, h * 0.6, h * 0.6, -h * 0.6),
                        new CadMarkerStroke(Points(-2.0 * h, 0, 2.0 * h, 0), false, dotted: true),
                    };
                case CadSnapKind.Insertion:
                    return new[] { Square(-h, -h, h * 0.3, h * 0.3), Square(-h * 0.3, -h * 0.3, h, h) };
                case CadSnapKind.Perpendicular:
                    return new[] { Chain(false, -h, -h, -h, h, h, h), Chain(false, -h, 0, 0, 0, 0, h) };
                case CadSnapKind.Tangent:
                    return new[] { Circle(h), Chain(false, -h, -h, h, -h) };
                case CadSnapKind.Nearest:
                    return new[] { Chain(true, -h, -h, h, -h, -h, h, h, h) };
                case CadSnapKind.ApparentIntersection:
                    return new[] { Square(-h, -h, h, h), Chain(false, -h, -h, h, h), Chain(false, -h, h, h, -h) };
                case CadSnapKind.Parallel:
                    return new[]
                    {
                        Chain(false, -h, h * 0.6, -h * 0.1, -h * 0.6),
                        Chain(false, h * 0.1, h * 0.6, h, -h * 0.6),
                    };
                case CadSnapKind.ExtendedApparentIntersection:
                    return new[]
                    {
                        Square(-h, -h, h, h),
                        Chain(false, -h, -h, h, h),
                        Chain(false, -h, h, h, -h),
                        Dot(-3.0, h + 2.5),
                        Dot(0.0, h + 2.5),
                        Dot(3.0, h + 2.5),
                    };
                case CadSnapKind.Tracking:
                    return new[]
                    {
                        Chain(false, -h * 0.5, -h * 0.5, h * 0.5, h * 0.5),
                        Chain(false, -h * 0.5, h * 0.5, h * 0.5, -h * 0.5),
                    };
                default:
                    return Array.Empty<CadMarkerStroke>();
            }
        }

        /// <summary>Gets the name of a snap kind as AutoCAD's tooltip writes it: "Endpoint", "Geometric Center", "Apparent Intersection".</summary>
        /// <param name="kind">The snap kind.</param>
        /// <returns>The name; empty for none.</returns>
        public static string NameOf(CadSnapKind kind)
        {
            switch (kind)
            {
                case CadSnapKind.None:
                    return string.Empty;
                case CadSnapKind.GeometricCenter:
                    return "Geometric Center";
                case CadSnapKind.ApparentIntersection:
                    return "Apparent Intersection";
                case CadSnapKind.ExtendedApparentIntersection:
                    return "Extended Apparent Intersection";
                case CadSnapKind.Tracking:
                    return "Object Snap Tracking";
                default:
                    return kind.ToString();
            }
        }

        /// <summary>
        /// Gets the colour of the markers on a background, as 0xRRGGBB: AutoCAD's yellow, 0xFFFF00, on a dark one, and a dark
        /// grey, 0x1A1A1A, on a light one, where yellow would not show; a background is light where its luminance passes half.
        /// </summary>
        /// <param name="background">The background, as 0xRRGGBB.</param>
        /// <returns>The colour.</returns>
        public static int ColorOn(int background)
        {
            double luminance =
                (0.299 * ((background >> 16) & 0xFF) + 0.587 * ((background >> 8) & 0xFF) + 0.114 * (background & 0xFF))
                / 255.0;
            return luminance > 0.5 ? 0x1A1A1A : 0xFFFF00;
        }

        private static CadMarkerStroke Square(double x0, double y0, double x1, double y1) =>
            Chain(true, x0, y0, x1, y0, x1, y1, x0, y1);

        private static CadMarkerStroke Circle(double radius) => new CadMarkerStroke(Ring(radius), true);

        private static CadMarkerStroke Dot(double x, double y)
        {
            GeoPoint2[] ring = Ring(0.7);

            for (int i = 0; i < ring.Length; i++)
            {
                ring[i] = new GeoPoint2(ring[i].X + x, ring[i].Y + y);
            }

            return new CadMarkerStroke(ring, true, filled: true);
        }

        private static CadMarkerStroke Chain(bool closed, params double[] xy) =>
            new CadMarkerStroke(Points(xy), closed);

        private static GeoPoint2[] Points(params double[] xy)
        {
            var points = new GeoPoint2[xy.Length / 2];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(xy[2 * i], xy[2 * i + 1]);
            }

            return points;
        }

        private static GeoPoint2[] Ring(double radius)
        {
            var points = new GeoPoint2[CircleSides];

            for (int i = 0; i < CircleSides; i++)
            {
                double angle = 2.0 * Math.PI * i / CircleSides;
                points[i] = new GeoPoint2(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            return points;
        }
    }
}
