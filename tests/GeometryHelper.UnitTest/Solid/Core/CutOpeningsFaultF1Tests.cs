using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Openings cut into a closed body give a body closed as Validate reads it, as a boolean gives one: a plate with blind
    /// holes ending a hair under its top, turned and far from the origin, came out of the cut open.
    /// </summary>
    public class CutOpeningsFaultF1Tests
    {
        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 1E-5, 0.01);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoObb3(new GeoPoint3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), x1 - x0, y1 - y0, z1 - z0).ToSolid();

        /// <summary>
        /// A plate 300 by 200 by 12 with round holes 22 across, each ending a depth under the top, turned about an axis and
        /// carried far from the origin.
        /// </summary>
        private static GeoSolid3 Plate(GeoVector3 axis, double angle, double under, IEnumerable<(double X, double Y)> holes)
        {
            GeoTransform3 place = GeoTransform3.Translation(new GeoVector3(123456.7, -65432.1, 9876.5))
                * GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle);
            var openings = holes
                .Select(h => GeoSolid3.Cylinder(new GeoPoint3(h.X, h.Y, -5), new GeoPoint3(h.X, h.Y, 12 - under), 11, 16, Tolerance.Default).TransformBy(place))
                .ToList();

            return new GeoSolid3(Box(0, 0, 0, 300, 200, 12).TransformBy(place).Faces, openings);
        }

        private static IEnumerable<(double X, double Y)> Twelve()
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    yield return (40 + i * 70, 40 + j * 60);
                }
            }
        }

        /// <summary>
        /// Cuts the openings into a plate, and says how many times on the way the cut was welded closed and how many times its
        /// openings were taken out one at a time.
        /// </summary>
        private static GeoSolid3 Cut(GeoSolid3 plate, out int welded, out int takenOut)
        {
            int weldsBefore = Boolean3.ClosedByWeld;
            int takeOutsBefore = Boolean3.TakenOutOneAtATime;

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material, Hundredth));

            welded = Boolean3.ClosedByWeld - weldsBefore;
            takenOut = Boolean3.TakenOutOneAtATime - takeOutsBefore;
            return material;
        }

        private static void AssertClosedAndAsMeasured(GeoSolid3 plate, GeoSolid3 material, int precision)
        {
            SolidValidation3 check = material.Validate(Hundredth);

            Assert.DoesNotContain(check.Issues, issue => issue.Kind == SolidIssueKind.OpenEdge);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(plate.GetVolume(Hundredth), material.GetVolume(Hundredth), precision);
        }

        [Fact]
        public void APlateWithTwoBlindHolesEndingOneAndAHalfTolerancesUnderItsTop_IsCutClosed()
        {
            // Cut in at once, the plate came out open by four edges 0.015 long round the top of a hole, and was handed back so.
            GeoSolid3 plate = Plate(
                new GeoVector3(0.932557276418692, 0.0002794801258851898, 0.6692527503097676), 3.6106674045373994, 0.015, new[] { (40.0, 100.0), (110.0, 40.0) });

            GeoSolid3 material = Cut(plate, out int welded, out int takenOut);

            // The weld does not close it; the two holes are taken out one at a time.
            Assert.Equal(0, welded);
            Assert.Equal(1, takenOut);
            Assert.Empty(material.Openings);
            AssertClosedAndAsMeasured(plate, material, 6);
            Assert.Equal(711120.6116, material.GetVolume(Hundredth), 3);
        }

        [Fact]
        public void APlateWithTwelveBlindHolesThatAWeldCloses_IsCutClosed()
        {
            // Open by a crack at the top of a hole 0.011 under the plate's top; welded within the tolerance it closes, within a
            // few thousandths of a cubic millimetre of the material GetVolume measures.
            GeoSolid3 plate = Plate(
                new GeoVector3(0.932557276418692, 0.0002794801258851898, 0.6692527503097676), 3.6106674045373994, 0.011, Twelve());

            GeoSolid3 material = Cut(plate, out int welded, out int takenOut);

            Assert.Equal(1, welded);
            Assert.Equal(0, takenOut);
            AssertClosedAndAsMeasured(plate, material, 2);
        }

        [Fact]
        public void APlateWithTwelveBlindHolesThatAWeldDoesNotClose_HasThemTakenOutOneAtATime()
        {
            // Cut in at once it came out open and 0.4 cubic millimetres over; taken out one at a time by a difference, it
            // holds what GetVolume measures, which takes them out the same way.
            GeoSolid3 plate = Plate(
                new GeoVector3(-0.7252258452238636, -0.847994776371864, 0.6927852079704335), 1.8129779378943973, 0.011, Twelve());

            GeoSolid3 material = Cut(plate, out int welded, out int takenOut);

            Assert.Equal(0, welded);
            Assert.Equal(1, takenOut);
            AssertClosedAndAsMeasured(plate, material, 6);
        }
    }
}
