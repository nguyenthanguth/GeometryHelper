using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Solid.MeetingInSpaceOracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body of two pieces either side of a plane, touching it nowhere, read as nought from it.
    /// </summary>
    /// <remarks>
    /// The distance reads the heights of all the body's corners at once, so corners each side of the plane read as the body
    /// straddling it, though no piece does. The pieces are the boxes 40 by 40 from z = 10 to 20 and from z = -20 to -10,
    /// and the plane is z = 0: each piece is 10 from it.
    /// </remarks>
    public class OldFaultF9Tests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private static GeoPlane3 Middle() => new GeoPlane3(Pt(0, 0, 0), new GeoVector3(0, 0, 1));

        [Fact]
        public void ABoxCutInTwoByAnOpeningAcrossThePlane_IsTenFromIt()
        {
            // The box from z = -20 to 20 less the opening from z = -10 to 10, wider than it every way.
            GeoSolid3 cut = Block(Pt(0, 0, -20), Pt(40, 40, 20)).WithOpenings(new[] { Block(Pt(-5, -5, -10), Pt(45, 45, 10)) });

            Assert.False(Middle().CollidesWith(cut, Within));
            Assert.Equal(10.0, cut.DistanceTo(Middle(), Within), 9);
        }

        [Fact]
        public void ABodyOfTwoShellsEitherSideOfThePlane_IsTenFromIt()
        {
            GeoSolid3 pair = new GeoSolid3(Block(Pt(0, 0, 10), Pt(40, 40, 20)).Faces.Concat(Block(Pt(0, 0, -20), Pt(40, 40, -10)).Faces));

            Assert.False(Middle().CollidesWith(pair, Within));
            Assert.Equal(10.0, pair.DistanceTo(Middle(), Within), 9);
        }

        [Fact]
        public void ABodyOfTwoShellsEitherSideOfThePlane_IsJoinedToItFromTheNearestCornerAsFarAsItsDistance()
        {
            GeoSolid3 pair = new GeoSolid3(Block(Pt(0, 0, 10), Pt(40, 40, 20)).Faces.Concat(Block(Pt(0, 0, -20), Pt(40, 40, -10)).Faces));

            Assert.Equal(pair.DistanceTo(Middle(), Within), pair.GetShortestLineTo(Middle(), Within).Length, 9);
        }

        [Fact]
        public void AShellCrossingThePlaneBesideAnotherClearOfIt_IsNoughtFromIt()
        {
            GeoSolid3 pair = new GeoSolid3(Block(Pt(0, 0, -5), Pt(40, 40, 5)).Faces.Concat(Block(Pt(60, 0, 10), Pt(100, 40, 20)).Faces));

            Assert.True(Middle().CollidesWith(pair, Within));
            Assert.Equal(0.0, pair.DistanceTo(Middle(), Within));
        }

        [Fact]
        public void ABoxClearOfThePlane_IsAsFarAsItsNearestFace_ThroughTheBoxDistancesToo()
        {
            var box = new GeoAabb3(Pt(0, 0, 10), Pt(40, 40, 20));

            Assert.Equal(10.0, box.ToObb().ToSolid().DistanceTo(Middle(), Within), 9);
            Assert.Equal(10.0, Middle().DistanceTo(box, Within), 9);
            Assert.Equal(10.0, Middle().DistanceTo(box.ToObb(), Within), 9);
        }
    }
}
