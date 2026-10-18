using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// In space, an arc, a circle, a chain of arcs and a loop of arcs collide with another shape exactly where the gap
    /// between them is within the point tolerance. A circle is read as its rim and a loop by its outline, as their
    /// documentation says, so a circle round a shape does not touch it. A straight side half a tolerance off the top of
    /// an arc of radius 50 and facing it used to miss it at a tolerance of 0.001, though a segment coming at it end first
    /// collided; and an arc whose end stood 0.0012 off a square's corner collided with it.
    /// No member measures the distance from a curve in space to anything but a point, so the collision is all there is.
    /// </summary>
    public class Collision3CurvedExactTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A plane in space: an origin, two axes in it and its normal.</summary>
        private sealed class Frame
        {
            public GeoPoint3 O;
            public GeoVector3 X;
            public GeoVector3 Y;
            public GeoVector3 N;

            public GeoPoint3 P(double x, double y, double h = 0) => new GeoPoint3(
                O.X + x * X.X + y * Y.X + h * N.X, O.Y + x * X.Y + y * Y.Y + h * N.Y, O.Z + x * X.Z + y * Y.Z + h * N.Z);

            public GeoVector3 V(double x, double y) => new GeoVector3(x * X.X + y * Y.X, x * X.Y + y * Y.Y, x * X.Z + y * Y.Z);
        }

        /// <summary>The floor z = 0, or a plane turned 0.6 rad about Z and 0.8 about X, through (3000.25, -1500.5, 800.125).</summary>
        private static Frame On(string plane)
        {
            if (plane == "floor")
            {
                return new Frame { O = new GeoPoint3(0, 0, 0), X = GeoVector3.XAxis, Y = GeoVector3.YAxis, N = GeoVector3.ZAxis };
            }

            double ca = Math.Cos(0.6), sa = Math.Sin(0.6), cb = Math.Cos(0.8), sb = Math.Sin(0.8);
            return new Frame
            {
                O = new GeoPoint3(3000.25, -1500.5, 800.125),
                X = new GeoVector3(ca, sa, 0),
                Y = new GeoVector3(-sa * cb, ca * cb, sb),
                N = new GeoVector3(sa * sb, -ca * sb, cb),
            };
        }

        /// <summary>
        /// A curved shape, the point of it nearest the probes and the direction out from it there:
        /// the dome, the upper half of the circle of radius 50 about the origin, top (0, 50), out along Y;
        /// the circle of radius 40 about the origin, (40, 0), out along X;
        /// the chain from (100, 0) to (100, 100) bulging 0.5, its middle at (125, 50), out along X;
        /// the square 100 by 100 with that side bulging, the same.
        /// </summary>
        private static (object Shape, double NearX, double NearY, double OutX, double OutY) Curved(string kind, Frame f)
        {
            switch (kind)
            {
                case "arc": return (GeoArc3.FromThreePoints(f.P(-50, 0), f.P(0, 50), f.P(50, 0)), 0, 50, 0, 1);
                case "circle": return (new GeoCircle3(f.P(0, 0), f.N, 40), 40, 0, 1, 0);
                case "chain":
                    return (new GeoPolylineArc3(new[] { f.P(100, 0), f.P(100, 100) }, new[] { 0.5, 0 }, new[] { f.N, f.N }), 125, 50, 1, 0);
                default:
                    return (new GeoPolygonArc3(new[] { f.P(0, 0), f.P(100, 0), f.P(100, 100), f.P(0, 100) }, new[] { 0, 0.5, 0, 0 },
                        new[] { f.N, f.N, f.N, f.N }), 125, 50, 1, 0);
            }
        }

        /// <summary>
        /// A straight shape whose side faces the curve: its nearest point is s = near + gap out (u), its side running
        /// across the outward line (along v) through s, the rest of it further out: a segment s - 10 v to s + 10 v; a ray
        /// from s - 10 v along v; a polyline s - 10 v, s + 10 v, s + 10 v + 30 u; a polygon, a face and a straight loop on
        /// the square s +- 10 v, s +- 10 v + 20 u; a triangle s - 10 v, s + 10 v, s + 20 u; a block 50 deep under the plane
        /// whose near side passes through s. The height h lifts it off the plane, and the tilt turns the side about the
        /// outward line, its ends rising and falling 10 tilt. "segment end-on" runs from s out along u instead.
        /// </summary>
        private static object Probe(string kind, Frame f, double sx, double sy, double ux, double uy, double h = 0, double tilt = 0)
        {
            double vx = -uy, vy = ux;
            GeoPoint3 S(double along, double aside) => f.P(sx + along * ux + aside * vx, sy + along * uy + aside * vy, h + aside * tilt);

            switch (kind)
            {
                case "segment": return new GeoLine3(S(0, -10), S(0, 10));
                case "ray": return new GeoRay3(S(0, -10), f.V(vx, vy));
                case "polyline": return new GeoPolyline3(S(0, -10), S(0, 10), S(30, 10));
                case "polygon": return new GeoPolygon3(S(0, -10), S(0, 10), S(20, 10), S(20, -10));
                case "triangle": return new GeoTriangle3(S(0, -10), S(0, 10), S(20, 0));
                case "face": return new GeoFace3(new GeoPolygon3(S(0, -10), S(0, 10), S(20, 10), S(20, -10)));
                case "straight loop":
                    return new GeoPolygonArc3(new[] { S(0, -10), S(0, 10), S(20, 10), S(20, -10) }, new[] { 0.0, 0, 0, 0 }, new[] { f.N, f.N, f.N, f.N });
                case "segment end-on": return new GeoLine3(S(0, 0), S(50, 0));
                default:
                {
                    GeoPoint3 c = S(25, 0);
                    var centre = new GeoPoint3(c.X - 25 * f.N.X, c.Y - 25 * f.N.Y, c.Z - 25 * f.N.Z);
                    return new GeoObb3(centre, 50, 40, 50, f.V(ux, uy), f.V(vx, vy));
                }
            }
        }

        /// <summary>Whether a curved shape collides with another shape, at the tolerance, asked of the curved one.</summary>
        private static bool Hits(object curved, object other, Tolerance t)
        {
            switch (curved)
            {
                case GeoArc3 a: return other is GeoLine3 l ? a.CollidesWith(l, t) : other is GeoRay3 r ? a.CollidesWith(r, t) : other is GeoPolyline3 pl ? a.CollidesWith(pl, t) : other is GeoPolygon3 pg ? a.CollidesWith(pg, t) : other is GeoTriangle3 tr ? a.CollidesWith(tr, t) : other is GeoFace3 fc ? a.CollidesWith(fc, t) : other is GeoObb3 ob ? a.CollidesWith(ob, t) : other is GeoAabb3 ab ? a.CollidesWith(ab, t) : other is GeoArc3 ar ? a.CollidesWith(ar, t) : other is GeoCircle3 ci ? a.CollidesWith(ci, t) : other is GeoPolylineArc3 ch ? a.CollidesWith(ch, t) : a.CollidesWith((GeoPolygonArc3)other, t);
                case GeoCircle3 c: return other is GeoLine3 l2 ? c.CollidesWith(l2, t) : other is GeoRay3 r2 ? c.CollidesWith(r2, t) : other is GeoPolyline3 pl2 ? c.CollidesWith(pl2, t) : other is GeoPolygon3 pg2 ? c.CollidesWith(pg2, t) : other is GeoTriangle3 tr2 ? c.CollidesWith(tr2, t) : other is GeoFace3 fc2 ? c.CollidesWith(fc2, t) : other is GeoObb3 ob2 ? c.CollidesWith(ob2, t) : other is GeoAabb3 ab2 ? c.CollidesWith(ab2, t) : other is GeoArc3 ar2 ? c.CollidesWith(ar2, t) : other is GeoCircle3 ci2 ? c.CollidesWith(ci2, t) : other is GeoPolylineArc3 ch2 ? c.CollidesWith(ch2, t) : c.CollidesWith((GeoPolygonArc3)other, t);
                case GeoPolylineArc3 k: return other is GeoLine3 l3 ? k.CollidesWith(l3, t) : other is GeoRay3 r3 ? k.CollidesWith(r3, t) : other is GeoPolyline3 pl3 ? k.CollidesWith(pl3, t) : other is GeoPolygon3 pg3 ? k.CollidesWith(pg3, t) : other is GeoTriangle3 tr3 ? k.CollidesWith(tr3, t) : other is GeoFace3 fc3 ? k.CollidesWith(fc3, t) : other is GeoObb3 ob3 ? k.CollidesWith(ob3, t) : other is GeoAabb3 ab3 ? k.CollidesWith(ab3, t) : other is GeoArc3 ar3 ? k.CollidesWith(ar3, t) : other is GeoCircle3 ci3 ? k.CollidesWith(ci3, t) : other is GeoPolylineArc3 ch3 ? k.CollidesWith(ch3, t) : k.CollidesWith((GeoPolygonArc3)other, t);
                default:
                {
                    var g = (GeoPolygonArc3)curved;
                    return other is GeoLine3 l4 ? g.CollidesWith(l4, t) : other is GeoRay3 r4 ? g.CollidesWith(r4, t) : other is GeoPolyline3 pl4 ? g.CollidesWith(pl4, t) : other is GeoPolygon3 pg4 ? g.CollidesWith(pg4, t) : other is GeoTriangle3 tr4 ? g.CollidesWith(tr4, t) : other is GeoFace3 fc4 ? g.CollidesWith(fc4, t) : other is GeoObb3 ob4 ? g.CollidesWith(ob4, t) : other is GeoAabb3 ab4 ? g.CollidesWith(ab4, t) : other is GeoArc3 ar4 ? g.CollidesWith(ar4, t) : other is GeoCircle3 ci4 ? g.CollidesWith(ci4, t) : other is GeoPolylineArc3 ch4 ? g.CollidesWith(ch4, t) : g.CollidesWith((GeoPolygonArc3)other, t);
                }
            }
        }

        /// <summary>Whether a shape collides with a curved one, at the tolerance, asked of the shape.</summary>
        private static bool HitBy(object shape, object curved, Tolerance t)
        {
            switch (shape)
            {
                case GeoLine3 l: return curved is GeoArc3 a ? l.CollidesWith(a, t) : curved is GeoCircle3 c ? l.CollidesWith(c, t) : curved is GeoPolylineArc3 k ? l.CollidesWith(k, t) : l.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoRay3 r: return curved is GeoArc3 a2 ? r.CollidesWith(a2, t) : curved is GeoCircle3 c2 ? r.CollidesWith(c2, t) : curved is GeoPolylineArc3 k2 ? r.CollidesWith(k2, t) : r.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoPolyline3 p: return curved is GeoArc3 a3 ? p.CollidesWith(a3, t) : curved is GeoCircle3 c3 ? p.CollidesWith(c3, t) : curved is GeoPolylineArc3 k3 ? p.CollidesWith(k3, t) : p.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoPolygon3 g: return curved is GeoArc3 a4 ? g.CollidesWith(a4, t) : curved is GeoCircle3 c4 ? g.CollidesWith(c4, t) : curved is GeoPolylineArc3 k4 ? g.CollidesWith(k4, t) : g.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoTriangle3 tr: return curved is GeoArc3 a5 ? tr.CollidesWith(a5, t) : curved is GeoCircle3 c5 ? tr.CollidesWith(c5, t) : curved is GeoPolylineArc3 k5 ? tr.CollidesWith(k5, t) : tr.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoFace3 fc: return curved is GeoArc3 a6 ? fc.CollidesWith(a6, t) : curved is GeoCircle3 c6 ? fc.CollidesWith(c6, t) : curved is GeoPolylineArc3 k6 ? fc.CollidesWith(k6, t) : fc.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoObb3 ob: return curved is GeoArc3 a7 ? ob.CollidesWith(a7, t) : curved is GeoCircle3 c7 ? ob.CollidesWith(c7, t) : curved is GeoPolylineArc3 k7 ? ob.CollidesWith(k7, t) : ob.CollidesWith((GeoPolygonArc3)curved, t);
                case GeoAabb3 ab: return curved is GeoArc3 a8 ? ab.CollidesWith(a8, t) : curved is GeoCircle3 c8 ? ab.CollidesWith(c8, t) : curved is GeoPolylineArc3 k8 ? ab.CollidesWith(k8, t) : ab.CollidesWith((GeoPolygonArc3)curved, t);
                default: return Hits(shape, curved, t);
            }
        }

        /// <summary>Both ways round, they collide exactly where the gap is within the tolerance.</summary>
        private static void AssertCollides(double gap, object curved, object other)
        {
            bool expected = gap <= Tol.EqualPoint;
            Assert.Equal(expected, Hits(curved, other, Tol));
            Assert.Equal(expected, HitBy(other, curved, Tol));
        }

        private static readonly string[] CurvedKinds = { "arc", "circle", "chain", "loop" };

        #region T5: half a tolerance off, the side facing the curve: they collide

        public static IEnumerable<object[]> SideOnCases()
        {
            foreach (string curved in CurvedKinds)
            {
                foreach (string probe in new[] { "segment", "ray", "polyline", "polygon", "triangle", "face", "block" })
                {
                    yield return new object[] { curved, probe, "floor" };
                }

                foreach (string probe in new[] { "segment", "polygon", "block" })
                {
                    yield return new object[] { curved, probe, "turned" };
                }
            }
        }

        [Theory]
        [MemberData(nameof(SideOnCases))]
        public void AStraightSideHalfAToleranceOffACurveInItsPlane_Collides(string curved, string probe, string plane)
        {
            Frame f = On(plane);
            (object shape, double nx, double ny, double ux, double uy) = Curved(curved, f);
            double h = Tol.EqualPoint / 2;

            AssertCollides(h, shape, Probe(probe, f, nx + h * ux, ny + h * uy, ux, uy));
        }

        [Theory]
        [InlineData("arc")]
        [InlineData("circle")]
        [InlineData("chain")]
        [InlineData("loop")]
        public void ASideHalfAToleranceOutAnd0_0003OffACurvesPlane_Collides(string curved)
        {
            // The side segment half a tolerance out and lifted 0.0003: sqrt(0.0005^2 + 0.0003^2) = 0.00058 from the curve.
            Frame f = On("floor");
            (object shape, double nx, double ny, double ux, double uy) = Curved(curved, f);
            double h = Tol.EqualPoint / 2;

            AssertCollides(Math.Sqrt(h * h + 0.0003 * 0.0003), shape, Probe("segment", f, nx + h * ux, ny + h * uy, ux, uy, 0.0003));
        }

        [Theory]
        [InlineData("arc")]
        [InlineData("circle")]
        [InlineData("chain")]
        [InlineData("loop")]
        public void ASideHalfAToleranceOffACurveTiltedOutOfItsPlane_Collides(string curved)
        {
            // The side segment turned about the outward line by 1E-5 rad, its ends 0.0001 up and down: its middle is nearest.
            Frame f = On("floor");
            (object shape, double nx, double ny, double ux, double uy) = Curved(curved, f);
            double h = Tol.EqualPoint / 2;

            AssertCollides(h, shape, Probe("segment", f, nx + h * ux, ny + h * uy, ux, uy, 0, 1E-5));
        }

        [Theory]
        [InlineData("arc")]
        [InlineData("circle")]
        [InlineData("chain")]
        [InlineData("loop")]
        public void AStraightLoopsSideHalfAToleranceOffACurve_Collides(string curved)
        {
            Frame f = On("floor");
            (object shape, double nx, double ny, double ux, double uy) = Curved(curved, f);
            double h = Tol.EqualPoint / 2;

            AssertCollides(h, shape, Probe("straight loop", f, nx + h * ux, ny + h * uy, ux, uy));
        }

        #endregion

        #region T7: past the tolerance at an arc's end: they do not collide

        /// <summary>
        /// The arc of radius 50 from s = (100 + d, 100 + d), 0.0012021 off the square's corner, sweeping out along X and up
        /// (its centre at (s, s + 50)), with its points lifted by h plus tilt times their reach along (1, 1) / sqrt 2.
        /// </summary>
        private static GeoArc3 OffTheCorner(Frame f, double h, double tilt)
        {
            double s = 100 + (100.00085 - 100);
            GeoPoint3 P(double x, double y) => f.P(x, y, h + tilt * ((x - s) + (y - s)) * Math.Sqrt(0.5));
            return GeoArc3.FromThreePoints(P(s, s), P(s + 50 * Math.Sin(0.5), s + 50 - 50 * Math.Cos(0.5)), P(s + 50, s + 50));
        }

        [Theory]
        [InlineData("arc starting off the square's corner")]
        [InlineData("the same lifted 0.0003")]
        [InlineData("the same tilted up 1E-5 rad")]
        [InlineData("the same lifted 0.0003 off a block's corner")]
        [InlineData("arcs start to start, lifted 0.0003")]
        public void AnArcsEndOneAndAFifthTolerancesOffACorner_DoesNotCollide(string kind)
        {
            Frame f = On("floor");
            var square = new GeoPolygon3(f.P(0, 0), f.P(100, 0), f.P(100, 100), f.P(0, 100));
            double d = 100.00085 - 100;
            double corner = d * Math.Sqrt(2);
            double lifted = Math.Sqrt(corner * corner + 0.0003 * 0.0003);

            switch (kind)
            {
                case "arc starting off the square's corner":
                    AssertCollides(corner, OffTheCorner(f, 0, 0), square);
                    break;

                case "the same lifted 0.0003":
                    AssertCollides(lifted, OffTheCorner(f, 0.0003, 0), square);
                    break;

                case "the same tilted up 1E-5 rad":
                    AssertCollides(corner, OffTheCorner(f, 0, 1E-5), square);
                    break;

                case "the same lifted 0.0003 off a block's corner":
                    AssertCollides(lifted, OffTheCorner(f, 0.0003, 0), new GeoAabb3(new GeoPoint3(0, 0, -50), new GeoPoint3(100, 100, 0)));
                    break;

                default:
                {
                    // The dome leaves its start (-50, 0) going up; the other, the dome turned a half turn about that start and
                    // moved 0.0012 down, leaves (-50, -0.0012) going down, 0.0003 above the plane: sqrt(0.0012^2 + 0.0003^2) apart.
                    object dome = Curved("arc", f).Shape;
                    double g = 0.0012;
                    var other = GeoArc3.FromThreePoints(f.P(-50, -g, 0.0003), f.P(-100, -50 - g, 0.0003), f.P(-150, -g, 0.0003));
                    AssertCollides(Math.Sqrt(g * g + 0.0003 * 0.0003), dome, other);
                    break;
                }
            }
        }

        #endregion

        #region Controls: what already reads right and must stay so

        [Theory]
        [InlineData("segment crossing the arc", "floor")]
        [InlineData("segment tangent to the arc's top", "floor")]
        [InlineData("segment five tolerances off the arc", "floor")]
        [InlineData("circle round a square", "floor")]
        [InlineData("loop round a square", "floor")]
        [InlineData("segment crossing the arc", "turned")]
        [InlineData("segment five tolerances off the arc", "turned")]
        public void ACrossingATangencyAWideGapOrAShapeInsideARim_ReadsAsItDid(string kind, string plane)
        {
            Frame f = On(plane);
            object arc = Curved("arc", f).Shape;

            switch (kind)
            {
                case "segment crossing the arc":
                    AssertCollides(0, arc, new GeoLine3(f.P(0, 0), f.P(0, 100)));
                    break;

                case "segment tangent to the arc's top":
                    AssertCollides(0, arc, new GeoLine3(f.P(-10, 50), f.P(10, 50)));
                    break;

                case "segment five tolerances off the arc":
                    AssertCollides(50.005 - 50, arc, new GeoLine3(f.P(-10, 50.005), f.P(10, 50.005)));
                    break;

                case "circle round a square":
                {
                    // The square 10 by 10 in the middle of the circle of radius 40: 40 - 5 sqrt 2 from its rim, no contact.
                    var square = new GeoPolygon3(f.P(-5, -5), f.P(5, -5), f.P(5, 5), f.P(-5, 5));
                    AssertCollides(40 - 5 * Math.Sqrt(2), Curved("circle", f).Shape, square);
                    break;
                }

                default:
                {
                    // The square 20 by 20 in the middle of the bowed loop: 40 from its outline, no contact.
                    var square = new GeoPolygon3(f.P(40, 40), f.P(60, 40), f.P(60, 60), f.P(40, 60));
                    AssertCollides(40, Curved("loop", f).Shape, square);
                    break;
                }
            }
        }

        [Theory]
        [InlineData("arc")]
        [InlineData("circle")]
        [InlineData("chain")]
        [InlineData("loop")]
        public void ASegmentEndOnHalfAToleranceOffACurve_AlreadyCollides(string curved)
        {
            // Coming at the curve end first, along the outward line: its start is half a tolerance off, and it collides.
            Frame f = On("floor");
            (object shape, double nx, double ny, double ux, double uy) = Curved(curved, f);
            double h = Tol.EqualPoint / 2;

            AssertCollides(h, shape, Probe("segment end-on", f, nx + h * ux, ny + h * uy, ux, uy));
        }

        #endregion
    }
}
