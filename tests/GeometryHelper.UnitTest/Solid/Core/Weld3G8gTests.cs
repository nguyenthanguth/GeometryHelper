using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Unions of a column and a beam whose end stands exactly the tolerance into or clear of its side, turned and moved, and
    /// of a cube and a block running exactly the tolerance into its top: the weld that closes them takes the beam's corners
    /// onto the column's face, along the beam's sides, rather than a corner the column's face lies round off its plane.
    /// </summary>
    public class Weld3G8gTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        [Theory]
        [InlineData(-0.49432111205268703, -0.12014541803865947, 0.49375473784317947, 4.056002415742726, 50726.219709369456, 4270.591216287851, 1058.534994283009, -0.01, 160.13542789040852, 160.7675563827006, false)]
        [InlineData(-0.05748715557040979, 0.20450097075873097, 0.2970021234098832, 0.5438177942036735, 60771.88209666492, 27657.18331917058, 8061.105216881775, 0.01, 129.24516607506442, 366.65753208410814, true)]
        [InlineData(0.40817094264001164, -0.2405602828322725, -0.3493604325700367, 5.320040476191808, 82777.96627151685, 91663.01088019414, 2880.904065855268, -0.01, 289.82138926620655, 112.6885429083782, false)]
        [InlineData(0.10658294549518399, 0.19643128649165453, -0.47486108603321997, 0.8380475262357143, 0, 0, 0, 0.01, 141.76975574426808, 164.81750033088844, false)]
        [InlineData(0.08325684982503623, -0.434979429438235, 0.4023692209969132, 2.5405259460865177, 0, 0, 0, 0.01, 343.8884716685342, 316.7509703509281, false)]
        [InlineData(-0.47992647717703435, 0.16507614947160532, 0.08063488697057353, 0.23425406601012405, 478.9594460646433, 271.9027429222608, 61.684913729170766, 0.05, 298.2084496869745, 155.05689073123824, false)]
        [InlineData(-0.2874564541445377, -0.2764782867284856, 0.3479948916914849, 4.075951216777764, 31570.01046071295, 25485.96650617475, 6636.616497597013, -0.05, 150.64050296816998, 117.25475523493009, false)]
        public void ABeamExactlyTheToleranceIntoOrClearOfAColumn_IsJoinedWithoutTiltingTheColumnsFace(double ax, double ay, double az, double angle, double sx, double sy, double sz, double d, double w, double h, bool beamFirst)
        {
            // A column 300 by 300 by 1 000 and a beam from d inside its side to 2 000 along x, turned about the origin and moved,
            // joined within the tolerance |d|. The weld took a corner of the column's face, round a hole or along an edge where
            // the beam met it, the tolerance off its plane, tilting a face of 220 000 to 300 000 square millimetres: the first
            // held the two and 918.9, where filling the gap holds 257.4, and the others came out 1.04 to 5.2 times the tolerance
            // times the area where they meet under the two less their overlap, or 1.5 and 2.7 times over the two and the gap,
            // each closed and valid.
            double tol = Math.Abs(d);
            var tolerance = new Tolerance(tol, 1E-5, 1E-5, tol);
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(sx, sy, sz)) * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(ax, ay, az), angle);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(place);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(place);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            double contact = Math.Min(w, 300) * h;

            GeoSolid3 union;
            Assert.True(beamFirst ? beam.TryUnion(column, out union, tolerance) : column.TryUnion(beam, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // The two less the overlap, or the two and the gap filled, within the tolerance times the area where they meet.
            Assert.InRange(union.GetVolume(tolerance), two - Math.Max(d, 0.0) * contact - tol * contact, two + Math.Max(-d, 0.0) * contact + tol * contact);
        }

        [Theory]
        [InlineData(0.24333679105310546, -0.48896821401499596, 0.3927866865600397, 0.6714398612600937, 0, 0, 0, 0.01, 377.4328325304356, 107.46428328913836, false)]
        [InlineData(-0.03786122404870634, -0.2067261290302156, -0.3673578245660094, 1.2112593004532433, 555.5250684523605, 11357.489512934111, 4870.102473008494, 0.01, 329.2594463700705, 125.62471182347495, true)]
        [InlineData(0.3820798489647358, 0.03624316981818676, -0.4426436649149487, 2.0510711837797757, 0, 0, 0, 0.001, 245.9004539744465, 327.45151330132575, false)]
        [InlineData(-0.04781643000795338, -0.04664347299684002, 0.01007896482808468, 1.327621915064576, 0, 0, 0, 0.001, 329.07359638673887, 305.0460847118153, false)]
        public void ABeamExactlyTheToleranceIntoAColumnThatOnlyATiltingWeldClosed_IsJoined(double ax, double ay, double az, double angle, double sx, double sy, double sz, double d, double w, double h, bool beamFirst)
        {
            // A column 300 by 300 by 1 000 and a beam from d inside its side to 2 000 along x, turned about the origin and moved,
            // joined within d. The weld that closed the union tilted the column's face, and moved the volume further than
            // the union may, or came out open as Validate reads it, and the union was not worked out.
            var tolerance = new Tolerance(d, 1E-5, 1E-5, d);
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(sx, sy, sz)) * GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(ax, ay, az), angle);
            GeoSolid3 column = Box(0, 0, 0, 300, 300, 1000).TransformBy(place);
            GeoSolid3 beam = Box(300 - d, 150 - w / 2, 500 - h / 2, 2000, 150 + w / 2, 500 + h / 2).TransformBy(place);
            double two = 300.0 * 300 * 1000 + (1700 + d) * w * h;
            double contact = Math.Min(w, 300) * h;

            GeoSolid3 union;
            Assert.True(beamFirst ? beam.TryUnion(column, out union, tolerance) : column.TryUnion(beam, out union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // The two less the overlap within the tolerance times the area where they meet.
            Assert.InRange(union.GetVolume(tolerance), two - d * contact - d * contact, two + d * contact);
        }

        [Fact]
        public void ABlockRunningAHundredthIntoACubesTop_IsJoinedWithinTheToleranceTimesWhereTheyMeet()
        {
            // A cube of 100 and a block 40 by 30 by 50.01 running 0.01 into its top, turned, joined within a hundredth. The
            // union held 1 059 986, 14 short of the two less their overlap, where the tolerance times the area where they meet
            // is 12, closed and valid.
            const double t = 0.01;
            const double ax = 0.3910031457855381, ay = 0.4043457824291409, az = -0.2769519524322599, ox = 38.33137598742329, angle = 4.393190281648743;
            var tolerance = new Tolerance(t, 1E-5, 1E-5, t);
            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoPoint3(ox, 0, 0), new GeoVector3(ax, ay, az), angle);
            GeoSolid3 cube = Box(0, 0, 0, 100, 100, 100).TransformBy(turn);
            GeoSolid3 block = Box(30, 30, 100 - t, 70, 60, 150).TransformBy(turn);

            Assert.True(cube.TryUnion(block, out GeoSolid3 union, tolerance));
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // The two less the overlap, 1 060 000, within the tolerance times the area where they meet; no more than the two.
            Assert.InRange(union.GetVolume(tolerance), 1060000.0 - 1200 * t, 1060000.0 + 2400 * t);
        }
    }
}
