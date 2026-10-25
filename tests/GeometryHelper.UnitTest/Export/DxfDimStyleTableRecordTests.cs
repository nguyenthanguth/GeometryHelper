using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The dimension style table: every variable read by its group as ezdxf 1.4.5 reads it, set in its record, a style added,
    /// renamed and erased, on the drawings of <see cref="DxfDimensionDrawings"/>.
    /// </summary>
    public class DxfDimStyleTableRecordTests
    {
        #region Reading

        [Fact]
        public void TheStyleGH_DIMOfR2018_ReadsEveryVariableAsEzdxfReadsIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimStyleTableRecord style = db.DimStyleTable["gh_dim"];

            // ezdxf: dimasz=3.5 dimexo=0.75 dimgap=1.1/0x1.199999999999ap+0 dimtfac=0.8/0x1.999999999999ap-1 dimjogang=60.0
            // dimtfill=2 dimtfillclr=5 dimblk='_ARCHTICK' dimblk1='_DOT' dimblk2='_OPEN' dimldrblk='_OPEN30' dimtxsty='GH_TXT'
            // dimltype='DASHED' dimltex1='CENTER' dimltex2='DASHED' dimlwd=35 dimlwe=50 dimdsep=44 dimzin=12 dimcen=-2.0.
            Assert.Equal("GH_DIM", style.Name);
            Assert.Equal(3.5, style.Dimasz);
            Assert.Equal(0.75, style.Dimexo);
            Assert.Equal(Bits(FromBits(0x3ff199999999999a)), Bits(style.Dimgap));
            Assert.Equal(Bits(FromBits(0x3fe999999999999a)), Bits(style.Dimtfac));
            Assert.Equal(60.0 * Math.PI / 180.0, style.Dimjogang, 15);
            Assert.Equal(2, style.Dimtfill);
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5), style.Dimtfillclr);
            Assert.Equal("_ARCHTICK", style.Dimblk.Name);
            Assert.Equal("_DOT", style.Dimblk1.Name);
            Assert.Equal("_OPEN", style.Dimblk2.Name);
            Assert.Equal("_OPEN30", style.Dimldrblk.Name);
            Assert.Equal("GH_TXT", style.Dimtxsty.Name);
            Assert.Equal("DASHED", style.Dimltype.Name);
            Assert.Equal("CENTER", style.Dimltex1.Name);
            Assert.Equal("DASHED", style.Dimltex2.Name);
            Assert.Equal(DxfLineWeight.LineWeight035, style.Dimlwd);
            Assert.Equal(DxfLineWeight.LineWeight050, style.Dimlwe);
            Assert.Equal(',', style.Dimdsep);
            Assert.Equal(12, style.Dimzin);
            Assert.Equal(-2.0, style.Dimcen);
            // ezdxf writes no DIMPOST (3) or DIMAPOST (4) from R2007 on, and reads none back: empty, as AutoCAD's own.
            Assert.Equal(string.Empty, style.Dimpost);
            Assert.Equal("<> mm", DxfDatabase.Parse(DxfDimensionDrawings.R2000).DimStyleTable["GH_DIM"].Dimpost);
            Assert.Equal("[]in", DxfDatabase.Parse(DxfDimensionDrawings.R2000).DimStyleTable["GH_DIM"].Dimapost);
            Assert.True(style.Dimsah);
            Assert.True(style.DimfxlenOn);
            Assert.Equal(3.0, style.Dimfxlen);
            Assert.Equal(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), style.Dimclrd);
            Assert.Equal(1, style.Dimarcsym);
            Assert.Equal("_DOT", style.GetArrow(DxfDimArrowFlag.FirstArrow).Name);
            Assert.Equal("_OPEN", style.GetArrow(DxfDimArrowFlag.SecondArrow).Name);
            Assert.Equal(new[] { "Standard", "GH_DIM", "GH_EMPTY" }, db.DimStyleTable.Select(s => s.Name));
        }

        [Fact]
        public void TheStylesOfR12_ReadTheirArrowsByName_AndWhatR12HasNotAsAutoCAD()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];

            // R12 names the arrowheads ARCHTICK, DOT and OPEN, AutoCAD's names of the blocks _ARCHTICK, _DOT and _OPEN.
            Assert.Equal("_ARCHTICK", style.Dimblk.Name);
            Assert.Equal("_DOT", style.Dimblk1.Name);
            Assert.Equal("_OPEN", style.Dimblk2.Name);
            Assert.Equal(3.5, style.Dimasz);

            // R12 has no DIMDEC, DIMTXSTY, DIMLDRBLK or DIMJOGANG: AutoCAD's own values.
            DxfDimStyleTableRecord standard = db.DimStyleTable["Standard"];
            Assert.Equal(4, standard.Dimdec);
            Assert.Null(standard.Dimtxsty);
            Assert.Null(standard.Dimldrblk);
            Assert.Null(standard.Dimblk);
            Assert.Equal(Math.PI / 4.0, standard.Dimjogang, 15);
            Assert.Equal(DxfColor.ByBlock, standard.Dimtfillclr);
            Assert.Equal('.', standard.Dimdsep);
            Assert.Equal(DxfLineWeight.ByBlock, standard.Dimlwd);
        }

        [Fact]
        public void AStyleMadeInCode_HoldsAutoCADsOwnValues()
        {
            var style = new DxfDimStyleTableRecord();

            Assert.Equal(0.18, style.Dimasz);
            Assert.Equal(0.18, style.Dimtxt);
            Assert.Equal(0.0625, style.Dimexo);
            Assert.Equal(4, style.Dimdec);
            Assert.Equal(25.4, style.Dimaltf);
            Assert.True(style.Dimtih);
            Assert.True(style.Dimtoh);
            Assert.Equal(3, style.Dimatfit);
            Assert.Equal(1, style.Dimtolj);
            Assert.Equal(DxfColor.ByBlock, style.Dimclrd);
            Assert.Null(style.Dimblk);
            Assert.Equal(string.Empty, style.Dimpost);
        }

        #endregion

        #region Setting

        [Fact]
        public void EverySetterGivenItsOwnValue_ChangesNothing()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                byte[] before = Saved(db);

                foreach (DxfDimStyleTableRecord style in db.DimStyleTable)
                {
                    foreach (var property in typeof(DxfDimStyleTableRecord).GetProperties().Where(p => p.CanWrite && p.Name.StartsWith("Dim", StringComparison.Ordinal)))
                    {
                        property.SetValue(style, property.GetValue(style));
                    }

                    style.Name = style.Name;
                }

                Assert.Equal(before, Saved(db));
            }
        }

        [Fact]
        public void AVariableSet_IsWrittenInItsGroup_TheSecond70ForDimtfillclr_AndTheRestKept()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];

            style.Dimasz = 5.0;
            style.Dimtfillclr = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            style.Dimjogang = Math.PI / 6.0;
            style.Dimblk = db.BlockTable["_OPEN30"];
            style.Dimtxsty = db.TextStyleTable["GH_TXT2"];
            style.Dimltype = null;
            style.Dimdsep = '.';

            string saved = db.ToText();
            OnlyStylesChanged(text, saved, "33");
            string record = StyleRecord(saved, "33");
            Assert.Contains("\n 41\n5\n", record, StringComparison.Ordinal);
            Assert.Contains("\n 69\n2\n 70\n3\n", record, StringComparison.Ordinal);
            Assert.Contains("\n  2\nGH_DIM\n 70\n0\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n345\n", record, StringComparison.Ordinal);

            DxfDimStyleTableRecord read = DxfDatabase.Parse(saved).DimStyleTable["GH_DIM"];
            Assert.Equal(5.0, read.Dimasz);
            Assert.Equal(3, read.Dimtfillclr.ColorIndex);
            Assert.Equal(Math.PI / 6.0, read.Dimjogang, 15);
            Assert.Equal("_OPEN30", read.Dimblk.Name);
            Assert.Equal("GH_TXT2", read.Dimtxsty.Name);
            Assert.Null(read.Dimltype);
            Assert.Equal('.', read.Dimdsep);
            Assert.Equal(2, read.Dimtfill);
        }

        [Fact]
        public void AVariableTheRecordHasNot_IsPutInWhereAutoCADWritesIt()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord standard = db.DimStyleTable["Standard"];

            Assert.Equal(0, standard.Dimarcsym);
            standard.Dimarcsym = 2;

            string record = StyleRecord(db.ToText(), "2B");
            Assert.Contains("\n 79\n3\n 90\n2\n170\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void AVariableAFileIsTooOldFor_IsRefused()
        {
            DxfDatabase r12 = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            Refused<InvalidOperationException>(() => r12.DimStyleTable["Standard"].Dimdec = 2, "DIMDEC, which came with R2000");

            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            Refused<InvalidOperationException>(() => r2000.DimStyleTable["Standard"].Dimtfill = 1, "DIMTFILL, which came with R2007");
            Refused<ArgumentException>(() => r2000.DimStyleTable["Standard"].Dimclrd = DxfColor.FromRgb(255, 128, 0), "colour of AutoCAD's index");
            Refused<ArgumentOutOfRangeException>(() => r2000.DimStyleTable["Standard"].Dimasz = double.NaN, "finite number");
            Refused<ArgumentException>(() => r2000.DimStyleTable["Standard"].Dimblk = DxfDatabase.Parse(DxfDimensionDrawings.R2018).BlockTable["_OPEN"], "same drawing");

            // Nothing was written.
            Assert.Equal(DxfDimensionDrawings.R12, r12.ToText());
            Assert.Equal(DxfDimensionDrawings.R2000, r2000.ToText());
        }

        [Fact]
        public void AValueTheFileHoldsOutOfRange_SetAsItIsRead_WritesNothing()
        {
            // DIMLWD 7, which is none of AutoCAD's lineweights, and DIMATFIT 9: given back what they read, nothing is checked or written.
            string text = DxfDimensionDrawings.R2018.Replace("372\n50\n", "372\n7\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];

            Assert.Equal((DxfLineWeight)7, style.Dimlwe);
            style.Dimlwe = style.Dimlwe;
            Assert.Equal(text, db.ToText());
            Refused<ArgumentOutOfRangeException>(() => style.Dimlwe = (DxfLineWeight)8, "lineweights");
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnArrowOfR12_IsWrittenByItsName()
        {
            string text = DxfDimensionDrawings.R12;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDimStyleTableRecord style = db.DimStyleTable["GH_DIM"];

            style.Dimblk = db.BlockTable["_DOT"];

            Assert.Contains("\n  5\n_DOT\n  6\nDOT\n", StyleRecord(db.ToText(), "31"), StringComparison.Ordinal);
            Assert.Equal("_DOT", DxfDatabase.Parse(db.ToText()).DimStyleTable["GH_DIM"].Dimblk.Name);
        }

        #endregion

        #region Adding, renaming and erasing

        [Fact]
        public void AStyleAdded_IsWrittenWhole_ItsHandleAs105_AndReadBack()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                bool r12 = text == DxfDimensionDrawings.R12;
                var made = new DxfDimStyleTableRecord { Name = "GH_NEW", Dimasz = 2.5, Dimtxt = 3.5, Dimblk = db.BlockTable["_DOT"] };

                if (!r12)
                {
                    made.Dimtxsty = db.TextStyleTable["GH_TXT"];
                    made.Dimdec = 1;
                }

                db.DimStyleTable.Add(made);

                Assert.Same(made, db.DimStyleTable["gh_new"]);
                Refused<ArgumentException>(() => db.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = "Gh_New" }), "has a record named Gh_New already");
                string saved = db.ToText();
                string record = Records(saved).Single(r => r.Contains("\n  2\nGH_NEW\n"));
                Assert.StartsWith("  0\nDIMSTYLE\n105\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("\n  5\n" + made.Handle, record, StringComparison.Ordinal);
                Assert.Contains("\n  2\nDIMSTYLE\n", saved, StringComparison.Ordinal);

                DxfDimStyleTableRecord read = DxfDatabase.Parse(saved).DimStyleTable["GH_NEW"];
                Assert.Equal(2.5, read.Dimasz);
                Assert.Equal(3.5, read.Dimtxt);
                Assert.Equal(0.0625, read.Dimexo);
                Assert.Equal("_DOT", read.Dimblk.Name);
                Assert.Equal(r12 ? 4 : 1, read.Dimdec);
                Assert.Equal(r12 ? null : "GH_TXT", read.Dimtxsty?.Name);
            }
        }

        [Fact]
        public void AStyleAdded_IsCountedAndListedInTheTablesHead_AsAutoCADWritesIt()
        {
            string text = AutoCADHead();
            DxfDatabase db = DxfDatabase.Parse(text);
            var made = new DxfDimStyleTableRecord { Name = "GH_NEW" };

            db.DimStyleTable.Add(made);
            string saved = db.ToText();
            string handle = made.Handle;

            Assert.Contains("100\nAcDbDimStyleTable\n 71\n2\n340\n27\n340\n" + handle + "\n", saved, StringComparison.Ordinal);
            Assert.Contains("\n 70\n2\n100\nAcDbDimStyleTable", saved, StringComparison.Ordinal);

            made.Erase();
            string erased = db.ToText();
            Assert.Contains("100\nAcDbDimStyleTable\n 71\n1\n340\n27\n  0\nDIMSTYLE", erased, StringComparison.Ordinal);
            Assert.DoesNotContain("GH_NEW", erased, StringComparison.Ordinal);
        }

        [Fact]
        public void AStyleRenamed_TakesItsDimensionsAndItsKindsWithIt()
        {
            string text = DxfDimensionDrawings.R2018.Replace("  2\nGH_EMPTY\n", "  2\nGH_DIM$0\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            db.DimStyleTable["GH_DIM"].Name = "GH_25";

            string saved = db.ToText();
            Assert.DoesNotContain("GH_DIM", saved, StringComparison.Ordinal);
            Assert.Contains("\n  2\nGH_25$0\n", saved, StringComparison.Ordinal);
            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.Equal("GH_25", read.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "4D").DimensionStyleName);
            Assert.Equal(3.5, read.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "80").Dimasz);
            Refused<ArgumentException>(() => db.DimStyleTable["GH_25"].Name = "standard", "has a style named standard already");
        }

        [Fact]
        public void AStyleNothingIsDrawnIn_IsErased_AndOneInUseOrStandardIsKept()
        {
            string text = DxfDimensionDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);

            Refused<InvalidOperationException>(() => db.DimStyleTable["Standard"].Erase(), "which AutoCAD keeps");
            Refused<InvalidOperationException>(() => db.DimStyleTable["GH_DIM"].Erase(), "drawn in it");
            DxfDimStyleTableRecord empty = db.DimStyleTable["GH_EMPTY"];
            empty.Erase();

            Assert.True(empty.IsErased);
            Assert.False(db.DimStyleTable.Has("GH_EMPTY"));
            Refused<InvalidOperationException>(() => empty.Dimasz = 3.0, "is erased");
            string saved = db.ToText();
            Assert.DoesNotContain("GH_EMPTY", saved, StringComparison.Ordinal);
            Assert.Contains("  2\nDIMSTYLE\n  5\n4\n330\n0\n100\nAcDbSymbolTable\n 70\n2\n", saved, StringComparison.Ordinal);
        }

        #endregion

        /// <summary>A drawing of R2000 whose DIMSTYLE table lists its one style in its head (71, 340), as AutoCAD writes one.</summary>
        private static string AutoCADHead()
            => "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1015\n  9\n$HANDSEED\n  5\n100\n  0\nENDSEC\n"
            + "  0\nSECTION\n  2\nTABLES\n"
            + "  0\nTABLE\n  2\nDIMSTYLE\n  5\nA\n330\n0\n100\nAcDbSymbolTable\n 70\n1\n100\nAcDbDimStyleTable\n 71\n1\n340\n27\n"
            + "  0\nDIMSTYLE\n105\n27\n330\nA\n100\nAcDbSymbolTableRecord\n100\nAcDbDimStyleTableRecord\n  2\nStandard\n 70\n0\n"
            + "  0\nENDTAB\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n";

        /// <summary>The DIMSTYLE record of a handle (its 105) in a text.</summary>
        private static string StyleRecord(string text, string handle)
            => Records(text).Single(r => r.StartsWith("  0\nDIMSTYLE\n105\n" + handle + "\n", StringComparison.Ordinal));

        /// <summary>The untouched rule for DIMSTYLE records, whose handles are 105: only those of the handles given differ.</summary>
        private static void OnlyStylesChanged(string before, string after, params string[] handles)
        {
            Differ(before, after, out List<string> gone, out List<string> come);
            string[] changed = gone.Concat(come).Where(r => !r.Contains("\nHEADER\n")).Select(r => r.StartsWith("  0\nDIMSTYLE\n105\n", StringComparison.Ordinal) ? r.Split('\n')[3].Trim() : HandleOf(r)).Distinct().OrderBy(h => h, StringComparer.Ordinal).ToArray();
            Assert.Equal(handles.OrderBy(h => h, StringComparer.Ordinal).ToArray(), changed);
        }
    }
}
