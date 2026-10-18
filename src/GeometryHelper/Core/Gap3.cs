using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The exact gap between straight shapes in space and flat regions: nought only where they truly meet, otherwise the
    /// length of the shortest segment between them, however small.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat region here is a triangle, a polygon or a face, read filled. Two shapes meet where an edge of one passes
    /// through the other, where one holds a corner of the other, or where they lie along each other; apart, the nearest
    /// pair always has an end of a segment on one side, so the gap is the least of four things: a segment through the
    /// region, the height of an end over it where its foot lands on it, the distance of an end to its rim where it does
    /// not, and the distance of the segment to each rim edge. Nothing here widens a shape by a tolerance: a segment
    /// starting 0.0005 off the square 100 by 100 in its plane is 0.0005 from it, and one hovering 0.0003 over it is
    /// 0.0003 from it, from either side and in whatever plane they lie. Whether the two collide is then the question
    /// whether this gap is within the point tolerance.
    /// </para>
    /// <para>
    /// Shapes in one plane are laid out in it to ask where a foot lands and where a segment crosses the plane, through the
    /// containment of the plane with no slack, a rim counting as inside; the height over the plane stays in the gap, so a
    /// shape off the plane by less than the planar tolerance is as far in space as it is.
    /// </para>
    /// </remarks>
    internal static class Gap3
    {
        /// <summary>
        /// A tolerance of nought: a point on a rim counts as inside, and one a hair outside does not.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);

        /// <summary>
        /// Gets the distance from a point to a segment.
        /// </summary>
        internal static double PointToSegment(GeoPoint3 point, GeoPoint3 from, GeoPoint3 to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double dz = to.Z - from.Z;
            double px = point.X - from.X;
            double py = point.Y - from.Y;
            double pz = point.Z - from.Z;
            double length = dx * dx + dy * dy + dz * dz;
            double t = length > 0.0 ? Clamp((px * dx + py * dy + pz * dz) / length) : 0.0;
            double ex = px - dx * t;
            double ey = py - dy * t;
            double ez = pz - dz * t;

            return Math.Sqrt(ex * ex + ey * ey + ez * ez);
        }

        /// <summary>
        /// Gets the distance from a point to a triangle, read from the triangle itself with nothing built for it.
        /// </summary>
        /// <remarks>
        /// The same answer as <see cref="Region.PointGap(GeoPoint3)"/> of the region of the triangle, which a caller asking
        /// of one triangle once does not need to make.
        /// </remarks>
        internal static double PointToTriangle(GeoTriangle3 triangle, GeoPoint3 point)
            => PointToTriangle(triangle.A, triangle.B, triangle.C, triangle.A.GetVectorTo(triangle.B).CrossProduct(triangle.A.GetVectorTo(triangle.C)), point);

        private static double PointToTriangle(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoVector3 normal, GeoPoint3 point)
        {
            // The foot along the normal of the triangle itself, held by the same side tests a segment passing through
            // it is: the closest-point search of Projection3 cancels on a sliver, where the areas it weighs are the
            // difference of two nearly equal products, and put a point on one 2E-6 away.
            double squared = normal.DotProduct(normal);

            if (squared > 0.0)
            {
                double height = normal.DotProduct(a.GetVectorTo(point));

                if (TriangleCovers(a, b, c, normal, point.Add(normal.Multiply(-height / squared))))
                {
                    return Math.Abs(height) / Math.Sqrt(squared);
                }
            }

            return Math.Min(PointToSegment(point, a, b), Math.Min(PointToSegment(point, b, c), PointToSegment(point, c, a)));
        }

        private static bool TriangleCovers(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoVector3 normal, GeoPoint3 point)
            => TriangleSide(a, b, normal, point) >= 0.0 && TriangleSide(b, c, normal, point) >= 0.0 && TriangleSide(c, a, normal, point) >= 0.0;

        private static double TriangleSide(GeoPoint3 from, GeoPoint3 to, GeoVector3 normal, GeoPoint3 point)
            => normal.DotProduct(from.GetVectorTo(to).CrossProduct(from.GetVectorTo(point)));

        /// <summary>
        /// Gets the distance between two segments, each the segment it is however short.
        /// </summary>
        /// <remarks>
        /// The nearest pair of two segments that are not parallel is found from the two lines and kept inside the
        /// segments, as <see cref="Projection3.GetShortestLineTo(GeoLine3, GeoLine3, Tolerance)"/> does, but a segment is
        /// a point only if it has no length at all: a side of a shape a ten-thousandth long is that long. Two that are
        /// parallel to within rounding have no single nearest pair, and the nearest ends are asked instead.
        /// </remarks>
        internal static double SegmentToSegment(GeoPoint3 from1, GeoPoint3 to1, GeoPoint3 from2, GeoPoint3 to2)
        {
            double d1x = to1.X - from1.X, d1y = to1.Y - from1.Y, d1z = to1.Z - from1.Z;
            double d2x = to2.X - from2.X, d2y = to2.Y - from2.Y, d2z = to2.Z - from2.Z;
            double rx = from1.X - from2.X, ry = from1.Y - from2.Y, rz = from1.Z - from2.Z;

            double a = d1x * d1x + d1y * d1y + d1z * d1z;
            double e = d2x * d2x + d2y * d2y + d2z * d2z;
            double f = d2x * rx + d2y * ry + d2z * rz;

            if (a == 0.0)
            {
                return PointToSegment(from1, from2, to2);
            }

            if (e == 0.0)
            {
                return PointToSegment(from2, from1, to1);
            }

            double c = d1x * rx + d1y * ry + d1z * rz;
            double b = d1x * d2x + d1y * d2y + d1z * d2z;
            double denominator = a * e - b * b;
            bool parallel = !(denominator > 1E-12 * a * e);

            double s = parallel ? 0.0 : Clamp((b * f - c * e) / denominator);
            double t = (b * s + f) / e;

            if (t < 0.0)
            {
                t = 0.0;
                s = Clamp(-c / a);
            }
            else if (t > 1.0)
            {
                t = 1.0;
                s = Clamp((b - c) / a);
            }

            double gx = rx + d1x * s - d2x * t;
            double gy = ry + d1y * s - d2y * t;
            double gz = rz + d1z * s - d2z * t;
            double best = Math.Sqrt(gx * gx + gy * gy + gz * gz);

            if (parallel)
            {
                best = Math.Min(best, Math.Min(PointToSegment(from1, from2, to2), PointToSegment(to1, from2, to2)));
                best = Math.Min(best, Math.Min(PointToSegment(from2, from1, to1), PointToSegment(to2, from1, to1)));
            }

            return best;
        }

        private static double Clamp(double value) => Math.Max(0.0, Math.Min(1.0, value));

        /// <summary>
        /// Gets the gap between a segment and a flat region.
        /// </summary>
        /// <param name="region">The region.</param>
        /// <param name="from">The start of the segment.</param>
        /// <param name="to">The end of the segment.</param>
        /// <param name="readShortAsPoint">
        /// true for a segment a caller gave: one no longer than the point tolerance is read as its start where it does not
        /// pass through the region. false for a side of a shape, which is the side it is.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
        internal static double Segment(Region region, GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance, double stopAt)
        {
            if (region.Pierces(from, to))
            {
                return 0.0;
            }

            if (readShortAsPoint && from.GetDistanceSquaredTo(to) <= tolerance.EqualPoint * tolerance.EqualPoint)
            {
                // A segment too short to have a direction is its start, unless it truly meets the region, as it does lying
                // across a side or through a corner: it is nought from it however short.
                double reach = region.AroundGap(from, to, 0.0);
                double rounding = 1E-12 * Math.Max(1.0, Math.Max(Math.Abs(from.X), Math.Max(Math.Abs(from.Y), Math.Abs(from.Z))));

                return reach <= rounding ? 0.0 : region.PointGap(from);
            }

            return region.AroundGap(from, to, stopAt);
        }

        /// <summary>
        /// Gets the gap between a chain and a flat region, each side of the chain the side it is.
        /// </summary>
        internal static double Chain(Region region, GeoPolyline3 polyline, double stopAt)
        {
            GeoAabb3 box = region.Box;
            double best = double.MaxValue;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);

                if (BoxGap(edge.StartPoint, edge.EndPoint, box) > best)
                {
                    continue;
                }

                best = Math.Min(best, Segment(region, edge.StartPoint, edge.EndPoint, false, Exactly, stopAt));

                if (best <= stopAt)
                {
                    return best;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the gap between a ray and a flat region: the ray as far as the corner farthest from its origin, past which
        /// it only draws away from every point of the region.
        /// </summary>
        internal static double Ray(Region region, GeoRay3 ray, double stopAt)
        {
            double reach = region.Reach(ray.Origin);

            return Segment(region, ray.Origin, Parametrization3.GetPointAtDistance(ray, reach), false, Exactly, stopAt);
        }

        /// <summary>
        /// Gets the gap between two flat regions: the least of every edge of the first against the second and every edge of
        /// the second against the first.
        /// </summary>
        /// <remarks>
        /// Two regions that do not meet always have their nearest pair on an edge of one of them, and two that meet have an
        /// edge of one passing through the other, a corner of one held by the other or an edge along one. An edge that
        /// cannot come as near as the least so far, its box being that much clear of the region's, is passed over.
        /// </remarks>
        internal static double Regions(Region first, Region second, double stopAt)
        {
            double best = double.MaxValue;

            if (!Walk(first, second, stopAt, ref best))
            {
                Walk(second, first, stopAt, ref best);
            }

            return best;
        }

        /// <summary>
        /// Determines whether an edge of one region passes through the other, or ends on it, with no slack: the two cross.
        /// </summary>
        internal static bool Cross(Region first, Region second) => Passes(first, second) || Passes(second, first);

        private static bool Passes(Region edges, Region through)
        {
            for (int i = 0; i < edges.EdgeCount; i++)
            {
                if (through.Pierces(edges.EdgeFrom(i), edges.EdgeTo(i)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Weighs every edge of one region against another, false while no gap within <paramref name="stopAt"/> has been
        /// found.
        /// </summary>
        private static bool Walk(Region edges, Region against, double stopAt, ref double best)
        {
            GeoAabb3 box = against.Box;

            for (int i = 0; i < edges.EdgeCount; i++)
            {
                GeoPoint3 from = edges.EdgeFrom(i);
                GeoPoint3 to = edges.EdgeTo(i);

                if (BoxGap(from, to, box) > best)
                {
                    continue;
                }

                if (against.Pierces(from, to))
                {
                    best = 0.0;
                    return true;
                }

                // Each corner is the start of one side of its ring, so it is weighed once here, not again as the end of the
                // side before it.
                if (!Beyond(box.DistanceTo(from), best))
                {
                    best = Math.Min(best, against.PointGap(from));
                }

                for (int side = 0; side < against.EdgeCount && best > stopAt; side++)
                {
                    GeoPoint3 start = against.EdgeFrom(side);
                    GeoPoint3 end = against.EdgeTo(side);

                    if (!Beyond(SegmentsGap(from, to, start, end), best))
                    {
                        best = Math.Min(best, SegmentToSegment(from, to, start, end));
                    }
                }

                if (best <= stopAt)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the gap between a segment and the surface of a body: the least over its faces, a face read by its
        /// material, holes left open.
        /// </summary>
        /// <param name="material">The body, its openings already cut in.</param>
        /// <param name="from">The start of the segment.</param>
        /// <param name="to">The end of the segment.</param>
        /// <param name="readShortAsPoint">true for a segment a caller gave; see <see cref="Segment"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="within">A face whose box stands further than this from the segment's is passed over.</param>
        /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
        /// <remarks>
        /// A body whose segment lies wholly inside it, touching no face, is not met here: where its ends are is asked of
        /// <see cref="Holds"/>.
        /// </remarks>
        internal static double SurfaceSegment(
            GeoSolid3 material, GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance, double within, double stopAt)
            => SurfaceSegment(material, from, to, readShortAsPoint, tolerance, within, stopAt, false);

        private static double SurfaceSegment(
            GeoSolid3 material, GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance, double within, double stopAt, bool ray)
        {
            IReadOnlyList<GeoFace3> faces = material.Faces;
            var reach = new double[faces.Count];
            double[] rank = ray ? new double[faces.Count] : reach;

            for (int i = 0; i < reach.Length; i++)
            {
                GeoAabb3 box = faces[i].GetAabb();

                reach[i] = Math.Max(0.0, BoxGap(from, to, box) - PlanarSlack);

                if (ray)
                {
                    // A ray runs as far as the body does, so its box says little: the faces nearest its origin are weighed
                    // first, and the rest are passed over once one is found no further than they can be.
                    rank[i] = box.DistanceTo(from);
                }
            }

            double best = double.MaxValue;

            foreach (int i in Nearest(rank))
            {
                if (reach[i] > within || Beyond(reach[i], best))
                {
                    continue;
                }

                Region region = material.RegionOf(i);
                double height = region.HeightGap(from, to);

                if (height > within || Beyond(height, best))
                {
                    continue;
                }

                best = Math.Min(best, Segment(region, from, to, readShortAsPoint, tolerance, stopAt));

                if (best <= stopAt)
                {
                    return best;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the gap between a ray and the surface of a body: the ray as far as the farthest corner of the body's box,
        /// past which it only draws away.
        /// </summary>
        internal static double SurfaceRay(GeoSolid3 material, GeoRay3 ray, double stopAt)
        {
            GeoAabb3 box = material.GetAabb();
            double reach = ray.Origin.DistanceTo(box.Center) + 0.5 * box.Min.DistanceTo(box.Max);

            return SurfaceSegment(
                material, ray.Origin, Parametrization3.GetPointAtDistance(ray, reach), false, Exactly, double.MaxValue, stopAt, true);
        }

        /// <summary>
        /// Gets the gap between a flat region and the surface of a body: the least over its faces.
        /// </summary>
        /// <param name="material">The body, its openings already cut in.</param>
        /// <param name="region">The region.</param>
        /// <param name="within">A face whose box stands further than this from the region's is passed over.</param>
        /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
        internal static double SurfaceRegion(GeoSolid3 material, Region region, double within, double stopAt)
        {
            IReadOnlyList<GeoFace3> faces = material.Faces;
            var reach = new double[faces.Count];

            for (int i = 0; i < reach.Length; i++)
            {
                reach[i] = Math.Max(0.0, region.Box.DistanceTo(faces[i].GetAabb()) - PlanarSlack);
            }

            double best = double.MaxValue;

            foreach (int i in Nearest(reach))
            {
                if (reach[i] > within || Beyond(reach[i], best))
                {
                    continue;
                }

                best = Math.Min(best, Regions(region, material.RegionOf(i), stopAt));

                if (best <= stopAt)
                {
                    return best;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the gap between a point and the surface of a body: the least over its faces.
        /// </summary>
        internal static double SurfacePoint(GeoSolid3 material, GeoPoint3 point)
        {
            IReadOnlyList<GeoFace3> faces = material.Faces;
            var reach = new double[faces.Count];

            for (int i = 0; i < reach.Length; i++)
            {
                reach[i] = Math.Max(0.0, faces[i].GetAabb().DistanceTo(point) - PlanarSlack);
            }

            double best = double.MaxValue;

            foreach (int i in Nearest(reach))
            {
                if (Beyond(reach[i], best))
                {
                    continue;
                }

                Region region = material.RegionOf(i);

                if (Beyond(region.HeightGap(point, point), best))
                {
                    continue;
                }

                best = Math.Min(best, region.PointGap(point));
            }

            return best;
        }

        /// <summary>
        /// Determines whether a lower bound on a gap stands clear of the least gap found, so that the candidate it bounds can
        /// be passed over.
        /// </summary>
        /// <remarks>
        /// A candidate is passed over only when its bound clears the least by a billionth of a millimetre and a trillionth of
        /// the gap, more than rounding can make up. Two candidates of one gap round differently in the last digits, so one
        /// that merely ties is weighed all the same, and the answer is the least of every candidate whichever order they are
        /// weighed in. The same rule serves a distance and a collision, which stops at the first candidate within the
        /// distance it is asked about, so that the two weigh the same candidates and cannot disagree about a pair standing
        /// that distance apart. A bound that is not a number is never beyond it.
        /// </remarks>
        private static bool Beyond(double bound, double best) => bound > best + best * 1E-12 + 1E-9;
        /// <summary>
        /// Gets the indices of some faces, nearest first, a tie going to the lower index.
        /// </summary>
        private static int[] Nearest(double[] reach)
        {
            var order = new int[reach.Length];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            if (order.Length > 16)
            {
                Array.Sort(order, (a, b) =>
                {
                    int by = reach[a].CompareTo(reach[b]);

                    return by != 0 ? by : a.CompareTo(b);
                });

                return order;
            }

            // A body has a handful of faces: an insertion that moves an index past only the larger keys keeps the lower of
            // equal ones in front.
            for (int i = 1; i < order.Length; i++)
            {
                int moved = order[i];
                int at = i - 1;

                while (at >= 0 && reach[order[at]].CompareTo(reach[moved]) > 0)
                {
                    order[at + 1] = order[at];
                    at--;
                }

                order[at + 1] = moved;
            }

            return order;
        }
        /// <summary>
        /// Gets how near, at the least, two segments can come: the gap between their boxes.
        /// </summary>
        private static double SegmentsGap(GeoPoint3 from1, GeoPoint3 to1, GeoPoint3 from2, GeoPoint3 to2)
        {
            double x = Math.Max(0.0, Math.Max(Math.Min(from2.X, to2.X) - Math.Max(from1.X, to1.X), Math.Min(from1.X, to1.X) - Math.Max(from2.X, to2.X)));
            double y = Math.Max(0.0, Math.Max(Math.Min(from2.Y, to2.Y) - Math.Max(from1.Y, to1.Y), Math.Min(from1.Y, to1.Y) - Math.Max(from2.Y, to2.Y)));
            double z = Math.Max(0.0, Math.Max(Math.Min(from2.Z, to2.Z) - Math.Max(from1.Z, to1.Z), Math.Min(from1.Z, to1.Z) - Math.Max(from2.Z, to2.Z)));

            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Gets how near, at the least, a segment can come to a box: the gap between its box and the box.
        /// </summary>
        internal static double BoxGap(GeoPoint3 from, GeoPoint3 to, GeoAabb3 box)
        {
            double x = Math.Max(0.0, Math.Max(box.Min.X - Math.Max(from.X, to.X), Math.Min(from.X, to.X) - box.Max.X));
            double y = Math.Max(0.0, Math.Max(box.Min.Y - Math.Max(from.Y, to.Y), Math.Min(from.Y, to.Y) - box.Max.Y));
            double z = Math.Max(0.0, Math.Max(box.Min.Z - Math.Max(from.Z, to.Z), Math.Min(from.Z, to.Z) - box.Max.Z));

            return Math.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>
        /// Determines whether a solid holds a point, on its surface included, to within rounding: the point is held where
        /// the material is and a hair outside is outside.
        /// </summary>
        /// <remarks>
        /// The point is located under a tolerance of a billionth of a millimetre, not of nought: a face holds a crossing
        /// of a ray only within the planar tolerance of its plane, and a crossing that rounding leaves a trillionth off the
        /// plane, as it does in a body turned about in space, would be refused by one of nought and the count of the
        /// crossings would come out wrong.
        /// </remarks>
        internal static bool Holds(GeoSolid3 solid, GeoPoint3 point)
            => Containment3.Locate(solid, point, Fine) != Enums.PointLocation.OutSide;

        /// <summary>
        /// A tolerance a billionth of a millimetre wide, which no model comes near and rounding stays well inside.
        /// </summary>
        private static Tolerance Fine => new Tolerance(1E-9, 1E-9, Tolerance.DefaultEqualAngleRad, 1E-9);

        /// <summary>
        /// How far a corner may stand off the plane of its region and be taken for on it: a billionth of a millimetre, which
        /// no model comes near and rounding stays well inside.
        /// </summary>
        private const double Hair = 1E-9;

        /// <summary>
        /// How far the box of a face may stand from what it is weighed against and the face, read flat in its plane, still come
        /// nearer: the most its corners may stand off that plane, a thousandth of a millimetre, the planar tolerance a polygon is
        /// made within.
        /// </summary>
        private const double PlanarSlack = 1E-3;

        /// <summary>
        /// An oriented box read for the gap to a segment, a chain or a ray, in its own frame: where a segment passes through
        /// the solid box, and otherwise the least of the ends and the twelve edges.
        /// </summary>
        /// <remarks>
        /// A box is convex, so a segment clear of it has its nearest pair at an end of the segment, or between its inside and
        /// an edge of the box: against a face the distance changes linearly along the segment and is least at an end, or
        /// stays the same along it, and then an edge of the face is as near. This is the answer of the body of the box
        /// (<see cref="GeoObb3.ToSolid"/>) without cutting the body into triangles.
        /// </remarks>
        internal readonly struct Block
        {
            private readonly GeoPoint3 _centre;
            private readonly GeoVector3 _x;
            private readonly GeoVector3 _y;
            private readonly GeoVector3 _z;
            private readonly double _hx;
            private readonly double _hy;
            private readonly double _hz;

            /// <summary>Reads an oriented box.</summary>
            internal Block(GeoObb3 box)
            {
                GeoCoordinateSystem3 frame = box.CoordinateSystem;

                _centre = frame.Origin;
                _x = frame.XAxis;
                _y = frame.YAxis;
                _z = frame.ZAxis;
                _hx = box.ExtentX;
                _hy = box.ExtentY;
                _hz = box.ExtentZ;
            }

            /// <summary>
            /// Gets the gap between a segment and the box.
            /// </summary>
            /// <param name="from">The start of the segment.</param>
            /// <param name="to">The end of the segment.</param>
            /// <param name="readShortAsPoint">
            /// true for a segment a caller gave: one no longer than the point tolerance is read as its start where it does not
            /// touch the box. false for a side of a shape.
            /// </param>
            /// <param name="tolerance">The tolerance.</param>
            /// <param name="stopAt">A gap no longer than this ends the search; nought asks for the least of all.</param>
            internal double Segment(GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance, double stopAt)
                => Segment(Local(from), Local(to), from, to, readShortAsPoint, tolerance, stopAt);

            /// <summary>
            /// Determines whether a segment a caller gave comes within the point tolerance of the box, the box of the segment
            /// asked first, in the frame of the box, so that a segment plainly clear of it goes no further.
            /// </summary>
            /// <param name="from">The start of the segment.</param>
            /// <param name="to">The end of the segment.</param>
            /// <param name="tolerance">The tolerance.</param>
            /// <param name="reach">How far apart the two boxes may stand and the shapes still come within the tolerance.</param>
            internal bool Within(GeoPoint3 from, GeoPoint3 to, Tolerance tolerance, double reach)
            {
                GeoPoint3 p = Local(from);
                GeoPoint3 q = Local(to);

                return BoxGapLocal(p, q) <= reach
                    && Segment(p, q, from, to, true, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;
            }

            private double Segment(
                GeoPoint3 p, GeoPoint3 q, GeoPoint3 from, GeoPoint3 to, bool readShortAsPoint, Tolerance tolerance, double stopAt)
            {
                // An end in the box or on it is nought from it, as a segment passing through is; asked first, since it is
                // three clamps and a square root where the slabs are divisions.
                double atStart = Outside(p);

                if (atStart <= 0.0)
                {
                    return 0.0;
                }

                double atEnd = Outside(q);

                if (atEnd <= 0.0 || Passes(p, q))
                {
                    return 0.0;
                }

                if (readShortAsPoint && from.GetDistanceSquaredTo(to) <= tolerance.EqualPoint * tolerance.EqualPoint)
                {
                    return atStart;
                }

                double best = Math.Min(atStart, atEnd);

                if (best <= stopAt)
                {
                    return best;
                }

                for (int axis = 0; axis < 3; axis++)
                {
                    for (int corner = 0; corner < 4; corner++)
                    {
                        double a = (corner & 1) == 0 ? -1.0 : 1.0;
                        double b = (corner & 2) == 0 ? -1.0 : 1.0;
                        GeoPoint3 start = axis == 0 ? new GeoPoint3(-_hx, a * _hy, b * _hz)
                            : axis == 1 ? new GeoPoint3(a * _hx, -_hy, b * _hz)
                            : new GeoPoint3(a * _hx, b * _hy, -_hz);
                        GeoPoint3 end = axis == 0 ? new GeoPoint3(_hx, a * _hy, b * _hz)
                            : axis == 1 ? new GeoPoint3(a * _hx, _hy, b * _hz)
                            : new GeoPoint3(a * _hx, b * _hy, _hz);

                        best = Math.Min(best, SegmentToSegment(p, q, start, end));

                        if (best <= stopAt)
                        {
                            return best;
                        }
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the gap between a chain and the box, each side of the chain the side it is.
            /// </summary>
            internal double Chain(GeoPolyline3 polyline, double stopAt)
            {
                double best = double.MaxValue;

                for (int i = 0; i < polyline.EdgeCount; i++)
                {
                    GeoLine3 edge = polyline.GetEdgeAt(i);

                    best = Math.Min(best, Segment(edge.StartPoint, edge.EndPoint, false, Exactly, stopAt));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            /// <summary>
            /// Gets the gap between a ray and the box: the ray as far as the farthest corner of the box, past which it only
            /// draws away.
            /// </summary>
            internal double Ray(GeoRay3 ray, double stopAt)
            {
                double reach = ray.Origin.DistanceTo(_centre) + Math.Sqrt(_hx * _hx + _hy * _hy + _hz * _hz);

                return Segment(ray.Origin, Parametrization3.GetPointAtDistance(ray, reach), false, Exactly, stopAt);
            }

            /// <summary>
            /// Gets how near, at the least, a segment can come to the box: the gap between the box of the segment, in the
            /// frame of the box, and the box.
            /// </summary>
            internal double BoxGap(GeoPoint3 from, GeoPoint3 to) => BoxGapLocal(Local(from), Local(to));

            private double BoxGapLocal(GeoPoint3 p, GeoPoint3 q)
            {
                double x = Math.Max(0.0, Math.Max(-_hx - Math.Max(p.X, q.X), Math.Min(p.X, q.X) - _hx));
                double y = Math.Max(0.0, Math.Max(-_hy - Math.Max(p.Y, q.Y), Math.Min(p.Y, q.Y) - _hy));
                double z = Math.Max(0.0, Math.Max(-_hz - Math.Max(p.Z, q.Z), Math.Min(p.Z, q.Z) - _hz));

                return Math.Sqrt(x * x + y * y + z * z);
            }

            private GeoPoint3 Local(GeoPoint3 point)
            {
                GeoVector3 d = _centre.GetVectorTo(point);

                return new GeoPoint3(d.DotProduct(_x), d.DotProduct(_y), d.DotProduct(_z));
            }

            /// <summary>
            /// Gets the distance from a point, in the frame of the box, to the box: nought in it or on it.
            /// </summary>
            private double Outside(GeoPoint3 p)
            {
                double x = Math.Max(0.0, Math.Abs(p.X) - _hx);
                double y = Math.Max(0.0, Math.Abs(p.Y) - _hy);
                double z = Math.Max(0.0, Math.Abs(p.Z) - _hz);

                return Math.Sqrt(x * x + y * y + z * z);
            }

            /// <summary>
            /// Determines whether a segment, in the frame of the box, has a point in the box or on it: the stretch of it
            /// inside each of the three slabs has a part in common.
            /// </summary>
            private bool Passes(GeoPoint3 p, GeoPoint3 q)
            {
                double near = 0.0;
                double far = 1.0;

                return Slab(p.X, q.X, _hx, ref near, ref far) && Slab(p.Y, q.Y, _hy, ref near, ref far) && Slab(p.Z, q.Z, _hz, ref near, ref far);
            }

            private static bool Slab(double from, double to, double half, ref double near, ref double far)
            {
                double along = to - from;

                if (along == 0.0)
                {
                    return Math.Abs(from) <= half;
                }

                double a = (-half - from) / along;
                double b = (half - from) / along;

                near = Math.Max(near, Math.Min(a, b));
                far = Math.Min(far, Math.Max(a, b));

                return near <= far;
            }
        }

        /// <summary>
        /// A triangle, a polygon or a face, read for the gap: the segments of its rim, the height of a point over it where
        /// the point's foot lands on it, and where a segment passes through it.
        /// </summary>
        internal sealed class Region
        {
            private readonly bool _isTriangle;
            private readonly GeoPoint3 _a;
            private readonly GeoPoint3 _b;
            private readonly GeoPoint3 _c;
            private readonly GeoVector3 _normal;
            private readonly GeoTriangle3 _triangle;

            private readonly GeoCoordinateSystem3 _frame;
            private readonly GeoPolygon3 _polygonIn3;
            private readonly GeoFace3 _faceIn3;
            private volatile bool _laidOut;
            private GeoPolygon2 _polygon;
            private GeoFace2 _face;
            private volatile GeoLine3[] _rim;
            private volatile bool _offsetKnown;
            private double _offset;
            private readonly GeoAabb3 _box;
            private volatile bool _flatBoxKnown;
            private GeoAabb3 _flatBox;

            private Region(GeoTriangle3 triangle)
            {
                _isTriangle = true;
                _triangle = triangle;
                _a = triangle.A;
                _b = triangle.B;
                _c = triangle.C;
                _normal = _a.GetVectorTo(_b).CrossProduct(_a.GetVectorTo(_c));
                _box = triangle.GetAabb();
            }

            private Region(GeoCoordinateSystem3 frame, GeoPolygon3 polygon, GeoFace3 face, GeoAabb3 box)
            {
                _frame = frame;
                _polygonIn3 = polygon;
                _faceIn3 = face;
                _box = box;
            }

            /// <summary>The number of sides of the rim: the outline and then each hole, each in order round it.</summary>
            internal int EdgeCount => _isTriangle ? 3 : Rim.Length;

            /// <summary>Gets where a side of the rim starts.</summary>
            internal GeoPoint3 EdgeFrom(int index)
                => _isTriangle ? (index == 0 ? _a : index == 1 ? _b : _c) : Rim[index].StartPoint;

            /// <summary>Gets where a side of the rim ends.</summary>
            internal GeoPoint3 EdgeTo(int index)
                => _isTriangle ? (index == 0 ? _b : index == 1 ? _c : _a) : Rim[index].EndPoint;

            /// <summary>
            /// The sides of a polygon or a face, made when first asked: a region plainly clear of what it is weighed against is
            /// never walked.
            /// </summary>
            private GeoLine3[] Rim
                => _rim ?? (_rim = _faceIn3 != null ? Intersection3.EdgesOf(_faceIn3).ToArray() : _polygonIn3.GetEdges());

            /// <summary>
            /// The box round the region, as it is read: a polygon or a face is read flat in its plane, and its corners may stand
            /// off that plane, so the box of the corners is widened by as far as they stand. A triangle is flat already.
            /// </summary>
            internal GeoAabb3 Box
            {
                get
                {
                    if (_isTriangle)
                    {
                        return _box;
                    }

                    if (!_flatBoxKnown)
                    {
                        double off = Offset();

                        _flatBox = off > Hair ? _box.Expand(off) : _box;
                        _flatBoxKnown = true;
                    }

                    return _flatBox;
                }
            }

            /// <summary>Reads a triangle.</summary>
            internal static Region Of(GeoTriangle3 triangle) => new Region(triangle);

            /// <summary>Reads a polygon, laid out in its own plane.</summary>
            internal static Region Of(GeoPolygon3 polygon)
                => new Region(FrameOf(polygon.Vertices, polygon.Normal), polygon, null, polygon.GetAabb());

            /// <summary>Reads the material of a face, laid out in the plane of its outline.</summary>
            internal static Region Of(GeoFace3 face)
                => new Region(FrameOf(face.Boundary.Vertices, face.Boundary.Normal), null, face, face.GetAabb());

            /// <summary>
            /// Gets the frame of a ring of corners: the plane through the first, normal to the sum of the areas the sides
            /// sweep about it.
            /// </summary>
            /// <remarks>
            /// The normal of the polygon is that sum taken about the origin of space, and the digits of its coordinates
            /// cancel: for a face a few tenths of a millimetre across 80 000 from the origin, it is tilted by a millionth of
            /// a radian, enough to put a point on the face 1.3E-6 off it. About the first corner the coordinates are the
            /// size of the face. The normal the polygon keeps is not changed, since the booleans read it; this is the frame
            /// that is measured in.
            /// </remarks>
            private static GeoCoordinateSystem3 FrameOf(IReadOnlyList<GeoPoint3> corners, GeoVector3 normal)
            {
                GeoPoint3 origin = corners[0];
                double nx = 0.0;
                double ny = 0.0;
                double nz = 0.0;

                double cx = 0.0, cy = 0.0, cz = 0.0;

                for (int i = 1; i <= corners.Count; i++)
                {
                    GeoPoint3 next = i == corners.Count ? origin : corners[i];
                    double dx = next.X - origin.X, dy = next.Y - origin.Y, dz = next.Z - origin.Z;

                    nx += (cy - dy) * (cz + dz);
                    ny += (cz - dz) * (cx + dx);
                    nz += (cx - dx) * (cy + dy);

                    cx = dx;
                    cy = dy;
                    cz = dz;
                }

                double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);

                return PlanarMap.FrameOf(corners, length > 0.0 ? new GeoVector3(nx / length, ny / length, nz / length) : normal);
            }

            /// <summary>
            /// Gets how near, at the least, a segment can come to the region: the height of its nearer end over the plane of the
            /// region where both ends stand on one side, and nought where they do not. A triangle asks nothing of it.
            /// </summary>
            internal double HeightGap(GeoPoint3 from, GeoPoint3 to)
            {
                if (_isTriangle)
                {
                    return 0.0;
                }

                double atFrom = _frame.ZAxis.DotProduct(_frame.Origin.GetVectorTo(from));
                double atTo = _frame.ZAxis.DotProduct(_frame.Origin.GetVectorTo(to));

                return atFrom > 0.0 && atTo > 0.0 ? Math.Min(atFrom, atTo) : atFrom < 0.0 && atTo < 0.0 ? -Math.Max(atFrom, atTo) : 0.0;
            }

            /// <summary>
            /// Gets how far a point is from the farthest corner of the region, which a ray from it never need pass.
            /// </summary>
            internal double Reach(GeoPoint3 origin)
            {
                double reach = 0.0;

                for (int i = 0; i < EdgeCount; i++)
                {
                    reach = Math.Max(reach, origin.DistanceTo(EdgeFrom(i)));
                }

                return reach;
            }

            /// <summary>
            /// Gets the distance from a point to the region: the height over it where the foot lands on it, rim included,
            /// and otherwise the distance to the nearest side.
            /// </summary>
            internal double PointGap(GeoPoint3 point)
            {
                if (_isTriangle)
                {
                    return PointToTriangle(_a, _b, _c, _normal, point);
                }

                GeoPoint3 local = _frame.ToLocal(point);

                // A foot on the rim is as far from the rim as it is high, so the rim need not be asked here, as it is of a
                // segment passing through the region, unless the corners stand off the plane: then the point may lie on a side
                // that climbs from it, and the side is nearer than the height.
                if (CoversInside(local.X, local.Y))
                {
                    return StandsOffFlat() ? Math.Min(Math.Abs(local.Z), RimGap(point)) : Math.Abs(local.Z);
                }

                return RimGap(point);
            }

            /// <summary>
            /// Gets the distance from a point to the sides of the rim: the outline and each hole.
            /// </summary>
            private double RimGap(GeoPoint3 point)
            {
                double best = Ring(point, _faceIn3 != null ? _faceIn3.Boundary.Vertices : _polygonIn3.Vertices);

                if (_faceIn3 != null)
                {
                    foreach (GeoPolygon3 hole in _faceIn3.Holes)
                    {
                        best = Math.Min(best, Ring(point, hole.Vertices));
                    }
                }

                return best;
            }

            /// <summary>
            /// Determines whether a corner of the region stands off its plane by more than rounding: a polygon a few
            /// ten-thousandths out of flat is a polygon all the same.
            /// </summary>
            private bool StandsOffFlat() => Offset() > Hair;

            /// <summary>
            /// Gets how far the farthest corner of the region stands off its plane, made when first asked.
            /// </summary>
            private double Offset()
            {
                if (!_offsetKnown)
                {
                    double farthest = 0.0;

                    foreach (IReadOnlyList<GeoPoint3> ring in Rings())
                    {
                        for (int i = 0; i < ring.Count; i++)
                        {
                            farthest = Math.Max(farthest, Math.Abs(_frame.ZAxis.DotProduct(_frame.Origin.GetVectorTo(ring[i]))));
                        }
                    }

                    _offset = farthest;
                    _offsetKnown = true;
                }

                return _offset;
            }

            private IEnumerable<IReadOnlyList<GeoPoint3>> Rings()
            {
                if (_faceIn3 == null)
                {
                    yield return _polygonIn3.Vertices;
                    yield break;
                }

                yield return _faceIn3.Boundary.Vertices;

                foreach (GeoPolygon3 hole in _faceIn3.Holes)
                {
                    yield return hole.Vertices;
                }
            }

            /// <summary>
            /// Determines whether a segment passes through the region, or ends on it: its ends are not both to one side of
            /// the plane, and where it crosses the plane it lands on the region. A segment lying in the plane crosses it
            /// nowhere and is left to its ends and the rim.
            /// </summary>
            internal bool Pierces(GeoPoint3 from, GeoPoint3 to)
            {
                if (_isTriangle)
                {
                    double atFrom = _normal.DotProduct(_a.GetVectorTo(from));
                    double atTo = _normal.DotProduct(_a.GetVectorTo(to));

                    if ((atFrom > 0.0 && atTo > 0.0) || (atFrom < 0.0 && atTo < 0.0) || atFrom == atTo)
                    {
                        return false;
                    }

                    double along = atFrom / (atFrom - atTo);

                    return CoversAt(new GeoPoint3(
                        from.X + along * (to.X - from.X), from.Y + along * (to.Y - from.Y), from.Z + along * (to.Z - from.Z)));
                }

                GeoPoint3 start = _frame.ToLocal(from);
                GeoPoint3 end = _frame.ToLocal(to);

                if ((start.Z > 0.0 && end.Z > 0.0) || (start.Z < 0.0 && end.Z < 0.0) || start.Z == end.Z)
                {
                    return false;
                }

                double crossing = start.Z / (start.Z - end.Z);

                // Where the segment crosses the plane, against the box of the region before the region is laid out: a crossing
                // beyond the box, widened by as far as the corners stand off the plane, is plainly not on it.
                double x = from.X + crossing * (to.X - from.X);
                double y = from.Y + crossing * (to.Y - from.Y);
                double z = from.Z + crossing * (to.Z - from.Z);

                if ((x < _box.Min.X || x > _box.Max.X || y < _box.Min.Y || y > _box.Max.Y || z < _box.Min.Z || z > _box.Max.Z) &&
                    OutsideByMore(x, y, z, Offset() + Hair))
                {
                    return false;
                }

                return Covers(new GeoPoint2(start.X + crossing * (end.X - start.X), start.Y + crossing * (end.Y - start.Y)));
            }

            /// <summary>
            /// Gets the gap between a segment that does not pass through the region and the region: the least of its two ends
            /// and every side of the rim.
            /// </summary>
            internal double AroundGap(GeoPoint3 from, GeoPoint3 to, double stopAt)
            {
                double best = PointGap(from);

                if (best <= stopAt)
                {
                    return best;
                }

                // An end no nearer the region's box than the least so far, or a side no nearer the segment's box, cannot lower it.
                if (!Beyond(Box.DistanceTo(to), best))
                {
                    best = Math.Min(best, PointGap(to));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                for (int i = 0; i < EdgeCount; i++)
                {
                    GeoPoint3 start = EdgeFrom(i);
                    GeoPoint3 end = EdgeTo(i);

                    if (Beyond(SegmentsGap(from, to, start, end), best))
                    {
                        continue;
                    }

                    best = Math.Min(best, SegmentToSegment(from, to, start, end));

                    if (best <= stopAt)
                    {
                        return best;
                    }
                }

                return best;
            }

            private bool OutsideByMore(double x, double y, double z, double margin)
                => x < _box.Min.X - margin || x > _box.Max.X + margin
                    || y < _box.Min.Y - margin || y > _box.Max.Y + margin
                    || z < _box.Min.Z - margin || z > _box.Max.Z + margin;

            private void LayOut()
            {
                if (_laidOut)
                {
                    return;
                }

                // Made whole and then announced, so that a region shared between threads is never read half laid out: two that
                // lay it out together make the same layout.
                if (_faceIn3 != null)
                {
                    PlanarMap.TryProjectToFace2(_frame, _faceIn3, Exactly, out GeoFace2 face);
                    _face = face;
                }
                else
                {
                    PlanarMap.TryProjectToPolygon2(_frame, _polygonIn3, Exactly, out GeoPolygon2 polygon);
                    _polygon = polygon;
                }

                _laidOut = true;
            }

            /// <summary>
            /// Gets the distance from a point to a ring of corners, side by side.
            /// </summary>
            private static double Ring(GeoPoint3 point, IReadOnlyList<GeoPoint3> corners)
            {
                double best = double.MaxValue;

                for (int i = 0; i < corners.Count; i++)
                {
                    best = Math.Min(best, PointToSegment(point, corners[i], corners[(i + 1) % corners.Count]));
                }

                return best;
            }

            /// <summary>
            /// Determines whether a point of the plane lies in the region by the crossings of a ray from it, as far as the
            /// rim: a point on the rim may be read either way. The rings are read in the frame as they stand, the corners
            /// laid out one by one, as <see cref="LayOut"/> lays them out, without making the polygon that is only wanted
            /// for the rim.
            /// </summary>
            private bool CoversInside(double x, double y)
            {
                if (_faceIn3 != null)
                {
                    if (!Crossed(_faceIn3.Boundary.Vertices, x, y))
                    {
                        return false;
                    }

                    foreach (GeoPolygon3 hole in _faceIn3.Holes)
                    {
                        if (Crossed(hole.Vertices, x, y))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return Crossed(_polygonIn3.Vertices, x, y);
            }

            private bool Crossed(IReadOnlyList<GeoPoint3> ring, double x, double y)
            {
                bool inside = false;
                int n = ring.Count;
                GeoPoint3 p2 = _frame.ToLocal(ring[n - 1]);

                for (int i = 0; i < n; i++)
                {
                    GeoPoint3 p1 = _frame.ToLocal(ring[i]);

                    if (((p1.Y > y) != (p2.Y > y)) &&
                        (x < (p2.X - p1.X) * (y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                    {
                        inside = !inside;
                    }

                    p2 = p1;
                }

                return inside;
            }

            private bool Covers(GeoPoint2 point)
            {
                LayOut();

                if (_face != null)
                {
                    return Containment2.Contains(_face, point, Exactly);
                }

                return _polygon != null && Containment2.Contains(_polygon, point, Exactly);
            }

            private bool CoversAt(GeoPoint3 point) => TriangleCovers(_a, _b, _c, _normal, point);
        }
    }
}
