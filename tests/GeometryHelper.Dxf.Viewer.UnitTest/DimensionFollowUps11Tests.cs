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
    /// Four places where AutoCAD 2021 drew the words of a round or angular dimension another way than the viewer: the leader
    /// of an angle's words on either side of the height of the arc's middle, free words behind the centre of a radius or in
    /// reach of the line of a diameter, jogged words exactly on the rim, and a radius of exactly two arrowheads. Every figure
    /// is a picture made by accoreconsole: the metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625
    /// and Arial, a circle or an angle about the origin.
    /// </summary>
    public class DimensionFollowUps11Tests
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
                ["Dimtmove"] = 0,
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

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static void Near(double x, double y, GeoPoint3 actual)
        {
            Assert.True(
                Math.Abs(actual.X - x) < 1E-3 && Math.Abs(actual.Y - y) < 1E-3,
                $"expected ({x}, {y}), got ({actual.X}, {actual.Y})"
            );
        }

        private static bool Joins(DxfLine l, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1E-3;
            bool forward =
                Math.Abs(l.StartPoint.X - x1) < tolerance
                && Math.Abs(l.StartPoint.Y - y1) < tolerance
                && Math.Abs(l.EndPoint.X - x2) < tolerance
                && Math.Abs(l.EndPoint.Y - y2) < tolerance;
            bool backward =
                Math.Abs(l.StartPoint.X - x2) < tolerance
                && Math.Abs(l.StartPoint.Y - y2) < tolerance
                && Math.Abs(l.EndPoint.X - x1) < tolerance
                && Math.Abs(l.EndPoint.Y - y1) < tolerance;
            return forward || backward;
        }

        private static void HasLine(DxfDimension dimension, double x1, double y1, double x2, double y2) =>
            Assert.Contains(Lines(dimension), l => Joins(l, x1, y1, x2, y2));

        // ---- the leader of an angle's words, either side of the height of the arc's middle ----

        /// <summary>An angle of 60 degrees at the origin, its arc 30 out, its words moved under DIMTMOVE 1 to the point.</summary>
        private static DxfDimension AngleMovedTo(GeoPoint2 at)
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            Run(session, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, at);
            return dimension;
        }

        [Fact]
        public void WordsOfAnAnglePutInsideTheArcJustUnderTheHeightOfItsMiddle_DrawALeaderAndAboveItNone()
        {
            // AutoCAD 2021: "60°" put 19.5 left of the vertex, 24.6 from it (5.4 inside the arc of 30), draws no leader at the
            // height of the arc's middle (15) or above it, and a leader at every height from 14.9 down to 13.5.
            Assert.Equal(2, Lines(AngleMovedTo(P(-19.5, 15.0))).Count);
            Assert.Equal(4, Lines(AngleMovedTo(P(-19.5, 14.9))).Count);
        }

        [Fact]
        public void WordsOfAnAnglePutOutsideTheArcAtTheHeightOfItsMiddle_DrawNoLeaderWhereJustUnderItTheyDo()
        {
            // AutoCAD 2021: the same words put 33.5 left of the vertex (6.7 outside the arc) draw a leader at 14.9 and none at
            // 15.0; the band is 0.2 landings narrower between the height of the vertex and the arc's middle.
            Assert.Equal(2, Lines(AngleMovedTo(P(-33.5, 15.0))).Count);
            Assert.Equal(4, Lines(AngleMovedTo(P(-33.5, 14.9))).Count);
        }

        // ---- words put by a radius or a diameter under DIMTMOVE 2 ----

        /// <summary>A circle about the origin picked at 30 degrees, its dimension with the words put at a point under DIMTMOVE 2, DIMTAD 0, level.</summary>
        private static DxfDimension Free(string command, double radius, GeoPoint2 at)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 2;
                v["Dimtad"] = 0;
            });
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Run(session, command, Polar(30, radius), at);
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        [Fact]
        public void LevelWordsPutBehindTheCentreOfARadius_LeaveItsArrowheadInsideAndItsLineWhole()
        {
            // AutoCAD 2021: "R8" put at (-3.531, -0.884) under DIMTMOVE 2 (a circle of 8 picked at 30 degrees; 3.5 behind the
            // centre along the line, 1 off it), their box taking the line only behind the centre: the arrowhead stays inside, the
            // line whole from the centre to its back. (Before, the viewer pushed the arrowhead out and ran a landing beyond it.)
            DxfDimension dimension = Free("DRA", 8, P(-3.531, -0.884));

            Near(-3.531, -0.884, Words(dimension).Location);
            HasLine(dimension, 0, 0, 4.763, 2.75);
            Assert.Single(Lines(dimension));
        }

        [Fact]
        public void LevelWordsOfADiameterThatTheirBoxCrossesButTheirMiddleIsOffThatBand_BreakTheLineAboutTheMiddleOfWhatTheyTake()
        {
            // AutoCAD 2021: "%%c40" put at (-1.5, 2.598), 3 off the line of a diameter of 40 picked at 30 degrees (the foot of
            // their middle is the circle's centre), which their level box still crosses, breaks the line from -2.0 to 5.88
            // along it: the break's middle is the middle of what the line takes of their box (1.94), not the foot.
            DxfDimension dimension = Free("DDI", 20, P(-1.5, 2.598));

            Near(-1.5, 2.598, Words(dimension).Location);
            HasLine(dimension, -15.155, -8.75, -1.736, -1.002);
            HasLine(dimension, 5.093, 2.94, 15.155, 8.75);
        }

        // ---- jogged words exactly on the rim ----

        /// <summary>A circle of 20 about the origin, its jogged radius with the centre drawn 100 below and the words put the distance out at 30 degrees.</summary>
        private static DxfDimension JoggedAt(double distance)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtoh"] = false;
                v["Dimtih"] = false;
                v["Dimjogang"] = Math.PI / 4.0;
            });
            Run(session, "C", P(0, 0), "20");
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            GeoPoint2 over = P(0, -100);
            GeoPoint2 location = Polar(30, distance);
            Run(
                session,
                "DIMJOGGED",
                new[] { circle },
                over,
                location,
                P((over.X + location.X) / 2.0, (over.Y + location.Y) / 2.0)
            );
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        [Fact]
        public void WordsOfAJoggedRadiusPutExactlyOnTheRim_StayWhereTheyAreAndBreakTheLineOnTheInside()
        {
            // AutoCAD 2021: "R20" put 20 out at 30 degrees, the centre drawn 100 below, counts as inside: the words stay at the
            // point, the line is broken with the box on the inside (gone from 12.8 to 17.5 along it) and drawn from there to the rim.
            DxfDimension dimension = JoggedAt(20);

            Near(17.32, 10, Words(dimension).Location);
            HasLine(dimension, 11.072, 6.392, -50.49, -29.151);
            HasLine(dimension, 15.155, 8.75, 17.32, 10);
        }

        // ---- a radius of exactly two arrowheads ----

        [Fact]
        public void LevelWordsOfARadiusOfExactlyTwoArrowheadsNearlyLevel_StandAboveALandingTheirWidthLong()
        {
            // AutoCAD 2021: "R5" under DIMASZ 2.5 put 2.5 out at 170 degrees: the line has exactly the two arrowheads and no
            // more, which does not keep the words on it (14 of 16 such radii left them off): a landing 4.675 long from the
            // centre, the words above it.
            CadCommandSession session = Probe(v => { });
            Run(session, "C", P(0, 0), "5");
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, "DRA", new[] { circle }, Polar(170, 2.5));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

            Near(2.65, 1.896, Words(dimension).Location);
            HasLine(dimension, 4.675, 0, 0, 0);
        }
    }
}
