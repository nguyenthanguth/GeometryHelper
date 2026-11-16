using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The clipping planes of a clipped block reference (XCLIP Clipdepth) laid on what its block holds.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// What of an entity of a block whose reference has front and back clipping planes is drawn, as AutoCAD 2021 shows it
        /// (PNGOUT of a block of lines, circles, arcs and polylines at several heights): the entity is shown where it lies
        /// between the planes, planes included, and not at all where its box lies wholly above the front plane or below the back
        /// one. A line, a circle, an arc and a polyline that cross a plane are cut at it: a circle standing on its edge with
        /// front 10 and back -10 leaves the two arcs of its heights between them. Any other entity that crosses one is drawn
        /// whole, since the display keeps no height to cut it by.
        /// </summary>
        /// <param name="slabs">The planes, each measured in the block's coordinates: the reference's own, and those of the
        /// blocks it stands in, carried into this block.</param>
        /// <param name="entity">The entity of the block.</param>
        /// <returns>The entities to draw: itself, cut copies of it, or none.</returns>
        private static List<DxfEntity> Slice(IReadOnlyList<DxfSpatialFilter.Slab> slabs, DxfEntity entity)
        {
            var pieces = new List<DxfEntity>(1) { entity };

            for (int s = 0; s < slabs.Count && pieces.Count > 0; s++)
            {
                var next = new List<DxfEntity>(pieces.Count);

                foreach (DxfEntity piece in pieces)
                {
                    SliceBy(slabs[s], piece, next);
                }

                pieces = next;
            }

            return pieces;
        }

        /// <summary>
        /// The planes the entities of a block are cut by where a reference to it is drawn: those of the blocks the reference stands
        /// in, carried into its block by its placing (and the shift of its cell, where it is an array), and its own, which are in
        /// its block's coordinates already. Null where there are none.
        /// </summary>
        private static IReadOnlyList<DxfSpatialFilter.Slab> PlacedSlabs(
            IReadOnlyList<DxfSpatialFilter.Slab> held,
            GeoTransform3 placing,
            DxfSpatialFilter.Slab own,
            double shiftX,
            double shiftY
        )
        {
            if (held == null || held.Count == 0)
            {
                return own == null ? null : new[] { own };
            }

            var shift = new GeoVector3(shiftX, shiftY, 0.0);
            var slabs = new List<DxfSpatialFilter.Slab>(held.Count + 1);

            foreach (DxfSpatialFilter.Slab slab in held)
            {
                slabs.Add(slab.Through(placing, shift));
            }

            if (own != null)
            {
                slabs.Add(own);
            }

            return slabs;
        }

        /// <summary>The planes of a set for entities that stand a distance from where the set measures them from.</summary>
        private static IReadOnlyList<DxfSpatialFilter.Slab> ShiftedSlabs(
            IReadOnlyList<DxfSpatialFilter.Slab> slabs,
            double shiftX,
            double shiftY,
            double shiftZ
        )
        {
            if (slabs == null)
            {
                return slabs;
            }

            var shift = new GeoVector3(shiftX, shiftY, shiftZ);
            var shifted = new List<DxfSpatialFilter.Slab>(slabs.Count);

            foreach (DxfSpatialFilter.Slab slab in slabs)
            {
                shifted.Add(slab.Through(GeoTransform3.Identity, shift));
            }

            return shifted;
        }

        /// <summary>The part of an entity between the two planes of one set, added to a list.</summary>
        private static void SliceBy(DxfSpatialFilter.Slab slab, DxfEntity entity, List<DxfEntity> into)
        {
            GeoAabb3 box;

            try
            {
                box = entity.GeometricExtents;
            }
            catch (Exception)
            {
                // An entity that has no extents is shown as it is.
                into.Add(entity);
                return;
            }

            if (box.IsEmpty)
            {
                into.Add(entity);
                return;
            }

            double least = double.MaxValue;
            double most = double.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                double h = slab.HeightAt(
                    (corner & 1) == 0 ? box.Min.X : box.Max.X,
                    (corner & 2) == 0 ? box.Min.Y : box.Max.Y,
                    (corner & 4) == 0 ? box.Min.Z : box.Max.Z
                );
                least = Math.Min(least, h);
                most = Math.Max(most, h);
            }

            double tolerance = Tolerance.Default.EqualPoint;
            double? front = slab.Front;
            double? back = slab.Back;

            if ((front.HasValue && least > front.Value + tolerance) || (back.HasValue && most < back.Value - tolerance))
            {
                return;
            }

            bool crosses =
                (front.HasValue && most > front.Value + tolerance) || (back.HasValue && least < back.Value - tolerance);

            if (!crosses)
            {
                into.Add(entity);
                return;
            }

            try
            {
                switch (entity)
                {
                    case DxfLine line:
                        CutLine(slab, line, into);
                        return;
                    case DxfCircle circle:
                        CutCurve(
                            slab,
                            circle,
                            CircleCuts(slab, circle.Center, circle.Normal, circle.Radius, 0.0, 2.0 * Math.PI),
                            into
                        );
                        return;
                    case DxfArc arc:
                        CutCurve(
                            slab,
                            arc,
                            CircleCuts(slab, arc.Center, arc.Normal, arc.Radius, arc.StartParam, arc.EndParam),
                            into
                        );
                        return;
                    case DxfPolyline polyline:
                        CutCurve(slab, polyline, PolylineCuts(slab, polyline), into);
                        return;
                }
            }
            catch (Exception)
            {
                // A curve that cannot be cut is shown as it is.
            }

            into.Add(entity);
        }

        /// <summary>The part of a line whose height lies between the planes, from 0 at its start to 1 at its end.</summary>
        private static void CutLine(DxfSpatialFilter.Slab slab, DxfLine line, List<DxfEntity> into)
        {
            double tolerance = Tolerance.Default.EqualPoint;
            double? front = slab.Front;
            double? back = slab.Back;
            GeoPoint3 a = line.StartPoint;
            GeoPoint3 b = line.EndPoint;
            double ha = slab.HeightAt(a.X, a.Y, a.Z);
            double hb = slab.HeightAt(b.X, b.Y, b.Z);
            double rise = hb - ha;

            if (Math.Abs(rise) <= tolerance)
            {
                into.Add(line);
                return;
            }

            double t0 = 0.0;
            double t1 = 1.0;

            if (front.HasValue)
            {
                double t = (front.Value - ha) / rise;

                if (rise > 0.0)
                {
                    t1 = Math.Min(t1, t);
                }
                else
                {
                    t0 = Math.Max(t0, t);
                }
            }

            if (back.HasValue)
            {
                double t = (back.Value - ha) / rise;

                if (rise > 0.0)
                {
                    t0 = Math.Max(t0, t);
                }
                else
                {
                    t1 = Math.Min(t1, t);
                }
            }

            if (t0 > t1)
            {
                return;
            }

            var cut = (DxfLine)line.Clone();
            cut.StartPoint = new GeoPoint3(a.X + t0 * (b.X - a.X), a.Y + t0 * (b.Y - a.Y), a.Z + t0 * (b.Z - a.Z));
            cut.EndPoint = new GeoPoint3(a.X + t1 * (b.X - a.X), a.Y + t1 * (b.Y - a.Y), a.Z + t1 * (b.Z - a.Z));
            into.Add(cut);
        }

        /// <summary>
        /// A curve cut at the parameters where it meets a plane, the pieces whose middle lies between the planes kept. A curve
        /// that meets no plane, or one that is cut once, is shown whole where it is not wholly outside.
        /// </summary>
        private static void CutCurve(
            DxfSpatialFilter.Slab slab,
            DxfCurve curve,
            List<double> cuts,
            List<DxfEntity> into
        )
        {
            IReadOnlyList<DxfCurve> pieces;

            if (cuts.Count == 0)
            {
                pieces = new[] { curve };
            }
            else
            {
                cuts.Sort();
                pieces = curve.GetSplitCurves(cuts);

                if (pieces.Count < 2)
                {
                    into.Add(curve);
                    return;
                }
            }

            double tolerance = Tolerance.Default.EqualPoint;

            foreach (DxfCurve piece in pieces)
            {
                GeoPoint3 middle = piece.GetPointAtParameter(0.5 * (piece.StartParam + piece.EndParam));
                double h = slab.HeightAt(middle.X, middle.Y, middle.Z);

                if (
                    (!slab.Front.HasValue || h <= slab.Front.Value + tolerance)
                    && (!slab.Back.HasValue || h >= slab.Back.Value - tolerance)
                )
                {
                    into.Add(piece);
                }
            }
        }

        /// <summary>
        /// The angles, from one to another, at which a circle meets the planes. A point of the circle at an angle is
        /// c + A cos(angle) + B sin(angle) high, the three numbers read off the heights at the angles 0, a quarter turn and a
        /// half; the angles that put it at a plane are on either side of atan2(B, A), by the arc cosine of the plane's distance
        /// from c over the radius of the wave. A circle on a plane parallel to the planes, and one a plane only touches, meet
        /// none.
        /// </summary>
        private static List<double> CircleCuts(
            DxfSpatialFilter.Slab slab,
            GeoPoint3 centre,
            GeoVector3 normal,
            double radius,
            double from,
            double to
        )
        {
            var circle = new DxfCircle(centre, normal, radius);
            double h0 = HeightOf(slab, circle.GetPointAtParameter(0.0));
            double h90 = HeightOf(slab, circle.GetPointAtParameter(0.5 * Math.PI));
            double h180 = HeightOf(slab, circle.GetPointAtParameter(Math.PI));
            var cuts = new List<double>();
            AddWaveCuts(slab, h0, h90, h180, from, to, cuts);
            return cuts;
        }

        /// <summary>The angles within a range at which a wave of three heights meets each plane of a set, added to a list.</summary>
        private static void AddWaveCuts(
            DxfSpatialFilter.Slab slab,
            double h0,
            double h90,
            double h180,
            double from,
            double to,
            List<double> cuts
        )
        {
            double c = 0.5 * (h0 + h180);
            double a = 0.5 * (h0 - h180);
            double b = h90 - c;
            double wave = Math.Sqrt(a * a + b * b);

            if (wave <= Tolerance.Default.EqualPoint)
            {
                return;
            }

            double phase = Math.Atan2(b, a);

            foreach (double plane in Planes(slab))
            {
                double k = (plane - c) / wave;

                if (!(k > -1.0 && k < 1.0))
                {
                    continue;
                }

                double spread = Math.Acos(k);

                for (int side = -1; side <= 1; side += 2)
                {
                    for (int turn = -2; turn <= 2; turn++)
                    {
                        double angle = phase + side * spread + turn * 2.0 * Math.PI;

                        if (angle > from && angle < to)
                        {
                            cuts.Add(angle);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The parameters at which a polyline meets the planes: on a line segment where the height passes a plane between its
        /// ends, on an arc segment where its wave does (a fraction of the segment from its first vertex, as the parameter of a
        /// polyline runs), and at each vertex that lies on a plane, so that a piece of it is wholly on one side.
        /// </summary>
        private static List<double> PolylineCuts(DxfSpatialFilter.Slab slab, DxfPolyline polyline)
        {
            var cuts = new List<double>();
            int count = polyline.NumberOfVertices;

            if (count < 2)
            {
                return cuts;
            }

            double tolerance = Tolerance.Default.EqualPoint;
            bool closed = polyline.Closed;
            int segments = closed ? count : count - 1;
            var heights = new double[count];

            for (int i = 0; i < count; i++)
            {
                heights[i] = HeightOf(slab, polyline.GetPoint3dAt(i));
            }

            foreach (double plane in Planes(slab))
            {
                for (int i = 0; i < count; i++)
                {
                    // A vertex on the plane is a place to cut at; the ends of an open polyline cut nothing.
                    if (Math.Abs(heights[i] - plane) <= tolerance && (closed || (i > 0 && i < count - 1)))
                    {
                        cuts.Add(i);
                    }
                }
            }

            for (int i = 0; i < segments; i++)
            {
                DxfSegmentType type = polyline.GetSegmentType(i);
                double ha = heights[i];
                double hb = heights[(i + 1) % count];

                if (type == DxfSegmentType.Line)
                {
                    foreach (double plane in Planes(slab))
                    {
                        if (
                            (ha < plane - tolerance && hb > plane + tolerance)
                            || (ha > plane + tolerance && hb < plane - tolerance)
                        )
                        {
                            cuts.Add(i + (plane - ha) / (hb - ha));
                        }
                    }
                }
                else if (type == DxfSegmentType.Arc)
                {
                    GeoArc3 arc = polyline.GetArcSegmentAt(i);
                    GeoPoint3 first = polyline.GetPoint3dAt(i);
                    bool reversed = arc.StartPoint.DistanceTo(first) > arc.EndPoint.DistanceTo(first);
                    double sweep = arc.SweptAngle;
                    double h0 = HeightOf(slab, arc.GetPointAtAngle(arc.StartAngle));
                    double h90 = HeightOf(slab, arc.GetPointAtAngle(arc.StartAngle + 0.5 * Math.PI));
                    double h180 = HeightOf(slab, arc.GetPointAtAngle(arc.StartAngle + Math.PI));
                    var angles = new List<double>();
                    AddWaveCuts(slab, h0, h90, h180, Math.Min(0.0, sweep), Math.Max(0.0, sweep), angles);

                    foreach (double angle in angles)
                    {
                        double fraction = angle / sweep;
                        cuts.Add(i + (reversed ? 1.0 - fraction : fraction));
                    }
                }
            }

            return cuts;
        }

        /// <summary>The planes of a set: the front and the back, where it has them.</summary>
        private static IEnumerable<double> Planes(DxfSpatialFilter.Slab slab)
        {
            if (slab.Front.HasValue)
            {
                yield return slab.Front.Value;
            }

            if (slab.Back.HasValue)
            {
                yield return slab.Back.Value;
            }
        }

        private static double HeightOf(DxfSpatialFilter.Slab slab, GeoPoint3 point) =>
            slab.HeightAt(point.X, point.Y, point.Z);
    }
}
