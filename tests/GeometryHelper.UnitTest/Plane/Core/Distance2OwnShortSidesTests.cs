using System;
using System.Collections.Generic;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A shape's own sides are the sides they are against every straight shape, however short, at any tolerance. A shape
    /// whose top side is 0.004 long, with another shape touching down 0.006 over the middle of that side, is 0.006 from
    /// it: not 0.00632, the gap to the corner a side shorter than the tolerance would be read as. Only a segment, a ray or
    /// a straight edge a caller gives keeps that reading.
    /// </summary>
    public class Distance2OwnShortSidesTests
    {
        private const double Truth = 0.006;

        private static Tolerance At(double equalPoint) => new Tolerance(equalPoint, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, equalPoint);

        /// <summary>
        /// A shape standing under the origin whose top side runs from (0, 0) to (0.004, 0), built under the default
        /// tolerance, where it is a side like any other.
        /// </summary>
        private static object Short(string kind)
        {
            switch (kind)
            {
                case "polyline":
                    return new GeoPolyline2(new GeoPoint2(0, -50), new GeoPoint2(0, 0), new GeoPoint2(0.004, 0), new GeoPoint2(0.004, -50));
                case "polygon":
                    return new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(0, -50), new GeoPoint2(0.004, -50), new GeoPoint2(0.004, 0));
                case "triangle":
                    return new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(0.004, 0), new GeoPoint2(0.002, -50));
                case "rectangle":
                    return new GeoRectangle2(new GeoPoint2(0.002, -25), 0.004, 50, 0);
                case "loop":
                    return new GeoPolygonArc2(
                        new[] { new GeoPoint2(0, 0), new GeoPoint2(0, -50), new GeoPoint2(0.004, -50), new GeoPoint2(0.004, 0) },
                        new[] { 0.0, 0.0, 0.0, 0.0 });
                default:
                    throw new ArgumentException(kind);
            }
        }

        /// <summary>
        /// A shape with a long side everywhere, whose nearest point to the one above is (0.002, 0.006), a corner, an end
        /// or the foot of a circle, so that only a side read whole finds the gap.
        /// </summary>
        private static object Over(string kind)
        {
            var apex = new GeoPoint2(0.002, 0.006);

            switch (kind)
            {
                case "point":
                    return apex;
                case "segment":
                    return new GeoLine2(apex, new GeoPoint2(0.002, 10));
                case "ray":
                    return new GeoRay2(apex, new GeoVector2(0, 1));
                case "edge":
                    return new GeoEdge2(apex, new GeoPoint2(0.002, 10));
                case "circle":
                    return new GeoCircle2(new GeoPoint2(0.002, 5.006), 5);
                case "polygon":
                    return new GeoPolygon2(apex, new GeoPoint2(5, 10), new GeoPoint2(-5, 10));
                case "polyline":
                    return new GeoPolyline2(new GeoPoint2(-5, 10), apex, new GeoPoint2(5, 10));
                case "triangle":
                    return new GeoTriangle2(apex, new GeoPoint2(5, 10), new GeoPoint2(-5, 10));
                case "rectangle":
                    // A square 10 by 10 turned a quarter of a right angle, standing on its corner.
                    return new GeoRectangle2(new GeoPoint2(0.002, 0.006 + 5.0 * Math.Sqrt(2.0)), 10, 10, Math.PI / 4.0);
                default:
                    throw new ArgumentException(kind);
            }
        }

        public static IEnumerable<object[]> Rows()
        {
            foreach (string other in new[] { "point", "segment", "polygon", "polyline", "ray", "triangle", "edge", "circle", "rectangle" })
            {
                yield return new object[] { "polyline", other };
            }

            foreach (string other in new[] { "segment", "ray", "polygon", "polyline", "triangle", "edge" })
            {
                yield return new object[] { "polygon", other };
            }

            foreach (string other in new[] { "segment", "ray", "polyline", "polygon", "edge" })
            {
                yield return new object[] { "triangle", other };
            }

            foreach (string other in new[] { "segment", "ray", "polyline", "edge" })
            {
                yield return new object[] { "rectangle", other };
            }

            foreach (string other in new[] { "segment", "ray", "edge" })
            {
                yield return new object[] { "loop", other };
            }
        }

        /// <summary>
        /// The distance as the shape's own method gives it, or nothing where it has no such member.
        /// </summary>
        private static double? Distance(object from, object to, Tolerance? tolerance)
        {
            MethodInfo method = tolerance == null
                ? from.GetType().GetMethod("DistanceTo", new[] { to.GetType() })
                : from.GetType().GetMethod("DistanceTo", new[] { to.GetType(), typeof(Tolerance) });

            if (method == null)
            {
                return null;
            }

            return (double)method.Invoke(from, tolerance == null ? new[] { to } : new[] { to, (object)tolerance.Value });
        }

        private static bool? Collides(object from, object to, Tolerance tolerance)
        {
            MethodInfo method = from.GetType().GetMethod("CollidesWith", new[] { to.GetType(), typeof(Tolerance) });

            if (method == null)
            {
                return null;
            }

            return (bool)method.Invoke(from, new[] { to, (object)tolerance });
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void AShapeWithASideOfFourThousandths_IsSixThousandthsFromAShapeTouchingOverItsMiddle_AtAGlobalToleranceOfAHundredth(string own, string other)
        {
            object a = Short(own);
            object b = Over(other);

            using (Tolerance.Use(At(1E-2)))
            {
                double? there = Distance(a, b, null);
                double? back = Distance(b, a, null);

                Assert.NotNull(there);
                Assert.NotNull(back);
                Assert.InRange(there.Value, Truth - 1E-9, Truth + 1E-9);
                Assert.InRange(back.Value, Truth - 1E-9, Truth + 1E-9);
            }
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void AShapeWithASideOfFourThousandths_IsSixThousandthsFromAShapeTouchingOverItsMiddle_AtAnExplicitToleranceOfAHundredth(string own, string other)
        {
            object a = Short(own);
            object b = Over(other);
            Tolerance t = At(1E-2);

            double? there = Distance(a, b, t);
            double? back = Distance(b, a, t);

            // A pair with no member that takes a tolerance has nothing to say here: the global scope above covers it.
            if (there != null)
            {
                Assert.InRange(there.Value, Truth - 1E-9, Truth + 1E-9);
            }

            if (back != null)
            {
                Assert.InRange(back.Value, Truth - 1E-9, Truth + 1E-9);
            }
        }

        [Fact]
        public void AnArcSoFlatThatItIsItsChord_IsMeasuredByTheChordHoweverShort_ToASegmentOverItsMiddle()
        {
            // The arc of radius 1E6 about (0.002, -1E6) from 0.002 to the left of the top to 0.002 to the right: a chord of
            // 0.004 that bows by 2E-12, so it is read as the segment it lies along. The segment leaving (0.002, 0.006) is
            // 0.006 from it, and not the 0.00632 it is from the corner a side shorter than the tolerance would be read as.
            const double half = 0.002 / 1E6;
            var arc = new GeoArc2(new GeoPoint2(0.002, -1E6), 1E6, Math.PI / 2.0 - half, Math.PI / 2.0 + half);
            var segment = (GeoLine2)Over("segment");
            Tolerance t = At(1E-2);

            Assert.InRange(arc.DistanceTo(segment, t), Truth - 1E-7, Truth + 1E-7);
            Assert.InRange(segment.DistanceTo(arc, t), Truth - 1E-7, Truth + 1E-7);

            using (Tolerance.Use(t))
            {
                Assert.InRange(arc.DistanceTo(segment), Truth - 1E-7, Truth + 1E-7);
                Assert.InRange(segment.DistanceTo(arc), Truth - 1E-7, Truth + 1E-7);
            }
        }

        [Fact]
        public void ASegmentShorterThanTheTolerance_CrossingAnArcSoFlatThatItIsItsChord_IsNoughtFromIt()
        {
            // Crossing, the segment is nought from the arc as it is from any arc, flat or bowed; it is not read as its start
            // point, 0.001 under the chord.
            const double half = 0.002 / 1E6;
            var arc = new GeoArc2(new GeoPoint2(0.002, -1E6), 1E6, Math.PI / 2.0 - half, Math.PI / 2.0 + half);
            var stub = new GeoLine2(new GeoPoint2(0.002, -0.001), new GeoPoint2(0.002, 0.001));
            Tolerance t = At(1E-2);

            Assert.Equal(0.0, arc.DistanceTo(stub, t));
            Assert.Equal(0.0, stub.DistanceTo(arc, t));
            Assert.True(arc.CollidesWith(stub, t));
        }

        [Theory]
        [MemberData(nameof(Rows))]
        public void AShapeWithASideOfFourThousandths_CollidesWithAShapeSixThousandthsOverItsMiddle_AtAToleranceOf62Thousandths(string own, string other)
        {
            // The corner a short side would be read as is 0.00632 away, and the tolerance is 0.0062: the pair collides
            // exactly where the distance, 0.006, is at most the tolerance.
            object a = Short(own);
            object b = Over(other);
            Tolerance t = At(0.0062);
            Tolerance tight = At(0.0059);

            bool? there = Collides(a, b, t);
            bool? back = Collides(b, a, t);
            bool? thereTight = Collides(a, b, tight);
            bool? backTight = Collides(b, a, tight);

            if (there != null)
            {
                Assert.True(there.Value);
                Assert.False(thereTight.Value);
            }

            if (back != null)
            {
                Assert.True(back.Value);
                Assert.False(backTight.Value);
            }
        }
    }
}
