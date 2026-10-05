using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A cell thinner than the tolerance has its two sides within the tolerance of each other, facing apart; the gluing of
    /// a boolean takes faces lying back to back from each other, and must not take a cell's two sides for such a pair.
    /// </summary>
    public class SliverCellSidesTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        // How far each corner of a 3 x 3 grid over the low step's top stands off z = 300, row by row along x: up to 0.0015,
        // as Tekla Structures leaves a face out of flat by more than a thousandth, read as triangles.
        private static readonly double[] Warp =
        {
            -0.00048489047725297496, -0.00064674465738789877, -0.00071111207466856285, 0.00037612753311577762,
            -0.00010961444144186316, 0.0012851214841020919, -0.0010610683381173658, 0.0013517996621317252,
            0.00028839976562267111, -0.0011476201622713234, 0.0014266767734056884, -0.00038733922127676124,
            -0.0012902568987556151, -0.0012876353258093332, -5.3968254746905586E-05, -0.0010410855834379618,
        };

        private const int Grid = 3;

        /// <summary>
        /// A block 2000 by 1000, 300 high over x up to 1000 and 400 beyond, its low step's top the grid of triangles.
        /// </summary>
        private static GeoSolid3 SteppedBlock()
        {
            GeoPoint3 P(int a, int b) => new GeoPoint3(1000.0 * a / Grid, 1000.0 * b / Grid, 300.0 + Warp[a * (Grid + 1) + b]);
            var faces = new List<GeoFace3>();

            for (int a = 0; a < Grid; a++)
            {
                for (int b = 0; b < Grid; b++)
                {
                    faces.Add(new GeoFace3(new GeoPolygon3(new[] { P(a, b), P(a + 1, b), P(a + 1, b + 1) }, Fine)));
                    faces.Add(new GeoFace3(new GeoPolygon3(new[] { P(a, b), P(a + 1, b + 1), P(a, b + 1) }, Fine)));
                }
            }

            faces.Add(new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 1000, 0), new GeoPoint3(2000, 1000, 0), new GeoPoint3(2000, 0, 0) }, Fine)));
            faces.Add(new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(1000, 0, 400), new GeoPoint3(2000, 0, 400), new GeoPoint3(2000, 1000, 400), new GeoPoint3(1000, 1000, 400) }, Fine)));
            faces.Add(new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(2000, 0, 0), new GeoPoint3(2000, 1000, 0), new GeoPoint3(2000, 1000, 400), new GeoPoint3(2000, 0, 400) }, Fine)));

            var x0 = new List<GeoPoint3> { new GeoPoint3(0, 1000, 0), new GeoPoint3(0, 0, 0) };
            var riser = new List<GeoPoint3>();
            var y0 = new List<GeoPoint3> { new GeoPoint3(0, 0, 0), new GeoPoint3(2000, 0, 0), new GeoPoint3(2000, 0, 400), new GeoPoint3(1000, 0, 400) };
            var y1 = new List<GeoPoint3> { new GeoPoint3(0, 1000, 0), new GeoPoint3(2000, 1000, 0), new GeoPoint3(2000, 1000, 400), new GeoPoint3(1000, 1000, 400) };

            for (int k = 0; k <= Grid; k++)
            {
                x0.Add(P(0, k));
                riser.Add(P(Grid, k));
                y0.Add(P(Grid - k, 0));
                y1.Add(P(Grid - k, Grid));
            }

            riser.Add(new GeoPoint3(1000, 1000, 400));
            riser.Add(new GeoPoint3(1000, 0, 400));
            riser.Reverse();
            y1.Reverse();
            faces.Add(new GeoFace3(new GeoPolygon3(x0, Fine)));
            faces.Add(new GeoFace3(new GeoPolygon3(riser, Fine)));
            faces.Add(new GeoFace3(new GeoPolygon3(y0, Fine)));
            faces.Add(new GeoFace3(new GeoPolygon3(y1, Fine)));

            return new GeoSolid3(faces).TurnOutwards();
        }

        // Its top at z = 300, along the warped top of the low step and through the high one.
        private static GeoSolid3 Tool() => new GeoAabb3(new GeoPoint3(700, 200, 100), new GeoPoint3(1300, 800, 300)).ToObb().ToSolid();

        // The tool's 600 by 600 by 200, less what of it stands over the warped top, a few hundred cubic millimetres.
        private const double Shared = 600.0 * 600.0 * 200.0;

        [Fact]
        public void ABlockWhoseTopIsOutOfFlat_LessABoxWhoseTopLiesAlongIt_IsClosed()
        {
            // The box's top cuts the block into cells a hair thick between z = 300 and the triangles; each such cell's top
            // and bottom were taken for back to back with each other, and the difference came out open, 0.7 litres short.
            GeoSolid3 block = SteppedBlock();
            Assert.True(block.Validate(Fine).IsValid);

            Assert.True(block.TrySubtract(Tool(), out GeoSolid3 rest, Fine, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.True(rest.Validate(Fine).IsValid, rest.Validate(Fine).ToString());
            Assert.InRange(rest.GetVolume(Fine), block.GetVolume(Fine) - Shared - 1000.0, block.GetVolume(Fine) - Shared + 1000.0);
        }

        [Fact]
        public void ABlockWhoseTopIsOutOfFlat_AndABoxWhoseTopLiesAlongIt_UniteClosed()
        {
            // Open before, 1.3 litres short.
            GeoSolid3 block = SteppedBlock();

            Assert.True(block.TryUnion(Tool(), out GeoSolid3 union, Fine, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);
            Assert.True(union.Validate(Fine).IsValid, union.Validate(Fine).ToString());
            double united = block.GetVolume(Fine) + Tool().GetVolume(Fine) - Shared;
            Assert.InRange(union.GetVolume(Fine), united - 1000.0, united + 1000.0);
        }

        [Fact]
        public void ABlockWhoseTopIsOutOfFlat_SharesTheBoxBelowIt()
        {
            Assert.True(SteppedBlock().TryIntersect(Tool(), out GeoSolid3 common, Fine, out _));
            Assert.True(common.Validate(Fine).IsValid, common.Validate(Fine).ToString());
            Assert.InRange(common.GetVolume(Fine), Shared - 1000.0, Shared + 1000.0);
        }
    }
}
