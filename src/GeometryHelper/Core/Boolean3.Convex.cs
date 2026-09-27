using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The region two convex bodies share, found by clipping one by the faces of the other.
    /// </summary>
    /// <remarks>
    /// Two convex bodies share at most one region, and it is convex: what is left of the first once everything
    /// in front of each face of the second is cut away. Clipping each face against a plane, and capping the cut,
    /// costs a pass over the corners per face of the second body, where the general boolean cuts the first into
    /// cells by every plane of both and sorts the cells. A round bar crossing another is the common case in a
    /// model: a reinforcing cage, a mesh, bolts through plates' clearance.
    /// </remarks>
    public static partial class Boolean3
    {
        /// <summary>
        /// Checks whether a body is convex: no openings, no face with a hole, every face a convex outline, and
        /// every corner of the body behind the plane of every face.
        /// </summary>
        /// <remarks>
        /// A body whose faces point inwards is not called convex, and nor is one too large to check cheaply:
        /// either way it is left to the general boolean, which is always right.
        /// </remarks>
        internal static bool IsConvex(GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid.Openings.Count > 0)
            {
                return false;
            }

            var corners = new List<GeoPoint3>();

            foreach (GeoFace3 face in solid.Faces)
            {
                if (face.Holes.Count > 0 || !IsConvexOutline(face.Boundary, tolerance))
                {
                    return false;
                }

                corners.AddRange(face.Boundary.Vertices);
            }

            if ((long)solid.Faces.Count * corners.Count > 1000000L)
            {
                return false;
            }

            foreach (GeoFace3 face in solid.Faces)
            {
                GeoPlane3 plane = face.GetPlane();

                foreach (GeoPoint3 corner in corners)
                {
                    if (plane.SignedDistanceTo(corner) > tolerance.EqualPlanar)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether an outline turns the same way at every corner, about its own normal.
        /// </summary>
        private static bool IsConvexOutline(GeoPolygon3 outline, Tolerance tolerance)
        {
            IReadOnlyList<GeoPoint3> ring = outline.Vertices;
            GeoVector3 normal = outline.Normal;
            int count = ring.Count;

            for (int i = 0; i < count; i++)
            {
                GeoVector3 into = ring[i].GetVectorTo(ring[(i + 1) % count]);
                GeoVector3 outOf = ring[(i + 1) % count].GetVectorTo(ring[(i + 2) % count]);

                // A turn the wrong way by more than the point tolerance over the two edges.
                if (into.CrossProduct(outOf).DotProduct(normal) < -tolerance.EqualPoint * (into.Length + outOf.Length))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the region two convex bodies share, when it is plainly a region.
        /// </summary>
        /// <param name="first">A convex body, as <see cref="IsConvex"/> says.</param>
        /// <param name="second">Another.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="shared">The region, as the one piece of it; null when the method returns false.</param>
        /// <returns>
        /// false when the clipping cannot settle it on its own: the bodies only touch, or overlap by a sliver near
        /// the tolerance, or the clipped outline is not a clean body. The caller then asks the general boolean,
        /// which decides such cases as it always has; so this answers only where the two must agree.
        /// </returns>
        internal static bool TryIntersectConvex(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance, out GeoSolid3[] shared)
        {
            shared = null;

            double on = tolerance.EqualPlanar;
            var rings = new List<List<GeoPoint3>>();

            foreach (GeoFace3 face in first.Faces)
            {
                rings.Add(new List<GeoPoint3>(face.Boundary.Vertices));
            }

            foreach (GeoFace3 cutter in second.Faces)
            {
                GeoPlane3 plane = cutter.GetPlane();
                double nearest = double.MaxValue, farthest = double.MinValue;

                foreach (List<GeoPoint3> ring in rings)
                {
                    foreach (GeoPoint3 point in ring)
                    {
                        double distance = plane.SignedDistanceTo(point);
                        nearest = Math.Min(nearest, distance);
                        farthest = Math.Max(farthest, distance);
                    }
                }

                if (farthest <= on)
                {
                    // Wholly behind this face: it cuts nothing.
                    continue;
                }

                if (nearest >= -on)
                {
                    // Nothing left behind this face beyond the tolerance: they touch at most.
                    return false;
                }

                var clipped = new List<List<GeoPoint3>>(rings.Count + 1);
                var cap = new List<GeoPoint3>();

                foreach (List<GeoPoint3> ring in rings)
                {
                    List<GeoPoint3> kept = Clip(ring, plane, on, cap);

                    if (kept.Count >= 3)
                    {
                        clipped.Add(kept);
                    }
                }

                List<GeoPoint3> lid = Cap(cap, plane, tolerance);

                if (lid.Count >= 3)
                {
                    clipped.Add(lid);
                }

                rings = clipped;
            }

            var faces = new List<GeoFace3>(rings.Count);

            foreach (List<GeoPoint3> ring in rings)
            {
                try
                {
                    faces.Add(new GeoFace3(new GeoPolygon3(ring, tolerance)));
                }
                catch (ArgumentException)
                {
                    // A face the clipping pared down past what a polygon accepts. With no area at all it leaves
                    // nothing open; with a little — a polygon refuses less than the vector tolerance as an area —
                    // it leaves a gap, which the check for a closed body below catches.
                }
            }

            if (faces.Count < 4)
            {
                return false;
            }

            var region = new GeoSolid3(faces);

            // Only a clean body is an answer: a face lost to the polygon's own tolerance leaves the surface open and
            // the volume short, and then the general boolean decides.
            if (!region.IsClosed(tolerance))
            {
                return false;
            }
            double volume = region.Volume;
            double area = 0.0;

            foreach (GeoFace3 face in faces)
            {
                area += face.Area;
            }

            // Plainly a region: as thick, on the whole, as ten times the larger tolerance and more. A thinner one
            // is where the general boolean's own tolerance decides whether there is a region at all.
            if (!(volume > 0.0) || volume / area <= 10.0 * Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar))
            {
                return false;
            }

            shared = new[] { region };
            return true;
        }

        /// <summary>
        /// Keeps the part of a convex outline behind a plane, adding to a list, when one is given, the points of it that
        /// lie on the plane.
        /// </summary>
        /// <remarks>
        /// A corner within the tolerance of the plane counts as on it, and is kept; an edge running from well behind
        /// to well in front is cut where it crosses. So a corner the plane grazes is neither lost nor doubled.
        /// </remarks>
        internal static List<GeoPoint3> Clip(IReadOnlyList<GeoPoint3> ring, GeoPlane3 plane, double on, List<GeoPoint3> cap)
        {
            int count = ring.Count;
            var distances = new double[count];

            for (int i = 0; i < count; i++)
            {
                distances[i] = plane.SignedDistanceTo(ring[i]);
            }

            var kept = new List<GeoPoint3>(count + 1);

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                double here = distances[i], next = distances[j];

                if (here <= on)
                {
                    kept.Add(ring[i]);

                    if (here >= -on && cap != null)
                    {
                        cap.Add(ring[i]);
                    }
                }

                if ((here < -on && next > on) || (here > on && next < -on))
                {
                    double t = here / (here - next);
                    GeoPoint3 crossing = new GeoPoint3(
                        ring[i].X + (ring[j].X - ring[i].X) * t,
                        ring[i].Y + (ring[j].Y - ring[i].Y) * t,
                        ring[i].Z + (ring[j].Z - ring[i].Z) * t);

                    kept.Add(crossing);
                    cap?.Add(crossing);
                }
            }

            return kept;
        }

        /// <summary>
        /// Orders the points a cut left on a plane into the outline of the cut, turning about the plane's normal.
        /// </summary>
        private static List<GeoPoint3> Cap(List<GeoPoint3> points, GeoPlane3 plane, Tolerance tolerance)
        {
            var outline = new List<GeoPoint3>();

            if (points.Count < 3)
            {
                return outline;
            }

            double x = 0.0, y = 0.0, z = 0.0;

            foreach (GeoPoint3 point in points)
            {
                x += point.X;
                y += point.Y;
                z += point.Z;
            }

            var centre = new GeoPoint3(x / points.Count, y / points.Count, z / points.Count);
            GeoVector3 normal = plane.Normal;
            GeoVector3 u = Math.Abs(normal.X) < 0.9 ? GeoVector3.XAxis.CrossProduct(normal) : GeoVector3.YAxis.CrossProduct(normal);
            u = u.Normalize();
            GeoVector3 v = normal.CrossProduct(u);

            var angles = new double[points.Count];
            var ordered = points.ToArray();

            for (int i = 0; i < ordered.Length; i++)
            {
                GeoVector3 offset = centre.GetVectorTo(ordered[i]);
                angles[i] = Math.Atan2(offset.DotProduct(v), offset.DotProduct(u));
            }

            Array.Sort(angles, ordered);

            foreach (GeoPoint3 point in ordered)
            {
                if (outline.Count == 0 || !outline[outline.Count - 1].IsEqualTo(point, tolerance))
                {
                    outline.Add(point);
                }
            }

            while (outline.Count > 1 && outline[outline.Count - 1].IsEqualTo(outline[0], tolerance))
            {
                outline.RemoveAt(outline.Count - 1);
            }

            return outline;
        }
    }
}
