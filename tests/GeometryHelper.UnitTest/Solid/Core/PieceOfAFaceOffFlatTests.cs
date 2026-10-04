using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A face a hair out of flat cut in two: each piece stands within the planar tolerance of the face's plane, as the face
    /// does, but measured from a corner of its own and about its own normal it can stand further, and a piece refused left
    /// the half it belongs to open.
    /// </summary>
    /// <remarks>
    /// The piece came from a model as it is, its hair 0.007, so it is cut within a hundredth, the default when it was found.
    /// </remarks>
    public class PieceOfAFaceOffFlatTests
    {
        private static readonly Tolerance Tolerance = new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>
        /// A piece of a column with a ledge, cut from a body turned and moved far out: its side has a corner 0.007 off the
        /// plane of the rest, along the end of the ledge.
        /// </summary>
        private static GeoSolid3 Piece()
        {
            GeoPoint3[][] faces =
            {
                new[] { P(-49755.645172586606, -2479543.8153665178, 16054.096172826752), P(-49772.886675276066, -2479805.1198993851, 16189.735281201905), P(-49964.814524989961, -2479899.6441194657, 15983.240767596153), P(-49959.914104610718, -2479825.3755315635, 15944.68910171899) },
                new[] { P(-49968.851551205342, -2479819.2109968895, 15950.174221177211), P(-49973.89325907419, -2479895.6208750335, 15989.837397429015), P(-49781.965409360295, -2479801.096654953, 16196.331911034767), P(-49764.582175114338, -2479537.6441017631, 16059.577798805376) },
                new[] { P(-49649.927384940056, -2480039.18647429, 15754.443020821498), P(-49964.814524989961, -2479899.6441194657, 15983.240767596153), P(-49772.886675276066, -2479805.1198993851, 16189.735281201905), P(-49781.965409360295, -2479801.096654953, 16196.331911034767), P(-49973.89325907419, -2479895.6208750335, 15989.837397429015), P(-50766.924804122944, -2479544.1886468739, 16566.05600895956), P(-50958.852653836839, -2479638.7128669545, 16359.56149535381), P(-50634.858245220719, -2479782.2911112779, 16124.14638552364) },
                new[] { P(-49764.582175114338, -2479537.6441017631, 16059.577798805376), P(-49781.965409360295, -2479801.096654953, 16196.331911034767), P(-49772.886675276066, -2479805.1198993851, 16189.735281201905), P(-49755.645172586606, -2479543.8153665178, 16054.096172826752) },
                new[] { P(-50953.8105019011, -2479562.2962587303, 16319.894825622421), P(-50958.852653836839, -2479638.7128669545, 16359.56149535381), P(-50766.924804122944, -2479544.1886468739, 16566.05600895956), P(-50749.541569876987, -2479280.7360936841, 16429.301896730169) },
                new[] { P(-50749.541569876987, -2479280.7360936841, 16429.301896730169), P(-50766.924804122944, -2479544.1886468739, 16566.05600895956), P(-49973.89325907419, -2479895.6208750335, 15989.837397429015), P(-49968.851551205342, -2479819.2109968895, 15950.174221177211) },
                new[] { P(-49959.914104610718, -2479825.3755315635, 15944.68910171899), P(-49964.814524989961, -2479899.6441194657, 15983.240767596153), P(-49649.927384940056, -2480039.18647429, 15754.443020821498) },
                new[] { P(-50634.858245220719, -2479782.2911112779, 16124.14638552364), P(-50958.852653836839, -2479638.7128669545, 16359.56149535381), P(-50953.8105019011, -2479562.2962587303, 16319.894825622421) },
                new[] { P(-49755.645172586606, -2479543.8153665178, 16054.096172826752), P(-49959.914104610718, -2479825.3755315635, 15944.68910171899), P(-49649.927384940056, -2480039.18647429, 15754.443020821498), P(-50634.858245220719, -2479782.2911112779, 16124.14638552364), P(-50953.8105019011, -2479562.2962587303, 16319.894825622421), P(-50749.541569876987, -2479280.7360936841, 16429.301896730169), P(-49968.851551205342, -2479819.2109968895, 15950.174221177211), P(-49764.582175114338, -2479537.6441017631, 16059.577798805376) },
            };

            return new GeoSolid3(faces.Select(f => new GeoFace3(new GeoPolygon3(f, Tolerance))));
        }

        [Fact]
        public void APieceOfAFaceAHairOutOfFlatIsKeptWhenTheFaceIsCut()
        {
            GeoSolid3 piece = Piece();
            var plane = new GeoPlane3(P(-49886.242436606, -2480034.9467781442, 15330.111103134757), new GeoVector3(0.81969821824168154, 0.55962294710614024, 0.12213512223619677));

            Assert.True(piece.IsClosed(Tolerance));
            Assert.True(piece.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.True(above.IsClosed(Tolerance));
            Assert.True(below.IsClosed(Tolerance));
            // The faces a hair out of flat hold their volume only as far as their corners say: as it is triangulated, a face
            // 0.007 off its plane over 70 000 mm2 moves it by a couple of hundred cubic millimetres.
            Assert.InRange((above.GetVolume() + below.GetVolume()) / piece.GetVolume(), 1 - 1E-5, 1 + 1E-5);
        }
    }
}
