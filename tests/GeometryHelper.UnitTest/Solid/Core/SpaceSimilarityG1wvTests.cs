using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A polygon, a face or a body scaled evenly, turned, moved or mirrored besides, is carried as a move carries it, and one
    /// stretched unevenly or sheared is checked within the global tolerance as far loosened as the transformation stretches
    /// or shrinks it; two circles whose centres stand a few tenths of a millionth apart cross at both crossings.
    /// </summary>
    /// <remarks>
    /// A transformation that did not keep every length had the shape checked flat and enclosing an area within the global
    /// tolerance again, so a top 0.0009 off flat scaled by 10 was refused as not flat and a pit a few hundred-thousandths
    /// deep cut at a millionth and scaled by a tenth as enclosing no area. The foot of the radical line of two circles was
    /// carried along the line between their centres, which leant out of their plane by the rounding of the centres, so the
    /// crossings found from it lay off both circles. No test sets the global tolerance.
    /// </remarks>
    public class SpaceSimilarityG1wvTests
    {
        private static Tolerance Within(double point) =>
            new Tolerance(point, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, point);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static int Corners(GeoSolid3 body) =>
            body.Faces.Sum(f => f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount));

        // The block 100 by 70 by 40 whose top has the corner over (100, 70) raised by e.
        private static GeoSolid3 RaisedBlock(double e)
        {
            Tolerance tolerance = Tolerance.Default;
            GeoPoint3[] b = { P(0, 0, 0), P(100, 0, 0), P(100, 70, 0), P(0, 70, 0) };
            GeoPoint3[] t = { P(0, 0, 40), P(100, 0, 40), P(100, 70, 40 + e), P(0, 70, 40) };
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(new[] { b[0], b[3], b[2], b[1] }, tolerance), null, tolerance),
                new GeoFace3(new GeoPolygon3(t, tolerance), null, tolerance),
            };

            for (int j = 0; j < 4; j++)
            {
                int k = (j + 1) % 4;
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { b[j], b[k], t[k], t[j] }, tolerance), null, tolerance));
            }

            return new GeoSolid3(faces);
        }

        [Fact]
        public void ABlockWhoseTopIsNineTenThousandthsOffFlatScaledByTenOrAThousand_StaysClosedWithItsVolumeScaled()
        {
            GeoSolid3 block = RaisedBlock(0.0009);
            double volume = block.GetVolume(Tolerance.Default);

            // By 1 000, as a model in metres is brought into millimetres; turned and moved besides.
            foreach (double scale in new[] { 10.0, 1000.0 })
            {
                GeoTransform3 transform =
                    GeoTransform3.Translation(new GeoVector3(500, -300, 20))
                    * GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7)
                    * GeoTransform3.Scaling(scale);

                GeoSolid3 scaled = block.TransformBy(transform);

                Tolerance within = Within(1E-3 * scale);
                Assert.True(scaled.IsClosed(within), "scaled by " + scale);
                Assert.Equal(Corners(block), Corners(scaled));
                Assert.True(
                    Math.Abs(scaled.GetVolume(within) - volume * scale * scale * scale)
                        <= 1E-9 * volume * scale * scale * scale,
                    "scaled by " + scale
                );

                for (int i = 0; i < block.Faces.Count; i++)
                {
                    Assert.Equal(block.Faces[i].Area * scale * scale, scaled.Faces[i].Area, 9);
                }
            }
        }

        [Fact]
        public void ABlockWithAPitFiveHundredThousandthsDeepCutAtAMillionthScaledByATenthTurnedAndMirrored_StaysClosed()
        {
            Tolerance millionth = Within(1E-6);
            GeoSolid3 box = new GeoObb3(P(50, 35, 20), 100, 70, 40).ToSolid();

            // A pit 0.1 by 0.1 and 0.00005 deep in the top: its walls are 0.00005 wide and enclose 0.000005 square units.
            GeoSolid3 tool = new GeoObb3(P(40, 30, 40 - 0.00005 + 5), 0.1, 0.1, 10).ToSolid();

            Assert.True(box.TrySubtract(tool, out GeoSolid3 pitted, millionth));
            Assert.True(pitted.IsClosed(millionth));

            double volume = pitted.GetVolume(millionth);

            var transforms = new[]
            {
                GeoTransform3.Scaling(0.1),
                GeoTransform3.Mirror(new GeoPlane3(P(10, 0, 0), new GeoVector3(1, 1, 0)))
                    * GeoTransform3.RotationAxis(new GeoVector3(0, 1, 1), 1.1)
                    * GeoTransform3.Scaling(0.1),
            };

            foreach (GeoTransform3 transform in transforms)
            {
                GeoSolid3 scaled = pitted.TransformBy(transform);

                Assert.True(scaled.IsClosed(Within(1E-7)));
                Assert.Equal(Corners(pitted), Corners(scaled));
                Assert.True(Math.Abs(scaled.GetVolume(Within(1E-7)) - volume * 0.001) <= 1E-11 * volume * 0.001);
            }
        }

        [Fact]
        public void ATriangleWithItsCornersCutByThreeTenThousandthsShrunkByATenThousandthAndMirrored_KeepsItsSixCornersAndItsAreaScaled()
        {
            // The triangle (0, 0, 0), (10, 0, 0), (10, 0, 10) with every corner cut by 0.0003, built at a millionth.
            GeoPoint3[] corners =
            {
                P(0.0003 / Math.Sqrt(2), 0, 0.0003 / Math.Sqrt(2)),
                P(0.0003, 0, 0),
                P(10 - 0.0003, 0, 0),
                P(10, 0, 0.0003),
                P(10, 0, 10 - 0.0003),
                P(10 - 0.0003 / Math.Sqrt(2), 0, 10 - 0.0003 / Math.Sqrt(2)),
            };
            var triangle = new GeoPolygon3(corners, Within(1E-6));

            GeoTransform3 shrink =
                GeoTransform3.Mirror(new GeoPlane3(P(0, 0, 0), GeoVector3.XAxis)) * GeoTransform3.Scaling(0.0001);

            GeoPolygon3 shrunk = triangle.TransformBy(shrink);

            Assert.Equal(6, shrunk.VertexCount);

            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(shrink.Transform(corners[i]), shrunk[i]);
            }

            // 0.0000005 square units, 0.0007 wide.
            Assert.True(Math.Abs(shrunk.Area - triangle.Area * 1E-8) <= 1E-12 * shrunk.Area, shrunk.Area.ToString());

            // Mirrored in the plane x = 0, the corners run the other way round, and the normal turns over with them.
            Assert.True(
                shrunk.Normal.IsEqualTo(
                    new GeoVector3(triangle.Normal.X, -triangle.Normal.Y, -triangle.Normal.Z),
                    Within(1E-12)
                )
            );
        }

        [Fact]
        public void APolygonNineTenThousandthsOffFlatUnderAScalingByTenWrittenWithItsNumbersRoundedToFloats_IsKept()
        {
            GeoPolygon3 top = RaisedBlock(0.0009).Faces[1].Boundary;
            GeoTransform3 exact = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7) * GeoTransform3.Scaling(10);
            var rounded = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    rounded[i, j] = (float)exact[i, j];
                }
            }

            GeoPolygon3 scaled = top.TransformBy(new GeoTransform3(rounded));

            Assert.Equal(4, scaled.VertexCount);
            // The area scaled by the mean square of the axes, as long as a float rounds them: 100 within 1E-7 of it.
            Assert.True(Math.Abs(scaled.Area - top.Area * 100) <= 1E-7 * top.Area * 100, scaled.Area.ToString());
        }

        [Fact]
        public void ABlockWhoseTopIsNineTenThousandthsOffFlatStretchedTenTimesUpwards_StaysClosedWithItsVolumeTenTimes()
        {
            GeoSolid3 block = RaisedBlock(0.0009);
            double volume = block.GetVolume(Tolerance.Default);

            // The corner stands 0.009 off the plane of the other three now, within the global tolerance stretched by 10.
            GeoSolid3 stretched = block.TransformBy(GeoTransform3.Scaling(1, 1, 10));

            Assert.True(stretched.IsClosed(Within(1E-2)));
            Assert.Equal(Corners(block), Corners(stretched));
            Assert.Equal(volume * 10, stretched.GetVolume(Within(1E-2)), 6);
        }

        [Fact]
        public void ASquareThreeThousandthsAcrossShrunkUnevenlyToUnderAThousandthWide_IsKeptWithItsAreaScaled()
        {
            var square = new GeoPolygon3(
                new[] { P(0, 0, 0), P(0.003, 0, 0), P(0.003, 0.003, 0), P(0, 0.003, 0) },
                Tolerance.Default
            );

            // 0.0006 by 0.00075, 0.00094 wide: within the global tolerance shrunk by a half, the most the scaling stretches.
            GeoPolygon3 shrunk = square.TransformBy(GeoTransform3.Scaling(0.2, 0.25, 0.5));

            Assert.Equal(4, shrunk.VertexCount);
            Assert.True(Math.Abs(shrunk.Area - 0.0006 * 0.00075) <= 1E-12 * shrunk.Area, shrunk.Area.ToString());
        }

        [Fact]
        public void APolygonStandingInAPlaneTheTransformationFlattensToALine_IsStillRefused()
        {
            var standing = new GeoPolygon3(
                new[] { P(0, 0, 0), P(10, 0, 0), P(10, 0, 10), P(0, 0, 10) },
                Tolerance.Default
            );

            // Flattened onto the floor, and projected along (1, 0, 1), a direction in its own plane, and moved.
            var along = new double[4, 4]
            {
                { 0.5, 0, -0.5, 7 },
                { 0, 1, 0, -3 },
                { -0.5, 0, 0.5, 2 },
                { 0, 0, 0, 1 },
            };

            Assert.Throws<ArgumentException>(() => standing.TransformBy(GeoTransform3.Scaling(1, 1, 0)));
            Assert.Throws<ArgumentException>(() => standing.TransformBy(new GeoTransform3(along)));
        }

        [Fact]
        public void ASquareFifteenTenThousandthsAcrossStandingAcrossAStretchByTen_IsKeptAsItWas()
        {
            // In the plane x = 5, enclosing 0.00000225 square units and 0.0015 wide: a stretch along X leaves it as it is.
            var square = new GeoPolygon3(
                new[] { P(5, 0, 0), P(5, 0.0015, 0), P(5, 0.0015, 0.0015), P(5, 0, 0.0015) },
                Tolerance.Default
            );

            GeoPolygon3 stretched = square.TransformBy(GeoTransform3.Scaling(10, 1, 1));

            Assert.Equal(4, stretched.VertexCount);
            Assert.True(
                Math.Abs(stretched.Area - 0.0015 * 0.0015) <= 1E-12 * stretched.Area,
                stretched.Area.ToString()
            );
        }

        [Fact]
        public void APolygonBuiltAtAHundredthEighteenTenThousandthsOffFlatShearedAndHalved_IsKeptAsItWas()
        {
            var hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);
            var quad = new GeoPolygon3(
                new[] { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0.0018), P(0, 100, 0) },
                hundredth
            );
            var matrix = new double[4, 4];
            matrix[0, 0] = 0.5;
            matrix[1, 1] = 0.5;
            matrix[2, 2] = 0.5;
            matrix[0, 1] = 0.05;
            matrix[3, 3] = 1;

            // Under a thousandth off flat once halved, as it was kept before; not refused within a tolerance halved with it.
            GeoPolygon3 sheared = quad.TransformBy(new GeoTransform3(matrix));

            Assert.Equal(4, sheared.VertexCount);
        }

        [Fact]
        public void TwoArcsAboutCentresFourteenHundredMillionthsApartAtATenMillionth_CrossTwiceOnBothAsAHundredTimesLarger()
        {
            // Circles of radius 39.7175765 and 0.0000000487 less, in a plane leaning every way, about centres 0.00000014
            // apart: they cross 1.4E-8 radians apart, 13.86 along the line between the centres.
            var frame = new GeoCoordinateSystem3(
                new GeoPlane3(P(9.68, -18.63, 6.1), new GeoVector3(0.373, -0.919, 0.129))
            );
            double radius = 39.717576470094535,
                apart = 1.3966711830951286E-07,
                less = 4.874832135163453E-08,
                turn = 2.1;

            foreach (double scale in new[] { 1.0, 100.0 })
            {
                Func<double, double, GeoPoint3> at = (x, y) =>
                {
                    GeoPoint3 g = frame.ToGlobal(P(x, y, 0));
                    return P(g.X * scale, g.Y * scale, g.Z * scale);
                };
                double cx = apart * Math.Cos(turn),
                    cy = apart * Math.Sin(turn),
                    other = radius - less,
                    gap = turn + Math.PI;
                GeoArc3 one = GeoArc3.FromThreePoints(
                    at(radius * Math.Cos(gap + 0.2), radius * Math.Sin(gap + 0.2)),
                    at(radius * Math.Cos(turn), radius * Math.Sin(turn)),
                    at(radius * Math.Cos(gap - 0.2), radius * Math.Sin(gap - 0.2))
                );
                GeoArc3 two = GeoArc3.FromThreePoints(
                    at(cx + other * Math.Cos(gap + 0.3), cy + other * Math.Sin(gap + 0.3)),
                    at(cx + other * Math.Cos(turn), cy + other * Math.Sin(turn)),
                    at(cx + other * Math.Cos(gap - 0.3), cy + other * Math.Sin(gap - 0.3))
                );
                Tolerance tolerance = Within(1E-7 * scale);

                GeoPoint3[] met = one.GetIntersections(two, tolerance);

                Assert.Equal(2, met.Length);

                foreach (GeoPoint3 point in met)
                {
                    Assert.True(
                        one.DistanceTo(point) <= 1E-3 * tolerance.EqualPoint,
                        "at a scale of " + scale + ": " + one.DistanceTo(point)
                    );
                    Assert.True(
                        two.DistanceTo(point) <= 1E-3 * tolerance.EqualPoint,
                        "at a scale of " + scale + ": " + two.DistanceTo(point)
                    );
                }

                // One either side of the line between the centres, about 69.6 degrees off it.
                GeoVector3 between = one.Center.GetVectorTo(two.Center);
                GeoVector3 side = one.Normal.CrossProduct(between);
                Assert.True(
                    one.Center.GetVectorTo(met[0]).DotProduct(side) * one.Center.GetVectorTo(met[1]).DotProduct(side)
                        < 0.0
                );
            }
        }
    }
}
