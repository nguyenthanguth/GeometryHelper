using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.CenterMarkTests;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// CENTERLINE as AutoCAD 2021's console ran it (accoreconsole fed through standard input): the line it draws between two
    /// lines or segments of a polyline, where it stands and how long it is, the objects written with it, and how it follows.
    /// </summary>
    public class CenterLineTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>A drawing with two lines, from their ends, the first line picked at its middle and the second at its.</summary>
        private static CadDocument TwoLines(
            double a0x,
            double a0y,
            double a1x,
            double a1y,
            double b0x,
            double b0y,
            double b1x,
            double b1y
        )
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(a0x, a0y), P(a1x, a1y), Enter, "LINE", P(b0x, b0y), P(b1x, b1y), Enter);
            return document;
        }

        private static DxfBlockReference TheInsert(CadDocument document) =>
            All<DxfBlockReference>(document.ModelSpace).Single();

        private static DxfLine TheLine(DxfBlockReference insert) => insert.BlockTableRecord.OfType<DxfLine>().Single();

        private static GeoPoint2 Middle(double x0, double y0, double x1, double y1) =>
            P((x0 + x1) / 2.0, (y0 + y1) / 2.0);

        private static void IsCentreLine(
            CadDocument document,
            double x,
            double y,
            double x0,
            double y0,
            double x1,
            double y1,
            double tolerance
        )
        {
            DxfBlockReference insert = TheInsert(document);
            Near(new GeoPoint3(x, y, 0), insert.Position, tolerance);
            DxfLine line = TheLine(insert);
            Near(new GeoPoint3(x0, y0, 0), line.StartPoint, tolerance);
            Near(new GeoPoint3(x1, y1, 0), line.EndPoint, tolerance);
            Assert.StartsWith("*U", insert.Name, StringComparison.Ordinal);
            Assert.Equal("CENTER2", insert.Linetype, ignoreCase: true);
            Assert.Equal("CENTER2", line.Linetype, ignoreCase: true);
            Assert.True(line.Color.IsByBlock);
            Assert.True(line.Transparency.IsByBlock);
        }

        [Fact]
        public void TwoParallelLinesOverTheSameRun_HaveACentreLineBetweenThem_LongerByTheOvershoot()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            CadCommandSession session = Start(document, "CENTERLINE");

            Assert.Equal("Select first line:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Equal("Select second line:", session.PromptText);
            Run(session, P(50, 40));
            Assert.False(session.IsRunning);

            // AutoCAD 2021: the insert at the middle, a line of 100 and 0.12 over at each end.
            IsCentreLine(document, 50, 20, -50.12, 0, 50.12, 0, 1E-9);
            Assert.Equal("CENTERLINE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.DoesNotContain(All<DxfBlockReference>(document.ModelSpace), i =>
                i.Name.StartsWith("*U", StringComparison.Ordinal)
            );
        }

        [Fact]
        public void TwoParallelLinesOfOtherRuns_GiveTheAverageOfTheirStartsAndTheirEnds()
        {
            // AutoCAD 2021: a line from 0 to 100 and one from 30 to 150: the line from 15 to 125, about (70, 20).
            CadDocument document = TwoLines(0, 0, 100, 0, 30, 40, 150, 40);
            Start(document, "CENTERLINE", P(50, 0), P(100, 40));
            IsCentreLine(document, 70, 20, -55.12, 0, 55.12, 0, 1E-9);

            // One run inside the other: from 0 to 50 and -30 to 80 give 80 long about (25, 315).
            document = TwoLines(0, 300, 50, 300, -30, 330, 80, 330);
            Start(document, "CENTERLINE", P(20, 300), P(20, 330));
            IsCentreLine(document, 25, 315, -40.12, 0, 40.12, 0, 1E-9);
        }

        [Fact]
        public void TwoLinesTurnedFortyFiveDegrees_GiveALineAlongTheirDirection()
        {
            CadDocument document = TwoLines(200, 0, 300, 100, 220, -20, 320, 80);
            Start(document, "CENTERLINE", P(250, 50), P(270, 30));
            IsCentreLine(document, 260, 40, -50.0849, -50.0849, 50.0849, 50.0849, 1E-4);
        }

        [Fact]
        public void TwoLinesThatMeet_GiveTheBisector_FromTheEndsOfTheirStartsToTheirEnds()
        {
            // AutoCAD 2021: a line along 0 degrees and one rising 20 in 100, the bisector at 5.65 degrees through where they meet.
            CadDocument document = TwoLines(0, 100, 100, 100, 0, 140, 100, 160);
            Start(document, "CENTERLINE", P(50, 100), P(50, 150));
            IsCentreLine(document, 50, 124.755, -50.1194, -4.9628, 50.1194, 4.9628, 1E-3);
        }

        [Fact]
        public void TwoLinesThatCrossWithinTheirRun_GiveALineThatStopsAtTheCrossing()
        {
            // AutoCAD 2021: 20 and 40 degrees from the lower left: the line along 30 degrees ends where they cross and is drawn from there.
            CadDocument document = TwoLines(
                0,
                -20,
                200,
                -20 + 200 * 0.36397,
                50,
                -60,
                50 + 120 * 0.766044,
                -60 + 120 * 0.642788
            );
            Start(document, "CENTERLINE", P(20, -20 + 20 * 0.36397), P(60, -60 + 10 * 0.8391));
            IsCentreLine(document, 99.6059, 0.7016, 72.9875, 42.1394, -72.9875, -42.1394, 2E-3);
        }

        [Fact]
        public void TheSegmentsOfAPolyline_AreLinesToo()
        {
            CadDocument document = Blank();
            Start(document, "PLINE", P(0, 300), P(100, 300), P(100, 350), P(0, 350), "C");
            Start(document, "CENTERLINE", P(50, 300), P(50, 350));

            IsCentreLine(document, 50, 325, -50.12, 0, 50.12, 0, 1E-9);

            // AutoCAD 2021 names the segments among its subentities: the bottom one 1 and 2, the top one 3 and 4.
            List<Rec> geoms = Objects(document).Where(r => r.Type == "ACDBASSOCGEOMDEPENDENCY").ToList();
            Assert.Equal(2, geoms.Count);
            string[] first = geoms[0]
                .Groups.Where(g => g.Key == 90 || g.Key == 1)
                .Select(g => g.Key + "=" + g.Value)
                .ToArray();
            Assert.Equal(new[] { "1=AcDbAssocEdgePersSubentId", "90=0", "90=1", "90=2" }, first.Skip(first.Length - 4));
            string[] second = geoms[1].Groups.Where(g => g.Key == 90).Select(g => g.Key + "=" + g.Value).ToArray();
            Assert.Equal(new[] { "90=0", "90=3", "90=4" }, second.Skip(second.Length - 3));
        }

        [Fact]
        public void WhatIsNoLine_IsRefused_AsIsTheFirstLineAgain_AndTheQuestionAskedAgain()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            Start(document, "CIRCLE", P(200, 200), "20");
            CadCommandSession session = Start(document, "CENTERLINE", P(220, 200));

            Assert.Contains("Object selected is not a line.", session.History);
            Assert.Equal("Select first line:", session.PromptText);
            Run(session, P(50, 0), P(220, 200));
            Assert.Equal(2, session.History.Count(l => l == "Object selected is not a line."));
            Assert.Equal("Select second line:", session.PromptText);
            Run(session, P(60, 0));
            Assert.Contains("Invalid. Select another line.", session.History);
            Assert.Equal("Select second line:", session.PromptText);
            Run(session, P(50, 40));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void TheLinesOfTheVariables_SizeACentreLine_AndHiddenExtensionsAreNoneOfIt()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            document.Database.CenterExe = 2.0;
            document.Database.CenterLtScale = 3.0;
            Start(document, "CENTERLINE", P(50, 0), P(50, 40));

            DxfBlockReference insert = TheInsert(document);
            Near(new GeoPoint3(-52, 0, 0), TheLine(insert).StartPoint);
            Assert.Equal(3.0, TheLine(insert).LinetypeScale);
            Assert.Equal(3.0, insert.LinetypeScale);
        }

        [Fact]
        public void TheAssociativeObjectsOfACentreLine_AreAutoCad2021sGroupForGroup()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            string one = All<DxfLine>(document.ModelSpace)[0].Handle;
            string two = All<DxfLine>(document.ModelSpace)[1].Handle;
            Start(document, "CENTERLINE", P(50, 0), P(50, 40));

            List<Rec> records = Objects(document);
            Rec action = records.Single(r => r.Type == "ACDBASSOCACTION");
            Rec body = records.Single(r => r.Type == "AcDbCenterLineActionBody");
            List<Rec> edges = records.Where(r => r.Type == "ACDBASSOCEDGEACTIONPARAM").ToList();
            List<Rec> geoms = records.Where(r => r.Type == "ACDBASSOCGEOMDEPENDENCY").ToList();
            Rec dependency = records.Single(r => r.Type == "ACDBASSOCDEPENDENCY");
            Assert.Equal(2, edges.Count);
            Assert.Equal(2, geoms.Count);

            var names = new Dictionary<string, string>
            {
                [action.Handle] = "ACTION",
                [body.Handle] = "BODY",
                [edges[0].Handle] = "EDGE1",
                [edges[1].Handle] = "EDGE2",
                [geoms[0].Handle] = "GEOM1",
                [geoms[1].Handle] = "GEOM2",
                [dependency.Handle] = "DEP",
                [one] = "ONE",
                [two] = "TWO",
                [TheInsert(document).Handle] = "INSERT",
                ["0"] = "0",
            };
            Rec net = records.Single(r =>
                r.Type == "ACDBASSOCNETWORK" && r.Groups.Any(g => g.Key == 360 && g.Value == action.Handle)
            );
            names[net.Handle] = "NET";

            Assert.Equal(
                new[]
                {
                    "5=ACTION",
                    "330=NET",
                    "100=AcDbAssocAction",
                    "90=2",
                    "90=0",
                    "330=NET",
                    "360=BODY",
                    "90=1",
                    "90=3",
                    "90=3",
                    "360=GEOM1",
                    "360=GEOM2",
                    "360=DEP",
                    "90=0",
                    "90=2",
                    "360=EDGE1",
                    "360=EDGE2",
                    "90=0",
                    "90=14",
                }
                    .Concat(Value("StartExtLength", "40=1E+60"))
                    .Concat(Value("EndExtLength", "40=1E+60"))
                    .Concat(Value("StartOvershoot", "40=0.12"))
                    .Concat(Value("EndOvershoot", "40=0.12"))
                    .Concat(
                        new[]
                        {
                            "90=0",
                            "1=EdgeRefDirectionEnding",
                            "90=0",
                            "90=2",
                            "90=1",
                            "330=0",
                            "90=1",
                            "330=0",
                            "330=0",
                        }
                    )
                    .Concat(Value("CenterDepId", "330=DEP"))
                    .Concat(Value("CachedCenterLineLength", "40=100"))
                    .Concat(Value("CachedCenterLineDirectionX", "40=1"))
                    .Concat(Value("CachedCenterLineDirectionY", "40=0"))
                    .Concat(Value("CachedCenterLineDirectionZ", "40=0"))
                    .Concat(Value("SubentLineType", "1=CENTER2"))
                    .Concat(Value("SubentLineWeight", "40=-1"))
                    .Concat(Value("SubentLinetypeScale", "40=1"))
                    .Concat(Value("CachedAssociateStatus", "40=1"))
                    .ToArray(),
                Shape(action, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=BODY",
                    "330=ACTION",
                    "100=AcDbAssocActionBody",
                    "90=2",
                    "100=AcDbSmartCenterActionBody",
                    "90=0",
                },
                Shape(body, names)
            );
            Assert.Equal(
                new[]
                {
                    "5=EDGE1",
                    "330=ACTION",
                    "100=AcDbAssocActionParam",
                    "90=1",
                    "90=0",
                    "1=InputEdge",
                    "100=AcDbAssocSingleDependencyActionParam",
                    "90=0",
                    "330=GEOM1",
                    "100=AcDbAssocEdgeActionParam",
                    "90=0",
                    "330=0",
                    "290=1",
                    "90=23",
                    "10=0",
                    "20=0",
                    "30=0",
                    "10=100",
                    "20=0",
                    "30=0",
                },
                Shape(edges[0], names)
            );
            Assert.Equal(
                new[]
                {
                    "5=EDGE2",
                    "330=ACTION",
                    "100=AcDbAssocActionParam",
                    "90=1",
                    "90=0",
                    "1=InputEdge",
                    "100=AcDbAssocSingleDependencyActionParam",
                    "90=0",
                    "330=GEOM2",
                    "100=AcDbAssocEdgeActionParam",
                    "90=0",
                    "330=0",
                    "290=1",
                    "90=23",
                    "10=0",
                    "20=40",
                    "30=0",
                    "10=100",
                    "20=0",
                    "30=0",
                },
                Shape(edges[1], names)
            );
            Assert.Equal(
                new[]
                {
                    "5=GEOM2",
                    "330=ACTION",
                    "100=AcDbAssocDependency",
                    "90=2",
                    "90=0",
                    "290=1",
                    "290=0",
                    "290=1",
                    "290=1",
                    "90=0",
                    "330=TWO",
                    "290=0",
                    "330=0",
                    "330=0",
                    "360=0",
                    "90=2",
                    "100=AcDbAssocGeomDependency",
                    "90=0",
                    "290=1",
                    "1=AcDbAssocSingleEdgePersSubentId",
                    "290=0",
                },
                Shape(geoms[1], names)
            );
            Assert.Equal(
                new[]
                {
                    "5=DEP",
                    "330=ACTION",
                    "100=AcDbAssocDependency",
                    "90=2",
                    "90=0",
                    "290=1",
                    "290=1",
                    "290=1",
                    "290=1",
                    "90=0",
                    "330=INSERT",
                    "290=0",
                    "330=0",
                    "330=0",
                    "360=0",
                    "90=3",
                },
                Shape(dependency, names)
            );
            AssertReopens(document);
        }

        [Fact]
        public void ACentreLineFollowsItsLines_MovedTogetherOrOneAtATime()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            string two = All<DxfLine>(document.ModelSpace)[1].Handle;
            Start(document, "CENTERLINE", P(50, 0), P(50, 40));

            document.Selection.Set(new[] { two });
            Start(document, "MOVE", P(0, 0), P(0, 20));
            Near(new GeoPoint3(50, 30, 0), TheInsert(document).Position);

            // The second line made shorter by a stretch of its end: the line is drawn again between the new run.
            document.ApplyEdit("test", new[] { new CadEditRecord(CadEditRecordKind.Reshaped, two) }, s =>
                ((DxfLine)s.Document.FindEntity(two)).EndPoint = new GeoPoint3(50, 60, 0)
            );
            IsCentreLine(document, 37.5, 30, -37.62, 0, 37.62, 0, 1E-9);
        }

        [Fact]
        public void ACentreLineMovedOnItsOwn_NoLongerFollowsItsLines()
        {
            CadDocument document = TwoLines(0, 0, 100, 0, 0, 40, 100, 40);
            string one = All<DxfLine>(document.ModelSpace)[0].Handle;
            Start(document, "CENTERLINE", P(50, 0), P(50, 40));

            document.Selection.Set(new[] { TheInsert(document).Handle });
            Start(document, "MOVE", P(0, 0), P(0, 5));
            Assert.DoesNotContain(Objects(document), r => r.Type == "ACDBASSOCACTION");
            Assert.Empty(document.FindEntity(one).GetPersistentReactorIds());

            document.Selection.Set(new[] { one });
            Start(document, "MOVE", P(0, 0), P(0, 10));
            Near(new GeoPoint3(50, 25, 0), TheInsert(document).Position);
        }
    }
}
