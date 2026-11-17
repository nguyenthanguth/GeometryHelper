using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The boundaries of a hatch read back as shapes and made into objects, for HATCHGENERATEBOUNDARY, the Recreate of
    /// -HATCHEDIT and the Hatch Editor tab. A loop of a polyline, or of lines and arcs that run end to end, is a closed chain
    /// of corners and bulges; a loop that is one whole circle is a circle. A loop with an ellipse or a spline edge, or a loop
    /// of a hatch that does not lie flat to the plan, is no shape here and is passed over (HATCH takes such an area, BOUNDARY
    /// refuses it).
    /// </summary>
    internal static class CadHatchBoundaries
    {
        /// <summary>
        /// The loops of a hatch as shapes, one for each loop in the order the hatch has them; null where a loop is no shape
        /// (<see cref="CadGrips.BoundaryOf"/> says which).
        /// </summary>
        /// <param name="hatch">The hatch.</param>
        /// <returns>The shapes, as many as the hatch has loops.</returns>
        internal static CadGrips.HatchBoundary[] Shapes(DxfHatch hatch)
        {
            var shapes = new CadGrips.HatchBoundary[hatch.NumberOfLoops];

            if (hatch.Normal.Z <= 0.0)
            {
                return shapes;
            }

            for (int i = 0; i < shapes.Length; i++)
            {
                try
                {
                    shapes[i] = CadGrips.BoundaryOf(hatch.GetLoopAt(i), i);
                }
                catch (InvalidOperationException)
                {
                    // An edge that is no shape: the loop is passed over.
                }
            }

            return shapes;
        }

        /// <summary>
        /// The closed polyline of a shape: its corners and the bulges of its edges, a circle as two half circles. The first corner
        /// of a polyline is where the loop starts.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <returns>The polyline, in no drawing yet.</returns>
        internal static DxfPolyline PolylineOf(CadGrips.HatchBoundary shape)
        {
            var polyline = new DxfPolyline();

            if (shape.IsCircle)
            {
                polyline.AddVertexAt(0, new GeoPoint2(shape.Center.X + shape.Radius, shape.Center.Y), 1.0, 0.0, 0.0);
                polyline.AddVertexAt(1, new GeoPoint2(shape.Center.X - shape.Radius, shape.Center.Y), 1.0, 0.0, 0.0);
            }
            else
            {
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    polyline.AddVertexAt(i, shape.Points[i], shape.Bulges[i], 0.0, 0.0);
                }
            }

            polyline.Closed = true;
            polyline.Elevation = shape.Elevation;
            return polyline;
        }

        /// <summary>
        /// The circle of a shape that is one whole circle, in no drawing yet: a loop of one circle edge, or a loop of arcs that all
        /// turn the same way about one centre and add up to a whole turn, as the loop HATCH finds round a circle is (a circle
        /// cut at the points where other objects cross it is two or three arcs). Null for any other shape.
        /// </summary>
        /// <param name="shape">The shape.</param>
        /// <param name="tolerance">The tolerance: centres within its point tolerance are one.</param>
        /// <returns>The circle, or null.</returns>
        internal static DxfCircle WholeCircle(CadGrips.HatchBoundary shape, Tolerance tolerance)
        {
            GeoPoint2 center = shape.Center;
            double radius = shape.Radius;

            if (!shape.IsCircle)
            {
                double turn = 0.0;
                int n = shape.Points.Count;
                var centers = new GeoPoint2[n];

                for (int i = 0; i < n; i++)
                {
                    double bulge = shape.Bulges[i];

                    if (Math.Abs(bulge) < 1E-9 || (bulge > 0.0) != (shape.Bulges[0] > 0.0))
                    {
                        return null;
                    }

                    GeoPoint2 from = shape.Points[i];
                    GeoPoint2 to = shape.Points[(i + 1) % n];
                    double dx = to.X - from.X;
                    double dy = to.Y - from.Y;
                    double chord = Math.Sqrt(dx * dx + dy * dy);
                    double half = 2.0 * Math.Atan(bulge);
                    double along = chord / 2.0 / Math.Tan(half);
                    turn += Math.Abs(4.0 * Math.Atan(bulge));
                    centers[i] = new GeoPoint2(
                        (from.X + to.X) / 2.0 - dy / chord * along,
                        (from.Y + to.Y) / 2.0 + dx / chord * along
                    );
                }

                if (Math.Abs(turn - 2.0 * Math.PI) > 1E-6)
                {
                    return null;
                }

                center = centers[0];

                foreach (GeoPoint2 other in centers)
                {
                    if (
                        Math.Abs(other.X - center.X) > tolerance.EqualPoint
                        || Math.Abs(other.Y - center.Y) > tolerance.EqualPoint
                    )
                    {
                        return null;
                    }
                }

                double ex = shape.Points[0].X - center.X;
                double ey = shape.Points[0].Y - center.Y;
                radius = Math.Sqrt(ex * ex + ey * ey);
            }

            return new DxfCircle(
                new GeoPoint3(center.X, center.Y, shape.Elevation),
                new GeoVector3(0.0, 0.0, 1.0),
                radius
            );
        }

        /// <summary>
        /// Whether a loop the finder made is the loop of a shape of a hatch: the same corners within the point tolerance, as many
        /// as each other, which is how a loop of a hatch that follows no object is told from the objects a pick or a selection
        /// found. A shape that is a circle is the same as no loop of corners.
        /// </summary>
        /// <param name="shape">The shape of a hatch's loop.</param>
        /// <param name="loop">The loop a pick or a selection found.</param>
        /// <param name="tolerance">The tolerance: corners within its point tolerance are one.</param>
        internal static bool Same(CadGrips.HatchBoundary shape, CadBoundaryLoop loop, Tolerance tolerance)
        {
            if (shape == null || shape.IsCircle || loop.Curve != null || shape.Points.Count != loop.Vertices.Count)
            {
                return false;
            }

            foreach (GeoPoint2 corner in loop.Vertices)
            {
                bool met = false;

                foreach (GeoPoint2 point in shape.Points)
                {
                    met |=
                        Math.Abs(point.X - corner.X) <= tolerance.EqualPoint
                        && Math.Abs(point.Y - corner.Y) <= tolerance.EqualPoint;
                }

                if (!met)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The corners of the box round the outer loops of a hatch's shapes, for the origin a hatch is laid from: a circle by its
        /// centre and radius, the rest by their corners.
        /// </summary>
        /// <param name="shapes">The shapes.</param>
        /// <param name="minX">The least X.</param>
        /// <param name="minY">The least Y.</param>
        /// <param name="maxX">The greatest X.</param>
        /// <param name="maxY">The greatest Y.</param>
        /// <returns>False where there is no shape.</returns>
        internal static bool Box(
            IEnumerable<CadGrips.HatchBoundary> shapes,
            out double minX,
            out double minY,
            out double maxX,
            out double maxY
        )
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            bool any = false;
            var all = new List<CadGrips.HatchBoundary>(shapes);

            // A hatch whose loops are flagged none of them External (a file of another maker's) is boxed by every loop.
            bool outer = all.Exists(s => s != null && (s.Type & DxfHatchLoopTypes.External) != 0);

            foreach (CadGrips.HatchBoundary shape in all)
            {
                if (shape == null || (outer && (shape.Type & DxfHatchLoopTypes.External) == 0))
                {
                    continue;
                }

                any = true;

                if (shape.IsCircle)
                {
                    minX = Math.Min(minX, shape.Center.X - shape.Radius);
                    maxX = Math.Max(maxX, shape.Center.X + shape.Radius);
                    minY = Math.Min(minY, shape.Center.Y - shape.Radius);
                    maxY = Math.Max(maxY, shape.Center.Y + shape.Radius);
                    continue;
                }

                foreach (GeoPoint2 point in shape.Points)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            return any;
        }
    }
}
