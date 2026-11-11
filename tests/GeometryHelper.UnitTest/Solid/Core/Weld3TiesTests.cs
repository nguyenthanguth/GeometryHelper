using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A union whose weld weighs two corners alike to the rounding of the coordinates: of two corners part way along the
    /// edges of two faces, the weld takes the one standing off the other's face to it, rather than tilting that face.
    /// </summary>
    public class Weld3TiesTests
    {
        // A cube of 100 and a block 40 by 30 by 50.001 running exactly 0.001 into its top, turned about an axis through
        // (18.52, 0, 0) by 0.503 rad, as .NET 10 turns them: its cosine of the angle is a bit above .NET Framework's, and the
        // union comes out open and is welded, where turned as .NET Framework turns them it closes as it is.
        private static readonly GeoPoint3[] CubeCorners =
        {
            new GeoPoint3(1.3392463766215208, -5.583722516574382, 4.079565280062154),
            new GeoPoint3(-34.30636175428009, 83.47272091811877, -24.175354711710973),
            new GeoPoint3(58.46355509561005, 113.61712062432943, -46.19938009347702),
            new GeoPoint3(94.10916322651165, 24.56067718963628, -17.944460101703896),
            new GeoPoint3(12.435784063786352, 28.478941048031167, 97.44230839658495),
            new GeoPoint3(105.20570091367648, 58.62334075424182, 75.4182830148189),
            new GeoPoint3(69.56009278277487, 147.679784188935, 47.163363023045775),
            new GeoPoint3(-23.20982406711526, 117.53538448272433, 69.18738840481183),
        };

        private static readonly GeoPoint3[] BlockCorners =
        {
            new GeoPoint3(29.572965714106036, 64.23885336366666, 82.35769115709203),
            new GeoPoint3(18.879283274835554, 90.95578639407462, 73.8812151595601),
            new GeoPoint3(55.98725001479161, 103.01354627655887, 65.07160500685367),
            new GeoPoint3(66.68093245406209, 76.29661324615093, 73.54808100438561),
            new GeoPoint3(35.12134552306533, 81.27052577260508, 129.0399963427846),
            new GeoPoint3(72.22931226302138, 93.32828565508935, 120.23038619007818),
            new GeoPoint3(61.535629823750895, 120.04521868549729, 111.75391019254624),
            new GeoPoint3(24.427663083794844, 107.98745880301304, 120.56352034525267),
        };

        private static readonly int[][] BoxFaces =
        {
            new[] { 0, 1, 2, 3 },
            new[] { 4, 5, 6, 7 },
            new[] { 0, 3, 5, 4 },
            new[] { 2, 1, 7, 6 },
            new[] { 3, 2, 6, 5 },
            new[] { 1, 0, 4, 7 },
        };

        private static GeoSolid3 Box(GeoPoint3[] corners, Tolerance tolerance) =>
            new GeoSolid3(BoxFaces.Select(f => new GeoFace3(new GeoPolygon3(f.Select(i => corners[i]), tolerance))));

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TwoCornersAlongEdgesThatOnlyRoundingWeighsApart_AreWeldedWithoutTiltingAFace(bool blockFirst)
        {
            // The weld took a corner 0.0003 off the other's face, of 10 000 square millimetres, to it, the two weighing 3.3E-18
            // and 2.1E-17 cubic millimetres moved, and the union came out 1.39 short of the two less their overlap, where
            // the tolerance times the area where they meet is 1.2, closed and valid.
            var tolerance = new Tolerance(1E-3, 1E-5, 1E-5, 1E-3);
            GeoSolid3 cube = Box(CubeCorners, tolerance);
            GeoSolid3 block = Box(BlockCorners, tolerance);

            GeoSolid3 union;
            Assert.True(
                blockFirst ? block.TryUnion(cube, out union, tolerance) : cube.TryUnion(block, out union, tolerance)
            );
            Assert.True(union.Validate(tolerance).IsValid, union.Validate(tolerance).ToString());

            // The two less the overlap, 1 060 000, within the tolerance times the area where they meet, 1 200.
            Assert.InRange(union.GetVolume(tolerance), 1060000.0 - 1.2, 1060000.0 + 1.2);
        }
    }
}
