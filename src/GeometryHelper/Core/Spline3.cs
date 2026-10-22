using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Core
{
    /// <summary>
    /// A shape asked about by a spline in space, taken to pieces the spline's own maths can answer: the segments, the flat pieces and
    /// the curves of its surface or outline, and for a body, the way to tell whether a point lies in its material.
    /// </summary>
    internal sealed class SplineShape3
    {
        /// <summary>The pieces of the shape the distance and the shortest segment are measured to.</summary>
        internal readonly List<Partner> Parts = new List<Partner>();

        /// <summary>The pieces the crossings are those of, where they are not all the pieces: the rim of a disc, the disc being read filled for the distance only; null otherwise.</summary>
        internal List<Partner> Rims;

        /// <summary>Says whether a point lies in the material of a body; null for a shape that has none.</summary>
        internal Func<GeoPoint3, bool> Holds;

        /// <summary>Gets the pieces the crossings of the shape are those of.</summary>
        internal List<Partner> Crossing => Rims ?? Parts;

        /// <summary>
        /// For a shape that is a curve of one plane, an arc, a circle, an ellipse, an edge, a chain or a loop: given that plane, the pieces
        /// of the shape the plane's queries take for its crossings, with the shape's numbers in the plane; null for any other shape.
        /// </summary>
        internal Func<FlatFrame, List<Partner>> InPlane;
    }

    /// <summary>
    /// The queries of a spline in space against every other shape in space: how far apart they are, where they meet, and the shortest
    /// segment between them. Each shape is cut into pieces the spline can work out its meetings with, one maths for all, so that the spline
    /// and the shape agree to the bit asked from either side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat shape, a triangle, a polygon or a face, is filled: a spline that pierces it, or lies in it, is nought from it, and the
    /// crossings are the places it pierces and where it meets the outline. A circle and an ellipse are discs for the distance, as their
    /// own distances to a point read them, and their crossings are those of the rim. A body, a box, an oriented box, a solid or a ball,
    /// is material: a spline in it is nought from it, the crossings and the shortest segment are those of its skin, and the shortest
    /// segment out of a body that holds the spline is the gap out to the skin. A curve, a chain or a loop of arcs and segments, is the
    /// path it is.
    /// </para>
    /// <para>
    /// In space two curves seldom meet, so a place is where the distance of the spline from the shape is least within the point
    /// tolerance: a crossing is such a place, where the distance comes to nought, and a near miss within the tolerance is another; the
    /// distance itself is never read through the tolerance, only the places and the collision are.
    /// </para>
    /// </remarks>
    internal static partial class Spline3
    {
        private static Tolerance Exactly => default(Tolerance);

        #region Building the shape

        /// <summary>
        /// Gets the stretch of a segment as the spline's partner.
        /// </summary>
        private static LinePartner3 Segment(GeoPoint3 start, GeoPoint3 end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double dz = end.Z - start.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // A segment that is one point, to the bit, is that point: it has no direction, and is none along any.
            if (!(length > 0.0))
            {
                return new LinePartner3(start.X, start.Y, start.Z, 1.0, 0.0, 0.0, 0.0, 0.0, true, true);
            }

            return new LinePartner3(start.X, start.Y, start.Z, dx / length, dy / length, dz / length, 0.0, length, true, true);
        }

        /// <summary>
        /// Gets the range along a line, from its origin, that the box of a spline covers.
        /// </summary>
        private static void Cover(GeoSpline3 spline, GeoPoint3 origin, GeoVector3 direction, out double lowest, out double highest)
        {
            var min = new double[3];
            var max = new double[3];
            spline.Curve.Index.Whole(min, max);
            lowest = double.PositiveInfinity;
            highest = double.NegativeInfinity;

            for (int i = 0; i < 8; i++)
            {
                double x = (i & 1) == 0 ? min[0] : max[0];
                double y = (i & 2) == 0 ? min[1] : max[1];
                double z = (i & 4) == 0 ? min[2] : max[2];
                double along = (x - origin.X) * direction.X + (y - origin.Y) * direction.Y + (z - origin.Z) * direction.Z;
                lowest = Math.Min(lowest, along);
                highest = Math.Max(highest, along);
            }
        }

        private static LinePartner3 Ray(GeoSpline3 spline, GeoRay3 ray)
        {
            Cover(spline, ray.Origin, ray.Direction, out _, out double highest);
            double stop = Math.Max(highest, 0.0) + Math.Max(1.0, highest);

            return new LinePartner3(ray.Origin.X, ray.Origin.Y, ray.Origin.Z, ray.Direction.X, ray.Direction.Y, ray.Direction.Z, 0.0, stop, true, false);
        }

        private static LinePartner3 Endless(GeoSpline3 spline, GeoXLine3 xline)
        {
            Cover(spline, xline.Origin, xline.Direction, out double lowest, out double highest);
            double pad = Math.Max(1.0, highest - lowest);

            return new LinePartner3(xline.Origin.X, xline.Origin.Y, xline.Origin.Z, xline.Direction.X, xline.Direction.Y, xline.Direction.Z, lowest - pad, highest + pad, false, false);
        }

        /// <summary>
        /// Gets the partner of the whole of a plane.
        /// </summary>
        private static PlanePartner3 Whole(GeoSpline3 spline, GeoPlane3 plane)
        {
            return new PlanePartner3(plane.Origin.X, plane.Origin.Y, plane.Origin.Z, plane.Normal.X, plane.Normal.Y, plane.Normal.Z, null, null, null, spline.Curve.Scale, spline.Curve.Extent);
        }

        private static double[] Min(GeoAabb3 box) => new[] { box.Min.X, box.Min.Y, box.Min.Z };

        private static double[] Max(GeoAabb3 box) => new[] { box.Max.X, box.Max.Y, box.Max.Z };

        private static double ScaleOf(GeoAabb3 box)
        {
            return Math.Max(Math.Max(Math.Max(Math.Abs(box.Min.X), Math.Abs(box.Min.Y)), Math.Abs(box.Min.Z)), Math.Max(Math.Max(Math.Abs(box.Max.X), Math.Abs(box.Max.Y)), Math.Abs(box.Max.Z)));
        }

        private static double ExtentOf(GeoAabb3 box) => box.Min.DistanceTo(box.Max);

        /// <summary>
        /// Adds a flat region: the plane it is read in with the test that a place stands over it, and each side of its outline.
        /// </summary>
        private static void AddRegion(SplineShape3 shape, Gap3.Region region)
        {
            GeoVector3 normal = region.UnitNormal;
            GeoPoint3 origin = region.PlaneOrigin;
            GeoAabb3 box = region.Box;

            shape.Parts.Add(new PlanePartner3(origin.X, origin.Y, origin.Z, normal.X, normal.Y, normal.Z, region.Holds, Min(box), Max(box), ScaleOf(box), ExtentOf(box)));

            for (int i = 0; i < region.EdgeCount; i++)
            {
                shape.Parts.Add(Segment(region.EdgeFrom(i), region.EdgeTo(i)));
            }
        }

        private static void AddAll(SplineShape3 shape, IEnumerable<GeoLine3> edges)
        {
            foreach (GeoLine3 edge in edges)
            {
                shape.Parts.Add(Segment(edge.StartPoint, edge.EndPoint));
            }
        }

        /// <summary>
        /// Gets the point of a plane at a point of space, by the plane's two numbers.
        /// </summary>
        private static GeoPoint2 Flat(FlatFrame frame, GeoPoint3 point)
        {
            frame.ToPlane(point.X, point.Y, point.Z, out double x, out double y);

            return new GeoPoint2(x, y);
        }

        /// <summary>
        /// Gets an arc of space as the arc of a plane it lies in: the same centre, radius and ends, turning the way it turns seen from
        /// the side of the plane's own normal.
        /// </summary>
        private static GeoArc2 Flat(FlatFrame frame, GeoArc3 arc)
        {
            GeoPoint2 center = Flat(frame, arc.Center);
            GeoPoint2 start = Flat(frame, arc.StartPoint);
            double from = Math.Atan2(start.Y - center.Y, start.X - center.X);
            bool clockwise = (arc.SweptAngle > 0.0) != (frame.Handedness(arc.Normal.X, arc.Normal.Y, arc.Normal.Z) > 0.0);

            if (Math.Abs(arc.SweptAngle) >= 2.0 * Math.PI)
            {
                return new GeoArc2(center, arc.Radius, from, from, clockwise);
            }

            GeoPoint2 end = Flat(frame, arc.EndPoint);

            return new GeoArc2(center, arc.Radius, from, Math.Atan2(end.Y - center.Y, end.X - center.X), clockwise);
        }

        /// <summary>
        /// Gets the pieces the plane takes for an edge of space laid in it: the segment, or the arc.
        /// </summary>
        private static List<Partner> Flat(FlatFrame frame, GeoEdge3 edge)
        {
            return edge.IsArc
                ? Spline2.Of(null, Flat(frame, edge.ToArc())).Parts
                : Spline2.Of(null, new GeoLine2(Flat(frame, edge.StartPoint), Flat(frame, edge.EndPoint))).Parts;
        }

        /// <summary>
        /// Gets the pieces the plane takes for the edges of a chain or a loop of space laid in it.
        /// </summary>
        private static List<Partner> Flat(FlatFrame frame, IEnumerable<GeoEdge3> edges)
        {
            var parts = new List<Partner>();

            foreach (GeoEdge3 edge in edges)
            {
                parts.AddRange(Flat(frame, edge));
            }

            return parts;
        }

        private static void Add(SplineShape3 shape, GeoEdge3 edge)
        {
            if (edge.IsArc)
            {
                shape.Parts.Add(new SplinePartner(GeoSpline3.FromArc(edge.ToArc()).Curve));
            }
            else
            {
                shape.Parts.Add(Segment(edge.StartPoint, edge.EndPoint));
            }
        }

        private static T Required<T>(T shape, string name) where T : class
        {
            if (shape == null) throw new ArgumentNullException(name);

            return shape;
        }

        /// <summary>
        /// Gets a direction of space lying in a plane as the direction of the plane it is.
        /// </summary>
        private static GeoVector2 Flat(FlatFrame frame, GeoVector3 direction)
        {
            frame.ToPlane(direction.X, direction.Y, direction.Z, out double x, out double y);

            return new GeoVector2(x, y);
        }

        /// <summary>
        /// Gets the spline laid in a plane it lies in, as the plane's own spline, which a ray or a line without ends is cut to the reach of.
        /// </summary>
        private static GeoSpline2 Flat(FlatFrame frame, GeoSpline3 spline) => new GeoSpline2(spline.Curve.LaidFlat(frame, spline.Curve.Scale));

        internal static SplineShape3 Of(GeoSpline3 spline, GeoLine3 line)
        {
            var shape = new SplineShape3();
            shape.Parts.Add(Segment(line.StartPoint, line.EndPoint));
            shape.InPlane = frame => Spline2.Of(null, new GeoLine2(Flat(frame, line.StartPoint), Flat(frame, line.EndPoint))).Parts;

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoRay3 ray)
        {
            var shape = new SplineShape3();
            shape.Parts.Add(Ray(spline, ray));
            shape.InPlane = frame => Spline2.Of(Flat(frame, spline), new GeoRay2(Flat(frame, ray.Origin), Flat(frame, ray.Direction))).Parts;

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoXLine3 xline)
        {
            var shape = new SplineShape3();
            shape.Parts.Add(Endless(spline, xline));
            shape.InPlane = frame => Spline2.Of(Flat(frame, spline), new GeoXLine2(Flat(frame, xline.Origin), Flat(frame, xline.Direction))).Parts;

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoPlane3 plane)
        {
            var shape = new SplineShape3();
            shape.Parts.Add(Whole(spline, plane));

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoPolyline3 polyline)
        {
            Required(polyline, nameof(polyline));

            var shape = new SplineShape3();

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);
                shape.Parts.Add(Segment(edge.StartPoint, edge.EndPoint));
            }

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoPolygon3 polygon)
        {
            Required(polygon, nameof(polygon));

            var shape = new SplineShape3();
            AddRegion(shape, Gap3.Region.Of(polygon));

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoTriangle3 triangle)
        {
            var shape = new SplineShape3();

            // A triangle with no area has no plane and no region: the segment that spans it is all there is.
            if (!(triangle.A.GetVectorTo(triangle.B).CrossProduct(triangle.A.GetVectorTo(triangle.C)).Length > 0.0))
            {
                GeoLine3 longest = triangle.GetEdgeAt(0);

                for (int i = 1; i < 3; i++)
                {
                    GeoLine3 edge = triangle.GetEdgeAt(i);

                    if (edge.Length > longest.Length)
                    {
                        longest = edge;
                    }
                }

                shape.Parts.Add(Segment(longest.StartPoint, longest.EndPoint));
                return shape;
            }

            AddRegion(shape, Gap3.Region.Of(triangle));

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoFace3 face)
        {
            Required(face, nameof(face));

            var shape = new SplineShape3();
            AddRegion(shape, Gap3.Region.Of(face));

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoArc3 arc)
        {
            var shape = new SplineShape3();
            shape.Parts.Add(new SplinePartner(GeoSpline3.FromArc(arc).Curve));
            shape.InPlane = frame => Spline2.Of(null, Flat(frame, arc)).Parts;

            return shape;
        }

        /// <summary>
        /// Adds a disc: the plane it lies in with the test that a place stands over it, and the rim, which is a curve of its own.
        /// </summary>
        private static SplineShape3 Disc(GeoPoint3 center, GeoVector3 normal, double reach, Func<GeoPoint3, bool> holds, NurbsCurve rim)
        {
            var shape = new SplineShape3();
            double scale = Math.Max(Math.Max(Math.Abs(center.X), Math.Abs(center.Y)), Math.Abs(center.Z)) + reach;
            double[] min = { center.X - reach, center.Y - reach, center.Z - reach };
            double[] max = { center.X + reach, center.Y + reach, center.Z + reach };
            var edge = new SplinePartner(rim);

            shape.Parts.Add(new PlanePartner3(center.X, center.Y, center.Z, normal.X, normal.Y, normal.Z, holds, min, max, scale, 2.0 * reach));
            shape.Parts.Add(edge);
            shape.Rims = new List<Partner> { edge };

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoCircle3 circle)
        {
            GeoPoint3 center = circle.Center;
            double radius = circle.Radius;

            SplineShape3 shape = Disc(center, circle.Normal, radius, foot =>
            {
                double dx = foot.X - center.X;
                double dy = foot.Y - center.Y;
                double dz = foot.Z - center.Z;

                return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= radius;
            }, GeoSpline3.FromCircle(circle).Curve);
            shape.InPlane = frame => Spline2.Of(null, new GeoCircle2(Flat(frame, center), radius)).Parts;

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoEllipse3 ellipse)
        {
            GeoPoint3 center = ellipse.Center;
            GeoVector3 major = ellipse.MajorAxis;
            GeoVector3 minor = ellipse.MinorAxis;
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            NurbsCurve rim = NurbsCurve.FromEllipse(new[] { center.X, center.Y, center.Z }, new[] { major.X, major.Y, major.Z }, new[] { minor.X, minor.Y, minor.Z }, a, b);

            // The same test the ellipse's own distances make: inside or on the rim, in its own frame.
            SplineShape3 shape = Disc(center, ellipse.Normal, Math.Max(a, b), foot =>
            {
                double dx = foot.X - center.X;
                double dy = foot.Y - center.Y;
                double dz = foot.Z - center.Z;
                double u = (dx * major.X + dy * major.Y + dz * major.Z) / a;
                double v = (dx * minor.X + dy * minor.Y + dz * minor.Z) / b;

                return u * u + v * v <= 1.0;
            }, rim);
            shape.InPlane = frame =>
            {
                frame.ToPlane(major.X, major.Y, major.Z, out double mx, out double my);
                double length = Math.Sqrt(mx * mx + my * my);

                return Spline2.Of(null, new GeoEllipse2(Flat(frame, center), new GeoVector2(mx / length, my / length), a, b)).Parts;
            };

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoEdge3 edge)
        {
            var shape = new SplineShape3();
            Add(shape, edge);
            shape.InPlane = frame => Flat(frame, edge);

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoPolylineArc3 chain)
        {
            Required(chain, nameof(chain));

            var shape = new SplineShape3();

            foreach (GeoEdge3 edge in chain.GetEdges())
            {
                Add(shape, edge);
            }

            shape.InPlane = frame => Flat(frame, chain.GetEdges());

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoPolygonArc3 loop)
        {
            Required(loop, nameof(loop));

            var shape = new SplineShape3();

            foreach (GeoEdge3 edge in loop.GetEdges())
            {
                Add(shape, edge);
            }

            shape.InPlane = frame => Flat(frame, loop.GetEdges());

            return shape;
        }

        #endregion

        #region Bodies

        /// <summary>
        /// Gets a box, given by its centre, its three unit directions and its three half sizes: its six faces, each a rectangle that a
        /// place stands over, and its twelve edges.
        /// </summary>
        private static SplineShape3 Block(GeoPoint3 center, GeoVector3[] axes, double[] half)
        {
            var shape = new SplineShape3();
            double[] c = { center.X, center.Y, center.Z };
            double[][] a = new double[3][];

            for (int k = 0; k < 3; k++)
            {
                a[k] = new[] { axes[k].X, axes[k].Y, axes[k].Z };
            }

            Func<int, int, int, GeoPoint3> corner = (sx, sy, sz) =>
            {
                double[] s = { sx * half[0], sy * half[1], sz * half[2] };
                double x = c[0], y = c[1], z = c[2];

                for (int k = 0; k < 3; k++)
                {
                    x += s[k] * a[k][0];
                    y += s[k] * a[k][1];
                    z += s[k] * a[k][2];
                }

                return new GeoPoint3(x, y, z);
            };

            for (int axis = 0; axis < 3; axis++)
            {
                int first = (axis + 1) % 3;
                int second = (axis + 2) % 3;

                for (int side = -1; side <= 1; side += 2)
                {
                    var origin = new GeoPoint3(c[0] + side * half[axis] * a[axis][0], c[1] + side * half[axis] * a[axis][1], c[2] + side * half[axis] * a[axis][2]);
                    var min = new[] { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
                    var max = new[] { double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity };

                    for (int i = 0; i < 4; i++)
                    {
                        var s = new int[3];
                        s[axis] = side;
                        s[first] = (i & 1) == 0 ? -1 : 1;
                        s[second] = (i & 2) == 0 ? -1 : 1;
                        GeoPoint3 p = corner(s[0], s[1], s[2]);
                        min[0] = Math.Min(min[0], p.X);
                        min[1] = Math.Min(min[1], p.Y);
                        min[2] = Math.Min(min[2], p.Z);
                        max[0] = Math.Max(max[0], p.X);
                        max[1] = Math.Max(max[1], p.Y);
                        max[2] = Math.Max(max[2], p.Z);
                    }

                    double scale = Math.Max(Math.Max(Math.Max(Math.Abs(min[0]), Math.Abs(min[1])), Math.Abs(min[2])), Math.Max(Math.Max(Math.Abs(max[0]), Math.Abs(max[1])), Math.Abs(max[2])));
                    double extent = Math.Sqrt((max[0] - min[0]) * (max[0] - min[0]) + (max[1] - min[1]) * (max[1] - min[1]) + (max[2] - min[2]) * (max[2] - min[2]));
                    double h1 = half[first];
                    double h2 = half[second];
                    double[] f = a[first];
                    double[] g = a[second];

                    shape.Parts.Add(new PlanePartner3(
                        origin.X, origin.Y, origin.Z, side * a[axis][0], side * a[axis][1], side * a[axis][2],
                        foot =>
                        {
                            double rx = foot.X - c[0];
                            double ry = foot.Y - c[1];
                            double rz = foot.Z - c[2];

                            return Math.Abs(rx * f[0] + ry * f[1] + rz * f[2]) <= h1 && Math.Abs(rx * g[0] + ry * g[1] + rz * g[2]) <= h2;
                        },
                        min, max, scale, extent));
                }

                // The four edges along the axis.
                for (int i = 0; i < 4; i++)
                {
                    var from = new int[3];
                    var to = new int[3];
                    from[axis] = -1;
                    to[axis] = 1;
                    from[first] = to[first] = (i & 1) == 0 ? -1 : 1;
                    from[second] = to[second] = (i & 2) == 0 ? -1 : 1;
                    shape.Parts.Add(Segment(corner(from[0], from[1], from[2]), corner(to[0], to[1], to[2])));
                }
            }

            shape.Holds = point =>
            {
                double rx = point.X - c[0];
                double ry = point.Y - c[1];
                double rz = point.Z - c[2];

                return Math.Abs(rx * a[0][0] + ry * a[0][1] + rz * a[0][2]) <= half[0]
                    && Math.Abs(rx * a[1][0] + ry * a[1][1] + rz * a[1][2]) <= half[1]
                    && Math.Abs(rx * a[2][0] + ry * a[2][1] + rz * a[2][2]) <= half[2];
            };

            return shape;
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoAabb3 box)
        {
            return Block(box.Center, new[] { GeoVector3.XAxis, GeoVector3.YAxis, GeoVector3.ZAxis }, new[] { 0.5 * (box.Max.X - box.Min.X), 0.5 * (box.Max.Y - box.Min.Y), 0.5 * (box.Max.Z - box.Min.Z) });
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoObb3 box)
        {
            Required(box, nameof(box));

            return Block(box.Center, new[] { box.AxisX, box.AxisY, box.AxisZ }, new[] { box.ExtentX, box.ExtentY, box.ExtentZ });
        }

        internal static SplineShape3 Of(GeoSpline3 spline, GeoSphere3 sphere)
        {
            var shape = new SplineShape3();
            GeoPoint3 center = sphere.Center;
            double radius = sphere.Radius;

            shape.Parts.Add(new SpherePartner3(center.X, center.Y, center.Z, radius));
            shape.Holds = point =>
            {
                double dx = point.X - center.X;
                double dy = point.Y - center.Y;
                double dz = point.Z - center.Z;

                return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= radius;
            };

            return shape;
        }

        /// <summary>
        /// Gets a solid: the faces of its material, each a flat region with the outline, and the test of its material.
        /// </summary>
        /// <param name="spline">The spline asked about.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance whose point tolerance says how near the spline must come to an opening for it to be cut in.</param>
        /// <param name="whole">Whether every opening is cut in, as the distance needs, since the nearest material can be on the rim of an opening far from the spline; or only those the spline can reach.</param>
        internal static SplineShape3 Of(GeoSpline3 spline, GeoSolid3 solid, Tolerance tolerance, bool whole)
        {
            Required(solid, nameof(solid));

            var min = new double[3];
            var max = new double[3];
            spline.Curve.Index.Whole(min, max);
            double pad = tolerance.EqualPoint;
            GeoSolid3 material = whole
                ? Material3.Whole(solid, tolerance)
                : Material3.Near(solid, new GeoAabb3(new GeoPoint3(min[0] - pad, min[1] - pad, min[2] - pad), new GeoPoint3(max[0] + pad, max[1] + pad, max[2] + pad)), tolerance);

            var shape = new SplineShape3();

            for (int i = 0; i < material.Faces.Count; i++)
            {
                AddRegion(shape, material.RegionOf(i));
            }

            shape.Holds = point => Gap3.Holds(material, point);

            return shape;
        }

        #endregion

        #region Two splines

        /// <summary>
        /// Says whether a spline comes before another in a fixed order: by degree, then number of control points, then each
        /// control point, weight and knot in turn. Two splines asked about each other are worked out from the one that comes
        /// first, so that each says the same to the bit whichever is asked.
        /// </summary>
        private static bool Precedes(GeoSpline3 a, GeoSpline3 b)
        {
            if (a.Degree != b.Degree)
            {
                return a.Degree < b.Degree;
            }

            if (a.ControlPointCount != b.ControlPointCount)
            {
                return a.ControlPointCount < b.ControlPointCount;
            }

            for (int i = 0; i < a.ControlPointCount; i++)
            {
                if (a.ControlPoints[i].X != b.ControlPoints[i].X)
                {
                    return a.ControlPoints[i].X < b.ControlPoints[i].X;
                }

                if (a.ControlPoints[i].Y != b.ControlPoints[i].Y)
                {
                    return a.ControlPoints[i].Y < b.ControlPoints[i].Y;
                }

                if (a.ControlPoints[i].Z != b.ControlPoints[i].Z)
                {
                    return a.ControlPoints[i].Z < b.ControlPoints[i].Z;
                }
            }

            for (int i = 0; i < a.ControlPointCount; i++)
            {
                if (a.Weights[i] != b.Weights[i])
                {
                    return a.Weights[i] < b.Weights[i];
                }
            }

            for (int i = 0; i < a.Knots.Count; i++)
            {
                if (a.Knots[i] != b.Knots[i])
                {
                    return a.Knots[i] < b.Knots[i];
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the two curves a pair of splines is worked out on: the splines themselves, or, where they lie in one plane to the snap, the
        /// two laid in that plane, with the plane, so that two splines in one plane give what they give in the plane.
        /// </summary>
        private static FlatFrame Curves(GeoSpline3 first, GeoSpline3 second, out NurbsCurve a, out NurbsCurve b)
        {
            if (NurbsCurve.TryLayFlat(first.Curve, second.Curve, out FlatFrame frame, out a, out b))
            {
                return frame;
            }

            a = first.Curve;
            b = second.Curve;
            return null;
        }

        /// <summary>
        /// Gets the point of space of a point a curve gave: itself, or, for a curve laid in a plane, the point of the plane it is.
        /// </summary>
        private static GeoPoint3 Back(FlatFrame frame, double x, double y, double z)
        {
            if (frame == null)
            {
                return new GeoPoint3(x, y, z);
            }

            frame.ToSpace(x, y, out double px, out double py, out double pz);
            return new GeoPoint3(px, py, pz);
        }

        /// <summary>
        /// Gets the distance from a spline to another, the same from either.
        /// </summary>
        internal static double DistanceTo(GeoSpline3 spline, GeoSpline3 other)
        {
            GeoSpline3 first = Precedes(other, spline) ? other : spline;
            GeoSpline3 second = ReferenceEquals(first, spline) ? other : spline;
            Curves(first, second, out NurbsCurve a, out NurbsCurve b);

            return a.TryGetNearest(new List<Partner> { new SplinePartner(b) }, out Contact nearest) ? nearest.Gap : double.PositiveInfinity;
        }

        /// <summary>
        /// Gets the shortest segment from a spline to another, the same from either but turned round.
        /// </summary>
        internal static GeoLine3 GetShortestLineTo(GeoSpline3 spline, GeoSpline3 other)
        {
            bool turned = Precedes(other, spline);
            GeoSpline3 first = turned ? other : spline;
            GeoSpline3 second = turned ? spline : other;
            FlatFrame frame = Curves(first, second, out NurbsCurve a, out NurbsCurve b);

            if (!a.TryGetNearest(new List<Partner> { new SplinePartner(b) }, out Contact nearest))
            {
                throw new InvalidOperationException("The splines have nothing to measure between.");
            }

            var line = new GeoLine3(Back(frame, nearest.Px, nearest.Py, nearest.Pz), Back(frame, nearest.Qx, nearest.Qy, nearest.Qz));

            return turned ? line.Reverse() : line;
        }

        /// <summary>
        /// Gets the places two splines meet, the same points from either, in order along the one asked.
        /// </summary>
        internal static GeoPoint3[] GetIntersections(GeoSpline3 spline, GeoSpline3 other, Tolerance tolerance)
        {
            bool turned = Precedes(other, spline);
            GeoSpline3 first = turned ? other : spline;
            GeoSpline3 second = turned ? spline : other;
            FlatFrame frame = Curves(first, second, out NurbsCurve a, out NurbsCurve b);
            List<Contact> meetings = a.GetMeetings(new List<Partner> { new SplinePartner(b) }, tolerance.EqualPoint);

            // Worked out along the first, they are given along the one asked: the same places, the same bits.
            if (turned)
            {
                meetings.Sort((x, y) => x.Other != y.Other ? x.Other.CompareTo(y.Other) : x.Knot.CompareTo(y.Knot));
            }

            var points = new GeoPoint3[meetings.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = Back(frame, meetings[i].Px, meetings[i].Py, meetings[i].Pz);
            }

            return points;
        }

        #endregion

        #region Asking

        /// <summary>
        /// Gets the distance from a spline to a shape: nought where a point of the spline lies in the material of a body or the two meet,
        /// otherwise the gap, measured exactly.
        /// </summary>
        internal static double DistanceTo(GeoSpline3 spline, SplineShape3 shape)
        {
            if (shape.Holds != null && shape.Holds(spline.StartPoint))
            {
                return 0.0;
            }

            return spline.Curve.TryGetNearest(shape.Parts, out Contact nearest) ? nearest.Gap : double.PositiveInfinity;
        }

        /// <summary>
        /// Gets the places where a spline meets the surface or the outline of a shape.
        /// </summary>
        internal static GeoPoint3[] GetIntersections(GeoSpline3 spline, SplineShape3 shape, Tolerance tolerance)
        {
            // A curve of one plane that the spline lies in too, to the snap, is crossed where the plane has the two cross.
            FlatFrame frame = null;
            List<Contact> meetings = shape.InPlane != null && TryLayFlat(spline, shape, out frame, out NurbsCurve flat)
                ? flat.GetMeetings(shape.InPlane(frame), tolerance.EqualPoint)
                : spline.Curve.GetMeetings(shape.Crossing, tolerance.EqualPoint);
            var points = new GeoPoint3[meetings.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = Back(frame, meetings[i].Px, meetings[i].Py, meetings[i].Pz);
            }

            return points;
        }

        /// <summary>
        /// Lays a spline flat in the plane of a shape that is a curve of one plane, where the two lie in it to the snap: the control points
        /// of the curves of the shape and the ends of its segments are what the plane is found from.
        /// </summary>
        private static bool TryLayFlat(GeoSpline3 spline, SplineShape3 shape, out FlatFrame frame, out NurbsCurve flat)
        {
            flat = null;
            var points = new List<double>();
            double scale = spline.Curve.Scale;

            foreach (Partner part in shape.Crossing)
            {
                if (part is SplinePartner curve)
                {
                    points.AddRange(curve.Curve.Points);
                    scale = Math.Max(scale, curve.Curve.Scale);
                }
                else if (part is LinePartner3 line)
                {
                    points.Add(line.Ox + line.T0 * line.Dx);
                    points.Add(line.Oy + line.T0 * line.Dy);
                    points.Add(line.Oz + line.T0 * line.Dz);
                    points.Add(line.Ox + line.T1 * line.Dx);
                    points.Add(line.Oy + line.T1 * line.Dy);
                    points.Add(line.Oz + line.T1 * line.Dz);
                    scale = Math.Max(scale, line.Scale);
                }
                else
                {
                    frame = null;
                    return false;
                }
            }

            if (points.Count == 0 || !NurbsCurve.InOnePlane(spline.Curve.Points, points.ToArray(), scale, out frame))
            {
                frame = null;
                return false;
            }

            flat = spline.Curve.LaidFlat(frame, scale);
            return true;
        }

        /// <summary>
        /// Gets the shortest segment from a spline to a shape: to its surface or outline, so that of no length where they meet, and the
        /// gap out to the skin of a body that holds the spline.
        /// </summary>
        internal static GeoLine3 GetShortestLineTo(GeoSpline3 spline, SplineShape3 shape)
        {
            if (!spline.Curve.TryGetNearest(shape.Parts, out Contact nearest))
            {
                throw new InvalidOperationException("The shape has nothing to measure to.");
            }

            return new GeoLine3(new GeoPoint3(nearest.Px, nearest.Py, nearest.Pz), new GeoPoint3(nearest.Qx, nearest.Qy, nearest.Qz));
        }

        #endregion
    }
}
