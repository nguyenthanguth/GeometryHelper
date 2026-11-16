using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMBREAK Auto on the arcs of a dimension's picture, the arc of an angular dimension and of an arc length one, as AutoCAD
    /// 2021 breaks them (probed in its console): a gap of the break size along the arc, half either side of each crossing, so
    /// that a line across the arc of an angle of 84.85 at 45 degrees leaves two arcs, from 0.002121 to 0.784662 and from
    /// 0.786135 to 1.568675 radians, and a circle across it at two places leaves three.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>Where along an arc, counter-clockwise from its start in radians, an object crosses it, in order.</summary>
        private static List<double> ArcCrossings(
            GeoPoint2 centre,
            double radius,
            double start,
            double sweep,
            DxfEntity cutter
        )
        {
            var found = new List<double>();

            switch (cutter)
            {
                case DxfLine line:
                    SegmentOnArc(centre, radius, start, sweep, P2(line.StartPoint), P2(line.EndPoint), 0.0, 1.0, found);
                    break;
                case DxfPolyline polyline:
                    int n = polyline.NumberOfVertices;

                    for (int i = 0; i + 1 < n || (polyline.Closed && i < n && n > 2); i++)
                    {
                        SegmentOnArc(
                            centre,
                            radius,
                            start,
                            sweep,
                            polyline.GetPoint2dAt(i),
                            polyline.GetPoint2dAt((i + 1) % n),
                            0.0,
                            1.0,
                            found
                        );
                    }

                    break;
                case DxfCircle circle:
                    CircleOnArc(
                        centre,
                        radius,
                        start,
                        sweep,
                        P2(circle.Center),
                        circle.Radius,
                        0.0,
                        2.0 * Math.PI,
                        found
                    );
                    break;
                case DxfArc other:
                    CircleOnArc(
                        centre,
                        radius,
                        start,
                        sweep,
                        P2(other.Center),
                        other.Radius,
                        other.StartAngle,
                        CadGrips.Sweep(other.StartAngle, other.EndAngle),
                        found
                    );
                    break;
                case DxfEllipse _:
                case DxfSpline _:
                    var curve = (DxfCurve)cutter;
                    const int pieces = 256;
                    double from = curve.StartParam;
                    double step = (curve.EndParam - from) / pieces;
                    GeoPoint2 previous = P2(curve.GetPointAtParameter(from));

                    for (int i = 1; i <= pieces; i++)
                    {
                        GeoPoint2 next = P2(curve.GetPointAtParameter(i == pieces ? curve.EndParam : from + step * i));
                        SegmentOnArc(centre, radius, start, sweep, previous, next, 0.0, 1.0, found);
                        previous = next;
                    }

                    break;
                case DxfXline xline:
                    SegmentOnArc(
                        centre,
                        radius,
                        start,
                        sweep,
                        P2(xline.BasePoint),
                        P2(xline.BasePoint) + new GeoVector2(xline.UnitDir.X, xline.UnitDir.Y),
                        double.NegativeInfinity,
                        double.PositiveInfinity,
                        found
                    );
                    break;
                case DxfRay ray:
                    SegmentOnArc(
                        centre,
                        radius,
                        start,
                        sweep,
                        P2(ray.BasePoint),
                        P2(ray.BasePoint) + new GeoVector2(ray.UnitDir.X, ray.UnitDir.Y),
                        0.0,
                        double.PositiveInfinity,
                        found
                    );
                    break;
            }

            found.Sort();
            return found;
        }

        /// <summary>
        /// Where along an arc the box of words is crossed: the offsets at which the four sides of the box (turned with the words)
        /// meet it, of which the first and the last are where it enters and leaves. Words whose box the arc does not meet give none.
        /// </summary>
        private static List<double> TextOnArc(
            GeoPoint2 centre,
            double radius,
            double start,
            double sweep,
            DxfEntity words
        )
        {
            var found = new List<double>();
            double turn =
                words is DxfDBText text ? text.Rotation
                : words is DxfMText mtext ? mtext.Rotation
                : 0.0;
            GeoPoint3 pivot = words is DxfDBText placed ? placed.Position : ((DxfMText)words).Location;

            try
            {
                GeoAabb3 box;

                if (Math.Abs(Math.IEEERemainder(turn, 2.0 * Math.PI)) > 1E-9)
                {
                    DxfEntity upright = words.Clone();
                    upright.TransformBy(GeoTransform3.RotationAxis(pivot, new GeoVector3(0, 0, 1), -turn));
                    box = upright.GeometricExtents;
                }
                else
                {
                    box = words.GeometricExtents;
                }

                double cos = Math.Cos(turn);
                double sin = Math.Sin(turn);
                GeoPoint2 Corner(double x, double y) =>
                    new GeoPoint2(
                        pivot.X + (x - pivot.X) * cos - (y - pivot.Y) * sin,
                        pivot.Y + (x - pivot.X) * sin + (y - pivot.Y) * cos
                    );
                GeoPoint2[] corners =
                {
                    Corner(box.Min.X, box.Min.Y),
                    Corner(box.Max.X, box.Min.Y),
                    Corner(box.Max.X, box.Max.Y),
                    Corner(box.Min.X, box.Max.Y),
                };

                for (int i = 0; i < 4; i++)
                {
                    SegmentOnArc(centre, radius, start, sweep, corners[i], corners[(i + 1) % 4], 0.0, 1.0, found);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                return new List<double>();
            }

            found.Sort();
            return found;
        }

        /// <summary>Where the line through <paramref name="p"/> and <paramref name="q"/>, from the parameter <paramref name="lo"/> to <paramref name="hi"/> (0 is <paramref name="p"/>, 1 is <paramref name="q"/>), meets a circle, kept where the point falls on the arc.</summary>
        private static void SegmentOnArc(
            GeoPoint2 centre,
            double radius,
            double start,
            double sweep,
            GeoPoint2 p,
            GeoPoint2 q,
            double lo,
            double hi,
            List<double> found
        )
        {
            GeoVector2 d = q - p;
            double a = d.X * d.X + d.Y * d.Y;

            if (a <= 1E-24)
            {
                return;
            }

            GeoVector2 from = p - centre;
            double b = d.X * from.X + d.Y * from.Y;
            double c = from.X * from.X + from.Y * from.Y - radius * radius;
            double disc = b * b - a * c;

            if (disc < 0.0)
            {
                return;
            }

            double root = Math.Sqrt(disc);
            double eps = 1E-9;

            foreach (
                double t in disc <= 1E-18 * a * radius * radius
                    ? new[] { -b / a }
                    : new[] { (-b - root) / a, (-b + root) / a }
            )
            {
                if (t < lo - eps || t > hi + eps)
                {
                    continue;
                }

                GeoPoint2 at = p + d * t;
                AddOnArc(start, sweep, Math.Atan2(at.Y - centre.Y, at.X - centre.X), found);
            }
        }

        /// <summary>Where a circle, or an arc of it from <paramref name="otherStart"/> through <paramref name="otherSweep"/>, meets an arc.</summary>
        private static void CircleOnArc(
            GeoPoint2 centre,
            double radius,
            double start,
            double sweep,
            GeoPoint2 other,
            double otherRadius,
            double otherStart,
            double otherSweep,
            List<double> found
        )
        {
            GeoVector2 between = other - centre;
            double distance = between.Length;

            if (
                distance <= 1E-12
                || distance > radius + otherRadius + 1E-9
                || distance < Math.Abs(radius - otherRadius) - 1E-9
            )
            {
                return;
            }

            double along = (radius * radius - otherRadius * otherRadius + distance * distance) / (2.0 * distance);
            double height = Math.Sqrt(Math.Max(0.0, radius * radius - along * along));
            GeoVector2 way = between / distance;
            GeoPoint2 middle = centre + way * along;
            GeoVector2 across = new GeoVector2(-way.Y, way.X);

            foreach (double sign in height <= 1E-9 ? new[] { 0.0 } : new[] { -1.0, 1.0 })
            {
                GeoPoint2 at = middle + across * (height * sign);
                double onOther = CadGrips.Sweep(otherStart, Math.Atan2(at.Y - other.Y, at.X - other.X));

                if (onOther > otherSweep + 1E-9 && onOther < 2.0 * Math.PI - 1E-9)
                {
                    continue;
                }

                AddOnArc(start, sweep, Math.Atan2(at.Y - centre.Y, at.X - centre.X), found);
            }
        }

        private static void AddOnArc(double start, double sweep, double angle, List<double> found)
        {
            double offset = CadGrips.Sweep(start, angle);

            if (offset > 2.0 * Math.PI - 1E-9)
            {
                offset = 0.0;
            }

            if (offset <= sweep + 1E-9)
            {
                found.Add(Math.Min(offset, sweep));
            }
        }

        /// <summary>Replaces an arc of a picture by the pieces of it the gaps (offsets in radians from its start) leave, each with its look.</summary>
        private static void CutArc(DxfBlockTableRecord picture, DxfArc arc, List<KeyValuePair<double, double>> gaps)
        {
            double sweep = CadGrips.Sweep(arc.StartAngle, arc.EndAngle);
            gaps.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
            var pieces = new List<KeyValuePair<double, double>>();
            double from = 0.0;

            foreach (KeyValuePair<double, double> gap in gaps)
            {
                if (gap.Key > from)
                {
                    pieces.Add(new KeyValuePair<double, double>(from, Math.Min(gap.Key, sweep)));
                }

                from = Math.Max(from, gap.Value);
            }

            if (from < sweep)
            {
                pieces.Add(new KeyValuePair<double, double>(from, sweep));
            }

            foreach (KeyValuePair<double, double> piece in pieces)
            {
                if ((piece.Value - piece.Key) * arc.Radius <= 1E-9)
                {
                    continue;
                }

                picture.AppendEntity(
                    new DxfArc(arc.Center, arc.Radius, arc.StartAngle + piece.Key, arc.StartAngle + piece.Value)
                    {
                        Layer = arc.Layer,
                        Color = arc.Color,
                        LineWeight = arc.LineWeight,
                    }
                );
            }

            arc.Erase();
        }
    }
}
