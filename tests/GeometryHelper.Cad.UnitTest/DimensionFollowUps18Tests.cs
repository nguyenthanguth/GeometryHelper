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
    /// What AutoCAD 2021 does with an angle's words put by DIMTEDIT that the rounds before left: where it writes the arc point,
    /// and the leader band on an arc far out. Every figure is a picture made by accoreconsole: the metric template with a style
    /// of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial, an angle of 60 degrees at the origin.
    /// </summary>
    public class DimensionFollowUps18Tests
    {
        private static CadCommandSession Probe(int move = 1)
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
                ["Dimtmove"] = move,
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

        /// <summary>The angle of 60 degrees with its arc <paramref name="arc"/> out, its words put at a point by DIMTEDIT.</summary>
        private static DxfPoint3AngularDimension WordsPut(double arc, GeoPoint2 at, int move = 1)
        {
            CadCommandSession session = Probe(move);
            Run(session, "DAN", Enter, P(0, 0), Polar(0, arc + 20), Polar(60, arc + 20), Polar(30, arc));
            var dimension = (DxfPoint3AngularDimension)session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, at);
            return dimension;
        }

        /// <summary>The direction of an arc point from the vertex, in degrees.</summary>
        private static double Direction(GeoPoint3 arcPoint) => Math.Atan2(arcPoint.Y, arcPoint.X) * 180.0 / Math.PI;

        private static int LinesWithWordsPut(double arc, double degrees, double offTheArc) =>
            WordsPut(arc, Polar(degrees, arc + offTheArc)).DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);

        private const int NoLeader = 2;
        private const int Leader = 4;

        [Fact]
        public void WordsPutNearerTheFirstRay_HaveTheArcPointTwoThirdsRound()
        {
            // AutoCAD 2021, an arc of 30: the arc point stood at 20 degrees (a third) where the arc was made and went to 40
            // degrees (two thirds) with words put at (33.5, 14), 22.7 degrees, at (35, 15), or at 215 degrees and 350 degrees.
            Assert.Equal(40.0, Direction(WordsPut(30.0, P(33.5, 14.0)).ArcPoint), 6);
            Assert.Equal(40.0, Direction(WordsPut(30.0, P(35.0, 15.0)).ArcPoint), 6);
            Assert.Equal(40.0, Direction(WordsPut(30.0, Polar(215.0, 31.0)).ArcPoint), 6);
            Assert.Equal(40.0, Direction(WordsPut(30.0, Polar(350.0, 31.0)).ArcPoint), 6);
        }

        [Fact]
        public void WordsPutNearerTheSecondRay_KeepTheArcPointAThirdRound()
        {
            // AutoCAD 2021: at (20, 14) (35 degrees), at (-33.5, 14.5) (156.6 degrees) and at 180 degrees the arc point stays at
            // 20 degrees, a third of the way round.
            Assert.Equal(20.0, Direction(WordsPut(30.0, P(20.0, 14.0)).ArcPoint), 6);
            Assert.Equal(20.0, Direction(WordsPut(30.0, P(-33.5, 14.5)).ArcPoint), 6);
            Assert.Equal(20.0, Direction(WordsPut(30.0, Polar(180.0, 32.0)).ArcPoint), 6);
        }

        [Fact]
        public void WordsPutOnTheArcUnderDimtmove0_HaveTheArcPointThroughThemAtTheNearerRaysThird()
        {
            // AutoCAD 2021, DIMTMOVE 0: the arc runs through the words and its point stands at 40 degrees (two thirds) where they
            // are put at 20 or 220 degrees, at 20 degrees (a third) where they are put at 100, 36 and 24 out (6 off the arc of
            // 30 either way).
            foreach (
                (double degrees, double out6, double arcPoint) in new[]
                {
                    (20.0, 36.0, 40.0),
                    (20.0, 24.0, 40.0),
                    (220.0, 36.0, 40.0),
                    (100.0, 36.0, 20.0),
                    (100.0, 24.0, 20.0),
                }
            )
            {
                GeoPoint3 at = WordsPut(30.0, Polar(degrees, out6), 0).ArcPoint;
                Assert.Equal(arcPoint, Direction(at), 6);
                Assert.Equal(out6, Math.Sqrt(at.X * at.X + at.Y * at.Y), 6);
            }
        }

        [Fact]
        public void WordsOfAnAngleOfTwoLinesPutByHand_HaveTheArcPointAtTheNearerRaysThird()
        {
            // AutoCAD 2021: two lines from the origin along 0 and 60 degrees, the arc through (26, 15): words put 36 out at 220
            // or 350 degrees have the arc point (group 16) at 40 degrees, put at 100 or 60 degrees at 20.
            foreach (
                (double degrees, double arcPoint) in new[] { (220.0, 40.0), (350.0, 40.0), (100.0, 20.0), (60.0, 20.0) }
            )
            {
                CadCommandSession session = Probe();
                Run(session, "L", P(0, 0), Polar(0, 50), Enter);
                string first = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
                Run(session, "L", P(0, 0), Polar(60, 50), Enter);
                string second = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
                Run(session, "DAN", new[] { first }, new[] { second }, Polar(30, 30));
                var dimension = (DxfLineAngularDimension2)
                    session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
                Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, 36.0));
                Assert.Equal(arcPoint, Direction(dimension.ArcPoint), 6);
            }
        }

        /// <summary>The lines of the picture of an angle of 60 degrees from a first ray, its arc 30 out, words put by DIMTEDIT.</summary>
        private static int LinesAtFirstRay(double first, double arc, double degrees, double offTheArc)
        {
            CadCommandSession session = Probe();
            Run(
                session,
                "DAN",
                Enter,
                P(0, 0),
                Polar(first, arc + 20),
                Polar(first + 60.0, arc + 20),
                Polar(first + 30.0, arc)
            );
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, Polar(degrees, arc + offTheArc));
            return dimension.DimBlock.OfType<DxfLine>().Count(l => !l.IsErased);
        }

        [Fact]
        public void AnAngleFrom30Degrees_KeepsNoLeaderNearItsFirstRay()
        {
            // AutoCAD 2021, "60°" put round an angle from 30 to 90 degrees, its arc 30 out: at 40 degrees none up to 3.75 inside
            // it and a leader from 4.0 (the band fitted to the angle from 0, which has the first ray beside the words, drew
            // none to 5.5 inside), at 120 degrees none up to 3.0 inside and a leader from 3.25.
            Assert.Equal(NoLeader, LinesAtFirstRay(30.0, 30.0, 40.0, -3.75));
            Assert.Equal(Leader, LinesAtFirstRay(30.0, 30.0, 40.0, -4.5));
            Assert.Equal(NoLeader, LinesAtFirstRay(30.0, 30.0, 120.0, -3.0));
            Assert.Equal(Leader, LinesAtFirstRay(30.0, 30.0, 120.0, -3.5));
        }

        [Fact]
        public void AnAngleFrom210Degrees_KeepsNoLeaderBesideTheMiddleOfItsArc()
        {
            // AutoCAD 2021, an angle from 210 to 270 degrees, its arc 30 out: words put at 110 degrees keep no leader to 3.25
            // inside it (the band of the angle from 0 asked 2.5 of it) and put at 60 degrees get one from 4.5 inside.
            Assert.Equal(NoLeader, LinesAtFirstRay(210.0, 30.0, 110.0, -3.25));
            Assert.Equal(Leader, LinesAtFirstRay(210.0, 30.0, 60.0, -4.5));
        }

        [Fact]
        public void WordsPutOnAnArcOf12_ReadByTheRuleNotTheFit()
        {
            // AutoCAD 2021, an arc 12 out (2.1 landings of 5.58, nearer than the 2.5 the rule was read from): words put at 60
            // degrees keep no leader as far in as 5.25, and put at 100 degrees they get one from 2.75 inside it.
            Assert.Equal(NoLeader, LinesAtFirstRay(0.0, 12.0, 60.0, -5.25));
            Assert.Equal(Leader, LinesAtFirstRay(0.0, 12.0, 100.0, -2.75));
        }

        [Fact]
        public void WordsPutAbove95DegreesOfAnArcOf240_KeepNoLeaderTo4Inside()
        {
            // AutoCAD 2021, the angle's arc 240 out (43 landings), words put at 95 degrees: none up to 4.0 inside it and a leader
            // at 4.25 (the table fitted to arcs of 30 out drew one from 4.0), the same at 265 degrees.
            Assert.Equal(NoLeader, LinesWithWordsPut(240.0, 95.0, -4.0));
            Assert.Equal(Leader, LinesWithWordsPut(240.0, 95.0, -4.25));
            Assert.Equal(NoLeader, LinesWithWordsPut(240.0, 265.0, -4.0));
            Assert.Equal(Leader, LinesWithWordsPut(240.0, 265.0, -4.25));
        }
    }
}
