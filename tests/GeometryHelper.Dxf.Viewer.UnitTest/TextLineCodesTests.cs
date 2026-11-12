using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The lines a TEXT's codes draw in an SHX font, as AutoCAD 2021 draws them (probed in its console on simplex and txt
    /// 10 high): <c>%%U</c> 0.2 of the height below the baseline, <c>%%K</c> half the height above it and <c>%%O</c> 1.2
    /// heights above it, each along the pens of the letters it lines but 0.15 of the height left of them, the words boxed
    /// with the line where they are justified; the codes themselves are not drawn. The font is 10 above its baseline: an I
    /// moves the pen 4, so that "II" 10 high moves it 8.
    /// </summary>
    public sealed class TextLineCodesTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private DisplayText2 Shown(string words, DxfTextHorizontalMode mode = DxfTextHorizontalMode.TextLeft)
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            var text = new DxfDBText
            {
                TextString = words,
                Position = new GeoPoint3(0, 0, 0),
                Height = 10.0,
            };
            database.ModelSpace.AppendEntity(text);
            text.TextStyleName = "GH_SHX";

            if (mode != DxfTextHorizontalMode.TextLeft)
            {
                text.HorizontalMode = mode;
                text.AlignmentPoint = new GeoPoint3(0, 0, 0);
            }

            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return Assert.IsType<DisplayText2>(Assert.Single(document.GetDisplay(document.ModelSpace).Items));
        }

        /// <summary>The level strokes of a text: its lines, each as from x, to x and y.</summary>
        private static List<double[]> Level(DisplayText2 shown) =>
            shown
                .Strokes.Where(s => s.Length == 4 && Math.Abs(s[1] - s[3]) < 1E-9)
                .Select(s => new[] { s[0], s[2], s[1] })
                .ToList();

        private static void Line(double from, double to, double y, double[] line)
        {
            Assert.Equal(from, line[0], 9);
            Assert.Equal(to, line[1], 9);
            Assert.Equal(y, line[2], 9);
        }

        [Fact]
        public void UnderOverAndThrough_AreDrawnAlongTheLettersPens_015OfTheHeightLeftOfThem()
        {
            DisplayText2 plain = Shown("II");
            Assert.Empty(Level(plain));

            DisplayText2 under = Shown("%%uII");
            Assert.Equal("II", under.Text);
            Assert.Equal(plain.Strokes.Count + 1, under.Strokes.Count);
            Line(-1.5, 6.5, -2.0, Assert.Single(Level(under)));

            Line(-1.5, 6.5, 12.0, Assert.Single(Level(Shown("%%OII"))));
            Line(-1.5, 6.5, 5.0, Assert.Single(Level(Shown("%%kII"))));

            // A code turns its line off again: "I%%uI%%uI" lines the middle I alone, from its pen at 4.
            Line(2.5, 6.5, -2.0, Assert.Single(Level(Shown("I%%uI%%uI"))));
        }

        [Fact]
        public void ATextJustifiedWithALine_IsBoxedWithIt()
        {
            // The ink of "II" and the line from -1.5 to 6.5, centred on nought.
            DisplayText2 left = Shown("II");
            double inkLeft = left.Strokes.Min(s => Math.Min(s[0], s[s.Length - 2]));
            double inkRight = left.Strokes.Max(s => Math.Max(s[0], s[s.Length - 2]));
            double boxLeft = Math.Min(inkLeft, -1.5);
            double boxRight = Math.Max(inkRight, 6.5);
            double shift = -(boxLeft + boxRight) / 2.0;

            DisplayText2 centred = Shown("%%uII", DxfTextHorizontalMode.TextCenter);
            Line(shift - 1.5, shift + 6.5, -2.0, Assert.Single(Level(centred)));

            DisplayText2 right = Shown("%%uII", DxfTextHorizontalMode.TextRight);
            Line(-boxRight - 1.5, -boxRight + 6.5, -2.0, Assert.Single(Level(right)));
        }
    }
}
