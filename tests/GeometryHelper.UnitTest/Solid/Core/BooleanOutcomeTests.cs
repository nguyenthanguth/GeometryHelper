using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// The booleans of solids saying how they came out: a body made, nothing left of it, or no answer at all.
    /// </summary>
    public class BooleanOutcomeTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        /// <summary>
        /// A box whose top face says its normal is nought, which no face made the usual way can: no plane can be made of it,
        /// so no boolean with it can be worked out.
        /// </summary>
        private static GeoSolid3 Broken(double x0, double y0, double z0, double x1, double y1, double z1)
        {
            List<GeoFace3> faces = Box(x0, y0, z0, x1, y1, z1).Faces.ToList();
            int top = faces.FindIndex(f => f.Boundary.Normal.Z > 0.5);
            faces[top] = new GeoFace3(GeoPolygon3.FromValidated(faces[top].Boundary.Vertices.ToArray(), new GeoVector3(0, 0, 0), faces[top].Area));
            return new GeoSolid3(faces);
        }

        /// <summary>Runs an action and gives back the warnings it wrote.</summary>
        private static List<string> Warnings(Action action)
        {
            var written = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                if (level == GeometryHelperLogLevel.Warn)
                {
                    written.Add(message);
                }
            };

            try
            {
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = null;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        [Fact]
        public void ADifferenceSaysWhetherAnythingIsLeftOrNoAnswerWasFound()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20);
            BooleanOutcome outcome = BooleanOutcome.NotWorkedOut;

            Assert.Empty(Warnings(() => Assert.True(Boolean3.TrySubtract(plate, Box(40, 40, -5, 60, 60, 25), out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.Made, outcome);

            // Swallowed whole: nothing left, as it should be, and nothing to warn of.
            Assert.Empty(Warnings(() => Assert.False(Boolean3.TrySubtract(Box(10, 10, 5, 20, 20, 15), plate, out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.Empty, outcome);

            // No answer: not nothing left, and said why.
            Assert.NotEmpty(Warnings(() => Assert.False(Boolean3.TrySubtract(plate, Broken(40, 40, -5, 60, 60, 25), out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
        }

        [Fact]
        public void AnIntersectionSaysWhetherTheTwoShareAnythingOrNoAnswerWasFound()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20);
            BooleanOutcome outcome = BooleanOutcome.NotWorkedOut;

            Assert.True(Boolean3.TryIntersect(plate, Box(40, 40, -5, 60, 60, 25), out GeoSolid3 shared, Tolerance, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(20 * 20 * 20, shared.GetVolume(), 6);

            Assert.Empty(Warnings(() => Assert.False(Boolean3.TryIntersect(plate, Box(200, 0, 0, 300, 100, 20), out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.Empty, outcome);

            // Touching shares no volume.
            Assert.False(Boolean3.TryIntersect(plate, Box(100, 0, 0, 200, 100, 20), out _, Tolerance, out outcome));
            Assert.Equal(BooleanOutcome.Empty, outcome);

            Assert.NotEmpty(Warnings(() => Assert.False(Boolean3.TryIntersect(plate, Broken(40, 40, -5, 60, 60, 25), out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
        }

        [Fact]
        public void AUnionIsMadeOrNotWorkedOut_NeverEmpty()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20);
            BooleanOutcome outcome = BooleanOutcome.NotWorkedOut;

            Assert.True(Boolean3.TryUnion(plate, Box(40, 40, -5, 60, 60, 25), out GeoSolid3 both, Tolerance, out outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.Equal(100 * 100 * 20 + 20 * 20 * 10, both.GetVolume(), 6);

            Assert.NotEmpty(Warnings(() => Assert.False(Boolean3.TryUnion(plate, Broken(40, 40, -5, 60, 60, 25), out _, Tolerance, out outcome))));
            Assert.Equal(BooleanOutcome.NotWorkedOut, outcome);
        }

        [Fact]
        public void TheBodiesOwnOverloadsAndTheOnesWithoutAnOutcomeAgree()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20);
            GeoSolid3 post = Box(40, 40, -5, 60, 60, 25);

            Assert.True(plate.TrySubtract(post, out GeoSolid3 less, Tolerance, out BooleanOutcome subtracted));
            Assert.True(plate.TryIntersect(post, out GeoSolid3 shared, Tolerance, out BooleanOutcome intersected));
            Assert.True(plate.TryUnion(post, out GeoSolid3 both, Tolerance, out BooleanOutcome united));
            Assert.Equal(new[] { BooleanOutcome.Made, BooleanOutcome.Made, BooleanOutcome.Made }, new[] { subtracted, intersected, united });

            Assert.True(plate.TrySubtract(post, out GeoSolid3 lessAgain, Tolerance));
            Assert.True(plate.TryIntersect(post, out GeoSolid3 sharedAgain, Tolerance));
            Assert.True(plate.TryUnion(post, out GeoSolid3 bothAgain, Tolerance));
            Assert.Equal(less.GetVolume(), lessAgain.GetVolume(), 9);
            Assert.Equal(shared.GetVolume(), sharedAgain.GetVolume(), 9);
            Assert.Equal(both.GetVolume(), bothAgain.GetVolume(), 9);

            Assert.True(plate.TrySubtract(post, out _, out BooleanOutcome atDefault));
            Assert.Equal(BooleanOutcome.Made, atDefault);
        }
    }
}
