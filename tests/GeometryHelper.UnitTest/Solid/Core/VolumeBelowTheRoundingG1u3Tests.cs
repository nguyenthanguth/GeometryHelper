using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body whose openings will not come out closed within the rounding of its coordinates, asked its volume below that
    /// rounding, has them cut within what rounding can move a corner off a plane, a trillionth of the size of the coordinates,
    /// as its section and its cells are. Cut at the tolerance asked instead, a plate turned about in space with a blind
    /// opening down from its top measured nought, or the whole plate, and the volume was not trusted.
    /// </summary>
    public class VolumeBelowTheRoundingG1u3Tests
    {
        /// <summary>The plate's length, leaning in space.</summary>
        private static readonly GeoVector3 Along = new GeoVector3(0.46852538559841106, 0.8586705147503214, 0.207771292887825);

        /// <summary>The plate's width.</summary>
        private static readonly GeoVector3 Across = new GeoVector3(0.6278105508683705, -0.48907640703675703, 0.605523063390958);

        /// <summary>The plate's up.</summary>
        private static readonly GeoVector3 Up = new GeoVector3(0.6215608379460644, -0.15326191692144078, -0.7682271210738084);

        private static GeoPoint3 At(double x, double y, double z) => GeoPoint3.Origin.Add(Along.Multiply(x)).Add(Across.Multiply(y)).Add(Up.Multiply(z));

        /// <summary>
        /// A plate 200 by 100 by 3 at the origin, turned about in space, with an opening 32.7 by 51 down from 7 over its top to
        /// 0.9 over its bottom.
        /// </summary>
        private static GeoSolid3 Plate()
        {
            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(At(0, 0, 0), At(200, 0, 0), At(200, 100, 0), At(0, 100, 0)), Up.Multiply(3), Tolerance.Default);
            GeoSolid3 opening = GeoSolid3.Extrude(
                new GeoPolygon3(At(23.5, 16.9, 0.9), At(56.2, 16.9, 0.9), At(56.2, 67.9, 0.9), At(23.5, 67.9, 0.9)), Up.Multiply(9.1), Tolerance.Default);

            return body.WithOpenings(new[] { opening });
        }

        /// <summary>The plate less what the opening takes of it.</summary>
        private const double Held = 60_000 - 32.7 * 51 * 2.1;

        /// <summary>The plate's area with the opening's walls and floor: the floor takes the place of the top it leaves.</summary>
        private const double Area = 41_800 + 2 * (32.7 + 51) * 2.1;

        [Theory]
        [InlineData(0.0)]
        [InlineData(1E-15)]
        [InlineData(1E-13)]
        public void BelowTheRounding_APlateWithABlindOpening_MeasuresWhatItHoldsAndIsTrusted(double tolerance)
        {
            // At nought it measured nought, its area nought too, at 1E-15 the whole plate, 60 000, and at 1E-13 what it holds,
            // none of them trusted.
            var within = new Tolerance(tolerance, tolerance, tolerance, tolerance);
            GeoSolid3 plate = Plate();

            Assert.Equal(Held, plate.GetVolume(within), 1E-6);
            Assert.Equal(Area, plate.GetSurfaceArea(within), 1E-6);
            Assert.True(plate.TryGetVolume(out double volume, within));
            Assert.Equal(Held, volume, 1E-6);
        }
    }
}
