using System;
using System.Collections.Generic;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using SolidGeometry.Spatial;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers the pairs of shapes the Core classes gained after the audit: the face overloads that were
    /// only reachable from the instance side, the box and solid crossings, and the shape-to-shape
    /// distances that previously stopped at a point.
    /// </summary>
    public class CorePairCoverageTests
    {
        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        private static GeoSolid3 MakeCube() => MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

        private static GeoPolygon3 MakePlate() => new GeoPolygon3(
            new GeoPoint3(0, 0, 0),
            new GeoPoint3(10, 0, 0),
            new GeoPoint3(10, 10, 0),
            new GeoPoint3(0, 10, 0));

        private static GeoFace3 MakePlateWithHole()
        {
            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(6, 6, 0),
                new GeoPoint3(4, 6, 0));

            return new GeoFace3(MakePlate(), new[] { hole });
        }

        #region Intersection3 face overloads

        [Fact]
        public void TheStaticFaceOverloadAgreesWithTheInstanceOne()
        {
            GeoFace3 plate = MakePlateWithHole();
            GeoLine3 throughMaterial = new GeoLine3(new GeoPoint3(1, 1, -5), new GeoPoint3(1, 1, 5));

            bool fromStatic = Intersection3.TryIntersectWith(throughMaterial, plate, out GeoPoint3 staticHit);
            bool fromInstance = plate.TryIntersectWith(throughMaterial, out GeoPoint3 instanceHit);

            Assert.True(fromStatic);
            Assert.Equal(fromStatic, fromInstance);
            Assert.True(staticHit.IsEqualTo(instanceHit));
        }

        [Fact]
        public void AHoleIsRespectedByTheStaticFaceOverloads()
        {
            GeoFace3 plate = MakePlateWithHole();

            GeoLine3 throughHole = new GeoLine3(new GeoPoint3(5, 5, -5), new GeoPoint3(5, 5, 5));
            GeoRay3 rayThroughHole = new GeoRay3(new GeoPoint3(5, 5, -5), GeoVector3.ZAxis);
            GeoRay3 rayThroughMaterial = new GeoRay3(new GeoPoint3(1, 1, -5), GeoVector3.ZAxis);

            Assert.False(Intersection3.TryIntersectWith(throughHole, plate, out _));
            Assert.False(Intersection3.TryIntersectWith(rayThroughHole, plate, out _));
            Assert.True(Intersection3.TryIntersectWith(rayThroughMaterial, plate, out _));
        }

        #endregion

        #region Intersection3 solid and box crossings

        [Fact]
        public void ASegmentThroughASolidEntersAndLeavesIt()
        {
            GeoPoint3[] hits = Intersection3.GetIntersections(
                new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5)), MakeCube());

            Assert.Equal(2, hits.Length);
            Assert.True(hits[0].IsEqualTo(new GeoPoint3(0, 5, 5)));
            Assert.True(hits[1].IsEqualTo(new GeoPoint3(10, 5, 5)));
        }

        [Fact]
        public void TheWallsOfAnOpeningCountAsSurface()
        {
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(4, 4, 0), new GeoPoint3(6, 6, 10));
            GeoSolid3 pierced = MakeCube().WithOpenings(new[] { duct });

            // Across the middle: into the material, into the void, back into material, out.
            GeoPoint3[] hits = Intersection3.GetIntersections(
                new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5)), pierced);

            Assert.Equal(4, hits.Length);
        }

        [Fact]
        public void ASegmentMissingASolidCrossesNothing()
        {
            Assert.Empty(Intersection3.GetIntersections(
                new GeoLine3(new GeoPoint3(-5, 50, 5), new GeoPoint3(20, 50, 5)), MakeCube()));
        }

        [Fact]
        public void AnAxisAlignedBoxIsCrossedLikeAnOrientedOne()
        {
            GeoAabb3 bounds = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoLine3 line = new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            GeoPoint3[] viaAabb = Intersection3.GetIntersections(line, bounds);
            GeoPoint3[] viaObb = Intersection3.GetIntersections(line, bounds.ToObb());

            Assert.Equal(viaObb.Length, viaAabb.Length);
            Assert.Equal(2, viaAabb.Length);
            Assert.Empty(Intersection3.GetIntersections(line, GeoAabb3.Empty));
        }

        [Fact]
        public void ARayEntersAndLeavesABoxAheadOfItOnly()
        {
            GeoAabb3 bounds = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));

            GeoRay3 towards = new GeoRay3(new GeoPoint3(-5, 5, 5), GeoVector3.XAxis);
            GeoRay3 away = new GeoRay3(new GeoPoint3(-5, 5, 5), GeoVector3.XAxis.Negate());

            Assert.Equal(2, Intersection3.GetIntersections(towards, bounds).Length);
            Assert.Empty(Intersection3.GetIntersections(away, bounds));
        }

        [Fact]
        public void ARayStartingInsideABoxLeavesItOnce()
        {
            GeoAabb3 bounds = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoRay3 outward = new GeoRay3(new GeoPoint3(5, 5, 5), GeoVector3.XAxis);

            Assert.Single(Intersection3.GetIntersections(outward, bounds));
        }

        #endregion

        #region Distance3 shape pairs

        [Fact]
        public void TwoTrianglesThatMeetAreAtDistanceZero()
        {
            GeoTriangle3 flat = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));
            GeoTriangle3 crossing = new GeoTriangle3(
                new GeoPoint3(2, 2, -5), new GeoPoint3(2, 2, 5), new GeoPoint3(8, 2, 5));

            Assert.Equal(0.0, Distance3.DistanceTo(flat, crossing), 9);
        }

        [Fact]
        public void TwoTrianglesApartAreMeasuredBetweenTheirNearestParts()
        {
            GeoTriangle3 lower = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));
            GeoTriangle3 upper = new GeoTriangle3(
                new GeoPoint3(0, 0, 7), new GeoPoint3(10, 0, 7), new GeoPoint3(0, 10, 7));

            Assert.Equal(7.0, Distance3.DistanceTo(lower, upper), 9);
            Assert.Equal(Distance3.DistanceTo(lower, upper), Distance3.DistanceTo(upper, lower), 9);
        }

        [Fact]
        public void ASegmentIsMeasuredToATriangleAndToAPolygon()
        {
            GeoTriangle3 triangle = new GeoTriangle3(GeoPoint3.Origin, new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0));
            GeoLine3 above = new GeoLine3(new GeoPoint3(1, 1, 4), new GeoPoint3(2, 2, 9));

            Assert.Equal(4.0, Distance3.DistanceTo(above, triangle), 9);
            Assert.Equal(4.0, Distance3.DistanceTo(above, MakePlate()), 9);

            GeoLine3 crossing = new GeoLine3(new GeoPoint3(1, 1, -4), new GeoPoint3(1, 1, 4));
            Assert.Equal(0.0, Distance3.DistanceTo(crossing, triangle), 9);
        }

        [Fact]
        public void ASegmentReachingIntoASolidIsAtDistanceZero()
        {
            GeoSolid3 cube = MakeCube();

            Assert.Equal(0.0, Distance3.DistanceTo(new GeoLine3(new GeoPoint3(5, 5, 5), new GeoPoint3(50, 5, 5)), cube), 9);
            Assert.Equal(5.0, Distance3.DistanceTo(new GeoLine3(new GeoPoint3(15, 5, 5), new GeoPoint3(50, 5, 5)), cube), 6);
        }

        [Fact]
        public void TwoSolidsApartAreMeasuredBetweenTheirSurfaces()
        {
            GeoSolid3 first = MakeCube();
            GeoSolid3 second = MakeBoxSolid(new GeoPoint3(17, 0, 0), new GeoPoint3(27, 10, 10));

            Assert.Equal(7.0, Distance3.DistanceTo(first, second), 6);
            Assert.Equal(Distance3.DistanceTo(first, second), Distance3.DistanceTo(second, first), 6);
        }

        [Fact]
        public void TwoSolidsThatOverlapAreAtDistanceZero()
        {
            GeoSolid3 first = MakeCube();
            GeoSolid3 overlapping = MakeBoxSolid(new GeoPoint3(5, 5, 5), new GeoPoint3(15, 15, 15));
            GeoSolid3 nested = MakeBoxSolid(new GeoPoint3(3, 3, 3), new GeoPoint3(7, 7, 7));

            Assert.Equal(0.0, Distance3.DistanceTo(first, overlapping), 9);
            Assert.Equal(0.0, Distance3.DistanceTo(first, nested), 9);
        }

        [Fact]
        public void TheIndexedDistanceAgreesWithComparingEveryTrianglePair()
        {
            GeoSolid3 first = MakeCube();
            GeoSolid3 second = MakeBoxSolid(new GeoPoint3(23, 4, 1), new GeoPoint3(33, 14, 11));

            GeoTriangle3[] mesh1 = first.Triangulate();
            GeoTriangle3[] mesh2 = second.Triangulate();

            double scanned = double.MaxValue;
            foreach (GeoTriangle3 t1 in mesh1)
            {
                foreach (GeoTriangle3 t2 in mesh2)
                {
                    scanned = Math.Min(scanned, Distance3.DistanceTo(t1, t2));
                }
            }

            Assert.Equal(scanned, new GeoBvh3(mesh1).DistanceTo(new GeoBvh3(mesh2)), 9);
            Assert.Equal(scanned, Distance3.DistanceTo(first, second), 9);
        }

        [Fact]
        public void TwoOrientedBoxesAreMeasuredAsTheBodiesTheyAre()
        {
            GeoObb3 first = new GeoObb3(new GeoPoint3(5, 5, 5), 10, 10, 10);
            GeoObb3 apart = new GeoObb3(new GeoPoint3(22, 5, 5), 10, 10, 10);
            GeoObb3 touching = new GeoObb3(new GeoPoint3(15, 5, 5), 10, 10, 10);

            Assert.Equal(7.0, Distance3.DistanceTo(first, apart), 6);
            Assert.Equal(0.0, Distance3.DistanceTo(first, touching), 9);
        }

        [Fact]
        public void BoxToBoxAndBoxToPointDistancesAreAvailableFromDistance3()
        {
            GeoAabb3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoAabb3 second = new GeoAabb3(new GeoPoint3(13, 0, 0), new GeoPoint3(20, 10, 10));

            Assert.Equal(3.0, Distance3.DistanceTo(first, second), 9);
            Assert.Equal(0.0, Distance3.DistanceTo(first, first), 9);
            Assert.Equal(5.0, Distance3.DistanceTo(first, new GeoPoint3(15, 5, 5)), 9);
            Assert.True(double.IsPositiveInfinity(Distance3.DistanceTo(GeoAabb3.Empty, first)));
        }

        [Fact]
        public void BoxToBoxDistanceSeparatesOnEachAxis()
        {
            GeoAabb3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(1, 1, 1));
            GeoAabb3 diagonal = new GeoAabb3(new GeoPoint3(4, 5, 1), new GeoPoint3(5, 6, 2));

            // Gaps of 3 and 4 on X and Y, none on Z.
            Assert.Equal(5.0, Distance3.DistanceTo(first, diagonal), 9);
        }

        #endregion

        #region Collision3 mixed pairs

        [Fact]
        public void AnOrientedBoxIsTestedAgainstAnAxisAlignedOne()
        {
            GeoObb3 rotated = new GeoObb3(
                new GeoPoint3(5, 5, 5), 10, 2, 2,
                new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 0));

            Assert.True(Collision3.CollidesWith(rotated, new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10))));
            Assert.False(Collision3.CollidesWith(rotated, new GeoAabb3(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60))));
            Assert.False(Collision3.CollidesWith(rotated, GeoAabb3.Empty));
        }

        [Fact]
        public void ACurveIsTestedAgainstASolid()
        {
            GeoSolid3 cube = MakeCube();

            Assert.True(Collision3.CollidesWith(new GeoLine3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5)), cube));
            Assert.True(Collision3.CollidesWith(new GeoLine3(new GeoPoint3(4, 5, 5), new GeoPoint3(6, 5, 5)), cube));
            Assert.False(Collision3.CollidesWith(new GeoLine3(new GeoPoint3(-5, 50, 5), new GeoPoint3(20, 50, 5)), cube));

            GeoPolyline3 chain = new GeoPolyline3(
                new GeoPoint3(-5, 50, 5), new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(Collision3.CollidesWith(chain, cube));
        }

        [Fact]
        public void ARegionIsTestedAgainstABody()
        {
            GeoSolid3 cube = MakeCube();

            // A plate slicing through the middle of the cube.
            GeoPolygon3 crossing = new GeoPolygon3(
                new GeoPoint3(-5, -5, 5), new GeoPoint3(15, -5, 5),
                new GeoPoint3(15, 15, 5), new GeoPoint3(-5, 15, 5));

            // A plate well clear of it.
            GeoPolygon3 apart = new GeoPolygon3(
                new GeoPoint3(-5, -5, 50), new GeoPoint3(15, -5, 50),
                new GeoPoint3(15, 15, 50), new GeoPoint3(-5, 15, 50));

            // A plate sitting entirely within the body, touching nothing.
            GeoPolygon3 nested = new GeoPolygon3(
                new GeoPoint3(3, 3, 5), new GeoPoint3(7, 3, 5),
                new GeoPoint3(7, 7, 5), new GeoPoint3(3, 7, 5));

            Assert.True(Collision3.CollidesWith(crossing, cube));
            Assert.False(Collision3.CollidesWith(apart, cube));
            Assert.True(Collision3.CollidesWith(nested, cube));
            Assert.True(Collision3.CollidesWith(crossing, new GeoObb3(new GeoPoint3(5, 5, 5), 10, 10, 10)));
        }

        [Fact]
        public void AFaceIsTestedAgainstABody()
        {
            GeoSolid3 slim = MakeBoxSolid(new GeoPoint3(4.5, 4.5, -5), new GeoPoint3(5.5, 5.5, 5));

            // The bar passes exactly through the hole, so it touches no material.
            Assert.False(Collision3.CollidesWith(MakePlateWithHole(), slim));

            GeoSolid3 offset = MakeBoxSolid(new GeoPoint3(1, 1, -5), new GeoPoint3(2, 2, 5));

            Assert.True(Collision3.CollidesWith(MakePlateWithHole(), offset));
        }

        #endregion
    }
}
