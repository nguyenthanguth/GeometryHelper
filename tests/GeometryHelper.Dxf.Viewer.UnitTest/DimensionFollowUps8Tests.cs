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
    /// Where the fit of a radius or a diameter puts its words and arrowheads when the circle is tight, level words under it,
    /// words pushed out under DIMATFIT 3, level words slid under DIMTMOVE 1, the leader of an angle's words and DIM's angle of an
    /// arc, as AutoCAD 2021 draws them (accoreconsole: the dimensions made by DIMRADIUS, DIMDIAMETER, DIMANGULAR and DIMTEDIT in
    /// it; every figure is its picture). The drawings are the metric template with a style of arrowheads and words 2.5 high,
    /// DIMGAP 0.625 and Arial, a circle about the origin.
    /// </summary>
    public class DimensionFollowUps8Tests
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
                ["Dimtih"] = false,
                ["Dimtoh"] = false,
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

        /// <summary>A circle about the origin, then its dimension: the circle's handle, or a point on the rim at 30 degrees where DIMTMOVE asks.</summary>
        private static DxfDimension Round(CadCommandSession session, string command, double radius, GeoPoint2 at)
        {
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;

            if (session.Document.Database.DimStyleTable["Probe"].Dimtmove != 0)
            {
                Run(session, command, Polar(30, radius), at);
            }
            else
            {
                Run(session, command, new[] { circle }, at);
            }

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

        /// <summary>The arrowheads (the SOLIDs) that stand outside the rim, their backs past it, counted.</summary>
        private static int ArrowsOutside(DxfDimension dimension, double radius)
        {
            int count = 0;

            foreach (DxfSolid solid in dimension.DimBlock.OfType<DxfSolid>().Where(x => !x.IsErased))
            {
                double far = 0.0;

                for (short i = 0; i < 3; i++)
                {
                    GeoPoint3 corner = solid.GetPointAt(i);
                    far = Math.Max(far, Math.Sqrt(corner.X * corner.X + corner.Y * corner.Y));
                }

                if (far > radius + 1.0)
                {
                    count++;
                }
            }

            return count;
        }

        // ---- a tight radius or diameter: the fit by the room the words take along the line ----

        [Fact]
        public void ARadiusOf12UnderFit0_KeepsItsWordsInAndSendsTheArrowheadOut()
        {
            // AutoCAD 2021: "R12" fits in a radius of 12 and a gap each side but not with an arrowhead as well: the arrowhead
            // stands outside on its short line and the words in the middle of the line the arrowhead would have left, 4.114
            // out at 30 degrees; the radius has to be 14.7425 for the arrowhead in.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
            });
            DxfDimension dimension = Round(session, "DRA", 12, Polar(30, 19.2));

            Near(4.114, 2.375, Words(dimension).Location);
            HasLine(dimension, 12.557, 7.25, 14.722, 8.5);
            HasLine(dimension, 0, 0, 0.997, 0.575);
            HasLine(dimension, 10.392, 6, 7.231, 4.175);
        }

        [Fact]
        public void ARadiusOf8UnderFit1_KeepsItsWordsInTheMiddleOfTheRadius()
        {
            // AutoCAD 2021: DIMATFIT 1 moves the arrowhead out before the words, which fit a radius of 8 with a gap each side:
            // the words stand in the middle of the radius, 4 out, and the line is broken about them.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 1;
                v["Dimtad"] = 0;
            });
            DxfDimension dimension = Round(session, "DRA", 8, Polar(30, 12.8));

            Near(3.464, 2, Words(dimension).Location);
            HasLine(dimension, 9.093, 5.25, 11.258, 6.5);
            HasLine(dimension, 0, 0, 1.175, 0.678);
            HasLine(dimension, 6.928, 4, 5.753, 3.322);
            Assert.Equal(1, ArrowsOutside(dimension, 8.0));
        }

        [Fact]
        public void ARadiusOf5UnderFit2_PutOut_RunsItsLineFromTheRimNotFromTheBackOfTheArrowhead()
        {
            // AutoCAD 2021: the words "R5" do not fit, DIMATFIT 2 moves them out and keeps the arrowhead in: the line runs on from
            // the rim, 5 out, to the words, with no line over the arrowhead's length.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 2;
                v["Dimtad"] = 0;
            });
            DxfDimension dimension = Round(session, "DRA", 5, Polar(30, 8));

            Near(8.79, 5.075, Words(dimension).Location);
            HasLine(dimension, 0, 0, 2.165, 1.25);
            HasLine(dimension, 4.33, 2.5, 6.495, 3.75);
            Assert.Equal(0, ArrowsOutside(dimension, 5.0));
        }

        [Fact]
        public void LevelWordsOfARadiusOf3_StandOnALandingTwoArrowheadsPastTheRim()
        {
            // AutoCAD 2021: level "R3" moved out under DIMATFIT 0 stands on a landing from 8 out, the rim and two arrowheads, with
            // no more room for the words before it; the arrowhead's line from 5.5 to 8.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
            });
            DxfDimension dimension = Round(session, "DRA", 3, Polar(30, 4.8));

            Near(12.069, 4, Words(dimension).Location);
            HasLine(dimension, 4.763, 2.75, 6.928, 4);
            HasLine(dimension, 6.928, 4, 9.428, 4);
            HasLine(dimension, 0, 0, 2.598, 1.5);
        }

        [Fact]
        public void LevelWordsOfAnUprightRadiusOf8_FitInAsTheirHeightTakesLessOfTheLine()
        {
            // AutoCAD 2021: level "R8" on an upright line takes only its height along it, so a radius of 8 keeps the words in,
            // 2.75 up, the arrowhead outside; the same words along a level line take their width and go out.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
            });
            DxfDimension dimension = Round(session, "DRA", 8, Polar(90, 12.8));

            Near(0, 2.75, Words(dimension).Location);
            HasLine(dimension, 0, 0, 0, 0.849);
            HasLine(dimension, 0, 8, 0, 4.651);
            HasLine(dimension, 0, 10.5, 0, 13);
        }

        [Fact]
        public void LevelWordsOfADiameterOf10_StandOutAtTheEndOfItsLine()
        {
            // AutoCAD 2021: level "%%c10" does not fit a diameter of 10 at 30 degrees with its arrowheads (it needs 10.4): the
            // arrowheads stand outside, the line whole between them, the words level on a landing past the second.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 0;
                v["Dimtad"] = 0;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
            });
            DxfDimension dimension = Round(session, "DDI", 5, Polar(30, 8));

            HasLine(dimension, -4.33, -2.5, 4.33, 2.5);
            HasLine(dimension, 8.66, 5, 11.16, 5);
            Assert.Equal(2, ArrowsOutside(dimension, 5.0));
        }

        // ---- words pushed out under DIMATFIT 3, level words slid under DIMTMOVE 1 ----

        [Fact]
        public void LevelWordsPushedOutPastTheRimUnderDimtad1_StandOnTheirLandingNotAboveIt()
        {
            // AutoCAD 2021: "R8" put 10.4 out at 30 degrees under DIMATFIT 3, DIMTAD 1 and level words stands pushed out to the
            // landing 13 out, an arrowhead long, the words on it level with it (not above it as past the push).
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 3;
                v["Dimtad"] = 1;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
            });
            DxfDimension dimension = Round(session, "DRA", 8, Polar(30, 10.4));

            Near(16.402, 6.5, Words(dimension).Location);
            HasLine(dimension, 13.758, 6.5, 11.258, 6.5);
            HasLine(dimension, 11.258, 6.5, 9.093, 5.25);
        }

        [Fact]
        public void LevelWordsUnderDimtmove1_SlideToTheHeightOfThePointAndAreRecordedByTheLinesEnd()
        {
            // AutoCAD 2021: a circle of 8 picked at 30 degrees, the point put 1.3 radii out at 150 degrees from that line, level
            // words, DIMTMOVE 1: the line runs on through the centre to the height of the point, -9.007, and the words stand
            // level there, recorded by the end of the line.
            CadCommandSession session = Probe(v =>
            {
                v["Dimatfit"] = 3;
                v["Dimtad"] = 0;
                v["Dimtoh"] = true;
                v["Dimtih"] = true;
                v["Dimtmove"] = 1;
            });
            DxfDimension dimension = Round(session, "DRA", 8, Polar(30 - 150, 10.4));

            Near(-20.743, -9.007, Words(dimension).Location);
            HasLine(dimension, -18.1, -9.007, -15.6, -9.007);
            HasLine(dimension, -15.6, -9.007, 4.763, 2.75);
            Near(-15.6, -9.007, dimension.TextPosition);
            Assert.False(dimension.UsingDefaultTextPosition);
        }

        // ---- the leader of an angle's words ----

        [Fact]
        public void WordsPutOffTheArcOfAnAngleUnderDimtmove1_GetALeaderFrom6Point6()
        {
            // AutoCAD 2021: words 6.5 off the arc of 30 draw no leader, 6.7 off they do (it appears from 6.6 for words "60°" of 2.5
            // with DIMASZ 2.5 and DIMGAP 0.625: the end of the landing, a landing's length before the point, is 1.405 and 0.137
            // heights of the words off the arc as the landing leans).
            CadCommandSession near = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 1;
            });
            Run(near, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension close = near.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(near, "DIMTEDIT", new[] { close.Handle }, Polar(30, 36.5));
            Assert.Equal(2, Lines(close).Count);

            CadCommandSession far = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 1;
            });
            Run(far, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension distant = far.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(far, "DIMTEDIT", new[] { distant.Handle }, Polar(30, 36.7));
            Assert.Equal(4, Lines(distant).Count);
            HasLine(distant, 25.981, 15, 26.199, 18.35);
            HasLine(distant, 26.199, 18.35, 28.699, 18.35);
        }

        [Fact]
        public void WordsOfWiderTextPutOffTheArcOfAnAngle_NeedTheirLeaderFurtherOff()
        {
            // AutoCAD 2021: "60.00°" (DIMADEC 2), 9.77 wide, draws its leader from 8.8 off the arc and not before.
            CadCommandSession near = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 1;
                v["Dimadec"] = 2;
            });
            Run(near, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension close = near.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(near, "DIMTEDIT", new[] { close.Handle }, Polar(30, 38.0));
            Assert.Equal(2, Lines(close).Count);

            CadCommandSession far = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = 1;
                v["Dimadec"] = 2;
            });
            Run(far, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension distant = far.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(far, "DIMTEDIT", new[] { distant.Handle }, Polar(30, 39.0));
            Assert.Equal(4, Lines(distant).Count);
        }

        // ---- DIM's angle of an arc ----

        [Fact]
        public void DimAngularOfAnArc_AsksForItsLocationWithRadiusAndDiameterAmongItsOptions()
        {
            // AutoCAD 2021's DIM: "Specify angular dimension location or [Radius/Diameter/Mtext/Text/text aNgle/Undo]:" after
            // Angular on an arc; Radius and Diameter go back to the radius and diameter prompts of the arc.
            CadCommandSession session = Probe(v => { });
            Run(session, "ARC", P(100, 0), P(120, 20), P(140, 0));

            Run(session, "DIM", P(120, 20));
            Assert.Equal(
                "Specify radius dimension location or [Diameter/Angular/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );

            Run(session, "A");
            Assert.Equal(
                "Specify angular dimension location or [Radius/Diameter/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );

            Run(session, "D");
            Assert.Equal(
                "Specify diameter dimension location or [Radius/Angular/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );

            Run(session, "A", "R");
            Assert.Equal(
                "Specify radius dimension location or [Diameter/Angular/Mtext/Text/text aNgle/Undo]:",
                session.PromptText
            );
        }

        [Fact]
        public void DimAngularOfAnArc_SwitchedBackToRadius_MakesTheRadiusWithTheWordsGivenOnTheWay()
        {
            CadCommandSession session = Probe(v => { });
            Run(session, "ARC", P(100, 0), P(120, 20), P(140, 0));

            Run(session, "DIM", P(120, 20), "A", "T", "KEEP", "R", P(120, 40));
            DxfDimension made = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();

            Assert.IsAssignableFrom<DxfRadialDimension>(made);
            Assert.Equal("KEEP", made.DimensionText);
        }

        [Fact]
        public void DimAngularOfAnArc_GivenAPoint_MakesTheAngle()
        {
            CadCommandSession session = Probe(v => { });
            Run(session, "ARC", P(100, 0), P(120, 20), P(140, 0));

            Run(session, "DIM", P(120, 20), "A", P(120, 40));

            Assert.IsAssignableFrom<DxfPoint3AngularDimension>(
                session.Document.ModelSpace.Block.OfType<DxfDimension>().Last()
            );
        }
    }
}
