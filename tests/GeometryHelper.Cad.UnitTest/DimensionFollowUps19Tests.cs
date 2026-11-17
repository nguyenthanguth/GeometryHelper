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
    /// The leader band of an angle's words put by DIMTEDIT for angles of other sizes, and on arcs of other landings. Every figure
    /// is a picture made by accoreconsole: the metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625 and
    /// Arial, an angle at the origin with its arc 30 out (or the radius given).
    /// </summary>
    public class DimensionFollowUps19Tests
    {
        private static CadCommandSession Probe(double scale = 1.0)
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
                ["Dimscale"] = scale,
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
                ["Dimadec"] = 0,
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
        /// The lines of the picture of an angle of <paramref name="sweep"/> degrees from <paramref name="first"/>, its arc
        /// <paramref name="arc"/> out, its words put by DIMTEDIT <paramref name="offTheArc"/> off the arc at
        /// <paramref name="degrees"/> from the vertex.
        /// </summary>
        private static int Lines(
            double first,
            double sweep,
            double arc,
            double degrees,
            double offTheArc,
            double scale = 1.0
        )
        {
            CadCommandSession session = Probe(scale);
            Run(
                session,
                "DAN",
                Enter,
                P(0, 0),
                Polar(first, arc + 20),
                Polar(first + sweep, arc + 20),
                Polar(first + sweep / 2.0, arc)
            );
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        [Fact]
        public void AnAngleOf90Degrees_IsReadByTheDirectionOfItsMiddleNotItsFirstRay()
        {
            // AutoCAD 2021, "90°" put round an angle of 90 degrees from 45 (its middle at 90) and from 270 (its middle at 315), its
            // arc 30 out: the band is the one of an angle of 60 degrees from 60 and from 285, so that words put at 60 degrees keep
            // no leader to 5.25 out and get one from 5.5 (the band of the first ray at 45 drew one from 5.25), at 80 degrees get
            // one from 3.5 in, and at 240 degrees from the second angle keep none to 5.25 out and get one from 5.5.
            Assert.Equal(NoLeader, Lines(45.0, 90.0, 30.0, 60.0, 5.25));
            Assert.Equal(Leader, Lines(45.0, 90.0, 30.0, 60.0, 5.5));
            Assert.Equal(Leader, Lines(45.0, 90.0, 30.0, 80.0, -3.5));
            Assert.Equal(NoLeader, Lines(45.0, 90.0, 30.0, 80.0, -3.25));
            Assert.Equal(NoLeader, Lines(270.0, 90.0, 30.0, 240.0, 5.25));
            Assert.Equal(Leader, Lines(270.0, 90.0, 30.0, 240.0, 5.5));
        }

        [Fact]
        public void WordsPutRoundAnArcOf8AndAHalf_KeepNoLeaderToTheEdgeOfTheBandFittedAcrossLandings()
        {
            // AutoCAD 2021, an angle of 60 degrees from 0 with its arc 8.5 out (1.5 landings of Arial's 5.58): words put at 40
            // degrees keep no leader to 6.0 out and get one from 6.25, at 130 degrees none to 5.5 and one from 5.75, at 280
            // degrees none to 5.25 and one from 5.5, and at 190 degrees a leader at 2.75 in and none at 2.5 (the band fitted to
            // the arcs of SHX's landing alone drew none at 6.25, 5.75 and 5.5 out, and one at 2.5 in).
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 8.5, 40.0, 6.0));
            Assert.Equal(Leader, Lines(0.0, 60.0, 8.5, 40.0, 6.25));
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 8.5, 130.0, 5.5));
            Assert.Equal(Leader, Lines(0.0, 60.0, 8.5, 130.0, 5.75));
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 8.5, 280.0, 5.25));
            Assert.Equal(Leader, Lines(0.0, 60.0, 8.5, 280.0, 5.5));
            Assert.Equal(Leader, Lines(0.0, 60.0, 8.5, 190.0, -2.75));
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 8.5, 190.0, -2.5));
        }

        [Fact]
        public void WordsOfDimscale2PutRoundAnArcOf17_KeepNoLeaderToTheEdgeOfTheBandFittedAcrossLandings()
        {
            // AutoCAD 2021, DIMSCALE 2 (a landing of 11.2, the arc 17 out 1.5 landings): words put at 40 degrees keep no leader
            // to 12.0 out and get one from 12.5, at 130 degrees none to 11.0 and one from 11.5, at 80 degrees none to 8.5 in
            // and one from 8.0.
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 17.0, 40.0, 12.0, 2.0));
            Assert.Equal(Leader, Lines(0.0, 60.0, 17.0, 40.0, 12.5, 2.0));
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 17.0, 130.0, 11.0, 2.0));
            Assert.Equal(Leader, Lines(0.0, 60.0, 17.0, 130.0, 11.5, 2.0));
            Assert.Equal(NoLeader, Lines(0.0, 60.0, 17.0, 80.0, -8.5, 2.0));
            Assert.Equal(Leader, Lines(0.0, 60.0, 17.0, 80.0, -8.0, 2.0));
        }

        [Fact]
        public void AnAngleOf30Degrees_IsReadByTheDirectionOfItsMiddleNotItsFirstRay()
        {
            // AutoCAD 2021, "30°" put round an angle of 30 degrees from 45 (its middle at 60) and from 180 (its middle at 195),
            // its arc 30 out: the band is the one of an angle of 60 degrees from 30 and from 165. At 60 degrees none to 5.25 out
            // and a leader from 5.5; at 300 degrees a leader at 4.25 in and none at 4.0; from 180, at 220 degrees a leader at
            // 5.25 in and none at 5.0.
            Assert.Equal(NoLeader, Lines(45.0, 30.0, 30.0, 60.0, 5.25));
            Assert.Equal(Leader, Lines(45.0, 30.0, 30.0, 60.0, 5.5));
            Assert.Equal(Leader, Lines(45.0, 30.0, 30.0, 300.0, -4.25));
            Assert.Equal(NoLeader, Lines(45.0, 30.0, 30.0, 300.0, -4.0));
            Assert.Equal(Leader, Lines(180.0, 30.0, 30.0, 220.0, -5.25));
            Assert.Equal(NoLeader, Lines(180.0, 30.0, 30.0, 220.0, -5.0));
        }
    }
}
