using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The paragraph codes the MTEXT editor's ruler and Paragraph dialog write, each as AutoCAD 2021's own text editor wrote it
    /// for the same change (accoreconsole, its TextEditor saving an MTEXT 2.5 high), and read back as AutoCAD reads them; the
    /// editor's box (width, height, columns) and its column breaks.
    /// </summary>
    public sealed class MTextParagraphFormatTests : IDisposable
    {
        private const string Three = "A\tB\\PE\tF\\PG\tH";
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private static CadMTextParagraphFormat None => CadMTextParagraphFormat.Default;

        private static string All(Func<CadMTextParagraphFormat, CadMTextParagraphFormat> change)
        {
            var editor = new CadMTextEditor(Three, 2.5);
            editor.SelectAll();
            editor.SetFormat(change);
            return editor.Contents;
        }

        [Fact]
        public void TabStopsOfEachKind_AreWrittenInHeightsAfterX_AsAutoCADWritesThem()
        {
            string written = None.WithTabStops(
                    new[]
                    {
                        new CadMTextTabStop(10.0),
                        new CadMTextTabStop(20.0, 'c'),
                        new CadMTextTabStop(30.0, 'r'),
                        new CadMTextTabStop(40.0, 'd'),
                    }
                )
                .WriteAfter(None, 2.5);

            Assert.Equal(@"\pxt4,c8,r12,d16;", written);
            Assert.Equal(@"\pxt2.92;", None.WithTabStops(new[] { new CadMTextTabStop(7.3) }).WriteAfter(None, 2.5));
            Assert.Equal(
                @"\pxt2,4,r8,10;",
                None.WithTabStops(
                        new[]
                        {
                            new CadMTextTabStop(5.0),
                            new CadMTextTabStop(10.0),
                            new CadMTextTabStop(20.0, 'r'),
                            new CadMTextTabStop(25.0),
                        }
                    )
                    .WriteAfter(None, 2.5)
            );
            Assert.Equal(
                @"\pxtc1.5;",
                None.WithTabStops(new[] { new CadMTextTabStop(15.0, 'c') }).WriteAfter(None, 10.0)
            );
        }

        [Fact]
        public void ADecimalStopOnACommaOrASpace_IsWrittenWithACapitalD_AndReadBackWhole()
        {
            Assert.Equal(
                @"\pxtD,16,20;",
                None.WithTabStops(new[] { new CadMTextTabStop(40.0, 'd', ','), new CadMTextTabStop(50.0) })
                    .WriteAfter(None, 2.5)
            );
            Assert.Equal(
                @"\pxtD 16;",
                None.WithTabStops(new[] { new CadMTextTabStop(40.0, 'd', ' ') }).WriteAfter(None, 2.5)
            );

            CadMTextParagraphFormat read = CadMTextParagraphFormat.Read(None, @"\pxtD,16,20;", 2.5);
            Assert.Equal(2, read.TabStops.Count);
            Assert.Equal('d', read.TabStops[0].Kind);
            Assert.Equal(',', read.TabStops[0].DecimalSeparator);
            Assert.Equal(40.0, read.TabStops[0].Position, 9);
            Assert.Equal('l', read.TabStops[1].Kind);
            Assert.Equal(50.0, read.TabStops[1].Position, 9);
        }

        [Theory]
        [InlineData(5.0, 0.0, 0.0, @"\pxi2;")]
        [InlineData(0.0, 10.0, 0.0, @"\pxi-4,l4;")]
        [InlineData(0.0, 0.0, 7.0, @"\pxr2.8;")]
        public void TheFirstLineAndHangingIndents_AreWrittenAsIRelativeToL(
            double first,
            double hanging,
            double right,
            string written
        )
        {
            Assert.Equal(written, None.WithIndents(first, hanging, right).WriteAfter(None, 2.5));
        }

        [Fact]
        public void SpacingAndLineSpacing_AreWrittenAsAutoCADWritesThem_ExactlyAndAtLeastAsDistancesInHeights()
        {
            Assert.Equal(@"\pxb0.8,a1.2;", None.WithSpacing(2.0, 3.0).WriteAfter(None, 2.5));
            Assert.Equal(@"\pxsm1.5;", None.WithLineSpacing('m', 1.5).WriteAfter(None, 2.5));
            Assert.Equal(@"\pxse0.6;", None.WithLineSpacing('e', 1.5).WriteAfter(None, 2.5));
            Assert.Equal(@"\pxsa0.6;", None.WithLineSpacing('a', 1.5).WriteAfter(None, 2.5));
        }

        [Fact]
        public void EverythingAtOnce_IsWrittenInAutoCADsOrder_AndTheNextParagraphIsResetAsAutoCADResetsIt()
        {
            CadMTextParagraphFormat all = None.WithIndents(2.0, 10.0, 3.0)
                .WithSpacing(2.0, 3.0)
                .WithLineSpacing('m', 2.0)
                .WithTabStops(new[] { new CadMTextTabStop(15.0), new CadMTextTabStop(20.0, 'c') });

            Assert.Equal(@"\pxi-3.2,l4,r1.2,b0.8,a1.2,sm2,t6,c8;", all.WriteAfter(None, 2.5));
            Assert.Equal(@"\pi0,l0,r0,b0,a0,s*,tz;", None.WriteAfter(all, 2.5));
            Assert.Equal(string.Empty, all.WriteAfter(all, 2.5));
        }

        [Fact]
        public void CodesAreReadAsAutoCADReadsThem_XInHeights_NoXInUnits_AndCarriedOn()
        {
            CadMTextParagraphFormat a = CadMTextParagraphFormat.Read(None, @"\pxi-3,l3,t3,c5;", 2.5);
            Assert.Equal(0.0, a.FirstLineIndent, 9);
            Assert.Equal(7.5, a.HangingIndent, 9);
            Assert.Equal(new[] { 7.5, 12.5 }, a.TabStops.Select(t => Math.Round(t.Position, 9)).ToArray());
            Assert.Equal(new[] { 'l', 'c' }, a.TabStops.Select(t => t.Kind).ToArray());

            // Without x, distances: AutoCAD's own API read \pi1,l2,r3 as a first line at 3, the rest at 2, 3 from the right.
            CadMTextParagraphFormat b = CadMTextParagraphFormat.Read(None, @"\pi1,l2,r3;", 2.5);
            Assert.Equal(3.0, b.FirstLineIndent, 9);
            Assert.Equal(2.0, b.HangingIndent, 9);
            Assert.Equal(3.0, b.RightIndent, 9);

            CadMTextParagraphFormat e = CadMTextParagraphFormat.Read(None, @"\pxse2;", 2.5);
            Assert.Equal('e', e.LineSpacing);
            Assert.Equal(5.0, e.LineSpacingValue, 9);

            var editor = new CadMTextEditor(@"\pxt4;A\PB\P\pi0,tz;C", 2.5);
            Assert.Equal(10.0, editor.FormatOf(1).TabStops.Single().Position, 9);
            Assert.Empty(editor.FormatOf(2).TabStops);
        }

        [Fact]
        public void AFormatSetOnEveryParagraph_IsWrittenOnceAtTheStart()
        {
            Assert.Equal("\\pxt4;A\tB\\PE\tF\\PG\tH", All(f => f.WithTabStops(new[] { new CadMTextTabStop(10.0) })));
            Assert.Equal(
                "\\pxi-3.2,l4,r1.2,b0.8,a1.2,sm2,t6,c8;A\tB\\PE\tF\\PG\tH",
                All(f =>
                    f.WithIndents(2.0, 10.0, 3.0)
                        .WithSpacing(2.0, 3.0)
                        .WithLineSpacing('m', 2.0)
                        .WithTabStops(new[] { new CadMTextTabStop(15.0), new CadMTextTabStop(20.0, 'c') })
                )
            );
        }

        [Fact]
        public void AFormatSetOnTheMiddleParagraph_IsWrittenThere_AndTheNextIsPutBack()
        {
            var editor = new CadMTextEditor(Three, 2.5);
            editor.MoveTo(4);

            editor.SetFormat(f => f.WithIndents(2.0, 0.0, 0.0).WithTabStops(new[] { new CadMTextTabStop(10.0, 'r') }));

            // As AutoCAD wrote the same change: A^IB^IC^ID\P\pxi0.8,tr4;E^IF\P\pi0,tz;G^IH.
            Assert.Equal("A\tB\\P\\pxi0.8,tr4;E\tF\\P\\pi0,tz;G\tH", editor.Contents);
            Assert.True(editor.Undo());
            Assert.Equal(Three, editor.Contents);
        }

        [Fact]
        public void AFormatSetOnTheFirstParagraph_ResetsTheSecond()
        {
            var editor = new CadMTextEditor(Three, 2.5);

            editor.SetFormat(f => f.WithIndents(0.0, 4.0, 0.0).WithTabStops(new[] { new CadMTextTabStop(10.0, 'r') }));

            // AutoCAD: \pxi-1.6,l1.6,tr4;A^IB^IC^ID\P\pi0,l0,tz;E^IF\PG^IH.
            Assert.Equal("\\pxi-1.6,l1.6,tr4;A\tB\\P\\pi0,l0,tz;E\tF\\PG\tH", editor.Contents);
        }

        [Fact]
        public void ADragOfTheRuler_IsOneUndoStepWhenReleased_AndNothingWhenPutBack()
        {
            var editor = new CadMTextEditor("A\tB", 2.5);
            editor.BeginChange();

            for (int i = 1; i <= 5; i++)
            {
                editor.SetFormat(f => f.WithTabStops(new[] { new CadMTextTabStop(i * 2.0) }));
            }

            Assert.True(editor.IsChanging);
            Assert.Equal("\\pxt4;A\tB", editor.Contents);
            editor.EndChange(true);
            Assert.True(editor.Undo());
            Assert.Equal("A\tB", editor.Contents);
            Assert.False(editor.CanUndo);

            editor.BeginChange();
            editor.SetFrame(CadMTextFrame.Of(30.0));
            editor.SetFormat(f => f.WithIndents(5.0, 5.0, 0.0));
            editor.EndChange(false);
            Assert.Equal("A\tB", editor.Contents);
            Assert.Equal(0.0, editor.Frame.Width, 9);
            Assert.False(editor.CanUndo);
        }

        [Fact]
        public void TheBox_IsPartOfTheEditorsUndo_AndStaticColumnsShareANewTotalKeepingTheGutter()
        {
            var editor = new CadMTextEditor("A", 2.5, CadMTextFrame.Of(55.0));
            editor.SetFrame(editor.Frame.WithWidth(45.0));
            Assert.Equal(45.0, editor.Frame.Width, 9);
            Assert.True(editor.Undo());
            Assert.Equal(55.0, editor.Frame.Width, 9);

            CadMTextFrame columns = CadMTextFrame
                .Of(55.0)
                .WithColumns(DxfColumnType.StaticColumns, 40.0, 12.5, 3, false)
                .WithWidth(145.0);
            Assert.Equal(40.0, columns.ColumnWidth, 9);
            Assert.Equal(12.5, columns.Gutter, 9);
            Assert.Equal(145.0, columns.Width, 9);

            CadMTextFrame none = columns.WithColumns(DxfColumnType.NoColumns, columns.Width, 0.0, 1, false);
            Assert.False(none.HasColumns);
            Assert.Equal(145.0, none.Width, 9);
        }

        [Fact]
        public void AColumnBreak_IsKeptAsAColumnBreak_AndAltEnterWritesOne()
        {
            var kept = new CadMTextEditor(@"A\NB", 2.5);
            kept.MoveTo(3);
            kept.Type("C");
            Assert.Equal(@"A\NBC", kept.Contents);

            var editor = new CadMTextEditor("AB", 2.5);
            editor.MoveTo(1);
            editor.InsertColumnBreak();
            Assert.Equal(@"A\NB", editor.Contents);
            Assert.Equal(2, editor.ParagraphCount);
        }

        private CadDocument Document(string contents, out DxfMText mtext)
        {
            _folder.Write("test.shx", ShxFiles.Unifont());
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_SHX", FileName = "test.shx" });
            mtext = new DxfMText
            {
                Contents = contents,
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 10.0,
                Width = 0.0,
            };
            database.ModelSpace.AppendEntity(mtext);
            mtext.TextStyleName = "GH_SHX";
            CadDocument document = CadDocument.From(database);
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null, f => f == "Arial");
            return document;
        }

        [Theory]
        [InlineData("\\pxtD,4;I\tII,I", 33.0)]
        [InlineData("\\pxtd4;I\tII,I", 25.0)]
        [InlineData("\\pxtd4;I\tII.I", 33.0)]
        public void ADecimalStop_SetsTheWordOnItsOwnSeparator_OrItsEndWithoutOne(string contents, double foot)
        {
            // AutoCAD 2021 exploded \pxtD,16;A^I12,5 with 12,5 set as \pxtd16;A^I12.5 is, and 12.5 under D, as 12,5 under d:
            // on its end. Here a stop at 40; II is 8 across, a letter 4, an I's line 1 in from its pen.
            CadDocument document = Document(contents, out _);
            DisplayText2 shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();
            DisplayText2 word = shown.Parts.OfType<DisplayText2>().ToArray()[1];
            Assert.Equal(foot, word.Strokes[0][0], 9);
        }
    }
}
