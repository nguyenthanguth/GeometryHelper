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
        /// one. A line, a circle, an arc, an ellipse, a spline and a polyline of any kind (lightweight, 2D or 3D) that cross a
        /// plane are cut at it: a circle standing on its edge with front 10 and back -10 leaves the two arcs of its heights
        /// between them. Any other entity that crosses one is drawn whole, since the display keeps no height to cut it by.
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

        /// <summary>Draws the parts of an entity that a clip's prism has cut, each cut again by the planes of Clipdepth where there are any.</summary>
        private void DrawParts(
            List<DxfEntity> parts,
            IReadOnlyList<DxfSpatialFilter.Slab> slabs,
            Frame frame,
            DisplayList2 list,
            bool shown
        )
        {
            foreach (DxfEntity part in parts)
            {
                if (slabs == null)
                {
                    Draw(part, frame, list, null, shown);
                    continue;
                }

                foreach (DxfEntity drawn in Slice(slabs, part))
                {
                    Draw(drawn, frame, list, null, shown);
                }
            }
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
            double shiftY,
            double shiftZ
        )
        {
            if (held == null || held.Count == 0)
            {
                return own == null ? null : new[] { own };
            }

            var shift = new GeoVector3(shiftX, shiftY, shiftZ);
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

        /// <summary>
        /// The steps from a cell of an array to the next along its columns and along its rows, in the coordinates of the block the
        /// array stands in, as unit vectors: the axes of the array's plane turned by its angle, the scale of the placing taken
        /// off. False where the plane of the array is the plan's, whose steps are those of its angle alone.
        /// </summary>
        private static bool TryTiltedSteps(DxfBlockReference reference, out GeoVector3 column, out GeoVector3 row)
        {
            column = default;
            row = default;
            GeoVector3 normal = reference.Normal;
            GeoVector3 scales = reference.ScaleFactors;

            if ((normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0) || scales.X == 0.0 || scales.Y == 0.0)
            {
                return false;
            }

            GeoTransform3 placing = reference.BlockTransform;
            column = new GeoVector3(placing[0, 0] / scales.X, placing[1, 0] / scales.X, placing[2, 0] / scales.X);
            row = new GeoVector3(placing[0, 1] / scales.Y, placing[1, 1] / scales.Y, placing[2, 1] / scales.Y);
            return true;
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
                if (entity is DxfLine line)
                {
                    CutLine(slab, line, into);
                    return;
                }

                List<double> cuts = CurveCuts(slab, entity, out DxfCurve curve);

                if (cuts != null)
                {
                    CutCurve(slab, curve, cuts, into);
                    return;
                }
            }
            catch (Exception)
            {
                // A curve that cannot be cut is shown as it is.
            }

            into.Add(entity);
        }

        /// <summary>
        /// The parameters at which a curve other than a line meets the planes of a set, null for an entity that is none of the
        /// curves the display cuts.
        /// </summary>
        private static List<double> CurveCuts(DxfSpatialFilter.Slab slab, DxfEntity entity, out DxfCurve curve)
        {
            switch (entity)
            {
                case DxfCircle circle:
                    curve = circle;
                    return CircleCuts(slab, circle.Center, circle.Normal, circle.Radius, 0.0, 2.0 * Math.PI);
                case DxfArc arc:
                    curve = arc;
                    return CircleCuts(slab, arc.Center, arc.Normal, arc.Radius, arc.StartParam, arc.EndParam);
                case DxfPolyline polyline:
                    curve = polyline;
                    return PolylineCuts(slab, polyline);
                case DxfEllipse ellipse:
                    curve = ellipse;
                    return EllipseCuts(slab, ellipse);
                case DxfSpline spline:
                    curve = spline;
                    return SplineCuts(slab, spline);
                case DxfPolyline2d heavy:
                    curve = heavy;
                    return ChainCuts(slab, heavy.DrawnChain(), heavy.Closed);
                case DxfPolyline3d space:
                    curve = space;
                    return ChainCuts(slab, space.DrawnChain(), space.Closed);
                default:
                    curve = null;
                    return null;
            }
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
            RememberCut(cut, line);
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
                    if (!ReferenceEquals(piece, curve))
                    {
                        RememberCut(piece, curve);
                    }

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

        /// <summary>
        /// The parameters (eccentric angles) at which an ellipse meets the planes: a point of it at an angle is
        /// c + A cos(angle) + B sin(angle) high, the three numbers read off the heights at the ends of its axes, as for a circle.
        /// </summary>
        private static List<double> EllipseCuts(DxfSpatialFilter.Slab slab, DxfEllipse ellipse)
        {
            GeoPoint3 centre = ellipse.Center;
            GeoVector3 major = ellipse.MajorAxis;
            double h0 = HeightOf(slab, centre.Add(major));
            double h90 = HeightOf(slab, centre.Add(ellipse.MinorAxis));
            double h180 = HeightOf(slab, centre.Subtract(major));
            var cuts = new List<double>();
            AddWaveCuts(slab, h0, h90, h180, ellipse.StartParam, ellipse.EndParam, cuts);
            return cuts;
        }

        /// <summary>
        /// The parameters at which a spline meets the planes: its height walked at a fixed number of steps for each control point,
        /// and each step whose ends lie on either side of a plane narrowed down by halving to the parameter where it meets it.
        /// </summary>
        private static List<double> SplineCuts(DxfSpatialFilter.Slab slab, DxfSpline spline)
        {
            var cuts = new List<double>();
            double from = spline.StartParam;
            double to = spline.EndParam;

            if (!(to > from))
            {
                return cuts;
            }

            int steps = Math.Min(8192, Math.Max(64, 16 * Math.Max(spline.NumControlPoints, spline.NumFitPoints)));
            var heights = new double[steps + 1];

            for (int i = 0; i <= steps; i++)
            {
                heights[i] = HeightOf(slab, spline.GetPointAtParameter(SplineStep(from, to, i, steps)));
            }

            foreach (double plane in Planes(slab))
            {
                for (int i = 1; i <= steps; i++)
                {
                    if ((heights[i - 1] < plane) == (heights[i] < plane))
                    {
                        continue;
                    }

                    double low = SplineStep(from, to, i - 1, steps);
                    double high = SplineStep(from, to, i, steps);
                    bool lowBelow = heights[i - 1] < plane;

                    for (int round = 0; round < 60; round++)
                    {
                        double middle = 0.5 * (low + high);

                        if ((HeightOf(slab, spline.GetPointAtParameter(middle)) < plane) == lowBelow)
                        {
                            low = middle;
                        }
                        else
                        {
                            high = middle;
                        }
                    }

                    cuts.Add(0.5 * (low + high));
                }
            }

            return cuts;
        }

        private static double SplineStep(double from, double to, int step, int steps) =>
            step == steps ? to : from + (to - from) * step / steps;

        /// <summary>
        /// The parameters at which a heavy polyline, 2D or 3D, meets the planes, found as for a lightweight one: on a line segment
        /// where the height passes a plane between its ends, on an arc segment where its wave does, and at each vertex that lies
        /// on a plane, so that a piece of it is wholly on one side. The parameter is that of a chain: the index of the vertex a
        /// segment leaves plus how far along the segment (in angle on an arc).
        /// </summary>
        private static List<double> ChainCuts(DxfSpatialFilter.Slab slab, DxfBulgeChain chain, bool closed)
        {
            var cuts = new List<double>();
            int segments = chain.SegmentCount;

            if (segments < 1)
            {
                return cuts;
            }

            double tolerance = Tolerance.Default.EqualPoint;
            int count = closed ? segments : segments + 1;
            var heights = new double[count];

            for (int i = 0; i < count; i++)
            {
                heights[i] = HeightOf(slab, chain.PointAt(i));
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
                double ha = heights[i];
                double hb = heights[(i + 1) % count];

                if (!chain.IsArc(i))
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

                    continue;
                }

                chain.GetArcWave(i, out GeoPoint3 centre, out GeoVector3 u, out GeoVector3 v, out double sweep);
                var angles = new List<double>();
                AddWaveCuts(
                    slab,
                    HeightOf(slab, centre.Add(u)),
                    HeightOf(slab, centre.Add(v)),
                    HeightOf(slab, centre.Subtract(u)),
                    Math.Min(0.0, sweep),
                    Math.Max(0.0, sweep),
                    angles
                );

                foreach (double angle in angles)
                {
                    cuts.Add(i + angle / sweep);
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

        // The piece of a line or a curve that a plane or a prism cut, and what it was cut from, kept so that the linetype of the
        // piece goes on from where the pattern of the whole stood at its start, as AutoCAD 2021 draws it (PNGOUT of a dashed
        // line, and of polylines with PLINEGEN on and off, across a plane: the dashes of the pieces fall where those of the
        // whole fall, none restarts at the cut).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            DxfEntity,
            DxfEntity
        > s_cutParents = new System.Runtime.CompilerServices.ConditionalWeakTable<DxfEntity, DxfEntity>();

        // Whether any piece was ever cut: until one is, the entities with dashes are not looked up.
        private static int s_anyCut;

        private static void RememberCut(DxfEntity piece, DxfEntity parent)
        {
            s_cutParents.GetValue(piece, key => parent);
            System.Threading.Volatile.Write(ref s_anyCut, 1);
        }

        /// <summary>
        /// The style of an entity with dashes where the entity is a piece cut from a longer line or curve: the same, with its
        /// pattern starting as far along as the part cut off ahead of the piece, measured on the drawing as the dashes are.
        /// </summary>
        private static DisplayStyle Continued(DisplayStyle style, DxfEntity piece, Frame frame)
        {
            if (System.Threading.Volatile.Read(ref s_anyCut) == 0 || !s_cutParents.TryGetValue(piece, out _))
            {
                return style;
            }

            double phase;

            try
            {
                phase = PhaseOf(piece);
            }
            catch (Exception)
            {
                // A piece whose place on what it was cut from cannot be found starts its pattern afresh.
                return style;
            }

            phase *= frame.Matrix.Scale;
            return phase > 0.0 ? new DisplayStyle(style.Rgb, style.LineWeight, style.Dashes, phase) : style;
        }

        /// <summary>How far along the plan a piece starts from the start of the line or curve it came from, through the pieces it was cut from in turn.</summary>
        private static double PhaseOf(DxfEntity piece)
        {
            double total = 0.0;
            DxfEntity current = piece;

            while (s_cutParents.TryGetValue(current, out DxfEntity parent))
            {
                total += PlanLengthTo((DxfCurve)parent, (DxfCurve)current);
                current = parent;
            }

            return total;
        }

        /// <summary>The length in the plan (the block's x and y) from the start of a curve to the start of a piece of it.</summary>
        private static double PlanLengthTo(DxfCurve parent, DxfCurve piece)
        {
            GeoPoint3 at = piece.StartPoint;

            if (parent is DxfLine)
            {
                GeoPoint3 start = parent.StartPoint;
                return Math.Sqrt((at.X - start.X) * (at.X - start.X) + (at.Y - start.Y) * (at.Y - start.Y));
            }

            double from = parent.StartParam;
            double to = parent.GetParameterAtPoint(at);

            if (!(to > from))
            {
                return 0.0;
            }

            DxfBulgeChain chain = null;

            switch (parent)
            {
                case DxfPolyline polyline:
                    chain = polyline.DrawnChain();
                    break;
                case DxfPolyline2d heavy:
                    chain = heavy.DrawnChain();
                    break;
                case DxfPolyline3d space:
                    chain = space.DrawnChain();
                    break;
            }

            double sum = 0.0;

            if (chain != null)
            {
                // A line segment in one chord, an arc segment in sixteen, from the start to the parameter.
                GeoPoint3 previous = chain.PointAt(from);
                double t = from;

                while (t < to)
                {
                    double next = Math.Min(to, Math.Floor(t) + 1.0);
                    int chords = chain.IsArc((int)Math.Floor(t)) ? 16 : 1;

                    for (int k = 1; k <= chords; k++)
                    {
                        GeoPoint3 point = chain.PointAt(t + (next - t) * k / chords);
                        sum += Math.Sqrt(
                            (point.X - previous.X) * (point.X - previous.X)
                                + (point.Y - previous.Y) * (point.Y - previous.Y)
                        );
                        previous = point;
                    }

                    t = next;
                }

                return sum;
            }

            const int Chords = 96;
            GeoPoint3 last = parent.GetPointAtParameter(from);

            for (int k = 1; k <= Chords; k++)
            {
                GeoPoint3 point = parent.GetPointAtParameter(k == Chords ? to : from + (to - from) * k / Chords);
                sum += Math.Sqrt((point.X - last.X) * (point.X - last.X) + (point.Y - last.Y) * (point.Y - last.Y));
                last = point;
            }

            return sum;
        }
    }
}
