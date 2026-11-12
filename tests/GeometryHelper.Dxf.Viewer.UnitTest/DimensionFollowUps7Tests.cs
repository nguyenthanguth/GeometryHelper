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
    /// Radii and diameters under DIMTIX, DIMTEDIT of their words, and the pictures of linear, ordinate and angular dimensions
    /// whose words were moved, as AutoCAD 2021 draws them (accoreconsole: the dimensions made by DIMRADIUS, DIMDIAMETER,
    /// DIMLINEAR, DIMORDINATE and DIMANGULAR and moved by DIMTEDIT in it; every figure is its picture). The drawings are the
    /// metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial, a circle of 20 about the origin.
    /// </summary>
    public class DimensionFollowUps7Tests
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

        private static void HasNoLine(DxfDimension dimension, double x1, double y1, double x2, double y2) =>
            Assert.DoesNotContain(Lines(dimension), l => Joins(l, x1, y1, x2, y2));

        private static void Move(CadCommandSession session, DxfDimension dimension, GeoPoint2 to) =>
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, to);

        // ---- DIMTIX: the picture of the words next to the rim ----

        [Theory]
        [InlineData("DRA")]
        [InlineData("DDI")]
        public void WordsPutFarOutUnderDimtixAndFit3_StandNextToTheRimNotAtThePoint(string command)
        {
            // AutoCAD 2021: "R20" (and "%%c40") put 40 out at 30 degrees under DIMTIX and DIMATFIT 3 stands 28.6 out, past the
            // arrowhead's two lengths and the gap and half the words; the point decided nothing and the dimension is a 36.
            CadCommandSession session = Probe(v => v["Dimtix"] = true);
            bool diameter = command == "DDI";
            DxfDimension dimension = Round(session, command, 20, Polar(30, 40));

            Assert.True(dimension.UsingDefaultTextPosition);
            Near(diameter ? 23.9310 : 23.8242, diameter ? 16.0929 : 15.9505, Words(dimension).Location);
            HasLine(dimension, diameter ? 28.1827 : 27.8992, diameter ? 16.2713 : 16.1076, 19.4856, 11.25);
            HasLine(dimension, diameter ? -17.3205 : 0, diameter ? -10 : 0, 17.3205, 10);

            if (diameter)
            {
                HasLine(dimension, -19.4856, -11.25, -21.6506, -12.5);
            }
        }

        [Fact]
        public void ADiameterUnderDimtixWithDimtoflOff_DrawsNoFarArrowheadAndMarksTheCentre()
        {
            // AutoCAD 2021: the far arrowhead and its short line stay out, the centre mark of DIMCEN stands in the circle.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimtofl"] = false;
            });
            DxfDimension dimension = Round(session, "DDI", 20, Polar(30, 40));

            HasLine(dimension, 28.1827, 16.2713, 19.4856, 11.25);
            HasLine(dimension, -2.5, 0, 2.5, 0);
            HasLine(dimension, 0, -2.5, 0, 2.5);
            HasNoLine(dimension, -19.4856, -11.25, -21.6506, -12.5);
            HasNoLine(dimension, -17.3205, -10, 17.3205, 10);
        }

        [Fact]
        public void LevelWordsUnderDimtixAndFit3_StandOnTheirLandingByDimtih()
        {
            // AutoCAD 2021: DIMTIX decides by DIMTIH, not DIMTOH: level words next to the rim stand on a landing an arrowhead long
            // from the knee, the line under DIMTAD 1 no longer run on beneath them.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimtih"] = true;
            });
            DxfDimension dimension = Round(session, "DRA", 20, Polar(30, 40));

            Near(27.7582, 12.5, Words(dimension).Location);
            HasLine(dimension, 24.1506, 12.5, 21.6506, 12.5);
            HasLine(dimension, 21.6506, 12.5, 19.4856, 11.25);
        }

        [Fact]
        public void ARadiusUnderDimtixAndFit0_StandsInTheMiddleWhereItFits()
        {
            // AutoCAD 2021: DIMTIX keeps the words inside, wherever the point was put (35 out): "R20" in the middle of the line.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimatfit"] = 0;
            });
            DxfDimension dimension = Round(session, "DRA", 20, Polar(30, 35));

            Assert.True(dimension.UsingDefaultTextPosition);
            Near(6.6270, 6.0217, Words(dimension).Location);
            HasLine(dimension, 0, 0, 15.1554, 8.75);
        }

        [Fact]
        public void ARadiusOfFiveUnderDimtixAndFit0_KeepsItsWordsInAndTheArrowheadOut()
        {
            // AutoCAD 2021: the radius takes no more than its words and a gap each side; the arrowhead stands outside on its short
            // line, the line runs whole from the centre to the rim.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimatfit"] = 0;
            });
            DxfDimension dimension = Round(session, "DRA", 5, Polar(30, 10));

            Near(1.2169, 2.8923, Words(dimension).Location);
            HasLine(dimension, 6.4952, 3.75, 8.6603, 5);
            HasLine(dimension, 0, 0, 4.3301, 2.5);
        }

        [Fact]
        public void ARadiusOfFiveUnderDimtixAndDimtoflOff_DrawsNoLineAndMarksTheCentre()
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimatfit"] = 0;
                v["Dimtofl"] = false;
            });
            DxfDimension dimension = Round(session, "DRA", 5, Polar(30, 10));

            HasLine(dimension, 6.4952, 3.75, 8.6603, 5);
            HasLine(dimension, -2.5, 0, 2.5, 0);
            HasLine(dimension, 0, -2.5, 0, 2.5);
            HasNoLine(dimension, 0, 0, 4.3301, 2.5);
        }

        [Fact]
        public void ADiameterOfSixUnderDimtixAndFit0_KeepsItsWordsInAndBothArrowheadsOut()
        {
            // AutoCAD 2021: "%%c6" stays at the centre on the line, the arrowheads outside on their short lines.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimatfit"] = 0;
            });
            DxfDimension dimension = Round(session, "DDI", 3, Polar(30, 10));

            Near(-0.9857, 1.7072, Words(dimension).Location);
            HasLine(dimension, -4.7631, -2.75, -6.9282, -4);
            HasLine(dimension, 4.7631, 2.75, 6.9282, 4);
            HasLine(dimension, -2.5981, -1.5, 2.5981, 1.5);
        }

        [Fact]
        public void WordsUnderDimtixAndDimtmove1_StandNextToTheRimNotWhereThePointPutThem()
        {
            // AutoCAD 2021: the point (35 out at 70 degrees, the circle picked at 30) is not heeded: the same picture as DIMTMOVE 0.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimtmove"] = 1;
            });
            DxfDimension dimension = Round(session, "DRA", 20, Polar(70, 35));

            Assert.True(dimension.UsingDefaultTextPosition);
            Near(23.8242, 15.9505, Words(dimension).Location);
            HasLine(dimension, 27.8992, 16.1076, 19.4856, 11.25);
        }

        [Fact]
        public void WordsUnderDimtixDimatfit0AndDimtmove2_StandInTheMiddleWhereTheyFit()
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtix"] = true;
                v["Dimatfit"] = 0;
                v["Dimtmove"] = 2;
            });
            DxfDimension dimension = Round(session, "DRA", 20, Polar(70, 35));

            Assert.True(dimension.UsingDefaultTextPosition);
            Near(6.6270, 6.0217, Words(dimension).Location);
            HasLine(dimension, 0, 0, 15.1554, 8.75);
        }

        // ---- DIMTEDIT on a radius or a diameter at DIMTMOVE 0 ----

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void DimteditOnARadiusUnderFit0And2_TurnsItsLineTowardThePoint(int fit)
        {
            // AutoCAD 2021: "R20" whose words stand in the middle of its line, moved to 15 out at 120 degrees, has its line at 120
            // degrees through the rim point (-10, 17.3205), the words at 15 out and the line whole.
            CadCommandSession session = Probe(v => v["Dimatfit"] = fit);
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 35));
            Move(session, radius, Polar(120, 15));

            Assert.False(radius.UsingDefaultTextPosition);
            Near(-10, 17.3205, ((DxfRadialDimension)radius).ChordPoint);
            Near(-5.8533, 13.9411, Words(radius).Location);
            HasLine(radius, 0, 0, -8.75, 15.1554);
        }

        [Fact]
        public void DimteditOnARadiusUnderFit3_SlidesTheWordsAlongItsLine()
        {
            // AutoCAD 2021: the line stays at 30 degrees; the point (15 out at 120 degrees) falls on it at the centre.
            CadCommandSession session = Probe(v => { });
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 35));
            Move(session, radius, Polar(120, 15));

            Near(17.3205, 10, ((DxfRadialDimension)radius).ChordPoint);
            Near(-0.9507, 1.6467, Words(radius).Location);
            HasLine(radius, -3.1243, -1.8038, 15.1554, 8.75);
        }

        [Fact]
        public void DimteditOnARadiusUnderFit0_PutsWordsPastTheRimWhereTheFitPutsThem()
        {
            // AutoCAD 2021: moved to 25 out (inside the room an arrowhead and the words take) they stand 28.6 out, the arrowhead
            // outside, and the dimension is a 164: the words put by hand.
            CadCommandSession session = Probe(v => v["Dimatfit"] = 0);
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 35));
            Move(session, radius, Polar(30, 25));

            Assert.False(radius.UsingDefaultTextPosition);
            Near(23.8242, 15.9505, Words(radius).Location);
            HasLine(radius, 19.4856, 11.25, 27.8992, 16.1076);
            HasLine(radius, 0, 0, 17.3205, 10);
        }

        [Fact]
        public void DimteditOnARadiusUnderFit2_KeepsTheArrowheadInWhenTheWordsGoOut()
        {
            // AutoCAD 2021: text first, then the arrowhead: the words 26.25 out past the rim, the arrowhead inside on its line,
            // which runs on from the rim to the end of the words.
            CadCommandSession session = Probe(v => v["Dimatfit"] = 2);
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 35));
            Move(session, radius, Polar(30, 25));

            Near(21.6591, 14.7005, Words(radius).Location);
            HasLine(radius, 0, 0, 15.1554, 8.75);
            HasLine(radius, 17.3205, 10, 25.7341, 14.8576);
        }

        [Fact]
        public void DimteditOnARadiusUnderFit3_WritesWordsHeldOffTheRimAsADefaultPlace()
        {
            // AutoCAD 2021: put 25 out under DIMATFIT 3 the words are pushed to 28.6 out and the dimension is a 36.
            CadCommandSession session = Probe(v => { });
            DxfDimension radius = Round(session, "DRA", 20, Polar(30, 35));
            Move(session, radius, Polar(30, 25));

            Assert.True(radius.UsingDefaultTextPosition);
            Near(23.8242, 15.9505, Words(radius).Location);
            HasLine(radius, 27.8992, 16.1076, 19.4856, 11.25);
        }

        [Fact]
        public void DimteditOnADiameterUnderFit0_PutsWordsPastTheRimAndTheArrowheadsOut()
        {
            CadCommandSession session = Probe(v => v["Dimatfit"] = 0);
            DxfDimension diameter = Round(session, "DDI", 20, Polar(30, 35));
            Move(session, diameter, Polar(30, 25));

            Near(23.9310, 16.0929, Words(diameter).Location);
            HasLine(diameter, -19.4856, -11.25, -21.6506, -12.5);
            HasLine(diameter, 19.4856, 11.25, 28.1827, 16.2713);
            HasLine(diameter, -17.3205, -10, 17.3205, 10);
        }

        [Fact]
        public void DimteditOnADiameterUnderFit2_KeepsBothArrowheadsIn()
        {
            CadCommandSession session = Probe(v => v["Dimatfit"] = 2);
            DxfDimension diameter = Round(session, "DDI", 20, Polar(30, 35));
            Move(session, diameter, Polar(30, 25));

            Near(21.7660, 14.8429, Words(diameter).Location);
            HasLine(diameter, -15.1554, -8.75, 15.1554, 8.75);
            HasLine(diameter, 17.3205, 10, 26.0177, 15.0213);
        }

        // ---- the words of an ordinate, a linear and an angular dimension moved ----

        [Fact]
        public void DimteditOnAnOrdinate_RunsTheLeaderToTheWordsWithAJog()
        {
            // AutoCAD 2021: the leader of the point (1200, 0) measuring 1200, its words moved 8 across and 50 up, runs up to 35.764,
            // across 8 and up 5, then 5 more, ending the words' half width and a gap short of them.
            CadCommandSession session = Probe(v => v["Dimtad"] = 0);
            Run(session, "DOR", P(1200, 0), P(1200, 25));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(1208, 50));

            Near(1208, 50, Words(ordinate).Location);
            HasLine(ordinate, 1200, 0.625, 1200, 35.764);
            HasLine(ordinate, 1200, 35.764, 1208, 40.764);
            HasLine(ordinate, 1208, 40.764, 1208, 45.764);
        }

        [Fact]
        public void DimteditOnAnOrdinateToAPointInLine_RunsTheLeaderStraightToTheWords()
        {
            CadCommandSession session = Probe(v => v["Dimtad"] = 0);
            Run(session, "DOR", P(1200, 0), P(1200, 25));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(1200, 50));

            HasLine(ordinate, 1200, 0.625, 1200, 45.764);
        }

        [Fact]
        public void DimteditOnAnOrdinateToAPointNearerThanTheLeaderEnd_KeepsTheFirstLegAnArrowheadTwiceLong()
        {
            // AutoCAD 2021: the words moved to (-8, 15) leave room for a first leg of 5 only: up to 5, across, up 5 more.
            CadCommandSession session = Probe(v => v["Dimtad"] = 0);
            Run(session, "DOR", P(1200, 0), P(1200, 25));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(1192, 15));

            HasLine(ordinate, 1200, 0.625, 1200, 5);
            HasLine(ordinate, 1200, 5, 1192, 5.764);
            HasLine(ordinate, 1192, 5.764, 1192, 10.764);
        }

        [Fact]
        public void DimteditOnAnOrdinateAlongY_JogsTheSameWayAcross()
        {
            CadCommandSession session = Probe(v => v["Dimtad"] = 0);
            Run(session, "DOR", P(0, 900), P(25, 900));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(75, 908));

            HasLine(ordinate, 0.625, 900, 61.6175, 900);
            HasLine(ordinate, 61.6175, 900, 66.6175, 908);
            HasLine(ordinate, 66.6175, 908, 71.6175, 908);
        }

        [Fact]
        public void DimteditOnAnOrdinateUnderDimtad1_RunsTheLeaderOnUnderTheWords()
        {
            // AutoCAD 2021: the words of "2400" beside the leader, which runs on under them to 53.75.
            CadCommandSession session = Probe(v => v["Dimtad"] = 1);
            Run(session, "DOR", P(2400, 0), P(2400, 25));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(2408, 50));

            Near(2408, 50, Words(ordinate).Location);
            HasLine(ordinate, 2400, 0.625, 2400, 35.625);
            HasLine(ordinate, 2400, 35.625, 2409.9014, 40.625);
            HasLine(ordinate, 2409.9014, 40.625, 2409.9014, 53.75);
        }

        [Fact]
        public void DimteditOnAnOrdinateUnderDimtmove2_LeavesTheLeaderWhereItEnds()
        {
            // AutoCAD 2021: the words stand free at (8, 50); the leader still ends at 25 and does not run on under them.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtad"] = 1;
                v["Dimtmove"] = 2;
            });
            Run(session, "DOR", P(1200, 0), P(1200, 25));
            DxfDimension ordinate = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, ordinate, P(1208, 50));

            Near(1208, 50, Words(ordinate).Location);
            HasLine(ordinate, 1200, 0.625, 1200, 25);
        }

        [Theory]
        [InlineData(0, 116.7352, 20)]
        [InlineData(1, 123.2648, 21.9014)]
        public void WordsPutPastTheEndOfALinearDimensionUnderDimtmove1_HaveTheLineRunOnToThem(
            int tad,
            double lineEnd,
            double wordsY
        )
        {
            // AutoCAD 2021: "100" put 20 past the end of a line 100 long, on it (DIMTAD 0) the line stops a gap short of them,
            // above it (1) it runs on under them.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                v["Dimtad"] = tad;
            });
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            DxfDimension linear = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, linear, P(120, 20));

            Near(120, wordsY, Words(linear).Location);
            HasLine(linear, 100, 20, lineEnd, 20);
        }

        [Fact]
        public void WordsPutBeforeTheStartOfALinearDimensionUnderDimtmove1_HaveTheLineRunOnBackToThem()
        {
            CadCommandSession session = Probe(v => v["Dimtmove"] = 1);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            DxfDimension linear = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Move(session, linear, P(-20, 20));

            HasLine(linear, 0, 20, -23.2648, 20);
        }

        private static DxfDimension Angular(CadCommandSession session)
        {
            Run(session, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        [Theory]
        [InlineData(1, 0.4518, 0.5985, 0.0)]
        [InlineData(2, 0.4206, 0.6266, 5.2360)]
        public void WordsPutOnTheArcOfAnAngleUnderDimtmove1And2_BreakTheArcAboutThem(
            int move,
            double breakFrom,
            double breakTo,
            double rotation
        )
        {
            // AutoCAD 2021: "60°" put on the arc at 30 degrees, level (1) or along it (2), is not lifted off it: the arc, 30 from
            // the vertex, is broken about the words, and the arrowheads stay inside.
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = move;
                v["Dimtad"] = 1;
            });
            DxfDimension angle = Angular(session);
            Move(session, angle, Polar(30, 30));

            Near(25.9808, 15, Words(angle).Location);
            Assert.True(Math.Abs(Words(angle).Rotation - rotation) < 1E-3);
            List<DxfArc> arcs = angle
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased)
                .OrderBy(a => a.StartAngle)
                .ToList();
            Assert.Equal(2, arcs.Count);
            Assert.True(Math.Abs(arcs[0].StartAngle - 0.0834) < 1E-3 && Math.Abs(arcs[0].EndAngle - breakFrom) < 1E-3);
            Assert.True(Math.Abs(arcs[1].StartAngle - breakTo) < 1E-3 && Math.Abs(arcs[1].EndAngle - 0.9638) < 1E-3);
        }

        [Fact]
        public void WordsPutNearTheArcOfAnAngleUnderDimtmove1_HaveNoLeader()
        {
            // AutoCAD 2021: words 5 off the arc draw no leader, 8 off they do: from the middle of the arc to a landing level with them.
            CadCommandSession near = Probe(v => v["Dimtmove"] = 1);
            DxfDimension close = Angular(near);
            Move(near, close, Polar(30, 35));
            Assert.Equal(2, Lines(close).Count);

            CadCommandSession far = Probe(v => v["Dimtmove"] = 1);
            DxfDimension distant = Angular(far);
            Move(far, distant, Polar(30, 38));
            Near(32.9090, 19, Words(distant).Location);
            HasLine(distant, 25.9808, 15, 27.3249, 19);
            HasLine(distant, 27.3249, 19, 29.8249, 19);
        }

        [Fact]
        public void WordsPutOnTheArcNextToARay_SendTheArrowheadsOutside()
        {
            // AutoCAD 2021: "60°" put on the arc 6 degrees from the first ray, 35 out, cut into the first arrowhead's room: the
            // arrowheads stand outside on arcs of their own and the arc runs whole.
            CadCommandSession session = Probe(v => v["Dimtad"] = 0);
            DxfDimension angle = Angular(session);
            Move(session, angle, Polar(6, 35));

            List<DxfArc> arcs = angle.DimBlock.OfType<DxfArc>().Where(a => !a.IsErased).ToList();
            Assert.Equal(3, arcs.Count);
            Assert.All(arcs, a => Assert.Equal(35.0, a.Radius, 3));
            Assert.Contains(arcs, a => Math.Abs(a.EndAngle - 1.0472) < 1E-3);
        }
    }
}
