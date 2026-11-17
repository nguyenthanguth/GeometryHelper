using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// MASSPROP of regions: the report of AutoCAD 2021's console for the same regions (a rectangle, an L, a box and a second
    /// one, a rectangle turned, a disc with a hole), to the four places it writes, and the .mpr file it writes.
    /// </summary>
    public class MassPropTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfRegionShape Polygon(params double[] xy)
        {
            var curves = new List<DxfCurve>();
            int n = xy.Length / 2;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                curves.Add(
                    new DxfLine(new GeoPoint3(xy[2 * i], xy[2 * i + 1], 0), new GeoPoint3(xy[2 * j], xy[2 * j + 1], 0))
                );
            }

            return DxfRegionShape.Create(curves, null, Tolerance.Default);
        }

        private static CadDocument With(params DxfRegionShape[] shapes)
        {
            var database = new DxfDatabase("AC1027");

            foreach (DxfRegionShape shape in shapes)
            {
                database.ModelSpace.AppendEntity(DxfUnknownEntity.CreateRegion(shape, database));
            }

            return CadDocument.From(database, "plan.dxf");
        }

        private static string[] Report(CadCommandSession session) =>
            session.History.SkipWhile(h => !h.StartsWith(" -----", StringComparison.Ordinal)).Take(15).ToArray();

        [Fact]
        public void ARectangleOf100By50_GivesTheReportAutoCadGave()
        {
            CadDocument document = With(Polygon(0, 0, 100, 0, 100, 50, 0, 50));
            CadCommandSession session = Start(
                document,
                "MASSPROP",
                new[] { document.ModelSpace.Block.First().Handle },
                Enter
            );

            Assert.Equal(
                new[]
                {
                    " ----------------   REGIONS   ----------------",
                    "Area:                    5000.0000",
                    "Perimeter:               300.0000",
                    "Bounding box:         X: 0.0000  --  100.0000",
                    "                      Y: 0.0000  --  50.0000",
                    "Centroid:             X: 50.0000",
                    "                      Y: 25.0000",
                    "Moments of inertia:   X: 4166666.6667",
                    "                      Y: 16666666.6667",
                    "Product of inertia:  XY: -6250000.0000",
                    "Radii of gyration:    X: 28.8675",
                    "                      Y: 57.7350",
                    "Principal moments and X-Y directions about centroid:",
                    "                      I: 1041666.6667 along [1.0000 0.0000]",
                    "                      J: 4166666.6667 along [0.0000 1.0000]",
                },
                Report(session)
            );
            Assert.Equal("Write analysis to a file? [Yes/No] <N>:", session.PromptText);
            Run(session, "N");
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnLShape_GivesItsPrincipalMomentsAboutTheCentroidAsAutoCadGaveThem()
        {
            CadDocument document = With(Polygon(0, 0, 100, 0, 100, 20, 20, 20, 20, 60, 0, 60));
            CadCommandSession session = Start(
                document,
                "MASSPROP",
                new[] { document.ModelSpace.Block.First().Handle },
                Enter
            );
            string[] report = Report(session);

            Assert.Equal("Area:                    2800.0000", report[1]);
            Assert.Equal("Perimeter:               320.0000", report[2]);
            Assert.Equal("Centroid:             X: 38.5714", report[5]);
            Assert.Equal("                      Y: 18.5714", report[6]);
            Assert.Equal("Moments of inertia:   X: 1653333.3333", report[7]);
            Assert.Equal("                      Y: 6773333.3333", report[8]);
            Assert.Equal("Product of inertia:  XY: -1320000.0000", report[9]);
            Assert.Equal("Radii of gyration:    X: 24.2997", report[10]);
            Assert.Equal("                      Y: 49.1838", report[11]);
            Assert.Equal("                      I: 467871.5824 along [0.9523 -0.3052]", report[13]);
            Assert.Equal("                      J: 2827366.5128 along [0.3052 0.9523]", report[14]);
        }

        [Fact]
        public void ATallRectangleAndATriangleAndTurnedRectangles_HaveTheDirectionsAutoCadGave()
        {
            // The axis of I lies between -45 and 45 degrees; J is a quarter turn counter-clockwise of it.
            CadDocument document = With(
                Polygon(0, 0, 50, 0, 50, 100, 0, 100),
                Polygon(0, 0, 100, 0, 0, 100),
                Polygon(0, 0, 86.6, 50, 76.6, 67.3, -10, 17.3),
                Polygon(0, 0, 50, 86.6, 30, 98.1, -20, 11.5)
            );
            string[] handles = document.ModelSpace.Block.Select(e => e.Handle).ToArray();
            string[][] expected =
            {
                new[] { "I: 4166666.6667 along [1.0000 0.0000]", "J: 1041666.6667 along [0.0000 1.0000]" },
                new[] { "I: 1388888.8889 along [0.7071 -0.7071]", "J: 4166666.6667 along [0.7071 0.7071]" },
                new[] { "I: 66487.7570 along [0.8660 0.5000]", "J: 1665076.7507 along [-0.5000 0.8660]" },
                new[] { "I: 1922415.7514 along [0.8660 -0.5001]", "J: 102324.7211 along [0.5001 0.8660]" },
            };

            for (int i = 0; i < handles.Length; i++)
            {
                CadCommandSession session = Start(document, "MASSPROP", new[] { handles[i] }, Enter);
                string[] report = Report(session);
                Assert.Equal("                      " + expected[i][0], report[13]);
                Assert.Equal("                      " + expected[i][1], report[14]);
                Run(session, "N");
            }
        }

        [Fact]
        public void TwoRegionsSelected_AreOneBody_ThePerimeterTheSumOfThePerimeters()
        {
            CadDocument document = With(Polygon(0, 0, 10, 0, 10, 10, 0, 10), Polygon(50, 50, 60, 50, 60, 70, 50, 70));
            CadCommandSession session = Start(
                document,
                "MASSPROP",
                document.ModelSpace.Block.Select(e => e.Handle).ToArray(),
                Enter
            );
            string[] report = Report(session);

            Assert.Equal("Area:                    300.0000", report[1]);
            Assert.Equal("Perimeter:               100.0000", report[2]);
            Assert.Equal("Bounding box:         X: 0.0000  --  60.0000", report[3]);
            Assert.Equal("                      Y: 0.0000  --  70.0000", report[4]);
            Assert.Equal("Centroid:             X: 38.3333", report[5]);
            Assert.Equal("                      Y: 41.6667", report[6]);
            Assert.Equal("Moments of inertia:   X: 730000.0000", report[7]);
            Assert.Equal("                      Y: 610000.0000", report[8]);
            Assert.Equal("Product of inertia:  XY: -662500.0000", report[9]);
            Assert.Equal("                      I: 373587.6826 along [0.7445 -0.6677]", report[13]);
            Assert.Equal("                      J: 4745.6508 along [0.6677 0.7445]", report[14]);
        }

        [Fact]
        public void ADiscWithAHole_TakesTheHoleAway_ItsMomentsExactToTheBit()
        {
            DxfRegionShape ring = DxfRegionShape.Create(
                new DxfCurve[] { new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 30) },
                new[]
                {
                    (IEnumerable<DxfCurve>)
                        new DxfCurve[] { new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10) },
                },
                Tolerance.Default
            );
            CadMassProperties found = CadMassProperties.Of(new[] { ring });

            double area = Math.PI * (30 * 30 - 10 * 10);
            double moment = Math.PI / 4.0 * (Math.Pow(30, 4) - Math.Pow(10, 4));
            Assert.Equal(area, found.Area, 8);
            Assert.Equal(2.0 * Math.PI * 40.0, found.Perimeter, 8);
            Assert.Equal(moment, found.MomentX, 6);
            Assert.Equal(moment, found.MomentY, 6);
            Assert.Equal(0.0, found.ProductXY, 6);
            Assert.Equal(0.0, found.Centroid.X, 8);
            Assert.Equal(-30.0, found.BoxMin.X, 9);
            Assert.Equal(30.0, found.BoxMax.Y, 9);
            Assert.Equal(moment, found.PrincipalI, 6);
            Assert.Equal(moment, found.PrincipalJ, 6);
        }

        [Fact]
        public void ARegionBuiltOfArcs_IsIntegratedAlongItsArcs_AHalfDiscHasItsKnownCentroid()
        {
            double r = 20;
            DxfRegionShape half = DxfRegionShape.Create(
                new DxfCurve[]
                {
                    new DxfArc(new GeoPoint3(0, 0, 0), r, 0, Math.PI),
                    new DxfLine(new GeoPoint3(-r, 0, 0), new GeoPoint3(r, 0, 0)),
                },
                null,
                Tolerance.Default
            );
            CadMassProperties found = CadMassProperties.Of(new[] { half });

            Assert.Equal(Math.PI * r * r / 2.0, found.Area, 8);
            Assert.Equal(0.0, found.Centroid.X, 8);
            Assert.Equal(4.0 * r / (3.0 * Math.PI), found.Centroid.Y, 8);
            Assert.Equal(Math.PI * Math.Pow(r, 4) / 8.0, found.MomentX, 6);
            Assert.Equal(0.0, found.BoxMin.Y, 9);
            Assert.Equal(r, found.BoxMax.Y, 9);
        }

        [Fact]
        public void MasspropSaysWhatIsWrong_ForNothingSelected_AndForObjectsThatAreNoRegions()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document, "MASSPROP", Enter);
            Assert.Equal("Nothing selected.", session.History.Last());

            Run(session, "MASSPROP", new[] { "A1" }, Enter);
            Assert.Equal("No solids or regions selected.", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void WriteAnalysisToAFile_WritesTheMprAsAutoCadWritesIt()
        {
            CadDocument document = With(Polygon(0, 0, 100, 0, 100, 50, 0, 50));
            string path = Path.Combine(Path.GetTempPath(), "small3-" + Guid.NewGuid().ToString("N") + ".mpr");

            try
            {
                CadCommandSession session = Start(
                    document,
                    "MASSPROP",
                    new[] { document.ModelSpace.Block.First().Handle },
                    Enter,
                    "Y"
                );
                Assert.StartsWith("Enter file name <", session.PromptText, StringComparison.Ordinal);
                Assert.EndsWith("plan.mpr>:", session.PromptText, StringComparison.Ordinal);
                Run(session, path);

                string text = File.ReadAllText(path);
                Assert.Equal(
                    "\r\n"
                        + " ----------------   REGIONS   ---------------- \r\n"
                        + "\r\n"
                        + "Area:                    5000.0000 \r\n"
                        + "Perimeter:               300.0000 \r\n"
                        + "Bounding box:         X: 0.0000  --  100.0000 \r\n"
                        + "                      Y: 0.0000  --  50.0000 \r\n"
                        + "Centroid:             X: 50.0000 \r\n"
                        + "                      Y: 25.0000 \r\n"
                        + "Moments of inertia:   X: 4166666.6667  \r\n"
                        + "                      Y: 16666666.6667  \r\n"
                        + "Product of inertia:  XY: -6250000.0000  \r\n"
                        + "Radii of gyration:    X: 28.8675 \r\n"
                        + "                      Y: 57.7350 \r\n"
                        + "Principal moments and X-Y directions about centroid:\r\n"
                        + "                      I: 1041666.6667 along [1.0000 0.0000]\r\n"
                        + "                      J: 4166666.6667 along [0.0000 1.0000]\r\n",
                    text
                );
                Assert.False(session.IsRunning);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
