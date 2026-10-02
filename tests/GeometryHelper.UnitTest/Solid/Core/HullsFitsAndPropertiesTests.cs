using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Convex hulls, boxes fitted to points, mass properties and sections.
    /// </summary>
    public class HullsFitsAndPropertiesTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static double Cross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        #region Hulls

        [Fact]
        public void TheHullOfASquareAndWhatIsInsideItIsTheSquare()
        {
            var points = new List<GeoPoint2> { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) };
            points.AddRange(new[] { new GeoPoint2(50, 50), new GeoPoint2(50, 0), new GeoPoint2(100, 30), new GeoPoint2(20, 70) });

            GeoPolygon2 hull = ConvexHull2.Of(points);

            Assert.Equal(4, hull.VertexCount);
            Assert.Equal(10000.0, hull.SignedArea, 9);
        }

        [Fact]
        public void TheHullOfRandomPointsHoldsThemAllAndOnlyTurnsLeft()
        {
            var rng = new Random(5);
            var points = Enumerable.Range(0, 300).Select(_ => new GeoPoint2(rng.NextDouble() * 1000 - 500, rng.NextDouble() * 600 - 300)).ToList();

            GeoPolygon2 hull = ConvexHull2.Of(points);

            Assert.All(points, p => Assert.NotEqual(PointLocation.OutSide, hull.Locate(p)));
            Assert.All(hull.Vertices, v => Assert.Contains(v, points));

            for (int i = 0; i < hull.VertexCount; i++)
            {
                Assert.True(Cross(hull[i], hull[(i + 1) % hull.VertexCount], hull[(i + 2) % hull.VertexCount]) > 0);
            }
        }

        [Fact]
        public void PointsInOneLineHaveNoHull()
        {
            var line = Enumerable.Range(0, 10).Select(i => new GeoPoint2(i, 2 * i)).ToList();

            Assert.False(ConvexHull2.TryOf(line, out _));
            Assert.Throws<ArgumentException>(() => ConvexHull2.Of(line));
            Assert.False(ConvexHull3.TryOf(Enumerable.Range(0, 10).Select(i => new GeoPoint3(i, i * 3 % 7, 0)), out _));
        }

        [Fact]
        public void TheHullOfACubeAndWhatIsInsideItIsTheCube()
        {
            var rng = new Random(9);
            var points = new List<GeoPoint3>();

            foreach (int x in new[] { 0, 100 })
            {
                foreach (int y in new[] { 0, 100 })
                {
                    foreach (int z in new[] { 0, 100 })
                    {
                        points.Add(new GeoPoint3(x, y, z));
                    }
                }
            }

            points.AddRange(Enumerable.Range(0, 200).Select(_ => new GeoPoint3(rng.NextDouble() * 100, rng.NextDouble() * 100, rng.NextDouble() * 100)));

            GeoSolid3 hull = ConvexHull3.Of(points);

            Assert.Equal(6, hull.Faces.Count);
            Assert.Equal(1000000.0, hull.Volume, 6);
            Assert.True(hull.IsClosed());
        }

        [Fact]
        public void TheHullOfRandomPointsHoldsThemAllAndIsConvex()
        {
            var rng = new Random(13);
            var points = Enumerable.Range(0, 400).Select(_ =>
            {
                var d = new GeoVector3(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5, rng.NextDouble() - 0.5);
                return GeoPoint3.Origin.Add(d.Normalize().Multiply(50 + rng.NextDouble() * 30)).Add(new GeoVector3(1000, -2000, 300));
            }).ToList();

            GeoSolid3 hull = ConvexHull3.Of(points);

            Assert.True(hull.IsClosed());
            Assert.All(points, p => Assert.NotEqual(PointLocation.OutSide, hull.Locate(p)));

            foreach (GeoFace3 face in hull.Faces)
            {
                GeoPlane3 plane = face.GetPlane();
                Assert.All(points, p => Assert.True(plane.SignedDistanceTo(p) <= 1E-6));
            }
        }

        #endregion

        #region Fitting boxes

        [Fact]
        public void ATurnedRectangleIsFoundAgainFromItsCornersAndInside()
        {
            var rng = new Random(17);
            var rectangle = new GeoRectangle2(new GeoPoint2(100, 50), 40, 10, 0.5);
            var points = rectangle.GetVertices().ToList();
            points.AddRange(Enumerable.Range(0, 50).Select(_ => rectangle.CoordinateSystem.ToGlobal(new GeoPoint2((rng.NextDouble() - 0.5) * 40, (rng.NextDouble() - 0.5) * 10))));

            GeoRectangle2 fitted = GeoRectangle2.Fit(points);

            Assert.Equal(400.0, fitted.Area, 9);
            Assert.Equal(new[] { 10.0, 40.0 }, new[] { fitted.Width, fitted.Height }.OrderBy(v => v).Select(v => Math.Round(v, 9)));
            Assert.All(points, p => Assert.NotEqual(PointLocation.OutSide, fitted.Locate(p)));
        }

        [Fact]
        public void AFittedRectangleHoldsRandomPointsInNoMoreThanTheirSquareBox()
        {
            var rng = new Random(19);
            var points = Enumerable.Range(0, 100).Select(_ => new GeoPoint2(rng.NextDouble() * 300, rng.NextDouble() * 80 + rng.NextDouble() * 50)).ToList();

            GeoRectangle2 fitted = GeoRectangle2.Fit(points);
            double square = (points.Max(p => p.X) - points.Min(p => p.X)) * (points.Max(p => p.Y) - points.Min(p => p.Y));

            Assert.True(fitted.Area <= square + 1E-9);
            Assert.All(points, p => Assert.NotEqual(PointLocation.OutSide, fitted.Locate(p)));
        }

        [Fact]
        public void PointsInOneLineFitARectangleOfNoHeight()
        {
            GeoRectangle2 fitted = GeoRectangle2.Fit(Enumerable.Range(0, 5).Select(i => new GeoPoint2(3 * i, 4 * i)));

            Assert.Equal(0.0, fitted.Area, 9);
            Assert.Equal(20.0, Math.Max(fitted.Width, fitted.Height), 9);
        }

        [Fact]
        public void ATurnedBoxIsFoundAgainFromItsCornersAndInside()
        {
            var rng = new Random(23);
            var box = new GeoObb3(new GeoPoint3(10, 20, 30), 40, 20, 10, new GeoVector3(1, 1, 0), new GeoVector3(-1, 1, 1));
            var points = box.ToSolid().Faces.SelectMany(f => f.Boundary.Vertices).ToList();
            points.AddRange(Enumerable.Range(0, 60).Select(_ => box.CoordinateSystem.ToGlobal(new GeoPoint3((rng.NextDouble() - 0.5) * 40, (rng.NextDouble() - 0.5) * 20, (rng.NextDouble() - 0.5) * 10))));

            GeoObb3 fitted = GeoObb3.Fit(points);

            Assert.Equal(8000.0, fitted.Volume, 6);
            Assert.All(points, p => Assert.True(fitted.Contains(p)));
        }

        [Fact]
        public void AFittedBoxHoldsRandomPointsInNoMoreThanTheirSquareBox()
        {
            var rng = new Random(29);
            GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.8);
            var points = Enumerable.Range(0, 200).Select(_ => turn.Transform(new GeoPoint3(rng.NextDouble() * 200, rng.NextDouble() * 50, rng.NextDouble() * 20))).ToList();

            GeoObb3 fitted = GeoObb3.Fit(points);
            GeoAabb3 square = GeoAabb3.FromPoints(points);

            Assert.True(fitted.Volume <= square.Volume + 1E-6);
            Assert.True(fitted.Volume >= ConvexHull3.Of(points).Volume - 1E-6);
            Assert.All(points, p => Assert.True(fitted.Contains(p)));
        }

        [Fact]
        public void FlatPointsFitABoxOfNoDepth()
        {
            var flat = new[] { new GeoPoint3(0, 0, 5), new GeoPoint3(30, 0, 5), new GeoPoint3(30, 10, 5), new GeoPoint3(0, 10, 5), new GeoPoint3(12, 3, 5) };

            GeoObb3 fitted = GeoObb3.Fit(flat);

            Assert.Equal(0.0, fitted.Volume, 9);
            Assert.All(flat, p => Assert.True(fitted.Contains(p)));
        }

        #endregion

        #region Mass properties

        [Fact]
        public void ABlockHasTheTextbookMoments()
        {
            GeoSolid3 block = Box(10, 20, 30, 110, 80, 50);
            MassProperties3 mass = block.GetMassProperties(2.0);

            double v = 100.0 * 60 * 20;
            Assert.Equal(v, mass.Volume, 6);
            Assert.Equal(2 * v, mass.Mass, 6);
            Assert.True(mass.Centroid.DistanceTo(new GeoPoint3(60, 50, 40)) < 1E-9);
            Assert.Equal(2 * v * (60 * 60 + 20 * 20) / 12, mass.Ixx, 3);
            Assert.Equal(2 * v * (100 * 100 + 20 * 20) / 12, mass.Iyy, 3);
            Assert.Equal(2 * v * (100 * 100 + 60 * 60) / 12, mass.Izz, 3);
            Assert.Equal(0.0, mass.Ixy, 3);
            Assert.Equal(0.0, mass.Iyz, 3);
            Assert.Equal(0.0, mass.Izx, 3);
            Assert.Equal(new[] { mass.Izz, mass.Iyy, mass.Ixx }.OrderBy(m => m).Select(m => Math.Round(m, 3)), mass.PrincipalMoments.Select(m => Math.Round(m, 3)));
            Assert.Equal(2 * (100.0 * 60 + 60 * 20 + 20 * 100), mass.SurfaceArea, 6);
        }

        [Fact]
        public void TurningABlockTurnsItsAxesAndKeepsItsMoments()
        {
            GeoSolid3 block = Box(0, 0, 0, 100, 60, 20);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(5, 5, 5), new GeoVector3(1, 2, 3), 0.7);

            MassProperties3 straight = block.GetMassProperties();
            MassProperties3 turned = block.TransformBy(turn).GetMassProperties();

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(straight.PrincipalMoments[i], turned.PrincipalMoments[i], 3);
                GeoVector3 expected = turn.Transform(straight.PrincipalAxes[i]);
                Assert.Equal(1.0, Math.Abs(expected.DotProduct(turned.PrincipalAxes[i])), 9);
            }

            Assert.True(turned.Centroid.DistanceTo(turn.Transform(straight.Centroid)) < 1E-9);
            Assert.Equal(1.0, turned.PrincipalAxes[0].CrossProduct(turned.PrincipalAxes[1]).DotProduct(turned.PrincipalAxes[2]), 9);
        }

        [Fact]
        public void AnOpeningComesOutOfTheWeightAndMovesTheCentroid()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(60, 40, -1, 80, 60, 21) });
            MassProperties3 mass = plate.GetMassProperties();

            double vp = 100.0 * 100 * 20, vh = 20.0 * 20 * 20, v = vp - vh;
            double cx = (vp * 50 - vh * 70) / v;
            double izz = vp * (100.0 * 100 + 100 * 100) / 12 + vp * (50 - cx) * (50 - cx)
                - (vh * (20.0 * 20 + 20 * 20) / 12 + vh * (70 - cx) * (70 - cx));

            Assert.Equal(v, mass.Volume, 6);
            Assert.True(mass.Centroid.DistanceTo(new GeoPoint3(cx, 50, 10)) < 1E-9);
            Assert.Equal(izz, mass.Izz, 2);
            Assert.Equal(2 * (10000.0 - 400) + 4 * 100 * 20 + 4 * 20 * 20, mass.SurfaceArea, 6);
        }

        [Fact]
        public void ABodyItsOpeningsTakeWholeWeighsNothing()
        {
            // An opening larger than the plate every way leaves no material. Read as the faces it was given, the plate
            // weighed what it would without the opening.
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(-10, -10, -10, 110, 110, 30) });
            MassProperties3 mass = plate.GetMassProperties(7.85E-6);

            Assert.Equal(0.0, mass.Volume);
            Assert.Equal(0.0, mass.Mass);
            Assert.Equal(0.0, mass.SurfaceArea);
        }

        [Fact]
        public void AMomentAboutAnotherAxisGoesByTheParallelAxisTheorem()
        {
            MassProperties3 mass = Box(0, 0, 0, 100, 60, 20).GetMassProperties();

            // About the bottom edge running along X: the centroid is 30 and 10 from it.
            double expected = mass.Ixx + mass.Mass * (30.0 * 30 + 10 * 10);

            Assert.Equal(expected, mass.GetMomentAbout(GeoPoint3.Origin, GeoVector3.XAxis), 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => Box(0, 0, 0, 1, 1, 1).GetMassProperties(0.0));
        }

        #endregion

        #region Sections

        [Fact]
        public void ABoxCutAcrossGivesItsSection()
        {
            GeoFace3 cut = Assert.Single(Box(0, 0, 0, 100, 50, 20).Section(new GeoPlane3(new GeoPoint3(0, 0, 10), GeoVector3.ZAxis)));

            Assert.Equal(5000.0, cut.Area, 6);
            Assert.Equal(1.0, cut.Boundary.Normal.Z, 9);
            Assert.All(cut.Boundary.Vertices, p => Assert.Equal(10.0, p.Z, 9));
        }

        [Fact]
        public void AHoleThePlanePassesThroughIsAHoleInTheSection()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });
            GeoFace3 cut = Assert.Single(plate.Section(new GeoPlane3(new GeoPoint3(0, 0, 10), GeoVector3.ZAxis)));

            Assert.Single(cut.Holes);
            Assert.Equal(10000.0 - 400, cut.Area, 6);
        }

        [Fact]
        public void TwoPiecesGiveTwoFacesAndAMissGivesNone()
        {
            Assert.True(Box(0, 0, 0, 10, 10, 10).TryUnion(Box(50, 0, 0, 60, 10, 10), out GeoSolid3 two));

            Assert.Equal(2, two.Section(new GeoPlane3(new GeoPoint3(0, 0, 5), GeoVector3.ZAxis)).Length);
            Assert.Empty(two.Section(new GeoPlane3(new GeoPoint3(0, 0, 50), GeoVector3.ZAxis)));
            Assert.Empty(two.Section(new GeoPlane3(new GeoPoint3(0, 0, 10), GeoVector3.ZAxis)));
        }

        [Fact]
        public void APlaneTouchingABodyInTwoPartsAlongAFaceGivesNoSection()
        {
            // The plane parts the two blocks, lying along the top of the lower: it cuts no material, whichever way it faces.
            var both = new GeoSolid3(Box(0, 0, 0, 1000, 1000, 1000).Faces.Concat(Box(0, 0, 2000, 1000, 1000, 3000).Faces));

            Assert.Empty(both.Section(new GeoPlane3(new GeoPoint3(0, 0, 1000), GeoVector3.ZAxis)));
            Assert.Empty(both.Section(new GeoPlane3(new GeoPoint3(0, 0, 1000), GeoVector3.ZAxis.Negate())));
        }

        [Fact]
        public void APlaneAlongTheFloorOfANotchGivesOnlyWhatItCuts()
        {
            // An L in plan: the plane y = 1000 cuts the arm at x 0..1000 and lies along the floor of the notch beyond it.
            var plan = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(2000, 0, 0), new GeoPoint3(2000, 1000, 0), new GeoPoint3(1000, 1000, 0), new GeoPoint3(1000, 2000, 0), new GeoPoint3(0, 2000, 0) };
            GeoSolid3 ell = GeoSolid3.Extrude(new GeoPolygon3(plan), new GeoVector3(0, 0, 500));

            foreach (GeoVector3 facing in new[] { GeoVector3.YAxis, GeoVector3.YAxis.Negate() })
            {
                GeoFace3 cut = Assert.Single(ell.Section(new GeoPlane3(new GeoPoint3(0, 1000, 0), facing)));
                Assert.Equal(1000.0 * 500, cut.Area, 6);
                Assert.Equal(1.0, cut.Boundary.Normal.DotProduct(facing), 9);
                Assert.All(cut.Boundary.Vertices, p => Assert.InRange(p.X, -1E-9, 1000 + 1E-9));
            }
        }

        [Fact]
        public void APlaneBetweenTwoBlocksStandingOnEachOtherGivesWhereTheyMeet()
        {
            // The upper block stands half over the lower: the plane between them has material on both sides only there.
            var stacked = new GeoSolid3(Box(0, 0, 0, 1000, 1000, 1000).Faces.Concat(Box(500, 0, 1000, 1500, 1000, 2000).Faces));
            GeoFace3 cut = Assert.Single(stacked.Section(new GeoPlane3(new GeoPoint3(0, 0, 1000), GeoVector3.ZAxis)));

            Assert.Equal(500.0 * 1000, cut.Area, 6);
            Assert.All(cut.Boundary.Vertices, p => Assert.Equal(1000.0, p.Z, 9));
        }

        [Fact]
        public void ACubeCutOnTheSlantGivesItsDiagonalSection()
        {
            var slant = new GeoPlane3(new GeoPoint3(50, 50, 50), new GeoVector3(1, 1, 0));
            GeoFace3 cut = Assert.Single(Box(0, 0, 0, 100, 100, 100).Section(slant));

            Assert.Equal(100.0 * Math.Sqrt(2) * 100, cut.Area, 6);
            Assert.Equal(1.0, cut.Boundary.Normal.DotProduct(slant.Normal), 9);
        }

        #endregion
    }
}
