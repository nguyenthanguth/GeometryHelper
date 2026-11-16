using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The leader band of an angle's words put by DIMTEDIT for words wider or narrower than "60°". Every figure is a picture made
    /// by accoreconsole: the metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial, an angle of
    /// 60 degrees at the origin with its arc 30 out, the words put at a direction and a distance off the arc.
    /// </summary>
    public class DimensionFollowUps20Tests
    {
        private static CadCommandSession Probe(int decimals)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "probe.dxf");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Probe", FileName = "arial.ttf" });
            string start = CadDimStyleEditor.Current(database);
            var values = new SortedDictionary<string, object>(CadDimStyleEditor.Read(database.DimStyleTable[start]))
            {
                ["Dimasz"] = 2.5,
                ["Dimtxt"] = 2.5,
                ["Dimgap"] = 0.625,
                ["Dimexo"] = 0.625,
                ["Dimexe"] = 1.25,
                ["Dimscale"] = 1.0,
                ["Dimdec"] = 0,
                ["Dimtad"] = 1,
                ["Dimtih"] = false,
                ["Dimtoh"] = false,
                ["Dimtofl"] = true,
                ["Dimatfit"] = 3,
                ["Dimcen"] = 2.5,
                ["Dimjust"] = 0,
                ["Dimtix"] = false,
                ["Dimsoxd"] = false,
                ["Dimtmove"] = 1,
                ["Dimtvp"] = 0.0,
                ["Dimadec"] = decimals,
                ["Dimdsep"] = (char)46,
                ["Dimazin"] = 0,
                ["Dimtxsty"] = "Probe",
            };
            CadDimStyleEditor.Add(document, "Probe", start, values);
            CadDimStyleEditor.SetCurrent(document, "Probe");
            return Session(document);
        }

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

        private const int NoLeader = 2;
        private const int Leader = 4;

        /// <summary>
        /// The lines of the picture of an angle of 60 degrees from <paramref name="first"/>, its arc 30 out, its words (typed as
        /// <paramref name="words"/>, or the measured angle with <paramref name="decimals"/> decimals where that is null) put by
        /// DIMTEDIT <paramref name="offTheArc"/> off the arc at <paramref name="degrees"/> from the vertex.
        /// </summary>
        private static int Lines(double first, double degrees, double offTheArc, int decimals, string words)
        {
            const double Arc = 30.0;
            CadCommandSession session = Probe(decimals);
            GeoPoint2 a = Polar(first, Arc + 20);
            GeoPoint2 b = Polar(first + 60.0, Arc + 20);
            GeoPoint2 at = Polar(first + 30.0, Arc);

            if (words == null)
            {
                Run(session, "DAN", Enter, P(0, 0), a, b, at);
            }
            else
            {
                Run(session, "DAN", Enter, P(0, 0), a, b, "Text", words, at);
            }

            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, Arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        [Fact]
        public void WordsOfADimadecOf2PutRoundAnAngleFromOtherFirstRays_KeepNoLeaderToTheEdgeOfTheirBand()
        {
            // AutoCAD 2021, "60.00°" (Arial, 9.77 wide): from 45 degrees the words put at 30 degrees from the vertex get a leader
            // at 5.5 in and none at 5.25; from 90 degrees, at 60 degrees none to 6.5 out and one from 6.75, and at 240 degrees a
            // leader at 5.75 in and none at 5.5; from 0, at 30 degrees none to 8.75 out and one from 9.0.
            Assert.Equal(Leader, Lines(45.0, 30.0, -5.5, 2, null));
            Assert.Equal(NoLeader, Lines(45.0, 30.0, -5.25, 2, null));
            Assert.Equal(NoLeader, Lines(90.0, 60.0, 6.5, 2, null));
            Assert.Equal(Leader, Lines(90.0, 60.0, 6.75, 2, null));
            Assert.Equal(Leader, Lines(90.0, 240.0, -5.75, 2, null));
            Assert.Equal(NoLeader, Lines(90.0, 240.0, -5.5, 2, null));
            Assert.Equal(NoLeader, Lines(0.0, 30.0, 8.75, 2, null));
            Assert.Equal(Leader, Lines(0.0, 30.0, 9.0, 2, null));
        }

        [Fact]
        public void The6DegreeWordsPutRoundAnAngleOf60Degrees_KeepNoLeaderToTheEdgeOfTheirBand()
        {
            // AutoCAD 2021, "6°" (Arial, 2.98 wide) put round an angle of 60 degrees from 0: at 0 degrees from the vertex a leader at
            // 5.75 in and none at 5.5, none to 5.5 out and one from 5.75; at 50 degrees a leader at 4.0 in and none at 3.75; at 120
            // degrees none to 4.5 out and one from 4.75.
            Assert.Equal(Leader, Lines(0.0, 0.0, -5.75, 0, "6%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 0.0, -5.5, 0, "6%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 0.0, 5.5, 0, "6%%d"));
            Assert.Equal(Leader, Lines(0.0, 0.0, 5.75, 0, "6%%d"));
            Assert.Equal(Leader, Lines(0.0, 50.0, -4.0, 0, "6%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 50.0, -3.75, 0, "6%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 120.0, 4.5, 0, "6%%d"));
            Assert.Equal(Leader, Lines(0.0, 120.0, 4.75, 0, "6%%d"));
        }

        [Fact]
        public void The120DegreeWordsPutRoundAnAngleOf60Degrees_KeepNoLeaderToTheEdgeOfTheirBand()
        {
            // AutoCAD 2021, "120°" (Arial, 6.61 wide) put round an angle of 60 degrees: from 0 degrees at 40 degrees from the vertex a
            // leader at 6.0 in and none at 5.75, at 260 degrees none to 4.75 out and one from 5.0; from 45 degrees at 170 degrees
            // none to 7.0 out and one from 7.25; from 90 degrees at 120 degrees none to 5.75 out and one from 6.0.
            Assert.Equal(Leader, Lines(0.0, 40.0, -6.0, 0, "120%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 40.0, -5.75, 0, "120%%d"));
            Assert.Equal(NoLeader, Lines(0.0, 260.0, 4.75, 0, "120%%d"));
            Assert.Equal(Leader, Lines(0.0, 260.0, 5.0, 0, "120%%d"));
            Assert.Equal(NoLeader, Lines(45.0, 170.0, 7.0, 0, "120%%d"));
            Assert.Equal(Leader, Lines(45.0, 170.0, 7.25, 0, "120%%d"));
            Assert.Equal(NoLeader, Lines(90.0, 120.0, 5.75, 0, "120%%d"));
            Assert.Equal(Leader, Lines(90.0, 120.0, 6.0, 0, "120%%d"));
        }
    }
}
