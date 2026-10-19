using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The measures of a loop, taken from the exact numbers of its vertices and bulges.
    /// </summary>
    internal static class LoopMath
    {
        /// <summary>
        /// Gets the vector area of a loop of straight edges: the normal by the right-hand rule, as long as the area. Measured from the first
        /// vertex, so that a loop far from the origin keeps its digits.
        /// </summary>
        internal static Vec3 Newell(Vec3[] points)
        {
            if (points.Length < 3)
            {
                return default(Vec3);
            }

            Vec3 origin = points[0];
            Vec3 sum = default(Vec3);

            for (int k = 1; k + 1 < points.Length; k++)
            {
                sum = sum + Vec3.Cross(points[k] - origin, points[k + 1] - origin);
            }

            return sum * 0.5;
        }

        /// <summary>
        /// Gets the area between an arc of a bulge and its chord, positive for a positive bulge. A bulge is the tangent of a quarter of the
        /// angle the arc turns through.
        /// </summary>
        internal static double SegmentArea(double chord, double bulge)
        {
            if (bulge == 0.0 || !(chord > 0.0))
            {
                return 0.0;
            }

            double a = Math.Abs(bulge);
            double theta = 4.0 * Math.Atan(a);
            double radius = chord * (1.0 + a * a) / (4.0 * a);

            // theta - sin(theta) loses its digits for a small angle, where its series does not.
            double lost = theta < 0.1
                ? theta * theta * theta / 6.0 * (1.0 - theta * theta / 20.0 * (1.0 - theta * theta / 42.0))
                : theta - Math.Sin(theta);

            return Math.Sign(bulge) * radius * radius * 0.5 * lost;
        }

        /// <summary>
        /// Gets the signed area of a closed loop about its own normal, arcs included, and that normal: positive where the loop runs
        /// counter-clockwise seen from the normal. In the plane the normal is +Z, so a clockwise loop has a negative area.
        /// </summary>
        internal static double SignedArea(LoopData loop, out Vec3 normal)
        {
            Vec3[] p = loop.Points;
            double area;

            if (loop.Dimension == 2)
            {
                normal = new Vec3(0.0, 0.0, 1.0);
                area = Newell(p).Z;
            }
            else
            {
                Vec3 vector = Newell(p);
                double length = vector.Length;
                normal = length > 0.0 ? vector * (1.0 / length) : default(Vec3);
                area = length;
            }

            if (loop.HasArcs)
            {
                for (int k = 0; k < loop.EdgeCount; k++)
                {
                    double bulge = k < loop.Bulges.Length ? loop.Bulges[k] : 0.0;

                    if (bulge == 0.0)
                    {
                        continue;
                    }

                    double side = loop.Dimension == 2 ? 1.0 : loop.Normals != null && k < loop.Normals.Length ? Math.Sign(Vec3.Dot(loop.Normals[k], normal)) : 0.0;
                    area += side * SegmentArea((p[(k + 1) % p.Length] - p[k]).Length, bulge);
                }
            }

            return area;
        }

        internal static double LongestChord(LoopData loop)
        {
            double longest = 0.0;

            for (int k = 0; k < loop.EdgeCount; k++)
            {
                longest = Math.Max(longest, (loop.Points[(k + 1) % loop.Points.Length] - loop.Points[k]).Length);
            }

            return longest;
        }

        internal static double Perimeter(LoopData loop)
        {
            double sum = 0.0;

            for (int k = 0; k < loop.EdgeCount; k++)
            {
                sum += (loop.Points[(k + 1) % loop.Points.Length] - loop.Points[k]).Length;
            }

            return sum;
        }

        /// <summary>
        /// Gets the unit direction of travel along a curve at one end.
        /// </summary>
        internal static Vec3 Tangent(SceneCurve curve, bool atEnd)
        {
            if (curve.Shape == CurveShape.Segment)
            {
                return (curve.End - curve.Start).Normalized();
            }

            return ConicTangent(curve, curve.StartAngle + (atEnd ? curve.Sweep : 0.0));
        }

        /// <summary>
        /// Gets the unit direction of travel along a curve at its middle.
        /// </summary>
        internal static Vec3 MiddleTangent(SceneCurve curve)
            => curve.Shape == CurveShape.Segment ? (curve.End - curve.Start).Normalized() : ConicTangent(curve, curve.StartAngle + curve.Sweep * 0.5);

        private static Vec3 ConicTangent(SceneCurve curve, double t)
        {
            Vec3 d = curve.AxisV * (curve.RadiusV * Math.Cos(t)) - curve.AxisU * (curve.RadiusU * Math.Sin(t));
            return (curve.Sweep >= 0.0 ? d : -d).Normalized();
        }

        /// <summary>
        /// Gets the box round a curve, exactly: the ends, and for a conic each place along an axis where it turns round.
        /// </summary>
        internal static void CurveBox(SceneCurve curve, out Vec3 min, out Vec3 max)
        {
            Vec3 a = curve.Start;
            Vec3 b = curve.End;
            double x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y), z0 = Math.Min(a.Z, b.Z);
            double x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y), z1 = Math.Max(a.Z, b.Z);

            if (curve.Shape == CurveShape.Conic)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    double u = axis == 0 ? curve.AxisU.X : axis == 1 ? curve.AxisU.Y : curve.AxisU.Z;
                    double v = axis == 0 ? curve.AxisV.X : axis == 1 ? curve.AxisV.Y : curve.AxisV.Z;
                    double t0 = Math.Atan2(curve.RadiusV * v, curve.RadiusU * u);

                    for (int turn = 0; turn < 2; turn++)
                    {
                        double t = t0 + turn * Math.PI;

                        if (!curve.ContainsParameter(t, 1E-9))
                        {
                            continue;
                        }

                        Vec3 p = curve.PointAt(t);
                        x0 = Math.Min(x0, p.X);
                        y0 = Math.Min(y0, p.Y);
                        z0 = Math.Min(z0, p.Z);
                        x1 = Math.Max(x1, p.X);
                        y1 = Math.Max(y1, p.Y);
                        z1 = Math.Max(z1, p.Z);
                    }
                }
            }

            min = new Vec3(x0, y0, z0);
            max = new Vec3(x1, y1, z1);
        }

        /// <summary>
        /// Gets any unit vector square to a unit vector.
        /// </summary>
        internal static Vec3 Perpendicular(Vec3 unit)
        {
            Vec3 helper = Math.Abs(unit.X) < 0.6 ? new Vec3(1.0, 0.0, 0.0) : new Vec3(0.0, 1.0, 0.0);
            return Vec3.Cross(unit, helper).Normalized();
        }

        /// <summary>
        /// Draws an arrow from one point to another into an overlay: the shaft, and a head of two barbs a quarter of its length.
        /// </summary>
        internal static void Arrow(DiagnosticOverlay overlay, string color, double thickness, Vec3 from, Vec3 to, Vec3 side)
        {
            Vec3 d = to - from;
            double length = d.Length;

            if (!(length > 0.0) || side.Length == 0.0)
            {
                return;
            }

            Vec3 dir = d * (1.0 / length);
            double h = length * 0.25;
            overlay.Line(color, thickness, from, to);
            overlay.Line(color, thickness, to - dir * h + side * (h * 0.5), to, to - dir * h - side * (h * 0.5));
        }
    }
}
