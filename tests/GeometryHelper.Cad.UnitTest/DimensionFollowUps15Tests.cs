using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Where AutoCAD 2021 draws the leader of an angle's words put by DIMTEDIT under DIMTMOVE 1 on arcs between 5.5 and 8
    /// landings out, where the band round the arc was a fit and is now the rule found at 240 out with the curve of the arc in
    /// it. Every figure is a picture made by accoreconsole: the metric template with a style of arrowheads and words 2.5 high,
    /// DIMGAP 0.625 and Arial, an angle of 60 degrees at the origin with its arc 45 out, the words put d off the arc.
    /// </summary>
    public class DimensionFollowUps15Tests
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
        private static int LinesWithWordsPut(double degrees, double offTheArc, double arc = 45.0)
        {
            CadCommandSession session = Probe(v => { });
            Run(session, "DAN", Enter, P(0, 0), Polar(0, arc + 20), Polar(60, arc + 20), Polar(30, arc));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        private const int NoLeader = 2;
        private const int Leader = 4;

        [Fact]
        public void WordsPutAboveTheLeftOfAnArcOf45_DrawNoLeaderUpTo5Point25Out()
        {
            // AutoCAD 2021, an angle of 60 degrees with its arc 45 out, the words put at 45 degrees: none up to 5.25 off the arc
            // and a leader from 5.5 (the fit drew one from 4.75, the sum of straight distances from 4.9).
            Assert.Equal(NoLeader, LinesWithWordsPut(45.0, 5.25));
            Assert.Equal(Leader, LinesWithWordsPut(45.0, 5.5));
        }

        [Fact]
        public void WordsPutInsideTheLeftOfAnArcOf45_DrawALeaderFrom5Point75()
        {
            // AutoCAD 2021, the same arc, the words put at 135 degrees: none up to 5.5 inside it and a leader from 5.75 (the
            // fit kept them to 6.0).
            Assert.Equal(NoLeader, LinesWithWordsPut(135.0, -5.5));
            Assert.Equal(Leader, LinesWithWordsPut(135.0, -5.75));
        }

        [Fact]
        public void WordsPutNearlyAboveTheVertexOfAnArcOf45_DrawALeaderBy3Point75Inside()
        {
            // AutoCAD 2021, the same arc, the words put at 95 degrees: none up to 3.25 inside it and a leader from 3.5 (the fit
            // kept them to 4.0).
            Assert.Equal(NoLeader, LinesWithWordsPut(95.0, -3.25));
            Assert.Equal(Leader, LinesWithWordsPut(95.0, -3.75));
        }
    }
}
