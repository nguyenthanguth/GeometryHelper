using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Whether a probe reaches the material of a face. This is the one reading in the measuring surface that
    /// is not a delegation to something Core already computed — the rule was written here, so these tests go
    /// looking for the cases it could have got wrong rather than checking it against itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule: a probe reaches the material when it reaches the outline and no hole holds it whole. A probe
    /// crossing no rim of a hole is inside that hole, outside it, or wrapped around it, and those are told
    /// apart by asking where one point of the probe falls and, for a probe with an inside, whether the rim
    /// falls within the probe.
    /// </para>
    /// <para>
    /// Two holes rather than one throughout, because a single hole cannot catch a rule that stops at the
    /// first one it looks at, and the answers are checked against the same face built with its holes the
    /// other way round, because nothing about the question depends on the order they were given in.
    /// </para>
    /// </remarks>
    public class Face2TouchingTests
    {
        private static GeoPolygon2 Box(double left, double bottom, double right, double top) => new GeoPolygon2(
            new GeoPoint2(left, bottom), new GeoPoint2(right, bottom),
            new GeoPoint2(right, top), new GeoPoint2(left, top));

        /// <summary>
        /// Three hundred square with two tall holes in it, a twenty-wide strip of material between them.
        /// </summary>
        private static GeoPolygon2 Outline() => Box(0, 0, 300, 300);

        private static GeoPolygon2 Left() => Box(100, 100, 140, 200);

        private static GeoPolygon2 Right() => Box(160, 100, 200, 200);

        /// <summary>
        /// The same face with the holes given in each order, so every answer can be asked of both.
        /// </summary>
        private static IEnumerable<GeoFace2> BothOrders()
        {
            yield return new GeoFace2(Outline(), new[] { Left(), Right() });
            yield return new GeoFace2(Outline(), new[] { Right(), Left() });
        }

        private static void Both(Func<GeoFace2, bool> ask, bool expected, string what)
        {
            foreach (GeoFace2 face in BothOrders())
            {
                Assert.True(ask(face) == expected, what);
            }
        }

        [Fact]
        public void AProbeThreadingTheStripBetweenTwoHolesReachesTheMaterial()
        {
            // x = 150 runs down the strip and crosses neither rim.
            var threading = new GeoLine2(new GeoPoint2(150, 50), new GeoPoint2(150, 250));

            Both(face => face.CollidesWith(threading), true, "the strip between two holes is material");

            foreach (GeoFace2 face in BothOrders())
            {
                Assert.Empty(face.Holes.SelectMany(hole => hole.GetIntersections(threading)));
            }
        }

        [Fact]
        public void AProbeRunningFromOneHoleToTheOtherReachesTheMaterialBetweenThem()
        {
            var spanning = new GeoLine2(new GeoPoint2(120, 150), new GeoPoint2(180, 150));

            Both(face => face.CollidesWith(spanning), true, "it must cross the strip to get from one to the other");

            // It leaves one hole and enters the other, so it crosses a rim of each.
            foreach (GeoFace2 face in BothOrders())
            {
                Assert.All(face.Holes, hole => Assert.NotEmpty(hole.GetIntersections(spanning)));
            }
        }

        [Fact]
        public void AProbeInEitherHoleReachesNothingWhicheverHoleItIs()
        {
            var inLeft = new GeoLine2(new GeoPoint2(110, 150), new GeoPoint2(130, 150));
            var inRight = new GeoLine2(new GeoPoint2(170, 150), new GeoPoint2(190, 150));

            // Whichever hole was looked at first, the other one still has to be looked at.
            Both(face => face.CollidesWith(inLeft), false, "a probe in the left hole reaches nothing");
            Both(face => face.CollidesWith(inRight), false, "a probe in the right hole reaches nothing");

            // The outline alone says both are there, which is what makes the hole rule do the work.
            foreach (GeoFace2 face in BothOrders())
            {
                Assert.True(face.Boundary.CollidesWith(inLeft));
                Assert.True(face.Boundary.CollidesWith(inRight));
            }
        }

        [Fact]
        public void AProbeTouchingARimAnswersTheSameWhicheverEndIsGivenFirst()
        {
            // One end on the rim of the left hole, the rest inside it. Reading the probe from either end
            // must give the same answer: which end happens to be first is not geometry.
            var fromRim = new GeoLine2(new GeoPoint2(140, 150), new GeoPoint2(130, 150));
            var toRim = new GeoLine2(new GeoPoint2(130, 150), new GeoPoint2(140, 150));

            foreach (GeoFace2 face in BothOrders())
            {
                Assert.Equal(face.CollidesWith(fromRim), face.CollidesWith(toRim));

                // It does reach the material, because the rim is part of the face's boundary, and the touch
                // shows up as a crossing rather than being left to whichever end was asked about.
                Assert.True(face.CollidesWith(fromRim));
                Assert.Single(face.GetIntersections(fromRim));
            }
        }

        [Fact]
        public void ARingAroundAHoleReachesTheMaterialAndOneInsideItDoesNot()
        {
            // A circle centred in the left hole: its centre is in a hole and it crosses no rim, which is
            // exactly what a speck lying in that hole looks like.
            var around = new GeoCircle2(new GeoPoint2(120, 150), 80);
            var within = new GeoCircle2(new GeoPoint2(120, 150), 15);

            Both(face => face.CollidesWith(around), true, "a ring around a hole crosses material");
            Both(face => face.CollidesWith(within), false, "a speck in a hole crosses none");

            // And a ring wide enough to take in both holes.
            var takingBoth = new GeoCircle2(new GeoPoint2(150, 150), 120);

            Both(face => face.CollidesWith(takingBoth), true, "a ring around both holes crosses material");

            // The same told apart for a closed shape whose own first vertex sits on the material.
            Both(face => face.CollidesWith(Box(60, 60, 240, 240)), true, "a frame around both holes");
            Both(face => face.CollidesWith(Box(110, 140, 130, 160)), false, "a patch inside the left hole");
        }

        [Fact]
        public void AProbeThatSwallowsTheWholeFaceReachesIt()
        {
            GeoPolygon2 vast = Box(-1000, -1000, 1000, 1000);

            Both(face => face.CollidesWith(vast), true, "the face is inside the probe");

            // And something wholly outside the outline reaches nothing, holes or no holes.
            Both(face => face.CollidesWith(Box(500, 500, 600, 600)), false, "outside the outline");
            Both(face => face.CollidesWith(new GeoLine2(new GeoPoint2(400, 0), new GeoPoint2(400, 300))), false, "beside it");
        }

        [Fact]
        public void TheCrossingsAndTheReachAreBothTakenOverEveryLoop()
        {
            var right = new GeoLine2(new GeoPoint2(-50, 150), new GeoPoint2(350, 150));

            foreach (GeoFace2 face in BothOrders())
            {
                // Two sides of the outline and two rims each, so six in all, and the order the holes were
                // given in cannot change how many there are.
                double[] crossings = face.GetIntersections(right)
                    .Select(point => Math.Round(point.X, 6))
                    .OrderBy(x => x)
                    .ToArray();

                Assert.Equal(new[] { 0.0, 100.0, 140.0, 160.0, 200.0, 300.0 }, crossings);

                // The nearest boundary to the middle of the strip is a rim, twenty off, not the outline.
                Assert.Equal(10.0, face.GetShortestLineTo(new GeoPoint2(150, 150)).Length, 6);
            }
        }

        [Fact]
        public void AFaceWithNoHolesAnswersExactlyAsItsOutlineDoes()
        {
            var plain = new GeoFace2(Outline());
            GeoPolygon2 outline = Outline();

            foreach (GeoLine2 probe in new[]
                     {
                         new GeoLine2(new GeoPoint2(150, 50), new GeoPoint2(150, 250)),
                         new GeoLine2(new GeoPoint2(400, 0), new GeoPoint2(400, 300)),
                         new GeoLine2(new GeoPoint2(-50, 150), new GeoPoint2(350, 150)),
                     })
            {
                Assert.Equal(outline.CollidesWith(probe), plain.CollidesWith(probe));
                Assert.Equal(outline.GetIntersections(probe).Length, plain.GetIntersections(probe).Length);
            }
        }
    }
}
