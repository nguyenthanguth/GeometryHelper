using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ellipse in space as a value: what its constructor takes, lays into its plane and refuses, what it holds, the
    /// circle it can be made from, its box, and how two of them compare. It is held as a centre, a unit normal, a unit
    /// major axis at right angles to the normal and two radii, the major no smaller than the minor; the minor axis is
    /// the normal crossed with the major, so that the eccentric angle runs counter-clockwise seen from the tip of the
    /// normal, as the angle of <see cref="GeoCircle3"/> does.
    /// </summary>
    public class GeoEllipse3Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static readonly Tolerance Loose = new Tolerance(1.0, 1E-5);

        private static readonly GeoPoint3 Centre = new GeoPoint3(40, -25, 60);

        #region The constructor

        [Fact]
        public void TheNormalIsMadeUnitAndTheMinorAxisIsTheNormalCrossTheMajor()
        {
            // A normal of (0, 0, 5) is held as Z; an axis of (3, 4, 0) is five long, held as (0.6, 0.8, 0). The minor axis
            // is Z × (0.6, 0.8, 0) = (-0.8, 0.6, 0), a quarter turn counter-clockwise seen from above.
            var ellipse = new GeoEllipse3(Centre, new GeoVector3(0, 0, 5), new GeoVector3(3, 4, 0), 300, 100);

            Assert.True(ellipse.Normal.IsEqualTo(GeoVector3.ZAxis, Tight));
            Assert.True(ellipse.MajorAxis.IsEqualTo(new GeoVector3(0.6, 0.8, 0), Tight));
            Assert.True(ellipse.MinorAxis.IsEqualTo(new GeoVector3(-0.8, 0.6, 0), Tight));

            // The centre and the radii are kept as given.
            Assert.Equal(Centre, ellipse.Center);
            Assert.Equal(300.0, ellipse.MajorRadius);
            Assert.Equal(100.0, ellipse.MinorRadius);

            // Turned over, the same axis gives the minor axis the other way: -Z × (0.6, 0.8, 0) = (0.8, -0.6, 0).
            var under = new GeoEllipse3(Centre, new GeoVector3(0, 0, -1), new GeoVector3(3, 4, 0), 300, 100);
            Assert.True(under.MinorAxis.IsEqualTo(new GeoVector3(0.8, -0.6, 0), Tight));
        }

        [Fact]
        public void AnAxisTiltedOutOfThePlaneIsLaidIntoIt()
        {
            // Over a level plane, (3, 0, 4) loses its 4 up and is X; (1, 1, 1) loses its 1 up and is (1, 1, 0)/√2.
            var steep = new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(3, 0, 4), 300, 100);
            var diagonal = new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(1, 1, 1), 300, 100);

            Assert.True(steep.MajorAxis.IsEqualTo(GeoVector3.XAxis, Tight));
            Assert.True(diagonal.MajorAxis.IsEqualTo(new GeoVector3(1, 1, 0).Normalize(), Tight));

            // Over the plane of normal (1, 2, 2)/3, the axis (7, 11, 8) is 3 U + 5 times (1, 2, 2), U = (2, 1, -2)/3: what is
            // left of it once the part along the normal is taken off is 3 U, laid in as U.
            var tilted = new GeoEllipse3(Centre, new GeoVector3(1, 2, 2), new GeoVector3(7, 11, 8), 300, 100);

            Assert.True(tilted.MajorAxis.IsEqualTo(new GeoVector3(2.0 / 3, 1.0 / 3, -2.0 / 3), Tight));
            Assert.Equal(0.0, tilted.MajorAxis.DotProduct(tilted.Normal), 15);
        }

        [Theory]
        [InlineData(0.0, 0.0, 2.0)]
        [InlineData(0.0, 0.0, -1.0)]
        [InlineData(1E-13, 0.0, 1.0)]
        public void AnAxisAlongTheNormalIsRefused(double x, double y, double z)
        {
            // Along the normal, either way, nothing is left in the plane. (1E-13, 0, 1) leaves 1E-13 of a length of one, ten
            // times under the 1E-12 the spec allows: still no direction.
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(x, y, z), 300, 100));
        }

        [Fact]
        public void AnAxisAHairOffTheNormalIsStillADirection()
        {
            // (1E-10, 0, 1) leaves 1E-10 in the plane, a hundred times over the 1E-12 bound: X, laid in.
            var ellipse = new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(1E-10, 0, 1), 300, 100);

            Assert.True(ellipse.MajorAxis.IsEqualTo(GeoVector3.XAxis, Tight));
        }

        [Fact]
        public void AnAxisWithNoLengthOrNoFiniteValueIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.Zero, 300, 100));
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(double.NaN, 1, 0), 300, 100));
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, new GeoVector3(double.PositiveInfinity, 0, 0), 300, 100));
        }

        [Fact]
        public void ANormalWithNoLengthIsRefused()
        {
            // As the circle's (GeoCircle3.cs:42-45).
            Assert.Throws<ArgumentException>(() => new GeoEllipse3(Centre, GeoVector3.Zero, GeoVector3.XAxis, 300, 100));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(300.0, 0.0)]
        [InlineData(300.0, -1.0)]
        [InlineData(-100.0, -300.0)]
        [InlineData(double.NaN, 100.0)]
        [InlineData(300.0, double.NaN)]
        [InlineData(double.PositiveInfinity, 100.0)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
        public void ARadiusThatIsNotAPositiveFiniteNumberIsRefused(double majorRadius, double minorRadius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.XAxis, majorRadius, minorRadius));
        }

        [Fact]
        public void AMinorRadiusLargerThanTheMajorIsRefusedRatherThanSwapped()
        {
            // 100 by 300 is not read as 300 by 100 turned a quarter, and a hair over is still over: the check is on the numbers.
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.XAxis, 100, 300));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.XAxis, 100, 100.0001));
        }

        #endregion

        #region What it holds

        [Fact]
        public void TheMeasuresAreThoseOfTheEllipseLaidOut()
        {
            // 300 by 100 encloses π 300 100 = 94 247.78 and runs 1 336.489 round, whichever way it stands in space: the
            // length and the eccentricity, 0.943, are the 2D ellipse's to the bit.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            Assert.Equal(Math.PI * 300 * 100, ellipse.Area, 1E-9);
            Assert.Equal(flat.Length, ellipse.Length);
            Assert.Equal(1336.489, ellipse.Length, 1E-3);
            Assert.Equal(flat.Eccentricity, ellipse.Eccentricity);

            GeoPlane3 plane = ellipse.GetPlane();
            Assert.Equal(ellipse.Center, plane.Origin);
            Assert.True(plane.Normal.IsEqualTo(specimen.N, Tight));
        }

        [Fact]
        public void FromCircleStartsTheAngleWhereTheCircleStartsIt()
        {
            // The major axis is the first axis GeoPlane3.GetAxes gives the circle's plane, so the two put t at the same
            // place for every t, both radii the circle's 250.
            var circle = new GeoCircle3(new GeoPoint3(100, 200, 300), new GeoVector3(1, -1, 2), 250);
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(circle);
            circle.GetPlane().GetAxes(out GeoVector3 zero, out _);

            Assert.Equal(circle.Center, ellipse.Center);
            Assert.Equal(250.0, ellipse.MajorRadius);
            Assert.Equal(250.0, ellipse.MinorRadius);
            Assert.True(ellipse.Normal.IsEqualTo(circle.Normal, Tight));
            Assert.True(ellipse.MajorAxis.IsEqualTo(zero, Tight));

            for (int k = -8; k <= 24; k++)
            {
                double t = k * 0.37;
                Assert.True(ellipse.GetPointAtAngle(t).IsEqualTo(circle.GetPointAtAngle(t), Tight), $"t = {t}");
            }
        }

        [Fact]
        public void ACircleIsAnEllipseWithEqualRadiiWithinThePointTolerance()
        {
            // 300 by 299.5 is a circle within a whole unit and not within a thousandth; one made from a circle is one.
            var nearlyRound = new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 299.5);

            Assert.True(nearlyRound.IsCircle(Loose));
            Assert.False(nearlyRound.IsCircle(Strict));
            Assert.True(GeoEllipse3.FromCircle(new GeoCircle3(Centre, GeoVector3.ZAxis, 300)).IsCircle(Strict));
        }

        #endregion

        #region The box

        [Fact]
        public void TheBoxOfALevelEllipseIsItsRadiiAlongXAndY()
        {
            // 300 by 100 along X about (40, -25, 60): from (-260, -125, 60) to (340, 75, 60), flat in Z.
            GeoAabb3 box = Ellipse3Oracle.Upright(300, 100).Build().GetAabb();

            Assert.True(box.Min.IsEqualTo(new GeoPoint3(-260, -125, 60), Tight));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(340, 75, 60), Tight));
        }

        [Fact]
        public void TheBoxOfATiltedEllipseIsExactAgainstADenseSample()
        {
            // The rim reaches √((a Uk)² + (b Vk)²) either side of the centre along world axis k: along X that is
            // √(200² + 66.667²) = 210.819. The oracle samples the rim and refines its highest and lowest points.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoAabb3 box = specimen.Build().GetAabb();

            Ellipse3Oracle.Reach(specimen, GeoVector3.XAxis, out double lowX, out double highX);
            Ellipse3Oracle.Reach(specimen, GeoVector3.YAxis, out double lowY, out double highY);
            Ellipse3Oracle.Reach(specimen, GeoVector3.ZAxis, out double lowZ, out double highZ);

            Assert.Equal(lowX, box.Min.X, 1E-9);
            Assert.Equal(highX, box.Max.X, 1E-9);
            Assert.Equal(lowY, box.Min.Y, 1E-9);
            Assert.Equal(highY, box.Max.Y, 1E-9);
            Assert.Equal(lowZ, box.Min.Z, 1E-9);
            Assert.Equal(highZ, box.Max.Z, 1E-9);
            Assert.Equal(2 * 210.8185106778919, box.SizeX, 1E-9);
        }

        [Fact]
        public void ACircleTiltedSixtyDegreesIsBoxedAsTheCircleIs()
        {
            // Radius 300, its plane through X and 60 degrees up from Y: 300 either way along X, 300 cos 60° = 150 along Y,
            // 300 sin 60° = 259.808 along Z, as GeoCircle3.GetAabb has it.
            GeoCircle3 circle = Ellipse3Oracle.CircleTiltedSixty();
            GeoAabb3 box = GeoEllipse3.FromCircle(circle).GetAabb();
            GeoAabb3 circles = circle.GetAabb();

            Assert.Equal(600.0, box.SizeX, 1E-9);
            Assert.Equal(300.0, box.SizeY, 1E-9);
            Assert.Equal(600 * Math.Sin(Math.PI / 3), box.SizeZ, 1E-9);
            Assert.True(box.Min.IsEqualTo(circles.Min, Tight));
            Assert.True(box.Max.IsEqualTo(circles.Max, Tight));
        }

        #endregion

        #region Equality, validity, text, copy

        [Fact]
        public void TwoBuildsOfTheSameEllipseAreEqualAndHashAlike()
        {
            GeoEllipse3 first = Ellipse3Oracle.Tilted().Build();
            GeoEllipse3 second = Ellipse3Oracle.Tilted().Build();
            GeoEllipse3 copy = first.Clone();

            Assert.True(first.Equals(second));
            Assert.True(first == second);
            Assert.False(first != second);
            Assert.True(first.Equals((object)second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.False(first.Equals(null));
            Assert.False(first.Equals("not a shape"));

            Assert.Equal(first, copy);
            Assert.Equal(first.GetHashCode(), copy.GetHashCode());

            // Exact on the fields: a centre moved a millionth is another value, though the same ellipse within a tolerance.
            GeoEllipse3 moved = first.Translate(new GeoVector3(1E-6, 0, 0));
            Assert.False(first.Equals(moved));
            Assert.True(first != moved);
        }

        [Fact]
        public void IsEqualToReadsTheCentreAndTheRadiiWithinThePointTolerance()
        {
            // Half a unit along the major axis, or half a unit more radius: the same within a whole unit, not within a
            // thousandth.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse3 moved = ellipse.Translate(specimen.U.Multiply(0.5));
            GeoEllipse3 wider = new GeoEllipse3(specimen.Center, specimen.N, specimen.U, 300.5, 100);
            GeoEllipse3 fuller = new GeoEllipse3(specimen.Center, specimen.N, specimen.U, 300, 100.5);

            foreach (GeoEllipse3 other in new[] { moved, wider, fuller })
            {
                Assert.True(ellipse.IsEqualTo(other, Loose));
                Assert.False(ellipse.IsEqualTo(other, Strict));
            }
        }

        [Fact]
        public void IsEqualToReadsTheAxisEitherWayRoundAndNotAtAllForTwoCircles()
        {
            // The axis turned end for end draws the same ellipse. Turned 0.01 rad in the plane it does not, the axes
            // 0.01 apart against a vector tolerance of 1E-5.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            var reversed = new GeoEllipse3(specimen.Center, specimen.N, specimen.U.Multiply(-1), 300, 100);
            var turned = new GeoEllipse3(specimen.Center, specimen.N, specimen.U.Multiply(Math.Cos(0.01)).Add(specimen.V.Multiply(Math.Sin(0.01))), 300, 100);

            Assert.True(ellipse.IsEqualTo(reversed, Strict));
            Assert.False(ellipse.IsEqualTo(turned, Strict));

            // Two circles of 200 with axes a quarter turn apart are the same circle; two 300 by 100 so placed are not.
            var circleAlongU = new GeoEllipse3(specimen.Center, specimen.N, specimen.U, 200, 200);
            var circleAlongV = new GeoEllipse3(specimen.Center, specimen.N, specimen.V, 200, 200);
            var acrossV = new GeoEllipse3(specimen.Center, specimen.N, specimen.V, 300, 100);

            Assert.True(circleAlongU.IsEqualTo(circleAlongV, Strict));
            Assert.False(ellipse.IsEqualTo(acrossV, Strict));
        }

        [Fact]
        public void IsEqualToReadsTheNormalWithinTheVectorTolerance()
        {
            // The normal tipped 1E-7 rad towards U is within the 1E-5 of the vector tolerance; tipped 0.01 rad it is not.
            // The axis is laid into each tipped plane, so it moves by as much and no more.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            var hair = new GeoEllipse3(specimen.Center, specimen.N.Add(specimen.U.Multiply(1E-7)), specimen.U, 300, 100);
            var tipped = new GeoEllipse3(specimen.Center, specimen.N.Add(specimen.U.Multiply(0.01)), specimen.U, 300, 100);

            Assert.True(ellipse.IsEqualTo(hair, Strict));
            Assert.False(ellipse.IsEqualTo(tipped, Strict));
        }

        [Fact]
        public void AnEllipseMadeByAConstructorIsValidAndTheDefaultIsNot()
        {
            Assert.True(Ellipse3Oracle.Tilted().Build().IsValid);
            Assert.True(GeoEllipse3.FromCircle(Ellipse3Oracle.CircleTiltedSixty()).IsValid);

            // A thin one is still an ellipse (2D decision 17).
            Assert.True(new GeoEllipse3(Centre, GeoVector3.ZAxis, GeoVector3.XAxis, 300, 1E-6).IsValid);

            // The default has no radii, no normal and no axis.
            Assert.False(default(GeoEllipse3).IsValid);
        }

        [Fact]
        public void ToStringNamesTheTypeTheCentreTheNormalTheAxisAndBothRadii()
        {
            // In the 2D ellipse's format, the normal added; the order of the parts is not pinned.
            string text = Ellipse3Oracle.Tilted().Build().ToString();

            Assert.Contains("GeoEllipse3[", text);
            Assert.Contains("Center:(40, -25, 60)", text);
            Assert.Contains("Normal:[0.333, 0.667, 0.667]", text);
            Assert.Contains("MajorAxis:[0.667, 0.333, -0.667]", text);
            Assert.Contains("MajorRadius:300", text);
            Assert.Contains("MinorRadius:100", text);
        }

        [Fact]
        public void MovingOrAskingLeavesTheEllipseAsItWas()
        {
            // As TypeContractTests holds the other shapes: a transformation hands back another ellipse and changes none.
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();
            GeoEllipse3 before = Ellipse3Oracle.Tilted().Build();
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(100, -50, 25)) * GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7);

            GeoEllipse3 moved = ellipse.TransformBy(move);
            ellipse.GetAabb();
            ellipse.GetClosestPointOnBoundary(new GeoPoint3(500, 500, 500));
            ellipse.ToPolygon();

            Assert.Equal(before, ellipse);
            Assert.NotEqual(before, moved);
        }

        #endregion
    }
}
