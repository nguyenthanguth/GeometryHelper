using System;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Covers the polygon: a flat closed region whose coplanarity is enforced at construction.
    /// </summary>
    public class GeoPolygon3Tests
    {
        private static GeoPolygon3 MakeSquare() => new GeoPolygon3(
            new GeoPoint3(0.0, 0.0, 0.0),
            new GeoPoint3(10.0, 0.0, 0.0),
            new GeoPoint3(10.0, 10.0, 0.0),
            new GeoPoint3(0.0, 10.0, 0.0));

        [Fact]
        public void AreaAndPerimeterMatchTheShape()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.Equal(100.0, square.Area, 9);
            Assert.Equal(40.0, square.Length, 9);
            Assert.Equal(4, square.VertexCount);
            Assert.Equal(4, square.EdgeCount);
        }

        [Fact]
        public void NormalFollowsTheVertexOrder()
        {
            Assert.True(MakeSquare().Normal.IsEqualTo(GeoVector3.ZAxis));
            Assert.True(MakeSquare().Flip().Normal.IsEqualTo(GeoVector3.ZAxis.Negate()));
        }

        [Fact]
        public void AreaIsIndependentOfOrientation()
        {
            Assert.Equal(MakeSquare().Area, MakeSquare().Flip().Area, 9);
        }

        [Fact]
        public void ATiltedPolygonStillReportsItsTrueArea()
        {
            // A unit square tilted 45 degrees about the X axis keeps its area.
            double diagonal = Math.Sqrt(2.0) / 2.0;

            GeoPolygon3 tilted = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(1.0, diagonal, diagonal),
                new GeoPoint3(0.0, diagonal, diagonal));

            Assert.Equal(1.0, tilted.Area, 9);
            Assert.True(tilted.Normal.IsPerpendicularTo(GeoVector3.XAxis));
        }

        [Fact]
        public void ThreeCornersAreFlatWhateverTheTolerance()
        {
            // Three corners share a plane whatever they are. At a planar tolerance of nought, how far they stand from the
            // plane measured through them came out as rounding, not nought, and corners far from the origin, where a
            // model in millimetres has them, were refused as not flat.
            var none = new Tolerance();
            var random = new Random(7);

            for (int i = 0; i < 1000; i++)
            {
                GeoPoint3[] corners = { Near(random), Near(random), Near(random) };

                Assert.Equal(3, new GeoPolygon3(corners, none).VertexCount);
            }
        }

        /// <summary>
        /// A face of a steel girder in an IFC file, as read in the girder's own frame, one corner below the other seven: a
        /// hundredth in the file, at the edge of the default as it was then; a thousandth here, flat at the default planar
        /// tolerance with nothing to spare.
        /// </summary>
        internal static readonly GeoPoint3[] GirderFace =
        {
            new GeoPoint3(7989.99999991793, 20, 189.99900000000002), new GeoPoint3(7954.99999991793, 20, 190),
            new GeoPoint3(257.000004518777, 19.99999999959249, 190), new GeoPoint3(257.00003389886115, 149.999999999622, 190),
            new GeoPoint3(7954.99999991793, 150, 190), new GeoPoint3(7989.99999991793, 150, 190),
            new GeoPoint3(7992.99999991793, 150, 190), new GeoPoint3(7992.99999991793, 20, 190),
        };

        /// <summary>
        /// Where the girder stands: some 600 m out.
        /// </summary>
        internal static readonly GeoVector3 GirderPlace = new GeoVector3(195703.959999986, 580442.140000014, -15100);

        [Fact]
        public void APolygonAtTheEdgeOfFlatIsMovedWholeHoweverFar()
        {
            // Moved to where the girder stands, rounding put the low corner a hair past the planar tolerance, and the
            // face was refused, and the body with it. A move that keeps every length keeps the polygon as flat as it was.
            var face = new GeoPolygon3(GirderFace, Tolerance.Default);

            foreach (GeoTransform3 move in new[]
            {
                GeoTransform3.Translation(GirderPlace),
                GeoTransform3.Translation(GirderPlace) * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(0.2, -0.3, 1.0), 0.7),
            })
            {
                GeoPolygon3 placed = face.TransformBy(move);

                Assert.Equal(8, placed.VertexCount);
                Assert.Equal(face.Area, placed.Area, 6);
                Assert.Equal(move.Transform(GirderFace[0]), placed[0]);
            }
        }

        private static GeoPoint3 Near(Random random)
            => new GeoPoint3(24000.0 + random.NextDouble() * 1000.0, -26000.0 + random.NextDouble() * 1000.0, 3000.0 + random.NextDouble() * 1000.0);

        [Fact]
        public void NonCoplanarVerticesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 5.0)));
        }

        [Fact]
        public void CollinearVerticesAreRefusedBecauseTheyEncloseNothing()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0),
                new GeoPoint3(2.0, 0.0, 0.0)));
        }

        [Fact]
        public void FewerThanThreeDistinctVerticesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(1.0, 0.0, 0.0)));
        }

        [Fact]
        public void ARepeatedClosingVertexIsRemoved()
        {
            GeoPolygon3 square = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0),
                new GeoPoint3(0.0, 0.0, 0.0));

            Assert.Equal(4, square.VertexCount);
            Assert.Equal(100.0, square.Area, 9);
        }

        [Fact]
        public void EdgesCloseTheLoop()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.True(square.GetEdgeAt(3).EndPoint.IsEqualTo(square[0]));
            Assert.Equal(4, square.GetEdges().Length);
            Assert.Throws<ArgumentOutOfRangeException>(() => square.GetEdgeAt(4));
        }

        [Fact]
        public void LocateSeparatesInteriorBoundaryAndOutside()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.Equal(PointLocation.Inside, square.Locate(new GeoPoint3(5.0, 5.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, square.Locate(new GeoPoint3(5.0, 0.0, 0.0)));
            Assert.Equal(PointLocation.OnSide, square.Locate(new GeoPoint3(0.0, 0.0, 0.0)));
            Assert.Equal(PointLocation.OutSide, square.Locate(new GeoPoint3(15.0, 5.0, 0.0)));
        }

        [Fact]
        public void APointOffThePlaneIsOutsideHoweverWellItLinesUp()
        {
            Assert.Equal(PointLocation.OutSide, MakeSquare().Locate(new GeoPoint3(5.0, 5.0, 3.0)));
        }

        [Fact]
        public void AConcavePolygonExcludesTheNotchItCutsOut()
        {
            // An L shape occupying the lower-left of a 10 x 10 square.
            GeoPolygon3 lShape = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 4.0, 0.0),
                new GeoPoint3(4.0, 4.0, 0.0),
                new GeoPoint3(4.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            Assert.Equal(64.0, lShape.Area, 9);
            Assert.Equal(PointLocation.Inside, lShape.Locate(new GeoPoint3(2.0, 2.0, 0.0)));
            Assert.Equal(PointLocation.Inside, lShape.Locate(new GeoPoint3(8.0, 2.0, 0.0)));
            Assert.Equal(PointLocation.Inside, lShape.Locate(new GeoPoint3(2.0, 8.0, 0.0)));
            Assert.Equal(PointLocation.OutSide, lShape.Locate(new GeoPoint3(8.0, 8.0, 0.0)));
        }

        [Fact]
        public void CentroidOfASquareIsItsMiddle()
        {
            Assert.True(MakeSquare().Centroid.IsEqualTo(new GeoPoint3(5.0, 5.0, 0.0)));
        }

        [Fact]
        public void CentroidIsWeightedByAreaNotByVertexCount()
        {
            // A triangle with two vertices clustered together: the plain vertex average would be pulled
            // towards the cluster, the area centroid is not.
            GeoPolygon3 triangle = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(9.0, 0.0, 0.0),
                new GeoPoint3(0.0, 9.0, 0.0));

            Assert.True(triangle.Centroid.IsEqualTo(new GeoPoint3(3.0, 3.0, 0.0)));
        }

        [Fact]
        public void CentroidOfAConcavePolygonAccountsForTheNotch()
        {
            // The L shape is a 10 x 4 bar plus a 4 x 6 leg. Combining their centroids by area gives
            // (40*5 + 24*2) / 64 = 3.875 on both axes. Weighting the fan by unsigned area instead would
            // count the overhanging triangles as material and pull the answer off this value.
            GeoPolygon3 lShape = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 4.0, 0.0),
                new GeoPoint3(4.0, 4.0, 0.0),
                new GeoPoint3(4.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            Assert.True(lShape.Centroid.IsEqualTo(new GeoPoint3(3.875, 3.875, 0.0)));
        }

        [Fact]
        public void CentroidDoesNotDependOnWhichVertexTheLoopStartsAt()
        {
            GeoPolygon3 square = MakeSquare();
            GeoPolygon3 rotated = new GeoPolygon3(square[2], square[3], square[0], square[1]);

            Assert.True(square.Centroid.IsEqualTo(rotated.Centroid));
        }

        [Fact]
        public void TriangulationCoversTheSameArea()
        {
            GeoPolygon3 lShape = new GeoPolygon3(
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0, 4.0, 0.0),
                new GeoPoint3(4.0, 4.0, 0.0),
                new GeoPoint3(4.0, 10.0, 0.0),
                new GeoPoint3(0.0, 10.0, 0.0));

            GeoTriangle3[] fan = lShape.Triangulate();

            Assert.Equal(lShape.VertexCount - 2, fan.Length);

            // The fan reaches outside a concave polygon, so the areas only agree once the signs are taken
            // into account: this is the sum against the polygon normal, not the sum of magnitudes.
            double signedArea = 0.0;
            foreach (GeoTriangle3 triangle in fan)
            {
                signedArea += triangle.GetAreaVector().DotProduct(lShape.Normal) * 0.5;
            }

            Assert.Equal(lShape.Area, signedArea, 9);
        }

        [Fact]
        public void DistanceMeasuresToTheFilledSurface()
        {
            GeoPolygon3 square = MakeSquare();

            Assert.Equal(4.0, square.DistanceTo(new GeoPoint3(5.0, 5.0, 4.0)), 9);
            Assert.Equal(0.0, square.DistanceTo(new GeoPoint3(5.0, 5.0, 0.0)), 9);
            Assert.Equal(5.0, square.DistanceTo(new GeoPoint3(15.0, 5.0, 0.0)), 9);
        }

        [Fact]
        public void SegmentCrossingThePolygonIsFound()
        {
            GeoPolygon3 square = MakeSquare();
            GeoLine3 through = new GeoLine3(new GeoPoint3(5.0, 5.0, -3.0), new GeoPoint3(5.0, 5.0, 3.0));
            GeoLine3 beside = new GeoLine3(new GeoPoint3(50.0, 5.0, -3.0), new GeoPoint3(50.0, 5.0, 3.0));

            Assert.True(square.TryIntersectWith(through, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint3(5.0, 5.0, 0.0)));
            Assert.False(square.TryIntersectWith(beside, out _));
        }

        [Fact]
        public void ToleranceEqualityAllowsARotationOfTheLoopButNotAFlip()
        {
            GeoPolygon3 square = MakeSquare();
            GeoPolygon3 rotated = new GeoPolygon3(square[2], square[3], square[0], square[1]);

            Assert.True(square.IsEqualTo(rotated));
            Assert.False(square.IsEqualTo(square.Flip()));
        }

        [Fact]
        public void ToPolylineWritesOutTheClosingVertex()
        {
            GeoPolyline3 chain = MakeSquare().ToPolyline();

            Assert.Equal(5, chain.VertexCount);
            Assert.True(chain.StartPoint.IsEqualTo(chain.EndPoint));
            Assert.Equal(40.0, chain.Length, 9);
        }

        [Fact]
        public void CloneIsIndependentAndKeepsTheMeasurements()
        {
            GeoPolygon3 square = MakeSquare();
            GeoPolygon3 copy = square.Clone();

            Assert.NotSame(square, copy);
            Assert.Equal(square, copy);
            Assert.Equal(square.Area, copy.Area, 9);
            Assert.True(square.Normal.IsEqualTo(copy.Normal));
        }
    }
}
