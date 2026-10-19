using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// One valid shape of every kind a scene holds, made of random numbers that have many digits, so that a number written
    /// short or read back through a float shows.
    /// </summary>
    internal static class SampleShapes
    {
        /// <summary>
        /// Gets every kind that is a library shape, in the order of <see cref="SceneKinds.All"/>.
        /// </summary>
        internal static IEnumerable<string> Kinds => SceneKinds.All.Where(kind => kind != SceneKinds.Text);

        internal static IEnumerable<object[]> KindData => Kinds.Select(kind => new object[] { kind });

        /// <summary>
        /// Makes the shape of a kind from a seed.
        /// </summary>
        internal static object Make(string kind, int seed)
        {
            var random = new Random(seed);
            double R(double scale) => (random.NextDouble() * 2.0 - 1.0) * scale;
            GeoPoint2 P2() => new GeoPoint2(R(5000.0), R(5000.0));
            GeoPoint3 P3() => new GeoPoint3(R(5000.0), R(5000.0), R(5000.0));
            GeoVector3 UnitVector() => new GeoVector3(R(1.0) + 1.5, R(1.0), R(1.0)).Normalize();

            switch (kind)
            {
                case "GeoPoint2": return new GeoPoint2(0.1, R(1E-3) + 1E-300);
                case "GeoPoint3": return new GeoPoint3(0.1 + R(1E-9), 1E-300, 123456789.12345678);
                case "GeoLine2": return new GeoLine2(P2(), P2());
                case "GeoLine3": return new GeoLine3(P3(), P3());
                case "GeoRay2": return new GeoRay2(P2(), new GeoVector2(R(1.0) + 1.5, R(1.0)));
                case "GeoRay3": return new GeoRay3(P3(), UnitVector());
                case "GeoPolyline2": return new GeoPolyline2(Enumerable.Range(0, 6).Select(i => new GeoPoint2(i * 400.0 + R(100.0), R(900.0))));
                case "GeoPolyline3": return new GeoPolyline3(Enumerable.Range(0, 6).Select(i => new GeoPoint3(i * 400.0 + R(100.0), R(900.0), R(900.0))));
                case "GeoPolygon2": return new GeoPolygon2(Convex2(random, 7));
                case "GeoPolygon3": return new GeoPolygon3(InPlane(random, Convex2(random, 7)));
                case "GeoPolylineArc2": return new GeoPolylineArc2(Enumerable.Range(0, 6).Select(i => new GeoPoint2(i * 400.0 + R(100.0), R(900.0))), Enumerable.Range(0, 6).Select(i => R(1.0)));
                case "GeoPolygonArc2": return new GeoPolygonArc2(Convex2(random, 7), Enumerable.Range(0, 7).Select(i => R(0.2)));

                case "GeoPolylineArc3":
                {
                    var points = Enumerable.Range(0, 5).Select(i => new GeoPoint3(i * 400.0 + R(100.0), R(900.0), 250.0)).ToList();
                    return new GeoPolylineArc3(points, new[] { 0.3 + R(0.1), 0.0, -0.4 + R(0.1), 0.2 }, new[] { new GeoVector3(0.0, 0.0, 1.0), default(GeoVector3), new GeoVector3(0.0, 0.0, 1.0), new GeoVector3(0.0, 0.0, 1.0) });
                }

                case "GeoPolygonArc3":
                {
                    var corners = Convex2(random, 6).Select(p => new GeoPoint3(p.X, p.Y, 250.0)).ToList();
                    var up = new GeoVector3(0.0, 0.0, 1.0);
                    return new GeoPolygonArc3(corners, new[] { 0.3 + R(0.1), 0.0, -0.4 + R(0.1), 0.2, 0.0, 0.1 }, new[] { up, default(GeoVector3), up, up, default(GeoVector3), up });
                }

                case "GeoArc2": return new GeoArc2(P2(), 100.0 + R(50.0), R(3.0), R(3.0) + 6.5);
                case "GeoArc3": return new GeoArc3(P3(), UnitVector(), 100.0 + R(50.0), R(3.0), R(3.0) + 6.5);
                case "GeoCircle2": return new GeoCircle2(P2(), 100.0 + R(50.0));
                case "GeoCircle3": return new GeoCircle3(P3(), UnitVector(), 100.0 + R(50.0));
                case "GeoEllipse2": return new GeoEllipse2(P2(), new GeoVector2(R(1.0) + 1.5, R(1.0)), 200.0 + R(50.0), 100.0 + R(50.0));
                case "GeoEllipse3":
                {
                    GeoVector3 normal = UnitVector();
                    return new GeoEllipse3(P3(), normal, normal.GetPerpendicularVector(), 200.0 + R(50.0), 100.0 + R(50.0));
                }

                case "GeoRectangle2": return new GeoRectangle2(P2(), 300.0 + R(50.0), 200.0 + R(50.0), R(3.0));
                case "GeoTriangle2": return new GeoTriangle2(P2(), P2(), P2());
                case "GeoTriangle3": return new GeoTriangle3(P3(), P3(), P3());

                case "GeoFace2":
                    return new GeoFace2(
                        new GeoPolygon2(new GeoPoint2(0.1, 0.2), new GeoPoint2(100.1 + R(1.0), 0.2), new GeoPoint2(100.1, 100.2 + R(1.0)), new GeoPoint2(0.1, 100.2)),
                        new[] { new GeoPolygon2(new GeoPoint2(20.1, 20.2), new GeoPoint2(40.1, 20.2), new GeoPoint2(40.1, 40.2), new GeoPoint2(20.1, 40.2)) });

                case "GeoFace3":
                    return new GeoFace3(
                        new GeoPolygon3(InPlane(random, new[] { new GeoPoint2(0.1, 0.2), new GeoPoint2(100.1, 0.2), new GeoPoint2(100.1, 100.2), new GeoPoint2(0.1, 100.2) })),
                        null);

                case "GeoSolid3": return Box(30.0 + R(1.0), 20.0, 10.0, true);
                case "GeoMesh2": return new GeoPolygon2(LShape()).ToMesh(MeshKind.Convex);
                case "GeoMesh3": return new GeoPolygon3(LShape().Select(p => new GeoPoint3(p.X, p.Y, 7.5))).ToMesh(MeshKind.Convex);
                case "GeoAabb3": return new GeoAabb3(P3(), P3());
                case "GeoObb3": return new GeoObb3(P3(), 300.0 + R(10.0), 200.0 + R(10.0), 100.0 + R(10.0), UnitVector(), new GeoVector3(0.0, 0.0, 1.0));
                case "GeoPlane3": return new GeoPlane3(P3(), UnitVector());
                default: throw new ArgumentException("No sample for " + kind);
            }
        }

        /// <summary>
        /// Makes a box with one corner at the origin; with an opening, a through hole 10 by 10 in the middle of its top.
        /// </summary>
        internal static GeoSolid3 Box(double x, double y, double z, bool withOpening)
        {
            GeoPolygon3 floor = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(x, 0, 0), new GeoPoint3(x, y, 0), new GeoPoint3(0, y, 0));
            GeoSolid3 body = GeoSolid3.Extrude(floor, new GeoVector3(0, 0, z));

            if (!withOpening)
            {
                return body;
            }

            GeoPolygon3 cutterFloor = new GeoPolygon3(new GeoPoint3(10, 5, -1), new GeoPoint3(20, 5, -1), new GeoPoint3(20, 15, -1), new GeoPoint3(10, 15, -1));
            return body.WithOpenings(new[] { GeoSolid3.Extrude(cutterFloor, new GeoVector3(0, 0, z + 2.0)) });
        }

        internal static GeoPoint2[] LShape()
            => new[] { new GeoPoint2(0, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 20), new GeoPoint2(20, 20), new GeoPoint2(20, 60), new GeoPoint2(0, 60) };

        private static List<GeoPoint2> Convex2(Random random, int count)
        {
            double centerX = (random.NextDouble() - 0.5) * 2000.0;
            double centerY = (random.NextDouble() - 0.5) * 2000.0;
            var points = new List<GeoPoint2>();

            for (int i = 0; i < count; i++)
            {
                double angle = 2.0 * Math.PI * (i + 0.3 * random.NextDouble()) / count;
                double radius = 400.0 + 100.0 * random.NextDouble();
                points.Add(new GeoPoint2(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle)));
            }

            return points;
        }

        private static List<GeoPoint3> InPlane(Random random, IEnumerable<GeoPoint2> flat)
        {
            GeoVector3 normal = new GeoVector3(random.NextDouble() + 0.5, random.NextDouble() - 0.5, random.NextDouble() + 0.5).Normalize();
            GeoPlane3 plane = new GeoPlane3(new GeoPoint3(100.0, 200.0, 300.0), normal);
            plane.GetAxes(out GeoVector3 u, out GeoVector3 v);

            return flat.Select(p => plane.Origin.Add(u.Multiply(p.X)).Add(v.Multiply(p.Y))).ToList();
        }
    }
}
