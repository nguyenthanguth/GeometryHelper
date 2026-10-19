using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What a pick ray met.
    /// </summary>
    public enum PickKind
    {
        /// <summary>
        /// A triangle of a surface.
        /// </summary>
        Surface,

        /// <summary>
        /// A line: a segment, or an arc, circle or ellipse, found on the exact curve.
        /// </summary>
        Curve,

        /// <summary>
        /// A point or the anchor of a label.
        /// </summary>
        Point,
    }

    /// <summary>
    /// The item under the cursor and where.
    /// </summary>
    public sealed class PickHit
    {
        internal PickHit(int itemIndex, PickKind kind, Vec3 point, double depth)
        {
            ItemIndex = itemIndex;
            Kind = kind;
            Point = point;
            Depth = depth;
        }

        /// <summary>
        /// Gets the index of the item in <see cref="Scene.Items"/>.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets what was hit.
        /// </summary>
        public PickKind Kind { get; }

        /// <summary>
        /// Gets the place hit: on the surface, on the curve, or the point.
        /// </summary>
        public Vec3 Point { get; }

        /// <summary>
        /// Gets how far the place is from the eye along the view direction.
        /// </summary>
        public double Depth { get; }
    }

    /// <summary>
    /// A place on the screen taken to a point of the scene, and how.
    /// </summary>
    public sealed class ResolvedPoint
    {
        internal ResolvedPoint(Vec3 point, string source, SnapHit snap, int itemIndex)
        {
            Point = point;
            Source = source;
            Snap = snap;
            ItemIndex = itemIndex;
        }

        /// <summary>
        /// Gets the point, in double precision.
        /// </summary>
        public Vec3 Point { get; }

        /// <summary>
        /// Gets how it was found: the name of the snap (end, point, mid, intersection, centre, nearest), "surface", "line", or the plane
        /// it was taken on, "plane z = 0" or "view plane".
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the snap the point is, or null.
        /// </summary>
        public SnapHit Snap { get; }

        /// <summary>
        /// Gets the index of the item the point is on, or -1 for a point on a plane.
        /// </summary>
        public int ItemIndex { get; }
    }

    /// <summary>
    /// Finds what is under the cursor from the exact scene: the ray through the cursor against the triangles of the surfaces, and
    /// against the exact curves and points within a few pixels of it. The result does not depend on what the graphics card was given.
    /// </summary>
    public static class PickEngine
    {
        /// <summary>
        /// How far from the cursor a line may be and still be picked, in pixels: five.
        /// </summary>
        public const double CurveRadius = 5.0;

        /// <summary>
        /// How far from the cursor a point may be and still be picked, in pixels: seven.
        /// </summary>
        public const double PointRadius = 7.0;

        /// <summary>
        /// How far from the cursor an arc, circle or ellipse may be and still be taken to measure its radius, in pixels: eight.
        /// </summary>
        public const double ConicRadius = 8.0;

        /// <summary>
        /// How much nearer than a surface a line or a point counts, in pixels, so that an edge on a face is picked rather than the face.
        /// </summary>
        private const double LineBiasPixels = 4.0;

        /// <summary>
        /// Finds the item nearest the camera under the cursor.
        /// </summary>
        /// <param name="primitives">What the scene is drawn with: the triangles, points and labels.</param>
        /// <param name="geometry">The exact curves; null picks surfaces and points only.</param>
        /// <param name="map">The view.</param>
        /// <param name="x">The cursor across the picture, in pixels.</param>
        /// <param name="y">The cursor down the picture, in pixels.</param>
        /// <param name="visible">Tells whether an item, by its index, is shown; null for all.</param>
        /// <param name="surfaces">Whether surfaces are shown; in wireframe they are not and cannot be picked.</param>
        /// <returns>The hit, or null where there is nothing under the cursor.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives or the map is null.</exception>
        public static PickHit Pick(Primitives primitives, ExactGeometry geometry, ScreenMap map, double x, double y, Func<int, bool> visible, bool surfaces)
        {
            if (primitives == null)
            {
                throw new ArgumentNullException(nameof(primitives));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            map.GetRay(x, y, out Vec3 origin, out Vec3 direction);
            bool forward = !map.Orthographic;
            PickHit best = null;
            double bestDepth = double.PositiveInfinity;

            void Offer(int item, PickKind kind, Vec3 point, double depth, double rank)
            {
                if (rank < bestDepth || (rank == bestDepth && best != null && item < best.ItemIndex))
                {
                    best = new PickHit(item, kind, point, depth);
                    bestDepth = rank;
                }
            }

            if (surfaces)
            {
                for (int i = 0; i < primitives.Items.Count; i++)
                {
                    ItemPrimitives p = primitives.Items[i];

                    if (p.Triangles.Count == 0 || (visible != null && !visible(i)))
                    {
                        continue;
                    }

                    if (p.Triangles.Count > 64 && !p.Bounds.IsEmpty && !RayMeetsBox(origin, direction, forward, p.Bounds))
                    {
                        continue;
                    }

                    foreach (DrawTriangle t in p.Triangles)
                    {
                        if (RayMeetsTriangle(origin, direction, t, out double along))
                        {
                            Vec3 at = origin + direction * along;
                            double depth = map.DepthOf(at);

                            if (map.IsInDepth(at))
                            {
                                Offer(i, PickKind.Surface, at, depth, depth);
                            }
                        }
                    }
                }
            }

            if (geometry != null)
            {
                foreach (SceneCurve curve in geometry.Curves)
                {
                    if (visible != null && !visible(curve.ItemIndex))
                    {
                        continue;
                    }

                    curve.BoundingSphere(out Vec3 center, out double radius);
                    Vec3 w = center - origin;
                    double along = Vec3.Dot(w, direction);

                    if (forward && along + radius < 0.0)
                    {
                        continue;
                    }

                    if ((w - direction * along).Length > radius + CurveRadius * map.PixelSizeAt(center + map.Forward * radius))
                    {
                        continue;
                    }

                    double gap = curve.DistanceToLine(origin, direction, forward, out Vec3 onCurve);

                    if (!double.IsInfinity(gap) && map.IsInDepth(onCurve) && gap <= CurveRadius * map.PixelSizeAt(onCurve))
                    {
                        double depth = map.DepthOf(onCurve);
                        Offer(curve.ItemIndex, PickKind.Curve, onCurve, depth, depth - LineBiasPixels * map.PixelSizeAt(onCurve));
                    }
                }
            }

            for (int i = 0; i < primitives.Items.Count; i++)
            {
                ItemPrimitives p = primitives.Items[i];

                if (visible != null && !visible(i))
                {
                    continue;
                }

                foreach (Vec3 point in p.Points)
                {
                    OfferPoint(i, point);
                }

                foreach (DrawLabel label in p.Labels)
                {
                    OfferPoint(i, label.At);
                }
            }

            void OfferPoint(int item, Vec3 point)
            {
                if (!map.TryProject(point, out double px, out double py, out double depth))
                {
                    return;
                }

                if ((px - x) * (px - x) + (py - y) * (py - y) <= PointRadius * PointRadius)
                {
                    Offer(item, PickKind.Point, point, depth, depth - LineBiasPixels * map.PixelSizeAt(point));
                }
            }

            return best;
        }

        /// <summary>
        /// Finds the arc, circle or ellipse nearest the cursor, for measuring a radius; the segments beside it do not count.
        /// </summary>
        /// <param name="geometry">The exact curves.</param>
        /// <param name="map">The view.</param>
        /// <param name="x">The cursor across the picture, in pixels.</param>
        /// <param name="y">The cursor down the picture, in pixels.</param>
        /// <param name="visible">Tells whether an item, by its index, is shown; null for all.</param>
        /// <param name="onCurve">The point of the curve nearest the cursor.</param>
        /// <returns>The curve within <see cref="ConicRadius"/> pixels of the cursor that is nearest it, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the geometry or the map is null.</exception>
        public static SceneCurve PickConic(ExactGeometry geometry, ScreenMap map, double x, double y, Func<int, bool> visible, out Vec3 onCurve)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            map.GetRay(x, y, out Vec3 origin, out Vec3 direction);
            bool forward = !map.Orthographic;
            SceneCurve best = null;
            double bestPixels = double.PositiveInfinity;
            onCurve = default(Vec3);

            foreach (SceneCurve curve in geometry.Curves)
            {
                if (curve.Shape != CurveShape.Conic || (visible != null && !visible(curve.ItemIndex)))
                {
                    continue;
                }

                double gap = curve.DistanceToLine(origin, direction, forward, out Vec3 at);

                if (double.IsInfinity(gap) || !map.IsInDepth(at))
                {
                    continue;
                }

                double pixels = gap / map.PixelSizeAt(at);

                if (pixels <= ConicRadius && (pixels < bestPixels || (pixels == bestPixels && curve.ItemIndex < best.ItemIndex)))
                {
                    best = curve;
                    bestPixels = pixels;
                    onCurve = at;
                }
            }

            return best;
        }

        /// <summary>
        /// Takes a place on the screen to a point of the scene: the snap there when one is on, else the line, the point or the surface
        /// under the cursor, else the point of the plane z = 0 behind the cursor, else the point of the plane through the target square
        /// to the view.
        /// </summary>
        /// <param name="primitives">What the scene is drawn with.</param>
        /// <param name="geometry">The exact curves and points.</param>
        /// <param name="map">The view.</param>
        /// <param name="x">The cursor across the picture, in pixels.</param>
        /// <param name="y">The cursor down the picture, in pixels.</param>
        /// <param name="snaps">The kinds of snap on.</param>
        /// <param name="tolerance">How close two curves have to come to cross, in millimetres.</param>
        /// <param name="visible">Tells whether an item, by its index, is shown; null for all.</param>
        /// <param name="surfaces">Whether surfaces are shown.</param>
        /// <param name="groundPlane">Whether the plane z = 0 is the plane to fall back on; where it is not, the plane through the target square to the view is, which is the right one for a point to turn about.</param>
        /// <returns>The point and how it was found; never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the primitives, the geometry or the map is null.</exception>
        public static ResolvedPoint ResolvePoint(Primitives primitives, ExactGeometry geometry, ScreenMap map, double x, double y, SnapKinds snaps, double tolerance, Func<int, bool> visible, bool surfaces, bool groundPlane = true)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            SnapHit snap = SnapEngine.Find(geometry, map, x, y, snaps, SnapEngine.DefaultRadius, tolerance, visible);

            if (snap != null)
            {
                return new ResolvedPoint(snap.Point, snap.Name, snap, snap.ItemIndex);
            }

            PickHit hit = Pick(primitives, geometry, map, x, y, visible, surfaces);

            if (hit != null)
            {
                return new ResolvedPoint(hit.Point, hit.Kind == PickKind.Surface ? "surface" : hit.Kind == PickKind.Curve ? "line" : "point", null, hit.ItemIndex);
            }

            map.GetRay(x, y, out Vec3 origin, out Vec3 direction);

            if (groundPlane && Math.Abs(direction.Z) > 1E-9)
            {
                double along = -origin.Z / direction.Z;

                if (map.Orthographic || along > 0.0)
                {
                    return new ResolvedPoint(origin + direction * along, "plane z = 0", null, -1);
                }
            }

            return new ResolvedPoint(map.PointOnTargetPlane(x, y), "view plane", null, -1);
        }

        private static bool RayMeetsTriangle(Vec3 origin, Vec3 direction, DrawTriangle t, out double along)
        {
            // Moller and Trumbore, in double precision; a triangle seen edge on is missed.
            Vec3 e1 = t.B - t.A;
            Vec3 e2 = t.C - t.A;
            Vec3 p = Vec3.Cross(direction, e2);
            double det = Vec3.Dot(e1, p);
            along = 0.0;

            if (Math.Abs(det) < 1E-300)
            {
                return false;
            }

            double inv = 1.0 / det;
            Vec3 s = origin - t.A;
            double u = Vec3.Dot(s, p) * inv;

            if (u < 0.0 || u > 1.0)
            {
                return false;
            }

            Vec3 q = Vec3.Cross(s, e1);
            double v = Vec3.Dot(direction, q) * inv;

            if (v < 0.0 || u + v > 1.0)
            {
                return false;
            }

            along = Vec3.Dot(e2, q) * inv;
            return true;
        }

        private static bool RayMeetsBox(Vec3 origin, Vec3 direction, bool forward, Bounds3 box)
        {
            Vec3 size = box.Size;
            double pad = 1E-9 + 1E-9 * size.Length;
            double near = forward ? 0.0 : double.NegativeInfinity;
            double far = double.PositiveInfinity;

            for (int axis = 0; axis < 3; axis++)
            {
                double o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
                double d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
                double lo = (axis == 0 ? box.Min.X : axis == 1 ? box.Min.Y : box.Min.Z) - pad;
                double hi = (axis == 0 ? box.Max.X : axis == 1 ? box.Max.Y : box.Max.Z) + pad;

                if (Math.Abs(d) < 1E-300)
                {
                    if (o < lo || o > hi)
                    {
                        return false;
                    }

                    continue;
                }

                double t1 = (lo - o) / d;
                double t2 = (hi - o) / d;
                near = Math.Max(near, Math.Min(t1, t2));
                far = Math.Min(far, Math.Max(t1, t2));

                if (near > far)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
