using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Geometry
{
    /// <summary>
    /// Covers the face and the solid: the two types that carry openings.
    /// </summary>
    public class GeoSolid3Tests
    {
        /// <summary>
        /// Builds an axis-aligned box solid spanning two opposite corners.
        /// </summary>
        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max)
        {
            return new GeoAabb3(min, max).ToObb().ToSolid();
        }

        private static GeoSolid3 MakeUnitCube() => MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));

        #region Face

        [Fact]
        public void AFaceWithoutHolesHasTheAreaOfItsBoundary()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoFace3 face = new GeoFace3(square);

            Assert.Equal(100.0, face.Area, 9);
            Assert.Empty(face.Holes);
            Assert.True(face.Normal.IsEqualTo(GeoVector3.ZAxis));
        }

        [Fact]
        public void AHoleRemovesItsAreaFromTheFace()
        {
            GeoFace3 face = MakePlateWithHole();

            Assert.Equal(100.0 - 4.0, face.Area, 9);
            Assert.Single(face.Holes);
        }

        [Fact]
        public void AHoleOffThePlaneOfTheBoundaryIsRefused()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoPolygon3 floatingHole = new GeoPolygon3(
                new GeoPoint3(4.0, 4.0, 5.0),
                new GeoPoint3(6.0, 4.0, 5.0),
                new GeoPoint3(6.0, 6.0, 5.0),
                new GeoPoint3(4.0, 6.0, 5.0));

            Assert.Throws<ArgumentException>(() => new GeoFace3(boundary, new[] { floatingHole }));
        }

        [Fact]
        public void APointInAHoleIsOutsideTheFaceAndOnItsRimIsOnTheFace()
        {
            GeoFace3 face = MakePlateWithHole();

            Assert.Equal(PointLocation.Inside, face.Locate(new GeoPoint3(1.0, 1.0, 0.0)));
            Assert.Equal(PointLocation.OutSide, face.Locate(new GeoPoint3(5.0, 5.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, face.Locate(new GeoPoint3(4.0, 5.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, face.Locate(new GeoPoint3(0.0, 5.0, 0.0)));
        }

        [Fact]
        public void ASegmentAimedThroughAHoleDoesNotHitTheFace()
        {
            GeoFace3 face = MakePlateWithHole();

            GeoLine3 throughMaterial = new GeoLine3(new GeoPoint3(1.0, 1.0, -5.0), new GeoPoint3(1.0, 1.0, 5.0));
            GeoLine3 throughHole = new GeoLine3(new GeoPoint3(5.0, 5.0, -5.0), new GeoPoint3(5.0, 5.0, 5.0));

            Assert.True(face.TryIntersectWith(throughMaterial, out _));
            Assert.False(face.TryIntersectWith(throughHole, out _));
        }

        [Fact]
        public void FlippingAFaceReversesTheBoundaryAndTheHoles()
        {
            GeoFace3 flipped = MakePlateWithHole().Flip();

            Assert.True(flipped.Normal.IsEqualTo(GeoVector3.ZAxis.Negate()));
            Assert.Equal(MakePlateWithHole().Area, flipped.Area, 9);
        }

        private static GeoFace3 MakePlateWithHole()
        {
            GeoPolygon3 boundary = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(4.0, 4.0, 0.0),
                new GeoPoint3(6.0, 4.0, 0.0),
                new GeoPoint3(6.0, 6.0, 0.0),
                new GeoPoint3(4.0, 6.0, 0.0));

            return new GeoFace3(boundary, new[] { hole });
        }

        #endregion

        #region Solid

        [Fact]
        public void FewerThanFourFacesCannotEncloseAVolume()
        {
            GeoFace3 face = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(0.0, 1.0, 0.0)));

            Assert.Throws<ArgumentException>(() => new GeoSolid3(face, face, face));
        }

        [Fact]
        public void ACubeReportsItsVolumeAndSurfaceArea()
        {
            GeoSolid3 cube = MakeUnitCube();

            Assert.Equal(1000.0, cube.Volume, 6);
            Assert.Equal(600.0, cube.SurfaceArea, 6);
            Assert.Equal(1000.0, cube.NetVolume, 6);
        }

        [Fact]
        public void ATetrahedronReportsTheClassicalVolume()
        {
            GeoPoint3 a = new GeoPoint3(0.0, 0.0, 0.0);
            GeoPoint3 b = new GeoPoint3(6.0, 0.0, 0.0);
            GeoPoint3 c = new GeoPoint3(0.0, 6.0, 0.0);
            GeoPoint3 d = new GeoPoint3(0.0, 0.0, 6.0);

            GeoSolid3 tetrahedron = new GeoSolid3(
                new GeoFace3(new GeoPolygon3(a, c, b)),
                new GeoFace3(new GeoPolygon3(a, b, d)),
                new GeoFace3(new GeoPolygon3(b, c, d)),
                new GeoFace3(new GeoPolygon3(c, a, d)));

            // One sixth of the box spanned by the three edges at the right-angled corner.
            Assert.Equal(36.0, tetrahedron.Volume, 6);
            Assert.True(tetrahedron.IsClosed());
        }

        [Fact]
        public void VolumeDoesNotDependOnWhereTheSolidSitsInSpace()
        {
            GeoSolid3 atOrigin = MakeUnitCube();
            GeoSolid3 farAway = atOrigin.TransformBy(GeoTransform3.Translation(new GeoVector3(10000.0, -5000.0, 3000.0)));

            Assert.Equal(atOrigin.Volume, farAway.Volume, 3);
        }

        [Fact]
        public void VolumeSurvivesARotation()
        {
            GeoSolid3 cube = MakeUnitCube();
            GeoSolid3 rotated = cube.TransformBy(GeoTransform3.RotationAxis(new GeoVector3(1.0, 1.0, 1.0), 0.7));

            Assert.Equal(cube.Volume, rotated.Volume, 6);
            Assert.Equal(cube.SurfaceArea, rotated.SurfaceArea, 6);
        }

        [Fact]
        public void ScalingUniformlyCubesTheVolume()
        {
            GeoSolid3 cube = MakeUnitCube();
            GeoSolid3 scaled = cube.TransformBy(GeoTransform3.Scaling(2.0));

            Assert.Equal(cube.Volume * 8.0, scaled.Volume, 5);
        }

        [Fact]
        public void FacesWoundInwardsGiveTheSameUnsignedVolumeButTheOppositeSign()
        {
            GeoSolid3 cube = MakeUnitCube();

            List<GeoFace3> inverted = new List<GeoFace3>();
            foreach (GeoFace3 face in cube.Faces)
            {
                inverted.Add(face.Flip());
            }

            GeoSolid3 insideOut = new GeoSolid3(inverted);

            Assert.Equal(cube.Volume, insideOut.Volume, 6);
            Assert.Equal(cube.GetSignedVolume(), -insideOut.GetSignedVolume(), 6);
        }

        [Fact]
        public void CentroidOfACubeIsItsMiddle()
        {
            Assert.True(MakeUnitCube().Centroid.IsEqualTo(new GeoPoint3(5.0, 5.0, 5.0), new Tolerance(1E-6, 1E-6)));
        }

        [Fact]
        public void AnOpeningIsSubtractedFromTheNetVolume()
        {
            GeoSolid3 slab = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(4.0, 4.0, 4.0), new GeoPoint3(6.0, 6.0, 6.0));

            GeoSolid3 withOpening = slab.WithOpenings(new[] { duct });

            Assert.Equal(1000.0, withOpening.Volume, 6);
            Assert.Equal(1000.0 - 8.0, withOpening.NetVolume, 6);
        }

        [Fact]
        public void APointInsideAnOpeningIsOutsideTheSolid()
        {
            GeoSolid3 slab = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));
            GeoSolid3 duct = MakeBoxSolid(new GeoPoint3(4.0, 4.0, 4.0), new GeoPoint3(6.0, 6.0, 6.0));
            GeoSolid3 withOpening = slab.WithOpenings(new[] { duct });

            Assert.Equal(PointLocation.Inside, withOpening.Locate(new GeoPoint3(1.0, 1.0, 1.0)));
            Assert.Equal(PointLocation.OutSide, withOpening.Locate(new GeoPoint3(5.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.OnSide, withOpening.Locate(new GeoPoint3(4.0, 5.0, 5.0)));
        }

        [Fact]
        public void LocateSeparatesInteriorSurfaceAndOutside()
        {
            GeoSolid3 cube = MakeUnitCube();

            Assert.Equal(PointLocation.Inside, cube.Locate(new GeoPoint3(5.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.Inside, cube.Locate(new GeoPoint3(0.5, 0.5, 0.5)));
            Assert.Equal(PointLocation.OnSide, cube.Locate(new GeoPoint3(0.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.OnSide, cube.Locate(new GeoPoint3(0.0, 0.0, 0.0)));
            Assert.Equal(PointLocation.OutSide, cube.Locate(new GeoPoint3(-1.0, 5.0, 5.0)));
            Assert.Equal(PointLocation.OutSide, cube.Locate(new GeoPoint3(50.0, 50.0, 50.0)));
        }

        [Fact]
        public void ContainmentHoldsForARotatedSolidToo()
        {
            GeoSolid3 rotated = MakeUnitCube()
                .TransformBy(GeoTransform3.RotationAxis(new GeoVector3(1.0, 2.0, 3.0), 0.9));

            GeoPoint3 centre = rotated.Centroid;

            Assert.Equal(PointLocation.Inside, rotated.Locate(centre));
            Assert.Equal(PointLocation.OutSide, rotated.Locate(centre.Add(new GeoVector3(100.0, 0.0, 0.0))));
        }

        [Fact]
        public void DistanceIsZeroInsideAndPositiveOutside()
        {
            GeoSolid3 cube = MakeUnitCube();

            Assert.Equal(0.0, cube.DistanceTo(new GeoPoint3(5.0, 5.0, 5.0)), 9);
            Assert.Equal(5.0, cube.DistanceTo(new GeoPoint3(15.0, 5.0, 5.0)), 6);
        }

        [Fact]
        public void AClosedBoundaryIsRecognisedAndAnOpenOneIsNot()
        {
            GeoSolid3 cube = MakeUnitCube();

            Assert.True(cube.IsClosed());

            List<GeoFace3> withGap = new List<GeoFace3>(cube.Faces);
            withGap.RemoveAt(0);
            withGap.Add(withGap[0]);

            // Removing one face and duplicating another leaves rims that no second face closes.
            Assert.False(new GeoSolid3(withGap).IsClosed());
        }

        [Fact]
        public void BoundingBoxEnclosesTheWholeSolid()
        {
            GeoAabb3 bounds = MakeUnitCube().GetAabb();

            Assert.True(bounds.Min.IsEqualTo(GeoPoint3.Origin));
            Assert.True(bounds.Max.IsEqualTo(new GeoPoint3(10.0, 10.0, 10.0)));
        }

        [Fact]
        public void TriangulationCoversEveryFace()
        {
            GeoTriangle3[] mesh = MakeUnitCube().Triangulate();

            // Six quadrilateral faces, two triangles each.
            Assert.Equal(12, mesh.Length);

            double area = 0.0;
            foreach (GeoTriangle3 triangle in mesh)
            {
                area += triangle.Area;
            }

            Assert.Equal(600.0, area, 6);
        }

        [Fact]
        public void OverlappingSolidsCollideAndSeparatedOnesDoNot()
        {
            GeoSolid3 first = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10.0, 10.0, 10.0));

            Assert.True(GeometryHelper.SolidGeometry.Core.Collision3.CollidesWith(
                first, MakeBoxSolid(new GeoPoint3(5.0, 5.0, 5.0), new GeoPoint3(15.0, 15.0, 15.0))));

            Assert.False(GeometryHelper.SolidGeometry.Core.Collision3.CollidesWith(
                first, MakeBoxSolid(new GeoPoint3(20.0, 20.0, 20.0), new GeoPoint3(30.0, 30.0, 30.0))));
        }

        [Fact]
        public void ASolidWhollyInsideAnotherCollidesWithItEvenWithoutSurfaceContact()
        {
            GeoSolid3 outer = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(100.0, 100.0, 100.0));
            GeoSolid3 inner = MakeBoxSolid(new GeoPoint3(40.0, 40.0, 40.0), new GeoPoint3(60.0, 60.0, 60.0));

            Assert.True(GeometryHelper.SolidGeometry.Core.Collision3.CollidesWith(outer, inner));
            Assert.True(GeometryHelper.SolidGeometry.Core.Collision3.CollidesWith(inner, outer));
        }

        [Fact]
        public void CloneIsIndependentAndEqual()
        {
            GeoSolid3 cube = MakeUnitCube();
            GeoSolid3 copy = cube.Clone();

            Assert.NotSame(cube, copy);
            Assert.Equal(cube, copy);
            Assert.Equal(cube.Volume, copy.Volume, 9);
        }

        [Fact]
        public void ToleranceEqualityIgnoresTheOrderTheFacesAreListedIn()
        {
            GeoSolid3 cube = MakeUnitCube();

            List<GeoFace3> shuffled = new List<GeoFace3>(cube.Faces);
            GeoFace3 first = shuffled[0];
            shuffled.RemoveAt(0);
            shuffled.Add(first);

            GeoSolid3 reordered = new GeoSolid3(shuffled);

            Assert.True(cube.IsEqualTo(reordered));
            Assert.False(cube.Equals(reordered));
        }

        #endregion
    }
}
