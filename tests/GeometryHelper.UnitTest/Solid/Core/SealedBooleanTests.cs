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
    /// What a boolean cut within a tolerance wider than the default gives closes within the default too, where faces of the
    /// two bodies stood within the tolerance of each other: the corners within the tolerance of each other are made one, and
    /// put on the edges they stand on; see <see cref="Weld3"/>.
    /// </summary>
    public class SealedBooleanTests
    {
        private static readonly Tolerance Wide = new Tolerance(0.05, 0.01, Tolerance.DefaultEqualAngleRad, 0.05);

        private static readonly Tolerance Pieces = new Tolerance(0.01, 0.01 * 0.01 * 0.125, Tolerance.DefaultEqualAngleRad, 0.01);

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoFace3 Face(Tolerance tolerance, params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners, tolerance), null, tolerance);

        /// <summary>
        /// A slab 20 000 by 10 000 by 300 with a notch 2 000..6 200 along x, from its front to y = 5 000, the top's two
        /// corners at the back of the notch standing <paramref name="off"/> short of it, as a modeller's can.
        /// </summary>
        private static GeoSolid3 NotchedSlab(double off)
        {
            const double h = 300, back = 5000;
            var bottom = new[] { (0.0, 0.0), (2000.0, 0.0), (2000.0, back), (6200.0, back), (6200.0, 0.0), (20000.0, 0.0), (20000.0, 10000.0), (0.0, 10000.0) };
            var top = bottom.Select(p => p.Item2 == back ? (p.Item1, back - off) : p).ToArray();
            var faces = new List<GeoFace3>
            {
                Face(Pieces, bottom.Reverse().Select(p => new GeoPoint3(p.Item1, p.Item2, 0)).ToArray()),
                Face(Pieces, top.Select(p => new GeoPoint3(p.Item1, p.Item2, h)).ToArray()),
            };

            for (int i = 0; i < bottom.Length; i++)
            {
                int j = (i + 1) % bottom.Length;
                faces.Add(Face(Pieces,
                    new GeoPoint3(bottom[i].Item1, bottom[i].Item2, 0), new GeoPoint3(bottom[j].Item1, bottom[j].Item2, 0),
                    new GeoPoint3(top[j].Item1, top[j].Item2, h), new GeoPoint3(top[i].Item1, top[i].Item2, h)));
            }

            return new GeoSolid3(faces);
        }

        [Theory]
        [InlineData(0.0, 0.0113)]
        [InlineData(0.0, 0.045)]
        [InlineData(0.003, 0.02)]
        [InlineData(-0.003, -0.02)]
        [InlineData(0.006, 0.007)]
        public void ASlabLessAPocketWhosePlanePassesAHairOffANotchOfIt_ClosesWithinAHundredth(double off, double d)
        {
            GeoSolid3 slab = NotchedSlab(off);
            GeoSolid3 pocket = Box(10000, 5000 + d, -100, 14200, 8000, 400);

            // The pocket's front, 4 200 long, passes d off the back of the notch, 4 000 along x from it, and cuts the slab's
            // faces there; the pieces met the back of the notch on copies of its edges d apart.
            Assert.True(slab.IsClosed());
            Assert.True(slab.TrySubtract(pocket, out GeoSolid3 rest, Wide, out BooleanOutcome outcome));
            Assert.Equal(BooleanOutcome.Made, outcome);

            Assert.True(rest.IsClosed(Wide));
            Assert.True(rest.IsClosed(), rest.Validate().ToString());

            // As much as the pocket holds within the slab, within the tolerance times the area of the faces within it of the
            // pocket's front, the front itself and the back of the notch, which the cut lays in one plane.
            double taken = slab.GetVolume() - rest.GetVolume();
            double exact = 4200.0 * (3000.0 - d) * 300.0;
            Assert.InRange(taken, exact - 0.05 * 2 * 4200 * 300, exact + 0.05 * 2 * 4200 * 300);
        }

        [Fact]
        public void TheCornersOfAGroupGoToTheOneThatMovesTheVolumeLeast()
        {
            // A box whose top has its corner over (10, 10) at y = 10.02: moved along the top to the sides' corner, it moves
            // nothing; the sides' corner moved to it would tilt the side at y = 10.
            var faces = new List<GeoFace3>(Box(0, 0, 0, 10, 10, 10).Faces.Where(face => face.Normal.Z < 0.5))
            {
                Face(Pieces, new GeoPoint3(0, 0, 10), new GeoPoint3(10, 0, 10), new GeoPoint3(10, 10.02, 10), new GeoPoint3(0, 10, 10)),
            };
            var box = new GeoSolid3(faces);

            Assert.True(box.IsClosed(Wide));
            Assert.False(box.IsClosed());

            GeoSolid3 sealedBox = Weld3.Sealed(box, Wide);

            Assert.True(sealedBox.IsClosed(new Tolerance(1E-9, 1E-9, Tolerance.DefaultEqualAngleRad, 1E-9)));
            Assert.Equal(1000.0, sealedBox.GetVolume(), 9);
            Assert.Contains(sealedBox.Faces, face => face.Normal.Z > 0.5 && face.Boundary.Vertices.Contains(new GeoPoint3(10, 10, 10)));
        }

        [Fact]
        public void ACornerAHairOffTheEdgeOfAnotherFace_IsPutOnIt()
        {
            // A box whose front is two faces side by side, the corner between them at the top 0.02 in front of the top's edge.
            var loose = new Tolerance(0.05, 0.01, Tolerance.DefaultEqualAngleRad, 0.05);
            GeoPoint3 bottomMiddle = new GeoPoint3(4, 0, 0), topMiddle = new GeoPoint3(4, -0.02, 10);
            var faces = new List<GeoFace3>(Box(0, 0, 0, 10, 10, 10).Faces.Where(face => face.Normal.Y > -0.5))
            {
                Face(loose, new GeoPoint3(0, 0, 0), bottomMiddle, topMiddle, new GeoPoint3(0, 0, 10)),
                Face(loose, bottomMiddle, new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0, 10), topMiddle),
            };
            var box = new GeoSolid3(faces);

            Assert.True(box.IsClosed(Wide));
            Assert.False(box.IsClosed());

            GeoSolid3 sealedBox = Weld3.Sealed(box, Wide);

            Assert.True(sealedBox.IsClosed(new Tolerance(1E-9, 1E-9, Tolerance.DefaultEqualAngleRad, 1E-9)));
            Assert.Contains(sealedBox.Faces, face => face.Normal.Z > 0.5 && face.Boundary.Vertices.Contains(topMiddle));
            Assert.Contains(sealedBox.Faces, face => face.Normal.Z < -0.5 && face.Boundary.Vertices.Contains(bottomMiddle));

            // And the front bulges by the corner: a third of the front's triangle under it times 0.02, as the faces read it.
            Assert.InRange(sealedBox.GetVolume(), 1000.0, 1000.5);
        }

        [Fact]
        public void ABodyOpenWithinItsOwnTolerance_ByABendTwoFacesHaveAtTwoPlaces_IsClosedByPuttingEachCornerOnTheOthersEdge()
        {
            // A prism 20 000 along x, 1 000 deep and 300 high, its front bent at x = 10 000 to run 0.5 back over the second
            // half; the top has the bend 18 further along, on the line of the second half. Past the bend the edges of the
            // top and of the front's first half lie on two lines 0.5 apart at the far end, which no tolerance takes for one.
            const double h = 300;
            GeoPoint3 topBend = new GeoPoint3(10018, 0.5 * 18 / 10000, h);
            var faces = new List<GeoFace3>
            {
                Face(Pieces, new GeoPoint3(0, 1000, 0), new GeoPoint3(20000, 1000, 0), new GeoPoint3(20000, 0.5, 0), new GeoPoint3(10000, 0, 0), new GeoPoint3(0, 0, 0)),
                Face(Pieces, new GeoPoint3(0, 0, h), topBend, new GeoPoint3(20000, 0.5, h), new GeoPoint3(20000, 1000, h), new GeoPoint3(0, 1000, h)),
                Face(Pieces, new GeoPoint3(0, 0, 0), new GeoPoint3(10000, 0, 0), new GeoPoint3(10000, 0, h), new GeoPoint3(0, 0, h)),
                Face(Pieces, new GeoPoint3(10000, 0, 0), new GeoPoint3(20000, 0.5, 0), new GeoPoint3(20000, 0.5, h), new GeoPoint3(10000, 0, h)),
                Face(Pieces, new GeoPoint3(20000, 0.5, 0), new GeoPoint3(20000, 1000, 0), new GeoPoint3(20000, 1000, h), new GeoPoint3(20000, 0.5, h)),
                Face(Pieces, new GeoPoint3(20000, 1000, 0), new GeoPoint3(0, 1000, 0), new GeoPoint3(0, 1000, h), new GeoPoint3(20000, 1000, h)),
                Face(Pieces, new GeoPoint3(0, 1000, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, h), new GeoPoint3(0, 1000, h)),
            };
            var prism = new GeoSolid3(faces);

            Assert.False(prism.IsClosed(Wide));

            GeoSolid3 sealedPrism = Weld3.Sealed(prism, Wide);

            Assert.True(sealedPrism.IsClosed(new Tolerance(1E-9, 1E-9, Tolerance.DefaultEqualAngleRad, 1E-9)));
            Assert.True(sealedPrism.Validate().IsValid);

            // The prism less the wedge the bend leaves in front, 0.5 deep at the far end.
            Assert.Equal(20000.0 * 1000.0 * h - 0.5 * 10000.0 * 0.5 * h, sealedPrism.GetVolume(), 2);
        }

        [Fact]
        public void ABodyClosedWithinTheDefault_OrWithAFaceMissing_IsLeftAsItIs()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);
            Assert.Same(box, Weld3.Sealed(box, Wide));

            // A face missing is no corner out of place, and no weld closes it.
            var open = new GeoSolid3(box.Faces.Skip(1));
            Assert.Same(open, Weld3.Sealed(open, Wide));

            // A top 0.005 off its sides' corner: closed within the default, as a boolean within the default promises, though
            // not within a thousandth.
            var faces = new List<GeoFace3>(box.Faces.Where(face => face.Normal.Z < 0.5))
            {
                Face(Pieces, new GeoPoint3(0, 0, 10), new GeoPoint3(10, 0, 10), new GeoPoint3(10, 10.005, 10), new GeoPoint3(0, 10, 10)),
            };
            var nearly = new GeoSolid3(faces);

            Assert.True(nearly.IsClosed());
            Assert.False(nearly.IsClosed(new Tolerance(0.001, 0.001)));
            Assert.Same(nearly, Weld3.Sealed(nearly, Wide));
            Assert.Same(nearly, Weld3.Sealed(nearly, Tolerance.Global));
        }
    }
}
