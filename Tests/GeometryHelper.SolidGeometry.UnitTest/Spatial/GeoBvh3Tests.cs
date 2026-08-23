using System;
using System.Collections.Generic;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.SolidGeometry.Spatial;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Spatial
{
    /// <summary>
    /// Covers the bounding volume hierarchy, mostly by checking that it agrees with the plain scan it
    /// replaces. An index that gives a different answer is worse than no index at all.
    /// </summary>
    public class GeoBvh3Tests
    {
        /// <summary>
        /// Builds a closed prism approximating a cylinder, giving a mesh of a chosen size.
        /// </summary>
        private static GeoSolid3 MakeCylinder(int segments, double radius, double height)
        {
            GeoCircle3 profile = new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, radius);
            GeoPolygon3 bottomOutline = profile.ToPolygon(segments);

            GeoPoint3[] bottom = new GeoPoint3[segments];
            GeoPoint3[] top = new GeoPoint3[segments];

            for (int i = 0; i < segments; i++)
            {
                bottom[i] = bottomOutline[i];
                top[i] = bottomOutline[i].Add(new GeoVector3(0, 0, height));
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            GeoPoint3[] bottomReversed = new GeoPoint3[segments];
            for (int i = 0; i < segments; i++)
            {
                bottomReversed[i] = bottom[segments - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                faces.Add(new GeoFace3(new GeoPolygon3(bottom[i], bottom[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }

        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        [Fact]
        public void AnEmptyHierarchyHoldsNothing()
        {
            GeoBvh3 empty = new GeoBvh3(new GeoTriangle3[0]);

            Assert.Equal(0, empty.TriangleCount);
            Assert.True(empty.Bounds.IsEmpty);
            Assert.Empty(empty.GetIntersections(new GeoRay3(GeoPoint3.Origin, GeoVector3.ZAxis)));
            Assert.Throws<InvalidOperationException>(() => empty.GetClosestPoint(GeoPoint3.Origin));
        }

        [Fact]
        public void TheRootBoxEnclosesEveryTriangle()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)));

            Assert.Equal(12, tree.TriangleCount);
            Assert.True(tree.Bounds.Min.IsEqualTo(GeoPoint3.Origin));
            Assert.True(tree.Bounds.Max.IsEqualTo(new GeoPoint3(10, 10, 10)));
        }

        [Fact]
        public void ARayThroughASolidCrossesItsSurfaceTwice()
        {
            GeoBvh3 tree = GeoBvh3.FromSolid(MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)));

            // Away from the diagonals the two cap triangles are split along.
            GeoRay3 through = new GeoRay3(new GeoPoint3(3, 6, -20), GeoVector3.ZAxis);
            GeoRay3 beside = new GeoRay3(new GeoPoint3(50, 50, -20), GeoVector3.ZAxis);

            Assert.Equal(2, tree.GetIntersections(through).Length);
            Assert.Empty(tree.GetIntersections(beside));
        }

        [Fact]
        public void ARayAlongASharedEdgeIsReportedByBothTrianglesMeetingThere()
        {
            // Documented behaviour rather than a defect: a triangle owns its edges, so a ray through an
            // edge crosses both triangles that share it. Anything counting crossings to decide inside from
            // outside has to keep clear of edges, which is why Containment3 throws its ray again in another
            // direction when a hit lands near one.
            GeoBvh3 tree = GeoBvh3.FromSolid(MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)));

            GeoRay3 alongDiagonal = new GeoRay3(new GeoPoint3(5, 5, -20), GeoVector3.ZAxis);

            Assert.Equal(4, tree.GetIntersections(alongDiagonal).Length);
        }

        [Fact]
        public void RayCastingAgreesWithScanningEveryTriangle()
        {
            GeoSolid3 cylinder = MakeCylinder(64, 5.0, 12.0);
            GeoBvh3 tree = GeoBvh3.FromSolid(cylinder);
            GeoTriangle3[] mesh = cylinder.Triangulate();

            GeoRay3[] rays =
            {
                new GeoRay3(new GeoPoint3(0, 0, -30), GeoVector3.ZAxis),
                new GeoRay3(new GeoPoint3(-30, 0.7, 6), GeoVector3.XAxis),
                new GeoRay3(new GeoPoint3(-30, -30, 3), new GeoVector3(1, 1, 0.1)),
                new GeoRay3(new GeoPoint3(0, 0, 40), GeoVector3.ZAxis)
            };

            foreach (GeoRay3 ray in rays)
            {
                int scanned = 0;
                foreach (GeoTriangle3 triangle in mesh)
                {
                    if (Intersection3.TryIntersectWith(ray, triangle, out _))
                    {
                        scanned++;
                    }
                }

                Assert.Equal(scanned, tree.GetIntersections(ray).Length);
            }
        }

        [Fact]
        public void TheClosestPointAgreesWithScanningEveryTriangle()
        {
            GeoSolid3 cylinder = MakeCylinder(48, 5.0, 12.0);
            GeoBvh3 tree = GeoBvh3.FromSolid(cylinder);
            GeoTriangle3[] mesh = cylinder.Triangulate();

            GeoPoint3[] probes =
            {
                new GeoPoint3(0, 0, 6),
                new GeoPoint3(20, 0, 6),
                new GeoPoint3(0, 0, 40),
                new GeoPoint3(-13, 7, -4),
                new GeoPoint3(4.9, 0.1, 11.9)
            };

            foreach (GeoPoint3 probe in probes)
            {
                double scanned = double.MaxValue;
                foreach (GeoTriangle3 triangle in mesh)
                {
                    scanned = Math.Min(scanned, Distance3.DistanceTo(triangle, probe));
                }

                Assert.Equal(scanned, tree.DistanceTo(probe), 9);
                Assert.Equal(scanned, tree.GetClosestPoint(probe).DistanceTo(probe), 9);
            }
        }

        [Fact]
        public void CollisionAgreesWithComparingEveryPair()
        {
            GeoSolid3 first = MakeCylinder(32, 5.0, 10.0);

            GeoVector3[] offsets =
            {
                new GeoVector3(0, 0, 0),
                new GeoVector3(9, 0, 0),
                new GeoVector3(11, 0, 0),
                new GeoVector3(0, 0, 10),
                new GeoVector3(0, 0, 25),
                new GeoVector3(100, 100, 100)
            };

            GeoTriangle3[] mesh1 = first.Triangulate();

            foreach (GeoVector3 offset in offsets)
            {
                GeoSolid3 second = first.TransformBy(GeoTransform3.Translation(offset));
                GeoTriangle3[] mesh2 = second.Triangulate();

                bool scanned = false;
                foreach (GeoTriangle3 t1 in mesh1)
                {
                    foreach (GeoTriangle3 t2 in mesh2)
                    {
                        if (Collision3.CollidesWith(t1, t2))
                        {
                            scanned = true;
                            break;
                        }
                    }

                    if (scanned)
                    {
                        break;
                    }
                }

                Assert.Equal(scanned, new GeoBvh3(mesh1).CollidesWith(new GeoBvh3(mesh2)));
            }
        }

        [Fact]
        public void CollisionThroughTheHierarchyIsSymmetric()
        {
            GeoBvh3 first = GeoBvh3.FromSolid(MakeCylinder(24, 4.0, 8.0));
            GeoBvh3 second = GeoBvh3.FromSolid(
                MakeCylinder(24, 4.0, 8.0).TransformBy(GeoTransform3.Translation(new GeoVector3(6, 0, 2))));

            Assert.Equal(first.CollidesWith(second), second.CollidesWith(first));
            Assert.True(first.CollidesWith(second));
        }

        [Fact]
        public void SolidCollisionStillAgreesWithItselfOnceTheIndexTakesOver()
        {
            // Well past the threshold at which Collision3 switches to the indexed path.
            GeoSolid3 first = MakeCylinder(64, 5.0, 10.0);
            GeoSolid3 overlapping = first.TransformBy(GeoTransform3.Translation(new GeoVector3(4, 0, 0)));
            GeoSolid3 apart = first.TransformBy(GeoTransform3.Translation(new GeoVector3(40, 0, 0)));

            Assert.True(Collision3.CollidesWith(first, overlapping));
            Assert.False(Collision3.CollidesWith(first, apart));
        }

        [Fact]
        public void ASolidWhollyInsideAnotherIsStillFoundWithTheIndexInPlay()
        {
            // The trees only find surface contact, so this case has to be caught by the containment test
            // that Collision3 keeps alongside them.
            GeoSolid3 outer = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));
            GeoSolid3 inner = MakeCylinder(64, 5.0, 10.0)
                .TransformBy(GeoTransform3.Translation(new GeoVector3(50, 50, 40)));

            Assert.True(Collision3.CollidesWith(outer, inner));
            Assert.True(Collision3.CollidesWith(inner, outer));
        }

        [Fact]
        public void ClosingUpALargeSurfaceStaysQuick()
        {
            // Matching vertices by scanning what had been seen so far made this quadratic: at 4000
            // vertices that is eight million comparisons. The spatial grid keeps it linear. The assertion
            // is on the answer, not the clock, but a regression here would show as the suite slowing down.
            GeoSolid3 cylinder = MakeCylinder(2000, 5.0, 10.0);

            Assert.Equal(2002, cylinder.Faces.Count);
            Assert.True(cylinder.IsClosed());
        }

        [Fact]
        public void AnOpenSurfaceIsStillReportedAsOpen()
        {
            GeoSolid3 cylinder = MakeCylinder(32, 5.0, 10.0);

            List<GeoFace3> withGap = new List<GeoFace3>(cylinder.Faces);
            withGap.RemoveAt(0);
            withGap.Add(withGap[0]);

            Assert.False(new GeoSolid3(withGap).IsClosed());
        }
    }
}
