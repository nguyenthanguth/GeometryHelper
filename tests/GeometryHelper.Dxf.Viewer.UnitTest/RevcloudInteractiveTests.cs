using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// REVCLOUD and REVCLOUDPROPERTIES against AutoCAD 2021's console run interactively (14 November: the console reads its
    /// commands from standard input, and then runs the 2021 REVCLOUD with Rectangular, Polygonal, Freehand, Modify and the data
    /// of the clouds it makes; a script file made it run the older one). The vertices of each case are AutoCAD's, at
    /// REVCLOUDARCVARIANCE 0 where the arcs are of one length.
    /// </summary>
    public class RevcloudInteractiveTests
    {
        private const double Bulge = 0.52056705;

        private static DxfPolyline Cloud(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        /// <summary>A cloud of the vertices given, each with a bulge, to 7 places for the vertices and 8 for the bulges.</summary>
        private static void Ring(DxfPolyline cloud, double[] xy, double[] bulges)
        {
            Assert.Equal(xy.Length / 2, cloud.NumberOfVertices);

            for (int i = 0; i < cloud.NumberOfVertices; i++)
            {
                Near(P(xy[2 * i], xy[2 * i + 1]), cloud.GetPoint2dAt(i), 1E-7);
                Assert.Equal(bulges[i % bulges.Length], cloud.GetBulgeAt(i), 8);
            }
        }

        private static CadCommandSession Variance0(CadDocument document, double arc = 10.0)
        {
            CadCommandSession session = Session(document);
            session.PickAperture = 3.0;
            Run(
                session,
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUDAPPROXARCLEN",
                arc.ToString(CultureInfo.InvariantCulture)
            );
            return session;
        }

        [Fact]
        public void TheLengthsLine_IsSaidWhenTheCommandStartsAndWhenTheTypeChanges_NotAfterArcLengthOrStyle()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);

            Run(session, "REVCLOUD");
            Assert.Equal(1, session.History.Count(l => l.StartsWith("Minimum arc length")));
            Run(session, "A", "12");
            Run(session, "S", "C");
            Assert.Equal(1, session.History.Count(l => l.StartsWith("Minimum arc length")));

            Run(session, "P");
            Assert.Equal(2, session.History.Count(l => l.StartsWith("Minimum arc length")));
            Assert.Equal(
                "Minimum arc length: 12.0000   Maximum arc length: 12.0000   Style: Calligraphy   Type: Polygonal",
                session.History.Last()
            );
            Run(session, "P");
            Assert.Equal(2, session.History.Count(l => l.StartsWith("Minimum arc length")));
            Run(session, "F");
            Assert.Equal(3, session.History.Count(l => l.StartsWith("Minimum arc length")));
        }

        [Fact]
        public void RevcloudArcVariance_IsAnOnOffSwitch_AsAutoCad2021AsksIt()
        {
            // AutoCAD's SETVAR: "Enter new value for REVCLOUDARCVARIANCE <ON>:", 2 refused with "Requires 0 or 1 only, or option
            // keyword."; OFF is 0, which makes every arc the approximate length.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUDARCVARIANCE");
            Assert.Equal("Enter new value for REVCLOUDARCVARIANCE <ON>:", session.PromptText);
            Run(session, "2");
            Assert.Contains("Requires 0 or 1 only, or option keyword.", session.History);
            Run(session, "OFF", "REVCLOUDARCVARIANCE");
            Assert.Equal("Enter new value for REVCLOUDARCVARIANCE <OFF>:", session.PromptText);
            Run(session, Esc, "REVCLOUDAPPROXARCLEN", "9", "REVCLOUD");
            Assert.StartsWith("Minimum arc length: 9.0000   Maximum arc length: 9.0000", session.History.Last());
        }

        [Fact]
        public void ThePromptOfPolygonalSaysStartPoint_AndFreehandFirstPoint_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);

            Run(session, "REVCLOUD");
            Assert.Equal(
                "Specify first corner point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
            Run(session, "P");
            Assert.Equal(
                "Specify start point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
            Run(session, "F");
            Assert.Equal(
                "Specify first point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
        }

        [Fact]
        public void TheFirstCloudOfADrawing_IsSizedByATwentiethOfTheViewsDiagonal_AsAutoCad2021SizesIt()
        {
            // AutoCAD's console on a view 100 high, 942 by 534 pixels: lengths 6.7518 and 13.5036, a twentieth of the diagonal
            // 202.777 being 10.1389 (two thirds 6.7592, four thirds 13.5185; its own is a tenth of a per cent less, the
            // pixels its window leaves out). The arc length of the view's drawing does not follow DIMSCALE or the units.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            var view = new CadView(942, 534);
            view.ZoomToHeight(0, 0, 100);
            session.View = view;

            Run(session, "REVCLOUD");
            Assert.StartsWith("Minimum arc length: 6.7592   Maximum arc length: 13.5185", session.History.Last());
            Run(session, "A");
            Assert.Equal("Specify approximate length of arc <10.1389>:", session.PromptText);
            Run(session, Esc);

            // A view ten times the height gives ten times the length; set, REVCLOUDAPPROXARCLEN is kept instead.
            view.ZoomToHeight(0, 0, 1000);
            Run(session, "REVCLOUD");
            Assert.StartsWith("Minimum arc length: 67.5924   Maximum arc length: 135.1847", session.History.Last());
            Run(session, Esc, "REVCLOUDAPPROXARCLEN", "5", "REVCLOUD");
            Assert.StartsWith("Minimum arc length: 3.3333   Maximum arc length: 6.6667", session.History.Last());
        }

        [Fact]
        public void Rectangular_RunsFromTheFirstCornerAlongTheSideOfItsX_AsAutoCadDrawsIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);

            // (0, 0) and (100, 60): up the left side, along the top and down the right, clockwise.
            Run(session, "REVCLOUD", P(0, 0), P(100, 60));
            DxfPolyline cloud = Cloud(document);
            Assert.True(cloud.Closed);
            Assert.Equal(32, cloud.NumberOfVertices);
            Near(P(0, 10), cloud.GetPoint2dAt(1));
            Near(P(0, 60), cloud.GetPoint2dAt(6));
            Near(P(10, 60), cloud.GetPoint2dAt(7));
            Near(P(100, 60), cloud.GetPoint2dAt(16));
            Near(P(100, 0), cloud.GetPoint2dAt(22));
            Near(P(10, 0), cloud.GetPoint2dAt(31));
            Assert.Equal(-Bulge, cloud.GetBulgeAt(0), 8);

            // (100, 60) and (0, 0): down the right side, along the bottom, up the left, clockwise.
            Run(session, "REVCLOUD", P(100, 60), P(0, 0));
            cloud = Cloud(document);
            Near(P(100, 50), cloud.GetPoint2dAt(1));
            Near(P(0, 0), cloud.GetPoint2dAt(16));
            Near(P(90, 60), cloud.GetPoint2dAt(31));
            Assert.Equal(-Bulge, cloud.GetBulgeAt(0), 8);

            // (0, 60) and (100, 0): down the left side, counter-clockwise, the arcs bulging out the other way round.
            Run(session, "REVCLOUD", P(0, 60), P(100, 0));
            cloud = Cloud(document);
            Near(P(0, 50), cloud.GetPoint2dAt(1));
            Near(P(0, 0), cloud.GetPoint2dAt(6));
            Near(P(100, 0), cloud.GetPoint2dAt(16));
            Assert.Equal(Bulge, cloud.GetBulgeAt(0), 8);
        }

        [Fact]
        public void RectangularAndPolygonal_EndWithNoMessage_ObjectAndFreehandSayTheCloudIsFinished()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);

            Run(session, "REVCLOUD", P(0, 0), P(100, 60));
            Assert.DoesNotContain("Revision cloud finished.", session.History);
            Run(session, "REVCLOUD", "P", P(0, 100), P(50, 100), P(50, 150), Enter);
            Assert.DoesNotContain("Revision cloud finished.", session.History);

            Run(session, "CIRCLE", P(300, 0), "20", "REVCLOUD", "O", P(320, 0), "N");
            Assert.Equal(1, session.History.Count(l => l == "Revision cloud finished."));
        }

        [Fact]
        public void EveryCloud_CarriesTheRevcloudPropsDataOfItsApproximateArcLength_AsAutoCadMarksIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document, 12.0);

            Run(session, "REVCLOUD", P(0, 0), P(100, 60));
            Run(session, "REVCLOUD", "P", P(0, 100), P(50, 100), P(50, 150), Enter);
            Run(session, "CIRCLE", P(300, 0), "20", "REVCLOUD", "O", P(320, 0), "N");

            foreach (DxfPolyline cloud in All<DxfPolyline>(document.ModelSpace))
            {
                IReadOnlyList<DxfTypedValue> data = cloud.GetXDataForApplication("RevcloudProps");
                Assert.NotNull(data);
                Assert.Equal(3, data.Count);
                Assert.Equal(1070, data[1].TypeCode);
                Assert.Equal("0", data[1].Value);
                Assert.Equal(1040, data[2].TypeCode);
                Assert.Equal(12.0, double.Parse(data[2].Value, CultureInfo.InvariantCulture), 9);
            }
        }

        private static CadCommandSession WithCloud(out CadDocument document, out DxfPolyline cloud)
        {
            document = Blank();
            CadCommandSession session = Variance0(document);
            Run(session, "REVCLOUD", P(0, 0), P(100, 60));
            cloud = Cloud(document);
            return session;
        }

        [Fact]
        public void Modify_AsksWhichWayTheArcsBulge_AndLeavesNoMessage()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -20), P(80, -20), P(80, 0), P(65, 0));
            Assert.Equal("Reverse direction [Yes/No] <No>:", session.PromptText);
            Run(session, "N");
            Assert.False(session.IsRunning);
            Assert.DoesNotContain("Revision cloud finished.", session.History);
            Assert.Equal(36, Assert.Single(All<DxfPolyline>(document.ModelSpace)).NumberOfVertices);
        }

        [Fact]
        public void Modify_StartsTheCloudAtTheEndOfThePath_ReversesTheNewArcs_AndSpacesThemFromTheStart_AsAutoCad()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            // The path (50, 0), (50, -23), (80, -23), (80, 0) is 76 long: arcs 10 from its start, the last 6; the bottom between
            // 50 and 80, where (65, 0) is picked, goes. The cloud begins at the end of the path, goes back along it, and
            // goes on from the start round the way the old cloud ran.
            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -23), P(80, -23), P(80, 0), P(65, 0), "N");

            Ring(
                Assert.Single(All<DxfPolyline>(document.ModelSpace)),
                new double[]
                {
                    80,
                    0,
                    80,
                    -6,
                    80,
                    -16,
                    77,
                    -23,
                    67,
                    -23,
                    57,
                    -23,
                    50,
                    -20,
                    50,
                    -10,
                    50,
                    0,
                    40,
                    0,
                    30,
                    0,
                    20,
                    0,
                    10,
                    0,
                    0,
                    0,
                    0,
                    10,
                    0,
                    20,
                    0,
                    30,
                    0,
                    40,
                    0,
                    50,
                    0,
                    60,
                    10,
                    60,
                    20,
                    60,
                    30,
                    60,
                    40,
                    60,
                    50,
                    60,
                    60,
                    60,
                    70,
                    60,
                    80,
                    60,
                    90,
                    60,
                    100,
                    60,
                    100,
                    50,
                    100,
                    40,
                    100,
                    30,
                    100,
                    20,
                    100,
                    10,
                    100,
                    0,
                    90,
                    0,
                },
                new[] { -Bulge }
            );
        }

        [Fact]
        public void Modify_PickingTheOtherSide_KeepsTheBottomAndTheNewPathOnly_AsAutoCad()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -23), P(80, -23), P(80, 0), P(20, 0), "N");

            // The bulges stay those of the old cloud, clockwise, though the small cloud now runs counter-clockwise.
            Ring(
                Assert.Single(All<DxfPolyline>(document.ModelSpace)),
                new double[]
                {
                    80,
                    0,
                    70,
                    0,
                    60,
                    0,
                    50,
                    0,
                    50,
                    -10,
                    50,
                    -20,
                    57,
                    -23,
                    67,
                    -23,
                    77,
                    -23,
                    80,
                    -16,
                    80,
                    -6,
                },
                new[] { -Bulge }
            );
        }

        [Fact]
        public void Modify_AnsweringYesToReverse_TurnsEveryArcOver()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -23), P(80, -23), P(80, 0), P(65, 0), "Y");

            DxfPolyline made = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(37, made.NumberOfVertices);
            Assert.All(Enumerable.Range(0, made.NumberOfVertices), i => Assert.Equal(Bulge, made.GetBulgeAt(i), 8));
        }

        [Fact]
        public void Modify_FromAPointBetweenVertices_CutsTheArcsAtTheEnds_ToTheShapeTheyKeep()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            // Picked at (45, 0) the path starts on the arc there, at (45, -2.6028), and a point near the cloud ends it on the
            // arc at (78.0277, -1.799); the arcs cut there keep 0.2447 and 0.1107 of their bulge.
            Run(session, "REVCLOUD", "M", P(45, 0), P(45, -20), P(77, -20), P(77, 0), P(60, 0), "N");

            DxfPolyline made = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(37, made.NumberOfVertices);
            Near(P(78.027692318, -1.798994773), made.GetPoint2dAt(0), 1E-6);
            Near(P(77.597721059, -9.414026031), made.GetPoint2dAt(1), 1E-6);
            Near(P(77.033984054, -19.398123416), made.GetPoint2dAt(2), 1E-6);
            Near(P(67.60283525, -20), made.GetPoint2dAt(3), 1E-6);
            Near(P(45, -2.60283525), made.GetPoint2dAt(7), 1E-6);
            Near(P(80, 0), made.GetPoint2dAt(36), 1E-6);
            Assert.Equal(-Bulge, made.GetBulgeAt(0), 8);
            Assert.Equal(-0.24469844, made.GetBulgeAt(7), 7);
            Assert.Equal(-Bulge, made.GetBulgeAt(8), 8);
            Assert.Equal(-0.11067678, made.GetBulgeAt(36), 7);
        }

        [Fact]
        public void Modify_FromAPointBetweenVertices_KeepingTheOtherWay_StartsWithTheCutArc()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            Run(session, "REVCLOUD", "M", P(45, 0), P(45, -20), P(77, -20), P(77, 0), P(20, 0), "N");

            DxfPolyline made = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(11, made.NumberOfVertices);
            Near(P(78.027692318, -1.798994773), made.GetPoint2dAt(0), 1E-6);
            Near(P(70, 0), made.GetPoint2dAt(1), 1E-6);
            Near(P(45, -2.60283525), made.GetPoint2dAt(4), 1E-6);
            Near(P(77.597721059, -9.414026031), made.GetPoint2dAt(10), 1E-6);
            Assert.Equal(-0.38756106, made.GetBulgeAt(0), 7);
            Assert.Equal(-0.24469844, made.GetBulgeAt(3), 7);
            Assert.Equal(-Bulge, made.GetBulgeAt(4), 8);
        }

        [Fact]
        public void Modify_EnterEndsThePathWithNothingChanged()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);

            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -23), P(80, -23), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(32, Assert.Single(All<DxfPolyline>(document.ModelSpace)).NumberOfVertices);
        }

        [Fact]
        public void RevcloudProperties_AsksTheCloudsThenTheProperty_AndTheArcLengthTheFirstCloudWasDrawnWith()
        {
            CadCommandSession session = WithCloud(out CadDocument document, out DxfPolyline cloud);
            Run(session, "REVCLOUDAPPROXARCLEN", "7");

            Run(session, "REVCLOUDPROPERTIES");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { cloud.Handle }, Enter);
            Assert.Equal("Select property to change [Arc length]:", session.PromptText);
            Run(session, "A");
            Assert.Equal("Specify approximate length of arc <10.0000>:", session.PromptText);
            Run(session, "20");

            Assert.False(session.IsRunning);
            Assert.Equal(16, cloud.NumberOfVertices);
            Near(P(0, 20), cloud.GetPoint2dAt(1));
            Near(P(100, 60), cloud.GetPoint2dAt(8));
            Assert.Equal(-Bulge, cloud.GetBulgeAt(0), 8);
            Run(session, "REVCLOUDAPPROXARCLEN");
            Assert.Equal("Enter new value for REVCLOUDAPPROXARCLEN <20.0000>:", session.PromptText);
            Run(session, Esc);
            Assert.Equal(
                20.0,
                double.Parse(cloud.GetXDataForApplication("RevcloudProps")[2].Value, CultureInfo.InvariantCulture)
            );
        }

        [Fact]
        public void RevcloudProperties_DrawsAPolygonCloudAgainAlongTheChordsOfItsArcs_AsAutoCad()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);
            Run(session, "REVCLOUD", "P", P(0, 0), P(73, 0), P(80, 41), P(20, 52), Enter);
            DxfPolyline cloud = Cloud(document);
            Assert.Equal(23, cloud.NumberOfVertices);

            Run(session, "REVCLOUDPROPERTIES", new[] { cloud.Handle }, Enter, "A", "15");

            Ring(
                cloud,
                new[]
                {
                    0,
                    0,
                    15,
                    0,
                    30,
                    0,
                    45,
                    0,
                    60,
                    0,
                    72.589768985,
                    4.277042975,
                    76.186440776,
                    18.663438829,
                    78.710887719,
                    33.449485211,
                    69.964102802,
                    42.839914486,
                    55.210004442,
                    45.544832519,
                    40.455906081,
                    48.249750552,
                    25.70180772,
                    50.954668585,
                    15.882773591,
                    41.295211336,
                    10.498087401,
                    27.295027243,
                    5.113401211,
                    13.29484315,
                },
                new[] { Bulge }
            );
        }

        [Fact]
        public void RevcloudProperties_SkipsWhatIsNoCloud_AndEndsWhereNoneIsLeft()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);
            Run(session, "CIRCLE", P(0, 0), "5", "PLINE", P(0, 100), P(100, 100), P(100, 150), "C");
            string[] plain = All<DxfPolyline>(document.ModelSpace).Select(p => p.Handle).ToArray();
            DxfCircle circle = All<DxfCircle>(document.ModelSpace).Single();

            Run(session, "REVCLOUDPROPERTIES", plain.Concat(new[] { circle.Handle }).ToArray(), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(3, Assert.Single(All<DxfPolyline>(document.ModelSpace)).NumberOfVertices);
        }

        [Fact]
        public void ACloudMadeByAnEarlierVersionOfTheCommand_WithNoData_IsNotACloudToRevcloudProperties()
        {
            CadDocument document = Blank();
            CadCommandSession session = Variance0(document);
            Run(session, "REVCLOUD", P(0, 0), P(100, 60));
            DxfPolyline cloud = Cloud(document);
            cloud.XData = new[] { new DxfTypedValue(1001, "RevcloudProps") };

            Run(session, "REVCLOUDPROPERTIES", new[] { cloud.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(32, cloud.NumberOfVertices);
        }
    }
}
