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
    /// TXTEXP, the Express Tools command, as AutoCAD 2021's own TXTEXP.LSP words it: "N text object(s) have been exploded to
    /// lines." and "The line objects have been placed on layer 0." An SHX font's strokes become open polylines and a TrueType
    /// font's outlines closed ones; Arial's capital I is the four corners of its stem, its O a ring and a hole.
    /// </summary>
    public class TextExplodeTests
    {
        private static CadDocument Drawing(string fileName, DxfEntity text)
        {
            CadDocument document = Blank();
            document.Database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "OWN", FileName = fileName });
            document.ApplyEdit("draw", null, s => s.Append(document.ModelSpace, text));

            // An SHX font not found is drawn in the presenter's own, which the strokes are made from.
            if (fileName.EndsWith(".shx", StringComparison.Ordinal))
            {
                document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);
            }

            return document;
        }

        private static DxfDBText Text(string words, double x, double y, double height) =>
            new DxfDBText
            {
                TextString = words,
                Position = new GeoPoint3(x, y, 0),
                Height = height,
                TextStyleName = "OWN",
                Layer = "0",
            };

        private static void Box(
            IEnumerable<DxfPolyline> polylines,
            out double left,
            out double bottom,
            out double right,
            out double top
        )
        {
            left = bottom = double.MaxValue;
            right = top = double.MinValue;

            foreach (DxfPolyline polyline in polylines)
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    GeoPoint2 p = polyline.GetPoint2dAt(i);
                    left = Math.Min(left, p.X);
                    right = Math.Max(right, p.X);
                    bottom = Math.Min(bottom, p.Y);
                    top = Math.Max(top, p.Y);
                }
            }
        }

        [Fact]
        public void AnShxText_BecomesOpenPolylinesOnLayerZero_AndTheRunSaysSoAsAutoCadDoes()
        {
            DxfDBText text = Text("AB", 100, 50, 10);
            CadDocument document = Drawing("romans.shx", text);
            string handle = text.Handle;
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP");
            Assert.Contains("Select text to be EXPLODED: ", session.History);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(All<DxfDBText>(document.ModelSpace));
            List<DxfPolyline> lines = All<DxfPolyline>(document.ModelSpace);
            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.False(l.Closed));
            Assert.All(lines, l => Assert.Equal("0", l.Layer));
            Box(lines, out double left, out double bottom, out double right, out double top);
            Assert.InRange(left, 99.0, 101.0);
            Assert.InRange(bottom, 49.0, 51.0);
            Assert.InRange(top, 59.0, 61.0);
            Assert.InRange(right, 110.0, 125.0);
            Assert.Contains("1 text object(s) have been exploded to lines.", session.History);
            Assert.Contains("The line objects have been placed on layer 0.", session.History);
            Assert.Equal("TXTEXP", document.NextUndo.Name);
        }

        [Fact]
        public void ATrueTypeCapitalI_IsOneClosedPolylineOfFourCorners_TheHeightOfTheText()
        {
            DxfDBText text = Text("I", 0, 0, 10);
            CadDocument document = Drawing("arial.ttf", text);
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP", new[] { text.Handle }, Enter);

            DxfPolyline stem = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(stem.Closed);
            Assert.Equal(4, stem.NumberOfVertices);
            Box(new[] { stem }, out double left, out double bottom, out double right, out double top);
            Assert.Equal(0.0, bottom, 2);
            Assert.Equal(10.0, top, 1);
            Assert.InRange(right - left, 1.0, 1.5);
        }

        [Fact]
        public void ATrueTypeO_IsTheRingAndItsHole_EachClosed_AndATextTurnedAndRaisedFollowsItsPlace()
        {
            DxfDBText text = Text("O", 100, 200, 10);
            text.Rotation = Math.PI / 2.0;
            CadDocument document = Drawing("arial.ttf", text);
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP", new[] { text.Handle }, Enter);

            List<DxfPolyline> rings = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(2, rings.Count);
            Assert.All(rings, r => Assert.True(r.Closed));
            Assert.All(rings, r => Assert.True(r.NumberOfVertices > 8));

            // Turned a quarter, the letter's height runs along x to the left of its base point, its width up y.
            Box(rings, out double left, out double bottom, out double right, out double top);
            Assert.InRange(right, 99.0, 101.0);
            Assert.InRange(left, 89.0, 91.0);
            Assert.InRange(bottom, 200.0, 201.0);
            Assert.InRange(top, 207.0, 211.0);
        }

        [Fact]
        public void AnAccentedLetterOfArial_IsItsLetterAndItsAccent_ThePartsOfACompositeLetterFollowed()
        {
            DxfDBText plain = Text("e", 0, 0, 10);
            DxfDBText accented = Text("é", 0, 0, 10);
            CadDocument one = Drawing("arial.ttf", plain);
            CadDocument two = Drawing("arial.ttf", accented);

            Run(Session(one), "TXTEXP", new[] { plain.Handle }, Enter);
            Run(Session(two), "TXTEXP", new[] { accented.Handle }, Enter);

            // The e is its outline and its eye; the accent is one more outline, above it.
            Assert.Equal(2, All<DxfPolyline>(one.ModelSpace).Count);
            List<DxfPolyline> parts = All<DxfPolyline>(two.ModelSpace);
            Assert.Equal(3, parts.Count);
            Box(parts, out _, out double bottom, out _, out double top);
            Box(All<DxfPolyline>(one.ModelSpace), out _, out _, out _, out double topOfE);
            Assert.True(top > topOfE + 1.0);
            Assert.Equal(0.0, bottom, 0);
        }

        [Fact]
        public void ALetterTheFontLacks_IsExplodedFromAFontThatHasIt_AsArialsOwnIs()
        {
            // The Vietnamese o with a circumflex and a tilde is not in Malgun Gothic (a Korean font, like Gulim); Arial has it.
            DxfDBText korean = Text("ỗ", 5, 5, 10);
            DxfDBText arial = Text("ỗ", 5, 5, 10);
            CadDocument one = Drawing("malgun.ttf", korean);
            CadDocument two = Drawing("arial.ttf", arial);
            CadCommandSession session = Session(one);

            Run(session, "TXTEXP", new[] { korean.Handle }, Enter);
            Run(Session(two), "TXTEXP", new[] { arial.Handle }, Enter);

            Assert.Empty(All<DxfDBText>(one.ModelSpace));
            List<DxfPolyline> drawn = All<DxfPolyline>(one.ModelSpace);
            List<DxfPolyline> wanted = All<DxfPolyline>(two.ModelSpace);
            Assert.True(wanted.Count >= 3);
            Assert.Equal(wanted.Count, drawn.Count);
            Box(drawn, out double left, out double bottom, out double right, out double top);
            Box(wanted, out double wantedLeft, out double wantedBottom, out double wantedRight, out double wantedTop);
            Assert.Equal(wantedLeft, left, 6);
            Assert.Equal(wantedBottom, bottom, 6);
            Assert.Equal(wantedRight, right, 6);
            Assert.Equal(wantedTop, top, 6);
            Assert.Contains("1 text object(s) have been exploded to lines.", session.History);
        }

        [Fact]
        public void ALineOfLettersTheFontHasAndLacks_IsExplodedWithBoth_TheLettersAfterOneBeyondItsAdvance()
        {
            // "L" and "i" of Malgun Gothic with the letter it lacks between them: the outlines of the three.
            DxfDBText line = Text("Lỗi", 0, 0, 10);
            DxfDBText ends = Text("Li", 0, 0, 10);
            DxfDBText middle = Text("ỗ", 0, 0, 10);
            CadDocument whole = Drawing("malgun.ttf", line);
            CadDocument withoutMiddle = Drawing("malgun.ttf", ends);
            CadDocument alone = Drawing("malgun.ttf", middle);

            Run(Session(whole), "TXTEXP", new[] { line.Handle }, Enter);
            Run(Session(withoutMiddle), "TXTEXP", new[] { ends.Handle }, Enter);
            Run(Session(alone), "TXTEXP", new[] { middle.Handle }, Enter);

            List<DxfPolyline> all = All<DxfPolyline>(whole.ModelSpace);
            List<DxfPolyline> two = All<DxfPolyline>(withoutMiddle.ModelSpace);
            List<DxfPolyline> one = All<DxfPolyline>(alone.ModelSpace);
            Assert.NotEmpty(one);
            Assert.Equal(two.Count + one.Count, all.Count);
            Box(all, out _, out _, out double right, out _);
            Box(two, out _, out _, out double rightOfTwo, out _);

            // The i is set after the middle letter, further right than it stands in "Li".
            Assert.True(right > rightOfTwo + 1.0, "The line is wider than Li by the middle letter.");
        }

        [Fact]
        public void AnMTextIsBrokenIntoItsTextsFirst_AndEveryLineBecomesPolylines()
        {
            var mtext = new DxfMText
            {
                Contents = "AB\\PCD",
                Location = new GeoPoint3(0, 100, 0),
                TextHeight = 10,
            };
            mtext.TextStyleName = "OWN";
            CadDocument document = Drawing("romans.shx", mtext);
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP", new[] { mtext.Handle }, Enter);

            Assert.Empty(All<DxfMText>(document.ModelSpace));
            Assert.Empty(All<DxfDBText>(document.ModelSpace));
            List<DxfPolyline> lines = All<DxfPolyline>(document.ModelSpace);
            Assert.NotEmpty(lines);
            Box(lines, out _, out double bottom, out _, out double top);
            Assert.True(top - bottom > 15.0);
            Assert.Contains("1 text object(s) have been exploded to lines.", session.History);
        }

        [Fact]
        public void AFontWhoseFileIsNotFound_LeavesTheTextAsItIs_AndSaysSo()
        {
            DxfDBText text = Text("AB", 0, 0, 10);
            CadDocument document = Drawing("nofont.ttf", text);
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP", new[] { text.Handle }, Enter);

            Assert.Single(All<DxfDBText>(document.ModelSpace));
            Assert.Empty(All<DxfPolyline>(document.ModelSpace));
            Assert.Contains(
                "1 text object(s) could not be exploded: their font has no outlines to read here.",
                session.History
            );
        }

        [Fact]
        public void OtherObjectsAreNotTaken_AndAnAttributeIsNoText()
        {
            DxfDBText text = Text("I", 0, 0, 10);
            CadDocument document = Drawing("arial.ttf", text);
            document.ApplyEdit("draw", null, s =>
                s.Append(document.ModelSpace, new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5.0))
            );
            CadCommandSession session = Session(document);

            Run(session, "TXTEXP", "ALL", Enter);

            Assert.Single(All<DxfCircle>(document.ModelSpace));
            Assert.Single(All<DxfPolyline>(document.ModelSpace));
        }
    }
}
