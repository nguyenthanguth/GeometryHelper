using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The crossings of an infinite line with the mesh a hierarchy holds (XL-5): the line runs both ways from its origin,
    /// so wherever the origin stands, even inside the mesh or past it, the answer is the same.
    /// </summary>
    /// <remarks>
    /// The hierarchy's answers are held to the plain scan over every triangle, each asked the line's own
    /// <see cref="GeoXLine3.TryIntersectWith(GeoTriangle3, out GeoPoint3, Tolerance)"/>: an index that answers otherwise is
    /// worse than none.
    /// </remarks>
    public class XLine3BvhTests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoSolid3 Box(double x, double y, double z) => new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(x, y, z)).ToObb().ToSolid();

        private static GeoSolid3 Cylinder(int segments, double radius, double height)
        {
            GeoPolygon3 outline = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, radius).ToPolygon(segments);
            var faces = new List<GeoFace3>();
            var bottom = new GeoPoint3[segments];
            var top = new GeoPoint3[segments];

            for (int i = 0; i < segments; i++)
            {
                bottom[i] = outline[segments - 1 - i];
                top[i] = outline[i].Add(new GeoVector3(0, 0, height));
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottom)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                faces.Add(new GeoFace3(new GeoPolygon3(outline[i], outline[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }

        private static GeoPoint3[] Scan(GeoTriangle3[] mesh, GeoXLine3 xline, Tolerance tolerance)
        {
            var hits = new List<GeoPoint3>();

            foreach (GeoTriangle3 triangle in mesh)
            {
                if (xline.TryIntersectWith(triangle, out GeoPoint3 hit, tolerance))
                {
                    hits.Add(hit);
                }
            }

            return hits.ToArray();
        }

        private static void AssertSamePoints(GeoPoint3[] expected, GeoPoint3[] actual, double within = 1E-9)
        {
            Assert.Equal(expected.Length, actual.Length);

            foreach (GeoPoint3 point in expected)
            {
                Assert.Contains(actual, p => p.DistanceTo(point) <= within);
            }
        }

        [Fact]
        public void ALineThroughABox_CrossesItsSurfaceTwiceFromAnyOriginAlongIt()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(Box(10, 10, 10));

            // Away from the diagonals the two cap triangles are split along; the origin before the box, inside it, past it.
            foreach (double z in new[] { -20.0, 4.0, 30.0, 5000.0 })
            {
                var xline = new GeoXLine3(new GeoPoint3(3, 6, z), GeoVector3.ZAxis);

                AssertSamePoints(new[] { new GeoPoint3(3, 6, 0), new GeoPoint3(3, 6, 10) }, tree.GetIntersections(xline, Strict));
                AssertSamePoints(new[] { new GeoPoint3(3, 6, 0), new GeoPoint3(3, 6, 10) }, tree.GetIntersections(xline.Reverse(), Strict));
            }
        }

        [Fact]
        public void ARayPointingAway_CrossesNothingButTheLineItIsOnCrossesBothSides()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(Box(10, 10, 10));
            var ray = new GeoRay3(new GeoPoint3(3, 6, 30), GeoVector3.ZAxis);

            Assert.Empty(tree.GetIntersections(ray, Strict));
            Assert.Equal(2, tree.GetIntersections(new GeoXLine3(ray.Origin, ray.Direction), Strict).Length);
        }

        [Fact]
        public void ALineBesideTheBox_CrossesNothing()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(Box(10, 10, 10));

            Assert.Empty(tree.GetIntersections(new GeoXLine3(new GeoPoint3(50, 50, -20), GeoVector3.ZAxis), Strict));
            Assert.Empty(tree.GetIntersections(new GeoXLine3(new GeoPoint3(0, 0, 20), new GeoVector3(1, 1, 0)), Strict));
        }

        [Fact]
        public void ALineAlongASharedEdge_IsReportedByBothTrianglesMeetingThere_AsTheRaysIs()
        {
            // The documented behaviour of a ray through a diagonal two triangles share, both ends of it.
            GeoBvh3 tree = GeoBvh3.FromSolid(Box(10, 10, 10));
            var xline = new GeoXLine3(new GeoPoint3(5, 5, -20), GeoVector3.ZAxis);

            Assert.Equal(4, tree.GetIntersections(xline, Strict).Length);
            Assert.Equal(tree.GetIntersections(new GeoRay3(xline.Origin, xline.Direction), Strict).Length, tree.GetIntersections(xline, Strict).Length);
            Assert.Equal(4, tree.GetIntersections(new GeoXLine3(new GeoPoint3(5, 5, 40), GeoVector3.ZAxis), Strict).Length);
        }

        [Fact]
        public void LineCastingAgreesWithScanningEveryTriangle()
        {
            GeoSolid3 cylinder = Cylinder(64, 5.0, 12.0);
            GeoBvh3 tree = GeoBvh3.FromSolid(cylinder);
            GeoTriangle3[] mesh = cylinder.Triangulate();

            GeoXLine3[] lines =
            {
                new GeoXLine3(new GeoPoint3(0, 0, -30), GeoVector3.ZAxis),
                new GeoXLine3(new GeoPoint3(0, 0, 6), GeoVector3.ZAxis),
                new GeoXLine3(new GeoPoint3(0.3, 0.2, 400), GeoVector3.ZAxis),
                new GeoXLine3(new GeoPoint3(-30, 0.7, 6), GeoVector3.XAxis),
                new GeoXLine3(new GeoPoint3(30, 0.7, 6), GeoVector3.XAxis),
                new GeoXLine3(new GeoPoint3(-30, -30, 3), new GeoVector3(1, 1, 0.1)),
                new GeoXLine3(new GeoPoint3(0, 0, 40), new GeoVector3(0.1, 0.03, 1))
            };

            foreach (GeoXLine3 xline in lines)
            {
                GeoPoint3[] scanned = Scan(mesh, xline, Strict);

                Assert.NotEmpty(scanned);
                AssertSamePoints(scanned, tree.GetIntersections(xline, Strict));
            }
        }

        [Fact]
        public void ManyLinesThroughACylinder_AgreeWithTheScan_BothWaysRound()
        {
            GeoSolid3 cylinder = Cylinder(48, 5.0, 12.0);
            GeoBvh3 tree = GeoBvh3.FromSolid(cylinder);
            GeoTriangle3[] mesh = cylinder.Triangulate();
            var random = new Random(20261020);
            int crossing = 0;

            for (int k = 0; k < 300; k++)
            {
                var origin = new GeoPoint3(random.NextDouble() * 16 - 8, random.NextDouble() * 16 - 8, random.NextDouble() * 18 - 3);
                var direction = new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
                var xline = new GeoXLine3(origin, direction);

                GeoPoint3[] scanned = Scan(mesh, xline, Strict);

                AssertSamePoints(scanned, tree.GetIntersections(xline, Strict));
                AssertSamePoints(scanned, tree.GetIntersections(xline.Reverse(), Strict));
                crossing += scanned.Length > 0 ? 1 : 0;
            }

            Assert.InRange(crossing, 60, 300);
        }

        [Fact]
        public void AnEmptyHierarchy_IsCrossedByNoLine()
        {
            var empty = new GeoBvh3(new GeoTriangle3[0]);
            var xline = new GeoXLine3(GeoPoint3.Origin, GeoVector3.ZAxis);

            Assert.Empty(empty.GetIntersections(xline, Strict));
            Assert.Empty(empty.GetIntersections(xline));
        }

        [Fact]
        public void ALineSkimmingTheTopWithinThePlanarTolerance_IsKeptAsTheTrianglesSayAndNotDroppedWithABranch()
        {
            // The line from (-5, 5, 0.0004) rising by a hundredth over a box whose top lies in z = 10 is not near it; the
            // one level with the top 0.0004 over it is within the planar tolerance of every triangle there.
            GeoSolid3 box = Box(10, 10, 10);
            GeoBvh3 tree = GeoBvh3.FromSolid(box);
            GeoTriangle3[] mesh = box.Triangulate();

            foreach (double dz in new[] { 0.0004, -0.0004, 0.002, -0.002 })
            {
                foreach (GeoVector3 direction in new[] { new GeoVector3(1, 0, -0.01), new GeoVector3(1, 0, 0.01), GeoVector3.XAxis, new GeoVector3(1, 0.2, 0.001) })
                {
                    var xline = new GeoXLine3(new GeoPoint3(-5, 5, 10 + dz), direction);

                    AssertSamePoints(Scan(mesh, xline, Strict), tree.GetIntersections(xline, Strict));
                }
            }
        }

        [Fact]
        public void APreparedSolidIsCrossedByALineWhereTheBodyIs_InTheOrderAlongTheLine()
        {
            GeoSolid3 box = Box(10, 10, 10);
            GeoPreparedSolid3 prepared = box.Prepare();

            // From before the body, from inside it and from past it: the same two crossings, along the direction of the line.
            foreach (double z in new[] { -20.0, 4.0, 30.0 })
            {
                var xline = new GeoXLine3(new GeoPoint3(3, 6, z), GeoVector3.ZAxis);

                GeoPoint3[] found = prepared.GetIntersections(xline, Strict);

                Assert.Equal(2, found.Length);
                Assert.True(found[0].DistanceTo(new GeoPoint3(3, 6, 0)) < 1E-9);
                Assert.True(found[1].DistanceTo(new GeoPoint3(3, 6, 10)) < 1E-9);
                AssertSamePoints(box.GetIntersections(xline, Strict), found);
                Assert.True(prepared.CollidesWith(xline, Strict));
                Assert.True(box.CollidesWith(xline, Strict));
            }

            // The ray pointing away from the box crosses nothing; the line it is on does.
            Assert.Empty(prepared.GetIntersections(new GeoRay3(new GeoPoint3(3, 6, 30), GeoVector3.ZAxis), Strict));
            Assert.False(prepared.CollidesWith(new GeoXLine3(new GeoPoint3(50, 50, 5), GeoVector3.ZAxis), Strict));
            Assert.Empty(prepared.GetIntersections(new GeoXLine3(new GeoPoint3(50, 50, 5), GeoVector3.ZAxis), Strict));
            Assert.Equal(prepared.GetIntersections(new GeoXLine3(new GeoPoint3(3, 6, 4), GeoVector3.ZAxis)), prepared.GetIntersections(new GeoXLine3(new GeoPoint3(3, 6, 4), GeoVector3.ZAxis), Tolerance.Global));
        }

        [Fact]
        public void APreparedSolidReportsASeamOnceAndAgreesWithTheBodyOnManyLines()
        {
            GeoSolid3 cylinder = Cylinder(48, 5.0, 12.0);
            GeoPreparedSolid3 prepared = cylinder.Prepare();
            var random = new Random(20261021);
            int crossing = 0;

            for (int k = 0; k < 300; k++)
            {
                var origin = new GeoPoint3(random.NextDouble() * 16 - 8, random.NextDouble() * 16 - 8, random.NextDouble() * 18 - 3);
                var xline = new GeoXLine3(origin, new GeoVector3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5));

                GeoPoint3[] body = cylinder.GetIntersections(xline, Strict);
                GeoPoint3[] found = prepared.GetIntersections(xline, Strict);

                AssertSamePoints(body, found);
                Assert.Equal(cylinder.CollidesWith(xline, Strict), prepared.CollidesWith(xline, Strict));
                crossing += found.Length > 0 ? 1 : 0;
            }

            Assert.InRange(crossing, 60, 300);

            // Along the diagonal two cap triangles share, a ray is named by both, and the prepared body names the place once.
            GeoPreparedSolid3 boxPrepared = Box(10, 10, 10).Prepare();
            Assert.Equal(2, boxPrepared.GetIntersections(new GeoXLine3(new GeoPoint3(5, 5, -20), GeoVector3.ZAxis), Strict).Length);
        }

        [Fact]
        public void TheHierarchysFormWithoutATolerance_AnswersAsTheGlobalToleranceDoes()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(Box(10, 10, 10));
            var xline = new GeoXLine3(new GeoPoint3(3, 6, -20), GeoVector3.ZAxis);

            Assert.Equal(tree.GetIntersections(xline, Tolerance.Global), tree.GetIntersections(xline));
        }
    }
}
