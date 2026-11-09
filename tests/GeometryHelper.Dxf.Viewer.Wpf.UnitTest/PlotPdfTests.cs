using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// GeometryHelper PDF (C15): a plot written as one PDF page of the sheet's exact size, its text found through the font's
    /// ToUnicode map, its lines at their lineweights, its colours through the plot style table, its solids filled, and a
    /// cross-reference table that still points at every object after the page is given its exact size.
    /// </summary>
    public class PlotPdfTests
    {
        /// <summary>
        /// A plan of model space: a red frame from (120, 60) to (7 620, 5 360) of lineweight 0.5, ROOM 1 in Arial 250 high,
        /// and a solid 1 000 by 500 at (5 000, 1 000).
        /// </summary>
        internal static CadDocument Plan()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable["Standard"].Font = new DxfFontDescriptor("Arial", false, false, 0, 34);
            var corners = new[] { new GeoPoint3(120, 60, 0), new GeoPoint3(7620, 60, 0), new GeoPoint3(7620, 5360, 0), new GeoPoint3(120, 5360, 0) };

            for (int i = 0; i < 4; i++)
            {
                var line = new DxfLine(corners[i], corners[(i + 1) % 4]);
                database.ModelSpace.AppendEntity(line);
                line.ColorIndex = 1;
                line.LineWeight = DxfLineWeight.LineWeight050;
            }

            database.ModelSpace.AppendEntity(new DxfDBText { TextString = "ROOM 1", Position = new GeoPoint3(1500, 2500, 0), Height = 250 });
            database.ModelSpace.AppendEntity(new DxfSolid(new GeoPoint3(5000, 1000, 0), new GeoPoint3(6000, 1000, 0), new GeoPoint3(5000, 1500, 0), new GeoPoint3(6000, 1500, 0)));
            return CadDocument.From(database, "plan-level2.dxf");
        }

        internal static CadPlotSettings AtOneToTwenty()
            => new CadPlotSettings { Area = CadPlotArea.Window, Window = new DisplayBox(120, 60, 7620, 5360), FitToPaper = false, PaperUnits = 1, DrawingUnits = 20 };

        private static byte[] Written(CadDocument document, CadPlotSettings settings)
        {
            using (var memory = new MemoryStream())
            {
                CadPdfWriter.Write(memory, document, document.ModelSpace, settings, DisplayBox.Empty);
                return memory.ToArray();
            }
        }

        [Fact]
        public void APlanOnA3OnItsSide_IsAPageOf1190Point55By841Point89Points_WhoseTextIsFound()
        {
            CadDocument document = Plan();
            Assert.Contains(document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>(), t => t.Strokes == null && t.Text == "ROOM 1");

            byte[] pdf = Written(document, AtOneToTwenty());
            string text = Latin(pdf);

            Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
            Match box = Regex.Match(text, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]");
            Assert.True(box.Success, "No MediaBox.");
            Assert.Equal(1190.55, double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture), 2);
            Assert.Equal(841.89, double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture), 2);
            Assert.Single(Regex.Matches(text, @"/Type /Page\b").Cast<Match>());
            Assert.Contains("ROOM 1", Pdf.Text(pdf));
            Pdf.AssertCrossReferencesPointAtTheirObjects(pdf);
        }

        [Fact]
        public void A4Upright_IsAPageOf595Point28By841Point89Points()
        {
            CadPlotSettings settings = AtOneToTwenty();
            settings.Paper = CadPaper.Standard[4];
            settings.Landscape = false;
            settings.FitToPaper = true;

            string text = Latin(Written(Plan(), settings));

            Match box = Regex.Match(text, @"/MediaBox \[0 0 ([0-9.]+) ([0-9.]+)\]");
            Assert.Equal(595.28, double.Parse(box.Groups[1].Value, CultureInfo.InvariantCulture), 2);
            Assert.Equal(841.89, double.Parse(box.Groups[2].Value, CultureInfo.InvariantCulture), 2);
        }

        [Fact]
        public void LinesKeepTheirLineweight_TheThinnestWhereLineweightsAreOff_AndMonochromePlotsTheRedFrameBlack()
        {
            CadDocument document = Plan();
            CadPlotSettings settings = AtOneToTwenty();

            byte[] pdf = Written(document, settings);
            string content = string.Join("\n", Pdf.Streams(pdf));
            string text = Latin(pdf);

            // 0.5 mm is 1.4173 points, set in a graphics state of the page.
            Assert.Matches(@"/LW 1\.417", text);
            Assert.Contains("0 0 0 RG", content);
            Assert.DoesNotContain("1 0 0 RG", content);

            // The solid filled.
            Assert.Matches(@"\n(f|f\*)\n", content);

            settings.StyleTable = CadPlotStyleTable.None;
            settings.PlotLineweights = false;
            pdf = Written(document, settings);
            content = string.Join("\n", Pdf.Streams(pdf));
            Assert.Contains("1 0 0 RG", content);
            Assert.DoesNotMatch(@"/LW 1\.417", Latin(pdf));
        }

        [Fact]
        public void SixtyFourPdfsWrittenOnEightThreadsAtOnce_EachKeepsItsText()
        {
            int lost = 0;

            System.Threading.Tasks.Parallel.For(0, 64, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                CadDocument document = Plan();

                if (!Pdf.Text(Written(document, AtOneToTwenty())).Contains("ROOM 1"))
                {
                    System.Threading.Interlocked.Increment(ref lost);
                }
            });

            Assert.Equal(0, lost);
        }

        [Fact]
        public void NothingToPlot_WritesNoPdf()
        {
            var document = CadDocument.From(new DxfDatabase("AC1027"), "empty.dxf");
            var settings = new CadPlotSettings { Area = CadPlotArea.Extents };

            Assert.Throws<InvalidOperationException>(() => CadPdfWriter.Write(new MemoryStream(), document, document.ModelSpace, settings, DisplayBox.Empty));
        }

        private static string Latin(byte[] bytes) => Encoding.GetEncoding(28591).GetString(bytes);
    }

    /// <summary>
    /// What a reader takes out of a PDF, enough for the tests: its streams inflated, and the text of its pages decoded through
    /// the ToUnicode maps of its fonts, as a reader's search and copy find it.
    /// </summary>
    internal static class Pdf
    {
        /// <summary>The streams of the file, those of FlateDecode inflated, as Latin-1 text.</summary>
        public static List<string> Streams(byte[] pdf)
        {
            Encoding latin = Encoding.GetEncoding(28591);
            string text = latin.GetString(pdf);
            var streams = new List<string>();

            foreach (Match match in Regex.Matches(text, @"/Length (\d+)(?:(?!endobj).)*?>>\s*stream\r?\n", RegexOptions.Singleline))
            {
                int length = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int start = match.Index + match.Length;
                byte[] data = pdf.Skip(start).Take(length).ToArray();
                int head = text.LastIndexOf(" obj", match.Index, StringComparison.Ordinal);
                bool deflated = head >= 0 && text.IndexOf("/FlateDecode", head, match.Index + match.Length - head, StringComparison.Ordinal) >= 0;

                if (deflated && data.Length > 2)
                {
                    using (var input = new MemoryStream(data, 2, data.Length - 2))
                    using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        inflate.CopyTo(output);
                        data = output.ToArray();
                    }
                }

                streams.Add(latin.GetString(data));
            }

            return streams;
        }

        /// <summary>The text the pages show, each string of a Tj or TJ decoded through the ToUnicode maps, the strings joined.</summary>
        public static string Text(byte[] pdf)
        {
            List<string> streams = Streams(pdf);
            var map = new Dictionary<int, string>();

            foreach (string cmap in streams.Where(s => s.Contains("begincmap")))
            {
                string chars = string.Concat(Regex.Matches(cmap, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline).Cast<Match>().Select(m => m.Groups[1].Value));
                string ranges = string.Concat(Regex.Matches(cmap, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline).Cast<Match>().Select(m => m.Groups[1].Value));

                foreach (Match m in Regex.Matches(chars, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                {
                    map[Convert.ToInt32(m.Groups[1].Value, 16)] = Unicode(m.Groups[2].Value);
                }

                foreach (Match m in Regex.Matches(ranges, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                {
                    int low = Convert.ToInt32(m.Groups[1].Value, 16);
                    int high = Convert.ToInt32(m.Groups[2].Value, 16);
                    int first = Convert.ToInt32(m.Groups[3].Value, 16);

                    for (int code = low; code <= high; code++)
                    {
                        map[code] = char.ConvertFromUtf32(first + code - low);
                    }
                }
            }

            var shown = new StringBuilder();

            foreach (string content in streams.Where(s => s.Contains(" Tj") || s.Contains("TJ")))
            {
                foreach (Match m in Regex.Matches(content, @"<([0-9A-Fa-f]*)>\s*Tj|\[([^\]]*)\]\s*TJ"))
                {
                    IEnumerable<string> strings = m.Groups[1].Success && m.Value.EndsWith("Tj", StringComparison.Ordinal)
                        ? new[] { m.Groups[1].Value }
                        : Regex.Matches(m.Groups[2].Value, @"<([0-9A-Fa-f]*)>").Cast<Match>().Select(x => x.Groups[1].Value);

                    foreach (string hex in strings)
                    {
                        for (int i = 0; i + 3 < hex.Length; i += 4)
                        {
                            shown.Append(map.TryGetValue(Convert.ToInt32(hex.Substring(i, 4), 16), out string letter) ? letter : "?");
                        }
                    }

                    shown.Append('\n');
                }
            }

            return shown.ToString();
        }

        /// <summary>Asserts each object the cross-reference table lists in use is found at its offset, and startxref at the table.</summary>
        public static void AssertCrossReferencesPointAtTheirObjects(byte[] pdf)
        {
            string text = Encoding.GetEncoding(28591).GetString(pdf);
            Match start = Regex.Match(text, @"startxref\s+(\d+)\s+%%EOF\s*$");
            Assert.True(start.Success, "No startxref.");
            int xref = int.Parse(start.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.Equal("xref", text.Substring(xref, 4));
            Match head = Regex.Match(text.Substring(xref), @"^xref\s+(\d+) (\d+)\s*\n");
            int first = int.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture);
            int count = int.Parse(head.Groups[2].Value, CultureInfo.InvariantCulture);
            int at = xref + head.Length;

            for (int i = 0; i < count; i++, at += 20)
            {
                string entry = text.Substring(at, 20);

                if (entry[17] == 'n')
                {
                    int offset = int.Parse(entry.Substring(0, 10), CultureInfo.InvariantCulture);
                    Assert.StartsWith((first + i).ToString(CultureInfo.InvariantCulture) + " 0 obj", text.Substring(offset, 20), StringComparison.Ordinal);
                }
            }
        }

        private static string Unicode(string hex)
        {
            var letters = new StringBuilder();

            for (int i = 0; i + 3 < hex.Length; i += 4)
            {
                letters.Append((char)Convert.ToInt32(hex.Substring(i, 4), 16));
            }

            return letters.ToString();
        }
    }
}
