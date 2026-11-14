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
    /// Where AutoCAD 2021 draws the leader of an angle's words put by DIMTEDIT under DIMTMOVE 1 when DIMEXE, DIMASZ or DIMSCALE
    /// is not the 1.25, 2.5 and 1 the band round the arc was fitted with. Every figure is a picture made by accoreconsole: the
    /// metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial, an angle of 60 degrees at the
    /// origin with its arc 30 out, the words put d off the arc.
    /// </summary>
    public class DimensionFollowUps14Tests
    {
        private static CadCommandSession Probe(Action<SortedDictionary<string, object>> set)
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
                ["Dimtih"] = true,
                ["Dimtoh"] = true,
                ["Dimtofl"] = true,
                ["Dimatfit"] = 3,
                ["Dimcen"] = 2.5,
                ["Dimjust"] = 0,
                ["Dimtix"] = false,
                ["Dimsoxd"] = false,
                ["Dimtmove"] = 1,
                ["Dimtvp"] = 0.0,
                ["Dimadec"] = 0,
                ["Dimazin"] = 0,
                ["Dimtxsty"] = "Probe",
            };
            set(values);
            CadDimStyleEditor.Add(document, "Probe", start, values);
            CadDimStyleEditor.SetCurrent(document, "Probe");
            return Session(document);
        }

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

        /// <summary>The lines of the angle's picture: its two extension lines, and with a leader two more.</summary>
        private static int LinesWithWordsPut(
            Action<SortedDictionary<string, object>> style,
            double degrees,
            double offTheArc,
            double arc = 30.0
        )
        {
            CadCommandSession session = Probe(style);
            Run(session, "DAN", Enter, P(0, 0), Polar(0, arc + 20), Polar(60, arc + 20), Polar(30, arc));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        private const int NoLeader = 2;
        private const int Leader = 4;

        private static void MoreExtension(SortedDictionary<string, object> v) => v["Dimexe"] = 2.5;

        // ---- DIMEXE ----

        [Fact]
        public void WordsPut7Point75BesideTheVertexUnderDimexe2Point5_DrawNoLeader()
        {
            // AutoCAD 2021: with DIMEXE 2.5 the words put at 0 degrees draw no leader up to 7.75 off the arc and one from 8.0
            // (DIMEXE 1.25: up to 6.5); before, the viewer drew a leader at 7.75.
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 0.0, 7.75));
            Assert.Equal(Leader, LinesWithWordsPut(MoreExtension, 0.0, 8.0));
        }

        [Fact]
        public void WordsPutAlmostAboveTheVertexUnderDimexe2Point5_DrawNoLeaderFurtherOffTheArc()
        {
            // AutoCAD 2021: at 85 degrees the words draw none up to 6.0 out and 5.0 in (DIMEXE 1.25: 4.0 and 3.0), a leader from
            // 6.25 out.
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 85.0, 6.0));
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 85.0, -5.0));
            Assert.Equal(Leader, LinesWithWordsPut(MoreExtension, 85.0, 6.5));
        }

        [Fact]
        public void WordsPutExactlyAboveTheVertexUnderDimexe2Point5_DrawNoLeaderFurtherOffTheArc()
        {
            // AutoCAD 2021: exactly above the vertex they draw none up to 7.5 out and 6.75 in, a leader from 7.75 out and 7.0 in
            // (DIMEXE 1.25: 6.0 and 5.5).
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 90.0, 7.5));
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 90.0, -6.75));
            Assert.Equal(Leader, LinesWithWordsPut(MoreExtension, 90.0, 7.75));
        }

        // ---- DIMASZ ----

        [Fact]
        public void WordsPutExactlyAboveTheVertexUnderDimasz4_DrawNoLeaderUpTo7Point75Out()
        {
            // AutoCAD 2021: arrowheads 4 long draw none up to 7.75 out and 7.25 in, and a leader from 8.0 out (arrowheads 2.5:
            // 6.0 and 5.5); before, the viewer drew a leader at 7.75.
            Assert.Equal(NoLeader, LinesWithWordsPut(v => v["Dimasz"] = 4.0, 90.0, 7.75));
            Assert.Equal(Leader, LinesWithWordsPut(v => v["Dimasz"] = 4.0, 90.0, 8.0));
        }

        // ---- DIMSCALE ----

        [Fact]
        public void WordsPutBesideTheVertexUnderDimscaleOneHalf_DrawALeaderFrom3Point5()
        {
            // AutoCAD 2021: at DIMSCALE 0.5 (arrowheads, gap, words and DIMEXE half as long) the words put at 0 degrees draw
            // none up to 3.25 off the arc, in or out, and a leader from 3.5; before, the viewer drew none at 3.5.
            Assert.Equal(NoLeader, LinesWithWordsPut(v => v["Dimscale"] = 0.5, 0.0, 3.25));
            Assert.Equal(Leader, LinesWithWordsPut(v => v["Dimscale"] = 0.5, 0.0, 3.5));
            Assert.Equal(Leader, LinesWithWordsPut(v => v["Dimscale"] = 0.5, 0.0, -3.5));
        }

        // ---- a large arc ----

        [Fact]
        public void WordsPutOnTheLeftOfAnArcOf240_DrawNoLeaderUp5Point25Inside()
        {
            // AutoCAD 2021, an angle of 60 degrees with its arc 240 out, the words put at 120 degrees: none up to 5.25 off the
            // arc, inside or out, a leader from 5.5 (before, the viewer drew one from 4.25 inside), and at 210 degrees 6.5
            // inside (before 5.75), the leader from 6.75.
            Assert.Equal(NoLeader, LinesWithWordsPut(v => { }, 120.0, -5.25, 240.0));
            Assert.Equal(NoLeader, LinesWithWordsPut(v => { }, 120.0, 5.25, 240.0));
            Assert.Equal(Leader, LinesWithWordsPut(v => { }, 120.0, -5.5, 240.0));
            Assert.Equal(NoLeader, LinesWithWordsPut(v => { }, 210.0, -6.5, 240.0));
            Assert.Equal(Leader, LinesWithWordsPut(v => { }, 210.0, -6.75, 240.0));
        }

        [Fact]
        public void WordsPutAbovePointsOfTheArcOf240_DrawALeaderFromTheEdgeOfTheSumOfDistances()
        {
            // AutoCAD 2021, the same arc: at 60 degrees none up to 4.0 off the arc and a leader from 4.25 (before, none to 5.0),
            // at 40 degrees up to 5.0 and a leader from 5.25 (before, 6.0).
            Assert.Equal(NoLeader, LinesWithWordsPut(v => { }, 60.0, 4.0, 240.0));
            Assert.Equal(Leader, LinesWithWordsPut(v => { }, 60.0, 4.25, 240.0));
            Assert.Equal(NoLeader, LinesWithWordsPut(v => { }, 40.0, 5.0, 240.0));
            Assert.Equal(Leader, LinesWithWordsPut(v => { }, 40.0, 5.25, 240.0));
        }

        [Fact]
        public void WordsPutOnAnArcOf240UnderDimexe2Point5_DrawNoLeaderAsFarAsDimexeLets()
        {
            // AutoCAD 2021: with DIMEXE 2.5 the words put at 135 degrees on the arc of 240 draw no leader up to 7.25 off it
            // (6.0 with DIMEXE 1.25), a leader from 7.5.
            Assert.Equal(NoLeader, LinesWithWordsPut(MoreExtension, 135.0, 7.25, 240.0));
            Assert.Equal(Leader, LinesWithWordsPut(MoreExtension, 135.0, 7.5, 240.0));
        }

        [Fact]
        public void WordsPutAlmostAboveTheVertexUnderDimscale2_DrawNoLeader5Point5Inside()
        {
            // AutoCAD 2021: at DIMSCALE 2 the words put at 85 degrees draw none up to 5.5 inside the arc and a leader from 5.75
            // (before, the viewer drew a leader at 5.5 in).
            Assert.Equal(NoLeader, LinesWithWordsPut(v => v["Dimscale"] = 2.0, 85.0, -5.5));
            Assert.Equal(Leader, LinesWithWordsPut(v => v["Dimscale"] = 2.0, 85.0, -6.5));
        }
    }
}
