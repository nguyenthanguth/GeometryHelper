using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// REVCLOUDGRIPS against AutoCAD 2021's <c>GetGripPoints</c> and <c>MoveGripPointsAt</c>, run in its console through a plugin
    /// (16 November): with the variable on a cloud shows the few grips of the shape it goes round, and a grip dragged draws the
    /// cloud again along the new shape; with it off every vertex has a grip.
    /// </summary>
    public class RevcloudGripsTests
    {
        private static DxfPolyline Cloud(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static List<GeoPoint2> Points(string text) =>
            text.Split(';').Select(t => t.Split(',')).Select(p => new GeoPoint2(Number(p[0]), Number(p[1]))).ToList();

        private static void SameVertices(string expected, DxfPolyline cloud)
        {
            List<GeoPoint2> points = Points(expected);
            Assert.Equal(points.Count, cloud.NumberOfVertices);

            for (int i = 0; i < points.Count; i++)
            {
                // AutoCAD's console gave the vertices to six decimals.
                Near(points[i], cloud.GetPoint2dAt(i), 6E-6);
            }
        }

        private static CadCommandSession Arcs(out CadDocument document, double length)
        {
            document = Blank();
            CadCommandSession session = Session(document);
            Run(
                session,
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUDAPPROXARCLEN",
                length.ToString("R", CultureInfo.InvariantCulture)
            );
            return session;
        }

        /// <summary>
        /// Draws the cloud a row names: R then x, y, width, height, arc length and 1 for counter-clockwise; P then the arc length
        /// and the corners after semicolons; C then the centre, the radius and the arc length.
        /// </summary>
        private static CadCommandSession Make(string make, out CadDocument document)
        {
            CadCommandSession session;

            if (make.StartsWith("P,", StringComparison.Ordinal))
            {
                string[] parts = make.Substring(2).Split(new[] { ';' }, 2);
                session = Arcs(out document, Number(parts[0]));
                var inputs = new List<object> { "REVCLOUD", "P" };
                inputs.AddRange(Points(parts[1]).Cast<object>());
                inputs.Add(Enter);
                Run(session, inputs.ToArray());
                return session;
            }

            double[] n = make.Substring(2).Split(',').Select(Number).ToArray();

            if (make.StartsWith("R,", StringComparison.Ordinal))
            {
                session = Arcs(out document, n[4]);
                bool counterClockwise = n[5] != 0.0;
                Run(
                    session,
                    "REVCLOUD",
                    P(n[0], counterClockwise ? n[1] + n[3] : n[1]),
                    P(n[0] + n[2], counterClockwise ? n[1] : n[1] + n[3])
                );
                return session;
            }

            session = Arcs(out document, n[3]);
            Run(session, "CIRCLE", P(n[0], n[1]), n[2].ToString("R", CultureInfo.InvariantCulture));
            Run(session, "REVCLOUD", "O", P(n[0] + n[2], n[1]), "N");
            return session;
        }

        /// <summary>The rows AutoCAD 2021 gave: a name, the cloud, the grip dragged, the shift (dx, dy), the vertices before and after.</summary>
        public static readonly IEnumerable<object[]> Dragged = new[]
        {
            new object[]
            {
                "The top middle of a rectangle 60 by 40, dragged up 5",
                "R,0,0,60,40,10,0",
                3,
                0.0,
                5.0,
                "0,0;0,10;0,20;0,30;0,40;10,40;20,40;30,40;40,40;50,40;60,40;60,30;60,20;60,10;60,0;50,0;40,0;30,0;20,0;10,0",
                "0,0;0,11;0,22;0,33;0,44;10,45;21,45;32,45;43,45;54,45;60,40;60,29;60,18;60,7;56,0;45,0;34,0;23,0;12,0",
            },
            new object[]
            {
                "The first corner of a rectangle 60 by 40, dragged out 5 each way",
                "R,100,0,60,40,10,0",
                0,
                -5.0,
                -5.0,
                "100,0;100,10;100,20;100,30;100,40;110,40;120,40;130,40;140,40;150,40;160,40;160,30;160,20;160,10;160,0;150,0;140,0;130,0;120,0;110,0",
                "95,-5;96.214747,5.932721;97.429494,16.865442;98.64424,27.798163;99.858987,38.730884;109.723074,40;120.723074,40;131.723074,40;142.723074,40;153.723074,40;160,35.276926;160,24.276926;160,13.276926;160,2.276926;151.30262,-0.669029;140.33502,-1.512691;129.367421,-2.356352;118.399822,-3.200014;107.432222,-4.043675",
            },
            new object[]
            {
                "The middle of the left side of a rectangle 60 by 40, dragged out 5",
                "R,200,0,60,40,10,0",
                1,
                -5.0,
                0.0,
                "200,0;200,10;200,20;200,30;200,40;210,40;220,40;230,40;240,40;250,40;260,40;260,30;260,20;260,10;260,0;250,0;240,0;230,0;220,0;210,0",
                "195,0;195,11;195,22;195,33;199,40;210,40;221,40;232,40;243,40;254,40;260,35;260,24;260,13;260,2;251,0;240,0;229,0;218,0;207,0",
            },
            new object[]
            {
                "The top middle of a rectangle 120 by 100 with arcs 15 long, dragged up 5",
                "R,300,0,120,100,15,0",
                3,
                0.0,
                5.0,
                "300,0;300,15;300,30;300,45;300,60;300,75;300,90;305,100;320,100;335,100;350,100;365,100;380,100;395,100;410,100;420,95;420,80;420,65;420,50;420,35;420,20;420,5;410,0;395,0;380,0;365,0;350,0;335,0;320,0",
                "300,0;300,16.255004;300,32.510008;300,48.765013;300,65.020017;300,81.275021;300,97.530025;308.785029,105;325.040034,105;341.295038,105;357.550042,105;373.805046,105;390.06005,105;406.315055,105;420,102.429941;420,86.174937;420,69.919933;420,53.664929;420,37.409925;420,21.15492;420,4.899916;408.644912,0;392.389908,0;376.134904,0;359.879899,0;343.624895,0;327.369891,0;311.114887,0",
            },
            new object[]
            {
                "The first corner of a polygon of five corners, dragged out 5 each way",
                "P,10;600,0;660,0;660,40;630,60;600,40",
                0,
                -5.0,
                -5.0,
                "600,0;610,0;620,0;630,0;640,0;650,0;660,0;660,10;660,20;660,30;660,40;651.679497,45.547002;643.358994,51.094004;635.038491,56.641006;626.717988,57.811992;618.397485,52.26499;610.076982,46.717988;601.756479,41.170986;600,32.111026;600,22.111026;600,12.111026",
                "595,-5;605.954134,-4.157374;616.908269,-3.314749;627.862403,-2.472123;638.816537,-1.629497;649.770672,-0.786871;660,0.726947;660,11.713442;660,22.699937;660,33.686433;656.111889,42.592074;646.970573,48.686285;637.829256,54.780496;628.687939,59.125293;619.546623,53.031082;610.405306,46.936871;601.26399,40.84266;598.954504,30.590536;597.741249,19.671238;596.527993,8.751939",
            },
            new object[]
            {
                "The middle of the first side of a polygon of five corners, dragged up 5",
                "P,10;700,0;760,0;760,40;730,60;700,40",
                1,
                0.0,
                5.0,
                "700,0;710,0;720,0;730,0;740,0;750,0;760,0;760,10;760,20;760,30;760,40;751.679497,45.547002;743.358994,51.094004;735.038491,56.641006;726.717988,57.811992;718.397485,52.26499;710.076982,46.717988;701.756479,41.170986;700,32.111026;700,22.111026;700,12.111026",
                "700,5;710.986495,5;721.97299,5;732.959486,5;743.945981,5;754.932476,5;760,10.918971;760,21.905466;760,32.891961;756.772929,42.151381;747.631612,48.245592;738.490296,54.339803;729.348979,59.565986;720.207663,53.471775;711.066346,47.377564;701.92503,41.283353;700,31.327103;700,20.340607",
            },
            new object[]
            {
                "The corner a polygon's arcs cut off, dragged 10 each way",
                "P,10;800,0;860,0;860,40;830,60;800,40",
                6,
                10.0,
                10.0,
                "800,0;810,0;820,0;830,0;840,0;850,0;860,0;860,10;860,20;860,30;860,40;851.679497,45.547002;843.358994,51.094004;835.038491,56.641006;826.717988,57.811992;818.397485,52.26499;810.076982,46.717988;801.756479,41.170986;800,32.111026;800,22.111026;800,12.111026",
                "800,0;810.986495,0;821.97299,0;832.959486,0;843.945981,0;854.932476,0;860,5.918971;860,16.905466;860,27.891961;860,38.878457;854.527909,48.208136;848.433698,57.349453;842.339487,66.490769;834.58486,65.938645;825.795664,59.346748;817.006468,52.754851;808.217272,46.162954;800,39.285095;800,28.298599;800,17.312104;800,6.325609",
            },
            new object[]
            {
                "The centre of a circle of radius 20, dragged right 5",
                "C,950,50,20,10",
                0,
                5.0,
                0.0,
                "970,50;967.551651,59.588511;960.806046,66.82942;951.414744,69.9499;941.677063,68.185949;933.977128,61.969443;930.20015,52.8224;931.270866,42.984335;936.927128,34.86395;945.784084,30.449398;955.673244,30.821515;964.173395,35.889193;969.203406,44.41169",
                "975,50;972.293411,60.046788;964.906205,67.374323;954.837797,69.999342;944.81329,67.211361;937.545907,59.764971;935.002631,49.675604;937.871821,39.674038;945.376906,32.467286;955.486568,30.00592;965.464535,32.95613;972.610183,40.519416",
            },
            new object[]
            {
                "The right quadrant of a circle of radius 20, dragged right 5",
                "C,1050,50,20,10",
                1,
                5.0,
                0.0,
                "1070,50;1067.551651,59.588511;1060.806046,66.82942;1051.414744,69.9499;1041.677063,68.185949;1033.977128,61.969443;1030.20015,52.8224;1031.270866,42.984335;1036.927128,34.86395;1045.784084,30.449398;1055.673244,30.821515;1064.173395,35.889193;1069.203406,44.41169",
                "1075,50;1072.816591,60.217786;1066.647748,68.650804;1057.570997,73.826036;1047.1718,74.839511;1037.266612,71.514201;1029.5856,64.430948;1025.470425,54.827003;1025.639897,44.379912;1030.064414,34.914497;1037.971133,28.084107;1047.978966,25.081825;1058.339819,26.432068;1067.243934,31.898985;1073.136004,40.527654",
            },
            new object[]
            {
                "The left quadrant of a circle of radius 20, dragged up 5",
                "C,1150,50,20,10",
                2,
                0.0,
                5.0,
                "1170,50;1167.551651,59.588511;1160.806046,66.82942;1151.414744,69.9499;1141.677063,68.185949;1133.977128,61.969443;1130.20015,52.8224;1131.270866,42.984335;1136.927128,34.86395;1145.784084,30.449398;1155.673244,30.821515;1164.173395,35.889193;1169.203406,44.41169",
                "1170.615528,50;1167.986167,60.074612;1160.768795,67.579336;1150.80446,70.599826;1140.634918,68.3656;1132.854274,61.446575;1129.447255,51.607694;1131.282944,41.358714;1137.893083,33.314001;1147.59152,29.525645;1157.904326,30.959999;1166.200853,37.251182",
            },
            new object[]
            {
                "The first corner of a counter-clockwise rectangle, dragged in 5 each way",
                "R,1200,0,60,40,10,1",
                0,
                5.0,
                5.0,
                "1200,40;1200,30;1200,20;1200,10;1200,0;1210,0;1220,0;1230,0;1240,0;1250,0;1260,0;1260,10;1260,20;1260,30;1260,40;1250,40;1240,40;1230,40;1220,40;1210,40",
                "1205,45;1203.785253,34.067279;1202.570506,23.134558;1201.35576,12.201837;1200.141013,1.269116;1209.723074,0;1220.723074,0;1231.723074,0;1242.723074,0;1253.723074,0;1260,4.723074;1260,15.723074;1260,26.723074;1260,37.723074;1251.31275,40.78975;1240.357924,41.785643;1229.403099,42.781536;1218.448274,43.77743",
            },
            new object[]
            {
                "The top middle of a rectangle 120 by 100 with arcs 7.3 long, dragged up 5",
                "R,1300,100,120,100,7.3,0",
                3,
                0.0,
                5.0,
                "1300,100;1300,107.3;1300,114.6;1300,121.9;1300,129.2;1300,136.5;1300,143.8;1300,151.1;1300,158.4;1300,165.7;1300,173;1300,180.3;1300,187.6;1300,194.9;1302.2,200;1309.5,200;1316.8,200;1324.1,200;1331.4,200;1338.7,200;1346,200;1353.3,200;1360.6,200;1367.9,200;1375.2,200;1382.5,200;1389.8,200;1397.1,200;1404.4,200;1411.7,200;1419,200;1420,193.7;1420,186.4;1420,179.1;1420,171.8;1420,164.5;1420,157.2;1420,149.9;1420,142.6;1420,135.3;1420,128;1420,120.7;1420,113.4;1420,106.1;1418.8,100;1411.5,100;1404.2,100;1396.9,100;1389.6,100;1382.3,100;1375,100;1367.7,100;1360.4,100;1353.1,100;1345.8,100;1338.5,100;1331.2,100;1323.9,100;1316.6,100;1309.3,100",
                "1300,100;1300,107.997918;1300,115.995836;1300,123.993753;1300,131.991671;1300,139.989589;1300,147.987507;1300,155.985425;1300,163.983342;1300,171.98126;1300,179.979178;1300,187.977096;1300,195.975014;1300,203.972931;1306.970849,205;1314.968767,205;1322.966685,205;1330.964602,205;1338.96252,205;1346.960438,205;1354.958356,205;1362.956274,205;1370.954191,205;1378.952109,205;1386.950027,205;1394.947945,205;1402.945863,205;1410.94378,205;1418.941698,205;1420,198.060384;1420,190.062466;1420,182.064548;1420,174.066631;1420,166.068713;1420,158.070795;1420,150.072877;1420,142.074959;1420,134.077042;1420,126.079124;1420,118.081206;1420,110.083288;1420,102.08537;1414.087453,100;1406.089535,100;1398.091617,100;1390.093699,100;1382.095782,100;1374.097864,100;1366.099946,100;1358.102028,100;1350.10411,100;1342.106193,100;1334.108275,100;1326.110357,100;1318.112439,100;1310.114521,100",
            },
            new object[]
            {
                "The top middle of a rectangle 60 by 40, dragged down 30",
                "R,0,200,60,40,10,0",
                3,
                0.0,
                -30.0,
                "0,200;0,210;0,220;0,230;0,240;10,240;20,240;30,240;40,240;50,240;60,240;60,230;60,220;60,210;60,200;50,200;40,200;30,200;20,200;10,200",
                "0,200;1,210;12,210;23,210;34,210;45,210;56,210;60,203;52,200;41,200;30,200;19,200;8,200",
            },
            new object[]
            {
                "The top middle of a rectangle 60 by 40, dragged down 38",
                "R,100,200,60,40,10,0",
                3,
                0.0,
                -38.0,
                "100,200;100,210;100,220;100,230;100,240;110,240;120,240;130,240;140,240;150,240;160,240;160,230;160,220;160,210;160,200;150,200;140,200;130,200;120,200;110,200",
                "100,200;109,202;120,202;131,202;142,202;153,202;158,200;147,200;136,200;125,200;114,200",
            },
            new object[]
            {
                "A polygon of five corners and a sixth vertex on a side, a grip dragged nowhere",
                "P,10;200,200;260,200;260,240;230,260;200,240;200,220",
                6,
                0.0,
                0.0,
                "200,200;210,200;220,200;230,200;240,200;250,200;260,200;260,210;260,220;260,230;260,240;251.679497,245.547002;243.358994,251.094004;235.038491,256.641006;226.717988,257.811992;218.397485,252.26499;210.076982,246.717988;201.756479,241.170986;200,232.111026;200,222.111026;200,212.111026",
                "200,200;210.986495,200;221.97299,200;232.959486,200;243.945981,200;254.932476,200;260,205.918971;260,216.905466;260,227.891961;260,238.878457;251.791864,245.472091;242.650547,251.566302;233.509231,257.660513;224.367914,256.245276;215.226598,250.151065;206.085281,244.056854;200,236.327103;200,225.340607;200,214.354112",
            },
        };

        [Theory]
        [MemberData(nameof(Dragged))]
        public void AGripOfACloudDragged_DrawsItAgainAlongTheNewShape_AsAutoCad2021(
            string name,
            string make,
            int grip,
            double dx,
            double dy,
            string before,
            string after
        )
        {
            Assert.NotEmpty(name);
            Make(make, out CadDocument document);
            DxfPolyline cloud = Cloud(document);
            SameVertices(before, cloud);
            string data = cloud.GetXDataForApplication("RevcloudProps")[2].Value;
            string handle = cloud.Handle;
            GeoPoint2 at = CadGrips.Shown(document, cloud)[grip].Point;

            CadEditResult result = document.GripDrag(handle, grip, new GeoPoint2(at.X + dx, at.Y + dy));

            Assert.True(result.Succeeded, result.Message);
            DxfPolyline drawn = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(handle, drawn.Handle);
            SameVertices(after, drawn);
            Assert.True(drawn.Closed);
            Assert.Equal(data, drawn.GetXDataForApplication("RevcloudProps")[2].Value);
        }

        [Fact]
        public void ACloudRoundARectangle_HasTheCornersAndTheMiddleOfEachSide_AsAutoCad2021()
        {
            // AutoCAD 2021, a cloud of 20 arcs 10 long: eight grips, not the 40 of its vertices and arcs.
            Make("R,0,0,60,40,10,0", out CadDocument document);

            IReadOnlyList<CadGrip> grips = CadGrips.Shown(document, Cloud(document));

            Assert.Equal(8, grips.Count);
            double[] expected = { 0, 0, 0, 20, 0, 40, 30, 40, 60, 40, 60, 20, 60, 0, 30, 0 };

            for (int i = 0; i < 8; i++)
            {
                Near(P(expected[2 * i], expected[2 * i + 1]), grips[i].Point, 1E-9);
                Assert.Equal(i % 2 == 0 ? CadGripKind.Vertex : CadGripKind.SegmentMiddle, grips[i].Kind);
            }
        }

        [Fact]
        public void ACloudRoundAPolygon_HasItsCorners_TheOneTheArcsCutOffToo_AsAutoCad2021()
        {
            // The corner (130, 60) is no vertex of the cloud; AutoCAD 2021 has its grip there.
            Make("P,10;100,0;160,0;160,40;130,60;100,40", out CadDocument document);

            IReadOnlyList<CadGrip> grips = CadGrips.Shown(document, Cloud(document));

            double[] expected =
            {
                100,
                0,
                130,
                0,
                160,
                0,
                160,
                20,
                160,
                40,
                145,
                50,
                130,
                60,
                115,
                50,
                100,
                40,
                100,
                20,
            };
            Assert.Equal(10, grips.Count);

            for (int i = 0; i < 10; i++)
            {
                Near(P(expected[2 * i], expected[2 * i + 1]), grips[i].Point, 1E-6);
            }
        }

        [Fact]
        public void ACloudRoundACircle_HasTheGripsOfTheCircle_AsAutoCad2021()
        {
            Make("C,250,20,20,10", out CadDocument document);

            IReadOnlyList<CadGrip> grips = CadGrips.Shown(document, Cloud(document));

            double[] expected = { 250, 20, 270, 20, 230, 20, 250, 40, 250, 0 };
            Assert.Equal(5, grips.Count);
            Assert.Equal(CadGripKind.Center, grips[0].Kind);

            for (int i = 0; i < 5; i++)
            {
                Near(P(expected[2 * i], expected[2 * i + 1]), grips[i].Point, 1E-6);
            }
        }

        [Fact]
        public void WithRevcloudgripsOff_ACloudHasAGripForEachVertexAndEachArc_AsAnyPolylineHas()
        {
            // AutoCAD 2021 with REVCLOUDGRIPS 0: 40 grips for the 20 vertices of the cloud round a rectangle, as for a polyline.
            CadCommandSession session = Make("R,0,0,60,40,10,0", out CadDocument document);
            Assert.True(document.RevCloudGrips);

            Run(session, "REVCLOUDGRIPS", "0");

            Assert.False(document.RevCloudGrips);
            IReadOnlyList<CadGrip> grips = CadGrips.Shown(document, Cloud(document));
            Assert.Equal(40, grips.Count);
            Assert.Equal(40, CadGrips.Of(Cloud(document)).Count);
            Near(P(0, 0), grips[0].Point, 1E-9);
            Near(P(0, 10), grips[1].Point, 1E-9);

            Run(session, "REVCLOUDGRIPS", "1");

            Assert.True(document.RevCloudGrips);
            Assert.Equal(8, CadGrips.Shown(document, Cloud(document)).Count);
        }

        [Fact]
        public void WithRevcloudgripsOff_AVertexOfACloudDragged_MovesThatVertexAlone()
        {
            CadCommandSession session = Make("R,0,0,60,40,10,0", out CadDocument document);
            Run(session, "REVCLOUDGRIPS", "0");

            CadEditResult result = document.GripDrag(Cloud(document).Handle, 1, P(10, 3));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(20, Cloud(document).NumberOfVertices);
            Near(P(10, 3), Cloud(document).GetPoint2dAt(1), 1E-9);
            Near(P(0, 30), Cloud(document).GetPoint2dAt(3), 1E-9);
        }

        [Fact]
        public void ACloudOfFewerThanSixVertices_AnOpenOne_AndAPlainPolyline_ShowTheirVertices()
        {
            // AutoCAD 2021: a closed cloud of 3, 4 or 5 vertices, an open one, and a polyline with no cloud data keep the grips
            // of a polyline, a vertex and an arc middle for each.
            CadCommandSession session = Arcs(out CadDocument document, 10.0);
            Run(session, "REVCLOUD", P(0, 0), P(10, 10));
            DxfPolyline small = Cloud(document);
            Assert.True(small.NumberOfVertices < 6);
            Assert.Equal(2 * small.NumberOfVertices, CadGrips.Shown(document, small).Count);

            Run(session, "LINE", P(100, 0), P(150, 0), Enter, "REVCLOUD", "O", P(125, 0), "N");
            DxfPolyline open = Cloud(document);
            Assert.False(open.Closed);
            Assert.Equal(2 * open.NumberOfVertices - 1, CadGrips.Shown(document, open).Count);

            Run(session, "PLINE", P(0, 100), P(60, 100), P(60, 140), P(0, 140), "C");
            DxfPolyline plain = Cloud(document);
            Assert.Null(plain.GetXDataForApplication("RevcloudProps"));
            Assert.Equal(8, CadGrips.Shown(document, plain).Count);
        }

        [Fact]
        public void TheMenuOfAGripOfACloudWithFewGrips_IsEmpty_AndTheVerticesOfOneWithAllHaveTheirs()
        {
            CadCommandSession session = Make("R,0,0,60,40,10,0", out CadDocument document);
            string handle = Cloud(document).Handle;

            Assert.Empty(CadGripMenus.Of(document, handle, 0));

            Run(session, "REVCLOUDGRIPS", "0");

            Assert.Equal(3, CadGripMenus.Of(document, handle, 0).Count);
        }

        [Fact]
        public void ACircleCloudsQuadrantDraggedToItsCentre_IsRefused_AndTheCloudStays()
        {
            Make("C,250,20,20,10", out CadDocument document);
            DxfPolyline cloud = Cloud(document);
            string handle = cloud.Handle;
            double x = cloud.GetPoint2dAt(1).X;

            CadEditResult result = document.GripDrag(handle, 1, P(250, 20));

            Assert.False(result.Succeeded);
            Assert.Equal("The revision cloud would be too small for its arcs.", result.Message);
            Assert.Equal(x, Cloud(document).GetPoint2dAt(1).X, 12);
        }

        [Fact]
        public void ADraggedCloud_IsOneUndoStep()
        {
            Make("R,0,0,60,40,10,0", out CadDocument document);

            Assert.True(document.GripDrag(Cloud(document).Handle, 3, P(30, 45)).Succeeded);
            Assert.Equal(19, Cloud(document).NumberOfVertices);

            Assert.True(document.Undo());

            Assert.Equal(20, Cloud(document).NumberOfVertices);
            Near(P(0, 10), Cloud(document).GetPoint2dAt(1), 1E-9);
        }
    }
}
