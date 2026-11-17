using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A dimension that keeps breaks, scaled or copied, against AutoCAD 2021's console run through standard input (16
    /// November). A level dimension from (0, 0) to (100, 0), its line at y 20, scaled by 2 is drawn again: its extension lines
    /// run from 0.0625 off its points to 0.18 past its line at y 40, its offsets and arrowheads as its style sets them, not
    /// scaled with it. Copied, it has a BREAKDATA of its own naming the same objects, and is broken where those cross it now.
    /// </summary>
    public class DimBreakScaleCopyTests
    {
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

        /// <summary>The lines of a dimension's picture as "x1,y1,x2,y2" each, the lesser end first, rounded to 1E-4, in order.</summary>
        private static List<string> Lines(CadDocument document, string handle) =>
            ((DxfDimension)document.FindEntity(handle))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
                .Select(l =>
                {
                    bool swap =
                        l.StartPoint.X > l.EndPoint.X + 1E-9
                        || (Math.Abs(l.StartPoint.X - l.EndPoint.X) <= 1E-9 && l.StartPoint.Y > l.EndPoint.Y);
                    GeoPoint3 a = swap ? l.EndPoint : l.StartPoint;
                    GeoPoint3 b = swap ? l.StartPoint : l.EndPoint;
                    return Rounded(a.X) + "," + Rounded(a.Y) + "," + Rounded(b.X) + "," + Rounded(b.Y);
                })
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();

        private static string Rounded(double value) =>
            (Math.Round(value, 4) + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The BREAKDATAs of the drawing: for each, the handles it names (331).</summary>
        private static List<List<string>> BreakDatas(CadDocument document)
        {
            string text = document.Database.ToText().Replace("\r\n", "\n");
            var found = new List<List<string>>();

            foreach (Match start in Regex.Matches(text, "\n  0\nBREAKDATA\n"))
            {
                int end = text.IndexOf("\n  0\n", start.Index + 5, StringComparison.Ordinal);
                found.Add(
                    Regex
                        .Matches(text.Substring(start.Index, end - start.Index), @"\n331\n([0-9A-F]+)\n")
                        .Cast<Match>()
                        .Select(m => m.Groups[1].Value)
                        .ToList()
                );
            }

            return found;
        }

        [Fact]
        public void ADimensionBrokenByHand_ScaledByTwo_IsDrawnAgain_ItsOffsetsAsItsStyleSetsThem()
        {
            // AutoCAD 2021: the extension lines from (0, 0.0625) to (0, 40.18) and from (200, 0.0625) to (200, 40.18), the arrowheads
            // 0.18 long; the picture scaled with the dimension had them from 0.125 to 40.36. The break from (20, 20) to (25, 20)
            // goes with the dimension, to (40, 40) and (50, 40).
            CadDocument document = Drawing(out CadCommandSession session);
            string handle = Level(document, session);
            Run(session, "DIMBREAK", new[] { handle }, "M", P(20, 20), P(25, 20));

            Run(session, "SCALE", new[] { handle }, Enter, P(0, 0), "2");

            List<string> lines = Lines(document, handle);
            Assert.Contains("0,0.0625,0,40.18", lines);
            Assert.Contains("200,0.0625,200,40.18", lines);
            Assert.Contains("0.18,40,40,40", lines);
            Assert.Contains("50,40,199.82,40", lines);
        }

        [Fact]
        public void ABreakByHandAtAnExtensionLine_ScaledAboutItsPoint_IsDrawnAgain_WithTheBreakWhereTheScaleTakesIt()
        {
            // AutoCAD 2021 (sc4): the break from (0, 10) to (0, 15) of the first extension line, scaled by 2 about (0, 0), is at (0, 20)
            // and (0, 30) on the line from (0, 0.0625) to (0, 40.18).
            CadDocument document = Drawing(out CadCommandSession session);
            string handle = Level(document, session);
            Run(session, "DIMBREAK", new[] { handle }, "M", P(0, 10), P(0, 15));

            Run(session, "SCALE", new[] { handle }, Enter, P(0, 0), "2");

            List<string> lines = Lines(document, handle);
            Assert.Contains("0,0.0625,0,20", lines);
            Assert.Contains("0,30,0,40.18", lines);
            Assert.Contains("200,0.0625,200,40.18", lines);
        }

        [Fact]
        public void ACopyOfADimensionBrokenAtALine_HasABreakDataOfItsOwnNamingTheLine_AndIsWholeWhereTheLineDoesNotCrossIt()
        {
            // AutoCAD 2021 (cp3): a line at x 30 from y -5 to 50 breaks the dimension line at x 30; the dimension copied 100 to the
            // right has its own BREAKDATA naming the line, and its line at y 20 whole, from 100.18 to 199.82, the line not
            // crossing it. A line at x 130 the BREAKDATA does not name breaks nothing.
            CadDocument document = Drawing(out CadCommandSession session);
            string handle = Level(document, session);
            string named = Line(document, 30, -5, 30, 50);
            Line(document, 130, -5, 130, 50);
            Run(session, "DIMBREAK", new[] { handle }, "A");

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(100, 0), Enter);

            DxfDimension[] both = All<DxfRotatedDimension>(document.ModelSpace).ToArray();
            Assert.Equal(2, both.Length);
            DxfDimension copy = both.Single(d => d.Handle != handle);
            Assert.Contains("100.18,20,199.82,20", Lines(document, copy.Handle));
            Assert.Contains("0.18,20,29.9375,20", Lines(document, handle));
            List<List<string>> datas = BreakDatas(document);
            Assert.Equal(2, datas.Count);
            Assert.All(datas, d => Assert.Equal(new[] { named }, d));
        }

        [Fact]
        public void ACopyOfADimensionBrokenAtALine_MovedAcrossTheLine_IsBrokenWhereTheLineCrossesIt()
        {
            // The line at x 30 crosses the line of the copy at y 30 where the copy is moved 10 up: broken there, 0.0625 either side.
            CadDocument document = Drawing(out CadCommandSession session);
            string handle = Level(document, session);
            Line(document, 30, -5, 30, 50);
            Run(session, "DIMBREAK", new[] { handle }, "A");

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(0, 10), Enter);

            DxfDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);
            List<string> spans = ((DxfDimension)document.FindEntity(copy.Handle))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && Math.Abs(l.StartPoint.Y - l.EndPoint.Y) < 1E-9)
                .Select(l =>
                    Rounded(Math.Min(l.StartPoint.X, l.EndPoint.X))
                    + ","
                    + Rounded(Math.Max(l.StartPoint.X, l.EndPoint.X))
                )
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            Assert.Equal(new[] { "0.18,29.9375", "30.0625,99.82" }, spans);
        }

        [Fact]
        public void ACopyOfADimensionBrokenByHand_KeepsTheBreakWhereTheCopyStands_InABreakDataOfItsOwn()
        {
            // The break from (20, 20) to (25, 20) goes with the copy, 50 down, to (20, -30) and (25, -30), as it goes with a move.
            CadDocument document = Drawing(out CadCommandSession session);
            string handle = Level(document, session);
            Run(session, "DIMBREAK", new[] { handle }, "M", P(20, 20), P(25, 20));

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(0, -50), Enter);

            DxfDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);
            List<string> lines = Lines(document, copy.Handle);
            Assert.Contains("0.18,-30,20,-30", lines);
            Assert.Contains("25,-30,99.82,-30", lines);
            Assert.Equal(2, BreakDatas(document).Count);
        }

        [Fact]
        public void AnArcLengthDimensionOfWordsOfNoHeight_DrawsNoArcOfNoRadius()
        {
            // With no words there is no arc symbol: the symbol is half a circle of 0.8 times the words' height, which was no shape.
            CadDocument document = Drawing(out CadCommandSession session);
            document.Database.ModelSpace.AppendEntity(new DxfArc(new GeoPoint3(0, 0, 0), 50.0, 0.0, Math.PI / 2.0));
            string arc = All<DxfArc>(document.ModelSpace).Single().Handle;

            Run(session, "DIMARC", new[] { arc }, P(42.4264, 42.4264));

            var dimension = Assert.Single(All<DxfArcDimension>(document.ModelSpace));
            DxfArc[] arcs = dimension.DimBlock.OfType<DxfArc>().Where(a => !a.IsErased).ToArray();
            Assert.Single(arcs);
            Assert.True(arcs[0].Radius > 1.0);
        }
    }
}
