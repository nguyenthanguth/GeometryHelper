using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A face between two kept cells is found twice, once each way round — but not always vertex for vertex.
    /// </summary>
    /// <remarks>
    /// The booleans dropped such a pair only when the two copies matched exactly. A cut that reached one cell
    /// and not the other left one copy in two pieces, and both copies survived as a sheet of no thickness
    /// inside the result. The volume never noticed, since the two cancel; but a point beside the sheet
    /// measured one millimetre to the boundary instead of five, the surface mesh carried the sheet, and
    /// splitting the body into its pieces threw.
    /// </remarks>
    public class InnerSheetTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>A block standing half out of the cube's front face, turned a little about a slanted axis.</summary>
        private static GeoSolid3 Tilted()
            => Box(10, -5, 45, 50, 5, 95).TransformBy(
                GeoTransform3.RotationAxis(new GeoPoint3(30, 0, 70), new GeoVector3(0.1, -0.7, 0.55), 0.12));

        /// <summary>Counts the pairs of faces lying back to back in one plane and sharing some area.</summary>
        private static int Sheets(GeoSolid3 solid)
        {
            int found = 0;
            IReadOnlyList<GeoFace3> faces = solid.Faces;

            for (int i = 0; i < faces.Count; i++)
            {
                for (int j = i + 1; j < faces.Count; j++)
                {
                    if (faces[i].Boundary.Normal.DotProduct(faces[j].Boundary.Normal) > -0.999999
                        || Math.Abs(faces[i].GetPlane().SignedDistanceTo(faces[j].Boundary[0])) > 1E-6)
                    {
                        continue;
                    }

                    if (Boolean3.Intersect(faces[i], faces[j]).Sum(patch => patch.Area) > 1E-6)
                    {
                        found++;
                    }
                }
            }

            return found;
        }

        [Fact]
        public void AUnionLeavesNoSheetInside()
        {
            Assert.True(Box(0, 0, 0, 100, 100, 100).TryUnion(Tilted(), out GeoSolid3 union));

            Assert.Equal(0, Sheets(union));
            Assert.Equal(1010000.0, union.Volume, 3);
        }

        [Fact]
        public void APointBesideWhereTheSheetWasMeasuresToTheRealBoundary()
        {
            // Five in from the cube's front face. The sheet ran a millimetre above the point, and the point
            // measured to it.
            Assert.True(Box(0, 0, 0, 100, 100, 100).TryUnion(Tilted(), out GeoSolid3 union));

            Assert.Equal(-5.0, union.SignedDistanceTo(new GeoPoint3(80, 5, 48.6)), 6);
        }

        [Fact]
        public void TheUnionIsOnePiece()
        {
            Assert.True(Box(0, 0, 0, 100, 100, 100).TryUnion(Tilted(), out GeoSolid3 union));

            GeoSolid3 piece = Assert.Single(union.SplitShells());
            Assert.Equal(union.Volume, piece.Volume, 3);
        }

        [Fact]
        public void TakingTheCubeFromTheBlockLeavesNoSheetEither()
        {
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100);

            Assert.True(cube.TrySubtract(Tilted(), out GeoSolid3 bitten));
            Assert.True(Tilted().TrySubtract(cube, out GeoSolid3 outside));

            Assert.Equal(0, Sheets(bitten));
            Assert.Equal(0, Sheets(outside));
            Assert.Equal(990000.0, bitten.Volume, 3);
            Assert.Equal(10000.0, outside.Volume, 3);
        }

        [Fact]
        public void RandomPairsOfBoxesLeaveNoSheet()
        {
            // Half the boxes turned about a random axis; corners on a grid of ten so that faces land in one
            // plane often. Pairs 127 and 234 of this sequence left sheets.
            var rng = new Random(20260927);
            int Whole(int low, int high) => rng.Next(low, high + 1);
            double Real(double low, double high) => low + (high - low) * rng.NextDouble();

            GeoSolid3 Next()
            {
                int x0 = Whole(0, 6) * 10, y0 = Whole(0, 6) * 10, z0 = Whole(0, 6) * 10;
                int sx = Whole(1, 5) * 10, sy = Whole(1, 5) * 10, sz = Whole(1, 5) * 10;
                GeoSolid3 box = Box(x0, y0, z0, x0 + sx, y0 + sy, z0 + sz);

                if (rng.NextDouble() < 0.5)
                {
                    return box;
                }

                var centre = new GeoPoint3(x0 + sx / 2.0, y0 + sy / 2.0, z0 + sz / 2.0);
                var axis = new GeoVector3(Real(-1, 1), Real(-1, 1), Real(-1, 1) + 0.01);

                return box.TransformBy(GeoTransform3.RotationAxis(centre, axis, Real(0.05, 1.5)));
            }

            for (int k = 0; k < 250; k++)
            {
                GeoSolid3 a = Next();
                GeoSolid3 b = Next();
                a.TryIntersect(b, out GeoSolid3 shared);

                Assert.True(a.TryUnion(b, out GeoSolid3 union), $"pair {k}");

                foreach (GeoSolid3 result in new[] { shared, union, a.TrySubtract(b, out GeoSolid3 ab) ? ab : null, b.TrySubtract(a, out GeoSolid3 ba) ? ba : null })
                {
                    if (result == null)
                    {
                        continue;
                    }

                    Assert.True(Sheets(result) == 0, $"pair {k} left a sheet");
                    Assert.NotEmpty(result.SplitShells());
                }
            }
        }
        [Fact]
        public void ASheetInsideABodyStaysWithItAndIsNoPieceOfItsOwn()
        {
            var square = new GeoPolygon3(new GeoPoint3(20, 20, 50), new GeoPoint3(80, 20, 50), new GeoPoint3(80, 80, 50), new GeoPoint3(20, 80, 50));
            var sheeted = new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces.Concat(new[] { new GeoFace3(square), new GeoFace3(square.Flip()) }));

            GeoSolid3 piece = Assert.Single(sheeted.SplitShells());
            Assert.Equal(8, piece.Faces.Count);
            Assert.Equal(1000000.0, piece.Volume, 3);
        }

        [Fact]
        public void ASmallFaceAHairOffALongOnesPlaneIsTakenFromItEitherWayRound()
        {
            // A face 30 m long, and a small one lying back to back on it with one corner 0.04 mm up: the small face lies
            // in the long one's plane, but the long one's far end stands millimetres off the small one's. Met one way
            // round, taking the long face from the small one refused the pair as lying in different planes, and the
            // boolean threw; met the other, the pair was never found, and the two stayed in the body as a sheet.
            Tolerance tolerance = Tolerance.Default;
            var along = new GeoFace3(new GeoPolygon3(new[] { P(0, 0, 0), P(30000, 0, 0), P(30000, 400, 0), P(0, 400, 0) }, tolerance));
            var against = new GeoFace3(new GeoPolygon3(new[] { P(100, 100, 0), P(100, 300, 0), P(300, 300, 0.04), P(300, 100, 0) }, tolerance));

            foreach (List<GeoFace3> faces in new[] { new List<GeoFace3> { along, against }, new List<GeoFace3> { against, along } })
            {
                // What the two share goes from both: the long face keeps a hole where the small one lay, and nothing of the
                // small one is left.
                GeoFace3 left = Assert.Single(Boolean3.CancelBackToBack(faces, tolerance));

                Assert.True(left.Normal.IsCodirectionalTo(GeoVector3.ZAxis, tolerance));
                Assert.Single(left.Holes);
                Assert.InRange(left.Area, 30000.0 * 400.0 - 200.0 * 200.0 - 1.0, 30000.0 * 400.0 - 200.0 * 200.0 + 1.0);
            }
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        [Fact]
        public void ASheetNoPieceHoldsIsDropped()
        {
            var square = new GeoPolygon3(new GeoPoint3(200, 20, 50), new GeoPoint3(260, 20, 50), new GeoPoint3(260, 80, 50), new GeoPoint3(200, 80, 50));
            var stray = new GeoSolid3(Box(0, 0, 0, 100, 100, 100).Faces.Concat(new[] { new GeoFace3(square), new GeoFace3(square.Flip()) }));

            GeoSolid3 piece = Assert.Single(stray.SplitShells());
            Assert.Equal(6, piece.Faces.Count);
        }
    }
}
