using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// Every test drawing read from its DWG and compared, record by record, with the DXF AutoCAD wrote of the same
    /// drawing; <see cref="DwgAnswerKey"/> says how the drawings were made and which differences are allowed.
    /// </summary>
    public class DwgAnswerKeyTests : IDisposable
    {
        /// <summary>The types the reader does not read yet, which a comparison may find missing.</summary>
        private static readonly string[] NotReadYet =
        {
            "ACDBDETAILVIEWSTYLE", "ACDBSECTIONVIEWSTYLE", "CELLSTYLEMAP", "MATERIAL", "TABLESTYLE", "VISUALSTYLE",
        };

        private readonly List<(GeometryHelperLogLevel Level, string Message)> _written = new List<(GeometryHelperLogLevel Level, string Message)>();

        public DwgAnswerKeyTests()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => _written.Add((level, message));
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            GeometryHelperLog.Enable = true;
        }

        [Theory]
        [InlineData("empty", 150)]
        [InlineData("curves", 150)]
        [InlineData("polys", 150)]
        [InlineData("text", 150)]
        [InlineData("blocks", 150)]
        [InlineData("props", 150)]
        [InlineData("later", 150)]
        public void ADrawing_ReadFromItsDwg_IsTheDxfAutoCadWroteOfIt(string name, int atLeast)
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(name), DwgReader.ToDxf(DwgAnswerKey.Dwg(name)));

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet));
            Assert.True(result.Paired >= atLeast, "Only " + result.Paired + " records were paired.");
            Assert.DoesNotContain(_written, line => line.Level == GeometryHelperLogLevel.Warn);
        }

        [Theory]
        [InlineData("curves", "AC1018")]
        [InlineData("blocks", "AC1018")]
        [InlineData("props", "AC1018")]
        [InlineData("curves", "AC1021")]
        [InlineData("props", "AC1021")]
        [InlineData("curves", "AC1024")]
        [InlineData("blocks", "AC1024")]
        [InlineData("props", "AC1024")]
        [InlineData("curves", "AC1027")]
        [InlineData("blocks", "AC1027")]
        [InlineData("props", "AC1027")]
        [InlineData("curves", "AC1032")]
        [InlineData("blocks", "AC1032")]
        [InlineData("props", "AC1032")]
        public void ADrawingOfALaterVersion_ReadFromItsDwg_IsTheDxfAutoCadWroteOfIt(string name, string version)
        {
            string drawing = name + "_" + version;
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(drawing), DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing)));

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet).Except(NotReadYet.Select(type => "CLASS " + type)));
            Assert.True(result.Paired >= 300, "Only " + result.Paired + " records were paired.");
            Assert.DoesNotContain(_written, line => line.Level == GeometryHelperLogLevel.Warn);
        }

        [Theory]
        [InlineData("vports_AC1015")]
        [InlineData("vports_AC1032")]
        [InlineData("hatch_AC1015")]
        [InlineData("hatch_AC1032")]
        [InlineData("leaders_AC1015")]
        [InlineData("leaders_AC1032")]
        [InlineData("images_AC1015")]
        [InlineData("images_AC1032")]
        public void ADrawingOfTheTypesOfW4_ReadFromItsDwg_IsTheDxfAutoCadWroteOfIt(string drawing)
        {
            // Made by probe GhDwg4 in all six versions; the repository holds the oldest and the newest of each.
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key(drawing), DwgReader.ToDxf(DwgAnswerKey.Dwg(drawing)));

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet).Except(NotReadYet.Select(type => "CLASS " + type)));
            Assert.True(result.Paired >= 300, "Only " + result.Paired + " records were paired.");
            Assert.DoesNotContain(_written, line => line.Level == GeometryHelperLogLevel.Warn);
        }

        [Theory]
        [InlineData("vports_AC1015")]
        [InlineData("vports_AC1032")]
        public void TheViewportsOfTheCurrentLayout_AreRead_NumberedFromThePaper_WithFrozenLayersAndAClipBoundary(string drawing)
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg(drawing), false));
            List<DxfViewport> viewports = database.PaperSpace.OfType<DxfViewport>().ToList();

            // Layout1 holds the viewport of the paper, 12 by 9, and three more drawn in it.
            Assert.Equal(4, viewports.Count);
            DxfViewport paper = Assert.Single(viewports, v => v.Number == 1);
            Assert.Equal(12.0, paper.Width, 9);
            Assert.Equal(9.0, paper.Height, 9);
            Assert.Contains(viewports, v => v.GetFrozenLayers().Count == 1 && v.Locked);
            Assert.Contains(viewports, v => v.NonRectClipOn);
            Assert.Contains(viewports, v => Math.Abs(v.LensLength - 35.0) < 1E-9);
        }

        [Theory]
        [InlineData("hatch_AC1015", false)]
        [InlineData("hatch_AC1032", true)]
        public void TheHatches_AreRead_WithEveryKindOfBoundaryPath(string drawing, bool gradients)
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg(drawing), false));
            List<DxfHatch> hatches = database.ModelSpace.OfType<DxfHatch>().ToList();

            // ANSI31 over a polyline with a bulge, a double user pattern, a solid fill bounded by a line, an arc, a spline and
            // an elliptic arc, a gradient, ANSI37 with an island, NET following a circle, and a tilted solid fill.
            Assert.Equal(7, hatches.Count);
            Assert.Equal(new[] { "ANSI31", "_USER", "SOLID", "SOLID", "ANSI37", "NET", "SOLID" }, hatches.Select(h => h.PatternName));
            Assert.Equal(2, hatches[4].NumberOfLoops);
            Assert.True(hatches[5].Associative);
            Assert.Equal(5.0, hatches[6].Elevation, 9);

            // R2000 has no gradient: AutoCAD saves the gradient as a solid fill.
            Assert.Equal(gradients, hatches[3].IsGradient);
        }

        [Theory]
        [InlineData("leaders_AC1015")]
        [InlineData("leaders_AC1032")]
        public void TheLeaders_Multileaders_AndFeatureControlFrames_AreRead(string drawing)
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg(drawing), false));

            // A leader of three points annotated by an MTEXT and a spline leader of four; two frames.
            List<DxfLeader> leaders = database.ModelSpace.OfType<DxfLeader>().ToList();
            Assert.Equal(new[] { 3, 4 }, leaders.Select(l => l.NumVertices));
            Assert.Equal(2, database.ModelSpace.OfType<DxfUnknownEntity>().Count(e => e.DxfName == "TOLERANCE"));

            // Two multileaders of text and one of the block GhTag under the style GhBlock.
            List<DxfMLeader> multileaders = database.ModelSpace.OfType<DxfMLeader>().ToList();
            Assert.Equal(new[] { DxfContentType.MTextContent, DxfContentType.MTextContent, DxfContentType.BlockContent }, multileaders.Select(m => m.ContentType));
            Assert.Equal("Multi\\Pleader", multileaders[0].MText.Contents);
        }

        [Theory]
        [InlineData("images_AC1015")]
        [InlineData("images_AC1032")]
        public void TheImages_AndWipeouts_AreRead_WithTheirDefinitionAndClipBoundary(string drawing)
        {
            DxfDatabase database = DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg(drawing), false));

            // Two images of one PNG of 16 by 8 pixels, the second clipped by a triangle; two wipeouts.
            List<DxfRasterImage> images = database.ModelSpace.OfType<DxfRasterImage>().Where(i => !(i is DxfWipeout)).ToList();
            Assert.Equal(2, images.Count);
            Assert.All(images, image => Assert.EndsWith("gh.png", image.Path, StringComparison.Ordinal));
            Assert.True(images[1].IsClipped);
            Assert.Equal(4, images[1].GetClipBoundary().Count);
            Assert.Equal(2, database.ModelSpace.OfType<DxfWipeout>().Count());
        }

        [Fact]
        public void EveryDrawingOfEveryVersion_InTheFolderOfTheAnswerKeys_IsTheDxfAutoCadWroteOfIt()
        {
            // The full set, eight drawings in five versions with their keys, is too large for the repository; it runs where
            // DWG_ANSWER_KEYS names a folder that holds it (name_AC10xx.dwg and .dxf), and passes empty elsewhere.
            string folder = Environment.GetEnvironmentVariable("DWG_ANSWER_KEYS");

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            var faults = new List<string>();

            foreach (string dwg in Directory.GetFiles(folder, "*_AC10??.dwg").OrderBy(path => path, StringComparer.Ordinal))
            {
                string dxf = Path.ChangeExtension(dwg, ".dxf");
                byte[] file = File.ReadAllBytes(dwg);

                if (!File.Exists(dxf))
                {
                    continue;
                }

                string[] ignored = Path.GetFileName(dwg).StartsWith("tables_", StringComparison.Ordinal) ? new[] { "$TEXTSIZE" } : new string[0];
                DwgAnswerKey.Result result = DwgAnswerKey.Compare(File.ReadAllBytes(dxf), DwgReader.ToDxf(file), ignored);
                IEnumerable<string> missing = result.Missing.Keys.Except(NotReadYet).Except(NotReadYet.Select(type => "CLASS " + type));
                faults.AddRange(result.Differences.Concat(missing).Select(fault => Path.GetFileName(dwg) + ": " + fault));
            }

            Assert.Empty(faults);
        }

        [Fact]
        public void ADwgOfR2004_LabelledAsR2007_IsRefusedAsBroken()
        {
            // AC1021 is read through its own container, which an R2004 file does not have.
            byte[] file = DwgAnswerKey.Dwg("curves_AC1018");
            Encoding.ASCII.GetBytes("AC1021").CopyTo(file, 0);

            Assert.Throws<FormatException>(() => DwgReader.ToDxf(file));
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1032")]
        public void ALayerNamedInVietnamese_ComesOutWithItsLetters(string version)
        {
            // Before R2007 the name is in the code page of the drawing, from R2007 UTF-16 in the string stream of the layer.
            DxfDatabase database = DwgReader.Open(new MemoryStream(DwgAnswerKey.Dwg("props_" + version), false));

            Assert.Contains(database.LayerTable, layer => layer.Name == "Lớp ẩn");
        }

        [Fact]
        public void TheDrawingOfTheTables_IsTheDxfAutoCadWroteOfIt_ButForTheTextSizeAutoCadChanged()
        {
            // The DWG holds the text size 3.5 the drawing was given; AutoCAD wrote 0.2 after reading it back.
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(DwgAnswerKey.Key("tables"), DwgReader.ToDxf(DwgAnswerKey.Dwg("tables")), "$TEXTSIZE");

            Assert.Empty(result.Differences);
            Assert.Empty(result.Missing.Keys.Except(NotReadYet));
            Assert.True(result.Paired >= 150, "Only " + result.Paired + " records were paired.");
        }

        [Fact]
        public void TheComparison_FindsAChangedNumber_AChangedText_AndAMissingRecord()
        {
            byte[] key = DwgAnswerKey.Key("curves");
            string text = System.Text.Encoding.GetEncoding(28591).GetString(key);
            int line = text.IndexOf("\r\nLINE\r\n", StringComparison.Ordinal);
            int circle = text.IndexOf("\r\nCIRCLE\r\n", StringComparison.Ordinal);
            Assert.True(line > 0 && circle > 0);

            // The end of the line moved from 100 to 100.000001: more than the comparison lets pass.
            int eleven = text.IndexOf("\r\n 11\r\n100.0\r\n", line, StringComparison.Ordinal);
            Assert.True(eleven > 0);
            string moved = text.Substring(0, eleven) + "\r\n 11\r\n100.000001\r\n" + text.Substring(eleven + "\r\n 11\r\n100.0\r\n".Length);

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(key, System.Text.Encoding.GetEncoding(28591).GetBytes(moved));

            Assert.Single(result.Differences);
            Assert.Contains("LINE", result.Differences[0]);

            // The circle taken out of the entities: from its 0 group to the next.
            int next = text.IndexOf("\r\n  0\r\n", circle + 4, StringComparison.Ordinal);
            int start = text.LastIndexOf("\r\n  0", circle, StringComparison.Ordinal);
            string without = text.Substring(0, start) + text.Substring(next);

            result = DwgAnswerKey.Compare(key, System.Text.Encoding.GetEncoding(28591).GetBytes(without));

            Assert.Equal(1, result.Missing["CIRCLE"]);
        }

        [Fact]
        public void TheTypesNotReadYet_AreCountedInOneDebugLine_AndTheCallInOneInfoLine()
        {
            DwgReader.ToDxf(DwgAnswerKey.Dwg("later"));

            string skipped = Assert.Single(_written, line => line.Level == GeometryHelperLogLevel.Debug && line.Message.StartsWith("DwgReader:", StringComparison.Ordinal)).Message;
            Assert.DoesNotContain(" HATCH ", skipped);
            Assert.DoesNotContain(" LEADER ", skipped);
            Assert.Contains("MATERIAL 3", skipped);

            string summary = Assert.Single(_written, line => line.Level == GeometryHelperLogLevel.Info && line.Message.StartsWith("DwgReader:", StringComparison.Ordinal)).Message;
            Assert.Contains("AutoCAD 2000", summary);
            Assert.Contains("0 broken", summary);
        }
    }
}
