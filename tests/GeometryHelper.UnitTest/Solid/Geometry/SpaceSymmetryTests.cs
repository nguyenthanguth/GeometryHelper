using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The rest of the asymmetries in space. Measuring how far off a shape is ran one way round only, so a
    /// solid could be asked about a triangle and a triangle could only ever be asked about a point; and nine
    /// collisions were missing from Core itself rather than from the types.
    /// </summary>
    public class SpaceSymmetryTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoAabb3 Cube() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100));

        /// <summary>
        /// A triangle in the plane x = 200, a hundred clear of the cube.
        /// </summary>
        private static GeoTriangle3 Beyond() => new GeoTriangle3(
            new GeoPoint3(200, 0, 0), new GeoPoint3(200, 100, 0), new GeoPoint3(200, 0, 100));

        /// <summary>
        /// A hundred plate in the plane z = 0 with a twenty-wide hole in the middle.
        /// </summary>
        private static GeoFace3 Pierced() => new GeoFace3(
            new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)),
            new[]
            {
                new GeoPolygon3(
                    new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0))
            });

        [Fact]
        public void ATriangleCanNowBeAskedHowFarOffTheOtherShapesAre()
        {
            GeoSolid3 solid = Cube().ToObb().ToSolid();
            GeoTriangle3 beyond = Beyond();

            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(300, 50, 50), new GeoVector3(1, 0, 0));
            var chain = new GeoPolyline3(new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0));

            // The triangle stands at x = 200 and everything else is measured across to it.
            Assert.Equal(100.0, beyond.DistanceTo(solid), 6);
            Assert.Equal(150.0, beyond.DistanceTo(line), 6);
            Assert.Equal(100.0, beyond.DistanceTo(ray), 6);
            Assert.Equal(200.0, beyond.DistanceTo(chain), 6);

            // Each agrees with the direction that already existed, or with Core where neither did.
            Assert.Equal(solid.DistanceTo(beyond), beyond.DistanceTo(solid), 9);
            Assert.Equal(line.DistanceTo(beyond), beyond.DistanceTo(line), 9);
            Assert.Equal(chain.DistanceTo(beyond), beyond.DistanceTo(chain), 9);
            Assert.Equal(Distance3.DistanceTo(ray, beyond), beyond.DistanceTo(ray), 9);

            // And against another triangle, which it could not be asked either.
            var over = new GeoTriangle3(
                new GeoPoint3(0, 0, 200), new GeoPoint3(100, 0, 200), new GeoPoint3(0, 100, 200));
            var flat = new GeoTriangle3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            Assert.Equal(200.0, flat.DistanceTo(over), 6);
            Assert.Equal(over.DistanceTo(flat), flat.DistanceTo(over), 9);
        }

        [Fact]
        public void BoxesPlanesPolygonsAndRaysMeasureTheOtherWayRoundToo()
        {
            GeoAabb3 aabb = Cube();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();

            // Everything here lies in the plane z = 0 except the planes, so each number is a plain gap along x.
            var chain = new GeoPolyline3(new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0));
            var farChain = new GeoPolyline3(new GeoPoint3(500, 0, 0), new GeoPoint3(500, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0), new GeoPoint3(300, 100, 0));
            var ray = new GeoRay3(new GeoPoint3(300, 50, 50), new GeoVector3(1, 0, 0));
            var line = new GeoLine3(new GeoPoint3(0, 50, 0), new GeoPoint3(50, 50, 0));

            Assert.Equal(200.0, aabb.DistanceTo(chain), 6);
            Assert.Equal(200.0, obb.DistanceTo(chain), 6);
            Assert.Equal(250.0, polygon.DistanceTo(line), 6);
            Assert.Equal(100.0, polygon.DistanceTo(farChain), 6);
            Assert.Equal(200.0, ray.DistanceTo(solid), 6);

            Assert.Equal(chain.DistanceTo(aabb), aabb.DistanceTo(chain), 9);
            Assert.Equal(chain.DistanceTo(obb), obb.DistanceTo(chain), 9);
            Assert.Equal(line.DistanceTo(polygon), polygon.DistanceTo(line), 9);
            Assert.Equal(farChain.DistanceTo(polygon), polygon.DistanceTo(farChain), 9);
            Assert.Equal(Distance3.DistanceTo(ray, solid), ray.DistanceTo(solid), 9);

            // A box, a plane and a polygon against a body.
            Assert.Equal(0.0, aabb.DistanceTo(solid), 6);
            Assert.Equal(0.0, obb.DistanceTo(solid), 6);

            var away = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100));

            Assert.Equal(200.0, away.DistanceTo(solid), 6);
            Assert.Equal(solid.DistanceTo(away), away.DistanceTo(solid), 9);

            // Two of a kind, neither of which could be measured against its own sort.
            var high = new GeoPlane3(new GeoPoint3(0, 0, 300), new GeoVector3(0, 0, 1));
            var low = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            // The cube stops at z = 100 and the chain lies on z = 0, so the plane at z = 300 is 200 off one
            // and 300 off the other.
            Assert.Equal(300.0, low.DistanceTo(high), 6);
            Assert.Equal(200.0, high.DistanceTo(solid), 6);
            Assert.Equal(300.0, high.DistanceTo(chain), 6);

            Assert.Equal(200.0, obb.DistanceTo(away.ToObb()), 6);
        }

        [Fact]
        public void ASegmentOrRayTouchingAFlatShapeIsNowACollisionInItsOwnRight()
        {
            var flat = new GeoTriangle3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

            var through = new GeoLine3(new GeoPoint3(20, 10, -50), new GeoPoint3(20, 10, 50));
            var beside = new GeoLine3(new GeoPoint3(500, 500, -50), new GeoPoint3(500, 500, 50));

            Assert.True(through.CollidesWith(flat));
            Assert.True(flat.CollidesWith(through));
            Assert.False(beside.CollidesWith(flat));
            Assert.False(flat.CollidesWith(beside));

            Assert.True(through.CollidesWith(polygon));
            Assert.True(polygon.CollidesWith(through));

            var down = new GeoRay3(new GeoPoint3(20, 10, 50), new GeoVector3(0, 0, -1));
            var up = new GeoRay3(new GeoPoint3(20, 10, 50), new GeoVector3(0, 0, 1));

            Assert.True(down.CollidesWith(flat));
            Assert.True(flat.CollidesWith(down));
            Assert.False(up.CollidesWith(flat));
            Assert.True(down.CollidesWith(polygon));
            Assert.True(polygon.CollidesWith(down));
        }

        [Fact]
        public void AHoleInAFaceIsStillAHoleWhenTheQuestionIsWhetherSomethingTouchesIt()
        {
            GeoFace3 face = Pierced();

            var throughMaterial = new GeoLine3(new GeoPoint3(20, 20, -50), new GeoPoint3(20, 20, 50));
            var throughHole = new GeoLine3(new GeoPoint3(50, 50, -50), new GeoPoint3(50, 50, 50));

            Assert.True(throughMaterial.CollidesWith(face));
            Assert.True(face.CollidesWith(throughMaterial));

            // Down the middle of the hole touches nothing, which is what a hole is.
            Assert.False(throughHole.CollidesWith(face));
            Assert.False(face.CollidesWith(throughHole));

            var intoMaterial = new GeoRay3(new GeoPoint3(20, 20, 50), new GeoVector3(0, 0, -1));
            var intoHole = new GeoRay3(new GeoPoint3(50, 50, 50), new GeoVector3(0, 0, -1));

            Assert.True(intoMaterial.CollidesWith(face));
            Assert.True(face.CollidesWith(intoMaterial));
            Assert.False(intoHole.CollidesWith(face));
        }

        [Fact]
        public void ARayStartingInsideABoxTouchesItEvenThoughItEntersNowhere()
        {
            GeoAabb3 aabb = Cube();
            GeoObb3 obb = aabb.ToObb();

            var within = new GeoRay3(new GeoPoint3(50, 50, 50), new GeoVector3(1, 0, 0));
            var into = new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(1, 0, 0));
            var away = new GeoRay3(new GeoPoint3(-50, 50, 50), new GeoVector3(-1, 0, 0));

            Assert.True(within.CollidesWith(aabb));
            Assert.True(within.CollidesWith(obb));
            Assert.True(aabb.CollidesWith(within));
            Assert.True(obb.CollidesWith(within));

            Assert.True(into.CollidesWith(aabb));
            Assert.True(into.CollidesWith(obb));
            Assert.False(away.CollidesWith(aabb));
            Assert.False(away.CollidesWith(obb));
            Assert.False(aabb.CollidesWith(away));
        }

        [Fact]
        public void AnAxisAlignedBoxCanBeAskedAboutABody()
        {
            GeoSolid3 solid = Cube().ToObb().ToSolid();

            var overlapping = new GeoAabb3(new GeoPoint3(50, 50, 50), new GeoPoint3(150, 150, 150));
            var clear = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100));

            Assert.True(overlapping.CollidesWith(solid));
            Assert.True(solid.CollidesWith(overlapping));
            Assert.False(clear.CollidesWith(solid));
            Assert.False(solid.CollidesWith(clear));

            // It is the oriented answer, not an approximation of it.
            Assert.Equal(overlapping.ToObb().CollidesWith(solid), overlapping.CollidesWith(solid));
            Assert.Equal(clear.ToObb().CollidesWith(solid), clear.CollidesWith(solid));
        }

        [Fact]
        public void APointKnowsHowDeepInsideAShapeItSits()
        {
            GeoAabb3 aabb = Cube();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();

            var middle = new GeoPoint3(50, 50, 50);
            var outside = new GeoPoint3(150, 50, 50);

            Assert.Equal(-50.0, middle.SignedDistanceTo(aabb), 6);
            Assert.Equal(-50.0, middle.SignedDistanceTo(obb), 6);
            Assert.Equal(50.0, outside.SignedDistanceTo(aabb), 6);

            // Same answer whichever of the two is asked, which is the whole point of the direction.
            Assert.Equal(aabb.SignedDistanceTo(middle), middle.SignedDistanceTo(aabb), 9);
            Assert.Equal(obb.SignedDistanceTo(middle), middle.SignedDistanceTo(obb), 9);
            Assert.Equal(solid.SignedDistanceTo(middle), middle.SignedDistanceTo(solid), 9);
            Assert.Equal(solid.SignedDistanceTo(outside), outside.SignedDistanceTo(solid), 9);

            Assert.True(middle.SignedDistanceTo(solid) < 0.0);
            Assert.True(outside.SignedDistanceTo(solid) > 0.0);
        }

        [Fact]
        public void EveryNewDirectionTakesAToleranceAndAnswersTheSameWithTheDefaultOne()
        {
            Tolerance global = Tolerance.Global;
            GeoAabb3 aabb = Cube();
            GeoObb3 obb = aabb.ToObb();
            GeoSolid3 solid = obb.ToSolid();
            GeoTriangle3 beyond = Beyond();
            GeoFace3 face = Pierced();

            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(300, 50, 50), new GeoVector3(1, 0, 0));
            var chain = new GeoPolyline3(new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(400, 100, 0), new GeoPoint3(300, 100, 0));
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 300), new GeoVector3(0, 0, 1));
            var point = new GeoPoint3(50, 50, 50);

            Assert.Equal(beyond.DistanceTo(line), beyond.DistanceTo(line, global), 9);
            Assert.Equal(beyond.DistanceTo(ray), beyond.DistanceTo(ray, global), 9);
            Assert.Equal(beyond.DistanceTo(chain), beyond.DistanceTo(chain, global), 9);
            Assert.Equal(beyond.DistanceTo(solid), beyond.DistanceTo(solid, global), 9);
            Assert.Equal(beyond.DistanceTo(beyond), beyond.DistanceTo(beyond, global), 9);
            Assert.Equal(polygon.DistanceTo(line), polygon.DistanceTo(line, global), 9);
            Assert.Equal(polygon.DistanceTo(chain), polygon.DistanceTo(chain, global), 9);
            Assert.Equal(polygon.DistanceTo(solid), polygon.DistanceTo(solid, global), 9);
            Assert.Equal(aabb.DistanceTo(chain), aabb.DistanceTo(chain, global), 9);
            Assert.Equal(aabb.DistanceTo(solid), aabb.DistanceTo(solid, global), 9);
            Assert.Equal(obb.DistanceTo(chain), obb.DistanceTo(chain, global), 9);
            Assert.Equal(obb.DistanceTo(solid), obb.DistanceTo(solid, global), 9);
            Assert.Equal(obb.DistanceTo(obb), obb.DistanceTo(obb, global), 9);
            Assert.Equal(plane.DistanceTo(chain), plane.DistanceTo(chain, global), 9);
            Assert.Equal(plane.DistanceTo(solid), plane.DistanceTo(solid, global), 9);
            Assert.Equal(plane.DistanceTo(plane), plane.DistanceTo(plane, global), 9);
            Assert.Equal(ray.DistanceTo(solid), ray.DistanceTo(solid, global), 9);
            Assert.Equal(ray.DistanceTo(beyond), ray.DistanceTo(beyond, global), 9);
            Assert.Equal(line.DistanceTo(chain), line.DistanceTo(chain, global), 9);

            Assert.Equal(line.CollidesWith(beyond), line.CollidesWith(beyond, global));
            Assert.Equal(line.CollidesWith(polygon), line.CollidesWith(polygon, global));
            Assert.Equal(line.CollidesWith(face), line.CollidesWith(face, global));
            Assert.Equal(ray.CollidesWith(beyond), ray.CollidesWith(beyond, global));
            Assert.Equal(ray.CollidesWith(polygon), ray.CollidesWith(polygon, global));
            Assert.Equal(ray.CollidesWith(face), ray.CollidesWith(face, global));
            Assert.Equal(ray.CollidesWith(aabb), ray.CollidesWith(aabb, global));
            Assert.Equal(ray.CollidesWith(obb), ray.CollidesWith(obb, global));
            Assert.Equal(aabb.CollidesWith(solid), aabb.CollidesWith(solid, global));
            Assert.Equal(aabb.CollidesWith(ray), aabb.CollidesWith(ray, global));
            Assert.Equal(obb.CollidesWith(ray), obb.CollidesWith(ray, global));
            Assert.Equal(solid.CollidesWith(aabb), solid.CollidesWith(aabb, global));
            Assert.Equal(beyond.CollidesWith(line), beyond.CollidesWith(line, global));
            Assert.Equal(beyond.CollidesWith(ray), beyond.CollidesWith(ray, global));
            Assert.Equal(polygon.CollidesWith(line), polygon.CollidesWith(line, global));
            Assert.Equal(polygon.CollidesWith(ray), polygon.CollidesWith(ray, global));
            Assert.Equal(face.CollidesWith(line), face.CollidesWith(line, global));
            Assert.Equal(face.CollidesWith(ray), face.CollidesWith(ray, global));

            Assert.Equal(point.SignedDistanceTo(aabb), point.SignedDistanceTo(aabb, global), 9);
            Assert.Equal(point.SignedDistanceTo(obb), point.SignedDistanceTo(obb, global), 9);
            Assert.Equal(point.SignedDistanceTo(solid), point.SignedDistanceTo(solid, global), 9);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            GeoTriangle3 beyond = Beyond();
            var line = new GeoLine3(new GeoPoint3(0, 50, 50), new GeoPoint3(50, 50, 50));
            var ray = new GeoRay3(new GeoPoint3(300, 50, 50), new GeoVector3(1, 0, 0));
            var point = new GeoPoint3(50, 50, 50);

            Assert.Throws<ArgumentNullException>(() => beyond.DistanceTo((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => beyond.DistanceTo((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => Cube().DistanceTo((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => Cube().CollidesWith((GeoSolid3)null));
            Assert.Throws<ArgumentNullException>(() => line.CollidesWith((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => ray.CollidesWith((GeoFace3)null));
            Assert.Throws<ArgumentNullException>(() => point.SignedDistanceTo((GeoSolid3)null));
        }
    }
}
