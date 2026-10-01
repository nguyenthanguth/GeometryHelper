using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Breaks the material of a flat face into triangles by cutting it into strips at its corners, whatever its rings do
    /// to each other.
    /// <para>
    /// Ear clipping needs the rings of a face to stand apart, since it joins them into one loop that must not touch
    /// itself. This needs nothing of them. The region is first resolved as the booleans read it, inside the boundary and
    /// outside every hole, into loops that neither cross nor overlap: holes that touch or overlap are taken together, and
    /// one reaching past the boundary takes away only what it covers. Lines across the plane through every corner of
    /// those loops then cut the region into strips. Within a strip no edge ends and none crosses another, so the material
    /// between two edges in turn is a trapezoid, two triangles; a trapezoid running on between the same two edges into the
    /// next strip is carried on, so that a strip line ends a piece only where something changes.
    /// </para>
    /// <para>
    /// The triangles lie within the material and leave every hole open. They keep the face's own corners, but meet its
    /// edges at points of their own as well, where a strip line crosses them.
    /// </para>
    /// </summary>
    internal static class StripTriangulation
    {
        /// <summary>
        /// One end of an edge: the strip line it stands on, where it lies in the plane, and the point it is, the face's
        /// own corner when it is one.
        /// </summary>
        private struct End
        {
            public double Line;
            public double X;
            public double Y;
            public GeoPoint3 Point;
        }

        /// <summary>
        /// One edge of the resolved region that crosses strips, held left to right.
        /// </summary>
        private struct Edge
        {
            public int Id;
            public End Left;
            public End Right;

            /// <summary>
            /// How the winding number changes crossing the edge upwards: +1 where the loop runs left to right, the region
            /// lying above it, and -1 where it runs back.
            /// </summary>
            public int Winding;

            /// <summary>
            /// Gets where the edge crosses a strip line: at its own end on the line an end stands on, and on the edge itself
            /// between, so that the pieces meet the edge where it is.
            /// </summary>
            public double YAt(double line)
            {
                if (line == Left.Line)
                {
                    return Left.Y;
                }

                if (line == Right.Line)
                {
                    return Right.Y;
                }

                return Left.Y + (line - Left.X) * (Right.Y - Left.Y) / (Right.X - Left.X);
            }
        }

        /// <summary>
        /// Triangulates the material of a face, holes left open.
        /// </summary>
        /// <param name="face">The face to break up.</param>
        /// <param name="tolerance">The tolerance the region is resolved within and below which a triangle counts as none.</param>
        /// <returns>The triangles covering the material, wound to share the face's normal; none when nothing is left.</returns>
        public static GeoTriangle3[] Triangulate(GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var triangles = new List<GeoTriangle3>();

            foreach (GeoPoint3[] piece in Trapezoids(face, new GeoCoordinateSystem3(face.GetPlane()), tolerance))
            {
                Emit(triangles, piece[0], piece[1], piece[2], tolerance);
                Emit(triangles, piece[0], piece[2], piece[3], tolerance);
            }

            return triangles.ToArray();
        }

        /// <summary>
        /// Cuts the material of a face into the pieces between its edges, strip by strip across a frame laid in its plane.
        /// </summary>
        /// <param name="face">The face to cut up.</param>
        /// <param name="frame">
        /// A frame whose XY plane is the face's: the strip lines run along its Y axis, one through every corner, so the
        /// pieces have their parallel sides that way.
        /// </param>
        /// <param name="tolerance">The tolerance the region is resolved within.</param>
        /// <returns>
        /// Each piece as its four corners in turn, counter-clockwise about the frame's Z axis: along the lower edge from
        /// the strip line it starts at to the one it ends at, then back along the upper edge. Two of them stand on each
        /// other where a piece comes to a point at one end.
        /// </returns>
        public static List<GeoPoint3[]> Trapezoids(GeoFace3 face, GeoCoordinateSystem3 frame, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPoint3 first = frame.ToLocal(face.Boundary[0]);
            var origin = new GeoPoint2(first.X, first.Y);
            var corners = new Dictionary<(double, double), GeoPoint3>();
            var points = new List<GeoPoint2>();

            GeoPolygon2 Flat(GeoPolygon3 ring)
            {
                var flat = new List<GeoPoint2>(ring.VertexCount);

                foreach (GeoPoint3 corner in ring.Vertices)
                {
                    GeoPoint3 local = frame.ToLocal(corner);
                    var point = new GeoPoint2(local.X, local.Y);
                    flat.Add(point);
                    points.Add(point);

                    // Keyed as the region is laid out, from the origin, so that a corner the resolving hands back as it was
                    // given is found again and the triangles keep the face's own corners there.
                    corners[(point.X - origin.X, point.Y - origin.Y)] = corner;
                }

                return new GeoPolygon2(flat);
            }

            var holes = new List<GeoPolygon2>(face.Holes.Count);
            GeoPolygon2 boundary = Flat(face.Boundary);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                holes.Add(Flat(hole));
            }

            int precision = ClipperRegion.GetPrecision(ClipperRegion.Extent(points, origin));
            List<List<GeoPoint2>> region = ClipperRegion.RegionOf(new GeoFace2(boundary, holes), origin, precision, tolerance);
            List<List<GeoPoint2>> loops = ClipperRegion.ResolveOutline(region, Clipper.FillRule.Positive, precision);

            // A strip line through every corner, corners within rounding of each other across standing on one: a side of
            // the face across the plane, its ends a hair apart once laid out in it, would otherwise leave a strip no wider
            // than that hair, and a corner at its end that no piece reaches.
            var across = new List<double>();

            foreach (List<GeoPoint2> loop in loops)
            {
                foreach (GeoPoint2 point in loop)
                {
                    across.Add(point.X);
                }
            }

            across.Sort();

            double merge = tolerance.EqualPoint * 1E-6;
            var lines = new List<double>();
            var lineOf = new Dictionary<double, double>();

            for (int i = 0; i < across.Count; i++)
            {
                if (i == 0 || across[i] - across[i - 1] > merge)
                {
                    lines.Add(across[i]);
                }

                lineOf[across[i]] = lines[lines.Count - 1];
            }

            End EndOf(GeoPoint2 point)
            {
                GeoPoint3 at = corners.TryGetValue((point.X, point.Y), out GeoPoint3 corner)
                    ? corner
                    : frame.ToGlobal(new GeoPoint3(point.X + origin.X, point.Y + origin.Y, 0.0));

                return new End { Line = lineOf[point.X], X = point.X, Y = point.Y, Point = at };
            }

            var edges = new List<Edge>();

            foreach (List<GeoPoint2> loop in loops)
            {
                for (int i = 0; i < loop.Count; i++)
                {
                    End a = EndOf(loop[i]);
                    End b = EndOf(loop[(i + 1) % loop.Count]);

                    // An edge along a strip line bounds no strip; the line stands along it.
                    if (a.Line == b.Line)
                    {
                        continue;
                    }

                    edges.Add(a.Line < b.Line
                        ? new Edge { Id = edges.Count, Left = a, Right = b, Winding = 1 }
                        : new Edge { Id = edges.Count, Left = b, Right = a, Winding = -1 });
                }
            }

            edges.Sort((left, right) => left.Left.Line.CompareTo(right.Left.Line));

            // Where a strip line crosses an edge is taken along the edge as it runs between its ends, not on the plane. A
            // corner a hair off the plane and a point on the plane beside it, a strip apart, make a sliver standing up
            // across the strip: on a face 40 m long whose corners stood up to 1E-5 off its plane, such slivers came to
            // 0.9 mm2 the face does not have.
            GeoPoint3 Lift(Edge edge, double line)
            {
                if (line == edge.Left.Line)
                {
                    return edge.Left.Point;
                }

                if (line == edge.Right.Line)
                {
                    return edge.Right.Point;
                }

                double t = (line - edge.Left.X) / (edge.Right.X - edge.Left.X);
                GeoPoint3 a = edge.Left.Point;
                GeoPoint3 b = edge.Right.Point;

                return new GeoPoint3(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), a.Z + t * (b.Z - a.Z));
            }

            var pieces = new List<GeoPoint3[]>();
            var open = new Dictionary<(int, int), (Edge Lower, Edge Upper, double Start)>();
            var active = new List<Edge>();
            var pairs = new HashSet<(int, int)>();
            int added = 0;

            void Close((Edge Lower, Edge Upper, double Start) piece, double end)
            {
                pieces.Add(new[] { Lift(piece.Lower, piece.Start), Lift(piece.Lower, end), Lift(piece.Upper, end), Lift(piece.Upper, piece.Start) });
            }

            for (int k = 0; k + 1 < lines.Count; k++)
            {
                double left = lines[k];
                double right = lines[k + 1];

                active.RemoveAll(edge => edge.Right.Line <= left);

                while (added < edges.Count && edges[added].Left.Line <= left)
                {
                    active.Add(edges[added]);
                    added++;
                }

                // No edge ends or crosses another within the strip, so their order across its middle is their order
                // all the way along it.
                double middle = 0.5 * (left + right);
                active.Sort((lower, upper) => lower.YAt(middle).CompareTo(upper.YAt(middle)));

                pairs.Clear();
                int winding = 0;

                for (int i = 0; i + 1 < active.Count; i++)
                {
                    winding += active[i].Winding;

                    if (winding > 0)
                    {
                        (int, int) key = (active[i].Id, active[i + 1].Id);
                        pairs.Add(key);

                        if (!open.ContainsKey(key))
                        {
                            open[key] = (active[i], active[i + 1], left);
                        }
                    }
                }

                foreach ((int, int) key in new List<(int, int)>(open.Keys))
                {
                    if (!pairs.Contains(key))
                    {
                        Close(open[key], left);
                        open.Remove(key);
                    }
                }
            }

            if (lines.Count > 0)
            {
                foreach ((Edge Lower, Edge Upper, double Start) piece in open.Values)
                {
                    Close(piece, lines[lines.Count - 1]);
                }
            }

            return pieces;
        }

        /// <summary>
        /// Adds a triangle unless it has collapsed to nothing, as a piece pinched to a point at one end leaves one.
        /// </summary>
        private static void Emit(List<GeoTriangle3> triangles, GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, Tolerance tolerance)
        {
            var triangle = new GeoTriangle3(a, b, c);

            if (!triangle.IsDegenerate(tolerance))
            {
                triangles.Add(triangle);
            }
        }
    }
}
