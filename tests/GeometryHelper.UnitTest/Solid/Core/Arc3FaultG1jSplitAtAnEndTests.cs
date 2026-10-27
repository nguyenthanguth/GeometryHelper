using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An arc cut within a rounding of an end at a tolerance of nought is left whole, as a cut at an end is. The cut was
    /// worked out as an angle, and one rounding short of the end it landed on the end's own angle; the piece between them,
    /// built from two equal angles, was a whole turn: the arc of 1 radian from 0.063 cut at 0.9999999999999999 came apart
    /// into the arc and a whole turn, and a whole turn from 0.238 into two whole turns.
    /// </summary>
    /// <remarks>
    /// Every arc here is of radius 100 about the origin, in the plan. The starts are among the 821 of 898 starts seven
    /// thousandths of a radian apart that came apart so counter-clockwise, and the 625 clockwise.
    /// </remarks>
    public class Arc3FaultG1jSplitAtAnEndTests
    {
        private const double OneRoundingShort = 0.9999999999999999;

        private static readonly Tolerance Nought = default(Tolerance);

        private static GeoArc3 Arc3(double start, double end, bool clockwise)
            => new GeoArc3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 100, start, end, clockwise);

        [Theory]
        [InlineData(0.063, 1.0, false)]
        [InlineData(0.07, -1.0, true)]
        [InlineData(0.238, 2.0 * Math.PI, false)]
        [InlineData(0.371, -2.0 * Math.PI, true)]
        public void AnArcInSpaceCutOneRoundingShortOfItsEnd_IsLeftWhole(double start, double sweep, bool clockwise)
        {
            // It came apart into the arc and a whole turn, and a whole turn into two.
            GeoArc3 arc = Math.Abs(sweep) < 2.0 * Math.PI ? Arc3(start, start + sweep, clockwise) : Arc3(start, start, clockwise);

            Assert.False(arc.TrySplitAt(OneRoundingShort, out GeoArc3[] pieces, Nought));
            Assert.Equal(arc, Assert.Single(pieces));
        }

        [Theory]
        [InlineData(0.063, 1.0, false)]
        [InlineData(0.07, -1.0, true)]
        [InlineData(0.238, 2.0 * Math.PI, false)]
        [InlineData(0.371, -2.0 * Math.PI, true)]
        public void AFlatArcCutOneRoundingShortOfItsEnd_IsLeftWhole(double start, double sweep, bool clockwise)
        {
            // The arc in the plane built its pieces the same way.
            GeoArc2 arc = Math.Abs(sweep) < 2.0 * Math.PI
                ? new GeoArc2(GeoPoint2.Origin, 100, start, start + sweep, clockwise)
                : new GeoArc2(GeoPoint2.Origin, 100, start, start, clockwise);

            Assert.False(arc.TrySplitAt(OneRoundingShort, out GeoArc2[] pieces, Nought));
            Assert.Equal(arc, Assert.Single(pieces));
        }

        [Theory]
        [InlineData(5.22637915545221, 1.0139143994239385E-17)]
        [InlineData(4.965434153626826, 3.2310186550832812E-19)]
        public void AWholeTurnCutOneRoundingAfterItsStart_IsLeftWhole(double start, double parameter)
        {
            // From these starts the start plus a turn comes back a rounding more than a turn on, and the cut a rounding after
            // the start gave the whole turn first and a piece of 9E-16 radians after it.
            GeoArc3 arc = Arc3(start, start, false);
            var flat = new GeoArc2(GeoPoint2.Origin, 100, start, start);

            Assert.False(arc.TrySplitAt(parameter, out GeoArc3[] pieces, Nought));
            Assert.Equal(arc, Assert.Single(pieces));
            Assert.False(flat.TrySplitAt(parameter, out GeoArc2[] flatPieces, Nought));
            Assert.Equal(flat, Assert.Single(flatPieces));
        }

        [Theory]
        [InlineData(1E-17)]
        [InlineData(1E-12)]
        public void AWholeTurnFromNoughtCutJustAfterItsStart_ComesApartIntoASliverAndTheRest(double parameter)
        {
            // From nought the start plus a turn is a turn exactly, so the cut lands after the start and the pieces sweep the
            // turn between them in their shares.
            GeoArc3 arc = Arc3(0.0, 0.0, false);

            Assert.True(arc.TrySplitAt(parameter, out GeoArc3[] pieces, Nought));
            Assert.InRange(pieces[0].SweptAngle, 0.0, 2.0 * Math.PI * parameter * 1.000001);
            Assert.True(pieces[0].SweptAngle > 0.0);
            Assert.InRange(pieces[1].SweptAngle, 2.0 * Math.PI * (1.0 - parameter) - 1E-15, 2.0 * Math.PI);
        }

        [Theory]
        [InlineData(0.063, 1.0, false)]
        [InlineData(0.07, -1.0, true)]
        [InlineData(0.238, 2.0 * Math.PI, false)]
        public void AnArcCutAMillionthShortOfItsEnd_StillComesApartThere(double start, double sweep, bool clockwise)
        {
            // Nought still cuts anywhere short of the rounding of an end: pieces sweeping the arc less a millionth of it, and
            // the millionth.
            GeoArc3 arc = Math.Abs(sweep) < 2.0 * Math.PI ? Arc3(start, start + sweep, clockwise) : Arc3(start, start, clockwise);

            Assert.True(arc.TrySplitAt(1.0 - 1E-6, out GeoArc3[] pieces, Nought));
            Assert.Equal(2, pieces.Length);
            Assert.InRange(Math.Abs(pieces[0].SweptAngle), Math.Abs(sweep) * (1.0 - 1E-6) - 1E-12, Math.Abs(sweep) * (1.0 - 1E-6) + 1E-12);
            Assert.InRange(Math.Abs(pieces[1].SweptAngle), Math.Abs(sweep) * 1E-6 - 1E-12, Math.Abs(sweep) * 1E-6 + 1E-12);
            Assert.InRange(pieces[1].EndPoint.DistanceTo(arc.EndPoint), 0.0, 1E-9);
        }
    }
}
