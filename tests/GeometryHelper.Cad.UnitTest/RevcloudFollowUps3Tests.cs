using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// REVCLOUD against AutoCAD 2021's console run through standard input (15 November): the clouds of shapes shorter than their
    /// arcs, the four variables the presenter lacked, the length the first cloud sets, and Modify's new handle.
    /// </summary>
    public class RevcloudFollowUps3Tests
    {
        private const double Bulge = 0.52056705;
        private const string Invalid = "Invalid shape to create revision cloud.";

        private static DxfPolyline Cloud(CadDocument document) => All<DxfPolyline>(document.ModelSpace).Last();

        private static CadCommandSession Arcs(out CadDocument document, double length, bool varies = false)
        {
            document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUDARCVARIANCE", varies ? "1" : "0", "REVCLOUDAPPROXARCLEN", length.ToString("R"));
            return session;
        }

        private static void Vertices(DxfPolyline cloud, bool closed, double bulge, params double[] xy)
        {
            Assert.Equal(closed, cloud.Closed);
            Assert.Equal(xy.Length / 2, cloud.NumberOfVertices);

            for (int i = 0; i < cloud.NumberOfVertices; i++)
            {
                Near(P(xy[2 * i], xy[2 * i + 1]), cloud.GetPoint2dAt(i), 1E-7);
                Assert.Equal(bulge, cloud.GetBulgeAt(i), 8);
            }
        }

        [Theory]
        [InlineData(1.0, 1.0)]
        [InlineData(4.0, 4.0)]
        [InlineData(10.0, 10.0)]
        public void ARectangleShorterAroundThanTwoArcs_IsACloudOfTwoVertices_AndTheMessage_AsAutoCad2021(
            double side,
            double second
        )
        {
            // AutoCAD 2021's console, arcs of 20: Rectangular from (0, 0) to (10, 10) leaves a closed LWPOLYLINE of (0, 0) and
            // (10, 10), both bulging 0.52056705, with "Invalid shape to create revision cloud." said all the same.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", P(0, 0), P(side, second));

            Vertices(Cloud(document), true, Bulge, 0, 0, side, second);
            Assert.Contains(Invalid, session.History);
        }

        [Fact]
        public void ARectangleOfAPerimeterEqualToTheArc_IsTheMessageAndNoCloud()
        {
            // 5 by 5 at arcs of 20: the perimeter is 20 and one vertex lies on it, which AutoCAD leaves undrawn.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", P(0, 0), P(5, 5));

            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Contains(Invalid, session.History);
        }

        [Fact]
        public void ARectangleOfFiveArcs_IsDrawn_WithTheMessage_AsAutoCad2021()
        {
            // 25 by 25 at arcs of 20: five vertices (a perimeter of 100), drawn, clockwise, but below the six that pass.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", P(0, 0), P(25, 25));

            Vertices(Cloud(document), true, -Bulge, 0, 0, 0, 20, 15, 25, 25, 15, 20, 0);
            Assert.Contains(Invalid, session.History);
        }

        [Fact]
        public void ARectangleOfSixArcs_IsDrawn_WithNoMessage()
        {
            CadCommandSession session = Arcs(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", P(0, 0), P(30, 30));

            Assert.Equal(6, Cloud(document).NumberOfVertices);
            Assert.DoesNotContain(Invalid, session.History);
        }

        [Fact]
        public void APolygonOfFiveVertices_IsTheMessageAndNoCloud_ButSixAreDrawn()
        {
            // AutoCAD 2021's Polygonal draws nothing below six vertices, where its Rectangular draws what it has: the square of
            // 25 (five arcs) gives nothing, the square of 28 (six) a cloud.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);

            Run(session, "REVCLOUD", "P", P(0, 0), P(25, 0), P(25, 25), P(0, 25), Enter);
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Contains(Invalid, session.History);

            Run(session, "REVCLOUD", P(0, 0), P(28, 0), P(28, 28), P(0, 28), Enter);
            Assert.Equal(6, Cloud(document).NumberOfVertices);
        }

        [Theory]
        [InlineData(3.0, 1.5)]
        [InlineData(15.0, 7.5)]
        [InlineData(19.9, 9.95)]
        [InlineData(20.0, 20.0)]
        [InlineData(25.0, 25.0)]
        public void ALineShorterThanTheArc_IsACloudOfItsStartAndItsMiddle_AsAutoCad2021(double length, double second)
        {
            // AutoCAD 2021's console, arcs of 20: lines 3, 15 and 25 long gave (0, 0) with (1.5, 0), (7.5, 0) and (25, 0): the
            // middle where the line is shorter than an arc, the end from one arc long on.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);
            Run(session, "LINE", P(0, 0), P(length, 0), Enter, "REVCLOUD", "O", P(length / 2.0, 0), "N");

            Vertices(Cloud(document), false, Bulge, 0, 0, second, 0);
            Assert.Contains("Revision cloud finished.", session.History);
        }

        [Theory]
        [InlineData(26.6, 13.3)]
        [InlineData(26.7, 26.7)]
        public void WithTheArcsVarying_ALineShorterThanTheLongestArc_IsHalved(double length, double second)
        {
            // At arcs of 20 varying from 13.3333 to 26.6667 the middle is taken below 26.6667, not below the shortest arc.
            CadCommandSession session = Arcs(out CadDocument document, 20.0, true);
            Run(session, "LINE", P(0, 0), P(length, 0), Enter, "REVCLOUD", "O", P(length / 2.0, 0), "N");

            Vertices(Cloud(document), false, Bulge, 0, 0, second, 0);
        }

        [Fact]
        public void ACircleOf5AtArcsOf20_IsACloudOfTwoVertices_ACircleOf4IsTooSmall()
        {
            CadCommandSession session = Arcs(out CadDocument document, 20.0);
            Run(session, "CIRCLE", P(0, 0), "5", "REVCLOUD", "O", P(5, 0), "N");

            Vertices(Cloud(document), true, Bulge, 5, 0, -3.268218104, -3.784012477);
            Assert.Contains("Revision cloud finished.", session.History);

            Run(session, "CIRCLE", P(100, 0), "4", "REVCLOUD", "O", P(104, 0));
            Assert.Contains("Unable to convert object, object is too small for the cloud's arcs.", session.History);
            Assert.Equal("Select object:", session.PromptText);
        }

        [Fact]
        public void ACircleOf3AtArcsOf20_IsACloudOfTwoOppositeVertices()
        {
            // Shorter around than an arc: the start and the point halfway round.
            CadCommandSession session = Arcs(out CadDocument document, 20.0);
            Run(session, "CIRCLE", P(0, 0), "3", "REVCLOUD", "O", P(3, 0), "N");

            Vertices(Cloud(document), true, Bulge, 3, 0, -3, 0);
        }

        [Fact]
        public void TheLengthsOfTheLastArcLengthAndOfRevcloudproperties_AreKeptAsMinAndMaxArcLength()
        {
            // AutoCAD 2021: 0.5 and 0.5 to begin with; Arc length 30 sets 20 and 40 (30 and 30 with the variance off);
            // REVCLOUDPROPERTIES to 12 sets 8 and 16; SETVAR of the approximate length or the variance sets neither.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUDMINARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMINARCLENGTH <0.5000>:", session.PromptText);
            Run(session, Enter, "REVCLOUDMAXARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMAXARCLENGTH <0.5000>:", session.PromptText);
            Run(session, Enter);

            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUD", "A", "30", Esc);
            Run(session, "REVCLOUDMINARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMINARCLENGTH <30.0000>:", session.PromptText);
            Run(session, Enter, "REVCLOUDMAXARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMAXARCLENGTH <30.0000>:", session.PromptText);
            Run(session, Enter);

            Run(session, "REVCLOUDARCVARIANCE", "1", "REVCLOUD", "A", "30", Esc, "REVCLOUDMINARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMINARCLENGTH <20.0000>:", session.PromptText);
            Run(session, Enter, "REVCLOUDMAXARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMAXARCLENGTH <40.0000>:", session.PromptText);
            Run(session, Enter);

            // SETVAR of the approximate length changes neither.
            Run(session, "REVCLOUDAPPROXARCLEN", "7", "REVCLOUDMINARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMINARCLENGTH <20.0000>:", session.PromptText);
            Run(session, Enter);

            Run(session, "REVCLOUD", "P", P(0, 0), P(100, 0), P(100, 100), Enter);
            Run(session, "REVCLOUDPROPERTIES", new[] { Cloud(document).Handle }, Enter, "A", "12");
            Run(session, "REVCLOUDMINARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMINARCLENGTH <8.0000>:", session.PromptText);
            Run(session, Enter, "REVCLOUDMAXARCLENGTH");
            Assert.Equal("Enter new value for REVCLOUDMAXARCLENGTH <16.0000>:", session.PromptText);
            Run(session, Enter);
        }

        [Fact]
        public void RevcloudGrips_IsASwitch_OnToBeginWith()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUDGRIPS");
            Assert.Equal("Enter new value for REVCLOUDGRIPS <ON>:", session.PromptText);
            Run(session, "2");
            Assert.Contains("Requires 0 or 1 only, or option keyword.", session.History);
            Run(session, "OFF", "REVCLOUDGRIPS");
            Assert.Equal("Enter new value for REVCLOUDGRIPS <OFF>:", session.PromptText);
            Run(session, "1");
        }

        [Fact]
        public void RevcloudArcStyle_IsATextTheStyleOptionSets_AndOnlyExactlyCalligraphyIsCalligraphy()
        {
            // AutoCAD 2021: SETVAR takes any text; REVCLOUD says Calligraphy and draws widths only for "Calligraphy".
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUDAPPROXARCLEN", "10", "REVCLOUDARCSTYLE");
            Assert.Equal("Enter new value for REVCLOUDARCSTYLE <\"Normal\">:", session.PromptText);
            Run(session, "calligraphy", "REVCLOUDARCSTYLE");
            Assert.Equal("Enter new value for REVCLOUDARCSTYLE <\"calligraphy\">:", session.PromptText);
            Run(session, "Calli", "REVCLOUD");
            Assert.Contains("Style: Normal", session.History.Last());
            Run(session, P(0, 0), P(100, 60));
            Assert.Equal(0.0, Cloud(document).GetEndWidthAt(0));

            Run(session, "REVCLOUDARCSTYLE", "Calligraphy", "REVCLOUD");
            Assert.Contains("Style: Calligraphy", session.History.Last());
            Run(session, P(200, 0), P(300, 60));
            Assert.Equal(1.0, Cloud(document).GetEndWidthAt(0), 12);

            // The Style option writes the variable.
            Run(session, "REVCLOUD", "S", "N", Esc, "REVCLOUDARCSTYLE");
            Assert.Equal("Enter new value for REVCLOUDARCSTYLE <\"Normal\">:", session.PromptText);
        }

        [Fact]
        public void TheFirstCloud_SetsTheApproximateArcLength_AsAutoCad2021_AnEscapedOneDoesNot()
        {
            // AutoCAD 2021: REVCLOUDAPPROXARCLEN stays 0 after a REVCLOUD that draws nothing, and is the length the first cloud
            // was drawn with (15 here, a metric drawing with no view) after one that does.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "REVCLOUD", P(0, 0), Esc, "REVCLOUDAPPROXARCLEN");
            Assert.Equal("Enter new value for REVCLOUDAPPROXARCLEN <0.0000>:", session.PromptText);
            Run(session, Enter);

            Run(session, "REVCLOUD", P(0, 0), P(100, 60), "REVCLOUDAPPROXARCLEN");
            Assert.Equal("Enter new value for REVCLOUDAPPROXARCLEN <15.0000>:", session.PromptText);
        }

        [Fact]
        public void Modify_ErasesTheCloudPicked_AndMakesANewOne_KeepingItsDataButNotItsHandle()
        {
            // AutoCAD 2021: after Modify the cloud picked is gone, a new one of a new handle stands, on the same layer and with
            // the same RevcloudProps; REVCLOUDPROPERTIES, which redraws a cloud in place, keeps its handle.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REVCLOUDARCVARIANCE", "0", "REVCLOUDAPPROXARCLEN", "10", "REVCLOUD", P(0, 0), P(100, 60));
            DxfPolyline first = Cloud(document);
            string handle = first.Handle;

            Run(session, "REVCLOUD", "M", P(50, 0), P(50, -23), P(80, -23), P(80, 0), P(65, 0), "N");

            DxfPolyline modified = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.NotEqual(handle, modified.Handle);
            Assert.True(modified.Closed);
            IReadOnlyList<DxfTypedValue> data = modified.GetXDataForApplication("RevcloudProps");
            Assert.NotNull(data);
            Assert.Equal("10", data[2].Value);

            string kept = modified.Handle;
            Run(session, "REVCLOUDPROPERTIES", new[] { kept }, Enter, "A", "20");
            Assert.Equal(kept, Assert.Single(All<DxfPolyline>(document.ModelSpace)).Handle);
        }
    }
}
