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
    /// Level words inside a radius or a diameter under DIMTAD 1, DIMATFIT 3 and DIMTMOVE 0, put at half the radius: AutoCAD 2021
    /// keeps them on their line (a landing an arrowhead long, the words level with it) only where the line has the room, and
    /// else puts them above a landing as long as the arrowhead and the words. Every figure is a picture made by accoreconsole
    /// (the dimensions made by DIMRADIUS and DIMDIAMETER in it): the metric template with a style of arrowheads and words 2.5
    /// high, DIMGAP 0.625 and Arial, a circle about the origin, its dimension put at half the radius out on the line.
    /// </summary>
    public class DimensionFollowUps10Tests
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

        /// <summary>A circle about the origin, then its dimension put at half the radius out at the angle.</summary>
        private static DxfDimension Round(string command, double radius, double degrees)
        {
            CadCommandSession session = Probe(v => { });
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, command, new[] { circle }, Polar(degrees, radius / 2.0));
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        /// <summary>A circle of 8 about the origin picked at 30 degrees under DIMTMOVE 1, its words put at the point.</summary>
        private static DxfDimension RoundMoved(GeoPoint2 at)
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            Run(session, "C", P(0, 0), "8");
            Run(session, "DRA", Polar(30, 8), at);
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        /// <summary>A circle of 20 about the origin, its jogged radius with the centre drawn 100 below and the words put 32 out at 30 degrees.</summary>
        private static DxfDimension Jogged(bool level)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtoh"] = level;
                v["Dimtih"] = level;
                v["Dimjogang"] = Math.PI / 4.0;
            });
            Run(session, "C", P(0, 0), "20");
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            GeoPoint2 over = P(0, -100);
            GeoPoint2 location = Polar(30, 32);
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

        // ---- level words inside, on their line or above a landing ----

        [Fact]
        public void LevelWordsOfARadiusOf8At30Degrees_NeedMoreRoomThanTheLineHasAndStandAboveAWideLanding()
        {
            // AutoCAD 2021: "R8" put 4 out at 30 degrees under DIMATFIT 3: the line has 3 left past two arrowheads, the words
            // reach 4.66 across it, so they leave the line: the landing runs 7.161 left of the line's end (an arrowhead, a gap
            // and the words) and the words stand a gap and half their height above it.
            DxfDimension dimension = Round("DRA", 8, 30);

            Near(-2.545, 3.401, Words(dimension).Location);
            HasLine(dimension, -4.563, 1.5, 2.598, 1.5);
            HasLine(dimension, 2.598, 1.5, 4.763, 2.75);
        }

        [Fact]
        public void LevelWordsOfARadiusOf8At60Degrees_KeepTheirLandingAnArrowheadLongAndStandOnIt()
        {
            // AutoCAD 2021: the same radius steeper, at 60 degrees: the words reach less than 2.6 across the line, which has
            // 3 to spare, so they stay level with a landing an arrowhead long.
            DxfDimension dimension = Round("DRA", 8, 60);

            Near(-3.643, 2.598, Words(dimension).Location);
            HasLine(dimension, 1.5, 2.598, -1, 2.598);
            HasLine(dimension, 2.75, 4.763, 1.5, 2.598);
        }

        [Fact]
        public void LevelWordsOfARadiusOf8At250Degrees_OnALineRunningDown_NeedTheirHeightMoreRoom()
        {
            // AutoCAD 2021: on a line that runs down the words need their height more room along it: at 250 degrees "R8" leaves
            // the line for a landing 7.161 long to the right of its end, the words above it.
            DxfDimension dimension = Round("DRA", 8, 250);

            Near(4.99, 1.481, Words(dimension).Location);
            HasLine(dimension, 7.009, -0.42, -0.153, -0.42);
            HasLine(dimension, -0.153, -0.42, -1.881, -5.168);
        }

        [Fact]
        public void LevelWordsOfADiameterOf8At30Degrees_StandAboveAWideLandingToo()
        {
            // AutoCAD 2021: a diameter takes its whole line as the room: "%%c8" in a diameter of 8 at 30 degrees does not keep to
            // the line, the landing 7.489 long (an arrowhead, a gap and the words) and the words above it.
            DxfDimension dimension = Round("DDI", 4, 30);

            Near(-6.173, 1.471, Words(dimension).Location);
            HasLine(dimension, -8.355, -0.5, -0.866, -0.5);
            HasLine(dimension, -0.866, -0.5, 1.299, 0.75);
        }

        [Fact]
        public void LevelWordsOfADiameterOf8At80Degrees_StayOnTheirLine()
        {
            // AutoCAD 2021: the same diameter at 80 degrees: the words reach only 0.8 across the line and stay on it, the landing
            // an arrowhead long.
            DxfDimension dimension = Round("DDI", 4, 80);

            Near(-5.481, -0.985, Words(dimension).Location);
            HasLine(dimension, -0.174, -0.985, -2.674, -0.985);
            HasLine(dimension, -0.174, -0.985, 0.261, 1.477);
        }

        [Fact]
        public void LevelWordsOfARadiusPutBehindTheCentreUnderDimtmove1_StandAboveALandingTheirWidthLong()
        {
            // AutoCAD 2021: a circle of 8 picked at 30 degrees, "R8" put 2.4 out at -120 degrees under DIMTMOVE 1 and DIMTAD 1:
            // the line runs on through the centre to the height of the point and ends in a landing 4.657 long (a gap and the
            // words), the words above it, as they stand past the rim.
            DxfDimension dimension = RoundMoved(Polar(-120, 2.4));

            Near(-6.243, -0.177, Words(dimension).Location);
            HasLine(dimension, -8.262, -2.079, -3.6, -2.079);
            HasLine(dimension, -3.6, -2.079, 4.763, 2.75);
        }

        [Fact]
        public void LevelWordsOfARadiusPutInFrontOfTheCentreUnderDimtmove1_StayOnTheirLanding()
        {
            // AutoCAD 2021: the same words put 2.4 out at 70 degrees, in front of the centre, keep the landing an arrowhead long
            // at the height of the line's end, 3 out at 30 degrees, and stand level on it.
            DxfDimension dimension = RoundMoved(Polar(70, 2.4));

            Near(-2.545, 1.5, Words(dimension).Location);
            HasLine(dimension, 0.098, 1.5, 2.598, 1.5);
            HasLine(dimension, 2.598, 1.5, 4.763, 2.75);
        }

        // ---- the leader of an angle's words, by the direction they are put in ----

        /// <summary>An angle of 60 degrees at the origin, its arc 30 out, its words moved under DIMTMOVE 1 to the point at the angle.</summary>
        private static DxfDimension AngleMoved(double degrees, double distance)
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            Run(session, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, distance));
            return dimension;
        }

        [Fact]
        public void WordsOfAnAnglePutLevelWithTheVertex_NeedMoreRoomOffTheArcThanThoseAboveIt()
        {
            // AutoCAD 2021: the band without a leader is as wide as the words lean along a level line from the vertex: 70 degrees
            // from it "60°" draws no leader 4.5 off the arc and one 5 off, at 0 degrees none 6.5 off and one 7.25 off.
            DxfDimension near70 = AngleMoved(70, 34.5);
            Assert.Equal(2, Lines(near70).Count);
            Near(11.8, 32.419, Words(near70).Location);

            DxfDimension far70 = AngleMoved(70, 35);
            HasLine(far70, 25.981, 15, 17.555, 32.889);
            HasLine(far70, 17.555, 32.889, 15.055, 32.889);

            Assert.Equal(2, Lines(AngleMoved(0, 36.5)).Count);
            DxfDimension far0 = AngleMoved(0, 37.25);
            HasLine(far0, 25.981, 15, 31.666, 0);
            HasLine(far0, 31.666, 0, 34.166, 0);
        }

        [Fact]
        public void WordsOfAnAnglePutInsideTheArc_DrawTheirLeaderSoonerThanThoseOutside()
        {
            // AutoCAD 2021: the band is narrower inside the arc than outside it: at 70 degrees 3.5 inside it draws no leader and
            // 3.75 inside it does, where outside it takes 4.75 for one.
            Assert.Equal(2, Lines(AngleMoved(70, 26.5)).Count);
            Assert.Equal(4, Lines(AngleMoved(70, 26.25)).Count);
        }

        [Fact]
        public void WordsOfAnAnglePutStraightAboveTheVertex_StandAboveTheirLandingUnderThem()
        {
            // AutoCAD 2021: words exactly above the vertex stand a gap and half their height above the point when they have no
            // leader (36 out: words at 37.901), and with one the leader ends in a landing under them, from a gap past their right
            // end to their left end, the words at the point (36.25 out).
            DxfDimension near = AngleMoved(90, 36);
            Assert.Equal(2, Lines(near).Count);
            Near(0, 37.901, Words(near).Location);

            DxfDimension far = AngleMoved(90, 36.25);
            HasLine(far, 25.981, 15, 3.084, 34.349);
            HasLine(far, 3.084, 34.349, -2.459, 34.349);
            Near(0, 36.25, Words(far).Location);
        }

        // ---- the break about words on a jogged radius that straddle the rim ----

        [Fact]
        public void WordsOfAJoggedRadiusWhoseBoxStraddlesTheRim_TakeTheLineOutOnTheirOwnSideOfTheirPoint()
        {
            // AutoCAD 2021: "R20" of a circle of 20, its centre drawn 100 below, put 32 out at 30 degrees: the words stand
            // 21.795 out along the line, their box (3.75 either way) would run from 18.045 to 25.545 across the rim at 20.
            // The line is gone from 21.795 to 29.295 instead, and drawn from the back of the arrowhead (22.5) to 21.795.
            DxfDimension dimension = Jogged(false);

            Near(12.99, -17.5, Words(dimension).Location);
            HasLine(dimension, 13.411, -18.067, 12.99, -17.5);
            HasLine(dimension, 17.291, -23.294, 41.696, -56.171);
        }

        [Fact]
        public void LevelWordsOfAJoggedRadiusWhoseBoxStraddlesTheRim_TakeTheLineOutOnTheirOwnSideOfTheirPoint()
        {
            // AutoCAD 2021: the same words kept level take less out of the line, their box reaching 2.33 either way along it:
            // gone from 21.795 to 26.465.
            DxfDimension dimension = Jogged(true);

            Near(12.99, -17.5, Words(dimension).Location);
            HasLine(dimension, 13.411, -18.067, 12.99, -17.5);
            HasLine(dimension, 15.813, -21.303, 41.696, -56.171);
        }

        [Fact]
        public void LevelWordsOfARadiusOf3WithinFifteenDegreesOfLevel_StandAboveALandingTheirWidthLong()
        {
            // AutoCAD 2021: "R3" put 1.5 out at 5 degrees has no room to stay on its line, not even level: the words stand above
            // a landing a gap and their width long (4.657), left of the line's end.
            DxfDimension dimension = Round("DRA", 3, 5);

            Near(-4.633, 1.728, Words(dimension).Location);
            HasLine(dimension, -6.649, -0.174, -1.992, -0.174);
            HasLine(dimension, -1.992, -0.174, 0.498, 0.044);
        }

        [Fact]
        public void LevelWordsOfADiameterOf6WithinFifteenDegreesOfLevel_StandAboveALandingTheirWidthLong()
        {
            // AutoCAD 2021: "%%c6" in a diameter of 6 at 190 degrees: the line runs down, the words need two arrowheads and
            // their height along it and the diameter does not have them: the words stand above a landing 4.983 long.
            DxfDimension dimension = Round("DDI", 3, 190);

            Near(7.425, 2.786, Words(dimension).Location);
            HasLine(dimension, 9.604, 0.815, 4.621, 0.815);
            HasLine(dimension, 4.621, 0.815, -0.492, -0.087);
        }
    }
}
