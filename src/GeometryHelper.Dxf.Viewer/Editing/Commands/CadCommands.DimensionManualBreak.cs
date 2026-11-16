using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMBREAK Manual, as AutoCAD 2021 breaks a dimension between two points picked (probed in its console against some 400
    /// breaks of linear, aligned, angular, radius and diameter dimensions).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The line broken is the one nearest the first point: the extension lines (0 the first, 1 the second), the dimension line
    /// or the arc of an angular dimension (3), measured to the stretch of each that the dimension draws, a tie going to the
    /// lesser number. Both points are carried along that line, and the stretch between them is left out, where at least one of
    /// the two falls on the line's drawn stretch (the other held at its end where it falls beyond): a level dimension from
    /// (0, 0) to (100, 0) with its line at y 20 broken from (30, 21) to (40, 25) has its dimension line out from 30 to 40; broken
    /// from (0, 10) to (100, 10) it has its first extension line cut at y 10 and no more, the second point carried onto it. Where
    /// both fall off the stretch, nothing is cut, whichever sides they are. On an arc both must fall within it, else it stays
    /// whole. The centre marks of a radius or a diameter are not lines that break.
    /// </para>
    /// <para>
    /// Where AutoCAD runs in a view, a point picked near a point of the dimension it is attached to (an extension line's origin,
    /// the dimension line's point) is taken for that point, within some pixels of the screen; the presenter takes the points as
    /// they are given, its own snaps being the user's to ask for.
    /// </para>
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>One line of a dimension that DIMBREAK Manual can break: its number (91), what the picture draws of it, and where it runs.</summary>
        private sealed class BreakableLine
        {
            /// <summary>Gets the number AutoCAD keeps for the line (91): 0 and 1 the extension lines, 3 the dimension line or the arc.</summary>
            internal int Index { get; set; }

            /// <summary>Gets or sets a point of a straight line, or the centre of an arc.</summary>
            internal GeoPoint2 Origin { get; set; }

            /// <summary>Gets or sets the unit direction of a straight line.</summary>
            internal GeoVector2 Way { get; set; }

            /// <summary>Gets or sets the radius of an arc; nought for a straight line.</summary>
            internal double Radius { get; set; }

            /// <summary>Gets or sets the angle an arc's offsets are counted from, counter-clockwise.</summary>
            internal double Base { get; set; }

            internal List<DxfLine> Lines { get; } = new List<DxfLine>();

            internal List<DxfArc> Arcs { get; } = new List<DxfArc>();

            /// <summary>Gets or sets where the drawn stretch starts: along the way from the origin, or the offset of the arc's first piece.</summary>
            internal double From { get; set; }

            /// <summary>Gets or sets where the drawn stretch ends.</summary>
            internal double To { get; set; }

            internal bool IsArc => Radius > 0.0;
        }

        /// <summary>
        /// Breaks the picture of a dimension between two points as DIMBREAK Manual does, and gives the number of the line broken
        /// (91): <paramref name="only"/> where one is given (a break read back from a file), else the line nearest
        /// <paramref name="first"/>; 3 for a dimension of a kind whose lines are not known, which is broken at the line of its
        /// picture nearest the middle of the two points.
        /// </summary>
        private static int ManualBreak(
            DxfDimension dimension,
            DxfBlockTableRecord picture,
            GeoPoint2 first,
            GeoPoint2 second,
            int? only,
            double? dimscale = null
        )
        {
            if (picture == null)
            {
                return only ?? 3;
            }

            var shift = new GeoVector2(dimension.DimBlockPosition.X, dimension.DimBlockPosition.Y);
            List<BreakableLine> found = BreakableLinesOf(
                dimension,
                picture.OfType<DxfLine>().Where(Drawn).ToList(),
                picture.OfType<DxfArc>().Where(Drawn).ToList(),
                shift
            );

            if (found.Count == 0)
            {
                BreakAt(dimension, null, new[] { first, second }, picture, dimscale);
                return only ?? 3;
            }

            BreakableLine chosen = null;

            if (only.HasValue)
            {
                chosen = found.FirstOrDefault(f => f.Index == only.Value);
            }
            else
            {
                double best = double.MaxValue;

                foreach (BreakableLine line in found)
                {
                    double distance = DistanceTo(line, first);

                    if (distance < best - 1E-9)
                    {
                        best = distance;
                        chosen = line;
                    }
                }
            }

            if (chosen == null)
            {
                return only ?? 3;
            }

            if (chosen.IsArc)
            {
                BreakArc(picture, chosen, first, second);
            }
            else
            {
                BreakStraight(picture, chosen, shift, first, second);
            }

            return chosen.Index;
        }

        private static bool Drawn(DxfEntity entity) =>
            !entity.IsErased && !string.Equals(entity.Layer, "Defpoints", StringComparison.OrdinalIgnoreCase);

        /// <summary>The lines of a dimension AutoCAD numbers, each with the lines and arcs of the picture that lie on it, in the order 0, 1, 3; those the picture draws nothing of left out.</summary>
        private static List<BreakableLine> BreakableLinesOf(
            DxfDimension dimension,
            List<DxfLine> lines,
            List<DxfArc> arcs,
            GeoVector2 shift
        )
        {
            var found = new List<BreakableLine>();
            double tolerance = Tolerance.Default.EqualPoint;
            double markHalf = 0.0;
            GeoPoint2 centre = default;
            bool radial = false;

            switch (dimension)
            {
                case DxfRotatedDimension _:
                case DxfAlignedDimension _:
                {
                    GeoPoint2 p1;
                    GeoPoint2 p2;
                    GeoPoint2 onLine;

                    if (dimension is DxfRotatedDimension rotated)
                    {
                        p1 = P2(rotated.XLine1Point);
                        p2 = P2(rotated.XLine2Point);
                        onLine = P2(rotated.DimLinePoint);
                    }
                    else
                    {
                        var aligned = (DxfAlignedDimension)dimension;
                        p1 = P2(aligned.XLine1Point);
                        p2 = P2(aligned.XLine2Point);
                        onLine = P2(aligned.DimLinePoint);
                    }

                    double angle = LineAngleOf(dimension);
                    var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
                    double oblique = ObliqueOf(dimension);
                    GeoPoint2 q1 = Foot(p1, onLine, along, oblique);
                    GeoPoint2 q2 = Foot(p2, onLine, along, oblique);
                    var across = new GeoVector2(-along.Y, along.X);
                    found.Add(StraightLine(0, p1, q1 - p1, across));
                    found.Add(StraightLine(1, p2, q2 - p2, across));
                    found.Add(StraightLine(3, q1, q2 - q1, along));
                    break;
                }
                case DxfRadialDimension radius:
                    centre = P2(radius.Center);
                    radial = true;
                    found.Add(StraightLine(3, centre, P2(radius.ChordPoint) - centre, new GeoVector2(1.0, 0.0)));
                    break;
                case DxfDiametricDimension diameter:
                {
                    GeoPoint2 far = P2(diameter.FarChordPoint);
                    GeoPoint2 near = P2(diameter.ChordPoint);
                    centre = new GeoPoint2((far.X + near.X) / 2.0, (far.Y + near.Y) / 2.0);
                    radial = true;
                    found.Add(StraightLine(3, far, near - far, new GeoVector2(1.0, 0.0)));
                    break;
                }
                case DxfPoint3AngularDimension _:
                case DxfLineAngularDimension2 _:
                    AddAngularLines(dimension, found, arcs, shift, tolerance);
                    break;
                default:
                    return found;
            }

            if (radial)
            {
                double scale = dimension.Dimscale > 0.0 ? dimension.Dimscale : 1.0;
                markHalf = Math.Abs(dimension.Dimcen) * scale;
            }

            foreach (DxfLine line in lines)
            {
                GeoPoint2 a = P2(line.StartPoint) + shift;
                GeoPoint2 b = P2(line.EndPoint) + shift;

                // The centre mark of a radius or a diameter is no line of it, though it may lie along it.
                if (
                    radial
                    && (a - centre).Length <= markHalf + tolerance
                    && (b - centre).Length <= markHalf + tolerance
                )
                {
                    continue;
                }

                foreach (BreakableLine candidate in found)
                {
                    if (candidate.IsArc || Math.Abs(Cross(a - candidate.Origin, candidate.Way)) > tolerance)
                    {
                        continue;
                    }

                    if (Math.Abs(Cross(b - candidate.Origin, candidate.Way)) > tolerance)
                    {
                        continue;
                    }

                    candidate.Lines.Add(line);
                    break;
                }
            }

            foreach (BreakableLine candidate in found)
            {
                if (candidate.IsArc || candidate.Lines.Count == 0)
                {
                    continue;
                }

                candidate.From = double.MaxValue;
                candidate.To = double.MinValue;

                foreach (DxfLine line in candidate.Lines)
                {
                    foreach (GeoPoint3 end in new[] { line.StartPoint, line.EndPoint })
                    {
                        double at = Dot(P2(end) + shift - candidate.Origin, candidate.Way);
                        candidate.From = Math.Min(candidate.From, at);
                        candidate.To = Math.Max(candidate.To, at);
                    }
                }
            }

            found.RemoveAll(c => c.Lines.Count == 0 && c.Arcs.Count == 0);
            return found;
        }

        private static BreakableLine StraightLine(int index, GeoPoint2 origin, GeoVector2 toward, GeoVector2 otherwise)
        {
            double length = toward.Length;
            return new BreakableLine
            {
                Index = index,
                Origin = origin,
                Way = length > 1E-9 ? toward / length : otherwise,
            };
        }

        /// <summary>
        /// The two rays and the arc of an angular dimension. The rays are numbered as AutoCAD numbers them: of a vertex and two
        /// points the one the angle is measured from counter-clockwise first; of two lines the one along the second line first.
        /// </summary>
        private static void AddAngularLines(
            DxfDimension dimension,
            List<BreakableLine> found,
            List<DxfArc> arcs,
            GeoVector2 shift,
            double tolerance
        )
        {
            if (
                !AngularRays(
                    dimension,
                    out GeoPoint2 vertex,
                    out GeoPoint2 first,
                    out GeoPoint2 second,
                    out double radius
                )
            )
            {
                return;
            }

            GeoVector2 r0 = first - vertex;
            GeoVector2 r1 = second - vertex;
            r0 /= r0.Length;
            r1 /= r1.Length;

            if (dimension is DxfLineAngularDimension2 two)
            {
                var d2 = new GeoVector2(two.XLine2End.X - two.XLine2Start.X, two.XLine2End.Y - two.XLine2Start.Y);

                if (Math.Abs(Cross(r0, d2)) > Math.Abs(Cross(r1, d2)))
                {
                    GeoVector2 swap = r0;
                    r0 = r1;
                    r1 = swap;
                }
            }

            found.Add(
                new BreakableLine
                {
                    Index = 0,
                    Origin = vertex,
                    Way = r0,
                }
            );
            found.Add(
                new BreakableLine
                {
                    Index = 1,
                    Origin = vertex,
                    Way = r1,
                }
            );
            var arc = new BreakableLine
            {
                Index = 3,
                Origin = vertex,
                Radius = radius,
                Base = Math.Atan2(first.Y - vertex.Y, first.X - vertex.X),
                From = double.MaxValue,
                To = double.MinValue,
            };

            foreach (DxfArc part in arcs)
            {
                if (
                    ((P2(part.Center) + shift) - vertex).Length > tolerance
                    || Math.Abs(part.Radius - radius) > tolerance
                )
                {
                    continue;
                }

                double start = Offset(part.StartAngle, arc.Base);
                arc.Arcs.Add(part);
                arc.From = Math.Min(arc.From, start);
                arc.To = Math.Max(arc.To, start + CadGrips.Sweep(part.StartAngle, part.EndAngle));
            }

            found.Add(arc);
        }

        /// <summary>An angle as counter-clockwise from <paramref name="from"/>, from just under nought up to a turn less that.</summary>
        private static double Offset(double angle, double from)
        {
            double offset = angle - from;
            offset -= Math.Floor(offset / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return offset > 2.0 * Math.PI - 1E-6 ? offset - 2.0 * Math.PI : offset;
        }

        /// <summary>How far a point is from the stretch of a line the dimension draws.</summary>
        private static double DistanceTo(BreakableLine line, GeoPoint2 point)
        {
            if (!line.IsArc)
            {
                return SegmentDistance(line.Origin + line.Way * line.From, line.Origin + line.Way * line.To, point);
            }

            double offset = Offset(Math.Atan2(point.Y - line.Origin.Y, point.X - line.Origin.X), line.Base);

            if (offset >= line.From && offset <= line.To)
            {
                return Math.Abs((point - line.Origin).Length - line.Radius);
            }

            double at = Math.Min((point - OnArc(line, line.From)).Length, (point - OnArc(line, line.To)).Length);
            return at;
        }

        private static GeoPoint2 OnArc(BreakableLine line, double offset) =>
            line.Origin + new GeoVector2(Math.Cos(line.Base + offset), Math.Sin(line.Base + offset)) * line.Radius;

        /// <summary>Leaves out of a straight line the stretch between the feet of two points, where at least one foot falls on the stretch the line is drawn.</summary>
        private static void BreakStraight(
            DxfBlockTableRecord picture,
            BreakableLine line,
            GeoVector2 shift,
            GeoPoint2 first,
            GeoPoint2 second
        )
        {
            double s1 = Dot(first - line.Origin, line.Way);
            double s2 = Dot(second - line.Origin, line.Way);
            bool on1 = s1 >= line.From - 1E-9 && s1 <= line.To + 1E-9;
            bool on2 = s2 >= line.From - 1E-9 && s2 <= line.To + 1E-9;

            if (!on1 && !on2)
            {
                return;
            }

            double lo = Math.Max(Math.Min(s1, s2), line.From);
            double hi = Math.Min(Math.Max(s1, s2), line.To);

            foreach (DxfLine part in line.Lines)
            {
                double a = Dot(P2(part.StartPoint) + shift - line.Origin, line.Way);
                double b = Dot(P2(part.EndPoint) + shift - line.Origin, line.Way);

                if (hi < Math.Min(a, b) - 1E-9 || lo > Math.Max(a, b) + 1E-9)
                {
                    continue;
                }

                KeyValuePair<double, double> gap =
                    b >= a
                        ? new KeyValuePair<double, double>(lo - a, hi - a)
                        : new KeyValuePair<double, double>(a - hi, a - lo);
                Cut(picture, part, new List<KeyValuePair<double, double>> { gap });
            }
        }

        /// <summary>Leaves out of an arc the stretch between the angles of two points, where both fall on the arc as it is drawn.</summary>
        private static void BreakArc(DxfBlockTableRecord picture, BreakableLine line, GeoPoint2 first, GeoPoint2 second)
        {
            double o1 = Offset(Math.Atan2(first.Y - line.Origin.Y, first.X - line.Origin.X), line.Base);
            double o2 = Offset(Math.Atan2(second.Y - line.Origin.Y, second.X - line.Origin.X), line.Base);

            if (o1 < line.From - 1E-9 || o1 > line.To + 1E-9 || o2 < line.From - 1E-9 || o2 > line.To + 1E-9)
            {
                return;
            }

            double lo = Math.Min(o1, o2);
            double hi = Math.Max(o1, o2);

            foreach (DxfArc part in line.Arcs)
            {
                double start = Offset(part.StartAngle, line.Base);
                double end = start + CadGrips.Sweep(part.StartAngle, part.EndAngle);

                if (hi < start - 1E-9 || lo > end + 1E-9)
                {
                    continue;
                }

                var pieces = new List<KeyValuePair<double, double>>();

                if (lo - start > 1E-9)
                {
                    pieces.Add(new KeyValuePair<double, double>(start, Math.Min(lo, end)));
                }

                if (end - hi > 1E-9)
                {
                    pieces.Add(new KeyValuePair<double, double>(Math.Max(hi, start), end));
                }

                foreach (KeyValuePair<double, double> piece in pieces)
                {
                    picture.AppendEntity(
                        new DxfArc(part.Center, part.Radius, line.Base + piece.Key, line.Base + piece.Value)
                        {
                            Layer = part.Layer,
                            Color = part.Color,
                            LineWeight = part.LineWeight,
                        }
                    );
                }

                part.Erase();
            }
        }
    }
}
