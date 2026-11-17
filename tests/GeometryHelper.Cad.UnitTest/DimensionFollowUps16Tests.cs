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
    /// Where AutoCAD 2021 draws the leader of an angle's words put by DIMTEDIT under DIMTMOVE 1 on an arc only 5 landings out
    /// (the rule found at 240 out, with the curve of the arc in it and a correction by direction of the sagitta of a landing,
    /// now reads every arc), and when level words inside a radius leave their line for a wide landing. Every figure is a picture
    /// made by accoreconsole: the metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial.
    /// </summary>
    public class DimensionFollowUps16Tests
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

        /// <summary>The lines of an angle's picture: its two extension lines, and with a leader two more.</summary>
        private static int LinesWithWordsPut(
            double degrees,
            double offTheArc,
            Action<SortedDictionary<string, object>> set = null
        )
        {
            const double arc = 30.0;
            CadCommandSession session = Probe(v => set?.Invoke(v));
            Run(session, "DAN", Enter, P(0, 0), Polar(0, arc + 20), Polar(60, arc + 20), Polar(30, arc));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        private const int NoLeader = 2;
        private const int Leader = 4;

        [Fact]
        public void WordsPutAboveTheLeftOfAnArcOf30_DrawALeaderFrom5Point5Out()
        {
            // AutoCAD 2021, an angle of 60 degrees with its arc 30 out (5 landings), the words put at 45 degrees: none up to
            // 5.25 off the arc and a leader from 5.5 (the fit drew none up to 5.75).
            Assert.Equal(NoLeader, LinesWithWordsPut(45.0, 5.25));
            Assert.Equal(Leader, LinesWithWordsPut(45.0, 5.75));
        }

        [Fact]
        public void WordsPutInsideTheLeftOfAnArcOf30_KeepNoLeaderTo5Point25()
        {
            // AutoCAD 2021, the same arc, the words put at 135 degrees: none up to 5.25 inside it and a leader from 5.5 (the
            // fit drew one from 5.25).
            Assert.Equal(NoLeader, LinesWithWordsPut(135.0, -5.25));
            Assert.Equal(Leader, LinesWithWordsPut(135.0, -5.5));
        }

        [Fact]
        public void BiggerWordsPutInsideTheLeftOfAnArcOf30_KeepNoLeaderTo6Point25()
        {
            // AutoCAD 2021, the same arc with words 4 high (a landing of 7.5, 4 landings out), put at 135 degrees: none up to
            // 6.25 inside it (the fit drew a leader from 5.75), and put level with the vertex, at 0 degrees, none up to 8.0
            // inside it (the fit drew one from 7.75).
            Assert.Equal(NoLeader, LinesWithWordsPut(135.0, -6.25, v => v["Dimtxt"] = 4.0));
            Assert.Equal(NoLeader, LinesWithWordsPut(0.0, -8.0, v => v["Dimtxt"] = 4.0));
        }

        /// <summary>Whether the words of a radius picked at an angle stand above a wide landing, not level with their line.</summary>
        private static bool StandAboveALanding(
            string command,
            double radius,
            double degrees,
            Action<SortedDictionary<string, object>> set
        )
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 0;
                set(v);
            });
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, command, new[] { circle }, Polar(degrees, radius / 2.0));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            double wordsY = dimension.DimBlock.OfType<DxfMText>().Single().Location.Y;
            double nearest = dimension
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1E-6)
                .Min(l => Math.Abs(wordsY - l.StartPoint.Y));
            return nearest > 0.5;
        }

        [Fact]
        public void LevelWordsOfARadiusOf12Point4At320Degrees_KeepToTheirLine()
        {
            // AutoCAD 2021: "R12" on a radius that runs down at 320 degrees stands level with a landing an arrowhead long from a
            // radius of 12.2 on, and above a wide landing below that (the fit asked for 12.5).
            Assert.False(StandAboveALanding("DRA", 12.4, 320.0, v => { }));
            Assert.True(StandAboveALanding("DRA", 11.5, 320.0, v => { }));
        }

        [Fact]
        public void LevelWordsOfTallWordsOnARadiusOf12Point6At300Degrees_KeepToTheirLine()
        {
            // AutoCAD 2021: words 3.5 high ("R13", 8.33 wide) at 300 degrees keep their line from a radius of 12.2 (the fit asked
            // for 13.0).
            Assert.False(StandAboveALanding("DRA", 12.6, 300.0, v => v["Dimtxt"] = 3.5));
            Assert.True(StandAboveALanding("DRA", 11.8, 300.0, v => v["Dimtxt"] = 3.5));
        }

        [Fact]
        public void LevelWordsOfARadiusOf8At50Degrees_LeaveTheirLine()
        {
            // AutoCAD 2021: "R8" (4.04 wide) on a radius of 8 at 50 degrees stands above a wide landing (the fit kept it level
            // on the line, which asked 8.0 of it; it takes 8.17).
            Assert.True(StandAboveALanding("DRA", 8.0, 50.0, v => { }));
        }

        [Fact]
        public void LevelWordsOfBigWordsOnADiameterOf16At210Degrees_KeepToTheirLine()
        {
            // AutoCAD 2021: "%%c8" with words 3.5 high (8.8 wide) on a diameter of 16 that runs down at 210 degrees stands level
            // on a landing an arrowhead long (the fit asked 16.7 of the line, and put the words above a wide landing).
            Assert.False(StandAboveALanding("DDI", 8.0, 210.0, v => v["Dimtxt"] = 3.5));
        }

        [Fact]
        public void LevelWordsOfARadiusOf6At80Degrees_KeepToTheirLine()
        {
            // AutoCAD 2021: "R6" (4.03 wide) on a radius of 6 at 80 degrees, nearly upright, stands level on a landing an
            // arrowhead long, as at 90 degrees from a radius of 5 on.
            Assert.False(StandAboveALanding("DRA", 6.0, 80.0, v => { }));
            Assert.False(StandAboveALanding("DRA", 5.0, 90.0, v => { }));
        }
    }
}
