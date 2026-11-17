using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>The clipping planes of a clipped block reference (XCLIP Clipdepth) laid on what its block holds.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>
        /// What of an entity of a block whose reference has front and back clipping planes is drawn, as AutoCAD 2021 shows it
        /// (PNGOUT of a block of lines, circles, arcs and polylines at several heights): the entity is shown where it lies
        /// between the planes, planes included, and not at all where its box lies wholly above the front plane or below the back
        /// one. A line, a circle, an arc, an ellipse, a spline and a polyline of any kind (lightweight, 2D or 3D) that cross a
        /// plane are cut at it: a circle standing on its edge with front 10 and back -10 leaves the three arcs of its heights
        /// between them (the one that holds the circle's start is two, as the pattern of a dash begins there). A text, an MTEXT,
        /// a hatch, a solid or a 3D face in a plane tilted out of the plan is cut where the planes meet its plane, as the strip of
        /// the plan between them. Any other entity that crosses one is drawn whole, since the display keeps no height to cut it by.
        /// </summary>
        /// <param name="slabs">The planes, each measured in the block's coordinates: the reference's own, and those of the
        /// blocks it stands in, carried into this block.</param>
        /// <param name="entity">The entity of the block.</param>
        /// <returns>The pieces to draw: the entity itself, cut copies of it, or none.</returns>
        private static List<Piece> Slice(IReadOnlyList<DxfSpatialFilter.Slab> slabs, DxfEntity entity)
        {
            var pieces = new List<Piece>(1) { new Piece(entity, null) };

            for (int s = 0; s < slabs.Count && pieces.Count > 0; s++)
            {
                var next = new List<Piece>(pieces.Count);

                foreach (Piece piece in pieces)
                {
                    SliceBy(slabs[s], piece, next);
                }

                pieces = next;
            }

            return pieces;
        }

        /// <summary>
        /// What of an entity is drawn: the entity, or a cut copy of it, and for a text, a hatch or a solid that crosses a plane
        /// the part of the plan that keeps what lies between the planes: the box the entity stands in, grown, as four numbers
        /// (least x, least y, most x, most y), then each half-plane that bounds the part as three numbers a, b and c, a point
        /// (x, y) of the block's plan being kept where a x + b y + c is not below 0; null where the piece is drawn whole.
        /// </summary>
        private readonly struct Piece
        {
            public Piece(DxfEntity entity, double[] keep)
            {
                Entity = entity;
                Keep = keep;
            }

            public DxfEntity Entity { get; }

            public double[] Keep { get; }
        }

        /// <summary>
        /// Draws an entity of a block whose reference has planes: the pieces <see cref="Slice"/> leaves, each of a text, a
        /// hatch or a solid that a plane crosses inside the part of the plan between the planes.
        /// </summary>
        private void DrawSliced(
            IReadOnlyList<DxfSpatialFilter.Slab> slabs,
            DxfEntity entity,
            Frame frame,
            DisplayList2 list,
            bool shown
        )
        {
            foreach (Piece piece in Slice(slabs, entity))
            {
                if (piece.Keep == null)
                {
                    Draw(piece.Entity, frame, list, null, shown);
                    continue;
                }

                IReadOnlyList<GeoPoint2> ring = KeptRing(piece.Keep);

                if (ring == null)
                {
                    continue;
                }

                DisplayClipRegion before = list.PushClip(WorldRing(ring, frame.Matrix));

                try
                {
                    Draw(piece.Entity, frame, list, null, shown);
                }
                finally
                {
                    list.PopClip(before);
                }
            }
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

                DrawSliced(slabs, part, frame, list, shown);
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
        private static void SliceBy(DxfSpatialFilter.Slab slab, Piece piece, List<Piece> into)
        {
            DxfEntity entity = piece.Entity;
            GeoAabb3 box = default;
            bool measured;

            try
            {
                box = entity.GeometricExtents;
                measured = !box.IsEmpty;
            }
            catch (Exception)
            {
                measured = false;
            }

            if (
                (entity is DxfDBText || entity is DxfMText)
                && (
                    !measured
                    || (box.Max.X - box.Min.X) + (box.Max.Y - box.Min.Y) + (box.Max.Z - box.Min.Z)
                        <= Tolerance.Default.EqualPoint
                )
            )
            {
                // A text the drawing's fonts do not measure has no extents to say whether the planes cross it, and the planes may
                // meet its plane anywhere along it: it is cut where they do, the strip of the plan its plane's height puts between
                // them, which holds it whole where it lies between them and none of it where it does not. A text in a plane that
                // the planes do not cross at an angle is judged by its position, as any other is.
                try
                {
                    if (
                        TryStrip(
                            slab,
                            entity,
                            box,
                            measured,
                            slab.Front.HasValue,
                            slab.Back.HasValue,
                            out double[] unknown
                        )
                    )
                    {
                        into.Add(new Piece(entity, Joined(piece.Keep, unknown)));
                        return;
                    }
                }
                catch (Exception)
                {
                    // A text that cannot be cut is shown as it is.
                }
            }

            if (!measured)
            {
                // An entity that has no extents is shown as it is.
                into.Add(piece);
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

            bool overFront = front.HasValue && most > front.Value + tolerance;
            bool underBack = back.HasValue && least < back.Value - tolerance;

            if (!overFront && !underBack)
            {
                into.Add(piece);
                return;
            }

            try
            {
                var cut = new List<DxfEntity>(2);

                if (entity is DxfLine line)
                {
                    CutLine(slab, line, cut);
                    AddAll(cut, piece.Keep, into);
                    return;
                }

                List<double> cuts = CurveCuts(slab, entity, out DxfCurve curve);

                if (cuts != null)
                {
                    CutCurve(slab, curve, cuts, cut);
                    AddAll(cut, piece.Keep, into);
                    return;
                }

                // A text, a hatch or a solid is cut where the planes meet its own plane, in the plan.
                if (TryStrip(slab, entity, box, true, overFront, underBack, out double[] strip))
                {
                    into.Add(new Piece(entity, Joined(piece.Keep, strip)));
                    return;
                }
            }
            catch (Exception)
            {
                // An entity that cannot be cut is shown as it is.
            }

            into.Add(piece);
        }

        private static void AddAll(List<DxfEntity> entities, double[] keep, List<Piece> into)
        {
            foreach (DxfEntity entity in entities)
            {
                into.Add(new Piece(entity, keep));
            }
        }

        /// <summary>The box and half-planes of two strips: the box of the first, and the half-planes of both.</summary>
        private static double[] Joined(double[] first, double[] second)
        {
            if (first == null)
            {
                return second;
            }

            var joined = new double[first.Length + second.Length - 4];
            Array.Copy(first, joined, first.Length);
            Array.Copy(second, 4, joined, first.Length, second.Length - 4);
            return joined;
        }

        /// <summary>
        /// The part of the plan that holds what a text, a hatch, a solid or a 3D face shows between the planes, as AutoCAD 2021
        /// draws it (PNGOUT in plan of a text, a hatch and a solid of a plane tilted 45 degrees about X, planes 20 and 5: what is
        /// left is the strip of the plan where the plane's height lies between them, glyph by glyph, here y from 5 to 20). The
        /// height along the entity's plane is a straight function of the plan, so the part is bounded by a line for each plane
        /// that crosses it. A plane standing upright is seen edge-on in the plan, where its height does not change along any
        /// line of the plan that it does not stand on, and is drawn whole.
        /// </summary>
        /// <param name="slab">The planes.</param>
        /// <param name="entity">The entity, which the planes cross.</param>
        /// <param name="box">The entity's extents.</param>
        /// <param name="measured">Whether the box is the entity's: false for a text its fonts do not measure, which is taken to
        /// stand about its position as far as its words can reach.</param>
        /// <param name="overFront">Whether the entity stands over the front plane in part.</param>
        /// <param name="underBack">Whether it stands under the back plane in part.</param>
        /// <param name="strip">The box and half-planes of a <see cref="Piece"/>.</param>
        /// <returns>False where the entity has no plane to cut by, or it is upright, or its plane is parallel to the planes.</returns>
        private static bool TryStrip(
            DxfSpatialFilter.Slab slab,
            DxfEntity entity,
            GeoAabb3 box,
            bool measured,
            bool overFront,
            bool underBack,
            out double[] strip
        )
        {
            strip = null;

            if (!TryPlaneOf(entity, out GeoPoint3 point, out GeoVector3 normal) || Math.Abs(normal.Z) <= 1E-3)
            {
                return false;
            }

            // The height of a point of the plane above (x, y): gx x + gy y + h0.
            double gx = slab.X - slab.Z * normal.X / normal.Z;
            double gy = slab.Y - slab.Z * normal.Y / normal.Z;
            double along = normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z;
            double h0 = slab.Offset + slab.Z * along / normal.Z;
            double g = Math.Sqrt(gx * gx + gy * gy);

            if (!(g > 1E-9))
            {
                return false;
            }

            // The box grown by twice its diagonal, so that what the display draws of the entity beyond the extents the plane's
            // tilt gives it, a text's height in the plan, is not cut by the box.
            double dx = box.Max.X - box.Min.X;
            double dy = box.Max.Y - box.Min.Y;
            double dz = box.Max.Z - box.Min.Z;
            double reach = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            GeoPoint3 least = box.Min;
            GeoPoint3 most = box.Max;

            if (!measured || reach <= Tolerance.Default.EqualPoint)
            {
                reach = WordsReach(entity);
                least = point;
                most = point;
            }

            double grow = 2.0 * reach + 1.0;
            var planes = new List<double>(10) { least.X - grow, least.Y - grow, most.X + grow, most.Y + grow };

            if (overFront)
            {
                planes.Add(-gx / g);
                planes.Add(-gy / g);
                planes.Add((slab.Front.Value - h0) / g);
            }

            if (underBack)
            {
                planes.Add(gx / g);
                planes.Add(gy / g);
                planes.Add((h0 - slab.Back.Value) / g);
            }

            strip = planes.ToArray();
            return true;
        }

        /// <summary>How far the words of a text can reach from its position: as many heights as it has letters, and at least one.</summary>
        private static double WordsReach(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfDBText text:
                    return Math.Max(1, text.TextString?.Length ?? 0) * text.Height * Math.Max(1.0, text.WidthFactor);
                case DxfMText mtext:
                    return Math.Max(Math.Max(1, mtext.Contents?.Length ?? 0) * mtext.TextHeight, mtext.Width);
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// The plane of an entity that lies on one: a point of it and its unit normal, in the block's coordinates. A text, an
        /// MTEXT and a hatch have the plane of their normal, a solid its own, a 3D face the plane of three of its corners.
        /// </summary>
        private static bool TryPlaneOf(DxfEntity entity, out GeoPoint3 point, out GeoVector3 normal)
        {
            point = default;
            normal = default;

            switch (entity)
            {
                case DxfDBText text:
                    normal = text.Normal;
                    point = text.Position;
                    break;
                case DxfMText mtext:
                    normal = mtext.Normal;
                    point = mtext.Location;
                    break;
                case DxfHatch hatch:
                {
                    normal = hatch.Normal;
                    double elevation = hatch.Elevation;
                    point = new GeoPoint3(normal.X * elevation, normal.Y * elevation, normal.Z * elevation);
                    break;
                }

                case DxfSolid solid:
                    normal = solid.Normal;
                    point = solid.GetPointAt(0);
                    break;
                case DxfFace face:
                {
                    GeoPoint3 a = face.GetVertexAt(0);
                    GeoPoint3 b = face.GetVertexAt(1);
                    GeoPoint3 c = face.GetVertexAt(2);
                    GeoPoint3 d = face.GetVertexAt(3);
                    point = a;
                    var toC = new GeoVector3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
                    normal = new GeoVector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z).CrossProduct(toC);

                    if (normal.Length <= Tolerance.Default.EqualPoint * Tolerance.Default.EqualPoint)
                    {
                        normal = toC.CrossProduct(new GeoVector3(d.X - a.X, d.Y - a.Y, d.Z - a.Z));
                    }

                    break;
                }

                default:
                    return false;
            }

            double length = normal.Length;

            if (!normal.IsValid || !(length > 1E-12))
            {
                return false;
            }

            normal = normal.Divide(length);
            return true;
        }

        /// <summary>
        /// The ring of a strip: its box cut by each of its half-planes, in the block's coordinates; null where nothing of the box
        /// is kept.
        /// </summary>
        private static IReadOnlyList<GeoPoint2> KeptRing(double[] strip)
        {
            var ring = new List<GeoPoint2>
            {
                new GeoPoint2(strip[0], strip[1]),
                new GeoPoint2(strip[2], strip[1]),
                new GeoPoint2(strip[2], strip[3]),
                new GeoPoint2(strip[0], strip[3]),
            };

            for (int k = 4; k + 2 < strip.Length && ring.Count > 0; k += 3)
            {
                double a = strip[k];
                double b = strip[k + 1];
                double c = strip[k + 2];
                var kept = new List<GeoPoint2>(ring.Count + 1);

                for (int i = 0; i < ring.Count; i++)
                {
                    GeoPoint2 p = ring[i];
                    GeoPoint2 q = ring[(i + 1) % ring.Count];
                    double dp = a * p.X + b * p.Y + c;
                    double dq = a * q.X + b * q.Y + c;

                    if (dp >= 0.0)
                    {
                        kept.Add(p);
                    }

                    if ((dp >= 0.0) != (dq >= 0.0))
                    {
                        double t = dp / (dp - dq);
                        kept.Add(new GeoPoint2(p.X + t * (q.X - p.X), p.Y + t * (q.Y - p.Y)));
                    }
                }

                ring = kept;
            }

            return ring.Count < 3 ? null : ring;
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
            int wrap = WrapIndex(curve, pieces);

            for (int i = 0; i < pieces.Count; i++)
            {
                DxfCurve piece = pieces[i];
                GeoPoint3 middle = piece.GetPointAtParameter(0.5 * (piece.StartParam + piece.EndParam));
                double h = slab.HeightAt(middle.X, middle.Y, middle.Z);

                if (
                    (!slab.Front.HasValue || h <= slab.Front.Value + tolerance)
                    && (!slab.Back.HasValue || h >= slab.Back.Value - tolerance)
                )
                {
                    if (i == wrap && TrySplitAtSeam(curve, piece, into))
                    {
                        continue;
                    }

                    if (!ReferenceEquals(piece, curve))
                    {
                        RememberCut(piece, curve);
                    }

                    into.Add(piece);
                }
            }
        }

        /// <summary>
        /// The piece of a closed curve cut at the planes that runs on round the place its parameter starts: the last, which
        /// <c>GetSplitCurves</c> takes from the last cut round to the first. -1 for any other curve, and where a cut stands at the
        /// start, so that no piece runs past it.
        /// </summary>
        private static int WrapIndex(DxfCurve curve, IReadOnlyList<DxfCurve> pieces)
        {
            if (pieces.Count < 2 || !curve.Closed)
            {
                return -1;
            }

            DxfCurve last = pieces[pieces.Count - 1];
            GeoPoint3 seam = curve.StartPoint;
            double tolerance = Tolerance.Default.EqualPoint;
            return seam.DistanceTo(last.StartPoint) <= tolerance || seam.DistanceTo(last.EndPoint) <= tolerance
                ? -1
                : pieces.Count - 1;
        }

        /// <summary>
        /// Splits the piece of a closed curve that runs on past the place its parameter starts at that place, and adds the two
        /// halves: AutoCAD 2021 draws the linetype of a closed curve from the start, so that the pattern begins again there
        /// (PNGOUT in plan of a dashed circle, and of a dashed closed polyline, tilted 45 degrees about X, cut by the planes: the
        /// dashes of the piece that holds the start are those of the whole, 156 of 156 pixels and 512 of 512, whose pattern
        /// begins again at the start), where a piece kept whole would carry it across.
        /// </summary>
        /// <returns>False where the piece could not be split, which is then left as it is.</returns>
        private static bool TrySplitAtSeam(DxfCurve curve, DxfCurve piece, List<DxfEntity> into)
        {
            try
            {
                IReadOnlyList<DxfCurve> halves = piece.GetSplitCurves(new[] { curve.StartPoint });

                if (halves.Count != 2)
                {
                    return false;
                }

                foreach (DxfCurve half in halves)
                {
                    RememberCut(half, curve);
                    into.Add(half);
                }

                return true;
            }
            catch (Exception)
            {
                // A piece that cannot be split at the start is drawn whole.
                return false;
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
