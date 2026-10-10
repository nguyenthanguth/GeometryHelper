using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decision 6 for the queries of decision 18: an ellipse whose radii are equal is a circle, and every query answers as
    /// <see cref="GeoCircle3"/> does, the same bool and the same crossings in the same order, each to 1E-12 of the radius,
    /// whether it is made by <see cref="GeoEllipse3.FromCircle"/> or has its major axis turned 0.4 rad from the circle's
    /// zero direction. The circle is the one of the R1 tests, radius 250 about (100, 200, 300) with normal (1, -1, 2), and
    /// each shape is placed in its frame (u, v from <see cref="GeoPlane3.GetAxes"/>, h along the normal) so that it
    /// crosses, touches, lies on, or misses the rim clear of every tolerance band. A touch, found from a nearly double
    /// root by different arithmetic in the two, is held to 1E-6 along the rim. Planes a hair apart are left to the curve
    /// tests: there the circle itself is only good to 1.2E-5, and worse far from the origin. Where the circle is wrong
    /// (a long segment near a tangent, a segment almost square through the rim, a touch from outside) the plane-and-line
    /// and curve tests hold the ellipse to the answer instead.
    /// </summary>
    public class Ellipse3QueryAsCircleTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const double R = 250;

        /// <summary>Decision 6: to 1E-12 relative, of the radius.</summary>
        private const double Agree = 1E-12 * R;

        /// <summary>A touch is found from a nearly double root: its place along the rim to 1E-6.</summary>
        private const double AgreeAtATouch = 1E-6;

        private static readonly GeoCircle3 Circle = new GeoCircle3(new GeoPoint3(100, 200, 300), new GeoVector3(1, -1, 2), R);

        /// <summary>The circle's own frame, in which every shape here is placed.</summary>
        private static Specimen Frame()
        {
            Circle.GetPlane().GetAxes(out GeoVector3 u, out GeoVector3 v);
            return new Specimen(Circle.Center, u, v, R, R);
        }

        /// <summary>The circle made an ellipse twice: by FromCircle, and with its major axis turned 0.4 rad.</summary>
        private static IEnumerable<GeoEllipse3> AsEllipses()
        {
            Specimen f = Frame();
            yield return GeoEllipse3.FromCircle(Circle);
            yield return new GeoEllipse3(Circle.Center, Circle.Normal, f.U.Multiply(Math.Cos(0.4)).Add(f.V.Multiply(Math.Sin(0.4))), R, R);
        }

        /// <summary>A circle of the frame: about (u, v, h) with the frame's normal, or about a point with another normal.</summary>
        private static GeoCircle3 InFrame(double u, double v, double h, double radius) => new GeoCircle3(Frame().At(u, v, h), Frame().N, radius);

        private static GeoPolygon3 FramePolygon(double h, params double[] uv)
        {
            Specimen f = Frame();
            var points = new GeoPoint3[uv.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = f.At(uv[2 * i], uv[2 * i + 1], h);
            }

            return new GeoPolygon3(points);
        }

        private static GeoTriangle3 FrameTriangle(double h, params double[] uv)
        {
            GeoPolygon3 p = FramePolygon(h, uv);
            return new GeoTriangle3(p.Vertices[0], p.Vertices[1], p.Vertices[2]);
        }

        /// <summary>The chord between the rim points at 0.7 and 3.9 of the frame, and the leaning plane through it.</summary>
        private static GeoVector3 LeaningNormal(out GeoPoint3 first, out GeoPoint3 second, double degrees = 50)
        {
            Specimen f = Frame();
            first = f.Rim(0.7);
            second = f.Rim(3.9);
            return LeanAbout(f, first.GetVectorTo(second), degrees * Math.PI / 180);
        }

        /// <summary>
        /// Asserts the same crossings in the same order, to <see cref="Agree"/>; a touch, whose place along the rim the
        /// two find by different arithmetic from a double root, to <see cref="AgreeAtATouch"/>.
        /// </summary>
        private static void Same(string what, GeoPoint3[] circle, GeoPoint3[] ellipse)
        {
            double within = what.Contains("tangent") || what.Contains("touch") ? AgreeAtATouch : Agree;
            if (circle.Length != ellipse.Length)
            {
                Assert.Fail($"{what}: the circle gives {circle.Length} points, the ellipse {ellipse.Length}");
            }

            for (int i = 0; i < circle.Length; i++)
            {
                Assert.True(circle[i].DistanceTo(ellipse[i]) <= within, $"{what}, point {i}: circle {circle[i]}, ellipse {ellipse[i]}");
            }
        }

        private static void Alike(string what, bool circle, bool ellipse)
        {
            if (circle != ellipse)
            {
                Assert.Fail($"{what}: the circle touches {circle}, the ellipse {ellipse}");
            }
        }

        private static void Same(string what, bool circleTried, GeoPoint3[] circleOut, bool ellipseTried, GeoPoint3[] ellipseOut)
        {
            if (circleTried != ellipseTried)
            {
                Assert.Fail($"{what}: the circle tries {circleTried}, the ellipse {ellipseTried}");
            }

            Same(what, circleOut, ellipseOut);
        }

        [Fact]
        public void APlaneIsAnsweredAsTheCircleAnswersIt()
        {
            Specimen f = Frame();
            GeoVector3 normal = LeaningNormal(out GeoPoint3 first, out _);
            GeoVector3 tangent = Tangent(f, 1.2);
            var planes = new Dictionary<string, GeoPlane3>
            {
                ["leaning through a chord"] = new GeoPlane3(first, normal),
                ["through a tangent"] = new GeoPlane3(f.Rim(1.2), LeanAbout(f, tangent, 40 * Math.PI / 180)),
                ["five thousandths clear"] = new GeoPlane3(f.Rim(1.2).Add(tangent.CrossProduct(f.N).Multiply(0.005)), LeanAbout(f, tangent, 40 * Math.PI / 180)),
                ["parallel"] = new GeoPlane3(f.At(10, 10, 5), f.N),
                ["its own"] = new GeoPlane3(f.At(100, 30, 0), f.N.Negate()),
                ["across the centre"] = new GeoPlane3(f.Center, f.U),
            };

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoPlane3> plane in planes)
                {
                    Alike(plane.Key, Circle.CollidesWith(plane.Value, Tol), ellipse.CollidesWith(plane.Value, Tol));
                    Same(plane.Key, Circle.GetIntersections(plane.Value, Tol), ellipse.GetIntersections(plane.Value, Tol));
                    bool c = Circle.TryIntersectWith(plane.Value, out GeoPoint3[] co, Tol);
                    bool e = ellipse.TryIntersectWith(plane.Value, out GeoPoint3[] eo, Tol);
                    Same(plane.Key, c, co, e, eo);
                }
            }
        }

        [Fact]
        public void ASegmentAndARayAreAnsweredAsTheCircleAnswersThem()
        {
            Specimen f = Frame();
            GeoPoint3 rim = f.Rim(2.0);
            GeoVector3 lean = f.N.Add(f.U.Multiply(0.3)).Normalize();
            GeoVector3 tangent = Tangent(f, 1.0);
            double across = Math.Sqrt(R * R - 50 * 50);
            var lines = new Dictionary<string, GeoLine3>
            {
                ["a chord"] = new GeoLine3(f.At(-400, 50, 0), f.At(400, 50, 0)),
                ["a chord turned round"] = new GeoLine3(f.At(400, 50, 0), f.At(-400, 50, 0)),
                ["through the disc"] = new GeoLine3(f.At(30, -20, -50), f.At(30, -20, 50)),
                ["through a rim point"] = new GeoLine3(rim.Add(lean.Multiply(-40)), rim.Add(lean.Multiply(40))),
                ["outside"] = new GeoLine3(f.At(400, 0, -50), f.At(400, 0, 50)),
                ["along a tangent"] = new GeoLine3(f.Rim(1.0).Add(tangent.Multiply(-200)), f.Rim(1.0).Add(tangent.Multiply(200))),
                ["inside"] = new GeoLine3(f.At(-200, 50, 0), f.At(200, 50, 0)),
                ["from the centre to the rim"] = new GeoLine3(f.Center, f.Rim(0.5)),
                ["five above"] = new GeoLine3(f.At(-400, 50, 5), f.At(400, 50, 5)),
                ["half way out"] = new GeoLine3(f.At(0, 50, 0), f.At(across + 100, 50, 0)),
            };
            var rays = new Dictionary<string, GeoRay3>
            {
                ["from outside"] = new GeoRay3(f.At(-400, 50, 0), f.U),
                ["from inside"] = new GeoRay3(f.At(0, 50, 0), f.U),
                ["away"] = new GeoRay3(f.At(400, 50, 0), f.U),
                ["up through the disc"] = new GeoRay3(f.At(30, -20, -50), f.N),
                ["up through a rim point"] = new GeoRay3(rim.Add(lean.Multiply(-40)), lean),
                ["down from a rim point"] = new GeoRay3(rim.Add(lean.Multiply(-40)), lean.Negate()),
            };

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoLine3> line in lines)
                {
                    Alike(line.Key, Circle.CollidesWith(line.Value, Tol), ellipse.CollidesWith(line.Value, Tol));
                    Same(line.Key, Circle.GetIntersections(line.Value, Tol), ellipse.GetIntersections(line.Value, Tol));
                    bool c = Circle.TryIntersectWith(line.Value, out GeoPoint3[] co, Tol);
                    bool e = ellipse.TryIntersectWith(line.Value, out GeoPoint3[] eo, Tol);
                    Same(line.Key, c, co, e, eo);
                }

                foreach (KeyValuePair<string, GeoRay3> ray in rays)
                {
                    Alike(ray.Key, Circle.CollidesWith(ray.Value, Tol), ellipse.CollidesWith(ray.Value, Tol));
                    Same(ray.Key, Circle.GetIntersections(ray.Value, Tol), ellipse.GetIntersections(ray.Value, Tol));
                    bool c = Circle.TryIntersectWith(ray.Value, out GeoPoint3[] co, Tol);
                    bool e = ellipse.TryIntersectWith(ray.Value, out GeoPoint3[] eo, Tol);
                    Same(ray.Key, c, co, e, eo);
                }
            }
        }

        /// <summary>Circles about the circle: across, in its plane, on it, inside it, a hair inside it, and touching the line of the planes. Not a hair outside it: there the circle gives nothing, see the curve tests.</summary>
        private static Dictionary<string, GeoCircle3> Circles()
        {
            Specimen f = Frame();
            GeoVector3 normal = LeaningNormal(out GeoPoint3 first, out GeoPoint3 second);
            GeoPoint3 touch = f.Rim(1.1);
            GeoVector3 line = Tangent(f, 1.1);
            GeoVector3 touchNormal = LeanAbout(f, line, 65 * Math.PI / 180);
            GeoVector3 inTouchPlane = touchNormal.CrossProduct(line);
            return new Dictionary<string, GeoCircle3>
            {
                ["across, through two rim points"] = CircleThroughTwo(first, second, normal, 30),
                ["in the plane, apart"] = InFrame(150, 40, 0, 200),
                ["the same, turned over"] = new GeoCircle3(Circle.Center, Circle.Normal.Negate(), R),
                ["inside, about the centre"] = InFrame(0, 0, 0, 150),
                ["a touch four ten-thousandths inside"] = InFrame(0, 150 - 0.0004, 0, 100),
                ["well outside"] = InFrame(700, 0, 0, 100),
                ["across, touching the line of the planes"] = new GeoCircle3(touch.Add(inTouchPlane.Multiply(80)), touchNormal, 80),
            };
        }

        [Fact]
        public void ACircleAndAnEllipseThatIsOneAreAnsweredAsTheCircleAnswersACircle()
        {
            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoCircle3> other in Circles())
                {
                    GeoPoint3[] expected = Circle.GetIntersections(other.Value, Tol);
                    bool collides = Circle.CollidesWith(other.Value, Tol);
                    bool tried = Circle.TryIntersectWith(other.Value, out GeoPoint3[] triedOut, Tol);

                    Alike(other.Key, collides, ellipse.CollidesWith(other.Value, Tol));
                    Same(other.Key, expected, ellipse.GetIntersections(other.Value, Tol));
                    Same(other.Key, tried, triedOut, ellipse.TryIntersectWith(other.Value, out GeoPoint3[] eo, Tol), eo);

                    // The other circle as an ellipse too.
                    GeoEllipse3 asEllipse = GeoEllipse3.FromCircle(other.Value);
                    Alike(other.Key + ", as an ellipse", collides, ellipse.CollidesWith(asEllipse, Tol));
                    Same(other.Key + ", as an ellipse", expected, ellipse.GetIntersections(asEllipse, Tol));
                    Same(other.Key + ", as an ellipse", tried, triedOut, ellipse.TryIntersectWith(asEllipse, out GeoPoint3[] ao, Tol), ao);
                }
            }
        }

        [Fact]
        public void AnArcIsAnsweredAsTheCircleAnswersIt()
        {
            Specimen f = Frame();
            GeoVector3 normal = LeaningNormal(out GeoPoint3 first, out GeoPoint3 second);
            GeoCircle3 across = CircleThroughTwo(first, second, normal, 30);
            GeoVector3 x = across.Center.GetVectorTo(first).Normalize();
            GeoVector3 y = across.Normal.CrossProduct(x);
            GeoPoint3 On(double angle) => across.Center.Add(x.Multiply(across.Radius * Math.Cos(angle))).Add(y.Multiply(across.Radius * Math.Sin(angle)));
            GeoPoint3 far = across.Center.Add(first.Add(first.GetVectorTo(second).Multiply(0.5)).GetVectorTo(across.Center).Normalize().Multiply(across.Radius));
            var arcs = new Dictionary<string, GeoArc3>
            {
                ["across, over both"] = GeoArc3.FromThreePoints(first, far, second),
                ["across, about one"] = GeoArc3.FromThreePoints(On(-0.2), On(0.05), On(0.2)),
                ["in the plane, apart"] = GeoArc3.FromThreePoints(f.At(150 + 200, 40, 0), f.At(150, 240, 0), f.At(150 - 200, 40, 0)),
                ["on the circle itself"] = GeoArc3.FromThreePoints(f.Rim(0.2), f.Rim(1.0), f.Rim(2.0)),
                ["inside"] = GeoArc3.FromThreePoints(f.At(0, 50, 0), f.At(35.36, 35.36, 0), f.At(50, 0, 0)),
            };

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoArc3> arc in arcs)
                {
                    Alike(arc.Key, Circle.CollidesWith(arc.Value, Tol), ellipse.CollidesWith(arc.Value, Tol));
                    Same(arc.Key, Circle.GetIntersections(arc.Value, Tol), ellipse.GetIntersections(arc.Value, Tol));
                    bool c = Circle.TryIntersectWith(arc.Value, out GeoPoint3[] co, Tol);
                    bool e = ellipse.TryIntersectWith(arc.Value, out GeoPoint3[] eo, Tol);
                    Same(arc.Key, c, co, e, eo);
                }
            }
        }

        [Fact]
        public void TrianglesPolygonsAndFacesAreAnsweredAsTheCircleAnswersThem()
        {
            Specimen f = Frame();
            GeoVector3 normal = LeaningNormal(out GeoPoint3 first, out GeoPoint3 second, 60);
            GeoVector3 chord = first.GetVectorTo(second);
            GeoVector3 w = normal.CrossProduct(chord.Normalize());
            GeoPoint3 middle = first.Add(chord.Multiply(0.5));
            var triangles = new Dictionary<string, GeoTriangle3>
            {
                ["standing across"] = new GeoTriangle3(first.Add(chord.Multiply(-0.5)).Add(w.Multiply(-20)), second.Add(chord.Multiply(0.5)).Add(w.Multiply(-20)), middle.Add(w.Multiply(200))),
                ["standing through the disc"] = new GeoTriangle3(middle.Add(chord.Multiply(-0.1)).Add(w.Multiply(-20)), middle.Add(chord.Multiply(0.1)).Add(w.Multiply(-20)), middle.Add(w.Multiply(30))),
                ["in the plane, across"] = FrameTriangle(0, -50, -50, 400, -50, -50, 150),
                ["in the plane, inside"] = FrameTriangle(0, -20, -20, 20, -20, 0, 20),
                ["in the plane, holding it"] = FrameTriangle(0, -1000, -1000, 1000, -1000, 0, 1000),
            };
            var polygons = new Dictionary<string, GeoPolygon3>
            {
                ["standing across"] = new GeoPolygon3(first.Add(chord.Multiply(-0.3)).Add(w.Multiply(-30)), second.Add(chord.Multiply(0.3)).Add(w.Multiply(-30)), second.Add(chord.Multiply(0.3)).Add(w.Multiply(30)), first.Add(chord.Multiply(-0.3)).Add(w.Multiply(30))),
                ["in the plane, across"] = FramePolygon(0, -100, -300, 100, -300, 100, 300, -100, 300),
                ["in the plane, holding it"] = FramePolygon(0, -400, -400, 400, -400, 400, 400, -400, 400),
            };
            var faces = new Dictionary<string, GeoFace3>
            {
                ["in the plane, a hole across the rim"] = new GeoFace3(FramePolygon(0, -400, -400, 400, -400, 400, 400, -400, 400), new[] { FramePolygon(0, 200, -50, 350, -50, 350, 50, 200, 50) }),
                ["in the plane, in a hole"] = new GeoFace3(FramePolygon(0, -1000, -1000, 1000, -1000, 1000, 1000, -1000, 1000), new[] { FramePolygon(0, -400, -400, 400, -400, 400, 400, -400, 400) }),
                ["standing across, a hole about one point"] = new GeoFace3(polygons["standing across"], new[]
                {
                    new GeoPolygon3(first.Add(chord.Normalize().Multiply(-10)).Add(w.Multiply(-10)), first.Add(chord.Normalize().Multiply(10)).Add(w.Multiply(-10)), first.Add(chord.Normalize().Multiply(10)).Add(w.Multiply(10)), first.Add(chord.Normalize().Multiply(-10)).Add(w.Multiply(10))),
                }),
            };

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoTriangle3> t in triangles)
                {
                    Alike(t.Key, Circle.CollidesWith(t.Value, Tol), ellipse.CollidesWith(t.Value, Tol));
                    Same("triangle " + t.Key, Circle.GetIntersections(t.Value, Tol), ellipse.GetIntersections(t.Value, Tol));
                    Same("triangle " + t.Key, Circle.TryIntersectWith(t.Value, out GeoPoint3[] co, Tol), co, ellipse.TryIntersectWith(t.Value, out GeoPoint3[] eo, Tol), eo);
                }

                foreach (KeyValuePair<string, GeoPolygon3> p in polygons)
                {
                    Alike(p.Key, Circle.CollidesWith(p.Value, Tol), ellipse.CollidesWith(p.Value, Tol));
                    Same("polygon " + p.Key, Circle.GetIntersections(p.Value, Tol), ellipse.GetIntersections(p.Value, Tol));
                    Same("polygon " + p.Key, Circle.TryIntersectWith(p.Value, out GeoPoint3[] co, Tol), co, ellipse.TryIntersectWith(p.Value, out GeoPoint3[] eo, Tol), eo);
                }

                foreach (KeyValuePair<string, GeoFace3> face in faces)
                {
                    Alike(face.Key, Circle.CollidesWith(face.Value, Tol), ellipse.CollidesWith(face.Value, Tol));
                    Same("face " + face.Key, Circle.GetIntersections(face.Value, Tol), ellipse.GetIntersections(face.Value, Tol));
                    Same("face " + face.Key, Circle.TryIntersectWith(face.Value, out GeoPoint3[] co, Tol), co, ellipse.TryIntersectWith(face.Value, out GeoPoint3[] eo, Tol), eo);
                }
            }
        }

        [Fact]
        public void BoxesAndBodiesAreAnsweredAsTheCircleAnswersThem()
        {
            // An oriented box 120 on a side about the rim point at 0.3, turned with the frame; a world box about the centre
            // 300 across, which the rim of 250 leaves and re-enters; the same box as a body, with a duct 40 square along
            // the circle's normal through its middle; and a world box 100 across the disc passes round.
            Specimen f = Frame();
            GeoPoint3 c = Circle.Center;
            var obb = new GeoObb3(f.Rim(0.3), 120, 120, 120, f.U, f.V);
            var aabb = new GeoAabb3(new GeoPoint3(c.X - 150, c.Y - 150, c.Z - 150), new GeoPoint3(c.X + 150, c.Y + 150, c.Z + 150));
            var small = new GeoAabb3(new GeoPoint3(c.X - 50, c.Y - 50, c.Z - 50), new GeoPoint3(c.X + 50, c.Y + 50, c.Z + 50));
            GeoSolid3 duct = new GeoObb3(c, 40, 40, 600, f.U, f.V).ToSolid();
            var solids = new Dictionary<string, GeoSolid3>
            {
                ["the world box"] = aabb.ToObb().ToSolid(),
                ["the world box with a duct"] = aabb.ToObb().ToSolid().WithOpenings(new[] { duct }),
                ["the oriented box"] = obb.ToSolid(),
            };

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                Alike("oriented box", Circle.CollidesWith(obb, Tol), ellipse.CollidesWith(obb, Tol));
                Same("oriented box", Circle.GetIntersections(obb, Tol), ellipse.GetIntersections(obb, Tol));
                Same("oriented box", Circle.TryIntersectWith(obb, out GeoPoint3[] oc, Tol), oc, ellipse.TryIntersectWith(obb, out GeoPoint3[] oe, Tol), oe);

                foreach (GeoAabb3 box in new[] { aabb, small, default(GeoAabb3) })
                {
                    Alike($"world box {box}", Circle.CollidesWith(box, Tol), ellipse.CollidesWith(box, Tol));
                    Same($"world box {box}", Circle.GetIntersections(box, Tol), ellipse.GetIntersections(box, Tol));
                    Same($"world box {box}", Circle.TryIntersectWith(box, out GeoPoint3[] bc, Tol), bc, ellipse.TryIntersectWith(box, out GeoPoint3[] be, Tol), be);
                }

                foreach (KeyValuePair<string, GeoSolid3> solid in solids)
                {
                    Alike(solid.Key, Circle.CollidesWith(solid.Value, Tol), ellipse.CollidesWith(solid.Value, Tol));
                    Same(solid.Key, Circle.GetIntersections(solid.Value, Tol), ellipse.GetIntersections(solid.Value, Tol));
                    Same(solid.Key, Circle.TryIntersectWith(solid.Value, out GeoPoint3[] sc, Tol), sc, ellipse.TryIntersectWith(solid.Value, out GeoPoint3[] se, Tol), se);
                }
            }
        }

        [Fact]
        public void EdgesChainsAndLoopsAreAnsweredAsTheCircleAnswersThem()
        {
            Specimen f = Frame();
            double h = 300 * Math.Sqrt(0.5);
            GeoArc3 bigQuarter = GeoArc3.FromThreePoints(f.At(0, 300, 0), f.At(h, h, 0), f.At(300, 0, 0));
            var edges = new Dictionary<string, GeoEdge3>
            {
                ["straight"] = new GeoEdge3(f.At(-400, 50, 0), f.At(400, 50, 0)),
                ["curved"] = new GeoEdge3(GeoArc3.FromThreePoints(f.At(400, 40, 0), f.At(150, 240, 0), f.At(-50, 40, 0))),
            };
            var chain = new GeoPolylineArc3(new[]
            {
                new GeoEdge3(f.At(-400, 0, 0), f.At(0, 300, 0)), new GeoEdge3(bigQuarter), new GeoEdge3(f.At(300, 0, 0), f.At(400, -100, 0)),
            });
            var loop = new GeoPolygonArc3(new[] { f.At(-100, -300, 0), f.At(100, -300, 0), f.At(100, 300, 0), f.At(-100, 300, 0) });
            var round = new GeoPolygonArc3(new[] { f.At(-400, -400, 0), f.At(400, -400, 0), f.At(400, 400, 0), f.At(-400, 400, 0) });

            foreach (GeoEllipse3 ellipse in AsEllipses())
            {
                foreach (KeyValuePair<string, GeoEdge3> edge in edges)
                {
                    Alike(edge.Key, Circle.CollidesWith(edge.Value, Tol), ellipse.CollidesWith(edge.Value, Tol));
                    Same("edge " + edge.Key, Circle.GetIntersections(edge.Value, Tol), ellipse.GetIntersections(edge.Value, Tol));
                    Same("edge " + edge.Key, Circle.TryIntersectWith(edge.Value, out GeoPoint3[] co, Tol), co, ellipse.TryIntersectWith(edge.Value, out GeoPoint3[] eo, Tol), eo);
                }

                Alike("chain", Circle.CollidesWith(chain, Tol), ellipse.CollidesWith(chain, Tol));
                Same("chain", Circle.GetIntersections(chain, Tol), ellipse.GetIntersections(chain, Tol));
                foreach (GeoPolygonArc3 l in new[] { loop, round })
                {
                    Alike("loop", Circle.CollidesWith(l, Tol), ellipse.CollidesWith(l, Tol));
                    Same("loop", Circle.GetIntersections(l, Tol), ellipse.GetIntersections(l, Tol));
                }
            }
        }
    }
}
