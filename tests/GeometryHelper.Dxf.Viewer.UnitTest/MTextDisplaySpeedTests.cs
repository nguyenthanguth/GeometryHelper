using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The layout of an MTEXT is kept from one drawing of it to the next, so that a drawing drawn again from the start (REGEN)
    /// does not lay out again the words of MTEXTs that have not changed, and an MTEXT whose words, sizes, style or fonts have
    /// changed is laid out again.
    /// </summary>
    [Collection(ViewerSpeedCollection.Name)]
    public class MTextDisplaySpeedTests
    {
        private const int Runs = 3;
        private readonly ITestOutputHelper _output;

        public MTextDisplaySpeedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// 10 000 MTEXTs of 3 to 14 words, a third unwrapped, half in a TrueType style, a tenth with a taller red run and a
        /// stack. A new document over the drawing lays out every MTEXT; the document drawn again after a REGEN takes the
        /// layouts it kept. Before they were kept the two cost the same (196 and 197 ms under .NET Framework 4.8); kept, the
        /// REGEN costs 0.55 of the new document (128 and 233 ms). The limit is 0.8, each the best of three runs.
        /// </summary>
        [Fact]
        public void TenThousandMTextsDrawnAgainAfterARegen_TakeTheLayoutsKeptAndCostLessThanANewDocument()
        {
            DxfDatabase database = Drawing(10000);
            DisplayList2 fresh = null,
                again = null;
            double layingOut = double.MaxValue,
                kept = double.MaxValue;
            CadDocument document = CadDocument.From(database, "mtext-10k.dxf");
            document.GetDisplay(document.ModelSpace);

            for (int run = 0; run < Runs; run++)
            {
                CadDocument other = CadDocument.From(database, "mtext-10k.dxf");
                layingOut = Math.Min(layingOut, Time(() => fresh = other.GetDisplay(other.ModelSpace)));
                document.Regen();
                kept = Math.Min(kept, Time(() => again = document.GetDisplay(document.ModelSpace)));
            }

            _output.WriteLine(
                "new document " + layingOut.ToString("0") + " ms, after REGEN " + kept.ToString("0") + " ms"
            );
            Assert.Equal(fresh.Count, again.Count);
            Assert.True(kept < layingOut * 0.8, "after REGEN " + kept + " ms of " + layingOut);
        }

        /// <summary>
        /// A layout kept is not taken once the MTEXT's style is made wider: "II II II" in a style of width 1, 10 high, is two
        /// lines in a width of 20; at width 2 each II is 34 wide and has a line of its own.
        /// </summary>
        [Fact]
        public void AStyleMadeWiderAfterADrawing_LaysTheWordsOutAgainAtTheRegen()
        {
            using (var folder = new FontFolder())
            {
                folder.Write("test.shx", ShxFiles.Unifont());
                var database = new DxfDatabase("AC1027");
                var style = new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" };
                database.TextStyleTable.Add(style);
                var mtext = new DxfMText
                {
                    Contents = "II II II",
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 10.0,
                    Width = 20.0,
                };
                database.ModelSpace.AppendEntity(mtext);
                mtext.TextStyleName = "GH_SHX";
                CadDocument document = CadDocument.From(database);
                document.Fonts = new CadFonts(new[] { folder.FullPath }, null, null, f => f == "Arial");
                Assert.Equal(new[] { "II II", "II" }, Lines(document));

                database.TextStyleTable["GH_SHX"].XScale = 2.0;
                document.Regen();
                Assert.Equal(new[] { "II", "II", "II" }, Lines(document));
            }
        }

        /// <summary>A layout kept is not taken once the MTEXT's words or width change: each drawing shows the words as they stand.</summary>
        [Fact]
        public void AnMTextGivenNewWordsOrANewWidth_IsLaidOutAgainAtTheRegen()
        {
            using (var folder = new FontFolder())
            {
                folder.Write("test.shx", ShxFiles.Unifont());
                var database = new DxfDatabase("AC1027");
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
                var mtext = new DxfMText
                {
                    Contents = "II II II",
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 10.0,
                    Width = 20.0,
                };
                database.ModelSpace.AppendEntity(mtext);
                mtext.TextStyleName = "GH_SHX";
                CadDocument document = CadDocument.From(database);
                document.Fonts = new CadFonts(new[] { folder.FullPath }, null, null, f => f == "Arial");
                Assert.Equal(new[] { "II II", "II" }, Lines(document));

                mtext.Width = 40.0;
                document.Regen();
                Assert.Equal(new[] { "II II II" }, Lines(document));

                mtext.Contents = "I I";
                document.Regen();
                Assert.Equal(new[] { "I I" }, Lines(document));
            }
        }

        private static string[] Lines(CadDocument document) =>
            document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .Single()
                .Parts.OfType<DisplayText2>()
                .Select(t => t.Text)
                .ToArray();

        private static DxfDatabase Drawing(int count)
        {
            var database = new DxfDatabase("AC1032");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ARIALS", FileName = "arial.ttf" });
            string[] pool =
            {
                "steel",
                "plate",
                "bolt",
                "M20",
                "grade",
                "8.8",
                "weld",
                "fillet",
                "6mm",
                "both",
                "sides",
            };

            for (int i = 0; i < count; i++)
            {
                var words = new StringBuilder();

                for (int w = 0; w < 3 + (i % 12); w++)
                {
                    words.Append(pool[((i * 7) + (w * 3)) % pool.Length]).Append(w % 5 == 4 ? "\\P" : " ");
                }

                if (i % 10 == 3)
                {
                    words.Append("{\\H1.5x;\\C1;big} \\S1/2;");
                }

                var mtext = new DxfMText
                {
                    Contents = words.ToString().TrimEnd(),
                    Location = new GeoPoint3((i % 100) * 50.0, (i / 100) * 20.0, 0.0),
                    TextHeight = 2.5,
                    Width = i % 3 == 0 ? 0.0 : 30.0,
                };
                database.ModelSpace.AppendEntity(mtext);

                if (i % 2 == 1)
                {
                    mtext.TextStyleName = "ARIALS";
                }
            }

            return database;
        }

        /// <summary>
        /// The milliseconds a drawing takes, timed after a full collection so that one owed by what ran before is not counted
        /// against it; the collections the drawing makes itself are counted, those of the layouts a document keeps with them.
        /// </summary>
        private static double Time(Action draw)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var watch = Stopwatch.StartNew();
            draw();
            return watch.Elapsed.TotalMilliseconds;
        }
    }
}
