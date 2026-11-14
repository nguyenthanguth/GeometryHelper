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
    /// Where AutoCAD 2021 drew the leader of an angle's words and the arrowhead of a free radius's words another way than the
    /// viewer: the band round the arc in which words put by DIMTEDIT get no leader, refitted over 30 000 probes (SHX and
    /// Arial, several arrowheads, words, gaps and decimals), and the line of a radius whose arrowhead stays in where the box
    /// of the words takes it only to the centre. Every figure is a picture made by accoreconsole: the metric template with a
    /// style of arrowheads and words 2.5 high, DIMGAP 0.625 and Arial, a circle or an angle about the origin.
    /// </summary>
    public class DimensionFollowUps12Tests
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

        // ---- the leader of an angle's words ----

        /// <summary>An angle of 60 degrees at the origin, its arc 30 out, its words moved under DIMTMOVE 1 to the point.</summary>
        private static DxfDimension AngleMovedTo(GeoPoint2 at, Action<SortedDictionary<string, object>> style = null)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 1;
                style?.Invoke(v);
            });
            Run(session, "DAN", Enter, P(0, 0), Polar(0, 50), Polar(60, 50), Polar(30, 30));
            DxfDimension dimension = session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
            Run(session, "DIMTEDIT", new[] { dimension.Handle }, at);
            return dimension;
        }

        private const int NoLeader = 2;
        private const int Leader = 4;

        [Fact]
        public void WordsOfAnAnglePut4OutsideTheArcAlmostAboveTheVertex_DrawNoLeader()
        {
            // AutoCAD 2021: "60°" put 4 out of the arc of 30 at 85 degrees draws no leader, nor does it 3.5 out; and a leader
            // from 4.25 (before, the viewer drew one from 4.0).
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 34))).Count);
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 33.5))).Count);
            Assert.Equal(Leader, Lines(AngleMovedTo(Polar(85, 34.5))).Count);
        }

        [Fact]
        public void WordsOfAnAnglePut2Point75InsideTheArcAlmostAboveTheVertex_DrawNoLeader()
        {
            // AutoCAD 2021: the same words 2.75 and 2.5 inside the arc draw none (before, a leader at 2.75); 3.5 in draw one.
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 27.25))).Count);
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 27.5))).Count);
            Assert.Equal(Leader, Lines(AngleMovedTo(Polar(85, 26.5))).Count);
        }

        [Fact]
        public void WordsOfAnAnglePut7OutsideTheArcLevelWithTheVertex_DrawALeader()
        {
            // AutoCAD 2021: "60°" put 7 out of the arc at 0 degrees draws a leader, and none at 6.5 (before, none at 7).
            Assert.Equal(Leader, Lines(AngleMovedTo(Polar(0, 37))).Count);
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(0, 36.5))).Count);
        }

        [Fact]
        public void WordsOfAnAnglePut6OutsideTheArcAt45Degrees_DrawALeader()
        {
            // AutoCAD 2021: put 6 out at 45 degrees draws a leader (before, none).
            Assert.Equal(Leader, Lines(AngleMovedTo(Polar(45, 36))).Count);
        }

        [Fact]
        public void WordsOfAnAngleOfWideDecimals_KeepTheirBandAboveTheVertexNarrow()
        {
            // AutoCAD 2021: "60.0000°" (DIMADEC 4, 13.7 wide) put 6 out of the arc at 85 degrees draws no leader (before, one),
            // and nor does one 5.5 out.
            Action<SortedDictionary<string, object>> style = v => v["Dimadec"] = 4;
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 36), style)).Count);
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 35.5), style)).Count);
        }

        [Fact]
        public void WordsOfAnAngleUnderDimasz4Put8InsideTheArcLevelWithTheVertex_DrawNoLeader()
        {
            // AutoCAD 2021: DIMASZ 4, the same words put 8 inside the arc at 0 degrees draw no leader, and nor does one 7.5 in.
            Action<SortedDictionary<string, object>> style = v => v["Dimasz"] = 4.0;
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(0, 22), style)).Count);
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(0, 22.5), style)).Count);
        }

        [Fact]
        public void WordsOfAnAngleUnderDimgap1Point5Put4Point5OutsideTheArcAlmostAboveTheVertex_DrawNoLeader()
        {
            // AutoCAD 2021: DIMGAP 1.5, the same words put 4.5 out at 85 degrees draw no leader (before, a leader).
            Action<SortedDictionary<string, object>> style = v => v["Dimgap"] = 1.5;
            Assert.Equal(NoLeader, Lines(AngleMovedTo(Polar(85, 34.5), style)).Count);
        }

        // ---- the arrowhead of a free radius's words ----

        /// <summary>A circle of 20 about the origin picked at 30 degrees, its radius with "R20" put at a point under DIMTMOVE 2, DIMTAD 0, level.</summary>
        private static DxfDimension FreeRadius(GeoPoint2 at)
        {
            CadCommandSession session = Probe(v =>
            {
                v["Dimtmove"] = 2;
                v["Dimtad"] = 0;
            });
            Run(session, "C", P(0, 0), "20");
            Run(session, "DRA", Polar(30, 20), at);
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        /// <summary>The point <paramref name="along"/> out along the line at 30 degrees and <paramref name="across"/> to its left.</summary>
        private static GeoPoint2 Beside(double along, double across) =>
            P(
                along * Math.Cos(Math.PI / 6.0) - across * Math.Sin(Math.PI / 6.0),
                along * Math.Sin(Math.PI / 6.0) + across * Math.Cos(Math.PI / 6.0)
            );

        [Fact]
        public void LevelWordsOfARadiusWhoseBoxTakesItsLineQuarterOfAUnitShortOfTheCentre_KeepTheArrowheadIn()
        {
            // AutoCAD 2021: "R20" put 4 behind the centre of a radius of 20 picked at 30 degrees, on its line (the box takes the
            // line to 0.25 short of the centre), leaves the arrowhead inside and the line whole. (Before, the arrowhead
            // went out, as it does for a box reaching 0.25 behind.)
            DxfDimension dimension = FreeRadius(Beside(-4, 0));

            HasLine(dimension, 0, 0, 15.155, 8.75);
            Assert.Single(Lines(dimension));
        }

        [Fact]
        public void LevelWordsOfARadiusWhoseBoxJustMissesTheCentre_KeepTheArrowheadIn()
        {
            // AutoCAD 2021: "R20" put 2.5 behind the centre and 3 across the line, its box taking the line only to 0.066 short
            // of the centre, keeps the arrowhead in.
            DxfDimension dimension = FreeRadius(Beside(-2.5, 3));

            HasLine(dimension, 0, 0, 15.155, 8.75);
            Assert.Single(Lines(dimension));
        }

        [Fact]
        public void LevelWordsOfARadiusPutWithinTwoArrowheadsOfTheCentreOffTheLine_PushTheArrowheadOut()
        {
            // AutoCAD 2021: "R20" put 4 out along the line and 3 across it (5.0 from the centre, its box clipping the line
            // from 5.4 to 6.4) pushes the arrowhead out: the line whole to the rim and a stem past it. 5 out along the line
            // (5.83 from the centre) does not: the arrowhead stays in and the line is broken about the words.
            DxfDimension pushed = FreeRadius(Beside(4, 3));

            HasLine(pushed, 0, 0, 17.32, 10);
            HasLine(pushed, 19.486, 11.25, 21.651, 12.5);

            DxfDimension kept = FreeRadius(Beside(5, 3));

            HasLine(kept, 0, 0, 2.694, 1.555);
            HasLine(kept, 9.281, 5.358, 15.155, 8.75);
            Assert.Equal(2, Lines(kept).Count);
        }
    }
}
