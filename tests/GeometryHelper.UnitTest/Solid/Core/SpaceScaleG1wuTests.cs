using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment shorter than the vector tolerance meets an arc where it crosses it, two circles whose centres stand closer
    /// than the vector tolerance meet at both crossings, a curved loop flattens to a polygon checked enclosing an area as any
    /// polygon in space is, and a body with a step under a thousandth stays closed, its volume scaled, however it is scaled.
    /// </summary>
    /// <remarks>
    /// The points of an arc on a line were found as the one point the line starts from wherever its direction was no longer
    /// than the vector tolerance, 1E-5, read as a length, so a segment that short, or two circles that close, met as at one
    /// size and not at another. A curved loop flattened within a point tolerance of nought took a sliver for an area. No test
    /// sets the global tolerance.
    /// </remarks>
    public class SpaceScaleG1wuTests
    {
        private static Tolerance Within(double point) =>
            new Tolerance(point, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, point);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        // The arc of radius 10 about the point given, in the floor, running counter-clockwise from 2.5 radians before the X
        // axis to 2.5 after it.
        private static GeoArc3 Arc(double radius, GeoPoint3 centre, double scale) =>
            GeoArc3.FromThreePoints(
                P((centre.X + radius * Math.Cos(-2.5)) * scale, (centre.Y + radius * Math.Sin(-2.5)) * scale, 0),
                P((centre.X + radius) * scale, centre.Y * scale, 0),
                P((centre.X + radius * Math.Cos(2.5)) * scale, (centre.Y + radius * Math.Sin(2.5)) * scale, 0)
            );

        [Fact]
        public void ASegmentTwoMillionthsLongCrossingAnArcAtAMillionth_MeetsItWhereItCrossesAsAHundredTimesLarger()
        {
            foreach (double scale in new[] { 1.0, 100.0 })
            {
                GeoArc3 arc = GeoArc3.FromThreePoints(P(10 * scale, 0, 0), P(0, 10 * scale, 0), P(-10 * scale, 0, 0));
                var segment = new GeoLine3(P(0, (10 - 5E-7) * scale, 0), P(0, (10 + 1.5E-6) * scale, 0));

                GeoPoint3[] met = arc.GetIntersections(segment, Within(1E-6 * scale));

                Assert.Single(met);
                Assert.True(
                    met[0].DistanceTo(P(0, 10 * scale, 0)) <= 1E-12 * scale,
                    "at a scale of " + scale + ": " + met[0]
                );
            }
        }

        [Fact]
        public void TwoCirclesWhoseCentresStandTwoMillionthsApartCrossingAtAMillionth_MeetAtBothCrossingsAsAHundredTimesLarger()
        {
            foreach (double scale in new[] { 1.0, 100.0 })
            {
                GeoArc3 one = Arc(10, P(0, 0, 0), scale);
                GeoArc3 other = Arc(10 - 1E-6, P(2E-6, 0, 0), scale);

                GeoPoint3[] met = one.GetIntersections(other, Within(1E-6 * scale));

                // The radical line x = (a^2 - 2 R d - d^2) / 2a, a = 2E-6 the distance between the centres and d = -1E-6 the
                // difference of the radii.
                double x = (4E-12 + 2E-5 - 1E-12) / 4E-6;
                double y = Math.Sqrt(100 - x * x);

                Assert.Equal(2, met.Length);
                Assert.Contains(met, p => p.DistanceTo(P(x * scale, y * scale, 0)) <= 1E-6 * scale);
                Assert.Contains(met, p => p.DistanceTo(P(x * scale, -y * scale, 0)) <= 1E-6 * scale);
            }
        }

        [Fact]
        public void AHalfDiscAtAMillionthWhoseChordsEncloseASliverFlattened_IsRefusedAsThePolygonOfItsCornersIs()
        {
            // The half disc of radius 1 above the floor's X axis, its diameter cut at (1, 0.000002): the three corners enclose
            // a sliver of 0.000002 square units, under the vector tolerance read as an area and not 0.001 wide.
            GeoPoint3[] corners = { P(0, 0, 0), P(1, 2E-6, 0), P(2, 0, 0) };
            var loop = new GeoPolygonArc3(
                corners,
                new[] { 0.0, 0.0, 1.0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis },
                Within(1E-6)
            );

            Assert.Throws<ArgumentException>(() => loop.Flatten());
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(corners));
        }

        [Fact]
        public void AHalfDiscAtAMillionthWhoseChordsEncloseASliverOfMoreThanTheVectorTolerance_FlattensToItsThreeCorners()
        {
            // The half disc of radius 50 cut so: the three corners enclose 0.0001 square units.
            GeoPoint3[] corners = { P(0, 0, 0), P(50, 2E-6, 0), P(100, 0, 0) };
            var loop = new GeoPolygonArc3(
                corners,
                new[] { 0.0, 0.0, 1.0 },
                new[] { GeoVector3.ZAxis, GeoVector3.ZAxis, GeoVector3.ZAxis },
                Within(1E-6)
            );

            GeoPolygon3 flat = loop.Flatten();

            Assert.Equal(3, flat.VertexCount);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(corners[i], flat[i]);
            }

            Assert.Equal(1E-4, flat.Area, 12);
        }

        [Fact]
        public void ABoxWithAStepFourTenThousandthsHighCutAtAMillionthScaledOrSheared_StaysClosedWithItsVolumeScaled()
        {
            Tolerance millionth = Within(1E-6);
            GeoSolid3 box = new GeoObb3(P(50, 35, 20), 100, 70, 40).ToSolid();
            GeoSolid3 tool = new GeoObb3(P(40, 35, 40 - 0.0004 + 10), 40, 90, 20).ToSolid();

            Assert.True(box.TrySubtract(tool, out GeoSolid3 stepped, millionth));

            double volume = stepped.GetVolume(millionth);
            int corners = stepped.Faces.Sum(f => f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount));

            Assert.True(stepped.IsClosed(millionth));
            Assert.Equal(280000 - 40 * 70 * 0.0004, volume, 6);

            foreach (double scale in new[] { 0.5, 10.0 })
            {
                foreach (double shear in new[] { 0.0, 0.001 })
                {
                    var matrix = new double[4, 4];
                    matrix[0, 0] = scale;
                    matrix[1, 1] = scale;
                    matrix[2, 2] = scale;
                    matrix[3, 3] = 1;
                    matrix[0, 1] = shear * scale;

                    GeoSolid3 moved = stepped.TransformBy(new GeoTransform3(matrix));

                    string at = "scaled by " + scale + ", sheared by " + shear;
                    Assert.True(moved.IsClosed(Within(1E-6 * scale)), at);
                    Assert.Equal(
                        corners,
                        moved.Faces.Sum(f => f.Boundary.VertexCount + f.Holes.Sum(h => h.VertexCount))
                    );
                    Assert.True(
                        Math.Abs(moved.GetVolume(Within(1E-6 * scale)) - volume * scale * scale * scale)
                            <= 1E-9 * volume * scale * scale * scale,
                        at
                    );
                }
            }
        }
    }
}
