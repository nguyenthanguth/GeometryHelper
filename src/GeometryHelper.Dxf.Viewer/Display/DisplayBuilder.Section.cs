using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The part of a block that a clip tilted out of the block's plane shows, at the height each entity stands at.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// The rings a clip tilted out of the plan (XCLIP, made in a system AutoCAD tilted) cuts the planes of a block in, one for
        /// each height the block's entities stand at, as AutoCAD 2021 draws it (PNGOUT of a grid at height 50 under the boundary
        /// of a grid at height 0 that showed from y 73.09 to 130.83: the grid at 50 shows from y 44.23 to 101.96, where the lines
        /// along the boundary's normal meet that plane). An entity that lies flat is shown in the ring of its height. A line, a
        /// circle, an arc, an ellipse, a spline or a polyline that does not is cut by the prism itself, where its path leaves the
        /// boundary seen along the clip's normal (a line from (50, -100, 0) to (50, 100, 60) under a boundary tilted 30 degrees
        /// about X, y from -40 to 40, shows from y -54.2 to 24.5 where the ring of the plane at 0 would give -46.2 to 46.2). Any
        /// other entity that does not lie flat is shown in the ring of the block's own plane, as the display keeps no height to
        /// cut it by.
        /// </summary>
        private sealed class SectionLevels
        {
            private readonly DxfBlockReference _reference;
            private readonly DxfSpatialFilter _filter;
            private readonly Affine2 _placing;
            private readonly DisplayClip2 _outline;
            private readonly Dictionary<long, DisplayClip2> _rings = new Dictionary<long, DisplayClip2>();
            private long _key;
            private DisplayClipRegion _before;
            private bool _pushed;

            // The prism, made when the first line or curve that does not lie flat is met: the boundary in the clip system, the
            // plane of each of its sides as a height of the block's coordinates (the distance from the side's line, in the clip
            // system), and how a point of the block lands in the clip system (x and y each as three slopes and a shift).
            private bool _built;
            private GeoPoint2[] _polygon;
            private DxfSpatialFilter.Slab[] _sides;
            private double[] _clipX;
            private double[] _clipY;
            private GeoPoint3 _origin;

            /// <param name="reference">The clipped reference.</param>
            /// <param name="filter">Its clip.</param>
            /// <param name="placing">The block's coordinates into drawing units.</param>
            /// <param name="outline">The boundary's ring in drawing units, shown where no section can be made.</param>
            public SectionLevels(
                DxfBlockReference reference,
                DxfSpatialFilter filter,
                Affine2 placing,
                DisplayClip2 outline
            )
            {
                _reference = reference;
                _filter = filter;
                _placing = placing;
                _outline = outline;
            }

            /// <summary>Puts the ring of an entity's height on the list, taking the one before off where the height is not the same.</summary>
            public void Enter(DxfEntity entity, DisplayList2 list)
            {
                double height = HeightOf(entity);
                long key = (long)Math.Round(height * 1E6);

                if (_pushed && key == _key)
                {
                    return;
                }

                Leave(list);

                if (!_rings.TryGetValue(key, out DisplayClip2 ring))
                {
                    IReadOnlyList<GeoPoint2> section = _reference.GetClipSectionAt(_filter, height);
                    ring = section == null ? _outline : WorldRing(section, _placing);
                    _rings.Add(key, ring);
                }

                _before = list.PushClip(ring);
                _pushed = true;
                _key = key;
            }

            /// <summary>Takes the ring off the list.</summary>
            public void Leave(DisplayList2 list)
            {
                if (_pushed)
                {
                    list.PopClip(_before);
                    _pushed = false;
                }
            }

            /// <summary>
            /// The parts of a line or a curve that does not lie flat which the clip's prism holds, none where it holds none of it.
            /// The parts are in no ring: the prism has cut them.
            /// </summary>
            /// <param name="entity">The entity of the block.</param>
            /// <returns>The parts; null for an entity that lies flat, or is none of the shapes cut here, or cannot be cut, which
            /// is shown in a ring.</returns>
            public List<DxfEntity> Cut(DxfEntity entity)
            {
                if (!IsCut(entity) || !(entity is DxfCurve curve) || TryFlatHeight(entity, out _) || !Build())
                {
                    return null;
                }

                try
                {
                    var cuts = new List<double>();

                    foreach (DxfSpatialFilter.Slab side in _sides)
                    {
                        List<double> some = entity is DxfLine line
                            ? LineCuts(side, line)
                            : CurveCuts(side, entity, out _);

                        if (some == null)
                        {
                            return null;
                        }

                        cuts.AddRange(some);
                    }

                    return Parts(curve, cuts);
                }
                catch (Exception)
                {
                    // A curve that cannot be cut by the prism is shown in a ring.
                    return null;
                }
            }

            private static bool IsCut(DxfEntity entity) =>
                entity is DxfLine
                || entity is DxfCircle
                || entity is DxfArc
                || entity is DxfPolyline
                || entity is DxfEllipse
                || entity is DxfSpline
                || entity is DxfPolyline2d
                || entity is DxfPolyline3d;

            /// <summary>The distances along a line at which it meets the plane of one side of the prism.</summary>
            private static List<double> LineCuts(DxfSpatialFilter.Slab side, DxfLine line)
            {
                var cuts = new List<double>();
                GeoPoint3 a = line.StartPoint;
                GeoPoint3 b = line.EndPoint;
                double ha = side.HeightAt(a.X, a.Y, a.Z);
                double hb = side.HeightAt(b.X, b.Y, b.Z);
                double rise = hb - ha;
                double plane = side.Front.Value;

                if (Math.Abs(rise) > Tolerance.Default.EqualPoint)
                {
                    double t = (plane - ha) / rise;

                    if (t > 0.0 && t < 1.0)
                    {
                        cuts.Add(t * a.DistanceTo(b));
                    }
                }

                return cuts;
            }

            /// <summary>
            /// The parts of a curve cut at the parameters where it meets the planes of the sides, whose middle lies in the boundary
            /// seen along the clip's normal. A cut across which the curve stays in or stays out is left out, so that a part is as
            /// long as the prism keeps it.
            /// </summary>
            private List<DxfEntity> Parts(DxfCurve curve, List<double> cuts)
            {
                double from = curve.StartParam;
                double to = curve.EndParam;
                cuts.Sort();
                var bounds = new List<double> { from };

                foreach (double cut in cuts)
                {
                    if (cut > bounds[bounds.Count - 1] + 1E-9 && cut < to - 1E-9)
                    {
                        bounds.Add(cut);
                    }
                }

                bounds.Add(to);

                var inside = new bool[bounds.Count - 1];

                for (int i = 0; i < inside.Length; i++)
                {
                    inside[i] = Holds(curve.GetPointAtParameter(0.5 * (bounds[i] + bounds[i + 1])));
                }

                var real = new List<double>();

                for (int i = 1; i < inside.Length; i++)
                {
                    if (inside[i] != inside[i - 1])
                    {
                        real.Add(bounds[i]);
                    }
                }

                var parts = new List<DxfEntity>();

                if (real.Count == 0)
                {
                    if (inside[0])
                    {
                        parts.Add(curve);
                    }

                    return parts;
                }

                foreach (DxfCurve piece in curve.GetSplitCurves(real))
                {
                    if (Holds(piece.GetPointAtParameter(0.5 * (piece.StartParam + piece.EndParam))))
                    {
                        RememberCut(piece, curve);
                        parts.Add(piece);
                    }
                }

                return parts;
            }

            /// <summary>Whether the prism holds a point of the block: its foot on the boundary's plane lies inside the boundary.</summary>
            private bool Holds(GeoPoint3 point)
            {
                double x = _clipX[0] * point.X + _clipX[1] * point.Y + _clipX[2] * point.Z + _clipX[3];
                double y = _clipY[0] * point.X + _clipY[1] * point.Y + _clipY[2] * point.Z + _clipY[3];
                bool inside = false;
                int n = _polygon.Length;

                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    GeoPoint2 p = _polygon[i];
                    GeoPoint2 q = _polygon[j];

                    if ((p.Y > y) != (q.Y > y) && x < (q.X - p.X) * (y - p.Y) / (q.Y - p.Y) + p.X)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }

            /// <summary>Works out the prism once; false where the clip's matrices flatten space or its boundary is not a surface.</summary>
            private bool Build()
            {
                if (_built)
                {
                    return _sides != null;
                }

                _built = true;
                _origin = _reference.BlockTableRecord?.Origin ?? GeoPoint3.Origin;
                List<GeoPoint2> points = new List<GeoPoint2>(_filter.Points);

                if (points.Count == 2)
                {
                    GeoPoint2 a = points[0];
                    GeoPoint2 b = points[1];
                    points = new List<GeoPoint2>
                    {
                        new GeoPoint2(a.X, a.Y),
                        new GeoPoint2(b.X, a.Y),
                        new GeoPoint2(b.X, b.Y),
                        new GeoPoint2(a.X, b.Y),
                    };
                }

                if (
                    points.Count < 3
                    || !Foot(0, 0, 0, out GeoPoint2 zero)
                    || !Foot(1, 0, 0, out GeoPoint2 ex)
                    || !Foot(0, 1, 0, out GeoPoint2 ey)
                    || !Foot(0, 0, 1, out GeoPoint2 ez)
                )
                {
                    return false;
                }

                _clipX = new[] { ex.X - zero.X, ey.X - zero.X, ez.X - zero.X, zero.X };
                _clipY = new[] { ex.Y - zero.Y, ey.Y - zero.Y, ez.Y - zero.Y, zero.Y };
                _polygon = points.ToArray();
                var sides = new List<DxfSpatialFilter.Slab>(points.Count);

                for (int i = 0; i < points.Count; i++)
                {
                    GeoPoint2 a = points[i];
                    GeoPoint2 b = points[(i + 1) % points.Count];
                    double dx = b.X - a.X;
                    double dy = b.Y - a.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);

                    if (!(length > Tolerance.Default.EqualPoint))
                    {
                        continue;
                    }

                    // The distance of a point of the clip system from the line of the side: gx x + gy y + g0.
                    double gx = -dy / length;
                    double gy = dx / length;
                    double g0 = (dy * a.X - dx * a.Y) / length;
                    sides.Add(
                        new DxfSpatialFilter.Slab(
                            gx * _clipX[0] + gy * _clipY[0],
                            gx * _clipX[1] + gy * _clipY[1],
                            gx * _clipX[2] + gy * _clipY[2],
                            gx * _clipX[3] + gy * _clipY[3] + g0,
                            0.0,
                            null
                        )
                    );
                }

                if (sides.Count < 3)
                {
                    return false;
                }

                _sides = sides.ToArray();
                return true;
            }

            /// <summary>The point of the clip system a point of the block (as its entities have it) lands on.</summary>
            private bool Foot(double x, double y, double z, out GeoPoint2 clip) =>
                _filter.TryFromBlock(new GeoPoint3(x - _origin.X, y - _origin.Y, z - _origin.Z), out clip);

            /// <summary>The height an entity lies flat at in the block's coordinates, 0 for one that does not.</summary>
            private static double HeightOf(DxfEntity entity) => TryFlatHeight(entity, out double height) ? height : 0.0;

            /// <summary>Whether an entity lies flat, and the height it lies at in the block's coordinates.</summary>
            private static bool TryFlatHeight(DxfEntity entity, out double height)
            {
                try
                {
                    GeoAabb3 box = entity.GeometricExtents;

                    if (!box.IsEmpty && box.Max.Z - box.Min.Z <= Tolerance.Default.EqualPoint)
                    {
                        height = 0.5 * (box.Min.Z + box.Max.Z);
                        return true;
                    }
                }
                catch (Exception)
                {
                    // An entity that has no extents is shown in the ring of the block's plane.
                }

                height = 0.0;
                return false;
            }
        }
    }
}
