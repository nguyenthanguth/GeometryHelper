using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Booleans with a contact distance whose second body is a wedge thinner than the contact lying on a face of the
    /// first: putting the wedge's faces onto the box's leaves it fewer than four faces, and the boolean is still to come
    /// back, not throw; see <see cref="SolidBooleanOptions.Contact"/>.
    /// </summary>
    /// <remarks>
    /// Found by the volume takeoff on a real model, a girder against a wall whose overlap was such a wedge, 6 microns at its
    /// thickest; built here small. The box is 250 by 250 by 900, 56 250 000. The wedge is a triangle (249.994, 90),
    /// (250, 90), (250, 162) carried 900 up: 194.4, valid, its slanted face 0.006 / 72, 8.3E-5 rad, off the box's face at
    /// x = 250 and nowhere farther from it than 0.006, so within the contact of 0.01 and the angle of a degree. Without
    /// contact both booleans are made; at 120c78b, with it, both throw an ArgumentException from Touching3.PutOnto, "A solid
    /// must have at least 4 faces to enclose a volume". A wedge 0.02 thick, past the contact, is cut and intersected with
    /// contact as without.
    /// </remarks>
    public class ContactWedgeTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static readonly SolidBooleanOptions Touching = new SolidBooleanOptions(Fine, 0.01);

        private static GeoSolid3 Box() => new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(250, 250, 900)).ToObb().ToSolid();

        private static GeoSolid3 Wedge() => GeoSolid3.Extrude(
            new GeoPolygon3(new[] { new GeoPoint3(249.994, 90, 0), new GeoPoint3(250, 90, 0), new GeoPoint3(250, 162, 0) }, Fine),
            new GeoVector3(0, 0, 900),
            Fine);

        [Fact]
        public void AWedgeThinnerThanTheContactOnAFace_IsCutOutWithoutThrowing_LeavingTheBoxLessAtMostTheWedge()
        {
            Assert.True(Wedge().Validate(Fine).IsValid);

            bool made = Boolean3.TrySubtract(Box(), Wedge(), out GeoSolid3 rest, Touching, out BooleanOutcome outcome);

            Assert.True(made, outcome.ToString());
            Assert.True(rest.Validate(Fine).IsValid, rest.Validate(Fine).ToString());
            Assert.InRange(rest.GetVolume(Fine), 56250000.0 - 194.4 - 1E-3, 56250000.0 + 1E-3);
        }

        [Fact]
        public void AWedgeThinnerThanTheContactOnAFace_IsIntersectedWithoutThrowing_SharingAtMostTheWedge()
        {
            bool made = Boolean3.TryIntersect(Box(), Wedge(), out GeoSolid3 shared, Touching, out BooleanOutcome outcome);

            // Taken as touching, nothing is shared; taken as it is, the wedge. Either way no more than the wedge.
            if (outcome == BooleanOutcome.Empty)
            {
                Assert.False(made);
                return;
            }

            Assert.True(made, outcome.ToString());
            Assert.True(shared.Validate(Fine).IsValid, shared.Validate(Fine).ToString());
            Assert.InRange(shared.GetVolume(Fine), 0.0, 194.4 + 1E-3);
        }
    }
}
