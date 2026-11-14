using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PEDIT of a spline against AutoCAD 2021's console (14 November, run interactively): "Specify a precision &lt;10&gt;:", the
    /// polyline of lines it makes, the knots of a spline of fit points among its vertices, and the warning past 10 000 vertices.
    /// The vertices are AutoCAD's own for the spline through (0, 0), (100, 50), (200, 0) and (300, 80).
    /// </summary>
    public class PeditSplineTests
    {
        private static DxfPolyline Last(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        private static void Vertices(DxfPolyline polyline, params double[] xy)
        {
            Assert.Equal(xy.Length / 2, polyline.NumberOfVertices);

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Near(P(xy[2 * i], xy[2 * i + 1]), polyline.GetPoint2dAt(i), 1E-6);
                Assert.Equal(0.0, polyline.GetBulgeAt(i));
            }
        }

        /// <summary>The cubic through (0, 0), (100, 50), (200, 0) and (300, 80), a height up, as AutoCAD's SPLINE makes it of those fit points.</summary>
        private static DxfSpline Fit(CadDocument document, double up = 0.0)
        {
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(
                new[]
                {
                    new GeoPoint3(0, up, 0),
                    new GeoPoint3(100, up + 50, 0),
                    new GeoPoint3(200, up, 0),
                    new GeoPoint3(300, up + 80, 0),
                },
                3,
                0.0
            );
            return spline;
        }

        private static CadCommandSession WithSpline(out CadDocument document)
        {
            document = Blank();
            Fit(document);
            return Session(document);
        }

        [Theory]
        [InlineData(0, new[] { 0.0, 0.0, 100.0, 50.0, 200.0, 0.0, 300.0, 80.0 })]
        [InlineData(1, new[] { 0.0, 0.0, 100.0, 50.0, 158.206157536, 19.430869868, 200.0, 0.0, 300.0, 80.0 })]
        [InlineData(
            2,
            new[]
            {
                0.0,
                0.0,
                44.652961406,
                34.646411109,
                100.0,
                50.0,
                135.470589354,
                34.363923731,
                200.0,
                0.0,
                222.784000513,
                3.048450699,
                262.138620615,
                33.006507946,
                300.0,
                80.0,
            }
        )]
        [InlineData(
            5,
            new[]
            {
                0.0,
                0.0,
                39.056726027,
                30.947506515,
                58.674607156,
                42.550891904,
                78.399910024,
                49.509835851,
                100.0,
                50.0,
                118.288736684,
                43.902298449,
                158.206157536,
                19.430869868,
                177.745234497,
                7.697130624,
                200.0,
                0.0,
                215.117553115,
                0.710719346,
                232.844308458,
                8.009267657,
                250.07088649,
                21.014021181,
                266.922331539,
                38.298428615,
                300.0,
                80.0,
            }
        )]
        public void ASplineOfFitPoints_BecomesAPolylineOfLines_WithItsKnotsAmongTheVertices_AsAutoCad2021(
            int precision,
            double[] xy
        )
        {
            CadCommandSession session = WithSpline(out CadDocument document);

            Run(session, "PEDIT", P(100, 50));
            Assert.Equal("Do you want to turn it into one? <Y>", session.PromptText);
            Assert.Contains("Object selected is not a polyline", session.History);
            Run(session, Enter);
            Assert.Equal("Specify a precision <10>:", session.PromptText);
            Run(session, precision.ToString());
            Assert.StartsWith("Enter an option [Close/Join/Width/Edit vertex/Fit/Spline", session.PromptText);
            Run(session, Enter);

            Assert.Empty(All<DxfSpline>(document.ModelSpace));
            Vertices(Last(document), xy);
        }

        [Fact]
        public void ThePrecisionAskedLast_IsTheNextDefault_SharedBySingleAndMultiple()
        {
            CadDocument document = Blank();
            Fit(document);
            Fit(document, 100);
            Fit(document, 200);
            CadCommandSession session = Session(document);
            Run(session, "PEDIT", P(100, 50), Enter, "7", Enter);

            Run(session, "PEDIT", P(100, 150), Enter);
            Assert.Equal("Specify a precision <7>:", session.PromptText);
            Run(session, "100");
            Assert.Contains("Requires an integer between 0 and 99.", session.History);
            Run(session, "3", Enter);

            Run(session, "PEDIT", "M", P(100, 250), Enter);
            Assert.Equal("Convert Lines, Arcs and Splines to polylines [Yes/No]? <Y>", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify a precision for spline conversion <3>:", session.PromptText);
        }

        [Fact]
        public void PeditAcceptOne_StillAsksThePrecision_AndMultipleAsksItOnlyWhereSplinesAreAmongTheObjects()
        {
            CadCommandSession session = WithSpline(out CadDocument document);
            Run(session, "PEDITACCEPT", "1", "PEDIT", P(100, 50));
            Assert.Equal("Specify a precision <10>:", session.PromptText);
            Run(session, "4", Enter);
            Assert.Equal(12, Last(document).NumberOfVertices);

            Run(session, "LINE", P(0, 100), P(100, 100), Enter, "PEDIT", "M", P(50, 100), Enter);
            Assert.StartsWith("Enter an option [Close/Open/Join", session.PromptText);
        }

        [Fact]
        public void AtPlineTypeNought_TheSplineBecomesAnOldStylePolyline()
        {
            CadCommandSession session = WithSpline(out CadDocument document);

            Run(session, "PLINETYPE", "0", "PEDIT", P(100, 50), Enter, "5", Enter);

            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(14, Assert.Single(All<DxfPolyline2d>(document.ModelSpace)).Count());
        }

        [Fact]
        public void MultipleWithASplineAndALine_AsksOnePrecision_ThenMakesPolylinesOfBoth()
        {
            CadCommandSession session = WithSpline(out CadDocument document);
            Run(session, "LINE", P(0, 100), P(100, 100), Enter);

            string[] both = All<DxfSpline>(document.ModelSpace)
                .Select(s => s.Handle)
                .Concat(All<DxfLine>(document.ModelSpace).Select(l => l.Handle))
                .ToArray();
            Run(session, "PEDIT", "M", both, Enter);
            Assert.Equal("Convert Lines, Arcs and Splines to polylines [Yes/No]? <Y>", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Specify a precision for spline conversion <10>:", session.PromptText);
            Run(session, "2");
            Run(session, Enter);

            Assert.Empty(All<DxfSpline>(document.ModelSpace));
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            Assert.Equal(
                new[] { 2, 8 },
                All<DxfPolyline>(document.ModelSpace).Select(p => p.NumberOfVertices).OrderBy(n => n)
            );
        }

        [Fact]
        public void PastTenThousandVertices_ItWarnsAndNamesTheLargestPrecisionThatGivesNoMore_AsAutoCad2021()
        {
            // AutoCAD: a cubic of seven control points over the knots 0 to 4 gave 9 947 vertices at 81 and 10 027 at 82; at 99 it
            // said so, No asked again with 81 as the default, and Yes made the 12 917 of precision 99.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            var spline = new DxfSpline(
                3,
                false,
                false,
                false,
                new[]
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(40, 60, 0),
                    new GeoPoint3(90, -30, 0),
                    new GeoPoint3(150, 50, 0),
                    new GeoPoint3(210, -20, 0),
                    new GeoPoint3(260, 40, 0),
                    new GeoPoint3(300, 0, 0),
                },
                new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 2.0, 3.0, 4.0, 4.0, 4.0, 4.0 },
                null,
                1E-10,
                1E-9
            );
            document.Database.ModelSpace.AppendEntity(spline);

            Run(session, "PEDIT", new[] { spline.Handle }, Enter);
            Run(session, "99");
            Assert.Equal(
                "Using a precision value greater than 81 may cause performance problems.  Do you want to continue? [Yes/No]: <N>:",
                session.PromptText
            );
            Run(session, "N");
            Assert.Equal("Specify a precision <81>:", session.PromptText);
            Run(session, "50");
            Assert.StartsWith("Enter an option [", session.PromptText);
            Assert.Equal(6591, Last(document).NumberOfVertices);
        }
    }
}
