using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// REVCLOUD against AutoCAD 2021: the clouds of objects are compared vertex by vertex with those AutoCAD's own console made
    /// of the same objects at one arc length (accoreconsole, listed with entget); the prompts of Rectangular, Polygonal,
    /// Freehand and Modify, which the console does not offer, are AutoCAD 2021's own strings (AcRevCloudRes.dll).
    /// </summary>
    public class RevcloudTests
    {
        private const double Bulge = 0.52056705;

        private static DxfPolyline Cloud(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        private static void Vertices(DxfPolyline cloud, double bulge, params double[] xy)
        {
            Assert.Equal(xy.Length / 2, cloud.NumberOfVertices);

            for (int i = 0; i < cloud.NumberOfVertices; i++)
            {
                Near(P(xy[2 * i], xy[2 * i + 1]), cloud.GetPoint2dAt(i), 1E-7);
                Assert.Equal(bulge, cloud.GetBulgeAt(i), 8);
            }
        }

        [Fact]
        public void TheFirstPrompt_SaysTheArcLengthsStyleAndType_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUD");

            // At REVCLOUDARCVARIANCE 1 the arcs run from two thirds of the approximate length, 15 in a metric drawing, to four thirds.
            Assert.Equal(
                "Minimum arc length: 10.0000   Maximum arc length: 20.0000   Style: Normal   Type: Rectangular",
                session.History.Last()
            );
            Assert.Equal(
                "Specify first corner point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
            Run(session, "P");
            Assert.Equal(
                "Specify first point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
            Run(session, "F");
            Assert.Equal(
                "Specify start point or [Arc length/Object/Rectangular/Polygonal/Freehand/Style/Modify] <Object>:",
                session.PromptText
            );
            Assert.EndsWith("Type: Freehand", session.History.Last());
            Run(session, "S");
            Assert.Equal("Select arc style [Normal/Calligraphy] <Normal>:", session.PromptText);
            Run(session, "C");
            Assert.Contains("Calligraphy", session.History);
            Assert.EndsWith("Style: Calligraphy   Type: Freehand", session.History.Last());
        }

        [Fact]
        public void ArcLength_IsOneApproximateLength_KeptAsRevcloudApproxArcLen_AndNotScaledByDimscale()
        {
            // AutoCAD 2021: "Specify approximate length of arc", kept in REVCLOUDAPPROXARCLEN, nought until set; the minimum
            // two thirds of it and the maximum four thirds at REVCLOUDARCVARIANCE 1, both it at 0. 2021 has no
            // REVCLOUDSCALEMODE, which later releases added to scale it by DIMSCALE again.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.Database.Dimscale = 2.0;
            Run(session, "REVCLOUDAPPROXARCLEN");
            Assert.Equal("Enter new value for REVCLOUDAPPROXARCLEN <0.0000>:", session.PromptText);
            Run(session, Enter);

            Run(session, "REVCLOUD", "A");
            Assert.Equal("Specify approximate length of arc <15.0000>:", session.PromptText);
            Run(session, "0");
            Assert.Contains("Value must be positive and nonzero.", session.History);
            Run(session, "6");
            Assert.Equal(
                "Minimum arc length: 4.0000   Maximum arc length: 8.0000   Style: Normal   Type: Rectangular",
                session.History.Last()
            );
            Run(session, Esc);

            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUD");
            Assert.StartsWith("Minimum arc length: 6.0000   Maximum arc length: 6.0000", session.History.Last());
            Run(session, Esc, "REVCLOUDAPPROXARCLEN");
            Assert.Equal("Enter new value for REVCLOUDAPPROXARCLEN <6.0000>:", session.PromptText);
            Run(session, "9", "REVCLOUDCREATEMODE", "2", "REVCLOUD");
            Assert.Equal(
                "Minimum arc length: 9.0000   Maximum arc length: 9.0000   Style: Normal   Type: Polygonal",
                session.History.Last()
            );
        }

        [Fact]
        public void ACircle_BecomesACloudOfArcsFiveLongInItsPlace_ReversedArcsBulgingIn_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(0, 0), "20", "REVCLOUDARCVARIANCE", "0", "REVCLOUD", "A", "5", "O");
            Assert.Equal("Select object:", session.PromptText);
            Run(session, P(20, 0));
            Assert.Equal("Reverse direction [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");

            Assert.Contains("Revision cloud finished.", session.History);
            Assert.Empty(All<DxfCircle>(document.ModelSpace));
            DxfPolyline cloud = Cloud(document);
            Assert.True(cloud.Closed);
            Vertices(
                cloud,
                -Bulge,
                20,
                0,
                19.37824843,
                4.94807919,
                17.55165124,
                9.58851077,
                14.63377738,
                13.63277520,
                10.80604612,
                16.82941970,
                6.30644725,
                18.97969239,
                1.41474403,
                19.94989973,
                -3.56492111,
                19.67971894,
                -8.32293673,
                18.18594854,
                -12.56347245,
                15.56146394,
                -16.02287231,
                11.96944288,
                -18.48604757,
                7.63321984,
                -19.79984993,
                2.82240016,
                -19.88259352,
                -2.16390269,
                -18.72913375,
                -7.01566455,
                -16.41118715,
                -11.43122637,
                -13.07287242,
                -15.13604991,
                -8.92174980,
                -17.89978716,
                -4.21591599,
                -19.55060235,
                0.75204306,
                -19.98585578,
                5.67324371,
                -19.17848549,
                10.24170954,
                -17.17868987,
                14.17339549,
                -14.11080651,
                17.22384834,
                -10.16558155,
                19.20340573,
                -5.58830996
            );
        }

        [Fact]
        public void ACircleAtDimscaleTwo_IsCloudedWithArcsOfTheLengthGiven_AsAutoCadMadeThem()
        {
            // The console's REVCLOUD, at DIMSCALE 2 and 10 typed, drew these arcs of 10; 2021's own draws the length given too.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            document.Database.Dimscale = 2.0;
            Run(
                session,
                "CIRCLE",
                P(0, 0),
                "20",
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUD",
                "A",
                "10",
                "O",
                P(20, 0),
                Enter
            );

            Vertices(
                Cloud(document),
                Bulge,
                20,
                0,
                17.55165124,
                9.58851077,
                10.80604612,
                16.82941970,
                1.41474403,
                19.94989973,
                -8.32293673,
                18.18594854,
                -16.02287231,
                11.96944288,
                -19.79984993,
                2.82240016,
                -18.72913375,
                -7.01566455,
                -13.07287242,
                -15.13604991,
                -4.21591599,
                -19.55060235,
                5.67324371,
                -19.17848549,
                14.17339549,
                -14.11080651,
                19.20340573,
                -5.58830996
            );
        }

        [Fact]
        public void APolylineRunningEitherWay_IsCloudedAlongItsLength_TheArcsBulgingOut_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(
                session,
                "PLINE",
                P(0, 0),
                P(60, 0),
                P(60, 40),
                P(0, 40),
                "C",
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUD",
                "A",
                "7",
                "O",
                P(30, 0),
                Enter
            );

            // The corners are not kept: the vertices lie 7 apart along the 200 of the outline, the last arc 4 long.
            Vertices(
                Cloud(document),
                Bulge,
                0,
                0,
                7,
                0,
                14,
                0,
                21,
                0,
                28,
                0,
                35,
                0,
                42,
                0,
                49,
                0,
                56,
                0,
                60,
                3,
                60,
                10,
                60,
                17,
                60,
                24,
                60,
                31,
                60,
                38,
                55,
                40,
                48,
                40,
                41,
                40,
                34,
                40,
                27,
                40,
                20,
                40,
                13,
                40,
                6,
                40,
                0,
                39,
                0,
                32,
                0,
                25,
                0,
                18,
                0,
                11,
                0,
                4
            );

            Run(
                session,
                "ERASE",
                "ALL",
                Enter,
                "PLINE",
                P(0, 0),
                P(0, 40),
                P(60, 40),
                P(60, 0),
                "C",
                "REVCLOUD",
                "O",
                P(0, 20),
                Enter
            );
            Vertices(
                Cloud(document),
                -Bulge,
                0,
                0,
                0,
                7,
                0,
                14,
                0,
                21,
                0,
                28,
                0,
                35,
                2,
                40,
                9,
                40,
                16,
                40,
                23,
                40,
                30,
                40,
                37,
                40,
                44,
                40,
                51,
                40,
                58,
                40,
                60,
                35,
                60,
                28,
                60,
                21,
                60,
                14,
                60,
                7,
                60,
                0,
                53,
                0,
                46,
                0,
                39,
                0,
                32,
                0,
                25,
                0,
                18,
                0,
                11,
                0,
                4,
                0
            );
        }

        [Fact]
        public void ALastArcShorterThanHalfAnArc_IsTakenIntoTheOneBefore_AsAutoCad2021()
        {
            // AutoCAD 2021 on the outline of 200 at arc lengths 9.3 and 9.7585: a last arc of 4.7 (0.505 of 9.3) is kept, one of
            // 4.83 (0.495 of 9.7585) is not; 22 and 20 vertices.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(
                session,
                "PLINE",
                P(0, 0),
                P(60, 0),
                P(60, 40),
                P(0, 40),
                "C",
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUD",
                "A",
                "9.3",
                "O",
                P(30, 0),
                Enter
            );
            Assert.Equal(22, Cloud(document).NumberOfVertices);
            Near(P(0, 4.7), Cloud(document).GetPoint2dAt(21), 1E-9);

            Run(
                session,
                "PLINE",
                P(0, 0),
                P(60, 0),
                P(60, 40),
                P(0, 40),
                "C",
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUD",
                "A",
                "9.7585",
                "O",
                P(30, 0),
                Enter
            );
            Assert.Equal(20, Cloud(document).NumberOfVertices);
            Near(P(0, 14.5885), Cloud(document).GetPoint2dAt(19), 1E-9);
        }

        [Fact]
        public void ALineAndAnArc_GiveOpenClouds_EndingAtTheirEnds_AsAutoCad2021()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(
                session,
                "LINE",
                P(0, 0),
                P(3, 0),
                Enter,
                "REVCLOUDARCVARIANCE",
                "0",
                "REVCLOUD",
                "A",
                "0.5",
                "O",
                P(1.2, 0),
                Enter
            );

            DxfPolyline cloud = Cloud(document);
            Assert.False(cloud.Closed);
            Vertices(cloud, -Bulge, 0, 0, 0.5, 0, 1, 0, 1.5, 0, 2, 0, 2.5, 0, 3, 0);

            // The half circle from (2, 0) round to (0, 0), 3.14 long: 6 vertices half apart and its end, 7 in all.
            Run(session, "ARC", P(0, 10), P(1, 11), P(2, 10), "REVCLOUD", "O", P(1, 11), Enter);
            cloud = Cloud(document);
            Assert.Equal(7, cloud.NumberOfVertices);
            Near(P(0, 10), cloud.GetPoint2dAt(6), 1E-9);
        }

        [Fact]
        public void Calligraphy_WidensEachArcFromNoughtToATenthOfTheLongest()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUD", "A", "9", "S", "C", P(0, 0), P(60, 40));

            DxfPolyline cloud = Cloud(document);
            Assert.True(cloud.Closed);
            Assert.All(Enumerable.Range(0, cloud.NumberOfVertices), i =>
            {
                Assert.Equal(0.0, cloud.GetStartWidthAt(i));
                Assert.Equal(1.2, cloud.GetEndWidthAt(i), 12);
            });

            // Every arc between two thirds of 9 and four thirds, 6 and 12, along the 200 of the rectangle.
            List<GeoPoint2> points = Enumerable.Range(0, cloud.NumberOfVertices).Select(cloud.GetPoint2dAt).ToList();
            Assert.InRange(points.Count, 200 / 12, 200 / 6);
        }

        [Fact]
        public void RectangularAndPolygonal_CloudTheirOutlines_ArcsBulgingOut()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUD", "A", "10", P(0, 0));
            Assert.Equal("Specify opposite corner:", session.PromptText);
            Run(session, P(100, -50));
            DxfPolyline rectangle = Cloud(document);
            Assert.Equal(30, rectangle.NumberOfVertices);
            Near(P(0, 0), rectangle.GetPoint2dAt(0));
            Near(P(10, 0), rectangle.GetPoint2dAt(1));

            // Drawn clockwise: the bulges turn over so that the arcs still bulge out.
            Assert.Equal(-Bulge, rectangle.GetBulgeAt(0), 8);

            Run(session, "REVCLOUD", "P", P(0, 100));
            Assert.Equal("Specify next point:", session.PromptText);
            Run(session, P(50, 100));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(70, 70), "U", P(50, 150), Enter);
            DxfPolyline triangle = Cloud(document);
            Assert.True(triangle.Closed);
            Assert.Equal(Bulge, triangle.GetBulgeAt(0), 8);
            Assert.Equal(17, triangle.NumberOfVertices);

            Run(session, "REVCLOUD", "P", P(0, 300), P(50, 300), Enter);
            Assert.Contains("Invalid shape to create revision cloud.", session.History);
        }

        [Fact]
        public void Freehand_FollowsTheCursor_AndClosesBackAtTheStart()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUD", "A", "10", "F", P(0, 0));
            Assert.Contains("Guide crosshairs along cloud path...", session.History);
            Assert.True(session.Prompt.Tracks);
            int written = session.History.Count;

            for (int i = 1; i <= 100; i++)
            {
                double angle = i * 2.0 * System.Math.PI / 100.0;
                session.Feed(P(50 - 50 * System.Math.Cos(angle), 50 * System.Math.Sin(angle)));

                if (!session.IsRunning)
                {
                    break;
                }
            }

            Assert.False(session.IsRunning);
            Assert.Equal(written + 1, session.History.Count);
            Assert.Equal("Revision cloud finished.", session.History.Last());
            DxfPolyline cloud = Cloud(document);
            Assert.True(cloud.Closed);
            Assert.InRange(cloud.NumberOfVertices, 20, 32);
        }

        [Fact]
        public void Modify_RedrawsTheSidePickedAlongANewPath_OneClosedCloudAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUD", "A", "10", P(0, 0), P(100, 100));
            string handle = Cloud(document).Handle;

            Run(session, "REVCLOUD", "M");
            Assert.Equal("Select polyline to modify:", session.PromptText);
            Run(session, P(30, 0));
            Assert.Equal("Specify next point or [First point]:", session.PromptText);
            Run(session, P(30, -50));
            Assert.Equal("Specify next point or [Undo]:", session.PromptText);
            Run(session, P(70, -50), P(70, 0));
            Assert.Equal("Pick a side to erase:", session.PromptText);
            Run(session, P(50, 0));

            Assert.Contains("Revision cloud finished.", session.History);
            DxfPolyline cloud = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(handle, cloud.Handle);
            Assert.True(cloud.Closed);
            List<GeoPoint2> points = Enumerable.Range(0, cloud.NumberOfVertices).Select(cloud.GetPoint2dAt).ToList();
            Assert.Contains(points, p => p.Y < -40);
            Assert.DoesNotContain(points, p => p.Y > -1 && p.Y < 1 && p.X > 32 && p.X < 68);
        }

        [Fact]
        public void ADrawingMadeWithEveryOptionOfPeditAndRevcloud_AuditsClean_AndReopens()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "PLINE", P(0, 0), P(100, 0), P(100, 50), P(0, 80), Enter);
            Run(session, "PEDIT", P(50, 0), "C", "O", "W", "1", "L", "ON", "R", "F", "D", "S", "U", "U", "X");
            Run(session, "PLINE", P(0, 200), P(100, 200), P(100, 250), P(0, 280), Enter);
            Run(
                session,
                "PEDIT",
                P(50, 200),
                "E",
                "N",
                "W",
                "1",
                "2",
                "N",
                "I",
                P(120, 260),
                "M",
                P(130, 260),
                "T",
                "45",
                "P",
                "S",
                "N",
                "G",
                "B",
                "N",
                "G",
                "X",
                "S",
                "X"
            );
            Run(session, "PLINE", P(0, 400), P(100, 400), P(100, 450), P(0, 480), Enter);
            Run(session, "PEDIT", P(50, 400), "F", "E", "N", "M", P(120, 410), "X", "X");
            Run(
                session,
                "LINE",
                P(300, 0),
                P(400, 0),
                Enter,
                "LINE",
                P(403, 0),
                P(403, 50),
                Enter,
                "ARC",
                P(500, 0),
                P(550, 50),
                P(600, 0)
            );
            Run(
                session,
                "PEDIT",
                "M",
                P(350, 0),
                P(403, 25),
                P(550, 50),
                Enter,
                Enter,
                "J",
                "J",
                "B",
                "5",
                "F",
                "S",
                "D",
                "C",
                "O",
                "L",
                "ON",
                "R",
                "W",
                "0.5",
                "U",
                "X"
            );
            Run(session, "REVCLOUDARCVARIANCE", "0", "CIRCLE", P(700, 0), "20", "REVCLOUD", "O", P(720, 0), "Y");
            Run(session, "REVCLOUD", "S", "C", P(800, 0), P(900, 100));
            Run(session, "REVCLOUD", "P", P(800, 200), P(900, 200), P(850, 300), Enter);
            Run(session, "REVCLOUD", "S", "N", "M", P(845, 0), P(845, -40), P(875, -40), P(875, 0), P(860, 1));

            Assert.False(session.IsRunning);
            var bytes = new MemoryStream();
            document.Database.Save(bytes);
            bytes.Position = 0;
            DxfDatabase read = DxfDatabase.Open(bytes);
            Assert.Equal(0, read.Audit(false).Count);
            AssertReopens(document);
            Assert.NotEmpty(All<DxfPolyline2d>(document.ModelSpace));
        }
    }
}
