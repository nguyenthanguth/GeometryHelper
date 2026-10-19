using System;
using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The kinds of point the cursor snaps to.
    /// </summary>
    [Flags]
    public enum SnapKinds
    {
        /// <summary>
        /// No snap.
        /// </summary>
        None = 0,

        /// <summary>
        /// The ends of segments and arcs, the vertices of chains and loops, and points.
        /// </summary>
        End = 1,

        /// <summary>
        /// The middles of segments and arcs.
        /// </summary>
        Mid = 2,

        /// <summary>
        /// The crossings of two drawn segments or arcs.
        /// </summary>
        Intersection = 4,

        /// <summary>
        /// The centres of arcs, circles and ellipses.
        /// </summary>
        Centre = 8,

        /// <summary>
        /// The point of a curve nearest the cursor, taken only where none of the others is in reach.
        /// </summary>
        Nearest = 16,

        /// <summary>
        /// All of them.
        /// </summary>
        All = End | Mid | Intersection | Centre | Nearest,
    }

    /// <summary>
    /// A point the cursor snapped to.
    /// </summary>
    public sealed class SnapHit
    {
        internal SnapHit(Vec3 point, SnapKinds kind, string name, int itemIndex, int otherItemIndex, SceneCurve curve, double pixelDistance)
        {
            Point = point;
            Kind = kind;
            Name = name;
            ItemIndex = itemIndex;
            OtherItemIndex = otherItemIndex;
            Curve = curve;
            PixelDistance = pixelDistance;
        }

        /// <summary>
        /// Gets the point, in double precision world coordinates, exact for an end, a middle, a centre or a crossing.
        /// </summary>
        public Vec3 Point { get; }

        /// <summary>
        /// Gets the kind of snap.
        /// </summary>
        public SnapKinds Kind { get; }

        /// <summary>
        /// Gets the name the status bar shows: end, point, mid, intersection, centre or nearest.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the index of the item the point is on; for a crossing, the item of the first curve.
        /// </summary>
        public int ItemIndex { get; }

        /// <summary>
        /// Gets the index of the item of the second curve of a crossing, or -1.
        /// </summary>
        public int OtherItemIndex { get; }

        /// <summary>
        /// Gets the curve the point is on, or null for a point item and a crossing.
        /// </summary>
        public SceneCurve Curve { get; }

        /// <summary>
        /// Gets how far the point is from the cursor on the screen, in pixels.
        /// </summary>
        public double PixelDistance { get; }
    }

    /// <summary>
    /// Finds the snap point under the cursor: the nearest, on the screen, of the ends, middles, centres and crossings of the drawn
    /// curves, and where there is none, the nearest point of a curve. All of it is computed from the exact curves in double precision.
    /// </summary>
    public static class SnapEngine
    {
        /// <summary>
        /// How far from the cursor a snap point may be, in pixels: twelve.
        /// </summary>
        public const double DefaultRadius = 12.0;

        /// <summary>
        /// The most curves near the cursor whose crossings are looked for: twenty-four, the nearest to the cursor.
        /// </summary>
        public const int MaxCrossingCurves = 24;

        /// <summary>
        /// Finds the snap point nearest the cursor.
        /// </summary>
        /// <param name="geometry">The curves and points of the scene.</param>
        /// <param name="map">The view.</param>
        /// <param name="x">The cursor across the picture, in pixels.</param>
        /// <param name="y">The cursor down the picture, in pixels.</param>
        /// <param name="enabled">The kinds of snap on.</param>
        /// <param name="radiusPixels">How far from the cursor a snap point may be, in pixels.</param>
        /// <param name="tolerance">How close two curves have to come to cross, in millimetres; the point tolerance of the scene.</param>
        /// <param name="visible">Tells whether an item, by its index, is shown; null for all.</param>
        /// <returns>
        /// The nearest end, middle, centre or crossing within the radius, the earlier kind of those winning a tie, an end before a
        /// crossing before a middle before a centre; failing those the nearest point of a curve when <see cref="SnapKinds.Nearest"/> is on;
        /// null when there is none.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the geometry or the map is null.</exception>
        public static SnapHit Find(ExactGeometry geometry, ScreenMap map, double x, double y, SnapKinds enabled, double radiusPixels, double tolerance, Func<int, bool> visible)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (enabled == SnapKinds.None)
            {
                return null;
            }

            map.GetRay(x, y, out Vec3 origin, out Vec3 direction);
            bool forward = !map.Orthographic;
            SnapHit best = null;
            SnapHit nearest = null;
            var near = new List<Near>();

            void Offer(Vec3 point, SnapKinds kind, string name, SceneCurve curve, int item, int other)
            {
                if (!map.TryProject(point, out double px, out double py, out _))
                {
                    return;
                }

                double pixels = Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));

                if (pixels > radiusPixels)
                {
                    return;
                }

                var hit = new SnapHit(point, kind, name, item, other, curve, pixels);

                if (best == null || Better(hit, best))
                {
                    best = hit;
                }
            }

            if ((enabled & SnapKinds.End) != 0)
            {
                foreach (ScenePoint p in geometry.Points)
                {
                    if (visible == null || visible(p.ItemIndex))
                    {
                        Offer(p.At, SnapKinds.End, "point", null, p.ItemIndex, -1);
                    }
                }
            }

            for (int i = 0; i < geometry.Curves.Count; i++)
            {
                SceneCurve curve = geometry.Curves[i];

                if (visible != null && !visible(curve.ItemIndex))
                {
                    continue;
                }

                // The centre of an arc is off the arc, outside the sphere that holds it, so it is offered before the sphere is tried.
                if ((enabled & SnapKinds.Centre) != 0 && curve.Shape == CurveShape.Conic)
                {
                    Offer(curve.Center, SnapKinds.Centre, "centre", curve, curve.ItemIndex, -1);
                }

                curve.BoundingSphere(out Vec3 center, out double radius);
                Vec3 w = center - origin;
                double along = Vec3.Dot(w, direction);

                if (forward && along + radius < 0.0)
                {
                    continue;
                }

                double lateral = (w - direction * along).Length;
                double reach = radiusPixels * map.PixelSizeAt(center + map.Forward * radius);

                if (lateral > radius + reach)
                {
                    continue;
                }

                if ((enabled & SnapKinds.End) != 0 && curve.HasEnds)
                {
                    Offer(curve.Start, SnapKinds.End, "end", curve, curve.ItemIndex, -1);
                    Offer(curve.End, SnapKinds.End, "end", curve, curve.ItemIndex, -1);
                }

                if ((enabled & SnapKinds.Mid) != 0 && curve.Middle.HasValue)
                {
                    Offer(curve.Middle.Value, SnapKinds.Mid, "mid", curve, curve.ItemIndex, -1);
                }

                if ((enabled & (SnapKinds.Nearest | SnapKinds.Intersection)) != 0)
                {
                    double gap = curve.DistanceToLine(origin, direction, forward, out Vec3 onCurve);
                    double pixels = double.IsInfinity(gap) || !map.IsInDepth(onCurve) ? double.PositiveInfinity : gap / map.PixelSizeAt(onCurve);

                    if (!(pixels <= radiusPixels))
                    {
                        // The point nearest the ray in space is out of the picture or too far from the cursor on the screen: the part of the curve that shows is looked at.
                        pixels = curve.VisibleGapPixels(map, x, y, out onCurve);
                    }

                    if (pixels <= radiusPixels)
                    {
                        near.Add(new Near(curve, i, pixels, onCurve));
                    }
                }
            }

            if ((enabled & SnapKinds.Intersection) != 0 && near.Count > 1)
            {
                near.Sort((a, b) =>
                {
                    int c = a.Pixels.CompareTo(b.Pixels);
                    return c != 0 ? c : a.Index.CompareTo(b.Index);
                });

                int count = Math.Min(near.Count, MaxCrossingCurves);

                for (int a = 0; a < count; a++)
                {
                    for (int b = a + 1; b < count; b++)
                    {
                        SceneCurve first = near[a].Curve;
                        SceneCurve second = near[b].Curve;

                        if (!CurveIntersections.IsSupported(first, second))
                        {
                            continue;
                        }

                        foreach (Vec3 p in CurveIntersections.Find(first, second, tolerance))
                        {
                            // A crossing at the end of a curve is that end, and is offered as one while ends are snapped to.
                            if ((enabled & SnapKinds.End) != 0 && (AtAnEnd(first, p, tolerance) || AtAnEnd(second, p, tolerance)))
                            {
                                continue;
                            }

                            Offer(p, SnapKinds.Intersection, "intersection", null, first.ItemIndex, second.ItemIndex);
                        }
                    }
                }
            }

            if (best != null || (enabled & SnapKinds.Nearest) == 0)
            {
                return best;
            }

            foreach (Near n in near)
            {
                var hit = new SnapHit(n.Point, SnapKinds.Nearest, "nearest", n.Curve.ItemIndex, -1, n.Curve, n.Pixels);

                if (nearest == null || n.Pixels < nearest.PixelDistance - 1E-9 || (Math.Abs(n.Pixels - nearest.PixelDistance) <= 1E-9 && Compare(hit, nearest) < 0))
                {
                    nearest = hit;
                }
            }

            return nearest;
        }

        private static bool AtAnEnd(SceneCurve curve, Vec3 point, double tolerance)
            => curve.HasEnds && ((curve.Start - point).Length <= tolerance || (curve.End - point).Length <= tolerance);

        private static bool Better(SnapHit hit, SnapHit than)
        {
            if (hit.PixelDistance < than.PixelDistance - 1E-9)
            {
                return true;
            }

            return hit.PixelDistance <= than.PixelDistance + 1E-9 && Compare(hit, than) < 0;
        }

        /// <summary>
        /// Orders two snaps that are as near the cursor as each other: by kind, then item, then place, so that the choice never rests on the order the curves are held in.
        /// </summary>
        private static int Compare(SnapHit a, SnapHit b)
        {
            int c = Rank(a.Kind).CompareTo(Rank(b.Kind));

            if (c != 0)
            {
                return c;
            }

            c = a.ItemIndex.CompareTo(b.ItemIndex);

            if (c != 0)
            {
                return c;
            }

            c = a.Point.X.CompareTo(b.Point.X);
            return c != 0 ? c : a.Point.Y != b.Point.Y ? a.Point.Y.CompareTo(b.Point.Y) : a.Point.Z.CompareTo(b.Point.Z);
        }

        private static int Rank(SnapKinds kind)
        {
            switch (kind)
            {
                case SnapKinds.End: return 0;
                case SnapKinds.Intersection: return 1;
                case SnapKinds.Mid: return 2;
                case SnapKinds.Centre: return 3;
                default: return 4;
            }
        }

        private readonly struct Near
        {
            internal Near(SceneCurve curve, int index, double pixels, Vec3 point)
            {
                Curve = curve;
                Index = index;
                Pixels = pixels;
                Point = point;
            }

            internal SceneCurve Curve { get; }

            internal int Index { get; }

            internal double Pixels { get; }

            internal Vec3 Point { get; }
        }
    }
}
