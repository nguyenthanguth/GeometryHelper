using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two asymmetries the plane did not have. Moving a shape sideways was on almost everything in the plane
    /// and on almost nothing in space, and three shapes could not be asked for the point of them nearest a
    /// point at all. Neither needed new arithmetic; both are held here against the long way round.
    /// </summary>
    public class TranslateAndNearestTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);
        private static readonly Tolerance Loose = new Tolerance(1E-7, 1E-7);

        private static GeoVector3 By() => new GeoVector3(11, -23, 37);

        /// <summary>
        /// A plate a hundred square lying in the XY plane, with a twenty-wide hole in the middle.
        /// </summary>
        private static GeoFace3 Pierced() => new GeoFace3(
            new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0)),
            new[]
            {
                new GeoPolygon3(
                    new GeoPoint3(40, 40, 0), new GeoPoint3(60, 40, 0), new GeoPoint3(60, 60, 0), new GeoPoint3(40, 60, 0))
            });

        private static GeoFace2 PiercedFlat() => new GeoFace2(
            new GeoPolygon2(
                new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100)),
            new[]
            {
                new GeoPolygon2(
                    new GeoPoint2(40, 40), new GeoPoint2(60, 40), new GeoPoint2(60, 60), new GeoPoint2(40, 60))
            });

        [Fact]
        public void EveryShapeInSpaceCanBeMovedSideways()
        {
            GeoVector3 by = By();
            GeoTransform3 same = GeoTransform3.Translation(by);

            // Each moves the way the transformation would move it, which is the only thing Translate promises.
            Assert.True(new GeoPoint3(1, 2, 3).Translate(by).IsEqualTo(new GeoPoint3(1, 2, 3).TransformBy(same), Tight));
            Assert.True(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)).Translate(by)
                .IsEqualTo(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)).TransformBy(same), Tight));
            Assert.True(new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)).Translate(by)
                .Origin.IsEqualTo(new GeoPoint3(0, 0, 0).Add(by), Tight));
            Assert.True(new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0))
                .Translate(by).A.IsEqualTo(new GeoPoint3(0, 0, 0).Add(by), Tight));

            var arc = new GeoArc3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 50.0, 0.0, Math.PI / 2);

            Assert.True(arc.Translate(by).Center.IsEqualTo(arc.Center.Add(by), Tight));
            Assert.Equal(arc.Radius, arc.Translate(by).Radius, 9);
            Assert.Equal(arc.Length, arc.Translate(by).Length, 9);

            var circle = new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 30.0);

            Assert.True(circle.Translate(by).Center.IsEqualTo(circle.Center.Add(by), Tight));
            Assert.Equal(30.0, circle.Translate(by).Radius, 9);

            var box = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 10));

            Assert.True(box.Translate(by).Min.IsEqualTo(box.Min.Add(by), Tight));
            Assert.True(box.Translate(by).Max.IsEqualTo(box.Max.Add(by), Tight));

            // A plane keeps the way it faces and only moves its origin.
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1));

            Assert.True(plane.Translate(by).Normal.IsParallelTo(plane.Normal, Tight));
            Assert.Equal(by.Z, plane.Translate(by).SignedDistanceTo(new GeoPoint3(0, 0, 0)) * -1.0, 9);
        }

        [Fact]
        public void TheShapesBuiltOfManyPartsMoveTooAndKeepTheirMeasurements()
        {
            GeoVector3 by = By();

            GeoSolid3 cube = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            GeoObb3 obb = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 100, 100)).ToObb();
            var polygon = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0));
            GeoFace3 face = Pierced();

            Assert.Equal(cube.Volume, cube.Translate(by).Volume, 6);
            Assert.True(cube.Translate(by).Centroid.IsEqualTo(cube.Centroid.Add(by), Loose));

            Assert.True(obb.Translate(by).Center.IsEqualTo(obb.Center.Add(by), Loose));
            Assert.Equal(polygon.Area, polygon.Translate(by).Area, 6);
            Assert.Equal(chain.Length, chain.Translate(by).Length, 6);
            Assert.Equal(face.Area, face.Translate(by).Area, 6);

            // And every one of them lands where the transformation would have put it.
            GeoTransform3 same = GeoTransform3.Translation(by);

            Assert.True(polygon.Translate(by).IsEqualTo(polygon.TransformBy(same), Loose));
            Assert.True(chain.Translate(by).IsEqualTo(chain.TransformBy(same), Loose));
        }

        [Fact]
        public void AFaceInSpaceAnswersHowFarOffAPointIsAndWhereItsNearestMaterialIs()
        {
            GeoFace3 face = Pierced();

            // Over the material: straight down onto it.
            Assert.Equal(25.0, face.DistanceTo(new GeoPoint3(20, 20, 25)), 9);
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint3(20, 20, 25)).IsEqualTo(new GeoPoint3(20, 20, 0), Loose));

            // Over the hole: over nothing, so the nearest material is the rim and not the surface behind it.
            GeoPoint3 nearest = face.GetClosestPointOnBoundary(new GeoPoint3(50, 50, 0));

            Assert.Equal(10.0, face.DistanceTo(new GeoPoint3(50, 50, 0)), 9);
            Assert.Equal(0.0, nearest.Z, 9);
            Assert.Equal(10.0, new GeoPoint3(50, 50, 0).DistanceTo(nearest), 9);

            // Off the face altogether: the outline.
            Assert.Equal(20.0, face.DistanceTo(new GeoPoint3(120, 50, 0)), 9);
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint3(120, 50, 0)).IsEqualTo(new GeoPoint3(100, 50, 0), Loose));

            // Both ways of asking agree, and the tolerance forms with the ones that take none.
            Assert.Equal(Distance3.DistanceTo(face, new GeoPoint3(50, 50, 0)), face.DistanceTo(new GeoPoint3(50, 50, 0)), 9);
            Assert.Equal(face.DistanceTo(new GeoPoint3(50, 50, 0)), face.DistanceTo(new GeoPoint3(50, 50, 0), Tolerance.Global), 9);
            Assert.True(Projection3.ProjectToFace(face, new GeoPoint3(50, 50, 0)).IsEqualTo(nearest, Loose));
        }

        [Fact]
        public void AFaceInThePlaneAnswersWithItsBoundaryTheWayAPolygonDoes()
        {
            GeoFace2 face = PiercedFlat();

            // On the material, the answer is still on the boundary, which is where this parts company with
            // the distance: the distance calls a point on the material nought away. (10, 50) is ten from the
            // left-hand side and thirty from the rim of the hole, so there is no tie to settle.
            Assert.Equal(0.0, face.DistanceTo(new GeoPoint2(10, 50)), 12);
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint2(10, 50)).IsEqualTo(new GeoPoint2(0, 50), Loose));

            // In a hole, the answer is a point of that rim.
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint2(50, 45)).IsEqualTo(new GeoPoint2(50, 40), Loose));

            // Off the face, a point of the outline.
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint2(150, 50)).IsEqualTo(new GeoPoint2(100, 50), Loose));

            // The magnitude of the signed distance is how far that point is, which ties the two together.
            foreach (GeoPoint2 probe in new[] { new GeoPoint2(10, 50), new GeoPoint2(50, 45), new GeoPoint2(150, 50) })
            {
                Assert.Equal(
                    probe.DistanceTo(face.GetClosestPointOnBoundary(probe)),
                    Math.Abs(face.SignedDistanceTo(probe)), 7);
            }

            Assert.True(Projection2.ProjectToFace(face, new GeoPoint2(10, 50))
                .IsEqualTo(face.GetClosestPointOnBoundary(new GeoPoint2(10, 50), Tolerance.Global), Loose));

            // Where two pieces of the boundary are equally near, the earlier one is kept, as everywhere else
            // in this library: (20, 20) stands twenty from the bottom, twenty from the left and twenty from
            // the rim of the hole, and the bottom comes first.
            Assert.True(face.GetClosestPointOnBoundary(new GeoPoint2(20, 20)).IsEqualTo(new GeoPoint2(20, 0), Loose));
        }

        [Fact]
        public void APlaneAnswersWithTheFootOfThePerpendicular()
        {
            var plane = new GeoPlane3(new GeoPoint3(0, 0, 10), new GeoVector3(0, 0, 1));

            Assert.True(plane.GetClosestPointOnBoundary(new GeoPoint3(30, 40, 60)).IsEqualTo(new GeoPoint3(30, 40, 10), Loose));
            Assert.True(plane.GetClosestPointOnBoundary(new GeoPoint3(30, 40, -60)).IsEqualTo(new GeoPoint3(30, 40, 10), Loose));

            // A plane is endless, so the nearest point never falls back to an edge; the side is in the sign.
            Assert.Equal(50.0, plane.DistanceTo(new GeoPoint3(30, 40, 60)), 9);
            Assert.Equal(50.0, plane.SignedDistanceTo(new GeoPoint3(30, 40, 60)), 9);
            Assert.Equal(-70.0, plane.SignedDistanceTo(new GeoPoint3(30, 40, -60)), 9);

            Assert.True(plane.GetClosestPointOnBoundary(new GeoPoint3(30, 40, 60), Tolerance.Global)
                .IsEqualTo(plane.GetClosestPointOnBoundary(new GeoPoint3(30, 40, 60)), Loose));
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            Assert.Throws<ArgumentNullException>(() => Projection3.ProjectToFace(null, GeoPoint3.Origin));
            Assert.Throws<ArgumentNullException>(() => Projection3.ProjectToFace(null, GeoPoint3.Origin, Tolerance.Global));
            Assert.Throws<ArgumentNullException>(() => Projection2.ProjectToFace(null, new GeoPoint2(0, 0)));
            Assert.Throws<ArgumentNullException>(() => Distance3.DistanceTo((GeoFace3)null, GeoPoint3.Origin));
        }
    }
}
