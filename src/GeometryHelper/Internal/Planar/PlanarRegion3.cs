using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal.Planar;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The frame a flat shape is laid out in to reach the planar algorithms: an origin on the shape, the shape's
    /// unit normal, and two unit axes in its plane with u × v along the normal, so that a loop running
    /// counter-clockwise about the normal runs counter-clockwise in the frame too.
    /// </summary>
    internal readonly struct PlaneFrame
    {
        public PlaneFrame(GeoPoint3 origin, GeoVector3 normal, GeoVector3 uAxis)
        {
            Origin = origin;
            Normal = normal;
            U = uAxis;
            V = normal.CrossProduct(uAxis);
        }

        public GeoPoint3 Origin { get; }

        public GeoVector3 Normal { get; }

        public GeoVector3 U { get; }

        public GeoVector3 V { get; }

        /// <summary>
        /// Builds the frame of a flat shape: the origin at its first point and the first axis along the first of
        /// its points that lies clear of the origin, so the frame turns with the shape.
        /// </summary>
        public static PlaneFrame Of(IReadOnlyList<GeoPoint3> points, GeoVector3 unitNormal)
        {
            GeoPoint3 origin = points[0];

            for (int i = 1; i < points.Count; i++)
            {
                GeoVector3 along = origin.GetVectorTo(points[i]);
                along = along.Subtract(unitNormal.Multiply(along.DotProduct(unitNormal)));
                double length = along.Length;

                if (length > 1e-9 * Math.Max(1.0, Math.Abs(origin.X) + Math.Abs(origin.Y) + Math.Abs(origin.Z)))
                {
                    return new PlaneFrame(origin, unitNormal, along.Divide(length));
                }
            }

            return new PlaneFrame(origin, unitNormal, unitNormal.GetPerpendicularVector());
        }

        public GeoPoint2 ToLocal(GeoPoint3 point)
        {
            GeoVector3 offset = Origin.GetVectorTo(point);
            return new GeoPoint2(offset.DotProduct(U), offset.DotProduct(V));
        }

        public List<GeoPoint2> ToLocal(IReadOnlyList<GeoPoint3> points)
        {
            List<GeoPoint2> local = new List<GeoPoint2>(points.Count);

            foreach (GeoPoint3 point in points)
            {
                local.Add(ToLocal(point));
            }

            return local;
        }

        public GeoPoint3 ToWorld(GeoPoint2 point) => Origin.Add(U.Multiply(point.X)).Add(V.Multiply(point.Y));

        public GeoPoint3[] ToWorld(IReadOnlyList<GeoPoint2> points)
        {
            GeoPoint3[] world = new GeoPoint3[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                world[i] = ToWorld(points[i]);
            }

            return world;
        }
    }

    /// <summary>
    /// The planar region and offset algorithms the solid library shares with the plane library, applied in the
    /// frame of a flat shape. Regions are resolved by the shared winding-number solver.
    /// </summary>
    internal static class PlanarRegion3
    {
        // Tags that follow the edges of a polyline's offset band through the region solver.
        private const int ChainFlag = 1;
        private const int CapFlag = 2;
        private const int OffsetFlag = 4;
        private const int ThroughCornerFlag = 8;

        /// <summary>
        /// The distance within which the region solver takes two points to be one: a hundredth of the point
        /// tolerance, but never below what rounding leaves on coordinates the size of the shape.
        /// </summary>
        public static double GetSnap(Tolerance tolerance, double extent)
        {
            return Math.Max(tolerance.EqualPoint * 1e-2, Math.Max(extent, 1.0) * 1e-12);
        }

        /// <summary>
        /// The largest absolute coordinate of a set of loops.
        /// </summary>
        public static double Extent(IEnumerable<IReadOnlyList<GeoPoint2>> loops)
        {
            double extent = 0.0;

            foreach (IReadOnlyList<GeoPoint2> loop in loops)
            {
                foreach (GeoPoint2 point in loop)
                {
                    extent = Math.Max(extent, Math.Max(Math.Abs(point.X), Math.Abs(point.Y)));
                }
            }

            return extent;
        }

        /// <summary>
        /// Resolves loops into the clean loops of the region they enclose under a fill rule, with the region on
        /// their left.
        /// </summary>
        public static List<List<GeoPoint2>> Resolve(IEnumerable<IReadOnlyList<GeoPoint2>> loops, FillRule rule, Tolerance tolerance, double snap)
        {
            WindingRegion region = new WindingRegion();

            foreach (IReadOnlyList<GeoPoint2> loop in loops)
            {
                region.AddLoop(loop, 0);
            }

            return CleanLoops(region.Resolve(rule, snap), tolerance, snap);
        }

        /// <summary>
        /// Offsets a region given by clean loops with the region on their left, and groups the result.
        /// </summary>
        public static List<LoopGroup> OffsetRegion(List<List<GeoPoint2>> region, double distance, OffsetOptions options, Tolerance tolerance, double snap)
        {
            if (region.Count == 0)
            {
                return new List<LoopGroup>();
            }

            CornerStyle style = new CornerStyle(options.Join, options.MiterLimit, options.GetArcTolerance(distance));
            List<List<GeoPoint2>> raw = new List<List<GeoPoint2>>(region.Count);

            foreach (List<GeoPoint2> loop in region)
            {
                raw.Add(OffsetOutline.BuildClosedLoop(loop, distance, style));
            }

            // Sharp corners can reach far out, so the snap distance follows the raw loops themselves.
            double rawSnap = Math.Max(snap, GetSnap(tolerance, Extent(raw)));
            return LoopTools.Group(Resolve(raw, FillRule.Positive, tolerance, rawSnap));
        }

        /// <summary>
        /// Offsets an open chain to the left by a distance (to the right when negative) and returns the pieces of
        /// the parallel curve that survive, running the same way as the chain.
        /// </summary>
        public static List<List<GeoPoint2>> OffsetChain(List<GeoPoint2> chain, double distance, OffsetOptions options, double snap)
        {
            bool toLeft = distance > 0.0;
            double width = Math.Abs(distance);
            List<GeoPoint2> path = new List<GeoPoint2>(chain);

            // Offsetting to the right is offsetting the reversed chain to its left.
            if (!toLeft)
            {
                path.Reverse();
            }

            CornerStyle style = new CornerStyle(options.Join, options.MiterLimit, options.GetArcTolerance(width));

            // The shared builder offsets to the right for a positive distance, so the left takes a negative one.
            List<GeoPoint2> raw = OffsetOutline.BuildOpenChain(path, -width, style, out List<bool> throughCorner);

            // The band the chain sweeps to its left, traced with the band on the left of every edge: along the
            // chain, out along the end, back along the raw offset, and in along the start. Each edge carries a
            // tag saying where it came from, and the solver keeps the tags on the pieces it cuts.
            WindingRegion band = new WindingRegion();

            for (int i = 0; i + 1 < path.Count; i++)
            {
                band.AddEdge(path[i], path[i + 1], ChainFlag);
            }

            band.AddEdge(path[path.Count - 1], raw[raw.Count - 1], CapFlag);

            for (int i = raw.Count - 1; i > 0; i--)
            {
                band.AddEdge(raw[i], raw[i - 1], throughCorner[i - 1] ? ThroughCornerFlag : OffsetFlag);
            }

            band.AddEdge(raw[0], path[0], CapFlag);

            List<List<GeoPoint2>> runs = new List<List<GeoPoint2>>();

            foreach (RegionLoop loop in band.Resolve(FillRule.Positive, snap))
            {
                CollectRuns(loop, runs);
            }

            // The band lies on the left of its edge, so the surviving pieces run against the path: with the chain
            // for a right offset and against it for a left one.
            if (toLeft)
            {
                foreach (List<GeoPoint2> run in runs)
                {
                    run.Reverse();
                }
            }

            List<GeoPoint2> forward = new List<GeoPoint2>(raw);

            if (!toLeft)
            {
                forward.Reverse();
            }

            runs.Sort((a, b) => PositionAlong(forward, a[0]).CompareTo(PositionAlong(forward, b[0])));
            return runs;
        }

        /// <summary>
        /// Drops the extra points splitting leaves along straight edges and the points closer than the point
        /// tolerance, and the loops thinner than it.
        /// </summary>
        private static List<List<GeoPoint2>> CleanLoops(List<RegionLoop> loops, Tolerance tolerance, double snap)
        {
            List<List<GeoPoint2>> kept = new List<List<GeoPoint2>>(loops.Count);

            foreach (RegionLoop loop in loops)
            {
                List<GeoPoint2> cleaned = LoopTools.Clean(loop.Points, tolerance.EqualPoint, snap);

                // Area over perimeter is half the width of a thin loop: one narrower than the point tolerance is
                // a seam left where two edges almost met, not a shape.
                if (cleaned != null && Math.Abs(LoopTools.SignedArea(cleaned)) > 0.5 * tolerance.EqualPoint * LoopTools.Perimeter(cleaned))
                {
                    kept.Add(cleaned);
                }
            }

            return kept;
        }

        /// <summary>
        /// Collects the stretches of a band loop that come from the raw offset itself, rather than from the
        /// chain, its ends, or the detours through inside corners.
        /// </summary>
        private static void CollectRuns(RegionLoop loop, List<List<GeoPoint2>> runs)
        {
            int count = loop.Points.Length;

            bool IsOffset(int edge) => (loop.Flags[edge] & OffsetFlag) != 0 && (loop.Flags[edge] & ThroughCornerFlag) == 0;

            int start = -1;

            for (int k = 0; k < count; k++)
            {
                if (!IsOffset(k))
                {
                    start = k;
                    break;
                }
            }

            if (start < 0)
            {
                // The whole loop is offset curve: a closed chain offset to where nothing else remains.
                List<GeoPoint2> closed = new List<GeoPoint2>(loop.Points) { loop.Points[0] };
                runs.Add(closed);
                return;
            }

            List<GeoPoint2> current = null;

            for (int step = 1; step <= count; step++)
            {
                int edge = (start + step) % count;

                if (IsOffset(edge))
                {
                    if (current == null)
                    {
                        current = new List<GeoPoint2> { loop.Points[edge] };
                    }

                    current.Add(loop.Points[(edge + 1) % count]);
                }
                else if (current != null)
                {
                    runs.Add(current);
                    current = null;
                }
            }

            if (current != null)
            {
                runs.Add(current);
            }
        }

        /// <summary>
        /// The arc length along a chain to the point on it nearest a given point.
        /// </summary>
        private static double PositionAlong(List<GeoPoint2> chain, GeoPoint2 point)
        {
            double best = double.MaxValue;
            double position = 0.0;
            double travelled = 0.0;

            for (int i = 0; i + 1 < chain.Count; i++)
            {
                GeoPoint2 a = chain[i];
                GeoVector2 direction = chain[i + 1] - a;
                double length = direction.Length;
                double along = length > 0.0 ? Math.Max(0.0, Math.Min(length, (point - a).Dot(direction) / length)) : 0.0;
                GeoPoint2 nearest = length > 0.0 ? a + direction * (along / length) : a;
                double distance = nearest.DistanceSquaredTo(point);

                if (distance < best)
                {
                    best = distance;
                    position = travelled + along;
                }

                travelled += length;
            }

            return position;
        }
    }
}
