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
    /// How a radius or a diameter is written, as AutoCAD 2021 writes it: with its words flagged as put by hand (group 70 of 164
    /// and 163, flag 128) where they stand at the point given, and flagged as at their default place (36 and 35) where the
    /// style's fit or the room next to the rim put them, the leader's length (group 40) nought in both. The drawings are blank
    /// ones with acad.dwt's Standard, its text style Arial, DIMTAD 1, DIMTOH and DIMTIH off, as accoreconsole made them, a
    /// circle of 20 about the origin.
    /// </summary>
    public class DimensionFollowUps6Tests
    {
        private static CadCommandSession Standard(int fit, int move, bool tix = false, int tad = 1, bool level = false)
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable["Standard"].FileName = "arial.ttf";
            StandardCurrent(document.Database);
            document.Database.Dimtad = tad;
            document.Database.Dimtoh = level;
            document.Database.Dimtih = level;
            document.Database.Dimatfit = fit;
            document.Database.Dimtmove = move;
            document.Database.Dimtix = tix;
            return Session(document);
        }

        private static DxfDimension Round(CadCommandSession session, string command, double x, double y)
        {
            Run(session, "C", P(0, 0), "20");
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            if (session.Document.Database.Dimtmove == 1)
            {
                Run(session, command, P(20, 0), P(x, y));
            }
            else
            {
                Run(session, command, new[] { circle }, P(x, y));
            }

            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        private static double LeaderOf(DxfDimension dimension) =>
            dimension is DxfRadialDimension radial
                ? radial.LeaderLength
                : ((DxfDiametricDimension)dimension).LeaderLength;

        private static void Near(double x, double y, GeoPoint3 actual)
        {
            Assert.True(
                Math.Abs(actual.X - x) < 1E-3 && Math.Abs(actual.Y - y) < 1E-3,
                $"expected ({x}, {y}), got ({actual.X}, {actual.Y})"
            );
        }

        [Theory]
        [InlineData("DRA", 3, 0, 35, 12, false, 35, 12)]
        [InlineData("DRA", 3, 0, 10, 5, false, 10, 5)]
        [InlineData("DRA", 3, 0, 21.5, 0.1, false, 21.5, 0.1)]
        [InlineData("DRA", 3, 0, 20.3, 0.1, true, 20.9782, 0.2852)]
        [InlineData("DRA", 0, 0, 35, 12, true, 9.3153, 3.3861)]
        [InlineData("DRA", 0, 0, 10, 5, true, 8.7824, 4.5946)]
        [InlineData("DRA", 0, 1, 35, 12, false, 35, 0)]
        [InlineData("DRA", 0, 1, 20.3, 0.1, true, 20.9794, 0.1819)]
        [InlineData("DRA", 0, 2, 35, 12, false, 35, 12)]
        [InlineData("DRA", 0, 2, 20.3, 0.1, false, 20.3, 0.1)]
        [InlineData("DDI", 3, 0, 35, 12, false, 35, 12)]
        [InlineData("DDI", 3, 0, 20.3, 0.1, true, 20.99, 0.2903)]
        [InlineData("DDI", 0, 0, 35, 12, true, -0.0606, 0.1768)]
        [InlineData("DDI", 0, 1, 35, 12, false, 35, 0)]
        [InlineData("DDI", 0, 2, 35, 12, false, 35, 12)]
        public void ARadiusOrDiameter_IsWrittenAsAutoCadWritesIt(
            string command,
            int fit,
            int move,
            double x,
            double y,
            bool atDefaultPlace,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021 (probe GhD6): DIMRADIUS and DIMDIAMETER write 164 and 163, flag 128 set, where the words stand at the
            // point (DIMATFIT 3, or DIMTMOVE 1 and 2 whatever the fit, the point not within the room of an arrowhead of the rim)
            // and 36 and 35 where the fit or that room put them; group 40 is nought either way and group 11 the middle of the
            // words as recorded: "R20" put at (35, 12) under DIMATFIT 3 writes 164 and (35, 12), put at (20.3, 0.1), within
            // the room, 36 and (20.9782, 0.2852), and under DIMATFIT 0 it writes 36 and (9.3153, 3.3861) whatever the distance it was put at in that direction.
            CadCommandSession session = Standard(fit, move);
            DxfDimension dimension = Round(session, command, x, y);

            Assert.Equal(atDefaultPlace, dimension.UsingDefaultTextPosition);
            Assert.Equal(0.0, LeaderOf(dimension), 9);
            Near(wordsX, wordsY, dimension.TextPosition);
        }

        [Theory]
        [InlineData("DRA", 3, 0)]
        [InlineData("DRA", 0, 1)]
        [InlineData("DRA", 0, 2)]
        [InlineData("DDI", 3, 0)]
        public void WordsPutOutsideUnderDimtix_AreAtTheirDefaultPlace(string command, int fit, int move)
        {
            // AutoCAD 2021 (probe GhD6): DIMTIX puts the words outside, next to the rim, wherever the point was, and writes
            // 36 or 35: the point decided nothing.
            CadCommandSession session = Standard(fit, move, true);
            DxfDimension dimension = Round(session, command, 35, 12);

            Assert.True(dimension.UsingDefaultTextPosition);
            Assert.Equal(0.0, LeaderOf(dimension), 9);
        }

        [Fact]
        public void WordsMovedByDimtedit_KeepALeaderOfNought()
        {
            // AutoCAD 2021 (probe GhD6): DIMTEDIT to a point leaves a radius at 164 with group 40 nought.
            CadCommandSession session = Standard(3, 0);
            DxfDimension radius = Round(session, "DRA", 35, 12);
            Run(session, "DIMTEDIT", new[] { radius.Handle }, P(40, 5));

            Assert.False(radius.UsingDefaultTextPosition);
            Assert.Equal(0.0, LeaderOf(radius), 9);
        }

        [Fact]
        public void TheHomeOfWordsMovedByHand_PutsThemWhereDimradiusAtTheRimWould()
        {
            // AutoCAD 2021 (probe GhD6): DIMTEDIT's Home on a radius writes 36 with group 40 nought and stands the words as
            // DIMRADIUS does for a point on the rim: next to it, no nearer than the room of an arrowhead.
            CadCommandSession session = Standard(3, 0);
            DxfDimension radius = Round(session, "DRA", 35, 12);
            Run(session, "DIMTEDIT", new[] { radius.Handle }, P(40, 5));
            Run(session, "DIMTEDIT", new[] { radius.Handle }, "H");

            Assert.True(radius.UsingDefaultTextPosition);
            Assert.Equal(0.0, LeaderOf(radius), 9);

            GeoPoint3 chord = radius.ChordPointOf();
            CadCommandSession other = Standard(3, 0);
            DxfDimension rim = Round(other, "DRA", chord.X, chord.Y);
            DxfMText homed = radius.DimBlock.OfType<DxfMText>().Single();
            DxfMText fresh = rim.DimBlock.OfType<DxfMText>().Single();
            Near(fresh.Location.X, fresh.Location.Y, homed.Location);
        }

        private static void HasLine(DxfDimension dimension, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1E-3;
            List<DxfLine> lines = dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();
            bool found = lines.Any(l =>
                (
                    Math.Abs(l.StartPoint.X - x1) < tolerance
                    && Math.Abs(l.StartPoint.Y - y1) < tolerance
                    && Math.Abs(l.EndPoint.X - x2) < tolerance
                    && Math.Abs(l.EndPoint.Y - y2) < tolerance
                )
                || (
                    Math.Abs(l.StartPoint.X - x2) < tolerance
                    && Math.Abs(l.StartPoint.Y - y2) < tolerance
                    && Math.Abs(l.EndPoint.X - x1) < tolerance
                    && Math.Abs(l.EndPoint.Y - y1) < tolerance
                )
            );
            Assert.True(
                found,
                "no line from ("
                    + x1
                    + ", "
                    + y1
                    + ") to ("
                    + x2
                    + ", "
                    + y2
                    + ") among "
                    + string.Join(
                        " ",
                        lines.Select(l =>
                            "("
                            + l.StartPoint.X.ToString("0.####")
                            + ", "
                            + l.StartPoint.Y.ToString("0.####")
                            + ")-("
                            + l.EndPoint.X.ToString("0.####")
                            + ", "
                            + l.EndPoint.Y.ToString("0.####")
                            + ")"
                        )
                    )
            );
        }

        [Theory]
        [InlineData(1, -19.6, -19.4562)]
        [InlineData(4, -19.6, -19.4562)]
        [InlineData(0, -19.6, -19.6)]
        [InlineData(1, 19.6, 19.6)]
        public void LevelWordsInsideALineRunningDown_KeepThreeArrowheadsOfRoomUnderDimtad1And4(
            int tad,
            double y,
            double landingY
        )
        {
            // AutoCAD 2021 (probe GhD6): "R20" of a circle of 20 about the origin put 19.6 out straight down, its words kept level,
            // has its landing at -19.4562 (20 less three arrowheads of 0.18) under DIMTAD 1, not at the point as under DIMTAD 0
            // (-19.6) or for the same point straight up (19.6).
            CadCommandSession session = Standard(3, 0, false, tad, true);
            DxfDimension radius = Round(session, "DRA", 0, y);

            double side = y < 0 ? 0.18 : -0.18;
            HasLine(radius, side, landingY, 0, landingY);
        }

        [Fact]
        public void LevelWordsOutsideALineRunningDownUnderDimtad1_KeepThreeArrowheadsOfRoom()
        {
            // AutoCAD 2021 (probe GhD6): "R20" put 20.3 out straight down, level, DIMTAD 1, has its landing at -20.5438 (20 and
            // three arrowheads), from (-1.1488, -20.5438) to (0, -20.5438), its words' middle at (-0.6194, -20.3619).
            CadCommandSession session = Standard(3, 0, false, 1, true);
            DxfDimension radius = Round(session, "DRA", 0, -20.3);

            HasLine(radius, -1.1488, -20.5438, 0, -20.5438);
            Near(-0.6194, -20.3619, radius.DimBlock.OfType<DxfMText>().Single().Location);
        }

        [Fact]
        public void LevelWordsInsideALineWithin15DegreesOfLevel_HaveNoLanding_AndStandPastTheBend()
        {
            // AutoCAD 2021 (probe GhD6): "R20" of a circle of 20 about the origin put 14 out at 3 degrees, its words kept level
            // on the line (DIMTAD 0), has its line from the back of the arrowhead (19.7928, 1.0373) to the point itself
            // (13.9808, 0.7327), which is also group 11, and its words' middle (13.3614, 0.7327) a gap and half their width past
            // it: no landing.
            CadCommandSession session = Standard(3, 0, false, 0, true);
            DxfDimension radius = Round(session, "DRA", 13.9808, 0.7327);

            HasLine(radius, 13.9808, 0.7327, 19.7928, 1.0373);
            Near(13.3614, 0.7327, radius.DimBlock.OfType<DxfMText>().Single().Location);
            Near(13.9808, 0.7327, radius.TextPosition);
        }

        [Fact]
        public void ADiameterPutInsideUnderDimtmove1_StandsOnItsLineWithOneArrowhead_AsARadiusDoes()
        {
            // AutoCAD 2021 (probe GhD6): "%%c40" of a circle of 20, picked at (20, 0) and put at (10, 0) under DIMTMOVE 1 and
            // DIMATFIT 0, DIMTAD 1, has its line from (9.3688, 0) to the back of the one arrowhead (19.82, 0), its words' middle
            // (10, 0.1869), and is written as one put by hand (163), group 11 (10, 0).
            CadCommandSession session = Standard(0, 1);
            Run(session, "C", P(0, 0), "20");
            Run(session, "DDI", P(20, 0), P(10, 0));
            DxfDimension diameter = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

            HasLine(diameter, 9.3688, 0, 19.82, 0);
            Near(10, 0.1869, diameter.DimBlock.OfType<DxfMText>().Single().Location);
            Assert.Single(diameter.DimBlock.OfType<DxfSolid>(), s => !s.IsErased);
            Assert.False(diameter.UsingDefaultTextPosition);
            Near(10, 0, diameter.TextPosition);
        }
    }

    internal static class RadialExtensions
    {
        /// <summary>The point of the rim a radius runs to.</summary>
        public static GeoPoint3 ChordPointOf(this DxfDimension dimension) =>
            dimension is DxfRadialDimension radial ? radial.ChordPoint : ((DxfDiametricDimension)dimension).ChordPoint;
    }
}
