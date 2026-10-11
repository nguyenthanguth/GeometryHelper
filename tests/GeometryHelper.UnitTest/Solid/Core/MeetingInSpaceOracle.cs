using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Answers whether and where flat shapes, planes and bodies in space meet by plain arithmetic of its own, with no
    /// call into the library's collision, crossing or containment code, so that the tests of those queries are held to
    /// something that cannot share their mistakes. Also holds the shapes those tests are built from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A flat region is its rings, the outline first and then the holes, read filled with the holes taken out, as
    /// <c>DistanceTo</c> reads it. Whether two regions meet is read from dense samples: points 0.25 apart over each
    /// region and along each ring, each measured to the other region exactly. Where they cross is read from a fine
    /// subdivision: each ring edge cut into 2 000 pieces, and a piece kept where its ends fall either side of the other
    /// region's plane, or on it, and the place it crosses lies in that region.
    /// </para>
    /// <para>
    /// A body is held as a box less boxes cut from it, so whether a point is in its material is three comparisons a
    /// box. Its surface is crossed where that changes along a ring edge of the region, found by halving the piece
    /// down to 1E-12; the body's own edges come from its faces.
    /// </para>
    /// </remarks>
    internal static class MeetingInSpaceOracle
    {
        /// <summary>How far apart the samples over a region and along a ring are.</summary>
        internal const double Spacing = 0.25;

        /// <summary>How many pieces each ring edge is cut into.</summary>
        internal const int Pieces = 2000;

        /// <summary>How near two points found by the oracle are named once: the point tolerance.</summary>
        private const double Merge = 1E-3;

        /// <summary>How far a point may lie off a region, a plane or a box and still be on it: exact arithmetic aside, rounding.</summary>
        private const double On = 1E-9;

        #region Arithmetic

        private static double[] V(GeoPoint3 p) => new[] { p.X, p.Y, p.Z };

        private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

        private static double[] Add(double[] a, double[] b, double s) => new[] { a[0] + b[0] * s, a[1] + b[1] * s, a[2] + b[2] * s };

        private static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        private static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        private static double Length(double[] a) => Math.Sqrt(Dot(a, a));

        private static double[] Unit(double[] a)
        {
            double l = Length(a);
            return new[] { a[0] / l, a[1] / l, a[2] / l };
        }

        private static GeoPoint3 P(double[] a) => new GeoPoint3(a[0], a[1], a[2]);

        private static double SegmentDistance(double[] p, double[] a, double[] b)
        {
            double[] ab = Sub(b, a);
            double t = Dot(Sub(p, a), ab) / Dot(ab, ab);
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Length(Sub(p, Add(a, ab, t)));
        }

        #endregion

        #region Flat regions

        /// <summary>A flat region: its rings, outline first, and the frame of its plane.</summary>
        internal sealed class Flat
        {
            internal double[][][] Rings;
            internal double[] Origin;
            internal double[] U;
            internal double[] W;
            internal double[] Normal;
        }

        internal static Flat Of(GeoPolygon3 polygon) => Of(new[] { polygon.Vertices.ToArray() });

        internal static Flat Of(GeoTriangle3 triangle) => Of(new[] { new[] { triangle.A, triangle.B, triangle.C } });

        internal static Flat Of(GeoFace3 face) => Of(new[] { face.Boundary.Vertices.ToArray() }.Concat(face.Holes.Select(h => h.Vertices.ToArray())).ToArray());

        private static Flat Of(GeoPoint3[][] rings)
        {
            double[][][] r = rings.Select(ring => ring.Select(V).ToArray()).ToArray();
            double[][] outline = r[0];

            // Newell's normal of the outline, which every ring shares.
            var n = new double[3];

            for (int i = 0; i < outline.Length; i++)
            {
                double[] a = outline[i];
                double[] b = outline[(i + 1) % outline.Length];
                n[0] += (a[1] - b[1]) * (a[2] + b[2]);
                n[1] += (a[2] - b[2]) * (a[0] + b[0]);
                n[2] += (a[0] - b[0]) * (a[1] + b[1]);
            }

            double[] normal = Unit(n);
            double[] u = Unit(Sub(outline[1], outline[0]));
            u = Unit(Sub(u, Add(new double[3], normal, Dot(u, normal))));

            return new Flat { Rings = r, Origin = outline[0], U = u, W = Cross(normal, u), Normal = normal };
        }

        private static double[] InPlane(Flat f, double[] p)
        {
            double[] d = Sub(p, f.Origin);
            return new[] { Dot(d, f.U), Dot(d, f.W) };
        }

        /// <summary>Even-odd count over every ring: holes count against the outline.</summary>
        private static bool InsideOpen(Flat f, double[] p)
        {
            double[] q = InPlane(f, p);
            bool inside = false;

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    double[] a = InPlane(f, ring[i]);
                    double[] b = InPlane(f, ring[j]);

                    if ((a[1] > q[1]) != (b[1] > q[1]) && q[0] < (b[0] - a[0]) * (q[1] - a[1]) / (b[1] - a[1]) + a[0])
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        private static double ToRings(Flat f, double[] p)
        {
            double best = double.MaxValue;

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    best = Math.Min(best, SegmentDistance(p, ring[i], ring[(i + 1) % ring.Length]));
                }
            }

            return best;
        }

        /// <summary>The distance from a point to the material of a region.</summary>
        internal static double DistanceTo(Flat f, double[] p)
        {
            double height = Dot(Sub(p, f.Origin), f.Normal);
            double[] foot = Add(p, f.Normal, -height);

            return InsideOpen(f, foot) ? Math.Abs(height) : ToRings(f, p);
        }

        /// <summary>The distance from a point to the material of a region.</summary>
        internal static double DistanceTo(Flat f, GeoPoint3 p) => DistanceTo(f, V(p));

        /// <summary>Whether a point lies on the material of a region, its rims included.</summary>
        private static bool OnRegion(Flat f, double[] p, double slack) => DistanceTo(f, p) <= slack;

        /// <summary>Points 0.25 apart over the material and along every ring, the corners included.</summary>
        internal static List<double[]> Samples(Flat f)
        {
            var points = new List<double[]>();

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    double[] a = ring[i];
                    double[] b = ring[(i + 1) % ring.Length];
                    int steps = Math.Max(1, (int)Math.Ceiling(Length(Sub(b, a)) / Spacing));

                    for (int k = 0; k < steps; k++)
                    {
                        points.Add(Add(a, Sub(b, a), (double)k / steps));
                    }
                }
            }

            double[][] q = f.Rings[0].Select(p => InPlane(f, p)).ToArray();
            double u0 = q.Min(p => p[0]), u1 = q.Max(p => p[0]), w0 = q.Min(p => p[1]), w1 = q.Max(p => p[1]);

            for (double u = u0; u <= u1; u += Spacing)
            {
                for (double w = w0; w <= w1; w += Spacing)
                {
                    double[] p = Add(Add(f.Origin, f.U, u), f.W, w);

                    if (InsideOpen(f, p))
                    {
                        points.Add(p);
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// The least distance between the samples of either region and the other region: no less than the gap between
        /// them, and no more than the gap and the reach of the samples.
        /// </summary>
        internal static double SampledGap(Flat a, Flat b)
        {
            double best = double.MaxValue;

            foreach (double[] p in Samples(a))
            {
                best = Math.Min(best, DistanceTo(b, p));
            }

            foreach (double[] p in Samples(b))
            {
                best = Math.Min(best, DistanceTo(a, p));
            }

            return best;
        }

        /// <summary>
        /// Where the ring edges of the first region, and then of the second, cross the other region, found on a fine
        /// subdivision of each edge: in that order, each edge in order along it, each place named once. An edge lying in
        /// the other's plane crosses it nowhere.
        /// </summary>
        internal static GeoPoint3[] EdgeCrossings(Flat a, Flat b)
        {
            var found = new List<double[]>();

            foreach ((Flat from, Flat to) in new[] { (a, b), (b, a) })
            {
                foreach (double[][] ring in from.Rings)
                {
                    for (int i = 0; i < ring.Length; i++)
                    {
                        foreach (double[] p in PiecesCrossing(ring[i], ring[(i + 1) % ring.Length], to.Origin, to.Normal))
                        {
                            if (OnRegion(to, p, 1E-7))
                            {
                                AddOnce(found, p);
                            }
                        }
                    }
                }
            }

            return found.Select(P).ToArray();
        }

        /// <summary>Where each ring edge of a region crosses a plane, on a fine subdivision, ring by ring.</summary>
        internal static GeoPoint3[] PlaneCrossings(Flat f, GeoPlane3 plane)
        {
            var found = new List<double[]>();
            double[] origin = V(plane.Origin);
            double[] normal = Unit(new[] { plane.Normal.X, plane.Normal.Y, plane.Normal.Z });

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    foreach (double[] p in PiecesCrossing(ring[i], ring[(i + 1) % ring.Length], origin, normal))
                    {
                        AddOnce(found, p);
                    }
                }
            }

            return found.Select(P).ToArray();
        }

        /// <summary>The least and greatest height of the samples of a region over a plane.</summary>
        internal static (double Low, double High) SampledHeights(Flat f, GeoPlane3 plane)
        {
            double[] origin = V(plane.Origin);
            double[] normal = Unit(new[] { plane.Normal.X, plane.Normal.Y, plane.Normal.Z });
            List<double> heights = Samples(f).Select(p => Dot(Sub(p, origin), normal)).ToList();

            return (heights.Min(), heights.Max());
        }

        private static IEnumerable<double[]> PiecesCrossing(double[] a, double[] b, double[] origin, double[] normal)
        {
            double ha = Dot(Sub(a, origin), normal);
            double hb = Dot(Sub(b, origin), normal);

            if (Math.Abs(ha) <= On && Math.Abs(hb) <= On)
            {
                yield break;
            }

            double[] ab = Sub(b, a);

            for (int k = 0; k < Pieces; k++)
            {
                double t0 = (double)k / Pieces, t1 = (double)(k + 1) / Pieces;
                double h0 = ha + (hb - ha) * t0, h1 = ha + (hb - ha) * t1;

                if ((h0 <= On && h1 >= -On) || (h0 >= -On && h1 <= On))
                {
                    double t = Math.Abs(h0 - h1) <= 1E-300 ? t0 : t0 + (t1 - t0) * h0 / (h0 - h1);
                    yield return Add(a, ab, Math.Max(t0, Math.Min(t1, t)));
                }
            }
        }

        private static void AddOnce(List<double[]> found, double[] p)
        {
            if (!found.Any(q => Length(Sub(p, q)) <= Merge))
            {
                found.Add(p);
            }
        }

        #endregion

        #region Bodies held as boxes less boxes

        /// <summary>A body held as a box with boxes cut from it.</summary>
        internal sealed class Boxes
        {
            internal GeoPoint3 Min;
            internal GeoPoint3 Max;
            internal (GeoPoint3 Min, GeoPoint3 Max)[] Cuts = new (GeoPoint3, GeoPoint3)[0];

            /// <summary>Whether a point is in the closed material: in the box, and not strictly inside a cut.</summary>
            internal bool InMaterial(double[] p)
            {
                if (!(p[0] >= Min.X - On && p[0] <= Max.X + On && p[1] >= Min.Y - On && p[1] <= Max.Y + On && p[2] >= Min.Z - On && p[2] <= Max.Z + On))
                {
                    return false;
                }

                foreach ((GeoPoint3 lo, GeoPoint3 hi) in Cuts)
                {
                    if (p[0] > lo.X + On && p[0] < hi.X - On && p[1] > lo.Y + On && p[1] < hi.Y - On && p[2] > lo.Z + On && p[2] < hi.Z - On)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Whether any sample of a region is in the material of a body.</summary>
        internal static bool SampledInMaterial(Flat f, Boxes body) => Samples(f).Concat(f.Rings.SelectMany(r => r)).Any(body.InMaterial);

        /// <summary>
        /// Where a region meets the surface of a body: where its ring edges pass in or out of the material, found on a
        /// fine subdivision and halved down to 1E-12, and then where the body's edges, read from the faces of its
        /// material, cross the region. Each place is named once.
        /// </summary>
        internal static GeoPoint3[] BodyCrossings(Flat f, Boxes body, GeoSolid3 material)
        {
            var found = new List<double[]>();

            foreach (double[][] ring in f.Rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    double[] a = ring[i];
                    double[] ab = Sub(ring[(i + 1) % ring.Length], a);
                    bool was = body.InMaterial(a);

                    for (int k = 1; k <= Pieces; k++)
                    {
                        double t1 = (double)k / Pieces;
                        bool now = body.InMaterial(Add(a, ab, t1));

                        if (now != was)
                        {
                            double lo = (double)(k - 1) / Pieces, hi = t1;

                            while (hi - lo > 1E-12)
                            {
                                double mid = 0.5 * (lo + hi);

                                if (body.InMaterial(Add(a, ab, mid)) == was)
                                {
                                    lo = mid;
                                }
                                else
                                {
                                    hi = mid;
                                }
                            }

                            // The point on the material's side: its closed boundary.
                            AddOnce(found, Add(a, ab, was ? lo : hi));
                            was = now;
                        }
                    }
                }
            }

            foreach (GeoFace3 face in material.Faces)
            {
                foreach (GeoPolygon3 ring in new[] { face.Boundary }.Concat(face.Holes))
                {
                    for (int i = 0; i < ring.EdgeCount; i++)
                    {
                        GeoLine3 edge = ring.GetEdgeAt(i);

                        foreach (double[] p in PiecesCrossing(V(edge.StartPoint), V(edge.EndPoint), f.Origin, f.Normal))
                        {
                            if (OnRegion(f, p, 1E-7))
                            {
                                AddOnce(found, p);
                            }
                        }
                    }
                }
            }

            return found.Select(P).ToArray();
        }

        #endregion

        #region Shapes

        /// <summary>A point.</summary>
        internal static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>An axis-aligned rectangle in the plane z = <paramref name="z"/>, its corners anticlockwise from (x0, y0).</summary>
        internal static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z = 0.0)
            => new GeoPolygon3(Pt(x0, y0, z), Pt(x1, y0, z), Pt(x1, y1, z), Pt(x0, y1, z));

        /// <summary>
        /// A rectangle upright in the plane x = <paramref name="x"/>, from (y0, z0) to (y1, z1), its corners in the order
        /// (y0, z0), (y1, z0), (y1, z1), (y0, z1).
        /// </summary>
        internal static GeoPolygon3 Upright(double x, double y0, double z0, double y1, double z1)
            => new GeoPolygon3(Pt(x, y0, z0), Pt(x, y1, z0), Pt(x, y1, z1), Pt(x, y0, z1));

        /// <summary>A rectangle upright in the plane y = <paramref name="y"/>, from (x0, z0) to (x1, z1).</summary>
        internal static GeoPolygon3 Across(double y, double x0, double z0, double x1, double z1)
            => new GeoPolygon3(Pt(x0, y, z0), Pt(x1, y, z0), Pt(x1, y, z1), Pt(x0, y, z1));

        /// <summary>A polygon through its corners.</summary>
        internal static GeoPolygon3 Polygon(params GeoPoint3[] corners) => new GeoPolygon3(corners);

        /// <summary>A face with one hole.</summary>
        internal static GeoFace3 Holed(GeoPolygon3 outline, GeoPolygon3 hole) => new GeoFace3(outline, new[] { hole });

        /// <summary>A triangle.</summary>
        internal static GeoTriangle3 Tri(GeoPoint3 a, GeoPoint3 b, GeoPoint3 c) => new GeoTriangle3(a, b, c);

        /// <summary>
        /// A point turned about an axis through <paramref name="pivot"/> by <paramref name="degrees"/>, by Rodrigues'
        /// formula: so a shape is tilted a known angle off its plane about a known line.
        /// </summary>
        internal static GeoPoint3 Turned(GeoPoint3 point, GeoPoint3 pivot, GeoVector3 axis, double degrees)
        {
            double[] k = Unit(new[] { axis.X, axis.Y, axis.Z });
            double[] v = Sub(V(point), V(pivot));
            double angle = degrees * Math.PI / 180.0;
            double c = Math.Cos(angle), s = Math.Sin(angle);
            double[] turned = Add(Add(Add(new double[3], v, c), Cross(k, v), s), k, Dot(k, v) * (1.0 - c));

            return P(Add(V(pivot), turned, 1.0));
        }

        internal static GeoPolygon3 Turned(GeoPolygon3 polygon, GeoPoint3 pivot, GeoVector3 axis, double degrees)
            => new GeoPolygon3(polygon.Vertices.Select(p => Turned(p, pivot, axis, degrees)));

        internal static GeoTriangle3 Turned(GeoTriangle3 t, GeoPoint3 pivot, GeoVector3 axis, double degrees)
            => new GeoTriangle3(Turned(t.A, pivot, axis, degrees), Turned(t.B, pivot, axis, degrees), Turned(t.C, pivot, axis, degrees));

        internal static GeoFace3 Turned(GeoFace3 f, GeoPoint3 pivot, GeoVector3 axis, double degrees)
            => new GeoFace3(Turned(f.Boundary, pivot, axis, degrees), f.Holes.Select(h => Turned(h, pivot, axis, degrees)));

        /// <summary>A box as a body.</summary>
        internal static GeoSolid3 Block(GeoPoint3 min, GeoPoint3 max) => new GeoAabb3(min, max).ToObb().ToSolid();

        /// <summary>A body held both ways: as the library's solid and as the oracle's box less boxes.</summary>
        internal static (GeoSolid3 Solid, Boxes Held) Body(GeoPoint3 min, GeoPoint3 max, params (GeoPoint3 Min, GeoPoint3 Max)[] cuts)
        {
            GeoSolid3 solid = Block(min, max);

            if (cuts.Length > 0)
            {
                solid = solid.WithOpenings(cuts.Select(c => Block(c.Min, c.Max)));
            }

            return (solid, new Boxes { Min = min, Max = max, Cuts = cuts });
        }

        #endregion
    }
}
