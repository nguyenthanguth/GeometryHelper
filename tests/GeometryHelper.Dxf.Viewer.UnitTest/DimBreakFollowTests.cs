using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIMBREAK's breaks follow the objects they were made at, against AutoCAD 2021's console run through standard input
    /// (16 November, about 150 edits of level dimensions and multileaders with lines across): a level dimension from (0, 0) to
    /// (100, 0) with its line at y 20 and a line at x 30 across it, broken by Auto, then the line or the dimension moved,
    /// turned, scaled or erased. AutoCAD draws the dimension again from its BREAKDATA after each: the break is where the object
    /// named there crosses it now, none where it crosses nothing, the object gone from the BREAKDATA when it is erased. Auto
    /// names every object whose box meets the dimension's (not only those that cross it), the newest first. A break by hand
    /// goes with the dimension. Its extents in AutoCAD's own view add about a pixel, which the presenter, with no view, does not.
    /// </summary>
    public class DimBreakFollowTests
    {
        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        /// <summary>A drawing with the style of AutoCAD's blank one, the words of no height and no gap, so that the lines run whole to the arrows.</summary>
        private static CadDocument Drawing(out CadCommandSession session)
        {
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimtad = 1;
            standard.Dimexo = 0.0625;
            standard.Dimexe = 0.18;
            standard.Dimasz = 0.18;
            standard.Dimtxt = 0.0;
            standard.Dimgap = 0.0;
            standard.Dimcen = 0.09;
            StandardCurrent(document.Database);
            session = Session(document);
            return document;
        }

        /// <summary>The level dimension from (0, 0) to (100, 0), its line at y 20.</summary>
        private static string Level(CadDocument document, CadCommandSession session)
        {
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, 20));
            return Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
        }

        private static string Line(CadDocument document, double x1, double y1, double x2, double y2)
        {
            var line = new DxfLine(new GeoPoint3(x1, y1, 0.0), new GeoPoint3(x2, y2, 0.0));
            document.Database.ModelSpace.AppendEntity(line);
            return line.Handle;
        }

        /// <summary>The pieces of the horizontal line of a dimension's picture at a height, as "x1,x2" each, the lesser first.</summary>
        private static List<string> Pieces(CadDocument document, string handle, double y) =>
            ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfLine>()
                .Where(l =>
                    !l.IsErased
                    && Math.Abs(l.StartPoint.Y - y) < 1E-6
                    && Math.Abs(l.EndPoint.Y - y) < 1E-6
                    && Math.Abs(l.StartPoint.X - l.EndPoint.X) > 1E-9
                )
                .Select(l => new[] { Math.Min(l.StartPoint.X, l.EndPoint.X), Math.Max(l.StartPoint.X, l.EndPoint.X) })
                .OrderBy(p => p[0])
                .Select(p => Rounded(p[0]) + "," + Rounded(p[1]))
                .ToList();

        private static string Rounded(double value) =>
            Math.Round(value, 4).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The objects the drawing's BREAKDATA names, in order, as the handles of its 331.</summary>
        private static List<string> Named(CadDocument document)
        {
            string text = document.Database.ToText().Replace("\r\n", "\n");
            int at = text.IndexOf("\n100\nAcDbBreakData\n", StringComparison.Ordinal);

            if (at < 0)
            {
                return new List<string>();
            }

            int end = text.IndexOf("\n  0\n", at, StringComparison.Ordinal);
            return Regex
                .Matches(text.Substring(at, end - at), @"\n331\n([0-9A-F]+)\n")
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .ToList();
        }

        [Fact]
        public void ALineMovedTenToTheRight_TakesTheBreakWithIt()
        {
            // AutoCAD 2021: a line at x 30 across the dimension line, broken by Auto, then moved 10 to the right, leaves the
            // line broken about x 40 and whole at x 30.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string line = Line(document, 30, -5, 30, 50);
            Run(session, "DIMBREAK", new[] { dimension }, "A");
            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 20));

            Run(session, "MOVE", new[] { line }, Enter, P(0, 0), P(10, 0));

            Assert.Equal(new[] { "0.18,39.9375", "40.0625,99.82" }, Pieces(document, dimension, 20));
            Assert.Equal(new[] { line }, Named(document));
            Assert.True(document.Undo());
            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void ALineMovedOffTheDimension_LeavesItWhole_AndStaysNamed()
        {
            // AutoCAD 2021: moved 60 down, or 40 to the left, the line crosses nothing; the BREAKDATA still names it, and moved
            // back it breaks the dimension again.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string line = Line(document, 30, -5, 30, 50);
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            Run(session, "MOVE", new[] { line }, Enter, P(0, 0), P(0, -60));

            Assert.Equal(new[] { "0.18,99.82" }, Pieces(document, dimension, 20));
            Assert.Equal(new[] { line }, Named(document));

            Run(session, "MOVE", new[] { line }, Enter, P(0, 0), P(0, 60));

            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void ALineErased_IsTakenOutOfTheBreaks_AndTheBreakDataGoesWithTheLast()
        {
            // AutoCAD 2021: two lines at x 30 and 70, one erased: the dimension is broken at the other, which the BREAKDATA
            // keeps; both erased, the line is whole and the BREAKDATA and the extension dictionary are gone.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string first = Line(document, 30, -5, 30, 50);
            string second = Line(document, 70, -5, 70, 50);
            Run(session, "DIMBREAK", new[] { dimension }, "A");
            Assert.Equal(new[] { second, first }, Named(document));

            Run(session, "ERASE", new[] { first }, Enter);

            Assert.Equal(new[] { "0.18,69.9375", "70.0625,99.82" }, Pieces(document, dimension, 20));
            Assert.Equal(new[] { second }, Named(document));

            Run(session, "ERASE", new[] { second }, Enter);

            Assert.Equal(new[] { "0.18,99.82" }, Pieces(document, dimension, 20));
            Assert.Empty(Named(document));
            Assert.Null(((DxfDimension)document.FindEntity(dimension)).ExtensionDictionary);
            Assert.True(document.Undo());
            Assert.Equal(new[] { "0.18,69.9375", "70.0625,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void ALineTheBreakDataDoesNotName_BreaksNothingWhereverItIsMoved()
        {
            // AutoCAD 2021: a line at (60, 30) to (60, 40) is clear of the dimension, so Auto does not name it; moved 15 down
            // across the dimension line it breaks nothing.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string crossing = Line(document, 30, -5, 30, 50);
            string clear = Line(document, 60, 30, 60, 40);
            Run(session, "DIMBREAK", new[] { dimension }, "A");
            Assert.Equal(new[] { crossing }, Named(document));

            Run(session, "MOVE", new[] { clear }, Enter, P(0, 0), P(0, -15));

            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void AnObjectWhoseBoxMeetsTheDimension_IsNamedWhetherItCrossesOrNot_AndBreaksItWhereItIsMovedAcross()
        {
            // AutoCAD 2021: a line from (10, 0.5) to (20, 15) lies inside the dimension's box, from (0, 0) to (100, 20.18), and
            // touches none of its lines: Auto names it and leaves the line whole; moved 10 up it crosses the dimension line at
            // x 16.55 and breaks it there (from 16.489 to 16.614).
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string inside = Line(document, 10, 0.5, 20, 15);
            Line(document, 300, 0, 300, 10);
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            Assert.Equal(new[] { inside }, Named(document));
            Assert.Equal(new[] { "0.18,99.82" }, Pieces(document, dimension, 20));

            Run(session, "MOVE", new[] { inside }, Enter, P(0, 0), P(0, 10));

            Assert.Equal(new[] { "0.18,16.4892", "16.6142,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void Auto_NamesTheObjectsNewestFirst()
        {
            // AutoCAD 2021: lines drawn as c, a, b and a circle e are named e, b, a, c.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            string c = Line(document, 60, -5, 60, 30);
            string a = Line(document, 30, -5, 30, 30);
            string b = Line(document, 10, -5, 10, 30);
            var circle = new DxfCircle(new GeoPoint3(80, 10, 0), new GeoVector3(0, 0, 1), 3);
            document.Database.ModelSpace.AppendEntity(circle);

            Run(session, "DIMBREAK", new[] { dimension }, "A");

            Assert.Equal(new[] { circle.Handle, b, a, c }, Named(document));
        }

        [Fact]
        public void TheDimensionMoved_IsBrokenAgainWhereTheLinesNamedCrossItNow()
        {
            // AutoCAD 2021: the dimension moved 10 up is broken about x 30 on its line at y 30; moved 50 to the right, away from
            // the line, it is whole.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            Line(document, 30, -5, 30, 50);
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            Run(session, "MOVE", new[] { dimension }, Enter, P(0, 0), P(0, 10));

            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 30));

            Run(session, "MOVE", new[] { dimension }, Enter, P(0, 0), P(50, 0));

            Assert.Equal(new[] { "50.18,149.82" }, Pieces(document, dimension, 30));
            Assert.True(document.Undo());
            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, Pieces(document, dimension, 30));
        }

        [Fact]
        public void TheDimensionTurnedThirtyDegrees_IsBrokenWhereTheLineCrossesItNow()
        {
            // AutoCAD 2021: turned 30 degrees about (0, 0), the dimension line runs from (-9.8441, 17.4105) to (76.4467, 67.2305),
            // broken from (29.9459, 40.3833) to (30.0541, 40.4458) where the line at x 30 crosses it.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            Line(document, 30, -5, 30, 50);
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            Run(session, "ROTATE", new[] { dimension }, Enter, P(0, 0), "30");

            List<double[]> along = ((DxfDimension)document.FindEntity(dimension))
                .DimBlock.OfType<DxfLine>()
                .Where(l =>
                    !l.IsErased
                    && Math.Abs(
                        Math.Atan2(l.EndPoint.Y - l.StartPoint.Y, l.EndPoint.X - l.StartPoint.X) * 180 / Math.PI - 30
                    ) < 1E-3
                )
                .Select(l => new[] { l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y })
                .OrderBy(l => l[0])
                .ToList();
            Assert.Equal(2, along.Count);
            Assert.Equal(-9.8441, along[0][0], 3);
            Assert.Equal(29.9459, along[0][2], 3);
            Assert.Equal(40.3833, along[0][3], 3);
            Assert.Equal(30.0541, along[1][0], 3);
            Assert.Equal(76.4467, along[1][2], 3);
        }

        [Fact]
        public void ABreakByHand_GoesWithTheDimension_ItsPointsCarriedWhereTheDimensionIsMoved()
        {
            // AutoCAD 2021 with no view to draw in: a break from (60, 20) to (70, 20), the dimension moved 10 to the right, is
            // kept from (70, 20) to (80, 20), and the line is out between them.
            CadDocument document = Drawing(out CadCommandSession session);
            string dimension = Level(document, session);
            Run(session, "DIMBREAK", new[] { dimension }, "M", P(60, 20), P(70, 20));
            Assert.Equal(new[] { "0.18,60", "70,99.82" }, Pieces(document, dimension, 20));

            Run(session, "MOVE", new[] { dimension }, Enter, P(0, 0), P(10, 0));

            Assert.Equal(new[] { "10.18,70", "80,109.82" }, Pieces(document, dimension, 20));
            string text = document.Database.ToText().Replace("\r\n", "\n");
            Assert.Matches(
                @"AcDbBreakPointRef\n\s*71\n2\n\s*72\n0\n\s*91\n3\n\s*10\n70(\.0)?\n\s*20\n20(\.0)?\n",
                text
            );
            Assert.True(document.Undo());
            Assert.Equal(new[] { "0.18,60", "70,99.82" }, Pieces(document, dimension, 20));
        }

        [Fact]
        public void ALineAcrossTheArcOfAnAngularDimension_BreaksTheArc_AsAutoCad2021Does()
        {
            // AutoCAD 2021: the angle from 0 to 90 degrees on an arc of 84.85 and a line from (50, 50) to (100, 100) across it at 45
            // degrees, a gap of 0.125 along the arc about it: two arcs from 0.002121 to 0.784662 and from 0.786135 to 1.568675. A circle
            // of 5 about (70, 50) across it at 33 and 39 degrees leaves three.
            CadDocument document = Drawing(out CadCommandSession session);
            Run(session, "DIMANGULAR", Enter, P(0, 0), P(50, 0), P(0, 50), P(60, 60));
            string dimension = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace)).Handle;
            string line = Line(document, 50, 50, 100, 100);
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            List<DxfArc> arcs = ArcsOf(document, dimension);
            Assert.Equal(2, arcs.Count);
            Assert.Equal(0.784662, arcs[0].EndAngle, 4);
            Assert.Equal(0.786135, arcs[1].StartAngle, 4);
            Assert.Equal(Math.PI / 4.0, (arcs[0].EndAngle + arcs[1].StartAngle) / 2.0, 6);
            Assert.Equal(1.568675, arcs[1].EndAngle, 4);

            Run(session, "DIMBREAK", new[] { dimension }, "R");
            Run(session, "ERASE", new[] { line }, Enter);
            document.Database.ModelSpace.AppendEntity(
                new DxfCircle(new GeoPoint3(70, 50, 0), new GeoVector3(0, 0, 1), 5)
            );
            Run(session, "DIMBREAK", new[] { dimension }, "A");

            arcs = ArcsOf(document, dimension);
            Assert.Equal(3, arcs.Count);
            Assert.Equal(0.562608, arcs[0].EndAngle, 4);
            Assert.Equal(0.564081, arcs[1].StartAngle, 4);
            Assert.Equal(0.676418, arcs[1].EndAngle, 4);
            Assert.Equal(0.677891, arcs[2].StartAngle, 4);
        }

        [Fact]
        public void ALineAcrossTheArcOfAnArcLengthDimension_BreaksTheArc_AndAcrossItsRay_TheRay()
        {
            // AutoCAD 2021: an arc length dimension on an arc of 60 and a line across it at 45 degrees leaves two arcs a gap of
            // 0.125 / 60 apart about that angle; a line across the ray at x 55 breaks the ray from 54.938 to 55.062.
            CadDocument document = Drawing(out CadCommandSession session);
            document.Database.ModelSpace.AppendEntity(new DxfArc(new GeoPoint3(0, 0, 0), 50.0, 0.0, Math.PI / 2.0));
            string arc = All<DxfArc>(document.ModelSpace).Single().Handle;
            Run(session, "DIMARC", new[] { arc }, P(42.4264, 42.4264));
            string dimension = Assert.Single(All<DxfArcDimension>(document.ModelSpace)).Handle;
            double radius = ArcsOf(document, dimension)[0].Radius;
            Line(document, 40, 40, 70, 70);
            Line(document, 55, -10, 55, 10);

            Run(session, "DIMBREAK", new[] { dimension }, "A");

            List<DxfArc> arcs = ArcsOf(document, dimension);
            Assert.Equal(2, arcs.Count);
            Assert.Equal(0.125 / radius, arcs[1].StartAngle - arcs[0].EndAngle, 6);
            Assert.Equal(Math.PI / 4.0, (arcs[0].EndAngle + arcs[1].StartAngle) / 2.0, 6);
            Assert.Contains(
                ((DxfDimension)document.FindEntity(dimension)).DimBlock.OfType<DxfLine>().Where(l => !l.IsErased),
                l =>
                    Math.Abs(l.StartPoint.Y) < 1E-6
                    && Math.Abs(l.EndPoint.Y) < 1E-6
                    && Math.Abs(Math.Max(l.StartPoint.X, l.EndPoint.X) - 54.9375) < 1E-3
            );
        }

        private static List<DxfArc> ArcsOf(CadDocument document, string handle) =>
            ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfArc>()
                .Where(a => !a.IsErased && a.Radius > 1.0)
                .OrderBy(a => a.StartAngle)
                .ToList();

        [Fact]
        public void ALineAcrossTheLeaderOfAnOrdinate_BreaksItAsAutoCad2021Does()
        {
            // AutoCAD 2021: the leader of the X ordinate at (20, 30) running to (20, 60.09), crossed by a line at y 45, is broken
            // from 44.938 to 45.062; a line at x 40, clear of its box, is not named.
            CadDocument document = Drawing(out CadCommandSession session);
            Run(session, "DOR", P(20, 30), P(20, 60));
            string ordinate = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace)).Handle;
            string crossing = Line(document, 10, 45, 30, 45);
            Line(document, 40, 45, 45, 45);

            Run(session, "DIMBREAK", new[] { ordinate }, "A");

            List<DxfLine> leader = ((DxfDimension)document.FindEntity(ordinate))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && Math.Abs(l.StartPoint.X - 20) < 1E-6 && Math.Abs(l.EndPoint.X - 20) < 1E-6)
                .OrderBy(l => Math.Min(l.StartPoint.Y, l.EndPoint.Y))
                .ToList();
            Assert.Equal(2, leader.Count);
            Assert.Equal(44.9375, Math.Max(leader[0].StartPoint.Y, leader[0].EndPoint.Y), 3);
            Assert.Equal(45.0625, Math.Min(leader[1].StartPoint.Y, leader[1].EndPoint.Y), 3);
            Assert.Equal(new[] { crossing }, Named(document));
        }
    }
}
