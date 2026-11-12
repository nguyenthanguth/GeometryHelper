using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The ink of SHX letters measured as AutoCAD 2021 measures it to lay out an MTEXT and a dimension's words: an arc reaches
    /// the rectangle of its chord and of the line along it touching the arc, or the square about its circle; where the pen is
    /// put down counts though it moves no further; ink left of the pen is not counted. The font is made here, 10 above its
    /// baseline, so that in a text 10 high a unit of the font is a unit of the drawing.
    /// </summary>
    [Collection(DimensionWordsFontsCollection.Name)]
    public sealed class ShxInkMeasureTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>
        /// A unifont 10 above and 2 below: Q a quarter arc of radius 4 from (0, 0), turning counter-clockwise from 270 degrees
        /// to (4, 4); H a half circle of radius 4 from (8, 4) counter-clockwise from 90 degrees to (8, -4); T the pen put down at
        /// (0, 0) and lifted, then a line from (2, 0) to (4, 4); J a line from (-2, -2) to (2, 8), left of the pen; 1 a line up
        /// from (1, 0); 0 a rectangle 4 by 10. Each moves the pen on along the baseline.
        /// </summary>
        private static byte[] Font()
        {
            var letters = new SortedDictionary<int, byte[]>
            {
                ['Q'] = new byte[] { 10, 4, 0x62, 2, 8, 2, 0xFC, 0 },
                ['H'] = new byte[] { 2, 8, 8, 4, 1, 10, 4, 0x24, 2, 8, 6, 4, 0 },
                ['T'] = new byte[] { 2, 1, 2, 8, 2, 0, 1, 8, 2, 4, 2, 8, 2, 0xFC, 0 },
                ['J'] = new byte[] { 2, 8, 0xFE, 0xFE, 1, 8, 4, 10, 2, 8, 2, 0xF8, 0 },
                ['1'] = new byte[] { 2, 0x10, 1, 0xA4, 2, 0xAC, 0x30, 0 },
                ['0'] = new byte[] { 1, 0x40, 0xA4, 0x48, 0xAC, 2, 0x60, 0 },
            };
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\x1A"));
            byte[] info = Encoding.ASCII.GetBytes("INK\0").Concat(new byte[] { 10, 2, 0, 0, 0, 0 }).ToArray();
            U16(bytes, letters.Count + 1);
            U16(bytes, 0);
            U16(bytes, info.Length);
            bytes.AddRange(info);

            foreach (KeyValuePair<int, byte[]> letter in letters)
            {
                byte[] record = new byte[] { (byte)'n', 0 }
                    .Concat(letter.Value)
                    .ToArray();
                U16(bytes, letter.Key);
                U16(bytes, record.Length);
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        private static void U16(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
        }

        private DisplayText2 Shown(string contents)
        {
            _folder.Write("ink.shx", Font());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_INK", FileName = "ink.shx" });
            var mtext = new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 10.0,
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_INK";
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
        }

        [Fact]
        public void AQuarterArc_ReachesTheRectangleOfItsChord()
        {
            // AutoCAD 2021 measures isocp.shx's 3, two quarter arcs of radius 8 of its 40 units, 0.441421 of the height wide
            // where its strokes are 0.4: a quarter arc reaches (sqrt 2 - 1) / 2 of its radius past its end, the corner of the
            // rectangle of its chord and the line along it touching the arc.
            Assert.Equal(4.0 * (1.0 + ((Math.Sqrt(2.0) - 1.0) / 2.0)), Shown("Q").EstimatedWidth, 9);
        }

        [Fact]
        public void AHalfCircleStartingAboveTheXAxis_ReachesTheSquareAboutItsCircle()
        {
            // The left half of a circle of radius 4, from 90 degrees round to 270, is measured as AutoCAD measures it, by the
            // square about its circle: 8 wide, where its strokes are 4.
            Assert.Equal(8.0, Shown("H").EstimatedWidth, 9);
        }

        [Fact]
        public void WhereThePenIsPutDown_CountsThoughItMovesNoFurther()
        {
            // As the foot of monotxt.shx's 1, which AutoCAD 2021 measures half the height wide where its strokes are a third:
            // the pen put down at (0, 0) and lifted reaches the line from (2, 0) to (4, 4) 4 wide.
            Assert.Equal(4.0, Shown("T").EstimatedWidth, 9);
        }

        [Fact]
        public void InkLeftOfThePen_IsNotCounted_AndStandsOutOfTheBox()
        {
            // As italic.shx's f, 0.809524 of the height wide in AutoCAD 2021 where its ink is 0.857143: the line from (-2, -2)
            // to (2, 8) is 2 wide from the pen, and the pen stands on the left edge of the box, the ink 2 out of it.
            DisplayText2 shown = Shown("J");
            Assert.Equal(2.0, shown.EstimatedWidth, 9);
            DisplayText2 line = shown.Parts.OfType<DisplayText2>().Single();
            Assert.Equal(-2.0, line.Strokes[0][0], 9);
        }

        [Fact]
        public void WordsInkedUpAndDownAlone_AreMeasuredNoughtWide_ForTheDimensionsPicture()
        {
            // "1" of the font is a line up: AutoCAD 2021 measures such words nought wide ("I" of isocp.shx), so the line a
            // dimension's words stand on is broken a gap either side of their middle, not 0.7 of their height a letter.
            _folder.Write("ink.shx", Font());
            CadDocument document = Blank();
            document.Database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_INK", FileName = "ink.shx" });
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimscale = 1.0;
            style.Dimasz = 2.5;
            style.Dimtxt = 2.5;
            style.Dimgap = 0.625;
            style.Dimdec = 0;
            style.Dimtad = 0;
            style.Dimlfac = 0.01;
            style.Dimtxsty = document.Database.TextStyleTable["GH_INK"];
            StandardCurrent(document.Database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);

            try
            {
                CadCommandSession session = Session(document);
                Run(session, "DLI", new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(50, -20));
                DxfDimension dimension = document.ModelSpace.Block.OfType<DxfDimension>().Last();
                Assert.Equal("1", dimension.DimBlock.OfType<DxfMText>().Single().Contents);
                Assert.Contains(dimension.DimBlock.OfType<DxfLine>(), l =>
                    Near(l.StartPoint, 2.5, -20) && Near(l.EndPoint, 50 - 0.625, -20)
                    || Near(l.EndPoint, 2.5, -20) && Near(l.StartPoint, 50 - 0.625, -20)
                );
            }
            finally
            {
                // The fonts a dimension is measured in are those last given any document: put them off again.
                document.Fonts = new CadFonts(null, null, null);
            }
        }

        private static bool Near(GeoPoint3 point, double x, double y) =>
            Math.Abs(point.X - x) < 1E-3 && Math.Abs(point.Y - y) < 1E-3;
    }

    /// <summary>
    /// The tests that measure a dimension's words in an SHX font of their own, run with no other test beside them: the fonts a
    /// dimension is measured in are those last given any document, and another test giving its own would hide the font.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class DimensionWordsFontsCollection
    {
        /// <summary>The name of the collection.</summary>
        public const string Name = "Dimension words fonts";
    }
}
