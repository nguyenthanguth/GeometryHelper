using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Unions and differences with a bent bar in them.
    /// </summary>
    /// <remarks>
    /// Both bodies were cut by every face plane of both, and every plane of a bend runs on through the rest of the
    /// bar: thousands of cells, glued back together with faces that did not quite meet, so some results came out
    /// open though their volume was right, and a bar of a few hundred faces took too long to take part at all. The
    /// five pairs here are the ones that came out open in a scan of random bars, stars, Ls and Is; each is rebuilt
    /// from the numbers the scan drew.
    /// </remarks>
    public class BentBarBooleanTests
    {
        private static GeoSolid3 Turned(GeoSolid3 part, GeoVector3 axis, double angle)
            => part.TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, axis, angle));

        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        public static IEnumerable<object[]> Pairs => new[] { "6/1682", "6/2411", "8/167", "8/2176", "8/2755" }.Select(name => new object[] { name });

        private static (GeoSolid3 A, GeoSolid3 B) Pair(string name)
        {
            switch (name)
            {
                case "6/1682":
                    return (
                        Turned(GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(15.537791854673724, 20.846511081733436), new GeoPoint2(1.7315044358880436, 14.742414198505623), new GeoPoint2(-10.28471224971811, 23.879378005729091), new GeoPoint2(-11.901552991074253, 8.8707339274973), new GeoPoint2(-25.822504104391832, 3.0328669239956603), new GeoPoint2(-13.633057426962294, -5.8716802710083247), new GeoPoint2(-15.537791854673726, -20.846511081733432), new GeoPoint2(-1.731504435888052, -14.742414198505621), new GeoPoint2(10.284712249718101, -23.879378005729095), new GeoPoint2(11.901552991074251, -8.8707339274973016), new GeoPoint2(25.822504104391832, -3.0328669239956523), new GeoPoint2(13.633057426962297, 5.8716802710083167)),
                            new GeoCoordinateSystem3(new GeoPoint3(13, 10, -24), GeoVector3.XAxis, GeoVector3.YAxis), 47),
                            new GeoVector3(-0.065328822268791886, 0.074172071914268667, 0.046669136056056826), 5.2182945707898094),
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-38, 6, -9), new GeoPoint3(17, 38, -50), new GeoPoint3(27, 1, -21)).Fillet(9), 3, 0.3),
                            new GeoVector3(0.012236351385822686, 0.17521973497943011, 0.19879909311365296), 5.5319010473470671));
                case "6/2411":
                    return (
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(3, 1, 26), new GeoPoint3(31, -20, 12), new GeoPoint3(70, -52, 34)).Fillet(10), 5, 0.3),
                            new GeoVector3(-0.17458473875866493, 0.050957005261889177, 0.29730160152413954), 1.0953169488791921),
                        Turned(GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(40.122318952138812, 15.466076487035828), new GeoPoint2(2.463476154291202, 15.696140128973342), new GeoPoint2(-33.455174610715616, 27.013909227776146), new GeoPoint2(-14.82499417019687, -5.7146371332532961), new GeoPoint2(-6.6671443414231915, -42.479985714811967), new GeoPoint2(12.361518015905675, -9.981502995720037)),
                            new GeoCoordinateSystem3(new GeoPoint3(32, 15, 23), GeoVector3.XAxis, GeoVector3.YAxis), 69),
                            new GeoVector3(-0.059151054620347476, 0.36027062957187683, 0.3589823119616985), 1.2701747088088537));
                case "8/167":
                    return (
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(20, -1, -15), new GeoPoint3(52, 19, -66), new GeoPoint3(104, 4, -66)).Fillet(14), 5, 0.3),
                            new GeoVector3(0.17719828462097709, -0.3457060399678098, -0.045252256349312237), 3.6318091515599793),
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-35, 22, -34), new GeoPoint3(-22, -13, 7)).Fillet(18), 9, 0.3),
                            new GeoVector3(-0.31851769928751406, 0.2180349685801356, 0.40046138963683575), 1.3595290683952761));
                case "8/2176":
                    return (
                        Turned(GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(30.635009491020472, 16.411361238967054), new GeoPoint2(-7.1437125517898794, 21.7330883728835), new GeoPoint2(-32.476587507719266, 15.580380210305226), new GeoPoint2(-45.996873386009341, -0.2565006774055707), new GeoPoint2(-31.691789116892661, -15.945754619333568), new GeoPoint2(3.4517457135610532, -21.937974931873914), new GeoPoint2(33.087144481795164, -15.283715420062986), new GeoPoint2(45.739301501341338, 2.33890094174714)),
                            new GeoCoordinateSystem3(new GeoPoint3(-18, 15, -16), GeoVector3.XAxis, GeoVector3.YAxis), 18),
                            new GeoVector3(-0.17267917360769547, -0.31177252894815644, 0.10117902960683178), 5.1358616068660563),
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-32, 0, -25), new GeoPoint3(2, 23, 9), new GeoPoint3(-12, 48, -30)).Fillet(8), 4, 0.3),
                            new GeoVector3(0.37454138830049954, 0.43649476065137183, 0.10175619907758948), 5.2702632813110313));
                default:
                    return (
                        Turned(GeoSolid3.Extrude(new GeoPolygon2(new GeoPoint2(3.5015223392998278, 35.614793526589686), new GeoPoint2(-23.880064623287716, -3.5945313424848035), new GeoPoint2(12.241904876426471, -30.964584144505849)),
                            new GeoCoordinateSystem3(new GeoPoint3(-31, 6, -28), GeoVector3.XAxis, GeoVector3.YAxis), 39),
                            new GeoVector3(-0.026778286568251597, 0.26384814771071452, -0.32856777162690076), 0.094235999553574257),
                        Turned(GeoSolid3.Pipe(new GeoPolyline3(new GeoPoint3(-33, 21, -35), new GeoPoint3(14, -5, -25), new GeoPoint3(-30, -64, -18)).Fillet(8), 4, 0.3),
                            new GeoVector3(0.24356501071879877, 0.43929108648527926, 0.033061500421288192), 1.410494079538851));
            }
        }

        /// <summary>
        /// What the two bodies share, from the intersection, which never cut the bar by its own planes.
        /// </summary>
        private static double Shared(GeoSolid3 a, GeoSolid3 b) => Boolean3.Intersect(a, b).Sum(piece => piece.Volume);

        /// <summary>
        /// The cut welds corners within the point tolerance, which at the default hundredth moves what a bar and a
        /// body share by up to about a millionth of the two together, <paramref name="scale"/>; a piece lost or
        /// counted twice shows in the thousandths.
        /// </summary>
        private static void AssertVolume(double expected, GeoSolid3 actual, double scale)
            => Assert.True(Math.Abs(actual.Volume - expected) <= 5E-6 * scale, $"volume {actual.Volume}, expected {expected}");

        [Theory]
        [MemberData(nameof(Pairs))]
        public void TheUnion_IsClosed_AndHoldsBoth_LessWhatTheyShare(string pair)
        {
            (GeoSolid3 a, GeoSolid3 b) = Pair(pair);

            Assert.True(a.TryUnion(b, out GeoSolid3 union));

            Assert.True(union.IsClosed());
            AssertVolume(a.Volume + b.Volume - Shared(a, b), union, a.Volume + b.Volume);
        }

        [Theory]
        [MemberData(nameof(Pairs))]
        public void EachDifference_IsClosed_AndHoldsItsBody_LessWhatTheyShare(string pair)
        {
            (GeoSolid3 a, GeoSolid3 b) = Pair(pair);
            double shared = Shared(a, b);

            Assert.True(a.TrySubtract(b, out GeoSolid3 aLessB));
            Assert.True(b.TrySubtract(a, out GeoSolid3 bLessA));

            Assert.True(aLessB.IsClosed());
            Assert.True(bLessA.IsClosed());
            AssertVolume(a.Volume - shared, aLessB, a.Volume + b.Volume);
            AssertVolume(b.Volume - shared, bLessA, a.Volume + b.Volume);
        }

        /// <summary>
        /// A bar of some two hundred faces, hooked down through a plate: too many faces for the old cutting to take part
        /// in a scan at all.
        /// </summary>
        [Fact]
        public void AFinelyFacettedHookThroughAPlate_UnitesAndSubtractsExactly()
        {
            GeoSolid3 plate = Box(0, 0, 0, 200, 200, 20);
            GeoSolid3 hook = GeoSolid3.Pipe(
                new GeoPolyline3(new GeoPoint3(20, 100, -60), new GeoPoint3(100, 110, 60), new GeoPoint3(180, 90, -60)).Fillet(30), 8, 0.05);
            Assert.True(hook.Faces.Count > 150, $"{hook.Faces.Count} faces");

            double shared = Shared(plate, hook);
            Stopwatch watch = Stopwatch.StartNew();

            Assert.True(plate.TryUnion(hook, out GeoSolid3 union));
            Assert.True(plate.TrySubtract(hook, out GeoSolid3 plateLessHook));
            Assert.True(hook.TrySubtract(plate, out GeoSolid3 hookLessPlate));

            Assert.True(union.IsClosed());
            Assert.True(plateLessHook.IsClosed());
            Assert.True(hookLessPlate.IsClosed());
            AssertVolume(plate.Volume + hook.Volume - shared, union, plate.Volume + hook.Volume);
            AssertVolume(plate.Volume - shared, plateLessHook, plate.Volume + hook.Volume);
            AssertVolume(hook.Volume - shared, hookLessPlate, plate.Volume + hook.Volume);

            // The three took minutes when both bodies were cut by every plane of both.
            Assert.True(watch.Elapsed.TotalSeconds < 30, $"{watch.Elapsed.TotalSeconds:0.0} s");
        }

        /// <summary>
        /// Openings are carved into both bodies before either is cut, so a bar passing clear through a hole fills
        /// only itself: the rest of the hole stays open, and the bar is material inside it.
        /// </summary>
        [Fact]
        public void ABarThroughAHoleInAPlate_FillsOnlyItself()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 10).WithOpenings(new[] { Box(40, 40, -5, 60, 60, 15) });
            GeoSolid3 bar = GeoSolid3.Cylinder(new GeoPoint3(50, 50, -30), new GeoPoint3(50, 50, 40), 4, 16);

            Assert.True(plate.TryUnion(bar, out GeoSolid3 union));

            Assert.Empty(union.Openings);
            Assert.True(union.IsClosed());
            Assert.Equal(PointLocation.OutSide, union.Locate(new GeoPoint3(44, 44, 5)));
            Assert.Equal(PointLocation.Inside, union.Locate(new GeoPoint3(50, 50, 5)));
            Assert.Equal(PointLocation.Inside, union.Locate(new GeoPoint3(20, 20, 5)));
            AssertVolume(100 * 100 * 10 - 20 * 20 * 10 + bar.Volume, union, plate.Volume + bar.Volume);
        }

        /// <summary>
        /// A tool reaching into a hole takes nothing from the hole, and the plate keeps the rest of it.
        /// </summary>
        [Fact]
        public void ABoxTakenOutAcrossAHole_LeavesTheRestOfTheHoleOpen()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 10).WithOpenings(new[] { Box(40, 40, -5, 60, 60, 15), Box(80, 80, -5, 90, 90, 15) });
            GeoSolid3 tool = Box(30, 45, -10, 50, 55, 20);

            Assert.True(plate.TrySubtract(tool, out GeoSolid3 result));

            Assert.Empty(result.Openings);
            Assert.True(result.IsClosed());
            Assert.Equal(PointLocation.OutSide, result.Locate(new GeoPoint3(55, 55, 5)));
            Assert.Equal(PointLocation.OutSide, result.Locate(new GeoPoint3(35, 50, 5)));
            Assert.Equal(PointLocation.OutSide, result.Locate(new GeoPoint3(85, 85, 5)));
            Assert.Equal(PointLocation.Inside, result.Locate(new GeoPoint3(35, 30, 5)));

            // The plate, less both holes, less the part of the tool standing over material: 10 x 10 of it lies
            // over the plate, the other 10 x 10 over the hole.
            AssertVolume(100 * 100 * 10 - 20 * 20 * 10 - 10 * 10 * 10 - 10 * 10 * 10, result, plate.Volume + tool.Volume);
        }
    }
}
